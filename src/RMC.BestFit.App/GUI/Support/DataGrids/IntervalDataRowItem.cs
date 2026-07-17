using GenericControls;
using RMC.BestFit.Models;
using RMC.BestFit.UI;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;

namespace RMC_BestFit
{
    /// <summary>
    /// Represents a data grid row item for interval data observations, where each observation is characterized
    /// by a lower bound, most likely value, and upper bound. This is commonly used for representing
    /// historical or paleological data with uncertain measurements or estimates.
    /// </summary>
    public class IntervalDataRowItem : DataGridRowItem
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IntervalDataRowItem"/> class with default values.
        /// </summary>
        public IntervalDataRowItem() : base(null) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="IntervalDataRowItem"/> class with the specified collection and interval data.
        /// </summary>
        /// <param name="observableCollection">The observable collection that contains this row item.</param>
        /// <param name="ordinate">The interval data object to be wrapped by this row item.</param>
        /// <param name="dataframe">The data frame containing all data series, used for validation to prevent index overlap.</param>
        /// <param name="series">The data series that contains the ordinate, used for clone-and-replace on edits.</param>
        public IntervalDataRowItem(ObservableCollection<Object> observableCollection, IntervalData ordinate, DataFrame dataframe, DataSeries series) : base(observableCollection)
        {
            _ordinate = ordinate;
            _dataFrame = dataframe;
            _series = series;
        }

        /// <summary>
        /// The underlying interval data model object.
        /// </summary>
        private IntervalData _ordinate;

        /// <summary>
        /// The data frame containing all data series, used for cross-validation.
        /// </summary>
        private DataFrame _dataFrame;

        /// <summary>
        /// The data series containing the ordinate, used for the clone-and-replace pattern.
        /// </summary>
        private DataSeries _series;

        /// <summary>
        /// Gets or sets the index position of this observation in the data series.
        /// When set, creates a clone of the underlying data, modifies the clone, and replaces
        /// it in the data series to fire a CollectionChanged Replace event for undo support.
        /// </summary>
        public int Index
        {
            get { return _ordinate.Index; }
            set
            {
                if (_ordinate.Index != value)
                {
                    var clone = _ordinate.Clone();
                    clone.Index = value;
                    ReplaceOrdinate(clone);
                    NotifyPropertyChanged(nameof(Index));
                }
            }
        }

        /// <summary>
        /// Gets or sets the lower bound value of the interval estimate.
        /// When set, creates a clone of the underlying data, modifies the clone, and replaces
        /// it in the data series to fire a CollectionChanged Replace event for undo support.
        /// </summary>
        public double LowerValue
        {
            get { return _ordinate.LowerValue; }
            set
            {
                if (_ordinate.LowerValue != value)
                {
                    var clone = _ordinate.Clone();
                    clone.LowerValue = value;
                    ReplaceOrdinate(clone);
                    NotifyPropertyChanged(nameof(LowerValue));
                }
            }
        }

        /// <summary>
        /// Gets or sets the most likely value within the interval estimate.
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
        /// Gets or sets the upper bound value of the interval estimate.
        /// When set, creates a clone of the underlying data, modifies the clone, and replaces
        /// it in the data series to fire a CollectionChanged Replace event for undo support.
        /// </summary>
        public double UpperValue
        {
            get { return _ordinate.UpperValue; }
            set
            {
                if (_ordinate.UpperValue != value)
                {
                    var clone = _ordinate.Clone();
                    clone.UpperValue = value;
                    ReplaceOrdinate(clone);
                    NotifyPropertyChanged(nameof(UpperValue));
                }
            }
        }

        /// <summary>
        /// Gets the plotting position of this observation, used for graphical representation in probability plots.
        /// </summary>
        public double PlottingPosition => _ordinate.PlottingPosition;

        /// <summary>
        /// Updates the underlying ordinate reference and notifies the UI of all property changes.
        /// Called by the CollectionChanged(Replace) handler during undo/redo to sync the RowItem
        /// with the restored model data without triggering a clone-and-replace back to the series.
        /// </summary>
        /// <param name="newOrdinate">The restored ordinate from the model.</param>
        public void SetOrdinate(IntervalData newOrdinate)
        {
            _ordinate = newOrdinate;
            NotifyPropertyChanged(nameof(Index));
            NotifyPropertyChanged(nameof(LowerValue));
            NotifyPropertyChanged(nameof(Value));
            NotifyPropertyChanged(nameof(UpperValue));
            NotifyPropertyChanged(nameof(PlottingPosition));
        }

        /// <summary>
        /// Notifies the grid that the computed plotting position changed on the wrapped ordinate.
        /// </summary>
        /// <remarks>
        /// Plotting positions are recomputed in bulk by <see cref="DataFrame"/> without replacing
        /// row objects, so the row wrapper forwards the computed-property notification to WPF.
        /// </remarks>
        public void RefreshPlottingPosition()
        {
            NotifyPropertyChanged(nameof(PlottingPosition));
        }

        /// <summary>
        /// Replaces the current ordinate in the data series with a new clone.
        /// This fires a CollectionChanged Replace event which the UndoableCollectionBridge records.
        /// </summary>
        /// <param name="newOrdinate">The cloned and modified ordinate to replace the current one.</param>
        private void ReplaceOrdinate(IntervalData newOrdinate)
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
        /// Adds validation rules for the interval data row item properties.
        /// Validates that indexes are unique, within valid range, do not overlap with other data types,
        /// and that the interval bounds are properly ordered (lower &lt; most likely &lt; upper).
        /// </summary>
        public override void AddValidationRules()
        {
            AddRule(nameof(Index), IsIndexInvalid, "The interval data cannot overlap with the exact or uncertain data.");
            AddRule(nameof(Index), () => UniqueRule(nameof(Index), "The index must be unique."), "The index must be unique.");
            AddRule(nameof(Index), () => Index < -100000, "The index must be between -100,000 and +100,000.");
            AddRule(nameof(Index), () => Index > 100000, "The index must be between -100,000 and +100,000.");
            AddRule(nameof(LowerValue), () => LowerValue >= Value, "The lower value must be less than the most likely value.", new[] { nameof(Value) });
            AddRule(nameof(UpperValue), () => Value >= UpperValue, "The upper value must be greater than the most likely value.", new[] { nameof(Value) });
            AddRule(nameof(LowerValue), () => LowerValue >= UpperValue, "The lower value must be less than the upper value.", new[] { nameof(UpperValue) });
            AddRule(nameof(UpperValue), () => LowerValue >= UpperValue, "The upper value must be greater than the lower value.", new[] { nameof(LowerValue) });
            AddRule(nameof(LowerValue), () => double.IsNaN(LowerValue) || double.IsInfinity(LowerValue), "The lower value must be a number.", new[] { nameof(LowerValue) });
            AddRule(nameof(Value), () => double.IsNaN(Value) || double.IsInfinity(Value), "The most likely value must be a number.", new[] { nameof(Value) });
            AddRule(nameof(UpperValue), () => double.IsNaN(UpperValue) || double.IsInfinity(UpperValue), "The upper value must be a number.", new[] { nameof(UpperValue) });;
        }

        /// <summary>
        /// Validates that the index does not overlap with exact or uncertain data in the data frame.
        /// </summary>
        /// <returns>True if the index overlaps with existing exact or uncertain data; otherwise, false.</returns>
        private bool IsIndexInvalid()
        {
            if (_dataFrame.ExactSeries.Count > 0)
            {
                for (int i = 0; i < _dataFrame.ExactSeries.Count; i++)
                    if (Index == ((ExactData)_dataFrame.ExactSeries[i]).Index)
                        return true;
            }
            if (_dataFrame.UncertainSeries.Count > 0)
            {
                for (int i = 0; i < _dataFrame.UncertainSeries.Count; i++)
                    if (Index == ((UncertainData)_dataFrame.UncertainSeries[i]).Index)
                        return true;
            }
            return false;
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
            if (propertyName == nameof(Index))
            {
                return "Index";
            }
            else if (propertyName == nameof(LowerValue))
            {
                return "Lower";
            }
            else if (propertyName == nameof(Value))
            {
                return "Most Likely";
            }
            else if (propertyName == nameof(UpperValue))
            {
                return "Upper";
            }
            else if (propertyName == nameof(PlottingPosition))
            {
                return "Plotting Position";
            }
            else
            {
                return null;
            }
        }

    }
}
