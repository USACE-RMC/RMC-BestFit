using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Numerics.Data;
using RMC.BestFit.Api.Configuration;
using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Mappers;

namespace RMC.BestFit.Api.Controllers
{
    /// <summary>
    /// Discovery endpoints: supported distributions, accepted enum values, and server defaults.
    /// MCP agents should consult these before constructing analysis requests.
    /// </summary>
    [Route("api/metadata")]
    public class MetadataController : ApiControllerBase
    {
        /// <summary>
        /// The controller's logger.
        /// </summary>
        private readonly ILogger<MetadataController> _logger;

        /// <summary>
        /// The configured API limits reported by the defaults endpoint.
        /// </summary>
        private readonly ApiOptions _options;

        /// <summary>
        /// Constructs the controller.
        /// </summary>
        /// <param name="logger">The controller's logger.</param>
        /// <param name="options">The configured API limits.</param>
        /// <exception cref="ArgumentNullException">Thrown when a dependency is null.</exception>
        public MetadataController(ILogger<MetadataController> logger, IOptions<ApiOptions> options)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            if (options is null) throw new ArgumentNullException(nameof(options));
            _options = options.Value;
        }

        /// <summary>
        /// Lists the probability distributions available for fitting, with the analysis kinds that
        /// support each ("univariate" for Bayesian MCMC, "bulletin17c" for the Bulletin 17C procedure).
        /// </summary>
        /// <returns>The distribution listing.</returns>
        /// <response code="200">The listing.</response>
        [HttpGet("distributions")]
        [ProducesResponseType(typeof(DistributionsResponse), StatusCodes.Status200OK)]
        public Task<ActionResult<DistributionsResponse>> GetDistributions()
        {
            return ExecuteAsync(MetadataMapper.ToDistributionsResponse, _logger, "metadata.distributions");
        }

        /// <summary>
        /// Lists the accepted string values for every enum-typed request field, in the camelCase
        /// form the JSON contract expects.
        /// </summary>
        /// <returns>The enum options listing.</returns>
        /// <response code="200">The listing.</response>
        [HttpGet("enums")]
        [ProducesResponseType(typeof(EnumOptionsResponse), StatusCodes.Status200OK)]
        public Task<ActionResult<EnumOptionsResponse>> GetEnumOptions()
        {
            return ExecuteAsync(MetadataMapper.ToEnumOptionsResponse, _logger, "metadata.enums");
        }

        /// <summary>
        /// Reports the server defaults and limits: the default exceedance-probability ordinates
        /// frequency curves are evaluated at, and the configured run/store caps.
        /// </summary>
        /// <returns>The defaults listing.</returns>
        /// <response code="200">The defaults.</response>
        [HttpGet("defaults")]
        [ProducesResponseType(typeof(DefaultsResponse), StatusCodes.Status200OK)]
        public Task<ActionResult<DefaultsResponse>> GetDefaults()
        {
            return ExecuteAsync(() => new DefaultsResponse
            {
                ProbabilityOrdinates = new ProbabilityOrdinates().ToList(),
                MaxIterations = _options.MaxIterations,
                MaxConcurrentRuns = _options.MaxConcurrentRuns,
                MaxResources = _options.MaxResources,
                Notes = new List<string>
                {
                    "Probability ordinates are annual exceedance probabilities (AEP); e.g., 0.01 is the 100-year event.",
                    "Analysis run endpoints are synchronous: the response returns when the run completes.",
                    "Resources are held in memory only and are lost when the server restarts."
                }
            }, _logger, "metadata.defaults");
        }
    }
}
