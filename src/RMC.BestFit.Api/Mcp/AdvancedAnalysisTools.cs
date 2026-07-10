using System.ComponentModel;
using ModelContextProtocol.Server;
using Numerics.Data;
using Numerics.Distributions;
using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Helpers;
using RMC.BestFit.Api.Mappers;
using RMC.BestFit.Api.Services;

namespace RMC.BestFit.Api.Mcp
{
    /// <summary>
    /// MCP tools for creating the advanced analysis kinds (mixture, point process, competing
    /// risks, and later composite/bivariate/time-series kinds). Created analyses are run with the
    /// shared run_analysis tool.
    /// </summary>
    [McpServerToolType]
    public class AdvancedAnalysisTools
    {
        /// <summary>
        /// The analysis service shared with the REST controllers.
        /// </summary>
        private readonly IAnalysisService _service;

        /// <summary>
        /// Constructs the tools with their service dependency.
        /// </summary>
        /// <param name="service">The analysis service.</param>
        /// <exception cref="ArgumentNullException">Thrown when the service is null.</exception>
        public AdvancedAnalysisTools(IAnalysisService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        /// <summary>
        /// Creates a mixture-distribution frequency analysis.
        /// </summary>
        /// <param name="inputDataId">The input-data resource id.</param>
        /// <param name="distributions">The 1-3 component distribution names.</param>
        /// <param name="isZeroInflated">True to model a point mass at zero.</param>
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
        [McpServerTool(Name = "create_mixture_analysis")]
        [Description("Create a Bayesian MCMC mixture-distribution frequency analysis over an input-data resource — models samples from multiple flood-generating populations as a weighted combination of 1-3 component distributions with estimated weights. Returns the analysis id to pass to run_analysis.")]
        public string CreateMixtureAnalysis(
            [Description("Id of the input-data resource to fit.")] Guid inputDataId,
            [Description("1-3 component distributions, e.g. ['gumbel','logNormal'] (see get_metadata for names).")] string[] distributions,
            [Description("True to add a point mass at zero for zero-flow years. Default false.")] bool isZeroInflated = false,
            [Description("Optional AEP ordinates, each strictly between 0 and 1. Sorted automatically.")] double[]? probabilityOrdinates = null,
            [Description("Optional total MCMC iterations per chain (server-capped).")] int? iterations = null,
            [Description("Optional warm-up iterations (must be below iterations).")] int? warmupIterations = null,
            [Description("Optional number of parallel chains (1-8).")] int? numberOfChains = null,
            [Description("Optional PRNG seed for reproducible runs.")] int? prngSeed = null,
            [Description("Optional credible interval width, e.g. 0.90.")] double? credibleIntervalWidth = null,
            [Description("Optional sampler: demCzs (default), demCz, arwmh, or nuts.")] string? sampler = null,
            [Description("Optional point estimator: posteriorMode or posteriorMean.")] string? pointEstimator = null,
            [Description("Optional informative parameter priors: array of { parameterName, distribution: { type, parameters }, isFixed? }.")] List<ParameterPriorDto>? parameterPriors = null,
            [Description("Optional quantile priors: array of { alpha (AEP), distribution: { type, parameters } }.")] List<QuantilePriorDto>? quantilePriors = null,
            [Description("True for the single-quantile-prior formulation (Viglione et al. 2013).")] bool? useSingleQuantile = null,
            [Description("Optional display name for the analysis.")] string? name = null)
        {
            var resource = _service.CreateMixture(new CreateMixtureAnalysisRequest
            {
                InputDataId = inputDataId,
                Distributions = ParseDistributions(distributions),
                IsZeroInflated = isZeroInflated,
                ProbabilityOrdinates = probabilityOrdinates?.ToList(),
                BayesianOptions = AnalysisTools.BuildBayesianOptions(iterations, warmupIterations, numberOfChains, prngSeed, credibleIntervalWidth, sampler, pointEstimator),
                ParameterPriors = parameterPriors,
                QuantilePriors = quantilePriors,
                UseSingleQuantile = useSingleQuantile,
                Name = name
            });
            return McpJson.Serialize(AnalysisMapper.ToResourceResponse(resource));
        }

        /// <summary>
        /// Creates a peaks-over-threshold point process analysis.
        /// </summary>
        /// <param name="inputDataId">The POT input-data resource id.</param>
        /// <param name="isSeasonal">True to model seasonal event rates.</param>
        /// <param name="timeBlock">Optional seasonal time-block window name.</param>
        /// <param name="startMonth">Optional season start month (1-12).</param>
        /// <param name="threshold">Optional explicit threshold override.</param>
        /// <param name="totalYears">Optional explicit record span in years.</param>
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
        [McpServerTool(Name = "create_pointprocess_analysis")]
        [Description("Create a Bayesian MCMC peaks-over-threshold point process analysis over a POT input-data resource (create it with create_inputdata_pot). Jointly models event rate and magnitude with a GEV formulation; the POT resource's threshold seeds the model unless overridden. Returns the analysis id to pass to run_analysis.")]
        public string CreatePointProcessAnalysis(
            [Description("Id of the peaks-over-threshold input-data resource to fit.")] Guid inputDataId,
            [Description("True to model within-year seasonality of the event rate. Default false.")] bool isSeasonal = false,
            [Description("Seasonal time-block window (waterYear, calendarYear, ...); only with isSeasonal=true.")] string? timeBlock = null,
            [Description("Season start month 1-12; only with isSeasonal=true.")] int? startMonth = null,
            [Description("Optional explicit threshold; omit to use the POT resource's recorded threshold.")] double? threshold = null,
            [Description("Optional explicit record span in years (> 0) for the event rate λ = events/totalYears.")] double? totalYears = null,
            [Description("Optional AEP ordinates, each strictly between 0 and 1. Sorted automatically.")] double[]? probabilityOrdinates = null,
            [Description("Optional total MCMC iterations per chain (server-capped).")] int? iterations = null,
            [Description("Optional warm-up iterations (must be below iterations).")] int? warmupIterations = null,
            [Description("Optional number of parallel chains (1-8).")] int? numberOfChains = null,
            [Description("Optional PRNG seed for reproducible runs.")] int? prngSeed = null,
            [Description("Optional credible interval width, e.g. 0.90.")] double? credibleIntervalWidth = null,
            [Description("Optional sampler: demCzs (default), demCz, arwmh, or nuts.")] string? sampler = null,
            [Description("Optional point estimator: posteriorMode or posteriorMean.")] string? pointEstimator = null,
            [Description("Optional informative parameter priors: array of { parameterName, distribution: { type, parameters }, isFixed? }.")] List<ParameterPriorDto>? parameterPriors = null,
            [Description("Optional quantile priors: array of { alpha (AEP), distribution: { type, parameters } }.")] List<QuantilePriorDto>? quantilePriors = null,
            [Description("True for the single-quantile-prior formulation (Viglione et al. 2013).")] bool? useSingleQuantile = null,
            [Description("Optional display name for the analysis.")] string? name = null)
        {
            var resource = _service.CreatePointProcess(new CreatePointProcessAnalysisRequest
            {
                InputDataId = inputDataId,
                IsSeasonal = isSeasonal,
                TimeBlock = EnumHelper.ParseOrNull<TimeBlockWindow>(timeBlock),
                StartMonth = startMonth,
                Threshold = threshold,
                TotalYears = totalYears,
                ProbabilityOrdinates = probabilityOrdinates?.ToList(),
                BayesianOptions = AnalysisTools.BuildBayesianOptions(iterations, warmupIterations, numberOfChains, prngSeed, credibleIntervalWidth, sampler, pointEstimator),
                ParameterPriors = parameterPriors,
                QuantilePriors = quantilePriors,
                UseSingleQuantile = useSingleQuantile,
                Name = name
            });
            return McpJson.Serialize(AnalysisMapper.ToResourceResponse(resource));
        }

        /// <summary>
        /// Creates a competing risks frequency analysis.
        /// </summary>
        /// <param name="inputDataId">The input-data resource id.</param>
        /// <param name="distributions">The 1-3 component distribution names.</param>
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
        [McpServerTool(Name = "create_competingrisks_analysis")]
        [Description("Create a Bayesian MCMC competing risks frequency analysis over an input-data resource — models each annual maximum as the maximum of 1-3 independent flood-generating processes, each with its own component distribution. Returns the analysis id to pass to run_analysis.")]
        public string CreateCompetingRisksAnalysis(
            [Description("Id of the input-data resource to fit.")] Guid inputDataId,
            [Description("1-3 component distributions, one per competing process (see get_metadata for names).")] string[] distributions,
            [Description("Optional AEP ordinates, each strictly between 0 and 1. Sorted automatically.")] double[]? probabilityOrdinates = null,
            [Description("Optional total MCMC iterations per chain (server-capped).")] int? iterations = null,
            [Description("Optional warm-up iterations (must be below iterations).")] int? warmupIterations = null,
            [Description("Optional number of parallel chains (1-8).")] int? numberOfChains = null,
            [Description("Optional PRNG seed for reproducible runs.")] int? prngSeed = null,
            [Description("Optional credible interval width, e.g. 0.90.")] double? credibleIntervalWidth = null,
            [Description("Optional sampler: demCzs (default), demCz, arwmh, or nuts.")] string? sampler = null,
            [Description("Optional point estimator: posteriorMode or posteriorMean.")] string? pointEstimator = null,
            [Description("Optional informative parameter priors: array of { parameterName, distribution: { type, parameters }, isFixed? }.")] List<ParameterPriorDto>? parameterPriors = null,
            [Description("Optional quantile priors: array of { alpha (AEP), distribution: { type, parameters } }.")] List<QuantilePriorDto>? quantilePriors = null,
            [Description("True for the single-quantile-prior formulation (Viglione et al. 2013).")] bool? useSingleQuantile = null,
            [Description("Optional display name for the analysis.")] string? name = null)
        {
            var resource = _service.CreateCompetingRisks(new CreateCompetingRisksAnalysisRequest
            {
                InputDataId = inputDataId,
                Distributions = ParseDistributions(distributions),
                ProbabilityOrdinates = probabilityOrdinates?.ToList(),
                BayesianOptions = AnalysisTools.BuildBayesianOptions(iterations, warmupIterations, numberOfChains, prngSeed, credibleIntervalWidth, sampler, pointEstimator),
                ParameterPriors = parameterPriors,
                QuantilePriors = quantilePriors,
                UseSingleQuantile = useSingleQuantile,
                Name = name
            });
            return McpJson.Serialize(AnalysisMapper.ToResourceResponse(resource));
        }

        /// <summary>
        /// Creates a composite analysis over existing component analyses.
        /// </summary>
        /// <param name="componentAnalysisIds">The component analysis ids.</param>
        /// <param name="compositeType">The composition method name.</param>
        /// <param name="weights">Mixture weights aligned with the component ids.</param>
        /// <param name="averageMethod">Optional model-average weighting method name.</param>
        /// <param name="dependency">Optional competing risks dependence assumption name.</param>
        /// <param name="isMaximum">True to combine competing risks as the maximum.</param>
        /// <param name="probabilityOrdinates">Optional AEP ordinates.</param>
        /// <param name="credibleIntervalWidth">Optional credible interval width.</param>
        /// <param name="pointEstimator">Optional point estimator name.</param>
        /// <param name="name">Optional display name.</param>
        /// <returns>JSON with the created analysis summary including its id.</returns>
        [McpServerTool(Name = "create_composite_analysis")]
        [Description("Create a composite analysis over already-created component analyses (univariate, bulletin17c, mixture, pointprocess, or competingrisks kinds). Combines component posteriors by competing risks, mixture weights, or information-criterion model averaging — no MCMC of its own. Components must be RUN before the composite runs; components are LIVE references (re-running one refreshes the composite's next run). Returns the analysis id to pass to run_analysis.")]
        public string CreateCompositeAnalysis(
            [Description("Ids of the component analyses, in order.")] Guid[] componentAnalysisIds,
            [Description("Composition method: competingRisks (default), mixture, or modelAverage.")] string? compositeType = null,
            [Description("Mixture weights aligned with componentAnalysisIds (each strictly 0-1, sum ≤ 1). Required for mixture; ignored otherwise.")] double[]? weights = null,
            [Description("Model-average weighting: aic, bic, dic, waic, looic, equal, or rmse. Only with compositeType=modelAverage.")] string? averageMethod = null,
            [Description("Competing risks dependence: independent (default), perfectlyPositive, or perfectlyNegative.")] string? dependency = null,
            [Description("True (default) for the maximum of competing processes; false for the minimum.")] bool isMaximum = true,
            [Description("Optional AEP ordinates, each strictly between 0 and 1. Sorted automatically.")] double[]? probabilityOrdinates = null,
            [Description("Optional credible interval width for the composite bands, e.g. 0.90.")] double? credibleIntervalWidth = null,
            [Description("Optional point estimator: posteriorMode or posteriorMean.")] string? pointEstimator = null,
            [Description("Optional display name for the analysis.")] string? name = null)
        {
            if (componentAnalysisIds == null || componentAnalysisIds.Length == 0)
            {
                throw new ArgumentException("At least one component analysis id is required.");
            }
            if (weights != null && weights.Length != componentAnalysisIds.Length)
            {
                throw new ArgumentException("weights must align one-to-one with componentAnalysisIds.");
            }

            var components = new List<CompositeComponentDto>(componentAnalysisIds.Length);
            for (int i = 0; i < componentAnalysisIds.Length; i++)
            {
                components.Add(new CompositeComponentDto
                {
                    AnalysisId = componentAnalysisIds[i],
                    Weight = weights?[i]
                });
            }

            var resource = _service.CreateComposite(new CreateCompositeAnalysisRequest
            {
                Components = components,
                CompositeType = EnumHelper.ParseOrDefault(compositeType, RMC.BestFit.Analyses.CompositeType.CompetingRisks),
                AverageMethod = EnumHelper.ParseOrNull<RMC.BestFit.Analyses.AverageMethod>(averageMethod),
                Dependency = EnumHelper.ParseOrNull<Numerics.Data.Statistics.Probability.DependencyType>(dependency),
                IsMaximum = isMaximum,
                ProbabilityOrdinates = probabilityOrdinates?.ToList(),
                CredibleIntervalWidth = credibleIntervalWidth,
                PointEstimator = EnumHelper.ParseOrNull<RMC.BestFit.Estimation.BayesianAnalysis.PointEstimateType>(pointEstimator),
                Name = name
            });
            return McpJson.Serialize(AnalysisMapper.ToResourceResponse(resource));
        }

        /// <summary>
        /// Creates a distribution-fitting analysis (parallel MLE screen).
        /// </summary>
        /// <param name="inputDataId">The input-data resource id.</param>
        /// <param name="distributions">Optional candidate subset names.</param>
        /// <param name="name">Optional display name.</param>
        /// <returns>JSON with the created analysis summary including its id.</returns>
        [McpServerTool(Name = "create_distributionfitting_analysis")]
        [Description("Create a distribution-fitting analysis: a fast parallel maximum-likelihood fit of candidate distributions over an input-data resource, ranked by AIC/BIC/RMSE — a screening step (seconds, no MCMC) before Bayesian analyses. Omit distributions to fit all 15. Returns the analysis id to pass to run_analysis.")]
        public string CreateDistributionFittingAnalysis(
            [Description("Id of the input-data resource to fit.")] Guid inputDataId,
            [Description("Optional candidate subset, e.g. ['logPearsonTypeIII','generalizedExtremeValue']; omit for all 15.")] string[]? distributions = null,
            [Description("Optional display name for the analysis.")] string? name = null)
        {
            var resource = _service.CreateDistributionFitting(new CreateDistributionFittingAnalysisRequest
            {
                InputDataId = inputDataId,
                Distributions = distributions == null || distributions.Length == 0 ? null : ParseDistributions(distributions),
                Name = name
            });
            return McpJson.Serialize(AnalysisMapper.ToResourceResponse(resource));
        }

        /// <summary>
        /// Creates a bivariate copula analysis over two fitted marginal analyses.
        /// </summary>
        /// <param name="marginalXAnalysisId">The marginal-X analysis id.</param>
        /// <param name="marginalYAnalysisId">The marginal-Y analysis id.</param>
        /// <param name="xOrdinates">The x magnitudes of the joint-exceedance grid.</param>
        /// <param name="yOrdinates">The y magnitudes of the grid, aligned with xOrdinates.</param>
        /// <param name="copulaType">Optional copula family name.</param>
        /// <param name="estimationMethod">Optional copula estimation method name.</param>
        /// <param name="iterations">Optional MCMC iterations.</param>
        /// <param name="warmupIterations">Optional warm-up iterations.</param>
        /// <param name="numberOfChains">Optional chain count.</param>
        /// <param name="prngSeed">Optional PRNG seed for reproducibility.</param>
        /// <param name="credibleIntervalWidth">Optional credible interval width.</param>
        /// <param name="sampler">Optional sampler name.</param>
        /// <param name="pointEstimator">Optional point estimator name.</param>
        /// <param name="parameterPriors">Optional informative copula-parameter priors.</param>
        /// <param name="name">Optional display name.</param>
        /// <returns>JSON with the created analysis summary including its id.</returns>
        [McpServerTool(Name = "create_bivariate_analysis")]
        [Description("Create a Bayesian MCMC bivariate copula analysis over two FITTED marginal analyses (kinds univariate/bulletin17c/mixture/pointprocess; the marginals' data pair by shared time index, ≥10 overlapping observations required). Marginals are LIVE references and must be RUN before this analysis runs. The results report joint exceedance P(X>x AND Y>y) at each grid point. Returns the analysis id to pass to run_analysis.")]
        public string CreateBivariateAnalysis(
            [Description("Id of the marginal-X analysis.")] Guid marginalXAnalysisId,
            [Description("Id of the marginal-Y analysis (must differ from marginal X).")] Guid marginalYAnalysisId,
            [Description("X magnitudes of the joint-exceedance evaluation grid.")] double[] xOrdinates,
            [Description("Y magnitudes aligned one-to-one with xOrdinates.")] double[] yOrdinates,
            [Description("Copula family: normal (default), clayton, frank, gumbel, joe, aliMikhailHaq, or studentT.")] string? copulaType = null,
            [Description("Copula estimation: inferenceFromMargins (default) or pseudoLikelihood.")] string? estimationMethod = null,
            [Description("Optional total MCMC iterations per chain (server-capped).")] int? iterations = null,
            [Description("Optional warm-up iterations (must be below iterations).")] int? warmupIterations = null,
            [Description("Optional number of parallel chains (1-8).")] int? numberOfChains = null,
            [Description("Optional PRNG seed for reproducible runs.")] int? prngSeed = null,
            [Description("Optional credible interval width, e.g. 0.90.")] double? credibleIntervalWidth = null,
            [Description("Optional sampler: demCzs (default), demCz, arwmh, or nuts.")] string? sampler = null,
            [Description("Optional point estimator: posteriorMode or posteriorMean.")] string? pointEstimator = null,
            [Description("Optional informative priors on the copula parameter(s): array of { parameterName, distribution: { type, parameters }, isFixed? }.")] List<ParameterPriorDto>? parameterPriors = null,
            [Description("Optional display name for the analysis.")] string? name = null)
        {
            if (xOrdinates == null || yOrdinates == null || xOrdinates.Length == 0 || xOrdinates.Length != yOrdinates.Length)
            {
                throw new ArgumentException("xOrdinates and yOrdinates must be non-empty and align one-to-one.");
            }
            var points = new List<XyOrdinateDto>(xOrdinates.Length);
            for (int i = 0; i < xOrdinates.Length; i++)
            {
                points.Add(new XyOrdinateDto { X = xOrdinates[i], Y = yOrdinates[i] });
            }

            var resource = _service.CreateBivariate(new CreateBivariateAnalysisRequest
            {
                MarginalXAnalysisId = marginalXAnalysisId,
                MarginalYAnalysisId = marginalYAnalysisId,
                CopulaType = EnumHelper.ParseOrDefault(copulaType, Numerics.Distributions.Copulas.CopulaType.Normal),
                EstimationMethod = EnumHelper.ParseOrNull<Numerics.Distributions.Copulas.CopulaEstimationMethod>(estimationMethod),
                XyOrdinates = points,
                BayesianOptions = AnalysisTools.BuildBayesianOptions(iterations, warmupIterations, numberOfChains, prngSeed, credibleIntervalWidth, sampler, pointEstimator),
                ParameterPriors = parameterPriors,
                Name = name
            });
            return McpJson.Serialize(AnalysisMapper.ToResourceResponse(resource));
        }

        /// <summary>
        /// Creates a coincident frequency analysis over a fitted bivariate analysis.
        /// </summary>
        /// <param name="bivariateAnalysisId">The upstream bivariate analysis id.</param>
        /// <param name="xValues">The strictly ascending surface row ordinates.</param>
        /// <param name="yValues">The strictly ascending surface column ordinates.</param>
        /// <param name="bivariateResponseJson">The response surface as a JSON 2-D array.</param>
        /// <param name="numberOfBins">The number of output response-magnitude bins.</param>
        /// <param name="credibleIntervalWidth">Optional credible interval width.</param>
        /// <param name="pointEstimator">Optional point estimator name.</param>
        /// <param name="name">Optional display name.</param>
        /// <returns>JSON with the created analysis summary including its id.</returns>
        [McpServerTool(Name = "create_coincidentfrequency_analysis")]
        [Description("Create a coincident frequency analysis: integrate a tabulated response surface Z(x,y) (e.g., pool stage from inflow and starting stage) over a FITTED bivariate analysis to get the response's annual exceedance frequency curve. The bivariate is a LIVE reference and must be RUN before this analysis runs. Returns the analysis id to pass to run_analysis.")]
        public string CreateCoincidentFrequencyAnalysis(
            [Description("Id of the fitted bivariate analysis.")] Guid bivariateAnalysisId,
            [Description("Strictly ascending x ordinates of the surface rows (≥2).")] double[] xValues,
            [Description("Strictly ascending y ordinates of the surface columns (≥2).")] double[] yValues,
            [Description("Response surface as a JSON 2-D array, row i = xValues[i]: e.g. '[[10.1,10.5],[11.2,11.8]]'. Must be strictly increasing along both axes.")] string bivariateResponseJson,
            [Description("Number of output response-magnitude bins (5-1000). Default 50.")] int numberOfBins = 50,
            [Description("Optional credible interval width, e.g. 0.90.")] double? credibleIntervalWidth = null,
            [Description("Optional point estimator: posteriorMode or posteriorMean.")] string? pointEstimator = null,
            [Description("Optional display name for the analysis.")] string? name = null)
        {
            List<List<double>> surface;
            try
            {
                surface = System.Text.Json.JsonSerializer.Deserialize<List<List<double>>>(bivariateResponseJson)
                    ?? throw new ArgumentException("bivariateResponseJson deserialized to null.");
            }
            catch (System.Text.Json.JsonException ex)
            {
                throw new ArgumentException(
                    $"bivariateResponseJson is not a valid JSON 2-D array of numbers: {ex.Message}", ex);
            }

            var resource = _service.CreateCoincidentFrequency(new CreateCoincidentFrequencyAnalysisRequest
            {
                BivariateAnalysisId = bivariateAnalysisId,
                XValues = xValues?.ToList() ?? new List<double>(),
                YValues = yValues?.ToList() ?? new List<double>(),
                BivariateResponse = surface,
                NumberOfBins = numberOfBins,
                CredibleIntervalWidth = credibleIntervalWidth,
                PointEstimator = EnumHelper.ParseOrNull<RMC.BestFit.Estimation.BayesianAnalysis.PointEstimateType>(pointEstimator),
                Name = name
            });
            return McpJson.Serialize(AnalysisMapper.ToResourceResponse(resource));
        }

        /// <summary>
        /// Creates a time-series analysis (AR, MA, ARIMA, or ARIMAX per modelType).
        /// </summary>
        /// <param name="timeSeriesId">The source time-series resource id.</param>
        /// <param name="modelType">The model family name.</param>
        /// <param name="order">The AR/MA order for ar/ma.</param>
        /// <param name="pOrder">The AR order p for arima/arimax.</param>
        /// <param name="dOrder">The differencing order d for arima/arimax.</param>
        /// <param name="qOrder">The MA order q for arima/arimax.</param>
        /// <param name="xOrder">The covariate order b for arimax.</param>
        /// <param name="includeIntercept">Optional intercept flag.</param>
        /// <param name="transformType">Optional transform name.</param>
        /// <param name="trendType">Optional arimax trend name.</param>
        /// <param name="includeSeasonality">Optional arimax seasonality flag.</param>
        /// <param name="covariateTimeSeriesIds">Optional arimax covariate resource ids.</param>
        /// <param name="covariateExtension">Optional arimax covariate extension name.</param>
        /// <param name="trainingTimeSteps">Optional training window in time steps.</param>
        /// <param name="forecastingTimeSteps">Optional forecast horizon (0-100).</param>
        /// <param name="iterations">Optional MCMC iterations.</param>
        /// <param name="warmupIterations">Optional warm-up iterations.</param>
        /// <param name="numberOfChains">Optional chain count.</param>
        /// <param name="prngSeed">Optional PRNG seed for reproducibility.</param>
        /// <param name="credibleIntervalWidth">Optional credible interval width.</param>
        /// <param name="sampler">Optional sampler name.</param>
        /// <param name="pointEstimator">Optional point estimator name.</param>
        /// <param name="parameterPriors">Optional informative parameter priors.</param>
        /// <param name="name">Optional display name.</param>
        /// <returns>JSON with the created analysis summary including its id.</returns>
        [McpServerTool(Name = "create_timeseries_analysis")]
        [Description("Create a Bayesian MCMC time-series analysis over a stored time series (the series is cloned). modelType picks the family: 'ar' (order p), 'ma' (order q), 'arima' (pOrder/dOrder/qOrder), or 'arimax' (+ covariates, trend, seasonality). Fields that do not apply to the chosen family are rejected. Returns the analysis id to pass to run_analysis.")]
        public string CreateTimeSeriesAnalysis(
            [Description("Id of the time-series resource to model.")] Guid timeSeriesId,
            [Description("Model family: ar, ma, arima, or arimax.")] string modelType,
            [Description("AR/MA order for ar/ma (default 1). Rejected for arima/arimax.")] int? order = null,
            [Description("AR order p for arima/arimax (default 1). Rejected for ar/ma.")] int? pOrder = null,
            [Description("Differencing order d for arima/arimax (default 0). Rejected for ar/ma.")] int? dOrder = null,
            [Description("MA order q for arima/arimax (default 0). Rejected for ar/ma.")] int? qOrder = null,
            [Description("Covariate order b for arimax (default 0). Rejected otherwise.")] int? xOrder = null,
            [Description("Include the intercept term μ. Default true.")] bool? includeIntercept = null,
            [Description("Transform: none, logarithmic, boxCox, or yeoJohnson (parameters fitted automatically).")] string? transformType = null,
            [Description("ARIMAX trend: none, linear, quadratic, or cubic. Rejected otherwise.")] string? trendType = null,
            [Description("True to include an ARIMAX Fourier seasonal component. Rejected otherwise.")] bool? includeSeasonality = null,
            [Description("ARIMAX covariate time-series resource ids, in order (each cloned). Rejected otherwise.")] Guid[]? covariateTimeSeriesIds = null,
            [Description("ARIMAX covariate extension: none, blockBootstrap (default), or knn. Rejected otherwise.")] string? covariateExtension = null,
            [Description("Optional training window in time steps (default 80% of the series).")] int? trainingTimeSteps = null,
            [Description("Optional forecast horizon past the series end (0-100).")] int? forecastingTimeSteps = null,
            [Description("Optional total MCMC iterations per chain (server-capped).")] int? iterations = null,
            [Description("Optional warm-up iterations (must be below iterations).")] int? warmupIterations = null,
            [Description("Optional number of parallel chains (1-8).")] int? numberOfChains = null,
            [Description("Optional PRNG seed for reproducible runs.")] int? prngSeed = null,
            [Description("Optional credible interval width, e.g. 0.90.")] double? credibleIntervalWidth = null,
            [Description("Optional sampler: demCzs (default), demCz, arwmh, or nuts.")] string? sampler = null,
            [Description("Optional point estimator: posteriorMode or posteriorMean.")] string? pointEstimator = null,
            [Description("Optional informative parameter priors: array of { parameterName, distribution: { type, parameters }, isFixed? }.")] List<ParameterPriorDto>? parameterPriors = null,
            [Description("Optional display name for the analysis.")] string? name = null)
        {
            var parsedModelType = EnumHelper.ParseOrNull<Api.Store.TimeSeriesModelType>(modelType)
                ?? throw new ArgumentException("modelType is required: ar, ma, arima, or arimax.");

            var resource = _service.CreateTimeSeries(new CreateTimeSeriesAnalysisRequest
            {
                TimeSeriesId = timeSeriesId,
                ModelType = parsedModelType,
                Order = order,
                POrder = pOrder,
                DOrder = dOrder,
                QOrder = qOrder,
                XOrder = xOrder,
                IncludeIntercept = includeIntercept,
                TransformType = EnumHelper.ParseOrNull<RMC.BestFit.Models.Transform>(transformType),
                TrendType = EnumHelper.ParseOrNull<RMC.BestFit.Models.ARIMAX.Trend>(trendType),
                IncludeSeasonality = includeSeasonality,
                CovariateTimeSeriesIds = covariateTimeSeriesIds?.ToList(),
                CovariateExtension = EnumHelper.ParseOrNull<RMC.BestFit.Models.ARIMAX.CovariateExtensionMethod>(covariateExtension),
                TrainingTimeSteps = trainingTimeSteps,
                ForecastingTimeSteps = forecastingTimeSteps,
                BayesianOptions = AnalysisTools.BuildBayesianOptions(iterations, warmupIterations, numberOfChains, prngSeed, credibleIntervalWidth, sampler, pointEstimator),
                ParameterPriors = parameterPriors,
                Name = name
            });
            return McpJson.Serialize(AnalysisMapper.ToResourceResponse(resource));
        }

        /// <summary>
        /// Parses a component-distribution name array into typed values. Blank entries are
        /// rejected rather than silently defaulted.
        /// </summary>
        /// <param name="distributions">The camelCase distribution names.</param>
        /// <returns>The parsed distribution types.</returns>
        /// <exception cref="ArgumentException">Thrown when the array is null/empty, an entry is blank, or a name is not a distribution type; the message lists the accepted values.</exception>
        private static List<UnivariateDistributionType> ParseDistributions(string[] distributions)
        {
            if (distributions == null || distributions.Length == 0)
            {
                throw new ArgumentException("At least one component distribution name is required.");
            }
            var parsed = new List<UnivariateDistributionType>(distributions.Length);
            foreach (var entry in distributions)
            {
                parsed.Add(EnumHelper.ParseOrNull<UnivariateDistributionType>(entry)
                    ?? throw new ArgumentException("Component distribution names cannot be blank."));
            }
            return parsed;
        }
    }
}
