using Numerics.Data;
using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Helpers;
using RMC.BestFit.Api.Services.Exceptions;
using RMC.BestFit.Api.Store;

namespace RMC.BestFit.Api.Services
{
    /// <summary>
    /// Default <see cref="ITimeSeriesService"/> implementation backed by the in-memory resource
    /// store and the USGS download adapter.
    /// </summary>
    public class TimeSeriesService : ITimeSeriesService
    {
        /// <summary>
        /// The in-memory resource store.
        /// </summary>
        private readonly IResourceStore _store;

        /// <summary>
        /// The USGS download adapter (mockable seam for tests).
        /// </summary>
        private readonly IUsgsTimeSeriesService _usgs;

        /// <summary>
        /// Constructs the service.
        /// </summary>
        /// <param name="store">The in-memory resource store.</param>
        /// <param name="usgs">The USGS download adapter.</param>
        /// <exception cref="ArgumentNullException">Thrown when either dependency is null.</exception>
        public TimeSeriesService(IResourceStore store, IUsgsTimeSeriesService usgs)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _usgs = usgs ?? throw new ArgumentNullException(nameof(usgs));
        }

        /// <inheritdoc/>
        public async Task<TimeSeriesResource> CreateFromUsgsAsync(CreateUsgsTimeSeriesRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            var (timeSeries, _) = await _usgs.DownloadAsync(request.SiteNumber, request.SeriesType, cancellationToken);

            var resource = new TimeSeriesResource(timeSeries)
            {
                Name = string.IsNullOrWhiteSpace(request.Name)
                    ? $"USGS {request.SiteNumber} {EnumHelper.ToCamelCase(request.SeriesType.ToString())}"
                    : request.Name,
                Description = request.Description,
                Source = TimeSeriesSource.Usgs,
                UsgsSiteNumber = request.SiteNumber,
                UsgsSeriesType = request.SeriesType
            };
            return _store.AddTimeSeries(resource);
        }

        /// <inheritdoc/>
        public TimeSeriesResource CreateManual(CreateManualTimeSeriesRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (request.Points == null || request.Points.Count == 0)
            {
                throw new ArgumentException("At least one point is required to create a time series.", nameof(request));
            }

            var timeSeries = new TimeSeries(request.TimeInterval);
            foreach (var point in request.Points.OrderBy(p => p.DateTime))
            {
                timeSeries.Add(new SeriesOrdinate<DateTime, double>(point.DateTime, point.Value));
            }

            var resource = new TimeSeriesResource(timeSeries)
            {
                Name = string.IsNullOrWhiteSpace(request.Name) ? "Manual time series" : request.Name,
                Description = request.Description,
                Source = TimeSeriesSource.Manual
            };
            return _store.AddTimeSeries(resource);
        }

        /// <inheritdoc/>
        public IReadOnlyList<TimeSeriesResource> List()
        {
            return _store.ListTimeSeries();
        }

        /// <inheritdoc/>
        public TimeSeriesResource Get(Guid id)
        {
            return _store.GetTimeSeries(id) ?? throw new ResourceNotFoundException("time series", id);
        }

        /// <inheritdoc/>
        public void Delete(Guid id)
        {
            if (!_store.DeleteTimeSeries(id))
            {
                throw new ResourceNotFoundException("time series", id);
            }
        }
    }
}
