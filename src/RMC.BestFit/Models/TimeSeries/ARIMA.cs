using Numerics.Distributions;
using Numerics.Data;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using Numerics;
using Numerics.Data.Statistics;
using System.Xml.Linq;
using System.Globalization;

namespace RMC.BestFit.Models
{
    /// <summary>
    /// AutoRegressive Integrated Moving Average (ARIMA) time-series model with support for
    /// data transformations, differencing, training/forecasting splits, and Bayesian estimation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The ARIMA(p,d,q) model applies d-order differencing before fitting an ARMA(p,q) model:
    /// Δ^d Y(t) = μ + φ1*(Δ^d Y(t-1) - μ) + ... + φp*(Δ^d Y(t-p) - μ) + ε(t) + θ1*ε(t-1) + ... + θq*ε(t-q)
    /// where ε(t) ~ N(0, σ²)
    /// </para>
    /// <para>
    /// Features include:
    /// <list type="bullet">
    /// <item><description>Data transformations: logarithmic, Box-Cox, Yeo-Johnson</description></item>
    /// <item><description>Differencing of order d for non-stationary series</description></item>
    /// <item><description>Training/forecasting time step splits for out-of-sample validation</description></item>
    /// <item><description>Jeffreys' rule prior for scale parameter</description></item>
    /// </list>
    /// </para>
    /// <para>
    ///     <b>Authors:</b>
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// </remarks>
    public class ARIMA : ModelBase, ISimulatable<double[]>
    {
        #region Construction

        /// <summary>
        /// Constructs an empty ARIMA model with default orders (1,0,0).
        /// </summary>
        public ARIMA()
        {
            _pOrder = 1;
            _dOrder = 0;
            _qOrder = 0;
            _includeIntercept = true;
            SetDefaultParameters();
        }

        /// <summary>
        /// Constructs an ARIMA model with specified parameters.
        /// </summary>
        /// <param name="timeSeries">The time-series data to model.</param>
        /// <param name="pOrder">The AR order (p). Default = 1.</param>
        /// <param name="dOrder">The differencing order (d). Default = 0.</param>
        /// <param name="qOrder">The MA order (q). Default = 0.</param>
        /// <param name="includeIntercept">Whether to include an intercept term. Default = true.</param>
        public ARIMA(TimeSeries timeSeries, int pOrder = 1, int dOrder = 0, int qOrder = 0, bool includeIntercept = true)
        {
            _pOrder = pOrder;
            _dOrder = dOrder;
            _qOrder = qOrder;
            _includeIntercept = includeIntercept;
            TimeSeries = timeSeries;
        }

        /// <summary>
        /// Constructs an ARIMA model by deserializing from XML.
        /// </summary>
        /// <param name="timeSeries">The time-series data to model.</param>
        /// <param name="xElement">The XML element to deserialize.</param>
        public ARIMA(TimeSeries timeSeries, XElement xElement)
        {
            var pOrderAttr = xElement.Attribute(nameof(POrder));
            if (pOrderAttr != null)
                int.TryParse(pOrderAttr.Value, out _pOrder);
            var dOrderAttr = xElement.Attribute(nameof(DOrder));
            if (dOrderAttr != null)
                int.TryParse(dOrderAttr.Value, out _dOrder);
            var qOrderAttr = xElement.Attribute(nameof(QOrder));
            if (qOrderAttr != null)
                int.TryParse(qOrderAttr.Value, out _qOrder);
            var interceptAttr = xElement.Attribute(nameof(IncludeIntercept));
            if (interceptAttr != null)
                bool.TryParse(interceptAttr.Value, out _includeIntercept);
            var flatPriorsAttr = xElement.Attribute(nameof(UseDefaultFlatPriors));
            if (flatPriorsAttr != null)
                bool.TryParse(flatPriorsAttr.Value, out _useDefaultFlatPriors);
            var jeffreysAttr = xElement.Attribute(nameof(UseJeffreysRuleForScale));
            if (jeffreysAttr != null)
                bool.TryParse(jeffreysAttr.Value, out _useJeffreysRuleForScale);
            var transformAttr = xElement.Attribute(nameof(TransformType));
            if (transformAttr != null)
                Enum.TryParse(transformAttr.Value, out _transformType);
            var trainingStepsAttr = xElement.Attribute(nameof(TrainingTimeSteps));
            if (trainingStepsAttr != null)
                int.TryParse(trainingStepsAttr.Value, out _trainingTimeSteps);
            var useDefaultTrainingAttr = xElement.Attribute(nameof(UseDefaultTrainingSteps));
            if (useDefaultTrainingAttr != null)
                bool.TryParse(useDefaultTrainingAttr.Value, out _useDefaultTrainingSteps);

            // Set TimeSeries first (this may trigger SetDefaultParameters)
            TimeSeries = timeSeries;
            if (trainingStepsAttr != null)
                int.TryParse(trainingStepsAttr.Value, out _trainingTimeSteps);
            if (useDefaultTrainingAttr != null)
                bool.TryParse(useDefaultTrainingAttr.Value, out _useDefaultTrainingSteps);
            SetTrainingData();

            // Then restore parameters from XElement to override defaults
            var parmsElement = xElement.Element(nameof(Parameters));
            if (parmsElement != null)
            {
                var parms = new List<ModelParameter>();
                foreach (XElement p in parmsElement.Elements(nameof(ModelParameter)))
                    parms.Add(new ModelParameter(p));
                Parameters = parms;
            }
        }

        #endregion

        #region Members

        private TimeSeries _timeSeries = null!;
        private TimeSeries _trainingTimeSeries = null!;
        private TimeSeries _diffSeries = null!;
        private int _pOrder = 1;
        private int _dOrder = 0;
        private int _qOrder = 0;
        private bool _includeIntercept = true;

        // Transform members
        private Transform _transformType = Transform.None;
        private double _lambda = 0;
        private double _lambda2 = 0;
        private double _logJacobian = 0;

        // Training/forecasting split members
        private int _trainingTimeSteps = 0;
        private bool _useDefaultTrainingSteps = true;
        private bool _useJeffreysRuleForScale = true;

        /// <summary>
        /// Gets or sets the time series data.
        /// </summary>
        [Category("Inputs")]
        [DisplayName("Time Series Data")]
        [Description("The time series data to model.")]
        [Browsable(true)]
        public TimeSeries TimeSeries
        {
            get { return _timeSeries; }
            set
            {
                if (ReferenceEquals(_timeSeries, value)) return;

                if (_timeSeries != null)
                    _timeSeries.CollectionChanged -= TimeSeries_CollectionChanged;

                _timeSeries = value;

                if (_timeSeries != null)
                {
                    _timeSeries.CollectionChanged += TimeSeries_CollectionChanged;
                    ResetDefaultTrainingStepsForNewTimeSeries();
                    SetTrainingData();
                }
                else
                {
                    ResetDefaultTrainingStepsForNewTimeSeries();
                }

                RaisePropertyChange(nameof(TimeSeries));
                if (_timeSeries != null && UseDefaultFlatPriors)
                    SetDefaultParameters();
            }
        }

        /// <summary>
        /// Gets or sets the AR order (p).
        /// </summary>
        [Category("Inputs")]
        [DisplayName("AR Order (p)")]
        [Description("The number of lagged values used in the autoregression.")]
        [Browsable(true)]
        public int POrder
        {
            get { return _pOrder; }
            set
            {
                if (_pOrder != value)
                {
                    _pOrder = value;
                    RaisePropertyChange(nameof(POrder));
                    SetDefaultParameters();
                }
            }
        }

        /// <summary>
        /// Gets or sets the differencing order (d).
        /// </summary>
        [Category("Inputs")]
        [DisplayName("Differencing Order (d)")]
        [Description("The order of differencing applied to achieve stationarity.")]
        [Browsable(true)]
        public int DOrder
        {
            get { return _dOrder; }
            set
            {
                if (_dOrder != value)
                {
                    _dOrder = value;
                    SetTrainingData();
                    RaisePropertyChange(nameof(DOrder));
                    SetDefaultParameters();
                }
            }
        }

        /// <summary>
        /// Gets or sets the MA order (q).
        /// </summary>
        [Category("Inputs")]
        [DisplayName("MA Order (q)")]
        [Description("The number of lagged error terms used in the moving average.")]
        [Browsable(true)]
        public int QOrder
        {
            get { return _qOrder; }
            set
            {
                if (_qOrder != value)
                {
                    _qOrder = value;
                    RaisePropertyChange(nameof(QOrder));
                    SetDefaultParameters();
                }
            }
        }

        /// <summary>
        /// Gets or sets whether to include an intercept.
        /// </summary>
        [Category("Inputs")]
        [DisplayName("Include Intercept")]
        [Description("Determines whether to include an intercept term.")]
        [Browsable(true)]
        public bool IncludeIntercept
        {
            get { return _includeIntercept; }
            set
            {
                if (_includeIntercept != value)
                {
                    _includeIntercept = value;
                    RaisePropertyChange(nameof(IncludeIntercept));
                    SetDefaultParameters();
                }
            }
        }

        /// <summary>
        /// Gets or sets the data transformation type applied before modeling.
        /// </summary>
        [Category("Inputs")]
        [DisplayName("Transform Type")]
        [Description("Specifies the data transform type (None, Logarithmic, Box-Cox, or Yeo-Johnson).")]
        [Browsable(true)]
        public Transform TransformType
        {
            get { return _transformType; }
            set
            {
                if (_transformType != value)
                {
                    _transformType = value;
                    SetTrainingData();
                    RaisePropertyChange(nameof(TransformType));
                    SetDefaultParameters();
                }
            }
        }

        /// <summary>
        /// Gets the transformed time series used for model calibration.
        /// </summary>
        public TimeSeries TrainingTimeSeries => _trainingTimeSeries;

        /// <summary>
        /// Gets the differenced series after transformation.
        /// </summary>
        public TimeSeries DifferencedSeries => _diffSeries;

        /// <summary>
        /// Gets or sets the number of time steps used for training.
        /// </summary>
        [Category("Inputs")]
        [DisplayName("Training Time Steps")]
        [Description("The number of time steps used for training. Training begins at the start of the time series.")]
        [Browsable(true)]
        public int TrainingTimeSteps
        {
            get { return _trainingTimeSteps; }
            set
            {
                if (_trainingTimeSteps != value)
                {
                    _trainingTimeSteps = value;
                    SetTrainingData();
                    RaisePropertyChange(nameof(TrainingTimeSteps));
                    if (UseDefaultFlatPriors)
                        SetDefaultParameters();
                }
            }
        }

        /// <summary>
        /// Gets or sets whether to use default training steps (80% of data).
        /// </summary>
        [Category("Inputs")]
        [DisplayName("Use Default Training Steps")]
        [Description("If true, uses 80% of data for training. If false, uses TrainingTimeSteps value.")]
        [Browsable(true)]
        public bool UseDefaultTrainingSteps
        {
            get { return _useDefaultTrainingSteps; }
            set
            {
                if (_useDefaultTrainingSteps != value)
                {
                    _useDefaultTrainingSteps = value;
                    if (_useDefaultTrainingSteps && _timeSeries != null)
                        SetDefaultTrainingSteps();
                    RaisePropertyChange(nameof(UseDefaultTrainingSteps));
                }
            }
        }

        /// <summary>
        /// Gets the number of time steps reserved for forecasting validation.
        /// </summary>
        [Category("General")]
        [DisplayName("Forecasting Time Steps")]
        [Description("The number of time steps reserved for out-of-sample forecasting.")]
        [Browsable(true)]
        public int ForecastingTimeSteps => TimeSeries != null ? TimeSeries.Count - TrainingTimeSteps : 0;

        /// <summary>
        /// Gets or sets whether to use Jeffreys' rule for the scale parameter prior.
        /// </summary>
        [Category("Bayesian")]
        [DisplayName("Use Jeffreys Rule for Scale")]
        [Description("If true, applies Jeffreys' prior (1/σ) to the scale parameter.")]
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

        #region Private Methods

        /// <summary>
        /// Handles changes to the time series collection.
        /// </summary>
        private void TimeSeries_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (_useDefaultTrainingSteps)
                SetDefaultTrainingSteps();
            SetTrainingData();
            if (UseDefaultFlatPriors)
                SetDefaultParameters();
        }

        /// <summary>
        /// Sets the default training steps to 80% of the data.
        /// </summary>
        private void SetDefaultTrainingSteps()
        {
            if (_timeSeries == null || _timeSeries.Count == 0) return;

            int minSteps = Math.Max(30, Parameters?.Count ?? 0);
            TrainingTimeSteps = Math.Max(minSteps, (int)Math.Floor(0.8 * _timeSeries.Count));
        }

        /// <summary>
        /// Restores the default training split when the model is attached to a different input series.
        /// </summary>
        /// <remarks>
        /// A new response series represents a new calibration problem, so manual training-window edits
        /// from the previous series are discarded.
        /// </remarks>
        private void ResetDefaultTrainingStepsForNewTimeSeries()
        {
            if (!_useDefaultTrainingSteps)
            {
                _useDefaultTrainingSteps = true;
                RaisePropertyChange(nameof(UseDefaultTrainingSteps));
            }

            if (_timeSeries == null || _timeSeries.Count == 0)
            {
                if (_trainingTimeSteps != 0)
                {
                    _trainingTimeSteps = 0;
                    RaisePropertyChange(nameof(TrainingTimeSteps));
                }
                return;
            }

            SetDefaultTrainingSteps();
        }

        /// <summary>
        /// Applies differencing to the series.
        /// </summary>
        private TimeSeries Difference(TimeSeries series, int order)
        {
            if (order <= 0) return series;

            var result = series;
            for (int d = 0; d < order; d++)
            {
                var diffed = new TimeSeries(result.TimeInterval);
                for (int i = 1; i < result.Count; i++)
                {
                    var ord = result[i].Clone();
                    ord.Value = result[i].Value - result[i - 1].Value;
                    diffed.Add(ord);
                }
                result = diffed;
            }
            return result;
        }

        /// <summary>
        /// Prepares the training data by applying transformations and differencing.
        /// </summary>
        private void SetTrainingData()
        {
            if (TimeSeries == null || TrainingTimeSteps == 0) return;

            int effectiveTrainingSteps = Math.Min(TrainingTimeSteps, TimeSeries.Count);

            // First, create transformed series
            var transformedSeries = new TimeSeries(TimeSeries.TimeInterval);

            if (TransformType == Transform.None)
            {
                _lambda = 0;
                _logJacobian = 0;
                for (int i = 0; i < effectiveTrainingSteps; i++)
                {
                    transformedSeries.Add(TimeSeries[i].Clone());
                }
            }
            else if (TransformType == Transform.Logarithmic)
            {
                _lambda = 0;
                int maxOrder = Math.Max(POrder, QOrder);
                int startIdx = Math.Max(0, DOrder + maxOrder);
                int endIdx = effectiveTrainingSteps - 1;
                if (endIdx >= startIdx)
                {
                    var data = TimeSeries.ValuesToArray().Subset(startIdx, endIdx);
                    _logJacobian = BoxCox.LogJacobian(data, _lambda);
                }

                for (int i = 0; i < effectiveTrainingSteps; i++)
                {
                    var ord = TimeSeries[i].Clone();
                    ord.Value = BoxCox.Transform(TimeSeries[i].Value, _lambda);
                    transformedSeries.Add(ord);
                }
            }
            else if (TransformType == Transform.BoxCox)
            {
                BoxCox.FitLambda(TimeSeries.ValuesToList(), out _lambda);
                int maxOrder = Math.Max(POrder, QOrder);
                int startIdx = Math.Max(0, DOrder + maxOrder);
                int endIdx = effectiveTrainingSteps - 1;
                if (endIdx >= startIdx)
                {
                    var data = TimeSeries.ValuesToArray().Subset(startIdx, endIdx);
                    _logJacobian = BoxCox.LogJacobian(data, _lambda);
                }

                for (int i = 0; i < effectiveTrainingSteps; i++)
                {
                    var ord = TimeSeries[i].Clone();
                    ord.Value = BoxCox.Transform(TimeSeries[i].Value, _lambda);
                    transformedSeries.Add(ord);
                }
            }
            else if (TransformType == Transform.YeoJohnson)
            {
                YeoJohnson.FitLambda(TimeSeries.ValuesToList(), out _lambda);
                int maxOrder = Math.Max(POrder, QOrder);
                int startIdx = Math.Max(0, DOrder + maxOrder);
                int endIdx = effectiveTrainingSteps - 1;
                if (endIdx >= startIdx)
                {
                    var data = TimeSeries.ValuesToArray().Subset(startIdx, endIdx);
                    _logJacobian = YeoJohnson.LogJacobian(data, _lambda);
                }

                for (int i = 0; i < effectiveTrainingSteps; i++)
                {
                    var ord = TimeSeries[i].Clone();
                    ord.Value = YeoJohnson.Transform(TimeSeries[i].Value, _lambda);
                    transformedSeries.Add(ord);
                }
            }

            _trainingTimeSeries = transformedSeries;

            // Apply differencing
            _diffSeries = Difference(transformedSeries, DOrder);
        }

        #endregion

        #region Methods

        /// <summary>
        /// Sets the transformation parameters manually.
        /// </summary>
        /// <param name="lambda1">The primary transformation parameter.</param>
        /// <param name="lambda2">The offset for handling non-positive values.</param>
        public void SetTransformParameters(double lambda1 = 0, double lambda2 = 0)
        {
            _lambda = lambda1;
            _lambda2 = lambda2;
        }

        /// <inheritdoc/>
        public override void SetDefaultParameters()
        {
            if (Parameters.Count > 0)
            {
                for (int i = 0; i < Parameters.Count; i++)
                    Parameters[i].PropertyChanged -= Parameter_PropertyChanged;
            }

            _parameters = new List<ModelParameter>();

            double mean = 0, sigma = 1, min = -10, max = 10, sigmaUB = 10;

            if (_diffSeries != null && _diffSeries.Count > 0)
            {
                mean = _diffSeries.MeanValue();
                sigma = _diffSeries.StandardDeviation();

                double tempMin = Math.Sign(mean) * Math.Pow(10, Math.Floor(Math.Log10(Math.Abs(mean)) - 1));
                double tempMax = Math.Sign(mean) * Math.Pow(10, Math.Ceiling(Math.Log10(Math.Abs(mean)) + 1));
                min = Math.Min(tempMin, tempMax);
                max = Math.Max(tempMin, tempMax);

                if (double.IsNaN(min) || double.IsInfinity(min)) min = -1000;
                if (double.IsNaN(max) || double.IsInfinity(max)) max = 1000;
                if (min >= max) { min = mean - 100; max = mean + 100; }

                sigmaUB = Math.Pow(10, Math.Ceiling(Math.Log10(sigma) + 1));
                if (double.IsNaN(sigmaUB) || double.IsInfinity(sigmaUB)) sigmaUB = 100;
            }

            if (IncludeIntercept)
            {
                Parameters.Add(new ModelParameter()
                {
                    Name = "Intercept (μ)",
                    Value = mean,
                    LowerBound = min,
                    UpperBound = max,
                    PriorDistribution = new Uniform(min, max)
                });
            }

            for (int i = 1; i <= POrder; i++)
            {
                Parameters.Add(new ModelParameter()
                {
                    Name = "AR (φ" + SubscriptFormatter.ToSubscript(i) + ")",
                    Value = 0,
                    LowerBound = -2,
                    UpperBound = 2,
                    PriorDistribution = new Uniform(-2, 2)
                });
            }

            for (int i = 1; i <= QOrder; i++)
            {
                Parameters.Add(new ModelParameter()
                {
                    Name = "MA (θ" + SubscriptFormatter.ToSubscript(i) + ")",
                    Value = 0,
                    LowerBound = -2,
                    UpperBound = 2,
                    PriorDistribution = new Uniform(-2, 2)
                });
            }

            Parameters.Add(new ModelParameter()
            {
                Name = "Scale (σ)",
                Value = sigma,
                LowerBound = Tools.DoubleMachineEpsilon,
                UpperBound = sigmaUB,
                IsPositive = true,
                PriorDistribution = new Uniform(Tools.DoubleMachineEpsilon, sigmaUB)
            });

            for (int i = 0; i < Parameters.Count; i++)
                Parameters[i].PropertyChanged += Parameter_PropertyChanged;

            RaisePropertyChange(nameof(SetDefaultParameters));
        }

        /// <inheritdoc/>
        public override void SetParameterValues(IList<double> parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));
            if (parameters.Count != Parameters.Count)
                throw new ArgumentException("Parameter count mismatch.", nameof(parameters));

            for (int i = 0; i < Parameters.Count; i++)
                Parameters[i].Value = parameters[i];
        }

        /// <summary>
        /// Computes the model residuals for given parameters.
        /// </summary>
        /// <param name="parameters">The parameter vector for the model.</param>
        /// <returns>Array of residuals on the differenced/transformed scale.</returns>
        public double[] Residuals(double[] parameters)
        {
            int effectiveTrainingSteps = _diffSeries != null
                ? Math.Min(TrainingTimeSteps - DOrder, _diffSeries.Count)
                : TrainingTimeSteps;

            var residuals = new double[effectiveTrainingSteps];
            var epsilon = new double[effectiveTrainingSteps];
            int maxOrder = Math.Max(POrder, QOrder);

            int k = 0;
            double mu = IncludeIntercept ? parameters[k++] : 0;
            var phi = new double[POrder];
            for (int i = 0; i < POrder; i++)
                phi[i] = parameters[k++];
            var theta = new double[QOrder];
            for (int i = 0; i < QOrder; i++)
                theta[i] = parameters[k++];

            for (int t = 0; t < effectiveTrainingSteps; t++)
            {
                if (t < maxOrder)
                {
                    residuals[t] = 0;
                    epsilon[t] = 0;
                }
                else
                {
                    double prediction = mu;

                    // AR component
                    for (int p = 1; p <= POrder; p++)
                    {
                        prediction += phi[p - 1] * (_diffSeries![t - p].Value - mu);
                    }

                    // MA component
                    for (int q = 1; q <= Math.Min(t, QOrder); q++)
                    {
                        prediction += theta[q - 1] * epsilon[t - q];
                    }

                    epsilon[t] = _diffSeries![t].Value - prediction;
                    residuals[t] = epsilon[t];
                }
            }

            return residuals;
        }

        /// <inheritdoc/>
        public override double DataLogLikelihood(double[] parameters)
        {
            if (_diffSeries == null || _diffSeries.Count == 0)
                return double.NegativeInfinity;

            for (int i = 0; i < parameters.Length; i++)
            {
                if (double.IsNaN(parameters[i]))
                    return double.NegativeInfinity;
            }

            var residuals = Residuals(parameters);
            double sigma = parameters.Last();
            // Guard against non-positive sigma — Numerics.Distributions.Normal throws on
            // sigma <= 0, which would crash the sampler instead of being rejected as a
            // boundary move. User-defined priors with non-positive support trigger this.
            if (sigma <= 0) return double.NegativeInfinity;
            var normDist = new Normal(0, sigma);
            double logLH = 0;

            int maxOrder = Math.Max(POrder, QOrder);
            for (int t = maxOrder; t < residuals.Length; t++)
            {
                logLH += normDist.LogPDF(residuals[t]);
            }

            return logLH + _logJacobian;
        }

        /// <inheritdoc/>
        public override double[] PointwiseDataLogLikelihood(double[] parameters)
        {
            if (_diffSeries == null || _diffSeries.Count == 0)
                return Array.Empty<double>();

            int effectiveTrainingSteps = Math.Min(TrainingTimeSteps - DOrder, _diffSeries.Count);
            int maxOrder = Math.Max(POrder, QOrder);
            int n = effectiveTrainingSteps - maxOrder;
            if (n <= 0) return Array.Empty<double>();

            for (int i = 0; i < parameters.Length; i++)
            {
                if (double.IsNaN(parameters[i]))
                {
                    var invalid = new double[n];
                    for (int j = 0; j < n; j++) invalid[j] = double.NegativeInfinity;
                    return invalid;
                }
            }

            var residuals = Residuals(parameters);
            double sigma = parameters.Last();
            var normDist = new Normal(0, sigma);
            var result = new double[n];

            double jacobianPerObs = _logJacobian / n;
            int idx = 0;
            for (int t = maxOrder; t < residuals.Length; t++)
            {
                result[idx++] = normDist.LogPDF(residuals[t]) + jacobianPerObs;
            }

            return result;
        }

        /// <inheritdoc/>
        public override List<DataComponent> PointwiseDataLogLikelihoodComponents(double[] parameters)
        {
            if (_diffSeries == null || _diffSeries.Count == 0)
                return new List<DataComponent>();

            int effectiveTrainingSteps = Math.Min(TrainingTimeSteps - DOrder, _diffSeries.Count);
            int maxOrder = Math.Max(POrder, QOrder);
            int n = effectiveTrainingSteps - maxOrder;
            if (n <= 0) return new List<DataComponent>();

            var result = new List<DataComponent>(n);
            var responseValues = _diffSeries?.ValuesToArray();

            for (int i = 0; i < parameters.Length; i++)
            {
                if (double.IsNaN(parameters[i]))
                {
                    for (int j = 0; j < n; j++)
                    {
                        int tIdx = maxOrder + j;
                        double value = responseValues != null && tIdx < responseValues.Length ? responseValues[tIdx] : 0;
                        result.Add(new DataComponent(j, double.NegativeInfinity, value, DataComponentType.Exact, 1, $"t={tIdx}"));
                    }
                    return result;
                }
            }

            var residuals = Residuals(parameters);
            double sigma = parameters.Last();
            var normDist = new Normal(0, sigma);
            double jacobianPerObs = _logJacobian / n;

            int idx = 0;
            for (int t = maxOrder; t < residuals.Length; t++)
            {
                double logLH = normDist.LogPDF(residuals[t]) + jacobianPerObs;
                double value = responseValues != null && t < responseValues.Length ? responseValues[t] : 0;
                result.Add(new DataComponent(idx++, logLH, value, DataComponentType.Exact, 1, $"t={t}"));
            }

            return result;
        }

        /// <inheritdoc/>
        public override double PriorLogLikelihood(double[] parameters)
        {
            if (parameters == null || Parameters is null || parameters.Length < Parameters.Count)
                return double.NegativeInfinity;

            double sigma = parameters.Last();
            double logLH = 0;

            for (int i = 0; i < Parameters.Count; i++)
            {
                logLH += Parameters[i].PriorDistribution.LogPDF(parameters[i]);
            }

            if (UseJeffreysRuleForScale)
            {
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

            for (int i = 0; i < Parameters.Count; i++)
            {
                double ll = Parameters[i].PriorDistribution.LogPDF(parameters[i]);
                string paramName = string.IsNullOrEmpty(Parameters[i].OwnerName) ? Parameters[i].Name : Parameters[i].OwnerName;
                result.Add(new PriorComponent($"Parameter Prior: {paramName}", ll, PriorComponentType.ParameterPrior));
            }

            if (UseJeffreysRuleForScale)
            {
                double sigma = parameters.Last();
                double ll = sigma > 0 ? -Math.Log(sigma) : double.NegativeInfinity;
                result.Add(new PriorComponent("Jeffreys' rule for σ", ll, PriorComponentType.ParameterPrior));
            }

            return result;
        }

        /// <summary>
        /// Predicts time series values using specified parameters.
        /// </summary>
        /// <param name="parameters">The parameter vector.</param>
        /// <param name="forecastSteps">Number of steps to forecast beyond training data.</param>
        /// <param name="seed">Random seed for stochastic predictions. If -1, returns mean prediction.</param>
        /// <returns>Tuple containing predicted values and component decomposition.</returns>
        public (double[] Y, double[] InterceptPart, double[] ARPart, double[] MAPart) Predict(double[] parameters, int forecastSteps = 0, int seed = -1)
        {
            if (TimeSeries == null)
                throw new InvalidOperationException("TimeSeries must be set.");

            int totalSteps = TrainingTimeSteps + forecastSteps;

            var y = new double[totalSteps];
            var interceptPart = new double[totalSteps];
            var arPart = new double[totalSteps];
            var maPart = new double[totalSteps];
            var epsilon = new double[totalSteps];

            int k = 0;
            double mu = IncludeIntercept ? parameters[k++] : 0;
            var phi = new double[POrder];
            for (int i = 0; i < POrder; i++)
                phi[i] = parameters[k++];
            var theta = new double[QOrder];
            for (int i = 0; i < QOrder; i++)
                theta[i] = parameters[k++];
            double sigma = parameters[k];

            Random? prng = seed >= 0 ? new Random(seed) : null;
            Normal? errDist = seed >= 0 ? new Normal(0, sigma) : null;

            int maxOrder = Math.Max(POrder, QOrder);

            for (int t = 0; t < totalSteps; t++)
            {
                interceptPart[t] = mu;

                double ar = 0;
                double ma = 0;

                if (t >= maxOrder)
                {
                    // AR lags: use observed _diffSeries inside the fit window; propagate
                    // via predicted y[t-p] once we leave it (validation + future forecast).
                    for (int p = 1; p <= POrder; p++)
                    {
                        if (_diffSeries != null && t - p < TrainingTimeSteps && t - p < _diffSeries.Count)
                        {
                            ar += phi[p - 1] * (_diffSeries[t - p].Value - mu);
                        }
                        else
                        {
                            ar += phi[p - 1] * (y[t - p] - mu);
                        }
                    }

                    // MA component
                    for (int q = 1; q <= Math.Min(t, QOrder); q++)
                    {
                        ma += theta[q - 1] * epsilon[t - q];
                    }
                }

                arPart[t] = ar;
                maPart[t] = ma;

                // Compute y[t]. For t < maxOrder, seed from observed so the AR/MA buffer
                // has real values. For t >= maxOrder, use the model prediction on the
                // differenced scale — noise is injected below when seeded.
                if (t < maxOrder && _diffSeries != null && t < _diffSeries.Count)
                {
                    y[t] = _diffSeries[t].Value;
                    epsilon[t] = 0;
                }
                else
                {
                    y[t] = mu + ar + ma;
                }

                // Pre-noise epsilon inside the fit window = observed - model prediction.
                // Keeps MA recursion anchored to true residuals through training.
                if (_diffSeries != null && t >= maxOrder && t < TrainingTimeSteps && t < _diffSeries.Count)
                {
                    epsilon[t] = _diffSeries[t].Value - y[t];
                }

                // Residual noise: draw every step once AR/MA buffer is seeded so CIs
                // wrap observations in training and fan out past the fit window.
                if (prng != null && t >= maxOrder)
                {
                    double mt = y[t];
                    double error = errDist!.InverseCDF(prng.NextDouble());
                    y[t] += error;

                    // Epsilon overwrite only outside the fit window so validation + forecast
                    // MA fan-out uses injected noise.
                    if (t >= TrainingTimeSteps)
                        epsilon[t] = y[t] - mt;
                }
            }

            // Post-processing: y is on the transformed + differenced scale.
            //
            // Step A — Integrate (reverse differencing) to the transformed + undifferenced
            // scale. Inside the fit window we anchor each integration level to the
            // appropriate intermediate-difference of the observed transformed series, so
            // a one-step-ahead CI wraps observations with roughly constant width rather
            // than a random-walk cone. Outside the fit window we use a plain cumsum so
            // per-step noise compounds and the CI fans out as expected for forecasts.
            //
            // For DOrder >= 2 we precompute the chain of intermediate differences:
            //   anchorSeries[k] = k-th difference of _trainingTimeSeries (k = 0 .. DOrder).
            // At integration iteration d we are reversing the (DOrder - d)-th difference,
            // so the correct anchor is anchorSeries[DOrder - 1 - d]. The previous
            // implementation only used the original (k = 0) series at d == 0 and plain
            // cumsum for d >= 1, which produced biased forecasts whenever DOrder >= 2.
            if (DOrder > 0)
            {
                var integrated = new double[totalSteps];
                Array.Copy(y, integrated, totalSteps);

                // Precompute anchor series at each intermediate differencing level.
                // anchorSeries[0] is the original transformed series; anchorSeries[k]
                // is its k-th difference. Built only when training data exists.
                List<TimeSeries>? anchorSeries = null;
                if (_trainingTimeSeries != null && _trainingTimeSeries.Count > 0)
                {
                    anchorSeries = new List<TimeSeries> { _trainingTimeSeries };
                    for (int level = 1; level < DOrder; level++)
                        anchorSeries.Add(Difference(anchorSeries[level - 1], 1));
                }

                for (int d = 0; d < DOrder; d++)
                {
                    int anchorLevel = DOrder - 1 - d;
                    TimeSeries? anchor = anchorSeries != null && anchorLevel < anchorSeries.Count
                        ? anchorSeries[anchorLevel]
                        : null;
                    bool useObservedAnchor = anchor != null && anchor.Count > 0;

                    if (useObservedAnchor)
                    {
                        integrated[0] = anchor![0].Value;

                        int anchorEnd = Math.Min(TrainingTimeSteps, totalSteps);
                        for (int i = 1; i < anchorEnd; i++)
                        {
                            int obsIdx = i - 1;
                            if (obsIdx < anchor.Count)
                                integrated[i] = anchor[obsIdx].Value + integrated[i];
                            else
                                integrated[i] = integrated[i - 1] + integrated[i];
                        }

                        for (int i = Math.Max(1, anchorEnd); i < totalSteps; i++)
                        {
                            integrated[i] = integrated[i - 1] + integrated[i];
                        }
                    }
                    else
                    {
                        for (int i = 1; i < totalSteps; i++)
                        {
                            integrated[i] = integrated[i - 1] + integrated[i];
                        }
                    }
                }

                y = integrated;
            }

            // Step B — Inverse transform back to the original scale. Posterior-median point
            // forecast; no bias correction (matches R's forecast::forecast.Arima convention).
            if (TransformType == Transform.Logarithmic || TransformType == Transform.BoxCox)
            {
                for (int t = 0; t < totalSteps; t++)
                    y[t] = BoxCox.InverseTransform(y[t], _lambda);
            }
            else if (TransformType == Transform.YeoJohnson)
            {
                for (int t = 0; t < totalSteps; t++)
                    y[t] = YeoJohnson.InverseTransform(y[t], _lambda);
            }

            return (y, interceptPart, arPart, maPart);
        }

        /// <summary>
        /// Predicts time series values using current parameters.
        /// </summary>
        /// <param name="forecastSteps">Number of steps to forecast beyond training data.</param>
        /// <param name="seed">Random seed for stochastic predictions. If -1, returns mean prediction.</param>
        /// <returns>Array of predicted values.</returns>
        public double[] Predict(int forecastSteps = 0, int seed = -1)
        {
            var pars = Parameters.Select(x => x.Value).ToArray();
            return Predict(pars, forecastSteps, seed).Y;
        }

        /// <summary>
        /// Generates a random time series using current parameter values.
        /// </summary>
        /// <param name="timeSteps">Number of time steps to simulate.</param>
        /// <param name="seed">Random seed for reproducibility.</param>
        /// <returns>Simulated time series.</returns>
        public TimeSeries GenerateRandomSeries(int timeSteps, int seed = 12345)
        {
            if (TimeSeries == null)
                throw new InvalidOperationException("TimeSeries must be set.");

            DateTime startDate = TimeSeries.StartDate;
            DateTime endDate = startDate;
            for (int i = 0; i < timeSteps - 1; i++)
            {
                endDate = Numerics.Data.TimeSeries.AddTimeInterval(endDate, TimeSeries.TimeInterval);
            }

            var result = new TimeSeries(TimeSeries.TimeInterval, startDate, endDate);
            var parameters = Parameters.Select(x => x.Value).ToArray();
            var prediction = Predict(parameters, timeSteps - TrainingTimeSteps, seed);

            for (int i = 0; i < timeSteps; i++)
            {
                result[i].Value = prediction.Y[i];
            }

            return result;
        }

        /// <inheritdoc/>
        public override IModel Clone()
        {
            var parms = new List<ModelParameter>();
            for (int i = 0; i < NumberOfParameters; i++)
                parms.Add(Parameters[i].Clone());

            var result = new ARIMA()
            {
                _pOrder = POrder,
                _dOrder = DOrder,
                _qOrder = QOrder,
                _includeIntercept = IncludeIntercept,
                _useDefaultFlatPriors = UseDefaultFlatPriors,
                _useJeffreysRuleForScale = UseJeffreysRuleForScale,
                _transformType = TransformType,
                _trainingTimeSteps = TrainingTimeSteps,
                _useDefaultTrainingSteps = UseDefaultTrainingSteps,
                Parameters = parms
            };

            result.TimeSeries = TimeSeries?.Clone()!;
            result._trainingTimeSteps = TrainingTimeSteps;
            result._useDefaultTrainingSteps = UseDefaultTrainingSteps;
            result.SetTrainingData();
            return result;
        }

        /// <inheritdoc/>
        public override XElement ToXElement()
        {
            var result = new XElement(nameof(ARIMA));
            result.SetAttributeValue(nameof(POrder), POrder.ToString(CultureInfo.InvariantCulture));
            result.SetAttributeValue(nameof(DOrder), DOrder.ToString(CultureInfo.InvariantCulture));
            result.SetAttributeValue(nameof(QOrder), QOrder.ToString(CultureInfo.InvariantCulture));
            result.SetAttributeValue(nameof(IncludeIntercept), IncludeIntercept.ToString());
            result.SetAttributeValue(nameof(UseDefaultFlatPriors), UseDefaultFlatPriors.ToString());
            result.SetAttributeValue(nameof(UseJeffreysRuleForScale), UseJeffreysRuleForScale.ToString());
            result.SetAttributeValue(nameof(TransformType), TransformType.ToString());
            result.SetAttributeValue(nameof(TrainingTimeSteps), TrainingTimeSteps.ToString(CultureInfo.InvariantCulture));
            result.SetAttributeValue(nameof(UseDefaultTrainingSteps), UseDefaultTrainingSteps.ToString());

            var parms = new XElement(nameof(Parameters));
            foreach (var p in Parameters)
                parms.Add(p.ToXElement());
            result.Add(parms);

            return result;
        }

        /// <summary>
        /// Checks whether the AR component is stationary based on the current parameter values.
        /// </summary>
        /// <returns><c>true</c> if stationary; otherwise, <c>false</c>.</returns>
        public bool IsStationary()
        {
            if (POrder == 0 || Parameters == null || Parameters.Count == 0)
                return true;

            int k = IncludeIntercept ? 1 : 0;
            if (Parameters.Count <= k)
                return true;

            var phi = new double[POrder];
            for (int i = 0; i < POrder; i++)
                phi[i] = Parameters[k + i].Value;

            if (POrder == 1)
                return Math.Abs(phi[0]) < 1.0;

            double sumAbsPhi = 0.0;
            for (int i = 0; i < POrder; i++)
                sumAbsPhi += Math.Abs(phi[i]);
            return sumAbsPhi < 1.0;
        }

        /// <summary>
        /// Checks whether the MA component is invertible based on the current parameter values.
        /// </summary>
        /// <returns><c>true</c> if invertible; otherwise, <c>false</c>.</returns>
        public bool IsInvertible()
        {
            if (QOrder == 0 || Parameters == null || Parameters.Count == 0)
                return true;

            int k = IncludeIntercept ? 1 : 0;
            k += POrder;
            if (Parameters.Count <= k)
                return true;

            var theta = new double[QOrder];
            for (int i = 0; i < QOrder; i++)
                theta[i] = Parameters[k + i].Value;

            if (QOrder == 1)
                return Math.Abs(theta[0]) < 1.0;

            double sumAbsTheta = 0.0;
            for (int i = 0; i < QOrder; i++)
                sumAbsTheta += Math.Abs(theta[i]);
            return sumAbsTheta < 1.0;
        }

        /// <inheritdoc/>
        public override (bool IsValid, List<string> ValidationMessages) Validate()
        {
            bool isValid = true;
            var messages = new List<string>();

            if (TimeSeries == null)
            {
                isValid = false;
                messages.Add("Error: Time series is null.");
                return (isValid, messages);
            }

            if (TimeSeries.Count < 10)
            {
                isValid = false;
                messages.Add("Error: Time series must have at least 10 observations.");
            }

            if (TimeSeries.TimeInterval == TimeInterval.Irregular)
            {
                isValid = false;
                messages.Add("Error: Time series analysis requires a regular time interval. Resample or convert the series to a regular interval before estimating.");
            }

            if (POrder < 0 || POrder > 10)
            {
                isValid = false;
                messages.Add("Error: AR order (p) must be between 0 and 10.");
            }

            if (DOrder < 0 || DOrder > 2)
            {
                isValid = false;
                messages.Add("Error: Differencing order (d) must be between 0 and 2.");
            }

            if (QOrder < 0 || QOrder > 10)
            {
                isValid = false;
                messages.Add("Error: MA order (q) must be between 0 and 10.");
            }

            if (POrder == 0 && QOrder == 0)
            {
                isValid = false;
                messages.Add("Error: At least one of AR order (p) or MA order (q) must be greater than 0.");
            }

            if (TrainingTimeSteps > TimeSeries.Count)
            {
                isValid = false;
                messages.Add("Error: Training time steps cannot exceed time series length.");
            }

            int maxOrder = Math.Max(POrder, QOrder);
            if (TimeSeries.Count <= maxOrder + DOrder)
            {
                isValid = false;
                messages.Add($"Error: Time series length ({TimeSeries.Count}) must exceed max order + differencing ({maxOrder + DOrder}).");
            }

            if (DOrder > 0 && TrainingTimeSteps > TimeSeries.Count - DOrder)
            {
                messages.Add($"Warning: TrainingTimeSteps ({TrainingTimeSteps}) exceeds the differenced series length " +
                    $"({TimeSeries.Count - DOrder}). Effective training will use {TimeSeries.Count - DOrder} time steps.");
            }

            if (Parameters != null)
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

                if (POrder > 0 && !IsStationary())
                {
                    messages.Add("Warning: AR parameters do not satisfy the sum-of-absolute-values stationarity sufficient condition (Σ|φᵢ| < 1). The model may still be stationary — this check is conservative for orders ≥ 3 — but forecasts may be unstable if it is not.");
                }
                if (QOrder > 0 && !IsInvertible())
                {
                    messages.Add("Warning: MA parameters do not satisfy the sum-of-absolute-values invertibility sufficient condition (Σ|θᵢ| < 1). The model may still be invertible — this check is conservative for orders ≥ 2 — but uniqueness of the MA representation is not guaranteed if it is not.");
                }
            }

            return (isValid, messages);
        }

        /// <inheritdoc/>
        public double[] GenerateRandomValues(int sampleSize, int seed = -1)
        {
            if (sampleSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(sampleSize), "Sample size must be positive.");

            var rng = seed >= 0 ? new Numerics.Sampling.MersenneTwister(seed) : new Numerics.Sampling.MersenneTwister();

            int paramIndex = 0;
            double intercept = 0;
            if (IncludeIntercept)
            {
                intercept = Parameters[paramIndex].Value;
                paramIndex++;
            }

            double[] phi = new double[POrder];
            for (int i = 0; i < POrder; i++)
            {
                phi[i] = Parameters[paramIndex + i].Value;
            }
            paramIndex += POrder;

            double[] theta = new double[QOrder];
            for (int i = 0; i < QOrder; i++)
            {
                theta[i] = Parameters[paramIndex + i].Value;
            }
            paramIndex += QOrder;

            double sigma = Parameters[paramIndex].Value;
            var normal = new Numerics.Distributions.Normal(0, sigma);

            var series = new double[sampleSize];
            var epsilon = new double[sampleSize];

            for (int t = 0; t < sampleSize; t++)
            {
                epsilon[t] = normal.InverseCDF(rng.NextDouble());
            }

            for (int t = 0; t < Math.Max(POrder, QOrder); t++)
            {
                series[t] = intercept + epsilon[t];
            }

            for (int t = Math.Max(POrder, QOrder); t < sampleSize; t++)
            {
                double value = intercept;

                for (int j = 0; j < POrder; j++)
                {
                    value += phi[j] * (series[t - 1 - j] - intercept);
                }

                value += epsilon[t];
                for (int j = 0; j < QOrder; j++)
                {
                    value += theta[j] * epsilon[t - 1 - j];
                }

                series[t] = value;
            }

            return series;
        }

        #endregion
    }
}
