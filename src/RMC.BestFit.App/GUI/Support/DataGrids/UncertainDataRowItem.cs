using GenericControls;
using Numerics.Distributions;
using RMC.BestFit.Models;
using RMC.BestFit.UI;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace RMC_BestFit
{
    /// <summary>
    /// Represents a data grid row item for uncertain data observations, where each observation is represented
    /// by a probability distribution rather than a single value. This is used when measurement uncertainty
    /// is explicitly modeled in statistical analysis.
    /// </summary>
    public class UncertainDataRowItem : DataGridRowItem
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UncertainDataRowItem"/> class with default values.
        /// </summary>
        public UncertainDataRowItem() : base(null) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="UncertainDataRowItem"/> class with the specified collection and uncertain data.
        /// </summary>
        /// <param name="observableCollection">The observable collection that contains this row item.</param>
        /// <param name="ordinate">The uncertain data object to be wrapped by this row item.</param>
        /// <param name="dataframe">The data frame containing all data series, used for validation to prevent index overlap.</param>
        /// <param name="series">The data series that contains the ordinate, used for clone-and-replace on edits.</param>
        public UncertainDataRowItem(ObservableCollection<Object> observableCollection, UncertainData ordinate, DataFrame dataframe, DataSeries series) : base(observableCollection)
        {
            _ordinate = ordinate;
            _dataFrame = dataframe;
            _series = series;
        }

        /// <summary>
        /// The underlying uncertain data model object.
        /// </summary>
        private UncertainData _ordinate;

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
        /// Gets or sets the probability distribution representing the uncertainty in this observation.
        /// When set, creates a clone of the underlying data, modifies the clone, and replaces
        /// it in the data series to fire a CollectionChanged Replace event for undo support.
        /// </summary>
        public UnivariateDistributionBase Distribution
        {
            get { return _ordinate.Distribution; }
            set
            {
                if (_ordinate.Distribution != value)
                {
                    var clone = _ordinate.Clone();
                    clone.Distribution = value;
                    ReplaceOrdinate(clone);
                    NotifyPropertyChanged(nameof(Distribution));
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
        public void SetOrdinate(UncertainData newOrdinate)
        {
            _ordinate = newOrdinate;
            NotifyPropertyChanged(nameof(Index));
            NotifyPropertyChanged(nameof(Distribution));
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
        private void ReplaceOrdinate(UncertainData newOrdinate)
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
        /// Adds validation rules for the uncertain data row item properties.
        /// Validates that the index does not overlap with exact or interval data, is within valid range,
        /// and the distribution parameters are valid.
        /// </summary>
        public override void AddValidationRules()
        {
            AddRule(nameof(Index), IsIndexInvalid, "The uncertain data cannot overlap with the exact data.");
            AddRule(nameof(Index), () => Index < -100000, "The index must be between -100,000 and +100,000.");
            AddRule(nameof(Index), () => Index > 100000, "The index must be between -100,000 and +100,000.");
            AddRule(nameof(Distribution), () => Distribution.ValidateParameters(Distribution.GetParameters, false) != null, "The distribution parameters are invalid.", new[] { nameof(Distribution) });
        }

        /// <summary>
        /// Validates that the index does not overlap with exact or interval data in the data frame.
        /// </summary>
        /// <returns>True if the index overlaps with existing exact or interval data; otherwise, false.</returns>
        private bool IsIndexInvalid()
        {
            if (_dataFrame.ExactSeries.Count > 0)
            {
                for (int i = 0; i < _dataFrame.ExactSeries.Count; i++)
                    if (Index == ((ExactData)_dataFrame.ExactSeries[i]).Index)
                        return true;
            }
            if (_dataFrame.IntervalSeries.Count > 0)
            {
                for (int i = 0; i < _dataFrame.IntervalSeries.Count; i++)
                    if (Index == ((IntervalData)_dataFrame.IntervalSeries[i]).Index)
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
            else if (propertyName == nameof(Distribution))
            {
                return "Distribution";
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
