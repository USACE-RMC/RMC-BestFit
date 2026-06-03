using Numerics;
using Numerics.Distributions;
using Numerics.Functions;
using Numerics.MachineLearning;
using Numerics.Mathematics.Integration;
using Numerics.Mathematics.LinearAlgebra;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models.LinkFunctions;
using RMC.BestFit.Models.TrendFunctions.Support;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace RMC.BestFit.Models
{

    /// <summary>
    /// Bulletin 17C (B17C) distribution model.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    /// <b>Type-system design note:</b> Bulletin17CDistribution intentionally does NOT derive
    /// from <c>ModelBase</c> and does NOT implement <c>IModel</c>. The Bulletin 17C procedure
    /// is fit by Generalized Method of Moments (GMM) only — there is no log-likelihood and no
    /// posterior MCMC chain to expose. Implementing only <see cref="IGMMModel"/>,
    /// <see cref="ISimulatable{T}"/>, and <see cref="IUnivariateModel"/> keeps the surface
    /// minimal and prevents Bayesian-API consumers from accidentally calling LogLikelihood-
    /// shaped methods that would have no defined meaning here. <c>BootstrapDiagnostics</c>
    /// (exposed via the wrapping analysis) supplies the uncertainty quantification.
    /// </para>
    /// </remarks>
    public class Bulletin17CDistribution: IGMMModel, ISimulatable<double[]>, IUnivariateModel
    {

        #region Construction

        /// <summary>
        /// Constructs a new Bulletin 17C (B17C) distribution model using a Log-Pearson Type III distribution and no data.
        /// </summary>
        public Bulletin17CDistribution()
        {
            Distribution = CreateDistribution(UnivariateDistributionType.LogPearsonTypeIII);
            SetUpQuantilePenalties();
        }

        /// <summary>
        /// Constructs a new Bulletin 17C (B17C) distribution model.
        /// </summary>
        /// <param name="dataFrame">The data frame to fit to.</param>
        /// <param name="distribution">The univariate distribution.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="dataFrame"/> or <paramref name="distribution"/> is null.
        /// </exception>
        public Bulletin17CDistribution(DataFrame dataFrame, UnivariateDistributionBase distribution)
        {
            if (dataFrame is null) throw new ArgumentNullException(nameof(dataFrame));
            if (distribution is null) throw new ArgumentNullException(nameof(distribution));

            Distribution = distribution.Clone();
            DataFrame = dataFrame;
            SetUpQuantilePenalties();
        }

        /// <summary>
        /// Constructs a new Bulletin 17C (B17C) distribution model.
        /// </summary>
        /// <param name="dataFrame">The data frame to fit to.</param>
        /// <param name="distributionType">The univariate distribution type.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="dataFrame"/> is null.
        /// </exception>
        public Bulletin17CDistribution(DataFrame dataFrame, UnivariateDistributionType distributionType)
        {
            if (dataFrame == null) throw new ArgumentNullException(nameof(dataFrame));

            Distribution = CreateDistribution(distributionType);
            DataFrame = dataFrame;
            SetUpQuantilePenalties();
        }

        /// <summary>
        /// Constructs a Bulletin 17C distribution model from a serialized <see cref="XElement"/>.
        /// </summary>
        /// <param name="dataFrame">The data frame to associate with the model.</param>
        /// <param name="xElement">The XElement containing the serialized model state.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="dataFrame"/> or <paramref name="xElement"/> is null.
        /// </exception>
        /// <remarks>
        /// <para>
        ///     Follows the same deserialization pattern as <see cref="UnivariateDistribution(DataFrame, XElement)"/>.
        ///     Uses <see cref="BestFitLinkFunctionFactory"/> for deserializing link functions,
        ///     which supports both standard Numerics types and BestFit-specific types (SESLink, LogSESLink, CenteredLink).
        /// </para>
        /// </remarks>
        public Bulletin17CDistribution(DataFrame dataFrame, XElement xElement)
        {
            if (dataFrame == null) throw new ArgumentNullException(nameof(dataFrame));
            if (xElement == null) throw new ArgumentNullException(nameof(xElement));

            _isDeserializing = true;

            // Distribution
            var distElement = xElement.Element("Distribution");
            if (distElement != null)
                Distribution = UnivariateDistributionFactory.CreateDistribution(distElement);
            else
                Distribution = CreateDistribution(UnivariateDistributionType.LogPearsonTypeIII);

            DataFrame = dataFrame;

            // Parameters
            var parms = new List<ModelParameter>();
            var parmsElement = xElement.Element(nameof(Parameters));
            if (parmsElement != null)
            {
                foreach (XElement p in parmsElement.Elements(nameof(ModelParameter)))
                    parms.Add(new ModelParameter(p));
            }
            Parameters = parms;

            // Parameter penalties
            var paramPens = new List<ParameterPenalty>();
            var paramPensElement = xElement.Element(nameof(ParameterPenalties));
            if (paramPensElement != null)
            {
                foreach (XElement pen in paramPensElement.Elements(nameof(ParameterPenalty)))
                    paramPens.Add(new ParameterPenalty(pen));
            }
            ParameterPenalties = paramPens;

            // Quantile penalties
            var quantPens = new List<QuantilePenalty>();
            var quantPensElement = xElement.Element(nameof(QuantilePenalties));
            if (quantPensElement != null)
            {
                foreach (XElement pen in quantPensElement.Elements(nameof(QuantilePenalty)))
                    quantPens.Add(new QuantilePenalty(pen));
            }
            QuantilePenalties = quantPens;

            // Link controller — use BestFit factory for SESLink/LogSESLink/CenteredLink support
            var linkElement = xElement.Element("LinkController");
            if (linkElement != null)
            {
                var slots = linkElement.Elements("Link").ToList();
                var links = new ILinkFunction?[slots.Count];
                foreach (var slot in slots)
                {
                    var indexAttr = slot.Attribute("Index");
                    if (indexAttr == null) continue;
                    if (!int.TryParse(indexAttr.Value, out int index)) continue;
                    if (index < 0 || index >= links.Length) continue;
                    var child = slot.Elements().FirstOrDefault();
                    if (child != null)
                        links[index] = BestFitLinkFunctionFactory.CreateFromXElement(child);
                }
                _linkController = new LinkController(links);
            }
            else
            {
                _linkController = new LinkController();
            }

            _isDeserializing = false;
        }

        /// <summary>
        /// Constructs a Bulletin 17C distribution from a serialized <see cref="XElement"/> without a DataFrame.
        /// Used during undo restoration when InputData has been undone back to null.
        /// </summary>
        /// <param name="xElement">The serialized distribution state.</param>
        public Bulletin17CDistribution(XElement xElement)
        {
            if (xElement == null) throw new ArgumentNullException(nameof(xElement));

            _isDeserializing = true;

            // Distribution
            var distElement = xElement.Element("Distribution");
            if (distElement != null)
                Distribution = UnivariateDistributionFactory.CreateDistribution(distElement);
            else
                Distribution = CreateDistribution(UnivariateDistributionType.LogPearsonTypeIII);

            // DataFrame intentionally not set — InputData is null during this undo state

            // Parameters
            var parms = new List<ModelParameter>();
            var parmsElement = xElement.Element(nameof(Parameters));
            if (parmsElement != null)
            {
                foreach (XElement p in parmsElement.Elements(nameof(ModelParameter)))
                    parms.Add(new ModelParameter(p));
            }
            Parameters = parms;

            // Parameter penalties
            var paramPens = new List<ParameterPenalty>();
            var paramPensElement = xElement.Element(nameof(ParameterPenalties));
            if (paramPensElement != null)
            {
                foreach (XElement pen in paramPensElement.Elements(nameof(ParameterPenalty)))
                    paramPens.Add(new ParameterPenalty(pen));
            }
            ParameterPenalties = paramPens;

            // Quantile penalties
            var quantPens = new List<QuantilePenalty>();
            var quantPensElement = xElement.Element(nameof(QuantilePenalties));
            if (quantPensElement != null)
            {
                foreach (XElement pen in quantPensElement.Elements(nameof(QuantilePenalty)))
                    quantPens.Add(new QuantilePenalty(pen));
            }
            QuantilePenalties = quantPens;

            // Link controller
            var linkElement = xElement.Element("LinkController");
            if (linkElement != null)
            {
                var slots = linkElement.Elements("Link").ToList();
                var links = new ILinkFunction?[slots.Count];
                foreach (var slot in slots)
                {
                    var indexAttr = slot.Attribute("Index");
                    if (indexAttr == null) continue;
                    if (!int.TryParse(indexAttr.Value, out int index)) continue;
                    if (index < 0 || index >= links.Length) continue;
                    var child = slot.Elements().FirstOrDefault();
                    if (child != null)
                        links[index] = BestFitLinkFunctionFactory.CreateFromXElement(child);
                }
                _linkController = new LinkController(links);
            }
            else
            {
                _linkController = new LinkController();
            }

            _isDeserializing = false;
        }

        #endregion

        #region Members

        /// <summary>
        /// The set of univariate distribution types supported by the Bulletin 17C model.
        /// </summary>
        private static readonly HashSet<UnivariateDistributionType> _supportedDistributionTypes = new()
        {
            UnivariateDistributionType.Exponential,
            UnivariateDistributionType.GammaDistribution,
            UnivariateDistributionType.LogNormal,
            UnivariateDistributionType.LogPearsonTypeIII,
            UnivariateDistributionType.Normal,
            UnivariateDistributionType.PearsonTypeIII,
        };

        /// <summary>
        /// Determines whether the specified distribution type is supported by the Bulletin 17C model.
        /// </summary>
        /// <param name="distributionType">The distribution type to check.</param>
        /// <returns><c>true</c> if the distribution type is supported; otherwise, <c>false</c>.</returns>
        /// <remarks>
        /// The Bulletin 17C model supports: Exponential, Gamma, Log-Normal, Log-Pearson Type III, Normal, and Pearson Type III.
        /// </remarks>
        public static bool IsSupportedDistributionType(UnivariateDistributionType distributionType)
        {
            return _supportedDistributionTypes.Contains(distributionType);
        }

        /// <summary>
        /// Flag to suppress expensive default parameter initialization during XElement deserialization.
        /// When true, the DataFrame and Distribution setters skip SetDefaultParameters() since persisted values will be restored from the XElement.
        /// </summary>
        private bool _isDeserializing = false;

        private DataFrame _dataFrame = null!;
        private UnivariateDistributionBase _distribution = null!;
        private List<ModelParameter> _parameters = new List<ModelParameter>();
        private List<ParameterPenalty> _parameterPenalties = new List<ParameterPenalty>();
        private List<QuantilePenalty> _quantilePenalties = new List<QuantilePenalty>();
        private PenaltyFunction _penaltyFunction = null!;
        private LinkController _linkController = new LinkController();

        /// <summary>
        /// Gets or sets the link controller for parameter transformations.
        /// </summary>
        /// <remarks>
        /// <para>
        ///     The link controller manages per-parameter link functions that transform parameters
        ///     between real-space and link-space during GMM estimation. Setting to null resets
        ///     to an empty (identity) controller.
        /// </para>
        /// </remarks>
        public LinkController LinkController
        {
            get { return _linkController; }
            set
            {
                _linkController = value ?? new LinkController();
                RaisePropertyChange(nameof(LinkController));
            }
        }

        /// <summary>
        /// Gets or sets the input data frame used by the model.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When the data frame changes, this property:
        /// </para>
        /// <list type="bullet">
        /// <item><description>
        /// Subscribes to <see cref="DataFrame.PropertyChanged"/>.
        /// </description></item>
        /// <item><description>
        /// Calls <see cref="DataFrame.ProcessThresholdSeries"/> when
        /// <see cref="ModelBase.UseDefaultFlatPriors"/> is true, via
        /// <see cref="DataFrame_PropertyChanged"/>.
        /// </description></item>
        /// <item><description>
        /// Optionally calls <see cref="SetDefaultParameters"/> (defined in a derived
        /// class) when <c>UseDefaultFlatPriors</c> is true.
        /// </description></item>
        /// </list>
        /// </remarks>
        public DataFrame DataFrame
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

                    if (!_isDeserializing)
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

                RaisePropertyChange(nameof(Distribution));

                if (!_isDeserializing)
                {
                    SetDefaultParameters();
                }
            }
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Bulletin 17C analysis assumes a stationary parent population — there is no
        /// trend-on-parameters concept in the B17C methodology. Always returns <c>false</c>.
        /// </remarks>
        public bool IsNonstationary => false;

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

        /// <inheritdoc/>
        public List<ModelParameter> Parameters
        {
            get { return _parameters; }
            private set
            {
                if (_parameters != null)
                {
                    for (int i = 0; i < _parameters.Count; i++)
                        _parameters[i].PropertyChanged -= Parameter_PropertyChanged;
                }
                _parameters = value;

                if (_parameters != null)
                {
                    for (int i = 0; i < _parameters.Count; i++)
                        _parameters[i].PropertyChanged += Parameter_PropertyChanged;
                }
                RaisePropertyChange(nameof(Parameters));
            }
        }

        /// <inheritdoc/>
        public int NumberOfParameters => Parameters.Count;

        /// <inheritdoc/>
        public int NumberOfMomentConditions => Parameters.Count;

        /// <summary>
        /// Gets the collection of parameter penalites.
        /// </summary>
        public List<ParameterPenalty> ParameterPenalties
        {
            get { return _parameterPenalties; }
            private set
            {
                if (_parameterPenalties != null)
                {
                    for (int i = 0; i < _parameterPenalties.Count; i++)
                        _parameterPenalties[i].PropertyChanged -= ParameterPenalty_PropertyChanged;
                }
                _parameterPenalties = value;

                if (_parameterPenalties != null)
                {
                    for (int i = 0; i < _parameterPenalties.Count; i++)
                        _parameterPenalties[i].PropertyChanged += ParameterPenalty_PropertyChanged;
                }
                RaisePropertyChange(nameof(ParameterPenalties));
            }
        }


        /// <summary>
        /// Gets the collection of quantile penalites. 
        /// </summary>
        public List<QuantilePenalty> QuantilePenalties
        {
            get { return _quantilePenalties; }
            private set
            {
                if (_quantilePenalties != null)
                {
                    for (int i = 0; i < _quantilePenalties.Count; i++)
                        _quantilePenalties[i].PropertyChanged -= QuantilePenalty_PropertyChanged;
                }
                _quantilePenalties = value;

                if (_quantilePenalties != null)
                {
                    for (int i = 0; i < _quantilePenalties.Count; i++)
                        _quantilePenalties[i].PropertyChanged += QuantilePenalty_PropertyChanged;
                }
                RaisePropertyChange(nameof(QuantilePenalties));
            }
        }

        /// <inheritdoc/>
        public int SampleSize => DataFrame?.TotalRecordLength() ?? 0;

        /// <inheritdoc/>
        public MomentConditionFunction MomentConditionFunction => MomentConditions;

        /// <inheritdoc/>
        public JacobianFunction? JacobianFunction => null;

        /// <inheritdoc/>
        public PenaltyFunction? PenaltyFunction => _penaltyFunction;

        /// <inheritdoc/>
        public PointwiseMomentConditionFunction? PointwiseMomentConditions => PointwiseMomentConditionsImpl;

        /// <inheritdoc/>
        public event PropertyChangedEventHandler? PropertyChanged;

        #endregion

        #region Methods

        /// <summary>
        /// Raise property changed event.
        /// </summary>
        /// <param name="propertyName">Name of property that changed.</param>
        private void RaisePropertyChange(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Handles the <see cref="DataFrame.PropertyChanged"/> event.
        /// </summary>
        /// <param name="sender">The data frame that raised the event.</param>
        /// <param name="e">The event data.</param>
        /// <remarks>
        /// <para>
        /// The base implementation reprocesses threshold series and calls
        /// <see cref="SetDefaultParameters"/> in the derived class.
        /// </para>
        /// <para>
        /// Changes to plotting parameter properties are ignored because they
        /// do not affect moment conditions.
        /// </para>
        /// </remarks>
        private void DataFrame_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DataFrame.PlottingParameter) ||
                e.PropertyName == "PlottingPosition")
            {
                return;
            }

            if (DataFrame is null)
                return;

            DataFrame.ProcessThresholdSeries();
            RaisePropertyChange(nameof(DataFrame));

            // Set default parameters
            SetDefaultParameters();
        }

        /// <summary>
        /// Handles the model parameter property changed event.
        /// </summary>
        /// <param name="sender">The parameter that raised the event.</param>
        /// <param name="e">The event data.</param>
        private void Parameter_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ModelParameter.PriorDistribution))
                RaisePropertyChange(nameof(Parameters));
        }

        /// <summary>
        /// Handles the parameter penalty changed event.
        /// </summary>
        /// <param name="sender">The parameter penalty that raised the event.</param>
        /// <param name="e">The event data.</param>
        private void ParameterPenalty_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            RaisePropertyChange(nameof(ParameterPenalties));
        }

        /// <summary>
        /// Handles the quantile penalty changed event.
        /// </summary>
        /// <param name="sender">The quantile penalty that raised the event.</param>
        /// <param name="e">The event data.</param>
        private void QuantilePenalty_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            RaisePropertyChange(nameof(QuantilePenalties));
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
            else if (distributionType == UnivariateDistributionType.LogNormal)
                return new LogNormal();
            else if (distributionType == UnivariateDistributionType.LogPearsonTypeIII)
                return new LogPearsonTypeIII();
            else if (distributionType == UnivariateDistributionType.Normal)
                return new Normal();
            else if (distributionType == UnivariateDistributionType.PearsonTypeIII)
                return new PearsonTypeIII();
            else
                throw new ArgumentOutOfRangeException(nameof(distributionType), distributionType, "Unsupported distribution type.");
        }

        /// <summary>
        /// Sets initial parameter values based on the input data and distribution constraints.
        /// </summary>
        public void SetInitialParameters()
        {
            try
            {
                // Get data values for parameter constraints
                var data = new List<double>();
                data.AddRange(DataFrame.ExactSeries.Select(x => x.Value));
                data.AddRange(DataFrame.UncertainSeries.Select(x => x.Value));
                data.AddRange(DataFrame.IntervalSeries.Select(x => x.Value));

                // Get typical parameter constraints (initials, lower, upper bounds)
                var tuple = ((IMaximumLikelihoodEstimation)Distribution).GetParameterConstraints(data);
                var initials = tuple.Item1;
                var lowers = tuple.Item2;
                var uppers = tuple.Item3;

                // Override initials with nonparametric moment estimates when censored/uncertain data exists.
                // Use ROS (Regression on Order Statistics) to impute low-outlier values, which avoids
                // the severe moment distortion caused by log-transforming near-zero or zero flows.
                if (DataFrame.NumberOfLowOutliers > 0 ||
                    DataFrame.UncertainSeries.Count > 0 ||
                    DataFrame.IntervalSeries.Count > 0 ||
                    DataFrame.ThresholdSeries.Count > 0)
                {
                    bool useLog10 = DistributionType == UnivariateDistributionType.LogNormal ||
                                    DistributionType == UnivariateDistributionType.LogPearsonTypeIII;
                    var npMoments = DataFrame.GetNonparametricMomentsROS(useLog10);

                    if (npMoments != null)
                    {
                        bool allFinite = true;
                        for (int i = 0; i < Distribution.NumberOfParameters; i++)
                        {
                            if (!Tools.IsFinite(npMoments[i]))
                            {
                                allFinite = false;
                                break;
                            }
                        }
                        if (allFinite)
                        {
                            initials = ((IMomentEstimation)Distribution).ParametersFromMoments(npMoments);
                        }
                    }
                }

                // Clamp initials to bounds
                for (int i = 0; i < Distribution.NumberOfParameters; i++)
                {
                    if (initials[i] < lowers[i] || initials[i] > uppers[i])
                    {
                        initials[i] = 0.5 * (lowers[i] + uppers[i]);
                    }
                    Parameters[i].Value = initials[i];
                }
            }
            catch
            {
                // If parameter initialization failed, log a warning and set initials to mid point of bounds
                Debug.WriteLine("Parameter initialization failed.");
                
                // Make initials mid point of bounds
                for (int i = 0; i < Distribution.NumberOfParameters; i++)
                {
                    Parameters[i].Value = 0.5 * (Parameters[i].LowerBound + Parameters[i].UpperBound);
                }
            }
        }


        /// <inheritdoc/>
        public void SetDefaultParameters()
        {
            // Remove old handlers
            if (_parameters != null && _parameters.Count > 0)
            {
                for (int i = 0; i < _parameters.Count; i++)
                    _parameters[i].PropertyChanged -= Parameter_PropertyChanged;
            }

            // Remove old parameter penalty handlers
            for (int i = 0; i < ParameterPenalties.Count; i++)
                ParameterPenalties[i].PropertyChanged -= ParameterPenalty_PropertyChanged;

            _parameters = new List<ModelParameter>();
            _parameterPenalties = new List<ParameterPenalty>();

            if (Distribution is null || DataFrame is null || !DataFrame.Validate().IsValid || DataFrame.ExactSeries is null || DataFrame.ExactSeries.Count == 0)
            {
                // Build parameter name shells so the UI penalty grid shows the correct
                // rows even before InputData is selected. Values/bounds are left at defaults.
                if (Distribution is not null)
                {
                    var parametersToString = Distribution.ParametersToString;
                    for (int i = 0; i < Distribution.NumberOfParameters; i++)
                    {
                        // Add parameter
                        _parameters.Add(new ModelParameter()
                        {
                            Name = parametersToString[i, 0],
                        });
                        _parameters.Last().PropertyChanged += Parameter_PropertyChanged;

                        // Add penalty for parameter
                        var penalty = new ParameterPenalty() { Name = _parameters.Last().DisplayName };
                        penalty.PropertyChanged += ParameterPenalty_PropertyChanged;
                        _parameterPenalties.Add(penalty);
                    }

                }
                RaisePropertyChange(nameof(SetDefaultParameters));
                return;
            }

            try
            {
                // Get data values for parameter constraints
                var data = new List<double>();
                data.AddRange(DataFrame.ExactSeries.Select(x => x.Value));
                data.AddRange(DataFrame.UncertainSeries.Select(x => x.Value));
                data.AddRange(DataFrame.IntervalSeries.Select(x => x.Value));

                // Get typical parameter constraints (initials, lower, upper bounds)
                var tuple = ((IMaximumLikelihoodEstimation)Distribution).GetParameterConstraints(data);
                var initials = tuple.Item1;
                var lowers = tuple.Item2;
                var uppers = tuple.Item3;

                // Override initials with nonparametric moment estimates when censored/uncertain data exists.
                // Use ROS (Regression on Order Statistics) to impute low-outlier values, which avoids
                // the severe moment distortion caused by log-transforming near-zero or zero flows.
                if (DataFrame.NumberOfLowOutliers > 0 ||
                    DataFrame.UncertainSeries.Count > 0 ||
                    DataFrame.IntervalSeries.Count > 0 ||
                    DataFrame.ThresholdSeries.Count > 0)
                {
                    bool useLog10 = DistributionType == UnivariateDistributionType.LogNormal ||
                                    DistributionType == UnivariateDistributionType.LogPearsonTypeIII;
                    var npMoments = DataFrame.GetNonparametricMomentsROS(useLog10);

                    if (npMoments != null)
                    {
                        bool allFinite = true;
                        for (int i = 0; i < Distribution.NumberOfParameters; i++)
                        {
                            if (!Tools.IsFinite(npMoments[i]))
                            {
                                allFinite = false;
                                break;
                            }
                        }
                        if (allFinite)
                        {
                            initials = ((IMomentEstimation)Distribution).ParametersFromMoments(npMoments);
                        }
                    }
                }

                // Clamp initials to bounds
                for (int i = 0; i < Distribution.NumberOfParameters; i++)
                {
                    if (initials[i] < lowers[i] || initials[i] > uppers[i])
                    {
                        initials[i] = 0.5 * (lowers[i] + uppers[i]);
                    }
                }

                // Build model parameters with uniform priors
                var parametersToString = Distribution.ParametersToString;
                for (int i = 0; i < Distribution.NumberOfParameters; i++)
                {
                    // Add parameter
                    _parameters.Add(new ModelParameter()
                    {
                        Name = parametersToString[i, 0],
                        Value = initials[i],
                        LowerBound = lowers[i],
                        UpperBound = uppers[i],
                        PriorDistribution = new Uniform(lowers[i], uppers[i])
                    });
                    _parameters.Last().PropertyChanged += Parameter_PropertyChanged;

                    // Add penalty for parameter
                    var penalty = new ParameterPenalty() { Name = Parameters.Last().DisplayName };
                    penalty.PropertyChanged += ParameterPenalty_PropertyChanged;
                    _parameterPenalties.Add(penalty);

                }

            }
            catch (Exception ex)
            {
                // If parameter initialization fails, leave parameters empty.
                System.Diagnostics.Debug.WriteLine(
                    $"Bulletin17CDistribution.SetDefaultParameters failed: {ex.Message}");
                _parameters = new List<ModelParameter>();
                _parameterPenalties = new List<ParameterPenalty>();
            }

            RaisePropertyChange(nameof(SetDefaultParameters));
        }

        /// <summary>
        /// Sets up single quantile penalty.
        /// </summary>
        private void SetUpQuantilePenalties()
        {
            for (int i = 0; i < QuantilePenalties.Count; i++)
                QuantilePenalties[i].PropertyChanged -= QuantilePenalty_PropertyChanged;
            var penalty = new QuantilePenalty();
            penalty.PropertyChanged += QuantilePenalty_PropertyChanged;
            QuantilePenalties.Add(penalty);
            RaisePropertyChange(nameof(QuantilePenalties));
        }

        /// <inheritdoc/>
        public virtual void SetParameterValues(IList<double> parameters)
        {
            if (parameters.Count != NumberOfParameters) throw new ArgumentException("The list of parameter values are the wrong length", nameof(parameters));
            for (int i = 0; i < NumberOfParameters; i++)
                Parameters[i].Value = parameters[i];
            Distribution.SetParameters(parameters);
        }


        /// <summary>
        /// Returns the penalty function. 
        /// </summary>
        /// <param name="seed">PRNG seed. If negative, treat as deterministic.</param>
        public void SetPenaltyFunction(Random prng = null!)
        {
            // Determine if there any penalties
            int nPenalty = ParameterPenalties.Where(x => x.Enabled).Count() + QuantilePenalties.Where(x => x.Enabled).Count();
            if (nPenalty == 0) { _penaltyFunction = null!; return; }

            int nt = DataFrame.TotalRecordLength();

            // if seed < 0, penalty function is deterministic
            if (prng == null)
            {
                double PenaltyFunction(double[] parameters)
                {
                    var theta = LinkController.InverseLink(parameters);
                    double result = 0;
                    for (int i = 0; i < ParameterPenalties.Count; i++)
                    {
                        if (ParameterPenalties[i].Enabled)
                        {
                            result += ParameterPenalties[i].Function(theta[i], nt);
                        }
                    }
                    for (int i = 0; i < QuantilePenalties.Count; i++)
                    {
                        if (QuantilePenalties[i].Enabled)
                        {
                            var dist = Distribution.Clone();
                            dist.SetParameters(theta);
                            var aep = QuantilePenalties[i].AEP;
                            result += QuantilePenalties[i].Function(dist.InverseCDF(1 - aep), nt);
                        }
                    }
                    return result;
                }
                _penaltyFunction = PenaltyFunction;
                return;
            }
            else
            {

                // Create random parameter penalties
                var paramPenalties = new ParameterPenalty[ParameterPenalties.Count];
                for (int i = 0; i < ParameterPenalties.Count; i++)
                {
                    paramPenalties[i] = ParameterPenalties[i].Clone();
                    if (paramPenalties[i].Enabled)
                    {
                        paramPenalties[i].Mean = paramPenalties[i].Mean + Math.Sqrt(paramPenalties[i].MSE) * Normal.StandardZ(prng.NextDouble());
                    }
                }

                // Create random quantile penalties
                var quantPenalties = new QuantilePenalty[QuantilePenalties.Count];
                for (int i = 0; i < QuantilePenalties.Count; i++)
                {
                    quantPenalties[i] = QuantilePenalties[i].Clone();
                    if (quantPenalties[i].Enabled)
                    {
                        quantPenalties[i].Mean = quantPenalties[i].Mean + Math.Sqrt(quantPenalties[i].MSE) * Normal.StandardZ(prng.NextDouble());
                    }
                }

                // Create randomized penalty function
                double RandomizedPenaltyFunction(double[] parameters)
                {
                    var theta = LinkController.InverseLink(parameters);
                    double result = 0;
                    for (int i = 0; i < paramPenalties.Length; i++)
                    {
                        if (paramPenalties[i].Enabled)
                        {
                            result += paramPenalties[i].Function(theta[i], nt);
                        }
                    }
                    for (int i = 0; i < quantPenalties.Length; i++)
                    {
                        if (quantPenalties[i].Enabled)
                        {
                            var dist = Distribution.Clone();
                            dist.SetParameters(theta);
                            var aep = quantPenalties[i].AEP;
                            result += quantPenalties[i].Function(dist.InverseCDF(1 - aep), nt);
                        }
                    }
                    return result;
                }
                _penaltyFunction = RandomizedPenaltyFunction;
                return;
            }
        }

        /// <summary>
        /// Sets a randomized penalty function for bootstrap uncertainty analysis.
        /// </summary>
        /// <param name="parentParameters">The parent distribution parameters.</param>
        /// <param name="prng">The pseudo random number generator used to peturb penalties.</param>
        public void SetRandomPenaltyFunction(double[] parentParameters, Random prng)
        {
            // Determine if there any penalties
            int nPenalty = ParameterPenalties.Where(x => x.Enabled).Count() + QuantilePenalties.Where(x => x.Enabled).Count();
            if (nPenalty == 0) { _penaltyFunction = null!; return; }
            // Make sure inputs are valid
            if (parentParameters.Length != NumberOfParameters)
                throw new ArgumentOutOfRangeException("The parent parameter length is invalid.", nameof(parentParameters));
            if (prng is null)
                throw new ArgumentNullException(nameof(prng));

            int nt = DataFrame.TotalRecordLength();

            // Create random parameter penalties centered at the parent parameters
            var paramPenalties = new ParameterPenalty[ParameterPenalties.Count];
            for (int i = 0; i < ParameterPenalties.Count; i++)
            {
                paramPenalties[i] = ParameterPenalties[i].Clone();
                if (paramPenalties[i].Enabled)
                {
                    paramPenalties[i].Mean = parentParameters[i] + Math.Sqrt(paramPenalties[i].MSE) * Normal.StandardZ(prng.NextDouble());
                }
            }

            // Create random quantile penalties centered at the parent quantile
            var parentDist = Distribution.Clone();
            parentDist.SetParameters(parentParameters);
            var quantPenalties = new QuantilePenalty[QuantilePenalties.Count];
            for (int i = 0; i < QuantilePenalties.Count; i++)
            {
                quantPenalties[i] = QuantilePenalties[i].Clone();
                if (quantPenalties[i].Enabled)
                {
                    double qMean = quantPenalties[i].UseLog10 ? Tools.Log10(parentDist.InverseCDF(1 - quantPenalties[i].AEP)) : parentDist.InverseCDF(1 - quantPenalties[i].AEP);
                    quantPenalties[i].Mean = qMean + Math.Sqrt(quantPenalties[i].MSE) * Normal.StandardZ(prng.NextDouble());
                }
            }

            // Create bootstrap penalty function
            double BootstrapPenaltyFunction(double[] parameters)
            {
                var theta = LinkController.InverseLink(parameters);
                double result = 0;
                for (int i = 0; i < paramPenalties.Length; i++)
                {
                    if (paramPenalties[i].Enabled)
                    {
                        result += paramPenalties[i].Function(theta[i], nt);
                    }
                }
                for (int i = 0; i < quantPenalties.Length; i++)
                {
                    if (quantPenalties[i].Enabled)
                    {
                        var dist = Distribution.Clone();
                        dist.SetParameters(theta);
                        var aep = quantPenalties[i].AEP;
                        result += quantPenalties[i].Function(dist.InverseCDF(1 - aep), nt);
                    }
                }
                return result;
            }
            // Set penalty function
            _penaltyFunction = BootstrapPenaltyFunction;
            return;
        }



        /// <summary>
        /// Computes the GMM moment condition vector G and its covariance matrix S.
        /// </summary>
        /// <param name="parameters">Parameter values in link space.</param>
        /// <returns>
        /// A tuple (G, S) where G is the q×1 sample mean of moment conditions and
        /// S is the q×q covariance matrix used to form the optimal weighting matrix W = S⁻¹.
        /// </returns>
        /// <remarks>
        /// <para>
        /// The moment conditions are g(Y; θ) = [Y − μ, (Y − μ)² − σ², (Y − μ)³ − μ₃],
        /// where μ, σ², μ₃ are the unconditional central moments of the fitted distribution.
        /// For q = 2 distributions (Exponential, Gamma), only the first two conditions are used.
        /// </para>
        /// <para>
        /// <b>Exact data:</b> Observed values with Bessel small-sample corrections c₂ = n/(n−1)
        /// for variance and c₃ = n²/((n−1)(n−2)) for skewness. The covariance E[gg'] is computed
        /// using model higher central moments μ₄–μ₆ for supported families (Normal, Gamma, Pearson III),
        /// or via outer-product fallback for other families.
        /// </para>
        /// <para>
        /// <b>Censored, interval, and threshold data:</b> Conditional moments E[Y^k | A]
        /// are computed via numerical integration (<see cref="UnivariateDistributionBase.ConditionalMoments"/>),
        /// and the covariance uses the outer-product approximation E[gg'|A] ≈ E[g|A] E[g|A]'.
        /// </para>
        /// <para>
        /// <b>Uncertain data:</b> Moments and their second moments are integrated over
        /// the measurement-error distribution with 20-point Gauss-Legendre quadrature and
        /// normalized by the retained probability mass. The covariance contribution uses
        /// E_ME[gg'] for uncertain rows so within-measurement-error spread is represented
        /// directly in the GMM weighting matrix.
        /// </para>
        /// <para>
        /// <b>Low outliers:</b> When present, model-based covariance is disabled globally to avoid
        /// bias from the mixture of censored and exact contributions.
        /// </para>
        /// </remarks>
        public (Vector G, Matrix S) MomentConditions(double[] parameters)
        {
            parameters = _linkController.InverseLink(parameters);

            int q = NumberOfParameters;
            int n = DataFrame.TotalRecordLength();

            // Create result holders
            var mean = new Vector(q);
            var covariance = new Matrix(q);

            // Bessel small-sample correction factors for non-outlier exact data
            int Ns = 0;
            foreach (ExactData data in DataFrame.ExactSeries)
                if (!data.IsLowOutlier) Ns++;
            double c2 = Ns >= 2 ? Ns / (double)(Ns - 1) : 1.0;
            double c3 = Ns >= 3 ? (double)(Ns * Ns) / ((Ns - 1) * (Ns - 2)) : 1.0;

            // Configure distribution model and log-space flag
            UnivariateDistributionBase model;
            bool isLog10 = false;

            // Disable model-based covariance when low outliers are present, since the
            // unconditional μ₄–μ₆ formulas don't account for the truncated contribution
            bool useModelCovariance = DataFrame.NumberOfLowOutliers == 0;

            if (DistributionType == UnivariateDistributionType.LogPearsonTypeIII)
            {
                model = CreateDistribution(UnivariateDistributionType.PearsonTypeIII);
                isLog10 = true;
            }
            else if (DistributionType == UnivariateDistributionType.LogNormal)
            {
                model = CreateDistribution(UnivariateDistributionType.Normal);
                isLog10 = true;
            }
            else
            {
                model = Distribution.Clone();
            }

            // Validate parameters without throwing — this is called thousands of times
            // during GMM optimization and exception overhead is significant
            var validation = model.ValidateParameters(parameters, false);
            if (validation is not null)
            {
                // Invalid parameters — signal the optimizer to reject this parameter set
                mean.Fill(double.MaxValue);
                return (mean, covariance);
            }
            model.SetParameters(parameters);

            // Integration bounds from distribution support
            double min = model.InverseCDF(Tools.DoubleMachineEpsilon);
            double max = model.InverseCDF(1 - Tools.DoubleMachineEpsilon);

            // Unconditional central moments of the fitted distribution
            double mu = model.Mean;
            double sigma = model.StandardDeviation;
            double sigma2 = model.Variance;
            double skewness = model.Skewness;

            // Guard against non-finite moments from extreme parameter values during optimization
            if (!Tools.IsFinite(mu) || !Tools.IsFinite(sigma2) || sigma <= 0)
            {
                mean.Fill(double.MaxValue);
                return (mean, covariance);
            }

            // Store unconditional moments: [μ, σ², μ₃]
            var unconditionalMoments = new double[q];
            unconditionalMoments[0] = mu;
            if (q >= 2) unconditionalMoments[1] = sigma2;
            if (q >= 3) unconditionalMoments[2] = skewness * sigma * sigma * sigma;

            // Reusable buffer for conditional moments
            var conditionalMoments = new double[q];

            // Low outlier moments — treated as left-censored below the low outlier threshold
            if (DataFrame.NumberOfLowOutliers > 0)
            {
                var lower = min;
                var upper = Math.Min(max, isLog10 ? Tools.Log10(DataFrame.LowOutlierThreshold) : DataFrame.LowOutlierThreshold);
                if (lower >= upper)
                    conditionalMoments.Fill(0);
                else
                    conditionalMoments = model.ConditionalMoments(lower, upper);
                UpdateMomentMeanCovariance(conditionalMoments, unconditionalMoments, mean, covariance, DataFrame.NumberOfLowOutliers, true, false);
            }

            // Exact data — non-outlier observed values with Bessel corrections
            foreach (ExactData data in DataFrame.ExactSeries)
            {
                if (!data.IsLowOutlier)
                {
                    double value = isLog10 ? data.Log10Value : data.Value;
                    double d = value - unconditionalMoments[0];
                    double d2 = d * d;
                    conditionalMoments[0] = value;
                    if (q >= 2) conditionalMoments[1] = c2 * d2;
                    if (q >= 3) conditionalMoments[2] = c3 * d2 * d;
                    UpdateMomentMeanCovariance(conditionalMoments, unconditionalMoments, mean, covariance, 1, false, useModelCovariance, sigma, skewness);
                }
            }

            // Uncertain data — integrate over measurement error distribution
            foreach (UncertainData data in DataFrame.UncertainSeries)
            {
                var measurementErrorSecondMoment = new Matrix(q);
                var uncertainMoments = ConditionalMomentsForUncertainData(unconditionalMoments, data, isLog10, measurementErrorSecondMoment);

                // Law of total variance for uncertain observations:
                // keep the usual hydrologic/process covariance contribution, then add the
                // ME-induced second moment. The row update handles whether the conditional
                // mean outer product has already been included by the selected covariance path.
                UpdateMomentMeanCovariance(uncertainMoments, unconditionalMoments, mean, covariance, 1, false, useModelCovariance, sigma, skewness, measurementErrorSecondMoment);
            }

            // Interval data — conditional moments over [L, U]
            foreach (IntervalData data in DataFrame.IntervalSeries)
            {
                var lower = Math.Max(min, isLog10 ? data.Log10LowerValue : data.LowerValue);
                var upper = Math.Min(max, isLog10 ? data.Log10UpperValue : data.UpperValue);
                if (lower >= upper)
                    conditionalMoments.Fill(0);
                else
                    conditionalMoments = model.ConditionalMoments(lower, upper);
                UpdateMomentMeanCovariance(conditionalMoments, unconditionalMoments, mean, covariance, 1, true, false);
            }

            // Threshold data — left-censored (Y < threshold) and right-censored (Y >= threshold)
            foreach (ThresholdData data in DataFrame.ThresholdSeries)
            {
                // Left censored
                if (data.NumberBelow > 0)
                {
                    var lower = min;
                    var upper = Math.Min(max, isLog10 ? data.Log10Value : data.Value);
                    if (lower >= upper)
                        conditionalMoments.Fill(0);
                    else
                        conditionalMoments = model.ConditionalMoments(lower, upper);
                    UpdateMomentMeanCovariance(conditionalMoments, unconditionalMoments, mean, covariance, data.NumberBelow, true, false);
                }
                // Right censored
                if (data.NumberAbove > 0)
                {
                    var lower = Math.Min(max, isLog10 ? data.Log10Value : data.Value);
                    var upper = max;
                    if (lower >= upper)
                        conditionalMoments.Fill(0);
                    else
                        conditionalMoments = model.ConditionalMoments(lower, upper);
                    UpdateMomentMeanCovariance(conditionalMoments, unconditionalMoments, mean, covariance, data.NumberAbove, true, false);
                }
            }

            // Detect non-finite values and signal failure to the optimizer
            RepairErrors(mean.Array, covariance.Array);

            // Normalize to sample means and form covariance: Cov(g) = E[gg'] - E[g]E[g]'
            mean /= (double)n;
            covariance /= (double)n;
            covariance = covariance - Matrix.Outer(mean, mean);
            return (mean, covariance);
        }


        /// <summary>
        /// Detects non-finite values (NaN or Infinity) in the moment condition vector or covariance matrix
        /// and replaces them with sentinel values that signal the optimizer to reject the parameter set.
        /// </summary>
        /// <param name="errors">The moment condition vector (mean of g). Filled with <see cref="double.MaxValue"/> if any non-finite value is detected.</param>
        /// <param name="covariance">The covariance accumulator. Filled with zeros if any non-finite value is detected.</param>
        /// <remarks>
        /// Non-finite values can arise from invalid parameter combinations that produce degenerate distributions
        /// (e.g., zero variance), numerical overflow in moment computations, or failed integration in
        /// <see cref="UnivariateDistributionBase.ConditionalMoments"/>. Setting errors to MaxValue ensures the
        /// GMM objective Q(θ) = g'Wg is very large, steering the optimizer away from the invalid region.
        /// </remarks>
        private void RepairErrors(double[] errors, double[,] covariance)
        {
            bool hasBadValue = false;

            // Check the moment condition vector
            for (int i = 0; i < errors.Length; i++)
            {
                if (!Tools.IsFinite(errors[i]))
                {
                    hasBadValue = true;
                    break;
                }
            }

            // Check the covariance matrix
            if (!hasBadValue)
            {
                int rows = covariance.GetLength(0);
                int cols = covariance.GetLength(1);
                for (int i = 0; i < rows && !hasBadValue; i++)
                    for (int j = 0; j < cols && !hasBadValue; j++)
                        if (!Tools.IsFinite(covariance[i, j]))
                            hasBadValue = true;
            }

            if (hasBadValue)
            {
                errors.Fill(double.MaxValue);
                covariance.Fill(0);
            }
        }

        /// <summary>
        /// Computes the 4th, 5th, and 6th central moments of a Pearson Type III distribution
        /// from its standard deviation σ and skewness coefficient γ.
        /// </summary>
        /// <param name="sigma">The standard deviation σ of the distribution.</param>
        /// <param name="gamma">The skewness coefficient γ of the distribution.</param>
        /// <param name="mu4">Output: μ₄ = σ⁴(3 + 3γ²/2).</param>
        /// <param name="mu5">Output: μ₅ = σ⁵γ(10 + 3γ²).</param>
        /// <param name="mu6">Output: μ₆ = σ⁶(15 + 65γ²/2 + 15γ⁴/2).</param>
        /// <remarks>
        /// <para>
        /// These formulas are derived from the Pearson Type III moment-generating function.
        /// The Pearson Type III has shape α = (2/γ)², scale β = σγ/2, so the raw central
        /// moments can be expressed in closed form as functions of σ and γ.
        /// </para>
        /// <para>
        /// Special cases: For the Normal distribution (γ = 0), these reduce to
        /// μ₄ = 3σ⁴, μ₅ = 0, μ₆ = 15σ⁶. For the Exponential (γ = 2, σ = 1/λ),
        /// they reduce to μ₄ = 9σ⁴, μ₅ = 44σ⁵, μ₆ = 265σ⁶.
        /// </para>
        /// <para>
        /// Reference: Stuart, A. and Ord, J.K. (1994). Kendall's Advanced Theory of Statistics,
        /// Volume 1: Distribution Theory. 6th Edition, Chapter 3.
        /// </para>
        /// </remarks>
        private static void Mu456_Pearson3_FromSigmaGamma(double sigma, double gamma, out double mu4, out double mu5, out double mu6)
        {
            double s2 = sigma * sigma;
            double s4 = s2 * s2;
            double s5 = s4 * sigma;
            double s6 = s4 * s2;
            double g2 = gamma * gamma;
            double g4 = g2 * g2;

            mu4 = s4 * (3.0 + 1.5 * g2);
            mu5 = s5 * gamma * (10.0 + 3.0 * g2);
            mu6 = s6 * (15.0 + 32.5 * g2 + 7.5 * g4); // 65/2 = 32.5; 15/2 = 7.5
        }


        /// <summary>
        /// Computes conditional central moments for an uncertain data observation by integrating
        /// over the measurement error distribution using 20-point Gauss-Legendre quadrature.
        /// </summary>
        /// <param name="unconditionalMoments">
        /// Unconditional moments [μ, σ², μ₃] used to center the conditional moments about the model mean.
        /// </param>
        /// <param name="uncertainData">The uncertain data observation with its measurement error distribution.</param>
        /// <param name="isLog10">If true, observations are transformed to log₁₀ space before computing moments.</param>
        /// <returns>
        /// Conditional moments [E[Y|dist], E[(Y−μ)²|dist], E[(Y−μ)³|dist]] suitable for passing
        /// to <see cref="UpdateMomentMeanCovariance"/>.
        /// </returns>
        /// <param name="measurementErrorSecondMoment">
        /// Optional matrix filled with the raw E_ME[gg'] for the uncertain row. This is used only by
        /// <see cref="MomentConditions"/> so the weighting matrix includes measurement-error spread.
        /// </param>
        /// <remarks>
        /// <para>
        /// The integration computes E[h(Y)] = ∫ h(y) f_ε(y) dy / P(a ≤ Y ≤ b), where f_ε is
        /// the measurement-error PDF. Unbounded supports are clipped at 1E-8 and 1 - 1E-8;
        /// the retained mass is computed from the ME CDF so finite-support and unbounded
        /// ME distributions share the same normalization convention.
        /// </para>
        /// <para>
        /// The optional second-moment matrix intentionally stores the raw E_ME[gg'] rather
        /// than a pre-centered covariance. The caller forms S = E[gg'] - gbar gbar' once at
        /// the end; pre-centering here would subtract the uncertain-row mean twice.
        /// </para>
        /// </remarks>
        private double[] ConditionalMomentsForUncertainData(double[] unconditionalMoments, UncertainData uncertainData, bool isLog10, Matrix? measurementErrorSecondMoment = null)
        {
            var dist = uncertainData.Distribution;
            double a = dist.InverseCDF(1E-8);
            double b = dist.InverseCDF(1 - 1E-8);
            double mass = dist.CDF(b) - dist.CDF(a);

            int q = unconditionalMoments.Length;
            if (a >= b) return new double[q];

            double mu = unconditionalMoments[0];
            double mu2 = q >= 2 ? unconditionalMoments[1] : 0.0;
            double mu3 = q >= 3 ? unconditionalMoments[2] : 0.0;
            var moments = new double[q];

            // E[Y | error_dist] normalized by retained ME probability mass.
            moments[0] = Integration.GaussLegendre20(x => (isLog10 ? Tools.Log10(x) : x) * dist.PDF(x), a, b) / mass;

            // E[(Y - μ)² | error_dist]
            if (q >= 2)
            {
                moments[1] = Integration.GaussLegendre20(x =>
                {
                    double v = isLog10 ? Tools.Log10(x) : x;
                    double d = v - mu;
                    return d * d * dist.PDF(x);
                }, a, b) / mass;
            }

            // E[(Y - μ)³ | error_dist]
            if (q >= 3)
            {
                moments[2] = Integration.GaussLegendre20(x =>
                {
                    double v = isLog10 ? Tools.Log10(x) : x;
                    double d = v - mu;
                    return d * d * d * dist.PDF(x);
                }, a, b) / mass;
            }

            if (measurementErrorSecondMoment is not null)
            {
                // For uncertain rows only, integrate E_ME[gg'] so the GMM S matrix carries
                // measurement-error variance. UpdateMomentMeanCovariance adds this term by
                // the law of total variance without replacing the hydrologic/process term.
                double MomentError(double x, int index)
                {
                    double v = isLog10 ? Tools.Log10(x) : x;
                    double d = v - mu;

                    if (index == 0) return d;
                    if (index == 1) return d * d - mu2;
                    return d * d * d - mu3;
                }

                for (int i = 0; i < q; i++)
                {
                    for (int j = i; j < q; j++)
                    {
                        double value = Integration.GaussLegendre20(x =>
                        {
                            double gi = MomentError(x, i);
                            double gj = MomentError(x, j);
                            return gi * gj * dist.PDF(x);
                        }, a, b) / mass;

                        measurementErrorSecondMoment[i, j] = value;
                        measurementErrorSecondMoment[j, i] = value;
                    }
                }
            }

            return moments;
        }

        /// <summary>
        /// Computes moment condition errors (g-vector) for an uncertain data observation by delegating
        /// to <see cref="ConditionalMomentsForUncertainData"/> and subtracting unconditional moments.
        /// </summary>
        /// <param name="unconditionalMoments">
        /// Unconditional moments [μ, σ², μ₃] against which the errors are computed.
        /// </param>
        /// <param name="uncertainData">The uncertain data observation with its measurement error distribution.</param>
        /// <param name="isLog10">If true, observations are transformed to log₁₀ space before computing moments.</param>
        /// <returns>
        /// The g-vector errors [E[Y|dist]−μ, E[(Y−μ)²|dist]−σ², E[(Y−μ)³|dist]−μ₃].
        /// Used by <see cref="CensoringAsymmetryScore"/> which needs errors directly.
        /// </returns>
        /// <remarks>
        /// This method delegates to <see cref="ConditionalMomentsForUncertainData"/> for the integration,
        /// ensuring the same 20-point Gauss-Legendre implementation is shared by both
        /// <see cref="MomentConditions"/> and <see cref="CensoringAsymmetryScore"/>.
        /// </remarks>
        private double[] MomentConditionsForUncertainData(double[] unconditionalMoments, UncertainData uncertainData, bool isLog10)
        {
            var moments = ConditionalMomentsForUncertainData(unconditionalMoments, uncertainData, isLog10);
            var errors = new double[moments.Length];
            for (int i = 0; i < moments.Length; i++)
                errors[i] = moments[i] - unconditionalMoments[i];
            return errors;
        }

        /// <summary>
        /// Accumulates running sums for the moment condition mean vector and second-moment matrix,
        /// contributing one observation (or group of censored observations) per call.
        /// </summary>
        /// <param name="c">
        /// Conditional central moments under event A (about μ):
        /// c[0] = E[Y|A], c[1] = E[(Y−μ)²|A], c[2] = E[(Y−μ)³|A] (if q ≥ 3).
        /// For exact rows, pass the observed values: [Y, c₂·(Y−μ)², c₃·(Y−μ)³] with Bessel corrections.
        /// </param>
        /// <param name="m">
        /// Unconditional (model) central moments:
        /// m[0] = μ, m[1] = σ², m[2] = μ₃ (if q ≥ 3).
        /// </param>
        /// <param name="mean">Running sum of g (NOT averaged yet). Averaged to ḡ = Σg/n in the caller.</param>
        /// <param name="covariance">Running sum of gg' (NOT averaged yet). Used to form S = E[gg'] − ḡḡ' in the caller.</param>
        /// <param name="w">Observation weight: 1.0 for individual observations, or count for grouped censored data.</param>
        /// <param name="isCensored">If true, forces the outer-product fallback for E[gg'|A] regardless of family support.</param>
        /// <param name="useModelCovariance">
        /// If true (and not censored), uses model higher central moments μ₄–μ₆ to compute the
        /// theoretically correct E[gg'] for supported distribution families (Normal, Gamma, Pearson III).
        /// Otherwise falls back to the outer-product approximation E[gg'|A] ≈ E[g|A]·E[g|A]'.
        /// </param>
        /// <param name="modelSigma">
        /// Model standard deviation σ, passed directly from the distribution.
        /// Required because for q &lt; 3 distributions (Exponential, Gamma),
        /// the moments array m[] does not contain μ₃, so σ and γ cannot
        /// be derived from m[] alone.
        /// </param>
        /// <param name="modelGamma">
        /// Model skewness coefficient γ, passed directly from the distribution.
        /// Used with modelSigma to compute μ₃ = γσ³ and higher central moments via
        /// <see cref="Mu456_Pearson3_FromSigmaGamma"/>.
        /// </param>
        /// <param name="measurementErrorSecondMoment">
        /// Optional raw E_ME[gg'] matrix for uncertain rows. When supplied, it is added as
        /// the measurement-error term in the law of total variance rather than replacing
        /// the hydrologic/process covariance contribution.
        /// </param>
        /// <remarks>
        /// <para>
        /// <b>Two accumulation paths:</b>
        /// </para>
        /// <para>
        /// <b>Model-based path (exact data, supported families):</b> Uses the distribution's
        /// higher central moments μ₄, μ₅, μ₆ to compute E[gg'] analytically. For the moment
        /// condition vector g = [(Y−μ), (Y−μ)²−σ², (Y−μ)³−μ₃], the second-moment matrix is:
        /// M₁₁ = μ₂, M₁₂ = μ₃, M₂₂ = μ₄−μ₂², M₁₃ = μ₄, M₂₃ = μ₅−μ₂μ₃, M₃₃ = μ₆−μ₃².
        /// This gives the optimal weighting matrix for efficient GMM.
        /// </para>
        /// <para>
        /// <b>Outer-product path (censored data or unsupported families):</b> Approximates
        /// E[gg'|A] ≈ E[g|A]⊗E[g|A]', producing a rank-1 contribution. This is the standard
        /// approach for censored observations in the GMM literature, since the unconditional
        /// μ₄–μ₆ formulas do not apply to truncated conditional distributions.
        /// </para>
        /// <para>
        /// <b>Measurement-error rows:</b> The uncertain-data integration supplies the raw
        /// E_ME[gg'] term. If the model-based path is used, that raw term is added directly.
        /// If the outer-product path is used, only E_ME[gg'] - E_ME[g]E_ME[g]' is added
        /// because the outer product has already contributed E_ME[g]E_ME[g]'. The caller's
        /// final centering step then forms S = E[gg'] - gbar gbar' exactly once.
        /// </para>
        /// </remarks>
        private void UpdateMomentMeanCovariance(
            double[] c, double[] m,
            Vector mean, Matrix covariance,
            double w,
            bool isCensored = false,
            bool useModelCovariance = true,
            double modelSigma = 0.0,
            double modelGamma = 0.0,
            Matrix? measurementErrorSecondMoment = null)
        {
            int q = mean.Length;

            // Unconditional low-order (about μ)
            double mu = m[0];
            double mu2 = m[1];
            double mu3 = (q >= 3 && m.Length >= 3) ? m[2] : 0.0;

            // Use model-provided sigma/gamma for higher central moments (μ₄–μ₆).
            // For q<3 distributions (Exponential, Gamma), the moments array m[]
            // doesn't include μ₃, so gamma cannot be derived from m[] alone.
            // The caller passes the model's StandardDeviation and Skewness directly.
            double sigma = modelSigma;
            double gamma = modelGamma;

            // "Conditional" pieces provided for this row (about μ for indices ≥1)
            double c1 = c[0] - mu;                     // E[(Y-μ)|A] or (Y-μ) for exact row
            double c2 = (c.Length >= 2) ? c[1] : 0.0;  // E[(Y-μ)^2|A]  or (Y-μ)^2
            double c3 = (q >= 3 && c.Length >= 3) ? c[2] : 0.0; // E[(Y-μ)^3|A] or (Y-μ)^3

            // E[g|A]
            double eg1 = c1;
            double eg2 = c2 - mu2;
            double eg3 = (q >= 3) ? (c3 - mu3) : 0.0;

            // --- accumulate sum_g (RAW) ---
            mean[0] += w * eg1;
            mean[1] += w * eg2;
            if (q >= 3) mean[2] += w * eg3;

            double ExpectedError(int index)
            {
                if (index == 0) return eg1;
                if (index == 1) return eg2;
                return eg3;
            }

            void AddMeasurementErrorSecondMoment(bool conditionalMeanOuterAlreadyIncluded)
            {
                if (measurementErrorSecondMoment is null)
                    return;

                for (int i = 0; i < q; i++)
                {
                    for (int j = 0; j < q; j++)
                    {
                        double value = measurementErrorSecondMoment[i, j];

                        // When the fallback path already added E_ME[g]E_ME[g]', subtract
                        // that piece here so the added term is only Var_ME(g). When the
                        // analytic model path is used, no conditional mean outer product
                        // has been added, so the raw E_ME[gg'] term belongs in the accumulator.
                        if (conditionalMeanOuterAlreadyIncluded)
                            value -= ExpectedError(i) * ExpectedError(j);

                        covariance[i, j] += w * value;
                    }
                }
            }

            // By default, use the outer-product approximation (works for censored or unsupported families)
            bool usedModelForThisRow = false;

            if (!isCensored && useModelCovariance)
            {
                // Try to use model μ4..μ6 to get the proper "inflated" variance for EXACT rows.
                double mu4 = 0.0, mu5 = 0.0, mu6 = 0.0;

                switch (DistributionType)
                {
                    case UnivariateDistributionType.Exponential:
                    case UnivariateDistributionType.GammaDistribution:
                    case UnivariateDistributionType.PearsonTypeIII:
                    case UnivariateDistributionType.LogPearsonTypeIII:
                        // Pearson Type III / Gamma family uses (σ, γ). Exponential is γ=2 special case.
                        Mu456_Pearson3_FromSigmaGamma(sigma, gamma, out mu4, out mu5, out mu6);
                        usedModelForThisRow = true;
                        break;

                    // If you want to extend to other families, add cases here, e.g.:
                    case UnivariateDistributionType.Normal:
                    case UnivariateDistributionType.LogNormal:
                        // μ4 = 3σ^4, μ5 = 0, μ6 = 15σ^6
                        {
                            double s2 = mu2;
                            double s4 = s2 * s2;
                            double s6 = s4 * s2;
                            mu4 = 3.0 * s4;
                            mu5 = 0.0;
                            mu6 = 15.0 * s6;
                            usedModelForThisRow = true;
                        }
                        break;

                    default:
                        usedModelForThisRow = false; // fall back to outer product
                        break;
                }

                if (usedModelForThisRow)
                {
                    // Per-row E[ggᵀ] using model central moments (unconditional)
                    // g = [ g1,             g2,                  g3              ]
                    //   = [ (Y-μ),          (Y-μ)^2 - μ2,        (Y-μ)^3 - μ3    ]
                    //
                    // Use model μ₃ = γσ³ instead of mu3 from m[2], which is 0 for
                    // q<3 distributions (Exponential, Gamma) since m[] has only q entries.
                    double modelMu3 = gamma * sigma * sigma * sigma;

                    double M11 = mu2;
                    double M12 = modelMu3;
                    double M22 = mu4 - mu2 * mu2;

                    double M13 = 0.0, M23 = 0.0, M33 = 0.0;
                    if (q >= 3)
                    {
                        M13 = mu4;                         // E[(Y-μ)·((Y-μ)³-μ₃)] = μ₄
                        M23 = mu5 - mu2 * modelMu3;        // E[((Y-μ)²-μ₂)·((Y-μ)³-μ₃)] = μ₅ - μ₂μ₃
                        M33 = mu6 - modelMu3 * modelMu3;   // E[((Y-μ)³-μ₃)²] = μ₆ - μ₃²
                    }

                    covariance[0, 0] += w * M11;
                    covariance[0, 1] += w * M12; covariance[1, 0] += w * M12;
                    covariance[1, 1] += w * M22;

                    if (q >= 3)
                    {
                        covariance[0, 2] += w * M13; covariance[2, 0] += w * M13;
                        covariance[1, 2] += w * M23; covariance[2, 1] += w * M23;
                        covariance[2, 2] += w * M33;
                    }

                    AddMeasurementErrorSecondMoment(false);
                    return; // IMPORTANT: we've completed the model-based path
                }
            }

            // --- Outer-product fallback (censored rows or unsupported families) ---
            {
                double g1 = eg1, g2 = eg2, g3 = eg3;

                double M11 = g1 * g1;
                double M12 = g1 * g2;
                double M22 = g2 * g2;

                double M13 = 0.0, M23 = 0.0, M33 = 0.0;
                if (q >= 3)
                {
                    M13 = g1 * g3;
                    M23 = g2 * g3;
                    M33 = g3 * g3;
                }

                covariance[0, 0] += w * M11;
                covariance[0, 1] += w * M12; covariance[1, 0] += w * M12;
                covariance[1, 1] += w * M22;

                if (q >= 3)
                {
                    covariance[0, 2] += w * M13; covariance[2, 0] += w * M13;
                    covariance[1, 2] += w * M23; covariance[2, 1] += w * M23;
                    covariance[2, 2] += w * M33;
                }

                AddMeasurementErrorSecondMoment(true);
            }
        }


        /// <summary>
        /// Computes a directional censoring asymmetry score S ∈ [−1, 1] for each moment condition,
        /// measuring whether censoring pulls the sample toward higher or lower quantiles.
        /// </summary>
        /// <param name="parameters">Parameter values in link space.</param>
        /// <returns>
        /// An array of q scores where:
        /// S ≈ 0 indicates symmetric censoring,
        /// S &gt; 0 indicates right-tail pull (high values censored more),
        /// S &lt; 0 indicates left-tail pull (low values censored more).
        /// Returns NaN-filled array if parameters are invalid.
        /// </returns>
        /// <remarks>
        /// <para>
        /// For each observation, the moment condition error g is decomposed into positive (right-tail)
        /// and negative (left-tail) contributions, weighted by the observation count. The score is
        /// computed as S = (posPull − negPull) / (posPull + negPull + ε).
        /// </para>
        /// <para>
        /// This diagnostic helps detect when heavy censoring biases the GMM estimator in one direction,
        /// which can indicate the need for adjusted starting values or a different estimation strategy.
        /// </para>
        /// </remarks>
        public double[] CensoringAsymmetryScore(double[] parameters)
        {
            parameters = _linkController.InverseLink(parameters);

            int q = NumberOfParameters;

            var posPull = new double[q]; // right-tail pull
            var negPull = new double[q]; // left-tail pull
            var score = new double[q];

            // Bessel small-sample correction factors for non-outlier exact data
            int Ns = 0;
            foreach (ExactData data in DataFrame.ExactSeries)
                if (!data.IsLowOutlier) Ns++;
            double c2 = Ns >= 2 ? Ns / (double)(Ns - 1) : 1.0;
            double c3 = Ns >= 3 ? (double)(Ns * Ns) / ((Ns - 1) * (Ns - 2)) : 1.0;

            // Configure distribution model and log-space flag
            UnivariateDistributionBase model;
            bool isLog10 = false;

            if (DistributionType == UnivariateDistributionType.LogPearsonTypeIII)
            {
                model = CreateDistribution(UnivariateDistributionType.PearsonTypeIII);
                isLog10 = true;
            }
            else if (DistributionType == UnivariateDistributionType.LogNormal)
            {
                model = CreateDistribution(UnivariateDistributionType.Normal);
                isLog10 = true;
            }
            else
            {
                model = Distribution.Clone();
            }

            // Validate parameters without throwing — called frequently during optimization
            var validation = model.ValidateParameters(parameters, false);
            if (validation is not null)
            {
                score.Fill(double.NaN);
                return score;
            }
            model.SetParameters(parameters);

            // Integration bounds from distribution support
            double min = model.InverseCDF(Tools.DoubleMachineEpsilon);
            double max = model.InverseCDF(1 - Tools.DoubleMachineEpsilon);

            // Unconditional central moments of the fitted distribution
            double mu = model.Mean;
            double sigma = model.StandardDeviation;
            double sigma2 = model.Variance;
            double skewness = model.Skewness;

            // Guard against non-finite moments
            if (!Tools.IsFinite(mu) || !Tools.IsFinite(sigma2) || sigma <= 0)
            {
                score.Fill(double.NaN);
                return score;
            }

            // Store unconditional moments: [μ, σ², μ₃]
            var unconditionalMoments = new double[q];
            unconditionalMoments[0] = mu;
            if (q >= 2) unconditionalMoments[1] = sigma2;
            if (q >= 3) unconditionalMoments[2] = skewness * sigma * sigma * sigma;

            // Reusable buffers
            var errors = new double[q];
            var conditionalMoments = new double[q];

            // Helper: accumulate pull for a given observation weight
            void AccumulatePull(double[] errs, double weight)
            {
                for (int j = 0; j < q; j++)
                {
                    if (errs[j] >= 0)
                        negPull[j] += weight * Math.Abs(errs[j]);
                    else
                        posPull[j] += weight * Math.Abs(errs[j]);
                }
            }

            // Low outlier moments — left-censored below the threshold
            if (DataFrame.NumberOfLowOutliers > 0)
            {
                var lower = min;
                var upper = Math.Min(max, isLog10 ? Tools.Log10(DataFrame.LowOutlierThreshold) : DataFrame.LowOutlierThreshold);
                if (lower >= upper)
                    conditionalMoments.Fill(0);
                else
                    conditionalMoments = model.ConditionalMoments(lower, upper);
                for (int j = 0; j < q; j++)
                    errors[j] = conditionalMoments[j] - unconditionalMoments[j];
                AccumulatePull(errors, DataFrame.NumberOfLowOutliers);
            }

            // Exact data — non-outlier observed values with Bessel corrections
            foreach (ExactData data in DataFrame.ExactSeries)
            {
                if (!data.IsLowOutlier)
                {
                    double value = isLog10 ? data.Log10Value : data.Value;
                    double d = value - unconditionalMoments[0];
                    double d2 = d * d;
                    conditionalMoments[0] = value;
                    if (q >= 2) conditionalMoments[1] = c2 * d2;
                    if (q >= 3) conditionalMoments[2] = c3 * d2 * d;
                    for (int j = 0; j < q; j++)
                        errors[j] = conditionalMoments[j] - unconditionalMoments[j];
                    AccumulatePull(errors, 1);
                }
            }

            // Uncertain data — integrate over measurement error distribution
            foreach (UncertainData data in DataFrame.UncertainSeries)
            {
                var uncertainErrors = MomentConditionsForUncertainData(unconditionalMoments, data, isLog10);
                AccumulatePull(uncertainErrors, 1);
            }

            // Interval data — conditional moments over [L, U]
            foreach (IntervalData data in DataFrame.IntervalSeries)
            {
                var lower = Math.Max(min, isLog10 ? data.Log10LowerValue : data.LowerValue);
                var upper = Math.Min(max, isLog10 ? data.Log10UpperValue : data.UpperValue);
                if (lower >= upper)
                    conditionalMoments.Fill(0);
                else
                    conditionalMoments = model.ConditionalMoments(lower, upper);
                for (int j = 0; j < q; j++)
                    errors[j] = conditionalMoments[j] - unconditionalMoments[j];
                AccumulatePull(errors, 1);
            }

            // Threshold data — left-censored and right-censored
            foreach (ThresholdData data in DataFrame.ThresholdSeries)
            {
                // Left censored
                if (data.NumberBelow > 0)
                {
                    var lower = min;
                    var upper = Math.Min(max, isLog10 ? data.Log10Value : data.Value);
                    if (lower >= upper)
                        conditionalMoments.Fill(0);
                    else
                        conditionalMoments = model.ConditionalMoments(lower, upper);
                    for (int j = 0; j < q; j++)
                        errors[j] = conditionalMoments[j] - unconditionalMoments[j];
                    AccumulatePull(errors, data.NumberBelow);
                }
                // Right censored
                if (data.NumberAbove > 0)
                {
                    var lower = Math.Min(max, isLog10 ? data.Log10Value : data.Value);
                    var upper = max;
                    if (lower >= upper)
                        conditionalMoments.Fill(0);
                    else
                        conditionalMoments = model.ConditionalMoments(lower, upper);
                    for (int j = 0; j < q; j++)
                        errors[j] = conditionalMoments[j] - unconditionalMoments[j];
                    AccumulatePull(errors, data.NumberAbove);
                }
            }

            for (int j = 0; j < q; j++)
            {
                double denom = posPull[j] + negPull[j];
                score[j] = (denom > 0.0) ? (posPull[j] - negPull[j]) / (denom + 1e-12) : 0.0;
            }

            return score;
        }

        /// <summary>
        /// Computes a weighted error direction score (WEDS) for each parameter, measuring the
        /// fractional imbalance between observations producing positive vs negative moment condition errors.
        /// </summary>
        /// <param name="parameters">Parameter values in natural distribution space (for example, [mu, sigma, gamma]).</param>
        /// <returns>
        /// A q-length array with scores in [-1, +1]:
        /// S &gt; 0 indicates more weighted data above the model expectation (right-tail dominance),
        /// S &lt; 0 indicates more weighted data below the model expectation (left-tail dominance),
        /// S ≈ 0 indicates balanced data.
        /// Returns NaN-filled array if parameters are invalid.
        /// </returns>
        /// <remarks>
        /// <para>
        /// WEDS is intentionally evaluated in the natural parameterization, not link space. It is
        /// a diagnostic of the fitted distribution, data, and censoring pattern; it must remain
        /// invariant to later uncertainty-link choices. This keeps the statistic publication-facing:
        /// the direction score is tied to moment-condition residual signs, not to an arbitrary
        /// transformation used to simulate uncertainty draws.
        /// </para>
        /// <para>
        /// Unlike <see cref="CensoringAsymmetryScore"/> which decomposes error magnitudes (and is
        /// constrained to zero at the GMM solution by ḡ = 0), WEDS counts the weighted fraction of
        /// observations on each side. The count decomposition is NOT constrained by ḡ = 0, so WEDS
        /// is nonzero whenever the data or censoring structure is directionally asymmetric — even
        /// without penalties and with perfect GMM convergence.
        /// </para>
        /// <para>
        /// For the location parameter (index 0), WEDS measures what fraction of the weighted sample
        /// falls above vs below the fitted mean. Left censoring (low outliers) produces WEDS &lt; 0;
        /// right censoring (historical floods) produces WEDS &gt; 0. For the Exponential distribution,
        /// WEDS is naturally negative (~−0.26) because P(X &lt; μ) = 1 − 1/e ≈ 0.63.
        /// </para>
        /// </remarks>
        public double[] WeightedErrorDirectionScore(double[] parameters)
        {
            int q = NumberOfParameters;

            var wPos = new double[q]; // weighted count of positive errors (obs above model expectation)
            var wNeg = new double[q]; // weighted count of negative errors (obs below model expectation)
            var score = new double[q];

            // Bessel small-sample correction factors for non-outlier exact data
            int Ns = 0;
            foreach (ExactData data in DataFrame.ExactSeries)
                if (!data.IsLowOutlier) Ns++;
            double c2 = Ns >= 2 ? Ns / (double)(Ns - 1) : 1.0;
            double c3 = Ns >= 3 ? (double)(Ns * Ns) / ((Ns - 1) * (Ns - 2)) : 1.0;

            // Configure distribution model and log-space flag
            UnivariateDistributionBase model;
            bool isLog10 = false;

            if (DistributionType == UnivariateDistributionType.LogPearsonTypeIII)
            {
                model = CreateDistribution(UnivariateDistributionType.PearsonTypeIII);
                isLog10 = true;
            }
            else if (DistributionType == UnivariateDistributionType.LogNormal)
            {
                model = CreateDistribution(UnivariateDistributionType.Normal);
                isLog10 = true;
            }
            else
            {
                model = Distribution.Clone();
            }

            // Validate parameters without throwing
            var validation = model.ValidateParameters(parameters, false);
            if (validation is not null)
            {
                score.Fill(double.NaN);
                return score;
            }
            model.SetParameters(parameters);

            // Integration bounds from distribution support
            double min = model.InverseCDF(Tools.DoubleMachineEpsilon);
            double max = model.InverseCDF(1 - Tools.DoubleMachineEpsilon);

            // Unconditional central moments of the fitted distribution
            double mu = model.Mean;
            double sigma = model.StandardDeviation;
            double sigma2 = model.Variance;
            double skewness = model.Skewness;

            // Guard against non-finite moments
            if (!Tools.IsFinite(mu) || !Tools.IsFinite(sigma2) || sigma <= 0)
            {
                score.Fill(double.NaN);
                return score;
            }

            // Store unconditional moments: [μ, σ², μ₃]
            var unconditionalMoments = new double[q];
            unconditionalMoments[0] = mu;
            if (q >= 2) unconditionalMoments[1] = sigma2;
            if (q >= 3) unconditionalMoments[2] = skewness * sigma * sigma * sigma;

            // Reusable buffers
            var errors = new double[q];
            var conditionalMoments = new double[q];

            // Helper: accumulate weighted direction counts
            void AccumulateDirection(double[] errs, double weight)
            {
                for (int j = 0; j < q; j++)
                {
                    if (errs[j] > 0)
                        wPos[j] += weight;
                    else if (errs[j] < 0)
                        wNeg[j] += weight;
                }
            }

            // Low outlier moments — left-censored below the threshold
            if (DataFrame.NumberOfLowOutliers > 0)
            {
                var lower = min;
                var upper = Math.Min(max, isLog10 ? Tools.Log10(DataFrame.LowOutlierThreshold) : DataFrame.LowOutlierThreshold);
                if (lower >= upper)
                    conditionalMoments.Fill(0);
                else
                    conditionalMoments = model.ConditionalMoments(lower, upper);
                for (int j = 0; j < q; j++)
                    errors[j] = conditionalMoments[j] - unconditionalMoments[j];
                AccumulateDirection(errors, DataFrame.NumberOfLowOutliers);
            }

            // Exact data — non-outlier observed values with Bessel corrections
            foreach (ExactData data in DataFrame.ExactSeries)
            {
                if (!data.IsLowOutlier)
                {
                    double value = isLog10 ? data.Log10Value : data.Value;
                    double d = value - unconditionalMoments[0];
                    double d2 = d * d;
                    conditionalMoments[0] = value;
                    if (q >= 2) conditionalMoments[1] = c2 * d2;
                    if (q >= 3) conditionalMoments[2] = c3 * d2 * d;
                    for (int j = 0; j < q; j++)
                        errors[j] = conditionalMoments[j] - unconditionalMoments[j];
                    AccumulateDirection(errors, 1);
                }
            }

            // Uncertain data — integrate over measurement error distribution
            foreach (UncertainData data in DataFrame.UncertainSeries)
            {
                var uncertainErrors = MomentConditionsForUncertainData(unconditionalMoments, data, isLog10);
                AccumulateDirection(uncertainErrors, 1);
            }

            // Interval data — conditional moments over [L, U]
            foreach (IntervalData data in DataFrame.IntervalSeries)
            {
                var lower = Math.Max(min, isLog10 ? data.Log10LowerValue : data.LowerValue);
                var upper = Math.Min(max, isLog10 ? data.Log10UpperValue : data.UpperValue);
                if (lower >= upper)
                    conditionalMoments.Fill(0);
                else
                    conditionalMoments = model.ConditionalMoments(lower, upper);
                for (int j = 0; j < q; j++)
                    errors[j] = conditionalMoments[j] - unconditionalMoments[j];
                AccumulateDirection(errors, 1);
            }

            // Threshold data — left-censored and right-censored
            foreach (ThresholdData data in DataFrame.ThresholdSeries)
            {
                // Left censored
                if (data.NumberBelow > 0)
                {
                    var lower = min;
                    var upper = Math.Min(max, isLog10 ? data.Log10Value : data.Value);
                    if (lower >= upper)
                        conditionalMoments.Fill(0);
                    else
                        conditionalMoments = model.ConditionalMoments(lower, upper);
                    for (int j = 0; j < q; j++)
                        errors[j] = conditionalMoments[j] - unconditionalMoments[j];
                    AccumulateDirection(errors, data.NumberBelow);
                }
                // Right censored
                if (data.NumberAbove > 0)
                {
                    var lower = Math.Min(max, isLog10 ? data.Log10Value : data.Value);
                    var upper = max;
                    if (lower >= upper)
                        conditionalMoments.Fill(0);
                    else
                        conditionalMoments = model.ConditionalMoments(lower, upper);
                    for (int j = 0; j < q; j++)
                        errors[j] = conditionalMoments[j] - unconditionalMoments[j];
                    AccumulateDirection(errors, data.NumberAbove);
                }
            }

            for (int j = 0; j < q; j++)
            {
                double total = wPos[j] + wNeg[j];
                score[j] = total > 0 ? (wPos[j] - wNeg[j]) / total : 0.0;
            }

            return score;
        }

        /// <summary>
        /// Computes a weighted error direction score from link-space parameters by first mapping
        /// them back to natural distribution space with the current <see cref="LinkController"/>.
        /// </summary>
        /// <param name="linkedParameters">Parameter values in link space.</param>
        /// <returns>
        /// A q-length array with scores in [-1, +1], computed after inverse-linking
        /// <paramref name="linkedParameters"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="linkedParameters"/> is null.</exception>
        /// <remarks>
        /// This method exists to make the parameter-space contract explicit. Production B17C
        /// link selection should call <see cref="WeightedErrorDirectionScore(double[])"/> on
        /// natural parameters so WEDS remains independent of the temporary uncertainty links.
        /// </remarks>
        public double[] WeightedErrorDirectionScoreFromLinked(double[] linkedParameters)
        {
            if (linkedParameters == null) throw new ArgumentNullException(nameof(linkedParameters));

            return WeightedErrorDirectionScore(_linkController.InverseLink(linkedParameters));
        }

        /// <summary>
        /// Computes per-observation moment condition g-vectors for all observations in the data frame,
        /// returning an [n × q] matrix where row i contains the g-vector for observation i.
        /// </summary>
        /// <param name="parameters">Parameter values in link space (transformed by the link controller).</param>
        /// <returns>
        /// A double[n, q] matrix where n = <see cref="DataFrame.TotalRecordLength()"/> and q = <see cref="NumberOfParameters"/>.
        /// Each row contains [g₁, g₂, g₃] = [c₁−μ, c₂−σ², c₃−μ₃] for the corresponding observation.
        /// Returns a zero-filled matrix if parameters are invalid.
        /// </returns>
        /// <remarks>
        /// <para>
        /// <b>Row ordering:</b> Low outliers (NumberOfLowOutliers identical rows) → Exact data (1 row per non-outlier)
        /// → Uncertain data (1 row per observation) → Interval data (1 row per observation)
        /// → Threshold data (NumberBelow + NumberAbove rows per record).
        /// </para>
        /// <para>
        /// <b>Invariant:</b> The column-wise mean of the returned matrix must equal the G vector
        /// from <see cref="MomentConditions"/>: (1/n) Σᵢ result[i, j] = G[j].
        /// </para>
        /// <para>
        /// <b>Consumers:</b> This matrix is used by <see cref="GeneralizedMethodOfMoments.GetObservationInfluence"/>,
        /// <see cref="GeneralizedMethodOfMoments.GetCooksDistance"/>, and
        /// <see cref="GeneralizedMethodOfMoments.GetInfluenceDiagnostics"/> to compute
        /// per-observation influence measures for GMM diagnostics.
        /// </para>
        /// <para>
        /// The setup (parameter transformation, model configuration, Bessel corrections, bounds, and
        /// unconditional moments) mirrors <see cref="MomentConditions"/> exactly to ensure consistency.
        /// </para>
        /// </remarks>
        private double[,] PointwiseMomentConditionsImpl(double[] parameters)
        {
            parameters = _linkController.InverseLink(parameters);

            int q = NumberOfParameters;
            int n = DataFrame.TotalRecordLength();
            var result = new double[n, q];

            // Bessel small-sample correction factors for non-outlier exact data
            int Ns = 0;
            foreach (ExactData data in DataFrame.ExactSeries)
                if (!data.IsLowOutlier) Ns++;
            double c2 = Ns >= 2 ? Ns / (double)(Ns - 1) : 1.0;
            double c3 = Ns >= 3 ? (double)(Ns * Ns) / ((Ns - 1) * (Ns - 2)) : 1.0;

            // Configure distribution model and log-space flag
            UnivariateDistributionBase model;
            bool isLog10 = false;

            if (DistributionType == UnivariateDistributionType.LogPearsonTypeIII)
            {
                model = CreateDistribution(UnivariateDistributionType.PearsonTypeIII);
                isLog10 = true;
            }
            else if (DistributionType == UnivariateDistributionType.LogNormal)
            {
                model = CreateDistribution(UnivariateDistributionType.Normal);
                isLog10 = true;
            }
            else
            {
                model = Distribution.Clone();
            }

            // Validate parameters without throwing — called frequently during optimization
            var validation = model.ValidateParameters(parameters, false);
            if (validation is not null)
            {
                return result;
            }
            model.SetParameters(parameters);

            // Integration bounds from distribution support
            double min = model.InverseCDF(Tools.DoubleMachineEpsilon);
            double max = model.InverseCDF(1 - Tools.DoubleMachineEpsilon);

            // Unconditional central moments of the fitted distribution
            double mu = model.Mean;
            double sigma = model.StandardDeviation;
            double sigma2 = model.Variance;
            double skewness = model.Skewness;

            // Guard against non-finite moments from extreme parameter values
            if (!Tools.IsFinite(mu) || !Tools.IsFinite(sigma2) || sigma <= 0)
            {
                return result;
            }

            // Store unconditional moments: [μ, σ², μ₃]
            var unconditionalMoments = new double[q];
            unconditionalMoments[0] = mu;
            if (q >= 2) unconditionalMoments[1] = sigma2;
            if (q >= 3) unconditionalMoments[2] = skewness * sigma * sigma * sigma;

            // Reusable buffer for conditional moments
            var conditionalMoments = new double[q];

            // Current row index into the result matrix
            int row = 0;

            // Helper: store g-vector (c[j] - m[j]) into result[row..row+count-1, :]
            void StoreG(double[] c, double[] m, int count)
            {
                double g0 = c[0] - m[0];
                double g1 = (c.Length >= 2) ? c[1] - m[1] : 0.0;
                double g2 = (q >= 3 && c.Length >= 3) ? c[2] - m[2] : 0.0;
                for (int r = 0; r < count; r++)
                {
                    result[row, 0] = g0;
                    if (q >= 2) result[row, 1] = g1;
                    if (q >= 3) result[row, 2] = g2;
                    row++;
                }
            }

            // Low outlier moments — treated as left-censored below the low outlier threshold
            if (DataFrame.NumberOfLowOutliers > 0)
            {
                var lower = min;
                var upper = Math.Min(max, isLog10 ? Tools.Log10(DataFrame.LowOutlierThreshold) : DataFrame.LowOutlierThreshold);
                if (lower >= upper)
                    conditionalMoments.Fill(0);
                else
                    conditionalMoments = model.ConditionalMoments(lower, upper);
                StoreG(conditionalMoments, unconditionalMoments, DataFrame.NumberOfLowOutliers);
            }

            // Exact data — non-outlier observed values with Bessel corrections
            foreach (ExactData data in DataFrame.ExactSeries)
            {
                if (!data.IsLowOutlier)
                {
                    double value = isLog10 ? data.Log10Value : data.Value;
                    double d = value - unconditionalMoments[0];
                    double d2 = d * d;
                    conditionalMoments[0] = value;
                    if (q >= 2) conditionalMoments[1] = c2 * d2;
                    if (q >= 3) conditionalMoments[2] = c3 * d2 * d;
                    StoreG(conditionalMoments, unconditionalMoments, 1);
                }
            }

            // Uncertain data — integrate over measurement error distribution
            foreach (UncertainData data in DataFrame.UncertainSeries)
            {
                var uncertainMoments = ConditionalMomentsForUncertainData(unconditionalMoments, data, isLog10);
                StoreG(uncertainMoments, unconditionalMoments, 1);
            }

            // Interval data — conditional moments over [L, U]
            foreach (IntervalData data in DataFrame.IntervalSeries)
            {
                var lower = Math.Max(min, isLog10 ? data.Log10LowerValue : data.LowerValue);
                var upper = Math.Min(max, isLog10 ? data.Log10UpperValue : data.UpperValue);
                if (lower >= upper)
                    conditionalMoments.Fill(0);
                else
                    conditionalMoments = model.ConditionalMoments(lower, upper);
                StoreG(conditionalMoments, unconditionalMoments, 1);
            }

            // Threshold data — left-censored (Y < threshold) and right-censored (Y >= threshold)
            foreach (ThresholdData data in DataFrame.ThresholdSeries)
            {
                // Left censored
                if (data.NumberBelow > 0)
                {
                    var lower = min;
                    var upper = Math.Min(max, isLog10 ? data.Log10Value : data.Value);
                    if (lower >= upper)
                        conditionalMoments.Fill(0);
                    else
                        conditionalMoments = model.ConditionalMoments(lower, upper);
                    StoreG(conditionalMoments, unconditionalMoments, data.NumberBelow);
                }
                // Right censored
                if (data.NumberAbove > 0)
                {
                    var lower = Math.Min(max, isLog10 ? data.Log10Value : data.Value);
                    var upper = max;
                    if (lower >= upper)
                        conditionalMoments.Fill(0);
                    else
                        conditionalMoments = model.ConditionalMoments(lower, upper);
                    StoreG(conditionalMoments, unconditionalMoments, data.NumberAbove);
                }
            }

            return result;
        }

        /// <summary>
        /// Computes the gradient of the quantile function with respect to the distribution parameters.
        /// </summary>
        /// <param name="probability">The non-exceedance probability, in the open interval (0, 1).</param>
        /// <param name="parameters">The distribution parameter values (moment parameters for B17C).</param>
        /// <returns>
        /// The gradient vector ∂F⁻¹(p)/∂θ, where each element is the partial derivative
        /// of the quantile with respect to the corresponding parameter.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="parameters"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if <paramref name="probability"/> is not in the open interval (0, 1).
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="parameters"/> length does not match <see cref="NumberOfParameters"/>.
        /// </exception>
        /// <remarks>
        /// <para>
        /// For log-transformed distributions (LogPearsonTypeIII, LogNormal), the gradient is
        /// computed on the base distribution (PearsonTypeIII, Normal) in log-space. The Pearson
        /// family uses <c>QuantileGradientForMoments</c> (gradient w.r.t. moment parameters);
        /// other distributions use <c>IStandardError.QuantileGradient</c>.
        /// </para>
        /// </remarks>
        public double[] QuantileGradient(double probability, double[] parameters)
        {
            if (parameters == null) throw new ArgumentNullException(nameof(parameters));
            if (probability <= 0.0 || probability >= 1.0)
                throw new ArgumentOutOfRangeException(nameof(probability), probability, "Probability must be between 0 and 1 exclusive.");

            int p = NumberOfParameters;
            if (parameters.Length != p)
                throw new ArgumentException($"Expected {p} parameters but received {parameters.Length}.", nameof(parameters));

            // Create the base distribution model. Log-transformed distributions (LP3, LogNormal)
            // use their base distribution (PT3, Normal) since parameters are in log-space.
            UnivariateDistributionBase model;
            if (DistributionType == UnivariateDistributionType.LogPearsonTypeIII)
            {
                model = CreateDistribution(UnivariateDistributionType.PearsonTypeIII);
            }
            else if (DistributionType == UnivariateDistributionType.LogNormal)
            {
                model = CreateDistribution(UnivariateDistributionType.Normal);
            }
            else
            {
                model = Distribution.Clone();
            }

            // Validate and set model parameters
            var validation = model.ValidateParameters(parameters, false);
            if (validation is not null)
                throw new ArgumentException("Invalid parameters for quantile gradient computation.");
            model.SetParameters(parameters);

            // Get quantile gradient ∂F⁻¹(p)/∂θ
            if (DistributionType == UnivariateDistributionType.PearsonTypeIII ||
                DistributionType == UnivariateDistributionType.LogPearsonTypeIII)
            {
                return ((PearsonTypeIII)model).QuantileGradientForMoments(probability);
            }
            else
            {
                return ((IStandardError)model).QuantileGradient(probability);
            }
        }

        /// <summary>
        /// Computes the variance of a quantile estimate using the delta method.
        /// </summary>
        /// <param name="probability">The non-exceedance probability, strictly between 0 and 1.</param>
        /// <param name="parameters">
        /// The distribution parameter values (moment-space: e.g., μ, σ, γ for Pearson family;
        /// μ, σ for Normal/LogNormal). Length must equal <see cref="NumberOfParameters"/>.
        /// </param>
        /// <param name="covarianceMatrix">
        /// The p × p covariance matrix of the parameter estimates, where p = <see cref="NumberOfParameters"/>.
        /// Typically obtained from <see cref="GeneralizedMethodOfMoments.GetCovarianceMatrix"/>.
        /// </param>
        /// <returns>
        /// The estimated variance of the quantile at the specified probability,
        /// computed as Var(Q_p) = g' Σ g where g is the quantile gradient vector.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="covarianceMatrix"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="covarianceMatrix"/> dimensions do not match <see cref="NumberOfParameters"/>.
        /// </exception>
        /// <remarks>
        /// <para>
        /// Uses the delta method to propagate parameter uncertainty to the quantile function:
        /// Var(Q_p) = ∇Q_p' · Σ · ∇Q_p. Delegates to <see cref="QuantileGradient(double, double[])"/>
        /// for the gradient computation, then forms the quadratic form g' Σ g.
        /// </para>
        /// </remarks>
        public double QuantileVariance(double probability, double[] parameters, double[,] covarianceMatrix)
        {
            if (covarianceMatrix == null) throw new ArgumentNullException(nameof(covarianceMatrix));

            int p = NumberOfParameters;
            if (covarianceMatrix.GetLength(0) != p || covarianceMatrix.GetLength(1) != p)
                throw new ArgumentException($"Covariance matrix must be {p} × {p} but received {covarianceMatrix.GetLength(0)} × {covarianceMatrix.GetLength(1)}.", nameof(covarianceMatrix));

            // Get the quantile gradient vector
            var gradient = QuantileGradient(probability, parameters);

            // Compute quadratic form: Var(Q_p) = g' Σ g
            double qVar = 0.0;
            for (int i = 0; i < p; i++)
            {
                for (int j = 0; j < p; j++)
                {
                    qVar += gradient[i] * gradient[j] * covarianceMatrix[i, j];
                }
            }

            return qVar;
        }

        /// <inheritdoc/>
        public IGMMModel Clone()
        {
            // Round-trip through XElement to avoid the public constructor's
            // SetDefaultParameters() call, which would overwrite user-defined penalties.
            // The XElement constructor sets _isDeserializing = true to suppress this.
            return DataFrame != null
                ? new Bulletin17CDistribution(DataFrame, ToXElement())
                : new Bulletin17CDistribution(ToXElement());
        }

        /// <inheritdoc/>
        public XElement ToXElement()
        {
            var result = new XElement(nameof(Bulletin17CDistribution));

            // Distribution
            result.Add(Distribution.ToXElement());

            // Parameters
            var parms = new XElement(nameof(Parameters));
            foreach (var p in _parameters)
                parms.Add(p.ToXElement());
            result.Add(parms);

            // Parameter penalties
            var paramPens = new XElement(nameof(ParameterPenalties));
            foreach (var pen in _parameterPenalties)
                paramPens.Add(pen.ToXElement());
            result.Add(paramPens);

            // Quantile penalties
            var quantPens = new XElement(nameof(QuantilePenalties));
            foreach (var pen in _quantilePenalties)
                quantPens.Add(pen.ToXElement());
            result.Add(quantPens);

            // Link controller
            result.Add(_linkController.ToXElement());

            return result;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// <para>
        ///     Validates all components of the Bulletin 17C model:
        ///     <list type="bullet">
        ///     <item><description>DataFrame: must be non-null and internally valid.</description></item>
        ///     <item><description>Distribution: must be non-null and a supported type.</description></item>
        ///     <item><description>Log-distribution data: non-positive values are invalid for LogNormal and LogPearsonTypeIII.</description></item>
        ///     <item><description>Parameters: each <see cref="ModelParameter"/> is validated (bounds, value, prior).</description></item>
        ///     <item><description>Parameter penalties: each enabled <see cref="ParameterPenalty"/> is validated.</description></item>
        ///     <item><description>Quantile penalties: each enabled <see cref="QuantilePenalty"/> is validated, and cross-validated for AEP ordering.</description></item>
        ///     </list>
        /// </para>
        /// </remarks>
        public (bool IsValid, List<string> ValidationMessages) Validate()
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
            if (Distribution is not null && !IsSupportedDistributionType(Distribution.Type))
            {
                isValid = false;
                messages.Add($"Error: Distribution type '{Distribution.Type}' is not supported by the Bulletin 17C model. " +
                    "Supported types: Exponential, Gamma, Log-Normal, Log-Pearson Type III, Normal, Pearson Type III.");
            }

            // Validate retained ME quantile bounds directly. This guard uses 1E-8 to avoid
            // InverseCDF(0/1) infinities and matches the B17C ME moment integration window.
            if (DataFrame.UncertainSeries is not null)
            {
                foreach (UncertainData data in DataFrame.UncertainSeries)
                {
                    var dist = data.Distribution;
                    double lowerProbability = dist.Minimum == double.NegativeInfinity ? 1E-8 : 0.0;
                    double upperProbability = dist.Maximum == double.PositiveInfinity ? 1.0 - 1E-8 : 1.0;
                    double lower = lowerProbability > 0.0 ? dist.InverseCDF(lowerProbability) : dist.Minimum;
                    double upper = upperProbability < 1.0 ? dist.InverseCDF(upperProbability) : dist.Maximum;
                    double mass = upperProbability - lowerProbability;

                    if (!Tools.IsFinite(lower) || !Tools.IsFinite(upper) || !Tools.IsFinite(mass) ||
                        mass <= 0.0 || lower >= upper)
                    {
                        isValid = false;
                        messages.Add($"Error: Uncertain data at index {data.Index} has invalid measurement-error integration bounds at the 1E-8 probability window.");
                    }
                }
            }

            // Log-distribution non-positive data checks
            if (Distribution is not null &&
                (DistributionType == UnivariateDistributionType.LogNormal ||
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
                        double lower = dist.InverseCDF(1E-8);
                        double upper = dist.InverseCDF(1-1E-8);

                        // Log-space B17C validation checks retained support at 1E-8 so
                        // endpoint infinities do not masquerade as data-support failures.
                        if (!Tools.IsFinite(lower) || !Tools.IsFinite(upper) || lower >= upper || lower <= 0.0)
                        {
                            nonPositiveUncertain = true;
                            break;
                        }
                    }
                }

                if (nonPositiveExact || nonPositiveUncertain)
                {
                    isValid = false;
                    messages.Add("Error: Log-based distributions cannot be used because some data values are non-positive.");
                }
            }

            // Parameter validation
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

            // Parameter penalty validation
            for (int i = 0; i < ParameterPenalties.Count; i++)
            {
                var valid = ParameterPenalties[i].Validate();
                if (!valid.IsValid)
                {
                    isValid = false;
                    messages.Add(valid.Message);
                }
            }

            // Quantile penalty validation
            for (int i = 0; i < QuantilePenalties.Count; i++)
            {
                var valid = QuantilePenalties[i].Validate();
                if (!valid.IsValid)
                {
                    isValid = false;
                    messages.Add(valid.Message);
                }

                // Cross-validate quantile penalty ordering
                if (i >= 1)
                {
                    if (QuantilePenalties[i].AEP >= QuantilePenalties[i - 1].AEP)
                    {
                        isValid = false;
                        messages.Add("Error: Quantile penalties must have strictly decreasing annual exceedance probabilities (AEP).");
                    }

                    if (QuantilePenalties[i].Mean <= QuantilePenalties[i - 1].Mean)
                    {
                        isValid = false;
                        messages.Add("Error: Quantile penalty means must increase with decreasing annual exceedance probability.");
                    }
                }
            }

            return (isValid, messages);
        }

        /// <inheritdoc/>
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
