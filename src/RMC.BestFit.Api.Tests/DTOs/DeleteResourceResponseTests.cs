using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.DTOs
{
    /// <summary>
    /// Unit tests for <see cref="DeleteResourceResponse"/>: defaults, JSON round-trip including the
    /// inherited <see cref="ResponseBase"/> fields, and camelCase wire names.
    /// </summary>
    [TestClass]
    public class DeleteResourceResponseTests
    {
        /// <summary>
        /// Verifies a new response defaults to success with an empty id and no resource type.
        /// </summary>
        [TestMethod]
        public void Defaults_AreExpected()
        {
            var response = new DeleteResourceResponse();
            Assert.IsTrue(response.Success);
            Assert.AreEqual(Guid.Empty, response.DeletedId);
            Assert.IsNull(response.ResourceType);
        }

        /// <summary>
        /// Verifies every property, including the inherited response fields, survives a JSON round-trip.
        /// </summary>
        [TestMethod]
        public void Roundtrip_PreservesValues()
        {
            var response = new DeleteResourceResponse
            {
                DeletedId = Guid.NewGuid(),
                ResourceType = "timeSeries",
                Success = false,
                ErrorMessage = "delete failed",
                ValidationErrors = new List<string> { "bad id" },
                ValidationWarnings = new List<string> { "warn" },
                ComputationTimeMs = 42,
                Timestamp = "2026-07-01T00:00:00.0000000Z",
                NonFiniteFindings = new List<string> { "$.x" }
            };

            var copy = TestJson.Roundtrip(response);

            Assert.AreEqual(response.DeletedId, copy.DeletedId);
            Assert.AreEqual("timeSeries", copy.ResourceType);
            Assert.IsFalse(copy.Success);
            Assert.AreEqual("delete failed", copy.ErrorMessage);
            CollectionAssert.AreEqual(response.ValidationErrors, copy.ValidationErrors);
            CollectionAssert.AreEqual(response.ValidationWarnings, copy.ValidationWarnings);
            Assert.AreEqual(42L, copy.ComputationTimeMs);
            Assert.AreEqual("2026-07-01T00:00:00.0000000Z", copy.Timestamp);
            CollectionAssert.AreEqual(response.NonFiniteFindings, copy.NonFiniteFindings);
        }

        /// <summary>
        /// Verifies the serialized JSON uses camelCase wire names and omits the null resource type.
        /// </summary>
        [TestMethod]
        public void Serialize_UsesCamelCaseWireNames()
        {
            string json = TestJson.Serialize(new DeleteResourceResponse { ResourceType = "inputData" });
            StringAssert.Contains(json, "\"deletedId\"");
            StringAssert.Contains(json, "\"resourceType\"");
            StringAssert.Contains(json, "\"success\"");

            string defaultJson = TestJson.Serialize(new DeleteResourceResponse());
            Assert.IsFalse(defaultJson.Contains("\"resourceType\""), "Null resourceType should be omitted.");
        }
    }
}
