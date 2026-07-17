using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.DTOs
{
    /// <summary>
    /// Unit tests for <see cref="RatingCurveDto"/>: defaults, JSON round-trip, and camelCase
    /// wire names.
    /// </summary>
    [TestClass]
    public class RatingCurveDtoTests
    {
        /// <summary>
        /// Verifies a new curve defaults to an empty stage grid with no discharge curves, a zero
        /// credible-interval width, and no grid bounds.
        /// </summary>
        [TestMethod]
        public void Defaults_AreExpected()
        {
            var curve = new RatingCurveDto();
            Assert.IsNotNull(curve.Stages);
            Assert.AreEqual(0, curve.Stages.Count);
            Assert.IsNull(curve.ModeCurve);
            Assert.IsNull(curve.MeanCurve);
            Assert.IsNull(curve.CiLower);
            Assert.IsNull(curve.CiUpper);
            Assert.AreEqual(0.0, curve.CredibleIntervalWidth);
            Assert.IsNull(curve.MinStage);
            Assert.IsNull(curve.MaxStage);
            Assert.IsNull(curve.StageBins);
        }

        /// <summary>
        /// Verifies every property, including all four aligned discharge curves and the grid
        /// bounds, survives a JSON round-trip.
        /// </summary>
        [TestMethod]
        public void Roundtrip_PreservesValues()
        {
            var curve = new RatingCurveDto
            {
                Stages = new List<double> { 2.0, 6.0, 10.0 },
                ModeCurve = new List<double> { 150.0, 2400.0, 9800.0 },
                MeanCurve = new List<double> { 155.0, 2450.0, 10100.0 },
                CiLower = new List<double> { 120.0, 2000.0, 8200.0 },
                CiUpper = new List<double> { 190.0, 2900.0, 12200.0 },
                CredibleIntervalWidth = 0.9,
                MinStage = 2.0,
                MaxStage = 10.0,
                StageBins = 3
            };

            var copy = TestJson.Roundtrip(curve);

            CollectionAssert.AreEqual(curve.Stages, copy.Stages);
            CollectionAssert.AreEqual(curve.ModeCurve, copy.ModeCurve);
            CollectionAssert.AreEqual(curve.MeanCurve, copy.MeanCurve);
            CollectionAssert.AreEqual(curve.CiLower, copy.CiLower);
            CollectionAssert.AreEqual(curve.CiUpper, copy.CiUpper);
            Assert.AreEqual(0.9, copy.CredibleIntervalWidth);
            Assert.AreEqual(2.0, copy.MinStage);
            Assert.AreEqual(10.0, copy.MaxStage);
            Assert.AreEqual(3, copy.StageBins);
        }

        /// <summary>
        /// Verifies the serialized JSON uses camelCase wire names and omits null curves and bounds.
        /// </summary>
        [TestMethod]
        public void Serialize_UsesCamelCaseWireNames()
        {
            string json = TestJson.Serialize(new RatingCurveDto
            {
                CiUpper = new List<double> { 1.0 },
                StageBins = 100
            });
            StringAssert.Contains(json, "\"stages\"");
            StringAssert.Contains(json, "\"ciUpper\"");
            StringAssert.Contains(json, "\"credibleIntervalWidth\"");
            StringAssert.Contains(json, "\"stageBins\"");
            Assert.IsFalse(json.Contains("\"minStage\""), "Null minStage should be omitted.");
            Assert.IsFalse(json.Contains("\"modeCurve\""), "Null modeCurve should be omitted.");
        }
    }
}
