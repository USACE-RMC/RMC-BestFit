using Numerics.Distributions;
using Numerics.Mathematics.Optimization;
using RMC.BestFit.Diagnostics;
using RMC.BestFit.Models;
using System.Xml.Linq;

namespace RMC.BestFit.Verification.ModelEstimation;

/// <summary>
/// Characterizes the documented TR-028 limitation for prior predictive parameter draws.
/// </summary>
/// <remarks>
/// The fixture has uniform marginal parameter priors and a narrow soft coupling term
/// centered on y = x. Independent marginal sampling should have near-zero correlation
/// and a mean squared separation near 2/3, which is incompatible with that joint term.
/// </remarks>
[TestClass]
public sealed class JointPriorSamplingFindingTests
{
    /// <summary>
    /// Confirms that prior sampling records but does not sample from a soft joint prior term.
    /// </summary>
    [TestMethod]
    public void SampleFromPriors_SoftJointPrior_CurrentlyDrawsIndependentMarginals()
    {
        const int drawCount = 20_000;
        var model = new SoftCoupledPriorModel();
        var check = new PriorPredictiveCheck(model)
        {
            Seed = 24680,
            NumberOfDraws = drawCount
        };

        IList<ParameterSet> samples = check.SampleFromPriors();
        double[] x = samples.Select(sample => sample.Values[0]).ToArray();
        double[] y = samples.Select(sample => sample.Values[1]).ToArray();
        double correlation = SampleCorrelation(x, y);
        double meanSquaredSeparation = samples.Average(
            sample =>
            {
                double difference = sample.Values[1] - sample.Values[0];
                return difference * difference;
            });
        double closeFraction = samples.Count(
            sample => Math.Abs(sample.Values[1] - sample.Values[0]) < 0.1) / (double)drawCount;

        Assert.AreEqual(drawCount, samples.Count);
        Assert.IsTrue(
            Math.Abs(correlation) < 0.03,
            $"Marginal draws should be independent; observed correlation was {correlation:G6}.");
        Assert.IsTrue(
            meanSquaredSeparation > 0.60,
            $"Independent Uniform(-1,1) draws should have E[(Y-X)^2] near 2/3; observed {meanSquaredSeparation:G6}.");
        Assert.IsTrue(
            closeFraction < 0.12,
            $"Independent draws should rarely satisfy the narrow y = x coupling; observed fraction {closeFraction:G6}.");

        foreach (ParameterSet sample in samples.Take(100))
        {
            Assert.AreEqual(
                -model.PriorLogLikelihood(sample.Values),
                sample.Fitness,
                1e-12,
                "Fitness should record the joint prior density even though it does not alter draw frequency.");
        }
    }

    /// <summary>
    /// Computes the ordinary sample correlation of two equal-length vectors.
    /// </summary>
    /// <param name="x">First sample vector.</param>
    /// <param name="y">Second sample vector.</param>
    /// <returns>The sample Pearson correlation.</returns>
    private static double SampleCorrelation(IReadOnlyList<double> x, IReadOnlyList<double> y)
    {
        if (x.Count != y.Count || x.Count < 2)
            throw new ArgumentException("Correlation inputs must have the same length and contain at least two values.");

        double meanX = x.Average();
        double meanY = y.Average();
        double covariance = 0.0;
        double varianceX = 0.0;
        double varianceY = 0.0;

        for (int i = 0; i < x.Count; i++)
        {
            double centeredX = x[i] - meanX;
            double centeredY = y[i] - meanY;
            covariance += centeredX * centeredY;
            varianceX += centeredX * centeredX;
            varianceY += centeredY * centeredY;
        }

        return covariance / Math.Sqrt(varianceX * varianceY);
    }

    /// <summary>
    /// Model with independent uniform marginals and an additional narrow y-given-x prior term.
    /// </summary>
    private sealed class SoftCoupledPriorModel : ModelBase, ISimulatable<double[]>
    {
        private const double CouplingStandardDeviation = 0.05;

        /// <summary>
        /// Initializes the coupled-prior fixture.
        /// </summary>
        public SoftCoupledPriorModel()
        {
            _useDefaultFlatPriors = false;
            SetDefaultParameters();
        }

        /// <inheritdoc/>
        public override void SetDefaultParameters()
        {
            Parameters = new List<ModelParameter>
            {
                new("Soft coupled prior", "x", 0.0, -1.0, 1.0, new Uniform(-1.0, 1.0)),
                new("Soft coupled prior", "y", 0.0, -1.0, 1.0, new Uniform(-1.0, 1.0))
            };
        }

        /// <inheritdoc/>
        public override double DataLogLikelihood(double[] parameters)
        {
            return 0.0;
        }

        /// <inheritdoc/>
        public override double[] PointwiseDataLogLikelihood(double[] parameters)
        {
            return new[] { 0.0 };
        }

        /// <inheritdoc/>
        public override List<DataComponent> PointwiseDataLogLikelihoodComponents(double[] parameters)
        {
            return new List<DataComponent> { new(0, 0.0, 0.0, name: "synthetic") };
        }

        /// <inheritdoc/>
        public override double PriorLogLikelihood(double[] parameters)
        {
            if (parameters.Length != NumberOfParameters)
                return double.NegativeInfinity;

            double marginalX = Parameters[0].PriorDistribution.LogPDF(parameters[0]);
            double marginalY = Parameters[1].PriorDistribution.LogPDF(parameters[1]);
            double coupling = new Normal(parameters[0], CouplingStandardDeviation).LogPDF(parameters[1]);
            return marginalX + marginalY + coupling;
        }

        /// <inheritdoc/>
        public override List<PriorComponent> PointwisePriorLogLikelihood(double[] parameters)
        {
            return new List<PriorComponent>
            {
                new("x marginal", Parameters[0].PriorDistribution.LogPDF(parameters[0])),
                new("y marginal", Parameters[1].PriorDistribution.LogPDF(parameters[1])),
                new(
                    "y given x coupling",
                    new Normal(parameters[0], CouplingStandardDeviation).LogPDF(parameters[1]),
                    PriorComponentType.OtherPenalty)
            };
        }

        /// <inheritdoc/>
        public double[] GenerateRandomValues(int sampleSize, int seed = -1)
        {
            if (sampleSize < 1)
                throw new ArgumentOutOfRangeException(nameof(sampleSize));

            return Enumerable.Repeat(Parameters[0].Value + Parameters[1].Value, sampleSize).ToArray();
        }

        /// <inheritdoc/>
        public override IModel Clone()
        {
            var clone = new SoftCoupledPriorModel();
            clone.SetParameterValues(Parameters.Select(parameter => parameter.Value).ToArray());
            return clone;
        }

        /// <inheritdoc/>
        public override XElement ToXElement()
        {
            return new XElement(nameof(SoftCoupledPriorModel));
        }

        /// <inheritdoc/>
        public override (bool IsValid, List<string> ValidationMessages) Validate()
        {
            return (true, new List<string>());
        }
    }
}
