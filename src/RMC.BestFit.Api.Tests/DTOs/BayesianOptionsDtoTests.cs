using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Tests.Support;
using RMC.BestFit.Estimation;

namespace RMC.BestFit.Api.Tests.DTOs
{
    /// <summary>
    /// Unit tests for <see cref="BayesianOptionsDto"/>: all-null defaults (so the model library
    /// keeps its own defaults), JSON round-trip, and camelCase wire names including string enum
    /// values.
    /// </summary>
    [TestClass]
    public class BayesianOptionsDtoTests
    {
        /// <summary>
        /// Verifies every option defaults to null so omitted fields keep the model defaults.
        /// </summary>
        [TestMethod]
        public void Defaults_AreExpected()
        {
            var options = new BayesianOptionsDto();
            Assert.IsNull(options.Sampler);
            Assert.IsNull(options.Iterations);
            Assert.IsNull(options.WarmupIterations);
            Assert.IsNull(options.ThinningInterval);
            Assert.IsNull(options.NumberOfChains);
            Assert.IsNull(options.PrngSeed);
            Assert.IsNull(options.CredibleIntervalWidth);
            Assert.IsNull(options.OutputLength);
            Assert.IsNull(options.PointEstimator);
        }

        /// <summary>
        /// Verifies every property, including the two nullable enum options, survives a JSON round-trip.
        /// </summary>
        [TestMethod]
        public void Roundtrip_PreservesValues()
        {
            var options = new BayesianOptionsDto
            {
                Sampler = BayesianAnalysis.SamplerType.ARWMH,
                Iterations = 20000,
                WarmupIterations = 10000,
                ThinningInterval = 10,
                NumberOfChains = 4,
                PrngSeed = 12345,
                CredibleIntervalWidth = 0.95,
                OutputLength = 5000,
                PointEstimator = BayesianAnalysis.PointEstimateType.PosteriorMean
            };

            var copy = TestJson.Roundtrip(options);

            Assert.AreEqual(BayesianAnalysis.SamplerType.ARWMH, copy.Sampler);
            Assert.AreEqual(20000, copy.Iterations);
            Assert.AreEqual(10000, copy.WarmupIterations);
            Assert.AreEqual(10, copy.ThinningInterval);
            Assert.AreEqual(4, copy.NumberOfChains);
            Assert.AreEqual(12345, copy.PrngSeed);
            Assert.AreEqual(0.95, copy.CredibleIntervalWidth);
            Assert.AreEqual(5000, copy.OutputLength);
            Assert.AreEqual(BayesianAnalysis.PointEstimateType.PosteriorMean, copy.PointEstimator);
        }

        /// <summary>
        /// Verifies the serialized JSON uses camelCase wire names, camelCase enum value strings,
        /// and omits null options.
        /// </summary>
        [TestMethod]
        public void Serialize_UsesCamelCaseWireNames()
        {
            string json = TestJson.Serialize(new BayesianOptionsDto
            {
                Sampler = BayesianAnalysis.SamplerType.DEMCzs,
                PointEstimator = BayesianAnalysis.PointEstimateType.PosteriorMean,
                PrngSeed = 42
            });
            StringAssert.Contains(json, "\"sampler\"");
            StringAssert.Contains(json, "\"demCzs\"");
            StringAssert.Contains(json, "\"pointEstimator\"");
            StringAssert.Contains(json, "\"posteriorMean\"");
            StringAssert.Contains(json, "\"prngSeed\"");
            Assert.IsFalse(json.Contains("\"iterations\""), "Null iterations should be omitted.");

            string defaultJson = TestJson.Serialize(new BayesianOptionsDto());
            Assert.AreEqual("{}", defaultJson, "An all-default options object should serialize empty.");
        }
    }
}
