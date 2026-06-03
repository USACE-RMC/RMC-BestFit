using Numerics;
using Numerics.Distributions;
using RMC.BestFit.Models.TrendFunctions.Support;
using System.Xml.Linq;

namespace RMC.BestFit.Models.TrendFunctions
{
    /// <summary>
    /// Sinusoidal trend model:
    /// <c>y(t) = α + β sin(2π γ (t - StartIndex) + δ)</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// </remarks>
    public class SinusoidalTrend : TrendModelBase
    {
        /// <inheritdoc/>
        public SinusoidalTrend() : base() { }

        /// <inheritdoc/>
        public SinusoidalTrend(XElement xElement) : base(xElement) { }

        /// <inheritdoc/>
        public override TrendModelType Type => TrendModelType.Sinusoidal;

        /// <inheritdoc/>
        public override void SetDefaultParameters()
        {
            Parameters = new List<ModelParameter>
            {
                // α: mean level
                new ModelParameter
                {
                    OwnerName = OwnerName,
                    Name = "(α)"
                },
                // β: amplitude (non-negative)
                new ModelParameter
                {
                    OwnerName = OwnerName,
                    Name = "(β)",
                    LowerBound = 0.0,
                    UpperBound = 1.0,
                    Value = 0.5,
                    PriorDistribution = new Uniform(0.0, 1.0)
                },
                // γ: frequency (cycles per time unit)
                new ModelParameter
                {
                    OwnerName = OwnerName,
                    Name = "(γ)",
                    LowerBound = Tools.DoubleMachineEpsilon,
                    UpperBound = 0.5,
                    Value = 0.25,
                    PriorDistribution = new Uniform(0.0, 0.5)
                },
                // δ: phase shift
                new ModelParameter
                {
                    OwnerName = OwnerName,
                    Name = "(δ)",
                    LowerBound = 0.0,
                    UpperBound = 2 * Math.PI,
                    Value = Math.PI,
                    PriorDistribution = new Uniform(0.0, 2 * Math.PI)
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

            return a + b * Math.Sin(2 * Math.PI * c * t + d);
        }

        /// <inheritdoc/>
        public override ITrendModel Clone()
        {
            var model = new SinusoidalTrend
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

