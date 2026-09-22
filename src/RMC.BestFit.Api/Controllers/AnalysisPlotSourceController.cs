using Microsoft.AspNetCore.Mvc;
using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Services;

namespace RMC.BestFit.Api.Controllers
{
    /// <summary>Read-only plot source for any completed analysis kind.</summary>
    [Route("api/analyses")]
    public sealed class AnalysisPlotSourceController : ApiControllerBase
    {
        private readonly ILogger<AnalysisPlotSourceController> _logger;
        private readonly IAnalysisService _service;

        /// <summary>Creates the plot source endpoint.</summary>
        public AnalysisPlotSourceController(ILogger<AnalysisPlotSourceController> logger, IAnalysisService service)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        /// <summary>Returns a detached snapshot from a completed run without invoking estimation.</summary>
        /// <param name="analysisId">The analysis resource id.</param>
        /// <param name="includeSamples">Include full saved draws and chains. Defaults to false.</param>
        /// <returns>Versioned plot source data and results from one run.</returns>
        [HttpGet("{analysisId:guid}/plot-source")]
        [ProducesResponseType(typeof(PlotSourceResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(PlotSourceResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(PlotSourceResponse), StatusCodes.Status409Conflict)]
        public Task<ActionResult<PlotSourceResponse>> Get(Guid analysisId, [FromQuery] bool includeSamples = false)
        {
            return ExecuteAsync(() => PlotSourceExporter.Export(_service.Get(analysisId), includeSamples),
                _logger, "analyses.plot-source.get");
        }
    }
}
