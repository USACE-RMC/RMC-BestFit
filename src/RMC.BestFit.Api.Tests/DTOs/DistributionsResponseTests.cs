using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.DTOs
{
    /// <summary>
    /// Unit tests for <see cref="DistributionsResponse"/>: defaults, JSON round-trip including the
    /// inherited <see cref="ResponseBase"/> fields, and camelCase wire names.
    /// </summary>
    [TestClass]
    public class DistributionsResponseTests
    {
        /// <summary>
        /// Verifies a new response defaults to success with an empty distribution list.
        /// </summary>
        [TestMethod]
        public void Defaults_AreExpected()
        {
            var response = new DistributionsResponse();
            Assert.IsTrue(response.Success);
            Assert.IsNotNull(response.Distributions);
            Assert.AreEqual(0, response.Distributions.Count);
        }

        /// <summary>
        /// Verifies every property, including the inherited response fields, survives a JSON round-trip.
        /// </summary>
        [TestMethod]
        public void Roundtrip_PreservesValues()
        {
            var response = new DistributionsResponse
            {
                Distributions = new List<DistributionInfoDto>
                {
                    new DistributionInfoDto
                    {
                        Name = "logPearsonTypeIII",
                        DisplayName = "Log-Pearson Type III",
                        SupportedBy = new List<string> { "univariate", "bulletin17c" }
                    }
                },
                Success = false,
                ErrorMessage = "discovery failed",
                ValidationErrors = new List<string> { "e1" },
                ValidationWarnings = new List<string> { "w1" },
                ComputationTimeMs = 2,
                Timestamp = "2026-07-01T00:00:00.0000000Z",
                NonFiniteFindings = new List<string> { "$.d" }
            };

            var copy = TestJson.Roundtrip(response);

            Assert.AreEqual(1, copy.Distributions.Count);
            Assert.AreEqual("logPearsonTypeIII", copy.Distributions[0].Name);
            Assert.AreEqual("Log-Pearson Type III", copy.Distributions[0].DisplayName);
            CollectionAssert.AreEqual(response.Distributions[0].SupportedBy, copy.Distributions[0].SupportedBy);
            Assert.IsFalse(copy.Success);
            Assert.AreEqual("discovery failed", copy.ErrorMessage);
            CollectionAssert.AreEqual(response.ValidationErrors, copy.ValidationErrors);
            CollectionAssert.AreEqual(response.ValidationWarnings, copy.ValidationWarnings);
            Assert.AreEqual(2L, copy.ComputationTimeMs);
            Assert.AreEqual("2026-07-01T00:00:00.0000000Z", copy.Timestamp);
            CollectionAssert.AreEqual(response.NonFiniteFindings, copy.NonFiniteFindings);
        }

        /// <summary>
        /// Verifies the serialized JSON uses camelCase wire names.
        /// </summary>
        [TestMethod]
        public void Serialize_UsesCamelCaseWireNames()
        {
            string json = TestJson.Serialize(new DistributionsResponse());
            StringAssert.Contains(json, "\"distributions\"");
            StringAssert.Contains(json, "\"success\"");
            Assert.IsFalse(json.Contains("\"errorMessage\""), "Null errorMessage should be omitted.");
        }
    }
}
