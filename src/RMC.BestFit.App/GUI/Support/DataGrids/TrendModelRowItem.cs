using RMC.BestFit.Models.TrendFunctions.Support;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RMC_BestFit
{
    /// <summary>
    /// Represents a trend model row item for display in a data grid or list, providing a mapping
    /// between a human-readable display name and a trend model type enumeration value.
    /// Used for selecting trend models in the user interface for time series analysis.
    /// </summary>
    public class TrendModelRowItem : INotifyPropertyChanged
    {
        /// <summary>
        /// The backing field for the Name property.
        /// </summary>
        private string _name;

        /// <summary>
        /// The backing field for the Value property.
        /// </summary>
        private TrendModelType _value;

        /// <summary>
        /// Gets or sets the display name of the trend model shown to the user.
        /// </summary>
        public string Name
        {
            get { return _name; }
            set
            {
                _name = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
            }
        }

        /// <summary>
        /// Gets or sets the enumeration value representing the type of trend model.
        /// </summary>
        public TrendModelType Value
        {
            get { return _value; }
            set
            {
                _value = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TrendModelRowItem"/> class with the specified display name and model type.
        /// </summary>
        /// <param name="displayName">The human-readable name to display in the user interface.</param>
        /// <param name="value">The trend model type enumeration value.</param>
        public TrendModelRowItem(string displayName, TrendModelType value)
        {
            Name = displayName;
            Value = value;
        }

        /// <summary>
        /// Occurs when a property value changes.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;
    }
}
