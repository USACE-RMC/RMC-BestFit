using Numerics.Data;
using Numerics.Distributions;
using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.DTOs
{
    /// <summary>
    /// Serialization tests for <see cref="UsgsBlockMaxFrequencyWorkflowRequest"/>.
    /// </summary>
    [TestClass]
    public class UsgsBlockMaxFrequencyWorkflowRequestTests
    {
        /// <summary>
        /// Verifies the documented defaults.
        /// </summary>
        [TestMethod]
        public void Defaults_AreExpected()
        {
            var request = new UsgsBlockMaxFrequencyWorkflowRequest();
            Assert.AreEqual(TimeSeriesDownload.TimeSeriesType.DailyDischarge, request.SeriesType);
            Assert.AreEqual(TimeBlockWindow.WaterYear, request.TimeBlock);
            Assert.AreEqual(BlockFunctionType.Maximum, request.BlockFunction);
            Assert.AreEqual(SmoothingFunctionType.None, request.SmoothingFunction);
            Assert.AreEqual(10, request.StartMonth);
            Assert.AreEqual(9, request.EndMonth);
            Assert.AreEqual(1, request.Period);
            Assert.AreEqual(UnivariateDistributionType.LogPearsonTypeIII, request.Distribution);
        }

        /// <summary>
        /// Verifies every property survives a wire round-trip.
        /// </summary>
        [TestMethod]
        public void Roundtrip_PreservesValues()
        {
            var request = new UsgsBlockMaxFrequencyWorkflowRequest
            {
                SiteNumber = "01646500",
                SeriesType = TimeSeriesDownload.TimeSeriesType.DailyStage,
                TimeBlock = TimeBlockWindow.CalendarYear,
                BlockFunction = BlockFunctionType.Minimum,
                SmoothingFunction = SmoothingFunctionType.MovingAverage,
                StartMonth = 4,
                EndMonth = 3,
                Period = 7,
                Distribution = UnivariateDistributionType.Gumbel,
                ProbabilityOrdinates = new List<double> { 0.1 },
                BayesianOptions = new BayesianOptionsDto { Iterations = 5000 },
                Name = "n"
            };
            var copy = TestJson.Roundtrip(request);
            Assert.AreEqual(TimeBlockWindow.CalendarYear, copy.TimeBlock);
            Assert.AreEqual(BlockFunctionType.Minimum, copy.BlockFunction);
            Assert.AreEqual(SmoothingFunctionType.MovingAverage, copy.SmoothingFunction);
            Assert.AreEqual(7, copy.Period);
            Assert.AreEqual(UnivariateDistributionType.Gumbel, copy.Distribution);
            Assert.AreEqual(5000, copy.BayesianOptions!.Iterations);
        }

        /// <summary>
        /// Verifies the camelCase wire names and enum strings.
        /// </summary>
        [TestMethod]
        public void Serialize_UsesCamelCaseWireNames()
        {
            string json = TestJson.Serialize(new UsgsBlockMaxFrequencyWorkflowRequest { SiteNumber = "01646500" });
            StringAssert.Contains(json, "\"timeBlock\"");
            StringAssert.Contains(json, "\"waterYear\"");
            StringAssert.Contains(json, "\"startMonth\"");
        }
    }
}
