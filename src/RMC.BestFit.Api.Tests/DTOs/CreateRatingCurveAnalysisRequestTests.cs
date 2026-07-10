using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.DTOs
{
    /// <summary>
    /// Unit tests for <see cref="CreateRatingCurveAnalysisRequest"/>: defaults, JSON round-trip,
    /// and camelCase wire names.
    /// </summary>
    [TestClass]
    public class CreateRatingCurveAnalysisRequestTests
    {
        /// <summary>
        /// Verifies a new request defaults to a single segment with no stage grid overrides,
        /// Bayesian options, name, or description.
        /// </summary>
        [TestMethod]
        public void Defaults_AreExpected()
        {
            var request = new CreateRatingCurveAnalysisRequest();
            Assert.AreEqual(Guid.Empty, request.StageTimeSeriesId);
            Assert.AreEqual(Guid.Empty, request.DischargeTimeSeriesId);
            Assert.AreEqual(1, request.NumberOfSegments);
            Assert.IsNull(request.MinStage);
            Assert.IsNull(request.MaxStage);
            Assert.IsNull(request.StageBins);
            Assert.IsNull(request.BayesianOptions);
            Assert.IsNull(request.Name);
            Assert.IsNull(request.Description);
        }

        /// <summary>
        /// Verifies every property, including the nested Bayesian options, survives a JSON round-trip.
        /// </summary>
        [TestMethod]
        public void Roundtrip_PreservesValues()
        {
            var request = new CreateRatingCurveAnalysisRequest
            {
                StageTimeSeriesId = Guid.NewGuid(),
                DischargeTimeSeriesId = Guid.NewGuid(),
                NumberOfSegments = 2,
                MinStage = 1.5,
                MaxStage = 12.25,
                StageBins = 150,
                BayesianOptions = new BayesianOptionsDto
                {
                    Iterations = 15000,
                    CredibleIntervalWidth = 0.95
                },
                Name = "Overbank rating",
                Description = "Two-segment fit with overbank control."
            };

            var copy = TestJson.Roundtrip(request);

            Assert.AreEqual(request.StageTimeSeriesId, copy.StageTimeSeriesId);
            Assert.AreEqual(request.DischargeTimeSeriesId, copy.DischargeTimeSeriesId);
            Assert.AreEqual(2, copy.NumberOfSegments);
            Assert.AreEqual(1.5, copy.MinStage);
            Assert.AreEqual(12.25, copy.MaxStage);
            Assert.AreEqual(150, copy.StageBins);
            Assert.IsNotNull(copy.BayesianOptions);
            Assert.AreEqual(15000, copy.BayesianOptions.Iterations);
            Assert.AreEqual(0.95, copy.BayesianOptions.CredibleIntervalWidth);
            Assert.AreEqual("Overbank rating", copy.Name);
            Assert.AreEqual("Two-segment fit with overbank control.", copy.Description);
        }

        /// <summary>
        /// Verifies the serialized JSON uses camelCase wire names and omits null optional fields.
        /// </summary>
        [TestMethod]
        public void Serialize_UsesCamelCaseWireNames()
        {
            string json = TestJson.Serialize(new CreateRatingCurveAnalysisRequest());
            StringAssert.Contains(json, "\"stageTimeSeriesId\"");
            StringAssert.Contains(json, "\"dischargeTimeSeriesId\"");
            StringAssert.Contains(json, "\"numberOfSegments\"");
            Assert.IsFalse(json.Contains("\"minStage\""), "Null minStage should be omitted.");
            Assert.IsFalse(json.Contains("\"bayesianOptions\""), "Null Bayesian options should be omitted.");
        }
    }
}
