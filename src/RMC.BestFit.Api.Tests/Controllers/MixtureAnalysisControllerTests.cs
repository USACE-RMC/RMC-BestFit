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
    /// Unit tests for <see cref="MixtureAnalysisController"/> status codes and payloads, using
    /// the real service over an in-memory store. Analyses are created and validated but never run.
    /// </summary>
    [TestClass]
    public class MixtureAnalysisControllerTests
    {
        /// <summary>
        /// The store backing the controller stack.
        /// </summary>
        private InMemoryResourceStore _store = null!;

        /// <summary>
        /// The controller under test.
        /// </summary>
        private MixtureAnalysisController _controller = null!;

        /// <summary>
        /// Creates a fresh controller stack before each test.
        /// </summary>
        [TestInitialize]
        public void Initialize()
        {
            _store = new InMemoryResourceStore(Options.Create(new ApiOptions()));
            var service = new AnalysisService(_store, Options.Create(new ApiOptions()));
            _controller = new MixtureAnalysisController(NullLogger<MixtureAnalysisController>.Instance, service);
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
        /// Builds a valid two-component creation request over a stored input-data resource.
        /// </summary>
        /// <returns>The request.</returns>
        private CreateMixtureAnalysisRequest CreateValidRequest()
        {
            var input = _store.AddInputData(TestAnalyses.CreateInputDataResource());
            return new CreateMixtureAnalysisRequest
            {
                InputDataId = input.Id,
                Distributions = new List<UnivariateDistributionType>
                {
                    UnivariateDistributionType.Gumbel,
                    UnivariateDistributionType.LogNormal
                }
            };
        }

        /// <summary>
        /// Verifies create returns 201 with kind and component metadata, 404 for an unknown
        /// input-data id, and 400 for too many components.
        /// </summary>
        [TestMethod]
        public async Task Create_Returns201_404_400()
        {
            var (status, body) = Unwrap(await _controller.Create(CreateValidRequest()));
            Assert.AreEqual(201, status);
            Assert.AreEqual("mixture", body.Analysis!.Kind);
            Assert.IsTrue(body.Analysis.IsValid);
            Assert.IsNotNull(body.Analysis.ComponentDistributions);
            Assert.AreEqual(2, body.Analysis.ComponentDistributions.Count);
            Assert.IsFalse(body.Analysis.IsZeroInflated!.Value);

            var (missingStatus, _) = Unwrap(await _controller.Create(new CreateMixtureAnalysisRequest
            {
                InputDataId = Guid.NewGuid(),
                Distributions = new List<UnivariateDistributionType> { UnivariateDistributionType.Gumbel }
            }));
            Assert.AreEqual(404, missingStatus);

            var tooMany = CreateValidRequest();
            tooMany.Distributions = Enumerable.Repeat(UnivariateDistributionType.Gumbel, 4).ToList();
            var (tooManyStatus, _) = Unwrap(await _controller.Create(tooMany));
            Assert.AreEqual(400, tooManyStatus);
        }

        /// <summary>
        /// Verifies get/list/validate/results-before-run/delete round-trip.
        /// </summary>
        [TestMethod]
        public async Task Get_List_Validate_Delete_Lifecycle()
        {
            var (_, created) = Unwrap(await _controller.Create(CreateValidRequest()));
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

            var (deleteStatus, deleted) = Unwrap(await _controller.Delete(id));
            Assert.AreEqual(200, deleteStatus);
            Assert.AreEqual(id, deleted.DeletedId);
        }

        /// <summary>
        /// Verifies a run request while the run lock is held returns 409, and an invalid
        /// configuration returns 400 with validation errors.
        /// </summary>
        [TestMethod]
        public async Task Run_Conflict409_AndInvalid400()
        {
            var (_, created) = Unwrap(await _controller.Create(CreateValidRequest()));
            var resource = _store.GetAnalysis(created.Analysis!.Id)!;

            Assert.IsTrue(resource.RunLock.Wait(0));
            try
            {
                var (conflictStatus, conflictBody) = Unwrap(await _controller.Run(resource.Id, CancellationToken.None));
                Assert.AreEqual(409, conflictStatus);
                Assert.IsFalse(conflictBody.Success);
            }
            finally
            {
                resource.RunLock.Release();
            }

            // Corrupt the ordinates directly on the model so the run fails validation fast — no
            // estimation starts.
            resource.Mixture!.ProbabilityOrdinates.Clear();
            resource.Mixture.ProbabilityOrdinates.AddRange(new[] { 0.5, 0.1 });
            var (invalidStatus, invalidBody) = Unwrap(await _controller.Run(resource.Id, CancellationToken.None));
            Assert.AreEqual(400, invalidStatus);
            Assert.IsNotNull(invalidBody.ValidationErrors);
        }

        /// <summary>
        /// Verifies the kind guard: a univariate analysis id 404s on every mixture route.
        /// </summary>
        [TestMethod]
        public async Task KindGuard_UnivariateId_Returns404()
        {
            var other = _store.AddAnalysis(TestAnalyses.CreateUnivariateResource());
            var (getStatus, _) = Unwrap(await _controller.Get(other.Id));
            Assert.AreEqual(404, getStatus);
        }
    }
}
