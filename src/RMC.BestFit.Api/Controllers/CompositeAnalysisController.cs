using Microsoft.AspNetCore.Mvc;
using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Mappers;
using RMC.BestFit.Api.Services;
using RMC.BestFit.Api.Store;

namespace RMC.BestFit.Api.Controllers
{
    /// <summary>
    /// Endpoints for composite analyses that combine already-fitted component analyses by
    /// competing risks, mixture weighting, or information-criterion model averaging. Components
    /// are LIVE references: every component must have been run before the composite runs, a
    /// component mid-run blocks the composite (409), and re-running a component refreshes the
    /// composite's next run.
    /// </summary>
    [Route("api/analyses/composite")]
    public class CompositeAnalysisController : ApiControllerBase
    {
        /// <summary>
        /// The controller's logger.
        /// </summary>
        private readonly ILogger<CompositeAnalysisController> _logger;

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
        public CompositeAnalysisController(ILogger<CompositeAnalysisController> logger, IAnalysisService service)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        /// <summary>
        /// Creates a composite analysis over existing component analyses (univariate,
        /// bulletin17c, mixture, pointprocess, or competingrisks kinds; composites cannot nest).
        /// Components do not need to be run yet — they must be run before the composite runs.
        /// </summary>
        /// <param name="request">The creation request.</param>
        /// <returns>The created analysis summary, including its id for run/results calls.</returns>
        /// <response code="201">The analysis was created.</response>
        /// <response code="400">A component kind, mixture weight, ordinate, or option is invalid.</response>
        /// <response code="404">A component analysis does not exist.</response>
        [HttpPost]
        [ProducesResponseType(typeof(AnalysisResourceResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(AnalysisResourceResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(AnalysisResourceResponse), StatusCodes.Status404NotFound)]
        public Task<ActionResult<AnalysisResourceResponse>> Create([FromBody] CreateCompositeAnalysisRequest request)
        {
            return ExecuteAsync(() => AnalysisMapper.ToResourceResponse(_service.CreateComposite(request)),
                _logger, "analyses.composite.create", StatusCodes.Status201Created);
        }

        /// <summary>
        /// Runs the composite synchronously — no MCMC of its own; it aggregates the component
        /// posteriors (typically a few seconds). Every component must be estimated first.
        /// </summary>
        /// <param name="id">The analysis id.</param>
        /// <param name="cancellationToken">Cancellation token (client disconnect aborts the aggregation).</param>
        /// <returns>The composite frequency results.</returns>
        /// <response code="200">The run completed; results returned.</response>
        /// <response code="400">The configuration failed validation (e.g., a component has not been run).</response>
        /// <response code="404">No composite analysis has the id.</response>
        /// <response code="409">The composite or one of its component analyses is currently running.</response>
        /// <response code="499">The run was cancelled by the client.</response>
        /// <response code="500">The aggregation failed; see errorMessage.</response>
        [HttpPost("{id:guid}/run")]
        [ProducesResponseType(typeof(FrequencyResultsResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(FrequencyResultsResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(FrequencyResultsResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(FrequencyResultsResponse), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(FrequencyResultsResponse), StatusCodes.Status500InternalServerError)]
        public Task<ActionResult<FrequencyResultsResponse>> Run(Guid id, CancellationToken cancellationToken)
        {
            return ExecuteAsync(() => _service.RunFrequencyAsync(id, AnalysisKind.Composite, cancellationToken),
                _logger, "analyses.composite.run");
        }

        /// <summary>
        /// Lists all composite analyses.
        /// </summary>
        /// <returns>Summaries of every composite analysis.</returns>
        /// <response code="200">The listing.</response>
        [HttpGet]
        [ProducesResponseType(typeof(AnalysisListResponse), StatusCodes.Status200OK)]
        public Task<ActionResult<AnalysisListResponse>> List()
        {
            return ExecuteAsync(() => AnalysisMapper.ToListResponse(_service.List(AnalysisKind.Composite)),
                _logger, "analyses.composite.list");
        }

        /// <summary>
        /// Returns the analysis configuration, validity, and run state.
        /// </summary>
        /// <param name="id">The analysis id.</param>
        /// <returns>The analysis summary.</returns>
        /// <response code="200">The analysis.</response>
        /// <response code="404">No composite analysis has the id.</response>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(AnalysisResourceResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(AnalysisResourceResponse), StatusCodes.Status404NotFound)]
        public Task<ActionResult<AnalysisResourceResponse>> Get(Guid id)
        {
            return ExecuteAsync(() => AnalysisMapper.ToResourceResponse(_service.Get(id, AnalysisKind.Composite)),
                _logger, "analyses.composite.get");
        }

        /// <summary>
        /// Returns the stored composite results of a previously completed run (no recomputation).
        /// </summary>
        /// <param name="id">The analysis id.</param>
        /// <returns>The composite frequency results.</returns>
        /// <response code="200">The results.</response>
        /// <response code="404">No composite analysis has the id, or it has never been run.</response>
        [HttpGet("{id:guid}/results")]
        [ProducesResponseType(typeof(FrequencyResultsResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(FrequencyResultsResponse), StatusCodes.Status404NotFound)]
        public Task<ActionResult<FrequencyResultsResponse>> GetResults(Guid id)
        {
            return ExecuteAsync(() => _service.GetFrequencyResults(id, AnalysisKind.Composite),
                _logger, "analyses.composite.results");
        }

        /// <summary>
        /// Runs model-layer validation for the analysis without running it (e.g., reports
        /// components that still require estimation).
        /// </summary>
        /// <param name="id">The analysis id.</param>
        /// <returns>The validation verdict.</returns>
        /// <response code="200">The verdict (check isValid).</response>
        /// <response code="404">No composite analysis has the id.</response>
        [HttpGet("{id:guid}/validate")]
        [ProducesResponseType(typeof(ValidationResponse), StatusCodes.Status200OK)]
        public ActionResult<ValidationResponse> Validate(Guid id)
        {
            try
            {
                return Ok(_service.Validate(id, AnalysisKind.Composite));
            }
            catch (Services.Exceptions.ResourceNotFoundException ex)
            {
                return NotFound(new ValidationResponse { IsValid = false, Errors = new List<string> { ex.Message } });
            }
        }

        /// <summary>
        /// Deletes the composite and its stored results. Component analyses are not affected.
        /// </summary>
        /// <param name="id">The analysis id.</param>
        /// <returns>Confirmation of the deletion.</returns>
        /// <response code="200">The analysis was deleted.</response>
        /// <response code="404">No composite analysis has the id.</response>
        /// <response code="409">The analysis is currently running.</response>
        [HttpDelete("{id:guid}")]
        [ProducesResponseType(typeof(DeleteResourceResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(DeleteResourceResponse), StatusCodes.Status404NotFound)]
        public Task<ActionResult<DeleteResourceResponse>> Delete(Guid id)
        {
            return ExecuteAsync(() =>
            {
                _service.Delete(id, AnalysisKind.Composite);
                return new DeleteResourceResponse { DeletedId = id, ResourceType = "analysis" };
            }, _logger, "analyses.composite.delete");
        }
    }
}
