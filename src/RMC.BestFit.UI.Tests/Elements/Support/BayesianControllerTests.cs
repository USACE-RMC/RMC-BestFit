using Microsoft.VisualStudio.TestTools.UnitTesting;
using OxyPlot.Wpf;
using RMC.BestFit.Estimation;
using RMC.BestFit.UI;
using System;

namespace RMC.BestFit.UI.Tests.Elements.Support;

/// <summary>
/// Unit tests for <see cref="BayesianController"/>, the shared owner of the 7 Bayesian
/// MCMC diagnostic plots and the BayesianAnalysis settings undo bridges.
/// </summary>
/// <remarks>
/// All tests require an STA thread because the controller constructor creates OxyPlot WPF
/// <see cref="Plot"/> instances. Tests focus on construction, bridge lifecycle idempotency,
/// SuspendPlotBridges semantics, CopyTo round-trip, UpdateReport, Dispose idempotency,
/// and the static property-name lists.
/// </remarks>
[TestClass]
public class BayesianControllerTests
{
    /// <summary>
    /// Verifies that all 7 diagnostic plot properties are non-null after construction.
    /// </summary>
    [STATestMethod]
    public void Constructor_AllSevenPlotsCreated()
    {
        var controller = new BayesianController();

        Assert.IsNotNull(controller.MarkovChainTracePlot);
        Assert.IsNotNull(controller.HistogramPlot);
        Assert.IsNotNull(controller.KernelDensityPlot);
        Assert.IsNotNull(controller.AutocorrelationPlot);
        Assert.IsNotNull(controller.MeanLikelihoodPlot);
        Assert.IsNotNull(controller.BivariateHeatMapPlot);
        Assert.IsNotNull(controller.InfluenceDiagnosticsPlot);
    }

    /// <summary>
    /// Verifies that <see cref="BayesianController.MCMCReport"/> is null after construction
    /// (no report has been generated yet).
    /// </summary>
    [STATestMethod]
    public void Constructor_MCMCReport_IsNullInitially()
    {
        var controller = new BayesianController();

        Assert.IsNull(controller.MCMCReport);
    }

    /// <summary>
    /// Verifies that the 7 default plots each have at least 2 axes (X and Y) configured
    /// by the factory methods. Bivariate heat map has a 3rd color axis.
    /// </summary>
    [STATestMethod]
    public void Constructor_PlotsHaveAxesConfigured()
    {
        var controller = new BayesianController();

        Assert.IsTrue(controller.MarkovChainTracePlot.Axes.Count >= 2);
        Assert.IsTrue(controller.HistogramPlot.Axes.Count >= 2);
        Assert.IsTrue(controller.KernelDensityPlot.Axes.Count >= 2);
        Assert.IsTrue(controller.AutocorrelationPlot.Axes.Count >= 2);
        Assert.IsTrue(controller.MeanLikelihoodPlot.Axes.Count >= 2);
        Assert.IsTrue(controller.BivariateHeatMapPlot.Axes.Count >= 3, "Bivariate heat map must include a color axis.");
        Assert.IsTrue(controller.InfluenceDiagnosticsPlot.Axes.Count >= 2);
    }

    /// <summary>
    /// Verifies that <see cref="BayesianController.DisposeBridges"/> is safe to call
    /// before <see cref="BayesianController.SetupBridges"/> has run.
    /// </summary>
    [STATestMethod]
    public void DisposeBridges_BeforeSetup_DoesNotThrow()
    {
        var controller = new BayesianController();

        // Must not throw — bridges have never been created
        controller.DisposeBridges();
    }

    /// <summary>
    /// Verifies that <see cref="BayesianController.DisposeBridges"/> is idempotent —
    /// calling it twice is safe.
    /// </summary>
    [STATestMethod]
    public void DisposeBridges_CalledTwice_DoesNotThrow()
    {
        var controller = new BayesianController();

        controller.DisposeBridges();
        controller.DisposeBridges();
    }

    /// <summary>
    /// Verifies that <see cref="BayesianController.Dispose"/> is idempotent.
    /// </summary>
    [STATestMethod]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var controller = new BayesianController();

        controller.Dispose();
        controller.Dispose();
    }

    /// <summary>
    /// Verifies that <see cref="BayesianController.SuspendPlotBridges"/> returns a
    /// non-null disposable even when no bridges have been set up.
    /// </summary>
    [STATestMethod]
    public void SuspendPlotBridges_BeforeSetup_ReturnsDisposable()
    {
        var controller = new BayesianController();

        using var token = controller.SuspendPlotBridges();

        Assert.IsNotNull(token);
    }

    /// <summary>
    /// Verifies that the disposable returned by <see cref="BayesianController.SuspendPlotBridges"/>
    /// can be safely disposed twice (defensive idempotency).
    /// </summary>
    [STATestMethod]
    public void SuspendPlotBridges_DoubleDispose_DoesNotThrow()
    {
        var controller = new BayesianController();

        var token = controller.SuspendPlotBridges();
        token.Dispose();
        token.Dispose();
    }

    /// <summary>
    /// Verifies that <see cref="BayesianController.RebuildSeriesAndAnnotationBridges(Plot)"/>
    /// returns silently when given a plot that the controller does not own.
    /// </summary>
    [STATestMethod]
    public void RebuildSeriesAndAnnotationBridges_UnknownPlot_DoesNotThrow()
    {
        var controller = new BayesianController();
        var foreignPlot = new Plot();

        // Foreign plot is not one of the 7 managed plots — must be a no-op.
        controller.RebuildSeriesAndAnnotationBridges(foreignPlot);
    }

    /// <summary>
    /// Verifies that <see cref="BayesianController.CopyTo(BayesianController)"/> with
    /// a null target is a safe no-op.
    /// </summary>
    [STATestMethod]
    public void CopyTo_NullTarget_DoesNotThrow()
    {
        var controller = new BayesianController();

        controller.CopyTo(null!);
    }

    /// <summary>
    /// Verifies that <see cref="BayesianController.CopyTo(BayesianController)"/> deep-copies
    /// a visual property (axis title) from source to target via the PlotSerializer round-trip.
    /// </summary>
    [STATestMethod]
    public void CopyTo_ValidTarget_CopiesAxisTitle()
    {
        var source = new BayesianController();
        var target = new BayesianController();

        // Modify a unique property on the source's histogram plot
        source.HistogramPlot.Axes[1].Title = "CustomXAxisTitleForTest";

        source.CopyTo(target);

        Assert.AreEqual("CustomXAxisTitleForTest", target.HistogramPlot.Axes[1].Title);
    }

    /// <summary>
    /// Verifies that <see cref="BayesianController.UpdateReport(BayesianAnalysis)"/> with
    /// a null analysis sets the cached report to <see cref="string.Empty"/> rather than null.
    /// </summary>
    [STATestMethod]
    public void UpdateReport_NullBayesianAnalysis_SetsEmptyString()
    {
        var controller = new BayesianController();

        controller.UpdateReport(null!);

        Assert.AreEqual(string.Empty, controller.MCMCReport);
    }

    /// <summary>
    /// Verifies that the static <see cref="BayesianController.SimulationDefaultsProperties"/>
    /// list contains the canonical set of simulation property names.
    /// </summary>
    [TestMethod]
    public void SimulationDefaultsProperties_ContainsExpectedProperties()
    {
        CollectionAssert.Contains(BayesianController.SimulationDefaultsProperties, nameof(BayesianAnalysis.Iterations));
        CollectionAssert.Contains(BayesianController.SimulationDefaultsProperties, nameof(BayesianAnalysis.WarmupIterations));
        CollectionAssert.Contains(BayesianController.SimulationDefaultsProperties, nameof(BayesianAnalysis.NumberOfChains));
    }

    /// <summary>
    /// Verifies that the static <see cref="BayesianController.AdvancedDefaultsProperties"/>
    /// list contains the canonical set of advanced simulation property names.
    /// </summary>
    [TestMethod]
    public void AdvancedDefaultsProperties_ContainsExpectedProperties()
    {
        CollectionAssert.Contains(BayesianController.AdvancedDefaultsProperties, nameof(BayesianAnalysis.Jump));
        CollectionAssert.Contains(BayesianController.AdvancedDefaultsProperties, nameof(BayesianAnalysis.Noise));
    }

    /// <summary>
    /// Verifies that the static <see cref="BayesianController.AlwaysRecordProperties"/>
    /// list contains the always-recorded property names (UseDefaults flags, point estimator).
    /// </summary>
    [TestMethod]
    public void AlwaysRecordProperties_ContainsUseDefaultsAndPointEstimator()
    {
        CollectionAssert.Contains(BayesianController.AlwaysRecordProperties, nameof(BayesianAnalysis.UseSimulationDefaults));
        CollectionAssert.Contains(BayesianController.AlwaysRecordProperties, nameof(BayesianAnalysis.UseAdvancedSimulationDefaults));
        CollectionAssert.Contains(BayesianController.AlwaysRecordProperties, nameof(BayesianAnalysis.PointEstimator));
    }
}
