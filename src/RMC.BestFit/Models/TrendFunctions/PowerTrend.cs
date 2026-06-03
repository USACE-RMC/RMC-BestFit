using Numerics.Distributions;
using RMC.BestFit.Models.TrendFunctions.Support;
using System.Xml.Linq;

namespace RMC.BestFit.Models.TrendFunctions
{

    /// <summary>
    /// Power-law trend model:
    /// <c>y(t) = α (t - StartIndex)^β</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// </remarks>
    public class PowerTrend : TrendModelBase
    {
        /// <inheritdoc/>
        public PowerTrend() : base() { }

        /// <inheritdoc/>
        public PowerTrend(XElement xElement) : base(xElement) { }

        /// <inheritdoc/>
        public override TrendModelType Type => TrendModelType.Power;


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
                    LowerBound = -5,
                    UpperBound = 5,
                    PriorDistribution = new Uniform(-5, 5)
                }
            };
        }

        /// <inheritdoc/>
        public override double Predict(int index)
        {
            double t = index - StartIndex;
            double a = Parameters[0].Value;
            double b = Parameters[1].Value;

            // Guard against negative t passed in by mistake, which could
            // lead to complex values for non-integer β. Under normal usage,
            // StartIndex is less than or equal to the current index.
            if (t < 0.0)
            {
                t = 0.0;
            }

            // Math.Pow(0, β) is +∞ for β < 0 and 1 for β = 0 (per IEEE-754).
            // The prior allows β ∈ [-5, 5], so β < 0 is reachable. Clamp t to a
            // small positive floor when β < 0 so Predict(StartIndex) returns a
            // finite (though still extreme) value at the boundary rather than
            // propagating +Inf into downstream log-likelihood evaluation. The floor
            // value 1e-2 was chosen so that for β=-5 the magnitude Pow(1e-2, -5) = 1e10
            // is large enough to be confidently rejected by the data likelihood at the
            // sampler's accept/reject step yet finite enough that intermediate Math.Log
            // / numerical-derivative passes don't overflow. The previous floor 1e-12
            // produced Pow(1e-12, -5) = 1e60, which is unphysical and risks intermediate
            // numerical issues even though the LL ultimately rejects.
            if (t == 0.0 && b < 0.0)
            {
                t = 1e-2;
            }

            return a * Math.Pow(t, b);
        }

        /// <inheritdoc/>
        public override ITrendModel Clone()
        {
            var model = new PowerTrend
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
