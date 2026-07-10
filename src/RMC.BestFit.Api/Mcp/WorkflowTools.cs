using System.ComponentModel;
using ModelContextProtocol.Server;
using Numerics.Data;
using Numerics.Distributions;
using RMC.BestFit.Analyses;
using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Helpers;
using RMC.BestFit.Api.Services;

namespace RMC.BestFit.Api.Mcp
{
    /// <summary>
    /// MCP tools for the one-shot USGS workflows: each downloads the data, builds the intermediate
    /// resources, runs the analysis, and returns the results plus every created resource id. On a
    /// step failure the response reports success=false and the failed step while preserving the
    /// ids of resources already created.
    /// </summary>
    [McpServerToolType]
    public class WorkflowTools
    {
        /// <summary>
        /// The workflow service shared with the REST controllers.
        /// </summary>
        private readonly IWorkflowService _service;

        /// <summary>
        /// Constructs the tools with their service dependency.
        /// </summary>
        /// <param name="service">The workflow service.</param>
        public WorkflowTools(IWorkflowService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        /// <summary>
        /// Runs the USGS peak-flow frequency workflow.
        /// </summary>
        /// <param name="siteNumber">The 8-digit USGS site number.</param>
        /// <param name="distribution">The distribution name.</param>
        /// <param name="probabilityOrdinates">Optional AEP ordinates.</param>
        /// <param name="prngSeed">Optional PRNG seed.</param>
        /// <param name="name">Optional base name for the created resources.</param>
        /// <param name="cancellationToken">Cancellation token supplied by the MCP host.</param>
        /// <returns>JSON with the created resource ids and the frequency results.</returns>
        [McpServerTool(Name = "run_usgs_peak_frequency_workflow")]
        [Description("One call: download the USGS annual peak-flow file for a site, build input data, run a univariate Bayesian frequency analysis, and return the frequency curve with uncertainty plus the created resource ids. Synchronous — may take up to about a minute.")]
        public async Task<string> RunUsgsPeakFrequencyWorkflow(
            [Description("8-digit USGS surface-water site number, e.g. 01646500.")] string siteNumber,
            [Description("Distribution: logPearsonTypeIII (default), generalizedExtremeValue, ... (see get_metadata).")] string? distribution = null,
            [Description("Optional AEP ordinates, each strictly between 0 and 1.")] double[]? probabilityOrdinates = null,
            [Description("Optional PRNG seed for reproducible runs.")] int? prngSeed = null,
            [Description("Optional base name for the created resources.")] string? name = null,
            CancellationToken cancellationToken = default)
        {
            var response = await _service.RunUsgsPeakFrequencyAsync(new UsgsPeakFrequencyWorkflowRequest
            {
                SiteNumber = siteNumber,
                Distribution = EnumHelper.ParseOrDefault(distribution, UnivariateDistributionType.LogPearsonTypeIII),
                ProbabilityOrdinates = probabilityOrdinates?.ToList(),
                BayesianOptions = prngSeed.HasValue ? new BayesianOptionsDto { PrngSeed = prngSeed } : null,
                Name = name
            }, cancellationToken);
            return McpJson.Serialize(response);
        }

        /// <summary>
        /// Runs the USGS daily-flow block-maxima frequency workflow.
        /// </summary>
        /// <param name="siteNumber">The 8-digit USGS site number.</param>
        /// <param name="distribution">The distribution name.</param>
        /// <param name="timeBlock">The block window name.</param>
        /// <param name="smoothingFunction">The smoothing function name.</param>
        /// <param name="period">The smoothing period in days.</param>
        /// <param name="probabilityOrdinates">Optional AEP ordinates.</param>
        /// <param name="prngSeed">Optional PRNG seed.</param>
        /// <param name="name">Optional base name for the created resources.</param>
        /// <param name="cancellationToken">Cancellation token supplied by the MCP host.</param>
        /// <returns>JSON with the created resource ids and the frequency results.</returns>
        [McpServerTool(Name = "run_usgs_daily_block_max_frequency_workflow")]
        [Description("One call: download the USGS daily-flow record for a site, extract water-year annual maxima (or another block), run a univariate Bayesian frequency analysis, and return the frequency curve plus the created resource ids (timeSeriesId, inputDataId, analysisId). Synchronous.")]
        public async Task<string> RunUsgsDailyBlockMaxFrequencyWorkflow(
            [Description("8-digit USGS surface-water site number.")] string siteNumber,
            [Description("Distribution: logPearsonTypeIII (default), generalizedExtremeValue, ...")] string? distribution = null,
            [Description("Block window: waterYear (default), calendarYear, quarter, month.")] string? timeBlock = null,
            [Description("Smoothing before extraction: none (default) or movingAverage (with period, e.g. 7 for 7-day flows).")] string? smoothingFunction = null,
            [Description("Smoothing period in days. Default 1.")] int period = 1,
            [Description("Optional AEP ordinates, each strictly between 0 and 1.")] double[]? probabilityOrdinates = null,
            [Description("Optional PRNG seed for reproducible runs.")] int? prngSeed = null,
            [Description("Optional base name for the created resources.")] string? name = null,
            CancellationToken cancellationToken = default)
        {
            var response = await _service.RunUsgsBlockMaxFrequencyAsync(new UsgsBlockMaxFrequencyWorkflowRequest
            {
                SiteNumber = siteNumber,
                Distribution = EnumHelper.ParseOrDefault(distribution, UnivariateDistributionType.LogPearsonTypeIII),
                TimeBlock = EnumHelper.ParseOrDefault(timeBlock, TimeBlockWindow.WaterYear),
                SmoothingFunction = EnumHelper.ParseOrDefault(smoothingFunction, SmoothingFunctionType.None),
                Period = period,
                ProbabilityOrdinates = probabilityOrdinates?.ToList(),
                BayesianOptions = prngSeed.HasValue ? new BayesianOptionsDto { PrngSeed = prngSeed } : null,
                Name = name
            }, cancellationToken);
            return McpJson.Serialize(response);
        }

        /// <summary>
        /// Runs the USGS Bulletin 17C workflow.
        /// </summary>
        /// <param name="siteNumber">The 8-digit USGS site number.</param>
        /// <param name="uncertaintyMethod">Optional uncertainty method name.</param>
        /// <param name="probabilityOrdinates">Optional AEP ordinates.</param>
        /// <param name="name">Optional base name for the created resources.</param>
        /// <param name="cancellationToken">Cancellation token supplied by the MCP host.</param>
        /// <returns>JSON with the created resource ids and the frequency results.</returns>
        [McpServerTool(Name = "run_usgs_bulletin17c_workflow")]
        [Description("One call: download the USGS annual peak-flow file for a site, build input data, run a Bulletin 17C flood frequency analysis (USGS guidelines), and return the frequency curve with confidence intervals plus the created resource ids. Synchronous.")]
        public async Task<string> RunUsgsBulletin17CWorkflow(
            [Description("8-digit USGS surface-water site number.")] string siteNumber,
            [Description("Uncertainty method: linkedMultivariateNormal (default), multivariateNormal, bootstrap, biasCorrectedBootstrap.")] string? uncertaintyMethod = null,
            [Description("Optional AEP ordinates, each strictly between 0 and 1.")] double[]? probabilityOrdinates = null,
            [Description("Optional base name for the created resources.")] string? name = null,
            CancellationToken cancellationToken = default)
        {
            var response = await _service.RunUsgsBulletin17CAsync(new UsgsBulletin17CWorkflowRequest
            {
                SiteNumber = siteNumber,
                UncertaintyMethod = EnumHelper.ParseOrNull<UncertaintyMethod>(uncertaintyMethod),
                ProbabilityOrdinates = probabilityOrdinates?.ToList(),
                Name = name
            }, cancellationToken);
            return McpJson.Serialize(response);
        }

        /// <summary>
        /// Runs the USGS rating curve workflow.
        /// </summary>
        /// <param name="siteNumber">The 8-digit USGS site number.</param>
        /// <param name="numberOfSegments">The number of power-law segments (1-3).</param>
        /// <param name="prngSeed">Optional PRNG seed.</param>
        /// <param name="name">Optional base name for the created resources.</param>
        /// <param name="cancellationToken">Cancellation token supplied by the MCP host.</param>
        /// <returns>JSON with the created resource ids and the rating curve results.</returns>
        [McpServerTool(Name = "run_usgs_rating_curve_workflow")]
        [Description("One call: download the discrete USGS field measurements of stage and discharge for a site, run a Bayesian rating curve analysis over the date-aligned pairs, and return the fitted stage-discharge curve with credible intervals plus the created resource ids. Synchronous.")]
        public async Task<string> RunUsgsRatingCurveWorkflow(
            [Description("8-digit USGS surface-water site number.")] string siteNumber,
            [Description("Number of piecewise power-law segments (1-3). Default 1.")] int numberOfSegments = 1,
            [Description("Optional PRNG seed for reproducible runs.")] int? prngSeed = null,
            [Description("Optional base name for the created resources.")] string? name = null,
            CancellationToken cancellationToken = default)
        {
            var response = await _service.RunUsgsRatingCurveAsync(new UsgsRatingCurveWorkflowRequest
            {
                SiteNumber = siteNumber,
                NumberOfSegments = numberOfSegments,
                BayesianOptions = prngSeed.HasValue ? new BayesianOptionsDto { PrngSeed = prngSeed } : null,
                Name = name
            }, cancellationToken);
            return McpJson.Serialize(response);
        }
    }
}
