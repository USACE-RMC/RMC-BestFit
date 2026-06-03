using Numerics.Distributions;
using RMC.BestFit.Models;

namespace RMC.BestFit.Tests.UnivariateAnalyses;

/// <summary>
/// Unit tests for the <see cref="MixtureModel"/> class.
/// Tests mixture distribution models with multiple components and optional zero inflation.
/// </summary>
/// <remarks>
/// Mixture models are used in flood frequency analysis to represent multiple
/// flood-generating mechanisms (e.g., snowmelt vs. rainfall floods, or seasonal
/// populations). This test class validates:
/// - Multiple constructor patterns
/// - Zero-inflation for intermittent streams
/// - EM algorithm for initial parameter estimation
/// - Bayesian likelihood computation
/// - Single quantile prior restriction
/// </remarks>
[TestClass]
public class MixtureModelTests
{
    #region Test Data Helper

    /// <summary>
    /// Creates a sample data frame with positive exact observations.
    /// </summary>
    private static DataFrame CreateSampleDataFrame()
    {
        var df = new DataFrame();
        var data = new List<ExactData>
        {
            new ExactData { Index = 1990, Value = 1200 },
            new ExactData { Index = 1991, Value = 1500 },
            new ExactData { Index = 1992, Value = 1800 },
            new ExactData { Index = 1993, Value = 2200 },
            new ExactData { Index = 1994, Value = 2500 },
            new ExactData { Index = 1995, Value = 2800 },
            new ExactData { Index = 1996, Value = 3200 },
            new ExactData { Index = 1997, Value = 3600 },
            new ExactData { Index = 1998, Value = 4000 },
            new ExactData { Index = 1999, Value = 4500 }
        };
        df.ExactSeries = new ExactSeries(data);
        return df;
    }

    /// <summary>
    /// Creates a bimodal data frame typical of mixed flood populations.
    /// </summary>
    private static DataFrame CreateBimodalDataFrame()
    {
        var df = new DataFrame();
        var data = new List<ExactData>();

        // Low flow population (e.g., baseflow floods)
        for (int i = 0; i < 15; i++)
        {
            data.Add(new ExactData { Index = 1980 + i, Value = 500 + i * 50 });
        }

        // High flow population (e.g., extreme events)
        data.Add(new ExactData { Index = 1995, Value = 5000 });
        data.Add(new ExactData { Index = 1996, Value = 6000 });
        data.Add(new ExactData { Index = 1997, Value = 7500 });
        data.Add(new ExactData { Index = 1998, Value = 9000 });
        data.Add(new ExactData { Index = 1999, Value = 12000 });

        df.ExactSeries = new ExactSeries(data);
        return df;
    }

    /// <summary>
    /// Creates a data frame with zero values for zero-inflation testing.
    /// </summary>
    private static DataFrame CreateZeroInflatedDataFrame()
    {
        var df = new DataFrame();
        var data = new List<ExactData>
        {
            new ExactData { Index = 1990, Value = 0 },
            new ExactData { Index = 1991, Value = 0 },
            new ExactData { Index = 1992, Value = 100 },
            new ExactData { Index = 1993, Value = 0 },
            new ExactData { Index = 1994, Value = 250 },
            new ExactData { Index = 1995, Value = 0 },
            new ExactData { Index = 1996, Value = 500 },
            new ExactData { Index = 1997, Value = 750 },
            new ExactData { Index = 1998, Value = 0 },
            new ExactData { Index = 1999, Value = 1000 }
        };
        df.ExactSeries = new ExactSeries(data);
        return df;
    }

    #endregion

    #region Constructor Tests

    /// <summary>Verifies that constructor empty constructor creates default normal mixture.</summary>
    [TestMethod]
    public void Test_Constructor_EmptyConstructor_CreatesDefaultNormalMixture()
    {
        var model = new MixtureModel();

        Assert.IsNotNull(model);
        Assert.IsNotNull(model.Mixture);
        Assert.AreEqual(2, model.Mixture!.Distributions.Length);
        Assert.IsInstanceOfType(model.Mixture!.Distributions[0], typeof(Normal));
        Assert.IsInstanceOfType(model.Mixture!.Distributions[1], typeof(Normal));
    }

    /// <summary>Verifies that constructor empty constructor has equal weights.</summary>
    [TestMethod]
    public void Test_Constructor_EmptyConstructor_HasEqualWeights()
    {
        var model = new MixtureModel();

        Assert.AreEqual(0.5, model.Mixture!.Weights[0], 1e-10);
        Assert.AreEqual(0.5, model.Mixture!.Weights[1], 1e-10);
    }

    /// <summary>Verifies that constructor with data and mixture sets properties.</summary>
    [TestMethod]
    public void Test_Constructor_WithDataAndMixture_SetsProperties()
    {
        var df = CreateSampleDataFrame();
        var mixture = new Mixture(
            new[] { 0.3, 0.7 },
            new UnivariateDistributionBase[] { new Normal(1000, 200), new Normal(3000, 500) });

        var model = new MixtureModel(df, mixture);

        Assert.AreSame(df, model.DataFrame);
        Assert.IsNotNull(model.Mixture);
        Assert.AreEqual(2, model.Mixture!.Distributions.Length);
    }

    /// <summary>Verifies that constructor with distribution types creates components.</summary>
    [TestMethod]
    public void Test_Constructor_WithDistributionTypes_CreatesComponents()
    {
        var df = CreateSampleDataFrame();
        var types = new List<UnivariateDistributionType>
        {
            UnivariateDistributionType.Normal,
            UnivariateDistributionType.GammaDistribution
        };

        var model = new MixtureModel(df, types);

        Assert.AreEqual(2, model.Mixture!.Distributions.Length);
        Assert.IsInstanceOfType(model.Mixture!.Distributions[0], typeof(Normal));
        Assert.IsInstanceOfType(model.Mixture!.Distributions[1], typeof(GammaDistribution));
    }

    /// <summary>Verifies that constructor with distribution types zero inflated.</summary>
    [TestMethod]
    public void Test_Constructor_WithDistributionTypes_ZeroInflated()
    {
        var df = CreateZeroInflatedDataFrame();
        var types = new List<UnivariateDistributionType> { UnivariateDistributionType.Normal };

        var model = new MixtureModel(df, types, isZeroInflated: true);

        Assert.IsTrue(model.IsZeroInflated);
        Assert.IsTrue(model.Mixture!.ZeroWeight > 0);
    }

    /// <summary>Verifies that constructor with distributions uses provided instances.</summary>
    [TestMethod]
    public void Test_Constructor_WithDistributions_UsesProvidedInstances()
    {
        var df = CreateSampleDataFrame();
        var dist1 = new Normal(1500, 300);
        var dist2 = new Gumbel(2500, 400);
        var distributions = new List<UnivariateDistributionBase> { dist1, dist2 };

        var model = new MixtureModel(df, distributions);

        Assert.AreEqual(2, model.Mixture!.Distributions.Length);
        Assert.AreEqual(1500, ((Normal)model.Mixture!.Distributions[0]).Mu, 1e-10);
    }

    /// <summary>Verifies that constructor throws when with empty distribution types.</summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void Test_Constructor_WithEmptyDistributionTypes_ThrowsException()
    {
        var df = CreateSampleDataFrame();
        var types = new List<UnivariateDistributionType>();

        var model = new MixtureModel(df, types);
    }

    /// <summary>Verifies that constructor throws when with empty distributions.</summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void Test_Constructor_WithEmptyDistributions_ThrowsException()
    {
        var df = CreateSampleDataFrame();
        var distributions = new List<UnivariateDistributionBase>();

        var model = new MixtureModel(df, distributions);
    }

    /// <summary>Verifies that constructor X element restores model.</summary>
    [TestMethod]
    public void Test_Constructor_XElement_RestoresModel()
    {
        var df = CreateSampleDataFrame();
        var types = new List<UnivariateDistributionType>
        {
            UnivariateDistributionType.Normal,
            UnivariateDistributionType.Gumbel
        };
        var original = new MixtureModel(df, types);
        var xElement = original.ToXElement();

        var restored = new MixtureModel(df, xElement);

        Assert.AreEqual(2, restored.Mixture!.Distributions.Length);
        Assert.AreEqual(original.IsZeroInflated, restored.IsZeroInflated);
    }

    #endregion

    #region Property Tests

    /// <summary>Verifies that mixture set and get.</summary>
    [TestMethod]
    public void Test_Mixture_SetAndGet()
    {
        var model = new MixtureModel();
        var newMixture = new Mixture(
            new[] { 0.4, 0.6 },
            new UnivariateDistributionBase[] { new Gumbel(100, 20), new Normal(200, 50) });

        model.Mixture = newMixture;

        Assert.IsNotNull(model.Mixture);
        Assert.AreEqual(0.4, model.Mixture!.Weights[0], 1e-10);
    }

    /// <summary>Verifies that is zero inflated set and get.</summary>
    [TestMethod]
    public void Test_IsZeroInflated_SetAndGet()
    {
        var df = CreateZeroInflatedDataFrame();
        var types = new List<UnivariateDistributionType> { UnivariateDistributionType.Normal };
        var model = new MixtureModel(df, types);

        model.IsZeroInflated = true;

        Assert.IsTrue(model.IsZeroInflated);
        Assert.IsTrue(model.Mixture!.IsZeroInflated);
    }

    /// <summary>Verifies that is zero inflated sets zero weight.</summary>
    [TestMethod]
    public void Test_IsZeroInflated_SetsZeroWeight()
    {
        var df = CreateZeroInflatedDataFrame();
        var types = new List<UnivariateDistributionType> { UnivariateDistributionType.Normal };
        var model = new MixtureModel(df, types);

        model.IsZeroInflated = true;

        // 5 out of 10 values are zero, so ZeroWeight should be ~0.5
        Assert.IsTrue(model.Mixture!.ZeroWeight > 0.4 && model.Mixture!.ZeroWeight < 0.6);
    }

    /// <summary>Verifies that is zero inflated false zero weight is zero.</summary>
    [TestMethod]
    public void Test_IsZeroInflated_False_ZeroWeightIsZero()
    {
        var df = CreateSampleDataFrame();
        var types = new List<UnivariateDistributionType> { UnivariateDistributionType.Normal };
        var model = new MixtureModel(df, types);

        model.IsZeroInflated = false;

        Assert.AreEqual(0.0, model.Mixture!.ZeroWeight);
    }

    /// <summary>Verifies that use single quantile always true.</summary>
    [TestMethod]
    public void Test_UseSingleQuantile_AlwaysTrue()
    {
        var model = new MixtureModel();

        Assert.IsTrue(model.UseSingleQuantile);

        // Setting to false should be ignored
        model.UseSingleQuantile = false;
        Assert.IsTrue(model.UseSingleQuantile);
    }

    /// <summary>Verifies that data frame set triggers parameter update.</summary>
    [TestMethod]
    public void Test_DataFrame_SetTriggersParameterUpdate()
    {
        var df1 = CreateSampleDataFrame();
        var types = new List<UnivariateDistributionType>
        {
            UnivariateDistributionType.Normal,
            UnivariateDistributionType.Normal
        };
        var model = new MixtureModel(df1, types);
        int initialParamCount = model.NumberOfParameters;

        var df2 = CreateBimodalDataFrame();
        model.DataFrame = df2;

        // Parameters should be recalculated
        Assert.IsNotNull(model.Parameters);
        Assert.AreEqual(initialParamCount, model.NumberOfParameters);
    }

    #endregion

    #region SetDefaultMixture Tests

    /// <summary>Verifies that set default mixture single component.</summary>
    [TestMethod]
    public void Test_SetDefaultMixture_SingleComponent()
    {
        var model = new MixtureModel();

        model.SetDefaultMixture(new[] { UnivariateDistributionType.Gumbel });

        Assert.AreEqual(1, model.Mixture!.Distributions.Length);
        Assert.IsInstanceOfType(model.Mixture!.Distributions[0], typeof(Gumbel));
    }

    /// <summary>Verifies that set default mixture two components equal weights.</summary>
    [TestMethod]
    public void Test_SetDefaultMixture_TwoComponents_EqualWeights()
    {
        var model = new MixtureModel();

        model.SetDefaultMixture(new[]
        {
            UnivariateDistributionType.Normal,
            UnivariateDistributionType.Gumbel
        });

        Assert.AreEqual(0.5, model.Mixture!.Weights[0], 1e-10);
        Assert.AreEqual(0.5, model.Mixture!.Weights[1], 1e-10);
    }

    /// <summary>Verifies that set default mixture three components equal weights.</summary>
    [TestMethod]
    public void Test_SetDefaultMixture_ThreeComponents_EqualWeights()
    {
        var model = new MixtureModel();

        model.SetDefaultMixture(new[]
        {
            UnivariateDistributionType.Normal,
            UnivariateDistributionType.Gumbel,
            UnivariateDistributionType.LogNormal
        });

        double expectedWeight = 1.0 / 3.0;
        Assert.AreEqual(expectedWeight, model.Mixture!.Weights[0], 1e-10);
        Assert.AreEqual(expectedWeight, model.Mixture!.Weights[1], 1e-10);
        Assert.AreEqual(expectedWeight, model.Mixture!.Weights[2], 1e-10);
    }

    /// <summary>Verifies that set default mixture weights sum to one.</summary>
    [TestMethod]
    public void Test_SetDefaultMixture_WeightsSumToOne()
    {
        var model = new MixtureModel();

        model.SetDefaultMixture(new[]
        {
            UnivariateDistributionType.Normal,
            UnivariateDistributionType.Gumbel,
            UnivariateDistributionType.GammaDistribution
        });

        double sum = model.Mixture!.Weights.Sum();
        Assert.AreEqual(1.0, sum, 1e-10);
    }

    /// <summary>Verifies that set default mixture throws when empty list.</summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void Test_SetDefaultMixture_EmptyList_ThrowsException()
    {
        var model = new MixtureModel();
        model.SetDefaultMixture(Array.Empty<UnivariateDistributionType>());
    }

    #endregion

    #region SetDefaultParameters Tests

    /// <summary>Verifies that set default parameters two components has weight parameters.</summary>
    [TestMethod]
    public void Test_SetDefaultParameters_TwoComponents_HasWeightParameters()
    {
        var df = CreateSampleDataFrame();
        var types = new List<UnivariateDistributionType>
        {
            UnivariateDistributionType.Normal,
            UnivariateDistributionType.Normal
        };
        var model = new MixtureModel(df, types);

        // Should have 2 weight parameters + 2*2 distribution parameters = 6
        Assert.IsTrue(model.NumberOfParameters >= 6);

        // First two parameters should be weights
        Assert.IsTrue(model.Parameters[0].Name.Contains("Weight"));
        Assert.IsTrue(model.Parameters[1].Name.Contains("Weight"));
    }

    /// <summary>Verifies that set default parameters single component no weight parameters.</summary>
    [TestMethod]
    public void Test_SetDefaultParameters_SingleComponent_NoWeightParameters()
    {
        var df = CreateSampleDataFrame();
        var types = new List<UnivariateDistributionType> { UnivariateDistributionType.Normal };
        var model = new MixtureModel(df, types);

        // Single component has no weight parameters
        Assert.IsFalse(model.Parameters.Any(p => p.Name.Contains("Weight")));
    }

    /// <summary>Verifies that set default parameters weight bounds.</summary>
    [TestMethod]
    public void Test_SetDefaultParameters_WeightBounds()
    {
        var df = CreateSampleDataFrame();
        var types = new List<UnivariateDistributionType>
        {
            UnivariateDistributionType.Normal,
            UnivariateDistributionType.Normal
        };
        var model = new MixtureModel(df, types);

        var weightParams = model.Parameters.Where(p => p.Name.Contains("Weight")).ToList();

        foreach (var param in weightParams)
        {
            Assert.AreEqual(0.0, param.LowerBound);
            Assert.AreEqual(1.0, param.UpperBound);
        }
    }

    /// <summary>Verifies that set default parameters weights have uniform prior.</summary>
    [TestMethod]
    public void Test_SetDefaultParameters_WeightsHaveUniformPrior()
    {
        var df = CreateSampleDataFrame();
        var types = new List<UnivariateDistributionType>
        {
            UnivariateDistributionType.Normal,
            UnivariateDistributionType.Gumbel
        };
        var model = new MixtureModel(df, types);

        var weightParams = model.Parameters.Where(p => p.Name.Contains("Weight")).ToList();

        foreach (var param in weightParams)
        {
            Assert.IsInstanceOfType(param.PriorDistribution, typeof(Uniform));
        }
    }

    #endregion

    #region SetDefaultQuantilePriors Tests

    /// <summary>Verifies that set default quantile priors single quantile.</summary>
    [TestMethod]
    public void Test_SetDefaultQuantilePriors_SingleQuantile()
    {
        var df = CreateSampleDataFrame();
        var types = new List<UnivariateDistributionType> { UnivariateDistributionType.Normal };
        var model = new MixtureModel(df, types);
        model.EnableQuantilePriors = true;

        model.SetDefaultQuantilePriors();

        // Mixture model uses single quantile prior
        Assert.AreEqual(1, model.QuantilePriors.Count);
    }

    /// <summary>Verifies that set default quantile priors disabled empty list.</summary>
    [TestMethod]
    public void Test_SetDefaultQuantilePriors_Disabled_EmptyList()
    {
        var df = CreateSampleDataFrame();
        var types = new List<UnivariateDistributionType> { UnivariateDistributionType.Normal };
        var model = new MixtureModel(df, types);
        model.EnableQuantilePriors = false;

        model.SetDefaultQuantilePriors();

        Assert.AreEqual(0, model.QuantilePriors.Count);
    }

    /// <summary>Verifies that set default quantile priors uses ln normal distribution.</summary>
    [TestMethod]
    public void Test_SetDefaultQuantilePriors_UsesLnNormalDistribution()
    {
        var df = CreateSampleDataFrame();
        var types = new List<UnivariateDistributionType> { UnivariateDistributionType.Normal };
        var model = new MixtureModel(df, types);
        model.EnableQuantilePriors = true;

        model.SetDefaultQuantilePriors();

        Assert.IsInstanceOfType(model.QuantilePriors[0].Distribution, typeof(LnNormal));
    }

    #endregion

    #region LogLikelihood Tests

    /// <summary>Verifies that log likelihood returns finite value.</summary>
    [TestMethod]
    public void Test_LogLikelihood_ReturnsFiniteValue()
    {
        var df = CreateSampleDataFrame();
        var types = new List<UnivariateDistributionType>
        {
            UnivariateDistributionType.Normal,
            UnivariateDistributionType.Normal
        };
        var model = new MixtureModel(df, types);

        var parameters = model.Parameters.Select(p => p.Value).ToArray();
        double logLH = model.LogLikelihood(parameters);

        Assert.IsFalse(double.IsNaN(logLH));
        Assert.IsFalse(double.IsPositiveInfinity(logLH));
    }

    /// <summary>Verifies that data log likelihood returns finite value.</summary>
    [TestMethod]
    public void Test_DataLogLikelihood_ReturnsFiniteValue()
    {
        var df = CreateSampleDataFrame();
        var types = new List<UnivariateDistributionType>
        {
            UnivariateDistributionType.Normal,
            UnivariateDistributionType.Gumbel
        };
        var model = new MixtureModel(df, types);

        var parameters = model.Parameters.Select(p => p.Value).ToArray();
        double dataLogLH = model.DataLogLikelihood(parameters);

        Assert.IsFalse(double.IsNaN(dataLogLH));
        Assert.IsFalse(double.IsPositiveInfinity(dataLogLH));
    }

    /// <summary>Verifies that prior log likelihood returns finite value.</summary>
    [TestMethod]
    public void Test_PriorLogLikelihood_ReturnsFiniteValue()
    {
        var df = CreateSampleDataFrame();
        var types = new List<UnivariateDistributionType> { UnivariateDistributionType.Normal };
        var model = new MixtureModel(df, types);

        var parameters = model.Parameters.Select(p => p.Value).ToArray();
        double priorLogLH = model.PriorLogLikelihood(parameters);

        Assert.IsFalse(double.IsNaN(priorLogLH));
        Assert.IsFalse(double.IsPositiveInfinity(priorLogLH));
    }

    /// <summary>Verifies that log likelihood equals data plus prior.</summary>
    [TestMethod]
    public void Test_LogLikelihood_EqualsDataPlusPrior()
    {
        var df = CreateSampleDataFrame();
        var types = new List<UnivariateDistributionType> { UnivariateDistributionType.Normal };
        var model = new MixtureModel(df, types);

        var parameters = model.Parameters.Select(p => p.Value).ToArray();
        double fullLogLH = model.LogLikelihood(parameters);
        double dataLogLH = model.DataLogLikelihood(parameters);
        double priorLogLH = model.PriorLogLikelihood(parameters);

        // Full = Data + Prior (allowing for numerical precision)
        Assert.AreEqual(dataLogLH + priorLogLH, fullLogLH, 1e-6);
    }

    /// <summary>Verifies that pointwise data log likelihood returns correct count.</summary>
    [TestMethod]
    public void Test_PointwiseDataLogLikelihood_ReturnsCorrectCount()
    {
        var df = CreateSampleDataFrame();
        var types = new List<UnivariateDistributionType> { UnivariateDistributionType.Normal };
        var model = new MixtureModel(df, types);

        var parameters = model.Parameters.Select(p => p.Value).ToArray();
        var pointwise = model.PointwiseDataLogLikelihood(parameters);

        Assert.AreEqual(df.ExactSeries.Count, pointwise.Length);
    }

    /// <summary>Verifies that pointwise data log likelihood sum equals data log likelihood.</summary>
    [TestMethod]
    public void Test_PointwiseDataLogLikelihood_SumEqualsDataLogLikelihood()
    {
        var df = CreateSampleDataFrame();
        var types = new List<UnivariateDistributionType> { UnivariateDistributionType.Normal };
        var model = new MixtureModel(df, types);

        var parameters = model.Parameters.Select(p => p.Value).ToArray();
        var pointwise = model.PointwiseDataLogLikelihood(parameters);
        double dataLogLH = model.DataLogLikelihood(parameters);

        double sum = pointwise.Sum();
        Assert.AreEqual(dataLogLH, sum, 1e-6);
    }

    #endregion

    #region Zero-Inflation Tests

    /// <summary>Verifies that zero inflated log likelihood handles zero values.</summary>
    [TestMethod]
    public void Test_ZeroInflated_LogLikelihood_HandlesZeroValues()
    {
        var df = CreateZeroInflatedDataFrame();
        var types = new List<UnivariateDistributionType> { UnivariateDistributionType.Normal };
        var model = new MixtureModel(df, types, isZeroInflated: true);

        var parameters = model.Parameters.Select(p => p.Value).ToArray();
        double logLH = model.LogLikelihood(parameters);

        Assert.IsFalse(double.IsNaN(logLH));
        Assert.IsFalse(double.IsNegativeInfinity(logLH));
    }

    /// <summary>Verifies that zero inflated zero weight reflects data.</summary>
    [TestMethod]
    public void Test_ZeroInflated_ZeroWeightReflectsData()
    {
        var df = CreateZeroInflatedDataFrame();
        var types = new List<UnivariateDistributionType> { UnivariateDistributionType.Normal };
        var model = new MixtureModel(df, types, isZeroInflated: true);

        // Should reflect proportion of zeros in data
        Assert.IsTrue(model.Mixture!.ZeroWeight > 0);
    }

    #endregion

    #region ExpectationMaximization Tests

    /// <summary>Verifies that expectation maximization returns parameters.</summary>
    [TestMethod]
    public void Test_ExpectationMaximization_ReturnsParameters()
    {
        var df = CreateSampleDataFrame();
        var types = new List<UnivariateDistributionType>
        {
            UnivariateDistributionType.Normal,
            UnivariateDistributionType.Normal
        };
        var model = new MixtureModel(df, types);

        model.ExpectationMaximization(out double[] parameters, out double[,] covariance, out int iterations);

        Assert.IsTrue(parameters.Length > 0);
        Assert.IsTrue(iterations > 0);
    }

    /// <summary>Verifies that expectation maximization converges within max iterations.</summary>
    [TestMethod]
    public void Test_ExpectationMaximization_ConvergesWithinMaxIterations()
    {
        var df = CreateSampleDataFrame();
        var types = new List<UnivariateDistributionType>
        {
            UnivariateDistributionType.Normal,
            UnivariateDistributionType.Normal
        };
        var model = new MixtureModel(df, types);

        int maxIterations = 100;
        model.ExpectationMaximization(out double[] parameters, out double[,] covariance, out int iterations, maxIterations);

        Assert.IsTrue(iterations <= maxIterations);
    }

    /// <summary>Verifies that expectation maximization returns covariance.</summary>
    [TestMethod]
    public void Test_ExpectationMaximization_ReturnsCovariance()
    {
        var df = CreateSampleDataFrame();
        var types = new List<UnivariateDistributionType>
        {
            UnivariateDistributionType.Normal,
            UnivariateDistributionType.Normal
        };
        var model = new MixtureModel(df, types);

        model.ExpectationMaximization(out double[] parameters, out double[,] covariance, out int iterations);

        Assert.AreEqual(parameters.Length, covariance.GetLength(0));
        Assert.AreEqual(parameters.Length, covariance.GetLength(1));
    }

    /// <summary>Verifies that expectation maximization weights sum to one.</summary>
    [TestMethod]
    public void Test_ExpectationMaximization_WeightsSumToOne()
    {
        var df = CreateSampleDataFrame();
        var types = new List<UnivariateDistributionType>
        {
            UnivariateDistributionType.Normal,
            UnivariateDistributionType.Normal
        };
        var model = new MixtureModel(df, types);

        model.ExpectationMaximization(out double[] parameters, out double[,] covariance, out int iterations);

        // First two parameters are weights
        double weightSum = parameters[0] + parameters[1];
        Assert.AreEqual(1.0, weightSum, 0.01); // EM may not be perfectly converged
    }

    #endregion

    #region Clone Tests

    /// <summary>Verifies that clone creates independent copy.</summary>
    [TestMethod]
    public void Test_Clone_CreatesIndependentCopy()
    {
        var df = CreateSampleDataFrame();
        var types = new List<UnivariateDistributionType>
        {
            UnivariateDistributionType.Normal,
            UnivariateDistributionType.Gumbel
        };
        var original = new MixtureModel(df, types);
        original.IsZeroInflated = false;

        var clone = (MixtureModel)original.Clone();

        Assert.AreNotSame(original, clone);
        Assert.AreNotSame(original.Mixture, clone.Mixture);
    }

    /// <summary>Verifies that clone preserves is zero inflated for .</summary>
    [TestMethod]
    public void Test_Clone_PreservesIsZeroInflated()
    {
        var df = CreateZeroInflatedDataFrame();
        var types = new List<UnivariateDistributionType> { UnivariateDistributionType.Normal };
        var original = new MixtureModel(df, types, isZeroInflated: true);

        var clone = (MixtureModel)original.Clone();

        Assert.AreEqual(original.IsZeroInflated, clone.IsZeroInflated);
    }

    /// <summary>Verifies that clone preserves quantile priors for .</summary>
    [TestMethod]
    public void Test_Clone_PreservesQuantilePriors()
    {
        var df = CreateSampleDataFrame();
        var types = new List<UnivariateDistributionType> { UnivariateDistributionType.Normal };
        var original = new MixtureModel(df, types);
        original.EnableQuantilePriors = true;
        original.SetDefaultQuantilePriors();

        var clone = (MixtureModel)original.Clone();

        Assert.AreEqual(original.QuantilePriors.Count, clone.QuantilePriors.Count);
    }

    /// <summary>Verifies that clone parameters are independent.</summary>
    [TestMethod]
    public void Test_Clone_ParametersAreIndependent()
    {
        var df = CreateSampleDataFrame();
        var types = new List<UnivariateDistributionType> { UnivariateDistributionType.Normal };
        var original = new MixtureModel(df, types);
        double originalValue = original.Parameters[0].Value;

        var clone = (MixtureModel)original.Clone();
        original.Parameters[0].Value = 99999;

        Assert.AreEqual(originalValue, clone.Parameters[0].Value);
    }

    #endregion

    #region Serialization Tests

    /// <summary>Verifies that to X element contains mixture model element.</summary>
    [TestMethod]
    public void Test_ToXElement_ContainsMixtureModelElement()
    {
        var df = CreateSampleDataFrame();
        var types = new List<UnivariateDistributionType>
        {
            UnivariateDistributionType.Normal,
            UnivariateDistributionType.Gumbel
        };
        var model = new MixtureModel(df, types);

        var xElement = model.ToXElement();

        Assert.AreEqual("MixtureModel", xElement.Name.LocalName);
    }

    /// <summary>Verifies that to X element contains is zero inflated attribute.</summary>
    [TestMethod]
    public void Test_ToXElement_ContainsIsZeroInflatedAttribute()
    {
        var df = CreateZeroInflatedDataFrame();
        var types = new List<UnivariateDistributionType> { UnivariateDistributionType.Normal };
        var model = new MixtureModel(df, types, isZeroInflated: true);

        var xElement = model.ToXElement();

        Assert.IsNotNull(xElement.Attribute("IsZeroInflated"));
        Assert.AreEqual("True", xElement.Attribute("IsZeroInflated")?.Value);
    }

    /// <summary>Verifies that to X element contains distribution element.</summary>
    [TestMethod]
    public void Test_ToXElement_ContainsDistributionElement()
    {
        var df = CreateSampleDataFrame();
        var types = new List<UnivariateDistributionType> { UnivariateDistributionType.Normal };
        var model = new MixtureModel(df, types);

        var xElement = model.ToXElement();

        Assert.IsNotNull(xElement.Element("Distribution"));
    }

    /// <summary>Verifies that round trip preserves all properties for .</summary>
    [TestMethod]
    public void Test_RoundTrip_PreservesAllProperties()
    {
        var df = CreateSampleDataFrame();
        var types = new List<UnivariateDistributionType>
        {
            UnivariateDistributionType.Normal,
            UnivariateDistributionType.Gumbel
        };
        var original = new MixtureModel(df, types);
        original.EnableQuantilePriors = true;
        original.SetDefaultQuantilePriors();

        var xElement = original.ToXElement();
        var restored = new MixtureModel(df, xElement);

        Assert.AreEqual(original.NumberOfParameters, restored.NumberOfParameters);
        Assert.AreEqual(original.IsZeroInflated, restored.IsZeroInflated);
        Assert.AreEqual(original.EnableQuantilePriors, restored.EnableQuantilePriors);
    }

    /// <summary>Verifies that round trip preserves parameter values for .</summary>
    [TestMethod]
    public void Test_RoundTrip_PreservesParameterValues()
    {
        var df = CreateSampleDataFrame();
        var types = new List<UnivariateDistributionType> { UnivariateDistributionType.Normal };
        var original = new MixtureModel(df, types);
        original.Parameters[0].Value = 12345.67;

        var xElement = original.ToXElement();
        var restored = new MixtureModel(df, xElement);

        Assert.AreEqual(12345.67, restored.Parameters[0].Value, 1e-6);
    }

    #endregion

    #region Validation Tests

    /// <summary>Verifies that validate returns true when valid model.</summary>
    [TestMethod]
    public void Test_Validate_ValidModel_ReturnsTrue()
    {
        var df = CreateSampleDataFrame();
        var types = new List<UnivariateDistributionType>
        {
            UnivariateDistributionType.Normal,
            UnivariateDistributionType.Gumbel
        };
        var model = new MixtureModel(df, types);

        var (isValid, messages) = model.Validate();

        Assert.IsTrue(isValid, $"Validation failed: {string.Join(", ", messages)}");
    }

    /// <summary>Verifies that validate returns false when null data frame.</summary>
    [TestMethod]
    public void Test_Validate_NullDataFrame_ReturnsFalse()
    {
        var model = new MixtureModel();
        model.DataFrame = null!;

        var (isValid, messages) = model.Validate();

        Assert.IsFalse(isValid);
        Assert.IsTrue(messages.Any(m => m.Contains("Data frame")));
    }

    /// <summary>Verifies that validate returns false when null mixture.</summary>
    [TestMethod]
    public void Test_Validate_NullMixture_ReturnsFalse()
    {
        var df = CreateSampleDataFrame();
        var model = new MixtureModel();
        model.DataFrame = df;
        model.Mixture = null;

        var (isValid, messages) = model.Validate();

        Assert.IsFalse(isValid);
        Assert.IsTrue(messages.Any(m => m.Contains("Mixture")));
    }

    /// <summary>Verifies that validate returns false when too many components.</summary>
    [TestMethod]
    public void Test_Validate_TooManyComponents_ReturnsFalse()
    {
        var df = CreateSampleDataFrame();
        var model = new MixtureModel();
        model.DataFrame = df;

        // Create mixture with 4 components (exceeds limit of 3)
        var weights = new double[] { 0.25, 0.25, 0.25, 0.25 };
        var dists = new UnivariateDistributionBase[]
        {
            new Normal(100, 10),
            new Normal(200, 20),
            new Normal(300, 30),
            new Normal(400, 40)
        };
        model.Mixture = new Mixture(weights, dists);

        var (isValid, messages) = model.Validate();

        Assert.IsFalse(isValid);
        Assert.IsTrue(messages.Any(m => m.Contains("1 to 3")));
    }

    /// <summary>Verifies that validate returns false when log distribution with non positive data.</summary>
    [TestMethod]
    public void Test_Validate_LogDistributionWithNonPositiveData_ReturnsFalse()
    {
        var df = CreateZeroInflatedDataFrame();
        var types = new List<UnivariateDistributionType> { UnivariateDistributionType.LogNormal };
        var model = new MixtureModel(df, types, isZeroInflated: false);

        var (isValid, messages) = model.Validate();

        Assert.IsFalse(isValid);
        Assert.IsTrue(messages.Any(m => m.Contains("log-based") || m.Contains("non-positive")));
    }

    /// <summary>Verifies that validate returns true when log distribution with zero inflation.</summary>
    [TestMethod]
    public void Test_Validate_LogDistributionWithZeroInflation_ReturnsTrue()
    {
        var df = CreateZeroInflatedDataFrame();
        var types = new List<UnivariateDistributionType> { UnivariateDistributionType.LogNormal };
        var model = new MixtureModel(df, types, isZeroInflated: true);

        var (isValid, messages) = model.Validate();

        // Zero inflation handles the non-positive values
        Assert.IsTrue(isValid, $"Validation failed: {string.Join(", ", messages)}");
    }

    #endregion

    #region Engineering Application Tests

    /// <summary>Verifies that mixture model bimodal flood distribution.</summary>
    [TestMethod]
    public void Test_MixtureModel_BimodalFloodDistribution()
    {
        // Typical scenario: snowmelt floods (lower, more frequent)
        // and rainfall floods (higher, less frequent)
        var df = CreateBimodalDataFrame();
        var types = new List<UnivariateDistributionType>
        {
            UnivariateDistributionType.Normal,  // Snowmelt population
            UnivariateDistributionType.Gumbel   // Rainfall population
        };
        var model = new MixtureModel(df, types);

        var (isValid, _) = model.Validate();
        Assert.IsTrue(isValid);

        var parameters = model.Parameters.Select(p => p.Value).ToArray();
        double logLH = model.LogLikelihood(parameters);
        Assert.IsFalse(double.IsNegativeInfinity(logLH));
    }

    /// <summary>Verifies that mixture model intermittent stream.</summary>
    [TestMethod]
    public void Test_MixtureModel_IntermittentStream()
    {
        // Intermittent stream with many zero-flow years
        var df = CreateZeroInflatedDataFrame();
        var types = new List<UnivariateDistributionType>
        {
            UnivariateDistributionType.GammaDistribution
        };
        var model = new MixtureModel(df, types, isZeroInflated: true);

        var (isValid, _) = model.Validate();
        Assert.IsTrue(isValid);

        // Zero weight should be significant for intermittent stream
        Assert.IsTrue(model.Mixture!.ZeroWeight > 0.3);
    }

    /// <summary>Verifies that mixture model seasonal flood populations.</summary>
    [TestMethod]
    public void Test_MixtureModel_SeasonalFloodPopulations()
    {
        // Spring snowmelt, summer thunderstorm, fall tropical system
        var df = CreateBimodalDataFrame();
        var types = new List<UnivariateDistributionType>
        {
            UnivariateDistributionType.Normal,
            UnivariateDistributionType.Gumbel,
            UnivariateDistributionType.GeneralizedExtremeValue
        };
        var model = new MixtureModel(df, types);

        Assert.AreEqual(3, model.Mixture!.Distributions.Length);

        // Weights should sum to 1
        double weightSum = model.Mixture!.Weights.Sum();
        Assert.AreEqual(1.0, weightSum, 1e-10);
    }

    #endregion

    #region Edge Cases

    /// <summary>Verifies that mixture model single data point.</summary>
    [TestMethod]
    public void Test_MixtureModel_SingleDataPoint()
    {
        var df = new DataFrame();
        df.ExactSeries = new ExactSeries(new List<ExactData>
        {
            new ExactData { Index = 2000, Value = 1000 }
        });
        var types = new List<UnivariateDistributionType> { UnivariateDistributionType.Normal };

        var model = new MixtureModel(df, types);

        // Should handle single data point
        Assert.IsNotNull(model.Mixture);
    }

    /// <summary>
    /// When input data is constant (zero-width sample range), the MixtureModel's
    /// auto-fit Uniform prior collapses to <c>Uniform(a, a)</c>, which
    /// <see cref="Numerics.Distributions.Uniform"/> correctly rejects with
    /// <see cref="ArgumentOutOfRangeException"/> during PDF evaluation. This is the
    /// intended contract: degenerate data surfaces as an exception rather than a
    /// silent NaN / −∞, so the caller cannot mistakenly proceed with meaningless
    /// posterior inference.
    /// </summary>
    /// <remarks>
    /// The test pins down the throw contract. If the contract changes (e.g., future
    /// work adds an upstream degeneracy guard in MixtureModel that returns
    /// <c>double.NegativeInfinity</c> instead), this test should be updated with the
    /// new expected behavior.
    /// </remarks>
    [TestMethod]
    public void Test_MixtureModel_AllSameValue_ThrowsOnDegenerateData()
    {
        var df = new DataFrame();
        var data = new List<ExactData>();
        for (int i = 0; i < 10; i++)
        {
            data.Add(new ExactData { Index = 2000 + i, Value = 500 });
        }
        df.ExactSeries = new ExactSeries(data);

        var types = new List<UnivariateDistributionType> { UnivariateDistributionType.Normal };
        var model = new MixtureModel(df, types);

        var parameters = model.Parameters.Select(p => p.Value).ToArray();

        // All-same data → auto-fit Uniform prior has min == max → Uniform.PDF throws.
        // This surfaces the data degeneracy instead of silently returning NaN / −∞.
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => model.LogLikelihood(parameters),
            "LogLikelihood should throw ArgumentOutOfRangeException on constant-value data.");
    }

    /// <summary>Verifies that mixture model large values.</summary>
    [TestMethod]
    public void Test_MixtureModel_LargeValues()
    {
        var df = new DataFrame();
        var data = new List<ExactData>();
        for (int i = 0; i < 10; i++)
        {
            data.Add(new ExactData { Index = 2000 + i, Value = 1e8 + i * 1e6 });
        }
        df.ExactSeries = new ExactSeries(data);

        var types = new List<UnivariateDistributionType> { UnivariateDistributionType.Normal };
        var model = new MixtureModel(df, types);

        var (isValid, _) = model.Validate();
        Assert.IsTrue(isValid);
    }

    /// <summary>Verifies that mixture model small values.</summary>
    [TestMethod]
    public void Test_MixtureModel_SmallValues()
    {
        var df = new DataFrame();
        var data = new List<ExactData>();
        for (int i = 0; i < 10; i++)
        {
            data.Add(new ExactData { Index = 2000 + i, Value = 0.001 + i * 0.0001 });
        }
        df.ExactSeries = new ExactSeries(data);

        var types = new List<UnivariateDistributionType> { UnivariateDistributionType.Normal };
        var model = new MixtureModel(df, types);

        var (isValid, _) = model.Validate();
        Assert.IsTrue(isValid);
    }

    #endregion

    #region Jeffreys Prior Tests

    /// <summary>Verifies that jeffreys prior affects prior log likelihood.</summary>
    [TestMethod]
    public void Test_JeffreysPrior_AffectsPriorLogLikelihood()
    {
        var df = CreateSampleDataFrame();
        var types = new List<UnivariateDistributionType> { UnivariateDistributionType.Normal };
        var model = new MixtureModel(df, types);

        var parameters = model.Parameters.Select(p => p.Value).ToArray();

        model.UseJeffreysRuleForScale = false;
        double priorNoJeffreys = model.PriorLogLikelihood(parameters);

        model.UseJeffreysRuleForScale = true;
        double priorWithJeffreys = model.PriorLogLikelihood(parameters);

        // Jeffreys prior adds -log(scale) term, so they should differ
        Assert.AreNotEqual(priorNoJeffreys, priorWithJeffreys);
    }

    #endregion
}
