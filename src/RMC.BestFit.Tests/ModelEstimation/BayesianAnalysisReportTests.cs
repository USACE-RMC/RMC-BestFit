using Numerics.Distributions;
using Numerics.Mathematics.Optimization;
using Numerics.Sampling.MCMC;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using System.Reflection;
using BestFitDataFrame = RMC.BestFit.Models.DataFrame;

namespace RMC.BestFit.Tests.ModelEstimation;

/// <summary>
/// Fast unit tests for <c>BayesianAnalysis.GenerateReport</c> diagnostic text.
/// These tests inject synthetic <c>MCMCResults</c> and do not run MCMC.
/// </summary>
[TestClass]
public class BayesianAnalysisReportTests
{
    /// <summary>
    /// Verifies that a DE-MCzs acceptance rate below the preferred range but inside
    /// the acceptable buffer is reported as a note, not a warning.
    /// </summary>
    [TestMethod]
    public void GenerateReport_DEMCzsAcceptanceBelowPreferredInsideBuffer_ReportsNote()
    {
        var analysis = CreateEstimatedAnalysis(
            BayesianAnalysis.SamplerType.DEMCzs,
            new[] { 0.16, 0.16, 0.16, 0.16 },
            ess: 250.0,
            retainedDrawCount: 1000);

        string report = analysis.GenerateReport();

        StringAssert.Contains(report, "Preferred: 23-44% (DE-MCzs)");
        StringAssert.Contains(report, "Buffer:    15-50% acceptable buffer");
        StringAssert.Contains(report, "Status:    NOTE - below preferred range but within acceptable buffer");
        Assert.IsFalse(report.Contains("Status:    OK - within preferred range"));
    }

    /// <summary>
    /// Verifies that a DE-MCzs acceptance rate inside the preferred range is reported as OK.
    /// </summary>
    [TestMethod]
    public void GenerateReport_DEMCzsAcceptanceInsidePreferredRange_ReportsOk()
    {
        var analysis = CreateEstimatedAnalysis(
            BayesianAnalysis.SamplerType.DEMCzs,
            new[] { 0.30, 0.31, 0.29, 0.30 },
            ess: 250.0,
            retainedDrawCount: 1000);

        string report = analysis.GenerateReport();

        StringAssert.Contains(report, "Status:    OK - within preferred range");
    }

    /// <summary>
    /// Verifies that a DE-MCzs acceptance rate below the acceptable buffer is reported as a warning.
    /// </summary>
    [TestMethod]
    public void GenerateReport_DEMCzsAcceptanceBelowBuffer_ReportsWarning()
    {
        var analysis = CreateEstimatedAnalysis(
            BayesianAnalysis.SamplerType.DEMCzs,
            new[] { 0.10, 0.11, 0.09, 0.10 },
            ess: 250.0,
            retainedDrawCount: 1000);

        string report = analysis.GenerateReport();

        StringAssert.Contains(report, "Status:    WARNING - below acceptable buffer");
        StringAssert.Contains(report, "Reduce the Jump parameter");
    }

    /// <summary>
    /// Verifies that a DE-MCzs acceptance rate above the acceptable buffer is reported as a warning.
    /// </summary>
    [TestMethod]
    public void GenerateReport_DEMCzsAcceptanceAboveBuffer_ReportsWarning()
    {
        var analysis = CreateEstimatedAnalysis(
            BayesianAnalysis.SamplerType.DEMCzs,
            new[] { 0.55, 0.56, 0.54, 0.55 },
            ess: 250.0,
            retainedDrawCount: 1000);

        string report = analysis.GenerateReport();

        StringAssert.Contains(report, "Status:    WARNING - above acceptable buffer");
        StringAssert.Contains(report, "Increase the Jump parameter");
    }

    /// <summary>
    /// Verifies that NUTS reports sampler-specific diagnostics without generic Metropolis descriptors.
    /// </summary>
    [TestMethod]
    public void GenerateReport_NutsDiagnostics_ReportSamplerSpecificValues()
    {
        var analysis = CreateEstimatedAnalysis(
            BayesianAnalysis.SamplerType.NUTS,
            new[] { 0.97, 0.97, 0.97, 0.97 },
            ess: 250.0,
            retainedDrawCount: 1000);
        SetNutsDiagnostics(analysis.Results!);

        string report = analysis.GenerateReport();

        StringAssert.Contains(report, "NUTS SAMPLER DIAGNOSTICS");
        StringAssert.Contains(report, "Overall Hamiltonian Acceptance: 97.0%");
        StringAssert.Contains(report, "Divergences:                    1/1,000");
        StringAssert.Contains(report, "Maximum Tree Depth Hits:        2/1,000");
        StringAssert.Contains(report, "Minimum E-BFMI:                 0.150");
        StringAssert.Contains(report, "WARNING - divergent transitions detected");
        StringAssert.Contains(report, "WARNING - E-BFMI below 0.2");
        StringAssert.Contains(report, "Advice: Hamiltonian acceptance is above the acceptable range.");
        Assert.IsFalse(report.Contains("Proposals are too timid"));
        Assert.IsFalse(report.Contains("NUTS is accepting almost every proposal"));
    }

    /// <summary>
    /// Verifies that legacy NUTS results do not reuse an always-one transition counter.
    /// </summary>
    [TestMethod]
    public void GenerateReport_NutsLegacyResult_SuppressesGenericAcceptanceAdvice()
    {
        var analysis = CreateEstimatedAnalysis(
            BayesianAnalysis.SamplerType.NUTS,
            new[] { 1.0, 1.0, 1.0, 1.0 },
            ess: 250.0,
            retainedDrawCount: 1000);

        string report = analysis.GenerateReport();

        StringAssert.Contains(report, "Sampler-specific NUTS diagnostics are unavailable for this legacy result.");
        Assert.IsFalse(report.Contains("Overall:   100.0%"));
        Assert.IsFalse(report.Contains("above acceptable buffer"));
        Assert.IsFalse(report.Contains("Proposals are too timid"));
    }

    /// <summary>
    /// Verifies that a single chain outside the acceptable buffer is called out even
    /// when the overall acceptance rate remains acceptable.
    /// </summary>
    [TestMethod]
    public void GenerateReport_ChainAcceptanceOutsideBuffer_ReportsChainLevelWarning()
    {
        var analysis = CreateEstimatedAnalysis(
            BayesianAnalysis.SamplerType.DEMCzs,
            new[] { 0.10, 0.30, 0.30, 0.30 },
            ess: 250.0,
            retainedDrawCount: 1000);

        string report = analysis.GenerateReport();

        StringAssert.Contains(report, "Chain-level warnings:");
        StringAssert.Contains(report, "Chain 1 acceptance 10.0% is below acceptable buffer 15-50%.");
    }

    /// <summary>
    /// Verifies rank-normalized R-hat below 1.01 passes without expanding report labels.
    /// </summary>
    [TestMethod]
    public void GenerateReport_Rhat1005_PassesModernThreshold()
    {
        var analysis = CreateEstimatedAnalysis(
            BayesianAnalysis.SamplerType.DEMCzs,
            new[] { 0.30, 0.30, 0.30, 0.30 },
            ess: 2500.0,
            retainedDrawCount: 10000);

        string report = analysis.GenerateReport();

        StringAssert.Contains(report, "Max R-hat:   1.0050   (target < 1.01)   OK");
        StringAssert.Contains(report, "R-hat Verdict: OK - chains mixed across parameters");
        StringAssert.Contains(report, "Overall Readiness: READY");
    }

    /// <summary>
    /// Verifies rank-normalized R-hat above 1.01 produces the existing concise warning.
    /// </summary>
    [TestMethod]
    public void GenerateReport_Rhat102_WarnsAtModernThreshold()
    {
        var analysis = CreateEstimatedAnalysis(
            BayesianAnalysis.SamplerType.DEMCzs,
            new[] { 0.30, 0.30, 0.30, 0.30 },
            ess: 2500.0,
            retainedDrawCount: 10000);
        foreach (ParameterResults parameterResult in analysis.Results!.ParameterResults)
            parameterResult.SummaryStatistics.Rhat = 1.02;

        string report = analysis.GenerateReport();

        StringAssert.Contains(report, "Max R-hat:   1.0200   (target < 1.01)   WARNING");
        StringAssert.Contains(report, "R-hat Verdict: WARNING - chain mixing problem detected");
        StringAssert.Contains(report, "Overall Readiness: NOT READY");
    }

    /// <summary>
    /// Verifies that ESS above 400 but below 10 percent of retained draws is not reported as OK.
    /// </summary>
    [TestMethod]
    public void GenerateReport_EssLowEfficiency_ReportsWarning()
    {
        var analysis = CreateEstimatedAnalysis(
            BayesianAnalysis.SamplerType.DEMCzs,
            new[] { 0.30, 0.30, 0.30, 0.30 },
            ess: 500.0,
            retainedDrawCount: 10000);

        string report = analysis.GenerateReport();

        StringAssert.Contains(report, "500 of 10,000 retained draws (5.0% efficiency; MCSE 4.5x independent draws)");
        StringAssert.Contains(report, "ESS Verdict:   WARNING - low efficiency relative to retained draw count");
    }

    /// <summary>
    /// Verifies that ESS at 25 percent of retained draws is reported as OK.
    /// </summary>
    [TestMethod]
    public void GenerateReport_EssPreferredEfficiency_ReportsOk()
    {
        var analysis = CreateEstimatedAnalysis(
            BayesianAnalysis.SamplerType.DEMCzs,
            new[] { 0.30, 0.30, 0.30, 0.30 },
            ess: 2500.0,
            retainedDrawCount: 10000);

        string report = analysis.GenerateReport();

        StringAssert.Contains(report, "2,500 of 10,000 retained draws (25.0% efficiency; MCSE 2.0x independent draws)");
        StringAssert.Contains(report, "ESS Verdict:   OK - meets preferred effective-sample efficiency");
        StringAssert.Contains(report, "Overall Readiness: READY");
    }

    /// <summary>
    /// Verifies that ESS below the diagnostic floor reports a warning.
    /// </summary>
    [TestMethod]
    public void GenerateReport_EssBelowDiagnosticFloor_ReportsWarning()
    {
        var analysis = CreateEstimatedAnalysis(
            BayesianAnalysis.SamplerType.DEMCzs,
            new[] { 0.30, 0.30, 0.30, 0.30 },
            ess: 350.0,
            retainedDrawCount: 1000);

        string report = analysis.GenerateReport();

        StringAssert.Contains(report, "ESS Verdict:   WARNING - below 400 diagnostic floor");
    }

    /// <summary>
    /// Verifies that ESS at or below 100 reports the severe warning.
    /// </summary>
    [TestMethod]
    public void GenerateReport_EssSevereWarning_ReportsSevereWarning()
    {
        var analysis = CreateEstimatedAnalysis(
            BayesianAnalysis.SamplerType.DEMCzs,
            new[] { 0.30, 0.30, 0.30, 0.30 },
            ess: 100.0,
            retainedDrawCount: 1000);

        string report = analysis.GenerateReport();

        StringAssert.Contains(report, "ESS Verdict:   WARNING - severe Monte Carlo precision warning");
        StringAssert.Contains(report, "ESS <= 100 is very low");
    }

    /// <summary>
    /// Creates an estimated Bayesian analysis with synthetic results.
    /// </summary>
    /// <param name="type">The sampler type to assign.</param>
    /// <param name="acceptanceRates">The per-chain acceptance rates to inject.</param>
    /// <param name="ess">The ESS to assign to all parameter summaries.</param>
    /// <param name="retainedDrawCount">The number of synthetic retained posterior draws.</param>
    /// <returns>An estimated Bayesian analysis containing synthetic MCMC results.</returns>
    private static BayesianAnalysis CreateEstimatedAnalysis(BayesianAnalysis.SamplerType type,
        double[] acceptanceRates, double ess, int retainedDrawCount)
    {
        var model = new UnivariateDistribution(CreateNormalTestData(), UnivariateDistributionType.Normal);
        var analysis = new BayesianAnalysis(model, type)
        {
            OutputLength = retainedDrawCount
        };

        var results = CreateSyntheticResults(retainedDrawCount);
        SetAcceptanceRates(results, acceptanceRates);
        analysis.SetCustomMCMCResults(results, skipInformationCriteria: true);

        foreach (var parameterResult in analysis.Results!.ParameterResults)
        {
            parameterResult.SummaryStatistics.Rhat = 1.005;
            parameterResult.SummaryStatistics.ESS = ess;
        }

        return analysis;
    }

    /// <summary>
    /// Creates a small inline Normal data frame for model construction.
    /// </summary>
    /// <returns>A data frame containing exact observations.</returns>
    private static BestFitDataFrame CreateNormalTestData()
    {
        var values = new double[] { 12500, 15300, 8900, 22100, 18700, 14200, 9800, 28500, 17400, 11600 };
        var df = new BestFitDataFrame();
        for (int i = 0; i < values.Length; i++)
            df.ExactSeries.Add(new ExactData(1990 + i, values[i]));
        return df;
    }

    /// <summary>
    /// Creates deterministic synthetic MCMC results without running a sampler.
    /// </summary>
    /// <param name="retainedDrawCount">The number of retained posterior draws to synthesize.</param>
    /// <returns>Synthetic MCMC results.</returns>
    private static MCMCResults CreateSyntheticResults(int retainedDrawCount)
    {
        var output = new List<ParameterSet>(retainedDrawCount);
        for (int i = 0; i < retainedDrawCount; i++)
        {
            double mu = 16000.0 + 100.0 * Math.Sin(i);
            double sigma = 5000.0 + 50.0 * Math.Cos(i);
            output.Add(new ParameterSet(new[] { mu, sigma }, 0.0));
        }

        var map = new ParameterSet(new[] { 16000.0, 5000.0 }, 0.0);
        return new MCMCResults(map, output, alpha: 0.10);
    }

    /// <summary>
    /// Injects a complete, chain-aligned NUTS diagnostic fixture.
    /// </summary>
    /// <param name="results">The MCMC results to update.</param>
    private static void SetNutsDiagnostics(MCMCResults results)
    {
        SetResultProperty(results, nameof(MCMCResults.NUTSDiagnosticSampleCounts), new[] { 250, 250, 250, 250 });
        SetResultProperty(results, nameof(MCMCResults.NUTSDivergenceCounts), new[] { 0, 1, 0, 0 });
        SetResultProperty(results, nameof(MCMCResults.NUTSMaxTreeDepthHitCounts), new[] { 0, 2, 0, 0 });
        SetResultProperty(results, nameof(MCMCResults.NUTSMeanTreeDepths), new[] { 2.1, 2.2, 2.3, 2.4 });
        SetResultProperty(results, nameof(MCMCResults.NUTSMeanLeapfrogSteps), new[] { 4.0, 4.2, 4.4, 4.6 });
        SetResultProperty(results, nameof(MCMCResults.NUTSStepSizes), new[] { 0.12, 0.11, 0.10, 0.09 });
        SetResultProperty(results, nameof(MCMCResults.NUTSEnergyBayesianFractionOfMissingInformation),
            new[] { 0.75, 0.60, 0.15, 0.55 });
    }

    /// <summary>
    /// Assigns a private-set MCMC result property for a synthetic report fixture.
    /// </summary>
    /// <typeparam name="T">The property value type.</typeparam>
    /// <param name="results">The result instance to update.</param>
    /// <param name="propertyName">The public property name.</param>
    /// <param name="value">The synthetic value.</param>
    private static void SetResultProperty<T>(MCMCResults results, string propertyName, T value)
    {
        PropertyInfo? property = typeof(MCMCResults).GetProperty(propertyName);
        MethodInfo? setter = property?.GetSetMethod(nonPublic: true);
        Assert.IsNotNull(setter, $"Unable to inject synthetic {propertyName} into MCMCResults.");
        setter.Invoke(results, new object?[] { value });
    }

    /// <summary>
    /// Injects acceptance rates into synthetic MCMC results.
    /// </summary>
    /// <param name="results">The MCMC results to update.</param>
    /// <param name="acceptanceRates">The acceptance rates to assign.</param>
    private static void SetAcceptanceRates(MCMCResults results, double[] acceptanceRates)
    {
        var property = typeof(MCMCResults).GetProperty(nameof(MCMCResults.AcceptanceRates));
        if (property?.SetMethod != null)
        {
            property.SetValue(results, acceptanceRates);
            return;
        }

        var backingField = typeof(MCMCResults).GetField("<AcceptanceRates>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (backingField != null)
        {
            backingField.SetValue(results, acceptanceRates);
            return;
        }

        Assert.Fail("Unable to inject synthetic acceptance rates into MCMCResults.");
    }
}
