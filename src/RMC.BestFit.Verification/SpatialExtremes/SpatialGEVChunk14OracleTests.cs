using System.Text.Json;
using System.Xml.Linq;
using Numerics.Data;
using Numerics.Distributions;
using Numerics.Mathematics.Optimization;
using Numerics.Sampling.MCMC;
using RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using RMC.BestFit.Models.SpatialExtremes;
using RMC.BestFit.Models.TrendFunctions;

namespace RMC.BestFit.Verification.SpatialExtremes;

/// <summary>
/// Verifies the Chunk 14 spatial correlation, held-out prediction, posterior aggregation, Godambe,
/// block-bootstrap, and variance-inflation formulas against independently implemented Python targets.
/// </summary>
/// <remarks>
/// The frozen artifact is generated without calling BestFit. It declares Cartesian and geodesic distance
/// conventions, declared link-space and physical parameter orders, fixed draws and seeds, row/year blocks, uncertainty sources,
/// and analytical tolerances. Fast tests retain ownership of dispatch, validation, and result-state contracts.
/// </remarks>
[TestClass]
public class SpatialGEVChunk14OracleTests
{
    /// <summary>The committed artifact copied to the test output.</summary>
    private const string ArtifactFileName = "chunk14-independent-oracle.json";

    /// <summary>
    /// Absolute tolerance for deterministic closed-form, dense-linear-algebra, and serialized-double
    /// comparisons; this covers cross-runtime summation roundoff while remaining below reported effects.
    /// </summary>
    private const double FormulaTolerance = 1e-9;

    /// <summary>
    /// Relative tolerance for independently accumulated finite-difference matrices; this covers cancellation
    /// and step-rounding differences without regularizing the sensitivity or sandwich matrix.
    /// </summary>
    private const double MatrixRelativeTolerance = 2e-5;

    /// <summary>Verifies rho(h)=exp(-h/range), including zero and the declared range distance.</summary>
    [TestMethod]
    public void BasicExponentialCorrelation_MatchesAnalyticalGrid()
    {
        AssertCorrelationGrid("basic_exponential", new BasicExponential());
    }

    /// <summary>Verifies rho(h)=exp(-(h/range)^smoothness) for a non-exponential smoothness.</summary>
    [TestMethod]
    public void PoweredExponentialCorrelation_MatchesAnalyticalGrid()
    {
        AssertCorrelationGrid("powered_exponential", new PoweredExponential());
    }

    /// <summary>Verifies the compact-support spherical polynomial at, inside, and beyond its range.</summary>
    [TestMethod]
    public void SphericalCorrelation_MatchesAnalyticalGridAndCompactSupport()
    {
        AssertCorrelationGrid("spherical", new Spherical());
    }

    /// <summary>
    /// Verifies a complete Gaussian-copula training fold against an independently optimized reduced-fold
    /// likelihood and its held-out quantiles, without invoking production model reduction or the production
    /// cross-validation constructor.
    /// </summary>
    [TestMethod]
    public void HeldOutCopulaFold_MatchesIndependentFittedOracle()
    {
        using JsonDocument document = LoadDocument();
        JsonElement fold = document.RootElement.GetProperty("cross_validation").GetProperty("copula_complete_fold");
        double[,] data = ReadMatrix(fold.GetProperty("training_data"));
        double[,] coordinates = ReadMatrix(fold.GetProperty("training_coordinates"));
        double[] parent = ReadDoubles(fold.GetProperty("parent_parameters"));
        double[] independentOptimum = ReadDoubles(fold.GetProperty("independent_optimum"));
        double[,] bounds = ReadMatrix(fold.GetProperty("parameter_bounds"));
        var model = new SpatialGEV(
            data,
            coordinates,
            new GeneralLinearFunction("Location"),
            new GeneralLinearFunction("Scale"),
            new GeneralLinearFunction("Shape"));
        model.SpatialDependence = new GaussianCopula(coordinates, CorrelationFunctionType.Exponential);
        model.UseCopulaDependence = true;
        model.SetDefaultParameters();
        for (int parameter = 0; parameter < model.NumberOfParameters; parameter++)
        {
            model.Parameters[parameter].LowerBound = bounds[parameter, 0];
            model.Parameters[parameter].UpperBound = bounds[parameter, 1];
        }
        model.SetParameterValues(parent);
        double independentMaximum = fold.GetProperty("independent_maximum_log_likelihood").GetDouble();
        Assert.AreEqual(independentMaximum, model.DataLogLikelihood(independentOptimum), 1e-7, "Same-point reduced-fold copula likelihood.");
        var estimator = new MaximumLikelihood(model, OptimizationMethod.DifferentialEvolution);

        estimator.Estimate();

        Assert.AreEqual(OptimizationMethod.DifferentialEvolution, estimator.OptimizerMethod, "The production default-policy optimizer remains Differential Evolution.");
        Assert.IsTrue(estimator.IsEstimated, $"Reduced-fold MLE failed with status {estimator.Status}.");
        Assert.IsTrue(double.IsFinite(independentMaximum), "Independent reduced-fold optimum log likelihood must be finite.");
        Assert.IsTrue(double.IsFinite(estimator.MaximumLogLikelihood), "Production reduced-fold optimum log likelihood must be finite.");
        double likelihoodRatioStatistic = 2d * Math.Abs(independentMaximum - estimator.MaximumLogLikelihood);
        double likelihoodRatioCutoff = fold.GetProperty("joint_95_likelihood_ratio_cutoff").GetDouble();
        Assert.IsTrue(
            likelihoodRatioStatistic <= likelihoodRatioCutoff,
            $"Production reduced-fold optimum must lie inside the four-coordinate joint 95% " +
            $"likelihood-ratio region: statistic={likelihoodRatioStatistic:R}, cutoff={likelihoodRatioCutoff:R}.");
        model.SetParameterValues(estimator.BestParameterSet.Values);
        double[] probabilities = ReadDoubles(fold.GetProperty("prediction_exceedance_probabilities"));
        double[] expected = ReadDoubles(fold.GetProperty("held_out_quantiles"));
        double[] standardErrors = ReadDoubles(fold.GetProperty("held_out_quantile_standard_errors"));
        for (int index = 0; index < probabilities.Length; index++)
        {
            double prediction = model.InverseCDF(1.0 - probabilities[index], 0);
            Assert.IsTrue(
                Math.Abs(prediction - expected[index]) <= 1.96 * standardErrors[index],
                $"Held-out quantile at exceedance probability {probabilities[index]}: production {prediction:G12}, independent {expected[index]:G12} +/- {1.96 * standardErrors[index]:G12}.");
        }
        CollectionAssert.AreEqual(new[] { 3, 2 }, ReadInts(fold.GetProperty("training_network_dimensions")), "Three training sites with two coordinates each.");
    }

    /// <summary>
    /// Verifies held-out covariate evaluation from independently fitted fixed-site summaries, including
    /// the declared log-link to the physical GEV location.
    /// </summary>
    [TestMethod]
    public void HeldOutCovariateFold_MatchesIndependentRegressionOracle()
    {
        using JsonDocument document = LoadDocument();
        JsonElement fold = document.RootElement.GetProperty("cross_validation").GetProperty("covariate_complete_fold");
        double[,] covariates = ReadMatrix(fold.GetProperty("training_covariates"));
        double[] responses = ReadDoubles(fold.GetProperty("training_log_locations"));
        var design = new double[covariates.GetLength(0), 3];
        for (int row = 0; row < design.GetLength(0); row++)
        {
            design[row, 0] = 1.0;
            design[row, 1] = covariates[row, 0];
            design[row, 2] = covariates[row, 1];
        }
        double[,] crossProduct = Multiply(Transpose(design), design);
        double[,] inverseCrossProduct = Invert3By3(crossProduct);
        double[] coefficients = Multiply(inverseCrossProduct, Multiply(Transpose(design), responses));
        double residualSumSquares = 0.0;
        for (int row = 0; row < design.GetLength(0); row++)
        {
            double residual = responses[row] - Dot(GetRow(design, row), coefficients);
            residualSumSquares += residual * residual;
        }
        double residualVariance = residualSumSquares / (design.GetLength(0) - design.GetLength(1));
        double[] heldOut = ReadDoubles(fold.GetProperty("held_out_covariates"));
        double[] heldOutDesign = { 1.0, heldOut[0], heldOut[1] };
        double parameterVariance = residualVariance * Dot(heldOutDesign, Multiply(inverseCrossProduct, heldOutDesign));
        var trend = new GeneralLinearFunction("Location", covariates);
        trend.SetParameterValues(coefficients);

        double linkMean = trend.PredictWithCovariates(heldOut);

        double[] expectedCoefficients = ReadDoubles(fold.GetProperty("coefficients"));
        for (int index = 0; index < coefficients.Length; index++)
            Assert.AreEqual(expectedCoefficients[index], coefficients[index], FormulaTolerance, $"Independent normal-equation OLS coefficient {index + 1}.");
        Assert.AreEqual(fold.GetProperty("held_out_link_mean").GetDouble(), linkMean, FormulaTolerance, "Held-out link-space location.");
        Assert.AreEqual(fold.GetProperty("held_out_physical_location").GetDouble(), Math.Exp(linkMean), FormulaTolerance, "Held-out physical-space location.");
        Assert.AreEqual(fold.GetProperty("residual_variance").GetDouble(), residualVariance, FormulaTolerance, "Independently recomputed residual variance.");
        Assert.AreEqual(fold.GetProperty("parameter_prediction_variance").GetDouble(), parameterVariance, FormulaTolerance, "Independently recomputed parameter prediction variance.");
        Assert.AreEqual(fold.GetProperty("observation_prediction_variance").GetDouble(), parameterVariance + residualVariance, FormulaTolerance, "Independently recomputed observation prediction variance.");
    }

    /// <summary>
    /// Verifies draw-specific geodesic conditional-GP mean, variance, and log-link prediction at an
    /// ungauged site against fixed Python draws.
    /// </summary>
    [TestMethod]
    public void UngaugedDrawSpecificPrediction_MatchesIndependentGeodesicGaussianOracle()
    {
        using JsonDocument document = LoadDocument();
        JsonElement oracle = document.RootElement.GetProperty("prediction").GetProperty("draw_specific_gp");
        double[,] coordinates = ReadMatrix(oracle.GetProperty("training_coordinates"));
        double[] target = ReadDoubles(oracle.GetProperty("target_coordinates"));
        int drawCount = 0;
        foreach (JsonElement draw in oracle.GetProperty("draws").EnumerateArray())
        {
            var errors = new SpatialRegressionErrors(
                coordinates,
                CorrelationFunctionType.Exponential,
                maxError: 10.0,
                distanceMetric: SpatialDistanceMetric.Geodesic);
            errors.SetParameterValues(ReadDoubles(draw.GetProperty("parameters")));

            var (mean, variance) = errors.GetKrigingPrediction(target);
            double physicalLocation = Math.Exp(draw.GetProperty("log_location_trend").GetDouble() + mean);

            Assert.AreEqual(draw.GetProperty("conditional_mean").GetDouble(), mean, FormulaTolerance, $"Conditional mean, draw {drawCount + 1}.");
            Assert.AreEqual(draw.GetProperty("conditional_variance").GetDouble(), variance, FormulaTolerance, $"Conditional variance, draw {drawCount + 1}.");
            Assert.AreEqual(draw.GetProperty("physical_location").GetDouble(), physicalLocation, FormulaTolerance, $"Physical location, draw {drawCount + 1}.");
            drawCount++;
        }
        Assert.AreEqual(4, drawCount, "Four predeclared fixed draws.");
    }

    /// <summary>
    /// Verifies that the regional curve is the posterior mean and equal-tailed 95 percent interval of
    /// the per-draw regional mean quantile supplied independently for three nonexchangeable sites.
    /// </summary>
    [TestMethod]
    public async Task RegionalFixedDrawAggregation_MatchesIndependentPosteriorOracle()
    {
        using JsonDocument document = LoadDocument();
        JsonElement oracle = document.RootElement.GetProperty("prediction").GetProperty("regional_fixed_draws");
        double[] siteCovariates = ReadDoubles(oracle.GetProperty("site_covariates"));
        double[,] covariates = ToColumnMatrix(siteCovariates);
        double[,] data = ReadMatrix(document.RootElement.GetProperty("variance_inflation").GetProperty("data"));
        double[,] coordinates = { { 0.0, 0.0 }, { 10.0, 0.0 }, { 0.0, 12.0 } };
        var model = new SpatialGEV(
            data,
            coordinates,
            new GeneralLinearFunction("Location", covariates),
            new GeneralLinearFunction("Scale"),
            new GeneralLinearFunction("Shape"));
        List<double[]> draws = ReadRows(oracle.GetProperty("draws"));
        SpatialGEVAnalysis analysis = CreateEstimatedAnalysis(model, draws, credibleIntervalWidth: 0.95);

        await analysis.RebuildPosteriorResultsAsync();

        Assert.IsNotNull(analysis.AnalysisResults, "Regional results must be published.");
        UncertaintyAnalysisResults results = analysis.AnalysisResults!;
        int row = 0;
        foreach (JsonElement summary in oracle.GetProperty("summaries").EnumerateArray())
        {
            double probability = summary.GetProperty("exceedance_probability").GetDouble();
            Assert.AreEqual(probability, results.ConfidenceIntervals![row, 0], 0.0, $"Probability row {row + 1}.");
            Assert.AreEqual(summary.GetProperty("mean").GetDouble(), results.MeanCurve![row], FormulaTolerance, $"Regional posterior mean at p={probability}.");
            Assert.AreEqual(summary.GetProperty("lower_95").GetDouble(), results.ConfidenceIntervals[row, 1], FormulaTolerance, $"Regional lower 95 percent bound at p={probability}.");
            Assert.AreEqual(summary.GetProperty("upper_95").GetDouble(), results.ConfidenceIntervals[row, 2], FormulaTolerance, $"Regional upper 95 percent bound at p={probability}.");
            row++;
        }
        Assert.AreEqual(3, row, "Three predeclared prediction ordinates.");
    }

    /// <summary>
    /// Verifies that independently differentiated sensitivity H and variability J reconstruct the frozen
    /// H-inverse J H-inverse target, then compares the production sandwich covariance with that target.
    /// </summary>
    [TestMethod]
    public void GodambeSensitivityVariabilityAndSandwich_MatchIndependentOracle()
    {
        using JsonDocument document = LoadDocument();
        JsonElement oracle = document.RootElement.GetProperty("godambe");
        double[,] data = ReadMatrix(oracle.GetProperty("data"));
        double[,] coordinates = ReadMatrix(oracle.GetProperty("coordinates"));
        double[] parameters = ReadDoubles(oracle.GetProperty("parameters"));
        var model = new SpatialGEV(
            data,
            coordinates,
            new GeneralLinearFunction("Location"),
            new GeneralLinearFunction("Scale"),
            new GeneralLinearFunction("Shape"));
        model.SetParameterValues(parameters);
        var analysis = new SpatialGEVAnalysis(model);

        double[,]? actual = analysis.ComputeGodambeCovariance(parameters);

        Assert.IsNotNull(actual, analysis.GodambeCovarianceDiagnostic);
        Assert.AreEqual(CovarianceComputationStatus.Available, analysis.GodambeCovarianceStatus, analysis.GodambeCovarianceDiagnostic);
        double[,] expectedH = ReadMatrix(oracle.GetProperty("h_sensitivity"));
        double[,] expectedJ = ReadMatrix(oracle.GetProperty("j_variability"));
        double[,] expected = ReadMatrix(oracle.GetProperty("sandwich_covariance"));
        double[,] reconstructed = Multiply(Multiply(Invert3By3(expectedH), expectedJ), Invert3By3(expectedH));
        AssertMatrix(expected, reconstructed, FormulaTolerance, "The frozen H and J reconstruct the frozen sandwich covariance.");
        AssertMatrix(expected, actual!, MatrixRelativeTolerance, "Production Godambe covariance versus the independent H/J construction.");
        Assert.AreEqual(24, oracle.GetProperty("row_year_blocks").GetInt32(), "Variability uses 24 complete row/year score blocks.");
    }

    /// <summary>
    /// Verifies production temporal whole-row bootstrap inference against independently fitted SciPy MAP
    /// replicates and their predeclared Type-7 site and regional output intervals.
    /// </summary>
    [TestMethod]
    public async Task TemporalBlockBootstrap_MatchesIndependentWholeRowOracle()
    {
        using JsonDocument document = LoadDocument();
        JsonElement oracle = document.RootElement.GetProperty("bootstrap");
        int observations = oracle.GetProperty("observations").GetInt32();
        int blockSize = oracle.GetProperty("block_size").GetInt32();
        int seed = oracle.GetProperty("seed").GetInt32();
        int replicates = oracle.GetProperty("replicates").GetInt32();
        double[,] data = ReadMatrix(oracle.GetProperty("data"));
        double[,] coordinates = ReadMatrix(oracle.GetProperty("coordinates"));
        double[,] bounds = ReadMatrix(oracle.GetProperty("parameter_bounds"));
        double[] fullOptimum = ReadDoubles(oracle.GetProperty("independent_full_optimum"));
        double[] probabilities = ReadDoubles(oracle.GetProperty("exceedance_probabilities"));
        double relativeTolerance = oracle.GetProperty("fitted_output_relative_tolerance").GetDouble();
        var model = new SpatialGEV(
            data,
            coordinates,
            new GeneralLinearFunction("Location"),
            new GeneralLinearFunction("Scale"),
            new GeneralLinearFunction("Shape"));
        for (int parameter = 0; parameter < model.NumberOfParameters; parameter++)
        {
            Assert.AreEqual(bounds[parameter, 0], model.Parameters[parameter].LowerBound, FormulaTolerance, $"Full-model lower bound, parameter {parameter + 1}.");
            Assert.AreEqual(bounds[parameter, 1], model.Parameters[parameter].UpperBound, FormulaTolerance, $"Full-model upper bound, parameter {parameter + 1}.");
        }

        foreach (JsonElement fit in oracle.GetProperty("independent_replicate_fits").EnumerateArray())
        {
            double[] optimum = ReadDoubles(fit.GetProperty("optimum"));
            double[] physical = ReadDoubles(fit.GetProperty("physical_parameters"));
            Assert.AreEqual(Math.Exp(optimum[0]), physical[0], FormulaTolerance, "Independent fitted location link transformation.");
            Assert.AreEqual(Math.Exp(optimum[1]), physical[1], FormulaTolerance, "Independent fitted scale link transformation.");
            Assert.AreEqual(optimum[2], physical[2], FormulaTolerance, "Independent fitted shape coordinate.");
            double[] quantiles = ReadDoubles(fit.GetProperty("quantiles"));
            for (int probability = 0; probability < probabilities.Length; probability++)
            {
                Assert.AreEqual(
                    IndependentGevQuantile(1.0 - probabilities[probability], physical[0], physical[1], physical[2]),
                    quantiles[probability],
                    FormulaTolerance,
                    $"Independent fitted quantile, replicate {fit.GetProperty("replicate").GetInt32() + 1}, ordinate {probability + 1}.");
            }
        }

        SpatialGEVAnalysis analysis = CreateEstimatedAnalysis(model, new List<double[]> { fullOptimum }, credibleIntervalWidth: 0.95, prngSeed: seed);
        analysis.ProbabilityOrdinates = new ProbabilityOrdinates(probabilities.ToList());
        await analysis.RebuildPosteriorResultsAsync();

        await analysis.RunSpatialBootstrapAsync(replicates, blockSize);

        Assert.AreEqual(observations, model.Observations, "The declared raw row/year count is unchanged.");
        Assert.IsNotNull(analysis.BootstrapResults);
        Assert.AreEqual(replicates, analysis.BootstrapResults!.RequestedReplicates);
        Assert.AreEqual(oracle.GetProperty("successful_replicates").GetInt32(), analysis.BootstrapResults.SuccessfulReplicates);
        Assert.AreEqual(blockSize, analysis.BootstrapResults.BlockSize);
        Assert.AreEqual(seed, analysis.BootstrapResults.Seed);

        JsonElement summary = oracle.GetProperty("fitted_output_intervals");
        double[] location = ReadDoubles(summary.GetProperty("location_95"));
        double[] scale = ReadDoubles(summary.GetProperty("scale_95"));
        double[] shape = ReadDoubles(summary.GetProperty("shape_95"));
        double[,] quantileIntervals = ReadMatrix(summary.GetProperty("quantile_95"));
        double[,] regionalIntervals = ReadMatrix(summary.GetProperty("regional_quantile_95"));
        foreach (SpatialGEVSiteResults site in analysis.SiteResults!)
        {
            AssertRelative(location[0], site.LocationLower, relativeTolerance, "Independent fitted-bootstrap location lower bound.");
            AssertRelative(location[1], site.LocationUpper, relativeTolerance, "Independent fitted-bootstrap location upper bound.");
            AssertRelative(scale[0], site.ScaleLower, relativeTolerance, "Independent fitted-bootstrap scale lower bound.");
            AssertRelative(scale[1], site.ScaleUpper, relativeTolerance, "Independent fitted-bootstrap scale upper bound.");
            AssertRelative(shape[0], site.ShapeLower, relativeTolerance, "Independent fitted-bootstrap shape lower bound.");
            AssertRelative(shape[1], site.ShapeUpper, relativeTolerance, "Independent fitted-bootstrap shape upper bound.");
            for (int probability = 0; probability < probabilities.Length; probability++)
            {
                AssertRelative(quantileIntervals[probability, 0], site.QuantileLower[probability], relativeTolerance, $"Independent fitted-bootstrap site quantile lower bound, ordinate {probability + 1}.");
                AssertRelative(quantileIntervals[probability, 1], site.QuantileUpper[probability], relativeTolerance, $"Independent fitted-bootstrap site quantile upper bound, ordinate {probability + 1}.");
                AssertRelative(regionalIntervals[probability, 0], analysis.AnalysisResults!.ConfidenceIntervals![probability, 1], relativeTolerance, $"Independent fitted-bootstrap regional lower bound, ordinate {probability + 1}.");
                AssertRelative(regionalIntervals[probability, 1], analysis.AnalysisResults.ConfidenceIntervals[probability, 2], relativeTolerance, $"Independent fitted-bootstrap regional upper bound, ordinate {probability + 1}.");
            }
        }
    }

    /// <summary>
    /// Verifies the analytical variance-inflation factor and exact midpoint-plus/minus-half-width
    /// transformation for site and regional posterior intervals.
    /// </summary>
    [TestMethod]
    public async Task VarianceInflation_UsesExactIndependentAnalyticalTransformation()
    {
        using JsonDocument document = LoadDocument();
        JsonElement oracle = document.RootElement.GetProperty("variance_inflation");
        JsonElement regional = document.RootElement.GetProperty("prediction").GetProperty("regional_fixed_draws");
        double[,] data = ReadMatrix(oracle.GetProperty("data"));
        double[,] coordinates = { { 0.0, 0.0 }, { 10.0, 0.0 }, { 0.0, 12.0 } };
        var model = new SpatialGEV(
            data,
            coordinates,
            new GeneralLinearFunction("Location"),
            new GeneralLinearFunction("Scale"),
            new GeneralLinearFunction("Shape"));
        List<double[]> draws = ReadRows(regional.GetProperty("draws"))
            .Select(draw => new[] { draw[0], draw[2], draw[3] })
            .ToList();
        SpatialGEVAnalysis analysis = CreateEstimatedAnalysis(model, draws, credibleIntervalWidth: 0.95);
        await analysis.RebuildPosteriorResultsAsync();
        SpatialGEVSiteResults site = analysis.SiteResults![0];
        double originalSiteHalfWidth = (site.QuantileUpper[1] - site.QuantileLower[1]) / 2.0;
        double originalSiteMidpoint = site.QuantileMean[1];
        double originalRegionalHalfWidth = (analysis.AnalysisResults!.ConfidenceIntervals![1, 2] - analysis.AnalysisResults.ConfidenceIntervals[1, 1]) / 2.0;
        double originalRegionalMidpoint = analysis.AnalysisResults.MeanCurve![1];
        analysis.UncertaintyMethod = SpatialGEVUncertaintyMethod.BayesianInflated;

        await analysis.ApplyUncertaintyMethodAsync(null);

        double expectedVif = oracle.GetProperty("vif").GetDouble();
        double sqrtVif = oracle.GetProperty("sqrt_vif").GetDouble();
        Assert.AreEqual(expectedVif, analysis.VarianceInflationFactor, FormulaTolerance, "Analytical VIF from the fixed complete-site matrix.");
        Assert.AreEqual(originalSiteMidpoint - originalSiteHalfWidth * sqrtVif, site.QuantileLower[1], FormulaTolerance, "Exact site lower transformation.");
        Assert.AreEqual(originalSiteMidpoint + originalSiteHalfWidth * sqrtVif, site.QuantileUpper[1], FormulaTolerance, "Exact site upper transformation.");
        Assert.AreEqual(originalRegionalMidpoint - originalRegionalHalfWidth * sqrtVif, analysis.AnalysisResults.ConfidenceIntervals[1, 1], FormulaTolerance, "Exact regional lower transformation.");
        Assert.AreEqual(originalRegionalMidpoint + originalRegionalHalfWidth * sqrtVif, analysis.AnalysisResults.ConfidenceIntervals[1, 2], FormulaTolerance, "Exact regional upper transformation.");
    }

    /// <summary>Compares one production correlation model with one artifact grid.</summary>
    /// <param name="key">The JSON property containing the grid.</param>
    /// <param name="model">The production correlation model.</param>
    private static void AssertCorrelationGrid(string key, ICorrelationModel model)
    {
        using JsonDocument document = LoadDocument();
        JsonElement oracle = document.RootElement.GetProperty("correlations").GetProperty(key);
        double[] parameters = ReadDoubles(oracle.GetProperty("parameters"));
        double[] distances = ReadDoubles(oracle.GetProperty("distances"));
        double[] expected = ReadDoubles(oracle.GetProperty("correlations"));
        model.SetParameterValues(parameters);
        for (int i = 0; i < distances.Length; i++)
            Assert.AreEqual(expected[i], model.Evaluate(distances[i]), FormulaTolerance, $"{model.Type} correlation at h={distances[i]}.");
    }

    /// <summary>Builds an estimated analysis from fixed independently supplied draws without running a sampler.</summary>
    /// <param name="model">The spatial model.</param>
    /// <param name="draws">The fixed parameter draws in production order.</param>
    /// <param name="credibleIntervalWidth">The equal-tailed credible interval width.</param>
    /// <param name="prngSeed">Optional seed to serialize before the estimated state is restored.</param>
    /// <returns>The restored estimated analysis.</returns>
    private static SpatialGEVAnalysis CreateEstimatedAnalysis(SpatialGEV model, IReadOnlyList<double[]> draws, double credibleIntervalWidth, int? prngSeed = null)
    {
        var sets = draws.Select(values => new ParameterSet(values, model.LogLikelihood(values))).ToList();
        ParameterSet best = sets.OrderByDescending(set => set.Fitness).First();
        var results = new MCMCResults(new ParameterSet(best.Values, best.Fitness), sets, alpha: 1.0 - credibleIntervalWidth);
        var seed = new SpatialGEVAnalysis(model);
        seed.BayesianAnalysis.CredibleIntervalWidth = credibleIntervalWidth;
        if (prngSeed.HasValue)
            seed.BayesianAnalysis.PRNGSeed = prngSeed.Value;
        seed.ProbabilityOrdinates = new ProbabilityOrdinates(new List<double> { 0.5, 0.1, 0.02 });
        XElement xml = seed.ToXElement();
        xml.SetAttributeValue("IsEstimated", true);
        var analysis = new SpatialGEVAnalysis(model, xml);
        analysis.BayesianAnalysis.CredibleIntervalWidth = credibleIntervalWidth;
        analysis.BayesianAnalysis.SetCustomMCMCResults(results, skipInformationCriteria: true);
        Assert.IsTrue(analysis.IsEstimated, "The injected posterior must restore the estimated state.");
        return analysis;
    }

    /// <summary>Parses the copied Chunk 14 artifact.</summary>
    /// <returns>The parsed document; the caller disposes it.</returns>
    private static JsonDocument LoadDocument()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "VerificationData", ArtifactFileName);
        return JsonDocument.Parse(File.ReadAllText(path));
    }

    /// <summary>Reads a JSON numeric array.</summary>
    /// <param name="element">The numeric array.</param>
    /// <returns>The values.</returns>
    private static double[] ReadDoubles(JsonElement element) =>
        element.EnumerateArray().Select(item => item.GetDouble()).ToArray();

    /// <summary>Reads a JSON integer array.</summary>
    /// <param name="element">The integer array.</param>
    /// <returns>The values.</returns>
    private static int[] ReadInts(JsonElement element) =>
        element.EnumerateArray().Select(item => item.GetInt32()).ToArray();

    /// <summary>Reads a JSON array of rows as jagged arrays.</summary>
    /// <param name="element">The array of rows.</param>
    /// <returns>The row list.</returns>
    private static List<double[]> ReadRows(JsonElement element) =>
        element.EnumerateArray().Select(ReadDoubles).ToList();

    /// <summary>Reads a JSON array of rows into a rectangular matrix.</summary>
    /// <param name="element">The array of rows.</param>
    /// <returns>The matrix.</returns>
    private static double[,] ReadMatrix(JsonElement element)
    {
        JsonElement[] rows = element.EnumerateArray().ToArray();
        int columns = rows[0].GetArrayLength();
        var matrix = new double[rows.Length, columns];
        for (int row = 0; row < rows.Length; row++)
        {
            int column = 0;
            foreach (JsonElement cell in rows[row].EnumerateArray())
                matrix[row, column++] = cell.GetDouble();
        }
        return matrix;
    }

    /// <summary>Converts a vector to an n-by-one covariate matrix.</summary>
    /// <param name="values">The vector.</param>
    /// <returns>The column matrix.</returns>
    private static double[,] ToColumnMatrix(double[] values)
    {
        var matrix = new double[values.Length, 1];
        for (int row = 0; row < values.Length; row++)
            matrix[row, 0] = values[row];
        return matrix;
    }

    /// <summary>Asserts two same-size matrices agree element by element under a relative scale.</summary>
    /// <param name="expected">The expected matrix.</param>
    /// <param name="actual">The actual matrix.</param>
    /// <param name="relativeTolerance">The relative tolerance.</param>
    /// <param name="message">The assertion context.</param>
    private static void AssertMatrix(double[,] expected, double[,] actual, double relativeTolerance, string message)
    {
        Assert.AreEqual(expected.GetLength(0), actual.GetLength(0), message);
        Assert.AreEqual(expected.GetLength(1), actual.GetLength(1), message);
        for (int row = 0; row < expected.GetLength(0); row++)
        {
            for (int column = 0; column < expected.GetLength(1); column++)
            {
                double scale = Math.Max(1.0, Math.Abs(expected[row, column]));
                Assert.AreEqual(expected[row, column], actual[row, column], relativeTolerance * scale, $"{message} Entry ({row + 1}, {column + 1}).");
            }
        }
    }

    /// <summary>Inverts a nonsingular 3-by-3 matrix by cofactors.</summary>
    /// <param name="matrix">The matrix.</param>
    /// <returns>The inverse.</returns>
    private static double[,] Invert3By3(double[,] matrix)
    {
        double determinant =
            matrix[0, 0] * (matrix[1, 1] * matrix[2, 2] - matrix[1, 2] * matrix[2, 1])
            - matrix[0, 1] * (matrix[1, 0] * matrix[2, 2] - matrix[1, 2] * matrix[2, 0])
            + matrix[0, 2] * (matrix[1, 0] * matrix[2, 1] - matrix[1, 1] * matrix[2, 0]);
        Assert.AreNotEqual(0.0, determinant, "The independent sensitivity matrix must be nonsingular.");
        var inverse = new double[3, 3];
        inverse[0, 0] = (matrix[1, 1] * matrix[2, 2] - matrix[1, 2] * matrix[2, 1]) / determinant;
        inverse[0, 1] = (matrix[0, 2] * matrix[2, 1] - matrix[0, 1] * matrix[2, 2]) / determinant;
        inverse[0, 2] = (matrix[0, 1] * matrix[1, 2] - matrix[0, 2] * matrix[1, 1]) / determinant;
        inverse[1, 0] = (matrix[1, 2] * matrix[2, 0] - matrix[1, 0] * matrix[2, 2]) / determinant;
        inverse[1, 1] = (matrix[0, 0] * matrix[2, 2] - matrix[0, 2] * matrix[2, 0]) / determinant;
        inverse[1, 2] = (matrix[0, 2] * matrix[1, 0] - matrix[0, 0] * matrix[1, 2]) / determinant;
        inverse[2, 0] = (matrix[1, 0] * matrix[2, 1] - matrix[1, 1] * matrix[2, 0]) / determinant;
        inverse[2, 1] = (matrix[0, 1] * matrix[2, 0] - matrix[0, 0] * matrix[2, 1]) / determinant;
        inverse[2, 2] = (matrix[0, 0] * matrix[1, 1] - matrix[0, 1] * matrix[1, 0]) / determinant;
        return inverse;
    }

    /// <summary>Multiplies two compatible matrices.</summary>
    /// <param name="left">The left matrix.</param>
    /// <param name="right">The right matrix.</param>
    /// <returns>The matrix product.</returns>
    private static double[,] Multiply(double[,] left, double[,] right)
    {
        var product = new double[left.GetLength(0), right.GetLength(1)];
        for (int row = 0; row < product.GetLength(0); row++)
        {
            for (int column = 0; column < product.GetLength(1); column++)
            {
                for (int inner = 0; inner < left.GetLength(1); inner++)
                    product[row, column] += left[row, inner] * right[inner, column];
            }
        }
        return product;
    }

    /// <summary>Multiplies a matrix by a compatible vector.</summary>
    /// <param name="matrix">The matrix.</param>
    /// <param name="vector">The vector.</param>
    /// <returns>The product vector.</returns>
    private static double[] Multiply(double[,] matrix, double[] vector)
    {
        var product = new double[matrix.GetLength(0)];
        for (int row = 0; row < matrix.GetLength(0); row++)
        {
            for (int column = 0; column < matrix.GetLength(1); column++)
                product[row] += matrix[row, column] * vector[column];
        }
        return product;
    }

    /// <summary>Returns the transpose of a matrix.</summary>
    /// <param name="matrix">The matrix.</param>
    /// <returns>The transposed matrix.</returns>
    private static double[,] Transpose(double[,] matrix)
    {
        var transpose = new double[matrix.GetLength(1), matrix.GetLength(0)];
        for (int row = 0; row < matrix.GetLength(0); row++)
        {
            for (int column = 0; column < matrix.GetLength(1); column++)
                transpose[column, row] = matrix[row, column];
        }
        return transpose;
    }

    /// <summary>Returns one matrix row as a vector.</summary>
    /// <param name="matrix">The matrix.</param>
    /// <param name="row">The row index.</param>
    /// <returns>The row vector.</returns>
    private static double[] GetRow(double[,] matrix, int row)
    {
        var values = new double[matrix.GetLength(1)];
        for (int column = 0; column < values.Length; column++)
            values[column] = matrix[row, column];
        return values;
    }

    /// <summary>Computes the inner product of two vectors.</summary>
    /// <param name="left">The left vector.</param>
    /// <param name="right">The right vector.</param>
    /// <returns>The inner product.</returns>
    private static double Dot(double[] left, double[] right)
    {
        double value = 0.0;
        for (int index = 0; index < left.Length; index++)
            value += left[index] * right[index];
        return value;
    }

    /// <summary>Evaluates the Numerics-kappa GEV quantile independently in physical parameter space.</summary>
    /// <param name="nonexceedanceProbability">The nonexceedance probability.</param>
    /// <param name="location">The physical location.</param>
    /// <param name="scale">The positive physical scale.</param>
    /// <param name="shape">The Numerics kappa shape.</param>
    /// <returns>The independently evaluated quantile.</returns>
    private static double IndependentGevQuantile(double nonexceedanceProbability, double location, double scale, double shape)
    {
        double reduced = -Math.Log(nonexceedanceProbability);
        return Math.Abs(shape) <= 1e-12
            ? location - scale * Math.Log(reduced)
            : location + scale / shape * (1.0 - Math.Pow(reduced, shape));
    }

    /// <summary>Asserts a scalar agrees within a declared relative tolerance.</summary>
    /// <param name="expected">The independent expected value.</param>
    /// <param name="actual">The production value.</param>
    /// <param name="relativeTolerance">The declared relative tolerance.</param>
    /// <param name="message">The assertion context.</param>
    private static void AssertRelative(double expected, double actual, double relativeTolerance, string message)
    {
        Assert.AreEqual(expected, actual, relativeTolerance * Math.Max(1.0, Math.Abs(expected)), message);
    }
}
