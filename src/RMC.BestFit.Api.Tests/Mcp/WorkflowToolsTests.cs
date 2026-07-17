using System.Text.Json;
using Microsoft.Extensions.Options;
using RMC.BestFit.Api.Configuration;
using RMC.BestFit.Api.Mcp;
using RMC.BestFit.Api.Services;
using RMC.BestFit.Api.Services.Exceptions;
using RMC.BestFit.Api.Store;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.Mcp
{
    /// <summary>
    /// Unit tests for <see cref="WorkflowTools"/>: the in-body failure contract over the faked
    /// USGS seam. Success paths are not exercised (they run estimators).
    /// </summary>
    [TestClass]
    public class WorkflowToolsTests
    {
        /// <summary>
        /// The faked USGS seam.
        /// </summary>
        private FakeUsgsTimeSeriesService _usgs = null!;

        /// <summary>
        /// The tools under test.
        /// </summary>
        private WorkflowTools _tools = null!;

        /// <summary>
        /// Creates a fresh tool stack before each test.
        /// </summary>
        [TestInitialize]
        public void Initialize()
        {
            var store = new InMemoryResourceStore(Options.Create(new ApiOptions()));
            _usgs = new FakeUsgsTimeSeriesService { Result = TestSeries.IrregularPeaks() };
            var timeSeries = new TimeSeriesService(store, _usgs);
            var inputData = new InputDataService(store, _usgs);
            var analyses = new AnalysisService(store, Options.Create(new ApiOptions()));
            _tools = new WorkflowTools(new WorkflowService(timeSeries, inputData, analyses));
        }

        /// <summary>
        /// Verifies a step failure is reported in the tool's JSON body with the failed step named.
        /// </summary>
        [TestMethod]
        public async Task PeakFrequencyWorkflow_StepFailure_ReportedInBody()
        {
            _usgs.ExceptionToThrow = new UsgsDataNotFoundException("no data");
            string json = await _tools.RunUsgsPeakFrequencyWorkflow("01646500");
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            Assert.IsFalse(root.GetProperty("success").GetBoolean());
            Assert.AreEqual("createInputData", root.GetProperty("failedStep").GetString());
            Assert.IsTrue(root.TryGetProperty("errorMessage", out _));
        }

        /// <summary>
        /// Verifies invalid enum strings on workflow tools fail with the accepted-values message
        /// before any download happens.
        /// </summary>
        [TestMethod]
        public async Task Bulletin17CWorkflow_InvalidMethodString_Throws()
        {
            var ex = await Assert.ThrowsExceptionAsync<ArgumentException>(
                () => _tools.RunUsgsBulletin17CWorkflow("01646500", uncertaintyMethod: "bogus"));
            StringAssert.Contains(ex.Message, "multivariateNormal");
            Assert.IsNull(_usgs.LastSiteNumber, "No download may happen when the parameters are invalid.");
        }
    }
}
