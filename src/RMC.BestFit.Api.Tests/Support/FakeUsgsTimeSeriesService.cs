using Numerics.Data;
using RMC.BestFit.Api.Services;

namespace RMC.BestFit.Api.Tests.Support
{
    /// <summary>
    /// Configurable in-memory fake of the USGS download seam: returns a canned series or throws a
    /// configured exception, and records the last requested site/series type for assertions.
    /// </summary>
    public class FakeUsgsTimeSeriesService : IUsgsTimeSeriesService
    {
        /// <summary>
        /// The series returned by <see cref="DownloadAsync"/> when no exception is configured.
        /// </summary>
        public TimeSeries? Result { get; set; }

        /// <summary>
        /// The raw text returned alongside <see cref="Result"/>.
        /// </summary>
        public string RawText { get; set; } = "canned";

        /// <summary>
        /// When set, <see cref="DownloadAsync"/> throws this exception instead of returning a series.
        /// </summary>
        public Exception? ExceptionToThrow { get; set; }

        /// <summary>
        /// The site number of the most recent download request.
        /// </summary>
        public string? LastSiteNumber { get; private set; }

        /// <summary>
        /// The series type of the most recent download request.
        /// </summary>
        public TimeSeriesDownload.TimeSeriesType? LastSeriesType { get; private set; }

        /// <summary>
        /// Returns the canned series or throws the configured exception.
        /// </summary>
        /// <param name="siteNumber">The requested site number (recorded).</param>
        /// <param name="seriesType">The requested series type (recorded).</param>
        /// <param name="cancellationToken">Observed for pre-cancelled tokens.</param>
        /// <returns>The canned series and raw text.</returns>
        public Task<(TimeSeries TimeSeries, string RawText)> DownloadAsync(
            string siteNumber,
            TimeSeriesDownload.TimeSeriesType seriesType,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastSiteNumber = siteNumber;
            LastSeriesType = seriesType;
            if (ExceptionToThrow != null) throw ExceptionToThrow;
            if (Result == null) throw new InvalidOperationException("FakeUsgsTimeSeriesService.Result was not configured.");
            return Task.FromResult((Result, RawText));
        }
    }
}
