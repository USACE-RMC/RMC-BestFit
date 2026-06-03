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
    /// Moving Average (MA) time-series model with support for data transformations,
    /// training/forecasting splits, and Bayesian estimation features.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The MA(q) model: Y(t) = μ + ε(t) + θ1*ε(t-1) + ... + θq*ε(t-q)
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
    public class MovingAverage : ModelBase, ISimulatable<double[]>
    {
        #region Construction

        /// <summary>
        /// Constructs an empty MA model with default order 1.
        /// </summary>
        public MovingAverage()
        {
            _order = 1;
            _includeIntercept = true;
            SetDefaultParameters();
        }

        /// <summary>
        /// Constructs an MA model with specified parameters.
        /// </summary>
        /// <param name="timeSeries">The time-series data to model.</param>
        /// <param name="order">The MA order (q). Default = 1.</param>
        /// <param name="includeIntercept">Whether to include an intercept term. Default = true.</param>
        public MovingAverage(TimeSeries timeSeries, int order = 1, bool includeIntercept = true)
        {
            _order = order;
            _includeIntercept = includeIntercept;
            TimeSeries = timeSeries;
        }

        /// <summary>
        /// Constructs an MA model by deserializing from XML.
        /// </summary>
        /// <param name="timeSeries">The time-series data to model.</param>
        /// <param name="xElement">The XML element to deserialize.</param>
        public MovingAverage(TimeSeries timeSeries, XElement xElement)
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

            // Set TimeSeries first (this may trigger SetDefaultParameters)
            TimeSeries = timeSeries;

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
                if (_timeSeries != null)
                    _timeSeries.CollectionChanged -= TimeSeries_CollectionChanged;

                _timeSeries = value;

                if (_timeSeries != null)
                {
                    _timeSeries.CollectionChanged += TimeSeries_CollectionChanged;

                    if (_useDefaultTrainingSteps)
                        SetDefaultTrainingSteps();
                    SetTrainingData();
                }

                RaisePropertyChange(nameof(TimeSeries));
                if (_timeSeries != null && UseDefaultFlatPriors)
                    SetDefaultParameters();
            }
        }

        /// <summary>
        /// Gets or sets the MA order (q).
        /// </summary>
        [Category("Inputs")]
        [DisplayName("MA Order (q)")]
        [Description("The number of lagged error terms used in the moving average.")]
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
        /// Prepares the training data by applying transformations.
        /// </summary>
        private void SetTrainingData()
        {
            if (TimeSeries == null || TrainingTimeSteps == 0) return;

            int effectiveTrainingSteps = Math.Min(TrainingTimeSteps, TimeSeries.Count);

            _trainingTimeSeries = new TimeSeries(TimeSeries.TimeInterval);

            if (TransformType == Transform.None)
            {
                _lambda = 0;
                _logJacobian = 0;
                for (int i = 0; i < effectiveTrainingSteps; i++)
                {
                    _trainingTimeSeries.Add(TimeSeries[i].Clone());
                }
            }
            else if (TransformType == Transform.Logarithmic)
            {
                _lambda = 0;
                var data = TimeSeries.ValuesToArray().Subset(0, effectiveTrainingSteps - 1);
                _logJacobian = BoxCox.LogJacobian(data, _lambda);

                for (int i = 0; i < effectiveTrainingSteps; i++)
                {
                    _trainingTimeSeries.Add(TimeSeries[i].Clone());
                    _trainingTimeSeries[i].Value = BoxCox.Transform(TimeSeries[i].Value, _lambda);
                }
            }
            else if (TransformType == Transform.BoxCox)
            {
                BoxCox.FitLambda(TimeSeries.ValuesToList(), out _lambda);
                var data = TimeSeries.ValuesToArray().Subset(0, effectiveTrainingSteps - 1);
                _logJacobian = BoxCox.LogJacobian(data, _lambda);

                for (int i = 0; i < effectiveTrainingSteps; i++)
                {
                    _trainingTimeSeries.Add(TimeSeries[i].Clone());
                    _trainingTimeSeries[i].Value = BoxCox.Transform(TimeSeries[i].Value, _lambda);
                }
            }
            else if (TransformType == Transform.YeoJohnson)
            {
                YeoJohnson.FitLambda(TimeSeries.ValuesToList(), out _lambda);
                var data = TimeSeries.ValuesToArray().Subset(0, effectiveTrainingSteps - 1);
                _logJacobian = YeoJohnson.LogJacobian(data, _lambda);

                for (int i = 0; i < effectiveTrainingSteps; i++)
                {
                    _trainingTimeSeries.Add(TimeSeries[i].Clone());
                    _trainingTimeSeries[i].Value = YeoJohnson.Transform(TimeSeries[i].Value, _lambda);
                }
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Sets the transformation parameters manually.
        /// </summary>
        /// <param name="lambda1">The primary transformation parameter (λ for Box-Cox/Yeo-Johnson).</param>
        /// <param name="lambda2">The offset for handling non-positive values (default = 0).</param>
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

            if (TrainingTimeSeries != null && TrainingTimeSeries.Count > 0)
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
        /// <returns>Array of residuals on the transformed scale.</returns>
        public double[] Residuals(double[] parameters)
        {
            int effectiveTrainingSteps = TrainingTimeSeries != null
                ? Math.Min(TrainingTimeSteps, TrainingTimeSeries.Count)
                : TrainingTimeSteps;

            var epsilon = new double[effectiveTrainingSteps];

            int k = 0;
            double mu = IncludeIntercept ? parameters[k++] : 0;
            var theta = new double[Order];
            for (int i = 0; i < Order; i++)
                theta[i] = parameters[k++];

            for (int t = 0; t < effectiveTrainingSteps; t++)
            {
                double prediction = mu;
                for (int q = 1; q <= Math.Min(t, Order); q++)
                {
                    prediction += theta[q - 1] * epsilon[t - q];
                }
                epsilon[t] = TrainingTimeSeries![t].Value - prediction;
            }

            return epsilon;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// This is a <b>conditional</b> log-likelihood (not the exact /
        /// Kalman-filter / unconditional likelihood). Warm-up residuals at
        /// <c>t &lt; q</c> are computed with <c>ε[t-q] = 0</c>; their contribution to
        /// the sum is finite (small, controlled by σ) and matches R's
        /// <c>arima(method="CSS")</c> conditional likelihood. Validated against
        /// R's conditional-likelihood tests; do not compare against R's default
        /// unconditional likelihood (<c>method="ML"</c>) — they will differ in
        /// the first <c>q</c> residual contributions.
        /// </remarks>
        public override double DataLogLikelihood(double[] parameters)
        {
            if (TrainingTimeSeries == null || TrainingTimeSeries.Count == 0)
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

            for (int t = 0; t < residuals.Length; t++)
            {
                logLH += normDist.LogPDF(residuals[t]);
            }

            return logLH + _logJacobian;
        }

        /// <inheritdoc/>
        public override double[] PointwiseDataLogLikelihood(double[] parameters)
        {
            if (TrainingTimeSeries == null || TrainingTimeSeries.Count == 0)
                return Array.Empty<double>();

            int effectiveTrainingSteps = Math.Min(TrainingTimeSteps, TrainingTimeSeries.Count);
            int n = effectiveTrainingSteps;
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
            for (int t = 0; t < n; t++)
            {
                result[t] = normDist.LogPDF(residuals[t]) + jacobianPerObs;
            }

            return result;
        }

        /// <inheritdoc/>
        public override List<DataComponent> PointwiseDataLogLikelihoodComponents(double[] parameters)
        {
            if (TrainingTimeSeries == null || TrainingTimeSeries.Count == 0)
                return new List<DataComponent>();

            int effectiveTrainingSteps = Math.Min(TrainingTimeSteps, TrainingTimeSeries.Count);
            int n = effectiveTrainingSteps;
            if (n <= 0) return new List<DataComponent>();

            var result = new List<DataComponent>(n);
            var responseValues = TrainingTimeSeries?.ValuesToArray();

            for (int i = 0; i < parameters.Length; i++)
            {
                if (double.IsNaN(parameters[i]))
                {
                    for (int j = 0; j < n; j++)
                    {
                        double value = responseValues != null && j < responseValues.Length ? responseValues[j] : 0;
                        result.Add(new DataComponent(j, double.NegativeInfinity, value, DataComponentType.Exact, 1, $"t={j}"));
                    }
                    return result;
                }
            }

            var residuals = Residuals(parameters);
            double sigma = parameters.Last();
            var normDist = new Normal(0, sigma);
            double jacobianPerObs = _logJacobian / n;

            for (int t = 0; t < n; t++)
            {
                double logLH = normDist.LogPDF(residuals[t]) + jacobianPerObs;
                double value = responseValues != null && t < responseValues.Length ? responseValues[t] : 0;
                result.Add(new DataComponent(t, logLH, value, DataComponentType.Exact, 1, $"t={t}"));
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
        public (double[] Y, double[] InterceptPart, double[] MAPart) Predict(double[] parameters, int forecastSteps = 0, int seed = -1)
        {
            if (TimeSeries == null)
                throw new InvalidOperationException("TimeSeries must be set.");

            int totalSteps = TrainingTimeSteps + forecastSteps;

            var y = new double[totalSteps];
            var interceptPart = new double[totalSteps];
            var maPart = new double[totalSteps];
            var epsilon = new double[totalSteps];

            int k = 0;
            double mu = IncludeIntercept ? parameters[k++] : 0;
            var theta = new double[Order];
            for (int i = 0; i < Order; i++)
                theta[i] = parameters[k++];
            double sigma = parameters[k];

            Random? prng = seed >= 0 ? new Random(seed) : null;
            Normal? errDist = seed >= 0 ? new Normal(0, sigma) : null;

            for (int t = 0; t < totalSteps; t++)
            {
                interceptPart[t] = mu;

                // MA recursion over injected/observed residual history
                double ma = 0;
                for (int q = 1; q <= Math.Min(t, Order); q++)
                {
                    ma += theta[q - 1] * epsilon[t - q];
                }
                maPart[t] = ma;

                double prediction = mu + ma;
                y[t] = prediction;

                // Epsilon in the fit window uses the true one-step residual so the MA
                // recursion stays anchored to observations. Outside the fit window it
                // uses the injected noise so the CI fans out.
                bool inFitWindow = TrainingTimeSeries != null && t < TrainingTimeSteps && t < TrainingTimeSeries.Count;
                if (inFitWindow)
                    epsilon[t] = TrainingTimeSeries![t].Value - prediction;
                else
                    epsilon[t] = 0;

                // Residual noise: draw every step when seeded so CIs wrap observations
                // in training (≈ ±1.96σ) and fan out through MA recursion in forecast.
                if (prng != null)
                {
                    double error = errDist!.InverseCDF(prng.NextDouble());
                    y[t] += error;

                    if (!inFitWindow)
                        epsilon[t] = error;
                }
            }

            // Inverse transform back to the original scale. MA has no differencing or AR
            // carry-over, so only the transform needs reversing. Posterior-median point
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

            return (y, interceptPart, maPart);
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

            var result = new MovingAverage()
            {
                _order = Order,
                _includeIntercept = IncludeIntercept,
                _useDefaultFlatPriors = UseDefaultFlatPriors,
                _useJeffreysRuleForScale = UseJeffreysRuleForScale,
                _transformType = TransformType,
                _trainingTimeSteps = TrainingTimeSteps,
                _useDefaultTrainingSteps = UseDefaultTrainingSteps,
                Parameters = parms
            };

            result.TimeSeries = TimeSeries?.Clone()!;
            return result;
        }

        /// <inheritdoc/>
        public override XElement ToXElement()
        {
            var result = new XElement(nameof(MovingAverage));
            result.SetAttributeValue(nameof(Order), Order.ToString(CultureInfo.InvariantCulture));
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
        /// Checks whether the MA process is invertible based on the current parameter values.
        /// </summary>
        /// <returns><c>true</c> if the MA process is invertible; otherwise, <c>false</c>.</returns>
        public bool IsInvertible()
        {
            if (Parameters == null || Parameters.Count == 0)
                return true;

            int k = IncludeIntercept ? 1 : 0;
            if (Parameters.Count <= k)
                return true;

            var theta = new double[Order];
            for (int i = 0; i < Order; i++)
                theta[i] = Parameters[k + i].Value;

            if (Order == 1)
                return Math.Abs(theta[0]) < 1.0;

            double sumAbsTheta = 0.0;
            for (int i = 0; i < Order; i++)
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

            if (Order < 1 || Order > 10)
            {
                isValid = false;
                messages.Add("Error: MA order must be between 1 and 10.");
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

                if (!IsInvertible())
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
            var normal = new Numerics.Distributions.Normal(0, 1);

            int paramIndex = 0;
            double intercept = 0;
            if (IncludeIntercept)
            {
                intercept = Parameters[paramIndex].Value;
                paramIndex++;
            }

            double[] theta = new double[Order];
            for (int i = 0; i < Order; i++)
            {
                theta[i] = Parameters[paramIndex + i].Value;
            }
            paramIndex += Order;

            double sigma = Parameters[paramIndex].Value;

            // Generate innovations (errors)
            var epsilon = new double[sampleSize];
            for (int i = 0; i < sampleSize; i++)
            {
                epsilon[i] = sigma * normal.InverseCDF(rng.NextDouble());
            }

            // Generate MA process
            var result = new double[sampleSize];
            for (int t = 0; t < sampleSize; t++)
            {
                double value = intercept + epsilon[t];
                // MA component uses past innovations (only available ones)
                for (int j = 0; j < Math.Min(t, Order); j++)
                {
                    value += theta[j] * epsilon[t - 1 - j];
                }
                result[t] = value;
            }

            return result;
        }

        #endregion
    }
}
