using Numerics.Data;
using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.DTOs
{
    /// <summary>
    /// Unit tests for <see cref="CreatePotInputDataRequest"/>: defaults, JSON round-trip, and
    /// camelCase wire names including string enum values.
    /// </summary>
    [TestClass]
    public class CreatePotInputDataRequestTests
    {
        /// <summary>
        /// Verifies a new request defaults to a zero threshold with no smoothing and unit spacing.
        /// </summary>
        [TestMethod]
        public void Defaults_AreExpected()
        {
            var request = new CreatePotInputDataRequest();
            Assert.AreEqual(Guid.Empty, request.TimeSeriesId);
            Assert.IsNull(request.Name);
            Assert.IsNull(request.Description);
            Assert.AreEqual(0.0, request.Threshold);
            Assert.AreEqual(1, request.MinStepsBetweenPeaks);
            Assert.AreEqual(SmoothingFunctionType.None, request.SmoothingFunction);
            Assert.AreEqual(1, request.Period);
            Assert.IsNull(request.Lambda);
        }

        /// <summary>
        /// Verifies every property, including the enum-typed smoothing option, survives a JSON round-trip.
        /// </summary>
        [TestMethod]
        public void Roundtrip_PreservesValues()
        {
            var request = new CreatePotInputDataRequest
            {
                TimeSeriesId = Guid.NewGuid(),
                Name = "POT events",
                Description = "Peaks above 10000 cfs.",
                Threshold = 10000.0,
                MinStepsBetweenPeaks = 7,
                SmoothingFunction = SmoothingFunctionType.MovingSum,
                Period = 3,
                Lambda = 2.4
            };

            var copy = TestJson.Roundtrip(request);

            Assert.AreEqual(request.TimeSeriesId, copy.TimeSeriesId);
            Assert.AreEqual("POT events", copy.Name);
            Assert.AreEqual("Peaks above 10000 cfs.", copy.Description);
            Assert.AreEqual(10000.0, copy.Threshold);
            Assert.AreEqual(7, copy.MinStepsBetweenPeaks);
            Assert.AreEqual(SmoothingFunctionType.MovingSum, copy.SmoothingFunction);
            Assert.AreEqual(3, copy.Period);
            Assert.AreEqual(2.4, copy.Lambda);
        }

        /// <summary>
        /// Verifies the serialized JSON uses camelCase wire names and camelCase enum value strings.
        /// </summary>
        [TestMethod]
        public void Serialize_UsesCamelCaseWireNames()
        {
            string json = TestJson.Serialize(new CreatePotInputDataRequest());
            StringAssert.Contains(json, "\"timeSeriesId\"");
            StringAssert.Contains(json, "\"threshold\"");
            StringAssert.Contains(json, "\"minStepsBetweenPeaks\"");
            StringAssert.Contains(json, "\"none\"");
            Assert.IsFalse(json.Contains("\"lambda\""), "Null lambda should be omitted.");
            Assert.IsFalse(json.Contains("\"name\""), "Null name should be omitted.");
        }
    }
}
