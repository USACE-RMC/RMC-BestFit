using Numerics.Distributions;
using Numerics.Sampling;

namespace RMC.BestFit.Verification;

/// <summary>
/// Provides synthetic test data generated from known distributions for unit testing.
/// All datasets contain 1000 samples generated with seed 12345 for reproducibility.
/// </summary>
/// <remarks>
///     <b> Authors: </b>
///     <list type="bullet">
///     <item>Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil</item>
///     </list>
/// </remarks>
public static class TestData
{
    /// <summary>
    /// Random seed used for all synthetic data generation.
    /// </summary>
    public const int Seed = 12345;

    /// <summary>
    /// Number of samples in each synthetic dataset.
    /// </summary>
    public const int SampleSize = 1000;

    #region Normal Family

    /// <summary>
    /// Synthetic Normal distribution data. N(μ=100, σ=15).
    /// </summary>
    public static double[] NormalData { get; } = GenerateNormalData();

    /// <summary>
    /// True parameters for NormalData: [μ, σ].
    /// </summary>
    public static double[] NormalTrueParams { get; } = [100.0, 15.0];

    /// <summary>
    /// Generates normal Data.
    /// </summary>
    /// <returns>The generated data values.</returns>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static double[] GenerateNormalData()
    {
        var dist = new Normal(100.0, 15.0);
        return dist.GenerateRandomValues(SampleSize, Seed);
    }

    /// <summary>
    /// Synthetic Log-Normal (natural log) distribution data. LN(μ=3.5, σ=0.4).
    /// </summary>
    public static double[] LnNormalData { get; } = GenerateLnNormalData();

    /// <summary>
    /// True parameters for LnNormalData: [μ, σ].
    /// </summary>
    public static double[] LnNormalTrueParams { get; } = [3.5, 0.4];

    /// <summary>
    /// Generates ln Normal Data.
    /// </summary>
    /// <returns>The generated data values.</returns>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static double[] GenerateLnNormalData()
    {
        var dist = new LnNormal(3.5, 0.4);
        return dist.GenerateRandomValues(SampleSize, Seed);
    }

    /// <summary>
    /// Synthetic Generalized Normal distribution data. GNO(ξ=50, α=10, κ=-0.3).
    /// </summary>
    public static double[] GeneralizedNormalData { get; } = GenerateGeneralizedNormalData();

    /// <summary>
    /// True parameters for GeneralizedNormalData: [ξ, α, κ].
    /// </summary>
    public static double[] GeneralizedNormalTrueParams { get; } = [50.0, 10.0, -0.3];

    /// <summary>
    /// Generates generalized Normal Data.
    /// </summary>
    /// <returns>The generated data values.</returns>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static double[] GenerateGeneralizedNormalData()
    {
        var dist = new GeneralizedNormal(50.0, 10.0, -0.3);
        return dist.GenerateRandomValues(SampleSize, Seed);
    }

    #endregion

    #region Gamma Family

    /// <summary>
    /// Synthetic Exponential distribution data. Exp(ξ=10, α=25).
    /// </summary>
    public static double[] ExponentialData { get; } = GenerateExponentialData();

    /// <summary>
    /// True parameters for ExponentialData: [ξ, α].
    /// </summary>
    public static double[] ExponentialTrueParams { get; } = [10.0, 25.0];

    /// <summary>
    /// Generates exponential Data.
    /// </summary>
    /// <returns>The generated data values.</returns>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static double[] GenerateExponentialData()
    {
        var dist = new Exponential(10.0, 25.0);
        return dist.GenerateRandomValues(SampleSize, Seed);
    }

    /// <summary>
    /// Synthetic Gamma distribution data. Gamma(θ=5, κ=3).
    /// </summary>
    public static double[] GammaData { get; } = GenerateGammaData();

    /// <summary>
    /// True parameters for GammaData: [θ, κ].
    /// </summary>
    public static double[] GammaTrueParams { get; } = [5.0, 3.0];

    /// <summary>
    /// Generates gamma Data.
    /// </summary>
    /// <returns>The generated data values.</returns>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static double[] GenerateGammaData()
    {
        var dist = new GammaDistribution(5.0, 3.0);
        return dist.GenerateRandomValues(SampleSize, Seed);
    }

    /// <summary>
    /// Synthetic Pearson Type III distribution data. PE3(μ=100, σ=20, γ=0.8).
    /// </summary>
    public static double[] PearsonTypeIIIData { get; } = GeneratePearsonTypeIIIData();

    /// <summary>
    /// True parameters for PearsonTypeIIIData: [μ, σ, γ].
    /// </summary>
    public static double[] PearsonTypeIIITrueParams { get; } = [100.0, 20.0, 0.8];

    /// <summary>
    /// Generates pearson Type III Data.
    /// </summary>
    /// <returns>The generated data values.</returns>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static double[] GeneratePearsonTypeIIIData()
    {
        var dist = new PearsonTypeIII(100.0, 20.0, 0.8);
        return dist.GenerateRandomValues(SampleSize, Seed);
    }

    /// <summary>
    /// Synthetic Log-Pearson Type III distribution data. LP3(μ=2.0, σ=0.3, γ=0.5).
    /// </summary>
    public static double[] LogPearsonTypeIIIData { get; } = GenerateLogPearsonTypeIIIData();

    /// <summary>
    /// True parameters for LogPearsonTypeIIIData: [μ, σ, γ].
    /// </summary>
    public static double[] LogPearsonTypeIIITrueParams { get; } = [2.0, 0.3, 0.5];

    /// <summary>
    /// Generates log Pearson Type III Data.
    /// </summary>
    /// <returns>The generated data values.</returns>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static double[] GenerateLogPearsonTypeIIIData()
    {
        var dist = new LogPearsonTypeIII(2.0, 0.3, 0.5);
        return dist.GenerateRandomValues(SampleSize, Seed);
    }

    #endregion

    #region Extreme Value Distributions

    /// <summary>
    /// Synthetic Gumbel distribution data. Gumbel(ξ=50, α=15).
    /// </summary>
    public static double[] GumbelData { get; } = GenerateGumbelData();

    /// <summary>
    /// True parameters for GumbelData: [ξ, α].
    /// </summary>
    public static double[] GumbelTrueParams { get; } = [50.0, 15.0];

    /// <summary>
    /// Generates gumbel Data.
    /// </summary>
    /// <returns>The generated data values.</returns>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static double[] GenerateGumbelData()
    {
        var dist = new Gumbel(50.0, 15.0);
        return dist.GenerateRandomValues(SampleSize, Seed);
    }

    /// <summary>
    /// Synthetic Weibull distribution data. Weibull(λ=100, κ=2.5).
    /// </summary>
    public static double[] WeibullData { get; } = GenerateWeibullData();

    /// <summary>
    /// True parameters for WeibullData: [λ, κ].
    /// </summary>
    public static double[] WeibullTrueParams { get; } = [100.0, 2.5];

    /// <summary>
    /// Generates weibull Data.
    /// </summary>
    /// <returns>The generated data values.</returns>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static double[] GenerateWeibullData()
    {
        var dist = new Weibull(100.0, 2.5);
        return dist.GenerateRandomValues(SampleSize, Seed);
    }

    /// <summary>
    /// Synthetic GEV distribution data. GEV(ξ=50, α=15, κ=0.1).
    /// </summary>
    public static double[] GEVData { get; } = GenerateGEVData();

    /// <summary>
    /// True parameters for GEVData: [ξ, α, κ].
    /// </summary>
    public static double[] GEVTrueParams { get; } = [50.0, 15.0, 0.1];

    /// <summary>
    /// Generates gEV Data.
    /// </summary>
    /// <returns>The generated data values.</returns>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static double[] GenerateGEVData()
    {
        var dist = new GeneralizedExtremeValue(50.0, 15.0, 0.1);
        return dist.GenerateRandomValues(SampleSize, Seed);
    }

    /// <summary>
    /// Synthetic Generalized Pareto distribution data. GPA(ξ=0, α=20, κ=0.15).
    /// </summary>
    public static double[] GeneralizedParetoData { get; } = GenerateGeneralizedParetoData();

    /// <summary>
    /// True parameters for GeneralizedParetoData: [ξ, α, κ].
    /// </summary>
    public static double[] GeneralizedParetoTrueParams { get; } = [0.0, 20.0, 0.15];

    /// <summary>
    /// Generates generalized Pareto Data.
    /// </summary>
    /// <returns>The generated data values.</returns>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static double[] GenerateGeneralizedParetoData()
    {
        var dist = new GeneralizedPareto(0.0, 20.0, 0.15);
        return dist.GenerateRandomValues(SampleSize, Seed);
    }

    #endregion

    #region Logistic Distributions

    /// <summary>
    /// Synthetic Logistic distribution data. Logistic(ξ=75, α=10).
    /// </summary>
    public static double[] LogisticData { get; } = GenerateLogisticData();

    /// <summary>
    /// True parameters for LogisticData: [ξ, α].
    /// </summary>
    public static double[] LogisticTrueParams { get; } = [75.0, 10.0];

    /// <summary>
    /// Generates logistic Data.
    /// </summary>
    /// <returns>The generated data values.</returns>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static double[] GenerateLogisticData()
    {
        var dist = new Logistic(75.0, 10.0);
        return dist.GenerateRandomValues(SampleSize, Seed);
    }

    /// <summary>
    /// Synthetic Generalized Logistic distribution data. GLO(ξ=75, α=10, κ=0.15).
    /// </summary>
    public static double[] GeneralizedLogisticData { get; } = GenerateGeneralizedLogisticData();

    /// <summary>
    /// True parameters for GeneralizedLogisticData: [ξ, α, κ].
    /// </summary>
    public static double[] GeneralizedLogisticTrueParams { get; } = [75.0, 10.0, 0.15];

    /// <summary>
    /// Generates generalized Logistic Data.
    /// </summary>
    /// <returns>The generated data values.</returns>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static double[] GenerateGeneralizedLogisticData()
    {
        var dist = new GeneralizedLogistic(75.0, 10.0, 0.15);
        return dist.GenerateRandomValues(SampleSize, Seed);
    }

    #endregion

    #region Time Series Data

    /// <summary>
    /// Synthetic AR(1) time series data with φ=0.7.
    /// </summary>
    public static double[] AR1Data { get; } = GenerateAR1Data();

    /// <summary>
    /// True parameters for AR1Data: [μ, φ₁, σ].
    /// </summary>
    public static double[] AR1TrueParams { get; } = [50.0, 0.7, 5.0];

    /// <summary>
    /// Generates aR1 Data.
    /// </summary>
    /// <returns>The generated data values.</returns>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static double[] GenerateAR1Data()
    {
        var rng = new MersenneTwister(Seed);
        var noise = new Normal(0, 5.0);
        double[] data = new double[SampleSize];
        double mu = 50.0;
        double phi = 0.7;

        // Initialize
        data[0] = mu + noise.InverseCDF(rng.NextDouble());

        // Generate AR(1) process: X_t = mu + phi*(X_{t-1} - mu) + e_t
        for (int i = 1; i < SampleSize; i++)
        {
            data[i] = mu + phi * (data[i - 1] - mu) + noise.InverseCDF(rng.NextDouble());
        }

        return data;
    }

    /// <summary>
    /// Synthetic AR(2) time series data with φ₁=0.5, φ₂=0.3.
    /// </summary>
    public static double[] AR2Data { get; } = GenerateAR2Data();

    /// <summary>
    /// True parameters for AR2Data: [μ, φ₁, φ₂, σ].
    /// </summary>
    public static double[] AR2TrueParams { get; } = [50.0, 0.5, 0.3, 5.0];

    /// <summary>
    /// Generates aR2 Data.
    /// </summary>
    /// <returns>The generated data values.</returns>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static double[] GenerateAR2Data()
    {
        var rng = new MersenneTwister(Seed + 1);
        var noise = new Normal(0, 5.0);
        double[] data = new double[SampleSize];
        double mu = 50.0;
        double phi1 = 0.5;
        double phi2 = 0.3;

        // Initialize
        data[0] = mu + noise.InverseCDF(rng.NextDouble());
        data[1] = mu + phi1 * (data[0] - mu) + noise.InverseCDF(rng.NextDouble());

        // Generate AR(2) process
        for (int i = 2; i < SampleSize; i++)
        {
            data[i] = mu + phi1 * (data[i - 1] - mu) + phi2 * (data[i - 2] - mu) + noise.InverseCDF(rng.NextDouble());
        }

        return data;
    }

    /// <summary>
    /// Synthetic MA(1) time series data with θ=0.6.
    /// </summary>
    public static double[] MA1Data { get; } = GenerateMA1Data();

    /// <summary>
    /// True parameters for MA1Data: [μ, θ₁, σ].
    /// </summary>
    public static double[] MA1TrueParams { get; } = [50.0, 0.6, 5.0];

    /// <summary>
    /// Generates mA1 Data.
    /// </summary>
    /// <returns>The generated data values.</returns>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static double[] GenerateMA1Data()
    {
        var rng = new MersenneTwister(Seed + 2);
        var noise = new Normal(0, 5.0);
        double[] errors = new double[SampleSize + 1];
        double[] data = new double[SampleSize];
        double mu = 50.0;
        double theta = 0.6;

        // Generate errors
        for (int i = 0; i <= SampleSize; i++)
        {
            errors[i] = noise.InverseCDF(rng.NextDouble());
        }

        // Generate MA(1) process: X_t = mu + e_t + theta*e_{t-1}
        for (int i = 0; i < SampleSize; i++)
        {
            data[i] = mu + errors[i + 1] + theta * errors[i];
        }

        return data;
    }

    /// <summary>
    /// Synthetic ARMA(1,1) time series data with φ=0.5, θ=0.4.
    /// </summary>
    public static double[] ARMA11Data { get; } = GenerateARMA11Data();

    /// <summary>
    /// True parameters for ARMA11Data: [μ, φ₁, θ₁, σ].
    /// </summary>
    public static double[] ARMA11TrueParams { get; } = [50.0, 0.5, 0.4, 5.0];

    /// <summary>
    /// Generates aRMA11 Data.
    /// </summary>
    /// <returns>The generated data values.</returns>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static double[] GenerateARMA11Data()
    {
        var rng = new MersenneTwister(Seed + 3);
        var noise = new Normal(0, 5.0);
        double[] errors = new double[SampleSize + 1];
        double[] data = new double[SampleSize];
        double mu = 50.0;
        double phi = 0.5;
        double theta = 0.4;

        // Generate errors
        for (int i = 0; i <= SampleSize; i++)
        {
            errors[i] = noise.InverseCDF(rng.NextDouble());
        }

        // Initialize
        data[0] = mu + errors[1] + theta * errors[0];

        // Generate ARMA(1,1) process: X_t = mu + phi*(X_{t-1} - mu) + e_t + theta*e_{t-1}
        for (int i = 1; i < SampleSize; i++)
        {
            data[i] = mu + phi * (data[i - 1] - mu) + errors[i + 1] + theta * errors[i];
        }

        return data;
    }

    #endregion

    #region Bivariate Data

    /// <summary>
    /// Synthetic bivariate X data from Normal(100, 15).
    /// </summary>
    public static double[] BivariateXData { get; } = GenerateBivariateXData();

    /// <summary>
    /// Synthetic bivariate Y data correlated with X using Gaussian copula (ρ=0.7).
    /// Marginal: Gumbel(50, 15).
    /// </summary>
    public static double[] BivariateYData { get; } = GenerateBivariateYData();

    /// <summary>
    /// True Pearson correlation for bivariate data.
    /// </summary>
    public const double BivariateTrueCorrelation = 0.7;

    /// <summary>
    /// Generates bivariate X Data.
    /// </summary>
    /// <returns>The generated data values.</returns>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static double[] GenerateBivariateXData()
    {
        var dist = new Normal(100.0, 15.0);
        return dist.GenerateRandomValues(SampleSize, Seed + 200);
    }

    /// <summary>
    /// Generates bivariate Y Data.
    /// </summary>
    /// <returns>The generated data values.</returns>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static double[] GenerateBivariateYData()
    {
        var rng = new MersenneTwister(Seed + 201);
        var normalX = new Normal();
        var gumbelY = new Gumbel(50.0, 15.0);
        double rho = BivariateTrueCorrelation;

        // Generate correlated normals then transform Y marginal
        var z1 = new Normal(0, 1);
        double[] yData = new double[SampleSize];

        for (int i = 0; i < SampleSize; i++)
        {
            // Get X in standard normal space
            double u1 = normalX.CDF(BivariateXData[i]);
            double z1Val = Normal.StandardZ(u1);

            // Generate correlated Z2
            double z2Independent = z1.InverseCDF(rng.NextDouble());
            double z2Val = rho * z1Val + Math.Sqrt(1 - rho * rho) * z2Independent;

            // Transform to Gumbel marginal
            double u2 = normalX.CDF(z2Val);
            yData[i] = gumbelY.InverseCDF(u2);
        }

        return yData;
    }

    #endregion

    #region Small Samples for Edge Case Testing

    /// <summary>
    /// Very small sample (n=10) for edge case testing.
    /// </summary>
    public static double[] SmallSample { get; } = GenerateSmallSample();

    /// <summary>
    /// Generates small Sample.
    /// </summary>
    /// <returns>The generated data values.</returns>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static double[] GenerateSmallSample()
    {
        var dist = new Normal(100.0, 15.0);
        return dist.GenerateRandomValues(10, Seed);
    }

    /// <summary>
    /// Sample with outliers for robust estimation testing.
    /// </summary>
    public static double[] DataWithOutliers { get; } = GenerateDataWithOutliers();

    /// <summary>
    /// Generates data With Outliers.
    /// </summary>
    /// <returns>The generated data values.</returns>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static double[] GenerateDataWithOutliers()
    {
        var dist = new Normal(100.0, 15.0);
        double[] data = dist.GenerateRandomValues(100, Seed);

        // Add some outliers (5% contamination)
        data[0] = 200.0;   // High outlier
        data[1] = 250.0;   // High outlier
        data[2] = 10.0;    // Low outlier
        data[3] = -20.0;   // Low outlier
        data[4] = 300.0;   // Extreme high outlier

        return data;
    }

    /// <summary>
    /// Data with some negative values for testing distributions with bounded support.
    /// </summary>
    public static double[] DataWithNegatives { get; } = GenerateDataWithNegatives();

    /// <summary>
    /// Generates data With Negatives.
    /// </summary>
    /// <returns>The generated data values.</returns>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static double[] GenerateDataWithNegatives()
    {
        var dist = new Normal(10.0, 20.0);
        return dist.GenerateRandomValues(100, Seed);
    }

    #endregion

    #region Censored Data

    /// <summary>
    /// Complete data for generating censored versions.
    /// GEV(50, 15, 0.1) distribution.
    /// </summary>
    public static double[] CensoredBaseData { get; } = GEVData;

    /// <summary>
    /// Left-censoring threshold (observations below this are censored).
    /// </summary>
    public const double LeftCensorThreshold = 30.0;

    /// <summary>
    /// Right-censoring threshold (observations above this are censored).
    /// </summary>
    public const double RightCensorThreshold = 100.0;

    /// <summary>
    /// Count of observations below the left-censoring threshold.
    /// </summary>
    public static int LeftCensoredCount => CensoredBaseData.Count(x => x < LeftCensorThreshold);

    /// <summary>
    /// Count of observations above the right-censoring threshold.
    /// </summary>
    public static int RightCensoredCount => CensoredBaseData.Count(x => x > RightCensorThreshold);

    /// <summary>
    /// Exact observations (between thresholds).
    /// </summary>
    public static double[] ExactObservations => CensoredBaseData
        .Where(x => x >= LeftCensorThreshold && x <= RightCensorThreshold)
        .ToArray();

    #endregion

    #region Mixture Model Data

    /// <summary>
    /// Synthetic mixture data from 70% N(50, 10) + 30% N(100, 15).
    /// </summary>
    public static double[] MixtureData { get; } = GenerateMixtureData();

    /// <summary>
    /// True mixing weight for first component.
    /// </summary>
    public const double MixtureTrueWeight1 = 0.7;

    /// <summary>
    /// Generates mixture Data.
    /// </summary>
    /// <returns>The generated data values.</returns>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static double[] GenerateMixtureData()
    {
        var rng = new MersenneTwister(Seed + 300);
        var dist1 = new Normal(50.0, 10.0);
        var dist2 = new Normal(100.0, 15.0);
        double[] data = new double[SampleSize];

        for (int i = 0; i < SampleSize; i++)
        {
            if (rng.NextDouble() < MixtureTrueWeight1)
            {
                data[i] = dist1.InverseCDF(rng.NextDouble());
            }
            else
            {
                data[i] = dist2.InverseCDF(rng.NextDouble());
            }
        }

        return data;
    }

    #endregion

    #region Point Process Data

    /// <summary>
    /// Synthetic event times for point process testing (Poisson process with λ=10 events/year).
    /// </summary>
    public static double[] PointProcessEventTimes { get; } = GeneratePointProcessEventTimes();

    /// <summary>
    /// True rate parameter for point process (events per year).
    /// </summary>
    public const double PointProcessTrueRate = 10.0;

    /// <summary>
    /// Total observation period in years.
    /// </summary>
    public const double PointProcessObservationPeriod = 100.0;

    /// <summary>
    /// Generates point Process Event Times.
    /// </summary>
    /// <returns>The generated data values.</returns>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static double[] GeneratePointProcessEventTimes()
    {
        var rng = new MersenneTwister(Seed + 400);
        var exponential = new Exponential(0, 1.0 / PointProcessTrueRate);
        var events = new List<double>();
        double currentTime = 0;

        while (currentTime < PointProcessObservationPeriod)
        {
            double interarrival = exponential.InverseCDF(rng.NextDouble());
            currentTime += interarrival;
            if (currentTime < PointProcessObservationPeriod)
            {
                events.Add(currentTime);
            }
        }

        return events.ToArray();
    }

    /// <summary>
    /// Synthetic event magnitudes (GEV distributed) for point process testing.
    /// </summary>
    public static double[] PointProcessEventMagnitudes { get; } = GeneratePointProcessEventMagnitudes();

    /// <summary>
    /// Generates point Process Event Magnitudes.
    /// </summary>
    /// <returns>The generated data values.</returns>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static double[] GeneratePointProcessEventMagnitudes()
    {
        var dist = new GeneralizedExtremeValue(50.0, 15.0, 0.1);
        return dist.GenerateRandomValues(PointProcessEventTimes.Length, Seed + 401);
    }

    #endregion

    #region Spatial Data

    /// <summary>
    /// Synthetic X coordinates for spatial analysis.
    /// </summary>
    public static double[] SpatialXCoordinates { get; } = GenerateSpatialXCoordinates();

    /// <summary>
    /// Synthetic Y coordinates for spatial analysis.
    /// </summary>
    public static double[] SpatialYCoordinates { get; } = GenerateSpatialYCoordinates();

    /// <summary>
    /// Number of spatial sites.
    /// </summary>
    public const int SpatialSiteCount = 20;

    /// <summary>
    /// Years of data at each site.
    /// </summary>
    public const int SpatialYearsPerSite = 50;

    /// <summary>
    /// Generates spatial X Coordinates.
    /// </summary>
    /// <returns>The generated data values.</returns>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static double[] GenerateSpatialXCoordinates()
    {
        var rng = new MersenneTwister(Seed + 500);
        double[] coords = new double[SpatialSiteCount];
        for (int i = 0; i < SpatialSiteCount; i++)
        {
            coords[i] = rng.NextDouble() * 100.0; // 0-100 km
        }
        return coords;
    }

    /// <summary>
    /// Generates spatial Y Coordinates.
    /// </summary>
    /// <returns>The generated data values.</returns>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static double[] GenerateSpatialYCoordinates()
    {
        var rng = new MersenneTwister(Seed + 501);
        double[] coords = new double[SpatialSiteCount];
        for (int i = 0; i < SpatialSiteCount; i++)
        {
            coords[i] = rng.NextDouble() * 100.0; // 0-100 km
        }
        return coords;
    }

    /// <summary>
    /// Synthetic spatial GEV data at each site (SpatialSiteCount x SpatialYearsPerSite).
    /// GEV parameters vary spatially with location parameter increasing with X coordinate.
    /// </summary>
    public static double[,] SpatialGEVData { get; } = GenerateSpatialGEVData();

    /// <summary>
    /// Generates spatial GEV Data.
    /// </summary>
    /// <returns>The generated data values.</returns>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static double[,] GenerateSpatialGEVData()
    {
        var rng = new MersenneTwister(Seed + 502);
        double[,] data = new double[SpatialSiteCount, SpatialYearsPerSite];

        for (int site = 0; site < SpatialSiteCount; site++)
        {
            // Location increases with X coordinate
            double xi = 50.0 + 0.5 * SpatialXCoordinates[site];
            double alpha = 15.0;
            double kappa = 0.1;

            var dist = new GeneralizedExtremeValue(xi, alpha, kappa);

            for (int year = 0; year < SpatialYearsPerSite; year++)
            {
                data[site, year] = dist.InverseCDF(rng.NextDouble());
            }
        }

        return data;
    }

    #endregion
}
