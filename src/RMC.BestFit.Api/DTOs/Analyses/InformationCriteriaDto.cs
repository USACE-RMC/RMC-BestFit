using System.Text.Json.Serialization;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>
    /// Model-fit information criteria and error measures. Lower is better for all criteria;
    /// values undefined for the estimation method are null.
    /// </summary>
    public class InformationCriteriaDto
    {
        /// <summary>
        /// Akaike information criterion.
        /// </summary>
        [JsonPropertyName("aic")]
        public double? Aic { get; set; }

        /// <summary>
        /// Bayesian information criterion.
        /// </summary>
        [JsonPropertyName("bic")]
        public double? Bic { get; set; }

        /// <summary>
        /// Deviance information criterion.
        /// </summary>
        [JsonPropertyName("dic")]
        public double? Dic { get; set; }

        /// <summary>
        /// Widely applicable information criterion (Bayesian MCMC only).
        /// </summary>
        [JsonPropertyName("waic")]
        public double? Waic { get; set; }

        /// <summary>
        /// Effective number of parameters behind WAIC (Bayesian MCMC only).
        /// </summary>
        [JsonPropertyName("waicPD")]
        public double? WaicPD { get; set; }

        /// <summary>
        /// Leave-one-out cross-validation information criterion with Pareto-smoothed importance
        /// sampling (Bayesian MCMC only).
        /// </summary>
        [JsonPropertyName("looic")]
        public double? Looic { get; set; }

        /// <summary>
        /// Standard error of LOOIC (Bayesian MCMC only).
        /// </summary>
        [JsonPropertyName("looicSE")]
        public double? LooicSE { get; set; }

        /// <summary>
        /// Root mean square error of the fit against plotting positions.
        /// </summary>
        [JsonPropertyName("rmse")]
        public double? Rmse { get; set; }

        /// <summary>
        /// Effective record length in years (Bulletin 17C).
        /// </summary>
        [JsonPropertyName("erl")]
        public double? Erl { get; set; }
    }
}
