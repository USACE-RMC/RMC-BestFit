using Numerics;
using System.Collections.Generic;
using System.Globalization;
using System.Xml.Linq;

namespace RMC.BestFit.Models
{

    /// <summary>
    /// Interval censored data ordinate.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// </remarks>
    public class IntervalData : Data
    {

        /// <summary>
        /// Construct an empty interval censored data ordinate
        /// </summary>
        public IntervalData() { }

        /// <summary>
        /// Constructs a new interval censored data ordinate.
        /// </summary>
        /// <param name="index">The index of the data ordinate.</param>
        /// <param name="lowerValue">The lower bound of the interval.</param>
        /// <param name="value">The most likely value.</param>
        /// <param name="upperValue">The upper bound of the interval.</param>
        /// <param name="plottingPosition">Optional. The plotting position of the data ordinate. Default = 0.</param>
        public IntervalData(int index, double lowerValue, double value, double upperValue, double plottingPosition = 0) : base(index, value, plottingPosition)
        {
            _index = index;
            _lowerValue = lowerValue;
            _value = value;
            _upperValue = upperValue;
            _plottingPosition = plottingPosition;
        }

        /// <summary>
        /// Deserializes the XElement to an ordinate. 
        /// </summary>
        /// <param name="xElement">The XElement that will be deserialized.</param>
        public IntervalData(XElement xElement)
        {
            var indexAttr = xElement.Attribute(nameof(Index));
            if (indexAttr != null) int.TryParse(indexAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out _index);
            var lowerValueAttr = xElement.Attribute(nameof(LowerValue));
            if (lowerValueAttr != null) double.TryParse(lowerValueAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out _lowerValue);
            var valueAttr = xElement.Attribute(nameof(Value));
            if (valueAttr != null) double.TryParse(valueAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out _value);
            var upperValueAttr = xElement.Attribute(nameof(UpperValue));
            if (upperValueAttr != null) double.TryParse(upperValueAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out _upperValue);
            var plottingPositionAttr = xElement.Attribute(nameof(PlottingPosition));
            if (plottingPositionAttr != null) double.TryParse(plottingPositionAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out _plottingPosition);
        }

        private double _lowerValue;
        private double _upperValue;

        /// <summary>
        /// The lower bound of the interval.
        /// </summary>
        public double LowerValue
        {
            get { return _lowerValue; }
            set
            {
                if (_lowerValue != value)
                {
                    _lowerValue = value;
                    RaisePropertyChanged(nameof(LowerValue));
                }
            }
        }

        /// <summary>
        /// The upper bound of the interval.
        /// </summary>
        public double UpperValue
        {
            get { return _upperValue; }
            set
            {
                if (_upperValue != value)
                {
                    _upperValue = value;
                    RaisePropertyChanged(nameof(UpperValue));
                }
            }
        }

        /// <summary>
        /// Returns the log base 10 transform of the upper value. 
        /// </summary>
        public double Log10UpperValue => Tools.Log10(UpperValue);

        /// <summary>
        /// Returns the log base 10 transform of the lower value. 
        /// </summary>
        public double Log10LowerValue => Tools.Log10(LowerValue);

        /// <summary>
        /// Validates the current state of the interval data and reports any issues found.
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

            if (double.IsNaN(LowerValue) || double.IsInfinity(LowerValue))
            {
                messages.Add("Error: The lower value must be a number.");
                isValid = false;
            }

            if (double.IsNaN(Value) || double.IsInfinity(Value))
            {
                messages.Add("Error: The most likely value must be a number.");
                isValid = false;
            }

            if (double.IsNaN(UpperValue) || double.IsInfinity(UpperValue))
            {
                messages.Add("Error: The upper value must be a number.");
                isValid = false;
            }

            if (LowerValue >= Value)
            {
                messages.Add("Error: The lower value must be less than the most likely value.");
                isValid = false;
            }

            if (Value >= UpperValue)
            {
                messages.Add("Error: The upper value must be greater than the most likely value.");
                isValid = false;
            }

            if (LowerValue >= UpperValue)
            {
                messages.Add("Error: The lower value must be less than the upper value.");
                isValid = false;
            }

            return (isValid, messages);
        }

        /// <summary>
        /// Returns a copy of the data ordinate.
        /// </summary>
        public new virtual IntervalData Clone()
        {
            return new IntervalData(Index, LowerValue, Value, UpperValue, PlottingPosition);
        }

        /// <summary>
        /// Serializes the ordinate to XElement. 
        /// </summary>
        public XElement ToXElement()
        {
            var result = new XElement(nameof(IntervalData));
            result.SetAttributeValue(nameof(Index), Index.ToString(CultureInfo.InvariantCulture));
            result.SetAttributeValue(nameof(LowerValue), LowerValue.ToString("G17", CultureInfo.InvariantCulture));
            result.SetAttributeValue(nameof(Value), Value.ToString("G17", CultureInfo.InvariantCulture));
            result.SetAttributeValue(nameof(UpperValue), UpperValue.ToString("G17", CultureInfo.InvariantCulture));
            result.SetAttributeValue(nameof(PlottingPosition), PlottingPosition.ToString("G17", CultureInfo.InvariantCulture));
            return result;
        }

    }
}
