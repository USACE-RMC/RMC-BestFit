using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.DTOs
{
    /// <summary>
    /// Unit tests for <see cref="FrequencyCurveDto"/>: defaults, JSON round-trip, and camelCase
    /// wire names.
    /// </summary>
    [TestClass]
    public class FrequencyCurveDtoTests
    {
        /// <summary>
        /// Verifies a new curve defaults to an empty probability grid with no curves and a zero
        /// credible-interval width.
        /// </summary>
        [TestMethod]
        public void Defaults_AreExpected()
        {
            var curve = new FrequencyCurveDto();
            Assert.IsNotNull(curve.Probabilities);
            Assert.AreEqual(0, curve.Probabilities.Count);
            Assert.IsNull(curve.ModeCurve);
            Assert.IsNull(curve.MeanCurve);
            Assert.IsNull(curve.CiLower);
            Assert.IsNull(curve.CiUpper);
            Assert.AreEqual(0.0, curve.CredibleIntervalWidth);
        }

        /// <summary>
        /// Verifies every property, including all four aligned curves, survives a JSON round-trip.
        /// </summary>
        [TestMethod]
        public void Roundtrip_PreservesValues()
        {
            var curve = new FrequencyCurveDto
            {
                Probabilities = new List<double> { 0.5, 0.01 },
                ModeCurve = new List<double> { 3300.0, 15400.0 },
                MeanCurve = new List<double> { 3350.0, 16100.0 },
                CiLower = new List<double> { 2800.0, 11200.0 },
                CiUpper = new List<double> { 3900.0, 24500.0 },
                CredibleIntervalWidth = 0.9
            };

            var copy = TestJson.Roundtrip(curve);

            CollectionAssert.AreEqual(curve.Probabilities, copy.Probabilities);
            CollectionAssert.AreEqual(curve.ModeCurve, copy.ModeCurve);
            CollectionAssert.AreEqual(curve.MeanCurve, copy.MeanCurve);
            CollectionAssert.AreEqual(curve.CiLower, copy.CiLower);
            CollectionAssert.AreEqual(curve.CiUpper, copy.CiUpper);
            Assert.AreEqual(0.9, copy.CredibleIntervalWidth);
        }

        /// <summary>
        /// Verifies the serialized JSON uses camelCase wire names and omits null curves.
        /// </summary>
        [TestMethod]
        public void Serialize_UsesCamelCaseWireNames()
        {
            string json = TestJson.Serialize(new FrequencyCurveDto
            {
                ModeCurve = new List<double> { 1.0 }
            });
            StringAssert.Contains(json, "\"probabilities\"");
            StringAssert.Contains(json, "\"modeCurve\"");
            StringAssert.Contains(json, "\"credibleIntervalWidth\"");
            Assert.IsFalse(json.Contains("\"ciLower\""), "Null ciLower should be omitted.");
            Assert.IsFalse(json.Contains("\"meanCurve\""), "Null meanCurve should be omitted.");
        }
    }
}
