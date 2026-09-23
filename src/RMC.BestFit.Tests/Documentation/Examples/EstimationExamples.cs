using RMC.BestFit.Diagnostics;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;

namespace RMC.BestFit.Tests.Documentation.Examples
{
    /// <summary>
    /// Compile-checked examples used by the estimation and diagnostics chapters.
    /// </summary>
    internal static class EstimationExamples
    {
        #region doc:maximum-likelihood-workflow
        private static double[] EstimateByMaximumLikelihood(IModel model)
        {
            var estimation = new MaximumLikelihood(
                model,
                OptimizationMethod.DifferentialEvolution)
            {
                ComputeHessian = true,
                ReportFailure = false
            };

            if (!estimation.Estimate())
            {
                throw new InvalidOperationException(
                    $"MLE failed with optimizer status {estimation.Status}.");
            }

            return estimation.BestParameterSet.Values.ToArray();
        }
        #endregion

        #region doc:maximum-a-posteriori-workflow
        private static double[] EstimateByMaximumAPosteriori(IModel model)
        {
            var estimation = new MaximumAPosteriori(
                model,
                OptimizationMethod.DifferentialEvolution)
            {
                ComputeHessian = true,
                ReportFailure = false
            };

            if (!estimation.Estimate())
            {
                throw new InvalidOperationException(
                    $"MAP failed with optimizer status {estimation.Status}.");
            }

            return estimation.BestParameterSet.Values.ToArray();
        }
        #endregion

        #region doc:gmm-workflow
        private static GeneralizedMethodOfMoments ConfigureIterativeGmm(
            IGMMModel model)
        {
            return new GeneralizedMethodOfMoments(
                model,
                OptimizationMethod.BFGS)
            {
                EstimationStrategy =
                    GeneralizedMethodOfMoments.GMMEstimationStrategy.Iterative,
                UseFallbackOptimizer = true,
                MaxGMMIterations = 100,
                MaxFunctionEvaluations = 2_000,
                AbsoluteTolerance = 1E-8,
                RelativeTolerance = 1E-8
            };
        }
        #endregion

        #region doc:bayesian-mcmc-workflow
        private static BayesianAnalysis ConfigureBayesianMcmc(IModel model)
        {
            int parameterCount = Math.Max(1, model.Parameters.Count);
            var analysis = new BayesianAnalysis(
                model,
                BayesianAnalysis.SamplerType.DEMCzs)
            {
                UseSimulationDefaults = false,
                UseAdvancedSimulationDefaults = false,
                NumberOfChains = 4,
                InitialIterations = Math.Max(4, 100 * parameterCount),
                WarmupIterations = 1_500,
                Iterations = 3_000,
                ThinningInterval = 20,
                OutputLength = 10_000,
                CredibleIntervalWidth = 0.90,
                PRNGSeed = 12345,
                Jump = 2.38 / Math.Sqrt(2.0 * parameterCount),
                JumpThreshold = 0.10,
                SnookerThreshold = 0.10,
                Noise = 1E-12
            };

            analysis.SetUpSampler();
            return analysis;
        }
        #endregion

        #region doc:bayesian-diagnostics-workflow
        private static (
            InfluenceDiagnostics Influence,
            PriorInfluenceDiagnostics PriorInfluence,
            LeverageDiagnostics Leverage) ComputeBayesianDiagnostics(
                BayesianAnalysis analysis)
        {
            if (!analysis.IsEstimated || analysis.Results is null)
            {
                throw new InvalidOperationException(
                    "Run the Bayesian analysis before computing diagnostics.");
            }

            return (
                analysis.ComputeInfluenceDiagnostics(),
                analysis.ComputePriorInfluenceDiagnostics(thinEvery: 10),
                analysis.ComputeLeverageDiagnostics());
        }
        #endregion
    }
}
