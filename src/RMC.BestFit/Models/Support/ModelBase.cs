using Numerics;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Xml.Linq;

namespace RMC.BestFit.Models
{
    /// <summary>
    /// Model base class.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// </remarks>
    public abstract class ModelBase : IModel
    {

        #region Members

        /// <summary>
        /// Backing collection of model parameters.
        /// </summary>
        protected List<ModelParameter> _parameters = new List<ModelParameter>();

        /// <summary>
        /// Backing value indicating whether default flat priors are applied.
        /// </summary>
        protected bool _useDefaultFlatPriors = true;

        /// <inheritdoc/>
        [Category("Inputs")]
        [DisplayName("Model Parameter Priors")]
        [Description("Specifies the prior distributions for the model parameters.")]
        [Browsable(true)]
        public virtual List<ModelParameter> Parameters
        {
            get { return _parameters; }
            protected set
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
        [Category("Inputs")]
        [DisplayName("Use Default Flat Priors")]
        [Description("If true, the default flat (uniform) prior distributions are applied to the model parameters.")]
        [Browsable(true)]
        public virtual bool UseDefaultFlatPriors
        {
            get { return _useDefaultFlatPriors; }
            set
            {
                if (_useDefaultFlatPriors != value)
                {
                    _useDefaultFlatPriors = value;
                    RaisePropertyChange(nameof(UseDefaultFlatPriors));
                    if (_useDefaultFlatPriors == true) SetDefaultParameters();
                }
            }
        }

        /// <inheritdoc/>
        /// <summary>
        /// Occurs when a model property changes.
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        #endregion

        #region Methods

        /// <summary>
        /// Raise property changed event.
        /// </summary>
        /// <param name="propertyName">Name of property that changed.</param>
        protected virtual void RaisePropertyChange(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        }

        /// <summary>
        /// Handles the model parameter property changed event.
        /// </summary>
        protected virtual void Parameter_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ModelParameter.PriorDistribution))
                RaisePropertyChange(nameof(Parameters));
        }

        /// <inheritdoc/>
        public abstract void SetDefaultParameters();

        /// <inheritdoc/>
        public virtual double LogLikelihood(double[] parameters)
        {
            double logLH = DataLogLikelihood(parameters) + PriorLogLikelihood(parameters);
            if (!Tools.IsFinite(logLH))
                return double.NegativeInfinity;
            return logLH;
        }

        /// <inheritdoc/>
        public abstract double DataLogLikelihood(double[] parameters);

        /// <inheritdoc/>
        public abstract double[] PointwiseDataLogLikelihood(double[] parameters);

        /// <inheritdoc/>
        public abstract List<DataComponent> PointwiseDataLogLikelihoodComponents(double[] parameters);

        /// <inheritdoc/>
        public virtual double PriorLogLikelihood(double[] parameters)
        {
            if (parameters == null || parameters.Length != Parameters.Count)
                return double.NegativeInfinity;

            double logLH = 0;
            for (int i = 0; i < Parameters.Count; i++)
            {
                logLH += Parameters[i].PriorDistribution.LogPDF(parameters[i]);
            }
            if (!Tools.IsFinite(logLH)) return double.NegativeInfinity;
            return logLH;
        }

        /// <inheritdoc/>
        public virtual List<PriorComponent> PointwisePriorLogLikelihood(double[] parameters)
        {
            var result = new List<PriorComponent>();

            if (parameters == null || parameters.Length != Parameters.Count)
                return result;

            // Add parameter priors
            for (int i = 0; i < Parameters.Count; i++)
            {
                double ll = Parameters[i].PriorDistribution.LogPDF(parameters[i]);
                string paramName = string.IsNullOrEmpty(Parameters[i].OwnerName) ? Parameters[i].Name : Parameters[i].OwnerName;
                result.Add(new PriorComponent(
                    $"Parameter Prior: {paramName}",
                    ll,
                    PriorComponentType.ParameterPrior));
            }

            return result;
        }

        /// <inheritdoc/>
        public virtual void SetParameterValues(IList<double> parameters)
        {
            if (parameters.Count != NumberOfParameters) throw new ArgumentException("The list of parameter values are the wrong length", nameof(parameters));
            for (int i = 0; i < NumberOfParameters; i++)
                Parameters[i].Value = parameters[i];
        }

        /// <inheritdoc/>
        public abstract IModel Clone();

        /// <inheritdoc/>
        public abstract XElement ToXElement();

        /// <inheritdoc/>
        public abstract (bool IsValid, List<string> ValidationMessages) Validate();

        #endregion

    }
}
