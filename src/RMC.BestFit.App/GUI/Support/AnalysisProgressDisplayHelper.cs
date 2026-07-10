using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace RMC_BestFit
{
    /// <summary>
    /// Applies shared progress display behavior to standalone analysis property controls.
    /// </summary>
    internal static class AnalysisProgressDisplayHelper
    {
        /// <summary>
        /// Progress value at which the UI switches to the processing-results state.
        /// </summary>
        internal const double ProcessingThreshold = 99.0;

        /// <summary>
        /// Shows an initialized progress bar and text block.
        /// </summary>
        /// <param name="progressBar">The progress bar to initialize.</param>
        /// <param name="progressTextBlock">The progress text block to initialize.</param>
        /// <param name="startingText">The text to show before measurable progress is available.</param>
        internal static void ShowInitial(ProgressBar progressBar, TextBlock progressTextBlock, string startingText = "Starting...")
        {
            progressBar.IsIndeterminate = true;
            progressBar.Value = 0;
            progressTextBlock.Text = startingText;
            progressBar.Visibility = Visibility.Visible;
            progressTextBlock.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Applies progress to the controls on the owning dispatcher.
        /// </summary>
        /// <param name="dispatcher">The dispatcher that owns the controls.</param>
        /// <param name="progressBar">The progress bar to update.</param>
        /// <param name="progressTextBlock">The progress text block to update.</param>
        /// <param name="progress">The progress percentage.</param>
        /// <param name="startingText">The text to show before measurable progress is available.</param>
        internal static void PostProgress(
            Dispatcher dispatcher,
            ProgressBar progressBar,
            TextBlock progressTextBlock,
            double progress,
            string startingText = "Starting...")
        {
            if (dispatcher.CheckAccess())
            {
                ApplyProgress(progressBar, progressTextBlock, progress, startingText);
                return;
            }

            _ = dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                ApplyProgress(progressBar, progressTextBlock, progress, startingText);
            }));
        }

        /// <summary>
        /// Applies progress to the controls.
        /// </summary>
        /// <param name="progressBar">The progress bar to update.</param>
        /// <param name="progressTextBlock">The progress text block to update.</param>
        /// <param name="progress">The progress percentage.</param>
        /// <param name="startingText">The text to show before measurable progress is available.</param>
        internal static void ApplyProgress(
            ProgressBar progressBar,
            TextBlock progressTextBlock,
            double progress,
            string startingText = "Starting...")
        {
            progressBar.Value = Math.Max(0.0, Math.Min(100.0, progress));

            if (progress <= 0.01)
            {
                progressTextBlock.Text = startingText;
                progressBar.IsIndeterminate = true;
            }
            else if (progress >= ProcessingThreshold)
            {
                progressTextBlock.Text = "Processing Results...";
                progressBar.IsIndeterminate = true;
            }
            else
            {
                progressTextBlock.Text = progress.ToString("N0") + "% Complete";
                progressBar.IsIndeterminate = false;
            }
        }

        /// <summary>
        /// Hides progress controls after the awaited analysis task has completed.
        /// </summary>
        /// <param name="progressBar">The progress bar to hide.</param>
        /// <param name="progressTextBlock">The progress text block to hide.</param>
        /// <param name="completeText">The completion text to set before hiding.</param>
        internal static void HideCompleted(ProgressBar progressBar, TextBlock progressTextBlock, string completeText = "Analysis Complete")
        {
            progressBar.Value = 100;
            progressBar.IsIndeterminate = false;
            progressTextBlock.Text = completeText;
            progressBar.Visibility = Visibility.Hidden;
            progressTextBlock.Visibility = Visibility.Hidden;
        }
    }
}
