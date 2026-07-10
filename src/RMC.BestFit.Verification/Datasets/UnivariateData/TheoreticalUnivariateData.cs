using Numerics.Data.Statistics;
using Numerics.Distributions;
using RMC.BestFit.Models;

namespace RMC.BestFit.Verification.Datasets.UnivariateData
{
    /// <summary>
    /// Provides methods for generating theoretical (deterministic) datasets from known distributions
    /// using Weibull plotting positions. These datasets contain the exact population quantiles,
    /// making them ideal for verifying that estimators recover known parameter values.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unlike <see cref="SyntheticUnivariateData"/> which generates random samples, this class
    /// produces deterministic datasets via <c>InverseCDF(PlottingPositions.Weibull(n))</c>.
    /// The resulting values are equally-spaced quantiles of the theoretical distribution,
    /// perfectly representative of the population.
    /// </para>
    /// <para>
    /// For Monte Carlo coverage tests that require independent random samples, use
    /// <c>trueDist.GenerateRandomValues(n, prng)</c> instead of these methods.
    /// </para>
    /// </remarks>
    public static class TheoreticalUnivariateData
    {
        /// <summary>
        /// Generates synthetic data from an Exponential distribution using Weibull plotting positions.
        /// </summary>
        /// <param name="xi">The location parameter (lower bound). Default = 10.0.</param>
        /// <param name="alpha">The scale parameter. Default = 50.0.</param>
        /// <param name="n">The sample size to simulate. Default = 1000.</param>
        /// <returns>A tuple containing the DataFrame and the true parameters [Xi, Alpha].</returns>
        public static (DataFrame DataFrame, double[] TrueParameters) GenerateExponentialData(
            double xi = 10.0, double alpha = 50.0, int n = 1000)
        {
            var dist = new Exponential(xi, alpha);
            var values = dist.InverseCDF(PlottingPositions.Weibull(n));
            var df = new DataFrame();
            df.ExactSeries = new ExactSeries(values);
            return (df, new[] { xi, alpha });
        }

        /// <summary>
        /// Generates synthetic data from a Gamma distribution using Weibull plotting positions.
        /// </summary>
        /// <param name="alpha">The shape parameter. Default = 5.0.</param>
        /// <param name="beta">The rate parameter. Default = 2.0.</param>
        /// <param name="n">The sample size to simulate. Default = 1000.</param>
        /// <returns>A tuple containing the DataFrame and the true parameters [Alpha, Beta].</returns>
        public static (DataFrame DataFrame, double[] TrueParameters) GenerateGammaData(
            double alpha = 5.0, double beta = 2.0, int n = 1000)
        {
            var dist = new GammaDistribution(alpha, beta);
            var values = dist.InverseCDF(PlottingPositions.Weibull(n));
            var df = new DataFrame();
            df.ExactSeries = new ExactSeries(values);
            return (df, new[] { alpha, beta });
        }

        /// <summary>
        /// Generates synthetic data from a Normal distribution using Weibull plotting positions.
        /// </summary>
        /// <param name="mu">The mean. Default = 100.0.</param>
        /// <param name="sigma">The standard deviation. Default = 15.0.</param>
        /// <param name="n">The sample size to simulate. Default = 1000.</param>
        /// <returns>A tuple containing the DataFrame and the true parameters [Mu, Sigma].</returns>
        public static (DataFrame DataFrame, double[] TrueParameters) GenerateNormalData(
            double mu = 100.0, double sigma = 15.0, int n = 1000)
        {
            var dist = new Normal(mu, sigma);
            var values = dist.InverseCDF(PlottingPositions.Weibull(n));
            var df = new DataFrame();
            df.ExactSeries = new ExactSeries(values);
            return (df, new[] { mu, sigma });
        }

        /// <summary>
        /// Generates synthetic data from a Pearson Type III distribution using Weibull plotting positions.
        /// </summary>
        /// <param name="mu">The mean. Default = 100.0.</param>
        /// <param name="sigma">The standard deviation. Default = 20.0.</param>
        /// <param name="gamma">The skewness coefficient. Default = 0.5.</param>
        /// <param name="n">The sample size to simulate. Default = 1000.</param>
        /// <returns>A tuple containing the DataFrame and the true parameters [Mu, Sigma, Gamma].</returns>
        public static (DataFrame DataFrame, double[] TrueParameters) GeneratePearsonTypeIIIData(
            double mu = 100.0, double sigma = 20.0, double gamma = 0.5, int n = 1000)
        {
            var dist = new PearsonTypeIII(mu, sigma, gamma);
            var values = dist.InverseCDF(PlottingPositions.Weibull(n));
            var df = new DataFrame();
            df.ExactSeries = new ExactSeries(values);
            return (df, new[] { mu, sigma, gamma });
        }

        /// <summary>
        /// Generates synthetic data from a Log-Normal distribution.
        /// </summary>
        /// <param name="mu">The mean of the log-transformed data. Default = 3.0.</param>
        /// <param name="sigma">The standard deviation of the log-transformed data. Default = 0.5.</param>
        /// <param name="n">The sample size to simulate. Default = 1000.</param>
        /// <returns>A tuple containing the DataFrame and the true parameters [Mu, Sigma].</returns>
        public static (DataFrame DataFrame, double[] TrueParameters) GenerateLogNormalData(
            double mu = 3.0, double sigma = 0.5, int n = 1000)
        {
            var dist = new LogNormal(mu, sigma);
            var values = dist.InverseCDF(PlottingPositions.Weibull(n));
            var df = new DataFrame();
            df.ExactSeries = new ExactSeries(values);
            return (df, new[] { mu, sigma });
        }

        /// <summary>
        /// Generates synthetic data from a Log-Pearson Type III (LP3) distribution.
        /// This is the standard distribution for flood frequency analysis in the United States (Bulletin 17C).
        /// </summary>
        /// <param name="mu">The mean of the log-transformed data. Default = 3.0.</param>
        /// <param name="sigma">The standard deviation of the log-transformed data. Default = 0.5.</param>
        /// <param name="gamma">The skewness of the log-transformed data. Default = 0.2.</param>
        /// <param name="n">The sample size to simulate. Default = 1000.</param>
        /// <returns>A tuple containing the DataFrame and the true parameters [Mu, Sigma, Gamma].</returns>
        public static (DataFrame DataFrame, double[] TrueParameters) GenerateLogPearsonTypeIIIData(
            double mu = 3.0, double sigma = 0.5, double gamma = 0.2, int n = 1000)
        {
            var dist = new LogPearsonTypeIII(mu, sigma, gamma);
            var values = dist.InverseCDF(PlottingPositions.Weibull(n));
            var df = new DataFrame();
            df.ExactSeries = new ExactSeries(values);
            return (df, new[] { mu, sigma, gamma });
        }
    }
}

