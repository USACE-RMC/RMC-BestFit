using System.ComponentModel;
using ModelContextProtocol.Server;
using Numerics.Distributions;
using RMC.BestFit.Analyses;
using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Helpers;
using RMC.BestFit.Api.Mappers;
using RMC.BestFit.Api.Services;
using RMC.BestFit.Api.Store;
using RMC.BestFit.Estimation;

namespace RMC.BestFit.Api.Mcp
{
    /// <summary>
    /// MCP tools for creating, running, and inspecting analyses. Run tools are synchronous and
    /// may take seconds to about a minute for MCMC analyses.
    /// </summary>
    [McpServerToolType]
    public class AnalysisTools
    {
        /// <summary>
        /// The analysis service shared with the REST controllers.
        /// </summary>
        private readonly IAnalysisService _service;

        /// <summary>
        /// Constructs the tools with their service dependency.
        /// </summary>
        /// <param name="service">The analysis service.</param>
        public AnalysisTools(IAnalysisService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        /// <summary>
        /// Creates a univariate Bayesian MCMC frequency analysis.
        /// </summary>
        /// <param name="inputDataId">The input-data resource id.</param>
        /// <param name="distribution">The distribution name.</param>
        /// <param name="probabilityOrdinates">Optional AEP ordinates.</param>
        /// <param name="iterations">Optional MCMC iterations.</param>
        /// <param name="warmupIterations">Optional warm-up iterations.</param>
        /// <param name="numberOfChains">Optional chain count.</param>
        /// <param name="prngSeed">Optional PRNG seed for reproducibility.</param>
        /// <param name="credibleIntervalWidth">Optional credible interval width.</param>
        /// <param name="sampler">Optional sampler name.</param>
        /// <param name="pointEstimator">Optional point estimator name.</param>
        /// <param name="parameterPriors">Optional informative parameter priors.</param>
        /// <param name="quantilePriors">Optional quantile priors.</param>
        /// <param name="useSingleQuantile">Optional single-quantile-prior formulation flag.</param>
        /// <param name="name">Optional display name.</param>
        /// <returns>JSON with the created analysis summary including its id.</returns>
        [McpServerTool(Name = "create_univariate_analysis")]
        [Description("Create a Bayesian MCMC univariate frequency analysis over an input-data resource (the data is cloned at creation). Returns the analysis id to pass to run_analysis. Probability ordinates are annual exceedance probabilities (AEP, 0-1); omit for the 25 defaults. Omit MCMC settings for data-scaled defaults. Supports informative parameter priors and quantile priors (engineering judgment).")]
        public string CreateUnivariateAnalysis(
            [Description("Id of the input-data resource to fit.")] Guid inputDataId,
            [Description("Distribution: logPearsonTypeIII (default), generalizedExtremeValue, gumbel, lnNormal, weibull, ... (see get_metadata).")] string? distribution = null,
            [Description("Optional AEP ordinates, each strictly between 0 and 1 (e.g., 0.01 = 100-year event). Sorted automatically.")] double[]? probabilityOrdinates = null,
            [Description("Optional total MCMC iterations per chain (server-capped).")] int? iterations = null,
            [Description("Optional warm-up iterations (must be below iterations).")] int? warmupIterations = null,
            [Description("Optional number of parallel chains (1-8).")] int? numberOfChains = null,
            [Description("Optional PRNG seed for reproducible runs.")] int? prngSeed = null,
            [Description("Optional credible interval width, e.g. 0.90.")] double? credibleIntervalWidth = null,
            [Description("Optional sampler: demCzs (default), demCz, arwmh, or nuts.")] string? sampler = null,
            [Description("Optional point estimator: posteriorMode or posteriorMean.")] string? pointEstimator = null,
            [Description("Optional informative parameter priors: array of { parameterName, distribution: { type, parameters }, isFixed? }. Parameter names per distribution come from get_metadata. Unnamed parameters keep flat priors.")] List<ParameterPriorDto>? parameterPriors = null,
            [Description("Optional quantile priors (engineering judgment about flood magnitudes): array of { alpha (AEP), distribution: { type, parameters } }. Supply one per distribution parameter, or one total with useSingleQuantile=true.")] List<QuantilePriorDto>? quantilePriors = null,
            [Description("True for the single-quantile-prior formulation (Viglione et al. 2013); false/omit for one prior per parameter (Coles and Tawn 1996).")] bool? useSingleQuantile = null,
            [Description("Optional display name for the analysis.")] string? name = null)
        {
            var resource = _service.CreateUnivariate(new CreateUnivariateAnalysisRequest
            {
                InputDataId = inputDataId,
                Distribution = EnumHelper.ParseOrDefault(distribution, UnivariateDistributionType.LogPearsonTypeIII),
                ProbabilityOrdinates = probabilityOrdinates?.ToList(),
                BayesianOptions = BuildBayesianOptions(iterations, warmupIterations, numberOfChains, prngSeed, credibleIntervalWidth, sampler, pointEstimator),
                ParameterPriors = parameterPriors,
                QuantilePriors = quantilePriors,
                UseSingleQuantile = useSingleQuantile,
                Name = name
            });
            return McpJson.Serialize(AnalysisMapper.ToResourceResponse(resource));
        }

        /// <summary>
        /// Creates a Bulletin 17C flood frequency analysis.
        /// </summary>
        /// <param name="inputDataId">The input-data resource id.</param>
        /// <param name="distribution">The distribution name (Bulletin 17C set).</param>
        /// <param name="uncertaintyMethod">Optional uncertainty method name.</param>
        /// <param name="probabilityOrdinates">Optional AEP ordinates.</param>
        /// <param name="parameterPenalties">Optional parameter penalties (e.g., regional skew).</param>
        /// <param name="quantilePenalties">Optional quantile penalties.</param>
        /// <param name="name">Optional display name.</param>
        /// <returns>JSON with the created analysis summary including its id.</returns>
        [McpServerTool(Name = "create_bulletin17c_analysis")]
        [Description("Create a Bulletin 17C flood frequency analysis (USGS guidelines; Expected Moments Algorithm with parametric/bootstrap uncertainty) over an input-data resource — typically USGS annual peaks. Supports regional-skew parameter penalties and quantile penalties. Returns the analysis id to pass to run_analysis.")]
        public string CreateBulletin17CAnalysis(
            [Description("Id of the input-data resource to fit.")] Guid inputDataId,
            [Description("Distribution: logPearsonTypeIII (default), pearsonTypeIII, logNormal, normal, gammaDistribution, or exponential.")] string? distribution = null,
            [Description("Uncertainty method: linkedMultivariateNormal (default), multivariateNormal, bootstrap, or biasCorrectedBootstrap.")] string? uncertaintyMethod = null,
            [Description("Optional AEP ordinates, each strictly between 0 and 1. Sorted automatically.")] double[]? probabilityOrdinates = null,
            [Description("Optional parameter penalties: array of { parameterName, mean, mse, useLog? }. Canonical use: regional skew — { parameterName: 'Skew (of log)', mean: regionalSkew, mse: regionalSkewMSE }.")] List<ParameterPenaltyDto>? parameterPenalties = null,
            [Description("Optional quantile penalties: array of { aep, mean, mse, useLog10? } — mean/mse in log10 units when useLog10 (default true).")] List<QuantilePenaltyDto>? quantilePenalties = null,
            [Description("Optional display name for the analysis.")] string? name = null)
        {
            var resource = _service.CreateBulletin17C(new CreateBulletin17CAnalysisRequest
            {
                InputDataId = inputDataId,
                Distribution = EnumHelper.ParseOrDefault(distribution, UnivariateDistributionType.LogPearsonTypeIII),
                UncertaintyMethod = EnumHelper.ParseOrNull<UncertaintyMethod>(uncertaintyMethod),
                ProbabilityOrdinates = probabilityOrdinates?.ToList(),
                ParameterPenalties = parameterPenalties,
                QuantilePenalties = quantilePenalties,
                Name = name
            });
            return McpJson.Serialize(AnalysisMapper.ToResourceResponse(resource));
        }

        /// <summary>
        /// Creates a Bayesian rating curve analysis.
        /// </summary>
        /// <param name="stageTimeSeriesId">The stage time-series resource id.</param>
        /// <param name="dischargeTimeSeriesId">The discharge time-series resource id.</param>
        /// <param name="numberOfSegments">The number of power-law segments (1-3).</param>
        /// <param name="minStage">Optional minimum stage of the output grid.</param>
        /// <param name="maxStage">Optional maximum stage of the output grid.</param>
        /// <param name="stageBins">Optional number of stage grid points.</param>
        /// <param name="prngSeed">Optional PRNG seed for reproducibility.</param>
        /// <param name="parameterPriors">Optional informative parameter priors.</param>
        /// <param name="name">Optional display name.</param>
        /// <returns>JSON with the created analysis summary including its id.</returns>
        [McpServerTool(Name = "create_ratingcurve_analysis")]
        [Description("Create a Bayesian stage-discharge rating curve analysis over two time-series resources (typically USGS measuredStage and measuredDischarge for the same site). The series are date-aligned; at least 10 common dates are required. Returns the analysis id to pass to run_analysis.")]
        public string CreateRatingCurveAnalysis(
            [Description("Id of the stage (gage height) time-series resource.")] Guid stageTimeSeriesId,
            [Description("Id of the discharge time-series resource.")] Guid dischargeTimeSeriesId,
            [Description("Number of piecewise power-law segments (1-3). Default 1.")] int numberOfSegments = 1,
            [Description("Optional minimum stage of the output grid (supply with maxStage).")] double? minStage = null,
            [Description("Optional maximum stage of the output grid (supply with minStage).")] double? maxStage = null,
            [Description("Optional number of stage grid points (2-1000).")] int? stageBins = null,
            [Description("Optional PRNG seed for reproducible runs.")] int? prngSeed = null,
            [Description("Optional informative parameter priors: array of { parameterName, distribution: { type, parameters }, isFixed? } (e.g., a prior on the offset from a surveyed gage datum).")] List<ParameterPriorDto>? parameterPriors = null,
            [Description("Optional display name for the analysis.")] string? name = null)
        {
            var resource = _service.CreateRatingCurve(new CreateRatingCurveAnalysisRequest
            {
                StageTimeSeriesId = stageTimeSeriesId,
                DischargeTimeSeriesId = dischargeTimeSeriesId,
                NumberOfSegments = numberOfSegments,
                MinStage = minStage,
                MaxStage = maxStage,
                StageBins = stageBins,
                BayesianOptions = prngSeed.HasValue ? new BayesianOptionsDto { PrngSeed = prngSeed } : null,
                ParameterPriors = parameterPriors,
                Name = name
            });
            return McpJson.Serialize(AnalysisMapper.ToResourceResponse(resource));
        }

        /// <summary>
        /// Runs an analysis synchronously and returns its results.
        /// </summary>
        /// <param name="analysisId">The analysis id.</param>
        /// <param name="cancellationToken">Cancellation token supplied by the MCP host.</param>
        /// <returns>JSON with the frequency or rating curve results depending on the analysis kind.</returns>
        [McpServerTool(Name = "run_analysis")]
        [Description("Run an analysis synchronously and return its results (frequency curve for univariate/Bulletin 17C, stage-discharge curve for rating curves). MCMC runs typically take seconds to about a minute. Rerunning replaces prior results.")]
        public async Task<string> RunAnalysis(
            [Description("The analysis id from a create_*_analysis tool.")] Guid analysisId,
            CancellationToken cancellationToken = default)
        {
            var resource = _service.Get(analysisId);
            if (resource.Kind == AnalysisKind.RatingCurve)
            {
                return McpJson.Serialize(await _service.RunRatingCurveAsync(analysisId, cancellationToken));
            }
            if (resource.Kind == AnalysisKind.DistributionFitting)
            {
                return McpJson.Serialize(await _service.RunDistributionFittingAsync(analysisId, cancellationToken));
            }
            if (resource.Kind == AnalysisKind.Bivariate)
            {
                return McpJson.Serialize(await _service.RunBivariateAsync(analysisId, cancellationToken));
            }
            if (resource.Kind == AnalysisKind.CoincidentFrequency)
            {
                return McpJson.Serialize(await _service.RunCoincidentFrequencyAsync(analysisId, cancellationToken));
            }
            if (resource.Kind == AnalysisKind.TimeSeries)
            {
                return McpJson.Serialize(await _service.RunTimeSeriesAsync(analysisId, cancellationToken));
            }
            return McpJson.Serialize(await _service.RunFrequencyAsync(analysisId, expectedKind: null, cancellationToken));
        }

        /// <summary>
        /// Returns the stored results of a previously run analysis.
        /// </summary>
        /// <param name="analysisId">The analysis id.</param>
        /// <returns>JSON with the stored results.</returns>
        [McpServerTool(Name = "get_analysis_results")]
        [Description("Get the stored results of a previously run analysis (no recomputation). Fails if the analysis has never been run — call run_analysis first.")]
        public string GetAnalysisResults(
            [Description("The analysis id.")] Guid analysisId)
        {
            var resource = _service.Get(analysisId);
            if (resource.Kind == AnalysisKind.RatingCurve)
            {
                return McpJson.Serialize(_service.GetRatingCurveResults(analysisId));
            }
            if (resource.Kind == AnalysisKind.DistributionFitting)
            {
                return McpJson.Serialize(_service.GetDistributionFittingResults(analysisId));
            }
            if (resource.Kind == AnalysisKind.Bivariate)
            {
                return McpJson.Serialize(_service.GetBivariateResults(analysisId));
            }
            if (resource.Kind == AnalysisKind.CoincidentFrequency)
            {
                return McpJson.Serialize(_service.GetCoincidentFrequencyResults(analysisId));
            }
            if (resource.Kind == AnalysisKind.TimeSeries)
            {
                return McpJson.Serialize(_service.GetTimeSeriesResults(analysisId));
            }
            return McpJson.Serialize(_service.GetFrequencyResults(analysisId));
        }

        /// <summary>
        /// Validates an analysis configuration without running it.
        /// </summary>
        /// <param name="analysisId">The analysis id.</param>
        /// <returns>JSON with the validation verdict.</returns>
        [McpServerTool(Name = "validate_analysis")]
        [Description("Validate an analysis configuration without running it (e.g., check a rating curve has at least 10 date-aligned observation pairs).")]
        public string ValidateAnalysis(
            [Description("The analysis id.")] Guid analysisId)
        {
            return McpJson.Serialize(_service.Validate(analysisId));
        }

        /// <summary>
        /// Composes a Bayesian options DTO from the flat tool parameters, or returns null when
        /// none were supplied. Shared with <see cref="AdvancedAnalysisTools"/>.
        /// </summary>
        /// <param name="iterations">Optional MCMC iterations.</param>
        /// <param name="warmupIterations">Optional warm-up iterations.</param>
        /// <param name="numberOfChains">Optional chain count.</param>
        /// <param name="prngSeed">Optional PRNG seed.</param>
        /// <param name="credibleIntervalWidth">Optional credible interval width.</param>
        /// <param name="sampler">Optional sampler name.</param>
        /// <param name="pointEstimator">Optional point estimator name.</param>
        /// <returns>The options DTO, or null.</returns>
        internal static BayesianOptionsDto? BuildBayesianOptions(
            int? iterations, int? warmupIterations, int? numberOfChains, int? prngSeed,
            double? credibleIntervalWidth, string? sampler, string? pointEstimator)
        {
            var samplerValue = EnumHelper.ParseOrNull<BayesianAnalysis.SamplerType>(sampler);
            var estimatorValue = EnumHelper.ParseOrNull<BayesianAnalysis.PointEstimateType>(pointEstimator);
            if (iterations == null && warmupIterations == null && numberOfChains == null && prngSeed == null &&
                credibleIntervalWidth == null && samplerValue == null && estimatorValue == null)
            {
                return null;
            }
            return new BayesianOptionsDto
            {
                Iterations = iterations,
                WarmupIterations = warmupIterations,
                NumberOfChains = numberOfChains,
                PrngSeed = prngSeed,
                CredibleIntervalWidth = credibleIntervalWidth,
                Sampler = samplerValue,
                PointEstimator = estimatorValue
            };
        }
    }
}
