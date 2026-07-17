using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RMC.BestFit.Api.Configuration;
using RMC.BestFit.Api.Controllers;
using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Services;
using RMC.BestFit.Api.Store;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.Controllers
{
    /// <summary>
    /// Unit tests for <see cref="BivariateAnalysisController"/> status codes and payloads, using
    /// the real service over an in-memory store. Analyses are created and validated but never run.
    /// </summary>
    [TestClass]
    public class BivariateAnalysisControllerTests
    {
        /// <summary>
        /// The store backing the controller stack.
        /// </summary>
        private InMemoryResourceStore _store = null!;

        /// <summary>
        /// The service shared by the controller and marginal creation.
        /// </summary>
        private AnalysisService _service = null!;

        /// <summary>
        /// The controller under test.
        /// </summary>
        private BivariateAnalysisController _controller = null!;

        /// <summary>
        /// Creates a fresh controller stack before each test.
        /// </summary>
        [TestInitialize]
        public void Initialize()
        {
            _store = new InMemoryResourceStore(Options.Create(new ApiOptions()));
            _service = new AnalysisService(_store, Options.Create(new ApiOptions()));
            _controller = new BivariateAnalysisController(NullLogger<BivariateAnalysisController>.Instance, _service);
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
        /// Builds a valid creation request over two stored unrun univariate marginals.
        /// </summary>
        /// <returns>The request.</returns>
        private CreateBivariateAnalysisRequest CreateValidRequest()
        {
            var input = _store.AddInputData(TestAnalyses.CreateInputDataResource());
            var marginalX = _service.CreateUnivariate(new CreateUnivariateAnalysisRequest { InputDataId = input.Id });
            var marginalY = _service.CreateUnivariate(new CreateUnivariateAnalysisRequest { InputDataId = input.Id });
            return new CreateBivariateAnalysisRequest
            {
                MarginalXAnalysisId = marginalX.Id,
                MarginalYAnalysisId = marginalY.Id,
                XyOrdinates = new List<XyOrdinateDto> { new() { X = 300d, Y = 320d } }
            };
        }

        /// <summary>
        /// Verifies create returns 201 with copula metadata and marginal provenance, and 404 for
        /// an unknown marginal id.
        /// </summary>
        [TestMethod]
        public async Task Create_Returns201_Or404()
        {
            var request = CreateValidRequest();
            var (status, body) = Unwrap(await _controller.Create(request));
            Assert.AreEqual(201, status);
            Assert.AreEqual("bivariate", body.Analysis!.Kind);
            Assert.AreEqual("normal", body.Analysis.CopulaType);
            Assert.AreEqual("inferenceFromMargins", body.Analysis.CopulaEstimationMethod);
            Assert.AreEqual(request.MarginalXAnalysisId, body.Analysis.MarginalXAnalysisId);
            Assert.IsFalse(body.Analysis.IsValid, "Unrun marginals must make the bivariate invalid until estimated.");

            request.MarginalYAnalysisId = Guid.NewGuid();
            var (missingStatus, _) = Unwrap(await _controller.Create(request));
            Assert.AreEqual(404, missingStatus);
        }

        /// <summary>
        /// Verifies get/list/validate/results-before-run/delete round-trip with kind guarding.
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
            Assert.IsFalse(validateBody.IsValid);
            Assert.IsTrue(validateBody.Errors.Any(e => e.Contains("marginal")), "Validation must name the unestimated marginals.");

            var (resultsStatus, _) = Unwrap(await _controller.GetResults(id));
            Assert.AreEqual(404, resultsStatus, "Results before any run must be 404.");

            var other = _store.AddAnalysis(TestAnalyses.CreateUnivariateResource());
            var (guardStatus, _) = Unwrap(await _controller.Get(other.Id));
            Assert.AreEqual(404, guardStatus, "A univariate id must 404 on the bivariate routes.");

            var (deleteStatus, deleted) = Unwrap(await _controller.Delete(id));
            Assert.AreEqual(200, deleteStatus);
            Assert.AreEqual(id, deleted.DeletedId);
        }

        /// <summary>
        /// Verifies a run returns 409 while a marginal's run lock is held, and 400 once the
        /// locks are free (marginals unestimated).
        /// </summary>
        [TestMethod]
        public async Task Run_MarginalRunning409_ThenInvalid400()
        {
            var (_, created) = Unwrap(await _controller.Create(CreateValidRequest()));
            var resource = _store.GetAnalysis(created.Analysis!.Id)!;
            var marginal = resource.MarginalXResource!;

            Assert.IsTrue(marginal.RunLock.Wait(0));
            try
            {
                var (conflictStatus, conflictBody) = Unwrap(await _controller.Run(resource.Id, CancellationToken.None));
                Assert.AreEqual(409, conflictStatus);
                Assert.IsFalse(conflictBody.Success);
            }
            finally
            {
                marginal.RunLock.Release();
            }

            var (invalidStatus, invalidBody) = Unwrap(await _controller.Run(resource.Id, CancellationToken.None));
            Assert.AreEqual(400, invalidStatus);
            Assert.IsNotNull(invalidBody.ValidationErrors);
        }
    }
}
