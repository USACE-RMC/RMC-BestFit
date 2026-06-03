using Numerics.Distributions;
using RMC.BestFit.Estimation;

namespace RMC.BestFit.Analyses
{
    /// <summary>
    /// Interface for Bayesian analyses.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// </remarks>
    public interface IBayesianAnalysis: IAnalysis
    {
        /// <summary>
        /// The Bayesian Analysis object.
        /// </summary>
        BayesianAnalysis BayesianAnalysis { get; }

        /// <summary>
        /// The uncertainty analysis results.
        /// </summary>
        UncertaintyAnalysisResults? AnalysisResults { get; }
    }
}
