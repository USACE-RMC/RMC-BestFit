using Numerics;
using Numerics.Distributions;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Xml.Linq;

namespace RMC.BestFit.Models
{

    /// <summary>
    /// A quantile prior.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// </remarks>
    public class QuantilePrior : INotifyPropertyChanged
    {
        /// <summary>
        /// Empty constructor
        /// </summary>
        public QuantilePrior()
        {
            _distribution = new Uniform(double.MinValue, double.MaxValue);
        }

        /// <summary>
        /// Constructs a quantile prior. 
        /// </summary>
        /// <param name="alpha">The quantile exceedance probability.</param>
        /// <param name="distribution">The quantile prior distribution.</param>
        public QuantilePrior(double alpha, UnivariateDistributionBase distribution)
        {
            Alpha = alpha;
            Distribution = distribution;
        }

        /// <summary>
        /// Constructs a quantile prior from XElement.
        /// </summary>
        /// <param name="xElement">The XElement to deserialize.</param>
        public QuantilePrior(XElement xElement)
        {
            var alphaAttr = xElement.Attribute(nameof(Alpha));
            if (alphaAttr != null) double.TryParse(alphaAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out _alpha);
            var distElement = xElement.Element("Distribution");
            if (distElement != null)
                _distribution = UnivariateDistributionFactory.CreateDistribution(distElement);
        }

        /// <summary>
        /// Occurs when a quantile prior property changes.
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;
        private double _alpha;
        private UnivariateDistributionBase _distribution = null!;

        /// <summary>
        /// The quantile exceedance probability.
        /// </summary>
        public double Alpha
        {
            get { return _alpha; }
            set
            {
                if (_alpha != value)
                {
                    _alpha = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Alpha)));
                }
            }
        }

        /// <summary>
        /// The quantile prior distribution. 
        /// </summary>
        public UnivariateDistributionBase Distribution
        {
            get { return _distribution; }
            set
            {
                if (_distribution != value)
                {
                    _distribution = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Distribution)));
                }
            }
        }

        /// <summary>
        /// Gets the 95th percentile of the distribution.
        /// </summary>
        public double UpperValue => Distribution.InverseCDF(0.95);

        /// <summary>
        /// Gets the 5th percentile of the distribution.
        /// </summary>
        public double LowerValue => Distribution.InverseCDF(0.05);

        /// <summary>
        /// Gets the mean of the distribution.
        /// </summary>
        public double MeanValue => Distribution.Mean;

        /// <summary>
        /// Create a deep copy of the quantile prior.
        /// </summary>
        public QuantilePrior Clone()
        {
            return new QuantilePrior(Alpha, Distribution.Clone());
        }

        /// <summary>
        /// Returns the quantile prior as XElement.
        /// </summary>
        public XElement ToXElement()
        {
            var result = new XElement(nameof(QuantilePrior));
            result.SetAttributeValue(nameof(Alpha), Alpha.ToString("G17", CultureInfo.InvariantCulture));
            result.Add(Distribution.ToXElement());
            return result;
        }

        /// <summary>
        /// Validates the current state of the object and reports any issues found.
        /// </summary>
        /// <returns>
        /// A tuple containing:
        /// <list type="bullet">
        /// <item>
        /// <description><c>IsValid</c>: <c>true</c> if the object passes all validation checks; otherwise <c>false</c>.</description>
        /// </item>
        /// <item>
        /// <description><c>ValidationMessages</c>: a list of messages describing any validation errors or warnings. This list is empty when the object is valid.</description>
        /// </item>
        /// </list>
        /// </returns>
        public (bool IsValid, List<string> ValidationMessages) Validate()
        {
            bool isValid = true;
            var messageList = new List<string>();

            if (!Tools.IsFinite(Alpha) || Alpha <= 0 || Alpha >= 1)
            {
                isValid = false;
                messageList.Add("Error: The exceedance probability α (alpha) must be between 0 and 1.");
            }
            if (Distribution.ValidateParameters(Distribution.GetParameters, false) != null)
            {
                isValid = false;
                messageList.Add("Error: The quantile prior distribution is invalid.");
            }
            return (isValid, messageList);
        }
    }
}
