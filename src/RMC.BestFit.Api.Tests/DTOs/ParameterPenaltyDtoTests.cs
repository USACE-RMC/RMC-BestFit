using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.DTOs
{
    /// <summary>
    /// Unit tests for <see cref="ParameterPenaltyDto"/>: defaults, JSON round-trip, and camelCase
    /// wire names.
    /// </summary>
    [TestClass]
    public class ParameterPenaltyDtoTests
    {
        /// <summary>
        /// Verifies the defaults: empty name, zero mean/mse, and real-space (useLog false) penalty.
        /// </summary>
        [TestMethod]
        public void Defaults_AreExpected()
        {
            var penalty = new ParameterPenaltyDto();
            Assert.AreEqual(string.Empty, penalty.ParameterName);
            Assert.AreEqual(0d, penalty.Mean);
            Assert.AreEqual(0d, penalty.Mse);
            Assert.IsFalse(penalty.UseLog);
        }

        /// <summary>
        /// Verifies every property survives a JSON round-trip.
        /// </summary>
        [TestMethod]
        public void Roundtrip_PreservesValues()
        {
            var penalty = new ParameterPenaltyDto
            {
                ParameterName = "Skew (of log)",
                Mean = -0.05,
                Mse = 0.12,
                UseLog = false
            };

            var copy = TestJson.Roundtrip(penalty);

            Assert.AreEqual("Skew (of log)", copy.ParameterName);
            Assert.AreEqual(-0.05, copy.Mean);
            Assert.AreEqual(0.12, copy.Mse);
            Assert.IsFalse(copy.UseLog);
        }

        /// <summary>
        /// Verifies the serialized JSON uses camelCase wire names.
        /// </summary>
        [TestMethod]
        public void Serialize_UsesCamelCaseWireNames()
        {
            string json = TestJson.Serialize(new ParameterPenaltyDto { ParameterName = "Skew (of log)", Mean = -0.1, Mse = 0.09, UseLog = true });
            StringAssert.Contains(json, "\"parameterName\"");
            StringAssert.Contains(json, "\"mean\"");
            StringAssert.Contains(json, "\"mse\"");
            StringAssert.Contains(json, "\"useLog\"");
        }
    }
}
