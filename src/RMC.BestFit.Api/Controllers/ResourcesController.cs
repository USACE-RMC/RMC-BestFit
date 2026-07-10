using Microsoft.AspNetCore.Mvc;
using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Helpers;
using RMC.BestFit.Api.Store;

namespace RMC.BestFit.Api.Controllers
{
    /// <summary>
    /// Cross-cutting resource overview so clients (and agents mid-session) can re-orient
    /// themselves: every resource id, type, and a one-line detail.
    /// </summary>
    [Route("api/resources")]
    public class ResourcesController : ApiControllerBase
    {
        /// <summary>
        /// The controller's logger.
        /// </summary>
        private readonly ILogger<ResourcesController> _logger;

        /// <summary>
        /// The in-memory resource store.
        /// </summary>
        private readonly IResourceStore _store;

        /// <summary>
        /// Constructs the controller.
        /// </summary>
        /// <param name="logger">The controller's logger.</param>
        /// <param name="store">The in-memory resource store.</param>
        /// <exception cref="ArgumentNullException">Thrown when a dependency is null.</exception>
        public ResourcesController(ILogger<ResourcesController> logger, IResourceStore store)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        /// <summary>
        /// Lists every resource currently held by the server (time series, input data, analyses)
        /// with one-line summaries, ordered by creation time within each type.
        /// </summary>
        /// <returns>The resource overview.</returns>
        /// <response code="200">The overview.</response>
        [HttpGet]
        [ProducesResponseType(typeof(ResourcesOverviewResponse), StatusCodes.Status200OK)]
        public Task<ActionResult<ResourcesOverviewResponse>> GetOverview()
        {
            return ExecuteAsync(() =>
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
                return response;
            }, _logger, "resources.overview");
        }
    }
}
