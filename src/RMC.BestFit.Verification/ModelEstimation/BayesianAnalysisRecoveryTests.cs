using Numerics.Distributions;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using RMC.BestFit.Verification.Recovery;

namespace RMC.BestFit.Verification.ModelEstimation;

/// <summary>
/// Verifies Bayesian Normal generated-parent recovery under unchanged production sampler defaults.
/// </summary>
/// <remarks>
/// Each cell uses 1,000 seeded scalar observations from Normal(mu=100, sigma=15), requests a
/// central 95% posterior interval, and requires parent inclusion, R-hat below 1.10, and ESS of
/// at least 100 for both fitted coordinates. The conjugate Normal-Normal calculation is retained
/// separately in <see cref="BayesianConjugateOracleVerificationTests"/> as analytical evidence.
/// </remarks>
[TestClass]
public class BayesianAnalysisRecoveryTests
{
    private const double GeneratingMean = 100.0;
    private const double GeneratingStandardDeviation = 15.0;

#region Test Data

    /// <summary>
    /// Creates normal Test Data.
    /// </summary>
    /// <returns>The created test object.</returns>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static DataFrame CreateNormalTestData()
    {
        _ = RecoveryDesign.ScalarObservations("Independent Normal scalar observations.");
        var random = new Random(12345);
        var data = new List<ExactData>();
        var normal = new Normal(GeneratingMean, GeneratingStandardDeviation);

        for (int i = 0; i < RecoveryDesign.SampleSize; i++)
        {
            double value = normal.InverseCDF(random.NextDouble());
            data.Add(new ExactData(i, value));
        }

        Assert.AreEqual(RecoveryDesign.SampleSize, data.Count, "The declared scalar recovery design must contain exactly 1,000 observations.");
        var df = new DataFrame();
        df.ExactSeries = new ExactSeries(data);
        return df;
    }

    #endregion

    /// <summary>
    /// Verifies central-95% parent inclusion and convergence diagnostics for the seeded Normal recovery.
    /// </summary>
    /// <remarks>
    /// Sample unit: scalar observation; N=1000; seed=12345; parent=(mu=100, sigma=15); fitted
    /// coordinates=(mu, sigma). The central interval width is 95%. The conditional secondary 5%
    /// point criterion is not used because this cell's primary evidence is posterior inclusion.
    /// </remarks>
    [TestMethod]
    public async Task Test_Estimate_PosteriorMean_NearTrueValue()
    {
        // Arrange - known parameters μ=100, σ=15
        var dataFrame = CreateNormalTestData();
        var model = new UnivariateDistribution(dataFrame, UnivariateDistributionType.Normal);
        var bayesian = new BayesianAnalysis(model) { CredibleIntervalWidth = 0.95d };

        // Act
        await bayesian.RunAsync();

        Assert.IsNotNull(bayesian.Results);
        AssertNormalRecovery(bayesian);
    }

    /// <summary>
    /// Verifies central-95% parent inclusion and convergence diagnostics through the interval-focused Normal recovery cell.
    /// </summary>
    /// <remarks>
    /// Sample unit: scalar observation; N=1000; seed=12345; parent=(mu=100, sigma=15); fitted
    /// coordinates=(mu, sigma). The 95% interval is reported directly; the secondary 5% criterion
    /// is inapplicable because no response curve is identified for this cell.
    /// </remarks>
    [TestMethod]
    public async Task Test_N1000_DefaultPosteriorIntervals_ContainGeneratingParameters()
    {
        var dataFrame = CreateNormalTestData();
        var model = new UnivariateDistribution(dataFrame, UnivariateDistributionType.Normal);
        var bayesian = new BayesianAnalysis(model) { CredibleIntervalWidth = 0.95d };

        await bayesian.RunAsync();

        Assert.IsNotNull(bayesian.Results);
        AssertNormalRecovery(bayesian);
    }

    /// <summary>
    /// Applies the shared recovery policy to both Normal posterior coordinates.
    /// </summary>
    /// <param name="bayesian">Completed Bayesian analysis with the declared central 95% interval.</param>
    /// <remarks>
    /// The scientific source of the diagnostics is the retained MCMC posterior summary. This helper
    /// does not set sampler options, seeds, or numerical tolerances.
    /// </remarks>
    private static void AssertNormalRecovery(BayesianAnalysis bayesian)
    {
        Assert.IsNotNull(bayesian.Results);
        var mu = bayesian.Results.ParameterResults[0].SummaryStatistics;
        var sigma = bayesian.Results.ParameterResults[1].SummaryStatistics;
        RecoveryAcceptance.AssertBayesianRecovery("mu", GeneratingMean, mu.LowerCI, mu.UpperCI, mu.Rhat, mu.ESS);
        RecoveryAcceptance.AssertBayesianRecovery("sigma", GeneratingStandardDeviation, sigma.LowerCI, sigma.UpperCI, sigma.Rhat, sigma.ESS);
    }
}
