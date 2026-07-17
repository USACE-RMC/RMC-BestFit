using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using RMC.BestFit.Estimation;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>
    /// Request body for creating a coincident frequency analysis: integrates a user-supplied
    /// response surface Z(x, y) — e.g., pool stage as a function of inflow and starting stage —
    /// over the joint distribution of a FITTED bivariate analysis to produce the annual
    /// exceedance frequency curve of the response. No MCMC of its own; the upstream bivariate
    /// (a LIVE reference) must be run before this analysis runs.
    /// </summary>
    public class CreateCoincidentFrequencyAnalysisRequest
    {
        /// <summary>
        /// The id of the upstream bivariate analysis. A LIVE reference — re-running it (or its
        /// marginals) refreshes this analysis's next run.
        /// </summary>
        [Required]
        [JsonPropertyName("bivariateAnalysisId")]
        public Guid BivariateAnalysisId { get; set; }

        /// <summary>
        /// The strictly ascending marginal-X ordinates of the response surface rows (at least 2).
        /// </summary>
        [Required]
        [MinLength(2)]
        [JsonPropertyName("xValues")]
        public List<double> XValues { get; set; } = new();

        /// <summary>
        /// The strictly ascending marginal-Y ordinates of the response surface columns (at least 2).
        /// </summary>
        [Required]
        [MinLength(2)]
        [JsonPropertyName("yValues")]
        public List<double> YValues { get; set; } = new();

        /// <summary>
        /// The tabulated response surface: bivariateResponse[i][j] = Z(xValues[i], yValues[j]).
        /// Every row must have exactly yValues.Count entries, the row count must equal
        /// xValues.Count, and the surface must be strictly increasing along both axes.
        /// </summary>
        [Required]
        [JsonPropertyName("bivariateResponse")]
        public List<List<double>> BivariateResponse { get; set; } = new();

        /// <summary>
        /// The number of evenly spaced response-magnitude bins the output frequency curve is
        /// evaluated at (5-1000). Default 50.
        /// </summary>
        [Range(5, 1000)]
        [JsonPropertyName("numberOfBins")]
        public int NumberOfBins { get; set; } = 50;

        /// <summary>
        /// Optional credible interval width for the output uncertainty bands (e.g., 0.90). This
        /// analysis accepts no other Bayesian options — it reuses the upstream posteriors.
        /// </summary>
        [Range(0.01, 0.999)]
        [JsonPropertyName("credibleIntervalWidth")]
        public double? CredibleIntervalWidth { get; set; }

        /// <summary>
        /// Optional point estimator used for the point-estimate curve: "posteriorMode" or
        /// "posteriorMean". Leave null for the model default.
        /// </summary>
        [JsonPropertyName("pointEstimator")]
        public BayesianAnalysis.PointEstimateType? PointEstimator { get; set; }

        /// <summary>
        /// Optional display name for the analysis. Defaults to "Coincident frequency over {bivariate name}".
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
