using System.Text.Json;
using Numerics.Distributions;
using RMC.BestFit.Api.Mappers;
using RMC.BestFit.Api.Tests.Support;
using RMC.BestFit.Models;

namespace RMC.BestFit.Api.Tests.Mappers
{
    /// <summary>Tests model-owned display coordinates using restored results, without estimation.</summary>
    [TestClass]
    public class FrequencyPlotDataTests
    {
        /// <summary>Checks uncertain observation bounds are the model's display bounds.</summary>
        [TestMethod]
        public void UncertainObservations_IncludeModelBounds()
        {
            var frame = TestAnalyses.CreateDataFrame();
            var observation = new UncertainData(1950, new Normal(600, 60));
            frame.UncertainSeries.Add(observation);
            using var json = JsonDocument.Parse(TestJson.Serialize(InputDataMapper.ToUncertainObservations(frame)));
            var item = json.RootElement[0];
            Assert.AreEqual(observation.LowerValue, item.GetProperty("lowerBound").GetDouble());
            Assert.AreEqual(observation.UpperValue, item.GetProperty("upperBound").GetDouble());
        }

        /// <summary>Checks the global prior switch and preserves the model's physical-unit bounds.</summary>
        /// <param name="enabled">Whether priors are enabled.</param>
        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void Univariate_ExportsOnlyEnabledPriorDisplay(bool enabled)
        {
            var resource = TestAnalyses.CreateUnivariateResource();
            var analysis = resource.Univariate!;
            var prior = new QuantilePrior(0.01, new Normal(900, 100));
            analysis.UnivariateDistribution.EnableQuantilePriors = enabled;
            analysis.UnivariateDistribution.QuantilePriors.Clear();
            analysis.UnivariateDistribution.QuantilePriors.Add(prior);
            RestoreCurve(analysis);
            using var json = JsonDocument.Parse(TestJson.Serialize(ResultsMapper.ToFrequencyResults(resource)));
            var annotations = json.RootElement.GetProperty("quantileAnnotations");
            Assert.AreEqual(enabled ? 1 : 0, annotations.GetArrayLength());
            if (enabled) AssertAnnotation(annotations[0], prior.Alpha, prior.MeanValue, prior.LowerValue, prior.UpperValue);
        }

        /// <summary>Checks disabled B17C penalties are omitted and log-space penalties map to physical units.</summary>
        [TestMethod]
        public void Bulletin17C_ExportsEnabledPenaltyInPhysicalUnits()
        {
            var resource = TestAnalyses.CreateBulletin17CResource();
            var analysis = resource.Bulletin17C!;
            var penalty = new QuantilePenalty { Enabled = true, UseLog10 = true, AEP = 0.01, Mean = 3, MSE = 0.01 };
            analysis.Bulletin17CDistribution.QuantilePenalties.Clear();
            analysis.Bulletin17CDistribution.QuantilePenalties.Add(penalty);
            analysis.Bulletin17CDistribution.QuantilePenalties.Add(new QuantilePenalty { Enabled = false });
            RestoreCurve(analysis);
            using var json = JsonDocument.Parse(TestJson.Serialize(ResultsMapper.ToFrequencyResults(resource)));
            var annotations = json.RootElement.GetProperty("quantileAnnotations");
            Assert.AreEqual(1, annotations.GetArrayLength());
            AssertAnnotation(annotations[0], penalty.AEP, penalty.MeanValue, penalty.LowerValue, penalty.UpperValue);
        }

        /// <summary>Injects a synthetic curve into a private-set property to avoid running estimators.</summary>
        /// <param name="analysis">The univariate or B17C analysis.</param>
        private static void RestoreCurve(object analysis)
        {
            analysis.GetType().GetProperty("AnalysisResults")!.SetValue(analysis,
                new UncertaintyAnalysisResults { ModeCurve = new[] { 100d } });
        }

        /// <summary>Checks the annotation's wire coordinates against the existing model.</summary>
        /// <param name="item">The serialized annotation.</param>
        /// <param name="aep">The exceedance probability.</param>
        /// <param name="value">The mean display magnitude.</param>
        /// <param name="lower">The lower display bound.</param>
        /// <param name="upper">The upper display bound.</param>
        private static void AssertAnnotation(JsonElement item, double aep, double value, double lower, double upper)
        {
            Assert.AreEqual(aep, item.GetProperty("aep").GetDouble());
            Assert.AreEqual(value, item.GetProperty("value").GetDouble());
            Assert.AreEqual(lower, item.GetProperty("lowerBound").GetDouble());
            Assert.AreEqual(upper, item.GetProperty("upperBound").GetDouble());
        }
    }
}
