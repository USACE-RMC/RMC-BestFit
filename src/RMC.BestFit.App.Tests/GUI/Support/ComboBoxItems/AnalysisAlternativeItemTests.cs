using FrameworkInterfaces;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Numerics.Distributions;
using Numerics.Utilities;
using RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;
using RMC.BestFit.UI;
using System;
using System.ComponentModel;
using System.Data;
using System.Threading.Tasks;
using System.Windows.Media;

namespace RMC.BestFit.App.Tests.GUI.Support.ComboBoxItems
{
    /// <summary>
    /// Minimal fake <see cref="IAnalysisElement"/> for <see cref="RMC_BestFit.AnalysisAlternativeItem"/>
    /// tests. Tracks the most recent <c>PropertyChanged</c> subscriber count so the test can assert
    /// <c>Dispose</c> unsubscribes correctly.
    /// </summary>
    internal sealed class TrackingAnalysisElement : IAnalysisElement
    {
        /// <summary>
        /// Fires <see cref="INotifyPropertyChanged.PropertyChanged"/> with the supplied property name —
        /// helper used by tests to verify propagation logic.
        /// </summary>
        public void RaiseChange(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Indicates whether at least one external subscriber is attached. Tests use this to verify
        /// that <see cref="RMC_BestFit.AnalysisAlternativeItem.Dispose"/> unhooks its subscription.
        /// </summary>
        public bool HasSubscribers => PropertyChanged != null;

        /// <inheritdoc/>
        public event PropertyChangedEventHandler PropertyChanged;

        // --- IElement / IMetaData / ISave ---

        /// <inheritdoc/>
        public string DisplayName => "Tracking";
        /// <inheritdoc/>
        public ImageSource ElementImage => null;
        /// <inheritdoc/>
        public string Name { get; set; } = "Track";
        /// <inheritdoc/>
        public string Description { get; set; } = string.Empty;
        /// <inheritdoc/>
        public DateTime CreationDate { get; set; } = DateTime.MinValue;
        /// <inheritdoc/>
        public DateTime LastModified { get; set; } = DateTime.MinValue;
        /// <inheritdoc/>
        public bool IsDirty => false;
        /// <inheritdoc/>
        public event PreviewObjectSavedEventHandler PreviewObjectSaved { add { } remove { } }
        /// <inheritdoc/>
        public event ObjectSavedEventHandler ObjectSaved { add { } remove { } }
        /// <inheritdoc/>
        public string NameOnDisk => throw new NotImplementedException();
        /// <inheritdoc/>
        public bool CanCopyFromExternal => throw new NotImplementedException();
        /// <inheritdoc/>
        public bool IsValid => throw new NotImplementedException();
        /// <inheritdoc/>
        public IElementCollection ParentCollection => throw new NotImplementedException();
        /// <inheritdoc/>
        public event PreviewDeletedEventHandler PreviewDeleted { add { } remove { } }
        /// <inheritdoc/>
        public event DeletedEventHandler Deleted { add { } remove { } }
        /// <inheritdoc/>
        public IElement Copy(string newName = "") => throw new NotImplementedException();
        /// <inheritdoc/>
        public IElement CopyFromExternal(string itemName, string fullFileName) => throw new NotImplementedException();
        /// <inheritdoc/>
        public void Open() => throw new NotImplementedException();
        /// <inheritdoc/>
        public void Save() => throw new NotImplementedException();
        /// <inheritdoc/>
        public void Delete() => throw new NotImplementedException();
        /// <inheritdoc/>
        public void SetIsValid(bool isValid, string message) => throw new NotImplementedException();
        /// <inheritdoc/>
        public DataTable CreateTable() => throw new NotImplementedException();
        /// <inheritdoc/>
        public System.Collections.Generic.List<string> RequiredColumns() => throw new NotImplementedException();

        // --- IAnalysisElement ---

        /// <inheritdoc/>
        public IAnalysis InnerAnalysis => null;
        /// <inheritdoc/>
        public BayesianAnalysis BayesianAnalysis => null;
        /// <inheritdoc/>
        public UncertaintyAnalysisResults AnalysisResults => null;
        /// <inheritdoc/>
        public Task RunAsync(Numerics.Utilities.SafeProgressReporter progressReporter) => Task.CompletedTask;
        /// <inheritdoc/>
        public void CancelAnalysis() { }
        /// <inheritdoc/>
        public bool IsEstimated => false;
    }

    /// <summary>
    /// Unit tests for <see cref="RMC_BestFit.AnalysisAlternativeItem"/>.
    /// </summary>
    [TestClass]
    public class AnalysisAlternativeItemTests
    {
        /// <summary>
        /// Verifies the constructor stores the supplied <c>IAnalysisElement</c> as the
        /// <c>Alternative</c> property.
        /// </summary>
        [STATestMethod]
        public void Constructor_StoresAlternative()
        {
            var fake = new TrackingAnalysisElement();
            var item = new RMC_BestFit.AnalysisAlternativeItem(fake);
            Assert.AreSame(fake, item.Alternative);
        }

        /// <summary>
        /// Verifies the constructor creates the credible-intervals area series with the expected
        /// <c>Name</c>.
        /// </summary>
        [STATestMethod]
        public void Constructor_CreatesCredibleIntervalsSeries()
        {
            var item = new RMC_BestFit.AnalysisAlternativeItem(new TrackingAnalysisElement());
            Assert.IsNotNull(item.CredibleIntervals);
            Assert.AreEqual("CredibleIntervals", item.CredibleIntervals.Name);
        }

        /// <summary>
        /// Verifies the constructor creates the posterior-predictive line series with the expected
        /// <c>Name</c> and <c>Title</c>.
        /// </summary>
        [STATestMethod]
        public void Constructor_CreatesPosteriorPredictiveSeries()
        {
            var item = new RMC_BestFit.AnalysisAlternativeItem(new TrackingAnalysisElement());
            Assert.IsNotNull(item.PosteriorPredictive);
            Assert.AreEqual("PosteriorPredictive", item.PosteriorPredictive.Name);
            Assert.AreEqual("Posterior Predictive", item.PosteriorPredictive.Title);
        }

        /// <summary>
        /// Verifies the constructor creates the posterior-mode line series with the expected
        /// <c>Name</c> and <c>Title</c>.
        /// </summary>
        [STATestMethod]
        public void Constructor_CreatesPosteriorModeSeries()
        {
            var item = new RMC_BestFit.AnalysisAlternativeItem(new TrackingAnalysisElement());
            Assert.IsNotNull(item.PosteriorMode);
            Assert.AreEqual("PosteriorMode", item.PosteriorMode.Name);
            Assert.AreEqual("Posterior Mode", item.PosteriorMode.Title);
        }

        /// <summary>
        /// Verifies the constructor assigns OxyPlot-compatible tracker strings to every
        /// reusable plot series created for the alternative.
        /// </summary>
        [STATestMethod]
        public void Constructor_AssignsTrackerFormatStrings()
        {
            var item = new RMC_BestFit.AnalysisAlternativeItem(new TrackingAnalysisElement());
            const string expected = "{0}\n{1}: {2}\n{3}: {4}";
            Assert.AreEqual(expected, item.CredibleIntervals.TrackerFormatString);
            Assert.AreEqual(expected, item.PosteriorPredictive.TrackerFormatString);
            Assert.AreEqual(expected, item.PosteriorMode.TrackerFormatString);
        }

        /// <summary>
        /// Verifies the initial <c>IsChecked</c> state is <c>false</c>.
        /// </summary>
        [STATestMethod]
        public void IsChecked_InitialValue_IsFalse()
        {
            var item = new RMC_BestFit.AnalysisAlternativeItem(new TrackingAnalysisElement());
            Assert.IsFalse(item.IsChecked);
        }

        /// <summary>
        /// Verifies setting <c>IsChecked</c> to a new value updates the property.
        /// </summary>
        [STATestMethod]
        public void IsChecked_SetToTrue_UpdatesValue()
        {
            var item = new RMC_BestFit.AnalysisAlternativeItem(new TrackingAnalysisElement());
            item.IsChecked = true;
            Assert.IsTrue(item.IsChecked);
        }

        /// <summary>
        /// Verifies setting <c>IsChecked</c> fires <c>PropertyChanged</c> with the correct property name.
        /// </summary>
        [STATestMethod]
        public void IsChecked_SetNewValue_FiresPropertyChanged()
        {
            var item = new RMC_BestFit.AnalysisAlternativeItem(new TrackingAnalysisElement());
            int notifications = 0;
            item.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(item.IsChecked)) notifications++; };
            item.IsChecked = true;
            Assert.AreEqual(1, notifications);
        }

        /// <summary>
        /// Verifies setting <c>IsChecked</c> to its current value does NOT fire <c>PropertyChanged</c>.
        /// </summary>
        [STATestMethod]
        public void IsChecked_SetSameValue_DoesNotFirePropertyChanged()
        {
            var item = new RMC_BestFit.AnalysisAlternativeItem(new TrackingAnalysisElement());
            int notifications = 0;
            item.PropertyChanged += (_, e) => notifications++;
            item.IsChecked = false; // already false
            Assert.AreEqual(0, notifications);
        }

        /// <summary>
        /// Verifies the wrapper propagates <c>AnalysisResults</c> changes from the wrapped
        /// alternative to its own subscribers.
        /// </summary>
        [STATestMethod]
        public void AlternativePropertyChanged_AnalysisResults_IsPropagated()
        {
            var fake = new TrackingAnalysisElement();
            var item = new RMC_BestFit.AnalysisAlternativeItem(fake);
            string lastPropertyName = null;
            item.PropertyChanged += (_, e) => lastPropertyName = e.PropertyName;
            fake.RaiseChange(nameof(IAnalysisElement.AnalysisResults));
            Assert.AreEqual(nameof(IAnalysisElement.AnalysisResults), lastPropertyName);
        }

        /// <summary>
        /// Verifies the wrapper propagates <c>IsEstimated</c> changes from the wrapped alternative.
        /// </summary>
        [STATestMethod]
        public void AlternativePropertyChanged_IsEstimated_IsPropagated()
        {
            var fake = new TrackingAnalysisElement();
            var item = new RMC_BestFit.AnalysisAlternativeItem(fake);
            string lastPropertyName = null;
            item.PropertyChanged += (_, e) => lastPropertyName = e.PropertyName;
            fake.RaiseChange(nameof(IAnalysisElement.IsEstimated));
            Assert.AreEqual(nameof(IAnalysisElement.IsEstimated), lastPropertyName);
        }

        /// <summary>
        /// Verifies the wrapper does NOT propagate unrelated property changes (filter contract).
        /// </summary>
        [STATestMethod]
        public void AlternativePropertyChanged_UnrelatedProperty_IsNotPropagated()
        {
            var fake = new TrackingAnalysisElement();
            var item = new RMC_BestFit.AnalysisAlternativeItem(fake);
            int count = 0;
            item.PropertyChanged += (_, _) => count++;
            fake.RaiseChange("SomeOtherProperty");
            Assert.AreEqual(0, count);
        }

        /// <summary>
        /// Verifies <see cref="RMC_BestFit.AnalysisAlternativeItem.Dispose"/> unsubscribes the
        /// internal handler from the wrapped alternative.
        /// </summary>
        [STATestMethod]
        public void Dispose_UnsubscribesFromAlternative()
        {
            var fake = new TrackingAnalysisElement();
            var item = new RMC_BestFit.AnalysisAlternativeItem(fake);
            Assert.IsTrue(fake.HasSubscribers, "subscriber attached after construction");
            item.Dispose();
            Assert.IsFalse(fake.HasSubscribers, "Dispose() should remove the subscription");
        }
    }
}
