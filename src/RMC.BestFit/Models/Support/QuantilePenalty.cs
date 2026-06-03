using Numerics;
using Numerics.Distributions;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Xml.Linq;

namespace RMC.BestFit.Models
{
    /// <summary>
    /// Represents a penalty term for a specific quantile in Bulletin 17C analysis.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Authors:</b>
    /// Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    /// Quantile penalties allow incorporation of prior information about specific flood quantiles
    /// into the Generalized Method of Moments estimation. This is useful for incorporating
    /// historical flood estimates or regional regression equations at specific return periods.
    /// </para>
    /// <para>
    /// The penalty function adds a term proportional to (quantile - mean)² / (MSE × n) to the
    /// objective function, where n is the sample size. When <see cref="UseLog10"/> is <c>true</c>,
    /// the penalty is computed in log10 space, which is appropriate for flood discharges.
    /// </para>
    /// </remarks>
    public class QuantilePenalty : INotifyPropertyChanged
    {
        #region Construction

        /// <summary>
        /// Initializes a new instance of the <see cref="QuantilePenalty"/> class.
        /// </summary>
        public QuantilePenalty()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="QuantilePenalty"/> class
        /// by deserializing from an <see cref="XElement"/>.
        /// </summary>
        /// <param name="xElement">The XML element containing the serialized penalty.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="xElement"/> is <c>null</c>.
        /// </exception>
        public QuantilePenalty(XElement xElement)
        {
            if (xElement == null) throw new ArgumentNullException(nameof(xElement));

            var enabledAttr = xElement.Attribute(nameof(Enabled));
            if (enabledAttr != null)
                bool.TryParse(enabledAttr.Value, out _enabled);

            var useLog10Attr = xElement.Attribute(nameof(UseLog10));
            if (useLog10Attr != null)
                bool.TryParse(useLog10Attr.Value, out _useLog10);

            var aepAttr = xElement.Attribute(nameof(AEP));
            if (aepAttr != null)
                double.TryParse(aepAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out _aep);

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
        private bool _useLog10;
        private double _aep = 0.01;
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
        /// Gets or sets the annual exceedance probability (AEP) for this quantile.
        /// </summary>
        /// <value>
        /// The probability of exceeding this quantile in any given year (e.g., 0.01 for the 1% AEP flood).
        /// Must be between 0 and 1.
        /// </value>
        public double AEP
        {
            get => _aep;
            set
            {
                if (_aep != value)
                {
                    _aep = value;
                    RaisePropertyChanged(nameof(AEP));
                }
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the mean and MSE are specified in log10 space.
        /// </summary>
        /// <value>
        /// <c>true</c> if <see cref="Mean"/> and <see cref="MSE"/> represent log10-transformed values;
        /// otherwise, <c>false</c> for real-space values.
        /// </value>
        /// <remarks>
        /// <para>
        /// For flood frequency analysis, it is common to specify quantile estimates in log10 space
        /// because flood discharges are typically log-normally distributed.
        /// </para>
        /// </remarks>
        public bool UseLog10
        {
            get => _useLog10;
            set
            {
                if (_useLog10 != value)
                {
                    _useLog10 = value;
                    RaisePropertyChanged(nameof(UseLog10));
                }
            }
        }

        /// <summary>
        /// Gets or sets the prior mean value for the quantile.
        /// </summary>
        /// <value>
        /// The expected value of the quantile from prior information.
        /// If <see cref="UseLog10"/> is <c>true</c>, this is the expected log10 value.
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
        /// The variance of the prior distribution.
        /// If <see cref="UseLog10"/> is <c>true</c>, this is the variance in log10 space.
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
        /// Gets the mean value in real space (not log-transformed).
        /// </summary>
        /// <value>
        /// If <see cref="UseLog10"/> is <c>false</c>, returns <see cref="Mean"/> directly.
        /// Otherwise, transforms from log10 space accounting for lognormal bias.
        /// </value>
        public double MeanValue
        {
            get
            {
                if (!UseLog10)
                    return Mean;

                double lnB = Math.Log(10);
                return Math.Exp((Mean + 0.5 * MSE * lnB) * lnB);
            }
        }

        /// <summary>
        /// Gets the MSE value in real space (not log-transformed).
        /// </summary>
        /// <value>
        /// If <see cref="UseLog10"/> is <c>false</c>, returns <see cref="MSE"/> directly.
        /// Otherwise, transforms from log10-space variance to real-space variance.
        /// </value>
        public double MSEValue
        {
            get
            {
                if (!UseLog10)
                    return MSE;

                double lnB = Math.Log(10);
                double a = MSE * lnB;
                double logPrefactor = (2.0 * Mean + a) * lnB;
                double expA = Math.Exp(a * lnB);
                return Math.Exp(logPrefactor) * (expA - 1.0);
            }
        }

        /// <summary>
        /// Gets the 95th percentile of the quantile prior distribution.
        /// </summary>
        /// <value>
        /// The upper bound of the 90% credible interval for the quantile estimate.
        /// </value>
        public double UpperValue
        {
            get
            {
                if (!UseLog10)
                    return Mean + Normal.StandardZ(0.95) * Math.Sqrt(MSE);
                return Math.Pow(10, Mean + Normal.StandardZ(0.95) * Math.Sqrt(MSE));
            }
        }

        /// <summary>
        /// Gets the 5th percentile of the quantile prior distribution.
        /// </summary>
        /// <value>
        /// The lower bound of the 90% credible interval for the quantile estimate.
        /// </value>
        public double LowerValue
        {
            get
            {
                if (!UseLog10)
                    return Mean + Normal.StandardZ(0.05) * Math.Sqrt(MSE);
                return Math.Pow(10, Mean + Normal.StandardZ(0.05) * Math.Sqrt(MSE));
            }
        }

        /// <summary>
        /// Gets a value indicating whether the penalty parameters are valid.
        /// </summary>
        /// <value>
        /// <c>true</c> if all parameters are valid; otherwise, <c>false</c>.
        /// </value>
        public bool IsValid =>
            AEP > 0 && AEP < 1 &&
            !double.IsNaN(Mean) && !double.IsInfinity(Mean) &&
            !double.IsNaN(MSE) && !double.IsInfinity(MSE) && MSE > 0;

        #endregion

        #region Methods

        /// <summary>
        /// Computes the penalty function value for a given quantile value.
        /// </summary>
        /// <param name="quantileValue">The current quantile value from the distribution.</param>
        /// <param name="sampleSize">The sample size, used to scale the penalty.</param>
        /// <returns>
        /// The penalty value computed as (1/2)(quantile - Mean)² / (MSE × sampleSize).
        /// If <see cref="UseLog10"/> is <c>true</c>, the calculation is performed in log10 space.
        /// Returns 0 if the penalty is not enabled or parameters are invalid.
        /// </returns>
        /// <remarks>
        /// <para>
        /// The 1/2 factor is part of the half-quadratic convention used in penalized GMM:
        /// Q = (1/2)g'Wg + penalty, where penalty = (1/2)(q−q₀)²/(MSE·n).
        /// This ensures the gradient and Hessian are directly usable without a Lambda correction factor.
        /// </para>
        /// </remarks>
        public double Function(double quantileValue, int sampleSize)
        {
            if (!Enabled || !IsValid || sampleSize <= 0)
                return 0.0;

            double value;
            if (UseLog10)
            {
                // Guard against non-positive values for log10
                if (quantileValue <= 0)
                    return 0.0;
                value = Tools.Log10(quantileValue);
            }
            else
            {
                value = quantileValue;
            }
            return 0.5 * Tools.Sqr(value - Mean) / (MSE * sampleSize);
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

            if (AEP <= 0 || AEP >= 1)
                return (false, $"Quantile penalty: AEP must be between 0 and 1 (got {AEP}).");

            if (double.IsNaN(Mean) || double.IsInfinity(Mean))
                return (false, "Quantile penalty: Mean value is not a valid number.");

            if (double.IsNaN(MSE) || double.IsInfinity(MSE))
                return (false, "Quantile penalty: MSE is not a valid number.");

            if (MSE <= 0)
                return (false, "Quantile penalty: MSE must be greater than zero.");

            return (true, string.Empty);
        }

        /// <summary>
        /// Creates a deep copy of this penalty.
        /// </summary>
        /// <returns>A new <see cref="QuantilePenalty"/> instance with the same values.</returns>
        public QuantilePenalty Clone()
        {
            return new QuantilePenalty
            {
                Enabled = Enabled,
                UseLog10 = UseLog10,
                AEP = AEP,
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
            var element = new XElement(nameof(QuantilePenalty));
            element.SetAttributeValue(nameof(Enabled), Enabled.ToString());
            element.SetAttributeValue(nameof(UseLog10), UseLog10.ToString());
            element.SetAttributeValue(nameof(AEP), AEP.ToString("G17", CultureInfo.InvariantCulture));
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
