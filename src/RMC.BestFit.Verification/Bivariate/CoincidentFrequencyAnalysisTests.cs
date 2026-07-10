using Numerics.Distributions;
using Numerics.Distributions.Copulas;
using RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using RMC.BestFit.Verification.Datasets;

namespace RMC.BestFit.Verification.Bivariate;

/// <summary>
/// End-to-end Bayesian-MCMC verification of <see cref="CoincidentFrequencyAnalysis"/>
/// against the closed-form distribution of the sum of two correlated standard normals.
/// </summary>
/// <remarks>
/// <para>
///     <b>Authors:</b>
///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
/// </para>
/// <para>
///     <b>Test setup:</b> simulate a paired sample with X ~ N(0, 1), Y ~ N(0, 1), and a
///     Gaussian copula at correlation ρ ∈ {0.0, 0.5, -0.5}. Fit the marginals via MLE and
///     run the full BivariateAnalysis Bayesian MCMC workflow. Build a 5×5 input grid from
///     the posterior-mean fitted marginals at quantiles {0.05, 0.25, 0.5, 0.75, 0.95},
///     define the response surface as Z = X + Y on that grid, and run
///     <see cref="CoincidentFrequencyAnalysis"/>.
/// </para>
/// <para>
///     <b>Truth:</b> X + Y ~ N(0, √(2(1+ρ))). The mode curve is asserted element-wise
///     against 1 − Φ(z; 0, σ̂) where σ̂ = √(σ̂_X² + σ̂_Y² + 2 ρ̂ σ̂_X σ̂_Y) is computed
///     from the posterior-mean marginal moments and the posterior-mean copula correlation.
/// </para>
/// <para>
///     <b>Tolerance:</b> max abs error ≤ 0.05, mean abs error ≤ 0.01 across all
///     <see cref="CoincidentFrequencyAnalysis.NumberOfBins"/> Z output bins. The bound is
///     more permissive than the closed-form-input unit test because both the marginal fits
///     and the copula correlation carry MCMC sampling noise.
/// </para>
/// <para>
///     <b>Runtime:</b> ≈ 30–90 seconds per ρ. Long enough to belong in
///     <c>RMC.BestFit.Verification</c>. Inherits <c>[TestCategory("Verification")]</c>
///     from <c>VerificationAssemblyInfo.cs</c>.
/// </para>
/// </remarks>
[TestClass]
public class CoincidentFrequencyAnalysisTests
{
    #region Configuration

    /// <summary>Maximum permitted absolute error between mode-curve AEP and closed-form AEP.</summary>
    private const double MaxAbsErrorTolerance = 0.05;

    /// <summary>Maximum permitted mean absolute error.</summary>
    private const double MeanAbsErrorTolerance = 0.01;

    /// <summary>X / Y grid quantiles for the 5×5 response table.</summary>
    private static readonly double[] GridQuantiles = { 0.05, 0.25, 0.5, 0.75, 0.95 };

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
    /// Builds the bivariate analysis with a fast MCMC configuration appropriate for a
    /// verification run. The settings are deliberately lighter than the parameter-recovery
    /// suite — we don't need tight ρ̂ recovery, just enough posterior samples for a stable
    /// mean curve and credible interval.
    /// </summary>
    private static BivariateAnalysis BuildAnalysis(BivariateDistribution dist)
    {
        var analysis = new BivariateAnalysis(dist);
        analysis.BayesianAnalysis.Type                  = BayesianAnalysis.SamplerType.DEMCzs;
        analysis.BayesianAnalysis.NumberOfChains        = 3;
        analysis.BayesianAnalysis.WarmupIterations      = 1000;
        analysis.BayesianAnalysis.Iterations            = 2000;
        analysis.BayesianAnalysis.ThinningInterval      = 5;
        analysis.BayesianAnalysis.PRNGSeed              = 12345;
        analysis.BayesianAnalysis.CredibleIntervalWidth = 0.90;
        analysis.BayesianAnalysis.PointEstimator        = BayesianAnalysis.PointEstimateType.PosteriorMean;
        analysis.BayesianAnalysis.UseSimulationDefaults = false;
        return analysis;
    }

    /// <summary>
    /// Per-rho test driver: simulate, fit the bivariate, build the 5×5 grid of X+Y, run
    /// the coincident frequency analysis, and assert the mode curve matches the closed-form
    /// element-wise at every Z output bin.
    /// </summary>
    private static async Task RunSumOfNormalsAsync(double rho)
    {
        // Step 1 — simulate a paired sample under N(0,1), N(0,1), Gaussian copula(rho).
        // Override SyntheticBivariateData's default Normal-marginal parameters to (0, 1)
        // so the closed-form X+Y ~ N(0, √(2(1+rho))) applies cleanly.
        var sample = SyntheticBivariateData.GenerateNormalCopulaData(rho: rho, n: 1000, seed: 12345);

        // Step 2 — fit marginals (MLE) and run Bayesian MCMC on the bivariate copula model.
        var marginalX = FitNormalMarginal(sample.DataFrameX);
        var marginalY = FitNormalMarginal(sample.DataFrameY);
        var dist = new BivariateDistribution(marginalX, marginalY, CopulaType.Normal);
        var bivariate = BuildAnalysis(dist);
        await bivariate.RunAsync();

        Assert.IsTrue(bivariate.IsEstimated, "Bivariate MCMC failed to converge.");
        Assert.IsNotNull(bivariate.BayesianAnalysis.Results);

        // Step 3 — pull the posterior-mean point estimates so the closed-form sigma is
        // built from the FITTED rather than the parent parameters (apples-to-apples).
        var fittedX = (Normal)marginalX.Distribution!;
        var fittedY = (Normal)marginalY.Distribution!;
        double muX    = fittedX.Mu;
        double sigmaX = fittedX.Sigma;
        double muY    = fittedY.Mu;
        double sigmaY = fittedY.Sigma;
        double rhoHat = bivariate.BayesianAnalysis.Results!.PosteriorMean.Values[0];

        // Step 4 — build 5×5 grid at posterior-mean quantiles and the response Z = X + Y.
        var xValues = GridQuantiles.Select(q => fittedX.InverseCDF(q)).ToArray();
        var yValues = GridQuantiles.Select(q => fittedY.InverseCDF(q)).ToArray();
        var response = new double[GridQuantiles.Length, GridQuantiles.Length];
        for (int i = 0; i < GridQuantiles.Length; i++)
            for (int j = 0; j < GridQuantiles.Length; j++)
                response[i, j] = xValues[i] + yValues[j];

        // Step 5 — run the CFA. Marginal MCMC chains are intentionally not attached:
        // the upstream BivariateAnalysis copula-only chain still drives the bands, and
        // the mode curve uses the point-estimate marginals.
        var cfa = new CoincidentFrequencyAnalysis(bivariate, xValues, yValues, response)
        {
            NumberOfBins = 50,
        };
        cfa.BayesianAnalysis.CredibleIntervalWidth = 0.90;
        await cfa.RunAsync();

        Assert.IsTrue(cfa.IsEstimated, "CoincidentFrequencyAnalysis failed.");
        Assert.IsNotNull(cfa.AnalysisResults);
        Assert.IsNotNull(cfa.ZOutputValues);

        // Step 6 — closed-form truth at every Z output bin. The fitted ρ̂ is used so this
        // verification is independent of MCMC noise in ρ̂ (we are testing the CFA
        // algorithm, not the upstream BivariateAnalysis fit accuracy).
        double truthSigma = Math.Sqrt(sigmaX * sigmaX + sigmaY * sigmaY + 2 * rhoHat * sigmaX * sigmaY);
        var truthDist = new Normal(muX + muY, truthSigma);

        var modeCurve = cfa.AnalysisResults!.ModeCurve!;
        double maxAbsErr = 0;
        double sumAbsErr = 0;
        for (int k = 0; k < cfa.ZOutputValues!.Length; k++)
        {
            double truthAep = 1.0 - truthDist.CDF(cfa.ZOutputValues[k]);
            double err = Math.Abs(modeCurve[k] - truthAep);
            if (err > maxAbsErr) maxAbsErr = err;
            sumAbsErr += err;
        }
        double meanAbsErr = sumAbsErr / cfa.ZOutputValues.Length;

        // Step 7 — assert.
        Assert.IsTrue(maxAbsErr <= MaxAbsErrorTolerance,
            $"rho={rho} (rho_hat={rhoHat:F4}, sigma_hat={truthSigma:F4}): " +
            $"max abs error {maxAbsErr:F4} exceeds tolerance {MaxAbsErrorTolerance}.");
        Assert.IsTrue(meanAbsErr <= MeanAbsErrorTolerance,
            $"rho={rho} (rho_hat={rhoHat:F4}): " +
            $"mean abs error {meanAbsErr:F4} exceeds tolerance {MeanAbsErrorTolerance}.");

        // Sanity: AEP curve must be (weakly) monotonically decreasing in z.
        for (int k = 1; k < modeCurve.Length; k++)
        {
            Assert.IsTrue(modeCurve[k] <= modeCurve[k - 1] + 1e-9,
                $"rho={rho}: AEP must be non-increasing; failed at z={cfa.ZOutputValues[k]:F3}, k={k}.");
        }

        // Sanity: posterior-mean ρ̂ should be roughly within 0.1 of true ρ at n=1000.
        Assert.AreEqual(rho, rhoHat, 0.1,
            $"Posterior-mean rho={rhoHat:F4} drifted too far from truth {rho:F4}.");
    }

    #endregion

    #region Tests

    /// <summary>
    /// Verifies <c>SumOfNormals_RhoZero_MatchesClosedForm</c>.
    /// </summary>
    [TestMethod]
    public async Task SumOfNormals_RhoZero_MatchesClosedForm()
    {
        await RunSumOfNormalsAsync(0.0);
    }

    /// <summary>
    /// Verifies <c>SumOfNormals_RhoPositive_MatchesClosedForm</c>.
    /// </summary>
    [TestMethod]
    public async Task SumOfNormals_RhoPositive_MatchesClosedForm()
    {
        await RunSumOfNormalsAsync(0.5);
    }

    /// <summary>
    /// Verifies <c>SumOfNormals_RhoNegative_MatchesClosedForm</c>.
    /// </summary>
    [TestMethod]
    public async Task SumOfNormals_RhoNegative_MatchesClosedForm()
    {
        await RunSumOfNormalsAsync(-0.5);
    }

    #endregion
}
