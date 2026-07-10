using System.ComponentModel;

namespace RMC.BestFit.Models
{
    /// <summary>
    /// Base class for univariate distribution models with support for
    /// quantile priors and common data frame handling.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    /// Concrete models inherit from <see cref="UnivariateDistributionModelBase"/>
    /// to gain:
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// A shared <see cref="DataFrame"/> input used for likelihood evaluation.
    /// </description></item>
    /// <item><description>
    /// Infrastructure for quantile priors via the <see cref="IQuantilePriors"/> interface.
    /// </description></item>
    /// <item><description>
    /// Automatic wiring of <see cref="DataFrame.PropertyChanged"/> and
    /// <see cref="QuantilePrior.PropertyChanged"/> events.
    /// </description></item>
    /// </list>
    /// <para>
    /// Derived classes are responsible for implementing
    /// <see cref="SetDefaultQuantilePriors"/> and <see cref="ProcessQuantilePriors"/>,
    /// as well as any distribution specific behavior.
    /// </para>
    /// </remarks>
    public abstract class UnivariateDistributionModelBase : ModelBase, IQuantilePriors
    {
        #region Members

        /// <summary>
        /// Backing field for <see cref="DataFrame"/>.
        /// </summary>
        protected DataFrame _dataFrame = null!;

        /// <summary>
        /// Backing field for <see cref="QuantilePriors"/>.
        /// </summary>
        protected List<QuantilePrior> _quantilePriors = new List<QuantilePrior>();

        /// <summary>
        /// Processed quantile priors used internally (for example,
        /// after transforming priors on absolute quantiles to priors on
        /// quantile differences).
        /// </summary>
        protected List<QuantilePrior> _quantilePriorsTrue = new List<QuantilePrior>();

        /// <summary>
        /// Backing field for <see cref="UseJeffreysRuleForScale"/>.
        /// </summary>
        protected bool _useJeffreysRuleForScale = true;

        /// <summary>
        /// Backing field for <see cref="EnableQuantilePriors"/>.
        /// </summary>
        protected bool _enableQuantilePriors = false;

        /// <summary>
        /// Backing field for <see cref="UseSingleQuantile"/>.
        /// </summary>
        protected bool _useSingleQuantile = false;

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
        /// Optionally calls <c>SetDefaultParameters</c> (defined in a derived
        /// class) when <c>UseDefaultFlatPriors</c> is true.
        /// </description></item>
        /// </list>
        /// </remarks>
        public virtual DataFrame DataFrame
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

                    if (UseDefaultFlatPriors == true)
                        SetDefaultParameters();
                }

                RaisePropertyChange(nameof(DataFrame));
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether Jeffreys rule should be
        /// applied to the scale parameter.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When true, the prior on the scale parameter s is proportional to 1/s,
        /// which is the Jeffreys noninformative prior for a pure scale parameter.
        /// </para>
        /// </remarks>
        [Category("Inputs")]
        [DisplayName("Use Jeffreys' Rule for Scale")]
        [Description("If true, applies Jeffreys' rule to the scale (s) parameter, setting the prior P(s) proportional to 1/s.")]
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

        /// <inheritdoc/>
        [Category("Inputs")]
        [DisplayName("Quantile Priors")]
        [Description("Specifies the prior distributions for the distribution quantiles.")]
        [Browsable(true)]
        public virtual List<QuantilePrior> QuantilePriors
        {
            get { return _quantilePriors; }
            set
            {
                // Unsubscribe from previous handlers
                if (_quantilePriors != null)
                {
                    for (int i = 0; i < _quantilePriors.Count; i++)
                        _quantilePriors[i].PropertyChanged -= QuantilePrior_PropertyChanged;
                }

                _quantilePriors = value ?? new List<QuantilePrior>();

                // Subscribe to new handlers
                if (_quantilePriors != null)
                {
                    for (int i = 0; i < _quantilePriors.Count; i++)
                        _quantilePriors[i].PropertyChanged += QuantilePrior_PropertyChanged;
                }

                // Recompute the processed priors for use in the likelihood
                ProcessQuantilePriors();

                RaisePropertyChange(nameof(QuantilePriors));
            }
        }

        /// <inheritdoc/>
        [Category("Inputs")]
        [DisplayName("Enable Quantile Priors")]
        [Description("If true, enables the use of prior distributions for the parent distribution quantiles.")]
        [Browsable(true)]
        public virtual bool EnableQuantilePriors
        {
            get { return _enableQuantilePriors; }
            set
            {
                if (_enableQuantilePriors != value)
                {
                    _enableQuantilePriors = value;
                    RaisePropertyChange(nameof(EnableQuantilePriors));
                    SetDefaultQuantilePriors();
                }
            }
        }

        /// <inheritdoc/>
        [Category("Inputs")]
        [DisplayName("Use Single Quantile")]
        [Description("If true, a single quantile prior is applied (Viglione et al., 2013). If false, provide one quantile prior per parent distribution parameter (Coles and Tawn, 1996).")]
        [Browsable(true)]
        public virtual bool UseSingleQuantile
        {
            get { return _useSingleQuantile; }
            set
            {
                if (_useSingleQuantile != value)
                {
                    _useSingleQuantile = value;
                    RaisePropertyChange(nameof(UseSingleQuantile));
                    SetDefaultQuantilePriors();
                }
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Handles the <see cref="DataFrame.PropertyChanged"/> event.
        /// </summary>
        /// <param name="sender">The data frame that raised the event.</param>
        /// <param name="e">The event data.</param>
        /// <remarks>
        /// <para>
        /// The base implementation reprocesses threshold series and, when
        /// <see cref="ModelBase.UseDefaultFlatPriors"/> is true, calls
        /// <c>SetDefaultParameters</c> in the derived class.
        /// </para>
        /// <para>
        /// Changes to plotting parameter properties are ignored because they
        /// do not affect likelihood evaluation.
        /// </para>
        /// </remarks>
        protected virtual void DataFrame_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DataFrame.PlottingParameter) || e.PropertyName == nameof(Data.PlottingPosition))
            {
                return;
            }

            if (DataFrame is null)
                return;

            DataFrame.ProcessThresholdSeries();
            RaisePropertyChange(nameof(DataFrame));

            if (UseDefaultFlatPriors == true)
                SetDefaultParameters();
        }

        /// <summary>
        /// Handles the <see cref="QuantilePrior.PropertyChanged"/> event for
        /// any quantile prior in <see cref="QuantilePriors"/>.
        /// </summary>
        /// <param name="sender">The quantile prior that raised the event.</param>
        /// <param name="e">The event data.</param>
        /// <remarks>
        /// <para>
        /// The base implementation raises a property change notification for
        /// <see cref="QuantilePriors"/> and recomputes the internal
        /// <see cref="_quantilePriorsTrue"/> collection by calling
        /// <see cref="ProcessQuantilePriors"/>.
        /// </para>
        /// </remarks>
        protected virtual void QuantilePrior_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            ProcessQuantilePriors();
            RaisePropertyChange(nameof(QuantilePriors));
        }

        /// <inheritdoc/>
        public abstract void SetDefaultQuantilePriors();

        /// <inheritdoc/>
        public abstract void ProcessQuantilePriors();
        
        #endregion
    }
}
