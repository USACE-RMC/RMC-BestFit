using GenericControls;
using RMC.BestFit.Models;
using RMC.BestFit.UI;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace RMC_BestFit
{
    /// <summary>
    /// Represents a data grid row item for threshold data, providing a view model wrapper for threshold observations
    /// used in statistical analysis of values exceeding specified thresholds over defined time periods.
    /// </summary>
    public class ThresholdDataRowItem : DataGridRowItem
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ThresholdDataRowItem"/> class with default values.
        /// </summary>
        public ThresholdDataRowItem() : base(null) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="ThresholdDataRowItem"/> class with the specified collection and threshold data.
        /// </summary>
        /// <param name="observableCollection">The observable collection that contains this row item.</param>
        /// <param name="ordinate">The threshold data object to be wrapped by this row item.</param>
        /// <param name="series">The data series that contains the ordinate, used for clone-and-replace on edits.</param>
        public ThresholdDataRowItem(ObservableCollection<Object> observableCollection, ThresholdData ordinate, DataSeries series) : base(observableCollection)
        {
            _collection = observableCollection;
            _ordinate = ordinate;
            _series = series;
        }

        /// <summary>
        /// The underlying threshold data model object.
        /// </summary>
        private ThresholdData _ordinate;

        /// <summary>
        /// The observable collection that contains this row item.
        /// </summary>
        private ObservableCollection<Object> _collection;

        /// <summary>
        /// The data series containing the ordinate, used for the clone-and-replace pattern.
        /// </summary>
        private DataSeries _series;

        /// <summary>
        /// Gets or sets the starting index of the threshold period.
        /// When set, creates a clone of the underlying data, modifies the clone, and replaces
        /// it in the data series to fire a CollectionChanged Replace event for undo support.
        /// </summary>
        public int StartIndex
        {
            get { return _ordinate.StartIndex; }
            set
            {
                if (_ordinate.StartIndex != value)
                {
                    var clone = _ordinate.Clone();
                    clone.StartIndex = value;
                    ReplaceOrdinate(clone);
                    NotifyPropertyChanged(nameof(StartIndex));
                }
            }
        }

        /// <summary>
        /// Gets or sets the ending index of the threshold period.
        /// When set, creates a clone of the underlying data, modifies the clone, and replaces
        /// it in the data series to fire a CollectionChanged Replace event for undo support.
        /// </summary>
        public int EndIndex
        {
            get { return _ordinate.EndIndex; }
            set
            {
                if (_ordinate.EndIndex != value)
                {
                    var clone = _ordinate.Clone();
                    clone.EndIndex = value;
                    ReplaceOrdinate(clone);
                    NotifyPropertyChanged(nameof(EndIndex));
                }
            }
        }

        /// <summary>
        /// Gets or sets the threshold value used for comparison.
        /// When set, creates a clone of the underlying data, modifies the clone, and replaces
        /// it in the data series to fire a CollectionChanged Replace event for undo support.
        /// </summary>
        public double Value
        {
            get { return _ordinate.Value; }
            set
            {
                if (_ordinate.Value != value)
                {
                    var clone = _ordinate.Clone();
                    clone.Value = value;
                    ReplaceOrdinate(clone);
                    NotifyPropertyChanged(nameof(Value));
                }
            }
        }

        /// <summary>
        /// Gets or sets the number of observations above the threshold value within the specified period.
        /// When set, creates a clone of the underlying data, modifies the clone, and replaces
        /// it in the data series to fire a CollectionChanged Replace event for undo support.
        /// </summary>
        public int NumberAbove
        {
            get { return _ordinate.NumberAbove; }
            set
            {
                if (_ordinate.NumberAbove != value)
                {
                    var clone = _ordinate.Clone();
                    clone.NumberAbove = value;
                    ReplaceOrdinate(clone);
                    NotifyPropertyChanged(nameof(NumberAbove));
                }
            }
        }

        /// <summary>
        /// Updates the underlying ordinate reference and notifies the UI of all property changes.
        /// Called by the CollectionChanged(Replace) handler during undo/redo to sync the RowItem
        /// with the restored model data without triggering a clone-and-replace back to the series.
        /// </summary>
        /// <param name="newOrdinate">The restored ordinate from the model.</param>
        public void SetOrdinate(ThresholdData newOrdinate)
        {
            _ordinate = newOrdinate;
            NotifyPropertyChanged(nameof(StartIndex));
            NotifyPropertyChanged(nameof(EndIndex));
            NotifyPropertyChanged(nameof(Value));
            NotifyPropertyChanged(nameof(NumberAbove));
        }

        /// <summary>
        /// Replaces the current ordinate in the data series with a new clone.
        /// This fires a CollectionChanged Replace event which the UndoableCollectionBridge records.
        /// </summary>
        /// <param name="newOrdinate">The cloned and modified ordinate to replace the current one.</param>
        private void ReplaceOrdinate(ThresholdData newOrdinate)
        {
            if (_series == null) return;
            int idx = _series.IndexOf(_ordinate);
            if (idx >= 0)
            {
                _series[idx] = newOrdinate;
                _ordinate = newOrdinate;
            }
        }

        /// <summary>
        /// Adds validation rules for the threshold data row item properties.
        /// Validates that periods do not overlap, indexes are within valid range and in order,
        /// values are valid numbers, and the number above does not exceed the threshold duration.
        /// </summary>
        public override void AddValidationRules()
        {
            AddRule(nameof(StartIndex), IsStartOverlapping, "The threshold periods cannot overlap.");
            AddRule(nameof(EndIndex), IsEndOverlapping, "The threshold periods cannot overlap.");
            AddRule(nameof(StartIndex), () => OrderRule<int, ThresholdDataRowItem>((x) => x.StartIndex, nameof(StartIndex), true, false), "Start indexes must be in ascending order.");
            AddRule(nameof(EndIndex), () => OrderRule<int, ThresholdDataRowItem>((x) => x.EndIndex, nameof(EndIndex), true, false), "End indexes must be in ascending order.");
            AddRule(nameof(StartIndex), () => StartIndex < -100000, "The start index must be between -100,000 and +100,000.");
            AddRule(nameof(StartIndex), () => StartIndex > 100000, "The start index must be between -100,000 and +100,000.");
            AddRule(nameof(EndIndex), () => EndIndex < -100000, "The end index must be between -100,000 and +100,000.");
            AddRule(nameof(EndIndex), () => EndIndex > 100000, "The end index must be between -100,000 and +100,000.");
            AddRule(nameof(StartIndex), () => StartIndex > EndIndex, "The start index must be less than or equal to the end index.", new[] { nameof(EndIndex) });
            AddRule(nameof(EndIndex), () => StartIndex > EndIndex, "The start index must be less than or equal to the end index.", new[] { nameof(StartIndex) });
            AddRule(nameof(Value), () => double.IsNaN(Value) || double.IsInfinity(Value), "The value must be a number.", new[] { nameof(Value) });
            AddRule(nameof(NumberAbove), () => NumberAbove > _ordinate.Duration, "The number above must be less than or equal to the threshold duration.", new[] { nameof(NumberAbove) });
        }

        /// <summary>
        /// Determines whether this threshold period overlaps with any other period in the collection.
        /// Two intervals overlap if neither ends before the other starts.
        /// </summary>
        /// <returns>True if this period overlaps with another period; otherwise, false.</returns>
        private bool IsOverlapping()
        {
            for (int i = 0; i < _collection.Count; i++)
            {
                var other = (ThresholdDataRowItem)_collection[i];
                if (other == this) continue;
                // Two intervals [a,b] and [c,d] overlap if NOT (b < c OR d < a)
                if (!(EndIndex < other.StartIndex || other.EndIndex < StartIndex))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Determines whether the start index of this threshold period overlaps with other periods in the collection.
        /// </summary>
        /// <returns>True if this period overlaps with another period; otherwise, false.</returns>
        private bool IsStartOverlapping()
        {
            return IsOverlapping();
        }

        /// <summary>
        /// Determines whether the end index of this threshold period overlaps with other periods in the collection.
        /// </summary>
        /// <returns>True if this period overlaps with another period; otherwise, false.</returns>
        private bool IsEndOverlapping()
        {
            return IsOverlapping();
        }

        /// <summary>
        /// Determines whether the specified property should be displayed in the data grid.
        /// </summary>
        /// <param name="propertyName">The name of the property to check.</param>
        /// <returns>Always returns true, indicating all properties are displayable.</returns>
        public override bool IsGridDisplayable(string propertyName)
        {
            return true;
        }

        /// <summary>
        /// Gets the display name for the specified property to be shown in the data grid header.
        /// </summary>
        /// <param name="propertyName">The name of the property.</param>
        /// <returns>The display name for the property, or null if not recognized.</returns>
        public override string PropertyDisplayName(string propertyName)
        {
            if (propertyName == nameof(StartIndex))
            {
                return "Start Index";
            }
            if (propertyName == nameof(EndIndex))
            {
                return "End Index";
            }
            else if (propertyName == nameof(Value))
            {
                return "Value";
            }
            else if (propertyName == nameof(NumberAbove))
            {
                return "No. Above";
            }
            else
            {
                return null;
            }
        }

    }
}
