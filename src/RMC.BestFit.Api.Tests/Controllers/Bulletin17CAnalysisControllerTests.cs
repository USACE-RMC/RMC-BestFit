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
    /// Unit tests for <see cref="Bulletin17CAnalysisController"/> status codes and payloads.
    /// Analyses are created and validated but never run.
    /// </summary>
    [TestClass]
    public class Bulletin17CAnalysisControllerTests
    {
        /// <summary>
        /// The store backing the controller stack.
        /// </summary>
        private InMemoryResourceStore _store = null!;

        /// <summary>
        /// The controller under test.
        /// </summary>
        private Bulletin17CAnalysisController _controller = null!;

        /// <summary>
        /// Creates a fresh controller stack before each test.
        /// </summary>
        [TestInitialize]
        public void Initialize()
        {
            _store = new InMemoryResourceStore(Options.Create(new ApiOptions()));
            var service = new AnalysisService(_store, Options.Create(new ApiOptions()));
            _controller = new Bulletin17CAnalysisController(NullLogger<Bulletin17CAnalysisController>.Instance, service);
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
        /// Verifies create returns 201 with the uncertainty method, and 400 for a distribution
        /// Bulletin 17C does not support.
        /// </summary>
        [TestMethod]
        public async Task Create_Returns201_Or400ForUnsupportedDistribution()
        {
            var input = _store.AddInputData(TestAnalyses.CreateInputDataResource());

            var (status, body) = Unwrap(await _controller.Create(new CreateBulletin17CAnalysisRequest { InputDataId = input.Id }));
            Assert.AreEqual(201, status);
            Assert.AreEqual("bulletin17C", body.Analysis!.Kind);
            // Omitting uncertaintyMethod keeps the model default (linkedMultivariateNormal).
            Assert.AreEqual("linkedMultivariateNormal", body.Analysis.UncertaintyMethod);

            var (badStatus, badBody) = Unwrap(await _controller.Create(new CreateBulletin17CAnalysisRequest
            {
                InputDataId = input.Id,
                Distribution = UnivariateDistributionType.GeneralizedExtremeValue
            }));
            Assert.AreEqual(400, badStatus);
            Assert.IsFalse(badBody.Success);
        }

        /// <summary>
        /// Verifies kind guarding: a univariate analysis id is not visible through the Bulletin 17C routes.
        /// </summary>
        [TestMethod]
        public async Task Get_UnivariateId_Returns404()
        {
            var univariate = _store.AddAnalysis(TestAnalyses.CreateUnivariateResource());
            var (status, _) = Unwrap(await _controller.Get(univariate.Id));
            Assert.AreEqual(404, status);
        }

        /// <summary>
        /// Verifies get/list/validate/results-404/delete round-trip.
        /// </summary>
        [TestMethod]
        public async Task Get_List_Validate_Results_Delete_Lifecycle()
        {
            var input = _store.AddInputData(TestAnalyses.CreateInputDataResource());
            var (_, created) = Unwrap(await _controller.Create(new CreateBulletin17CAnalysisRequest { InputDataId = input.Id }));
            var id = created.Analysis!.Id;

            var (getStatus, _) = Unwrap(await _controller.Get(id));
            Assert.AreEqual(200, getStatus);

            var (listStatus, list) = Unwrap(await _controller.List());
            Assert.AreEqual(200, listStatus);
            Assert.AreEqual(1, list.Count);

            var validateBody = (ValidationResponse)((OkObjectResult)_controller.Validate(id).Result!).Value!;
            Assert.IsTrue(validateBody.IsValid);

            var (resultsStatus, _) = Unwrap(await _controller.GetResults(id));
            Assert.AreEqual(404, resultsStatus);

            var (deleteStatus, _) = Unwrap(await _controller.Delete(id));
            Assert.AreEqual(200, deleteStatus);
        }
    }
}
