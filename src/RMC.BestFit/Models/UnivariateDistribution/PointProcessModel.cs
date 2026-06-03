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
    /// Seasonal point process (two GEVs with day-of-year based seasons).
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

        /// <summary>
        /// Tracks whether the user has explicitly set <see cref="TotalYears"/>.
        /// When <c>false</c>, <see cref="SetDefaultThresholdAndTotalYears"/> is
        /// allowed to overwrite the value with its data-derived heuristic. When
        /// the user changes <see cref="DataFrame"/>, this is reset to <c>false</c>
        /// so the heuristic for the new data is presented to them.
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

                // The user is supplying new data; the previously-explicit TotalYears
                // (if any) no longer reflects this record. Reset so the heuristic
                // for the new data is applied and re-presented in the GUI.
                _totalYearsExplicit = false;

                if (_dataFrame != null)
                {
                    _dataFrame.PropertyChanged += DataFrame_PropertyChanged;

                    _dataFrame.ProcessThresholdSeries();
                    SetAMSData();

                    if (UseDefaults)
                        SetDefaultThresholdAndTotalYears(forceTotalYears: true);

                    if (UseDefaultFlatPriors)
                        SetDefaultParameters();
                }

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
        /// Gets the day-of-year (calendar or water year) for each peaks-over-threshold event.
        /// </summary>
        public List<int> POTDays => _potDays;

        /// <summary>
        /// Gets the average number of POT events per year.
        /// </summary>
        public double Lambda => _lambda;

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
                if (!_totalYears.AlmostEquals(value))
                {
                    _totalYears = value;
                    _totalYearsExplicit = true;  // user (or caller) explicitly set the value
                    CalculateLambda();
                    RaisePropertyChange(nameof(TotalYears));
                }
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

            // TotalYears is the Poisson exposure duration for exact POT events only.
            // If exact events miss the first/last observed years, this is only a
            // heuristic; users can uncheck UseDefaults and enter the better exposure.
            bool totalYearsChanged = false;
            if (forceTotalYears || !_totalYearsExplicit)
            {
                double inferred = DataFrame.ExactSeries.Count > 0
                    ? DataFrame.ExactSeries.IndexSpan()
                    : 1.0;

                if (!_totalYears.AlmostEquals(inferred))
                {
                    _totalYears = inferred;
                    totalYearsChanged = true;
                }
                _totalYearsExplicit = false;
            }

            CalculateLambda();
            if (totalYearsChanged)
                RaisePropertyChange(nameof(TotalYears));
        }

        /// <summary>
        /// Calculates λ, the average number of POT events per year.
        /// </summary>
        public void CalculateLambda()
        {
            if (DataFrame == null || double.IsNaN(_totalYears) || _totalYears <= 0)
            {
                _lambda = double.NaN;
                return;
            }

            double events =
                DataFrame.ExactSeries.Count +
                DataFrame.UncertainSeries.Count +
                DataFrame.IntervalSeries.Count;

            _lambda = events / _totalYears;
        }

        /// <summary>
        /// Preprocesses the annual maximum (or block) data and, for seasonal
        /// models, computes the day-of-year for each POT event.
        /// </summary>
        public void SetAMSData()
        {
            _amsDataFrame = new DataFrame();
            _potDays = new List<int>();

            if (DataFrame == null || DataFrame.ExactSeries == null || DataFrame.ExactSeries.Count == 0)
            {
                return;
            }

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
                        var dt = exact.DateTime;

                        if (dt == default)
                        {
                            dt = new DateTime(exact.Index, 1, 1);
                        }

                        ts.Add(new SeriesOrdinate<DateTime, double>(dt, exact.Value));
                    }

                    // Compute block maxima.
                    _amsDataFrame.CreateBlockSeries(ts, TimeBlock, BlockFunctionType.Maximum, SmoothingFunctionType.None, StartMonth);

                    // Compute day of year for POT events.
                    if (TimeBlock == TimeBlockWindow.WaterYear)
                    {
                        // Shift dates for water-year convention if needed.
                        int shift = StartMonth != 1 ? 12 - StartMonth + 1 : 0;
                        ts = StartMonth != 1 ? ts.ShiftDatesByMonth(shift) : ts;
                    }

                    for (int i = 0; i < ts.Count; i++)
                    {
                        _potDays.Add(ts[i].Index.DayOfYear);
                    }
                }
            }
            catch (Exception ex)
            {
                // Swallow exceptions to avoid crashing the model on imperfect
                // time-series data; AMSDataFrame and POTDays will simply
                // remain minimal or empty.
                Debug.WriteLine($"PointProcessModel.SetTimeBlockSeries: time-series processing skipped: {ex.Message}");
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

            // Seasonal change-point parameters (in day-of-year).
            if (Distribution.Distributions.Count() > 1)
            {
                Parameters.Add(new ModelParameter
                {
                    Name = "Change Point K₁",
                    Value = 90,
                    LowerBound = 10,
                    UpperBound = 170,
                    PriorDistribution = new Uniform(10, 170)
                });

                Parameters.Add(new ModelParameter
                {
                    Name = "Change Point K₂",
                    Value = 250,
                    LowerBound = 171,
                    UpperBound = 330,
                    PriorDistribution = new Uniform(171, 330)
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
        /// <remarks>
        /// Explicit override of the base implementation to ensure the canonical
        /// pattern: <c>Data + Prior</c>, with non-finite results collapsed to
        /// <see cref="double.NegativeInfinity"/>. The base implementation collapses
        /// to <c>NegativeInfinity</c> only when <see cref="Tools.IsFinite"/> fails,
        /// which is bypassed when an inner method returns <see cref="double.NegativeInfinity"/>
        /// (a finite sentinel). Forcing the check here keeps the contract consistent
        /// with other <c>IModel</c> implementers.
        /// </remarks>
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

            double k1 = 0.0;
            double k2 = 0.0;
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

                k1 = parameters[0];
                k2 = parameters[1];

                if (!(k1 >= 1 && k1 <= 366 && k2 >= 1 && k2 <= 366 && k1 < k2))
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

                    if (day < k1 || day >= k2)
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

            // Uncertain / Interval / Threshold data are evaluated against the
            // composite (competing-risks) GEV likelihood — they are block-indexed,
            // not date-indexed, so day-of-year season dispatch does not apply.
            // The composite `model` already represents the seasonal mixture in its
            // full form for these data types.
            //
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
                    var ep = Integration.GaussLegendre20((q) => { return dist.PDF(q) * model.PDF(q); }, a, b) / mass;
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
                logLH += model.LogLikelihood_Intervals(data.LowerValue, data.UpperValue);
            }

            // Threshold Data
            for (int i = 0; i < DataFrame.ThresholdSeries.Count; i++)
            {
                var data = (ThresholdData)DataFrame.ThresholdSeries[i];
                if (data.NumberBelow > 0)
                    logLH += model.LogLikelihood_LeftCensored(data.Value, data.NumberBelow);
                if (data.NumberAbove > 0)
                    logLH += model.LogLikelihood_RightCensored(data.Value, data.NumberAbove);
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

            double k1 = 0.0;
            double k2 = 0.0;
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

                k1 = parameters[0];
                k2 = parameters[1];

                if (!(k1 >= 1 && k1 <= 366 && k2 >= 1 && k2 <= 366 && k1 < k2))
                    return Array.Empty<double>();

                ny1 = Ny * (k1 + (366.0 - k2)) / 366.0;
                ny2 = Ny * (k2 - k1) / 366.0;

                model.SetParameters(parameters.Skip(2).ToArray());
            }

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

                    if (day < k1 || day >= k2)
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

            // Uncertain Data — composite GEV likelihood only (no rate-term share).
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
                    var ep = Integration.GaussLegendre20((q) => { return dist.PDF(q) * model.PDF(q); }, a, b) / mass;
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
                double ll = model.LogLikelihood_Intervals(data.LowerValue, data.UpperValue);
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
                    ll += model.LogLikelihood_LeftCensored(data.Value, data.NumberBelow);
                if (data.NumberAbove > 0)
                    ll += model.LogLikelihood_RightCensored(data.Value, data.NumberAbove);
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
                logLH += _quantilePriorsTrue[0].Distribution.LogPDF(model.InverseCDF(1 - _quantilePriorsTrue[0].Alpha));
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
                double quantile = model.InverseCDF(1 - _quantilePriorsTrue[0].Alpha);
                double ll = _quantilePriorsTrue[0].Distribution.LogPDF(quantile);
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
                double k1 = parameters[0];
                double k2 = parameters[1];

                double xi1 = parameters[2];
                double alpha1 = parameters[3];
                double kappa1 = parameters[4];

                double xi2 = parameters[5];
                double alpha2 = parameters[6];
                double kappa2 = parameters[7];

                // Correction factors
                double p1 = (k1 + (366 - k2)) / 366;
                double p2 = (k2 - k1) / 366;

                // Season 1 - use Gumbel limit when kappa is near zero
                double xiHat1, alphaHat1;
                if (Math.Abs(kappa1) < 1e-8)
                {
                    // Gumbel limit: xi_hat = xi - alpha * log(p), alpha_hat = alpha
                    xiHat1 = xi1 - alpha1 * Math.Log(p1);
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
                if (Math.Abs(kappa2) < 1e-8)
                {
                    // Gumbel limit: xi_hat = xi - alpha * log(p), alpha_hat = alpha
                    xiHat2 = xi2 - alpha2 * Math.Log(p2);
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
                double k1 = parameters[0];
                double k2 = parameters[1];

                double xi1 = parameters[2];
                double alpha1 = parameters[3];
                double kappa1 = parameters[4];

                double xi2 = parameters[5];
                double alpha2 = parameters[6];
                double kappa2 = parameters[7];

                // Correction factors
                double p1 = (k1 + (366 - k2)) / 366;
                double p2 = (k2 - k1) / 366;

                // Season 1 - use Gumbel limit when kappa is near zero
                double xiHat1, alphaHat1;
                if (Math.Abs(kappa1) < 1e-8)
                {
                    xiHat1 = xi1 - alpha1 * Math.Log(p1);
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
                if (Math.Abs(kappa2) < 1e-8)
                {
                    xiHat2 = xi2 - alpha2 * Math.Log(p2);
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

            if (double.IsNaN(Lambda) || !Tools.IsFinite(Lambda) || Lambda <= 0)
            {
                isValid = false;
                messages.Add("Error: Lambda (average events per year) must be positive and finite.");
            }

            // Seasonal-specific checks
            if (IsSeasonal)
            {
                if (StartMonth < 1 || StartMonth > 12)
                {
                    isValid = false;
                    messages.Add("Error: StartMonth must be between 1 and 12 for seasonal models.");
                }

                if (AMSDataFrame is null || AMSDataFrame.ExactSeries.Count == 0)
                {
                    isValid = false;
                    messages.Add("Error: AMSDataFrame has no exact data for seasonal model.");
                }

                // Change points (if present)
                if (Parameters is not null && Parameters.Count >= 2 &&
                    Parameters[0].Name.StartsWith("Change Point", StringComparison.OrdinalIgnoreCase) &&
                    Parameters[1].Name.StartsWith("Change Point", StringComparison.OrdinalIgnoreCase))
                {
                    double k1 = Parameters[0].Value;
                    double k2 = Parameters[1].Value;

                    if (!(k1 >= 1 && k1 <= 366 && k2 >= 1 && k2 <= 366 && k1 < k2))
                    {
                        isValid = false;
                        messages.Add("Error: Seasonal change points K1 and K2 must satisfy 1 ≤ K1 < K2 ≤ 366.");
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
        /// Generates random samples from the point process (peaks-over-threshold) distribution.
        /// The exceedance distribution (typically GPD) with the threshold is used for generation.
        /// </para>
        /// </remarks>
        public double[] GenerateRandomValues(int sampleSize, int seed = -1)
        {
            if (sampleSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(sampleSize), "Sample size must be positive.");
            if (Distribution is null)
                throw new InvalidOperationException("Distribution cannot be null when generating random values.");

            var rng = seed > 0 ? new Numerics.Sampling.MersenneTwister(seed) : new Numerics.Sampling.MersenneTwister();
            var result = new double[sampleSize];
            var components = Distribution.Distributions;
            bool useMinimum = Distribution.MinimumOfRandomVariables;

            for (int i = 0; i < sampleSize; i++)
            {
                // Generate values from all components (GPD + any other components)
                double combinedValue = useMinimum ? double.MaxValue : double.NegativeInfinity;

                foreach (var component in components)
                {
                    double value = component.InverseCDF(rng.NextDouble());
                    if (useMinimum)
                        combinedValue = Math.Min(combinedValue, value);
                    else
                        combinedValue = Math.Max(combinedValue, value);
                }

                result[i] = combinedValue;
            }

            return result;
        }

        /// <summary>
        /// Generates a synthetic Peaks-Over-Threshold (POT) time series from the
        /// fitted Poisson-GPD/GEV process: total event count is drawn from
        /// Poisson(λ · durationYears) where λ = N / TotalYears, then magnitudes are
        /// sampled from the exceedance distribution conditional on Y &gt; Threshold.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Unlike <see cref="GenerateRandomValues(int, int)"/> — which mirrors the
        /// CompetingRisks max/min API and returns just magnitudes — this method
        /// returns a date-stamped <see cref="TimeSeries"/> that respects the model's
        /// seasonal structure (when <see cref="IsSeasonal"/> is true).
        /// </para>
        /// <para>
        /// <b>Water year vs. calendar year:</b> the per-year rate λ is independent
        /// of the year convention; only the date assignment cares. Pass
        /// <paramref name="startDate"/> aligned with whichever year convention you
        /// want — for example, the first day of <see cref="StartMonth"/> for a
        /// water year, or January 1 for a calendar year. For seasonal models,
        /// season classification uses the day-of-year (1-366) of the generated
        /// date and the fitted season-change parameters (k1, k2) to dispatch to
        /// Season 1 (day &lt; k1 || day ≥ k2) or Season 2 (k1 ≤ day &lt; k2),
        /// matching <see cref="DataLogLikelihood(double[])"/>.
        /// </para>
        /// </remarks>
        /// <param name="startDate">First date in the synthetic record.</param>
        /// <param name="durationYears">Length of the synthetic record in years.</param>
        /// <param name="seed">Master PRNG seed; pass -1 for non-deterministic.</param>
        /// <returns>A daily-interval <see cref="TimeSeries"/> of (date, magnitude)
        /// pairs, one per simulated exceedance, sorted by date.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when
        /// <paramref name="durationYears"/> is non-positive.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the model state
        /// is incomplete (no <see cref="Distribution"/>, no <see cref="DataFrame"/>,
        /// or NaN/non-positive <see cref="Threshold"/>/<see cref="TotalYears"/>).</exception>
        public TimeSeries GeneratePOTTimeSeries(DateTime startDate, double durationYears, int seed = -1)
        {
            if (durationYears <= 0)
                throw new ArgumentOutOfRangeException(nameof(durationYears), "Duration must be positive.");
            if (Distribution is null)
                throw new InvalidOperationException("Distribution cannot be null when generating POT samples.");
            if (DataFrame is null)
                throw new InvalidOperationException("DataFrame cannot be null when generating POT samples.");

            double u = Threshold;
            double Ny = TotalYears;
            if (double.IsNaN(u) || double.IsNaN(Ny) || Ny <= 0)
                throw new InvalidOperationException("Threshold and TotalYears must be set on the model.");

            int observedCount = DataFrame.ExactSeries?.Count ?? 0;
            double lambda = observedCount / Ny;            // events per year
            double expectedEvents = lambda * durationYears;

            var rng = seed > 0 ? new Numerics.Sampling.MersenneTwister(seed) : new Numerics.Sampling.MersenneTwister();

            // Sample total event count from Poisson(expectedEvents).
            int totalEvents = SamplePoisson(expectedEvents, rng);

            var result = new TimeSeries(TimeInterval.OneDay);
            if (totalEvents <= 0) return result;

            // Configure a local clone of the CompetingRisks distribution. For
            // seasonal mode, the first two parameters are k1, k2; the rest go to
            // the per-season distributions.
            var parameters = Parameters.Select(p => p.Value).ToArray();
            var localModel = (CompetingRisks)Distribution.Clone();
            double k1 = 0.0, k2 = 0.0;
            if (IsSeasonal && parameters.Length >= 2)
            {
                k1 = parameters[0];
                k2 = parameters[1];
                localModel.SetParameters(parameters.Skip(2).ToArray());
            }
            else
            {
                localModel.SetParameters(parameters);
            }

            // Total span for uniform date sampling. 365.25 averages over leap years.
            double totalDays = durationYears * 365.25;

            var events = new List<(DateTime date, double magnitude)>(totalEvents);
            for (int i = 0; i < totalEvents; i++)
            {
                double offsetDays = rng.NextDouble() * totalDays;
                var eventDate = startDate.AddDays(offsetDays);

                // Pick the marginal distribution for this date.
                UnivariateDistributionBase mark;
                if (IsSeasonal && localModel.Distributions.Count >= 2)
                {
                    int day = eventDate.DayOfYear;
                    mark = (day < k1 || day >= k2)
                        ? localModel.Distributions[0]
                        : localModel.Distributions[1];
                }
                else
                {
                    mark = localModel.Distributions[0];
                }

                // Sample conditional on Y > u via inverse-CDF on the conditional
                // CDF: F_cond(y) = (F(y) - F(u)) / (1 - F(u)); inverse mapping is
                // y = F^{-1}(F(u) + (1 - F(u)) · U).
                double Fu = mark.CDF(u);
                if (Fu >= 1.0 - 1e-12) Fu = 1.0 - 1e-12;
                double pUnif = Fu + (1.0 - Fu) * rng.NextDouble();
                if (pUnif >= 1.0) pUnif = 1.0 - 1e-12;
                double magnitude = mark.InverseCDF(pUnif);

                events.Add((eventDate, magnitude));
            }

            events.Sort((a, b) => a.date.CompareTo(b.date));
            foreach (var (date, magnitude) in events)
            {
                result.Add(new SeriesOrdinate<DateTime, double>(date, magnitude));
            }

            return result;
        }

        /// <summary>
        /// Samples an integer event count from a Poisson distribution with the
        /// given mean. Uses Knuth's algorithm for small means and a normal
        /// approximation for large means.
        /// </summary>
        /// <param name="mean">The Poisson rate (event count expected).</param>
        /// <param name="rng">A Mersenne-Twister PRNG.</param>
        /// <returns>A non-negative integer event count.</returns>
        private static int SamplePoisson(double mean, Numerics.Sampling.MersenneTwister rng)
        {
            if (mean <= 0.0) return 0;

            if (mean > 30.0)
            {
                // Normal approximation: N(mean, mean) is accurate for mean > 30.
                double z = Numerics.Distributions.Normal.StandardZ(rng.NextDouble());
                int n = (int)Math.Round(mean + Math.Sqrt(mean) * z);
                return Math.Max(0, n);
            }

            // Knuth's small-mean algorithm.
            double L = Math.Exp(-mean);
            int k = 0;
            double p = 1.0;
            while (true)
            {
                k++;
                p *= rng.NextDouble();
                if (p <= L) return k - 1;
                // Safety guard for pathological RNG sequences.
                if (k > 1000000) return k - 1;
            }
        }

        #endregion


    }
}
