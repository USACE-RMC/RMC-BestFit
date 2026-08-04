using Numerics;
using Numerics.Sampling;
using Numerics.Utilities;

namespace RMC.BestFit.Analyses
{
    /// <summary>
    /// Creates reproducible, independently randomized posterior-index selections for
    /// analyses that combine separately fitted posterior sources.
    /// </summary>
    /// <remarks>
    /// Each source contributes the same number of indices, equal to the shortest retained
    /// output count. Sampling is without replacement within a source and uses the full
    /// retained range of that source. The source rows are generated sequentially from one
    /// <see cref="MersenneTwister"/>, so a fixed seed and source order reproduce the same
    /// finite pairing.
    /// </remarks>
    internal static class PosteriorIndexResampler
    {
        /// <summary>
        /// Creates one independently randomized index row for each posterior source.
        /// </summary>
        /// <param name="sourceOutputCounts">The positive retained-output count for every source.</param>
        /// <param name="seed">The nonnegative seed used to initialize the random-number generator.</param>
        /// <returns>
        /// A jagged array whose row count equals <paramref name="sourceOutputCounts"/>. Every
        /// row has length equal to the minimum source count and contains distinct indices in
        /// the half-open range for that source.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="sourceOutputCounts"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when no source counts are supplied or any source count is not positive.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="seed"/> is negative.
        /// </exception>
        internal static int[][] CreateRandomIndexes(IReadOnlyList<int> sourceOutputCounts, int seed)
        {
            ArgumentNullException.ThrowIfNull(sourceOutputCounts);
            if (sourceOutputCounts.Count == 0)
                throw new ArgumentException("At least one posterior source is required.", nameof(sourceOutputCounts));
            if (seed < 0)
                throw new ArgumentOutOfRangeException(nameof(seed), "The posterior-resampling seed must be nonnegative.");

            int outputCount = int.MaxValue;
            for (int sourceIndex = 0; sourceIndex < sourceOutputCounts.Count; sourceIndex++)
            {
                int sourceCount = sourceOutputCounts[sourceIndex];
                if (sourceCount <= 0)
                {
                    throw new ArgumentException(
                        $"Posterior source {sourceIndex + 1} must contain at least one retained output.",
                        nameof(sourceOutputCounts));
                }

                outputCount = Math.Min(outputCount, sourceCount);
            }

            var prng = new MersenneTwister(seed);
            var randomIndexes = new int[sourceOutputCounts.Count][];
            for (int sourceIndex = 0; sourceIndex < sourceOutputCounts.Count; sourceIndex++)
            {
                randomIndexes[sourceIndex] = prng.NextIntegers(
                    0,
                    sourceOutputCounts[sourceIndex],
                    outputCount,
                    false);
            }

            return randomIndexes;
        }
    }
}
