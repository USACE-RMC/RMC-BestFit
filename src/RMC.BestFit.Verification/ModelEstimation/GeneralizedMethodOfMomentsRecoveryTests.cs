using System.ComponentModel;
using System.Xml.Linq;
using Numerics.Distributions;
using Numerics.Mathematics.LinearAlgebra;
using Numerics.Mathematics.Optimization;
using RMC.BestFit.Diagnostics;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;

namespace RMC.BestFit.Verification.ModelEstimation;

/// <summary>
/// Verifies GMM parameter recovery across one-step, two-step, iterative, and alternate-optimizer paths.
/// </summary>
[TestClass]
public class GeneralizedMethodOfMomentsRecoveryTests
{
#region Test Helpers

    /// <summary>
    /// Creates a simple moment condition function for testing.
    /// Estimates the mean of a normal distribution: E[X - μ] = 0.
    /// </summary>
    private static (MomentConditionFunction Function, double[] Data) CreateSimpleMeanMomentCondition()
    {
        // Generate test data: sample from N(100, 10)
        var random = new Random(12345);
        var data = new double[100];
        for (int i = 0; i < data.Length; i++)
        {
            // Box-Muller transform for normal random
            double u1 = random.NextDouble();
            double u2 = random.NextDouble();
            double z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
            data[i] = 100 + 10 * z;
        }

        MomentConditionFunction momentFunc = (parameters) =>
        {
            double mu = parameters[0];
            double[] g = new double[data.Length];
            for (int i = 0; i < data.Length; i++)
            {
                g[i] = data[i] - mu;
            }

            double gMean = g.Average();
            double gVar = g.Select(x => (x - gMean) * (x - gMean)).Average();

            var G = new Vector(new[] { gMean });
            var S = new Matrix(new double[,] { { gVar } });
            return (G, S);
        };

        return (momentFunc, data);
    }

    /// <summary>
    /// Creates a just-identified moment condition (2 params, 2 moments).
    /// Estimates mean and variance: E[X - μ] = 0, E[(X - μ)² - σ²] = 0.
    /// </summary>
    private static MomentConditionFunction CreateMeanVarianceMomentCondition(double[] data)
    {
        return (parameters) =>
        {
            double mu = parameters[0];
            double sigma2 = parameters[1];

            double g1 = 0; // Mean moment
            double g2 = 0; // Variance moment
            for (int i = 0; i < data.Length; i++)
            {
                g1 += data[i] - mu;
                g2 += (data[i] - mu) * (data[i] - mu) - sigma2;
            }
            g1 /= data.Length;
            g2 /= data.Length;

            // Simplified covariance matrix
            var G = new Vector(new[] { g1, g2 });
            var S = Matrix.Identity(2);
            return (G, S);
        };
    }

    /// <summary>
    /// Creates a moment condition function for Normal(μ, σ²) with proper covariance estimation.
    /// Returns both the function and an optional pointwise function for influence diagnostics.
    /// </summary>
    private static (MomentConditionFunction Function, PointwiseMomentConditionFunction Pointwise) CreateNormalMomentConditions(double[] data)
    {
        MomentConditionFunction momentFunc = (parameters) =>
        {
            double mu = parameters[0];
            double sigma2 = parameters[1];
            int n = data.Length;

            double g1Sum = 0, g2Sum = 0;
            for (int i = 0; i < n; i++)
            {
                double diff = data[i] - mu;
                g1Sum += diff;
                g2Sum += diff * diff - sigma2;
            }
            double g1 = g1Sum / n;
            double g2 = g2Sum / n;

            // Compute moment condition covariance
            double s11 = 0, s12 = 0, s22 = 0;
            for (int i = 0; i < n; i++)
            {
                double diff = data[i] - mu;
                double m1 = diff - g1;
                double m2 = (diff * diff - sigma2) - g2;
                s11 += m1 * m1;
                s12 += m1 * m2;
                s22 += m2 * m2;
            }
            s11 /= n; s12 /= n; s22 /= n;

            var G = new Vector(new[] { g1, g2 });
            var S = new Matrix(new double[,] { { s11, s12 }, { s12, s22 } });
            return (G, S);
        };

        PointwiseMomentConditionFunction pointwiseFunc = (parameters) =>
        {
            double mu = parameters[0];
            double sigma2 = parameters[1];
            int n = data.Length;
            var result = new double[n, 2];
            for (int i = 0; i < n; i++)
            {
                double diff = data[i] - mu;
                result[i, 0] = diff;
                result[i, 1] = diff * diff - sigma2;
            }
            return result;
        };

        return (momentFunc, pointwiseFunc);
    }

    /// <summary>
    /// Generates normally distributed test data using Box-Muller transform.
    /// </summary>
    private static double[] GenerateNormalData(double mean = 100, double stddev = 10, int n = 100, int seed = 12345)
    {
        var random = new Random(seed);
        var data = new double[n];
        for (int i = 0; i < n; i++)
        {
            double u1 = random.NextDouble();
            double u2 = random.NextDouble();
            double z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
            data[i] = mean + stddev * z;
        }
        return data;
    }

    /// <summary>
    /// A simple IGMMModel for Normal(μ, σ²) used in testing.
    /// Moment conditions: g₁ᵢ = xᵢ - μ, g₂ᵢ = (xᵢ - μ)² - σ².
    /// Just-identified (2 parameters, 2 moment conditions).
    /// </summary>
    private class TestGMMModel : IGMMModel
    {
        private readonly double[] _data;

        public TestGMMModel(double[] data)
        {
            _data = data;
            double sampleMean = data.Average();
            double sampleVar = data.Select(x => (x - sampleMean) * (x - sampleMean)).Average();

            Parameters = new List<ModelParameter>
            {
                new ModelParameter("Normal", "μ", sampleMean, -1000, 1000, new Uniform(-1000, 1000)),
                new ModelParameter("Normal", "σ²", Math.Max(sampleVar, 0.01), 0.01, 10000, new Uniform(0.01, 10000), isPositive: true)
            };
        }

        public List<ModelParameter> Parameters { get; }
        public int NumberOfParameters => 2;
        public int NumberOfMomentConditions => 2;
        public int SampleSize => _data.Length;

        public MomentConditionFunction MomentConditionFunction => (parameters) =>
        {
            double mu = parameters[0];
            double sigma2 = parameters[1];
            int n = _data.Length;

            double g1Sum = 0, g2Sum = 0;
            for (int i = 0; i < n; i++)
            {
                double diff = _data[i] - mu;
                g1Sum += diff;
                g2Sum += diff * diff - sigma2;
            }
            double g1 = g1Sum / n;
            double g2 = g2Sum / n;

            double s11 = 0, s12 = 0, s22 = 0;
            for (int i = 0; i < n; i++)
            {
                double diff = _data[i] - mu;
                double m1 = diff - g1;
                double m2 = (diff * diff - sigma2) - g2;
                s11 += m1 * m1;
                s12 += m1 * m2;
                s22 += m2 * m2;
            }
            s11 /= n; s12 /= n; s22 /= n;

            var G = new Vector(new[] { g1, g2 });
            var S = new Matrix(new double[,] { { s11, s12 }, { s12, s22 } });
            return (G, S);
        };

        public JacobianFunction? JacobianFunction => null;
        public PenaltyFunction? PenaltyFunction => null;

        public PointwiseMomentConditionFunction? PointwiseMomentConditions => (parameters) =>
        {
            double mu = parameters[0];
            double sigma2 = parameters[1];
            int n = _data.Length;
            var result = new double[n, 2];
            for (int i = 0; i < n; i++)
            {
                double diff = _data[i] - mu;
                result[i, 0] = diff;
                result[i, 1] = diff * diff - sigma2;
            }
            return result;
        };

#pragma warning disable CS0067
        public event PropertyChangedEventHandler? PropertyChanged;
#pragma warning restore CS0067

        /// <summary>
        /// Supports the <c>SetParameterValues</c> helper.
        /// </summary>
        /// <param name="parameters">The parameter values.</param>
        /// <remarks>
        /// This helper keeps fixture setup local to the tests that use it.
        /// </remarks>
        public void SetParameterValues(IList<double> parameters)
        {
            for (int i = 0; i < parameters.Count; i++)
                Parameters[i].Value = parameters[i];
        }

        /// <summary>
        /// Supports the <c>SetDefaultParameters</c> helper.
        /// </summary>
        /// <remarks>
        /// This helper keeps fixture setup local to the tests that use it.
        /// </remarks>
        public void SetDefaultParameters()
        {
            double mean = _data.Average();
            Parameters[0].Value = mean;
            Parameters[1].Value = _data.Select(x => (x - mean) * (x - mean)).Average();
        }

        /// <summary>
        /// Supports the <c>Clone</c> helper.
        /// </summary>
        /// <returns>The result.</returns>
        /// <remarks>
        /// This helper keeps fixture setup local to the tests that use it.
        /// </remarks>
        public IGMMModel Clone() => new TestGMMModel((double[])_data.Clone());

        /// <summary>
        /// Supports the <c>ToXElement</c> helper.
        /// </summary>
        /// <returns>The result.</returns>
        /// <remarks>
        /// This helper keeps fixture setup local to the tests that use it.
        /// </remarks>
        public XElement ToXElement() => new XElement("TestGMMModel");

        public (bool IsValid, List<string> ValidationMessages) Validate() => (true, new List<string>());
    }

    /// <summary>
    /// A variant of TestGMMModel that does NOT provide pointwise moment conditions.
    /// Used for testing influence diagnostics error handling.
    /// </summary>
    private class TestGMMModelNoPointwise : IGMMModel
    {
        private readonly TestGMMModel _inner;

        public TestGMMModelNoPointwise(double[] data)
        {
            _inner = new TestGMMModel(data);
        }

        public List<ModelParameter> Parameters => _inner.Parameters;
        public int NumberOfParameters => _inner.NumberOfParameters;
        public int NumberOfMomentConditions => _inner.NumberOfMomentConditions;
        public int SampleSize => _inner.SampleSize;
        public MomentConditionFunction MomentConditionFunction => _inner.MomentConditionFunction;
        public JacobianFunction? JacobianFunction => null;
        public PenaltyFunction? PenaltyFunction => null;
        public PointwiseMomentConditionFunction? PointwiseMomentConditions => null;

#pragma warning disable CS0067
        public event PropertyChangedEventHandler? PropertyChanged;
#pragma warning restore CS0067

        /// <summary>
        /// Supports the <c>SetParameterValues</c> helper.
        /// </summary>
        /// <remarks>
        /// This helper keeps fixture setup local to the tests that use it.
        /// </remarks>
        public void SetParameterValues(IList<double> parameters) => _inner.SetParameterValues(parameters);
        /// <summary>
        /// Supports the <c>SetDefaultParameters</c> helper.
        /// </summary>
        /// <remarks>
        /// This helper keeps fixture setup local to the tests that use it.
        /// </remarks>
        public void SetDefaultParameters() => _inner.SetDefaultParameters();
        /// <summary>
        /// Supports the <c>Clone</c> helper.
        /// </summary>
        /// <returns>The result.</returns>
        /// <remarks>
        /// This helper keeps fixture setup local to the tests that use it.
        /// </remarks>
        public IGMMModel Clone() => new TestGMMModelNoPointwise(new double[0]);
        /// <summary>
        /// Supports the <c>ToXElement</c> helper.
        /// </summary>
        /// <returns>The result.</returns>
        /// <remarks>
        /// This helper keeps fixture setup local to the tests that use it.
        /// </remarks>
        public XElement ToXElement() => new XElement("TestGMMModelNoPointwise");
        public (bool IsValid, List<string> ValidationMessages) Validate() => (true, new List<string>());
    }

    #endregion

    /// <summary>
    /// Verifies <c>Test_NelderMead_DirectlyWorks</c>.
    /// </summary>
    [TestMethod]
    public void Test_NelderMead_DirectlyWorks()
    {
        var data = GenerateNormalData(mean: 50, stddev: 5, n: 200);
        var model = new TestGMMModel(data);
        var gmm = new GeneralizedMethodOfMoments(model, OptimizationMethod.NelderMead);
        gmm.EstimationStrategy = GeneralizedMethodOfMoments.GMMEstimationStrategy.OneStep;

        bool result = gmm.Estimate();

        Assert.IsTrue(result);
        double sampleMean = data.Average();
        Assert.AreEqual(sampleMean, gmm.BestParameterSet.Values[0], 2.0);
    }

    /// <summary>
    /// Verifies <c>Test_ModelEstimate_OneStep</c>.
    /// </summary>
    [TestMethod]
    public void Test_ModelEstimate_OneStep()
    {
        var data = GenerateNormalData(mean: 50, stddev: 5, n: 200);
        var model = new TestGMMModel(data);
        var gmm = new GeneralizedMethodOfMoments(model);
        gmm.EstimationStrategy = GeneralizedMethodOfMoments.GMMEstimationStrategy.OneStep;

        bool result = gmm.Estimate();

        Assert.IsTrue(result);
        Assert.IsTrue(gmm.IsEstimated);
        Assert.IsNotNull(gmm.BestParameterSet);
    }

    /// <summary>
    /// Verifies <c>Test_ModelEstimate_TwoStep</c>.
    /// </summary>
    [TestMethod]
    public void Test_ModelEstimate_TwoStep()
    {
        var data = GenerateNormalData(mean: 50, stddev: 5, n: 200);
        var model = new TestGMMModel(data);
        var gmm = new GeneralizedMethodOfMoments(model);
        gmm.EstimationStrategy = GeneralizedMethodOfMoments.GMMEstimationStrategy.TwoStep;

        bool result = gmm.Estimate();

        Assert.IsTrue(result);
        Assert.IsTrue(gmm.IsEstimated);
        Assert.IsNotNull(gmm.BestParameterSet);
    }

    /// <summary>
    /// Verifies <c>Test_ModelEstimate_Iterative</c>.
    /// </summary>
    [TestMethod]
    public void Test_ModelEstimate_Iterative()
    {
        var data = GenerateNormalData(mean: 50, stddev: 5, n: 200);
        var model = new TestGMMModel(data);
        var gmm = new GeneralizedMethodOfMoments(model);
        gmm.EstimationStrategy = GeneralizedMethodOfMoments.GMMEstimationStrategy.Iterative;
        gmm.MaxGMMIterations = 50;

        bool result = gmm.Estimate();

        Assert.IsTrue(result);
        Assert.IsTrue(gmm.IsEstimated);
        Assert.IsNotNull(gmm.BestParameterSet);
    }
}
