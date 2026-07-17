using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.DTOs
{
    /// <summary>
    /// Unit tests for <see cref="TimeSeriesResultsResponse"/> and its supporting
    /// <see cref="TimeSeriesCurveDto"/>: defaults, JSON round-trip, and camelCase wire names.
    /// </summary>
    [TestClass]
    public class TimeSeriesResultsResponseTests
    {
        /// <summary>
        /// Verifies the defaults across the response and the curve DTO.
        /// </summary>
        [TestMethod]
        public void Defaults_AreExpected()
        {
            var response = new TimeSeriesResultsResponse();
            Assert.AreEqual(Guid.Empty, response.AnalysisId);
            Assert.IsNull(response.ModelType);
            Assert.AreEqual(0, response.DataLength);
            Assert.IsNull(response.Order);
            Assert.IsNull(response.POrder);
            Assert.IsNull(response.XOrder);
            Assert.IsNull(response.Curve);
            Assert.AreEqual(0, response.Parameters.Count);

            var curve = new TimeSeriesCurveDto();
            Assert.AreEqual(0, curve.TimeIndices.Count);
            Assert.IsNull(curve.ModeCurve);
            Assert.AreEqual(0d, curve.CredibleIntervalWidth);
        }

        /// <summary>
        /// Verifies the nested curve survives a JSON round-trip.
        /// </summary>
        [TestMethod]
        public void Roundtrip_PreservesValues()
        {
            var response = new TimeSeriesResultsResponse
            {
                AnalysisId = Guid.NewGuid(),
                Kind = "timeSeries",
                ModelType = "arima",
                TransformType = "logarithmic",
                DataLength = 1096,
                TrainingTimeSteps = 900,
                ForecastingTimeSteps = 12,
                POrder = 2,
                DOrder = 1,
                QOrder = 1,
                Curve = new TimeSeriesCurveDto
                {
                    TimeIndices = new List<double> { 1d, 2d },
                    ModeCurve = new List<double> { 100d, 101d },
                    CiLower = new List<double> { 90d, 91d },
                    CiUpper = new List<double> { 110d, 111d },
                    CredibleIntervalWidth = 0.9
                }
            };

            var copy = TestJson.Roundtrip(response);

            Assert.AreEqual("arima", copy.ModelType);
            Assert.AreEqual(1096, copy.DataLength);
            Assert.AreEqual(900, copy.TrainingTimeSteps);
            Assert.AreEqual(12, copy.ForecastingTimeSteps);
            Assert.AreEqual(2, copy.POrder);
            Assert.AreEqual(2, copy.Curve!.TimeIndices.Count);
            Assert.AreEqual(0.9, copy.Curve.CredibleIntervalWidth);
        }

        /// <summary>
        /// Verifies the serialized JSON uses camelCase wire names and omits null orders.
        /// </summary>
        [TestMethod]
        public void Serialize_UsesCamelCaseWireNames()
        {
            string json = TestJson.Serialize(new TimeSeriesResultsResponse
            {
                Kind = "timeSeries",
                ModelType = "ar",
                Order = 1,
                Curve = new TimeSeriesCurveDto { TimeIndices = new List<double> { 1d } }
            });
            StringAssert.Contains(json, "\"modelType\"");
            StringAssert.Contains(json, "\"order\"");
            StringAssert.Contains(json, "\"curve\"");
            StringAssert.Contains(json, "\"timeIndices\"");
            Assert.IsFalse(json.Contains("\"pOrder\""), "Null pOrder should be omitted.");
        }
    }
}
