namespace RMC.BestFit.Api.Store
{
    /// <summary>
    /// Thread-safe in-memory store for the API's server-side resources. All state created through
    /// the REST endpoints or MCP tools lives here, keyed by GUID, for the lifetime of the host process.
    /// </summary>
    public interface IResourceStore
    {
        /// <summary>
        /// The total number of resources currently held across all three collections.
        /// </summary>
        int TotalCount { get; }

        /// <summary>
        /// Adds a time-series resource to the store.
        /// </summary>
        /// <param name="resource">The resource to add.</param>
        /// <returns>The added resource, for fluent use.</returns>
        /// <exception cref="Services.Exceptions.ResourceConflictException">Thrown when the store is at capacity.</exception>
        TimeSeriesResource AddTimeSeries(TimeSeriesResource resource);

        /// <summary>
        /// Returns the time-series resource with the given id, or null when it does not exist.
        /// </summary>
        /// <param name="id">The resource id.</param>
        /// <returns>The resource, or null.</returns>
        TimeSeriesResource? GetTimeSeries(Guid id);

        /// <summary>
        /// Lists all time-series resources ordered by creation time.
        /// </summary>
        /// <returns>The resources ordered by <see cref="ResourceBase.CreatedUtc"/>.</returns>
        IReadOnlyList<TimeSeriesResource> ListTimeSeries();

        /// <summary>
        /// Deletes the time-series resource with the given id.
        /// </summary>
        /// <param name="id">The resource id.</param>
        /// <returns>True when the resource existed and was removed; false when it did not exist.</returns>
        bool DeleteTimeSeries(Guid id);

        /// <summary>
        /// Adds an input-data resource to the store.
        /// </summary>
        /// <param name="resource">The resource to add.</param>
        /// <returns>The added resource, for fluent use.</returns>
        /// <exception cref="Services.Exceptions.ResourceConflictException">Thrown when the store is at capacity.</exception>
        InputDataResource AddInputData(InputDataResource resource);

        /// <summary>
        /// Returns the input-data resource with the given id, or null when it does not exist.
        /// </summary>
        /// <param name="id">The resource id.</param>
        /// <returns>The resource, or null.</returns>
        InputDataResource? GetInputData(Guid id);

        /// <summary>
        /// Lists all input-data resources ordered by creation time.
        /// </summary>
        /// <returns>The resources ordered by <see cref="ResourceBase.CreatedUtc"/>.</returns>
        IReadOnlyList<InputDataResource> ListInputData();

        /// <summary>
        /// Deletes the input-data resource with the given id.
        /// </summary>
        /// <param name="id">The resource id.</param>
        /// <returns>True when the resource existed and was removed; false when it did not exist.</returns>
        bool DeleteInputData(Guid id);

        /// <summary>
        /// Adds an analysis resource to the store.
        /// </summary>
        /// <param name="resource">The resource to add.</param>
        /// <returns>The added resource, for fluent use.</returns>
        /// <exception cref="Services.Exceptions.ResourceConflictException">Thrown when the store is at capacity.</exception>
        AnalysisResource AddAnalysis(AnalysisResource resource);

        /// <summary>
        /// Returns the analysis resource with the given id, or null when it does not exist.
        /// </summary>
        /// <param name="id">The resource id.</param>
        /// <returns>The resource, or null.</returns>
        AnalysisResource? GetAnalysis(Guid id);

        /// <summary>
        /// Lists all analysis resources ordered by creation time.
        /// </summary>
        /// <returns>The resources ordered by <see cref="ResourceBase.CreatedUtc"/>.</returns>
        IReadOnlyList<AnalysisResource> ListAnalyses();

        /// <summary>
        /// Deletes the analysis resource with the given id.
        /// </summary>
        /// <param name="id">The resource id.</param>
        /// <returns>True when the resource existed and was removed; false when it did not exist.</returns>
        bool DeleteAnalysis(Guid id);
    }
}
