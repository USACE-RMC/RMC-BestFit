using System;
using OxyPlot;
using OxyPlot.Wpf;
using FrameworkInterfaces;
using RMC.BestFit.Models;
using RMC.BestFit.UI;
using System.ComponentModel;
using System.Windows.Media;

namespace RMC_BestFit
{
    /// <summary>
    /// Represents a selectable analysis alternative item with associated visualization series for Bayesian analysis results.
    /// </summary>
    /// <remarks>
    /// This class encapsulates an analysis alternative along with its graphical representations including
    /// credible intervals, posterior predictive distribution, and posterior mode. It implements property
    /// change notification to enable binding in WPF applications and tracks the checked state for UI selection.
    /// Implements <see cref="IDisposable"/> to allow callers to unsubscribe from the wrapped
    /// <c>IAnalysisElement.PropertyChanged</c> event when the item is no longer needed.
    /// </remarks>
    public class AnalysisAlternativeItem : IDisposable
    {
        /// <summary>
        /// Backing field for the IsChecked property.
        /// </summary>
        private bool _isChecked = false;

        /// <summary>
        /// Default OxyPlot tracker format string for analysis alternative series.
        /// </summary>
        private const string DefaultTrackerFormatString = "{0}\n{1}: {2}\n{3}: {4}";

        /// <summary>
        /// Occurs when a property value changes.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Gets the analysis alternative associated with this item.
        /// </summary>
        public IAnalysisElement Alternative { get; }

        /// <summary>
        /// Gets the area series representing the credible intervals for the analysis.
        /// </summary>
        /// <remarks>
        /// The credible intervals are displayed as a shaded area with translucent blue fill.
        /// </remarks>
        public AreaSeries CredibleIntervals { get; }

        /// <summary>
        /// Gets the line series representing the posterior predictive distribution.
        /// </summary>
        /// <remarks>
        /// The posterior predictive is displayed as a dashed blue line.
        /// </remarks>
        public LineSeries PosteriorPredictive { get; }

        /// <summary>
        /// Gets the line series representing the posterior mode.
        /// </summary>
        /// <remarks>
        /// The posterior mode is displayed as a solid black line.
        /// </remarks>
        public LineSeries PosteriorMode { get; }

        /// <summary>
        /// Gets or sets a value indicating whether this analysis alternative item is checked in the user interface.
        /// </summary>
        /// <remarks>
        /// Setting this property raises the PropertyChanged event when the value changes.
        /// </remarks>
        public bool IsChecked
        {
            get { return _isChecked; }
            set
            {
                if (_isChecked != value)
                {
                    _isChecked = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AnalysisAlternativeItem"/> class.
        /// </summary>
        /// <param name="alternative">The analysis alternative to wrap.</param>
        /// <remarks>
        /// This constructor initializes the visualization series with predefined styling for
        /// credible intervals, posterior predictive, and posterior mode. It also subscribes
        /// to property change events from the alternative to propagate relevant changes.
        /// </remarks>
        public AnalysisAlternativeItem(IAnalysisElement alternative)
        {
            Alternative = alternative;
            Alternative.PropertyChanged += Alternative_PropertyChanged;

            CredibleIntervals = new AreaSeries()
            {
                Name = "CredibleIntervals",
                Fill = Color.FromArgb(75, 104, 140, 175),
                Color = Color.FromArgb(255, 53, 59, 122),
                LineStyle = LineStyle.Solid,
                BrokenLineThickness = 1,
                StrokeThickness = 1,
                Decimator = OxyPlot.Decimator.Decimate,
                TrackerFormatString = DefaultTrackerFormatString
            };

            PosteriorPredictive = new LineSeries()
            {
                Name = "PosteriorPredictive",
                Title = "Posterior Predictive",
                Color = Colors.Blue,
                StrokeThickness = 1,
                LineStyle = LineStyle.Dash,
                Decimator = OxyPlot.Decimator.Decimate,
                MinimumSegmentLength = 4.0,
                TrackerFormatString = DefaultTrackerFormatString
            };

            PosteriorMode = new LineSeries()
            {
                Name = "PosteriorMode",
                Title = "Posterior Mode",
                Color = Colors.Black,
                StrokeThickness = 1,
                LineStyle = LineStyle.Solid,
                Decimator = OxyPlot.Decimator.Decimate,
                MinimumSegmentLength = 4.0,
                TrackerFormatString = DefaultTrackerFormatString
            };

        }

        /// <summary>
        /// Handles property change events from the associated analysis alternative.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="PropertyChangedEventArgs"/> containing the event data.</param>
        /// <remarks>
        /// This method propagates property change notifications for AnalysisResults and IsEstimated
        /// properties to listeners of this item's PropertyChanged event.
        /// </remarks>
        private void Alternative_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Alternative.AnalysisResults) || e.PropertyName == nameof(Alternative.IsEstimated))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(e.PropertyName));
            }
        }

        /// <summary>
        /// Releases resources held by this instance. Unsubscribes from the wrapped
        /// <c>IAnalysisElement.PropertyChanged</c> event to prevent memory leaks
        /// when long-lived <see cref="Alternative"/> instances would otherwise keep this
        /// wrapper alive via the event subscription.
        /// </summary>
        public void Dispose()
        {
            Alternative.PropertyChanged -= Alternative_PropertyChanged;
        }
    }
}
