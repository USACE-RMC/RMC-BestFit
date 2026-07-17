using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.DTOs
{
    /// <summary>
    /// Unit tests for <see cref="ResponseBase"/>: shared success/error/diagnostic contract defaults,
    /// JSON round-trip, and camelCase wire names, exercised through a private concrete subclass.
    /// </summary>
    [TestClass]
    public class ResponseBaseTests
    {
        /// <summary>
        /// Concrete subclass used to instantiate the abstract <see cref="ResponseBase"/> in tests.
        /// </summary>
        private sealed class TestResponse : ResponseBase
        {
        }

        /// <summary>
        /// Verifies a new response defaults to success with all diagnostic fields null.
        /// </summary>
        [TestMethod]
        public void Defaults_AreExpected()
        {
            var response = new TestResponse();
            Assert.IsTrue(response.Success);
            Assert.IsNull(response.ErrorMessage);
            Assert.IsNull(response.ValidationErrors);
            Assert.IsNull(response.ValidationWarnings);
            Assert.IsNull(response.ComputationTimeMs);
            Assert.IsNull(response.Timestamp);
            Assert.IsNull(response.NonFiniteFindings);
        }

        /// <summary>
        /// Verifies every shared field survives a JSON round-trip with the API's wire options.
        /// </summary>
        [TestMethod]
        public void Roundtrip_PreservesValues()
        {
            var response = new TestResponse
            {
                Success = false,
                ErrorMessage = "Something failed.",
                ValidationErrors = new List<string> { "error one", "error two" },
                ValidationWarnings = new List<string> { "warning one" },
                ComputationTimeMs = 1234,
                Timestamp = "2026-07-01T12:00:00.0000000Z",
                NonFiniteFindings = new List<string> { "$.statistics.skew" }
            };

            var copy = TestJson.Roundtrip(response);

            Assert.IsFalse(copy.Success);
            Assert.AreEqual("Something failed.", copy.ErrorMessage);
            CollectionAssert.AreEqual(response.ValidationErrors, copy.ValidationErrors);
            CollectionAssert.AreEqual(response.ValidationWarnings, copy.ValidationWarnings);
            Assert.AreEqual(1234L, copy.ComputationTimeMs);
            Assert.AreEqual("2026-07-01T12:00:00.0000000Z", copy.Timestamp);
            CollectionAssert.AreEqual(response.NonFiniteFindings, copy.NonFiniteFindings);
        }

        /// <summary>
        /// Verifies the serialized JSON uses camelCase wire names and omits null diagnostic fields.
        /// </summary>
        [TestMethod]
        public void Serialize_UsesCamelCaseWireNames()
        {
            string json = TestJson.Serialize(new TestResponse { ErrorMessage = "boom" });
            StringAssert.Contains(json, "\"success\"");
            StringAssert.Contains(json, "\"errorMessage\"");

            string defaultJson = TestJson.Serialize(new TestResponse());
            Assert.IsFalse(defaultJson.Contains("\"errorMessage\""), "Null errorMessage should be omitted.");
            Assert.IsFalse(defaultJson.Contains("\"validationErrors\""), "Null validationErrors should be omitted.");
            Assert.IsFalse(defaultJson.Contains("\"nonFiniteFindings\""), "Null nonFiniteFindings should be omitted.");
        }
    }
}
