using System.ComponentModel;
using ModelContextProtocol.Server;
using Numerics.Data;
using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Helpers;
using RMC.BestFit.Api.Mappers;
using RMC.BestFit.Api.Services;

namespace RMC.BestFit.Api.Mcp
{
    /// <summary>
    /// MCP tools for creating and inspecting input-data resources (the observation sets analyses
    /// are fit to).
    /// </summary>
    [McpServerToolType]
    public class InputDataTools
    {
        /// <summary>
        /// The input-data service shared with the REST controllers.
        /// </summary>
        private readonly IInputDataService _service;

        /// <summary>
        /// Constructs the tools with their service dependency.
        /// </summary>
        /// <param name="service">The input-data service.</param>
        public InputDataTools(IInputDataService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        /// <summary>
        /// Downloads the USGS annual peak-flow file directly into an input-data resource.
        /// </summary>
        /// <param name="siteNumber">The 8-digit USGS site number.</param>
        /// <param name="seriesType">peakDischarge (default) or peakStage.</param>
        /// <param name="name">Optional display name.</param>
        /// <param name="cancellationToken">Cancellation token supplied by the MCP host.</param>
        /// <returns>JSON with the created resource summary including its id.</returns>
        [McpServerTool(Name = "create_inputdata_usgs_peaks")]
        [Description("Download the USGS annual peak-flow file for a site directly into an input-data resource (no intermediate time series needed). Returns the inputData id to pass to create_univariate_analysis or create_bulletin17c_analysis.")]
        public async Task<string> CreateInputDataUsgsPeaks(
            [Description("8-digit USGS surface-water site number.")] string siteNumber,
            [Description("peakDischarge (default) or peakStage.")] string? seriesType = null,
            [Description("Optional display name for the resource.")] string? name = null,
            CancellationToken cancellationToken = default)
        {
            var resource = await _service.CreateFromUsgsPeaksAsync(new CreateUsgsPeaksInputDataRequest
            {
                SiteNumber = siteNumber,
                SeriesType = EnumHelper.ParseOrDefault(seriesType, TimeSeriesDownload.TimeSeriesType.PeakDischarge),
                Name = name
            }, cancellationToken);
            return McpJson.Serialize(InputDataMapper.ToResourceResponse(resource));
        }

        /// <summary>
        /// Extracts block maxima from a stored time series into an input-data resource.
        /// </summary>
        /// <param name="timeSeriesId">The source time-series resource id.</param>
        /// <param name="timeBlock">The block window name.</param>
        /// <param name="blockFunction">The block function name.</param>
        /// <param name="smoothingFunction">The smoothing function name.</param>
        /// <param name="startMonth">Starting month of the water/custom year.</param>
        /// <param name="endMonth">Ending month of the custom year.</param>
        /// <param name="period">Smoothing period in time steps.</param>
        /// <param name="name">Optional display name.</param>
        /// <returns>JSON with the created resource summary including its id.</returns>
        [McpServerTool(Name = "create_inputdata_block_max")]
        [Description("Extract block maxima (water-year annual maxima by default) from a stored daily time series into an input-data resource. Returns the inputData id to pass to analysis creation tools.")]
        public string CreateInputDataBlockMax(
            [Description("Id of the source time-series resource (from usgs_download_timeseries or create_manual_timeseries).")] Guid timeSeriesId,
            [Description("Block window: waterYear (default), calendarYear, customYear, quarter, or month.")] string? timeBlock = null,
            [Description("Function over each block: maximum (default), minimum, ...")] string? blockFunction = null,
            [Description("Smoothing before extraction: none (default), movingAverage, movingSum, difference. Use movingAverage with period 7 for 7-day flows.")] string? smoothingFunction = null,
            [Description("Starting month of the water/custom year (1-12). Default 10 (October).")] int startMonth = 10,
            [Description("Ending month of the custom year (1-12); only used for customYear. Default 9.")] int endMonth = 9,
            [Description("Smoothing period in time steps. Default 1.")] int period = 1,
            [Description("Optional display name for the resource.")] string? name = null)
        {
            var resource = _service.CreateBlockMax(new CreateBlockMaxInputDataRequest
            {
                TimeSeriesId = timeSeriesId,
                TimeBlock = EnumHelper.ParseOrDefault(timeBlock, TimeBlockWindow.WaterYear),
                BlockFunction = EnumHelper.ParseOrDefault(blockFunction, BlockFunctionType.Maximum),
                SmoothingFunction = EnumHelper.ParseOrDefault(smoothingFunction, SmoothingFunctionType.None),
                StartMonth = startMonth,
                EndMonth = endMonth,
                Period = period,
                Name = name
            });
            return McpJson.Serialize(InputDataMapper.ToResourceResponse(resource));
        }

        /// <summary>
        /// Extracts independent peaks-over-threshold events from a stored time series.
        /// </summary>
        /// <param name="timeSeriesId">The source time-series resource id.</param>
        /// <param name="threshold">The threshold magnitude.</param>
        /// <param name="minStepsBetweenPeaks">Minimum steps between independent peaks.</param>
        /// <param name="lambda">Optional explicit events-per-year rate.</param>
        /// <param name="name">Optional display name.</param>
        /// <returns>JSON with the created resource summary including its id.</returns>
        [McpServerTool(Name = "create_inputdata_pot")]
        [Description("Extract independent peaks-over-threshold events from a stored time series into an input-data resource. Choose the threshold so lambda (events per year, reported back) is roughly 1-3. Returns the inputData id.")]
        public string CreateInputDataPot(
            [Description("Id of the source time-series resource.")] Guid timeSeriesId,
            [Description("Threshold magnitude; peaks above it are recorded as events.")] double threshold,
            [Description("Minimum time steps between independent peaks (e.g., 7 for daily data). Default 1.")] int minStepsBetweenPeaks = 1,
            [Description("Optional explicit events-per-year rate; omit to use the model-computed value.")] double? lambda = null,
            [Description("Optional display name for the resource.")] string? name = null)
        {
            var resource = _service.CreatePeaksOverThreshold(new CreatePotInputDataRequest
            {
                TimeSeriesId = timeSeriesId,
                Threshold = threshold,
                MinStepsBetweenPeaks = minStepsBetweenPeaks,
                Lambda = lambda,
                Name = name
            });
            return McpJson.Serialize(InputDataMapper.ToResourceResponse(resource));
        }

        /// <summary>
        /// Builds an input-data resource from explicit observations.
        /// </summary>
        /// <param name="exactData">The exact observations.</param>
        /// <param name="uncertainData">Optional uncertain observations with measurement-error distributions.</param>
        /// <param name="intervalData">Optional interval-censored observations.</param>
        /// <param name="thresholdData">Optional perception-threshold records.</param>
        /// <param name="plottingParameter">Optional plotting-position parameter.</param>
        /// <param name="lowOutlierThreshold">Optional low-outlier threshold.</param>
        /// <param name="lambda">Optional events-per-year rate.</param>
        /// <param name="name">Optional display name.</param>
        /// <returns>JSON with the created resource summary including its id.</returns>
        [McpServerTool(Name = "create_inputdata_manual")]
        [Description("Create an input-data resource from explicit observations: exactData is required (each { index: waterYear, value }), optionally with uncertain observations ({ index, distribution: { type, parameters } } — e.g., paleoflood estimates as measurement-error distributions), interval-censored observations ({ index, lowerBound, upperBound }), and perception thresholds ({ startIndex, endIndex, value, numberAbove }) for historical floods. Returns the inputData id.")]
        public string CreateInputDataManual(
            [Description("Exact observations: array of { index (water year) or dateTime, value, isLowOutlier? }.")] List<ExactObservationDto> exactData,
            [Description("Optional uncertain observations: array of { index (water year) or dateTime, distribution: { type, parameters } }. Example distribution: { type: 'triangular', parameters: [min, mostLikely, max] }. See get_metadata for parameter orders. May not share an index with an exact observation.")] List<UncertainObservationDto>? uncertainData = null,
            [Description("Optional interval-censored observations: array of { index, lowerBound, upperBound, value? }.")] List<IntervalObservationDto>? intervalData = null,
            [Description("Optional perception-threshold records: array of { startIndex, endIndex, value, numberAbove }.")] List<ThresholdObservationDto>? thresholdData = null,
            [Description("Plotting-position parameter a: 0 Weibull (default), 0.375 Blom, 0.44 Gringorten, 0.5 Hazen.")] double? plottingParameter = null,
            [Description("Optional low-outlier threshold; observations at or below it are censored.")] double? lowOutlierThreshold = null,
            [Description("Optional events-per-year rate (lambda); omit for annual data (≈1).")] double? lambda = null,
            [Description("Optional display name for the resource.")] string? name = null)
        {
            var resource = _service.CreateManual(new CreateManualInputDataRequest
            {
                ExactData = exactData,
                UncertainData = uncertainData,
                IntervalData = intervalData,
                ThresholdData = thresholdData,
                PlottingParameter = plottingParameter,
                LowOutlierThreshold = lowOutlierThreshold,
                Lambda = lambda,
                Name = name
            });
            return McpJson.Serialize(InputDataMapper.ToResourceResponse(resource));
        }

        /// <summary>
        /// Returns an input-data resource summary, optionally with the observation lists.
        /// </summary>
        /// <param name="id">The resource id.</param>
        /// <param name="includeData">True to include the observation lists.</param>
        /// <returns>JSON with the resource summary and optional observations.</returns>
        [McpServerTool(Name = "get_inputdata")]
        [Description("Get an input-data resource summary by id, optionally with its observations and computed plotting positions.")]
        public string GetInputData(
            [Description("The input-data resource id.")] Guid id,
            [Description("True to include the exact/interval/threshold observation lists. Default false.")] bool includeData = false)
        {
            var resource = _service.Get(id);
            return McpJson.Serialize(InputDataMapper.ToResourceResponse(resource, includeData));
        }
    }
}
