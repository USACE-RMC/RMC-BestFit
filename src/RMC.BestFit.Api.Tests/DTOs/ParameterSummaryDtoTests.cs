using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.DTOs
{
    /// <summary>
    /// Unit tests for <see cref="ParameterSummaryDto"/>: defaults, JSON round-trip, and camelCase
    /// wire names.
    /// </summary>
    [TestClass]
    public class ParameterSummaryDtoTests
    {
        /// <summary>
        /// Verifies a new summary defaults to no name and all-null statistics so non-chain methods
        /// can omit the convergence diagnostics.
        /// </summary>
        [TestMethod]
        public void Defaults_AreExpected()
        {
            var summary = new ParameterSummaryDto();
            Assert.IsNull(summary.Name);
            Assert.IsNull(summary.Mean);
            Assert.IsNull(summary.Median);
            Assert.IsNull(summary.StandardDeviation);
            Assert.IsNull(summary.LowerCI);
            Assert.IsNull(summary.UpperCI);
            Assert.IsNull(summary.Rhat);
            Assert.IsNull(summary.Ess);
        }

        /// <summary>
        /// Verifies every property, including the convergence diagnostics, survives a JSON round-trip.
        /// </summary>
        [TestMethod]
        public void Roundtrip_PreservesValues()
        {
            var summary = new ParameterSummaryDto
            {
                Name = "Mu",
                Mean = 3.52,
                Median = 3.51,
                StandardDeviation = 0.031,
                LowerCI = 3.46,
                UpperCI = 3.58,
                Rhat = 1.002,
                Ess = 8450.0
            };

            var copy = TestJson.Roundtrip(summary);

            Assert.AreEqual("Mu", copy.Name);
            Assert.AreEqual(3.52, copy.Mean);
            Assert.AreEqual(3.51, copy.Median);
            Assert.AreEqual(0.031, copy.StandardDeviation);
            Assert.AreEqual(3.46, copy.LowerCI);
            Assert.AreEqual(3.58, copy.UpperCI);
            Assert.AreEqual(1.002, copy.Rhat);
            Assert.AreEqual(8450.0, copy.Ess);
        }

        /// <summary>
        /// Verifies the serialized JSON uses camelCase wire names and omits null statistics.
        /// </summary>
        [TestMethod]
        public void Serialize_UsesCamelCaseWireNames()
        {
            string json = TestJson.Serialize(new ParameterSummaryDto
            {
                Name = "Sigma",
                StandardDeviation = 0.02,
                LowerCI = 0.2,
                UpperCI = 0.3,
                Rhat = 1.01
            });
            StringAssert.Contains(json, "\"standardDeviation\"");
            StringAssert.Contains(json, "\"lowerCI\"");
            StringAssert.Contains(json, "\"upperCI\"");
            StringAssert.Contains(json, "\"rhat\"");
            Assert.IsFalse(json.Contains("\"ess\""), "Null ess should be omitted.");
        }
    }
}
