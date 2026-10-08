using Microsoft.VisualStudio.TestTools.UnitTesting;
using Numerics.Data;
using Numerics.Distributions;
using Numerics.Distributions.Copulas;
using Numerics.Mathematics.Optimization;
using Numerics.Sampling.MCMC;
using RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using RMC.BestFit.Models.SpatialExtremes;
using RMC.BestFit.Models.TrendFunctions;
using System.ComponentModel;
using System.Reflection;
using PointEstimator = RMC.BestFit.Estimation.BayesianAnalysis.PointEstimateType;
using BestFitDataFrame = RMC.BestFit.Models.DataFrame;
using NumericsTimeSeries = Numerics.Data.TimeSeries;

namespace RMC.BestFit.Tests.Analyses;

/// <summary>Checks retained-fit point-estimator round trips across the remaining headless analysis families.</summary>
/// <remarks>All posteriors and uncertainty arrays are restored inline. No optimizer, MCMC, or uncertainty simulation runs.</remarks>
[TestClass]
public sealed class RetainedPointEstimatorContractTests
{
    /// <summary>Checks parameters and predictions for each time-series analysis, including an ARIMAX covariate.</summary>
    /// <param name="family">The time-series family to instantiate.</param>
    /// <returns>A task representing deterministic postprocessing and contract checks.</returns>
    [DataTestMethod]
    [DataRow("AR")]
    [DataRow("MA")]
    [DataRow("ARIMA")]
    [DataRow("ARIMAX")]
    public async Task TimeSeries_MeanModeMean_PreservesPosteriorAndUncertainty(string family)
    {
        NumericsTimeSeries source = CreateSource();
        AnalysisBase analysis;
        IModel model;
        BayesianAnalysis bayesian;
        Func<Task> update;
        Func<UncertaintyAnalysisResults> results;
        int forecast = family == "ARIMAX" ? 0 : 2;
        switch (family)
        {
            case "AR":
                var arModel = new AutoRegressive(source, 1, true);
                var ar = new ARAnalysis(arModel) { ForecastingTimeSteps = forecast };
                (analysis, model, bayesian, update, results) = (ar, arModel, ar.BayesianAnalysis,
                    ar.UpdatePointEstimateResultsAsync, () => ar.AnalysisResults!);
                break;
            case "MA":
                var maModel = new MovingAverage(source, 1, true);
                var ma = new MAAnalysis(maModel) { ForecastingTimeSteps = forecast };
                (analysis, model, bayesian, update, results) = (ma, maModel, ma.BayesianAnalysis,
                    ma.UpdatePointEstimateResultsAsync, () => ma.AnalysisResults!);
                break;
            case "ARIMA":
                var arimaModel = new ARIMA(source, 1, 0, 1, true);
                var arima = new ARIMAAnalysis(arimaModel) { ForecastingTimeSteps = forecast };
                (analysis, model, bayesian, update, results) = (arima, arimaModel, arima.BayesianAnalysis,
                    arima.UpdatePointEstimateResultsAsync, () => arima.AnalysisResults!);
                break;
            case "ARIMAX":
                var arimaxModel = new ARIMAX(source) { AROrderP = 1, MAOrderQ = 1 };
                var covariate = source.Clone();
                for (int i = 0; i < covariate.Count; i++) covariate[i].Value = 10 + Math.Sin(i);
                arimaxModel.SetCovariates(new List<NumericsTimeSeries> { covariate });
                arimaxModel.CovariateExtension = ARIMAX.CovariateExtensionMethod.None;
                var arimax = new ARIMAXAnalysis(arimaxModel) { ForecastingTimeSteps = forecast };
                (analysis, model, bayesian, update, results) = (arimax, arimaxModel, arimax.BayesianAnalysis,
                    arimax.UpdatePointEstimateResultsAsync, () => arimax.AnalysisResults!);
                break;
            default:
                throw new ArgumentException("Unknown time-series family.", nameof(family));
        }
        MCMCResults posterior = CreatePosterior(model.Parameters.Select(parameter => parameter.Value).ToArray());
        bayesian.SetCustomMCMCResults(posterior, skipInformationCriteria: true);
        RestoreResults(analysis, source.Count + forecast);
        Assert.IsTrue(analysis.Validate().IsValid, "The complete source and covariate must satisfy model validation.");
        await AssertRoundTripAsync(analysis, bayesian, model, update, results,
            () => new[] { bayesian.Results! }, null);
    }

    /// <summary>Checks competing-risk component parameters and point quantiles while retaining the fitted ensemble.</summary>
    /// <returns>A task representing the deterministic round trip.</returns>
    [TestMethod]
    public async Task CompetingRisk_MeanModeMean_PreservesPosteriorAndUncertainty()
    {
        var frame = new BestFitDataFrame();
        var distribution = new Gumbel(15000, 4000);
        for (int i = 0; i < 30; i++)
            frame.ExactSeries.Add(new ExactData(1990 + i, distribution.InverseCDF((i + 0.5) / 30)));
        frame.CalculatePlottingPositions();
        var model = new CompetingRisksModel(frame,
            new List<UnivariateDistributionType> { UnivariateDistributionType.Gumbel, UnivariateDistributionType.Gumbel });
        var analysis = new CompetingRiskAnalysis(model);
        SetProbabilities(analysis.ProbabilityOrdinates);
        analysis.BayesianAnalysis.SetCustomMCMCResults(
            CreatePosterior(model.Parameters.Select(parameter => parameter.Value).ToArray()), skipInformationCriteria: true);
        RestoreResults(analysis, 3);
        await AssertRoundTripAsync(analysis, analysis.BayesianAnalysis, model,
            analysis.UpdatePointEstimateResultsAsync, () => analysis.AnalysisResults!,
            () => new[] { analysis.BayesianAnalysis.Results! }, null);
    }

    /// <summary>Checks regional and every site's point quantiles, leaving posterior site summaries and intervals intact.</summary>
    /// <returns>A task representing the deterministic round trip.</returns>
    [TestMethod]
    public async Task SpatialGEV_MeanModeMean_PreservesSiteAndRegionalUncertainty()
    {
        SpatialGEV model = CreateSpatialModel();
        var analysis = new SpatialGEVAnalysis(model);
        SetProbabilities(analysis.ProbabilityOrdinates);
        analysis.BayesianAnalysis.SetCustomMCMCResults(
            CreatePosterior(model.Parameters.Select(parameter => parameter.Value).ToArray()), skipInformationCriteria: true);
        RestoreResults(analysis, 3);
        var sites = Enumerable.Range(0, model.Sites).Select(_ => new SpatialGEVSiteResults
        {
            QuantileMode = new double[3], QuantileMean = new[] { 100d, 200d, 300d },
            QuantileLower = new[] { 90d, 190d, 290d }, QuantileUpper = new[] { 110d, 210d, 310d },
            LocationMean = 100
        }).ToArray();
        SetRestoredProperty(analysis, nameof(SpatialGEVAnalysis.SiteResults), sites);
        double[][] originalMean = sites.Select(site => site.QuantileMean).ToArray();
        double[][] originalLower = sites.Select(site => site.QuantileLower).ToArray();
        double[][] originalUpper = sites.Select(site => site.QuantileUpper).ToArray();
        double[][]? firstPoints = null;
        await AssertRoundTripAsync(analysis, analysis.BayesianAnalysis, model,
            analysis.UpdatePointEstimateResultsAsync, () => analysis.AnalysisResults!,
            () => new[] { analysis.BayesianAnalysis.Results! }, (estimator, phase) =>
            {
                Assert.AreSame(sites, analysis.SiteResults);
                if (phase == 0) firstPoints = sites.Select(site => (double[])site.QuantileMode.Clone()).ToArray();
                for (int i = 0; i < sites.Length; i++)
                {
                    Assert.AreSame(originalMean[i], sites[i].QuantileMean);
                    Assert.AreSame(originalLower[i], sites[i].QuantileLower);
                    Assert.AreSame(originalUpper[i], sites[i].QuantileUpper);
                    CollectionAssert.AreEqual(new[] { 100d, 200d, 300d }, sites[i].QuantileMean);
                    CollectionAssert.AreEqual(new[] { 90d, 190d, 290d }, sites[i].QuantileLower);
                    CollectionAssert.AreEqual(new[] { 110d, 210d, 310d }, sites[i].QuantileUpper);
                    Assert.AreEqual(100d, sites[i].LocationMean);
                    if (phase == 1) Assert.IsFalse(firstPoints![i].SequenceEqual(sites[i].QuantileMode), "Every site's point curve changes.");
                    if (phase == 2) CollectionAssert.AreEqual(firstPoints![i], sites[i].QuantileMode);
                }
            });
    }

    /// <summary>Checks CFA selection from all three chains, refreshed distribution values, and preserved upstream fits.</summary>
    /// <returns>A task representing the deterministic round trip.</returns>
    /// <remarks>CFA evaluates cloned copula and marginal distributions; the upstream fitted model is intentionally unchanged.</remarks>
    [TestMethod]
    public async Task CoincidentFrequency_MeanModeMean_PreservesAllThreePosteriorsAndUncertainty()
    {
        CoincidentFrequencyAnalysis analysis = CreateCoincidentFrequency();
        BivariateAnalysis upstream = analysis.BivariateAnalysis;
        double[] upstreamParameters = upstream.BivariateDistribution.Parameters.Select(parameter => parameter.Value).ToArray();
        var marginalX = (IModel)upstream.BivariateDistribution.MarginalX;
        var marginalY = (IModel)upstream.BivariateDistribution.MarginalY;
        double[] marginalXParameters = marginalX.Parameters.Select(parameter => parameter.Value).ToArray();
        double[] marginalYParameters = marginalY.Parameters.Select(parameter => parameter.Value).ToArray();
        double? firstMedian = null;
        var inputs = new[] { upstream.BayesianAnalysis.Results!, analysis.MarginalXChain!, analysis.MarginalYChain! };
        await AssertRoundTripAsync(analysis, analysis.BayesianAnalysis, null,
            analysis.UpdatePointEstimateResultsAsync, () => analysis.AnalysisResults!,
            () => new[] { upstream.BayesianAnalysis.Results!, analysis.MarginalXChain!, analysis.MarginalYChain! },
            (estimator, phase) =>
            {
                // Compare against the explicit-parameter path to test selection routing, not numerical accuracy.
                double[][] selected = inputs.Select(posterior => estimator == PointEstimator.PosteriorMean
                    ? posterior.PosteriorMean.Values : posterior.MAP.Values).ToArray();
                MethodInfo explicitDraw = typeof(CoincidentFrequencyAnalysis).GetMethod("ComputeAEPCurveForDraw",
                    BindingFlags.Instance | BindingFlags.NonPublic)!;
                var expected = (double[])explicitDraw.Invoke(analysis, new object[]
                {
                    selected[0], selected[1], selected[2],
                    new[] { double.NegativeInfinity, -1.5, -0.5, 0.5, 1.5, double.PositiveInfinity }
                })!;
                CollectionAssert.AreEqual(expected, analysis.AnalysisResults!.ModeCurve);
                var pointDistribution = analysis.GetPointEstimateDistribution();
                Assert.IsNotNull(pointDistribution);
                double median = pointDistribution.InverseCDF(0.5);
                if (phase == 0) firstMedian = median;
                if (phase == 1) Assert.AreNotEqual(firstMedian!.Value, median);
                if (phase == 2) Assert.AreEqual(firstMedian!.Value, median);
                CollectionAssert.AreEqual(upstreamParameters,
                    upstream.BivariateDistribution.Parameters.Select(parameter => parameter.Value).ToArray());
                CollectionAssert.AreEqual(marginalXParameters,
                    marginalX.Parameters.Select(parameter => parameter.Value).ToArray());
                CollectionAssert.AreEqual(marginalYParameters,
                    marginalY.Parameters.Select(parameter => parameter.Value).ToArray());
                Assert.IsTrue(upstream.IsEstimated);
            });
    }

    /// <summary>Creates a complete sixty-year source with seeded arithmetic and no estimation.</summary>
    /// <returns>The annual source series.</returns>
    private static NumericsTimeSeries CreateSource()
    {
        var series = new NumericsTimeSeries(TimeInterval.OneYear, new DateTime(1960, 1, 1), new DateTime(2019, 1, 1));
        var random = new Random(12345);
        double previous = 5000;
        for (int i = 0; i < series.Count; i++)
        {
            previous = 5000 + 0.6 * (previous - 5000) + (random.NextDouble() * 2 - 1) * 600;
            series[i].Value = Math.Max(100, previous);
        }
        return series;
    }

    /// <summary>Creates a small, fixed five-site model with intercept-only trends.</summary>
    /// <returns>The configured spatial model.</returns>
    private static SpatialGEV CreateSpatialModel()
    {
        var data = new double[30, 5];
        var random = new Random(12345);
        var distribution = new GeneralizedExtremeValue(10000, 2500, 0);
        for (int site = 0; site < 5; site++)
            for (int year = 0; year < 30; year++)
                data[year, site] = distribution.InverseCDF(random.NextDouble());
        var coordinates = new double[,] { { 0, 0 }, { 10, 5 }, { 22, 8 }, { 35, 12 }, { 50, 15 } };
        return new SpatialGEV(data, coordinates, new GeneralLinearFunction("Location"),
            new GeneralLinearFunction("Scale"), new GeneralLinearFunction("Shape"));
    }

    /// <summary>Creates a Normal-copula sum response with independent retained marginal chains.</summary>
    /// <returns>The restored estimated CFA.</returns>
    private static CoincidentFrequencyAnalysis CreateCoincidentFrequency()
    {
        var frameX = new BestFitDataFrame { ExactSeries = new ExactSeries(new[] { -1d, 0, 1, -0.5, 0.5 }) };
        var frameY = new BestFitDataFrame { ExactSeries = new ExactSeries(new[] { -1d, 0, 1, -0.5, 0.5 }) };
        var x = new UnivariateDistribution(frameX, UnivariateDistributionType.Normal);
        var y = new UnivariateDistribution(frameY, UnivariateDistributionType.Normal);
        x.SetParameterValues(new[] { 0d, 1d });
        y.SetParameterValues(new[] { 0d, 1d });
        var model = new BivariateDistribution(x, y, CopulaType.Normal);
        model.Copula.SetCopulaParameters(new[] { 0.2 });
        var upstream = new BivariateAnalysis(model);
        upstream.BayesianAnalysis.SetCustomMCMCResults(CreatePosterior(new[] { 0.2 }), skipInformationCriteria: true);
        SetRestoredProperty(upstream, nameof(AnalysisBase.IsEstimated), true);
        var grid = new[] { -2d, -1, 0, 1, 2 };
        var response = new double[5, 5];
        for (int i = 0; i < 5; i++)
            for (int j = 0; j < 5; j++) response[i, j] = grid[i] + grid[j];
        var analysis = new CoincidentFrequencyAnalysis(upstream, grid, (double[])grid.Clone(), response)
        {
            NumberOfBins = 15,
            MarginalXChain = CreatePosterior(new[] { 0d, 1d }),
            MarginalYChain = CreatePosterior(new[] { 0d, 1d })
        };
        analysis.SetZOutputValues(new[] { -4d, -2, 0, 2, 4 });
        RestoreResults(analysis, 5);
        return analysis;
    }

    /// <summary>Creates six fixed posterior parameter vectors and a distinct stored MAP.</summary>
    /// <param name="baseline">The model's configured default parameters.</param>
    /// <returns>Stored posterior summaries and draws.</returns>
    private static MCMCResults CreatePosterior(double[] baseline)
    {
        var draws = new List<ParameterSet>();
        for (int i = 0; i < 6; i++)
        {
            var values = (double[])baseline.Clone();
            for (int j = 0; j < values.Length; j++)
                values[j] += (i - 2) * 0.005 * Math.Max(1, Math.Abs(values[j]));
            draws.Add(new ParameterSet(values, -10 + i));
        }
        var map = (double[])baseline.Clone();
        map[0] += 0.1 * Math.Max(1, Math.Abs(map[0]));
        return new MCMCResults(new ParameterSet(map, 0), draws, alpha: 0.1);
    }

    /// <summary>Sets a compact common frequency grid before restoring the estimated state.</summary>
    /// <param name="probabilities">The analysis-owned mutable probability collection.</param>
    private static void SetProbabilities(ICollection<double> probabilities)
    {
        probabilities.Clear();
        foreach (double probability in new[] { 0.5, 0.1, 0.01 }) probabilities.Add(probability);
    }

    /// <summary>Restores non-null result arrays and the completed-fit flag without invoking estimation.</summary>
    /// <param name="analysis">The analysis receiving stored results.</param>
    /// <param name="count">The number of point and predictive ordinates.</param>
    private static void RestoreResults(AnalysisBase analysis, int count)
    {
        var intervals = new double[count, 3];
        for (int i = 0; i < count; i++)
        {
            intervals[i, 0] = i;
            intervals[i, 1] = 100 + i;
            intervals[i, 2] = 200 + i;
        }
        var results = new UncertaintyAnalysisResults
        {
            ModeCurve = new double[count], MeanCurve = Enumerable.Range(0, count).Select(i => (double)i).ToArray(),
            ConfidenceIntervals = intervals
        };
        SetRestoredProperty(analysis, "AnalysisResults", results);
        SetRestoredProperty(analysis, nameof(AnalysisBase.IsEstimated), true);
    }

    /// <summary>Sets a non-public result setter solely to restore deterministic test state.</summary>
    /// <param name="target">The analysis to restore.</param>
    /// <param name="name">The result or estimated-state property.</param>
    /// <param name="value">The retained value.</param>
    private static void SetRestoredProperty(object target, string name, object value)
    {
        target.GetType().GetProperty(name)!.GetSetMethod(nonPublic: true)!.Invoke(target, new[] { value });
    }

    /// <summary>Asserts a complete retained-fit round trip using the real property-triggered asynchronous path.</summary>
    /// <param name="analysis">The estimated analysis.</param>
    /// <param name="bayesian">Its point-estimator settings owner.</param>
    /// <param name="model">The fitted model, or null for CFA's cloned input distributions.</param>
    /// <param name="initialize">The deterministic initial point-result builder.</param>
    /// <param name="getResults">Accesses current analysis results.</param>
    /// <param name="getPosteriors">Accesses every retained input posterior.</param>
    /// <param name="assertAdditional">Optional family-specific assertions for each estimator and phase.</param>
    /// <returns>A task that completes after all queued postprocessing and assertions.</returns>
    private static async Task AssertRoundTripAsync(AnalysisBase analysis, BayesianAnalysis bayesian, IModel? model,
        Func<Task> initialize, Func<UncertaintyAnalysisResults> getResults, Func<MCMCResults[]> getPosteriors,
        Action<PointEstimator, int>? assertAdditional)
    {
        UncertaintyAnalysisResults originalResults = getResults();
        Assert.IsNotNull(originalResults.ConfidenceIntervals);
        Assert.IsNotNull(originalResults.MeanCurve);
        double[,] intervals = originalResults.ConfidenceIntervals;
        double[] intervalValues = intervals.Cast<double>().ToArray();
        double[] predictive = originalResults.MeanCurve;
        double[] predictiveValues = (double[])predictive.Clone();
        MCMCResults[] posteriors = getPosteriors();
        double[][] draws = posteriors.Select(p => p.Output.SelectMany(draw => draw.Values).ToArray()).ToArray();
        double[][] maps = posteriors.Select(p => (double[])p.MAP.Values.Clone()).ToArray();
        double[][] means = posteriors.Select(p => (double[])p.PosteriorMean.Values.Clone()).ToArray();
        double[]? originalPoints = null;
        await initialize();
        PointEstimator[] selections = { PointEstimator.PosteriorMean, PointEstimator.PosteriorMode, PointEstimator.PosteriorMean };
        for (int phase = 0; phase < selections.Length; phase++)
        {
            PointEstimator estimator = selections[phase];
            if (phase > 0) await SelectAndWaitAsync(analysis, bayesian, estimator);
            Assert.AreEqual(estimator, bayesian.PointEstimator);
            Assert.IsTrue(analysis.IsEstimated, "Changing a retained fit's point estimator must preserve the estimated state.");
            UncertaintyAnalysisResults current = getResults();
            Assert.AreSame(originalResults, current);
            Assert.IsNotNull(current.ModeCurve);
            Assert.IsTrue(current.ModeCurve.All(double.IsFinite), "The fixture must produce usable point results.");
            if (phase == 0) originalPoints = (double[])current.ModeCurve.Clone();
            if (phase == 1) Assert.IsFalse(originalPoints!.SequenceEqual(current.ModeCurve), "The distinct mean and mode must produce different point results.");
            if (phase == 2) CollectionAssert.AreEqual(originalPoints!, current.ModeCurve);
            if (model != null)
            {
                double[] selected = estimator == PointEstimator.PosteriorMean ? means[0] : maps[0];
                CollectionAssert.AreEqual(selected, model.Parameters.Select(parameter => parameter.Value).ToArray());
            }
            Assert.AreSame(intervals, current.ConfidenceIntervals);
            Assert.IsNotNull(current.ConfidenceIntervals);
            CollectionAssert.AreEqual(intervalValues, current.ConfidenceIntervals.Cast<double>().ToArray());
            Assert.AreSame(predictive, current.MeanCurve);
            CollectionAssert.AreEqual(predictiveValues, current.MeanCurve);
            MCMCResults[] currentPosteriors = getPosteriors();
            Assert.AreEqual(posteriors.Length, currentPosteriors.Length);
            for (int i = 0; i < posteriors.Length; i++)
            {
                Assert.AreSame(posteriors[i], currentPosteriors[i]);
                CollectionAssert.AreEqual(draws[i], currentPosteriors[i].Output.SelectMany(draw => draw.Values).ToArray());
                CollectionAssert.AreEqual(maps[i], currentPosteriors[i].MAP.Values);
                CollectionAssert.AreEqual(means[i], currentPosteriors[i].PosteriorMean.Values);
            }
            assertAdditional?.Invoke(estimator, phase);
        }
    }

    /// <summary>Changes the real settings property and waits for publication plus release of the processing gate.</summary>
    /// <param name="analysis">The analysis that publishes its rebuilt point results.</param>
    /// <param name="bayesian">The settings object whose setter triggers reprocessing.</param>
    /// <param name="estimator">The selected estimator.</param>
    /// <returns>A task completing after the triggered worker has finished.</returns>
    private static async Task SelectAndWaitAsync(AnalysisBase analysis, BayesianAnalysis bayesian, PointEstimator estimator)
    {
        var completed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        PropertyChangedEventHandler handler = (_, args) =>
        {
            if (args.PropertyName == "AnalysisResults") completed.TrySetResult(true);
        };
        analysis.PropertyChanged += handler;
        try
        {
            bayesian.PointEstimator = estimator;
            await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var gate = (SemaphoreSlim)typeof(AnalysisBase).GetField("_reprocessGate",
                BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(analysis)!;
            bool acquired = await gate.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsTrue(acquired, "Point-result publication must be followed by worker completion.");
            if (acquired) gate.Release();
        }
        finally
        {
            analysis.PropertyChanged -= handler;
        }
    }
}
