using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.DTOs
{
    /// <summary>
    /// Unit tests for <see cref="EnumOptionsResponse"/>: defaults, JSON round-trip including the
    /// inherited <see cref="ResponseBase"/> fields, and camelCase wire names.
    /// </summary>
    [TestClass]
    public class EnumOptionsResponseTests
    {
        /// <summary>
        /// Verifies a new response defaults to success with all ten option lists empty.
        /// </summary>
        [TestMethod]
        public void Defaults_AreExpected()
        {
            var response = new EnumOptionsResponse();
            Assert.IsTrue(response.Success);
            Assert.IsNotNull(response.Samplers);
            Assert.AreEqual(0, response.Samplers.Count);
            Assert.IsNotNull(response.PointEstimators);
            Assert.AreEqual(0, response.PointEstimators.Count);
            Assert.IsNotNull(response.UncertaintyMethods);
            Assert.AreEqual(0, response.UncertaintyMethods.Count);
            Assert.IsNotNull(response.TimeBlockWindows);
            Assert.AreEqual(0, response.TimeBlockWindows.Count);
            Assert.IsNotNull(response.BlockFunctions);
            Assert.AreEqual(0, response.BlockFunctions.Count);
            Assert.IsNotNull(response.SmoothingFunctions);
            Assert.AreEqual(0, response.SmoothingFunctions.Count);
            Assert.IsNotNull(response.UsgsSeriesTypes);
            Assert.AreEqual(0, response.UsgsSeriesTypes.Count);
            Assert.IsNotNull(response.TimeIntervals);
            Assert.AreEqual(0, response.TimeIntervals.Count);
            Assert.IsNotNull(response.AnalysisKinds);
            Assert.AreEqual(0, response.AnalysisKinds.Count);
            Assert.IsNotNull(response.InputDataMethods);
            Assert.AreEqual(0, response.InputDataMethods.Count);
        }

        /// <summary>
        /// Verifies every option list, plus the inherited response fields, survives a JSON round-trip.
        /// </summary>
        [TestMethod]
        public void Roundtrip_PreservesValues()
        {
            var response = new EnumOptionsResponse
            {
                Samplers = new List<string> { "demCzs" },
                PointEstimators = new List<string> { "posteriorMean", "posteriorMode" },
                UncertaintyMethods = new List<string> { "multivariateNormal" },
                TimeBlockWindows = new List<string> { "waterYear", "calendarYear" },
                BlockFunctions = new List<string> { "maximum" },
                SmoothingFunctions = new List<string> { "none", "movingAverage" },
                UsgsSeriesTypes = new List<string> { "dailyDischarge", "peakDischarge" },
                TimeIntervals = new List<string> { "oneDay", "irregular" },
                AnalysisKinds = new List<string> { "univariate", "bulletin17c" },
                InputDataMethods = new List<string> { "manual", "blockMaxima" },
                Success = false,
                ErrorMessage = "options unavailable",
                ValidationErrors = new List<string> { "e1" },
                ValidationWarnings = new List<string> { "w1" },
                ComputationTimeMs = 1,
                Timestamp = "2026-07-01T00:00:00.0000000Z",
                NonFiniteFindings = new List<string> { "$.n" }
            };

            var copy = TestJson.Roundtrip(response);

            CollectionAssert.AreEqual(response.Samplers, copy.Samplers);
            CollectionAssert.AreEqual(response.PointEstimators, copy.PointEstimators);
            CollectionAssert.AreEqual(response.UncertaintyMethods, copy.UncertaintyMethods);
            CollectionAssert.AreEqual(response.TimeBlockWindows, copy.TimeBlockWindows);
            CollectionAssert.AreEqual(response.BlockFunctions, copy.BlockFunctions);
            CollectionAssert.AreEqual(response.SmoothingFunctions, copy.SmoothingFunctions);
            CollectionAssert.AreEqual(response.UsgsSeriesTypes, copy.UsgsSeriesTypes);
            CollectionAssert.AreEqual(response.TimeIntervals, copy.TimeIntervals);
            CollectionAssert.AreEqual(response.AnalysisKinds, copy.AnalysisKinds);
            CollectionAssert.AreEqual(response.InputDataMethods, copy.InputDataMethods);
            Assert.IsFalse(copy.Success);
            Assert.AreEqual("options unavailable", copy.ErrorMessage);
            CollectionAssert.AreEqual(response.ValidationErrors, copy.ValidationErrors);
            CollectionAssert.AreEqual(response.ValidationWarnings, copy.ValidationWarnings);
            Assert.AreEqual(1L, copy.ComputationTimeMs);
            Assert.AreEqual("2026-07-01T00:00:00.0000000Z", copy.Timestamp);
            CollectionAssert.AreEqual(response.NonFiniteFindings, copy.NonFiniteFindings);
        }

        /// <summary>
        /// Verifies the serialized JSON uses camelCase wire names.
        /// </summary>
        [TestMethod]
        public void Serialize_UsesCamelCaseWireNames()
        {
            string json = TestJson.Serialize(new EnumOptionsResponse());
            StringAssert.Contains(json, "\"samplers\"");
            StringAssert.Contains(json, "\"pointEstimators\"");
            StringAssert.Contains(json, "\"timeBlockWindows\"");
            StringAssert.Contains(json, "\"usgsSeriesTypes\"");
            StringAssert.Contains(json, "\"inputDataMethods\"");
            Assert.IsFalse(json.Contains("\"errorMessage\""), "Null errorMessage should be omitted.");
        }
    }
}
