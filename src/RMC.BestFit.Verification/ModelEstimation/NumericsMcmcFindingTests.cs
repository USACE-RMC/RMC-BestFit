using System.Reflection;
using System.Text.Json;
using System.Xml.Linq;
using Numerics.Data.Statistics;
using Numerics.Distributions;
using Numerics.Mathematics.Optimization;
using Numerics.Sampling.MCMC;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;

namespace RMC.BestFit.Verification.ModelEstimation;

/// <summary>
/// Verifies the BestFit integration of the corrected Numerics ARWMH state history,
/// NUTS gradient route, and acceptance-rate contracts.
/// </summary>
/// <remarks>
/// The fixtures use deterministic inline targets and require no R or Python runtime.
/// MCMC diagnostic parity consumes a committed R <c>posterior</c> 1.7.0 oracle;
/// all focused C# methods run without an R or Python runtime.
/// </remarks>
[TestClass]
public class NumericsMcmcFindingTests
{
    /// <summary>
    /// Confirms that BestFit's ARWMH configuration records every retained state in
    /// the continually adapted covariance history.
    /// </summary>
    /// <remarks>
    /// A rejected Adaptive Metropolis transition repeats the retained state and is
    /// therefore part of the realized Markov-chain history used by the empirical
    /// covariance in the Haario-Saksman-Tamminen construction.
    /// </remarks>
    [TestMethod]
    public void Arwmh_BestFitWiringRecordsEveryRealizedStateInAdaptiveCovariance()
    {
        BayesianAnalysis analysis = CreateAnalysis(
            new SyntheticMcmcModel(useSpikeTarget: true),
            BayesianAnalysis.SamplerType.ARWMH);
        analysis.Beta = 1d;
        analysis.SetUpSampler();
        MCMCSampler sampler = SeedSampler(analysis);

        sampler.Sample();

        int expectedRawTransitions = analysis.Iterations
            + (int)Math.Ceiling(analysis.OutputLength / (double)analysis.NumberOfChains);
        Assert.IsTrue(sampler.AcceptCount.All(count => count == 0));
        Assert.IsTrue(sampler.SampleCount.All(count => count == expectedRawTransitions));
        foreach (RunningCovarianceMatrix covariance in GetArwmhCovariances((ARWMH)sampler))
        {
            Assert.AreEqual(
                expectedRawTransitions,
                covariance.N,
                "Every accepted, rejected, or infeasible transition must contribute its retained state.");
        }
    }

    /// <summary>
    /// Confirms that BestFit persists and reports NUTS Hamiltonian acceptance while the
    /// generic sampler property retains accepted-transition semantics.
    /// </summary>
    [TestMethod]
    public void Nuts_BestFitResultsUseHamiltonianAcceptanceWithoutDetailedDiagnostics()
    {
        BayesianAnalysis analysis = CreateAnalysis(
            new SyntheticMcmcModel(useSpikeTarget: false),
            BayesianAnalysis.SamplerType.NUTS);
        MCMCSampler sampler = SeedSampler(analysis);

        sampler.Sample();
        var nuts = (NUTS)sampler;
        var results = new MCMCResults(sampler, alpha: 0.10);
        analysis.SetCustomMCMCResults(results, skipInformationCriteria: true);
        string report = analysis.GenerateReport();

        for (int chainIndex = 0; chainIndex < sampler.NumberOfChains; chainIndex++)
        {
            double transitionRate = (double)sampler.AcceptCount[chainIndex] /
                sampler.SampleCount[chainIndex];
            Assert.AreEqual(transitionRate, sampler.AcceptanceRates[chainIndex], 0d);
            Assert.AreEqual(1d, sampler.AcceptanceRates[chainIndex], 0d);
            Assert.IsTrue(nuts.HamiltonianAcceptanceRates[chainIndex] > 0d);
            Assert.IsTrue(nuts.HamiltonianAcceptanceRates[chainIndex] < 1d);
        }

        CollectionAssert.AreEqual(nuts.HamiltonianAcceptanceRates, results.AcceptanceRates);
        StringAssert.Contains(report, "NUTS SAMPLER DIAGNOSTICS");
        StringAssert.Contains(report, "Overall Hamiltonian Acceptance:");
        Assert.IsFalse(report.Contains("Overall:   100.0%"));
        Assert.IsFalse(report.Contains("Divergences:"));
        Assert.IsFalse(report.Contains("Maximum Tree Depth Hits:"));
        Assert.IsFalse(report.Contains("E-BFMI"));
        Assert.IsFalse(report.Contains("legacy result"));
        Assert.IsFalse(report.Contains("Proposals are too timid"));
    }
    /// <summary>
    /// Confirms that BestFit's default NUTS gradient differentiates the complete model
    /// log posterior supplied through the production setup path.
    /// </summary>
    [TestMethod]
    public void Nuts_BestFitNumericalGradientMatchesPosteriorGradient()
    {
        BayesianAnalysis analysis = CreateAnalysis(
            new SyntheticMcmcModel(useSpikeTarget: false),
            BayesianAnalysis.SamplerType.NUTS);
        var sampler = (NUTS)analysis.Sampler!;
        double[] point = { 1.25d, -0.75d };

        var gradient = sampler.GradientFunction(point);

        Assert.AreEqual(-1.50d, gradient[0], 1e-8);
        Assert.AreEqual(0.50d, gradient[1], 1e-8);
    }

    /// <summary>
    /// Verifies rank-normalized split/folded R-hat against R <c>posterior</c> 1.7.0.
    /// </summary>
    [TestMethod]
    public void RankNormalizedRhat_MatchesRPosteriorOracle()
    {
        JsonElement oracle = LoadMcmcDiagnosticsOracle();
        double tolerance = oracle.GetProperty("metadata")
            .GetProperty("rhat_absolute_tolerance")
            .GetDouble();

        foreach (JsonElement fixture in oracle.GetProperty("fixtures").EnumerateArray())
        {
            string name = fixture.GetProperty("name").GetString()!;
            List<List<ParameterSet>> chains = ReadDiagnosticChains(fixture, trimWarmup: false);
            double actual = MCMCDiagnostics.GelmanRubin(
                chains,
                fixture.GetProperty("warmup").GetInt32())[0];
            JsonElement expected = fixture.GetProperty("expected").GetProperty("rhat");
            if (expected.ValueKind == JsonValueKind.Null)
            {
                Assert.IsTrue(double.IsNaN(actual), $"{name} R-hat should be unavailable.");
            }
            else
            {
                Assert.AreEqual(expected.GetDouble(), actual, tolerance, $"{name} R-hat mismatch.");
            }
        }
    }

    /// <summary>
    /// Verifies the existing scalar ESS equals the conservative R bulk/tail minimum.
    /// </summary>
    [TestMethod]
    public void ConservativeEss_MatchesRPosteriorBulkAndTailOracle()
    {
        JsonElement oracle = LoadMcmcDiagnosticsOracle();
        double tolerance = oracle.GetProperty("metadata")
            .GetProperty("ess_absolute_tolerance")
            .GetDouble();

        foreach (JsonElement fixture in oracle.GetProperty("fixtures").EnumerateArray())
        {
            string name = fixture.GetProperty("name").GetString()!;
            JsonElement expected = fixture.GetProperty("expected");
            double actual = MCMCDiagnostics.EffectiveSampleSize(
                ReadDiagnosticChains(fixture, trimWarmup: true),
                out double[][,] averageAcf)[0];
            JsonElement expectedScalar = expected.GetProperty("scalar_ess");
            if (expectedScalar.ValueKind == JsonValueKind.Null)
            {
                Assert.IsTrue(double.IsNaN(actual), $"{name} ESS should be unavailable.");
                continue;
            }

            double expectedMinimum = Math.Min(
                expected.GetProperty("bulk_ess").GetDouble(),
                Math.Min(
                    expected.GetProperty("lower_tail_ess").GetDouble(),
                    expected.GetProperty("upper_tail_ess").GetDouble()));
            Assert.AreEqual(expectedMinimum, expectedScalar.GetDouble(), tolerance,
                $"{name} oracle scalar ESS is not the declared conservative minimum.");
            Assert.AreEqual(expectedScalar.GetDouble(), actual, tolerance, $"{name} ESS mismatch.");
            Assert.AreEqual(51, averageAcf[0].GetLength(0), $"{name} ACF row contract changed.");
            Assert.AreEqual(2, averageAcf[0].GetLength(1), $"{name} ACF column contract changed.");
        }
    }

    /// <summary>
    /// Loads the committed R posterior diagnostic oracle.
    /// </summary>
    /// <returns>A detached JSON root element.</returns>
    private static JsonElement LoadMcmcDiagnosticsOracle()
    {
        string path = Path.Combine(
            AppContext.BaseDirectory,
            "VerificationData",
            "mcmc-diagnostics-oracle.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.Clone();
    }

    /// <summary>
    /// Reconstructs Numerics parameter-set chains from one oracle fixture.
    /// </summary>
    /// <param name="fixture">Oracle fixture containing chain-major draws.</param>
    /// <param name="trimWarmup">Whether to remove the fixture's declared warmup.</param>
    /// <returns>Chains containing one diagnostic parameter.</returns>
    private static List<List<ParameterSet>> ReadDiagnosticChains(
        JsonElement fixture,
        bool trimWarmup)
    {
        int warmup = trimWarmup ? fixture.GetProperty("warmup").GetInt32() : 0;
        var chains = new List<List<ParameterSet>>();
        foreach (JsonElement chainElement in fixture.GetProperty("chains").EnumerateArray())
        {
            var chain = new List<ParameterSet>();
            int iteration = 0;
            foreach (JsonElement value in chainElement.EnumerateArray())
            {
                if (iteration++ >= warmup)
                    chain.Add(new ParameterSet(new[] { value.GetDouble() }, 0d));
            }
            chains.Add(chain);
        }
        return chains;
    }

    /// <summary>
    /// Creates a small deterministic BestFit Bayesian-analysis fixture.
    /// </summary>
    /// <param name="model">Synthetic target model.</param>
    /// <param name="type">Sampler type under verification.</param>
    /// <returns>A configured analysis whose sampler has not yet run.</returns>
    /// <remarks>
    /// The minimum valid iteration and output sizes keep the focused verification run
    /// fast while exercising the same setup path used by production analyses.
    /// </remarks>
    private static BayesianAnalysis CreateAnalysis(IModel model, BayesianAnalysis.SamplerType type)
    {
        var analysis = new BayesianAnalysis(model, type)
        {
            UseSimulationDefaults = false,
            UseAdvancedSimulationDefaults = false,
            NumberOfChains = 4,
            ThinningInterval = 1,
            WarmupIterations = 50,
            Iterations = 100,
            OutputLength = 100,
            InitialIterations = 4,
            PRNGSeed = 8675309,
            MaxTreeDepth = 4
        };
        analysis.SetUpSampler();
        return analysis;
    }

    /// <summary>
    /// Resets and seeds every sampler chain at the origin through the public
    /// user-defined initialization path.
    /// </summary>
    /// <param name="analysis">Analysis containing the sampler to seed.</param>
    /// <returns>The seeded sampler.</returns>
    /// <remarks>
    /// Serial execution makes the fixture deterministic without changing production
    /// pseudo-random-number generation.
    /// </remarks>
    private static MCMCSampler SeedSampler(BayesianAnalysis analysis)
    {
        MCMCSampler sampler = analysis.Sampler!;
        sampler.Reset();
        for (int i = 0; i < sampler.NumberOfChains; i++)
            sampler.MarkovChains[i].Add(new ParameterSet(new[] { 0d, 0d }, 0d));
        sampler.Initialize = MCMCSampler.InitializationType.UserDefined;
        sampler.ParallelizeChains = false;
        return sampler;
    }

    /// <summary>
    /// Reads the per-chain running covariance objects used by ARWMH.
    /// </summary>
    /// <param name="sampler">Initialized ARWMH sampler.</param>
    /// <returns>The per-chain running covariance objects.</returns>
    /// <remarks>
    /// Reflection is confined to this characterization because the covariance history
    /// count is not part of the current public Numerics API.
    /// </remarks>
    private static RunningCovarianceMatrix[] GetArwmhCovariances(ARWMH sampler)
    {
        FieldInfo field = typeof(ARWMH).GetField("sigma", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.IsNotNull(field);
        var covariance = field.GetValue(sampler) as RunningCovarianceMatrix[];
        Assert.IsNotNull(covariance);
        return covariance;
    }

    /// <summary>
    /// Minimal two-parameter model used to exercise BestFit sampler setup and reporting.
    /// </summary>
    private sealed class SyntheticMcmcModel : IModel
    {
        private readonly bool _useSpikeTarget;

        /// <summary>
        /// Initializes either a point-mass rejection fixture or a smooth normal target.
        /// </summary>
        /// <param name="useSpikeTarget">Whether only the origin has finite density.</param>
        /// <remarks>
        /// The spike fixture guarantees rejected ARWMH proposals; the smooth fixture
        /// supplies a stable finite-difference target for NUTS.
        /// </remarks>
        public SyntheticMcmcModel(bool useSpikeTarget)
        {
            _useSpikeTarget = useSpikeTarget;
            Parameters = new List<ModelParameter>
            {
                new("Synthetic", "theta1", 0d, -10d, 10d, new Uniform(-10d, 10d)),
                new("Synthetic", "theta2", 0d, -10d, 10d, new Uniform(-10d, 10d))
            };
        }

        /// <inheritdoc/>
        public List<ModelParameter> Parameters { get; }

        /// <inheritdoc/>
        public int NumberOfParameters => Parameters.Count;

        /// <inheritdoc/>
        public bool UseDefaultFlatPriors { get; set; }

        /// <inheritdoc/>
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        /// <inheritdoc/>
        public double LogLikelihood(double[] parameters) => DataLogLikelihood(parameters) + PriorLogLikelihood(parameters);

        /// <inheritdoc/>
        public double DataLogLikelihood(double[] parameters)
        {
            if (_useSpikeTarget)
                return parameters.All(value => value == 0d) ? 0d : double.NegativeInfinity;
            return -0.5d * parameters.Sum(value => value * value);
        }

        /// <inheritdoc/>
        public double[] PointwiseDataLogLikelihood(double[] parameters)
            => new[] { DataLogLikelihood(parameters) };

        /// <inheritdoc/>
        public List<DataComponent> PointwiseDataLogLikelihoodComponents(double[] parameters)
            => new() { new DataComponent(0, DataLogLikelihood(parameters), 0) };

        /// <inheritdoc/>
        public double PriorLogLikelihood(double[] parameters)
        {
            double coupledSum = parameters[0] + parameters[1];
            return -0.25d * coupledSum * coupledSum;
        }

        /// <inheritdoc/>
        public List<PriorComponent> PointwisePriorLogLikelihood(double[] parameters) => new();

        /// <inheritdoc/>
        public void SetParameterValues(IList<double> parameters)
        {
            for (int i = 0; i < Parameters.Count; i++)
                Parameters[i].Value = parameters[i];
            RaisePropertyChanged();
        }

        /// <inheritdoc/>
        public void SetDefaultParameters()
        {
            foreach (ModelParameter parameter in Parameters)
                parameter.Value = 0d;
            RaisePropertyChanged();
        }

        /// <inheritdoc/>
        public IModel Clone() => new SyntheticMcmcModel(_useSpikeTarget);

        /// <inheritdoc/>
        public XElement ToXElement() => new("SyntheticMcmcModel");

        /// <inheritdoc/>
        public (bool IsValid, List<string> ValidationMessages) Validate()
            => (true, new List<string>());

        /// <summary>
        /// Notifies interface consumers after the synthetic parameter state changes.
        /// </summary>
        /// <remarks>
        /// The event keeps the fixture behavior aligned with mutable production models.
        /// </remarks>
        private void RaisePropertyChanged()
            => PropertyChanged?.Invoke(
                this,
                new System.ComponentModel.PropertyChangedEventArgs(string.Empty));
    }
}
