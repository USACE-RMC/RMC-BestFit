using Numerics.Distributions;
using Numerics.Mathematics.Optimization;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using System.Xml.Linq;

namespace RMC.BestFit.Verification.ModelEstimation;

/// <summary>
/// Verifies that a profile-likelihood grid survives grid points whose nuisance optimization has no
/// finite optimum: those points are reported as NaN, the remaining points equal the profile of the
/// unrestricted model, and the likelihood-ratio interval solve still requires converged solves.
/// </summary>
[TestClass]
public sealed class ProfileLikelihoodGridPointFailureTests
{
    private const double Rho = 0.5;
    private const double LowerBound = -4.0;
    private const double UpperBound = 4.0;
    private const double SupportCutoff = -1.0;
    private const int Bins = 16;
    private static readonly double[] Start = [0.5, 0.5];

    /// <summary>
    /// Maximum likelihood: grid points of the profiled parameter below the support cutoff have no
    /// finite nuisance optimum and are reported as NaN; the other points match the unrestricted profile.
    /// </summary>
    [TestMethod]
    public void MLE_ProfileLikelihood_ReportsNaNForGridPointsWithoutFiniteNuisanceOptimum()
    {
        var reference = new MaximumLikelihood(CreateModel(double.NegativeInfinity), OptimizationMethod.BFGS)
        {
            ComputeHessian = false,
            ReportFailure = true
        };
        var estimator = new MaximumLikelihood(CreateModel(SupportCutoff), OptimizationMethod.BFGS)
        {
            ComputeHessian = false,
            ReportFailure = true
        };
        Assert.IsTrue(reference.Estimate());
        Assert.IsTrue(estimator.Estimate());
        Assert.AreEqual(0.0, estimator.BestParameterSet.Values[0], 1e-6);
        Assert.AreEqual(0.0, estimator.BestParameterSet.Values[1], 1e-6);

        double[,] expected = reference.ProfileLikelihood(Bins)[0];
        double[,] actual = estimator.ProfileLikelihood(Bins)[0];

        AssertRestrictedProfile(expected, actual);
        Assert.ThrowsException<InvalidOperationException>(
            () => estimator.ParameterConfidenceIntervals(0.1),
            "The interval solve evaluates the profile at the parameter bound, where no finite nuisance optimum exists.");
    }

    /// <summary>
    /// Maximum a posteriori with flat priors: the same grid-point contract as maximum likelihood.
    /// </summary>
    [TestMethod]
    public void MAP_ProfileLikelihood_ReportsNaNForGridPointsWithoutFiniteNuisanceOptimum()
    {
        var reference = new MaximumAPosteriori(CreateModel(double.NegativeInfinity), OptimizationMethod.BFGS)
        {
            ComputeHessian = false,
            ReportFailure = true
        };
        var estimator = new MaximumAPosteriori(CreateModel(SupportCutoff), OptimizationMethod.BFGS)
        {
            ComputeHessian = false,
            ReportFailure = true
        };
        Assert.IsTrue(reference.Estimate());
        Assert.IsTrue(estimator.Estimate());

        double[,] expected = reference.ProfileLikelihood(Bins)[0];
        double[,] actual = estimator.ProfileLikelihood(Bins)[0];

        AssertRestrictedProfile(expected, actual);
        Assert.ThrowsException<InvalidOperationException>(
            () => estimator.ParameterConfidenceIntervals(0.1));
    }

    /// <summary>
    /// Asserts that the restricted profile is NaN below the support cutoff and equals the unrestricted
    /// profile elsewhere.
    /// </summary>
    /// <param name="expected">Profile of the unrestricted model.</param>
    /// <param name="actual">Profile of the support-restricted model.</param>
    private static void AssertRestrictedProfile(double[,] expected, double[,] actual)
    {
        int nanCount = 0;
        for (int index = 0; index < Bins; index++)
        {
            Assert.AreEqual(expected[index, 0], actual[index, 0], 1e-12, "Grid abscissae must agree.");
            if (actual[index, 0] < SupportCutoff)
            {
                Assert.IsTrue(double.IsNaN(actual[index, 1]), $"Grid point {actual[index, 0]} lies outside the support.");
                nanCount++;
            }
            else
            {
                Assert.IsTrue(double.IsFinite(actual[index, 1]), $"Grid point {actual[index, 0]} lies inside the support.");
                Assert.AreEqual(expected[index, 1], actual[index, 1], 1e-6);
            }
        }

        Assert.AreEqual(6, nanCount, "Six of the sixteen grid midpoints lie below the support cutoff.");
    }

    /// <summary>
    /// Creates the correlated quadratic model with an optional support restriction on the first parameter.
    /// </summary>
    /// <param name="supportCutoff">Values of the first parameter below this cutoff have zero likelihood.</param>
    /// <returns>The model.</returns>
    private static SupportRestrictedQuadraticModel CreateModel(double supportCutoff)
        => new(Rho, LowerBound, UpperBound, Start, supportCutoff);

    /// <summary>
    /// A two-parameter correlated quadratic log-likelihood whose first parameter has zero likelihood
    /// below a cutoff inside its bounds.
    /// </summary>
    private sealed class SupportRestrictedQuadraticModel : ModelBase
    {
        private readonly double _rho;
        private readonly double _lowerBound;
        private readonly double _upperBound;
        private readonly double[] _start;
        private readonly double _supportCutoff;

        /// <summary>
        /// Creates the model.
        /// </summary>
        /// <param name="rho">Correlation of the quadratic form.</param>
        /// <param name="lowerBound">Lower parameter bound.</param>
        /// <param name="upperBound">Upper parameter bound.</param>
        /// <param name="start">Starting values.</param>
        /// <param name="supportCutoff">Values of the first parameter below this cutoff have zero likelihood.</param>
        public SupportRestrictedQuadraticModel(
            double rho,
            double lowerBound,
            double upperBound,
            IReadOnlyList<double> start,
            double supportCutoff)
        {
            _rho = rho;
            _lowerBound = lowerBound;
            _upperBound = upperBound;
            _start = start.ToArray();
            _supportCutoff = supportCutoff;
            SetDefaultParameters();
        }

        /// <inheritdoc/>
        public override void SetDefaultParameters()
        {
            Parameters = new List<ModelParameter>
            {
                new("Quadratic", "theta1", _start[0], _lowerBound, _upperBound, new Uniform(_lowerBound, _upperBound)),
                new("Quadratic", "theta2", _start[1], _lowerBound, _upperBound, new Uniform(_lowerBound, _upperBound))
            };
        }

        /// <inheritdoc/>
        public override double DataLogLikelihood(double[] parameters)
        {
            double theta1 = parameters[0];
            double theta2 = parameters[1];
            if (theta1 < _supportCutoff)
                return double.NegativeInfinity;

            double numerator =
                (theta1 * theta1) -
                (2.0 * _rho * theta1 * theta2) +
                (theta2 * theta2);
            return -numerator / (2.0 * (1.0 - (_rho * _rho)));
        }

        /// <inheritdoc/>
        public override double[] PointwiseDataLogLikelihood(double[] parameters)
            => new[] { DataLogLikelihood(parameters) };

        /// <inheritdoc/>
        public override List<DataComponent> PointwiseDataLogLikelihoodComponents(double[] parameters)
            => new() { new DataComponent(0, DataLogLikelihood(parameters), 0.0, name: "quadratic fixture") };

        /// <inheritdoc/>
        public override IModel Clone()
        {
            var clone = new SupportRestrictedQuadraticModel(_rho, _lowerBound, _upperBound, _start, _supportCutoff);
            clone.SetParameterValues(Parameters.Select(parameter => parameter.Value).ToArray());
            return clone;
        }

        /// <inheritdoc/>
        public override XElement ToXElement() => new(nameof(SupportRestrictedQuadraticModel));

        /// <inheritdoc/>
        public override (bool IsValid, List<string> ValidationMessages) Validate()
            => (true, new List<string>());
    }
}
