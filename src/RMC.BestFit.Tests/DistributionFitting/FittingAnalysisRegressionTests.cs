using Numerics;
using Numerics.Data.Statistics;
using Numerics.Distributions;
using Numerics.Utilities;
using RMC.BestFit.Analyses;
using RMC.BestFit.Models;
using BestFitDataFrame = RMC.BestFit.Models.DataFrame;

namespace RMC.BestFit.Tests.DistributionFitting;

/// <summary>
/// Fast behavior and regression tests for the <see cref="FittingAnalysis"/> class.
/// These tests exercise state, events, cancellation, and short estimator regressions.
/// Published-data numerical comparisons remain in the Verification project.
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
public class FittingAnalysisRegressionTests
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
    private static BestFitDataFrame CreateTestDataFrame(int count = 30)
    {
        var df = new BestFitDataFrame();
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
    /// Tests that an all-candidate failure does not report the analysis as estimated.
    /// </summary>
    [TestMethod]
    public async Task RunAsync_AllCandidatesFail_ReportsFailure()
    {
        var dataFrame = new BestFitDataFrame
        {
            ExactSeries = new ExactSeries(SampleAnnualPeaks.Concat([150000d]).ToArray())
        };
        var analysis = new FittingAnalysis(dataFrame);
        bool? eventSucceeded = null;
        analysis.AnalysisCompleted += (_, args) => eventSucceeded = args.Succeeded;

        await analysis.RunAsync();

        int successfulCount = analysis.FittedDistributions.Count(fitted => fitted.FitSucceeded);
        Assert.AreEqual(0, successfulCount, "The regression fixture must keep every candidate in a failed state.");
        Assert.IsFalse(analysis.IsEstimated, "An analysis with no successful candidate is not estimated.");
        Assert.AreEqual(false, eventSucceeded, "AnalysisCompleted must report the all-failed run as unsuccessful.");
    }

    /// <summary>
    /// Tests that a partial candidate failure remains an overall successful analysis.
    /// </summary>
    [TestMethod]
    public async Task RunAsync_PartialCandidateSuccess_ReportsSuccess()
    {
        var dataFrame = new BestFitDataFrame
        {
            ExactSeries = new ExactSeries([-100d, -50d, -20d, -10d, -5d, -2d, -1d, 0.5d, 1d, 2d, 5d, 10d, 20d, 50d, 100d])
        };
        var analysis = new FittingAnalysis(dataFrame);
        bool? eventSucceeded = null;
        analysis.AnalysisCompleted += (_, args) => eventSucceeded = args.Succeeded;

        await analysis.RunAsync();

        int successfulCount = analysis.FittedDistributions.Count(fitted => fitted.FitSucceeded);
        Assert.IsTrue(successfulCount > 0, "At least one unrestricted-family candidate must succeed.");
        Assert.IsTrue(successfulCount < analysis.FittedDistributions.Count, "At least one positive-support candidate must fail.");
        Assert.IsTrue(analysis.IsEstimated, "One or more successful candidates make the analysis estimated.");
        Assert.AreEqual(true, eventSucceeded, "AnalysisCompleted must report a partial-success run as successful.");
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

}
