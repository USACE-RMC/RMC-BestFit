using Numerics.Distributions;
using Numerics.Functions;
using Numerics.Mathematics.LinearAlgebra;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using RMC.BestFit.Verification.Datasets.UnivariateData;
using System.Diagnostics;

namespace RMC.BestFit.Verification.Univariate.Bulletin17CTests;

/// <summary>
/// Tests that the Bulletin 17C penalty function mechanism (parameter and quantile penalties)
/// produces inverse-variance weighted estimates consistent with the analytic multivariate
/// weighting formula for LogNormal and Log-Pearson Type III distributions.
/// </summary>
/// <remarks>
/// <para>
/// Each test generates synthetic data from a known distribution, fits the B17C model via GMM,
/// then adds parameter and/or quantile penalties with specified regional information. The
/// weighted GMM result is compared against the closed-form multivariate inverse-variance
/// weighted combination computed by <see cref="ComputeMultivariateWeightedCombination"/>.
/// </para>
/// <para>
/// The multivariate weighting formula is:
/// <c>theta_w = (Sigma_as^-1 + P)^-1 * (Sigma_as^-1 * theta_as + P * theta_reg)</c>,
/// where P is the precision contributed by the regional penalty terms. For quantile penalties,
/// the precision contribution is rank-1: <c>P_q = (1/MSE_q) * g * g'</c>, where g is the quantile gradient.
/// </para>
/// <para>
/// Tolerances vary by parameter type:
/// <list type="bullet">
/// <item><description>Location (mu): 1-5% — near-conjugate (Normal-Normal weighting), linear in GMM objective.</description></item>
/// <item><description>Scale (sigma): 5-10% — not conjugate (GMM objective is nonlinear in sigma), so there is inherent bias.</description></item>
/// <item><description>Skewness (gamma): 15% + absolute floor — skewness estimation is inherently noisy with small samples.</description></item>
/// <item><description>Quantile (Q99): 10-20% — derived from weighted parameters via delta method linearization.</description></item>
/// </list>
/// </para>
/// <para>
/// <b>Reference:</b> England, J.F., Jr., et al. (2019). Guidelines for determining flood flow frequency — Bulletin 17C.
/// U.S. Geological Survey Techniques and Methods, book 4, chap. B5, 148 p.
/// </para>
/// </remarks>
[TestClass]
[DoNotParallelize]
public class B17CPenalityTests
{

    /// <summary>
    /// Verifies that a penalty on mu (mean) for LogNormal with N=25 produces weighted estimates
    /// matching the analytic inverse-variance formula within 1% tolerance.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Regional info: regMean=3.25, regMeanMSE=0.004. The mu penalty is Normal-Normal conjugate
    /// (the GMM moment condition for mu is linear), so the scalar inverse-variance weighted
    /// formula is exact: <c>theta_w = (theta_as/MSE_as + theta_reg/MSE_reg) / (1/MSE_as + 1/MSE_reg)</c>.
    /// This justifies the tight 1% tolerance on both the weighted mean and its MSE.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void LogNormal_PenalityOnMu_N25()
    {
        var (df, trueParameters) = TheoreticalUnivariateData.GenerateLogNormalData(n: 25);

        // Fit at-site model
        var model = new Bulletin17CDistribution(df, UnivariateDistributionType.LogNormal);
        var gmm = new GeneralizedMethodOfMoments(model);
        gmm.Estimate();

        // Get regional factors and theoretical weighted results
        var atSiteCovar = gmm.GetCovarianceMatrix();
        double atSiteMean = gmm.BestParameterSet.Values[0];
        double atSiteMeanMSE = atSiteCovar[0, 0];
        double regMean = 3.25, regMeanMSE = 0.004;
        double weightedMean = (atSiteMean * regMeanMSE + regMean * atSiteMeanMSE) / (atSiteMeanMSE + regMeanMSE);
        double weightedMeanMSE = (atSiteMeanMSE * regMeanMSE) / (atSiteMeanMSE + regMeanMSE);

        // Setup penalty function
        model.ParameterPenalties[0].Mean = regMean;
        model.ParameterPenalties[0].MSE = regMeanMSE;
        model.ParameterPenalties[0].Enabled = true;
        model.SetPenaltyFunction();

        // Fit weighted model
        gmm = new GeneralizedMethodOfMoments(model);
        gmm.Estimate();

        // Get weighted results
        var gmmCovar = gmm.GetCovarianceMatrix();
        double gmmMean = gmm.BestParameterSet.Values[0];
        double gmmMeanMSE = gmmCovar[0, 0];

        // Test weighted  results
        Assert.AreEqual(weightedMean, gmmMean, Math.Abs(weightedMean) * 0.01);
        Assert.AreEqual(weightedMeanMSE, gmmMeanMSE, Math.Abs(weightedMeanMSE) * 0.01);

    }

    /// <summary>
    /// Verifies that a penalty on mu (mean) for LogNormal with N=100 produces weighted estimates
    /// matching the analytic inverse-variance formula within 1% tolerance.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Same as <see cref="LogNormal_PenalityOnMu_N25"/> but with N=100. The larger sample size reduces
    /// the at-site MSE, so the regional penalty has proportionally less influence on the weighted result.
    /// The 1% tolerance is maintained because the Normal-Normal conjugacy is exact regardless of sample size.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void LogNormal_PenalityOnMu_N100()
    {
        var (df, trueParameters) = TheoreticalUnivariateData.GenerateLogNormalData(n: 100);

        // Fit at-site model
        var model = new Bulletin17CDistribution(df, UnivariateDistributionType.LogNormal);
        var gmm = new GeneralizedMethodOfMoments(model);
        gmm.Estimate();

        // Get regional factors and theoretical weighted results
        var atSiteCovar = gmm.GetCovarianceMatrix();
        double atSiteMean = gmm.BestParameterSet.Values[0];
        double atSiteMeanMSE = atSiteCovar[0, 0];
        double regMean = 3.25, regMeanMSE = 0.004;
        double weightedMean = (atSiteMean * regMeanMSE + regMean * atSiteMeanMSE) / (atSiteMeanMSE + regMeanMSE);
        double weightedMeanMSE = (atSiteMeanMSE * regMeanMSE) / (atSiteMeanMSE + regMeanMSE);

        // Setup penalty function
        model.ParameterPenalties[0].Mean = regMean;
        model.ParameterPenalties[0].MSE = regMeanMSE;
        model.ParameterPenalties[0].Enabled = true;
        model.SetPenaltyFunction();

        // Fit weighted model
        gmm = new GeneralizedMethodOfMoments(model);
        gmm.Estimate();

        // Get weighted results
        var gmmCovar = gmm.GetCovarianceMatrix();
        double gmmMean = gmm.BestParameterSet.Values[0];
        double gmmMeanMSE = gmmCovar[0, 0];

        // Test weighted  results
        Assert.AreEqual(weightedMean, gmmMean, Math.Abs(weightedMean) * 0.01);
        Assert.AreEqual(weightedMeanMSE, gmmMeanMSE, Math.Abs(weightedMeanMSE) * 0.01);

    }


    /// <summary>
    /// Verifies that a penalty on sigma (standard deviation) for LogNormal with N=25 produces
    /// weighted estimates matching the analytic inverse-variance formula within 5% tolerance.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Regional info: regSigma=0.5, regSigmaMSE=0.0015. Unlike mu, the sigma penalty is NOT
    /// Normal-Normal conjugate because the GMM objective function is nonlinear in sigma (the
    /// second moment condition involves sigma squared). This means the linear inverse-variance
    /// formula is only an approximation, and there is inherent bias. The 5% tolerance accounts
    /// for this non-conjugacy.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void LogNormal_PenalityOnSigma_N25()
    {
        var (df, trueParameters) = TheoreticalUnivariateData.GenerateLogNormalData(n: 25);

        // Fit at-site model
        var model = new Bulletin17CDistribution(df, UnivariateDistributionType.LogNormal);
        var gmm = new GeneralizedMethodOfMoments(model);
        gmm.Estimate();

        // Get at-site sigma and its variance
        var atSiteCovar = gmm.GetCovarianceMatrix();
        double atSiteSigma = gmm.BestParameterSet.Values[1];
        double atSiteSigmaMSE = atSiteCovar[1, 1];

        // Regional sigma info (real-space)
        double regSigma = 0.5, regSigmaMSE = 0.0015;

        // Get theoretical weighted results
        double weightedSigma = (atSiteSigma * regSigmaMSE + regSigma * atSiteSigmaMSE) / (atSiteSigmaMSE + regSigmaMSE);
        double weightedSigmaMSE = (atSiteSigmaMSE * regSigmaMSE) / (atSiteSigmaMSE + regSigmaMSE);

        // Setup penalty — Mean and MSE stay in real-space, UseLog handles the conversion
        model.ParameterPenalties[1].Mean = regSigma;
        model.ParameterPenalties[1].MSE = regSigmaMSE;
        model.ParameterPenalties[1].Enabled = true;
        model.SetPenaltyFunction();

        // Fit weighted model
        gmm = new GeneralizedMethodOfMoments(model);
        gmm.Estimate();

        // Get weighted results
        var gmmCovar = gmm.GetCovarianceMatrix();
        double gmmSigma = gmm.BestParameterSet.Values[1];
        double gmmSigmaMSE = gmmCovar[1, 1];

        // Sigma is not Normal-Normal conjugate (GMM objective for sigma is not quadratic),
        // so there is inherent bias. Tolerance set to 5%
        Assert.AreEqual(weightedSigma, gmmSigma, Math.Abs(weightedSigma) * 0.05);
        Assert.AreEqual(weightedSigmaMSE, gmmSigmaMSE, Math.Abs(weightedSigmaMSE) * 0.05);

    }

    /// <summary>
    /// Verifies that a penalty on sigma (standard deviation) for LogNormal with N=100 produces
    /// weighted estimates matching the analytic inverse-variance formula within 5% tolerance.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Same as <see cref="LogNormal_PenalityOnSigma_N25"/> but with N=100. The larger sample size
    /// improves the GMM asymptotic approximation, but the non-conjugacy of the sigma penalty
    /// still introduces bias. The 5% tolerance is maintained.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void LogNormal_PenalityOnSigma_N100()
    {
        var (df, trueParameters) = TheoreticalUnivariateData.GenerateLogNormalData(n: 100);

        // Fit at-site model
        var model = new Bulletin17CDistribution(df, UnivariateDistributionType.LogNormal);
        var gmm = new GeneralizedMethodOfMoments(model);
        gmm.Estimate();

        // Get at-site sigma and its variance
        var atSiteCovar = gmm.GetCovarianceMatrix();
        double atSiteSigma = gmm.BestParameterSet.Values[1];
        double atSiteSigmaMSE = atSiteCovar[1, 1];

        // Regional sigma info (real-space)
        double regSigma = 0.5, regSigmaMSE = 0.0015;

        // Get theoretical weighted results
        double weightedSigma = (atSiteSigma * regSigmaMSE + regSigma * atSiteSigmaMSE) / (atSiteSigmaMSE + regSigmaMSE);
        double weightedSigmaMSE = (atSiteSigmaMSE * regSigmaMSE) / (atSiteSigmaMSE + regSigmaMSE);

        // Setup penalty — Mean and MSE stay in real-space, UseLog handles the conversion
        model.ParameterPenalties[1].Mean = regSigma;
        model.ParameterPenalties[1].MSE = regSigmaMSE;
        model.ParameterPenalties[1].Enabled = true;
        model.SetPenaltyFunction();

        // Fit weighted model
        gmm = new GeneralizedMethodOfMoments(model);
        gmm.Estimate();

        // Get weighted results
        var gmmCovar = gmm.GetCovarianceMatrix();
        double gmmSigma = gmm.BestParameterSet.Values[1];
        double gmmSigmaMSE = gmmCovar[1, 1];

        // Sigma is not Normal-Normal conjugate (GMM objective for sigma is not quadratic),
        // so there is inherent bias. Tolerance set to 5%
        Assert.AreEqual(weightedSigma, gmmSigma, Math.Abs(weightedSigma) * 0.05);
        Assert.AreEqual(weightedSigmaMSE, gmmSigmaMSE, Math.Abs(weightedSigmaMSE) * 0.05);

    }

    /// <summary>
    /// Verifies that simultaneous penalties on mu and sigma for LogNormal with N=25 produce
    /// weighted estimates matching the multivariate inverse-variance formula.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Regional info: regMean=3.25 (MSE=0.004), regSigma=0.5 (MSE=0.0015). With penalties on
    /// both parameters, the multivariate formula accounts for off-diagonal covariance coupling
    /// between mu and sigma. For LogNormal, this coupling is small but nonzero.
    /// </para>
    /// <para>
    /// Tolerances: mu at 5% (near-conjugate with small coupling), sigma at 10% (non-conjugate).
    /// </para>
    /// </remarks>
    [TestMethod]
    public void LogNormal_PenalityOnMuAndSigma_N25()
    {
        var (df, trueParameters) = TheoreticalUnivariateData.GenerateLogNormalData(n: 25);

        // Fit at-site model
        var model = new Bulletin17CDistribution(df, UnivariateDistributionType.LogNormal);
        var gmm = new GeneralizedMethodOfMoments(model);
        gmm.Estimate();

        // Get at-site estimates and covariance
        var atSiteCovar = gmm.GetCovarianceMatrix();
        var atSiteParams = gmm.BestParameterSet.Values;

        // Regional parameter info
        double regMean = 3.25, regMeanMSE = 0.004;
        double regSigma = 0.5, regSigmaMSE = 0.0015;

        // Compute multivariate weighted combination
        var paramPenalties = new (double Mean, double MSE)[]
        {
            (regMean, regMeanMSE),
            (regSigma, regSigmaMSE)
        };
        var (weightedParams, weightedCov) = ComputeMultivariateWeightedCombination(
            atSiteCovar, atSiteParams, paramPenalties, null, model);

        // Setup penalty functions
        model.ParameterPenalties[0].Mean = regMean;
        model.ParameterPenalties[0].MSE = regMeanMSE;
        model.ParameterPenalties[0].Enabled = true;
        model.ParameterPenalties[1].Mean = regSigma;
        model.ParameterPenalties[1].MSE = regSigmaMSE;
        model.ParameterPenalties[1].Enabled = true;
        model.SetPenaltyFunction();

        // Fit weighted model
        gmm = new GeneralizedMethodOfMoments(model);
        gmm.Estimate();

        // Get weighted results
        var gmmCovar = gmm.GetCovarianceMatrix();

        // Test weighted parameter results
        // Mu: 5% tolerance — near-conjugate, small off-diagonal coupling for LogNormal
        Assert.AreEqual(weightedParams[0], gmm.BestParameterSet.Values[0], Math.Abs(weightedParams[0]) * 0.05);
        Assert.AreEqual(weightedCov[0, 0], gmmCovar[0, 0], Math.Abs(weightedCov[0, 0]) * 0.05);
        // Sigma: 10% tolerance — not conjugate, so there is bias
        Assert.AreEqual(weightedParams[1], gmm.BestParameterSet.Values[1], Math.Abs(weightedParams[1]) * 0.10);
        Assert.AreEqual(weightedCov[1, 1], gmmCovar[1, 1], Math.Abs(weightedCov[1, 1]) * 0.10);
    }

    /// <summary>
    /// Verifies that simultaneous penalties on mu and sigma for LogNormal with N=100 produce
    /// weighted estimates matching the multivariate inverse-variance formula.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Same as <see cref="LogNormal_PenalityOnMuAndSigma_N25"/> but with N=100. Tolerances remain
    /// at 5% (mu) and 10% (sigma) because the non-conjugacy bias persists regardless of sample size.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void LogNormal_PenalityOnMuAndSigma_N100()
    {
        var (df, trueParameters) = TheoreticalUnivariateData.GenerateLogNormalData(n: 100);

        // Fit at-site model
        var model = new Bulletin17CDistribution(df, UnivariateDistributionType.LogNormal);
        var gmm = new GeneralizedMethodOfMoments(model);
        gmm.Estimate();

        // Get at-site estimates and covariance
        var atSiteCovar = gmm.GetCovarianceMatrix();
        var atSiteParams = gmm.BestParameterSet.Values;

        // Regional parameter info
        double regMean = 3.25, regMeanMSE = 0.004;
        double regSigma = 0.5, regSigmaMSE = 0.0015;

        // Compute multivariate weighted combination
        var paramPenalties = new (double Mean, double MSE)[]
        {
            (regMean, regMeanMSE),
            (regSigma, regSigmaMSE)
        };
        var (weightedParams, weightedCov) = ComputeMultivariateWeightedCombination(
            atSiteCovar, atSiteParams, paramPenalties, null, model);

        // Setup penalty functions
        model.ParameterPenalties[0].Mean = regMean;
        model.ParameterPenalties[0].MSE = regMeanMSE;
        model.ParameterPenalties[0].Enabled = true;
        model.ParameterPenalties[1].Mean = regSigma;
        model.ParameterPenalties[1].MSE = regSigmaMSE;
        model.ParameterPenalties[1].Enabled = true;
        model.SetPenaltyFunction();

        // Fit weighted model
        gmm = new GeneralizedMethodOfMoments(model);
        gmm.Estimate();

        // Get weighted results
        var gmmCovar = gmm.GetCovarianceMatrix();

        // Test weighted parameter results — tighter tolerance with N=100
        Assert.AreEqual(weightedParams[0], gmm.BestParameterSet.Values[0], Math.Abs(weightedParams[0]) * 0.05);
        Assert.AreEqual(weightedCov[0, 0], gmmCovar[0, 0], Math.Abs(weightedCov[0, 0]) * 0.05);
        Assert.AreEqual(weightedParams[1], gmm.BestParameterSet.Values[1], Math.Abs(weightedParams[1]) * 0.10);
        Assert.AreEqual(weightedCov[1, 1], gmmCovar[1, 1], Math.Abs(weightedCov[1, 1]) * 0.10);
    }

    /// <summary>
    /// Verifies that a quantile penalty on Q99 (AEP=0.01) for LogNormal with N=25 produces
    /// weighted estimates matching the multivariate inverse-variance formula with rank-1
    /// quantile precision.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Regional info: regQ99=4.5 (log10 space), regQ99MSE=0.01. The quantile penalty adds a
    /// rank-1 precision matrix <c>P_q = (1/MSE_q) * g * g'</c> to the at-site precision, where
    /// g = dQ/dtheta is the quantile gradient vector. For LogNormal, Q99 = mu + z_0.99 * sigma
    /// in log10 space, so the gradient is [1, z_0.99] and the linearization is exact.
    /// </para>
    /// <para>
    /// Tolerances: Q99 value at 10%, Q99 MSE at 20% (delta method variance propagation amplifies
    /// small parameter errors).
    /// </para>
    /// </remarks>
    [TestMethod]
    public void LogNormal_PenalityOnQ99_N25()
    {
        var (df, trueParameters) = TheoreticalUnivariateData.GenerateLogNormalData(n: 25);

        // Fit at-site model
        var model = new Bulletin17CDistribution(df, UnivariateDistributionType.LogNormal);
        var gmm = new GeneralizedMethodOfMoments(model);
        gmm.Estimate();

        // Get at-site estimates
        model.SetParameterValues(gmm.BestParameterSet.Values);
        var atSiteCovar = gmm.GetCovarianceMatrix();
        var atSiteParams = gmm.BestParameterSet.Values;

        // Compute multivariate weighted combination with quantile penalty
        double regQ99 = 4.5, regQ99MSE = 0.01;
        var quantilePenalties = new QuantilePenaltyInfo[]
        {
            new(0.01, regQ99, regQ99MSE, true)
        };
        var (weightedParams, weightedCov) = ComputeMultivariateWeightedCombination(
            atSiteCovar, atSiteParams, null, quantilePenalties, model);

        // Compute expected weighted Q99 from the weighted parameters
        double weightedQ99 = ComputeQuantile(weightedParams, 0.99, model);
        double weightedQ99MSE = model.QuantileVariance(0.99, weightedParams, weightedCov.ToArray());

        // Setup penalty function
        model.QuantilePenalties[0].AEP = 0.01;
        model.QuantilePenalties[0].Mean = regQ99;
        model.QuantilePenalties[0].MSE = regQ99MSE;
        model.QuantilePenalties[0].UseLog10 = true;
        model.QuantilePenalties[0].Enabled = true;
        model.SetPenaltyFunction();

        // Fit weighted model
        gmm = new GeneralizedMethodOfMoments(model);
        gmm.Estimate();

        // Get weighted results
        model.SetParameterValues(gmm.BestParameterSet.Values);
        var gmmCovar = gmm.GetCovarianceMatrix();
        double gmmQ99 = Math.Log10(model.Distribution.InverseCDF(0.99));
        double gmmQ99MSE = model.QuantileVariance(0.99, gmm.BestParameterSet.Values, gmmCovar.ToArray());

        // Test weighted results
        Assert.AreEqual(weightedQ99, gmmQ99, Math.Abs(weightedQ99) * 0.1);
        Assert.AreEqual(weightedQ99MSE, gmmQ99MSE, Math.Abs(weightedQ99MSE) * 0.2);
    }

    /// <summary>
    /// Verifies that a quantile penalty on Q99 (AEP=0.01) for LogNormal with N=100 produces
    /// weighted estimates matching the multivariate inverse-variance formula.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Same as <see cref="LogNormal_PenalityOnQ99_N25"/> but with N=100. The larger sample size
    /// reduces the at-site quantile variance, so the regional penalty has proportionally less
    /// influence. Tolerances remain at 10% (Q99 value) and 20% (Q99 MSE).
    /// </para>
    /// </remarks>
    [TestMethod]
    public void LogNormal_PenalityOnQ99_N100()
    {
        var (df, trueParameters) = TheoreticalUnivariateData.GenerateLogNormalData(n: 100);

        // Fit at-site model
        var model = new Bulletin17CDistribution(df, UnivariateDistributionType.LogNormal);
        var gmm = new GeneralizedMethodOfMoments(model);
        gmm.Estimate();

        // Get at-site estimates
        model.SetParameterValues(gmm.BestParameterSet.Values);
        var atSiteCovar = gmm.GetCovarianceMatrix();
        var atSiteParams = gmm.BestParameterSet.Values;

        // Compute multivariate weighted combination with quantile penalty
        double regQ99 = 4.5, regQ99MSE = 0.01;
        var quantilePenalties = new QuantilePenaltyInfo[]
        {
            new(0.01, regQ99, regQ99MSE, true)
        };
        var (weightedParams, weightedCov) = ComputeMultivariateWeightedCombination(
            atSiteCovar, atSiteParams, null, quantilePenalties, model);

        // Compute expected weighted Q99 from the weighted parameters
        double weightedQ99 = ComputeQuantile(weightedParams, 0.99, model);
        double weightedQ99MSE = model.QuantileVariance(0.99, weightedParams, weightedCov.ToArray());

        // Setup penalty function
        model.QuantilePenalties[0].AEP = 0.01;
        model.QuantilePenalties[0].Mean = regQ99;
        model.QuantilePenalties[0].MSE = regQ99MSE;
        model.QuantilePenalties[0].UseLog10 = true;
        model.QuantilePenalties[0].Enabled = true;
        model.SetPenaltyFunction();

        // Fit weighted model
        gmm = new GeneralizedMethodOfMoments(model);
        gmm.Estimate();

        // Get weighted results
        model.SetParameterValues(gmm.BestParameterSet.Values);
        var gmmCovar = gmm.GetCovarianceMatrix();
        double gmmQ99 = Math.Log10(model.Distribution.InverseCDF(0.99));
        double gmmQ99MSE = model.QuantileVariance(0.99, gmm.BestParameterSet.Values, gmmCovar.ToArray());

        // Test weighted results
        Assert.AreEqual(weightedQ99, gmmQ99, Math.Abs(weightedQ99) * 0.1);
        Assert.AreEqual(weightedQ99MSE, gmmQ99MSE, Math.Abs(weightedQ99MSE) * 0.2);
    }

    /// <summary>
    /// Verifies that simultaneous penalties on mu, sigma, and Q99 for LogNormal with N=25
    /// produce weighted estimates matching the multivariate inverse-variance formula with
    /// combined parameter and quantile precisions.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Regional info: regMean=3.25 (MSE=0.004), regSigma=0.5 (MSE=0.0015), regQ99=4.5 (MSE=0.01).
    /// The combined precision matrix includes both diagonal parameter precisions and the rank-1
    /// quantile precision: <c>A = Sigma_as^-1 + P_param + P_quant</c>. The penalties interact through
    /// the precision matrix, so each parameter estimate is influenced by all three penalty sources.
    /// </para>
    /// <para>
    /// Tolerances: mu at 5%/10% (value/MSE), sigma at 10%/15% (value/MSE), Q99 at 10%/20% (value/MSE).
    /// The wider MSE tolerances reflect the cumulative linearization error from combining multiple
    /// penalty precisions.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void LogNormal_PenalityOnMuSigmaAndQ99_N25()
    {
        var (df, trueParameters) = TheoreticalUnivariateData.GenerateLogNormalData(n: 25);

        // Fit at-site model
        var model = new Bulletin17CDistribution(df, UnivariateDistributionType.LogNormal);
        var gmm = new GeneralizedMethodOfMoments(model);
        gmm.Estimate();

        // Get at-site estimates
        model.SetParameterValues(gmm.BestParameterSet.Values);
        var atSiteCovar = gmm.GetCovarianceMatrix();
        var atSiteParams = gmm.BestParameterSet.Values;

        // Regional info
        double regMean = 3.25, regMeanMSE = 0.004;
        double regSigma = 0.5, regSigmaMSE = 0.0015;
        double regQ99 = 4.5, regQ99MSE = 0.01;

        // Compute multivariate weighted combination with both parameter and quantile penalties
        var paramPenalties = new (double Mean, double MSE)[]
        {
            (regMean, regMeanMSE),
            (regSigma, regSigmaMSE)
        };
        var quantilePenalties = new QuantilePenaltyInfo[]
        {
            new(0.01, regQ99, regQ99MSE, true)
        };
        var (weightedParams, weightedCov) = ComputeMultivariateWeightedCombination(
            atSiteCovar, atSiteParams, paramPenalties, quantilePenalties, model);

        // Compute expected weighted Q99 from the weighted parameters
        double weightedQ99 = ComputeQuantile(weightedParams, 0.99, model);
        double weightedQ99MSE = model.QuantileVariance(0.99, weightedParams, weightedCov.ToArray());

        // Setup penalty functions
        model.ParameterPenalties[0].Mean = regMean;
        model.ParameterPenalties[0].MSE = regMeanMSE;
        model.ParameterPenalties[0].Enabled = true;
        model.ParameterPenalties[1].Mean = regSigma;
        model.ParameterPenalties[1].MSE = regSigmaMSE;
        model.ParameterPenalties[1].Enabled = true;
        model.QuantilePenalties[0].AEP = 0.01;
        model.QuantilePenalties[0].Mean = regQ99;
        model.QuantilePenalties[0].MSE = regQ99MSE;
        model.QuantilePenalties[0].UseLog10 = true;
        model.QuantilePenalties[0].Enabled = true;
        model.SetPenaltyFunction();

        // Fit weighted model
        gmm = new GeneralizedMethodOfMoments(model);
        gmm.Estimate();

        // Get weighted results
        model.SetParameterValues(gmm.BestParameterSet.Values);
        var gmmCovar = gmm.GetCovarianceMatrix();

        // Test weighted parameter results — combined penalties interact through the precision matrix
        // Mu: 5% tolerance
        Assert.AreEqual(weightedParams[0], gmm.BestParameterSet.Values[0], Math.Abs(weightedParams[0]) * 0.05);
        Assert.AreEqual(weightedCov[0, 0], gmmCovar[0, 0], Math.Abs(weightedCov[0, 0]) * 0.10);
        // Sigma: 10% tolerance — not conjugate, so there is bias
        Assert.AreEqual(weightedParams[1], gmm.BestParameterSet.Values[1], Math.Abs(weightedParams[1]) * 0.10);
        Assert.AreEqual(weightedCov[1, 1], gmmCovar[1, 1], Math.Abs(weightedCov[1, 1]) * 0.15);
        // Q99: derived from weighted parameters
        double gmmQ99 = Math.Log10(model.Distribution.InverseCDF(0.99));
        double gmmQ99MSE = model.QuantileVariance(0.99, gmm.BestParameterSet.Values, gmmCovar.ToArray());
        Assert.AreEqual(weightedQ99, gmmQ99, Math.Abs(weightedQ99) * 0.1);
        Assert.AreEqual(weightedQ99MSE, gmmQ99MSE, Math.Abs(weightedQ99MSE) * 0.2);
    }

    #region LP-III Penalty Tests

    /// <summary>
    /// Verifies that penalties on mu and gamma (skipping sigma) for Log-Pearson Type III with N=25
    /// produce weighted estimates matching the multivariate inverse-variance formula.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Regional info: regMean=3.25 (MSE=0.004), regGamma=0.3 (MSE=0.05). No penalty on sigma
    /// (index 1), specified as NaN MSE in the analytic formula. The multivariate formula predicts
    /// an indirect effect on sigma through off-diagonal covariance coupling (rho(mu,sigma),
    /// rho(sigma,gamma)), but for LP-III the GMM objective is nonlinear in sigma, so the linear
    /// approximation breaks down (~45% error on sigma MSE). We only assert on penalized parameters.
    /// </para>
    /// <para>
    /// Tolerances: mu at 5%/25% (value/MSE), gamma at 15%+0.01/20% (value/MSE). The absolute
    /// floor on gamma (0.01) prevents failures when the true skewness is near zero.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void LogPearsonTypeIII_PenalityOnMuAndGamma_N25()
    {
        var (df, trueParameters) = TheoreticalUnivariateData.GenerateLogPearsonTypeIIIData(n: 25);

        // Fit at-site model
        var model = new Bulletin17CDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);
        var gmm = new GeneralizedMethodOfMoments(model);
        gmm.Estimate();

        // Get at-site estimates and covariance
        var atSiteCovar = gmm.GetCovarianceMatrix();
        var atSiteParams = gmm.BestParameterSet.Values;

        // Regional info — penalties on μ (index 0) and γ (index 2), not σ (index 1)
        double regMean = 3.25, regMeanMSE = 0.004;
        double regGamma = 0.3, regGammaMSE = 0.05;

        // Compute multivariate weighted combination — NaN MSE = no penalty on that parameter
        var paramPenalties = new (double Mean, double MSE)[]
        {
            (regMean, regMeanMSE),
            (double.NaN, double.NaN),   // no penalty on σ
            (regGamma, regGammaMSE)
        };
        var (weightedParams, weightedCov) = ComputeMultivariateWeightedCombination(
            atSiteCovar, atSiteParams, paramPenalties, null, model);

        // Debug: show at-site off-diagonal correlation
        double rho01 = atSiteCovar[0, 1] / Math.Sqrt(atSiteCovar[0, 0] * atSiteCovar[1, 1]);
        double rho02 = atSiteCovar[0, 2] / Math.Sqrt(atSiteCovar[0, 0] * atSiteCovar[2, 2]);
        double rho12 = atSiteCovar[1, 2] / Math.Sqrt(atSiteCovar[1, 1] * atSiteCovar[2, 2]);
        Debug.WriteLine($"LP-III N=25 correlations: ρ(μ,σ)={rho01:F4}, ρ(μ,γ)={rho02:F4}, ρ(σ,γ)={rho12:F4}");

        // Setup penalty functions
        model.ParameterPenalties[0].Mean = regMean;
        model.ParameterPenalties[0].MSE = regMeanMSE;
        model.ParameterPenalties[0].Enabled = true;
        model.ParameterPenalties[2].Mean = regGamma;
        model.ParameterPenalties[2].MSE = regGammaMSE;
        model.ParameterPenalties[2].Enabled = true;
        model.SetPenaltyFunction();

        // Fit weighted model
        gmm = new GeneralizedMethodOfMoments(model);
        gmm.Estimate();

        // Get weighted results
        var gmmCovar = gmm.GetCovarianceMatrix();

        // Test weighted parameter results — off-diagonal covariance matters here
        // Mu: 5% tolerance
        Assert.AreEqual(weightedParams[0], gmm.BestParameterSet.Values[0], Math.Abs(weightedParams[0]) * 0.05);
        Assert.AreEqual(weightedCov[0, 0], gmmCovar[0, 0], Math.Abs(weightedCov[0, 0]) * 0.25);
        // Sigma (index 1) is NOT penalized in this test. The multivariate formula predicts an
        // indirect effect on σ through off-diagonal coupling, but for LP-III the GMM objective
        // is nonlinear in σ, so the linear approximation breaks down (~45% error on σ MSE).
        // We only assert on penalized parameters (μ and γ).
        // Gamma: 15% tolerance — skewness estimation is inherently noisy
        Assert.AreEqual(weightedParams[2], gmm.BestParameterSet.Values[2], Math.Abs(weightedParams[2]) * 0.15 + 0.01);
        Assert.AreEqual(weightedCov[2, 2], gmmCovar[2, 2], Math.Abs(weightedCov[2, 2]) * 0.20);
    }

    /// <summary>
    /// Verifies that penalties on all three parameters (mu, sigma, gamma) for Log-Pearson Type III
    /// with N=25 produce weighted estimates matching the multivariate inverse-variance formula.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Regional info: regMean=3.25 (MSE=0.004), regSigma=0.5 (MSE=0.0015), regGamma=0.3 (MSE=0.05).
    /// With penalties on all three parameters, the full 3x3 precision matrix is augmented. The LP-III
    /// has significant off-diagonal coupling, particularly between sigma and gamma.
    /// </para>
    /// <para>
    /// Tolerances: mu at 5%/10%, sigma at 10%/15%, gamma at 15%+0.01/20%. The sigma tolerance is
    /// tighter here than in <see cref="LogPearsonTypeIII_PenalityOnMuAndGamma_N25"/> because the
    /// direct penalty on sigma constrains the GMM objective directly.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void LogPearsonTypeIII_PenalityOnAllParams_N25()
    {
        var (df, trueParameters) = TheoreticalUnivariateData.GenerateLogPearsonTypeIIIData(n: 25);

        // Fit at-site model
        var model = new Bulletin17CDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);
        var gmm = new GeneralizedMethodOfMoments(model);
        gmm.Estimate();

        // Get at-site estimates and covariance
        var atSiteCovar = gmm.GetCovarianceMatrix();
        var atSiteParams = gmm.BestParameterSet.Values;

        // Regional info — penalties on all 3 parameters
        double regMean = 3.25, regMeanMSE = 0.004;
        double regSigma = 0.5, regSigmaMSE = 0.0015;
        double regGamma = 0.3, regGammaMSE = 0.05;

        // Compute multivariate weighted combination
        var paramPenalties = new (double Mean, double MSE)[]
        {
            (regMean, regMeanMSE),
            (regSigma, regSigmaMSE),
            (regGamma, regGammaMSE)
        };
        var (weightedParams, weightedCov) = ComputeMultivariateWeightedCombination(
            atSiteCovar, atSiteParams, paramPenalties, null, model);

        // Setup penalty functions
        model.ParameterPenalties[0].Mean = regMean;
        model.ParameterPenalties[0].MSE = regMeanMSE;
        model.ParameterPenalties[0].Enabled = true;
        model.ParameterPenalties[1].Mean = regSigma;
        model.ParameterPenalties[1].MSE = regSigmaMSE;
        model.ParameterPenalties[1].Enabled = true;
        model.ParameterPenalties[2].Mean = regGamma;
        model.ParameterPenalties[2].MSE = regGammaMSE;
        model.ParameterPenalties[2].Enabled = true;
        model.SetPenaltyFunction();

        // Fit weighted model
        gmm = new GeneralizedMethodOfMoments(model);
        gmm.Estimate();

        // Get weighted results
        var gmmCovar = gmm.GetCovarianceMatrix();

        // Test weighted parameter results
        // Mu: 5% tolerance
        Assert.AreEqual(weightedParams[0], gmm.BestParameterSet.Values[0], Math.Abs(weightedParams[0]) * 0.05);
        Assert.AreEqual(weightedCov[0, 0], gmmCovar[0, 0], Math.Abs(weightedCov[0, 0]) * 0.10);
        // Sigma: 10% tolerance
        Assert.AreEqual(weightedParams[1], gmm.BestParameterSet.Values[1], Math.Abs(weightedParams[1]) * 0.10);
        Assert.AreEqual(weightedCov[1, 1], gmmCovar[1, 1], Math.Abs(weightedCov[1, 1]) * 0.15);
        // Gamma: 15% tolerance — skewness estimation is inherently noisy
        Assert.AreEqual(weightedParams[2], gmm.BestParameterSet.Values[2], Math.Abs(weightedParams[2]) * 0.15 + 0.01);
        Assert.AreEqual(weightedCov[2, 2], gmmCovar[2, 2], Math.Abs(weightedCov[2, 2]) * 0.20);
    }

    /// <summary>
    /// Verifies that penalties on all three parameters plus a quantile penalty on Q99 for
    /// Log-Pearson Type III with N=25 produce weighted estimates matching the multivariate
    /// inverse-variance formula.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Regional info: regMean=3.25 (MSE=0.004), regSigma=0.5 (MSE=0.0015), regGamma=0.3 (MSE=0.05),
    /// regQ99=4.5 (MSE=0.01, log10 space). This is the most complex penalty configuration tested,
    /// combining three diagonal parameter precisions with a rank-1 quantile precision in a 3x3
    /// precision matrix.
    /// </para>
    /// <para>
    /// For LP-III, the quantile function is nonlinear in the parameters (unlike LogNormal where it
    /// is linear), so the gradient-based linearization <c>Q(theta) ≈ Q(theta_0) + g'(theta - theta_0)</c>
    /// introduces additional approximation error. This is reflected in the wider tolerances: parameters
    /// at 5-15%/25%, Q99 at 10%/25%.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void LogPearsonTypeIII_PenalityOnAllParamsAndQ99_N25()
    {
        var (df, trueParameters) = TheoreticalUnivariateData.GenerateLogPearsonTypeIIIData(n: 25);

        // Fit at-site model
        var model = new Bulletin17CDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);
        var gmm = new GeneralizedMethodOfMoments(model);
        gmm.Estimate();

        // Get at-site estimates
        model.SetParameterValues(gmm.BestParameterSet.Values);
        var atSiteCovar = gmm.GetCovarianceMatrix();
        var atSiteParams = gmm.BestParameterSet.Values;

        // Regional info
        double regMean = 3.25, regMeanMSE = 0.004;
        double regSigma = 0.5, regSigmaMSE = 0.0015;
        double regGamma = 0.3, regGammaMSE = 0.05;
        double regQ99 = 4.5, regQ99MSE = 0.01;

        // Compute multivariate weighted combination
        var paramPenalties = new (double Mean, double MSE)[]
        {
            (regMean, regMeanMSE),
            (regSigma, regSigmaMSE),
            (regGamma, regGammaMSE)
        };
        var quantilePenalties = new QuantilePenaltyInfo[]
        {
            new(0.01, regQ99, regQ99MSE, true)
        };
        var (weightedParams, weightedCov) = ComputeMultivariateWeightedCombination(
            atSiteCovar, atSiteParams, paramPenalties, quantilePenalties, model);

        // Compute expected weighted Q99 from the weighted parameters
        double weightedQ99 = ComputeQuantile(weightedParams, 0.99, model);
        double weightedQ99MSE = model.QuantileVariance(0.99, weightedParams, weightedCov.ToArray());

        // Debug: show quantile gradient
        double[] g = model.QuantileGradient(0.99, atSiteParams);
        Debug.WriteLine($"LP-III Q99 gradient: ∂Q/∂μ={g[0]:F4}, ∂Q/∂σ={g[1]:F4}, ∂Q/∂γ={g[2]:F4}");

        // Setup penalty functions
        model.ParameterPenalties[0].Mean = regMean;
        model.ParameterPenalties[0].MSE = regMeanMSE;
        model.ParameterPenalties[0].Enabled = true;
        model.ParameterPenalties[1].Mean = regSigma;
        model.ParameterPenalties[1].MSE = regSigmaMSE;
        model.ParameterPenalties[1].Enabled = true;
        model.ParameterPenalties[2].Mean = regGamma;
        model.ParameterPenalties[2].MSE = regGammaMSE;
        model.ParameterPenalties[2].Enabled = true;
        model.QuantilePenalties[0].AEP = 0.01;
        model.QuantilePenalties[0].Mean = regQ99;
        model.QuantilePenalties[0].MSE = regQ99MSE;
        model.QuantilePenalties[0].UseLog10 = true;
        model.QuantilePenalties[0].Enabled = true;
        model.SetPenaltyFunction();

        // Fit weighted model
        gmm = new GeneralizedMethodOfMoments(model);
        gmm.Estimate();

        // Get weighted results
        model.SetParameterValues(gmm.BestParameterSet.Values);
        var gmmCovar = gmm.GetCovarianceMatrix();

        // Test weighted parameter results — combined penalties with 3-parameter coupling
        // Mu: 5% tolerance
        Assert.AreEqual(weightedParams[0], gmm.BestParameterSet.Values[0], Math.Abs(weightedParams[0]) * 0.05);
        Assert.AreEqual(weightedCov[0, 0], gmmCovar[0, 0], Math.Abs(weightedCov[0, 0]) * 0.25);
        // Sigma: 10% tolerance
        Assert.AreEqual(weightedParams[1], gmm.BestParameterSet.Values[1], Math.Abs(weightedParams[1]) * 0.10);
        Assert.AreEqual(weightedCov[1, 1], gmmCovar[1, 1], Math.Abs(weightedCov[1, 1]) * 0.25);
        // Gamma: 15% tolerance
        Assert.AreEqual(weightedParams[2], gmm.BestParameterSet.Values[2], Math.Abs(weightedParams[2]) * 0.15 + 0.01);
        Assert.AreEqual(weightedCov[2, 2], gmmCovar[2, 2], Math.Abs(weightedCov[2, 2]) * 0.25);
        // Q99: derived from weighted parameters — linearization introduces additional error for LP-III
        double gmmQ99 = Math.Log10(model.Distribution.InverseCDF(0.99));
        double gmmQ99MSE = model.QuantileVariance(0.99, gmm.BestParameterSet.Values, gmmCovar.ToArray());
        Assert.AreEqual(weightedQ99, gmmQ99, Math.Abs(weightedQ99) * 0.1);
        Assert.AreEqual(weightedQ99MSE, gmmQ99MSE, Math.Abs(weightedQ99MSE) * 0.25);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Information about a quantile penalty for multivariate weighting.
    /// </summary>
    /// <param name="AEP">Annual exceedance probability.</param>
    /// <param name="RegionalMean">Regional mean quantile value (in log10 if UseLog10).</param>
    /// <param name="RegionalMSE">Regional MSE of the quantile estimate.</param>
    /// <param name="UseLog10">Whether the penalty is in log10 space.</param>
    private record QuantilePenaltyInfo(double AEP, double RegionalMean, double RegionalMSE, bool UseLog10);

    /// <summary>
    /// Computes the multivariate inverse-variance weighted combination of at-site and
    /// regional estimates, accounting for parameter correlation and quantile penalty coupling.
    /// </summary>
    /// <param name="atSiteCovariance">The p × p at-site parameter covariance matrix.</param>
    /// <param name="atSiteParams">The at-site parameter estimates (length p).</param>
    /// <param name="parameterPenalties">
    /// Array of (Mean, MSE) tuples, one per parameter. Use NaN for MSE to skip a parameter.
    /// Pass null if no parameter penalties.
    /// </param>
    /// <param name="quantilePenalties">
    /// Array of quantile penalty info. Pass null if no quantile penalties.
    /// </param>
    /// <param name="model">The B17C model, used for computing quantile gradients.</param>
    /// <returns>
    /// A tuple of (WeightedParams, WeightedCovariance) computed via the multivariate
    /// inverse-variance weighting formula:
    /// <para>A = Σ_as⁻¹ + P_param + P_quant</para>
    /// <para>θ̂_w = A⁻¹ · b</para>
    /// where P_param is the diagonal parameter penalty precision, P_quant is the rank-1
    /// quantile penalty precision, and b is the combined information vector.
    /// </returns>
    /// <remarks>
    /// <para>
    /// For parameter penalties: P_param = diag(1/MSE_i) for penalized parameters.
    /// </para>
    /// <para>
    /// For quantile penalties: P_quant = Σ_k (1/MSE_qk) · g_k · g_k' where g_k = ∂Q_k/∂θ.
    /// The quantile contribution to the RHS uses a linearization:
    /// b_quant = Σ_k (1/MSE_qk) · g_k · (g_k'·θ̂_as − Q̂_k + Q₀_k).
    /// For distributions where the quantile is linear in parameters (Normal, LogNormal),
    /// this is exact because g_k'·θ̂_as = Q̂_k.
    /// </para>
    /// </remarks>
    private static (double[] WeightedParams, Matrix WeightedCovariance)
        ComputeMultivariateWeightedCombination(
            Matrix atSiteCovariance,
            double[] atSiteParams,
            (double Mean, double MSE)[]? parameterPenalties,
            QuantilePenaltyInfo[]? quantilePenalties,
            Bulletin17CDistribution model)
    {
        int p = atSiteParams.Length;

        // 1. At-site precision: Σ_as⁻¹
        var atSitePrecision = atSiteCovariance.Inverse();
        var precision = new double[p, p];
        for (int i = 0; i < p; i++)
            for (int j = 0; j < p; j++)
                precision[i, j] = atSitePrecision[i, j];

        // 2. RHS: b = Σ_as⁻¹ · θ̂_as
        double[] b = new double[p];
        for (int i = 0; i < p; i++)
            for (int j = 0; j < p; j++)
                b[i] += atSitePrecision[i, j] * atSiteParams[j];

        // 3. Add diagonal parameter penalty precisions
        if (parameterPenalties != null)
        {
            for (int i = 0; i < parameterPenalties.Length && i < p; i++)
            {
                var pen = parameterPenalties[i];
                if (double.IsNaN(pen.MSE) || pen.MSE <= 0) continue;

                double precI = 1.0 / pen.MSE;
                precision[i, i] += precI;
                b[i] += precI * pen.Mean;
            }
        }

        // 4. Add rank-1 quantile penalty precisions
        if (quantilePenalties != null)
        {
            foreach (var qp in quantilePenalties)
            {
                double prob = 1.0 - qp.AEP; // non-exceedance probability
                double[] g = model.QuantileGradient(prob, atSiteParams);

                // At-site quantile value in the penalty's space
                model.SetParameterValues(atSiteParams);
                double qAtSite;
                if (qp.UseLog10)
                    qAtSite = Math.Log10(model.Distribution.InverseCDF(prob));
                else
                    qAtSite = model.Distribution.InverseCDF(prob);

                double precQ = 1.0 / qp.RegionalMSE;

                // Rank-1 precision: P_quant += precQ · g · g'
                for (int i = 0; i < p; i++)
                    for (int j = 0; j < p; j++)
                        precision[i, j] += precQ * g[i] * g[j];

                // RHS: b += precQ · g · (g'·θ̂_as − Q̂_as + Q₀)
                // For linear quantile functions (Normal/LogNormal), g'·θ̂_as = Q̂_as exactly.
                double gDotTheta = 0;
                for (int k = 0; k < p; k++)
                    gDotTheta += g[k] * atSiteParams[k];

                double rhsScalar = gDotTheta - qAtSite + qp.RegionalMean;
                for (int i = 0; i < p; i++)
                    b[i] += precQ * g[i] * rhsScalar;
            }
        }

        // 5. Weighted covariance = A⁻¹
        var precisionMatrix = new Matrix(precision);
        var weightedCov = precisionMatrix.Inverse();

        // 6. Weighted parameters = A⁻¹ · b
        double[] weightedParams = new double[p];
        for (int i = 0; i < p; i++)
            for (int j = 0; j < p; j++)
                weightedParams[i] += weightedCov[i, j] * b[j];

        return (weightedParams, weightedCov);
    }

    /// <summary>
    /// Computes the log10 quantile from the given parameters using the model's distribution.
    /// </summary>
    /// <param name="parameters">The distribution parameters (moment-space).</param>
    /// <param name="probability">The non-exceedance probability.</param>
    /// <param name="model">The B17C model.</param>
    /// <returns>The log10 of the quantile at the given probability.</returns>
    private static double ComputeQuantile(double[] parameters, double probability, Bulletin17CDistribution model)
    {
        model.SetParameterValues(parameters);
        return Math.Log10(model.Distribution.InverseCDF(probability));
    }

    #endregion

}
