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
    /// Unit tests for <see cref="InputDataController"/> status codes and payloads, using the real
    /// service over an in-memory store with the USGS seam faked.
    /// </summary>
    [TestClass]
    public class InputDataControllerTests
    {
        /// <summary>
        /// The store shared by the controller stack.
        /// </summary>
        private InMemoryResourceStore _store = null!;

        /// <summary>
        /// The faked USGS seam.
        /// </summary>
        private FakeUsgsTimeSeriesService _usgs = null!;

        /// <summary>
        /// The controller under test.
        /// </summary>
        private InputDataController _controller = null!;

        /// <summary>
        /// Creates a fresh controller stack before each test.
        /// </summary>
        [TestInitialize]
        public void Initialize()
        {
            _store = new InMemoryResourceStore(Options.Create(new ApiOptions()));
            _usgs = new FakeUsgsTimeSeriesService { Result = TestSeries.IrregularPeaks() };
            var service = new InputDataService(_store, _usgs);
            _controller = new InputDataController(NullLogger<InputDataController>.Instance, service);
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
        /// Verifies manual create returns 201 and the detail endpoint returns observations.
        /// </summary>
        [TestMethod]
        public async Task CreateManual_ThenGetWithData_ReturnsObservations()
        {
            var (createStatus, created) = Unwrap(await _controller.CreateManual(new CreateManualInputDataRequest
            {
                ExactData = new List<ExactObservationDto>
                {
                    new() { Index = 2000, Value = 100d },
                    new() { Index = 2001, Value = 200d }
                }
            }));
            Assert.AreEqual(201, createStatus);
            var id = created.InputData!.Id;

            var (getStatus, got) = Unwrap(await _controller.Get(id, includeData: true));
            Assert.AreEqual(200, getStatus);
            Assert.IsNotNull(got.ExactData);
            Assert.AreEqual(2, got.ExactData.Count);
        }

        /// <summary>
        /// Verifies invalid manual data returns 400 with the model-layer validation messages.
        /// </summary>
        [TestMethod]
        public async Task CreateManual_Invalid_Returns400WithErrors()
        {
            var (status, body) = Unwrap(await _controller.CreateManual(new CreateManualInputDataRequest
            {
                ExactData = new List<ExactObservationDto> { new() { Index = 2000, Value = 100d } },
                ThresholdData = new List<ThresholdObservationDto>
                {
                    new() { StartIndex = 1950, EndIndex = 1900, Value = 500d }
                }
            }));
            Assert.AreEqual(400, status);
            Assert.IsNotNull(body.ValidationErrors);
        }

        /// <summary>
        /// Verifies the block-max endpoint links to a stored series and returns 201, and an
        /// unknown link returns 404.
        /// </summary>
        [TestMethod]
        public async Task CreateBlockMax_LinksSeries_Or404()
        {
            var series = new TimeSeriesResource(TestSeries.DailyThreeWaterYears()) { Name = "daily", Source = TimeSeriesSource.Manual };
            _store.AddTimeSeries(series);

            var (status, body) = Unwrap(await _controller.CreateBlockMax(new CreateBlockMaxInputDataRequest { TimeSeriesId = series.Id }));
            Assert.AreEqual(201, status);
            Assert.AreEqual(series.Id, body.InputData!.SourceTimeSeriesId);
            Assert.AreEqual(3, body.InputData.ExactCount);

            var (missingStatus, _) = Unwrap(await _controller.CreateBlockMax(new CreateBlockMaxInputDataRequest { TimeSeriesId = Guid.NewGuid() }));
            Assert.AreEqual(404, missingStatus);
        }

        /// <summary>
        /// Verifies the POT endpoint returns 201 with the extraction echo.
        /// </summary>
        [TestMethod]
        public async Task CreatePeaksOverThreshold_Returns201()
        {
            var series = new TimeSeriesResource(TestSeries.DailyThreeWaterYears()) { Name = "daily", Source = TimeSeriesSource.Manual };
            _store.AddTimeSeries(series);

            var (status, body) = Unwrap(await _controller.CreatePeaksOverThreshold(new CreatePotInputDataRequest
            {
                TimeSeriesId = series.Id,
                Threshold = 400d
            }));
            Assert.AreEqual(201, status);
            Assert.IsNotNull(body.InputData!.PotOptions);
            Assert.AreEqual(400d, body.InputData.PotOptions.Threshold);
        }

        /// <summary>
        /// Verifies the USGS peaks endpoint returns 201 through the faked seam and 400 for
        /// non-peak series types.
        /// </summary>
        [TestMethod]
        public async Task CreateFromUsgsPeaks_Returns201_Or400()
        {
            var (status, body) = Unwrap(await _controller.CreateFromUsgsPeaks(
                new CreateUsgsPeaksInputDataRequest { SiteNumber = "01646500" }, CancellationToken.None));
            Assert.AreEqual(201, status);
            Assert.AreEqual("usgsPeakDischarge", body.InputData!.Method);

            var (badStatus, _) = Unwrap(await _controller.CreateFromUsgsPeaks(
                new CreateUsgsPeaksInputDataRequest
                {
                    SiteNumber = "01646500",
                    SeriesType = Numerics.Data.TimeSeriesDownload.TimeSeriesType.DailyDischarge
                }, CancellationToken.None));
            Assert.AreEqual(400, badStatus);
        }

        /// <summary>
        /// Verifies list, summary statistics, and delete round-trip and enforce 404 semantics.
        /// </summary>
        [TestMethod]
        public async Task List_SummaryStatistics_Delete_Lifecycle()
        {
            var (_, created) = Unwrap(await _controller.CreateManual(new CreateManualInputDataRequest
            {
                ExactData = new List<ExactObservationDto>
                {
                    new() { Index = 2000, Value = 100d },
                    new() { Index = 2001, Value = 200d },
                    new() { Index = 2002, Value = 300d }
                }
            }));
            var id = created.InputData!.Id;

            var (listStatus, list) = Unwrap(await _controller.List());
            Assert.AreEqual(200, listStatus);
            Assert.AreEqual(1, list.Count);

            var (statisticsStatus, statistics) = Unwrap(await _controller.GetSummaryStatistics(id));
            Assert.AreEqual(200, statisticsStatus);
            Assert.IsTrue(statistics.Statistics.Count > 0);

            var (deleteStatus, deleted) = Unwrap(await _controller.Delete(id));
            Assert.AreEqual(200, deleteStatus);
            Assert.AreEqual("inputData", deleted.ResourceType);

            var (missingStatus, _) = Unwrap(await _controller.GetSummaryStatistics(id));
            Assert.AreEqual(404, missingStatus);
        }
    }
}
