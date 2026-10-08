using FrameworkInterfaces;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RMC.BestFit.UI;
using RMC_BestFit;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace RMC.BestFit.App.Tests.GUI.Support.Controls
{
    /// <summary>
    /// Exercises comparison refresh through the real selector and each XAML-wired analysis host.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public sealed class AlternativeControlRefreshTests
    {
        /// <summary>Removes this test's dispatcher-owned resources before another STA test runs.</summary>
        [TestCleanup]
        public void Cleanup() => WpfTestHost.ReleaseResources();

        /// <summary>
        /// Verifies worker notifications reach the actual host on its owning dispatcher without throwing.
        /// </summary>
        /// <param name="family">The analysis view hosting the shared selector.</param>
        /// <param name="propertyName">The result or status notification forwarded by an alternative.</param>
        [STATestMethod]
        [DataRow("Univariate", "AnalysisResults")]
        [DataRow("Univariate", "IsEstimated")]
        [DataRow("Mixture", "AnalysisResults")]
        [DataRow("Mixture", "IsEstimated")]
        [DataRow("PointProcess", "AnalysisResults")]
        [DataRow("PointProcess", "IsEstimated")]
        [DataRow("Composite", "AnalysisResults")]
        [DataRow("Composite", "IsEstimated")]
        [DataRow("B17C", "AnalysisResults")]
        [DataRow("B17C", "IsEstimated")]
        [DataRow("RatingCurve", "AnalysisResults")]
        [DataRow("RatingCurve", "IsEstimated")]
        [DataRow("TimeSeries", "AnalysisResults")]
        [DataRow("TimeSeries", "IsEstimated")]
        [DataRow("CoincidentFrequency", "AnalysisResults")]
        [DataRow("CoincidentFrequency", "IsEstimated")]
        public void WorkerNotification_RefreshesEveryHostOnDispatcher(string family, string propertyName)
        {
            using var fixture = new ComparisonFixture(family);
            int owningThread = Environment.CurrentManagedThreadId;
            int callbackThread = -1;
            int calls = 0;
            fixture.Selector.AnalysisAdded += _ => { callbackThread = Environment.CurrentManagedThreadId; calls++; };
            fixture.Item.IsChecked = true;
            Assert.AreEqual(1, calls, "Checking an alternative must still notify synchronously on the owning thread.");
            calls = 0;

            Task.Run(() => fixture.Notify(propertyName)).GetAwaiter().GetResult();
            WpfTestHost.DrainDispatcher();

            Assert.AreEqual(1, calls, "One source notification must produce one comparison refresh.");
            Assert.AreEqual(owningThread, callbackThread);
        }

        /// <summary>
        /// Verifies queued results cannot restore an unchecked, removed, or detached comparison.
        /// </summary>
        /// <param name="change">The user action performed before the dispatcher consumes the result.</param>
        [STATestMethod]
        [DataRow("Uncheck")]
        [DataRow("Remove")]
        [DataRow("ClearOwner")]
        [DataRow("ReplaceOwner")]
        public void QueuedNotification_IgnoresStaleComparison(string change)
        {
            using var fixture = new ComparisonFixture("Mixture");
            fixture.Item.IsChecked = true;
            int calls = 0;
            fixture.Selector.AnalysisAdded += _ => calls++;
            Task.Run(() => fixture.Notify(nameof(IAnalysisElement.AnalysisResults))).GetAwaiter().GetResult();

            switch (change)
            {
                case "Uncheck": fixture.Item.IsChecked = false; break;
                case "Remove": fixture.Collection.Remove(fixture.Alternative); break;
                case "ClearOwner": fixture.Selector.Element = null; break;
                case "ReplaceOwner":
                    fixture.Selector.Element = new MixtureAnalysis("Replacement", fixture.Collection);
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(change));
            }
            WpfTestHost.DrainDispatcher();
            Assert.AreEqual(0, calls, "A completed background update must not resurrect an old comparison.");
        }

        /// <summary>
        /// Verifies rebinding the shared selector does not duplicate source subscriptions.
        /// </summary>
        [STATestMethod]
        public void Rebind_RefreshesCheckedAlternativeOnce()
        {
            using var fixture = new ComparisonFixture("Mixture");
            var owner = fixture.Selector.Element;
            for (int i = 0; i < 3; i++)
            {
                fixture.Selector.Element = null;
                fixture.Selector.Element = owner;
            }
            fixture.Selector.AnalysisList[0].IsChecked = true;
            int calls = 0;
            fixture.Selector.AnalysisAdded += _ => calls++;
            Task.Run(() => fixture.Notify(nameof(IAnalysisElement.AnalysisResults))).GetAwaiter().GetResult();
            WpfTestHost.DrainDispatcher();
            Assert.AreEqual(1, calls);
        }

        /// <summary>
        /// Hosts a real view and matching UI elements without invoking unrelated SQLite persistence.
        /// </summary>
        private sealed class ComparisonFixture : IDisposable
        {
            /// <summary>The view containing the XAML-connected selector.</summary>
            private readonly UserControl _host;
            /// <summary>The host's strongly typed element property.</summary>
            private readonly PropertyInfo _elementProperty;
            /// <summary>The real base notification method used by analysis wrappers.</summary>
            private readonly MethodInfo _raisePropertyChange;
            /// <summary>Gets the real source collection observed by the selector.</summary>
            internal ElementCollectionBase Collection { get; }
            /// <summary>Gets the wrapped alternative whose worker notifications are tested.</summary>
            internal IAnalysisElement Alternative { get; }
            /// <summary>Gets the shared selector attached to the host.</summary>
            internal AlternativeControl Selector { get; }
            /// <summary>Gets the original comparison item.</summary>
            internal AnalysisAlternativeItem Item { get; }

            /// <summary>
            /// Creates the selected analysis host and lets its ordinary Element callback wire the selector.
            /// </summary>
            /// <param name="family">The analysis family to construct.</param>
            internal ComparisonFixture(string family)
            {
                WpfTestHost.EnsureResources();
                var project = BestFitProject.GetInstance();
                string controlType;
                Type elementType;
                switch (family)
                {
                    case "Univariate": elementType = typeof(UnivariateAnalysis); controlType = "UnivariateAnalysisControl"; break;
                    case "Mixture": elementType = typeof(MixtureAnalysis); controlType = "MixtureAnalysisControl"; break;
                    case "PointProcess": elementType = typeof(PointProcessAnalysis); controlType = "PointProcessAnalysisControl"; break;
                    case "Composite": elementType = typeof(CompositeAnalysis); controlType = "CompositeAnalysisControl"; break;
                    case "B17C": elementType = typeof(B17CAnalysis); controlType = "B17CAnalysisControl"; break;
                    case "RatingCurve": elementType = typeof(RatingCurveAnalysis); controlType = "RatingCurveAnalysisControl"; break;
                    case "TimeSeries": elementType = typeof(TimeSeriesAnalysis); controlType = "TimeSeriesAnalysisControl"; break;
                    case "CoincidentFrequency": elementType = typeof(CoincidentFrequencyAnalysis); controlType = "CoincidentFrequencyControl"; break;
                    default: throw new ArgumentOutOfRangeException(nameof(family));
                }
                Collection = family switch
                {
                    "RatingCurve" => new RatingCurveAnalysisCollection(project),
                    "TimeSeries" => new TimeSeriesAnalysisCollection(project),
                    "CoincidentFrequency" => new BivariateAnalysisCollection(project),
                    _ => new UnivariateAnalysisCollection(project)
                };
                var owner = (IAnalysisElement)Activator.CreateInstance(elementType, "Current", Collection, false);
                Alternative = (IAnalysisElement)Activator.CreateInstance(elementType, "Alternative", Collection, false);
                // Populate storage only; the production selector still enumerates the real collection
                // and creates/subscribes its own items through its ordinary Element callback.
                var elements = (List<IElement>)typeof(ElementCollectionBase)
                    .GetField("ElementList", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Collection);
                elements.Add(Alternative);
                _host = (UserControl)Activator.CreateInstance(typeof(AlternativeControl).Assembly.GetType("RMC_BestFit." + controlType));
                _elementProperty = _host.GetType().GetProperty("Element");
                _elementProperty.SetValue(_host, owner);
                Selector = (AlternativeControl)_host.FindName("AlternativeSelector");
                Assert.IsNotNull(Selector);
                Assert.AreEqual(1, Selector.AnalysisList.Count);
                Item = Selector.AnalysisList[0];
                _raisePropertyChange = typeof(ElementBase).GetMethod("RaisePropertyChange", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                WpfTestHost.DrainDispatcher();
            }

            /// <summary>
            /// Raises an ordinary result/status notification through the real element-to-selector event chain.
            /// </summary>
            /// <param name="propertyName">The property notification to publish.</param>
            internal void Notify(string propertyName)
            {
                _raisePropertyChange.Invoke(Alternative, new object[] { propertyName, false });
            }

            /// <summary>Detaches the controls and drains callbacks before leaving the owning STA thread.</summary>
            public void Dispose()
            {
                Selector.Element = null;
                _elementProperty.SetValue(_host, null);
                WpfTestHost.DrainDispatcher();
            }
        }
    }
}
