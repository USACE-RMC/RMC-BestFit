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
    /// Unit tests for <see cref="CompositeAnalysisController"/> status codes and payloads, using
    /// the real service over an in-memory store. Analyses are created and validated but never run.
    /// </summary>
    [TestClass]
    public class CompositeAnalysisControllerTests
    {
        /// <summary>
        /// The store backing the controller stack.
        /// </summary>
        private InMemoryResourceStore _store = null!;

        /// <summary>
        /// The service shared by the composite controller and component creation.
        /// </summary>
        private AnalysisService _service = null!;

        /// <summary>
        /// The controller under test.
        /// </summary>
        private CompositeAnalysisController _controller = null!;

        /// <summary>
        /// Creates a fresh controller stack before each test.
        /// </summary>
        [TestInitialize]
        public void Initialize()
        {
            _store = new InMemoryResourceStore(Options.Create(new ApiOptions()));
            _service = new AnalysisService(_store, Options.Create(new ApiOptions()));
            _controller = new CompositeAnalysisController(NullLogger<CompositeAnalysisController>.Instance, _service);
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
        /// Builds a valid two-component creation request over two stored unrun univariate analyses.
        /// </summary>
        /// <returns>The request.</returns>
        private CreateCompositeAnalysisRequest CreateValidRequest()
        {
            var input = _store.AddInputData(TestAnalyses.CreateInputDataResource());
            var componentA = _service.CreateUnivariate(new CreateUnivariateAnalysisRequest { InputDataId = input.Id });
            var componentB = _service.CreateUnivariate(new CreateUnivariateAnalysisRequest { InputDataId = input.Id });
            return new CreateCompositeAnalysisRequest
            {
                Components = new List<CompositeComponentDto>
                {
                    new() { AnalysisId = componentA.Id },
                    new() { AnalysisId = componentB.Id }
                }
            };
        }

        /// <summary>
        /// Verifies create returns 201 with composite metadata and 404 for an unknown component id.
        /// </summary>
        [TestMethod]
        public async Task Create_Returns201_Or404()
        {
            var (status, body) = Unwrap(await _controller.Create(CreateValidRequest()));
            Assert.AreEqual(201, status);
            Assert.AreEqual("composite", body.Analysis!.Kind);
            Assert.AreEqual("competingRisks", body.Analysis.CompositeType);
            Assert.IsNotNull(body.Analysis.ComponentAnalysisIds);
            Assert.AreEqual(2, body.Analysis.ComponentAnalysisIds.Count);
            Assert.IsFalse(body.Analysis.IsValid, "A composite over unrun components must report invalid until they are estimated.");

            var (missingStatus, _) = Unwrap(await _controller.Create(new CreateCompositeAnalysisRequest
            {
                Components = new List<CompositeComponentDto> { new() { AnalysisId = Guid.NewGuid() } }
            }));
            Assert.AreEqual(404, missingStatus);
        }

        /// <summary>
        /// Verifies validate reports the unestimated components without running, and get/list/
        /// results-before-run/delete round-trip with kind guarding.
        /// </summary>
        [TestMethod]
        public async Task Get_List_Validate_Delete_Lifecycle()
        {
            var (_, created) = Unwrap(await _controller.Create(CreateValidRequest()));
            var id = created.Analysis!.Id;

            var validateResult = _controller.Validate(id);
            var validateBody = (ValidationResponse)((OkObjectResult)validateResult.Result!).Value!;
            Assert.IsFalse(validateBody.IsValid);
            Assert.IsTrue(validateBody.Errors.Any(e => e.Contains("requires estimation")),
                "Validation must explain that components require estimation.");

            var (getStatus, got) = Unwrap(await _controller.Get(id));
            Assert.AreEqual(200, getStatus);
            Assert.AreEqual(id, got.Analysis!.Id);

            var (listStatus, list) = Unwrap(await _controller.List());
            Assert.AreEqual(200, listStatus);
            Assert.AreEqual(1, list.Count);

            var (resultsStatus, _) = Unwrap(await _controller.GetResults(id));
            Assert.AreEqual(404, resultsStatus, "Results before any run must be 404.");

            var other = _store.AddAnalysis(TestAnalyses.CreateUnivariateResource());
            var (guardStatus, _) = Unwrap(await _controller.Get(other.Id));
            Assert.AreEqual(404, guardStatus, "A univariate id must 404 on the composite routes.");

            var (deleteStatus, deleted) = Unwrap(await _controller.Delete(id));
            Assert.AreEqual(200, deleteStatus);
            Assert.AreEqual(id, deleted.DeletedId);
        }

        /// <summary>
        /// Verifies a composite run returns 409 while a component's run lock is held, and 400
        /// (children unestimated) once the locks are free.
        /// </summary>
        [TestMethod]
        public async Task Run_ComponentRunning409_ThenInvalid400()
        {
            var (_, created) = Unwrap(await _controller.Create(CreateValidRequest()));
            var composite = _store.GetAnalysis(created.Analysis!.Id)!;
            var component = composite.ComponentResources![0];

            Assert.IsTrue(component.RunLock.Wait(0));
            try
            {
                var (conflictStatus, conflictBody) = Unwrap(await _controller.Run(composite.Id, CancellationToken.None));
                Assert.AreEqual(409, conflictStatus);
                StringAssert.Contains(conflictBody.ErrorMessage!, "component analysis");
            }
            finally
            {
                component.RunLock.Release();
            }

            var (invalidStatus, invalidBody) = Unwrap(await _controller.Run(composite.Id, CancellationToken.None));
            Assert.AreEqual(400, invalidStatus);
            Assert.IsNotNull(invalidBody.ValidationErrors);
        }
    }
}
