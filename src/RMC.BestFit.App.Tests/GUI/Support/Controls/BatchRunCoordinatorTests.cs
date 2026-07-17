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

namespace RMC.BestFit.App.Tests.GUI.Support.Controls
{
    /// <summary>
    /// Unit tests for <see cref="RMC_BestFit.BatchRunCoordinator"/>.
    /// </summary>
    [TestClass]
    public class BatchRunCoordinatorTests
    {
        /// <summary>
        /// Verifies non-analysis elements and analysis elements without an inner model analysis
        /// are not queued for batch execution.
        /// </summary>
        [TestMethod]
        public void BuildViewModels_SkipsNonRunnableElements()
        {
            var runnable = new FakeAnalysis();
            var estimated = new FakeAnalysis();
            var elements = new IElement[]
            {
                new FakeElement("plain"),
                new FakeAnalysisElement("missing", null, false),
                new FakeAnalysisElement("runnable", runnable, false),
                new FakeAnalysisElement("estimated", estimated, true)
            };

            var viewModels = RMC_BestFit.BatchRunCoordinator.BuildViewModels(elements);

            Assert.AreEqual(2, viewModels.Count);
            Assert.AreSame(runnable, viewModels[0].Analysis.InnerAnalysis);
            Assert.AreSame(estimated, viewModels[1].Analysis.InnerAnalysis);
            Assert.AreEqual(RMC_BestFit.BatchRunStatus.Succeeded, viewModels[1].Status);
            Assert.AreEqual(100, viewModels[1].Progress);
            Assert.AreEqual("Already estimated", viewModels[1].StatusMessage);
        }

        /// <summary>
        /// Verifies selected analyses are returned in row order.
        /// </summary>
        [TestMethod]
        public void SelectAnalyses_ReturnsInnerAnalysesInOrder()
        {
            var first = new FakeAnalysis();
            var second = new FakeAnalysis();
            var viewModels = RMC_BestFit.BatchRunCoordinator.BuildViewModels(new IElement[]
            {
                new FakeAnalysisElement("first", first, false),
                new FakeAnalysisElement("second", second, false)
            });

            List<IAnalysis> analyses = RMC_BestFit.BatchRunCoordinator.SelectAnalyses(viewModels);

            CollectionAssert.AreEqual(new IAnalysis[] { first, second }, analyses);
        }

        /// <summary>
        /// Verifies start and progress events update the matching row only.
        /// </summary>
        [TestMethod]
        public void MarkStartingAndApplyProgress_UpdateMatchingViewModel()
        {
            var first = new FakeAnalysis();
            var second = new FakeAnalysis();
            var viewModels = RMC_BestFit.BatchRunCoordinator.BuildViewModels(new IElement[]
            {
                new FakeAnalysisElement("first", first, false),
                new FakeAnalysisElement("second", second, false)
            });
            var map = RMC_BestFit.BatchRunCoordinator.BuildAnalysisMap(viewModels);

            RMC_BestFit.BatchRunCoordinator.MarkStarting(map, second);
            RMC_BestFit.BatchRunCoordinator.ApplyProgress(map, second, 35);

            Assert.AreEqual(RMC_BestFit.BatchRunStatus.Pending, viewModels[0].Status);
            Assert.AreEqual(0, viewModels[0].Progress);
            Assert.AreEqual(RMC_BestFit.BatchRunStatus.Running, viewModels[1].Status);
            Assert.AreEqual("Running...", viewModels[1].StatusMessage);
            Assert.AreEqual(35, viewModels[1].Progress);
        }

        /// <summary>
        /// Verifies successful completion applies the finished state.
        /// </summary>
        [TestMethod]
        public void ApplyResult_Succeeded_SetsCompleteState()
        {
            var analysis = new FakeAnalysis();
            var viewModel = CreateSingleViewModel(analysis);
            var map = RMC_BestFit.BatchRunCoordinator.BuildAnalysisMap(new[] { viewModel });
            var result = new BatchAnalysisResult(analysis, true, false, null, TimeSpan.FromSeconds(3));

            RMC_BestFit.BatchRunCoordinator.ApplyResult(map, result);

            Assert.AreEqual(RMC_BestFit.BatchRunStatus.Succeeded, viewModel.Status);
            Assert.AreEqual(100, viewModel.Progress);
            Assert.AreEqual("Complete", viewModel.StatusMessage);
            Assert.AreEqual(TimeSpan.FromSeconds(3), viewModel.Duration);
        }

        /// <summary>
        /// Verifies failed completion applies the exception message.
        /// </summary>
        [TestMethod]
        public void ApplyResult_Failed_UsesErrorMessage()
        {
            var analysis = new FakeAnalysis();
            var viewModel = CreateSingleViewModel(analysis);
            var map = RMC_BestFit.BatchRunCoordinator.BuildAnalysisMap(new[] { viewModel });
            var result = new BatchAnalysisResult(analysis, false, false, new InvalidOperationException("boom"), TimeSpan.Zero);

            RMC_BestFit.BatchRunCoordinator.ApplyResult(map, result);

            Assert.AreEqual(RMC_BestFit.BatchRunStatus.Failed, viewModel.Status);
            Assert.AreEqual("boom", viewModel.StatusMessage);
        }

        /// <summary>
        /// Verifies canceled completion applies the canceled state.
        /// </summary>
        [TestMethod]
        public void ApplyResult_Canceled_SetsCanceledState()
        {
            var analysis = new FakeAnalysis();
            var viewModel = CreateSingleViewModel(analysis);
            var map = RMC_BestFit.BatchRunCoordinator.BuildAnalysisMap(new[] { viewModel });
            var result = new BatchAnalysisResult(analysis, false, true, null, TimeSpan.Zero);

            RMC_BestFit.BatchRunCoordinator.ApplyResult(map, result);

            Assert.AreEqual(RMC_BestFit.BatchRunStatus.Canceled, viewModel.Status);
            Assert.AreEqual("Canceled", viewModel.StatusMessage);
        }

        /// <summary>
        /// Verifies batch options use one worker for serial mode and a conservative slot cap
        /// for parallel mode.
        /// </summary>
        [TestMethod]
        public void CreateOptions_MapsParallelSetting()
        {
            BatchAnalysisOptions serial = RMC_BestFit.BatchRunCoordinator.CreateOptions(false, 8);
            BatchAnalysisOptions parallel = RMC_BestFit.BatchRunCoordinator.CreateOptions(true, 8);

            Assert.AreEqual(1, serial.MaxDegreeOfParallelism);
            Assert.AreEqual(4, parallel.MaxDegreeOfParallelism);
            Assert.IsTrue(serial.ContinueOnError);
            Assert.IsTrue(serial.OrderByDependency);
        }

        /// <summary>
        /// Verifies low processor counts still produce at least one parallel analysis slot.
        /// </summary>
        [TestMethod]
        public void CalculateParallelAnalysisSlots_LowProcessorCount_ReturnsOne()
        {
            Assert.AreEqual(1, RMC_BestFit.BatchRunCoordinator.CalculateParallelAnalysisSlots(1));
            Assert.AreEqual(1, RMC_BestFit.BatchRunCoordinator.CalculateParallelAnalysisSlots(2));
        }

        /// <summary>
        /// Verifies larger machines are capped to leave CPU headroom for WPF rendering.
        /// </summary>
        [TestMethod]
        public void CalculateParallelAnalysisSlots_HighProcessorCount_CapsAtFour()
        {
            Assert.AreEqual(3, RMC_BestFit.BatchRunCoordinator.CalculateParallelAnalysisSlots(6));
            Assert.AreEqual(4, RMC_BestFit.BatchRunCoordinator.CalculateParallelAnalysisSlots(32));
        }

        /// <summary>
        /// Verifies completion summary formatting includes failed and canceled counts only
        /// when they are present.
        /// </summary>
        [TestMethod]
        public void FormatSummary_MixedResults_IncludesOutcomeCounts()
        {
            var first = new FakeAnalysis();
            var second = new FakeAnalysis();
            var third = new FakeAnalysis();
            var results = new[]
            {
                new BatchAnalysisResult(first, true, false, null, TimeSpan.Zero),
                new BatchAnalysisResult(second, false, false, new Exception("failed"), TimeSpan.Zero),
                new BatchAnalysisResult(third, false, true, null, TimeSpan.Zero)
            };

            string summary = RMC_BestFit.BatchRunCoordinator.FormatSummary(results);

            Assert.AreEqual("Batch run finished: 3 Analyses (1 Succeeded, 1 Failed, 1 Canceled)", summary);
        }

        /// <summary>
        /// Creates a single coordinator row for the supplied analysis.
        /// </summary>
        /// <param name="analysis">The fake analysis.</param>
        /// <returns>The constructed view model.</returns>
        private static RMC_BestFit.BatchRunItemViewModel CreateSingleViewModel(FakeAnalysis analysis)
        {
            return RMC_BestFit.BatchRunCoordinator.BuildViewModels(new IElement[]
            {
                new FakeAnalysisElement("analysis", analysis, false)
            })[0];
        }

        /// <summary>
        /// Minimal non-analysis element used to verify batch filtering.
        /// </summary>
        private class FakeElement : IElement
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="FakeElement"/> class.
            /// </summary>
            /// <param name="name">The element name.</param>
            internal FakeElement(string name)
            {
                Name = name;
            }

            /// <inheritdoc/>
            public string DisplayName => Name;

            /// <inheritdoc/>
            public ImageSource ElementImage => null;

            /// <inheritdoc/>
            public string Name { get; set; }

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
            public string NameOnDisk => Name;

            /// <inheritdoc/>
            public bool CanCopyFromExternal => false;

            /// <inheritdoc/>
            public bool IsValid => true;

            /// <inheritdoc/>
            public IElementCollection ParentCollection => null;

            /// <inheritdoc/>
            public event PreviewDeletedEventHandler PreviewDeleted { add { } remove { } }

            /// <inheritdoc/>
            public event DeletedEventHandler Deleted { add { } remove { } }

            /// <inheritdoc/>
            public event PropertyChangedEventHandler PropertyChanged { add { } remove { } }

            /// <inheritdoc/>
            public IElement Copy(string newName = "") => throw new NotImplementedException();

            /// <inheritdoc/>
            public IElement CopyFromExternal(string itemName, string fullFileName) => throw new NotImplementedException();

            /// <inheritdoc/>
            public void Open()
            {
            }

            /// <inheritdoc/>
            public void Save()
            {
            }

            /// <inheritdoc/>
            public void Delete()
            {
            }

            /// <inheritdoc/>
            public void SetIsValid(bool isValid, string message)
            {
            }

            /// <inheritdoc/>
            public DataTable CreateTable() => new DataTable();

            /// <inheritdoc/>
            public List<string> RequiredColumns() => new List<string>();
        }

        /// <summary>
        /// Minimal analysis element used to verify coordinator behavior.
        /// </summary>
        private sealed class FakeAnalysisElement : FakeElement, IAnalysisElement
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="FakeAnalysisElement"/> class.
            /// </summary>
            /// <param name="name">The element name.</param>
            /// <param name="innerAnalysis">The model-layer analysis exposed by the element.</param>
            /// <param name="isEstimated">Whether the element starts as estimated.</param>
            internal FakeAnalysisElement(string name, IAnalysis innerAnalysis, bool isEstimated)
                : base(name)
            {
                InnerAnalysis = innerAnalysis;
                IsEstimated = isEstimated;
            }

            /// <inheritdoc/>
            public IAnalysis InnerAnalysis { get; }

            /// <inheritdoc/>
            public BayesianAnalysis BayesianAnalysis => null;

            /// <inheritdoc/>
            public UncertaintyAnalysisResults AnalysisResults => null;

            /// <inheritdoc/>
            public Task RunAsync(SafeProgressReporter progressReporter) => Task.CompletedTask;

            /// <inheritdoc/>
            public void CancelAnalysis()
            {
            }

            /// <inheritdoc/>
            public bool IsEstimated { get; }
        }

        /// <summary>
        /// Minimal model-layer analysis used as a dictionary key.
        /// </summary>
        private sealed class FakeAnalysis : IAnalysis
        {
            /// <inheritdoc/>
            public event PropertyChangedEventHandler PropertyChanged { add { } remove { } }

            /// <inheritdoc/>
            public event EventHandler<CancelEventArgs> AnalysisStarting { add { } remove { } }

            /// <inheritdoc/>
            public event EventHandler<AnalysisRunCompletedEventArgs> AnalysisCompleted { add { } remove { } }

            /// <inheritdoc/>
            public Task RunAsync(SafeProgressReporter progressReporter = null) => Task.CompletedTask;

            /// <inheritdoc/>
            public void CancelAnalysis()
            {
            }

            /// <inheritdoc/>
            public bool IsEstimated => false;

            /// <inheritdoc/>
            public (bool IsValid, List<string> ValidationMessages) Validate()
            {
                return (true, new List<string>());
            }
        }
    }
}
