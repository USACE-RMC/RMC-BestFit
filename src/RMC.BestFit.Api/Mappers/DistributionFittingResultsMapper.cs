using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Helpers;
using RMC.BestFit.Api.Store;
using RMC.BestFit.Models;

namespace RMC.BestFit.Api.Mappers
{
    /// <summary>
    /// Maps distribution-fitting results (per-candidate maximum-likelihood fits with information
    /// criteria) to the ranked results response.
    /// </summary>
    public static class DistributionFittingResultsMapper
    {
        /// <summary>
        /// Builds the ranked results response for a distribution-fitting analysis. Successful
        /// fits are ranked by AIC ascending (NaN AIC after finite); failed fits follow.
        /// </summary>
        /// <param name="resource">The distribution-fitting analysis resource.</param>
        /// <returns>The results response.</returns>
        /// <exception cref="ArgumentException">Thrown when the resource is not a distribution-fitting analysis.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the analysis has never been run.</exception>
        public static DistributionFittingResultsResponse ToResults(AnalysisResource resource)
        {
            ArgumentNullException.ThrowIfNull(resource);
            if (resource.Kind != AnalysisKind.DistributionFitting)
            {
                throw new ArgumentException($"Analysis kind '{resource.Kind}' does not produce distribution-fitting results.", nameof(resource));
            }
            var analysis = resource.DistributionFitting!;
            if (!analysis.IsEstimated)
            {
                throw new InvalidOperationException("The analysis has no results. Run it first (POST .../run).");
            }

            var response = new DistributionFittingResultsResponse
            {
                AnalysisId = resource.Id,
                Kind = EnumHelper.ToCamelCase(resource.Kind.ToString()),
                Fits = ToRankedFits(analysis.FittedDistributions)
            };
            return response;
        }

        /// <summary>
        /// Ranks candidate fits — successful fits by AIC ascending (NaN AIC after finite), failed
        /// fits after all successful ones — and maps them to DTOs with 1-based ranks.
        /// </summary>
        /// <param name="fits">The model-layer fitted distributions.</param>
        /// <returns>The ranked fit DTOs.</returns>
        public static List<DistributionFitDto> ToRankedFits(IEnumerable<FittedDistribution> fits)
        {
            ArgumentNullException.ThrowIfNull(fits);
            var ordered = fits
                .OrderByDescending(f => f.FitSucceeded)
                .ThenBy(f => double.IsNaN(f.AIC) ? double.PositiveInfinity : f.AIC)
                .ToList();

            var result = new List<DistributionFitDto>(ordered.Count);
            for (int i = 0; i < ordered.Count; i++)
            {
                result.Add(ToFit(ordered[i], rank: i + 1));
            }
            return result;
        }

        /// <summary>
        /// Maps one candidate fit to its DTO.
        /// </summary>
        /// <param name="fit">The model-layer fitted distribution.</param>
        /// <param name="rank">The 1-based rank in the ordered listing.</param>
        /// <returns>The fit DTO.</returns>
        private static DistributionFitDto ToFit(FittedDistribution fit, int rank)
        {
            var distribution = fit.Distribution;
            return new DistributionFitDto
            {
                Rank = rank,
                Distribution = distribution is null ? null : EnumHelper.ToCamelCase(distribution.Type.ToString()),
                DisplayName = distribution is null ? null : MetadataMapper.ToDisplayName(distribution.Type),
                Parameters = distribution is null || !fit.FitSucceeded
                    ? null
                    : distribution.ParameterNames
                        .Zip(distribution.GetParameters, (name, value) => new ParameterValueDto { Name = name, Value = value })
                        .ToList(),
                Aic = NanToNull(fit.AIC),
                Bic = NanToNull(fit.BIC),
                Rmse = NanToNull(fit.RMSE),
                FitSucceeded = fit.FitSucceeded,
                ErrorMessage = string.IsNullOrWhiteSpace(fit.ErrorMessage) ? null : fit.ErrorMessage
            };
        }

        /// <summary>
        /// Converts NaN to null so inapplicable criteria are omitted from the JSON.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>Null when NaN; otherwise the value.</returns>
        private static double? NanToNull(double value)
        {
            return double.IsNaN(value) ? null : value;
        }
    }
}
