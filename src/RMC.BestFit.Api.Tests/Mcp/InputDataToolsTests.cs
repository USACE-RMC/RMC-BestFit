using System.Text.Json;
using Microsoft.Extensions.Options;
using RMC.BestFit.Api.Configuration;
using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Mcp;
using RMC.BestFit.Api.Services;
using RMC.BestFit.Api.Store;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.Mcp
{
    /// <summary>
    /// Unit tests for <see cref="InputDataTools"/>: contract parity with the REST endpoints via
    /// the faked USGS seam.
    /// </summary>
    [TestClass]
    public class InputDataToolsTests
    {
        /// <summary>
        /// The store shared by the tool stack.
        /// </summary>
        private InMemoryResourceStore _store = null!;

        /// <summary>
        /// The faked USGS seam.
        /// </summary>
        private FakeUsgsTimeSeriesService _usgs = null!;

        /// <summary>
        /// The tools under test.
        /// </summary>
        private InputDataTools _tools = null!;

        /// <summary>
        /// Creates a fresh tool stack before each test.
        /// </summary>
        [TestInitialize]
        public void Initialize()
        {
            _store = new InMemoryResourceStore(Options.Create(new ApiOptions()));
            _usgs = new FakeUsgsTimeSeriesService { Result = TestSeries.IrregularPeaks() };
            _tools = new InputDataTools(new InputDataService(_store, _usgs));
        }

        /// <summary>
        /// Verifies the direct USGS peaks tool creates an input-data resource.
        /// </summary>
        [TestMethod]
        public async Task CreateInputDataUsgsPeaks_CreatesResource()
        {
            string json = await _tools.CreateInputDataUsgsPeaks("01646500");
            using var document = JsonDocument.Parse(json);
            var summary = document.RootElement.GetProperty("inputData");

            Assert.AreEqual("usgsPeakDischarge", summary.GetProperty("method").GetString());
            Assert.AreEqual(10, summary.GetProperty("exactCount").GetInt32());
        }

        /// <summary>
        /// Verifies the block-maxima tool extracts the planted water-year maxima from a stored series.
        /// </summary>
        [TestMethod]
        public void CreateInputDataBlockMax_ExtractsPlantedMaxima()
        {
            var series = _store.AddTimeSeries(new TimeSeriesResource(TestSeries.DailyThreeWaterYears())
            {
                Name = "daily",
                Source = TimeSeriesSource.Manual
            });

            string json = _tools.CreateInputDataBlockMax(series.Id);
            using var document = JsonDocument.Parse(json);
            var summary = document.RootElement.GetProperty("inputData");

            Assert.AreEqual("blockMaxima", summary.GetProperty("method").GetString());
            Assert.AreEqual(3, summary.GetProperty("exactCount").GetInt32());
            Assert.AreEqual(series.Id, summary.GetProperty("sourceTimeSeriesId").GetGuid());
        }

        /// <summary>
        /// Verifies the manual tool builds observations and the get tool returns them with
        /// plotting positions.
        /// </summary>
        [TestMethod]
        public void CreateInputDataManual_ThenGet_RoundTrips()
        {
            string createdJson = _tools.CreateInputDataManual(new List<ExactObservationDto>
            {
                new() { Index = 2000, Value = 100d },
                new() { Index = 2001, Value = 200d }
            });
            using var createdDocument = JsonDocument.Parse(createdJson);
            var id = createdDocument.RootElement.GetProperty("inputData").GetProperty("id").GetGuid();

            string detailJson = _tools.GetInputData(id, includeData: true);
            using var detailDocument = JsonDocument.Parse(detailJson);
            var exactData = detailDocument.RootElement.GetProperty("exactData");

            Assert.AreEqual(2, exactData.GetArrayLength());
            Assert.IsTrue(exactData[0].GetProperty("plottingPosition").GetDouble() > 0d);
        }
    }
}
