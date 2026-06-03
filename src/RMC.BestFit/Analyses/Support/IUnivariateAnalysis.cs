using Numerics.Distributions;
using RMC.BestFit.Estimation;

namespace RMC.BestFit.Analyses
{
    /// <summary>
    /// Interface for univariate distribution analyses.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// </remarks>
    public interface IUnivariateAnalysis: IBayesianAnalysis, IProbabilityOrdinates
    {
        /// <summary>
        /// Returns the distribution for a given output index.
        /// </summary>
        /// <param name="index">The output index.</param>
        UnivariateDistributionBase? GetDistribution(int index);


        /// <summary>
        /// Returns the point estimate distribution using the analysis's currently
        /// configured <see cref="BayesianAnalysis.PointEstimator"/>.
        /// </summary>
        UnivariateDistributionBase? GetPointEstimateDistribution();

        /// <summary>
        /// Returns the point estimate distribution using a caller-supplied estimator,
        /// without mutating the analysis's own <see cref="BayesianAnalysis.PointEstimator"/>.
        /// </summary>
        /// <param name="pointEstimator">
        /// The estimator (posterior mean or posterior mode) to use for selecting the
        /// parameter array.
        /// </param>
        /// <returns>
        /// The point-estimate distribution, or <c>null</c> when the analysis has not
        /// been estimated.
        /// </returns>
        /// <remarks>
        /// Used by parent analyses (e.g. <c>CompositeAnalysis</c>) that need to extract
        /// a posterior-mean or MAP distribution from a child without triggering the
        /// reprocess cascade that mutating the child's <c>PointEstimator</c> property
        /// would cause.
        /// </remarks>
        UnivariateDistributionBase? GetPointEstimateDistribution(
            BayesianAnalysis.PointEstimateType pointEstimator);
    }
}
