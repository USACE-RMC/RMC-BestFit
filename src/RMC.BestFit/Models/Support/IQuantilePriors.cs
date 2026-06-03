using System.Collections.Generic;

namespace RMC.BestFit.Models
{
    /// <summary>
    /// Interface for quantile priors.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// </remarks>
    public interface IQuantilePriors
    {
        /// <summary>
        /// The list of quantile prior distributions. 
        /// </summary>
        List<QuantilePrior> QuantilePriors { get; set; }

        /// <summary>
        /// Determines whether to enable quantile priors. 
        /// </summary>
        bool EnableQuantilePriors { get; set; }

        /// <summary>
        /// Determines whether to use exact data only in the log-likelihood function. 
        /// </summary>
        bool UseSingleQuantile { get; set; }

        /// <summary>
        /// Process the quantile priors to be Gamma distributed differences.
        /// </summary>
        void ProcessQuantilePriors();

        /// <summary>
        /// Set the default quantile prior distributions.
        /// </summary>
        void SetDefaultQuantilePriors();

    }
}
