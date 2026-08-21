using Numerics.Distributions;
using Numerics.Mathematics.LinearAlgebra;
using Numerics.Mathematics.Optimization;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using System.Xml.Linq;

namespace RMC.BestFit.Verification.ModelEstimation;

/// <summary>
/// Verifies the maximum-likelihood covariance against closed-form information for a one-parameter
/// model and the covariance parity between maximum likelihood and flat-prior maximum a posteriori.
/// </summary>
[TestClass]
public sealed class MaximumLikelihoodCovarianceVerificationTests
{
    private static readonly double[] Observations = [1.2, 0.7, 1.9, 1.1, 0.4, 1.6, 0.9, 1.3, 1.0, 1.5];
    private const double KnownScale = 0.5;

    /// <summary>
    /// For a normal mean with known scale the information is n / sigma^2, so the single-parameter
    /// covariance is sigma^2 / n and Cook's distances are finite.
    /// </summary>
    [TestMethod]
    public void MLE_OneParameterModel_CovarianceMatchesClosedFormInformation()
    {
        var model = new KnownScaleNormalMeanModel(Observations, KnownScale);
        var estimator = new MaximumLikelihood(model, OptimizationMethod.Brent)
        {
            ComputeHessian = true,
            ReportFailure = true
        };

        Assert.IsTrue(estimator.Estimate(), "Brent search on a concave quadratic log-likelihood must converge.");
        Assert.AreEqual(Observations.Average(), estimator.BestParameterSet.Values[0], 1e-6);

        double expectedVariance = KnownScale * KnownScale / Observations.Length;
        Matrix covariance = estimator.GetCovarianceMatrix();
        Assert.AreEqual(1, covariance.NumberOfRows);
        Assert.AreEqual(expectedVariance, covariance[0, 0], 1e-8 * expectedVariance);
        Assert.AreEqual(Math.Sqrt(expectedVariance), estimator.GetStandardErrors()[0], 1e-8 * Math.Sqrt(expectedVariance));

        double[] cooks = estimator.GetCooksDistance();
        Assert.AreEqual(Observations.Length, cooks.Length);
        Assert.IsTrue(cooks.All(distance => double.IsFinite(distance) && distance >= 0.0));
    }

    /// <summary>
    /// With flat priors the posterior Hessian equals the data Hessian up to an additive constant, so
    /// maximum likelihood and maximum a posteriori must report the same covariance. The interior
    /// case also matches the exact unit curvature; the bound case exercises the bounded finite
    /// differences that both estimators must share.
    /// </summary>
    [TestMethod]
    public void MLE_AndFlatPriorMAP_ReportTheSameCovariance()
    {
        // Interior optimum: exact unit curvature.
        var interiorMle = new MaximumLikelihood(new BoundedQuadraticModel(0.3, -0.2, upperBoundOne: 5.0), OptimizationMethod.BFGS)
        {
            ReportFailure = true
        };
        var interiorMap = new MaximumAPosteriori(new BoundedQuadraticModel(0.3, -0.2, upperBoundOne: 5.0), OptimizationMethod.BFGS)
        {
            ReportFailure = true
        };
        Assert.IsTrue(interiorMle.Estimate());
        Assert.IsTrue(interiorMap.Estimate());

        // The adaptive finite-difference Hessian reproduces the unit curvature to about 1e-5;
        // the parity tolerance covers the roundoff introduced by the constant flat-prior term.
        const double numericalHessianTolerance = 1e-4;
        Matrix interiorMleCovariance = interiorMle.GetCovarianceMatrix();
        Matrix interiorMapCovariance = interiorMap.GetCovarianceMatrix();
        for (int row = 0; row < 2; row++)
        {
            for (int column = 0; column < 2; column++)
            {
                double expected = row == column ? 1.0 : 0.0;
                Assert.AreEqual(expected, interiorMleCovariance[row, column], numericalHessianTolerance);
                Assert.AreEqual(interiorMleCovariance[row, column], interiorMapCovariance[row, column], numericalHessianTolerance);
            }
        }

        // Optimum on the upper bound of the first parameter: the finite differences are clipped to
        // the support, and both estimators must clip them the same way.
        var boundMle = new MaximumLikelihood(new BoundedQuadraticModel(1.5, -0.2, upperBoundOne: 1.0), OptimizationMethod.BFGS)
        {
            ReportFailure = true
        };
        var boundMap = new MaximumAPosteriori(new BoundedQuadraticModel(1.5, -0.2, upperBoundOne: 1.0), OptimizationMethod.BFGS)
        {
            ReportFailure = true
        };
        Assert.IsTrue(boundMle.Estimate());
        Assert.IsTrue(boundMap.Estimate());
        Assert.AreEqual(1.0, boundMle.BestParameterSet.Values[0], 1e-6);
        Assert.AreEqual(1.0, boundMap.BestParameterSet.Values[0], 1e-6);

        Matrix boundMleCovariance = boundMle.GetCovarianceMatrix();
        Matrix boundMapCovariance = boundMap.GetCovarianceMatrix();
        for (int row = 0; row < 2; row++)
        {
            for (int column = 0; column < 2; column++)
            {
                Assert.IsTrue(double.IsFinite(boundMleCovariance[row, column]));
                double scale = Math.Max(1.0, Math.Abs(boundMleCovariance[row, column]));
                Assert.AreEqual(boundMleCovariance[row, column], boundMapCovariance[row, column], numericalHessianTolerance * scale);
            }
        }
    }

    /// <summary>
    /// A two-parameter quadratic log-likelihood with unit curvature and a configurable upper bound on
    /// the first parameter.
    /// </summary>
    private sealed class BoundedQuadraticModel : ModelBase
    {
        private readonly double _centerOne;
        private readonly double _centerTwo;
        private readonly double _upperBoundOne;

        /// <summary>
        /// Creates the model.
        /// </summary>
        /// <param name="centerOne">Unconstrained optimum of the first parameter.</param>
        /// <param name="centerTwo">Unconstrained optimum of the second parameter.</param>
        /// <param name="upperBoundOne">Upper bound of the first parameter.</param>
        public BoundedQuadraticModel(double centerOne, double centerTwo, double upperBoundOne)
        {
            _centerOne = centerOne;
            _centerTwo = centerTwo;
            _upperBoundOne = upperBoundOne;
            SetDefaultParameters();
        }

        /// <inheritdoc/>
        public override void SetDefaultParameters()
        {
            Parameters = new List<ModelParameter>
            {
                new("Quadratic", "theta1", 0.0, -5.0, _upperBoundOne, new Uniform(-5.0, _upperBoundOne)),
                new("Quadratic", "theta2", 0.0, -5.0, 5.0, new Uniform(-5.0, 5.0))
            };
        }

        /// <inheritdoc/>
        public override double DataLogLikelihood(double[] parameters)
        {
            double one = parameters[0] - _centerOne;
            double two = parameters[1] - _centerTwo;
            return -0.5 * (one * one + two * two);
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
            var clone = new BoundedQuadraticModel(_centerOne, _centerTwo, _upperBoundOne);
            clone.SetParameterValues(Parameters.Select(parameter => parameter.Value).ToArray());
            return clone;
        }

        /// <inheritdoc/>
        public override XElement ToXElement() => new(nameof(BoundedQuadraticModel));

        /// <inheritdoc/>
        public override (bool IsValid, List<string> ValidationMessages) Validate()
            => (true, new List<string>());
    }

    /// <summary>
    /// A normal mean with known scale: the single parameter is the mean.
    /// </summary>
    private sealed class KnownScaleNormalMeanModel : ModelBase
    {
        private readonly double[] _observations;
        private readonly double _scale;

        /// <summary>
        /// Creates the model.
        /// </summary>
        /// <param name="observations">Observed values.</param>
        /// <param name="scale">Known standard deviation.</param>
        public KnownScaleNormalMeanModel(double[] observations, double scale)
        {
            _observations = observations;
            _scale = scale;
            SetDefaultParameters();
        }

        /// <inheritdoc/>
        public override void SetDefaultParameters()
        {
            Parameters = new List<ModelParameter>
            {
                new("Normal mean", "mu", 0.0, -10.0, 10.0, new Uniform(-10.0, 10.0))
            };
        }

        /// <inheritdoc/>
        public override double DataLogLikelihood(double[] parameters)
            => PointwiseDataLogLikelihood(parameters).Sum();

        /// <inheritdoc/>
        public override double[] PointwiseDataLogLikelihood(double[] parameters)
        {
            double mu = parameters[0];
            double logNormalization = -Math.Log(_scale * Math.Sqrt(2.0 * Math.PI));
            return _observations
                .Select(x => logNormalization - (x - mu) * (x - mu) / (2.0 * _scale * _scale))
                .ToArray();
        }

        /// <inheritdoc/>
        public override List<DataComponent> PointwiseDataLogLikelihoodComponents(double[] parameters)
        {
            double[] pointwise = PointwiseDataLogLikelihood(parameters);
            return pointwise
                .Select((value, index) => new DataComponent(index, value, _observations[index]))
                .ToList();
        }

        /// <inheritdoc/>
        public override IModel Clone()
        {
            var clone = new KnownScaleNormalMeanModel(_observations, _scale);
            clone.SetParameterValues(Parameters.Select(parameter => parameter.Value).ToArray());
            return clone;
        }

        /// <inheritdoc/>
        public override XElement ToXElement() => new(nameof(KnownScaleNormalMeanModel));

        /// <inheritdoc/>
        public override (bool IsValid, List<string> ValidationMessages) Validate()
            => (true, new List<string>());
    }
}
