using RMC.BestFit.Models.TrendFunctions.Support;
using System.Xml.Linq;

namespace RMC.BestFit.Models.TrendFunctions
{

    /// <summary>
    /// Step-function trend model with a single change point:
    /// <c>y(t) = μ₁</c> for <c>t ≤ tₛ</c> and
    /// <c>y(t) = μ₂</c> for <c>t &gt; tₛ</c>, where <c>tₛ</c> is the step time
    /// (the change-point index at which the mean shifts).
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// </remarks>
    public class StepFunction : TrendModelBase
    {
        /// <inheritdoc/>
        public StepFunction() : base() { }

        /// <inheritdoc/>
        public StepFunction(XElement xElement) : base(xElement) { }

        /// <inheritdoc/>
        public override TrendModelType Type => TrendModelType.StepFunction;

        /// <inheritdoc/>
        public override void SetDefaultParameters()
        {
            Parameters = new List<ModelParameter>
            {
                new ModelParameter
                {
                    OwnerName = OwnerName,
                    Name = "(μ₁)"
                },
                new ModelParameter
                {
                    OwnerName = OwnerName,
                    Name = "(μ₂)"
                },
                // Change point (index)
                new ModelParameter
                {
                    OwnerName = OwnerName,
                    Name = "(tₛ)"
                }
            };
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Inclusion convention: at the change-point itself (t == tc), the function
        /// returns μ₁ (the pre-change value). Strictly after the change-point (t &gt; tc),
        /// it returns μ₂. This matches the standard left-continuous step convention.
        /// </remarks>
        public override double Predict(int index)
        {
            double t = index - StartIndex;
            double mu1 = Parameters[0].Value;
            double mu2 = Parameters[1].Value;
            double tc = Parameters[2].Value - StartIndex;

            // Step at change-point tc; left-continuous (μ₁ for t ≤ tc, μ₂ for t > tc).
            if (t <= tc)
            {
                return mu1;
            }

            return mu2;
        }

        /// <inheritdoc/>
        public override ITrendModel Clone()
        {
            var model = new StepFunction
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
