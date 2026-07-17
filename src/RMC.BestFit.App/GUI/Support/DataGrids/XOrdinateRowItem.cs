using System.Collections.ObjectModel;

namespace RMC_BestFit
{
    /// <summary>
    /// Row wrapper for a primary (X-axis) ordinate value in the coincident frequency
    /// analysis properties control. Inherits all validation behavior from
    /// <see cref="OrdinateRowItem"/>; only overrides <see cref="PropertyDisplayName"/>
    /// so the auto-generated DataGrid column header reads "X Value".
    /// </summary>
    public class XOrdinateRowItem : OrdinateRowItem
    {
        /// <summary>
        /// Initializes a new <see cref="XOrdinateRowItem"/> with default values. Required
        /// by <see cref="GenericControls.ValidationDataGrid"/> when adding a row from the
        /// click-to-add row factory (which uses the type's parameterless constructor).
        /// </summary>
        public XOrdinateRowItem() : base() { }

        /// <summary>
        /// Initializes a new <see cref="XOrdinateRowItem"/> with the specified parent
        /// list and value.
        /// </summary>
        /// <param name="parentList">The observable collection that contains this row item.</param>
        /// <param name="value">The X ordinate value.</param>
        public XOrdinateRowItem(ObservableCollection<object> parentList, double value)
            : base(parentList, value)
        {
        }

        /// <summary>
        /// Returns the column header for the auto-generated DataGrid column. "X Value"
        /// for <see cref="OrdinateRowItem.Value"/>, null for any other property.
        /// </summary>
        /// <param name="propertyName">The property name.</param>
        /// <returns>"X Value" for <see cref="OrdinateRowItem.Value"/>; null otherwise.</returns>
        public override string PropertyDisplayName(string propertyName)
        {
            if (propertyName == nameof(Value)) return "X Value";
            return null;
        }
    }
}
