using Microsoft.AspNetCore.Mvc;
using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Mappers;
using RMC.BestFit.Api.Services;

namespace RMC.BestFit.Api.Controllers
{
    /// <summary>
    /// Endpoints for creating, inspecting, and deleting time-series resources — the raw data
    /// inputs for block-maxima extraction and rating curve analyses.
    /// </summary>
    [Route("api/timeseries")]
    public class TimeSeriesController : ApiControllerBase
    {
        /// <summary>
        /// The controller's logger.
        /// </summary>
        private readonly ILogger<TimeSeriesController> _logger;

        /// <summary>
        /// The time-series service shared with the MCP tools.
        /// </summary>
        private readonly ITimeSeriesService _service;

        /// <summary>
        /// Constructs the controller.
        /// </summary>
        /// <param name="logger">The controller's logger.</param>
        /// <param name="service">The time-series service.</param>
        /// <exception cref="ArgumentNullException">Thrown when a dependency is null.</exception>
        public TimeSeriesController(ILogger<TimeSeriesController> logger, ITimeSeriesService service)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        /// <summary>
        /// Downloads a series from the USGS water services and stores it as a resource. Use
        /// dailyDischarge for block-maxima workflows and measuredStage/measuredDischarge pairs for
        /// rating curves.
        /// </summary>
        /// <param name="request">The download request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The created resource summary, including the id to reference in later requests.</returns>
        /// <response code="201">The resource was created.</response>
        /// <response code="400">The site number or series type is invalid.</response>
        /// <response code="404">The site returned no data for the series type.</response>
        /// <response code="502">The USGS service returned an error.</response>
        /// <response code="503">The USGS service could not be reached.</response>
        [HttpPost("usgs")]
        [ProducesResponseType(typeof(TimeSeriesResourceResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(TimeSeriesResourceResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(TimeSeriesResourceResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(TimeSeriesResourceResponse), StatusCodes.Status502BadGateway)]
        public Task<ActionResult<TimeSeriesResourceResponse>> CreateFromUsgs(
            [FromBody] CreateUsgsTimeSeriesRequest request,
            CancellationToken cancellationToken)
        {
            return ExecuteAsync(async () =>
            {
                var resource = await _service.CreateFromUsgsAsync(request, cancellationToken);
                return TimeSeriesMapper.ToResourceResponse(resource);
            }, _logger, "timeseries.usgs", StatusCodes.Status201Created);
        }

        /// <summary>
        /// Builds a series from client-supplied points and stores it as a resource.
        /// </summary>
        /// <param name="request">The manual creation request.</param>
        /// <returns>The created resource summary.</returns>
        /// <response code="201">The resource was created.</response>
        /// <response code="400">The request is invalid (e.g., no points).</response>
        [HttpPost("manual")]
        [ProducesResponseType(typeof(TimeSeriesResourceResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(TimeSeriesResourceResponse), StatusCodes.Status400BadRequest)]
        public Task<ActionResult<TimeSeriesResourceResponse>> CreateManual([FromBody] CreateManualTimeSeriesRequest request)
        {
            return ExecuteAsync(() =>
            {
                var resource = _service.CreateManual(request);
                return TimeSeriesMapper.ToResourceResponse(resource);
            }, _logger, "timeseries.manual", StatusCodes.Status201Created);
        }

        /// <summary>
        /// Lists all time-series resources currently held by the server.
        /// </summary>
        /// <returns>Summaries of every time-series resource.</returns>
        /// <response code="200">The listing.</response>
        [HttpGet]
        [ProducesResponseType(typeof(TimeSeriesListResponse), StatusCodes.Status200OK)]
        public Task<ActionResult<TimeSeriesListResponse>> List()
        {
            return ExecuteAsync(() => TimeSeriesMapper.ToListResponse(_service.List()), _logger, "timeseries.list");
        }

        /// <summary>
        /// Returns a time-series resource summary, optionally with a page of ordinates.
        /// </summary>
        /// <param name="id">The resource id.</param>
        /// <param name="includePoints">True to include ordinate values (paged).</param>
        /// <param name="offset">Zero-based index of the first point to include. Default 0.</param>
        /// <param name="limit">Maximum number of points to include. Default 10,000.</param>
        /// <returns>The resource summary and optional points page.</returns>
        /// <response code="200">The resource.</response>
        /// <response code="404">No resource has the id.</response>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(TimeSeriesResourceResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(TimeSeriesResourceResponse), StatusCodes.Status404NotFound)]
        public Task<ActionResult<TimeSeriesResourceResponse>> Get(
            Guid id,
            [FromQuery] bool includePoints = false,
            [FromQuery] int offset = 0,
            [FromQuery] int limit = 10000)
        {
            return ExecuteAsync(() =>
            {
                var resource = _service.Get(id);
                return TimeSeriesMapper.ToResourceResponse(resource, includePoints, offset, limit);
            }, _logger, "timeseries.get");
        }

        /// <summary>
        /// Deletes a time-series resource. Analyses already created from it are unaffected
        /// (they hold their own copies of the data).
        /// </summary>
        /// <param name="id">The resource id.</param>
        /// <returns>Confirmation of the deletion.</returns>
        /// <response code="200">The resource was deleted.</response>
        /// <response code="404">No resource has the id.</response>
        [HttpDelete("{id:guid}")]
        [ProducesResponseType(typeof(DeleteResourceResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(DeleteResourceResponse), StatusCodes.Status404NotFound)]
        public Task<ActionResult<DeleteResourceResponse>> Delete(Guid id)
        {
            return ExecuteAsync(() =>
            {
                _service.Delete(id);
                return new DeleteResourceResponse { DeletedId = id, ResourceType = "timeSeries" };
            }, _logger, "timeseries.delete");
        }
    }
}
