using Numerics.Data.Statistics;
using Numerics.Distributions;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using RMC.BestFit.Verification.Datasets.UnivariateData;
using System.Diagnostics;

namespace RMC.BestFit.Verification.Univariate.B17CTests;

/// <summary>
/// Tests that the Bulletin 17C GMM (Generalized Method of Moments) covariance matrix
/// matches the Numerics asymptotic MoM ParameterCovariance for all supported distribution types.
/// </summary>
/// <remarks>
/// <para>
/// Each test generates synthetic data from a known distribution, fits the Bulletin 17C model
/// via GMM, and compares the resulting covariance matrix against the closed-form asymptotic
/// covariance from the Numerics distribution's <c>ParameterCovariance</c> method.
/// </para>
/// <para>
/// The two approaches differ because the GMM moment conditions include Bessel correction factors
/// (c2 = n/(n-1) on the second moment condition, c3 = n²/((n-1)(n-2)) on the third), while the
/// Numerics formulas use population central moments. This creates O(1/n) discrepancies that are
/// larger for small samples (N=25) and diminish for larger samples (N=100).
/// </para>
/// <para>
/// For 3-parameter distributions (Pearson Type III, Log-Pearson Type III), the Bessel factor c3
/// enters the numerical Jacobian D[2,0], causing the GMM Cov[0,2] entry to be nonzero where the
/// Numerics formula gives exactly zero (due to exact cancellation in D⁻¹·S·D⁻ᵀ). The tolerance
/// formula accounts for this by using the geometric mean of diagonal entries as a scale-appropriate
/// absolute floor.
/// </para>
/// </remarks>
[TestClass]
[DoNotParallelize]
public class B17CCovarianceTests
{

    /// <summary>
    /// Computes a tolerance for comparing covariance matrix entries between Numerics
    /// ParameterCovariance (asymptotic MoM) and B17C GMM (finite-sample with Bessel corrections).
    /// </summary>
    /// <param name="trueCovar">The Numerics ParameterCovariance matrix.</param>
    /// <param name="i">Row index.</param>
    /// <param name="j">Column index.</param>
    /// <param name="relativeTolerance">Relative tolerance (e.g. 0.15 for N=25, 0.05 for N=100).</param>
    /// <returns>The absolute tolerance for Assert.AreEqual.</returns>
    /// <remarks>
    /// The GMM moment conditions include Bessel correction factors c2 = n/(n-1) and c3 = n^2/((n-1)(n-2)),
    /// which create O(1/n) discrepancies vs. Numerics asymptotic formulas. For 3-parameter distributions,
    /// the entry Cov[0,2] is exactly zero from Numerics (exact cancellation in D^-1·S·D^-T) but nonzero
    /// from GMM because c3 enters the numerical Jacobian D[2,0]. Using the geometric mean of diagonal
    /// entries as a floor provides a scale-appropriate tolerance for these near-zero entries.
    /// </remarks>
    private static double CovarianceTolerance(double[,] trueCovar, int i, int j, double relativeTolerance)
    {
        double entryMagnitude = Math.Abs(trueCovar[i, j]);
        double diagonalScale = Math.Sqrt(Math.Abs(trueCovar[i, i] * trueCovar[j, j]));
        return relativeTolerance * Math.Max(entryMagnitude, diagonalScale) + 1E-6;
    }

    /// <summary>
    /// Verifies Exponential GMM covariance against asymptotic MoM covariance with N=25.
    /// </summary>
    /// <remarks>
    /// Exponential distribution (xi=10, alpha=50) is 2-parameter, so Bessel corrections
    /// affect only the S matrix (not D). Uses 15% relative tolerance for the small sample size.
    /// </remarks>
    [TestMethod]
    public void Exponential_Covariance_N25()
    {
        int n = 25;
        var (df, trueParameters) = SyntheticUnivariateData.GenerateExponentialData(xi: 10, alpha: 50, n: n);

        // Compute product-moment parameter estimates as the reference truth
        var dist = new Exponential();
        trueParameters = dist.ParametersFromMoments(Statistics.ProductMoments(df.ExactSeries.ValuesToArray()));
        dist.SetParameters(trueParameters);

        // Fit with GMM
        var model = new Bulletin17CDistribution(df, UnivariateDistributionType.Exponential);
        var gmm = new GeneralizedMethodOfMoments(model);
        gmm.Estimate();

        // Test fit
        Assert.IsTrue(gmm.IsEstimated, "GMM estimation failed for Exponential.");
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(trueParameters[i], gmm.BestParameterSet.Values[i], 1E-3,
                $"Parameter[{i}] mismatch for Exponential.");
        }

        // Test covariance
        var trueCovar = dist.ParameterCovariance(n, ParameterEstimationMethod.MethodOfMoments);
        var gmmCovar = gmm.GetCovarianceMatrix();

        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            for (int j = 0; j < model.NumberOfParameters; j++)
            {
                Debug.WriteLine($"Covariance[{i},{j}], True: [{trueCovar[i, j]}], GMM: [{gmmCovar[i, j]}]");
                double tol = CovarianceTolerance(trueCovar, i, j, 0.15);
                Assert.AreEqual(trueCovar[i, j], gmmCovar[i, j], tol,
                    $"Covariance[{i},{j}] mismatch for Exponential.");
            }
        }
    }

    /// <summary>
    /// Verifies Exponential GMM covariance against asymptotic MoM covariance with N=100.
    /// </summary>
    /// <remarks>
    /// Larger sample size reduces Bessel discrepancy to O(1%). Uses 5% relative tolerance.
    /// </remarks>
    [TestMethod]
    public void Exponential_Covariance_N100()
    {
        int n = 100;
        var (df, trueParameters) = SyntheticUnivariateData.GenerateExponentialData(xi: 10, alpha: 50, n: n);

        // Compute product-moment parameter estimates as the reference truth
        var dist = new Exponential();
        trueParameters = dist.ParametersFromMoments(Statistics.ProductMoments(df.ExactSeries.ValuesToArray()));
        dist.SetParameters(trueParameters);

        // Fit with GMM
        var model = new Bulletin17CDistribution(df, UnivariateDistributionType.Exponential);
        var gmm = new GeneralizedMethodOfMoments(model);
        gmm.Estimate();

        // Test fit
        Assert.IsTrue(gmm.IsEstimated, "GMM estimation failed for Exponential.");
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(trueParameters[i], gmm.BestParameterSet.Values[i], 1E-3,
                $"Parameter[{i}] mismatch for Exponential.");
        }

        // Test covariance
        var trueCovar = dist.ParameterCovariance(n, ParameterEstimationMethod.MethodOfMoments);
        var gmmCovar = gmm.GetCovarianceMatrix();

        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            for (int j = 0; j < model.NumberOfParameters; j++)
            {
                Debug.WriteLine($"Covariance[{i},{j}], True: [{trueCovar[i, j]}], GMM: [{gmmCovar[i, j]}]");
                double tol = CovarianceTolerance(trueCovar, i, j, 0.05);
                Assert.AreEqual(trueCovar[i, j], gmmCovar[i, j], tol,
                    $"Covariance[{i},{j}] mismatch for Exponential.");
            }
        }
    }

    /// <summary>
    /// Verifies Gamma GMM covariance against asymptotic MoM covariance with N=25.
    /// </summary>
    /// <remarks>
    /// Gamma distribution (alpha=5, beta=2) is 2-parameter, so Bessel corrections affect only
    /// the S matrix. Uses 15% relative tolerance for the small sample size.
    /// </remarks>
    [TestMethod]
    public void Gamma_Covariance_N25()
    {
        int n = 25;
        var (df, trueParameters) = SyntheticUnivariateData.GenerateGammaData(n: n);

        // Compute product-moment parameter estimates as the reference truth
        var dist = new GammaDistribution();
        trueParameters = dist.ParametersFromMoments(Statistics.ProductMoments(df.ExactSeries.ValuesToArray()));
        dist.SetParameters(trueParameters);

        // Fit with GMM
        var model = new Bulletin17CDistribution(df, UnivariateDistributionType.GammaDistribution);
        var gmm = new GeneralizedMethodOfMoments(model);
        gmm.Estimate();

        // Test fit
        Assert.IsTrue(gmm.IsEstimated, "GMM estimation failed for Gamma.");
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(trueParameters[i], gmm.BestParameterSet.Values[i], 1E-3,
                $"Parameter[{i}] mismatch for Gamma.");
        }

        // Test covariance
        var trueCovar = dist.ParameterCovariance(n, ParameterEstimationMethod.MethodOfMoments);
        var gmmCovar = gmm.GetCovarianceMatrix();

        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            for (int j = 0; j < model.NumberOfParameters; j++)
            {
                Debug.WriteLine($"Covariance[{i},{j}], True: [{trueCovar[i, j]}], GMM: [{gmmCovar[i, j]}]");
                double tol = CovarianceTolerance(trueCovar, i, j, 0.15);
                Assert.AreEqual(trueCovar[i, j], gmmCovar[i, j], tol,
                    $"Covariance[{i},{j}] mismatch for Gamma.");
            }
        }
    }

    /// <summary>
    /// Verifies Gamma GMM covariance against asymptotic MoM covariance with N=100.
    /// </summary>
    /// <remarks>
    /// Larger sample size reduces Bessel discrepancy to O(1%). Uses 5% relative tolerance.
    /// </remarks>
    [TestMethod]
    public void Gamma_Covariance_N100()
    {
        int n = 100;
        var (df, trueParameters) = SyntheticUnivariateData.GenerateGammaData(n: n);

        // Compute product-moment parameter estimates as the reference truth
        var dist = new GammaDistribution();
        trueParameters = dist.ParametersFromMoments(Statistics.ProductMoments(df.ExactSeries.ValuesToArray()));
        dist.SetParameters(trueParameters);

        // Fit with GMM
        var model = new Bulletin17CDistribution(df, UnivariateDistributionType.GammaDistribution);
        var gmm = new GeneralizedMethodOfMoments(model);
        gmm.Estimate();

        // Test fit
        Assert.IsTrue(gmm.IsEstimated, "GMM estimation failed for Gamma.");
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(trueParameters[i], gmm.BestParameterSet.Values[i], 1E-3,
                $"Parameter[{i}] mismatch for Gamma.");
        }

        // Test covariance
        var trueCovar = dist.ParameterCovariance(n, ParameterEstimationMethod.MethodOfMoments);
        var gmmCovar = gmm.GetCovarianceMatrix();

        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            for (int j = 0; j < model.NumberOfParameters; j++)
            {
                Debug.WriteLine($"Covariance[{i},{j}], True: [{trueCovar[i, j]}], GMM: [{gmmCovar[i, j]}]");
                double tol = CovarianceTolerance(trueCovar, i, j, 0.05);
                Assert.AreEqual(trueCovar[i, j], gmmCovar[i, j], tol,
                    $"Covariance[{i},{j}] mismatch for Gamma.");
            }
        }
    }

    /// <summary>
    /// Verifies Normal GMM covariance against asymptotic MoM covariance with N=25.
    /// </summary>
    /// <remarks>
    /// Normal distribution (mu=100, sigma=15) is 2-parameter symmetric (gamma=0). The Bessel
    /// correction on c2 affects Cov[1,1] (variance of sigma estimator) by approximately
    /// (2n²+1)/(2n(n-1)²) vs 1/(2n). Uses 15% relative tolerance.
    /// </remarks>
    [TestMethod]
    public void Normal_Covariance_N25()
    {
        int n = 25;
        var (df, trueParameters) = SyntheticUnivariateData.GenerateNormalData(n: n);

        // Compute product-moment parameter estimates as the reference truth
        var dist = new Normal();
        trueParameters = dist.ParametersFromMoments(Statistics.ProductMoments(df.ExactSeries.ValuesToArray()));
        dist.SetParameters(trueParameters);

        // Fit with GMM
        var model = new Bulletin17CDistribution(df, UnivariateDistributionType.Normal);
        var gmm = new GeneralizedMethodOfMoments(model);
        gmm.Estimate();

        // Test fit
        Assert.IsTrue(gmm.IsEstimated, "GMM estimation failed for Normal.");
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(trueParameters[i], gmm.BestParameterSet.Values[i], 1E-3,
                $"Parameter[{i}] mismatch for Normal.");
        }

        // Test covariance
        var trueCovar = dist.ParameterCovariance(n, ParameterEstimationMethod.MethodOfMoments);
        var gmmCovar = gmm.GetCovarianceMatrix();

        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            for (int j = 0; j < model.NumberOfParameters; j++)
            {
                Debug.WriteLine($"Covariance[{i},{j}], True: [{trueCovar[i, j]}], GMM: [{gmmCovar[i, j]}]");
                double tol = CovarianceTolerance(trueCovar, i, j, 0.15);
                Assert.AreEqual(trueCovar[i, j], gmmCovar[i, j], tol,
                    $"Covariance[{i},{j}] mismatch for Normal.");
            }
        }
    }

    /// <summary>
    /// Verifies Normal GMM covariance against asymptotic MoM covariance with N=100.
    /// </summary>
    /// <remarks>
    /// Larger sample size reduces Bessel discrepancy to O(1%). Uses 5% relative tolerance.
    /// </remarks>
    [TestMethod]
    public void Normal_Covariance_N100()
    {
        int n = 100;
        var (df, trueParameters) = SyntheticUnivariateData.GenerateNormalData(n: n);

        // Compute product-moment parameter estimates as the reference truth
        var dist = new Normal();
        trueParameters = dist.ParametersFromMoments(Statistics.ProductMoments(df.ExactSeries.ValuesToArray()));
        dist.SetParameters(trueParameters);

        // Fit with GMM
        var model = new Bulletin17CDistribution(df, UnivariateDistributionType.Normal);
        var gmm = new GeneralizedMethodOfMoments(model);
        gmm.Estimate();

        // Test fit
        Assert.IsTrue(gmm.IsEstimated, "GMM estimation failed for Normal.");
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(trueParameters[i], gmm.BestParameterSet.Values[i], 1E-3,
                $"Parameter[{i}] mismatch for Normal.");
        }

        // Test covariance
        var trueCovar = dist.ParameterCovariance(n, ParameterEstimationMethod.MethodOfMoments);
        var gmmCovar = gmm.GetCovarianceMatrix();

        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            for (int j = 0; j < model.NumberOfParameters; j++)
            {
                Debug.WriteLine($"Covariance[{i},{j}], True: [{trueCovar[i, j]}], GMM: [{gmmCovar[i, j]}]");
                double tol = CovarianceTolerance(trueCovar, i, j, 0.05);
                Assert.AreEqual(trueCovar[i, j], gmmCovar[i, j], tol,
                    $"Covariance[{i},{j}] mismatch for Normal.");
            }
        }
    }


    /// <summary>
    /// Verifies Pearson Type III GMM covariance against asymptotic MoM covariance with N=25.
    /// </summary>
    /// <remarks>
    /// Pearson Type III (mu=100, sigma=20, gamma=0.5) is 3-parameter, so both c2 and c3 Bessel
    /// factors apply. The c3 factor in D[2,0] causes Cov[0,2] to be nonzero from GMM where the
    /// Numerics formula gives exactly zero. The diagonal-scaled tolerance handles this naturally.
    /// Uses 15% relative tolerance.
    /// </remarks>
    [TestMethod]
    public void PearsonTypeIII_Covariance_N25()
    {
        int n = 25;
        var (df, trueParameters) = SyntheticUnivariateData.GeneratePearsonTypeIIIData(n: n);

        // Compute product-moment parameter estimates as the reference truth
        var dist = new PearsonTypeIII();
        trueParameters = dist.ParametersFromMoments(Statistics.ProductMoments(df.ExactSeries.ValuesToArray()));
        dist.SetParameters(trueParameters);

        // Fit with GMM
        var model = new Bulletin17CDistribution(df, UnivariateDistributionType.PearsonTypeIII);
        var gmm = new GeneralizedMethodOfMoments(model);
        gmm.Estimate();

        // Test fit
        Assert.IsTrue(gmm.IsEstimated, "GMM estimation failed for Pearson Type III.");
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(trueParameters[i], gmm.BestParameterSet.Values[i], 1E-3,
                $"Parameter[{i}] mismatch for Pearson Type III.");
        }

        // Test covariance
        var trueCovar = dist.ParameterCovariance(n, ParameterEstimationMethod.MethodOfMoments);
        var gmmCovar = gmm.GetCovarianceMatrix();

        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            for (int j = 0; j < model.NumberOfParameters; j++)
            {
                Debug.WriteLine($"Covariance[{i},{j}], True: [{trueCovar[i, j]}], GMM: [{gmmCovar[i, j]}]");
                double tol = CovarianceTolerance(trueCovar, i, j, 0.15);
                Assert.AreEqual(trueCovar[i, j], gmmCovar[i, j], tol,
                    $"Covariance[{i},{j}] mismatch for Pearson Type III.");
            }
        }
    }

    /// <summary>
    /// Verifies Pearson Type III GMM covariance against asymptotic MoM covariance with N=100.
    /// </summary>
    /// <remarks>
    /// Larger sample size reduces Bessel discrepancy. Uses 5% relative tolerance.
    /// </remarks>
    [TestMethod]
    public void PearsonTypeIII_Covariance_N100()
    {
        int n = 100;
        var (df, trueParameters) = SyntheticUnivariateData.GeneratePearsonTypeIIIData(n: n);

        // Compute product-moment parameter estimates as the reference truth
        var dist = new PearsonTypeIII();
        trueParameters = dist.ParametersFromMoments(Statistics.ProductMoments(df.ExactSeries.ValuesToArray()));
        dist.SetParameters(trueParameters);

        // Fit with GMM
        var model = new Bulletin17CDistribution(df, UnivariateDistributionType.PearsonTypeIII);
        var gmm = new GeneralizedMethodOfMoments(model);
        gmm.Estimate();

        // Test fit
        Assert.IsTrue(gmm.IsEstimated, "GMM estimation failed for Pearson Type III.");
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(trueParameters[i], gmm.BestParameterSet.Values[i], 1E-3,
                $"Parameter[{i}] mismatch for Pearson Type III.");
        }

        // Test covariance
        var trueCovar = dist.ParameterCovariance(n, ParameterEstimationMethod.MethodOfMoments);
        var gmmCovar = gmm.GetCovarianceMatrix();

        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            for (int j = 0; j < model.NumberOfParameters; j++)
            {
                Debug.WriteLine($"Covariance[{i},{j}], True: [{trueCovar[i, j]}], GMM: [{gmmCovar[i, j]}]");
                double tol = CovarianceTolerance(trueCovar, i, j, 0.05);
                Assert.AreEqual(trueCovar[i, j], gmmCovar[i, j], tol,
                    $"Covariance[{i},{j}] mismatch for Pearson Type III.");
            }
        }
    }

    /// <summary>
    /// Verifies Log-Normal GMM covariance against asymptotic MoM covariance with N=25.
    /// </summary>
    /// <remarks>
    /// Log-Normal (mu=3, sigma=0.5 in log10-space) is 2-parameter. The B17C GMM operates on
    /// log10-transformed data, so the covariance is in log-space parameters. Uses 15% relative tolerance.
    /// </remarks>
    [TestMethod]
    public void LogNormal_Covariance_N25()
    {
        int n = 25;
        var (df, trueParameters) = SyntheticUnivariateData.GenerateLogNormalData(n: n);

        // LogNormal parameters are moments of log10(x), so compute moments in log-space
        var dist = new LogNormal();
        trueParameters = dist.IndirectMethodOfMoments(df.ExactSeries.ValuesToArray());
        dist.SetParameters(trueParameters);

        // Fit with GMM
        var model = new Bulletin17CDistribution(df, UnivariateDistributionType.LogNormal);
        var gmm = new GeneralizedMethodOfMoments(model);
        gmm.Estimate();

        // Test fit
        Assert.IsTrue(gmm.IsEstimated, "GMM estimation failed for Log-Normal.");
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(trueParameters[i], gmm.BestParameterSet.Values[i], 1E-3,
                $"Parameter[{i}] mismatch for Log-Normal.");
        }

        // Test covariance
        var trueCovar = dist.ParameterCovariance(n, ParameterEstimationMethod.MethodOfMoments);
        var gmmCovar = gmm.GetCovarianceMatrix();

        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            for (int j = 0; j < model.NumberOfParameters; j++)
            {
                Debug.WriteLine($"Covariance[{i},{j}], True: [{trueCovar[i, j]}], GMM: [{gmmCovar[i, j]}]");
                double tol = CovarianceTolerance(trueCovar, i, j, 0.15);
                Assert.AreEqual(trueCovar[i, j], gmmCovar[i, j], tol,
                    $"Covariance[{i},{j}] mismatch for Log-Normal.");
            }
        }
    }

    /// <summary>
    /// Verifies Log-Normal GMM covariance against asymptotic MoM covariance with N=100.
    /// </summary>
    /// <remarks>
    /// Larger sample size reduces Bessel discrepancy to O(1%). Uses 5% relative tolerance.
    /// </remarks>
    [TestMethod]
    public void LogNormal_Covariance_N100()
    {
        int n = 100;
        var (df, trueParameters) = SyntheticUnivariateData.GenerateLogNormalData(n: n);

        // LogNormal parameters are moments of log10(x), so compute moments in log-space
        var dist = new LogNormal();
        trueParameters = dist.IndirectMethodOfMoments(df.ExactSeries.ValuesToArray());
        dist.SetParameters(trueParameters);

        // Fit with GMM
        var model = new Bulletin17CDistribution(df, UnivariateDistributionType.LogNormal);
        var gmm = new GeneralizedMethodOfMoments(model);
        gmm.Estimate();

        // Test fit
        Assert.IsTrue(gmm.IsEstimated, "GMM estimation failed for Log-Normal.");
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(trueParameters[i], gmm.BestParameterSet.Values[i], 1E-3,
                $"Parameter[{i}] mismatch for Log-Normal.");
        }

        // Test covariance
        var trueCovar = dist.ParameterCovariance(n, ParameterEstimationMethod.MethodOfMoments);
        var gmmCovar = gmm.GetCovarianceMatrix();

        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            for (int j = 0; j < model.NumberOfParameters; j++)
            {
                Debug.WriteLine($"Covariance[{i},{j}], True: [{trueCovar[i, j]}], GMM: [{gmmCovar[i, j]}]");
                double tol = CovarianceTolerance(trueCovar, i, j, 0.05);
                Assert.AreEqual(trueCovar[i, j], gmmCovar[i, j], tol,
                    $"Covariance[{i},{j}] mismatch for Log-Normal.");
            }
        }
    }

    /// <summary>
    /// Verifies Log-Pearson Type III GMM covariance against asymptotic MoM covariance with N=25.
    /// </summary>
    /// <remarks>
    /// Log-Pearson Type III (mu=3, sigma=0.5, gamma=0.2 in log10-space) is 3-parameter, so both
    /// c2 and c3 Bessel factors apply. The c3 factor in D[2,0] causes Cov[0,2] to be nonzero from
    /// GMM where the Numerics formula gives exactly zero. Uses 15% relative tolerance.
    /// </remarks>
    [TestMethod]
    public void LogPearsonTypeIII_Covariance_N25()
    {
        int n = 25;
        var (df, trueParameters) = SyntheticUnivariateData.GenerateLogPearsonTypeIIIData(n: n);

        // LP-III parameters are moments of log10(x), so compute moments in log-space
        var logValues = df.ExactSeries.ValuesToArray().Select(x => Math.Log10(x)).ToArray();
        var dist = new LogPearsonTypeIII();
        trueParameters = dist.ParametersFromMoments(Statistics.ProductMoments(logValues));
        dist.SetParameters(trueParameters);

        // Fit with GMM
        var model = new Bulletin17CDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);
        var gmm = new GeneralizedMethodOfMoments(model);
        gmm.Estimate();

        // Test fit
        Assert.IsTrue(gmm.IsEstimated, "GMM estimation failed for Log-Pearson Type III.");
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(trueParameters[i], gmm.BestParameterSet.Values[i], 1E-3,
                $"Parameter[{i}] mismatch for Log-Pearson Type III.");
        }

        // Test covariance
        var trueCovar = dist.ParameterCovariance(n, ParameterEstimationMethod.MethodOfMoments);
        var gmmCovar = gmm.GetCovarianceMatrix();

        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            for (int j = 0; j < model.NumberOfParameters; j++)
            {
                Debug.WriteLine($"Covariance[{i},{j}], True: [{trueCovar[i, j]}], GMM: [{gmmCovar[i, j]}]");
                double tol = CovarianceTolerance(trueCovar, i, j, 0.15);
                Assert.AreEqual(trueCovar[i, j], gmmCovar[i, j], tol,
                    $"Covariance[{i},{j}] mismatch for Log-Pearson Type III.");
            }
        }
    }

    /// <summary>
    /// Verifies Log-Pearson Type III GMM covariance against asymptotic MoM covariance with N=100.
    /// </summary>
    /// <remarks>
    /// Larger sample size reduces Bessel discrepancy. Uses 5% relative tolerance.
    /// </remarks>
    [TestMethod]
    public void LogPearsonTypeIII_Covariance_N100()
    {
        int n = 100;
        var (df, trueParameters) = SyntheticUnivariateData.GenerateLogPearsonTypeIIIData(n: n);

        // LP-III parameters are moments of log10(x), so compute moments in log-space
        var logValues = df.ExactSeries.ValuesToArray().Select(x => Math.Log10(x)).ToArray();
        var dist = new LogPearsonTypeIII();
        trueParameters = dist.ParametersFromMoments(Statistics.ProductMoments(logValues));
        dist.SetParameters(trueParameters);

        // Fit with GMM
        var model = new Bulletin17CDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);
        var gmm = new GeneralizedMethodOfMoments(model);
        gmm.Estimate();

        // Test fit
        Assert.IsTrue(gmm.IsEstimated, "GMM estimation failed for Log-Pearson Type III.");
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(trueParameters[i], gmm.BestParameterSet.Values[i], 1E-3,
                $"Parameter[{i}] mismatch for Log-Pearson Type III.");
        }

        // Test covariance
        var trueCovar = dist.ParameterCovariance(n, ParameterEstimationMethod.MethodOfMoments);
        var gmmCovar = gmm.GetCovarianceMatrix();

        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            for (int j = 0; j < model.NumberOfParameters; j++)
            {
                Debug.WriteLine($"Covariance[{i},{j}], True: [{trueCovar[i, j]}], GMM: [{gmmCovar[i, j]}]");
                double tol = CovarianceTolerance(trueCovar, i, j, 0.05);
                Assert.AreEqual(trueCovar[i, j], gmmCovar[i, j], tol,
                    $"Covariance[{i},{j}] mismatch for Log-Pearson Type III.");
            }
        }
    }


    /// <summary>
    /// Verifies Log-Pearson Type III GMM covariance against asymptotic MoM covariance using
    /// the Bulletin 17C Example 1 dataset (Fishkill Creek, n=68).
    /// </summary>
    /// <remarks>
    /// Uses the standard B17C verification dataset rather than synthetic data. The sample size
    /// of 68 is intermediate between N=25 and N=100, so a 10% relative tolerance is used.
    /// </remarks>
    [TestMethod]
    public void LogPearsonTypeIII_Covariance_Example1()
    {
        var (df, trueParameters) = Bulletin17CData.GetExample1();

        // LP-III parameters are moments of log10(x), so compute moments in log-space
        var logValues = df.ExactSeries.ValuesToArray().Select(x => Math.Log10(x)).ToArray();
        var dist = new LogPearsonTypeIII();
        trueParameters = dist.ParametersFromMoments(Statistics.ProductMoments(logValues));
        dist.SetParameters(trueParameters);

        // Fit with GMM
        var model = new Bulletin17CDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);
        var gmm = new GeneralizedMethodOfMoments(model);
        gmm.Estimate();

        // Test fit
        Assert.IsTrue(gmm.IsEstimated, "GMM estimation failed for Log-Pearson Type III.");
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(trueParameters[i], gmm.BestParameterSet.Values[i], 1E-3,
                $"Parameter[{i}] mismatch for Log-Pearson Type III.");
        }

        // Test covariance
        var trueCovar = dist.ParameterCovariance(df.TotalRecordLength(), ParameterEstimationMethod.MethodOfMoments);
        var gmmCovar = gmm.GetCovarianceMatrix();

        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            for (int j = 0; j < model.NumberOfParameters; j++)
            {
                Debug.WriteLine($"Covariance[{i},{j}], True: [{trueCovar[i, j]}], GMM: [{gmmCovar[i, j]}]");
                double tol = CovarianceTolerance(trueCovar, i, j, 0.10);
                Assert.AreEqual(trueCovar[i, j], gmmCovar[i, j], tol,
                    $"Covariance[{i},{j}] mismatch for Log-Pearson Type III Example 1.");
            }
        }
    }
}
