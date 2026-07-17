using Numerics.Distributions;
using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Helpers;

namespace RMC.BestFit.Api.Mappers
{
    /// <summary>
    /// Materializes <see cref="DistributionSpecDto"/> payloads into model-layer
    /// <see cref="UnivariateDistributionBase"/> instances (and back, for response echoes). Used by
    /// uncertain-data observations, parameter priors, and quantile priors.
    /// </summary>
    public static class DistributionSpecMapper
    {
        /// <summary>
        /// Builds and validates a distribution from its spec.
        /// </summary>
        /// <param name="spec">The client-supplied distribution spec.</param>
        /// <param name="context">The request field the spec came from (e.g., "parameterPriors[0].distribution"), used in error messages.</param>
        /// <returns>The constructed distribution with the supplied parameters applied.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown when the spec is missing, the type cannot be constructed, the parameter count
        /// does not match the distribution's canonical parameter list, or the parameter values are
        /// invalid for the distribution (all mapped to HTTP 400).
        /// </exception>
        public static UnivariateDistributionBase ToDistribution(DistributionSpecDto? spec, string context)
        {
            if (spec == null)
            {
                throw new ArgumentException($"{context}: a distribution spec ({{ type, parameters }}) is required.");
            }

            UnivariateDistributionBase distribution;
            try
            {
                distribution = UnivariateDistributionFactory.CreateDistribution(spec.Type);
            }
            catch (Exception ex)
            {
                throw new ArgumentException(
                    $"{context}: the distribution type '{EnumHelper.ToCamelCase(spec.Type.ToString())}' cannot be used here. " +
                    "See GET api/metadata/enums (priorDistributions) for the accepted types.", ex);
            }

            if (spec.Parameters == null || spec.Parameters.Count != distribution.NumberOfParameters)
            {
                throw new ArgumentException(
                    $"{context}: the {distribution.DisplayName} distribution requires exactly {distribution.NumberOfParameters} " +
                    $"parameter(s) in order [{string.Join(", ", distribution.ParameterNames)}], but {spec.Parameters?.Count ?? 0} were supplied.");
            }

            var invalid = distribution.ValidateParameters(spec.Parameters, throwException: false);
            if (invalid != null)
            {
                throw new ArgumentException($"{context}: {invalid.Message}");
            }
            distribution.SetParameters(spec.Parameters);
            return distribution;
        }

        /// <summary>
        /// Builds the response echo of a distribution: its type and current parameter values in
        /// canonical order.
        /// </summary>
        /// <param name="distribution">The model-layer distribution.</param>
        /// <returns>The spec DTO describing the distribution.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="distribution"/> is null.</exception>
        public static DistributionSpecDto ToSpec(UnivariateDistributionBase distribution)
        {
            ArgumentNullException.ThrowIfNull(distribution);
            return new DistributionSpecDto
            {
                Type = distribution.Type,
                Parameters = distribution.GetParameters.ToList()
            };
        }
    }
}
