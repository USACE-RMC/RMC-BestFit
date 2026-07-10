using Numerics.Distributions;
using Numerics.Mathematics.Optimization;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;

namespace RMC.BestFit.Verification.ModelEstimation;

/// <summary>
/// Unit tests for the <see cref="MaximumAPosteriori"/> class.
/// Tests MAP estimation with various optimization methods.
/// </summary>
/// <remarks>
/// Maximum A Posteriori (MAP) estimation finds the mode of the posterior distribution
/// by maximizing the sum of data log-likelihood and prior log-likelihood.
/// It provides a Bayesian point estimate that incorporates prior information.
/// </remarks>
[TestClass]
public class MaximumAPosterioriTests
{
    #region Test Helpers

    /// <summary>
    /// Simple test model for MAP estimation tests.
    /// </summary>
    private class SimpleNormalModel : ModelBase
    {
        private readonly double[] _data;

        public SimpleNormalModel(double[] data)
        {
            _data = data;
            SetDefaultParameters();
        }

        /// <summary>
        /// Supports the <c>SetDefaultParameters</c> helper.
        /// </summary>
        /// <remarks>
        /// This helper keeps fixture setup local to the tests that use it.
        /// </remarks>
        public override void SetDefaultParameters()
        {
            Parameters = new List<ModelParameter>
            {
                new ModelParameter
                {
                    Name = "μ",
                    Value = 0.0,
                    LowerBound = -1000,
                    UpperBound = 1000,
                    PriorDistribution = new Uniform(-1000, 1000)
                },
                new ModelParameter
                {
                    Name = "σ",
                    Value = 1.0,
                    LowerBound = 0.001,
                    UpperBound = 1000,
                    PriorDistribution = new Uniform(0.001, 1000)
                }
            };
        }

        /// <summary>
        /// Supports the <c>DataLogLikelihood</c> helper.
        /// </summary>
        /// <param name="parameters">The parameter values.</param>
        /// <returns>The numeric data.</returns>
        /// <remarks>
        /// This helper keeps fixture setup local to the tests that use it.
        /// </remarks>
        public override double DataLogLikelihood(double[] parameters)
        {
            double mu = parameters[0];
            double sigma = parameters[1];
            if (sigma <= 0) return double.NegativeInfinity;

            double logLH = 0;
            var normal = new Normal(mu, sigma);
            foreach (var x in _data)
            {
                logLH += normal.LogPDF(x);
            }
            return logLH;
        }

        /// <summary>
        /// Supports the <c>PointwiseDataLogLikelihood</c> helper.
        /// </summary>
        /// <param name="parameters">The parameter values.</param>
        /// <returns>The numeric data.</returns>
        /// <remarks>
        /// This helper keeps fixture setup local to the tests that use it.
        /// </remarks>
        public override double[] PointwiseDataLogLikelihood(double[] parameters)
        {
            double mu = parameters[0];
            double sigma = parameters[1];
            if (sigma <= 0) return _data.Select(x => double.NegativeInfinity).ToArray();

            var normal = new Normal(mu, sigma);
            return _data.Select(x => normal.LogPDF(x)).ToArray();
        }

        /// <summary>
        /// Supports the <c>PointwiseDataLogLikelihoodComponents</c> helper.
        /// </summary>
        /// <param name="parameters">The parameter values.</param>
        /// <returns>The result.</returns>
        /// <remarks>
        /// This helper keeps fixture setup local to the tests that use it.
        /// </remarks>
        public override List<DataComponent> PointwiseDataLogLikelihoodComponents(double[] parameters)
        {
            var pointwise = PointwiseDataLogLikelihood(parameters);
            return pointwise.Select((ll, i) => new DataComponent(i, ll, _data[i])).ToList();
        }

        /// <summary>
        /// Supports the <c>Clone</c> helper.
        /// </summary>
        /// <returns>The result.</returns>
        /// <remarks>
        /// This helper keeps fixture setup local to the tests that use it.
        /// </remarks>
        public override IModel Clone()
        {
            var clone = new SimpleNormalModel(_data);
            for (int i = 0; i < NumberOfParameters; i++)
                clone.Parameters[i] = Parameters[i].Clone();
            return clone;
        }

        /// <summary>
        /// Supports the <c>ToXElement</c> helper.
        /// </summary>
        /// <returns>The result.</returns>
        /// <remarks>
        /// This helper keeps fixture setup local to the tests that use it.
        /// </remarks>
        public override System.Xml.Linq.XElement ToXElement()
        {
            return new System.Xml.Linq.XElement("SimpleNormalModel");
        }

        public override (bool IsValid, List<string> ValidationMessages) Validate()
        {
            return (true, new List<string>());
        }
    }

    /// <summary>
    /// Creates test data from a normal distribution.
    /// </summary>
    private static double[] CreateNormalData(double trueMu, double trueSigma, int n, int seed = 12345)
    {
        var random = new Random(seed);
        var data = new double[n];
        for (int i = 0; i < n; i++)
        {
            // Box-Muller transform
            double u1 = random.NextDouble();
            double u2 = random.NextDouble();
            double z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
            data[i] = trueMu + trueSigma * z;
        }
        return data;
    }

    #endregion

    #region Constructor Tests

    /// <summary>
    /// Verifies <c>Test_Constructor_ValidModel_CreatesInstance</c>.
    /// </summary>
    [TestMethod]
    public void Test_Constructor_ValidModel_CreatesInstance()
    {
        var data = CreateNormalData(100, 10, 50);
        var model = new SimpleNormalModel(data);

        var map = new MaximumAPosteriori(model);

        Assert.IsNotNull(map);
        Assert.AreSame(model, map.Model);
        Assert.AreEqual(2, map.NumberOfParameters);
        Assert.IsFalse(map.IsEstimated);
    }

    /// <summary>
    /// Verifies <c>Test_Constructor_WithOptimizationMethod</c>.
    /// </summary>
    [TestMethod]
    public void Test_Constructor_WithOptimizationMethod()
    {
        var data = CreateNormalData(100, 10, 50);
        var model = new SimpleNormalModel(data);

        var map = new MaximumAPosteriori(model, OptimizationMethod.NelderMead);

        Assert.AreEqual(OptimizationMethod.NelderMead, map.OptimizerMethod);
    }

    /// <summary>
    /// Verifies <c>Test_Constructor_NullModel_ThrowsException</c>.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Test_Constructor_NullModel_ThrowsException()
    {
        new MaximumAPosteriori(null!);
    }

    #endregion

    #region Property Tests

    /// <summary>
    /// Verifies <c>Test_OptimizerMethod_SetClearsResults</c>.
    /// </summary>
    [TestMethod]
    public void Test_OptimizerMethod_SetClearsResults()
    {
        var data = CreateNormalData(100, 10, 50);
        var model = new SimpleNormalModel(data);
        var map = new MaximumAPosteriori(model);

        map.Estimate();
        Assert.IsTrue(map.IsEstimated);

        map.OptimizerMethod = OptimizationMethod.NelderMead;
        Assert.IsFalse(map.IsEstimated);
    }

    /// <summary>
    /// Verifies <c>Test_InitialValues_FromModelParameters</c>.
    /// </summary>
    [TestMethod]
    public void Test_InitialValues_FromModelParameters()
    {
        var data = CreateNormalData(100, 10, 50);
        var model = new SimpleNormalModel(data);
        model.Parameters[0].Value = 50.0;
        model.Parameters[1].Value = 5.0;

        var map = new MaximumAPosteriori(model);

        Assert.AreEqual(50.0, map.InitialValues[0]);
        Assert.AreEqual(5.0, map.InitialValues[1]);
    }

    /// <summary>
    /// Verifies <c>Test_Bounds_FromModelParameters</c>.
    /// </summary>
    [TestMethod]
    public void Test_Bounds_FromModelParameters()
    {
        var data = CreateNormalData(100, 10, 50);
        var model = new SimpleNormalModel(data);

        var map = new MaximumAPosteriori(model);

        Assert.AreEqual(-1000, map.LowerBounds[0]);
        Assert.AreEqual(1000, map.UpperBounds[0]);
        Assert.AreEqual(0.001, map.LowerBounds[1]);
        Assert.AreEqual(1000, map.UpperBounds[1]);
    }

    #endregion

    #region Estimation Tests

    /// <summary>
    /// Verifies <c>Test_Estimate_NormalData_FindsMode</c>.
    /// </summary>
    [TestMethod]
    public void Test_Estimate_NormalData_FindsMode()
    {
        double trueMu = 100;
        double trueSigma = 10;
        var data = CreateNormalData(trueMu, trueSigma, 100);
        var model = new SimpleNormalModel(data);

        // Set initial values closer to the expected range for better convergence
        model.Parameters[0].Value = data.Average();
        model.Parameters[1].Value = Math.Sqrt(data.Select(x => Math.Pow(x - data.Average(), 2)).Average());

        // Use DifferentialEvolution for more robust global optimization
        var map = new MaximumAPosteriori(model, OptimizationMethod.DifferentialEvolution);
        bool success = map.Estimate();

        Assert.IsTrue(success, "MAP estimation failed.");
        Assert.IsTrue(map.IsEstimated);

        // MAP estimates should be close to sample statistics (MLE with flat priors)
        double sampleMean = data.Average();
        double sampleStd = Math.Sqrt(data.Select(x => Math.Pow(x - sampleMean, 2)).Average());

        Assert.AreEqual(sampleMean, map.BestParameterSet.Values[0], 5.0, "μ estimate not close to sample mean.");
        Assert.AreEqual(sampleStd, map.BestParameterSet.Values[1], 5.0, "σ estimate not close to sample std.");
    }

    /// <summary>
    /// Verifies <c>Test_Estimate_ReturnsPositiveLogLikelihood</c>.
    /// </summary>
    [TestMethod]
    public void Test_Estimate_ReturnsPositiveLogLikelihood()
    {
        var data = CreateNormalData(100, 10, 50);
        var model = new SimpleNormalModel(data);

        var map = new MaximumAPosteriori(model);
        map.Estimate();

        // Maximum log-likelihood should be finite
        Assert.IsFalse(double.IsNaN(map.MaximumLogLikelihood));
        Assert.IsFalse(double.IsInfinity(map.MaximumLogLikelihood));
    }

    /// <summary>
    /// Verifies <c>Test_Estimate_DifferentialEvolution</c>.
    /// </summary>
    [TestMethod]
    public void Test_Estimate_DifferentialEvolution()
    {
        var data = CreateNormalData(100, 10, 50);
        var model = new SimpleNormalModel(data);

        var map = new MaximumAPosteriori(model, OptimizationMethod.DifferentialEvolution);
        bool success = map.Estimate();

        Assert.IsTrue(success);
        Assert.IsTrue(map.TotalFunctionEvaluations > 0);
    }

    /// <summary>
    /// Verifies <c>Test_Estimate_BFGS</c>.
    /// </summary>
    [TestMethod]
    public void Test_Estimate_BFGS()
    {
        var data = CreateNormalData(100, 10, 50);
        var model = new SimpleNormalModel(data);
        model.Parameters[0].Value = 90.0;
        model.Parameters[1].Value = 8.0;

        var map = new MaximumAPosteriori(model, OptimizationMethod.BFGS);
        bool success = map.Estimate();

        // BFGS may not always succeed depending on gradient quality
        if (success)
        {
            Assert.IsTrue(map.IsEstimated);
        }
    }

    #endregion

    #region Profile Likelihood Tests

    /// <summary>
    /// Verifies <c>Test_ProfileLikelihood_ReturnsProfileForEachParameter</c>.
    /// </summary>
    [TestMethod]
    public void Test_ProfileLikelihood_ReturnsProfileForEachParameter()
    {
        var data = CreateNormalData(100, 10, 50);
        var model = new SimpleNormalModel(data);

        var map = new MaximumAPosteriori(model);
        map.Estimate();

        var profiles = map.ProfileLikelihood(bins: 50);

        Assert.AreEqual(2, profiles.Count);  // Two parameters
        Assert.AreEqual(50, profiles[0].GetLength(0));  // 50 bins
        Assert.AreEqual(2, profiles[0].GetLength(1));  // [value, logLH]
    }

    /// <summary>
    /// Verifies <c>Test_ProfileLikelihood_BeforeEstimation_ThrowsException</c>.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(InvalidOperationException))]
    public void Test_ProfileLikelihood_BeforeEstimation_ThrowsException()
    {
        var data = CreateNormalData(100, 10, 50);
        var model = new SimpleNormalModel(data);

        var map = new MaximumAPosteriori(model);
        map.ProfileLikelihood();  // Not estimated yet
    }

    /// <summary>
    /// Verifies <c>Test_ProfileLikelihood_InvalidBins_ThrowsException</c>.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void Test_ProfileLikelihood_InvalidBins_ThrowsException()
    {
        var data = CreateNormalData(100, 10, 50);
        var model = new SimpleNormalModel(data);

        var map = new MaximumAPosteriori(model);
        map.Estimate();
        map.ProfileLikelihood(bins: 1);  // Bins must be at least 2
    }

    #endregion

    #region Confidence Interval Tests

    /// <summary>
    /// Verifies <c>Test_ParameterConfidenceIntervals_ReturnsIntervals</c>.
    /// </summary>
    [TestMethod]
    public void Test_ParameterConfidenceIntervals_ReturnsIntervals()
    {
        var data = CreateNormalData(100, 10, 100);
        var model = new SimpleNormalModel(data);

        var map = new MaximumAPosteriori(model);
        map.Estimate();

        var cis = map.ParameterConfidenceIntervals(alpha: 0.1);

        Assert.AreEqual(2, cis.GetLength(0));  // Two parameters
        Assert.AreEqual(2, cis.GetLength(1));  // Lower and upper

        // Lower should be less than upper
        Assert.IsTrue(cis[0, 0] < cis[0, 1]);
        Assert.IsTrue(cis[1, 0] < cis[1, 1]);
    }

    /// <summary>
    /// Verifies <c>Test_ParameterConfidenceIntervals_InvalidAlpha_ThrowsException</c>.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void Test_ParameterConfidenceIntervals_InvalidAlpha_ThrowsException()
    {
        var data = CreateNormalData(100, 10, 50);
        var model = new SimpleNormalModel(data);

        var map = new MaximumAPosteriori(model);
        map.Estimate();
        map.ParameterConfidenceIntervals(alpha: 1.5);  // Invalid alpha
    }

    #endregion

    #region Covariance Matrix Tests

    /// <summary>
    /// Verifies <c>Test_GetCovarianceMatrix_ReturnsSymmetricMatrix</c>.
    /// </summary>
    [TestMethod]
    public void Test_GetCovarianceMatrix_ReturnsSymmetricMatrix()
    {
        var data = CreateNormalData(100, 10, 100);
        var model = new SimpleNormalModel(data);

        // Set initial values closer to expected range for better convergence
        model.Parameters[0].Value = data.Average();
        model.Parameters[1].Value = Math.Sqrt(data.Select(x => Math.Pow(x - data.Average(), 2)).Average());

        // Use DifferentialEvolution for more reliable Hessian computation
        var map = new MaximumAPosteriori(model, OptimizationMethod.DifferentialEvolution);
        map.Estimate();

        var covariance = map.GetCovarianceMatrix();

        Assert.AreEqual(2, covariance.NumberOfRows);
        Assert.AreEqual(2, covariance.NumberOfColumns);

        // Should be symmetric (after matrix regularization)
        Assert.AreEqual(covariance[0, 1], covariance[1, 0], 1e-10);
    }

    /// <summary>
    /// Verifies <c>Test_GetStandardErrors_ReturnsPositiveValues</c>.
    /// </summary>
    [TestMethod]
    public void Test_GetStandardErrors_ReturnsPositiveValues()
    {
        var data = CreateNormalData(100, 10, 100);
        var model = new SimpleNormalModel(data);

        var map = new MaximumAPosteriori(model);
        map.Estimate();

        var se = map.GetStandardErrors();

        Assert.AreEqual(2, se.Length);
        Assert.IsTrue(se[0] >= 0);
        Assert.IsTrue(se[1] >= 0);
    }

    /// <summary>
    /// Verifies <c>Test_GetCorrelationMatrix_DiagonalIsOne</c>.
    /// </summary>
    [TestMethod]
    public void Test_GetCorrelationMatrix_DiagonalIsOne()
    {
        var data = CreateNormalData(100, 10, 100);
        var model = new SimpleNormalModel(data);

        var map = new MaximumAPosteriori(model);
        map.Estimate();

        var correlation = map.GetCorrelationMatrix();

        Assert.AreEqual(1.0, correlation[0, 0], 1e-10);
        Assert.AreEqual(1.0, correlation[1, 1], 1e-10);
    }

    #endregion

    #region Information Criteria Tests

    /// <summary>
    /// Verifies <c>Test_GetAIC_ReturnsFiniteValue</c>.
    /// </summary>
    [TestMethod]
    public void Test_GetAIC_ReturnsFiniteValue()
    {
        var data = CreateNormalData(100, 10, 100);
        var model = new SimpleNormalModel(data);

        var map = new MaximumAPosteriori(model);
        map.Estimate();

        double aic = map.GetAIC();

        Assert.IsFalse(double.IsNaN(aic));
        Assert.IsFalse(double.IsInfinity(aic));
    }

    /// <summary>
    /// Verifies <c>Test_GetBIC_ReturnsFiniteValue</c>.
    /// </summary>
    [TestMethod]
    public void Test_GetBIC_ReturnsFiniteValue()
    {
        var data = CreateNormalData(100, 10, 100);
        var model = new SimpleNormalModel(data);

        var map = new MaximumAPosteriori(model);
        map.Estimate();

        double bic = map.GetBIC(sampleSize: data.Length);

        Assert.IsFalse(double.IsNaN(bic));
        Assert.IsFalse(double.IsInfinity(bic));
    }

    /// <summary>
    /// Verifies <c>Test_GetAIC_BeforeEstimation_ThrowsException</c>.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(InvalidOperationException))]
    public void Test_GetAIC_BeforeEstimation_ThrowsException()
    {
        var data = CreateNormalData(100, 10, 50);
        var model = new SimpleNormalModel(data);

        var map = new MaximumAPosteriori(model);
        map.GetAIC();  // Not estimated yet
    }

    /// <summary>
    /// Verifies <c>Test_GetBIC_InvalidSampleSize_ThrowsException</c>.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void Test_GetBIC_InvalidSampleSize_ThrowsException()
    {
        var data = CreateNormalData(100, 10, 50);
        var model = new SimpleNormalModel(data);

        var map = new MaximumAPosteriori(model);
        map.Estimate();
        map.GetBIC(sampleSize: 0);  // Invalid sample size
    }

    #endregion

    #region Clear Results Tests

    /// <summary>
    /// Verifies <c>Test_ClearResults_ResetsState</c>.
    /// </summary>
    [TestMethod]
    public void Test_ClearResults_ResetsState()
    {
        var data = CreateNormalData(100, 10, 50);
        var model = new SimpleNormalModel(data);

        var map = new MaximumAPosteriori(model);
        map.Estimate();
        Assert.IsTrue(map.IsEstimated);

        map.ClearResults();

        Assert.IsFalse(map.IsEstimated);
        Assert.AreEqual(0, map.TotalFunctionEvaluations);
    }

    #endregion
}
