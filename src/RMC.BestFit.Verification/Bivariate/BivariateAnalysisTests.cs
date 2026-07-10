using Numerics;
using Numerics.Distributions;
using Numerics.Distributions.Copulas;
using RMC.BestFit.Analyses;
using RMC.BestFit.Models;

namespace RMC.BestFit.Verification.Bivariate;

/// <summary>
/// Computational verification tests for the <see cref="BivariateAnalysis"/> class.
/// Holds only tests that drive the Bayesian MCMC pipeline via <see cref="BivariateAnalysis.RunAsync"/>.
/// Programmatic configuration / serialization / property-round-trip tests have moved to
/// <c>RMC.BestFit.Verification/Bivariate/BivariateAnalysisTests.cs</c>. End-to-end Bayesian
/// parameter recovery for every supported copula lives in
/// <see cref="BivariateAnalysisParameterRecoveryTests"/>.
/// </summary>
/// <remarks>
/// <para>
///     <b>Authors:</b>
/// </para>
/// <list type="bullet">
///     <item>Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil</item>
/// </list>
/// </remarks>
[TestClass]
public class BivariateAnalysisTests
{
    #region Test Data

    /// <summary>
    /// Creates a pair of test marginal distributions for bivariate analysis.
    /// </summary>
    private static (UnivariateDistribution marginalX, UnivariateDistribution marginalY) CreateMarginals(int? count = null)
    {
        int n = count ?? TestData.SampleSize;

        var dfX = new DataFrame { ExactSeries = new ExactSeries(TestData.BivariateXData.Take(n).ToArray()) };
        var marginalX = new UnivariateDistribution(dfX, UnivariateDistributionType.Normal);

        var dfY = new DataFrame { ExactSeries = new ExactSeries(TestData.BivariateYData.Take(n).ToArray()) };
        var marginalY = new UnivariateDistribution(dfY, UnivariateDistributionType.Gumbel);

        return (marginalX, marginalY);
    }

    /// <summary>
    /// Creates a test <see cref="BivariateDistribution"/> with Normal copula.
    /// </summary>
    private static BivariateDistribution CreateTestBivariateDistribution(int? count = null)
    {
        var (marginalX, marginalY) = CreateMarginals(count);
        return new BivariateDistribution(marginalX, marginalY, CopulaType.Normal);
    }

    #endregion

    #region Event Tests (RunAsync)

    /// <summary>
    /// Tests that AnalysisStarting event is raised when RunAsync is called.
    /// </summary>
    [TestMethod]
    public async Task RunAsync_RaisesAnalysisStartingEvent()
    {
        var bivariateDist = CreateTestBivariateDistribution(100);
        var analysis = new BivariateAnalysis(bivariateDist);
        analysis.BayesianAnalysis.Iterations = 100;
        analysis.BayesianAnalysis.WarmupIterations = 50;

        bool eventRaised = false;
        analysis.AnalysisStarting += (s, e) =>
        {
            eventRaised = true;
            e.Cancel = true; // Cancel to avoid long test
        };

        await analysis.RunAsync();

        Assert.IsTrue(eventRaised, "AnalysisStarting event should be raised.");
    }

    /// <summary>
    /// Tests that AnalysisCompleted event is raised after RunAsync completes.
    /// </summary>
    [TestMethod]
    public async Task RunAsync_RaisesAnalysisCompletedEvent()
    {
        var bivariateDist = CreateTestBivariateDistribution(100);
        var analysis = new BivariateAnalysis(bivariateDist);

        bool eventRaised = false;
        analysis.AnalysisStarting += (s, e) => e.Cancel = true; // Cancel immediately
        analysis.AnalysisCompleted += (s, e) =>
        {
            eventRaised = true;
            Assert.IsTrue(e.Cancelled, "Cancelled should be true when canceled.");
        };

        await analysis.RunAsync();

        Assert.IsTrue(eventRaised, "AnalysisCompleted event should be raised.");
    }

    /// <summary>
    /// Tests that AnalysisCompleted reports correct success status when canceled.
    /// </summary>
    [TestMethod]
    public async Task RunAsync_WhenCanceled_ReportsNotSuccessful()
    {
        var bivariateDist = CreateTestBivariateDistribution(100);
        var analysis = new BivariateAnalysis(bivariateDist);
        bool wasSuccessful = true;

        analysis.AnalysisStarting += (s, e) => e.Cancel = true;
        analysis.AnalysisCompleted += (s, e) =>
        {
            wasSuccessful = e.Succeeded;
        };

        await analysis.RunAsync();

        Assert.IsFalse(wasSuccessful, "Succeeded should be false when canceled.");
    }

    #endregion

    #region RunAsync Validation Tests

    /// <summary>
    /// Tests that RunAsync throws when validation fails.
    /// </summary>
    [TestMethod]
    public async Task RunAsync_WhenValidationFails_ThrowsInvalidOperationException()
    {
        var invalidDist = new BivariateDistribution();
        var analysis = new BivariateAnalysis(invalidDist);

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            async () => await analysis.RunAsync(),
            "RunAsync should throw when validation fails.");
    }

    #endregion
}
