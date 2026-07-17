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
    /// MCP tools for creating and inspecting time-series resources.
    /// </summary>
    [McpServerToolType]
    public class TimeSeriesTools
    {
        /// <summary>
        /// The time-series service shared with the REST controllers.
        /// </summary>
        private readonly ITimeSeriesService _service;

        /// <summary>
        /// Constructs the tools with their service dependency.
        /// </summary>
        /// <param name="service">The time-series service.</param>
        public TimeSeriesTools(ITimeSeriesService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        /// <summary>
        /// Downloads a series from the USGS water services and stores it as a resource.
        /// </summary>
        /// <param name="siteNumber">The 8-digit USGS site number.</param>
        /// <param name="seriesType">The series type name.</param>
        /// <param name="name">Optional display name.</param>
        /// <param name="cancellationToken">Cancellation token supplied by the MCP host.</param>
        /// <returns>JSON with the created resource summary including its id.</returns>
        [McpServerTool(Name = "usgs_download_timeseries")]
        [Description("Download a time series from the USGS water services and store it as a resource. Returns the resource id to pass to later tools. Use seriesType dailyDischarge/dailyStage for block-maxima workflows, measuredStage/measuredDischarge pairs for rating curves, and peakDischarge/peakStage for annual peaks (or use create_inputdata_usgs_peaks directly).")]
        public async Task<string> UsgsDownloadTimeSeries(
            [Description("8-digit USGS surface-water site number, e.g. 01646500.")] string siteNumber,
            [Description("Series type: dailyDischarge (default), dailyStage, instantaneousDischarge, instantaneousStage, peakDischarge, peakStage, measuredDischarge, or measuredStage.")] string? seriesType = null,
            [Description("Optional display name for the resource.")] string? name = null,
            CancellationToken cancellationToken = default)
        {
            var resource = await _service.CreateFromUsgsAsync(new CreateUsgsTimeSeriesRequest
            {
                SiteNumber = siteNumber,
                SeriesType = EnumHelper.ParseOrDefault(seriesType, TimeSeriesDownload.TimeSeriesType.DailyDischarge),
                Name = name
            }, cancellationToken);
            return McpJson.Serialize(TimeSeriesMapper.ToResourceResponse(resource));
        }

        /// <summary>
        /// Builds a time series from supplied points and stores it as a resource.
        /// </summary>
        /// <param name="points">The ordinates (dateTime + value per point).</param>
        /// <param name="timeInterval">The recording interval name.</param>
        /// <param name="name">Optional display name.</param>
        /// <returns>JSON with the created resource summary including its id.</returns>
        [McpServerTool(Name = "create_manual_timeseries")]
        [Description("Create a time-series resource from explicit points (each with dateTime and value; NaN marks missing). Use timeInterval oneDay for daily records or irregular for discrete measurements. Returns the resource id.")]
        public string CreateManualTimeSeries(
            [Description("The points: array of { dateTime, value }. Sorted by date automatically.")] List<TimeSeriesPointDto> points,
            [Description("Recording interval: oneDay (default), oneHour, irregular, ...")] string? timeInterval = null,
            [Description("Optional display name for the resource.")] string? name = null)
        {
            var resource = _service.CreateManual(new CreateManualTimeSeriesRequest
            {
                Points = points,
                TimeInterval = EnumHelper.ParseOrDefault(timeInterval, TimeInterval.OneDay),
                Name = name
            });
            return McpJson.Serialize(TimeSeriesMapper.ToResourceResponse(resource));
        }

        /// <summary>
        /// Returns a time-series resource summary, optionally with a page of points.
        /// </summary>
        /// <param name="id">The resource id.</param>
        /// <param name="includePoints">True to include ordinate values.</param>
        /// <param name="offset">Zero-based index of the first point.</param>
        /// <param name="limit">Maximum number of points returned.</param>
        /// <returns>JSON with the resource summary and optional points page.</returns>
        [McpServerTool(Name = "get_timeseries")]
        [Description("Get a time-series resource summary by id, optionally with a page of its points (keep limit modest — daily records span decades).")]
        public string GetTimeSeries(
            [Description("The time-series resource id.")] Guid id,
            [Description("True to include point values. Default false.")] bool includePoints = false,
            [Description("Zero-based index of the first point to return. Default 0.")] int offset = 0,
            [Description("Maximum number of points to return. Default 1000.")] int limit = 1000)
        {
            var resource = _service.Get(id);
            return McpJson.Serialize(TimeSeriesMapper.ToResourceResponse(resource, includePoints, offset, limit));
        }
    }
}
