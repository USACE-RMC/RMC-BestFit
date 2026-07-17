using Numerics.Data;
using Numerics.Distributions;
using RMC.BestFit.Analyses;
using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.DTOs
{
    /// <summary>
    /// Serialization tests for <see cref="UsgsBulletin17CWorkflowRequest"/>.
    /// </summary>
    [TestClass]
    public class UsgsBulletin17CWorkflowRequestTests
    {
        /// <summary>
        /// Verifies the documented defaults (nullable uncertainty method keeps the model default).
        /// </summary>
        [TestMethod]
        public void Defaults_AreExpected()
        {
            var request = new UsgsBulletin17CWorkflowRequest();
            Assert.AreEqual(TimeSeriesDownload.TimeSeriesType.PeakDischarge, request.SeriesType);
            Assert.AreEqual(UnivariateDistributionType.LogPearsonTypeIII, request.Distribution);
            Assert.IsNull(request.UncertaintyMethod);
            Assert.IsNull(request.ProbabilityOrdinates);
        }

        /// <summary>
        /// Verifies every property survives a wire round-trip.
        /// </summary>
        [TestMethod]
        public void Roundtrip_PreservesValues()
        {
            var request = new UsgsBulletin17CWorkflowRequest
            {
                SiteNumber = "01646500",
                SeriesType = TimeSeriesDownload.TimeSeriesType.PeakStage,
                Distribution = UnivariateDistributionType.LogNormal,
                UncertaintyMethod = UncertaintyMethod.Bootstrap,
                ProbabilityOrdinates = new List<double> { 0.01 },
                Name = "n"
            };
            var copy = TestJson.Roundtrip(request);
            Assert.AreEqual(UnivariateDistributionType.LogNormal, copy.Distribution);
            Assert.AreEqual(UncertaintyMethod.Bootstrap, copy.UncertaintyMethod);
        }

        /// <summary>
        /// Verifies the camelCase wire names and enum strings.
        /// </summary>
        [TestMethod]
        public void Serialize_UsesCamelCaseWireNames()
        {
            string json = TestJson.Serialize(new UsgsBulletin17CWorkflowRequest
            {
                SiteNumber = "01646500",
                UncertaintyMethod = UncertaintyMethod.BiasCorrectedBootstrap
            });
            StringAssert.Contains(json, "\"uncertaintyMethod\"");
            StringAssert.Contains(json, "\"biasCorrectedBootstrap\"");
        }
    }
}
