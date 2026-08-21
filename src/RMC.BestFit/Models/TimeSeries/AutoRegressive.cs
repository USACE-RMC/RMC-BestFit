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
    /// Autoregressive (AR) time-series model with support for data transformations,
    /// training/forecasting splits, and Bayesian estimation features.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The AR(p) model: Y(t) = μ + φ1*(Y(t-1) - μ) + ... + φp*(Y(t-p) - μ) + ε(t)
    /// where ε(t) ~ N(0, σ²)
    /// </para>
    /// <para>
    /// Features include:
    /// <list type="bullet">
    /// <item><description>Data transformations: logarithmic, Box-Cox, Yeo-Johnson</description></item>
    /// <item><description>Training/forecasting time step splits for out-of-sample validation</description></item>
    /// <item><description>Jeffreys' rule prior for scale parameter</description></item>
    /// </list>
    /// </para>
    /// <para>
    ///     <b>Authors:</b>
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// </remarks>
    public class AutoRegressive : ModelBase, ISimulatable<double[]>
    {
        #region Construction

        /// <summary>
        /// Constructs an empty AR model with default order 1.
        /// </summary>
        public AutoRegressive()
        {
            _order = 1;
            _includeIntercept = true;
            SetDefaultParameters();
        }

        /// <summary>
        /// Constructs an AR model with specified parameters.
        /// </summary>
        /// <param name="timeSeries">The time-series data to model.</param>
        /// <param name="order">The AR order (p). Default = 1.</param>
        /// <param name="includeIntercept">Whether to include an intercept term. Default = true.</param>
        public AutoRegressive(TimeSeries timeSeries, int order = 1, bool includeIntercept = true)
        {
            _order = order;
            _includeIntercept = includeIntercept;
            TimeSeries = timeSeries;
        }

        /// <summary>
        /// Constructs an AR model by deserializing from XML.
        /// </summary>
        /// <param name="timeSeries">The time-series data to model.</param>
        /// <param name="xElement">The XML element to deserialize.</param>
        public AutoRegressive(TimeSeries timeSeries, XElement xElement)
        {
            var orderAttr = xElement.Attribute(nameof(Order));
            if (orderAttr != null)
                int.TryParse(orderAttr.Value, out _order);
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
            double? persistedTransformLambda = null;
            var transformLambdaAttr = xElement.Attribute(nameof(TransformLambda));
            if (transformLambdaAttr != null &&
                double.TryParse(transformLambdaAttr.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedLambda) &&
                double.IsFinite(parsedLambda))
            {
                persistedTransformLambda = parsedLambda;
            }
            bool persistedTransformLambdaIsManual = false;
            var transformLambdaIsManualAttr = xElement.Attribute("TransformLambdaIsManual");
            if (transformLambdaIsManualAttr != null)
                bool.TryParse(transformLambdaIsManualAttr.Value, out persistedTransformLambdaIsManual);

            // Set TimeSeries first (this may trigger SetDefaultParameters)
            TimeSeries = timeSeries;
            if (trainingStepsAttr != null)
                int.TryParse(trainingStepsAttr.Value, out _trainingTimeSteps);
            if (useDefaultTrainingAttr != null)
                bool.TryParse(useDefaultTrainingAttr.Value, out _useDefaultTrainingSteps);
            if (persistedTransformLambda.HasValue)
            {
                _lambda = persistedTransformLambda.Value;
                _transformLambdaIsManual = persistedTransformLambdaIsManual;
                _usePersistedTransformLambda = true;
            }
            SetTrainingData();
            _usePersistedTransformLambda = false;

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
        private int _order = 1;
        private bool _includeIntercept = true;

        // Transform members
        private Transform _transformType = Transform.None;
        private TimeSeries _transformedTimeSeries = null!;
        private double _lambda = 0;
        private bool _transformLambdaIsManual;
        private bool _usePersistedTransformLambda;
        private double _logJacobian = 0;
        private double[]? _logJacobianTerms;
        private string? _transformFitValidationMessage;

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

                _usePersistedTransformLambda = false;
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
        public int Order
        {
            get { return _order; }
            set
            {
                if (_order != value)
                {
                    _order = value;
                    RaisePropertyChange(nameof(Order));
                    SetTrainingData();
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
                    double previousLambda = _lambda;
                    _transformType = value;
                    _transformLambdaIsManual = false;
                    _usePersistedTransformLambda = false;
                    SetTrainingData(false);
                    if (UseDefaultFlatPriors)
                        SetDefaultParameters();
                    if (_lambda != previousLambda)
                        RaisePropertyChange(nameof(TransformLambda));
                    RaisePropertyChange(nameof(TransformType));
                }
            }
        }

        /// <summary>
        /// Gets the transformed time series used for model calibration.
        /// </summary>
        public TimeSeries TrainingTimeSeries => _trainingTimeSeries;

        /// <summary>
        /// Gets the effective Box-Cox or Yeo-Johnson transformation exponent.
        /// </summary>
        /// <remarks>
        /// The value is fitted from the training prefix unless it was assigned through
        /// <see cref="SetTransformParameters(double, double)"/>. None and logarithmic transforms
        /// use the canonical value zero. This model-state property is hidden from property grids.
        /// </remarks>
        [Browsable(false)]
        public double TransformLambda => _lambda;

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

            // Use 80% for training, with minimum of 30 or parameter count
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
        /// Prepares the training data by applying transformations.
        /// </summary>
        /// <param name="notifyTransformLambda">Whether to notify observers when the effective exponent changes.</param>
        private void SetTrainingData(bool notifyTransformLambda = true)
        {
            double previousLambda = _lambda;
            try
            {
                _transformFitValidationMessage = null;
                _logJacobian = 0;
                _logJacobianTerms = null;
                if (TransformType == Transform.None || TransformType == Transform.Logarithmic)
                {
                    _lambda = 0;
                    _transformLambdaIsManual = false;
                    _usePersistedTransformLambda = false;
                }
                if (TimeSeries == null)
                {
                    _transformedTimeSeries = null!;
                    _trainingTimeSeries = null!;
                    return;
                }

                _transformedTimeSeries = new TimeSeries(TimeSeries.TimeInterval);
                _trainingTimeSeries = new TimeSeries(TimeSeries.TimeInterval);
                int effectiveTrainingSteps = Math.Min(TrainingTimeSteps, TimeSeries.Count);

                if (TransformType == Transform.None || TransformType == Transform.Logarithmic)
                {
                    _lambda = 0;
                    _transformLambdaIsManual = false;
                    _usePersistedTransformLambda = false;
                }
                else if (effectiveTrainingSteps == 0)
                {
                    _lambda = 0;
                    return;
                }
                else if (!_transformLambdaIsManual && !_usePersistedTransformLambda)
                {
                    var fittingValues = TimeSeries.ValuesToArray().Subset(0, effectiveTrainingSteps - 1);
                    try
                    {
                        if (TransformType == Transform.BoxCox)
                            BoxCox.FitLambda(fittingValues, out _lambda);
                        else
                            YeoJohnson.FitLambda(fittingValues, out _lambda);
                    }
                    catch (ArithmeticException ex)
                    {
                        _lambda = 0;
                        string transformName = TransformType == Transform.BoxCox ? "Box-Cox" : "Yeo-Johnson";
                        _transformFitValidationMessage = $"Error: {transformName} lambda estimation failed. Select a different transform or revise the time-series data. Solver message: {ex.Message}";
                        System.Diagnostics.Debug.WriteLine($"AutoRegressive.SetTrainingData: {_transformFitValidationMessage}");
                        System.Diagnostics.Debug.WriteLine(ex);
                        return;
                    }

                    if (!double.IsFinite(_lambda))
                    {
                        _lambda = 0;
                        string transformName = TransformType == Transform.BoxCox ? "Box-Cox" : "Yeo-Johnson";
                        _transformFitValidationMessage = $"Error: {transformName} lambda estimation failed. Select a different transform or revise the time-series data.";
                        System.Diagnostics.Debug.WriteLine($"AutoRegressive.SetTrainingData: {_transformFitValidationMessage}");
                        return;
                    }
                }

                for (int i = 0; i < TimeSeries.Count; i++)
                {
                    var ordinate = TimeSeries[i].Clone();
                    if (TransformType == Transform.Logarithmic || TransformType == Transform.BoxCox)
                        ordinate.Value = BoxCox.Transform(ordinate.Value, _lambda);
                    else if (TransformType == Transform.YeoJohnson)
                        ordinate.Value = YeoJohnson.Transform(ordinate.Value, _lambda);
                    _transformedTimeSeries.Add(ordinate);
                }

                for (int i = 0; i < effectiveTrainingSteps; i++)
                    _trainingTimeSeries.Add(_transformedTimeSeries[i].Clone());

                if (TransformType != Transform.None && effectiveTrainingSteps > Order)
                {
                    var likelihoodValues = TimeSeries.ValuesToArray().Subset(Order, effectiveTrainingSteps - 1);
                    _logJacobian = TransformType == Transform.YeoJohnson
                        ? YeoJohnson.LogJacobian(likelihoodValues, _lambda)
                        : BoxCox.LogJacobian(likelihoodValues, _lambda);
                    _logJacobianTerms = ComputeLogJacobianTerms(likelihoodValues);
                }
            }
            finally
            {
                if (notifyTransformLambda && _lambda != previousLambda)
                    RaisePropertyChange(nameof(TransformLambda));
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Sets the transformation parameters manually.
        /// </summary>
        /// <param name="lambda1">The primary transformation parameter (λ for Box-Cox/Yeo-Johnson).</param>
        /// <param name="lambda2">Ignored; the transform uses a single parameter.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="lambda1"/> is not finite.</exception>
        /// <remarks>
        /// For Box-Cox and Yeo-Johnson, the supplied exponent becomes manual state and remains fixed
        /// when the training window changes. None and logarithmic transforms canonicalize the exponent
        /// to zero and discard manual state.
        /// </remarks>
        public void SetTransformParameters(double lambda1 = 0, double lambda2 = 0)
        {
            if (!double.IsFinite(lambda1))
                throw new ArgumentOutOfRangeException(nameof(lambda1), "The transformation exponent must be finite.");

            _usePersistedTransformLambda = false;
            if (TransformType == Transform.None || TransformType == Transform.Logarithmic)
            {
                _lambda = 0;
                _transformLambdaIsManual = false;
            }
            else
            {
                _lambda = lambda1;
                _transformLambdaIsManual = true;
            }

            SetTrainingData(false);
            if (UseDefaultFlatPriors)
                SetDefaultParameters();
            RaisePropertyChange(nameof(TransformLambda));
            RaisePropertyChange(nameof(TransformType));
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

            if (TrainingTimeSeries != null && TrainingTimeSeries.Count > Order)
            {
                mean = TrainingTimeSeries.MeanValue();
                sigma = TrainingTimeSeries.StandardDeviation();

                // Data-driven bounds
                double tempMin = Math.Sign(mean) * Math.Pow(10, Math.Floor(Math.Log10(Math.Abs(mean)) - 1));
                double tempMax = Math.Sign(mean) * Math.Pow(10, Math.Ceiling(Math.Log10(Math.Abs(mean)) + 1));
                min = Math.Min(tempMin, tempMax);
                max = Math.Max(tempMin, tempMax);

                // Handle edge cases
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

            for (int i = 1; i <= Order; i++)
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
        /// <returns>Array of residuals on the transformed scale.</returns>
        public double[] Residuals(double[] parameters)
        {
            int effectiveTrainingSteps = TrainingTimeSeries != null
                ? Math.Min(TrainingTimeSteps, TrainingTimeSeries.Count)
                : TrainingTimeSteps;

            var residuals = new double[effectiveTrainingSteps];

            int k = 0;
            double mu = IncludeIntercept ? parameters[k++] : 0;
            var phi = new double[Order];
            for (int i = 0; i < Order; i++)
                phi[i] = parameters[k++];

            for (int t = 0; t < effectiveTrainingSteps; t++)
            {
                if (t < Order)
                {
                    residuals[t] = 0;
                }
                else
                {
                    double prediction = mu;
                    for (int p = 1; p <= Order; p++)
                    {
                        prediction += phi[p - 1] * (TrainingTimeSeries![t - p].Value - mu);
                    }
                    residuals[t] = TrainingTimeSeries![t].Value - prediction;
                }
            }

            return residuals;
        }

        /// <inheritdoc/>
        public override double DataLogLikelihood(double[] parameters)
        {
            if (TrainingTimeSeries == null || TrainingTimeSeries.Count <= Order)
                return double.NegativeInfinity;

            for (int i = 0; i < parameters.Length; i++)
            {
                if (double.IsNaN(parameters[i]))
                    return double.NegativeInfinity;
            }

            var residuals = Residuals(parameters);
            double sigma = parameters.Last();
            // Guard against non-finite or non-positive sigma — Numerics.Distributions.Normal
            // rejects it. User-defined priors with non-positive support could otherwise
            // crash the sampler instead of seeing -Inf log-likelihood at boundary moves.
            if (!Tools.IsFinite(sigma) || sigma <= 0) return double.NegativeInfinity;
            var normDist = new Normal(0, sigma);
            double logLH = 0;

            // Conditional likelihood starting at t = Order
            for (int t = Order; t < residuals.Length; t++)
            {
                logLH += normDist.LogPDF(residuals[t]);
            }

            return logLH + _logJacobian;
        }

        /// <inheritdoc/>
        public override double[] PointwiseDataLogLikelihood(double[] parameters)
        {
            if (TrainingTimeSeries == null || TrainingTimeSeries.Count <= Order)
                return Array.Empty<double>();

            int effectiveTrainingSteps = Math.Min(TrainingTimeSteps, TrainingTimeSeries.Count);
            int n = effectiveTrainingSteps - Order;
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
            if (!Tools.IsFinite(sigma) || sigma <= 0)
            {
                var invalid = new double[n];
                Array.Fill(invalid, double.NegativeInfinity);
                return invalid;
            }
            var normDist = new Normal(0, sigma);
            var result = new double[n];

            double[] jacobianTerms = GetLogJacobianTerms(n);
            int idx = 0;
            for (int t = Order; t < residuals.Length; t++)
            {
                result[idx++] = normDist.LogPDF(residuals[t]) + jacobianTerms[t - Order];
            }

            return result;
        }

        /// <inheritdoc/>
        public override List<DataComponent> PointwiseDataLogLikelihoodComponents(double[] parameters)
        {
            if (TrainingTimeSeries == null || TrainingTimeSeries.Count <= Order)
                return new List<DataComponent>();

            int effectiveTrainingSteps = Math.Min(TrainingTimeSteps, TrainingTimeSeries.Count);
            int n = effectiveTrainingSteps - Order;
            if (n <= 0) return new List<DataComponent>();

            var result = new List<DataComponent>(n);
            var responseValues = TrainingTimeSeries?.ValuesToArray();

            for (int i = 0; i < parameters.Length; i++)
            {
                if (double.IsNaN(parameters[i]))
                {
                    for (int j = 0; j < n; j++)
                    {
                        int tIdx = Order + j;
                        double value = responseValues != null && tIdx < responseValues.Length ? responseValues[tIdx] : 0;
                        result.Add(new DataComponent(j, double.NegativeInfinity, value, DataComponentType.Exact, 1, $"t={tIdx}"));
                    }
                    return result;
                }
            }

            var residuals = Residuals(parameters);
            double sigma = parameters.Last();
            if (!Tools.IsFinite(sigma) || sigma <= 0)
            {
                for (int j = 0; j < n; j++)
                {
                    int tIdx = Order + j;
                    double value = responseValues != null && tIdx < responseValues.Length ? responseValues[tIdx] : 0;
                    result.Add(new DataComponent(j, double.NegativeInfinity, value, DataComponentType.Exact, 1, $"t={tIdx}"));
                }
                return result;
            }
            var normDist = new Normal(0, sigma);
            double[] jacobianTerms = GetLogJacobianTerms(n);

            int idx = 0;
            for (int t = Order; t < residuals.Length; t++)
            {
                double logLH = normDist.LogPDF(residuals[t]) + jacobianTerms[t - Order];
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
            if (!Tools.IsFinite(sigma) || sigma <= 0)
                return double.NegativeInfinity;
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
            double sigma = parameters.Last();
            bool isValidScale = Tools.IsFinite(sigma) && sigma > 0;

            for (int i = 0; i < Parameters.Count; i++)
            {
                double ll = i == Parameters.Count - 1 && !isValidScale
                    ? double.NegativeInfinity
                    : Parameters[i].PriorDistribution.LogPDF(parameters[i]);
                string paramName = string.IsNullOrEmpty(Parameters[i].OwnerName) ? Parameters[i].Name : Parameters[i].OwnerName;
                result.Add(new PriorComponent($"Parameter Prior: {paramName}", ll, PriorComponentType.ParameterPrior));
            }

            if (UseJeffreysRuleForScale)
            {
                double ll = isValidScale ? -Math.Log(sigma) : double.NegativeInfinity;
                result.Add(new PriorComponent("Jeffreys' rule for σ", ll, PriorComponentType.JeffreysScalePrior));
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
        public (double[] Y, double[] InterceptPart, double[] ARPart) Predict(double[] parameters, int forecastSteps = 0, int seed = -1)
        {
            if (TimeSeries == null)
                throw new InvalidOperationException("TimeSeries must be set.");

            int totalSteps = TrainingTimeSteps + forecastSteps;

            var y = new double[totalSteps];
            var interceptPart = new double[totalSteps];
            var arPart = new double[totalSteps];

            int k = 0;
            double mu = IncludeIntercept ? parameters[k++] : 0;
            var phi = new double[Order];
            for (int i = 0; i < Order; i++)
                phi[i] = parameters[k++];
            double sigma = parameters[k];

            Random? prng = seed >= 0 ? new Random(seed) : null;
            Normal? errDist = seed >= 0 ? new Normal(0, sigma) : null;

            for (int t = 0; t < totalSteps; t++)
            {
                interceptPart[t] = mu;

                double ar = 0;
                if (t >= Order)
                {
                    // AR lag: observed training data where available (t - p < TrainingTimeSteps).
                    // Anchors the first forecast step to the last observed training value, then
                    // propagates through noisy y[t-p] once we leave the fit window.
                    for (int p = 1; p <= Order; p++)
                    {
                        if (TrainingTimeSeries != null && t - p < TrainingTimeSteps && t - p < TrainingTimeSeries.Count)
                        {
                            ar += phi[p - 1] * (TrainingTimeSeries[t - p].Value - mu);
                        }
                        else
                        {
                            ar += phi[p - 1] * (y[t - p] - mu);
                        }
                    }
                }
                arPart[t] = ar;

                // Seed the AR buffer for the first few steps, then use model prediction.
                if (t < Order && TrainingTimeSeries != null && t < TrainingTimeSeries.Count)
                {
                    y[t] = TrainingTimeSeries[t].Value;
                }
                else
                {
                    y[t] = mu + ar;

                    // Residual noise for every step once the AR buffer is seeded. Gives a
                    // proper one-step-ahead CI in training (wraps observations) and allows
                    // the CI to fan out past TrainingTimeSteps as noise propagates via y[t-p].
                    if (prng != null && t >= Order)
                    {
                        y[t] += errDist!.InverseCDF(prng.NextDouble());
                    }
                }
            }

            // Inverse transform back to the original scale. AR has no differencing, so the
            // integration step that ARIMA/ARIMAX apply is not needed. Posterior-median point
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

            return (y, interceptPart, arPart);
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

            var result = new AutoRegressive()
            {
                _order = Order,
                _includeIntercept = IncludeIntercept,
                _useDefaultFlatPriors = UseDefaultFlatPriors,
                _useJeffreysRuleForScale = UseJeffreysRuleForScale,
                _transformType = TransformType,
                _lambda = TransformLambda,
                _transformLambdaIsManual = _transformLambdaIsManual,
                _trainingTimeSteps = TrainingTimeSteps,
                _useDefaultTrainingSteps = UseDefaultTrainingSteps,
                Parameters = parms
            };

            result.TimeSeries = TimeSeries?.Clone()!;
            result._trainingTimeSteps = TrainingTimeSteps;
            result._useDefaultTrainingSteps = UseDefaultTrainingSteps;
            result._lambda = TransformLambda;
            result._transformLambdaIsManual = _transformLambdaIsManual;
            result._usePersistedTransformLambda = true;
            result.SetTrainingData();
            result._usePersistedTransformLambda = false;
            return result;
        }

        /// <inheritdoc/>
        public override XElement ToXElement()
        {
            var result = new XElement(nameof(AutoRegressive));
            result.SetAttributeValue(nameof(Order), Order.ToString(CultureInfo.InvariantCulture));
            result.SetAttributeValue(nameof(IncludeIntercept), IncludeIntercept.ToString());
            result.SetAttributeValue(nameof(UseDefaultFlatPriors), UseDefaultFlatPriors.ToString());
            result.SetAttributeValue(nameof(UseJeffreysRuleForScale), UseJeffreysRuleForScale.ToString());
            result.SetAttributeValue(nameof(TransformType), TransformType.ToString());
            result.SetAttributeValue(nameof(TransformLambda), TransformLambda.ToString("R", CultureInfo.InvariantCulture));
            result.SetAttributeValue("TransformLambdaIsManual", _transformLambdaIsManual.ToString());
            result.SetAttributeValue(nameof(TrainingTimeSteps), TrainingTimeSteps.ToString(CultureInfo.InvariantCulture));
            result.SetAttributeValue(nameof(UseDefaultTrainingSteps), UseDefaultTrainingSteps.ToString());

            var parms = new XElement(nameof(Parameters));
            foreach (var p in Parameters)
                parms.Add(p.ToXElement());
            result.Add(parms);

            return result;
        }

        /// <summary>
        /// Checks whether the AR process is stationary based on the current parameter values.
        /// </summary>
        /// <returns><c>true</c> if the AR process is stationary; otherwise, <c>false</c>.</returns>
        public bool IsStationary()
        {
            if (Parameters == null || Parameters.Count == 0)
                return true;

            int k = IncludeIntercept ? 1 : 0;
            if (Parameters.Count <= k)
                return true;

            var phi = new double[Order];
            for (int i = 0; i < Order; i++)
                phi[i] = Parameters[k + i].Value;

            if (Order == 1)
                return Math.Abs(phi[0]) < 1.0;

            if (Order == 2)
                return phi[0] + phi[1] < 1.0 && phi[1] - phi[0] < 1.0 && Math.Abs(phi[1]) < 1.0;

            double sumAbsPhi = 0.0;
            for (int i = 0; i < Order; i++)
                sumAbsPhi += Math.Abs(phi[i]);
            return sumAbsPhi < 1.0;
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

            if (Order < 1 || Order > 10)
            {
                isValid = false;
                messages.Add("Error: AR order must be between 1 and 10.");
            }

            if (TrainingTimeSteps < NumberOfParameters)
            {
                isValid = false;
                messages.Add($"Error: Training time steps ({TrainingTimeSteps}) must be at least equal to the number of parameters ({NumberOfParameters}).");
            }

            if (TrainingTimeSteps > TimeSeries.Count)
            {
                isValid = false;
                messages.Add("Error: Training time steps cannot exceed time series length.");
            }

            if (TimeSeries.Count <= Order)
            {
                isValid = false;
                messages.Add($"Error: Time series length ({TimeSeries.Count}) must exceed AR order ({Order}).");
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

                if (!IsStationary())
                {
                    messages.Add("Warning: AR parameters do not satisfy the sum-of-absolute-values stationarity sufficient condition (Σ|φᵢ| < 1). The model may still be stationary — this check is conservative for orders ≥ 3 — but forecasts may be unstable if it is not.");
                }
            }

            if (_transformFitValidationMessage != null)
            {
                isValid = false;
                messages.Add(_transformFitValidationMessage);
            }

            return (isValid, messages);
        }

        /// <inheritdoc/>
        public double[] GenerateRandomValues(int sampleSize, int seed = -1)
        {
            if (sampleSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(sampleSize), "Sample size must be positive.");

            int paramIndex = 0;
            double mu = 0;
            if (IncludeIntercept)
            {
                mu = Parameters[paramIndex].Value;
                paramIndex++;
            }
            double[] phi = new double[Order];
            for (int i = 0; i < Order; i++)
            {
                phi[i] = Parameters[paramIndex].Value;
                paramIndex++;
            }
            double sigma = Parameters[paramIndex].Value;

            var rng = seed > 0 ? new Numerics.Sampling.MersenneTwister(seed) : new Numerics.Sampling.MersenneTwister();
            var normal = new Normal(0, sigma);

            var series = new double[sampleSize];

            for (int i = 0; i < Order; i++)
            {
                series[i] = mu + normal.InverseCDF(rng.NextDouble());
            }

            for (int t = Order; t < sampleSize; t++)
            {
                double value = mu;
                for (int j = 0; j < Order; j++)
                {
                    value += phi[j] * (series[t - 1 - j] - mu);
                }
                value += normal.InverseCDF(rng.NextDouble());
                series[t] = value;
            }

            return InverseTransformGeneratedSeries(series);
        }

        /// <summary>
        /// Converts a completed model-scale simulation to the raw response scale.
        /// </summary>
        /// <param name="values">The complete simulated model-scale series.</param>
        /// <returns>The raw-scale series, or the original array when no transform is configured.</returns>
        private double[] InverseTransformGeneratedSeries(double[] values)
        {
            if (TransformType == Transform.None)
                return values;

            for (int i = 0; i < values.Length; i++)
            {
                values[i] = TransformType == Transform.YeoJohnson
                    ? YeoJohnson.InverseTransform(values[i], _lambda)
                    : BoxCox.InverseTransform(values[i], _lambda);
            }
            return values;
        }

        /// <summary>
        /// Computes the log-Jacobian term of each raw observation in the likelihood window.
        /// </summary>
        /// <param name="values">The raw observations whose transformed values enter the likelihood.</param>
        /// <returns>One log-Jacobian term per observation; the terms sum to the scalar log Jacobian.</returns>
        private double[] ComputeLogJacobianTerms(double[] values)
        {
            var terms = new double[values.Length];
            var single = new double[1];
            for (int i = 0; i < values.Length; i++)
            {
                single[0] = values[i];
                terms[i] = TransformType == Transform.YeoJohnson
                    ? YeoJohnson.LogJacobian(single, _lambda)
                    : BoxCox.LogJacobian(single, _lambda);
            }

            return terms;
        }

        /// <summary>
        /// Gets the per-observation log-Jacobian terms aligned with the evaluated model steps.
        /// </summary>
        /// <param name="count">The number of evaluated model steps.</param>
        /// <returns>One term per evaluated step; zeros when no transform is active.</returns>
        /// <remarks>
        /// Each observation carries its own change-of-variables term, so pointwise terms reflect
        /// that observation's actual contribution. If the stored terms do not match the evaluated
        /// count, the scalar log Jacobian is spread uniformly so the pointwise sum still equals
        /// the scalar data log-likelihood.
        /// </remarks>
        private double[] GetLogJacobianTerms(int count)
        {
            if (_logJacobianTerms != null && _logJacobianTerms.Length == count)
                return _logJacobianTerms;

            var terms = new double[count];
            if (count > 0 && _logJacobian != 0d)
                Array.Fill(terms, _logJacobian / count);
            return terms;
        }

        #endregion
    }
}
