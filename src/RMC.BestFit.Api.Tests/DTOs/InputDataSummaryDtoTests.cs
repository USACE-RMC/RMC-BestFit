using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.DTOs
{
    /// <summary>
    /// Unit tests for <see cref="InputDataSummaryDto"/>: defaults, JSON round-trip including the
    /// nested option echoes, and camelCase wire names.
    /// </summary>
    [TestClass]
    public class InputDataSummaryDtoTests
    {
        /// <summary>
        /// Verifies a new summary defaults to an empty id, zero counts, and null optional fields.
        /// </summary>
        [TestMethod]
        public void Defaults_AreExpected()
        {
            var dto = new InputDataSummaryDto();
            Assert.AreEqual(Guid.Empty, dto.Id);
            Assert.IsNull(dto.Name);
            Assert.IsNull(dto.Description);
            Assert.AreEqual(default, dto.CreatedUtc);
            Assert.IsNull(dto.Method);
            Assert.IsNull(dto.SourceTimeSeriesId);
            Assert.IsNull(dto.UsgsSiteNumber);
            Assert.AreEqual(0, dto.RecordLength);
            Assert.AreEqual(0, dto.ExactCount);
            Assert.AreEqual(0, dto.IntervalCount);
            Assert.AreEqual(0, dto.ThresholdCount);
            Assert.AreEqual(0.0, dto.Lambda);
            Assert.AreEqual(0.0, dto.PlottingParameter);
            Assert.IsNull(dto.LowOutlierThreshold);
            Assert.AreEqual(0, dto.LowOutlierCount);
            Assert.IsNull(dto.BlockOptions);
            Assert.IsNull(dto.PotOptions);
        }

        /// <summary>
        /// Verifies every property, including the nested block and POT option echoes, survives a
        /// JSON round-trip.
        /// </summary>
        [TestMethod]
        public void Roundtrip_PreservesValues()
        {
            var dto = new InputDataSummaryDto
            {
                Id = Guid.NewGuid(),
                Name = "Annual maxima",
                Description = "Block maxima of daily flows.",
                CreatedUtc = new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Utc),
                Method = "blockMaxima",
                SourceTimeSeriesId = Guid.NewGuid(),
                UsgsSiteNumber = "01646500",
                RecordLength = 96,
                ExactCount = 95,
                IntervalCount = 1,
                ThresholdCount = 2,
                Lambda = 1.0,
                PlottingParameter = 0.44,
                LowOutlierThreshold = 500.0,
                LowOutlierCount = 3,
                BlockOptions = new BlockOptionsDto { TimeBlock = "waterYear", StartMonth = 10 },
                PotOptions = new PotOptionsDto { Threshold = 10000.0, Period = 3 }
            };

            var copy = TestJson.Roundtrip(dto);

            Assert.AreEqual(dto.Id, copy.Id);
            Assert.AreEqual("Annual maxima", copy.Name);
            Assert.AreEqual("Block maxima of daily flows.", copy.Description);
            Assert.AreEqual(dto.CreatedUtc, copy.CreatedUtc);
            Assert.AreEqual("blockMaxima", copy.Method);
            Assert.AreEqual(dto.SourceTimeSeriesId, copy.SourceTimeSeriesId);
            Assert.AreEqual("01646500", copy.UsgsSiteNumber);
            Assert.AreEqual(96, copy.RecordLength);
            Assert.AreEqual(95, copy.ExactCount);
            Assert.AreEqual(1, copy.IntervalCount);
            Assert.AreEqual(2, copy.ThresholdCount);
            Assert.AreEqual(1.0, copy.Lambda);
            Assert.AreEqual(0.44, copy.PlottingParameter);
            Assert.AreEqual(500.0, copy.LowOutlierThreshold);
            Assert.AreEqual(3, copy.LowOutlierCount);
            Assert.IsNotNull(copy.BlockOptions);
            Assert.AreEqual("waterYear", copy.BlockOptions.TimeBlock);
            Assert.AreEqual(10, copy.BlockOptions.StartMonth);
            Assert.IsNotNull(copy.PotOptions);
            Assert.AreEqual(10000.0, copy.PotOptions.Threshold);
            Assert.AreEqual(3, copy.PotOptions.Period);
        }

        /// <summary>
        /// Verifies the serialized JSON uses camelCase wire names and omits null optional fields.
        /// </summary>
        [TestMethod]
        public void Serialize_UsesCamelCaseWireNames()
        {
            string json = TestJson.Serialize(new InputDataSummaryDto { Method = "manual" });
            StringAssert.Contains(json, "\"recordLength\"");
            StringAssert.Contains(json, "\"lambda\"");
            StringAssert.Contains(json, "\"plottingParameter\"");
            StringAssert.Contains(json, "\"method\"");

            string defaultJson = TestJson.Serialize(new InputDataSummaryDto());
            Assert.IsFalse(defaultJson.Contains("\"blockOptions\""), "Null blockOptions should be omitted.");
            Assert.IsFalse(defaultJson.Contains("\"potOptions\""), "Null potOptions should be omitted.");
            Assert.IsFalse(defaultJson.Contains("\"sourceTimeSeriesId\""), "Null sourceTimeSeriesId should be omitted.");
        }
    }
}
