using Numerics.Distributions;
using RMC.BestFit.Models;
using RMC.BestFit.Models.TrendFunctions.Support;

namespace RMC.BestFit.Verification.Datasets.UnivariateData
{
    /// <summary>
    /// A collection of methods to generate synthetic nonstationary univariate datasets for testing.
    /// </summary>
    public static class SyntheticNonstationaryData
    {

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
            model.Distribution.GenerateRandomValues(n, prngSeed);
            var values = model.Distribution.GenerateRandomValues(n, prngSeed);
            var df = new DataFrame();
            df.ExactSeries = new ExactSeries(values);
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
            model.Distribution.GenerateRandomValues(n, prngSeed);
            var values = model.Distribution.GenerateRandomValues(n, prngSeed);
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
            var trueParameters = new double[] { 50.0, 0.2, 15.0 };
            var model = new UnivariateDistribution();
            model.DistributionType = UnivariateDistributionType.Normal;
            model.IsNonstationary = true;
            model.SetTrendModel(0, TrendModelType.Exponential);
            model.SetParameterValues(trueParameters);
            model.Distribution.GenerateRandomValues(n, prngSeed);
            var values = model.Distribution.GenerateRandomValues(n, prngSeed);
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
            model.Distribution.GenerateRandomValues(n, prngSeed);
            var values = model.Distribution.GenerateRandomValues(n, prngSeed);
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
            var trueParameters = new double[] { 100.0, 0.05, 15.0 };
            var model = new UnivariateDistribution();
            model.DistributionType = UnivariateDistributionType.Normal;
            model.IsNonstationary = true;
            model.SetTrendModel(0, TrendModelType.Logistic);
            model.SetParameterValues(trueParameters);
            model.Distribution.GenerateRandomValues(n, prngSeed);
            var values = model.Distribution.GenerateRandomValues(n, prngSeed);
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
            model.Distribution.GenerateRandomValues(n, prngSeed);
            var values = model.Distribution.GenerateRandomValues(n, prngSeed);
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
            model.Distribution.GenerateRandomValues(n, prngSeed);
            var values = model.Distribution.GenerateRandomValues(n, prngSeed);
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
            model.Distribution.GenerateRandomValues(n, prngSeed);
            var values = model.Distribution.GenerateRandomValues(n, prngSeed);
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
            model.Distribution.GenerateRandomValues(n, prngSeed);
            var values = model.Distribution.GenerateRandomValues(n, prngSeed);
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
            var values = model.Distribution.GenerateRandomValues(n, prngSeed);
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
            var values = model.Distribution.GenerateRandomValues(n, prngSeed);
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
            var values = model.Distribution.GenerateRandomValues(n, prngSeed);
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
            var values = model.Distribution.GenerateRandomValues(n, prngSeed);
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
            var values = model.Distribution.GenerateRandomValues(n, prngSeed);
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
            var values = model.Distribution.GenerateRandomValues(n, prngSeed);
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
            var values = model.Distribution.GenerateRandomValues(n, prngSeed);
            var df = new DataFrame();
            df.ExactSeries = new ExactSeries(values);
            return (df, trueParameters);
        }

        #endregion

    }
}
