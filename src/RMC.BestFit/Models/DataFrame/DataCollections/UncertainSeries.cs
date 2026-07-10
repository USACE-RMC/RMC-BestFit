using Numerics.Data;
using RMC.BestFit.Models;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace RMC.BestFit.Models
{
    /// <summary>
    /// Uncertain data series class.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// </remarks>
    public class UncertainSeries : DataSeries
    {
        #region Construction

        /// <summary>
        /// Constructs an empty series. 
        /// </summary>
        public UncertainSeries() { }

        /// <summary>
        /// Constructs series based on list. 
        /// </summary>
        /// <param name="data">List of data.</param>
        public UncertainSeries(IList<UncertainData> data)
        {
            for (int i = 0; i < data.Count; i++)
                Add(data[i].Clone());
        }

        /// <summary>
        /// Deserializes the XElement to a series. 
        /// </summary>
        /// <param name="xElement">The XElement that will be deserialized.</param>
        public UncertainSeries(XElement xElement)
        {
            foreach (XElement el in xElement.Elements(nameof(UncertainData)))
                Add(new UncertainData(el));
        }

        #endregion

        #region Methods

        /// <summary>
        /// Gets the minimum value of the series.
        /// </summary>
        public double MinimumValue()
        {
            if (Count == 0) return double.MaxValue;
            return _seriesOrdinates.Min(x => ((UncertainData)x).LowerValue);
        }

        /// <summary>
        /// Gets the maximum value of the series.
        /// </summary>
        public double MaximumValue()
        {
            if (Count == 0) return double.MinValue;
            return _seriesOrdinates.Max(x => ((UncertainData)x).UpperValue);
        }

        /// <summary>
        /// Gets the minimum index of the series.
        /// </summary>
        public int MinimumIndex()
        {
            if (Count == 0) return -100000;
            return _seriesOrdinates.Min(x => x.Index);
        }

        /// <summary>
        /// Gets the maximum index of the series.
        /// </summary>
        public int MaximumIndex()
        {
            if (Count == 0) return 100000;
            return _seriesOrdinates.Max(x => x.Index);
        }

        /// <summary>
        /// Returns the series as a List.
        /// </summary>
        public List<UncertainData> ToList()
        {
            var result = new List<UncertainData>();
            for (int i = 0; i < Count; i++)
                result.Add(((UncertainData)this[i]).Clone());
            return result;
        }

        /// <summary>
        /// Create a clone of the series. 
        /// </summary>
        public UncertainSeries Clone()
        {
            return new UncertainSeries(ToList());
        }

        /// <summary>
        /// Sorts the elements in the collection by index. 
        /// </summary>
        /// <param name="order">Ascending or descending order. Default = ascending.</param>
        public void SortByIndex(SortOrder order = SortOrder.Ascending)
        {
            if (order == SortOrder.Ascending)
            {
                _seriesOrdinates.Sort((x, y) => x.Index.CompareTo(y.Index));
            }
            else
            {
                _seriesOrdinates.Sort((x, y) => -x.Index.CompareTo(y.Index));
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
        /// Validates the current state of the uncertain series and reports any issues found.
        /// </summary>
        /// <param name="dataFrame">The data frame to cross reference.</param>
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
        public (bool IsValid, List<string> ValidationMessages) Validate(DataFrame dataFrame)
        {
            var messages = new List<string>();
            bool isValid = true;

            for (int i = 0; i < Count; i++)
            {
                var dataValid = ((UncertainData)this[i]).Validate();
                if (!dataValid.IsValid)
                {
                    messages.AddRange(dataValid.ValidationMessages);
                    isValid = false;
                }

                // Check for duplicate indexes
                var duplicates = this.GroupBy(d => d.Index)
                                     .Where(g => g.Count() > 1)
                                     .Select(g => g.Key)
                                     .ToList();

                if (duplicates.Count > 0)
                {
                    messages.Add($"Error: Duplicate indexes found in uncertain series: {string.Join(", ", duplicates)}");
                    isValid = false;
                }

                // Check for overlaps with exact series
                if (dataFrame != null)
                {
                    var exactIndexes = new HashSet<int>(dataFrame.ExactSeries.Select(d => d.Index));

                    foreach (var uncertain in this)
                    {
                        if (exactIndexes.Contains(uncertain.Index))
                        {
                            messages.Add($"Error: Uncertain data at index {uncertain.Index} overlaps with exact data.");
                            isValid = false;
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
            var result = new XElement(nameof(UncertainSeries));
            for (int i = 0; i < Count; i++)
                result.Add(((UncertainData)this[i]).ToXElement());
            return result;
        }

        #endregion

    }
}
