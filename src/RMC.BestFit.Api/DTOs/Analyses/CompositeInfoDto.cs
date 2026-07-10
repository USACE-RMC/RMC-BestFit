using System.Text.Json.Serialization;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>
    /// Composite-specific block on the frequency results response: the composition settings and
    /// the per-component weights. For model-average composites the weights are computed at run
    /// time and are a first-class output.
    /// </summary>
    public class CompositeInfoDto
    {
        /// <summary>
        /// The composition method ("competingRisks", "mixture", or "modelAverage").
        /// </summary>
        [JsonPropertyName("compositeType")]
        public string? CompositeType { get; set; }

        /// <summary>
        /// The model-average weighting method, when compositeType is "modelAverage"; otherwise null.
        /// </summary>
        [JsonPropertyName("averageMethod")]
        public string? AverageMethod { get; set; }

        /// <summary>
        /// The competing risks dependence assumption, when compositeType is "competingRisks";
        /// otherwise null.
        /// </summary>
        [JsonPropertyName("dependency")]
        public string? Dependency { get; set; }

        /// <summary>
        /// True when competing risks combine as the maximum of the component processes; null for
        /// other composite types.
        /// </summary>
        [JsonPropertyName("isMaximum")]
        public bool? IsMaximum { get; set; }

        /// <summary>
        /// The components with their current weights, in composite order.
        /// </summary>
        [JsonPropertyName("components")]
        public List<CompositeComponentInfoDto> Components { get; set; } = new();
    }

    /// <summary>
    /// One component's identity and weight within a composite results response. Kept in this
    /// file as a minor supporting DTO tightly coupled to <see cref="CompositeInfoDto"/>.
    /// </summary>
    public class CompositeComponentInfoDto
    {
        /// <summary>
        /// The component analysis resource id (provenance — the resource may since have been deleted).
        /// </summary>
        [JsonPropertyName("analysisId")]
        public Guid? AnalysisId { get; set; }

        /// <summary>
        /// The component analysis display name.
        /// </summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        /// <summary>
        /// The component analysis kind ("univariate", "bulletin17C", ...).
        /// </summary>
        [JsonPropertyName("kind")]
        public string? Kind { get; set; }

        /// <summary>
        /// The component's weight: client-supplied for mixture composites, computed at run time
        /// for model averaging, and unused (0) for competing risks.
        /// </summary>
        [JsonPropertyName("weight")]
        public double Weight { get; set; }
    }
}
