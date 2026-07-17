using Microsoft.AspNetCore.Mvc;
using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Mappers;
using RMC.BestFit.Api.Services;
using RMC.BestFit.Api.Store;

namespace RMC.BestFit.Api.Controllers
{
    /// <summary>
    /// Endpoints for coincident frequency analyses: integrate a client-supplied response surface
    /// Z(x, y) over a fitted bivariate analysis to produce the response's annual exceedance
    /// frequency curve. The upstream bivariate (and transitively its marginals) are LIVE
    /// references: the bivariate must be run before this analysis runs, any of them mid-run
    /// blocks the run (409), and re-running them refreshes this analysis's next run.
    /// </summary>
    [Route("api/analyses/coincidentfrequency")]
    public class CoincidentFrequencyAnalysisController : ApiControllerBase
    {
        /// <summary>
        /// The controller's logger.
        /// </summary>
        private readonly ILogger<CoincidentFrequencyAnalysisController> _logger;

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
        public CoincidentFrequencyAnalysisController(ILogger<CoincidentFrequencyAnalysisController> logger, IAnalysisService service)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        /// <summary>
        /// Creates a coincident frequency analysis over an existing bivariate analysis plus a
        /// tabulated response surface (rows = xValues, columns = yValues, strictly increasing
        /// along both axes).
        /// </summary>
        /// <param name="request">The creation request.</param>
        /// <returns>The created analysis summary, including its id for run/results calls.</returns>
        /// <response code="201">The analysis was created.</response>
        /// <response code="400">The surface shape, ordinates, bin count, or options are invalid.</response>
        /// <response code="404">The bivariate analysis does not exist.</response>
        [HttpPost]
        [ProducesResponseType(typeof(AnalysisResourceResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(AnalysisResourceResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(AnalysisResourceResponse), StatusCodes.Status404NotFound)]
        public Task<ActionResult<AnalysisResourceResponse>> Create([FromBody] CreateCoincidentFrequencyAnalysisRequest request)
        {
            return ExecuteAsync(() => AnalysisMapper.ToResourceResponse(_service.CreateCoincidentFrequency(request)),
                _logger, "analyses.coincidentfrequency.create", StatusCodes.Status201Created);
        }

        /// <summary>
        /// Runs the analysis synchronously — no MCMC of its own; it integrates the surface over
        /// the upstream posteriors (typically seconds). The bivariate must be estimated first.
        /// </summary>
        /// <param name="id">The analysis id.</param>
        /// <param name="cancellationToken">Cancellation token (client disconnect aborts the aggregation).</param>
        /// <returns>The coincident frequency results.</returns>
        /// <response code="200">The run completed; results returned.</response>
        /// <response code="400">The configuration failed validation (e.g., the bivariate has not been run).</response>
        /// <response code="404">No coincident frequency analysis has the id.</response>
        /// <response code="409">The analysis, its bivariate, or a transitive marginal is currently running.</response>
        /// <response code="499">The run was cancelled by the client.</response>
        /// <response code="500">The aggregation failed; see errorMessage.</response>
        [HttpPost("{id:guid}/run")]
        [ProducesResponseType(typeof(CoincidentFrequencyResultsResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(CoincidentFrequencyResultsResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(CoincidentFrequencyResultsResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(CoincidentFrequencyResultsResponse), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(CoincidentFrequencyResultsResponse), StatusCodes.Status500InternalServerError)]
        public Task<ActionResult<CoincidentFrequencyResultsResponse>> Run(Guid id, CancellationToken cancellationToken)
        {
            return ExecuteAsync(() => _service.RunCoincidentFrequencyAsync(id, cancellationToken),
                _logger, "analyses.coincidentfrequency.run");
        }

        /// <summary>
        /// Lists all coincident frequency analyses.
        /// </summary>
        /// <returns>Summaries of every coincident frequency analysis.</returns>
        /// <response code="200">The listing.</response>
        [HttpGet]
        [ProducesResponseType(typeof(AnalysisListResponse), StatusCodes.Status200OK)]
        public Task<ActionResult<AnalysisListResponse>> List()
        {
            return ExecuteAsync(() => AnalysisMapper.ToListResponse(_service.List(AnalysisKind.CoincidentFrequency)),
                _logger, "analyses.coincidentfrequency.list");
        }

        /// <summary>
        /// Returns the analysis configuration, validity, and run state.
        /// </summary>
        /// <param name="id">The analysis id.</param>
        /// <returns>The analysis summary.</returns>
        /// <response code="200">The analysis.</response>
        /// <response code="404">No coincident frequency analysis has the id.</response>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(AnalysisResourceResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(AnalysisResourceResponse), StatusCodes.Status404NotFound)]
        public Task<ActionResult<AnalysisResourceResponse>> Get(Guid id)
        {
            return ExecuteAsync(() => AnalysisMapper.ToResourceResponse(_service.Get(id, AnalysisKind.CoincidentFrequency)),
                _logger, "analyses.coincidentfrequency.get");
        }

        /// <summary>
        /// Returns the stored results of a previously completed run (no recomputation).
        /// </summary>
        /// <param name="id">The analysis id.</param>
        /// <returns>The coincident frequency results.</returns>
        /// <response code="200">The results.</response>
        /// <response code="404">No coincident frequency analysis has the id, or it has never been run.</response>
        [HttpGet("{id:guid}/results")]
        [ProducesResponseType(typeof(CoincidentFrequencyResultsResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(CoincidentFrequencyResultsResponse), StatusCodes.Status404NotFound)]
        public Task<ActionResult<CoincidentFrequencyResultsResponse>> GetResults(Guid id)
        {
            return ExecuteAsync(() => _service.GetCoincidentFrequencyResults(id),
                _logger, "analyses.coincidentfrequency.results");
        }

        /// <summary>
        /// Runs model-layer validation for the analysis without running it (e.g., reports an
        /// unestimated bivariate or a non-monotonic response surface).
        /// </summary>
        /// <param name="id">The analysis id.</param>
        /// <returns>The validation verdict.</returns>
        /// <response code="200">The verdict (check isValid).</response>
        /// <response code="404">No coincident frequency analysis has the id.</response>
        [HttpGet("{id:guid}/validate")]
        [ProducesResponseType(typeof(ValidationResponse), StatusCodes.Status200OK)]
        public ActionResult<ValidationResponse> Validate(Guid id)
        {
            try
            {
                return Ok(_service.Validate(id, AnalysisKind.CoincidentFrequency));
            }
            catch (Services.Exceptions.ResourceNotFoundException ex)
            {
                return NotFound(new ValidationResponse { IsValid = false, Errors = new List<string> { ex.Message } });
            }
        }

        /// <summary>
        /// Deletes the analysis and its stored results. The upstream bivariate is not affected.
        /// </summary>
        /// <param name="id">The analysis id.</param>
        /// <returns>Confirmation of the deletion.</returns>
        /// <response code="200">The analysis was deleted.</response>
        /// <response code="404">No coincident frequency analysis has the id.</response>
        /// <response code="409">The analysis is currently running.</response>
        [HttpDelete("{id:guid}")]
        [ProducesResponseType(typeof(DeleteResourceResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(DeleteResourceResponse), StatusCodes.Status404NotFound)]
        public Task<ActionResult<DeleteResourceResponse>> Delete(Guid id)
        {
            return ExecuteAsync(() =>
            {
                _service.Delete(id, AnalysisKind.CoincidentFrequency);
                return new DeleteResourceResponse { DeletedId = id, ResourceType = "analysis" };
            }, _logger, "analyses.coincidentfrequency.delete");
        }
    }
}
