using Numerics.Distributions;
using Numerics.Sampling.MCMC;
using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Helpers;
using RMC.BestFit.Api.Store;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;

namespace RMC.BestFit.Api.Mappers
{
    /// <summary>
    /// Maps model-layer results (<see cref="UncertaintyAnalysisResults"/>, MCMC posterior
    /// summaries, information criteria) to the API's flat, agent-friendly results DTOs.
    /// </summary>
    /// <remarks>
    /// The confidence-interval array shape differs by analysis: univariate and Bulletin 17C
    /// produce <c>[p, 2]</c> (lower, upper per probability ordinate) while the rating curve
    /// produces <c>[n, 3]</c> (stage bin, lower, upper per grid point). NaN diagnostics are
    /// converted to nulls so clients never have to parse named floating-point literals for
    /// values that simply do not apply.
    /// </remarks>
    public static class ResultsMapper
    {
        /// <summary>
        /// R-hat threshold above which a convergence warning is emitted.
        /// </summary>
        private const double RhatWarningThreshold = 1.1;

        /// <summary>
        /// Effective-sample-size threshold below which a convergence warning is emitted.
        /// </summary>
        private const double EssWarningThreshold = 400d;

        /// <summary>
        /// Builds the frequency results response for a univariate or Bulletin 17C analysis.
        /// </summary>
        /// <param name="resource">The analysis resource; its analysis must be estimated with results available.</param>
        /// <returns>The results response.</returns>
        /// <exception cref="ArgumentException">Thrown when the resource is a rating curve analysis.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the analysis has no results.</exception>
        public static FrequencyResultsResponse ToFrequencyResults(AnalysisResource resource)
        {
            ArgumentNullException.ThrowIfNull(resource);
            return resource.Kind switch
            {
                AnalysisKind.Univariate => ToUnivariateResults(resource),
                AnalysisKind.Bulletin17C => ToBulletin17CResults(resource),
                AnalysisKind.Mixture => ToMixtureResults(resource),
                AnalysisKind.PointProcess => ToPointProcessResults(resource),
                AnalysisKind.CompetingRisks => ToCompetingRisksResults(resource),
                AnalysisKind.Composite => ToCompositeResults(resource),
                _ => throw new ArgumentException($"Analysis kind '{resource.Kind}' does not produce frequency results.", nameof(resource))
            };
        }

        /// <summary>
        /// Builds the results response for a univariate MCMC analysis.
        /// </summary>
        /// <param name="resource">The univariate analysis resource.</param>
        /// <returns>The results response.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the analysis has no results.</exception>
        private static FrequencyResultsResponse ToUnivariateResults(AnalysisResource resource)
        {
            var analysis = resource.Univariate!;
            var distribution = analysis.UnivariateDistribution;
            return ToMcmcFrequencyResults(
                resource,
                analysis.AnalysisResults,
                analysis.BayesianAnalysis,
                analysis.ProbabilityOrdinates.ToList(),
                distribution.Parameters,
                EnumHelper.ToCamelCase(distribution.DistributionType.ToString()),
                MetadataMapper.ToDisplayName(distribution.DistributionType));
        }

        /// <summary>
        /// Builds the results response for a mixture-distribution MCMC analysis.
        /// </summary>
        /// <param name="resource">The mixture analysis resource.</param>
        /// <returns>The results response.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the analysis has no results.</exception>
        private static FrequencyResultsResponse ToMixtureResults(AnalysisResource resource)
        {
            var analysis = resource.Mixture!;
            var model = analysis.MixtureDistribution;
            string components = string.Join(", ", (model.Mixture?.Distributions ?? Array.Empty<UnivariateDistributionBase>())
                .Select(d => MetadataMapper.ToDisplayName(d.Type)));
            return ToMcmcFrequencyResults(
                resource,
                analysis.AnalysisResults,
                analysis.BayesianAnalysis,
                analysis.ProbabilityOrdinates.ToList(),
                model.Parameters,
                "mixture",
                components.Length > 0 ? $"Mixture of {components}" : "Mixture");
        }

        /// <summary>
        /// Builds the results response for a peaks-over-threshold point process MCMC analysis.
        /// </summary>
        /// <param name="resource">The point process analysis resource.</param>
        /// <returns>The results response.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the analysis has no results.</exception>
        private static FrequencyResultsResponse ToPointProcessResults(AnalysisResource resource)
        {
            var analysis = resource.PointProcess!;
            return ToMcmcFrequencyResults(
                resource,
                analysis.AnalysisResults,
                analysis.BayesianAnalysis,
                analysis.ProbabilityOrdinates.ToList(),
                analysis.PointProcess.Parameters,
                "pointProcess",
                "Point Process (GEV)");
        }

        /// <summary>
        /// Builds the results response for a competing risks MCMC analysis.
        /// </summary>
        /// <param name="resource">The competing risks analysis resource.</param>
        /// <returns>The results response.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the analysis has no results.</exception>
        private static FrequencyResultsResponse ToCompetingRisksResults(AnalysisResource resource)
        {
            var analysis = resource.CompetingRisks!;
            var model = analysis.CompetingRisksDistribution;
            string components = model.CompetingRisks is null
                ? string.Empty
                : string.Join(", ", model.CompetingRisks.Distributions.Select(d => MetadataMapper.ToDisplayName(d.Type)));
            return ToMcmcFrequencyResults(
                resource,
                analysis.AnalysisResults,
                analysis.BayesianAnalysis,
                analysis.ProbabilityOrdinates.ToList(),
                model.Parameters,
                "competingRisks",
                components.Length > 0 ? $"Competing Risks of {components}" : "Competing Risks");
        }

        /// <summary>
        /// Builds the results response for a composite analysis. Composites run no MCMC of their
        /// own — the response carries the aggregated curve, component weights (a first-class
        /// output for model averaging), and the fit criteria the aggregation produced; there is
        /// no single fitted distribution and no chain diagnostics.
        /// </summary>
        /// <param name="resource">The composite analysis resource.</param>
        /// <returns>The results response.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the analysis has no results.</exception>
        private static FrequencyResultsResponse ToCompositeResults(AnalysisResource resource)
        {
            var analysis = resource.Composite!;
            var results = analysis.AnalysisResults
                ?? throw new InvalidOperationException("The analysis has no results. Run it first (POST .../run).");

            var components = new List<CompositeComponentInfoDto>(analysis.Analyses.Count);
            for (int i = 0; i < analysis.Analyses.Count; i++)
            {
                var component = analysis.Analyses[i];
                Guid? componentId = resource.ComponentAnalysisIds != null && i < resource.ComponentAnalysisIds.Count
                    ? resource.ComponentAnalysisIds[i]
                    : null;
                var componentResource = resource.ComponentResources != null && i < resource.ComponentResources.Count
                    ? resource.ComponentResources[i]
                    : null;
                components.Add(new CompositeComponentInfoDto
                {
                    AnalysisId = componentId,
                    Name = componentResource?.Name,
                    Kind = componentResource == null ? null : EnumHelper.ToCamelCase(componentResource.Kind.ToString()),
                    Weight = component.Weight
                });
            }

            return new FrequencyResultsResponse
            {
                AnalysisId = resource.Id,
                Kind = EnumHelper.ToCamelCase(resource.Kind.ToString()),
                FittedDistribution = null,
                FrequencyCurve = BuildFrequencyCurve(results, analysis.ProbabilityOrdinates.ToList(), analysis.BayesianAnalysis.CredibleIntervalWidth),
                ParameterSummaries = new List<ParameterSummaryDto>(),
                InformationCriteria = BuildInformationCriteria(results, bayesianAnalysis: null),
                Diagnostics = new DiagnosticsDto(),
                Composite = new CompositeInfoDto
                {
                    CompositeType = EnumHelper.ToCamelCase(analysis.CompositeDistributionType.ToString()),
                    AverageMethod = analysis.CompositeDistributionType == RMC.BestFit.Analyses.CompositeType.ModelAverage
                        ? EnumHelper.ToCamelCase(analysis.ModelAverageMethod.ToString())
                        : null,
                    Dependency = analysis.CompositeDistributionType == RMC.BestFit.Analyses.CompositeType.CompetingRisks
                        ? EnumHelper.ToCamelCase(analysis.Dependency.ToString())
                        : null,
                    IsMaximum = analysis.CompositeDistributionType == RMC.BestFit.Analyses.CompositeType.CompetingRisks
                        ? analysis.IsMaximum
                        : null,
                    Components = components
                }
            };
        }

        /// <summary>
        /// Shared response assembly for the MCMC frequency-curve kinds (univariate, mixture,
        /// point process, competing risks): fitted parameters at the point estimate, the
        /// AEP-aligned curve, posterior summaries with chain diagnostics, and information criteria.
        /// </summary>
        /// <param name="resource">The analysis resource.</param>
        /// <param name="results">The analysis results; null means never run.</param>
        /// <param name="bayesian">The Bayesian analysis carrying the chain and settings.</param>
        /// <param name="probabilities">The exceedance-probability ordinates.</param>
        /// <param name="parameters">The model parameters in canonical order.</param>
        /// <param name="typeName">The wire name for the fitted distribution type.</param>
        /// <param name="displayName">The human-readable fitted distribution name.</param>
        /// <returns>The results response.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the analysis has no results.</exception>
        private static FrequencyResultsResponse ToMcmcFrequencyResults(
            AnalysisResource resource,
            UncertaintyAnalysisResults? results,
            BayesianAnalysis bayesian,
            IReadOnlyList<double> probabilities,
            IReadOnlyList<RMC.BestFit.Models.ModelParameter> parameters,
            string typeName,
            string displayName)
        {
            if (results == null)
            {
                throw new InvalidOperationException("The analysis has no results. Run it first (POST .../run).");
            }
            // DisplayName, not Name: stationary models blank the short name and keep the
            // user-facing identifier (the same one priors are matched against) in DisplayName.
            var parameterNames = GetSampledParameterNames(bayesian, parameters);

            return new FrequencyResultsResponse
            {
                AnalysisId = resource.Id,
                Kind = EnumHelper.ToCamelCase(resource.Kind.ToString()),
                FittedDistribution = new FittedDistributionDto
                {
                    Type = typeName,
                    DisplayName = displayName,
                    PointEstimator = EnumHelper.ToCamelCase(bayesian.PointEstimator.ToString()),
                    Parameters = parameters
                        .Select(p => new ParameterValueDto { Name = p.DisplayName, Value = p.Value })
                        .ToList()
                },
                FrequencyCurve = BuildFrequencyCurve(results, probabilities, bayesian.CredibleIntervalWidth),
                ParameterSummaries = BuildParameterSummaries(parameterNames, bayesian.Results, includeChainDiagnostics: true),
                InformationCriteria = BuildInformationCriteria(results, bayesian),
                Diagnostics = BuildMcmcDiagnostics(bayesian, parameterNames)
            };
        }

        /// <summary>
        /// Builds the results response for a Bulletin 17C analysis.
        /// </summary>
        /// <param name="resource">The Bulletin 17C analysis resource.</param>
        /// <returns>The results response.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the analysis has no results.</exception>
        private static FrequencyResultsResponse ToBulletin17CResults(AnalysisResource resource)
        {
            var analysis = resource.Bulletin17C!;
            var results = analysis.AnalysisResults
                ?? throw new InvalidOperationException("The analysis has no results. Run it first (POST .../run).");
            var distribution = analysis.Bulletin17CDistribution;
            var parameterNames = distribution.Parameters.Select(p => p.Name).ToList();

            return new FrequencyResultsResponse
            {
                AnalysisId = resource.Id,
                Kind = EnumHelper.ToCamelCase(resource.Kind.ToString()),
                FittedDistribution = new FittedDistributionDto
                {
                    Type = EnumHelper.ToCamelCase(distribution.DistributionType.ToString()),
                    DisplayName = MetadataMapper.ToDisplayName(distribution.DistributionType),
                    PointEstimator = "gmm",
                    Parameters = distribution.Parameters
                        .Select(p => new ParameterValueDto { Name = p.Name, Value = p.Value })
                        .ToList()
                },
                FrequencyCurve = BuildFrequencyCurve(results, analysis.ProbabilityOrdinates.ToList(), analysis.BayesianAnalysis.CredibleIntervalWidth),
                ParameterSummaries = BuildParameterSummaries(parameterNames, analysis.BayesianAnalysis.Results, includeChainDiagnostics: false),
                InformationCriteria = BuildInformationCriteria(results, bayesianAnalysis: null),
                Diagnostics = new DiagnosticsDto
                {
                    ElapsedMs = (long?)analysis.ElapsedTime?.TotalMilliseconds
                },
                Bulletin17C = new Bulletin17CInfoDto
                {
                    UncertaintyMethod = EnumHelper.ToCamelCase(analysis.UncertaintyMethod.ToString()),
                    GmmElapsedMs = (long?)analysis.GMMElapsedTime?.TotalMilliseconds,
                    UncertaintyElapsedMs = (long?)analysis.UncertaintyElapsedTime?.TotalMilliseconds
                }
            };
        }

        /// <summary>
        /// Builds the results response for a rating curve analysis.
        /// </summary>
        /// <param name="resource">The rating curve analysis resource.</param>
        /// <returns>The results response.</returns>
        /// <exception cref="ArgumentException">Thrown when the resource is not a rating curve analysis.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the analysis has no results.</exception>
        public static RatingCurveResultsResponse ToRatingCurveResults(AnalysisResource resource)
        {
            ArgumentNullException.ThrowIfNull(resource);
            if (resource.Kind != AnalysisKind.RatingCurve)
            {
                throw new ArgumentException($"Analysis kind '{resource.Kind}' does not produce rating curve results.", nameof(resource));
            }

            var analysis = resource.RatingCurve!;
            var results = analysis.AnalysisResults
                ?? throw new InvalidOperationException("The analysis has no results. Run it first (POST .../run).");
            var model = analysis.RatingCurve;
            var parameterNames = model.Parameters.Select(p => p.Name).ToList();

            return new RatingCurveResultsResponse
            {
                AnalysisId = resource.Id,
                Kind = EnumHelper.ToCamelCase(resource.Kind.ToString()),
                RatingCurve = BuildRatingCurve(results, analysis.BayesianAnalysis.CredibleIntervalWidth, analysis.MinStage, analysis.MaxStage, analysis.StageBins),
                NumberOfSegments = model.NumberOfSegments,
                AlignedObservationCount = model.GetAlignedObservations().Count,
                Parameters = model.Parameters
                    .Select(p => new ParameterValueDto { Name = p.Name, Value = p.Value })
                    .ToList(),
                ParameterSummaries = BuildParameterSummaries(parameterNames, analysis.BayesianAnalysis.Results, includeChainDiagnostics: true),
                InformationCriteria = BuildInformationCriteria(results, analysis.BayesianAnalysis),
                Diagnostics = BuildMcmcDiagnostics(analysis.BayesianAnalysis, parameterNames)
            };
        }

        /// <summary>
        /// Builds a frequency curve from an uncertainty results object whose confidence intervals
        /// have the <c>[p, 2]</c> (lower, upper) shape.
        /// </summary>
        /// <param name="results">The uncertainty analysis results.</param>
        /// <param name="probabilities">The exceedance probabilities the curves are aligned to.</param>
        /// <param name="credibleIntervalWidth">The credible interval width (e.g., 0.90).</param>
        /// <returns>The frequency curve DTO.</returns>
        public static FrequencyCurveDto BuildFrequencyCurve(UncertaintyAnalysisResults results, IReadOnlyList<double> probabilities, double credibleIntervalWidth)
        {
            ArgumentNullException.ThrowIfNull(results);
            ArgumentNullException.ThrowIfNull(probabilities);

            var curve = new FrequencyCurveDto
            {
                Probabilities = probabilities.ToList(),
                ModeCurve = results.ModeCurve?.ToList(),
                MeanCurve = results.MeanCurve?.ToList(),
                CredibleIntervalWidth = credibleIntervalWidth
            };

            if (results.ConfidenceIntervals is { } intervals && intervals.GetLength(1) >= 2)
            {
                // [p, 2]: column 0 = lower, column 1 = upper.
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

            return curve;
        }

        /// <summary>
        /// Builds a rating curve from an uncertainty results object whose confidence intervals
        /// have the <c>[n, 3]</c> (stage bin, lower, upper) shape.
        /// </summary>
        /// <param name="results">The uncertainty analysis results.</param>
        /// <param name="credibleIntervalWidth">The credible interval width (e.g., 0.90).</param>
        /// <param name="minStage">The configured minimum stage of the grid.</param>
        /// <param name="maxStage">The configured maximum stage of the grid.</param>
        /// <param name="stageBins">The configured number of stage bins.</param>
        /// <returns>The rating curve DTO.</returns>
        public static RatingCurveDto BuildRatingCurve(UncertaintyAnalysisResults results, double credibleIntervalWidth, double? minStage = null, double? maxStage = null, int? stageBins = null)
        {
            ArgumentNullException.ThrowIfNull(results);

            var curve = new RatingCurveDto
            {
                ModeCurve = results.ModeCurve?.ToList(),
                MeanCurve = results.MeanCurve?.ToList(),
                CredibleIntervalWidth = credibleIntervalWidth,
                MinStage = minStage,
                MaxStage = maxStage,
                StageBins = stageBins
            };

            if (results.ConfidenceIntervals is { } intervals && intervals.GetLength(1) >= 3)
            {
                // [n, 3]: column 0 = stage bin, column 1 = lower, column 2 = upper.
                int rows = intervals.GetLength(0);
                var stages = new List<double>(rows);
                var lower = new List<double>(rows);
                var upper = new List<double>(rows);
                for (int i = 0; i < rows; i++)
                {
                    stages.Add(intervals[i, 0]);
                    lower.Add(intervals[i, 1]);
                    upper.Add(intervals[i, 2]);
                }
                curve.Stages = stages;
                curve.CiLower = lower;
                curve.CiUpper = upper;
            }

            return curve;
        }

        /// <summary>
        /// Builds per-parameter posterior summaries from MCMC results, pairing names with
        /// summary statistics by index.
        /// </summary>
        /// <param name="parameterNames">The model parameter names, in model order.</param>
        /// <param name="results">The MCMC (or sampled parameter set) results; null yields an empty list.</param>
        /// <param name="includeChainDiagnostics">True to report R-hat/ESS; false (Bulletin 17C sampled sets) reports them as null.</param>
        /// <returns>The parameter summaries.</returns>
        public static List<ParameterSummaryDto> BuildParameterSummaries(IReadOnlyList<string> parameterNames, MCMCResults? results, bool includeChainDiagnostics)
        {
            ArgumentNullException.ThrowIfNull(parameterNames);
            var summaries = new List<ParameterSummaryDto>();
            if (results?.ParameterResults == null) return summaries;

            for (int i = 0; i < results.ParameterResults.Length; i++)
            {
                var statistics = results.ParameterResults[i].SummaryStatistics;
                summaries.Add(new ParameterSummaryDto
                {
                    Name = i < parameterNames.Count ? parameterNames[i] : $"parameter{i}",
                    Mean = NanToNull(statistics.Mean),
                    Median = NanToNull(statistics.Median),
                    StandardDeviation = NanToNull(statistics.StandardDeviation),
                    LowerCI = NanToNull(statistics.LowerCI),
                    UpperCI = NanToNull(statistics.UpperCI),
                    Rhat = includeChainDiagnostics ? NanToNull(statistics.Rhat) : null,
                    Ess = includeChainDiagnostics ? NanToNull(statistics.ESS) : null
                });
            }
            return summaries;
        }

        /// <summary>
        /// Gets names aligned with the coordinates stored in an MCMC result.
        /// </summary>
        /// <param name="bayesian">The Bayesian analysis that owns the results.</param>
        /// <param name="parameters">The public model parameters.</param>
        /// <returns>Sampled-coordinate names, omitting the derived final mixture weight for new K-1 results.</returns>
        private static List<string> GetSampledParameterNames(
            BayesianAnalysis bayesian,
            IReadOnlyList<RMC.BestFit.Models.ModelParameter> parameters)
        {
            var names = parameters.Select(parameter => parameter.DisplayName).ToList();
            if (bayesian.Model is MixtureModel mixtureModel &&
                mixtureModel.Mixture is not null &&
                mixtureModel.Mixture.Distributions.Length > 1 &&
                bayesian.Results?.ParameterResults?.Length == names.Count - 1)
            {
                names.RemoveAt(mixtureModel.Mixture.Distributions.Length - 1);
            }
            return names;
        }

        /// <summary>
        /// Scans MCMC parameter results for convergence concerns: R-hat above
        /// <see cref="RhatWarningThreshold"/> or effective sample size below
        /// <see cref="EssWarningThreshold"/>. Warnings never fail a run.
        /// </summary>
        /// <param name="results">The MCMC results; null yields no warnings.</param>
        /// <param name="parameterNames">The model parameter names, in model order.</param>
        /// <returns>The warning messages, empty when no concerns were detected.</returns>
        public static List<string> BuildConvergenceWarnings(MCMCResults? results, IReadOnlyList<string> parameterNames)
        {
            ArgumentNullException.ThrowIfNull(parameterNames);
            var warnings = new List<string>();
            if (results?.ParameterResults == null) return warnings;

            for (int i = 0; i < results.ParameterResults.Length; i++)
            {
                var statistics = results.ParameterResults[i].SummaryStatistics;
                string name = i < parameterNames.Count ? parameterNames[i] : $"parameter{i}";
                if (!double.IsNaN(statistics.Rhat) && statistics.Rhat > RhatWarningThreshold)
                {
                    warnings.Add($"R-hat for '{name}' is {statistics.Rhat:F3} (> {RhatWarningThreshold}); the chains may not have converged. Consider more iterations or warm-up.");
                }
                if (!double.IsNaN(statistics.ESS) && statistics.ESS < EssWarningThreshold)
                {
                    warnings.Add($"Effective sample size for '{name}' is {statistics.ESS:F0} (< {EssWarningThreshold}); posterior summaries may be noisy. Consider more iterations or less thinning.");
                }
            }
            return warnings;
        }

        /// <summary>
        /// Builds the diagnostics block for an MCMC-based analysis.
        /// </summary>
        /// <param name="bayesianAnalysis">The Bayesian analysis carrying sampler settings and results.</param>
        /// <param name="parameterNames">The model parameter names for warning messages.</param>
        /// <returns>The diagnostics DTO.</returns>
        public static DiagnosticsDto BuildMcmcDiagnostics(BayesianAnalysis bayesianAnalysis, IReadOnlyList<string> parameterNames)
        {
            return new DiagnosticsDto
            {
                Sampler = EnumHelper.ToCamelCase(bayesianAnalysis.Type.ToString()),
                Iterations = bayesianAnalysis.Iterations,
                WarmupIterations = bayesianAnalysis.WarmupIterations,
                NumberOfChains = bayesianAnalysis.NumberOfChains,
                AcceptanceRates = bayesianAnalysis.Results?.AcceptanceRates?.ToList(),
                ConvergenceWarnings = BuildConvergenceWarnings(bayesianAnalysis.Results, parameterNames),
                ElapsedMs = (long?)bayesianAnalysis.ElapsedTime?.TotalMilliseconds
            };
        }

        /// <summary>
        /// Builds the information-criteria block, merging the fit criteria on the results object
        /// with the MCMC-only criteria on the Bayesian analysis (when applicable).
        /// </summary>
        /// <param name="results">The uncertainty analysis results carrying AIC/BIC/DIC/RMSE/ERL.</param>
        /// <param name="bayesianAnalysis">The Bayesian analysis carrying WAIC/LOOIC, or null for non-MCMC fits.</param>
        /// <returns>The information criteria DTO.</returns>
        public static InformationCriteriaDto BuildInformationCriteria(UncertaintyAnalysisResults results, BayesianAnalysis? bayesianAnalysis)
        {
            return new InformationCriteriaDto
            {
                Aic = NanToNull(results.AIC),
                Bic = NanToNull(results.BIC),
                Dic = NanToNull(results.DIC),
                Rmse = NanToNull(results.RMSE),
                Erl = NanToNull(results.ERL),
                Waic = bayesianAnalysis == null ? null : NanToNull(bayesianAnalysis.WAIC),
                WaicPD = bayesianAnalysis == null ? null : NanToNull(bayesianAnalysis.WAIC_pD),
                Looic = bayesianAnalysis == null ? null : NanToNull(bayesianAnalysis.LOOIC),
                LooicSE = bayesianAnalysis == null ? null : NanToNull(bayesianAnalysis.LOOIC_SE)
            };
        }

        /// <summary>
        /// Converts NaN to null so diagnostics that do not apply are omitted from the JSON rather
        /// than serialized as named floating-point literals.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>Null when NaN; otherwise the value.</returns>
        private static double? NanToNull(double value)
        {
            return double.IsNaN(value) ? null : value;
        }
    }
}
