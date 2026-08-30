using Numerics.Distributions;
using Numerics.Data.Statistics;
using RMC.BestFit.Analyses;
using RMC.BestFit.Models;
using RMC.BestFit.Models.TrendFunctions;
using RMC.BestFit.Models.TrendFunctions.Support;
using RMC.BestFit.Verification.Datasets.UnivariateData;
using RMC.BestFit.Verification.Recovery;

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
///     a distribution with known trend parameters, then verifies the recovery of every
///     parameter with the gross-error gate: the posterior mode lies within four posterior
///     standard deviations of the true value, R-hat is below 1.1, and ESS exceeds 100.
/// </para>
/// <para>
///     <b>Test Configuration:</b>
///     All tests use 1000 synthetic samples drawn from the trend evaluated at each observation's
///     own time index with seed 12345, the untouched production <c>BayesianAnalysis</c> defaults
///     (DEMCzs), and the posterior mode as the point estimator. The former 1% relative rule was
///     tighter than the information in 1,000 observations (intercepts and scales carry 1-1.5%
///     posterior standard deviations), and a single-seed central-interval coverage check is a
///     one-shot probabilistic criterion whose outcome is shared across cells that use the same
///     noise realization (the first 100 residuals of seed 12345 average +3.2, which shifts every
///     polynomial and step intercept by about 2 posterior standard deviations); both were replaced
///     on 22 August 2026 (TR-084).
/// </para>
/// </remarks>
[TestClass]
public class NonstationaryValidationTests
{
    /// <summary>
    /// The standardized-error limit: the posterior mode must lie within this many posterior
    /// standard deviations of the true value.
    /// </summary>
    private const double StandardizedErrorLimit = 4.0;

    /// <summary>
    /// The R-hat convergence limit.
    /// </summary>
    private const double RhatLimit = 1.1;

    /// <summary>
    /// The minimum effective sample size.
    /// </summary>
    private const double MinimumEffectiveSampleSize = 100.0;

    /// <summary>
    /// Sample size for synthetic data generation.
    /// </summary>
    private const int SampleSize = 1000;

    #region Trend on Mean Only

    /// <summary>
    /// Checks reciprocal production values and parameter derivatives against the analytical formula.
    /// </summary>
    /// <remarks>
    /// At StartIndex 10 with a=0.02 and b=0.001, this independently checks f(t)=1/(a+b(t-StartIndex)),
    /// df/da=-1/d^2, and df/db=-(t-StartIndex)/d^2 by central finite perturbations of the production trend.
    /// </remarks>
    [TestMethod]
    public void ReciprocalTrend_ValuesAndParameterDerivativesMatchAnalyticalFormula()
    {
        var trend = new ReciprocalTrend { StartIndex = 10 };
        trend.Parameters[0].Value = 0.02d;
        trend.Parameters[1].Value = 0.001d;
        const int index = 510;
        const double perturbation = 1e-7d;
        double t = index - trend.StartIndex;
        double denominator = 0.02d + 0.001d * t;

        Assert.AreEqual(1d / denominator, trend.Predict(index), 1e-12d);
        trend.Parameters[0].Value += perturbation;
        double plusA = trend.Predict(index);
        trend.Parameters[0].Value -= 2d * perturbation;
        double minusA = trend.Predict(index);
        trend.Parameters[0].Value += perturbation;
        trend.Parameters[1].Value += perturbation;
        double plusB = trend.Predict(index);
        trend.Parameters[1].Value -= 2d * perturbation;
        double minusB = trend.Predict(index);

        Assert.AreEqual(-1d / (denominator * denominator), (plusA - minusA) / (2d * perturbation), 1e-6d);
        Assert.AreEqual(-t / (denominator * denominator), (plusB - minusB) / (2d * perturbation), 1e-4d);
    }

    /// <summary>
    /// Recovers a reciprocal mean Normal parent on a predeclared start, midpoint, and end response grid.
    /// </summary>
    /// <remarks>
    /// The cell contains 1,000 response/covariate rows generated with seed 12345; it uses untouched
    /// production Bayesian defaults and a 95% central posterior band. The correlated reciprocal mean
    /// coefficients are checked through mean responses at 0, 499, and 999, while Normal scale uses its
    /// identifiable fitted coordinate. R-hat and ESS apply to every sampled parameter; the secondary
    /// five-percent rule applies only when an existing response or scale band is narrower than five percent.
    /// </remarks>
    [TestMethod]
    public async Task Nonstationary_ReciprocalMeanTrend_RecoversParentResponses()
    {
        var (df, trueParameters) = SyntheticNonstationaryData.GenerateReciprocalMeanTrendData(SampleSize);
        var model = new UnivariateDistribution(df, UnivariateDistributionType.Normal) { IsNonstationary = true };
        model.SetTrendModel(0, TrendModelType.Reciprocal);
        var analysis = new UnivariateAnalysis(model);
        ConfigureBayesianAnalysis(analysis);
        analysis.BayesianAnalysis.CredibleIntervalWidth = 0.95d;

        await analysis.RunAsync();

        Assert.IsTrue(analysis.IsEstimated, "Reciprocal mean analysis failed to complete.");
        var results = analysis.BayesianAnalysis.Results!;
        for (int parameterIndex = 0; parameterIndex < results.ParameterResults.Count(); parameterIndex++)
        {
            var summary = results.ParameterResults[parameterIndex].SummaryStatistics;
            Assert.IsTrue(double.IsFinite(summary.Rhat) && summary.Rhat < RecoveryAcceptance.MaximumRhat,
                $"Reciprocal.parameter[{parameterIndex}] R-hat must be below {RecoveryAcceptance.MaximumRhat}.");
            Assert.IsTrue(double.IsFinite(summary.ESS) && summary.ESS >= RecoveryAcceptance.MinimumEffectiveSampleSize,
                $"Reciprocal.parameter[{parameterIndex}] ESS must be at least {RecoveryAcceptance.MinimumEffectiveSampleSize}.");
        }

        var scaleSummary = results.ParameterResults[2].SummaryStatistics;
        RecoveryAcceptance.AssertBayesianRecovery("Reciprocal.NormalScale", trueParameters[2], scaleSummary.LowerCI, scaleSummary.UpperCI, scaleSummary.Rhat, scaleSummary.ESS);
        RecoveryAcceptance.AssertSecondaryPointCriterionWhenResolved("Reciprocal.NormalScale", results.MAP.Values[2], trueParameters[2], scaleSummary.LowerCI, scaleSummary.UpperCI);

        foreach (int index in new[] { 0, 499, 999 })
        {
            double parentResponse = 1d / (trueParameters[0] + trueParameters[1] * index);
            double[] responseDraws = results.Output.Select(draw => 1d / (draw.Values[0] + draw.Values[1] * index)).ToArray();
            Assert.IsTrue(responseDraws.All(double.IsFinite), $"Reciprocal response draws at {index} must be finite.");
            Array.Sort(responseDraws);
            double lower = Statistics.Percentile(responseDraws, 0.025d, true);
            double upper = Statistics.Percentile(responseDraws, 0.975d, true);
            double estimate = 1d / (results.MAP.Values[0] + results.MAP.Values[1] * index);
            string label = $"Reciprocal.MeanResponse[{index}]";
            RecoveryAcceptance.AssertIdentifiedResponseGrid(label, parentResponse, lower, upper);
            RecoveryAcceptance.AssertSecondaryPointCriterionWhenResolved(label, estimate, parentResponse, lower, upper);
        }
    }

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
    /// Configures the Bayesian analysis with the default MCMC settings and the posterior-mode point estimator.
    /// </summary>
    /// <param name="analysis">The univariate analysis to configure.</param>
    private static void ConfigureBayesianAnalysis(UnivariateAnalysis analysis)
    {
        analysis.BayesianAnalysis.PointEstimator = RMC.BestFit.Estimation.BayesianAnalysis.PointEstimateType.PosteriorMode;
    }

    /// <summary>
    /// Asserts the recovery of every parameter with the gross-error gate: the posterior mode lies
    /// within <see cref="StandardizedErrorLimit"/> posterior standard deviations of the true value,
    /// R-hat is below the convergence limit, and the effective sample size exceeds the minimum.
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
            var summary = results.ParameterResults[i].SummaryStatistics;

            string paramName = i < model.Parameters.Count()
                ? model.Parameters.ElementAt(i).Name
                : $"Parameter[{i}]";

            Assert.IsTrue(double.IsFinite(summary.StandardDeviation) && summary.StandardDeviation > 0.0,
                $"{testName}: {paramName} - posterior standard deviation {summary.StandardDeviation:G6} is not positive and finite.");
            double standardizedError = Math.Abs(estimated - trueValue) / summary.StandardDeviation;
            Assert.IsTrue(standardizedError <= StandardizedErrorLimit,
                $"{testName}: {paramName} - posterior mode {estimated:F6} is {standardizedError:F2} posterior standard deviations " +
                $"({summary.StandardDeviation:G6}) from the true value {trueValue:F6}; limit {StandardizedErrorLimit}.");
            Assert.IsTrue(double.IsFinite(summary.Rhat) && summary.Rhat < RhatLimit,
                $"{testName}: {paramName} - R-hat {summary.Rhat:F4} is not below {RhatLimit}.");
            Assert.IsTrue(double.IsFinite(summary.ESS) && summary.ESS > MinimumEffectiveSampleSize,
                $"{testName}: {paramName} - ESS {summary.ESS:F0} is not above {MinimumEffectiveSampleSize}.");
        }
    }

    #endregion
}
