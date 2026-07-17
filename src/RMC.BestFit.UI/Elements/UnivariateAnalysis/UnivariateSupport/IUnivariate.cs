using Numerics.Distributions;
using RMC.BestFit.Models;
using ModelAnalyses = RMC.BestFit.Analyses;

namespace RMC.BestFit.UI
{
    /// <summary>
    /// Interface for univariate distributions.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// </remarks>
    public interface IUnivariate : IAnalysisElement, ModelAnalyses.IProbabilityOrdinates
    {

        /// <summary>
        /// The input-data element backing this analysis (unit label, data frame, etc.).
        /// </summary>
        InputData InputData { get; }

        /// <summary>
        /// Returns the distribution for a given output index from the analysis results
        /// (e.g., MAP, posterior-mean, or quantile-of-uncertainty distribution).
        /// </summary>
        /// <param name="index">The output index into <see cref="ModelAnalyses.IProbabilityOrdinates.ProbabilityOrdinates"/>.</param>
        /// <returns>The univariate distribution at the specified output index, or null if not estimated.</returns>
        UnivariateDistributionBase GetDistribution(int index);


        /// <summary>
        /// Returns the point estimate distribution (e.g., the MAP or posterior-mean distribution
        /// produced by the configured <see cref="RMC.BestFit.Estimation.BayesianAnalysis.PointEstimateType"/>).
        /// </summary>
        /// <returns>The point estimate distribution, or null if the analysis has not been estimated.</returns>
        UnivariateDistributionBase GetPointEstimateDistribution();

        /// <summary>
        /// Returns the underlying <see cref="IUnivariateModel"/> driving this analysis so it
        /// can be assigned as a marginal on a <see cref="BivariateDistribution"/>, or
        /// <c>null</c> if the analysis cannot serve as a marginal (e.g., CompositeAnalysis,
        /// which has no owned InputData).
        /// </summary>
        /// <returns>The marginal model, or null if this analysis cannot serve as a marginal.</returns>
        IUnivariateModel GetMarginalModel();

    }
}
