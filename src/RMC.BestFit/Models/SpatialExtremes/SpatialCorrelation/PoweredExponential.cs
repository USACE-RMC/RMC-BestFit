using Numerics;
using Numerics.Distributions;

namespace RMC.BestFit.Models.SpatialExtremes
{
    /// <summary>
    /// Powered exponential (Gaussian) spatial correlation function: ρ(h) = exp(-(h/φ)^ν).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The powered exponential family includes the exponential (ν=1) and Gaussian (ν=2) as special cases.
    /// - φ is the range parameter (distance scale)
    /// - ν is the smoothness parameter (0 &lt; ν ≤ 2)
    /// </para>
    /// <para>
    ///     <b>Authors:</b>
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// </remarks>
    public class PoweredExponential : ICorrelationModel
    {
        /// <summary>
        /// Constructs a new powered exponential correlation model.
        /// </summary>
        public PoweredExponential()
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
            Parameters.Add(new ModelParameter()
            {
                Name = "Smoothness",
                Value = 1.5,
                LowerBound = 0.1,
                UpperBound = 2.0,
                IsPositive = true,
                PriorDistribution = new Uniform(0.1, 2.0)
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
        public CorrelationFunctionType Type => CorrelationFunctionType.PoweredExponential;

        /// <inheritdoc/>
        public System.Xml.Linq.XElement ToXElement()
        {
            var x = new System.Xml.Linq.XElement(nameof(PoweredExponential));
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
            double smoothness = Parameters[1].Value;

            if (range <= 0)
                return 0.0;

            return Math.Exp(-Math.Pow(h / range, smoothness));
        }

        /// <summary>
        /// Returns a deep copy of the correlation model.
        /// </summary>
        public ICorrelationModel Clone()
        {
            var result = new PoweredExponential();
            for (int i = 0; i < Parameters.Count; i++)
            {
                result.Parameters[i].Value = Parameters[i].Value;
                result.Parameters[i].LowerBound = Parameters[i].LowerBound;
                result.Parameters[i].UpperBound = Parameters[i].UpperBound;
                result.Parameters[i].PriorDistribution = Parameters[i].PriorDistribution?.Clone()!;
            }
            return result;
        }
    }
}
