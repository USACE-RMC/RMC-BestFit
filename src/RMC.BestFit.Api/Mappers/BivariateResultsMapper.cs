using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Helpers;
using RMC.BestFit.Api.Store;

namespace RMC.BestFit.Api.Mappers
{
    /// <summary>
    /// Maps bivariate copula and coincident frequency results to their response DTOs. Both
    /// results carry <c>[n, 2]</c> confidence intervals (lower, upper) — per XY grid point for
    /// the bivariate joint exceedance, per response-magnitude bin for the coincident frequency
    /// curve.
    /// </summary>
    public static class BivariateResultsMapper
    {
        /// <summary>
        /// Builds the joint-exceedance results response for a bivariate analysis.
        /// </summary>
        /// <param name="resource">The bivariate analysis resource.</param>
        /// <returns>The results response.</returns>
        /// <exception cref="ArgumentException">Thrown when the resource is not a bivariate analysis.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the analysis has never been run.</exception>
        public static BivariateResultsResponse ToBivariateResults(AnalysisResource resource)
        {
            ArgumentNullException.ThrowIfNull(resource);
            if (resource.Kind != AnalysisKind.Bivariate)
            {
                throw new ArgumentException($"Analysis kind '{resource.Kind}' does not produce bivariate results.", nameof(resource));
            }
            var analysis = resource.Bivariate!;
            var results = analysis.AnalysisResults
                ?? throw new InvalidOperationException("The analysis has no results. Run it first (POST .../run).");
            var distribution = analysis.BivariateDistribution;
            var parameterNames = distribution.Parameters.Select(p => p.DisplayName).ToList();

            var curve = new JointExceedanceCurveDto
            {
                ModeProbabilities = results.ModeCurve?.ToList(),
                MeanProbabilities = results.MeanCurve?.ToList(),
                CredibleIntervalWidth = analysis.BayesianAnalysis.CredibleIntervalWidth
            };
            foreach (var ordinate in analysis.XYOrdinates)
            {
                curve.X.Add(ordinate.X);
                curve.Y.Add(ordinate.Y?.Mean ?? double.NaN);
            }
            if (results.ConfidenceIntervals is { } intervals && intervals.GetLength(1) >= 2)
            {
                int rows = intervals.GetLength(0);
                var lower = new List<double>(rows);
                var upper = new List<double>(rows);
                for (int i = 0; i < rows; i++)
                {
                    lower.Add(intervals[i, 0]);
                    upper.Add(intervals[i, 1]);
                }
                curve.CiLower = lower;
                curve.CiUpper = upper;
            }

            return new BivariateResultsResponse
            {
                AnalysisId = resource.Id,
                Kind = EnumHelper.ToCamelCase(resource.Kind.ToString()),
                CopulaType = EnumHelper.ToCamelCase(distribution.CopulaType.ToString()),
                EstimationMethod = EnumHelper.ToCamelCase(distribution.CopulaEstimationMethod.ToString()),
                MarginalX = ToMarginalInfo(resource.MarginalXResource, resource.MarginalXAnalysisId),
                MarginalY = ToMarginalInfo(resource.MarginalYResource, resource.MarginalYAnalysisId),
                Parameters = distribution.Parameters
                    .Select(p => new ParameterValueDto { Name = p.DisplayName, Value = p.Value })
                    .ToList(),
                ParameterSummaries = ResultsMapper.BuildParameterSummaries(parameterNames, analysis.BayesianAnalysis.Results, includeChainDiagnostics: true),
                JointExceedance = curve,
                InformationCriteria = ResultsMapper.BuildInformationCriteria(results, analysis.BayesianAnalysis),
                Diagnostics = ResultsMapper.BuildMcmcDiagnostics(analysis.BayesianAnalysis, parameterNames)
            };
        }

        /// <summary>
        /// Builds the response frequency-curve results for a coincident frequency analysis.
        /// </summary>
        /// <param name="resource">The coincident frequency analysis resource.</param>
        /// <returns>The results response.</returns>
        /// <exception cref="ArgumentException">Thrown when the resource is not a coincident frequency analysis.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the analysis has never been run.</exception>
        public static CoincidentFrequencyResultsResponse ToCoincidentFrequencyResults(AnalysisResource resource)
        {
            ArgumentNullException.ThrowIfNull(resource);
            if (resource.Kind != AnalysisKind.CoincidentFrequency)
            {
                throw new ArgumentException($"Analysis kind '{resource.Kind}' does not produce coincident frequency results.", nameof(resource));
            }
            var analysis = resource.CoincidentFrequency!;
            var results = analysis.AnalysisResults
                ?? throw new InvalidOperationException("The analysis has no results. Run it first (POST .../run).");

            var response = new CoincidentFrequencyResultsResponse
            {
                AnalysisId = resource.Id,
                Kind = EnumHelper.ToCamelCase(resource.Kind.ToString()),
                NumberOfBins = analysis.NumberOfBins,
                ZValues = analysis.ZOutputValues?.ToList() ?? new List<double>(),
                AepMode = results.ModeCurve?.ToList(),
                AepMean = results.MeanCurve?.ToList(),
                CredibleIntervalWidth = analysis.BayesianAnalysis.CredibleIntervalWidth,
                MarginalXChainUsed = analysis.MarginalXChain != null,
                MarginalYChainUsed = analysis.MarginalYChain != null
            };
            if (results.ConfidenceIntervals is { } intervals && intervals.GetLength(1) >= 2)
            {
                int rows = intervals.GetLength(0);
                var lower = new List<double>(rows);
                var upper = new List<double>(rows);
                for (int i = 0; i < rows; i++)
                {
                    lower.Add(intervals[i, 0]);
                    upper.Add(intervals[i, 1]);
                }
                response.CiLower = lower;
                response.CiUpper = upper;
            }
            return response;
        }

        /// <summary>
        /// Builds the identity block for one marginal of a bivariate results response.
        /// </summary>
        /// <param name="marginal">The LIVE marginal resource, or null when unavailable.</param>
        /// <param name="analysisId">The provenance id recorded at creation.</param>
        /// <returns>The marginal info DTO.</returns>
        private static MarginalInfoDto ToMarginalInfo(AnalysisResource? marginal, Guid? analysisId)
        {
            var info = new MarginalInfoDto { AnalysisId = analysisId };
            if (marginal == null) return info;

            info.Name = marginal.Name;
            info.Kind = EnumHelper.ToCamelCase(marginal.Kind.ToString());
            info.DistributionType = marginal.Kind switch
            {
                AnalysisKind.Univariate => EnumHelper.ToCamelCase(marginal.Univariate!.UnivariateDistribution.DistributionType.ToString()),
                AnalysisKind.Bulletin17C => EnumHelper.ToCamelCase(marginal.Bulletin17C!.Bulletin17CDistribution.DistributionType.ToString()),
                _ => null
            };
            return info;
        }
    }
}
