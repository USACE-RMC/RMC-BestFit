using Numerics;
using Numerics.Data.Statistics;
using Numerics.Distributions;
using Numerics.Utilities;
using RMC.BestFit.Analyses;
using RMC.BestFit.Models;
using RMC.BestFit.Verification.Datasets.UnivariateData;

namespace RMC.BestFit.Verification.DistributionFitting;

/// <summary>
/// Computational verification tests for the <see cref="FittingAnalysis"/> class.
/// Holds only tests that drive the MLE pipeline via <see cref="FittingAnalysis.RunAsync"/>.
/// Fast behavior and regression tests live in
/// <c>RMC.BestFit.Tests/DistributionFitting/FittingAnalysisRegressionTests.cs</c>.
/// </summary>
/// <remarks>
/// <para>
///     <b>Authors:</b>
/// </para>
/// <list type="bullet">
///     <item>Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil</item>
/// </list>
/// <para>
/// The <see cref="FittingAnalysis"/> class performs automated distribution fitting using
/// maximum likelihood estimation (MLE) for all 15 supported univariate distributions.
/// It computes AIC, BIC, and RMSE metrics for model comparison.
/// </para>
/// </remarks>
[TestClass]
public class FittingAnalysisTests
{
    #region Test Data Helpers

    /// <summary>
    /// Sample annual peak flow data (cfs) for testing.
    /// </summary>
    private static readonly double[] SampleAnnualPeaks =
    [
        45000, 38000, 52000, 61000, 33000, 49000, 55000, 42000, 67000, 39000,
        48000, 51000, 36000, 58000, 44000, 53000, 47000, 62000, 41000, 50000,
        37000, 54000, 46000, 59000, 43000, 56000, 40000, 63000, 35000, 57000
    ];

    /// <summary>
    /// Creates a test DataFrame with exact data.
    /// </summary>
    private static DataFrame CreateTestDataFrame(int count = 30)
    {
        var df = new DataFrame();
        var data = SampleAnnualPeaks.Take(count).ToArray();
        df.ExactSeries = new ExactSeries(data);
        return df;
    }

    /// <summary>
    /// Creates a test FittingAnalysis with sample data.
    /// </summary>
    private static FittingAnalysis CreateTestFittingAnalysis()
    {
        var df = CreateTestDataFrame();
        return new FittingAnalysis(df);
    }

    #endregion

    #region Verification Tests Against Published Results

    /// <summary>
    /// Tests <see cref="FittingAnalysis"/> using Wabash River at Lafayette, IN data.
    /// </summary>
    /// <remarks>
    /// Validates Normal, Log-Normal, Ln-Normal, and Exponential distributions.
    /// Reference: Rao &amp; Hamed (2000), Table 1.8.1.
    /// </remarks>
    [TestMethod]
    public async Task Test_FittingAnalysis_WabashRiverData()
    {
        var (df, _, _) = VerificationData.Test_Exponential_MLE();

        var fittingAnalysis = new FittingAnalysis(df);
        await fittingAnalysis.RunAsync();

        var normalFittedDist = fittingAnalysis.FittedDistributions.FirstOrDefault(fd => fd.Distribution!.Type == UnivariateDistributionType.Normal);
        var logNormalFittedDist = fittingAnalysis.FittedDistributions.FirstOrDefault(fd => fd.Distribution!.Type == UnivariateDistributionType.LogNormal);
        var lnNormalFittedDist = fittingAnalysis.FittedDistributions.FirstOrDefault(fd => fd.Distribution!.Type == UnivariateDistributionType.LnNormal);
        var exponentialFittedDist = fittingAnalysis.FittedDistributions.FirstOrDefault(fd => fd.Distribution!.Type == UnivariateDistributionType.Exponential);

        Assert.AreEqual(true, normalFittedDist?.FitSucceeded, "Normal distribution fitting failed.");
        Assert.AreEqual(true, logNormalFittedDist?.FitSucceeded, "Log-Normal distribution fitting failed.");
        Assert.AreEqual(true, lnNormalFittedDist?.FitSucceeded, "Ln-Normal distribution fitting failed.");
        Assert.AreEqual(true, exponentialFittedDist?.FitSucceeded, "Exponential distribution fitting failed.");

        var normalDist = (Normal)normalFittedDist!.Distribution!;
        double trueMu = Statistics.Mean(df.ExactSeries!.ValuesToArray());
        double trueSigma = Statistics.PopulationStandardDeviation(df.ExactSeries.ValuesToArray());
        Assert.AreEqual(trueMu, normalDist.Mu, Math.Abs(trueMu * 0.01), "Normal distribution mean parameter is incorrect.");
        Assert.AreEqual(trueSigma, normalDist.Sigma, Math.Abs(trueSigma * 0.01), "Normal distribution standard deviation parameter is incorrect.");

        var logNormalDist = (LogNormal)logNormalFittedDist!.Distribution!;
        var (_, trueLogMu, trueLogSigma) = VerificationData.Test_Log10Normal_MLE();
        Assert.AreEqual(trueLogMu, logNormalDist.Mu, Math.Abs(trueLogMu * 0.01), "Log-Normal distribution mean parameter is incorrect.");
        Assert.AreEqual(trueLogSigma, logNormalDist.Sigma, Math.Abs(trueLogSigma * 0.01), "Log-Normal distribution standard deviation parameter is incorrect.");

        var lnNormalDist = (LnNormal)lnNormalFittedDist!.Distribution!;
        var (__, trueLnMu, trueLnSigma) = VerificationData.Test_LnNormal_MLE();
        Assert.AreEqual(trueLnMu, lnNormalDist.Mu, Math.Abs(trueLnMu * 0.01), "Ln-Normal distribution mean parameter is incorrect.");
        Assert.AreEqual(trueLnSigma, lnNormalDist.Sigma, Math.Abs(trueLnSigma * 0.01), "Ln-Normal distribution standard deviation parameter is incorrect.");

        var exponentialDist = (Exponential)exponentialFittedDist!.Distribution!;
        var (_, trueExpXi, trueExpAlpha) = VerificationData.Test_Exponential_MLE();
        Assert.AreEqual(trueExpXi, exponentialDist.Xi, Math.Abs(trueExpXi * 0.01), "Exponential distribution location parameter is incorrect.");
        Assert.AreEqual(trueExpAlpha, exponentialDist.Alpha, Math.Abs(trueExpAlpha * 0.01), "Exponential distribution scale parameter is incorrect.");
    }

    /// <summary>
    /// Tests <see cref="FittingAnalysis"/> using Tippecanoe River near Delphi, IN data.
    /// </summary>
    /// <remarks>
    /// Validates Normal, Log-Normal, Ln-Normal, Logistic, and Weibull distributions.
    /// Reference: Rao &amp; Hamed (2000), Table 5.1.1.
    /// </remarks>
    [TestMethod]
    public async Task Test_FittingAnalysis_TippecanoeRiverData()
    {
        var (df, _, _) = VerificationData.Test_Normal_MLE();

        var fittingAnalysis = new FittingAnalysis(df);
        await fittingAnalysis.RunAsync();

        var normalFittedDist = fittingAnalysis.FittedDistributions.FirstOrDefault(fd => fd.Distribution!.Type == UnivariateDistributionType.Normal);
        var logNormalFittedDist = fittingAnalysis.FittedDistributions.FirstOrDefault(fd => fd.Distribution!.Type == UnivariateDistributionType.LogNormal);
        var lnNormalFittedDist = fittingAnalysis.FittedDistributions.FirstOrDefault(fd => fd.Distribution!.Type == UnivariateDistributionType.LnNormal);
        var logisticFittedDist = fittingAnalysis.FittedDistributions.FirstOrDefault(fd => fd.Distribution!.Type == UnivariateDistributionType.Logistic);
        var weibullFittedDist = fittingAnalysis.FittedDistributions.FirstOrDefault(fd => fd.Distribution!.Type == UnivariateDistributionType.Weibull);

        Assert.AreEqual(true, normalFittedDist?.FitSucceeded, "Normal distribution fitting failed.");
        Assert.AreEqual(true, logNormalFittedDist?.FitSucceeded, "Log-Normal distribution fitting failed.");
        Assert.AreEqual(true, lnNormalFittedDist?.FitSucceeded, "Ln-Normal distribution fitting failed.");
        Assert.AreEqual(true, logisticFittedDist?.FitSucceeded, "Logistic distribution fitting failed.");
        Assert.AreEqual(true, weibullFittedDist?.FitSucceeded, "Weibull distribution fitting failed.");

        var normalDist = (Normal)normalFittedDist!.Distribution!;
        var (_, trueMu, trueSigma) = VerificationData.Test_Normal_MLE();
        Assert.AreEqual(trueMu, normalDist.Mu, Math.Abs(trueMu * 0.01), "Normal distribution mean parameter is incorrect.");
        Assert.AreEqual(trueSigma, normalDist.Sigma, Math.Abs(trueSigma * 0.01), "Normal distribution standard deviation parameter is incorrect.");

        var logNormalDist = (LogNormal)logNormalFittedDist!.Distribution!;
        double trueLogMu = Statistics.Mean(df.ExactSeries!.ValuesToArray().Map(Math.Log10));
        double trueLogSigma = Statistics.PopulationStandardDeviation(df.ExactSeries.ValuesToArray().Map(Math.Log10));
        Assert.AreEqual(trueLogMu, logNormalDist.Mu, Math.Abs(trueLogMu * 0.01), "Log-Normal distribution mean parameter is incorrect.");
        Assert.AreEqual(trueLogSigma, logNormalDist.Sigma, Math.Abs(trueLogSigma * 0.01), "Log-Normal distribution standard deviation parameter is incorrect.");

        var lnNormalDist = (LnNormal)lnNormalFittedDist!.Distribution!;
        double trueLnMu = Statistics.Mean(df.ExactSeries.ValuesToArray().Map(Math.Log));
        double trueLnSigma = Statistics.PopulationStandardDeviation(df.ExactSeries.ValuesToArray().Map(Math.Log));
        Assert.AreEqual(trueLnMu, lnNormalDist.Mu, Math.Abs(trueLnMu * 0.01), "Ln-Normal distribution mean parameter is incorrect.");
        Assert.AreEqual(trueLnSigma, lnNormalDist.Sigma, Math.Abs(trueLnSigma * 0.01), "Ln-Normal distribution standard deviation parameter is incorrect.");

        var logisticDist = (Logistic)logisticFittedDist!.Distribution!;
        var (__, trueLogXi, trueLogAlpha) = VerificationData.Test_Logistic_MLE();
        Assert.AreEqual(trueLogXi, logisticDist.Xi, Math.Abs(trueLogXi * 0.01), "Logistic distribution location parameter is incorrect.");
        Assert.AreEqual(trueLogAlpha, logisticDist.Alpha, Math.Abs(trueLogAlpha * 0.01), "Logistic distribution scale parameter is incorrect.");

        var weibullDist = (Weibull)weibullFittedDist!.Distribution!;
        var (___, trueWeibullScale, trueWeibullShape) = VerificationData.Test_Weibull_MLE();
        Assert.AreEqual(trueWeibullScale, weibullDist.Lambda, Math.Abs(trueWeibullScale * 0.01), "Weibull distribution scale parameter is incorrect.");
        Assert.AreEqual(trueWeibullShape, weibullDist.Kappa, Math.Abs(trueWeibullShape * 0.01), "Weibull distribution shape parameter is incorrect.");
    }

    /// <summary>
    /// Tests <see cref="FittingAnalysis"/> using Harricana River at Amos, Quebec, Canada data.
    /// </summary>
    /// <remarks>
    /// Validates Gamma, Pearson Type III, and Log-Pearson Type III distributions.
    /// Reference: Bobee &amp; Ashkar (1991), Table 1.2.
    /// </remarks>
    [TestMethod]
    public async Task Test_FittingAnalysis_HarricanaRiverData()
    {
        var (df, _, _, _) = VerificationData.Test_PearsonTypeIII_MLE();

        var fittingAnalysis = new FittingAnalysis(df);
        await fittingAnalysis.RunAsync();

        var gammaFittedDist = fittingAnalysis.FittedDistributions.FirstOrDefault(fd => fd.Distribution!.Type == UnivariateDistributionType.GammaDistribution);
        var pearsonType3FittedDist = fittingAnalysis.FittedDistributions.FirstOrDefault(fd => fd.Distribution!.Type == UnivariateDistributionType.PearsonTypeIII);
        var logPearsonType3FittedDist = fittingAnalysis.FittedDistributions.FirstOrDefault(fd => fd.Distribution!.Type == UnivariateDistributionType.LogPearsonTypeIII);

        Assert.AreEqual(true, gammaFittedDist?.FitSucceeded, "Gamma distribution fitting failed.");
        Assert.AreEqual(true, pearsonType3FittedDist?.FitSucceeded, "Pearson Type III distribution fitting failed.");
        Assert.AreEqual(true, logPearsonType3FittedDist?.FitSucceeded, "Log-Pearson Type III distribution fitting failed.");

        var gammaDist = (GammaDistribution)gammaFittedDist!.Distribution!;
        var (_, trueGammaTheta, trueGammaKappa) = VerificationData.Test_GammaDist_MLE();
        Assert.AreEqual(trueGammaTheta, gammaDist.Theta, Math.Abs(trueGammaTheta * 0.01), "Gamma distribution scale parameter is incorrect.");
        Assert.AreEqual(trueGammaKappa, gammaDist.Kappa, Math.Abs(trueGammaKappa * 0.01), "Gamma distribution shape parameter is incorrect.");

        var pearsonTypeIIIDist = (PearsonTypeIII)pearsonType3FittedDist!.Distribution!;
        var (__, truePT3Mu, truePT3Sigma, truePT3Gamma) = VerificationData.Test_PearsonTypeIII_MLE();
        Assert.AreEqual(truePT3Mu, pearsonTypeIIIDist.Mu, Math.Abs(truePT3Mu * 0.01), "Pearson Type III distribution mean parameter is incorrect.");
        Assert.AreEqual(truePT3Sigma, pearsonTypeIIIDist.Sigma, Math.Abs(truePT3Sigma * 0.01), "Pearson Type III distribution standard deviation parameter is incorrect.");
        Assert.AreEqual(truePT3Gamma, pearsonTypeIIIDist.Gamma, Math.Abs(truePT3Gamma * 0.01), "Pearson Type III distribution skewness parameter is incorrect.");

        var logPearsonTypeIIIDist = (LogPearsonTypeIII)logPearsonType3FittedDist!.Distribution!;
        var (___, trueLP3Mu, trueLP3Sigma, trueLP3Gamma) = VerificationData.Test_LogPearsonTypeIII_MLE();
        Assert.AreEqual(trueLP3Mu, logPearsonTypeIIIDist.Mu, Math.Abs(trueLP3Mu * 0.01), "Log-Pearson Type III distribution mean parameter is incorrect.");
        Assert.AreEqual(trueLP3Sigma, logPearsonTypeIIIDist.Sigma, Math.Abs(trueLP3Sigma * 0.01), "Log-Pearson Type III distribution standard deviation parameter is incorrect.");
        Assert.AreEqual(trueLP3Gamma, logPearsonTypeIIIDist.Gamma, Math.Abs(trueLP3Gamma * 0.01), "Log-Pearson Type III distribution skewness parameter is incorrect.");
    }

    /// <summary>
    /// Tests <see cref="FittingAnalysis"/> using Sugar Creek at Crawfordsville, IN data.
    /// </summary>
    /// <remarks>
    /// Validates Gumbel distribution.
    /// Reference: Rao &amp; Hamed (2000), Table 7.2.1, Example 7.2.1, page 234.
    /// </remarks>
    [TestMethod]
    public async Task Test_FittingAnalysis_SugarCreekData()
    {
        var (df, _, _) = VerificationData.Test_Gumbel_MLE();

        var fittingAnalysis = new FittingAnalysis(df);
        await fittingAnalysis.RunAsync();

        var gumbelFittedDist = fittingAnalysis.FittedDistributions.FirstOrDefault(fd => fd.Distribution!.Type == UnivariateDistributionType.Gumbel);

        Assert.AreEqual(true, gumbelFittedDist?.FitSucceeded, "Gumbel distribution fitting failed.");

        var gumbelDist = (Gumbel)gumbelFittedDist!.Distribution!;
        var (_, trueGumbelLocation, trueGumbelScale) = VerificationData.Test_Gumbel_MLE();
        Assert.AreEqual(trueGumbelLocation, gumbelDist.Xi, Math.Abs(trueGumbelLocation * 0.01), "Gumbel distribution location parameter is incorrect.");
        Assert.AreEqual(trueGumbelScale, gumbelDist.Alpha, Math.Abs(trueGumbelScale * 0.01), "Gumbel distribution scale parameter is incorrect.");
    }

    /// <summary>
    /// Tests <see cref="FittingAnalysis"/> using White River near Nora, IN data.
    /// </summary>
    /// <remarks>
    /// Validates Generalized Extreme Value (GEV) distribution.
    /// Reference: Rao &amp; Hamed (2000), Table 7.1.2, Example 7.1.1, page 219.
    /// </remarks>
    [TestMethod]
    public async Task Test_FittingAnalysis_WhiteRiverData()
    {
        var (df, _, _, _) = VerificationData.Test_GEV_MLE();

        var fittingAnalysis = new FittingAnalysis(df);
        await fittingAnalysis.RunAsync();

        var gevFittedDist = fittingAnalysis.FittedDistributions.FirstOrDefault(fd => fd.Distribution!.Type == UnivariateDistributionType.GeneralizedExtremeValue);

        Assert.AreEqual(true, gevFittedDist?.FitSucceeded, "GEV distribution fitting failed.");

        var gevDist = (GeneralizedExtremeValue)gevFittedDist!.Distribution!;
        var (_, trueGEVLocation, trueGEVScale, trueGEVShape) = VerificationData.Test_GEV_MLE();
        Assert.AreEqual(trueGEVLocation, gevDist.Xi, Math.Abs(trueGEVLocation * 0.01), "GEV distribution location parameter is incorrect.");
        Assert.AreEqual(trueGEVScale, gevDist.Alpha, Math.Abs(trueGEVScale * 0.01), "GEV distribution scale parameter is incorrect.");
        Assert.AreEqual(trueGEVShape, gevDist.Kappa, 0.001, "GEV distribution shape parameter is incorrect.");
    }

    /// <summary>
    /// Tests <see cref="FittingAnalysis"/> using White River at Mt. Carmel, IN data.
    /// </summary>
    /// <remarks>
    /// Validates Generalized Pareto distribution for peaks-over-threshold analysis.
    /// Reference: Rao &amp; Hamed (2000), Table 8.3.1, Example 8.3.1, page 279.
    /// </remarks>
    [TestMethod]
    public async Task Test_FittingAnalysis_WhiteRiverAtMtCarmelData()
    {
        var (df, _, _, _) = VerificationData.Test_GeneralizedPareto_MLE();

        var fittingAnalysis = new FittingAnalysis(df);
        await fittingAnalysis.RunAsync();

        var gpFittedDist = fittingAnalysis.FittedDistributions.FirstOrDefault(fd => fd.Distribution!.Type == UnivariateDistributionType.GeneralizedPareto);

        Assert.AreEqual(true, gpFittedDist?.FitSucceeded, "Generalized Pareto distribution fitting failed.");

        var gpDist = (GeneralizedPareto)gpFittedDist!.Distribution!;
        var (_, trueGPLocation, trueGPScale, trueGPShape) = VerificationData.Test_GeneralizedPareto_MLE();
        Assert.AreEqual(trueGPLocation, gpDist.Xi, Math.Abs(trueGPLocation * 0.01), "Generalized Pareto distribution location parameter is incorrect.");
        Assert.AreEqual(trueGPScale, gpDist.Alpha, Math.Abs(trueGPScale * 0.01), "Generalized Pareto distribution scale parameter is incorrect.");
        Assert.AreEqual(trueGPShape, gpDist.Kappa, Math.Abs(trueGPShape * 0.01), "Generalized Pareto distribution shape parameter is incorrect.");
    }

    /// <summary>
    /// Tests <see cref="FittingAnalysis"/> using Air Quality wind data from R.
    /// </summary>
    /// <remarks>
    /// Validates Generalized Normal and Kappa-4 distributions.
    /// Reference: R lmom package.
    /// </remarks>
    [TestMethod]
    public async Task Test_FittingAnalysis_AirQualityWindData()
    {
        var (df, _, _, _) = VerificationData.Test_GeneralizedNormal_MLE();

        var fittingAnalysis = new FittingAnalysis(df);
        await fittingAnalysis.RunAsync();

        var genNormalFittedDist = fittingAnalysis.FittedDistributions.FirstOrDefault(fd => fd.Distribution!.Type == UnivariateDistributionType.GeneralizedNormal);
        var kappa4FittedDist = fittingAnalysis.FittedDistributions.FirstOrDefault(fd => fd.Distribution!.Type == UnivariateDistributionType.KappaFour);

        Assert.AreEqual(true, genNormalFittedDist?.FitSucceeded, "Generalized Normal distribution fitting failed.");
        Assert.AreEqual(true, kappa4FittedDist?.FitSucceeded, "Kappa-4 distribution fitting failed.");

        var genNormalDist = (GeneralizedNormal)genNormalFittedDist!.Distribution!;
        var (_, trueGNLocation, trueGNScale, trueGNShape) = VerificationData.Test_GeneralizedNormal_MLE();
        Assert.AreEqual(trueGNLocation, genNormalDist.Xi, Math.Abs(trueGNLocation * 0.01), "Generalized Normal distribution location parameter is incorrect.");
        Assert.AreEqual(trueGNScale, genNormalDist.Alpha, Math.Abs(trueGNScale * 0.01), "Generalized Normal distribution scale parameter is incorrect.");
        Assert.AreEqual(trueGNShape, genNormalDist.Kappa, Math.Abs(trueGNShape * 0.1), "Generalized Normal distribution shape parameter is incorrect.");

        var kappa4Dist = (KappaFour)kappa4FittedDist!.Distribution!;
        var (__, trueK4Location, trueK4Scale, trueK4Shape, trueK4Shape2) = VerificationData.Test_Kappa4_MLE();
        Assert.AreEqual(trueK4Location, kappa4Dist.Xi, Math.Abs(trueK4Location * 0.1), "Kappa-4 distribution location parameter is incorrect.");
        Assert.AreEqual(trueK4Scale, kappa4Dist.Alpha, Math.Abs(trueK4Scale * 0.1), "Kappa-4 distribution scale parameter is incorrect.");
        Assert.AreEqual(trueK4Shape, kappa4Dist.Kappa, 0.05, "Kappa-4 distribution shape parameter is incorrect.");
        Assert.AreEqual(trueK4Shape2, kappa4Dist.Hondo, 0.05, "Kappa-4 distribution shape2 parameter is incorrect.");
    }

    /// <summary>
    /// Tests <see cref="FittingAnalysis"/> using East Fork White River at Seymour, IN data.
    /// </summary>
    /// <remarks>
    /// Validates Generalized Logistic distribution.
    /// Reference: Rao &amp; Hamed (2000), Table 9.2.1, Example 9.1.1, page 295.
    /// Note: Results validated within 10% tolerance due to discrepancies between published summary statistics and actual dataset.
    /// </remarks>
    [TestMethod]
    public async Task Test_FittingAnalysis_EastForkWhiteRiverData()
    {
        var (df, _, _, _) = VerificationData.Test_GeneralizedLogistic_MLE();

        var fittingAnalysis = new FittingAnalysis(df);
        await fittingAnalysis.RunAsync();

        var genLogisticFittedDist = fittingAnalysis.FittedDistributions.FirstOrDefault(fd => fd.Distribution!.Type == UnivariateDistributionType.GeneralizedLogistic);

        Assert.AreEqual(true, genLogisticFittedDist?.FitSucceeded, "Generalized Logistic distribution fitting failed.");

        var genLogisticDist = (GeneralizedLogistic)genLogisticFittedDist!.Distribution!;
        var (_, trueGLLocation, trueGLScale, trueGLShape) = VerificationData.Test_GeneralizedLogistic_MLE();
        Assert.AreEqual(trueGLLocation, genLogisticDist.Xi, Math.Abs(trueGLLocation * 0.1), "Generalized Logistic distribution location parameter is incorrect.");
        Assert.AreEqual(trueGLScale, genLogisticDist.Alpha, Math.Abs(trueGLScale * 0.1), "Generalized Logistic distribution scale parameter is incorrect.");
        Assert.AreEqual(trueGLShape, genLogisticDist.Kappa, Math.Abs(trueGLShape * 0.1), "Generalized Logistic distribution shape parameter is incorrect.");
    }

    #endregion
}
