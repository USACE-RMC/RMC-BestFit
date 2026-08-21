using Numerics.Distributions;
using Numerics.Data;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Numerics;
using Numerics.Data.Statistics;
using System.Xml.Linq;
using System.Collections.Specialized;
using System.Globalization;

namespace RMC.BestFit.Models
{
    /// <summary>
    /// Autoregressive Moving Average with Exogenous Variables (ARIMAX) time series model.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The ARIMAX model extends ARIMA by incorporating exogenous (external) variables as predictors.
    /// Model structure: Y(t) = μ + γ(t) + ψ(t) + β*X(t) + φ*Y(t-p) + θ*ε(t-q) + ε(t)
    /// where:
    /// - μ is the intercept
    /// - γ(t) is the trend component (linear, quadratic, or cubic)
    /// - ψ(t) is the seasonal component (Fourier series)
    /// - β*X(t) represents exogenous covariates
    /// - φ*Y(t-p) is the autoregressive component of order p
    /// - θ*ε(t-q) is the moving average component of order q
    /// - ε(t) is white noise error
    /// </para>
    /// <para>
    /// The model supports:
    /// - ARMA (p,q): Autoregressive Moving Average
    /// - ARIMA (p,d,q): Integrated ARMA with differencing of order d
    /// - ARIMAX (p,q,b): ARMA with exogenous variables of order b
    /// - Box-Cox and Yeo-Johnson power transformations
    /// - Multiple trend types and seasonal patterns
    /// </para>
    /// <para>
    ///     <b>Authors:</b>
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// </remarks>
    public class ARIMAX : ModelBase, ISimulatable<double[]>
    {
        #region Construction

        /// <summary>
        /// Constructs a new ARIMAX model with default parameters.
        /// </summary>
        public ARIMAX()
        {
            SetDefaultParameters();
        }

        /// <summary>
        /// Constructs a new ARIMAX model with time-series data.
        /// </summary>
        /// <param name="timeSeries">The time-series data to model.</param>
        public ARIMAX(TimeSeries timeSeries)
        {
            TimeSeries = timeSeries;
            SetTrainingData();
            SetDefaultParameters();
        }

        /// <summary>
        /// Constructs a new ARIMAX model by deserializing from XML.
        /// </summary>
        /// <param name="timeSeries">The time-series data to model.</param>
        /// <param name="xElement">The XElement containing serialized model configuration.</param>
        public ARIMAX(TimeSeries timeSeries, XElement xElement)
        {
            TimeSeries = timeSeries;

            var transformAttr = xElement.Attribute(nameof(TransformType));
            if (transformAttr != null)
                Enum.TryParse(transformAttr.Value, out _transformType);
            var covExtAttr = xElement.Attribute(nameof(CovariateExtension));
            if (covExtAttr != null)
                Enum.TryParse(covExtAttr.Value, out _covariateExtension);
            var interceptAttr = xElement.Attribute(nameof(IncludeIntercept));
            if (interceptAttr != null)
                bool.TryParse(interceptAttr.Value, out _includeIntercept);
            var seasonalityAttr = xElement.Attribute(nameof(IncludeSeasonality));
            if (seasonalityAttr != null)
                bool.TryParse(seasonalityAttr.Value, out _includeSeasonality);
            var trendAttr = xElement.Attribute(nameof(TrendType));
            if (trendAttr != null)
                Enum.TryParse(trendAttr.Value, out _trendType);
            var arOrderAttr = xElement.Attribute(nameof(AROrderP));
            if (arOrderAttr != null)
                int.TryParse(arOrderAttr.Value, out _arOrderP);
            var diffOrderAttr = xElement.Attribute(nameof(DiffOrderD));
            if (diffOrderAttr != null)
                int.TryParse(diffOrderAttr.Value, out _diffOrderD);
            var maOrderAttr = xElement.Attribute(nameof(MAOrderQ));
            if (maOrderAttr != null)
                int.TryParse(maOrderAttr.Value, out _maOrderQ);
            var xOrderAttr = xElement.Attribute(nameof(XOrderB));
            if (xOrderAttr != null)
                int.TryParse(xOrderAttr.Value, out _xOrderB);
            var trainingStepsAttr = xElement.Attribute(nameof(TrainingTimeSteps));
            if (trainingStepsAttr != null)
                int.TryParse(trainingStepsAttr.Value, out _trainingTimeSteps);
            var defaultTrainingAttr = xElement.Attribute(nameof(UseDefaultTrainingSteps));
            if (defaultTrainingAttr != null)
                bool.TryParse(defaultTrainingAttr.Value, out _useDefaultTrainingSteps);

            // Parameters
            var flatPriorsAttr = xElement.Attribute(nameof(UseDefaultFlatPriors));
            if (flatPriorsAttr != null)
                bool.TryParse(flatPriorsAttr.Value, out _useDefaultFlatPriors);
            var jeffreysAttr = xElement.Attribute(nameof(UseJeffreysRuleForScale));
            if (jeffreysAttr != null)
                bool.TryParse(jeffreysAttr.Value, out _useJeffreysRuleForScale);
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

            var parms = new List<ModelParameter>();
            var parmsElement = xElement.Element(nameof(Parameters));
            if (parmsElement != null)
            {
                foreach (XElement p in parmsElement.Elements(nameof(ModelParameter)))
                    parms.Add(new ModelParameter(p));
            }
            Parameters = parms;

            if (persistedTransformLambda.HasValue)
            {
                _lambda = persistedTransformLambda.Value;
                _transformLambdaIsManual = persistedTransformLambdaIsManual;
                _usePersistedTransformLambda = true;
            }
            SetTrainingData();
            _usePersistedTransformLambda = false;
        }

        #endregion

        #region Members

        private TimeSeries _timeSeries = null!;
        private TimeSeries _transformedTimeSeries = null!;
        private TimeSeries _diffSeries = null!;
        private TimeSeries _trainingTimeSeries = null!;
        private Transform _transformType = Transform.None;
        private double _lambda = 0;
        private bool _transformLambdaIsManual;
        private bool _usePersistedTransformLambda;
        private double _logJacobian = 0;
        private double[]? _logJacobianTerms;
        private string? _transformFitValidationMessage;
        private bool _includeIntercept = true;
        private bool _includeSeasonality = false;
        private int _seasonalPeriod = 12;
        private Trend _trendType = Trend.None;
        private List<TimeSeries> _covariates = null!;
        private int _arOrderP = 1;
        private int _diffOrderD = 0;
        private int _maOrderQ = 0;
        private int _xOrderB = 0;
        private bool _useJeffreysRuleForScale = true;
        private int _trainingTimeSteps;
        private bool _useDefaultTrainingSteps = true;
        private int[,,]? _trainingCovariatePositions;
        private readonly List<string> _covariateAlignmentValidationMessages = new();
        private readonly HashSet<SeriesOrdinate<DateTime, double>> _subscribedCovariateOrdinates = new();

        /// <summary>
        /// Enumeration of trend types for the time series model.
        /// </summary>
        public enum Trend
        {
            /// <summary>No trend component.</summary>
            None,
            /// <summary>Linear trend: γ*t</summary>
            Linear,
            /// <summary>Quadratic trend: γ1*t + γ2*t²</summary>
            Quadratic,
            /// <summary>Cubic trend: γ1*t + γ2*t² + γ3*t³</summary>
            Cubic,
        }

        /// <summary>
        /// Enumeration of methods for extending covariate data beyond available observations.
        /// Used in <see cref="Predict"/> and <see cref="GenerateRandomValues"/> when covariates
        /// are required but not available for all time steps.
        /// </summary>
        public enum CovariateExtensionMethod
        {
            /// <summary>
            /// No extension. Covariates must be provided for all required time steps.
            /// An exception will be thrown if covariates are insufficient.
            /// </summary>
            None,

            /// <summary>
            /// Extend covariates using block bootstrap resampling.
            /// Preserves temporal autocorrelation within blocks.
            /// Block size is automatically set to min(10, N/4) where N is the covariate length.
            /// </summary>
            BlockBootstrap,

            /// <summary>
            /// Extend covariates using k-Nearest Neighbors resampling.
            /// Preserves local covariate structure by sampling from similar historical values.
            /// k is automatically set to max(3, N/10) where N is the covariate length.
            /// </summary>
            KNN,
        }

        private CovariateExtensionMethod _covariateExtension = CovariateExtensionMethod.BlockBootstrap;

        /// <summary>
        /// Gets or sets the time series data to be modeled.
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
                    UpdateSeasonalPeriod();
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
        /// Gets the transformed and differenced time series used for model calibration.
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
        /// Gets the differenced (but not transformed) time series.
        /// </summary>
        public TimeSeries DifferencedSeries => _diffSeries;

        /// <summary>
        /// Gets the list of exogenous regression covariates.
        /// </summary>
        public List<TimeSeries> Covariates => _covariates;

        /// <summary>
        /// Gets or sets the method used to extend covariate data when forecasting or generating
        /// values beyond the available covariate observations.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This property controls how <see cref="Predict"/> and <see cref="GenerateRandomValues"/>
        /// handle cases where covariates are needed but not available for all time steps:
        /// </para>
        /// <list type="bullet">
        /// <item><description><see cref="CovariateExtensionMethod.None"/>: No extension is performed.
        /// An exception is thrown if covariates are insufficient.</description></item>
        /// <item><description><see cref="CovariateExtensionMethod.BlockBootstrap"/>: Extends covariates
        /// using block bootstrap resampling, preserving temporal autocorrelation.</description></item>
        /// <item><description><see cref="CovariateExtensionMethod.KNN"/>: Extends covariates using
        /// k-Nearest Neighbors, preserving local covariate structure.</description></item>
        /// </list>
        /// <para>Default is <see cref="CovariateExtensionMethod.BlockBootstrap"/>.</para>
        /// </remarks>
        [Category("Inputs")]
        [DisplayName("Covariate Extension")]
        [Description("Method used to extend covariate data when forecasting beyond available observations.")]
        [Browsable(true)]
        public CovariateExtensionMethod CovariateExtension
        {
            get { return _covariateExtension; }
            set
            {
                if (_covariateExtension != value)
                {
                    _covariateExtension = value;
                    RaisePropertyChange(nameof(CovariateExtension));
                }
            }
        }

        /// <summary>
        /// Gets the seasonal period inferred from the time series interval.
        /// </summary>
        [Category("General")]
        [DisplayName("Seasonal Period")]
        [Description("The number of time steps in one seasonal cycle (automatically inferred from time interval).")]
        [Browsable(true)]
        public int SeasonalPeriod => _seasonalPeriod;

        /// <summary>
        /// Gets or sets whether to include an intercept term (μ) in the model.
        /// </summary>
        [Category("Inputs")]
        [DisplayName("Include Intercept")]
        [Description("Determines whether to include an intercept term in the model.")]
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
        /// Gets or sets whether to include a Fourier series seasonal component.
        /// </summary>
        [Category("Inputs")]
        [DisplayName("Include Seasonality")]
        [Description("Determines whether to include a Fourier series seasonal component: ψ1*sin(2π*t/S) + ψ2*cos(2π*t/S), where S is the seasonal period.")]
        [Browsable(true)]
        public bool IncludeSeasonality
        {
            get { return _includeSeasonality; }
            set
            {
                if (_includeSeasonality != value)
                {
                    _includeSeasonality = value;
                    RaisePropertyChange(nameof(IncludeSeasonality));
                    SetDefaultParameters();
                }
            }
        }

        /// <summary>
        /// Gets or sets the trend type (None, Linear, Quadratic, or Cubic).
        /// </summary>
        [Category("Inputs")]
        [DisplayName("Trend Type")]
        [Description("Specifies the deterministic trend type.")]
        [Browsable(true)]
        public Trend TrendType
        {
            get { return _trendType; }
            set
            {
                if (_trendType != value)
                {
                    _trendType = value;
                    RaisePropertyChange(nameof(TrendType));
                    SetDefaultParameters();
                }
            }
        }

        /// <summary>
        /// Gets or sets the autoregressive order (p).
        /// </summary>
        [Category("Inputs")]
        [DisplayName("AR Order (p)")]
        [Description("The order (p) of the Autoregressive component: φ1*Y(t-1) + ... + φp*Y(t-p).")]
        [Browsable(true)]
        public int AROrderP
        {
            get { return _arOrderP; }
            set
            {
                if (_arOrderP != value)
                {
                    _arOrderP = value;
                    RaisePropertyChange(nameof(AROrderP));
                    SetTrainingData();
                    SetDefaultParameters();
                }
            }
        }

        /// <summary>
        /// Gets or sets the differencing order (d) for achieving stationarity.
        /// </summary>
        [Category("Inputs")]
        [DisplayName("Diff Order (d)")]
        [Description("The order (d) of differencing applied to achieve stationarity (ARIMA models).")]
        [Browsable(true)]
        public int DiffOrderD
        {
            get { return _diffOrderD; }
            set
            {
                if (_diffOrderD != value)
                {
                    _diffOrderD = value;
                    RaisePropertyChange(nameof(DiffOrderD));
                    SetTrainingData();
                    SetDefaultParameters();
                }
            }
        }

        /// <summary>
        /// Gets or sets the moving average order (q).
        /// </summary>
        [Category("Inputs")]
        [DisplayName("MA Order (q)")]
        [Description("The order (q) of the Moving Average component: θ1*ε(t-1) + ... + θq*ε(t-q).")]
        [Browsable(true)]
        public int MAOrderQ
        {
            get { return _maOrderQ; }
            set
            {
                if (_maOrderQ != value)
                {
                    _maOrderQ = value;
                    RaisePropertyChange(nameof(MAOrderQ));
                    SetTrainingData();
                    SetDefaultParameters();
                }
            }
        }

        /// <summary>
        /// Gets or sets the exogenous variable lag order (b).
        /// </summary>
        [Category("Inputs")]
        [DisplayName("X Order (b)")]
        [Description("The lag order (b) for exogenous variables. If b=0, current values are used; if b>0, lagged values X(t-b) are used.")]
        [Browsable(true)]
        public int XOrderB
        {
            get { return _xOrderB; }
            set
            {
                if (_xOrderB != value)
                {
                    _xOrderB = value;
                    RaisePropertyChange(nameof(XOrderB));
                    RebuildTrainingCovariateAlignment();
                    SetDefaultParameters();
                }
            }
        }

        /// <summary>
        /// Gets or sets whether to use Jeffreys' rule prior (1/σ) for the scale parameter.
        /// </summary>
        [Category("Inputs")]
        [DisplayName("Use Jeffreys' Rule for Scale")]
        [Description("Determines whether to use Jeffreys' rule for the scale (σ) parameter: P(σ) ∝ 1/σ.")]
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

        /// <summary>
        /// Gets or sets the number of time steps used for model training.
        /// </summary>
        [Category("General")]
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
        /// Gets or sets whether to automatically set training steps to 80% of available data.
        /// </summary>
        [Category("Inputs")]
        [DisplayName("Use Default Training Steps")]
        [Description("Determines whether to automatically set training steps to 80% of the time series length (minimum 30 or parameter count).")]
        [Browsable(true)]
        public bool UseDefaultTrainingSteps
        {
            get { return _useDefaultTrainingSteps; }
            set
            {
                if (_useDefaultTrainingSteps != value)
                {
                    _useDefaultTrainingSteps = value;
                    RaisePropertyChange(nameof(UseDefaultTrainingSteps));
                    if (_useDefaultTrainingSteps)
                        SetDefaultTrainingSteps();
                }
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Handles changes to the time series collection.
        /// </summary>
        private void TimeSeries_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            UpdateSeasonalPeriod();

            if (_useDefaultTrainingSteps)
                SetDefaultTrainingSteps();

            SetTrainingData();
            RaisePropertyChange(nameof(TimeSeries));

            if (UseDefaultFlatPriors)
                SetDefaultParameters();
        }

        /// <summary>
        /// Sets the list of exogenous covariate time series.
        /// </summary>
        /// <param name="covariates">List of time series to use as exogenous predictors.</param>
        public void SetCovariates(List<TimeSeries> covariates)
        {
            DetachCovariateSubscriptions();
            _covariates = covariates;
            AttachCovariateSubscriptions();
            RebuildTrainingCovariateAlignment();
            RaisePropertyChange(nameof(Covariates));
            SetDefaultParameters();
        }

        /// <summary>
        /// Attaches change handlers to configured covariate collections and ordinates.
        /// </summary>
        private void AttachCovariateSubscriptions()
        {
            if (_covariates == null)
                return;

            foreach (TimeSeries covariate in _covariates)
                covariate.CollectionChanged += Covariate_CollectionChanged;
            RefreshCovariateOrdinateSubscriptions();
        }

        /// <summary>
        /// Detaches every covariate collection and ordinate change handler.
        /// </summary>
        private void DetachCovariateSubscriptions()
        {
            if (_covariates != null)
            {
                foreach (TimeSeries covariate in _covariates)
                    covariate.CollectionChanged -= Covariate_CollectionChanged;
            }

            foreach (SeriesOrdinate<DateTime, double> ordinate in _subscribedCovariateOrdinates)
                ordinate.PropertyChanged -= CovariateOrdinate_PropertyChanged;
            _subscribedCovariateOrdinates.Clear();
        }

        /// <summary>
        /// Refreshes ordinate-level subscriptions after a covariate collection changes.
        /// </summary>
        private void RefreshCovariateOrdinateSubscriptions()
        {
            foreach (SeriesOrdinate<DateTime, double> ordinate in _subscribedCovariateOrdinates)
                ordinate.PropertyChanged -= CovariateOrdinate_PropertyChanged;
            _subscribedCovariateOrdinates.Clear();

            if (_covariates == null)
                return;

            foreach (TimeSeries covariate in _covariates)
            {
                foreach (SeriesOrdinate<DateTime, double> ordinate in covariate)
                {
                    if (_subscribedCovariateOrdinates.Add(ordinate))
                        ordinate.PropertyChanged += CovariateOrdinate_PropertyChanged;
                }
            }
        }

        /// <summary>
        /// Rebuilds date alignment when a configured covariate series changes.
        /// </summary>
        /// <param name="sender">The covariate series that changed.</param>
        /// <param name="e">The collection-change event data.</param>
        private void Covariate_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            RefreshCovariateOrdinateSubscriptions();
            RefreshCovariateDependentState();
        }

        /// <summary>
        /// Rebuilds date alignment when a covariate ordinate timestamp or value changes.
        /// </summary>
        /// <param name="sender">The changed covariate ordinate.</param>
        /// <param name="e">The property-change event data.</param>
        private void CovariateOrdinate_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            RefreshCovariateDependentState();
        }

        /// <summary>
        /// Refreshes alignment, notifications, and flat-prior defaults after a covariate change.
        /// </summary>
        private void RefreshCovariateDependentState()
        {
            RebuildTrainingCovariateAlignment();
            RaisePropertyChange(nameof(Covariates));
            if (UseDefaultFlatPriors)
                SetDefaultParameters();
        }

        /// <summary>
        /// Sets the default number of training time steps to 80% of the series length.
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
        /// A new response series represents a new calibration problem. Manual training-window edits from
        /// the previous series are therefore discarded, while analysis-layer forecast settings remain
        /// outside this model and are preserved by their owning analysis.
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
        /// Updates the inferred seasonal period and notifies bindings when the interval scale changes.
        /// </summary>
        /// <remarks>
        /// The period is derived from <see cref="TimeSeries.TimeInterval"/> rather than user input, so
        /// assigning or replacing the response series is the authoritative trigger.
        /// </remarks>
        private void UpdateSeasonalPeriod()
        {
            int oldSeasonalPeriod = _seasonalPeriod;
            _seasonalPeriod = InferSeasonalPeriod();
            if (_seasonalPeriod != oldSeasonalPeriod)
                RaisePropertyChange(nameof(SeasonalPeriod));
        }

        /// <summary>
        /// Creates the training data by applying transformation then differencing.
        /// </summary>
        /// <remarks>
        /// Processing order: (1) Transformation, (2) Differencing. Transform-first matches
        /// ARIMA in this library and R's forecast::forecast.Arima. Variance stabilization on
        /// the raw scale is well-defined; differencing log-values produces log-ratios which
        /// is what the AR/MA structure is intended to model. Box-Cox refuses non-positive
        /// inputs; the raw series must be positive (Validate() enforces this).
        /// </remarks>
        /// <param name="notifyTransformLambda">Whether to notify observers when the effective exponent changes.</param>
        private void SetTrainingData(bool notifyTransformLambda = true)
        {
            double previousLambda = _lambda;
            try
            {
                _transformFitValidationMessage = null;
                _logJacobian = 0;
                _logJacobianTerms = null;
                _trainingCovariatePositions = null;
                _covariateAlignmentValidationMessages.Clear();
                if (TransformType == Transform.None || TransformType == Transform.Logarithmic)
                {
                    _lambda = 0;
                    _transformLambdaIsManual = false;
                    _usePersistedTransformLambda = false;
                }
                if (TimeSeries == null)
                {
                    _transformedTimeSeries = null!;
                    _diffSeries = null!;
                    _trainingTimeSeries = null!;
                    return;
                }

                _transformedTimeSeries = new TimeSeries(TimeSeries.TimeInterval);
                _diffSeries = new TimeSeries(TimeSeries.TimeInterval);
                _trainingTimeSeries = new TimeSeries(TimeSeries.TimeInterval);
                int effectiveRawTrainingSteps = Math.Min(TrainingTimeSteps, TimeSeries.Count);

                if (TransformType == Transform.None || TransformType == Transform.Logarithmic)
                {
                    _lambda = 0;
                    _transformLambdaIsManual = false;
                    _usePersistedTransformLambda = false;
                }
                else if (effectiveRawTrainingSteps == 0)
                {
                    _lambda = 0;
                    return;
                }
                else if (!_transformLambdaIsManual && !_usePersistedTransformLambda)
                {
                    var fittingValues = TimeSeries.ValuesToArray().Subset(0, effectiveRawTrainingSteps - 1);
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
                        System.Diagnostics.Debug.WriteLine($"ARIMAX.SetTrainingData: {_transformFitValidationMessage}");
                        System.Diagnostics.Debug.WriteLine(ex);
                        return;
                    }

                    if (!double.IsFinite(_lambda))
                    {
                        _lambda = 0;
                        string transformName = TransformType == Transform.BoxCox ? "Box-Cox" : "Yeo-Johnson";
                        _transformFitValidationMessage = $"Error: {transformName} lambda estimation failed. Select a different transform or revise the time-series data.";
                        System.Diagnostics.Debug.WriteLine($"ARIMAX.SetTrainingData: {_transformFitValidationMessage}");
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

                _diffSeries = DifferenceWithLaterTimestamps(_transformedTimeSeries, DiffOrderD);

                int effectiveTrainingSteps = Math.Max(0, effectiveRawTrainingSteps - DiffOrderD);
                _trainingTimeSeries = new TimeSeries(_diffSeries.TimeInterval);
                for (int i = 0; i < effectiveTrainingSteps; i++)
                    _trainingTimeSeries.Add(_diffSeries[i].Clone());

                int maxOrder = ConditionalOrder;
                int startRawIdx = DiffOrderD + maxOrder;
                int endRawIdx = effectiveRawTrainingSteps - 1;
                if (TransformType != Transform.None && endRawIdx >= startRawIdx)
                {
                    var rawSubset = TimeSeries.ValuesToArray().Subset(startRawIdx, endRawIdx);
                    _logJacobian = TransformType == Transform.YeoJohnson
                        ? YeoJohnson.LogJacobian(rawSubset, _lambda)
                        : BoxCox.LogJacobian(rawSubset, _lambda);
                    _logJacobianTerms = ComputeLogJacobianTerms(rawSubset);
                }

                RebuildTrainingCovariateAlignment();
            }
            finally
            {
                if (notifyTransformLambda && _lambda != previousLambda)
                    RaisePropertyChange(nameof(TransformLambda));
            }
        }

        /// <summary>
        /// Differences a series while assigning each result the timestamp of its later raw value.
        /// </summary>
        /// <param name="series">The transformed level series.</param>
        /// <param name="order">The number of successive first differences.</param>
        /// <returns>The differenced series with date-preserving later-value timestamps.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="order"/> is negative.</exception>
        private static TimeSeries DifferenceWithLaterTimestamps(TimeSeries series, int order)
        {
            if (order < 0)
                throw new ArgumentOutOfRangeException(nameof(order), "Differencing order cannot be negative.");

            TimeSeries result = series.Clone();
            for (int difference = 0; difference < order; difference++)
            {
                var next = new TimeSeries(result.TimeInterval);
                for (int i = 1; i < result.Count; i++)
                {
                    var ordinate = result[i].Clone();
                    ordinate.Value = result[i].Value - result[i - 1].Value;
                    next.Add(ordinate);
                }

                result = next;
            }

            return result;
        }

        /// <summary>
        /// Rebuilds the exact-date map from transformed/differenced model steps to level covariates.
        /// </summary>
        /// <remarks>
        /// Model step <c>k</c> maps to raw response index <c>k + d</c>. Covariates remain on their
        /// level scale and are selected by the response timestamp at that raw index. Lagged
        /// covariates use preceding model-step timestamps; dates outside the required window are
        /// ignored.
        /// </remarks>
        private void RebuildTrainingCovariateAlignment()
        {
            _trainingCovariatePositions = null;
            _covariateAlignmentValidationMessages.Clear();

            if (TimeSeries == null || _trainingTimeSeries == null || _trainingTimeSeries.Count == 0 ||
                Covariates == null || Covariates.Count == 0 || XOrderB < 0)
            {
                return;
            }

            int trainingCount = _trainingTimeSeries.Count;
            _trainingCovariatePositions = new int[Covariates.Count, trainingCount, XOrderB + 1];
            for (int covariateIndex = 0; covariateIndex < Covariates.Count; covariateIndex++)
            {
                TimeSeries covariate = Covariates[covariateIndex];
                var positionsByDate = new Dictionary<DateTime, List<int>>();
                for (int position = 0; position < covariate.Count; position++)
                {
                    DateTime date = covariate[position].Index;
                    if (!positionsByDate.TryGetValue(date, out List<int>? positions))
                    {
                        positions = new List<int>();
                        positionsByDate.Add(date, positions);
                    }

                    positions.Add(position);
                }

                var reportedMissing = new HashSet<DateTime>();
                var reportedDuplicate = new HashSet<DateTime>();
                for (int modelIndex = 0; modelIndex < trainingCount; modelIndex++)
                {
                    for (int lag = 0; lag <= XOrderB; lag++)
                    {
                        _trainingCovariatePositions[covariateIndex, modelIndex, lag] = -1;
                        if (lag > modelIndex)
                            continue;

                        DateTime requiredDate = _trainingTimeSeries[modelIndex - lag].Index;
                        int rawIndex = DiffOrderD + modelIndex - lag;
                        if (!positionsByDate.TryGetValue(requiredDate, out List<int>? matches))
                        {
                            if (reportedMissing.Add(requiredDate))
                            {
                                _covariateAlignmentValidationMessages.Add(
                                    $"Error: Covariate {covariateIndex + 1} is missing required timestamp {requiredDate:O} for raw response index {rawIndex}.");
                            }
                            continue;
                        }

                        if (matches.Count != 1)
                        {
                            if (reportedDuplicate.Add(requiredDate))
                            {
                                _covariateAlignmentValidationMessages.Add(
                                    $"Error: Covariate {covariateIndex + 1} contains duplicate required timestamp {requiredDate:O} for raw response index {rawIndex}.");
                            }
                            continue;
                        }

                        _trainingCovariatePositions[covariateIndex, modelIndex, lag] = matches[0];
                    }
                }
            }
        }

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

        /// <summary>
        /// Infers the seasonal period from the time series time interval.
        /// </summary>
        /// <returns>The number of time steps in one seasonal cycle.</returns>
        /// <summary>
        /// Clones <paramref name="observed"/> and appends <paramref name="tail"/> values using
        /// the observed series' time interval to advance timestamps. Used by covariate
        /// extension to preserve observed covariate values across posterior realizations.
        /// </summary>
        private static TimeSeries AppendTail(TimeSeries observed, TimeSeries tail)
        {
            var extended = observed.Clone();
            DateTime next = extended.Count > 0
                ? TimeSeries.AddTimeInterval(extended[extended.Count - 1].Index, extended.TimeInterval)
                : observed.StartDate;
            for (int j = 0; j < tail.Count; j++)
            {
                extended.Add(new SeriesOrdinate<DateTime, double>(next, tail[j].Value));
                next = TimeSeries.AddTimeInterval(next, extended.TimeInterval);
            }
            return extended;
        }

        /// <summary>
        /// Clones <paramref name="observed"/> and appends a constant-valued tail of length
        /// <paramref name="tailLength"/>. Used by Predict's deterministic mode (seed == -1)
        /// so the "deterministic forecast" trace does not depend on a particular bootstrap
        /// or KNN realization.
        /// </summary>
        private static TimeSeries AppendConstantTail(TimeSeries observed, double constant, int tailLength)
        {
            var extended = observed.Clone();
            DateTime next = extended.Count > 0
                ? TimeSeries.AddTimeInterval(extended[extended.Count - 1].Index, extended.TimeInterval)
                : observed.StartDate;
            for (int j = 0; j < tailLength; j++)
            {
                extended.Add(new SeriesOrdinate<DateTime, double>(next, constant));
                next = TimeSeries.AddTimeInterval(next, extended.TimeInterval);
            }
            return extended;
        }

        /// <summary>
        /// Supports the <c>InferSeasonalPeriod</c> helper.
        /// </summary>
        /// <returns>The result.</returns>
        /// <remarks>
        /// This member supports the owning analysis or model implementation.
        /// </remarks>
        private int InferSeasonalPeriod()
        {
            if (TimeSeries == null) return 12; // Default fallback

            switch (TimeSeries.TimeInterval)
            {
                case TimeInterval.OneMinute:
                    return 1440; // Daily cycle (minutes per day)

                case TimeInterval.FiveMinute:
                    return 288; // Daily cycle

                case TimeInterval.FifteenMinute:
                    return 96; // Daily cycle

                case TimeInterval.ThirtyMinute:
                    return 48; // Daily cycle

                case TimeInterval.OneHour:
                    return 24; // Daily cycle

                case TimeInterval.SixHour:
                    return 4; // Daily cycle

                case TimeInterval.TwelveHour:
                    return 2; // Daily cycle

                case TimeInterval.OneDay:
                    return 365; // Annual cycle

                case TimeInterval.SevenDay:
                    return 52; // Annual cycle

                case TimeInterval.OneMonth:
                    return 12; // Annual cycle

                case TimeInterval.OneQuarter:
                    return 4; // Annual cycle

                case TimeInterval.OneYear:
                    return 10; // Decadal cycle for annual records

                default:
                    return 12;
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

            int N = 0;
            double mean = 0;
            double min = -10;
            double max = 10;
            double range = max - min;
            double delta1 = 0, delta2 = 0, delta3 = 0;
            double sigma = 1;
            double sigmaLB = Tools.DoubleMachineEpsilon;
            double sigmaUB = 10;

            // Derive defaults only from the transformed/differenced training prefix. The full
            // differenced series also contains holdout values and must not influence calibration.
            if (_trainingTimeSeries != null && _trainingTimeSeries.Count > 0)
            {
                N = _trainingTimeSeries.Count;
                mean = _trainingTimeSeries.MeanValue();
                sigma = _trainingTimeSeries.StandardDeviation();

                // Match ARIMA's min/max calculation based on mean
                double tempMin = Math.Sign(mean) * Math.Pow(10, Math.Floor(Math.Log10(Math.Abs(mean)) - 1));
                double tempMax = Math.Sign(mean) * Math.Pow(10, Math.Ceiling(Math.Log10(Math.Abs(mean)) + 1));
                min = Math.Min(tempMin, tempMax);
                max = Math.Max(tempMin, tempMax);

                if (double.IsNaN(min) || double.IsInfinity(min)) min = -1000;
                if (double.IsNaN(max) || double.IsInfinity(max)) max = 1000;
                if (min >= max) { min = mean - 100; max = mean + 100; }

                range = max - min;

                // Trend parameter scales
                delta1 = (_trainingTimeSeries[^1].Value - _trainingTimeSeries.First().Value) / N;
                delta2 = delta1 / N;
                delta3 = delta2 / N;
                delta1 = Math.Pow(10, Math.Floor(Math.Log10(Math.Abs(delta1)) + 1));
                delta2 = Math.Pow(10, Math.Floor(Math.Log10(Math.Abs(delta2)) + 1));
                delta3 = Math.Pow(10, Math.Floor(Math.Log10(Math.Abs(delta3)) + 1));

                // Match ARIMA's sigmaUB calculation using Ceiling
                sigmaUB = Math.Pow(10, Math.Ceiling(Math.Log10(sigma) + 1));
                if (double.IsNaN(sigmaUB) || double.IsInfinity(sigmaUB)) sigmaUB = 100;
            }

            _parameters = new List<ModelParameter>();

            // Initial estimates for trend parameters
            // Use polynomial regression for intercept (good starting point)
            // Keep gamma2/gamma3 at 0 to ensure they stay within bounds
            double interceptInit = mean;
            double gamma1Init = 0, gamma2Init = 0, gamma3Init = 0;

            if (TrendType != Trend.None && _trainingTimeSeries != null && _trainingTimeSeries.Count > 1)
            {
                int n = _trainingTimeSeries.Count;
                var y = new double[n];
                for (int i = 0; i < n; i++)
                    y[i] = _trainingTimeSeries[i].Value;

                if (TrendType == Trend.Linear)
                {
                    // Linear regression: Y = a + b*t
                    double sumT = 0, sumT2 = 0, sumY = 0, sumTY = 0;
                    for (int t = 0; t < n; t++)
                    {
                        sumT += t;
                        sumT2 += (double)t * t;
                        sumY += y[t];
                        sumTY += t * y[t];
                    }
                    double denom = n * sumT2 - sumT * sumT;
                    if (Math.Abs(denom) > 1e-10)
                    {
                        gamma1Init = (n * sumTY - sumT * sumY) / denom;
                        interceptInit = (sumY - gamma1Init * sumT) / n;
                    }
                }
                else if (TrendType == Trend.Quadratic)
                {
                    // Quadratic regression: Y = a + b*t + c*t² for intercept only
                    double s0 = n, s1 = 0, s2 = 0, s3 = 0, s4 = 0;
                    double sy = 0, sty = 0, st2y = 0;
                    for (int t = 0; t < n; t++)
                    {
                        double t2 = (double)t * t;
                        s1 += t;
                        s2 += t2;
                        s3 += t * t2;
                        s4 += t2 * t2;
                        sy += y[t];
                        sty += t * y[t];
                        st2y += t2 * y[t];
                    }
                    double det = s0 * (s2 * s4 - s3 * s3) - s1 * (s1 * s4 - s2 * s3) + s2 * (s1 * s3 - s2 * s2);
                    if (Math.Abs(det) > 1e-10)
                    {
                        interceptInit = (sy * (s2 * s4 - s3 * s3) - s1 * (sty * s4 - st2y * s3) + s2 * (sty * s3 - st2y * s2)) / det;
                        // gamma1Init and gamma2Init stay at 0 to ensure within bounds
                    }
                }
                else if (TrendType == Trend.Cubic)
                {
                    // Use first observation as intercept for cubic
                    interceptInit = y[0];
                    // gamma1Init, gamma2Init, gamma3Init stay at 0 to ensure within bounds
                }
            }

            // Intercept
            if (IncludeIntercept)
            {
                Parameters.Add(new ModelParameter()
                {
                    Name = "Intercept (μ)",
                    Value = interceptInit,
                    LowerBound = min,
                    UpperBound = max,
                    PriorDistribution = new Uniform(min, max)
                });
            }

            // Trend parameters
            if (TrendType == Trend.Linear)
            {
                Parameters.Add(new ModelParameter()
                {
                    Name = "Trend (γ)",
                    Value = gamma1Init,
                    LowerBound = -delta1,
                    UpperBound = delta1,
                    PriorDistribution = new Uniform(-delta1, delta1)
                });
            }
            else if (TrendType == Trend.Quadratic)
            {
                Parameters.Add(new ModelParameter()
                {
                    Name = "Trend (γ₁)",
                    Value = gamma1Init,
                    LowerBound = -delta1,
                    UpperBound = delta1,
                    PriorDistribution = new Uniform(-delta1, delta1)
                });
                Parameters.Add(new ModelParameter()
                {
                    Name = "Trend (γ₂)",
                    Value = gamma2Init,
                    LowerBound = -delta2,
                    UpperBound = delta2,
                    PriorDistribution = new Uniform(-delta2, delta2)
                });
            }
            else if (TrendType == Trend.Cubic)
            {
                Parameters.Add(new ModelParameter()
                {
                    Name = "Trend (γ₁)",
                    Value = gamma1Init,
                    LowerBound = -delta1,
                    UpperBound = delta1,
                    PriorDistribution = new Uniform(-delta1, delta1)
                });
                Parameters.Add(new ModelParameter()
                {
                    Name = "Trend (γ₂)",
                    Value = gamma2Init,
                    LowerBound = -delta2,
                    UpperBound = delta2,
                    PriorDistribution = new Uniform(-delta2, delta2)
                });
                Parameters.Add(new ModelParameter()
                {
                    Name = "Trend (γ₃)",
                    Value = gamma3Init,
                    LowerBound = -delta3,
                    UpperBound = delta3,
                    PriorDistribution = new Uniform(-delta3, delta3)
                });
            }

            // Seasonality parameters - Fourier series approach
            if (IncludeSeasonality)
            {
                // Amplitude bounds based on data range
                double amplitude = range / 4;

                Parameters.Add(new ModelParameter()
                {
                    Name = "Seasonality Sin (ψ₁)",
                    Value = 0,
                    LowerBound = -amplitude,
                    UpperBound = amplitude,
                    PriorDistribution = new Uniform(-amplitude, amplitude)
                });
                Parameters.Add(new ModelParameter()
                {
                    Name = "Seasonality Cos (ψ₂)",
                    Value = 0,
                    LowerBound = -amplitude,
                    UpperBound = amplitude,
                    PriorDistribution = new Uniform(-amplitude, amplitude)
                });
            }

            // Covariate parameters
            if (Covariates != null && Covariates.Count > 0)
            {
                for (int i = 1; i <= Covariates.Count; i++)
                {
                    // Number of covariate parameters: 1 for current + XOrderB for lags
                    int numCovParams = XOrderB + 1;

                    if (XOrderB == 0)
                    {
                        // Only current value
                        Parameters.Add(new ModelParameter()
                        {
                            Name = "Covariate (β" + SubscriptFormatter.ToSubscript(i) + ")",
                            Value = 0,
                            LowerBound = -10,
                            UpperBound = 10,
                            PriorDistribution = new Uniform(-10, 10)
                        });
                    }
                    else
                    {
                        // Current value + lags: β_i0*X[t] + β_i1*X[t-1] + ... + β_ib*X[t-b]
                        for (int j = 0; j <= XOrderB; j++)
                        {
                            string lagLabel = j == 0 ? "" : ",-" + j;
                            Parameters.Add(new ModelParameter()
                            {
                                Name = "Covariate (β" + SubscriptFormatter.ToSubscript(i) + lagLabel + ")",
                                Value = 0,
                                LowerBound = -10,
                                UpperBound = 10,
                                PriorDistribution = new Uniform(-10, 10)
                            });
                        }
                    }
                }
            }

            // AR parameters
            for (int i = 1; i <= AROrderP; i++)
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

            // MA parameters
            for (int i = 1; i <= MAOrderQ; i++)
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

            // Scale (standard error) parameter
            Parameters.Add(new ModelParameter()
            {
                Name = "Scale (σ)",
                Value = sigma,
                LowerBound = sigmaLB,
                UpperBound = sigmaUB,
                IsPositive = true,
                PriorDistribution = new Uniform(sigmaLB, sigmaUB)
            });

            // Add handlers
            for (int i = 0; i < NumberOfParameters; i++)
                Parameters[i].PropertyChanged += Parameter_PropertyChanged;

            RaisePropertyChange(nameof(SetDefaultParameters));
        }

        /// <inheritdoc/>
        public override void SetParameterValues(IList<double> parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));
            if (parameters.Count != Parameters.Count)
                throw new ArgumentException("Parameter list length does not match model parameter count.", nameof(parameters));

            for (int i = 0; i < Parameters.Count; i++)
                Parameters[i].Value = parameters[i];
        }

        /// <inheritdoc/>
        public override double DataLogLikelihood(double[] parameters)
        {
            if (_covariateAlignmentValidationMessages.Count > 0)
                return double.NegativeInfinity;

            // Validate parameters
            for (int i = 0; i < parameters.Length; i++)
            {
                if (double.IsNaN(parameters[i]))
                    return double.NegativeInfinity;
            }

            double sigma = parameters.Last();
            // Guard against non-finite or non-positive sigma — Numerics.Distributions.Normal
            // rejects it, which would crash the sampler instead of being treated as a
            // boundary move. User-defined priors with non-positive support trigger this.
            if (!Tools.IsFinite(sigma) || sigma <= 0) return double.NegativeInfinity;
            var normDist = new Normal(0, sigma);
            var residuals = Residuals(parameters);
            int maxOrder = ConditionalOrder;
            double logLH = 0;

            // An empty conditional sum means no model step is evaluated; the model is invalid for
            // the attached training window rather than a perfect fit.
            if (residuals.Length <= maxOrder)
                return double.NegativeInfinity;

            // Compute conditional log-likelihood (use residuals.Length to account for differencing)
            for (int t = maxOrder; t < residuals.Length; t++)
            {
                logLH += normDist.LogPDF(residuals[t]);
            }

            return logLH + _logJacobian;
        }

        /// <inheritdoc/>
        public override double[] PointwiseDataLogLikelihood(double[] parameters)
        {
            int maxOrder = ConditionalOrder;

            int effectiveTrainingSteps = _trainingTimeSeries?.Count ?? 0;
            int n = effectiveTrainingSteps - maxOrder;
            if (n <= 0)
                return Array.Empty<double>();

            if (_covariateAlignmentValidationMessages.Count > 0)
            {
                var invalid = new double[n];
                Array.Fill(invalid, double.NegativeInfinity);
                return invalid;
            }

            // Validate parameters
            for (int i = 0; i < parameters.Length; i++)
            {
                if (double.IsNaN(parameters[i]))
                {
                    var invalid = new double[n];
                    for (int j = 0; j < n; j++) invalid[j] = double.NegativeInfinity;
                    return invalid;
                }
            }

            double sigma = parameters.Last();
            if (!Tools.IsFinite(sigma) || sigma <= 0)
            {
                var invalid = new double[n];
                Array.Fill(invalid, double.NegativeInfinity);
                return invalid;
            }
            var normDist = new Normal(0, sigma);
            var residuals = Residuals(parameters);
            var result = new double[n];

            // Attribute each observation's own log-Jacobian term.
            double[] jacobianTerms = GetLogJacobianTerms(n);

            // Compute pointwise conditional log-likelihood
            int idx = 0;
            for (int t = maxOrder; t < residuals.Length; t++)
            {
                result[idx++] = normDist.LogPDF(residuals[t]) + jacobianTerms[t - maxOrder];
            }

            return result;
        }

        /// <inheritdoc/>
        public override List<DataComponent> PointwiseDataLogLikelihoodComponents(double[] parameters)
        {
            int maxOrder = ConditionalOrder;

            int effectiveTrainingSteps = _trainingTimeSeries?.Count ?? 0;
            int n = effectiveTrainingSteps - maxOrder;
            if (n <= 0)
                return new List<DataComponent>();

            var result = new List<DataComponent>(n);
            var responseValues = _trainingTimeSeries?.ValuesToArray();

            if (_covariateAlignmentValidationMessages.Count > 0)
            {
                for (int j = 0; j < n; j++)
                {
                    int modelIndex = maxOrder + j;
                    double value = responseValues != null && modelIndex < responseValues.Length ? responseValues[modelIndex] : 0;
                    result.Add(new DataComponent(j, double.NegativeInfinity, value, DataComponentType.Exact, 1, $"t={modelIndex}"));
                }
                return result;
            }

            // Validate parameters
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

            double sigma = parameters.Last();
            if (!Tools.IsFinite(sigma) || sigma <= 0)
            {
                for (int j = 0; j < n; j++)
                {
                    int tIdx = maxOrder + j;
                    double value = responseValues != null && tIdx < responseValues.Length ? responseValues[tIdx] : 0;
                    result.Add(new DataComponent(j, double.NegativeInfinity, value, DataComponentType.Exact, 1, $"t={tIdx}"));
                }
                return result;
            }
            var normDist = new Normal(0, sigma);
            var residuals = Residuals(parameters);

            // Attribute each observation's own log-Jacobian term.
            double[] jacobianTerms = GetLogJacobianTerms(n);

            // Compute pointwise conditional log-likelihood components
            int idx = 0;
            for (int t = maxOrder; t < residuals.Length; t++)
            {
                double logLH = normDist.LogPDF(residuals[t]) + jacobianTerms[t - maxOrder];
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

            // Parameter priors
            for (int i = 0; i < Parameters.Count; i++)
            {
                double ll = i == Parameters.Count - 1 && !isValidScale
                    ? double.NegativeInfinity
                    : Parameters[i].PriorDistribution.LogPDF(parameters[i]);
                string paramName = string.IsNullOrEmpty(Parameters[i].OwnerName) ? Parameters[i].Name : Parameters[i].OwnerName;
                result.Add(new PriorComponent($"Parameter Prior: {paramName}", ll, PriorComponentType.ParameterPrior));
            }

            // Jeffreys rule for sigma (scale)
            if (UseJeffreysRuleForScale)
            {
                double ll = isValidScale ? -Math.Log(sigma) : double.NegativeInfinity;
                result.Add(new PriorComponent("Jeffreys Scale: σ", ll, PriorComponentType.JeffreysScalePrior));
            }

            return result;
        }

        /// <summary>
        /// Computes the model residuals (prediction errors) for given parameters.
        /// </summary>
        /// <param name="parameters">The parameter vector for the model.</param>
        /// <returns>Array of residuals on the transformed scale.</returns>
        public double[] Residuals(double[] parameters)
        {
            TimeSeries? trainingSeries = _trainingTimeSeries;
            if (trainingSeries == null)
                return Array.Empty<double>();

            int effectiveTrainingSteps = trainingSeries.Count;

            var y = new double[effectiveTrainingSteps];
            var mean = new double[effectiveTrainingSteps];
            var epsilon = new double[effectiveTrainingSteps];
            var residuals = new double[effectiveTrainingSteps];
            int maxOrder = ConditionalOrder;

            if (_covariateAlignmentValidationMessages.Count > 0)
            {
                Array.Fill(residuals, double.NaN);
                return residuals;
            }

            // Extract parameters
            int k = 0;
            double mu = 0;
            double[]? gamma = null;
            double[]? psi = null;
            double[,]? beta = null;
            var phi = new double[AROrderP];
            var theta = new double[MAOrderQ];

            if (IncludeIntercept)
            {
                mu = parameters[k++];
            }

            if (TrendType == Trend.Linear)
            {
                gamma = new[] { parameters[k++] };
            }
            else if (TrendType == Trend.Quadratic)
            {
                gamma = new[] { parameters[k++], parameters[k++] };
            }
            else if (TrendType == Trend.Cubic)
            {
                gamma = new[] { parameters[k++], parameters[k++], parameters[k++] };
            }

            if (IncludeSeasonality)
            {
                psi = new[] { parameters[k++], parameters[k++] };
            }

            if (Covariates != null && Covariates.Count > 0)
            {
                beta = new double[Covariates.Count, XOrderB + 1];
                for (int i = 0; i < Covariates.Count; i++)
                {
                    for (int j = 0; j <= XOrderB; j++)
                    {
                        beta[i, j] = parameters[k++];
                    }
                }
            }

            for (int i = 0; i < AROrderP; i++)
                phi[i] = parameters[k++];

            for (int i = 0; i < MAOrderQ; i++)
                theta[i] = parameters[k++];

            // Compute predictions and residuals
            for (int t = 0; t < effectiveTrainingSteps; t++)
            {
                mean[t] = mu;

                // Trend component
                if (gamma != null)
                {
                    if (TrendType == Trend.Linear)
                        mean[t] += gamma[0] * t;
                    else if (TrendType == Trend.Quadratic)
                        mean[t] += gamma[0] * t + gamma[1] * t * t;
                    else if (TrendType == Trend.Cubic)
                        mean[t] += gamma[0] * t + gamma[1] * t * t + gamma[2] * t * t * t;
                }

                // Seasonal component - Fourier series
                if (psi != null)
                {
                    double angle = 2.0 * Math.PI * t / _seasonalPeriod;
                    mean[t] += psi[0] * Math.Sin(angle) + psi[1] * Math.Cos(angle);
                }

                // Level covariates are matched to the raw-response date represented by this
                // transformed/differenced model step. Covariates are never differenced.
                if (beta != null && Covariates != null && _trainingCovariatePositions != null)
                {
                    for (int i = 0; i < Covariates.Count; i++)
                    {
                        for (int lag = 0; lag <= XOrderB && lag <= t; lag++)
                        {
                            int position = _trainingCovariatePositions[i, t, lag];
                            mean[t] += beta[i, lag] * _covariates[i][position].Value;
                        }
                    }
                }

                // AR component
                double ar = 0;
                if (t >= AROrderP)
                {
                    for (int p = 1; p <= AROrderP; p++)
                    {
                        ar += phi[p - 1] * (trainingSeries[t - p].Value - mean[t - p]);
                    }
                }

                // MA component
                double ma = 0;
                for (int q = 1; q <= Math.Min(t, MAOrderQ); q++)
                {
                    ma += theta[q - 1] * epsilon[t - q];
                }

                // Complete prediction
                if (t < maxOrder)
                {
                    y[t] = trainingSeries[t].Value;
                }
                else
                {
                    y[t] = mean[t] + ar + ma;
                }

                // Update epsilon for MA. _diffSeries, y, and residuals are all on the
                // transformed + differenced scale (the parameter scale), so subtraction is
                // well-defined without per-transform branches.
                epsilon[t] = trainingSeries[t].Value - y[t];
                residuals[t] = epsilon[t];
            }

            return residuals;
        }

        /// <summary>
        /// Predicts time series values using the specified parameters.
        /// </summary>
        /// <param name="parameters">The parameter vector for the model.</param>
        /// <param name="forecastSteps">Number of time steps to forecast beyond the training period (default = 0).</param>
        /// <param name="seed">Random seed for stochastic predictions. If -1, returns the deterministic mean
        /// prediction without random AR/MA noise; covariates that need extending are filled with each covariate's
        /// empirical mean (rather than a single bootstrap/KNN realization), so the deterministic trace is
        /// independent of <see cref="CovariateExtension"/>.</param>
        /// <param name="forecastCovariates">Optional covariates for the forecast period. If null and covariates exist,
        /// the <see cref="CovariateExtension"/> property determines how covariates are extended (in stochastic mode);
        /// in deterministic mode (seed == -1) the forecast tail is filled with each covariate's empirical mean.</param>
        /// <returns>
        /// A tuple containing:
        /// - Y: Predicted values on the original (undifferenced, untransformed) scale
        /// - Component decomposition: Intercept, Trend, Seasonality, Covariate, AR, and MA contributions
        /// </returns>
        /// <exception cref="InvalidOperationException">Thrown when <see cref="CovariateExtension"/> is
        /// <see cref="CovariateExtensionMethod.None"/> and covariates are insufficient for the forecast period.</exception>
        public (double[] Y,
            double[] InterceptPart,
            double[] TrendPart,
            double[] SeasonalityPart,
            double[] CovariatePart,
            double[] ARPart,
            double[] MAPart)
            Predict(double[] parameters, int forecastSteps = 0, int seed = -1, List<TimeSeries>? forecastCovariates = null)
        {
            int totalSteps = TrainingTimeSteps + forecastSteps;
            int modelSteps = totalSteps - DiffOrderD;
            int trainingModelSteps = TrainingTimeSteps - DiffOrderD;

            var modelY = new double[modelSteps];
            var interceptPart = new double[totalSteps];
            var trendPart = new double[totalSteps];
            var seasonalityPart = new double[totalSteps];
            var covariatePart = new double[totalSteps];
            var arPart = new double[totalSteps];
            var maPart = new double[totalSteps];
            var mean = new double[modelSteps];
            var epsilon = new double[modelSteps];

            int maxOrder = ConditionalOrder;
            Random? prng = seed >= 0 ? new Random(seed) : null;
            Normal? errDist = seed >= 0 ? new Normal(0, parameters.Last()) : null;

            // Prepare forecast covariates if needed
            List<TimeSeries> useCovariates = _covariates;
            if (forecastSteps > 0 && _covariates != null && _covariates.Count > 0)
            {
                if (forecastCovariates != null)
                {
                    // Use provided forecast covariates
                    useCovariates = new List<TimeSeries>();
                    for (int i = 0; i < _covariates.Count; i++)
                    {
                        var combined = _covariates[i].Clone();
                        for (int j = 0; j < forecastCovariates[i].Count; j++)
                        {
                            combined.Add(forecastCovariates[i][j].Clone());
                        }
                        useCovariates.Add(combined);
                    }
                }
                else
                {
                    // Extend covariates based on the CovariateExtension setting. Observed
                    // covariate values must be preserved exactly across posterior realizations
                    // (otherwise the training-period CI inflates because beta*X[t] varies
                    // randomly for every draw). We always keep [0..Count) as observed and
                    // only resample the forecast tail of length forecastSteps.
                    //
                    // For deterministic predictions (seed == -1) the bootstrap/KNN resamplers
                    // would inject a single arbitrary covariate path into the forecast tail,
                    // making the "deterministic" forecast depend on the CovariateExtension
                    // choice and on a fixed-but-arbitrary seed. Use the empirical mean of
                    // each covariate instead — the maximum-entropy choice that yields a
                    // smooth AR(I)MA decay toward steady state independent of method.
                    bool deterministic = seed < 0;
                    int resampleSeed = seed >= 0 ? seed + 1000 : 12345;

                    switch (CovariateExtension)
                    {
                        case CovariateExtensionMethod.None:
                            // Validate that covariates are long enough for the whole horizon.
                            int requiredLength = TrainingTimeSteps + forecastSteps;
                            for (int i = 0; i < _covariates.Count; i++)
                            {
                                if (_covariates[i].Count < requiredLength)
                                {
                                    throw new InvalidOperationException(
                                        $"Covariate {i} has {_covariates[i].Count} observations but {requiredLength} are required " +
                                        $"for {forecastSteps} forecast steps. Either provide forecastCovariates or set " +
                                        "CovariateExtension to BlockBootstrap or KNN.");
                                }
                            }
                            break;

                        case CovariateExtensionMethod.BlockBootstrap:
                            useCovariates = new List<TimeSeries>();
                            for (int i = 0; i < _covariates.Count; i++)
                            {
                                if (deterministic)
                                {
                                    useCovariates.Add(AppendConstantTail(_covariates[i], _covariates[i].MeanValue(), forecastSteps));
                                }
                                else
                                {
                                    int blockSize = Math.Max(1, Math.Min(10, _covariates[i].Count / 4));
                                    var tail = _covariates[i].ResampleWithBlockBootstrap(
                                        forecastSteps,
                                        blockSize,
                                        resampleSeed + i);
                                    useCovariates.Add(AppendTail(_covariates[i], tail));
                                }
                            }
                            break;

                        case CovariateExtensionMethod.KNN:
                            useCovariates = new List<TimeSeries>();
                            for (int i = 0; i < _covariates.Count; i++)
                            {
                                if (deterministic)
                                {
                                    useCovariates.Add(AppendConstantTail(_covariates[i], _covariates[i].MeanValue(), forecastSteps));
                                }
                                else
                                {
                                    int knn = Math.Max(3, _covariates[i].Count / 10);
                                    var tail = _covariates[i].ResampleWithKNN(
                                        forecastSteps,
                                        knn,
                                        resampleSeed + i);
                                    useCovariates.Add(AppendTail(_covariates[i], tail));
                                }
                            }
                            break;
                    }
                }
            }

            int[,,]? predictionCovariatePositions = useCovariates != null && useCovariates.Count > 0
                ? BuildPredictionCovariatePositions(useCovariates, modelSteps)
                : null;

            // Extract parameters
            int k = 0;
            double mu = 0;
            double[]? gamma = null;
            double[]? psi = null;
            double[,]? beta = null;
            var phi = new double[AROrderP];
            var theta = new double[MAOrderQ];

            if (IncludeIntercept)
                mu = parameters[k++];

            if (TrendType == Trend.Linear)
                gamma = new[] { parameters[k++] };
            else if (TrendType == Trend.Quadratic)
                gamma = new[] { parameters[k++], parameters[k++] };
            else if (TrendType == Trend.Cubic)
                gamma = new[] { parameters[k++], parameters[k++], parameters[k++] };

            if (IncludeSeasonality)
                psi = new[] { parameters[k++], parameters[k++] };

            if (useCovariates != null && useCovariates.Count > 0)
            {
                beta = new double[useCovariates.Count, XOrderB + 1];
                for (int i = 0; i < useCovariates.Count; i++)
                {
                    for (int j = 0; j <= XOrderB; j++)
                    {
                        beta[i, j] = parameters[k++];
                    }
                }
            }

            for (int i = 0; i < AROrderP; i++)
                phi[i] = parameters[k++];

            for (int i = 0; i < MAOrderQ; i++)
                theta[i] = parameters[k++];

            // Generate predictions
            for (int t = 0; t < modelSteps; t++)
            {
                int rawIndex = t + DiffOrderD;

                // Intercept
                mean[t] = mu;
                interceptPart[rawIndex] = mu;

                // Trend
                double trend = 0;
                if (gamma != null)
                {
                    if (TrendType == Trend.Linear)
                        trend = gamma[0] * t;
                    else if (TrendType == Trend.Quadratic)
                        trend = gamma[0] * t + gamma[1] * t * t;
                    else if (TrendType == Trend.Cubic)
                        trend = gamma[0] * t + gamma[1] * t * t + gamma[2] * t * t * t;
                }
                mean[t] += trend;
                trendPart[rawIndex] = trend;

                // Seasonality - Fourier series
                double seasonality = 0;
                if (psi != null)
                {
                    double angle = 2.0 * Math.PI * t / _seasonalPeriod;
                    seasonality = psi[0] * Math.Sin(angle) + psi[1] * Math.Cos(angle);
                }
                mean[t] += seasonality;
                seasonalityPart[rawIndex] = seasonality;

                // Covariates (includes current and lagged values if XOrderB > 0)
                double covariate = 0;
                if (beta != null && useCovariates != null && predictionCovariatePositions != null)
                {
                    for (int i = 0; i < useCovariates.Count; i++)
                    {
                        for (int lag = 0; lag <= XOrderB && lag <= t; lag++)
                        {
                            int position = predictionCovariatePositions[i, t, lag];
                            covariate += beta[i, lag] * useCovariates[i][position].Value;
                        }
                    }
                }
                mean[t] += covariate;
                covariatePart[rawIndex] = covariate;

                // AR lags: inside the fit window (t - p < TrainingTimeSteps) use observed
                // _diffSeries for one-step-ahead residual structure. Outside the fit window
                // (validation + future forecast) propagate via predicted y[t-p] so the
                // model's own uncertainty compounds — the model never saw holdout data.
                double ar = 0;
                if (t >= AROrderP)
                {
                    for (int p = 1; p <= AROrderP; p++)
                    {
                        if (_diffSeries != null && t - p < trainingModelSteps && t - p < _diffSeries.Count)
                        {
                            ar += phi[p - 1] * (_diffSeries[t - p].Value - mean[t - p]);
                        }
                        else
                        {
                            ar += phi[p - 1] * (modelY[t - p] - mean[t - p]);
                        }
                    }
                }
                arPart[rawIndex] = ar;

                // MA
                double ma = 0;
                for (int q = 1; q <= Math.Min(t, MAOrderQ); q++)
                {
                    ma += theta[q - 1] * epsilon[t - q];
                }
                maPart[rawIndex] = ma;

                // Complete prediction on the transformed + differenced scale. Parameters
                // were fit on this scale (via _trainingTimeSeries), and _diffSeries now
                // lives on the same scale after SetTrainingData's reorder, so no mixing.
                if (t < maxOrder && _diffSeries != null && t < _diffSeries.Count)
                {
                    modelY[t] = _diffSeries[t].Value;
                }
                else
                {
                    modelY[t] = mean[t] + ar + ma;
                }

                // Pre-noise epsilon inside the fit window = observed - model prediction.
                // Keeps MA recursion anchored to true residuals through training.
                if (_diffSeries != null && t < trainingModelSteps && t < _diffSeries.Count)
                {
                    epsilon[t] = _diffSeries[t].Value - modelY[t];
                }

                // Residual noise for the posterior predictive distribution. Draw every step
                // once the AR/MA buffer is seeded (t >= maxOrder), on the model scale — the
                // integration and inverse-transform after the loop propagate it correctly.
                if (prng != null && t >= maxOrder)
                {
                    double mt = modelY[t];
                    double error = errDist!.InverseCDF(prng.NextDouble());
                    modelY[t] += error;

                    // Outside the fit window the model never saw observations; overwrite
                    // epsilon with the injected noise so MA fans out from injected error.
                    if (t >= trainingModelSteps)
                        epsilon[t] = modelY[t] - mt;
                }
            }

            // Model step k maps to raw slot k+d. Fitted levels condition on the observed state
            // at raw slot k+d-1; the first forecast starts from the final training state and
            // later forecasts recurse from generated states.
            double[] y = DiffOrderD > 0
                ? TimeSeriesPredictionIntegrator.ReconstructConditionalLevels(
                    modelY,
                    _transformedTimeSeries,
                    TrainingTimeSteps,
                    DiffOrderD)
                : modelY;

            // Step B — Inverse transform back to the original (user-facing) scale. Applied
            // uniformly to every step. No bias correction for the ModeCurve (seed < 0): the
            // back-transformed mean is the posterior-median point forecast, matching R's
            // forecast::forecast.Arima convention. MeanCurve, computed from averaged draws
            // in the analysis layer, gives the true posterior mean on the original scale.
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

            return (y, interceptPart, trendPart, seasonalityPart, covariatePart, arPart, maPart);
        }

        /// <summary>
        /// Builds the exact-date level-covariate map for every prediction model step.
        /// </summary>
        /// <param name="covariates">The observed and extended level covariates.</param>
        /// <param name="modelSteps">The number of transformed/differenced model steps.</param>
        /// <returns>Covariate positions indexed by covariate, model step, and lag.</returns>
        /// <exception cref="InvalidOperationException">A required response/covariate timestamp is missing or duplicated.</exception>
        private int[,,] BuildPredictionCovariatePositions(List<TimeSeries> covariates, int modelSteps)
        {
            var result = new int[covariates.Count, modelSteps, XOrderB + 1];
            for (int covariateIndex = 0; covariateIndex < covariates.Count; covariateIndex++)
            {
                TimeSeries covariate = covariates[covariateIndex];
                var positionsByDate = new Dictionary<DateTime, List<int>>();
                for (int position = 0; position < covariate.Count; position++)
                {
                    DateTime date = covariate[position].Index;
                    if (!positionsByDate.TryGetValue(date, out List<int>? positions))
                    {
                        positions = new List<int>();
                        positionsByDate.Add(date, positions);
                    }
                    positions.Add(position);
                }

                for (int modelIndex = 0; modelIndex < modelSteps; modelIndex++)
                {
                    for (int lag = 0; lag <= XOrderB && lag <= modelIndex; lag++)
                    {
                        int rawIndex = DiffOrderD + modelIndex - lag;
                        DateTime requiredDate = GetPredictionResponseDate(rawIndex);
                        if (!positionsByDate.TryGetValue(requiredDate, out List<int>? matches))
                        {
                            throw new InvalidOperationException(
                                $"Covariate {covariateIndex + 1} is missing required timestamp {requiredDate:O} for raw response index {rawIndex}.");
                        }
                        if (matches.Count != 1)
                        {
                            throw new InvalidOperationException(
                                $"Covariate {covariateIndex + 1} contains duplicate required timestamp {requiredDate:O} for raw response index {rawIndex}.");
                        }
                        result[covariateIndex, modelIndex, lag] = matches[0];
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Gets the observed or regularly extended response timestamp at one raw output index.
        /// </summary>
        /// <param name="rawIndex">The zero-based raw response index.</param>
        /// <returns>The corresponding response timestamp.</returns>
        /// <exception cref="InvalidOperationException">No response timestamps are available.</exception>
        private DateTime GetPredictionResponseDate(int rawIndex)
        {
            if (TimeSeries == null || TimeSeries.Count == 0)
                throw new InvalidOperationException("TimeSeries must contain at least one timestamp before prediction.");
            if (rawIndex < TimeSeries.Count)
                return TimeSeries[rawIndex].Index;

            DateTime date = TimeSeries[TimeSeries.Count - 1].Index;
            for (int index = TimeSeries.Count; index <= rawIndex; index++)
                date = TimeSeries.AddTimeInterval(date, TimeSeries.TimeInterval);
            return date;
        }

        /// <summary>
        /// Generates a synthetic random time series using the current parameter values.
        /// </summary>
        /// <param name="timeSteps">The number of time steps to simulate.</param>
        /// <param name="seed">Random seed for reproducibility (default = 12345).</param>
        /// <returns>A simulated time series.</returns>
        public TimeSeries GenerateRandomSeries(int timeSteps, int seed = 12345)
        {
            if (TimeSeries == null)
                throw new InvalidOperationException("TimeSeries must be set before generating random series.");

            DateTime startDate = TimeSeries.StartDate;
            DateTime endDate = startDate;
            for (int i = 0; i < timeSteps - 1; i++)
            {
                endDate = Numerics.Data.TimeSeries.AddTimeInterval(endDate, TimeSeries.TimeInterval);
            }

            var result = new TimeSeries(TimeSeries.TimeInterval, startDate, endDate);
            var parameters = Parameters.Select(x => x.Value).ToArray();

            // Use predict method with stochastic errors
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

            var result = new ARIMAX()
            {
                _transformType = TransformType,
                _includeIntercept = IncludeIntercept,
                _includeSeasonality = IncludeSeasonality,
                _trendType = TrendType,
                _arOrderP = AROrderP,
                _diffOrderD = DiffOrderD,
                _maOrderQ = MAOrderQ,
                _xOrderB = XOrderB,
                _useDefaultFlatPriors = UseDefaultFlatPriors,
                _useJeffreysRuleForScale = UseJeffreysRuleForScale,
                _trainingTimeSteps = TrainingTimeSteps,
                _useDefaultTrainingSteps = UseDefaultTrainingSteps,
                _lambda = TransformLambda,
                _transformLambdaIsManual = _transformLambdaIsManual,
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
            if (_covariates != null)
            {
                result.SetCovariates(_covariates.Select(c => c.Clone()).ToList());
            }

            return result;
        }

        /// <inheritdoc/>
        public override XElement ToXElement()
        {
            var result = new XElement(nameof(ARIMAX));
            result.SetAttributeValue(nameof(TransformType), TransformType.ToString());
            result.SetAttributeValue(nameof(TransformLambda), TransformLambda.ToString("R", CultureInfo.InvariantCulture));
            result.SetAttributeValue("TransformLambdaIsManual", _transformLambdaIsManual.ToString());
            result.SetAttributeValue(nameof(CovariateExtension), CovariateExtension.ToString());
            result.SetAttributeValue(nameof(IncludeIntercept), IncludeIntercept.ToString());
            result.SetAttributeValue(nameof(IncludeSeasonality), IncludeSeasonality.ToString());
            result.SetAttributeValue(nameof(TrendType), TrendType.ToString());
            result.SetAttributeValue(nameof(AROrderP), AROrderP.ToString(CultureInfo.InvariantCulture));
            result.SetAttributeValue(nameof(DiffOrderD), DiffOrderD.ToString(CultureInfo.InvariantCulture));
            result.SetAttributeValue(nameof(MAOrderQ), MAOrderQ.ToString(CultureInfo.InvariantCulture));
            result.SetAttributeValue(nameof(XOrderB), XOrderB.ToString(CultureInfo.InvariantCulture));
            result.SetAttributeValue(nameof(TrainingTimeSteps), TrainingTimeSteps.ToString(CultureInfo.InvariantCulture));
            result.SetAttributeValue(nameof(UseDefaultTrainingSteps), UseDefaultTrainingSteps.ToString());

            // Parameters
            var parms = new XElement(nameof(Parameters));
            foreach (var p in Parameters)
                parms.Add(p.ToXElement());
            result.Add(parms);
            result.SetAttributeValue(nameof(UseDefaultFlatPriors), UseDefaultFlatPriors.ToString());
            result.SetAttributeValue(nameof(UseJeffreysRuleForScale), UseJeffreysRuleForScale.ToString());

            return result;
        }

        /// <inheritdoc/>
        public override (bool IsValid, List<string> ValidationMessages) Validate()
        {
            bool isValid = true;
            var messages = new List<string>();

            // Check time series
            if (TimeSeries == null)
            {
                isValid = false;
                messages.Add("Error: Time series data is null.");
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

            // Check training steps
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

            int effectiveRawTrainingSteps = Math.Min(TrainingTimeSteps, TimeSeries.Count);
            int trainingDifferenceCount = Math.Max(0, effectiveRawTrainingSteps - DiffOrderD);
            int conditionalOrder = ConditionalOrder;
            if (trainingDifferenceCount <= conditionalOrder)
            {
                isValid = false;
                messages.Add(
                    $"Error: The raw training window provides {trainingDifferenceCount} differenced model steps, " +
                    $"which must exceed the conditional AR/MA/covariate-lag order ({conditionalOrder}).");
            }

            // Check orders
            if (AROrderP < 0 || AROrderP > 10)
            {
                isValid = false;
                messages.Add("Error: AR order (p) must be between 0 and 10.");
            }

            if (DiffOrderD < 0 || DiffOrderD > 2)
            {
                isValid = false;
                messages.Add("Error: Differencing order (d) must be 0, 1, or 2.");
            }

            if (MAOrderQ < 0 || MAOrderQ > 10)
            {
                isValid = false;
                messages.Add("Error: MA order (q) must be between 0 and 10.");
            }

            if (XOrderB < 0 || XOrderB > 10)
            {
                isValid = false;
                messages.Add("Error: Exogenous lag order (b) must be between 0 and 10.");
            }

            // Check for at least one component
            if (AROrderP == 0 && MAOrderQ == 0 && !IncludeIntercept && TrendType == Trend.None &&
                !IncludeSeasonality && (Covariates == null || Covariates.Count == 0))
            {
                isValid = false;
                messages.Add("Error: Model must have at least one component (AR, MA, intercept, trend, seasonality, or covariates).");
            }

            // Warn about redundant configuration: differencing removes trends, so fitting trend parameters
            // to differenced data is generally not meaningful
            if (DiffOrderD > 0 && TrendType != Trend.None)
            {
                messages.Add("Warning: Using both differencing and trend parameters is typically redundant. " +
                    "Differencing removes trends from the data, so trend parameters on differenced data may not be meaningful. " +
                    "Consider using either differencing (d > 0) OR trend parameters, but not both.");
            }

            // Warn about differencing with Fourier seasonality: differencing changes seasonal amplitude/phase
            if (DiffOrderD > 0 && IncludeSeasonality)
            {
                messages.Add("Warning: Using both differencing and Fourier seasonality can cause parameter identification issues. " +
                    "Differencing alters the amplitude and phase of seasonal components. " +
                    "Consider using either differencing (d > 0) OR explicit Fourier seasonality, but not both.");
            }

            // Check exact-date level-covariate alignment. Extra dates outside the required
            // transformed/differenced training window are intentionally harmless.
            if (Covariates != null && Covariates.Count > 0)
            {
                RebuildTrainingCovariateAlignment();
                if (_covariateAlignmentValidationMessages.Count > 0)
                {
                    isValid = false;
                    messages.AddRange(_covariateAlignmentValidationMessages);
                }
            }

            // Validate parameters
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
            }

            // Check for non-positive values with log-based transforms. Box-Cox (including
            // the Logarithmic special case λ = 0) is only defined for strictly positive inputs.
            // Validation is on the raw time series — differencing happens after transformation
            // now, so negative differences on the log scale are expected and fine.
            if (TransformType == Transform.Logarithmic || TransformType == Transform.BoxCox)
            {
                if (TimeSeries != null && TimeSeries.Count > 0 && TimeSeries.MinValue() <= 0)
                {
                    isValid = false;
                    messages.Add("Error: Log-based transformations require all time series values to be strictly positive. Use Yeo-Johnson for series with non-positive values.");
                }
            }

            // Check AR stationarity condition (roots outside unit circle)
            if (AROrderP > 0 && Parameters != null)
            {
                // Extract AR coefficients
                int arStart = GetARParameterStartIndex();
                if (arStart >= 0 && arStart + AROrderP <= Parameters.Count)
                {
                    var arCoeffs = new double[AROrderP];
                    for (int i = 0; i < AROrderP; i++)
                    {
                        arCoeffs[i] = Parameters[arStart + i].Value;
                    }

                    // Simple check: sum of absolute values < 1 (sufficient but not necessary)
                    double sumAbsAR = 0;
                    for (int i = 0; i < AROrderP; i++)
                    {
                        sumAbsAR += Math.Abs(arCoeffs[i]);
                    }

                    if (sumAbsAR >= 1.0)
                    {
                        // This is a warning, not an error - model may still be stationary
                        messages.Add($"Warning: AR coefficients may violate stationarity (sum of absolute values = {sumAbsAR:F3} >= 1). Consider checking characteristic equation roots.");
                    }
                }
            }

            // Check MA invertibility condition (roots outside unit circle)
            if (MAOrderQ > 0 && Parameters != null)
            {
                // Extract MA coefficients
                int maStart = GetMAParameterStartIndex();
                if (maStart >= 0 && maStart + MAOrderQ <= Parameters.Count)
                {
                    var maCoeffs = new double[MAOrderQ];
                    for (int i = 0; i < MAOrderQ; i++)
                    {
                        maCoeffs[i] = Parameters[maStart + i].Value;
                    }

                    // Simple check: sum of absolute values < 1 (sufficient but not necessary)
                    double sumAbsMA = 0;
                    for (int i = 0; i < MAOrderQ; i++)
                    {
                        sumAbsMA += Math.Abs(maCoeffs[i]);
                    }

                    if (sumAbsMA >= 1.0)
                    {
                        // This is a warning, not an error - model may still be invertible
                        messages.Add($"Warning: MA coefficients may violate invertibility (sum of absolute values = {sumAbsMA:F3} >= 1). Consider checking characteristic equation roots.");
                    }
                }
            }

            if (_transformFitValidationMessage != null)
            {
                isValid = false;
                messages.Add(_transformFitValidationMessage);
            }

            return (isValid, messages);
        }

        /// <summary>
        /// Gets the number of leading model steps that condition the likelihood: max(p, q, b).
        /// </summary>
        /// <remarks>
        /// Conditional evaluation, residuals, prediction seeding and the transform Jacobian window
        /// all start at model step max(p, q, b), so every evaluated step has its p autoregressive
        /// lags, q residual lags and b lagged covariate values available. Model step k maps to raw
        /// index k + d.
        /// </remarks>
        private int ConditionalOrder => Math.Max(AROrderP, Math.Max(MAOrderQ, XOrderB));

        /// <summary>
        /// Gets the starting index of AR parameters in the parameter list.
        /// Returns the index where AR parameters would start, even if AROrderP is 0.
        /// </summary>
        /// <returns>The index where AR parameters begin, or -1 if Parameters is null.</returns>
        private int GetARParameterStartIndex()
        {
            if (Parameters == null) return -1;

            int idx = 0;
            if (IncludeIntercept) idx++;

            if (TrendType == Trend.Linear) idx += 1;
            else if (TrendType == Trend.Quadratic) idx += 2;
            else if (TrendType == Trend.Cubic) idx += 3;

            if (IncludeSeasonality) idx += 2;

            if (Covariates != null && Covariates.Count > 0)
                idx += Covariates.Count * (XOrderB + 1);

            return idx;
        }

        /// <summary>
        /// Gets the starting index of MA parameters in the parameter list.
        /// Returns the index where MA parameters would start, even if MAOrderQ is 0.
        /// </summary>
        /// <returns>The index where MA parameters begin, or -1 if Parameters is null.</returns>
        private int GetMAParameterStartIndex()
        {
            if (Parameters == null) return -1;
            return GetARParameterStartIndex() + AROrderP;
        }

        /// <inheritdoc/>
        double[] ISimulatable<double[]>.GenerateRandomValues(int sampleSize, int seed)
        {
            return GenerateRandomValues(sampleSize, seed, null);
        }

        /// <summary>
        /// Generates random samples from the ARIMAX model including intercept, trend, seasonality,
        /// covariate effects, and ARMA components.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Generates random samples from the ARIMAX model including intercept, trend, seasonality,
        /// covariate effects, and ARMA components.
        /// </para>
        /// <para>
        /// The generated values represent the ARIMAX process:
        /// y_t = μ + γ(t) + ψ(t) + β*X(t) + φ₁(y_{t-1} - μ_t₋₁) + ... + φₚ(y_{t-p} - μ_t₋ₚ) + ε_t + θ₁ε_{t-1} + ... + θₚε_{t-q}
        /// </para>
        /// <para>
        /// where γ(t) is the trend component (Linear: γ₁t, Quadratic: γ₁t + γ₂t², Cubic: γ₁t + γ₂t² + γ₃t³),
        /// ψ(t) is the seasonal component: ψ₁sin(2πt/S) + ψ₂cos(2πt/S), and β*X(t) is the covariate effect.
        /// Every term is evaluated on the transformed/differenced model scale. Generated highest-order
        /// differences are integrated from observed transformed anchors, when available, or zero anchors,
        /// and the completed level series is inverse-transformed exactly once.
        /// </para>
        /// <para>
        /// <b>Covariate handling:</b> When covariates are present and <paramref name="sampleSize"/> exceeds
        /// the available covariate observations, the <see cref="CovariateExtension"/> property determines
        /// how covariates are extended:
        /// <list type="bullet">
        /// <item><description><see cref="CovariateExtensionMethod.None"/>: Throws an exception if insufficient covariates.</description></item>
        /// <item><description><see cref="CovariateExtensionMethod.BlockBootstrap"/>: Extends using block bootstrap (default).</description></item>
        /// <item><description><see cref="CovariateExtensionMethod.KNN"/>: Extends using k-Nearest Neighbors.</description></item>
        /// </list>
        /// Alternatively, provide pre-extended covariates via the <paramref name="generateCovariates"/> parameter.
        /// </para>
        /// </remarks>
        /// <param name="sampleSize">The number of random values to generate.</param>
        /// <param name="seed">Random seed for reproducibility. If -1, uses system time.</param>
        /// <param name="generateCovariates">Optional covariates for the generation period. If null and covariates
        /// exist, the <see cref="CovariateExtension"/> property determines how they are extended.</param>
        /// <returns>Array of generated random values.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when sampleSize is not positive.</exception>
        /// <exception cref="InvalidOperationException">Thrown when <see cref="CovariateExtension"/> is
        /// <see cref="CovariateExtensionMethod.None"/> and covariates are insufficient, when required
        /// response/covariate timestamps are missing or duplicated, or when attached data do not provide
        /// the required transformed differencing anchors.</exception>
        public double[] GenerateRandomValues(int sampleSize, int seed = -1, List<TimeSeries>? generateCovariates = null)
        {
            if (sampleSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(sampleSize), "Sample size must be positive.");

            if (DiffOrderD > 0 && sampleSize <= DiffOrderD)
                return InverseTransformGeneratedSeries(GetGenerationAnchors(sampleSize));

            var rng = seed >= 0 ? new Numerics.Sampling.MersenneTwister(seed) : new Numerics.Sampling.MersenneTwister();

            // Extract parameters in order: intercept, trend, seasonality, covariates, AR, MA, sigma
            int k = 0;
            double mu = 0;
            double[]? gamma = null;
            double[]? psi = null;
            double[,]? beta = null;

            // Extract intercept if present
            if (IncludeIntercept)
            {
                mu = Parameters[k++].Value;
            }

            // Extract trend parameters
            if (TrendType == Trend.Linear)
            {
                gamma = new[] { Parameters[k++].Value };
            }
            else if (TrendType == Trend.Quadratic)
            {
                gamma = new[] { Parameters[k++].Value, Parameters[k++].Value };
            }
            else if (TrendType == Trend.Cubic)
            {
                gamma = new[] { Parameters[k++].Value, Parameters[k++].Value, Parameters[k++].Value };
            }

            // Extract seasonality parameters (Fourier coefficients)
            if (IncludeSeasonality)
            {
                psi = new[] { Parameters[k++].Value, Parameters[k++].Value };
            }

            // Extract covariate parameters and prepare covariate data
            List<TimeSeries>? useCovariates = null;
            if (Covariates != null && Covariates.Count > 0)
            {
                // Extract beta coefficients
                beta = new double[Covariates.Count, XOrderB + 1];
                for (int i = 0; i < Covariates.Count; i++)
                {
                    for (int j = 0; j <= XOrderB; j++)
                    {
                        beta[i, j] = Parameters[k++].Value;
                    }
                }

                // Prepare covariates for generation
                if (generateCovariates != null)
                {
                    useCovariates = generateCovariates;
                }
                else if (sampleSize > Covariates[0].Count)
                {
                    // Need to extend covariates
                    int resampleSeed = seed > 0 ? seed + 2000 : 54321;

                    switch (CovariateExtension)
                    {
                        case CovariateExtensionMethod.None:
                            throw new InvalidOperationException(
                                $"Covariates have {Covariates[0].Count} observations but {sampleSize} are required. " +
                                "Either provide generateCovariates or set CovariateExtension to BlockBootstrap or KNN.");

                        case CovariateExtensionMethod.BlockBootstrap:
                            useCovariates = new List<TimeSeries>();
                            for (int i = 0; i < Covariates.Count; i++)
                            {
                                int blockSize = Math.Max(1, Math.Min(10, Covariates[i].Count / 4));
                                int tailSize = sampleSize - Covariates[i].Count;
                                var tail = Covariates[i].ResampleWithBlockBootstrap(
                                    tailSize,
                                    blockSize,
                                    resampleSeed + i);
                                useCovariates.Add(AppendTail(Covariates[i], tail));
                            }
                            break;

                        case CovariateExtensionMethod.KNN:
                            useCovariates = new List<TimeSeries>();
                            for (int i = 0; i < Covariates.Count; i++)
                            {
                                int knn = Math.Max(3, Covariates[i].Count / 10);
                                int tailSize = sampleSize - Covariates[i].Count;
                                var tail = Covariates[i].ResampleWithKNN(
                                    tailSize,
                                    knn,
                                    resampleSeed + i);
                                useCovariates.Add(AppendTail(Covariates[i], tail));
                            }
                            break;
                    }
                }
                else
                {
                    // Covariates are sufficient, use original
                    useCovariates = Covariates;
                }

                if (useCovariates == null)
                    throw new InvalidOperationException($"Unsupported covariate extension method: {CovariateExtension}.");
                if (useCovariates.Count != Covariates.Count)
                {
                    throw new InvalidOperationException(
                        $"Generation requires {Covariates.Count} covariate series but {useCovariates.Count} were supplied.");
                }
            }

            // Extract AR coefficients
            double[] phi = new double[AROrderP];
            for (int i = 0; i < AROrderP; i++)
            {
                phi[i] = Parameters[k++].Value;
            }

            // Extract MA coefficients
            double[] theta = new double[MAOrderQ];
            for (int i = 0; i < MAOrderQ; i++)
            {
                theta[i] = Parameters[k++].Value;
            }

            double sigma = Parameters[k].Value;
            var normal = new Numerics.Distributions.Normal(0, sigma);

            int modelSampleSize = Math.Max(0, sampleSize - DiffOrderD);
            int[,,]? generationCovariatePositions = beta != null && useCovariates != null
                ? BuildGenerationCovariatePositions(useCovariates, modelSampleSize)
                : null;

            // All recursion arrays remain on the transformed/differenced model scale.
            var series = new double[modelSampleSize];
            var mean = new double[modelSampleSize];
            var epsilon = new double[modelSampleSize];

            var noise = new double[modelSampleSize];
            for (int t = 0; t < modelSampleSize; t++)
            {
                noise[t] = normal.InverseCDF(rng.NextDouble());
            }

            // Pre-compute mean at each time step (matches Residuals method)
            for (int t = 0; t < modelSampleSize; t++)
            {
                mean[t] = mu;

                // Trend component
                if (gamma != null)
                {
                    if (TrendType == Trend.Linear)
                        mean[t] += gamma[0] * t;
                    else if (TrendType == Trend.Quadratic)
                        mean[t] += gamma[0] * t + gamma[1] * t * t;
                    else if (TrendType == Trend.Cubic)
                        mean[t] += gamma[0] * t + gamma[1] * t * t + gamma[2] * t * t * t;
                }

                // Seasonal component
                if (psi != null)
                {
                    double angle = 2.0 * Math.PI * t / _seasonalPeriod;
                    mean[t] += psi[0] * Math.Sin(angle) + psi[1] * Math.Cos(angle);
                }

                // Level covariates use the exact raw-response date represented by model step t.
                if (beta != null && useCovariates != null && generationCovariatePositions != null)
                {
                    for (int i = 0; i < useCovariates.Count; i++)
                    {
                        for (int lag = 0; lag <= XOrderB && lag <= t; lag++)
                        {
                            int position = generationCovariatePositions[i, t, lag];
                            mean[t] += beta[i, lag] * useCovariates[i][position].Value;
                        }
                    }
                }
            }

            // Generate series (matches structure in Residuals/Predict methods)
            for (int t = 0; t < modelSampleSize; t++)
            {
                // AR component (mean-centered, only for t >= AROrderP)
                double ar = 0;
                if (t >= AROrderP)
                {
                    for (int p = 1; p <= AROrderP; p++)
                    {
                        ar += phi[p - 1] * (series[t - p] - mean[t - p]);
                    }
                }

                // MA component uses epsilon (residuals on original scale)
                double ma = 0;
                for (int q = 1; q <= Math.Min(t, MAOrderQ); q++)
                {
                    ma += theta[q - 1] * epsilon[t - q];
                }

                double deterministic = mean[t] + ar + ma;
                series[t] = deterministic + noise[t];

                // The residual is the generated value minus its deterministic part, so the MA terms of
                // later steps use exactly the innovation realized in the series (bit-identical for Transform.None, d = 0).
                epsilon[t] = series[t] - deterministic;
            }

            double[] transformedLevels = DiffOrderD > 0
                ? IntegrateGeneratedDifferences(series, sampleSize)
                : series;
            return InverseTransformGeneratedSeries(transformedLevels);
        }

        /// <summary>
        /// Builds the exact-date level-covariate map for every generated model step.
        /// </summary>
        /// <param name="covariates">The supplied or extended level covariates.</param>
        /// <param name="modelSteps">The number of transformed/differenced model steps.</param>
        /// <returns>Covariate positions indexed by covariate, model step, and lag.</returns>
        /// <exception cref="InvalidOperationException">A required response/covariate timestamp is missing or duplicated.</exception>
        private int[,,] BuildGenerationCovariatePositions(List<TimeSeries> covariates, int modelSteps)
        {
            var result = new int[covariates.Count, modelSteps, XOrderB + 1];
            for (int covariateIndex = 0; covariateIndex < covariates.Count; covariateIndex++)
            {
                TimeSeries covariate = covariates[covariateIndex];
                var positionsByDate = new Dictionary<DateTime, List<int>>();
                for (int position = 0; position < covariate.Count; position++)
                {
                    DateTime date = covariate[position].Index;
                    if (!positionsByDate.TryGetValue(date, out List<int>? positions))
                    {
                        positions = new List<int>();
                        positionsByDate.Add(date, positions);
                    }
                    positions.Add(position);
                }

                for (int modelIndex = 0; modelIndex < modelSteps; modelIndex++)
                {
                    for (int lag = 0; lag <= XOrderB && lag <= modelIndex; lag++)
                    {
                        int rawIndex = DiffOrderD + modelIndex - lag;
                        DateTime requiredDate = GetGenerationResponseDate(covariates, rawIndex);
                        if (!positionsByDate.TryGetValue(requiredDate, out List<int>? matches))
                        {
                            throw new InvalidOperationException(
                                $"Covariate {covariateIndex + 1} is missing required timestamp {requiredDate:O} for raw response index {rawIndex}.");
                        }
                        if (matches.Count != 1)
                        {
                            throw new InvalidOperationException(
                                $"Covariate {covariateIndex + 1} contains duplicate required timestamp {requiredDate:O} for raw response index {rawIndex}.");
                        }
                        result[covariateIndex, modelIndex, lag] = matches[0];
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Gets the response timestamp represented by one generated raw output index.
        /// </summary>
        /// <param name="covariates">The supplied or extended level covariates.</param>
        /// <param name="rawIndex">The zero-based raw output index.</param>
        /// <returns>The observed, regularly extended, or covariate-defined response timestamp.</returns>
        /// <exception cref="InvalidOperationException">Neither response nor covariate timestamps cover the required index.</exception>
        private DateTime GetGenerationResponseDate(List<TimeSeries> covariates, int rawIndex)
        {
            if (TimeSeries != null && TimeSeries.Count > 0)
            {
                if (rawIndex < TimeSeries.Count)
                    return TimeSeries[rawIndex].Index;

                DateTime date = TimeSeries[TimeSeries.Count - 1].Index;
                for (int index = TimeSeries.Count; index <= rawIndex; index++)
                    date = TimeSeries.AddTimeInterval(date, TimeSeries.TimeInterval);
                return date;
            }

            if (covariates.Count == 0 || covariates[0].Count <= rawIndex)
                throw new InvalidOperationException($"No response or covariate timestamp is available for raw response index {rawIndex}.");
            return covariates[0][rawIndex].Index;
        }

        /// <summary>
        /// Gets observed transformed generation anchors when data are attached and zero anchors otherwise.
        /// </summary>
        /// <param name="anchorCount">The number of transformed anchors to return.</param>
        /// <returns>The requested transformed anchors.</returns>
        /// <exception cref="InvalidOperationException">Attached data do not contain every required transformed anchor.</exception>
        private double[] GetGenerationAnchors(int anchorCount)
        {
            var anchors = new double[anchorCount];
            if (TimeSeries == null || TimeSeries.Count == 0)
                return anchors;
            if (_transformedTimeSeries == null || _transformedTimeSeries.Count < anchorCount)
                throw new InvalidOperationException($"At least {anchorCount} transformed observations are required as generation anchors.");

            for (int i = 0; i < anchorCount; i++)
                anchors[i] = _transformedTimeSeries[i].Value;
            return anchors;
        }

        /// <summary>
        /// Reconstructs transformed levels from generated highest-order differences.
        /// </summary>
        /// <param name="differences">The completed transformed/differenced model-scale simulation.</param>
        /// <param name="sampleSize">The requested raw-scale output length.</param>
        /// <returns>Exactly <paramref name="sampleSize"/> transformed levels.</returns>
        private double[] IntegrateGeneratedDifferences(double[] differences, int sampleSize)
        {
            var workingAnchors = GetGenerationAnchors(DiffOrderD);
            var initialValues = new double[DiffOrderD];
            initialValues[0] = workingAnchors[0];
            int workingCount = DiffOrderD;
            for (int level = 1; level < DiffOrderD; level++)
            {
                for (int i = 0; i < workingCount - 1; i++)
                    workingAnchors[i] = workingAnchors[i + 1] - workingAnchors[i];
                workingCount--;
                initialValues[level] = workingAnchors[0];
            }

            double[] current = (double[])differences.Clone();
            for (int level = DiffOrderD - 1; level >= 0; level--)
            {
                var integrated = new double[current.Length + 1];
                integrated[0] = initialValues[level];
                for (int i = 0; i < current.Length; i++)
                    integrated[i + 1] = integrated[i] + current[i];
                current = integrated;
            }
            return current;
        }

        /// <summary>
        /// Converts a completed transformed-level simulation to the raw response scale.
        /// </summary>
        /// <param name="values">The complete simulated transformed-level series.</param>
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
