using Numerics.Distributions;
using Numerics.Sampling;
using RMC.BestFit.Models;
using RMC.BestFit.Models.TrendFunctions.Support;

namespace RMC.BestFit.Verification.Datasets.UnivariateData
{
    /// <summary>
    /// A collection of methods to generate synthetic nonstationary univariate datasets for testing.
    /// </summary>
    /// <remarks>
    /// Every generator evaluates the configured trend models at each observation's own time index
    /// (0 through n - 1, the index convention of an <see cref="ExactSeries"/> built from a value list
    /// and therefore the <c>StartIndex = 0</c> convention the fitted model uses) and draws that
    /// observation from the distribution at that index. The returned generating coordinates and
    /// observation indices therefore describe the same trend convention used by the fitted model.
    /// </remarks>
    public static class SyntheticNonstationaryData
    {
        /// <summary>
        /// Draws one observation per time index from the nonstationary model, evaluating the trend
        /// models at indices 0 through <paramref name="n"/> - 1.
        /// </summary>
        /// <param name="model">The configured nonstationary model with its true parameter values set.</param>
        /// <param name="n">The number of observations.</param>
        /// <param name="prngSeed">The pseudo random number generator seed.</param>
        /// <returns>The generated values in time order.</returns>
        private static double[] GenerateTrendValues(UnivariateDistribution model, int n, int prngSeed)
        {
            var prng = new MersenneTwister(prngSeed);
            var values = new double[n];
            for (int t = 0; t < n; t++)
            {
                model.SetDistributionParameterValues(t);
                values[t] = model.Distribution.InverseCDF(prng.NextDouble());
            }
            return values;
        }


        /// <summary>
        /// Generates synthetic nonstationary data from a normal distribution with a constant trend on the mean parameter.
        /// </summary>
        /// <param name="n">The sample size to simulate. Default = 1000</param>
        /// <param name="prngSeed">The pseudo random number generator seed. Default = 12345.</param>
        /// <returns></returns>
        public static (DataFrame DataFrame, double[] TrueParameters) GenerateConstantTrendData(int n = 1000, int prngSeed = 12345)
        {
            var trueParameters = new double[] { 100.0, 15.0 };
            var model = new UnivariateDistribution();
            model.DistributionType = UnivariateDistributionType.Normal;
            model.IsNonstationary = true;
            model.SetTrendModel(0, TrendModelType.Constant);
            model.SetParameterValues(trueParameters);
            var values = GenerateTrendValues(model, n, prngSeed);
            var df = new DataFrame();
            df.ExactSeries = new ExactSeries(values);
            return (df, trueParameters);
        }

        /// <summary>
        /// Generates seeded Normal observations with a reciprocal trend on the mean.
        /// </summary>
        /// <param name="n">The number of response/covariate rows. Default = 1000.</param>
        /// <param name="prngSeed">The deterministic pseudo-random seed. Default = 12345.</param>
        /// <returns>The data frame and parents [mean alpha, mean beta, Normal sigma].</returns>
        /// <remarks>
        /// The small reciprocal coefficients define a mean response from 100 at index zero to about 50 at
        /// index 999. This intentionally correlated parameterization is assessed by identified response
        /// ordinates rather than coefficient-wise inclusion.
        /// </remarks>
        public static (DataFrame DataFrame, double[] TrueParameters) GenerateReciprocalMeanTrendData(int n = 1000, int prngSeed = 12345)
        {
            var trueParameters = new double[] { 0.01d, 0.00001d, 15d };
            var model = new UnivariateDistribution { DistributionType = UnivariateDistributionType.Normal, IsNonstationary = true };
            model.SetTrendModel(0, TrendModelType.Reciprocal);
            model.SetParameterValues(trueParameters);
            var values = GenerateTrendValues(model, n, prngSeed);
            var df = new DataFrame { ExactSeries = new ExactSeries(values) };
            return (df, trueParameters);
        }

        /// <summary>
        /// Generates synthetic nonstationary data from a normal distribution with a cubic trend on the mean parameter.
        /// </summary>
        /// <param name="n">The sample size to simulate. Default = 1000</param>
        /// <param name="prngSeed">The pseudo random number generator seed. Default = 12345.</param>
        /// <returns></returns>
        public static (DataFrame DataFrame, double[] TrueParameters) GenerateCubicTrendData(int n = 1000, int prngSeed = 12345)
        {
            var trueParameters = new double[] { 100.0, 0.2, -0.008, 0.00015, 15.0 };
            var model = new UnivariateDistribution();
            model.DistributionType = UnivariateDistributionType.Normal;
            model.IsNonstationary = true;
            model.SetTrendModel(0, TrendModelType.Cubic);
            model.SetParameterValues(trueParameters);
            var values = GenerateTrendValues(model, n, prngSeed);
            var df = new DataFrame();
            df.ExactSeries = new ExactSeries(values);
            return (df, trueParameters);
        }

        /// <summary>
        /// Generates synthetic nonstationary data from a normal distribution with a exponential trend on the mean parameter.
        /// </summary>
        /// <param name="n">The sample size to simulate. Default = 1000</param>
        /// <param name="prngSeed">The pseudo random number generator seed. Default = 12345.</param>
        /// <returns></returns>
        public static (DataFrame DataFrame, double[] TrueParameters) GenerateExponentialTrendData(int n = 1000, int prngSeed = 12345)
        {
            // The rate must lie inside the model's default prior bounds for the exponential rate,
            // +/- 5/(n - 1) = +/- 0.005 for 1,000 observations; 0.2 overflowed over 1,000 steps (TR-084).
            var trueParameters = new double[] { 50.0, 0.002, 15.0 };
            var model = new UnivariateDistribution();
            model.DistributionType = UnivariateDistributionType.Normal;
            model.IsNonstationary = true;
            model.SetTrendModel(0, TrendModelType.Exponential);
            model.SetParameterValues(trueParameters);
            var values = GenerateTrendValues(model, n, prngSeed);
            var df = new DataFrame();
            df.ExactSeries = new ExactSeries(values);
            return (df, trueParameters);
        }

        /// <summary>
        /// Generates synthetic nonstationary data from a normal distribution with a linear trend on the mean parameter.
        /// </summary>
        /// <param name="n">The sample size to simulate. Default = 1000</param>
        /// <param name="prngSeed">The pseudo random number generator seed. Default = 12345.</param>
        /// <returns></returns>
        public static (DataFrame DataFrame, double[] TrueParameters) GenerateLinearTrendData(int n = 1000, int prngSeed = 12345)
        {
            var trueParameters = new double[] { 80.0, 0.5, 15.0 };
            var model = new UnivariateDistribution();
            model.DistributionType = UnivariateDistributionType.Normal;
            model.IsNonstationary = true;
            model.SetTrendModel(0, TrendModelType.Linear);
            model.SetParameterValues(trueParameters);
            var values = GenerateTrendValues(model, n, prngSeed);
            var df = new DataFrame();
            df.ExactSeries = new ExactSeries(values);
            return (df, trueParameters);
        }

        /// <summary>
        /// Generates synthetic nonstationary data from a normal distribution with a logistic trend on the mean parameter.
        /// </summary>
        /// <param name="n">The sample size to simulate. Default = 1000</param>
        /// <param name="prngSeed">The pseudo random number generator seed. Default = 12345.</param>
        /// <returns></returns>
        public static (DataFrame DataFrame, double[] TrueParameters) GenerateLogisticTrendData(int n = 1000, int prngSeed = 12345)
        {
            // The rate must lie inside the model's default prior bounds for the logistic rate,
            // +/- 5/(n - 1) = +/- 0.005 for 1,000 observations (TR-084).
            var trueParameters = new double[] { 100.0, 0.004, 15.0 };
            var model = new UnivariateDistribution();
            model.DistributionType = UnivariateDistributionType.Normal;
            model.IsNonstationary = true;
            model.SetTrendModel(0, TrendModelType.Logistic);
            model.SetParameterValues(trueParameters);
            var values = GenerateTrendValues(model, n, prngSeed);
            var df = new DataFrame();
            df.ExactSeries = new ExactSeries(values);
            return (df, trueParameters);
        }

        /// <summary>
        /// Generates synthetic nonstationary data from a normal distribution with a power trend on the mean parameter.
        /// </summary>
        /// <param name="n">The sample size to simulate. Default = 1000</param>
        /// <param name="prngSeed">The pseudo random number generator seed. Default = 12345.</param>
        /// <returns></returns>
        public static (DataFrame DataFrame, double[] TrueParameters) GeneratePowerTrendData(int n = 1000, int prngSeed = 12345)
        {
            var trueParameters = new double[] { 100.0, 0.2, 15.0 };
            var model = new UnivariateDistribution();
            model.DistributionType = UnivariateDistributionType.Normal;
            model.IsNonstationary = true;
            model.SetTrendModel(0, TrendModelType.Power);
            model.SetParameterValues(trueParameters);
            var values = GenerateTrendValues(model, n, prngSeed);
            var df = new DataFrame();
            df.ExactSeries = new ExactSeries(values);
            return (df, trueParameters);
        }

        /// <summary>
        /// Generates synthetic nonstationary data from a normal distribution with a quadratic trend on the mean parameter.
        /// </summary>
        /// <param name="n">The sample size to simulate. Default = 1000</param>
        /// <param name="prngSeed">The pseudo random number generator seed. Default = 12345.</param>
        /// <returns></returns>
        public static (DataFrame DataFrame, double[] TrueParameters) GenerateQuadraticTrendData(int n = 1000, int prngSeed = 12345)
        {
            var trueParameters = new double[] { 100.0, -0.2, 0.008, 15.0 };
            var model = new UnivariateDistribution();
            model.DistributionType = UnivariateDistributionType.Normal;
            model.IsNonstationary = true;
            model.SetTrendModel(0, TrendModelType.Quadratic);
            model.SetParameterValues(trueParameters);
            var values = GenerateTrendValues(model, n, prngSeed);
            var df = new DataFrame();
            df.ExactSeries = new ExactSeries(values);
            return (df, trueParameters);
        }

        /// <summary>
        /// Generates synthetic nonstationary data from a normal distribution with a sinusoidal trend on the mean parameter.
        /// </summary>
        /// <param name="n">The sample size to simulate. Default = 1000</param>
        /// <param name="prngSeed">The pseudo random number generator seed. Default = 12345.</param>
        /// <returns></returns>
        public static (DataFrame DataFrame, double[] TrueParameters) GenerateSinusoidalTrendData(int n = 1000, int prngSeed = 12345)
        {
            var trueParameters = new double[] { 100.0, 25.0, 0.015, 3.0, 15.0 };
            var model = new UnivariateDistribution();
            model.DistributionType = UnivariateDistributionType.Normal;
            model.IsNonstationary = true;
            model.SetTrendModel(0, TrendModelType.Sinusoidal);
            model.SetParameterValues(trueParameters);
            var values = GenerateTrendValues(model, n, prngSeed);
            var df = new DataFrame();
            df.ExactSeries = new ExactSeries(values);
            return (df, trueParameters);
        }


        /// <summary>
        /// Generates synthetic nonstationary data from a normal distribution with a step function trend on the mean parameter.
        /// </summary>
        /// <param name="n">The sample size to simulate. Default = 1000</param>
        /// <param name="prngSeed">The pseudo random number generator seed. Default = 12345.</param>
        /// <returns></returns>
        public static (DataFrame DataFrame, double[] TrueParameters) GenerateStepFunctionTrendData(int n = 1000, int prngSeed = 12345)
        {
            var trueParameters = new double[] { 100.0, 150.0, 60.0, 15.0 };
            var model = new UnivariateDistribution();
            model.DistributionType = UnivariateDistributionType.Normal;
            model.IsNonstationary = true;
            model.SetTrendModel(0, TrendModelType.StepFunction);
            model.SetParameterValues(trueParameters);
            var values = GenerateTrendValues(model, n, prngSeed);
            var df = new DataFrame();
            df.ExactSeries = new ExactSeries(values);
            return (df, trueParameters);
        }

        #region Trend on Standard Deviation Only

        /// <summary>
        /// Generates synthetic nonstationary data from a normal distribution with a linear trend on the standard deviation parameter only.
        /// The mean remains constant at 100.0, while standard deviation increases linearly from 10.0 with slope 0.05.
        /// </summary>
        /// <param name="n">The sample size to simulate. Default = 1000</param>
        /// <param name="prngSeed">The pseudo random number generator seed. Default = 12345.</param>
        /// <returns>A tuple containing the DataFrame and the true parameters [Mean, Sigma_Intercept, Sigma_Slope].</returns>
        public static (DataFrame DataFrame, double[] TrueParameters) GenerateSigmaLinearTrendData(int n = 1000, int prngSeed = 12345)
        {
            // Parameters: [Mu (constant), Sigma_Intercept, Sigma_Slope]
            var trueParameters = new double[] { 100.0, 10.0, 0.05 };
            var model = new UnivariateDistribution();
            model.DistributionType = UnivariateDistributionType.Normal;
            model.IsNonstationary = true;
            model.SetTrendModel(1, TrendModelType.Linear); // Linear trend on sigma (parameter index 1)
            model.SetParameterValues(trueParameters);
            var values = GenerateTrendValues(model, n, prngSeed);
            var df = new DataFrame();
            df.ExactSeries = new ExactSeries(values);
            return (df, trueParameters);
        }

        /// <summary>
        /// Generates synthetic nonstationary data from a normal distribution with a quadratic trend on the standard deviation parameter only.
        /// The mean remains constant, while standard deviation follows a quadratic pattern.
        /// </summary>
        /// <param name="n">The sample size to simulate. Default = 1000</param>
        /// <param name="prngSeed">The pseudo random number generator seed. Default = 12345.</param>
        /// <returns>A tuple containing the DataFrame and the true parameters [Mean, Sigma_Intercept, Sigma_Linear, Sigma_Quadratic].</returns>
        public static (DataFrame DataFrame, double[] TrueParameters) GenerateSigmaQuadraticTrendData(int n = 1000, int prngSeed = 12345)
        {
            // Parameters: [Mu (constant), Sigma_Intercept, Sigma_Linear, Sigma_Quadratic]
            var trueParameters = new double[] { 100.0, 15.0, -0.1, 0.002 };
            var model = new UnivariateDistribution();
            model.DistributionType = UnivariateDistributionType.Normal;
            model.IsNonstationary = true;
            model.SetTrendModel(1, TrendModelType.Quadratic); // Quadratic trend on sigma
            model.SetParameterValues(trueParameters);
            var values = GenerateTrendValues(model, n, prngSeed);
            var df = new DataFrame();
            df.ExactSeries = new ExactSeries(values);
            return (df, trueParameters);
        }

        /// <summary>
        /// Generates synthetic nonstationary data from a normal distribution with an exponential trend on the standard deviation parameter only.
        /// The mean remains constant, while standard deviation grows exponentially.
        /// </summary>
        /// <param name="n">The sample size to simulate. Default = 1000</param>
        /// <param name="prngSeed">The pseudo random number generator seed. Default = 12345.</param>
        /// <returns>A tuple containing the DataFrame and the true parameters [Mean, Sigma_A, Sigma_B].</returns>
        public static (DataFrame DataFrame, double[] TrueParameters) GenerateSigmaExponentialTrendData(int n = 1000, int prngSeed = 12345)
        {
            // Parameters: [Mu (constant), Sigma_A (scale), Sigma_B (rate)]
            var trueParameters = new double[] { 100.0, 8.0, 0.003 };
            var model = new UnivariateDistribution();
            model.DistributionType = UnivariateDistributionType.Normal;
            model.IsNonstationary = true;
            model.SetTrendModel(1, TrendModelType.Exponential); // Exponential trend on sigma
            model.SetParameterValues(trueParameters);
            var values = GenerateTrendValues(model, n, prngSeed);
            var df = new DataFrame();
            df.ExactSeries = new ExactSeries(values);
            return (df, trueParameters);
        }

        #endregion

        #region Trend on Both Mean and Standard Deviation

        /// <summary>
        /// Generates synthetic nonstationary data from a normal distribution with linear trends on both mean and standard deviation.
        /// Both parameters increase linearly over time, representing a scenario where both the central tendency
        /// and variability of the process are changing (e.g., climate change effects on flood magnitudes).
        /// </summary>
        /// <param name="n">The sample size to simulate. Default = 1000</param>
        /// <param name="prngSeed">The pseudo random number generator seed. Default = 12345.</param>
        /// <returns>A tuple containing the DataFrame and the true parameters [Mu_Intercept, Mu_Slope, Sigma_Intercept, Sigma_Slope].</returns>
        public static (DataFrame DataFrame, double[] TrueParameters) GenerateBothLinearTrendData(int n = 1000, int prngSeed = 12345)
        {
            // Parameters: [Mu_Intercept, Mu_Slope, Sigma_Intercept, Sigma_Slope]
            var trueParameters = new double[] { 80.0, 0.4, 10.0, 0.03 };
            var model = new UnivariateDistribution();
            model.DistributionType = UnivariateDistributionType.Normal;
            model.IsNonstationary = true;
            model.SetTrendModel(0, TrendModelType.Linear); // Linear trend on mean
            model.SetTrendModel(1, TrendModelType.Linear); // Linear trend on sigma
            model.SetParameterValues(trueParameters);
            var values = GenerateTrendValues(model, n, prngSeed);
            var df = new DataFrame();
            df.ExactSeries = new ExactSeries(values);
            return (df, trueParameters);
        }

        /// <summary>
        /// Generates synthetic nonstationary data from a normal distribution with quadratic trend on mean and linear trend on standard deviation.
        /// This represents a scenario with accelerating change in central tendency and steady increase in variability.
        /// </summary>
        /// <param name="n">The sample size to simulate. Default = 1000</param>
        /// <param name="prngSeed">The pseudo random number generator seed. Default = 12345.</param>
        /// <returns>A tuple containing the DataFrame and the true parameters [Mu_Intercept, Mu_Linear, Mu_Quadratic, Sigma_Intercept, Sigma_Slope].</returns>
        public static (DataFrame DataFrame, double[] TrueParameters) GenerateMuQuadraticSigmaLinearTrendData(int n = 1000, int prngSeed = 12345)
        {
            // Parameters: [Mu_Intercept, Mu_Linear, Mu_Quadratic, Sigma_Intercept, Sigma_Slope]
            var trueParameters = new double[] { 100.0, -0.1, 0.005, 12.0, 0.02 };
            var model = new UnivariateDistribution();
            model.DistributionType = UnivariateDistributionType.Normal;
            model.IsNonstationary = true;
            model.SetTrendModel(0, TrendModelType.Quadratic); // Quadratic trend on mean
            model.SetTrendModel(1, TrendModelType.Linear);    // Linear trend on sigma
            model.SetParameterValues(trueParameters);
            var values = GenerateTrendValues(model, n, prngSeed);
            var df = new DataFrame();
            df.ExactSeries = new ExactSeries(values);
            return (df, trueParameters);
        }

        /// <summary>
        /// Generates synthetic nonstationary data from a normal distribution with linear trend on mean and exponential trend on standard deviation.
        /// This represents a scenario with steady change in central tendency and exponentially increasing variability.
        /// </summary>
        /// <param name="n">The sample size to simulate. Default = 1000</param>
        /// <param name="prngSeed">The pseudo random number generator seed. Default = 12345.</param>
        /// <returns>A tuple containing the DataFrame and the true parameters [Mu_Intercept, Mu_Slope, Sigma_A, Sigma_B].</returns>
        public static (DataFrame DataFrame, double[] TrueParameters) GenerateMuLinearSigmaExponentialTrendData(int n = 1000, int prngSeed = 12345)
        {
            // Parameters: [Mu_Intercept, Mu_Slope, Sigma_A (scale), Sigma_B (rate)]
            var trueParameters = new double[] { 90.0, 0.3, 8.0, 0.002 };
            var model = new UnivariateDistribution();
            model.DistributionType = UnivariateDistributionType.Normal;
            model.IsNonstationary = true;
            model.SetTrendModel(0, TrendModelType.Linear);      // Linear trend on mean
            model.SetTrendModel(1, TrendModelType.Exponential); // Exponential trend on sigma
            model.SetParameterValues(trueParameters);
            var values = GenerateTrendValues(model, n, prngSeed);
            var df = new DataFrame();
            df.ExactSeries = new ExactSeries(values);
            return (df, trueParameters);
        }

        /// <summary>
        /// Generates synthetic nonstationary data from a normal distribution with step function trends on both mean and standard deviation.
        /// This represents an abrupt shift in both the central tendency and variability, such as a regime change.
        /// </summary>
        /// <param name="n">The sample size to simulate. Default = 1000</param>
        /// <param name="prngSeed">The pseudo random number generator seed. Default = 12345.</param>
        /// <returns>A tuple containing the DataFrame and the true parameters [Mu_Before, Mu_After, Mu_ChangePoint, Sigma_Before, Sigma_After, Sigma_ChangePoint].</returns>
        public static (DataFrame DataFrame, double[] TrueParameters) GenerateBothStepFunctionTrendData(int n = 1000, int prngSeed = 12345)
        {
            // Parameters: [Mu_Before, Mu_After, Mu_ChangePoint, Sigma_Before, Sigma_After, Sigma_ChangePoint]
            var trueParameters = new double[] { 80.0, 120.0, 50.0, 10.0, 20.0, 50.0 };
            var model = new UnivariateDistribution();
            model.DistributionType = UnivariateDistributionType.Normal;
            model.IsNonstationary = true;
            model.SetTrendModel(0, TrendModelType.StepFunction); // Step function on mean
            model.SetTrendModel(1, TrendModelType.StepFunction); // Step function on sigma
            model.SetParameterValues(trueParameters);
            var values = GenerateTrendValues(model, n, prngSeed);
            var df = new DataFrame();
            df.ExactSeries = new ExactSeries(values);
            return (df, trueParameters);
        }

        #endregion

        #region Parent Distribution and Trend Coverage

        /// <summary>
        /// Generates Normal observations with constant mean and reciprocal standard deviation.
        /// </summary>
        /// <param name="n">The number of observations. Default = 1000.</param>
        /// <param name="prngSeed">The deterministic pseudo-random seed. Default = 12345.</param>
        /// <returns>The data frame and parents [mean, reciprocal sigma alpha, reciprocal sigma beta].</returns>
        public static (DataFrame DataFrame, double[] TrueParameters) GenerateReciprocalSigmaNormalData(int n = 1000, int prngSeed = 12345)
        {
            var trueParameters = new[] { 100d, 1d / 15d, (1d / 10d - 1d / 15d) / (n - 1d) };
            var model = new UnivariateDistribution
            {
                DistributionType = UnivariateDistributionType.Normal,
                IsNonstationary = true
            };
            model.SetTrendModel(1, TrendModelType.Reciprocal);
            model.SetParameterValues(trueParameters);
            double[] values = GenerateTrendValues(model, n, prngSeed);
            return (new DataFrame { ExactSeries = new ExactSeries(values) }, trueParameters);
        }

        /// <summary>
        /// Generates Normal observations with constant mean and sinusoidal standard deviation.
        /// </summary>
        /// <param name="n">The number of observations. Default = 1000.</param>
        /// <param name="prngSeed">The deterministic pseudo-random seed. Default = 12345.</param>
        /// <returns>The data frame and parents [mean, sigma level, amplitude, frequency, phase].</returns>
        public static (DataFrame DataFrame, double[] TrueParameters) GenerateSinusoidalSigmaNormalData(int n = 1000, int prngSeed = 12345)
        {
            var trueParameters = new[] { 100d, 15d, 3d, 0.01d, 0.3d };
            var model = new UnivariateDistribution
            {
                DistributionType = UnivariateDistributionType.Normal,
                IsNonstationary = true
            };
            model.SetTrendModel(1, TrendModelType.Sinusoidal);
            model.SetParameterValues(trueParameters);
            double[] values = GenerateTrendValues(model, n, prngSeed);
            return (new DataFrame { ExactSeries = new ExactSeries(values) }, trueParameters);
        }

        /// <summary>
        /// Generates generalized-extreme-value observations with a linear shape response.
        /// </summary>
        /// <param name="n">The number of observations. Default = 1000.</param>
        /// <param name="prngSeed">The deterministic pseudo-random seed. Default = 12345.</param>
        /// <returns>The data frame and parents [location, scale, shape intercept, shape slope].</returns>
        public static (DataFrame DataFrame, double[] TrueParameters) GenerateLinearShapeGevData(int n = 1000, int prngSeed = 12345)
        {
            var trueParameters = new[] { 50d, 15d, 0.05d, 0.0001d };
            var model = new UnivariateDistribution
            {
                DistributionType = UnivariateDistributionType.GeneralizedExtremeValue,
                IsNonstationary = true
            };
            model.SetTrendModel(2, TrendModelType.Linear);
            model.SetParameterValues(trueParameters);
            double[] values = GenerateTrendValues(model, n, prngSeed);
            return (new DataFrame { ExactSeries = new ExactSeries(values) }, trueParameters);
        }

        /// <summary>
        /// Generates generalized-Pareto observations with linear location and exponential scale responses.
        /// </summary>
        /// <param name="n">The number of observations. Default = 1000.</param>
        /// <param name="prngSeed">The deterministic pseudo-random seed. Default = 12345.</param>
        /// <returns>The data frame and parents [location intercept, location slope, scale level, scale rate, shape].</returns>
        public static (DataFrame DataFrame, double[] TrueParameters) GenerateLinearLocationExponentialScaleGpdData(int n = 1000, int prngSeed = 12345)
        {
            var trueParameters = new[] { 10d, 0.02d, 20d, 0.0005d, 0.1d };
            var model = new UnivariateDistribution
            {
                DistributionType = UnivariateDistributionType.GeneralizedPareto,
                IsNonstationary = true
            };
            model.SetTrendModel(0, TrendModelType.Linear);
            model.SetTrendModel(1, TrendModelType.Exponential);
            model.SetParameterValues(trueParameters);
            double[] values = GenerateTrendValues(model, n, prngSeed);
            return (new DataFrame { ExactSeries = new ExactSeries(values) }, trueParameters);
        }

        /// <summary>
        /// Generates Log-Pearson type III observations with linear log-mean and exponential log-scale responses.
        /// </summary>
        /// <param name="n">The number of observations. Default = 1000.</param>
        /// <param name="prngSeed">The deterministic pseudo-random seed. Default = 12345.</param>
        /// <returns>The data frame and parents [log-mean intercept, log-mean slope, log-scale level, log-scale rate, skew].</returns>
        public static (DataFrame DataFrame, double[] TrueParameters) GenerateLinearLogMeanExponentialLogScaleLp3Data(int n = 1000, int prngSeed = 12345)
        {
            var trueParameters = new[] { 3d, 0.0002d, 0.2d, 0.0002d, 0.2d };
            var model = new UnivariateDistribution
            {
                DistributionType = UnivariateDistributionType.LogPearsonTypeIII,
                IsNonstationary = true
            };
            model.SetTrendModel(0, TrendModelType.Linear);
            model.SetTrendModel(1, TrendModelType.Exponential);
            model.SetParameterValues(trueParameters);
            double[] values = GenerateTrendValues(model, n, prngSeed);
            return (new DataFrame { ExactSeries = new ExactSeries(values) }, trueParameters);
        }

        #endregion

    }
}
