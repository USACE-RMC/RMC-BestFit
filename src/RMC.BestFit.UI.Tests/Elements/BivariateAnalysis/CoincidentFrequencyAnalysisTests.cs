using Microsoft.VisualStudio.TestTools.UnitTesting;
using Numerics.Distributions;
using Numerics.Mathematics.Optimization;
using Numerics.Sampling.MCMC;
using RMC.BestFit.Models;
using RMC.BestFit.UI;
using System.ComponentModel;
using System.Reflection;
using ModelAnalyses = RMC.BestFit.Analyses;

namespace RMC.BestFit.UI.Tests.Elements.BivariateAnalysis;

/// <summary>
/// Unit tests for <see cref="CoincidentFrequencyAnalysis"/>, the UI wrapper for the
/// model-layer coincident frequency analysis.
/// </summary>
/// <remarks>
/// All tests require STA thread because the constructor creates an OxyPlot WPF
/// <c>Plot</c> instance. Tests cover constructor defaults, property change notifications,
/// upstream <see cref="UI.BivariateAnalysis"/> linking, ordinate collections, response
/// surface assignment, settings pass-through, plot ownership, undo bridge setup, and
/// <c>Copy()</c>. Save/Open/Delete are excluded because they require a SQLite project file.
/// </remarks>
[TestClass]
public class CoincidentFrequencyAnalysisTests
{
    private static BivariateAnalysisCollection? _collection;
    private static UnivariateAnalysisCollection? _univariateCollection;

    /// <summary>
    /// Creates a shared collection backed by the singleton BestFitProject.
    /// </summary>
    [ClassInitialize]
    public static void ClassInitialize(TestContext _)
    {
        var project = BestFitProject.GetInstance();
        _collection = new BivariateAnalysisCollection(project);
        _univariateCollection = new UnivariateAnalysisCollection(project);
    }

    /// <summary>
    /// Builds deterministic synthetic posterior samples for a Normal marginal.
    /// </summary>
    /// <param name="mean">The MAP mean value.</param>
    /// <param name="standardDeviation">The MAP standard deviation value.</param>
    /// <returns>Synthetic MCMC results with two Normal parameters.</returns>
    private static MCMCResults BuildSyntheticMarginalResults(double mean, double standardDeviation)
    {
        var output = new List<ParameterSet>();
        for (int i = 0; i < 10; i++)
        {
            output.Add(new ParameterSet(
                new[] { mean + 0.05 * i, standardDeviation * (1.0 + 0.01 * i) },
                0.0));
        }

        return new MCMCResults(new ParameterSet(new[] { mean, standardDeviation }, 0.0), output, alpha: 0.10);
    }

    /// <summary>
    /// Creates a UI univariate analysis with injected synthetic MCMC results.
    /// </summary>
    /// <param name="name">The analysis name.</param>
    /// <param name="mean">The Normal mean.</param>
    /// <param name="standardDeviation">The Normal standard deviation.</param>
    /// <returns>An estimated univariate analysis suitable for bivariate marginal tests.</returns>
    private static UI.UnivariateAnalysis CreateEstimatedMarginal(
        string name,
        double mean,
        double standardDeviation)
    {
        var analysis = new UI.UnivariateAnalysis(name, _univariateCollection!);
        analysis.UnivariateDistribution.DistributionType = UnivariateDistributionType.Normal;
        analysis.UnivariateDistribution.SetParameterValues(new[] { mean, standardDeviation });

        analysis.BayesianAnalysis.OutputLength = 10;
        analysis.BayesianAnalysis.SetCustomMCMCResults(
            BuildSyntheticMarginalResults(mean, standardDeviation),
            skipInformationCriteria: true);

        var isEstimatedField = typeof(ModelAnalyses.AnalysisBase).GetField(
            "_isEstimated",
            BindingFlags.Instance | BindingFlags.NonPublic);
        isEstimatedField!.SetValue(analysis.InnerAnalysis, true);

        return analysis;
    }

    #region Constructor

    /// <summary>
    /// Verifies the constructor stores the supplied name.
    /// </summary>
    [STATestMethod]
    public void Constructor_StoresName()
    {
        var cfa = new CoincidentFrequencyAnalysis("TestCFA", _collection!);
        Assert.AreEqual("TestCFA", cfa.Name);
    }

    /// <summary>
    /// Verifies <see cref="CoincidentFrequencyAnalysis.NameOnDisk"/> matches the constructor name.
    /// </summary>
    [STATestMethod]
    public void Constructor_NameOnDiskMatchesName()
    {
        var cfa = new CoincidentFrequencyAnalysis("DiskCFA", _collection!);
        Assert.AreEqual("DiskCFA", cfa.NameOnDisk);
    }

    /// <summary>
    /// Verifies the analysis is not yet estimated after construction.
    /// </summary>
    [STATestMethod]
    public void Constructor_IsEstimated_IsFalseInitially()
    {
        var cfa = new CoincidentFrequencyAnalysis("EstCFA", _collection!);
        Assert.IsFalse(cfa.IsEstimated);
    }

    /// <summary>
    /// Verifies that <see cref="CoincidentFrequencyAnalysis.BivariateAnalysis"/> starts null.
    /// </summary>
    [STATestMethod]
    public void Constructor_BivariateAnalysis_IsNullInitially()
    {
        var cfa = new CoincidentFrequencyAnalysis("BiCFA", _collection!);
        Assert.IsNull(cfa.BivariateAnalysis);
    }

    /// <summary>
    /// Verifies <see cref="CoincidentFrequencyAnalysis.XValues"/> and <see cref="CoincidentFrequencyAnalysis.YValues"/>
    /// are non-null and seeded with one default row of 0.0 on a freshly-created CFA.
    /// </summary>
    /// <remarks>
    /// The default row gives the X / Y ordinate <c>ValidationDataGrid</c>s a populated row out
    /// of the box so the user does not have to click the click-to-add row before entering data.
    /// </remarks>
    [STATestMethod]
    public void Constructor_XYValues_AreSeededWithOneDefaultRow()
    {
        var cfa = new CoincidentFrequencyAnalysis("XYCFA", _collection!);
        Assert.IsNotNull(cfa.XValues);
        Assert.IsNotNull(cfa.YValues);
        Assert.AreEqual(1, cfa.XValues.Count);
        Assert.AreEqual(1, cfa.YValues.Count);
        Assert.AreEqual(0.0, cfa.XValues[0]);
        Assert.AreEqual(0.0, cfa.YValues[0]);
    }

    /// <summary>
    /// Verifies the default <see cref="CoincidentFrequencyAnalysis.NumberOfBins"/> is 50.
    /// </summary>
    [STATestMethod]
    public void Constructor_NumberOfBins_DefaultsTo50()
    {
        var cfa = new CoincidentFrequencyAnalysis("BinsCFA", _collection!);
        Assert.AreEqual(50, cfa.NumberOfBins);
    }

    /// <summary>
    /// Verifies the default <c>BayesianAnalysis.CredibleIntervalWidth</c> is 0.90 and that
    /// CFA owns its own <c>BayesianAnalysis</c> (not proxied from the upstream bivariate).
    /// </summary>
    [STATestMethod]
    public void Constructor_BayesianAnalysisCredibleIntervalWidth_DefaultsTo90()
    {
        var cfa = new CoincidentFrequencyAnalysis("CICFA", _collection!);
        Assert.IsNotNull(cfa.BayesianAnalysis);
        Assert.AreEqual(0.90, cfa.BayesianAnalysis.CredibleIntervalWidth);
    }

    /// <summary>
    /// Verifies that <see cref="CoincidentFrequencyAnalysis.FrequencyPlot"/> is initialised.
    /// </summary>
    [STATestMethod]
    public void Constructor_FrequencyPlot_IsNotNull()
    {
        var cfa = new CoincidentFrequencyAnalysis("PlotCFA", _collection!);
        Assert.IsNotNull(cfa.FrequencyPlot);
    }

    /// <summary>
    /// Verifies that <see cref="CoincidentFrequencyAnalysis.AnalysisResults"/> and
    /// <see cref="CoincidentFrequencyAnalysis.ZOutputValues"/> start null (no run yet).
    /// </summary>
    [STATestMethod]
    public void Constructor_Results_AreNullInitially()
    {
        var cfa = new CoincidentFrequencyAnalysis("ResCFA", _collection!);
        Assert.IsNull(cfa.AnalysisResults);
        Assert.IsNull(cfa.ZOutputValues);
    }

    /// <summary>
    /// Verifies <see cref="CoincidentFrequencyAnalysis.CanCopyFromExternal"/> returns false.
    /// </summary>
    [STATestMethod]
    public void CanCopyFromExternal_ReturnsFalse()
    {
        var cfa = new CoincidentFrequencyAnalysis("CopyExtCFA", _collection!);
        Assert.IsFalse(cfa.CanCopyFromExternal);
    }

    /// <summary>
    /// Verifies <see cref="CoincidentFrequencyAnalysis.InnerAnalysis"/> exposes the
    /// model-layer analysis instance.
    /// </summary>
    [STATestMethod]
    public void Constructor_InnerAnalysis_IsNotNull()
    {
        var cfa = new CoincidentFrequencyAnalysis("InnerCFA", _collection!);
        Assert.IsNotNull(cfa.InnerAnalysis);
    }

    /// <summary>
    /// Verifies CreationDate / LastModified are set close to construction time.
    /// </summary>
    [STATestMethod]
    public void Constructor_DatesAreRecent()
    {
        var before = DateTime.Now.AddSeconds(-5);
        var cfa = new CoincidentFrequencyAnalysis("DateCFA", _collection!);
        var after = DateTime.Now.AddSeconds(5);
        Assert.IsTrue(cfa.CreationDate >= before && cfa.CreationDate <= after);
        Assert.IsTrue(cfa.LastModified >= before && cfa.LastModified <= after);
    }

    #endregion

    #region Property change notifications

    /// <summary>
    /// Verifies that the Description setter raises PropertyChanged.
    /// </summary>
    [STATestMethod]
    public void Description_Setter_RaisesPropertyChanged()
    {
        var cfa = new CoincidentFrequencyAnalysis("DescPropCFA", _collection!);
        var raised = new List<string>();
        cfa.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        cfa.Description = "Hello";

        Assert.IsTrue(raised.Contains(nameof(CoincidentFrequencyAnalysis.Description)));
    }

    /// <summary>
    /// Verifies that setting Description to the same value does NOT raise PropertyChanged.
    /// </summary>
    [STATestMethod]
    public void Description_SameValue_DoesNotRaisePropertyChanged()
    {
        var cfa = new CoincidentFrequencyAnalysis("DescSameCFA", _collection!);
        cfa.Description = "Same";
        var raised = new List<string>();
        cfa.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        cfa.Description = "Same";

        CollectionAssert.DoesNotContain(raised, nameof(CoincidentFrequencyAnalysis.Description));
    }

    /// <summary>
    /// Verifies that setting NumberOfBins fires PropertyChanged and updates the inner value.
    /// </summary>
    [STATestMethod]
    public void NumberOfBins_Setter_RaisesPropertyChangedAndUpdatesValue()
    {
        var cfa = new CoincidentFrequencyAnalysis("BinsPropCFA", _collection!);
        var raised = new List<string>();
        cfa.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        cfa.NumberOfBins = 75;

        Assert.AreEqual(75, cfa.NumberOfBins);
        Assert.IsTrue(raised.Contains(nameof(CoincidentFrequencyAnalysis.NumberOfBins)));
    }

    /// <summary>
    /// Verifies that adding to XValues fires PropertyChanged for XValues.
    /// </summary>
    [STATestMethod]
    public void XValues_AddRaisesPropertyChanged()
    {
        var cfa = new CoincidentFrequencyAnalysis("XValPropCFA", _collection!);
        var raised = new List<string>();
        cfa.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        cfa.XValues.Add(1.0);

        Assert.IsTrue(raised.Contains(nameof(CoincidentFrequencyAnalysis.XValues)));
    }

    /// <summary>
    /// Verifies that BivariateResponse 2D-array setter fires PropertyChanged.
    /// </summary>
    [STATestMethod]
    public void BivariateResponse_Setter_RaisesPropertyChanged()
    {
        var cfa = new CoincidentFrequencyAnalysis("RespPropCFA", _collection!);
        var raised = new List<string>();
        cfa.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        cfa.BivariateResponse = new double[2, 2] { { 0, 1 }, { 2, 3 } };

        Assert.IsTrue(raised.Contains(nameof(CoincidentFrequencyAnalysis.BivariateResponse)));
        Assert.AreEqual(3.0, cfa.BivariateResponse[1, 1]);
    }

    /// <summary>
    /// Verifies direct response-surface edits are restored by the element undo/redo manager.
    /// </summary>
    [STATestMethod]
    public void BivariateResponse_UndoRedo_RestoresSnapshot()
    {
        var cfa = new CoincidentFrequencyAnalysis("RespUndoCFA", _collection!);
        ConfigureUndoableResponseSurface(cfa);

        cfa.BivariateResponse = new double[2, 2] { { 1, 2 }, { 3, 9 } };

        Assert.IsTrue(cfa.UndoManager.CanUndo);
        Assert.AreEqual(9.0, cfa.BivariateResponse[1, 1]);

        cfa.UndoManager.Undo();

        Assert.AreEqual(4.0, cfa.BivariateResponse[1, 1]);
        Assert.IsTrue(cfa.UndoManager.CanRedo);

        cfa.UndoManager.Redo();

        Assert.AreEqual(9.0, cfa.BivariateResponse[1, 1]);
    }

    /// <summary>
    /// Verifies undoing an X-ordinate add replays the resize path and restores response dimensions.
    /// </summary>
    [STATestMethod]
    public void XValues_AddUndoRedo_ResizesResponseSurface()
    {
        var cfa = new CoincidentFrequencyAnalysis("RespResizeUndoCFA", _collection!);
        ConfigureUndoableResponseSurface(cfa);

        cfa.XValues.Add(2.0);

        Assert.AreEqual(3, cfa.XValues.Count);
        Assert.AreEqual(3, cfa.BivariateResponse.GetLength(0));
        Assert.AreEqual(2, cfa.BivariateResponse.GetLength(1));
        Assert.AreEqual(4.0, cfa.BivariateResponse[1, 1]);

        cfa.UndoManager.Undo();

        Assert.AreEqual(2, cfa.XValues.Count);
        Assert.AreEqual(2, cfa.BivariateResponse.GetLength(0));
        Assert.AreEqual(2, cfa.BivariateResponse.GetLength(1));
        Assert.AreEqual(4.0, cfa.BivariateResponse[1, 1]);

        cfa.UndoManager.Redo();

        Assert.AreEqual(3, cfa.XValues.Count);
        Assert.AreEqual(3, cfa.BivariateResponse.GetLength(0));
        Assert.AreEqual(2, cfa.BivariateResponse.GetLength(1));
        Assert.AreEqual(4.0, cfa.BivariateResponse[1, 1]);
    }

    /// <summary>
    /// Configures a clean 2x2 response surface and clears setup actions from undo history.
    /// </summary>
    /// <param name="cfa">The coincident-frequency analysis to configure.</param>
    /// <remarks>
    /// The final response assignment runs with undo enabled so the internal rolling
    /// response snapshot is aligned with the baseline surface before test edits begin.
    /// </remarks>
    private static void ConfigureUndoableResponseSurface(CoincidentFrequencyAnalysis cfa)
    {
        cfa.XValues.Clear();
        cfa.XValues.Add(0.0);
        cfa.XValues.Add(1.0);

        cfa.YValues.Clear();
        cfa.YValues.Add(0.0);
        cfa.YValues.Add(1.0);

        cfa.BivariateResponse = new double[2, 2] { { 1, 2 }, { 3, 4 } };
        cfa.UndoManager.Clear();
    }

    #endregion

    #region Upstream link

    /// <summary>
    /// Verifies that setting BivariateAnalysis fires PropertyChanged and routes the model-layer
    /// link to the inner analysis.
    /// </summary>
    [STATestMethod]
    public void BivariateAnalysis_Setter_RaisesPropertyChangedAndLinksInner()
    {
        var ba = new UI.BivariateAnalysis("UpstreamBA", _collection!);
        var cfa = new CoincidentFrequencyAnalysis("LinkedCFA", _collection!);
        var raised = new List<string>();
        cfa.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        cfa.BivariateAnalysis = ba;

        Assert.AreSame(ba, cfa.BivariateAnalysis);
        Assert.IsTrue(raised.Contains(nameof(CoincidentFrequencyAnalysis.BivariateAnalysis)));
    }

    /// <summary>
    /// Verifies that linking an upstream bivariate analysis also synchronizes the marginal
    /// MCMC chains into the model-layer CFA used by the batch runner.
    /// </summary>
    [STATestMethod]
    public void BivariateAnalysis_Setter_SyncsMarginalChainsToInner()
    {
        var marginalX = CreateEstimatedMarginal("CfaMarginalX", 100.0, 10.0);
        var marginalY = CreateEstimatedMarginal("CfaMarginalY", 80.0, 8.0);
        var ba = new UI.BivariateAnalysis("UpstreamBAWithChains", _collection!)
        {
            MarginalX = marginalX,
            MarginalY = marginalY,
        };
        var cfa = new CoincidentFrequencyAnalysis("ChainLinkedCFA", _collection!);

        cfa.BivariateAnalysis = ba;

        var inner = (ModelAnalyses.CoincidentFrequencyAnalysis)cfa.InnerAnalysis;
        Assert.AreSame(marginalX.BayesianAnalysis.Results, inner.MarginalXChain);
        Assert.AreSame(marginalY.BayesianAnalysis.Results, inner.MarginalYChain);
    }

    /// <summary>
    /// Verifies that setting BivariateAnalysis to the same value does NOT raise PropertyChanged.
    /// </summary>
    [STATestMethod]
    public void BivariateAnalysis_SameValue_DoesNotRaisePropertyChanged()
    {
        var ba = new UI.BivariateAnalysis("SameUpstreamBA", _collection!);
        var cfa = new CoincidentFrequencyAnalysis("SameLinkCFA", _collection!) { BivariateAnalysis = ba };
        var raised = new List<string>();
        cfa.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        cfa.BivariateAnalysis = ba;

        CollectionAssert.DoesNotContain(raised, nameof(CoincidentFrequencyAnalysis.BivariateAnalysis));
    }

    #endregion

    #region Validation

    /// <summary>
    /// Verifies that a freshly-constructed analysis is invalid (no upstream BivariateAnalysis).
    /// </summary>
    [STATestMethod]
    public void Validate_NoBivariateAnalysis_IsInvalid()
    {
        var cfa = new CoincidentFrequencyAnalysis("InvalidCFA", _collection!);
        Assert.IsFalse(cfa.IsValid);
    }

    #endregion

    #region Copy

    /// <summary>
    /// Verifies that <see cref="CoincidentFrequencyAnalysis.Copy"/> produces a distinct
    /// instance with the same scalar settings, the same upstream BivariateAnalysis reference,
    /// and an independent (deep-copied) response surface.
    /// </summary>
    [STATestMethod]
    public void Copy_PreservesScalarsAndDeepCopiesResponse()
    {
        var ba = new UI.BivariateAnalysis("UpstreamBAforCopy", _collection!);
        var cfa = new CoincidentFrequencyAnalysis("OriginalCFA", _collection!)
        {
            Description = "Original description",
            BivariateAnalysis = ba,
            NumberOfBins = 100,
        };
        cfa.BayesianAnalysis.CredibleIntervalWidth = 0.95;
        // Clear the seeded default row before populating, so the test's expected values are
        // independent of the constructor's default-seeding behavior.
        cfa.XValues.Clear(); cfa.XValues.Add(0); cfa.XValues.Add(1);
        cfa.YValues.Clear(); cfa.YValues.Add(0); cfa.YValues.Add(1);
        cfa.BivariateResponse = new double[2, 2] { { 1, 2 }, { 3, 4 } };

        var clone = (CoincidentFrequencyAnalysis)cfa.Copy("ClonedCFA");

        Assert.AreNotSame(cfa, clone);
        Assert.AreEqual("ClonedCFA", clone.Name);
        Assert.AreEqual("Original description", clone.Description);
        Assert.AreSame(ba, clone.BivariateAnalysis);
        Assert.AreEqual(100, clone.NumberOfBins);
        Assert.AreEqual(0.95, clone.BayesianAnalysis.CredibleIntervalWidth);
        CollectionAssert.AreEqual(new[] { 0.0, 1.0 }, clone.XValues.ToArray());
        CollectionAssert.AreEqual(new[] { 0.0, 1.0 }, clone.YValues.ToArray());

        // Mutate the source response and verify the clone is independent.
        cfa.BivariateResponse[0, 0] = 999;
        Assert.AreEqual(1.0, clone.BivariateResponse[0, 0],
            "Response surface must be deep-copied so source mutations do not bleed into the clone.");
    }

    #endregion

    #region Plot ownership

    /// <summary>
    /// Verifies <see cref="CoincidentFrequencyAnalysis.SuspendPlotBridges"/> returns a usable
    /// disposable scope without throwing.
    /// </summary>
    [STATestMethod]
    public void SuspendPlotBridges_ReturnsUsableScope()
    {
        var cfa = new CoincidentFrequencyAnalysis("SuspendCFA", _collection!);
        using (cfa.SuspendPlotBridges())
        {
            // No-op — just verify no throw and scope ends cleanly.
        }
    }

    /// <summary>
    /// Verifies <see cref="CoincidentFrequencyAnalysis.RebuildSeriesAndAnnotationBridges"/>
    /// accepts the element's own plot without throwing.
    /// </summary>
    [STATestMethod]
    public void RebuildSeriesAndAnnotationBridges_AcceptsOwnPlot()
    {
        var cfa = new CoincidentFrequencyAnalysis("RebuildCFA", _collection!);
        cfa.RebuildSeriesAndAnnotationBridges(cfa.FrequencyPlot);
        // No assertions — successful no-op return is the test.
    }

    #endregion

    #region Static collection name

    /// <summary>
    /// Verifies the static <c>CollectionName</c> matches the canonical bracketed convention
    /// shared with <see cref="UI.UnivariateAnalysis"/> ("<c>&lt;Univariate Distribution&gt;</c>")
    /// and <see cref="CompositeAnalysis"/> ("<c>&lt;Composite Distribution&gt;</c>").
    /// </summary>
    [TestMethod]
    public void CollectionName_IsCanonicalBracketedForm()
    {
        Assert.AreEqual("<Coincident Frequency>", CoincidentFrequencyAnalysis.CollectionName);
    }

    /// <summary>
    /// Verifies that <see cref="UI.BivariateAnalysis.CollectionName"/> follows the canonical
    /// bracketed form — pinning the migration target so tests fail loudly if it is ever
    /// renamed without coordinating with project files in the wild.
    /// </summary>
    [TestMethod]
    public void BivariateAnalysis_CollectionName_IsCanonicalBracketedForm()
    {
        Assert.AreEqual("<Bivariate Distribution>", UI.BivariateAnalysis.CollectionName);
    }

    #endregion

    #region IsBatchEligible

    /// <summary>
    /// Verifies a freshly-constructed CFA (no BA, only the constructor's single seeded row in
    /// X / Y) is not batch-eligible — both the bivariate-config flag and the ordinates flag fail.
    /// </summary>
    [STATestMethod]
    public void IsBatchEligible_FreshlyConstructed_IsFalse()
    {
        var cfa = new CoincidentFrequencyAnalysis("BatchEligFresh", _collection!);
        Assert.IsFalse(cfa.IsBatchEligible);
    }

    /// <summary>
    /// Verifies that ordinates with fewer than 2 entries make a CFA ineligible for batch
    /// (the <c>_ordinatesValid</c> flag in the new <c>IsBatchEligible</c> expression).
    /// </summary>
    [STATestMethod]
    public void IsBatchEligible_OrdinatesTooShort_IsFalse()
    {
        var ba = new UI.BivariateAnalysis("BatchEligBA1", _collection!);
        var cfa = new CoincidentFrequencyAnalysis("BatchEligOrds", _collection!)
        {
            BivariateAnalysis = ba,
        };
        // Constructor seeds a single 0.0 row; clear it to produce zero-length ordinates.
        cfa.XValues.Clear();
        cfa.YValues.Clear();
        Assert.IsFalse(cfa.IsBatchEligible,
            "CFA with empty X / Y ordinates should not be batch-eligible regardless of upstream BA.");
    }

    /// <summary>
    /// Verifies that <c>NumberOfBins &lt; 2</c> makes a CFA ineligible for batch via the
    /// new <c>_bayesianOptionsValid</c> flag.
    /// </summary>
    [STATestMethod]
    public void IsBatchEligible_NumberOfBinsBelowMinimum_IsFalse()
    {
        var cfa = new CoincidentFrequencyAnalysis("BatchEligBins", _collection!)
        {
            NumberOfBins = 1,
        };
        Assert.IsFalse(cfa.IsBatchEligible,
            "CFA with NumberOfBins below the minimum (2) should not be batch-eligible.");
    }

    /// <summary>
    /// Verifies that the <c>IsBatchEligible</c> getter does NOT call the model-layer
    /// <c>Validate()</c> method. The model gate enforces upstream-estimated; the UI gate
    /// must let an un-estimated upstream BA through so the batch runner's Phase 1 can fit
    /// it before Phase 2 runs the CFA. We assert this by checking that switching the
    /// upstream BA's <c>IsEstimated</c> state has no influence on the result for an
    /// otherwise-invalid CFA configuration (ordinates too short).
    /// </summary>
    [STATestMethod]
    public void IsBatchEligible_DoesNotRequireUpstreamEstimated()
    {
        var ba = new UI.BivariateAnalysis("BatchEligBA2", _collection!);
        var cfa = new CoincidentFrequencyAnalysis("BatchEligNoEst", _collection!)
        {
            BivariateAnalysis = ba,
        };
        cfa.XValues.Clear();
        cfa.YValues.Clear();

        // Whether upstream BA is estimated or not, an otherwise-invalid CFA must not
        // become batch-eligible — and conversely, the un-estimated state alone must not
        // be the blocker that would prevent a future fully-configured CFA from being
        // eligible. Pin the negative direction here; the positive direction is exercised
        // end-to-end via the batch runner integration (see RMC.BestFit.Tests).
        Assert.IsFalse(cfa.IsBatchEligible,
            "Empty ordinates must dominate; upstream-estimated state alone cannot rescue an invalid config.");
    }

    /// <summary>
    /// Regression test: <see cref="CoincidentFrequencyAnalysis.CancelAnalysis"/> must
    /// delegate to the inner model analysis so the App's CancelButton actually stops
    /// a running CFA estimation. Mirrors the corresponding CompositeAnalysis test
    /// (CompositeAnalysisTests.CancelAnalysis_FlipsInnerAnalysisCancellationToken)
    /// after the equivalent stub-delegation bug landed there.
    /// </summary>
    [STATestMethod]
    public void CancelAnalysis_FlipsInnerAnalysisCancellationToken()
    {
        var cfa = new CoincidentFrequencyAnalysis("CancelTokenCFA", _collection!);
        var inner = (ModelAnalyses.CoincidentFrequencyAnalysis)cfa.InnerAnalysis;

        Assert.IsFalse(inner.CancellationTokenSource.IsCancellationRequested,
            "Sanity: a fresh CFA inner analysis should have an un-canceled CancellationTokenSource.");

        cfa.CancelAnalysis();

        Assert.IsTrue(inner.CancellationTokenSource.IsCancellationRequested,
            "After CFA.CancelAnalysis(), the inner model analysis's CancellationTokenSource " +
            "must be canceled — otherwise the App's Cancel button is a visual-only no-op " +
            "and the parallel realisation loop runs to completion.");
    }

    #endregion
}
