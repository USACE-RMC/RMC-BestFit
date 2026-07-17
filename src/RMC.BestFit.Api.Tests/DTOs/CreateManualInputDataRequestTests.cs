using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.DTOs
{
    /// <summary>
    /// Unit tests for <see cref="CreateManualInputDataRequest"/>: defaults, JSON round-trip, and
    /// camelCase wire names.
    /// </summary>
    [TestClass]
    public class CreateManualInputDataRequestTests
    {
        /// <summary>
        /// Verifies a new request defaults to an empty exact series with all optional settings null.
        /// </summary>
        [TestMethod]
        public void Defaults_AreExpected()
        {
            var request = new CreateManualInputDataRequest();
            Assert.IsNull(request.Name);
            Assert.IsNull(request.Description);
            Assert.IsNotNull(request.ExactData);
            Assert.AreEqual(0, request.ExactData.Count);
            Assert.IsNull(request.IntervalData);
            Assert.IsNull(request.ThresholdData);
            Assert.IsNull(request.PlottingParameter);
            Assert.IsNull(request.LowOutlierThreshold);
            Assert.IsNull(request.Lambda);
        }

        /// <summary>
        /// Verifies every property, including the nested observation lists, survives a JSON round-trip.
        /// </summary>
        [TestMethod]
        public void Roundtrip_PreservesValues()
        {
            var request = new CreateManualInputDataRequest
            {
                Name = "Historic record",
                Description = "Systematic plus paleoflood data.",
                ExactData = new List<ExactObservationDto>
                {
                    new ExactObservationDto { Index = 1996, Value = 15200.5, IsLowOutlier = true }
                },
                IntervalData = new List<IntervalObservationDto>
                {
                    new IntervalObservationDto { Index = 1889, LowerBound = 90000.0, UpperBound = 110000.0 }
                },
                ThresholdData = new List<ThresholdObservationDto>
                {
                    new ThresholdObservationDto { StartIndex = 1861, EndIndex = 1929, Value = 85000.0, NumberAbove = 1 }
                },
                PlottingParameter = 0.44,
                LowOutlierThreshold = 500.0,
                Lambda = 1.5
            };

            var copy = TestJson.Roundtrip(request);

            Assert.AreEqual("Historic record", copy.Name);
            Assert.AreEqual("Systematic plus paleoflood data.", copy.Description);
            Assert.AreEqual(1, copy.ExactData.Count);
            Assert.AreEqual(1996, copy.ExactData[0].Index);
            Assert.AreEqual(15200.5, copy.ExactData[0].Value);
            Assert.IsTrue(copy.ExactData[0].IsLowOutlier);
            Assert.IsNotNull(copy.IntervalData);
            Assert.AreEqual(90000.0, copy.IntervalData[0].LowerBound);
            Assert.IsNotNull(copy.ThresholdData);
            Assert.AreEqual(85000.0, copy.ThresholdData[0].Value);
            Assert.AreEqual(0.44, copy.PlottingParameter);
            Assert.AreEqual(500.0, copy.LowOutlierThreshold);
            Assert.AreEqual(1.5, copy.Lambda);
        }

        /// <summary>
        /// Verifies the serialized JSON uses camelCase wire names and omits null optional settings.
        /// </summary>
        [TestMethod]
        public void Serialize_UsesCamelCaseWireNames()
        {
            string json = TestJson.Serialize(new CreateManualInputDataRequest
            {
                PlottingParameter = 0.375,
                Lambda = 2.0
            });
            StringAssert.Contains(json, "\"exactData\"");
            StringAssert.Contains(json, "\"plottingParameter\"");
            StringAssert.Contains(json, "\"lambda\"");

            string defaultJson = TestJson.Serialize(new CreateManualInputDataRequest());
            Assert.IsFalse(defaultJson.Contains("\"intervalData\""), "Null intervalData should be omitted.");
            Assert.IsFalse(defaultJson.Contains("\"thresholdData\""), "Null thresholdData should be omitted.");
            Assert.IsFalse(defaultJson.Contains("\"lowOutlierThreshold\""), "Null lowOutlierThreshold should be omitted.");
        }
    }
}
