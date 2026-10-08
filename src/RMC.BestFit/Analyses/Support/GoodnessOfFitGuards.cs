using Numerics.Data.Statistics;
using Numerics.Distributions;
using System.Collections.Generic;

namespace RMC.BestFit.Analyses
{
    /// <summary>
    /// Shared guard that reports RMSE for a fitted univariate distribution only when the
    /// residual degrees of freedom are positive.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     <b>Authors:</b>
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    /// <see cref="GoodnessOfFit"/>'s distribution-based RMSE overload derives the residual
    /// degrees of freedom as the observation count minus the distribution's parameter count and
    /// throws <see cref="System.ArgumentOutOfRangeException"/> when that count is not positive.
    /// Several analyses (Univariate, Mixture, CompetingRisk, PointProcess, B17C, and
    /// distribution fitting) report RMSE alongside AIC/BIC after MCMC or MLE has already
    /// completed; a small sample whose parameter count reaches or exceeds the observation count
    /// would otherwise fail the whole results assembly on the RMSE call alone, discarding an
    /// estimation that succeeded. <see cref="RmseOrNaN"/> centralizes the guard so every caller
    /// degrades to <see cref="double.NaN"/> identically instead of duplicating the same
    /// ternary check.
    /// </para>
    /// </remarks>
    internal static class GoodnessOfFitGuards
    {
        /// <summary>
        /// Computes the RMSE of <paramref name="distribution"/> against <paramref name="values"/>
        /// at the given <paramref name="plottingPositions"/>, or <see cref="double.NaN"/> when
        /// the residual degrees of freedom are not positive.
        /// </summary>
        /// <param name="values">The observed values to measure against.</param>
        /// <param name="plottingPositions">The plotting positions associated with <paramref name="values"/>.</param>
        /// <param name="distribution">The fitted distribution being scored.</param>
        /// <returns>
        /// The RMSE computed by <see cref="GoodnessOfFit"/>, or <see cref="double.NaN"/> when
        /// the count of <paramref name="values"/> does not exceed <paramref name="distribution"/>'s
        /// <c>NumberOfParameters</c>.
        /// </returns>
        /// <exception cref="System.ArgumentOutOfRangeException">
        /// Thrown when <paramref name="values"/> and <paramref name="plottingPositions"/> have
        /// different counts, propagated from the underlying RMSE computation.
        /// </exception>
        /// <remarks>
        /// The guard condition uses <paramref name="distribution"/>'s own <c>NumberOfParameters</c>,
        /// the same count <see cref="GoodnessOfFit"/> derives internally for the identical
        /// distribution instance, so the guard and the downstream RMSE computation can never
        /// disagree about the residual degrees of freedom.
        /// </remarks>
        internal static double RmseOrNaN(IList<double> values, IList<double> plottingPositions, UnivariateDistributionBase distribution)
        {
            return values.Count > distribution.NumberOfParameters
                ? GoodnessOfFit.RMSE(values, plottingPositions, distribution)
                : double.NaN;
        }
    }
}
