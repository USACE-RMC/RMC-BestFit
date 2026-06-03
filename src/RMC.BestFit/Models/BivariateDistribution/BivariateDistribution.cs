using Numerics;
using Numerics.Distributions;
using Numerics.Distributions.Copulas;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;

namespace RMC.BestFit.Models
{
    /// <summary>
    /// Represents a bivariate joint distribution model constructed from
    /// two marginal univariate distributions and a copula that describes
    /// the dependence structure between them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Authors:
    /// Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// </remarks>
    public class BivariateDistribution : ModelBase, ISimulatable<double[,]>
    {

        #region Construction

        /// <summary>
        /// Initializes a new instance of the <see cref="BivariateDistribution"/> class
        /// with a default normal copula and no marginals.
        /// </summary>
        public BivariateDistribution()
        {
            Copula = CreateCopula(CopulaType.Normal);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BivariateDistribution"/> class.
        /// </summary>
        /// <param name="marginalX">Marginal distribution model for variable X.</param>
        /// <param name="marginalY">Marginal distribution model for variable Y.</param>
        /// <param name="copulaType">The copula type used to model dependence.</param>
        public BivariateDistribution(IUnivariateModel marginalX, IUnivariateModel marginalY, CopulaType copulaType)
        {
            // Set inputs
            MarginalX = marginalX;
            MarginalY = marginalY;
            Copula = CreateCopula(copulaType);
            SetDefaultParameters();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BivariateDistribution"/> class
        /// from an XML element.
        /// </summary>
        /// <param name="marginalX">Marginal distribution model for variable X.</param>
        /// <param name="marginalY">Marginal distribution model for variable Y.</param>
        /// <param name="xElement">The <see cref="XElement"/> containing the serialized state.</param>
        public BivariateDistribution(IUnivariateModel marginalX, IUnivariateModel marginalY, XElement xElement)
        {
            // Set marginals
            MarginalX = marginalX;
            MarginalY = marginalY;

            // Parameters
            if (xElement.Attribute(nameof(UseDefaultFlatPriors)) != null) bool.TryParse(xElement.Attribute(nameof(UseDefaultFlatPriors))!.Value, out _useDefaultFlatPriors);
            CopulaType copulaType = CopulaType.Normal;
            if (xElement.Attribute(nameof(CopulaType)) != null) Enum.TryParse(xElement.Attribute(nameof(CopulaType))!.Value, out copulaType);
            _copula = CreateCopula(copulaType);
            if (xElement.Attribute(nameof(CopulaEstimationMethod)) != null) Enum.TryParse(xElement.Attribute(nameof(CopulaEstimationMethod))!.Value, out _copulaEstimationMethod);

            var parms = new List<ModelParameter>();
            int idx = 0;
            foreach (XElement p in xElement.Elements(nameof(Parameters)).Elements(nameof(ModelParameter)))
            {
                var param = new ModelParameter(p);
                // Canonicalize the display name so projects saved with an older label
                // (e.g., "Theta") pick up the current label ("Dependency (θ)") on load.
                // The mutation is safe ONLY because we are still inside the constructor
                // — PropertyChanged subscribers have not yet been attached. Mutating
                // param.Name after construction would surface as an undo-redo entry
                // and could corrupt the GUI's tracking of canonical names.
                param.Name = ParameterNameFor(_copula, idx++);
                parms.Add(param);
            }
            Parameters = parms;
        
            // Set Copula parameters
            if (Parameters.Count > 0)
                Copula.SetCopulaParameters(Parameters.Select(p => p.Value).ToArray());

            // Build sample data for likelihood evaluation
            SetSampleData();
        }

        #endregion

        #region Members

        private BivariateCopula _copula = null!;
        private IUnivariateModel _marginalX = null!;
        private IUnivariateModel _marginalY = null!;
        private IList<double> _sampleDataX = null!;
        private IList<double> _sampleDataY = null!;
        private CopulaEstimationMethod _copulaEstimationMethod = CopulaEstimationMethod.InferenceFromMargins;

        /// <summary>
        /// Gets or sets the bivariate copula that models dependence between X and Y.
        /// </summary>
        [Category("Inputs")]
        [DisplayName("Bivariate Copula")]
        [Description("Defines the copula used to model dependency between the marginal distributions.")]
        [Browsable(true)]
        public BivariateCopula Copula
        {
            get => _copula;
            set
            {
                _copula = value ?? throw new ArgumentNullException(nameof(value));
                RaisePropertyChange(nameof(Copula));
                SetDefaultParameters();
            }
        }

        /// <summary>
        /// Gets or sets the copula type used in the joint model.
        /// </summary>
        public CopulaType CopulaType
        {
            get => Copula.Type;
            set
            {
                Copula = CreateCopula(value);
                RaisePropertyChange(nameof(CopulaType));
            }
        }

        /// <summary>
        /// Gets or sets the marginal distribution model for variable X.
        /// </summary>
        [Category("Inputs")]
        [DisplayName("Marginal-X")]
        [Description("The marginal distribution for X in the bivariate copula model.")]
        [Browsable(true)]
        public IUnivariateModel MarginalX
        {
            get => _marginalX;
            set
            {
                if (_marginalX is INotifyPropertyChanged oldNpc)
                {
                    oldNpc.PropertyChanged -= MarginalX_PropertyChanged;
                }

                _marginalX = value;

                if (_marginalX is INotifyPropertyChanged newNpc)
                {
                    newNpc.PropertyChanged += MarginalX_PropertyChanged;
                }

                RaisePropertyChange(nameof(MarginalX));

                if (_marginalX != null && UseDefaultFlatPriors)
                {
                    SetDefaultParameters();
                }
            }
        }

        /// <summary>
        /// Gets or sets the marginal distribution model for variable Y.
        /// </summary>
        [Category("Inputs")]
        [DisplayName("Marginal-Y")]
        [Description("The marginal distribution for Y in the bivariate copula model.")]
        [Browsable(true)]
        public IUnivariateModel MarginalY
        {
            get => _marginalY;
            set
            {
                if (_marginalY is INotifyPropertyChanged oldNpc)
                {
                    oldNpc.PropertyChanged -= MarginalY_PropertyChanged;
                }

                _marginalY = value;

                if (_marginalY is INotifyPropertyChanged newNpc)
                {
                    newNpc.PropertyChanged += MarginalY_PropertyChanged;
                }

                RaisePropertyChange(nameof(MarginalY));

                if (_marginalY != null && UseDefaultFlatPriors)
                {
                    SetDefaultParameters();
                }
            }
        }

        /// <summary>
        /// Gets or sets the estimation method used to fit the copula.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <see cref="CopulaEstimationMethod.InferenceFromMargins"/> uses the fitted
        /// marginal distributions and their CDF values.
        /// </para>
        /// <para>
        /// <see cref="CopulaEstimationMethod.PseudoLikelihood"/> uses empirical plotting
        /// positions based on the marginal data.
        /// </para>
        /// </remarks>
        [Category("Inputs")]
        [DisplayName("Estimation Method")]
        [Description("Method used to estimate the bivariate copula. IFM uses fitted marginals. Pseudo-likelihood uses empirical plotting positions.")]
        [Browsable(true)]
        public CopulaEstimationMethod CopulaEstimationMethod
        {
            get => _copulaEstimationMethod;
            set
            {
                if (_copulaEstimationMethod != value)
                {
                    _copulaEstimationMethod = value;
                    RaisePropertyChange(nameof(CopulaEstimationMethod));
                    // Rebuild sample data for the selected method
                    SetSampleData();
                }
            }
        }

        #endregion

        #region Methods


        /// <summary>
        /// Handles property changes on the X-marginal model.
        /// </summary>
        /// <param name="sender">The marginal model that raised the event.</param>
        /// <param name="e">Event arguments describing the change.</param>
        private void MarginalX_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            RaisePropertyChange(nameof(MarginalX));

            if (UseDefaultFlatPriors)
            {
                SetDefaultParameters();
            }
        }

        /// <summary>
        /// Handles property changes on the Y-marginal model.
        /// </summary>
        /// <param name="sender">The marginal model that raised the event.</param>
        /// <param name="e">Event arguments describing the change.</param>
        private void MarginalY_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            RaisePropertyChange(nameof(MarginalY));

            if (UseDefaultFlatPriors)
            {
                SetDefaultParameters();
            }
        }

        /// <summary>
        /// Creates a bivariate copula instance for use in RMC BestFit.
        /// </summary>
        /// <param name="copulaType">The copula type.</param>
        /// <returns>A new <see cref="BivariateCopula"/> instance.</returns>
        public static BivariateCopula CreateCopula(CopulaType copulaType)
        {
            return copulaType switch
            {
                CopulaType.AliMikhailHaq => new AMHCopula(),
                CopulaType.Clayton => new ClaytonCopula(),
                CopulaType.Frank => new FrankCopula(),
                CopulaType.Normal => new NormalCopula(),
                CopulaType.Gumbel => new GumbelCopula(),
                CopulaType.Joe => new JoeCopula(),
                CopulaType.StudentT => new StudentTCopula(),
                _ => throw new ArgumentOutOfRangeException(nameof(copulaType), copulaType, "Unsupported copula type.")
            };
        }

        /// <inheritdoc/>
        public override void SetDefaultParameters()
        {

            // Remove old handlers
            if (Parameters is not null && Parameters.Count > 0)
            {
                for (int i = 0; i < NumberOfParameters; i++)
                    Parameters[i].PropertyChanged -= Parameter_PropertyChanged;
            }

            _parameters = new List<ModelParameter>();

            if (Copula is null) return;
            if (MarginalX is null || MarginalX.Validate().IsValid == false || MarginalX.DataFrame is null) return;
            if (MarginalY is null || MarginalY.Validate().IsValid == false || MarginalY.DataFrame is null) return;

            // Build joint sample from marginals
            SetSampleData();
            if (_sampleDataX is null || _sampleDataY is null || _sampleDataX.Count == 0 || _sampleDataY.Count == 0)
            {
                return;
            }

            var bounds = Copula.ParameterConstraints(_sampleDataX, _sampleDataY);
            int nParams = Copula.NumberOfCopulaParameters;

            // Set the list of model parameters — one per copula parameter.
            for (int i = 0; i < nParams; i++)
            {
                double lower = bounds[i, 0];
                double upper = bounds[i, 1];

                _parameters.Add(new ModelParameter()
                {
                    Name = ParameterNameFor(Copula, i),
                    Value = InitialValueFor(Copula, i, lower, upper),
                    LowerBound = lower,
                    UpperBound = upper,
                    IsPositive = lower == Tools.DoubleMachineEpsilon,
                    PriorDistribution = new Uniform(lower, upper)
                });
            }

            // Add handlers
            for (int i = 0; i < NumberOfParameters; i++)
                _parameters[i].PropertyChanged += Parameter_PropertyChanged;

            RaisePropertyChange(nameof(SetDefaultParameters));
        }

        /// <summary>
        /// Returns the canonical parameter name for the i-th copula parameter.
        /// </summary>
        /// <remarks>
        /// Index 0 is always the dependency parameter (Theta). Student's t adds a second
        /// parameter (DegreesOfFreedom). The generic fallback is unreachable today but
        /// future-proofs against additional multi-parameter copulas.
        /// </remarks>
        private static string ParameterNameFor(BivariateCopula copula, int index)
        {
            if (index == 0) return "Dependency (θ)";
            if (copula is StudentTCopula && index == 1) return "DegreesOfFreedom";
            return $"Parameter{index + 1}";
        }

        /// <summary>
        /// Returns a sensible default initial value for the i-th copula parameter
        /// inside the [lower, upper] bounds returned by <see cref="BivariateCopula.ParameterConstraints"/>.
        /// </summary>
        /// <remarks>
        /// Index 0 uses the midpoint, matching the prior behavior for 1-parameter copulas.
        /// For Student's t index 1 (DegreesOfFreedom) we return 5 — the same default as the
        /// <see cref="StudentTCopula"/> parameterless constructor, well inside the [3, 60] bounds.
        /// </remarks>
        private static double InitialValueFor(BivariateCopula copula, int index, double lower, double upper)
        {
            if (copula is StudentTCopula && index == 1) return 5.0;
            return 0.5 * (lower + upper);
        }

        /// <summary>
        /// Builds the paired sample data required for copula estimation.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Only exact data that is not flagged as a low outlier is used.
        /// The two series are matched by their index value.
        /// </para>
        /// <para>
        /// When <see cref="CopulaEstimationMethod.PseudoLikelihood"/> is selected,
        /// the plotting position complements are used as pseudo observations on
        /// the unit interval. Otherwise, the raw exact values are used and
        /// transformed via the marginal CDFs inside the likelihood.
        /// </para>
        /// </remarks>
        public void SetSampleData()
        {
            _sampleDataX = new List<double>();
            _sampleDataY = new List<double>();

            if (MarginalX is null || MarginalX.Validate().IsValid == false) return;
            if (MarginalY is null || MarginalY.Validate().IsValid == false) return;

            // Process data frames
            MarginalX.DataFrame.ExactSeries.SortByIndex();
            MarginalY.DataFrame.ExactSeries.SortByIndex();

            var dataX = MarginalX.DataFrame.ExactSeries.Where(y => ((ExactData)y).IsLowOutlier == false).ToList();
            var dataY = MarginalY.DataFrame.ExactSeries.Where(y => ((ExactData)y).IsLowOutlier == false).ToList();

            // Two-pointer linear merge — both lists are sorted by Index above, so we
            // can pair matching indexes in O(n + m) instead of the previous O(n × m).
            if (CopulaEstimationMethod == CopulaEstimationMethod.PseudoLikelihood)
            {
                int i = 0, j = 0;
                while (i < dataX.Count && j < dataY.Count)
                {
                    int idxX = dataX[i].Index;
                    int idxY = dataY[j].Index;
                    if (idxX == idxY)
                    {
                        _sampleDataX.Add(dataX[i].PlottingPositionComplement);
                        _sampleDataY.Add(dataY[j].PlottingPositionComplement);
                        i++; j++;
                    }
                    else if (idxX < idxY) i++;
                    else j++;
                }
            }
            else
            {
                int i = 0, j = 0;
                while (i < dataX.Count && j < dataY.Count)
                {
                    int idxX = dataX[i].Index;
                    int idxY = dataY[j].Index;
                    if (idxX == idxY)
                    {
                        _sampleDataX.Add(dataX[i].Value);
                        _sampleDataY.Add(dataY[j].Value);
                        i++; j++;
                    }
                    else if (idxX < idxY) i++;
                    else j++;
                }

            }

        }

        /// <inheritdoc/>
        /// <remarks>
        /// Per-sample <c>LogPDF</c> and (under <see cref="CopulaEstimationMethod.InferenceFromMargins"/>)
        /// marginal <c>CDF</c> evaluations are wrapped so that any exception from the inner
        /// numerical code (e.g. <see cref="OverflowException"/> from a special-function
        /// evaluation in a heavy-tailed marginal at an MCMC-proposed pathological parameter)
        /// is treated as a rejected proposal: the offending sample contributes
        /// <see cref="double.NegativeInfinity"/> to the sum, which the
        /// <see cref="Tools.IsFinite"/> guard at the bottom converts to a top-level
        /// <see cref="double.NegativeInfinity"/> return. This keeps the MCMC chain alive
        /// instead of crashing on an arithmetic edge case.
        /// </remarks>
        public override double DataLogLikelihood(double[] parameters)
        {
            if (parameters == null) throw new ArgumentNullException(nameof(parameters));
            if (Copula is null || MarginalX?.Distribution is null || MarginalY?.Distribution is null)
            {
                return double.NegativeInfinity;
            }
            if (parameters.Length != Copula.NumberOfCopulaParameters)
            {
                return double.NegativeInfinity;
            }

            // Set model
            var model = Copula.Clone();
            model.SetCopulaParameters(parameters);
            model.MarginalDistributionX = MarginalX.Distribution.Clone();
            model.MarginalDistributionY = MarginalY.Distribution.Clone();

            // Compute data likelihood
            double logLH = 0;
            if (CopulaEstimationMethod == CopulaEstimationMethod.PseudoLikelihood)
            {
                for (int i = 0; i < _sampleDataX.Count; i++)
                {
                    double u = _sampleDataX[i];
                    double v = _sampleDataY[i];
                    logLH += SafeLogPDF(model, u, v);
                }
            }
            else if (CopulaEstimationMethod == CopulaEstimationMethod.InferenceFromMargins)
            {
                for (int i = 0; i < _sampleDataX.Count; i++)
                {
                    logLH += SafeIfmLogPDF(model, _sampleDataX[i], _sampleDataY[i]);
                }
            }
            if (!Tools.IsFinite(logLH)) return double.NegativeInfinity;
            return logLH;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Per-sample evaluations are individually shielded so a single arithmetic
        /// exception only zeros out (i.e. <see cref="double.NegativeInfinity"/>'s) the
        /// offending entry, leaving the rest of the pointwise vector intact for downstream
        /// WAIC / LOO-CV consumers.
        /// </remarks>
        public override double[] PointwiseDataLogLikelihood(double[] parameters)
        {
            if (parameters == null) throw new ArgumentNullException(nameof(parameters));

            int n = _sampleDataX?.Count ?? 0;
            if (n == 0) return Array.Empty<double>();

            if (parameters.Length != Copula.NumberOfCopulaParameters)
            {
                var invalid = new double[n];
                for (int i = 0; i < n; i++) invalid[i] = double.NegativeInfinity;
                return invalid;
            }

            // Set model
            var model = Copula.Clone();
            model.SetCopulaParameters(parameters);
            model.MarginalDistributionX = MarginalX.Distribution!.Clone();
            model.MarginalDistributionY = MarginalY.Distribution!.Clone();

            var result = new double[n];

            if (CopulaEstimationMethod == CopulaEstimationMethod.PseudoLikelihood)
            {
                for (int i = 0; i < n; i++)
                {
                    result[i] = SafeLogPDF(model, _sampleDataX![i], _sampleDataY![i]);
                }
            }
            else if (CopulaEstimationMethod == CopulaEstimationMethod.InferenceFromMargins)
            {
                for (int i = 0; i < n; i++)
                {
                    result[i] = SafeIfmLogPDF(model, _sampleDataX![i], _sampleDataY![i]);
                }
            }

            return result;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Same per-sample exception shielding as <see cref="PointwiseDataLogLikelihood"/>.
        /// </remarks>
        public override List<DataComponent> PointwiseDataLogLikelihoodComponents(double[] parameters)
        {
            if (parameters == null) throw new ArgumentNullException(nameof(parameters));

            int n = _sampleDataX?.Count ?? 0;
            if (n == 0) return new List<DataComponent>();

            var result = new List<DataComponent>(n);

            if (parameters.Length != Copula.NumberOfCopulaParameters)
            {
                for (int i = 0; i < n; i++)
                    result.Add(new DataComponent(i, double.NegativeInfinity, _sampleDataX![i], DataComponentType.Exact, 1, i.ToString()));
                return result;
            }

            // Set model
            var model = Copula.Clone();
            model.SetCopulaParameters(parameters);
            model.MarginalDistributionX = MarginalX.Distribution!.Clone();
            model.MarginalDistributionY = MarginalY.Distribution!.Clone();

            if (CopulaEstimationMethod == CopulaEstimationMethod.PseudoLikelihood)
            {
                for (int i = 0; i < n; i++)
                {
                    double logLH = SafeLogPDF(model, _sampleDataX![i], _sampleDataY![i]);
                    result.Add(new DataComponent(i, logLH, _sampleDataX[i], DataComponentType.Exact, 1, i.ToString()));
                }
            }
            else if (CopulaEstimationMethod == CopulaEstimationMethod.InferenceFromMargins)
            {
                for (int i = 0; i < n; i++)
                {
                    double logLH = SafeIfmLogPDF(model, _sampleDataX![i], _sampleDataY![i]);
                    result.Add(new DataComponent(i, logLH, _sampleDataX[i], DataComponentType.Exact, 1, i.ToString()));
                }
            }

            return result;
        }

        /// <summary>
        /// Pseudo-likelihood per-sample wrapper: evaluates <c>copula.LogPDF(u, v)</c> on the
        /// pseudo-uniform pair already on the (0, 1) scale, returning
        /// <see cref="double.NegativeInfinity"/> if the call throws or returns a non-finite
        /// value. Centralizes the exception handling for <see cref="DataLogLikelihood"/>,
        /// <see cref="PointwiseDataLogLikelihood"/>, and <see cref="PointwiseDataLogLikelihoodComponents"/>.
        /// </summary>
        /// <param name="model">Working copy of the copula with parameters already set.</param>
        /// <param name="u">Pseudo-uniform X value.</param>
        /// <param name="v">Pseudo-uniform Y value.</param>
        private static double SafeLogPDF(BivariateCopula model, double u, double v)
        {
            try
            {
                double logLH = model.LogPDF(u, v);
                return Tools.IsFinite(logLH) ? logLH : double.NegativeInfinity;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"BivariateDistribution.SafeLogPDF: {ex.GetType().Name} at (u={u}, v={v}) — {ex.Message}");
                return double.NegativeInfinity;
            }
        }

        /// <summary>
        /// Inference-from-margins per-sample wrapper: evaluates the marginal CDFs to obtain
        /// (u, v), then the copula <c>LogPDF</c>. Returns <see cref="double.NegativeInfinity"/>
        /// on any exception or non-finite intermediate value. The marginal-CDF stage is the
        /// one that brings in special-function arithmetic (Gamma, Beta, etc.) and is the
        /// most likely throw site for heavy-tailed marginals (LP3, GEV, Weibull, ...) when
        /// MCMC proposes an extreme parameter.
        /// </summary>
        /// <param name="model">Working copy of the copula (with cloned marginals attached).</param>
        /// <param name="x">Raw X observation.</param>
        /// <param name="y">Raw Y observation.</param>
        private static double SafeIfmLogPDF(BivariateCopula model, double x, double y)
        {
            try
            {
                double u = model.MarginalDistributionX!.CDF(x);
                double v = model.MarginalDistributionY!.CDF(y);
                if (!Tools.IsFinite(u) || !Tools.IsFinite(v)) return double.NegativeInfinity;
                double logLH = model.LogPDF(u, v);
                return Tools.IsFinite(logLH) ? logLH : double.NegativeInfinity;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"BivariateDistribution.SafeIfmLogPDF: {ex.GetType().Name} at (x={x}, y={y}) — {ex.Message}");
                return double.NegativeInfinity;
            }
        }

        /// <inheritdoc/>
        public override void SetParameterValues(IList<double> parameters)
        {
            if (parameters == null) throw new ArgumentNullException(nameof(parameters));
            if (parameters.Count != NumberOfParameters)
                throw new ArgumentException("The length of the parameter list in incorrect.", nameof(parameters));

            for (int i = 0; i < parameters.Count; i++)
                Parameters[i].Value = parameters[i];

            Copula.SetCopulaParameters(parameters.ToArray());
        }


        /// <inheritdoc/>
        /// <remarks>
        /// <para>
        /// Generates random paired samples from the bivariate distribution using
        /// the configured copula and marginal distributions.
        /// </para>
        /// </remarks>
        public double[,] GenerateRandomValues(int sampleSize, int seed = -1)
        {
            if (sampleSize <= 0) throw new ArgumentOutOfRangeException(nameof(sampleSize), "Sample size must be positive.");
            if (Copula is null) throw new InvalidOperationException("Copula cannot be null when generating random values.");
            if (MarginalX is null || MarginalY is null)
            {
                throw new InvalidOperationException("Both marginal distributions must be specified before generating random values.");
            }
            var copula = Copula.Clone();
            copula.MarginalDistributionX = MarginalX.Distribution;
            copula.MarginalDistributionY = MarginalY.Distribution;
            return copula.GenerateRandomValues(sampleSize, seed);
        }

        /// <inheritdoc/>
        public override IModel Clone()
        {
            var parms = new List<ModelParameter>();
            for (int i = 0; i < NumberOfParameters; i++)
                parms.Add(Parameters[i].Clone());

            var result = new BivariateDistribution()
            {
                _marginalX = MarginalX,
                _marginalY = MarginalY,
                _copula = Copula.Clone(),
                _useDefaultFlatPriors = UseDefaultFlatPriors,
                Parameters = parms,
            };

            // Rebuild sample data in the cloned object
            result.CopulaEstimationMethod = CopulaEstimationMethod;
            result.SetSampleData();
            return result;
        }

        /// <inheritdoc/>
        public override XElement ToXElement()
        {
            var result = new XElement(nameof(BivariateDistribution));
            result.SetAttributeValue(nameof(CopulaType), CopulaType.ToString());
            result.SetAttributeValue(nameof(CopulaEstimationMethod), CopulaEstimationMethod.ToString());
            result.SetAttributeValue(nameof(UseDefaultFlatPriors), UseDefaultFlatPriors.ToString());

            // Parameters
            var parms = new XElement(nameof(Parameters));
            if (Parameters is not null)
            {
                foreach (var p in Parameters)
                {
                    parms.Add(p.ToXElement());
                }
            }

            result.Add(parms);

            return result;
        }

        /// <inheritdoc/>
        public override (bool IsValid, List<string> ValidationMessages) Validate()
        {
            bool isValid = true;
            var messages = new List<string>();

            // Marginal checks
            if (MarginalX is null)
            {
                isValid = false;
                messages.Add("Error: Marginal distribution for X is not specified.");
            }
            else
            {
                var mxVal = MarginalX.Validate();
                if (!mxVal.IsValid)
                {
                    isValid = false;
                    messages.Add("Error: Marginal-X validation failed:");
                    messages.AddRange(mxVal.ValidationMessages);
                }
            }

            if (MarginalY is null)
            {
                isValid = false;
                messages.Add("Error: Marginal distribution for Y is not specified.");
            }
            else
            {
                var myVal = MarginalY.Validate();
                if (!myVal.IsValid)
                {
                    isValid = false;
                    messages.Add("Error: Marginal-Y validation failed:");
                    messages.AddRange(myVal.ValidationMessages);
                }
            }

            // Copula
            if (Copula is null)
            {
                isValid = false;
                messages.Add("Error: Copula is not specified.");
            }

            // Parameter checks
            int expectedParamCount = Copula?.NumberOfCopulaParameters ?? 0;
            if (Parameters is null || Parameters.Count != expectedParamCount)
            {
                isValid = false;
                messages.Add($"Error: Copula parameter collection is missing or has an unexpected size. Expected {expectedParamCount} parameter(s) for the {Copula?.Type.ToString() ?? "current"} copula.");
            }
            else
            {
                foreach (var p in Parameters)
                {
                    if (p.LowerBound > p.UpperBound)
                    {
                        isValid = false;
                        messages.Add($"Error: {p.Name} has inconsistent bounds. LowerBound must be less than or equal to UpperBound.");
                    }

                    if (p.Value < p.LowerBound || p.Value > p.UpperBound)
                    {
                        isValid = false;
                        messages.Add($"Error: {p.Name} is outside its specified bounds.");
                    }

                    if (p.PriorDistribution is null || !p.PriorDistribution.ParametersValid)
                    {
                        isValid = false;
                        messages.Add($"Error: {p.Name} prior distribution is not defined or is invalid.");
                    }
                }
            }

            // Sample data checks. Validate must be read-only — sample data is
            // populated as a side effect of marginal assignment, not here.
            if (_sampleDataX is null || _sampleDataY is null ||
                _sampleDataX.Count == 0 || _sampleDataY.Count == 0)
            {
                isValid = false;
                messages.Add("Error: No overlapping, non-outlier exact data is available to estimate the copula.");
            }
            else if (_sampleDataX.Count != _sampleDataY.Count)
            {
                isValid = false;
                messages.Add("Error: The paired sample data for the copula is inconsistent. X and Y sample sizes do not match.");
            }

            // Pseudo likelihood requires unit interval data
            if (CopulaEstimationMethod == CopulaEstimationMethod.PseudoLikelihood &&
                _sampleDataX is not null &&
                _sampleDataY is not null &&
                _sampleDataX.Count > 0)
            {
                if (_sampleDataX.Any(u => u <= 0.0 || u >= 1.0) ||
                    _sampleDataY.Any(v => v <= 0.0 || v >= 1.0))
                {
                    isValid = false;
                    messages.Add("Error: Pseudo likelihood estimation requires plotting positions strictly between 0 and 1.");
                }
            }

            return (isValid, messages);
        }

        #endregion
    }
}
