using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.DTOs
{
    /// <summary>
    /// Unit tests for <see cref="SummaryStatisticsResponse"/>: defaults, JSON round-trip including
    /// NaN measures and the inherited <see cref="ResponseBase"/> fields, and camelCase wire names.
    /// </summary>
    [TestClass]
    public class SummaryStatisticsResponseTests
    {
        /// <summary>
        /// Verifies a new response defaults to success with an empty id and statistics dictionary.
        /// </summary>
        [TestMethod]
        public void Defaults_AreExpected()
        {
            var response = new SummaryStatisticsResponse();
            Assert.IsTrue(response.Success);
            Assert.AreEqual(Guid.Empty, response.InputDataId);
            Assert.IsNotNull(response.Statistics);
            Assert.AreEqual(0, response.Statistics.Count);
        }

        /// <summary>
        /// Verifies every property, including a NaN-valued measure and the inherited response
        /// fields, survives a JSON round-trip.
        /// </summary>
        [TestMethod]
        public void Roundtrip_PreservesValues()
        {
            var response = new SummaryStatisticsResponse
            {
                InputDataId = Guid.NewGuid(),
                Statistics = new Dictionary<string, double>
                {
                    ["mean"] = 15200.5,
                    ["standardDeviation"] = 4100.25,
                    ["skew"] = double.NaN
                },
                Success = false,
                ErrorMessage = "stats incomplete",
                ValidationErrors = new List<string> { "e1" },
                ValidationWarnings = new List<string> { "w1" },
                ComputationTimeMs = 7,
                Timestamp = "2026-07-01T00:00:00.0000000Z",
                NonFiniteFindings = new List<string> { "$.statistics.skew" }
            };

            var copy = TestJson.Roundtrip(response);

            Assert.AreEqual(response.InputDataId, copy.InputDataId);
            Assert.AreEqual(3, copy.Statistics.Count);
            Assert.AreEqual(15200.5, copy.Statistics["mean"]);
            Assert.AreEqual(4100.25, copy.Statistics["standardDeviation"]);
            Assert.IsTrue(double.IsNaN(copy.Statistics["skew"]), "NaN measure should survive the round-trip.");
            Assert.IsFalse(copy.Success);
            Assert.AreEqual("stats incomplete", copy.ErrorMessage);
            CollectionAssert.AreEqual(response.ValidationErrors, copy.ValidationErrors);
            CollectionAssert.AreEqual(response.ValidationWarnings, copy.ValidationWarnings);
            Assert.AreEqual(7L, copy.ComputationTimeMs);
            Assert.AreEqual("2026-07-01T00:00:00.0000000Z", copy.Timestamp);
            CollectionAssert.AreEqual(response.NonFiniteFindings, copy.NonFiniteFindings);
        }

        /// <summary>
        /// Verifies the serialized JSON uses camelCase wire names and writes NaN as a named literal.
        /// </summary>
        [TestMethod]
        public void Serialize_UsesCamelCaseWireNames()
        {
            string json = TestJson.Serialize(new SummaryStatisticsResponse
            {
                Statistics = new Dictionary<string, double> { ["skew"] = double.NaN }
            });
            StringAssert.Contains(json, "\"inputDataId\"");
            StringAssert.Contains(json, "\"statistics\"");
            StringAssert.Contains(json, "\"NaN\"");
            Assert.IsFalse(json.Contains("\"errorMessage\""), "Null errorMessage should be omitted.");
        }
    }
}
