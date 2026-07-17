using Numerics.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace RMC.BestFit.Models
{

    /// <summary>
    /// Exact data series class.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// </remarks>
    public class ExactSeries : DataSeries
    {
        #region Construction

        /// <summary>
        /// Constructs an empty series. 
        /// </summary>
        public ExactSeries() { }

        /// <summary>
        /// Constructs series based on list. 
        /// </summary>
        /// <param name="data">List of data.</param>
        public ExactSeries(IList<ExactData> data)
        {
            for (int i = 0; i < data.Count; i++)
                Add(data[i].Clone());
        }

        /// <summary>
        /// Constructs series based on list of values. 
        /// </summary>
        /// <param name="values">List of values.</param>
        public ExactSeries(IList<double> values)
        {
            for (int i = 0; i < values.Count; i++)
                Add(new ExactData(i, values[i]));
        }

        /// <summary>
        /// Deserializes the XElement to a series. 
        /// </summary>
        /// <param name="xElement">The XElement that will be deserialized.</param>
        public ExactSeries(XElement xElement)
        {
            foreach (XElement el in xElement.Elements(nameof(ExactData)))
                Add(new ExactData(el));
        }

       
        /// <summary>
        /// Determines whether or not to enforce a unique index requirement. 
        /// </summary>
        public bool EnforceUniqueIndex { get; set; } = false;

        #endregion

        /// <summary>
        /// Gets the median value of the series.
        /// For odd-count series, returns the middle value.
        /// For even-count series, returns the average of the two middle values.
        /// </summary>
        public double MedianValue
        {
            get
            {
                if (Count == 0) return 0;
                if (Count == 1) return _seriesOrdinates[0].Value;
                var data = _seriesOrdinates.Select(x => x.Value).ToList();
                data.Sort();
                int mid = Count / 2;
                if (Count % 2 == 0)
                    return (data[mid - 1] + data[mid]) / 2.0;
                else
                    return data[mid];
            }
        }

        /// <summary>
        /// Gets the smallest value in the upper half of the sorted series.
        /// For even-count series, returns the value at index Count/2 (the lower of the two middle values is at Count/2 - 1).
        /// For odd-count series, returns the middle value (same as MedianValue).
        /// </summary>
        /// <remarks>
        /// This property is used as the upper bound for the low outlier threshold validation.
        /// Using the interpolated median as the bound would reject valid thresholds that are actual data values
        /// at or near the center, because the interpolated median can fall between two data points.
        /// The upper-middle value ensures that any threshold set to an actual data value at or below the
        /// 50th percentile position will pass validation.
        /// </remarks>
        public double UpperMiddleValue
        {
            get
            {
                if (Count == 0) return 0;
                if (Count == 1) return _seriesOrdinates[0].Value;
                var data = _seriesOrdinates.Select(x => x.Value).ToList();
                data.Sort();
                return data[Count / 2];
            }
        }

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
        /// Gets the number of unique indexes.
        /// </summary>
        public int UniqueIndices()
        {
            return IndicesToList().Distinct().Count();
        }

        /// <summary>
        /// Gets the index span. 
        /// </summary>
        public int IndexSpan()
        {
            if (Count == 0) return 0;
            return MaximumIndex() - MinimumIndex() + 1;
        }

        /// <summary>
        /// Returns the series as a List.
        /// </summary>
        public List<ExactData> ToList()
        {
            var result = new List<ExactData>();
            for (int i = 0; i < Count; i++)
                result.Add(((ExactData)this[i]).Clone());
            return result;
        }

        /// <summary>
        /// Create a clone of the series. 
        /// </summary>
        public ExactSeries Clone()
        {
            return new ExactSeries(ToList());
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
        /// Validates the current state of the exact series and reports any issues found.
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
                var dataValid = ((ExactData)this[i]).Validate();
                if (!dataValid.IsValid)
                {
                    messages.AddRange(dataValid.ValidationMessages);
                    isValid = false;
                }
            }

            // Check for duplicate indexes once after the loop (not per-item)
            if (EnforceUniqueIndex)
            {
                var duplicates = this.GroupBy(d => d.Index)
                                     .Where(g => g.Count() > 1)
                                     .Select(g => g.Key)
                                     .ToList();

                if (duplicates.Count > 0)
                {
                    messages.Add($"Error: Duplicate indexes found: {string.Join(", ", duplicates)}");
                    isValid = false;
                }
            }

            return (isValid, messages);
        }

        /// <summary>
        /// Returns the autocorrelation function
        /// </summary>
        /// <returns>A matrix containing autocorrelation values by lag.</returns>
        public double[,] Autocorrelation()
        {
            var data = ValuesToArray();
            return Numerics.Data.Statistics.Autocorrelation.Function(data)!;
        }

        /// <summary>
        /// Returns the partial autocorrelation function.
        /// </summary>
        /// <returns>A matrix containing partial autocorrelation values by lag.</returns>
        public double[,] PartialAutocorrelation()
        {
            var data = ValuesToArray();
            return Numerics.Data.Statistics.Autocorrelation.Function(data, -1, Numerics.Data.Statistics.Autocorrelation.Type.Partial)!;
        }

        /// <summary>
        /// Serializes the data series to XElement. 
        /// </summary>
        public XElement ToXElement()
        {
            var result = new XElement(nameof(ExactSeries));
            for (int i = 0; i < Count; i++)
                result.Add(((ExactData)this[i]).ToXElement());
            return result;
        }

        #endregion

    }
}
