using Numerics.Data;
using Numerics.Distributions;
using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.DTOs
{
    /// <summary>
    /// Serialization tests for <see cref="UsgsPeakFrequencyWorkflowRequest"/>.
    /// </summary>
    [TestClass]
    public class UsgsPeakFrequencyWorkflowRequestTests
    {
        /// <summary>
        /// Verifies the documented defaults.
        /// </summary>
        [TestMethod]
        public void Defaults_AreExpected()
        {
            var request = new UsgsPeakFrequencyWorkflowRequest();
            Assert.AreEqual(string.Empty, request.SiteNumber);
            Assert.AreEqual(TimeSeriesDownload.TimeSeriesType.PeakDischarge, request.SeriesType);
            Assert.AreEqual(UnivariateDistributionType.LogPearsonTypeIII, request.Distribution);
            Assert.IsNull(request.ProbabilityOrdinates);
            Assert.IsNull(request.BayesianOptions);
            Assert.IsNull(request.Name);
        }

        /// <summary>
        /// Verifies every property survives a wire round-trip.
        /// </summary>
        [TestMethod]
        public void Roundtrip_PreservesValues()
        {
            var request = new UsgsPeakFrequencyWorkflowRequest
            {
                SiteNumber = "01646500",
                SeriesType = TimeSeriesDownload.TimeSeriesType.PeakStage,
                Distribution = UnivariateDistributionType.GeneralizedExtremeValue,
                ProbabilityOrdinates = new List<double> { 0.01, 0.5 },
                BayesianOptions = new BayesianOptionsDto { PrngSeed = 7 },
                Name = "workflow"
            };
            var copy = TestJson.Roundtrip(request);
            Assert.AreEqual("01646500", copy.SiteNumber);
            Assert.AreEqual(TimeSeriesDownload.TimeSeriesType.PeakStage, copy.SeriesType);
            Assert.AreEqual(UnivariateDistributionType.GeneralizedExtremeValue, copy.Distribution);
            CollectionAssert.AreEqual(request.ProbabilityOrdinates, copy.ProbabilityOrdinates);
            Assert.AreEqual(7, copy.BayesianOptions!.PrngSeed);
            Assert.AreEqual("workflow", copy.Name);
        }

        /// <summary>
        /// Verifies the camelCase wire names and enum strings.
        /// </summary>
        [TestMethod]
        public void Serialize_UsesCamelCaseWireNames()
        {
            string json = TestJson.Serialize(new UsgsPeakFrequencyWorkflowRequest { SiteNumber = "01646500" });
            StringAssert.Contains(json, "\"siteNumber\"");
            StringAssert.Contains(json, "\"peakDischarge\"");
            StringAssert.Contains(json, "\"logPearsonTypeIII\"");
            Assert.IsFalse(json.Contains("\"name\""));
        }
    }
}
