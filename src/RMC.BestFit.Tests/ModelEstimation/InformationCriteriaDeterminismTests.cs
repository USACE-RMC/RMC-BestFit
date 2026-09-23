using System.ComponentModel;
using System.Xml.Linq;
using Numerics.Mathematics.Optimization;
using Numerics.Sampling.MCMC;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;

namespace RMC.BestFit.Tests.ModelEstimation;

/// <summary>
/// Verifies that the information criteria computed from identical posterior draws are exactly
/// reproducible across analysis instances.
/// </summary>
/// <remarks>
/// DIC and WAIC formerly combined their per-draw and per-observation terms through a shared
/// scalar accumulator whose addition order followed the thread scheduler, so the reported values
/// could differ in their last bits between runs and machines; floating-point addition is not
/// associative. Both now write per-index terms and sum them sequentially, matching the PSIS-LOO
/// reduction, so two analyses over the same injected results must agree to the exact bit. The
/// fixture spans several orders of magnitude specifically so that any reassociation of the
/// summation order would change the last bits.
/// </remarks>
[TestClass]
public class InformationCriteriaDeterminismTests
{
    /// <summary>
    /// Two analyses over the same injected posterior report bitwise-identical criteria.
    /// </summary>
    [TestMethod]
    public void InformationCriteria_FromIdenticalDraws_AreBitwiseReproducible()
    {
        var first = CreateAnalysis();
        var second = CreateAnalysis();

        Assert.AreEqual(first.DIC, second.DIC, 0d);
        Assert.AreEqual(first.WAIC, second.WAIC, 0d);
        Assert.AreEqual(first.WAIC_pD, second.WAIC_pD, 0d);
        Assert.AreEqual(first.LOOIC, second.LOOIC, 0d);
        Assert.AreEqual(first.LOO_pD, second.LOO_pD, 0d);
        Assert.AreEqual(first.LOOIC_SE, second.LOOIC_SE, 0d);
        Assert.IsFalse(double.IsNaN(first.DIC), "Fixture precondition: the criteria were computed.");
        Assert.IsFalse(double.IsNaN(first.WAIC), "Fixture precondition: the criteria were computed.");
    }

    /// <summary>
    /// Builds an analysis over a deterministic inline pointwise matrix without fitting a model.
    /// </summary>
    /// <returns>The analysis with computed information criteria.</returns>
    private static BayesianAnalysis CreateAnalysis()
    {
        const int drawCount = 96;
        const int observationCount = 48;
        var pointwise = new double[drawCount][];
        for (int draw = 0; draw < drawCount; draw++)
        {
            pointwise[draw] = new double[observationCount];
            for (int observation = 0; observation < observationCount; observation++)
            {
                // Deterministic magnitude-spanning log likelihoods (roughly -1E-1 to -1.5E+3).
                pointwise[draw][observation] =
                    -Math.Exp(((draw * 31 + observation * 17) % 97) / 10.0) / (1.0 + 0.01 * observation);
            }
        }

        var model = new PointwiseMatrixModel(pointwise);
        var output = Enumerable.Range(0, drawCount)
            .Select(index => new ParameterSet([(double)index], 0d))
            .ToList();
        var results = new MCMCResults(new ParameterSet([0d], 0d), output, alpha: 0.10);
        var analysis = new BayesianAnalysis(model);
        analysis.SetCustomMCMCResults(results);
        return analysis;
    }

    /// <summary>
    /// Minimal pointwise model that maps the synthetic draw-index parameter to one matrix row.
    /// </summary>
    private sealed class PointwiseMatrixModel : IModel
    {
        private readonly double[][] _pointwise;

        /// <summary>
        /// Initializes the model around a draw-by-observation matrix.
        /// </summary>
        /// <param name="pointwise">The pointwise likelihood matrix.</param>
        public PointwiseMatrixModel(double[][] pointwise)
        {
            _pointwise = pointwise;
        }

        /// <inheritdoc/>
        public List<ModelParameter> Parameters { get; } = new();

        /// <inheritdoc/>
        public int NumberOfParameters => 1;

        /// <inheritdoc/>
        public bool UseDefaultFlatPriors { get; set; }

        /// <inheritdoc/>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <inheritdoc/>
        public double LogLikelihood(double[] parameters) => DataLogLikelihood(parameters);

        /// <inheritdoc/>
        public double DataLogLikelihood(double[] parameters) => Row(parameters).Sum();

        /// <inheritdoc/>
        public double[] PointwiseDataLogLikelihood(double[] parameters) => (double[])Row(parameters).Clone();

        /// <inheritdoc/>
        public List<DataComponent> PointwiseDataLogLikelihoodComponents(double[] parameters)
            => Row(parameters).Select((value, index) => new DataComponent(index, value, index)).ToList();

        /// <inheritdoc/>
        public double PriorLogLikelihood(double[] parameters) => 0d;

        /// <inheritdoc/>
        public List<PriorComponent> PointwisePriorLogLikelihood(double[] parameters) => new();

        /// <inheritdoc/>
        public void SetParameterValues(IList<double> parameters) { }

        /// <inheritdoc/>
        public void SetDefaultParameters() { }

        /// <inheritdoc/>
        public IModel Clone() => new PointwiseMatrixModel(_pointwise);

        /// <inheritdoc/>
        public XElement ToXElement() => new("PointwiseMatrixModel");

        /// <inheritdoc/>
        public (bool IsValid, List<string> ValidationMessages) Validate() => (true, new List<string>());

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
