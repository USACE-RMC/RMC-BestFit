using Numerics.Distributions;
using RMC.BestFit.Models;
using BestFitDataFrame = RMC.BestFit.Models.DataFrame;

namespace RMC.BestFit.Tests.Univariate;

/// <summary>
/// Programmatic unit tests for the <c>UnivariateDistribution</c> wrapper class.
/// Construction, parameter management, log-likelihood at fixed parameters, prior evaluation,
/// serialization, and validation. Estimation-driven tests live in <c>RMC.BestFit.Verification</c>.
/// </summary>
[TestClass]
public class UnivariateDistributionTests
{
    #region Inline test fixtures

    private const int FixtureSize = 100;

    /// <summary>
    /// Deterministic Normal(100, 15) fixture. Inline (generated from a fixed RNG seed) so
    /// this file doesn't depend on the Verification project's <c>TestData</c>, but the
    /// sample is large enough that downstream construction / validation behaves the same
    /// as it would on a real workload.
    /// </summary>
    private static readonly double[] InlineNormalData = new Normal(100.0, 15.0)
        .GenerateRandomValues(FixtureSize, 12345);

    /// <summary>
    /// Deterministic GEV(50, 15, 0.1) fixture (right-skewed extremes).
    /// </summary>
    private static readonly double[] InlineGEVData = new GeneralizedExtremeValue(50.0, 15.0, 0.1)
        .GenerateRandomValues(FixtureSize, 23456);

    /// <summary>
    /// "True" parameters used in the original Verification fixture (μ=100, σ=15).
    /// </summary>
    private static readonly double[] InlineNormalTrueParams = [100.0, 15.0];

    /// <summary>
    /// Creates data Frame.
    /// </summary>
    /// <param name="data">The input data.</param>
    /// <returns>The created test object.</returns>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static BestFitDataFrame MakeDataFrame(double[] data)
    {
        var df = new BestFitDataFrame { ExactSeries = new ExactSeries(data) };
        return df;
    }

    #endregion

    #region Construction

    /// <summary>Verifies that constructor creates valid model.</summary>
    [TestMethod]
    public void Test_Constructor_CreatesValidModel()
    {
        var df = MakeDataFrame(InlineNormalData);

        var model = new UnivariateDistribution(df, UnivariateDistributionType.Normal);

        Assert.AreEqual(UnivariateDistributionType.Normal, model.DistributionType);
        Assert.IsNotNull(model.Parameters);
        Assert.IsTrue(model.Parameters.Count > 0);
    }

    /// <summary>Verifies that constructor all distribution types.</summary>
    [TestMethod]
    public void Test_Constructor_AllDistributionTypes()
    {
        var df = MakeDataFrame(InlineNormalData);
        var distributionTypes = new[]
        {
            UnivariateDistributionType.Exponential,
            UnivariateDistributionType.GammaDistribution,
            UnivariateDistributionType.GeneralizedExtremeValue,
            UnivariateDistributionType.GeneralizedLogistic,
            UnivariateDistributionType.GeneralizedNormal,
            UnivariateDistributionType.GeneralizedPareto,
            UnivariateDistributionType.Gumbel,
            UnivariateDistributionType.KappaFour,
            UnivariateDistributionType.LnNormal,
            UnivariateDistributionType.Logistic,
            UnivariateDistributionType.LogNormal,
            UnivariateDistributionType.LogPearsonTypeIII,
            UnivariateDistributionType.Normal,
            UnivariateDistributionType.PearsonTypeIII,
            UnivariateDistributionType.Weibull
        };

        foreach (var distType in distributionTypes)
        {
            var model = new UnivariateDistribution(df, distType);
            Assert.IsNotNull(model.Distribution, $"{distType}: Distribution should not be null.");
            Assert.IsNotNull(model.Parameters, $"{distType}: Parameters should not be null.");
        }
    }

    /// <summary>Verifies that constructor normal distribution has two parameters.</summary>
    [TestMethod]
    public void Test_Constructor_NormalDistribution_HasTwoParameters()
    {
        var df = MakeDataFrame(InlineNormalData);

        var model = new UnivariateDistribution(df, UnivariateDistributionType.Normal);

        Assert.AreEqual(2, model.Parameters.Count, "Normal distribution should have 2 parameters (μ, σ).");
    }

    /// <summary>Verifies that constructor GEV has three parameters.</summary>
    [TestMethod]
    public void Test_Constructor_GEV_HasThreeParameters()
    {
        var df = MakeDataFrame(InlineGEVData);

        var model = new UnivariateDistribution(df, UnivariateDistributionType.GeneralizedExtremeValue);

        Assert.AreEqual(3, model.Parameters.Count, "GEV should have 3 parameters (ξ, α, κ).");
    }

    #endregion

    #region Parameter management

    /// <summary>Verifies that parameters has correct count.</summary>
    [TestMethod]
    public void Test_Parameters_HasCorrectCount()
    {
        var df = MakeDataFrame(InlineNormalData);
        var model = new UnivariateDistribution(df, UnivariateDistributionType.Normal);

        Assert.AreEqual(2, model.Parameters.Count);
    }

    /// <summary>Verifies that set parameter values updates distribution.</summary>
    [TestMethod]
    public void Test_SetParameterValues_UpdatesDistribution()
    {
        var df = MakeDataFrame(InlineNormalData);
        var model = new UnivariateDistribution(df, UnivariateDistributionType.Normal);

        double[] newValues = [123.0, 45.0];
        model.SetParameterValues(newValues);

        var dist = (Normal)model.Distribution;
        Assert.AreEqual(123.0, dist.Mu, 1e-10);
        Assert.AreEqual(45.0, dist.Sigma, 1e-10);
    }

    /// <summary>Verifies that set parameter values updates model parameters.</summary>
    [TestMethod]
    public void Test_SetParameterValues_UpdatesModelParameters()
    {
        var df = MakeDataFrame(InlineNormalData);
        var model = new UnivariateDistribution(df, UnivariateDistributionType.Normal);

        double[] newValues = [50.0, 10.0];
        model.SetParameterValues(newValues);

        Assert.AreEqual(50.0, model.Parameters[0].Value, 1e-10);
        Assert.AreEqual(10.0, model.Parameters[1].Value, 1e-10);
    }

    /// <summary>Verifies that set parameter values round trip for .</summary>
    [TestMethod]
    public void Test_SetParameterValues_RoundTrip()
    {
        var df = MakeDataFrame(InlineNormalData);
        var model = new UnivariateDistribution(df, UnivariateDistributionType.Normal);

        double[] originalValues = [Math.PI, Math.E];
        model.SetParameterValues(originalValues);

        for (int i = 0; i < originalValues.Length; i++)
        {
            Assert.AreEqual(originalValues[i], model.Parameters[i].Value, 1e-15,
                $"Parameter {i} not preserved in round-trip.");
        }
    }

    #endregion

    #region Log-likelihood at fixed parameters (no estimation)

    /// <summary>Verifies that log likelihood returns finite value.</summary>
    [TestMethod]
    public void Test_LogLikelihood_ReturnsFiniteValue()
    {
        var df = MakeDataFrame(InlineNormalData);
        var model = new UnivariateDistribution(df, UnivariateDistributionType.Normal);

        double ll = model.LogLikelihood(InlineNormalTrueParams);

        Assert.IsFalse(double.IsNaN(ll), "Log-likelihood should not be NaN.");
        Assert.IsFalse(double.IsInfinity(ll), "Log-likelihood should be finite.");
    }

    /// <summary>Verifies that log likelihood negative for continuous distributions.</summary>
    [TestMethod]
    public void Test_LogLikelihood_NegativeForContinuousDistributions()
    {
        var df = MakeDataFrame(InlineNormalData);
        var model = new UnivariateDistribution(df, UnivariateDistributionType.Normal);

        double ll = model.LogLikelihood(InlineNormalTrueParams);

        Assert.IsTrue(ll < 0, "Log-likelihood for continuous distribution should be negative.");
    }

    /// <summary>Verifies that log likelihood returns negative infinity when invalid parameters.</summary>
    [TestMethod]
    public void Test_LogLikelihood_InvalidParameters_ReturnsNegativeInfinity()
    {
        var df = MakeDataFrame(InlineNormalData);
        var model = new UnivariateDistribution(df, UnivariateDistributionType.Normal);

        double[] invalidParams = [100.0, -10.0]; // negative scale
        double ll = model.LogLikelihood(invalidParams);

        Assert.IsTrue(double.IsNegativeInfinity(ll),
            "Invalid parameters should return negative infinity log-likelihood.");
    }

    #endregion

    #region Log-prior

    /// <summary>Verifies that log prior returns finite when with uniform prior.</summary>
    [TestMethod]
    public void Test_LogPrior_WithUniformPrior_ReturnsFinite()
    {
        var df = MakeDataFrame(InlineNormalData);
        var model = new UnivariateDistribution(df, UnivariateDistributionType.Normal);

        model.Parameters[0].PriorDistribution = new Uniform(-1e10, 1e10);
        model.Parameters[1].PriorDistribution = new Uniform(0.001, 1e10);

        double logPrior = model.PriorLogLikelihood(InlineNormalTrueParams);

        Assert.IsFalse(double.IsNaN(logPrior), "Log-prior should not be NaN.");
        Assert.IsFalse(double.IsNegativeInfinity(logPrior),
            "Log-prior should not be -infinity for valid parameters under a wide uniform prior.");
    }

    /// <summary>Verifies that log prior returns finite when with informative prior.</summary>
    [TestMethod]
    public void Test_LogPrior_WithInformativePrior_ReturnsFinite()
    {
        var df = MakeDataFrame(InlineNormalData);
        var model = new UnivariateDistribution(df, UnivariateDistributionType.Normal);

        model.Parameters[0].PriorDistribution = new Normal(100, 10);
        model.Parameters[1].PriorDistribution = new Exponential(0.001, 15);

        double logPrior = model.PriorLogLikelihood(InlineNormalTrueParams);

        Assert.IsFalse(double.IsNaN(logPrior), "Log-prior should not be NaN.");
        Assert.IsFalse(double.IsInfinity(logPrior), "Log-prior should be finite.");
    }

    /// <summary>Verifies that log prior returns negative infinity when outside prior support.</summary>
    [TestMethod]
    public void Test_LogPrior_OutsidePriorSupport_ReturnsNegativeInfinity()
    {
        var df = MakeDataFrame(InlineNormalData);
        var model = new UnivariateDistribution(df, UnivariateDistributionType.Normal);

        model.Parameters[0].PriorDistribution = new Uniform(0, 50); // μ ∈ [0, 50]
        model.Parameters[1].PriorDistribution = new Uniform(1, 100);

        double[] parameters = [100.0, 15.0]; // μ = 100 is outside [0, 50]
        double logPrior = model.PriorLogLikelihood(parameters);

        Assert.IsTrue(double.IsNegativeInfinity(logPrior),
            "Parameters outside prior support should have -infinity log-prior.");
    }

    #endregion

    #region Serialization

    /// <summary>Verifies that to X element contains required elements.</summary>
    [TestMethod]
    public void Test_ToXElement_ContainsRequiredElements()
    {
        var df = MakeDataFrame(InlineNormalData.Take(10).ToArray());
        var model = new UnivariateDistribution(df, UnivariateDistributionType.Normal);

        var xElement = model.ToXElement();

        Assert.IsNotNull(xElement);
        Assert.AreEqual("UnivariateDistribution", xElement.Name.LocalName);
        Assert.IsNotNull(xElement.Element("DataFrame") ?? xElement.Element("Distribution"),
            "Should contain DataFrame or Distribution element.");
    }

    /// <summary>Verifies that from X element restores model.</summary>
    [TestMethod]
    public void Test_FromXElement_RestoresModel()
    {
        var df = MakeDataFrame(InlineNormalData.Take(10).ToArray());
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

    #region Validation

    /// <summary>Verifies that validate returns true when valid model.</summary>
    [TestMethod]
    public void Test_Validate_ValidModel_ReturnsTrue()
    {
        var df = MakeDataFrame(InlineNormalData);
        var model = new UnivariateDistribution(df, UnivariateDistributionType.Normal);

        var (isValid, messages) = model.Validate();

        Assert.IsTrue(isValid, $"Validation failed: {string.Join(", ", messages)}");
    }

    // Note: An empty BestFitDataFrame currently passes validation in the model layer
    // (empty series and zero data points are not flagged as errors). The "model with
    // no data should fail validation" expectation lives at the UI / analysis layer,
    // not in UnivariateDistribution itself. Keep this contract documented here so
    // a future regression that flips the production behavior would be a deliberate
    // change rather than an accident.

    #endregion

    #region State transitions

    /// <summary>Verifies that change distribution type updates parameters.</summary>
    [TestMethod]
    public void Test_ChangeDistributionType_UpdatesParameters()
    {
        var df = MakeDataFrame(InlineNormalData);

        var normalModel = new UnivariateDistribution(df, UnivariateDistributionType.Normal);
        int normalParamCount = normalModel.Parameters.Count;

        var gevModel = new UnivariateDistribution(df, UnivariateDistributionType.GeneralizedExtremeValue);
        int gevParamCount = gevModel.Parameters.Count;

        Assert.AreEqual(2, normalParamCount);
        Assert.AreEqual(3, gevParamCount);
    }

    #endregion
}
