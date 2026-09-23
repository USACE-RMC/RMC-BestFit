using System.Text.Json;
using RMC.BestFit.Models.SpatialExtremes;

namespace RMC.BestFit.Verification.SpatialExtremes;

/// <summary>
/// Verifies the latent-error conditional Gaussian-process predictor (simple kriging) of
/// <see cref="SpatialRegressionErrors"/> against the committed R oracle
/// <c>spatial-conditional-gp-oracle.json</c>: conditional mean and variance at ungauged locations for
/// three parameter sets on the five-site network (TR-054 routes the analysis-level ungauged prediction
/// through this predictor).
/// </summary>
[TestClass]
public class SpatialGEVKrigingOracleTests
{
    /// <summary>The committed artifact copied to the test output.</summary>
    private const string ArtifactFileName = "spatial-conditional-gp-oracle.json";

    /// <summary>Absolute tolerance for the conditional mean and variance.</summary>
    private const double Tolerance = 1e-10;

    /// <summary>
    /// Every oracle case's conditional mean and variance are reproduced by <c>GetKrigingPrediction</c>;
    /// at a site coordinate the conditional mean equals the site's latent error and the variance is zero.
    /// </summary>
    [TestMethod]
    public void KrigingPrediction_MatchesConditionalGaussianProcessOracle()
    {
        using JsonDocument document = LoadDocument();
        JsonElement root = document.RootElement;
        double[,] coordinates = ReadMatrix(root.GetProperty("network").GetProperty("coordinates"));
        int cases = 0;
        foreach (JsonElement item in root.GetProperty("cases").EnumerateArray())
        {
            double sigma = item.GetProperty("sigma").GetDouble();
            double range = item.GetProperty("range").GetDouble();
            double[] errors = ReadDoubles(item.GetProperty("errors"));
            double[] target = ReadDoubles(item.GetProperty("target"));
            double expectedMean = item.GetProperty("conditional_mean").GetDouble();
            double expectedVariance = item.GetProperty("conditional_variance").GetDouble();
            string label = $"{item.GetProperty("parameter_set").GetString()} at ({target[0]}, {target[1]})";

            var model = new SpatialRegressionErrors(coordinates, CorrelationFunctionType.Exponential);
            var values = new List<double> { sigma, range };
            values.AddRange(errors);
            model.SetParameterValues(values);

            var (mean, variance) = model.GetKrigingPrediction(target);

            Assert.AreEqual(expectedMean, mean, Tolerance, $"Conditional mean, {label}.");
            Assert.AreEqual(expectedVariance, variance, Tolerance, $"Conditional variance, {label}.");
            if (item.GetProperty("target_is_site").GetBoolean())
            {
                Assert.AreEqual(0.0, variance, 1e-9, $"Zero conditional variance at a site, {label}.");
            }
            cases++;
        }
        Assert.AreEqual(15, cases, "Fifteen oracle cases.");
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
    /// Reads a JSON array of numbers.
    /// </summary>
    /// <param name="element">The array element.</param>
    /// <returns>The values.</returns>
    private static double[] ReadDoubles(JsonElement element) =>
        element.EnumerateArray().Select(item => item.GetDouble()).ToArray();

    /// <summary>
    /// Reads a JSON array of rows into a matrix.
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
                matrix[i, j++] = cell.GetDouble();
        }
        return matrix;
    }
}
