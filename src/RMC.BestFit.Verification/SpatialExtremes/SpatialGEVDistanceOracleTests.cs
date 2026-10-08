using System.Text.Json;
using RMC.BestFit.Models.SpatialExtremes;

namespace RMC.BestFit.Verification.SpatialExtremes;

/// <summary>
/// Verifies the geodesic distance metric of the spatial GEV components against the committed R oracle
/// <c>geodesic-distance-oracle.json</c> (TR-060): the great-circle site separations of
/// <see cref="SpatialRegressionErrors"/>, the copula correlation built from them, and the simple-kriging
/// prediction at target latitude/longitude points; and that the Cartesian metric reproduces the planar
/// Euclidean distances used before the metric option existed.
/// </summary>
[TestClass]
public class SpatialGEVDistanceOracleTests
{
    /// <summary>The committed artifact copied to the test output.</summary>
    private const string ArtifactFileName = "geodesic-distance-oracle.json";

    /// <summary>Absolute tolerance for distances in kilometres.</summary>
    private const double DistanceTolerance = 1e-9;

    /// <summary>Absolute tolerance for correlations and kriging moments.</summary>
    private const double MomentTolerance = 1e-10;

    /// <summary>
    /// The geodesic distance matrix, the exponential correlation at the oracle range, and the kriging
    /// prediction at the five targets match the R haversine oracle.
    /// </summary>
    [TestMethod]
    public void GeodesicMetric_MatchesHaversineOracle()
    {
        using JsonDocument document = LoadDocument();
        JsonElement root = document.RootElement;
        double[,] sites = ReadMatrix(root.GetProperty("sites"));
        double[,] expectedDistances = ReadMatrix(root.GetProperty("distance_matrix_km"));
        double[,] expectedCorrelation = ReadMatrix(root.GetProperty("correlation_matrix"));
        double range = root.GetProperty("range_km").GetDouble();
        double sigma = root.GetProperty("error_scale").GetDouble();
        double[] errors = ReadDoubles(root.GetProperty("errors"));
        int n = sites.GetLength(0);

        var errorModel = new SpatialRegressionErrors(sites, CorrelationFunctionType.Exponential, 10, SpatialDistanceMetric.Geodesic);
        var copula = new GaussianCopula(sites, CorrelationFunctionType.Exponential, SpatialDistanceMetric.Geodesic);
        Assert.AreEqual(SpatialDistanceMetric.Geodesic, errorModel.DistanceMetric);
        Assert.AreEqual(SpatialDistanceMetric.Geodesic, copula.DistanceMetric);

        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
                Assert.AreEqual(expectedDistances[i, j], errorModel.DistanceMatrix[i, j], DistanceTolerance, $"Distance ({i + 1}, {j + 1}) km.");
        }

        copula.SetParameterValues(new List<double> { range });
        double[,] correlation = copula.GetCorrelationMatrix()!;
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
                Assert.AreEqual(expectedCorrelation[i, j], correlation[i, j], MomentTolerance, $"Correlation ({i + 1}, {j + 1}) at {range} km.");
        }

        var values = new List<double> { sigma, range };
        values.AddRange(errors);
        errorModel.SetParameterValues(values);
        int cases = 0;
        foreach (JsonElement item in root.GetProperty("kriging").EnumerateArray())
        {
            double[] target = ReadDoubles(item.GetProperty("target"));
            var (mean, variance) = errorModel.GetKrigingPrediction(target);
            Assert.AreEqual(item.GetProperty("conditional_mean").GetDouble(), mean, MomentTolerance, $"Conditional mean at ({target[0]}, {target[1]}).");
            Assert.AreEqual(item.GetProperty("conditional_variance").GetDouble(), variance, MomentTolerance, $"Conditional variance at ({target[0]}, {target[1]}).");
            cases++;
        }
        Assert.AreEqual(5, cases, "Five target points.");
        Console.WriteLine($"Geodesic network: site 1 to site 2 {errorModel.DistanceMatrix[0, 1]:F3} km, site 1 to site 5 {errorModel.DistanceMatrix[0, 4]:F3} km.");
    }

    /// <summary>
    /// The Cartesian metric (the default) reproduces the planar Euclidean distances, so projected networks
    /// are unchanged by the metric option; the same coordinates interpreted geodesically give kilometres.
    /// </summary>
    [TestMethod]
    public void CartesianMetric_IsPlanarEuclidean()
    {
        using JsonDocument document = LoadDocument();
        double[,] sites = ReadMatrix(document.RootElement.GetProperty("sites"));
        int n = sites.GetLength(0);

        var cartesian = new SpatialRegressionErrors(sites, CorrelationFunctionType.Exponential);
        var implicitDefault = new GaussianCopula(sites, CorrelationFunctionType.Exponential);

        Assert.AreEqual(SpatialDistanceMetric.Cartesian, cartesian.DistanceMetric, "Cartesian by default.");
        Assert.AreEqual(SpatialDistanceMetric.Cartesian, implicitDefault.DistanceMetric, "Cartesian by default.");
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                double euclidean = Numerics.Tools.Distance(sites[i, 0], sites[i, 1], sites[j, 0], sites[j, 1]);
                Assert.AreEqual(euclidean, cartesian.DistanceMatrix[i, j], 0.0, $"Planar distance ({i + 1}, {j + 1}) in degrees.");
            }
        }
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
