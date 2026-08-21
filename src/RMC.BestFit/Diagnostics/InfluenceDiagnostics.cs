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
    /// The key diagnostic is the Pareto-k value from Pareto-smoothed importance-sampling
    /// leave-one-out cross-validation. For Bayesian results with <c>S</c> retained draws,
    /// the reliability limit is <c>min(1 - 1 / log10(S), 0.7)</c>. A value at or above
    /// this limit signals that the PSIS approximation for that pointwise unit requires
    /// investigation; a value at or above 1.0 lacks the usual finite-mean guarantee. A Pareto k
    /// that could not be estimated (<see cref="double.NaN"/> or positive infinity) is counted as
    /// exceeding every limit, so it is never reported as reliable.
    /// </para>
    /// <para>
    /// Public constructors retain the historical fixed category thresholds for source and
    /// serialization compatibility. Diagnostics produced by <c>BayesianAnalysis</c> use the
    /// draw-count-specific limit.
    /// </para>
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
        private const double LegacyDiagnosticThreshold = 0.7;
        private const string DiagnosticThresholdAttribute = "ParetoKDiagnosticThreshold";
        private double _diagnosticThreshold = LegacyDiagnosticThreshold;
        private bool _usesSampleSizeDiagnosticThreshold;
        private int _countParetoKAboveDiagnosticThreshold;

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
        /// Creates influence diagnostics from PSIS-LOO results using the legacy fixed thresholds.
        /// </summary>
        /// <param name="paretoK">The Pareto k diagnostic values for each observation.</param>
        /// <param name="elpdLoo">The pointwise expected log predictive density contributions.</param>
        /// <param name="dataComponents">Optional data component metadata for each observation.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="paretoK"/> or <paramref name="elpdLoo"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when array lengths do not match.</exception>
        public InfluenceDiagnostics(double[] paretoK, double[] elpdLoo, List<DataComponent>? dataComponents = null)
            : this(paretoK, elpdLoo, dataComponents, LegacyDiagnosticThreshold, false)
        {
        }

        /// <summary>
        /// Creates Bayesian influence diagnostics with a sample-size-dependent Pareto-k threshold.
        /// </summary>
        /// <param name="paretoK">The Pareto k diagnostic values for each observation.</param>
        /// <param name="elpdLoo">The pointwise expected log predictive density contributions.</param>
        /// <param name="dataComponents">Optional data component metadata for each observation.</param>
        /// <param name="diagnosticThreshold">The draw-count-specific Pareto-k reliability threshold.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="paretoK"/> or <paramref name="elpdLoo"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when array lengths do not match or the threshold is NaN.</exception>
        internal InfluenceDiagnostics(double[] paretoK, double[] elpdLoo,
            List<DataComponent>? dataComponents, double diagnosticThreshold)
            : this(paretoK, elpdLoo, dataComponents, diagnosticThreshold, true)
        {
        }

        /// <summary>
        /// Initializes influence diagnostics with either legacy or sample-size-dependent interpretation.
        /// </summary>
        /// <param name="paretoK">The Pareto k diagnostic values for each observation.</param>
        /// <param name="elpdLoo">The pointwise expected log predictive density contributions.</param>
        /// <param name="dataComponents">Optional data component metadata for each observation.</param>
        /// <param name="diagnosticThreshold">The active Pareto-k reliability threshold.</param>
        /// <param name="usesSampleSizeDiagnosticThreshold">Whether the threshold came from posterior draw count.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="paretoK"/> or <paramref name="elpdLoo"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when array lengths do not match or the threshold is NaN.</exception>
        private InfluenceDiagnostics(double[] paretoK, double[] elpdLoo,
            List<DataComponent>? dataComponents, double diagnosticThreshold,
            bool usesSampleSizeDiagnosticThreshold)
        {
            if (paretoK == null) throw new ArgumentNullException(nameof(paretoK));
            if (elpdLoo == null) throw new ArgumentNullException(nameof(elpdLoo));
            if (paretoK.Length != elpdLoo.Length)
                throw new ArgumentException("Array lengths must match.", nameof(elpdLoo));
            if (dataComponents != null && dataComponents.Count != paretoK.Length)
                throw new ArgumentException("DataComponents count must match array lengths.", nameof(dataComponents));
            if (double.IsNaN(diagnosticThreshold))
                throw new ArgumentException("Diagnostic threshold cannot be NaN.", nameof(diagnosticThreshold));

            _diagnosticThreshold = Math.Min(diagnosticThreshold, LegacyDiagnosticThreshold);
            _usesSampleSizeDiagnosticThreshold = usesSampleSizeDiagnosticThreshold;
            int observationCount = paretoK.Length;
            Observations = new ObservationInfluence[observationCount];

            for (int observationIndex = 0; observationIndex < observationCount; observationIndex++)
            {
                DataComponent? dataComponent = dataComponents?[observationIndex];
                Observations[observationIndex] = new ObservationInfluence(
                    index: observationIndex,
                    paretoK: paretoK[observationIndex],
                    elpdLoo: elpdLoo[observationIndex],
                    diagnosticThreshold: _diagnosticThreshold,
                    usesSampleSizeDiagnosticThreshold: usesSampleSizeDiagnosticThreshold,
                    value: dataComponent?.Value ?? double.NaN,
                    dataType: dataComponent?.Type ?? DataComponentType.Exact,
                    count: dataComponent?.Count ?? 1,
                    name: dataComponent?.Name);
            }

            ComputeSummaryStatistics();
        }
        /// <summary>
        /// Deserializes influence diagnostics from an <see cref="XElement"/>.
        /// </summary>
        /// <param name="xElement">The XML element to deserialize.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="xElement"/> is null.</exception>
        /// <remarks>
        /// The summary statistics are recomputed from the deserialized observations rather than
        /// read from the element, so a file whose observation list was pruned reports the
        /// summaries of the observations it still contains.
        /// </remarks>
        public InfluenceDiagnostics(XElement xElement)
        {
            if (xElement == null) throw new ArgumentNullException(nameof(xElement));

            XAttribute? thresholdAttribute = xElement.Attribute(DiagnosticThresholdAttribute);
            if (thresholdAttribute != null
                && double.TryParse(thresholdAttribute.Value, NumberStyles.Any,
                    CultureInfo.InvariantCulture, out double diagnosticThreshold)
                && !double.IsNaN(diagnosticThreshold))
            {
                _diagnosticThreshold = Math.Min(diagnosticThreshold, LegacyDiagnosticThreshold);
                _usesSampleSizeDiagnosticThreshold = true;
            }

            List<XElement> observationElements = xElement.Elements("Observation").ToList();
            Observations = new ObservationInfluence[observationElements.Count];
            for (int observationIndex = 0; observationIndex < observationElements.Count; observationIndex++)
            {
                Observations[observationIndex] = new ObservationInfluence(
                    observationElements[observationIndex],
                    _diagnosticThreshold,
                    _usesSampleSizeDiagnosticThreshold);
            }

            ComputeSummaryStatistics();
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
        /// Gets whether all PSIS-LOO pointwise estimates satisfy the active Pareto-k reliability limit.
        /// </summary>
        /// <remarks>
        /// Bayesian-analysis results use the draw-count-specific limit
        /// <c>min(1 - 1 / log10(S), 0.7)</c>. Instances created through the historical public
        /// constructors retain the earlier aggregate rule for compatibility.
        /// </remarks>
        public bool IsReliable => _usesSampleSizeDiagnosticThreshold
            ? _countParetoKAboveDiagnosticThreshold == 0
            : ProportionProblematic < 0.01 && CountParetoKAbove10 == 0;

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
                _countParetoKAboveDiagnosticThreshold = 0;
                return;
            }

            double sum = 0;
            double max = double.NegativeInfinity;
            int count05 = 0, count07 = 0, count10 = 0, countDiagnostic = 0;
            int validCount = 0;

            foreach (var obs in Observations)
            {
                double k = obs.ParetoK;
                if (double.IsNaN(k))
                {
                    // A tail fit that produced no estimate is an unreliable pointwise unit:
                    // it exceeds every limit but contributes nothing to the mean or maximum.
                    count05++;
                    count07++;
                    count10++;
                    countDiagnostic++;
                    continue;
                }

                sum += k;
                validCount++;
                if (k > max) max = k;
                if (k >= 0.5) count05++;
                if (k >= 0.7) count07++;
                if (k >= 1.0) count10++;
                if (k >= _diagnosticThreshold) countDiagnostic++;
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
            _countParetoKAboveDiagnosticThreshold = countDiagnostic;
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
        /// Gets observations with Pareto k at or above the instance's own reliability limit: the
        /// draw-count-specific limit for diagnostics produced by <c>BayesianAnalysis</c>, otherwise 0.7.
        /// </summary>
        /// <returns>
        /// Array of observations with Pareto k ≥ the reliability limit, including observations whose
        /// Pareto k could not be estimated (<see cref="double.NaN"/>), ordered from the most to the
        /// least influential.
        /// </returns>
        public ObservationInfluence[] GetProblematicObservations()
        {
            return GetProblematicObservations(
                _usesSampleSizeDiagnosticThreshold ? _diagnosticThreshold : LegacyDiagnosticThreshold);
        }

        /// <summary>
        /// Gets observations with Pareto k at or above the specified threshold.
        /// </summary>
        /// <param name="threshold">The Pareto k threshold. Default is 0.7.</param>
        /// <returns>
        /// Array of observations with Pareto k ≥ threshold, including observations whose Pareto k
        /// could not be estimated (<see cref="double.NaN"/>), ordered from the most to the least
        /// influential.
        /// </returns>
        public ObservationInfluence[] GetProblematicObservations(double threshold = 0.7)
        {
            if (Observations == null || Observations.Length == 0)
                return Array.Empty<ObservationInfluence>();

            return Observations
                .Where(o => double.IsNaN(o.ParetoK) || o.ParetoK >= threshold)
                .OrderByDescending(o => double.IsNaN(o.ParetoK) ? double.PositiveInfinity : o.ParetoK)
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

            if (_usesSampleSizeDiagnosticThreshold)
            {
                if (CountParetoKAbove10 > 0)
                {
                    return $"UNRELIABLE: {CountParetoKAbove10} observation(s) have Pareto k >= 1.0. " +
                           "The corresponding PSIS estimates do not have a finite-mean guarantee.";
                }

                if (_countParetoKAboveDiagnosticThreshold > 0)
                {
                    return $"CAUTION: {_countParetoKAboveDiagnosticThreshold} observation(s) have Pareto k " +
                           $">= {_diagnosticThreshold:F3}, the reliability limit for this posterior sample size.";
                }

                return $"GOOD: All observations have Pareto k < {_diagnosticThreshold:F3}, " +
                       "the reliability limit for this posterior sample size.";
            }

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

            if (_usesSampleSizeDiagnosticThreshold)
            {
                element.Add(new XAttribute(
                    DiagnosticThresholdAttribute,
                    _diagnosticThreshold.ToString(CultureInfo.InvariantCulture)));
            }

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
        private const double LegacyGoodThreshold = 0.5;
        private const string DiagnosticThresholdAttribute = "ParetoKDiagnosticThreshold";
        private readonly double _diagnosticThreshold;
        private readonly bool _usesSampleSizeDiagnosticThreshold;
        /// <summary>
        /// Creates an observation influence with the specified metrics and legacy category thresholds.
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
            : this(index, paretoK, elpdLoo, LegacyGoodThreshold, false,
                  value, dataType, count, name)
        {
        }

        /// <summary>
        /// Creates a Bayesian observation influence using a sample-size-dependent threshold.
        /// </summary>
        /// <param name="index">The zero-based observation index.</param>
        /// <param name="paretoK">The Pareto k diagnostic value.</param>
        /// <param name="elpdLoo">The pointwise ELPD-LOO contribution.</param>
        /// <param name="diagnosticThreshold">The draw-count-specific reliability threshold.</param>
        /// <param name="usesSampleSizeDiagnosticThreshold">Whether to apply the supplied threshold to the category.</param>
        /// <param name="value">The representative data value.</param>
        /// <param name="dataType">The type of data observation.</param>
        /// <param name="count">The count for threshold observations.</param>
        /// <param name="name">Optional label for the observation.</param>
        internal ObservationInfluence(int index, double paretoK, double elpdLoo,
            double diagnosticThreshold, bool usesSampleSizeDiagnosticThreshold,
            double value = double.NaN, DataComponentType dataType = DataComponentType.Exact,
            int count = 1, string? name = null)
        {
            Index = index;
            ParetoK = paretoK;
            ElpdLoo = elpdLoo;
            Value = value;
            DataType = dataType;
            Count = count;
            Name = name;
            _diagnosticThreshold = diagnosticThreshold;
            _usesSampleSizeDiagnosticThreshold = usesSampleSizeDiagnosticThreshold;
        }

        /// <summary>
        /// Deserializes an observation influence from an <see cref="XElement"/>.
        /// </summary>
        /// <param name="xElement">The XML element to deserialize.</param>
        public ObservationInfluence(XElement xElement)
            : this(xElement, LegacyGoodThreshold, false)
        {
        }

        /// <summary>
        /// Deserializes an observation using a parent diagnostic threshold as a fallback.
        /// </summary>
        /// <param name="xElement">The XML element to deserialize.</param>
        /// <param name="diagnosticThreshold">Parent reliability threshold.</param>
        /// <param name="usesSampleSizeDiagnosticThreshold">Whether the parent uses a draw-count threshold.</param>
        internal ObservationInfluence(XElement xElement, double diagnosticThreshold,
            bool usesSampleSizeDiagnosticThreshold)
        {
            int.TryParse(xElement.Attribute(nameof(Index))?.Value, NumberStyles.Any,
                CultureInfo.InvariantCulture, out int index);
            Index = index;

            double.TryParse(xElement.Attribute(nameof(ParetoK))?.Value, NumberStyles.Any,
                CultureInfo.InvariantCulture, out double paretoK);
            ParetoK = paretoK;

            double.TryParse(xElement.Attribute(nameof(ElpdLoo))?.Value, NumberStyles.Any,
                CultureInfo.InvariantCulture, out double elpdLoo);
            ElpdLoo = elpdLoo;

            double.TryParse(xElement.Attribute(nameof(Value))?.Value, NumberStyles.Any,
                CultureInfo.InvariantCulture, out double value);
            Value = value;

            if (xElement.Attribute(nameof(DataType)) != null)
            {
                Enum.TryParse(xElement.Attribute(nameof(DataType))?.Value, out DataComponentType dataType);
                DataType = dataType;
            }
            else
            {
                DataType = DataComponentType.Exact;
            }

            int.TryParse(xElement.Attribute(nameof(Count))?.Value, NumberStyles.Any,
                CultureInfo.InvariantCulture, out int count);
            Count = count > 0 ? count : 1;
            Name = xElement.Attribute(nameof(Name))?.Value;

            XAttribute? thresholdAttribute = xElement.Attribute(DiagnosticThresholdAttribute);
            if (thresholdAttribute != null
                && double.TryParse(thresholdAttribute.Value, NumberStyles.Any,
                    CultureInfo.InvariantCulture, out double observationThreshold)
                && !double.IsNaN(observationThreshold))
            {
                _diagnosticThreshold = observationThreshold;
                _usesSampleSizeDiagnosticThreshold = true;
            }
            else
            {
                _diagnosticThreshold = diagnosticThreshold;
                _usesSampleSizeDiagnosticThreshold = usesSampleSizeDiagnosticThreshold;
            }
        }
        /// <summary>
        /// Gets the zero-based index of this observation in the data.
        /// </summary>
        public int Index { get; }

        /// <summary>
        /// Gets the Pareto-k diagnostic value for this pointwise observation unit.
        /// </summary>
        /// <remarks>
        /// Bayesian-analysis observations are interpreted against the draw-count-specific
        /// reliability limit stored with the diagnostic. Historical standalone observations
        /// retain the fixed 0.5, 0.7, and 1.0 category boundaries.
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
        /// Gets the diagnostic category using the stored draw-count limit or legacy fixed thresholds.
        /// </summary>
        public ParetoKCategory Category
        {
            get
            {
                double goodThreshold = _usesSampleSizeDiagnosticThreshold
                    ? _diagnosticThreshold
                    : LegacyGoodThreshold;
                if (ParetoK < goodThreshold) return ParetoKCategory.Good;
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

            if (_usesSampleSizeDiagnosticThreshold)
            {
                element.Add(new XAttribute(
                    DiagnosticThresholdAttribute,
                    _diagnosticThreshold.ToString(CultureInfo.InvariantCulture)));
            }

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
    /// Categorizes a Pareto-k diagnostic relative to the active reliability boundaries.
    /// </summary>
    public enum ParetoKCategory
    {
        /// <summary>
        /// The value is below the active reliability limit.
        /// </summary>
        Good,

        /// <summary>
        /// The value is at or above the active reliability limit but below 0.7.
        /// </summary>
        OK,

        /// <summary>
        /// The value is at least 0.7 but below 1.0.
        /// </summary>
        Bad,

        /// <summary>
        /// The value is at least 1.0 and lacks the usual finite-mean guarantee.
        /// </summary>
        VeryBad
    }
}
