using Numerics;
using Numerics.Distributions;
using RMC.BestFit.Models.TrendFunctions.Support;
using System.Xml.Linq;

namespace RMC.BestFit.Models.TrendFunctions
{
    /// <summary>
    /// Logistic (sigmoid) trend model:
    /// <c>y(t) = α / (1 + exp(-β (t - StartIndex)))</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// </remarks>
    public class LogisticTrend: TrendModelBase
    {
        private const double MaxExponent = 700.0; // ~log(1e304), guard against overflow

        /// <inheritdoc/>
        public LogisticTrend() : base() { }

        /// <inheritdoc/>
        public LogisticTrend(XElement xElement) : base(xElement) { }

        /// <inheritdoc/>
        public override TrendModelType Type => TrendModelType.Logistic;

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
            double exponent = Tools.Clamp(-b * t, -MaxExponent, MaxExponent);
            double denom = 1.0 + Math.Exp(exponent);
            return a / denom;
        }

        /// <inheritdoc/>
        public override ITrendModel Clone()
        {
            var model = new LogisticTrend
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
