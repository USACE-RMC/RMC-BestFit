using Numerics.Mathematics.Optimization;
using RMC.BestFit.Analyses;
using System.Text;

namespace RMC.BestFit.Tests.Univariate;

/// <summary>
/// Programmatic unit tests for the Bulletin 17C report's uncertainty sampling
/// diagnostics section.
/// </summary>
/// <remarks>
/// Exercises the internal <c>ReportAppendBootstrapDiagnostics</c> helper directly with
/// pre-populated diagnostics, so no estimator runs. Verifies the discard-semantics labels,
/// the retained-count line, the optimizer-status distribution, and the low-retention warnings.
/// </remarks>
[TestClass]
public class Bulletin17CReportDiagnosticsTests
{
    private const int LabelWidth = 24;

    /// <summary>
    /// Renders the diagnostics section for the given diagnostics and method.
    /// </summary>
    /// <param name="diag">The diagnostics to render.</param>
    /// <param name="method">The uncertainty method label.</param>
    /// <returns>The rendered report section text.</returns>
    private static string Render(BootstrapDiagnostics diag, UncertaintyMethod method)
    {
        var sb = new StringBuilder();
        Bulletin17CAnalysis.ReportAppendBootstrapDiagnostics(sb, diag, LabelWidth, method);
        return sb.ToString();
    }

    /// <summary>
    /// Bootstrap-method sections report the realizations attempted, the replicates substituted
    /// with the parent fit, the replicates used, and the per-candidate GMM status distribution.
    /// </summary>
    [TestMethod]
    public void Render_BootstrapMethod_ReportsSubstitutedUsedAndStatusCounts()
    {
        var diag = new BootstrapDiagnostics { TotalReplicates = 10_000 };
        for (int i = 0; i < 10_120; i++) diag.IncrementAttempted();
        for (int i = 0; i < 120; i++) diag.IncrementFailed();
        diag.AddOptimizerFallbacks(4);
        diag.RecordGMMStatus(OptimizationStatus.Success);
        diag.RecordGMMStatus(OptimizationStatus.MaximumIterationsReached);
        diag.RetainedReplicates = 9_880;

        string text = Render(diag, UncertaintyMethod.Bootstrap);

        StringAssert.Contains(text, "BOOTSTRAP DIAGNOSTICS");
        StringAssert.Contains(text, "Replicates Requested:");
        StringAssert.Contains(text, "Realizations Attempted:");
        StringAssert.Contains(text, "10,120");
        StringAssert.Contains(text, "Substituted (parent fit):");
        StringAssert.Contains(text, "120 (1.2%)");
        StringAssert.Contains(text, "Replicates Used:");
        StringAssert.Contains(text, "9,880");
        StringAssert.Contains(text, "Optimizer Fallbacks:");
        StringAssert.Contains(text, "GMM Status Counts (per start candidate):");
        StringAssert.Contains(text, "120 of 10,000 replicates (1.2%) were substituted with the parent fit");
        StringAssert.Contains(text, "point mass at the parent estimate");
    }

    /// <summary>
    /// The substitution fraction is per requested replicate, so a quarter of the replicates
    /// substituted after ten attempts each triggers the high-discard warning.
    /// </summary>
    [TestMethod]
    public void Render_SubstitutionFraction_IsPerReplicate_AndWarnsAboveTenPercent()
    {
        var diag = new BootstrapDiagnostics { TotalReplicates = 1_000 };
        for (int i = 0; i < 1_000 + 9 * 250; i++) diag.IncrementAttempted();
        for (int i = 0; i < 250; i++) diag.IncrementFailed();
        diag.AddRetries(9 * 250);
        diag.RetainedReplicates = 750;

        string text = Render(diag, UncertaintyMethod.Bootstrap);

        StringAssert.Contains(text, "Realizations Attempted:");
        StringAssert.Contains(text, "3,250");
        StringAssert.Contains(text, "250 (25.0%)");
        StringAssert.Contains(text, "250 of 1,000 replicates (25.0%) were substituted with the parent fit");
        StringAssert.Contains(text, "WARNING: High discard rate (>10%)");
        Assert.IsFalse(text.Contains("WARNING: Very high discard rate (>30%)"),
            "A 25% substitution rate must not trigger the 30% warning.");
        Assert.IsFalse(text.Contains("WARNING: Fewer than half"),
            "750 retained replicates of 1,000 must not trigger the half-retained warning.");
    }

    /// <summary>
    /// Pivot-bootstrap bound repairs and z-limit clips are reported with their per-replicate rates.
    /// </summary>
    [TestMethod]
    public void Render_BoundRepairsAndZLimitClips_ShowLines()
    {
        var diag = new BootstrapDiagnostics { TotalReplicates = 1_000 };
        for (int i = 0; i < 1_000; i++) diag.IncrementAttempted();
        for (int i = 0; i < 15; i++) diag.IncrementBoundRepair();
        for (int i = 0; i < 4; i++) diag.IncrementPivotRejection();
        diag.RetainedReplicates = 1_000;

        string text = Render(diag, UncertaintyMethod.BiasCorrectedBootstrap);

        StringAssert.Contains(text, "Bound Repairs:");
        StringAssert.Contains(text, "15 (1.5%)");
        StringAssert.Contains(text, "Pivot z-limit Clips:");
        StringAssert.Contains(text, "4 (0.4%)");
        StringAssert.Contains(text, "15 pivot draws (1.5%) were moved inside the parameter bounds");
    }

    /// <summary>
    /// The MVN section uses the sampling header and draw labels, and omits the GMM
    /// status distribution (MVN draws are not estimator refits).
    /// </summary>
    [TestMethod]
    public void Render_MvnMethod_UsesSamplingHeaderWithoutStatusCounts()
    {
        var diag = new BootstrapDiagnostics { TotalReplicates = 10_000 };
        for (int i = 0; i < 40; i++) diag.IncrementFailed();
        diag.RetainedReplicates = 9_960;

        string text = Render(diag, UncertaintyMethod.MultivariateNormal);

        StringAssert.Contains(text, "SAMPLING DIAGNOSTICS");
        StringAssert.Contains(text, "Draws Requested:");
        StringAssert.Contains(text, "Draws Used:");
        Assert.IsFalse(text.Contains("GMM Status Counts:"),
            "MVN sampling has no replicate GMM fits, so no status distribution is shown.");
        Assert.IsFalse(text.Contains("Total Retries:"),
            "The retry statistics are bootstrap-only lines.");
    }

    /// <summary>
    /// Retaining fewer than half of the requested realizations produces the strong warning.
    /// </summary>
    [TestMethod]
    public void Render_LessThanHalfRetained_ShowsStrongWarning()
    {
        var diag = new BootstrapDiagnostics { TotalReplicates = 10_000 };
        diag.RetainedReplicates = 4_000;

        string text = Render(diag, UncertaintyMethod.BiasCorrectedBootstrap);

        StringAssert.Contains(text, "WARNING: Fewer than half of the requested realizations were retained");
        StringAssert.Contains(text, "4,000 of 10,000");
    }

    /// <summary>
    /// Retaining fewer than 1,000 realizations (but at least half) produces the
    /// quantile-resolution note instead of the strong warning.
    /// </summary>
    [TestMethod]
    public void Render_FewerThanThousandRetained_ShowsResolutionNote()
    {
        var diag = new BootstrapDiagnostics { TotalReplicates = 1_000 };
        diag.RetainedReplicates = 900;

        string text = Render(diag, UncertaintyMethod.Bootstrap);

        StringAssert.Contains(text, "Note: Only 900 realizations were retained.");
        Assert.IsFalse(text.Contains("WARNING: Fewer than half"),
            "The strong warning applies only below the 50% retention threshold.");
    }

    /// <summary>
    /// A high discard rate produces the reworded discard-rate warning.
    /// </summary>
    [TestMethod]
    public void Render_HighDiscardRate_ShowsDiscardWarning()
    {
        var diag = new BootstrapDiagnostics { TotalReplicates = 65 };
        for (int i = 0; i < 100; i++) diag.IncrementAttempted();
        for (int i = 0; i < 35; i++) diag.IncrementFailed();
        diag.RetainedReplicates = 65;

        string text = Render(diag, UncertaintyMethod.Bootstrap);

        StringAssert.Contains(text, "WARNING: Very high discard rate (>30%)");
    }

    /// <summary>
    /// Pivot transform failures render their own line when present.
    /// </summary>
    [TestMethod]
    public void Render_TransformFailures_ShowLine()
    {
        var diag = new BootstrapDiagnostics { TotalReplicates = 5_000 };
        for (int i = 0; i < 12; i++) diag.IncrementTransformFailure();
        diag.RetainedReplicates = 4_988;

        string text = Render(diag, UncertaintyMethod.BiasCorrectedBootstrap);

        StringAssert.Contains(text, "Transform Failures:");
        StringAssert.Contains(text, "12");
    }
}
