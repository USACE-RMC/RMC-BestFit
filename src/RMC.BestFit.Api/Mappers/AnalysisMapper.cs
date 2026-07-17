using Numerics.Distributions;
using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Helpers;
using RMC.BestFit.Api.Store;

namespace RMC.BestFit.Api.Mappers
{
    /// <summary>
    /// Maps analysis resources to their summary/response DTOs.
    /// </summary>
    public static class AnalysisMapper
    {
        /// <summary>
        /// Maps a resource to its summary DTO, running model-layer validation to report current validity.
        /// </summary>
        /// <param name="resource">The analysis resource.</param>
        /// <returns>The summary DTO.</returns>
        public static AnalysisSummaryDto ToSummary(AnalysisResource resource)
        {
            ArgumentNullException.ThrowIfNull(resource);
            var (isValid, messages) = AnalysisRunHelper.Validate(resource);

            var summary = new AnalysisSummaryDto
            {
                Id = resource.Id,
                Name = resource.Name,
                Description = resource.Description,
                CreatedUtc = resource.CreatedUtc,
                Kind = EnumHelper.ToCamelCase(resource.Kind.ToString()),
                State = EnumHelper.ToCamelCase(resource.State.ToString()),
                IsValid = isValid,
                ValidationMessages = isValid ? null : messages,
                Warnings = resource.CreationWarnings.Count > 0 ? resource.CreationWarnings.ToList() : null,
                InputDataId = resource.InputDataId,
                StageTimeSeriesId = resource.StageTimeSeriesId,
                DischargeTimeSeriesId = resource.DischargeTimeSeriesId,
                LastRunUtc = resource.LastRunUtc,
                LastRunMs = resource.LastRunMs,
                LastError = resource.LastError
            };

            switch (resource.Kind)
            {
                case AnalysisKind.Univariate:
                    summary.Distribution = EnumHelper.ToCamelCase(resource.Univariate!.UnivariateDistribution.DistributionType.ToString());
                    break;
                case AnalysisKind.Bulletin17C:
                    summary.Distribution = EnumHelper.ToCamelCase(resource.Bulletin17C!.Bulletin17CDistribution.DistributionType.ToString());
                    summary.UncertaintyMethod = EnumHelper.ToCamelCase(resource.Bulletin17C.UncertaintyMethod.ToString());
                    break;
                case AnalysisKind.RatingCurve:
                    summary.NumberOfSegments = resource.RatingCurve!.RatingCurve.NumberOfSegments;
                    break;
                case AnalysisKind.Mixture:
                    var mixture = resource.Mixture!.MixtureDistribution;
                    summary.ComponentDistributions = (mixture.Mixture?.Distributions ?? Array.Empty<UnivariateDistributionBase>())
                        .Select(d => EnumHelper.ToCamelCase(d.Type.ToString()))
                        .ToList();
                    summary.IsZeroInflated = mixture.IsZeroInflated;
                    break;
                case AnalysisKind.PointProcess:
                    var pointProcess = resource.PointProcess!.PointProcess;
                    summary.IsSeasonal = pointProcess.IsSeasonal;
                    summary.Threshold = NanToNull(pointProcess.Threshold);
                    summary.TotalYears = NanToNull(pointProcess.TotalYears);
                    summary.Lambda = NanToNull(pointProcess.Lambda);
                    break;
                case AnalysisKind.CompetingRisks:
                    var competingRisks = resource.CompetingRisks!.CompetingRisksDistribution;
                    summary.ComponentDistributions = competingRisks.CompetingRisks is null
                        ? new List<string>()
                        : competingRisks.CompetingRisks.Distributions
                            .Select(d => EnumHelper.ToCamelCase(d.Type.ToString()))
                            .ToList();
                    break;
                case AnalysisKind.Composite:
                    var composite = resource.Composite!;
                    summary.CompositeType = EnumHelper.ToCamelCase(composite.CompositeDistributionType.ToString());
                    if (composite.CompositeDistributionType == RMC.BestFit.Analyses.CompositeType.ModelAverage)
                    {
                        summary.AverageMethod = EnumHelper.ToCamelCase(composite.ModelAverageMethod.ToString());
                    }
                    if (composite.CompositeDistributionType == RMC.BestFit.Analyses.CompositeType.CompetingRisks)
                    {
                        summary.Dependency = EnumHelper.ToCamelCase(composite.Dependency.ToString());
                        summary.IsMaximum = composite.IsMaximum;
                    }
                    summary.ComponentAnalysisIds = resource.ComponentAnalysisIds;
                    break;
                case AnalysisKind.DistributionFitting:
                    summary.ComponentDistributions = resource.DistributionFitting!.DistributionList
                        .Select(d => EnumHelper.ToCamelCase(d.Type.ToString()))
                        .ToList();
                    break;
                case AnalysisKind.Bivariate:
                    var bivariate = resource.Bivariate!.BivariateDistribution;
                    summary.CopulaType = EnumHelper.ToCamelCase(bivariate.CopulaType.ToString());
                    summary.CopulaEstimationMethod = EnumHelper.ToCamelCase(bivariate.CopulaEstimationMethod.ToString());
                    summary.MarginalXAnalysisId = resource.MarginalXAnalysisId;
                    summary.MarginalYAnalysisId = resource.MarginalYAnalysisId;
                    break;
                case AnalysisKind.CoincidentFrequency:
                    summary.BivariateAnalysisId = resource.BivariateAnalysisId;
                    summary.NumberOfBins = resource.CoincidentFrequency!.NumberOfBins;
                    break;
                case AnalysisKind.TimeSeries:
                    summary.TimeSeriesModelType = EnumHelper.ToCamelCase(resource.TimeSeriesModel!.Value.ToString());
                    summary.TimeSeriesId = resource.TimeSeriesId;
                    summary.CovariateTimeSeriesIds = resource.CovariateTimeSeriesIds;
                    summary.ForecastingTimeSteps = resource.TimeSeriesModel switch
                    {
                        TimeSeriesModelType.Ar => resource.Ar!.ForecastingTimeSteps,
                        TimeSeriesModelType.Ma => resource.Ma!.ForecastingTimeSteps,
                        TimeSeriesModelType.Arima => resource.Arima!.ForecastingTimeSteps,
                        TimeSeriesModelType.Arimax => resource.Arimax!.ForecastingTimeSteps,
                        _ => null
                    };
                    break;
                default:
                    break;
            }

            return summary;
        }

        /// <summary>
        /// Converts NaN to null so not-yet-derived numeric settings are omitted from the JSON.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>Null when NaN; otherwise the value.</returns>
        private static double? NanToNull(double value)
        {
            return double.IsNaN(value) ? null : value;
        }

        /// <summary>
        /// Maps a resource to a single-resource response.
        /// </summary>
        /// <param name="resource">The analysis resource.</param>
        /// <returns>The response DTO.</returns>
        public static AnalysisResourceResponse ToResourceResponse(AnalysisResource resource)
        {
            return new AnalysisResourceResponse { Analysis = ToSummary(resource) };
        }

        /// <summary>
        /// Maps a list of resources to the list response.
        /// </summary>
        /// <param name="resources">The analysis resources ordered by creation time.</param>
        /// <returns>The list response DTO.</returns>
        public static AnalysisListResponse ToListResponse(IReadOnlyList<AnalysisResource> resources)
        {
            ArgumentNullException.ThrowIfNull(resources);
            return new AnalysisListResponse
            {
                Count = resources.Count,
                Analyses = resources.Select(ToSummary).ToList()
            };
        }
    }
}
