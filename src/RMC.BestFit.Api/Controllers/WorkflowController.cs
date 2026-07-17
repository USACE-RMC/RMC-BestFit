using Microsoft.AspNetCore.Mvc;
using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Services;

namespace RMC.BestFit.Api.Controllers
{
    /// <summary>
    /// One-shot USGS workflow endpoints: each call downloads the data, builds the intermediate
    /// resources, runs the analysis synchronously, and returns the results together with every
    /// created resource id. Step failures are reported in-body (success=false, failedStep) so the
    /// ids of already-created resources are preserved for manual resumption.
    /// </summary>
    [Route("api/workflows")]
    public class WorkflowController : ApiControllerBase
    {
        /// <summary>
        /// The controller's logger.
        /// </summary>
        private readonly ILogger<WorkflowController> _logger;

        /// <summary>
        /// The workflow service composing the resource services.
        /// </summary>
        private readonly IWorkflowService _service;

        /// <summary>
        /// Constructs the controller.
        /// </summary>
        /// <param name="logger">The controller's logger.</param>
        /// <param name="service">The workflow service.</param>
        /// <exception cref="ArgumentNullException">Thrown when a dependency is null.</exception>
        public WorkflowController(ILogger<WorkflowController> logger, IWorkflowService service)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        /// <summary>
        /// Downloads USGS annual peaks, builds input data, runs a univariate Bayesian frequency
        /// analysis, and returns the frequency curve — all in one call.
        /// </summary>
        /// <param name="request">The workflow request.</param>
        /// <param name="cancellationToken">Cancellation token (client disconnect aborts the run).</param>
        /// <returns>The workflow response with resource ids and results.</returns>
        /// <response code="200">The workflow finished; check success/failedStep for step failures.</response>
        /// <response code="499">The workflow was cancelled by the client.</response>
        [HttpPost("usgs-peak-frequency")]
        [ProducesResponseType(typeof(UsgsFrequencyWorkflowResponse), StatusCodes.Status200OK)]
        public Task<ActionResult<UsgsFrequencyWorkflowResponse>> RunUsgsPeakFrequency(
            [FromBody] UsgsPeakFrequencyWorkflowRequest request,
            CancellationToken cancellationToken)
        {
            return ExecuteAsync(() => _service.RunUsgsPeakFrequencyAsync(request, cancellationToken),
                _logger, "workflows.usgspeakfrequency");
        }

        /// <summary>
        /// Downloads USGS daily flow, extracts block maxima (water-year annual maxima by default),
        /// runs a univariate Bayesian frequency analysis, and returns the frequency curve.
        /// </summary>
        /// <param name="request">The workflow request.</param>
        /// <param name="cancellationToken">Cancellation token (client disconnect aborts the run).</param>
        /// <returns>The workflow response with resource ids and results.</returns>
        /// <response code="200">The workflow finished; check success/failedStep for step failures.</response>
        /// <response code="499">The workflow was cancelled by the client.</response>
        [HttpPost("usgs-daily-block-max-frequency")]
        [ProducesResponseType(typeof(UsgsFrequencyWorkflowResponse), StatusCodes.Status200OK)]
        public Task<ActionResult<UsgsFrequencyWorkflowResponse>> RunUsgsBlockMaxFrequency(
            [FromBody] UsgsBlockMaxFrequencyWorkflowRequest request,
            CancellationToken cancellationToken)
        {
            return ExecuteAsync(() => _service.RunUsgsBlockMaxFrequencyAsync(request, cancellationToken),
                _logger, "workflows.usgsblockmaxfrequency");
        }

        /// <summary>
        /// Downloads USGS annual peaks, builds input data, runs a Bulletin 17C analysis, and
        /// returns the frequency curve with confidence intervals.
        /// </summary>
        /// <param name="request">The workflow request.</param>
        /// <param name="cancellationToken">Cancellation token (client disconnect aborts the run).</param>
        /// <returns>The workflow response with resource ids and results.</returns>
        /// <response code="200">The workflow finished; check success/failedStep for step failures.</response>
        /// <response code="499">The workflow was cancelled by the client.</response>
        [HttpPost("usgs-bulletin17c")]
        [ProducesResponseType(typeof(UsgsFrequencyWorkflowResponse), StatusCodes.Status200OK)]
        public Task<ActionResult<UsgsFrequencyWorkflowResponse>> RunUsgsBulletin17C(
            [FromBody] UsgsBulletin17CWorkflowRequest request,
            CancellationToken cancellationToken)
        {
            return ExecuteAsync(() => _service.RunUsgsBulletin17CAsync(request, cancellationToken),
                _logger, "workflows.usgsbulletin17c");
        }

        /// <summary>
        /// Downloads the discrete USGS field measurements of stage and discharge for a site, runs
        /// a Bayesian rating curve analysis over the date-aligned pairs, and returns the fitted
        /// curve with credible intervals.
        /// </summary>
        /// <param name="request">The workflow request.</param>
        /// <param name="cancellationToken">Cancellation token (client disconnect aborts the run).</param>
        /// <returns>The workflow response with resource ids and results.</returns>
        /// <response code="200">The workflow finished; check success/failedStep for step failures.</response>
        /// <response code="499">The workflow was cancelled by the client.</response>
        [HttpPost("usgs-rating-curve")]
        [ProducesResponseType(typeof(UsgsRatingCurveWorkflowResponse), StatusCodes.Status200OK)]
        public Task<ActionResult<UsgsRatingCurveWorkflowResponse>> RunUsgsRatingCurve(
            [FromBody] UsgsRatingCurveWorkflowRequest request,
            CancellationToken cancellationToken)
        {
            return ExecuteAsync(() => _service.RunUsgsRatingCurveAsync(request, cancellationToken),
                _logger, "workflows.usgsratingcurve");
        }
    }
}
