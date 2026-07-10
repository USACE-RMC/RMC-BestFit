using OxyPlot;
using OxyPlot.Wpf;
using RMC.BestFit.UI;
using System.ComponentModel;

namespace RMC_BestFit
{
    /// <summary>
    /// Represents a selectable time series alternative item with an associated line series for overlaying on the Time Series plot.
    /// </summary>
    /// <remarks>
    /// This class wraps a <see cref="TimeSeriesElement"/> along with a pre-created <see cref="LineSeries"/> for display.
    /// Unlike <see cref="AnalysisAlternativeItem"/> which handles Bayesian analysis results with multiple series,
    /// this class only requires a single line series since time series data is raw observational data.
    /// The line color is assigned dynamically by the parent control when adding to the plot.
    /// </remarks>
    public class TimeSeriesAlternativeItem : INotifyPropertyChanged
    {
        /// <summary>
        /// Backing field for the <see cref="IsChecked"/> property.
        /// </summary>
        private bool _isChecked = false;

        /// <summary>
        /// Occurs when a property value changes.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Gets the time series element associated with this alternative item.
        /// </summary>
        public TimeSeriesElement Alternative { get; }

        /// <summary>
        /// Gets the line series used to display this alternative on the time series plot.
        /// </summary>
        /// <remarks>
        /// The series color is not set during construction; it is assigned dynamically by
        /// <see cref="TimeSeriesControl"/> when the alternative is added to the plot, using
        /// a unique color from <see cref="GenericControls.GeneralMethods.RandomColorsLongList"/>.
        /// </remarks>
        public LineSeries TimeSeriesLine { get; }

        /// <summary>
        /// Gets a value indicating whether this alternative has data available for plotting.
        /// </summary>
        /// <remarks>
        /// Returns <c>true</c> if the alternative's <see cref="TimeSeriesElement.TimeSeries"/> is not null
        /// and contains at least one ordinate; otherwise <c>false</c>.
        /// </remarks>
        public bool HasData
        {
            get { return Alternative.TimeSeries != null && Alternative.TimeSeries.Count > 0; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether this time series alternative item is checked in the user interface.
        /// </summary>
        /// <remarks>
        /// Setting this property raises the <see cref="PropertyChanged"/> event when the value changes.
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
        /// Initializes a new instance of the <see cref="TimeSeriesAlternativeItem"/> class.
        /// </summary>
        /// <param name="alternative">The time series element to wrap as an alternative.</param>
        /// <remarks>
        /// Creates a <see cref="LineSeries"/> with default styling (solid line, stroke thickness 1).
        /// Subscribes to the alternative's <see cref="INotifyPropertyChanged.PropertyChanged"/> event
        /// to propagate relevant changes (TimeSeries data and Name).
        /// </remarks>
        public TimeSeriesAlternativeItem(TimeSeriesElement alternative)
        {
            Alternative = alternative;
            Alternative.PropertyChanged += Alternative_PropertyChanged;

            TimeSeriesLine = new LineSeries()
            {
                Name = "TimeSeriesLine",
                Title = alternative.Name,
                StrokeThickness = 1,
                LineStyle = LineStyle.Solid,
                Decimator = OxyPlot.Decimator.Decimate,
                MinimumSegmentLength = 4.0
            };
        }

        /// <summary>
        /// Handles property change events from the associated time series element.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="PropertyChangedEventArgs"/> containing the event data.</param>
        /// <remarks>
        /// Propagates <see cref="PropertyChangedEventArgs"/> for <c>TimeSeries</c> and <c>Name</c>
        /// property changes to listeners, enabling the parent control to refresh the plot when
        /// the alternative's data or name changes.
        /// </remarks>
        private void Alternative_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Alternative.TimeSeries) || e.PropertyName == nameof(Alternative.Name))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(e.PropertyName));
            }
        }

        /// <summary>
        /// Detaches event subscriptions from the wrapped <see cref="Alternative"/> element.
        /// Call this before discarding the item to prevent memory leaks.
        /// </summary>
        public void Detach()
        {
            Alternative.PropertyChanged -= Alternative_PropertyChanged;
        }
    }
}
