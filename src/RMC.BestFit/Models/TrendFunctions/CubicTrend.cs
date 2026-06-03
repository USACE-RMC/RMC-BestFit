using Numerics.Distributions;
using RMC.BestFit.Models.TrendFunctions.Support;
using System.Xml.Linq;

namespace RMC.BestFit.Models.TrendFunctions
{
    /// <summary>
    /// Cubic trend model:
    /// <c>y(t) = α + β (t - StartIndex) + γ (t - StartIndex)^2 + δ (t - StartIndex)^3</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// </remarks>
    public class CubicTrend : TrendModelBase
    {
        /// <inheritdoc/>
        public CubicTrend() : base() { }

        /// <inheritdoc/>
        public CubicTrend(XElement xElement) : base(xElement) { }

        /// <inheritdoc/>
        public override TrendModelType Type => TrendModelType.Cubic;

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
                },
                new ModelParameter
                {
                    OwnerName = OwnerName,
                    Name = "(γ)",
                    LowerBound = -1,
                    UpperBound = 1,
                    PriorDistribution = new Uniform(-1, 1)
                },
                new ModelParameter
                {
                    OwnerName = OwnerName,
                    Name = "(δ)",
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
            double c = Parameters[2].Value;
            double d = Parameters[3].Value;
            return a + b * t + c * t * t + d * t * t * t;
        }

        /// <inheritdoc/>
        public override ITrendModel Clone()
        {
            var model = new CubicTrend
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
