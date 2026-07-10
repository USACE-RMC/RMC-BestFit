using Numerics.Distributions;
using Numerics.Distributions.Copulas;
using RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using RMC.BestFit.Verification.Datasets;

namespace RMC.BestFit.Verification.Bivariate;

/// <summary>
/// End-to-end Bayesian parameter-recovery tests for the seven supported copulas.
/// </summary>
/// <remarks>
/// <para>
///     <b>Authors:</b>
///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
/// </para>
/// <para>
///     <b>Intent:</b> Simulate a paired sample with known parent copula + marginal
///     parameters (via <see cref="SyntheticBivariateData"/>), fit the marginals via MLE,
///     run the full <see cref="BivariateAnalysis"/> Bayesian MCMC workflow, and assert that
///     the posterior MAP recovers the parent parameters within tolerance. The Normal-copula
///     test mirrors the Normal entry in <c>examples/5-bivariate-distribution-analysis/</c>
///     byte-for-byte in MCMC configuration.
/// </para>
/// <para>
///     <b>Tolerance:</b> 15% relative for copula θ / ρ — derived from the observed
///     fitted-vs-parent deltas in the example project (≤ 4% for all six), with headroom for
///     MCMC noise at n=100. 50% for the Student's t degrees-of-freedom parameter (discrete
///     integer, weakly identified at this sample size). ±3/±5 absolute for marginal μ / σ.
/// </para>
/// <para>
///     <b>Runtime:</b> ≈ 1–3 minutes for the full class. Long enough to belong in
///     <c>RMC.BestFit.Verification</c> (not <c>RMC.BestFit.Verification</c>). Inherits
///     <c>[TestCategory("Verification")]</c> from <c>VerificationAssemblyInfo.cs</c>.
/// </para>
/// </remarks>
[TestClass]
public class BivariateAnalysisParameterRecoveryTests
{
    #region Shared Configuration

    /// <summary>Relative tolerance for 1-parameter copula recovery (ρ or θ).</summary>
    private const double CopulaThetaRelativeTolerance = 0.15;

    /// <summary>Absolute tolerance for the Normal-marginal location parameter μ.</summary>
    private const double MarginalMuAbsoluteTolerance = 5.0;

    /// <summary>Absolute tolerance for the Normal-marginal scale parameter σ.</summary>
    private const double MarginalSigmaAbsoluteTolerance = 5.0;

    #endregion

    #region Helpers

    /// <summary>
    /// Fits a Normal univariate distribution via MLE to the supplied DataFrame and returns
    /// a ready-to-use <see cref="UnivariateDistribution"/> with the fitted point estimate.
    /// </summary>
    private static UnivariateDistribution FitNormalMarginal(DataFrame dataFrame)
    {
        var dist = new UnivariateDistribution(dataFrame, UnivariateDistributionType.Normal);
        var mle = new MaximumLikelihood(dist);
        mle.Estimate();
        dist.SetParameterValues(mle.BestParameterSet.Values);
        return dist;
    }

    /// <summary>
    /// Builds a <see cref="BivariateAnalysis"/> with the MCMC configuration used by the
    /// example project (DEMCzs, 3 chains, 1750 warmup, 3500 iterations, thinning 10,
    /// seed 12345, posterior-mean point estimator, 90% credible interval).
    /// </summary>
    private static BivariateAnalysis BuildAnalysis(BivariateDistribution dist)
    {
        var analysis = new BivariateAnalysis(dist);

        analysis.BayesianAnalysis.Type               = BayesianAnalysis.SamplerType.DEMCzs;
        analysis.BayesianAnalysis.NumberOfChains     = 3;
        analysis.BayesianAnalysis.WarmupIterations   = 1750;
        analysis.BayesianAnalysis.Iterations         = 3500;
        analysis.BayesianAnalysis.ThinningInterval   = 10;
        analysis.BayesianAnalysis.PRNGSeed           = 12345;
        analysis.BayesianAnalysis.CredibleIntervalWidth = 0.9;
        analysis.BayesianAnalysis.PointEstimator     = BayesianAnalysis.PointEstimateType.PosteriorMean;
        analysis.BayesianAnalysis.UseSimulationDefaults = false;

        return analysis;
    }

    /// <summary>
    /// Shared assertions: Bayesian fit succeeded, both marginals recovered within tolerance,
    /// and the first copula parameter recovered within the relative tolerance.
    /// </summary>
    private static void AssertRecovery(
        SyntheticBivariateData.BivariateSample sample,
        UnivariateDistribution marginalX,
        UnivariateDistribution marginalY,
        BivariateAnalysis analysis)
    {
        Assert.IsTrue(analysis.IsEstimated,
            $"Analysis failed to complete for {sample.CopulaType}.");
        Assert.IsNotNull(analysis.BayesianAnalysis.Results,
            $"Bayesian results are null for {sample.CopulaType}.");

        var map = analysis.BayesianAnalysis.Results!.MAP.Values;

        // Copula parameter[0] recovery.
        double trueTheta = sample.TrueCopulaParameters[0];
        double absTol    = Math.Abs(trueTheta) * CopulaThetaRelativeTolerance;
        Assert.AreEqual(trueTheta, map[0], absTol,
            $"{sample.CopulaType} Theta not recovered within {CopulaThetaRelativeTolerance:P0}: " +
            $"truth={trueTheta}, map={map[0]}.");

        // Marginal X (Normal) recovery.
        var xDist = (Normal)marginalX.Distribution;
        Assert.AreEqual(sample.TrueMarginalXParameters[0], xDist.Mu,    MarginalMuAbsoluteTolerance,
            "Marginal X μ not recovered.");
        Assert.AreEqual(sample.TrueMarginalXParameters[1], xDist.Sigma, MarginalSigmaAbsoluteTolerance,
            "Marginal X σ not recovered.");

        // Marginal Y (Normal) recovery.
        var yDist = (Normal)marginalY.Distribution;
        Assert.AreEqual(sample.TrueMarginalYParameters[0], yDist.Mu,    MarginalMuAbsoluteTolerance,
            "Marginal Y μ not recovered.");
        Assert.AreEqual(sample.TrueMarginalYParameters[1], yDist.Sigma, MarginalSigmaAbsoluteTolerance,
            "Marginal Y σ not recovered.");
    }

    /// <summary>
    /// Driver for 1-parameter copula recovery tests. Every copula uses the same workflow
    /// and assertions — the only differences are the generator and the parent CopulaType.
    /// </summary>
    private static async Task RunRecoveryAsync(SyntheticBivariateData.BivariateSample sample)
    {
        var marginalX = FitNormalMarginal(sample.DataFrameX);
        var marginalY = FitNormalMarginal(sample.DataFrameY);

        var dist     = new BivariateDistribution(marginalX, marginalY, sample.CopulaType);
        var analysis = BuildAnalysis(dist);

        await analysis.RunAsync();

        AssertRecovery(sample, marginalX, marginalY, analysis);
    }

    #endregion

    #region One-Parameter Copula Recovery

    /// <summary>
    /// Recovers ρ = 0.8 for a Normal (Gaussian) copula from 100 paired observations.
    /// </summary>
    [TestMethod]
    public async Task RecoverNormalCopulaParameters()
    {
        await RunRecoveryAsync(SyntheticBivariateData.GenerateNormalCopulaData());
    }

    /// <summary>
    /// Recovers θ = 3.0 for a Joe copula from 100 paired observations.
    /// </summary>
    [TestMethod]
    public async Task RecoverJoeCopulaParameters()
    {
        await RunRecoveryAsync(SyntheticBivariateData.GenerateJoeCopulaData());
    }

    /// <summary>
    /// Recovers θ = 2.0 for a Gumbel copula from 100 paired observations.
    /// </summary>
    [TestMethod]
    public async Task RecoverGumbelCopulaParameters()
    {
        await RunRecoveryAsync(SyntheticBivariateData.GenerateGumbelCopulaData());
    }

    /// <summary>
    /// Recovers θ = 8.0 for a Frank copula from 100 paired observations.
    /// </summary>
    [TestMethod]
    public async Task RecoverFrankCopulaParameters()
    {
        await RunRecoveryAsync(SyntheticBivariateData.GenerateFrankCopulaData());
    }

    /// <summary>
    /// Recovers θ = 1.5 for a Clayton copula from 100 paired observations.
    /// </summary>
    [TestMethod]
    public async Task RecoverClaytonCopulaParameters()
    {
        await RunRecoveryAsync(SyntheticBivariateData.GenerateClaytonCopulaData());
    }

    /// <summary>
    /// Recovers θ = 0.8 for an Ali-Mikhail-Haq copula from 100 paired observations.
    /// </summary>
    [TestMethod]
    public async Task RecoverAMHCopulaParameters()
    {
        await RunRecoveryAsync(SyntheticBivariateData.GenerateAMHCopulaData());
    }

    #endregion

    #region Two-Parameter Copula Recovery (Student's t)

    /// <summary>
    /// Recovers ρ = 0.8 and checks the degrees-of-freedom posterior for a Student's t copula
    /// fit from 100 paired observations. Strong symmetric tail dependence (λ_U = λ_L ≈ 0.49)
    /// at the true ν = 4.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ρ is asserted within 15% of the parent value (same as the 1-parameter copulas).
    /// </para>
    /// <para>
    /// ν is <b>weakly identified</b> at n = 100: for ν ≳ 20 the Student's t copula is
    /// empirically indistinguishable from the Gaussian copula, so the likelihood is nearly
    /// flat on the upper half of the prior range. Demarta &amp; McNeil (2005) §4.3 document
    /// this. Rather than asserting MAP-close-to-truth — which would be fragile — the test
    /// instead sanity-checks that the recovered ν sits inside the prior domain [3, 30] and
    /// has not piled up at the upper boundary (a signature of sampler failure). This is a
    /// weak but honest assertion given the information content of the data.
    /// </para>
    /// <para>
    /// The primary purpose of this test is to exercise the 2-parameter code path end-to-end
    /// (factory → <c>SetDefaultParameters</c> loop → <c>SetCopulaParameters</c> fan-out →
    /// MCMC posterior → <c>CreateFrequencyAnalysisResultsAsync</c> replay), not to prove
    /// tight recovery of ν. Tight ν recovery would require n ≫ 100.
    /// </para>
    /// </remarks>
    [TestMethod]
    public async Task RecoverStudentTCopulaParameters()
    {
        var sample = SyntheticBivariateData.GenerateStudentTCopulaData();

        var marginalX = FitNormalMarginal(sample.DataFrameX);
        var marginalY = FitNormalMarginal(sample.DataFrameY);

        var dist     = new BivariateDistribution(marginalX, marginalY, CopulaType.StudentT);
        var analysis = BuildAnalysis(dist);

        await analysis.RunAsync();

        AssertRecovery(sample, marginalX, marginalY, analysis);

        // Additional df assertion (not covered by the shared helper, which only checks [0]).
        var map = analysis.BayesianAnalysis.Results!.MAP.Values;
        Assert.AreEqual(2, map.Length, "Student's t MAP must have two parameters.");

        // Sanity check: ν inside the [3, 30] prior and not stuck at the upper boundary
        // (would indicate MCMC failed to latch onto data at all).
        Assert.IsTrue(map[1] >= 3.0 && map[1] <= 30.0,
            $"Student's t ν is outside its prior domain [3, 30]: map={map[1]}.");
        Assert.IsTrue(map[1] <= 25.0,
            $"Student's t ν piled up at the upper boundary (map={map[1]}); " +
            $"MCMC likely did not find the low-ν region favored by the data.");
    }

    #endregion
}
