using System.ComponentModel;
using ModelContextProtocol.Server;
using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Helpers;
using RMC.BestFit.Api.Mappers;
using RMC.BestFit.Api.Store;

namespace RMC.BestFit.Api.Mcp
{
    /// <summary>
    /// MCP tools for discovery: supported distributions, accepted enum values, server defaults,
    /// and the cross-cutting resource overview. Agents should call these before constructing
    /// analysis requests.
    /// </summary>
    [McpServerToolType]
    public class MetadataTools
    {
        /// <summary>
        /// The in-memory resource store (for the resource overview).
        /// </summary>
        private readonly IResourceStore _store;

        /// <summary>
        /// The configured API limits.
        /// </summary>
        private readonly Configuration.ApiOptions _options;

        /// <summary>
        /// Constructs the tools with their service dependencies.
        /// </summary>
        /// <param name="store">The in-memory resource store.</param>
        /// <param name="options">The configured API limits.</param>
        public MetadataTools(IResourceStore store, Microsoft.Extensions.Options.IOptions<Configuration.ApiOptions> options)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _options = (options ?? throw new ArgumentNullException(nameof(options))).Value;
        }

        /// <summary>
        /// Lists the supported distributions, accepted enum values, and server defaults.
        /// </summary>
        /// <returns>JSON with distributions (name/displayName/supportedBy), enum option lists, default AEP ordinates, and server limits.</returns>
        [McpServerTool(Name = "get_metadata")]
        [Description("Get the RMC-BestFit metadata: supported probability distributions (with which analysis kinds accept each), every accepted enum value (samplers, uncertainty methods, time blocks, USGS series types, ...), the default annual-exceedance-probability ordinates, and server limits. Call this before constructing analysis requests.")]
        public string GetMetadata()
        {
            var payload = new
            {
                distributions = MetadataMapper.ToDistributionsResponse().Distributions,
                enums = MetadataMapper.ToEnumOptionsResponse(),
                defaults = new DefaultsResponse
                {
                    ProbabilityOrdinates = new Numerics.Data.ProbabilityOrdinates().ToList(),
                    MaxIterations = _options.MaxIterations,
                    MaxConcurrentRuns = _options.MaxConcurrentRuns,
                    MaxResources = _options.MaxResources,
                    Notes = new List<string>
                    {
                        "Probability ordinates are annual exceedance probabilities (AEP); e.g., 0.01 is the 100-year event.",
                        "Run tools are synchronous and may take seconds to about a minute for MCMC analyses.",
                        "Resources are held in memory only and are lost when the server restarts."
                    }
                }
            };
            return McpJson.Serialize(payload);
        }

        /// <summary>
        /// Lists every resource currently held by the server.
        /// </summary>
        /// <returns>JSON with per-type counts and one-line summaries (id, type, name, detail).</returns>
        [McpServerTool(Name = "list_resources")]
        [Description("List every resource on the server (time series, input data, analyses) with ids and one-line summaries. Use to re-orient mid-session or find an id you created earlier.")]
        public string ListResources()
        {
            var response = new ResourcesOverviewResponse();
            foreach (var resource in _store.ListTimeSeries())
            {
                response.Resources.Add(new ResourceSummaryDto
                {
                    Id = resource.Id,
                    ResourceType = "timeSeries",
                    Name = resource.Name,
                    CreatedUtc = resource.CreatedUtc,
                    Detail = $"{resource.PointCount} points, {resource.StartDate:yyyy-MM-dd} to {resource.EndDate:yyyy-MM-dd}"
                });
            }
            foreach (var resource in _store.ListInputData())
            {
                response.Resources.Add(new ResourceSummaryDto
                {
                    Id = resource.Id,
                    ResourceType = "inputData",
                    Name = resource.Name,
                    CreatedUtc = resource.CreatedUtc,
                    Detail = $"{EnumHelper.ToCamelCase(resource.Method.ToString())}, {resource.DataFrame.ExactSeries.Count} exact observations"
                });
            }
            foreach (var resource in _store.ListAnalyses())
            {
                response.Resources.Add(new ResourceSummaryDto
                {
                    Id = resource.Id,
                    ResourceType = "analysis",
                    Name = resource.Name,
                    CreatedUtc = resource.CreatedUtc,
                    Detail = $"{EnumHelper.ToCamelCase(resource.Kind.ToString())}, state {EnumHelper.ToCamelCase(resource.State.ToString())}"
                });
            }
            response.TimeSeriesCount = _store.ListTimeSeries().Count;
            response.InputDataCount = _store.ListInputData().Count;
            response.AnalysisCount = _store.ListAnalyses().Count;
            return McpJson.Serialize(response);
        }

        /// <summary>
        /// Deletes a resource of any type by id.
        /// </summary>
        /// <param name="resourceType">The resource type: "timeSeries", "inputData", or "analysis".</param>
        /// <param name="id">The resource id.</param>
        /// <returns>JSON confirming the deletion.</returns>
        /// <exception cref="ArgumentException">Thrown when the resource type is not recognized.</exception>
        /// <exception cref="Services.Exceptions.ResourceNotFoundException">Thrown when no resource of that type has the id.</exception>
        [McpServerTool(Name = "delete_resource")]
        [Description("Delete a resource by type and id. Analyses created from a deleted resource are unaffected (they hold their own copies of the data). Use to free store capacity.")]
        public string DeleteResource(
            [Description("Resource type: timeSeries, inputData, or analysis.")] string resourceType,
            [Description("The resource id.")] Guid id)
        {
            bool deleted = resourceType?.Trim().ToLowerInvariant() switch
            {
                "timeseries" => _store.DeleteTimeSeries(id),
                "inputdata" => _store.DeleteInputData(id),
                "analysis" => _store.DeleteAnalysis(id),
                _ => throw new ArgumentException($"'{resourceType}' is not a valid resource type. Accepted values: timeSeries, inputData, analysis.")
            };
            if (!deleted)
            {
                throw new Services.Exceptions.ResourceNotFoundException(resourceType!, id);
            }
            return McpJson.Serialize(new DeleteResourceResponse
            {
                DeletedId = id,
                ResourceType = resourceType,
                Timestamp = DateTime.UtcNow.ToString("O")
            });
        }
    }
}
