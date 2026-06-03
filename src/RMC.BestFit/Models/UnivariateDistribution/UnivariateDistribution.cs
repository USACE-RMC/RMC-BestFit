using Numerics;
using Numerics.Distributions;
using Numerics.Mathematics.Integration;
using RMC.BestFit.Models.TrendFunctions;
using RMC.BestFit.Models.TrendFunctions.Support;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Xml.Linq;


namespace RMC.BestFit.Models
{
    /// <summary>
    /// Univariate distribution model.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// </remarks>
    public class UnivariateDistribution : UnivariateDistributionModelBase, ISimulatable<double[]>, IUnivariateModel
    {

        #region Construction

        /// <summary>
        /// Constructs a new univariate distribution model using a Log-Pearson Type III distribution and no data.
        /// </summary>
        public UnivariateDistribution()
        {
            Distribution = CreateDistribution(UnivariateDistributionType.LogPearsonTypeIII);
        }


        /// <summary>
        /// Constructs a new univariate distribution model.
        /// </summary>
        /// <param name="dataFrame">The data frame to fit to.</param>
        /// <param name="distribution">The univariate distribution.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="dataFrame"/> or <paramref name="distribution"/> is null.
        /// </exception>
        public UnivariateDistribution(DataFrame dataFrame, UnivariateDistributionBase distribution)
        {
            if (dataFrame is null) throw new ArgumentNullException(nameof(dataFrame));
            if (distribution is null) throw new ArgumentNullException(nameof(distribution));

            Distribution = distribution.Clone();
            DataFrame = dataFrame;
        }

        /// <summary>
        /// Constructs a new univariate distribution model.
        /// </summary>
        /// <param name="dataFrame">The data frame to fit to.</param>
        /// <param name="distributionType">The univariate distribution type.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="dataFrame"/> is null.
        /// </exception>
        public UnivariateDistribution(DataFrame dataFrame, UnivariateDistributionType distributionType)
        {
            if (dataFrame == null) throw new ArgumentNullException(nameof(dataFrame));

            Distribution = CreateDistribution(distributionType);
            DataFrame = dataFrame;
        }

        /// <summary>
        /// Constructs a new univariate distribution model by deserializing from XML.
        /// </summary>
        /// <param name="dataFrame">The data frame to fit to.</param>
        /// <param name="xElement">The XML element to deserialize.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="dataFrame"/> or <paramref name="xElement"/> is null.
        /// </exception>
        public UnivariateDistribution(DataFrame dataFrame, XElement xElement)
        {
            if (dataFrame == null) throw new ArgumentNullException(nameof(dataFrame));
            if (xElement == null) throw new ArgumentNullException(nameof(xElement));

            // Suppress SetDefaultParameters and SetDefaultQuantilePriors during deserialization.
            // The DataFrame and Distribution setters call these methods, but the persisted
            // parameter values from the XElement will immediately overwrite the defaults.
            _isDeserializing = true;

            // Use the property to ensure consistent wiring of events and preprocessing.
            DataFrame = dataFrame;

            // Distribution
            var distElement = xElement.Element("Distribution");
            if (distElement != null)
            {
                Distribution = UnivariateDistributionFactory.CreateDistribution(distElement);
            }
            else
            {
                // Fall back to default if the element is missing.
                Distribution = CreateDistribution(UnivariateDistributionType.LogPearsonTypeIII);
            }

            // Parameters
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

            // Quantiles
            var enableQuantilesAttr = xElement.Attribute(nameof(EnableQuantilePriors));
            if (enableQuantilesAttr != null)
                bool.TryParse(enableQuantilesAttr.Value, out _enableQuantilePriors);

            var singleQuantileAttr = xElement.Attribute(nameof(UseSingleQuantile));
            if (singleQuantileAttr != null)
                bool.TryParse(singleQuantileAttr.Value, out _useSingleQuantile);

            var quants = new List<QuantilePrior>();
            var quantsElement = xElement.Element(nameof(QuantilePriors));
            if (quantsElement != null)
            {
                foreach (XElement q in quantsElement.Elements(nameof(QuantilePrior)))
                    quants.Add(new QuantilePrior(q));
            }
            QuantilePriors = quants;

            // Nonstationary flags and parameters
            var nonstationaryAttr = xElement.Attribute(nameof(IsNonstationary));
            if (nonstationaryAttr != null)
                bool.TryParse(nonstationaryAttr.Value, out _isNonstationary);

            var timeIndexAttr = xElement.Attribute(nameof(ParameterTimeIndex));
            if (timeIndexAttr != null)
                int.TryParse(timeIndexAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out _parameterTimeIndex);

            var alphaAttr = xElement.Attribute(nameof(Alpha));
            if (alphaAttr != null)
                double.TryParse(alphaAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out _alpha);

            // Trend models
            var trends = new List<ITrendModel>();
            var trendsElement = xElement.Element(nameof(TrendModels));
            if (trendsElement != null)
            {
                foreach (XElement t in trendsElement.Elements())
                {
                    // Get type from attribute
                    TrendModelType type = TrendModelType.Constant;
                    var typeAttr = t.Attribute(nameof(ITrendModel.Type));
                    if (typeAttr != null)
                    {
                        Enum.TryParse(typeAttr.Value, out type);
                    }

                    ITrendModel? model = null;
                    if (type == TrendModelType.Constant)
                    {
                        model = new ConstantTrend(t);
                    }
                    if (type == TrendModelType.Cubic)
                    {
                        model = new CubicTrend(t);
                    }
                    else if (type == TrendModelType.Exponential)
                    {
                        model = new ExponentialTrend(t);
                    }
                    else if (type == TrendModelType.Linear)
                    {
                        model = new LinearTrend(t);
                    }
                    else if (type == TrendModelType.Logistic)
                    {
                        model = new LogisticTrend(t);
                    }
                    else if (type == TrendModelType.Power)
                    {
                        model = new PowerTrend(t);
                    }
                    else if (type == TrendModelType.Quadratic)
                    {
                        model = new QuadraticTrend(t);
                    }
                    else if (type == TrendModelType.Reciprocal)
                    {
                        model = new ReciprocalTrend(t);
                    }
                    else if (type == TrendModelType.Sinusoidal)
                    {
                        model = new SinusoidalTrend(t);
                    }
                    else if (type == TrendModelType.StepFunction)
                    {
                        model = new StepFunction(t);
                    }

                    if (model == null)
                        continue;

                    // Add parameter change handlers
                    for (int i = 0; i < model.NumberOfParameters; i++)
                        model.Parameters[i].PropertyChanged += Parameter_PropertyChanged;


                    trends.Add(model);
                }

                TrendModels = trends;
            }

            _isDeserializing = false;
        }

        #endregion

        #region Members

        /// <summary>
        /// Flag to suppress expensive default parameter initialization during XElement deserialization.
        /// When true, the DataFrame and Distribution setters skip SetDefaultParameters() and
        /// SetDefaultQuantilePriors() since persisted values will be restored from the XElement.
        /// </summary>
        /// <summary>
        /// The set of univariate distribution types supported by this model.
        /// </summary>
        private static readonly HashSet<UnivariateDistributionType> _supportedDistributionTypes = new()
        {
            UnivariateDistributionType.Exponential,
            UnivariateDistributionType.GammaDistribution,
            UnivariateDistributionType.GeneralizedExtremeValue,
            UnivariateDistributionType.GeneralizedLogistic,
            UnivariateDistributionType.GeneralizedNormal,
            UnivariateDistributionType.GeneralizedPareto,
            UnivariateDistributionType.Gumbel,
            UnivariateDistributionType.KappaFour,
            UnivariateDistributionType.LnNormal,
            UnivariateDistributionType.Logistic,
            UnivariateDistributionType.LogNormal,
            UnivariateDistributionType.LogPearsonTypeIII,
            UnivariateDistributionType.Normal,
            UnivariateDistributionType.PearsonTypeIII,
            UnivariateDistributionType.Weibull,
        };

        /// <summary>
        /// Determines whether the specified distribution type is supported by the univariate distribution model.
        /// </summary>
        /// <param name="distributionType">The distribution type to check.</param>
        /// <returns><c>true</c> if the distribution type is supported; otherwise, <c>false</c>.</returns>
        public static bool IsSupportedDistributionType(UnivariateDistributionType distributionType)
        {
            return _supportedDistributionTypes.Contains(distributionType);
        }

        private bool _isDeserializing = false;
        private UnivariateDistributionBase _distribution = null!;
        private bool _isNonstationary = false;
        private int _parameterTimeIndex = 0;
        private double _alpha = 0.5;

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

                    if (IsNonstationary)
                    {
                        _dataFrame.CreateFullTimeSeries();

                        if (_dataFrame.FullTimeSeries != null && _dataFrame.FullTimeSeries.Count > 0)
                        {
                            int firstIndex = _dataFrame.FullTimeSeries.First().Index;
                            int lastIndex = _dataFrame.FullTimeSeries.Last().Index;

                            if (_parameterTimeIndex < firstIndex || _parameterTimeIndex > lastIndex + 100)
                            {
                                if (_dataFrame.ExactSeries != null && _dataFrame.ExactSeries.Count > 0)
                                {
                                    ParameterTimeIndex = (int)Math.Ceiling((_dataFrame.ExactSeries.MinimumIndex() + _dataFrame.ExactSeries.MaximumIndex()) / 2.0);
                                }
                            }

                            for (int i = 0; i < TrendModels.Count; i++)
                                TrendModels[i].StartIndex = firstIndex;
                        }
                    }

                    if (UseDefaultFlatPriors && !_isDeserializing)
                        SetDefaultParameters();
                }

                RaisePropertyChange(nameof(DataFrame));
            }
        }

        /// <summary>
        /// Gets or sets the parent probability distribution.
        /// </summary>
        [Category("Inputs")]
        [DisplayName("Parent Distribution")]
        [Description("The univariate continuous probability distribution that describes the parent population. The input data is assumed to be a sample from the parent population.")]
        [Browsable(true)]
        public UnivariateDistributionBase Distribution
        {
            get { return _distribution; }
            set
            {
                if (value is null)
                    throw new ArgumentNullException(nameof(Distribution));

                _distribution = value;

                // Reset trend models based on the new distribution parameter names.
                var parameterNames = _distribution.ParameterNames;

                // Remove any previous handlers
                if (TrendModels != null)
                {
                    foreach (var tm in TrendModels)
                    {
                        for (int i = 0; i < tm.NumberOfParameters; i++)
                            tm.Parameters[i].PropertyChanged -= Parameter_PropertyChanged;
                    }
                }

                TrendModels = new List<ITrendModel>();
                for (int i = 0; i < parameterNames.Length; i++)
                    TrendModels.Add(new ConstantTrend { OwnerName = parameterNames[i] });

                RaisePropertyChange(nameof(Distribution));

                if (!_isDeserializing)
                {
                    SetDefaultParameters();
                    SetDefaultQuantilePriors();
                }
            }
        }

        /// <summary>
        /// Gets or sets the distribution type for the parent distribution.
        /// </summary>
        public UnivariateDistributionType DistributionType
        {
            get { return Distribution.Type; }
            set
            {
                if (Distribution is not null && Distribution.Type == value) return;
                Distribution = CreateDistribution(value);
                RaisePropertyChange(nameof(DistributionType));
            }
        }

        /// <summary>
        /// Gets the list of parameter trend models.
        /// </summary>
        [Category("Model Options")]
        [DisplayName("Parameter Trend Models")]
        [Description("Set the trend model for each distribution parameter.")]
        [Browsable(true)]
        public List<ITrendModel> TrendModels { get; private set; } = new List<ITrendModel>();

        #region Nonstationarity

        /// <summary>
        /// Gets or sets a value indicating whether the distribution is stationary
        /// or nonstationary.
        /// </summary>
        [Category("Model Options")]
        [DisplayName("Is Nonstationary")]
        [Description("Determines if the distribution is stationary or nonstationary. If nonstationary, the distribution parameters are allowed to vary with time.")]
        [Browsable(true)]
        public bool IsNonstationary
        {
            get { return _isNonstationary; }
            set
            {
                if (_isNonstationary != value)
                {
                    _isNonstationary = value;
                    RaisePropertyChange(nameof(IsNonstationary));

                    if (!_isNonstationary && Distribution is not null)
                    {
                        var parameterNames = _distribution.ParameterNames;
                        TrendModels = new List<ITrendModel>();
                        for (int i = 0; i < parameterNames.Length; i++)
                            TrendModels.Add(new ConstantTrend { OwnerName = parameterNames[i] });

                        RaisePropertyChange(nameof(TrendModels));
                        SetDefaultParameters();
                    }
                    else
                    {
                        if (DataFrame != null)
                        {
                            DataFrame.CreateFullTimeSeries();
                            if (DataFrame.ExactSeries != null && DataFrame.ExactSeries.Count > 0)
                            {
                                ParameterTimeIndex = (int)Math.Ceiling(
                                    (DataFrame.ExactSeries.MinimumIndex() + DataFrame.ExactSeries.MaximumIndex()) / 2.0);
                            }

                            if (DataFrame.FullTimeSeries != null && DataFrame.FullTimeSeries.Count > 0)
                            {
                                int startIndex = DataFrame.FullTimeSeries.First().Index;
                                for (int i = 0; i < TrendModels.Count; i++)
                                    TrendModels[i].StartIndex = startIndex;
                            }
                        }

                        if (UseDefaultFlatPriors)
                            SetDefaultParameters();
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the time step index for evaluating the nonstationary distribution.
        /// </summary>
        [Category("Model Options")]
        [DisplayName("Parameter Time Index")]
        [Description("The time index for evaluating the nonstationary frequency distribution. By default, the average time index of the exact data series is used.")]
        [Browsable(true)]
        public int ParameterTimeIndex
        {
            get { return _parameterTimeIndex; }
            set
            {
                if (_parameterTimeIndex != value)
                {
                    _parameterTimeIndex = value;
                    RaisePropertyChange(nameof(ParameterTimeIndex));
                    if (Parameters != null)
                        SetParameterValues(Parameters.Select(x => x.Value).ToList());
                }
            }
        }

        /// <summary>
        /// Gets or sets the exceedance probability used for evaluating the nonstationary chronology.
        /// </summary>
        [Category("Model Options")]
        [DisplayName("Exceedance Probability, Alpha")]
        [Description("The exceedance probability used for evaluating the nonstationary chronology. The default exceedance probability is 0.5, corresponding to a 2 year return period.")]
        [Browsable(true)]
        public double Alpha
        {
            get { return _alpha; }
            set
            {
                if (_alpha != value)
                {
                    _alpha = value;
                    RaisePropertyChange(nameof(Alpha));
                }
            }
        }

        #endregion

        #endregion

        #region Methods

        /// <inheritdoc/>
        protected override void DataFrame_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DataFrame.PlottingParameter) ||
                e.PropertyName == nameof(Data.PlottingPosition))
            {
                return;
            }

            if (DataFrame is null)
                return;

            DataFrame.ProcessThresholdSeries();
            RaisePropertyChange(nameof(DataFrame));

            if (UseDefaultFlatPriors)
                SetDefaultParameters();

            if (IsNonstationary)
            {
                if (DataFrame.ExactSeries != null && DataFrame.ExactSeries.Count > 0)
                {
                    DataFrame.CreateFullTimeSeries();
                    ParameterTimeIndex = (int)Math.Ceiling((DataFrame.ExactSeries.MinimumIndex() + DataFrame.ExactSeries.MaximumIndex()) / 2.0);
                    if (DataFrame.FullTimeSeries != null && DataFrame.FullTimeSeries.Count > 0)
                    {
                        int startIndex = DataFrame.FullTimeSeries.First().Index;
                        for (int i = 0; i < TrendModels.Count; i++)
                            TrendModels[i].StartIndex = startIndex;
                    }
                }
            }
        }

        /// <summary>
        /// Creates a univariate distribution instance from a distribution type.
        /// </summary>
        /// <param name="distributionType">The type of distribution.</param>
        /// <returns>A new <see cref="UnivariateDistributionBase"/> instance.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if <paramref name="distributionType"/> is not supported.
        /// </exception>
        public static UnivariateDistributionBase CreateDistribution(UnivariateDistributionType distributionType)
        {
            if (distributionType == UnivariateDistributionType.Exponential)
                return new Exponential();
            else if (distributionType == UnivariateDistributionType.GammaDistribution)
                return new GammaDistribution();
            else if (distributionType == UnivariateDistributionType.GeneralizedExtremeValue)
                return new GeneralizedExtremeValue();
            else if (distributionType == UnivariateDistributionType.GeneralizedLogistic)
                return new GeneralizedLogistic();
            else if (distributionType == UnivariateDistributionType.GeneralizedNormal)
                return new GeneralizedNormal();
            else if (distributionType == UnivariateDistributionType.GeneralizedPareto)
                return new GeneralizedPareto();
            else if (distributionType == UnivariateDistributionType.Gumbel)
                return new Gumbel();
            else if (distributionType == UnivariateDistributionType.KappaFour)
                return new KappaFour();
            else if (distributionType == UnivariateDistributionType.LnNormal)
                return new LnNormal();
            else if (distributionType == UnivariateDistributionType.Logistic)
                return new Logistic();
            else if (distributionType == UnivariateDistributionType.LogNormal)
                return new LogNormal();
            else if (distributionType == UnivariateDistributionType.LogPearsonTypeIII)
                return new LogPearsonTypeIII();
            else if (distributionType == UnivariateDistributionType.Normal)
                return new Normal();
            else if (distributionType == UnivariateDistributionType.PearsonTypeIII)
                return new PearsonTypeIII();
            else if (distributionType == UnivariateDistributionType.Weibull)
                return new Weibull();
            else
                throw new ArgumentOutOfRangeException(nameof(distributionType), distributionType, "Unsupported distribution type.");
        }

        /// <inheritdoc/>
        public override void SetDefaultParameters()
        {
            if (Distribution is null)
                return;

            // Remove old handlers
            if (Parameters is not null && Parameters.Count > 0)
            {
                for (int i = 0; i < Parameters.Count; i++)
                    Parameters[i].PropertyChanged -= Parameter_PropertyChanged;               
            }

            // Remove old handlers
            if (TrendModels.Count > 0)
            {
                for (int i = 0; i < TrendModels.Count; i++)
                {
                    var tm = TrendModels[i];
                    if (tm is not null)
                    {
                        for (int j = 0; j < tm.NumberOfParameters; j++)
                            tm.Parameters[j].PropertyChanged -= Parameter_PropertyChanged;
                    }
                }
            }

            try
            {
                // Get default parameter values
                if (DataFrame != null && DataFrame.Validate().IsValid && DataFrame.ExactSeries != null && DataFrame.ExactSeries.Count > 0)
                {
                    // Get typical parameter constraints
                    var tuple = ((IMaximumLikelihoodEstimation)Distribution).GetParameterConstraints(DataFrame.ExactSeries.Select(x => x.Value).ToList());
                    var initials = tuple.Item1;
                    var lowers = tuple.Item2;
                    var uppers = tuple.Item3;

                    // Make sure full time series exists
                    if (IsNonstationary && (DataFrame.FullTimeSeries == null || DataFrame.FullTimeSeries.Count() != DataFrame.TotalRecordLength()))
                        DataFrame.CreateFullTimeSeries();
                    
                    // If no time series, use default constraints
                    double tmin = DataFrame.FullTimeSeries!.Count > 2 ? DataFrame.FullTimeSeries!.First().Index : 1;
                    double tmax = DataFrame.FullTimeSeries!.Count > 2 ? DataFrame.FullTimeSeries!.Last().Index : 2;
                    double trange = tmax - tmin;
                    double tmid = (tmin + tmax) / 2.0;

                    for (int i = 0; i < Distribution.NumberOfParameters; i++)
                    {

                        TrendModels[i].SetDefaultParameters();
                        TrendModels[i].StartIndex = (int)tmin;

                        // Set location parameters
                        TrendModels[i].Parameters[0].Value = initials[i];
                        TrendModels[i].Parameters[0].LowerBound = lowers[i];
                        TrendModels[i].Parameters[0].UpperBound = uppers[i];
                        TrendModels[i].Parameters[0].IsPositive = lowers[i] == Tools.DoubleMachineEpsilon;
                        TrendModels[i].Parameters[0].PriorDistribution = new Uniform(lowers[i], uppers[i]);

                        // Get values bounds
                        double xmin = Tools.Min(new[]
                        {
                            DataFrame.ExactSeries.MinimumValue(),
                            DataFrame.UncertainSeries.MinimumValue(),
                            DataFrame.IntervalSeries.MinimumValue(),
                            DataFrame.ThresholdSeries.MinimumValue()
                        });

                        double xmax = Tools.Max(new[]
                        {
                            DataFrame.ExactSeries.MaximumValue(),
                            DataFrame.UncertainSeries.MaximumValue(),
                            DataFrame.IntervalSeries.MaximumValue(),
                            DataFrame.ThresholdSeries.MaximumValue()
                        });

                        // xrange is shaped by the parameter's upper bound rather than
                        // the data range; the data-range computation here is intentional
                        // for log-distribution parameter scaling but currently overridden
                        // by uppers[i] for trend-coefficient bounds.
                        double xrange = uppers[i];

                        double beta = 5d / trange;
                        // Guard against zero/very small beta
                        double absBeta = Math.Abs(beta);
                        double scale = absBeta > 1e-15 ? Math.Pow(10, Math.Floor(Math.Log10(absBeta))) : 1e-15;
                        beta = Math.Ceiling(beta / scale) * scale;

                        double tdelta1 = xrange / trange;
                        double tdelta2 = xrange / Math.Pow(trange / 2d, 2);
                        double tdelta3 = xrange / Math.Pow(trange / 2d, 3);
                        // Guard against zero/very small tdelta values
                        tdelta1 = Math.Abs(tdelta1) > 1e-15 ? Math.Pow(10, Math.Floor(Math.Log10(Math.Abs(tdelta1)) + 1)) : 1e-15;
                        tdelta2 = Math.Abs(tdelta2) > 1e-15 ? Math.Pow(10, Math.Floor(Math.Log10(Math.Abs(tdelta2)) + 1)) : 1e-15;
                        tdelta3 = Math.Abs(tdelta3) > 1e-15 ? Math.Pow(10, Math.Floor(Math.Log10(Math.Abs(tdelta3)) + 1)) : 1e-15;

                        // Linear Trend
                        if (TrendModels[i] is LinearTrend)
                        {
                            TrendModels[i].Parameters[1].Value = 0;
                            TrendModels[i].Parameters[1].LowerBound = -tdelta1;
                            TrendModels[i].Parameters[1].UpperBound = tdelta1;
                            TrendModels[i].Parameters[1].PriorDistribution = new Uniform(-tdelta1, tdelta1);
                        }
                        // Quadratic Trend
                        else if (TrendModels[i] is QuadraticTrend)
                        {
                            TrendModels[i].Parameters[1].Value = 0;
                            TrendModels[i].Parameters[1].LowerBound = -tdelta1;
                            TrendModels[i].Parameters[1].UpperBound = tdelta1;
                            TrendModels[i].Parameters[1].PriorDistribution = new Uniform(-tdelta1, tdelta1);

                            TrendModels[i].Parameters[2].Value = 0;
                            TrendModels[i].Parameters[2].LowerBound = -tdelta2;
                            TrendModels[i].Parameters[2].UpperBound = tdelta2;
                            TrendModels[i].Parameters[2].PriorDistribution = new Uniform(-tdelta2, tdelta2);
                        }
                        // Cubic Trend
                        else if (TrendModels[i] is CubicTrend)
                        {
                            TrendModels[i].Parameters[1].Value = 0;
                            TrendModels[i].Parameters[1].LowerBound = -tdelta1;
                            TrendModels[i].Parameters[1].UpperBound = tdelta1;
                            TrendModels[i].Parameters[1].PriorDistribution = new Uniform(-tdelta1, tdelta1);

                            TrendModels[i].Parameters[2].Value = 0;
                            TrendModels[i].Parameters[2].LowerBound = -tdelta2;
                            TrendModels[i].Parameters[2].UpperBound = tdelta2;
                            TrendModels[i].Parameters[2].PriorDistribution = new Uniform(-tdelta2, tdelta2);

                            TrendModels[i].Parameters[3].Value = 0;
                            TrendModels[i].Parameters[3].LowerBound = -tdelta3;
                            TrendModels[i].Parameters[3].UpperBound = tdelta3;
                            TrendModels[i].Parameters[3].PriorDistribution = new Uniform(-tdelta3, tdelta3);
                        }
                        // Exponential and Logistic Trend
                        else if (TrendModels[i] is ExponentialTrend || TrendModels[i] is LogisticTrend)
                        {
                            TrendModels[i].Parameters[1].Value = 0;
                            TrendModels[i].Parameters[1].LowerBound = -beta;
                            TrendModels[i].Parameters[1].UpperBound = beta;
                            TrendModels[i].Parameters[1].PriorDistribution = new Uniform(-beta, beta);
                        }
                        // Sinusoidal
                        else if (TrendModels[i] is SinusoidalTrend)
                        {
                            TrendModels[i].Parameters[1].LowerBound = 0;
                            TrendModels[i].Parameters[1].UpperBound = uppers[i] / 2d;
                            TrendModels[i].Parameters[1].PriorDistribution = new Uniform(0, uppers[i] / 2d);
                        }
                        // Step Function
                        else if (TrendModels[i] is StepFunction)
                        {
                            TrendModels[i].Parameters[1].Value = initials[i];
                            TrendModels[i].Parameters[1].LowerBound = lowers[i];
                            TrendModels[i].Parameters[1].UpperBound = uppers[i];
                            TrendModels[i].Parameters[1].IsPositive = lowers[i] == Tools.DoubleMachineEpsilon;
                            TrendModels[i].Parameters[1].PriorDistribution = new Uniform(lowers[i], uppers[i]);

                            // Change point
                            TrendModels[i].Parameters[2].Value = tmid;
                            TrendModels[i].Parameters[2].LowerBound = tmin;
                            TrendModels[i].Parameters[2].UpperBound = tmax;
                            TrendModels[i].Parameters[2].PriorDistribution = new Uniform(tmin, tmax);
                        }

                    }
                }

            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to set default parameters for the univariate distribution.", ex);
            }

            var parameterNames = Distribution.ParameterNames;
            for (int i = 0; i < parameterNames.Length; i++)
                TrendModels[i].OwnerName = parameterNames[i];

            _parameters = new List<ModelParameter>();
            for (int i = 0; i < Distribution.NumberOfParameters; i++)
                _parameters.AddRange(TrendModels[i].Parameters);

            // If not nonstationary, remove name string
            if (!IsNonstationary)
            {
                for (int i = 0; i < Distribution.NumberOfParameters; i++)
                    _parameters[i].Name = "";
            }

            // Add handlers
            if (Parameters is not null && Parameters.Count > 0)
            {
                for (int i = 0; i < Parameters.Count; i++)
                    Parameters[i].PropertyChanged += Parameter_PropertyChanged;
            }

            // Add handlers
            if (TrendModels is not null && TrendModels.Count > 0)
            {
                for (int i = 0; i < TrendModels.Count; i++)
                {
                    if (TrendModels[i] != null)
                    {
                        for (int j = 0; j < TrendModels[i].NumberOfParameters; j++)
                            TrendModels[i].Parameters[j].PropertyChanged += Parameter_PropertyChanged;
                    }
                }
            }

            RaisePropertyChange(nameof(SetDefaultParameters));

        }

        /// <summary>
        /// Sets the trend model type for a given distribution parameter index.
        /// </summary>
        /// <param name="index">Index of the distribution parameter.</param>
        /// <param name="type">The trend function type.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if <paramref name="index"/> is out of range.
        /// </exception>
        public void SetTrendModel(int index, TrendModelType type)
        {
            if (Distribution is null)
                throw new InvalidOperationException("Distribution must be set before setting a trend model.");

            if (index < 0 || index >= Distribution.NumberOfParameters)
                throw new ArgumentOutOfRangeException(nameof(index), "The index is out of range.");

            // Remove old handlers
            if (Parameters is not null && Parameters.Count > 0)
            {
                for (int i = 0; i < Parameters.Count; i++)
                    Parameters[i].PropertyChanged -= Parameter_PropertyChanged;
            }

            // Remove old handlers for this trend model
            if (TrendModels[index] != null)
            {
                for (int j = 0; j < TrendModels[index].NumberOfParameters; j++)
                    TrendModels[index].Parameters[j].PropertyChanged -= Parameter_PropertyChanged;
            }

            var parameterNames = Distribution.ParameterNames;
            ITrendModel model = new ConstantTrend();
            if (type == TrendModelType.Cubic)
            {
                model = new CubicTrend();
            }
            else if (type == TrendModelType.Exponential)
            {
                model = new ExponentialTrend();
            }
            else if (type == TrendModelType.Linear)
            {
                model = new LinearTrend();
            }
            else if (type == TrendModelType.Logistic)
            {
                model = new LogisticTrend();
            }
            else if (type == TrendModelType.Power)
            {
                model = new PowerTrend();
            }
            else if (type == TrendModelType.Quadratic)
            {
                model = new QuadraticTrend();
            }
            else if (type == TrendModelType.Reciprocal)
            {
                model = new ReciprocalTrend();
            }
            else if (type == TrendModelType.Sinusoidal)
            {
                model = new SinusoidalTrend();
            }
            else if (type == TrendModelType.StepFunction)
            {
                model = new StepFunction();
            }

            try
            {
                // Get default parameter values
                if (DataFrame != null && DataFrame.Validate().IsValid && DataFrame.ExactSeries != null && DataFrame.ExactSeries.Count > 0)
                {

                    // Get typical parameter constraints
                    var tuple = ((IMaximumLikelihoodEstimation)Distribution).GetParameterConstraints(DataFrame.ExactSeries.Select(x => x.Value).ToList());
                    var initials = tuple.Item1;
                    var lowers = tuple.Item2;
                    var uppers = tuple.Item3;

                    // Make sure full time series exists
                    if (IsNonstationary && (DataFrame.FullTimeSeries == null || DataFrame.FullTimeSeries.Count() != DataFrame.TotalRecordLength()))
                        DataFrame.CreateFullTimeSeries();

                    // Get time constraints
                    double tmin = DataFrame.FullTimeSeries!.Count > 2 ? DataFrame.FullTimeSeries!.First().Index : 1;
                    double tmax = DataFrame.FullTimeSeries!.Count > 2 ? DataFrame.FullTimeSeries!.Last().Index : 2;
                    double trange = tmax - tmin;
                    double tmid = (tmin + tmax) / 2.0;

                    model.SetDefaultParameters();
                    model.StartIndex = (int)tmin;
                    
                    // Set location parameters
                    model.Parameters[0].Value = initials[index];
                    model.Parameters[0].LowerBound = lowers[index];
                    model.Parameters[0].UpperBound = uppers[index];
                    model.Parameters[0].IsPositive = lowers[index] == Tools.DoubleMachineEpsilon;
                    model.Parameters[0].PriorDistribution = new Uniform(lowers[index], uppers[index]);

                    // Get values bounds
                    double xmin = Tools.Min(new[]
                    {
                        DataFrame.ExactSeries.MinimumValue(),
                        DataFrame.UncertainSeries.MinimumValue(),
                        DataFrame.IntervalSeries.MinimumValue(),
                        DataFrame.ThresholdSeries.MinimumValue()
                    });

                    double xmax = Tools.Max(new[]
{
                        DataFrame.ExactSeries.MaximumValue(),
                        DataFrame.UncertainSeries.MaximumValue(),
                        DataFrame.IntervalSeries.MaximumValue(),
                        DataFrame.ThresholdSeries.MaximumValue()
                    });

                    // xrange is shaped by the parameter's upper bound rather than the
                    // data range; the data-range computation here is intentional for
                    // log-distribution parameter scaling but currently overridden by
                    // uppers[index] for trend-coefficient bounds.
                    double xrange = uppers[index];

                    double beta = 5d / trange;
                    // Guard against zero/very small beta
                    double absBeta = Math.Abs(beta);
                    double scale = absBeta > 1e-15 ? Math.Pow(10, Math.Floor(Math.Log10(absBeta))) : 1e-15;
                    beta = Math.Ceiling(beta / scale) * scale;

                    double tdelta1 = xrange / trange;
                    double tdelta2 = xrange / Math.Pow(trange / 2d, 2);
                    double tdelta3 = xrange / Math.Pow(trange / 2d, 3);
                    // Guard against zero/very small tdelta values
                    tdelta1 = Math.Abs(tdelta1) > 1e-15 ? Math.Pow(10, Math.Floor(Math.Log10(Math.Abs(tdelta1)) + 1)) : 1e-15;
                    tdelta2 = Math.Abs(tdelta2) > 1e-15 ? Math.Pow(10, Math.Floor(Math.Log10(Math.Abs(tdelta2)) + 1)) : 1e-15;
                    tdelta3 = Math.Abs(tdelta3) > 1e-15 ? Math.Pow(10, Math.Floor(Math.Log10(Math.Abs(tdelta3)) + 1)) : 1e-15;


                    // Linear Trend
                    if (model is LinearTrend)
                    {
                        model.Parameters[1].Value = 0;
                        model.Parameters[1].LowerBound = -tdelta1;
                        model.Parameters[1].UpperBound = tdelta1;
                        model.Parameters[1].PriorDistribution = new Uniform(-tdelta1, tdelta1);
                    }
                    // Quadratic Trend
                    else if (model is QuadraticTrend)
                    {
                        model.Parameters[1].Value = 0;
                        model.Parameters[1].LowerBound = -tdelta1;
                        model.Parameters[1].UpperBound = tdelta1;
                        model.Parameters[1].PriorDistribution = new Uniform(-tdelta1, tdelta1);

                        model.Parameters[2].Value = 0;
                        model.Parameters[2].LowerBound = -tdelta2;
                        model.Parameters[2].UpperBound = tdelta2;
                        model.Parameters[2].PriorDistribution = new Uniform(-tdelta2, tdelta2);
                    }
                    // Cubic Trend
                    else if (model is CubicTrend)
                    {
                        model.Parameters[1].Value = 0;
                        model.Parameters[1].LowerBound = -tdelta1;
                        model.Parameters[1].UpperBound = tdelta1;
                        model.Parameters[1].PriorDistribution = new Uniform(-tdelta1, tdelta1);

                        model.Parameters[2].Value = 0;
                        model.Parameters[2].LowerBound = -tdelta2;
                        model.Parameters[2].UpperBound = tdelta2;
                        model.Parameters[2].PriorDistribution = new Uniform(-tdelta2, tdelta2);

                        model.Parameters[3].Value = 0;
                        model.Parameters[3].LowerBound = -tdelta3;
                        model.Parameters[3].UpperBound = tdelta3;
                        model.Parameters[3].PriorDistribution = new Uniform(-tdelta3, tdelta3);
                    }
                    // Exponential and Logistic Trend
                    else if (model is ExponentialTrend || model is LogisticTrend)
                    {
                        model.Parameters[1].Value = 0;
                        model.Parameters[1].LowerBound = -beta;
                        model.Parameters[1].UpperBound = beta;
                        model.Parameters[1].PriorDistribution = new Uniform(-beta, beta);
                    }
                    // Sinusoidal
                    else if (model is SinusoidalTrend)
                    {                   
                        model.Parameters[1].LowerBound = 0;
                        model.Parameters[1].UpperBound = uppers[index] / 2d;
                        model.Parameters[1].Value = 0.5 * (model.Parameters[1].LowerBound + model.Parameters[1].UpperBound);
                        model.Parameters[1].PriorDistribution = new Uniform(0, uppers[index] / 2d);
                    }
                    // Step Function
                    else if (model is StepFunction)
                    {
                        model.Parameters[1].Value = initials[index];
                        model.Parameters[1].LowerBound = lowers[index];
                        model.Parameters[1].UpperBound = uppers[index];
                        model.Parameters[1].IsPositive = lowers[index] == Tools.DoubleMachineEpsilon;
                        model.Parameters[1].PriorDistribution = new Uniform(lowers[index], uppers[index]);

                        // Change point
                        model.Parameters[2].Value = tmid;
                        model.Parameters[2].LowerBound = tmin;
                        model.Parameters[2].UpperBound = tmax;
                        model.Parameters[2].PriorDistribution = new Uniform(tmin, tmax);
                    }
                  
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to set the trend model defaults.", ex);
            }

            TrendModels[index] = model;
            TrendModels[index].OwnerName = parameterNames[index];

            // Reset Parameters list
            _parameters = new List<ModelParameter>();
            for (int i = 0; i < Distribution.NumberOfParameters; i++)
            {
                _parameters.AddRange(TrendModels[i].Parameters);
            }

            // Add handlers to all parameters
            if (Parameters is not null && Parameters.Count > 0)
            {
                for (int i = 0; i < Parameters.Count; i++)
                    Parameters[i].PropertyChanged += Parameter_PropertyChanged;
            }

            RaisePropertyChange(nameof(Parameters));
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
            int qCount = UseSingleQuantile ? 1 : Distribution.NumberOfParameters;

            // See if there are already quantile priors
            if (QuantilePriors != null && QuantilePriors.Count >= 1)
            {
                priors = QuantilePriors.ToList();
                // Remove existing priors if needed
                if (priors.Count > qCount)
                {
                    while (priors.Count != qCount)
                        priors.Remove(priors.Last());
                }
                else if (priors.Count < qCount)
                {
                    while (priors.Count < qCount)
                    {
                        priors.Add(new QuantilePrior(priors.Last().Alpha / 10, new LnNormal()));
                        double mu = Math.Round(Distribution.InverseCDF(1 - priors.Last().Alpha), 2);
                        double sigma = Math.Round(mu * 0.15, 2);
                        priors.Last().Distribution.SetParameters(new double[] { mu, sigma });
                    }
                }
            }
            else
            {
                for (int i = 1; i <= qCount; i++)
                {
                    priors.Add(new QuantilePrior(1 * Math.Pow(10, -i), new LnNormal()));
                    double mu = Math.Round(Distribution.InverseCDF(1 - priors[i - 1].Alpha), 2);
                    double sigma = Math.Round(mu * 0.15, 2);
                    priors[i - 1].Distribution.SetParameters(new double[] { mu, sigma });
                }
            }

            // Reset the quantile priors with the new list and add handlers
            _quantilePriors = priors;
             for (int i = 0; i < _quantilePriors.Count; i++)
                _quantilePriors[i].PropertyChanged += QuantilePrior_PropertyChanged;

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
            else if (!UseSingleQuantile && QuantilePriors.Count == Distribution.NumberOfParameters)
            {
                // The first quantile prior remains the same
                _quantilePriorsTrue.Add(QuantilePriors[0].Clone());
                
                // Convert the remaining priors to Gamma distributions of the difference in random quantiles
                for (int i = 1; i < QuantilePriors.Count; i++)
                {
                    double muDiff = QuantilePriors[i].Distribution.Mean - QuantilePriors[i - 1].Distribution.Mean;
                    double sigmaDiff = Math.Sqrt(QuantilePriors[i].Distribution.Variance + QuantilePriors[i - 1].Distribution.Variance);
                    
                    var gamma = new GammaDistribution();
                    gamma.SetParameters(gamma.ParametersFromMoments(new double[] { muDiff, sigmaDiff }));
                    _quantilePriorsTrue.Add(new QuantilePrior(QuantilePriors[i].Alpha, gamma));
                }
            }
        }

        /// <inheritdoc/>
        public override double LogLikelihood(double[] parameters)
        {
            if (Distribution is null)
                throw new InvalidOperationException("Distribution must be set before computing log likelihood.");

            // Create model
            var model = Distribution.Clone();

            // Get the data likelihood
            double dataLogLH = IsNonstationary ? NonstationaryData_LogLikelihood(model, parameters) : StationaryData_LogLikelihood(model, parameters);

            // Get the prior likelihood
            double priorLogLH = Prior_LogLikelihood(model, parameters);

            // Return the full likelihood
            double logLH = dataLogLH + priorLogLH;
            return Tools.IsFinite(logLH) ? logLH : double.NegativeInfinity;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Overrides the base implementation so that the public prior log-likelihood
        /// returns the same value the inner <see cref="LogLikelihood(double[])"/>
        /// override uses: parameter priors plus Jeffreys and quantile priors evaluated
        /// against the distribution at the last time step (for nonstationary) or at
        /// the supplied parameters (for stationary).
        /// </remarks>
        public override double PriorLogLikelihood(double[] parameters)
        {
            if (Distribution is null)
                throw new InvalidOperationException("Distribution must be set before computing prior log likelihood.");

            if (parameters == null || parameters.Length != Parameters.Count)
                return double.NegativeInfinity;

            var model = Distribution.Clone();

            // Priors are evaluated against the distribution at the last time step,
            // matching the semantics of LogLikelihood. ParameterTimeIndex is for
            // prediction only.
            double[] distParams;
            if (IsNonstationary)
            {
                if (DataFrame is null || DataFrame.FullTimeSeries.Count == 0)
                    return double.NegativeInfinity;
                distParams = GetParameterValues(DataFrame.FullTimeSeries.Last().Index);
            }
            else
            {
                distParams = parameters.Take(model.NumberOfParameters).ToArray();
            }

            var valid = model.ValidateParameters(distParams, false);
            if (valid is null)
                model.SetParameters(distParams);

            return Prior_LogLikelihood(model, parameters);
        }

        /// <summary>
        /// Returns the stationary data log likelihood for the specified parameters.
        /// </summary>
        /// <param name="model">Working copy of the distribution.</param>
        /// <param name="parameters">Parameter vector.</param>
        /// <returns>Log likelihood of the observed data.</returns>
        public double StationaryData_LogLikelihood(UnivariateDistributionBase model, double[] parameters)
        {
            if (DataFrame is null)
                throw new InvalidOperationException("DataFrame must be set before computing log likelihood.");

            // Check if proposed parameters are valid
            var valid = model.ValidateParameters(parameters, false);
            if (valid is not null) return double.NegativeInfinity;

            // Set model parameters
            model.SetParameters(parameters);

            double logLH = 0;

            // Exact Data
            for (int i = 0; i < DataFrame.ExactSeries.Count; i++)
            {
                var data = (ExactData)DataFrame.ExactSeries[i];
                if (!data.IsLowOutlier)
                {
                    logLH += model.LogLikelihood(data.Value);
                }
                else
                {
                    logLH += model.LogLikelihood_LeftCensored(DataFrame.LowOutlierThreshold, 1);
                }
            }

            // Uncertain Data
            for (int i = 0; i < DataFrame.UncertainSeries.Count; i++)
            {
                var dist = ((UncertainData)DataFrame.UncertainSeries[i]).Distribution;
                double lowerProbability = 1E-8;
                double upperProbability = 1.0 - 1E-8;
                var a = dist.InverseCDF(lowerProbability);
                var b = dist.InverseCDF(upperProbability);
                double mass = upperProbability - lowerProbability;
                if (!Tools.IsFinite(a) || !Tools.IsFinite(b) || !Tools.IsFinite(mass) || mass <= 0.0 || a >= b)
                {
                    logLH += double.NegativeInfinity;
                    continue;
                }

                // Normalize by retained ME mass so the likelihood remains conditional on
                // the same 1E-8 probability window used across uncertain-data models.
                var ep = Integration.GaussLegendre20(q => dist.PDF(q) * model.PDF(q), a, b) / mass;
                logLH += ep > 0 ? Math.Log(ep) : double.NegativeInfinity;
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

        /// <summary>
        /// Returns the nonstationary data log likelihood for the specified parameters.
        /// </summary>
        /// <param name="model">Working copy of the distribution.</param>
        /// <param name="parameters">Parameter vector.</param>
        /// <returns>Log likelihood of the observed data.</returns>
        public double NonstationaryData_LogLikelihood(UnivariateDistributionBase model, double[] parameters)
        {
            if (DataFrame is null)
                throw new InvalidOperationException("DataFrame must be set before computing log likelihood.");

            // Cache the FullTimeSeries reference and the LowOutlierThreshold once. The
            // FullTimeSeries property getter performs a Volatile.Read + TotalRecordLength()
            // call on every access; before this caching, the loop condition and indexer
            // each invoked it, producing 2N+1 getter calls per MCMC iteration. The cache
            // is built once per analysis run (UnivariateAnalysis.RunAsync line 493) and
            // is stable for the duration of MCMC, so capturing the reference here is safe.
            var fullTimeSeries = DataFrame.FullTimeSeries;
            if (fullTimeSeries is null || fullTimeSeries.Count == 0)
                throw new InvalidOperationException("FullTimeSeries is not populated for nonstationary log likelihood.");
            int seriesCount = fullTimeSeries.Count;
            double lowOutlierThreshold = DataFrame.LowOutlierThreshold;

            double logLH = 0.0;

            // Build trend models with proposed parameters
            int t = 0;
            var trendModelsCopy = new List<ITrendModel>();
            for (int i = 0; i < TrendModels.Count; i++)
            {
                var parms = new List<double>();
                for (int j = t; j < t + TrendModels[i].NumberOfParameters; j++)
                    parms.Add(parameters[j]);

                var clone = (ITrendModel)TrendModels[i].Clone();
                clone.SetParameterValues(parms);
                trendModelsCopy.Add(clone);

                t += TrendModels[i].NumberOfParameters;
            }

            // *** Compute the full log-likelihood *** //
            int numParameters = model.NumberOfParameters;
            var values = new double[numParameters];
            for (int i = 0; i < seriesCount; i++)
            {
                // Get data at time-step i
                var data = fullTimeSeries[i];

                // Get parameters
                for (int j = 0; j < numParameters; j++)
                    values[j] = trendModelsCopy[j].Predict(data.Index);

                // Check if parameters are valid
                var valid = model.ValidateParameters(values, false);
                if (valid is not null) return double.NegativeInfinity;

                // Set parameters
                model.SetParameters(values);

                // exact data
                if (data is ExactData exact)
                {
                    if (!exact.IsLowOutlier)
                    {
                        logLH += model.LogLikelihood(exact.Value);
                    }
                    else
                    {
                        logLH += model.LogLikelihood_LeftCensored(lowOutlierThreshold, 1);
                    }
                }
                // uncertain Data
                else if (data is UncertainData uncertain)
                {
                    var dist = uncertain.Distribution;
                    double lowerProbability = 1E-8;
                    double upperProbability = 1.0 - 1E-8;
                    var a = dist.InverseCDF(lowerProbability);
                    var b = dist.InverseCDF(upperProbability);
                    double mass = upperProbability - lowerProbability;
                    if (Tools.IsFinite(a) && Tools.IsFinite(b) && Tools.IsFinite(mass) && mass > 0.0 && a < b)
                    {
                        var ep = Integration.GaussLegendre20(q => dist.PDF(q) * model.PDF(q), a, b) / mass;
                        logLH += ep > 0 ? Math.Log(ep) : double.NegativeInfinity;
                    }
                    else
                    {
                        logLH += double.NegativeInfinity;
                    }
                }
                // interval data
                else if (data is IntervalData interval)
                {
                    logLH += model.LogLikelihood_Intervals(interval.LowerValue, interval.UpperValue);
                }
                // threshold data
                else if (data is ThresholdData threshold)
                {
                    if (threshold.NumberBelow > 0)
                        logLH += model.LogLikelihood_LeftCensored(threshold.Value, threshold.NumberBelow);
                    if (threshold.NumberAbove > 0)
                        logLH += model.LogLikelihood_RightCensored(threshold.Value, threshold.NumberAbove);
                }

            }

            return logLH;
        }

        /// <inheritdoc/>
        public override double DataLogLikelihood(double[] parameters)
        {
            if (Distribution is null)
                throw new InvalidOperationException("Distribution must be set before computing data log likelihood.");

            var model = Distribution.Clone();
            double logLH =  IsNonstationary ? NonstationaryData_LogLikelihood(model, parameters) : StationaryData_LogLikelihood(model, parameters);
            if (!Tools.IsFinite(logLH)) return double.NegativeInfinity;
            return logLH;
        }

        /// <inheritdoc/>
        public override double[] PointwiseDataLogLikelihood(double[] parameters)
        {
            if (Distribution is null)
                throw new InvalidOperationException("Distribution must be set before computing pointwise log likelihood.");

            var model = Distribution.Clone();
            return IsNonstationary
                ? NonstationaryPointwiseLogLikelihood(model, parameters)
                : StationaryPointwiseLogLikelihood(model, parameters);
        }

        /// <inheritdoc/>
        public override List<DataComponent> PointwiseDataLogLikelihoodComponents(double[] parameters)
        {
            if (Distribution is null)
                throw new InvalidOperationException("Distribution must be set before computing pointwise log likelihood components.");

            var model = Distribution.Clone();
            return IsNonstationary
                ? NonstationaryPointwiseLogLikelihoodComponents(model, parameters)
                : StationaryPointwiseLogLikelihoodComponents(model, parameters);
        }

        /// <summary>
        /// Returns the stationary pointwise log-likelihood components for each observation.
        /// </summary>
        /// <param name="model">Working copy of the distribution.</param>
        /// <param name="parameters">Parameter vector.</param>
        /// <returns>List of DataComponent structs with log-likelihood and metadata.</returns>
        private List<DataComponent> StationaryPointwiseLogLikelihoodComponents(UnivariateDistributionBase model, double[] parameters)
        {
            if (DataFrame is null)
                throw new InvalidOperationException("DataFrame must be set before computing pointwise log likelihood components.");

            var result = new List<DataComponent>();
            int idx = 0;

            // Check if proposed parameters are valid
            var valid = model.ValidateParameters(parameters, false);
            if (valid is not null)
            {
                // Return list of invalid components
                for (int i = 0; i < DataFrame.ExactSeries.Count; i++)
                    result.Add(new DataComponent(idx++, double.NegativeInfinity, ((ExactData)DataFrame.ExactSeries[i]).Value, DataComponentType.Exact));
                for (int i = 0; i < DataFrame.UncertainSeries.Count; i++)
                    result.Add(new DataComponent(idx++, double.NegativeInfinity, ((UncertainData)DataFrame.UncertainSeries[i]).Distribution.Mean, DataComponentType.Uncertain));
                for (int i = 0; i < DataFrame.IntervalSeries.Count; i++)
                {
                    var interval = (IntervalData)DataFrame.IntervalSeries[i];
                    result.Add(new DataComponent(idx++, double.NegativeInfinity, (interval.LowerValue + interval.UpperValue) / 2.0, DataComponentType.Interval));
                }
                for (int i = 0; i < DataFrame.ThresholdSeries.Count; i++)
                {
                    var threshold = (ThresholdData)DataFrame.ThresholdSeries[i];
                    result.Add(new DataComponent(idx++, double.NegativeInfinity, threshold.Value, DataComponentType.LeftCensored,
                        threshold.NumberBelow + threshold.NumberAbove, $"{threshold.StartIndex}-{threshold.EndIndex}"));
                }
                return result;
            }

            // Set model parameters
            model.SetParameters(parameters);

            // Exact Data
            for (int i = 0; i < DataFrame.ExactSeries.Count; i++)
            {
                var data = (ExactData)DataFrame.ExactSeries[i];
                double logLH;
                double value = data.Value;

                if (!data.IsLowOutlier)
                {
                    logLH = model.LogLikelihood(data.Value);
                }
                else
                {
                    logLH = model.LogLikelihood_LeftCensored(DataFrame.LowOutlierThreshold, 1);
                    value = DataFrame.LowOutlierThreshold;
                }

                result.Add(new DataComponent(idx++, logLH, value, DataComponentType.Exact, 1, data.Index.ToString()));
            }

            // Uncertain Data
            for (int i = 0; i < DataFrame.UncertainSeries.Count; i++)
            {
                var data = (UncertainData)DataFrame.UncertainSeries[i];
                var dist = data.Distribution;
                double lowerProbability = 1E-8;
                double upperProbability = 1.0 - 1E-8;
                var a = dist.InverseCDF(lowerProbability);
                var b = dist.InverseCDF(upperProbability);
                double mass = upperProbability - lowerProbability;
                double logLH = double.NegativeInfinity;
                if (Tools.IsFinite(a) && Tools.IsFinite(b) && Tools.IsFinite(mass) && mass > 0.0 && a < b)
                {
                    var ep = Integration.GaussLegendre20(q => dist.PDF(q) * model.PDF(q), a, b) / mass;
                    logLH = ep > 0 ? Math.Log(ep) : double.NegativeInfinity;
                }
                result.Add(new DataComponent(idx++, logLH, dist.Mean, DataComponentType.Uncertain, 1, data.Index.ToString()));
            }

            // Interval Data
            for (int i = 0; i < DataFrame.IntervalSeries.Count; i++)
            {
                var data = (IntervalData)DataFrame.IntervalSeries[i];
                double logLH = model.LogLikelihood_Intervals(data.LowerValue, data.UpperValue);
                double midpoint = (data.LowerValue + data.UpperValue) / 2.0;

                result.Add(new DataComponent(idx++, logLH, midpoint, DataComponentType.Interval, 1, data.Index.ToString()));
            }

            // Threshold Data
            for (int i = 0; i < DataFrame.ThresholdSeries.Count; i++)
            {
                var data = (ThresholdData)DataFrame.ThresholdSeries[i];
                double logLH = 0;
                if (data.NumberBelow > 0)
                    logLH += model.LogLikelihood_LeftCensored(data.Value, data.NumberBelow);
                if (data.NumberAbove > 0)
                    logLH += model.LogLikelihood_RightCensored(data.Value, data.NumberAbove);

                // Use LeftCensored type with combined count
                int totalCount = data.NumberBelow + data.NumberAbove;
                result.Add(new DataComponent(idx++, logLH, data.Value, DataComponentType.LeftCensored, totalCount, $"{data.StartIndex}-{data.EndIndex}"));
            }

            return result;
        }

        /// <summary>
        /// Returns the nonstationary pointwise log-likelihood components for each observation.
        /// </summary>
        /// <param name="model">Working copy of the distribution.</param>
        /// <param name="parameters">Parameter vector.</param>
        /// <returns>List of DataComponent structs with log-likelihood and metadata.</returns>
        private List<DataComponent> NonstationaryPointwiseLogLikelihoodComponents(UnivariateDistributionBase model, double[] parameters)
        {
            if (DataFrame is null)
                throw new InvalidOperationException("DataFrame must be set before computing pointwise log likelihood components.");

            // See NonstationaryData_LogLikelihood for the rationale on caching FullTimeSeries.
            var fullTimeSeries = DataFrame.FullTimeSeries;
            if (fullTimeSeries is null || fullTimeSeries.Count == 0)
                throw new InvalidOperationException("FullTimeSeries is not populated for nonstationary log likelihood.");
            int seriesCount = fullTimeSeries.Count;
            double lowOutlierThreshold = DataFrame.LowOutlierThreshold;

            // Build trend models with proposed parameters
            int t = 0;
            var trendModelsCopy = new List<ITrendModel>();
            for (int i = 0; i < TrendModels.Count; i++)
            {
                var parms = new List<double>();
                for (int j = t; j < t + TrendModels[i].NumberOfParameters; j++)
                    parms.Add(parameters[j]);

                var clone = (ITrendModel)TrendModels[i].Clone();
                clone.SetParameterValues(parms);
                trendModelsCopy.Add(clone);

                t += TrendModels[i].NumberOfParameters;
            }

            var result = new List<DataComponent>(seriesCount);

            // Compute pointwise log-likelihood components for each time step
            int numParameters = model.NumberOfParameters;
            var values = new double[numParameters];
            for (int i = 0; i < seriesCount; i++)
            {
                var data = fullTimeSeries[i];

                // Get parameters at this time step
                for (int j = 0; j < numParameters; j++)
                    values[j] = trendModelsCopy[j].Predict(data.Index);

                // Check if parameters are valid
                var valid = model.ValidateParameters(values, false);
                if (valid is not null)
                {
                    // Add invalid component based on data type
                    if (data is ExactData exact)
                        result.Add(new DataComponent(i, double.NegativeInfinity, exact.Value, DataComponentType.Exact, 1, data.Index.ToString()));
                    else if (data is UncertainData uncertain)
                        result.Add(new DataComponent(i, double.NegativeInfinity, uncertain.Distribution.Mean, DataComponentType.Uncertain, 1, data.Index.ToString()));
                    else if (data is IntervalData interval)
                        result.Add(new DataComponent(i, double.NegativeInfinity, (interval.LowerValue + interval.UpperValue) / 2.0, DataComponentType.Interval, 1, data.Index.ToString()));
                    else if (data is ThresholdData threshold)
                        result.Add(new DataComponent(i, double.NegativeInfinity, threshold.Value, DataComponentType.LeftCensored, threshold.NumberBelow + threshold.NumberAbove, $"Threshold"));
                    continue;
                }

                // Set parameters
                model.SetParameters(values);

                // Compute log-likelihood based on data type
                if (data is ExactData exactData)
                {
                    double logLH;
                    double value = exactData.Value;

                    if (!exactData.IsLowOutlier)
                    {
                        logLH = model.LogLikelihood(exactData.Value);
                    }
                    else
                    {
                        logLH = model.LogLikelihood_LeftCensored(lowOutlierThreshold, 1);
                        value = lowOutlierThreshold;
                    }

                    result.Add(new DataComponent(i, logLH, value, DataComponentType.Exact, 1, data.Index.ToString()));
                }
                else if (data is UncertainData uncertainData)
                {
                    var dist = uncertainData.Distribution;
                    double lowerProbability = 1E-8;
                    double upperProbability = 1.0 - 1E-8;
                    var a = dist.InverseCDF(lowerProbability);
                    var b = dist.InverseCDF(upperProbability);
                    double mass = upperProbability - lowerProbability;
                    double logLH = double.NegativeInfinity;
                    if (Tools.IsFinite(a) && Tools.IsFinite(b) && Tools.IsFinite(mass) && mass > 0.0 && a < b)
                    {
                        var ep = Integration.GaussLegendre20(q => dist.PDF(q) * model.PDF(q), a, b) / mass;
                        logLH = ep > 0 ? Math.Log(ep) : double.NegativeInfinity;
                    }
                    result.Add(new DataComponent(i, logLH, dist.Mean, DataComponentType.Uncertain, 1, data.Index.ToString()));
                }
                else if (data is IntervalData intervalData)
                {
                    double logLH = model.LogLikelihood_Intervals(intervalData.LowerValue, intervalData.UpperValue);
                    double midpoint = (intervalData.LowerValue + intervalData.UpperValue) / 2.0;

                    result.Add(new DataComponent(i, logLH, midpoint, DataComponentType.Interval, 1, data.Index.ToString()));
                }
                else if (data is ThresholdData thresholdData)
                {
                    double logLH = 0.0;
                    if (thresholdData.NumberBelow > 0)
                        logLH += model.LogLikelihood_LeftCensored(thresholdData.Value, thresholdData.NumberBelow);
                    if (thresholdData.NumberAbove > 0)
                        logLH += model.LogLikelihood_RightCensored(thresholdData.Value, thresholdData.NumberAbove);

                    int totalCount = thresholdData.NumberBelow + thresholdData.NumberAbove;
                    result.Add(new DataComponent(i, logLH, thresholdData.Value, DataComponentType.LeftCensored, totalCount, $"Threshold"));
                }
            }

            return result;
        }

        /// <summary>
        /// Returns the stationary pointwise log-likelihood for each observation.
        /// </summary>
        /// <param name="model">Working copy of the distribution.</param>
        /// <param name="parameters">Parameter vector.</param>
        /// <returns>Array of log-likelihood values, one per observation.</returns>
        private double[] StationaryPointwiseLogLikelihood(UnivariateDistributionBase model, double[] parameters)
        {
            if (DataFrame is null)
                throw new InvalidOperationException("DataFrame must be set before computing pointwise log likelihood.");

            // Check if proposed parameters are valid
            var valid = model.ValidateParameters(parameters, false);
            if (valid is not null)
            {
                // Return array of MinValue for invalid parameters
                int n = DataFrame.ExactSeries.Count + DataFrame.UncertainSeries.Count +
                        DataFrame.IntervalSeries.Count + DataFrame.ThresholdSeries.Count;
                var invalid = new double[n];
                for (int i = 0; i < n; i++) invalid[i] = double.NegativeInfinity;
                return invalid;
            }

            // Set model parameters
            model.SetParameters(parameters);

            var result = new List<double>();

            // Exact Data
            for (int i = 0; i < DataFrame.ExactSeries.Count; i++)
            {
                var data = (ExactData)DataFrame.ExactSeries[i];
                if (!data.IsLowOutlier)
                {
                    result.Add(model.LogLikelihood(data.Value));
                }
                else
                {
                    result.Add(model.LogLikelihood_LeftCensored(DataFrame.LowOutlierThreshold, 1));
                }
            }

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
                    var ep = Integration.GaussLegendre20(q => dist.PDF(q) * model.PDF(q), a, b) / mass;
                    result.Add(ep > 0 ? Math.Log(ep) : double.NegativeInfinity);
                }
                else
                {
                    result.Add(double.NegativeInfinity);
                }
            }

            // Interval Data
            for (int i = 0; i < DataFrame.IntervalSeries.Count; i++)
            {
                var data = (IntervalData)DataFrame.IntervalSeries[i];
                result.Add(model.LogLikelihood_Intervals(data.LowerValue, data.UpperValue));
            }

            // Threshold Data - each threshold contributes one observation
            for (int i = 0; i < DataFrame.ThresholdSeries.Count; i++)
            {
                var data = (ThresholdData)DataFrame.ThresholdSeries[i];
                double logLH = 0;
                if (data.NumberBelow > 0)
                    logLH += model.LogLikelihood_LeftCensored(data.Value, data.NumberBelow);
                if (data.NumberAbove > 0)
                    logLH += model.LogLikelihood_RightCensored(data.Value, data.NumberAbove);
                result.Add(logLH);
            }

            return result.ToArray();
        }

        /// <summary>
        /// Returns the nonstationary pointwise log-likelihood for each observation.
        /// </summary>
        /// <param name="model">Working copy of the distribution.</param>
        /// <param name="parameters">Parameter vector.</param>
        /// <returns>Array of log-likelihood values, one per observation.</returns>
        private double[] NonstationaryPointwiseLogLikelihood(UnivariateDistributionBase model, double[] parameters)
        {
            if (DataFrame is null)
                throw new InvalidOperationException("DataFrame must be set before computing pointwise log likelihood.");

            // See NonstationaryData_LogLikelihood for the rationale on caching FullTimeSeries.
            var fullTimeSeries = DataFrame.FullTimeSeries;
            if (fullTimeSeries is null || fullTimeSeries.Count == 0)
                throw new InvalidOperationException("FullTimeSeries is not populated for nonstationary log likelihood.");
            int seriesCount = fullTimeSeries.Count;
            double lowOutlierThreshold = DataFrame.LowOutlierThreshold;

            // Build trend models with proposed parameters
            int t = 0;
            var trendModelsCopy = new List<ITrendModel>();
            for (int i = 0; i < TrendModels.Count; i++)
            {
                var parms = new List<double>();
                for (int j = t; j < t + TrendModels[i].NumberOfParameters; j++)
                    parms.Add(parameters[j]);

                var clone = (ITrendModel)TrendModels[i].Clone();
                clone.SetParameterValues(parms);
                trendModelsCopy.Add(clone);

                t += TrendModels[i].NumberOfParameters;
            }

            var result = new double[seriesCount];

            // Compute pointwise log-likelihood for each time step
            int numParameters = model.NumberOfParameters;
            var values = new double[numParameters];
            for (int i = 0; i < seriesCount; i++)
            {
                var data = fullTimeSeries[i];

                // Get parameters at this time step
                for (int j = 0; j < numParameters; j++)
                    values[j] = trendModelsCopy[j].Predict(data.Index);

                // Check if parameters are valid
                var valid = model.ValidateParameters(values, false);
                if (valid is not null)
                {
                    result[i] = double.NegativeInfinity;
                    continue;
                }

                // Set parameters
                model.SetParameters(values);

                // Compute log-likelihood based on data type
                if (data is ExactData exact)
                {
                    if (!exact.IsLowOutlier)
                    {
                        result[i] = model.LogLikelihood(exact.Value);
                    }
                    else
                    {
                        result[i] = model.LogLikelihood_LeftCensored(lowOutlierThreshold, 1);
                    }
                }
                else if (data is UncertainData uncertain)
                {
                    var dist = uncertain.Distribution;
                    double lowerProbability = 1E-8;
                    double upperProbability = 1.0 - 1E-8;
                    var a = dist.InverseCDF(lowerProbability);
                    var b = dist.InverseCDF(upperProbability);
                    double mass = upperProbability - lowerProbability;
                    if (Tools.IsFinite(a) && Tools.IsFinite(b) && Tools.IsFinite(mass) && mass > 0.0 && a < b)
                    {
                        var ep = Integration.GaussLegendre20(q => dist.PDF(q) * model.PDF(q), a, b) / mass;
                        result[i] = ep > 0 ? Math.Log(ep) : double.NegativeInfinity;
                    }
                    else
                    {
                        result[i] = double.NegativeInfinity;
                    }
                }
                else if (data is IntervalData interval)
                {
                    result[i] = model.LogLikelihood_Intervals(interval.LowerValue, interval.UpperValue);
                }
                else if (data is ThresholdData threshold)
                {
                    double logLH = 0.0;
                    if (threshold.NumberBelow > 0)
                        logLH += model.LogLikelihood_LeftCensored(threshold.Value, threshold.NumberBelow);
                    if (threshold.NumberAbove > 0)
                        logLH += model.LogLikelihood_RightCensored(threshold.Value, threshold.NumberAbove);
                    result[i] = logLH;
                }
            }

            return result;
        }

        /// <summary>
        /// Returns the log likelihood contribution of parameter and quantile priors.
        /// </summary>
        /// <param name="model">Working copy of the distribution.</param>
        /// <param name="parameters">Parameter vector.</param>
        /// <returns>Log likelihood of all priors.</returns>
        public double Prior_LogLikelihood(UnivariateDistributionBase model, double[] parameters)
        {
            double logLH = 0.0;

            // Parameter priors
            for (int i = 0; i < Parameters.Count; i++)
            {
                logLH += Parameters[i].PriorDistribution.LogPDF(parameters[i]);
            }

            if (UseJeffreysRuleForScale)
            {
                double scaleParam;
                if (model.Type == UnivariateDistributionType.GammaDistribution ||
                    model.Type == UnivariateDistributionType.Weibull)
                {
                    scaleParam = model.GetParameters[0];
                }
                else
                {
                    scaleParam = model.GetParameters[1];
                }
                // Jeffreys prior requires positive scale parameter. Return -Inf directly
                // rather than subtracting +Inf (mathematically equivalent today because of the
                // outer Tools.IsFinite collapse, but the early-return is more robust to future
                // refactors that might add positive terms after this one). Mirrors the
                // pointwise-variant pattern at line ~1865.
                if (scaleParam <= 0) return double.NegativeInfinity;
                logLH -= Math.Log(scaleParam);
            }

            // Quantile priors
            if (EnableQuantilePriors && UseSingleQuantile && _quantilePriorsTrue.Count == 1)
            {
                logLH += _quantilePriorsTrue[0].Distribution.LogPDF(model.InverseCDF(1 - _quantilePriorsTrue[0].Alpha));
            }
            else if (EnableQuantilePriors && !UseSingleQuantile && _quantilePriorsTrue.Count == Distribution.NumberOfParameters)
            {
                logLH += _quantilePriorsTrue[0].Distribution.LogPDF(model.InverseCDF(1 - _quantilePriorsTrue[0].Alpha));

                var pVals = new double[_quantilePriorsTrue.Count];
                pVals[0] = 1 - _quantilePriorsTrue[0].Alpha;

                for (int i = 1; i < _quantilePriorsTrue.Count; i++)
                {
                    pVals[i] = 1 - _quantilePriorsTrue[i].Alpha;
                    double qCurr = model.InverseCDF(1 - _quantilePriorsTrue[i].Alpha);
                    double qPrev = model.InverseCDF(1 - _quantilePriorsTrue[i - 1].Alpha);
                    logLH += _quantilePriorsTrue[i].Distribution.LogPDF(qCurr - qPrev);
                }

                ((IStandardError)model).QuantileJacobian(pVals, out var D);
                logLH += D != 0 ? Math.Log(Math.Abs(D)) : double.NegativeInfinity;
            }

            if (!Tools.IsFinite(logLH)) return double.NegativeInfinity;
            return logLH;
        }

        /// <inheritdoc/>
        public override List<PriorComponent> PointwisePriorLogLikelihood(double[] parameters)
        {
            if (Distribution is null)
                throw new InvalidOperationException("Distribution must be set before computing pointwise prior log likelihood.");

            var result = new List<PriorComponent>();
            var model = Distribution.Clone();

            // Check if proposed parameters are valid for the distribution.
            // Priors are evaluated at the last time step (matches LogLikelihood);
            // ParameterTimeIndex is reserved for prediction.
            double[] distParams;
            if (IsNonstationary)
            {
                if (DataFrame is null || DataFrame.FullTimeSeries.Count == 0)
                    return result;
                distParams = GetParameterValues(DataFrame.FullTimeSeries.Last().Index);
            }
            else
            {
                distParams = parameters.Take(model.NumberOfParameters).ToArray();
            }
            var valid = model.ValidateParameters(distParams, false);
            if (valid is null)
                model.SetParameters(distParams);

            // Parameter priors
            for (int i = 0; i < Parameters.Count; i++)
            {
                double ll = Parameters[i].PriorDistribution.LogPDF(parameters[i]);
                string paramName = string.IsNullOrEmpty(Parameters[i].OwnerName) ? Parameters[i].Name : Parameters[i].OwnerName;
                result.Add(new PriorComponent($"Parameter Prior: {paramName}", ll, PriorComponentType.ParameterPrior));
            }

            // Jeffreys rule for scale parameter
            if (UseJeffreysRuleForScale && valid is null)
            {
                double scale;
                string scaleName;
                if (model.Type == UnivariateDistributionType.GammaDistribution ||
                    model.Type == UnivariateDistributionType.Weibull)
                {
                    scale = model.GetParameters[0];
                    scaleName = model.ParameterNames[0];
                }
                else
                {
                    scale = model.GetParameters[1];
                    scaleName = model.ParameterNames[1];
                }
                // Jeffreys prior 1/σ requires positive scale; mirror the scalar guard
                // in Prior_LogLikelihood so the Pointwise sum stays consistent.
                double ll = scale > 0 ? -Math.Log(scale) : double.NegativeInfinity;
                result.Add(new PriorComponent($"Jeffreys Scale: {scaleName}", ll, PriorComponentType.JeffreysScalePrior));
            }

            // Quantile priors
            if (EnableQuantilePriors && valid is null)
            {
                if (UseSingleQuantile && _quantilePriorsTrue.Count == 1)
                {
                    double quantile = model.InverseCDF(1 - _quantilePriorsTrue[0].Alpha);
                    double ll = _quantilePriorsTrue[0].Distribution.LogPDF(quantile);
                    result.Add(new PriorComponent($"Quantile Prior: p={_quantilePriorsTrue[0].Alpha:G4}", ll, PriorComponentType.QuantilePrior));
                }
                else if (!UseSingleQuantile && _quantilePriorsTrue.Count == Distribution.NumberOfParameters)
                {
                    // First quantile prior
                    double q0 = model.InverseCDF(1 - _quantilePriorsTrue[0].Alpha);
                    double ll0 = _quantilePriorsTrue[0].Distribution.LogPDF(q0);
                    result.Add(new PriorComponent($"Quantile Prior: p={_quantilePriorsTrue[0].Alpha:G4}", ll0, PriorComponentType.QuantilePrior));

                    // Remaining quantile priors (on differences)
                    var pVals = new double[_quantilePriorsTrue.Count];
                    pVals[0] = 1 - _quantilePriorsTrue[0].Alpha;

                    for (int i = 1; i < _quantilePriorsTrue.Count; i++)
                    {
                        pVals[i] = 1 - _quantilePriorsTrue[i].Alpha;
                        double qCurr = model.InverseCDF(1 - _quantilePriorsTrue[i].Alpha);
                        double qPrev = model.InverseCDF(1 - _quantilePriorsTrue[i - 1].Alpha);
                        double ll = _quantilePriorsTrue[i].Distribution.LogPDF(qCurr - qPrev);
                        result.Add(new PriorComponent($"Quantile Prior: p={_quantilePriorsTrue[i].Alpha:G4} (diff)", ll, PriorComponentType.QuantilePrior));
                    }

                    // Jacobian term
                    ((IStandardError)model).QuantileJacobian(pVals, out var D);
                    double jacobianLL = D != 0 ? Math.Log(Math.Abs(D)) : double.NegativeInfinity;
                    result.Add(new PriorComponent("Quantile Jacobian", jacobianLL, PriorComponentType.Jacobian));
                }
            }

            return result;
        }

        /// <inheritdoc/>
        public override void SetParameterValues(IList<double> parameters)
        {
            if (parameters == null) throw new ArgumentNullException(nameof(parameters));
            if (parameters.Count != NumberOfParameters)
                throw new ArgumentException("The length of the parameter list is incorrect.", nameof(parameters));

            for (int i = 0; i < parameters.Count; i++)
                Parameters[i].Value = parameters[i];

            int t = 0;
            for (int i = 0; i < TrendModels.Count; i++)
            {
                var parms = new List<double>();
                for (int j = t; j < t + TrendModels[i].NumberOfParameters; j++)
                    parms.Add(parameters[j]);

                TrendModels[i].SetParameterValues(parms);
                t += TrendModels[i].NumberOfParameters;
            }

            SetDistributionParameterValues(ParameterTimeIndex);
        }

        /// <summary>
        /// Returns the distribution parameters for a specific time step index.
        /// </summary>
        /// <param name="index">The time step index.</param>
        /// <returns>Array of distribution parameter values at the specified index.</returns>
        public double[] GetParameterValues(int index)
        {
            var values = new double[Distribution.NumberOfParameters];
            for (int i = 0; i < Distribution.NumberOfParameters; i++)
            {
                values[i] = TrendModels[i].Predict(index);
            }
            return values;
        }

        /// <summary>
        /// Sets the distribution parameters for a specific time step index.
        /// </summary>
        /// <param name="index">The time step index.</param>
        public void SetDistributionParameterValues(int index)
        {
            Distribution.SetParameters(GetParameterValues(index));
        }

        /// <summary>
        /// Returns nonstationary return level (quantile) values based on
        /// <see cref="Alpha"/> for each time index between the first and the
        /// maximum of <see cref="ParameterTimeIndex"/> and the last index in
        /// <see cref="DataFrame.FullTimeSeries"/>.
        /// </summary>
        /// <returns>
        /// Array of return level values, or <c>null</c> if the model is stationary
        /// or the required time series is not available.
        /// </returns>
        public double[]? GetNonstationaryReturnLevel()
        {
            if (!IsNonstationary || DataFrame is null ||
                DataFrame.FullTimeSeries is null || DataFrame.FullTimeSeries.Count == 0)
            {
                return null;
            }

            var dist = Distribution.Clone();
            var indices = Tools.Sequence(
                DataFrame.FullTimeSeries.First().Index,
                Math.Max(ParameterTimeIndex, DataFrame.FullTimeSeries.Last().Index));

            var result = new double[indices.Length];
            for (int i = 0; i < indices.Length; i++)
            {
                dist.SetParameters(GetParameterValues(indices[i]));
                result[i] = dist.InverseCDF(1 - Alpha);
            }
            return result;
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

            var trends = new List<ITrendModel>();
            for (int i = 0; i < TrendModels.Count; i++)
                trends.Add((ITrendModel)TrendModels[i].Clone());

            var result = new UnivariateDistribution(DataFrame, Distribution)
            {
                _useDefaultFlatPriors = UseDefaultFlatPriors,
                _useJeffreysRuleForScale = UseJeffreysRuleForScale,
                _enableQuantilePriors = EnableQuantilePriors,
                _useSingleQuantile = UseSingleQuantile,
                _isNonstationary = IsNonstationary,
                _parameterTimeIndex = ParameterTimeIndex,
                _alpha = Alpha,
                Parameters = parms,
                QuantilePriors = quants,
                TrendModels = trends
            };

            result.ProcessQuantilePriors();
            return result;
        }

        /// <inheritdoc/>
        public override XElement ToXElement()
        {
            var result = new XElement(nameof(UnivariateDistribution));

            // Distribution
            result.Add(Distribution.ToXElement());

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

            // Nonstationary
            var trends = new XElement(nameof(TrendModels));
            foreach (var t in TrendModels)
                trends.Add(t.ToXElement());
            result.Add(trends);
            result.SetAttributeValue(nameof(IsNonstationary), IsNonstationary.ToString());
            result.SetAttributeValue(nameof(ParameterTimeIndex), ParameterTimeIndex.ToString(CultureInfo.InvariantCulture));
            result.SetAttributeValue(nameof(Alpha), Alpha.ToString("G17", CultureInfo.InvariantCulture));

            return result;
        }

        /// <inheritdoc/>
        public override (bool IsValid, List<string> ValidationMessages) Validate()
        {
            bool isValid = true;
            var messages = new List<string>();

            // DataFrame checks
            if (DataFrame is null)
            {
                isValid = false;
                messages.Add("Error: The input DataFrame is null.");
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
                messages.Add("Error: The parent distribution is null.");
            }

            // Distribution type check
            if (Distribution is not null && !IsSupportedDistributionType(DistributionType))
            {
                isValid = false;
                messages.Add($"Error: Distribution type '{DistributionType}' is not supported.");
            }

            // Validate uncertain-data quadrature bounds locally. The likelihood normalizes
            // each ME integral by the retained probability mass in the 1E-8 tail window,
            // so invalid bounds or zero mass must be caught before fitting.
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

            // Log distribution support checks
            if (Distribution is not null &&
                (DistributionType == UnivariateDistributionType.LnNormal ||
                 DistributionType == UnivariateDistributionType.LogNormal ||
                 DistributionType == UnivariateDistributionType.LogPearsonTypeIII))
            {
                bool nonPositiveExact =
                    DataFrame.ExactSeries is not null &&
                    DataFrame.ExactSeries.Count > 0 &&
                    DataFrame.ExactSeries
                        .Where(x => !((ExactData)x).IsLowOutlier)
                        .Any(y => y.Value <= 0.0);

                bool nonPositiveUncertain = false;
                if (DataFrame.UncertainSeries is not null)
                {
                    foreach (UncertainData data in DataFrame.UncertainSeries)
                    {
                        var dist = data.Distribution;
                        double lowerProbability = 1E-8;
                        double lower = dist.InverseCDF(lowerProbability);

                        // Log likelihoods evaluate model density only on positive data. A positive
                        // ME mean is not enough; the retained 1E-8 lower support must be positive.
                        if (!Tools.IsFinite(lower) || lower <= 0.0)
                        {
                            nonPositiveUncertain = true;
                            break;
                        }
                    }
                }

                if (nonPositiveExact || nonPositiveUncertain)
                {
                    isValid = false;
                    messages.Add("Error: Log based distributions cannot be used because some data values are non positive.");
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

            // Nonstationary checks
            if (IsNonstationary)
            {
                if (DataFrame.FullTimeSeries is not null && DataFrame.FullTimeSeries.Count > 0)
                {
                    int firstIndex = DataFrame.FullTimeSeries.First().Index;
                    int lastIndex = DataFrame.FullTimeSeries.Last().Index;

                    if (ParameterTimeIndex < firstIndex || ParameterTimeIndex > lastIndex + 100)
                    {
                        isValid = false;
                        messages.Add("Error: ParameterTimeIndex is outside the valid range for the full time series.");
                    }
                }

                if (Alpha <= 0.0 || Alpha >= 1.0)
                {
                    isValid = false;
                    messages.Add("Error: Alpha must be strictly between 0 and 1 for nonstationary analysis.");
                }
            }

            return (isValid, messages);
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Generates random samples from the univariate distribution using the inverse CDF method.
        /// The distribution's current parameters are used for generation.
        /// </remarks>
        public double[] GenerateRandomValues(int sampleSize, int seed = -1)
        {
            if (sampleSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(sampleSize), "Sample size must be positive.");
            if (Distribution is null)
                throw new InvalidOperationException("Distribution cannot be null when generating random values.");

            return Distribution.GenerateRandomValues(sampleSize, seed);

        }

        #endregion

    }
}
