using System;

namespace RMC.BestFit.Models
{
    /// <summary>
    /// The default training window of the AR, MA, ARIMA, and ARIMAX models.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A model with differencing order d and conditioning order K scores n = T − d − K steps of a
    /// T-step training window in its conditional likelihood: the first d raw steps are consumed by
    /// differencing and the next K steps only condition the recursion. With k estimated parameters
    /// (the scale included; a fitted transform exponent is preprocessing and not counted) the
    /// residual degrees of freedom are n − k. The default window leaves at least ten of them, which
    /// also guarantees at least ten fitted steps:
    /// </para>
    /// <para>
    /// T_min = d + K + k + 10, and the default window is max(T_min, ⌊0.8·N⌋) for a series of N
    /// observations, so longer series keep the 80% training split.
    /// </para>
    /// <para>
    /// The window is not capped at N: a series shorter than T_min is too short for the model under
    /// the default settings, and validation reports it through <see cref="ExceedsSeriesMessage"/>.
    /// Manual windows keep the models' own validation checks. The rule was approved by Haden Smith
    /// on 26 September 2026; it replaced max(30, k, ⌊0.8·N⌋), which made every series shorter than
    /// 30 steps invalid under the default settings.
    /// </para>
    /// </remarks>
    internal static class DefaultTrainingWindow
    {
        /// <summary>
        /// The residual degrees of freedom the default window leaves after every estimated parameter.
        /// </summary>
        internal const int MinimumResidualDegreesOfFreedom = 10;

        /// <summary>
        /// The share of the series the default window trains on when the series is long enough.
        /// </summary>
        internal const double TrainingFraction = 0.8;

        /// <summary>
        /// Gets the smallest default training window for a model structure.
        /// </summary>
        /// <param name="differencingOrder">The differencing order d (zero for AR and MA models).</param>
        /// <param name="conditioningOrder">The conditioning order K: the leading differenced steps
        /// the likelihood conditions on without scoring them.</param>
        /// <param name="parameterCount">The number of estimated parameters k, the scale included.</param>
        /// <returns>d + K + k + 10.</returns>
        internal static int MinimumSteps(int differencingOrder, int conditioningOrder, int parameterCount)
        {
            return differencingOrder + conditioningOrder + parameterCount + MinimumResidualDegreesOfFreedom;
        }

        /// <summary>
        /// Gets the default training window for a series and model structure.
        /// </summary>
        /// <param name="seriesLength">The number of observations N in the response series.</param>
        /// <param name="differencingOrder">The differencing order d (zero for AR and MA models).</param>
        /// <param name="conditioningOrder">The conditioning order K.</param>
        /// <param name="parameterCount">The number of estimated parameters k, the scale included.</param>
        /// <returns>max(d + K + k + 10, ⌊0.8·N⌋), which exceeds N when the series is too short for
        /// the model.</returns>
        internal static int Steps(int seriesLength, int differencingOrder, int conditioningOrder, int parameterCount)
        {
            return Math.Max(
                MinimumSteps(differencingOrder, conditioningOrder, parameterCount),
                (int)Math.Floor(TrainingFraction * seriesLength));
        }

        /// <summary>
        /// Builds the validation error for a training window longer than the series.
        /// </summary>
        /// <param name="useDefaultWindow">Whether the model uses the default training window.</param>
        /// <param name="seriesLength">The number of observations N in the response series.</param>
        /// <param name="differencingOrder">The differencing order d.</param>
        /// <param name="conditioningOrder">The conditioning order K.</param>
        /// <param name="parameterCount">The number of estimated parameters k.</param>
        /// <returns>
        /// For a manual window, the plain length error. For the default window, either the message
        /// that the series is too short for the model, naming the minimum window and the terms it is
        /// built from, or, when the series is long enough, the plain error with a hint to recompute a
        /// default window saved under an earlier rule.
        /// </returns>
        internal static string ExceedsSeriesMessage(bool useDefaultWindow, int seriesLength, int differencingOrder, int conditioningOrder, int parameterCount)
        {
            const string lengthError = "Error: Training time steps cannot exceed time series length.";
            if (!useDefaultWindow)
                return lengthError;

            int minimum = MinimumSteps(differencingOrder, conditioningOrder, parameterCount);
            if (minimum <= seriesLength)
                return lengthError + " Turn Use Default Training Steps off and on to recompute the default window.";

            return $"Error: The time series has {seriesLength} observations, but the default training window for this model " +
                $"needs at least {minimum} training steps: {MinimumResidualDegreesOfFreedom} more fitted steps than its " +
                $"{parameterCount} parameters, plus the conditioning order ({conditioningOrder}) and the differencing order " +
                $"({differencingOrder}). Use a model with fewer parameters or a longer series.";
        }
    }
}
