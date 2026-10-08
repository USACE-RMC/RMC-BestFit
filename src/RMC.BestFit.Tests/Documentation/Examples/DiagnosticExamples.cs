using Numerics.Sampling.MCMC;
using RMC.BestFit.Diagnostics;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;

namespace RMC.BestFit.Tests.Documentation.Examples
{
    /// <summary>
    /// Compile-checked examples used by model-comparison and predictive-check chapters.
    /// </summary>
    internal static class DiagnosticExamples
    {
        #region doc:model-comparison-workflow
        private static (
            double Dic,
            double Waic,
            double EffectiveParametersWaic,
            double Looic,
            double LooicStandardError) ReadBayesianCriteria(
                BayesianAnalysis analysis)
        {
            if (!analysis.IsEstimated || analysis.Results is null)
            {
                throw new InvalidOperationException(
                    "Run the Bayesian analysis before reading model criteria.");
            }

            return (
                analysis.DIC,
                analysis.WAIC,
                analysis.WAIC_pD,
                analysis.LOOIC,
                analysis.LOOIC_SE);
        }
        #endregion

        #region doc:predictive-checks-workflow
        private static (
            PredictiveCheckResults Posterior,
            PredictiveSummary Prior) RunPredictiveChecks(
                IModel model,
                MCMCResults results,
                double[] observedData)
        {
            var posteriorCheck = new PosteriorPredictiveCheck(
                model,
                results,
                observedData)
            {
                Seed = 12345
            };

            var priorCheck = new PriorPredictiveCheck(model)
            {
                Seed = 12345,
                NumberOfDraws = 1_000
            };

            return (
                posteriorCheck.ComputeCommonPValues(numberOfReplicates: 1_000),
                priorCheck.ComputeSummary(sampleSize: observedData.Length));
        }
        #endregion
    }
}
