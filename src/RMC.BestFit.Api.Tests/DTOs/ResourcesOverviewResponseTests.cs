using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.DTOs
{
    /// <summary>
    /// Unit tests for <see cref="ResourcesOverviewResponse"/>: defaults, JSON round-trip including
    /// the inherited <see cref="ResponseBase"/> fields, and camelCase wire names.
    /// </summary>
    [TestClass]
    public class ResourcesOverviewResponseTests
    {
        /// <summary>
        /// Verifies a new overview defaults to success with zero counts and an empty resource list.
        /// </summary>
        [TestMethod]
        public void Defaults_AreExpected()
        {
            var response = new ResourcesOverviewResponse();
            Assert.IsTrue(response.Success);
            Assert.AreEqual(0, response.TimeSeriesCount);
            Assert.AreEqual(0, response.InputDataCount);
            Assert.AreEqual(0, response.AnalysisCount);
            Assert.IsNotNull(response.Resources);
            Assert.AreEqual(0, response.Resources.Count);
        }

        /// <summary>
        /// Verifies every property, including the inherited response fields, survives a JSON round-trip.
        /// </summary>
        [TestMethod]
        public void Roundtrip_PreservesValues()
        {
            var response = new ResourcesOverviewResponse
            {
                TimeSeriesCount = 2,
                InputDataCount = 3,
                AnalysisCount = 4,
                Resources = new List<ResourceSummaryDto>
                {
                    new ResourceSummaryDto
                    {
                        Id = Guid.NewGuid(),
                        ResourceType = "timeSeries",
                        Name = "Potomac daily",
                        CreatedUtc = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                        Detail = "36500 points"
                    }
                },
                Success = false,
                ErrorMessage = "partial",
                ValidationErrors = new List<string> { "e1" },
                ValidationWarnings = new List<string> { "w1" },
                ComputationTimeMs = 5,
                Timestamp = "2026-07-01T00:00:00.0000000Z",
                NonFiniteFindings = new List<string> { "$.y" }
            };

            var copy = TestJson.Roundtrip(response);

            Assert.AreEqual(2, copy.TimeSeriesCount);
            Assert.AreEqual(3, copy.InputDataCount);
            Assert.AreEqual(4, copy.AnalysisCount);
            Assert.AreEqual(1, copy.Resources.Count);
            Assert.AreEqual(response.Resources[0].Id, copy.Resources[0].Id);
            Assert.AreEqual("Potomac daily", copy.Resources[0].Name);
            Assert.IsFalse(copy.Success);
            Assert.AreEqual("partial", copy.ErrorMessage);
            CollectionAssert.AreEqual(response.ValidationErrors, copy.ValidationErrors);
            CollectionAssert.AreEqual(response.ValidationWarnings, copy.ValidationWarnings);
            Assert.AreEqual(5L, copy.ComputationTimeMs);
            Assert.AreEqual("2026-07-01T00:00:00.0000000Z", copy.Timestamp);
            CollectionAssert.AreEqual(response.NonFiniteFindings, copy.NonFiniteFindings);
        }

        /// <summary>
        /// Verifies the serialized JSON uses camelCase wire names.
        /// </summary>
        [TestMethod]
        public void Serialize_UsesCamelCaseWireNames()
        {
            string json = TestJson.Serialize(new ResourcesOverviewResponse());
            StringAssert.Contains(json, "\"timeSeriesCount\"");
            StringAssert.Contains(json, "\"inputDataCount\"");
            StringAssert.Contains(json, "\"analysisCount\"");
            StringAssert.Contains(json, "\"resources\"");
            Assert.IsFalse(json.Contains("\"errorMessage\""), "Null errorMessage should be omitted.");
        }
    }
}
