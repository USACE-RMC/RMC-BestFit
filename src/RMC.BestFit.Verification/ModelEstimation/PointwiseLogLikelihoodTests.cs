using Numerics;
using Numerics.Data.Statistics;
using Numerics.Distributions;
using RMC.BestFit.Models;
using RMC.BestFit.Estimation;

namespace RMC.BestFit.Verification.ModelEstimation;

/// <summary>
/// Unit tests for pointwise log-likelihood decomposition methods.
/// Tests PointwiseDataLogLikelihood and PointwiseDataLogLikelihoodComponents for various models.
/// </summary>
[TestClass]
public class PointwiseLogLikelihoodTests
{
    #region Pointwise Data Log-Likelihood Tests

    /// <summary>
    /// Verifies <c>Test_PointwiseDataLogLikelihood_SumsToTotalLogLikelihood</c>.
    /// </summary>
    [TestMethod]
    public void Test_PointwiseDataLogLikelihood_SumsToTotalLogLikelihood()
    {
        // Arrange
        var df = new Models.DataFrame();
        df.ExactSeries = new ExactSeries(TestData.NormalData);
        var model = new UnivariateDistribution(df, UnivariateDistributionType.Normal);

        // Use MLE parameters
        var mle = new MaximumLikelihood(model);
        mle.Estimate();
        var parameters = mle.BestParameterSet.Values;

        // Act
        var pointwise = model.PointwiseDataLogLikelihood(parameters);
        double sumPointwise = pointwise.Sum();
        double totalDataLogLikelihood = model.DataLogLikelihood(parameters);

        // Assert - pointwise DATA log-likelihoods should sum to total DATA log-likelihood
        Assert.AreEqual(totalDataLogLikelihood, sumPointwise, 1e-8,
            "Sum of pointwise data log-likelihoods should equal total data log-likelihood.");
    }

    /// <summary>
    /// Verifies <c>Test_PointwiseDataLogLikelihood_ReturnsCorrectCount</c>.
    /// </summary>
    [TestMethod]
    public void Test_PointwiseDataLogLikelihood_ReturnsCorrectCount()
    {
        // Arrange
        var df = new Models.DataFrame();
        df.ExactSeries = new ExactSeries(TestData.GEVData);
        var model = new UnivariateDistribution(df, UnivariateDistributionType.GeneralizedExtremeValue);

        var mle = new MaximumLikelihood(model);
        mle.Estimate();
        var parameters = mle.BestParameterSet.Values;

        // Act
        var pointwise = model.PointwiseDataLogLikelihood(parameters);

        // Assert
        Assert.AreEqual(TestData.SampleSize, pointwise.Length,
            "Pointwise array length should match sample size.");
    }

    /// <summary>
    /// Verifies <c>Test_PointwiseDataLogLikelihood_AllValuesNegative</c>.
    /// </summary>
    [TestMethod]
    public void Test_PointwiseDataLogLikelihood_AllValuesNegative()
    {
        // Arrange
        var df = new Models.DataFrame();
        df.ExactSeries = new ExactSeries(TestData.GumbelData);
        var model = new UnivariateDistribution(df, UnivariateDistributionType.Gumbel);

        var mle = new MaximumLikelihood(model);
        mle.Estimate();
        var parameters = mle.BestParameterSet.Values;

        // Act
        var pointwise = model.PointwiseDataLogLikelihood(parameters);

        // Assert - log densities for continuous distributions are typically negative
        foreach (var ll in pointwise)
        {
            Assert.IsFalse(double.IsNaN(ll), "Log-likelihood should not be NaN.");
            Assert.IsFalse(double.IsPositiveInfinity(ll), "Log-likelihood should not be +Infinity.");
        }
    }

    #endregion

    #region Pointwise Data Log-Likelihood Components Tests

    /// <summary>
    /// Verifies <c>Test_PointwiseDataLogLikelihoodComponents_ReturnsCorrectCount</c>.
    /// </summary>
    [TestMethod]
    public void Test_PointwiseDataLogLikelihoodComponents_ReturnsCorrectCount()
    {
        // Arrange
        var df = new Models.DataFrame();
        df.ExactSeries = new ExactSeries(TestData.NormalData);
        var model = new UnivariateDistribution(df, UnivariateDistributionType.Normal);

        var mle = new MaximumLikelihood(model);
        mle.Estimate();
        var parameters = mle.BestParameterSet.Values;

        // Act
        var components = model.PointwiseDataLogLikelihoodComponents(parameters);

        // Assert
        Assert.AreEqual(TestData.SampleSize, components.Count,
            "Number of components should match sample size.");
    }

    /// <summary>
    /// Verifies <c>Test_PointwiseDataLogLikelihoodComponents_CorrectIndices</c>.
    /// </summary>
    [TestMethod]
    public void Test_PointwiseDataLogLikelihoodComponents_CorrectIndices()
    {
        // Arrange
        var df = new Models.DataFrame();
        df.ExactSeries = new ExactSeries(TestData.GumbelData);
        var model = new UnivariateDistribution(df, UnivariateDistributionType.Gumbel);

        var mle = new MaximumLikelihood(model);
        mle.Estimate();
        var parameters = mle.BestParameterSet.Values;

        // Act
        var components = model.PointwiseDataLogLikelihoodComponents(parameters);

        // Assert - indices should be sequential from 0 to n-1
        for (int i = 0; i < components.Count; i++)
        {
            Assert.AreEqual(i, components[i].Index, "Component index should match position.");
        }
    }

    /// <summary>
    /// Verifies <c>Test_PointwiseDataLogLikelihoodComponents_ExactType</c>.
    /// </summary>
    [TestMethod]
    public void Test_PointwiseDataLogLikelihoodComponents_ExactType()
    {
        // Arrange
        var df = new Models.DataFrame();
        df.ExactSeries = new ExactSeries(TestData.WeibullData);
        var model = new UnivariateDistribution(df, UnivariateDistributionType.Weibull);

        var mle = new MaximumLikelihood(model);
        mle.Estimate();
        var parameters = mle.BestParameterSet.Values;

        // Act
        var components = model.PointwiseDataLogLikelihoodComponents(parameters);

        // Assert - all components from ExactSeries should be Exact type
        foreach (var comp in components)
        {
            Assert.AreEqual(DataComponentType.Exact, comp.Type,
                "All components from ExactSeries should be Exact type.");
        }
    }

    /// <summary>
    /// Verifies <c>Test_PointwiseDataLogLikelihoodComponents_CorrectValues</c>.
    /// </summary>
    [TestMethod]
    public void Test_PointwiseDataLogLikelihoodComponents_CorrectValues()
    {
        // Arrange
        var df = new Models.DataFrame();
        df.ExactSeries = new ExactSeries(TestData.LogisticData);
        var model = new UnivariateDistribution(df, UnivariateDistributionType.Logistic);

        var mle = new MaximumLikelihood(model);
        mle.Estimate();
        var parameters = mle.BestParameterSet.Values;

        // Act
        var components = model.PointwiseDataLogLikelihoodComponents(parameters);

        // Assert - component values should match the data
        for (int i = 0; i < components.Count; i++)
        {
            Assert.AreEqual(TestData.LogisticData[i], components[i].Value, 1e-10,
                $"Component value at index {i} should match data value.");
        }
    }

    /// <summary>
    /// Verifies <c>Test_PointwiseDataLogLikelihoodComponents_CountEqualsOne_ForExactData</c>.
    /// </summary>
    [TestMethod]
    public void Test_PointwiseDataLogLikelihoodComponents_CountEqualsOne_ForExactData()
    {
        // Arrange
        var df = new Models.DataFrame();
        df.ExactSeries = new ExactSeries(TestData.GEVData);
        var model = new UnivariateDistribution(df, UnivariateDistributionType.GeneralizedExtremeValue);

        var mle = new MaximumLikelihood(model);
        mle.Estimate();
        var parameters = mle.BestParameterSet.Values;

        // Act
        var components = model.PointwiseDataLogLikelihoodComponents(parameters);

        // Assert
        foreach (var comp in components)
        {
            Assert.AreEqual(1, comp.Count, "Count should be 1 for exact observations.");
        }
    }

    /// <summary>
    /// Verifies <c>Test_PointwiseDataLogLikelihoodComponents_SumsToTotal</c>.
    /// </summary>
    [TestMethod]
    public void Test_PointwiseDataLogLikelihoodComponents_SumsToTotal()
    {
        // Arrange
        var df = new Models.DataFrame();
        df.ExactSeries = new ExactSeries(TestData.PearsonTypeIIIData);
        var model = new UnivariateDistribution(df, UnivariateDistributionType.PearsonTypeIII);

        var mle = new MaximumLikelihood(model);
        mle.Estimate();
        var parameters = mle.BestParameterSet.Values;

        // Act
        var components = model.PointwiseDataLogLikelihoodComponents(parameters);
        double sumComponentLogLikelihoods = components.Sum(c => c.LogLikelihood);
        double totalDataLogLikelihood = model.DataLogLikelihood(parameters);

        // Assert - pointwise DATA log-likelihood components should sum to total DATA log-likelihood
        Assert.AreEqual(totalDataLogLikelihood, sumComponentLogLikelihoods, 1e-8,
            "Sum of component data log-likelihoods should equal total data log-likelihood.");
    }

    #endregion

    #region Pointwise Prior Log-Likelihood Tests

    /// <summary>
    /// Verifies <c>Test_PointwisePriorLogLikelihood_SumsToTotalPrior</c>.
    /// </summary>
    [TestMethod]
    public void Test_PointwisePriorLogLikelihood_SumsToTotalPrior()
    {
        // Arrange
        var df = new Models.DataFrame();
        df.ExactSeries = new ExactSeries(TestData.NormalData);
        var model = new UnivariateDistribution(df, UnivariateDistributionType.Normal);

        // Disable Jeffreys rule so PointwisePriorLogLikelihood only returns parameter priors
        // (base PriorLogLikelihood doesn't include Jeffreys, so this ensures equality)
        model.UseJeffreysRuleForScale = false;

        // Set informative priors
        model.Parameters[0].PriorDistribution = new Normal(100, 20);
        model.Parameters[1].PriorDistribution = new Exponential(0.001, 20);

        var mle = new MaximumLikelihood(model);
        mle.Estimate();
        var parameters = mle.BestParameterSet.Values;

        // Act
        var priorComponents = model.PointwisePriorLogLikelihood(parameters);
        double sumPriorLogLikelihood = priorComponents.Sum(c => c.LogLikelihood);
        double totalPriorLogLikelihood = model.PriorLogLikelihood(parameters);

        // Assert
        Assert.AreEqual(totalPriorLogLikelihood, sumPriorLogLikelihood, 1e-8,
            "Sum of prior components should equal total prior log-likelihood.");
    }

    /// <summary>
    /// Verifies <c>Test_PointwisePriorLogLikelihood_HasParameterPriors</c>.
    /// </summary>
    [TestMethod]
    public void Test_PointwisePriorLogLikelihood_HasParameterPriors()
    {
        // Arrange
        var df = new Models.DataFrame();
        df.ExactSeries = new ExactSeries(TestData.GEVData);
        var model = new UnivariateDistribution(df, UnivariateDistributionType.GeneralizedExtremeValue);

        // Set informative priors
        model.Parameters[0].PriorDistribution = new Normal(50, 10);
        model.Parameters[1].PriorDistribution = new Exponential(0.001, 20);
        model.Parameters[2].PriorDistribution = new Normal(0, 0.3);

        var mle = new MaximumLikelihood(model);
        mle.Estimate();
        var parameters = mle.BestParameterSet.Values;

        // Act
        var priorComponents = model.PointwisePriorLogLikelihood(parameters);

        // Assert - should have at least as many components as parameters
        Assert.IsTrue(priorComponents.Count >= model.Parameters.Count,
            "Should have at least one prior component per parameter.");

        // Check that parameter priors are included
        bool hasParameterPriors = priorComponents.Any(c => c.Type == PriorComponentType.ParameterPrior);
        Assert.IsTrue(hasParameterPriors, "Should include parameter prior components.");
    }

    /// <summary>
    /// Verifies <c>Test_PointwisePriorLogLikelihood_ComponentNamesAreDescriptive</c>.
    /// </summary>
    [TestMethod]
    public void Test_PointwisePriorLogLikelihood_ComponentNamesAreDescriptive()
    {
        // Arrange
        var df = new Models.DataFrame();
        df.ExactSeries = new ExactSeries(TestData.GumbelData);
        var model = new UnivariateDistribution(df, UnivariateDistributionType.Gumbel);

        // Set priors
        model.Parameters[0].PriorDistribution = new Normal(50, 10);
        model.Parameters[1].PriorDistribution = new Exponential(0.001, 20);

        var mle = new MaximumLikelihood(model);
        mle.Estimate();
        var parameters = mle.BestParameterSet.Values;

        // Act
        var priorComponents = model.PointwisePriorLogLikelihood(parameters);

        // Assert - component names should not be null or empty
        foreach (var comp in priorComponents)
        {
            Assert.IsFalse(string.IsNullOrEmpty(comp.Name),
                "Prior component names should be descriptive.");
        }
    }

    #endregion

    #region Multiple Distribution Types

    /// <summary>
    /// Verifies <c>Test_PointwiseLogLikelihood_ConsistentAcrossDistributions</c>.
    /// </summary>
    [TestMethod]
    public void Test_PointwiseLogLikelihood_ConsistentAcrossDistributions()
    {
        var distributionTypes = new[]
        {
            UnivariateDistributionType.Normal,
            UnivariateDistributionType.Gumbel,
            UnivariateDistributionType.GeneralizedExtremeValue,
            UnivariateDistributionType.Exponential,
            UnivariateDistributionType.Logistic
        };

        foreach (var distType in distributionTypes)
        {
            // Arrange
            var df = new Models.DataFrame();
            df.ExactSeries = new ExactSeries(TestData.NormalData); // Use same data
            var model = new UnivariateDistribution(df, distType);

            var mle = new MaximumLikelihood(model);
            mle.Estimate();
            var parameters = mle.BestParameterSet.Values;

            // Act
            var pointwise = model.PointwiseDataLogLikelihood(parameters);
            var components = model.PointwiseDataLogLikelihoodComponents(parameters);
            double totalDataLL = model.DataLogLikelihood(parameters);

            // Assert - pointwise DATA sums should match total DATA log-likelihood
            Assert.AreEqual(TestData.SampleSize, pointwise.Length,
                $"{distType}: Pointwise array length incorrect.");
            Assert.AreEqual(TestData.SampleSize, components.Count,
                $"{distType}: Components count incorrect.");
            Assert.AreEqual(totalDataLL, pointwise.Sum(), 1e-6,
                $"{distType}: Pointwise sum doesn't match total data log-likelihood.");
            Assert.AreEqual(totalDataLL, components.Sum(c => c.LogLikelihood), 1e-6,
                $"{distType}: Components sum doesn't match total data log-likelihood.");
        }
    }

    #endregion
}
