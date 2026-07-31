using System.ComponentModel;
using System.Globalization;
using System.Xml.Linq;
using Numerics;
using Numerics.Distributions;
using Numerics.Mathematics;
using Numerics.Mathematics.Integration;
using Numerics.Mathematics.LinearAlgebra;
using Numerics.Mathematics.Optimization;
using Numerics.Mathematics.SpecialFunctions;
using RMC.BestFit.Estimation;

namespace RMC.BestFit.Models
{
    /// <summary>
    /// Mixture model for univariate distributions, with optional zero inflation.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    /// The <see cref="MixtureModel"/> represents a finite mixture of
    /// univariate distributions, optionally with a zero-inflated mass at
    /// zero. It supports:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Mixtures of one to three component distributions.</description></item>
    /// <item><description>Bayesian priors on parameters and quantiles.</description></item>
    /// <item><description>A fixed atom at zero with continuous components conditioned on positive values.</description></item>
    /// <item><description>Expectation–Maximization (EM) for approximate MLE.</description></item>
    /// </list>
    /// </remarks>
    public class MixtureModel : UnivariateDistributionModelBase, ISimulatable<double[]>, IUnivariateModel
    {
        #region Construction

        /// <summary>
        /// Constructs an empty mixture distribution model with a default
        /// Normal–Normal mixture and a single quantile prior.
        /// </summary>
        public MixtureModel()
        {
            _useSingleQuantile = true;
            SetDefaultMixture(new[] { UnivariateDistributionType.Normal, UnivariateDistributionType.Normal });
        }

        /// <summary>
        /// Constructs a mixture distribution model.
        /// </summary>
        /// <param name="dataFrame">The data frame to fit to.</param>
        /// <param name="distribution">The mixture distribution.</param>
        public MixtureModel(DataFrame dataFrame, Mixture distribution)
        {
            _useSingleQuantile = true;
            Mixture = (Mixture)distribution.Clone();
            DataFrame = dataFrame;
        }

        /// <summary>
        /// Constructs a mixture distribution model from a list of existing
        /// component distributions.
        /// </summary>
        /// <param name="dataFrame">The data frame to fit to.</param>
        /// <param name="distributions">The list of component distributions.</param>
        /// <param name="isZeroInflated">
        /// If true, derives a fixed atom from exact observations equal to zero.
        /// </param>
        public MixtureModel(DataFrame dataFrame, List<UnivariateDistributionBase> distributions, bool isZeroInflated = false)
        {
            _useSingleQuantile = true;
            DataFrame = dataFrame;
            IsZeroInflated = isZeroInflated;

            if (distributions == null || distributions.Count == 0)
                throw new ArgumentException("At least one component distribution is required.", nameof(distributions));

            int k = distributions.Count;
            var weights = Enumerable.Repeat(1.0 / k, k).ToArray();

            // Use the user-provided distributions directly.
            Mixture = new Mixture(weights, distributions.ToArray());
        }

        /// <summary>
        /// Constructs a mixture distribution model from a list of component
        /// distribution types with equal initial weights.
        /// </summary>
        /// <param name="dataFrame">The data frame to fit to.</param>
        /// <param name="distributionTypes">The list of univariate distribution types.</param>
        /// <param name="isZeroInflated">
        /// If true, derives a fixed atom from exact observations equal to zero.
        /// </param>
        public MixtureModel(DataFrame dataFrame, List<UnivariateDistributionType> distributionTypes, bool isZeroInflated = false)
        {
            _useSingleQuantile = true;
            DataFrame = dataFrame;
            IsZeroInflated = isZeroInflated;
            SetDefaultMixture(distributionTypes);
        }

        /// <summary>
        /// Constructs a mixture distribution model from an XML element and
        /// an associated data frame.
        /// </summary>
        /// <param name="dataFrame">The data frame to fit to.</param>
        /// <param name="xElement">The XML element to deserialize.</param>
        public MixtureModel(DataFrame dataFrame, XElement xElement)
        {
            // Set data frame
            if (_dataFrame != null)
                _dataFrame.PropertyChanged -= DataFrame_PropertyChanged;

            _dataFrame = dataFrame;
            _dataFrame.PropertyChanged += DataFrame_PropertyChanged;
            _dataFrame.ProcessThresholdSeries();
            _dataFrame.CreateFullTimeSeries();

            var distributionElement = xElement.Element("Distribution");
            if (distributionElement != null)
                Mixture = Mixture.FromXElement(distributionElement);

            var isZeroInflatedAttr = xElement.Attribute(nameof(IsZeroInflated));
            if (isZeroInflatedAttr != null)
            {
                bool.TryParse(isZeroInflatedAttr.Value, out var isZeroInflated);
                IsZeroInflated = isZeroInflated;
            }

            // Parameters
            var useDefaultFlatPriorsAttr = xElement.Attribute(nameof(UseDefaultFlatPriors));
            if (useDefaultFlatPriorsAttr != null)
                bool.TryParse(useDefaultFlatPriorsAttr.Value, out _useDefaultFlatPriors);

            var useJeffreysRuleForScaleAttr = xElement.Attribute(nameof(UseJeffreysRuleForScale));
            if (useJeffreysRuleForScaleAttr != null)
                bool.TryParse(useJeffreysRuleForScaleAttr.Value, out _useJeffreysRuleForScale);

            var parms = new List<ModelParameter>();
            foreach (XElement p in xElement.Elements(nameof(Parameters)).Elements(nameof(ModelParameter)))
                parms.Add(new ModelParameter(p));
            Parameters = parms;

            // Quantiles (mixture model uses a single quantile prior)
            var enableQuantilePriorsAttr = xElement.Attribute(nameof(EnableQuantilePriors));
            if (enableQuantilePriorsAttr != null)
                bool.TryParse(enableQuantilePriorsAttr.Value, out _enableQuantilePriors);

            _useSingleQuantile = true;
            var quants = new List<QuantilePrior>();
            foreach (XElement q in xElement.Elements(nameof(QuantilePriors)).Elements(nameof(QuantilePrior)))
                quants.Add(new QuantilePrior(q));
            QuantilePriors = quants;


        }


        #endregion

        #region Members

        /// <summary>
        /// The set of univariate distribution types supported by the mixture model.
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
        /// Determines whether the specified distribution type is supported by the mixture model.
        /// </summary>
        /// <param name="distributionType">The distribution type to check.</param>
        /// <returns><c>true</c> if the distribution type is supported; otherwise, <c>false</c>.</returns>
        public static bool IsSupportedDistributionType(UnivariateDistributionType distributionType)
        {
            return _supportedDistributionTypes.Contains(distributionType);
        }

        private Mixture? _mixture = null;
        private bool _isZeroInflated = false;

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

                    if (Mixture is not null)
                    {
                        if (IsZeroInflated)
                        {
                            Mixture.ZeroWeight = GetExactZeroWeight();
                        }
                        else
                        {
                            Mixture.ZeroWeight = 0.0;
                        }
                    }

                    if (UseDefaultFlatPriors)
                        SetDefaultParameters();
                }

                RaisePropertyChange(nameof(DataFrame));
            }
        }

        /// <summary>
        /// Gets or sets the mixture distribution representing the parent population.
        /// </summary>
        [Category("Inputs")]
        [DisplayName("Mixture Distribution")]
        [Description("The mixture distribution representing the parent population.")]
        [Browsable(true)]
        public Mixture? Mixture
        {
            get { return _mixture; }
            set
            {
                _mixture = value;
                RaisePropertyChange(nameof(Mixture));

                if (_mixture is not null)
                {
                    _mixture.IsZeroInflated = IsZeroInflated;
                    if (IsZeroInflated)
                    {
                        _mixture.ZeroWeight = DataFrame is not null ? GetExactZeroWeight() : 0.0;
                    }
                    else
                    {
                        _mixture.ZeroWeight = 0.0;
                    }
                }

                SetDefaultParameters();
                SetDefaultQuantilePriors();
            }
        }

        /// <inheritdoc/>
        UnivariateDistributionBase? IUnivariateModel.Distribution => _mixture;

        /// <inheritdoc/>
        /// <remarks>
        /// Mixture component distributions are treated as stationary within this model.
        /// Always returns <c>false</c>.
        /// </remarks>
        public bool IsNonstationary => false;

        /// <summary>
        /// Determines whether a fixed atom is assigned to exact observations equal to zero.
        /// </summary>
        [Category("Inputs")]
        [DisplayName("Is Zero-Inflated")]
        [Description("If true, a weight derived from the input data is assigned to all data points ≤ 0.")]
        [Browsable(true)]
        public bool IsZeroInflated
        {
            get { return _isZeroInflated; }
            set
            {
                if (_isZeroInflated != value)
                {
                    _isZeroInflated = value;
                    RaisePropertyChange(nameof(IsZeroInflated));

                    if (Mixture is not null)
                    {
                        Mixture.IsZeroInflated = _isZeroInflated;
                        if (_isZeroInflated)
                        {
                            Mixture.ZeroWeight = DataFrame != null ? GetExactZeroWeight() : 0.0;
                        }
                        else
                        {
                            Mixture.ZeroWeight = 0.0;
                        }
                    }

                    if (_useDefaultFlatPriors)
                        SetDefaultParameters();
                }
            }
        }

        /// <summary>
        /// Mixture model currently supports only a single quantile prior.
        /// Attempts to disable this are ignored.
        /// </summary>
        [Category("Inputs")]
        [DisplayName("Use Single Quantile")]
        [Description("Mixture model currently supports only a single quantile prior.")]
        [Browsable(true)]
        public override bool UseSingleQuantile
        {
            get { return true; }
            set
            {
                // Mixture model is restricted to a single quantile prior.
                if (!value)
                    return;

                if (!_useSingleQuantile)
                {
                    _useSingleQuantile = true;
                    RaisePropertyChange(nameof(UseSingleQuantile));
                    SetDefaultQuantilePriors();
                }
            }
        }


        #endregion

        #region Methods

        /// <summary>
        /// Computes the fixed zero atom from exact annual records only.
        /// </summary>
        /// <returns>The number of exact values equal to zero divided by the number of exact records.</returns>
        private double GetExactZeroWeight()
        {
            if (DataFrame?.ExactSeries is null || DataFrame.ExactSeries.Count == 0) return 0.0;
            int zeroCount = DataFrame.ExactSeries.Count(data => data.Value == 0.0);
            return zeroCount / (double)DataFrame.ExactSeries.Count;
        }

        /// <summary>
        /// Gets the exact sample used to initialize continuous component parameters.
        /// </summary>
        /// <returns>All exact values for an ordinary mixture, or strictly positive exact values for a hurdle mixture.</returns>
        private List<double> GetComponentInitializationSample()
        {
            return DataFrame.ExactSeries
                .Where(data => !IsZeroInflated || data.Value > 0.0)
                .Select(data => data.Value)
                .ToList();
        }

        /// <summary>
        /// Gets the number of free physical mixture weights.
        /// </summary>
        /// <param name="mixture">The Numerics mixture.</param>
        /// <returns>Zero for one component; otherwise, one less than the component count.</returns>
        private static int GetFreeWeightCount(Mixture mixture)
        {
            return Math.Max(0, mixture.Distributions.Length - 1);
        }

        /// <summary>
        /// Expands BestFit's free-weight vector to Numerics' full physical-weight vector.
        /// </summary>
        /// <param name="parameters">The BestFit parameter vector.</param>
        /// <param name="physicalParameters">The full Numerics parameter vector when feasible.</param>
        /// <returns><see langword="true"/> when the proposal has the expected size and lies on the configured simplex.</returns>
        private bool TryExpandPhysicalParameters(IList<double> parameters, out double[] physicalParameters)
        {
            physicalParameters = Array.Empty<double>();
            if (Mixture is null || parameters is null) return false;

            int componentCount = Mixture.Distributions.Length;
            int freeWeightCount = GetFreeWeightCount(Mixture);
            int distributionParameterCount = Mixture.Distributions!.Sum(distribution => distribution.NumberOfParameters);
            if (parameters.Count != freeWeightCount + distributionParameterCount) return false;

            double componentMass = IsZeroInflated ? 1.0 - Mixture.ZeroWeight : 1.0;
            if (!Tools.IsFinite(componentMass) || componentMass <= 0.0) return false;

            physicalParameters = new double[componentCount + distributionParameterCount];
            double trackedWeightSum = 0.0;
            for (int i = 0; i < freeWeightCount; i++)
            {
                double weight = parameters[i];
                if (!Tools.IsFinite(weight) || weight < 0.0) return false;
                physicalParameters[i] = weight;
                trackedWeightSum += weight;
            }

            double finalWeight = componentMass - trackedWeightSum;
            if (!Tools.IsFinite(finalWeight) || finalWeight < 0.0) return false;
            physicalParameters[componentCount - 1] = finalWeight;

            for (int i = 0; i < distributionParameterCount; i++)
            {
                physicalParameters[componentCount + i] = parameters[freeWeightCount + i];
            }
            return true;
        }

        /// <summary>
        /// Attempts to reconstruct a Numerics mixture from a BestFit parameter vector.
        /// </summary>
        /// <param name="parameters">The BestFit parameter vector.</param>
        /// <param name="distribution">The reconstructed distribution when the proposal is valid.</param>
        /// <returns><see langword="true"/> when reconstruction succeeds and all Numerics parameters are valid.</returns>
        private bool TryCreateDistribution(IList<double> parameters, out Mixture? distribution)
        {
            distribution = null;
            if (Mixture is null || !TryExpandPhysicalParameters(parameters, out double[] physicalParameters)) return false;
            try
            {
                var candidate = (Mixture)Mixture.Clone();
                candidate.SetParameters(physicalParameters);
                if (!candidate.ParametersValid) return false;
                distribution = candidate;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Reconstructs a Numerics mixture from a BestFit parameter vector without mutating the caller.
        /// </summary>
        /// <param name="parameters">The BestFit parameter vector.</param>
        /// <returns>A reconstructed Numerics mixture with all physical component weights.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the proposal is outside the feasible simplex or contains invalid component parameters.</exception>
        internal Mixture CreateDistribution(IList<double> parameters)
        {
            if (!TryCreateDistribution(parameters, out Mixture? distribution))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(parameters),
                    "The mixture proposal is outside the feasible physical simplex or contains invalid component parameters.");
            }
            return distribution!;
        }

        /// <summary>
        /// Evaluates an uncertain observation, including the fixed atom and continuous contribution.
        /// </summary>
        /// <param name="model">The configured Numerics mixture.</param>
        /// <param name="measurementDistribution">The observation's measurement-error distribution.</param>
        /// <returns>The retained-window observation probability, or zero when the window is invalid.</returns>
        private static double UncertainObservationProbability(
            Mixture model,
            UnivariateDistributionBase measurementDistribution)
        {
            const double lowerProbability = 1E-8;
            const double upperProbability = 1.0 - 1E-8;
            double lower = measurementDistribution.InverseCDF(lowerProbability);
            double upper = measurementDistribution.InverseCDF(upperProbability);
            double retainedMass = upperProbability - lowerProbability;
            if (!Tools.IsFinite(lower) || !Tools.IsFinite(upper) || !Tools.IsFinite(retainedMass) ||
                retainedMass <= 0.0 || lower >= upper)
            {
                return 0.0;
            }

            double probability = 0.0;
            if (model.IsZeroInflated && lower <= 0.0 && upper >= 0.0)
            {
                probability += model.ZeroWeight * measurementDistribution.PDF(0.0);
            }

            double integrationLower = model.IsZeroInflated ? Math.Max(0.0, lower) : lower;
            if (integrationLower < upper)
            {
                probability += Integration.GaussLegendre20(
                    value => measurementDistribution.PDF(value) * model.PDF(value),
                    integrationLower,
                    upper);
            }

            probability /= retainedMass;
            return Tools.IsFinite(probability) && probability > 0.0 ? probability : 0.0;
        }

        /// <summary>
        /// Evaluates the complete BestFit data likelihood for a configured Numerics mixture.
        /// </summary>
        /// <param name="model">The configured Numerics mixture.</param>
        /// <returns>The data log likelihood, or negative infinity for an impossible observation.</returns>
        private double EvaluateDataLogLikelihood(Mixture model)
        {
            double logLikelihood = 0.0;
            foreach (ExactData data in DataFrame.ExactSeries)
            {
                if (model.IsZeroInflated && data.Value < 0.0) return double.NegativeInfinity;
                logLikelihood += !data.IsLowOutlier
                    ? model.LogLikelihood(data.Value)
                    : model.LogLikelihood_LeftCensored(DataFrame.LowOutlierThreshold, 1);
            }
            foreach (UncertainData data in DataFrame.UncertainSeries!)
            {
                double probability = UncertainObservationProbability(model, data.Distribution);
                if (probability <= 0.0) return double.NegativeInfinity;
                logLikelihood += Math.Log(probability);
            }
            foreach (IntervalData data in DataFrame.IntervalSeries)
            {
                logLikelihood += model.LogLikelihood_Intervals(data.LowerValue, data.UpperValue);
            }
            foreach (ThresholdData data in DataFrame.ThresholdSeries)
            {
                if (data.NumberBelow > 0)
                    logLikelihood += model.LogLikelihood_LeftCensored(data.Value, data.NumberBelow);
                if (data.NumberAbove > 0)
                    logLikelihood += model.LogLikelihood_RightCensored(data.Value, data.NumberAbove);
            }
            return Tools.IsFinite(logLikelihood) ? logLikelihood : double.NegativeInfinity;
        }
        /// <inheritdoc/>
        protected override void DataFrame_PropertyChanged(object? sender, PropertyChangedEventArgs e)
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

            if (Mixture is not null)
            {
                if (IsZeroInflated)
                {
                    Mixture.ZeroWeight = GetExactZeroWeight();
                }
                else
                {
                    Mixture.ZeroWeight = 0.0;
                }
            }

            if (UseDefaultFlatPriors)
                SetDefaultParameters();
        }

        /// <summary>
        /// Sets the default mixture distribution with equal weights
        /// for the given distribution types.
        /// </summary>
        /// <param name="distributionTypes">The list of component distribution types.</param>
        public void SetDefaultMixture(IList<UnivariateDistributionType> distributionTypes)
        {
            if (distributionTypes == null || distributionTypes.Count == 0)
                throw new ArgumentException("At least one distribution type is required.", nameof(distributionTypes));

            var weights = new List<double>();
            var distributions = new List<UnivariateDistributionBase>();
            double w = 1d / distributionTypes.Count;
            double sum = 0d;

            for (int i = 0; i < distributionTypes.Count; i++)
            {
                if (i != distributionTypes.Count - 1)
                {
                    sum += w;
                    weights.Add(w);
                }
                else
                {
                    // Ensure exact sum of weights is one.
                    weights.Add(1d - sum);
                }

                distributions.Add(UnivariateDistributionFactory.CreateDistribution(distributionTypes[i]));
            }

            Mixture = new Mixture(weights.ToArray(), distributions.ToArray());
        }

        /// <inheritdoc/>
        public override void SetDefaultParameters()
        {
            foreach (ModelParameter parameter in Parameters)
            {
                parameter.PropertyChanged -= Parameter_PropertyChanged;
            }
            _parameters = new List<ModelParameter>();

            if (Mixture is null ||
                DataFrame == null ||
                !DataFrame.Validate().IsValid ||
                Mixture.Distributions == null ||
                Mixture.Distributions.Length == 0)
            {
                RaisePropertyChange(nameof(SetDefaultParameters));
                return;
            }

            int componentCount = Mixture.Distributions.Length;
            int freeWeightCount = GetFreeWeightCount(Mixture);
            double componentMass = IsZeroInflated ? 1.0 - Mixture.ZeroWeight : 1.0;
            double initialWeight = componentMass / componentCount;
            for (int i = 0; i < freeWeightCount; i++)
            {
                Parameters.Add(new ModelParameter
                {
                    Name = "Weight (w" + SubscriptFormatter.ToSubscript(i + 1) + ")",
                    Value = initialWeight,
                    LowerBound = 0.0,
                    UpperBound = componentMass,
                    PriorDistribution = new Uniform(0.0, componentMass)
                });
            }

            List<double> initializationSample = GetComponentInitializationSample();
            for (int i = 0; i < componentCount; i++)
            {
                UnivariateDistributionBase component = Mixture.Distributions[i];
                Tuple<double[], double[], double[]> constraints =
                    ((IMaximumLikelihoodEstimation)component).GetParameterConstraints(initializationSample);
                double[] initials = constraints.Item1;
                double[] lowers = constraints.Item2;
                double[] uppers = constraints.Item3;
                string[,] parametersToString = component.ParametersToString;
                for (int j = 0; j < component.NumberOfParameters; j++)
                {
                    Parameters.Add(new ModelParameter
                    {
                        OwnerName = "D" + (i + 1).ToString(CultureInfo.InvariantCulture),
                        Name = parametersToString[j, 0],
                        Value = initials[j],
                        LowerBound = lowers[j],
                        UpperBound = uppers[j],
                        IsPositive = lowers[j] == Tools.DoubleMachineEpsilon,
                        PriorDistribution = new Uniform(lowers[j], uppers[j])
                    });
                }
            }

            foreach (ModelParameter parameter in Parameters)
            {
                parameter.PropertyChanged += Parameter_PropertyChanged;
            }
            RaisePropertyChange(nameof(SetDefaultParameters));
        }

        /// <inheritdoc/>
        public override void SetDefaultQuantilePriors()
        {
            if (Mixture is null)
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
            int qCount = 1; // mixture model uses single quantile prior

            if (QuantilePriors.Count >= 1)
            {
                priors = QuantilePriors.ToList();

                while (priors.Count > qCount)
                    priors.Remove(priors.Last());

                while (priors.Count < qCount)
                {
                    priors.Add(new QuantilePrior(priors.Last().Alpha / 10.0, new LnNormal()));
                    double mu = Math.Round(Mixture.InverseCDF(1 - priors.Last().Alpha), 2);
                    double sigma = Math.Round(mu * 0.15, 2);
                    priors.Last().Distribution.SetParameters(new[] { mu, sigma });
                }
            }
            else
            {
                for (int i = 1; i <= qCount; i++)
                {
                    priors.Add(new QuantilePrior(Math.Pow(10, -i), new LnNormal()));
                    double mu = Math.Round(Mixture.InverseCDF(1 - priors[i - 1].Alpha), 2);
                    double sigma = Math.Round(mu * 0.15, 2);
                    priors[i - 1].Distribution.SetParameters(new[] { mu, sigma });
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

            // Currently mixture uses only a single quantile prior; multi-quantile
            // branch is left here for possible future extension but not used.
            if (!UseSingleQuantile &&
                Mixture is not null &&
                QuantilePriors.Count == Mixture.NumberOfParameters)
            {
                _quantilePriorsTrue.Add(QuantilePriors[0].Clone());

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

        /// <summary>
        /// Performs Expectation–Maximization to obtain approximate MLE
        /// estimates and covariance matrix for the mixture model.
        /// </summary>
        /// <param name="parameters">The estimated parameters with K-1 free weights followed by component parameters.</param>
        /// <param name="covariance">The approximate covariance matrix in the K-1 free-weight parameterization.</param>
        /// <param name="iterations">The number of EM iterations performed.</param>
        /// <param name="maxIterations">Maximum EM iterations (default 1000).</param>
        /// <param name="tolerance">Relative tolerance for convergence (default 1E-8).</param>
        public void ExpectationMaximization(out double[] parameters, out double[,] covariance, out int iterations, int maxIterations = 1000, double tolerance = 1E-8)
        {
            parameters = Array.Empty<double>();
            covariance = new double[0, 0];
            iterations = 0;
            if (Mixture is null || DataFrame is null) return;

            var model = (Mixture)Mixture.Clone();
            DataFrame.CreateFullTimeSeries();
            List<Data> observations = DataFrame.FullTimeSeries.ToList();
            int observationCount = observations.Count;
            int componentCount = model.Distributions.Length;
            int freeWeightCount = GetFreeWeightCount(model);
            int distributionParameterCount = model.Distributions.Sum(distribution => distribution.NumberOfParameters);
            if (observationCount == 0 || componentCount == 0) return;

            for (int rowIndex = 0; rowIndex < DataFrame.ExactSeries.Count; rowIndex++)
            {
                ExactData exact = (ExactData)DataFrame.ExactSeries[rowIndex];
                if (model.IsZeroInflated && exact.Value < 0.0)
                {
                    throw new InvalidOperationException(
                        $"Mixture EM row {rowIndex} has negative exact value {exact.Value:R} in a zero-inflated model.");
                }
            }
            model.ValidateParameters(model.GetParameters, true);

            List<double> initializationSample = GetComponentInitializationSample();
            Tuple<double[], double[], double[]> constraints = model.GetParameterConstraints(initializationSample);
            double[] initialParameters = constraints.Item1.Subset(componentCount);
            double[] lowerBounds = constraints.Item2.Subset(componentCount);
            double[] upperBounds = constraints.Item3.Subset(componentCount);
            double[] mleWeights = constraints.Item1.Subset(0, componentCount - 1);
            double[] mleParameters = initialParameters;
            var responsibilities = new double[observationCount, componentCount];
            double oldLogLikelihood = double.MinValue;
            double newLogLikelihood = double.MinValue;

            double PositiveMass(int componentIndex)
            {
                return model.Distributions[componentIndex].CCDF(0.0);
            }

            double ComponentDensity(int componentIndex, double value)
            {
                if (!model.IsZeroInflated) return model.Distributions[componentIndex].PDF(value);
                return value > 0.0
                    ? model.Distributions[componentIndex].PDF(value) / PositiveMass(componentIndex)
                    : 0.0;
            }

            double ComponentCdf(int componentIndex, double value)
            {
                if (!model.IsZeroInflated) return model.Distributions[componentIndex].CDF(value);
                if (value <= 0.0) return 0.0;
                double mass = PositiveMass(componentIndex);
                return Math.Max(
                    0.0,
                    Math.Min(
                        1.0,
                        (model.Distributions[componentIndex].CDF(value) -
                         model.Distributions[componentIndex].CDF(0.0)) / mass));
            }

            double ComponentCcdf(int componentIndex, double value)
            {
                if (!model.IsZeroInflated) return model.Distributions[componentIndex].CCDF(value);
                if (value < 0.0) return 1.0;
                return Math.Max(
                    0.0,
                    Math.Min(1.0, model.Distributions[componentIndex].CCDF(value) / PositiveMass(componentIndex)));
            }

            double UncertainComponentProbability(int componentIndex, UnivariateDistributionBase measurementDistribution)
            {
                const double lowerProbability = 1E-8;
                const double upperProbability = 1.0 - 1E-8;
                double lower = measurementDistribution.InverseCDF(lowerProbability);
                double upper = measurementDistribution.InverseCDF(upperProbability);
                double retainedMass = upperProbability - lowerProbability;
                if (!Tools.IsFinite(lower) || !Tools.IsFinite(upper) || !Tools.IsFinite(retainedMass) ||
                    retainedMass <= 0.0 || lower >= upper)
                {
                    return 0.0;
                }

                double integrationLower = model.IsZeroInflated ? Math.Max(0.0, lower) : lower;
                if (integrationLower >= upper) return 0.0;
                double probability = Integration.GaussLegendre20(
                    value => measurementDistribution.PDF(value) * ComponentDensity(componentIndex, value),
                    integrationLower,
                    upper) / retainedMass;
                return Tools.IsFinite(probability) && probability > 0.0 ? probability : 0.0;
            }

            double AtomProbability(Data observation)
            {
                if (!model.IsZeroInflated || model.ZeroWeight <= 0.0) return 0.0;
                if (observation is ExactData exact)
                {
                    if (!exact.IsLowOutlier) return exact.Value == 0.0 ? model.ZeroWeight : 0.0;
                    return DataFrame.LowOutlierThreshold >= 0.0 ? model.ZeroWeight : 0.0;
                }
                if (observation is UncertainData uncertain)
                {
                    const double lowerProbability = 1E-8;
                    const double upperProbability = 1.0 - 1E-8;
                    double lower = uncertain.Distribution.InverseCDF(lowerProbability);
                    double upper = uncertain.Distribution.InverseCDF(upperProbability);
                    double retainedMass = upperProbability - lowerProbability;
                    if (!Tools.IsFinite(lower) || !Tools.IsFinite(upper) || !Tools.IsFinite(retainedMass) ||
                        retainedMass <= 0.0 || lower >= upper || lower > 0.0 || upper < 0.0)
                    {
                        return 0.0;
                    }
                    return model.ZeroWeight * uncertain.Distribution.PDF(0.0) / retainedMass;
                }
                if (observation is IntervalData interval)
                {
                    return interval.LowerValue < 0.0 && interval.UpperValue >= 0.0
                        ? model.ZeroWeight
                        : 0.0;
                }
                if (observation is ThresholdData threshold)
                {
                    if (threshold.NumberBelow == 1 && threshold.NumberAbove == 0)
                        return threshold.Value >= 0.0 ? model.ZeroWeight : 0.0;
                    if (threshold.NumberBelow == 0 && threshold.NumberAbove == 1)
                        return threshold.Value < 0.0 ? model.ZeroWeight : 0.0;
                }
                return 0.0;
            }

            double ComponentObservationProbability(Data observation, int componentIndex)
            {
                if (observation is ExactData exact)
                {
                    return !exact.IsLowOutlier
                        ? ComponentDensity(componentIndex, exact.Value)
                        : ComponentCdf(componentIndex, DataFrame.LowOutlierThreshold);
                }
                if (observation is UncertainData uncertain)
                {
                    return UncertainComponentProbability(componentIndex, uncertain.Distribution);
                }
                if (observation is IntervalData interval)
                {
                    return Math.Max(
                        0.0,
                        ComponentCdf(componentIndex, interval.UpperValue) -
                        ComponentCdf(componentIndex, interval.LowerValue));
                }
                if (observation is ThresholdData threshold)
                {
                    if (threshold.NumberBelow == 1 && threshold.NumberAbove == 0)
                        return ComponentCdf(componentIndex, threshold.Value);
                    if (threshold.NumberBelow == 0 && threshold.NumberAbove == 1)
                        return ComponentCcdf(componentIndex, threshold.Value);
                }
                return 0.0;
            }

            InvalidOperationException CreateImpossibleRowException(int rowIndex, double value)
            {
                return new InvalidOperationException(
                    $"Mixture EM row {rowIndex} with value {value:R} has zero or nonfinite total probability.");
            }

            double EStep(double[] distributionParameters)
            {
                model.SetParameters(mleWeights, distributionParameters);
                double logLikelihood = 0.0;
                for (int rowIndex = 0; rowIndex < observationCount; rowIndex++)
                {
                    Data observation = observations[rowIndex];
                    double atomProbability = AtomProbability(observation);
                    if (!Tools.IsFinite(atomProbability) || atomProbability < 0.0)
                        throw CreateImpossibleRowException(rowIndex, observation.Value);
                    double atomLogProbability = atomProbability > 0.0
                        ? Math.Log(atomProbability)
                        : double.NegativeInfinity;
                    double maximumLogProbability = atomLogProbability;

                    for (int componentIndex = 0; componentIndex < componentCount; componentIndex++)
                    {
                        double componentProbability = ComponentObservationProbability(observation, componentIndex);
                        if (!Tools.IsFinite(componentProbability) || componentProbability < 0.0)
                            throw CreateImpossibleRowException(rowIndex, observation.Value);
                        double componentLogProbability = mleWeights[componentIndex] > 0.0 && componentProbability > 0.0
                            ? Math.Log(mleWeights[componentIndex]) + Math.Log(componentProbability)
                            : double.NegativeInfinity;
                        responsibilities[rowIndex, componentIndex] = componentLogProbability;
                        if (componentLogProbability > maximumLogProbability)
                            maximumLogProbability = componentLogProbability;
                    }

                    if (!Tools.IsFinite(maximumLogProbability))
                        throw CreateImpossibleRowException(rowIndex, observation.Value);

                    double scaledProbabilitySum = atomProbability > 0.0
                        ? Math.Exp(atomLogProbability - maximumLogProbability)
                        : 0.0;
                    for (int componentIndex = 0; componentIndex < componentCount; componentIndex++)
                    {
                        scaledProbabilitySum += Math.Exp(
                            responsibilities[rowIndex, componentIndex] - maximumLogProbability);
                    }
                    if (!Tools.IsFinite(scaledProbabilitySum) || scaledProbabilitySum <= 0.0)
                        throw CreateImpossibleRowException(rowIndex, observation.Value);

                    double rowLogProbability = maximumLogProbability + Math.Log(scaledProbabilitySum);
                    if (!Tools.IsFinite(rowLogProbability))
                        throw CreateImpossibleRowException(rowIndex, observation.Value);
                    for (int componentIndex = 0; componentIndex < componentCount; componentIndex++)
                    {
                        responsibilities[rowIndex, componentIndex] =
                            Math.Exp(responsibilities[rowIndex, componentIndex] - rowLogProbability);
                    }
                    logLikelihood += rowLogProbability;
                }
                return logLikelihood;
            }

            double Objective(double[] distributionParameters)
            {
                model.SetParameters(mleWeights, distributionParameters);
                return EvaluateDataLogLikelihood(model);
            }

            double[] MStep(double[] distributionParameters)
            {
                for (int componentIndex = 0; componentIndex < componentCount; componentIndex++)
                {
                    double weight = 0.0;
                    for (int rowIndex = 0; rowIndex < observationCount; rowIndex++)
                    {
                        weight += responsibilities[rowIndex, componentIndex];
                    }
                    mleWeights[componentIndex] = weight;
                }

                double weightSum = mleWeights.Sum();
                double componentMass = model.IsZeroInflated ? 1.0 - model.ZeroWeight : 1.0;
                if (!Tools.IsFinite(weightSum) || weightSum <= 0.0)
                {
                    throw new InvalidOperationException(
                        "Mixture EM cannot update component weights because no finite positive responsibility mass is available.");
                }
                double scale = componentMass / weightSum;
                for (int componentIndex = 0; componentIndex < componentCount; componentIndex++)
                {
                    mleWeights[componentIndex] *= scale;
                }

                var solver = new NelderMead(
                    Objective,
                    distributionParameterCount,
                    distributionParameters,
                    lowerBounds,
                    upperBounds);
                solver.Maximize();
                return solver.BestParameterSet.Values;
            }

            bool useNumericsExactPath =
                DataFrame.UncertainSeries.Count == 0 &&
                DataFrame.IntervalSeries.Count == 0 &&
                DataFrame.ThresholdSeries.Count == 0 &&
                DataFrame.ExactSeries.All(data => !((ExactData)data).IsLowOutlier);

            if (useNumericsExactPath)
            {
                model.MaxIterations = maxIterations;
                model.Tolerance = tolerance;
                double[] physicalResult = model.MLE(DataFrame.ExactSeries.Select(data => data.Value).ToList());
                Array.Copy(physicalResult, 0, mleWeights, 0, componentCount);
                Array.Copy(physicalResult, componentCount, mleParameters, 0, distributionParameterCount);
                iterations = model.Iterations;
            }
            else
            {
                for (iterations = 1; iterations <= maxIterations; iterations++)
                {
                    newLogLikelihood = EStep(mleParameters);
                    if (Math.Abs((oldLogLikelihood - newLogLikelihood) / oldLogLikelihood) < tolerance) break;
                    mleParameters = MStep(mleParameters);
                    oldLogLikelihood = newLogLikelihood;
                }
            }

            var result = new List<double>();
            result.AddRange(mleWeights.Take(freeWeightCount));
            result.AddRange(mleParameters);
            parameters = result.ToArray();

            Matrix hessian = NumericalDiff.ComputeHessian(
                Objective,
                mleParameters,
                mleParameters.Length);
            Matrix fisher = (hessian * -1.0).Inverse();
            covariance = new double[parameters.Length, parameters.Length];

            // The free coordinates are the first K-1 physical weights, so the
            // transformed covariance is the corresponding principal submatrix.
            for (int i = 0; i < freeWeightCount; i++)
            {
                double weightI = mleWeights[i];
                covariance[i, i] = weightI * (1.0 - weightI) / observationCount;
                for (int j = 0; j < freeWeightCount; j++)
                {
                    if (i == j) continue;
                    double weightJ = mleWeights[j];
                    double correlation = -1.0 / (componentCount - 1.0) + Tools.DoubleMachineEpsilon;
                    double varianceI = weightI * (1.0 - weightI) / observationCount;
                    double varianceJ = weightJ * (1.0 - weightJ) / observationCount;
                    covariance[i, j] = correlation * Math.Sqrt(varianceI * varianceJ);
                }
            }
            for (int i = 0; i < fisher.NumberOfRows; i++)
            {
                for (int j = 0; j < fisher.NumberOfColumns; j++)
                {
                    covariance[i + freeWeightCount, j + freeWeightCount] = fisher[i, j];
                }
            }
        }

        /// <inheritdoc/>
        public override double LogLikelihood(double[] parameters)
        {
            // Get the data likelihood
            double dataLogLH = DataLogLikelihood(parameters);
            if (!Tools.IsFinite(dataLogLH))
                return double.NegativeInfinity;

            // Get the prior likelihood
            double priorLogLH = PriorLogLikelihood(parameters);

            // The full likelihood
            double logLH = dataLogLH + priorLogLH;

            return Tools.IsFinite(logLH) ? logLH : double.NegativeInfinity;
        }

        /// <inheritdoc/>
        public override double DataLogLikelihood(double[] parameters)
        {
            if (Mixture is null || DataFrame is null ||
                !TryCreateDistribution(parameters, out Mixture? model))
            {
                return double.NegativeInfinity;
            }
            return EvaluateDataLogLikelihood(model!);
        }

        /// <inheritdoc/>
        public override double[] PointwiseDataLogLikelihood(double[] parameters)
        {
            if (Mixture is null || DataFrame is null ||
                !TryCreateDistribution(parameters, out Mixture? model))
            {
                int count = DataFrame?.ExactSeries.Count ?? 0;
                count += DataFrame?.UncertainSeries.Count ?? 0;
                count += DataFrame?.IntervalSeries.Count ?? 0;
                count += DataFrame?.ThresholdSeries.Count ?? 0;
                return Enumerable.Repeat(double.NegativeInfinity, count).ToArray();
            }

            var result = new List<double>();
            foreach (ExactData data in DataFrame.ExactSeries)
            {
                double logLikelihood = model!.IsZeroInflated && data.Value < 0.0
                    ? double.NegativeInfinity
                    : !data.IsLowOutlier
                        ? model.LogLikelihood(data.Value)
                        : model.LogLikelihood_LeftCensored(DataFrame.LowOutlierThreshold, 1);
                result.Add(logLikelihood);
            }
            foreach (UncertainData data in DataFrame.UncertainSeries!)
            {
                double probability = UncertainObservationProbability(model!, data.Distribution);
                result.Add(probability > 0.0 ? Math.Log(probability) : double.NegativeInfinity);
            }
            foreach (IntervalData data in DataFrame.IntervalSeries)
            {
                result.Add(model!.LogLikelihood_Intervals(data.LowerValue, data.UpperValue));
            }
            foreach (ThresholdData data in DataFrame.ThresholdSeries)
            {
                double logLikelihood = 0.0;
                if (data.NumberBelow > 0)
                    logLikelihood += model!.LogLikelihood_LeftCensored(data.Value, data.NumberBelow);
                if (data.NumberAbove > 0)
                    logLikelihood += model!.LogLikelihood_RightCensored(data.Value, data.NumberAbove);
                result.Add(logLikelihood);
            }
            return result.ToArray();
        }

        /// <inheritdoc/>
        public override List<DataComponent> PointwiseDataLogLikelihoodComponents(double[] parameters)
        {
            var result = new List<DataComponent>();
            if (Mixture is null || DataFrame is null ||
                !TryCreateDistribution(parameters, out Mixture? model))
            {
                return result;
            }

            int resultIndex = 0;
            foreach (ExactData data in DataFrame.ExactSeries)
            {
                double value = data.IsLowOutlier ? DataFrame.LowOutlierThreshold : data.Value;
                double logLikelihood = model!.IsZeroInflated && data.Value < 0.0
                    ? double.NegativeInfinity
                    : !data.IsLowOutlier
                        ? model.LogLikelihood(data.Value)
                        : model.LogLikelihood_LeftCensored(DataFrame.LowOutlierThreshold, 1);
                result.Add(new DataComponent(resultIndex++, logLikelihood, value, DataComponentType.Exact, 1, data.Index.ToString()));
            }
            foreach (UncertainData data in DataFrame.UncertainSeries!)
            {
                double probability = UncertainObservationProbability(model!, data.Distribution);
                double logLikelihood = probability > 0.0 ? Math.Log(probability) : double.NegativeInfinity;
                result.Add(new DataComponent(resultIndex++, logLikelihood, data.Distribution.Mean, DataComponentType.Uncertain, 1, data.Index.ToString()));
            }
            foreach (IntervalData data in DataFrame.IntervalSeries)
            {
                double logLikelihood = model!.LogLikelihood_Intervals(data.LowerValue, data.UpperValue);
                double midpoint = (data.LowerValue + data.UpperValue) / 2.0;
                result.Add(new DataComponent(resultIndex++, logLikelihood, midpoint, DataComponentType.Interval, 1, data.Index.ToString()));
            }
            int thresholdIndex = 0;
            foreach (ThresholdData data in DataFrame.ThresholdSeries)
            {
                double logLikelihood = 0.0;
                if (data.NumberBelow > 0)
                    logLikelihood += model!.LogLikelihood_LeftCensored(data.Value, data.NumberBelow);
                if (data.NumberAbove > 0)
                    logLikelihood += model!.LogLikelihood_RightCensored(data.Value, data.NumberAbove);
                int totalCount = data.NumberBelow + data.NumberAbove;
                result.Add(new DataComponent(
                    resultIndex++,
                    logLikelihood,
                    data.Value,
                    DataComponentType.LeftCensored,
                    totalCount,
                    $"Threshold {++thresholdIndex}"));
            }
            return result;
        }

        /// <inheritdoc/>
        public override double PriorLogLikelihood(double[] parameters)
        {
            if (Mixture is null || Parameters is null ||
                !TryCreateDistribution(parameters, out Mixture? model))
            {
                return double.NegativeInfinity;
            }

            double logLikelihood = 0.0;
            for (int i = 0; i < Parameters.Count; i++)
            {
                logLikelihood += Parameters[i].PriorDistribution.LogPDF(parameters[i]);
            }

            int componentCount = model!.Distributions.Length;
            if (componentCount > 1)
            {
                // Uniform(0, m) terms contribute -(K-1)log(m); this completes
                // the normalized flat density on the physical simplex.
                logLikelihood += Gamma.LogGamma(componentCount);
            }

            if (UseJeffreysRuleForScale)
            {
                for (int i = 0; i < componentCount; i++)
                {
                    if (!TryGetJeffreysScaleParameter(model.Distributions[i], out double scale)) continue;
                    if (scale <= 0.0) return double.NegativeInfinity;
                    logLikelihood -= Math.Log(scale);
                }
            }
            if (EnableQuantilePriors && _quantilePriorsTrue.Count == 1)
            {
                double quantile = model.InverseCDF(1.0 - _quantilePriorsTrue[0].Alpha);
                logLikelihood += _quantilePriorsTrue[0].Distribution.LogPDF(quantile);
            }
            return Tools.IsFinite(logLikelihood) ? logLikelihood : double.NegativeInfinity;
        }

        /// <inheritdoc/>
        public override List<PriorComponent> PointwisePriorLogLikelihood(double[] parameters)
        {
            var result = new List<PriorComponent>();
            if (Mixture is null || Parameters is null ||
                !TryCreateDistribution(parameters, out Mixture? model))
            {
                return result;
            }

            for (int i = 0; i < Parameters.Count; i++)
            {
                double logLikelihood = Parameters[i].PriorDistribution.LogPDF(parameters[i]);
                string parameterName = string.IsNullOrEmpty(Parameters[i].OwnerName)
                    ? Parameters[i].Name
                    : Parameters[i].OwnerName;
                result.Add(new PriorComponent(
                    $"Parameter Prior: {parameterName}",
                    logLikelihood,
                    PriorComponentType.ParameterPrior));
            }

            int componentCount = model!.Distributions.Length;
            if (componentCount > 1)
            {
                result.Add(new PriorComponent(
                    "Mixture simplex normalization",
                    Gamma.LogGamma(componentCount),
                    PriorComponentType.ParameterPrior));
            }
            if (UseJeffreysRuleForScale)
            {
                for (int i = 0; i < componentCount; i++)
                {
                    if (!TryGetJeffreysScaleParameter(model.Distributions[i], out double scale, out _)) continue;
                    result.Add(new PriorComponent(
                        $"Jeffreys Scale: Component {i + 1}",
                        scale > 0.0 ? -Math.Log(scale) : double.NegativeInfinity,
                        PriorComponentType.JeffreysScalePrior));
                }
            }
            if (EnableQuantilePriors && _quantilePriorsTrue.Count == 1)
            {
                double quantile = model.InverseCDF(1.0 - _quantilePriorsTrue[0].Alpha);
                double logLikelihood = _quantilePriorsTrue[0].Distribution.LogPDF(quantile);
                result.Add(new PriorComponent(
                    $"Quantile Prior: p={_quantilePriorsTrue[0].Alpha:P2}",
                    logLikelihood,
                    PriorComponentType.QuantilePrior));
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
            if (!TryExpandPhysicalParameters(parameters, out double[] physicalParameters))
                throw new ArgumentOutOfRangeException(nameof(parameters), "The mixture weights are outside the feasible physical simplex.");

            var candidate = (Mixture)Mixture!.Clone();
            candidate.SetParameters(physicalParameters);
            if (!candidate.ParametersValid)
                throw new ArgumentOutOfRangeException(nameof(parameters), "The mixture proposal contains invalid component parameters.");

            for (int i = 0; i < parameters.Count; i++)
            {
                Parameters[i].Value = parameters[i];
            }
            Mixture.SetParameters(physicalParameters);
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

            var result = new MixtureModel(DataFrame, Mixture!)
            {
                _isZeroInflated = IsZeroInflated,
                _useDefaultFlatPriors = UseDefaultFlatPriors,
                _useJeffreysRuleForScale = UseJeffreysRuleForScale,
                _enableQuantilePriors = EnableQuantilePriors,
                _useSingleQuantile = true,
                Parameters = parms,
                QuantilePriors = quants,
            };

            result.Mixture!.IsZeroInflated = result._isZeroInflated;
            result.Mixture.ZeroWeight = result._isZeroInflated
                ? Mixture!.ZeroWeight : 0.0;

            result.ProcessQuantilePriors();
            return result;
        }

        /// <inheritdoc/>
        public override XElement ToXElement()
        {
            var result = new XElement(nameof(MixtureModel));
            result.Add(Mixture!.ToXElement());
            result.SetAttributeValue(nameof(IsZeroInflated), IsZeroInflated.ToString());

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

            return result;
        }

        /// <inheritdoc/>
        public override (bool IsValid, List<string> ValidationMessages) Validate()
        {
            bool isValid = true;
            var messages = new List<string>();

            // Data frame
            if (DataFrame is null)
            {
                isValid = false;
                messages.Add("Error: Data frame is null.");
                return (isValid, messages);
            }

            // Mixture distribution
            if (Mixture is null)
            {
                isValid = false;
                messages.Add("Error: Mixture distribution is null.");
                return (isValid, messages);
            }

            // Validate data frame
            var dataValid = DataFrame.Validate();
            if (!dataValid.IsValid)
            {
                isValid = false;
                messages.AddRange(dataValid.ValidationMessages);
            }

            if (Mixture!.Distributions is null || Mixture.Distributions.Length == 0)
            {
                isValid = false;
                messages.Add("Error: Mixture distribution has no component distributions.");
            }

            if (Mixture.Distributions!.Length < 1 || Mixture.Distributions.Length > 3)
            {
                isValid = false;
                messages.Add("Error: Mixture model currently supports 1 to 3 component distributions.");
            }

            // Validate uncertain-data ME bounds before likelihood evaluation. Each uncertain
            // integral is normalized by the retained mass in the 1E-8 probability window.
            if (DataFrame.UncertainSeries is not null)
            {
                foreach (UncertainData data in DataFrame.UncertainSeries!)
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

            // Component distribution type check
            for (int i = 0; i < Mixture.Distributions!.Length; i++)
            {
                if (!IsSupportedDistributionType(Mixture.Distributions[i].Type))
                {
                    isValid = false;
                    messages.Add($"Error: Component distribution {i + 1} has unsupported type '{Mixture.Distributions[i].Type}'.");
                }
            }

            // Zero-inflation checks
            if (IsZeroInflated)
            {
                if (Mixture.ZeroWeight < 0.0 || Mixture.ZeroWeight >= 1.0 ||
                    double.IsNaN(Mixture.ZeroWeight) || double.IsInfinity(Mixture.ZeroWeight))
                {
                    isValid = false;
                    messages.Add("Error: ZeroWeight must be in [0, 1) for a zero-inflated model.");
                }

                ExactData? negativeExact = DataFrame.ExactSeries.Cast<ExactData>().FirstOrDefault(data => data.Value < 0.0);
                if (negativeExact is not null)
                {
                    isValid = false;
                    messages.Add($"Error: Zero-inflated mixtures do not permit negative exact observations; index {negativeExact.Index} has value {negativeExact.Value:R}.");
                }

                for (int i = 0; i < Mixture.Distributions.Length; i++)
                {
                    double positiveMass = Mixture.Distributions[i].CCDF(0.0);
                    if (!Tools.IsFinite(positiveMass) || positiveMass <= 0.0)
                    {
                        isValid = false;
                        messages.Add($"Error: Component distribution {i + 1} must have finite, positive probability above zero.");
                    }
                }
            }
            else if (Mixture.ZeroWeight != 0.0)
            {
                messages.Add("Warning: ZeroWeight is non-zero while IsZeroInflated is false. This will be reset to zero at runtime.");
            }

            // Check log distributions against data and retained ME support.
            if (Mixture.Distributions is not null)
            {
                for (int i = 0; i < Mixture.Distributions.Length; i++)
                {
                    UnivariateDistributionBase distribution = Mixture.Distributions[i];
                    if (distribution.Type != UnivariateDistributionType.LnNormal &&
                        distribution.Type != UnivariateDistributionType.LogNormal &&
                        distribution.Type != UnivariateDistributionType.LogPearsonTypeIII)
                    {
                        continue;
                    }

                    bool hasNonPositiveExact =
                        !IsZeroInflated &&
                        DataFrame.ExactSeries.Count > 0 &&
                        DataFrame.ExactSeries
                            .Where(data => !((ExactData)data).IsLowOutlier)
                            .Any(data => data.Value <= 0.0);

                    bool hasNonPositiveUncertain = false;
                    if (!IsZeroInflated)
                    {
                        foreach (UncertainData data in DataFrame.UncertainSeries!)
                        {
                            double lower = data.Distribution.InverseCDF(1E-8);
                            if (!Tools.IsFinite(lower) || lower <= 0.0)
                            {
                                hasNonPositiveUncertain = true;
                                break;
                            }
                        }
                    }

                    if (hasNonPositiveExact || hasNonPositiveUncertain)
                    {
                        isValid = false;
                        messages.Add($"Error: Component distribution {i + 1} is log-based but data include non-positive values or retained uncertain support.");
                    }
                }
            }

            int expectedParameterCount = Math.Max(0, Mixture.Distributions!.Length - 1) +
                Mixture.Distributions!.Sum(distribution => distribution.NumberOfParameters);
            if (Parameters.Count != expectedParameterCount)
            {
                isValid = false;
                messages.Add($"Error: Mixture parameter vector must contain K-1 free weights and component parameters ({expectedParameterCount} total).");
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
        /// Generates random samples from the mixture distribution. For each sample:
        /// </para>
        /// <list type="number">
        /// <item><description>A component is selected based on the mixing weights.</description></item>
        /// <item><description>A value is drawn from that component's distribution.</description></item>
        /// </list>
        /// <para>
        /// If zero inflation is enabled, some samples may be zero based on the zero-inflation weight.
        /// </para>
        /// </remarks>
        public double[] GenerateRandomValues(int sampleSize, int seed = -1)
        {
            if (sampleSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(sampleSize), "Sample size must be positive.");
            if (Mixture is null)
                throw new InvalidOperationException("Mixture distribution cannot be null when generating random values.");

            return Mixture.GenerateRandomValues(sampleSize, seed);
        }

        #endregion
    }
}
