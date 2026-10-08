using System.Collections.Concurrent;
using System.ComponentModel;
using System.Reflection;
using Numerics.Distributions;
using Numerics.Mathematics.Optimization;
using Numerics.Sampling.MCMC;
using RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using RMC.BestFit.Models.TrendFunctions.Support;

namespace RMC.BestFit.Tests.Univariate;

/// <summary>Tests point-estimator reprocessing of stored nonstationary results.</summary>
/// <remarks>Fixtures contain fixed posterior draws; no estimator or sampler runs.</remarks>
[TestClass]
public class UnivariatePointEstimatorChronologyTests
{
    /// <summary>Changing the selected estimate refreshes both point curves without changing uncertainty.</summary>
    /// <returns>A task representing the completed regression check.</returns>
    /// <remarks>The first annual median equals the Normal location intercept, providing an independent oracle.</remarks>
    [TestMethod]
    public async Task PointEstimatorChange_RefreshesChronologyAndPreservesStoredUncertainty()
    {
        var analysis = CreateAnalysis();
        await analysis.CreateFrequencyAnalysisResultsAsync();
        await analysis.CreateChronologyResultsAsync();
        var frequency = analysis.AnalysisResults!;
        var chronology = analysis.ChronologyAnalysisResults!;
        var posterior = analysis.BayesianAnalysis.Results!;
        Assert.IsNotNull(frequency.ConfidenceIntervals);
        Assert.IsNotNull(chronology.ConfidenceIntervals);
        Assert.IsNotNull(frequency.MeanCurve);
        Assert.IsNotNull(chronology.MeanCurve);
        var frequencyBands = frequency.ConfidenceIntervals;
        var chronologyBands = chronology.ConfidenceIntervals;
        var frequencyMean = frequency.MeanCurve;
        var chronologyMean = chronology.MeanCurve;
        var frequencyBandValues = frequencyBands.Cast<double>().ToArray();
        var chronologyBandValues = chronologyBands.Cast<double>().ToArray();
        var frequencyMeanValues = (double[])frequencyMean.Clone();
        var chronologyMeanValues = (double[])chronologyMean.Clone();
        var sampleValues = posterior.Output.SelectMany(p => p.Values).ToArray();
        var originalFrequency = (double[])frequency.ModeCurve!.Clone();
        Assert.AreEqual(17000d, chronology.ModeCurve![0], 1e-9);

        foreach (var (estimator, expected) in new[]
        {
            (BayesianAnalysis.PointEstimateType.PosteriorMode, 16000d),
            (BayesianAnalysis.PointEstimateType.PosteriorMean, 17000d)
        })
        {
            var frequencyChanged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var chronologyChanged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            PropertyChangedEventHandler handler = (_, e) =>
            {
                if (e.PropertyName == nameof(UnivariateAnalysis.AnalysisResults)) frequencyChanged.TrySetResult();
                if (e.PropertyName == nameof(UnivariateAnalysis.ChronologyAnalysisResults)) chronologyChanged.TrySetResult();
            };
            analysis.PropertyChanged += handler;
            try
            {
                analysis.BayesianAnalysis.PointEstimator = estimator;
                await frequencyChanged.Task.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.AreEqual(expected, chronology.ModeCurve[0], 1e-9,
                    "The chronology median must use the selected location intercept.");
                await chronologyChanged.Task.WaitAsync(TimeSpan.FromSeconds(10));
            }
            finally
            {
                analysis.PropertyChanged -= handler;
            }

            Assert.AreSame(frequency, analysis.AnalysisResults);
            Assert.AreSame(chronology, analysis.ChronologyAnalysisResults);
            Assert.AreSame(posterior, analysis.BayesianAnalysis.Results);
            CollectionAssert.AreEqual(sampleValues, posterior.Output.SelectMany(p => p.Values).ToArray());
            Assert.AreSame(frequencyBands, frequency.ConfidenceIntervals);
            Assert.AreSame(chronologyBands, chronology.ConfidenceIntervals);
            Assert.AreSame(frequencyMean, frequency.MeanCurve);
            Assert.AreSame(chronologyMean, chronology.MeanCurve);
            CollectionAssert.AreEqual(frequencyBandValues, frequency.ConfidenceIntervals.Cast<double>().ToArray());
            CollectionAssert.AreEqual(chronologyBandValues, chronology.ConfidenceIntervals.Cast<double>().ToArray());
            CollectionAssert.AreEqual(frequencyMeanValues, frequency.MeanCurve);
            CollectionAssert.AreEqual(chronologyMeanValues, chronology.MeanCurve);
            Assert.IsTrue(analysis.IsEstimated);
            Assert.IsTrue(analysis.BayesianAnalysis.IsEstimated);
            if (estimator == BayesianAnalysis.PointEstimateType.PosteriorMode)
                Assert.AreNotEqual(originalFrequency[0], frequency.ModeCurve[0]);
            else
                CollectionAssert.AreEqual(originalFrequency, frequency.ModeCurve);
        }
    }

    /// <summary>Moving the time index past the record never publishes a chronology whose arrays disagree.</summary>
    /// <returns>A task representing the completed regression check.</returns>
    /// <remarks>
    /// The frequency reprocess ends with the point-estimate refresh, and the chronology reprocess follows it.
    /// A point-estimate curve spanning the new extent cannot pair with the previous extent's intervals, so each
    /// published chronology is checked on its own change notification, as a view reads it.
    /// </remarks>
    [TestMethod]
    public async Task TimeIndexPastRecord_PublishesOnlyAlignedChronologies()
    {
        var analysis = CreateAnalysis();
        await analysis.CreateFrequencyAnalysisResultsAsync();
        await analysis.CreateChronologyResultsAsync();
        int recordLength = analysis.ChronologyAnalysisResults!.ModeCurve!.Length;
        var misaligned = new ConcurrentQueue<string>();
        var extended = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        PropertyChangedEventHandler handler = (_, e) =>
        {
            if (e.PropertyName != nameof(UnivariateAnalysis.ChronologyAnalysisResults)) return;
            var results = analysis.ChronologyAnalysisResults;
            if (results == null) return;
            int length = results.ModeCurve?.Length ?? -1;
            if (results.MeanCurve?.Length != length || results.ConfidenceIntervals?.GetLength(0) != length)
                misaligned.Enqueue($"mode {length}, mean {results.MeanCurve?.Length}, intervals {results.ConfidenceIntervals?.GetLength(0)}");
            else if (length > recordLength)
                extended.TrySetResult();
        };
        analysis.PropertyChanged += handler;
        try
        {
            analysis.UnivariateDistribution.ParameterTimeIndex = 2015;
            await extended.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            analysis.PropertyChanged -= handler;
        }

        Assert.IsTrue(misaligned.IsEmpty, "Published misaligned chronologies: " + string.Join("; ", misaligned));
    }

    /// <summary>A chronology rebuild never exposes results whose mean and intervals are still being computed.</summary>
    /// <returns>A task representing the completed regression check.</returns>
    /// <remarks>
    /// Views redraw the chronology from change notifications that can arrive while a rebuild runs, so the
    /// property must hold either no results or complete results. Many stored draws keep the rebuild long
    /// enough for the polling loop to observe any intermediate state.
    /// </remarks>
    [TestMethod]
    public async Task ChronologyRebuild_NeverExposesIncompleteResults()
    {
        var analysis = CreateAnalysis(draws: 8000);
        bool sawIncomplete = false;
        var rebuild = analysis.CreateChronologyResultsAsync();
        while (!rebuild.IsCompleted)
        {
            var results = analysis.ChronologyAnalysisResults;
            if (results != null && (results.MeanCurve == null || results.ConfidenceIntervals == null))
                sawIncomplete = true;
        }
        await rebuild;

        Assert.IsFalse(sawIncomplete, "The chronology was visible before its mean and intervals were set.");
        Assert.IsNotNull(analysis.ChronologyAnalysisResults?.ConfidenceIntervals);
    }

    /// <summary>Builds an estimated linear-location Normal model using fixed stored posterior draws.</summary>
    /// <param name="draws">The number of stored posterior draws, which is also the output length.</param>
    /// <returns>The injected analysis ready for deterministic output processing.</returns>
    /// <remarks>The synthetic posterior mean and MAP differ by construction.</remarks>
    private static UnivariateAnalysis CreateAnalysis(int draws = 100)
    {
        double[] values = [12500, 15300, 8900, 22100, 18700, 14200, 9800, 28500, 17400, 11600,
            19200, 13800, 25600, 10500, 16900, 21300, 14700, 8200, 23800, 15900];
        var frame = new RMC.BestFit.Models.DataFrame();
        for (int i = 0; i < values.Length; i++) frame.ExactSeries.Add(new ExactData(1990 + i, values[i]));
        frame.CalculatePlottingPositions();
        var model = new UnivariateDistribution(frame, UnivariateDistributionType.Normal) { IsNonstationary = true };
        model.SetTrendModel(0, TrendModelType.Linear);
        var analysis = new UnivariateAnalysis(model);
        analysis.BayesianAnalysis.OutputLength = draws;
        var output = Enumerable.Range(0, draws)
            .Select(_ => new ParameterSet(new[] { 17000d, 80d, 6000d }, 0d)).ToList();
        analysis.BayesianAnalysis.SetCustomMCMCResults(
            new MCMCResults(new ParameterSet(new[] { 16000d, 50d, 5000d }, 0d), output, alpha: 0.10),
            skipInformationCriteria: true);
        typeof(AnalysisBase).GetField("_isEstimated", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(analysis, true);
        return analysis;
    }
}
