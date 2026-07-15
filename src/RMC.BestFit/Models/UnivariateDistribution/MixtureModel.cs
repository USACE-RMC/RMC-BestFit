using System.ComponentModel;
using System.Globalization;
using System.Xml.Linq;
using Numerics;
using Numerics.Distributions;
using Numerics.Mathematics;
using Numerics.Mathematics.Integration;
using Numerics.Mathematics.LinearAlgebra;
using Numerics.Mathematics.Optimization;
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
    /// <item><description>Zero inflation for values less than or equal to zero.</description></item>
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
        /// If true, assigns a weight to all data points less than or equal to zero.
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
        /// If true, assigns a weight to all data points less than or equal to zero.
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
                            Mixture.ZeroWeight = _dataFrame.ZeroValueRelativeFrequency();
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
                        _mixture.ZeroWeight = DataFrame is not null ? DataFrame.ZeroValueRelativeFrequency() : 0.0;
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
        /// Determines whether a zero-inflated weight is assigned to values less than or equal to zero.
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
                            Mixture.ZeroWeight = DataFrame != null ? DataFrame.ZeroValueRelativeFrequency() : 0.0;
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
                    Mixture.ZeroWeight = DataFrame.ZeroValueRelativeFrequency();
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

            // Remove old handlers
            if (Parameters.Count > 0)
            {
                for (int i = 0; i < NumberOfParameters; i++)
                    Parameters[i].PropertyChanged -= Parameter_PropertyChanged;
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

            // Priors for mixture weights
            int k = Mixture.Distributions.Length;
            if (k > 1)
            {
                double w0 = 1d / k;
                for (int i = 0; i < k; i++)
                {
                    Parameters.Add(new ModelParameter
                    {
                        Name = "Weight (w" + SubscriptFormatter.ToSubscript(i + 1) + ")",
                        Value = w0,
                        LowerBound = 0,
                        UpperBound = 1,
                        PriorDistribution = new Uniform(0, 1)
                    });
                }
            }

            // Priors for distribution parameters
            for (int i = 0; i < k; i++)
            {
                var component = Mixture.Distributions[i];

                var tuple = ((IMaximumLikelihoodEstimation)component).GetParameterConstraints(DataFrame.ExactSeries.Select(x => x.Value).ToList());
                var initials = tuple.Item1;
                var lowers = tuple.Item2;
                var uppers = tuple.Item3;

                var parametersToString = component.ParametersToString;
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

            // Add handlers
            for (int i = 0; i < NumberOfParameters; i++)
                Parameters[i].PropertyChanged += Parameter_PropertyChanged;

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
        /// <param name="parameters">The estimated MLE parameters (weights then component parameters).</param>
        /// <param name="covariance">The approximate covariance matrix of the parameters.</param>
        /// <param name="iterations">The number of EM iterations performed.</param>
        /// <param name="maxIterations">Maximum EM iterations (default 1000).</param>
        /// <param name="tolerance">Relative tolerance for convergence (default 1E-8).</param>
        public void ExpectationMaximization(out double[] parameters, out double[,] covariance, out int iterations, int maxIterations = 1000, double tolerance = 1E-8)
        {

            parameters = Array.Empty<double>();
            covariance = new double[0, 0];
            iterations = 0;

            if (Mixture is null || DataFrame is null)
                return;

            var model = (Mixture)Mixture.Clone();

            DataFrame.CreateFullTimeSeries();
            int N = DataFrame.TotalRecordLength();
            int Np = model.Distributions.Sum(x => x.NumberOfParameters);
            int K = model.Distributions.Count();

            if (N == 0 || K == 0)
                return;

            // Get constraints 
            var tuple = model.GetParameterConstraints(DataFrame.ExactSeries.Select(x => x.Value).ToList());
            var initialValues = tuple.Item1.Subset(K);
            var lowerBounds = tuple.Item2.Subset(K);
            var upperBounds = tuple.Item3.Subset(K);

            // Initialize EM state
            var mleWeights = tuple.Item1.Subset(0, K - 1);
            var mleParameters = initialValues;
            var likelihood = new double[N, K];

            double oldLogLH = double.NegativeInfinity;
            double newLogLH = double.NegativeInfinity;

            // E-step: compute responsibilities and log-likelihood
            double EStep(double[] x)
            {
                // Set mixture parameters
                model.SetParameters(mleWeights, x);

                // Outer loop for computing the likelihoods
                for (int k = 0; k < K; k++)
                {
                    for (int i = 0; i < DataFrame.FullTimeSeries.Count; i++)
                    {
                        var data = DataFrame.FullTimeSeries[i];

                        // Exact Data
                        if (data is ExactData exact)
                        {
                            if (!exact.IsLowOutlier)
                            {
                                if (model.IsZeroInflated && data.Value <= 0.0)
                                {
                                    likelihood[i, k] = Math.Log(model.ZeroWeight);
                                }
                                else
                                {
                                    likelihood[i, k] = Math.Log(mleWeights[k]) + model.Distributions[k].LogPDF(data.Value);
                                }
                            }
                            else
                            {
                                // Low outliers are treated as left censored
                                likelihood[i, k] = Math.Log(mleWeights[k]) + model.Distributions[k].LogCDF(DataFrame.LowOutlierThreshold);
                            }
                        }
                        // Uncertain Data
                        else if (data is UncertainData uncertain)
                        {
                            var dist = uncertain.Distribution;
                            double lowerProbability = 1E-8;
                            double upperProbability = 1.0 - 1E-8;
                            var a = dist.InverseCDF(lowerProbability);
                            var b = dist.InverseCDF(upperProbability);
                            double mass = upperProbability - lowerProbability;
                            if (Tools.IsFinite(a) && Tools.IsFinite(b) && Tools.IsFinite(mass) && mass > 0.0 && a < b && mleWeights[k] > 0)
                            {
                                var ep = Integration.GaussLegendre20((q) => { return dist.PDF(q) * model.Distributions[k].PDF(q); }, a, b) / mass;
                                likelihood[i, k] = Math.Log(mleWeights[k]) + (ep > 0 ? Math.Log(ep) : double.NegativeInfinity);
                            }
                            else
                            {
                                likelihood[i, k] = double.NegativeInfinity;
                            }
                        }
                        // interval data
                        else if (data is IntervalData interval)
                        {
                            // DataFrame validates LowerValue < UpperValue at construction,
                            // but defend against numerical near-equality producing CDF
                            // differences <= 0 by guarding the log argument.
                            double cdfDiff = model.Distributions[k].CDF(interval.UpperValue) - model.Distributions[k].CDF(interval.LowerValue);
                            likelihood[i, k] = cdfDiff > 0
                                ? Math.Log(mleWeights[k]) + Math.Log(cdfDiff)
                                : double.NegativeInfinity;
                        }
                        // threshold data
                        // Invariant from DataFrame.CreateFullTimeSeries: each
                        // threshold ordinate in the expanded series carries either
                        // NumberBelow == 1 OR NumberAbove == 1, never both. The
                        // two branches below are exhaustive for valid input; the
                        // -∞ fall-through guards against malformed data and is
                        // not a "dropped observation" path.
                        else if (data is ThresholdData threshold)
                        {
                            if (threshold.NumberBelow == 1 && threshold.NumberAbove == 0)
                            {
                                likelihood[i, k] = Math.Log(mleWeights[k]) + model.Distributions[k].LogCDF(threshold.Value);
                            }
                            else if (threshold.NumberBelow == 0 && threshold.NumberAbove == 1)
                            {
                                likelihood[i, k] = Math.Log(mleWeights[k]) + model.Distributions[k].LogCCDF(threshold.Value);
                            }
                            else
                            {
                                likelihood[i, k] = double.NegativeInfinity;
                            }
                        }
                    }
                }
                // At this point we have unnormalized log likelihoods.
                // We need to normalize using log-sum-exp and compute the true log-likelihoods.
                double lh = 0.0;
                for (int i = 0; i < N; i++)
                {
                    // Get max likelihood
                    double max = double.NegativeInfinity;
                    for (int k = 0; k < K; k++)
                    {
                        if (likelihood[i, k] > max)
                        {
                            max = likelihood[i, k];
                        }
                    }

                    if (!Tools.IsFinite(max) || max == double.NegativeInfinity)
                    {
                        continue;
                    } // sum <= 0 is excluded above

                    // log-sum-exp trick begins here
                    double sum = 0;
                    for (int k = 0; k < K; k++)
                        sum += Math.Exp(likelihood[i, k] - max);

                    if (sum <=0)
                    {
                        continue;
                    }   

                    double tmp = max + Math.Log(sum);
                    lh += tmp;

                    for (int k = 0; k < K; k++)
                    {
                        likelihood[i, k] = Math.Exp(likelihood[i, k] - tmp);
                        // After normalization, likelihood is a probability in [0,1]
                        // If not finite, set to 0 (not MinValue which is a large negative number)
                        if (!Tools.IsFinite(likelihood[i, k]))
                            likelihood[i, k] = 0.0;
                    }

                }

                if (!Tools.IsFinite(lh)) return double.NegativeInfinity;
                return lh;

            }

            // M-step: The Maximization step (update weights and continuous parameters)
            double[] MStep(double[] x)
            {
                // Get updated weights
                for (int k = 0; k < K; k++)
                {
                    double wgt = 0d;
                    for (int i = 0; i < DataFrame.FullTimeSeries.Count; i++)
                    {
                        var data = DataFrame.FullTimeSeries[i];
                        if (!IsZeroInflated || data.Value > 0.0)
                        {
                            wgt += likelihood[i, k];
                        }
                    }
                    mleWeights[k] = wgt / N;
                }
                // MLE
                var solver = new NelderMead(logLH, Np, x, lowerBounds, upperBounds);
                solver.Maximize();
                return solver.BestParameterSet.Values;
            }

            // The log-likelihood function to maximize in the M-Step.
            // Weights are held fixed, only the distribution parameters are solved.
            double logLH(double[] x)
            {
                // Set mixture parameters
                model.SetParameters(mleWeights, x);

                double lh = 0;
                // Compute the data likelihood
                // Exact Data
                for (int i = 0; i < DataFrame.ExactSeries.Count; i++)
                {
                    var data = (ExactData)DataFrame.ExactSeries[i];
                    if (data.IsLowOutlier == false)
                    {
                        lh += model.LogLikelihood(data.Value);
                    }
                    else
                    {
                        // Low outliers are treated as left censored
                        lh += model.LogLikelihood_LeftCensored(DataFrame.LowOutlierThreshold, 1);
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
                        // Normalize by retained ME mass so truncated 1E-8 tails do not
                        // damp the uncertain likelihood contribution.
                        var ep = Integration.GaussLegendre20((q) => { return dist.PDF(q) * model.PDF(q); }, a, b) / mass;
                        lh += ep > 0 ? Math.Log(ep) : double.NegativeInfinity;
                    }
                    else
                    {
                        lh += double.NegativeInfinity;
                    }
                }

                // Interval Data
                for (int i = 0; i < DataFrame.IntervalSeries.Count; i++)
                {
                    var data = (IntervalData)DataFrame.IntervalSeries[i];
                    lh += model.LogLikelihood_Intervals(data.LowerValue, data.UpperValue);
                }

                // Threshold Data
                for (int i = 0; i < DataFrame.ThresholdSeries.Count; i++)
                {
                    var data = (ThresholdData)DataFrame.ThresholdSeries[i];
                    if (data.NumberBelow > 0)
                        lh += model.LogLikelihood_LeftCensored(data.Value, data.NumberBelow);
                    if (data.NumberAbove > 0)
                        lh += model.LogLikelihood_RightCensored(data.Value, data.NumberAbove);
                }

                // Return the full log-likelihood 
                if (!Tools.IsFinite(lh)) return double.NegativeInfinity;
                return lh;
            }

            // Estimate using the EM Algorithm
            for (iterations = 1; iterations <= maxIterations; iterations++)
            {
                // Perform the expectation step
                newLogLH = EStep(mleParameters);

                // Check convergence
                if (Tools.IsFinite(oldLogLH))
                {
                    if (Math.Abs((oldLogLH - newLogLH) / oldLogLH) < tolerance)
                        break;
                }

                // Perform the maximization step
                mleParameters = MStep(mleParameters);

                // Update log-likelihood state
                oldLogLH = newLogLH;

            }

            // Return the full list of distribution parameters
            var result = new List<double>();
            result.AddRange(mleWeights);
            result.AddRange(mleParameters);
            parameters = result.ToArray();

            // Estimate the covariance matrix of parameters
            // Get Hessian and invert it to get the covariance matrix
            var hessian = NumericalDiff.ComputeHessian((x) => { return EStep(x); }, mleParameters, mleParameters.Length);
            Matrix fisher = hessian * -1;
            fisher = fisher.Inverse();
            covariance = new double[parameters.Length, parameters.Length];
            // Mixing weights covariance (multinomial approximation)
            for (int i = 0; i < K; i++)
            {
                double wi = mleWeights[i];
                covariance[i, i] = wi * (1.0 - wi) / N;

                for (int j = 0; j < K; j++)
                {
                    if (i == j) continue;

                    double wj = mleWeights[j];
                    // Equicorrelation rho between MLE Dirichlet weights. Add a tiny
                    // epsilon nudge so the resulting covariance matrix is strictly
                    // positive-definite for downstream Cholesky. Guard K==1 to avoid
                    // division by zero (single-component model has no off-diagonal).
                    if (K <= 1) { covariance[i, j] = 0.0; continue; }
                    double rho = -1.0 / (K - 1.0) + Tools.DoubleMachineEpsilon;
                    double vari = wi * (1.0 - wi) / N;
                    double varj = wj * (1.0 - wj) / N;
                    covariance[i, j] = rho * Math.Sqrt(vari * varj);
                }
            }
            // Component parameter covariance from Fisher information
            for (int i = 0; i < fisher.NumberOfRows; i++)
            {
                for (int j = 0; j < fisher.NumberOfColumns; j++)
                {
                    covariance[i + K, j + K] = fisher[i, j];
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
            if (Mixture is null || DataFrame is null)
                return double.NegativeInfinity;

            var model = (Mixture)Mixture.Clone();
            int k = model.Distributions.Length;
            double logLH = 0.0;

            // Set model parameter
            model.SetParameters(ref parameters);

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
                if (Tools.IsFinite(a) && Tools.IsFinite(b) && Tools.IsFinite(mass) && mass > 0.0 && a < b)
                {
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
        public override double[] PointwiseDataLogLikelihood(double[] parameters)
        {
            if (Mixture is null || DataFrame is null)
                return Array.Empty<double>();

            var model = (Mixture)Mixture.Clone();

            // Set model parameters
            var parmsCopy = parameters.ToArray();
            model.SetParameters(ref parmsCopy);

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
                    var ep = Integration.GaussLegendre20((q) => { return dist.PDF(q) * model.PDF(q); }, a, b) / mass;
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

            // Threshold Data
            for (int i = 0; i < DataFrame.ThresholdSeries.Count; i++)
            {
                var data = (ThresholdData)DataFrame.ThresholdSeries[i];
                // Combine left and right censored contributions for this threshold
                double ll = 0.0;
                if (data.NumberBelow > 0)
                    ll += model.LogLikelihood_LeftCensored(data.Value, data.NumberBelow);
                if (data.NumberAbove > 0)
                    ll += model.LogLikelihood_RightCensored(data.Value, data.NumberAbove);
                result.Add(ll);
            }

            return result.ToArray();
        }

        /// <inheritdoc/>
        public override List<DataComponent> PointwiseDataLogLikelihoodComponents(double[] parameters)
        {
            if (Mixture is null || DataFrame is null)
                return new List<DataComponent>();

            var model = (Mixture)Mixture.Clone();
            var parmsCopy = parameters.ToArray();
            model.SetParameters(ref parmsCopy);

            var result = new List<DataComponent>();
            int idx = 0;

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
                    var ep = Integration.GaussLegendre20((q) => { return dist.PDF(q) * model.PDF(q); }, a, b) / mass;
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
                double ll = 0.0;
                if (data.NumberBelow > 0)
                    ll += model.LogLikelihood_LeftCensored(data.Value, data.NumberBelow);
                if (data.NumberAbove > 0)
                    ll += model.LogLikelihood_RightCensored(data.Value, data.NumberAbove);

                int totalCount = data.NumberBelow + data.NumberAbove;
                result.Add(new DataComponent(idx++, ll, data.Value, DataComponentType.LeftCensored, totalCount, $"Threshold {i + 1}"));
            }

            return result;
        }

        /// <inheritdoc/>
        public override double PriorLogLikelihood(double[] parameters)
        {
            if (Mixture is null || Parameters is null)
                return double.NegativeInfinity;

            var model = (Mixture)Mixture.Clone();
            int k = model.Distributions.Length;
            double logLH = 0.0;

            // Set model parameter
            model.SetParameters(ref parameters);

            // Parameter Priors
            for (int i = 0; i < Parameters.Count; i++)
            {
                logLH += Parameters[i].PriorDistribution.LogPDF(parameters[i]);
            }

            // Jeffreys rule on scale parameters for each component
            if (UseJeffreysRuleForScale)
            {
                for (int j = 0; j < k; j++)
                {
                    var dist = model.Distributions[j];
                    double scale;

                    if (dist.Type == UnivariateDistributionType.GammaDistribution ||
                        dist.Type == UnivariateDistributionType.Weibull)
                    {
                        scale = dist.GetParameters[0];
                    }
                    else
                    {
                        scale = dist.GetParameters[1];
                    }
                    logLH -= scale > 0 ? Math.Log(scale) : double.PositiveInfinity;
                }
            }

            // Single quantile prior
            if (EnableQuantilePriors == true && _quantilePriorsTrue.Count == 1)
            {
                logLH += _quantilePriorsTrue[0].Distribution.LogPDF(model.InverseCDF(1 - _quantilePriorsTrue[0].Alpha));
            }

            return logLH;
        }

        /// <inheritdoc/>
        public override List<PriorComponent> PointwisePriorLogLikelihood(double[] parameters)
        {
            var result = new List<PriorComponent>();

            if (Mixture is null || Parameters is null)
                return result;

            var model = (Mixture)Mixture.Clone();
            int k = model.Distributions.Length;

            // Set model parameters
            var parmsCopy = parameters.ToArray();
            model.SetParameters(ref parmsCopy);

            // Parameter Priors
            for (int i = 0; i < Parameters.Count; i++)
            {
                double ll = Parameters[i].PriorDistribution.LogPDF(parameters[i]);
                string paramName = string.IsNullOrEmpty(Parameters[i].OwnerName) ? Parameters[i].Name : Parameters[i].OwnerName;
                result.Add(new PriorComponent(
                    $"Parameter Prior: {paramName}",
                    ll,
                    PriorComponentType.ParameterPrior));
            }

            // Jeffreys rule on scale parameters for each component
            if (UseJeffreysRuleForScale)
            {
                for (int j = 0; j < k; j++)
                {
                    var dist = model.Distributions[j];
                    double scale;

                    if (dist.Type == UnivariateDistributionType.GammaDistribution ||
                        dist.Type == UnivariateDistributionType.Weibull)
                    {
                        scale = dist.GetParameters[0];
                    }
                    else
                    {
                        scale = dist.GetParameters[1];
                    }

                    result.Add(new PriorComponent(
                        $"Jeffreys Scale: Component {j + 1}",
                        scale > 0 ? -Math.Log(scale) : double.NegativeInfinity,
                        PriorComponentType.JeffreysScalePrior));
                }
            }

            // Single quantile prior
            if (EnableQuantilePriors && _quantilePriorsTrue.Count == 1)
            {
                double quantile = model.InverseCDF(1 - _quantilePriorsTrue[0].Alpha);
                double ll = _quantilePriorsTrue[0].Distribution.LogPDF(quantile);
                result.Add(new PriorComponent(
                    $"Quantile Prior: p={_quantilePriorsTrue[0].Alpha:P2}",
                    ll,
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

            for (int i = 0; i < parameters.Count; i++)
                Parameters[i].Value = parameters[i];

            var parmsCopy = parameters.ToArray();
            Mixture!.SetParameters(ref parmsCopy);
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
            }
            else
            {
                if (Mixture.ZeroWeight != 0.0)
                {
                    messages.Add("Warning: ZeroWeight is non-zero while IsZeroInflated is false. This will be reset to zero at runtime.");
                }
            }

            // Check log distributions against data and retained ME support.
            if (Mixture.Distributions is not null)
            {
                for (int i = 0; i < Mixture.Distributions.Length; i++)
                {
                    var dist = Mixture.Distributions[i];

                    if (dist.Type == UnivariateDistributionType.LnNormal ||
                        dist.Type == UnivariateDistributionType.LogNormal ||
                        dist.Type == UnivariateDistributionType.LogPearsonTypeIII)
                    {
                        bool hasNonPositiveExact =
                            !IsZeroInflated &&
                            DataFrame.ExactSeries.Count > 0 &&
                             DataFrame.ExactSeries
                                 .Where(x => !((ExactData)x).IsLowOutlier)
                                  .Any(y => y.Value <= 0.0);

                        bool hasNonPositiveUncertain = false;
                        foreach (UncertainData data in DataFrame.UncertainSeries!)
                        {
                            var meDist = data.Distribution;
                            double lowerProbability = 1E-8;
                            double lower = meDist.InverseCDF(lowerProbability);

                            // Zero inflation can account for exact zeros, but it cannot make a
                            // log component valid over negative retained ME support.
                            if (!Tools.IsFinite(lower) || lower <= 0.0)
                            {
                                hasNonPositiveUncertain = true;
                                break;
                            }
                        }

                        if (hasNonPositiveExact || hasNonPositiveUncertain)
                        {
                            isValid = false;
                            messages.Add($"Error: Component distribution {i + 1} is log-based but data include non-positive values or retained uncertain support.");
                        }
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
