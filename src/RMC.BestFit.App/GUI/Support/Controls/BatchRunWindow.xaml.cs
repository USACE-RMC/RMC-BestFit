using FrameworkInterfaces;
using RMC.BestFit.Models;
using RMC.BestFit.Analyses;
using RMC.BestFit.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using GenericControls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace RMC_BestFit
{
    /// <summary>
    /// Window for executing multiple analyses in batch mode with per-item status tracking.
    /// Provides functionality to select, queue, and run multiple analyses using
    /// <see cref="BatchAnalysisRunner"/> with progress tracking, status icons,
    /// duration display, and cancellation support.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    /// The DataGrid displays each analysis with columns for:
    /// element icon, name, per-row progress bar, duration (mm:ss), and a status icon
    /// (pending clock, spinning arrows for running, green check for success, red X for failure).
    /// </para>
    /// <para>
    /// The batch execution is delegated to <see cref="BatchAnalysisRunner"/> from the
    /// model library (<c>RMC.BestFit.dll</c>), which handles ordering, error handling,
    /// and cancellation. The window subscribes to the runner's events to update the
    /// view models on the UI thread via <see cref="System.Windows.Threading.Dispatcher"/>.
    /// </para>
    /// </remarks>
    public partial class BatchRunWindow : MetroWindow
    {
        /// <summary>
        /// The batch analysis runner used to execute analyses.
        /// </summary>
        private BatchAnalysisRunner _runner;

        /// <summary>
        /// Cancellation token source for the current batch run.
        /// </summary>
        private CancellationTokenSource _cancellationTokenSource;

        /// <summary>
        /// The view models wrapping each analysis for DataGrid display.
        /// </summary>
        private ObservableCollection<BatchRunItemViewModel> _viewModels;

        /// <summary>
        /// Lookup from the model-layer <see cref="RMC.BestFit.Analyses.IAnalysis"/> to its
        /// corresponding view model, used for updating status when runner events fire.
        /// </summary>
        private Dictionary<RMC.BestFit.Analyses.IAnalysis, BatchRunItemViewModel> _analysisToViewModel;

        /// <summary>
        /// Initializes a new instance of the <see cref="BatchRunWindow"/> class.
        /// Sets up command bindings for window close operations.
        /// </summary>
        public BatchRunWindow()
        {
            InitializeComponent();
            CommandBindings.Add(new CommandBinding(SystemCommands.CloseWindowCommand, OnCloseWindow));
        }

        /// <summary>
        /// Identifies the <see cref="Analyses"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty AnalysesProperty = DependencyProperty.Register(
            nameof(Analyses), typeof(ObservableCollection<IElement>), typeof(BatchRunWindow),
            new PropertyMetadata(null, ElementCallback));

        /// <summary>
        /// Gets or sets the collection of analysis elements available for batch processing.
        /// </summary>
        public ObservableCollection<IElement> Analyses
        {
            get { return (ObservableCollection<IElement>)GetValue(AnalysesProperty); }
            set { SetValue(AnalysesProperty, value); }
        }

        /// <summary>
        /// Callback method invoked when the <see cref="Analyses"/> property changes.
        /// Builds the view model collection and sets it as the DataGrid source.
        /// </summary>
        /// <param name="d">The dependency object whose property changed.</param>
        /// <param name="e">Event arguments containing the old and new property values.</param>
        private static void ElementCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not BatchRunWindow window) return;
            if (e.NewValue is not ObservableCollection<IElement> newCollection) return;

            window.Title = newCollection.Count > 0
                ? "Batch Run " + newCollection[0].ParentCollection.Name
                : "Batch Run";

            window._viewModels = BatchRunCoordinator.BuildViewModels(newCollection);
            window._analysisToViewModel = BatchRunCoordinator.BuildAnalysisMap(window._viewModels);

            window.MyDataGrid.ItemsSource = window._viewModels;
        }

        /// <summary>
        /// Handles the close window command.
        /// Prevents window closure if a simulation is currently in progress.
        /// </summary>
        /// <param name="target">The target of the command.</param>
        /// <param name="e">Executed routed event arguments.</param>
        private new void OnCloseWindow(object target, ExecutedRoutedEventArgs e)
        {
            if (FrameworkUI.ShellPublicVariables.SimulationInProgress == false)
                SystemCommands.CloseWindow(this);
        }

        /// <summary>
        /// Handles the SelectionChanged event for the data grid.
        /// Enables or disables buttons based on whether items are selected.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Selection changed event arguments.</param>
        private void MyDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MyDataGrid.SelectedItems.Count <= 0)
            {
                DeselectButton.IsEnabled = false;
                SimulateButton.IsEnabled = false;
            }
            else if (MyDataGrid.SelectedItems.Count >= 1)
            {
                DeselectButton.IsEnabled = true;
                SimulateButton.IsEnabled = true;
            }
        }

        /// <summary>
        /// Handles the Click event for the simulate button.
        /// Initiates the batch run process for all selected analyses using
        /// <see cref="BatchAnalysisRunner"/>.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Routed event arguments.</param>
        private async void SimulateButton_Click(object sender, RoutedEventArgs e)
        {
            // Collect selected analyses from view models
            var selectedViewModels = MyDataGrid.SelectedItems
                .OfType<BatchRunItemViewModel>()
                .ToList();

            if (selectedViewModels.Count == 0) return;

            BatchRunCoordinator.ResetForRun(selectedViewModels);

            // Set up UI state
            _cancellationTokenSource = new CancellationTokenSource();

            SummaryTextBlock.Text = "Running batch...";

            SimulateButton.Visibility = Visibility.Hidden;
            CancelButton.Visibility = Visibility.Visible;

            FrameworkUI.ShellPublicVariables.SimulationInProgress = true;
            MyDataGrid.IsEnabled = false;
            SelectButton.IsEnabled = false;
            DeselectButton.IsEnabled = false;
            ParallelCheckBox.IsEnabled = false;

            // Create runner and wire events
            _runner = new BatchAnalysisRunner();
            using var progressUpdates = new BatchProgressUpdateDispatcher(Dispatcher, _analysisToViewModel);

            _runner.AnalysisStarting += (s, analysis) =>
            {
                progressUpdates.MarkStarting(analysis);
            };

            _runner.AnalysisCompleted += (s, result) =>
            {
                progressUpdates.ApplyResult(result);
            };

            // Wire per-analysis progress (each analysis gets its own reporter in the runner)
            _runner.AnalysisProgressChanged += (s, e) =>
            {
                progressUpdates.PostProgress(e.Analysis, e.Progress);
            };

            // Extract model-layer IAnalysis list in selection order via InnerAnalysis bridge
            var analyses = BatchRunCoordinator.SelectAnalyses(selectedViewModels);

            bool runParallel = ParallelCheckBox.IsChecked == true;
            var options = BatchRunCoordinator.CreateOptions(runParallel, Environment.ProcessorCount);

            // Run the batch
            try
            {
                List<BatchAnalysisResult> results = null;
                await WaitCursorHelper.RunWithVisibleWaitCursorAsync(Dispatcher, async () =>
                {
                    results = await _runner.RunAsync(analyses, options, null,
                        _cancellationTokenSource.Token);
                    await progressUpdates.FlushAsync();
                    await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
                });

                SummaryTextBlock.Text = BatchRunCoordinator.FormatSummary(results);
            }
            catch (OperationCanceledException)
            {
                SummaryTextBlock.Text = "Batch run was canceled.";
            }
            finally
            {
                // Re-enable UI
                FrameworkUI.ShellPublicVariables.SimulationInProgress = false;
                MyDataGrid.IsEnabled = true;
                SelectButton.IsEnabled = true;
                DeselectButton.IsEnabled = true;
                ParallelCheckBox.IsEnabled = true;

                SimulateButton.Visibility = Visibility.Visible;
                CancelButton.Visibility = Visibility.Hidden;
                Mouse.OverrideCursor = null;

                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        /// <summary>
        /// Handles the Click event for the cancel button.
        /// Cancels the batch run and all remaining analyses.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Routed event arguments.</param>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _cancellationTokenSource?.Cancel();
            _runner?.Cancel();
        }

        /// <summary>
        /// Handles the KeyDown event for the batch run window.
        /// Cancels the batch run when the Escape key is pressed.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Key event arguments.</param>
        private void BatchRun_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                _cancellationTokenSource?.Cancel();
                _runner?.Cancel();
            }
        }

        /// <summary>
        /// Handles the Click event for the select button.
        /// Selects all analyses in the data grid and sets focus to the grid.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Routed event arguments.</param>
        private void SelectButton_Click(object sender, RoutedEventArgs e)
        {
            MyDataGrid.SelectAll();
            MyDataGrid.Focus();
        }

        /// <summary>
        /// Handles the Click event for the deselect button.
        /// Clears all selections in the data grid.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Routed event arguments.</param>
        private void DeselectButton_Click(object sender, RoutedEventArgs e)
        {
            MyDataGrid.UnselectAll();
        }
    }
}
