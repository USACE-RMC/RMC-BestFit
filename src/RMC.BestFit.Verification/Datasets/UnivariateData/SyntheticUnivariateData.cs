using Numerics.Distributions;
using RMC.BestFit.Models;

namespace RMC.BestFit.Verification.Datasets.UnivariateData
{
    /// <summary>
    /// A collection of methods to generate synthetic univariate datasets for testing.
    /// Provides data generators for all 15 supported univariate distributions.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     <b>Authors:</b>
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    ///     <b>Purpose:</b>
    ///     These methods generate synthetic data with known true parameters for validation
    ///     testing of Bayesian parameter estimation. Each method returns both the generated
    ///     data and the true parameters used for generation, allowing tests to verify that
    ///     posterior estimates recover the true values within acceptable tolerance.
    /// </para>
    /// </remarks>
    public static class SyntheticUnivariateData
    {
        #region Exponential Family

        /// <summary>
        /// Generates synthetic data from an Exponential distribution.
        /// </summary>
        /// <param name="xi">The location parameter. Default = 0.0.</param>
        /// <param name="alpha">The scale parameter (mean = xi + alpha). Default = 50.0.</param>
        /// <param name="n">The sample size to simulate. Default = 1000.</param>
        /// <param name="prngSeed">The pseudo random number generator seed. Default = 12345.</param>
        /// <returns>A tuple containing the DataFrame and the true parameters [Xi, Alpha].</returns>
        public static (DataFrame DataFrame, double[] TrueParameters) GenerateExponentialData(
            double xi = 0.0, double alpha = 50.0, int n = 1000, int prngSeed = 12345)
        {
            var dist = new Exponential(xi, alpha);
            var values = dist.GenerateRandomValues(n, prngSeed);
            var df = new DataFrame();
            df.ExactSeries = new ExactSeries(values);
            return (df, new[] { xi, alpha });
        }

        /// <summary>
        /// Generates synthetic data from a Gamma distribution.
        /// </summary>
        /// <param name="alpha">The shape parameter. Default = 5.0.</param>
        /// <param name="beta">The rate parameter. Default = 2.0.</param>
        /// <param name="n">The sample size to simulate. Default = 1000.</param>
        /// <param name="prngSeed">The pseudo random number generator seed. Default = 12345.</param>
        /// <returns>A tuple containing the DataFrame and the true parameters [Alpha, Beta].</returns>
        public static (DataFrame DataFrame, double[] TrueParameters) GenerateGammaData(
            double alpha = 5.0, double beta = 2.0, int n = 1000, int prngSeed = 12345)
        {
            var dist = new GammaDistribution(alpha, beta);
            var values = dist.GenerateRandomValues(n, prngSeed);
            var df = new DataFrame();
            df.ExactSeries = new ExactSeries(values);
            return (df, new[] { alpha, beta });
        }

        /// <summary>
        /// Generates synthetic data from a Weibull distribution (2-parameter reliability version).
        /// </summary>
        /// <param name="lambda">The scale parameter λ (lambda). Default = 100.0.</param>
        /// <param name="kappa">The shape parameter κ (kappa). Default = 2.5.</param>
        /// <param name="n">The sample size to simulate. Default = 1000.</param>
        /// <param name="prngSeed">The pseudo random number generator seed. Default = 12345.</param>
        /// <returns>A tuple containing the DataFrame and the true parameters [Lambda, Kappa].</returns>
        public static (DataFrame DataFrame, double[] TrueParameters) GenerateWeibullData(
            double lambda = 100.0, double kappa = 2.5, int n = 1000, int prngSeed = 12345)
        {
            var dist = new Weibull(lambda, kappa);
            var values = dist.GenerateRandomValues(n, prngSeed);
            var df = new DataFrame();
            df.ExactSeries = new ExactSeries(values);
            return (df, new[] { lambda, kappa });
        }

        #endregion

        #region Normal Family

        /// <summary>
        /// Generates synthetic data from a Normal distribution.
        /// </summary>
        /// <param name="mu">The mean of the distribution. Default = 100.0.</param>
        /// <param name="sigma">The standard deviation of the distribution. Default = 15.0.</param>
        /// <param name="n">The sample size to simulate. Default = 1000.</param>
        /// <param name="prngSeed">The pseudo random number generator seed. Default = 12345.</param>
        /// <returns>A tuple containing the DataFrame and the true parameters [Mu, Sigma].</returns>
        public static (DataFrame DataFrame, double[] TrueParameters) GenerateNormalData(
            double mu = 100.0, double sigma = 15.0, int n = 1000, int prngSeed = 12345)
        {
            var dist = new Normal(mu, sigma);
            var values = dist.GenerateRandomValues(n, prngSeed);
            var df = new DataFrame();
            df.ExactSeries = new ExactSeries(values);
            return (df, new[] { mu, sigma });
        }

        /// <summary>
        /// Generates synthetic data from a Generalized Normal distribution.
        /// </summary>
        /// <param name="xi">The location parameter. Default = 100.0.</param>
        /// <param name="alpha">The scale parameter. Default = 15.0.</param>
        /// <param name="kappa">The shape parameter. Default = 0.1.</param>
        /// <param name="n">The sample size to simulate. Default = 1000.</param>
        /// <param name="prngSeed">The pseudo random number generator seed. Default = 12345.</param>
        /// <returns>A tuple containing the DataFrame and the true parameters [Xi, Alpha, Kappa].</returns>
        public static (DataFrame DataFrame, double[] TrueParameters) GenerateGeneralizedNormalData(
            double xi = 100.0, double alpha = 15.0, double kappa = 0.1, int n = 1000, int prngSeed = 12345)
        {
            var dist = new GeneralizedNormal(xi, alpha, kappa);
            var values = dist.GenerateRandomValues(n, prngSeed);
            var df = new DataFrame();
            df.ExactSeries = new ExactSeries(values);
            return (df, new[] { xi, alpha, kappa });
        }

        /// <summary>
        /// Generates synthetic data from a Logistic distribution.
        /// </summary>
        /// <param name="xi">The location parameter. Default = 100.0.</param>
        /// <param name="alpha">The scale parameter. Default = 10.0.</param>
        /// <param name="n">The sample size to simulate. Default = 1000.</param>
        /// <param name="prngSeed">The pseudo random number generator seed. Default = 12345.</param>
        /// <returns>A tuple containing the DataFrame and the true parameters [Xi, Alpha].</returns>
        public static (DataFrame DataFrame, double[] TrueParameters) GenerateLogisticData(
            double xi = 100.0, double alpha = 10.0, int n = 1000, int prngSeed = 12345)
        {
            var dist = new Logistic(xi, alpha);
            var values = dist.GenerateRandomValues(n, prngSeed);
            var df = new DataFrame();
            df.ExactSeries = new ExactSeries(values);
            return (df, new[] { xi, alpha });
        }

        /// <summary>
        /// Generates synthetic data from a Generalized Logistic distribution.
        /// </summary>
        /// <param name="xi">The location parameter. Default = 100.0.</param>
        /// <param name="alpha">The scale parameter. Default = 15.0.</param>
        /// <param name="kappa">The shape parameter. Default = 0.1.</param>
        /// <param name="n">The sample size to simulate. Default = 1000.</param>
        /// <param name="prngSeed">The pseudo random number generator seed. Default = 12345.</param>
        /// <returns>A tuple containing the DataFrame and the true parameters [Xi, Alpha, Kappa].</returns>
        public static (DataFrame DataFrame, double[] TrueParameters) GenerateGeneralizedLogisticData(
            double xi = 100.0, double alpha = 15.0, double kappa = 0.1, int n = 1000, int prngSeed = 12345)
        {
            var dist = new GeneralizedLogistic(xi, alpha, kappa);
            var values = dist.GenerateRandomValues(n, prngSeed);
            var df = new DataFrame();
            df.ExactSeries = new ExactSeries(values);
            return (df, new[] { xi, alpha, kappa });
        }

        #endregion

        #region Extreme Value Family

        /// <summary>
        /// Generates synthetic data from a Gumbel (Type I Extreme Value) distribution.
        /// </summary>
        /// <param name="xi">The location parameter. Default = 100.0.</param>
        /// <param name="alpha">The scale parameter. Default = 20.0.</param>
        /// <param name="n">The sample size to simulate. Default = 1000.</param>
        /// <param name="prngSeed">The pseudo random number generator seed. Default = 12345.</param>
        /// <returns>A tuple containing the DataFrame and the true parameters [Xi, Alpha].</returns>
        public static (DataFrame DataFrame, double[] TrueParameters) GenerateGumbelData(
            double xi = 100.0, double alpha = 20.0, int n = 1000, int prngSeed = 12345)
        {
            var dist = new Gumbel(xi, alpha);
            var values = dist.GenerateRandomValues(n, prngSeed);
            var df = new DataFrame();
            df.ExactSeries = new ExactSeries(values);
            return (df, new[] { xi, alpha });
        }

        /// <summary>
        /// Generates synthetic data from a Generalized Extreme Value (GEV) distribution.
        /// </summary>
        /// <param name="xi">The location parameter. Default = 100.0.</param>
        /// <param name="alpha">The scale parameter. Default = 20.0.</param>
        /// <param name="kappa">The shape parameter. Default = -0.1 (Frechet-like).</param>
        /// <param name="n">The sample size to simulate. Default = 1000.</param>
        /// <param name="prngSeed">The pseudo random number generator seed. Default = 12345.</param>
        /// <returns>A tuple containing the DataFrame and the true parameters [Xi, Alpha, Kappa].</returns>
        public static (DataFrame DataFrame, double[] TrueParameters) GenerateGeneralizedExtremeValueData(
            double xi = 100.0, double alpha = 20.0, double kappa = -0.1, int n = 1000, int prngSeed = 12345)
        {
            var dist = new GeneralizedExtremeValue(xi, alpha, kappa);
            var values = dist.GenerateRandomValues(n, prngSeed);
            var df = new DataFrame();
            df.ExactSeries = new ExactSeries(values);
            return (df, new[] { xi, alpha, kappa });
        }

        /// <summary>
        /// Generates synthetic data from a Generalized Pareto distribution.
        /// Used for peaks-over-threshold (POT) analysis.
        /// </summary>
        /// <param name="xi">The location/threshold parameter. Default = 0.0.</param>
        /// <param name="alpha">The scale parameter. Default = 50.0.</param>
        /// <param name="kappa">The shape parameter. Default = 0.1.</param>
        /// <param name="n">The sample size to simulate. Default = 1000.</param>
        /// <param name="prngSeed">The pseudo random number generator seed. Default = 12345.</param>
        /// <returns>A tuple containing the DataFrame and the true parameters [Xi, Alpha, Kappa].</returns>
        public static (DataFrame DataFrame, double[] TrueParameters) GenerateGeneralizedParetoData(
            double xi = 0.0, double alpha = 50.0, double kappa = 0.1, int n = 1000, int prngSeed = 12345)
        {
            var dist = new GeneralizedPareto(xi, alpha, kappa);
            var values = dist.GenerateRandomValues(n, prngSeed);
            var df = new DataFrame();
            df.ExactSeries = new ExactSeries(values);
            return (df, new[] { xi, alpha, kappa });
        }

        /// <summary>
        /// Generates synthetic data from a Kappa Four-parameter distribution.
        /// </summary>
        /// <param name="xi">The location parameter. Default = 100.0.</param>
        /// <param name="alpha">The scale parameter. Default = 20.0.</param>
        /// <param name="kappa">The first shape parameter. Default = -0.1.</param>
        /// <param name="h">The second shape parameter. Default = 0.1.</param>
        /// <param name="n">The sample size to simulate. Default = 1000.</param>
        /// <param name="prngSeed">The pseudo random number generator seed. Default = 12345.</param>
        /// <returns>A tuple containing the DataFrame and the true parameters [Xi, Alpha, Kappa, H].</returns>
        public static (DataFrame DataFrame, double[] TrueParameters) GenerateKappaFourData(
            double xi = 100.0, double alpha = 20.0, double kappa = -0.1, double h = 0.1, int n = 1000, int prngSeed = 12345)
        {
            var dist = new KappaFour(xi, alpha, kappa, h);
            var values = dist.GenerateRandomValues(n, prngSeed);
            var df = new DataFrame();
            df.ExactSeries = new ExactSeries(values);
            return (df, new[] { xi, alpha, kappa, h });
        }

        #endregion

        #region Pearson Family

        /// <summary>
        /// Generates synthetic data from a Pearson Type III distribution.
        /// </summary>
        /// <param name="mu">The mean of the distribution. Default = 100.0.</param>
        /// <param name="sigma">The standard deviation of the distribution. Default = 20.0.</param>
        /// <param name="gamma">The skewness of the distribution. Default = 0.5.</param>
        /// <param name="n">The sample size to simulate. Default = 1000.</param>
        /// <param name="prngSeed">The pseudo random number generator seed. Default = 12345.</param>
        /// <returns>A tuple containing the DataFrame and the true parameters [Mu, Sigma, Gamma].</returns>
        public static (DataFrame DataFrame, double[] TrueParameters) GeneratePearsonTypeIIIData(
            double mu = 100.0, double sigma = 20.0, double gamma = 0.5, int n = 1000, int prngSeed = 12345)
        {
            var dist = new PearsonTypeIII(mu, sigma, gamma);
            var values = dist.GenerateRandomValues(n, prngSeed);
            var df = new DataFrame();
            df.ExactSeries = new ExactSeries(values);
            return (df, new[] { mu, sigma, gamma });
        }

        /// <summary>
        /// Generates synthetic data from a Log-Pearson Type III (LP3) distribution.
        /// This is the standard distribution for flood frequency analysis in the United States (Bulletin 17C).
        /// </summary>
        /// <param name="mu">The mean of the log-transformed data. Default = 3.0.</param>
        /// <param name="sigma">The standard deviation of the log-transformed data. Default = 0.5.</param>
        /// <param name="gamma">The skewness of the log-transformed data. Default = 0.2.</param>
        /// <param name="n">The sample size to simulate. Default = 1000.</param>
        /// <param name="prngSeed">The pseudo random number generator seed. Default = 12345.</param>
        /// <returns>A tuple containing the DataFrame and the true parameters [Mu, Sigma, Gamma].</returns>
        public static (DataFrame DataFrame, double[] TrueParameters) GenerateLogPearsonTypeIIIData(
            double mu = 3.0, double sigma = 0.5, double gamma = 0.2, int n = 1000, int prngSeed = 12345)
        {
            var dist = new LogPearsonTypeIII(mu, sigma, gamma);
            var values = dist.GenerateRandomValues(n, prngSeed);
            var df = new DataFrame();
            df.ExactSeries = new ExactSeries(values);
            return (df, new[] { mu, sigma, gamma });
        }

        #endregion

        #region Log-Transformed Distributions

        /// <summary>
        /// Generates synthetic data from a Log-Normal distribution.
        /// </summary>
        /// <param name="mu">The mean of the log-transformed data. Default = 3.0.</param>
        /// <param name="sigma">The standard deviation of the log-transformed data. Default = 0.5.</param>
        /// <param name="n">The sample size to simulate. Default = 1000.</param>
        /// <param name="prngSeed">The pseudo random number generator seed. Default = 12345.</param>
        /// <returns>A tuple containing the DataFrame and the true parameters [Mu, Sigma].</returns>
        public static (DataFrame DataFrame, double[] TrueParameters) GenerateLogNormalData(
            double mu = 3.0, double sigma = 0.5, int n = 1000, int prngSeed = 12345)
        {
            var dist = new LogNormal(mu, sigma);
            var values = dist.GenerateRandomValues(n, prngSeed);
            var df = new DataFrame();
            df.ExactSeries = new ExactSeries(values);
            return (df, new[] { mu, sigma });
        }

        /// <summary>
        /// Generates synthetic data from a Ln-Normal (base-e Log-Normal) distribution.
        /// This is equivalent to the standard Log-Normal but uses natural logarithm explicitly.
        /// </summary>
        /// <param name="mu">The mean of the ln-transformed data. Default = 4.5.</param>
        /// <param name="sigma">The standard deviation of the ln-transformed data. Default = 0.4.</param>
        /// <param name="n">The sample size to simulate. Default = 1000.</param>
        /// <param name="prngSeed">The pseudo random number generator seed. Default = 12345.</param>
        /// <returns>A tuple containing the DataFrame and the true parameters [Mu, Sigma].</returns>
        public static (DataFrame DataFrame, double[] TrueParameters) GenerateLnNormalData(
            double mu = 4.5, double sigma = 0.4, int n = 1000, int prngSeed = 12345)
        {
            var dist = new LnNormal(mu, sigma);
            var values = dist.GenerateRandomValues(n, prngSeed);
            var df = new DataFrame();
            df.ExactSeries = new ExactSeries(values);
            return (df, new[] { mu, sigma });
        }

        #endregion
    }
}
