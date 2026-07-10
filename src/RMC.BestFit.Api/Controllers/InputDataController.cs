using Microsoft.AspNetCore.Mvc;
using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Mappers;
using RMC.BestFit.Api.Services;

namespace RMC.BestFit.Api.Controllers
{
    /// <summary>
    /// Endpoints for creating, inspecting, and deleting input-data resources — the observation
    /// sets (systematic records, censored data, perception thresholds) that univariate and
    /// Bulletin 17C analyses are fit to.
    /// </summary>
    [Route("api/inputdata")]
    public class InputDataController : ApiControllerBase
    {
        /// <summary>
        /// The controller's logger.
        /// </summary>
        private readonly ILogger<InputDataController> _logger;

        /// <summary>
        /// The input-data service shared with the MCP tools.
        /// </summary>
        private readonly IInputDataService _service;

        /// <summary>
        /// Constructs the controller.
        /// </summary>
        /// <param name="logger">The controller's logger.</param>
        /// <param name="service">The input-data service.</param>
        /// <exception cref="ArgumentNullException">Thrown when a dependency is null.</exception>
        public InputDataController(ILogger<InputDataController> logger, IInputDataService service)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        /// <summary>
        /// Builds an input-data resource from client-supplied observations (systematic record,
        /// optionally interval-censored and perception-threshold data).
        /// </summary>
        /// <param name="request">The manual creation request.</param>
        /// <returns>The created resource summary, including the id to reference when creating analyses.</returns>
        /// <response code="201">The resource was created.</response>
        /// <response code="400">The observations failed validation; see validationErrors.</response>
        [HttpPost("manual")]
        [ProducesResponseType(typeof(InputDataResourceResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(InputDataResourceResponse), StatusCodes.Status400BadRequest)]
        public Task<ActionResult<InputDataResourceResponse>> CreateManual([FromBody] CreateManualInputDataRequest request)
        {
            return ExecuteAsync(() =>
            {
                var resource = _service.CreateManual(request);
                return InputDataMapper.ToResourceResponse(resource);
            }, _logger, "inputdata.manual", StatusCodes.Status201Created);
        }

        /// <summary>
        /// Extracts block maxima (e.g., annual maxima by water year) from a linked time-series
        /// resource, typically a USGS daily-flow download.
        /// </summary>
        /// <param name="request">The block-maxima creation request.</param>
        /// <returns>The created resource summary.</returns>
        /// <response code="201">The resource was created.</response>
        /// <response code="400">Extraction produced no observations or the result failed validation.</response>
        /// <response code="404">The linked time-series resource does not exist.</response>
        [HttpPost("block-max")]
        [ProducesResponseType(typeof(InputDataResourceResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(InputDataResourceResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(InputDataResourceResponse), StatusCodes.Status404NotFound)]
        public Task<ActionResult<InputDataResourceResponse>> CreateBlockMax([FromBody] CreateBlockMaxInputDataRequest request)
        {
            return ExecuteAsync(() =>
            {
                var resource = _service.CreateBlockMax(request);
                return InputDataMapper.ToResourceResponse(resource);
            }, _logger, "inputdata.blockmax", StatusCodes.Status201Created);
        }

        /// <summary>
        /// Extracts independent peaks-over-threshold events from a linked time-series resource.
        /// </summary>
        /// <param name="request">The peaks-over-threshold creation request.</param>
        /// <returns>The created resource summary (lambda reports the events-per-year rate).</returns>
        /// <response code="201">The resource was created.</response>
        /// <response code="400">Extraction produced no events or the result failed validation.</response>
        /// <response code="404">The linked time-series resource does not exist.</response>
        [HttpPost("peaks-over-threshold")]
        [ProducesResponseType(typeof(InputDataResourceResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(InputDataResourceResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(InputDataResourceResponse), StatusCodes.Status404NotFound)]
        public Task<ActionResult<InputDataResourceResponse>> CreatePeaksOverThreshold([FromBody] CreatePotInputDataRequest request)
        {
            return ExecuteAsync(() =>
            {
                var resource = _service.CreatePeaksOverThreshold(request);
                return InputDataMapper.ToResourceResponse(resource);
            }, _logger, "inputdata.pot", StatusCodes.Status201Created);
        }

        /// <summary>
        /// Downloads the USGS annual peak-flow file for a site and stores it directly as an
        /// input-data resource (no intermediate time-series resource required).
        /// </summary>
        /// <param name="request">The USGS peak download request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The created resource summary.</returns>
        /// <response code="201">The resource was created.</response>
        /// <response code="400">The site number or series type is invalid.</response>
        /// <response code="404">The site returned no peak data.</response>
        /// <response code="502">The USGS service returned an error.</response>
        /// <response code="503">The USGS service could not be reached.</response>
        [HttpPost("usgs-peaks")]
        [ProducesResponseType(typeof(InputDataResourceResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(InputDataResourceResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(InputDataResourceResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(InputDataResourceResponse), StatusCodes.Status502BadGateway)]
        public Task<ActionResult<InputDataResourceResponse>> CreateFromUsgsPeaks(
            [FromBody] CreateUsgsPeaksInputDataRequest request,
            CancellationToken cancellationToken)
        {
            return ExecuteAsync(async () =>
            {
                var resource = await _service.CreateFromUsgsPeaksAsync(request, cancellationToken);
                return InputDataMapper.ToResourceResponse(resource);
            }, _logger, "inputdata.usgspeaks", StatusCodes.Status201Created);
        }

        /// <summary>
        /// Lists all input-data resources currently held by the server.
        /// </summary>
        /// <returns>Summaries of every input-data resource.</returns>
        /// <response code="200">The listing.</response>
        [HttpGet]
        [ProducesResponseType(typeof(InputDataListResponse), StatusCodes.Status200OK)]
        public Task<ActionResult<InputDataListResponse>> List()
        {
            return ExecuteAsync(() => InputDataMapper.ToListResponse(_service.List()), _logger, "inputdata.list");
        }

        /// <summary>
        /// Returns an input-data resource summary, optionally with the full observation lists
        /// (including computed plotting positions).
        /// </summary>
        /// <param name="id">The resource id.</param>
        /// <param name="includeData">True to include the exact/interval/threshold observation lists.</param>
        /// <returns>The resource summary and optional observations.</returns>
        /// <response code="200">The resource.</response>
        /// <response code="404">No resource has the id.</response>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(InputDataResourceResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(InputDataResourceResponse), StatusCodes.Status404NotFound)]
        public Task<ActionResult<InputDataResourceResponse>> Get(Guid id, [FromQuery] bool includeData = false)
        {
            return ExecuteAsync(() =>
            {
                var resource = _service.Get(id);
                return InputDataMapper.ToResourceResponse(resource, includeData);
            }, _logger, "inputdata.get");
        }

        /// <summary>
        /// Computes the sample summary statistics of an input-data resource over all observations.
        /// </summary>
        /// <param name="id">The resource id.</param>
        /// <returns>Summary statistics keyed by measure name.</returns>
        /// <response code="200">The statistics.</response>
        /// <response code="404">No resource has the id.</response>
        [HttpGet("{id:guid}/summary-statistics")]
        [ProducesResponseType(typeof(SummaryStatisticsResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(SummaryStatisticsResponse), StatusCodes.Status404NotFound)]
        public Task<ActionResult<SummaryStatisticsResponse>> GetSummaryStatistics(Guid id)
        {
            return ExecuteAsync(() => new SummaryStatisticsResponse
            {
                InputDataId = id,
                Statistics = _service.GetSummaryStatistics(id)
            }, _logger, "inputdata.summarystatistics");
        }

        /// <summary>
        /// Deletes an input-data resource. Analyses already created from it are unaffected
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
                return new DeleteResourceResponse { DeletedId = id, ResourceType = "inputData" };
            }, _logger, "inputdata.delete");
        }
    }
}
