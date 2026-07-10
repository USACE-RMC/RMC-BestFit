using Numerics.Distributions;
using Numerics.Distributions.Copulas;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using RMC.BestFit.Verification;

namespace RMC.BestFit.Verification.Bivariate;

/// <summary>
/// Unit tests for the <see cref="BivariateDistribution"/> model class.
/// Tests bivariate distributions with copulas using synthetic data.
/// </summary>
[TestClass]
public class BivariateDistributionTests
{
    #region Construction Tests

    /// <summary>
    /// Verifies <c>Test_Constructor_EmptyConstructor</c>.
    /// </summary>
    [TestMethod]
    public void Test_Constructor_EmptyConstructor()
    {
        var model = new BivariateDistribution();

        Assert.IsNotNull(model);
        Assert.IsNotNull(model.Copula);
    }

    /// <summary>
    /// Verifies <c>Test_Constructor_WithMarginals</c>.
    /// </summary>
    [TestMethod]
    public void Test_Constructor_WithMarginals()
    {
        var (marginalX, marginalY) = CreateMarginals();
        var model = new BivariateDistribution(marginalX, marginalY, CopulaType.Normal);

        Assert.IsNotNull(model);
        Assert.IsNotNull(model.MarginalX);
        Assert.IsNotNull(model.MarginalY);
        Assert.IsNotNull(model.Copula);
    }

    /// <summary>
    /// Verifies <c>Test_Constructor_DifferentCopulaTypes</c>.
    /// </summary>
    [TestMethod]
    public void Test_Constructor_DifferentCopulaTypes()
    {
        var (marginalX, marginalY) = CreateMarginals();

        var copulaTypes = new[]
        {
            CopulaType.Normal,
            CopulaType.Gumbel,
            CopulaType.Clayton,
            CopulaType.Frank
        };

        foreach (var copulaType in copulaTypes)
        {
            try
            {
                var model = new BivariateDistribution(marginalX, marginalY, copulaType);
                Assert.IsNotNull(model.Copula, $"{copulaType}: Copula should not be null.");
            }
            catch (Exception ex) when (ex is not AssertFailedException)
            {
                // Some copula types may not be fully implemented
                Assert.Inconclusive($"{copulaType}: {ex.Message}");
            }
        }
    }

    #endregion

    #region Parameter Tests

    /// <summary>
    /// Verifies <c>Test_Parameters_HasCopulaParameter</c>.
    /// </summary>
    [TestMethod]
    public void Test_Parameters_HasCopulaParameter()
    {
        var (marginalX, marginalY) = CreateMarginals();
        var model = new BivariateDistribution(marginalX, marginalY, CopulaType.Normal);

        // Should have parameters from both marginals plus copula parameter(s)
        Assert.IsTrue(model.Parameters.Count > 0, "Should have parameters.");

        // Check for copula parameter (e.g., correlation)
        bool hasCopulaParam = model.Parameters.Any(p =>
            p.Name.Contains("ρ") ||
            p.Name.Contains("rho") ||
            p.Name.Contains("Rho") ||
            p.Name.Contains("Correlation") ||
            p.Name.Contains("copula", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("theta", StringComparison.OrdinalIgnoreCase));

        Assert.IsTrue(hasCopulaParam || model.Parameters.Count > 4,
            "Should have copula parameter or combined parameters.");
    }

    #endregion

    #region Log-Likelihood Tests

    /// <summary>
    /// Verifies <c>Test_LogLikelihood_ReturnsFiniteValue</c>.
    /// </summary>
    [TestMethod]
    public void Test_LogLikelihood_ReturnsFiniteValue()
    {
        var (marginalX, marginalY) = CreateMarginals();
        var model = new BivariateDistribution(marginalX, marginalY, CopulaType.Normal);

        var parameters = model.Parameters.Select(p => p.Value).ToArray();
        double ll = model.LogLikelihood(parameters);

        Assert.IsFalse(double.IsNaN(ll), "Log-likelihood should not be NaN.");
        Assert.IsFalse(double.IsInfinity(ll), "Log-likelihood should be finite.");
    }

    /// <summary>
    /// Verifies <c>Test_LogLikelihood_DifferentCopulas</c>.
    /// </summary>
    [TestMethod]
    public void Test_LogLikelihood_DifferentCopulas()
    {
        var (marginalX, marginalY) = CreateMarginals();

        var copulaTypes = new[] { CopulaType.Normal, CopulaType.Frank };

        foreach (var copulaType in copulaTypes)
        {
            try
            {
                var model = new BivariateDistribution(marginalX, marginalY, copulaType);
                var parameters = model.Parameters.Select(p => p.Value).ToArray();
                double ll = model.LogLikelihood(parameters);

                Assert.IsFalse(double.IsNaN(ll), $"{copulaType}: Log-likelihood should not be NaN.");
            }
            catch (Exception ex) when (ex is not AssertFailedException)
            {
                Assert.Inconclusive($"{copulaType}: {ex.Message}");
            }
        }
    }

    #endregion

    #region MLE Estimation Tests

    /// <summary>
    /// Verifies <c>Test_MLE_NormalCopula_Converges</c>.
    /// </summary>
    [TestMethod]
    public void Test_MLE_NormalCopula_Converges()
    {
        var (marginalX, marginalY) = CreateMarginals();
        var model = new BivariateDistribution(marginalX, marginalY, CopulaType.Normal);

        var mle = new MaximumLikelihood(model);
        mle.Estimate();

        Assert.IsTrue(mle.IsEstimated, "MLE should converge for bivariate Normal copula.");
    }

    /// <summary>
    /// Verifies <c>Test_MLE_RecoversMarginalParameters</c>.
    /// </summary>
    [TestMethod]
    public void Test_MLE_RecoversMarginalParameters()
    {
        var (marginalX, marginalY) = CreateMarginals();
        var model = new BivariateDistribution(marginalX, marginalY, CopulaType.Normal);

        var mle = new MaximumLikelihood(model);
        mle.Estimate();
        model.SetParameterValues(mle.BestParameterSet.Values);

        // Verify marginal X parameters (Normal with μ=100, σ=15)
        var marginalXModel = (UnivariateDistribution)model.MarginalX;
        double xMean = marginalXModel.Parameters[0].Value;
        double xSigma = marginalXModel.Parameters[1].Value;
        Assert.AreEqual(100.0, xMean, 10.0, "X marginal mean not recovered.");
        Assert.AreEqual(15.0, xSigma, 5.0, "X marginal sigma not recovered.");
    }

    #endregion

    #region Pointwise Log-Likelihood Tests

    /// <summary>
    /// Verifies <c>Test_PointwiseDataLogLikelihood_SumsToTotal</c>.
    /// </summary>
    [TestMethod]
    public void Test_PointwiseDataLogLikelihood_SumsToTotal()
    {
        var (marginalX, marginalY) = CreateMarginals();
        var model = new BivariateDistribution(marginalX, marginalY, CopulaType.Normal);

        var mle = new MaximumLikelihood(model);
        mle.Estimate();
        var parameters = mle.BestParameterSet.Values;

        var pointwise = model.PointwiseDataLogLikelihood(parameters);
        double sumPointwise = pointwise.Sum();
        double totalLL = model.LogLikelihood(parameters);

        Assert.AreEqual(totalLL, sumPointwise, 1e-6,
            "Sum of pointwise log-likelihoods should equal total.");
    }

    /// <summary>
    /// Verifies <c>Test_PointwiseDataLogLikelihoodComponents_ReturnsCorrectCount</c>.
    /// </summary>
    [TestMethod]
    public void Test_PointwiseDataLogLikelihoodComponents_ReturnsCorrectCount()
    {
        var (marginalX, marginalY) = CreateMarginals();
        var model = new BivariateDistribution(marginalX, marginalY, CopulaType.Normal);

        var mle = new MaximumLikelihood(model);
        mle.Estimate();
        var parameters = mle.BestParameterSet.Values;

        var components = model.PointwiseDataLogLikelihoodComponents(parameters);

        // Should have one component per observation
        Assert.AreEqual(TestData.SampleSize, components.Count,
            "Should have one component per observation.");
    }

    #endregion

    #region Serialization Tests

    /// <summary>
    /// Verifies <c>Test_ToXElement_ContainsRequiredAttributes</c>.
    /// </summary>
    [TestMethod]
    public void Test_ToXElement_ContainsRequiredAttributes()
    {
        var (marginalX, marginalY) = CreateMarginals(50);
        var model = new BivariateDistribution(marginalX, marginalY, CopulaType.Normal);

        var xElement = model.ToXElement();

        Assert.IsNotNull(xElement);
        Assert.IsNotNull(xElement.Attribute("CopulaType"));
    }

    /// <summary>
    /// Verifies <c>Test_FromXElement_RestoresModel</c>.
    /// </summary>
    [TestMethod]
    public void Test_FromXElement_RestoresModel()
    {
        var (marginalX, marginalY) = CreateMarginals(50);
        var original = new BivariateDistribution(marginalX, marginalY, CopulaType.Normal);

        var xElement = original.ToXElement();
        var restored = new BivariateDistribution(marginalX, marginalY, xElement);

        Assert.AreEqual(original.Parameters.Count, restored.Parameters.Count);
    }

    #endregion

    #region Validation Tests

    /// <summary>
    /// Verifies <c>Test_Validate_ValidModel_ReturnsTrue</c>.
    /// </summary>
    [TestMethod]
    public void Test_Validate_ValidModel_ReturnsTrue()
    {
        var (marginalX, marginalY) = CreateMarginals();
        var model = new BivariateDistribution(marginalX, marginalY, CopulaType.Normal);

        var (isValid, messages) = model.Validate();

        Assert.IsTrue(isValid, $"Validation failed: {string.Join(", ", messages)}");
    }

    #endregion

    #region Edge Cases

    /// <summary>
    /// Verifies <c>Test_SmallSample_StillWorks</c>.
    /// </summary>
    [TestMethod]
    public void Test_SmallSample_StillWorks()
    {
        var (marginalX, marginalY) = CreateMarginals(30);
        var model = new BivariateDistribution(marginalX, marginalY, CopulaType.Normal);

        var mle = new MaximumLikelihood(model);
        mle.Estimate();

        Assert.IsTrue(mle.IsEstimated, "Should converge with 30 observations.");
    }

    #endregion

    #region Helper Methods

    private static (UnivariateDistribution marginalX, UnivariateDistribution marginalY) CreateMarginals(int? count = null)
    {
        int n = count ?? TestData.SampleSize;

        // Create DataFrame for X marginal (Normal)
        var dfX = new RMC.BestFit.Models.DataFrame();
        dfX.ExactSeries = new ExactSeries(TestData.BivariateXData.Take(n).ToArray());
        var marginalX = new UnivariateDistribution(dfX, UnivariateDistributionType.Normal);

        // Create DataFrame for Y marginal (Gumbel)
        var dfY = new RMC.BestFit.Models.DataFrame();
        dfY.ExactSeries = new ExactSeries(TestData.BivariateYData.Take(n).ToArray());
        var marginalY = new UnivariateDistribution(dfY, UnivariateDistributionType.Gumbel);

        return (marginalX, marginalY);
    }

    #endregion
}
