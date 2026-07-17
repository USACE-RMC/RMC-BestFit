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
    /// Unit tests for <see cref="TimeSeriesAnalysisController"/> status codes and payloads, using
    /// the real service over an in-memory store. Analyses are created and validated but never run.
    /// </summary>
    [TestClass]
    public class TimeSeriesAnalysisControllerTests
    {
        /// <summary>
        /// The store backing the controller stack.
        /// </summary>
        private InMemoryResourceStore _store = null!;

        /// <summary>
        /// The controller under test.
        /// </summary>
        private TimeSeriesAnalysisController _controller = null!;

        /// <summary>
        /// Creates a fresh controller stack before each test.
        /// </summary>
        [TestInitialize]
        public void Initialize()
        {
            _store = new InMemoryResourceStore(Options.Create(new ApiOptions()));
            var service = new AnalysisService(_store, Options.Create(new ApiOptions()));
            _controller = new TimeSeriesAnalysisController(NullLogger<TimeSeriesAnalysisController>.Instance, service);
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
        /// Adds the synthetic daily series to the store.
        /// </summary>
        /// <returns>The stored resource.</returns>
        private TimeSeriesResource AddDailySeries()
        {
            return _store.AddTimeSeries(new TimeSeriesResource(TestSeries.DailyThreeWaterYears())
            {
                Name = "daily",
                Source = TimeSeriesSource.Manual
            });
        }

        /// <summary>
        /// Verifies create returns 201 for each model family, 404 for an unknown series id, and
        /// 400 for a field that does not apply to the model type.
        /// </summary>
        [TestMethod]
        public async Task Create_Returns201_404_400()
        {
            var source = AddDailySeries();

            foreach (var modelType in Enum.GetValues<TimeSeriesModelType>())
            {
                var (status, body) = Unwrap(await _controller.Create(new CreateTimeSeriesAnalysisRequest
                {
                    TimeSeriesId = source.Id,
                    ModelType = modelType
                }));
                Assert.AreEqual(201, status, $"Create must succeed for {modelType}.");
                Assert.AreEqual("timeSeries", body.Analysis!.Kind);
                Assert.AreEqual(Api.Helpers.EnumHelper.ToCamelCase(modelType.ToString()), body.Analysis.TimeSeriesModelType);
                Assert.IsTrue(body.Analysis.IsValid, $"A default {modelType} configuration must validate.");
            }

            var (missingStatus, _) = Unwrap(await _controller.Create(new CreateTimeSeriesAnalysisRequest
            {
                TimeSeriesId = Guid.NewGuid(),
                ModelType = TimeSeriesModelType.Ar
            }));
            Assert.AreEqual(404, missingStatus);

            var (badFieldStatus, badFieldBody) = Unwrap(await _controller.Create(new CreateTimeSeriesAnalysisRequest
            {
                TimeSeriesId = source.Id,
                ModelType = TimeSeriesModelType.Ma,
                TrendType = RMC.BestFit.Models.ARIMAX.Trend.Linear
            }));
            Assert.AreEqual(400, badFieldStatus);
            StringAssert.Contains(badFieldBody.ErrorMessage!, "trendType");
        }

        /// <summary>
        /// Verifies get/list/validate/results-before-run/delete round-trip with kind guarding in
        /// both directions and the run-lock conflict.
        /// </summary>
        [TestMethod]
        public async Task Get_List_Validate_Delete_Lifecycle()
        {
            var source = AddDailySeries();
            var (_, created) = Unwrap(await _controller.Create(new CreateTimeSeriesAnalysisRequest
            {
                TimeSeriesId = source.Id,
                ModelType = TimeSeriesModelType.Arima
            }));
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
            Assert.AreEqual(404, guardStatus, "A univariate id must 404 on the time-series routes.");

            var (deleteStatus, deleted) = Unwrap(await _controller.Delete(id));
            Assert.AreEqual(200, deleteStatus);
            Assert.AreEqual(id, deleted.DeletedId);
        }
    }
}
