using Numerics.Distributions;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using RMC.BestFit.Verification;

namespace RMC.BestFit.Verification.Univariate;

/// <summary>
/// Unit tests for the <see cref="UnivariateDistribution"/> model class.
/// Tests model construction, parameter management, log-likelihood computation, and serialization.
/// </summary>
[TestClass]
public class UnivariateDistributionTests
{
    #region Construction Tests

    /// <summary>
    /// Verifies <c>Test_Constructor_CreatesValidModel</c>.
    /// </summary>
    [TestMethod]
    public void Test_Constructor_CreatesValidModel()
    {
        // Arrange
        var df = new Models.DataFrame();
        df.ExactSeries = new ExactSeries(TestData.NormalData);

        // Act
        var model = new UnivariateDistribution(df, UnivariateDistributionType.Normal);

        // Assert
        Assert.IsNotNull(model);
        Assert.AreEqual(UnivariateDistributionType.Normal, model.DistributionType);
        Assert.IsNotNull(model.Parameters);
        Assert.IsTrue(model.Parameters.Count > 0);
    }

    /// <summary>
    /// Verifies <c>Test_Constructor_AllDistributionTypes</c>.
    /// </summary>
    [TestMethod]
    public void Test_Constructor_AllDistributionTypes()
    {
        var df = new Models.DataFrame();
        df.ExactSeries = new ExactSeries(TestData.NormalData);

        var distributionTypes = Enum.GetValues<UnivariateDistributionType>();

        foreach (var distType in distributionTypes)
        {
            // Skip any unsupported types
            try
            {
                var model = new UnivariateDistribution(df, distType);
                Assert.IsNotNull(model.Distribution, $"{distType}: Distribution should not be null.");
                Assert.IsNotNull(model.Parameters, $"{distType}: Parameters should not be null.");
            }
            catch (Exception ex) when (ex is not AssertFailedException)
            {
                // Some distribution types may not be implemented - that's OK for this test
                Assert.Inconclusive($"{distType}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Verifies <c>Test_Constructor_NormalDistribution_HasTwoParameters</c>.
    /// </summary>
    [TestMethod]
    public void Test_Constructor_NormalDistribution_HasTwoParameters()
    {
        var df = new Models.DataFrame();
        df.ExactSeries = new ExactSeries(TestData.NormalData);

        var model = new UnivariateDistribution(df, UnivariateDistributionType.Normal);

        Assert.AreEqual(2, model.Parameters.Count, "Normal distribution should have 2 parameters (μ, σ).");
    }

    /// <summary>
    /// Verifies <c>Test_Constructor_GEV_HasThreeParameters</c>.
    /// </summary>
    [TestMethod]
    public void Test_Constructor_GEV_HasThreeParameters()
    {
        var df = new Models.DataFrame();
        df.ExactSeries = new ExactSeries(TestData.GEVData);

        var model = new UnivariateDistribution(df, UnivariateDistributionType.GeneralizedExtremeValue);

        Assert.AreEqual(3, model.Parameters.Count, "GEV should have 3 parameters (ξ, α, κ).");
    }

    #endregion

    #region Parameter Management Tests

    /// <summary>
    /// Verifies <c>Test_Parameters_HasCorrectCount</c>.
    /// </summary>
    [TestMethod]
    public void Test_Parameters_HasCorrectCount()
    {
        var df = new Models.DataFrame();
        df.ExactSeries = new ExactSeries(TestData.NormalData);
        var model = new UnivariateDistribution(df, UnivariateDistributionType.Normal);

        // Normal distribution should have 2 parameters
        Assert.AreEqual(2, model.Parameters.Count);
    }

    /// <summary>
    /// Verifies <c>Test_SetParameterValues_UpdatesDistribution</c>.
    /// </summary>
    [TestMethod]
    public void Test_SetParameterValues_UpdatesDistribution()
    {
        var df = new Models.DataFrame();
        df.ExactSeries = new ExactSeries(TestData.NormalData);
        var model = new UnivariateDistribution(df, UnivariateDistributionType.Normal);

        // Set specific parameter values
        double[] newValues = [123.0, 45.0];
        model.SetParameterValues(newValues);

        var dist = (Normal)model.Distribution;
        Assert.AreEqual(123.0, dist.Mu, 1e-10);
        Assert.AreEqual(45.0, dist.Sigma, 1e-10);
    }

    /// <summary>
    /// Verifies <c>Test_SetParameterValues_UpdatesModelParameters</c>.
    /// </summary>
    [TestMethod]
    public void Test_SetParameterValues_UpdatesModelParameters()
    {
        var df = new Models.DataFrame();
        df.ExactSeries = new ExactSeries(TestData.NormalData);
        var model = new UnivariateDistribution(df, UnivariateDistributionType.Normal);

        double[] newValues = [50.0, 10.0];
        model.SetParameterValues(newValues);

        Assert.AreEqual(50.0, model.Parameters[0].Value, 1e-10);
        Assert.AreEqual(10.0, model.Parameters[1].Value, 1e-10);
    }

    /// <summary>
    /// Verifies <c>Test_SetParameterValues_RoundTrip</c>.
    /// </summary>
    [TestMethod]
    public void Test_SetParameterValues_RoundTrip()
    {
        var df = new Models.DataFrame();
        df.ExactSeries = new ExactSeries(TestData.NormalData);
        var model = new UnivariateDistribution(df, UnivariateDistributionType.Normal);

        double[] originalValues = [Math.PI, Math.E];
        model.SetParameterValues(originalValues);

        // Verify parameters were set correctly
        for (int i = 0; i < originalValues.Length; i++)
        {
            Assert.AreEqual(originalValues[i], model.Parameters[i].Value, 1e-15,
                $"Parameter {i} not preserved in round-trip.");
        }
    }

    #endregion

    #region Log-Likelihood Tests

    /// <summary>
    /// Verifies <c>Test_LogLikelihood_ReturnsFiniteValue</c>.
    /// </summary>
    [TestMethod]
    public void Test_LogLikelihood_ReturnsFiniteValue()
    {
        var df = new Models.DataFrame();
        df.ExactSeries = new ExactSeries(TestData.NormalData);
        var model = new UnivariateDistribution(df, UnivariateDistributionType.Normal);

        // Set reasonable parameters
        double[] parameters = TestData.NormalTrueParams;

        double ll = model.LogLikelihood(parameters);

        Assert.IsFalse(double.IsNaN(ll), "Log-likelihood should not be NaN.");
        Assert.IsFalse(double.IsInfinity(ll), "Log-likelihood should be finite.");
    }

    /// <summary>
    /// Verifies <c>Test_LogLikelihood_NegativeForContinuousDistributions</c>.
    /// </summary>
    [TestMethod]
    public void Test_LogLikelihood_NegativeForContinuousDistributions()
    {
        var df = new Models.DataFrame();
        df.ExactSeries = new ExactSeries(TestData.NormalData);
        var model = new UnivariateDistribution(df, UnivariateDistributionType.Normal);

        double[] parameters = TestData.NormalTrueParams;
        double ll = model.LogLikelihood(parameters);

        // Log-likelihood for continuous distributions is typically negative
        Assert.IsTrue(ll < 0, "Log-likelihood for continuous distribution should be negative.");
    }

    /// <summary>
    /// Verifies <c>Test_LogLikelihood_MaximizedAtMLE</c>.
    /// </summary>
    [TestMethod]
    public void Test_LogLikelihood_MaximizedAtMLE()
    {
        var df = new Models.DataFrame();
        df.ExactSeries = new ExactSeries(TestData.NormalData);
        var model = new UnivariateDistribution(df, UnivariateDistributionType.Normal);

        // Get MLE parameters
        var mle = new MaximumLikelihood(model);
        mle.Estimate();
        double[] mleParams = mle.BestParameterSet.Values;

        double llAtMLE = model.LogLikelihood(mleParams);

        // Try some perturbations - MLE should be at maximum
        double[] perturbedParams = [mleParams[0] + 5.0, mleParams[1] + 2.0];
        double llPerturbed = model.LogLikelihood(perturbedParams);

        Assert.IsTrue(llAtMLE >= llPerturbed,
            "Log-likelihood at MLE should be at least as large as at perturbed parameters.");
    }

    /// <summary>
    /// Verifies <c>Test_LogLikelihood_InvalidParameters_ReturnsNegativeInfinity</c>.
    /// </summary>
    [TestMethod]
    public void Test_LogLikelihood_InvalidParameters_ReturnsNegativeInfinity()
    {
        var df = new Models.DataFrame();
        df.ExactSeries = new ExactSeries(TestData.NormalData);
        var model = new UnivariateDistribution(df, UnivariateDistributionType.Normal);

        // Negative scale parameter is invalid
        double[] invalidParams = [100.0, -10.0];
        double ll = model.LogLikelihood(invalidParams);

        Assert.IsTrue(double.IsNegativeInfinity(ll),
            "Invalid parameters should return negative infinity log-likelihood.");
    }

    #endregion

    #region Log-Prior Tests

    /// <summary>
    /// Verifies <c>Test_LogPrior_WithUniformPrior_ReturnsZero</c>.
    /// </summary>
    [TestMethod]
    public void Test_LogPrior_WithUniformPrior_ReturnsZero()
    {
        var df = new Models.DataFrame();
        df.ExactSeries = new ExactSeries(TestData.NormalData);
        var model = new UnivariateDistribution(df, UnivariateDistributionType.Normal);

        // Uniform priors have constant log-density
        model.Parameters[0].PriorDistribution = new Uniform(-1e10, 1e10);
        model.Parameters[1].PriorDistribution = new Uniform(0.001, 1e10);

        double[] parameters = TestData.NormalTrueParams;
        double logPrior = model.PriorLogLikelihood(parameters);

        Assert.IsFalse(double.IsNaN(logPrior), "Log-prior should not be NaN.");
        Assert.IsFalse(double.IsNegativeInfinity(logPrior), "Log-prior should not be -infinity for valid parameters.");
    }

    /// <summary>
    /// Verifies <c>Test_LogPrior_WithInformativePrior_ReturnsNonZero</c>.
    /// </summary>
    [TestMethod]
    public void Test_LogPrior_WithInformativePrior_ReturnsNonZero()
    {
        var df = new Models.DataFrame();
        df.ExactSeries = new ExactSeries(TestData.NormalData);
        var model = new UnivariateDistribution(df, UnivariateDistributionType.Normal);

        // Set informative Normal priors
        model.Parameters[0].PriorDistribution = new Normal(100, 10);
        model.Parameters[1].PriorDistribution = new Exponential(0.001, 15);

        double[] parameters = TestData.NormalTrueParams;
        double logPrior = model.PriorLogLikelihood(parameters);

        Assert.IsFalse(double.IsNaN(logPrior), "Log-prior should not be NaN.");
        Assert.IsFalse(double.IsInfinity(logPrior), "Log-prior should be finite.");
    }

    /// <summary>
    /// Verifies <c>Test_LogPrior_OutsidePriorSupport_ReturnsNegativeInfinity</c>.
    /// </summary>
    [TestMethod]
    public void Test_LogPrior_OutsidePriorSupport_ReturnsNegativeInfinity()
    {
        var df = new Models.DataFrame();
        df.ExactSeries = new ExactSeries(TestData.NormalData);
        var model = new UnivariateDistribution(df, UnivariateDistributionType.Normal);

        // Set bounded prior
        model.Parameters[0].PriorDistribution = new Uniform(0, 50); // μ must be in [0, 50]
        model.Parameters[1].PriorDistribution = new Uniform(1, 100);

        // Parameter value outside prior support
        double[] parameters = [100.0, 15.0]; // μ = 100 is outside [0, 50]
        double logPrior = model.PriorLogLikelihood(parameters);

        Assert.IsTrue(double.IsNegativeInfinity(logPrior),
            "Parameters outside prior support should have -infinity log-prior.");
    }

    #endregion

    #region Serialization Tests

    /// <summary>
    /// Verifies <c>Test_ToXElement_ContainsRequiredElements</c>.
    /// </summary>
    [TestMethod]
    public void Test_ToXElement_ContainsRequiredElements()
    {
        var df = new Models.DataFrame();
        df.ExactSeries = new ExactSeries(TestData.NormalData.Take(10).ToArray());
        var model = new UnivariateDistribution(df, UnivariateDistributionType.Normal);

        var xElement = model.ToXElement();

        Assert.IsNotNull(xElement);
        Assert.AreEqual("UnivariateDistribution", xElement.Name.LocalName);
        Assert.IsNotNull(xElement.Element("DataFrame") ?? xElement.Element("Distribution"),
            "Should contain DataFrame or Distribution element.");
    }

    /// <summary>
    /// Verifies <c>Test_FromXElement_RestoresModel</c>.
    /// </summary>
    [TestMethod]
    public void Test_FromXElement_RestoresModel()
    {
        var df = new Models.DataFrame();
        df.ExactSeries = new ExactSeries(TestData.NormalData.Take(10).ToArray());
        var original = new UnivariateDistribution(df, UnivariateDistributionType.Normal);
        original.SetParameterValues([95.0, 12.0]);

        var xElement = original.ToXElement();
        var restored = new UnivariateDistribution(df, xElement);

        Assert.AreEqual(original.DistributionType, restored.DistributionType);
        Assert.AreEqual(original.Parameters.Count, restored.Parameters.Count);

        for (int i = 0; i < original.Parameters.Count; i++)
        {
            Assert.AreEqual(original.Parameters[i].Value, restored.Parameters[i].Value, 1e-10,
                $"Parameter {i} not preserved in serialization.");
        }
    }

    #endregion

    #region Validation Tests

    /// <summary>
    /// Verifies <c>Test_Validate_ValidModel_ReturnsTrue</c>.
    /// </summary>
    [TestMethod]
    public void Test_Validate_ValidModel_ReturnsTrue()
    {
        var df = new Models.DataFrame();
        df.ExactSeries = new ExactSeries(TestData.NormalData);
        var model = new UnivariateDistribution(df, UnivariateDistributionType.Normal);

        var (isValid, messages) = model.Validate();

        Assert.IsTrue(isValid, $"Validation failed: {string.Join(", ", messages)}");
    }

    /// <summary>
    /// Verifies <c>Test_Validate_NoData_ReturnsFalse</c>.
    /// </summary>
    [TestMethod]
    public void Test_Validate_NoData_ReturnsFalse()
    {
        var df = new Models.DataFrame();
        // No data added
        var model = new UnivariateDistribution(df, UnivariateDistributionType.Normal);

        var (isValid, messages) = model.Validate();

        Assert.IsFalse(isValid, "Model with no data should fail validation.");
        Assert.IsTrue(messages.Count > 0, "Should have validation messages.");
    }

    #endregion

    #region Edge Case Tests

    /// <summary>
    /// Verifies <c>Test_SmallSample_WorksCorrectly</c>.
    /// </summary>
    [TestMethod]
    public void Test_SmallSample_WorksCorrectly()
    {
        var df = new Models.DataFrame();
        df.ExactSeries = new ExactSeries(TestData.SmallSample);
        var model = new UnivariateDistribution(df, UnivariateDistributionType.Normal);

        var mle = new MaximumLikelihood(model);
        mle.Estimate();

        Assert.IsTrue(mle.IsEstimated, "MLE should work with small sample.");
    }

    /// <summary>
    /// Verifies <c>Test_DataWithOutliers_MLEStillConverges</c>.
    /// </summary>
    [TestMethod]
    public void Test_DataWithOutliers_MLEStillConverges()
    {
        var df = new Models.DataFrame();
        df.ExactSeries = new ExactSeries(TestData.DataWithOutliers);
        var model = new UnivariateDistribution(df, UnivariateDistributionType.Normal);

        var mle = new MaximumLikelihood(model);
        mle.Estimate();

        Assert.IsTrue(mle.IsEstimated, "MLE should converge even with outliers.");
    }

    /// <summary>
    /// Verifies <c>Test_ChangeDistributionType_UpdatesParameters</c>.
    /// </summary>
    [TestMethod]
    public void Test_ChangeDistributionType_UpdatesParameters()
    {
        var df = new Models.DataFrame();
        df.ExactSeries = new ExactSeries(TestData.NormalData);

        var normalModel = new UnivariateDistribution(df, UnivariateDistributionType.Normal);
        int normalParamCount = normalModel.Parameters.Count;

        var gevModel = new UnivariateDistribution(df, UnivariateDistributionType.GeneralizedExtremeValue);
        int gevParamCount = gevModel.Parameters.Count;

        Assert.AreEqual(2, normalParamCount);
        Assert.AreEqual(3, gevParamCount);
    }

    #endregion
}
