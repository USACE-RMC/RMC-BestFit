using Numerics.Data;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace RMC.BestFit.Models
{
    /// <summary>
    /// Threshold data series class.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// </remarks>
    public class ThresholdSeries : DataSeries
    {
        #region Construction

        /// <summary>
        /// Constructs an empty series. 
        /// </summary>
        public ThresholdSeries() { }

        /// <summary>
        /// Constructs series based on list. 
        /// </summary>
        /// <param name="data">List of data.</param>
        public ThresholdSeries(IList<ThresholdData> data)
        {
            for (int i = 0; i < data.Count; i++)
                Add(data[i].Clone());
        }

        /// <summary>
        /// Deserializes the XElement to a series. 
        /// </summary>
        /// <param name="xElement">The XElement that will be deserialized.</param>
        public ThresholdSeries(XElement xElement)
        {
            foreach (XElement el in xElement.Elements(nameof(ThresholdData)))
                Add(new ThresholdData(el));
        }

        #endregion

        #region Methods
 
        /// <summary>
        /// Gets the minimum value of the series.
        /// </summary>
        public double MinimumValue()
        {
            if (Count == 0) return double.MaxValue;
            return _seriesOrdinates.Min(x => x.Value);
        }

        /// <summary>
        /// Gets the maximum value of the series.
        /// </summary>
        public double MaximumValue()
        {
            if (Count == 0) return double.MinValue;
            return _seriesOrdinates.Max(x => x.Value);
        }

        /// <summary>
        /// Gets the minimum index of the series.
        /// </summary>
        public int MinimumIndex()
        {
            if (Count == 0) return -100000;
            return _seriesOrdinates.Min(x => ((ThresholdData)x).StartIndex);
        }

        /// <summary>
        /// Gets the maximum index of the series.
        /// </summary>
        public int MaximumIndex()
        {
            if (Count == 0) return 100000;
            return _seriesOrdinates.Max(x => ((ThresholdData)x).EndIndex);
        }

        /// <summary>
        /// Returns the series as a List.
        /// </summary>
        public List<ThresholdData> ToList()
        {
            var result = new List<ThresholdData>();
            for (int i = 0; i < Count; i++)
                result.Add(((ThresholdData)this[i]).Clone());         
            return result;
        }

        /// <summary>
        /// Create a clone of the series. 
        /// </summary>
        public ThresholdSeries Clone()
        {
            return new ThresholdSeries(ToList());
        }

        /// <summary>
        /// Sorts the elements in the collection by index. 
        /// </summary>
        /// <param name="order">Ascending or descending order. Default = ascending.</param>
        public void SortByIndex(SortOrder order = SortOrder.Ascending)
        {
            if (order == SortOrder.Ascending)
            {
                _seriesOrdinates.Sort((x, y) => ((ThresholdData)x).StartIndex.CompareTo(((ThresholdData)y).StartIndex));
            }
            else
            {
                _seriesOrdinates.Sort((x, y) => -((ThresholdData)x).StartIndex.CompareTo(((ThresholdData)y).StartIndex));
            }
        }

        /// <summary>
        /// Sorts the elements in the collection by value.
        /// </summary>
        /// <param name="order">Ascending or descending order. Default = ascending.</param>
        public void Sort(SortOrder order = SortOrder.Ascending)
        {
            if (order == SortOrder.Ascending)
            {
                _seriesOrdinates.Sort((x, y) => x.Value.CompareTo(y.Value));
            }
            else
            {
                _seriesOrdinates.Sort((x, y) => -x.Value.CompareTo(y.Value));
            }
        }

        /// <summary>
        /// Validates the current state of the threshold series and reports any issues found.
        /// </summary>
        /// <returns>
        /// A tuple containing:
        /// <list type="bullet">
        /// <item>
        /// <description><c>IsValid</c>: <c>true</c> if the series passes all validation checks; otherwise <c>false</c>.</description>
        /// </item>
        /// <item>
        /// <description><c>ValidationMessages</c>: a list of messages describing any validation errors or warnings. This list is empty when the series is valid.</description>
        /// </item>
        /// </list>
        /// </returns>
        public (bool IsValid, List<string> ValidationMessages) Validate()
        {
            var messages = new List<string>();
            bool isValid = true;

            for (int i = 0; i < Count; i++)
            {
                var dataValid = ((ThresholdData)this[i]).Validate();
                if (!dataValid.IsValid)
                {
                    messages.AddRange(dataValid.ValidationMessages);
                    isValid = false;
                }

                // Check for overlapping thresholds
                for (int j = 0; j < Count; j++)
                {
                    if (i != j)
                    {
                        if (((ThresholdData)this[i]).StartIndex >= ((ThresholdData)this[j]).StartIndex &&
                            ((ThresholdData)this[i]).StartIndex <= ((ThresholdData)this[j]).EndIndex)
                        {
                            messages.Add("Error: The threshold periods cannot overlap.");
                            isValid = false;
                            break;
                        }
                    }
                }
            }

            return (isValid, messages);
        }

        /// <summary>
        /// Serializes the data series to XElement. 
        /// </summary>
        public XElement ToXElement()
        {
            var result = new XElement(nameof(ThresholdSeries));
            for (int i = 0; i < Count; i++)
                result.Add(((ThresholdData)this[i]).ToXElement());
            return result;
        }

        #endregion
    }
}
