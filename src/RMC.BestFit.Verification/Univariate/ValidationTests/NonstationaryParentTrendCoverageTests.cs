using Numerics.Data.Statistics;
using Numerics.Distributions;
using RMC.BestFit.Analyses;
using RMC.BestFit.Models;
using RMC.BestFit.Models.TrendFunctions.Support;
using RMC.BestFit.Verification.Datasets.UnivariateData;
using RMC.BestFit.Verification.Recovery;

namespace RMC.BestFit.Verification.Univariate.ValidationTests;

/// <summary>
/// Verifies a covering array of parent distributions, parameter roles, and temporal trend models.
/// </summary>
/// <remarks>
/// Every cell uses 1,000 observations with seed 12345, unchanged production Bayesian defaults,
/// central 95 percent posterior intervals, R-hat below 1.10, and ESS of at least 100 for every
/// sampled coordinate. Correlated trend coefficients are assessed on predeclared response grids.
/// </remarks>
[TestClass]
public class NonstationaryParentTrendCoverageTests
{
    private const int SampleSize = 1000;
    private static readonly int[] ThreePointGrid = [0, 499, 999];
    private static readonly int[] SinusoidalGrid = [0, 25, 50, 75, 999];

    /// <summary>
    /// Verifies a reciprocal Normal scale response at the start, midpoint, and end of the record.
    /// </summary>
    [TestMethod]
    public async Task Normal_ReciprocalSigma_RecoversParentResponses()
    {
        var (dataFrame, parents) = SyntheticNonstationaryData.GenerateReciprocalSigmaNormalData(SampleSize);
        UnivariateAnalysis analysis = await FitAsync(
            dataFrame,
            UnivariateDistributionType.Normal,
            model => model.SetTrendModel(1, TrendModelType.Reciprocal));

        AssertAllDiagnostics(analysis, "Normal.ReciprocalSigma");
        AssertScalarCoordinate(analysis, 0, parents[0], "Normal.Mean");
        AssertResponseGrid(
            analysis,
            "Normal.ReciprocalSigma",
            ThreePointGrid,
            (values, index) => 1d / (values[1] + values[2] * index),
            index => 1d / (parents[1] + parents[2] * index));
    }

    /// <summary>
    /// Verifies a sinusoidal Normal scale response at predeclared phase-covering ordinates.
    /// </summary>
    [TestMethod]
    public async Task Normal_SinusoidalSigma_RecoversParentResponses()
    {
        var (dataFrame, parents) = SyntheticNonstationaryData.GenerateSinusoidalSigmaNormalData(SampleSize);
        UnivariateAnalysis analysis = await FitAsync(
            dataFrame,
            UnivariateDistributionType.Normal,
            model => model.SetTrendModel(1, TrendModelType.Sinusoidal));

        AssertAllDiagnostics(analysis, "Normal.SinusoidalSigma");
        AssertScalarCoordinate(analysis, 0, parents[0], "Normal.Mean");
        AssertResponseGrid(
            analysis,
            "Normal.SinusoidalSigma",
            SinusoidalGrid,
            (values, index) => values[1] + values[2] * Math.Sin(2d * Math.PI * values[3] * index + values[4]),
            index => parents[1] + parents[2] * Math.Sin(2d * Math.PI * parents[3] * index + parents[4]));
    }

    /// <summary>
    /// Verifies a generalized-extreme-value linear shape response at the record endpoints and midpoint.
    /// </summary>
    [TestMethod]
    public async Task GeneralizedExtremeValue_LinearShape_RecoversParentResponses()
    {
        var (dataFrame, parents) = SyntheticNonstationaryData.GenerateLinearShapeGevData(SampleSize);
        UnivariateAnalysis analysis = await FitAsync(
            dataFrame,
            UnivariateDistributionType.GeneralizedExtremeValue,
            model => model.SetTrendModel(2, TrendModelType.Linear));

        AssertAllDiagnostics(analysis, "GEV.LinearShape");
        AssertScalarCoordinate(analysis, 0, parents[0], "GEV.Location");
        AssertScalarCoordinate(analysis, 1, parents[1], "GEV.Scale");
        AssertResponseGrid(
            analysis,
            "GEV.LinearShape",
            ThreePointGrid,
            (values, index) => values[2] + values[3] * index,
            index => parents[2] + parents[3] * index);
    }

    /// <summary>
    /// Verifies generalized-Pareto linear location and exponential scale responses.
    /// </summary>
    [TestMethod]
    public async Task GeneralizedPareto_LinearLocationExponentialScale_RecoversParentResponses()
    {
        var (dataFrame, parents) = SyntheticNonstationaryData.GenerateLinearLocationExponentialScaleGpdData(SampleSize);
        UnivariateAnalysis analysis = await FitAsync(
            dataFrame,
            UnivariateDistributionType.GeneralizedPareto,
            model =>
            {
                model.SetTrendModel(0, TrendModelType.Linear);
                model.SetTrendModel(1, TrendModelType.Exponential);
            });

        AssertAllDiagnostics(analysis, "GPD.LinearLocationExponentialScale");
        AssertScalarCoordinate(analysis, 4, parents[4], "GPD.Shape");
        AssertResponseGrid(
            analysis,
            "GPD.LinearLocation",
            ThreePointGrid,
            (values, index) => values[0] + values[1] * index,
            index => parents[0] + parents[1] * index);
        AssertResponseGrid(
            analysis,
            "GPD.ExponentialScale",
            ThreePointGrid,
            (values, index) => values[2] * Math.Exp(values[3] * index),
            index => parents[2] * Math.Exp(parents[3] * index));
    }

    /// <summary>
    /// Verifies Log-Pearson type III linear log-mean and exponential log-scale responses.
    /// </summary>
    [TestMethod]
    public async Task LogPearsonTypeIII_LinearLogMeanExponentialLogScale_RecoversParentResponses()
    {
        var (dataFrame, parents) = SyntheticNonstationaryData.GenerateLinearLogMeanExponentialLogScaleLp3Data(SampleSize);
        UnivariateAnalysis analysis = await FitAsync(
            dataFrame,
            UnivariateDistributionType.LogPearsonTypeIII,
            model =>
            {
                model.SetTrendModel(0, TrendModelType.Linear);
                model.SetTrendModel(1, TrendModelType.Exponential);
            });

        AssertAllDiagnostics(analysis, "LP3.LinearLogMeanExponentialLogScale");
        AssertScalarCoordinate(analysis, 4, parents[4], "LP3.Skew");
        AssertResponseGrid(
            analysis,
            "LP3.LinearLogMean",
            ThreePointGrid,
            (values, index) => values[0] + values[1] * index,
            index => parents[0] + parents[1] * index);
        AssertResponseGrid(
            analysis,
            "LP3.ExponentialLogScale",
            ThreePointGrid,
            (values, index) => values[2] * Math.Exp(values[3] * index),
            index => parents[2] * Math.Exp(parents[3] * index));
    }

    /// <summary>
    /// Fits one preconfigured nonstationary recovery cell with unchanged production Bayesian defaults.
    /// </summary>
    /// <param name="dataFrame">The generated response data.</param>
    /// <param name="distributionType">The parent distribution family.</param>
    /// <param name="configureTrends">The predeclared trend assignment.</param>
    /// <returns>The completed univariate analysis.</returns>
    private static async Task<UnivariateAnalysis> FitAsync(
        DataFrame dataFrame,
        UnivariateDistributionType distributionType,
        Action<UnivariateDistribution> configureTrends)
    {
        var model = new UnivariateDistribution(dataFrame, distributionType) { IsNonstationary = true };
        configureTrends(model);
        var analysis = new UnivariateAnalysis(model);
        analysis.BayesianAnalysis.PointEstimator = RMC.BestFit.Estimation.BayesianAnalysis.PointEstimateType.PosteriorMode;
        analysis.BayesianAnalysis.CredibleIntervalWidth = 0.95d;

        await analysis.RunAsync();

        Assert.IsTrue(analysis.IsEstimated, $"{distributionType} nonstationary analysis failed to complete.");
        Assert.IsNotNull(analysis.BayesianAnalysis.Results);
        return analysis;
    }

    /// <summary>
    /// Requires finite accepted R-hat and ESS diagnostics for every sampled coordinate.
    /// </summary>
    /// <param name="analysis">The completed Bayesian analysis.</param>
    /// <param name="cell">The verification-cell label.</param>
    private static void AssertAllDiagnostics(UnivariateAnalysis analysis, string cell)
    {
        var results = analysis.BayesianAnalysis.Results!;
        for (int parameterIndex = 0; parameterIndex < results.ParameterResults.Count(); parameterIndex++)
        {
            var summary = results.ParameterResults[parameterIndex].SummaryStatistics;
            Assert.IsTrue(double.IsFinite(summary.Rhat) && summary.Rhat < RecoveryAcceptance.MaximumRhat,
                $"{cell}.parameter[{parameterIndex}] R-hat must be below {RecoveryAcceptance.MaximumRhat}.");
            Assert.IsTrue(double.IsFinite(summary.ESS) && summary.ESS >= RecoveryAcceptance.MinimumEffectiveSampleSize,
                $"{cell}.parameter[{parameterIndex}] ESS must be at least {RecoveryAcceptance.MinimumEffectiveSampleSize}.");
        }
    }

    /// <summary>
    /// Requires central-95 percent inclusion for one directly identified scalar coordinate.
    /// </summary>
    /// <param name="analysis">The completed Bayesian analysis.</param>
    /// <param name="parameterIndex">The sampled-coordinate index.</param>
    /// <param name="parent">The generating parent.</param>
    /// <param name="label">The coordinate label.</param>
    private static void AssertScalarCoordinate(
        UnivariateAnalysis analysis,
        int parameterIndex,
        double parent,
        string label)
    {
        var results = analysis.BayesianAnalysis.Results!;
        var summary = results.ParameterResults[parameterIndex].SummaryStatistics;
        RecoveryAcceptance.AssertBayesianRecovery(
            label,
            parent,
            summary.LowerCI,
            summary.UpperCI,
            summary.Rhat,
            summary.ESS);
        RecoveryAcceptance.AssertSecondaryPointCriterionWhenResolved(
            label,
            results.MAP.Values[parameterIndex],
            parent,
            summary.LowerCI,
            summary.UpperCI);
    }

    /// <summary>
    /// Requires central-95 percent posterior inclusion on a predeclared identified response grid.
    /// </summary>
    /// <param name="analysis">The completed Bayesian analysis.</param>
    /// <param name="label">The response label.</param>
    /// <param name="grid">The predeclared time indices.</param>
    /// <param name="response">Maps a sampled parameter vector and time index to its response.</param>
    /// <param name="parentResponse">Maps a time index to its generating response.</param>
    private static void AssertResponseGrid(
        UnivariateAnalysis analysis,
        string label,
        IEnumerable<int> grid,
        Func<double[], int, double> response,
        Func<int, double> parentResponse)
    {
        var results = analysis.BayesianAnalysis.Results!;
        foreach (int index in grid)
        {
            double[] responseDraws = results.Output.Select(draw => response(draw.Values, index)).ToArray();
            Assert.IsTrue(responseDraws.All(double.IsFinite), $"{label}[{index}] posterior responses must be finite.");
            Array.Sort(responseDraws);
            double lower = Statistics.Percentile(responseDraws, 0.025d, true);
            double upper = Statistics.Percentile(responseDraws, 0.975d, true);
            double parent = parentResponse(index);
            double estimate = response(results.MAP.Values, index);
            string ordinate = $"{label}[{index}]";
            RecoveryAcceptance.AssertIdentifiedResponseGrid(ordinate, parent, lower, upper);
            RecoveryAcceptance.AssertSecondaryPointCriterionWhenResolved(ordinate, estimate, parent, lower, upper);
        }
    }
}
