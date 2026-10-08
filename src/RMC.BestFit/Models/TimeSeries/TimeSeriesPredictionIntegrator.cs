using Numerics.Data;
using System;

namespace RMC.BestFit.Models
{
    /// <summary>
    /// Reconstructs conditional fitted and forecast levels from model-scale differences.
    /// </summary>
    /// <remarks>
    /// Fitted values inside the training window condition on the observed lower-order state at
    /// the preceding raw time step. The first forecast uses the final observed training state;
    /// only subsequent forecasts use states reconstructed by earlier forecast steps.
    /// </remarks>
    internal static class TimeSeriesPredictionIntegrator
    {
        /// <summary>
        /// Reconstructs transformed levels from highest-order fitted and forecast differences.
        /// </summary>
        /// <param name="differences">Predicted values on the <paramref name="order"/>-difference scale.</param>
        /// <param name="transformedLevels">Observed transformed levels containing the training window.</param>
        /// <param name="trainingSteps">The number of raw observations in the training window.</param>
        /// <param name="order">The positive differencing order.</param>
        /// <returns>
        /// The conditional fitted levels followed by recursively reconstructed forecast levels.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="differences"/> or <paramref name="transformedLevels"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="order"/> is not positive or <paramref name="trainingSteps"/> is outside
        /// the reconstructed raw-series length.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// The observed transformed series does not contain the required training states.
        /// </exception>
        internal static double[] ReconstructConditionalLevels(
            double[] differences,
            TimeSeries transformedLevels,
            int trainingSteps,
            int order)
        {
            ArgumentNullException.ThrowIfNull(differences);
            ArgumentNullException.ThrowIfNull(transformedLevels);

            if (order <= 0)
                throw new ArgumentOutOfRangeException(nameof(order), "The differencing order must be positive.");

            int totalSteps = differences.Length + order;
            if (trainingSteps < order || trainingSteps > totalSteps)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(trainingSteps),
                    $"Training steps must be between the differencing order {order} and reconstructed length {totalSteps}.");
            }
            if (transformedLevels.Count < trainingSteps)
            {
                throw new InvalidOperationException(
                    $"At least {trainingSteps} transformed observations are required to condition prediction reconstruction.");
            }

            double[][] observedStates = BuildObservedStates(transformedLevels, trainingSteps, order);
            var generatedStates = new double[order][];
            for (int level = 0; level < order; level++)
                generatedStates[level] = new double[totalSteps];

            var levels = new double[totalSteps];
            for (int rawIndex = 0; rawIndex < order; rawIndex++)
                levels[rawIndex] = observedStates[0][rawIndex];

            for (int rawIndex = order; rawIndex < totalSteps; rawIndex++)
            {
                double currentState = differences[rawIndex - order];
                int previousRawIndex = rawIndex - 1;

                for (int level = order - 1; level >= 0; level--)
                {
                    double previousState = previousRawIndex < trainingSteps
                        ? observedStates[level][previousRawIndex - level]
                        : generatedStates[level][previousRawIndex];
                    currentState += previousState;
                    generatedStates[level][rawIndex] = currentState;
                }

                levels[rawIndex] = currentState;
            }

            return levels;
        }

        /// <summary>
        /// Builds observed level and lower-order difference states for the training window.
        /// </summary>
        /// <param name="transformedLevels">The observed transformed response.</param>
        /// <param name="trainingSteps">The number of observed raw training steps.</param>
        /// <param name="order">The requested highest differencing order.</param>
        /// <returns>
        /// State arrays where position <c>i</c> at level <c>j</c> corresponds to raw index
        /// <c>i+j</c>.
        /// </returns>
        private static double[][] BuildObservedStates(TimeSeries transformedLevels, int trainingSteps, int order)
        {
            var states = new double[order][];
            states[0] = new double[trainingSteps];
            for (int index = 0; index < trainingSteps; index++)
                states[0][index] = transformedLevels[index].Value;

            for (int level = 1; level < order; level++)
            {
                double[] previous = states[level - 1];
                var current = new double[previous.Length - 1];
                for (int index = 0; index < current.Length; index++)
                    current[index] = previous[index + 1] - previous[index];
                states[level] = current;
            }

            return states;
        }
    }
}
