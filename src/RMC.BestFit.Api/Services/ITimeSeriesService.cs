using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Store;

namespace RMC.BestFit.Api.Services
{
    /// <summary>
    /// Creates, lists, and deletes server-side time-series resources. Shared by the REST
    /// controllers and the MCP tools.
    /// </summary>
    public interface ITimeSeriesService
    {
        /// <summary>
        /// Downloads a series from the USGS water services and stores it as a resource.
        /// </summary>
        /// <param name="request">The download request (site number, series type, name).</param>
        /// <param name="cancellationToken">Cancellation token for the download.</param>
        /// <returns>The created resource.</returns>
        Task<TimeSeriesResource> CreateFromUsgsAsync(CreateUsgsTimeSeriesRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Builds a series from client-supplied points and stores it as a resource.
        /// </summary>
        /// <param name="request">The manual creation request (interval, points, name).</param>
        /// <returns>The created resource.</returns>
        TimeSeriesResource CreateManual(CreateManualTimeSeriesRequest request);

        /// <summary>
        /// Lists all time-series resources ordered by creation time.
        /// </summary>
        /// <returns>The resources ordered by creation time.</returns>
        IReadOnlyList<TimeSeriesResource> List();

        /// <summary>
        /// Returns the time-series resource with the given id.
        /// </summary>
        /// <param name="id">The resource id.</param>
        /// <returns>The resource.</returns>
        /// <exception cref="Exceptions.ResourceNotFoundException">Thrown when no resource has the id.</exception>
        TimeSeriesResource Get(Guid id);

        /// <summary>
        /// Deletes the time-series resource with the given id.
        /// </summary>
        /// <param name="id">The resource id.</param>
        /// <exception cref="Exceptions.ResourceNotFoundException">Thrown when no resource has the id.</exception>
        void Delete(Guid id);
    }
}
