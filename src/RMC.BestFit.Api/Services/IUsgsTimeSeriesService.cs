using Numerics.Data;

namespace RMC.BestFit.Api.Services
{
    /// <summary>
    /// Abstraction over the USGS water-services download so the rest of the API can be tested
    /// without network access. The production implementation adapts the static
    /// <see cref="TimeSeriesDownload"/> class from the Numerics library.
    /// </summary>
    public interface IUsgsTimeSeriesService
    {
        /// <summary>
        /// Downloads a time series from the USGS water services.
        /// </summary>
        /// <param name="siteNumber">The 8-digit USGS surface-water site number.</param>
        /// <param name="seriesType">The series to download (daily, peak, instantaneous, or measured discharge/stage).</param>
        /// <param name="cancellationToken">Token observed before and after the download (the underlying client does not support mid-flight cancellation).</param>
        /// <returns>The downloaded series and the raw response text for provenance.</returns>
        /// <exception cref="ArgumentException">Thrown when the site number or series type is invalid (mapped to HTTP 400).</exception>
        /// <exception cref="Exceptions.UsgsDataNotFoundException">Thrown when the site returns no data for the series type (mapped to HTTP 404).</exception>
        /// <exception cref="Exceptions.UsgsUnavailableException">Thrown when the USGS service errors or there is no connectivity (mapped to HTTP 502/503).</exception>
        Task<(TimeSeries TimeSeries, string RawText)> DownloadAsync(
            string siteNumber,
            TimeSeriesDownload.TimeSeriesType seriesType,
            CancellationToken cancellationToken = default);
    }
}
