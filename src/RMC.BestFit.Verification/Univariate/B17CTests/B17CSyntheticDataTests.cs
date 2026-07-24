using Numerics.Data.Statistics;
using Numerics.Distributions;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using RMC.BestFit.Verification.Datasets.UnivariateData;

namespace RMC.BestFit.Verification.Univariate.B17CTests;

/// <summary>
/// Verifies that the Bulletin 17C Generalized Method of Moments (GMM) estimator
/// recovers standard product-moment estimates for each supported distribution type
/// when applied to large, uncensored synthetic datasets.
/// </summary>
/// <remarks>
/// <para>
/// For complete (uncensored) data with no perception thresholds or low outliers,
/// the B17C GMM estimator should reduce to standard product-moment estimation.
/// Each test generates 1000 synthetic values from a known distribution, computes
/// the sample product moments, converts them to distribution parameters via
/// <see cref="IMomentEstimation.ParametersFromMoments"/>, and verifies that GMM
/// returns the same parameter values within tolerance (1E-3).
/// </para>
/// <para>
/// The six distributions supported by <see cref="Bulletin17CDistribution"/> are tested:
/// Exponential, Gamma, Normal, Pearson Type III, Log-Normal, and Log-Pearson Type III.
/// For log-space distributions (Log-Normal, Log-Pearson Type III), product moments are
/// computed on the log10-transformed data to match the parameter space.
/// </para>
/// </remarks>
[TestClass]
[DoNotParallelize]
public class B17CSyntheticDataTests
{
    /// <summary>
    /// Tests GMM parameter recovery for the Exponential distribution.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The Exponential distribution has 2 parameters: location (ξ) and scale (α).
    /// Product moments of the raw data yield ξ = mean - stdDev and α = stdDev.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void Exponential_RecoversTrueParameters()
    {
        var (df, trueParameters) = SyntheticUnivariateData.GenerateExponentialData();

        // Compute product-moment parameter estimates as the reference truth
        var dist = new Exponential();
        trueParameters = dist.ParametersFromMoments(Statistics.ProductMoments(df.ExactSeries.ValuesToArray()));

        var model = new Bulletin17CDistribution(df, UnivariateDistributionType.Exponential);

        var gmm = new GeneralizedMethodOfMoments(model);
        gmm.Estimate();

        Assert.IsTrue(gmm.IsEstimated, "GMM estimation failed for Exponential.");

        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(trueParameters[i], gmm.BestParameterSet.Values[i], 1E-3,
                $"Parameter[{i}] mismatch for Exponential.");
        }
    }

    /// <summary>
    /// Tests GMM parameter recovery for the Gamma distribution.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The Gamma distribution has 2 parameters: shape (α) and rate (β).
    /// Product moments of the raw data are converted to α and β via the
    /// relationships α = (mean/stdDev)² and β = mean/stdDev².
    /// </para>
    /// </remarks>
    [TestMethod]
    public void Gamma_RecoversTrueParameters()
    {
        var (df, trueParameters) = SyntheticUnivariateData.GenerateGammaData();

        var dist = new GammaDistribution();
        trueParameters = dist.ParametersFromMoments(Statistics.ProductMoments(df.ExactSeries.ValuesToArray()));

        var model = new Bulletin17CDistribution(df, UnivariateDistributionType.GammaDistribution);

        var gmm = new GeneralizedMethodOfMoments(model);
        gmm.Estimate();

        Assert.IsTrue(gmm.IsEstimated, "GMM estimation failed for Gamma.");

        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(trueParameters[i], gmm.BestParameterSet.Values[i], 1E-3,
                $"Parameter[{i}] mismatch for Gamma.");
        }
    }

    /// <summary>
    /// Tests GMM parameter recovery for the Normal distribution.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The Normal distribution has 2 parameters: mean (μ) and standard deviation (σ).
    /// These are directly the first two product moments: μ = sample mean, σ = sample stdDev.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void Normal_RecoversTrueParameters()
    {
        var (df, trueParameters) = SyntheticUnivariateData.GenerateNormalData();

        var dist = new Normal();
        trueParameters = dist.ParametersFromMoments(Statistics.ProductMoments(df.ExactSeries.ValuesToArray()));

        var model = new Bulletin17CDistribution(df, UnivariateDistributionType.Normal);

        var gmm = new GeneralizedMethodOfMoments(model);
        gmm.Estimate();

        Assert.IsTrue(gmm.IsEstimated, "GMM estimation failed for Normal.");

        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(trueParameters[i], gmm.BestParameterSet.Values[i], 1E-3,
                $"Parameter[{i}] mismatch for Normal.");
        }
    }

    /// <summary>
    /// Tests GMM parameter recovery for the Pearson Type III distribution.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The Pearson Type III has 3 parameters: mean (μ), standard deviation (σ), and
    /// skewness (γ). These correspond directly to the first three product moments
    /// of the raw data.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void PearsonTypeIII_RecoversTrueParameters()
    {
        var (df, trueParameters) = SyntheticUnivariateData.GeneratePearsonTypeIIIData();

        var dist = new PearsonTypeIII();
        trueParameters = dist.ParametersFromMoments(Statistics.ProductMoments(df.ExactSeries.ValuesToArray()));

        var model = new Bulletin17CDistribution(df, UnivariateDistributionType.PearsonTypeIII);

        var gmm = new GeneralizedMethodOfMoments(model);
        gmm.Estimate();

        Assert.IsTrue(gmm.IsEstimated, "GMM estimation failed for Pearson Type III.");

        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(trueParameters[i], gmm.BestParameterSet.Values[i], 1E-3,
                $"Parameter[{i}] mismatch for Pearson Type III.");
        }
    }

    /// <summary>
    /// Tests GMM parameter recovery for the Log-Normal distribution.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The Log-Normal has 2 parameters: mean (μ) and standard deviation (σ) of the
    /// log10-transformed data. Product moments are computed on log10(x) values
    /// to match the parameter space.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void LogNormal_RecoversTrueParameters()
    {
        var (df, trueParameters) = SyntheticUnivariateData.GenerateLogNormalData();

        // Log-Normal parameters are moments of log10(x), so compute moments in log-space
        var dist = new LogNormal();
        trueParameters = dist.IndirectMethodOfMoments(df.ExactSeries.ValuesToArray());

        var model = new Bulletin17CDistribution(df, UnivariateDistributionType.LogNormal);

        var gmm = new GeneralizedMethodOfMoments(model);
        gmm.Estimate();

        Assert.IsTrue(gmm.IsEstimated, "GMM estimation failed for Log-Normal.");

        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(trueParameters[i], gmm.BestParameterSet.Values[i], 1E-3,
                $"Parameter[{i}] mismatch for Log-Normal.");
        }
    }

    /// <summary>
    /// Tests GMM parameter recovery for the Log-Pearson Type III distribution.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The Log-Pearson Type III has 3 parameters: mean (μ), standard deviation (σ),
    /// and skewness (γ) of the log10-transformed data. Product moments are computed
    /// on log10(x) values to match the parameter space. This is the standard
    /// distribution for U.S. flood frequency analysis per Bulletin 17C.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void LogPearsonTypeIII_RecoversTrueParameters()
    {
        var (df, trueParameters) = SyntheticUnivariateData.GenerateLogPearsonTypeIIIData();

        // LP-III parameters are moments of log10(x), so compute moments in log-space
        var logValues = df.ExactSeries.ValuesToArray().Select(x => Math.Log10(x)).ToArray();
        var dist = new LogPearsonTypeIII();
        trueParameters = dist.ParametersFromMoments(Statistics.ProductMoments(logValues));

        var model = new Bulletin17CDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);

        var gmm = new GeneralizedMethodOfMoments(model);
        gmm.Estimate();

        Assert.IsTrue(gmm.IsEstimated, "GMM estimation failed for Log-Pearson Type III.");

        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(trueParameters[i], gmm.BestParameterSet.Values[i], 1E-3,
                $"Parameter[{i}] mismatch for Log-Pearson Type III.");
        }
    }
}
