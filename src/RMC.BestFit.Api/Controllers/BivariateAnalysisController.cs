using Microsoft.AspNetCore.Mvc;
using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Mappers;
using RMC.BestFit.Api.Services;
using RMC.BestFit.Api.Store;

namespace RMC.BestFit.Api.Controllers
{
    /// <summary>
    /// Endpoints for Bayesian MCMC bivariate copula analyses over two fitted marginal analyses:
    /// create, run synchronously, and retrieve joint-exceedance curves with full posterior
    /// uncertainty. Marginals are LIVE references: they must be run before this analysis runs, a
    /// marginal mid-run blocks the run (409), and re-running a marginal refreshes this
    /// analysis's next run.
    /// </summary>
    [Route("api/analyses/bivariate")]
    public class BivariateAnalysisController : ApiControllerBase
    {
        /// <summary>
        /// The controller's logger.
        /// </summary>
        private readonly ILogger<BivariateAnalysisController> _logger;

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
        public BivariateAnalysisController(ILogger<BivariateAnalysisController> logger, IAnalysisService service)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        /// <summary>
        /// Creates a bivariate copula analysis over two existing marginal analyses (kinds
        /// univariate, bulletin17c, mixture, or pointprocess). The marginals' data are paired by
        /// shared time index; at least 10 overlapping non-outlier exact observations are required.
        /// </summary>
        /// <param name="request">The creation request.</param>
        /// <returns>The created analysis summary, including its id for run/results calls.</returns>
        /// <response code="201">The analysis was created.</response>
        /// <response code="400">A marginal kind, the data overlap, the copula options, or the XY grid is invalid.</response>
        /// <response code="404">A marginal analysis does not exist.</response>
        [HttpPost]
        [ProducesResponseType(typeof(AnalysisResourceResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(AnalysisResourceResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(AnalysisResourceResponse), StatusCodes.Status404NotFound)]
        public Task<ActionResult<AnalysisResourceResponse>> Create([FromBody] CreateBivariateAnalysisRequest request)
        {
            return ExecuteAsync(() => AnalysisMapper.ToResourceResponse(_service.CreateBivariate(request)),
                _logger, "analyses.bivariate.create", StatusCodes.Status201Created);
        }

        /// <summary>
        /// Runs the copula estimation synchronously (Bayesian MCMC; typically seconds to a
        /// minute) and returns the joint-exceedance results. Both marginals must be estimated
        /// first. Disconnecting cancels the run.
        /// </summary>
        /// <param name="id">The analysis id.</param>
        /// <param name="cancellationToken">Cancellation token (client disconnect aborts the estimation).</param>
        /// <returns>The bivariate results.</returns>
        /// <response code="200">The run completed; results returned.</response>
        /// <response code="400">The configuration failed validation (e.g., a marginal has not been run).</response>
        /// <response code="404">No bivariate analysis has the id.</response>
        /// <response code="409">The analysis or one of its marginal analyses is currently running.</response>
        /// <response code="499">The run was cancelled by the client.</response>
        /// <response code="500">The estimation failed; see errorMessage.</response>
        [HttpPost("{id:guid}/run")]
        [ProducesResponseType(typeof(BivariateResultsResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(BivariateResultsResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(BivariateResultsResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(BivariateResultsResponse), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(BivariateResultsResponse), StatusCodes.Status500InternalServerError)]
        public Task<ActionResult<BivariateResultsResponse>> Run(Guid id, CancellationToken cancellationToken)
        {
            return ExecuteAsync(() => _service.RunBivariateAsync(id, cancellationToken),
                _logger, "analyses.bivariate.run");
        }

        /// <summary>
        /// Lists all bivariate analyses.
        /// </summary>
        /// <returns>Summaries of every bivariate analysis.</returns>
        /// <response code="200">The listing.</response>
        [HttpGet]
        [ProducesResponseType(typeof(AnalysisListResponse), StatusCodes.Status200OK)]
        public Task<ActionResult<AnalysisListResponse>> List()
        {
            return ExecuteAsync(() => AnalysisMapper.ToListResponse(_service.List(AnalysisKind.Bivariate)),
                _logger, "analyses.bivariate.list");
        }

        /// <summary>
        /// Returns the analysis configuration, validity, and run state.
        /// </summary>
        /// <param name="id">The analysis id.</param>
        /// <returns>The analysis summary.</returns>
        /// <response code="200">The analysis.</response>
        /// <response code="404">No bivariate analysis has the id.</response>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(AnalysisResourceResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(AnalysisResourceResponse), StatusCodes.Status404NotFound)]
        public Task<ActionResult<AnalysisResourceResponse>> Get(Guid id)
        {
            return ExecuteAsync(() => AnalysisMapper.ToResourceResponse(_service.Get(id, AnalysisKind.Bivariate)),
                _logger, "analyses.bivariate.get");
        }

        /// <summary>
        /// Returns the stored joint-exceedance results of a previously completed run (no recomputation).
        /// </summary>
        /// <param name="id">The analysis id.</param>
        /// <returns>The bivariate results.</returns>
        /// <response code="200">The results.</response>
        /// <response code="404">No bivariate analysis has the id, or it has never been run.</response>
        [HttpGet("{id:guid}/results")]
        [ProducesResponseType(typeof(BivariateResultsResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(BivariateResultsResponse), StatusCodes.Status404NotFound)]
        public Task<ActionResult<BivariateResultsResponse>> GetResults(Guid id)
        {
            return ExecuteAsync(() => _service.GetBivariateResults(id),
                _logger, "analyses.bivariate.results");
        }

        /// <summary>
        /// Runs model-layer validation for the analysis without running it (e.g., reports
        /// marginals that still require estimation).
        /// </summary>
        /// <param name="id">The analysis id.</param>
        /// <returns>The validation verdict.</returns>
        /// <response code="200">The verdict (check isValid).</response>
        /// <response code="404">No bivariate analysis has the id.</response>
        [HttpGet("{id:guid}/validate")]
        [ProducesResponseType(typeof(ValidationResponse), StatusCodes.Status200OK)]
        public ActionResult<ValidationResponse> Validate(Guid id)
        {
            try
            {
                return Ok(_service.Validate(id, AnalysisKind.Bivariate));
            }
            catch (Services.Exceptions.ResourceNotFoundException ex)
            {
                return NotFound(new ValidationResponse { IsValid = false, Errors = new List<string> { ex.Message } });
            }
        }

        /// <summary>
        /// Deletes the analysis and its stored results. Marginal analyses are not affected.
        /// </summary>
        /// <param name="id">The analysis id.</param>
        /// <returns>Confirmation of the deletion.</returns>
        /// <response code="200">The analysis was deleted.</response>
        /// <response code="404">No bivariate analysis has the id.</response>
        /// <response code="409">The analysis is currently running.</response>
        [HttpDelete("{id:guid}")]
        [ProducesResponseType(typeof(DeleteResourceResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(DeleteResourceResponse), StatusCodes.Status404NotFound)]
        public Task<ActionResult<DeleteResourceResponse>> Delete(Guid id)
        {
            return ExecuteAsync(() =>
            {
                _service.Delete(id, AnalysisKind.Bivariate);
                return new DeleteResourceResponse { DeletedId = id, ResourceType = "analysis" };
            }, _logger, "analyses.bivariate.delete");
        }
    }
}
