using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.DTOs
{
    /// <summary>
    /// Unit tests for <see cref="CoincidentFrequencyResultsResponse"/>: defaults, JSON
    /// round-trip, and camelCase wire names.
    /// </summary>
    [TestClass]
    public class CoincidentFrequencyResultsResponseTests
    {
        /// <summary>
        /// Verifies the defaults: empty curves and chain-usage flags off.
        /// </summary>
        [TestMethod]
        public void Defaults_AreExpected()
        {
            var response = new CoincidentFrequencyResultsResponse();
            Assert.AreEqual(Guid.Empty, response.AnalysisId);
            Assert.AreEqual(0, response.NumberOfBins);
            Assert.AreEqual(0, response.ZValues.Count);
            Assert.IsNull(response.AepMode);
            Assert.IsNull(response.CiLower);
            Assert.IsFalse(response.MarginalXChainUsed);
            Assert.IsFalse(response.MarginalYChainUsed);
        }

        /// <summary>
        /// Verifies the curves survive a JSON round-trip.
        /// </summary>
        [TestMethod]
        public void Roundtrip_PreservesValues()
        {
            var response = new CoincidentFrequencyResultsResponse
            {
                AnalysisId = Guid.NewGuid(),
                Kind = "coincidentFrequency",
                NumberOfBins = 5,
                ZValues = new List<double> { 10d, 12d, 14d, 16d, 18d },
                AepMode = new List<double> { 0.5, 0.2, 0.1, 0.05, 0.01 },
                CiLower = new List<double> { 0.4, 0.15, 0.08, 0.03, 0.005 },
                CiUpper = new List<double> { 0.6, 0.25, 0.12, 0.07, 0.02 },
                CredibleIntervalWidth = 0.9,
                MarginalXChainUsed = true
            };

            var copy = TestJson.Roundtrip(response);

            Assert.AreEqual(5, copy.NumberOfBins);
            CollectionAssert.AreEqual(response.ZValues, copy.ZValues);
            CollectionAssert.AreEqual(response.AepMode, copy.AepMode);
            Assert.AreEqual(0.9, copy.CredibleIntervalWidth);
            Assert.IsTrue(copy.MarginalXChainUsed);
            Assert.IsFalse(copy.MarginalYChainUsed);
        }

        /// <summary>
        /// Verifies the serialized JSON uses camelCase wire names.
        /// </summary>
        [TestMethod]
        public void Serialize_UsesCamelCaseWireNames()
        {
            string json = TestJson.Serialize(new CoincidentFrequencyResultsResponse { Kind = "coincidentFrequency" });
            StringAssert.Contains(json, "\"zValues\"");
            StringAssert.Contains(json, "\"marginalXChainUsed\"");
            StringAssert.Contains(json, "\"credibleIntervalWidth\"");
        }
    }
}
