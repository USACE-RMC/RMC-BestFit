using Numerics;
using Numerics.Distributions;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Xml.Linq;

namespace RMC.BestFit.Models
{
    /// <summary>
    /// Represents a penalty term for a distribution parameter in Bulletin 17C analysis.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Authors:</b>
    /// Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    /// Parameter penalties allow incorporation of prior information about distribution parameters
    /// into the Generalized Method of Moments estimation. This is particularly useful for
    /// incorporating regional skewness estimates in flood frequency analysis.
    /// </para>
    /// <para>
    /// The penalty function adds a term proportional to (parameter - mean)² / (MSE × n) to the
    /// objective function, where n is the sample size. This effectively pulls the estimate
    /// toward the prior mean with strength inversely proportional to the prior variance (MSE).
    /// </para>
    /// </remarks>
    public class ParameterPenalty : INotifyPropertyChanged
    {
        #region Construction

        /// <summary>
        /// Initializes a new instance of the <see cref="ParameterPenalty"/> class.
        /// </summary>
        public ParameterPenalty() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="ParameterPenalty"/> class
        /// by deserializing from an <see cref="XElement"/>.
        /// </summary>
        /// <param name="xElement">The XML element containing the serialized penalty.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="xElement"/> is <c>null</c>.
        /// </exception>
        public ParameterPenalty(XElement xElement)
        {
            if (xElement == null) throw new ArgumentNullException(nameof(xElement));

            var enabledAttr = xElement.Attribute(nameof(Enabled));
            if (enabledAttr != null)
                bool.TryParse(enabledAttr.Value, out _enabled);

            var nameAttr = xElement.Attribute(nameof(Name));
            if (nameAttr != null)
                _name = nameAttr.Value;

            var useLogAttr = xElement.Attribute(nameof(UseLog));
            if (useLogAttr != null)
                bool.TryParse(useLogAttr.Value, out _useLog);

            var meanAttr = xElement.Attribute(nameof(Mean));
            if (meanAttr != null)
                double.TryParse(meanAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out _mean);

            var mseAttr = xElement.Attribute(nameof(MSE));
            if (mseAttr != null)
                double.TryParse(mseAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out _mse);
        }

        #endregion

        #region Members

        private bool _enabled;
        private bool _useLog;
        private string _name = string.Empty;
        private double _mean = double.NaN;
        private double _mse = double.NaN;

        /// <summary>
        /// Gets or sets a value indicating whether this penalty is enabled.
        /// </summary>
        /// <value>
        /// <c>true</c> if the penalty should be applied during estimation; otherwise, <c>false</c>.
        /// </value>
        public bool Enabled
        {
            get => _enabled;
            set
            {
                if (_enabled != value)
                {
                    _enabled = value;
                    RaisePropertyChanged(nameof(Enabled));
                }
            }
        }

        /// <summary>
        /// Gets or sets the name of the parameter being penalized.
        /// </summary>
        /// <value>
        /// The parameter name (e.g., "Skewness", "Mean", "Standard Deviation").
        /// </value>
        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    RaisePropertyChanged(nameof(Name));
                }
            }
        }

        /// <summary>
        /// Gets or sets the prior mean value for the parameter.
        /// </summary>
        /// <value>
        /// The expected value of the parameter from prior information (e.g., regional skewness).
        /// </value>
        public double Mean
        {
            get => _mean;
            set
            {
                if (_mean != value)
                {
                    _mean = value;
                    RaisePropertyChanged(nameof(Mean));
                }
            }
        }

        /// <summary>
        /// Gets or sets the mean squared error (variance) of the prior.
        /// </summary>
        /// <value>
        /// The variance of the prior distribution. Larger values indicate greater uncertainty
        /// in the prior and result in weaker regularization.
        /// </value>
        public double MSE
        {
            get => _mse;
            set
            {
                if (_mse != value)
                {
                    _mse = value;
                    RaisePropertyChanged(nameof(MSE));
                }
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the penalty should be computed in natural log space.
        /// </summary>
        /// <value>
        /// <c>true</c> to compute the penalty in log space; otherwise, <c>false</c> for real-space penalties.
        /// </value>
        /// <remarks>
        /// <para>
        /// <see cref="Mean"/> and <see cref="MSE"/> are always specified in real space regardless of this setting.
        /// When enabled, <see cref="Function"/> internally converts to log space using the delta method:
        /// ln(θ₀) for the mean and MSE/θ₀² for the variance. The penalty becomes
        /// (1/2)(ln(θ) − ln(θ₀))² / ((MSE/θ₀²) · n), which is equivalent to a lognormal prior.
        /// </para>
        /// <para>
        /// This is appropriate for scale parameters (e.g., standard deviation σ) that are bounded
        /// below by zero, where a real-space quadratic penalty creates asymmetric bias.
        /// </para>
        /// </remarks>
        public bool UseLog
        {
            get => _useLog;
            set
            {
                if (_useLog != value)
                {
                    _useLog = value;
                    RaisePropertyChanged(nameof(UseLog));
                }
            }
        }

        /// <summary>
        /// Gets the 95th percentile of the parameter prior distribution.
        /// </summary>
        /// <value>
        /// The upper bound of the 90% credible interval for the parameter estimate.
        /// When <see cref="UseLog"/> is <c>true</c>, uses a lognormal distribution.
        /// </value>
        public double UpperValue
        {
            get
            {
                if (!UseLog)
                    return Mean + Normal.StandardZ(0.95) * Math.Sqrt(MSE);
                // Lognormal upper bound: exp(ln(Mean) + z95 * sqrt(MSE/Mean²))
                double logSD = Math.Sqrt(MSE) / Mean;
                return Mean * Math.Exp(Normal.StandardZ(0.95) * logSD);
            }
        }

        /// <summary>
        /// Gets the 5th percentile of the parameter prior distribution.
        /// </summary>
        /// <value>
        /// The lower bound of the 90% credible interval for the parameter estimate.
        /// When <see cref="UseLog"/> is <c>true</c>, uses a lognormal distribution.
        /// </value>
        public double LowerValue
        {
            get
            {
                if (!UseLog)
                    return Mean + Normal.StandardZ(0.05) * Math.Sqrt(MSE);
                // Lognormal lower bound: exp(ln(Mean) + z05 * sqrt(MSE/Mean²))
                double logSD = Math.Sqrt(MSE) / Mean;
                return Mean * Math.Exp(Normal.StandardZ(0.05) * logSD);
            }
        }

        /// <summary>
        /// Gets a value indicating whether the penalty parameters are valid.
        /// </summary>
        /// <value>
        /// <c>true</c> if <see cref="Mean"/> is finite and <see cref="MSE"/> is positive and finite;
        /// otherwise, <c>false</c>.
        /// </value>
        public bool IsValid =>
            !double.IsNaN(Mean) && !double.IsInfinity(Mean) &&
            !double.IsNaN(MSE) && !double.IsInfinity(MSE) && MSE > 0 &&
            (!UseLog || Mean > 0);

        #endregion

        #region Methods

        /// <summary>
        /// Computes the penalty function value for a given parameter value.
        /// </summary>
        /// <param name="parameterValue">The current parameter value in real space.</param>
        /// <param name="sampleSize">The sample size, used to scale the penalty.</param>
        /// <returns>
        /// The penalty value. When <see cref="UseLog"/> is <c>false</c>, computed as
        /// (1/2)(θ − θ₀)² / (MSE · n). When <c>true</c>, Mean and MSE are internally
        /// converted to log space via the delta method and the penalty is computed as
        /// (1/2)(ln(θ) − ln(θ₀))² / ((MSE/θ₀²) · n).
        /// Returns 0 if the penalty is not enabled, parameters are invalid, or
        /// <paramref name="parameterValue"/> ≤ 0 when <see cref="UseLog"/> is <c>true</c>.
        /// </returns>
        /// <remarks>
        /// <para>
        /// The 1/2 factor is part of the half-quadratic convention used in penalized GMM:
        /// Q = (1/2)g'Wg + penalty. This ensures the gradient and Hessian are directly
        /// usable without a Lambda correction factor.
        /// </para>
        /// <para>
        /// When <see cref="UseLog"/> is <c>true</c>, the penalty is equivalent to a lognormal
        /// prior on the parameter. <see cref="Mean"/> and <see cref="MSE"/> remain in real space;
        /// the conversion to log space uses the delta method: ln(θ₀) for the mean and MSE/θ₀²
        /// for the variance.
        /// </para>
        /// <para>
        /// The 1/n scaling ensures the penalty maintains appropriate influence relative
        /// to the moment conditions as sample size increases.
        /// </para>
        /// </remarks>
        public double Function(double parameterValue, int sampleSize)
        {
            if (!Enabled || !IsValid || sampleSize <= 0)
                return 0.0;

            if (UseLog)
            {
                // Guard against non-positive values for log transform
                if (parameterValue <= 0 || Mean <= 0)
                    return 0.0;

                // Convert real-space Mean/MSE to log-space via delta method:
                // Var(ln(X)) ≈ Var(X) / E[X]²
                double logParam = Math.Log(parameterValue);
                double logMean = Math.Log(Mean);
                double logMSE = MSE / (Mean * Mean);
                return 0.5 * Tools.Sqr(logParam - logMean) / (logMSE * sampleSize);
            }
            else
            {
                return 0.5 * Tools.Sqr(parameterValue - Mean) / (MSE * sampleSize);
            }
        }

        /// <summary>
        /// Validates the penalty configuration.
        /// </summary>
        /// <returns>
        /// A tuple containing a boolean indicating validity and an error message if invalid.
        /// </returns>
        public (bool IsValid, string Message) Validate()
        {
            if (!Enabled)
                return (true, string.Empty);

            if (double.IsNaN(Mean) || double.IsInfinity(Mean))
                return (false, $"Parameter penalty '{Name}': Mean value is not a valid number.");

            if (double.IsNaN(MSE) || double.IsInfinity(MSE))
                return (false, $"Parameter penalty '{Name}': MSE is not a valid number.");

            if (MSE <= 0)
                return (false, $"Parameter penalty '{Name}': MSE must be greater than zero.");

            if (UseLog && Mean <= 0)
                return (false, $"Parameter penalty '{Name}': Mean must be positive when UseLog is enabled.");

            return (true, string.Empty);
        }

        /// <summary>
        /// Creates a deep copy of this penalty.
        /// </summary>
        /// <returns>A new <see cref="ParameterPenalty"/> instance with the same values.</returns>
        public ParameterPenalty Clone()
        {
            return new ParameterPenalty
            {
                Enabled = Enabled,
                Name = Name,
                UseLog = UseLog,
                Mean = Mean,
                MSE = MSE
            };
        }

        /// <summary>
        /// Serializes this penalty to an <see cref="XElement"/>.
        /// </summary>
        /// <returns>An XML element containing the serialized penalty.</returns>
        public XElement ToXElement()
        {
            var element = new XElement(nameof(ParameterPenalty));
            element.SetAttributeValue(nameof(Enabled), Enabled.ToString());
            element.SetAttributeValue(nameof(Name), Name ?? string.Empty);
            element.SetAttributeValue(nameof(UseLog), UseLog.ToString());
            element.SetAttributeValue(nameof(Mean), Mean.ToString("G17", CultureInfo.InvariantCulture));
            element.SetAttributeValue(nameof(MSE), MSE.ToString("G17", CultureInfo.InvariantCulture));
            return element;
        }

        #endregion

        #region INotifyPropertyChanged

        /// <inheritdoc/>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Raises the <see cref="PropertyChanged"/> event.
        /// </summary>
        /// <param name="propertyName">The name of the property that changed.</param>
        protected virtual void RaisePropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}
