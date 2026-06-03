using System.Globalization;
using System.Xml.Linq;
using RMC.BestFit.Models;

namespace RMC.BestFit.Diagnostics
{
    /// <summary>
    /// Provides influence diagnostics for individual observations in a Bayesian analysis.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    /// Influence diagnostics help identify observations that have a disproportionately large
    /// impact on the model's posterior estimates. This is useful for:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Detecting potential outliers that may distort inference</description></item>
    /// <item><description>Identifying highly leveraged observations</description></item>
    /// <item><description>Assessing the reliability of leave-one-out cross-validation estimates</description></item>
    /// <item><description>Understanding which data points drive model conclusions</description></item>
    /// </list>
    /// <para>
    /// The key diagnostic is the Pareto k value from PSIS-LOO (Pareto Smoothed Importance Sampling
    /// Leave-One-Out cross-validation). This value indicates how influential each observation is
    /// on the posterior distribution:
    /// </para>
    /// <list type="bullet">
    /// <item><description>k &lt; 0.5: Good - observation is not overly influential</description></item>
    /// <item><description>0.5 ≤ k &lt; 0.7: OK - moderate influence, estimates may be slightly biased</description></item>
    /// <item><description>0.7 ≤ k &lt; 1.0: Bad - high influence, PSIS-LOO estimates may be unreliable</description></item>
    /// <item><description>k ≥ 1.0: Very bad - observation dominates posterior, consider exact LOO-CV</description></item>
    /// </list>
    /// <para>
    /// References:
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// Vehtari, A., Gelman, A., and Gabry, J. (2017). Practical Bayesian model evaluation using
    /// leave-one-out cross-validation and WAIC. Statistics and Computing, 27(5), 1413-1432.
    /// </description></item>
    /// <item><description>
    /// Vehtari, A., Simpson, D., Gelman, A., Yao, Y., and Gabry, J. (2022). Pareto smoothed
    /// importance sampling. arXiv:1507.02646.
    /// </description></item>
    /// </list>
    /// </remarks>
    public class InfluenceDiagnostics
    {
        #region Constructors

        /// <summary>
        /// Creates an empty influence diagnostics instance.
        /// </summary>
        public InfluenceDiagnostics()
        {
            Observations = Array.Empty<ObservationInfluence>();
            MeanParetoK = double.NaN;
            MaxParetoK = double.NaN;
        }

        /// <summary>
        /// Creates influence diagnostics from computed values.
        /// </summary>
        /// <param name="observations">The influence metrics for each observation.</param>
        /// <exception cref="ArgumentNullException">Thrown when observations is null.</exception>
        public InfluenceDiagnostics(ObservationInfluence[] observations)
        {
            Observations = observations ?? throw new ArgumentNullException(nameof(observations));
            ComputeSummaryStatistics();
        }

        /// <summary>
        /// Creates influence diagnostics from PSIS-LOO results.
        /// </summary>
        /// <param name="paretoK">The Pareto k diagnostic values for each observation.</param>
        /// <param name="elpdLoo">The pointwise expected log predictive density (ELPD-LOO) contributions.</param>
        /// <param name="dataComponents">Optional data component metadata for each observation.</param>
        /// <exception cref="ArgumentNullException">Thrown when paretoK or elpdLoo is null.</exception>
        /// <exception cref="ArgumentException">Thrown when array lengths don't match.</exception>
        public InfluenceDiagnostics(double[] paretoK, double[] elpdLoo, List<DataComponent>? dataComponents = null)
        {
            if (paretoK == null) throw new ArgumentNullException(nameof(paretoK));
            if (elpdLoo == null) throw new ArgumentNullException(nameof(elpdLoo));
            if (paretoK.Length != elpdLoo.Length)
                throw new ArgumentException("Array lengths must match.", nameof(elpdLoo));
            if (dataComponents != null && dataComponents.Count != paretoK.Length)
                throw new ArgumentException("DataComponents count must match array lengths.", nameof(dataComponents));

            int n = paretoK.Length;
            Observations = new ObservationInfluence[n];

            for (int i = 0; i < n; i++)
            {
                var dataComp = dataComponents?[i];
                Observations[i] = new ObservationInfluence(
                    index: i,
                    paretoK: paretoK[i],
                    elpdLoo: elpdLoo[i],
                    value: dataComp?.Value ?? double.NaN,
                    dataType: dataComp?.Type ?? DataComponentType.Exact,
                    count: dataComp?.Count ?? 1,
                    name: dataComp?.Name
                );
            }

            ComputeSummaryStatistics();
        }

        /// <summary>
        /// Deserializes influence diagnostics from an <see cref="XElement"/>.
        /// </summary>
        /// <param name="xElement">The XML element to deserialize.</param>
        /// <exception cref="ArgumentNullException">Thrown when xElement is null.</exception>
        public InfluenceDiagnostics(XElement xElement)
        {
            if (xElement == null) throw new ArgumentNullException(nameof(xElement));

            var observationElements = xElement.Elements("Observation").ToList();
            Observations = new ObservationInfluence[observationElements.Count];

            for (int i = 0; i < observationElements.Count; i++)
            {
                Observations[i] = new ObservationInfluence(observationElements[i]);
            }

            // Read summary statistics if present, otherwise recompute
            if (xElement.Attribute(nameof(MeanParetoK)) != null)
            {
                double.TryParse(xElement.Attribute(nameof(MeanParetoK))?.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var meanK);
                MeanParetoK = meanK;
                double.TryParse(xElement.Attribute(nameof(MaxParetoK))?.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var maxK);
                MaxParetoK = maxK;
                int.TryParse(xElement.Attribute(nameof(CountParetoKAbove05))?.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var count05);
                CountParetoKAbove05 = count05;
                int.TryParse(xElement.Attribute(nameof(CountParetoKAbove07))?.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var count07);
                CountParetoKAbove07 = count07;
                int.TryParse(xElement.Attribute(nameof(CountParetoKAbove10))?.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var count10);
                CountParetoKAbove10 = count10;
            }
            else
            {
                ComputeSummaryStatistics();
            }
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the influence metrics for each observation.
        /// </summary>
        public ObservationInfluence[] Observations { get; private set; }

        /// <summary>
        /// Gets the number of observations.
        /// </summary>
        public int Count => Observations?.Length ?? 0;

        /// <summary>
        /// Gets the mean Pareto k value across all observations.
        /// </summary>
        /// <remarks>
        /// A high mean Pareto k (above 0.5) suggests the model may have systematic
        /// problems fitting the data or that the posterior is sensitive to many observations.
        /// </remarks>
        public double MeanParetoK { get; private set; }

        /// <summary>
        /// Gets the maximum Pareto k value across all observations.
        /// </summary>
        /// <remarks>
        /// The observation with the maximum Pareto k is the most influential point.
        /// Use <see cref="GetMostInfluentialObservations"/> to find multiple influential points.
        /// </remarks>
        public double MaxParetoK { get; private set; }

        /// <summary>
        /// Gets the count of observations with Pareto k ≥ 0.5 (moderately influential).
        /// </summary>
        public int CountParetoKAbove05 { get; private set; }

        /// <summary>
        /// Gets the count of observations with Pareto k ≥ 0.7 (highly influential).
        /// </summary>
        /// <remarks>
        /// If this count is high (e.g., more than 1-2% of observations), the PSIS-LOO
        /// estimates may be unreliable. Consider using exact LOO-CV or WAIC instead.
        /// </remarks>
        public int CountParetoKAbove07 { get; private set; }

        /// <summary>
        /// Gets the count of observations with Pareto k ≥ 1.0 (problematic).
        /// </summary>
        /// <remarks>
        /// Observations with k ≥ 1.0 indicate that the posterior is dominated by that
        /// observation. The PSIS-LOO estimate for these points is unreliable and exact
        /// LOO-CV should be used if possible.
        /// </remarks>
        public int CountParetoKAbove10 { get; private set; }

        /// <summary>
        /// Gets the proportion of observations with Pareto k ≥ 0.7.
        /// </summary>
        public double ProportionProblematic => Count > 0 ? (double)CountParetoKAbove07 / Count : 0.0;

        /// <summary>
        /// Gets whether the PSIS-LOO estimates are reliable based on Pareto k diagnostics.
        /// </summary>
        /// <remarks>
        /// Returns true if fewer than 1% of observations have k ≥ 0.7 and no observations
        /// have k ≥ 1.0. This is a conservative threshold; some practitioners use k ≥ 0.5.
        /// </remarks>
        public bool IsReliable => ProportionProblematic < 0.01 && CountParetoKAbove10 == 0;

        #endregion

        #region Methods

        /// <summary>
        /// Computes summary statistics from the observation data.
        /// </summary>
        private void ComputeSummaryStatistics()
        {
            if (Observations == null || Observations.Length == 0)
            {
                MeanParetoK = double.NaN;
                MaxParetoK = double.NaN;
                CountParetoKAbove05 = 0;
                CountParetoKAbove07 = 0;
                CountParetoKAbove10 = 0;
                return;
            }

            double sum = 0;
            double max = double.NegativeInfinity;
            int count05 = 0, count07 = 0, count10 = 0;
            int validCount = 0;

            foreach (var obs in Observations)
            {
                double k = obs.ParetoK;
                if (!double.IsNaN(k))
                {
                    sum += k;
                    validCount++;
                    if (k > max) max = k;
                    if (k >= 0.5) count05++;
                    if (k >= 0.7) count07++;
                    if (k >= 1.0) count10++;
                }
            }

            // Handle case where all Pareto k values are NaN
            if (validCount == 0)
            {
                MeanParetoK = double.NaN;
                MaxParetoK = double.NaN;
            }
            else
            {
                MeanParetoK = sum / validCount;
                MaxParetoK = max;
            }
            CountParetoKAbove05 = count05;
            CountParetoKAbove07 = count07;
            CountParetoKAbove10 = count10;
        }

        /// <summary>
        /// Gets the observations sorted by influence (highest Pareto k first).
        /// </summary>
        /// <param name="topN">The maximum number of observations to return. Default is all.</param>
        /// <returns>Array of observations sorted by descending Pareto k value.</returns>
        public ObservationInfluence[] GetMostInfluentialObservations(int topN = int.MaxValue)
        {
            if (Observations == null || Observations.Length == 0)
                return Array.Empty<ObservationInfluence>();

            return Observations
                .OrderByDescending(o => o.ParetoK)
                .Take(Math.Min(topN, Observations.Length))
                .ToArray();
        }

        /// <summary>
        /// Gets observations with Pareto k above the specified threshold.
        /// </summary>
        /// <param name="threshold">The Pareto k threshold. Default is 0.7.</param>
        /// <returns>Array of observations with Pareto k ≥ threshold.</returns>
        public ObservationInfluence[] GetProblematicObservations(double threshold = 0.7)
        {
            if (Observations == null || Observations.Length == 0)
                return Array.Empty<ObservationInfluence>();

            return Observations
                .Where(o => o.ParetoK >= threshold)
                .OrderByDescending(o => o.ParetoK)
                .ToArray();
        }

        /// <summary>
        /// Gets the reliability category based on Pareto k diagnostics.
        /// </summary>
        /// <returns>A string describing the reliability of the PSIS-LOO estimates.</returns>
        public string GetReliabilitySummary()
        {
            if (Count == 0)
                return "No observations available for diagnostics.";

            if (CountParetoKAbove10 > 0)
            {
                return $"UNRELIABLE: {CountParetoKAbove10} observation(s) have Pareto k ≥ 1.0. " +
                       "The PSIS-LOO estimates are unreliable. Consider using exact LOO-CV or WAIC.";
            }

            if (ProportionProblematic >= 0.01)
            {
                return $"CAUTION: {CountParetoKAbove07} observation(s) ({ProportionProblematic:P1}) have Pareto k ≥ 0.7. " +
                       "PSIS-LOO estimates may be biased. Consider using exact LOO-CV for problematic points.";
            }

            if (CountParetoKAbove05 > 0)
            {
                return $"OK: {CountParetoKAbove05} observation(s) have moderate influence (0.5 ≤ k < 0.7). " +
                       "PSIS-LOO estimates should be reasonably accurate.";
            }

            return "GOOD: All observations have Pareto k < 0.5. PSIS-LOO estimates are reliable.";
        }

        /// <summary>
        /// Gets the observation at the specified index.
        /// </summary>
        /// <param name="index">The zero-based observation index.</param>
        /// <returns>The observation influence metrics.</returns>
        /// <exception cref="IndexOutOfRangeException">Thrown when index is out of range.</exception>
        public ObservationInfluence this[int index] => Observations[index];

        /// <summary>
        /// Serializes the influence diagnostics to an <see cref="XElement"/>.
        /// </summary>
        /// <returns>An XML element containing the serialized diagnostics.</returns>
        public XElement ToXElement()
        {
            var element = new XElement("InfluenceDiagnostics",
                new XAttribute(nameof(MeanParetoK), MeanParetoK.ToString(CultureInfo.InvariantCulture)),
                new XAttribute(nameof(MaxParetoK), MaxParetoK.ToString(CultureInfo.InvariantCulture)),
                new XAttribute(nameof(CountParetoKAbove05), CountParetoKAbove05.ToString(CultureInfo.InvariantCulture)),
                new XAttribute(nameof(CountParetoKAbove07), CountParetoKAbove07.ToString(CultureInfo.InvariantCulture)),
                new XAttribute(nameof(CountParetoKAbove10), CountParetoKAbove10.ToString(CultureInfo.InvariantCulture))
            );

            if (Observations != null)
            {
                foreach (var obs in Observations)
                {
                    element.Add(obs.ToXElement());
                }
            }

            return element;
        }

        #endregion
    }

    /// <summary>
    /// Represents influence diagnostics for a single observation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each observation's influence is characterized by:
    /// </para>
    /// <list type="bullet">
    /// <item><description><c>ParetoK</c>: The Pareto k diagnostic from PSIS-LOO indicating posterior sensitivity</description></item>
    /// <item><description><c>ElpdLoo</c>: The leave-one-out expected log predictive density contribution</description></item>
    /// <item><description><c>Value</c>: The representative data value (exact, mean, midpoint, or threshold)</description></item>
    /// <item><description><c>DataType</c>: The type of observation (exact, uncertain, interval, censored)</description></item>
    /// </list>
    /// </remarks>
    public readonly struct ObservationInfluence
    {
        /// <summary>
        /// Creates an observation influence with the specified metrics.
        /// </summary>
        /// <param name="index">The zero-based observation index.</param>
        /// <param name="paretoK">The Pareto k diagnostic value.</param>
        /// <param name="elpdLoo">The pointwise ELPD-LOO contribution.</param>
        /// <param name="value">The representative data value.</param>
        /// <param name="dataType">The type of data observation.</param>
        /// <param name="count">The count for threshold observations.</param>
        /// <param name="name">Optional label for the observation.</param>
        public ObservationInfluence(int index, double paretoK, double elpdLoo, double value = double.NaN,
            DataComponentType dataType = DataComponentType.Exact, int count = 1, string? name = null)
        {
            Index = index;
            ParetoK = paretoK;
            ElpdLoo = elpdLoo;
            Value = value;
            DataType = dataType;
            Count = count;
            Name = name;
        }

        /// <summary>
        /// Deserializes an observation influence from an <see cref="XElement"/>.
        /// </summary>
        /// <param name="xElement">The XML element to deserialize.</param>
        public ObservationInfluence(XElement xElement)
        {
            int.TryParse(xElement.Attribute(nameof(Index))?.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var index);
            Index = index;

            double.TryParse(xElement.Attribute(nameof(ParetoK))?.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var paretoK);
            ParetoK = paretoK;

            double.TryParse(xElement.Attribute(nameof(ElpdLoo))?.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var elpdLoo);
            ElpdLoo = elpdLoo;

            double.TryParse(xElement.Attribute(nameof(Value))?.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var value);
            Value = value;

            if (xElement.Attribute(nameof(DataType)) != null)
            {
                Enum.TryParse(xElement.Attribute(nameof(DataType))?.Value, out DataComponentType dataType);
                DataType = dataType;
            }
            else
                DataType = DataComponentType.Exact;

            int.TryParse(xElement.Attribute(nameof(Count))?.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var count);
            Count = count > 0 ? count : 1;

            Name = xElement.Attribute(nameof(Name))?.Value;
        }

        /// <summary>
        /// Gets the zero-based index of this observation in the data.
        /// </summary>
        public int Index { get; }

        /// <summary>
        /// Gets the Pareto k diagnostic value for this observation.
        /// </summary>
        /// <remarks>
        /// <para>Interpretation:</para>
        /// <list type="bullet">
        /// <item><description>k &lt; 0.5: Good - observation is not overly influential</description></item>
        /// <item><description>0.5 ≤ k &lt; 0.7: OK - moderate influence</description></item>
        /// <item><description>0.7 ≤ k &lt; 1.0: Bad - high influence, PSIS-LOO may be unreliable</description></item>
        /// <item><description>k ≥ 1.0: Very bad - observation dominates posterior</description></item>
        /// </list>
        /// </remarks>
        public double ParetoK { get; }

        /// <summary>
        /// Gets the pointwise ELPD-LOO (expected log predictive density) contribution.
        /// </summary>
        /// <remarks>
        /// The ELPD-LOO measures the predictive accuracy for this observation when it is left out
        /// during model fitting. More negative values indicate the model has difficulty predicting
        /// this observation, which may indicate an outlier or model misspecification.
        /// </remarks>
        public double ElpdLoo { get; }

        /// <summary>
        /// Gets the representative value for this observation.
        /// </summary>
        /// <remarks>
        /// For exact data, this is the observed value. For uncertain data, this is the mean.
        /// For interval data, this is the midpoint. For censored data, this is the threshold.
        /// </remarks>
        public double Value { get; }

        /// <summary>
        /// Gets the type of data observation.
        /// </summary>
        public DataComponentType DataType { get; }

        /// <summary>
        /// Gets the count for this observation (typically 1, but may be higher for threshold data).
        /// </summary>
        public int Count { get; }

        /// <summary>
        /// Gets the optional name or label for this observation.
        /// </summary>
        public string? Name { get; }

        /// <summary>
        /// Gets the diagnostic category based on the Pareto k value.
        /// </summary>
        public ParetoKCategory Category
        {
            get
            {
                if (ParetoK < 0.5) return ParetoKCategory.Good;
                if (ParetoK < 0.7) return ParetoKCategory.OK;
                if (ParetoK < 1.0) return ParetoKCategory.Bad;
                return ParetoKCategory.VeryBad;
            }
        }

        /// <summary>
        /// Serializes this observation influence to an <see cref="XElement"/>.
        /// </summary>
        /// <returns>An XML element containing the serialized observation data.</returns>
        public XElement ToXElement()
        {
            var element = new XElement("Observation",
                new XAttribute(nameof(Index), Index.ToString(CultureInfo.InvariantCulture)),
                new XAttribute(nameof(ParetoK), ParetoK.ToString(CultureInfo.InvariantCulture)),
                new XAttribute(nameof(ElpdLoo), ElpdLoo.ToString(CultureInfo.InvariantCulture)),
                new XAttribute(nameof(Value), Value.ToString(CultureInfo.InvariantCulture)),
                new XAttribute(nameof(DataType), DataType.ToString()),
                new XAttribute(nameof(Count), Count.ToString(CultureInfo.InvariantCulture))
            );

            if (!string.IsNullOrEmpty(Name))
            {
                element.Add(new XAttribute(nameof(Name), Name));
            }

            return element;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            var nameStr = string.IsNullOrEmpty(Name) ? $"[{Index}]" : Name;
            return $"{nameStr}: Value={Value:G4}, ParetoK={ParetoK:F3} ({Category}), ELPD={ElpdLoo:F2}";
        }
    }

    /// <summary>
    /// Categorizes the Pareto k diagnostic value.
    /// </summary>
    public enum ParetoKCategory
    {
        /// <summary>
        /// k &lt; 0.5: Observation is not overly influential. PSIS-LOO is reliable.
        /// </summary>
        Good,

        /// <summary>
        /// 0.5 ≤ k &lt; 0.7: Moderate influence. PSIS-LOO estimates may have slight bias.
        /// </summary>
        OK,

        /// <summary>
        /// 0.7 ≤ k &lt; 1.0: High influence. PSIS-LOO estimates may be unreliable for this observation.
        /// </summary>
        Bad,

        /// <summary>
        /// k ≥ 1.0: Observation dominates the posterior. PSIS-LOO is unreliable; use exact LOO-CV.
        /// </summary>
        VeryBad
    }
}
