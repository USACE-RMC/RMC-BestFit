using Numerics.Distributions;
using Numerics.Mathematics.Optimization;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using RMC.BestFit.Verification.Recovery;

namespace RMC.BestFit.Verification.ModelEstimation;

/// <summary>
/// Verifies a generated-parent Normal MAP recovery under the existing optimizer configuration.
/// </summary>
/// <remarks>
/// The recovery design uses N=1000 scalar Normal observations from parent=(mu=100, sigma=10) with
/// seed=12345. The acceptance rule is absolute standardized error no greater than 1.96 using the
/// MAP observed-information Hessian standard errors. AIC and BIC analytical claims reside in
/// <see cref="MaximumAPosterioriInformationCriteriaOracleTests"/>.
/// </remarks>
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
    /// Verifies N=1000 Normal MAP generated-parent recovery using the common frequentist rule.
    /// </summary>
    /// <remarks>
    /// Sample unit: scalar observation; N=1000; seed=12345; parent=(mu=100, sigma=10); fitted
    /// coordinates=(mu, sigma). Standard errors come from the MAP observed-information Hessian.
    /// The conditional secondary 5% criterion is not used because this is not a response-grid cell.
    /// </remarks>
    [TestMethod]
    public void Test_Estimate_NormalData_FindsMode()
    {
        double trueMu = 100;
        double trueSigma = 10;
        var data = CreateNormalData(trueMu, trueSigma, RecoveryDesign.SampleSize);
        var model = new SimpleNormalModel(data);

        // Set initial values closer to the expected range for better convergence
        model.Parameters[0].Value = data.Average();
        model.Parameters[1].Value = Math.Sqrt(data.Select(x => Math.Pow(x - data.Average(), 2)).Average());

        // Use DifferentialEvolution for more robust global optimization
        var map = new MaximumAPosteriori(model, OptimizationMethod.DifferentialEvolution);
        bool success = map.Estimate();

        Assert.IsTrue(success, "MAP estimation failed.");
        Assert.IsTrue(map.IsEstimated);

        double[] standardErrors = map.GetStandardErrors();
        RecoveryAcceptance.AssertFrequentistStandardizedError("mu", map.BestParameterSet.Values[0], trueMu, standardErrors[0]);
        RecoveryAcceptance.AssertFrequentistStandardizedError("sigma", map.BestParameterSet.Values[1], trueSigma, standardErrors[1]);
    }

}
