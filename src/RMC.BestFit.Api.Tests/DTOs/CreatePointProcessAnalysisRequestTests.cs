using Numerics.Data;
using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.DTOs
{
    /// <summary>
    /// Unit tests for <see cref="CreatePointProcessAnalysisRequest"/>: defaults, JSON round-trip,
    /// and camelCase wire names.
    /// </summary>
    [TestClass]
    public class CreatePointProcessAnalysisRequestTests
    {
        /// <summary>
        /// Verifies the defaults: non-seasonal with every override null (model defaults apply).
        /// </summary>
        [TestMethod]
        public void Defaults_AreExpected()
        {
            var request = new CreatePointProcessAnalysisRequest();
            Assert.AreEqual(Guid.Empty, request.InputDataId);
            Assert.IsFalse(request.IsSeasonal);
            Assert.IsNull(request.TimeBlock);
            Assert.IsNull(request.StartMonth);
            Assert.IsNull(request.Threshold);
            Assert.IsNull(request.TotalYears);
            Assert.IsNull(request.ProbabilityOrdinates);
            Assert.IsNull(request.BayesianOptions);
            Assert.IsNull(request.ParameterPriors);
            Assert.IsNull(request.QuantilePriors);
            Assert.IsNull(request.UseSingleQuantile);
        }

        /// <summary>
        /// Verifies every property survives a JSON round-trip.
        /// </summary>
        [TestMethod]
        public void Roundtrip_PreservesValues()
        {
            var request = new CreatePointProcessAnalysisRequest
            {
                InputDataId = Guid.NewGuid(),
                IsSeasonal = true,
                TimeBlock = TimeBlockWindow.WaterYear,
                StartMonth = 10,
                Threshold = 425d,
                TotalYears = 63.5,
                ProbabilityOrdinates = new List<double> { 0.1, 0.01 },
                Name = "pot analysis"
            };

            var copy = TestJson.Roundtrip(request);

            Assert.AreEqual(request.InputDataId, copy.InputDataId);
            Assert.IsTrue(copy.IsSeasonal);
            Assert.AreEqual(TimeBlockWindow.WaterYear, copy.TimeBlock);
            Assert.AreEqual(10, copy.StartMonth);
            Assert.AreEqual(425d, copy.Threshold);
            Assert.AreEqual(63.5, copy.TotalYears);
            Assert.AreEqual("pot analysis", copy.Name);
        }

        /// <summary>
        /// Verifies the serialized JSON uses camelCase wire names and enum strings.
        /// </summary>
        [TestMethod]
        public void Serialize_UsesCamelCaseWireNames()
        {
            string json = TestJson.Serialize(new CreatePointProcessAnalysisRequest
            {
                IsSeasonal = true,
                TimeBlock = TimeBlockWindow.WaterYear
            });
            StringAssert.Contains(json, "\"inputDataId\"");
            StringAssert.Contains(json, "\"isSeasonal\"");
            StringAssert.Contains(json, "\"timeBlock\"");
            StringAssert.Contains(json, "\"waterYear\"");
            Assert.IsFalse(json.Contains("\"threshold\""), "Null threshold should be omitted.");
        }
    }
}
