using Numerics.Distributions;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;

namespace RMC.BestFit.Tests.Estimation;

/// <summary>
/// Fast structural unit tests for the <see cref="BayesianAnalysis"/> class.
/// MCMC-running tests live in RMC.BestFit.Verification/ModelEstimation/BayesianAnalysisMCMCTests.cs.
/// </summary>
[TestClass]
public class BayesianAnalysisTests
{
    #region Test Data

    private static DataFrame CreateNormalTestData()
    {
        // Small inline fixture: 10 fixed annual peak values (cfs).
        var values = new double[] { 12500, 15300, 8900, 22100, 18700, 14200, 9800, 28500, 17400, 11600 };
        var df = new DataFrame();
        for (int i = 0; i < values.Length; i++)
            df.ExactSeries.Add(new ExactData(1990 + i, values[i]));
        return df;
    }

    #endregion

    #region Constructor Tests

    /// <summary>Verifies that constructor with model creates valid analysis.</summary>
    [TestMethod]
    public void Test_Constructor_WithModel_CreatesValidAnalysis()
    {
        // Arrange
        var dataFrame = CreateNormalTestData();
        var model = new UnivariateDistribution(dataFrame, UnivariateDistributionType.Normal);

        // Act
        var bayesian = new BayesianAnalysis(model);

        // Assert
        Assert.IsNotNull(bayesian);
        Assert.AreSame(model, bayesian.Model);
        Assert.IsFalse(bayesian.IsEstimated);
    }

    /// <summary>Verifies that constructor sets default sampler type.</summary>
    [TestMethod]
    public void Test_Constructor_SetsDefaultSamplerType()
    {
        var dataFrame = CreateNormalTestData();
        var model = new UnivariateDistribution(dataFrame, UnivariateDistributionType.Normal);

        var bayesian = new BayesianAnalysis(model);

        // Default should be DEMCzs
        Assert.AreEqual(BayesianAnalysis.SamplerType.DEMCzs, bayesian.Type);
    }

    /// <summary>Verifies that constructor throws when null model.</summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Test_Constructor_NullModel_Throws()
    {
        var bayesian = new BayesianAnalysis((IModel)null!);
    }

    #endregion
}
