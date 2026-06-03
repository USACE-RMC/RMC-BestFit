using Numerics.Distributions;
using RMC.BestFit.Models.TrendFunctions.Support;
using System.Xml.Linq;

namespace RMC.BestFit.Models.TrendFunctions
{
    /// <summary>
    /// Reciprocal trend model:
    /// <c>y(t) = 1 / (α + β (t - StartIndex))</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// </remarks>
    public class ReciprocalTrend: TrendModelBase
    {
        private const double MinDenominatorMagnitude = 1e-12;

        /// <inheritdoc/>
        public ReciprocalTrend() : base() { }

        /// <inheritdoc/>
        public ReciprocalTrend(XElement xElement) : base(xElement) { }

        /// <inheritdoc/>
        public override TrendModelType Type => TrendModelType.Reciprocal;

        /// <inheritdoc/>
        public override void SetDefaultParameters()
        {
            Parameters = new List<ModelParameter>
            {
                new ModelParameter
                {
                    OwnerName = OwnerName,
                    Name = "(α)"
                },
                new ModelParameter
                {
                    OwnerName = OwnerName,
                    Name = "(β)",
                    LowerBound = -1,
                    UpperBound = 1,
                    PriorDistribution = new Uniform(-1, 1)
                }
            };
        }

        /// <inheritdoc/>
        public override double Predict(int index)
        {
            double t = index - StartIndex;
            double a = Parameters[0].Value;
            double b = Parameters[1].Value;
            double denom = a + b * t;

            if (Math.Abs(denom) < MinDenominatorMagnitude)
            {
                denom = denom >= 0.0 ? MinDenominatorMagnitude : -MinDenominatorMagnitude;
            }

            return 1.0 / denom;
        }

        /// <inheritdoc/>
        public override ITrendModel Clone()
        {
            var model = new ReciprocalTrend
            {
                OwnerName = OwnerName,
                UseDefaultFlatPriors = UseDefaultFlatPriors,
                StartIndex = StartIndex
            };

            for (int i = 0; i < NumberOfParameters; i++)
            {
                model.Parameters[i] = Parameters[i].Clone();
            }

            return model;
        }
    }
}
