using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>
    /// One component of a composite analysis: a reference to an existing analysis resource plus
    /// its mixture weight, when applicable.
    /// </summary>
    public class CompositeComponentDto
    {
        /// <summary>
        /// The id of the component analysis resource. Valid component kinds are univariate,
        /// bulletin17c, mixture, pointprocess, and competingrisks; composites cannot nest.
        /// The component keeps a LIVE reference: re-running it later changes the composite's
        /// next run, and deleting it from the store leaves the composite functional.
        /// </summary>
        [Required]
        [JsonPropertyName("analysisId")]
        public Guid AnalysisId { get; set; }

        /// <summary>
        /// The mixture weight for this component, strictly between 0 and 1. Required (per
        /// component) when compositeType is "mixture"; the weights must sum to at most 1, with
        /// any remainder modeled as a point mass at zero. Ignored for "competingRisks" and
        /// computed automatically for "modelAverage".
        /// </summary>
        [JsonPropertyName("weight")]
        public double? Weight { get; set; }
    }
}
