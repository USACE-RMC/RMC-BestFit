using System.Diagnostics;
using System.Globalization;
using System.Xml.Linq;
using Numerics.Sampling.MCMC;
using RMC.BestFit.Models;

namespace RMC.BestFit.Diagnostics
{
    /// <summary>
    /// Provides influence diagnostics for prior components in a Bayesian analysis.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    /// Prior influence diagnostics help understand how different prior components affect the
    /// posterior distribution. This is useful for:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Identifying which priors have the largest impact on estimates</description></item>
    /// <item><description>Detecting prior-data conflict (when data strongly disagrees with priors)</description></item>
    /// <item><description>Assessing prior sensitivity by comparing prior vs. posterior contributions</description></item>
    /// <item><description>Understanding the balance between different prior types</description></item>
    /// </list>
    /// <para>
    /// The diagnostics decompose the prior log-likelihood into components:
    /// </para>
    /// <list type="bullet">
    /// <item><description><c>ParameterPrior</c>: Priors directly on model parameters</description></item>
    /// <item><description><c>QuantilePrior</c>: Priors on derived quantities like quantiles</description></item>
    /// <item><description><c>JeffreysScalePrior</c>: Non-informative Jeffreys priors on scale parameters</description></item>
    /// <item><description><c>SpatialError</c>: Gaussian process priors in spatial models</description></item>
    /// <item><description><c>Jacobian</c>: Jacobian terms from parameter transformations</description></item>
    /// </list>
    /// <para>
    /// A high ratio of prior to data log-likelihood suggests the prior is very influential.
    /// A very negative prior contribution at the posterior mode may indicate prior-data conflict.
    /// </para>
    /// </remarks>
    public class PriorInfluenceDiagnostics
    {
        #region Constructors

        /// <summary>
        /// Creates an empty prior influence diagnostics instance.
        /// </summary>
        public PriorInfluenceDiagnostics()
        {
            Components = Array.Empty<PriorComponentSummary>();
        }

        /// <summary>
        /// Creates prior influence diagnostics from computed values.
        /// </summary>
        /// <param name="components">The prior component summaries.</param>
        /// <exception cref="ArgumentNullException">Thrown when components is null.</exception>
        public PriorInfluenceDiagnostics(PriorComponentSummary[] components)
        {
            Components = components ?? throw new ArgumentNullException(nameof(components));
            ComputeSummaryStatistics();
        }

        /// <summary>
        /// Creates prior influence diagnostics from MCMC posterior samples.
        /// </summary>
        /// <param name="model">The model used for Bayesian analysis.</param>
        /// <param name="results">The MCMC results containing posterior samples.</param>
        /// <param name="thinEvery">Thin posterior samples by this factor. Default is 10.</param>
        /// <exception cref="ArgumentNullException">Thrown when model or results is null.</exception>
        public PriorInfluenceDiagnostics(IModel model, MCMCResults results, int thinEvery = 10)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (results == null) throw new ArgumentNullException(nameof(results));

            ComputeFromPosterior(model, results, thinEvery);
        }

        /// <summary>
        /// Deserializes prior influence diagnostics from an <see cref="XElement"/>.
        /// </summary>
        /// <param name="xElement">The XML element to deserialize.</param>
        /// <exception cref="ArgumentNullException">Thrown when xElement is null.</exception>
        public PriorInfluenceDiagnostics(XElement xElement)
        {
            if (xElement == null) throw new ArgumentNullException(nameof(xElement));

            var componentElements = xElement.Elements("PriorComponentSummary").ToList();
            Components = new PriorComponentSummary[componentElements.Count];

            for (int i = 0; i < componentElements.Count; i++)
            {
                Components[i] = new PriorComponentSummary(componentElements[i]);
            }

            // Read summary statistics
            double.TryParse(xElement.Attribute(nameof(TotalPriorLogLikelihood))?.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var totalPrior);
            TotalPriorLogLikelihood = totalPrior;

            double.TryParse(xElement.Attribute(nameof(TotalDataLogLikelihood))?.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var totalData);
            TotalDataLogLikelihood = totalData;

            double.TryParse(xElement.Attribute(nameof(PriorToDataRatio))?.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var ratio);
            PriorToDataRatio = ratio;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the summary statistics for each prior component type.
        /// </summary>
        public PriorComponentSummary[] Components { get; private set; } = null!;

        /// <summary>
        /// Gets the number of prior component types.
        /// </summary>
        public int Count => Components?.Length ?? 0;

        /// <summary>
        /// Gets the total prior log-likelihood at the posterior point estimate.
        /// </summary>
        public double TotalPriorLogLikelihood { get; private set; }

        /// <summary>
        /// Gets the total data log-likelihood at the posterior point estimate.
        /// </summary>
        public double TotalDataLogLikelihood { get; private set; }

        /// <summary>
        /// Gets the ratio of prior to data log-likelihood magnitude.
        /// </summary>
        /// <remarks>
        /// A ratio close to 0 indicates data-dominated inference (priors have minimal influence).
        /// A ratio close to 1 or higher indicates the priors are strongly influencing the posterior.
        /// This ratio is computed as |prior LL| / (|prior LL| + |data LL|).
        /// <para>
        /// <b>Limitations:</b> log-likelihoods are scale- and parameterization-dependent;
        /// a unit change in the data scale shifts the absolute data LL without
        /// changing posterior inference. Flat priors contribute 0 to the prior LL but
        /// still influence the posterior through their bounds. Prefer
        /// <see cref="PriorPrecisionShare"/> / <see cref="MeanPriorPrecisionShare"/>
        /// for a measure that's invariant under linear reparameterization and bounded in [0, 1].
        /// </para>
        /// </remarks>
        public double PriorToDataRatio { get; private set; }

        /// <summary>
        /// Gets whether the prior appears to be influential based on the prior-to-data ratio.
        /// </summary>
        /// <remarks>
        /// Returns true if the prior contributes more than 20% of the total log-likelihood magnitude.
        /// This is a rough heuristic; the appropriate threshold depends on the application.
        /// </remarks>
        public bool IsPriorInfluential => PriorToDataRatio > 0.2;

        /// <summary>
        /// Per-parameter share of posterior precision contributed by the prior, in [0, 1].
        /// 0 → data-dominated (prior contributes negligible information); 1 → prior-dominated.
        /// </summary>
        /// <remarks>
        /// <para>
        /// For parameter <i>i</i>, defined as
        /// <c>PrecisionPrior_i / max(PrecisionPosterior_i, PrecisionPrior_i)</c>, where
        /// precision is the inverse of variance. Linear-reparameterization invariant
        /// (variance scales by the same constant²), bounded in [0, 1], and falls
        /// gracefully to 0 for flat priors (their variance → ∞ → precision → 0).
        /// </para>
        /// <para>
        /// Computed from posterior MCMC samples (posterior variance) and the prior
        /// distribution's analytical variance. Values are clamped to [0, 1] when
        /// the posterior happens to be wider than the prior (rare; signals a
        /// likelihood that's pulling away from the prior — informative against the
        /// prior). For non-finite prior variance (improper / heavy-tailed priors)
        /// the entry is 0.
        /// </para>
        /// </remarks>
        public double[] PriorPrecisionShare { get; private set; } = Array.Empty<double>();

        /// <summary>
        /// Mean of <see cref="PriorPrecisionShare"/> across parameters; a single
        /// scalar in [0, 1] summarizing overall prior influence.
        /// </summary>
        public double MeanPriorPrecisionShare { get; private set; }

        #endregion

        #region Methods

        /// <summary>
        /// Computes prior influence diagnostics from MCMC posterior samples.
        /// </summary>
        private void ComputeFromPosterior(IModel model, MCMCResults results, int thinEvery)
        {
            var samples = results.Output;
            int S = samples.Count;
            int step = Math.Max(1, thinEvery);

            // Collect prior components across samples
            var componentDict = new Dictionary<string, List<double>>();
            var typeDict = new Dictionary<string, PriorComponentType>();
            var dataLogLikelihoods = new List<double>();

            for (int s = 0; s < S; s += step)
            {
                var parameters = samples[s];

                // Get pointwise prior log-likelihood
                var priorComponents = model.PointwisePriorLogLikelihood(parameters.Values);
                foreach (var comp in priorComponents)
                {
                    if (!componentDict.ContainsKey(comp.Name))
                    {
                        componentDict[comp.Name] = new List<double>();
                        typeDict[comp.Name] = comp.Type;
                    }
                    componentDict[comp.Name].Add(comp.LogLikelihood);
                }

                // Get data log-likelihood
                dataLogLikelihoods.Add(model.DataLogLikelihood(parameters.Values));
            }

            // Create component summaries
            var summaries = new List<PriorComponentSummary>();
            foreach (var kvp in componentDict)
            {
                var values = kvp.Value.ToArray();
                summaries.Add(new PriorComponentSummary(
                    name: kvp.Key,
                    type: typeDict[kvp.Key],
                    mean: values.Average(),
                    standardDeviation: ComputeStdDev(values),
                    min: values.Min(),
                    max: values.Max()
                ));
            }

            Components = summaries.ToArray();

            // Compute overall statistics at the posterior mean/mode
            TotalPriorLogLikelihood = Components.Sum(c => c.MeanLogLikelihood);
            TotalDataLogLikelihood = dataLogLikelihoods.Average();
            ComputeSummaryStatistics();
            ComputePriorPrecisionShare(model, results, step);
        }

        /// <summary>
        /// Computes the per-parameter share of posterior precision contributed by
        /// the prior. Reparameterization-invariant under linear changes; bounded
        /// in [0, 1].
        /// </summary>
        /// <param name="model">The model.</param>
        /// <param name="results">The MCMC results.</param>
        /// <param name="step">Thinning step for the posterior samples.</param>
        private void ComputePriorPrecisionShare(IModel model, MCMCResults results, int step)
        {
            int p = model.Parameters.Count;
            if (p == 0 || results.Output == null || results.Output.Count == 0)
            {
                PriorPrecisionShare = Array.Empty<double>();
                MeanPriorPrecisionShare = 0.0;
                return;
            }

            var samples = results.Output;
            int S = samples.Count;

            // Posterior variance per parameter from the MCMC samples.
            var postVar = new double[p];
            for (int i = 0; i < p; i++)
            {
                double mean = 0.0;
                int count = 0;
                for (int s = 0; s < S; s += step)
                {
                    mean += samples[s].Values[i];
                    count++;
                }
                if (count <= 1) { postVar[i] = double.PositiveInfinity; continue; }
                mean /= count;
                double sumSq = 0.0;
                for (int s = 0; s < S; s += step)
                {
                    double d = samples[s].Values[i] - mean;
                    sumSq += d * d;
                }
                postVar[i] = sumSq / (count - 1);
            }

            // Prior variance per parameter — from the prior distribution's analytical
            // Variance property when finite, otherwise treated as infinite (flat /
            // improper prior contributes zero precision).
            var priorVar = new double[p];
            for (int i = 0; i < p; i++)
            {
                var prior = model.Parameters[i].PriorDistribution;
                double v = double.PositiveInfinity;
                try { v = prior.Variance; }
                catch (Exception ex)
                {
                    // Improper prior — Variance is undefined (e.g., flat / Jeffreys-1/σ).
                    // Treated as positive infinity by the fall-through.
                    Debug.WriteLine($"PriorInfluenceDiagnostics: prior {model.Parameters[i].Name}.Variance unavailable: {ex.Message}");
                }
                if (!double.IsFinite(v) || v <= 0) v = double.PositiveInfinity;
                priorVar[i] = v;
            }

            // Share: precision_prior / max(precision_post, precision_prior).
            // Clamped to [0, 1].
            PriorPrecisionShare = new double[p];
            double sum = 0.0;
            int finiteCount = 0;
            for (int i = 0; i < p; i++)
            {
                double precPrior = double.IsPositiveInfinity(priorVar[i]) ? 0.0 : 1.0 / priorVar[i];
                double precPost = postVar[i] > 0 ? 1.0 / postVar[i] : 0.0;
                double denom = Math.Max(precPost, precPrior);
                double share = denom > 0 ? precPrior / denom : 0.0;
                if (share < 0) share = 0;
                if (share > 1) share = 1;
                PriorPrecisionShare[i] = share;
                if (double.IsFinite(share)) { sum += share; finiteCount++; }
            }
            MeanPriorPrecisionShare = finiteCount > 0 ? sum / finiteCount : 0.0;
        }

        /// <summary>
        /// Computes summary statistics from the component data.
        /// </summary>
        private void ComputeSummaryStatistics()
        {
            if (Components == null || Components.Length == 0)
            {
                PriorToDataRatio = 0;
                return;
            }

            double absPrior = Math.Abs(TotalPriorLogLikelihood);
            double absData = Math.Abs(TotalDataLogLikelihood);
            double total = absPrior + absData;

            PriorToDataRatio = total > 0 ? absPrior / total : 0;
        }

        /// <summary>
        /// Computes the standard deviation of an array.
        /// </summary>
        private static double ComputeStdDev(double[] values)
        {
            if (values == null || values.Length <= 1)
                return 0;

            double mean = values.Average();
            double sumSq = values.Sum(v => (v - mean) * (v - mean));
            return Math.Sqrt(sumSq / (values.Length - 1));
        }

        /// <summary>
        /// Gets the component summaries for a specific prior type.
        /// </summary>
        /// <param name="type">The prior component type to filter by.</param>
        /// <returns>Array of component summaries matching the specified type.</returns>
        public PriorComponentSummary[] GetComponentsByType(PriorComponentType type)
        {
            if (Components == null)
                return Array.Empty<PriorComponentSummary>();

            return Components.Where(c => c.Type == type).ToArray();
        }

        /// <summary>
        /// Gets the components sorted by their contribution (most negative first).
        /// </summary>
        /// <param name="topN">The maximum number of components to return.</param>
        /// <returns>Array of component summaries sorted by mean log-likelihood (ascending).</returns>
        public PriorComponentSummary[] GetMostConstrainingComponents(int topN = int.MaxValue)
        {
            if (Components == null)
                return Array.Empty<PriorComponentSummary>();

            return Components
                .OrderBy(c => c.MeanLogLikelihood)
                .Take(Math.Min(topN, Components.Length))
                .ToArray();
        }

        /// <summary>
        /// Gets a summary of prior influence by type.
        /// </summary>
        /// <returns>A dictionary mapping prior types to their total mean log-likelihood contribution.</returns>
        public Dictionary<PriorComponentType, double> GetContributionByType()
        {
            var result = new Dictionary<PriorComponentType, double>();

            if (Components == null)
                return result;

            foreach (var comp in Components)
            {
                if (!result.ContainsKey(comp.Type))
                    result[comp.Type] = 0;
                result[comp.Type] += comp.MeanLogLikelihood;
            }

            return result;
        }

        /// <summary>
        /// Gets a human-readable summary of the prior influence diagnostics.
        /// </summary>
        /// <returns>A string describing the prior influence.</returns>
        public string GetSummary()
        {
            if (Components == null || Components.Length == 0)
                return "No prior components available for diagnostics.";

            var lines = new List<string>
            {
                $"Prior Influence Summary:",
                $"  Total Prior LL: {TotalPriorLogLikelihood:F2}",
                $"  Total Data LL: {TotalDataLogLikelihood:F2}",
                $"  Prior/Total Ratio: {PriorToDataRatio:P1}",
                $"  Prior is {(IsPriorInfluential ? "INFLUENTIAL" : "not dominant")}",
                "",
                "Contributions by Type:"
            };

            var byType = GetContributionByType();
            foreach (var kvp in byType.OrderBy(kv => kv.Value))
            {
                lines.Add($"  {kvp.Key}: {kvp.Value:F2}");
            }

            return string.Join(Environment.NewLine, lines);
        }

        /// <summary>
        /// Gets the component summary at the specified index.
        /// </summary>
        /// <param name="index">The zero-based component index.</param>
        /// <returns>The prior component summary.</returns>
        /// <exception cref="IndexOutOfRangeException">Thrown when index is out of range.</exception>
        public PriorComponentSummary this[int index] => Components[index];

        /// <summary>
        /// Serializes the prior influence diagnostics to an <see cref="XElement"/>.
        /// </summary>
        /// <returns>An XML element containing the serialized diagnostics.</returns>
        public XElement ToXElement()
        {
            var element = new XElement("PriorInfluenceDiagnostics",
                new XAttribute(nameof(TotalPriorLogLikelihood), TotalPriorLogLikelihood.ToString(CultureInfo.InvariantCulture)),
                new XAttribute(nameof(TotalDataLogLikelihood), TotalDataLogLikelihood.ToString(CultureInfo.InvariantCulture)),
                new XAttribute(nameof(PriorToDataRatio), PriorToDataRatio.ToString(CultureInfo.InvariantCulture))
            );

            if (Components != null)
            {
                foreach (var comp in Components)
                {
                    element.Add(comp.ToXElement());
                }
            }

            return element;
        }

        #endregion
    }

    /// <summary>
    /// Summary statistics for a prior component across posterior samples.
    /// </summary>
    /// <remarks>
    /// This struct provides statistics about how a specific prior component (e.g., a parameter
    /// prior or Jeffreys scale prior) behaves across the posterior distribution, including
    /// its mean contribution, variability, and range.
    /// </remarks>
    public readonly struct PriorComponentSummary
    {
        /// <summary>
        /// Creates a prior component summary with the specified statistics.
        /// </summary>
        /// <param name="name">The name of the prior component.</param>
        /// <param name="type">The type of prior component.</param>
        /// <param name="mean">The mean log-likelihood across posterior samples.</param>
        /// <param name="standardDeviation">The standard deviation of log-likelihood.</param>
        /// <param name="min">The minimum log-likelihood observed.</param>
        /// <param name="max">The maximum log-likelihood observed.</param>
        public PriorComponentSummary(string name, PriorComponentType type, double mean,
            double standardDeviation, double min, double max)
        {
            Name = name;
            Type = type;
            MeanLogLikelihood = mean;
            StandardDeviation = standardDeviation;
            MinLogLikelihood = min;
            MaxLogLikelihood = max;
        }

        /// <summary>
        /// Deserializes a prior component summary from an <see cref="XElement"/>.
        /// </summary>
        /// <param name="xElement">The XML element to deserialize.</param>
        public PriorComponentSummary(XElement xElement)
        {
            Name = xElement.Attribute(nameof(Name))?.Value ?? "";

            if (xElement.Attribute(nameof(Type)) != null)
            {
                Enum.TryParse(xElement.Attribute(nameof(Type))?.Value, out PriorComponentType type);
                Type = type;
            }
            else
            {
                Type = PriorComponentType.ParameterPrior;
            }

            double.TryParse(xElement.Attribute(nameof(MeanLogLikelihood))?.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var mean);
            MeanLogLikelihood = mean;

            double.TryParse(xElement.Attribute(nameof(StandardDeviation))?.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var stdDev);
            StandardDeviation = stdDev;

            double.TryParse(xElement.Attribute(nameof(MinLogLikelihood))?.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var min);
            MinLogLikelihood = min;

            double.TryParse(xElement.Attribute(nameof(MaxLogLikelihood))?.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var max);
            MaxLogLikelihood = max;
        }

        /// <summary>
        /// Gets the name of this prior component.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the type of this prior component.
        /// </summary>
        public PriorComponentType Type { get; }

        /// <summary>
        /// Gets the mean log-likelihood contribution across posterior samples.
        /// </summary>
        /// <remarks>
        /// More negative values indicate the prior is more constraining. Values close to zero
        /// indicate the prior has minimal influence (either non-informative or well-satisfied).
        /// </remarks>
        public double MeanLogLikelihood { get; }

        /// <summary>
        /// Gets the standard deviation of the log-likelihood across posterior samples.
        /// </summary>
        /// <remarks>
        /// High variability indicates the prior contribution varies significantly across
        /// the posterior distribution, suggesting the prior may be sensitive to parameter values.
        /// </remarks>
        public double StandardDeviation { get; }

        /// <summary>
        /// Gets the minimum (most constraining) log-likelihood observed.
        /// </summary>
        public double MinLogLikelihood { get; }

        /// <summary>
        /// Gets the maximum (least constraining) log-likelihood observed.
        /// </summary>
        public double MaxLogLikelihood { get; }

        /// <summary>
        /// Gets the coefficient of variation (|std dev / mean|).
        /// </summary>
        /// <remarks>
        /// A high CV indicates the prior contribution is highly variable relative to its magnitude.
        /// </remarks>
        public double CoefficientOfVariation =>
            Math.Abs(MeanLogLikelihood) > 1e-10 ? Math.Abs(StandardDeviation / MeanLogLikelihood) : 0;

        /// <summary>
        /// Serializes this prior component summary to an <see cref="XElement"/>.
        /// </summary>
        /// <returns>An XML element containing the serialized summary.</returns>
        public XElement ToXElement()
        {
            return new XElement("PriorComponentSummary",
                new XAttribute(nameof(Name), Name ?? ""),
                new XAttribute(nameof(Type), Type.ToString()),
                new XAttribute(nameof(MeanLogLikelihood), MeanLogLikelihood.ToString(CultureInfo.InvariantCulture)),
                new XAttribute(nameof(StandardDeviation), StandardDeviation.ToString(CultureInfo.InvariantCulture)),
                new XAttribute(nameof(MinLogLikelihood), MinLogLikelihood.ToString(CultureInfo.InvariantCulture)),
                new XAttribute(nameof(MaxLogLikelihood), MaxLogLikelihood.ToString(CultureInfo.InvariantCulture))
            );
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"{Name} ({Type}): Mean={MeanLogLikelihood:F2}, SD={StandardDeviation:F2}, Range=[{MinLogLikelihood:F2}, {MaxLogLikelihood:F2}]";
        }
    }
}
