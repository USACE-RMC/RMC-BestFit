using System.Xml.Linq;

namespace RMC.BestFit.Models.SpatialExtremes
{
    /// <summary>
    /// Interface for spatial correlation function model.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// </remarks>
    public interface ICorrelationModel
    {
        /// <summary>
        /// The list of model parameters.
        /// </summary>
        List<ModelParameter> Parameters { get; }

        /// <summary>
        /// Returns the number of model parameters.
        /// </summary>
        int NumberOfParameters { get; }

        /// <summary>
        /// The correlation model type identifier (used for serialization dispatch).
        /// </summary>
        CorrelationFunctionType Type { get; }

        /// <summary>
        /// Set the model parameter values.
        /// </summary>
        /// <param name="values">The parameter values.</param>
        void SetParameterValues(IList<double> values);

        /// <summary>
        /// Evaluate the model.
        /// </summary>
        /// <param name="h">The distance, h.</param>
        double Evaluate(double h);

        /// <summary>
        /// Returns a deep copy of the correlation model.
        /// </summary>
        ICorrelationModel Clone();

        /// <summary>
        /// Returns the correlation model serialized as an XElement. The element
        /// includes the <see cref="Type"/> attribute so a factory can dispatch to
        /// the correct concrete implementation on deserialization.
        /// </summary>
        XElement ToXElement();
    }
}
