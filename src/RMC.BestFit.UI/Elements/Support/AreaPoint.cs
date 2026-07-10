using OxyPlot;

namespace RMC.BestFit.UI
{
    /// <summary>
    /// Represents a rectangular area defined by two points in a 2D coordinate system.
    /// Used as the ItemsSource element type for OxyPlot <see cref="OxyPlot.Wpf.AreaSeries"/>
    /// with DataFieldX="X1", DataFieldX2="X2", DataFieldY="Y1", DataFieldY2="Y2".
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    /// This class is used to define areas on plots, typically for shading confidence intervals
    /// or other regions of interest between two data points on the seasonality plot.
    /// </para>
    /// </remarks>
    public class AreaPoint
    {
        /// <summary>
        /// Gets or sets the X-coordinate of the first point.
        /// </summary>
        public double X1 { get; set; }

        /// <summary>
        /// Gets or sets the X-coordinate of the second point.
        /// </summary>
        public double X2 { get; set; }

        /// <summary>
        /// Gets or sets the Y-coordinate of the first point.
        /// </summary>
        public double Y1 { get; set; }

        /// <summary>
        /// Gets or sets the Y-coordinate of the second point.
        /// </summary>
        public double Y2 { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="AreaPoint"/> class from two OxyPlot data points.
        /// </summary>
        /// <param name="p1">The first data point defining one corner of the area.</param>
        /// <param name="p2">The second data point defining the opposite corner of the area.</param>
        public AreaPoint(DataPoint p1, DataPoint p2)
        {
            X1 = p1.X;
            X2 = p2.X;
            Y1 = p1.Y;
            Y2 = p2.Y;
        }
    }
}
