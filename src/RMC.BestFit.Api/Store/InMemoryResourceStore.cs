using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using RMC.BestFit.Api.Configuration;
using RMC.BestFit.Api.Services.Exceptions;

namespace RMC.BestFit.Api.Store
{
    /// <summary>
    /// Default <see cref="IResourceStore"/> implementation backed by concurrent dictionaries.
    /// Registered as a DI singleton so resource ids remain valid across requests and across the
    /// REST/MCP boundary for the lifetime of the host process.
    /// </summary>
    /// <remarks>
    /// The store enforces a soft total-capacity cap (<see cref="ApiOptions.MaxResources"/>) and
    /// rejects further creations with <see cref="ResourceConflictException"/> (HTTP 409). There is
    /// deliberately no eviction: silently deleting a resource an agent still holds an id for would
    /// be worse than asking the client to delete resources explicitly.
    /// </remarks>
    public class InMemoryResourceStore : IResourceStore
    {
        /// <summary>
        /// Time-series resources keyed by id.
        /// </summary>
        private readonly ConcurrentDictionary<Guid, TimeSeriesResource> _timeSeries = new();

        /// <summary>
        /// Input-data resources keyed by id.
        /// </summary>
        private readonly ConcurrentDictionary<Guid, InputDataResource> _inputData = new();

        /// <summary>
        /// Analysis resources keyed by id.
        /// </summary>
        private readonly ConcurrentDictionary<Guid, AnalysisResource> _analyses = new();

        /// <summary>
        /// The configured API limits (capacity cap).
        /// </summary>
        private readonly ApiOptions _options;

        /// <summary>
        /// Constructs the store with the configured API limits.
        /// </summary>
        /// <param name="options">The API options carrying <see cref="ApiOptions.MaxResources"/>.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
        public InMemoryResourceStore(IOptions<ApiOptions> options)
        {
            if (options is null) throw new ArgumentNullException(nameof(options));
            _options = options.Value;
        }

        /// <inheritdoc/>
        public int TotalCount => _timeSeries.Count + _inputData.Count + _analyses.Count;

        /// <inheritdoc/>
        public TimeSeriesResource AddTimeSeries(TimeSeriesResource resource)
        {
            ArgumentNullException.ThrowIfNull(resource);
            EnsureCapacity();
            _timeSeries[resource.Id] = resource;
            return resource;
        }

        /// <inheritdoc/>
        public TimeSeriesResource? GetTimeSeries(Guid id)
        {
            return _timeSeries.TryGetValue(id, out var resource) ? resource : null;
        }

        /// <inheritdoc/>
        public IReadOnlyList<TimeSeriesResource> ListTimeSeries()
        {
            return _timeSeries.Values.OrderBy(r => r.CreatedUtc).ToList();
        }

        /// <inheritdoc/>
        public bool DeleteTimeSeries(Guid id)
        {
            return _timeSeries.TryRemove(id, out _);
        }

        /// <inheritdoc/>
        public InputDataResource AddInputData(InputDataResource resource)
        {
            ArgumentNullException.ThrowIfNull(resource);
            EnsureCapacity();
            _inputData[resource.Id] = resource;
            return resource;
        }

        /// <inheritdoc/>
        public InputDataResource? GetInputData(Guid id)
        {
            return _inputData.TryGetValue(id, out var resource) ? resource : null;
        }

        /// <inheritdoc/>
        public IReadOnlyList<InputDataResource> ListInputData()
        {
            return _inputData.Values.OrderBy(r => r.CreatedUtc).ToList();
        }

        /// <inheritdoc/>
        public bool DeleteInputData(Guid id)
        {
            return _inputData.TryRemove(id, out _);
        }

        /// <inheritdoc/>
        public AnalysisResource AddAnalysis(AnalysisResource resource)
        {
            ArgumentNullException.ThrowIfNull(resource);
            EnsureCapacity();
            _analyses[resource.Id] = resource;
            return resource;
        }

        /// <inheritdoc/>
        public AnalysisResource? GetAnalysis(Guid id)
        {
            return _analyses.TryGetValue(id, out var resource) ? resource : null;
        }

        /// <inheritdoc/>
        public IReadOnlyList<AnalysisResource> ListAnalyses()
        {
            return _analyses.Values.OrderBy(r => r.CreatedUtc).ToList();
        }

        /// <inheritdoc/>
        public bool DeleteAnalysis(Guid id)
        {
            return _analyses.TryRemove(id, out _);
        }

        /// <summary>
        /// Rejects a creation when the store already holds the configured maximum number of
        /// resources. The check-then-add is not atomic; under concurrent creations the cap may be
        /// exceeded by a handful of entries, which is acceptable for a soft memory-protection limit.
        /// </summary>
        /// <exception cref="ResourceConflictException">Thrown when the store is at capacity.</exception>
        private void EnsureCapacity()
        {
            if (TotalCount >= _options.MaxResources)
            {
                throw new ResourceConflictException(
                    $"The resource store is at its configured capacity ({_options.MaxResources}). " +
                    "Delete resources you no longer need (DELETE api/timeseries/{id}, api/inputdata/{id}, api/analyses/.../{id}) and retry.");
            }
        }
    }
}
