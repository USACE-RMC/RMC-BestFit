using RMC.BestFit.UI;
using System;
using System.ComponentModel;

namespace RMC_BestFit
{
    /// <summary>
    /// Represents an optional input-data overlay choice for properties-control ComboBoxes.
    /// </summary>
    /// <remarks>
    /// The item is intentionally UI-only: a <c>null</c> <see cref="Value"/> displays as
    /// <c>&lt;None&gt;</c> and maps back to an analysis <c>InputData</c> reference of
    /// <c>null</c>. Real <see cref="InputData"/> values are wrapped so their names can
    /// be displayed and resorted without creating dummy project elements.
    /// </remarks>
    public sealed class InputDataSelectionItem : INotifyPropertyChanged, IDisposable
    {
        /// <summary>
        /// The display text used for the optional no-overlay item.
        /// </summary>
        public const string NoneDisplayName = "<None>";

        /// <summary>
        /// Initializes a new instance of the <see cref="InputDataSelectionItem"/> class.
        /// </summary>
        /// <param name="inputData">
        /// The wrapped input data element, or <c>null</c> to create the no-overlay item.
        /// </param>
        /// <remarks>
        /// Real input data elements are observed for <see cref="InputData.Name"/> changes so
        /// WPF bindings can refresh the displayed text and live-sorted position.
        /// </remarks>
        public InputDataSelectionItem(InputData inputData)
        {
            Value = inputData;
            if (Value != null)
            {
                Value.PropertyChanged += InputData_PropertyChanged;
            }
        }

        /// <summary>
        /// Occurs when a displayed property value changes.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Gets the underlying input data value selected by the ComboBox.
        /// </summary>
        public InputData Value { get; }

        /// <summary>
        /// Gets the display text shown in the ComboBox.
        /// </summary>
        public string DisplayName
        {
            get { return Value == null ? NoneDisplayName : Value.Name; }
        }

        /// <summary>
        /// Gets a stable sort key that pins the no-overlay item before real input data.
        /// </summary>
        public string SortKey
        {
            get { return Value == null ? "0" : "1" + DisplayName; }
        }

        /// <summary>
        /// Creates the UI-only no-overlay selection item.
        /// </summary>
        /// <returns>An item whose <see cref="Value"/> is <c>null</c>.</returns>
        /// <remarks>
        /// A factory makes call sites read as intentional optional-overlay behavior rather
        /// than an accidental null wrapper.
        /// </remarks>
        public static InputDataSelectionItem CreateNone()
        {
            return new InputDataSelectionItem(null);
        }

        /// <summary>
        /// Releases the wrapped input-data event subscription.
        /// </summary>
        /// <remarks>
        /// Properties controls rebuild their option lists as project collections change; disposing
        /// removed wrappers prevents long-lived input data elements from retaining stale UI items.
        /// </remarks>
        public void Dispose()
        {
            if (Value != null)
            {
                Value.PropertyChanged -= InputData_PropertyChanged;
            }
        }

        /// <summary>
        /// Propagates wrapped input-data name changes to ComboBox bindings.
        /// </summary>
        /// <param name="sender">The input data element raising the event.</param>
        /// <param name="e">The property-changed event data.</param>
        /// <remarks>
        /// Only the name affects this wrapper's visible text and sort position.
        /// </remarks>
        private void InputData_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(InputData.Name))
            {
                OnPropertyChanged(nameof(DisplayName));
                OnPropertyChanged(nameof(SortKey));
            }
        }

        /// <summary>
        /// Raises <see cref="PropertyChanged"/> for the supplied property.
        /// </summary>
        /// <param name="propertyName">The name of the changed property.</param>
        /// <remarks>
        /// Centralizing notification keeps rename propagation consistent.
        /// </remarks>
        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
