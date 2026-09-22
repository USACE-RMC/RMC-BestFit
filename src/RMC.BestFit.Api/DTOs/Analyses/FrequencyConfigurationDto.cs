using System.Text.Json.Serialization;

namespace RMC.BestFit.Api.DTOs
{
    /// <summary>Effective stationary univariate or B17C settings read from the configured model.</summary>
    /// <remarks>Read again after running: automatic simulation defaults can change at run time.</remarks>
    public class FrequencyConfigurationDto
    {
        /// <summary>The configured parent distribution.</summary>
        [JsonPropertyName("distribution")]
        public string Distribution { get; set; } = string.Empty;

        /// <summary>The configured annual exceedance probabilities.</summary>
        [JsonPropertyName("probabilityOrdinates")]
        public List<double> ProbabilityOrdinates { get; set; } = new();

        /// <summary>Whether the Bayesian model uses default flat parameter priors.</summary>
        [JsonPropertyName("useDefaultFlatPriors")]
        public bool? UseDefaultFlatPriors { get; set; }

        /// <summary>The existing Jeffreys scale-rule setting, when applicable.</summary>
        [JsonPropertyName("useJeffreysRuleForScale")]
        public bool? UseJeffreysRuleForScale { get; set; }

        /// <summary>Whether quantile priors participate in the configured likelihood.</summary>
        [JsonPropertyName("enableQuantilePriors")]
        public bool? EnableQuantilePriors { get; set; }

        /// <summary>The configured single-quantile formulation flag.</summary>
        [JsonPropertyName("useSingleQuantile")]
        public bool? UseSingleQuantile { get; set; }

        /// <summary>Effective parameter priors including fixed flags and untouched default priors.</summary>
        [JsonPropertyName("parameterPriors")]
        public List<ParameterPriorDto>? ParameterPriors { get; set; }

        /// <summary>Active quantile priors; empty when disabled.</summary>
        [JsonPropertyName("quantilePriors")]
        public List<QuantilePriorDto>? QuantilePriors { get; set; }

        /// <summary>Whether run-time data-scaled simulation defaults remain enabled.</summary>
        [JsonPropertyName("useSimulationDefaults")]
        public bool? UseSimulationDefaults { get; set; }

        /// <summary>The effective exposed MCMC options at the time of this response.</summary>
        [JsonPropertyName("bayesianOptions")]
        public BayesianOptionsDto? BayesianOptions { get; set; }

        /// <summary>The B17C uncertainty method.</summary>
        [JsonPropertyName("uncertaintyMethod")]
        public string? UncertaintyMethod { get; set; }

        /// <summary>Enabled B17C parameter penalties, preserving their uncertainty units.</summary>
        [JsonPropertyName("parameterPenalties")]
        public List<ParameterPenaltyDto>? ParameterPenalties { get; set; }

        /// <summary>Enabled B17C quantile penalties, preserving physical or log10 units.</summary>
        [JsonPropertyName("quantilePenalties")]
        public List<QuantilePenaltyDto>? QuantilePenalties { get; set; }
    }
}
