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
/// Verifies selected nonstationary univariate trend formulas and generated-parent recovery designs.
/// </summary>
/// <remarks>
/// <para>
///     <b>Authors:</b>
///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
/// </para>
/// <para>
///     <b>Purpose:</b>
///     The retained constant-Normal baseline and reciprocal response cell use 1,000 response/covariate
///     rows, seed 12345, unchanged production DEMCzs defaults, central 95% parent or response inclusion,
///     R-hat below 1.10, and ESS at least 100. The analytical reciprocal cell independently checks the
///     production values and derivatives.
/// </para>
/// <para>
///     <b>Test Configuration:</b>
///     Fifteen broader Normal-only trend combinations remain as non-discovered fixture history below;
///     their former four-posterior-standard-deviation declarations were consolidated into the five-cell
///     <see cref="NonstationaryParentTrendCoverageTests"/> family/parameter-role covering array and the
///     380-assignment fast default matrix. No historical pass is transferred to those current owners.
/// </para>
/// </remarks>
[TestClass]
public class NonstationaryValidationTests
{
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
    /// Recovers the mean and scale of a constant-trend Normal baseline from 1,000 generated rows.
    /// </summary>
    /// <remarks>
    /// <see cref="SyntheticNonstationaryData.GenerateConstantTrendData"/> uses MT19937 seed 12345 and
    /// physical parent order [mean=100, scale=15]. Both parameters are constant trend coordinates.
    /// The exact oracle is generating-parent inclusion in each central 95% posterior interval, with
    /// R-hat below 1.10 and ESS at least 100. The test changes only the reported interval width to 95%.
    /// </remarks>
    [TestMethod]
    public async Task Nonstationary_ConstantTrend_RecoversTrueParameters()
    {
        // Arrange
        var (df, trueParams) = SyntheticNonstationaryData.GenerateConstantTrendData(SampleSize);
        Assert.AreEqual(SampleSize, df.ExactSeries.Count, "The recovery design must contain exactly 1,000 response/covariate rows.");
        Assert.IsTrue(df.ExactSeries.ValuesToArray().All(double.IsFinite), "Every generated response must be finite.");

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
        analysis.BayesianAnalysis.CredibleIntervalWidth = 0.95d;
    }

    /// <summary>
    /// Requires central-95% generating-parent inclusion and the common Bayesian diagnostics for every
    /// directly identified coordinate.
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
            var summary = results.ParameterResults[i].SummaryStatistics;

            string paramName = i < model.Parameters.Count()
                ? model.Parameters.ElementAt(i).Name
                : $"Parameter[{i}]";

            ModelParameter parameter = model.Parameters.ElementAt(i);
            Assert.IsTrue(trueValue >= parameter.LowerBound && trueValue <= parameter.UpperBound,
                $"{testName}: {paramName} parent {trueValue:G17} is outside the unchanged parameter bounds.");
            Assert.IsTrue(double.IsFinite(parameter.PriorDistribution.LogPDF(trueValue)),
                $"{testName}: {paramName} parent {trueValue:G17} is outside the unchanged prior support.");
            string coordinate = $"{testName}.{paramName}";
            RecoveryAcceptance.AssertBayesianRecovery(
                coordinate,
                trueValue,
                summary.LowerCI,
                summary.UpperCI,
                summary.Rhat,
                summary.ESS);
            RecoveryAcceptance.AssertSecondaryPointCriterionWhenResolved(
                coordinate,
                mapValues[i],
                trueValue,
                summary.LowerCI,
                summary.UpperCI);
        }
    }

    #endregion
}
