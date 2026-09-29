using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Numerics.Data;
using Numerics.Distributions;
using Numerics.Distributions.Copulas;
using Numerics.Mathematics.Optimization;
using Numerics.Sampling.MCMC;
using RMC.BestFit.App.Tests.GUI.Support;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using RMC.BestFit.Models.TrendFunctions.Support;
using ModelAnalyses = RMC.BestFit.Analyses;
using UI = RMC.BestFit.UI;

namespace RMC.BestFit.App.Tests.GUI;

/// <summary>Exercises the real point-estimator ComboBoxes and their result views across analysis families.</summary>
/// <remarks>All analyses use injected stored draws or restored uncertainty artifacts; estimation is never run.</remarks>
[TestClass]
[DoNotParallelize]
public class PointEstimatorControlContractTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    /// <summary>Releases resources owned by this test's STA thread.</summary>
    [TestCleanup]
    public void Cleanup() => WpfTestHost.ReleaseResources();

    /// <summary>Selected estimates round-trip through properties, summaries, tables, and plots.</summary>
    /// <param name="family">The analysis family and model variant.</param>
    /// <remarks>The baseline and returned posterior-mean values must match exactly, while the MAP must differ.</remarks>
    [STATestMethod]
    [DataRow("Univariate")]
    [DataRow("Nonstationary")]
    [DataRow("Mixture")]
    [DataRow("PointProcess")]
    [DataRow("SeasonalPointProcess")]
    [DataRow("Composite")]
    [DataRow("B17C")]
    [DataRow("Bivariate")]
    [DataRow("RatingCurve")]
    public void Selector_UpdatesDisplayedPointEstimateAndPreservesUncertainty(string family)
    {
        WpfTestHost.EnsureResources();
        var fixture = CreateFixture(family);
        var control = CreateControl(fixture.Kind + "Control", fixture.Wrapper);
        var properties = CreateControl(fixture.Kind + "PropertiesControl", fixture.Wrapper);
        try
        {
            WpfTestHost.DrainDispatcher();
            var output = properties.FindName("BayesianOutputControl") as FrameworkElement;
            var selectorContainer = (output ?? properties).FindName("PointEstimator");
            var selector = (ComboBox)Read(selectorContainer, "InnerContent");
            var results = (UncertaintyAnalysisResults)Read(fixture.Analysis, "AnalysisResults");
            var bayesian = (BayesianAnalysis)Read(fixture.Analysis, "BayesianAnalysis");
            var posterior = bayesian.Results;
            var samples = posterior?.Output.SelectMany(p => p.Values).ToArray();
            var bands = results.ConfidenceIntervals;
            var predictive = results.MeanCurve;
            Assert.IsNotNull(bands, family);
            Assert.IsNotNull(predictive, family);
            var bandValues = bands.Cast<double>().ToArray();
            var predictiveValues = (double[])predictive.Clone();
            var originalCurve = (double[])results.ModeCurve.Clone();
            var originalSummary = Summary(control, fixture.SummaryRow);
            var originalResiduals = family == "RatingCurve" ? Residuals(control) : null;
            Assert.AreEqual(fixture.MeanSummary, originalSummary, 1e-8, family);
            Assert.AreEqual(family == "B17C" ? "Mean Parameters" : "Posterior Mean", Header(control), family);
            AssertDisplayedCurve(fixture, control, results);

            foreach (var estimator in new[] { BayesianAnalysis.PointEstimateType.PosteriorMode, BayesianAnalysis.PointEstimateType.PosteriorMean })
            {
                int notifications = 0;
                PropertyChangedEventHandler onResults = (_, e) =>
                {
                    if (e.PropertyName == "AnalysisResults") Interlocked.Increment(ref notifications);
                };
                ((INotifyPropertyChanged)fixture.Analysis).PropertyChanged += onResults;
                try
                {
                    selector.SelectedValue = estimator;
                    PumpUntil(() => Volatile.Read(ref notifications) > 0, family);
                    WpfTestHost.DrainDispatcher();
                }
                finally
                {
                    ((INotifyPropertyChanged)fixture.Analysis).PropertyChanged -= onResults;
                }
                Assert.AreEqual(estimator, bayesian.PointEstimator, family);
                Assert.AreSame(results, Read(fixture.Analysis, "AnalysisResults"), family);
                Assert.AreEqual(estimator == BayesianAnalysis.PointEstimateType.PosteriorMean ? fixture.MeanSummary : fixture.ModeSummary,
                    Summary(control, fixture.SummaryRow), 1e-8, family);
                Assert.AreEqual(family == "B17C"
                    ? (estimator == BayesianAnalysis.PointEstimateType.PosteriorMean ? "Mean Parameters" : "Computed")
                    : (estimator == BayesianAnalysis.PointEstimateType.PosteriorMean ? "Posterior Mean" : "Posterior Mode"), Header(control), family);
                Assert.IsTrue((bool)Read(fixture.Analysis, "IsEstimated"), family);
                Assert.IsTrue((bool)Read(fixture.Wrapper, "IsEstimated"), family);
                Assert.AreSame(posterior, bayesian.Results, family);
                if (posterior != null)
                    CollectionAssert.AreEqual(samples, posterior.Output.SelectMany(p => p.Values).ToArray(), family);
                Assert.AreSame(bands, results.ConfidenceIntervals, family);
                Assert.AreSame(predictive, results.MeanCurve, family);
                CollectionAssert.AreEqual(bandValues, results.ConfidenceIntervals.Cast<double>().ToArray(), family);
                CollectionAssert.AreEqual(predictiveValues, results.MeanCurve, family);
                if (estimator == BayesianAnalysis.PointEstimateType.PosteriorMode)
                    Assert.IsFalse(originalCurve.SequenceEqual(results.ModeCurve), family);
                else
                    CollectionAssert.AreEqual(originalCurve, results.ModeCurve, family);
                AssertDisplayedCurve(fixture, control, results);
                if (family == "Bivariate")
                {
                    double rho = estimator == BayesianAnalysis.PointEstimateType.PosteriorMean ? .5 : .25;
                    Assert.AreEqual(.25 + Math.Asin(rho) / (2d * Math.PI), results.ModeCurve[0], 1e-8,
                        "The Gaussian copula probability at both marginal medians must use the selected rho.");
                }
                if (family == "RatingCurve")
                {
                    var residuals = Residuals(control);
                    if (estimator == BayesianAnalysis.PointEstimateType.PosteriorMode)
                        Assert.IsFalse(originalResiduals.SequenceEqual(residuals), "Rating residuals must refresh with the selected parameters.");
                    else
                        CollectionAssert.AreEqual(originalResiduals, residuals);
                }
                if (family == "Nonstationary")
                {
                    var chronology = ((ModelAnalyses.UnivariateAnalysis)fixture.Analysis).ChronologyAnalysisResults;
                    Assert.AreEqual(estimator == BayesianAnalysis.PointEstimateType.PosteriorMean ? 17000d : 16000d,
                        chronology.ModeCurve[0], 1e-8);
                }
            }
        }
        finally
        {
            control.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
            properties.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
            control.GetType().GetProperty("Element").SetValue(control, null);
            properties.GetType().GetProperty("Element").SetValue(properties, null);
            WpfTestHost.DrainDispatcher();
        }
    }

    /// <summary>Checks the displayed table and plotted point curve against the current results.</summary>
    /// <param name="fixture">The family fixture.</param>
    /// <param name="control">The result control.</param>
    /// <param name="results">The current point and uncertainty output.</param>
    /// <remarks>Bivariate probability results have no ordinary frequency plot.</remarks>
    private static void AssertDisplayedCurve(Fixture fixture, FrameworkElement control, UncertaintyAnalysisResults results)
    {
        int index = results.ModeCurve.Length / 2;
        var table = control.FindName(fixture.Kind == "RatingCurveAnalysis" ? "RatingCurveTable" : "FrequencyCurveTable");
        var rows = ((IEnumerable)Read(table, "ItemsSource")).Cast<object>().ToArray();
        Assert.AreEqual(results.ModeCurve[index], (double)Read(rows[index], "Mode"), 1e-8, fixture.Kind);
        if (fixture.Kind == "BivariateAnalysis") return;
        var plot = Read(fixture.Wrapper, fixture.Kind == "RatingCurveAnalysis" ? "RatingCurvePlot" : "FrequencyPlot");
        var series = ((IEnumerable)Read(plot, "Series")).Cast<object>().Single(s => (string)Read(s, "Name") == "PosteriorMode");
        var points = ((IEnumerable)Read(series, "ItemsSource")).Cast<object>().ToArray();
        Assert.AreEqual(results.ModeCurve[index], (double)Read(points[index], fixture.Kind == "RatingCurveAnalysis" ? "X" : "Y"), 1e-8, fixture.Kind);
    }

    /// <summary>Reads a summary statistic currently displayed in the result control.</summary>
    /// <param name="control">The result control.</param>
    /// <param name="row">The parameter row.</param>
    /// <returns>The displayed value.</returns>
    private static double Summary(FrameworkElement control, int row)
        => (double)Read(((IEnumerable)Read(control.FindName("SummaryStatisticsTable"), "ItemsSource")).Cast<object>().ElementAt(row), "Value");

    /// <summary>Reads the current summary-column label.</summary>
    /// <param name="control">The result control.</param>
    /// <returns>The displayed estimator label.</returns>
    private static string Header(FrameworkElement control) => (string)Read(control.FindName("StatValueColumn"), "Header");

    /// <summary>Snapshots the rating residuals supplied to the residual, histogram, and QQ views.</summary>
    /// <param name="control">The rating result control.</param>
    /// <returns>The current residual values.</returns>
    private static double[] Residuals(FrameworkElement control)
        => ((IEnumerable<double>)control.GetType().GetField("_residuals", PrivateInstance).GetValue(control)).ToArray();

    /// <summary>Pumps the dispatcher until background reprocessing publishes results.</summary>
    /// <param name="condition">The completion predicate.</param>
    /// <param name="family">The assertion context.</param>
    private static void PumpUntil(Func<bool> condition, string family)
    {
        var watch = Stopwatch.StartNew();
        while (!condition() && watch.Elapsed < TimeSpan.FromSeconds(10))
        {
            WpfTestHost.DrainDispatcher();
            Thread.Sleep(10);
        }
        Assert.IsTrue(condition(), family + " did not publish results.");
    }

    /// <summary>Creates and loads a real App control with the supplied analysis wrapper.</summary>
    /// <param name="name">The App control type name.</param>
    /// <param name="wrapper">The UI analysis element.</param>
    /// <returns>The initialized control.</returns>
    private static FrameworkElement CreateControl(string name, object wrapper)
    {
        var type = typeof(RMC_BestFit.UnivariateAnalysisControl).Assembly.GetType("RMC_BestFit." + name, true);
        var control = (FrameworkElement)Activator.CreateInstance(type);
        type.GetProperty("Element").SetValue(control, wrapper);
        control.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        return control;
    }

    /// <summary>Reads a common property from the intentionally different family types.</summary>
    /// <param name="target">The object to inspect.</param>
    /// <param name="name">The property name.</param>
    /// <returns>The current property value.</returns>
    private static object Read(object target, string name) => target.GetType().GetProperty(name).GetValue(target);

    /// <summary>Creates the requested family using fixed stored estimation artifacts.</summary>
    /// <param name="family">The family variant.</param>
    /// <returns>The initialized core and UI fixture.</returns>
    private static Fixture CreateFixture(string family)
    {
        if (family == "Bivariate") return CreateBivariate();
        if (family == "RatingCurve") return CreateRating();
        if (family == "B17C") return CreateB17C();
        if (family == "Composite") return CreateComposite();
        var frame = Frame();
        ModelAnalyses.AnalysisBase analysis;
        double[] map;
        double[] mean;
        string kind;
        int row = 0;
        if (family == "Mixture")
        {
            analysis = new ModelAnalyses.MixtureAnalysis(new MixtureModel(frame,
                new List<UnivariateDistributionType> { UnivariateDistributionType.Normal, UnivariateDistributionType.Normal }));
            map = new[] { .4, 5500d, 600d, 20000d, 3000d };
            mean = new[] { .6, 6000d, 700d, 21000d, 3200d };
            kind = "MixtureAnalysis";
        }
        else if (family.Contains("PointProcess"))
        {
            bool seasonal = family == "SeasonalPointProcess";
            frame = SeasonalFrame();
            var model = new PointProcessModel { IsSeasonal = seasonal, DataFrame = frame };
            analysis = new ModelAnalyses.PointProcessAnalysis(model);
            map = model.Parameters.Select(p => p.Value).ToArray();
            mean = (double[])map.Clone();
            row = seasonal ? 2 : 0;
            mean[row] += 100d;
            if (seasonal) mean[5] += 75d;
            kind = "PointProcessAnalysis";
        }
        else
        {
            var model = new UnivariateDistribution(frame, UnivariateDistributionType.Normal);
            if (family == "Nonstationary")
            {
                model.IsNonstationary = true;
                model.SetTrendModel(0, TrendModelType.Linear);
                map = new[] { 16000d, 50d, 5000d };
                mean = new[] { 17000d, 80d, 6000d };
            }
            else
            {
                map = new[] { 17000d, 5000d };
                mean = new[] { 16000d, 6000d };
            }
            analysis = new ModelAnalyses.UnivariateAnalysis(model);
            kind = "UnivariateAnalysis";
        }
        Inject(analysis, map, mean);
        Complete(analysis, "CreateFrequencyAnalysisResultsAsync");
        if (family == "Nonstationary") Complete(analysis, "CreateChronologyResultsAsync");
        return new Fixture(kind, analysis, Wrap(kind, analysis, frame), row, mean[row], map[row]);
    }

    /// <summary>Creates a dated annual exact-observation frame.</summary>
    /// <returns>The local positive-flow fixture.</returns>
    private static DataFrame Frame()
    {
        double[] values = { 12500, 15300, 8900, 22100, 18700, 14200, 9800, 28500, 17400, 11600,
            19200, 13800, 25600, 10500, 16900, 21300, 14700, 8200, 23800, 15900 };
        var frame = new DataFrame();
        for (int i = 0; i < values.Length; i++) frame.ExactSeries.Add(new ExactData(1990 + i, values[i]));
        frame.CalculatePlottingPositions();
        return frame;
    }

    /// <summary>Creates two seasonal events per year without random generation.</summary>
    /// <returns>The dated peaks-over-threshold fixture.</returns>
    private static DataFrame SeasonalFrame()
    {
        var frame = new DataFrame();
        for (int i = 0; i < 10; i++)
        {
            frame.ExactSeries.Add(new ExactData(new DateTime(1990 + i, 2, 15), 1500 + i * 100));
            frame.ExactSeries.Add(new ExactData(new DateTime(1990 + i, 7, 15), 2000 + i * 150));
        }
        frame.CalculatePlottingPositions();
        return frame;
    }

    /// <summary>Injects a fixed posterior and restores the outer estimated state.</summary>
    /// <param name="analysis">The target analysis.</param>
    /// <param name="map">The stored mode.</param>
    /// <param name="mean">The stored mean.</param>
    private static void Inject(ModelAnalyses.AnalysisBase analysis, double[] map, double[] mean)
    {
        var bayesian = (BayesianAnalysis)Read(analysis, "BayesianAnalysis");
        bayesian.OutputLength = 100;
        bayesian.SetCustomMCMCResults(BayesianStoredResults.Build(map, mean), skipInformationCriteria: true);
        BayesianStoredResults.MarkEstimated(analysis);
    }

    /// <summary>Completes deterministic output processing before UI controls subscribe.</summary>
    /// <param name="analysis">The analysis.</param>
    /// <param name="method">The output-processing method.</param>
    private static void Complete(object analysis, string method)
        => Task.Run(async () => await (Task)analysis.GetType().GetMethod(method).Invoke(analysis, null)).GetAwaiter().GetResult();

    /// <summary>Wraps an injected univariate-family analysis with its actual UI wrapper.</summary>
    /// <param name="kind">The wrapper type name.</param>
    /// <param name="analysis">The core analysis.</param>
    /// <param name="frame">The displayed input frame.</param>
    /// <returns>The UI wrapper.</returns>
    private static object Wrap(string kind, ModelAnalyses.AnalysisBase analysis, DataFrame frame)
    {
        var project = UI.BestFitProject.GetInstance();
        var type = typeof(UI.UnivariateAnalysis).Assembly.GetType("RMC.BestFit.UI." + kind, true);
        var wrapper = Activator.CreateInstance(type, "Selector contract", new UI.UnivariateAnalysisCollection(project), false);
        type.GetProperty("InputData").SetValue(wrapper,
            new UI.InputData("Selector input", new UI.InputDataCollection(project)) { DataFrame = frame });
        Attach(wrapper, analysis);
        return wrapper;
    }

    /// <summary>Replaces the initial core object and rebuilds the wrapper's ordinary subscriptions and undo bridges.</summary>
    /// <param name="wrapper">The UI wrapper.</param>
    /// <param name="analysis">The injected core analysis.</param>
    private static void Attach(object wrapper, ModelAnalyses.AnalysisBase analysis)
    {
        var type = wrapper.GetType();
        type.GetMethod("DisposeBridges", PrivateInstance).Invoke(wrapper, null);
        var unsubscribe = type.GetMethod("UnsubscribeInnerAnalysis", PrivateInstance);
        var subscribe = type.GetMethod("SubscribeInnerAnalysis", PrivateInstance);
        PropertyChangedEventHandler handler = null;
        if (unsubscribe != null)
            unsubscribe.Invoke(wrapper, null);
        else
        {
            // Bivariate and Rating use a direct PropertyChanged subscription in their constructors and restore paths.
            var method = type.GetMethod("InnerAnalysis_PropertyChanged", PrivateInstance);
            handler = (PropertyChangedEventHandler)Delegate.CreateDelegate(typeof(PropertyChangedEventHandler), wrapper, method);
            ((INotifyPropertyChanged)Read(wrapper, "InnerAnalysis")).PropertyChanged -= handler;
        }
        type.GetField("_innerAnalysis", PrivateInstance).SetValue(wrapper, analysis);
        if (subscribe != null) subscribe.Invoke(wrapper, null);
        else analysis.PropertyChanged += handler;
        type.GetMethod("SetupBridges", PrivateInstance).Invoke(wrapper, null);
        type.GetMethod("SetIsValid", PrivateInstance, null, Type.EmptyTypes, null).Invoke(wrapper, null);
    }

    /// <summary>Creates a Gaussian copula with fixed Normal marginals.</summary>
    /// <returns>The bivariate control fixture.</returns>
    private static Fixture CreateBivariate()
    {
        var x = Marginal(new[] { 98.1, 102.7, 115.3, 88.4, 104.9, 92d, 110.5, 99.2, 107.6, 101.3 }, 100d, 10d);
        var y = Marginal(new[] { 75.2, 82.1, 93.6, 68.7, 84.3, 72.5, 90.4, 78.9, 88.5, 81d }, 80d, 8d);
        var analysis = new ModelAnalyses.BivariateAnalysis(new BivariateDistribution(x, y, CopulaType.Normal));
        analysis.XYOrdinates = new UncertainOrderedPairedData(
            new List<UncertainOrdinate> { new UncertainOrdinate(100d, new Deterministic(80d)) },
            false, SortOrder.Ascending, false, SortOrder.Ascending, UnivariateDistributionType.Deterministic);
        Inject(analysis, new[] { .25 }, new[] { .5 });
        Complete(analysis, "CreateFrequencyAnalysisResultsAsync");
        var project = UI.BestFitProject.GetInstance();
        var wrapper = new UI.BivariateAnalysis("Selector bivariate", new UI.BivariateAnalysisCollection(project), false)
        {
            MarginalX = (UI.IUnivariate)Wrap("UnivariateAnalysis", new ModelAnalyses.UnivariateAnalysis(x), x.DataFrame),
            MarginalY = (UI.IUnivariate)Wrap("UnivariateAnalysis", new ModelAnalyses.UnivariateAnalysis(y), y.DataFrame)
        };
        Attach(wrapper, analysis);
        return new Fixture("BivariateAnalysis", analysis, wrapper, 0, .5, .25);
    }

    /// <summary>Builds a Normal marginal with fixed parameters and aligned observation indices.</summary>
    /// <param name="values">The observed values.</param>
    /// <param name="mean">The location.</param>
    /// <param name="deviation">The scale.</param>
    /// <returns>The configured marginal.</returns>
    private static UnivariateDistribution Marginal(double[] values, double mean, double deviation)
    {
        var frame = new DataFrame { ExactSeries = new ExactSeries(values) };
        for (int i = 0; i < values.Length; i++) frame.ExactSeries[i].Index = i;
        frame.CalculatePlottingPositions();
        var model = new UnivariateDistribution(frame, UnivariateDistributionType.Normal);
        model.SetParameterValues(new[] { mean, deviation });
        return model;
    }

    /// <summary>Creates an aligned rating-curve fixture with more than the required ten observations.</summary>
    /// <returns>The rating-curve control fixture.</returns>
    private static Fixture CreateRating()
    {
        var stages = Enumerable.Range(0, 20).Select(i => 1d + i * .4).ToArray();
        var discharges = stages.Select(h => 10d * Math.Pow(h - .5, 2)).ToArray();
        var stageSeries = new Numerics.Data.TimeSeries(TimeInterval.OneDay, new DateTime(2000, 1, 1), stages);
        var dischargeSeries = new Numerics.Data.TimeSeries(TimeInterval.OneDay, new DateTime(2000, 1, 1), discharges);
        var analysis = new ModelAnalyses.RatingCurveAnalysis(new RMC.BestFit.Models.RatingCurve(stageSeries, dischargeSeries, 1));
        Inject(analysis, new[] { .5, 1d, 2d, .05 }, new[] { .6, 1.1, 2.1, .06 });
        Complete(analysis, "CreateUncertaintyAnalysisResultsAsync");
        var project = UI.BestFitProject.GetInstance();
        var collection = new UI.TimeSeriesCollection(project);
        var wrapper = new UI.RatingCurveAnalysis("Selector rating", new UI.RatingCurveAnalysisCollection(project), false)
        {
            StageData = new UI.TimeSeriesElement("Selector stages", collection) { TimeSeries = stageSeries },
            DischargeData = new UI.TimeSeriesElement("Selector discharges", collection) { TimeSeries = dischargeSeries }
        };
        Attach(wrapper, analysis);
        var validity = analysis.RatingCurve.Validate();
        Assert.IsTrue(validity.IsValid, string.Join(Environment.NewLine, validity.ValidationMessages));
        Assert.IsTrue(wrapper.IsValid, "The 20 aligned, positive pairs must form a valid RatingCurve UI fixture.");
        return new Fixture("RatingCurveAnalysis", analysis, wrapper, 0, .6, .5);
    }

    /// <summary>Creates the maximum of two identically distributed Normal children.</summary>
    /// <returns>The composite fixture with its analytic expected mean.</returns>
    /// <remarks>The expected maximum of two independent Normal variables is location plus scale divided by sqrt(pi).</remarks>
    private static Fixture CreateComposite()
    {
        var frame = Frame();
        var children = new[]
        {
            new ModelAnalyses.UnivariateAnalysis(new UnivariateDistribution(frame, UnivariateDistributionType.Normal)),
            new ModelAnalyses.UnivariateAnalysis(new UnivariateDistribution(frame, UnivariateDistributionType.Normal))
        };
        foreach (var child in children)
        {
            Inject(child, new[] { 17000d, 5000d }, new[] { 16000d, 6000d });
            Complete(child, "CreateFrequencyAnalysisResultsAsync");
        }
        var analysis = new ModelAnalyses.CompositeAnalysis(children.Select(child => new ModelAnalyses.WeightedUnivariateAnalysis(child, .5)));
        analysis.RestoreAnalysisResults(StoredUncertainty(children[0].UnivariateDistribution.Distribution, analysis.ProbabilityOrdinates.Count));
        Complete(analysis, "UpdatePointEstimateResultsAsync");
        var project = UI.BestFitProject.GetInstance();
        var wrapper = new UI.CompositeAnalysis("Selector composite", new UI.UnivariateAnalysisCollection(project), false)
        {
            InputData = new UI.InputData("Composite data", new UI.InputDataCollection(project)) { DataFrame = frame }
        };
        foreach (var child in children)
            wrapper.Analyses.Add(new UI.WeightedUnivariateAnalysis
            {
                Weight = .5,
                UnivariateAnalysis = (UI.IUnivariate)Wrap("UnivariateAnalysis", child, frame)
            });
        Attach(wrapper, analysis);
        return new Fixture("CompositeAnalysis", analysis, wrapper, 2, 16000d + 6000d / Math.Sqrt(Math.PI), 17000d + 5000d / Math.Sqrt(Math.PI));
    }

    /// <summary>Creates stored Bulletin 17C computed and mean-parameter outputs.</summary>
    /// <returns>The Bulletin 17C fixture.</returns>
    /// <remarks>The compatibility ensemble represents saved GMM uncertainty, not a Bayesian fit.</remarks>
    private static Fixture CreateB17C()
    {
        var frame = Frame();
        var model = new Bulletin17CDistribution(frame, UnivariateDistributionType.LogPearsonTypeIII);
        model.SetInitialParameters();
        var map = model.Parameters.Select(p => p.Value).ToArray();
        var mean = (double[])map.Clone();
        mean[0] *= 1.01;
        var analysis = new ModelAnalyses.Bulletin17CAnalysis(model,
            new XElement("Bulletin17CAnalysis", new XAttribute("IsEstimated", true)),
            BayesianStoredResults.Build(map, mean), StoredUncertainty(model.Distribution, 25));
        var gmm = new GeneralizedMethodOfMoments(
            _ => throw new InvalidOperationException("No covariance calculation is provided by this stored-results fixture."),
            map.Length, map.Length, 50, map, map.Select(v => v - 100d).ToArray(), map.Select(v => v + 100d).ToArray());
        typeof(GeneralizedMethodOfMoments).GetProperty("BestParameterSet").SetValue(gmm, new ParameterSet(map, 0d));
        typeof(GeneralizedMethodOfMoments).GetProperty("IsEstimated").SetValue(gmm, true);
        typeof(ModelAnalyses.Bulletin17CAnalysis).GetField("_gmm", PrivateInstance).SetValue(analysis, gmm);
        Complete(analysis, "UpdatePointEstimateResultsAsync");
        return new Fixture("B17CAnalysis", analysis, Wrap("B17CAnalysis", analysis, frame), 0, mean[0], map[0]);
    }

    /// <summary>Provides non-null stored uncertainty arrays whose preservation can be asserted exactly.</summary>
    /// <param name="distribution">The saved parent distribution.</param>
    /// <param name="count">The number of ordinates.</param>
    /// <returns>The fixed restored uncertainty artifacts.</returns>
    /// <remarks>These synthetic arrays test preservation only; their values are not a numerical uncertainty oracle.</remarks>
    private static UncertaintyAnalysisResults StoredUncertainty(UnivariateDistributionBase distribution, int count)
    {
        var bands = new double[count, 2];
        for (int i = 0; i < count; i++) { bands[i, 0] = 100d + i; bands[i, 1] = 200d + i; }
        return new UncertaintyAnalysisResults
        {
            ParentDistribution = distribution.Clone(), ModeCurve = new double[count],
            MeanCurve = Enumerable.Range(0, count).Select(i => 150d + i).ToArray(), ConfidenceIntervals = bands
        };
    }

    /// <summary>Holds the core analysis, real wrapper, and literal displayed parameter oracle.</summary>
    private sealed class Fixture
    {
        /// <summary>Initializes the fixture.</summary>
        /// <param name="kind">The control family.</param>
        /// <param name="analysis">The core analysis.</param>
        /// <param name="wrapper">The UI wrapper.</param>
        /// <param name="summaryRow">The parameter row.</param>
        /// <param name="meanSummary">The expected posterior mean.</param>
        /// <param name="modeSummary">The expected mode.</param>
        internal Fixture(string kind, ModelAnalyses.AnalysisBase analysis, object wrapper, int summaryRow, double meanSummary, double modeSummary)
        { Kind = kind; Analysis = analysis; Wrapper = wrapper; SummaryRow = summaryRow; MeanSummary = meanSummary; ModeSummary = modeSummary; }
        internal string Kind { get; }
        internal ModelAnalyses.AnalysisBase Analysis { get; }
        internal object Wrapper { get; }
        internal int SummaryRow { get; }
        internal double MeanSummary { get; }
        internal double ModeSummary { get; }
    }
}
