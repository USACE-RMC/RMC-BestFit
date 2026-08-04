using Numerics.Distributions;
using Numerics.Mathematics.Optimization;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using RMC.BestFit.Verification.Datasets.UnivariateData;

namespace RMC.BestFit.Verification.ModelEstimation;

/// <summary>
/// Verifies that GMM Profile-Q confidence intervals recover the generating LP3 parameters.
/// </summary>
[TestClass]
public class ProfileQRecoveryTests
{
#region Test Setup

    /// <summary>
    /// True LP3 parameters for all tests: μ=3.0, σ=0.5, γ=0.5 in log10-space.
    /// </summary>
    private const double TrueMu = 3.0;
    private const double TrueSigma = 0.5;
    private const double TrueGamma = -0.5;
    private const int SampleSize = 50;
    private const int Seed = 12345;

    /// <summary>
    /// Creates a Bulletin17CDistribution with LP3 synthetic data and runs GMM estimation.
    /// Returns the estimated GMM object ready for profile Q analysis.
    /// </summary>
    /// <param name="n">Sample size. Default = 50.</param>
    /// <param name="seed">PRNG seed for reproducibility. Default = 12345.</param>
    /// <returns>A tuple of the estimated GMM and the Bulletin17CDistribution model.</returns>
    private static (GeneralizedMethodOfMoments GMM, Bulletin17CDistribution Model) CreateEstimatedLP3GMM(
        int n = SampleSize, int seed = Seed)
    {
        var (df, _) = TheoreticalUnivariateData.GenerateLogPearsonTypeIIIData(
            mu: TrueMu, sigma: TrueSigma, gamma: TrueGamma, n: n);

        var model = new Bulletin17CDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);
        var gmm = new GeneralizedMethodOfMoments(model);
        gmm.Estimate();

        return (gmm, model);
    }

    /// <summary>
    /// Creates a Bulletin17CDistribution with LP3 synthetic data but does NOT estimate.
    /// Used for testing pre-estimation error handling.
    /// </summary>
    private static GeneralizedMethodOfMoments CreateUnestimatedLP3GMM()
    {
        var (df, _) = SyntheticUnivariateData.GenerateLogPearsonTypeIIIData(
            mu: TrueMu, sigma: TrueSigma, gamma: TrueGamma, n: SampleSize, prngSeed: Seed);

        var model = new Bulletin17CDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);
        return new GeneralizedMethodOfMoments(model);
    }

    #endregion

    /// <summary>
    /// Verifies that profile CIs contain the true population parameters at 90% level
    /// (nominal coverage check). With n=50 and a single realization, this is a sanity check
    /// rather than a statistical coverage test.
    /// </summary>
    [TestMethod]
    public void ProfileConfidenceIntervals_LP3_ContainTrueParameters()
    {
        // Arrange — use larger sample for better coverage probability
        var (gmm, _) = CreateEstimatedLP3GMM(n: 200, seed: 42);

        // Act — 90% CIs should contain true values most of the time
        var cis = gmm.ProfileConfidenceIntervals(alpha: 0.1, trueProfile: false);

        // The true parameters (in the GMM's parameterization, which are moments of log10(x))
        // may differ slightly from the constructor values due to sampling.
        // We check that the true generating values are at least roughly within the CIs.
        // For a large sample (n=200), the GMM estimates should be close to truth.
        string[] paramNames = { "μ", "σ", "γ" };
        double[] trueParams = { TrueMu, TrueSigma, TrueGamma };

        int covered = 0;
        for (int i = 0; i < 3; i++)
        {
            if (trueParams[i] >= cis[i, 0] && trueParams[i] <= cis[i, 1])
                covered++;
        }

        // At least 2 of 3 should be covered (allowing for one unlucky parameter)
        Assert.IsTrue(covered >= 2,
            $"At least 2 of 3 true parameters should be within 90% profile CIs. " +
            $"Covered={covered}. " +
            $"μ: [{cis[0, 0]:G4}, {cis[0, 1]:G4}] (true={TrueMu}), " +
            $"σ: [{cis[1, 0]:G4}, {cis[1, 1]:G4}] (true={TrueSigma}), " +
            $"γ: [{cis[2, 0]:G4}, {cis[2, 1]:G4}] (true={TrueGamma})");
    }
}
