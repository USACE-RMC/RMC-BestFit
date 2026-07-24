using Numerics.Distributions;
using RMC.BestFit.Models;

namespace RMC.BestFit.Tests.Documentation.Examples
{
    /// <summary>
    /// Contains compile-checked parameter and prior examples.
    /// </summary>
    internal static class ParameterPriorExamples
    {
        #region doc:priors-single-quantile
        private static UnivariateDistribution ConfigureSingleQuantilePrior(global::RMC.BestFit.Models.DataFrame dataFrame)
        {
            var model = new UnivariateDistribution(
                dataFrame,
                UnivariateDistributionType.Normal)
            {
                UseDefaultFlatPriors = false,
                UseJeffreysRuleForScale = true,
                EnableQuantilePriors = true,
                UseSingleQuantile = true,
                QuantilePriors = new List<QuantilePrior>
                {
                    new QuantilePrior(0.01, new Normal(2500.0, 300.0))
                }
            };

            model.Parameters[0].PriorDistribution = new Normal(1400.0, 500.0);
            model.ProcessQuantilePriors();
            return model;
        }
        #endregion
    }
}
