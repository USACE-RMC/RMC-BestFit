using Numerics.Distributions;
using Numerics.Mathematics.Optimization;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;

namespace RMC.BestFit.Verification.ModelEstimation;

/// <summary>
/// Verifies MAP mode recovery and analytical information-criterion contracts.
/// </summary>
[TestClass]
public class MaximumAPosterioriRecoveryTests
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
    /// Verifies that AIC uses the data likelihood at MAP and excludes prior density.
    /// </summary>
    [TestMethod]
    public void Test_GetAIC_ReturnsFiniteValue()
    {
        var data = CreateNormalData(100, 10, 100);
        var model = new SimpleNormalModel(data);

        var map = new MaximumAPosteriori(model);
        map.Estimate();

        double dataLogLikelihood = model.DataLogLikelihood(map.BestParameterSet.Values);
        double posteriorLogLikelihood = model.LogLikelihood(map.BestParameterSet.Values);
        double aic = map.GetAIC();
        double expectedAic = -2.0 * dataLogLikelihood + 2.0 * map.NumberOfParameters;
        double posteriorKernelAic = -2.0 * posteriorLogLikelihood + 2.0 * map.NumberOfParameters;

        Assert.AreEqual(expectedAic, aic, 1e-10);
        Assert.IsTrue(Math.Abs(aic - posteriorKernelAic) > 1e-6);
    }

    /// <summary>
    /// Verifies that BIC uses the data likelihood at MAP and excludes prior density.
    /// </summary>
    [TestMethod]
    public void Test_GetBIC_ReturnsFiniteValue()
    {
        var data = CreateNormalData(100, 10, 100);
        var model = new SimpleNormalModel(data);

        var map = new MaximumAPosteriori(model);
        map.Estimate();

        double dataLogLikelihood = model.DataLogLikelihood(map.BestParameterSet.Values);
        double posteriorLogLikelihood = model.LogLikelihood(map.BestParameterSet.Values);
        double bic = map.GetBIC(sampleSize: data.Length);
        double expectedBic = -2.0 * dataLogLikelihood + map.NumberOfParameters * Math.Log(data.Length);
        double posteriorKernelBic = -2.0 * posteriorLogLikelihood + map.NumberOfParameters * Math.Log(data.Length);

        Assert.AreEqual(expectedBic, bic, 1e-10);
        Assert.IsTrue(Math.Abs(bic - posteriorKernelBic) > 1e-6);
    }
}
