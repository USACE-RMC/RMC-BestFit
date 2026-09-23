using Numerics.Distributions;
using Numerics.Mathematics.Optimization;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using System.Xml.Linq;

namespace RMC.BestFit.Verification.ModelEstimation;

/// <summary>Verifies MAP AIC and BIC formulas against independently evaluated data likelihoods.</summary>
/// <remarks>
/// These are analytical formula cells, distinct from generated-parent recovery. The fixture uses
/// 100 scalar Normal observations with seed=12345 only to supply a nonconstant likelihood; neither
/// claim is a recovery sample-size assertion or a conditional secondary 5% criterion.
/// </remarks>
[TestClass]
public sealed class MaximumAPosterioriInformationCriteriaOracleTests
{
    /// <summary>Verifies that AIC uses the data likelihood at MAP and excludes prior density.</summary>
    /// <remarks>
    /// This analytical identity uses 100 seeded scalar Normal observations (seed=12345; generating
    /// parent μ=100, σ=10) only to produce a nonconstant likelihood. It compares the MAP fitted
    /// coordinates (μ, σ) through the data-likelihood AIC formula; no recovery interval, response
    /// coordinate, uncertainty source, interval width, or secondary five-percent rule applies.
    /// </remarks>
    [TestMethod]
    public void AIC_UsesDataLikelihoodAtMap_ExcludesPriorDensity()
    {
        var model = new NormalLocationScaleModel(CreateNormalData());
        var map = new MaximumAPosteriori(model, OptimizationMethod.DifferentialEvolution);
        Assert.IsTrue(map.Estimate(), "MAP estimation failed.");
        double dataLogLikelihood = model.DataLogLikelihood(map.BestParameterSet.Values);
        double posteriorLogLikelihood = model.LogLikelihood(map.BestParameterSet.Values);
        Assert.AreEqual(-2d * dataLogLikelihood + (2d * map.NumberOfParameters), map.GetAIC(), 1E-10d);
        Assert.IsTrue(Math.Abs(map.GetAIC() - (-2d * posteriorLogLikelihood + (2d * map.NumberOfParameters))) > 1E-6d);
    }

    /// <summary>Verifies that BIC uses the data likelihood at MAP and excludes prior density.</summary>
    /// <remarks>
    /// This analytical identity uses 100 seeded scalar Normal observations (seed=12345; generating
    /// parent μ=100, σ=10) only to produce a nonconstant likelihood. It compares the MAP fitted
    /// coordinates (μ, σ) through the data-likelihood BIC formula; no recovery interval, response
    /// coordinate, uncertainty source, interval width, or secondary five-percent rule applies.
    /// </remarks>
    [TestMethod]
    public void BIC_UsesDataLikelihoodAtMap_ExcludesPriorDensity()
    {
        double[] data = CreateNormalData();
        var model = new NormalLocationScaleModel(data);
        var map = new MaximumAPosteriori(model, OptimizationMethod.DifferentialEvolution);
        Assert.IsTrue(map.Estimate(), "MAP estimation failed.");
        double dataLogLikelihood = model.DataLogLikelihood(map.BestParameterSet.Values);
        double posteriorLogLikelihood = model.LogLikelihood(map.BestParameterSet.Values);
        Assert.AreEqual(-2d * dataLogLikelihood + (map.NumberOfParameters * Math.Log(data.Length)), map.GetBIC(data.Length), 1E-10d);
        Assert.IsTrue(Math.Abs(map.GetBIC(data.Length) - (-2d * posteriorLogLikelihood + (map.NumberOfParameters * Math.Log(data.Length)))) > 1E-6d);
    }

    /// <summary>Creates the fixed seeded fixture for the formula checks.</summary>
    /// <returns>One hundred scalar Normal observations.</returns>
    /// <remarks>
    /// The fixture has parent μ=100 and σ=10 with seed=12345. It is intentionally an analytical
    /// formula input rather than a generated-parent recovery design, so it has no uncertainty band
    /// or secondary response criterion.
    /// </remarks>
    private static double[] CreateNormalData()
    {
        var random = new Random(12345);
        var data = new double[100];
        for (int index = 0; index < data.Length; index++)
        {
            double radius = Math.Sqrt(-2d * Math.Log(random.NextDouble()));
            data[index] = 100d + (10d * radius * Math.Cos(2d * Math.PI * random.NextDouble()));
        }
        return data;
    }

    /// <summary>Provides the flat-prior Normal location-scale model used by the analytical formulas.</summary>
    private sealed class NormalLocationScaleModel : ModelBase
    {
        private readonly double[] _data;

        /// <summary>Initializes the analytical fixture.</summary>
        /// <param name="data">Observed scalar data.</param>
        /// <remarks>
        /// The model holds the fixed 100-observation, seed-12345 Normal fixture with parent μ=100
        /// and σ=10. Its fitted coordinates support the AIC/BIC identities only; no recovery band,
        /// response grid, or secondary-five-percent criterion is defined here.
        /// </remarks>
        public NormalLocationScaleModel(double[] data) { _data = data; SetDefaultParameters(); }

        /// <inheritdoc/>
        public override void SetDefaultParameters() => Parameters =
        [
            new ModelParameter("Normal", "mu", 0d, -1000d, 1000d, new Uniform(-1000d, 1000d)),
            new ModelParameter("Normal", "sigma", 1d, 0.001d, 1000d, new Uniform(0.001d, 1000d))
        ];

        /// <inheritdoc/>
        public override double DataLogLikelihood(double[] parameters) => PointwiseDataLogLikelihood(parameters).Sum();

        /// <inheritdoc/>
        public override double[] PointwiseDataLogLikelihood(double[] parameters) => parameters[1] <= 0d
            ? Enumerable.Repeat(double.NegativeInfinity, _data.Length).ToArray()
            : _data.Select(value => new Normal(parameters[0], parameters[1]).LogPDF(value)).ToArray();

        /// <inheritdoc/>
        public override List<DataComponent> PointwiseDataLogLikelihoodComponents(double[] parameters) => PointwiseDataLogLikelihood(parameters).Select((value, index) => new DataComponent(index, value, _data[index])).ToList();

        /// <inheritdoc/>
        public override IModel Clone()
        {
            var clone = new NormalLocationScaleModel(_data);
            clone.SetParameterValues(Parameters.Select(parameter => parameter.Value).ToArray());
            return clone;
        }

        /// <inheritdoc/>
        public override XElement ToXElement() => new(nameof(NormalLocationScaleModel));

        /// <inheritdoc/>
        public override (bool IsValid, List<string> ValidationMessages) Validate() => (true, []);
    }
}
