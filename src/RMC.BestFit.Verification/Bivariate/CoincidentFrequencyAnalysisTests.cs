using Numerics.Distributions;
using Numerics.Distributions.Copulas;
using Numerics.Mathematics.Optimization;
using Numerics.Sampling.MCMC;
using RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using RMC.BestFit.Verification.Datasets;
using RMC.BestFit.Verification.Recovery;

namespace RMC.BestFit.Verification.Bivariate;

/// <summary>
/// End-to-end verification of <see cref="CoincidentFrequencyAnalysis"/> against closed-form
/// linear Normal-sum and nonlinear Lognormal response distributions.
/// </summary>
/// <remarks>
/// <para>
///     <b>Authors:</b>
///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
/// </para>
/// <para>
///     <b>Linear setup:</b> simulate a paired sample with Normal marginals and a
///     Gaussian copula at correlation ρ ∈ {0.0, 0.5, -0.5}. Fit the marginals via MLE and
///     run the full BivariateAnalysis Bayesian MCMC workflow. Build a 5×5 input grid from
///     the posterior-mean fitted marginals at quantiles {0.05, 0.25, 0.5, 0.75, 0.95},
///     define the response surface as Z = X + Y on that grid, and run
///     <see cref="CoincidentFrequencyAnalysis"/>.
/// </para>
/// <para>
///     <b>Linear truth:</b> X + Y is Normal. The mode curve is asserted element-wise
///     against 1 − Φ(z; μ̂_X+μ̂_Y, σ̂) where σ̂ = √(σ̂_X² + σ̂_Y² + 2 ρ̂ σ̂_X σ̂_Y) is computed
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

    /// <summary>Predeclared input quantiles for the nonlinear response table.</summary>
    private static readonly double[] NonlinearGridQuantiles =
        { 0.001, 0.005, 0.01, 0.025, 0.05, 0.1, 0.2, 0.35, 0.5, 0.65, 0.8, 0.9, 0.95, 0.975, 0.99, 0.995, 0.999 };

    /// <summary>Nonexceedance ordinates at which the nonlinear parent response must be covered.</summary>
    private static readonly double[] NonlinearCoverageProbabilities = { 0.1, 0.25, 0.5, 0.75, 0.9 };

    /// <summary>Predeclared maximum AEP error due to response-table numerical integration.</summary>
    private const double NonlinearNumericalAepErrorBound = 0.015;

    /// <summary>Independent asymptotic marginal-MLE draws used for uncertainty propagation.</summary>
    private const int MarginalMleUncertaintyDraws = 2000;

    #endregion

    #region Helpers

    /// <summary>
    /// Fits a Normal univariate distribution via MLE to the supplied DataFrame and returns
    /// a ready-to-use <see cref="UnivariateDistribution"/> with the fitted point estimate.
    /// </summary>
    private static UnivariateDistribution FitNormalMarginal(DataFrame dataFrame)
    {
        var dist = new UnivariateDistribution(dataFrame, UnivariateDistributionType.Normal);
        var mle = new MaximumLikelihood(dist, OptimizationMethod.DifferentialEvolution);
        mle.Estimate();
        dist.SetParameterValues(mle.BestParameterSet.Values);
        return dist;
    }

    /// <summary>
    /// Fits one Normal marginal and returns both the fitted model and its completed MLE.
    /// </summary>
    /// <param name="dataFrame">The generated marginal observations.</param>
    /// <returns>The fitted distribution and estimator.</returns>
    private static (UnivariateDistribution Distribution, MaximumLikelihood Estimator) FitNormalMarginalWithEstimator(
        DataFrame dataFrame)
    {
        var distribution = new UnivariateDistribution(dataFrame, UnivariateDistributionType.Normal);
        var estimator = new MaximumLikelihood(distribution, OptimizationMethod.DifferentialEvolution);
        Assert.IsTrue(estimator.Estimate(), $"Normal marginal MLE failed with status {estimator.Status}.");
        distribution.SetParameterValues(estimator.BestParameterSet.Values);
        return (distribution, estimator);
    }

    /// <summary>
    /// Builds independent asymptotic Normal-MLE uncertainty draws in physical `[mu, sigma]` order.
    /// </summary>
    /// <param name="fitted">The fitted Normal marginal.</param>
    /// <param name="sampleSize">The observational sample size.</param>
    /// <param name="seed">The predeclared uncertainty-draw seed.</param>
    /// <returns>A transport container holding independent MLE-uncertainty draws, not posterior draws.</returns>
    private static MCMCResults BuildNormalMleUncertaintyDraws(Normal fitted, int sampleSize, int seed)
    {
        var random = new Random(seed);
        var standardNormal = new Normal(0d, 1d);
        double muStandardError = fitted.Sigma / Math.Sqrt(sampleSize);
        double sigmaStandardError = fitted.Sigma / Math.Sqrt(2d * sampleSize);
        var draws = new List<ParameterSet>(MarginalMleUncertaintyDraws);
        for (int index = 0; index < MarginalMleUncertaintyDraws; index++)
        {
            double muProbability = Math.Clamp(random.NextDouble(), 1E-12, 1d - 1E-12);
            double sigmaProbability = Math.Clamp(random.NextDouble(), 1E-12, 1d - 1E-12);
            double mu = fitted.Mu + muStandardError * standardNormal.InverseCDF(muProbability);
            double sigma = fitted.Sigma + sigmaStandardError * standardNormal.InverseCDF(sigmaProbability);
            Assert.IsTrue(sigma > 0d, "Asymptotic Normal-MLE scale draw must remain positive.");
            draws.Add(new ParameterSet([mu, sigma], 0d));
        }
        return new MCMCResults(new ParameterSet([fitted.Mu, fitted.Sigma], 0d), draws, 0.05d);
    }

    /// <summary>
    /// Applies observed-information recovery to a fitted Normal marginal.
    /// </summary>
    /// <param name="label">Marginal label.</param>
    /// <param name="parents">Generating `[mu, sigma]` coordinates.</param>
    /// <param name="distribution">Fitted Normal model.</param>
    /// <param name="estimator">Completed maximum-likelihood fit.</param>
    private static void AssertNormalMarginalRecovery(
        string label,
        IReadOnlyList<double> parents,
        UnivariateDistribution distribution,
        MaximumLikelihood estimator)
    {
        Assert.IsInstanceOfType<IStandardError>(distribution.Distribution);
        double[,] covariance = ((IStandardError)distribution.Distribution).ParameterCovariance(
            RecoveryDesign.SampleSize,
            ParameterEstimationMethod.MaximumLikelihood);
        for (int index = 0; index < parents.Count; index++)
        {
            RecoveryAcceptance.AssertFrequentistStandardizedError(
                $"nonlinear marginal {label} {(index == 0 ? "mu" : "sigma")}",
                estimator.BestParameterSet.Values[index],
                parents[index],
                Math.Sqrt(covariance[index, index]));
        }
    }

    /// <summary>
    /// Interpolates one column of a response-band matrix on an ascending response grid.
    /// </summary>
    /// <param name="grid">Ascending response ordinates.</param>
    /// <param name="bands">Two-column lower/upper response bands.</param>
    /// <param name="column">Band column to interpolate.</param>
    /// <param name="ordinate">Response ordinate.</param>
    /// <returns>The linearly interpolated band value.</returns>
    private static double InterpolateBand(double[] grid, double[,] bands, int column, double ordinate)
    {
        Assert.IsTrue(grid[0] <= ordinate && ordinate <= grid[^1],
            $"Response ordinate {ordinate:G17} lies outside the CFA output grid.");
        int upper = Array.BinarySearch(grid, ordinate);
        if (upper >= 0)
            return bands[upper, column];
        upper = ~upper;
        int lower = upper - 1;
        double fraction = (ordinate - grid[lower]) / (grid[upper] - grid[lower]);
        return bands[lower, column] + fraction * (bands[upper, column] - bands[lower, column]);
    }

    /// <summary>
    /// Builds the bivariate analysis with the default MCMC configuration (DEMCzs simulation
    /// defaults, seed 12345, posterior-mean point estimator, 90% credible interval).
    /// </summary>
    private static BivariateAnalysis BuildAnalysis(BivariateDistribution dist)
    {
        var analysis = new BivariateAnalysis(dist);
        analysis.BayesianAnalysis.PRNGSeed = 12345;
        analysis.BayesianAnalysis.CredibleIntervalWidth = 0.90;
        analysis.BayesianAnalysis.PointEstimator = BayesianAnalysis.PointEstimateType.PosteriorMean;
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
        double muX = fittedX.Mu;
        double sigmaX = fittedX.Sigma;
        double muY = fittedY.Mu;
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

    /// <summary>
    /// Fits an N=1000 Gaussian-copula parent and verifies the monotone nonlinear response
    /// `Z=exp(0.01X+0.01Y)` against its exact Lognormal law.
    /// </summary>
    private static async Task RunExponentialLinearCombinationRecoveryAsync()
    {
        const double parentRho = 0.5d;
        const double responseCoefficient = 0.01d;
        SyntheticBivariateData.BivariateSample sample = SyntheticBivariateData.GenerateNormalCopulaData(
            rho: parentRho,
            n: RecoveryDesign.SampleSize,
            seed: 13055);
        Assert.AreEqual(RecoveryDesign.SampleSize, sample.DataFrameX.ExactSeries.Count);
        Assert.AreEqual(RecoveryDesign.SampleSize, sample.DataFrameY.ExactSeries.Count);

        var (marginalX, marginalXMle) = FitNormalMarginalWithEstimator(sample.DataFrameX);
        var (marginalY, marginalYMle) = FitNormalMarginalWithEstimator(sample.DataFrameY);
        var distribution = new BivariateDistribution(marginalX, marginalY, CopulaType.Normal);
        Assert.IsTrue(distribution.Parameters[0].LowerBound <= parentRho &&
            parentRho <= distribution.Parameters[0].UpperBound,
            "The nonlinear parent rho must be inside copula prior support.");
        Assert.IsTrue(distribution.DataLogLikelihood([parentRho]) > distribution.DataLogLikelihood([0d]),
            "The nonlinear parent likelihood must discriminate rho=0.5 from independence.");

        BivariateAnalysis bivariate = BuildAnalysis(distribution);
        bivariate.BayesianAnalysis.CredibleIntervalWidth = 0.95d;
        await bivariate.RunAsync();
        Assert.IsTrue(bivariate.IsEstimated, "Nonlinear parent BivariateAnalysis did not complete.");
        Assert.IsNotNull(bivariate.BayesianAnalysis.Results);

        AssertNormalMarginalRecovery("X", sample.TrueMarginalXParameters, marginalX, marginalXMle);
        AssertNormalMarginalRecovery("Y", sample.TrueMarginalYParameters, marginalY, marginalYMle);
        var rhoSummary = bivariate.BayesianAnalysis.Results!.ParameterResults[0].SummaryStatistics;
        RecoveryAcceptance.AssertBayesianRecovery(
            "nonlinear Gaussian-copula rho",
            parentRho,
            rhoSummary.LowerCI,
            rhoSummary.UpperCI,
            rhoSummary.Rhat,
            rhoSummary.ESS);

        var fittedX = (Normal)marginalX.Distribution!;
        var fittedY = (Normal)marginalY.Distribution!;
        double rhoHat = bivariate.BayesianAnalysis.Results.PosteriorMean.Values[0];
        double[] xValues = NonlinearGridQuantiles.Select(fittedX.InverseCDF).ToArray();
        double[] yValues = NonlinearGridQuantiles.Select(fittedY.InverseCDF).ToArray();
        var response = new double[xValues.Length, yValues.Length];
        for (int row = 0; row < xValues.Length; row++)
            for (int column = 0; column < yValues.Length; column++)
                response[row, column] = Math.Exp(responseCoefficient * (xValues[row] + yValues[column]));

        var cfa = new CoincidentFrequencyAnalysis(bivariate, xValues, yValues, response)
        {
            NumberOfBins = 61,
            MarginalXChain = BuildNormalMleUncertaintyDraws(fittedX, RecoveryDesign.SampleSize, 24680),
            MarginalYChain = BuildNormalMleUncertaintyDraws(fittedY, RecoveryDesign.SampleSize, 24681),
        };
        cfa.BayesianAnalysis.CredibleIntervalWidth = 0.95d;
        await cfa.RunAsync();
        Assert.IsTrue(cfa.IsEstimated, "Nonlinear CoincidentFrequencyAnalysis did not complete.");
        Assert.IsNotNull(cfa.AnalysisResults);
        Assert.IsNotNull(cfa.ZOutputValues);
        Assert.IsNotNull(cfa.AnalysisResults!.ConfidenceIntervals);

        int propagatedDraws = Math.Min(
            bivariate.BayesianAnalysis.Results.Output.Count,
            MarginalMleUncertaintyDraws);
        Assert.IsTrue(propagatedDraws >= RecoveryAcceptance.MinimumEffectiveSampleSize,
            $"The nonlinear response requires at least {RecoveryAcceptance.MinimumEffectiveSampleSize} propagated parameter draws.");

        double fittedLogMean = responseCoefficient * (fittedX.Mu + fittedY.Mu);
        double fittedLogSigma = responseCoefficient * Math.Sqrt(
            fittedX.Sigma * fittedX.Sigma + fittedY.Sigma * fittedY.Sigma +
            2d * rhoHat * fittedX.Sigma * fittedY.Sigma);
        var fittedLogResponse = new Normal(fittedLogMean, fittedLogSigma);
        double maximumNumericalError = 0d;
        for (int index = 0; index < cfa.ZOutputValues!.Length; index++)
        {
            double exactAep = 1d - fittedLogResponse.CDF(Math.Log(cfa.ZOutputValues[index]));
            maximumNumericalError = Math.Max(
                maximumNumericalError,
                Math.Abs(cfa.AnalysisResults.ModeCurve![index] - exactAep));
        }
        Assert.IsTrue(maximumNumericalError <= NonlinearNumericalAepErrorBound,
            $"Nonlinear response-table maximum AEP error {maximumNumericalError:G17} exceeds " +
            $"the predeclared bound {NonlinearNumericalAepErrorBound:G17}.");

        double parentLogMean = responseCoefficient *
            (sample.TrueMarginalXParameters[0] + sample.TrueMarginalYParameters[0]);
        double parentLogSigma = responseCoefficient * Math.Sqrt(
            sample.TrueMarginalXParameters[1] * sample.TrueMarginalXParameters[1] +
            sample.TrueMarginalYParameters[1] * sample.TrueMarginalYParameters[1] +
            2d * parentRho * sample.TrueMarginalXParameters[1] * sample.TrueMarginalYParameters[1]);
        var parentLogResponse = new Normal(parentLogMean, parentLogSigma);
        foreach (double probability in NonlinearCoverageProbabilities)
        {
            double responseOrdinate = Math.Exp(parentLogResponse.InverseCDF(probability));
            double parentAep = 1d - probability;
            double lower = InterpolateBand(
                cfa.ZOutputValues,
                cfa.AnalysisResults.ConfidenceIntervals!,
                0,
                responseOrdinate);
            double upper = InterpolateBand(
                cfa.ZOutputValues,
                cfa.AnalysisResults.ConfidenceIntervals!,
                1,
                responseOrdinate);
            RecoveryAcceptance.AssertIdentifiedResponseGrid(
                $"nonlinear response AEP at nonexceedance {probability:F2}",
                parentAep,
                lower,
                upper);
        }
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

    /// <summary>
    /// Verifies N=1000 recovery of a monotone nonlinear exponential response against its exact
    /// Lognormal distribution and central 95% propagated response bands.
    /// </summary>
    [TestMethod]
    public async Task ExponentialLinearCombination_ParentResponseInsidePredictiveBands()
    {
        await RunExponentialLinearCombinationRecoveryAsync();
    }

    #endregion
}
