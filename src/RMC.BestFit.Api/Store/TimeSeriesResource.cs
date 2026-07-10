using Numerics.Data;

namespace RMC.BestFit.Api.Store
{
    /// <summary>
    /// A server-side time-series resource wrapping a <see cref="Numerics.Data.TimeSeries"/>,
    /// created either from a USGS download or from client-supplied points.
    /// </summary>
    /// <remarks>
    /// Summary statistics (point count, missing count, date range) are computed once at creation
    /// because the underlying series is treated as immutable for the lifetime of the resource.
    /// </remarks>
    public class TimeSeriesResource : ResourceBase
    {
        /// <summary>
        /// Constructs the resource and computes the point-count / missing-count / date-range summary
        /// from the supplied series.
        /// </summary>
        /// <param name="timeSeries">The underlying time series. Treated as immutable after construction.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="timeSeries"/> is null.</exception>
        public TimeSeriesResource(TimeSeries timeSeries)
        {
            TimeSeries = timeSeries ?? throw new ArgumentNullException(nameof(timeSeries));
            PointCount = timeSeries.Count;
            MissingCount = timeSeries.Count(o => double.IsNaN(o.Value));
            if (timeSeries.Count > 0)
            {
                StartDate = timeSeries[0].Index;
                EndDate = timeSeries[timeSeries.Count - 1].Index;
            }
        }

        /// <summary>
        /// The underlying time series. Treated as immutable; downstream consumers clone it before mutating.
        /// </summary>
        public TimeSeries TimeSeries { get; }

        /// <summary>
        /// How the resource was created (USGS download or manual entry).
        /// </summary>
        public required TimeSeriesSource Source { get; init; }

        /// <summary>
        /// The USGS site number the series was downloaded from, when <see cref="Source"/> is USGS.
        /// </summary>
        public string? UsgsSiteNumber { get; init; }

        /// <summary>
        /// The USGS series type that was downloaded (daily/peak/measured discharge or stage),
        /// when <see cref="Source"/> is USGS.
        /// </summary>
        public TimeSeriesDownload.TimeSeriesType? UsgsSeriesType { get; init; }

        /// <summary>
        /// The number of ordinates in the series, computed at creation.
        /// </summary>
        public int PointCount { get; }

        /// <summary>
        /// The number of ordinates whose value is NaN (missing observations), computed at creation.
        /// </summary>
        public int MissingCount { get; }

        /// <summary>
        /// The date of the first ordinate, or null when the series is empty.
        /// </summary>
        public DateTime? StartDate { get; }

        /// <summary>
        /// The date of the last ordinate, or null when the series is empty.
        /// </summary>
        public DateTime? EndDate { get; }
    }
}
