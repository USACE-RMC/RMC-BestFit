using Numerics.Data.Statistics;
using Numerics.Distributions;
using RMC.BestFit.Analyses;
using RMC.BestFit.Models;

namespace RMC.BestFit.Tests.Documentation.Examples
{
    /// <summary>
    /// Contains compile-checked examples for advanced univariate analyses.
    /// </summary>
    internal static class AdvancedUnivariateExamples
    {
        #region doc:fitting-analysis-workflow
        private static async Task<IReadOnlyList<FittedDistribution>> FitCandidateFamilies(
            global::RMC.BestFit.Models.DataFrame dataFrame)
        {
            var analysis = new FittingAnalysis(dataFrame);
            var validation = analysis.Validate();
            if (!validation.IsValid)
            {
                throw new InvalidOperationException(
                    string.Join(Environment.NewLine, validation.ValidationMessages));
            }

            await analysis.RunAsync();

            return analysis.FittedDistributions
                .Where(result => result.FitSucceeded)
                .OrderBy(result => result.AIC)
                .ToArray();
        }
        #endregion

        #region doc:point-process-workflow
        private static PointProcessAnalysis ConfigurePointProcess(
            global::RMC.BestFit.Models.DataFrame peaks)
        {
            var parent = new CompetingRisks(
                new UnivariateDistributionBase[]
                {
                    new GeneralizedExtremeValue()
                })
            {
                MinimumOfRandomVariables = false,
                Dependency = Probability.DependencyType.Independent
            };

            var model = new PointProcessModel(peaks, parent)
            {
                UseDefaults = false,
                Threshold = 1200.0,
                TotalYears = 25.0,
                IsSeasonal = false
            };

            return new PointProcessAnalysis(model);
        }
        #endregion

        #region doc:mixture-workflow
        private static MixtureAnalysis ConfigureLatentPopulationMixture(
            global::RMC.BestFit.Models.DataFrame annualPeaks)
        {
            var model = new MixtureModel(
                annualPeaks,
                new List<UnivariateDistributionType>
                {
                    UnivariateDistributionType.LogNormal,
                    UnivariateDistributionType.GeneralizedExtremeValue
                });

            model.EnableQuantilePriors = false;
            return new MixtureAnalysis(model);
        }
        #endregion

        #region doc:competing-risks-workflow
        private static CompetingRiskAnalysis ConfigureIndependentFloodSources(
            global::RMC.BestFit.Models.DataFrame annualMaxima)
        {
            var parent = new CompetingRisks(
                new UnivariateDistributionBase[]
                {
                    new GeneralizedExtremeValue(),
                    new LogNormal()
                })
            {
                MinimumOfRandomVariables = false,
                Dependency = Probability.DependencyType.Independent
            };

            var model = new CompetingRisksModel(annualMaxima, parent);
            return new CompetingRiskAnalysis(model);
        }
        #endregion

        #region doc:composite-model-average
        private static CompositeAnalysis ConfigureModelAverage(
            UnivariateAnalysis gevAnalysis,
            UnivariateAnalysis logPearsonAnalysis)
        {
            var composite = new CompositeAnalysis(
                new[]
                {
                    new WeightedUnivariateAnalysis(gevAnalysis, 0.5),
                    new WeightedUnivariateAnalysis(logPearsonAnalysis, 0.5)
                })
            {
                CompositeDistributionType = CompositeType.ModelAverage,
                ModelAverageMethod = AverageMethod.WAIC
            };

            composite.EstimateModelWeights();
            return composite;
        }
        #endregion
        #region doc:bulletin17c-workflow
        private static Bulletin17CAnalysis ConfigureBulletin17C(
            global::RMC.BestFit.Models.DataFrame annualPeakRecord)
        {
            var model = new Bulletin17CDistribution(
                annualPeakRecord,
                UnivariateDistributionType.LogPearsonTypeIII);

            var analysis = new Bulletin17CAnalysis(model)
            {
                UncertaintyMethod = UncertaintyMethod.LinkedMultivariateNormal
            };

            analysis.BayesianAnalysis.PRNGSeed = 12345;
            analysis.BayesianAnalysis.OutputLength = 10_000;
            analysis.BayesianAnalysis.CredibleIntervalWidth = 0.90;
            return analysis;
        }
        #endregion
    }
}
