using GenericControls;
using System.Collections.ObjectModel;

namespace RMC_BestFit
{
    /// <summary>
    /// Base row wrapper for a single double value in an ordinate <see cref="ValidationDataGrid"/>.
    /// Inherits <see cref="DataGridRowItem"/> so the cell participates in the validation
    /// framework (auto-generated columns, ordering checks, error highlighting). Mirrors the
    /// canonical pattern from WPF-Framework's <c>ProbabilityOrdinateRowItem</c>.
    /// </summary>
    /// <remarks>
    /// Subclassed by <see cref="XOrdinateRowItem"/> and <see cref="YOrdinateRowItem"/> so each
    /// auto-generated column gets the right header ("X Value" or "Y Value"). Shared validation
    /// (<c>OrderRule</c>) and column-display logic live here.
    /// </remarks>
    public class OrdinateRowItem : DataGridRowItem
    {
        /// <summary>
        /// The backing field for the <see cref="Value"/> property.
        /// </summary>
        private double _value;

        /// <summary>
        /// Initializes a new instance of the <see cref="OrdinateRowItem"/> class with default values.
        /// Required by <see cref="ValidationDataGrid"/> when adding new rows from the click-to-add row.
        /// </summary>
        public OrdinateRowItem() : base(null) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="OrdinateRowItem"/> class with the specified value.
        /// </summary>
        /// <param name="parentList">The observable collection that contains this row item — used by
        /// the base class's <c>OrderRule</c> machinery to compare against neighbors.</param>
        /// <param name="value">The ordinate value.</param>
        public OrdinateRowItem(ObservableCollection<object> parentList, double value) : base(parentList)
        {
            _value = value;
        }

        /// <summary>Gets or sets the row value.</summary>
        public double Value
        {
            get { return _value; }
            set
            {
                if (_value != value)
                {
                    _value = value;
                    NotifyPropertyChanged(nameof(Value));
                }
            }
        }

        /// <summary>
        /// Adds validation rules for the ordinate row item properties.
        /// Validates that values are in strict ascending order — duplicates are not allowed
        /// because both X and Y ordinate vectors are used as bivariate-grid row/column keys.
        /// </summary>
        public override void AddValidationRules()
        {
            AddRule(nameof(Value), () => OrderRule<double, OrdinateRowItem>(x => x.Value, nameof(Value), true, false), "Ordinate values must be in ascending order.");
        }

        /// <summary>
        /// Determines whether the specified property should be displayed in the data grid.
        /// </summary>
        /// <param name="propertyName">The name of the property to check.</param>
        /// <returns><c>true</c> for <see cref="Value"/>; <c>false</c> for inherited / framework properties.</returns>
        public override bool IsGridDisplayable(string propertyName)
        {
            return propertyName == nameof(Value);
        }

        /// <summary>
        /// Gets the display name for the specified property to be shown in the data grid header.
        /// Default implementation returns "Value"; subclasses (<see cref="XOrdinateRowItem"/>,
        /// <see cref="YOrdinateRowItem"/>) override to provide axis-specific column headers.
        /// </summary>
        /// <param name="propertyName">The name of the property.</param>
        /// <returns>The display name for the property, or null if not recognized.</returns>
        public override string PropertyDisplayName(string propertyName)
        {
            if (propertyName == nameof(Value)) return "Value";
            return null;
        }
    }
}
