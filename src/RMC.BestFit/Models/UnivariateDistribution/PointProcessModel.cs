using Numerics;
using Numerics.Data;
using Numerics.Data.Statistics;
using Numerics.Distributions;
using Numerics.Mathematics.Integration;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Xml.Linq;

namespace RMC.BestFit.Models
{

    /// <summary>
    /// Point-process model for peaks-over-threshold (POT) data using competing GEV distributions.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    /// The <see cref="PointProcessModel"/> represents POT data with an
    /// underlying Poisson process for exceedance times and GEV (or seasonal
    /// GEV) models for magnitudes. It supports:
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// Non-seasonal point process (single GEV).
    /// </description></item>
    /// <item><description>
    /// Seasonal point process (two GEVs with block-day based seasons).
    /// </description></item>
    /// <item><description>
    /// Parameter priors and optional quantile priors in a Bayesian framework.
    /// </description></item>
    /// </list>
    /// </remarks>
    public class PointProcessModel : UnivariateDistributionModelBase, ISimulatable<double[]>, IUnivariateModel
    {

        #region Construction

        /// <summary>
        /// Constructs an empty point-process model with a default
        /// (non-seasonal) competing risks distribution.
        /// </summary>
        public PointProcessModel()
        {
            SetDistribution();
        }

        /// <summary>
        /// Constructs a point-process model using the specified data frame
        /// and competing risks distribution.
        /// </summary>
        /// <param name="dataFrame">The data frame to fit to.</param>
        /// <param name="distribution">The competing risks distribution.</param>
        public PointProcessModel(DataFrame dataFrame, CompetingRisks distribution)
        {
            Distribution = (CompetingRisks)distribution.Clone();
            DataFrame = dataFrame;
        }

        /// <summary>
        /// Constructs a point-process model from an XML element and associated
        /// data frame.
        /// </summary>
        /// <param name="dataFrame">The data frame to fit to.</param>
        /// <param name="xElement">The XML element to deserialize.</param>
        public PointProcessModel(DataFrame dataFrame, XElement xElement)
        {
            // Set data frame
            if (_dataFrame != null)
                _dataFrame.PropertyChanged -= DataFrame_PropertyChanged;
            _dataFrame = dataFrame;
            _dataFrame.PropertyChanged += DataFrame_PropertyChanged;
            _dataFrame.ProcessThresholdSeries();
            _dataFrame.CreateFullTimeSeries();

            // Inputs
            var thresholdAttr = xElement.Attribute(nameof(Threshold));
            if (thresholdAttr != null) double.TryParse(thresholdAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out _threshold);
            var totalYearsAttr = xElement.Attribute(nameof(TotalYears));
            if (totalYearsAttr != null) double.TryParse(totalYearsAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out _totalYears);
            var useDefaultsAttr = xElement.Attribute(nameof(UseDefaults));
            if (useDefaultsAttr != null) bool.TryParse(useDefaultsAttr.Value, out _useDefaults);
            var totalYearsInferredAttr = xElement.Attribute(nameof(IsTotalYearsInferred));
            if (totalYearsInferredAttr != null)
            {
                bool.TryParse(totalYearsInferredAttr.Value, out _isTotalYearsInferred);
            }
            else
            {
                _isTotalYearsInferred = _useDefaults && !HasStoredSourceExposure();
            }
            _totalYearsExplicit = !_useDefaults;
            CalculateLambda();
            var isSeasonalAttr = xElement.Attribute(nameof(IsSeasonal));
            if (isSeasonalAttr != null) bool.TryParse(isSeasonalAttr.Value, out _isSeasonal);
            var timeBlockAttr = xElement.Attribute(nameof(TimeBlock));
            if (timeBlockAttr != null) Enum.TryParse(timeBlockAttr.Value, out _timeBlock);
            var startMonthAttr = xElement.Attribute(nameof(StartMonth));
            if (startMonthAttr != null) int.TryParse(startMonthAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out _startMonth);
            SetAMSData();

            // Distribution
            var distElement = xElement.Element("Distribution");
            if (distElement != null)
                Distribution = CompetingRisks.FromXElement(distElement);

            // Parameters
            var flatPriorsAttr = xElement.Attribute(nameof(UseDefaultFlatPriors));
            if (flatPriorsAttr != null) bool.TryParse(flatPriorsAttr.Value, out _useDefaultFlatPriors);
            var jeffreysAttr = xElement.Attribute(nameof(UseJeffreysRuleForScale));
            if (jeffreysAttr != null) bool.TryParse(jeffreysAttr.Value, out _useJeffreysRuleForScale);
            var parms = new List<ModelParameter>();
            foreach (XElement p in xElement.Elements(nameof(Parameters)).Elements(nameof(ModelParameter)))
                parms.Add(new ModelParameter(p));
            Parameters = parms;

            // Quantiles
            var enableQuantilesAttr = xElement.Attribute(nameof(EnableQuantilePriors));
            if (enableQuantilesAttr != null) bool.TryParse(enableQuantilesAttr.Value, out _enableQuantilePriors);
            var singleQuantileAttr = xElement.Attribute(nameof(UseSingleQuantile));
            if (singleQuantileAttr != null) bool.TryParse(singleQuantileAttr.Value, out _useSingleQuantile);
            var quants = new List<QuantilePrior>();
            foreach (XElement q in xElement.Elements(nameof(QuantilePriors)).Elements(nameof(QuantilePrior)))
                quants.Add(new QuantilePrior(q));
            QuantilePriors = quants;

        }

        #endregion

        #region Members

        /// <summary>
        /// The set of univariate distribution types supported by the point process model.
        /// The point process likelihood requires Generalized Extreme Value (GEV) distributions.
        /// </summary>
        private static readonly HashSet<UnivariateDistributionType> _supportedDistributionTypes = new()
        {
            UnivariateDistributionType.GeneralizedExtremeValue,
        };

        /// <summary>
        /// Determines whether the specified distribution type is supported by the point process model.
        /// </summary>
        /// <param name="distributionType">The distribution type to check.</param>
        /// <returns><c>true</c> if the distribution type is supported; otherwise, <c>false</c>.</returns>
        /// <remarks>
        /// The point process model requires all component distributions to be Generalized Extreme Value (GEV).
        /// </remarks>
        public static bool IsSupportedDistributionType(UnivariateDistributionType distributionType)
        {
            return _supportedDistributionTypes.Contains(distributionType);
        }

        private CompetingRisks? _distribution = null;
        private DataFrame _amsDataFrame = new DataFrame();
        private List<int> _potDays = new List<int>();
        private bool _isSeasonal = false;
        private TimeBlockWindow _timeBlock = TimeBlockWindow.WaterYear;
        private int _startMonth = 10;
        private double _threshold = double.NaN;
        private double _totalYears = double.NaN;
        private bool _useDefaults = true;
        private double _lambda = double.NaN;
        private bool _isTotalYearsInferred = true;

        /// <summary>
        /// Tracks whether automatic exposure updates may replace <see cref="TotalYears"/>.
        /// Explicit values remain protected while defaults are disabled; enabling defaults
        /// restores source-exposure or event-span inference.
        /// </summary>
        private bool _totalYearsExplicit = false;

        /// <inheritdoc/>
        public override DataFrame DataFrame
        {
            get { return _dataFrame; }
            set
            {
                if (_dataFrame != null)
                    _dataFrame.PropertyChanged -= DataFrame_PropertyChanged;

                _dataFrame = value;
                if (_dataFrame != null)
                {
                    _dataFrame.PropertyChanged += DataFrame_PropertyChanged;
                    _dataFrame.ProcessThresholdSeries();
                }

                SetAMSData();
                if (UseDefaults && _dataFrame != null)
                    SetDefaultThresholdAndTotalYears(forceTotalYears: true);
                else
                    CalculateLambda();

                if (UseDefaultFlatPriors && _dataFrame != null)
                    SetDefaultParameters();

                RaisePropertyChange(nameof(DataFrame));
            }
        }

        /// <summary>
        /// Gets or sets the competing risks distribution representing the
        /// parent population of exceedances.
        /// </summary>
        [Category("Inputs")]
        [DisplayName("Competing Risks Distribution")]
        [Description("The competing risks distribution representing the parent population.")]
        [Browsable(true)]
        public CompetingRisks? Distribution
        {
            get { return _distribution; }
            set
            {
                _distribution = value;
                RaisePropertyChange(nameof(Distribution));
                SetDefaultParameters();
                SetDefaultQuantilePriors();
            }
        }

        /// <inheritdoc/>
        UnivariateDistributionBase? IUnivariateModel.Distribution => _distribution;

        /// <inheritdoc/>
        /// <remarks>
        /// Peaks-over-threshold models in RMC-BestFit are currently stationary;
        /// exceedance intensity and mark distribution parameters are constant.
        /// Always returns <c>false</c>.
        /// </remarks>
        public bool IsNonstationary => false;

        /// <summary>
        /// Gets the pre-processed annual maximum (or block) data frame derived from the input POT data.
        /// </summary>
        public DataFrame AMSDataFrame => _amsDataFrame;

        /// <summary>
        /// Gets the one-based block day for each peaks-over-threshold event.
        /// </summary>
        public List<int> POTDays => _potDays;

        /// <summary>
        /// Gets the empirical number of exact POT events per year.
        /// </summary>
        /// <remarks>
        /// This compatibility alias is identical to <see cref="EmpiricalEventRate"/>.
        /// It is not the fitted threshold intensity used by the point-process likelihood.
        /// </remarks>
        public double Lambda => _lambda;

        /// <summary>
        /// Gets the number of exact observations treated as Poisson POT events.
        /// </summary>
        /// <remarks>
        /// Uncertain, interval, and threshold-count records contribute to the hybrid magnitude
        /// likelihood but are not silently converted into occurrence-process events.
        /// </remarks>
        public int EmpiricalEventCount => DataFrame?.ExactSeries.Count ?? 0;

        /// <summary>
        /// Gets the empirical exact-event rate, equal to <see cref="EmpiricalEventCount"/> divided
        /// by <see cref="TotalYears"/>.
        /// </summary>
        public double EmpiricalEventRate => _lambda;

        /// <summary>
        /// Gets the fitted total threshold intensity per year at <see cref="Threshold"/>.
        /// </summary>
        /// <remarks>
        /// In seasonal mode this is the exposure-weighted sum of the two fitted seasonal
        /// intensities, which is also the annual rate of the seasonal simulators. Nonseasonal
        /// simulation uses the separately documented empirical <see cref="Lambda"/>.
        /// </remarks>
        public double FittedThresholdIntensity
        {
            get
            {
                return TryGetFittedIntensities(out double total, out _, out _, out _, out _)
                    ? total
                    : double.NaN;
            }
        }

        /// <summary>
        /// Gets the fitted, unweighted threshold intensity for seasonal component one.
        /// </summary>
        public double FittedSeasonOneThresholdIntensity
        {
            get
            {
                return IsSeasonal && TryGetFittedIntensities(out _, out double first, out _, out _, out _)
                    ? first
                    : double.NaN;
            }
        }

        /// <summary>
        /// Gets the fitted, unweighted threshold intensity for seasonal component two.
        /// </summary>
        public double FittedSeasonTwoThresholdIntensity
        {
            get
            {
                return IsSeasonal && TryGetFittedIntensities(out _, out _, out double second, out _, out _)
                    ? second
                    : double.NaN;
            }
        }

        /// <summary>
        /// Gets the exposure weight for wrapped seasonal component one.
        /// </summary>
        public double SeasonOneExposureWeight
        {
            get
            {
                return IsSeasonal && TryGetFittedIntensities(out _, out _, out _, out double firstWeight, out _)
                    ? firstWeight
                    : double.NaN;
            }
        }

        /// <summary>
        /// Gets the exposure weight for interior seasonal component two.
        /// </summary>
        public double SeasonTwoExposureWeight
        {
            get
            {
                return IsSeasonal && TryGetFittedIntensities(out _, out _, out _, out _, out double secondWeight)
                    ? secondWeight
                    : double.NaN;
            }
        }

        /// <summary>
        /// Gets a value indicating whether <see cref="TotalYears"/> was inferred from the span of
        /// retained exact events rather than supplied explicitly or preserved from source exposure.
        /// </summary>
        public bool IsTotalYearsInferred => _isTotalYearsInferred;

        /// <summary>
        /// Gets or sets the threshold above which events are modeled by the point process.
        /// </summary>
        [Category("Inputs")]
        [DisplayName("Threshold")]
        [Description("Defines the threshold above which events are modeled in the point process.")]
        [Browsable(true)]
        public double Threshold
        {
            get { return _threshold; }
            set
            {
                if (!_threshold.AlmostEquals(value))
                {
                    _threshold = value;
                    RaisePropertyChange(nameof(Threshold));
                }
            }
        }

        /// <summary>
        /// Gets or sets the total number of years over which POT events were observed.
        /// </summary>
        [Category("Inputs")]
        [DisplayName("Total Years")]
        [Description("Sets the total number of years over which peaks-over-threshold events were recorded.")]
        [Browsable(true)]
        public double TotalYears
        {
            get { return _totalYears; }
            set
            {
                bool valueChanged = !_totalYears.AlmostEquals(value);
                bool inferenceChanged = _isTotalYearsInferred;
                _totalYears = value;
                _totalYearsExplicit = true;
                _isTotalYearsInferred = false;
                CalculateLambda();
                if (inferenceChanged)
                    RaisePropertyChange(nameof(IsTotalYearsInferred));
                if (valueChanged)
                    RaisePropertyChange(nameof(TotalYears));
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the default threshold
        /// and total years should be inferred from the data.
        /// </summary>
        [Category("Inputs")]
        [DisplayName("Use Defaults")]
        [Description("Use the default threshold and total observed years inferred from the data.")]
        [Browsable(true)]
        public bool UseDefaults
        {
            get { return _useDefaults; }
            set
            {
                if (_useDefaults != value)
                {
                    _useDefaults = value;
                    if (_useDefaults)
                        SetDefaultThresholdAndTotalYears(forceTotalYears: true);
                    else
                        _totalYearsExplicit = Tools.IsFinite(_totalYears) && _totalYears > 0.0;
                    RaisePropertyChange(nameof(UseDefaults));
                }
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the model should be
        /// treated as seasonal (two GEVs) or non-seasonal (single GEV).
        /// </summary>
        [Category("Model Options")]
        [DisplayName("Is Seasonal")]
        [Description("Specifies whether the point process model includes seasonal effects.")]
        [Browsable(true)]
        public bool IsSeasonal
        {
            get { return _isSeasonal; }
            set
            {
                if (_isSeasonal != value)
                {
                    _isSeasonal = value;
                    RaisePropertyChange(nameof(IsSeasonal));
                    SetDistribution();
                    SetAMSData();
                    SetDefaultParameters();
                }
            }
        }

        /// <summary>
        /// Gets or sets the time block window used to build the block series (default is water year).
        /// </summary>
        [Category("Time Series Options")]
        [DisplayName("Time Block Window")]
        [Description("Defines the time block window for creating the block series (default is water year).")]
        [Browsable(true)]
        public TimeBlockWindow TimeBlock
        {
            get { return _timeBlock; }
            set
            {
                if (_timeBlock != value)
                {
                    _timeBlock = value;
                    RaisePropertyChange(nameof(TimeBlock));
                    SetAMSData();
                    if (UseDefaultFlatPriors)
                        SetDefaultParameters();
                }
            }
        }

        /// <summary>
        /// Gets or sets the starting month for the water year (default is October).
        /// </summary>
        [Category("Time Series Options")]
        [DisplayName("Start Month")]
        [Description("Specifies the starting month for the water year (default is October).")]
        [Browsable(true)]
        public int StartMonth
        {
            get { return _startMonth; }
            set
            {
                if (_startMonth != value)
                {
                    _startMonth = value;
                    RaisePropertyChange(nameof(StartMonth));
                    SetAMSData();
                    if (UseDefaultFlatPriors)
                        SetDefaultParameters();
                }
            }
        }

        #endregion

        #region Methods

        /// <inheritdoc/>
        protected override void DataFrame_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DataFrame.PlottingParameter) ||
                e.PropertyName == "PlottingPosition")
            {
                return;
            }

            if (DataFrame == null)
                return;

            DataFrame.ProcessThresholdSeries();
            RaisePropertyChange(nameof(DataFrame));

            SetAMSData();

            if (UseDefaults)
                SetDefaultThresholdAndTotalYears(forceTotalYears: true);
            else
                CalculateLambda();

            if (UseDefaultFlatPriors)
                SetDefaultParameters();
        }

        /// <summary>
        /// Sets the default threshold and total number of years based on the data.
        /// </summary>
        /// <param name="peaksOverThreshold">Optional threshold used to create a peaks-over-threshold input series.</param>
        /// <param name="forceTotalYears">If true, overwrites a previously explicit <see cref="TotalYears"/> value.</param>
        /// <remarks>
        /// <para>
        /// Point-process exposure is the time span over which exact POT events could have occurred.
        /// Threshold, uncertain, and interval data contribute to the censored magnitude likelihood,
        /// but they do not extend the Poisson exposure duration.
        /// </para>
        /// </remarks>
        public void SetDefaultThresholdAndTotalYears(double? peaksOverThreshold = null, bool forceTotalYears = false)
        {
            if (DataFrame == null)
                return;

            if (DataFrame.ExactSeries.Count > 0)
            {
                double exactMinimum = DataFrame.ExactSeries.MinimumValue();
                if (peaksOverThreshold.HasValue &&
                    Tools.IsFinite(peaksOverThreshold.Value) &&
                    peaksOverThreshold.Value < exactMinimum)
                {
                    Threshold = peaksOverThreshold.Value;
                }
                else
                {
                    Threshold = Math.BitDecrement(exactMinimum);
                }
            }
            else
            {
                // Robust min across non-empty non-exact series; ignore series with no data.
                var minima = new List<double>();

                if (DataFrame.UncertainSeries.Count > 0)
                    minima.Add(DataFrame.UncertainSeries.MinimumValue());
                if (DataFrame.IntervalSeries.Count > 0)
                    minima.Add(DataFrame.IntervalSeries.MinimumValue());

                if (minima.Count > 0)
                    Threshold = Math.BitDecrement(minima.Min());
            }

            bool totalYearsChanged = false;
            bool inferenceChanged = false;
            if (forceTotalYears || !_totalYearsExplicit)
            {
                bool hasSourceExposure = HasStoredSourceExposure();
                double inferred = hasSourceExposure
                    ? DataFrame.PointProcessObservationYears
                    : DataFrame.ExactSeries.Count > 0
                        ? DataFrame.ExactSeries.IndexSpan()
                        : 1.0;

                if (!_totalYears.AlmostEquals(inferred))
                {
                    _totalYears = inferred;
                    totalYearsChanged = true;
                }
                _totalYearsExplicit = false;
                bool isInferred = !hasSourceExposure;
                inferenceChanged = _isTotalYearsInferred != isInferred;
                _isTotalYearsInferred = isInferred;
            }

            CalculateLambda();
            if (inferenceChanged)
                RaisePropertyChange(nameof(IsTotalYearsInferred));
            if (totalYearsChanged)
                RaisePropertyChange(nameof(TotalYears));
        }

        /// <summary>
        /// Calculates λ, the average number of POT events per year.
        /// </summary>
        public void CalculateLambda()
        {
            _lambda = DataFrame == null || !Tools.IsFinite(_totalYears) || _totalYears <= 0.0
                ? double.NaN
                : EmpiricalEventCount / _totalYears;
            RaisePropertyChange(nameof(EmpiricalEventCount));
            RaisePropertyChange(nameof(EmpiricalEventRate));
            RaisePropertyChange(nameof(Lambda));
        }

        /// <summary>
        /// Preprocesses the annual maximum (or block) data and, for seasonal
        /// models, computes the configured calendar- or water-year block day for each POT event.
        /// </summary>
        /// <remarks>
        /// Seasonal processing requires a valid date on every exact observation. Missing dates
        /// or dated block-processing failures leave the derived collections empty, and
        /// <see cref="Validate()"/> reports that the seasonal model cannot be estimated.
        /// </remarks>
        public void SetAMSData()
        {
            _amsDataFrame = new DataFrame();
            _potDays = new List<int>();

            if (DataFrame == null || DataFrame.ExactSeries == null || DataFrame.ExactSeries.Count == 0)
            {
                return;
            }

            if (IsSeasonal && DataFrame.ExactSeries.Cast<ExactData>().Any(data => data.DateTime == default))
                return;

            try
            {
                if (!IsSeasonal)
                {
                    // Get block-maximum values over each unique index.
                    var maxByIndex = DataFrame.ExactSeries.GroupBy(item => item.Index).ToDictionary(group => group.Key, group => group.Max(item => item.Value));
                    foreach (var item in maxByIndex)
                    {
                        _amsDataFrame.ExactSeries.Add(new ExactData(item.Key, item.Value));
                    }
                }
                else
                {
                    // Build irregular time series from exact data.
                    var ts = new TimeSeries(TimeInterval.Irregular);
                    for (int i = 0; i < DataFrame.ExactSeries.Count; i++)
                    {
                        var exact = (ExactData)DataFrame.ExactSeries[i];
                        ts.Add(new SeriesOrdinate<DateTime, double>(exact.DateTime, exact.Value));
                    }

                    // Compute block maxima.
                    _amsDataFrame.CreateBlockSeries(ts, TimeBlock, BlockFunctionType.Maximum, SmoothingFunctionType.None, StartMonth);

                    for (int i = 0; i < ts.Count; i++)
                    {
                        _potDays.Add(GetBlockDay(ts[i].Index));
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"PointProcessModel.SetAMSData: time-series processing skipped: {ex.Message}");
            }
        }

        /// <summary>
        /// Initializes the underlying competing risks distribution with one
        /// (non-seasonal) or two (seasonal) GEV components.
        /// </summary>
        public void SetDistribution()
        {
            if (!IsSeasonal)
            {
                _distribution = new CompetingRisks(new IUnivariateDistribution[] { new GeneralizedExtremeValue() })
                {
                    MinimumOfRandomVariables = false,
                    Dependency = Probability.DependencyType.Independent
                };
            }
            else
            {
                _distribution = new CompetingRisks(new IUnivariateDistribution[] { new GeneralizedExtremeValue(), new GeneralizedExtremeValue() })
                {
                    MinimumOfRandomVariables = false,
                    Dependency = Probability.DependencyType.Independent
                };
            }
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

            _parameters = new List<ModelParameter>();

            if (Distribution is null ||
                DataFrame == null ||
                !DataFrame.Validate().IsValid ||
                Distribution.Distributions == null ||
                !Distribution.Distributions.Any())
            {
                RaisePropertyChange(nameof(SetDefaultParameters));
                return;
            }

            if (Distribution.Distributions.Count() > 1)
            {
                var changePointDefaults = GetDefaultChangePointParameters();

                Parameters.Add(new ModelParameter
                {
                    Name = "Change Point K₁",
                    Value = changePointDefaults[0].Value,
                    LowerBound = changePointDefaults[0].Lower,
                    UpperBound = changePointDefaults[0].Upper,
                    PriorDistribution = new Uniform(changePointDefaults[0].Lower, changePointDefaults[0].Upper)
                });

                Parameters.Add(new ModelParameter
                {
                    Name = "Change Point K₂",
                    Value = changePointDefaults[1].Value,
                    LowerBound = changePointDefaults[1].Lower,
                    UpperBound = changePointDefaults[1].Upper,
                    PriorDistribution = new Uniform(changePointDefaults[1].Lower, changePointDefaults[1].Upper)
                });
            }

            // Priors for GEV distribution parameters.
            for (int i = 0; i < Distribution.Distributions.Count(); i++)
            {
                var gev = Distribution.Distributions[i];

                var tuple = ((IMaximumLikelihoodEstimation)gev)
                    .GetParameterConstraints(AMSDataFrame.ExactSeries.Select(x => x.Value).ToList());

                var initials = tuple.Item1;
                var lowers = tuple.Item2;
                var uppers = tuple.Item3;

                var parametersToString = gev.ParametersToString;
                for (int j = 0; j < gev.NumberOfParameters; j++)
                {
                    Parameters.Add(new ModelParameter
                    {
                        OwnerName = IsSeasonal ? "D" + (i + 1).ToString(CultureInfo.InvariantCulture) : string.Empty,
                        Name = parametersToString[j, 0],
                        Value = initials[j],
                        LowerBound = lowers[j],
                        UpperBound = uppers[j],
                        IsPositive = lowers[j] == Tools.DoubleMachineEpsilon,
                        PriorDistribution = new Uniform(lowers[j], uppers[j])
                    });
                }
            }

            // Add handlers
            for (int i = 0; i < NumberOfParameters; i++)
                Parameters[i].PropertyChanged += Parameter_PropertyChanged;

            RaisePropertyChange(nameof(SetDefaultParameters));
        }

        /// <summary>
        /// Gets broad or histogram-informed default parameters for the two seasonal changepoints.
        /// </summary>
        /// <returns>
        /// Two tuples containing the latent value and inclusive floating-point bounds for
        /// <c>K₁</c> and <c>K₂</c>, respectively.
        /// </returns>
        /// <remarks>
        /// The histogram rule is an empirical initialization aid, not an estimator. It counts
        /// exact dated POT events by month, rotates the bins to the configured block-year start,
        /// and locates broad valleys between two separated seasonal peaks. Ambiguous histograms
        /// retain the approved broad supports <c>K₁ ∈ [1,251)</c> and
        /// <c>K₂ ∈ [200,367)</c>.
        /// </remarks>
        private (double Value, double Lower, double Upper)[] GetDefaultChangePointParameters()
        {
            var broadDefaults = new[]
            {
                (Value: 90.0, Lower: 1.0, Upper: Math.BitDecrement(251.0)),
                (Value: 250.0, Lower: 200.0, Upper: Math.BitDecrement(367.0))
            };

            if (DataFrame == null)
                return broadDefaults;

            int firstMonth = TimeBlock == TimeBlockWindow.CalendarYear ? 1 : StartMonth;
            if (firstMonth < 1 || firstMonth > 12)
                return broadDefaults;

            var monthlyCounts = new double[12];
            int datedCount = 0;
            foreach (ExactData observation in DataFrame.ExactSeries)
            {
                if (observation.DateTime == default)
                    continue;

                int blockMonth = (observation.DateTime.Month - firstMonth + 12) % 12;
                monthlyCounts[blockMonth] += 1.0;
                datedCount++;
            }

            if (datedCount < 10)
                return broadDefaults;

            int[] monthStarts = GetBlockMonthStarts(firstMonth);
            if (IsEffectivelyFlat(monthlyCounts, monthStarts) ||
                !TryFindSeasonalValleys(monthlyCounts, out int firstValley, out int secondValley))
            {
                return broadDefaults;
            }
            if (!TryCreateChangePointDefault(firstValley, monthStarts, 1.0, 251.0, out var firstDefault) ||
                !TryCreateChangePointDefault(secondValley, monthStarts, 200.0, 367.0, out var secondDefault))
            {
                return broadDefaults;
            }

            return new[] { firstDefault, secondDefault };
        }

        /// <summary>
        /// Determines whether monthly counts are consistent with uniform daily occurrence exposure.
        /// </summary>
        /// <param name="monthlyCounts">Monthly counts ordered from the configured block-year start.</param>
        /// <param name="monthStarts">Canonical one-based block-month boundaries.</param>
        /// <returns><c>true</c> when broad random variation should not be interpreted as two seasons.</returns>
        /// <remarks>
        /// Expected counts are proportional to the number of modeled days in each month. The
        /// fixed cutoff 19.675 is the 95th percentile of a chi-square distribution with 11
        /// degrees of freedom. This guard only selects the broad prior fallback; it is not a
        /// point-process parameter estimate or a likelihood calculation.
        /// </remarks>
        private static bool IsEffectivelyFlat(double[] monthlyCounts, int[] monthStarts)
        {
            double total = monthlyCounts.Sum();
            if (total <= 0.0)
                return true;

            double statistic = 0.0;
            for (int i = 0; i < monthlyCounts.Length; i++)
            {
                double expected = total * (monthStarts[i + 1] - monthStarts[i]) / 366.0;
                double difference = monthlyCounts[i] - expected;
                statistic += difference * difference / expected;
            }

            return statistic <= 19.675;
        }

        /// <summary>
        /// Locates the two seasonal valleys in a twelve-bin monthly occurrence histogram.
        /// </summary>
        /// <param name="monthlyCounts">Monthly counts ordered from the configured block-year start.</param>
        /// <param name="firstValley">The earlier valley-month index in block-year order.</param>
        /// <param name="secondValley">The later valley-month index in block-year order.</param>
        /// <returns><c>true</c> when two separated peaks and their intervening valleys are identifiable.</returns>
        /// <remarks>
        /// Detection uses one circular <c>[1,2,1]/4</c> smoothing pass. The smoothing affects
        /// only the default-prior neighborhood; it does not alter observations or likelihoods.
        /// </remarks>
        private static bool TryFindSeasonalValleys(double[] monthlyCounts, out int firstValley, out int secondValley)
        {
            firstValley = -1;
            secondValley = -1;

            double minimum = monthlyCounts.Min();
            double maximum = monthlyCounts.Max();
            if (maximum <= minimum)
                return false;

            var smoothed = new double[12];
            for (int i = 0; i < smoothed.Length; i++)
            {
                int previous = (i + 11) % 12;
                int next = (i + 1) % 12;
                smoothed[i] = (monthlyCounts[previous] + 2.0 * monthlyCounts[i] + monthlyCounts[next]) / 4.0;
            }

            var peaks = new List<int>();
            for (int i = 0; i < smoothed.Length; i++)
            {
                double previous = smoothed[(i + 11) % 12];
                double next = smoothed[(i + 1) % 12];
                if (smoothed[i] >= previous && smoothed[i] >= next &&
                    (smoothed[i] > previous || smoothed[i] > next))
                {
                    peaks.Add(i);
                }
            }

            double mean = smoothed.Average();
            int bestFirstPeak = -1;
            int bestSecondPeak = -1;
            double bestScore = double.NegativeInfinity;
            int bestPairCount = 0;
            for (int i = 0; i < peaks.Count; i++)
            {
                for (int j = i + 1; j < peaks.Count; j++)
                {
                    int separation = (peaks[j] - peaks[i] + 12) % 12;
                    if (separation < 3 || separation > 9 ||
                        smoothed[peaks[i]] <= mean || smoothed[peaks[j]] <= mean)
                    {
                        continue;
                    }

                    double score = smoothed[peaks[i]] + smoothed[peaks[j]];
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestFirstPeak = peaks[i];
                        bestSecondPeak = peaks[j];
                        bestPairCount = 1;
                    }
                    else if (score == bestScore)
                    {
                        bestPairCount++;
                    }
                }
            }

            if (bestPairCount != 1)
                return false;

            int valleyA = FindCircularValley(smoothed, bestFirstPeak, bestSecondPeak);
            int valleyB = FindCircularValley(smoothed, bestSecondPeak, bestFirstPeak);
            if (valleyA < 0 || valleyB < 0 ||
                smoothed[valleyA] >= Math.Min(smoothed[bestFirstPeak], smoothed[bestSecondPeak]) ||
                smoothed[valleyB] >= Math.Min(smoothed[bestFirstPeak], smoothed[bestSecondPeak]))
            {
                return false;
            }

            firstValley = Math.Min(valleyA, valleyB);
            secondValley = Math.Max(valleyA, valleyB);
            return true;
        }

        /// <summary>
        /// Finds the lowest monthly bin on the forward circular arc between two peak bins.
        /// </summary>
        /// <param name="values">The smoothed circular monthly values.</param>
        /// <param name="startPeak">The excluded peak at the beginning of the arc.</param>
        /// <param name="endPeak">The excluded peak at the end of the arc.</param>
        /// <returns>The valley-month index, using the middle tied cell when a valley is broad.</returns>
        private static int FindCircularValley(double[] values, int startPeak, int endPeak)
        {
            var valleyCandidates = new List<int>();
            double valleyValue = double.PositiveInfinity;
            for (int index = (startPeak + 1) % 12; index != endPeak; index = (index + 1) % 12)
            {
                if (values[index] < valleyValue)
                {
                    valleyValue = values[index];
                    valleyCandidates.Clear();
                    valleyCandidates.Add(index);
                }
                else if (values[index] == valleyValue)
                {
                    valleyCandidates.Add(index);
                }
            }

            return valleyCandidates.Count == 0 ? -1 : valleyCandidates[(valleyCandidates.Count - 1) / 2];
        }

        /// <summary>
        /// Builds canonical leap-year block-month boundaries for a selected start month.
        /// </summary>
        /// <param name="firstMonth">The calendar month corresponding to the first block-month bin.</param>
        /// <returns>Thirteen one-based boundaries spanning <c>[1,367]</c>.</returns>
        private static int[] GetBlockMonthStarts(int firstMonth)
        {
            var starts = new int[13];
            starts[0] = 1;
            for (int i = 0; i < 12; i++)
            {
                int calendarMonth = (firstMonth - 1 + i) % 12 + 1;
                starts[i + 1] = starts[i] + DateTime.DaysInMonth(2000, calendarMonth);
            }

            return starts;
        }

        /// <summary>
        /// Creates a five-month flat changepoint support centered on a detected valley month.
        /// </summary>
        /// <param name="valleyMonth">The zero-based valley-month index in block-year order.</param>
        /// <param name="monthStarts">Canonical one-based block-month boundaries.</param>
        /// <param name="baseLower">Inclusive approved lower support.</param>
        /// <param name="baseUpperExclusive">Exclusive approved upper support.</param>
        /// <param name="result">The resulting latent value and floating-point bounds.</param>
        /// <returns><c>true</c> when the detected valley and its intersected window are valid.</returns>
        private static bool TryCreateChangePointDefault(
            int valleyMonth,
            int[] monthStarts,
            double baseLower,
            double baseUpperExclusive,
            out (double Value, double Lower, double Upper) result)
        {
            double value = 0.5 * (monthStarts[valleyMonth] + monthStarts[valleyMonth + 1]);
            double lower = Math.Max(baseLower, monthStarts[Math.Max(0, valleyMonth - 2)]);
            double upperExclusive = Math.Min(baseUpperExclusive, monthStarts[Math.Min(12, valleyMonth + 3)]);
            if (value < lower || value >= upperExclusive || lower >= upperExclusive)
            {
                result = default;
                return false;
            }

            result = (value, lower, Math.BitDecrement(upperExclusive));
            return true;
        }

        /// <inheritdoc/>
        public override void SetDefaultQuantilePriors()
        {
            if (Distribution is null)
                return;

            // Remove old handlers
            if (_quantilePriors != null)
            {
                for (int i = 0; i < _quantilePriors.Count; i++)
                    _quantilePriors[i].PropertyChanged -= QuantilePrior_PropertyChanged;
            }

            // If quantile priors are not enabled, clear the list and return
            if (EnableQuantilePriors == false)
            {
                _quantilePriors = new List<QuantilePrior>();
                _quantilePriorsTrue = new List<QuantilePrior>();
                RaisePropertyChange(nameof(SetDefaultQuantilePriors));
                return;
            }

            var priors = new List<QuantilePrior>();
            int qCount = 1;

            // For single GEV, allow up to 3 quantile priors (one per parameter).
            if (!UseSingleQuantile && Distribution.Distributions.Count() == 1)
            {
                qCount = 3;
            }

            // Respect existing priors when possible.
            if (QuantilePriors.Count >= 1)
            {
                priors = QuantilePriors.ToList();

                // Trim excess priors.
                while (priors.Count > qCount)
                    priors.Remove(priors.Last());

                // Add missing priors by refining the last alpha.
                while (priors.Count < qCount)
                {
                    priors.Add(new QuantilePrior(priors.Last().Alpha / 10.0, new LnNormal()));
                    double mu = Math.Round(Distribution.InverseCDF(1 - priors.Last().Alpha), 2);
                    double sigma = Math.Round(mu * 0.15, 2);
                    priors.Last().Distribution.SetParameters(new double[] { mu, sigma });
                }
            }
            else
            {
                // Create default priors: α = 0.1, 0.01, 0.001, ... up to qCount.
                for (int i = 1; i <= qCount; i++)
                {
                    priors.Add(new QuantilePrior(Math.Pow(10, -i), new LnNormal()));
                    double mu = Math.Round(Distribution.InverseCDF(1 - priors[i - 1].Alpha), 2);
                    double sigma = Math.Round(mu * 0.15, 2);
                    priors[i - 1].Distribution.SetParameters(new double[] { mu, sigma });
                }
            }

            // Reset the quantile priors with the new list and add handlers
            _quantilePriors = priors;
            for (int i = 0; i < _quantilePriors.Count; i++)
                _quantilePriors[i].PropertyChanged += QuantilePrior_PropertyChanged;

            ProcessQuantilePriors();

            RaisePropertyChange(nameof(SetDefaultQuantilePriors));
        }

        /// <inheritdoc/>
        public override void ProcessQuantilePriors()
        {
            _quantilePriorsTrue.Clear();

            if (QuantilePriors.Count == 1)
            {
                _quantilePriorsTrue.Add(QuantilePriors[0].Clone());
                return;
            }

            if (!UseSingleQuantile &&
                Distribution is not null &&
                Distribution.Distributions.Count() == 1 &&
                QuantilePriors.Count == 3)
            {
                // First quantile prior remains unchanged.
                _quantilePriorsTrue.Add(QuantilePriors[0].Clone());

                // Convert remaining priors to gamma distributions on the
                // differences between random quantiles.
                for (int i = 1; i < QuantilePriors.Count; i++)
                {
                    double muDiff = QuantilePriors[i].Distribution.Mean - QuantilePriors[i - 1].Distribution.Mean;
                    double sigmaDiff = Math.Sqrt(QuantilePriors[i].Distribution.Variance + QuantilePriors[i - 1].Distribution.Variance);

                    var gamma = new GammaDistribution();
                    gamma.SetParameters(gamma.ParametersFromMoments(new[] { muDiff, sigmaDiff }));
                    _quantilePriorsTrue.Add(new QuantilePrior(QuantilePriors[i].Alpha, gamma));
                }
            }
        }


        /// <inheritdoc/>
        public override double LogLikelihood(double[] parameters)
        {
            double dataLogLH = DataLogLikelihood(parameters);
            if (!Tools.IsFinite(dataLogLH) || dataLogLH <= double.NegativeInfinity)
                return double.NegativeInfinity;

            double priorLogLH = PriorLogLikelihood(parameters);
            if (!Tools.IsFinite(priorLogLH) || priorLogLH <= double.NegativeInfinity)
                return double.NegativeInfinity;

            double logLH = dataLogLH + priorLogLH;
            return Tools.IsFinite(logLH) ? logLH : double.NegativeInfinity;
        }

        /// <inheritdoc/>
        public override double DataLogLikelihood(double[] parameters)
        {
            if (Distribution is null || DataFrame is null)
                return double.NegativeInfinity;

            var model = (CompetingRisks)Distribution.Clone();
            double logLH = 0.0;

            double u = Threshold;
            double Ny = TotalYears;

            if (double.IsNaN(u) || double.IsNaN(Ny) || Ny <= 0)
                return double.NegativeInfinity;

            int k1 = 0;
            int k2 = 0;
            double ny1 = 0.0;
            double ny2 = 0.0;

            // Set model parameters (accounting for seasonal transformation).
            if (!IsSeasonal)
            {
                model.SetParameters(parameters);
            }
            else
            {
                if (parameters.Length < 8)
                    return double.NegativeInfinity;

                if (!TryGetEffectiveChangePoints(parameters, out k1, out k2))
                    return double.NegativeInfinity;

                ny1 = Ny * (k1 + (366.0 - k2)) / 366.0;
                ny2 = Ny * (k2 - k1) / 366.0;

                model.SetParameters(parameters.Skip(2).ToArray());
            }

            // Exact data contribution.
            if (!IsSeasonal)
            {
                var gev = model.Distributions[0] as GeneralizedExtremeValue;
                if (gev is null)
                    return double.NegativeInfinity;

                double loc = gev.Xi;
                double scl = gev.Alpha;
                double shp = -gev.Kappa; // Convert to Coles parameterization

                if (scl <= 0.0)
                    return double.NegativeInfinity;

                if (Math.Abs(shp) < 1E-4)
                {
                    // Gumbel (shape approximately zero).
                    foreach (var data in DataFrame.ExactSeries.Cast<ExactData>())
                    {
                        double x = data.Value;
                        logLH += -Math.Log(scl) - (x - loc) / scl;
                    }

                    logLH += -Ny * Math.Exp(-(u - loc) / scl);
                }
                else
                {
                    // General case (shape ≠ 0).
                    foreach (var data in DataFrame.ExactSeries.Cast<ExactData>())
                    {
                        double x = data.Value;
                        double z = 1.0 + shp * ((x - loc) / scl);
                        if (z <= 0.0)
                            return double.NegativeInfinity;

                        logLH += -Math.Log(scl) - (1.0 + 1.0 / shp) * Math.Log(z);
                    }

                    double zu = 1.0 + shp * ((u - loc) / scl);
                    if (zu <= 0.0)
                        return double.NegativeInfinity;

                    logLH += -Ny * Math.Pow(zu, -1.0 / shp);
                }
            }
            else
            {
                if (POTDays.Count != DataFrame.ExactSeries.Count)
                    return double.NegativeInfinity;

                var gev1 = model.Distributions[0] as GeneralizedExtremeValue;
                var gev2 = model.Distributions[1] as GeneralizedExtremeValue;

                if (gev1 is null || gev2 is null)
                    return double.NegativeInfinity;

                double loc1 = gev1.Xi;
                double scl1 = gev1.Alpha;
                double shp1 = -gev1.Kappa;

                double loc2 = gev2.Xi;
                double scl2 = gev2.Alpha;
                double shp2 = -gev2.Kappa;

                if (scl1 <= 0.0 || scl2 <= 0.0)
                    return double.NegativeInfinity;

                // Loop over POT days and assign to season 1 or 2.
                for (int i = 0; i < POTDays.Count && i < DataFrame.ExactSeries.Count; i++)
                {
                    double x = DataFrame.ExactSeries[i].Value;
                    int day = POTDays[i];

                    if (IsSeasonOneDay(day, k1, k2))
                    {
                        // Season 1
                        if (Math.Abs(shp1) < 1E-4)
                        {
                            logLH += -Math.Log(scl1) - (x - loc1) / scl1;
                        }
                        else
                        {
                            double z = 1.0 + shp1 * ((x - loc1) / scl1);
                            if (z <= 0.0)
                                return double.NegativeInfinity;

                            logLH += -Math.Log(scl1) - (1.0 + 1.0 / shp1) * Math.Log(z);
                        }
                    }
                    else
                    {
                        // Season 2
                        if (Math.Abs(shp2) < 1E-4)
                        {
                            logLH += -Math.Log(scl2) - (x - loc2) / scl2;
                        }
                        else
                        {
                            double z = 1.0 + shp2 * ((x - loc2) / scl2);
                            if (z <= 0.0)
                                return double.NegativeInfinity;

                            logLH += -Math.Log(scl2) - (1.0 + 1.0 / shp2) * Math.Log(z);
                        }
                    }
                }

                // Seasonal exceedance components.
                if (Math.Abs(shp1) < 1E-4)
                {
                    logLH += -ny1 * Math.Exp(-(u - loc1) / scl1);
                }
                else
                {
                    double zu1 = 1.0 + shp1 * ((u - loc1) / scl1);
                    if (zu1 <= 0.0)
                        return double.NegativeInfinity;

                    logLH += -ny1 * Math.Pow(zu1, -1.0 / shp1);
                }

                if (Math.Abs(shp2) < 1E-4)
                {
                    logLH += -ny2 * Math.Exp(-(u - loc2) / scl2);
                }
                else
                {
                    double zu2 = 1.0 + shp2 * ((u - loc2) / scl2);
                    if (zu2 <= 0.0)
                        return double.NegativeInfinity;

                    logLH += -ny2 * Math.Pow(zu2, -1.0 / shp2);
                }
            }

            CompetingRisks annualizedModel = IsSeasonal ? GetDistribution(parameters) : model;

            // Non-exact records are block-indexed annual observations and therefore use the
            // annual competing-risk distribution rather than a season-specific event likelihood.
            // Uncertain Data
            for (int i = 0; i < DataFrame.UncertainSeries.Count; i++)
            {
                var dist = ((UncertainData)DataFrame.UncertainSeries[i]).Distribution;
                double lowerProbability = 1E-8;
                double upperProbability = 1.0 - 1E-8;
                var a = dist.InverseCDF(lowerProbability);
                var b = dist.InverseCDF(upperProbability);
                double mass = upperProbability - lowerProbability;
                if (Tools.IsFinite(a) && Tools.IsFinite(b) && Tools.IsFinite(mass) && mass > 0.0 && a < b)
                {
                    // Normalize by retained ME mass from the 1E-8 probability window.
                    var ep = Integration.GaussLegendre20((q) => { return dist.PDF(q) * annualizedModel.PDF(q); }, a, b) / mass;
                    logLH += ep > 0 ? Math.Log(ep) : double.NegativeInfinity;
                }
                else
                {
                    logLH += double.NegativeInfinity;
                }
            }

            // Interval Data
            for (int i = 0; i < DataFrame.IntervalSeries.Count; i++)
            {
                var data = (IntervalData)DataFrame.IntervalSeries[i];
                logLH += annualizedModel.LogLikelihood_Intervals(data.LowerValue, data.UpperValue);
            }

            // Threshold Data
            for (int i = 0; i < DataFrame.ThresholdSeries.Count; i++)
            {
                var data = (ThresholdData)DataFrame.ThresholdSeries[i];
                if (data.NumberBelow > 0)
                    logLH += annualizedModel.LogLikelihood_LeftCensored(data.Value, data.NumberBelow);
                if (data.NumberAbove > 0)
                    logLH += annualizedModel.LogLikelihood_RightCensored(data.Value, data.NumberAbove);
            }
            return logLH;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// <para>
        /// Per-observation log-likelihood contributions for WAIC and LOO-CV. The
        /// allocation matches the Poisson-GPD/GEV process structure:
        /// </para>
        /// <list type="bullet">
        /// <item><description><b>Exact data</b> — per-event GEV log-PDF + a 1/N_exact share
        /// of the global Poisson rate term <c>-λ·N_y·G(u)</c>. Each exceedance
        /// represents one Poisson event, so distributing the rate term across the
        /// exact observations is the natural per-observation attribution.</description></item>
        /// <item><description><b>Uncertain / Interval / Threshold data</b> — composite
        /// (competing-risks) GEV likelihood only. These data types are
        /// block-indexed (one entry per period) and do not represent Poisson
        /// events; the rate term does NOT contribute to their pointwise share.</description></item>
        /// </list>
        /// <para>
        /// This preserves the sum invariant <c>Σ pointwise == DataLogLikelihood</c>
        /// while keeping each pointwise entry an interpretable per-observation
        /// quantity (which earlier "smear evenly across all observations" did not).
        /// </para>
        /// </remarks>
        public override double[] PointwiseDataLogLikelihood(double[] parameters)
        {
            if (Distribution is null || DataFrame is null)
                return Array.Empty<double>();

            var model = (CompetingRisks)Distribution.Clone();

            double u = Threshold;
            double Ny = TotalYears;

            if (double.IsNaN(u) || double.IsNaN(Ny) || Ny <= 0)
                return Array.Empty<double>();

            var result = new List<double>();

            int nExact = DataFrame.ExactSeries.Count;
            int totalObs = nExact + DataFrame.UncertainSeries.Count +
                           DataFrame.IntervalSeries.Count + DataFrame.ThresholdSeries.Count;

            if (totalObs == 0)
                return Array.Empty<double>();

            int k1 = 0;
            int k2 = 0;
            double ny1 = 0.0;
            double ny2 = 0.0;

            // Set model parameters
            if (!IsSeasonal)
            {
                model.SetParameters(parameters);
            }
            else
            {
                if (parameters.Length < 8)
                    return Array.Empty<double>();

                if (!TryGetEffectiveChangePoints(parameters, out k1, out k2))
                    return Array.Empty<double>();

                ny1 = Ny * (k1 + (366.0 - k2)) / 366.0;
                ny2 = Ny * (k2 - k1) / 366.0;

                model.SetParameters(parameters.Skip(2).ToArray());
            }

            if (IsSeasonal && POTDays.Count != DataFrame.ExactSeries.Count)
                return Array.Empty<double>();

            // Compute the global rate term to be distributed across observations
            double rateTerm = 0.0;

            if (!IsSeasonal)
            {
                var gev = model.Distributions[0] as GeneralizedExtremeValue;
                if (gev is null)
                    return Array.Empty<double>();

                double loc = gev.Xi;
                double scl = gev.Alpha;
                double shp = -gev.Kappa;

                if (scl <= 0.0)
                    return Array.Empty<double>();

                if (Math.Abs(shp) < 1E-4)
                {
                    rateTerm = -Ny * Math.Exp(-(u - loc) / scl);
                }
                else
                {
                    double zu = 1.0 + shp * ((u - loc) / scl);
                    if (zu <= 0.0)
                        return Array.Empty<double>();

                    rateTerm = -Ny * Math.Pow(zu, -1.0 / shp);
                }
            }
            else
            {

                var gev1 = model.Distributions[0] as GeneralizedExtremeValue;
                var gev2 = model.Distributions[1] as GeneralizedExtremeValue;

                if (gev1 is null || gev2 is null)
                    return Array.Empty<double>();

                double loc1 = gev1.Xi;
                double scl1 = gev1.Alpha;
                double shp1 = -gev1.Kappa;

                double loc2 = gev2.Xi;
                double scl2 = gev2.Alpha;
                double shp2 = -gev2.Kappa;

                if (scl1 <= 0.0 || scl2 <= 0.0)
                    return Array.Empty<double>();

                // Season 1 rate term
                if (Math.Abs(shp1) < 1E-4)
                {
                    rateTerm += -ny1 * Math.Exp(-(u - loc1) / scl1);
                }
                else
                {
                    double zu1 = 1.0 + shp1 * ((u - loc1) / scl1);
                    if (zu1 <= 0.0)
                        return Array.Empty<double>();

                    rateTerm += -ny1 * Math.Pow(zu1, -1.0 / shp1);
                }

                // Season 2 rate term
                if (Math.Abs(shp2) < 1E-4)
                {
                    rateTerm += -ny2 * Math.Exp(-(u - loc2) / scl2);
                }
                else
                {
                    double zu2 = 1.0 + shp2 * ((u - loc2) / scl2);
                    if (zu2 <= 0.0)
                        return Array.Empty<double>();

                    rateTerm += -ny2 * Math.Pow(zu2, -1.0 / shp2);
                }
            }

            // Distribute the global Poisson rate term across exact observations
            // ONLY. Each exact event represents one Poisson exceedance, so its
            // 1/N_exact share of -λ·N_y·G(u) is the natural per-observation
            // attribution. Uncertain / Interval / Threshold data are block-indexed
            // (one entry per period, not per Poisson event) and contribute the
            // composite GEV likelihood with no rate-term share.
            //
            // If N_exact == 0 (degenerate POT with no exact data), the rate term
            // is added to the first non-exact observation as a fallback so the
            // sum invariant Σ pointwise == DataLogLikelihood is preserved.
            double ratePerExact = nExact > 0 ? rateTerm / nExact : 0.0;
            bool fallbackRateApplied = nExact > 0;

            // Exact data contribution (with per-observation PDF + rate share)
            if (!IsSeasonal)
            {
                var gev = (GeneralizedExtremeValue)model.Distributions[0];
                double loc = gev.Xi;
                double scl = gev.Alpha;
                double shp = -gev.Kappa;

                foreach (var data in DataFrame.ExactSeries.Cast<ExactData>())
                {
                    double x = data.Value;
                    double ll;

                    if (Math.Abs(shp) < 1E-4)
                    {
                        ll = -Math.Log(scl) - (x - loc) / scl;
                    }
                    else
                    {
                        double z = 1.0 + shp * ((x - loc) / scl);
                        if (z <= 0.0)
                        {
                            ll = double.NegativeInfinity;
                        }
                        else
                        {
                            ll = -Math.Log(scl) - (1.0 + 1.0 / shp) * Math.Log(z);
                        }
                    }
                    result.Add(ll + ratePerExact);
                }
            }
            else
            {
                var gev1 = (GeneralizedExtremeValue)model.Distributions[0];
                var gev2 = (GeneralizedExtremeValue)model.Distributions[1];

                double loc1 = gev1.Xi;
                double scl1 = gev1.Alpha;
                double shp1 = -gev1.Kappa;

                double loc2 = gev2.Xi;
                double scl2 = gev2.Alpha;
                double shp2 = -gev2.Kappa;

                for (int i = 0; i < POTDays.Count && i < DataFrame.ExactSeries.Count; i++)
                {
                    double x = DataFrame.ExactSeries[i].Value;
                    int day = POTDays[i];
                    double ll;

                    if (IsSeasonOneDay(day, k1, k2))
                    {
                        // Season 1
                        if (Math.Abs(shp1) < 1E-4)
                        {
                            ll = -Math.Log(scl1) - (x - loc1) / scl1;
                        }
                        else
                        {
                            double z = 1.0 + shp1 * ((x - loc1) / scl1);
                            ll = z <= 0.0 ? double.NegativeInfinity : -Math.Log(scl1) - (1.0 + 1.0 / shp1) * Math.Log(z);
                        }
                    }
                    else
                    {
                        // Season 2
                        if (Math.Abs(shp2) < 1E-4)
                        {
                            ll = -Math.Log(scl2) - (x - loc2) / scl2;
                        }
                        else
                        {
                            double z = 1.0 + shp2 * ((x - loc2) / scl2);
                            ll = z <= 0.0 ? double.NegativeInfinity : -Math.Log(scl2) - (1.0 + 1.0 / shp2) * Math.Log(z);
                        }
                    }
                    result.Add(ll + ratePerExact);
                }
            }

            CompetingRisks annualizedModel = IsSeasonal ? GetDistribution(parameters) : model;

            // Uncertain Data — annual composite GEV likelihood only (no rate-term share).
            for (int i = 0; i < DataFrame.UncertainSeries.Count; i++)
            {
                var dist = ((UncertainData)DataFrame.UncertainSeries[i]).Distribution;
                double lowerProbability = 1E-8;
                double upperProbability = 1.0 - 1E-8;
                var a = dist.InverseCDF(lowerProbability);
                var b = dist.InverseCDF(upperProbability);
                double mass = upperProbability - lowerProbability;
                double ll;
                if (Tools.IsFinite(a) && Tools.IsFinite(b) && Tools.IsFinite(mass) && mass > 0.0 && a < b)
                {
                    var ep = Integration.GaussLegendre20((q) => { return dist.PDF(q) * annualizedModel.PDF(q); }, a, b) / mass;
                    ll = ep > 0 ? Math.Log(ep) : double.NegativeInfinity;
                }
                else
                {
                    ll = double.NegativeInfinity;
                }
                // Degenerate-case fallback: if there are no exact observations,
                // attach the global rate term to the first non-exact entry so the
                // sum invariant is preserved.
                if (!fallbackRateApplied)
                {
                    ll += rateTerm;
                    fallbackRateApplied = true;
                }
                result.Add(ll);
            }

            // Interval Data — composite GEV likelihood only.
            for (int i = 0; i < DataFrame.IntervalSeries.Count; i++)
            {
                var data = (IntervalData)DataFrame.IntervalSeries[i];
                double ll = annualizedModel.LogLikelihood_Intervals(data.LowerValue, data.UpperValue);
                if (!fallbackRateApplied)
                {
                    ll += rateTerm;
                    fallbackRateApplied = true;
                }
                result.Add(ll);
            }

            // Threshold Data — composite GEV likelihood only.
            for (int i = 0; i < DataFrame.ThresholdSeries.Count; i++)
            {
                var data = (ThresholdData)DataFrame.ThresholdSeries[i];
                double ll = 0.0;
                if (data.NumberBelow > 0)
                    ll += annualizedModel.LogLikelihood_LeftCensored(data.Value, data.NumberBelow);
                if (data.NumberAbove > 0)
                    ll += annualizedModel.LogLikelihood_RightCensored(data.Value, data.NumberAbove);
                if (!fallbackRateApplied)
                {
                    ll += rateTerm;
                    fallbackRateApplied = true;
                }
                result.Add(ll);
            }

            return result.ToArray();
        }

        /// <inheritdoc/>
        public override List<DataComponent> PointwiseDataLogLikelihoodComponents(double[] parameters)
        {
            if (Distribution is null || DataFrame is null)
                return new List<DataComponent>();

            // Get the raw log-likelihoods
            var logLiks = PointwiseDataLogLikelihood(parameters);
            var result = new List<DataComponent>(logLiks.Length);

            int idx = 0;

            // Exact Data
            for (int i = 0; i < DataFrame.ExactSeries.Count && idx < logLiks.Length; i++)
            {
                var data = (ExactData)DataFrame.ExactSeries[i];
                result.Add(new DataComponent(idx, logLiks[idx], data.Value, DataComponentType.Exact, 1, data.Index.ToString()));
                idx++;
            }

            // Uncertain Data
            for (int i = 0; i < DataFrame.UncertainSeries.Count && idx < logLiks.Length; i++)
            {
                var data = (UncertainData)DataFrame.UncertainSeries[i];
                result.Add(new DataComponent(idx, logLiks[idx], data.Distribution.Mean, DataComponentType.Uncertain, 1, data.Index.ToString()));
                idx++;
            }

            // Interval Data
            for (int i = 0; i < DataFrame.IntervalSeries.Count && idx < logLiks.Length; i++)
            {
                var data = (IntervalData)DataFrame.IntervalSeries[i];
                double midpoint = (data.LowerValue + data.UpperValue) / 2.0;
                result.Add(new DataComponent(idx, logLiks[idx], midpoint, DataComponentType.Interval, 1, data.Index.ToString()));
                idx++;
            }

            // Threshold Data
            for (int i = 0; i < DataFrame.ThresholdSeries.Count && idx < logLiks.Length; i++)
            {
                var data = (ThresholdData)DataFrame.ThresholdSeries[i];
                int totalCount = data.NumberBelow + data.NumberAbove;
                result.Add(new DataComponent(idx, logLiks[idx], data.Value, DataComponentType.LeftCensored, totalCount, $"Threshold {i + 1}"));
                idx++;
            }

            return result;
        }

        /// <inheritdoc/>
        public override double PriorLogLikelihood(double[] parameters)
        {
            if (Distribution is null || Parameters is null)
                return double.NegativeInfinity;

            var model = (CompetingRisks)Distribution.Clone();
            int k = model.Distributions.Count;
            double logLH = 0.0;

            // Set model parameters
            if (IsSeasonal == false)
            {
                model.SetParameters(parameters);
            }
            else
            {
                model.SetParameters(parameters.Skip(2).ToArray());
            }

            // Parameter priors (including change-point parameters if seasonal).
            for (int i = 0; i < Parameters.Count; i++)
            {
                logLH += Parameters[i].PriorDistribution.LogPDF(parameters[i]);

            }
            // Jeffreys rule on scale for each GEV component.
            if (UseJeffreysRuleForScale == true)
            {
                for (int j = 0; j < k; j++)
                {
                    double scale = model.Distributions[j].GetParameters[1];
                    logLH -= scale > 0 ? Math.Log(scale) : double.PositiveInfinity;
                }
            }

            // Quantile Priors
            if (EnableQuantilePriors && UseSingleQuantile && _quantilePriorsTrue.Count == 1)
            {
                CompetingRisks? quantileModel = GetQuantilePriorDistribution(model, parameters);
                if (quantileModel is null)
                    return double.NegativeInfinity;
                logLH += _quantilePriorsTrue[0].Distribution.LogPDF(quantileModel.InverseCDF(1 - _quantilePriorsTrue[0].Alpha));
            }
            else if (EnableQuantilePriors && !UseSingleQuantile &&
                Distribution is not null && Distribution.Distributions.Count() == 1 &&
                _quantilePriorsTrue.Count == 3)
            {
                // First quantile prior
                logLH += _quantilePriorsTrue[0].Distribution.LogPDF(model.InverseCDF(1 - _quantilePriorsTrue[0].Alpha));

                // Differences
                var pVals = new double[_quantilePriorsTrue.Count];
                pVals[0] = 1 - _quantilePriorsTrue[0].Alpha;

                for (int i = 1; i < _quantilePriorsTrue.Count; i++)
                {
                    pVals[i] = 1 - _quantilePriorsTrue[i].Alpha;

                    double qCurr = model.InverseCDF(1 - _quantilePriorsTrue[i].Alpha);
                    double qPrev = model.InverseCDF(1 - _quantilePriorsTrue[i - 1].Alpha);
                    double diff = qCurr - qPrev;

                    logLH += _quantilePriorsTrue[i].Distribution.LogPDF(diff);
                }

                // Jacobian determinant for transformation from parameters to quantiles.
                ((IStandardError)model.Distributions[0]).QuantileJacobian(pVals, out var D);
                logLH += D != 0 ? Math.Log(Math.Abs(D)) : double.NegativeInfinity;
            }

            return logLH;
        }

        /// <inheritdoc/>
        public override List<PriorComponent> PointwisePriorLogLikelihood(double[] parameters)
        {
            var result = new List<PriorComponent>();

            if (Distribution is null || Parameters is null)
                return result;

            var model = (CompetingRisks)Distribution.Clone();
            int k = model.Distributions.Count;

            // Set model parameters
            if (!IsSeasonal)
            {
                model.SetParameters(parameters);
            }
            else
            {
                model.SetParameters(parameters.Skip(2).ToArray());
            }

            // Parameter priors (including change-point parameters if seasonal)
            for (int i = 0; i < Parameters.Count; i++)
            {
                double ll = Parameters[i].PriorDistribution.LogPDF(parameters[i]);
                string paramName = string.IsNullOrEmpty(Parameters[i].OwnerName) ? Parameters[i].Name : Parameters[i].OwnerName;
                string name = $"Parameter Prior: {paramName}";
                result.Add(new PriorComponent(name, ll, PriorComponentType.ParameterPrior));
            }

            // Jeffreys rule on scale for each GEV component
            if (UseJeffreysRuleForScale)
            {
                for (int j = 0; j < k; j++)
                {
                    double scale = model.Distributions[j].GetParameters[1];
                    double ll = scale > 0 ? -Math.Log(scale) : double.NegativeInfinity;
                    string scaleName = IsSeasonal ? $"D{j + 1}.Scale" : "Scale";
                    result.Add(new PriorComponent($"Jeffreys Scale: {scaleName}", ll, PriorComponentType.JeffreysScalePrior));
                }
            }

            // Quantile Priors
            if (EnableQuantilePriors && UseSingleQuantile && _quantilePriorsTrue.Count == 1)
            {
                CompetingRisks? quantileModel = GetQuantilePriorDistribution(model, parameters);
                double ll = double.NegativeInfinity;
                if (quantileModel is not null)
                {
                    double quantile = quantileModel.InverseCDF(1 - _quantilePriorsTrue[0].Alpha);
                    ll = _quantilePriorsTrue[0].Distribution.LogPDF(quantile);
                }
                result.Add(new PriorComponent($"Quantile Prior: p={_quantilePriorsTrue[0].Alpha:G4}", ll, PriorComponentType.QuantilePrior));
            }
            else if (EnableQuantilePriors && !UseSingleQuantile &&
                Distribution is not null && Distribution.Distributions.Count() == 1 &&
                _quantilePriorsTrue.Count == 3)
            {
                // First quantile prior
                double q0 = model.InverseCDF(1 - _quantilePriorsTrue[0].Alpha);
                double ll0 = _quantilePriorsTrue[0].Distribution.LogPDF(q0);
                result.Add(new PriorComponent($"Quantile Prior: p={_quantilePriorsTrue[0].Alpha:G4}", ll0, PriorComponentType.QuantilePrior));

                // Differences
                var pVals = new double[_quantilePriorsTrue.Count];
                pVals[0] = 1 - _quantilePriorsTrue[0].Alpha;

                for (int i = 1; i < _quantilePriorsTrue.Count; i++)
                {
                    pVals[i] = 1 - _quantilePriorsTrue[i].Alpha;

                    double qCurr = model.InverseCDF(1 - _quantilePriorsTrue[i].Alpha);
                    double qPrev = model.InverseCDF(1 - _quantilePriorsTrue[i - 1].Alpha);
                    double diff = qCurr - qPrev;

                    double ll = _quantilePriorsTrue[i].Distribution.LogPDF(diff);
                    result.Add(new PriorComponent($"Quantile Prior: p={_quantilePriorsTrue[i].Alpha:G4} (diff)", ll, PriorComponentType.QuantilePrior));
                }

                // Jacobian determinant for transformation from parameters to quantiles
                ((IStandardError)model.Distributions[0]).QuantileJacobian(pVals, out var D);
                double jacobianLL = D != 0 ? Math.Log(Math.Abs(D)) : double.NegativeInfinity;
                result.Add(new PriorComponent("Quantile Jacobian", jacobianLL, PriorComponentType.Jacobian));
            }

            return result;
        }

        /// <inheritdoc/>
        public override void SetParameterValues(IList<double> parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));

            if (parameters.Count != NumberOfParameters)
                throw new ArgumentException("The length of the parameter list is incorrect.", nameof(parameters));

            for (int i = 0; i < parameters.Count; i++)
                Parameters[i].Value = parameters[i];

            // Set parameters
            if (!IsSeasonal)
            {
                Distribution!.SetParameters(parameters);
            }
            else
            {
                // Seasonal case: convert GEV parameters so that each season
                // has the correct marginal exceedance behavior.
                if (!TryGetEffectiveChangePoints(parameters, out int k1, out int k2))
                    throw new ArgumentOutOfRangeException(nameof(parameters), "Floored seasonal change points must satisfy 1 <= K1 < K2 <= 366.");

                double xi1 = parameters[2];
                double alpha1 = parameters[3];
                double kappa1 = parameters[4];

                double xi2 = parameters[5];
                double alpha2 = parameters[6];
                double kappa2 = parameters[7];

                // Correction factors
                double p1 = (k1 + (366.0 - k2)) / 366.0;
                double p2 = (k2 - k1) / 366.0;

                // Season 1 - use Gumbel limit when kappa is near zero
                double xiHat1, alphaHat1;
                if (Math.Abs(kappa1) < 1E-4)
                {
                    // Gumbel limit of xi + (alpha / kappa) * (1 - p^-kappa): xi_hat = xi + alpha * log(p), alpha_hat = alpha
                    xiHat1 = xi1 + alpha1 * Math.Log(p1);
                    alphaHat1 = alpha1;
                }
                else
                {
                    double powTerm1 = Math.Pow(p1, -kappa1);
                    // Guard against overflow
                    if (double.IsInfinity(powTerm1) || double.IsNaN(powTerm1))
                    {
                        xiHat1 = xi1;
                        alphaHat1 = alpha1;
                    }
                    else
                    {
                        xiHat1 = xi1 + (alpha1 / kappa1) * (1 - powTerm1);
                        alphaHat1 = alpha1 * powTerm1;
                    }
                }

                // Season 2 - use Gumbel limit when kappa is near zero
                double xiHat2, alphaHat2;
                if (Math.Abs(kappa2) < 1E-4)
                {
                    // Gumbel limit of xi + (alpha / kappa) * (1 - p^-kappa): xi_hat = xi + alpha * log(p), alpha_hat = alpha
                    xiHat2 = xi2 + alpha2 * Math.Log(p2);
                    alphaHat2 = alpha2;
                }
                else
                {
                    double powTerm2 = Math.Pow(p2, -kappa2);
                    // Guard against overflow
                    if (double.IsInfinity(powTerm2) || double.IsNaN(powTerm2))
                    {
                        xiHat2 = xi2;
                        alphaHat2 = alpha2;
                    }
                    else
                    {
                        xiHat2 = xi2 + (alpha2 / kappa2) * (1 - powTerm2);
                        alphaHat2 = alpha2 * powTerm2;
                    }
                }

                Distribution!.SetParameters(new double[] {xiHat1, alphaHat1, kappa1, xiHat2, alphaHat2, kappa2});
            }
        }

        /// <summary>
        /// Gets the distribution on which quantile priors are evaluated.
        /// </summary>
        /// <param name="model">The competing-risks model holding the raw component parameters.</param>
        /// <param name="parameters">The full parameter vector, including any seasonal change points.</param>
        /// <returns>
        /// The raw model when nonseasonal; the exposure-annualized distribution when seasonal, so
        /// the prior constrains the same annual quantile that the likelihood and the fitted curve
        /// report; or <c>null</c> when the seasonal change points are invalid.
        /// </returns>
        private CompetingRisks? GetQuantilePriorDistribution(CompetingRisks model, double[] parameters)
        {
            if (!IsSeasonal)
                return model;

            return TryGetEffectiveChangePoints(parameters, out _, out _) ? GetDistribution(parameters) : null;
        }

        /// <summary>
        /// Returns a clone of the competing risks distribution with the
        /// specified parameter values applied (accounting for seasonal
        /// transformations).
        /// </summary>
        /// <param name="parameters">Full parameter vector, including any seasonal parameters.</param>
        /// <returns>A cloned <see cref="CompetingRisks"/> instance with parameters set.</returns>
        public CompetingRisks GetDistribution(IList<double> parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));

            var model = (CompetingRisks)Distribution!.Clone();

            // Set parameters
            if (!IsSeasonal)
            {
                model.SetParameters(parameters);
            }
            else
            {
                // Seasonal case: convert GEV parameters so that each season
                // has the correct marginal exceedance behavior.
                if (!TryGetEffectiveChangePoints(parameters, out int k1, out int k2))
                    throw new ArgumentOutOfRangeException(nameof(parameters), "Floored seasonal change points must satisfy 1 <= K1 < K2 <= 366.");

                double xi1 = parameters[2];
                double alpha1 = parameters[3];
                double kappa1 = parameters[4];

                double xi2 = parameters[5];
                double alpha2 = parameters[6];
                double kappa2 = parameters[7];

                // Correction factors
                double p1 = (k1 + (366.0 - k2)) / 366.0;
                double p2 = (k2 - k1) / 366.0;

                // Season 1 - use Gumbel limit when kappa is near zero
                double xiHat1, alphaHat1;
                if (Math.Abs(kappa1) < 1E-4)
                {
                    // Gumbel limit of xi + (alpha / kappa) * (1 - p^-kappa).
                    xiHat1 = xi1 + alpha1 * Math.Log(p1);
                    alphaHat1 = alpha1;
                }
                else
                {
                    double powTerm1 = Math.Pow(p1, -kappa1);
                    if (double.IsInfinity(powTerm1) || double.IsNaN(powTerm1))
                    {
                        xiHat1 = xi1;
                        alphaHat1 = alpha1;
                    }
                    else
                    {
                        xiHat1 = xi1 + (alpha1 / kappa1) * (1 - powTerm1);
                        alphaHat1 = alpha1 * powTerm1;
                    }
                }

                // Season 2 - use Gumbel limit when kappa is near zero
                double xiHat2, alphaHat2;
                if (Math.Abs(kappa2) < 1E-4)
                {
                    // Gumbel limit of xi + (alpha / kappa) * (1 - p^-kappa).
                    xiHat2 = xi2 + alpha2 * Math.Log(p2);
                    alphaHat2 = alpha2;
                }
                else
                {
                    double powTerm2 = Math.Pow(p2, -kappa2);
                    if (double.IsInfinity(powTerm2) || double.IsNaN(powTerm2))
                    {
                        xiHat2 = xi2;
                        alphaHat2 = alpha2;
                    }
                    else
                    {
                        xiHat2 = xi2 + (alpha2 / kappa2) * (1 - powTerm2);
                        alphaHat2 = alpha2 * powTerm2;
                    }
                }

                model.SetParameters(new double[] { xiHat1, alphaHat1, kappa1, xiHat2, alphaHat2, kappa2 });

            }
            return model;
        }

        /// <inheritdoc/>
        public override IModel Clone()
        {
            var parms = new List<ModelParameter>();
            for (int i = 0; i < NumberOfParameters; i++)
                parms.Add(Parameters[i].Clone());

            var quants = new List<QuantilePrior>();
            for (int i = 0; i < QuantilePriors.Count; i++)
                quants.Add(QuantilePriors[i].Clone());

            var result = new PointProcessModel(DataFrame, Distribution!)
            {
                _threshold = Threshold,
                _totalYears = TotalYears,
                _totalYearsExplicit = _totalYearsExplicit,
                _isTotalYearsInferred = IsTotalYearsInferred,
                _useDefaults = UseDefaults,
                _isSeasonal = IsSeasonal,
                _timeBlock = TimeBlock,
                _startMonth = StartMonth,
                _useDefaultFlatPriors = UseDefaultFlatPriors,
                _useJeffreysRuleForScale = UseJeffreysRuleForScale,
                _enableQuantilePriors = EnableQuantilePriors,
                _useSingleQuantile = UseSingleQuantile,
                Parameters = parms,
                QuantilePriors = quants,
            };
            result.SetAMSData();
            result.CalculateLambda();
            result.ProcessQuantilePriors();
            return result;
        }

        /// <inheritdoc/>
        public override XElement ToXElement()
        {
            var result = new XElement(nameof(PointProcessModel));

            // Distribution
            result.Add(Distribution!.ToXElement());

            // Inputs
            result.SetAttributeValue(nameof(Threshold), Threshold.ToString("G17", CultureInfo.InvariantCulture));
            result.SetAttributeValue(nameof(TotalYears), TotalYears.ToString("G17", CultureInfo.InvariantCulture));
            result.SetAttributeValue(nameof(IsTotalYearsInferred), IsTotalYearsInferred.ToString());
            result.SetAttributeValue(nameof(UseDefaults), UseDefaults.ToString());
            result.SetAttributeValue(nameof(IsSeasonal), IsSeasonal.ToString());
            result.SetAttributeValue(nameof(TimeBlock), TimeBlock.ToString());
            result.SetAttributeValue(nameof(StartMonth), StartMonth.ToString(CultureInfo.InvariantCulture));

            // Parameters
            var parms = new XElement(nameof(Parameters));
            foreach (var p in Parameters)
                parms.Add(p.ToXElement());

            result.Add(parms);
            result.SetAttributeValue(nameof(UseDefaultFlatPriors), UseDefaultFlatPriors.ToString());
            result.SetAttributeValue(nameof(UseJeffreysRuleForScale), UseJeffreysRuleForScale.ToString());

            // Quantiles
            var quants = new XElement(nameof(QuantilePriors));
            foreach (var q in QuantilePriors)
                quants.Add(q.ToXElement());

            result.Add(quants);
            result.SetAttributeValue(nameof(EnableQuantilePriors), EnableQuantilePriors.ToString());
            result.SetAttributeValue(nameof(UseSingleQuantile), UseSingleQuantile.ToString());

            return result;
        }

        /// <inheritdoc/>
        public override (bool IsValid, List<string> ValidationMessages) Validate()
        {
            bool isValid = true;
            var messages = new List<string>();

            // Data frame checks
            if (DataFrame == null)
            {
                isValid = false;
                messages.Add("Error: Data frame is null.");
                return (isValid, messages);
            }

            // Validate data frame
            var dataValid = DataFrame.Validate();
            if (!dataValid.IsValid)
            {
                isValid = false;
                messages.AddRange(dataValid.ValidationMessages);
            }

            // Distribution checks
            if (Distribution is null)
            {
                isValid = false;
                messages.Add("Error: Competing risks distribution is null.");
            }
            else if (Distribution.Distributions is null || !Distribution.Distributions.Any())
            {
                isValid = false;
                messages.Add("Error: Competing risks distribution has no component distributions.");
            }

            // Validate uncertain-data ME bounds before likelihood evaluation. The point-process
            // magnitude likelihood normalizes uncertain integrals by retained 1E-8 probability mass.
            if (DataFrame.UncertainSeries is not null)
            {
                foreach (UncertainData data in DataFrame.UncertainSeries)
                {
                    var dist = data.Distribution;
                    double lowerProbability = 1E-8;
                    double upperProbability = 1.0 - 1E-8;
                    // Validate the retained quantile window directly; endpoints at 0/1
                    // are infinite for common ME distributions such as Normal.
                    double lower = dist.InverseCDF(lowerProbability);
                    double upper = dist.InverseCDF(upperProbability);
                    double mass = upperProbability - lowerProbability;

                    if (!Tools.IsFinite(lower) || !Tools.IsFinite(upper) || !Tools.IsFinite(mass) ||
                        mass <= 0.0 || lower >= upper)
                    {
                        isValid = false;
                        messages.Add($"Error: Uncertain data at index {data.Index} has invalid measurement-error integration bounds at the 1E-8 probability window.");
                    }
                }
            }

            // Component distribution type check (must all be GEV)
            if (Distribution is not null && Distribution.Distributions is not null)
            {
                for (int i = 0; i < Distribution.Distributions.Count; i++)
                {
                    if (!IsSupportedDistributionType(Distribution.Distributions[i].Type))
                    {
                        isValid = false;
                        messages.Add($"Error: Component distribution {i + 1} has type '{Distribution.Distributions[i].Type}' but the point process model requires Generalized Extreme Value (GEV) distributions.");
                    }
                }
            }

            // Threshold / years / lambda
            if (double.IsNaN(Threshold) || !Tools.IsFinite(Threshold))
            {
                isValid = false;
                messages.Add("Error: Threshold is not set or is not finite.");
            }

            if (double.IsNaN(TotalYears) || !Tools.IsFinite(TotalYears) || TotalYears <= 0)
            {
                isValid = false;
                messages.Add("Error: TotalYears must be positive and finite.");
            }
            else if (IsTotalYearsInferred)
            {
                messages.Add("Warning: TotalYears was inferred from the first and last retained exact POT events. Leading or trailing zero-event years are not observable from the POT data; override TotalYears when source-record coverage is known.");
            }

            if (double.IsNaN(Lambda) || !Tools.IsFinite(Lambda) || Lambda <= 0)
            {
                isValid = false;
                messages.Add("Error: Lambda (average events per year) must be positive and finite.");
            }

            double fittedIntensity = FittedThresholdIntensity;
            if (!Tools.IsFinite(fittedIntensity) || fittedIntensity <= 0.0)
            {
                isValid = false;
                messages.Add("Error: The fitted threshold intensity must be positive and finite.");
            }

            // Seasonal-specific checks
            if (IsSeasonal)
            {
                int[] undatedIndexes = DataFrame.ExactSeries
                    .Cast<ExactData>()
                    .Where(data => data.DateTime == default)
                    .Select(data => data.Index)
                    .ToArray();
                if (undatedIndexes.Length > 0)
                {
                    isValid = false;
                    messages.Add($"Error: Every exact observation requires a valid date for seasonal point-process fitting. Missing dates at indexes: {string.Join(", ", undatedIndexes)}.");
                }

                if (StartMonth < 1 || StartMonth > 12)
                {
                    isValid = false;
                    messages.Add("Error: StartMonth must be between 1 and 12 for seasonal models.");
                }

                if (undatedIndexes.Length == 0 &&
                    (AMSDataFrame is null || AMSDataFrame.ExactSeries.Count == 0))
                {
                    isValid = false;
                    messages.Add("Error: AMSDataFrame has no exact data for seasonal model.");
                }

                // Change points (if present)
                if (Parameters is not null && Parameters.Count >= 2 &&
                    Parameters[0].Name.StartsWith("Change Point", StringComparison.OrdinalIgnoreCase) &&
                    Parameters[1].Name.StartsWith("Change Point", StringComparison.OrdinalIgnoreCase))
                {
                    if (!TryGetEffectiveChangePoints(Parameters.Select(parameter => parameter.Value).ToArray(), out _, out _))
                    {
                        isValid = false;
                        messages.Add("Error: Floored seasonal change points K1 and K2 must satisfy 1 <= K1 < K2 <= 366.");
                    }
                }
            }

            // Parameter priors
            if (Parameters is not null)
            {
                for (int i = 0; i < Parameters.Count; i++)
                {
                    var valid = Parameters[i].Validate();
                    if (!valid.IsValid)
                    {
                        isValid = false;
                        messages.AddRange(valid.ValidationMessages);
                    }
                }
            }

            // Quantile priors
            for (int i = 0; i < QuantilePriors.Count; i++)
            {
                var valid = QuantilePriors[i].Validate();
                if (!valid.IsValid)
                {
                    isValid = false;
                    messages.AddRange(valid.ValidationMessages);
                }

                if (i >= 1)
                {
                    if (QuantilePriors[i].Alpha >= QuantilePriors[i - 1].Alpha)
                    {
                        isValid = false;
                        messages.Add("Error: Quantile priors must have strictly decreasing exceedance probabilities (Alpha).");
                    }

                    if (QuantilePriors[i].Distribution.Mean <= QuantilePriors[i - 1].Distribution.Mean)
                    {
                        isValid = false;
                        messages.Add("Error: Quantile prior means must increase with decreasing exceedance probability.");
                    }

                    if (QuantilePriors[i].Distribution.InverseCDF(0.05) <= QuantilePriors[i - 1].Distribution.InverseCDF(0.05) ||
                        QuantilePriors[i].Distribution.InverseCDF(0.95) <= QuantilePriors[i - 1].Distribution.InverseCDF(0.95))
                    {
                        isValid = false;
                        messages.Add("Error: Quantile prior 5 percent and 95 percent bounds must increase with decreasing exceedance probability.");
                    }
                }
            }

            return (isValid, messages);
        }

        /// <inheritdoc/>
        /// <remarks>
        /// <para>
        /// Generates a fixed-size POT magnitude sample from the Poisson-GPA process implied by the
        /// configured parameters. A nonseasonal process uses the empirical arrival rate
        /// <see cref="Lambda"/>; a seasonal process uses the fitted threshold intensity of each
        /// season, so its annual rate is the exposure-weighted sum of the two intensities and each
        /// event is assigned to a season in proportion to that season's exposure-weighted
        /// intensity. Yearly event counts are drawn from a Numerics Poisson distribution until the
        /// requested number of exceedances has been accumulated, and each event is marked from the
        /// Madsen-equivalent generalized Pareto distribution of its season.
        /// </para>
        /// </remarks>
        public double[] GenerateRandomValues(int sampleSize, int seed = -1)
        {
            if (sampleSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(sampleSize), "Sample size must be positive.");

            if (!IsSeasonal)
                return GenerateNonSeasonalRandomValues(sampleSize, seed);

            return GenerateFixedPoissonGpaEvents(sampleSize, seed)
                .Select(point => point.magnitude)
                .ToArray();
        }

        /// <summary>
        /// Generates a fixed-size nonseasonal POT sample from the Poisson-GPA process.
        /// </summary>
        /// <param name="sampleSize">The number of exceedance magnitudes to return.</param>
        /// <param name="seed">The pseudorandom number generator seed.</param>
        /// <returns>An array containing exactly <paramref name="sampleSize"/> POT magnitudes.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the empirical arrival rate, threshold, GEV parameters, or converted GPA
        /// parameters do not define a valid nonseasonal point process.
        /// </exception>
        /// <remarks>
        /// The Madsen conversion preserves the Hosking shape parameter used by Numerics:
        /// <c>Alpha_GPA = Alpha_GEV * Lambda^Kappa</c>, <c>Xi_GPA = Threshold</c>, and
        /// <c>Kappa_GPA = Kappa_GEV</c>. The Poisson count and GPA marks share one seeded
        /// Mersenne-Twister stream.
        /// </remarks>
        private double[] GenerateNonSeasonalRandomValues(int sampleSize, int seed)
        {
            GeneralizedPareto gpa = CreatePoissonGpaComponents(out _, out _, out _, out _)[0];
            var poisson = new Poisson(Lambda);
            var rng = seed > 0
                ? new Numerics.Sampling.MersenneTwister(seed)
                : new Numerics.Sampling.MersenneTwister();
            var result = new double[sampleSize];
            int generated = 0;

            while (generated < sampleSize)
            {
                int yearlyEventCount = checked((int)poisson.InverseCDF(rng.NextDouble()));
                int eventsToRetain = Math.Min(yearlyEventCount, sampleSize - generated);
                for (int i = 0; i < eventsToRetain; i++)
                {
                    result[generated] = gpa.InverseCDF(rng.NextDouble());
                    generated++;
                }
            }

            return result;
        }

        /// <summary>
        /// Generates a fixed-size, date-stamped POT sample from the Poisson-GPA process.
        /// </summary>
        /// <param name="sampleSize">The exact number of POT exceedances to return.</param>
        /// <param name="seed">The pseudorandom number generator seed.</param>
        /// <returns>A daily-interval series containing dummy dates and POT magnitudes.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="sampleSize"/> is not positive.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the arrival rate, changepoints, or Madsen-converted GPA parameters are
        /// invalid.
        /// </exception>
        /// <remarks>
        /// Yearly counts are sampled from <c>Poisson(Lambda)</c> until the requested POT sample is
        /// complete. Seasonal membership is sampled using only the changepoint exposure weights.
        /// Dates use successive leap-containing dummy blocks beginning with calendar year 2000 or
        /// the corresponding configured water/custom-year block. Multiple exceedances may share
        /// a date.
        /// </remarks>
        public TimeSeries GeneratePOTTimeSeries(int sampleSize, int seed = -1)
        {
            if (sampleSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(sampleSize), "Sample size must be positive.");

            List<(DateTime date, double magnitude)> events = GenerateFixedPoissonGpaEvents(sampleSize, seed);
            events.Sort((left, right) => left.date.CompareTo(right.date));
            var result = new TimeSeries(TimeInterval.OneDay);
            foreach ((DateTime date, double magnitude) in events)
            {
                result.Add(new SeriesOrdinate<DateTime, double>(date, magnitude));
            }

            return result;
        }

        /// <summary>
        /// Generates a synthetic Peaks-Over-Threshold (POT) time series from the fitted
        /// Poisson-GPA process over a requested exposure duration.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Unlike <see cref="GenerateRandomValues(int, int)"/> — which returns a fixed-size
        /// sample containing only exceedance magnitudes — this method
        /// returns a date-stamped <see cref="TimeSeries"/> that respects the model's
        /// seasonal structure (when <see cref="IsSeasonal"/> is true).
        /// </para>
        /// <para>
        /// <b>Water year vs. calendar year:</b> generated and observed events use the same
        /// elapsed-day calculation from the configured block start. The total event count has mean
        /// <c>durationYears * Lambda</c> for a nonseasonal process and
        /// <c>durationYears * (w_1 Lambda_1 + w_2 Lambda_2)</c> for a seasonal process, where each
        /// <c>Lambda_j</c> is the fitted threshold intensity of season <c>j</c>. Seasonal
        /// membership is assigned by Poisson thinning with the exposure-weighted intensities, and
        /// generated dates are restricted to the matching season.
        /// </para>
        /// </remarks>
        /// <param name="startDate">First date in the synthetic record.</param>
        /// <param name="durationYears">Length of the synthetic record in years.</param>
        /// <param name="seed">Master PRNG seed; pass -1 for non-deterministic.</param>
        /// <returns>A daily-interval <see cref="TimeSeries"/> of (date, magnitude)
        /// pairs, one per simulated exceedance, sorted by date.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when
        /// <paramref name="durationYears"/> is non-positive.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the arrival rate, changepoints, or Madsen-converted GPA parameters are invalid.
        /// </exception>
        public TimeSeries GeneratePOTTimeSeries(DateTime startDate, double durationYears, int seed = -1)
        {
            if (durationYears <= 0)
                throw new ArgumentOutOfRangeException(nameof(durationYears), "Duration must be positive.");
            GeneralizedPareto[] components = CreatePoissonGpaComponents(
                out int k1,
                out int k2,
                out double seasonOneSelectionProbability,
                out double annualEventRate);

            double totalDays = durationYears * 365.25;
            if (!Tools.IsFinite(totalDays) || totalDays <= 0.0)
                throw new ArgumentOutOfRangeException(nameof(durationYears), "Duration must produce a positive finite date span.");

            DateTime endDate = startDate.AddDays(totalDays);
            var rng = seed > 0 ? new Numerics.Sampling.MersenneTwister(seed) : new Numerics.Sampling.MersenneTwister();

            int totalCount = SamplePoisson(durationYears * annualEventRate, rng);
            var events = new List<(DateTime date, double magnitude)>(totalCount);
            for (int i = 0; i < totalCount; i++)
            {
                int componentIndex = IsSeasonal && rng.NextDouble() >= seasonOneSelectionProbability ? 1 : 0;
                DateTime eventDate = SampleEventDate(startDate, endDate, componentIndex, k1, k2, rng);
                double magnitude = components[componentIndex].InverseCDF(rng.NextDouble());
                events.Add((eventDate, magnitude));
            }

            events.Sort((a, b) => a.date.CompareTo(b.date));
            var result = new TimeSeries(TimeInterval.OneDay);
            foreach (var (date, magnitude) in events)
            {
                result.Add(new SeriesOrdinate<DateTime, double>(date, magnitude));
            }

            return result;
        }

        /// <summary>
        /// Determines whether the data frame retains a valid point-process source exposure.
        /// </summary>
        /// <returns><c>true</c> when source observation years are positive and finite.</returns>
        private bool HasStoredSourceExposure()
        {
            return DataFrame is not null &&
                   Tools.IsFinite(DataFrame.PointProcessObservationYears) &&
                   DataFrame.PointProcessObservationYears > 0.0;
        }

        /// <summary>
        /// Floors the two latent seasonal changepoints and validates their ordered day cells.
        /// </summary>
        /// <param name="parameters">The full point-process parameter vector.</param>
        /// <param name="k1">The effective first integer changepoint day.</param>
        /// <param name="k2">The effective second integer changepoint day.</param>
        /// <returns><c>true</c> when the floored days satisfy 1 &lt;= K1 &lt; K2 &lt;= 366.</returns>
        /// <remarks>
        /// Continuous MCMC proposals are mapped to equal-width discrete day cells. Default supports
        /// are either broad or histogram-informed; custom priors can narrow them further.
        /// </remarks>
        private static bool TryGetEffectiveChangePoints(IList<double> parameters, out int k1, out int k2)
        {
            k1 = 0;
            k2 = 0;
            if (parameters.Count < 2 || !Tools.IsFinite(parameters[0]) || !Tools.IsFinite(parameters[1]))
                return false;

            double flooredK1 = Math.Floor(parameters[0]);
            double flooredK2 = Math.Floor(parameters[1]);
            if (flooredK1 < 1.0 || flooredK1 > 365.0 || flooredK2 < 2.0 || flooredK2 > 366.0)
                return false;

            k1 = (int)flooredK1;
            k2 = (int)flooredK2;
            return k1 < k2;
        }

        /// <summary>
        /// Computes the one-based day within the selected calendar or water-year block.
        /// </summary>
        /// <param name="date">The event date.</param>
        /// <returns>The one-based elapsed day from the applicable block start.</returns>
        /// <remarks>
        /// Water-year and custom-year blocks use <see cref="StartMonth"/>; other block types use
        /// January 1. Elapsed-day arithmetic preserves leap days without shifting calendar months.
        /// </remarks>
        private int GetBlockDay(DateTime date)
        {
            bool shiftedYear = TimeBlock == TimeBlockWindow.WaterYear || TimeBlock == TimeBlockWindow.CustomYear;
            int startMonth = shiftedYear ? StartMonth : 1;
            if (startMonth < 1 || startMonth > 12)
                return date.DayOfYear;

            int startYear = date.Month >= startMonth ? date.Year : date.Year - 1;
            var blockStart = new DateTime(startYear, startMonth, 1, 0, 0, 0, date.Kind);
            return (date.Date - blockStart.Date).Days + 1;
        }

        /// <summary>
        /// Determines which wrapped season contains an effective block day.
        /// </summary>
        /// <param name="day">The one-based block day.</param>
        /// <param name="k1">The first effective changepoint.</param>
        /// <param name="k2">The second effective changepoint.</param>
        /// <returns><c>true</c> for wrapped season one; otherwise, <c>false</c>.</returns>
        private static bool IsSeasonOneDay(int day, int k1, int k2)
        {
            return day < k1 || day >= k2;
        }

        /// <summary>
        /// Evaluates the GEV-compatible point-process threshold measure.
        /// </summary>
        /// <param name="distribution">The GEV component.</param>
        /// <param name="threshold">The POT threshold.</param>
        /// <returns>The fitted threshold intensity, or <see cref="double.NaN"/> outside support.</returns>
        /// <remarks>
        /// Numerics stores Kappa with the opposite sign from the Coles shape parameter. The
        /// zero-shape branch is the analytical Gumbel limit.
        /// </remarks>
        private static double CalculateThresholdIntensity(GeneralizedExtremeValue distribution, double threshold)
        {
            double location = distribution.Xi;
            double scale = distribution.Alpha;
            double shape = -distribution.Kappa;
            if (!Tools.IsFinite(threshold) || !Tools.IsFinite(location) || !Tools.IsFinite(scale) ||
                !Tools.IsFinite(shape) || scale <= 0.0)
            {
                return double.NaN;
            }

            if (Math.Abs(shape) < 1E-4)
                return Math.Exp(-(threshold - location) / scale);

            double support = 1.0 + shape * ((threshold - location) / scale);
            return support > 0.0 ? Math.Pow(support, -1.0 / shape) : double.NaN;
        }

        /// <summary>
        /// Computes current fitted total and component threshold intensities for read-only APIs.
        /// </summary>
        /// <param name="totalIntensity">The exposure-weighted total annual intensity.</param>
        /// <param name="seasonOneIntensity">The first unweighted component intensity.</param>
        /// <param name="seasonTwoIntensity">The second unweighted component intensity.</param>
        /// <param name="seasonOneWeight">The first component exposure weight.</param>
        /// <param name="seasonTwoWeight">The second component exposure weight.</param>
        /// <returns><c>true</c> when the fitted state is valid.</returns>
        private bool TryGetFittedIntensities(
            out double totalIntensity,
            out double seasonOneIntensity,
            out double seasonTwoIntensity,
            out double seasonOneWeight,
            out double seasonTwoWeight)
        {
            totalIntensity = double.NaN;
            seasonOneIntensity = double.NaN;
            seasonTwoIntensity = 0.0;
            seasonOneWeight = 1.0;
            seasonTwoWeight = 0.0;

            if (Distribution is null || Parameters is null)
                return false;

            int requiredComponents = IsSeasonal ? 2 : 1;
            var process = (CompetingRisks)Distribution.Clone();
            if (process.Distributions.Count != requiredComponents)
                return false;

            double[] parameters = Parameters.Select(parameter => parameter.Value).ToArray();
            if (IsSeasonal)
            {
                if (parameters.Length != process.NumberOfParameters + 2 ||
                    !TryGetEffectiveChangePoints(parameters, out int k1, out int k2))
                {
                    return false;
                }

                seasonOneWeight = (k1 + 366.0 - k2) / 366.0;
                seasonTwoWeight = (k2 - k1) / 366.0;
                process.SetParameters(parameters.Skip(2).ToArray());
            }
            else
            {
                if (parameters.Length != process.NumberOfParameters)
                    return false;
                process.SetParameters(parameters);
            }

            if (process.Distributions[0] is not GeneralizedExtremeValue first)
                return false;
            seasonOneIntensity = CalculateThresholdIntensity(first, Threshold);
            if (!Tools.IsFinite(seasonOneIntensity) || seasonOneIntensity < 0.0)
                return false;

            if (IsSeasonal)
            {
                if (process.Distributions[1] is not GeneralizedExtremeValue second)
                    return false;
                seasonTwoIntensity = CalculateThresholdIntensity(second, Threshold);
                if (!Tools.IsFinite(seasonTwoIntensity) || seasonTwoIntensity < 0.0)
                    return false;
            }

            totalIntensity = seasonOneWeight * seasonOneIntensity + seasonTwoWeight * seasonTwoIntensity;
            return seasonOneWeight > 0.0 && seasonTwoWeight >= 0.0 &&
                   Tools.IsFinite(totalIntensity) && totalIntensity >= 0.0;
        }

        /// <summary>
        /// Generates a fixed-size marked Poisson sample using the configured GPA components.
        /// </summary>
        /// <param name="sampleSize">The exact number of exceedances to generate.</param>
        /// <param name="seed">The pseudorandom number generator seed.</param>
        /// <returns>Dummy dates and magnitudes in generation order.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the configured point process cannot be converted to valid GPA components.
        /// </exception>
        /// <remarks>
        /// The annual event count is <c>Poisson(Lambda)</c> for a nonseasonal process and
        /// <c>Poisson(w_1 Lambda_1 + w_2 Lambda_2)</c> for a seasonal process, where each
        /// <c>Lambda_j</c> is the fitted threshold intensity of season <c>j</c>; the seasonal
        /// categorical draw uses the rate-weighted season probability. Successive leap-containing
        /// dummy blocks retain the annual batches while making all 366 modeled block days
        /// representable as <see cref="DateTime"/> values.
        /// </remarks>
        private List<(DateTime date, double magnitude)> GenerateFixedPoissonGpaEvents(int sampleSize, int seed)
        {
            GeneralizedPareto[] components = CreatePoissonGpaComponents(
                out int k1,
                out int k2,
                out double seasonOneSelectionProbability,
                out double annualEventRate);
            var poisson = new Poisson(annualEventRate);
            var rng = seed > 0
                ? new Numerics.Sampling.MersenneTwister(seed)
                : new Numerics.Sampling.MersenneTwister();

            bool shiftedYear = TimeBlock == TimeBlockWindow.WaterYear || TimeBlock == TimeBlockWindow.CustomYear;
            int startMonth = shiftedYear ? StartMonth : 1;
            if (startMonth < 1 || startMonth > 12)
                throw new InvalidOperationException("The point-process block start month must be between 1 and 12.");

            var events = new List<(DateTime date, double magnitude)>(sampleSize);
            int blockIndex = 0;
            while (events.Count < sampleSize)
            {
                if (blockIndex > 1999)
                    throw new InvalidOperationException("The requested POT sample exceeds the available dummy DateTime blocks.");
                int dummyLeapYear = 2000 + 4 * blockIndex;
                int startYear = startMonth <= 2 ? dummyLeapYear : dummyLeapYear - 1;
                DateTime blockStart = new DateTime(startYear, startMonth, 1);

                int yearlyEventCount = checked((int)poisson.InverseCDF(rng.NextDouble()));
                int eventsToRetain = Math.Min(yearlyEventCount, sampleSize - events.Count);
                for (int i = 0; i < eventsToRetain; i++)
                {
                    int componentIndex = IsSeasonal && rng.NextDouble() >= seasonOneSelectionProbability ? 1 : 0;
                    int day;
                    if (!IsSeasonal)
                    {
                        day = 1 + Math.Min((int)(rng.NextDouble() * 366.0), 365);
                    }
                    else if (componentIndex == 0)
                    {
                        int firstSegmentDays = k1 - 1;
                        int seasonOneDays = k1 + 366 - k2;
                        int seasonIndex = Math.Min((int)(rng.NextDouble() * seasonOneDays), seasonOneDays - 1);
                        day = seasonIndex < firstSegmentDays
                            ? seasonIndex + 1
                            : k2 + seasonIndex - firstSegmentDays;
                    }
                    else
                    {
                        int seasonTwoDays = k2 - k1;
                        day = k1 + Math.Min((int)(rng.NextDouble() * seasonTwoDays), seasonTwoDays - 1);
                    }

                    DateTime date = blockStart.AddDays(day - 1);
                    double magnitude = components[componentIndex].InverseCDF(rng.NextDouble());
                    events.Add((date, magnitude));
                }
                blockIndex++;
            }

            return events;
        }

        /// <summary>
        /// Converts the configured raw Hosking GEV parameters to Poisson-GPA simulation components.
        /// </summary>
        /// <param name="k1">The floored first changepoint, or zero when nonseasonal.</param>
        /// <param name="k2">The floored second changepoint, or zero when nonseasonal.</param>
        /// <param name="seasonOneSelectionProbability">
        /// The probability that a simulated event belongs to the wrapped first season; one when nonseasonal.
        /// </param>
        /// <param name="annualEventRate">The annual Poisson exceedance rate of the simulated process.</param>
        /// <returns>One nonseasonal GPA or two seasonal GPA components.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the empirical rate, threshold intensities, parameters, or converted GPA
        /// components are invalid.
        /// </exception>
        /// <remarks>
        /// Madsen conversion under the Numerics/Hosking sign convention is
        /// <c>Alpha_GPA = Alpha_GEV * Lambda^Kappa</c>, <c>Xi_GPA = Threshold</c>, and
        /// <c>Kappa_GPA = Kappa_GEV</c>, where <c>Lambda</c> is the annual exceedance rate of the
        /// process described by the component. The nonseasonal process uses the empirical rate
        /// <see cref="Lambda"/>. Each seasonal component uses its fitted threshold intensity
        /// <c>Lambda_j(u)</c>, the rate it would produce over a full year, so the simulated
        /// season-<c>j</c> annual maximum matches the exposure-annualized seasonal distribution
        /// <c>G_j^{w_j}</c> of the likelihood: the annual rate is
        /// <c>w_1 Lambda_1 + w_2 Lambda_2</c> and each event belongs to season one with probability
        /// <c>w_1 Lambda_1 / (w_1 Lambda_1 + w_2 Lambda_2)</c>.
        /// </remarks>
        private GeneralizedPareto[] CreatePoissonGpaComponents(
            out int k1,
            out int k2,
            out double seasonOneSelectionProbability,
            out double annualEventRate)
        {
            if (!Tools.IsFinite(Lambda) || Lambda <= 0.0)
                throw new InvalidOperationException("The empirical point-process arrival rate must be positive and finite.");
            if (!Tools.IsFinite(Threshold))
                throw new InvalidOperationException("The point-process threshold must be finite.");
            if (Distribution is null || Parameters is null)
                throw new InvalidOperationException("The point-process distribution and parameters must be configured.");

            k1 = 0;
            k2 = 0;
            seasonOneSelectionProbability = 1.0;
            annualEventRate = Lambda;
            int componentCount = IsSeasonal ? 2 : 1;
            int expectedParameterCount = IsSeasonal ? 8 : 3;
            if (Parameters.Count != expectedParameterCount || Distribution.Distributions.Count != componentCount)
                throw new InvalidOperationException("The point-process component count does not match its parameter vector.");

            double seasonOneWeight = 1.0;
            double seasonTwoWeight = 0.0;
            if (IsSeasonal)
            {
                double[] values = Parameters.Select(parameter => parameter.Value).ToArray();
                if (!TryGetEffectiveChangePoints(values, out k1, out k2))
                    throw new InvalidOperationException("Floored seasonal changepoints must satisfy 1 <= K1 < K2 <= 366.");
                seasonOneWeight = (k1 + 366.0 - k2) / 366.0;
                seasonTwoWeight = (k2 - k1) / 366.0;
            }

            var gevs = new GeneralizedExtremeValue[componentCount];
            var componentRates = new double[componentCount];
            for (int componentIndex = 0; componentIndex < componentCount; componentIndex++)
            {
                int parameterIndex = IsSeasonal ? 2 + 3 * componentIndex : 3 * componentIndex;
                if (Distribution.Distributions[componentIndex] is not GeneralizedExtremeValue)
                    throw new InvalidOperationException("Each point-process component must be a generalized extreme-value distribution.");

                var gev = new GeneralizedExtremeValue(
                    Parameters[parameterIndex].Value,
                    Parameters[parameterIndex + 1].Value,
                    Parameters[parameterIndex + 2].Value);
                if (!gev.ParametersValid)
                    throw new InvalidOperationException("The fitted generalized extreme-value parameters are invalid.");
                gevs[componentIndex] = gev;

                if (IsSeasonal)
                {
                    double intensity = CalculateThresholdIntensity(gev, Threshold);
                    if (!Tools.IsFinite(intensity) || intensity <= 0.0)
                        throw new InvalidOperationException("Each fitted seasonal threshold intensity must be positive and finite.");
                    componentRates[componentIndex] = intensity;
                }
                else
                {
                    componentRates[componentIndex] = Lambda;
                }
            }

            if (IsSeasonal)
            {
                double seasonOneRate = seasonOneWeight * componentRates[0];
                double seasonTwoRate = seasonTwoWeight * componentRates[1];
                annualEventRate = seasonOneRate + seasonTwoRate;
                if (!Tools.IsFinite(annualEventRate) || annualEventRate <= 0.0)
                    throw new InvalidOperationException("The fitted seasonal annual exceedance rate must be positive and finite.");
                seasonOneSelectionProbability = seasonOneRate / annualEventRate;
            }

            var components = new GeneralizedPareto[componentCount];
            for (int componentIndex = 0; componentIndex < componentCount; componentIndex++)
            {
                GeneralizedExtremeValue gev = gevs[componentIndex];
                double gpaScale = gev.Alpha * Math.Pow(componentRates[componentIndex], gev.Kappa);
                var gpa = new GeneralizedPareto(Threshold, gpaScale, gev.Kappa);
                if (!gpa.ParametersValid)
                    throw new InvalidOperationException("The Madsen-converted generalized Pareto parameters are invalid.");
                components[componentIndex] = gpa;
            }

            return components;
        }

        /// <summary>
        /// Samples a date uniformly from the requested component's portion of the record.
        /// </summary>
        /// <param name="startDate">The inclusive record start.</param>
        /// <param name="endDate">The exclusive record end.</param>
        /// <param name="componentIndex">The zero-based process component.</param>
        /// <param name="k1">The effective first changepoint.</param>
        /// <param name="k2">The effective second changepoint.</param>
        /// <param name="rng">The pseudorandom number generator.</param>
        /// <returns>A date whose block day belongs to the requested component.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the requested season has no representable date in the supplied record span.
        /// </exception>
        private DateTime SampleEventDate(
            DateTime startDate,
            DateTime endDate,
            int componentIndex,
            int k1,
            int k2,
            Numerics.Sampling.MersenneTwister rng)
        {
            double spanDays = (endDate - startDate).TotalDays;
            if (!IsSeasonal)
                return startDate.AddDays(rng.NextDouble() * spanDays);

            bool requireSeasonOne = componentIndex == 0;
            for (int attempt = 0; attempt < 1000000; attempt++)
            {
                DateTime candidate = startDate.AddDays(rng.NextDouble() * spanDays);
                if (IsSeasonOneDay(GetBlockDay(candidate), k1, k2) == requireSeasonOne)
                    return candidate;
            }

            throw new InvalidOperationException("The requested seasonal component has no representable date in the simulation record.");
        }

        /// <summary>
        /// Samples an integer event count from a Poisson distribution with the given mean.
        /// </summary>
        /// <param name="mean">The Poisson rate (event count expected).</param>
        /// <param name="rng">A Mersenne-Twister PRNG.</param>
        /// <returns>A non-negative integer event count.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the mean is non-finite, negative, or too large for an integer count.
        /// </exception>
        private static int SamplePoisson(double mean, Numerics.Sampling.MersenneTwister rng)
        {
            if (!Tools.IsFinite(mean) || mean < 0.0 || mean > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(mean), mean, "The Poisson mean must be finite, non-negative, and representable as an integer count.");
            if (mean == 0.0)
                return 0;

            double probability = Math.Clamp(rng.NextDouble(), 1E-12, 1.0 - 1E-12);
            return checked((int)new Poisson(mean).InverseCDF(probability));
        }

        #endregion


    }
}
