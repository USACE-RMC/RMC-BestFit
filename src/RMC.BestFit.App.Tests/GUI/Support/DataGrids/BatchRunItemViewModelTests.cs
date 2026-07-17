using FrameworkInterfaces;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Numerics.Distributions;
using Numerics.Utilities;
using RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;
using RMC.BestFit.UI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Threading.Tasks;
using System.Windows.Media;

namespace RMC.BestFit.App.Tests.GUI.Support.DataGrids
{
    #region Fakes

    /// <summary>
    /// Minimal fake implementation of <see cref="IElement"/> for use in unit tests.
    /// Only <see cref="DisplayName"/> and <see cref="ElementImage"/> are meaningful;
    /// all other members are no-ops or throw <see cref="NotImplementedException"/>
    /// because <see cref="RMC_BestFit.BatchRunItemViewModel"/> never invokes them.
    /// </summary>
    internal class FakeElement : IElement
    {
        /// <inheritdoc/>
        public string DisplayName => "Fake Analysis";

        /// <inheritdoc/>
        public ImageSource ElementImage => null;

        // --- IMetaData ---

        /// <inheritdoc/>
        public string Name { get; set; } = "Fake";

        /// <inheritdoc/>
        public string Description { get; set; } = string.Empty;

        /// <inheritdoc/>
        public DateTime CreationDate { get; set; } = DateTime.Now;

        /// <inheritdoc/>
        public DateTime LastModified { get; set; } = DateTime.Now;

        // --- ISave ---

        /// <inheritdoc/>
        public bool IsDirty => false;

        /// <inheritdoc/>
        public event PreviewObjectSavedEventHandler PreviewObjectSaved { add { } remove { } }

        /// <inheritdoc/>
        public event ObjectSavedEventHandler ObjectSaved { add { } remove { } }

        // --- IElement ---

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
        public List<string> RequiredColumns() => throw new NotImplementedException();

        // --- INotifyPropertyChanged ---

        /// <inheritdoc/>
        public event PropertyChangedEventHandler PropertyChanged { add { } remove { } }
    }

    /// <summary>
    /// Minimal fake implementation of <see cref="IAnalysisElement"/> for use in unit tests.
    /// All analysis members throw <see cref="NotImplementedException"/> because
    /// <see cref="RMC_BestFit.BatchRunItemViewModel"/> never invokes them in these tests.
    /// </summary>
    internal class FakeAnalysisElement : FakeElement, IAnalysisElement
    {
        /// <inheritdoc/>
        public IAnalysis InnerAnalysis => throw new NotImplementedException();

        /// <inheritdoc/>
        public BayesianAnalysis BayesianAnalysis => throw new NotImplementedException();

        /// <inheritdoc/>
        public UncertaintyAnalysisResults AnalysisResults => throw new NotImplementedException();

        /// <inheritdoc/>
        public Task RunAsync(SafeProgressReporter progressReporter) => throw new NotImplementedException();

        /// <inheritdoc/>
        public void CancelAnalysis() => throw new NotImplementedException();

        /// <inheritdoc/>
        public bool IsEstimated => false;
    }

    #endregion

    /// <summary>
    /// Unit tests for <see cref="RMC_BestFit.BatchRunItemViewModel"/>.
    /// </summary>
    [TestClass]
    public class BatchRunItemViewModelTests
    {
        /// <summary>
        /// Creates a <see cref="RMC_BestFit.BatchRunItemViewModel"/> with fake dependencies
        /// that satisfy the constructor null-guard requirements.
        /// </summary>
        private static RMC_BestFit.BatchRunItemViewModel CreateViewModel()
        {
            var element = new FakeElement();
            var analysis = new FakeAnalysisElement();
            return new RMC_BestFit.BatchRunItemViewModel(element, analysis);
        }

        // ----------------------------------------------------------------
        // Constructor
        // ----------------------------------------------------------------

        /// <summary>
        /// Verifies that the constructor stores the element reference correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_StoresElement()
        {
            var element = new FakeElement();
            var analysis = new FakeAnalysisElement();
            var vm = new RMC_BestFit.BatchRunItemViewModel(element, analysis);
            Assert.AreSame(element, vm.Element);
        }

        /// <summary>
        /// Verifies that the constructor stores the analysis reference correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_StoresAnalysis()
        {
            var element = new FakeElement();
            var analysis = new FakeAnalysisElement();
            var vm = new RMC_BestFit.BatchRunItemViewModel(element, analysis);
            Assert.AreSame(analysis, vm.Analysis);
        }

        /// <summary>
        /// Verifies that the constructor throws ArgumentNullException when element is null.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Constructor_NullElement_ThrowsArgumentNullException()
        {
            var analysis = new FakeAnalysisElement();
            _ = new RMC_BestFit.BatchRunItemViewModel(null, analysis);
        }

        /// <summary>
        /// Verifies that the constructor throws ArgumentNullException when analysis is null.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Constructor_NullAnalysis_ThrowsArgumentNullException()
        {
            var element = new FakeElement();
            _ = new RMC_BestFit.BatchRunItemViewModel(element, null);
        }

        // ----------------------------------------------------------------
        // Initial State
        // ----------------------------------------------------------------

        /// <summary>
        /// Verifies that the initial Status is Pending.
        /// </summary>
        [TestMethod]
        public void InitialState_Status_IsPending()
        {
            var vm = CreateViewModel();
            Assert.AreEqual(RMC_BestFit.BatchRunStatus.Pending, vm.Status);
        }

        /// <summary>
        /// Verifies that the initial Progress is 0.
        /// </summary>
        [TestMethod]
        public void InitialState_Progress_IsZero()
        {
            var vm = CreateViewModel();
            Assert.AreEqual(0.0, vm.Progress, 1e-12);
        }

        /// <summary>
        /// Verifies that the initial StatusMessage is "Waiting...".
        /// </summary>
        [TestMethod]
        public void InitialState_StatusMessage_IsWaiting()
        {
            var vm = CreateViewModel();
            Assert.AreEqual("Waiting...", vm.StatusMessage);
        }

        /// <summary>
        /// Verifies that the initial Duration is null.
        /// </summary>
        [TestMethod]
        public void InitialState_Duration_IsNull()
        {
            var vm = CreateViewModel();
            Assert.IsNull(vm.Duration);
        }

        /// <summary>
        /// Verifies that the initial DurationText is empty string when Duration is null.
        /// </summary>
        [TestMethod]
        public void InitialState_DurationText_IsEmpty()
        {
            var vm = CreateViewModel();
            Assert.AreEqual(string.Empty, vm.DurationText);
        }

        /// <summary>
        /// Verifies that the initial ProgressText is empty string when Status is Pending.
        /// </summary>
        [TestMethod]
        public void InitialState_ProgressText_IsEmptyWhenPending()
        {
            var vm = CreateViewModel();
            Assert.AreEqual(string.Empty, vm.ProgressText);
        }

        /// <summary>
        /// Verifies that the initial progress bar state is determinate while pending.
        /// </summary>
        [TestMethod]
        public void InitialState_IsProgressIndeterminate_IsFalse()
        {
            var vm = CreateViewModel();
            Assert.IsFalse(vm.IsProgressIndeterminate);
        }

        // ----------------------------------------------------------------
        // Status transitions
        // ----------------------------------------------------------------

        /// <summary>
        /// Verifies that setting Status to Running fires PropertyChanged for Status and ProgressText.
        /// </summary>
        [TestMethod]
        public void Status_SetToRunning_FiresPropertyChanged()
        {
            var vm = CreateViewModel();
            var changed = new System.Collections.Generic.List<string>();
            vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
            vm.Status = RMC_BestFit.BatchRunStatus.Running;
            CollectionAssert.Contains(changed, nameof(vm.Status));
            CollectionAssert.Contains(changed, nameof(vm.ProgressText));
            CollectionAssert.Contains(changed, nameof(vm.IsProgressIndeterminate));
        }

        /// <summary>
        /// Verifies that setting Status to the same value does NOT fire PropertyChanged.
        /// </summary>
        [TestMethod]
        public void Status_SetToSameValue_DoesNotFirePropertyChanged()
        {
            var vm = CreateViewModel();
            var changed = new System.Collections.Generic.List<string>();
            vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
            vm.Status = RMC_BestFit.BatchRunStatus.Pending; // already Pending
            Assert.AreEqual(0, changed.Count);
        }

        /// <summary>
        /// Verifies transitions through all BatchRunStatus values.
        /// </summary>
        [TestMethod]
        public void Status_AllTransitions_StoreValue()
        {
            var vm = CreateViewModel();
            foreach (RMC_BestFit.BatchRunStatus s in System.Enum.GetValues(typeof(RMC_BestFit.BatchRunStatus)))
            {
                vm.Status = s;
                Assert.AreEqual(s, vm.Status, $"Status mismatch for {s}");
            }
        }

        // ----------------------------------------------------------------
        // Progress
        // ----------------------------------------------------------------

        /// <summary>
        /// Verifies that setting Progress fires PropertyChanged for Progress and ProgressText.
        /// </summary>
        [TestMethod]
        public void Progress_SetNewValue_FiresPropertyChanged()
        {
            var vm = CreateViewModel();
            vm.Status = RMC_BestFit.BatchRunStatus.Running; // need Running for ProgressText to be non-empty
            var changed = new System.Collections.Generic.List<string>();
            vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
            vm.Progress = 50.0;
            CollectionAssert.Contains(changed, nameof(vm.Progress));
            CollectionAssert.Contains(changed, nameof(vm.ProgressText));
            CollectionAssert.Contains(changed, nameof(vm.IsProgressIndeterminate));
        }

        /// <summary>
        /// Verifies that setting Progress to a value within tolerance does NOT fire PropertyChanged.
        /// </summary>
        [TestMethod]
        public void Progress_SetNearlyIdenticalValue_DoesNotFirePropertyChanged()
        {
            var vm = CreateViewModel();
            vm.Progress = 50.0;
            var changed = new System.Collections.Generic.List<string>();
            vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
            vm.Progress = 50.005; // within 0.01 tolerance
            Assert.AreEqual(0, changed.Count);
        }

        // ----------------------------------------------------------------
        // ProgressText
        // ----------------------------------------------------------------

        /// <summary>
        /// Verifies that ProgressText shows a starting message when Running and progress is zero.
        /// </summary>
        [TestMethod]
        public void ProgressText_RunningZeroPercent_ShowsStarting()
        {
            var vm = CreateViewModel();
            vm.Status = RMC_BestFit.BatchRunStatus.Running;
            vm.Progress = 0.0;
            Assert.AreEqual("Starting...", vm.ProgressText);
        }

        /// <summary>
        /// Verifies that ProgressText shows percentage when Running and progress is under 100.
        /// </summary>
        [TestMethod]
        public void ProgressText_Running50Percent_ShowsPercentage()
        {
            var vm = CreateViewModel();
            vm.Status = RMC_BestFit.BatchRunStatus.Running;
            vm.Progress = 50.0;
            Assert.AreEqual("50% Complete", vm.ProgressText);
        }

        /// <summary>
        /// Verifies that ProgressText shows "Processing Results..." when Running and progress reaches 99.
        /// </summary>
        [TestMethod]
        public void ProgressText_Running99Percent_ShowsProcessing()
        {
            var vm = CreateViewModel();
            vm.Status = RMC_BestFit.BatchRunStatus.Running;
            vm.Progress = 99.0;
            Assert.AreEqual("Processing Results...", vm.ProgressText);
        }

        /// <summary>
        /// Verifies that ProgressText still shows processing if a running row receives 100 before completion state.
        /// </summary>
        [TestMethod]
        public void ProgressText_Running100Percent_ShowsProcessing()
        {
            var vm = CreateViewModel();
            vm.Status = RMC_BestFit.BatchRunStatus.Running;
            vm.Progress = 100.0;
            Assert.AreEqual("Processing Results...", vm.ProgressText);
        }

        /// <summary>
        /// Verifies that ProgressText is empty when Status is Succeeded (not Running).
        /// </summary>
        [TestMethod]
        public void ProgressText_Succeeded_IsEmpty()
        {
            var vm = CreateViewModel();
            vm.Status = RMC_BestFit.BatchRunStatus.Succeeded;
            Assert.AreEqual(string.Empty, vm.ProgressText);
        }

        /// <summary>
        /// Verifies that ProgressText is empty when Status is Failed.
        /// </summary>
        [TestMethod]
        public void ProgressText_Failed_IsEmpty()
        {
            var vm = CreateViewModel();
            vm.Status = RMC_BestFit.BatchRunStatus.Failed;
            Assert.AreEqual(string.Empty, vm.ProgressText);
        }

        // ----------------------------------------------------------------
        // IsProgressIndeterminate
        // ----------------------------------------------------------------

        /// <summary>
        /// Verifies that the row is indeterminate at the beginning of a run.
        /// </summary>
        [TestMethod]
        public void IsProgressIndeterminate_RunningAtZero_IsTrue()
        {
            var vm = CreateViewModel();
            vm.Status = RMC_BestFit.BatchRunStatus.Running;
            vm.Progress = 0.0;
            Assert.IsTrue(vm.IsProgressIndeterminate);
        }

        /// <summary>
        /// Verifies that the row is determinate while measurable progress is available.
        /// </summary>
        [TestMethod]
        public void IsProgressIndeterminate_RunningAtFifty_IsFalse()
        {
            var vm = CreateViewModel();
            vm.Status = RMC_BestFit.BatchRunStatus.Running;
            vm.Progress = 50.0;
            Assert.IsFalse(vm.IsProgressIndeterminate);
        }

        /// <summary>
        /// Verifies that the row becomes indeterminate while final results are processing.
        /// </summary>
        [TestMethod]
        public void IsProgressIndeterminate_RunningAtNinetyNine_IsTrue()
        {
            var vm = CreateViewModel();
            vm.Status = RMC_BestFit.BatchRunStatus.Running;
            vm.Progress = 99.0;
            Assert.IsTrue(vm.IsProgressIndeterminate);
        }

        /// <summary>
        /// Verifies that a running row remains indeterminate if it receives 100 before completion state.
        /// </summary>
        [TestMethod]
        public void IsProgressIndeterminate_RunningAtHundred_IsTrue()
        {
            var vm = CreateViewModel();
            vm.Status = RMC_BestFit.BatchRunStatus.Running;
            vm.Progress = 100.0;
            Assert.IsTrue(vm.IsProgressIndeterminate);
        }

        /// <summary>
        /// Verifies that completed rows do not keep the indeterminate animation.
        /// </summary>
        [TestMethod]
        public void IsProgressIndeterminate_SucceededAtHundred_IsFalse()
        {
            var vm = CreateViewModel();
            vm.Progress = 100.0;
            vm.Status = RMC_BestFit.BatchRunStatus.Succeeded;
            Assert.IsFalse(vm.IsProgressIndeterminate);
        }

        // ----------------------------------------------------------------
        // StatusMessage
        // ----------------------------------------------------------------

        /// <summary>
        /// Verifies that setting StatusMessage stores the new value.
        /// </summary>
        [TestMethod]
        public void StatusMessage_SetValue_StoresValue()
        {
            var vm = CreateViewModel();
            vm.StatusMessage = "Running analysis...";
            Assert.AreEqual("Running analysis...", vm.StatusMessage);
        }

        /// <summary>
        /// Verifies that setting StatusMessage fires PropertyChanged.
        /// </summary>
        [TestMethod]
        public void StatusMessage_SetValue_FiresPropertyChanged()
        {
            var vm = CreateViewModel();
            var changed = new System.Collections.Generic.List<string>();
            vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
            vm.StatusMessage = "Done";
            CollectionAssert.Contains(changed, nameof(vm.StatusMessage));
        }

        /// <summary>
        /// Verifies that setting StatusMessage to the same value does NOT fire PropertyChanged.
        /// </summary>
        [TestMethod]
        public void StatusMessage_SetSameValue_DoesNotFirePropertyChanged()
        {
            var vm = CreateViewModel();
            var changed = new System.Collections.Generic.List<string>();
            vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
            vm.StatusMessage = "Waiting..."; // same as initial
            Assert.AreEqual(0, changed.Count);
        }

        // ----------------------------------------------------------------
        // Duration / DurationText
        // ----------------------------------------------------------------

        /// <summary>
        /// Verifies that setting Duration stores the value and fires PropertyChanged for both Duration and DurationText.
        /// </summary>
        [TestMethod]
        public void Duration_SetValue_FiresPropertyChanged()
        {
            var vm = CreateViewModel();
            var changed = new System.Collections.Generic.List<string>();
            vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
            vm.Duration = TimeSpan.FromSeconds(135);
            CollectionAssert.Contains(changed, nameof(vm.Duration));
            CollectionAssert.Contains(changed, nameof(vm.DurationText));
        }

        /// <summary>
        /// Verifies that DurationText shows mm:ss format for durations under one hour.
        /// </summary>
        [TestMethod]
        public void DurationText_UnderOneHour_ShowsMinuteSeconds()
        {
            var vm = CreateViewModel();
            vm.Duration = TimeSpan.FromSeconds(135); // 2m15s
            Assert.AreEqual("02:15", vm.DurationText);
        }

        /// <summary>
        /// Verifies that DurationText shows h:mm:ss format for durations of one hour or more.
        /// </summary>
        [TestMethod]
        public void DurationText_OneHourOrMore_ShowsHoursMinutesSeconds()
        {
            var vm = CreateViewModel();
            vm.Duration = TimeSpan.FromSeconds(3750); // 1h2m30s
            Assert.AreEqual("1:02:30", vm.DurationText);
        }

        /// <summary>
        /// Verifies that DurationText is empty string when Duration is null.
        /// </summary>
        [TestMethod]
        public void DurationText_NullDuration_IsEmpty()
        {
            var vm = CreateViewModel();
            vm.Duration = null;
            Assert.AreEqual(string.Empty, vm.DurationText);
        }
    }
}
