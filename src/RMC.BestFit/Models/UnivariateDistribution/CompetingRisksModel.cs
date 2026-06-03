using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using Numerics;
using Numerics.Distributions;
using Numerics.Mathematics.Integration;

namespace RMC.BestFit.Models
{

    /// <summary>
    /// Competing risks model for univariate distributions.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    /// The <see cref="CompetingRisksModel"/> represents a set of competing
    /// univariate parent distributions combined through a competing risks
    /// structure (for example, multiple processes that can generate an
    /// annual maximum).
    /// </para>
    /// <para>
    /// It supports censored, interval, threshold, and uncertain data, as well
    /// as Bayesian priors on parameters and a single quantile of the parent
    /// competing risks distribution.
    /// </para>
    /// </remarks>
    public class CompetingRisksModel : UnivariateDistributionModelBase, ISimulatable<double[]>
    {
        #region Construction

        /// <summary>
        /// Constructs an empty competing risks model.
        /// </summary>
        public CompetingRisksModel()
        {
            // Default to a single quantile prior for this model.
            _useSingleQuantile = true;
        }

        /// <summary>
        /// Constructs a competing risks model.
        /// </summary>
        /// <param name="dataFrame">The censored data frame to fit to.</param>
        /// <param name="distributionTypes">The list of univariate distribution types.</param>
        public CompetingRisksModel(DataFrame dataFrame, List<UnivariateDistributionType> distributionTypes)
        {
            if (distributionTypes == null || distributionTypes.Count == 0)
                throw new ArgumentException("At least one distribution type is required.", nameof(distributionTypes));

            if (distributionTypes.Count > 3)
                throw new ArgumentException("There cannot be more than three distributions in the competing risks model.", nameof(distributionTypes));

            _useSingleQuantile = true;
            DataFrame = dataFrame;

            DataFrame.ProcessThresholdSeries();

            var distributions = new List<UnivariateDistributionBase>();
            for (int i = 0; i < distributionTypes.Count; i++)
            {
                distributions.Add(UnivariateDistribution.CreateDistribution(distributionTypes[i]));
            }

            CompetingRisks = new CompetingRisks(distributions.ToArray());
        }

        /// <summary>
        /// Constructs a competing risks model from an existing
        /// <see cref="CompetingRisks"/> distribution.
        /// </summary>
        /// <param name="dataFrame">The censored data frame to fit to.</param>
        /// <param name="distribution">The competing risks distribution.</param>
        public CompetingRisksModel(DataFrame dataFrame, CompetingRisks distribution)
        {
            if (distribution is null)
                throw new ArgumentNullException(nameof(distribution));

            _useSingleQuantile = true;
            DataFrame = dataFrame;
            CompetingRisks = (CompetingRisks)distribution.Clone();
        }

        /// <summary>
        /// Constructs a competing risks model from a serialized <see cref="XElement"/>.
        /// </summary>
        /// <remarks>
        /// Pairs with <see cref="ToXElement"/>. The data frame and the
        /// <see cref="CompetingRisks"/> distribution are passed in by the caller (the
        /// data is the single source of truth, and the distribution carries its own
        /// serialization schema). Scalar configuration (flags, parameter values,
        /// quantile priors) is read from the XElement.
        /// </remarks>
        /// <param name="dataFrame">The censored data frame.</param>
        /// <param name="distribution">The competing risks distribution (already deserialized).</param>
        /// <param name="xElement">The serialized configuration.</param>
        public CompetingRisksModel(DataFrame dataFrame, CompetingRisks distribution, XElement xElement)
            : this(dataFrame, distribution)
        {
            if (xElement == null) return;

            var useDefaultFlatPriorsAttr = xElement.Attribute(nameof(UseDefaultFlatPriors));
            if (useDefaultFlatPriorsAttr != null && bool.TryParse(useDefaultFlatPriorsAttr.Value, out var udfp))
                UseDefaultFlatPriors = udfp;

            var useJeffreysAttr = xElement.Attribute(nameof(UseJeffreysRuleForScale));
            if (useJeffreysAttr != null && bool.TryParse(useJeffreysAttr.Value, out var ujr))
                UseJeffreysRuleForScale = ujr;

            var enableQpAttr = xElement.Attribute(nameof(EnableQuantilePriors));
            if (enableQpAttr != null && bool.TryParse(enableQpAttr.Value, out var eqp))
                EnableQuantilePriors = eqp;

            var useSingleQAttr = xElement.Attribute(nameof(UseSingleQuantile));
            if (useSingleQAttr != null && bool.TryParse(useSingleQAttr.Value, out var usq))
                _useSingleQuantile = usq;

            // Restore parameter values (bounds and priors are set by SetDefaultParameters
            // via the chained ctor; only Value needs to be reapplied from XML).
            var parmsElem = xElement.Element(nameof(Parameters));
            if (parmsElem != null)
            {
                var paramElems = parmsElem.Elements().ToList();
                int n = Math.Min(paramElems.Count, Parameters.Count);
                for (int i = 0; i < n; i++)
                {
                    var valueAttr = paramElems[i].Attribute(nameof(ModelParameter.Value));
                    if (valueAttr != null && double.TryParse(valueAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
                        Parameters[i].Value = v;
                }
            }

            // Restore quantile priors
            var quantsElem = xElement.Element(nameof(QuantilePriors));
            if (quantsElem != null)
            {
                QuantilePriors.Clear();
                foreach (var qp in quantsElem.Elements())
                {
                    QuantilePriors.Add(new QuantilePrior(qp));
                }
            }
        }

        #endregion

        #region Members

        /// <summary>
        /// The set of univariate distribution types supported by the competing risks model.
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
        /// Determines whether the specified distribution type is supported by the competing risks model.
        /// </summary>
        /// <param name="distributionType">The distribution type to check.</param>
        /// <returns><c>true</c> if the distribution type is supported; otherwise, <c>false</c>.</returns>
        public static bool IsSupportedDistributionType(UnivariateDistributionType distributionType)
        {
            return _supportedDistributionTypes.Contains(distributionType);
        }

        private CompetingRisks? _competingRisks = null;

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

                    if (UseDefaultFlatPriors)
                        SetDefaultParameters();
                }

                RaisePropertyChange(nameof(DataFrame));
            }
        }

        /// <summary>
        /// Gets or sets the competing risks distribution representing the parent population.
        /// </summary>
        public CompetingRisks? CompetingRisks
        {
            get { return _competingRisks; }
            set
            {
                _competingRisks = value;
                RaisePropertyChange(nameof(CompetingRisks));
                SetDefaultParameters();
                SetDefaultQuantilePriors();
            }
        }

        /// <summary>
        /// Competing risks model currently supports a single quantile prior.
        /// Attempts to disable this are ignored.
        /// </summary>
        [Category("Inputs")]
        [DisplayName("Use Single Quantile")]
        [Description("Competing risks model currently supports only a single quantile prior.")]
        [Browsable(true)]
        public override bool UseSingleQuantile
        {
            get { return true; }
            set
            {
                // Model is restricted to a single quantile prior.
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

            if (UseDefaultFlatPriors)
                SetDefaultParameters();
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

            if (CompetingRisks is null ||
                DataFrame is null ||
                !DataFrame.Validate().IsValid ||
                CompetingRisks.Distributions == null ||
                CompetingRisks.Distributions.Count == 0)
            {
                RaisePropertyChange(nameof(SetDefaultParameters));
                return;
            }

            // Set the list of model parameters.
            for (int i = 0; i < CompetingRisks.Distributions.Count; i++)
            {
                // Get constraints
                var tuple = ((IMaximumLikelihoodEstimation)CompetingRisks.Distributions[i]).GetParameterConstraints(DataFrame.ExactSeries.Select(x => x.Value).ToList());
                var initials = tuple.Item1;
                var lowers = tuple.Item2;
                var uppers = tuple.Item3;

                var parametersToString = CompetingRisks.Distributions[i].ParametersToString;
                for (int j = 0; j < CompetingRisks.Distributions[i].NumberOfParameters; j++)
                {
                    Parameters.Add(new ModelParameter()
                    {
                        OwnerName = "D" + (i + 1).ToString(CultureInfo.InvariantCulture),
                        Name = parametersToString[j, 0],
                        Value = initials[j],
                        LowerBound = lowers[j],
                        UpperBound = uppers[j],
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
            if (CompetingRisks is null)
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
            int qCount = 1; // competing-risks model uses a single quantile prior

            if (QuantilePriors.Count >= 1)
            {
                priors = QuantilePriors.ToList();

                while (priors.Count > qCount)
                    priors.Remove(priors.Last());

                while (priors.Count < qCount)
                {
                    priors.Add(new QuantilePrior(priors.Last().Alpha / 10.0, new LnNormal()));
                    double mu = Math.Round(CompetingRisks.InverseCDF(1 - priors.Last().Alpha), 2);
                    double sigma = Math.Round(mu * 0.15, 2);
                    priors.Last().Distribution.SetParameters(new[] { mu, sigma });
                }
            }
            else
            {
                for (int i = 1; i <= qCount; i++)
                {
                    priors.Add(new QuantilePrior(Math.Pow(10, -i), new LnNormal()));
                    double mu = Math.Round(CompetingRisks.InverseCDF(1 - priors[i - 1].Alpha), 2);
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
                CompetingRisks is not null &&
                QuantilePriors.Count == CompetingRisks.NumberOfParameters)
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
            if (CompetingRisks is null || DataFrame is null)
                return double.NegativeInfinity;

            var model = (CompetingRisks)CompetingRisks.Clone();
            int k = model.Distributions.Count;
            double logLH = 0.0;

            // Set model parameter
            model.SetParameters(parameters);

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
        public override double[] PointwiseDataLogLikelihood(double[] parameters)
        {
            if (CompetingRisks is null || DataFrame is null)
                return Array.Empty<double>();

            var model = (CompetingRisks)CompetingRisks.Clone();

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
            if (CompetingRisks is null || DataFrame is null)
                return new List<DataComponent>();

            var model = (CompetingRisks)CompetingRisks.Clone();
            model.SetParameters(parameters);

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
            if (CompetingRisks is null || Parameters == null)
                return double.NegativeInfinity;

            var model = (CompetingRisks)CompetingRisks.Clone();
            int k = model.Distributions.Count;
            double logLH = 0.0;

            // Set model parameter
            model.SetParameters(parameters);

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

            if (CompetingRisks is null || Parameters == null)
                return result;

            var model = (CompetingRisks)CompetingRisks.Clone();
            int k = model.Distributions.Count;

            // Set model parameters
            model.SetParameters(parameters);

            // Parameter Priors
            for (int i = 0; i < Parameters.Count; i++)
            {
                double ll = Parameters[i].PriorDistribution.LogPDF(parameters[i]);
                string paramName = string.IsNullOrEmpty(Parameters[i].OwnerName) ? Parameters[i].Name : Parameters[i].OwnerName;
                result.Add(new PriorComponent($"Parameter Prior: {paramName}", ll, PriorComponentType.ParameterPrior));
            }

            // Jeffreys rule on scale parameters for each component
            if (UseJeffreysRuleForScale)
            {
                for (int j = 0; j < k; j++)
                {
                    var dist = model.Distributions[j];
                    double scale;
                    string scaleName;

                    if (dist.Type == UnivariateDistributionType.GammaDistribution ||
                        dist.Type == UnivariateDistributionType.Weibull)
                    {
                        scale = dist.GetParameters[0];
                        scaleName = dist.ParameterNames[0];
                    }
                    else
                    {
                        scale = dist.GetParameters[1];
                        scaleName = dist.ParameterNames[1];
                    }
                    double ll = scale > 0 ? -Math.Log(scale) : double.NegativeInfinity;
                    result.Add(new PriorComponent($"Jeffreys Scale: D{j + 1}.{scaleName}", ll, PriorComponentType.JeffreysScalePrior));
                }
            }

            // Single quantile prior
            if (EnableQuantilePriors && _quantilePriorsTrue.Count == 1)
            {
                double quantile = model.InverseCDF(1 - _quantilePriorsTrue[0].Alpha);
                double ll = _quantilePriorsTrue[0].Distribution.LogPDF(quantile);
                result.Add(new PriorComponent($"Quantile Prior: p={_quantilePriorsTrue[0].Alpha:G4}", ll, PriorComponentType.QuantilePrior));
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

            double[] parmsCopy = parameters.ToArray();
            CompetingRisks!.SetParameters(parmsCopy);
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

            var result = new CompetingRisksModel(DataFrame, CompetingRisks!)
            {
                _useDefaultFlatPriors = UseDefaultFlatPriors,
                _useJeffreysRuleForScale = UseJeffreysRuleForScale,
                _enableQuantilePriors = EnableQuantilePriors,
                _useSingleQuantile = true,
                Parameters = parms,
                QuantilePriors = quants
            };

            result.ProcessQuantilePriors();
            return result;
        }

        /// <inheritdoc/>
        public override XElement ToXElement()
        {
            var result = new XElement(nameof(CompetingRisksModel));

            if (CompetingRisks is not null)
                result.Add(CompetingRisks.ToXElement());

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

            // Data frame
            if (DataFrame is null)
            {
                isValid = false;
                messages.Add("Error: Data frame is null.");
                return (isValid, messages);
            }

            // Competing risks distribution
            if (CompetingRisks is null)
            {
                isValid = false;
                messages.Add("Error: Competing risks distribution is null.");
                return (isValid, messages);
            }

            // Validate data frame
            var dataValid = DataFrame.Validate();
            if (!dataValid.IsValid)
            {
                isValid = false;
                messages.AddRange(dataValid.ValidationMessages);
            }

            if (CompetingRisks!.Distributions == null || CompetingRisks.Distributions.Count == 0)
            {
                isValid = false;
                messages.Add("Error: Competing risks distribution has no component distributions.");
            }

            if (CompetingRisks.Distributions!.Count > 3)
            {
                isValid = false;
                messages.Add("Error: Competing risks model currently supports at most 3 component distributions.");
            }

            // Validate uncertain-data ME bounds before likelihood evaluation. The uncertain
            // contribution is normalized by retained probability mass over this 1E-8 window.
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
            for (int i = 0; i < CompetingRisks.Distributions!.Count; i++)
            {
                if (!IsSupportedDistributionType(CompetingRisks.Distributions[i].Type))
                {
                    isValid = false;
                    messages.Add($"Error: Component distribution {i + 1} has unsupported type '{CompetingRisks.Distributions[i].Type}'.");
                }

                if (CompetingRisks.Distributions[i].Type == UnivariateDistributionType.LnNormal ||
                    CompetingRisks.Distributions[i].Type == UnivariateDistributionType.LogNormal ||
                    CompetingRisks.Distributions[i].Type == UnivariateDistributionType.LogPearsonTypeIII)
                {
                    foreach (UncertainData data in DataFrame.UncertainSeries!)
                    {
                        var dist = data.Distribution;
                        double lowerProbability = 1E-8;
                        double lower = dist.InverseCDF(lowerProbability);

                        // Log components require positive retained ME support; checking the
                        // ME mean alone can miss a negative lower tail.
                        if (!Tools.IsFinite(lower) || lower <= 0.0)
                        {
                            isValid = false;
                            messages.Add($"Error: Component distribution {i + 1} is log-based but uncertain data at index {data.Index} has non-positive retained support.");
                        }
                    }
                }
            }

            // Parameter priors
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
        /// Generates random samples from the competing risks distribution. For each sample:
        /// </para>
        /// <list type="number">
        /// <item><description>A value is drawn from each component distribution.</description></item>
        /// <item><description>The minimum (or maximum, depending on configuration) is taken.</description></item>
        /// </list>
        /// </remarks>
        public double[] GenerateRandomValues(int sampleSize, int seed = -1)
        {
            if (sampleSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(sampleSize), "Sample size must be positive.");
            if (CompetingRisks is null)
                throw new InvalidOperationException("CompetingRisks distribution cannot be null when generating random values.");

            return CompetingRisks.GenerateRandomValues(sampleSize, seed);         
        }

        #endregion
    }
}
