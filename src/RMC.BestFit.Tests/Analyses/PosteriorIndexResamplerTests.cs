using RMC.BestFit.Analyses;

namespace RMC.BestFit.Tests.Analyses;

/// <summary>
/// Fast programmatic tests for independent posterior-index resampling.
/// </summary>
[TestClass]
public class PosteriorIndexResamplerTests
{
    /// <summary>
    /// Verifies every source row has the shortest-chain length, contains no duplicate
    /// indices, and remains within its own retained-output range.
    /// </summary>
    [TestMethod]
    public void CreateRandomIndexes_UnequalCounts_ReturnsUniqueInRangeRows()
    {
        int[] counts = { 37, 11, 23 };

        int[][] indexes = PosteriorIndexResampler.CreateRandomIndexes(counts, 12345);

        Assert.AreEqual(counts.Length, indexes.Length);
        for (int source = 0; source < counts.Length; source++)
        {
            Assert.AreEqual(11, indexes[source].Length);
            Assert.AreEqual(11, indexes[source].Distinct().Count());
            Assert.IsTrue(indexes[source].All(index => index >= 0 && index < counts[source]));
        }
    }

    /// <summary>
    /// Verifies a longer source is sampled from its complete retained range rather than
    /// being restricted to the shortest-chain prefix.
    /// </summary>
    [TestMethod]
    public void CreateRandomIndexes_LongerSource_CanSelectBeyondShortestPrefix()
    {
        int[][] indexes = PosteriorIndexResampler.CreateRandomIndexes([10, 100], 12345);

        Assert.IsTrue(indexes[1].Any(index => index >= 10),
            "The longer source should contribute retained draws beyond B - 1.");
    }

    /// <summary>
    /// Verifies the same seed and source order reproduce the exact finite index matrix.
    /// </summary>
    [TestMethod]
    public void CreateRandomIndexes_SameSeed_ReproducesExactMatrix()
    {
        int[][] first = PosteriorIndexResampler.CreateRandomIndexes([25, 40, 30], 8675309);
        int[][] second = PosteriorIndexResampler.CreateRandomIndexes([25, 40, 30], 8675309);

        for (int source = 0; source < first.Length; source++)
            CollectionAssert.AreEqual(first[source], second[source]);
    }

    /// <summary>
    /// Verifies separately generated source rows do not reuse one shared permutation.
    /// </summary>
    [TestMethod]
    public void CreateRandomIndexes_EqualSourceCounts_UsesDifferentPermutations()
    {
        int[][] indexes = PosteriorIndexResampler.CreateRandomIndexes([100, 100, 100], 24680);

        Assert.IsFalse(indexes[0].SequenceEqual(indexes[1]));
        Assert.IsFalse(indexes[0].SequenceEqual(indexes[2]));
        Assert.IsFalse(indexes[1].SequenceEqual(indexes[2]));
    }

    /// <summary>
    /// Verifies fixed 5,000-draw source mappings have negligible pairwise rank correlation.
    /// </summary>
    [TestMethod]
    public void CreateRandomIndexes_FiveThousandDraws_HasLowPairwiseSpearmanCorrelation()
    {
        int[][] indexes = PosteriorIndexResampler.CreateRandomIndexes([5000, 5000, 5000], 13579);

        for (int first = 0; first < indexes.Length; first++)
        {
            for (int second = first + 1; second < indexes.Length; second++)
            {
                double correlation = PearsonCorrelation(indexes[first], indexes[second]);
                Assert.IsTrue(Math.Abs(correlation) < 0.05d,
                    $"Rows {first} and {second} had Spearman correlation {correlation:G17}.");
            }
        }
    }

    /// <summary>
    /// Verifies invalid source counts and negative seeds are rejected before sampling.
    /// </summary>
    [TestMethod]
    public void CreateRandomIndexes_InvalidInputs_ThrowNamedExceptions()
    {
        Assert.ThrowsException<ArgumentNullException>(() =>
            PosteriorIndexResampler.CreateRandomIndexes(null!, 1));
        Assert.ThrowsException<ArgumentException>(() =>
            PosteriorIndexResampler.CreateRandomIndexes([], 1));
        Assert.ThrowsException<ArgumentException>(() =>
            PosteriorIndexResampler.CreateRandomIndexes([10, 0], 1));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
            PosteriorIndexResampler.CreateRandomIndexes([10], -1));
    }

    /// <summary>
    /// Computes the Pearson correlation of two integer vectors.
    /// </summary>
    /// <param name="first">The first vector.</param>
    /// <param name="second">The second vector.</param>
    /// <returns>The sample correlation.</returns>
    /// <remarks>
    /// Each test row is a permutation of consecutive integers, so its values are also its
    /// ranks and this Pearson correlation is exactly the Spearman rank correlation.
    /// </remarks>
    private static double PearsonCorrelation(IReadOnlyList<int> first, IReadOnlyList<int> second)
    {
        Assert.AreEqual(first.Count, second.Count);
        double firstMean = first.Average();
        double secondMean = second.Average();
        double covariance = 0d;
        double firstVariance = 0d;
        double secondVariance = 0d;

        for (int index = 0; index < first.Count; index++)
        {
            double firstDeviation = first[index] - firstMean;
            double secondDeviation = second[index] - secondMean;
            covariance += firstDeviation * secondDeviation;
            firstVariance += firstDeviation * firstDeviation;
            secondVariance += secondDeviation * secondDeviation;
        }

        return covariance / Math.Sqrt(firstVariance * secondVariance);
    }
}
