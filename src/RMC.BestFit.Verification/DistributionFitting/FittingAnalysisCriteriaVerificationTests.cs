using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Numerics.Data.Statistics;
using Numerics.Distributions;
using RMC.BestFit.Analyses;
using RMC.BestFit.Models;

namespace RMC.BestFit.Verification.DistributionFitting;

/// <summary>
/// Verifies distribution-fitting criteria, ranking, weights, and result ordering.
/// </summary>
/// <remarks>
/// The numerical oracle is generated independently by SciPy from one common dataset.
/// Fast state, event, failure, and cancellation regressions remain in RMC.BestFit.Tests.
/// </remarks>
[TestClass]
public class FittingAnalysisCriteriaVerificationTests
{
    /// <summary>
    /// Verifies that complete exact-series replacement produces the analytical Weibull plotting
    /// positions consumed by distribution fitting.
    /// </summary>
    [TestMethod]
    public void ExactSeriesReplacement_ProducesAnalyticalWeibullPositions()
    {
        JsonElement oracle = LoadOracle();
        double[] values = ReadArray(oracle.GetProperty("data"));
        double[] expectedPlottingPositions = ReadArray(oracle.GetProperty("plotting_positions"));
        var dataFrame = new DataFrame
        {
            ExactSeries = new ExactSeries(values)
        };

        double[] actualPlottingPositions = dataFrame.ExactSeries
            .Select(observation => observation.PlottingPositionComplement)
            .ToArray();
        AssertArrayEqual(expectedPlottingPositions, actualPlottingPositions, 1E-12d, "plotting positions");
        Assert.IsTrue(dataFrame.ExactSeries.All(observation =>
            observation.PlottingPosition > 0d && observation.PlottingPosition < 1d));
    }

    /// <summary>
    /// Verifies fitted parameters for three common-data candidates against SciPy.
    /// </summary>
    [TestMethod]
    public async Task FittedParameters_MatchIndependentOracle()
    {
        JsonElement oracle = LoadOracle();
        double[] values = ReadArray(oracle.GetProperty("data"));
        var dataFrame = new DataFrame
        {
            ExactSeries = new ExactSeries(values)
        };
        var analysis = new FittingAnalysis(dataFrame);
        analysis.DistributionList.Clear();
        analysis.DistributionList.Add(new Gumbel());
        analysis.DistributionList.Add(new Normal());
        analysis.DistributionList.Add(new Logistic());

        await analysis.RunAsync();

        string diagnostics = string.Join(
            "; ",
            analysis.FittedDistributions.Select(result =>
                $"{ResultName(result)}: succeeded={result.FitSucceeded}, AIC={result.AIC}, BIC={result.BIC}, RMSE={result.RMSE}, error={result.ErrorMessage}"));
        Assert.IsTrue(analysis.IsEstimated, diagnostics);
        Assert.IsTrue(analysis.FittedDistributions.All(result => result.FitSucceeded), diagnostics);

        JsonElement candidates = oracle.GetProperty("candidates");
        foreach (FittedDistribution result in analysis.FittedDistributions)
        {
            string name = ResultName(result);
            double[] expectedParameters = ReadArray(
                candidates.GetProperty(name).GetProperty("numerics_parameters"));
            double[] actualParameters = result.Distribution!.GetParameters;

            Assert.AreEqual(expectedParameters.Length, actualParameters.Length);
            for (int i = 0; i < expectedParameters.Length; i++)
            {
                double tolerance = 1E-5d * Math.Max(1d, Math.Abs(expectedParameters[i]));
                Assert.AreEqual(
                    expectedParameters[i],
                    actualParameters[i],
                    tolerance,
                    $"{name} parameter {i} differs from the SciPy common-data MLE; " +
                    $"all actual parameters=[{string.Join(", ", actualParameters)}].");
            }
        }
    }

    /// <summary>
    /// Verifies criteria and ordering for three common-data candidate fits against SciPy.
    /// </summary>
    [TestMethod]
    public async Task CriteriaRankingWeightsAndConfiguredOrder_MatchIndependentOracle()
    {
        JsonElement oracle = LoadOracle();
        double[] values = ReadArray(oracle.GetProperty("data"));
        double[] expectedPlottingPositions = ReadArray(oracle.GetProperty("plotting_positions"));
        string[] configuredOrder = ReadStrings(oracle.GetProperty("configured_order"));
        string[] expectedAicRanking = ReadStrings(oracle.GetProperty("aic_ranking"));

        var dataFrame = new DataFrame
        {
            ExactSeries = new ExactSeries(values)
        };
        var analysis = new FittingAnalysis(dataFrame);
        analysis.DistributionList.Clear();
        analysis.DistributionList.Add(new Gumbel());
        analysis.DistributionList.Add(new Normal());
        analysis.DistributionList.Add(new Logistic());

        await analysis.RunAsync();

        string diagnostics = string.Join(
            "; ",
            analysis.FittedDistributions.Select(result =>
                $"{ResultName(result)}: succeeded={result.FitSucceeded}, AIC={result.AIC}, BIC={result.BIC}, RMSE={result.RMSE}, error={result.ErrorMessage}"));
        Assert.IsTrue(analysis.IsEstimated, diagnostics);
        Assert.AreEqual(configuredOrder.Length, analysis.FittedDistributions.Count);
        CollectionAssert.AreEqual(
            configuredOrder,
            analysis.FittedDistributions.Select(ResultName).ToArray(),
            "Parallel fitting must preserve configured candidate order.");
        Assert.IsTrue(analysis.FittedDistributions.All(result => result.FitSucceeded));

        double[] actualPlottingPositions = dataFrame.ExactSeries
            .Select(observation => observation.PlottingPositionComplement)
            .ToArray();
        AssertArrayEqual(expectedPlottingPositions, actualPlottingPositions, 1E-12d, "plotting positions");

        JsonElement candidates = oracle.GetProperty("candidates");
        foreach (FittedDistribution result in analysis.FittedDistributions)
        {
            string name = ResultName(result);
            JsonElement expected = candidates.GetProperty(name);
            double[] actualParameters = result.Distribution!.GetParameters;
            var model = new UnivariateDistribution(dataFrame, result.Distribution.Type);
            double logLikelihood = model.DataLogLikelihood(actualParameters);
            int parameterCount = result.Distribution.NumberOfParameters;
            double handAic = -2d * logLikelihood + 2d * parameterCount;
            double handBic = -2d * logLikelihood + parameterCount * Math.Log(values.Length);
            double handRmse = HandRmse(
                values,
                actualPlottingPositions,
                result.Distribution,
                parameterCount);

            Assert.AreEqual(handAic, result.AIC, 1E-10d, $"{name} AIC formula mismatch.");
            Assert.AreEqual(handBic, result.BIC, 1E-10d, $"{name} BIC formula mismatch.");
            Assert.AreEqual(handRmse, result.RMSE, 1E-10d, $"{name} RMSE formula mismatch.");
            AssertCrossLanguageEqual(
                expected.GetProperty("maximum_log_likelihood").GetDouble(),
                logLikelihood,
                $"{name} maximum log likelihood");
            AssertCrossLanguageEqual(expected.GetProperty("aic").GetDouble(), result.AIC, $"{name} AIC");
            AssertCrossLanguageEqual(expected.GetProperty("bic").GetDouble(), result.BIC, $"{name} BIC");
            AssertOptimizerEqual(expected.GetProperty("rmse").GetDouble(), result.RMSE, $"{name} RMSE");
        }

        string[] actualAicRanking = analysis.FittedDistributions
            .OrderBy(result => result.AIC)
            .ThenBy(ResultName, StringComparer.Ordinal)
            .Select(ResultName)
            .ToArray();
        CollectionAssert.AreEqual(expectedAicRanking, actualAicRanking, "AIC ranking differs from SciPy.");

        var rmseValues = analysis.FittedDistributions.Select(result => result.RMSE).ToArray();
        double[] actualWeights = GoodnessOfFit.RMSEWeights(rmseValues);
        for (int i = 0; i < analysis.FittedDistributions.Count; i++)
        {
            string name = ResultName(analysis.FittedDistributions[i]);
            double expectedWeight = candidates.GetProperty(name).GetProperty("inverse_mse_weight").GetDouble();
            double handWeight = (1d / Math.Pow(rmseValues[i], 2d)) /
                rmseValues.Sum(rmse => 1d / Math.Pow(rmse, 2d));
            Assert.AreEqual(handWeight, actualWeights[i], 1E-12d, $"{name} inverse-MSE formula mismatch.");
            AssertOptimizerEqual(expectedWeight, actualWeights[i], $"{name} inverse-MSE weight");
        }
        Assert.AreEqual(1d, actualWeights.Sum(), 1E-12d, "RMSE weights must sum to one.");
    }

    /// <summary>
    /// Computes RMSE from every paired residual with an <c>n-k</c> denominator.
    /// </summary>
    /// <param name="values">Observed values.</param>
    /// <param name="plottingPositions">Nonexceedance plotting positions.</param>
    /// <param name="distribution">Fitted distribution.</param>
    /// <param name="parameterCount">Number of fitted parameters.</param>
    /// <returns>The hand-computed parameter-adjusted RMSE.</returns>
    private static double HandRmse(
        IReadOnlyList<double> values,
        IReadOnlyList<double> plottingPositions,
        UnivariateDistributionBase distribution,
        int parameterCount)
    {
        double squaredError = 0d;
        for (int i = 0; i < values.Count; i++)
        {
            double residual = distribution.InverseCDF(plottingPositions[i]) - values[i];
            squaredError += residual * residual;
        }
        return Math.Sqrt(squaredError / (values.Count - parameterCount));
    }

    /// <summary>
    /// Returns the stable artifact name for a fitted result.
    /// </summary>
    /// <param name="result">Fitted result.</param>
    /// <returns>The distribution type name.</returns>
    private static string ResultName(FittedDistribution result)
    {
        return result.Distribution!.Type.ToString();
    }

    /// <summary>
    /// Loads the common-data FittingAnalysis oracle from the committed SciPy artifact.
    /// </summary>
    /// <returns>A detached JSON element.</returns>
    private static JsonElement LoadOracle()
    {
        string path = Path.Combine(
            AppContext.BaseDirectory,
            "VerificationData",
            "scipy-family-oracles.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.GetProperty("fitting_analysis").Clone();
    }

    /// <summary>
    /// Reads a JSON numeric array.
    /// </summary>
    /// <param name="element">JSON array element.</param>
    /// <returns>The numeric values.</returns>
    private static double[] ReadArray(JsonElement element)
    {
        return element.EnumerateArray().Select(value => value.GetDouble()).ToArray();
    }

    /// <summary>
    /// Reads a JSON string array.
    /// </summary>
    /// <param name="element">JSON array element.</param>
    /// <returns>The string values.</returns>
    private static string[] ReadStrings(JsonElement element)
    {
        return element.EnumerateArray().Select(value => value.GetString()!).ToArray();
    }

    /// <summary>
    /// Compares a deterministic array to a fixed tolerance.
    /// </summary>
    /// <param name="expected">Expected values.</param>
    /// <param name="actual">Actual values.</param>
    /// <param name="tolerance">Absolute tolerance.</param>
    /// <param name="quantity">Quantity name.</param>
    private static void AssertArrayEqual(
        IReadOnlyList<double> expected,
        IReadOnlyList<double> actual,
        double tolerance,
        string quantity)
    {
        Assert.AreEqual(expected.Count, actual.Count);
        for (int i = 0; i < expected.Count; i++)
        {
            Assert.AreEqual(expected[i], actual[i], tolerance, $"{quantity} index {i} differs.");
        }
    }

    /// <summary>
    /// Applies cross-language likelihood and criterion tolerances.
    /// </summary>
    /// <param name="expected">External value.</param>
    /// <param name="actual">C# value.</param>
    /// <param name="quantity">Quantity name.</param>
    private static void AssertCrossLanguageEqual(double expected, double actual, string quantity)
    {
        double tolerance = 1E-8d + 1E-7d * Math.Abs(expected);
        Assert.AreEqual(expected, actual, tolerance, $"Cross-language {quantity} differs.");
    }

    /// <summary>
    /// Applies the scaled optimizer tolerance to derived quantities.
    /// </summary>
    /// <param name="expected">External value.</param>
    /// <param name="actual">C# value.</param>
    /// <param name="quantity">Quantity name.</param>
    private static void AssertOptimizerEqual(double expected, double actual, string quantity)
    {
        double tolerance = 1E-5d * Math.Max(1d, Math.Abs(expected));
        Assert.AreEqual(expected, actual, tolerance, $"Optimizer-derived {quantity} differs.");
    }
}
