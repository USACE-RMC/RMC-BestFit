using Numerics.Distributions;
using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Tests.Support;
using RMC.BestFit.Estimation;

namespace RMC.BestFit.Api.Tests.DTOs
{
    /// <summary>
    /// Unit tests for <see cref="CreateUnivariateAnalysisRequest"/>: defaults, JSON round-trip,
    /// and camelCase wire names including string enum values.
    /// </summary>
    [TestClass]
    public class CreateUnivariateAnalysisRequestTests
    {
        /// <summary>
        /// Verifies a new request defaults to the Log-Pearson Type III distribution with no
        /// ordinates, Bayesian options, name, or description.
        /// </summary>
        [TestMethod]
        public void Defaults_AreExpected()
        {
            var request = new CreateUnivariateAnalysisRequest();
            Assert.AreEqual(Guid.Empty, request.InputDataId);
            Assert.AreEqual(UnivariateDistributionType.LogPearsonTypeIII, request.Distribution);
            Assert.IsNull(request.ProbabilityOrdinates);
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
            var request = new CreateUnivariateAnalysisRequest
            {
                InputDataId = Guid.NewGuid(),
                Distribution = UnivariateDistributionType.GeneralizedExtremeValue,
                ProbabilityOrdinates = new List<double> { 0.5, 0.01, 0.001 },
                BayesianOptions = new BayesianOptionsDto
                {
                    Iterations = 20000,
                    PointEstimator = BayesianAnalysis.PointEstimateType.PosteriorMean
                },
                Name = "GEV fit",
                Description = "Annual maxima frequency analysis."
            };

            var copy = TestJson.Roundtrip(request);

            Assert.AreEqual(request.InputDataId, copy.InputDataId);
            Assert.AreEqual(UnivariateDistributionType.GeneralizedExtremeValue, copy.Distribution);
            CollectionAssert.AreEqual(request.ProbabilityOrdinates, copy.ProbabilityOrdinates);
            Assert.IsNotNull(copy.BayesianOptions);
            Assert.AreEqual(20000, copy.BayesianOptions.Iterations);
            Assert.AreEqual(BayesianAnalysis.PointEstimateType.PosteriorMean, copy.BayesianOptions.PointEstimator);
            Assert.AreEqual("GEV fit", copy.Name);
            Assert.AreEqual("Annual maxima frequency analysis.", copy.Description);
        }

        /// <summary>
        /// Verifies the serialized JSON uses camelCase wire names, the camelCase distribution enum
        /// string, and omits null optional fields.
        /// </summary>
        [TestMethod]
        public void Serialize_UsesCamelCaseWireNames()
        {
            string json = TestJson.Serialize(new CreateUnivariateAnalysisRequest());
            StringAssert.Contains(json, "\"inputDataId\"");
            StringAssert.Contains(json, "\"distribution\"");
            StringAssert.Contains(json, "\"logPearsonTypeIII\"");
            Assert.IsFalse(json.Contains("\"name\""), "Null name should be omitted.");
            Assert.IsFalse(json.Contains("\"bayesianOptions\""), "Null Bayesian options should be omitted.");
        }
    }
}
