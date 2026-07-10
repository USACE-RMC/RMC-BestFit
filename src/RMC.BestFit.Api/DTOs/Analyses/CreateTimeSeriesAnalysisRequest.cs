using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using RMC.BestFit.Api.Store;
using RMC.BestFit.Models;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>
    /// Request body for creating a Bayesian MCMC time-series analysis over a stored time series.
    /// One route covers four model families discriminated by <see cref="ModelType"/>: "ar"
    /// (order p), "ma" (order q), "arima" (p, d, q), and "arimax" (p, d, q plus exogenous
    /// covariates, trend, and seasonality). Fields that do not apply to the chosen model type
    /// are rejected with 400 — never silently ignored.
    /// </summary>
    public class CreateTimeSeriesAnalysisRequest
    {
        /// <summary>
        /// The id of the time-series resource to model (create it first via the api/timeseries
        /// endpoints). The series is cloned at creation.
        /// </summary>
        [Required]
        [JsonPropertyName("timeSeriesId")]
        public Guid TimeSeriesId { get; set; }

        /// <summary>
        /// The model family: "ar", "ma", "arima", or "arimax".
        /// </summary>
        [Required]
        [JsonPropertyName("modelType")]
        public TimeSeriesModelType ModelType { get; set; }

        /// <summary>
        /// The AR or MA order for the "ar"/"ma" model types (default 1). Rejected for
        /// "arima"/"arimax" — use pOrder/dOrder/qOrder there.
        /// </summary>
        [Range(1, 10)]
        [JsonPropertyName("order")]
        public int? Order { get; set; }

        /// <summary>
        /// The autoregressive order p for "arima"/"arimax" (default 1). Rejected for "ar"/"ma".
        /// </summary>
        [Range(0, 10)]
        [JsonPropertyName("pOrder")]
        public int? POrder { get; set; }

        /// <summary>
        /// The differencing order d for "arima"/"arimax" (default 0). Rejected for "ar"/"ma".
        /// </summary>
        [Range(0, 3)]
        [JsonPropertyName("dOrder")]
        public int? DOrder { get; set; }

        /// <summary>
        /// The moving-average order q for "arima"/"arimax" (default 0). Rejected for "ar"/"ma".
        /// </summary>
        [Range(0, 10)]
        [JsonPropertyName("qOrder")]
        public int? QOrder { get; set; }

        /// <summary>
        /// The number of exogenous covariates b for "arimax" (default 0 — set alongside
        /// covariateTimeSeriesIds). Rejected for other model types.
        /// </summary>
        [Range(0, 10)]
        [JsonPropertyName("xOrder")]
        public int? XOrder { get; set; }

        /// <summary>
        /// True (default) to include the intercept term μ.
        /// </summary>
        [JsonPropertyName("includeIntercept")]
        public bool? IncludeIntercept { get; set; }

        /// <summary>
        /// Optional variance-stabilizing transform applied before modeling: "none",
        /// "logarithmic", "boxCox", or "yeoJohnson". Transform parameters (e.g., the Box-Cox
        /// lambda) are fitted automatically. Leave null for the model default.
        /// </summary>
        [JsonPropertyName("transformType")]
        public Transform? TransformType { get; set; }

        /// <summary>
        /// Optional deterministic trend for "arimax": "none", "linear", "quadratic", or "cubic".
        /// Rejected for other model types.
        /// </summary>
        [JsonPropertyName("trendType")]
        public ARIMAX.Trend? TrendType { get; set; }

        /// <summary>
        /// True to include a Fourier seasonal component for "arimax" (the seasonal period is
        /// inferred from the series interval). Rejected for other model types.
        /// </summary>
        [JsonPropertyName("includeSeasonality")]
        public bool? IncludeSeasonality { get; set; }

        /// <summary>
        /// The ids of the exogenous covariate time-series resources for "arimax", in covariate
        /// order. Each series is cloned at creation. Rejected for other model types.
        /// </summary>
        [JsonPropertyName("covariateTimeSeriesIds")]
        public List<Guid>? CovariateTimeSeriesIds { get; set; }

        /// <summary>
        /// How "arimax" covariates are extended past their observed record for forecasting:
        /// "none" (covariates must cover the forecast horizon), "blockBootstrap" (the model
        /// default), or "knn". Rejected for other model types.
        /// </summary>
        [JsonPropertyName("covariateExtension")]
        public ARIMAX.CovariateExtensionMethod? CovariateExtension { get; set; }

        /// <summary>
        /// Optional number of time steps used for training (the remainder validates the fit).
        /// Leave null for the model default (80% of the series).
        /// </summary>
        [Range(1, int.MaxValue)]
        [JsonPropertyName("trainingTimeSteps")]
        public int? TrainingTimeSteps { get; set; }

        /// <summary>
        /// Optional number of time steps to forecast past the end of the observed series (0-100).
        /// Leave null for the model default.
        /// </summary>
        [JsonPropertyName("forecastingTimeSteps")]
        public int? ForecastingTimeSteps { get; set; }

        /// <summary>
        /// Optional Bayesian MCMC settings; omitted fields keep the model defaults.
        /// </summary>
        [JsonPropertyName("bayesianOptions")]
        public BayesianOptionsDto? BayesianOptions { get; set; }

        /// <summary>
        /// Optional informative priors on individual model parameters (e.g., the AR coefficient
        /// or the scale), matched by parameter name. Unnamed parameters keep the default flat priors.
        /// </summary>
        [JsonPropertyName("parameterPriors")]
        public List<ParameterPriorDto>? ParameterPriors { get; set; }

        /// <summary>
        /// Optional display name for the analysis. Defaults to "{MODEL} analysis of {series name}".
        /// </summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        /// <summary>
        /// Optional description stored with the analysis.
        /// </summary>
        [JsonPropertyName("description")]
        public string? Description { get; set; }
    }
}
