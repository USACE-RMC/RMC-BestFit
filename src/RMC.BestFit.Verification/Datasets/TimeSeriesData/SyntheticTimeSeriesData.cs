using Numerics.Data;
using RMC.BestFit.Models;
using System.Diagnostics;

namespace RMC.BestFit.Verification.Datasets.TimeSeriesData
{
    /// <summary>
    /// Synthetic time series data for unit testing.
    /// </summary>
    public static class SyntheticTimeSeriesData
    {

        #region Autoregressive (AR) Data

        /// <summary>
        /// Generates synthetic AR(1) time series data with known parameters for validation testing.
        /// </summary>
        /// <param name="mu">Process mean.</param>
        /// <param name="phi">Autoregressive coefficient.</param>
        /// <param name="sigma">Innovation standard deviation.</param>
        /// <param name="length">Number of observations to generate.</param>
        /// <param name="seed">Random seed for reproducibility.</param>
        /// <returns>A tuple containing the generated time series and the true parameter values [μ, φ, σ].</returns>
        public static (TimeSeries TimeSeries, double[] TrueParameters) GetAR1Data(double mu = 0.0, double phi = 0.5, double sigma = 2.0, int length = 300, int seed = 12345)
        {
            // Set true parameters
            var trueParameters = new double[] { mu, phi, sigma };

            // Create model
            var ar = new AutoRegressive
            {
                IncludeIntercept = true,
                Order = 1
            };
            ar.SetParameterValues(trueParameters);

            // Generate data with burn-in for stationarity
            int burnIn = ar.Order * 10 + 100;
            var fullData = ar.GenerateRandomValues(length + burnIn, seed);
            var data = fullData.Skip(burnIn).ToArray();
            var timeSeries = new TimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1), data);

            return (timeSeries, trueParameters);
        }

        /// <summary>
        /// Generates synthetic AR(2) time series data with known parameters for validation testing.
        /// </summary>
        /// <param name="mu">Process mean.</param>
        /// <param name="phi1">Autoregressive coefficient.</param>
        /// <param name="phi2">Autoregressive coefficient.</param>
        /// <param name="sigma">Innovation standard deviation.</param>
        /// <param name="length">Number of observations to generate.</param>
        /// <param name="seed">Random seed for reproducibility.</param>
        /// <returns>A tuple containing the generated time series and the true parameter values [μ, φ1, φ2, σ].</returns>
        public static (TimeSeries TimeSeries, double[] TrueParameters) GetAR2Data(double mu = 0.0, double phi1 = 0.5, double phi2 = -0.5, double sigma = 2.0, int length = 300, int seed = 12345)
        {
            // Set true parameters
            var trueParameters = new double[] { mu, phi1, phi2, sigma };

            // Create model
            var ar = new AutoRegressive
            {
                IncludeIntercept = true,
                Order = 2
            };
            ar.SetParameterValues(trueParameters);

            // Generate data with burn-in for stationarity
            int burnIn = ar.Order * 10 + 100;
            var fullData = ar.GenerateRandomValues(length + burnIn, seed);
            var data = fullData.Skip(burnIn).ToArray();
            var timeSeries = new TimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1), data);

            return (timeSeries, trueParameters);
        }

        /// <summary>
        /// Generates synthetic AR(3) time series data with known parameters for validation testing.
        /// </summary>
        /// <param name="mu">Process mean.</param>
        /// <param name="phi1">Autoregressive coefficient.</param>
        /// <param name="phi2">Autoregressive coefficient.</param>
        /// <param name="phi3">Autoregressive coefficient.</param>
        /// <param name="sigma">Innovation standard deviation.</param>
        /// <param name="length">Number of observations to generate.</param>
        /// <param name="seed">Random seed for reproducibility.</param>
        /// <returns>A tuple containing the generated time series and the true parameter values [μ, φ1, φ2, φ3, σ].</returns>
        public static (TimeSeries TimeSeries, double[] TrueParameters) GetAR3Data(double mu = 0.0, double phi1 = 0.5, double phi2 = -0.5, double phi3 = 0.5, double sigma = 2.0, int length = 300, int seed = 12345)
        {
            // Set true parameters
            var trueParameters = new double[] { mu, phi1, phi2, phi3, sigma };

            // Create model
            var ar = new AutoRegressive
            {
                IncludeIntercept = true,
                Order = 3
            };
            ar.SetParameterValues(trueParameters);

            // Generate data with burn-in for stationarity
            int burnIn = ar.Order * 10 + 100;
            var fullData = ar.GenerateRandomValues(length + burnIn, seed);
            var data = fullData.Skip(burnIn).ToArray();
            var timeSeries = new TimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1), data);

            return (timeSeries, trueParameters);
        }

        #endregion

        #region Moving Average (MA) Data

        /// <summary>
        /// Generates synthetic MA(1) time series data with known parameters for validation testing.
        /// </summary>
        /// <param name="mu">Process mean (intercept).</param>
        /// <param name="theta">Moving average coefficient.</param>
        /// <param name="sigma">Innovation standard deviation.</param>
        /// <param name="length">Number of observations to generate.</param>
        /// <param name="seed">Random seed for reproducibility.</param>
        /// <returns>A tuple containing the generated time series and the true parameter values [μ, θ, σ].</returns>
        public static (TimeSeries TimeSeries, double[] TrueParameters) GetMA1Data(double mu = 0.0, double theta = 0.5, double sigma = 2.0, int length = 300, int seed = 12345)
        {
            // Set true parameters
            var trueParameters = new double[] { mu, theta, sigma };

            // Create model
            var ma = new MovingAverage
            {
                IncludeIntercept = true,
                Order = 1
            };
            ma.SetParameterValues(trueParameters);

            // Generate data with burn-in for stationarity
            int burnIn = ma.Order * 10 + 100;
            var fullData = ma.GenerateRandomValues(length + burnIn, seed);
            var data = fullData.Skip(burnIn).ToArray();
            var timeSeries = new TimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1), data);

            return (timeSeries, trueParameters);
        }

        /// <summary>
        /// Generates synthetic MA(2) time series data with known parameters for validation testing.
        /// </summary>
        /// <param name="mu">Process mean (intercept).</param>
        /// <param name="theta1">First moving average coefficient.</param>
        /// <param name="theta2">Second moving average coefficient.</param>
        /// <param name="sigma">Innovation standard deviation.</param>
        /// <param name="length">Number of observations to generate.</param>
        /// <param name="seed">Random seed for reproducibility.</param>
        /// <returns>A tuple containing the generated time series and the true parameter values [μ, θ1, θ2, σ].</returns>
        public static (TimeSeries TimeSeries, double[] TrueParameters) GetMA2Data(double mu = 0.0, double theta1 = 0.5, double theta2 = -0.3, double sigma = 2.0, int length = 300, int seed = 12345)
        {
            // Set true parameters
            var trueParameters = new double[] { mu, theta1, theta2, sigma };

            // Create model
            var ma = new MovingAverage
            {
                IncludeIntercept = true,
                Order = 2
            };
            ma.SetParameterValues(trueParameters);

            // Generate data with burn-in for stationarity
            int burnIn = ma.Order * 10 + 100;
            var fullData = ma.GenerateRandomValues(length + burnIn, seed);
            var data = fullData.Skip(burnIn).ToArray();
            var timeSeries = new TimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1), data);

            return (timeSeries, trueParameters);
        }

        /// <summary>
        /// Generates synthetic MA(3) time series data with known parameters for validation testing.
        /// </summary>
        /// <param name="mu">Process mean (intercept).</param>
        /// <param name="theta1">First moving average coefficient.</param>
        /// <param name="theta2">Second moving average coefficient.</param>
        /// <param name="theta3">Third moving average coefficient.</param>
        /// <param name="sigma">Innovation standard deviation.</param>
        /// <param name="length">Number of observations to generate.</param>
        /// <param name="seed">Random seed for reproducibility.</param>
        /// <returns>A tuple containing the generated time series and the true parameter values [μ, θ1, θ2, θ3, σ].</returns>
        public static (TimeSeries TimeSeries, double[] TrueParameters) GetMA3Data(double mu = 0.0, double theta1 = 0.5, double theta2 = -0.3, double theta3 = 0.2, double sigma = 2.0, int length = 300, int seed = 12345)
        {
            // Set true parameters
            var trueParameters = new double[] { mu, theta1, theta2, theta3, sigma };

            // Create model
            var ma = new MovingAverage
            {
                IncludeIntercept = true,
                Order = 3
            };
            ma.SetParameterValues(trueParameters);

            // Generate data with burn-in for stationarity
            int burnIn = ma.Order * 10 + 100;
            var fullData = ma.GenerateRandomValues(length + burnIn, seed);
            var data = fullData.Skip(burnIn).ToArray();
            var timeSeries = new TimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1), data);

            return (timeSeries, trueParameters);
        }

        #endregion

        #region ARIMA Data

        /// <summary>
        /// Generates synthetic ARIMA(1,1) time series data with known parameters for validation testing.
        /// </summary>
        /// <param name="mu">Process mean (intercept).</param>
        /// <param name="phi">Autoregressive coefficient.</param>
        /// <param name="theta">Moving average coefficient.</param>
        /// <param name="sigma">Innovation standard deviation.</param>
        /// <param name="length">Number of observations to generate.</param>
        /// <param name="seed">Random seed for reproducibility.</param>
        /// <returns>A tuple containing the generated time series and the true parameter values [μ, φ, θ, σ].</returns>
        public static (TimeSeries TimeSeries, double[] TrueParameters) GetARIMA11Data(double mu = 0.0, double phi = 0.5, double theta = 0.3, double sigma = 2.0, int length = 300, int seed = 12345)
        {
            // Set true parameters
            var trueParameters = new double[] { mu, phi, theta, sigma };

            // Create model
            var arma = new ARIMA
            {
                IncludeIntercept = true,
                POrder = 1,
                QOrder = 1
            };
            arma.SetParameterValues(trueParameters);

            // Generate data with burn-in for stationarity
            int burnIn = Math.Max(arma.POrder, arma.QOrder) * 10 + 100;
            var fullData = arma.GenerateRandomValues(length + burnIn, seed);
            var data = fullData.Skip(burnIn).ToArray();
            var timeSeries = new TimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1), data);

            return (timeSeries, trueParameters);
        }

        /// <summary>
        /// Generates synthetic ARIMA(2,1) time series data with known parameters for validation testing.
        /// </summary>
        /// <param name="mu">Process mean (intercept).</param>
        /// <param name="phi1">First autoregressive coefficient.</param>
        /// <param name="phi2">Second autoregressive coefficient.</param>
        /// <param name="theta">Moving average coefficient.</param>
        /// <param name="sigma">Innovation standard deviation.</param>
        /// <param name="length">Number of observations to generate.</param>
        /// <param name="seed">Random seed for reproducibility.</param>
        /// <returns>A tuple containing the generated time series and the true parameter values [μ, φ1, φ2, θ, σ].</returns>
        public static (TimeSeries TimeSeries, double[] TrueParameters) GetARIMA21Data(double mu = 0.0, double phi1 = 0.5, double phi2 = -0.3, double theta = 0.3, double sigma = 2.0, int length = 300, int seed = 12345)
        {
            // Set true parameters
            var trueParameters = new double[] { mu, phi1, phi2, theta, sigma };

            // Create model
            var arma = new ARIMA
            {
                IncludeIntercept = true,
                POrder = 2,
                QOrder = 1
            };
            arma.SetParameterValues(trueParameters);

            // Generate data with burn-in for stationarity
            int burnIn = Math.Max(arma.POrder, arma.QOrder) * 10 + 100;
            var fullData = arma.GenerateRandomValues(length + burnIn, seed);
            var data = fullData.Skip(burnIn).ToArray();
            var timeSeries = new TimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1), data);

            return (timeSeries, trueParameters);
        }

        /// <summary>
        /// Generates synthetic ARIMA(1,2) time series data with known parameters for validation testing.
        /// </summary>
        /// <param name="mu">Process mean (intercept).</param>
        /// <param name="phi">Autoregressive coefficient.</param>
        /// <param name="theta1">First moving average coefficient.</param>
        /// <param name="theta2">Second moving average coefficient.</param>
        /// <param name="sigma">Innovation standard deviation.</param>
        /// <param name="length">Number of observations to generate.</param>
        /// <param name="seed">Random seed for reproducibility.</param>
        /// <returns>A tuple containing the generated time series and the true parameter values [μ, φ, θ1, θ2, σ].</returns>
        public static (TimeSeries TimeSeries, double[] TrueParameters) GetARIMA12Data(double mu = 0.0, double phi = 0.5, double theta1 = 0.3, double theta2 = -0.2, double sigma = 2.0, int length = 300, int seed = 12345)
        {
            // Set true parameters
            var trueParameters = new double[] { mu, phi, theta1, theta2, sigma };

            // Create model
            var arma = new ARIMA
            {
                IncludeIntercept = true,
                POrder = 1,
                QOrder = 2
            };
            arma.SetParameterValues(trueParameters);

            // Generate data with burn-in for stationarity
            int burnIn = Math.Max(arma.POrder, arma.QOrder) * 10 + 100;
            var fullData = arma.GenerateRandomValues(length + burnIn, seed);
            var data = fullData.Skip(burnIn).ToArray();
            var timeSeries = new TimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1), data);

            return (timeSeries, trueParameters);
        }

        /// <summary>
        /// Generates synthetic ARIMA(2,2) time series data with known parameters for validation testing.
        /// </summary>
        /// <param name="mu">Process mean (intercept).</param>
        /// <param name="phi1">First autoregressive coefficient.</param>
        /// <param name="phi2">Second autoregressive coefficient.</param>
        /// <param name="theta1">First moving average coefficient.</param>
        /// <param name="theta2">Second moving average coefficient.</param>
        /// <param name="sigma">Innovation standard deviation.</param>
        /// <param name="length">Number of observations to generate.</param>
        /// <param name="seed">Random seed for reproducibility.</param>
        /// <returns>A tuple containing the generated time series and the true parameter values [μ, φ1, φ2, θ1, θ2, σ].</returns>
        public static (TimeSeries TimeSeries, double[] TrueParameters) GetARIMA22Data(double mu = 0.0, double phi1 = 0.5, double phi2 = -0.3, double theta1 = 0.3, double theta2 = -0.2, double sigma = 2.0, int length = 300, int seed = 12345)
        {
            // Set true parameters
            var trueParameters = new double[] { mu, phi1, phi2, theta1, theta2, sigma };

            // Create model
            var arma = new ARIMA
            {
                IncludeIntercept = true,
                POrder = 2,
                QOrder = 2
            };
            arma.SetParameterValues(trueParameters);

            // Generate data with burn-in for stationarity
            int burnIn = Math.Max(arma.POrder, arma.QOrder) * 10 + 100;
            var fullData = arma.GenerateRandomValues(length + burnIn, seed);
            var data = fullData.Skip(burnIn).ToArray();
            var timeSeries = new TimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1), data);

            return (timeSeries, trueParameters);
        }

        /// <summary>
        /// Generates synthetic pure AR(1) data using ARIMA model (p=1, q=0) for testing ARIMA's ability to fit AR data.
        /// </summary>
        /// <param name="mu">Process mean (intercept).</param>
        /// <param name="phi">Autoregressive coefficient.</param>
        /// <param name="sigma">Innovation standard deviation.</param>
        /// <param name="length">Number of observations to generate.</param>
        /// <param name="seed">Random seed for reproducibility.</param>
        /// <returns>A tuple containing the generated time series and the true parameter values [μ, φ, σ].</returns>
        public static (TimeSeries TimeSeries, double[] TrueParameters) GetARIMA_AR1Data(double mu = 0.0, double phi = 0.6, double sigma = 2.0, int length = 300, int seed = 12345)
        {
            // Set true parameters for ARIMA(1,0) which is equivalent to AR(1)
            var trueParameters = new double[] { mu, phi, sigma };

            // Create model
            var arma = new ARIMA
            {
                IncludeIntercept = true,
                POrder = 1,
                QOrder = 0
            };
            arma.SetParameterValues(trueParameters);

            // Generate data with burn-in for stationarity
            int burnIn = Math.Max(arma.POrder, arma.QOrder) * 10 + 100;
            var fullData = arma.GenerateRandomValues(length + burnIn, seed);
            var data = fullData.Skip(burnIn).ToArray();
            var timeSeries = new TimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1), data);

            return (timeSeries, trueParameters);
        }

        /// <summary>
        /// Generates synthetic pure MA(1) data using ARIMA model (p=0, q=1) for testing ARIMA's ability to fit MA data.
        /// </summary>
        /// <param name="mu">Process mean (intercept).</param>
        /// <param name="theta">Moving average coefficient.</param>
        /// <param name="sigma">Innovation standard deviation.</param>
        /// <param name="length">Number of observations to generate.</param>
        /// <param name="seed">Random seed for reproducibility.</param>
        /// <returns>A tuple containing the generated time series and the true parameter values [μ, θ, σ].</returns>
        public static (TimeSeries TimeSeries, double[] TrueParameters) GetARIMA_MA1Data(double mu = 0.0, double theta = 0.5, double sigma = 2.0, int length = 300, int seed = 12345)
        {
            // Set true parameters for ARIMA(0,1) which is equivalent to MA(1)
            var trueParameters = new double[] { mu, theta, sigma };

            // Create model
            var arma = new ARIMA
            {
                IncludeIntercept = true,
                POrder = 0,
                QOrder = 1
            };
            arma.SetParameterValues(trueParameters);

            // Generate data with burn-in for stationarity
            int burnIn = Math.Max(arma.POrder, arma.QOrder) * 10 + 100;
            var fullData = arma.GenerateRandomValues(length + burnIn, seed);
            var data = fullData.Skip(burnIn).ToArray();
            var timeSeries = new TimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1), data);

            return (timeSeries, trueParameters);
        }

        #endregion

        #region ARIMAX Data

        /// <summary>
        /// Generates synthetic ARIMAX(1,1) time series data with known parameters for validation testing.
        /// ARIMAX is configured with no transforms, differencing, seasonality, or covariates.
        /// </summary>
        /// <param name="mu">Process mean (intercept).</param>
        /// <param name="phi">Autoregressive coefficient.</param>
        /// <param name="theta">Moving average coefficient.</param>
        /// <param name="sigma">Innovation standard deviation.</param>
        /// <param name="length">Number of observations to generate.</param>
        /// <param name="seed">Random seed for reproducibility.</param>
        /// <returns>A tuple containing the generated time series and the true parameter values [μ, φ, θ, σ].</returns>
        public static (TimeSeries TimeSeries, double[] TrueParameters) GetARIMAX11Data(double mu = 0.0, double phi = 0.5, double theta = 0.3, double sigma = 2.0, int length = 300, int seed = 12345)
        {
            // Set true parameters
            var trueParameters = new double[] { mu, phi, theta, sigma };

            // Create model using default constructor (avoids zero-data parameter bound issues)
            var armax = new ARIMAX
            {
                IncludeIntercept = true,
                AROrderP = 1,
                MAOrderQ = 1
            };
            armax.SetParameterValues(trueParameters);

            // Generate data with burn-in for stationarity
            int burnIn = Math.Max(armax.AROrderP, armax.MAOrderQ) * 10 + 100;
            var fullData = armax.GenerateRandomValues(length + burnIn, seed);
            var data = fullData.Skip(burnIn).ToArray();
            var timeSeries = new TimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1), data);

            return (timeSeries, trueParameters);
        }

        /// <summary>
        /// Generates synthetic ARIMAX(2,1) time series data with known parameters for validation testing.
        /// </summary>
        /// <param name="mu">Process mean (intercept).</param>
        /// <param name="phi1">First autoregressive coefficient.</param>
        /// <param name="phi2">Second autoregressive coefficient.</param>
        /// <param name="theta">Moving average coefficient.</param>
        /// <param name="sigma">Innovation standard deviation.</param>
        /// <param name="length">Number of observations to generate.</param>
        /// <param name="seed">Random seed for reproducibility.</param>
        /// <returns>A tuple containing the generated time series and the true parameter values [μ, φ1, φ2, θ, σ].</returns>
        public static (TimeSeries TimeSeries, double[] TrueParameters) GetARIMAX21Data(double mu = 0.0, double phi1 = 0.5, double phi2 = -0.3, double theta = 0.3, double sigma = 2.0, int length = 300, int seed = 12345)
        {
            // Set true parameters
            var trueParameters = new double[] { mu, phi1, phi2, theta, sigma };

            // Create model using default constructor (avoids zero-data parameter bound issues)
            var armax = new ARIMAX
            {
                IncludeIntercept = true,
                AROrderP = 2,
                MAOrderQ = 1
            };
            armax.SetParameterValues(trueParameters);

            // Generate data with burn-in for stationarity
            int burnIn = Math.Max(armax.AROrderP, armax.MAOrderQ) * 10 + 100;
            var fullData = armax.GenerateRandomValues(length + burnIn, seed);
            var data = fullData.Skip(burnIn).ToArray();
            var timeSeries = new TimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1), data);

            return (timeSeries, trueParameters);
        }

        /// <summary>
        /// Generates synthetic ARIMAX(1,2) time series data with known parameters for validation testing.
        /// </summary>
        /// <param name="mu">Process mean (intercept).</param>
        /// <param name="phi">Autoregressive coefficient.</param>
        /// <param name="theta1">First moving average coefficient.</param>
        /// <param name="theta2">Second moving average coefficient.</param>
        /// <param name="sigma">Innovation standard deviation.</param>
        /// <param name="length">Number of observations to generate.</param>
        /// <param name="seed">Random seed for reproducibility.</param>
        /// <returns>A tuple containing the generated time series and the true parameter values [μ, φ, θ1, θ2, σ].</returns>
        public static (TimeSeries TimeSeries, double[] TrueParameters) GetARIMAX12Data(double mu = 0.0, double phi = 0.5, double theta1 = 0.3, double theta2 = -0.2, double sigma = 2.0, int length = 300, int seed = 12345)
        {
            // Set true parameters
            var trueParameters = new double[] { mu, phi, theta1, theta2, sigma };

            // Create model using default constructor (avoids zero-data parameter bound issues)
            var armax = new ARIMAX
            {
                IncludeIntercept = true,
                AROrderP = 1,
                MAOrderQ = 2
            };
            armax.SetParameterValues(trueParameters);

            // Generate data with burn-in for stationarity
            int burnIn = Math.Max(armax.AROrderP, armax.MAOrderQ) * 10 + 100;
            var fullData = armax.GenerateRandomValues(length + burnIn, seed);
            var data = fullData.Skip(burnIn).ToArray();
            var timeSeries = new TimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1), data);

            return (timeSeries, trueParameters);
        }

        /// <summary>
        /// Generates synthetic ARIMAX(2,2) time series data with known parameters for validation testing.
        /// </summary>
        /// <param name="mu">Process mean (intercept).</param>
        /// <param name="phi1">First autoregressive coefficient.</param>
        /// <param name="phi2">Second autoregressive coefficient.</param>
        /// <param name="theta1">First moving average coefficient.</param>
        /// <param name="theta2">Second moving average coefficient.</param>
        /// <param name="sigma">Innovation standard deviation.</param>
        /// <param name="length">Number of observations to generate.</param>
        /// <param name="seed">Random seed for reproducibility.</param>
        /// <returns>A tuple containing the generated time series and the true parameter values [μ, φ1, φ2, θ1, θ2, σ].</returns>
        public static (TimeSeries TimeSeries, double[] TrueParameters) GetARIMAX22Data(double mu = 0.0, double phi1 = 0.5, double phi2 = -0.3, double theta1 = 0.3, double theta2 = -0.2, double sigma = 2.0, int length = 300, int seed = 12345)
        {
            // Set true parameters
            var trueParameters = new double[] { mu, phi1, phi2, theta1, theta2, sigma };

            // Create model using default constructor (avoids zero-data parameter bound issues)
            var armax = new ARIMAX
            {
                IncludeIntercept = true,
                AROrderP = 2,
                MAOrderQ = 2
            };
            armax.SetParameterValues(trueParameters);

            // Generate data with burn-in for stationarity
            int burnIn = Math.Max(armax.AROrderP, armax.MAOrderQ) * 10 + 100;
            var fullData = armax.GenerateRandomValues(length + burnIn, seed);
            var data = fullData.Skip(burnIn).ToArray();
            var timeSeries = new TimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1), data);

            return (timeSeries, trueParameters);
        }

        /// <summary>
        /// Generates synthetic pure AR(1) data using ARIMAX model (p=1, q=0) for testing ARIMAX's ability to fit AR data.
        /// </summary>
        /// <param name="mu">Process mean (intercept).</param>
        /// <param name="phi">Autoregressive coefficient.</param>
        /// <param name="sigma">Innovation standard deviation.</param>
        /// <param name="length">Number of observations to generate.</param>
        /// <param name="seed">Random seed for reproducibility.</param>
        /// <returns>A tuple containing the generated time series and the true parameter values [μ, φ, σ].</returns>
        public static (TimeSeries TimeSeries, double[] TrueParameters) GetARIMAX_AR1Data(double mu = 0.0, double phi = 0.6, double sigma = 2.0, int length = 300, int seed = 12345)
        {
            // Set true parameters for ARIMAX(1,0) which is equivalent to AR(1)
            var trueParameters = new double[] { mu, phi, sigma };

            // Create model using default constructor (avoids zero-data parameter bound issues)
            var armax = new ARIMAX
            {
                IncludeIntercept = true,
                AROrderP = 1,
                MAOrderQ = 0
            };
            armax.SetParameterValues(trueParameters);

            // Generate data with burn-in for stationarity
            int burnIn = Math.Max(armax.AROrderP, armax.MAOrderQ) * 10 + 100;
            var fullData = armax.GenerateRandomValues(length + burnIn, seed);
            var data = fullData.Skip(burnIn).ToArray();
            var timeSeries = new TimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1), data);

            return (timeSeries, trueParameters);
        }

        /// <summary>
        /// Generates synthetic pure MA(1) data using ARIMAX model (p=0, q=1) for testing ARIMAX's ability to fit MA data.
        /// </summary>
        /// <param name="mu">Process mean (intercept).</param>
        /// <param name="theta">Moving average coefficient.</param>
        /// <param name="sigma">Innovation standard deviation.</param>
        /// <param name="length">Number of observations to generate.</param>
        /// <param name="seed">Random seed for reproducibility.</param>
        /// <returns>A tuple containing the generated time series and the true parameter values [μ, θ, σ].</returns>
        public static (TimeSeries TimeSeries, double[] TrueParameters) GetARIMAX_MA1Data(double mu = 0.0, double theta = 0.5, double sigma = 2.0, int length = 300, int seed = 12345)
        {
            // Set true parameters for ARIMAX(0,1) which is equivalent to MA(1)
            var trueParameters = new double[] { mu, theta, sigma };

            // Create model using default constructor (avoids zero-data parameter bound issues)
            var armax = new ARIMAX
            {
                IncludeIntercept = true,
                AROrderP = 0,
                MAOrderQ = 1
            };
            armax.SetParameterValues(trueParameters);

            // Generate data with burn-in for stationarity
            int burnIn = Math.Max(armax.AROrderP, armax.MAOrderQ) * 10 + 100;
            var fullData = armax.GenerateRandomValues(length + burnIn, seed);
            var data = fullData.Skip(burnIn).ToArray();
            var timeSeries = new TimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1), data);

            return (timeSeries, trueParameters);
        }

        #endregion

        #region ARIMA Data (Differencing)

        /// <summary>
        /// Generates synthetic ARIMA(1,1,0) time series data with known parameters for validation testing.
        /// This is an AR(1) model with first-order differencing to handle nonstationary data.
        /// </summary>
        /// <param name="mu">Process mean (intercept of the differenced series).</param>
        /// <param name="phi">Autoregressive coefficient.</param>
        /// <param name="sigma">Innovation standard deviation.</param>
        /// <param name="length">Number of observations to generate.</param>
        /// <param name="seed">Random seed for reproducibility.</param>
        /// <returns>A tuple containing the generated time series and the true parameter values [μ, φ, σ].</returns>
        public static (TimeSeries TimeSeries, double[] TrueParameters) GetARIMA110Data(double mu = 0.5, double phi = 0.6, double sigma = 2.0, int length = 500, int seed = 12345)
        {
            // Set true parameters - intercept, AR(1), sigma
            var trueParameters = new double[] { mu, phi, sigma };

            // Create model with differencing
            var armax = new ARIMAX
            {
                IncludeIntercept = true,
                AROrderP = 1,
                DiffOrderD = 1,
                MAOrderQ = 0
            };
            armax.SetParameterValues(trueParameters);

            // Create time series data
            var data = armax.GenerateRandomValues(length, seed);
            var timeSeries = new TimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1), data);

            return (timeSeries, trueParameters);
        }

        /// <summary>
        /// Generates synthetic ARIMA(0,1,1) time series data with known parameters for validation testing.
        /// This is an MA(1) model with first-order differencing (IMA model).
        /// </summary>
        /// <param name="mu">Process mean (intercept of the differenced series).</param>
        /// <param name="theta">Moving average coefficient.</param>
        /// <param name="sigma">Innovation standard deviation.</param>
        /// <param name="length">Number of observations to generate.</param>
        /// <param name="seed">Random seed for reproducibility.</param>
        /// <returns>A tuple containing the generated time series and the true parameter values [μ, θ, σ].</returns>
        public static (TimeSeries TimeSeries, double[] TrueParameters) GetARIMA011Data(double mu = 0.3, double theta = 0.5, double sigma = 2.0, int length = 500, int seed = 12345)
        {
            // Set true parameters - intercept, MA(1), sigma
            var trueParameters = new double[] { mu, theta, sigma };

            // Create model with differencing
            var armax = new ARIMAX
            {
                IncludeIntercept = true,
                AROrderP = 0,
                DiffOrderD = 1,
                MAOrderQ = 1
            };
            armax.SetParameterValues(trueParameters);

            // Create time series data
            var data = armax.GenerateRandomValues(length, seed);
            var timeSeries = new TimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1), data);

            return (timeSeries, trueParameters);
        }

        /// <summary>
        /// Generates synthetic ARIMA(1,1,1) time series data with known parameters for validation testing.
        /// This is the classic Box-Jenkins ARIMA model with AR(1), first-order differencing, and MA(1).
        /// </summary>
        /// <param name="mu">Process mean (intercept of the differenced series).</param>
        /// <param name="phi">Autoregressive coefficient.</param>
        /// <param name="theta">Moving average coefficient.</param>
        /// <param name="sigma">Innovation standard deviation.</param>
        /// <param name="length">Number of observations to generate.</param>
        /// <param name="seed">Random seed for reproducibility.</param>
        /// <returns>A tuple containing the generated time series and the true parameter values [μ, φ, θ, σ].</returns>
        public static (TimeSeries TimeSeries, double[] TrueParameters) GetARIMA111Data(double mu = 0.3, double phi = 0.6, double theta = 0.4, double sigma = 2.0, int length = 500, int seed = 12345)
        {
            // Set true parameters - intercept, AR(1), MA(1), sigma
            var trueParameters = new double[] { mu, phi, theta, sigma };

            // Create model with differencing
            var armax = new ARIMAX
            {
                IncludeIntercept = true,
                AROrderP = 1,
                DiffOrderD = 1,
                MAOrderQ = 1
            };
            armax.SetParameterValues(trueParameters);

            // Create time series data
            var data = armax.GenerateRandomValues(length, seed);
            var timeSeries = new TimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1), data);

            return (timeSeries, trueParameters);
        }

        /// <summary>
        /// Generates synthetic ARIMA(1,2,0) time series data with known parameters for validation testing.
        /// This is an AR(1) model with second-order differencing for data with quadratic trends.
        /// </summary>
        /// <param name="mu">Process mean (intercept of the twice-differenced series).</param>
        /// <param name="phi">Autoregressive coefficient.</param>
        /// <param name="sigma">Innovation standard deviation.</param>
        /// <param name="length">Number of observations to generate.</param>
        /// <param name="seed">Random seed for reproducibility.</param>
        /// <returns>A tuple containing the generated time series and the true parameter values [μ, φ, σ].</returns>
        public static (TimeSeries TimeSeries, double[] TrueParameters) GetARIMA120Data(double mu = 0.1, double phi = 0.5, double sigma = 2.0, int length = 500, int seed = 12345)
        {
            // Set true parameters - intercept, AR(1), sigma
            var trueParameters = new double[] { mu, phi, sigma };

            // Create model with second-order differencing
            var armax = new ARIMAX
            {
                IncludeIntercept = true,
                AROrderP = 1,
                DiffOrderD = 2,
                MAOrderQ = 0
            };
            armax.SetParameterValues(trueParameters);

            // Create time series data
            var data = armax.GenerateRandomValues(length, seed);
            var timeSeries = new TimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1), data);

            return (timeSeries, trueParameters);
        }

        /// <summary>
        /// Generates synthetic ARIMA(2,1,1) time series data with known parameters for validation testing.
        /// Higher-order ARIMA with AR(2), first-order differencing, and MA(1).
        /// </summary>
        /// <param name="mu">Process mean (intercept of the differenced series).</param>
        /// <param name="phi1">First autoregressive coefficient.</param>
        /// <param name="phi2">Second autoregressive coefficient.</param>
        /// <param name="theta">Moving average coefficient.</param>
        /// <param name="sigma">Innovation standard deviation.</param>
        /// <param name="length">Number of observations to generate.</param>
        /// <param name="seed">Random seed for reproducibility.</param>
        /// <returns>A tuple containing the generated time series and the true parameter values [μ, φ1, φ2, θ, σ].</returns>
        public static (TimeSeries TimeSeries, double[] TrueParameters) GetARIMA211Data(double mu = 0.2, double phi1 = 0.5, double phi2 = -0.25, double theta = 0.3, double sigma = 2.0, int length = 500, int seed = 12345)
        {
            // Set true parameters - intercept, AR(1), AR(2), MA(1), sigma
            var trueParameters = new double[] { mu, phi1, phi2, theta, sigma };

            // Create model with differencing
            var armax = new ARIMAX
            {
                IncludeIntercept = true,
                AROrderP = 2,
                DiffOrderD = 1,
                MAOrderQ = 1
            };
            armax.SetParameterValues(trueParameters);

            // Create time series data
            var data = armax.GenerateRandomValues(length, seed);
            var timeSeries = new TimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1), data);

            return (timeSeries, trueParameters);
        }

        #endregion

        #region Trend Model Data

        #region Standalone Trend Models (No AR/MA Components)

        /// <summary>
        /// Generates synthetic time series data with a linear trend only (no AR/MA components).
        /// Y(t) = μ + γ*t + ε(t) where ε(t) ~ N(0, σ²).
        /// Parameters: [μ, γ, σ].
        /// </summary>
        /// <param name="mu">Process intercept.</param>
        /// <param name="gamma">Linear trend slope coefficient.</param>
        /// <param name="sigma">Innovation standard deviation.</param>
        /// <param name="length">Number of observations to generate.</param>
        /// <param name="seed">Random seed for reproducibility.</param>
        /// <returns>A tuple containing the generated time series and the true parameter values [μ, γ, σ].</returns>
        public static (TimeSeries TimeSeries, double[] TrueParameters) GetLinearTrendData(double mu = 100.0, double gamma = 0.5, double sigma = 5.0, int length = 500, int seed = 12345)
        {
            var trueParameters = new double[] { mu, gamma, sigma };

            var armax = new ARIMAX
            {
                IncludeIntercept = true,
                TrendType = ARIMAX.Trend.Linear,
                AROrderP = 0,
                MAOrderQ = 0
            };
            armax.SetParameterValues(trueParameters);

            var data = armax.GenerateRandomValues(length, seed);
            var timeSeries = new TimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1), data);

            return (timeSeries, trueParameters);
        }

        /// <summary>
        /// Generates synthetic time series data with a quadratic trend only (no AR/MA components).
        /// Y(t) = μ + γ1*t + γ2*t² + ε(t) where ε(t) ~ N(0, σ²).
        /// Parameters: [μ, γ1, γ2, σ].
        /// </summary>
        /// <param name="mu">Process intercept.</param>
        /// <param name="gamma1">Linear trend coefficient.</param>
        /// <param name="gamma2">Quadratic trend coefficient.</param>
        /// <param name="sigma">Innovation standard deviation.</param>
        /// <param name="length">Number of observations to generate.</param>
        /// <param name="seed">Random seed for reproducibility.</param>
        /// <returns>A tuple containing the generated time series and the true parameter values [μ, γ1, γ2, σ].</returns>
        public static (TimeSeries TimeSeries, double[] TrueParameters) GetQuadraticTrendData(double mu = 100.0, double gamma1 = 0.5, double gamma2 = 0.01, double sigma = 5.0, int length = 500, int seed = 12345)
        {
            var trueParameters = new double[] { mu, gamma1, gamma2, sigma };

            var armax = new ARIMAX
            {
                IncludeIntercept = true,
                TrendType = ARIMAX.Trend.Quadratic,
                AROrderP = 0,
                MAOrderQ = 0
            };
            armax.SetParameterValues(trueParameters);

            var data = armax.GenerateRandomValues(length, seed);
            var timeSeries = new TimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1), data);

            return (timeSeries, trueParameters);
        }

        /// <summary>
        /// Generates synthetic time series data with a cubic trend only (no AR/MA components).
        /// Y(t) = μ + γ1*t + γ2*t² + γ3*t³ + ε(t) where ε(t) ~ N(0, σ²).
        /// Parameters: [μ, γ1, γ2, γ3, σ].
        /// </summary>
        /// <param name="mu">Process intercept.</param>
        /// <param name="gamma1">Linear trend coefficient.</param>
        /// <param name="gamma2">Quadratic trend coefficient.</param>
        /// <param name="gamma3">Cubic trend coefficient.</param>
        /// <param name="sigma">Innovation standard deviation.</param>
        /// <param name="length">Number of observations to generate.</param>
        /// <param name="seed">Random seed for reproducibility.</param>
        /// <returns>A tuple containing the generated time series and the true parameter values [μ, γ1, γ2, γ3, σ].</returns>
        public static (TimeSeries TimeSeries, double[] TrueParameters) GetCubicTrendData(double mu = 100.0, double gamma1 = 0.3, double gamma2 = 0.005, double gamma3 = 0.00001, double sigma = 5.0, int length = 500, int seed = 12345)
        {
            var trueParameters = new double[] { mu, gamma1, gamma2, gamma3, sigma };

            var armax = new ARIMAX
            {
                IncludeIntercept = true,
                TrendType = ARIMAX.Trend.Cubic,
                AROrderP = 0,
                MAOrderQ = 0
            };
            armax.SetParameterValues(trueParameters);

            var data = armax.GenerateRandomValues(length, seed);
            var timeSeries = new TimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1), data);

            return (timeSeries, trueParameters);
        }

        #endregion

        #region Trend Models with AR/MA Components

        /// <summary>
        /// Generates synthetic AR(1) time series data with a linear trend for validation testing.
        /// Uses ARIMAX.GenerateRandomValues for consistent data generation.
        /// Parameters: [μ, γ, φ, σ].
        /// </summary>
        /// <param name="mu">Process intercept.</param>
        /// <param name="gamma">Linear trend slope coefficient.</param>
        /// <param name="phi">Autoregressive coefficient.</param>
        /// <param name="sigma">Innovation standard deviation.</param>
        /// <param name="length">Number of observations to generate.</param>
        /// <param name="seed">Random seed for reproducibility.</param>
        /// <returns>A tuple containing the generated time series and the true parameter values [μ, γ, φ, σ].</returns>
        public static (TimeSeries TimeSeries, double[] TrueParameters) GetAR1LinearTrendData(double mu = 100.0, double gamma = 0.5, double phi = 0.6, double sigma = 5.0, int length = 500, int seed = 12345)
        {
            var trueParameters = new double[] { mu, gamma, phi, sigma };

            var armax = new ARIMAX
            {
                IncludeIntercept = true,
                TrendType = ARIMAX.Trend.Linear,
                AROrderP = 1,
                MAOrderQ = 0
            };
            armax.SetParameterValues(trueParameters);

            var data = armax.GenerateRandomValues(length, seed);
            var timeSeries = new TimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1), data);

            return (timeSeries, trueParameters);
        }

        /// <summary>
        /// Generates synthetic AR(1) time series data with a quadratic trend for validation testing.
        /// Uses ARIMAX.GenerateRandomValues for consistent data generation.
        /// Parameters: [μ, γ1, γ2, φ, σ].
        /// </summary>
        /// <param name="mu">Process intercept.</param>
        /// <param name="gamma1">Linear trend coefficient.</param>
        /// <param name="gamma2">Quadratic trend coefficient.</param>
        /// <param name="phi">Autoregressive coefficient.</param>
        /// <param name="sigma">Innovation standard deviation.</param>
        /// <param name="length">Number of observations to generate.</param>
        /// <param name="seed">Random seed for reproducibility.</param>
        /// <returns>A tuple containing the generated time series and the true parameter values [μ, γ1, γ2, φ, σ].</returns>
        public static (TimeSeries TimeSeries, double[] TrueParameters) GetAR1QuadraticTrendData(double mu = 100.0, double gamma1 = 0.5, double gamma2 = 0.01, double phi = 0.5, double sigma = 5.0, int length = 500, int seed = 12345)
        {
            var trueParameters = new double[] { mu, gamma1, gamma2, phi, sigma };

            var armax = new ARIMAX
            {
                IncludeIntercept = true,
                TrendType = ARIMAX.Trend.Quadratic,
                AROrderP = 1,
                MAOrderQ = 0
            };
            armax.SetParameterValues(trueParameters);

            var data = armax.GenerateRandomValues(length, seed);
            var timeSeries = new TimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1), data);

            return (timeSeries, trueParameters);
        }

        /// <summary>
        /// Generates synthetic AR(1) time series data with a cubic trend for validation testing.
        /// Uses ARIMAX.GenerateRandomValues for consistent data generation.
        /// Parameters: [μ, γ1, γ2, γ3, φ, σ].
        /// </summary>
        /// <param name="mu">Process intercept.</param>
        /// <param name="gamma1">Linear trend coefficient.</param>
        /// <param name="gamma2">Quadratic trend coefficient.</param>
        /// <param name="gamma3">Cubic trend coefficient.</param>
        /// <param name="phi">Autoregressive coefficient.</param>
        /// <param name="sigma">Innovation standard deviation.</param>
        /// <param name="length">Number of observations to generate.</param>
        /// <param name="seed">Random seed for reproducibility.</param>
        /// <returns>A tuple containing the generated time series and the true parameter values [μ, γ1, γ2, γ3, φ, σ].</returns>
        public static (TimeSeries TimeSeries, double[] TrueParameters) GetAR1CubicTrendData(double mu = 100.0, double gamma1 = 0.3, double gamma2 = 0.005, double gamma3 = 0.00001, double phi = 0.4, double sigma = 5.0, int length = 500, int seed = 12345)
        {
            var trueParameters = new double[] { mu, gamma1, gamma2, gamma3, phi, sigma };

            var armax = new ARIMAX
            {
                IncludeIntercept = true,
                TrendType = ARIMAX.Trend.Cubic,
                AROrderP = 1,
                MAOrderQ = 0
            };
            armax.SetParameterValues(trueParameters);

            var data = armax.GenerateRandomValues(length, seed);
            var timeSeries = new TimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1), data);

            return (timeSeries, trueParameters);
        }

        /// <summary>
        /// Generates synthetic MA(1) time series data with a linear trend for validation testing.
        /// Uses ARIMAX.GenerateRandomValues for consistent data generation.
        /// Parameters: [μ, γ, θ, σ].
        /// </summary>
        /// <param name="mu">Process intercept.</param>
        /// <param name="gamma">Linear trend slope coefficient.</param>
        /// <param name="theta">Moving average coefficient.</param>
        /// <param name="sigma">Innovation standard deviation.</param>
        /// <param name="length">Number of observations to generate.</param>
        /// <param name="seed">Random seed for reproducibility.</param>
        /// <returns>A tuple containing the generated time series and the true parameter values [μ, γ, θ, σ].</returns>
        public static (TimeSeries TimeSeries, double[] TrueParameters) GetMA1LinearTrendData(double mu = 100.0, double gamma = 0.5, double theta = 0.5, double sigma = 5.0, int length = 500, int seed = 12345)
        {
            var trueParameters = new double[] { mu, gamma, theta, sigma };

            var armax = new ARIMAX
            {
                IncludeIntercept = true,
                TrendType = ARIMAX.Trend.Linear,
                AROrderP = 0,
                MAOrderQ = 1
            };
            armax.SetParameterValues(trueParameters);

            var data = armax.GenerateRandomValues(length, seed);
            var timeSeries = new TimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1), data);

            return (timeSeries, trueParameters);
        }

        /// <summary>
        /// Generates synthetic ARIMA(1,1) time series data with a linear trend for validation testing.
        /// Uses ARIMAX.GenerateRandomValues for consistent data generation.
        /// Parameters: [μ, γ, φ, θ, σ].
        /// </summary>
        /// <param name="mu">Process intercept.</param>
        /// <param name="gamma">Linear trend slope coefficient.</param>
        /// <param name="phi">Autoregressive coefficient.</param>
        /// <param name="theta">Moving average coefficient.</param>
        /// <param name="sigma">Innovation standard deviation.</param>
        /// <param name="length">Number of observations to generate.</param>
        /// <param name="seed">Random seed for reproducibility.</param>
        /// <returns>A tuple containing the generated time series and the true parameter values [μ, γ, φ, θ, σ].</returns>
        public static (TimeSeries TimeSeries, double[] TrueParameters) GetARIMA11LinearTrendData(double mu = 100.0, double gamma = 0.5, double phi = 0.5, double theta = 0.3, double sigma = 5.0, int length = 500, int seed = 12345)
        {
            var trueParameters = new double[] { mu, gamma, phi, theta, sigma };

            var armax = new ARIMAX
            {
                IncludeIntercept = true,
                TrendType = ARIMAX.Trend.Linear,
                AROrderP = 1,
                MAOrderQ = 1
            };
            armax.SetParameterValues(trueParameters);

            var data = armax.GenerateRandomValues(length, seed);
            var timeSeries = new TimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1), data);

            return (timeSeries, trueParameters);
        }

        #endregion

        #endregion

        #region Seasonality Data

        /// <summary>
        /// Generates synthetic AR(1) time series data with seasonal (Fourier) component.
        /// Parameters: [μ, ψ1 (sin), ψ2 (cos), φ, σ].
        /// Uses monthly data with period 12 for seasonal pattern.
        /// </summary>
        /// <param name="mu">Process intercept.</param>
        /// <param name="psi1">Fourier sine coefficient.</param>
        /// <param name="psi2">Fourier cosine coefficient.</param>
        /// <param name="phi">Autoregressive coefficient.</param>
        /// <param name="sigma">Innovation standard deviation.</param>
        /// <param name="length">Number of observations to generate.</param>
        /// <param name="seed">Random seed for reproducibility.</param>
        /// <returns>A tuple containing the generated time series and the true parameter values [μ, ψ1, ψ2, φ, σ].</returns>
        public static (TimeSeries TimeSeries, double[] TrueParameters) GetAR1SeasonalData(double mu = 100.0, double psi1 = 20.0, double psi2 = 10.0, double phi = 0.5, double sigma = 5.0, int length = 500, int seed = 12345)
        {
            // Set true parameters - intercept, seasonality (sin, cos), AR(1), sigma
            var trueParameters = new double[] { mu, psi1, psi2, phi, sigma };

            // Create model with seasonality
            var armax = new ARIMAX
            {
                IncludeIntercept = true,
                IncludeSeasonality = true,
                AROrderP = 1,
                MAOrderQ = 0
            };
            armax.SetParameterValues(trueParameters);

            // Create time series data (monthly for seasonal period of 12)
            var data = armax.GenerateRandomValues(length, seed);
            var timeSeries = new TimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1), data);

            return (timeSeries, trueParameters);
        }

        /// <summary>
        /// Generates synthetic MA(1) time series data with seasonal (Fourier) component.
        /// Parameters: [μ, ψ1 (sin), ψ2 (cos), θ, σ].
        /// Uses monthly data with period 12 for seasonal pattern.
        /// </summary>
        /// <param name="mu">Process intercept.</param>
        /// <param name="psi1">Fourier sine coefficient.</param>
        /// <param name="psi2">Fourier cosine coefficient.</param>
        /// <param name="theta">Moving average coefficient.</param>
        /// <param name="sigma">Innovation standard deviation.</param>
        /// <param name="length">Number of observations to generate.</param>
        /// <param name="seed">Random seed for reproducibility.</param>
        /// <returns>A tuple containing the generated time series and the true parameter values [μ, ψ1, ψ2, θ, σ].</returns>
        public static (TimeSeries TimeSeries, double[] TrueParameters) GetMA1SeasonalData(double mu = 100.0, double psi1 = 20.0, double psi2 = 10.0, double theta = 0.5, double sigma = 5.0, int length = 500, int seed = 12345)
        {
            // Set true parameters - intercept, seasonality (sin, cos), MA(1), sigma
            var trueParameters = new double[] { mu, psi1, psi2, theta, sigma };

            // Create model with seasonality
            var armax = new ARIMAX
            {
                IncludeIntercept = true,
                IncludeSeasonality = true,
                AROrderP = 0,
                MAOrderQ = 1
            };
            armax.SetParameterValues(trueParameters);

            // Create time series data (monthly for seasonal period of 12)
            var data = armax.GenerateRandomValues(length, seed);
            var timeSeries = new TimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1), data);

            return (timeSeries, trueParameters);
        }

        /// <summary>
        /// Generates synthetic ARIMA(1,1) time series data with seasonal (Fourier) component.
        /// Parameters: [μ, ψ1 (sin), ψ2 (cos), φ, θ, σ].
        /// Uses monthly data with period 12 for seasonal pattern.
        /// </summary>
        /// <param name="mu">Process intercept.</param>
        /// <param name="psi1">Fourier sine coefficient.</param>
        /// <param name="psi2">Fourier cosine coefficient.</param>
        /// <param name="phi">Autoregressive coefficient.</param>
        /// <param name="theta">Moving average coefficient.</param>
        /// <param name="sigma">Innovation standard deviation.</param>
        /// <param name="length">Number of observations to generate.</param>
        /// <param name="seed">Random seed for reproducibility.</param>
        /// <returns>A tuple containing the generated time series and the true parameter values [μ, ψ1, ψ2, φ, θ, σ].</returns>
        public static (TimeSeries TimeSeries, double[] TrueParameters) GetARIMA11SeasonalData(double mu = 100.0, double psi1 = 20.0, double psi2 = 10.0, double phi = 0.5, double theta = 0.3, double sigma = 5.0, int length = 500, int seed = 12345)
        {
            // Set true parameters - intercept, seasonality (sin, cos), AR(1), MA(1), sigma
            var trueParameters = new double[] { mu, psi1, psi2, phi, theta, sigma };

            // Create model with seasonality
            var armax = new ARIMAX
            {
                IncludeIntercept = true,
                IncludeSeasonality = true,
                AROrderP = 1,
                MAOrderQ = 1
            };
            armax.SetParameterValues(trueParameters);

            // Create time series data (monthly for seasonal period of 12)
            var data = armax.GenerateRandomValues(length, seed);
            var timeSeries = new TimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1), data);

            return (timeSeries, trueParameters);
        }

        /// <summary>
        /// Generates synthetic AR(1) time series data with linear trend and seasonal components.
        /// Parameters: [μ, γ (slope), ψ1 (sin), ψ2 (cos), φ, σ].
        /// Uses monthly data with period 12 for seasonal pattern.
        /// </summary>
        /// <param name="mu">Process intercept.</param>
        /// <param name="gamma">Linear trend slope.</param>
        /// <param name="psi1">Fourier sine coefficient.</param>
        /// <param name="psi2">Fourier cosine coefficient.</param>
        /// <param name="phi">Autoregressive coefficient.</param>
        /// <param name="sigma">Innovation standard deviation.</param>
        /// <param name="length">Number of observations to generate.</param>
        /// <param name="seed">Random seed for reproducibility.</param>
        /// <returns>A tuple containing the generated time series and the true parameter values [μ, γ, ψ1, ψ2, φ, σ].</returns>
        public static (TimeSeries TimeSeries, double[] TrueParameters) GetAR1TrendSeasonalData(double mu = 100.0, double gamma = 0.3, double psi1 = 20.0, double psi2 = 10.0, double phi = 0.5, double sigma = 5.0, int length = 500, int seed = 12345)
        {
            // Set true parameters - intercept, linear trend, seasonality (sin, cos), AR(1), sigma
            var trueParameters = new double[] { mu, gamma, psi1, psi2, phi, sigma };

            // Create model with linear trend and seasonality
            var armax = new ARIMAX
            {
                IncludeIntercept = true,
                TrendType = ARIMAX.Trend.Linear,
                IncludeSeasonality = true,
                AROrderP = 1,
                MAOrderQ = 0
            };
            armax.SetParameterValues(trueParameters);

            // Create time series data (monthly for seasonal period of 12)
            var data = armax.GenerateRandomValues(length, seed);
            var timeSeries = new TimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1), data);

            return (timeSeries, trueParameters);
        }

        #endregion

        #region SARIMA Data (Differencing + Seasonality)

        /// <summary>
        /// Generates synthetic ARIMA(1,1,0) time series data with seasonal component.
        /// This is similar to SARIMA but using Fourier seasonality rather than seasonal AR/MA.
        /// Parameters: [μ, ψ1 (sin), ψ2 (cos), φ, σ].
        /// </summary>
        /// <param name="mu">Process mean (intercept of the differenced series).</param>
        /// <param name="psi1">Fourier sine coefficient.</param>
        /// <param name="psi2">Fourier cosine coefficient.</param>
        /// <param name="phi">Autoregressive coefficient.</param>
        /// <param name="sigma">Innovation standard deviation.</param>
        /// <param name="length">Number of observations to generate.</param>
        /// <param name="seed">Random seed for reproducibility.</param>
        /// <returns>A tuple containing the generated time series and the true parameter values [μ, ψ1, ψ2, φ, σ].</returns>
        public static (TimeSeries TimeSeries, double[] TrueParameters) GetSARIMA110SeasonalData(double mu = 0.5, double psi1 = 15.0, double psi2 = 10.0, double phi = 0.5, double sigma = 3.0, int length = 500, int seed = 12345)
        {
            // Set true parameters - intercept, seasonality (sin, cos), AR(1), sigma
            var trueParameters = new double[] { mu, psi1, psi2, phi, sigma };

            // Create model with differencing and seasonality
            var armax = new ARIMAX
            {
                IncludeIntercept = true,
                IncludeSeasonality = true,
                DiffOrderD = 1,
                AROrderP = 1,
                MAOrderQ = 0
            };
            armax.SetParameterValues(trueParameters);

            // Create time series data (monthly for seasonal period of 12)
            var data = armax.GenerateRandomValues(length, seed);
            var timeSeries = new TimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1), data);

            return (timeSeries, trueParameters);
        }

        /// <summary>
        /// Generates synthetic ARIMA(1,1,1) time series data with seasonal component.
        /// Combines ARIMA differencing with Fourier seasonal pattern.
        /// Parameters: [μ, ψ1 (sin), ψ2 (cos), φ, θ, σ].
        /// </summary>
        /// <param name="mu">Process mean (intercept of the differenced series).</param>
        /// <param name="psi1">Fourier sine coefficient.</param>
        /// <param name="psi2">Fourier cosine coefficient.</param>
        /// <param name="phi">Autoregressive coefficient.</param>
        /// <param name="theta">Moving average coefficient.</param>
        /// <param name="sigma">Innovation standard deviation.</param>
        /// <param name="length">Number of observations to generate.</param>
        /// <param name="seed">Random seed for reproducibility.</param>
        /// <returns>A tuple containing the generated time series and the true parameter values [μ, ψ1, ψ2, φ, θ, σ].</returns>
        public static (TimeSeries TimeSeries, double[] TrueParameters) GetSARIMA111SeasonalData(double mu = 0.3, double psi1 = 15.0, double psi2 = 10.0, double phi = 0.5, double theta = 0.3, double sigma = 3.0, int length = 500, int seed = 12345)
        {
            // Set true parameters - intercept, seasonality (sin, cos), AR(1), MA(1), sigma
            var trueParameters = new double[] { mu, psi1, psi2, phi, theta, sigma };

            // Create model with differencing and seasonality
            var armax = new ARIMAX
            {
                IncludeIntercept = true,
                IncludeSeasonality = true,
                DiffOrderD = 1,
                AROrderP = 1,
                MAOrderQ = 1
            };
            armax.SetParameterValues(trueParameters);

            // Create time series data (monthly for seasonal period of 12)
            var data = armax.GenerateRandomValues(length, seed);
            var timeSeries = new TimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1), data);

            return (timeSeries, trueParameters);
        }

        /// <summary>
        /// Generates synthetic ARIMA(1,1,0) with trend and seasonal components.
        /// Full nonstationary model with differencing, trend, and seasonality.
        /// Parameters: [μ, γ (slope), ψ1 (sin), ψ2 (cos), φ, σ].
        /// </summary>
        /// <param name="mu">Process mean (intercept of the differenced series).</param>
        /// <param name="gamma">Linear trend slope.</param>
        /// <param name="psi1">Fourier sine coefficient.</param>
        /// <param name="psi2">Fourier cosine coefficient.</param>
        /// <param name="phi">Autoregressive coefficient.</param>
        /// <param name="sigma">Innovation standard deviation.</param>
        /// <param name="length">Number of observations to generate.</param>
        /// <param name="seed">Random seed for reproducibility.</param>
        /// <returns>A tuple containing the generated time series and the true parameter values [μ, γ, ψ1, ψ2, φ, σ].</returns>
        public static (TimeSeries TimeSeries, double[] TrueParameters) GetSARIMA110TrendSeasonalData(double mu = 0.5, double gamma = 0.2, double psi1 = 15.0, double psi2 = 10.0, double phi = 0.4, double sigma = 3.0, int length = 500, int seed = 12345)
        {
            // Set true parameters - intercept, linear trend, seasonality (sin, cos), AR(1), sigma
            var trueParameters = new double[] { mu, gamma, psi1, psi2, phi, sigma };

            // Create model with differencing, trend, and seasonality
            var armax = new ARIMAX
            {
                IncludeIntercept = true,
                TrendType = ARIMAX.Trend.Linear,
                IncludeSeasonality = true,
                DiffOrderD = 1,
                AROrderP = 1,
                MAOrderQ = 0
            };
            armax.SetParameterValues(trueParameters);

            // Create time series data (monthly for seasonal period of 12)
            var data = armax.GenerateRandomValues(length, seed);
            var timeSeries = new TimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1), data);

            return (timeSeries, trueParameters);
        }

        #endregion

        #region Covariate Data

        /// <summary>
        /// Generates synthetic time series data with a single exogenous covariate.
        /// The response Y = μ + β*X + AR(1) error.
        /// Parameters: [μ, β, φ, σ].
        /// </summary>
        /// <param name="mu">Process intercept.</param>
        /// <param name="beta">Covariate coefficient.</param>
        /// <param name="phi">Autoregressive coefficient for the error term.</param>
        /// <param name="sigma">Innovation standard deviation.</param>
        /// <param name="length">Number of observations to generate.</param>
        /// <param name="seed">Random seed for reproducibility.</param>
        /// <returns>A tuple containing the response time series, covariate time series, and true parameters [μ, β, φ, σ].</returns>
        public static (TimeSeries TimeSeries, TimeSeries Covariate, double[] TrueParameters) GetAR1WithCovariateData(double mu = 50.0, double beta = 2.0, double phi = 0.5, double sigma = 5.0, int length = 500, int seed = 12345)
        {
            var trueParameters = new double[] { mu, beta, phi, sigma };
            var rng = new Random(seed);
            var normDist = new Numerics.Distributions.Normal(0, 1);

            // Generate covariate X as a random walk for realistic variation
            var xValues = new double[length];
            xValues[0] = 10.0;
            for (int t = 1; t < length; t++)
            {
                xValues[t] = xValues[t - 1] + normDist.InverseCDF(rng.NextDouble()) * 0.5;
            }

            // Generate AR(1) errors
            var errors = new double[length];
            errors[0] = normDist.InverseCDF(rng.NextDouble()) * sigma;
            for (int t = 1; t < length; t++)
            {
                double innovation = normDist.InverseCDF(rng.NextDouble()) * sigma;
                errors[t] = phi * errors[t - 1] + innovation;
            }

            // Construct Y = μ + β*X + AR(1) error
            var yValues = new double[length];
            for (int t = 0; t < length; t++)
            {
                yValues[t] = mu + beta * xValues[t] + errors[t];
            }

            var startDate = new DateTime(2000, 1, 1);
            var timeSeries = new TimeSeries(TimeInterval.OneMonth, startDate, yValues);
            var covariate = new TimeSeries(TimeInterval.OneMonth, startDate, xValues);

            return (timeSeries, covariate, trueParameters);
        }

        /// <summary>
        /// Generates synthetic time series data with a single exogenous covariate and MA(1) errors.
        /// The response Y = μ + β*X + MA(1) error.
        /// Parameters: [μ, β, θ, σ].
        /// </summary>
        /// <param name="mu">Process intercept.</param>
        /// <param name="beta">Covariate coefficient.</param>
        /// <param name="theta">Moving average coefficient for the error term.</param>
        /// <param name="sigma">Innovation standard deviation.</param>
        /// <param name="length">Number of observations to generate.</param>
        /// <param name="seed">Random seed for reproducibility.</param>
        /// <returns>A tuple containing the response time series, covariate time series, and true parameters [μ, β, θ, σ].</returns>
        public static (TimeSeries TimeSeries, TimeSeries Covariate, double[] TrueParameters) GetMA1WithCovariateData(double mu = 50.0, double beta = 2.0, double theta = 0.5, double sigma = 5.0, int length = 500, int seed = 12345)
        {
            var trueParameters = new double[] { mu, beta, theta, sigma };
            var rng = new Random(seed);
            var normDist = new Numerics.Distributions.Normal(0, 1);

            // Generate covariate X as a random walk for realistic variation
            var xValues = new double[length];
            xValues[0] = 10.0;
            for (int t = 1; t < length; t++)
            {
                xValues[t] = xValues[t - 1] + normDist.InverseCDF(rng.NextDouble()) * 0.5;
            }

            // Generate MA(1) errors: e[t] = eps[t] + theta * eps[t-1]
            var innovations = new double[length];
            var errors = new double[length];
            for (int t = 0; t < length; t++)
            {
                innovations[t] = normDist.InverseCDF(rng.NextDouble()) * sigma;
                errors[t] = innovations[t] + (t > 0 ? theta * innovations[t - 1] : 0);
            }

            // Construct Y = μ + β*X + MA(1) error
            var yValues = new double[length];
            for (int t = 0; t < length; t++)
            {
                yValues[t] = mu + beta * xValues[t] + errors[t];
            }

            var startDate = new DateTime(2000, 1, 1);
            var timeSeries = new TimeSeries(TimeInterval.OneMonth, startDate, yValues);
            var covariate = new TimeSeries(TimeInterval.OneMonth, startDate, xValues);

            return (timeSeries, covariate, trueParameters);
        }

        /// <summary>
        /// Generates synthetic time series data with a single exogenous covariate and ARIMA(1,1) errors.
        /// The response Y = μ + β*X + ARIMA(1,1) error.
        /// Parameters: [μ, β, φ, θ, σ].
        /// </summary>
        /// <param name="mu">Process intercept.</param>
        /// <param name="beta">Covariate coefficient.</param>
        /// <param name="phi">Autoregressive coefficient for the error term.</param>
        /// <param name="theta">Moving average coefficient for the error term.</param>
        /// <param name="sigma">Innovation standard deviation.</param>
        /// <param name="length">Number of observations to generate.</param>
        /// <param name="seed">Random seed for reproducibility.</param>
        /// <returns>A tuple containing the response time series, covariate time series, and true parameters [μ, β, φ, θ, σ].</returns>
        public static (TimeSeries TimeSeries, TimeSeries Covariate, double[] TrueParameters) GetARIMA11WithCovariateData(double mu = 50.0, double beta = 2.0, double phi = 0.5, double theta = 0.3, double sigma = 5.0, int length = 500, int seed = 12345)
        {
            var trueParameters = new double[] { mu, beta, phi, theta, sigma };
            var rng = new Random(seed);
            var normDist = new Numerics.Distributions.Normal(0, 1);

            // Generate covariate X as a random walk for realistic variation
            var xValues = new double[length];
            xValues[0] = 10.0;
            for (int t = 1; t < length; t++)
            {
                xValues[t] = xValues[t - 1] + normDist.InverseCDF(rng.NextDouble()) * 0.5;
            }

            // Generate ARIMA(1,1) errors: e[t] = phi * e[t-1] + eps[t] + theta * eps[t-1]
            var innovations = new double[length];
            var errors = new double[length];
            for (int t = 0; t < length; t++)
            {
                innovations[t] = normDist.InverseCDF(rng.NextDouble()) * sigma;
                double arPart = t > 0 ? phi * errors[t - 1] : 0;
                double maPart = t > 0 ? theta * innovations[t - 1] : 0;
                errors[t] = arPart + innovations[t] + maPart;
            }

            // Construct Y = μ + β*X + ARIMA(1,1) error
            var yValues = new double[length];
            for (int t = 0; t < length; t++)
            {
                yValues[t] = mu + beta * xValues[t] + errors[t];
            }

            var startDate = new DateTime(2000, 1, 1);
            var timeSeries = new TimeSeries(TimeInterval.OneMonth, startDate, yValues);
            var covariate = new TimeSeries(TimeInterval.OneMonth, startDate, xValues);

            return (timeSeries, covariate, trueParameters);
        }

        /// <summary>
        /// Generates synthetic time series data with two exogenous covariates and AR(1) errors.
        /// The response Y = μ + β1*X1 + β2*X2 + AR(1) error.
        /// Parameters: [μ, β1, β2, φ, σ].
        /// </summary>
        /// <param name="mu">Process intercept.</param>
        /// <param name="beta1">First covariate coefficient.</param>
        /// <param name="beta2">Second covariate coefficient.</param>
        /// <param name="phi">Autoregressive coefficient for the error term.</param>
        /// <param name="sigma">Innovation standard deviation.</param>
        /// <param name="length">Number of observations to generate.</param>
        /// <param name="seed">Random seed for reproducibility.</param>
        /// <returns>A tuple containing the response time series, list of covariates, and true parameters [μ, β1, β2, φ, σ].</returns>
        public static (TimeSeries TimeSeries, List<TimeSeries> Covariates, double[] TrueParameters) GetAR1WithTwoCovariatesData(double mu = 50.0, double beta1 = 2.0, double beta2 = -1.5, double phi = 0.5, double sigma = 5.0, int length = 500, int seed = 12345)
        {
            var trueParameters = new double[] { mu, beta1, beta2, phi, sigma };
            var rng = new Random(seed);
            var normDist = new Numerics.Distributions.Normal(0, 1);

            // Generate covariate X1 as a random walk
            var x1Values = new double[length];
            x1Values[0] = 10.0;
            for (int t = 1; t < length; t++)
            {
                x1Values[t] = x1Values[t - 1] + normDist.InverseCDF(rng.NextDouble()) * 0.5;
            }

            // Generate covariate X2 as a random walk (different seed offset)
            var x2Values = new double[length];
            x2Values[0] = 5.0;
            for (int t = 1; t < length; t++)
            {
                x2Values[t] = x2Values[t - 1] + normDist.InverseCDF(rng.NextDouble()) * 0.3;
            }

            // Generate AR(1) errors
            var errors = new double[length];
            errors[0] = normDist.InverseCDF(rng.NextDouble()) * sigma;
            for (int t = 1; t < length; t++)
            {
                double innovation = normDist.InverseCDF(rng.NextDouble()) * sigma;
                errors[t] = phi * errors[t - 1] + innovation;
            }

            // Construct Y = μ + β1*X1 + β2*X2 + AR(1) error
            var yValues = new double[length];
            for (int t = 0; t < length; t++)
            {
                yValues[t] = mu + beta1 * x1Values[t] + beta2 * x2Values[t] + errors[t];
            }

            var startDate = new DateTime(2000, 1, 1);
            var timeSeries = new TimeSeries(TimeInterval.OneMonth, startDate, yValues);
            var covariates = new List<TimeSeries>
            {
                new TimeSeries(TimeInterval.OneMonth, startDate, x1Values),
                new TimeSeries(TimeInterval.OneMonth, startDate, x2Values)
            };

            return (timeSeries, covariates, trueParameters);
        }

        #endregion

        #region Full Combination Data

        /// <summary>
        /// Generates synthetic time series combining trend, seasonality, and AR(1) errors.
        /// A comprehensive test of multiple ARIMAX features working together.
        /// Parameters: [μ, γ (slope), ψ1 (sin), ψ2 (cos), φ, σ].
        /// </summary>
        /// <param name="mu">Process intercept.</param>
        /// <param name="gamma">Linear trend slope.</param>
        /// <param name="psi1">Fourier sine coefficient.</param>
        /// <param name="psi2">Fourier cosine coefficient.</param>
        /// <param name="phi">Autoregressive coefficient.</param>
        /// <param name="sigma">Innovation standard deviation.</param>
        /// <param name="length">Number of observations to generate.</param>
        /// <param name="seed">Random seed for reproducibility.</param>
        /// <returns>A tuple containing the generated time series and the true parameter values [μ, γ, ψ1, ψ2, φ, σ].</returns>
        public static (TimeSeries TimeSeries, double[] TrueParameters) GetFullARIMAX_TrendSeasonalAR1Data(double mu = 100.0, double gamma = 0.3, double psi1 = 20.0, double psi2 = 10.0, double phi = 0.5, double sigma = 5.0, int length = 500, int seed = 12345)
        {
            // Set true parameters
            var trueParameters = new double[] { mu, gamma, psi1, psi2, phi, sigma };

            // Create model with trend and seasonality
            var armax = new ARIMAX
            {
                IncludeIntercept = true,
                TrendType = ARIMAX.Trend.Linear,
                IncludeSeasonality = true,
                AROrderP = 1,
                MAOrderQ = 0
            };
            armax.SetParameterValues(trueParameters);

            // Create time series data (monthly for seasonal period of 12)
            var data = armax.GenerateRandomValues(length, seed);
            var timeSeries = new TimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1), data);

            return (timeSeries, trueParameters);
        }

        /// <summary>
        /// Generates synthetic time series combining trend, seasonality, differencing, and ARIMA(1,1) errors.
        /// The most comprehensive combination test.
        /// Parameters: [μ, γ (slope), ψ1 (sin), ψ2 (cos), φ, θ, σ].
        /// </summary>
        /// <param name="mu">Process mean (intercept of the differenced series).</param>
        /// <param name="gamma">Linear trend slope.</param>
        /// <param name="psi1">Fourier sine coefficient.</param>
        /// <param name="psi2">Fourier cosine coefficient.</param>
        /// <param name="phi">Autoregressive coefficient.</param>
        /// <param name="theta">Moving average coefficient.</param>
        /// <param name="sigma">Innovation standard deviation.</param>
        /// <param name="length">Number of observations to generate.</param>
        /// <param name="seed">Random seed for reproducibility.</param>
        /// <returns>A tuple containing the generated time series and the true parameter values [μ, γ, ψ1, ψ2, φ, θ, σ].</returns>
        public static (TimeSeries TimeSeries, double[] TrueParameters) GetFullARIMAX_TrendSeasonalARIMA111Data(double mu = 0.3, double gamma = 0.15, double psi1 = 15.0, double psi2 = 8.0, double phi = 0.4, double theta = 0.3, double sigma = 3.0, int length = 500, int seed = 12345)
        {
            // Set true parameters
            var trueParameters = new double[] { mu, gamma, psi1, psi2, phi, theta, sigma };

            // Create model with trend, seasonality, and differencing
            var armax = new ARIMAX
            {
                IncludeIntercept = true,
                TrendType = ARIMAX.Trend.Linear,
                IncludeSeasonality = true,
                DiffOrderD = 1,
                AROrderP = 1,
                MAOrderQ = 1
            };
            armax.SetParameterValues(trueParameters);

            // Create time series data (monthly for seasonal period of 12)
            var data = armax.GenerateRandomValues(length, seed);
            var timeSeries = new TimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1), data);

            return (timeSeries, trueParameters);
        }

        /// <summary>
        /// Generates synthetic ARIMA(1,1) time series data with linear trend and Fourier seasonality (no differencing).
        /// This is the comprehensive combination without differencing since differencing removes trends.
        /// Parameters: [μ, γ, ψ1, ψ2, φ, θ, σ].
        /// </summary>
        /// <param name="mu">Process intercept.</param>
        /// <param name="gamma">Linear trend coefficient.</param>
        /// <param name="psi1">Sine seasonality coefficient.</param>
        /// <param name="psi2">Cosine seasonality coefficient.</param>
        /// <param name="phi">Autoregressive coefficient.</param>
        /// <param name="theta">Moving average coefficient.</param>
        /// <param name="sigma">Innovation standard deviation.</param>
        /// <param name="length">Number of observations to generate.</param>
        /// <param name="seed">Random seed for reproducibility.</param>
        /// <returns>A tuple containing the generated time series and the true parameter values.</returns>
        public static (TimeSeries TimeSeries, double[] TrueParameters) GetFullARIMAX_TrendSeasonalARIMA11Data(double mu = 100.0, double gamma = 0.2, double psi1 = 15.0, double psi2 = 8.0, double phi = 0.4, double theta = 0.3, double sigma = 3.0, int length = 500, int seed = 12345)
        {
            // Set true parameters
            var trueParameters = new double[] { mu, gamma, psi1, psi2, phi, theta, sigma };

            // Create model with trend and seasonality (no differencing)
            var armax = new ARIMAX
            {
                IncludeIntercept = true,
                TrendType = ARIMAX.Trend.Linear,
                IncludeSeasonality = true,
                DiffOrderD = 0,
                AROrderP = 1,
                MAOrderQ = 1
            };
            armax.SetParameterValues(trueParameters);

            // Create time series data (monthly for seasonal period of 12)
            var data = armax.GenerateRandomValues(length, seed);
            var timeSeries = new TimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1), data);

            return (timeSeries, trueParameters);
        }

        #endregion

        #region Transform Data

        /// <summary>
        /// Generates synthetic AR(1) time series data on positive values suitable for logarithmic transform.
        /// The data is generated with a positive mean to ensure all values remain positive.
        /// Parameters: [μ, φ, σ].
        /// </summary>
        /// <param name="mu">Process mean (must be large enough to keep data positive).</param>
        /// <param name="phi">Autoregressive coefficient.</param>
        /// <param name="sigma">Innovation standard deviation (should be small relative to mean).</param>
        /// <param name="length">Number of observations to generate.</param>
        /// <param name="seed">Random seed for reproducibility.</param>
        /// <returns>A tuple containing the generated positive time series and the true parameter values [μ, φ, σ].</returns>
        public static (TimeSeries TimeSeries, double[] TrueParameters) GetAR1PositiveData(double mu = 500.0, double phi = 0.5, double sigma = 20.0, int length = 500, int seed = 12345)
        {
            // Set true parameters
            var trueParameters = new double[] { mu, phi, sigma };

            // Create AR(1) model
            var armax = new ARIMAX
            {
                IncludeIntercept = true,
                AROrderP = 1,
                MAOrderQ = 0
            };
            armax.SetParameterValues(trueParameters);

            // Create time series data ensuring positive values
            var data = armax.GenerateRandomValues(length, seed);

            // Shift to ensure positive (for log transform testing)
            double minVal = data.Min();
            if (minVal <= 0)
            {
                double shift = Math.Abs(minVal) + 10;
                for (int i = 0; i < data.Length; i++)
                    data[i] += shift;
            }

            var timeSeries = new TimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1), data);

            return (timeSeries, trueParameters);
        }

        /// <summary>
        /// Generates synthetic AR(1) time series data with both positive and negative values.
        /// Suitable for Yeo-Johnson transform testing (which handles negative values).
        /// Parameters: [μ, φ, σ].
        /// </summary>
        /// <param name="mu">Process mean (can be negative, zero, or positive).</param>
        /// <param name="phi">Autoregressive coefficient.</param>
        /// <param name="sigma">Innovation standard deviation.</param>
        /// <param name="length">Number of observations to generate.</param>
        /// <param name="seed">Random seed for reproducibility.</param>
        /// <returns>A tuple containing the generated time series and the true parameter values [μ, φ, σ].</returns>
        public static (TimeSeries TimeSeries, double[] TrueParameters) GetAR1MixedSignData(double mu = 0.0, double phi = 0.5, double sigma = 10.0, int length = 500, int seed = 12345)
        {
            // Set true parameters
            var trueParameters = new double[] { mu, phi, sigma };

            // Create AR(1) model
            var armax = new ARIMAX
            {
                IncludeIntercept = true,
                AROrderP = 1,
                MAOrderQ = 0
            };
            armax.SetParameterValues(trueParameters);

            // Create time series data (will have mixed signs around zero mean)
            var data = armax.GenerateRandomValues(length, seed);
            var timeSeries = new TimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1), data);

            return (timeSeries, trueParameters);
        }

        /// <summary>
        /// Generates synthetic lognormal-like time series data suitable for log transform.
        /// Uses ARIMAX.GenerateRandomValues with log transform to generate data where
        /// parameters are interpreted on the original scale and errors are additive on the log scale.
        /// </summary>
        /// <param name="mu">Mean of the series on the original scale.</param>
        /// <param name="phi">Autoregressive coefficient.</param>
        /// <param name="sigma">Innovation standard deviation on the log scale.</param>
        /// <param name="length">Number of observations to generate.</param>
        /// <param name="seed">Random seed for reproducibility.</param>
        /// <returns>A tuple containing the generated time series and original-scale parameters [μ, φ, σ].</returns>
        public static (TimeSeries TimeSeries, double[] TrueParameters) GetLognormalAR1Data(double mu = 150.0, double phi = 0.5, double sigma = 0.3, int length = 500, int seed = 12345)
        {
            var trueParameters = new double[] { mu, phi, sigma };

            var armax = new ARIMAX
            {
                IncludeIntercept = true,
                TransformType = RMC.BestFit.Models.Transform.Logarithmic,
                AROrderP = 1,
                MAOrderQ = 0
            };
            armax.SetTransformParameters(0, 0); // lambda = 0 for log transform
            armax.SetParameterValues(trueParameters);

            var data = armax.GenerateRandomValues(length, seed);
            var timeSeries = new TimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1), data);

            return (timeSeries, trueParameters);
        }

        #endregion

    }
}
