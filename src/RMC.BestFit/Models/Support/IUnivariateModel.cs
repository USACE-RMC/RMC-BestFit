using Numerics.Distributions;
using System.Collections.Generic;

namespace RMC.BestFit.Models
{
    /// <summary>
    /// The minimal contract a univariate model must satisfy to serve as a marginal in a
    /// <see cref="BivariateDistribution"/> (or any future multivariate composite model).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Implemented by <see cref="UnivariateDistribution"/>, <see cref="Bulletin17CDistribution"/>,
    /// <see cref="PointProcessModel"/>, and <see cref="MixtureModel"/>. Intentionally minimal so
    /// that classes with different base hierarchies (e.g., <see cref="Bulletin17CDistribution"/>
    /// implements <see cref="IGMMModel"/> directly, not <c>ModelBase</c>) can satisfy it without
    /// a base-class refactor.
    /// </para>
    /// <para>
    /// <see cref="CompositeAnalysis"/> / CompositeModel is intentionally excluded: it has no owned
    /// <see cref="DataFrame"/> (its posterior is averaged across component analyses), so it cannot
    /// contribute paired observations to a bivariate copula fit.
    /// </para>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// </remarks>
    public interface IUnivariateModel
    {
        /// <summary>
        /// The observed data backing the marginal.
        /// </summary>
        DataFrame DataFrame { get; }

        /// <summary>
        /// The fitted Numerics univariate distribution, or <c>null</c> if the model is
        /// not yet estimated.
        /// </summary>
        /// <remarks>
        /// Any Numerics type deriving from <see cref="UnivariateDistributionBase"/> is acceptable,
        /// including <c>Mixture</c> and <c>CompetingRisks</c>. This matches what
        /// <see cref="Numerics.Distributions.Copulas.BivariateCopula.MarginalDistributionX"/>
        /// expects.
        /// </remarks>
        UnivariateDistributionBase? Distribution { get; }

        /// <summary>
        /// Whether the marginal carries a nonstationary trend on one or more parameters.
        /// </summary>
        /// <remarks>
        /// Bivariate copula fitting assumes stationary marginals so that a single
        /// fitted distribution applies to every paired observation. Models without a
        /// trend concept (Bulletin17C, Mixture) return <c>false</c>.
        /// </remarks>
        bool IsNonstationary { get; }

        /// <summary>
        /// Standard model validation.
        /// </summary>
        /// <returns>A tuple of <c>IsValid</c> and a list of human-readable messages.</returns>
        (bool IsValid, List<string> ValidationMessages) Validate();
    }
}
