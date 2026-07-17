using FrameworkInterfaces;
using RMC.BestFit.Analyses;
using RMC.BestFit.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace RMC_BestFit
{
    /// <summary>
    /// Provides testable batch-run view-model state transitions used by <see cref="BatchRunWindow"/>.
    /// </summary>
    /// <remarks>
    /// This class keeps orchestration logic that does not require WPF controls out of the
    /// window code-behind so status mapping, selection, and summary behavior can be covered
    /// by fast unit tests.
    /// </remarks>
    internal static class BatchRunCoordinator
    {
        /// <summary>
        /// Builds batch-run row view models for elements that expose a model-layer analysis.
        /// </summary>
        /// <param name="elements">The candidate project elements.</param>
        /// <returns>A collection of view models for runnable analysis elements.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="elements"/> is <c>null</c>.</exception>
        internal static ObservableCollection<BatchRunItemViewModel> BuildViewModels(IEnumerable<IElement> elements)
        {
            if (elements == null) throw new ArgumentNullException(nameof(elements));

            var viewModels = new ObservableCollection<BatchRunItemViewModel>();
            foreach (IElement element in elements)
            {
                if (element is IAnalysisElement analysisElement && analysisElement.InnerAnalysis != null)
                {
                    var viewModel = new BatchRunItemViewModel(element, analysisElement);
                    if (analysisElement.IsEstimated)
                    {
                        viewModel.Status = BatchRunStatus.Succeeded;
                        viewModel.Progress = 100;
                        viewModel.StatusMessage = "Already estimated";
                    }

                    viewModels.Add(viewModel);
                }
            }

            return viewModels;
        }

        /// <summary>
        /// Builds a lookup from model-layer analysis instances to their row view models.
        /// </summary>
        /// <param name="viewModels">The view models to index.</param>
        /// <returns>A dictionary keyed by model-layer analysis.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="viewModels"/> is <c>null</c>.</exception>
        internal static Dictionary<IAnalysis, BatchRunItemViewModel> BuildAnalysisMap(IEnumerable<BatchRunItemViewModel> viewModels)
        {
            if (viewModels == null) throw new ArgumentNullException(nameof(viewModels));

            var map = new Dictionary<IAnalysis, BatchRunItemViewModel>();
            foreach (BatchRunItemViewModel viewModel in viewModels)
            {
                if (viewModel.Analysis.InnerAnalysis != null)
                {
                    map[viewModel.Analysis.InnerAnalysis] = viewModel;
                }
            }

            return map;
        }

        /// <summary>
        /// Resets selected rows before a new batch execution begins.
        /// </summary>
        /// <param name="viewModels">The selected view models.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="viewModels"/> is <c>null</c>.</exception>
        internal static void ResetForRun(IEnumerable<BatchRunItemViewModel> viewModels)
        {
            if (viewModels == null) throw new ArgumentNullException(nameof(viewModels));

            foreach (BatchRunItemViewModel viewModel in viewModels)
            {
                viewModel.Status = BatchRunStatus.Pending;
                viewModel.Progress = 0;
                viewModel.Duration = null;
                viewModel.StatusMessage = "Waiting...";
            }
        }

        /// <summary>
        /// Marks the supplied analysis as running when its start event is received.
        /// </summary>
        /// <param name="map">The analysis-to-view-model map.</param>
        /// <param name="analysis">The analysis that started.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="map"/> is <c>null</c>.</exception>
        internal static void MarkStarting(Dictionary<IAnalysis, BatchRunItemViewModel> map, IAnalysis analysis)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));

            if (analysis != null && map.TryGetValue(analysis, out BatchRunItemViewModel viewModel))
            {
                viewModel.Status = BatchRunStatus.Running;
                viewModel.StatusMessage = "Running...";
                viewModel.Progress = 0;
            }
        }

        /// <summary>
        /// Applies an analysis completion result to the matching row view model.
        /// </summary>
        /// <param name="map">The analysis-to-view-model map.</param>
        /// <param name="result">The batch result to apply.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="map"/> or <paramref name="result"/> is <c>null</c>.
        /// </exception>
        internal static void ApplyResult(Dictionary<IAnalysis, BatchRunItemViewModel> map, BatchAnalysisResult result)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (result == null) throw new ArgumentNullException(nameof(result));

            if (!map.TryGetValue(result.Analysis, out BatchRunItemViewModel viewModel)) return;

            viewModel.Duration = result.Duration;
            if (result.Succeeded)
            {
                viewModel.Status = BatchRunStatus.Succeeded;
                viewModel.Progress = 100;
                viewModel.StatusMessage = "Complete";
            }
            else if (result.WasCanceled)
            {
                viewModel.Status = BatchRunStatus.Canceled;
                viewModel.StatusMessage = "Canceled";
            }
            else
            {
                viewModel.Status = BatchRunStatus.Failed;
                viewModel.StatusMessage = result.Error?.Message ?? "Failed";
            }
        }

        /// <summary>
        /// Applies a progress update to the matching row view model.
        /// </summary>
        /// <param name="map">The analysis-to-view-model map.</param>
        /// <param name="analysis">The analysis whose progress changed.</param>
        /// <param name="progress">The progress percentage.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="map"/> is <c>null</c>.</exception>
        internal static void ApplyProgress(Dictionary<IAnalysis, BatchRunItemViewModel> map, IAnalysis analysis, double progress)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));

            if (analysis != null && map.TryGetValue(analysis, out BatchRunItemViewModel viewModel))
            {
                viewModel.Progress = progress;
            }
        }

        /// <summary>
        /// Selects the model-layer analyses represented by the selected view models.
        /// </summary>
        /// <param name="viewModels">The selected view models.</param>
        /// <returns>The runnable model-layer analyses, preserving view-model order.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="viewModels"/> is <c>null</c>.</exception>
        internal static List<IAnalysis> SelectAnalyses(IEnumerable<BatchRunItemViewModel> viewModels)
        {
            if (viewModels == null) throw new ArgumentNullException(nameof(viewModels));

            return viewModels
                .Where(viewModel => viewModel.Analysis.InnerAnalysis != null)
                .Select(viewModel => viewModel.Analysis.InnerAnalysis)
                .ToList();
        }

        /// <summary>
        /// Creates batch-analysis execution options from the UI parallel setting.
        /// </summary>
        /// <param name="runParallel">Whether parallel execution is requested.</param>
        /// <param name="processorCount">The processor count to use for parallel execution.</param>
        /// <returns>The configured batch-analysis options.</returns>
        internal static BatchAnalysisOptions CreateOptions(bool runParallel, int processorCount)
        {
            return new BatchAnalysisOptions
            {
                MaxDegreeOfParallelism = runParallel ? CalculateParallelAnalysisSlots(processorCount) : 1,
                ContinueOnError = true,
                OrderByDependency = true
            };
        }

        /// <summary>
        /// Calculates the number of analyses to run concurrently from the processor count.
        /// </summary>
        /// <param name="processorCount">The number of logical processors available.</param>
        /// <returns>A conservative parallel slot count that leaves headroom for WPF rendering.</returns>
        internal static int CalculateParallelAnalysisSlots(int processorCount)
        {
            int availableProcessors = Math.Max(1, processorCount);
            return Math.Max(1, Math.Min(4, availableProcessors / 2));
        }

        /// <summary>
        /// Formats the completion summary displayed after a batch run.
        /// </summary>
        /// <param name="results">The completed batch results.</param>
        /// <returns>A concise status summary.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="results"/> is <c>null</c>.</exception>
        internal static string FormatSummary(IReadOnlyCollection<BatchAnalysisResult> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));

            int total = results.Count;
            int succeeded = results.Count(result => result.Succeeded);
            int failed = results.Count(result => !result.Succeeded && !result.WasCanceled);
            int canceled = results.Count(result => result.WasCanceled);

            var summary = new System.Text.StringBuilder();
            summary.Append($"Batch run finished: {total} {(total == 1 ? "Analysis" : "Analyses")}");
            summary.Append($" ({succeeded} Succeeded");
            if (failed > 0)
                summary.Append($", {failed} Failed");
            if (canceled > 0)
                summary.Append($", {canceled} Canceled");
            summary.Append(')');

            return summary.ToString();
        }
    }
}
