using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Numerics.Data.Statistics;
using RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>
    /// Request body for creating a composite analysis over already-created component analyses.
    /// A composite runs no MCMC of its own: it combines the component posteriors by competing
    /// risks, mixture weighting, or information-criterion model averaging. Every component must
    /// have been RUN before the composite runs.
    /// </summary>
    public class CreateCompositeAnalysisRequest
    {
        /// <summary>
        /// The component analyses (at least one; at most is practically limited by the
        /// composition method). Components are LIVE references — re-running a component later
        /// intentionally refreshes the composite's next run.
        /// </summary>
        [Required]
        [MinLength(1)]
        [JsonPropertyName("components")]
        public List<CompositeComponentDto> Components { get; set; } = new();

        /// <summary>
        /// How the components are combined: "competingRisks" (maximum/minimum of independent
        /// processes; default), "mixture" (client-supplied weights), or "modelAverage"
        /// (information-criterion weights computed at run time).
        /// </summary>
        [JsonPropertyName("compositeType")]
        public CompositeType CompositeType { get; set; } = CompositeType.CompetingRisks;

        /// <summary>
        /// The weighting method for "modelAverage" composites: "aic", "bic", "dic", "waic",
        /// "looic", "equal", or "rmse". Leave null for the model default. DIC/WAIC/LOOIC require
        /// every component to have an MCMC posterior (a Bulletin 17C component rules them out).
        /// </summary>
        [JsonPropertyName("averageMethod")]
        public AverageMethod? AverageMethod { get; set; }

        /// <summary>
        /// The dependence assumption between competing risks components: "independent" (default),
        /// "perfectlyPositive", or "perfectlyNegative". Ignored for other composite types.
        /// </summary>
        [JsonPropertyName("dependency")]
        public Probability.DependencyType? Dependency { get; set; }

        /// <summary>
        /// True (default) to combine competing risks as the maximum of the component processes;
        /// false for the minimum. Ignored for other composite types.
        /// </summary>
        [JsonPropertyName("isMaximum")]
        public bool IsMaximum { get; set; } = true;

        /// <summary>
        /// Optional annual exceedance probabilities (AEP) at which the composite frequency curve
        /// is evaluated. Each value must be strictly between 0 and 1. Leave null for the 25 defaults.
        /// </summary>
        [JsonPropertyName("probabilityOrdinates")]
        public List<double>? ProbabilityOrdinates { get; set; }

        /// <summary>
        /// Optional credible interval width for the composite uncertainty bands (e.g., 0.90).
        /// Composites accept no other Bayesian options — they run no MCMC of their own.
        /// </summary>
        [Range(0.01, 0.999)]
        [JsonPropertyName("credibleIntervalWidth")]
        public double? CredibleIntervalWidth { get; set; }

        /// <summary>
        /// Optional point estimator used when assembling the composite from component point
        /// estimates: "posteriorMode" or "posteriorMean". Leave null for the model default.
        /// </summary>
        [JsonPropertyName("pointEstimator")]
        public BayesianAnalysis.PointEstimateType? PointEstimator { get; set; }

        /// <summary>
        /// Optional display name for the analysis. Defaults to "Composite of {component names}".
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
