using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Models;

namespace RMC.BestFit.Api.Mappers
{
    /// <summary>
    /// Applies client-supplied Bayesian prior information onto model-layer objects: informative
    /// parameter priors (any Bayesian model), quantile priors (univariate-family models), and
    /// Bulletin 17C parameter/quantile penalties. Only supplied entries are applied; everything
    /// else keeps the model defaults.
    /// </summary>
    public static class PriorMapper
    {
        /// <summary>
        /// Applies informative parameter priors onto a model's parameters, matched by name.
        /// </summary>
        /// <param name="model">The model whose parameters receive the priors.</param>
        /// <param name="priors">The client-supplied priors; null or empty applies nothing.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="model"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown when a parameter name does not match any model parameter, is named twice, or a
        /// prior distribution spec is invalid (all mapped to HTTP 400).
        /// </exception>
        /// <remarks>
        /// Supplying any prior sets <see cref="ModelBase.UseDefaultFlatPriors"/> to false FIRST,
        /// because data-frame property changes rebuild the parameter list (wiping priors) whenever
        /// that flag is true; setting it false has no side effects. Parameters not named keep the
        /// flat priors already in place, so partial specification works.
        /// </remarks>
        public static void ApplyParameterPriors(ModelBase model, List<ParameterPriorDto>? priors)
        {
            ArgumentNullException.ThrowIfNull(model);
            if (priors == null || priors.Count == 0) return;

            model.UseDefaultFlatPriors = false;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < priors.Count; i++)
            {
                var dto = priors[i];
                if (string.IsNullOrWhiteSpace(dto.ParameterName))
                {
                    throw new ArgumentException($"parameterPriors[{i}]: parameterName is required. " +
                        $"Valid names: {DescribeParameterNames(model)}.");
                }
                if (!seen.Add(dto.ParameterName))
                {
                    throw new ArgumentException($"parameterPriors[{i}]: parameter '{dto.ParameterName}' is named more than once.");
                }

                var matches = model.Parameters.Where(p => NameMatches(p.DisplayName, dto.ParameterName) || NameMatches(p.Name, dto.ParameterName)).ToList();
                if (matches.Count == 0)
                {
                    throw new ArgumentException(
                        $"parameterPriors[{i}]: no model parameter is named '{dto.ParameterName}'. " +
                        $"Valid names: {DescribeParameterNames(model)}.");
                }
                if (matches.Count > 1)
                {
                    throw new ArgumentException(
                        $"parameterPriors[{i}]: '{dto.ParameterName}' matches more than one parameter. " +
                        $"Use the full name. Valid names: {DescribeParameterNames(model)}.");
                }
                var parameter = matches[0];

                parameter.PriorDistribution = DistributionSpecMapper.ToDistribution(dto.Distribution, $"parameterPriors[{i}].distribution");
                if (dto.IsFixed.HasValue)
                {
                    parameter.IsFixed = dto.IsFixed.Value;
                }
            }
        }

        /// <summary>
        /// Applies quantile priors onto a univariate-family model.
        /// </summary>
        /// <param name="model">The model implementing <see cref="IQuantilePriors"/>.</param>
        /// <param name="priors">The client-supplied quantile priors; null or empty applies nothing.</param>
        /// <param name="useSingleQuantile">
        /// True for the single-quantile formulation (Viglione et al., 2013); false or null for one
        /// prior per parent-distribution parameter (Coles and Tawn, 1996; the model default).
        /// </param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="model"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown when an exceedance probability is outside (0, 1) or a prior distribution spec is
        /// invalid (mapped to HTTP 400).
        /// </exception>
        /// <remarks>
        /// Assignment order matters: <see cref="IQuantilePriors.EnableQuantilePriors"/> and
        /// <see cref="IQuantilePriors.UseSingleQuantile"/> re-seed default priors from their
        /// setters, so the client's <see cref="IQuantilePriors.QuantilePriors"/> list is assigned
        /// LAST (its setter processes the priors into likelihood form itself).
        /// </remarks>
        public static void ApplyQuantilePriors(IQuantilePriors model, List<QuantilePriorDto>? priors, bool? useSingleQuantile)
        {
            ArgumentNullException.ThrowIfNull(model);
            if (priors == null || priors.Count == 0) return;

            var list = new List<QuantilePrior>(priors.Count);
            for (int i = 0; i < priors.Count; i++)
            {
                var dto = priors[i];
                if (double.IsNaN(dto.Alpha) || dto.Alpha <= 0d || dto.Alpha >= 1d)
                {
                    throw new ArgumentException(
                        $"quantilePriors[{i}]: alpha must be strictly between 0 and 1 (annual exceedance probability).");
                }
                list.Add(new QuantilePrior(dto.Alpha, DistributionSpecMapper.ToDistribution(dto.Distribution, $"quantilePriors[{i}].distribution")));
            }

            model.EnableQuantilePriors = true;
            if (useSingleQuantile.HasValue)
            {
                model.UseSingleQuantile = useSingleQuantile.Value;
            }
            model.QuantilePriors = list;
        }

        /// <summary>
        /// Applies Bulletin 17C parameter and quantile penalties onto the distribution.
        /// </summary>
        /// <param name="distribution">The Bulletin 17C distribution to receive the penalties.</param>
        /// <param name="parameterPenalties">The client-supplied parameter penalties; null or empty applies nothing.</param>
        /// <param name="quantilePenalties">The client-supplied quantile penalties; null or empty applies nothing.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="distribution"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown when a parameter name does not match, a name is repeated, an exceedance
        /// probability is outside (0, 1), an MSE is not positive, or a log-space penalty has a
        /// non-positive mean (all mapped to HTTP 400).
        /// </exception>
        /// <remarks>
        /// Parameter penalties FILL the distribution's pre-created entries in place: the model
        /// keeps <c>ParameterPenalties</c> index-aligned 1:1 with <c>Parameters</c> (the GMM
        /// penalty function pairs them by index), so the collection is never replaced. Quantile
        /// penalties are self-contained (each carries its own AEP); their collection contents are
        /// replaced in place because the model exposes no public setter.
        /// </remarks>
        public static void ApplyPenalties(
            Bulletin17CDistribution distribution,
            List<ParameterPenaltyDto>? parameterPenalties,
            List<QuantilePenaltyDto>? quantilePenalties)
        {
            ArgumentNullException.ThrowIfNull(distribution);

            if (parameterPenalties is { Count: > 0 })
            {
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < parameterPenalties.Count; i++)
                {
                    var dto = parameterPenalties[i];
                    if (string.IsNullOrWhiteSpace(dto.ParameterName))
                    {
                        throw new ArgumentException($"parameterPenalties[{i}]: parameterName is required. " +
                            $"Valid names: {DescribePenaltyNames(distribution)}.");
                    }
                    if (!seen.Add(dto.ParameterName))
                    {
                        throw new ArgumentException($"parameterPenalties[{i}]: parameter '{dto.ParameterName}' is named more than once.");
                    }
                    ValidatePenaltyNumbers(dto.Mean, dto.Mse, dto.UseLog, $"parameterPenalties[{i}]", "useLog");

                    var matches = distribution.ParameterPenalties.Where(p => NameMatches(p.Name, dto.ParameterName)).ToList();
                    if (matches.Count == 0)
                    {
                        throw new ArgumentException(
                            $"parameterPenalties[{i}]: no distribution parameter is named '{dto.ParameterName}'. " +
                            $"Valid names: {DescribePenaltyNames(distribution)}.");
                    }
                    if (matches.Count > 1)
                    {
                        throw new ArgumentException(
                            $"parameterPenalties[{i}]: '{dto.ParameterName}' matches more than one parameter. " +
                            $"Use the full name. Valid names: {DescribePenaltyNames(distribution)}.");
                    }
                    var entry = matches[0];

                    entry.Mean = dto.Mean;
                    entry.MSE = dto.Mse;
                    entry.UseLog = dto.UseLog;
                    entry.Enabled = true;
                }
            }

            if (quantilePenalties is { Count: > 0 })
            {
                var list = new List<QuantilePenalty>(quantilePenalties.Count);
                for (int i = 0; i < quantilePenalties.Count; i++)
                {
                    var dto = quantilePenalties[i];
                    if (double.IsNaN(dto.Aep) || dto.Aep <= 0d || dto.Aep >= 1d)
                    {
                        throw new ArgumentException(
                            $"quantilePenalties[{i}]: aep must be strictly between 0 and 1 (annual exceedance probability).");
                    }
                    ValidatePenaltyNumbers(dto.Mean, dto.Mse, dto.UseLog10, $"quantilePenalties[{i}]", "useLog10");

                    list.Add(new QuantilePenalty
                    {
                        Enabled = true,
                        AEP = dto.Aep,
                        Mean = dto.Mean,
                        MSE = dto.Mse,
                        UseLog10 = dto.UseLog10
                    });
                }
                // The collection setter is private (the model pre-creates one disabled entry in
                // its constructors), so replace the contents in place. PropertyChanged wiring on
                // the new entries is irrelevant here: analysis resources are immutable after
                // creation, so nothing edits penalties later.
                distribution.QuantilePenalties.Clear();
                distribution.QuantilePenalties.AddRange(list);
            }
        }

        /// <summary>
        /// Validates the numeric fields shared by both penalty kinds.
        /// </summary>
        /// <param name="mean">The prior mean.</param>
        /// <param name="mse">The prior mean squared error.</param>
        /// <param name="useLog">The log-space flag value.</param>
        /// <param name="context">The request field the penalty came from, used in error messages.</param>
        /// <param name="logFlagName">The JSON name of the log-space flag ("useLog" or "useLog10").</param>
        /// <exception cref="ArgumentException">Thrown when a value is invalid.</exception>
        private static void ValidatePenaltyNumbers(double mean, double mse, bool useLog, string context, string logFlagName)
        {
            if (!double.IsFinite(mean))
            {
                throw new ArgumentException($"{context}: mean must be a finite number.");
            }
            if (!double.IsFinite(mse) || mse <= 0d)
            {
                throw new ArgumentException($"{context}: mse must be a finite number greater than 0.");
            }
            if (useLog && mean <= 0d)
            {
                throw new ArgumentException($"{context}: mean must be greater than 0 when {logFlagName} is true.");
            }
        }

        /// <summary>
        /// Compares a model parameter name against a client-supplied name, case-insensitively.
        /// The model's names carry a trailing short-form group (e.g., "Skew (of log) (γ)"); the
        /// comparison also accepts the name without it so clients need not type Greek letters.
        /// </summary>
        /// <param name="candidate">The model-side parameter name.</param>
        /// <param name="clientName">The client-supplied name.</param>
        /// <returns>True when the names match.</returns>
        private static bool NameMatches(string candidate, string clientName)
        {
            return string.Equals(candidate, clientName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(StripShortForm(candidate), clientName, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Removes a trailing short-form marker like " (γ)" from a parameter name. Only a final
        /// parenthesized group of at most three characters is treated as a short form, so
        /// semantic groups like "(of log)" are never stripped.
        /// </summary>
        /// <param name="name">The parameter name.</param>
        /// <returns>The name without its short-form marker, or the name unchanged.</returns>
        private static string StripShortForm(string name)
        {
            int open = name.LastIndexOf(" (", StringComparison.Ordinal);
            if (open > 0 && name.EndsWith(')') && name.Length - open - 3 <= 3)
            {
                return name[..open];
            }
            return name;
        }

        /// <summary>
        /// Lists the model's parameter display names for error messages.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <returns>The quoted, comma-separated display names.</returns>
        private static string DescribeParameterNames(ModelBase model)
        {
            return string.Join(", ", model.Parameters.Select(p => $"'{p.DisplayName}'"));
        }

        /// <summary>
        /// Lists the distribution's penalty entry names for error messages.
        /// </summary>
        /// <param name="distribution">The Bulletin 17C distribution.</param>
        /// <returns>The quoted, comma-separated penalty names.</returns>
        private static string DescribePenaltyNames(Bulletin17CDistribution distribution)
        {
            return string.Join(", ", distribution.ParameterPenalties.Select(p => $"'{p.Name}'"));
        }
    }
}
