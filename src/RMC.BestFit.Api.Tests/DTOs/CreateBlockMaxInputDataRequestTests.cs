using Numerics.Data;
using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.DTOs
{
    /// <summary>
    /// Unit tests for <see cref="CreateBlockMaxInputDataRequest"/>: defaults, JSON round-trip, and
    /// camelCase wire names including string enum values.
    /// </summary>
    [TestClass]
    public class CreateBlockMaxInputDataRequestTests
    {
        /// <summary>
        /// Verifies a new request defaults to water-year maxima with no smoothing and the U.S.
        /// water-year month window.
        /// </summary>
        [TestMethod]
        public void Defaults_AreExpected()
        {
            var request = new CreateBlockMaxInputDataRequest();
            Assert.AreEqual(Guid.Empty, request.TimeSeriesId);
            Assert.IsNull(request.Name);
            Assert.IsNull(request.Description);
            Assert.AreEqual(TimeBlockWindow.WaterYear, request.TimeBlock);
            Assert.AreEqual(BlockFunctionType.Maximum, request.BlockFunction);
            Assert.AreEqual(SmoothingFunctionType.None, request.SmoothingFunction);
            Assert.AreEqual(10, request.StartMonth);
            Assert.AreEqual(9, request.EndMonth);
            Assert.AreEqual(1, request.Period);
        }

        /// <summary>
        /// Verifies every property, including the three enum-typed options, survives a JSON round-trip.
        /// </summary>
        [TestMethod]
        public void Roundtrip_PreservesValues()
        {
            var request = new CreateBlockMaxInputDataRequest
            {
                TimeSeriesId = Guid.NewGuid(),
                Name = "7-day minima",
                Description = "Custom year low-flow extraction.",
                TimeBlock = TimeBlockWindow.CustomYear,
                BlockFunction = BlockFunctionType.Minimum,
                SmoothingFunction = SmoothingFunctionType.MovingAverage,
                StartMonth = 4,
                EndMonth = 3,
                Period = 7
            };

            var copy = TestJson.Roundtrip(request);

            Assert.AreEqual(request.TimeSeriesId, copy.TimeSeriesId);
            Assert.AreEqual("7-day minima", copy.Name);
            Assert.AreEqual("Custom year low-flow extraction.", copy.Description);
            Assert.AreEqual(TimeBlockWindow.CustomYear, copy.TimeBlock);
            Assert.AreEqual(BlockFunctionType.Minimum, copy.BlockFunction);
            Assert.AreEqual(SmoothingFunctionType.MovingAverage, copy.SmoothingFunction);
            Assert.AreEqual(4, copy.StartMonth);
            Assert.AreEqual(3, copy.EndMonth);
            Assert.AreEqual(7, copy.Period);
        }

        /// <summary>
        /// Verifies the serialized JSON uses camelCase wire names and camelCase enum value strings.
        /// </summary>
        [TestMethod]
        public void Serialize_UsesCamelCaseWireNames()
        {
            string json = TestJson.Serialize(new CreateBlockMaxInputDataRequest());
            StringAssert.Contains(json, "\"timeSeriesId\"");
            StringAssert.Contains(json, "\"timeBlock\"");
            StringAssert.Contains(json, "\"waterYear\"");
            StringAssert.Contains(json, "\"maximum\"");
            StringAssert.Contains(json, "\"startMonth\"");
            Assert.IsFalse(json.Contains("\"name\""), "Null name should be omitted.");
        }
    }
}
