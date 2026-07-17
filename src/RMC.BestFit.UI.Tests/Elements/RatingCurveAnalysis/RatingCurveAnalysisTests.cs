using DatabaseManager;
using FrameworkInterfaces;
using FrameworkInterfaces.Messaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RMC.BestFit.UI;
using System.Data;
using System.IO;
using System.Xml.Linq;

namespace RMC.BestFit.UI.Tests.Elements.RatingCurveAnalysis;

/// <summary>
/// Unit tests for <see cref="UI.RatingCurveAnalysis"/>, the UI wrapper for rating curve analysis.
/// </summary>
/// <remarks>
/// All tests require STA thread because the constructor creates OxyPlot WPF Plot objects.
/// Tests focus on: constructor defaults, property change notifications, IsEstimated flag,
/// StageData/DischargeData, and plot initialization. Save/Open/Delete are excluded (require SQLite).
/// </remarks>
[TestClass]
public class RatingCurveAnalysisTests
{
    private static RatingCurveAnalysisCollection? _collection;

    /// <summary>
    /// Creates a shared collection backed by the singleton BestFitProject.
    /// </summary>
    [ClassInitialize]
    public static void ClassInitialize(TestContext _)
    {
        _collection = new RatingCurveAnalysisCollection(BestFitProject.GetInstance());
    }

    /// <summary>
    /// Verifies that the constructor stores the provided name.
    /// </summary>
    [STATestMethod]
    public void Constructor_StoresName()
    {
        var rca = new UI.RatingCurveAnalysis("TestRCA", _collection!);

        Assert.AreEqual("TestRCA", rca.Name);
    }

    /// <summary>
    /// Verifies that <see cref="UI.RatingCurveAnalysis.NameOnDisk"/> matches the constructor name.
    /// </summary>
    [STATestMethod]
    public void Constructor_NameOnDiskMatchesName()
    {
        var rca = new UI.RatingCurveAnalysis("DiskRCA", _collection!);

        Assert.AreEqual("DiskRCA", rca.NameOnDisk);
    }

    /// <summary>
    /// Verifies that <see cref="UI.RatingCurveAnalysis.IsEstimated"/> is <c>false</c> after construction.
    /// </summary>
    [STATestMethod]
    public void Constructor_IsEstimated_IsFalseInitially()
    {
        var rca = new UI.RatingCurveAnalysis("EstRCA", _collection!);

        Assert.IsFalse(rca.IsEstimated);
    }

    /// <summary>
    /// Verifies that <see cref="UI.RatingCurveAnalysis.StageData"/> is <c>null</c> after construction.
    /// </summary>
    [STATestMethod]
    public void Constructor_StageData_IsNullInitially()
    {
        var rca = new UI.RatingCurveAnalysis("StageRCA", _collection!);

        Assert.IsNull(rca.StageData);
    }

    /// <summary>
    /// Verifies that <see cref="UI.RatingCurveAnalysis.DischargeData"/> is <c>null</c> after construction.
    /// </summary>
    [STATestMethod]
    public void Constructor_DischargeData_IsNullInitially()
    {
        var rca = new UI.RatingCurveAnalysis("DischargeRCA", _collection!);

        Assert.IsNull(rca.DischargeData);
    }

    /// <summary>
    /// Verifies that <see cref="UI.RatingCurveAnalysis.BayesianAnalysis"/> is not null after construction.
    /// </summary>
    [STATestMethod]
    public void Constructor_BayesianAnalysis_IsNotNull()
    {
        var rca = new UI.RatingCurveAnalysis("BaRCA", _collection!);

        Assert.IsNotNull(rca.BayesianAnalysis);
    }

    /// <summary>
    /// Verifies that <see cref="UI.RatingCurveAnalysis.CanCopyFromExternal"/> returns <c>false</c>.
    /// </summary>
    [STATestMethod]
    public void CanCopyFromExternal_ReturnsFalse()
    {
        var rca = new UI.RatingCurveAnalysis("CopyRCA", _collection!);

        Assert.IsFalse(rca.CanCopyFromExternal);
    }

    /// <summary>
    /// Verifies that all four plots are initialized by the constructor.
    /// </summary>
    [STATestMethod]
    public void Constructor_AllFourPlots_AreNotNull()
    {
        var rca = new UI.RatingCurveAnalysis("PlotsRCA", _collection!);

        Assert.IsNotNull(rca.RatingCurvePlot, "RatingCurvePlot must not be null.");
        Assert.IsNotNull(rca.ResidualPlot, "ResidualPlot must not be null.");
        Assert.IsNotNull(rca.ResidualHistogramPlot, "ResidualHistogramPlot must not be null.");
        Assert.IsNotNull(rca.ResidualQQPlot, "ResidualQQPlot must not be null.");
    }

    /// <summary>
    /// Verifies that <see cref="UI.RatingCurveAnalysis.BayesianPlots"/> is not null after construction.
    /// </summary>
    [STATestMethod]
    public void Constructor_BayesianPlots_IsNotNull()
    {
        var rca = new UI.RatingCurveAnalysis("BpRCA", _collection!);

        Assert.IsNotNull(rca.BayesianPlots);
    }

    /// <summary>
    /// Verifies that the Description setter raises PropertyChanged.
    /// </summary>
    [STATestMethod]
    public void Description_Setter_RaisesPropertyChanged()
    {
        var rca = new UI.RatingCurveAnalysis("PropRCA", _collection!);
        var raised = new List<string>();
        rca.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        rca.Description = "A description";

        Assert.IsTrue(raised.Contains(nameof(UI.RatingCurveAnalysis.Description)));
    }

    /// <summary>
    /// Verifies that setting Description to the same value does NOT raise PropertyChanged.
    /// </summary>
    [STATestMethod]
    public void Description_SameValue_DoesNotRaisePropertyChanged()
    {
        var rca = new UI.RatingCurveAnalysis("PropRCA2", _collection!);
        rca.Description = "Same";

        var raised = new List<string>();
        rca.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        rca.Description = "Same";

        CollectionAssert.DoesNotContain(raised, nameof(UI.RatingCurveAnalysis.Description));
    }

    /// <summary>
    /// Verifies that <see cref="UI.RatingCurveAnalysis.CreationDate"/> and <see cref="UI.RatingCurveAnalysis.LastModified"/>
    /// are within a few seconds of construction time.
    /// </summary>
    [STATestMethod]
    public void Constructor_DatesAreRecent()
    {
        var before = DateTime.Now.AddSeconds(-5);
        var rca = new UI.RatingCurveAnalysis("DateRCA", _collection!);
        var after = DateTime.Now.AddSeconds(5);

        Assert.IsTrue(rca.CreationDate >= before && rca.CreationDate <= after);
        Assert.IsTrue(rca.LastModified >= before && rca.LastModified <= after);
    }

    /// <summary>
    /// Verifies that <see cref="UI.RatingCurveAnalysis.CancelAnalysis"/> does not throw
    /// when no analysis is running.
    /// </summary>
    [STATestMethod]
    public void CancelAnalysis_WhenNotRunning_DoesNotThrow()
    {
        var rca = new UI.RatingCurveAnalysis("CancelRCA", _collection!);

        // Must not throw
        rca.CancelAnalysis();
    }

    /// <summary>
    /// Verifies that <see cref="UI.RatingCurveAnalysis.ClearResults"/> does not throw
    /// and leaves IsEstimated as false.
    /// </summary>
    [STATestMethod]
    public void ClearResults_WhenNotEstimated_DoesNotThrow()
    {
        var rca = new UI.RatingCurveAnalysis("ClearRCA", _collection!);

        // Must not throw
        rca.ClearResults();

        Assert.IsFalse(rca.IsEstimated);
    }

    /// <summary>
    /// Verifies that <see cref="UI.RatingCurveAnalysis.IsLegacyRatingCurveXml"/>
    /// returns <c>true</c> for the old additive-piecewise rating curve XML layout
    /// (parameter names: Coefficient (α), Exponent (β), Location (ξ), Sigma (σ)).
    /// </summary>
    [TestMethod]
    public void IsLegacyRatingCurveXml_OldAdditiveLayout_ReturnsTrue()
    {
        var xml = XElement.Parse(@"<RatingCurve NumberOfSegments=""1"" UseDefaultFlatPriors=""True"" UseJeffreysRuleForScale=""True"">
  <Parameters>
    <ModelParameter Name=""Coefficient (α)"" Value=""2.3359"" />
    <ModelParameter Name=""Exponent (β)"" Value=""2.2733"" />
    <ModelParameter Name=""Location (ξ)"" Value=""0.1813"" />
    <ModelParameter Name=""Sigma (σ)"" Value=""0.0491"" />
  </Parameters>
</RatingCurve>");

        Assert.IsTrue(UI.RatingCurveAnalysis.IsLegacyRatingCurveXml(xml));
    }

    /// <summary>
    /// Verifies that <see cref="UI.RatingCurveAnalysis.IsLegacyRatingCurveXml"/>
    /// returns <c>false</c> for the current BaRatin addition-mode 1-segment layout
    /// using <c>Zero-Flow Stage (h₁)</c> and per-control coefficient / exponent suffixes.
    /// </summary>
    [TestMethod]
    public void IsLegacyRatingCurveXml_NewLayout_ReturnsFalse()
    {
        var xml = XElement.Parse(@"<RatingCurve NumberOfSegments=""1"" UseDefaultFlatPriors=""True"" UseJeffreysRuleForScale=""True"">
  <Parameters>
    <ModelParameter Name=""Zero-Flow Stage (h₁)"" Value=""0"" />
    <ModelParameter Name=""Coefficient (α₁)"" Value=""0"" />
    <ModelParameter Name=""Exponent (β₁)"" Value=""2.0"" />
    <ModelParameter Name=""Scale (σ)"" Value=""0.5"" />
  </Parameters>
</RatingCurve>");

        Assert.IsFalse(UI.RatingCurveAnalysis.IsLegacyRatingCurveXml(xml));
    }

    /// <summary>
    /// Verifies null input is handled gracefully.
    /// </summary>
    [TestMethod]
    public void IsLegacyRatingCurveXml_NullInput_ReturnsFalse()
    {
        Assert.IsFalse(UI.RatingCurveAnalysis.IsLegacyRatingCurveXml(null!));
    }

    /// <summary>
    /// Verifies that an element missing the <c>Parameters</c> child is treated as
    /// non-legacy (defensive: avoids false-positive migration on corrupt XML).
    /// </summary>
    [TestMethod]
    public void IsLegacyRatingCurveXml_MissingParameters_ReturnsFalse()
    {
        var xml = XElement.Parse(@"<RatingCurve NumberOfSegments=""1"" />");

        Assert.IsFalse(UI.RatingCurveAnalysis.IsLegacyRatingCurveXml(xml));
    }

    /// <summary>
    /// Verifies that a multi-segment XML using the current BaRatin addition-mode
    /// layout (main-channel <c>Zero-Flow Stage (h₁)</c>, per-control
    /// <c>Coefficient (αₖ)</c> / <c>Exponent (βₖ)</c>, and per-control
    /// <c>Activation Stage (hₖ)</c>) is detected as non-legacy.
    /// </summary>
    [TestMethod]
    public void IsLegacyRatingCurveXml_MultiSegmentNewLayout_ReturnsFalse()
    {
        var xml = XElement.Parse(@"<RatingCurve NumberOfSegments=""3"">
  <Parameters>
    <ModelParameter Name=""Zero-Flow Stage (h₁)"" />
    <ModelParameter Name=""Coefficient (α₁)"" />
    <ModelParameter Name=""Exponent (β₁)"" />
    <ModelParameter Name=""Activation Stage (h₂)"" />
    <ModelParameter Name=""Coefficient (α₂)"" />
    <ModelParameter Name=""Exponent (β₂)"" />
    <ModelParameter Name=""Activation Stage (h₃)"" />
    <ModelParameter Name=""Coefficient (α₃)"" />
    <ModelParameter Name=""Exponent (β₃)"" />
    <ModelParameter Name=""Scale (σ)"" />
  </Parameters>
</RatingCurve>");

        Assert.IsFalse(UI.RatingCurveAnalysis.IsLegacyRatingCurveXml(xml));
    }

    /// <summary>
    /// Verifies that a v2.0 piecewise-without-continuity XML (shared
    /// <c>Zero-Flow Stage (ξ)</c> with free per-segment <c>Coefficient (αₖ)</c>) is
    /// detected as legacy. The current BaRatin-style model fits per-segment h₁ plus
    /// per-segment αₖ, with ξ₂/ξ₃ derived from continuity — the stored v2.0 parameter
    /// values cannot be reused because continuity was not enforced during their fit.
    /// </summary>
    [TestMethod]
    public void IsLegacyRatingCurveXml_V2PiecewiseWithoutContinuity_ReturnsTrue()
    {
        var xml = XElement.Parse(@"<RatingCurve NumberOfSegments=""3"">
  <Parameters>
    <ModelParameter Name=""Zero-Flow Stage (ξ)"" />
    <ModelParameter Name=""Coefficient (α1)"" />
    <ModelParameter Name=""Exponent (β1)"" />
    <ModelParameter Name=""Breakpoint (h2)"" />
    <ModelParameter Name=""Coefficient (α2)"" />
    <ModelParameter Name=""Exponent (β2)"" />
    <ModelParameter Name=""Breakpoint (h3)"" />
    <ModelParameter Name=""Coefficient (α3)"" />
    <ModelParameter Name=""Exponent (β3)"" />
    <ModelParameter Name=""Scale (σ)"" />
  </Parameters>
</RatingCurve>");

        Assert.IsTrue(UI.RatingCurveAnalysis.IsLegacyRatingCurveXml(xml));
    }

    /// <summary>
    /// Verifies that the transient NVE-style variant-3 XML (free per-segment
    /// <c>Location (ξ2)</c> / <c>Location (ξ3)</c>; αₖ derived from continuity — opposite
    /// of the current BaRatin-style parameterization) is detected as legacy.
    /// </summary>
    [TestMethod]
    public void IsLegacyRatingCurveXml_TransientNveVariant3Layout_ReturnsTrue()
    {
        var xml = XElement.Parse(@"<RatingCurve NumberOfSegments=""3"">
  <Parameters>
    <ModelParameter Name=""Location (ξ1)"" />
    <ModelParameter Name=""Coefficient (α1)"" />
    <ModelParameter Name=""Exponent (β1)"" />
    <ModelParameter Name=""Breakpoint (h2)"" />
    <ModelParameter Name=""Location (ξ2)"" />
    <ModelParameter Name=""Exponent (β2)"" />
    <ModelParameter Name=""Breakpoint (h3)"" />
    <ModelParameter Name=""Location (ξ3)"" />
    <ModelParameter Name=""Exponent (β3)"" />
    <ModelParameter Name=""Scale (σ)"" />
  </Parameters>
</RatingCurve>");

        Assert.IsTrue(UI.RatingCurveAnalysis.IsLegacyRatingCurveXml(xml));
    }

    /// <summary>
    /// Regression test: a SQLite project whose <c>RatingCurve</c> cell contains invalid XML
    /// must not cause <see cref="UI.RatingCurveAnalysis.Open(SQLiteManager)"/> to throw.
    /// The parse is guarded with try/catch so an unparseable cell falls back to default state
    /// instead of aborting Open() partway.
    /// </summary>
    [STATestMethod]
    public void Open_WithCorruptRatingCurveXml_DoesNotThrow()
    {
        string tempPath = Path.Combine(Path.GetTempPath(), $"rmcbf-rca-test-{Guid.NewGuid():N}.bestfit");
        try
        {
            BuildRatingCurveAnalysisDatabaseWithCorruptXml(tempPath, "CorruptRCA", "<not-a-valid-xml>");

            var analysis = new UI.RatingCurveAnalysis("CorruptRCA", _collection!);

            using (var sqlite = new SQLiteManager(tempPath))
            {
                analysis.Open(sqlite);
            }
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    #region Stage / Discharge date-alignment (inner-join) behavior

    /// <summary>
    /// Builds a TimeSeriesElement whose TimeSeries has ordinates at
    /// <paramref name="startDate"/>, startDate+1d, startDate+2d, …
    /// with values produced by <paramref name="values"/>.
    /// </summary>
    private static TimeSeriesElement MakeTimeSeriesElement(string name, DateTime startDate, double[] values)
    {
        var tsElem = new TimeSeriesElement(name);
        var ts = new Numerics.Data.TimeSeries(
            Numerics.Data.TimeInterval.OneDay, startDate, new double[values.Length]);
        for (int i = 0; i < values.Length; i++)
            ts[i] = new Numerics.Data.SeriesOrdinate<DateTime, double>(startDate.AddDays(i), values[i]);
        tsElem.TimeSeries = ts;
        return tsElem;
    }

    /// <summary>
    /// Returns the Messenger item whose Source is the supplied element and whose
    /// Code equals <paramref name="code"/>.
    /// </summary>
    /// <param name="source">The expected owner of the message.</param>
    /// <param name="code">The stable message code to find.</param>
    /// <returns>The matching message item, or <c>null</c> when no matching message is active.</returns>
    private static BasicMessageItem? MessengerMessage(object source, string code)
    {
        var all = Messenger.GetInstance().AllMessageItems();
        return all.OfType<BasicMessageItem>().FirstOrDefault(m => ReferenceEquals(m.Source, source) && m.Code == code);
    }

    /// <summary>
    /// Returns true if the Messenger currently holds a message whose Source is the
    /// supplied element and whose Code equals <paramref name="code"/>.
    /// </summary>
    /// <param name="source">The expected owner of the message.</param>
    /// <param name="code">The stable message code to find.</param>
    /// <returns><c>true</c> when a matching message is active; otherwise, <c>false</c>.</returns>
    private static bool MessengerHas(object source, string code)
    {
        return MessengerMessage(source, code) != null;
    }

    /// <summary>
    /// Two series with exactly matching dates and lengths: no alignment error, no partial-overlap warning.
    /// </summary>
    [STATestMethod]
    public void Validate_ExactMatchSeries_NoOverlapError_NoPartialOverlapWarning()
    {
        var rca = new UI.RatingCurveAnalysis("AlignExact", _collection!);
        var start = new DateTime(2000, 1, 1);
        var stageVals = Enumerable.Range(0, 30).Select(i => 1.0 + 0.1 * i).ToArray();
        var dischargeVals = stageVals.Select(h => 2.0 * Math.Pow(h - 0.2, 1.8)).ToArray();

        rca.StageData = MakeTimeSeriesElement("StageA", start, stageVals);
        rca.DischargeData = MakeTimeSeriesElement("DischargeA", start, dischargeVals);

        Assert.IsFalse(MessengerHas(rca, "RCA-ERR-010"),
            "Exact-match series must not raise the insufficient-overlap error.");
        Assert.IsFalse(MessengerHas(rca, "RCA-WRN-011"),
            "Exact-match series must not raise the partial-overlap warning.");
        Assert.AreEqual(30, rca.GetAlignedObservations().Count);
    }

    /// <summary>
    /// Two series sharing only 5 dates trigger the insufficient-overlap error.
    /// </summary>
    [STATestMethod]
    public void Validate_FewerThanTenCommonDates_AddsInsufficientOverlapError()
    {
        var rca = new UI.RatingCurveAnalysis("AlignTooFew", _collection!);
        var stageStart = new DateTime(2000, 1, 1);
        var dischargeStart = new DateTime(2000, 1, 25); // overlap = 6 days
        var stageVals = Enumerable.Range(0, 30).Select(i => 1.0 + 0.1 * i).ToArray();
        var dischargeVals = Enumerable.Range(0, 30).Select(i => 10.0 + 2.0 * i).ToArray();

        rca.StageData = MakeTimeSeriesElement("StageTooFew", stageStart, stageVals);
        rca.DischargeData = MakeTimeSeriesElement("DischargeTooFew", dischargeStart, dischargeVals);

        Assert.IsTrue(MessengerHas(rca, "RCA-ERR-010"),
            "Fewer than 10 common dates must raise the insufficient-overlap error.");
    }

    /// <summary>
    /// Two series with equal length but staggered dates (only 6 overlap) error out.
    /// </summary>
    [STATestMethod]
    public void Validate_StaggeredDatesLowOverlap_AddsInsufficientOverlapError()
    {
        var rca = new UI.RatingCurveAnalysis("AlignStaggered", _collection!);
        var stageStart = new DateTime(2000, 1, 1);
        var dischargeStart = new DateTime(2000, 1, 1).AddDays(14); // 20 - 14 = 6 overlap
        var stageVals = Enumerable.Range(0, 20).Select(i => 1.0 + 0.1 * i).ToArray();
        var dischargeVals = Enumerable.Range(0, 20).Select(i => 10.0 + 2.0 * i).ToArray();

        rca.StageData = MakeTimeSeriesElement("StageStaggered", stageStart, stageVals);
        rca.DischargeData = MakeTimeSeriesElement("DischargeStaggered", dischargeStart, dischargeVals);

        Assert.IsTrue(MessengerHas(rca, "RCA-ERR-010"),
            "Staggered series with 6 overlapping dates must error.");
    }

    /// <summary>
    /// Series of 100 vs 80 observations (all 80 discharge dates present in stage)
    /// raises the partial-overlap warning but NOT the overlap error.
    /// </summary>
    [STATestMethod]
    public void Validate_SubsetDates_AddsPartialOverlapWarning_NoOverlapError()
    {
        var rca = new UI.RatingCurveAnalysis("AlignLenDiff", _collection!);
        var start = new DateTime(2000, 1, 1);
        var stageVals = Enumerable.Range(0, 100).Select(i => 1.0 + 0.05 * i).ToArray();
        var dischargeVals = Enumerable.Range(0, 80).Select(i => 10.0 + 2.0 * i).ToArray();

        rca.StageData = MakeTimeSeriesElement("StageLen", start, stageVals);
        rca.DischargeData = MakeTimeSeriesElement("DischargeLen", start, dischargeVals);

        Assert.IsTrue(MessengerHas(rca, "RCA-WRN-011"),
            "Excluded stage dates should raise the partial-overlap warning.");
        Assert.IsFalse(MessengerHas(rca, "RCA-ERR-010"),
            "80 overlapping dates is well above the 10-pair minimum — no overlap error.");
        Assert.AreEqual(80, rca.GetAlignedObservations().Count);
    }

    /// <summary>
    /// Equal-length series with only 40 common dates raise the partial-overlap warning.
    /// </summary>
    [STATestMethod]
    public void Validate_EqualLengthFortyCommonDates_AddsPartialOverlapWarning()
    {
        var rca = new UI.RatingCurveAnalysis("AlignFortyCommon", _collection!);
        var stageStart = new DateTime(2000, 1, 1);
        var dischargeStart = stageStart.AddDays(60);
        var stageVals = Enumerable.Range(0, 100).Select(i => 1.0 + 0.05 * i).ToArray();
        var dischargeVals = Enumerable.Range(0, 100).Select(i => 10.0 + 2.0 * i).ToArray();

        rca.StageData = MakeTimeSeriesElement("StageForty", stageStart, stageVals);
        rca.DischargeData = MakeTimeSeriesElement("DischargeForty", dischargeStart, dischargeVals);

        var warning = MessengerMessage(rca, "RCA-WRN-011");

        Assert.IsFalse(MessengerHas(rca, "RCA-ERR-010"),
            "40 overlapping dates is above the 10-pair minimum, so no overlap error should be raised.");
        Assert.IsNotNull(warning,
            "Equal-length series with only 40 common dates should raise the partial-overlap warning.");
        StringAssert.Contains(warning!.Description, "40 aligned observations");
        StringAssert.Contains(warning.Description, "100 stage");
        StringAssert.Contains(warning.Description, "100 discharge");
        Assert.AreEqual(40, rca.GetAlignedObservations().Count);
    }

    /// <summary>
    /// Series of 100 vs 95 observations warns because the five unpaired stage dates are excluded.
    /// </summary>
    [STATestMethod]
    public void Validate_FivePercentLengthDiffWithExcludedDates_AddsPartialOverlapWarning()
    {
        var rca = new UI.RatingCurveAnalysis("AlignSmallDiff", _collection!);
        var start = new DateTime(2000, 1, 1);
        var stageVals = Enumerable.Range(0, 100).Select(i => 1.0 + 0.05 * i).ToArray();
        var dischargeVals = Enumerable.Range(0, 95).Select(i => 10.0 + 2.0 * i).ToArray();

        rca.StageData = MakeTimeSeriesElement("StageSmall", start, stageVals);
        rca.DischargeData = MakeTimeSeriesElement("DischargeSmall", start, dischargeVals);

        Assert.IsFalse(MessengerHas(rca, "RCA-ERR-010"),
            "95 overlapping dates must not error.");
        Assert.IsTrue(MessengerHas(rca, "RCA-WRN-011"),
            "Any excluded dates should raise the partial-overlap warning.");
    }

    #endregion

    /// <summary>
    /// Verifies that <see cref="UI.RatingCurveAnalysis.InnerAnalysis"/> is not null after construction.
    /// </summary>
    [STATestMethod]
    public void InnerAnalysis_IsNotNull()
    {
        var rca = new UI.RatingCurveAnalysis("InnerRCA", _collection!);
        Assert.IsNotNull(rca.InnerAnalysis);
    }

    /// <summary>
    /// Verifies that <see cref="UI.RatingCurveAnalysis.AnalysisResults"/> is null until the analysis runs.
    /// </summary>
    [STATestMethod]
    public void AnalysisResults_BeforeRun_IsNull()
    {
        var rca = new UI.RatingCurveAnalysis("ResRCA", _collection!);
        Assert.IsNull(rca.AnalysisResults);
    }

    /// <summary>
    /// Verifies that <see cref="UI.RatingCurveAnalysis.RaisePreviewSaved"/> does not throw or flip
    /// cancel when there are no PreviewObjectSaved subscribers.
    /// </summary>
    [STATestMethod]
    public void RaisePreviewSaved_NoSubscribers_DoesNotFlipCancel()
    {
        var rca = new UI.RatingCurveAnalysis("PrevRCA", _collection!);
        bool cancel = false;

        rca.RaisePreviewSaved(ref cancel);

        Assert.IsFalse(cancel);
    }

    /// <summary>
    /// Verifies that <see cref="UI.RatingCurveAnalysis.ElementImageResourceKey"/> returns the expected key.
    /// </summary>
    [STATestMethod]
    public void ElementImageResourceKey_ReturnsExpectedKey()
    {
        var rca = new UI.RatingCurveAnalysis("ImgKeyRCA", _collection!);
        Assert.AreEqual("RatingCurveAnalysisIcon", rca.ElementImageResourceKey);
    }

    /// <summary>
    /// Verifies that <see cref="UI.RatingCurveAnalysis.IsValid"/> is <c>false</c> on a freshly
    /// constructed instance because StageData and DischargeData have not been assigned.
    /// </summary>
    [STATestMethod]
    public void Constructor_IsValid_IsFalseInitiallyDueToMissingData()
    {
        var rca = new UI.RatingCurveAnalysis("IsValidRCA", _collection!);
        Assert.IsFalse(rca.IsValid);
    }

    /// <summary>
    /// Verifies that <see cref="UI.RatingCurveAnalysis.Name"/> setter raises PropertyChanged on rename.
    /// </summary>
    [STATestMethod]
    public void Name_Setter_RaisesPropertyChanged()
    {
        var rca = new UI.RatingCurveAnalysis("NameRCA", _collection!);
        var raised = new List<string>();
        rca.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        rca.Name = "NameRCA-Renamed";

        Assert.IsTrue(raised.Contains(nameof(UI.RatingCurveAnalysis.Name)));
        Assert.AreEqual("NameRCA-Renamed", rca.Name);
    }

    /// <summary>
    /// Verifies that <see cref="UI.RatingCurveAnalysis.RatingCurve"/> is not null after construction.
    /// </summary>
    [STATestMethod]
    public void RatingCurve_IsNotNull()
    {
        var rca = new UI.RatingCurveAnalysis("RCmodelRCA", _collection!);
        Assert.IsNotNull(rca.RatingCurve);
    }

    /// <summary>
    /// Builds a minimal SQLite <c>.bestfit</c> fixture with a corrupt <c>RatingCurve</c>
    /// XML cell. Referenced stage/discharge series are intentionally absent.
    /// </summary>
    private static void BuildRatingCurveAnalysisDatabaseWithCorruptXml(string path, string analysisName, string corruptXml)
    {
        var anTable = new DataTable("Rating Curve Analysis");
        anTable.Columns.Add("Name", typeof(string));
        anTable.Columns.Add("Description", typeof(string));
        anTable.Columns.Add("CreationDate", typeof(string));
        anTable.Columns.Add("LastModified", typeof(string));
        anTable.Columns.Add("StageData", typeof(string));
        anTable.Columns.Add("DischargeData", typeof(string));
        anTable.Columns.Add("RatingCurve", typeof(string));

        using (var sqlite = new SQLiteManager(path))
        {
            sqlite.Open();
            sqlite.SaveDataTable(anTable);

            var anView = sqlite.GetTableManager("Rating Curve Analysis");
            anView.AddRow();
            anView.EditCell(0, "Name", analysisName);
            anView.EditCell(0, "Description", "");
            anView.EditCell(0, "CreationDate", DateTime.Now.ToString("o"));
            anView.EditCell(0, "LastModified", DateTime.Now.ToString("o"));
            anView.EditCell(0, "StageData", "MissingStage");
            anView.EditCell(0, "DischargeData", "MissingDischarge");
            anView.EditCell(0, "RatingCurve", corruptXml);
            anView.ApplyEdits();
            sqlite.Close();
        }
    }
}
