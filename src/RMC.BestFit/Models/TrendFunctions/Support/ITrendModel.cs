using System.Xml.Linq;

namespace RMC.BestFit.Models.TrendFunctions.Support
{
    /// <summary>
    /// Interface for parameter trend models used to describe
    /// how a distribution parameter varies with time (index).
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// </remarks>
    public interface ITrendModel
    {
        /// <summary>
        /// Gets or sets the name of the owning distribution parameter.
        /// Typically matches one of the parent distribution parameter names.
        /// </summary>
        string OwnerName { get; set; }

        /// <summary>
        /// Gets the trend model function type.
        /// </summary>
        TrendModelType Type { get; }

        /// <summary>
        /// Gets or sets the starting time index for the trend.
        /// This is usually set to the first index of the full time series
        /// to improve numerical conditioning in the trend function.
        /// </summary>
        int StartIndex { get; set; }

        /// <summary>
        /// Gets the list of model parameters that define the trend.
        /// The number of parameters is given by <see cref="NumberOfParameters"/>.
        /// </summary>
        List<ModelParameter> Parameters { get; }

        /// <summary>
        /// Gets the number of model parameters.
        /// This is a convenience wrapper for <c>Parameters.Count</c>.
        /// </summary>
        int NumberOfParameters { get; }

        /// <summary>
        /// Gets or sets a value indicating whether default "flat" priors
        /// should be applied when initializing the trend parameters.
        /// When set to <c>true</c>, <see cref="SetDefaultParameters"/> is
        /// typically called to reset parameter definitions.
        /// </summary>
        bool UseDefaultFlatPriors { get; set; }

        /// <summary>
        /// Sets the model parameter values from the supplied list.
        /// </summary>
        /// <param name="parameters">
        /// The list of parameter values. The length must match
        /// <see cref="NumberOfParameters"/>.
        /// </param>
        void SetParameterValues(IList<double> parameters);

        /// <summary>
        /// Set the default parameters and priors for this model. 
        /// </summary>
        void SetDefaultParameters();

        /// <summary>
        /// Evaluates the trend function at the specified time index.
        /// </summary>
        /// <param name="index">The time-step index at which to evaluate the trend.</param>
        /// <returns>The value of the trend function at the given index.</returns>
        double Predict(int index);

        /// <summary>
        /// Creates a deep copy of the trend model, including its parameters.
        /// </summary>
        /// <returns>A deep copy of the current trend model.</returns>
        ITrendModel Clone();

        /// <summary>
        /// Serializes the trend model to an <see cref="XElement"/>.
        /// </summary>
        /// <returns>An <see cref="XElement"/> representation of the trend model.</returns>
        XElement ToXElement();
    }
}

