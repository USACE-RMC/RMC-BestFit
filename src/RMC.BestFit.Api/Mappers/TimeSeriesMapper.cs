using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Helpers;
using RMC.BestFit.Api.Store;

namespace RMC.BestFit.Api.Mappers
{
    /// <summary>
    /// Maps time-series resources to their response DTOs.
    /// </summary>
    public static class TimeSeriesMapper
    {
        /// <summary>
        /// Maps a resource to its summary DTO.
        /// </summary>
        /// <param name="resource">The time-series resource.</param>
        /// <returns>The summary DTO.</returns>
        public static TimeSeriesSummaryDto ToSummary(TimeSeriesResource resource)
        {
            ArgumentNullException.ThrowIfNull(resource);
            return new TimeSeriesSummaryDto
            {
                Id = resource.Id,
                Name = resource.Name,
                Description = resource.Description,
                CreatedUtc = resource.CreatedUtc,
                Source = EnumHelper.ToCamelCase(resource.Source.ToString()),
                UsgsSiteNumber = resource.UsgsSiteNumber,
                SeriesType = resource.UsgsSeriesType.HasValue ? EnumHelper.ToCamelCase(resource.UsgsSeriesType.Value.ToString()) : null,
                TimeInterval = EnumHelper.ToCamelCase(resource.TimeSeries.TimeInterval.ToString()),
                PointCount = resource.PointCount,
                MissingCount = resource.MissingCount,
                StartDate = resource.StartDate,
                EndDate = resource.EndDate
            };
        }

        /// <summary>
        /// Maps a resource to a single-resource response, optionally including a page of points.
        /// </summary>
        /// <param name="resource">The time-series resource.</param>
        /// <param name="includePoints">True to include ordinate values in the response.</param>
        /// <param name="offset">Zero-based index of the first point to include.</param>
        /// <param name="limit">Maximum number of points to include.</param>
        /// <returns>The response DTO.</returns>
        public static TimeSeriesResourceResponse ToResourceResponse(TimeSeriesResource resource, bool includePoints = false, int offset = 0, int limit = 10000)
        {
            ArgumentNullException.ThrowIfNull(resource);
            var response = new TimeSeriesResourceResponse { TimeSeries = ToSummary(resource) };
            if (includePoints)
            {
                offset = Math.Max(0, offset);
                limit = Math.Max(0, limit);
                var points = new List<TimeSeriesPointDto>();
                int end = Math.Min(resource.TimeSeries.Count, offset + limit);
                for (int i = offset; i < end; i++)
                {
                    points.Add(new TimeSeriesPointDto
                    {
                        DateTime = resource.TimeSeries[i].Index,
                        Value = resource.TimeSeries[i].Value
                    });
                }
                response.Points = points;
                response.PointsOffset = offset;
            }
            return response;
        }

        /// <summary>
        /// Maps a list of resources to the list response.
        /// </summary>
        /// <param name="resources">The time-series resources ordered by creation time.</param>
        /// <returns>The list response DTO.</returns>
        public static TimeSeriesListResponse ToListResponse(IReadOnlyList<TimeSeriesResource> resources)
        {
            ArgumentNullException.ThrowIfNull(resources);
            return new TimeSeriesListResponse
            {
                Count = resources.Count,
                TimeSeries = resources.Select(ToSummary).ToList()
            };
        }
    }
}
