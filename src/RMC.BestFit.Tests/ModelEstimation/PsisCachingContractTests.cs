using System.ComponentModel;
using System.Xml.Linq;
using Numerics.Mathematics.Optimization;
using Numerics.Sampling.MCMC;
using RMC.BestFit.Diagnostics;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;

namespace RMC.BestFit.Tests.ModelEstimation;

/// <summary>
/// Verifies deterministic pointwise-likelihood evaluation and caching contracts used
/// by WAIC, PSIS-LOO, and influence diagnostics.
/// </summary>
[TestClass]
public class PsisCachingContractTests
{
    /// <summary>
    /// Default information-criterion completion evaluates pointwise likelihood once per draw.
    /// </summary>
    [TestMethod]
    public void DefaultInformationCriteria_EvaluatePointwiseLikelihoodOnce()
    {
        var fixture = CreateFixture();

        Assert.AreEqual(fixture.DrawCount, fixture.Model.PointwiseCallCount);
        Assert.AreEqual(fixture.DrawCount + 1, fixture.Model.DataCallCount);
        Assert.AreEqual(0, fixture.Model.ComponentCallCount);
    }

    /// <summary>
    /// Influence diagnostics reuse cached ELPD and Pareto-k summaries.
    /// </summary>
    [TestMethod]
    public void InfluenceDiagnostics_ReuseCachedPointwiseLikelihood()
    {
        var fixture = CreateFixture();

        InfluenceDiagnostics diagnostics = fixture.Analysis.ComputeInfluenceDiagnostics();

        Assert.AreEqual(fixture.ObservationCount, diagnostics.Count);
        Assert.AreEqual(fixture.DrawCount, fixture.Model.PointwiseCallCount);
        Assert.AreEqual(1, fixture.Model.ComponentCallCount);
    }

    /// <summary>
    /// Creates an inline pointwise matrix and fixed posterior results without fitting a model.
    /// </summary>
    /// <returns>The configured analysis and its deterministic evaluation counters.</returns>
    private static (
        BayesianAnalysis Analysis,
        CountingPointwiseModel Model,
        int DrawCount,
        int ObservationCount) CreateFixture()
    {
        double[][] pointwise =
        [
            [-1.0, -1.2, -0.8],
            [-1.1, -1.0, -0.9],
            [-0.9, -1.3, -1.0],
            [-1.2, -0.9, -1.1],
        ];
        var model = new CountingPointwiseModel(pointwise);
        var output = Enumerable.Range(0, pointwise.Length)
            .Select(index => new ParameterSet([(double)index], 0d))
            .ToList();
        var results = new MCMCResults(
            new ParameterSet([0d], 0d),
            output,
            alpha: 0.10);
        var analysis = new BayesianAnalysis(model);
        analysis.SetCustomMCMCResults(results);
        return (analysis, model, pointwise.Length, pointwise[0].Length);
    }

    /// <summary>
    /// Minimal pointwise model that counts likelihood evaluations independently of timing.
    /// </summary>
    private sealed class CountingPointwiseModel : IModel
    {
        private readonly double[][] _pointwise;
        private int _dataCallCount;
        private int _pointwiseCallCount;
        private int _componentCallCount;

        /// <summary>
        /// Initializes the counter around a draw-by-observation matrix.
        /// </summary>
        /// <param name="pointwise">The pointwise likelihood matrix.</param>
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

        /// <summary>Gets scalar data-likelihood evaluation count.</summary>
        public int DataCallCount => _dataCallCount;

        /// <summary>Gets pointwise likelihood evaluation count.</summary>
        public int PointwiseCallCount => _pointwiseCallCount;

        /// <summary>Gets component metadata evaluation count.</summary>
        public int ComponentCallCount => _componentCallCount;

        /// <inheritdoc/>
        public event PropertyChangedEventHandler? PropertyChanged;

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
        /// <param name="parameters">The synthetic draw index.</param>
        /// <returns>The selected pointwise row.</returns>
        private double[] Row(double[] parameters)
        {
            int index = Math.Clamp((int)Math.Round(parameters[0]), 0, _pointwise.Length - 1);
            return _pointwise[index];
        }

        /// <summary>
        /// Retains a local event invocation so the interface event is not treated as unused.
        /// </summary>
        private void RaisePropertyChanged()
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }
}
