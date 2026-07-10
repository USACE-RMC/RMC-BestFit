using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.DTOs
{
    /// <summary>
    /// Unit tests for <see cref="QuantilePenaltyDto"/>: defaults (log10 space on by default),
    /// JSON round-trip, and camelCase wire names.
    /// </summary>
    [TestClass]
    public class QuantilePenaltyDtoTests
    {
        /// <summary>
        /// Verifies the defaults: zero aep/mean/mse and log10 comparison enabled (the flood
        /// discharge convention).
        /// </summary>
        [TestMethod]
        public void Defaults_AreExpected()
        {
            var penalty = new QuantilePenaltyDto();
            Assert.AreEqual(0d, penalty.Aep);
            Assert.AreEqual(0d, penalty.Mean);
            Assert.AreEqual(0d, penalty.Mse);
            Assert.IsTrue(penalty.UseLog10, "Log10 comparison should be the default for flood discharges.");
        }

        /// <summary>
        /// Verifies every property survives a JSON round-trip.
        /// </summary>
        [TestMethod]
        public void Roundtrip_PreservesValues()
        {
            var penalty = new QuantilePenaltyDto
            {
                Aep = 0.002,
                Mean = 4.85,
                Mse = 0.02,
                UseLog10 = false
            };

            var copy = TestJson.Roundtrip(penalty);

            Assert.AreEqual(0.002, copy.Aep);
            Assert.AreEqual(4.85, copy.Mean);
            Assert.AreEqual(0.02, copy.Mse);
            Assert.IsFalse(copy.UseLog10);
        }

        /// <summary>
        /// Verifies the serialized JSON uses camelCase wire names.
        /// </summary>
        [TestMethod]
        public void Serialize_UsesCamelCaseWireNames()
        {
            string json = TestJson.Serialize(new QuantilePenaltyDto { Aep = 0.01, Mean = 4.5, Mse = 0.05 });
            StringAssert.Contains(json, "\"aep\"");
            StringAssert.Contains(json, "\"mean\"");
            StringAssert.Contains(json, "\"mse\"");
            StringAssert.Contains(json, "\"useLog10\"");
        }
    }
}
