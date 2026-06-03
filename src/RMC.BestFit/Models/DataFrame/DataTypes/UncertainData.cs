using Numerics;
using Numerics.Distributions;
using System.Collections.Generic;
using System.Globalization;
using System.Xml.Linq;

namespace RMC.BestFit
{
    /// <summary>
    /// Uncertain data ordinate.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// </remarks>
    public class UncertainData : Data
    {

        /// <summary>
        /// Construct an empty uncertain data ordinate
        /// </summary>
        public UncertainData() { }

        /// <summary>
        /// Constructs a new uncertain data ordinate.
        /// </summary>
        /// <param name="index">The index of the data ordinate.</param>
        /// <param name="distribution">The uncertain data distribution.</param>
        /// <param name="plottingPosition">Optional. The plotting position of the data ordinate. Default = 0.</param>
        public UncertainData(int index, UnivariateDistributionBase distribution, double plottingPosition = 0) : base(index, distribution.Mean, plottingPosition)
        {
            _index = index;
            _distribution = distribution;
            _value = distribution.Mean;
            _plottingPosition = plottingPosition;
        }

        /// <summary>
        /// Deserializes the XElement to an ordinate. 
        /// </summary>
        /// <param name="xElement">The XElement that will be deserialized.</param>
        public UncertainData(XElement xElement)
        {
            var indexAttr = xElement.Attribute(nameof(Index));
            if (indexAttr != null) int.TryParse(indexAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out _index);
            var distributionElement = xElement.Element(nameof(Distribution));
            if (distributionElement != null)
            {
                _distribution = UnivariateDistributionFactory.CreateDistribution(distributionElement);
                _value = _distribution.Mean;
            }
            var plottingPositionAttr = xElement.Attribute(nameof(PlottingPosition));
            if (plottingPositionAttr != null) double.TryParse(plottingPositionAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out _plottingPosition);
        }

        private UnivariateDistributionBase _distribution = new Normal();

        /// <summary>
        /// The uncertain data distribution. 
        /// </summary>
        public UnivariateDistributionBase Distribution
        {
            get { return _distribution; }
            set
            {
                _distribution = value;
                _value = _distribution.Mean;
                RaisePropertyChanged(nameof(Distribution));
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
        /// Returns the log base 10 transform of the upper value. 
        /// </summary>
        public double Log10UpperValue => Tools.Log10(UpperValue);

        /// <summary>
        /// Returns the log base 10 transform of the lower value. 
        /// </summary>
        public double Log10LowerValue => Tools.Log10(LowerValue); 
        
        /// <summary>
        /// Gets the mean of the distribution.
        /// </summary>
        public override double Value
        {
            get { return _distribution.Mean; }
            set
            {
                // UncertainData value is determined by the distribution's mean.
                // Setting the value directly is not supported - use the Distribution property instead.
                // This setter exists for interface compatibility but does not change the underlying value.
                System.Diagnostics.Debug.WriteLine("Warning: Attempting to set Value directly on UncertainData. Use Distribution property instead.");
                _value = _distribution.Mean;
                RaisePropertyChanged(nameof(Value));
            }
        }

        /// <summary>
        /// Validates the current state of the uncertain data and reports any issues found.
        /// </summary>
        /// <returns>
        /// A tuple containing:
        /// <list type="bullet">
        /// <item>
        /// <description><c>IsValid</c>: <c>true</c> if the data passes all validation checks; otherwise <c>false</c>.</description>
        /// </item>
        /// <item>
        /// <description><c>ValidationMessages</c>: a list of messages describing any validation errors or warnings. This list is empty when the data is valid.</description>
        /// </item>
        /// </list>
        /// </returns>
        public (bool IsValid, List<string> ValidationMessages) Validate()
        {
            var messages = new List<string>();
            bool isValid = true;

            if (Index < -100000 || Index > 100000)
            {
                messages.Add("Error: The index must be between -100,000 and +100,000.");
                isValid = false;
            }

            var paramValidation = Distribution.ValidateParameters(Distribution.GetParameters, false);
            if (paramValidation != null)
            {
                messages.Add("Error: The distribution parameters are invalid.");
                isValid = false;
            }

            return (isValid, messages);
        }

        /// <summary>
        /// Returns a copy of the data ordinate.
        /// </summary>
        public new virtual UncertainData Clone()
        {
            return new UncertainData(Index, Distribution.Clone(), PlottingPosition);
        }

        /// <summary>
        /// Serializes the ordinate to XElement. 
        /// </summary>
        public XElement ToXElement()
        {
            var result = new XElement(nameof(UncertainData));
            result.SetAttributeValue(nameof(Index), Index.ToString(CultureInfo.InvariantCulture));
            result.SetAttributeValue(nameof(PlottingPosition), PlottingPosition.ToString("G17", CultureInfo.InvariantCulture));
            result.Add(Distribution.ToXElement());
            return result;
        }
    }
}
