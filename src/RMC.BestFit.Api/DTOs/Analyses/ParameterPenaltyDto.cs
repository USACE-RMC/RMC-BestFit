using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>
    /// A Gaussian penalty on a distribution parameter in a Bulletin 17C analysis — the Bulletin's
    /// mechanism for incorporating prior (typically regional) information into the Expected
    /// Moments Algorithm fit. The canonical use is a regional skew: penalize the station skew
    /// toward the regional value with strength inversely proportional to its mean squared error.
    /// </summary>
    /// <remarks>
    /// The penalty adds 0.5 × (parameter − mean)² / (mse × n) to the GMM objective, where n is
    /// the total record length. With useLog=true the penalty is computed in log space via the
    /// delta method (appropriate for strictly positive parameters).
    /// </remarks>
    public class ParameterPenaltyDto
    {
        /// <summary>
        /// The name of the parameter the penalty applies to, matched case-insensitively against
        /// the fitted distribution's parameter names (e.g., "Skew (of log)" for a Log-Pearson
        /// Type III regional skew). The names per distribution are listed by
        /// GET api/metadata/distributions.
        /// </summary>
        [Required]
        [JsonPropertyName("parameterName")]
        public string ParameterName { get; set; } = string.Empty;

        /// <summary>
        /// The prior mean for the parameter in real space (e.g., the regional skew value).
        /// </summary>
        [JsonPropertyName("mean")]
        public double Mean { get; set; }

        /// <summary>
        /// The mean squared error (variance) of the prior mean. Must be greater than 0; smaller
        /// values pull the estimate more strongly toward the mean (e.g., the regional skew MSE
        /// from Bulletin 17B/17C skew maps).
        /// </summary>
        [JsonPropertyName("mse")]
        public double Mse { get; set; }

        /// <summary>
        /// True to compute the penalty in log space via the delta method — use for strictly
        /// positive parameters. Requires mean &gt; 0. Default false (real space).
        /// </summary>
        [JsonPropertyName("useLog")]
        public bool UseLog { get; set; }
    }
}
