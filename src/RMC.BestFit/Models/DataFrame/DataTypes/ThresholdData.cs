using System.Collections.Generic;
using System.Globalization;
using System.Xml.Linq;

namespace RMC.BestFit.Models
{

    /// <summary>
    /// Threshold censored data ordinate.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// </remarks>
    public class ThresholdData : Data
    {

        /// <summary>
        /// Construct an empty threshold censored data ordinate
        /// </summary>
        public ThresholdData() { }

        /// <summary>
        /// Constructs a new threshold censored data ordinate
        /// </summary>
        /// <param name="startIndex">The start index of the threshold.</param>
        /// <param name="endIndex">The end index of the threshold.</param>
        /// <param name="value">The value threshold.</param>
        public ThresholdData(int startIndex, int endIndex, double value) : base(startIndex, value)
        {
            _startIndex = startIndex;
            _endIndex = endIndex;
            _value = value;
        }

        /// <summary>
        /// Deserializes the XElement to an ordinate. 
        /// </summary>
        /// <param name="xElement">The XElement that will be deserialized.</param>
        public ThresholdData(XElement xElement)
        {
            var startIndexAttr = xElement.Attribute(nameof(StartIndex));
            if (startIndexAttr != null) int.TryParse(startIndexAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out _startIndex);
            var endIndexAttr = xElement.Attribute(nameof(EndIndex));
            if (endIndexAttr != null) int.TryParse(endIndexAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out _endIndex);
            var valueAttr = xElement.Attribute(nameof(Value));
            if (valueAttr != null) double.TryParse(valueAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out _value);
            var numberBelowAttr = xElement.Attribute(nameof(NumberBelow));
            if (numberBelowAttr != null) int.TryParse(numberBelowAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out _numberBelow);
            var numberAboveAttr = xElement.Attribute(nameof(NumberAbove));
            if (numberAboveAttr != null) int.TryParse(numberAboveAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out _numberAbove);
            var plottingPositionAttr = xElement.Attribute(nameof(PlottingPosition));
            if (plottingPositionAttr != null) double.TryParse(plottingPositionAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out _plottingPosition);
            Index = StartIndex;
        }

        private int _startIndex;
        private int _endIndex;
        private int _numberBelow = 0;
        private int _numberAbove = 0;

        /// <summary>
        /// The start index of the threshold.
        /// </summary>
        public int StartIndex
        {
            get { return _startIndex; }
            set
            {
                if (_startIndex != value)
                {
                    _index = value;
                    _startIndex = value;
                    RaisePropertyChanged(nameof(StartIndex));
                }
            }
        }

        /// <summary>
        /// The end index of the threshold.
        /// </summary>
        public int EndIndex
        {
            get { return _endIndex; }
            set
            {
                if (_endIndex != value)
                {
                    _endIndex = value;
                    RaisePropertyChanged(nameof(EndIndex));
                }
            }
        }

        /// <summary>
        /// The number of data points below the threshold during the threshold window.
        /// </summary>
        /// <remarks>
        /// <b>Derived value — not user-settable.</b>
        /// Automatically computed by <see cref="DataFrame.ProcessThresholdSeries"/> as
        /// <c>Duration - NumberAbove - (overlapping exact / interval / uncertain data points)</c>
        /// whenever a threshold is added to <see cref="DataFrame.ThresholdSeries"/> or any
        /// data series changes. Users should only set <see cref="NumberAbove"/>; this
        /// property refreshes on the next <c>CalculatePlottingPositions</c> pass.
        /// The <c>internal set</c> is reserved for <see cref="DataFrame"/> internal use and
        /// for XML deserialization.
        /// </remarks>
        public int NumberBelow
        {
            get { return _numberBelow; }
            internal set { _numberBelow = value; }
        }

        /// <summary>
        /// The number of data points above the threshold.
        /// </summary>
        public int NumberAbove
        {
            get { return _numberAbove; }
            set
            {
                if (_numberAbove != value)
                {
                    _numberAbove = value;
                    RaisePropertyChanged(nameof(NumberAbove));
                }
            }
        }

        /// <summary>
        /// Returns the duration of the threshold.
        /// </summary>
        public int Duration
        {
            get { return EndIndex - StartIndex + 1; }
        }

        /// <summary>
        /// Validates the current state of the threshold data and reports any issues found.
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

            if (StartIndex < -100000 || StartIndex > 100000)
            {
                messages.Add("Error: The start index must be between -100,000 and +100,000.");
                isValid = false;
            }

            if (EndIndex < -100000 || EndIndex > 100000)
            {
                messages.Add("Error: The end index must be between -100,000 and +100,000.");
                isValid = false;
            }

            if (StartIndex > EndIndex)
            {
                messages.Add("Error: The start index must be less than or equal to the end index.");
                isValid = false;
            }

            if (double.IsNaN(Value) || double.IsInfinity(Value))
            {
                messages.Add("Error: The value must be a number.");
                isValid = false;
            }

            if (NumberBelow < 0)
            {
                messages.Add("Error: The number below cannot be negative.");
                isValid = false;
            }

            if (NumberAbove < 0)
            {
                messages.Add("Error: The number above cannot be negative.");
                isValid = false;
            }

            if (NumberAbove > Duration)
            {
                messages.Add("Error: The number above must be less than or equal to the threshold duration.");
                isValid = false;
            }

            if (NumberBelow + NumberAbove > Duration)
            {
                messages.Add("Error: The sum of number above and number below cannot exceed the threshold duration.");
                isValid = false;
            }

            return (isValid, messages);
        }

        /// <summary>
        /// Returns a copy of the data ordinate.
        /// </summary>
        public new virtual ThresholdData Clone()
        {
            return new ThresholdData(StartIndex, EndIndex, Value) { NumberBelow = NumberBelow, NumberAbove = NumberAbove, PlottingPosition = PlottingPosition };
        }

        /// <summary>
        /// Serializes the ordinate to XElement. 
        /// </summary>
        public XElement ToXElement()
        {
            var result = new XElement(nameof(ThresholdData));
            result.SetAttributeValue(nameof(StartIndex), StartIndex.ToString(CultureInfo.InvariantCulture));
            result.SetAttributeValue(nameof(EndIndex), EndIndex.ToString(CultureInfo.InvariantCulture));
            result.SetAttributeValue(nameof(Value), Value.ToString("G17", CultureInfo.InvariantCulture));
            result.SetAttributeValue(nameof(NumberBelow), NumberBelow.ToString(CultureInfo.InvariantCulture));
            result.SetAttributeValue(nameof(NumberAbove), NumberAbove.ToString(CultureInfo.InvariantCulture));
            result.SetAttributeValue(nameof(PlottingPosition), PlottingPosition.ToString("G17", CultureInfo.InvariantCulture));
            return result;
        }

    }
}
