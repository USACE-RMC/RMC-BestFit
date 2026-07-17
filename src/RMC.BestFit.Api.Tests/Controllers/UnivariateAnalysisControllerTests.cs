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
    /// Unit tests for <see cref="UnivariateAnalysisController"/> status codes and payloads, using
    /// the real service over an in-memory store. Analyses are created and validated but never run.
    /// </summary>
    [TestClass]
    public class UnivariateAnalysisControllerTests
    {
        /// <summary>
        /// The store backing the controller stack.
        /// </summary>
        private InMemoryResourceStore _store = null!;

        /// <summary>
        /// The controller under test.
        /// </summary>
        private UnivariateAnalysisController _controller = null!;

        /// <summary>
        /// Creates a fresh controller stack before each test.
        /// </summary>
        [TestInitialize]
        public void Initialize()
        {
            _store = new InMemoryResourceStore(Options.Create(new ApiOptions()));
            var service = new AnalysisService(_store, Options.Create(new ApiOptions()));
            _controller = new UnivariateAnalysisController(NullLogger<UnivariateAnalysisController>.Instance, service);
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
        /// Verifies create returns 201 with the analysis summary and 404 for an unknown input-data id.
        /// </summary>
        [TestMethod]
        public async Task Create_Returns201_Or404()
        {
            var input = _store.AddInputData(TestAnalyses.CreateInputDataResource());

            var (status, body) = Unwrap(await _controller.Create(new CreateUnivariateAnalysisRequest { InputDataId = input.Id }));
            Assert.AreEqual(201, status);
            Assert.AreEqual("univariate", body.Analysis!.Kind);
            Assert.IsTrue(body.Analysis.IsValid);

            var (missingStatus, _) = Unwrap(await _controller.Create(new CreateUnivariateAnalysisRequest { InputDataId = Guid.NewGuid() }));
            Assert.AreEqual(404, missingStatus);
        }

        /// <summary>
        /// Verifies get/list/validate/delete round-trip with kind guarding.
        /// </summary>
        [TestMethod]
        public async Task Get_List_Validate_Delete_Lifecycle()
        {
            var input = _store.AddInputData(TestAnalyses.CreateInputDataResource());
            var (_, created) = Unwrap(await _controller.Create(new CreateUnivariateAnalysisRequest { InputDataId = input.Id }));
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
            var input = _store.AddInputData(TestAnalyses.CreateInputDataResource());
            var (_, created) = Unwrap(await _controller.Create(new CreateUnivariateAnalysisRequest { InputDataId = input.Id }));
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

            // Corrupt the ordinates directly on the model (the service sorts client ordinates) so
            // the run fails validation fast — no estimation starts.
            resource.Univariate!.ProbabilityOrdinates.Clear();
            resource.Univariate.ProbabilityOrdinates.AddRange(new[] { 0.5, 0.1 });
            var (invalidStatus, invalidBody) = Unwrap(await _controller.Run(resource.Id, CancellationToken.None));
            Assert.AreEqual(400, invalidStatus);
            Assert.IsNotNull(invalidBody.ValidationErrors);
        }

        /// <summary>
        /// Verifies the validate endpoint returns 404 semantics for unknown ids.
        /// </summary>
        [TestMethod]
        public void Validate_UnknownId_Returns404()
        {
            var result = _controller.Validate(Guid.NewGuid());
            var notFound = result.Result as NotFoundObjectResult;
            Assert.IsNotNull(notFound);
            var body = (ValidationResponse)notFound.Value!;
            Assert.IsFalse(body.IsValid);
        }
    }
}
