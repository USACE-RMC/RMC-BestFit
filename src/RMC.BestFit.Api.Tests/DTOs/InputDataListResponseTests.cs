using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.DTOs
{
    /// <summary>
    /// Unit tests for <see cref="InputDataListResponse"/>: defaults, JSON round-trip including the
    /// inherited <see cref="ResponseBase"/> fields, and camelCase wire names.
    /// </summary>
    [TestClass]
    public class InputDataListResponseTests
    {
        /// <summary>
        /// Verifies a new listing defaults to success with a zero count and an empty summary list.
        /// </summary>
        [TestMethod]
        public void Defaults_AreExpected()
        {
            var response = new InputDataListResponse();
            Assert.IsTrue(response.Success);
            Assert.AreEqual(0, response.Count);
            Assert.IsNotNull(response.InputData);
            Assert.AreEqual(0, response.InputData.Count);
        }

        /// <summary>
        /// Verifies every property, including the inherited response fields, survives a JSON round-trip.
        /// </summary>
        [TestMethod]
        public void Roundtrip_PreservesValues()
        {
            var response = new InputDataListResponse
            {
                Count = 1,
                InputData = new List<InputDataSummaryDto>
                {
                    new InputDataSummaryDto { Id = Guid.NewGuid(), Name = "Record A", RecordLength = 96 }
                },
                Success = false,
                ErrorMessage = "listing incomplete",
                ValidationErrors = new List<string> { "e1" },
                ValidationWarnings = new List<string> { "w1" },
                ComputationTimeMs = 4,
                Timestamp = "2026-07-01T00:00:00.0000000Z",
                NonFiniteFindings = new List<string> { "$.q" }
            };

            var copy = TestJson.Roundtrip(response);

            Assert.AreEqual(1, copy.Count);
            Assert.AreEqual(1, copy.InputData.Count);
            Assert.AreEqual(response.InputData[0].Id, copy.InputData[0].Id);
            Assert.AreEqual("Record A", copy.InputData[0].Name);
            Assert.AreEqual(96, copy.InputData[0].RecordLength);
            Assert.IsFalse(copy.Success);
            Assert.AreEqual("listing incomplete", copy.ErrorMessage);
            CollectionAssert.AreEqual(response.ValidationErrors, copy.ValidationErrors);
            CollectionAssert.AreEqual(response.ValidationWarnings, copy.ValidationWarnings);
            Assert.AreEqual(4L, copy.ComputationTimeMs);
            Assert.AreEqual("2026-07-01T00:00:00.0000000Z", copy.Timestamp);
            CollectionAssert.AreEqual(response.NonFiniteFindings, copy.NonFiniteFindings);
        }

        /// <summary>
        /// Verifies the serialized JSON uses camelCase wire names.
        /// </summary>
        [TestMethod]
        public void Serialize_UsesCamelCaseWireNames()
        {
            string json = TestJson.Serialize(new InputDataListResponse());
            StringAssert.Contains(json, "\"count\"");
            StringAssert.Contains(json, "\"inputData\"");
            StringAssert.Contains(json, "\"success\"");
            Assert.IsFalse(json.Contains("\"errorMessage\""), "Null errorMessage should be omitted.");
        }
    }
}
