using System.ComponentModel;
using System.Globalization;
using System.Xml.Linq;
using Numerics.Distributions;
using Numerics.Mathematics.Optimization;
using Numerics.Sampling.MCMC;
using RMC.BestFit.Diagnostics;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;

namespace RMC.BestFit.Tests.ModelEstimation;

/// <summary>
/// Verifies the PSIS-LOO Pareto-k reporting for degenerate tail fits and for small retained draw
/// counts, using posterior draws injected without running a sampler.
/// </summary>
[TestClass]
public class PsisDegenerateTailDiagnosticTests
{
    /// <summary>
    /// With 25 retained draws the smoothing tail has five ratios, so the lower-quartile reference
    /// excess coincides with the smallest excess and no generalized Pareto fit exists. Such units must
    /// report an infinite Pareto k and be counted as unreliable.
    /// </summary>
    [TestMethod]
    public void ShortTail_ReportsInfiniteParetoK_AndUnreliableDiagnostics()
    {
        const int drawCount = 25;
        const int observationCount = 2;
        var pointwise = new double[drawCount][];
        for (int draw = 0; draw < drawCount; draw++)
        {
            pointwise[draw] = new double[observationCount];
            for (int observation = 0; observation < observationCount; observation++)
                pointwise[draw][observation] = -1.0 - 0.01 * draw * (observation + 1) - 0.001 * Math.Sin(draw + observation);
        }

        BayesianAnalysis analysis = CreateAnalysis(pointwise);

        InfluenceDiagnostics diagnostics = analysis.ComputeInfluenceDiagnostics();

        Assert.AreEqual(observationCount, analysis.ParetoK!.Length);
        Assert.IsTrue(analysis.ParetoK.All(double.IsPositiveInfinity),
            "A degenerate tail fit must report an infinite Pareto k.");
        Assert.IsFalse(diagnostics.IsReliable);
        Assert.AreEqual(observationCount, diagnostics.CountParetoKAbove10);
        StringAssert.Contains(analysis.GenerateReport(), $"{observationCount}/{observationCount} observations with k >=");
    }

    /// <summary>
    /// A tail whose five smallest excesses tie (repeated draws) has no usable lower-quartile reference
    /// and must report an infinite Pareto k, while a well-behaved unit keeps a finite estimate.
    /// </summary>
    [TestMethod]
    public void TiedLowerQuartileTailExcesses_ReportInfiniteParetoK()
    {
        const int drawCount = 100;
        var pointwise = new double[drawCount][];
        for (int draw = 0; draw < drawCount; draw++)
        {
            double first;
            if (draw >= 75 && draw < 90)
                first = -3.1 - 0.1 * (draw - 75);
            else if (draw >= 90 && draw < 95)
                first = -3.0;
            else
                first = -1.0 - 0.001 * draw;

            double second = -0.5 - 0.002 * draw;
            pointwise[draw] = new[] { first, second };
        }

        BayesianAnalysis analysis = CreateAnalysis(pointwise);

        analysis.ComputeInfluenceDiagnostics();

        Assert.IsTrue(double.IsPositiveInfinity(analysis.ParetoK![0]));
        Assert.IsTrue(double.IsFinite(analysis.ParetoK[1]));
    }

    /// <summary>
    /// With fewer than eleven retained draws the draw-count threshold formula is not positive; the
    /// diagnostics fall back to the fixed limit and no draw-count threshold is serialized or reported.
    /// </summary>
    [TestMethod]
    public void FewerThanElevenDraws_UseFixedLimit_WithoutNegativeThreshold()
    {
        const int drawCount = 8;
        var pointwise = new double[drawCount][];
        for (int draw = 0; draw < drawCount; draw++)
            pointwise[draw] = new[] { -1.0 - 0.1 * draw, -2.0 - 0.05 * draw };

        BayesianAnalysis analysis = CreateAnalysis(pointwise);

        XElement element = analysis.ComputeInfluenceDiagnostics().ToXElement();

        Assert.IsNull(element.Attribute("ParetoKDiagnosticThreshold"),
            "No draw-count threshold may be serialized when the formula is not positive.");
        StringAssert.Contains(analysis.GenerateReport(), "draw-count threshold unavailable");
    }

    /// <summary>
    /// With forty retained draws the draw-count threshold is serialized with its formula value.
    /// </summary>
    [TestMethod]
    public void FortyDraws_SerializeTheDrawCountThreshold()
    {
        const int drawCount = 40;
        var pointwise = new double[drawCount][];
        for (int draw = 0; draw < drawCount; draw++)
            pointwise[draw] = new[] { -1.0 - 0.01 * draw, -2.0 - 0.02 * draw };

        BayesianAnalysis analysis = CreateAnalysis(pointwise);

        XElement element = analysis.ComputeInfluenceDiagnostics().ToXElement();

        XAttribute? attribute = element.Attribute("ParetoKDiagnosticThreshold");
        Assert.IsNotNull(attribute);
        Assert.AreEqual(
            1.0 - 1.0 / Math.Log10(drawCount),
            double.Parse(attribute!.Value, CultureInfo.InvariantCulture),
            1e-12);
    }

    /// <summary>
    /// Builds a Bayesian analysis whose posterior draws index the rows of a pointwise log-likelihood table.
    /// </summary>
    /// <param name="pointwise">Pointwise log-likelihood rows, one per posterior draw.</param>
    /// <returns>The analysis with injected MCMC results.</returns>
    private static BayesianAnalysis CreateAnalysis(double[][] pointwise)
    {
        var model = new PointwiseTableModel(pointwise);
        var output = Enumerable.Range(0, pointwise.Length)
            .Select(index => new ParameterSet([(double)index], 0d))
            .ToList();
        var results = new MCMCResults(new ParameterSet([0d], 0d), output, alpha: 0.10);
        var analysis = new BayesianAnalysis(model);
        analysis.SetCustomMCMCResults(results);
        return analysis;
    }

    /// <summary>
    /// A model whose single parameter selects a row of pointwise log-likelihood values.
    /// </summary>
    private sealed class PointwiseTableModel : IModel
    {
        private readonly double[][] _pointwise;

        /// <summary>
        /// Creates the table model.
        /// </summary>
        /// <param name="pointwise">Pointwise log-likelihood rows, one per posterior draw.</param>
        public PointwiseTableModel(double[][] pointwise)
        {
            _pointwise = pointwise;
            Parameters = new List<ModelParameter>
            {
                new("Table", "row", 0d, -1d, pointwise.Length, new Uniform(-1d, pointwise.Length))
            };
        }

        /// <inheritdoc/>
        public List<ModelParameter> Parameters { get; }

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
        public void SetParameterValues(IList<double> parameters)
        {
            Parameters[0].Value = parameters[0];
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Parameters)));
        }

        /// <inheritdoc/>
        public void SetDefaultParameters()
        {
            Parameters[0].Value = 0d;
        }

        /// <inheritdoc/>
        public IModel Clone() => new PointwiseTableModel(_pointwise);

        /// <inheritdoc/>
        public XElement ToXElement() => new(nameof(PointwiseTableModel));

        /// <inheritdoc/>
        public (bool IsValid, List<string> ValidationMessages) Validate() => (true, new List<string>());

        /// <summary>
        /// Returns the pointwise row selected by the first parameter value.
        /// </summary>
        /// <param name="parameters">Parameter vector whose first value is the row index.</param>
        /// <returns>The pointwise log-likelihood row.</returns>
        private double[] Row(double[] parameters)
        {
            int row = (int)Math.Round(parameters[0]);
            row = Math.Clamp(row, 0, _pointwise.Length - 1);
            return _pointwise[row];
        }
    }
}
