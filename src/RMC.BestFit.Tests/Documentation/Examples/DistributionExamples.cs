namespace RMC.BestFit.Tests.Documentation.Examples
{
    /// <summary>
    /// Contains compile-checked examples for the fifteen supported univariate families.
    /// </summary>
    internal static class DistributionExamples
    {
        #region doc:distribution-exponential
        private static (double Density, double Cdf, double Quantile) EvaluateExponential()
        {
            var distribution = new global::Numerics.Distributions.Exponential(1000.0, 500.0);
            return Evaluate(distribution, value: 1800.0, nonExceedanceProbability: 0.99);
        }
        #endregion

        #region doc:distribution-gamma
        private static (double Density, double Cdf, double Quantile) EvaluateGamma()
        {
            var distribution = new global::Numerics.Distributions.GammaDistribution(500.0, 3.0);
            return Evaluate(distribution, value: 1800.0, nonExceedanceProbability: 0.99);
        }
        #endregion

        #region doc:distribution-gev
        private static (double Density, double Cdf, double Quantile) EvaluateGev()
        {
            var distribution = new global::Numerics.Distributions.GeneralizedExtremeValue(
                1500.0, 400.0, -0.10);
            return Evaluate(distribution, value: 2200.0, nonExceedanceProbability: 0.99);
        }
        #endregion

        #region doc:distribution-generalized-logistic
        private static (double Density, double Cdf, double Quantile) EvaluateGeneralizedLogistic()
        {
            var distribution = new global::Numerics.Distributions.GeneralizedLogistic(
                1500.0, 400.0, -0.10);
            return Evaluate(distribution, value: 2200.0, nonExceedanceProbability: 0.99);
        }
        #endregion

        #region doc:distribution-generalized-normal
        private static (double Density, double Cdf, double Quantile) EvaluateGeneralizedNormal()
        {
            var distribution = new global::Numerics.Distributions.GeneralizedNormal(
                1500.0, 400.0, -0.10);
            return Evaluate(distribution, value: 2200.0, nonExceedanceProbability: 0.99);
        }
        #endregion

        #region doc:distribution-generalized-pareto
        private static (double Density, double Cdf, double Quantile) EvaluateGeneralizedPareto()
        {
            var distribution = new global::Numerics.Distributions.GeneralizedPareto(
                1000.0, 400.0, -0.15);
            return Evaluate(distribution, value: 1800.0, nonExceedanceProbability: 0.99);
        }
        #endregion

        #region doc:distribution-gumbel
        private static (double Density, double Cdf, double Quantile) EvaluateGumbel()
        {
            var distribution = new global::Numerics.Distributions.Gumbel(1500.0, 400.0);
            return Evaluate(distribution, value: 2200.0, nonExceedanceProbability: 0.99);
        }
        #endregion

        #region doc:distribution-kappa-four
        private static (double Density, double Cdf, double Quantile) EvaluateKappaFour()
        {
            var distribution = new global::Numerics.Distributions.KappaFour(
                1500.0, 400.0, 0.10, 0.20);
            return Evaluate(distribution, value: 2200.0, nonExceedanceProbability: 0.99);
        }
        #endregion

        #region doc:distribution-ln-normal
        private static (double Density, double Cdf, double Quantile) EvaluateLnNormal()
        {
            var distribution = new global::Numerics.Distributions.LnNormal(1500.0, 500.0);
            return Evaluate(distribution, value: 2200.0, nonExceedanceProbability: 0.99);
        }
        #endregion

        #region doc:distribution-logistic
        private static (double Density, double Cdf, double Quantile) EvaluateLogistic()
        {
            var distribution = new global::Numerics.Distributions.Logistic(1500.0, 300.0);
            return Evaluate(distribution, value: 2200.0, nonExceedanceProbability: 0.99);
        }
        #endregion

        #region doc:distribution-log-normal
        private static (double Density, double Cdf, double Quantile) EvaluateLogNormal()
        {
            var distribution = new global::Numerics.Distributions.LogNormal(3.20, 0.20);
            return Evaluate(distribution, value: 2200.0, nonExceedanceProbability: 0.99);
        }
        #endregion

        #region doc:distribution-log-pearson-iii
        private static (double Density, double Cdf, double Quantile) EvaluateLogPearsonTypeIII()
        {
            var distribution = new global::Numerics.Distributions.LogPearsonTypeIII(
                3.20, 0.25, 0.30);
            return Evaluate(distribution, value: 2200.0, nonExceedanceProbability: 0.99);
        }
        #endregion

        #region doc:distribution-normal
        private static (double Density, double Cdf, double Quantile) EvaluateNormal()
        {
            var distribution = new global::Numerics.Distributions.Normal(1500.0, 400.0);
            return Evaluate(distribution, value: 2200.0, nonExceedanceProbability: 0.99);
        }
        #endregion

        #region doc:distribution-pearson-iii
        private static (double Density, double Cdf, double Quantile) EvaluatePearsonTypeIII()
        {
            var distribution = new global::Numerics.Distributions.PearsonTypeIII(
                1500.0, 400.0, 0.50);
            return Evaluate(distribution, value: 2200.0, nonExceedanceProbability: 0.99);
        }
        #endregion

        #region doc:distribution-weibull
        private static (double Density, double Cdf, double Quantile) EvaluateWeibull()
        {
            var distribution = new global::Numerics.Distributions.Weibull(1500.0, 2.0);
            return Evaluate(distribution, value: 2200.0, nonExceedanceProbability: 0.99);
        }
        #endregion

        /// <summary>
        /// Evaluates a distribution at a magnitude and nonexceedance probability.
        /// </summary>
        /// <param name="distribution">Distribution to evaluate.</param>
        /// <param name="value">Magnitude at which to evaluate the density and CDF.</param>
        /// <param name="nonExceedanceProbability">Probability used for the quantile.</param>
        /// <returns>The density, CDF, and quantile values.</returns>
        private static (double Density, double Cdf, double Quantile) Evaluate(
            global::Numerics.Distributions.UnivariateDistributionBase distribution,
            double value,
            double nonExceedanceProbability)
        {
            return (
                distribution.PDF(value),
                distribution.CDF(value),
                distribution.InverseCDF(nonExceedanceProbability));
        }
    }
}
