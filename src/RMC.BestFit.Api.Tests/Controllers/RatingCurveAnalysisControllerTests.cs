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
    /// Unit tests for <see cref="RatingCurveAnalysisController"/> status codes and payloads.
    /// Analyses are created and validated but never run.
    /// </summary>
    [TestClass]
    public class RatingCurveAnalysisControllerTests
    {
        /// <summary>
        /// The store backing the controller stack.
        /// </summary>
        private InMemoryResourceStore _store = null!;

        /// <summary>
        /// The controller under test.
        /// </summary>
        private RatingCurveAnalysisController _controller = null!;

        /// <summary>
        /// Creates a fresh controller stack before each test.
        /// </summary>
        [TestInitialize]
        public void Initialize()
        {
            _store = new InMemoryResourceStore(Options.Create(new ApiOptions()));
            var service = new AnalysisService(_store, Options.Create(new ApiOptions()));
            _controller = new RatingCurveAnalysisController(NullLogger<RatingCurveAnalysisController>.Instance, service);
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
        /// Adds a stage/discharge pair to the store.
        /// </summary>
        /// <returns>The stored stage and discharge resources.</returns>
        private (TimeSeriesResource Stage, TimeSeriesResource Discharge) AddPair()
        {
            var (stage, discharge) = TestAnalyses.CreateStageDischargePair();
            return (
                _store.AddTimeSeries(new TimeSeriesResource(stage) { Name = "stage", Source = TimeSeriesSource.Manual }),
                _store.AddTimeSeries(new TimeSeriesResource(discharge) { Name = "discharge", Source = TimeSeriesSource.Manual }));
        }

        /// <summary>
        /// Verifies create returns 201 linked to both series, 404 for a missing series, and 400
        /// for an inconsistent grid.
        /// </summary>
        [TestMethod]
        public async Task Create_Returns201_404_Or400()
        {
            var (stage, discharge) = AddPair();

            var (status, body) = Unwrap(await _controller.Create(new CreateRatingCurveAnalysisRequest
            {
                StageTimeSeriesId = stage.Id,
                DischargeTimeSeriesId = discharge.Id
            }));
            Assert.AreEqual(201, status);
            Assert.AreEqual("ratingCurve", body.Analysis!.Kind);
            Assert.AreEqual(stage.Id, body.Analysis.StageTimeSeriesId);
            Assert.IsTrue(body.Analysis.IsValid);

            var (missingStatus, _) = Unwrap(await _controller.Create(new CreateRatingCurveAnalysisRequest
            {
                StageTimeSeriesId = Guid.NewGuid(),
                DischargeTimeSeriesId = discharge.Id
            }));
            Assert.AreEqual(404, missingStatus);

            var (badStatus, _) = Unwrap(await _controller.Create(new CreateRatingCurveAnalysisRequest
            {
                StageTimeSeriesId = stage.Id,
                DischargeTimeSeriesId = discharge.Id,
                MinStage = 5d
            }));
            Assert.AreEqual(400, badStatus);
        }

        /// <summary>
        /// Verifies get/list/validate/results-404/delete round-trip.
        /// </summary>
        [TestMethod]
        public async Task Get_List_Validate_Results_Delete_Lifecycle()
        {
            var (stage, discharge) = AddPair();
            var (_, created) = Unwrap(await _controller.Create(new CreateRatingCurveAnalysisRequest
            {
                StageTimeSeriesId = stage.Id,
                DischargeTimeSeriesId = discharge.Id
            }));
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
