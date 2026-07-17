using Numerics.Data;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml.Linq;

namespace RMC.BestFit.Models
{

    /// <summary>
    /// Exact data ordinate.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// </remarks>
    public class ExactData : Data
    {

        /// <summary>
        /// Construct an empty exact data ordinate.
        /// </summary>
        public ExactData() { }

        /// <summary>
        /// Constructs a new exact data ordinate.
        /// </summary>
        /// <param name="dateTime">The date of the data ordinate.</param>
        /// <param name="value">The value of the data ordinate.</param>
        public ExactData(DateTime dateTime, double value) : base(dateTime.Year, value, 0) 
        {
            _dateTime = dateTime;
            _index = dateTime.Year;
            _value = value;
            _plottingPosition = 0;
            _isLowOutlier = false;
        }

        /// <summary>
        /// Constructs a new exact data ordinate.
        /// </summary>
        /// <param name="index">The index of the data ordinate.</param>
        /// <param name="value">The value of the data ordinate.</param>
        /// <param name="plottingPosition">Optional. The plotting position of the data ordinate. Default = 0.</param>
        /// <param name="isLowOutlier">Optional. Determines if the data ordinate is a low outlier. Default = False.</param>
        public ExactData(int index, double value, double plottingPosition = 0d, bool isLowOutlier = false) : base(index, value, plottingPosition)
        {
            _index = index;
            _value = value;
            _plottingPosition = plottingPosition;
            _isLowOutlier = isLowOutlier;
        }

        /// <summary>
        /// Deserializes the XElement to an ordinate. 
        /// </summary>
        /// <param name="xElement">The XElement that will be deserialized.</param>
        public ExactData(XElement xElement)
        {
            var indexAttr = xElement.Attribute(nameof(Index));
            if (indexAttr != null) int.TryParse(indexAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out _index);
            var valueAttr = xElement.Attribute(nameof(Value));
            if (valueAttr != null) double.TryParse(valueAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out _value);
            var plottingPositionAttr = xElement.Attribute(nameof(PlottingPosition));
            if (plottingPositionAttr != null) double.TryParse(plottingPositionAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out _plottingPosition);
            var isLowOutlierAttr = xElement.Attribute(nameof(IsLowOutlier));
            if (isLowOutlierAttr != null) bool.TryParse(isLowOutlierAttr.Value, out _isLowOutlier);
            var dateTimeAttr = xElement.Attribute(nameof(DateTime));
            if (dateTimeAttr != null)
            {
                // Try to parse the invariant date string using TryParseExact
                // If it fails, do a regular try parse.
                if (!DateTime.TryParseExact(dateTimeAttr.Value, "o", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _dateTime))
                {
                    DateTime.TryParse(dateTimeAttr.Value, out _dateTime);
                }
            }
        }

        private DateTime _dateTime;
        private bool _isLowOutlier;

        /// <summary>
        /// The date-time of the exact data value. 
        /// </summary>
        public DateTime DateTime => _dateTime;

        /// <summary>
        /// Determines whether the data value is a low outlier or not. 
        /// </summary>
        public bool IsLowOutlier
        {
            get { return _isLowOutlier; }
            set
            {
                if (_isLowOutlier != value)
                {
                    _isLowOutlier = value;
                    RaisePropertyChanged(nameof(IsLowOutlier));
                }
            }
        }

        /// <summary>
        /// Validates the current state of the exact data and reports any issues found.
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

            if (double.IsNaN(Value) || double.IsInfinity(Value))
            {
                messages.Add("Error: The value must be a number.");
                isValid = false;
            }

            return (isValid, messages);
        }

        /// <summary>
        /// Returns a copy of the data ordinate.
        /// </summary>
        public new virtual ExactData Clone()
        {
            return new ExactData(Index, Value, PlottingPosition, IsLowOutlier) { _dateTime = DateTime };
        }

        /// <summary>
        /// Serializes the ordinate to XElement. 
        /// </summary>
        public XElement ToXElement()
        {
            var result = new XElement(nameof(ExactData));
            result.SetAttributeValue(nameof(Index), Index.ToString(CultureInfo.InvariantCulture));
            result.SetAttributeValue(nameof(Value), Value.ToString("G17", CultureInfo.InvariantCulture));
            result.SetAttributeValue(nameof(PlottingPosition), PlottingPosition.ToString("G17", CultureInfo.InvariantCulture));
            result.SetAttributeValue(nameof(IsLowOutlier), IsLowOutlier.ToString());
            result.SetAttributeValue(nameof(DateTime), DateTime.ToString("o", CultureInfo.InvariantCulture));
            return result;
        }


    }
}

