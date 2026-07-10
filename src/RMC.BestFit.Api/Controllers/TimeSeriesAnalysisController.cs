using Microsoft.AspNetCore.Mvc;
using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Mappers;
using RMC.BestFit.Api.Services;
using RMC.BestFit.Api.Store;

namespace RMC.BestFit.Api.Controllers
{
    /// <summary>
    /// Endpoints for Bayesian MCMC time-series analyses over stored time-series resources. One
    /// route covers the AR, MA, ARIMA, and ARIMAX model families via the request's modelType
    /// discriminator; fields that do not apply to the chosen family are rejected, never silently
    /// ignored.
    /// </summary>
    [Route("api/analyses/timeseries")]
    public class TimeSeriesAnalysisController : ApiControllerBase
    {
        /// <summary>
        /// The controller's logger.
        /// </summary>
        private readonly ILogger<TimeSeriesAnalysisController> _logger;

        /// <summary>
        /// The analysis service shared with the MCP tools.
        /// </summary>
        private readonly IAnalysisService _service;

        /// <summary>
        /// Constructs the controller.
        /// </summary>
        /// <param name="logger">The controller's logger.</param>
        /// <param name="service">The analysis service.</param>
        /// <exception cref="ArgumentNullException">Thrown when a dependency is null.</exception>
        public TimeSeriesAnalysisController(ILogger<TimeSeriesAnalysisController> logger, IAnalysisService service)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        /// <summary>
        /// Creates a time-series analysis linked to a time-series resource. The analysis clones
        /// the series (and any ARIMAX covariates), so later edits or deletes of the source
        /// resources cannot affect it.
        /// </summary>
        /// <param name="request">The creation request.</param>
        /// <returns>The created analysis summary, including its id for run/results calls.</returns>
        /// <response code="201">The analysis was created.</response>
        /// <response code="400">A field does not apply to the model type, or a value is out of range.</response>
        /// <response code="404">The time-series or a covariate resource does not exist.</response>
        [HttpPost]
        [ProducesResponseType(typeof(AnalysisResourceResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(AnalysisResourceResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(AnalysisResourceResponse), StatusCodes.Status404NotFound)]
        public Task<ActionResult<AnalysisResourceResponse>> Create([FromBody] CreateTimeSeriesAnalysisRequest request)
        {
            return ExecuteAsync(() => AnalysisMapper.ToResourceResponse(_service.CreateTimeSeries(request)),
                _logger, "analyses.timeseries.create", StatusCodes.Status201Created);
        }

        /// <summary>
        /// Runs the analysis synchronously (Bayesian MCMC; typically seconds to a minute) and
        /// returns the fitted-plus-forecast results. Disconnecting cancels the run.
        /// </summary>
        /// <param name="id">The analysis id.</param>
        /// <param name="cancellationToken">Cancellation token (client disconnect aborts the estimation).</param>
        /// <returns>The time-series results.</returns>
        /// <response code="200">The run completed; results returned.</response>
        /// <response code="400">The configuration failed validation; see validationErrors.</response>
        /// <response code="404">No time-series analysis has the id.</response>
        /// <response code="409">The analysis is already running.</response>
        /// <response code="499">The run was cancelled by the client.</response>
        /// <response code="500">The estimation failed; see errorMessage.</response>
        [HttpPost("{id:guid}/run")]
        [ProducesResponseType(typeof(TimeSeriesResultsResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(TimeSeriesResultsResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(TimeSeriesResultsResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(TimeSeriesResultsResponse), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(TimeSeriesResultsResponse), StatusCodes.Status500InternalServerError)]
        public Task<ActionResult<TimeSeriesResultsResponse>> Run(Guid id, CancellationToken cancellationToken)
        {
            return ExecuteAsync(() => _service.RunTimeSeriesAsync(id, cancellationToken),
                _logger, "analyses.timeseries.run");
        }

        /// <summary>
        /// Lists all time-series analyses (all four model families).
        /// </summary>
        /// <returns>Summaries of every time-series analysis.</returns>
        /// <response code="200">The listing.</response>
        [HttpGet]
        [ProducesResponseType(typeof(AnalysisListResponse), StatusCodes.Status200OK)]
        public Task<ActionResult<AnalysisListResponse>> List()
        {
            return ExecuteAsync(() => AnalysisMapper.ToListResponse(_service.List(AnalysisKind.TimeSeries)),
                _logger, "analyses.timeseries.list");
        }

        /// <summary>
        /// Returns the analysis configuration, validity, and run state.
        /// </summary>
        /// <param name="id">The analysis id.</param>
        /// <returns>The analysis summary.</returns>
        /// <response code="200">The analysis.</response>
        /// <response code="404">No time-series analysis has the id.</response>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(AnalysisResourceResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(AnalysisResourceResponse), StatusCodes.Status404NotFound)]
        public Task<ActionResult<AnalysisResourceResponse>> Get(Guid id)
        {
            return ExecuteAsync(() => AnalysisMapper.ToResourceResponse(_service.Get(id, AnalysisKind.TimeSeries)),
                _logger, "analyses.timeseries.get");
        }

        /// <summary>
        /// Returns the stored fitted-plus-forecast results of a previously completed run (no
        /// recomputation).
        /// </summary>
        /// <param name="id">The analysis id.</param>
        /// <returns>The time-series results.</returns>
        /// <response code="200">The results.</response>
        /// <response code="404">No time-series analysis has the id, or it has never been run.</response>
        [HttpGet("{id:guid}/results")]
        [ProducesResponseType(typeof(TimeSeriesResultsResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(TimeSeriesResultsResponse), StatusCodes.Status404NotFound)]
        public Task<ActionResult<TimeSeriesResultsResponse>> GetResults(Guid id)
        {
            return ExecuteAsync(() => _service.GetTimeSeriesResults(id),
                _logger, "analyses.timeseries.results");
        }

        /// <summary>
        /// Runs model-layer validation for the analysis without running it.
        /// </summary>
        /// <param name="id">The analysis id.</param>
        /// <returns>The validation verdict.</returns>
        /// <response code="200">The verdict (check isValid).</response>
        /// <response code="404">No time-series analysis has the id.</response>
        [HttpGet("{id:guid}/validate")]
        [ProducesResponseType(typeof(ValidationResponse), StatusCodes.Status200OK)]
        public ActionResult<ValidationResponse> Validate(Guid id)
        {
            try
            {
                return Ok(_service.Validate(id, AnalysisKind.TimeSeries));
            }
            catch (Services.Exceptions.ResourceNotFoundException ex)
            {
                return NotFound(new ValidationResponse { IsValid = false, Errors = new List<string> { ex.Message } });
            }
        }

        /// <summary>
        /// Deletes the analysis and its stored results.
        /// </summary>
        /// <param name="id">The analysis id.</param>
        /// <returns>Confirmation of the deletion.</returns>
        /// <response code="200">The analysis was deleted.</response>
        /// <response code="404">No time-series analysis has the id.</response>
        /// <response code="409">The analysis is currently running.</response>
        [HttpDelete("{id:guid}")]
        [ProducesResponseType(typeof(DeleteResourceResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(DeleteResourceResponse), StatusCodes.Status404NotFound)]
        public Task<ActionResult<DeleteResourceResponse>> Delete(Guid id)
        {
            return ExecuteAsync(() =>
            {
                _service.Delete(id, AnalysisKind.TimeSeries);
                return new DeleteResourceResponse { DeletedId = id, ResourceType = "analysis" };
            }, _logger, "analyses.timeseries.delete");
        }
    }
}
