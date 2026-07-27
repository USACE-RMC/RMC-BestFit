using System.Reflection;
using System.Text.Json;
using System.Xml.Linq;
using Numerics.Distributions;
using Numerics.Mathematics.Optimization;
using Numerics.Sampling.MCMC;
using RMC.BestFit.Diagnostics;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;

namespace RMC.BestFit.Verification.ModelEstimation;

/// <summary>
/// Verifies corrected PSIS-LOO numerical, diagnostic-threshold, and single-pass
/// performance parity against R <c>loo</c> 2.10.0.
/// </summary>
/// <remarks>
/// The R package is used only to generate committed JSON artifacts. These C#
/// methods require no R or Python runtime.
/// </remarks>
[TestClass]
public class PsisLooOracleTests
{
    /// <summary>
    /// Gets or sets the MSTest context used to record external-parity finding values.
    /// </summary>
    public TestContext TestContext { get; set; } = null!;

    /// <summary>
    /// Verifies all aggregate, pointwise, weight, effective-sample-size, tail-length,
    /// and diagnostic-table identities recorded by the R <c>loo</c> oracle.
    /// </summary>
    [TestMethod]
    public void RlooOracle_InternalIdentitiesAreConsistent()
    {
        JsonElement modelOracle = LoadOracle("model-comparison-oracle.json");
        JsonElement psisOracle = LoadOracle("psis-loo-oracle.json");
        double[][] logLikelihood = ReadMatrix(
            modelOracle.GetProperty("pointwise_log_likelihood"));
        double[][] normalizedLogWeights = ReadMatrix(
            psisOracle.GetProperty("psis").GetProperty("normalized_log_weights"));
        double[] expectedParetoK = ReadArray(
            psisOracle.GetProperty("loo").GetProperty("pareto_k"));
        double[] expectedInfluenceParetoK = ReadArray(
            psisOracle.GetProperty("loo").GetProperty("influence_pareto_k"));
        double[] expectedNEff = ReadArray(
            psisOracle.GetProperty("loo").GetProperty("n_eff"));
        double[] psisParetoK = ReadArray(
            psisOracle.GetProperty("psis").GetProperty("pareto_k"));
        double[] psisNEff = ReadArray(
            psisOracle.GetProperty("psis").GetProperty("n_eff"));
        double[][] unnormalizedLogWeights = ReadMatrix(
            psisOracle.GetProperty("psis").GetProperty("unnormalized_log_weights"));
        double[][] expectedPointwise = ReadMatrix(
            psisOracle.GetProperty("loo").GetProperty("pointwise"));
        JsonElement tolerances = psisOracle.GetProperty("metadata").GetProperty("tolerances");
        double aggregateTolerance = tolerances.GetProperty("aggregate").GetDouble();
        double pointwiseTolerance = tolerances.GetProperty("pointwise").GetDouble();
        double paretoTolerance = tolerances.GetProperty("pareto_k").GetDouble();
        double logWeightTolerance = tolerances.GetProperty("log_weights").GetDouble();
        double nEffTolerance = tolerances.GetProperty("n_eff").GetDouble();
        int drawCount = logLikelihood.Length;
        int observationCount = logLikelihood[0].Length;

        Assert.AreEqual(drawCount, normalizedLogWeights.Length);
        Assert.AreEqual(observationCount, expectedPointwise.Length);

        var actualElpd = new double[observationCount];
        var actualPLoo = new double[observationCount];
        for (int observationIndex = 0; observationIndex < observationCount; observationIndex++)
        {
            double[] logWeights = normalizedLogWeights
                .Select(row => row[observationIndex])
                .ToArray();
            double[] likelihoodColumn = logLikelihood
                .Select(row => row[observationIndex])
                .ToArray();
            double[] weights = logWeights.Select(Math.Exp).ToArray();
            double weightSum = weights.Sum();
            double nEff = 1d / weights.Sum(weight => weight * weight);
            double elpd = LogSumExp(likelihoodColumn
                .Select((value, drawIndex) => value + logWeights[drawIndex]));
            double lppd = LogMeanExp(likelihoodColumn);
            double pLoo = lppd - elpd;

            Assert.AreEqual(1d, weightSum, aggregateTolerance,
                $"Normalized PSIS weights do not sum to one for observation {observationIndex}.");
            Assert.AreEqual(expectedNEff[observationIndex], nEff, nEffTolerance,
                $"PSIS effective sample size differs for observation {observationIndex}.");
            Assert.AreEqual(expectedParetoK[observationIndex],
                psisParetoK[observationIndex], paretoTolerance);
            Assert.AreEqual(expectedNEff[observationIndex],
                psisNEff[observationIndex], nEffTolerance);
            Assert.AreEqual(expectedPointwise[observationIndex][0], elpd, pointwiseTolerance,
                $"Pointwise ELPD-LOO identity differs for observation {observationIndex}.");
            Assert.AreEqual(expectedPointwise[observationIndex][2], pLoo, pointwiseTolerance,
                $"Pointwise p_LOO identity differs for observation {observationIndex}.");
            Assert.AreEqual(expectedPointwise[observationIndex][3], -2d * elpd, pointwiseTolerance,
                $"Pointwise LOOIC identity differs for observation {observationIndex}.");
            Assert.AreEqual(expectedParetoK[observationIndex],
                expectedInfluenceParetoK[observationIndex], paretoTolerance,
                $"Sampling and influence Pareto-k values differ unexpectedly at {observationIndex}.");

            double normalizationShift = unnormalizedLogWeights[0][observationIndex]
                - normalizedLogWeights[0][observationIndex];
            for (int drawIndex = 1; drawIndex < drawCount; drawIndex++)
            {
                Assert.AreEqual(
                    normalizationShift,
                    unnormalizedLogWeights[drawIndex][observationIndex]
                        - normalizedLogWeights[drawIndex][observationIndex],
                    logWeightTolerance,
                    $"Normalized and unnormalized PSIS weights differ by a nonconstant shift " +
                    $"for observation {observationIndex}.");
            }

            actualElpd[observationIndex] = elpd;
            actualPLoo[observationIndex] = pLoo;
        }

        double expectedElpd = ReadEstimate(psisOracle, "elpd_loo", "estimate");
        double expectedPLoo = ReadEstimate(psisOracle, "p_loo", "estimate");
        double expectedLooic = ReadEstimate(psisOracle, "looic", "estimate");
        Assert.AreEqual(expectedElpd, actualElpd.Sum(), aggregateTolerance);
        Assert.AreEqual(expectedPLoo, actualPLoo.Sum(), aggregateTolerance);
        Assert.AreEqual(expectedLooic, -2d * actualElpd.Sum(), aggregateTolerance);

        double elpdStandardError = Math.Sqrt(
            observationCount * SampleVariance(actualElpd));
        double pLooStandardError = Math.Sqrt(
            observationCount * SampleVariance(actualPLoo));
        Assert.AreEqual(
            ReadEstimate(psisOracle, "elpd_loo", "standard_error"),
            elpdStandardError,
            aggregateTolerance);
        Assert.AreEqual(
            ReadEstimate(psisOracle, "p_loo", "standard_error"),
            pLooStandardError,
            aggregateTolerance);
        Assert.AreEqual(
            ReadEstimate(psisOracle, "looic", "standard_error"),
            2d * elpdStandardError,
            aggregateTolerance);

        int expectedTailLength = (int)Math.Ceiling(
            Math.Min(0.2d * drawCount, 3d * Math.Sqrt(drawCount)));
        int[] tailLengths = ReadIntArray(
            psisOracle.GetProperty("psis").GetProperty("tail_length"));
        Assert.IsTrue(tailLengths.All(length => length == expectedTailLength));

        double expectedThreshold = Math.Min(1d - 1d / Math.Log10(drawCount), 0.7d);
        Assert.AreEqual(
            expectedThreshold,
            psisOracle.GetProperty("loo").GetProperty("diagnostic_threshold").GetDouble(),
            aggregateTolerance);
        CollectionAssert.AreEqual(
            new[] { 2 },
            ReadIntArray(psisOracle.GetProperty("loo")
                .GetProperty("problematic_zero_based_ids")));
        JsonElement paretoTable = psisOracle.GetProperty("loo").GetProperty("pareto_k_table");
        Assert.AreEqual(4d, paretoTable[0][0].GetDouble(), aggregateTolerance);
        Assert.AreEqual(0.8d, paretoTable[0][1].GetDouble(), aggregateTolerance);
        Assert.AreEqual(expectedNEff.Min(), paretoTable[0][2].GetDouble(), nEffTolerance);
        Assert.AreEqual(1d, paretoTable[1][0].GetDouble(), aggregateTolerance);
        Assert.AreEqual(0.2d, paretoTable[1][1].GetDouble(), aggregateTolerance);
        Assert.AreEqual(JsonValueKind.Null, paretoTable[1][2].ValueKind);
        Assert.AreEqual(0d, paretoTable[2][0].GetDouble(), aggregateTolerance);
        Assert.AreEqual(JsonValueKind.Null, paretoTable[2][2].ValueKind);
        Assert.AreEqual(
            JsonValueKind.Null,
            psisOracle.GetProperty("loo").GetProperty("mcse_elpd_loo").ValueKind,
            "R loo suppresses overall MCSE when a Pareto-k exceeds its sample-size threshold.");
    }

    /// <summary>
    /// Verifies BestFit aggregate, pointwise, Pareto-k, and smoothed-weight parity
    /// with R <c>loo</c> 2.10.0.
    /// </summary>
    [TestMethod]
    public void PSISLOO_MatchesRLooOracle()
    {
        var fixture = CreateNormalFixture();
        JsonElement psisOracle = LoadOracle("psis-loo-oracle.json");
        JsonElement tolerances = psisOracle.GetProperty("metadata").GetProperty("tolerances");
        double aggregateTolerance = tolerances.GetProperty("aggregate").GetDouble();
        double pointwiseTolerance = tolerances.GetProperty("pointwise").GetDouble();
        double paretoTolerance = tolerances.GetProperty("pareto_k").GetDouble();
        double logWeightTolerance = tolerances.GetProperty("log_weights").GetDouble();
        double expectedLooic = ReadEstimate(psisOracle, "looic", "estimate");
        double expectedPLoo = ReadEstimate(psisOracle, "p_loo", "estimate");
        double expectedLooicStandardError = ReadEstimate(
            psisOracle, "looic", "standard_error");
        double[] expectedParetoK = ReadArray(
            psisOracle.GetProperty("loo").GetProperty("pareto_k"));
        double[][] expectedPointwise = ReadMatrix(
            psisOracle.GetProperty("loo").GetProperty("pointwise"));

        Assert.AreEqual(expectedLooic, fixture.Analysis.LOOIC, aggregateTolerance);
        Assert.AreEqual(expectedPLoo, fixture.Analysis.LOO_pD, aggregateTolerance);
        Assert.AreEqual(
            expectedLooicStandardError,
            fixture.Analysis.LOOIC_SE,
            aggregateTolerance);
        CollectionAssert.AreEqual(
            expectedParetoK,
            fixture.Analysis.ParetoK!,
            Comparer<double>.Create((expected, actual) =>
                Math.Abs(expected - actual) <= paretoTolerance ? 0 : expected.CompareTo(actual)));

        InfluenceDiagnostics influence = fixture.Analysis.ComputeInfluenceDiagnostics();
        for (int observationIndex = 0; observationIndex < influence.Count; observationIndex++)
        {
            Assert.AreEqual(
                expectedPointwise[observationIndex][0],
                influence[observationIndex].ElpdLoo,
                pointwiseTolerance,
                $"Pointwise ELPD-LOO differs at observation {observationIndex}.");
            Assert.AreEqual(
                expectedParetoK[observationIndex],
                influence[observationIndex].ParetoK,
                paretoTolerance,
                $"Pareto k differs at observation {observationIndex}.");
        }

        MethodInfo smoother = typeof(BayesianAnalysis).GetMethod(
            "ParetoSmoothWeights",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new AssertFailedException("ParetoSmoothWeights was not found.");
        double[] firstLogLikelihood = fixture.PointwiseLogLikelihood
            .Select(row => row[0])
            .ToArray();
        double[] actualLogWeights = firstLogLikelihood.Select(value => -value).ToArray();
        int tailLength = ReadIntArray(
            psisOracle.GetProperty("psis").GetProperty("tail_length"))[0];
        double actualK = (double)(smoother.Invoke(
            null,
            new object[] { actualLogWeights, tailLength }) ?? double.NaN);
        double[][] expectedUnnormalized = ReadMatrix(
            psisOracle.GetProperty("psis").GetProperty("unnormalized_log_weights"));

        Assert.AreEqual(expectedParetoK[0], actualK, paretoTolerance);
        for (int drawIndex = 0; drawIndex < actualLogWeights.Length; drawIndex++)
        {
            Assert.AreEqual(
                expectedUnnormalized[drawIndex][0],
                actualLogWeights[drawIndex],
                logWeightTolerance,
                $"Smoothed log weight differs at draw {drawIndex}.");
        }
    }
    /// <summary>
    /// Verifies BestFit smoothed weights and Pareto-k estimates against R across
    /// bounded, light, moderate, high, nonfinite-mean, and degenerate tails.
    /// </summary>
    [TestMethod]
    public void PsisTailRegimes_MatchRLooOracle()
    {
        JsonElement oracle = LoadOracle("psis-loo-oracle.json");
        JsonElement cases = oracle.GetProperty("psis_tail_cases").GetProperty("cases");
        JsonElement tolerances = oracle.GetProperty("metadata").GetProperty("tolerances");
        double paretoTolerance = tolerances.GetProperty("pareto_k").GetDouble();
        double weightTolerance = tolerances.GetProperty("log_weights").GetDouble();
        double nEffTolerance = tolerances.GetProperty("n_eff").GetDouble();
        MethodInfo smoother = typeof(BayesianAnalysis).GetMethod(
            "ParetoSmoothWeights",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new AssertFailedException("ParetoSmoothWeights was not found.");

        foreach (JsonElement tailCase in cases.EnumerateArray())
        {
            string name = tailCase.GetProperty("name").GetString() ?? string.Empty;
            double[] actualLogWeights = ReadArray(tailCase.GetProperty("log_ratios"));
            double[] expectedLogWeights = ReadArray(
                tailCase.GetProperty("unnormalized_log_weights"));
            double expectedNEff = tailCase.GetProperty("n_eff").GetDouble();
            int tailLength = tailCase.GetProperty("tail_length").GetInt32();
            double actualK = (double)(smoother.Invoke(
                null,
                new object[] { actualLogWeights, tailLength }) ?? double.NaN);

            JsonElement expectedKElement = tailCase.GetProperty("pareto_k");
            if (expectedKElement.ValueKind == JsonValueKind.Null)
            {
                Assert.AreEqual("Inf", tailCase.GetProperty("pareto_k_label").GetString());
                Assert.IsTrue(double.IsPositiveInfinity(actualK),
                    $"BestFit should report an infinite Pareto k for {name}.");
            }
            else
            {
                Assert.AreEqual(
                    expectedKElement.GetDouble(),
                    actualK,
                    paretoTolerance,
                    $"Pareto k differs for {name}.");
            }

            for (int drawIndex = 0; drawIndex < actualLogWeights.Length; drawIndex++)
            {
                Assert.AreEqual(
                    expectedLogWeights[drawIndex],
                    actualLogWeights[drawIndex],
                    weightTolerance,
                    $"Smoothed log weight differs for {name} at draw {drawIndex}.");
            }

            double logNormalizer = LogSumExp(actualLogWeights);
            double[] normalizedWeights = actualLogWeights
                .Select(value => Math.Exp(value - logNormalizer))
                .ToArray();
            double actualNEff = 1d / normalizedWeights.Sum(weight => weight * weight);
            Assert.AreEqual(expectedNEff, actualNEff, nEffTolerance,
                $"PSIS effective sample size differs for {name}.");
        }
    }
    /// <summary>
    /// Verifies Bayesian influence categories and reliability use the R <c>loo</c>
    /// sample-size-dependent Pareto-k threshold and preserve it through XML.
    /// </summary>
    [TestMethod]
    public void ParetoInfluence_UsesRloo210DiagnosticThreshold()
    {
        var fixture = CreateNormalFixture();
        JsonElement psisOracle = LoadOracle("psis-loo-oracle.json");
        double[] paretoK = ReadArray(psisOracle.GetProperty("loo").GetProperty("pareto_k"));
        double threshold = psisOracle.GetProperty("loo")
            .GetProperty("diagnostic_threshold").GetDouble();
        InfluenceDiagnostics diagnostics = fixture.Analysis.ComputeInfluenceDiagnostics();

        Assert.AreEqual(paretoK.Average(), diagnostics.MeanParetoK, 1e-12);
        Assert.AreEqual(paretoK.Max(), diagnostics.MaxParetoK, 1e-12);
        Assert.AreEqual(0, diagnostics.CountParetoKAbove05);
        Assert.AreEqual(0, diagnostics.CountParetoKAbove07);
        Assert.AreEqual(0, diagnostics.CountParetoKAbove10);
        Assert.AreEqual(ParetoKCategory.OK, diagnostics[2].Category);
        Assert.IsFalse(diagnostics.IsReliable);
        CollectionAssert.AreEqual(
            new[] { 2 },
            diagnostics.GetProblematicObservations(threshold)
                .Select(observation => observation.Index)
                .ToArray());
        StringAssert.StartsWith(diagnostics.GetReliabilitySummary(), "CAUTION:");

        XElement xml = diagnostics.ToXElement();
        Assert.AreEqual(
            threshold,
            double.Parse(
                xml.Attribute("ParetoKDiagnosticThreshold")?.Value ?? string.Empty,
                System.Globalization.CultureInfo.InvariantCulture),
            1e-12);
        var restored = new InfluenceDiagnostics(xml);
        Assert.AreEqual(ParetoKCategory.OK, restored[2].Category);
        Assert.IsFalse(restored.IsReliable);
        StringAssert.StartsWith(restored.GetReliabilitySummary(), "CAUTION:");
    }

    /// <summary>
    /// Verifies default WAIC and PSIS-LOO completion evaluates the pointwise model once per draw.
    /// </summary>
    [TestMethod]
    public void DefaultInformationCriteria_EvaluatePointwiseLikelihoodOnce()
    {
        var fixture = CreateCountingFixture();

        Assert.AreEqual(fixture.DrawCount, fixture.Model.PointwiseCallCount);
        Assert.AreEqual(fixture.DrawCount + 1, fixture.Model.DataCallCount);
        Assert.AreEqual(0, fixture.Model.ComponentCallCount);
    }

    /// <summary>
    /// Verifies influence diagnostics reuse cached ELPD and Pareto-k summaries.
    /// </summary>
    [TestMethod]
    public void InfluenceDiagnostics_ReuseCachedPointwiseLikelihood()
    {
        var fixture = CreateCountingFixture();

        InfluenceDiagnostics diagnostics = fixture.Analysis.ComputeInfluenceDiagnostics();

        Assert.AreEqual(fixture.ObservationCount, diagnostics.Count);
        Assert.AreEqual(fixture.DrawCount, fixture.Model.PointwiseCallCount);
        Assert.AreEqual(1, fixture.Model.ComponentCallCount);
    }
    /// <summary>
    /// Creates the deterministic Normal model and fixed posterior results shared with R.
    /// </summary>
    /// <returns>The analysis, pointwise matrix, and posterior draws.</returns>
    private static (
        BayesianAnalysis Analysis,
        double[][] PointwiseLogLikelihood,
        double[][] Draws) CreateNormalFixture()
    {
        JsonElement oracle = LoadOracle("model-comparison-oracle.json");
        double[] observations = ReadArray(oracle.GetProperty("observations"));
        double[][] draws = ReadMatrix(oracle.GetProperty("posterior_draws"));
        var dataFrame = new DataFrame
        {
            ExactSeries = new ExactSeries(observations)
        };
        var model = new UnivariateDistribution(
            dataFrame,
            UnivariateDistributionType.Normal);
        var output = draws
            .Select(draw => new ParameterSet((double[])draw.Clone(), 0d))
            .ToList();
        var results = new MCMCResults(
            new ParameterSet((double[])draws[0].Clone(), 0d),
            output,
            alpha: 0.10);
        var analysis = new BayesianAnalysis(model);
        analysis.SetCustomMCMCResults(results);

        return (
            analysis,
            ReadMatrix(oracle.GetProperty("pointwise_log_likelihood")),
            draws);
    }

    /// <summary>
    /// Creates a counting model around the committed pointwise matrix.
    /// </summary>
    /// <returns>The analysis, counting model, draw count, and observation count.</returns>
    private static (
        BayesianAnalysis Analysis,
        CountingPointwiseModel Model,
        int DrawCount,
        int ObservationCount) CreateCountingFixture()
    {
        JsonElement oracle = LoadOracle("model-comparison-oracle.json");
        double[][] pointwise = ReadMatrix(oracle.GetProperty("pointwise_log_likelihood"));
        var model = new CountingPointwiseModel(pointwise);
        var output = Enumerable.Range(0, pointwise.Length)
            .Select(index => new ParameterSet(new[] { (double)index }, 0d))
            .ToList();
        var results = new MCMCResults(
            new ParameterSet(new[] { 0d }, 0d),
            output,
            alpha: 0.10);
        var analysis = new BayesianAnalysis(model);
        analysis.SetCustomMCMCResults(results);

        return (analysis, model, pointwise.Length, pointwise[0].Length);
    }

    /// <summary>
    /// Loads and clones a committed verification artifact root.
    /// </summary>
    /// <param name="fileName">Artifact file name under <c>VerificationData</c>.</param>
    /// <returns>A detached JSON root element.</returns>
    private static JsonElement LoadOracle(string fileName)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "VerificationData", fileName);
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.Clone();
    }

    /// <summary>
    /// Reads one named aggregate estimate from the R LOO artifact.
    /// </summary>
    /// <param name="oracle">PSIS-LOO artifact root.</param>
    /// <param name="name">Estimate name.</param>
    /// <param name="field">Field name, such as estimate or standard error.</param>
    /// <returns>The requested value.</returns>
    private static double ReadEstimate(JsonElement oracle, string name, string field)
    {
        JsonElement estimate = oracle.GetProperty("loo").GetProperty("estimates")
            .EnumerateArray()
            .Single(item => item.GetProperty("name").GetString() == name);
        return estimate.GetProperty(field).GetDouble();
    }

    /// <summary>
    /// Reads a JSON numeric array.
    /// </summary>
    /// <param name="element">JSON array.</param>
    /// <returns>Managed double values.</returns>
    private static double[] ReadArray(JsonElement element)
    {
        return element.EnumerateArray().Select(value => value.GetDouble()).ToArray();
    }

    /// <summary>
    /// Reads a JSON integer array.
    /// </summary>
    /// <param name="element">JSON array.</param>
    /// <returns>Managed integer values.</returns>
    private static int[] ReadIntArray(JsonElement element)
    {
        return element.EnumerateArray().Select(value => value.GetInt32()).ToArray();
    }

    /// <summary>
    /// Reads a JSON numeric matrix.
    /// </summary>
    /// <param name="element">JSON row array.</param>
    /// <returns>Managed matrix rows.</returns>
    private static double[][] ReadMatrix(JsonElement element)
    {
        return element.EnumerateArray().Select(ReadArray).ToArray();
    }

    /// <summary>
    /// Computes a stable log-sum-exp value.
    /// </summary>
    /// <param name="values">Log-scale values.</param>
    /// <returns>The logarithm of the sum of exponentials.</returns>
    private static double LogSumExp(IEnumerable<double> values)
    {
        double[] materialized = values.ToArray();
        double maximum = materialized.Max();
        return maximum + Math.Log(materialized.Sum(value => Math.Exp(value - maximum)));
    }

    /// <summary>
    /// Computes a stable log-mean-exp value.
    /// </summary>
    /// <param name="values">Log-scale values.</param>
    /// <returns>The logarithm of the mean exponential.</returns>
    private static double LogMeanExp(IEnumerable<double> values)
    {
        double[] materialized = values.ToArray();
        return LogSumExp(materialized) - Math.Log(materialized.Length);
    }

    /// <summary>
    /// Computes unbiased sample variance.
    /// </summary>
    /// <param name="values">Finite values.</param>
    /// <returns>Sample variance using an <c>n-1</c> divisor.</returns>
    private static double SampleVariance(double[] values)
    {
        double mean = values.Average();
        return values.Sum(value => Math.Pow(value - mean, 2d)) / (values.Length - 1d);
    }

    /// <summary>
    /// Minimal pointwise model that counts likelihood evaluations independently of wall-clock speed.
    /// </summary>
    private sealed class CountingPointwiseModel : IModel
    {
        private readonly double[][] _pointwise;
        private int _dataCallCount;
        private int _pointwiseCallCount;
        private int _componentCallCount;

        /// <summary>
        /// Creates a counting model for the supplied draw-by-observation matrix.
        /// </summary>
        /// <param name="pointwise">Draw-by-observation pointwise log likelihood.</param>
        public CountingPointwiseModel(double[][] pointwise)
        {
            _pointwise = pointwise;
        }

        /// <inheritdoc/>
        public List<ModelParameter> Parameters { get; } = new();

        /// <inheritdoc/>
        public int NumberOfParameters => 1;

        /// <inheritdoc/>
        public bool UseDefaultFlatPriors { get; set; }

        /// <summary>
        /// Gets the number of scalar data-likelihood evaluations.
        /// </summary>
        public int DataCallCount => _dataCallCount;

        /// <summary>
        /// Gets the number of pointwise likelihood evaluations.
        /// </summary>
        public int PointwiseCallCount => _pointwiseCallCount;

        /// <summary>
        /// Gets the number of metadata component evaluations.
        /// </summary>
        public int ComponentCallCount => _componentCallCount;

        /// <inheritdoc/>
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        /// <inheritdoc/>
        public double LogLikelihood(double[] parameters) => DataLogLikelihood(parameters);

        /// <inheritdoc/>
        public double DataLogLikelihood(double[] parameters)
        {
            Interlocked.Increment(ref _dataCallCount);
            return Row(parameters).Sum();
        }

        /// <inheritdoc/>
        public double[] PointwiseDataLogLikelihood(double[] parameters)
        {
            Interlocked.Increment(ref _pointwiseCallCount);
            return (double[])Row(parameters).Clone();
        }

        /// <inheritdoc/>
        public List<DataComponent> PointwiseDataLogLikelihoodComponents(double[] parameters)
        {
            Interlocked.Increment(ref _componentCallCount);
            return Row(parameters)
                .Select((value, index) => new DataComponent(index, value, index))
                .ToList();
        }

        /// <inheritdoc/>
        public double PriorLogLikelihood(double[] parameters) => 0d;

        /// <inheritdoc/>
        public List<PriorComponent> PointwisePriorLogLikelihood(double[] parameters) => new();

        /// <inheritdoc/>
        public void SetParameterValues(IList<double> parameters) { }

        /// <inheritdoc/>
        public void SetDefaultParameters() { }

        /// <inheritdoc/>
        public IModel Clone() => new CountingPointwiseModel(_pointwise);

        /// <inheritdoc/>
        public XElement ToXElement() => new("CountingPointwiseModel");

        /// <inheritdoc/>
        public (bool IsValid, List<string> ValidationMessages) Validate()
            => (true, new List<string>());

        /// <summary>
        /// Maps the synthetic draw-index parameter to one matrix row.
        /// </summary>
        /// <param name="parameters">Synthetic draw-index parameter.</param>
        /// <returns>The selected pointwise row.</returns>
        private double[] Row(double[] parameters)
        {
            int index = (int)Math.Round(parameters[0]);
            index = Math.Clamp(index, 0, _pointwise.Length - 1);
            return _pointwise[index];
        }

        /// <summary>
        /// Retains an event invocation so the interface event is not reported as unused.
        /// </summary>
        private void RaisePropertyChanged()
            => PropertyChanged?.Invoke(
                this,
                new System.ComponentModel.PropertyChangedEventArgs(string.Empty));
    }
}
