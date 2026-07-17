using Hec.Dss;
using System;
using System.Globalization;

namespace RMC_BestFit
{
    /// <summary>
    /// Represents the displayable data range resolved for a DSS pathname selector row.
    /// </summary>
    /// <remarks>
    /// The selector displays this range in the D-part column while preserving the dateless
    /// pathname used by the time-series import logic.
    /// </remarks>
    internal sealed class DssPathRangeResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DssPathRangeResult"/> class.
        /// </summary>
        /// <param name="hasDisplayDpart">Whether <paramref name="displayDpart"/> should replace the catalog D-part.</param>
        /// <param name="displayDpart">The D-part text to display when available.</param>
        /// <param name="message">The resolution message shown as detail text.</param>
        private DssPathRangeResult(bool hasDisplayDpart, string displayDpart, string message)
        {
            HasDisplayDpart = hasDisplayDpart;
            DisplayDpart = displayDpart;
            Message = message;
        }

        /// <summary>
        /// Gets whether <see cref="DisplayDpart"/> should replace the catalog D-part.
        /// </summary>
        internal bool HasDisplayDpart { get; }

        /// <summary>
        /// Gets the D-part text resolved from the actual DSS time-series data.
        /// </summary>
        internal string DisplayDpart { get; }

        /// <summary>
        /// Gets a detail message describing the resolved range or fallback reason.
        /// </summary>
        internal string Message { get; }

        /// <summary>
        /// Creates a range result from a retrieved DSS time series.
        /// </summary>
        /// <param name="timeSeries">The DSS time series returned by the reader.</param>
        /// <returns>The resolved display range result.</returns>
        internal static DssPathRangeResult FromTimeSeries(TimeSeries timeSeries)
        {
            if (timeSeries == null || timeSeries.Times == null || timeSeries.Times.Length == 0)
            {
                return NoData();
            }

            return FromEndpoints(timeSeries.Times[0], timeSeries.Times[timeSeries.Times.Length - 1]);
        }

        /// <summary>
        /// Creates a range result from timestamps.
        /// </summary>
        /// <param name="times">The timestamps to summarize.</param>
        /// <returns>The resolved display range result.</returns>
        internal static DssPathRangeResult FromTimes(DateTime[] times)
        {
            if (times == null || times.Length == 0)
            {
                return NoData();
            }

            return FromEndpoints(times[0], times[times.Length - 1]);
        }

        /// <summary>
        /// Creates a range result from first and last timestamps.
        /// </summary>
        /// <param name="first">The first timestamp returned by DSS.</param>
        /// <param name="last">The last timestamp returned by DSS.</param>
        /// <returns>The resolved display range result.</returns>
        internal static DssPathRangeResult FromEndpoints(DateTime first, DateTime last)
        {
            string display = FormatRange(first, last);
            return new DssPathRangeResult(true, display, $"Data range: {display}");
        }

        /// <summary>
        /// Creates a result for records with no readable timestamps.
        /// </summary>
        /// <returns>A display result indicating no data.</returns>
        internal static DssPathRangeResult NoData()
        {
            return new DssPathRangeResult(true, "No data", "No readable timestamps were returned for this DSS record.");
        }

        /// <summary>
        /// Creates a range-resolution failure result.
        /// </summary>
        /// <param name="message">The failure message.</param>
        /// <returns>A result that preserves the catalog D-part.</returns>
        internal static DssPathRangeResult Error(string message)
        {
            return new DssPathRangeResult(false, "", message);
        }

        /// <summary>
        /// Formats a DSS-style date or date-time range.
        /// </summary>
        /// <param name="first">The first timestamp.</param>
        /// <param name="last">The last timestamp.</param>
        /// <returns>A single timestamp or timestamp range.</returns>
        internal static string FormatRange(DateTime first, DateTime last)
        {
            string firstText = FormatTimestamp(first);
            string lastText = FormatTimestamp(last);
            return first == last ? firstText : $"{firstText}-{lastText}";
        }

        /// <summary>
        /// Formats one timestamp using DSS-style date text.
        /// </summary>
        /// <param name="time">The timestamp to format.</param>
        /// <returns>The formatted timestamp.</returns>
        internal static string FormatTimestamp(DateTime time)
        {
            string dateText = time.ToString("ddMMMyyyy", CultureInfo.InvariantCulture);
            if (time.TimeOfDay == TimeSpan.Zero)
            {
                return dateText;
            }

            string timeFormat = time.Second == 0 ? "HH:mm" : "HH:mm:ss";
            return $"{dateText} {time.ToString(timeFormat, CultureInfo.InvariantCulture)}";
        }
    }
}
