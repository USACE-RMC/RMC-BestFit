using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Store;

namespace RMC.BestFit.Api.Services
{
    /// <summary>
    /// Creates, lists, and deletes server-side input-data resources (model-layer data frames).
    /// Shared by the REST controllers and the MCP tools.
    /// </summary>
    public interface IInputDataService
    {
        /// <summary>
        /// Builds a data frame from client-supplied observations and stores it as a resource.
        /// </summary>
        /// <param name="request">The manual creation request.</param>
        /// <returns>The created resource.</returns>
        /// <exception cref="Exceptions.RequestValidationException">Thrown when the data frame fails model-layer validation.</exception>
        InputDataResource CreateManual(CreateManualInputDataRequest request);

        /// <summary>
        /// Extracts block maxima (e.g., annual maxima by water year) from a linked time-series
        /// resource and stores the result as a resource.
        /// </summary>
        /// <param name="request">The block-maxima creation request.</param>
        /// <returns>The created resource.</returns>
        /// <exception cref="Exceptions.ResourceNotFoundException">Thrown when the linked time series does not exist.</exception>
        /// <exception cref="Exceptions.RequestValidationException">Thrown when extraction produces no observations or the data frame fails validation.</exception>
        InputDataResource CreateBlockMax(CreateBlockMaxInputDataRequest request);

        /// <summary>
        /// Extracts independent peaks-over-threshold events from a linked time-series resource and
        /// stores the result as a resource.
        /// </summary>
        /// <param name="request">The peaks-over-threshold creation request.</param>
        /// <returns>The created resource.</returns>
        /// <exception cref="Exceptions.ResourceNotFoundException">Thrown when the linked time series does not exist.</exception>
        /// <exception cref="Exceptions.RequestValidationException">Thrown when extraction produces no observations or the data frame fails validation.</exception>
        InputDataResource CreatePeaksOverThreshold(CreatePotInputDataRequest request);

        /// <summary>
        /// Downloads the USGS annual peak-flow file for a site and stores it as a resource.
        /// </summary>
        /// <param name="request">The USGS peak download request.</param>
        /// <param name="cancellationToken">Cancellation token for the download.</param>
        /// <returns>The created resource.</returns>
        /// <exception cref="ArgumentException">Thrown when the series type is not peak discharge or peak stage.</exception>
        Task<InputDataResource> CreateFromUsgsPeaksAsync(CreateUsgsPeaksInputDataRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Lists all input-data resources ordered by creation time.
        /// </summary>
        /// <returns>The resources ordered by creation time.</returns>
        IReadOnlyList<InputDataResource> List();

        /// <summary>
        /// Returns the input-data resource with the given id.
        /// </summary>
        /// <param name="id">The resource id.</param>
        /// <returns>The resource.</returns>
        /// <exception cref="Exceptions.ResourceNotFoundException">Thrown when no resource has the id.</exception>
        InputDataResource Get(Guid id);

        /// <summary>
        /// Computes the sample summary statistics of an input-data resource over all data.
        /// </summary>
        /// <param name="id">The resource id.</param>
        /// <returns>Summary statistics keyed by measure name.</returns>
        /// <exception cref="Exceptions.ResourceNotFoundException">Thrown when no resource has the id.</exception>
        Dictionary<string, double> GetSummaryStatistics(Guid id);

        /// <summary>
        /// Deletes the input-data resource with the given id.
        /// </summary>
        /// <param name="id">The resource id.</param>
        /// <exception cref="Exceptions.ResourceNotFoundException">Thrown when no resource has the id.</exception>
        void Delete(Guid id);
    }
}
