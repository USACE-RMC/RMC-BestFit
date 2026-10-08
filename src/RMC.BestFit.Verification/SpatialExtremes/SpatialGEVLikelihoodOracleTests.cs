using System.Text.Json;
using RMC.BestFit.Models.SpatialExtremes;
using RMC.BestFit.Models.TrendFunctions;

namespace RMC.BestFit.Verification.SpatialExtremes;

/// <summary>
/// Verifies the spatial GEV Gaussian-copula likelihood against the committed R <c>mvtnorm</c> oracle
/// <c>spatial-copula-likelihood-oracle.json</c>: observed-site marginalization of missing sites
/// (TR-048), the data/prior decomposition of the latent Gaussian-process location errors (TR-049),
/// and the consistency of the scalar and pointwise estimating equations that the Godambe covariance
/// relies on (TR-057).
/// </summary>
/// <remarks>
/// <para>
/// The oracle defines a homogeneous five-site model on projected coordinates with exponential
/// correlation <c>exp(-h / range)</c>, log links for location and scale, Numerics (Hosking) GEV
/// conventions, and the Gaussian copula density <c>log phi_R(z) - sum log phi(z_j)</c> over the
/// observed sites of each row. Missing sites must be marginalized through the observed-site
/// correlation submatrix; a zero latent score substituted for a missing site and a full-dimensional
/// density is not the observed-data likelihood.
/// </para>
/// <para>
/// For the location-error model the oracle records the data log likelihood without the process
/// density and the Gaussian-process log density separately. The posterior kernel must equal their sum
/// plus the parameter priors regardless of which side of the data/prior decomposition holds the
/// process density, while the data log likelihood itself must contain only the observation terms.
/// </para>
/// </remarks>
[TestClass]
public class SpatialGEVLikelihoodOracleTests
{
    /// <summary>The committed artifact copied to the test output.</summary>
    private const string ArtifactFileName = "spatial-copula-likelihood-oracle.json";

    /// <summary>Absolute tolerance for summed log likelihoods.</summary>
    private const double TotalTolerance = 1e-8;

    /// <summary>Absolute tolerance for per-row log likelihoods.</summary>
    private const double RowTolerance = 1e-10;

    /// <summary>Relative step for the numerical gradients of the estimating-equation check.</summary>
    private const double GradientStep = 1e-5;

    /// <summary>Absolute tolerance for gradient agreement between the scalar and pointwise likelihoods.</summary>
    private const double GradientTolerance = 1e-4;

    /// <summary>
    /// With missing sites, the scalar data log likelihood equals the observed-site marginalized oracle.
    /// </summary>
    [TestMethod]
    public void MissingSites_DataLogLikelihood_UsesObservedSiteCopulaSubmatrix()
    {
        using JsonDocument document = LoadDocument();
        JsonElement section = document.RootElement.GetProperty("missing_site_model");
        SpatialGEV model = BuildModel(document.RootElement, section, withCopula: true, withLocationErrors: false);
        double[] theta = ReadDoubles(section.GetProperty("bestfit_parameters"));
        double expected = section.GetProperty("total_log_likelihood").GetDouble();
        double placeholder = section.GetProperty("total_log_likelihood_zero_placeholder").GetDouble();

        double actual = model.DataLogLikelihood(theta);

        Assert.AreEqual(
            expected,
            actual,
            TotalTolerance,
            $"DataLogLikelihood {actual:G17} versus the observed-site marginalized oracle {expected:G17}; "
            + $"the zero-placeholder full-dimensional evaluation equals {placeholder:G17} (TR-048).");
    }

    /// <summary>
    /// With missing sites, every pointwise row equals the observed-site marginalized oracle row and
    /// the rows sum to the scalar value.
    /// </summary>
    [TestMethod]
    public void MissingSites_PointwiseRows_MatchObservedSubsetOracle()
    {
        using JsonDocument document = LoadDocument();
        JsonElement section = document.RootElement.GetProperty("missing_site_model");
        SpatialGEV model = BuildModel(document.RootElement, section, withCopula: true, withLocationErrors: false);
        double[] theta = ReadDoubles(section.GetProperty("bestfit_parameters"));

        double[] pointwise = model.PointwiseDataLogLikelihood(theta);
        JsonElement rows = section.GetProperty("rows");
        Assert.AreEqual(rows.GetArrayLength(), pointwise.Length, "Pointwise row count.");
        int index = 0;
        foreach (JsonElement row in rows.EnumerateArray())
        {
            double expected = row.GetProperty("row_log_likelihood").GetDouble();
            double placeholder = row.GetProperty("row_log_likelihood_zero_placeholder").GetDouble();
            int observed = row.GetProperty("observed_sites").GetArrayLength();
            Assert.AreEqual(
                expected,
                pointwise[index],
                RowTolerance,
                $"Row {index + 1} ({observed} observed sites): pointwise {pointwise[index]:G17} versus the oracle {expected:G17}; "
                + $"the zero-placeholder evaluation equals {placeholder:G17} (TR-048).");
            index++;
        }
        Assert.AreEqual(model.DataLogLikelihood(theta), pointwise.Sum(), TotalTolerance, "Pointwise sum identity.");
    }

    /// <summary>
    /// With complete rows the copula likelihood equals the oracle, which fixes the GEV and copula
    /// density conventions independently of the missing-site question.
    /// </summary>
    [TestMethod]
    public void CompleteRows_CopulaLikelihood_MatchesIndependentOracle()
    {
        using JsonDocument document = LoadDocument();
        JsonElement section = document.RootElement.GetProperty("complete_data_model");
        SpatialGEV model = BuildModel(document.RootElement, section, withCopula: true, withLocationErrors: false);
        double[] theta = ReadDoubles(section.GetProperty("bestfit_parameters"));

        Assert.AreEqual(
            section.GetProperty("total_log_likelihood").GetDouble(),
            model.DataLogLikelihood(theta),
            TotalTolerance,
            "Complete-row copula log likelihood versus the mvtnorm oracle.");
        double[] pointwise = model.PointwiseDataLogLikelihood(theta);
        int index = 0;
        foreach (JsonElement row in section.GetProperty("rows").EnumerateArray())
        {
            Assert.AreEqual(row.GetProperty("row_log_likelihood").GetDouble(), pointwise[index], RowTolerance, $"Complete row {index + 1}.");
            index++;
        }
    }

    /// <summary>
    /// Without copula dependence the data log likelihood is the sum of the observed-site GEV log
    /// densities, which fixes the GEV convention against the oracle.
    /// </summary>
    [TestMethod]
    public void MarginalOnly_WithoutCopula_MatchesIndependentOracle()
    {
        using JsonDocument document = LoadDocument();
        JsonElement section = document.RootElement.GetProperty("missing_site_model");
        SpatialGEV model = BuildModel(document.RootElement, section, withCopula: false, withLocationErrors: false);
        double[] theta = ReadDoubles(section.GetProperty("bestfit_parameters")).Skip(1).ToArray();

        Assert.AreEqual(
            section.GetProperty("marginal_only_total").GetDouble(),
            model.DataLogLikelihood(theta),
            TotalTolerance,
            "Marginal-only log likelihood versus the Hosking GEV oracle.");
    }

    /// <summary>
    /// The posterior kernel of the location-error model equals the observation log likelihood plus the
    /// Gaussian-process log density plus the parameter priors, whichever side of the data/prior
    /// decomposition holds the process density.
    /// </summary>
    [TestMethod]
    public void LocationErrorModel_PosteriorKernel_IsInvariantToTheDecomposition()
    {
        using JsonDocument document = LoadDocument();
        JsonElement section = document.RootElement.GetProperty("location_error_model");
        SpatialGEV model = BuildModel(document.RootElement, section, withCopula: true, withLocationErrors: true);
        double[] theta = ReadDoubles(section.GetProperty("bestfit_parameters"));
        AssertInsideBounds(model, theta);

        double parameterPriors = 0.0;
        for (int index = 0; index < model.NumberOfParameters; index++)
        {
            parameterPriors += model.Parameters[index].PriorDistribution!.LogPDF(theta[index]);
        }
        double expected = section.GetProperty("data_log_likelihood_without_process_density").GetDouble()
            + section.GetProperty("process_log_density").GetDouble()
            + parameterPriors;

        Assert.AreEqual(expected, model.LogLikelihood(theta), TotalTolerance, "Posterior kernel of the location-error model.");
    }

    /// <summary>
    /// The data log likelihood of the location-error model contains only the observation terms; the
    /// Gaussian-process density is prior structure.
    /// </summary>
    [TestMethod]
    public void LocationErrorModel_DataLogLikelihood_ExcludesProcessDensity()
    {
        using JsonDocument document = LoadDocument();
        JsonElement section = document.RootElement.GetProperty("location_error_model");
        SpatialGEV model = BuildModel(document.RootElement, section, withCopula: true, withLocationErrors: true);
        double[] theta = ReadDoubles(section.GetProperty("bestfit_parameters"));
        double expected = section.GetProperty("data_log_likelihood_without_process_density").GetDouble();
        double process = section.GetProperty("process_log_density").GetDouble();

        double actual = model.DataLogLikelihood(theta);

        Assert.AreEqual(
            expected,
            actual,
            TotalTolerance,
            $"DataLogLikelihood {actual:G17} versus the observation-only oracle {expected:G17}; the Gaussian-process log density is {process:G17} (TR-049).");
    }

    /// <summary>
    /// The scalar data log likelihood equals the sum of the pointwise rows and the prior log likelihood
    /// equals the sum of the pointwise prior components for the location-error model.
    /// </summary>
    [TestMethod]
    public void LocationErrorModel_ScalarAndPointwiseDecompositionsAgree()
    {
        using JsonDocument document = LoadDocument();
        JsonElement section = document.RootElement.GetProperty("location_error_model");
        SpatialGEV model = BuildModel(document.RootElement, section, withCopula: true, withLocationErrors: true);
        double[] theta = ReadDoubles(section.GetProperty("bestfit_parameters"));

        double scalarData = model.DataLogLikelihood(theta);
        double pointwiseData = model.PointwiseDataLogLikelihood(theta).Sum();
        double scalarPrior = model.PriorLogLikelihood(theta);
        double pointwisePrior = model.PointwisePriorLogLikelihood(theta).Sum(component => component.LogLikelihood);

        Assert.AreEqual(scalarData, pointwiseData, TotalTolerance, $"DataLogLikelihood {scalarData:G17} versus the pointwise sum {pointwiseData:G17} (TR-049).");
        Assert.AreEqual(scalarPrior, pointwisePrior, TotalTolerance, $"PriorLogLikelihood {scalarPrior:G17} versus the pointwise prior component sum {pointwisePrior:G17} (TR-049).");
        Assert.AreEqual(model.LogLikelihood(theta), scalarData + scalarPrior, TotalTolerance, "LogLikelihood equals data plus prior.");
    }

    /// <summary>
    /// The gradients of the scalar data log likelihood and of the summed pointwise rows agree, so the
    /// Godambe sensitivity and variability matrices derive from the same estimating equations.
    /// </summary>
    [TestMethod]
    public void LocationErrorModel_ScalarAndPointwiseGradientsAgree()
    {
        using JsonDocument document = LoadDocument();
        JsonElement section = document.RootElement.GetProperty("location_error_model");
        SpatialGEV model = BuildModel(document.RootElement, section, withCopula: true, withLocationErrors: true);
        double[] theta = ReadDoubles(section.GetProperty("bestfit_parameters"));

        for (int index = 0; index < theta.Length; index++)
        {
            double step = Math.Max(Math.Abs(theta[index]) * GradientStep, GradientStep);
            double[] plus = (double[])theta.Clone();
            double[] minus = (double[])theta.Clone();
            plus[index] += step;
            minus[index] -= step;
            double scalarGradient = (model.DataLogLikelihood(plus) - model.DataLogLikelihood(minus)) / (2.0 * step);
            double pointwiseGradient = (model.PointwiseDataLogLikelihood(plus).Sum() - model.PointwiseDataLogLikelihood(minus).Sum()) / (2.0 * step);
            Assert.AreEqual(
                pointwiseGradient,
                scalarGradient,
                GradientTolerance,
                $"{model.Parameters[index].Name}: scalar gradient {scalarGradient:G10} versus pointwise gradient {pointwiseGradient:G10} (TR-057).");
        }
    }

    /// <summary>
    /// Builds the oracle model: five sites, intercept-only trends with log links for location and
    /// scale, optional exponential Gaussian-copula dependence, and optional exponential location errors.
    /// </summary>
    /// <param name="root">The artifact root.</param>
    /// <param name="section">The model section providing the data matrix.</param>
    /// <param name="withCopula">Whether to enable the Gaussian copula.</param>
    /// <param name="withLocationErrors">Whether to enable the latent location errors.</param>
    /// <returns>The configured model with its default parameter list rebuilt.</returns>
    private static SpatialGEV BuildModel(JsonElement root, JsonElement section, bool withCopula, bool withLocationErrors)
    {
        double[,] coordinates = ReadMatrix(root.GetProperty("network").GetProperty("coordinates"));
        double[,] data = ReadMatrix(section.GetProperty("data"));
        var model = new SpatialGEV(
            data,
            coordinates,
            new GeneralLinearFunction("Location"),
            new GeneralLinearFunction("Scale"),
            new GeneralLinearFunction("Shape"));
        if (withCopula)
        {
            model.SpatialDependence = new GaussianCopula(coordinates, CorrelationFunctionType.Exponential);
            model.UseCopulaDependence = true;
        }
        if (withLocationErrors)
        {
            model.LocationErrors = new SpatialRegressionErrors(coordinates, CorrelationFunctionType.Exponential);
            model.UseLocationErrors = true;
        }
        model.SetDefaultParameters();
        Assert.IsTrue(model.UseLogLinkForLocation && model.UseLogLinkForScale, "Log links for location and scale.");
        return model;
    }

    /// <summary>
    /// Asserts that a parameter vector lies inside the model's default bounds.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="theta">The parameter vector.</param>
    private static void AssertInsideBounds(SpatialGEV model, double[] theta)
    {
        Assert.AreEqual(model.NumberOfParameters, theta.Length, "Parameter count.");
        for (int index = 0; index < theta.Length; index++)
        {
            var parameter = model.Parameters[index];
            Assert.IsTrue(
                theta[index] > parameter.LowerBound && theta[index] < parameter.UpperBound,
                $"{parameter.Name} = {theta[index]:G17} must lie strictly inside [{parameter.LowerBound:G17}, {parameter.UpperBound:G17}].");
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
