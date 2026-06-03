using RMC.BestFit.Models.TrendFunctions.Support;
using System.Xml.Linq;

namespace RMC.BestFit.Models.TrendFunctions
{
    /// <summary>
    /// Trend model that is constant in time:
    /// <c>y(t) = α</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// </remarks>
    public class ConstantTrend : TrendModelBase
    {
        /// <inheritdoc/>
        public ConstantTrend() : base() { }

        /// <inheritdoc/>
        public ConstantTrend(XElement xElement) : base(xElement) { }

        /// <inheritdoc/>
        public override TrendModelType Type => TrendModelType.Constant;

        /// <inheritdoc/>
        public override void SetDefaultParameters()
        {
            Parameters = new List<ModelParameter>
            {
                new ModelParameter
                {
                    OwnerName = OwnerName,
                    Name = "(α)"
                }
            };
        }

        /// <inheritdoc/>
        public override double Predict(int index)
        {
            return Parameters[0].Value;
        }

        /// <inheritdoc/>
        public override ITrendModel Clone()
        {
            var model = new ConstantTrend
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
