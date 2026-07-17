using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Helpers;
using RMC.BestFit.Api.Store;
using RMC.BestFit.Models;

namespace RMC.BestFit.Api.Mappers
{
    /// <summary>
    /// Maps input-data resources to their response DTOs.
    /// </summary>
    public static class InputDataMapper
    {
        /// <summary>
        /// Maps a resource to its summary DTO.
        /// </summary>
        /// <param name="resource">The input-data resource.</param>
        /// <returns>The summary DTO.</returns>
        public static InputDataSummaryDto ToSummary(InputDataResource resource)
        {
            ArgumentNullException.ThrowIfNull(resource);
            var dataFrame = resource.DataFrame;

            int lowOutlierCount = 0;
            foreach (ExactData data in dataFrame.ExactSeries)
            {
                if (data.IsLowOutlier) lowOutlierCount++;
            }

            return new InputDataSummaryDto
            {
                Id = resource.Id,
                Name = resource.Name,
                Description = resource.Description,
                CreatedUtc = resource.CreatedUtc,
                Method = EnumHelper.ToCamelCase(resource.Method.ToString()),
                SourceTimeSeriesId = resource.SourceTimeSeriesId,
                UsgsSiteNumber = resource.UsgsSiteNumber,
                RecordLength = dataFrame.TotalRecordLength(),
                ExactCount = dataFrame.ExactSeries.Count,
                UncertainCount = dataFrame.UncertainSeries.Count,
                IntervalCount = dataFrame.IntervalSeries.Count,
                ThresholdCount = dataFrame.ThresholdSeries.Count,
                Lambda = dataFrame.Lambda,
                PlottingParameter = dataFrame.PlottingParameter,
                LowOutlierThreshold = double.IsNaN(dataFrame.LowOutlierThreshold) ? null : dataFrame.LowOutlierThreshold,
                LowOutlierCount = lowOutlierCount,
                BlockOptions = ToBlockOptions(resource),
                PotOptions = ToPotOptions(resource)
            };
        }

        /// <summary>
        /// Maps a resource to a single-resource response, optionally including the observation lists.
        /// </summary>
        /// <param name="resource">The input-data resource.</param>
        /// <param name="includeData">True to include the exact/interval/threshold observation lists.</param>
        /// <returns>The response DTO.</returns>
        public static InputDataResourceResponse ToResourceResponse(InputDataResource resource, bool includeData = false)
        {
            ArgumentNullException.ThrowIfNull(resource);
            var response = new InputDataResourceResponse { InputData = ToSummary(resource) };
            if (includeData)
            {
                response.ExactData = ToExactObservations(resource.DataFrame);
                if (resource.DataFrame.UncertainSeries.Count > 0) response.UncertainData = ToUncertainObservations(resource.DataFrame);
                if (resource.DataFrame.IntervalSeries.Count > 0) response.IntervalData = ToIntervalObservations(resource.DataFrame);
                if (resource.DataFrame.ThresholdSeries.Count > 0) response.ThresholdData = ToThresholdObservations(resource.DataFrame);
            }
            return response;
        }

        /// <summary>
        /// Maps a list of resources to the list response.
        /// </summary>
        /// <param name="resources">The input-data resources ordered by creation time.</param>
        /// <returns>The list response DTO.</returns>
        public static InputDataListResponse ToListResponse(IReadOnlyList<InputDataResource> resources)
        {
            ArgumentNullException.ThrowIfNull(resources);
            return new InputDataListResponse
            {
                Count = resources.Count,
                InputData = resources.Select(ToSummary).ToList()
            };
        }

        /// <summary>
        /// Extracts the exact observations (with computed plotting positions) from a data frame.
        /// </summary>
        /// <param name="dataFrame">The data frame.</param>
        /// <returns>The exact observation DTOs.</returns>
        public static List<ExactObservationDto> ToExactObservations(DataFrame dataFrame)
        {
            ArgumentNullException.ThrowIfNull(dataFrame);
            var list = new List<ExactObservationDto>(dataFrame.ExactSeries.Count);
            foreach (ExactData data in dataFrame.ExactSeries)
            {
                list.Add(new ExactObservationDto
                {
                    Index = data.Index,
                    Value = data.Value,
                    IsLowOutlier = data.IsLowOutlier,
                    PlottingPosition = data.PlottingPosition
                });
            }
            return list;
        }

        /// <summary>
        /// Extracts the uncertain observations (with their measurement-error distributions,
        /// nominal values, and computed plotting positions) from a data frame.
        /// </summary>
        /// <param name="dataFrame">The data frame.</param>
        /// <returns>The uncertain observation DTOs.</returns>
        public static List<UncertainObservationDto> ToUncertainObservations(DataFrame dataFrame)
        {
            ArgumentNullException.ThrowIfNull(dataFrame);
            var list = new List<UncertainObservationDto>(dataFrame.UncertainSeries.Count);
            foreach (UncertainData data in dataFrame.UncertainSeries)
            {
                list.Add(new UncertainObservationDto
                {
                    Index = data.Index,
                    Distribution = DistributionSpecMapper.ToSpec(data.Distribution),
                    Value = data.Value,
                    PlottingPosition = data.PlottingPosition
                });
            }
            return list;
        }

        /// <summary>
        /// Extracts the interval-censored observations from a data frame.
        /// </summary>
        /// <param name="dataFrame">The data frame.</param>
        /// <returns>The interval observation DTOs.</returns>
        public static List<IntervalObservationDto> ToIntervalObservations(DataFrame dataFrame)
        {
            ArgumentNullException.ThrowIfNull(dataFrame);
            var list = new List<IntervalObservationDto>(dataFrame.IntervalSeries.Count);
            foreach (IntervalData data in dataFrame.IntervalSeries)
            {
                list.Add(new IntervalObservationDto
                {
                    Index = data.Index,
                    LowerBound = data.LowerValue,
                    UpperBound = data.UpperValue,
                    Value = data.Value,
                    PlottingPosition = data.PlottingPosition
                });
            }
            return list;
        }

        /// <summary>
        /// Extracts the perception-threshold records from a data frame.
        /// </summary>
        /// <param name="dataFrame">The data frame.</param>
        /// <returns>The threshold observation DTOs.</returns>
        public static List<ThresholdObservationDto> ToThresholdObservations(DataFrame dataFrame)
        {
            ArgumentNullException.ThrowIfNull(dataFrame);
            var list = new List<ThresholdObservationDto>(dataFrame.ThresholdSeries.Count);
            foreach (ThresholdData data in dataFrame.ThresholdSeries)
            {
                list.Add(new ThresholdObservationDto
                {
                    StartIndex = data.StartIndex,
                    EndIndex = data.EndIndex,
                    Value = data.Value,
                    NumberAbove = data.NumberAbove,
                    NumberBelow = data.NumberBelow,
                    PlottingPosition = data.PlottingPosition
                });
            }
            return list;
        }

        /// <summary>
        /// Builds the block-options echo for a block-maxima resource, or null for other methods.
        /// </summary>
        /// <param name="resource">The input-data resource.</param>
        /// <returns>The block options DTO, or null.</returns>
        private static BlockOptionsDto? ToBlockOptions(InputDataResource resource)
        {
            if (resource.Method != InputDataMethod.BlockMaxima) return null;
            return new BlockOptionsDto
            {
                TimeBlock = resource.TimeBlock.HasValue ? EnumHelper.ToCamelCase(resource.TimeBlock.Value.ToString()) : null,
                BlockFunction = resource.BlockFunction.HasValue ? EnumHelper.ToCamelCase(resource.BlockFunction.Value.ToString()) : null,
                SmoothingFunction = resource.SmoothingFunction.HasValue ? EnumHelper.ToCamelCase(resource.SmoothingFunction.Value.ToString()) : null,
                StartMonth = resource.StartMonth,
                EndMonth = resource.EndMonth,
                Period = resource.Period
            };
        }

        /// <summary>
        /// Builds the POT-options echo for a peaks-over-threshold resource, or null for other methods.
        /// </summary>
        /// <param name="resource">The input-data resource.</param>
        /// <returns>The POT options DTO, or null.</returns>
        private static PotOptionsDto? ToPotOptions(InputDataResource resource)
        {
            if (resource.Method != InputDataMethod.PeaksOverThreshold) return null;
            return new PotOptionsDto
            {
                Threshold = resource.Threshold,
                MinStepsBetweenPeaks = resource.MinStepsBetweenPeaks,
                SmoothingFunction = resource.SmoothingFunction.HasValue ? EnumHelper.ToCamelCase(resource.SmoothingFunction.Value.ToString()) : null,
                Period = resource.Period
            };
        }
    }
}
