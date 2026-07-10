using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.DTOs
{
    /// <summary>
    /// Unit tests for <see cref="InputDataResourceResponse"/>: defaults, JSON round-trip including
    /// the inherited <see cref="ResponseBase"/> fields, and camelCase wire names.
    /// </summary>
    [TestClass]
    public class InputDataResourceResponseTests
    {
        /// <summary>
        /// Verifies a new response defaults to success with no summary or observation lists.
        /// </summary>
        [TestMethod]
        public void Defaults_AreExpected()
        {
            var response = new InputDataResourceResponse();
            Assert.IsTrue(response.Success);
            Assert.IsNull(response.InputData);
            Assert.IsNull(response.ExactData);
            Assert.IsNull(response.IntervalData);
            Assert.IsNull(response.ThresholdData);
        }

        /// <summary>
        /// Verifies every property, including the inherited response fields, survives a JSON round-trip.
        /// </summary>
        [TestMethod]
        public void Roundtrip_PreservesValues()
        {
            var response = new InputDataResourceResponse
            {
                InputData = new InputDataSummaryDto { Id = Guid.NewGuid(), Name = "Annual maxima", ExactCount = 1 },
                ExactData = new List<ExactObservationDto>
                {
                    new ExactObservationDto { Index = 1996, Value = 15200.5, PlottingPosition = 0.25 }
                },
                IntervalData = new List<IntervalObservationDto>
                {
                    new IntervalObservationDto { Index = 1889, LowerBound = 90000.0, UpperBound = 110000.0 }
                },
                ThresholdData = new List<ThresholdObservationDto>
                {
                    new ThresholdObservationDto { StartIndex = 1861, EndIndex = 1929, Value = 85000.0 }
                },
                Success = false,
                ErrorMessage = "partial data",
                ValidationErrors = new List<string> { "e1" },
                ValidationWarnings = new List<string> { "w1" },
                ComputationTimeMs = 11,
                Timestamp = "2026-07-01T00:00:00.0000000Z",
                NonFiniteFindings = new List<string> { "$.exactData[0].value" }
            };

            var copy = TestJson.Roundtrip(response);

            Assert.IsNotNull(copy.InputData);
            Assert.AreEqual(response.InputData.Id, copy.InputData.Id);
            Assert.AreEqual("Annual maxima", copy.InputData.Name);
            Assert.IsNotNull(copy.ExactData);
            Assert.AreEqual(15200.5, copy.ExactData[0].Value);
            Assert.AreEqual(0.25, copy.ExactData[0].PlottingPosition);
            Assert.IsNotNull(copy.IntervalData);
            Assert.AreEqual(110000.0, copy.IntervalData[0].UpperBound);
            Assert.IsNotNull(copy.ThresholdData);
            Assert.AreEqual(85000.0, copy.ThresholdData[0].Value);
            Assert.IsFalse(copy.Success);
            Assert.AreEqual("partial data", copy.ErrorMessage);
            CollectionAssert.AreEqual(response.ValidationErrors, copy.ValidationErrors);
            CollectionAssert.AreEqual(response.ValidationWarnings, copy.ValidationWarnings);
            Assert.AreEqual(11L, copy.ComputationTimeMs);
            Assert.AreEqual("2026-07-01T00:00:00.0000000Z", copy.Timestamp);
            CollectionAssert.AreEqual(response.NonFiniteFindings, copy.NonFiniteFindings);
        }

        /// <summary>
        /// Verifies the serialized JSON uses camelCase wire names and omits null observation lists.
        /// </summary>
        [TestMethod]
        public void Serialize_UsesCamelCaseWireNames()
        {
            string json = TestJson.Serialize(new InputDataResourceResponse
            {
                InputData = new InputDataSummaryDto()
            });
            StringAssert.Contains(json, "\"inputData\"");
            StringAssert.Contains(json, "\"success\"");

            string defaultJson = TestJson.Serialize(new InputDataResourceResponse());
            Assert.IsFalse(defaultJson.Contains("\"exactData\""), "Null exactData should be omitted.");
            Assert.IsFalse(defaultJson.Contains("\"intervalData\""), "Null intervalData should be omitted.");
            Assert.IsFalse(defaultJson.Contains("\"thresholdData\""), "Null thresholdData should be omitted.");
        }
    }
}
