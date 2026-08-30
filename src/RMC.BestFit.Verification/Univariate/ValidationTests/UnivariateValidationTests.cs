using Numerics.Distributions;
using Numerics.Data.Statistics;
using RMC.BestFit.Analyses;
using RMC.BestFit.Models;
using RMC.BestFit.Verification.Datasets.UnivariateData;
using RMC.BestFit.Verification.Recovery;

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
///     central 95% posterior intervals include the generating parents with monitored R-hat and ESS diagnostics.
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
///     All tests use 1,000 scalar observations generated with seed 12345 and the untouched production
///     Bayesian defaults. Their 95% central credible intervals, R-hat, ESS, and conditional secondary
///     five-percent point rule are evaluated in the fitted parameterization.
/// </para>
/// </remarks>
[TestClass]
public class UnivariateValidationTests
{
    /// <summary>
    /// Sample size for synthetic data generation.
    /// </summary>
    private const int SampleSize = RecoveryDesign.SampleSize;


    #region Exponential Family

    /// <summary>
    /// Tests parameter recovery for the Exponential distribution.
    /// </summary>
    /// <remarks>
    /// The Exponential distribution has 2 parameters: location (Xi) and scale (Alpha). This cell uses
    /// parent [Xi=0, Alpha=50], 1,000 scalar observations with seed 12345, central 95% posterior bands, R-hat below 1.10, ESS at
    /// least 100, and the conditional secondary five-percent rule. Xi=0 is a boundary coordinate, so its
    /// parent recovery is assessed by the predeclared Q(0.99) response band rather than direct coordinate inclusion.
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
    /// The Gamma distribution uses Numerics scale (Theta) and shape (Kappa) parameters with parent
    /// [Theta=5, Kappa=2]. This cell uses 1,000 scalar observations with seed 12345, central 95% parent-coordinate inclusion, R-hat below 1.10, ESS at least
    /// 100, and the conditional secondary five-percent rule.
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
    /// The Weibull distribution (2-parameter reliability version) has 2 parameters: scale (Lambda) and shape
    /// (Kappa), with parent [Lambda=100, Kappa=2.5]. This cell uses 1,000 scalar observations with seed 12345, central 95% parent-coordinate
    /// inclusion, R-hat below 1.10, ESS at least 100, and the conditional secondary five-percent rule.
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
    /// The Normal distribution has 2 parameters: mean (Mu) and standard deviation (Sigma), with parent
    /// [Mu=100, Sigma=15]. This cell uses 1,000
    /// scalar observations with seed 12345, central 95% parent-coordinate inclusion, R-hat below 1.10, ESS at
    /// least 100, and the conditional secondary five-percent rule.
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
    /// The Generalized Normal distribution has 3 parameters: location (Xi), scale (Alpha), and shape (Kappa), with parent [Xi=100, Alpha=15, Kappa=0.1].
    /// This cell uses 1,000 scalar observations with seed 12345, central 95% parent-coordinate inclusion, R-hat
    /// below 1.10, ESS at least 100, and the conditional secondary five-percent rule.
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
    /// The Logistic distribution has 2 parameters: location (Xi) and scale (Alpha), with parent [Xi=100, Alpha=10]. This cell uses 1,000 scalar
    /// observations with seed 12345, central 95% parent-coordinate inclusion, R-hat below 1.10, ESS at least
    /// 100, and the conditional secondary five-percent rule.
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
    /// The Generalized Logistic distribution has 3 parameters: location (Xi), scale (Alpha), and shape (Kappa), with parent [Xi=100, Alpha=15, Kappa=0.1].
    /// This cell uses 1,000 scalar observations with seed 12345, central 95% parent-coordinate inclusion, R-hat
    /// below 1.10, ESS at least 100, and the conditional secondary five-percent rule.
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
    /// The Gumbel distribution has 2 parameters: location (Xi) and scale (Alpha), with parent [Xi=100, Alpha=20]. This cell uses 1,000 scalar
    /// observations with seed 12345, central 95% parent-coordinate inclusion, R-hat below 1.10, ESS at least
    /// 100, and the conditional secondary five-percent rule.
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
    /// The GEV distribution has 3 parameters: location (Xi), scale (Alpha), and shape (Kappa), with parent [Xi=100, Alpha=20, Kappa=-0.1]. This cell uses
    /// 1,000 scalar observations with seed 12345, central 95% parent-coordinate inclusion, R-hat below 1.10,
    /// ESS at least 100, and the conditional secondary five-percent rule.
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
    /// The Generalized Pareto distribution has 3 parameters: location (Xi), scale (Alpha), and shape (Kappa), with parent [Xi=0, Alpha=50, Kappa=0.1].
    /// This cell uses 1,000 scalar observations with seed 12345, central 95% posterior bands, R-hat below 1.10,
    /// ESS at least 100, and the conditional secondary five-percent rule. Xi=0 is assessed by the predeclared
    /// Q(0.99) response band rather than direct boundary-coordinate inclusion.
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
    /// and includes GEV, Gumbel, Logistic, and Generalized Logistic as special cases; the parent is [Xi=100, Alpha=20, Kappa=-0.1, H=0.1].
    /// This cell uses 1,000 scalar observations with seed 12345, central 95% parent-coordinate inclusion,
    /// R-hat below 1.10, ESS at least 100, and the conditional secondary five-percent rule.
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
    /// The Pearson Type III distribution has 3 parameters: mean (Mu), standard deviation (Sigma), and skewness
    /// (Gamma), with parent [Mu=100, Sigma=20, Gamma=0.5]. This cell uses 1,000 scalar observations with seed 12345, central 95% parent-coordinate inclusion,
    /// R-hat below 1.10, ESS at least 100, and the conditional secondary five-percent rule.
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
    /// This is the standard distribution for flood frequency analysis in the United States (Bulletin 17C), with parent [Mu=3, Sigma=0.5, Gamma=0.2].
    /// The cell uses 1,000 scalar observations with seed 12345, central 95% parent-coordinate inclusion, R-hat
    /// below 1.10, ESS at least 100, and the conditional secondary five-percent rule.
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
    /// and standard deviation of log-transformed data (Sigma), with parent [Mu=3, Sigma=0.5].
    /// The cell uses 1,000 scalar observations with seed 12345, central 95% parent-coordinate inclusion, R-hat
    /// below 1.10, ESS at least 100, and the conditional secondary five-percent rule.
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
    /// The Ln-Normal distribution has public real-space parameters mean and standard deviation,
    /// with parent [Mean=4.5, StandardDeviation=0.4].
    /// The cell uses 1,000 scalar observations with seed 12345, central 95% parent-coordinate inclusion, R-hat
    /// below 1.10, ESS at least 100, and the conditional secondary five-percent rule.
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
        analysis.BayesianAnalysis.CredibleIntervalWidth = 0.95d;
    }

    /// <summary>
    /// Asserts central-95% parent inclusion and diagnostics for stationary posterior coordinates, using a
    /// response-space Q(0.99) contract for predeclared zero-location boundaries.
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
        var results = analysis.BayesianAnalysis.Results!;
        var mapValues = results.MAP.Values;

        Assert.AreEqual(trueParams.Length, mapValues.Length,
            $"{testName}: Parameter count mismatch. Expected {trueParams.Length}, got {mapValues.Length}.");

        for (int i = 0; i < trueParams.Length; i++)
        {
            double trueValue = trueParams[i];
            double estimated = mapValues[i];

            string paramName = i < model.Parameters.Count()
                ? model.Parameters.ElementAt(i).Name
                : $"Parameter[{i}]";
            var summary = results.ParameterResults[i].SummaryStatistics;
            string coordinate = $"{testName}.{paramName}";
            AssertDiagnostics(coordinate, summary.Rhat, summary.ESS);
            if (IsZeroLocationBoundary(testName, i, trueValue))
                continue;

            RecoveryAcceptance.AssertBayesianRecovery(coordinate, trueValue, summary.LowerCI, summary.UpperCI, summary.Rhat, summary.ESS);
            RecoveryAcceptance.AssertSecondaryPointCriterionWhenResolved(
                coordinate,
                estimated,
                trueValue,
                summary.LowerCI,
                summary.UpperCI);
        }

        if (IsZeroLocationBoundary(testName, 0, trueParams[0]))
            AssertZeroLocationQuantileRecovery(model, trueParams, results, testName);
    }

    /// <summary>
    /// Asserts the shared convergence diagnostics without imposing coordinate inclusion.
    /// </summary>
    /// <param name="coordinate">The monitored parameter coordinate.</param>
    /// <param name="rhat">The potential scale-reduction factor.</param>
    /// <param name="effectiveSampleSize">The effective sample size.</param>
    /// <remarks>
    /// Boundary coordinates remain sampled and diagnosed even when their recovery assertion is evaluated in response space.
    /// </remarks>
    private static void AssertDiagnostics(string coordinate, double rhat, double effectiveSampleSize)
    {
        Assert.IsTrue(double.IsFinite(rhat) && rhat < RecoveryAcceptance.MaximumRhat,
            $"{coordinate}: R-hat must be finite and below {RecoveryAcceptance.MaximumRhat}.");
        Assert.IsTrue(double.IsFinite(effectiveSampleSize) && effectiveSampleSize >= RecoveryAcceptance.MinimumEffectiveSampleSize,
            $"{coordinate}: ESS must be finite and at least {RecoveryAcceptance.MinimumEffectiveSampleSize}.");
    }

    /// <summary>
    /// Determines whether a stationary test uses the predeclared zero-location response-space contract.
    /// </summary>
    /// <param name="testName">The stationary family identifier.</param>
    /// <param name="parameterIndex">The flattened model-parameter index.</param>
    /// <param name="parentValue">The generating parent coordinate.</param>
    /// <returns><c>true</c> when the coordinate is a zero location boundary.</returns>
    /// <remarks>
    /// Only the Exponential and Generalized Pareto fixtures intentionally generate a location of zero.
    /// </remarks>
    private static bool IsZeroLocationBoundary(string testName, int parameterIndex, double parentValue)
    {
        return parameterIndex == 0 && parentValue == 0d &&
            (testName == "Exponential" || testName == "GeneralizedPareto");
    }

    /// <summary>
    /// Evaluates the Q(0.99) response-space recovery contract for a zero-location stationary fixture.
    /// </summary>
    /// <param name="model">The fitted stationary model.</param>
    /// <param name="trueParameters">The generating distribution parameters.</param>
    /// <param name="results">The completed posterior results.</param>
    /// <param name="testName">The stationary family identifier.</param>
    /// <remarks>
    /// Posterior draws are evaluated on cloned distributions so the finished analysis model remains unchanged.
    /// </remarks>
    private static void AssertZeroLocationQuantileRecovery(
        UnivariateDistribution model,
        double[] trueParameters,
        Numerics.Sampling.MCMC.MCMCResults results,
        string testName)
    {
        var parentDistribution = model.Distribution.Clone();
        parentDistribution.SetParameters(trueParameters);
        double parentQuantile = parentDistribution.InverseCDF(0.99d);

        double[] responseDraws = results.Output.Select(draw =>
        {
            var posteriorDistribution = model.Distribution.Clone();
            posteriorDistribution.SetParameters(draw.Values);
            return posteriorDistribution.InverseCDF(0.99d);
        }).ToArray();
        Assert.IsTrue(responseDraws.All(double.IsFinite), $"{testName}.Q99 posterior response draws must be finite.");
        Array.Sort(responseDraws);

        double lower = Statistics.Percentile(responseDraws, 0.025d, true);
        double upper = Statistics.Percentile(responseDraws, 0.975d, true);
        var fittedDistribution = model.Distribution.Clone();
        fittedDistribution.SetParameters(results.MAP.Values);
        double estimate = fittedDistribution.InverseCDF(0.99d);
        string coordinate = $"{testName}.Q99";
        RecoveryAcceptance.AssertIdentifiedResponseGrid(coordinate, parentQuantile, lower, upper);
        RecoveryAcceptance.AssertSecondaryPointCriterionWhenResolved(coordinate, estimate, parentQuantile, lower, upper);
    }

    #endregion
}
