using FrameworkInterfaces;
using RMC.BestFit.UI;
using System;
using System.ComponentModel;
using System.Windows.Media;

namespace RMC_BestFit
{
    /// <summary>
    /// Represents the execution status of a batch run item.
    /// </summary>
    public enum BatchRunStatus
    {
        /// <summary>
        /// The analysis is queued and waiting to run.
        /// </summary>
        Pending,

        /// <summary>
        /// The analysis is currently executing.
        /// </summary>
        Running,

        /// <summary>
        /// The analysis completed successfully.
        /// </summary>
        Succeeded,

        /// <summary>
        /// The analysis failed with an error.
        /// </summary>
        Failed,

        /// <summary>
        /// The analysis was canceled before completion.
        /// </summary>
        Canceled
    }

    /// <summary>
    /// View model wrapping an <see cref="IElement"/> and <see cref="IAnalysisElement"/> for display
    /// in the <see cref="BatchRunWindow"/> DataGrid, providing status tracking, progress,
    /// and duration properties with change notification.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    /// Each row in the batch run DataGrid is backed by an instance of this class.
    /// The <see cref="Status"/> property drives the status icon column via
    /// <see cref="BatchRunStatusToIconConverter"/>, and the <see cref="Progress"/>
    /// property drives the per-row progress bar.
    /// </para>
    /// </remarks>
    public class BatchRunItemViewModel : INotifyPropertyChanged
    {
        /// <summary>
        /// Backing field for <see cref="Status"/>.
        /// </summary>
        private BatchRunStatus _status = BatchRunStatus.Pending;

        /// <summary>
        /// Backing field for <see cref="Progress"/>.
        /// </summary>
        private double _progress;

        /// <summary>
        /// Backing field for <see cref="StatusMessage"/>.
        /// </summary>
        private string _statusMessage = "Waiting...";

        /// <summary>
        /// Backing field for <see cref="Duration"/>.
        /// </summary>
        private TimeSpan? _duration;

        /// <summary>
        /// Initializes a new instance of the <see cref="BatchRunItemViewModel"/> class.
        /// </summary>
        /// <param name="element">The element displayed in the DataGrid row.</param>
        /// <param name="analysis">The analysis to execute, cast from the element.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="element"/> or <paramref name="analysis"/> is <c>null</c>.
        /// </exception>
        public BatchRunItemViewModel(IElement element, IAnalysisElement analysis)
        {
            Element = element ?? throw new ArgumentNullException(nameof(element));
            Analysis = analysis ?? throw new ArgumentNullException(nameof(analysis));
        }

        /// <inheritdoc/>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Gets the underlying <see cref="IElement"/> for this row.
        /// </summary>
        public IElement Element { get; }

        /// <summary>
        /// Gets the <see cref="IAnalysisElement"/> instance for this row.
        /// </summary>
        public IAnalysisElement Analysis { get; }

        /// <summary>
        /// Gets the display name of the element, used for the Name column.
        /// </summary>
        public string DisplayName => Element.DisplayName;

        /// <summary>
        /// Gets the element image, used for the icon column.
        /// </summary>
        public ImageSource ElementImage => Element.ElementImage;

        /// <summary>
        /// Gets or sets the current execution status of this analysis.
        /// </summary>
        /// <value>
        /// One of the <see cref="BatchRunStatus"/> values. The default is
        /// <see cref="BatchRunStatus.Pending"/>.
        /// </value>
        /// <remarks>
        /// Changing this property updates the status icon in the DataGrid via
        /// the <see cref="BatchRunStatusToIconConverter"/>.
        /// </remarks>
        public BatchRunStatus Status
        {
            get => _status;
            set
            {
                if (_status != value)
                {
                    _status = value;
                    RaisePropertyChanged(nameof(Status));
                    RaisePropertyChanged(nameof(ProgressText));
                    RaisePropertyChanged(nameof(IsProgressIndeterminate));
                }
            }
        }

        /// <summary>
        /// Gets or sets the current progress of this analysis as a percentage (0-100).
        /// </summary>
        /// <value>
        /// A value between 0 and 100 inclusive. The default is 0.
        /// </value>
        /// <remarks>
        /// This value is bound to the per-row progress bar, which is only visible
        /// when <see cref="Status"/> is <see cref="BatchRunStatus.Running"/>.
        /// </remarks>
        public double Progress
        {
            get => _progress;
            set
            {
                if (Math.Abs(_progress - value) > 0.01)
                {
                    _progress = value;
                    RaisePropertyChanged(nameof(Progress));
                    RaisePropertyChanged(nameof(ProgressText));
                    RaisePropertyChanged(nameof(IsProgressIndeterminate));
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether the row progress bar should show indeterminate motion.
        /// </summary>
        /// <value>
        /// <c>true</c> while the analysis is running before measurable progress begins or
        /// after the analysis reports the processing-results phase.
        /// </value>
        public bool IsProgressIndeterminate
        {
            get
            {
                if (_status != BatchRunStatus.Running) return false;
                return _progress <= 0.01 || _progress >= 99;
            }
        }

        /// <summary>
        /// Gets a formatted text representation of the current progress,
        /// suitable for display as an overlay on the per-row progress bar.
        /// </summary>
        /// <value>
        /// An empty string when the status is not <see cref="BatchRunStatus.Running"/>;
        /// a percentage string like "45% Complete" when running and progress is under 100;
        /// "Processing Results..." when progress reaches the processing-results phase.
        /// </value>
        public string ProgressText
        {
            get
            {
                if (_status != BatchRunStatus.Running)
                    return "";
                if (_progress <= 0.01)
                    return "Starting...";
                if (_progress >= 99)
                    return "Processing Results...";
                return _progress.ToString("N0") + "% Complete";
            }
        }

        /// <summary>
        /// Gets or sets a human-readable status message for this analysis.
        /// </summary>
        /// <value>
        /// A descriptive string such as "Waiting...", "Running...", "Complete",
        /// or an error message. The default is "Waiting...".
        /// </value>
        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                if (_statusMessage != value)
                {
                    _statusMessage = value;
                    RaisePropertyChanged(nameof(StatusMessage));
                }
            }
        }

        /// <summary>
        /// Gets or sets the wall-clock duration of the analysis execution.
        /// </summary>
        /// <value>
        /// <c>null</c> before the analysis starts; the elapsed time after completion.
        /// </value>
        public TimeSpan? Duration
        {
            get => _duration;
            set
            {
                if (_duration != value)
                {
                    _duration = value;
                    RaisePropertyChanged(nameof(Duration));
                    RaisePropertyChanged(nameof(DurationText));
                }
            }
        }

        /// <summary>
        /// Gets a formatted string representation of <see cref="Duration"/> in mm:ss format.
        /// </summary>
        /// <value>
        /// An empty string if the duration is <c>null</c>; otherwise the duration formatted
        /// as "mm:ss" (e.g., "02:15" for 2 minutes and 15 seconds).
        /// </value>
        public string DurationText
        {
            get
            {
                if (_duration == null) return "";
                var d = _duration.Value;
                if (d.TotalHours >= 1)
                    return d.ToString(@"h\:mm\:ss");
                return d.ToString(@"mm\:ss");
            }
        }

        /// <summary>
        /// Raises the <see cref="PropertyChanged"/> event for the specified property.
        /// </summary>
        /// <param name="propertyName">The name of the property that changed.</param>
        private void RaisePropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
