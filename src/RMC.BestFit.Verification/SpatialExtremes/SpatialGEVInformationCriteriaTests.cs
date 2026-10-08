using System.Text.Json;
using Numerics.Data.Statistics;
using RMC.BestFit.Analyses;
using RMC.BestFit.Models.SpatialExtremes;
using RMC.BestFit.Models.TrendFunctions;
using RMC.BestFit.Verification.TimeSeriesAnalysis;

namespace RMC.BestFit.Verification.SpatialExtremes;

/// <summary>
/// Verifies, after an MCMC run with the production defaults on the oracle's five-site missing-site model,
/// that the spatial information criteria use the row/year unit (TR-055 closure after TR-048 and TR-049):
/// AIC and BIC derive from the observation log likelihood at the sampled MAP with one nonempty row/year
/// block per BIC observation, and WAIC and PSIS-LOO derive from the row/year pointwise terms of the
/// retained draws.
/// </summary>
/// <remarks>
/// The fixture is the <c>missing_site_model</c> of <c>spatial-copula-likelihood-oracle.json</c>: twelve rows
/// at five sites, rows 5-8 with one to three missing sites and row 9 fully missing, copula dependence
/// enabled. The criteria identities are deterministic given the sampled posterior; the cell asserts them
/// against independent recomputation from the same draws rather than against fixed numbers.
/// </remarks>
[TestClass]
public class SpatialGEVInformationCriteriaTests
{
    /// <summary>The committed artifact copied to the test output.</summary>
    private const string ArtifactFileName = "spatial-copula-likelihood-oracle.json";

    /// <summary>Absolute tolerance for the AIC/BIC recomputation at the sampled MAP.</summary>
    private const double CriterionTolerance = 1e-8;

    /// <summary>Relative tolerance for the WAIC recomputation over ten thousand retained draws.</summary>
    private const double PredictiveRelativeTolerance = 1e-9;

    /// <summary>
    /// The AIC and BIC reported by the analysis equal the observation log likelihood at the sampled MAP
    /// penalized with the parameter count, the BIC sample size being the number of nonempty row/year
    /// blocks (eleven of twelve rows); WAIC equals the row/year recomputation over the retained draws
    /// and PSIS-LOO reports one Pareto k per row/year.
    /// </summary>
    [TestMethod]
    public async Task MissingSiteModel_InformationCriteria_UseRowYearBlocks()
    {
        using JsonDocument document = LoadDocument();
        JsonElement root = document.RootElement;
        JsonElement section = root.GetProperty("missing_site_model");
        double[,] coordinates = ReadMatrix(root.GetProperty("network").GetProperty("coordinates"));
        double[,] data = ReadMatrix(section.GetProperty("data"));
        var model = new SpatialGEV(
            data,
            coordinates,
            new GeneralLinearFunction("Location"),
            new GeneralLinearFunction("Scale"),
            new GeneralLinearFunction("Shape"));
        model.SpatialDependence = new GaussianCopula(coordinates, CorrelationFunctionType.Exponential);
        model.UseCopulaDependence = true;
        model.SetDefaultParameters();
        var analysis = new SpatialGEVAnalysis(model);
        int parameterCount = model.NumberOfParameters;
        TimeSeriesIndependentRecoveryTests.AssertResolvedBayesianDefaults("SpatialGEV missing-site model", analysis.BayesianAnalysis, parameterCount);

        Exception? completionError = null;
        analysis.AnalysisCompleted += (_, e) => completionError = e.Error;

        await analysis.RunAsync();

        Assert.IsTrue(
            analysis.IsEstimated,
            $"The spatial analysis must complete under the production defaults. Sampler error: {analysis.BayesianAnalysis.LastError}; completion error: {completionError}");
        TimeSeriesIndependentRecoveryTests.AssertResolvedBayesianDefaults("SpatialGEV missing-site model (after run)", analysis.BayesianAnalysis, parameterCount);
        var results = analysis.BayesianAnalysis.Results!;
        Assert.IsNotNull(analysis.AnalysisResults, "The regional results hold the criteria.");

        // Row/year blocks: twelve rows, one fully missing.
        int rows = model.Observations;
        int nonEmptyRows = 0;
        int observedCells = 0;
        for (int i = 0; i < rows; i++)
        {
            bool any = false;
            for (int j = 0; j < model.Sites; j++)
            {
                if (!double.IsNaN(data[i, j]))
                {
                    any = true;
                    observedCells++;
                }
            }
            if (any)
                nonEmptyRows++;
        }
        Assert.AreEqual(12, rows);
        Assert.AreEqual(11, nonEmptyRows, "Row 9 of the oracle is fully missing.");

        // AIC/BIC at the sampled MAP from the observation log likelihood.
        double[] map = results.MAP.Values;
        double dataAtMap = model.DataLogLikelihood(map);
        double[] pointwiseAtMap = model.PointwiseDataLogLikelihood(map);
        Assert.IsTrue(double.IsFinite(dataAtMap), "Finite observation log likelihood at the MAP.");
        Assert.AreEqual(0.0, pointwiseAtMap[8], 0.0, "The fully missing row contributes nothing.");
        Assert.AreEqual(dataAtMap, pointwiseAtMap.Sum(), 1e-8, "Scalar equals the row/year sum at the MAP.");
        Assert.AreEqual(GoodnessOfFit.AIC(parameterCount, dataAtMap), analysis.AnalysisResults!.AIC, CriterionTolerance, "AIC at the MAP.");
        Assert.AreEqual(GoodnessOfFit.BIC(nonEmptyRows, parameterCount, dataAtMap), analysis.AnalysisResults.BIC, CriterionTolerance, "BIC with one nonempty row/year block per observation.");
        Assert.AreNotEqual(GoodnessOfFit.BIC(observedCells, parameterCount, dataAtMap), analysis.AnalysisResults.BIC, 1e-6, "Observed site cells are not the BIC sample unit.");
        Assert.AreNotEqual(GoodnessOfFit.BIC(rows, parameterCount, dataAtMap), analysis.AnalysisResults.BIC, 1e-6, "The fully missing row is not counted.");

        // WAIC from the row/year pointwise terms of the retained draws.
        int drawCount = results.Output.Count;
        Assert.IsTrue(drawCount >= 1000, $"Retained draws: {drawCount}.");
        var matrix = new double[rows, drawCount];
        for (int d = 0; d < drawCount; d++)
        {
            double[] pointwise = model.PointwiseDataLogLikelihood(results.Output[d].Values);
            Assert.AreEqual(rows, pointwise.Length, "One pointwise term per row/year.");
            for (int r = 0; r < rows; r++)
                matrix[r, d] = pointwise[r];
        }

        double lppd = 0.0;
        double pWaic = 0.0;
        for (int r = 0; r < rows; r++)
        {
            double max = double.NegativeInfinity;
            for (int d = 0; d < drawCount; d++)
                max = Math.Max(max, matrix[r, d]);
            double sumExp = 0.0;
            double mean = 0.0;
            for (int d = 0; d < drawCount; d++)
            {
                sumExp += Math.Exp(matrix[r, d] - max);
                mean += matrix[r, d];
            }
            mean /= drawCount;
            double variance = 0.0;
            for (int d = 0; d < drawCount; d++)
                variance += (matrix[r, d] - mean) * (matrix[r, d] - mean);
            variance /= drawCount - 1;
            lppd += max + Math.Log(sumExp) - Math.Log(drawCount);
            pWaic += variance;
        }
        double expectedWaic = -2.0 * lppd + 2.0 * pWaic;

        Assert.AreEqual(expectedWaic, analysis.BayesianAnalysis.WAIC, PredictiveRelativeTolerance * Math.Abs(expectedWaic), "WAIC from the row/year matrix.");
        Assert.AreEqual(pWaic, analysis.BayesianAnalysis.WAIC_pD, PredictiveRelativeTolerance * Math.Max(1.0, Math.Abs(pWaic)), "WAIC effective parameters from the row/year matrix.");
        Assert.IsNotNull(analysis.BayesianAnalysis.ParetoK);
        Assert.AreEqual(rows, analysis.BayesianAnalysis.ParetoK!.Length, "One Pareto k per row/year.");
        Assert.IsTrue(double.IsFinite(analysis.BayesianAnalysis.LOOIC), "PSIS-LOO from the same matrix.");

        Console.WriteLine(
            $"MAP {string.Join(", ", map.Select(v => v.ToString("G6")))}; data LL {dataAtMap:F4}; AIC {analysis.AnalysisResults.AIC:F4}; "
            + $"BIC {analysis.AnalysisResults.BIC:F4} (n = {nonEmptyRows}); DIC {analysis.AnalysisResults.DIC:F4}; "
            + $"WAIC {analysis.BayesianAnalysis.WAIC:F4}; LOOIC {analysis.BayesianAnalysis.LOOIC:F4}; "
            + $"max R-hat {results.ParameterResults.Max(p => p.SummaryStatistics.Rhat):F4}; min ESS {results.ParameterResults.Min(p => p.SummaryStatistics.ESS):F0}.");
    }

    /// <summary>
    /// Parses the copied artifact.
    /// </summary>
    /// <returns>The parsed document; the caller disposes it.</returns>
    private static JsonDocument LoadDocument()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "VerificationData", ArtifactFileName);
        return JsonDocument.Parse(File.ReadAllText(path));
    }

    /// <summary>
    /// Reads a JSON array of rows into a matrix, mapping JSON null to NaN.
    /// </summary>
    /// <param name="element">The array-of-arrays element.</param>
    /// <returns>The matrix.</returns>
    private static double[,] ReadMatrix(JsonElement element)
    {
        JsonElement[] rows = element.EnumerateArray().ToArray();
        int columns = rows[0].GetArrayLength();
        var matrix = new double[rows.Length, columns];
        for (int i = 0; i < rows.Length; i++)
        {
            int j = 0;
            foreach (JsonElement cell in rows[i].EnumerateArray())
            {
                matrix[i, j++] = cell.ValueKind == JsonValueKind.Null ? double.NaN : cell.GetDouble();
            }
        }
        return matrix;
    }
}
