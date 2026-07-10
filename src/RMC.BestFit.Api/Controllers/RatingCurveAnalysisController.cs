using Microsoft.AspNetCore.Mvc;
using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Mappers;
using RMC.BestFit.Api.Services;
using RMC.BestFit.Api.Store;

namespace RMC.BestFit.Api.Controllers
{
    /// <summary>
    /// Endpoints for Bayesian stage-discharge rating curve analyses: create against stage and
    /// discharge measurement time series, run synchronously, and retrieve the fitted piecewise
    /// power-law curve with credible intervals over a stage grid.
    /// </summary>
    [Route("api/analyses/ratingcurve")]
    public class RatingCurveAnalysisController : ApiControllerBase
    {
        /// <summary>
        /// The controller's logger.
        /// </summary>
        private readonly ILogger<RatingCurveAnalysisController> _logger;

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
        public RatingCurveAnalysisController(ILogger<RatingCurveAnalysisController> logger, IAnalysisService service)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        /// <summary>
        /// Creates a rating curve analysis linked to a stage and a discharge time-series resource
        /// (typically USGS measuredStage and measuredDischarge for the same site). The two series
        /// are date-aligned; at least 10 common dates are required. Series are cloned at creation.
        /// </summary>
        /// <param name="request">The creation request.</param>
        /// <returns>The created analysis summary.</returns>
        /// <response code="201">The analysis was created.</response>
        /// <response code="400">The stage-grid options or Bayesian options are invalid.</response>
        /// <response code="404">A referenced time-series resource does not exist.</response>
        [HttpPost]
        [ProducesResponseType(typeof(AnalysisResourceResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(AnalysisResourceResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(AnalysisResourceResponse), StatusCodes.Status404NotFound)]
        public Task<ActionResult<AnalysisResourceResponse>> Create([FromBody] CreateRatingCurveAnalysisRequest request)
        {
            return ExecuteAsync(() => AnalysisMapper.ToResourceResponse(_service.CreateRatingCurve(request)),
                _logger, "analyses.ratingcurve.create", StatusCodes.Status201Created);
        }

        /// <summary>
        /// Runs the analysis synchronously (Bayesian MCMC; typically seconds to a minute) and
        /// returns the fitted rating curve with uncertainty.
        /// </summary>
        /// <param name="id">The analysis id.</param>
        /// <param name="cancellationToken">Cancellation token (client disconnect aborts the estimation).</param>
        /// <returns>The rating curve results.</returns>
        /// <response code="200">The run completed; results returned.</response>
        /// <response code="400">The configuration failed validation (e.g., fewer than 10 aligned observations).</response>
        /// <response code="404">No rating curve analysis has the id.</response>
        /// <response code="409">The analysis is already running.</response>
        /// <response code="499">The run was cancelled by the client.</response>
        /// <response code="500">The estimation failed; see errorMessage.</response>
        [HttpPost("{id:guid}/run")]
        [ProducesResponseType(typeof(RatingCurveResultsResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(RatingCurveResultsResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(RatingCurveResultsResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(RatingCurveResultsResponse), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(RatingCurveResultsResponse), StatusCodes.Status500InternalServerError)]
        public Task<ActionResult<RatingCurveResultsResponse>> Run(Guid id, CancellationToken cancellationToken)
        {
            return ExecuteAsync(() => _service.RunRatingCurveAsync(id, cancellationToken),
                _logger, "analyses.ratingcurve.run");
        }

        /// <summary>
        /// Lists all rating curve analyses.
        /// </summary>
        /// <returns>Summaries of every rating curve analysis.</returns>
        /// <response code="200">The listing.</response>
        [HttpGet]
        [ProducesResponseType(typeof(AnalysisListResponse), StatusCodes.Status200OK)]
        public Task<ActionResult<AnalysisListResponse>> List()
        {
            return ExecuteAsync(() => AnalysisMapper.ToListResponse(_service.List(AnalysisKind.RatingCurve)),
                _logger, "analyses.ratingcurve.list");
        }

        /// <summary>
        /// Returns the analysis configuration, validity, and run state.
        /// </summary>
        /// <param name="id">The analysis id.</param>
        /// <returns>The analysis summary.</returns>
        /// <response code="200">The analysis.</response>
        /// <response code="404">No rating curve analysis has the id.</response>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(AnalysisResourceResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(AnalysisResourceResponse), StatusCodes.Status404NotFound)]
        public Task<ActionResult<AnalysisResourceResponse>> Get(Guid id)
        {
            return ExecuteAsync(() => AnalysisMapper.ToResourceResponse(_service.Get(id, AnalysisKind.RatingCurve)),
                _logger, "analyses.ratingcurve.get");
        }

        /// <summary>
        /// Returns the stored results of a previously completed run (no recomputation).
        /// </summary>
        /// <param name="id">The analysis id.</param>
        /// <returns>The rating curve results.</returns>
        /// <response code="200">The results.</response>
        /// <response code="404">No rating curve analysis has the id, or it has never been run.</response>
        [HttpGet("{id:guid}/results")]
        [ProducesResponseType(typeof(RatingCurveResultsResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(RatingCurveResultsResponse), StatusCodes.Status404NotFound)]
        public Task<ActionResult<RatingCurveResultsResponse>> GetResults(Guid id)
        {
            return ExecuteAsync(() => _service.GetRatingCurveResults(id),
                _logger, "analyses.ratingcurve.results");
        }

        /// <summary>
        /// Runs model-layer validation for the analysis without running it (e.g., checks the
        /// minimum of 10 date-aligned observation pairs).
        /// </summary>
        /// <param name="id">The analysis id.</param>
        /// <returns>The validation verdict.</returns>
        /// <response code="200">The verdict (check isValid).</response>
        /// <response code="404">No rating curve analysis has the id.</response>
        [HttpGet("{id:guid}/validate")]
        [ProducesResponseType(typeof(ValidationResponse), StatusCodes.Status200OK)]
        public ActionResult<ValidationResponse> Validate(Guid id)
        {
            try
            {
                return Ok(_service.Validate(id, AnalysisKind.RatingCurve));
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
        /// <response code="404">No rating curve analysis has the id.</response>
        /// <response code="409">The analysis is currently running.</response>
        [HttpDelete("{id:guid}")]
        [ProducesResponseType(typeof(DeleteResourceResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(DeleteResourceResponse), StatusCodes.Status404NotFound)]
        public Task<ActionResult<DeleteResourceResponse>> Delete(Guid id)
        {
            return ExecuteAsync(() =>
            {
                _service.Delete(id, AnalysisKind.RatingCurve);
                return new DeleteResourceResponse { DeletedId = id, ResourceType = "analysis" };
            }, _logger, "analyses.ratingcurve.delete");
        }
    }
}
