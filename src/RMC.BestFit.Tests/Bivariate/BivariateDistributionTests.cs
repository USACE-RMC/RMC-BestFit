using Numerics.Distributions;
using Numerics.Distributions.Copulas;
using RMC.BestFit.Models;
using BestFitDataFrame = RMC.BestFit.Models.DataFrame;

namespace RMC.BestFit.Tests.Bivariate;

/// <summary>
/// Programmatic unit tests for the <c>BivariateDistribution</c> model class.
/// </summary>
/// <remarks>
/// Computational / estimation tests (MLE convergence, parameter recovery) live in
/// <c>RMC.BestFit.Verification</c>. This file holds the fast, configuration-only tests:
/// constructor, parameters, log-likelihood evaluation at fixed inputs, pointwise-LL
/// invariants, serialization, and validation.
/// </remarks>
[TestClass]
public class BivariateDistributionTests
{
    #region Inline test fixtures

    private const int FixtureSize = 200;

    /// <summary>
    /// Deterministic Normal(100, 15) fixture for the X marginal. Generated inline
    /// from a fixed RNG seed so the file doesn't depend on the Verification project's
    /// <c>TestData</c>, but the sample is large enough to exercise marginal-default
    /// initialization (≈ small samples make Gumbel default-fitting fail).
    /// </summary>
    private static readonly double[] InlineXData = new Normal(100.0, 15.0)
        .GenerateRandomValues(FixtureSize, 12345);

    /// <summary>
    /// Deterministic Gumbel(50, 15) fixture for the Y marginal.
    /// </summary>
    private static readonly double[] InlineYData = new Gumbel(50.0, 15.0)
        .GenerateRandomValues(FixtureSize, 67890);

    private static (UnivariateDistribution marginalX, UnivariateDistribution marginalY) CreateMarginals(int? count = null)
    {
        int n = count ?? FixtureSize;

        var dfX = new BestFitDataFrame { ExactSeries = new ExactSeries(InlineXData.Take(n).ToArray()) };
        var marginalX = new UnivariateDistribution(dfX, UnivariateDistributionType.Normal);

        var dfY = new BestFitDataFrame { ExactSeries = new ExactSeries(InlineYData.Take(n).ToArray()) };
        var marginalY = new UnivariateDistribution(dfY, UnivariateDistributionType.Gumbel);

        return (marginalX, marginalY);
    }

    /// <summary>
    /// Creates a Normal marginal with exact data at the supplied indexes.
    /// </summary>
    /// <param name="indexes">The exact-data indexes to assign to the fixture rows.</param>
    /// <param name="lowOutlierIndexes">Indexes that should be flagged as low outliers.</param>
    /// <returns>A configured Normal marginal backed by the generated exact data.</returns>
    private static UnivariateDistribution CreateIndexedNormalMarginal(
        IReadOnlyList<int> indexes,
        ISet<int>? lowOutlierIndexes = null)
    {
        var dataFrame = new BestFitDataFrame();
        for (int i = 0; i < indexes.Count; i++)
        {
            bool isLowOutlier = lowOutlierIndexes?.Contains(indexes[i]) == true;
            dataFrame.ExactSeries.Add(new ExactData(indexes[i], 100.0 + i, isLowOutlier: isLowOutlier));
        }

        dataFrame.CalculatePlottingPositions();
        var marginal = new UnivariateDistribution(dataFrame, UnivariateDistributionType.Normal);
        marginal.SetParameterValues([100.0, 10.0]);
        return marginal;
    }

    /// <summary>
    /// Creates a bivariate model with the marginals' parameters explicitly set to the
    /// values that generated the inline fixture. Copula parameters stay at the bivariate
    /// model's defaults. <c>BivariateDistribution.Parameters</c> only exposes the copula
    /// parameters; marginal parameters live on <c>BivariateDistribution.MarginalX</c>
    /// / <c>BivariateDistribution.MarginalY</c> directly.
    /// </summary>
    private static BivariateDistribution CreateConfiguredModel(CopulaType copulaType = CopulaType.Normal, int? count = null)
    {
        var (marginalX, marginalY) = CreateMarginals(count);

        // Marginal X: Normal(μ=100, σ=15)
        marginalX.SetParameterValues([100.0, 15.0]);
        // Marginal Y: Gumbel(ξ=50, α=15)
        marginalY.SetParameterValues([50.0, 15.0]);

        return new BivariateDistribution(marginalX, marginalY, copulaType);
    }

    #endregion

    #region Sample-data alignment counts

    /// <summary>
    /// Exact data with matching indexes report all observations as paired.
    /// </summary>
    [TestMethod]
    public void GetSampleDataAlignmentCounts_FullOverlap_ReturnsAllCounts()
    {
        var indexes = Enumerable.Range(0, 100).ToArray();
        var model = new BivariateDistribution(
            CreateIndexedNormalMarginal(indexes),
            CreateIndexedNormalMarginal(indexes),
            CopulaType.Normal);

        var counts = model.GetSampleDataAlignmentCounts();

        Assert.AreEqual(100, counts.XExactCount);
        Assert.AreEqual(100, counts.YExactCount);
        Assert.AreEqual(100, counts.PairedCount);
    }

    /// <summary>
    /// Equal-length exact data with staggered indexes report only matching indexes as paired.
    /// </summary>
    [TestMethod]
    public void GetSampleDataAlignmentCounts_PartialOverlap_ReturnsPairedCount()
    {
        var xIndexes = Enumerable.Range(0, 100).ToArray();
        var yIndexes = Enumerable.Range(60, 100).ToArray();
        var model = new BivariateDistribution(
            CreateIndexedNormalMarginal(xIndexes),
            CreateIndexedNormalMarginal(yIndexes),
            CopulaType.Normal);

        var counts = model.GetSampleDataAlignmentCounts();

        Assert.AreEqual(100, counts.XExactCount);
        Assert.AreEqual(100, counts.YExactCount);
        Assert.AreEqual(40, counts.PairedCount);
    }

    /// <summary>
    /// Low outliers are excluded from source counts and cannot contribute paired observations.
    /// </summary>
    [TestMethod]
    public void GetSampleDataAlignmentCounts_LowOutliers_AreExcluded()
    {
        var indexes = Enumerable.Range(0, 12).ToArray();
        var model = new BivariateDistribution(
            CreateIndexedNormalMarginal(indexes, new HashSet<int> { 0, 1 }),
            CreateIndexedNormalMarginal(indexes, new HashSet<int> { 2 }),
            CopulaType.Normal);

        var counts = model.GetSampleDataAlignmentCounts();

        Assert.AreEqual(10, counts.XExactCount);
        Assert.AreEqual(11, counts.YExactCount);
        Assert.AreEqual(9, counts.PairedCount);
    }

    #endregion

    #region Construction

    /// <summary>Verifies that constructor empty constructor.</summary>
    [TestMethod]
    public void Test_Constructor_EmptyConstructor()
    {
        var model = new BivariateDistribution();

        Assert.IsNotNull(model);
        Assert.IsNotNull(model.Copula);
    }

    /// <summary>Verifies that constructor with marginals.</summary>
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

    /// <summary>Verifies that constructor different copula types.</summary>
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
            var model = new BivariateDistribution(marginalX, marginalY, copulaType);
            Assert.IsNotNull(model.Copula, $"{copulaType}: Copula should not be null.");
        }
    }

    #endregion

    #region Parameters

    /// <summary>Verifies that parameters normal copula has one copula parameter.</summary>
    [TestMethod]
    public void Test_Parameters_NormalCopula_HasOneCopulaParameter()
    {
        var (marginalX, marginalY) = CreateMarginals();
        var model = new BivariateDistribution(marginalX, marginalY, CopulaType.Normal);

        // BivariateDistribution.Parameters exposes only the copula's parameters, not the
        // marginals'. Normal copula has a single dependency parameter.
        Assert.AreEqual(1, model.Parameters.Count,
            $"Normal copula should expose exactly one parameter; got {model.Parameters.Count}.");
        Assert.AreEqual("Dependency (θ)", model.Parameters[0].Name);
    }

    /// <summary>Verifies that parameters student t copula has two copula parameters.</summary>
    [TestMethod]
    public void Test_Parameters_StudentTCopula_HasTwoCopulaParameters()
    {
        var (marginalX, marginalY) = CreateMarginals();

        var model = new BivariateDistribution(marginalX, marginalY, CopulaType.StudentT);

        // Student-T copula has dependency + degrees-of-freedom.
        Assert.AreEqual(2, model.Parameters.Count);
        Assert.AreEqual("DegreesOfFreedom", model.Parameters[1].Name);
    }

    #endregion

    #region Log-likelihood at fixed parameters (no estimation)

    /// <summary>Verifies that log likelihood returns finite value.</summary>
    [TestMethod]
    public void Test_LogLikelihood_ReturnsFiniteValue()
    {
        // Use known-good marginal parameters so the LL is well-defined without estimation.
        var model = CreateConfiguredModel(CopulaType.Normal);

        var parameters = model.Parameters.Select(p => p.Value).ToArray();
        double ll = model.LogLikelihood(parameters);

        Assert.IsFalse(double.IsNaN(ll), "Log-likelihood should not be NaN.");
        Assert.IsFalse(double.IsInfinity(ll), "Log-likelihood should be finite.");
    }

    /// <summary>Verifies that log likelihood different copulas.</summary>
    [TestMethod]
    public void Test_LogLikelihood_DifferentCopulas()
    {
        var copulaTypes = new[] { CopulaType.Normal, CopulaType.Frank };

        foreach (var copulaType in copulaTypes)
        {
            var model = CreateConfiguredModel(copulaType);
            var parameters = model.Parameters.Select(p => p.Value).ToArray();
            double ll = model.LogLikelihood(parameters);

            Assert.IsFalse(double.IsNaN(ll), $"{copulaType}: Log-likelihood should not be NaN.");
        }
    }

    #endregion

    #region Pointwise log-likelihood invariants (H1 split — fast variant)

    /// <summary>
    /// H1 split — fast invariant test. Uses known-good marginal parameters (no MLE) so the
    /// sum-equals-total invariant is exercised without invoking estimation. The MLE-driven
    /// version of this test stays in Verification.
    /// </summary>
    [TestMethod]
    public void Test_PointwiseDataLogLikelihood_SumsToTotal_AtKnownParameters()
    {
        var model = CreateConfiguredModel(CopulaType.Normal);

        var parameters = model.Parameters.Select(p => p.Value).ToArray();

        var pointwise = model.PointwiseDataLogLikelihood(parameters);
        double sumPointwise = pointwise.Sum();
        double totalDataLL = model.DataLogLikelihood(parameters);

        Assert.AreEqual(totalDataLL, sumPointwise, 1e-6,
            "Sum of pointwise log-likelihoods must equal DataLogLikelihood at the same parameters.");
    }

    /// <summary>Verifies that pointwise data log likelihood components returns correct count.</summary>
    [TestMethod]
    public void Test_PointwiseDataLogLikelihoodComponents_ReturnsCorrectCount()
    {
        var model = CreateConfiguredModel(CopulaType.Normal);

        var parameters = model.Parameters.Select(p => p.Value).ToArray();
        var components = model.PointwiseDataLogLikelihoodComponents(parameters);

        Assert.AreEqual(FixtureSize, components.Count,
            "Should have one component per observation.");
    }

    #endregion

    #region Serialization

    /// <summary>Verifies that to X element contains required attributes.</summary>
    [TestMethod]
    public void Test_ToXElement_ContainsRequiredAttributes()
    {
        var (marginalX, marginalY) = CreateMarginals(20);
        var model = new BivariateDistribution(marginalX, marginalY, CopulaType.Normal);

        var xElement = model.ToXElement();

        Assert.IsNotNull(xElement);
        Assert.IsNotNull(xElement.Attribute("CopulaType"));
    }

    /// <summary>Verifies that from X element restores model.</summary>
    [TestMethod]
    public void Test_FromXElement_RestoresModel()
    {
        var (marginalX, marginalY) = CreateMarginals(20);
        var original = new BivariateDistribution(marginalX, marginalY, CopulaType.Normal);

        var xElement = original.ToXElement();
        var restored = new BivariateDistribution(marginalX, marginalY, xElement);

        Assert.AreEqual(original.Parameters.Count, restored.Parameters.Count);
    }

    #endregion

    #region Validation

    /// <summary>Verifies that validate returns true when valid model.</summary>
    [TestMethod]
    public void Test_Validate_ValidModel_ReturnsTrue()
    {
        var (marginalX, marginalY) = CreateMarginals();
        var model = new BivariateDistribution(marginalX, marginalY, CopulaType.Normal);

        var (isValid, messages) = model.Validate();

        Assert.IsTrue(isValid, $"Validation failed: {string.Join(", ", messages)}");
    }

    #endregion
}
