using GenericControls;
using RMC.BestFit.Models;
using RMC.BestFit.UI;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace RMC_BestFit
{
    /// <summary>
    /// Represents a data grid row item for exact data observations, providing a view model wrapper for precise
    /// measurements or observations with known values used in statistical distribution fitting.
    /// </summary>
    public class ExactDataRowItem : DataGridRowItem
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ExactDataRowItem"/> class with default values.
        /// </summary>
        public ExactDataRowItem() : base(null) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExactDataRowItem"/> class with the specified collection and exact data.
        /// </summary>
        /// <param name="observableCollection">The observable collection that contains this row item.</param>
        /// <param name="ordinate">The exact data object to be wrapped by this row item.</param>
        /// <param name="series">The data series that contains the ordinate, used for clone-and-replace on edits.</param>
        /// <param name="showDateTime">Optional flag indicating whether to display DateTime instead of Index in the grid. Default is false.</param>
        public ExactDataRowItem(ObservableCollection<Object> observableCollection, ExactData ordinate, DataSeries series, bool showDateTime = false) : base(observableCollection)
        {
            _showDateTime = showDateTime;
            _ordinate = ordinate;
            _series = series;
        }

        /// <summary>
        /// Indicates whether the DateTime property should be displayed instead of the Index property in the grid.
        /// </summary>
        private bool _showDateTime;

        /// <summary>
        /// The underlying exact data model object.
        /// </summary>
        private ExactData _ordinate;

        /// <summary>
        /// The data series containing the ordinate, used for the clone-and-replace pattern.
        /// </summary>
        private DataSeries _series;

        /// <summary>
        /// Gets the date and time associated with this observation.
        /// </summary>
        public DateTime DateTime => _ordinate.DateTime;

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
        /// Gets or sets the exact measured value of this observation.
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
        /// Gets the plotting position of this observation, used for graphical representation in probability plots.
        /// </summary>
        public double PlottingPosition => _ordinate.PlottingPosition;

        /// <summary>
        /// Gets a value indicating whether this observation is classified as a low outlier in the dataset.
        /// </summary>
        public bool IsLowOutlier => _ordinate.IsLowOutlier;

        /// <summary>
        /// Updates the underlying ordinate reference and notifies the UI of all property changes.
        /// Called by the CollectionChanged(Replace) handler during undo/redo to sync the RowItem
        /// with the restored model data without triggering a clone-and-replace back to the series.
        /// </summary>
        /// <param name="newOrdinate">The restored ordinate from the model.</param>
        public void SetOrdinate(ExactData newOrdinate)
        {
            _ordinate = newOrdinate;
            NotifyPropertyChanged(nameof(Index));
            NotifyPropertyChanged(nameof(DateTime));
            NotifyPropertyChanged(nameof(Value));
            NotifyPropertyChanged(nameof(PlottingPosition));
            NotifyPropertyChanged(nameof(IsLowOutlier));
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
        private void ReplaceOrdinate(ExactData newOrdinate)
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
        /// Adds validation rules for the exact data row item properties.
        /// Validates that the index is within valid range and the value is a valid number.
        /// </summary>
        public override void AddValidationRules()
        {
            AddRule(nameof(Index), () => Index < -100000, "The index must be between -100,000 and +100,000.");
            AddRule(nameof(Index), () => Index > 100000, "The index must be between -100,000 and +100,000.");
            AddRule(nameof(Value), () => double.IsNaN(Value) || double.IsInfinity(Value), "The value must be a number.", new[] { nameof(Value) });
        }

        /// <summary>
        /// Determines whether the specified property should be displayed in the data grid based on the display mode.
        /// </summary>
        /// <param name="propertyName">The name of the property to check.</param>
        /// <returns>True if the property should be displayed; otherwise, false.</returns>
        public override bool IsGridDisplayable(string propertyName)
        {
            if (propertyName == nameof(Index))
                return !_showDateTime;
            if (propertyName == nameof(DateTime))
                return _showDateTime;
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
            if (propertyName == nameof(DateTime))
            {
                return "Date Time";
            }
            else if (propertyName == nameof(Value))
            {
                return "Value";
            }
            else if (propertyName == nameof(IsLowOutlier))
            {
                return "Low Outlier";
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
