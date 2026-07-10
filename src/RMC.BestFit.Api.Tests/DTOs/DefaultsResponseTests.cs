using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.DTOs
{
    /// <summary>
    /// Unit tests for <see cref="DefaultsResponse"/>: defaults, JSON round-trip including the
    /// inherited <see cref="ResponseBase"/> fields, and camelCase wire names.
    /// </summary>
    [TestClass]
    public class DefaultsResponseTests
    {
        /// <summary>
        /// Verifies a new response defaults to success with empty ordinate/note lists and zero limits.
        /// </summary>
        [TestMethod]
        public void Defaults_AreExpected()
        {
            var response = new DefaultsResponse();
            Assert.IsTrue(response.Success);
            Assert.IsNotNull(response.ProbabilityOrdinates);
            Assert.AreEqual(0, response.ProbabilityOrdinates.Count);
            Assert.AreEqual(0, response.MaxIterations);
            Assert.AreEqual(0, response.MaxConcurrentRuns);
            Assert.AreEqual(0, response.MaxResources);
            Assert.IsNotNull(response.Notes);
            Assert.AreEqual(0, response.Notes.Count);
        }

        /// <summary>
        /// Verifies every property, including the inherited response fields, survives a JSON round-trip.
        /// </summary>
        [TestMethod]
        public void Roundtrip_PreservesValues()
        {
            var response = new DefaultsResponse
            {
                ProbabilityOrdinates = new List<double> { 0.5, 0.01, 0.001 },
                MaxIterations = 100000,
                MaxConcurrentRuns = 4,
                MaxResources = 500,
                Notes = new List<string> { "Probabilities are AEP.", "Resources are in-memory only." },
                Success = false,
                ErrorMessage = "defaults unavailable",
                ValidationErrors = new List<string> { "e1" },
                ValidationWarnings = new List<string> { "w1" },
                ComputationTimeMs = 6,
                Timestamp = "2026-07-01T00:00:00.0000000Z",
                NonFiniteFindings = new List<string> { "$.p" }
            };

            var copy = TestJson.Roundtrip(response);

            CollectionAssert.AreEqual(response.ProbabilityOrdinates, copy.ProbabilityOrdinates);
            Assert.AreEqual(100000, copy.MaxIterations);
            Assert.AreEqual(4, copy.MaxConcurrentRuns);
            Assert.AreEqual(500, copy.MaxResources);
            CollectionAssert.AreEqual(response.Notes, copy.Notes);
            Assert.IsFalse(copy.Success);
            Assert.AreEqual("defaults unavailable", copy.ErrorMessage);
            CollectionAssert.AreEqual(response.ValidationErrors, copy.ValidationErrors);
            CollectionAssert.AreEqual(response.ValidationWarnings, copy.ValidationWarnings);
            Assert.AreEqual(6L, copy.ComputationTimeMs);
            Assert.AreEqual("2026-07-01T00:00:00.0000000Z", copy.Timestamp);
            CollectionAssert.AreEqual(response.NonFiniteFindings, copy.NonFiniteFindings);
        }

        /// <summary>
        /// Verifies the serialized JSON uses camelCase wire names.
        /// </summary>
        [TestMethod]
        public void Serialize_UsesCamelCaseWireNames()
        {
            string json = TestJson.Serialize(new DefaultsResponse());
            StringAssert.Contains(json, "\"probabilityOrdinates\"");
            StringAssert.Contains(json, "\"maxIterations\"");
            StringAssert.Contains(json, "\"maxConcurrentRuns\"");
            StringAssert.Contains(json, "\"maxResources\"");
            StringAssert.Contains(json, "\"notes\"");
            Assert.IsFalse(json.Contains("\"errorMessage\""), "Null errorMessage should be omitted.");
        }
    }
}
