using System.Text.Json.Serialization;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>
    /// A perception-threshold (censored) record: over the window from <see cref="StartIndex"/> to
    /// <see cref="EndIndex"/>, floods exceeding <see cref="Value"/> would have been observed, and
    /// <see cref="NumberAbove"/> of them were. Years below the threshold are treated as censored.
    /// Used to incorporate historical and paleoflood information.
    /// </summary>
    public class ThresholdObservationDto
    {
        /// <summary>
        /// The first time index (water year) of the threshold window.
        /// </summary>
        [JsonPropertyName("startIndex")]
        public int StartIndex { get; set; }

        /// <summary>
        /// The last time index (water year) of the threshold window.
        /// </summary>
        [JsonPropertyName("endIndex")]
        public int EndIndex { get; set; }

        /// <summary>
        /// The perception threshold magnitude: floods above this value would have been recorded
        /// during the window.
        /// </summary>
        [JsonPropertyName("value")]
        public double Value { get; set; }

        /// <summary>
        /// The number of observations above the threshold during the window. These are typically
        /// entered separately as exact or interval observations. Default 0.
        /// </summary>
        [JsonPropertyName("numberAbove")]
        public int NumberAbove { get; set; }

        /// <summary>
        /// The number of censored years below the threshold. Computed by the model layer from the
        /// window duration and overlapping observations; populated in responses and ignored in requests.
        /// </summary>
        [JsonPropertyName("numberBelow")]
        public int? NumberBelow { get; set; }

        /// <summary>
        /// The computed plotting position of the threshold. Populated in responses; ignored in requests.
        /// </summary>
        [JsonPropertyName("plottingPosition")]
        public double? PlottingPosition { get; set; }
    }
}
