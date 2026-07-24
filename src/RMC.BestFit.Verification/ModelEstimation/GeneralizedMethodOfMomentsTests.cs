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
/// Unit tests for the <see cref="GeneralizedMethodOfMoments"/> class.
/// Tests GMM estimation with various identification scenarios.
/// </summary>
/// <remarks>
/// Generalized Method of Moments (GMM) is a flexible estimation technique that
/// uses moment conditions to estimate model parameters. It is particularly useful
/// when the likelihood function is not available or difficult to compute.
/// </remarks>
[TestClass]
public class GeneralizedMethodOfMomentsTests
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

    #region Constructor Tests

    /// <summary>
    /// Verifies <c>Test_Constructor_ValidInputs_CreatesInstance</c>.
    /// </summary>
    [TestMethod]
    public void Test_Constructor_ValidInputs_CreatesInstance()
    {
        var (momentFunc, data) = CreateSimpleMeanMomentCondition();

        var gmm = new GeneralizedMethodOfMoments(
            momentConditionFunction: momentFunc,
            numberOfParameters: 1,
            numberOfMomentConditions: 1,
            sampleSize: data.Length,
            initialValues: new[] { 90.0 },
            lowerBounds: new[] { 0.0 },
            upperBounds: new[] { 200.0 });

        Assert.IsNotNull(gmm);
        Assert.AreEqual(1, gmm.NumberOfParameters);
        Assert.AreEqual(1, gmm.NumberOfMomentConditions);
    }

    /// <summary>
    /// Verifies <c>Test_Constructor_NullMomentFunction_ThrowsException</c>.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Test_Constructor_NullMomentFunction_ThrowsException()
    {
        new GeneralizedMethodOfMoments(
            momentConditionFunction: null!,
            numberOfParameters: 1,
            numberOfMomentConditions: 1,
            sampleSize: 100,
            initialValues: new[] { 0.0 },
            lowerBounds: new[] { -100.0 },
            upperBounds: new[] { 100.0 });
    }

    /// <summary>
    /// Verifies <c>Test_Constructor_MismatchedBounds_ThrowsException</c>.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void Test_Constructor_MismatchedBounds_ThrowsException()
    {
        var (momentFunc, _) = CreateSimpleMeanMomentCondition();

        new GeneralizedMethodOfMoments(
            momentConditionFunction: momentFunc,
            numberOfParameters: 2,
            numberOfMomentConditions: 1,
            sampleSize: 100,
            initialValues: new[] { 0.0 },  // Only 1 value, but 2 parameters
            lowerBounds: new[] { -100.0 },
            upperBounds: new[] { 100.0 });
    }

    /// <summary>
    /// Verifies <c>Test_Constructor_InitialOutsideBounds_ThrowsException</c>.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void Test_Constructor_InitialOutsideBounds_ThrowsException()
    {
        var (momentFunc, _) = CreateSimpleMeanMomentCondition();

        new GeneralizedMethodOfMoments(
            momentConditionFunction: momentFunc,
            numberOfParameters: 1,
            numberOfMomentConditions: 1,
            sampleSize: 100,
            initialValues: new[] { 150.0 },  // Outside bounds
            lowerBounds: new[] { 0.0 },
            upperBounds: new[] { 100.0 });
    }

    #endregion

    #region Identification Status Tests

    /// <summary>
    /// Verifies <c>Test_IdentificationStatus_JustIdentified</c>.
    /// </summary>
    [TestMethod]
    public void Test_IdentificationStatus_JustIdentified()
    {
        var (momentFunc, data) = CreateSimpleMeanMomentCondition();

        var gmm = new GeneralizedMethodOfMoments(
            momentConditionFunction: momentFunc,
            numberOfParameters: 1,
            numberOfMomentConditions: 1,
            sampleSize: data.Length,
            initialValues: new[] { 90.0 },
            lowerBounds: new[] { 0.0 },
            upperBounds: new[] { 200.0 });

        Assert.AreEqual(GeneralizedMethodOfMoments.GMMIdentificationStatus.JustIdentified, gmm.IdentificationStatus);
    }

    /// <summary>
    /// Verifies <c>Test_IdentificationStatus_OverIdentified</c>.
    /// </summary>
    [TestMethod]
    public void Test_IdentificationStatus_OverIdentified()
    {
        var (momentFunc, data) = CreateSimpleMeanMomentCondition();

        var gmm = new GeneralizedMethodOfMoments(
            momentConditionFunction: momentFunc,
            numberOfParameters: 1,
            numberOfMomentConditions: 3,  // More moments than parameters
            sampleSize: data.Length,
            initialValues: new[] { 90.0 },
            lowerBounds: new[] { 0.0 },
            upperBounds: new[] { 200.0 });

        Assert.AreEqual(GeneralizedMethodOfMoments.GMMIdentificationStatus.OverIdentified, gmm.IdentificationStatus);
    }

    /// <summary>
    /// Verifies <c>Test_IdentificationStatus_UnderIdentified</c>.
    /// </summary>
    [TestMethod]
    public void Test_IdentificationStatus_UnderIdentified()
    {
        var (momentFunc, data) = CreateSimpleMeanMomentCondition();

        var gmm = new GeneralizedMethodOfMoments(
            momentConditionFunction: momentFunc,
            numberOfParameters: 3,  // More parameters than moments
            numberOfMomentConditions: 1,
            sampleSize: data.Length,
            initialValues: new[] { 90.0, 10.0, 1.0 },
            lowerBounds: new[] { 0.0, 0.0, 0.0 },
            upperBounds: new[] { 200.0, 100.0, 10.0 });

        Assert.AreEqual(GeneralizedMethodOfMoments.GMMIdentificationStatus.UnderIdentified, gmm.IdentificationStatus);
    }

    #endregion

    #region Property Tests

    /// <summary>
    /// Verifies <c>Test_EstimationStrategy_SetAndGet</c>.
    /// </summary>
    [TestMethod]
    public void Test_EstimationStrategy_SetAndGet()
    {
        var (momentFunc, data) = CreateSimpleMeanMomentCondition();

        var gmm = new GeneralizedMethodOfMoments(
            momentConditionFunction: momentFunc,
            numberOfParameters: 1,
            numberOfMomentConditions: 1,
            sampleSize: data.Length,
            initialValues: new[] { 90.0 },
            lowerBounds: new[] { 0.0 },
            upperBounds: new[] { 200.0 });

        gmm.EstimationStrategy = GeneralizedMethodOfMoments.GMMEstimationStrategy.TwoStep;
        Assert.AreEqual(GeneralizedMethodOfMoments.GMMEstimationStrategy.TwoStep, gmm.EstimationStrategy);
    }

    /// <summary>
    /// Verifies <c>Test_OptimizerMethod_SetAndGet</c>.
    /// </summary>
    [TestMethod]
    public void Test_OptimizerMethod_SetAndGet()
    {
        var (momentFunc, data) = CreateSimpleMeanMomentCondition();

        var gmm = new GeneralizedMethodOfMoments(
            momentConditionFunction: momentFunc,
            numberOfParameters: 1,
            numberOfMomentConditions: 1,
            sampleSize: data.Length,
            initialValues: new[] { 90.0 },
            lowerBounds: new[] { 0.0 },
            upperBounds: new[] { 200.0 });

        gmm.OptimizerMethod = OptimizationMethod.Powell;
        Assert.AreEqual(OptimizationMethod.Powell, gmm.OptimizerMethod);
    }

    /// <summary>
    /// Verifies <c>Test_DegreeOfFreedom_OverIdentified</c>.
    /// </summary>
    [TestMethod]
    public void Test_DegreeOfFreedom_OverIdentified()
    {
        var (momentFunc, data) = CreateSimpleMeanMomentCondition();

        var gmm = new GeneralizedMethodOfMoments(
            momentConditionFunction: momentFunc,
            numberOfParameters: 1,
            numberOfMomentConditions: 4,
            sampleSize: data.Length,
            initialValues: new[] { 90.0 },
            lowerBounds: new[] { 0.0 },
            upperBounds: new[] { 200.0 });

        Assert.AreEqual(3, gmm.DegreeOfFreedom);  // 4 - 1 = 3
    }

    /// <summary>
    /// Verifies <c>Test_DegreeOfFreedom_JustIdentified</c>.
    /// </summary>
    [TestMethod]
    public void Test_DegreeOfFreedom_JustIdentified()
    {
        var (momentFunc, data) = CreateSimpleMeanMomentCondition();

        var gmm = new GeneralizedMethodOfMoments(
            momentConditionFunction: momentFunc,
            numberOfParameters: 1,
            numberOfMomentConditions: 1,
            sampleSize: data.Length,
            initialValues: new[] { 90.0 },
            lowerBounds: new[] { 0.0 },
            upperBounds: new[] { 200.0 });

        Assert.AreEqual(0, gmm.DegreeOfFreedom);
    }

    #endregion

    #region Estimation Tests

    /// <summary>
    /// Verifies <c>Test_Estimate_JustIdentified_FindsSolution</c>.
    /// </summary>
    [TestMethod]
    public void Test_Estimate_JustIdentified_FindsSolution()
    {
        var (momentFunc, data) = CreateSimpleMeanMomentCondition();
        double trueMean = data.Average();

        var gmm = new GeneralizedMethodOfMoments(
            momentConditionFunction: momentFunc,
            numberOfParameters: 1,
            numberOfMomentConditions: 1,
            sampleSize: data.Length,
            initialValues: new[] { 90.0 },
            lowerBounds: new[] { 0.0 },
            upperBounds: new[] { 200.0 });

        gmm.EstimationStrategy = GeneralizedMethodOfMoments.GMMEstimationStrategy.OneStep;
        gmm.Estimate();

        Assert.IsNotNull(gmm.BestParameterSet);
        Assert.AreEqual(trueMean, gmm.BestParameterSet.Values[0], 1.0);
    }

    /// <summary>
    /// Verifies <c>Test_Estimate_TwoStep_FindsSolution</c>.
    /// </summary>
    [TestMethod]
    public void Test_Estimate_TwoStep_FindsSolution()
    {
        var (momentFunc, data) = CreateSimpleMeanMomentCondition();
        double trueMean = data.Average();

        var gmm = new GeneralizedMethodOfMoments(
            momentConditionFunction: momentFunc,
            numberOfParameters: 1,
            numberOfMomentConditions: 1,
            sampleSize: data.Length,
            initialValues: new[] { 90.0 },
            lowerBounds: new[] { 0.0 },
            upperBounds: new[] { 200.0 });

        gmm.EstimationStrategy = GeneralizedMethodOfMoments.GMMEstimationStrategy.TwoStep;
        gmm.Estimate();

        Assert.IsNotNull(gmm.BestParameterSet);
        Assert.AreEqual(trueMean, gmm.BestParameterSet.Values[0], 1.0);
    }

    /// <summary>
    /// Verifies <c>Test_Estimate_Iterative_FindsSolution</c>.
    /// </summary>
    [TestMethod]
    public void Test_Estimate_Iterative_FindsSolution()
    {
        var (momentFunc, data) = CreateSimpleMeanMomentCondition();
        double trueMean = data.Average();

        var gmm = new GeneralizedMethodOfMoments(
            momentConditionFunction: momentFunc,
            numberOfParameters: 1,
            numberOfMomentConditions: 1,
            sampleSize: data.Length,
            initialValues: new[] { 90.0 },
            lowerBounds: new[] { 0.0 },
            upperBounds: new[] { 200.0 });

        gmm.EstimationStrategy = GeneralizedMethodOfMoments.GMMEstimationStrategy.Iterative;
        gmm.MaxGMMIterations = 50;
        gmm.Estimate();

        Assert.IsNotNull(gmm.BestParameterSet);
        Assert.AreEqual(trueMean, gmm.BestParameterSet.Values[0], 1.0);
    }

    /// <summary>
    /// Verifies <c>Test_Estimate_UnderIdentified_ThrowsException</c>.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(InvalidOperationException))]
    public void Test_Estimate_UnderIdentified_ThrowsException()
    {
        var (momentFunc, data) = CreateSimpleMeanMomentCondition();

        var gmm = new GeneralizedMethodOfMoments(
            momentConditionFunction: momentFunc,
            numberOfParameters: 3,
            numberOfMomentConditions: 1,
            sampleSize: data.Length,
            initialValues: new[] { 90.0, 10.0, 1.0 },
            lowerBounds: new[] { 0.0, 0.0, 0.0 },
            upperBounds: new[] { 200.0, 100.0, 10.0 });

        gmm.Estimate();
    }

    #endregion

    #region Validation Tests

    /// <summary>
    /// Verifies <c>Test_IsValid_ValidConfiguration_ReturnsTrue</c>.
    /// </summary>
    [TestMethod]
    public void Test_IsValid_ValidConfiguration_ReturnsTrue()
    {
        var (momentFunc, data) = CreateSimpleMeanMomentCondition();

        var gmm = new GeneralizedMethodOfMoments(
            momentConditionFunction: momentFunc,
            numberOfParameters: 1,
            numberOfMomentConditions: 1,
            sampleSize: data.Length,
            initialValues: new[] { 90.0 },
            lowerBounds: new[] { 0.0 },
            upperBounds: new[] { 200.0 });

        bool isValid = gmm.IsValid(out var errors);

        Assert.IsTrue(isValid);
        Assert.AreEqual(0, errors.Count);
    }

    /// <summary>
    /// Verifies <c>Test_IsValid_UnderIdentified_ReturnsFalse</c>.
    /// </summary>
    [TestMethod]
    public void Test_IsValid_UnderIdentified_ReturnsFalse()
    {
        var (momentFunc, data) = CreateSimpleMeanMomentCondition();

        var gmm = new GeneralizedMethodOfMoments(
            momentConditionFunction: momentFunc,
            numberOfParameters: 3,
            numberOfMomentConditions: 1,
            sampleSize: data.Length,
            initialValues: new[] { 90.0, 10.0, 1.0 },
            lowerBounds: new[] { 0.0, 0.0, 0.0 },
            upperBounds: new[] { 200.0, 100.0, 10.0 });

        bool isValid = gmm.IsValid(out var errors);

        Assert.IsFalse(isValid);
        Assert.IsTrue(errors.Any(e => e.Contains("under-identified")));
    }

    /// <summary>
    /// Verifies <c>Test_IsValid_InvalidTolerance_ReturnsFalse</c>.
    /// </summary>
    [TestMethod]
    public void Test_IsValid_InvalidTolerance_ReturnsFalse()
    {
        var (momentFunc, data) = CreateSimpleMeanMomentCondition();

        var gmm = new GeneralizedMethodOfMoments(
            momentConditionFunction: momentFunc,
            numberOfParameters: 1,
            numberOfMomentConditions: 1,
            sampleSize: data.Length,
            initialValues: new[] { 90.0 },
            lowerBounds: new[] { 0.0 },
            upperBounds: new[] { 200.0 });

        gmm.AbsoluteTolerance = 1e-20;  // Too small

        bool isValid = gmm.IsValid(out var errors);

        Assert.IsFalse(isValid);
        Assert.IsTrue(errors.Any(e => e.Contains("tolerance")));
    }

    #endregion

    #region Clear and Clone Tests

    /// <summary>
    /// Verifies <c>Test_ClearResults_ResetsState</c>.
    /// </summary>
    [TestMethod]
    public void Test_ClearResults_ResetsState()
    {
        var (momentFunc, data) = CreateSimpleMeanMomentCondition();

        var gmm = new GeneralizedMethodOfMoments(
            momentConditionFunction: momentFunc,
            numberOfParameters: 1,
            numberOfMomentConditions: 1,
            sampleSize: data.Length,
            initialValues: new[] { 90.0 },
            lowerBounds: new[] { 0.0 },
            upperBounds: new[] { 200.0 });

        gmm.EstimationStrategy = GeneralizedMethodOfMoments.GMMEstimationStrategy.OneStep;
        gmm.Estimate();
        gmm.ClearResults();

        Assert.AreEqual(0, gmm.GMMIterations);
        Assert.AreEqual(0, gmm.TotalFunctionEvaluations);
        Assert.IsTrue(double.IsNaN(gmm.JStat));
    }

    /// <summary>
    /// Verifies <c>Test_Clone_CreatesIndependentCopy</c>.
    /// </summary>
    [TestMethod]
    public void Test_Clone_CreatesIndependentCopy()
    {
        var (momentFunc, data) = CreateSimpleMeanMomentCondition();

        var original = new GeneralizedMethodOfMoments(
            momentConditionFunction: momentFunc,
            numberOfParameters: 1,
            numberOfMomentConditions: 1,
            sampleSize: data.Length,
            initialValues: new[] { 90.0 },
            lowerBounds: new[] { 0.0 },
            upperBounds: new[] { 200.0 });

        var clone = original.Clone();

        Assert.AreEqual(original.NumberOfParameters, clone.NumberOfParameters);
        Assert.AreEqual(original.SampleSize, clone.SampleSize);
    }

    #endregion

    #region Serialization Tests

    /// <summary>
    /// Verifies <c>Test_ToXElement_ContainsRequiredAttributes</c>.
    /// </summary>
    [TestMethod]
    public void Test_ToXElement_ContainsRequiredAttributes()
    {
        var (momentFunc, data) = CreateSimpleMeanMomentCondition();

        var gmm = new GeneralizedMethodOfMoments(
            momentConditionFunction: momentFunc,
            numberOfParameters: 1,
            numberOfMomentConditions: 1,
            sampleSize: data.Length,
            initialValues: new[] { 90.0 },
            lowerBounds: new[] { 0.0 },
            upperBounds: new[] { 200.0 });

        gmm.EstimationStrategy = GeneralizedMethodOfMoments.GMMEstimationStrategy.OneStep;
        gmm.Estimate();

        var xElement = gmm.ToXElement();

        Assert.AreEqual("GeneralizedMethodOfMoments", xElement.Name.LocalName);
        Assert.IsNotNull(xElement.Attribute("EstimationStrategy"));
        Assert.IsNotNull(xElement.Attribute("SampleSize"));
    }

    #endregion

    #region PropertyChanged Tests

    /// <summary>
    /// Verifies <c>Test_PropertyChanged_MaxGMMIterations</c>.
    /// </summary>
    [TestMethod]
    public void Test_PropertyChanged_MaxGMMIterations()
    {
        var (momentFunc, data) = CreateSimpleMeanMomentCondition();

        var gmm = new GeneralizedMethodOfMoments(
            momentConditionFunction: momentFunc,
            numberOfParameters: 1,
            numberOfMomentConditions: 1,
            sampleSize: data.Length,
            initialValues: new[] { 90.0 },
            lowerBounds: new[] { 0.0 },
            upperBounds: new[] { 200.0 });

        string? changedProperty = null;
        gmm.PropertyChanged += (s, e) => changedProperty = e.PropertyName;

        gmm.MaxGMMIterations = 200;

        Assert.AreEqual(nameof(gmm.MaxGMMIterations), changedProperty);
    }

    #endregion

    #region Model Constructor Tests

    /// <summary>
    /// Verifies <c>Test_ModelConstructor_CreatesInstance</c>.
    /// </summary>
    [TestMethod]
    public void Test_ModelConstructor_CreatesInstance()
    {
        var data = GenerateNormalData();
        var model = new TestGMMModel(data);

        var gmm = new GeneralizedMethodOfMoments(model);

        Assert.IsNotNull(gmm);
        Assert.AreSame(model, gmm.Model);
        Assert.AreEqual(2, gmm.NumberOfParameters);
        Assert.AreEqual(2, gmm.NumberOfMomentConditions);
        Assert.AreEqual(data.Length, gmm.SampleSize);
        Assert.AreEqual(GeneralizedMethodOfMoments.GMMIdentificationStatus.JustIdentified, gmm.IdentificationStatus);
    }

    /// <summary>
    /// Verifies <c>Test_ModelConstructor_ReadsParameterBounds</c>.
    /// </summary>
    [TestMethod]
    public void Test_ModelConstructor_ReadsParameterBounds()
    {
        var data = GenerateNormalData();
        var model = new TestGMMModel(data);

        var gmm = new GeneralizedMethodOfMoments(model);

        // Verify initial values come from model parameters
        Assert.AreEqual(model.Parameters[0].Value, gmm.InitialValues[0], 1e-10);
        Assert.AreEqual(model.Parameters[1].Value, gmm.InitialValues[1], 1e-10);

        // Verify bounds come from model parameters
        Assert.AreEqual(model.Parameters[0].LowerBound, gmm.LowerBounds[0]);
        Assert.AreEqual(model.Parameters[1].LowerBound, gmm.LowerBounds[1]);
        Assert.AreEqual(model.Parameters[0].UpperBound, gmm.UpperBounds[0]);
        Assert.AreEqual(model.Parameters[1].UpperBound, gmm.UpperBounds[1]);
    }

    /// <summary>
    /// Verifies <c>Test_ModelConstructor_NullModel_Throws</c>.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Test_ModelConstructor_NullModel_Throws()
    {
        new GeneralizedMethodOfMoments((IGMMModel)null!);
    }

    /// <summary>
    /// Verifies <c>Test_ModelConstructor_DefaultsToNonNullOptionalDelegates</c>.
    /// </summary>
    [TestMethod]
    public void Test_ModelConstructor_DefaultsToNonNullOptionalDelegates()
    {
        var data = GenerateNormalData();
        var model = new TestGMMModel(data);

        var gmm = new GeneralizedMethodOfMoments(model);

        // JacobianFunction and PenaltyFunction should be null (model returns null)
        Assert.IsNull(gmm.JacobianFunction);
        Assert.IsNull(gmm.PenaltyFunction);
        // PointwiseMomentConditions should be set (model provides it)
        Assert.IsNotNull(gmm.PointwiseMomentConditions);
    }

    /// <summary>
    /// Verifies <c>Test_DelegateConstructor_ModelIsNull</c>.
    /// </summary>
    [TestMethod]
    public void Test_DelegateConstructor_ModelIsNull()
    {
        var (momentFunc, data) = CreateSimpleMeanMomentCondition();

        var gmm = new GeneralizedMethodOfMoments(
            momentConditionFunction: momentFunc,
            numberOfParameters: 1,
            numberOfMomentConditions: 1,
            sampleSize: data.Length,
            initialValues: new[] { 90.0 },
            lowerBounds: new[] { 0.0 },
            upperBounds: new[] { 200.0 });

        Assert.IsNull(gmm.Model);
    }

    #endregion

    #region IsEstimated and Estimate Return Value Tests

    /// <summary>
    /// Verifies <c>Test_IsEstimated_FalseBeforeEstimation</c>.
    /// </summary>
    [TestMethod]
    public void Test_IsEstimated_FalseBeforeEstimation()
    {
        var data = GenerateNormalData();
        var model = new TestGMMModel(data);
        var gmm = new GeneralizedMethodOfMoments(model);

        Assert.IsFalse(gmm.IsEstimated);
    }

    /// <summary>
    /// Verifies <c>Test_IsEstimated_TrueAfterEstimation</c>.
    /// </summary>
    [TestMethod]
    public void Test_IsEstimated_TrueAfterEstimation()
    {
        var data = GenerateNormalData();
        var model = new TestGMMModel(data);
        var gmm = new GeneralizedMethodOfMoments(model);
        gmm.EstimationStrategy = GeneralizedMethodOfMoments.GMMEstimationStrategy.OneStep;

        gmm.Estimate();

        Assert.IsTrue(gmm.IsEstimated);
    }

    /// <summary>
    /// Verifies <c>Test_IsEstimated_FalseAfterClearResults</c>.
    /// </summary>
    [TestMethod]
    public void Test_IsEstimated_FalseAfterClearResults()
    {
        var data = GenerateNormalData();
        var model = new TestGMMModel(data);
        var gmm = new GeneralizedMethodOfMoments(model);
        gmm.EstimationStrategy = GeneralizedMethodOfMoments.GMMEstimationStrategy.OneStep;

        gmm.Estimate();
        Assert.IsTrue(gmm.IsEstimated);

        gmm.ClearResults();
        Assert.IsFalse(gmm.IsEstimated);
    }

    /// <summary>
    /// Verifies <c>Test_Estimate_ReturnsTrue_OnSuccess</c>.
    /// </summary>
    [TestMethod]
    public void Test_Estimate_ReturnsTrue_OnSuccess()
    {
        var data = GenerateNormalData();
        var model = new TestGMMModel(data);
        var gmm = new GeneralizedMethodOfMoments(model);
        gmm.EstimationStrategy = GeneralizedMethodOfMoments.GMMEstimationStrategy.OneStep;

        bool result = gmm.Estimate();

        Assert.IsTrue(result);
        Assert.IsTrue(gmm.IsEstimated);
    }

    #endregion

    #region BFGS and Fallback Tests

    /// <summary>
    /// Verifies <c>Test_DefaultOptimizer_IsBFGS</c>.
    /// </summary>
    [TestMethod]
    public void Test_DefaultOptimizer_IsBFGS()
    {
        var data = GenerateNormalData();
        var model = new TestGMMModel(data);
        var gmm = new GeneralizedMethodOfMoments(model);

        Assert.AreEqual(OptimizationMethod.BFGS, gmm.OptimizerMethod);
    }

    /// <summary>
    /// Verifies <c>Test_DelegateConstructor_DefaultOptimizer_IsBFGS</c>.
    /// </summary>
    [TestMethod]
    public void Test_DelegateConstructor_DefaultOptimizer_IsBFGS()
    {
        var (momentFunc, data) = CreateSimpleMeanMomentCondition();

        var gmm = new GeneralizedMethodOfMoments(
            momentConditionFunction: momentFunc,
            numberOfParameters: 1,
            numberOfMomentConditions: 1,
            sampleSize: data.Length,
            initialValues: new[] { 90.0 },
            lowerBounds: new[] { 0.0 },
            upperBounds: new[] { 200.0 });

        Assert.AreEqual(OptimizationMethod.BFGS, gmm.OptimizerMethod);
    }

    /// <summary>
    /// Verifies <c>Test_UseFallbackOptimizer_DefaultsTrue</c>.
    /// </summary>
    [TestMethod]
    public void Test_UseFallbackOptimizer_DefaultsTrue()
    {
        var data = GenerateNormalData();
        var model = new TestGMMModel(data);
        var gmm = new GeneralizedMethodOfMoments(model);

        Assert.IsTrue(gmm.UseFallbackOptimizer);
    }

    /// <summary>
    /// Verifies <c>Test_BFGS_FindsSolution_ModelConstructor</c>.
    /// </summary>
    [TestMethod]
    public void Test_BFGS_FindsSolution_ModelConstructor()
    {
        var data = GenerateNormalData(mean: 50, stddev: 5, n: 200);
        var model = new TestGMMModel(data);
        var gmm = new GeneralizedMethodOfMoments(model);
        gmm.EstimationStrategy = GeneralizedMethodOfMoments.GMMEstimationStrategy.OneStep;

        bool result = gmm.Estimate();

        Assert.IsTrue(result);
        Assert.IsTrue(gmm.IsEstimated);

        // Should find approximately the sample mean and variance
        double sampleMean = data.Average();
        double sampleVar = data.Select(x => (x - sampleMean) * (x - sampleMean)).Average();
        Assert.AreEqual(sampleMean, gmm.BestParameterSet.Values[0], 1.0);
        Assert.AreEqual(sampleVar, gmm.BestParameterSet.Values[1], 5.0);
    }

    /// <summary>
    /// Verifies <c>Test_FallbackDisabled_PropertyWorks</c>.
    /// </summary>
    [TestMethod]
    public void Test_FallbackDisabled_PropertyWorks()
    {
        var data = GenerateNormalData();
        var model = new TestGMMModel(data);
        var gmm = new GeneralizedMethodOfMoments(model);

        gmm.UseFallbackOptimizer = false;
        Assert.IsFalse(gmm.UseFallbackOptimizer);

        gmm.UseFallbackOptimizer = true;
        Assert.IsTrue(gmm.UseFallbackOptimizer);
    }

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

    #endregion

    #region Estimation Strategy Tests (Model Constructor)

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

    #endregion

    #region ObjectiveFunctionValue Tests

    /// <summary>
    /// Verifies <c>Test_ObjectiveFunctionValue_NaN_BeforeEstimation</c>.
    /// </summary>
    [TestMethod]
    public void Test_ObjectiveFunctionValue_NaN_BeforeEstimation()
    {
        var data = GenerateNormalData();
        var model = new TestGMMModel(data);
        var gmm = new GeneralizedMethodOfMoments(model);

        Assert.IsTrue(double.IsNaN(gmm.ObjectiveFunctionValue));
    }

    /// <summary>
    /// Verifies <c>Test_ObjectiveFunctionValue_Finite_AfterEstimation</c>.
    /// </summary>
    [TestMethod]
    public void Test_ObjectiveFunctionValue_Finite_AfterEstimation()
    {
        var data = GenerateNormalData();
        var model = new TestGMMModel(data);
        var gmm = new GeneralizedMethodOfMoments(model);
        gmm.EstimationStrategy = GeneralizedMethodOfMoments.GMMEstimationStrategy.OneStep;

        gmm.Estimate();

        Assert.IsFalse(double.IsNaN(gmm.ObjectiveFunctionValue));
        Assert.IsTrue(double.IsFinite(gmm.ObjectiveFunctionValue));
        Assert.IsTrue(gmm.ObjectiveFunctionValue >= 0, "Objective function Q(θ) should be non-negative.");
    }

    #endregion

    #region ConvergenceHistory Tests

    /// <summary>
    /// Verifies <c>Test_ConvergenceHistory_Iterative_NonEmpty</c>.
    /// </summary>
    [TestMethod]
    public void Test_ConvergenceHistory_Iterative_NonEmpty()
    {
        var data = GenerateNormalData(mean: 50, stddev: 5, n: 200);
        var model = new TestGMMModel(data);
        var gmm = new GeneralizedMethodOfMoments(model);
        gmm.EstimationStrategy = GeneralizedMethodOfMoments.GMMEstimationStrategy.Iterative;
        gmm.MaxGMMIterations = 50;

        gmm.Estimate();

        Assert.IsTrue(gmm.ConvergenceHistory.Count > 0, "Iterative estimation should populate ConvergenceHistory.");
        // All Q values should be non-negative
        foreach (double q in gmm.ConvergenceHistory)
        {
            Assert.IsTrue(q >= 0, $"Objective function value Q = {q} should be non-negative.");
        }
    }

    /// <summary>
    /// Verifies <c>Test_ConvergenceHistory_ClearedOnClearResults</c>.
    /// </summary>
    [TestMethod]
    public void Test_ConvergenceHistory_ClearedOnClearResults()
    {
        var data = GenerateNormalData();
        var model = new TestGMMModel(data);
        var gmm = new GeneralizedMethodOfMoments(model);
        gmm.EstimationStrategy = GeneralizedMethodOfMoments.GMMEstimationStrategy.Iterative;
        gmm.MaxGMMIterations = 50;

        gmm.Estimate();
        Assert.IsTrue(gmm.ConvergenceHistory.Count > 0);

        gmm.ClearResults();
        Assert.AreEqual(0, gmm.ConvergenceHistory.Count);
    }

    #endregion

    #region Diagnostic Method Tests

    /// <summary>
    /// Verifies <c>Test_GetStandardErrors_PositiveValues</c>.
    /// </summary>
    [TestMethod]
    public void Test_GetStandardErrors_PositiveValues()
    {
        var data = GenerateNormalData(mean: 50, stddev: 5, n: 200);
        var model = new TestGMMModel(data);
        var gmm = new GeneralizedMethodOfMoments(model);
        gmm.EstimationStrategy = GeneralizedMethodOfMoments.GMMEstimationStrategy.TwoStep;
        gmm.Estimate();

        var se = gmm.GetStandardErrors();

        Assert.AreEqual(2, se.Length);
        for (int i = 0; i < se.Length; i++)
        {
            Assert.IsTrue(se[i] > 0, $"Standard error for parameter {i} should be positive, got {se[i]}.");
            Assert.IsTrue(double.IsFinite(se[i]), $"Standard error for parameter {i} should be finite.");
        }
    }

    /// <summary>
    /// Verifies <c>Test_GetStandardErrors_ThrowsWhenNotEstimated</c>.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(InvalidOperationException))]
    public void Test_GetStandardErrors_ThrowsWhenNotEstimated()
    {
        var data = GenerateNormalData();
        var model = new TestGMMModel(data);
        var gmm = new GeneralizedMethodOfMoments(model);

        gmm.GetStandardErrors();
    }

    /// <summary>
    /// Verifies <c>Test_GetCovarianceMatrix_SymmetricPositiveDefinite</c>.
    /// </summary>
    [TestMethod]
    public void Test_GetCovarianceMatrix_SymmetricPositiveDefinite()
    {
        var data = GenerateNormalData(mean: 50, stddev: 5, n: 200);
        var model = new TestGMMModel(data);
        var gmm = new GeneralizedMethodOfMoments(model);
        gmm.EstimationStrategy = GeneralizedMethodOfMoments.GMMEstimationStrategy.TwoStep;
        gmm.Estimate();

        var cov = gmm.GetCovarianceMatrix();

        Assert.AreEqual(2, cov.NumberOfRows);
        Assert.AreEqual(2, cov.NumberOfColumns);

        // Symmetry
        Assert.AreEqual(cov[0, 1], cov[1, 0], 1e-10, "Covariance matrix should be symmetric.");

        // Positive diagonal (positive definite requires positive eigenvalues, but positive diagonal is a necessary condition)
        Assert.IsTrue(cov[0, 0] > 0, "Diagonal elements should be positive.");
        Assert.IsTrue(cov[1, 1] > 0, "Diagonal elements should be positive.");

        // Positive definite check via det > 0 for 2x2
        double det = cov[0, 0] * cov[1, 1] - cov[0, 1] * cov[1, 0];
        Assert.IsTrue(det > 0, $"Covariance matrix determinant = {det} should be positive (positive definite).");
    }

    /// <summary>
    /// Verifies <c>Test_GetCovarianceMatrix_ThrowsWhenNotEstimated</c>.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(InvalidOperationException))]
    public void Test_GetCovarianceMatrix_ThrowsWhenNotEstimated()
    {
        var data = GenerateNormalData();
        var model = new TestGMMModel(data);
        var gmm = new GeneralizedMethodOfMoments(model);

        gmm.GetCovarianceMatrix();
    }

    /// <summary>
    /// Verifies <c>Test_GetCorrelationMatrix_DiagonalOnesOffDiagonalBounded</c>.
    /// </summary>
    [TestMethod]
    public void Test_GetCorrelationMatrix_DiagonalOnesOffDiagonalBounded()
    {
        var data = GenerateNormalData(mean: 50, stddev: 5, n: 200);
        var model = new TestGMMModel(data);
        var gmm = new GeneralizedMethodOfMoments(model);
        gmm.EstimationStrategy = GeneralizedMethodOfMoments.GMMEstimationStrategy.TwoStep;
        gmm.Estimate();

        var corr = gmm.GetCorrelationMatrix();

        Assert.AreEqual(2, corr.NumberOfRows);
        Assert.AreEqual(2, corr.NumberOfColumns);

        // Diagonal should be 1.0
        Assert.AreEqual(1.0, corr[0, 0], 1e-10, "Diagonal of correlation matrix should be 1.0.");
        Assert.AreEqual(1.0, corr[1, 1], 1e-10, "Diagonal of correlation matrix should be 1.0.");

        // Off-diagonal should be in [-1, 1]
        Assert.IsTrue(corr[0, 1] >= -1.0 && corr[0, 1] <= 1.0,
            $"Off-diagonal correlation = {corr[0, 1]} should be in [-1, 1].");
        Assert.IsTrue(corr[1, 0] >= -1.0 && corr[1, 0] <= 1.0,
            $"Off-diagonal correlation = {corr[1, 0]} should be in [-1, 1].");

        // Symmetry
        Assert.AreEqual(corr[0, 1], corr[1, 0], 1e-10, "Correlation matrix should be symmetric.");
    }

    /// <summary>
    /// Verifies <c>Test_GetRobustStandardErrors_PositiveValues</c>.
    /// </summary>
    [TestMethod]
    public void Test_GetRobustStandardErrors_PositiveValues()
    {
        var data = GenerateNormalData(mean: 50, stddev: 5, n: 200);
        var model = new TestGMMModel(data);
        var gmm = new GeneralizedMethodOfMoments(model);
        gmm.EstimationStrategy = GeneralizedMethodOfMoments.GMMEstimationStrategy.TwoStep;
        gmm.Estimate();

        var robustSE = gmm.GetRobustStandardErrors();

        Assert.AreEqual(2, robustSE.Length);
        for (int i = 0; i < robustSE.Length; i++)
        {
            Assert.IsTrue(robustSE[i] > 0, $"Robust SE for parameter {i} should be positive, got {robustSE[i]}.");
            Assert.IsTrue(double.IsFinite(robustSE[i]), $"Robust SE for parameter {i} should be finite.");
        }
    }

    /// <summary>
    /// Verifies <c>Test_GetSandwichCovarianceMatrix_Symmetric</c>.
    /// </summary>
    [TestMethod]
    public void Test_GetSandwichCovarianceMatrix_Symmetric()
    {
        var data = GenerateNormalData(mean: 50, stddev: 5, n: 200);
        var model = new TestGMMModel(data);
        var gmm = new GeneralizedMethodOfMoments(model);
        gmm.EstimationStrategy = GeneralizedMethodOfMoments.GMMEstimationStrategy.TwoStep;
        gmm.Estimate();

        var sandwich = gmm.GetSandwichCovarianceMatrix();

        Assert.AreEqual(2, sandwich.NumberOfRows);
        Assert.AreEqual(2, sandwich.NumberOfColumns);
        Assert.AreEqual(sandwich[0, 1], sandwich[1, 0], 1e-10, "Sandwich covariance should be symmetric.");
    }

    /// <summary>
    /// Verifies <c>Test_StandardErrors_MatchCovarianceDiagonal</c>.
    /// </summary>
    [TestMethod]
    public void Test_StandardErrors_MatchCovarianceDiagonal()
    {
        var data = GenerateNormalData(mean: 50, stddev: 5, n: 200);
        var model = new TestGMMModel(data);
        var gmm = new GeneralizedMethodOfMoments(model);
        gmm.EstimationStrategy = GeneralizedMethodOfMoments.GMMEstimationStrategy.TwoStep;
        gmm.Estimate();

        var cov = gmm.GetCovarianceMatrix();
        var se = gmm.GetStandardErrors();

        for (int i = 0; i < 2; i++)
        {
            double expected = Math.Sqrt(cov[i, i]);
            Assert.AreEqual(expected, se[i], 1e-10, $"SE[{i}] should equal sqrt(Cov[{i},{i}]).");
        }
    }

    #endregion

    #region Influence Diagnostics Tests

    /// <summary>
    /// Verifies <c>Test_ObservationInfluence_Dimensions</c>.
    /// </summary>
    [TestMethod]
    public void Test_ObservationInfluence_Dimensions()
    {
        var data = GenerateNormalData(mean: 50, stddev: 5, n: 100);
        var model = new TestGMMModel(data);
        var gmm = new GeneralizedMethodOfMoments(model);
        gmm.EstimationStrategy = GeneralizedMethodOfMoments.GMMEstimationStrategy.TwoStep;
        gmm.Estimate();

        var influence = gmm.GetObservationInfluence();

        Assert.AreEqual(data.Length, influence.GetLength(0), "Influence should have n rows.");
        Assert.AreEqual(2, influence.GetLength(1), "Influence should have p columns.");

        // All values should be finite
        for (int i = 0; i < influence.GetLength(0); i++)
        {
            for (int j = 0; j < influence.GetLength(1); j++)
            {
                Assert.IsTrue(double.IsFinite(influence[i, j]),
                    $"Influence[{i},{j}] = {influence[i, j]} should be finite.");
            }
        }
    }

    /// <summary>
    /// Verifies <c>Test_CooksDistance_Length</c>.
    /// </summary>
    [TestMethod]
    public void Test_CooksDistance_Length()
    {
        var data = GenerateNormalData(mean: 50, stddev: 5, n: 100);
        var model = new TestGMMModel(data);
        var gmm = new GeneralizedMethodOfMoments(model);
        gmm.EstimationStrategy = GeneralizedMethodOfMoments.GMMEstimationStrategy.TwoStep;
        gmm.Estimate();

        var cooksD = gmm.GetCooksDistance();

        Assert.AreEqual(data.Length, cooksD.Length, "Cook's distance should have n entries.");

        // All Cook's distances should be non-negative and finite
        for (int i = 0; i < cooksD.Length; i++)
        {
            Assert.IsTrue(cooksD[i] >= 0, $"Cook's D[{i}] = {cooksD[i]} should be non-negative.");
            Assert.IsTrue(double.IsFinite(cooksD[i]), $"Cook's D[{i}] = {cooksD[i]} should be finite.");
        }
    }

    /// <summary>
    /// Verifies <c>Test_GetInfluenceDiagnostics_ReturnsValidObject</c>.
    /// </summary>
    [TestMethod]
    public void Test_GetInfluenceDiagnostics_ReturnsValidObject()
    {
        var data = GenerateNormalData(mean: 50, stddev: 5, n: 100);
        var model = new TestGMMModel(data);
        var gmm = new GeneralizedMethodOfMoments(model);
        gmm.EstimationStrategy = GeneralizedMethodOfMoments.GMMEstimationStrategy.TwoStep;
        gmm.Estimate();

        var diag = gmm.GetInfluenceDiagnostics();

        Assert.IsNotNull(diag);
        Assert.AreEqual(data.Length, diag.Count);
        Assert.IsNotNull(diag.Observations);
        Assert.AreEqual(data.Length, diag.Observations.Length);

        // ParetoK field is used to store Cook's D, which should be non-negative
        for (int i = 0; i < diag.Count; i++)
        {
            Assert.IsTrue(diag.Observations[i].ParetoK >= 0,
                $"Observation {i} Cook's D (stored as ParetoK) = {diag.Observations[i].ParetoK} should be non-negative.");
            Assert.IsTrue(double.IsNaN(diag.Observations[i].ElpdLoo),
                "ElpdLoo should be NaN for GMM diagnostics.");
        }
    }

    /// <summary>
    /// Verifies <c>Test_ObservationInfluence_ThrowsWhenNotEstimated</c>.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(InvalidOperationException))]
    public void Test_ObservationInfluence_ThrowsWhenNotEstimated()
    {
        var data = GenerateNormalData();
        var model = new TestGMMModel(data);
        var gmm = new GeneralizedMethodOfMoments(model);

        gmm.GetObservationInfluence();
    }

    /// <summary>
    /// Verifies <c>Test_CooksDistance_ThrowsWhenNotEstimated</c>.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(InvalidOperationException))]
    public void Test_CooksDistance_ThrowsWhenNotEstimated()
    {
        var data = GenerateNormalData();
        var model = new TestGMMModel(data);
        var gmm = new GeneralizedMethodOfMoments(model);

        gmm.GetCooksDistance();
    }

    /// <summary>
    /// Verifies <c>Test_ObservationInfluence_ThrowsWhenNoPointwise</c>.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(InvalidOperationException))]
    public void Test_ObservationInfluence_ThrowsWhenNoPointwise()
    {
        var data = GenerateNormalData();
        var model = new TestGMMModelNoPointwise(data);
        var gmm = new GeneralizedMethodOfMoments(model);
        gmm.EstimationStrategy = GeneralizedMethodOfMoments.GMMEstimationStrategy.OneStep;
        gmm.Estimate();

        // Should throw because PointwiseMomentConditions is null
        gmm.GetObservationInfluence();
    }

    /// <summary>
    /// Verifies <c>Test_CooksDistance_ThrowsWhenNoPointwise</c>.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(InvalidOperationException))]
    public void Test_CooksDistance_ThrowsWhenNoPointwise()
    {
        var data = GenerateNormalData();
        var model = new TestGMMModelNoPointwise(data);
        var gmm = new GeneralizedMethodOfMoments(model);
        gmm.EstimationStrategy = GeneralizedMethodOfMoments.GMMEstimationStrategy.OneStep;
        gmm.Estimate();

        gmm.GetCooksDistance();
    }

    /// <summary>
    /// Verifies <c>Test_InfluenceDiagnostics_DelegateConstructor_WithPointwise</c>.
    /// </summary>
    [TestMethod]
    public void Test_InfluenceDiagnostics_DelegateConstructor_WithPointwise()
    {
        var data = GenerateNormalData(mean: 50, stddev: 5, n: 100);
        var (momentFunc, pointwiseFunc) = CreateNormalMomentConditions(data);

        double sampleMean = data.Average();
        double sampleVar = data.Select(x => (x - sampleMean) * (x - sampleMean)).Average();

        var gmm = new GeneralizedMethodOfMoments(
            momentConditionFunction: momentFunc,
            numberOfParameters: 2,
            numberOfMomentConditions: 2,
            sampleSize: data.Length,
            initialValues: new[] { sampleMean, sampleVar },
            lowerBounds: new[] { -1000.0, 0.01 },
            upperBounds: new[] { 1000.0, 10000.0 },
            pointwiseMomentConditions: pointwiseFunc);

        gmm.EstimationStrategy = GeneralizedMethodOfMoments.GMMEstimationStrategy.TwoStep;
        gmm.Estimate();

        var cooksD = gmm.GetCooksDistance();

        Assert.AreEqual(data.Length, cooksD.Length);
        for (int i = 0; i < cooksD.Length; i++)
            Assert.IsTrue(cooksD[i] >= 0 && double.IsFinite(cooksD[i]));
    }

    #endregion

    #region PostProcess and J-Statistic Tests

    /// <summary>
    /// Verifies <c>Test_PostProcess_PopulatesSigma</c>.
    /// </summary>
    [TestMethod]
    public void Test_PostProcess_PopulatesSigma()
    {
        var data = GenerateNormalData(mean: 50, stddev: 5, n: 200);
        var model = new TestGMMModel(data);
        var gmm = new GeneralizedMethodOfMoments(model);
        gmm.EstimationStrategy = GeneralizedMethodOfMoments.GMMEstimationStrategy.TwoStep;
        gmm.Estimate();

        // Sigma is null before PostProcess
        Assert.IsNull(gmm.Sigma);

        gmm.PostProcess(useSandwich: true, computeJstat: false);

        Assert.IsNotNull(gmm.Sigma);
        Assert.AreEqual(2, gmm.Sigma.NumberOfRows);
        Assert.AreEqual(2, gmm.Sigma.NumberOfColumns);
    }

    #endregion

    #region SetDefaultOptions Tests

    /// <summary>
    /// Verifies <c>Test_SetDefaultOptions_RestoresDefaults</c>.
    /// </summary>
    [TestMethod]
    public void Test_SetDefaultOptions_RestoresDefaults()
    {
        var data = GenerateNormalData();
        var model = new TestGMMModel(data);
        var gmm = new GeneralizedMethodOfMoments(model);

        // Change everything
        gmm.OptimizerMethod = OptimizationMethod.NelderMead;
        gmm.UseFallbackOptimizer = false;
        gmm.EstimationStrategy = GeneralizedMethodOfMoments.GMMEstimationStrategy.OneStep;
        gmm.MaxGMMIterations = 5;
        gmm.MaxFunctionEvaluations = 10;
        gmm.AbsoluteTolerance = 0.5;
        gmm.RelativeTolerance = 0.5;

        gmm.SetDefaultOptions();

        Assert.AreEqual(OptimizationMethod.BFGS, gmm.OptimizerMethod);
        Assert.IsTrue(gmm.UseFallbackOptimizer);
        Assert.AreEqual(GeneralizedMethodOfMoments.GMMEstimationStrategy.Iterative, gmm.EstimationStrategy);
        Assert.AreEqual(100, gmm.MaxGMMIterations);
        Assert.AreEqual(2000, gmm.MaxFunctionEvaluations);
        Assert.AreEqual(1E-8, gmm.AbsoluteTolerance);
        Assert.AreEqual(1E-8, gmm.RelativeTolerance);
    }

    #endregion

    #region ClearResults Extended Tests

    /// <summary>
    /// Verifies <c>Test_ClearResults_ResetsAllNewProperties</c>.
    /// </summary>
    [TestMethod]
    public void Test_ClearResults_ResetsAllNewProperties()
    {
        var data = GenerateNormalData();
        var model = new TestGMMModel(data);
        var gmm = new GeneralizedMethodOfMoments(model);
        gmm.EstimationStrategy = GeneralizedMethodOfMoments.GMMEstimationStrategy.Iterative;
        gmm.MaxGMMIterations = 50;

        gmm.Estimate();
        Assert.IsTrue(gmm.IsEstimated);
        Assert.IsTrue(gmm.ConvergenceHistory.Count > 0);

        gmm.ClearResults();

        Assert.IsFalse(gmm.IsEstimated);
        Assert.AreEqual(0, gmm.ConvergenceHistory.Count);
        Assert.AreEqual(0, gmm.GMMIterations);
        Assert.AreEqual(0, gmm.TotalFunctionEvaluations);
        Assert.IsTrue(double.IsNaN(gmm.JStat));
        Assert.IsTrue(double.IsNaN(gmm.JStatPval));
        Assert.IsNull(gmm.W);
        Assert.IsNull(gmm.S);
        Assert.IsNull(gmm.Sigma);
        Assert.IsTrue(double.IsNaN(gmm.ObjectiveFunctionValue));
    }

    #endregion

    #region Clone Extended Tests

    /// <summary>
    /// Verifies <c>Test_Clone_IncludesPointwiseMomentConditions</c>.
    /// </summary>
    [TestMethod]
    public void Test_Clone_IncludesPointwiseMomentConditions()
    {
        var data = GenerateNormalData(mean: 50, stddev: 5, n: 100);
        var (momentFunc, pointwiseFunc) = CreateNormalMomentConditions(data);

        double sampleMean = data.Average();
        double sampleVar = data.Select(x => (x - sampleMean) * (x - sampleMean)).Average();

        var original = new GeneralizedMethodOfMoments(
            momentConditionFunction: momentFunc,
            numberOfParameters: 2,
            numberOfMomentConditions: 2,
            sampleSize: data.Length,
            initialValues: new[] { sampleMean, sampleVar },
            lowerBounds: new[] { -1000.0, 0.01 },
            upperBounds: new[] { 1000.0, 10000.0 },
            pointwiseMomentConditions: pointwiseFunc);

        var clone = original.Clone();

        Assert.AreEqual(original.NumberOfParameters, clone.NumberOfParameters);
        Assert.AreEqual(original.NumberOfMomentConditions, clone.NumberOfMomentConditions);
        Assert.AreEqual(original.SampleSize, clone.SampleSize);
        Assert.IsNotNull(clone.PointwiseMomentConditions, "Clone should preserve PointwiseMomentConditions.");
    }

    #endregion

    #region Validation Extended Tests

    /// <summary>
    /// Verifies <c>Test_IsValid_InvalidRelativeTolerance_ReturnsFalse</c>.
    /// </summary>
    [TestMethod]
    public void Test_IsValid_InvalidRelativeTolerance_ReturnsFalse()
    {
        var (momentFunc, data) = CreateSimpleMeanMomentCondition();

        var gmm = new GeneralizedMethodOfMoments(
            momentConditionFunction: momentFunc,
            numberOfParameters: 1,
            numberOfMomentConditions: 1,
            sampleSize: data.Length,
            initialValues: new[] { 90.0 },
            lowerBounds: new[] { 0.0 },
            upperBounds: new[] { 200.0 });

        gmm.RelativeTolerance = 1e-20;

        bool isValid = gmm.IsValid(out var errors);

        Assert.IsFalse(isValid);
        Assert.IsTrue(errors.Any(e => e.Contains("tolerance")));
    }

    #region Iteration accounting regression

    /// <summary>
    /// Convergence reached on the final permitted comparison pass is reported as confirmed.
    /// </summary>
    [TestMethod]
    public void Test_IterativeGmm_FinalPermittedPass_ReportsConverged()
    {
        var (momentFunc, data) = CreateSimpleMeanMomentCondition();
        var gmm = new GeneralizedMethodOfMoments(
            momentConditionFunction: momentFunc,
            numberOfParameters: 1,
            numberOfMomentConditions: 1,
            sampleSize: data.Length,
            initialValues: new[] { 90.0 },
            lowerBounds: new[] { 0.0 },
            upperBounds: new[] { 200.0 })
        {
            EstimationStrategy = GeneralizedMethodOfMoments.GMMEstimationStrategy.Iterative,
            MaxGMMIterations = 2,
            AbsoluteTolerance = 1e-6,
            RelativeTolerance = 1e-6,
        };

        Assert.IsTrue(gmm.Estimate());
        Assert.AreEqual(2, gmm.GMMIterations);
        Assert.IsTrue(gmm.ConvergedWithinTolerance);
    }

    /// <summary>
    /// Exhausting the iteration budget retains the final allowed pass count and reports no convergence.
    /// </summary>
    [TestMethod]
    public void Test_IterativeGmm_Exhaustion_ReportsMaxPassAndNotConverged()
    {
        MomentConditionFunction shiftingWeightedMoments = parameters =>
        {
            double theta = parameters[0];
            var moments = new Vector(new[] { theta, theta - 10.0 });
            double secondVariance = Math.Pow(1.0 + theta, 2.0);
            var covariance = new Matrix(new[,]
            {
                { 1.0, 0.0 },
                { 0.0, secondVariance },
            });
            return (moments, covariance);
        };

        var gmm = new GeneralizedMethodOfMoments(
            momentConditionFunction: shiftingWeightedMoments,
            numberOfParameters: 1,
            numberOfMomentConditions: 2,
            sampleSize: 100,
            initialValues: new[] { 5.0 },
            lowerBounds: new[] { 0.0 },
            upperBounds: new[] { 10.0 })
        {
            EstimationStrategy = GeneralizedMethodOfMoments.GMMEstimationStrategy.Iterative,
            MaxGMMIterations = 2,
            AbsoluteTolerance = 1e-12,
            RelativeTolerance = 1e-12,
        };

        Assert.IsTrue(gmm.Estimate());
        Assert.AreEqual(2, gmm.GMMIterations);
        Assert.IsFalse(gmm.ConvergedWithinTolerance);
        Assert.AreEqual(2, gmm.ConvergenceHistory.Count);
    }

    #endregion

    #endregion
}
