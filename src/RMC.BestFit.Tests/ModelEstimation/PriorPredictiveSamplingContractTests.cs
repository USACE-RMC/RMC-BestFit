using Numerics.Distributions;
using Numerics.Mathematics.Optimization;
using RMC.BestFit.Diagnostics;
using RMC.BestFit.Models;
using System.Xml.Linq;

namespace RMC.BestFit.Tests.ModelEstimation;

/// <summary>
/// Verifies deterministic prior-predictive sampling contracts without running an estimator.
/// </summary>
[TestClass]
public sealed class PriorPredictiveSamplingContractTests
{
    /// <summary>
    /// Verifies that prior samples store the negative full model prior, including joint terms,
    /// rather than the sum of marginal parameter priors or the unnegated density.
    /// </summary>
    [TestMethod]
    public void SampleFromPriors_StoresNegativeFullModelPriorLogLikelihood()
    {
        const double fullPriorLogLikelihood = -7.25;
        var model = new ConstantJointPriorModel(fullPriorLogLikelihood);
        var check = new PriorPredictiveCheck(model)
        {
            NumberOfDraws = 4,
            Seed = 24680
        };

        IList<ParameterSet> samples = check.SampleFromPriors();

        Assert.AreEqual(4, samples.Count);
        foreach (ParameterSet sample in samples)
        {
            Assert.AreEqual(
                -fullPriorLogLikelihood,
                sample.Fitness,
                1e-12,
                "Fitness must store the negative full model prior log-likelihood.");
        }
    }

    /// <summary>
    /// Supplies a constant full prior that differs from its marginal prior density so the test
    /// detects either a sign error or replacement with marginal-only evaluation.
    /// </summary>
    private sealed class ConstantJointPriorModel : ModelBase, ISimulatable<double[]>
    {
        private readonly double _fullPriorLogLikelihood;

        /// <summary>
        /// Initializes the deterministic joint-prior fixture.
        /// </summary>
        /// <param name="fullPriorLogLikelihood">The full model prior returned for every valid draw.</param>
        public ConstantJointPriorModel(double fullPriorLogLikelihood)
        {
            _fullPriorLogLikelihood = fullPriorLogLikelihood;
            _useDefaultFlatPriors = false;
            SetDefaultParameters();
        }

        /// <inheritdoc/>
        public override void SetDefaultParameters()
        {
            Parameters =
            [
                new ModelParameter("Joint prior fixture", "x", 0.0, -1.0, 1.0, new Uniform(-1.0, 1.0))
            ];
        }

        /// <inheritdoc/>
        public override double DataLogLikelihood(double[] parameters) => 0.0;

        /// <inheritdoc/>
        public override double[] PointwiseDataLogLikelihood(double[] parameters) => [0.0];

        /// <inheritdoc/>
        public override List<DataComponent> PointwiseDataLogLikelihoodComponents(double[] parameters)
            => [new DataComponent(0, 0.0, 0.0, name: "synthetic")];

        /// <inheritdoc/>
        public override double PriorLogLikelihood(double[] parameters)
            => parameters.Length == NumberOfParameters
                ? _fullPriorLogLikelihood
                : double.NegativeInfinity;

        /// <inheritdoc/>
        public double[] GenerateRandomValues(int sampleSize, int seed = -1)
            => Enumerable.Repeat(0.0, sampleSize).ToArray();

        /// <inheritdoc/>
        public override IModel Clone()
        {
            var clone = new ConstantJointPriorModel(_fullPriorLogLikelihood);
            clone.SetParameterValues(Parameters.Select(parameter => parameter.Value).ToArray());
            return clone;
        }

        /// <inheritdoc/>
        public override XElement ToXElement() => new(nameof(ConstantJointPriorModel));

        /// <inheritdoc/>
        public override (bool IsValid, List<string> ValidationMessages) Validate()
            => (true, []);
    }
}
