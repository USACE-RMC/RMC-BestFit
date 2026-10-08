using GenericControls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Numerics.Distributions;
using Numerics.Distributions.Copulas;
using Numerics.Mathematics.Optimization;
using Numerics.Sampling.MCMC;
using OxyPlot;
using RMC.BestFit.Analyses;
using RMC.BestFit.App.Tests.GUI.Support;
using RMC.BestFit.Models;
using RMC.BestFit.UI;
using RMC_BestFit;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ModelCfa = RMC.BestFit.Analyses.CoincidentFrequencyAnalysis;
using ModelBivariate = RMC.BestFit.Analyses.BivariateAnalysis;
using UiCfa = RMC.BestFit.UI.CoincidentFrequencyAnalysis;
using UiBivariate = RMC.BestFit.UI.BivariateAnalysis;
using PointEstimator = RMC.BestFit.Estimation.BayesianAnalysis.PointEstimateType;
using WpfLineSeries = OxyPlot.Wpf.LineSeries;

namespace RMC.BestFit.App.Tests.GUI
{
    /// <summary>Exercises CFA point-estimator selection through real loaded App controls.</summary>
    /// <remarks>Stored copula and marginal posteriors avoid estimation. This is a binding contract, not saved-project validation.</remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class CoincidentFrequencyPointEstimatorControlTests
    {
        /// <summary>Provides access to stored analysis state and ordinary wrapper notification forwarding.</summary>
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        /// <summary>Releases resources installed for this STA test after all control handlers have detached.</summary>
        [TestCleanup]
        public void Cleanup()
        {
            WpfTestHost.DrainDispatcher();
            WpfTestHost.ReleaseResources();
        }

        /// <summary>Updates real plot geometry, summary moments, and table rows while retaining all three posteriors.</summary>
        [STATestMethod]
        public void LoadedControls_MeanModeMean_RefreshResultsAndPreservePosteriors()
        {
            WpfTestHost.EnsureResources();
            ModelCfa analysis = CreateAnalysis();
            Task initialize = analysis.UpdatePointEstimateResultsAsync();
            PumpUntil(() => initialize.IsCompleted, "Initial CFA point results did not complete.");
            initialize.GetAwaiter().GetResult();
            var collection = new BivariateAnalysisCollection(BestFitProject.GetInstance());
            string suffix = Guid.NewGuid().ToString("N").Substring(0, 8);
            var marginalX = CreateMarginalWrapper("MarginalX" + suffix,
                (RMC.BestFit.Models.UnivariateDistribution)analysis.BivariateAnalysis.BivariateDistribution.MarginalX,
                analysis.MarginalXChain);
            var marginalY = CreateMarginalWrapper("MarginalY" + suffix,
                (RMC.BestFit.Models.UnivariateDistribution)analysis.BivariateAnalysis.BivariateDistribution.MarginalY,
                analysis.MarginalYChain);
            var upstream = new UiBivariate("Copula" + suffix, collection)
            {
                MarginalX = marginalX, MarginalY = marginalY
            };
            ReplaceInner(upstream, analysis.BivariateAnalysis);
            var element = new UiCfa("Coincident" + suffix, collection) { BivariateAnalysis = upstream };
            // New CFA wrappers seed a zero row in each axis; replace those rows with the restored grid.
            element.XValues.Clear();
            element.YValues.Clear();
            foreach (double x in analysis.XValues) element.XValues.Add(x);
            foreach (double y in analysis.YValues) element.YValues.Add(y);
            element.BivariateResponse = (double[,])analysis.BivariateResponse.Clone();
            ReplaceInner(element, analysis);
            Assert.IsTrue(marginalX.IsValid && marginalX.IsEstimated);
            Assert.IsTrue(marginalY.IsValid && marginalY.IsEstimated);
            Assert.IsTrue(upstream.IsValid && upstream.IsEstimated);
            Assert.IsTrue(analysis.Validate().IsValid);
            Assert.IsTrue(element.IsValid && element.IsEstimated);
            var control = new CoincidentFrequencyControl { Element = element };
            var properties = new CoincidentFrequencyPropertiesControl { Element = element };
            try
            {
                control.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
                properties.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
                WpfTestHost.DrainDispatcher();
                var combo = (ComboBox)((ContentPropertyControl)properties.FindName("PointEstimator")).InnerContent;
                var posteriors = new[] { analysis.BivariateAnalysis.BayesianAnalysis.Results, analysis.MarginalXChain, analysis.MarginalYChain };
                var drawValues = posteriors.Select(p => p.Output.SelectMany(draw => draw.Values).ToArray()).ToArray();
                var intervals = analysis.AnalysisResults.ConfidenceIntervals;
                var intervalValues = intervals.Cast<double>().ToArray();
                var predictive = analysis.AnalysisResults.MeanCurve;
                var predictiveValues = (double[])predictive.Clone();
                double[] originalCurve = (double[])analysis.AnalysisResults.ModeCurve.Clone();
                double originalSummary = AssertDisplayed(control, element, PointEstimator.PosteriorMean);
                var gate = (SemaphoreSlim)typeof(AnalysisBase).GetField("_reprocessGate", PrivateInstance).GetValue(analysis);
                foreach (PointEstimator estimator in new[] { PointEstimator.PosteriorMode, PointEstimator.PosteriorMean })
                {
                    var completed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    PropertyChangedEventHandler handler = (_, args) =>
                    {
                        if (args.PropertyName == nameof(ModelCfa.AnalysisResults)) completed.TrySetResult(true);
                    };
                    analysis.PropertyChanged += handler;
                    try
                    {
                        combo.SelectedValue = estimator;
                        PumpUntil(() => !analysis.IsEstimated || (completed.Task.IsCompleted && gate.CurrentCount == 1),
                            "The actual bound CFA selector did not complete point-result reprocessing.");
                    }
                    finally
                    {
                        analysis.PropertyChanged -= handler;
                    }
                    Assert.IsTrue(analysis.IsEstimated);
                    Assert.IsTrue(element.IsValid);
                    Assert.IsTrue(upstream.IsValid && upstream.IsEstimated);
                    Assert.AreEqual(estimator, analysis.BayesianAnalysis.PointEstimator);
                    Assert.AreEqual(estimator, combo.SelectedValue);
                    double summary = AssertDisplayed(control, element, estimator);
                    if (estimator == PointEstimator.PosteriorMode)
                    {
                        Assert.IsFalse(originalCurve.SequenceEqual(analysis.AnalysisResults.ModeCurve));
                        Assert.AreNotEqual(originalSummary, summary);
                    }
                    else
                    {
                        CollectionAssert.AreEqual(originalCurve, analysis.AnalysisResults.ModeCurve);
                        Assert.AreEqual(originalSummary, summary);
                    }
                    var currentPosteriors = new[] { analysis.BivariateAnalysis.BayesianAnalysis.Results, analysis.MarginalXChain, analysis.MarginalYChain };
                    for (int i = 0; i < posteriors.Length; i++)
                    {
                        Assert.AreSame(posteriors[i], currentPosteriors[i]);
                        CollectionAssert.AreEqual(drawValues[i], currentPosteriors[i].Output.SelectMany(draw => draw.Values).ToArray());
                    }
                    Assert.IsNotNull(analysis.AnalysisResults.ConfidenceIntervals);
                    Assert.AreSame(intervals, analysis.AnalysisResults.ConfidenceIntervals);
                    CollectionAssert.AreEqual(intervalValues, analysis.AnalysisResults.ConfidenceIntervals.Cast<double>().ToArray());
                    Assert.AreSame(predictive, analysis.AnalysisResults.MeanCurve);
                    CollectionAssert.AreEqual(predictiveValues, analysis.AnalysisResults.MeanCurve);
                }
            }
            finally
            {
                properties.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
                control.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
                properties.Element = null;
                control.Element = null;
                WpfTestHost.DrainDispatcher();
                foreach (object wrapper in new object[] { element, upstream, marginalX, marginalY })
                    wrapper.GetType().GetMethod("DisposeBridges", PrivateInstance).Invoke(wrapper, null);
            }
        }

        /// <summary>Checks that the current model curve is shown consistently in the plot, table, and summary.</summary>
        /// <param name="control">The loaded main control.</param>
        /// <param name="element">The restored UI analysis.</param>
        /// <param name="estimator">The selected mean or mode.</param>
        /// <returns>The displayed distribution mean.</returns>
        private static double AssertDisplayed(CoincidentFrequencyControl control, UiCfa element, PointEstimator estimator)
        {
            WpfTestHost.DrainDispatcher();
            string title = estimator == PointEstimator.PosteriorMean ? "Posterior Mean" : "Posterior Mode";
            Assert.AreEqual(title, ((DataGridTextColumn)control.FindName("StatValueColumn")).Header);
            Assert.AreEqual(title, ((DataGridTextColumn)control.FindName("ModeColumn")).Header);
            var line = element.FrequencyPlot.Series.OfType<WpfLineSeries>().Single(series => series.Name == "PosteriorMode");
            Assert.AreEqual(title, line.Title);
            CollectionAssert.AreEqual(element.AnalysisResults.ModeCurve,
                line.ItemsSource.Cast<DataPoint>().Select(point => point.X).ToArray());
            CollectionAssert.AreEqual(element.ZOutputValues,
                line.ItemsSource.Cast<DataPoint>().Select(point => point.Y).ToArray());
            var rows = ((DataGrid)control.FindName("FrequencyCurveTable")).ItemsSource.Cast<CoincidentFrequencyTablePoint>().ToArray();
            CollectionAssert.AreEqual(element.AnalysisResults.ModeCurve, rows.Select(row => row.Mode).ToArray());
            CollectionAssert.AreEqual(element.ZOutputValues, rows.Select(row => row.Z).ToArray());
            double mean = ((DataGrid)control.FindName("SummaryStatisticsTable")).ItemsSource.Cast<SummaryStatistic>()
                .Single(statistic => statistic.Name == "Mean").Value;
            Assert.IsTrue(double.IsFinite(mean));
            double[] moments = element.GetPointEstimateDistribution().CentralMoments(1000);
            Assert.AreEqual(moments[0], mean);
            return mean;
        }

        /// <summary>Builds a small sum response with six stored draws for the copula and each Normal marginal.</summary>
        /// <returns>An estimated model analysis with stored non-null uncertainty bands.</returns>
        private static ModelCfa CreateAnalysis()
        {
            var frameX = new RMC.BestFit.Models.DataFrame();
            var frameY = new RMC.BestFit.Models.DataFrame();
            for (int i = 0; i < 20; i++)
            {
                frameX.ExactSeries.Add(new ExactData(1990 + i, -1.9 + 0.2 * i));
                frameY.ExactSeries.Add(new ExactData(1990 + i, -1.8 + 0.19 * i + 0.1 * Math.Sin(i)));
            }
            frameX.CalculatePlottingPositions();
            frameY.CalculatePlottingPositions();
            var x = new RMC.BestFit.Models.UnivariateDistribution(frameX, UnivariateDistributionType.Normal);
            var y = new RMC.BestFit.Models.UnivariateDistribution(frameY, UnivariateDistributionType.Normal);
            x.SetParameterValues(new[] { 0d, 1d });
            y.SetParameterValues(new[] { 0d, 1d });
            var model = new BivariateDistribution(x, y, CopulaType.Normal);
            model.Copula.SetCopulaParameters(new[] { 0.2 });
            var upstream = new ModelBivariate(model);
            upstream.BayesianAnalysis.SetCustomMCMCResults(CreatePosterior(new[] { 0.2 }), skipInformationCriteria: true);
            SetRestoredProperty(upstream, nameof(AnalysisBase.IsEstimated), true);
            var grid = new[] { -2d, -1, 0, 1, 2 };
            var response = new double[5, 5];
            for (int i = 0; i < 5; i++)
                for (int j = 0; j < 5; j++) response[i, j] = grid[i] + grid[j];
            var analysis = new ModelCfa(upstream, grid, (double[])grid.Clone(), response)
            {
                NumberOfBins = 15, MarginalXChain = CreatePosterior(new[] { 0d, 1d }),
                MarginalYChain = CreatePosterior(new[] { 0d, 1d })
            };
            analysis.SetZOutputValues(new[] { -4d, -2, 0, 2, 4 });
            var intervals = new double[5, 2];
            for (int i = 0; i < 5; i++) { intervals[i, 0] = 0.01; intervals[i, 1] = 0.99; }
            SetRestoredProperty(analysis, nameof(ModelCfa.AnalysisResults), new UncertaintyAnalysisResults
            {
                ModeCurve = new double[5], MeanCurve = new[] { 0.99, 0.8, 0.5, 0.2, 0.01 }, ConfidenceIntervals = intervals
            });
            SetRestoredProperty(analysis, nameof(AnalysisBase.IsEstimated), true);
            return analysis;
        }

        /// <summary>Creates a retained posterior and distinct MAP using fixed parameter perturbations.</summary>
        /// <param name="baseline">Valid configured model parameters.</param>
        /// <returns>The stored posterior object.</returns>
        private static MCMCResults CreatePosterior(double[] baseline)
        {
            var draws = new List<ParameterSet>();
            for (int i = 0; i < 6; i++)
            {
                var values = (double[])baseline.Clone();
                for (int j = 0; j < values.Length; j++) values[j] += (i - 2) * 0.005 * Math.Max(1, Math.Abs(values[j]));
                draws.Add(new ParameterSet(values, -10 + i));
            }
            var map = (double[])baseline.Clone();
            map[0] += 0.1 * Math.Max(1, Math.Abs(map[0]));
            return new MCMCResults(new ParameterSet(map, 0), draws, alpha: 0.1);
        }

        /// <summary>Restores a private-set result property without running estimation.</summary>
        /// <param name="target">The target analysis.</param>
        /// <param name="name">The restored property.</param>
        /// <param name="value">Its stored value.</param>
        private static void SetRestoredProperty(object target, string name, object value)
        {
            target.GetType().GetProperty(name).GetSetMethod(nonPublic: true).Invoke(target, new[] { value });
        }

        /// <summary>Creates a valid estimated marginal UI element backed by aligned exact input data and stored results.</summary>
        /// <param name="name">The distinct marginal name.</param>
        /// <param name="model">The configured Normal marginal model.</param>
        /// <param name="posterior">The stored marginal posterior used by CFA.</param>
        /// <returns>The complete valid marginal wrapper.</returns>
        private static RMC.BestFit.UI.UnivariateAnalysis CreateMarginalWrapper(string name,
            RMC.BestFit.Models.UnivariateDistribution model, MCMCResults posterior)
        {
            var analysis = new RMC.BestFit.Analyses.UnivariateAnalysis(model);
            analysis.BayesianAnalysis.SetCustomMCMCResults(posterior, skipInformationCriteria: true);
            SetRestoredProperty(analysis, nameof(AnalysisBase.IsEstimated), true);
            var project = BestFitProject.GetInstance();
            var wrapper = new RMC.BestFit.UI.UnivariateAnalysis(name, new UnivariateAnalysisCollection(project))
            {
                InputData = new InputData(name + "Data", new InputDataCollection(project)) { DataFrame = model.DataFrame }
            };
            ReplaceInner(wrapper, analysis);
            Assert.IsTrue(wrapper.InputData.IsValid);
            Assert.IsTrue(analysis.Validate().IsValid);
            return wrapper;
        }

        /// <summary>Attaches a stored model and restores ordinary inner subscriptions, plot bridges, and Bayesian-controller routing.</summary>
        /// <param name="wrapper">The bivariate or CFA UI wrapper.</param>
        /// <param name="analysis">The restored model analysis.</param>
        private static void ReplaceInner(object wrapper, AnalysisBase analysis)
        {
            Type type = wrapper.GetType();
            type.GetMethod("DisposeBridges", PrivateInstance).Invoke(wrapper, null);
            MethodInfo unsubscribe = type.GetMethod("UnsubscribeInnerAnalysis", PrivateInstance);
            MethodInfo subscribe = type.GetMethod("SubscribeInnerAnalysis", PrivateInstance);
            PropertyChangedEventHandler handler = null;
            if (unsubscribe != null) unsubscribe.Invoke(wrapper, null);
            else
            {
                var old = (INotifyPropertyChanged)type.GetProperty("InnerAnalysis").GetValue(wrapper);
                MethodInfo method = type.GetMethod("InnerAnalysis_PropertyChanged", PrivateInstance);
                handler = (PropertyChangedEventHandler)Delegate.CreateDelegate(typeof(PropertyChangedEventHandler), wrapper, method);
                old.PropertyChanged -= handler;
            }
            type.GetField("_innerAnalysis", PrivateInstance).SetValue(wrapper, analysis);
            if (subscribe != null) subscribe.Invoke(wrapper, null);
            else analysis.PropertyChanged += handler;
            type.GetMethod("SetupBridges", PrivateInstance).Invoke(wrapper, null);
            type.GetMethod("SetIsValid", PrivateInstance, null, Type.EmptyTypes, null).Invoke(wrapper, Array.Empty<object>());
        }

        /// <summary>Services WPF callbacks while waiting for deterministic postprocessing to finish.</summary>
        /// <param name="completed">The completion condition evaluated on the STA thread.</param>
        /// <param name="message">The bounded-wait failure message.</param>
        private static void PumpUntil(Func<bool> completed, string message)
        {
            var watch = Stopwatch.StartNew();
            while (!completed() && watch.Elapsed < TimeSpan.FromSeconds(5))
            {
                WpfTestHost.DrainDispatcher();
                Thread.Sleep(10);
            }
            WpfTestHost.DrainDispatcher();
            Assert.IsTrue(completed(), message);
        }
    }
}
