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
/// Programmatic configuration / serialization / property-round-trip tests have moved to
/// <c>RMC.BestFit.Verification/DistributionFitting/FittingAnalysisTests.cs</c>.
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

    #region Constructor (estimation-driven) Tests

    /// <summary>
    /// Tests that the XElement constructor restores configuration after running an MLE fit.
    /// </summary>
    [TestMethod]
    public async Task Constructor_WithXElement_RestoresConfiguration()
    {
        var df = CreateTestDataFrame();
        var original = new FittingAnalysis(df);
        await original.RunAsync();
        var xElement = original.ToXElement();

        var restored = new FittingAnalysis(df, xElement);

        Assert.IsNotNull(restored);
        Assert.AreEqual(original.IsEstimated, restored.IsEstimated);
    }

    /// <summary>
    /// Tests that XML round-trip preserves fitted distributions after MLE estimation.
    /// </summary>
    [TestMethod]
    public async Task XmlRoundTrip_PreservesFittedDistributions()
    {
        var df = CreateTestDataFrame();
        var original = new FittingAnalysis(df);
        await original.RunAsync();

        var xElement = original.ToXElement();
        var restored = new FittingAnalysis(df, xElement);

        Assert.AreEqual(original.FittedDistributions.Count, restored.FittedDistributions.Count);
        for (int i = 0; i < original.FittedDistributions.Count; i++)
        {
            Assert.AreEqual(original.FittedDistributions[i].FitSucceeded, restored.FittedDistributions[i].FitSucceeded);
        }
    }

    #endregion

    #region ClearResults Tests

    /// <summary>
    /// Tests that ClearResults resets IsEstimated.
    /// </summary>
    [TestMethod]
    public async Task ClearResults_ResetsIsEstimated()
    {
        var analysis = CreateTestFittingAnalysis();
        await analysis.RunAsync();
        Assert.IsTrue(analysis.IsEstimated);

        analysis.ClearResults();

        Assert.IsFalse(analysis.IsEstimated);
    }

    /// <summary>
    /// Tests that ClearResults resets FittedDistributions.
    /// </summary>
    [TestMethod]
    public async Task ClearResults_ResetsFittedDistributions()
    {
        var analysis = CreateTestFittingAnalysis();
        await analysis.RunAsync();

        analysis.ClearResults();

        Assert.IsTrue(analysis.FittedDistributions.All(fd => !fd.FitSucceeded));
    }

    /// <summary>
    /// Tests that ClearResults maintains distribution list count.
    /// </summary>
    [TestMethod]
    public async Task ClearResults_MaintainsDistributionCount()
    {
        var analysis = CreateTestFittingAnalysis();
        await analysis.RunAsync();

        analysis.ClearResults();

        Assert.AreEqual(15, analysis.FittedDistributions.Count);
    }

    #endregion

    #region Property Change Tests (estimation-driven)

    /// <summary>
    /// Tests that DataFrame change clears results.
    /// </summary>
    [TestMethod]
    public async Task DataFrame_Change_ClearsResults()
    {
        var analysis = CreateTestFittingAnalysis();
        await analysis.RunAsync();
        Assert.IsTrue(analysis.IsEstimated);

        analysis.DataFrame.ExactSeries.Add(new ExactData(2024, 75000));

        Assert.IsFalse(analysis.IsEstimated, "Changing DataFrame should clear results.");
    }

    /// <summary>
    /// Tests that ProbabilityOrdinates change clears results.
    /// </summary>
    [TestMethod]
    public async Task ProbabilityOrdinates_Change_ClearsResults()
    {
        var analysis = CreateTestFittingAnalysis();
        await analysis.RunAsync();
        Assert.IsTrue(analysis.IsEstimated);

        analysis.ProbabilityOrdinates.Add(0.999);

        Assert.IsFalse(analysis.IsEstimated, "Changing ProbabilityOrdinates should clear results.");
    }

    #endregion

    #region Event Tests

    /// <summary>
    /// Tests that AnalysisStarting event is raised.
    /// </summary>
    [TestMethod]
    public async Task RunAsync_RaisesAnalysisStartingEvent()
    {
        var analysis = CreateTestFittingAnalysis();
        bool eventRaised = false;
        analysis.AnalysisStarting += (s, e) => eventRaised = true;

        await analysis.RunAsync();

        Assert.IsTrue(eventRaised, "AnalysisStarting event should be raised.");
    }

    /// <summary>
    /// Tests that AnalysisCompleted event is raised.
    /// </summary>
    [TestMethod]
    public async Task RunAsync_RaisesAnalysisCompletedEvent()
    {
        var analysis = CreateTestFittingAnalysis();
        bool eventRaised = false;
        analysis.AnalysisCompleted += (s, e) => eventRaised = true;

        await analysis.RunAsync();

        Assert.IsTrue(eventRaised, "AnalysisCompleted event should be raised.");
    }

    /// <summary>
    /// Tests that AnalysisCompleted event indicates success.
    /// </summary>
    [TestMethod]
    public async Task RunAsync_AnalysisCompletedEvent_IndicatesSuccess()
    {
        var analysis = CreateTestFittingAnalysis();
        bool? succeeded = null;
        analysis.AnalysisCompleted += (s, e) => succeeded = e.Succeeded;

        await analysis.RunAsync();

        Assert.IsTrue(succeeded.HasValue && succeeded.Value, "AnalysisCompleted should indicate success.");
    }

    /// <summary>
    /// Tests that canceling in AnalysisStarting prevents analysis.
    /// </summary>
    [TestMethod]
    public async Task RunAsync_CancelInAnalysisStarting_PreventsAnalysis()
    {
        var analysis = CreateTestFittingAnalysis();
        bool wasCanceled = false;
        analysis.AnalysisStarting += (s, e) => e.Cancel = true;
        analysis.AnalysisCompleted += (s, e) => wasCanceled = e.Cancelled;

        await analysis.RunAsync();

        Assert.IsTrue(wasCanceled, "Analysis should be canceled when AnalysisStarting sets Cancel=true.");
        Assert.IsFalse(analysis.IsEstimated, "Analysis should not be estimated when canceled.");
    }

    #endregion

    #region CancelAnalysis Tests (running)

    /// <summary>
    /// Tests that CancelAnalysis can cancel a running analysis.
    /// </summary>
    [TestMethod]
    public async Task CancelAnalysis_WhenRunning_CancelsAnalysis()
    {
        var analysis = CreateTestFittingAnalysis();
        bool wasCanceled = false;
        analysis.AnalysisCompleted += (s, e) => wasCanceled = e.Cancelled;

        var task = analysis.RunAsync();
        analysis.CancelAnalysis();
        await task;

        Assert.IsTrue(true);
    }

    #endregion

    #region FittedDistributions Tests

    /// <summary>
    /// Tests that after fitting, FittedDistributions contain AIC values.
    /// </summary>
    [TestMethod]
    public async Task FittedDistributions_AfterFitting_ContainsAICValues()
    {
        var analysis = CreateTestFittingAnalysis();

        await analysis.RunAsync();

        var successfulFits = analysis.FittedDistributions.Where(fd => fd.FitSucceeded).ToList();
        Assert.IsTrue(successfulFits.Count > 0, "At least one distribution should fit successfully.");
        Assert.IsTrue(successfulFits.All(fd => !double.IsNaN(fd.AIC)), "All successful fits should have finite AIC.");
    }

    /// <summary>
    /// Tests that after fitting, FittedDistributions contain BIC values.
    /// </summary>
    [TestMethod]
    public async Task FittedDistributions_AfterFitting_ContainsBICValues()
    {
        var analysis = CreateTestFittingAnalysis();

        await analysis.RunAsync();

        var successfulFits = analysis.FittedDistributions.Where(fd => fd.FitSucceeded).ToList();
        Assert.IsTrue(successfulFits.All(fd => !double.IsNaN(fd.BIC)), "All successful fits should have finite BIC.");
    }

    /// <summary>
    /// Tests that after fitting, FittedDistributions contain RMSE values.
    /// </summary>
    [TestMethod]
    public async Task FittedDistributions_AfterFitting_ContainsRMSEValues()
    {
        var analysis = CreateTestFittingAnalysis();

        await analysis.RunAsync();

        var successfulFits = analysis.FittedDistributions.Where(fd => fd.FitSucceeded).ToList();
        Assert.IsTrue(successfulFits.All(fd => !double.IsNaN(fd.RMSE) && fd.RMSE >= 0),
            "All successful fits should have non-negative finite RMSE.");
    }

    /// <summary>
    /// Tests that FittedDistributions can be sorted by AIC.
    /// </summary>
    [TestMethod]
    public async Task FittedDistributions_CanBeSortedByAIC()
    {
        var analysis = CreateTestFittingAnalysis();
        await analysis.RunAsync();

        var sortedByAIC = analysis.FittedDistributions
            .Where(fd => fd.FitSucceeded)
            .OrderBy(fd => fd.AIC)
            .ToList();

        Assert.IsTrue(sortedByAIC.Count > 0);
        for (int i = 1; i < sortedByAIC.Count; i++)
        {
            Assert.IsTrue(sortedByAIC[i].AIC >= sortedByAIC[i - 1].AIC,
                "FittedDistributions should be sortable by AIC.");
        }
    }

    #endregion

    #region Edge Cases

    /// <summary>
    /// Tests fitting with minimal data (5 observations).
    /// </summary>
    [TestMethod]
    public async Task FittingAnalysis_MinimalData_FitsAtLeastSomeDistributions()
    {
        var df = CreateTestDataFrame(5);
        var analysis = new FittingAnalysis(df);

        await analysis.RunAsync();

        var successfulFits = analysis.FittedDistributions.Where(fd => fd.FitSucceeded).ToList();
        Assert.IsTrue(successfulFits.Count > 0, "At least one distribution should fit with minimal data.");
    }

    /// <summary>
    /// Tests fitting with large dataset.
    /// </summary>
    [TestMethod]
    public async Task FittingAnalysis_LargeData_FitsAllDistributions()
    {
        var df = new DataFrame();
        var random = new Random(42);
        var largeData = Enumerable.Range(0, 500).Select(i => 30000 + random.NextDouble() * 40000).ToArray();
        df.ExactSeries = new ExactSeries(largeData);
        var analysis = new FittingAnalysis(df);

        await analysis.RunAsync();

        var successfulFits = analysis.FittedDistributions.Where(fd => fd.FitSucceeded).ToList();
        Assert.IsTrue(successfulFits.Count >= 10, "Most distributions should fit with large sample size.");
    }

    /// <summary>
    /// Tests fitting with data containing outliers.
    /// </summary>
    [TestMethod]
    public async Task FittingAnalysis_DataWithOutliers_HandlesCorrectly()
    {
        var df = new DataFrame();
        var dataWithOutlier = SampleAnnualPeaks.Concat(new[] { 150000.0 }).ToArray();
        df.ExactSeries = new ExactSeries(dataWithOutlier);
        var analysis = new FittingAnalysis(df);

        await analysis.RunAsync();

        Assert.IsTrue(analysis.IsEstimated);
        var successfulFits = analysis.FittedDistributions.Where(fd => fd.FitSucceeded).ToList();
        Assert.IsTrue(successfulFits.Count > 0, "Some distributions should handle outliers.");
    }

    /// <summary>
    /// Tests that multiple sequential runs work correctly.
    /// </summary>
    [TestMethod]
    public async Task FittingAnalysis_MultipleRuns_WorksCorrectly()
    {
        var analysis = CreateTestFittingAnalysis();

        await analysis.RunAsync();
        Assert.IsTrue(analysis.IsEstimated);

        analysis.ClearResults();
        await analysis.RunAsync();
        Assert.IsTrue(analysis.IsEstimated);
    }

    #endregion

    #region Integration Tests

    /// <summary>
    /// Tests that Normal distribution is always successfully fitted for normal-like data.
    /// </summary>
    [TestMethod]
    public async Task FittingAnalysis_NormalLikeData_FitsNormalDistribution()
    {
        var analysis = CreateTestFittingAnalysis();

        await analysis.RunAsync();

        var normalFit = analysis.FittedDistributions.FirstOrDefault(fd =>
            fd.Distribution?.Type == UnivariateDistributionType.Normal);
        Assert.IsNotNull(normalFit);
        Assert.IsTrue(normalFit.FitSucceeded, "Normal distribution should fit normal-like data.");
    }

    /// <summary>
    /// Tests that GEV distribution captures heavy tail behavior.
    /// </summary>
    [TestMethod]
    public async Task FittingAnalysis_StandardData_FitsGEVDistribution()
    {
        var analysis = CreateTestFittingAnalysis();

        await analysis.RunAsync();

        var gevFit = analysis.FittedDistributions.FirstOrDefault(fd =>
            fd.Distribution?.Type == UnivariateDistributionType.GeneralizedExtremeValue);
        Assert.IsNotNull(gevFit);
        Assert.IsTrue(gevFit.FitSucceeded, "GEV distribution should fit standard flood data.");
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
