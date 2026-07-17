using Microsoft.AspNetCore.Mvc;
using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Mappers;
using RMC.BestFit.Api.Services;
using RMC.BestFit.Api.Store;

namespace RMC.BestFit.Api.Controllers
{
    /// <summary>
    /// Endpoints for Bayesian MCMC mixture-distribution frequency analyses: create against an
    /// input-data resource with 1-3 component distributions, run synchronously, and retrieve
    /// frequency curves with full posterior uncertainty.
    /// </summary>
    [Route("api/analyses/mixture")]
    public class MixtureAnalysisController : ApiControllerBase
    {
        /// <summary>
        /// The controller's logger.
        /// </summary>
        private readonly ILogger<MixtureAnalysisController> _logger;

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
        public MixtureAnalysisController(ILogger<MixtureAnalysisController> logger, IAnalysisService service)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        /// <summary>
        /// Creates a mixture-distribution frequency analysis linked to an input-data resource.
        /// The analysis clones the input data, so later edits or deletes of the input-data
        /// resource cannot affect it.
        /// </summary>
        /// <param name="request">The creation request.</param>
        /// <returns>The created analysis summary, including its id for run/results calls.</returns>
        /// <response code="201">The analysis was created.</response>
        /// <response code="400">The component distributions, ordinates, priors, or Bayesian options are invalid.</response>
        /// <response code="404">The input-data resource does not exist.</response>
        [HttpPost]
        [ProducesResponseType(typeof(AnalysisResourceResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(AnalysisResourceResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(AnalysisResourceResponse), StatusCodes.Status404NotFound)]
        public Task<ActionResult<AnalysisResourceResponse>> Create([FromBody] CreateMixtureAnalysisRequest request)
        {
            return ExecuteAsync(() => AnalysisMapper.ToResourceResponse(_service.CreateMixture(request)),
                _logger, "analyses.mixture.create", StatusCodes.Status201Created);
        }

        /// <summary>
        /// Runs the analysis synchronously (Bayesian MCMC with expectation-maximization
        /// initialization; typically seconds to a minute) and returns the frequency results.
        /// Disconnecting cancels the run.
        /// </summary>
        /// <param name="id">The analysis id.</param>
        /// <param name="cancellationToken">Cancellation token (client disconnect aborts the estimation).</param>
        /// <returns>The frequency results.</returns>
        /// <response code="200">The run completed; results returned.</response>
        /// <response code="400">The configuration failed validation; see validationErrors.</response>
        /// <response code="404">No mixture analysis has the id.</response>
        /// <response code="409">The analysis is already running.</response>
        /// <response code="499">The run was cancelled by the client.</response>
        /// <response code="500">The estimation failed; see errorMessage.</response>
        [HttpPost("{id:guid}/run")]
        [ProducesResponseType(typeof(FrequencyResultsResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(FrequencyResultsResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(FrequencyResultsResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(FrequencyResultsResponse), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(FrequencyResultsResponse), StatusCodes.Status500InternalServerError)]
        public Task<ActionResult<FrequencyResultsResponse>> Run(Guid id, CancellationToken cancellationToken)
        {
            return ExecuteAsync(() => _service.RunFrequencyAsync(id, AnalysisKind.Mixture, cancellationToken),
                _logger, "analyses.mixture.run");
        }

        /// <summary>
        /// Lists all mixture analyses.
        /// </summary>
        /// <returns>Summaries of every mixture analysis.</returns>
        /// <response code="200">The listing.</response>
        [HttpGet]
        [ProducesResponseType(typeof(AnalysisListResponse), StatusCodes.Status200OK)]
        public Task<ActionResult<AnalysisListResponse>> List()
        {
            return ExecuteAsync(() => AnalysisMapper.ToListResponse(_service.List(AnalysisKind.Mixture)),
                _logger, "analyses.mixture.list");
        }

        /// <summary>
        /// Returns the analysis configuration, validity, and run state.
        /// </summary>
        /// <param name="id">The analysis id.</param>
        /// <returns>The analysis summary.</returns>
        /// <response code="200">The analysis.</response>
        /// <response code="404">No mixture analysis has the id.</response>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(AnalysisResourceResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(AnalysisResourceResponse), StatusCodes.Status404NotFound)]
        public Task<ActionResult<AnalysisResourceResponse>> Get(Guid id)
        {
            return ExecuteAsync(() => AnalysisMapper.ToResourceResponse(_service.Get(id, AnalysisKind.Mixture)),
                _logger, "analyses.mixture.get");
        }

        /// <summary>
        /// Returns the stored frequency results of a previously completed run (no recomputation).
        /// </summary>
        /// <param name="id">The analysis id.</param>
        /// <returns>The frequency results.</returns>
        /// <response code="200">The results.</response>
        /// <response code="404">No mixture analysis has the id, or it has never been run.</response>
        [HttpGet("{id:guid}/results")]
        [ProducesResponseType(typeof(FrequencyResultsResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(FrequencyResultsResponse), StatusCodes.Status404NotFound)]
        public Task<ActionResult<FrequencyResultsResponse>> GetResults(Guid id)
        {
            return ExecuteAsync(() => _service.GetFrequencyResults(id, AnalysisKind.Mixture),
                _logger, "analyses.mixture.results");
        }

        /// <summary>
        /// Runs model-layer validation for the analysis without running it.
        /// </summary>
        /// <param name="id">The analysis id.</param>
        /// <returns>The validation verdict.</returns>
        /// <response code="200">The verdict (check isValid).</response>
        /// <response code="404">No mixture analysis has the id.</response>
        [HttpGet("{id:guid}/validate")]
        [ProducesResponseType(typeof(ValidationResponse), StatusCodes.Status200OK)]
        public ActionResult<ValidationResponse> Validate(Guid id)
        {
            try
            {
                return Ok(_service.Validate(id, AnalysisKind.Mixture));
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
        /// <response code="404">No mixture analysis has the id.</response>
        /// <response code="409">The analysis is currently running.</response>
        [HttpDelete("{id:guid}")]
        [ProducesResponseType(typeof(DeleteResourceResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(DeleteResourceResponse), StatusCodes.Status404NotFound)]
        public Task<ActionResult<DeleteResourceResponse>> Delete(Guid id)
        {
            return ExecuteAsync(() =>
            {
                _service.Delete(id, AnalysisKind.Mixture);
                return new DeleteResourceResponse { DeletedId = id, ResourceType = "analysis" };
            }, _logger, "analyses.mixture.delete");
        }
    }
}
