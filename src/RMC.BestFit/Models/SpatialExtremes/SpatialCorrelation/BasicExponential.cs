using Numerics;
using Numerics.Distributions;

namespace RMC.BestFit.Models.SpatialExtremes
{

    /// <summary>
    /// Exponential spatial correlation function: ρ(h) = exp(-h / range).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The exponential correlation function is commonly used in geostatistics.
    /// The range parameter controls the distance at which correlation decays.
    /// At distance = range, correlation = exp(-1) ≈ 0.368.
    /// The practical range (distance at which correlation = 0.05) is approximately 3 × range.
    /// </para>
    /// <para>
    ///     <b>Authors:</b>
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// </remarks>
    public class BasicExponential : ICorrelationModel
    {
        /// <summary>
        /// Constructs a new exponential correlation model.
        /// </summary>
        public BasicExponential()
        {
            Parameters = new List<ModelParameter>();
            Parameters.Add(new ModelParameter()
            {
                Name = "Range",
                Value = 10.0,
                LowerBound = Tools.DoubleMachineEpsilon,
                UpperBound = 500,  // Allow for large spatial extents (up to 500 km)
                IsPositive = true,
                PriorDistribution = new Uniform(Tools.DoubleMachineEpsilon, 500)
            });
        }

        /// <summary>
        /// Gets the list of model parameters.
        /// </summary>
        public List<ModelParameter> Parameters { get; private set; }

        /// <summary>
        /// Gets the number of model parameters.
        /// </summary>
        public int NumberOfParameters => Parameters.Count;

        /// <inheritdoc/>
        public CorrelationFunctionType Type => CorrelationFunctionType.Exponential;

        /// <inheritdoc/>
        public System.Xml.Linq.XElement ToXElement()
        {
            var x = new System.Xml.Linq.XElement(nameof(BasicExponential));
            x.SetAttributeValue(nameof(Type), Type.ToString());
            for (int i = 0; i < Parameters.Count; i++)
                x.Add(Parameters[i].ToXElement());
            return x;
        }

        /// <summary>
        /// Sets the model parameter values.
        /// </summary>
        /// <param name="values">The parameter values.</param>
        public void SetParameterValues(IList<double> values)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));
            if (values.Count != NumberOfParameters)
                throw new ArgumentException("The list of values has incorrect length.", nameof(values));

            for (int i = 0; i < NumberOfParameters; i++)
                Parameters[i].Value = values[i];
        }

        /// <summary>
        /// Evaluates the correlation function at distance h.
        /// </summary>
        /// <param name="h">The distance between two locations.</param>
        /// <returns>The correlation value ρ(h) ∈ [0, 1].</returns>
        public double Evaluate(double h)
        {
            if (h < 0)
                throw new ArgumentException("Distance must be non-negative.", nameof(h));

            if (h == 0)
                return 1.0;

            double range = Parameters[0].Value;
            if (range <= 0)
                return 0.0;

            return Math.Exp(-h / range);
        }

        /// <summary>
        /// Returns a deep copy of the correlation model.
        /// </summary>
        public ICorrelationModel Clone()
        {
            var result = new BasicExponential();
            result.Parameters[0].Value = Parameters[0].Value;
            result.Parameters[0].LowerBound = Parameters[0].LowerBound;
            result.Parameters[0].UpperBound = Parameters[0].UpperBound;
            result.Parameters[0].PriorDistribution = Parameters[0].PriorDistribution?.Clone()!;
            return result;
        }
    }
}

