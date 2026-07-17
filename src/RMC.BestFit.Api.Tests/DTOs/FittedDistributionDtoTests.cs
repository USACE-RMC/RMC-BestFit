using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.DTOs
{
    /// <summary>
    /// Unit tests for <see cref="FittedDistributionDto"/>: defaults, JSON round-trip, and
    /// camelCase wire names.
    /// </summary>
    [TestClass]
    public class FittedDistributionDtoTests
    {
        /// <summary>
        /// Verifies a new fitted distribution defaults to no type, display name, or point
        /// estimator and an empty parameter list.
        /// </summary>
        [TestMethod]
        public void Defaults_AreExpected()
        {
            var fitted = new FittedDistributionDto();
            Assert.IsNull(fitted.Type);
            Assert.IsNull(fitted.DisplayName);
            Assert.IsNull(fitted.PointEstimator);
            Assert.IsNotNull(fitted.Parameters);
            Assert.AreEqual(0, fitted.Parameters.Count);
        }

        /// <summary>
        /// Verifies every property, including the nested parameter values, survives a JSON round-trip.
        /// </summary>
        [TestMethod]
        public void Roundtrip_PreservesValues()
        {
            var fitted = new FittedDistributionDto
            {
                Type = "logPearsonTypeIII",
                DisplayName = "Log-Pearson Type III",
                PointEstimator = "posteriorMode",
                Parameters = new List<ParameterValueDto>
                {
                    new ParameterValueDto { Name = "Mu", Value = 3.52 },
                    new ParameterValueDto { Name = "Sigma", Value = 0.24 }
                }
            };

            var copy = TestJson.Roundtrip(fitted);

            Assert.AreEqual("logPearsonTypeIII", copy.Type);
            Assert.AreEqual("Log-Pearson Type III", copy.DisplayName);
            Assert.AreEqual("posteriorMode", copy.PointEstimator);
            Assert.AreEqual(2, copy.Parameters.Count);
            Assert.AreEqual("Mu", copy.Parameters[0].Name);
            Assert.AreEqual(3.52, copy.Parameters[0].Value);
            Assert.AreEqual("Sigma", copy.Parameters[1].Name);
            Assert.AreEqual(0.24, copy.Parameters[1].Value);
        }

        /// <summary>
        /// Verifies the serialized JSON uses camelCase wire names and omits null optional fields.
        /// </summary>
        [TestMethod]
        public void Serialize_UsesCamelCaseWireNames()
        {
            string json = TestJson.Serialize(new FittedDistributionDto
            {
                Type = "gumbel",
                PointEstimator = "posteriorMean"
            });
            StringAssert.Contains(json, "\"type\"");
            StringAssert.Contains(json, "\"pointEstimator\"");
            StringAssert.Contains(json, "\"parameters\"");
            Assert.IsFalse(json.Contains("\"displayName\""), "Null displayName should be omitted.");
        }
    }
}
