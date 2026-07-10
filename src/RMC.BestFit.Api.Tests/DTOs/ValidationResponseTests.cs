using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.DTOs
{
    /// <summary>
    /// Unit tests for <see cref="ValidationResponse"/>: defaults, JSON round-trip, and camelCase
    /// wire names.
    /// </summary>
    [TestClass]
    public class ValidationResponseTests
    {
        /// <summary>
        /// Verifies a new response defaults to invalid with empty error and warning lists.
        /// </summary>
        [TestMethod]
        public void Defaults_AreExpected()
        {
            var response = new ValidationResponse();
            Assert.IsFalse(response.IsValid);
            Assert.IsNotNull(response.Errors);
            Assert.AreEqual(0, response.Errors.Count);
            Assert.IsNotNull(response.Warnings);
            Assert.AreEqual(0, response.Warnings.Count);
        }

        /// <summary>
        /// Verifies the verdict, errors, and warnings survive a JSON round-trip.
        /// </summary>
        [TestMethod]
        public void Roundtrip_PreservesValues()
        {
            var response = new ValidationResponse
            {
                IsValid = true,
                Errors = new List<string> { "The input data has no observations." },
                Warnings = new List<string> { "Record length is short." }
            };

            var copy = TestJson.Roundtrip(response);

            Assert.IsTrue(copy.IsValid);
            CollectionAssert.AreEqual(response.Errors, copy.Errors);
            CollectionAssert.AreEqual(response.Warnings, copy.Warnings);
        }

        /// <summary>
        /// Verifies the serialized JSON uses camelCase wire names and does not carry the
        /// <see cref="ResponseBase"/> envelope (this response type is standalone).
        /// </summary>
        [TestMethod]
        public void Serialize_UsesCamelCaseWireNames()
        {
            string json = TestJson.Serialize(new ValidationResponse());
            StringAssert.Contains(json, "\"isValid\"");
            StringAssert.Contains(json, "\"errors\"");
            StringAssert.Contains(json, "\"warnings\"");
            Assert.IsFalse(json.Contains("\"success\""), "ValidationResponse should not carry the ResponseBase envelope.");
        }
    }
}
