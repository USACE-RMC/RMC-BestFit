using System.Text.Json.Serialization;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>
    /// Response body carrying time-series analysis results: the fitted-plus-forecast curve with
    /// credible intervals, parameter posteriors, and information criteria. The curve spans the
    /// observed series followed by the forecast horizon.
    /// </summary>
    public class TimeSeriesResultsResponse : ResponseBase
    {
        /// <summary>
        /// The id of the analysis the results belong to.
        /// </summary>
        [JsonPropertyName("analysisId")]
        public Guid AnalysisId { get; set; }

        /// <summary>
        /// The analysis kind ("timeSeries").
        /// </summary>
        [JsonPropertyName("kind")]
        public string? Kind { get; set; }

        /// <summary>
        /// The model family ("ar", "ma", "arima", "arimax").
        /// </summary>
        [JsonPropertyName("modelType")]
        public string? ModelType { get; set; }

        /// <summary>
        /// The variance-stabilizing transform applied before modeling.
        /// </summary>
        [JsonPropertyName("transformType")]
        public string? TransformType { get; set; }

        /// <summary>
        /// The observed series length in time steps.
        /// </summary>
        [JsonPropertyName("dataLength")]
        public int DataLength { get; set; }

        /// <summary>
        /// The number of time steps used for training.
        /// </summary>
        [JsonPropertyName("trainingTimeSteps")]
        public int TrainingTimeSteps { get; set; }

        /// <summary>
        /// The number of forecast steps past the end of the observed series.
        /// </summary>
        [JsonPropertyName("forecastingTimeSteps")]
        public int ForecastingTimeSteps { get; set; }

        /// <summary>
        /// The AR/MA order for "ar"/"ma" models; null otherwise.
        /// </summary>
        [JsonPropertyName("order")]
        public int? Order { get; set; }

        /// <summary>
        /// The autoregressive order p for "arima"/"arimax" models; null otherwise.
        /// </summary>
        [JsonPropertyName("pOrder")]
        public int? POrder { get; set; }

        /// <summary>
        /// The differencing order d for "arima"/"arimax" models; null otherwise.
        /// </summary>
        [JsonPropertyName("dOrder")]
        public int? DOrder { get; set; }

        /// <summary>
        /// The moving-average order q for "arima"/"arimax" models; null otherwise.
        /// </summary>
        [JsonPropertyName("qOrder")]
        public int? QOrder { get; set; }

        /// <summary>
        /// The exogenous covariate order b for "arimax" models; null otherwise.
        /// </summary>
        [JsonPropertyName("xOrder")]
        public int? XOrder { get; set; }

        /// <summary>
        /// The fitted-plus-forecast curve with credible intervals.
        /// </summary>
        [JsonPropertyName("curve")]
        public TimeSeriesCurveDto? Curve { get; set; }

        /// <summary>
        /// The model parameter point estimates.
        /// </summary>
        [JsonPropertyName("parameters")]
        public List<ParameterValueDto> Parameters { get; set; } = new();

        /// <summary>
        /// Posterior summaries per parameter, including convergence diagnostics.
        /// </summary>
        [JsonPropertyName("parameterSummaries")]
        public List<ParameterSummaryDto> ParameterSummaries { get; set; } = new();

        /// <summary>
        /// Model-fit information criteria (computed on the training period).
        /// </summary>
        [JsonPropertyName("informationCriteria")]
        public InformationCriteriaDto? InformationCriteria { get; set; }

        /// <summary>
        /// Run diagnostics (sampler settings, acceptance rates, convergence warnings, timing).
        /// </summary>
        [JsonPropertyName("diagnostics")]
        public DiagnosticsDto? Diagnostics { get; set; }
    }

    /// <summary>
    /// The fitted-plus-forecast curve of a time-series results response, aligned by time index.
    /// Kept in this file as a minor supporting DTO tightly coupled to
    /// <see cref="TimeSeriesResultsResponse"/>.
    /// </summary>
    public class TimeSeriesCurveDto
    {
        /// <summary>
        /// The time indices of the curve points (observed steps followed by forecast steps).
        /// </summary>
        [JsonPropertyName("timeIndices")]
        public List<double> TimeIndices { get; set; } = new();

        /// <summary>
        /// The predicted values at the point estimate, per time index.
        /// </summary>
        [JsonPropertyName("modeCurve")]
        public List<double>? ModeCurve { get; set; }

        /// <summary>
        /// The posterior-mean predicted values, per time index.
        /// </summary>
        [JsonPropertyName("meanCurve")]
        public List<double>? MeanCurve { get; set; }

        /// <summary>
        /// The lower credible bound, per time index.
        /// </summary>
        [JsonPropertyName("ciLower")]
        public List<double>? CiLower { get; set; }

        /// <summary>
        /// The upper credible bound, per time index.
        /// </summary>
        [JsonPropertyName("ciUpper")]
        public List<double>? CiUpper { get; set; }

        /// <summary>
        /// The credible interval width the bounds correspond to (e.g., 0.90).
        /// </summary>
        [JsonPropertyName("credibleIntervalWidth")]
        public double CredibleIntervalWidth { get; set; }
    }
}
