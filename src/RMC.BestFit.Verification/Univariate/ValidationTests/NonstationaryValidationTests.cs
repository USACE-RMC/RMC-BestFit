using Numerics.Distributions;
using RMC.BestFit.Analyses;
using RMC.BestFit.Models;
using RMC.BestFit.Models.TrendFunctions.Support;
using RMC.BestFit.Verification.Datasets.UnivariateData;

namespace RMC.BestFit.Verification.Univariate.ValidationTests;

/// <summary>
/// Validation tests for nonstationary univariate distribution analysis using synthetic data.
/// </summary>
/// <remarks>
/// <para>
///     <b>Authors:</b>
///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
/// </para>
/// <para>
///     <b>Purpose:</b>
///     These tests validate that the Bayesian estimation framework correctly recovers
///     known parameters from synthetic nonstationary data. Each test generates data from
///     a distribution with known trend parameters, then verifies that the posterior mode
///     estimates are within 1% tolerance of the true values.
/// </para>
/// <para>
///     <b>Test Configuration:</b>
///     All tests use 1000 synthetic samples, DEMCzs sampler with 10000 iterations,
///     5000 warmup iterations, and posterior mode as the point estimator.
///     The 1% tolerance is relative to the true parameter value.
/// </para>
/// </remarks>
[TestClass]
public class NonstationaryValidationTests
{
    /// <summary>
    /// The relative tolerance for parameter recovery (1%).
    /// </summary>
    private const double RelativeTolerance = 0.01;

    /// <summary>
    /// Sample size for synthetic data generation.
    /// </summary>
    private const int SampleSize = 1000;

    /// <summary>
    /// Number of MCMC iterations.
    /// </summary>
    private const int Iterations = 10000;

    /// <summary>
    /// Number of warmup iterations.
    /// </summary>
    private const int WarmupIterations = 5000;

    #region Trend on Mean Only

    /// <summary>
    /// Tests parameter recovery for a Normal distribution with constant mean (stationary baseline).
    /// </summary>
    [TestMethod]
    public async Task Nonstationary_ConstantTrend_RecoversTrueParameters()
    {
        // Arrange
        var (df, trueParams) = SyntheticNonstationaryData.GenerateConstantTrendData(SampleSize);

        var model = new UnivariateDistribution(df, UnivariateDistributionType.Normal);
        model.IsNonstationary = true;
        model.SetTrendModel(0, TrendModelType.Constant);

        var analysis = new UnivariateAnalysis(model);
        ConfigureBayesianAnalysis(analysis);

        // Act
        await analysis.RunAsync();

        // Assert
        Assert.IsTrue(analysis.IsEstimated, "Analysis failed to complete.");
        AssertParametersWithinTolerance(model, trueParams, analysis, "ConstantTrend");
    }

    /// <summary>
    /// Tests parameter recovery for a Normal distribution with linear trend on mean.
    /// </summary>
    [TestMethod]
    public async Task Nonstationary_LinearTrend_RecoversTrueParameters()
    {
        // Arrange
        var (df, trueParams) = SyntheticNonstationaryData.GenerateLinearTrendData(SampleSize);

        var model = new UnivariateDistribution(df, UnivariateDistributionType.Normal);
        model.IsNonstationary = true;
        model.SetTrendModel(0, TrendModelType.Linear);

        var analysis = new UnivariateAnalysis(model);
        ConfigureBayesianAnalysis(analysis);

        // Act
        await analysis.RunAsync();

        // Assert
        Assert.IsTrue(analysis.IsEstimated, "Analysis failed to complete.");
        AssertParametersWithinTolerance(model, trueParams, analysis, "LinearTrend");
    }

    /// <summary>
    /// Tests parameter recovery for a Normal distribution with quadratic trend on mean.
    /// </summary>
    [TestMethod]
    public async Task Nonstationary_QuadraticTrend_RecoversTrueParameters()
    {
        // Arrange
        var (df, trueParams) = SyntheticNonstationaryData.GenerateQuadraticTrendData(SampleSize);

        var model = new UnivariateDistribution(df, UnivariateDistributionType.Normal);
        model.IsNonstationary = true;
        model.SetTrendModel(0, TrendModelType.Quadratic);

        var analysis = new UnivariateAnalysis(model);
        ConfigureBayesianAnalysis(analysis);

        // Act
        await analysis.RunAsync();

        // Assert
        Assert.IsTrue(analysis.IsEstimated, "Analysis failed to complete.");
        AssertParametersWithinTolerance(model, trueParams, analysis, "QuadraticTrend");
    }

    /// <summary>
    /// Tests parameter recovery for a Normal distribution with cubic trend on mean.
    /// </summary>
    [TestMethod]
    public async Task Nonstationary_CubicTrend_RecoversTrueParameters()
    {
        // Arrange
        var (df, trueParams) = SyntheticNonstationaryData.GenerateCubicTrendData(SampleSize);

        var model = new UnivariateDistribution(df, UnivariateDistributionType.Normal);
        model.IsNonstationary = true;
        model.SetTrendModel(0, TrendModelType.Cubic);

        var analysis = new UnivariateAnalysis(model);
        ConfigureBayesianAnalysis(analysis);

        // Act
        await analysis.RunAsync();

        // Assert
        Assert.IsTrue(analysis.IsEstimated, "Analysis failed to complete.");
        AssertParametersWithinTolerance(model, trueParams, analysis, "CubicTrend");
    }

    /// <summary>
    /// Tests parameter recovery for a Normal distribution with exponential trend on mean.
    /// </summary>
    [TestMethod]
    public async Task Nonstationary_ExponentialTrend_RecoversTrueParameters()
    {
        // Arrange
        var (df, trueParams) = SyntheticNonstationaryData.GenerateExponentialTrendData(SampleSize);

        var model = new UnivariateDistribution(df, UnivariateDistributionType.Normal);
        model.IsNonstationary = true;
        model.SetTrendModel(0, TrendModelType.Exponential);

        var analysis = new UnivariateAnalysis(model);
        ConfigureBayesianAnalysis(analysis);

        // Act
        await analysis.RunAsync();

        // Assert
        Assert.IsTrue(analysis.IsEstimated, "Analysis failed to complete.");
        AssertParametersWithinTolerance(model, trueParams, analysis, "ExponentialTrend");
    }

    /// <summary>
    /// Tests parameter recovery for a Normal distribution with logistic trend on mean.
    /// </summary>
    [TestMethod]
    public async Task Nonstationary_LogisticTrend_RecoversTrueParameters()
    {
        // Arrange
        var (df, trueParams) = SyntheticNonstationaryData.GenerateLogisticTrendData(SampleSize);

        var model = new UnivariateDistribution(df, UnivariateDistributionType.Normal);
        model.IsNonstationary = true;
        model.SetTrendModel(0, TrendModelType.Logistic);

        var analysis = new UnivariateAnalysis(model);
        ConfigureBayesianAnalysis(analysis);

        // Act
        await analysis.RunAsync();

        // Assert
        Assert.IsTrue(analysis.IsEstimated, "Analysis failed to complete.");
        AssertParametersWithinTolerance(model, trueParams, analysis, "LogisticTrend");
    }

    /// <summary>
    /// Tests parameter recovery for a Normal distribution with power trend on mean.
    /// </summary>
    [TestMethod]
    public async Task Nonstationary_PowerTrend_RecoversTrueParameters()
    {
        // Arrange
        var (df, trueParams) = SyntheticNonstationaryData.GeneratePowerTrendData(SampleSize);

        var model = new UnivariateDistribution(df, UnivariateDistributionType.Normal);
        model.IsNonstationary = true;
        model.SetTrendModel(0, TrendModelType.Power);

        var analysis = new UnivariateAnalysis(model);
        ConfigureBayesianAnalysis(analysis);

        // Act
        await analysis.RunAsync();

        // Assert
        Assert.IsTrue(analysis.IsEstimated, "Analysis failed to complete.");
        AssertParametersWithinTolerance(model, trueParams, analysis, "PowerTrend");
    }

    /// <summary>
    /// Tests parameter recovery for a Normal distribution with sinusoidal trend on mean.
    /// </summary>
    [TestMethod]
    public async Task Nonstationary_SinusoidalTrend_RecoversTrueParameters()
    {
        // Arrange
        var (df, trueParams) = SyntheticNonstationaryData.GenerateSinusoidalTrendData(SampleSize);

        var model = new UnivariateDistribution(df, UnivariateDistributionType.Normal);
        model.IsNonstationary = true;
        model.SetTrendModel(0, TrendModelType.Sinusoidal);

        var analysis = new UnivariateAnalysis(model);
        ConfigureBayesianAnalysis(analysis);

        // Act
        await analysis.RunAsync();

        // Assert
        Assert.IsTrue(analysis.IsEstimated, "Analysis failed to complete.");
        AssertParametersWithinTolerance(model, trueParams, analysis, "SinusoidalTrend");
    }

    /// <summary>
    /// Tests parameter recovery for a Normal distribution with step function trend on mean.
    /// </summary>
    [TestMethod]
    public async Task Nonstationary_StepFunctionTrend_RecoversTrueParameters()
    {
        // Arrange
        var (df, trueParams) = SyntheticNonstationaryData.GenerateStepFunctionTrendData(SampleSize);

        var model = new UnivariateDistribution(df, UnivariateDistributionType.Normal);
        model.IsNonstationary = true;
        model.SetTrendModel(0, TrendModelType.StepFunction);

        var analysis = new UnivariateAnalysis(model);
        ConfigureBayesianAnalysis(analysis);

        // Act
        await analysis.RunAsync();

        // Assert
        Assert.IsTrue(analysis.IsEstimated, "Analysis failed to complete.");
        AssertParametersWithinTolerance(model, trueParams, analysis, "StepFunctionTrend");
    }

    #endregion

    #region Trend on Standard Deviation Only

    /// <summary>
    /// Tests parameter recovery for a Normal distribution with linear trend on standard deviation only.
    /// </summary>
    [TestMethod]
    public async Task Nonstationary_SigmaLinearTrend_RecoversTrueParameters()
    {
        // Arrange
        var (df, trueParams) = SyntheticNonstationaryData.GenerateSigmaLinearTrendData(SampleSize);

        var model = new UnivariateDistribution(df, UnivariateDistributionType.Normal);
        model.IsNonstationary = true;
        model.SetTrendModel(1, TrendModelType.Linear);

        var analysis = new UnivariateAnalysis(model);
        ConfigureBayesianAnalysis(analysis);

        // Act
        await analysis.RunAsync();

        // Assert
        Assert.IsTrue(analysis.IsEstimated, "Analysis failed to complete.");
        AssertParametersWithinTolerance(model, trueParams, analysis, "SigmaLinearTrend");
    }

    /// <summary>
    /// Tests parameter recovery for a Normal distribution with quadratic trend on standard deviation only.
    /// </summary>
    [TestMethod]
    public async Task Nonstationary_SigmaQuadraticTrend_RecoversTrueParameters()
    {
        // Arrange
        var (df, trueParams) = SyntheticNonstationaryData.GenerateSigmaQuadraticTrendData(SampleSize);

        var model = new UnivariateDistribution(df, UnivariateDistributionType.Normal);
        model.IsNonstationary = true;
        model.SetTrendModel(1, TrendModelType.Quadratic);

        var analysis = new UnivariateAnalysis(model);
        ConfigureBayesianAnalysis(analysis);

        // Act
        await analysis.RunAsync();

        // Assert
        Assert.IsTrue(analysis.IsEstimated, "Analysis failed to complete.");
        AssertParametersWithinTolerance(model, trueParams, analysis, "SigmaQuadraticTrend");
    }

    /// <summary>
    /// Tests parameter recovery for a Normal distribution with exponential trend on standard deviation only.
    /// </summary>
    [TestMethod]
    public async Task Nonstationary_SigmaExponentialTrend_RecoversTrueParameters()
    {
        // Arrange
        var (df, trueParams) = SyntheticNonstationaryData.GenerateSigmaExponentialTrendData(SampleSize);

        var model = new UnivariateDistribution(df, UnivariateDistributionType.Normal);
        model.IsNonstationary = true;
        model.SetTrendModel(1, TrendModelType.Exponential);

        var analysis = new UnivariateAnalysis(model);
        ConfigureBayesianAnalysis(analysis);

        // Act
        await analysis.RunAsync();

        // Assert
        Assert.IsTrue(analysis.IsEstimated, "Analysis failed to complete.");
        AssertParametersWithinTolerance(model, trueParams, analysis, "SigmaExponentialTrend");
    }

    #endregion

    #region Trend on Both Mean and Standard Deviation

    /// <summary>
    /// Tests parameter recovery for a Normal distribution with linear trends on both mean and standard deviation.
    /// </summary>
    [TestMethod]
    public async Task Nonstationary_BothLinearTrend_RecoversTrueParameters()
    {
        // Arrange
        var (df, trueParams) = SyntheticNonstationaryData.GenerateBothLinearTrendData(SampleSize);

        var model = new UnivariateDistribution(df, UnivariateDistributionType.Normal);
        model.IsNonstationary = true;
        model.SetTrendModel(0, TrendModelType.Linear);
        model.SetTrendModel(1, TrendModelType.Linear);

        var analysis = new UnivariateAnalysis(model);
        ConfigureBayesianAnalysis(analysis);

        // Act
        await analysis.RunAsync();

        // Assert
        Assert.IsTrue(analysis.IsEstimated, "Analysis failed to complete.");
        AssertParametersWithinTolerance(model, trueParams, analysis, "BothLinearTrend");
    }

    /// <summary>
    /// Tests parameter recovery for a Normal distribution with quadratic trend on mean and linear trend on standard deviation.
    /// </summary>
    [TestMethod]
    public async Task Nonstationary_MuQuadraticSigmaLinear_RecoversTrueParameters()
    {
        // Arrange
        var (df, trueParams) = SyntheticNonstationaryData.GenerateMuQuadraticSigmaLinearTrendData(SampleSize);

        var model = new UnivariateDistribution(df, UnivariateDistributionType.Normal);
        model.IsNonstationary = true;
        model.SetTrendModel(0, TrendModelType.Quadratic);
        model.SetTrendModel(1, TrendModelType.Linear);

        var analysis = new UnivariateAnalysis(model);
        ConfigureBayesianAnalysis(analysis);

        // Act
        await analysis.RunAsync();

        // Assert
        Assert.IsTrue(analysis.IsEstimated, "Analysis failed to complete.");
        AssertParametersWithinTolerance(model, trueParams, analysis, "MuQuadraticSigmaLinear");
    }

    /// <summary>
    /// Tests parameter recovery for a Normal distribution with linear trend on mean and exponential trend on standard deviation.
    /// </summary>
    [TestMethod]
    public async Task Nonstationary_MuLinearSigmaExponential_RecoversTrueParameters()
    {
        // Arrange
        var (df, trueParams) = SyntheticNonstationaryData.GenerateMuLinearSigmaExponentialTrendData(SampleSize);

        var model = new UnivariateDistribution(df, UnivariateDistributionType.Normal);
        model.IsNonstationary = true;
        model.SetTrendModel(0, TrendModelType.Linear);
        model.SetTrendModel(1, TrendModelType.Exponential);

        var analysis = new UnivariateAnalysis(model);
        ConfigureBayesianAnalysis(analysis);

        // Act
        await analysis.RunAsync();

        // Assert
        Assert.IsTrue(analysis.IsEstimated, "Analysis failed to complete.");
        AssertParametersWithinTolerance(model, trueParams, analysis, "MuLinearSigmaExponential");
    }

    /// <summary>
    /// Tests parameter recovery for a Normal distribution with step function trends on both mean and standard deviation.
    /// </summary>
    [TestMethod]
    public async Task Nonstationary_BothStepFunction_RecoversTrueParameters()
    {
        // Arrange
        var (df, trueParams) = SyntheticNonstationaryData.GenerateBothStepFunctionTrendData(SampleSize);

        var model = new UnivariateDistribution(df, UnivariateDistributionType.Normal);
        model.IsNonstationary = true;
        model.SetTrendModel(0, TrendModelType.StepFunction);
        model.SetTrendModel(1, TrendModelType.StepFunction);

        var analysis = new UnivariateAnalysis(model);
        ConfigureBayesianAnalysis(analysis);

        // Act
        await analysis.RunAsync();

        // Assert
        Assert.IsTrue(analysis.IsEstimated, "Analysis failed to complete.");
        AssertParametersWithinTolerance(model, trueParams, analysis, "BothStepFunction");
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Configures the Bayesian analysis settings for consistent testing.
    /// </summary>
    /// <param name="analysis">The univariate analysis to configure.</param>
    private static void ConfigureBayesianAnalysis(UnivariateAnalysis analysis)
    {
        analysis.BayesianAnalysis.Iterations = Iterations;
        analysis.BayesianAnalysis.WarmupIterations = WarmupIterations;
        analysis.BayesianAnalysis.Type = RMC.BestFit.Estimation.BayesianAnalysis.SamplerType.DEMCzs;
        analysis.BayesianAnalysis.PointEstimator = RMC.BestFit.Estimation.BayesianAnalysis.PointEstimateType.PosteriorMode;
    }

    /// <summary>
    /// Asserts that all estimated parameters are within the specified relative tolerance of the true values.
    /// </summary>
    /// <param name="model">The univariate distribution model.</param>
    /// <param name="trueParams">The true parameter values used for data generation.</param>
    /// <param name="analysis">The completed analysis.</param>
    /// <param name="testName">The name of the test for error messages.</param>
    private static void AssertParametersWithinTolerance(
        UnivariateDistribution model,
        double[] trueParams,
        UnivariateAnalysis analysis,
        string testName)
    {
        // Get the MAP (posterior mode) parameter estimates
        var mapValues = analysis.BayesianAnalysis.Results!.MAP.Values;

        Assert.AreEqual(trueParams.Length, mapValues.Length,
            $"{testName}: Parameter count mismatch. Expected {trueParams.Length}, got {mapValues.Length}.");

        for (int i = 0; i < trueParams.Length; i++)
        {
            double trueValue = trueParams[i];
            double estimated = mapValues[i];

            // Calculate tolerance based on the magnitude of the true value
            // Use absolute tolerance for values near zero
            double tolerance = Math.Abs(trueValue) > 1e-6
                ? Math.Abs(trueValue * RelativeTolerance)
                : RelativeTolerance;

            string paramName = i < model.Parameters.Count()
                ? model.Parameters.ElementAt(i).Name
                : $"Parameter[{i}]";

            Assert.AreEqual(trueValue, estimated, tolerance,
                $"{testName}: {paramName} - Expected {trueValue:F6}, got {estimated:F6}, " +
                $"tolerance {tolerance:F6} ({RelativeTolerance * 100}%).");
        }
    }

    #endregion
}
