using RMC.BestFit.Analyses;
using RMC.BestFit.Models;

namespace RMC.BestFit.Tests.Documentation.Examples
{
    /// <summary>
    /// Contains compile-checked analysis-lifecycle examples.
    /// </summary>
    internal static class AnalysisExamples
    {
        #region doc:analysis-validated-run
        private static async Task<UnivariateAnalysis> RunValidatedAnalysis(
            UnivariateDistribution model)
        {
            var analysis = new UnivariateAnalysis(model);
            var validation = analysis.Validate();
            if (!validation.IsValid)
            {
                throw new InvalidOperationException(
                    string.Join(Environment.NewLine, validation.ValidationMessages));
            }

            await analysis.RunAsync();
            if (!analysis.IsEstimated || analysis.AnalysisResults is null)
            {
                throw new InvalidOperationException(
                    "The analysis did not produce uncertainty results.");
            }

            return analysis;
        }
        #endregion
    }
}
