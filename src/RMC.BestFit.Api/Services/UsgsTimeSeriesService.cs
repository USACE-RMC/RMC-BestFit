using Numerics.Data;
using RMC.BestFit.Api.Services.Exceptions;

namespace RMC.BestFit.Api.Services
{
    /// <summary>
    /// Production <see cref="IUsgsTimeSeriesService"/> adapting the static
    /// <see cref="TimeSeriesDownload.FromUSGS(string, TimeSeriesDownload.TimeSeriesType, CancellationToken)"/> download
    /// and translating its failure modes into the API's typed exceptions.
    /// </summary>
    public class UsgsTimeSeriesService : IUsgsTimeSeriesService
    {
        /// <summary>
        /// The underlying download call. Defaults to the static Numerics downloader; tests inject
        /// a delegate to exercise the exception translation without network access.
        /// </summary>
        private readonly Func<string, TimeSeriesDownload.TimeSeriesType, Task<(TimeSeries TimeSeries, string RawText)>> _download;

        /// <summary>
        /// Constructs the service over the static Numerics USGS downloader.
        /// </summary>
        public UsgsTimeSeriesService()
            : this(static (siteNumber, seriesType) => TimeSeriesDownload.FromUSGS(siteNumber, seriesType))
        {
        }

        /// <summary>
        /// Constructs the service over a custom download delegate. Intended for tests, which
        /// cannot fake the static Numerics downloader directly.
        /// </summary>
        /// <param name="download">The download call to adapt.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="download"/> is null.</exception>
        public UsgsTimeSeriesService(Func<string, TimeSeriesDownload.TimeSeriesType, Task<(TimeSeries TimeSeries, string RawText)>> download)
        {
            _download = download ?? throw new ArgumentNullException(nameof(download));
        }

        /// <inheritdoc/>
        public async Task<(TimeSeries TimeSeries, string RawText)> DownloadAsync(
            string siteNumber,
            TimeSeriesDownload.TimeSeriesType seriesType,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TimeSeries timeSeries;
            string rawText;
            try
            {
                (timeSeries, rawText) = await _download(siteNumber, seriesType);
            }
            catch (ArgumentException)
            {
                // Invalid site number format or unsupported series type — the request itself is
                // wrong. Rethrow for the controller layer to map to HTTP 400.
                throw;
            }
            catch (OperationCanceledException)
            {
                // Preserve cancellation semantics (mapped to HTTP 499 by the controller layer).
                throw;
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("internet", StringComparison.OrdinalIgnoreCase))
            {
                throw new UsgsUnavailableException(
                    "The USGS water services could not be reached: no internet connection.", statusCode: 503, innerException: ex);
            }
            catch (Exception ex) when (ex.Message.Contains("No data found", StringComparison.OrdinalIgnoreCase))
            {
                throw new UsgsDataNotFoundException(
                    $"USGS site '{siteNumber}' returned no {seriesType} data. Verify the site number and that the site records this series type.");
            }
            catch (Exception ex)
            {
                throw new UsgsUnavailableException(
                    $"The USGS download for site '{siteNumber}' ({seriesType}) failed: {ex.Message}", statusCode: 502, innerException: ex);
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (timeSeries == null || timeSeries.Count == 0)
            {
                throw new UsgsDataNotFoundException(
                    $"USGS site '{siteNumber}' returned no {seriesType} data. Verify the site number and that the site records this series type.");
            }

            return (timeSeries, rawText);
        }
    }
}
