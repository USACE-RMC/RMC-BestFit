using Numerics.Data.Statistics;

namespace RMC.BestFit.Verification.DistributionFitting;

/// <summary>
/// Verifies parameter-adjusted RMSE against its analytical definition.
/// </summary>
[TestClass]
public sealed class GoodnessOfFitRmseVerificationTests
{
    /// <summary>
    /// Verifies that every paired residual enters the numerator and that paired permutation does not change RMSE.
    /// </summary>
    [TestMethod]
    public void ParameterAdjustedRmse_UsesAllResidualsAndIsPermutationInvariant()
    {
        double[] observed = [0d, 0d, 0d, 0d];
        double[] modeled = [1d, 2d, 3d, 4d];
        double[] permutedModeled = [4d, 1d, 2d, 3d];
        const int parameterCount = 1;
        double expected = Math.Sqrt((1d + 4d + 9d + 16d) / (observed.Length - parameterCount));

        double direct = GoodnessOfFit.RMSE(observed, modeled, parameterCount);
        double permuted = GoodnessOfFit.RMSE(observed, permutedModeled, parameterCount);

        Assert.AreEqual(expected, direct, 1E-12d,
            "Parameter adjustment changes only the denominator; every residual must enter the numerator.");
        Assert.AreEqual(expected, permuted, 1E-12d,
            "RMSE must be invariant to a common permutation of observed-modeled pairs.");
    }
}
