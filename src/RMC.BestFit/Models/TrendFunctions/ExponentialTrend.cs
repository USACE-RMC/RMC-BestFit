using Numerics;
using Numerics.Distributions;
using RMC.BestFit.Models.TrendFunctions.Support;
using System.Xml.Linq;

namespace RMC.BestFit.Models.TrendFunctions
{
    /// <summary>
    /// Exponential trend model:
    /// <c>y(t) = α exp(β (t - StartIndex))</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// </remarks>
    public class ExponentialTrend : TrendModelBase
    {
        private const double MaxExponent = 700.0; // ~log(1e304), guard against overflow

        /// <inheritdoc/>
        public ExponentialTrend() : base() { }

        /// <inheritdoc/>
        public ExponentialTrend(XElement xElement) : base(xElement) { }

        /// <inheritdoc/>
        public override TrendModelType Type => TrendModelType.Exponential;

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
        /// <remarks>
        /// When <c>b · t</c> would overflow <c>Math.Exp</c> (|exponent| &gt; 700),
        /// the prediction is propagated as <see cref="double.PositiveInfinity"/> /
        /// <see cref="double.NegativeInfinity"/> / <c>0</c> rather than a
        /// finite-but-saturated value. This lets downstream log-likelihood guards
        /// detect the unphysical extrapolation and reject it (as <c>-∞</c>) rather
        /// than treat <c>a · 1e304</c> as a valid prediction.
        /// </remarks>
        public override double Predict(int index)
        {
            double t = index - StartIndex;
            double a = Parameters[0].Value;
            double b = Parameters[1].Value;
            double exponent = b * t;

            if (exponent > MaxExponent)
                return a >= 0 ? double.PositiveInfinity : double.NegativeInfinity;
            if (exponent < -MaxExponent)
                return 0.0;

            return a * Math.Exp(exponent);
        }

        /// <inheritdoc/>
        public override ITrendModel Clone()
        {
            var model = new ExponentialTrend
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
