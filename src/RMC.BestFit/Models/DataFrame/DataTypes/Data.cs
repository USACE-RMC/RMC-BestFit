using Numerics.Data;
using System;

namespace RMC.BestFit.Models
{
    /// <summary>
    /// Base class for censored data ordinate.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// </remarks>
    public abstract class Data : SeriesOrdinate<int, double>
    {
        /// <summary>
        /// Constructs an empty data class.
        /// </summary>
        public Data() { }

        /// <summary>
        /// Constructs a new censored data ordinate.
        /// </summary>
        /// <param name="index">The index of the data ordinate.</param>
        /// <param name="value">The value of the data ordinate.</param>
        /// <param name="plottingPosition">Optional. The plotting position of the data ordinate. Default = 0.</param>
        public Data(int index, double value, double plottingPosition = 0d) : base(index, value)
        {
            _index = index;
            _value = value;
            _plottingPosition = plottingPosition;
        }

        /// <summary>
        /// Backing value for the plotting position.
        /// </summary>
        protected double _plottingPosition;

        /// <summary>
        /// The plotting position of the data ordinate.
        /// </summary>
        public double PlottingPosition
        {
            get { return Math.Max(0, Math.Min(1, _plottingPosition)); }
            set
            {
                if (_plottingPosition != value)
                {
                    _plottingPosition = value;
                    RaisePropertyChanged(nameof(PlottingPosition));
                }
            }
        }

        /// <summary>
        /// Returns the complement of the plotting position.
        /// </summary>
        public double PlottingPositionComplement
        {
            get { return 1d - PlottingPosition; }
        }

        /// <summary>
        /// Returns the log base 10 transform of the data value. 
        /// </summary>
        public double Log10Value
        {
            get { return Value < 0 ? double.NaN : (Value == 0 ? Math.Log10(0.001) : Math.Log10(Value)); }
        }

        /// <summary>
        /// Gets or sets the standardized value. 
        /// </summary>
        public double StandardizedValue { get; set; }

        /// <summary>
        /// Gets or sets the standardized log10 value. 
        /// </summary>
        public double StandardizedLog10Value { get; set; }


    }
}
