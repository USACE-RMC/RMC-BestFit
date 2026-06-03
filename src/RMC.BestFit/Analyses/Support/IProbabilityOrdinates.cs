using Numerics.Data;

namespace RMC.BestFit.Analyses
{

    /// <summary>
    /// Interface for probability ordinates used in distribution analysis.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// </remarks>
    public interface IProbabilityOrdinates
    {
        /// <summary>
        /// Gets the exceedance probability values used for plotting the distribution.
        /// </summary>
        ProbabilityOrdinates ProbabilityOrdinates { get; }

    }
}
