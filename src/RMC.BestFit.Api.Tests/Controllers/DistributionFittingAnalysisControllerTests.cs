using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Numerics.Distributions;
using RMC.BestFit.Api.Configuration;
using RMC.BestFit.Api.Controllers;
using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Services;
using RMC.BestFit.Api.Store;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.Controllers
{
    /// <summary>
    /// Unit tests for <see cref="DistributionFittingAnalysisController"/> status codes and
    /// payloads, using the real service over an in-memory store. Analyses are created and
    /// validated but never run (no MLE).
    /// </summary>
    [TestClass]
    public class DistributionFittingAnalysisControllerTests
    {
        /// <summary>
        /// The store backing the controller stack.
        /// </summary>
        private InMemoryResourceStore _store = null!;

        /// <summary>
        /// The controller under test.
        /// </summary>
        private DistributionFittingAnalysisController _controller = null!;

        /// <summary>
        /// Creates a fresh controller stack before each test.
        /// </summary>
        [TestInitialize]
        public void Initialize()
        {
            _store = new InMemoryResourceStore(Options.Create(new ApiOptions()));
            var service = new AnalysisService(_store, Options.Create(new ApiOptions()));
            _controller = new DistributionFittingAnalysisController(NullLogger<DistributionFittingAnalysisController>.Instance, service);
        }

        /// <summary>
        /// Extracts status code and body from an action result.
        /// </summary>
        /// <typeparam name="TResponse">The response DTO type.</typeparam>
        /// <param name="actionResult">The action result.</param>
        /// <returns>The status code and body.</returns>
        private static (int StatusCode, TResponse Body) Unwrap<TResponse>(ActionResult<TResponse> actionResult)
            where TResponse : ResponseBase
        {
            var objectResult = (ObjectResult)actionResult.Result!;
            return (objectResult.StatusCode!.Value, (TResponse)objectResult.Value!);
        }

        /// <summary>
        /// Verifies create returns 201 (echoing the candidate set), 404 for an unknown input-data
        /// id, and 400 for an unsupported candidate.
        /// </summary>
        [TestMethod]
        public async Task Create_Returns201_404_400()
        {
            var input = _store.AddInputData(TestAnalyses.CreateInputDataResource());

            var (status, body) = Unwrap(await _controller.Create(new CreateDistributionFittingAnalysisRequest { InputDataId = input.Id }));
            Assert.AreEqual(201, status);
            Assert.AreEqual("distributionFitting", body.Analysis!.Kind);
            Assert.IsTrue(body.Analysis.IsValid);
            Assert.IsNotNull(body.Analysis.ComponentDistributions);
            Assert.AreEqual(15, body.Analysis.ComponentDistributions.Count);

            var (missingStatus, _) = Unwrap(await _controller.Create(new CreateDistributionFittingAnalysisRequest { InputDataId = Guid.NewGuid() }));
            Assert.AreEqual(404, missingStatus);

            var (badStatus, _) = Unwrap(await _controller.Create(new CreateDistributionFittingAnalysisRequest
            {
                InputDataId = input.Id,
                Distributions = new List<UnivariateDistributionType> { UnivariateDistributionType.Cauchy }
            }));
            Assert.AreEqual(400, badStatus);
        }

        /// <summary>
        /// Verifies get/list/validate/results-before-run/delete round-trip with kind guarding
        /// and the run-lock conflict.
        /// </summary>
        [TestMethod]
        public async Task Get_List_Validate_Delete_Lifecycle()
        {
            var input = _store.AddInputData(TestAnalyses.CreateInputDataResource());
            var (_, created) = Unwrap(await _controller.Create(new CreateDistributionFittingAnalysisRequest { InputDataId = input.Id }));
            var id = created.Analysis!.Id;

            var (getStatus, got) = Unwrap(await _controller.Get(id));
            Assert.AreEqual(200, getStatus);
            Assert.AreEqual(id, got.Analysis!.Id);

            var (listStatus, list) = Unwrap(await _controller.List());
            Assert.AreEqual(200, listStatus);
            Assert.AreEqual(1, list.Count);

            var validateResult = _controller.Validate(id);
            var validateBody = (ValidationResponse)((OkObjectResult)validateResult.Result!).Value!;
            Assert.IsTrue(validateBody.IsValid);

            var (resultsStatus, _) = Unwrap(await _controller.GetResults(id));
            Assert.AreEqual(404, resultsStatus, "Results before any run must be 404.");

            var resource = _store.GetAnalysis(id)!;
            Assert.IsTrue(resource.RunLock.Wait(0));
            try
            {
                var (conflictStatus, _) = Unwrap(await _controller.Run(id, CancellationToken.None));
                Assert.AreEqual(409, conflictStatus);
            }
            finally
            {
                resource.RunLock.Release();
            }

            var other = _store.AddAnalysis(TestAnalyses.CreateUnivariateResource());
            var (guardStatus, _) = Unwrap(await _controller.Get(other.Id));
            Assert.AreEqual(404, guardStatus, "A univariate id must 404 on the distribution-fitting routes.");

            var (deleteStatus, deleted) = Unwrap(await _controller.Delete(id));
            Assert.AreEqual(200, deleteStatus);
            Assert.AreEqual(id, deleted.DeletedId);
        }
    }
}
