using Numerics.Distributions;
using Numerics.Mathematics.Optimization;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using System.Xml.Linq;

namespace RMC.BestFit.Verification.ModelEstimation;

/// <summary>
/// Verifies supported profile-likelihood grid ordinates against the closed-form profile of a
/// correlated quadratic model. Unsupported grid points are outside this numerical claim.
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
    /// Verifies maximum-likelihood profile ordinates inside the fixture support against the
    /// independently derived quadratic profile.
    /// </summary>
    [TestMethod]
    public void MLE_ProfileLikelihood_SupportedGridMatchesClosedFormProfile()
    {
        var estimator = new MaximumLikelihood(CreateModel(SupportCutoff), OptimizationMethod.BFGS)
        {
            ComputeHessian = false,
            ReportFailure = true
        };
        Assert.IsTrue(estimator.Estimate());
        Assert.AreEqual(0.0, estimator.BestParameterSet.Values[0], 1e-6);
        Assert.AreEqual(0.0, estimator.BestParameterSet.Values[1], 1e-6);

        double[,] actual = estimator.ProfileLikelihood(Bins)[0];

        AssertSupportedProfileMatchesClosedForm(actual, logPriorConstant: 0.0);
    }

    /// <summary>
    /// Verifies flat-prior MAP profile ordinates inside the fixture support against the independently
    /// derived quadratic profile plus the constant Uniform-prior log density.
    /// </summary>
    [TestMethod]
    public void MAP_ProfileLikelihood_SupportedGridMatchesClosedFormProfile()
    {
        var estimator = new MaximumAPosteriori(CreateModel(SupportCutoff), OptimizationMethod.BFGS)
        {
            ComputeHessian = false,
            ReportFailure = true
        };
        Assert.IsTrue(estimator.Estimate());

        double[,] actual = estimator.ProfileLikelihood(Bins)[0];

        double logPriorConstant = -2.0 * Math.Log(UpperBound - LowerBound);
        AssertSupportedProfileMatchesClosedForm(actual, logPriorConstant);
    }

    /// <summary>
    /// Compares every supported grid ordinate with the closed-form profile. For fixed
    /// <c>theta1 = t</c>, the nuisance optimum is <c>theta2 = rho * t</c> and the profiled
    /// quadratic log likelihood is <c>-t^2 / 2</c>.
    /// </summary>
    /// <param name="actual">Profile of the support-restricted model.</param>
    /// <param name="logPriorConstant">Constant log-prior contribution for the profiled model.</param>
    private static void AssertSupportedProfileMatchesClosedForm(double[,] actual, double logPriorConstant)
    {
        int supportedCount = 0;
        for (int index = 0; index < Bins; index++)
        {
            double expectedGridValue = LowerBound + ((index + 0.5) * (UpperBound - LowerBound) / Bins);
            Assert.AreEqual(expectedGridValue, actual[index, 0], 1e-12, "Grid abscissa differs from the declared stratification.");
            if (actual[index, 0] < SupportCutoff)
                continue;

            double expectedProfile = (-0.5 * actual[index, 0] * actual[index, 0]) + logPriorConstant;
            Assert.AreEqual(expectedProfile, actual[index, 1], 1e-6, "Supported profile ordinate differs from the closed-form oracle.");
            supportedCount++;
        }

        Assert.AreEqual(10, supportedCount, "Ten of the sixteen grid midpoints lie inside the declared support.");
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
