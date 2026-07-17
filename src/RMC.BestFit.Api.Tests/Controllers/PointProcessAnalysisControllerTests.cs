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
    /// Unit tests for <see cref="PointProcessAnalysisController"/> status codes and payloads,
    /// using the real service over an in-memory store. Analyses are created and validated but
    /// never run.
    /// </summary>
    [TestClass]
    public class PointProcessAnalysisControllerTests
    {
        /// <summary>
        /// The store backing the controller stack.
        /// </summary>
        private InMemoryResourceStore _store = null!;

        /// <summary>
        /// The controller under test.
        /// </summary>
        private PointProcessAnalysisController _controller = null!;

        /// <summary>
        /// Creates a fresh controller stack before each test.
        /// </summary>
        [TestInitialize]
        public void Initialize()
        {
            _store = new InMemoryResourceStore(Options.Create(new ApiOptions()));
            var service = new AnalysisService(_store, Options.Create(new ApiOptions()));
            _controller = new PointProcessAnalysisController(NullLogger<PointProcessAnalysisController>.Instance, service);
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
        /// Adds a peaks-over-threshold input-data resource carrying its extraction threshold.
        /// </summary>
        /// <returns>The stored resource.</returns>
        private InputDataResource AddPotInputData()
        {
            return _store.AddInputData(new InputDataResource
            {
                Name = "pot",
                DataFrame = TestAnalyses.CreatePotDataFrame(),
                Method = InputDataMethod.PeaksOverThreshold,
                Threshold = 400d
            });
        }

        /// <summary>
        /// Verifies create returns 201 with point process metadata (threshold seeded from the POT
        /// resource) and 404 for an unknown input-data id.
        /// </summary>
        [TestMethod]
        public async Task Create_Returns201_Or404()
        {
            var input = AddPotInputData();

            var (status, body) = Unwrap(await _controller.Create(new CreatePointProcessAnalysisRequest { InputDataId = input.Id }));
            Assert.AreEqual(201, status);
            Assert.AreEqual("pointProcess", body.Analysis!.Kind);
            Assert.IsFalse(body.Analysis.IsSeasonal!.Value);
            Assert.IsNotNull(body.Analysis.Threshold, "The POT resource's threshold must seed the model.");
            Assert.IsNotNull(body.Analysis.Lambda);

            var (missingStatus, _) = Unwrap(await _controller.Create(new CreatePointProcessAnalysisRequest { InputDataId = Guid.NewGuid() }));
            Assert.AreEqual(404, missingStatus);
        }

        /// <summary>
        /// Verifies seasonal options without isSeasonal and non-positive record spans are 400s.
        /// </summary>
        [TestMethod]
        public async Task Create_InvalidOptions_Return400()
        {
            var input = AddPotInputData();

            var (seasonalStatus, _) = Unwrap(await _controller.Create(new CreatePointProcessAnalysisRequest
            {
                InputDataId = input.Id,
                StartMonth = 4
            }));
            Assert.AreEqual(400, seasonalStatus, "startMonth without isSeasonal must be rejected, not silently ignored.");

            var (yearsStatus, _) = Unwrap(await _controller.Create(new CreatePointProcessAnalysisRequest
            {
                InputDataId = input.Id,
                TotalYears = 0d
            }));
            Assert.AreEqual(400, yearsStatus);
        }

        /// <summary>
        /// Verifies get/list/validate/results-before-run/delete round-trip and the kind guard.
        /// </summary>
        [TestMethod]
        public async Task Get_List_Validate_Delete_Lifecycle()
        {
            var input = AddPotInputData();
            var (_, created) = Unwrap(await _controller.Create(new CreatePointProcessAnalysisRequest { InputDataId = input.Id }));
            var id = created.Analysis!.Id;

            var (getStatus, got) = Unwrap(await _controller.Get(id));
            Assert.AreEqual(200, getStatus);
            Assert.AreEqual(id, got.Analysis!.Id);

            var (listStatus, list) = Unwrap(await _controller.List());
            Assert.AreEqual(200, listStatus);
            Assert.AreEqual(1, list.Count);

            var validateResult = _controller.Validate(id);
            var validateBody = (ValidationResponse)((OkObjectResult)validateResult.Result!).Value!;
            Assert.IsTrue(validateBody.IsValid, string.Join("; ", validateBody.Errors));

            var (resultsStatus, _) = Unwrap(await _controller.GetResults(id));
            Assert.AreEqual(404, resultsStatus, "Results before any run must be 404.");

            var other = _store.AddAnalysis(TestAnalyses.CreateUnivariateResource());
            var (guardStatus, _) = Unwrap(await _controller.Get(other.Id));
            Assert.AreEqual(404, guardStatus, "A univariate id must 404 on the point process routes.");

            var (deleteStatus, deleted) = Unwrap(await _controller.Delete(id));
            Assert.AreEqual(200, deleteStatus);
            Assert.AreEqual(id, deleted.DeletedId);
        }

        /// <summary>
        /// Verifies a run request while the run lock is held returns 409.
        /// </summary>
        [TestMethod]
        public async Task Run_Conflict409()
        {
            var input = AddPotInputData();
            var (_, created) = Unwrap(await _controller.Create(new CreatePointProcessAnalysisRequest { InputDataId = input.Id }));
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
        }
    }
}
