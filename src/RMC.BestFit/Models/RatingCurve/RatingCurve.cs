using Numerics;
using Numerics.Data;
using Numerics.Data.Statistics;
using Numerics.Distributions;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace RMC.BestFit.Models
{
    /// <summary>
    /// Stage-discharge rating curve model using the BaRatin matrix-of-controls
    /// framework in ADDITION mode (controls accumulate as stage rises).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The rating curve relates river stage (water level) to discharge (flow rate) as
    /// a sum of power-law contributions from the active hydraulic controls:
    /// Q(h) = Σₖ αₖ · (h − ξₖ)^βₖ · 𝟙{h > ξₖ}.
    /// The first control (k=1) is the main/low-flow control active at all stages; each
    /// subsequent control (k = 2, 3) is an additional control that activates at its
    /// own threshold stage hₖ and ADDS its contribution on top of the already-active
    /// controls. This is appropriate for compound channels with overbank transitions —
    /// the overwhelming-majority use case in flood-frequency analysis.
    /// </para>
    /// <para>
    /// <b>Relation to the BaRatin framework.</b> This is the BaRatin master equation
    /// with the lower-triangular-all-ones control matrix M (reference Fortran source:
    /// github.com/BaRatin-tools/BaRatin, src/RatingCurve_tools.f90, ApplyRC_General
    /// lines 464–478, continuity derivation lines 308–326, validity rules lines 191–259):
    /// Q(h) = Σᵣ 𝟙_{[κᵣ, κᵣ₊₁]}(h) · Σⱼ M(r,j) · aⱼ · (h − bⱼ)^cⱼ.
    /// For M = [[1, 0, …], [1, 1, 0, …], [1, 1, 1, …]] the BaRatin continuity
    /// derivation gives bⱼ = κⱼ (the new control turns on at its activation stage),
    /// which reduces the equation to the cumulative sum above with ξⱼ = hⱼ for j ≥ 2.
    /// </para>
    /// <para>
    /// Parameter vector layout (length = 3·NumberOfSegments + 1):
    /// <list type="bullet">
    /// <item>1 segment: [h₁, log₁₀α₁, β₁, σ]</item>
    /// <item>2 segments: [h₁, log₁₀α₁, β₁, h₂, log₁₀α₂, β₂, σ]</item>
    /// <item>3 segments: [h₁, log₁₀α₁, β₁, h₂, log₁₀α₂, β₂, h₃, log₁₀α₃, β₃, σ]</item>
    /// </list>
    /// All αₖ and βₖ are fit directly. The breakpoint hₖ is both the activation stage
    /// of control k and the "b" offset of its power-law term (continuity is automatic
    /// in addition mode). h₁ is the main-channel cease-to-flow stage.
    /// </para>
    /// <para>
    /// Under addition mode, parameters identify well under flat priors: below h₂ only
    /// control 1 is active, so (h₁, α₁, β₁) are pinned by low-flow data; above h₂ the
    /// residual (Qₒbₛ − α₁(h − h₁)^β₁) is a clean power law in (h − h₂), pinning (α₂, β₂).
    /// The segments are decoupled, which avoids the model-misspecification ridge
    /// produced when a single replacement power law is forced to represent the sum of
    /// two physical contributions (e.g., main-channel-continuing + overbank-activating).
    /// </para>
    /// <para>
    /// Errors are modeled in log₁₀-space with residual σ, which accounts for the
    /// multiplicative-error heteroscedasticity typical of stage-discharge measurements.
    /// </para>
    /// <para>
    /// <b>Out of scope (deferred to v3):</b> user-facing control matrix M. Sites with
    /// drowning-out controls (succession mode) or mixed additive/successive matrices
    /// require exposing M and are not supported in this version.
    /// </para>
    /// <para>
    ///     <b>Authors:</b>
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    ///     <b>References:</b>
    ///     - ISO 1100-2:2010. Measurement of liquid flow in open channels – Part 2: Determination of the stage-discharge relationship. §5.2.5.
    ///     - Kennedy, E.J. (1984). Discharge ratings at gaging stations. USGS Techniques of Water-Resources Investigations, Book 3, Ch. A10.
    ///     - Rantz, S.E., et al. (1982). Measurement and computation of streamflow. USGS Water-Supply Paper 2175.
    ///     - WMO-No. 1044 (2010). Manual on Stream Gauging.
    ///     - Le Coz, J., Renard, B., Bonnifait, L., Branger, F., Le Boursicaud, R. (2014). Combining hydraulic knowledge and uncertain gaugings in the estimation of hydrometric rating curves: a Bayesian approach. <i>J. Hydrol.</i> 509:573–587. (BaRatin — canonical source for the matrix-of-controls framework.)
    ///     - Kiang, J.E., et al. (2018). A Comparison of Methods for Streamflow Uncertainty Estimation. <i>Water Resources Research</i> 54:7149–7176.
    ///     - Wickert, A.D., et al. (2024/2025). A double-Manning approach to compute robust rating curves and hydraulic geometries. <i>EGUsphere</i> preprint. (Additive main + floodplain Manning physics.)
    ///     - Reitan, T., &amp; Petersen-Øverleir, A. (2009). Bayesian methods for estimating multi-segment discharge rating curves. <i>Stochastic Environmental Research and Risk Assessment</i> 23:627–642. (Piecewise/succession formulation; appropriate for different physical cases.)
    ///     - BaRatin reference Fortran source: github.com/BaRatin-tools/BaRatin, src/RatingCurve_tools.f90.
    /// </para>
    /// </remarks>
    public class RatingCurve : ModelBase, ISimulatable<double[]>
    {
        #region Construction

        /// <summary>
        /// Constructs a new single-segment rating curve with default parameters.
        /// </summary>
        public RatingCurve()
        {
            _numberOfSegments = 1;
            SetDefaultParameters();
        }

        /// <summary>
        /// Constructs a new rating curve model.
        /// </summary>
        /// <param name="stageData">The stage (water level) time series data.</param>
        /// <param name="dischargeData">The discharge (flow rate) time series data.</param>
        /// <param name="numberOfSegments">The number of rating curve segments (1-3). Default = 1.</param>
        public RatingCurve(TimeSeries stageData, TimeSeries dischargeData, int numberOfSegments = 1)
        {
            StageData = stageData;
            DischargeData = dischargeData;
            NumberOfSegments = numberOfSegments;
            SetDefaultParameters();
        }

        /// <summary>
        /// Constructs a new rating curve model by deserializing from XML.
        /// </summary>
        /// <param name="stageData">The stage time series data.</param>
        /// <param name="dischargeData">The discharge time series data.</param>
        /// <param name="xElement">The XElement containing serialized model configuration.</param>
        public RatingCurve(TimeSeries stageData, TimeSeries dischargeData, XElement xElement)
        {
            StageData = stageData;
            DischargeData = dischargeData;

            var segmentsAttr = xElement.Attribute(nameof(NumberOfSegments));
            if (segmentsAttr != null)
                int.TryParse(segmentsAttr.Value, out _numberOfSegments);
            var flatPriorsAttr = xElement.Attribute(nameof(UseDefaultFlatPriors));
            if (flatPriorsAttr != null)
                bool.TryParse(flatPriorsAttr.Value, out _useDefaultFlatPriors);
            var jeffreysAttr = xElement.Attribute(nameof(UseJeffreysRuleForScale));
            if (jeffreysAttr != null)
                bool.TryParse(jeffreysAttr.Value, out _useJeffreysRuleForScale);

            var parms = new List<ModelParameter>();
            var parmsElement = xElement.Element(nameof(Parameters));
            if (parmsElement != null)
            {
                foreach (XElement p in parmsElement.Elements(nameof(ModelParameter)))
                    parms.Add(new ModelParameter(p));
            }
            Parameters = parms;
        }

        #endregion

        #region Members

        private TimeSeries _stageData = null!;
        private TimeSeries _dischargeData = null!;
        private int _numberOfSegments = 1;
        private bool _useJeffreysRuleForScale = true;

        /// <summary>
        /// Cached result of <see cref="GetAlignedObservations"/>. Built lazily on first
        /// access and invalidated (set to <see langword="null"/>) whenever either
        /// <see cref="StageData"/> or <see cref="DischargeData"/> changes — including
        /// in-place collection mutations. The cache is critical for MCMC performance
        /// because the likelihood is evaluated thousands of times per fit and the
        /// date-based inner-join is O(m + n) per call without it.
        /// </summary>
        private List<(DateTime Date, double Stage, double Discharge)>? _alignedObservationsCache;

        /// <summary>
        /// Gets or sets the stage (water level) time series data.
        /// </summary>
        [Category("Inputs")]
        [DisplayName("Stage Data")]
        [Description("The stage (water level) time series data.")]
        [Browsable(true)]
        public TimeSeries StageData
        {
            get { return _stageData; }
            set
            {
                if (_stageData != null)
                    _stageData.CollectionChanged -= StageData_CollectionChanged;

                _stageData = value;

                if (_stageData != null)
                    _stageData.CollectionChanged += StageData_CollectionChanged;

                _alignedObservationsCache = null;
                RaisePropertyChange(nameof(StageData));
                if (_stageData != null && UseDefaultFlatPriors)
                    SetDefaultParameters();
            }
        }

        /// <summary>
        /// Gets or sets the discharge (flow rate) time series data.
        /// </summary>
        [Category("Inputs")]
        [DisplayName("Discharge Data")]
        [Description("The discharge (flow rate) time series data.")]
        [Browsable(true)]
        public TimeSeries DischargeData
        {
            get { return _dischargeData; }
            set
            {
                if (_dischargeData != null)
                    _dischargeData.CollectionChanged -= DischargeData_CollectionChanged;

                _dischargeData = value;

                if (_dischargeData != null)
                    _dischargeData.CollectionChanged += DischargeData_CollectionChanged;

                _alignedObservationsCache = null;
                RaisePropertyChange(nameof(DischargeData));
                if (_dischargeData != null && UseDefaultFlatPriors)
                    SetDefaultParameters();
            }
        }

        /// <summary>
        /// Gets or sets the number of rating curve segments (1-3).
        /// </summary>
        [Category("Inputs")]
        [DisplayName("Number of Segments")]
        [Description("The number of hydraulic controls in the rating curve. Additional controls (k ≥ 2) activate at stage h_k and contribute additively to discharge. Appropriate for compound channels with overbank transitions. Range: 1-3.")]
        [Browsable(true)]
        public int NumberOfSegments
        {
            get { return _numberOfSegments; }
            set
            {
                if (_numberOfSegments != value)
                {
                    _numberOfSegments = value;
                    // Rebuild the parameter list BEFORE raising the change event.
                    // Downstream PropertyChanged handlers (ClearResults → SetIsValid →
                    // Validate → ValidateSegmentOrdering) read Parameters by index
                    // using the new segment count; leaving the old parameter list in
                    // place during the notification produces an IndexOutOfRangeException
                    // when, e.g., transitioning from 1 → 2 segments.
                    SetDefaultParameters();
                    RaisePropertyChange(nameof(NumberOfSegments));
                }
            }
        }

        /// <summary>
        /// Gets or sets whether to use Jeffreys' rule prior (1/σ) for the scale parameter.
        /// </summary>
        [Category("Inputs")]
        [DisplayName("Use Jeffreys' Rule for Scale")]
        [Description("Determines whether to use Jeffreys' rule for the scale (σ) parameter: P(σ) ∝ 1/σ. This is a non-informative prior appropriate for scale parameters.")]
        [Browsable(true)]
        public bool UseJeffreysRuleForScale
        {
            get { return _useJeffreysRuleForScale; }
            set
            {
                if (_useJeffreysRuleForScale != value)
                {
                    _useJeffreysRuleForScale = value;
                    RaisePropertyChange(nameof(UseJeffreysRuleForScale));
                }
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Handles changes to the stage data collection.
        /// </summary>
        private void StageData_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            _alignedObservationsCache = null;
            RaisePropertyChange(nameof(StageData));
            if (UseDefaultFlatPriors)
                SetDefaultParameters();
        }

        /// <summary>
        /// Handles changes to the discharge data collection.
        /// </summary>
        private void DischargeData_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            _alignedObservationsCache = null;
            RaisePropertyChange(nameof(DischargeData));
            if (UseDefaultFlatPriors)
                SetDefaultParameters();
        }

        /// <inheritdoc/>
        public override void SetDefaultParameters()
        {
            // Remove old handlers
            if (Parameters.Count > 0)
            {
                for (int i = 0; i < NumberOfParameters; i++)
                    Parameters[i].PropertyChanged -= Parameter_PropertyChanged;
            }

            var stageScale = GetDefaultPriorStageScale();
            double minH = stageScale.MinimumStage;
            double rangeH = stageScale.StageSpan;
            double sigmaUB = GetDefaultPriorSigmaUpperBound();

            // Zero-flow stage bounds for segment 1 (h₁ is the main-channel
            // cease-to-flow stage and the only location parameter fit directly).
            // In the BaRatin addition-mode formulation implemented here, control k
            // for k ≥ 2 has its "offset" equal to its activation stage h_k and is
            // thus not a separate location parameter — see Le Coz et al. 2014 and
            // the class-level XML header for details.
            double h1Min = minH - rangeH;
            double h1Max = minH + 0.1 * rangeH;

            _parameters = new List<ModelParameter>();

            // Segment 1 location (h₁)
            Parameters.Add(new ModelParameter()
            {
                Name = "Zero-Flow Stage (h₁)",
                Value = minH - 0.1 * rangeH,
                LowerBound = h1Min,
                UpperBound = h1Max,
                PriorDistribution = new Uniform(h1Min, h1Max)
            });

            // Segment 1 coefficient log10(α₁)
            Parameters.Add(new ModelParameter()
            {
                Name = "Coefficient (α₁)",
                Value = 0,
                LowerBound = -10,
                UpperBound = 10,
                PriorDistribution = new Uniform(-10, 10)
            });
            Parameters.Add(new ModelParameter()
            {
                Name = "Exponent (β₁)",
                Value = 2.0,
                LowerBound = 0,
                UpperBound = 5,
                PriorDistribution = new Uniform(0, 5)
            });

            if (NumberOfSegments >= 2)
            {
                // Breakpoint h₂, control-2 coefficient log₁₀α₂, and control-2 exponent β₂.
                // h₂ is the activation stage of the second control (e.g., overbank
                // floodplain). Under BaRatin addition mode, control 2 contributes
                // α₂·(h − h₂)^β₂ for h > h₂, added on top of the continuing main-channel
                // contribution α₁·(h − h₁)^β₁.
                double h2Min = minH + 0.2 * rangeH;
                double h2Max = minH + 0.7 * rangeH;
                double h2Default = minH + 0.45 * rangeH;

                Parameters.Add(new ModelParameter()
                {
                    Name = "Activation Stage (h₂)",
                    Value = h2Default,
                    LowerBound = h2Min,
                    UpperBound = h2Max,
                    PriorDistribution = new Uniform(h2Min, h2Max)
                });
                Parameters.Add(new ModelParameter()
                {
                    Name = "Coefficient (α₂)",
                    Value = 0,
                    LowerBound = -10,
                    UpperBound = 10,
                    PriorDistribution = new Uniform(-10, 10)
                });
                Parameters.Add(new ModelParameter()
                {
                    Name = "Exponent (β₂)",
                    Value = 2.0,
                    LowerBound = 0,
                    UpperBound = 5,
                    PriorDistribution = new Uniform(0, 5)
                });
            }

            if (NumberOfSegments >= 3)
            {
                // Breakpoint h₃, control-3 coefficient log₁₀α₃, and control-3 exponent β₃.
                // h₃ is the activation stage of the third control. Under BaRatin
                // addition mode, control 3 contributes α₃·(h − h₃)^β₃ for h > h₃,
                // added on top of the two lower controls.
                double h3Min = minH + 0.5 * rangeH;
                double h3Max = minH + rangeH;
                double h3Default = minH + 0.75 * rangeH;

                Parameters.Add(new ModelParameter()
                {
                    Name = "Activation Stage (h₃)",
                    Value = h3Default,
                    LowerBound = h3Min,
                    UpperBound = h3Max,
                    PriorDistribution = new Uniform(h3Min, h3Max)
                });
                Parameters.Add(new ModelParameter()
                {
                    Name = "Coefficient (α₃)",
                    Value = 0,
                    LowerBound = -10,
                    UpperBound = 10,
                    PriorDistribution = new Uniform(-10, 10)
                });
                Parameters.Add(new ModelParameter()
                {
                    Name = "Exponent (β₃)",
                    Value = 2.0,
                    LowerBound = 0,
                    UpperBound = 5,
                    PriorDistribution = new Uniform(0, 5)
                });
            }

            // Scale parameter (log-space standard deviation)
            Parameters.Add(new ModelParameter()
            {
                Name = "Scale (σ)",
                Value = sigmaUB / 3,
                LowerBound = Tools.DoubleMachineEpsilon,
                UpperBound = sigmaUB,
                IsPositive = true,
                PriorDistribution = new Uniform(Tools.DoubleMachineEpsilon, sigmaUB)
            });

            // Add handlers
            for (int i = 0; i < NumberOfParameters; i++)
                Parameters[i].PropertyChanged += Parameter_PropertyChanged;

            RaisePropertyChange(nameof(SetDefaultParameters));
        }

        /// <summary>
        /// Computes the stage location and span used to create weakly informative default priors.
        /// </summary>
        /// <returns>The minimum calibration stage and a finite positive stage span.</returns>
        /// <remarks>
        /// Date-aligned observations are preferred because the likelihood only uses paired
        /// stage-discharge observations. If no valid aligned pairs are available, the method
        /// falls back to finite stage observations so default construction remains robust while
        /// the user is still configuring the analysis.
        /// </remarks>
        private (double MinimumStage, double StageSpan) GetDefaultPriorStageScale()
        {
            var stages = GetDefaultPriorStageValues();
            if (stages.Count == 0)
                return (0.0, 10.0);

            double minStage = stages.Min();
            double maxStage = stages.Max();
            double span = maxStage - minStage;

            if (!Tools.IsFinite(minStage))
                minStage = 0.0;
            if (!Tools.IsFinite(span) || span < 1.0)
                span = 1.0;

            return (minStage, span);
        }

        /// <summary>
        /// Gets finite stage values for calibrating the default flat stage priors.
        /// </summary>
        /// <returns>A list of finite stage values from aligned pairs, or from all stage data as a fallback.</returns>
        /// <remarks>
        /// Aligned pairs with non-positive or non-finite discharge values are excluded because
        /// the log-space rating-curve likelihood cannot use them.
        /// </remarks>
        private List<double> GetDefaultPriorStageValues()
        {
            if (StageData != null && DischargeData != null)
            {
                var alignedStages = GetAlignedObservations()
                    .Where(x => Tools.IsFinite(x.Stage) && Tools.IsFinite(x.Discharge) && x.Discharge > 0)
                    .Select(x => x.Stage)
                    .ToList();

                if (alignedStages.Count > 0)
                    return alignedStages;
            }

            if (StageData == null)
                return new List<double>();

            return StageData
                .Where(x => Tools.IsFinite(x.Value))
                .Select(x => x.Value)
                .ToList();
        }

        /// <summary>
        /// Computes the upper bound for the log-space residual scale prior.
        /// </summary>
        /// <returns>A finite positive upper bound for the sigma prior.</returns>
        /// <remarks>
        /// The bound uses three standard deviations of log10 discharge, rounded up to keep
        /// the prior broad. Date-aligned discharges are preferred; unpaired discharge outliers
        /// should not widen priors for a likelihood that will never observe them.
        /// </remarks>
        private double GetDefaultPriorSigmaUpperBound()
        {
            const double defaultSigmaUpperBound = 2.0;

            var logQ = GetDefaultPriorLogDischarges();
            if (logQ.Count > 1)
            {
                double stdDevLogQ = Statistics.StandardDeviation(logQ.ToArray());
                double sigmaUpperBound = Math.Ceiling(stdDevLogQ * 3.0);
                if (Tools.IsFinite(sigmaUpperBound) && sigmaUpperBound > Tools.DoubleMachineEpsilon)
                    return sigmaUpperBound;
            }

            return defaultSigmaUpperBound;
        }

        /// <summary>
        /// Gets positive finite log10 discharge values for calibrating the default sigma prior.
        /// </summary>
        /// <returns>A list of log10 discharge values from aligned pairs, or from all discharge data as a fallback.</returns>
        /// <remarks>
        /// Non-positive discharges are excluded because the rating-curve error model is defined
        /// in log10 discharge space.
        /// </remarks>
        private List<double> GetDefaultPriorLogDischarges()
        {
            if (StageData != null && DischargeData != null)
            {
                var alignedLogQ = GetAlignedObservations()
                    .Where(x => Tools.IsFinite(x.Stage) && Tools.IsFinite(x.Discharge) && x.Discharge > 0)
                    .Select(x => Math.Log10(x.Discharge))
                    .Where(Tools.IsFinite)
                    .ToList();

                if (alignedLogQ.Count > 0)
                    return alignedLogQ;
            }

            if (DischargeData == null)
                return new List<double>();

            return DischargeData
                .Where(x => Tools.IsFinite(x.Value) && x.Value > 0)
                .Select(x => Math.Log10(x.Value))
                .Where(Tools.IsFinite)
                .ToList();
        }

        /// <inheritdoc/>
        public override void SetParameterValues(IList<double> parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));
            if (parameters.Count != NumberOfParameters)
                throw new ArgumentException("Parameter count does not match model parameter count.", nameof(parameters));

            for (int i = 0; i < parameters.Count; i++)
                Parameters[i].Value = parameters[i];
        }

        /// <summary>
        /// Inner-joins <see cref="StageData"/> and <see cref="DischargeData"/> on their
        /// <see cref="Numerics.Data.SeriesOrdinate{TIndex, TValue}.Index"/> (DateTime) and
        /// returns the matched (stage, discharge) pairs in chronological order.
        /// Observations present in only one series are silently dropped.
        /// </summary>
        /// <returns>
        /// List of <c>(Date, Stage, Discharge)</c> tuples sorted by <c>Date</c>.
        /// Returns an empty list when either series is <see langword="null"/>.
        /// </returns>
        /// <remarks>
        /// <para>
        /// All likelihood, diagnostic, and goodness-of-fit computations iterate the
        /// result of this method rather than pairing stage and discharge by position.
        /// This allows the rating curve to be fit from real-world USGS-style records
        /// where the two time series share many — but not all — dates.
        /// </para>
        /// <para>
        /// Results are <b>cached</b> between calls and invalidated automatically when
        /// either <see cref="StageData"/> or <see cref="DischargeData"/> is reassigned
        /// or mutated via <c>CollectionChanged</c>. This is critical for MCMC
        /// throughput: the likelihood is evaluated thousands of times per fit and the
        /// join itself is O(m + n), which dominated wall-clock time before caching.
        /// Callers must treat the returned list as read-only.
        /// </para>
        /// </remarks>
        public List<(DateTime Date, double Stage, double Discharge)> GetAlignedObservations()
        {
            if (_alignedObservationsCache != null)
                return _alignedObservationsCache;

            var result = new List<(DateTime, double, double)>();
            if (StageData == null || DischargeData == null)
            {
                _alignedObservationsCache = result;
                return result;
            }

            var dischargeByDate = new Dictionary<DateTime, double>(DischargeData.Count);
            for (int j = 0; j < DischargeData.Count; j++)
                dischargeByDate[DischargeData[j].Index] = DischargeData[j].Value;

            for (int i = 0; i < StageData.Count; i++)
            {
                if (dischargeByDate.TryGetValue(StageData[i].Index, out double q))
                    result.Add((StageData[i].Index, StageData[i].Value, q));
            }
            result.Sort((a, b) => a.Item1.CompareTo(b.Item1));

            _alignedObservationsCache = result;
            return result;
        }

        /// <summary>
        /// Counts the source stage and discharge observations and the aligned pairs
        /// that will contribute to the rating-curve likelihood.
        /// </summary>
        /// <returns>
        /// Tuple containing the stage count, discharge count, and date-aligned pair
        /// count used by the fit.
        /// </returns>
        /// <remarks>
        /// The paired count is the length of <see cref="GetAlignedObservations"/>,
        /// so the reported count matches the observations used by likelihood,
        /// residual, and fitted-value calculations.
        /// </remarks>
        public (int StageCount, int DischargeCount, int PairedCount) GetDataAlignmentCounts()
        {
            int stageCount = StageData?.Count ?? 0;
            int dischargeCount = DischargeData?.Count ?? 0;
            int pairedCount = GetAlignedObservations().Count;

            return (stageCount, dischargeCount, pairedCount);
        }

        /// <summary>
        /// Minimum number of date-aligned (stage, discharge) pairs required for the
        /// rating curve to be fittable. Observations with dates appearing in only one
        /// of the two series are not counted.
        /// </summary>
        public const int MinimumAlignedObservations = 10;

        /// <inheritdoc/>
        /// <remarks>
        /// Evaluated on the date-inner-join of <see cref="StageData"/> and
        /// <see cref="DischargeData"/>; observations not present in both series are
        /// excluded. Returns <see cref="double.NegativeInfinity"/> when fewer than
        /// <see cref="MinimumAlignedObservations"/> pairs are available or when any
        /// segment-ordering / positivity guard is violated.
        /// </remarks>
        public override double DataLogLikelihood(double[] parameters)
        {
            if (StageData == null || DischargeData == null)
                return double.NegativeInfinity;

            var aligned = GetAlignedObservations();
            if (aligned.Count < MinimumAlignedObservations)
                return double.NegativeInfinity;

            // Check for NaN
            for (int i = 0; i < parameters.Length; i++)
            {
                if (double.IsNaN(parameters[i]))
                    return double.NegativeInfinity;
            }

            // Enforce segment ordering (breakpoints must be monotonically increasing)
            if (!ValidateSegmentOrdering(parameters))
                return double.NegativeInfinity;

            double sigma = parameters[parameters.Length - 1];
            var normDist = new Normal(0, sigma);
            double logLH = 0;

            // Compute log-likelihood in log-space
            for (int i = 0; i < aligned.Count; i++)
            {
                double predQ = Predict(parameters, aligned[i].Stage);

                // Check for invalid predictions (zero or negative discharge)
                if (predQ <= 0)
                    return double.NegativeInfinity;

                double obsLogQ = Math.Log10(aligned[i].Discharge);
                double predLogQ = Math.Log10(predQ);

                // Log-space residual
                double residual = obsLogQ - predLogQ;
                logLH += normDist.LogPDF(residual);
            }

            return logLH;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// The returned array has one entry per date-aligned (stage, discharge) pair
        /// — observations not present in both series are excluded. Length equals the
        /// result of <c>GetAlignedObservations().Count</c>.
        /// </remarks>
        public override double[] PointwiseDataLogLikelihood(double[] parameters)
        {
            if (StageData == null || DischargeData == null)
                return Array.Empty<double>();

            var aligned = GetAlignedObservations();
            int n = aligned.Count;
            if (n == 0)
                return Array.Empty<double>();

            // Check for NaN in parameters
            for (int i = 0; i < parameters.Length; i++)
            {
                if (double.IsNaN(parameters[i]))
                {
                    var invalid = new double[n];
                    for (int j = 0; j < n; j++) invalid[j] = double.NegativeInfinity;
                    return invalid;
                }
            }

            // Enforce segment ordering
            if (!ValidateSegmentOrdering(parameters))
            {
                var invalid = new double[n];
                for (int i = 0; i < n; i++) invalid[i] = double.NegativeInfinity;
                return invalid;
            }

            double sigma = parameters[parameters.Length - 1];
            var normDist = new Normal(0, sigma);
            var result = new double[n];

            // Compute pointwise log-likelihood in log-space
            for (int i = 0; i < n; i++)
            {
                double predQ = Predict(parameters, aligned[i].Stage);

                // Check for invalid predictions
                if (predQ <= 0)
                {
                    result[i] = double.NegativeInfinity;
                    continue;
                }

                double obsLogQ = Math.Log10(aligned[i].Discharge);
                double predLogQ = Math.Log10(predQ);
                double residual = obsLogQ - predLogQ;
                result[i] = normDist.LogPDF(residual);
            }

            return result;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// The returned list has one entry per date-aligned (stage, discharge) pair
        /// — observations not present in both series are excluded.
        /// </remarks>
        public override List<DataComponent> PointwiseDataLogLikelihoodComponents(double[] parameters)
        {
            if (StageData == null || DischargeData == null)
                return new List<DataComponent>();

            var aligned = GetAlignedObservations();
            int n = aligned.Count;
            var result = new List<DataComponent>(n);
            if (n == 0)
                return result;

            // Check for NaN in parameters
            for (int i = 0; i < parameters.Length; i++)
            {
                if (double.IsNaN(parameters[i]))
                {
                    for (int j = 0; j < n; j++)
                        result.Add(new DataComponent(j, double.NegativeInfinity, aligned[j].Stage, DataComponentType.Exact, 1, j.ToString()));
                    return result;
                }
            }

            // Enforce segment ordering
            if (!ValidateSegmentOrdering(parameters))
            {
                for (int i = 0; i < n; i++)
                    result.Add(new DataComponent(i, double.NegativeInfinity, aligned[i].Stage, DataComponentType.Exact, 1, i.ToString()));
                return result;
            }

            double sigma = parameters[parameters.Length - 1];
            var normDist = new Normal(0, sigma);

            // Compute pointwise log-likelihood in log-space
            for (int i = 0; i < n; i++)
            {
                double stage = aligned[i].Stage;
                double predQ = Predict(parameters, stage);

                double logLH;
                if (predQ <= 0)
                {
                    logLH = double.NegativeInfinity;
                }
                else
                {
                    double obsLogQ = Math.Log10(aligned[i].Discharge);
                    double predLogQ = Math.Log10(predQ);
                    double residual = obsLogQ - predLogQ;
                    logLH = normDist.LogPDF(residual);
                }

                result.Add(new DataComponent(i, logLH, stage, DataComponentType.Exact, 1, $"Stage={stage:F2}"));
            }

            return result;
        }

        /// <inheritdoc/>
        public override double PriorLogLikelihood(double[] parameters)
        {
            if (parameters == null || Parameters is null || parameters.Length < Parameters.Count)
                return double.NegativeInfinity;

            double sigma = parameters[parameters.Length - 1];
            double logLH = 0;

            for (int i = 0; i < Parameters.Count; i++)
            {
                logLH += Parameters[i].PriorDistribution.LogPDF(parameters[i]);
            }

            if (UseJeffreysRuleForScale)
            {
                // Jeffreys prior 1/σ requires positive scale parameter; consistent with
                // AutoRegressive / MovingAverage Jeffreys-rule guard.
                logLH -= sigma > 0 ? Math.Log(sigma) : double.PositiveInfinity;
            }

            // Collapse NaN / +Inf — Bayesian samplers require -Inf as the "impossible" sentinel.
            // Without this, +Inf − Inf = NaN can corrupt the MCMC arithmetic. Mirrors ModelBase default.
            if (!Tools.IsFinite(logLH)) return double.NegativeInfinity;
            return logLH;
        }

        /// <inheritdoc/>
        public override List<PriorComponent> PointwisePriorLogLikelihood(double[] parameters)
        {
            var result = new List<PriorComponent>();

            // Parameter priors
            for (int i = 0; i < Parameters.Count; i++)
            {
                double ll = Parameters[i].PriorDistribution.LogPDF(parameters[i]);
                string paramName = string.IsNullOrEmpty(Parameters[i].OwnerName) ? Parameters[i].Name : Parameters[i].OwnerName;
                result.Add(new PriorComponent($"Parameter Prior: {paramName}", ll, PriorComponentType.ParameterPrior));
            }

            // Jeffreys rule for sigma (scale)
            if (UseJeffreysRuleForScale)
            {
                double sigma = parameters[parameters.Length - 1];
                // Jeffreys prior 1/σ requires positive scale; mirror the scalar guard
                // so the Pointwise sum stays consistent with the scalar PriorLogLikelihood.
                double ll = sigma > 0 ? -Math.Log(sigma) : double.NegativeInfinity;
                result.Add(new PriorComponent("Jeffreys Scale: σ", ll, PriorComponentType.JeffreysScalePrior));
            }

            return result;
        }

        /// <summary>
        /// Validates that the fit parameters satisfy the segment-ordering constraints
        /// required by the BaRatin addition-mode rating curve.
        /// </summary>
        /// <param name="parameters">The parameter array.</param>
        /// <returns>True if ordering is valid, false otherwise.</returns>
        /// <remarks>
        /// Required: h₁ &lt; h₂ &lt; h₃. h₁ is the main-channel cease-to-flow stage;
        /// h₂ and h₃ are the activation stages for controls 2 and 3 (and also the
        /// "b" offsets of those control's power-law terms under addition mode).
        /// </remarks>
        private bool ValidateSegmentOrdering(double[] parameters)
        {
            if (NumberOfSegments == 1)
                return true;

            // Defensive length check: callers can invoke Validate() during transient
            // states (e.g., while a project is still loading), so refuse to index past
            // the supplied array rather than throwing IndexOutOfRangeException.
            int expectedLength = 3 * NumberOfSegments + 1;
            if (parameters == null || parameters.Length < expectedLength)
                return false;

            // Parameter layout: [h₁, log₁₀α₁, β₁, h₂, log₁₀α₂, β₂, (h₃, log₁₀α₃, β₃), σ]
            // Under addition mode, the hard ordering the fit parameters must satisfy
            // is h₁ < h₂ < h₃ (each new control must activate above the main-channel
            // zero-flow stage, and breakpoints must be strictly ordered).
            double h1 = parameters[0];
            double h2 = parameters[3];

            if (NumberOfSegments == 2)
                return h1 < h2;

            if (NumberOfSegments == 3)
            {
                double h3 = parameters[6];
                return h1 < h2 && h2 < h3;
            }

            return true;
        }

        /// <summary>
        /// Predicts discharge for a given stage using the BaRatin matrix-of-controls
        /// rating curve in ADDITION mode (Le Coz et al. 2014).
        /// </summary>
        /// <param name="parameters">The model parameters.</param>
        /// <param name="stage">The stage (water level) value.</param>
        /// <returns>The predicted discharge in real-space.</returns>
        /// <remarks>
        /// Mirrors the BaRatin Fortran reference implementation
        /// (<c>ApplyRC_General</c>, <c>src/RatingCurve_tools.f90</c> lines 464–478)
        /// with the lower-triangular-all-ones control matrix M. The segment index
        /// identifies how many controls are active at the supplied stage; Q is the
        /// sum of the active controls' power laws:
        /// Q(h) = α₁(h − h₁)^β₁ + α₂(h − h₂)^β₂·𝟙{h &gt; h₂} + α₃(h − h₃)^β₃·𝟙{h &gt; h₃}.
        /// Under addition mode, the BaRatin continuity derivation collapses to
        /// b_k = κ_k (activation stage), so each subsequent control's "offset" equals
        /// its activation breakpoint h_k.
        /// </remarks>
        public double Predict(double[] parameters, double stage)
        {
            // Parameter layout:
            //   1 seg: [h₁, log₁₀α₁, β₁, σ]
            //   2 seg: [h₁, log₁₀α₁, β₁, h₂, log₁₀α₂, β₂, σ]
            //   3 seg: [h₁, log₁₀α₁, β₁, h₂, log₁₀α₂, β₂, h₃, log₁₀α₃, β₃, σ]
            double h1 = parameters[0];
            double depth1 = stage - h1;
            if (depth1 <= 0)
                return 0.0;

            double Q = Math.Pow(10, parameters[1]) * Math.Pow(depth1, parameters[2]);

            if (NumberOfSegments >= 2)
            {
                double h2 = parameters[3];
                double depth2 = stage - h2;
                if (depth2 > 0)
                    Q += Math.Pow(10, parameters[4]) * Math.Pow(depth2, parameters[5]);
            }

            if (NumberOfSegments >= 3)
            {
                double h3 = parameters[6];
                double depth3 = stage - h3;
                if (depth3 > 0)
                    Q += Math.Pow(10, parameters[7]) * Math.Pow(depth3, parameters[8]);
            }

            return Q;
        }

        /// <summary>
        /// Returns log₁₀(αₖ) for the requested segment. All αₖ are fit directly in the
        /// BaRatin parameterization, so this helper just indexes into <paramref name="parameters"/>.
        /// </summary>
        /// <param name="segmentOneBased">Segment index (1, 2, or 3).</param>
        /// <param name="parameters">The full parameter vector.</param>
        /// <returns>log₁₀(αₖ).</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="segmentOneBased"/>
        /// is less than 1 or greater than <see cref="NumberOfSegments"/>.</exception>
        public double GetLog10Alpha(int segmentOneBased, double[] parameters)
        {
            if (segmentOneBased < 1 || segmentOneBased > NumberOfSegments)
                throw new ArgumentOutOfRangeException(nameof(segmentOneBased),
                    $"Segment index must be between 1 and {NumberOfSegments}.");

            // The throw above already rejects segments outside [1, NumberOfSegments],
            // and NumberOfSegments is capped at 3 by Validate(), so the remaining cases
            // are exhaustive. The compiler still requires a default arm.
            return segmentOneBased switch
            {
                1 => parameters[1],
                2 => parameters[4],
                3 => parameters[7],
                _ => throw new ArgumentOutOfRangeException(nameof(segmentOneBased))
            };
        }

        /// <summary>
        /// Returns the location parameter ξₖ for the requested control — the "b" offset
        /// of that control's power-law term in the BaRatin addition-mode rating curve.
        /// </summary>
        /// <param name="segmentOneBased">Control index (1, 2, or 3).</param>
        /// <param name="parameters">The full parameter vector.</param>
        /// <returns>
        /// h₁ for control 1 (the main-channel cease-to-flow stage, a fit parameter);
        /// the activation stage h_k for controls k ≥ 2 (the BaRatin continuity
        /// derivation collapses to b_k = κ_k in pure addition mode, so the "offset" of
        /// each subsequent control equals its activation breakpoint).
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="segmentOneBased"/>
        /// is less than 1 or greater than <see cref="NumberOfSegments"/>.</exception>
        public double GetLocation(int segmentOneBased, double[] parameters)
        {
            if (segmentOneBased < 1 || segmentOneBased > NumberOfSegments)
                throw new ArgumentOutOfRangeException(nameof(segmentOneBased),
                    $"Segment index must be between 1 and {NumberOfSegments}.");

            return segmentOneBased switch
            {
                1 => parameters[0], // h₁ (fit)
                2 => parameters[3], // h₂ (activation stage of control 2)
                3 => parameters[6], // h₃ (activation stage of control 3)
                _ => double.NaN
            };
        }

        /// <summary>
        /// Predicts discharge with stochastic error for a given stage.
        /// </summary>
        /// <param name="parameters">The model parameters.</param>
        /// <param name="stage">The stage value.</param>
        /// <param name="seed">Random seed for error generation.</param>
        /// <returns>The predicted discharge with random error in real-space.</returns>
        public double Predict(double[] parameters, double stage, int seed)
        {
            var prng = new Random(seed);
            double sigma = parameters[parameters.Length - 1];
            var errDist = new Normal(0, sigma);

            // Compute mean prediction in log-space
            double logQ = Math.Log10(Predict(parameters, stage));

            // Add log-space error
            logQ += errDist.InverseCDF(prng.NextDouble());

            // Transform back to real-space
            return Math.Pow(10, logQ);
        }

        /// <summary>
        /// Determines how many controls are active at a given stage, returning the
        /// index of the highest-activated control (BaRatin "range" from
        /// <c>RC_General_GetRange</c>).
        /// </summary>
        /// <param name="parameters">The model parameters.</param>
        /// <param name="stage">The stage value.</param>
        /// <returns>
        /// Zero-based index of the highest active control: 0 if only control 1 is
        /// active (stage &lt; h₂), 1 if controls 1–2 are active, 2 if all three are.
        /// </returns>
        private int GetSegmentIndex(double[] parameters, double stage)
        {
            if (NumberOfSegments == 1)
                return 0;

            // Parameter layout: [h₁, log₁₀α₁, β₁, h₂, log₁₀α₂, β₂, (h₃, log₁₀α₃, β₃), σ]
            double h2 = parameters[3];
            if (NumberOfSegments == 2)
            {
                return stage < h2 ? 0 : 1;
            }

            // NumberOfSegments == 3
            double h3 = parameters[6];
            if (stage < h2)
                return 0;
            else if (stage < h3)
                return 1;
            else
                return 2;
        }

        /// <summary>
        /// Computes log-space residuals for the date-aligned observations.
        /// </summary>
        /// <param name="parameters">The model parameters.</param>
        /// <returns>
        /// Array of log-space residuals with length equal to the number of dates
        /// common to <see cref="StageData"/> and <see cref="DischargeData"/> — i.e.
        /// <c>GetAlignedObservations().Count</c>. Observations not present in both
        /// series are excluded so residuals correspond 1:1 with the points used in
        /// the likelihood.
        /// </returns>
        public double[] Residuals(double[] parameters)
        {
            var aligned = GetAlignedObservations();
            var residuals = new double[aligned.Count];
            for (int i = 0; i < aligned.Count; i++)
            {
                double predQ = Predict(parameters, aligned[i].Stage);
                if (predQ <= 0)
                {
                    residuals[i] = double.NaN;
                }
                else
                {
                    double obsLogQ = Math.Log10(aligned[i].Discharge);
                    double predLogQ = Math.Log10(predQ);
                    residuals[i] = obsLogQ - predLogQ;
                }
            }
            return residuals;
        }

        /// <summary>
        /// Computes log-space fitted values for the date-aligned observations.
        /// </summary>
        /// <param name="parameters">The model parameters.</param>
        /// <returns>
        /// Array of log-space fitted values with length equal to the number of dates
        /// common to <see cref="StageData"/> and <see cref="DischargeData"/>.
        /// Aligned 1:1 with <see cref="Residuals"/>.
        /// </returns>
        public double[] FittedValues(double[] parameters)
        {
            var aligned = GetAlignedObservations();
            var fittedValues = new double[aligned.Count];
            for (int i = 0; i < aligned.Count; i++)
            {
                double predQ = Predict(parameters, aligned[i].Stage);
                fittedValues[i] = predQ <= 0 ? double.NaN : Math.Log10(predQ);
            }
            return fittedValues;
        }

        /// <summary>
        /// Generates a rating curve table for a range of stages.
        /// </summary>
        /// <param name="parameters">The model parameters.</param>
        /// <param name="minStage">Minimum stage value.</param>
        /// <param name="maxStage">Maximum stage value.</param>
        /// <param name="numPoints">Number of points in the table.</param>
        /// <returns>2D array where [i,0] is stage and [i,1] is discharge.</returns>
        public double[,] GenerateRatingTable(double[] parameters, double minStage, double maxStage, int numPoints = 100)
        {
            var table = new double[numPoints, 2];
            double stepSize = (maxStage - minStage) / (numPoints - 1);

            for (int i = 0; i < numPoints; i++)
            {
                double stage = minStage + i * stepSize;
                table[i, 0] = stage;
                table[i, 1] = Predict(parameters, stage);
            }

            return table;
        }

        /// <inheritdoc/>
        public override IModel Clone()
        {
            var parms = new List<ModelParameter>();
            for (int i = 0; i < NumberOfParameters; i++)
                parms.Add(Parameters[i].Clone());

            var result = new RatingCurve()
            {
                _numberOfSegments = NumberOfSegments,
                _useDefaultFlatPriors = UseDefaultFlatPriors,
                _useJeffreysRuleForScale = UseJeffreysRuleForScale,
                Parameters = parms,
                // Set backing fields directly to avoid triggering SetDefaultParameters()
                // which would overwrite the cloned parameter values
                _stageData = StageData?.Clone()!,
                _dischargeData = DischargeData?.Clone()!
            };

            return result;
        }

        /// <inheritdoc/>
        public override XElement ToXElement()
        {
            var result = new XElement(nameof(RatingCurve));
            result.SetAttributeValue(nameof(NumberOfSegments), NumberOfSegments.ToString(CultureInfo.InvariantCulture));
            result.SetAttributeValue(nameof(UseDefaultFlatPriors), UseDefaultFlatPriors.ToString());
            result.SetAttributeValue(nameof(UseJeffreysRuleForScale), UseJeffreysRuleForScale.ToString());

            var parms = new XElement(nameof(Parameters));
            foreach (var p in Parameters)
                parms.Add(p.ToXElement());
            result.Add(parms);

            return result;
        }

        /// <inheritdoc/>
        public override (bool IsValid, List<string> ValidationMessages) Validate()
        {
            bool isValid = true;
            var messages = new List<string>();

            // Check data
            if (StageData == null)
            {
                isValid = false;
                messages.Add("Error: Stage time series is missing. Please select a valid time series.");
            }

            if (DischargeData == null)
            {
                isValid = false;
                messages.Add("Error: Discharge time series is missing. Please select a valid time series.");
            }

            if (StageData != null && DischargeData != null)
            {
                int alignedCount = GetAlignedObservations().Count;
                if (alignedCount < MinimumAlignedObservations)
                {
                    isValid = false;
                    messages.Add($"Error: Stage and discharge time series must share at least {MinimumAlignedObservations} common dates to fit a rating curve (found {alignedCount}).");
                }

                // Check for non-positive discharge values
                if (DischargeData.Any(d => d.Value <= 0))
                {
                    isValid = false;
                    messages.Add("Error: All discharge values must be positive (log-space model requires Q > 0).");
                }
            }

            // Check number of segments
            if (NumberOfSegments < 1 || NumberOfSegments > 3)
            {
                isValid = false;
                messages.Add("Error: Number of segments must be between 1 and 3.");
            }

            // Check parameters
            if (Parameters != null)
            {
                int expectedParams = NumberOfSegments * 3 + 1; // 3 per segment + sigma
                if (Parameters.Count != expectedParams)
                {
                    isValid = false;
                    messages.Add($"Error: Expected {expectedParams} parameters but found {Parameters.Count}.");
                }

                for (int i = 0; i < Parameters.Count; i++)
                {
                    var valid = Parameters[i].Validate();
                    if (!valid.IsValid)
                    {
                        isValid = false;
                        messages.AddRange(valid.ValidationMessages);
                    }
                }

                // Check segment ordering if parameters are set
                if (Parameters.Count > 0)
                {
                    var paramValues = Parameters.Select(p => p.Value).ToArray();
                    if (!ValidateSegmentOrdering(paramValues))
                    {
                        isValid = false;
                        messages.Add("Error: Segment ordering invalid. Require h₁ < h₂ < h₃.");
                    }
                }
            }

            return (isValid, messages);
        }

        /// <inheritdoc/>
        /// <remarks>
        /// <para>
        /// Generates random discharge values based on the rating curve model.
        /// For each observation in the original StageData:
        /// </para>
        /// <list type="number">
        /// <item><description>The predicted log(Q) is computed using the rating curve.</description></item>
        /// <item><description>Random noise ε ~ N(0, σ²) is added to log(Q).</description></item>
        /// <item><description>The result is transformed back to Q space.</description></item>
        /// </list>
        /// <para>
        /// If sampleSize differs from the number of stages, stages are randomly sampled
        /// with replacement.
        /// </para>
        /// </remarks>
        public double[] GenerateRandomValues(int sampleSize, int seed = -1)
        {
            if (sampleSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(sampleSize), "Sample size must be positive.");
            if (StageData == null || StageData.Count == 0)
                throw new InvalidOperationException("StageData cannot be null or empty when generating random values.");
            if (Parameters == null || Parameters.Count == 0)
                throw new InvalidOperationException("Parameters must be set before generating random values.");

            var rng = seed > 0 ? new Numerics.Sampling.MersenneTwister(seed) : new Numerics.Sampling.MersenneTwister();
            var normal = new Numerics.Distributions.Normal(0, 1);

            // Get sigma (last parameter)
            double sigma = Parameters[Parameters.Count - 1].Value;

            // Get parameter values
            var paramValues = Parameters.Select(p => p.Value).ToArray();

            var result = new double[sampleSize];

            for (int i = 0; i < sampleSize; i++)
            {
                // Select a stage (with replacement if sampleSize > StageData.Count)
                int stageIndex = rng.Next(0, StageData.Count);
                double stage = StageData[stageIndex].Value;

                // Predict discharge using rating curve
                double predQ = Predict(paramValues, stage);

                // Add noise in log space
                double logQ = Math.Log10(predQ) + sigma * normal.InverseCDF(rng.NextDouble());

                // Transform back
                result[i] = Math.Pow(10, logQ);
            }

            return result;
        }

        /// <summary>
        /// Generates synthetic stage-discharge data from the rating curve model.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method generates complete (stage, discharge) pairs for testing and simulation.
        /// Stage values are uniformly distributed in [minStage, maxStage], and discharge values
        /// are computed using the rating curve equation with log-normal error.
        /// </para>
        /// <para>
        /// The generated data is sorted by stage and returned as TimeSeries with daily intervals
        /// starting from January 1, 2000.
        /// </para>
        /// </remarks>
        /// <param name="sampleSize">Number of (stage, discharge) pairs to generate.</param>
        /// <param name="minStage">Minimum stage value for uniform sampling.</param>
        /// <param name="maxStage">Maximum stage value for uniform sampling.</param>
        /// <param name="seed">Random seed for reproducibility. Use -1 for random seed.</param>
        /// <returns>
        /// A tuple containing:
        /// <list type="bullet">
        /// <item><description>StageData: TimeSeries of generated stage values.</description></item>
        /// <item><description>DischargeData: TimeSeries of generated discharge values with noise.</description></item>
        /// </list>
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="sampleSize"/> is not positive, or when
        /// <paramref name="minStage"/> is greater than or equal to <paramref name="maxStage"/>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when parameters have not been set.
        /// </exception>
        public (TimeSeries StageData, TimeSeries DischargeData) GenerateSyntheticData(
            int sampleSize,
            double minStage,
            double maxStage,
            int seed = -1)
        {
            if (sampleSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(sampleSize), "Sample size must be positive.");
            if (minStage >= maxStage)
                throw new ArgumentOutOfRangeException(nameof(minStage), "Minimum stage must be less than maximum stage.");
            if (Parameters == null || Parameters.Count == 0)
                throw new InvalidOperationException("Parameters must be set before generating synthetic data.");

            var rng = seed > 0 ? new Numerics.Sampling.MersenneTwister(seed) : new Numerics.Sampling.MersenneTwister();

            // Get sigma (last parameter) for noise generation
            double sigma = Parameters[Parameters.Count - 1].Value;
            var normal = new Numerics.Distributions.Normal(0, sigma);

            // Get parameter values
            var paramValues = Parameters.Select(p => p.Value).ToArray();

            var stages = new double[sampleSize];
            var discharges = new double[sampleSize];

            for (int i = 0; i < sampleSize; i++)
            {
                // Generate stage uniformly in [minStage, maxStage]
                stages[i] = minStage + rng.NextDouble() * (maxStage - minStage);

                // Predict discharge using the rating curve
                double predQ = Predict(paramValues, stages[i]);

                // Add noise in log10-space
                double logQ = Math.Log10(predQ) + normal.InverseCDF(rng.NextDouble());

                // Transform back to real-space
                discharges[i] = Math.Pow(10, logQ);
            }

            // Sort by stage for cleaner data
            var pairs = stages.Zip(discharges, (s, d) => (Stage: s, Discharge: d))
                              .OrderBy(p => p.Stage)
                              .ToArray();

            var sortedStages = pairs.Select(p => p.Stage).ToArray();
            var sortedDischarges = pairs.Select(p => p.Discharge).ToArray();

            // Create TimeSeries with daily interval starting from 2000-01-01
            var startDate = new DateTime(2000, 1, 1);
            var stageTS = new TimeSeries(TimeInterval.OneDay, startDate, sortedStages);
            var dischargeTS = new TimeSeries(TimeInterval.OneDay, startDate, sortedDischarges);

            return (stageTS, dischargeTS);
        }

        #endregion
    }
}
