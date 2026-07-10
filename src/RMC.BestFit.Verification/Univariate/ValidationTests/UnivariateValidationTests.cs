using Numerics.Distributions;
using RMC.BestFit.Analyses;
using RMC.BestFit.Models;
using RMC.BestFit.Verification.Datasets.UnivariateData;

namespace RMC.BestFit.Verification.Univariate.ValidationTests;

/// <summary>
/// Validation tests for all 15 univariate distributions using synthetic data.
/// </summary>
/// <remarks>
/// <para>
///     <b>Authors:</b>
///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
/// </para>
/// <para>
///     <b>Purpose:</b>
///     These tests validate that the Bayesian estimation framework correctly recovers
///     known parameters for all 15 supported univariate distributions. Each test generates
///     synthetic data from a distribution with known parameters, then verifies that the
///     posterior mode estimates are within 1% tolerance of the true values.
/// </para>
/// <para>
///     <b>Supported Distributions:</b>
///     <list type="number">
///         <item>Exponential</item>
///         <item>Gamma</item>
///         <item>Generalized Extreme Value (GEV)</item>
///         <item>Generalized Logistic</item>
///         <item>Generalized Normal</item>
///         <item>Generalized Pareto</item>
///         <item>Gumbel</item>
///         <item>Kappa Four</item>
///         <item>Ln-Normal</item>
///         <item>Logistic</item>
///         <item>Log-Normal</item>
///         <item>Log-Pearson Type III</item>
///         <item>Normal</item>
///         <item>Pearson Type III</item>
///         <item>Weibull</item>
///     </list>
/// </para>
/// <para>
///     <b>Test Configuration:</b>
///     All tests use 1000 synthetic samples, DEMCzs sampler with 10000 iterations,
///     5000 warmup iterations, and posterior mode as the point estimator.
///     The 1% tolerance is relative to the true parameter value.
/// </para>
/// </remarks>
[TestClass]
public class UnivariateValidationTests
{
    /// <summary>
    /// The relative tolerance for parameter recovery (10%).
    /// </summary>
    private const double RelativeTolerance = 0.1;

    /// <summary>
    /// Sample size for synthetic data generation.
    /// </summary>
    private const int SampleSize = 300;


    #region Exponential Family

    /// <summary>
    /// Tests parameter recovery for the Exponential distribution.
    /// </summary>
    /// <remarks>
    /// The Exponential distribution has 2 parameters: location (Xi) and scale (Alpha).
    /// </remarks>
    [TestMethod]
    public async Task Exponential_RecoversTrueParameters()
    {
        // Arrange
        var (df, trueParams) = SyntheticUnivariateData.GenerateExponentialData(n: SampleSize);

        var model = new UnivariateDistribution(df, UnivariateDistributionType.Exponential);
        var analysis = new UnivariateAnalysis(model);
        ConfigureBayesianAnalysis(analysis);

        // Act
        await analysis.RunAsync();

        // Assert
        Assert.IsTrue(analysis.IsEstimated, "Analysis failed to complete.");
        AssertParametersWithinTolerance(model, trueParams, analysis, "Exponential");
    }

    /// <summary>
    /// Tests parameter recovery for the Gamma distribution.
    /// </summary>
    /// <remarks>
    /// The Gamma distribution has 2 parameters: shape (Alpha) and rate (Beta).
    /// </remarks>
    [TestMethod]
    public async Task Gamma_RecoversTrueParameters()
    {
        // Arrange
        var (df, trueParams) = SyntheticUnivariateData.GenerateGammaData(n: SampleSize);

        var model = new UnivariateDistribution(df, UnivariateDistributionType.GammaDistribution);
        var analysis = new UnivariateAnalysis(model);
        ConfigureBayesianAnalysis(analysis);

        // Act
        await analysis.RunAsync();

        // Assert
        Assert.IsTrue(analysis.IsEstimated, "Analysis failed to complete.");
        AssertParametersWithinTolerance(model, trueParams, analysis, "Gamma");
    }

    /// <summary>
    /// Tests parameter recovery for the Weibull distribution.
    /// </summary>
    /// <remarks>
    /// The Weibull distribution (2-parameter reliability version) has 2 parameters: scale (Lambda) and shape (Kappa).
    /// </remarks>
    [TestMethod]
    public async Task Weibull_RecoversTrueParameters()
    {
        // Arrange
        var (df, trueParams) = SyntheticUnivariateData.GenerateWeibullData(n: SampleSize);

        var model = new UnivariateDistribution(df, UnivariateDistributionType.Weibull);
        var analysis = new UnivariateAnalysis(model);
        ConfigureBayesianAnalysis(analysis);

        // Act
        await analysis.RunAsync();

        // Assert
        Assert.IsTrue(analysis.IsEstimated, "Analysis failed to complete.");
        AssertParametersWithinTolerance(model, trueParams, analysis, "Weibull");
    }

    #endregion

    #region Normal Family

    /// <summary>
    /// Tests parameter recovery for the Normal distribution.
    /// </summary>
    /// <remarks>
    /// The Normal distribution has 2 parameters: mean (Mu) and standard deviation (Sigma).
    /// </remarks>
    [TestMethod]
    public async Task Normal_RecoversTrueParameters()
    {
        // Arrange
        var (df, trueParams) = SyntheticUnivariateData.GenerateNormalData(n: SampleSize);

        var model = new UnivariateDistribution(df, UnivariateDistributionType.Normal);
        var analysis = new UnivariateAnalysis(model);
        ConfigureBayesianAnalysis(analysis);

        // Act
        await analysis.RunAsync();

        // Assert
        Assert.IsTrue(analysis.IsEstimated, "Analysis failed to complete.");
        AssertParametersWithinTolerance(model, trueParams, analysis, "Normal");
    }

    /// <summary>
    /// Tests parameter recovery for the Generalized Normal distribution.
    /// </summary>
    /// <remarks>
    /// The Generalized Normal distribution has 3 parameters: location (Xi), scale (Alpha), and shape (Kappa).
    /// </remarks>
    [TestMethod]
    public async Task GeneralizedNormal_RecoversTrueParameters()
    {
        // Arrange
        var (df, trueParams) = SyntheticUnivariateData.GenerateGeneralizedNormalData(n: SampleSize);

        var model = new UnivariateDistribution(df, UnivariateDistributionType.GeneralizedNormal);
        var analysis = new UnivariateAnalysis(model);
        ConfigureBayesianAnalysis(analysis);

        // Act
        await analysis.RunAsync();

        // Assert
        Assert.IsTrue(analysis.IsEstimated, "Analysis failed to complete.");
        AssertParametersWithinTolerance(model, trueParams, analysis, "GeneralizedNormal");
    }

    /// <summary>
    /// Tests parameter recovery for the Logistic distribution.
    /// </summary>
    /// <remarks>
    /// The Logistic distribution has 2 parameters: location (Xi) and scale (Alpha).
    /// </remarks>
    [TestMethod]
    public async Task Logistic_RecoversTrueParameters()
    {
        // Arrange
        var (df, trueParams) = SyntheticUnivariateData.GenerateLogisticData(n: SampleSize);

        var model = new UnivariateDistribution(df, UnivariateDistributionType.Logistic);
        var analysis = new UnivariateAnalysis(model);
        ConfigureBayesianAnalysis(analysis);

        // Act
        await analysis.RunAsync();

        // Assert
        Assert.IsTrue(analysis.IsEstimated, "Analysis failed to complete.");
        AssertParametersWithinTolerance(model, trueParams, analysis, "Logistic");
    }

    /// <summary>
    /// Tests parameter recovery for the Generalized Logistic distribution.
    /// </summary>
    /// <remarks>
    /// The Generalized Logistic distribution has 3 parameters: location (Xi), scale (Alpha), and shape (Kappa).
    /// </remarks>
    [TestMethod]
    public async Task GeneralizedLogistic_RecoversTrueParameters()
    {
        // Arrange
        var (df, trueParams) = SyntheticUnivariateData.GenerateGeneralizedLogisticData(n: SampleSize);

        var model = new UnivariateDistribution(df, UnivariateDistributionType.GeneralizedLogistic);
        var analysis = new UnivariateAnalysis(model);
        ConfigureBayesianAnalysis(analysis);

        // Act
        await analysis.RunAsync();

        // Assert
        Assert.IsTrue(analysis.IsEstimated, "Analysis failed to complete.");
        AssertParametersWithinTolerance(model, trueParams, analysis, "GeneralizedLogistic");
    }

    #endregion

    #region Extreme Value Family

    /// <summary>
    /// Tests parameter recovery for the Gumbel (Type I Extreme Value) distribution.
    /// </summary>
    /// <remarks>
    /// The Gumbel distribution has 2 parameters: location (Xi) and scale (Alpha).
    /// </remarks>
    [TestMethod]
    public async Task Gumbel_RecoversTrueParameters()
    {
        // Arrange
        var (df, trueParams) = SyntheticUnivariateData.GenerateGumbelData(n: SampleSize);

        var model = new UnivariateDistribution(df, UnivariateDistributionType.Gumbel);
        var analysis = new UnivariateAnalysis(model);
        ConfigureBayesianAnalysis(analysis);

        // Act
        await analysis.RunAsync();

        // Assert
        Assert.IsTrue(analysis.IsEstimated, "Analysis failed to complete.");
        AssertParametersWithinTolerance(model, trueParams, analysis, "Gumbel");
    }

    /// <summary>
    /// Tests parameter recovery for the Generalized Extreme Value (GEV) distribution.
    /// </summary>
    /// <remarks>
    /// The GEV distribution has 3 parameters: location (Xi), scale (Alpha), and shape (Kappa).
    /// This is a fundamental distribution for flood frequency analysis.
    /// </remarks>
    [TestMethod]
    public async Task GeneralizedExtremeValue_RecoversTrueParameters()
    {
        // Arrange
        var (df, trueParams) = SyntheticUnivariateData.GenerateGeneralizedExtremeValueData(n: SampleSize);

        var model = new UnivariateDistribution(df, UnivariateDistributionType.GeneralizedExtremeValue);
        var analysis = new UnivariateAnalysis(model);
        ConfigureBayesianAnalysis(analysis);

        // Act
        await analysis.RunAsync();

        // Assert
        Assert.IsTrue(analysis.IsEstimated, "Analysis failed to complete.");
        AssertParametersWithinTolerance(model, trueParams, analysis, "GeneralizedExtremeValue");
    }

    /// <summary>
    /// Tests parameter recovery for the Generalized Pareto distribution.
    /// </summary>
    /// <remarks>
    /// The Generalized Pareto distribution has 3 parameters: location (Xi), scale (Alpha), and shape (Kappa).
    /// Used for peaks-over-threshold (POT) analysis.
    /// </remarks>
    [TestMethod]
    public async Task GeneralizedPareto_RecoversTrueParameters()
    {
        // Arrange
        var (df, trueParams) = SyntheticUnivariateData.GenerateGeneralizedParetoData(n: SampleSize);

        var model = new UnivariateDistribution(df, UnivariateDistributionType.GeneralizedPareto);
        var analysis = new UnivariateAnalysis(model);
        ConfigureBayesianAnalysis(analysis);

        // Act
        await analysis.RunAsync();

        // Assert
        Assert.IsTrue(analysis.IsEstimated, "Analysis failed to complete.");
        AssertParametersWithinTolerance(model, trueParams, analysis, "GeneralizedPareto");
    }

    /// <summary>
    /// Tests parameter recovery for the Kappa Four-parameter distribution.
    /// </summary>
    /// <remarks>
    /// The Kappa Four distribution has 4 parameters: location (Xi), scale (Alpha),
    /// and two shape parameters (Kappa and H). This is the most flexible distribution
    /// and includes GEV, Gumbel, Logistic, and Generalized Logistic as special cases.
    /// </remarks>
    [TestMethod]
    public async Task KappaFour_RecoversTrueParameters()
    {
        // Arrange
        var (df, trueParams) = SyntheticUnivariateData.GenerateKappaFourData(n: SampleSize);

        var model = new UnivariateDistribution(df, UnivariateDistributionType.KappaFour);
        var analysis = new UnivariateAnalysis(model);
        ConfigureBayesianAnalysis(analysis);

        // Act
        await analysis.RunAsync();

        // Assert
        Assert.IsTrue(analysis.IsEstimated, "Analysis failed to complete.");
        AssertParametersWithinTolerance(model, trueParams, analysis, "KappaFour");
    }

    #endregion

    #region Pearson Family

    /// <summary>
    /// Tests parameter recovery for the Pearson Type III distribution.
    /// </summary>
    /// <remarks>
    /// The Pearson Type III distribution has 3 parameters: mean (Mu), standard deviation (Sigma), and skewness (Gamma).
    /// </remarks>
    [TestMethod]
    public async Task PearsonTypeIII_RecoversTrueParameters()
    {
        // Arrange
        var (df, trueParams) = SyntheticUnivariateData.GeneratePearsonTypeIIIData(n: SampleSize);

        var model = new UnivariateDistribution(df, UnivariateDistributionType.PearsonTypeIII);
        var analysis = new UnivariateAnalysis(model);
        ConfigureBayesianAnalysis(analysis);

        // Act
        await analysis.RunAsync();

        // Assert
        Assert.IsTrue(analysis.IsEstimated, "Analysis failed to complete.");
        AssertParametersWithinTolerance(model, trueParams, analysis, "PearsonTypeIII");
    }

    /// <summary>
    /// Tests parameter recovery for the Log-Pearson Type III distribution.
    /// </summary>
    /// <remarks>
    /// The Log-Pearson Type III distribution has 3 parameters: mean of log-transformed data (Mu),
    /// standard deviation of log-transformed data (Sigma), and skewness of log-transformed data (Gamma).
    /// This is the standard distribution for flood frequency analysis in the United States (Bulletin 17C).
    /// </remarks>
    [TestMethod]
    public async Task LogPearsonTypeIII_RecoversTrueParameters()
    {
        // Arrange
        var (df, trueParams) = SyntheticUnivariateData.GenerateLogPearsonTypeIIIData(n: SampleSize);

        var model = new UnivariateDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);
        model.UseJeffreysRuleForScale = true; // Consistent with Bulletin 17C
        var analysis = new UnivariateAnalysis(model);
        ConfigureBayesianAnalysis(analysis);

        // Act
        await analysis.RunAsync();

        // Assert
        Assert.IsTrue(analysis.IsEstimated, "Analysis failed to complete.");
        AssertParametersWithinTolerance(model, trueParams, analysis, "LogPearsonTypeIII");
    }

    #endregion

    #region Log-Transformed Distributions

    /// <summary>
    /// Tests parameter recovery for the Log-Normal distribution.
    /// </summary>
    /// <remarks>
    /// The Log-Normal distribution has 2 parameters: mean of log-transformed data (Mu)
    /// and standard deviation of log-transformed data (Sigma).
    /// </remarks>
    [TestMethod]
    public async Task LogNormal_RecoversTrueParameters()
    {
        // Arrange
        var (df, trueParams) = SyntheticUnivariateData.GenerateLogNormalData(n: SampleSize);

        var model = new UnivariateDistribution(df, UnivariateDistributionType.LogNormal);
        var analysis = new UnivariateAnalysis(model);
        ConfigureBayesianAnalysis(analysis);

        // Act
        await analysis.RunAsync();

        // Assert
        Assert.IsTrue(analysis.IsEstimated, "Analysis failed to complete.");
        AssertParametersWithinTolerance(model, trueParams, analysis, "LogNormal");
    }

    /// <summary>
    /// Tests parameter recovery for the Ln-Normal (base-e Log-Normal) distribution.
    /// </summary>
    /// <remarks>
    /// The Ln-Normal distribution has 2 parameters: mean of ln-transformed data (Mu)
    /// and standard deviation of ln-transformed data (Sigma).
    /// </remarks>
    [TestMethod]
    public async Task LnNormal_RecoversTrueParameters()
    {
        // Arrange
        var (df, trueParams) = SyntheticUnivariateData.GenerateLnNormalData(n: SampleSize);

        var model = new UnivariateDistribution(df, UnivariateDistributionType.LnNormal);
        var analysis = new UnivariateAnalysis(model);
        ConfigureBayesianAnalysis(analysis);

        // Act
        await analysis.RunAsync();

        // Assert
        Assert.IsTrue(analysis.IsEstimated, "Analysis failed to complete.");
        AssertParametersWithinTolerance(model, trueParams, analysis, "LnNormal");
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Configures the Bayesian analysis settings for consistent testing.
    /// </summary>
    /// <param name="analysis">The univariate analysis to configure.</param>
    private static void ConfigureBayesianAnalysis(UnivariateAnalysis analysis)
    {
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
