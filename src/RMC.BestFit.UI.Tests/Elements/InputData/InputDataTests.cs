using FrameworkInterfaces;
using FrameworkInterfaces.Messaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Numerics.Data;
using RMC.BestFit.Models;
using RMC.BestFit.UI;
using System.Linq;

namespace RMC.BestFit.UI.Tests.Elements.InputData;

/// <summary>
/// Unit tests for <see cref="UI.InputData"/>, the project-level input data element.
/// </summary>
/// <remarks>
/// All tests require STA thread because the constructor creates OxyPlot WPF Plot objects.
/// Tests focus on: constructor defaults, property change notifications, UnitLabel/IndexLabel,
/// DataFrame, ExactDataMethod, and plot initialization.
/// Save/Open/Delete are excluded (require SQLite project file).
/// </remarks>
[TestClass]
public class InputDataTests
{
    private static InputDataCollection? _collection;

    /// <summary>
    /// Creates a shared collection backed by the singleton BestFitProject.
    /// </summary>
    [ClassInitialize]
    public static void ClassInitialize(TestContext _)
    {
        _collection = new InputDataCollection(BestFitProject.GetInstance());
    }

    /// <summary>
    /// Creates a time-series element with regular interval values for input-data validation tests.
    /// </summary>
    /// <param name="name">The time-series element name.</param>
    /// <param name="interval">The regular time interval assigned to the series.</param>
    /// <param name="startDate">The date of the first ordinate.</param>
    /// <param name="values">The ordinate values to load into the series.</param>
    /// <returns>A time-series element containing the supplied values.</returns>
    /// <remarks>
    /// The helper keeps block-series warning tests focused on InputData behavior rather than project setup.
    /// </remarks>
    private static TimeSeriesElement MakeTimeSeriesElement(string name, TimeInterval interval, DateTime startDate, double[] values)
    {
        var element = new TimeSeriesElement(name);
        element.TimeSeries = new TimeSeries(interval, startDate, values);
        return element;
    }

    /// <summary>
    /// Creates a time-series element with an intentionally omitted regular timestamp.
    /// </summary>
    /// <param name="name">The time-series element name.</param>
    /// <param name="startDate">The date of the first ordinate.</param>
    /// <param name="count">The number of calendar days spanned by the generated dates.</param>
    /// <param name="omittedDayIndex">The zero-based day index to omit from the generated series.</param>
    /// <returns>A time-series element whose declared interval is daily but whose dates contain a gap.</returns>
    /// <remarks>
    /// DSS records can represent missing periods by omitting ordinates rather than storing explicit NaN values.
    /// </remarks>
    private static TimeSeriesElement MakeDailyTimeSeriesWithGap(string name, DateTime startDate, int count, int omittedDayIndex)
    {
        var element = new TimeSeriesElement(name);
        var timeSeries = new TimeSeries(TimeInterval.OneDay);
        for (int i = 0; i < count; i++)
        {
            if (i == omittedDayIndex) continue;
            timeSeries.Add(new SeriesOrdinate<DateTime, double>(startDate.AddDays(i), i + 1d));
        }

        element.TimeSeries = timeSeries;
        return element;
    }

    /// <summary>
    /// Returns whether the global messenger contains a message from the supplied source and code.
    /// </summary>
    /// <param name="source">The element expected to own the message.</param>
    /// <param name="code">The stable message code to search for.</param>
    /// <returns><c>true</c> when a matching message is currently active; otherwise, <c>false</c>.</returns>
    /// <remarks>
    /// Filtering by source avoids interference from other tests that may also use the singleton messenger.
    /// </remarks>
    private static bool MessengerHas(object source, string code)
    {
        return Messenger.GetInstance().AllMessageItems().Any(m => ReferenceEquals(m.Source, source) && m.Code == code);
    }

    /// <summary>
    /// Verifies that the constructor stores the provided name.
    /// </summary>
    [STATestMethod]
    public void Constructor_StoresName()
    {
        var id = new UI.InputData("TestID", _collection!);

        Assert.AreEqual("TestID", id.Name);
    }

    /// <summary>
    /// Verifies that <see cref="UI.InputData.NameOnDisk"/> matches the constructor name.
    /// </summary>
    [STATestMethod]
    public void Constructor_NameOnDiskMatchesName()
    {
        var id = new UI.InputData("DiskID", _collection!);

        Assert.AreEqual("DiskID", id.NameOnDisk);
    }

    /// <summary>
    /// Verifies that <see cref="UI.InputData.DataFrame"/> is not null after construction
    /// (a default empty DataFrame is created).
    /// </summary>
    [STATestMethod]
    public void Constructor_DataFrame_IsNotNull()
    {
        var id = new UI.InputData("DfID", _collection!);

        Assert.IsNotNull(id.DataFrame);
    }

    /// <summary>
    /// Verifies that <see cref="UI.InputData.UnitLabel"/> defaults to "Value".
    /// </summary>
    [STATestMethod]
    public void Constructor_UnitLabel_DefaultIsValue()
    {
        var id = new UI.InputData("UnitID", _collection!);

        Assert.AreEqual("Value", id.UnitLabel);
    }

    /// <summary>
    /// Verifies that <see cref="UI.InputData.ExactDataMethod"/> defaults to Manual.
    /// </summary>
    [STATestMethod]
    public void Constructor_ExactDataMethod_DefaultIsManual()
    {
        var id = new UI.InputData("MethodID", _collection!);

        Assert.AreEqual(UI.InputData.ExactDataEntryType.Manual, id.ExactDataMethod);
    }

    /// <summary>
    /// Verifies that <see cref="UI.InputData.CanCopyFromExternal"/> is <c>true</c> when
    /// ExactDataMethod is Manual (the default).
    /// </summary>
    [STATestMethod]
    public void CanCopyFromExternal_Manual_ReturnsTrue()
    {
        var id = new UI.InputData("CopyID", _collection!);

        // Default is Manual, so CanCopyFromExternal should be true
        Assert.IsTrue(id.CanCopyFromExternal);
    }

    /// <summary>
    /// Verifies that <see cref="UI.InputData.CanCopyFromExternal"/> is <c>false</c> when
    /// ExactDataMethod is BlockSeries.
    /// </summary>
    [STATestMethod]
    public void CanCopyFromExternal_BlockSeries_ReturnsFalse()
    {
        var id = new UI.InputData("CopyBlockID", _collection!);
        id.ExactDataMethod = UI.InputData.ExactDataEntryType.BlockSeries;

        Assert.IsFalse(id.CanCopyFromExternal);
    }

    /// <summary>
    /// Verifies that the Description setter raises PropertyChanged.
    /// </summary>
    [STATestMethod]
    public void Description_Setter_RaisesPropertyChanged()
    {
        var id = new UI.InputData("PropID", _collection!);
        var raised = new List<string>();
        id.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        id.Description = "My description";

        Assert.IsTrue(raised.Contains(nameof(UI.InputData.Description)));
    }

    /// <summary>
    /// Verifies that setting Description to the same value does NOT raise PropertyChanged.
    /// </summary>
    [STATestMethod]
    public void Description_SameValue_DoesNotRaisePropertyChanged()
    {
        var id = new UI.InputData("PropID2", _collection!);
        id.Description = "Same";

        var raised = new List<string>();
        id.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        id.Description = "Same";

        CollectionAssert.DoesNotContain(raised, nameof(UI.InputData.Description));
    }

    /// <summary>
    /// Verifies that the UnitLabel setter raises PropertyChanged.
    /// </summary>
    [STATestMethod]
    public void UnitLabel_Setter_RaisesPropertyChanged()
    {
        var id = new UI.InputData("ULPropID", _collection!);
        var raised = new List<string>();
        id.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        id.UnitLabel = "Flow (CFS)";

        Assert.IsTrue(raised.Contains(nameof(UI.InputData.UnitLabel)));
    }

    /// <summary>
    /// Verifies that setting UnitLabel to the same value does NOT raise PropertyChanged.
    /// </summary>
    [STATestMethod]
    public void UnitLabel_SameValue_DoesNotRaisePropertyChanged()
    {
        var id = new UI.InputData("ULSamePropID", _collection!);
        id.UnitLabel = "Elevation (FT)";

        var raised = new List<string>();
        id.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        id.UnitLabel = "Elevation (FT)";

        CollectionAssert.DoesNotContain(raised, nameof(UI.InputData.UnitLabel));
    }

    /// <summary>
    /// Verifies that <see cref="UI.InputData.CreationDate"/> and <see cref="UI.InputData.LastModified"/>
    /// are within a few seconds of construction time.
    /// </summary>
    [STATestMethod]
    public void Constructor_DatesAreRecent()
    {
        var before = DateTime.Now.AddSeconds(-5);
        var id = new UI.InputData("DateID", _collection!);
        var after = DateTime.Now.AddSeconds(5);

        Assert.IsTrue(id.CreationDate >= before && id.CreationDate <= after);
        Assert.IsTrue(id.LastModified >= before && id.LastModified <= after);
    }

    /// <summary>
    /// Verifies that all 11 plots are initialized (not null) by the constructor.
    /// </summary>
    [STATestMethod]
    public void Constructor_AllElevenPlots_AreNotNull()
    {
        var id = new UI.InputData("AllPlotsID", _collection!);

        Assert.IsNotNull(id.ChronologyPlot, "ChronologyPlot must not be null.");
        Assert.IsNotNull(id.FrequencyPlot, "FrequencyPlot must not be null.");
        Assert.IsNotNull(id.SeasonalityPlot, "SeasonalityPlot must not be null.");
        Assert.IsNotNull(id.DensityPlot, "DensityPlot must not be null.");
        Assert.IsNotNull(id.HistogramPlot, "HistogramPlot must not be null.");
        Assert.IsNotNull(id.QQPlot, "QQPlot must not be null.");
        Assert.IsNotNull(id.ACFPlot, "ACFPlot must not be null.");
        Assert.IsNotNull(id.PACFPlot, "PACFPlot must not be null.");
        Assert.IsNotNull(id.MRLPlot, "MRLPlot must not be null.");
        Assert.IsNotNull(id.ModifiedScalePlot, "ModifiedScalePlot must not be null.");
        Assert.IsNotNull(id.ShapePlot, "ShapePlot must not be null.");
    }

    /// <summary>
    /// Verifies that the Name setter raises PropertyChanged.
    /// </summary>
    [STATestMethod]
    public void Name_Setter_RaisesPropertyChanged()
    {
        var id = new UI.InputData("OrigName", _collection!);
        var raised = new List<string>();
        id.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        id.Name = "NewName";

        Assert.IsTrue(raised.Contains(nameof(UI.InputData.Name)));
    }

    /// <summary>
    /// Verifies that the IndexLabel setter raises PropertyChanged for IndexLabel.
    /// </summary>
    [STATestMethod]
    public void IndexLabel_Setter_RaisesPropertyChanged()
    {
        var id = new UI.InputData("IdxLblID", _collection!);
        var raised = new List<string>();
        id.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        // Default is "Year", so use a distinct value to ensure setter fires.
        id.IndexLabel = "WaterYear";

        Assert.IsTrue(raised.Contains(nameof(UI.InputData.IndexLabel)));
        Assert.AreEqual("WaterYear", id.IndexLabel);
    }

    /// <summary>
    /// Verifies that setting IndexLabel to the same value does NOT raise PropertyChanged.
    /// </summary>
    [STATestMethod]
    public void IndexLabel_SameValue_DoesNotRaisePropertyChanged()
    {
        var id = new UI.InputData("IdxSameID", _collection!);
        id.IndexLabel = "Year"; // matches default, but normalize first
        var raised = new List<string>();
        id.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        id.IndexLabel = "Year"; // same as default — must not raise

        CollectionAssert.DoesNotContain(raised, nameof(UI.InputData.IndexLabel));
    }

    /// <summary>
    /// Verifies that the UseMultipleGrubbsBeckTest setter raises PropertyChanged.
    /// </summary>
    [STATestMethod]
    public void UseMultipleGrubbsBeckTest_Setter_RaisesPropertyChanged()
    {
        var id = new UI.InputData("MGBID", _collection!);
        var raised = new List<string>();
        id.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        id.UseMultipleGrubbsBeckTest = !id.UseMultipleGrubbsBeckTest;

        Assert.IsTrue(raised.Contains(nameof(UI.InputData.UseMultipleGrubbsBeckTest)));
    }

    /// <summary>
    /// Verifies that the Threshold setter raises PropertyChanged when set to a different value.
    /// </summary>
    [STATestMethod]
    public void Threshold_Setter_RaisesPropertyChanged()
    {
        var id = new UI.InputData("ThreshID", _collection!);
        var raised = new List<string>();
        id.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        id.Threshold = 100.0;

        Assert.IsTrue(raised.Contains(nameof(UI.InputData.Threshold)));
        Assert.AreEqual(100.0, id.Threshold, 1e-12);
    }

    /// <summary>
    /// Verifies that the MinStepsBetweenPeaks setter raises PropertyChanged.
    /// </summary>
    [STATestMethod]
    public void MinStepsBetweenPeaks_Setter_RaisesPropertyChanged()
    {
        var id = new UI.InputData("MinStepsID", _collection!);
        var raised = new List<string>();
        id.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        id.MinStepsBetweenPeaks = 5;

        Assert.IsTrue(raised.Contains(nameof(UI.InputData.MinStepsBetweenPeaks)));
        Assert.AreEqual(5, id.MinStepsBetweenPeaks);
    }

    /// <summary>
    /// Verifies that the Period setter raises PropertyChanged.
    /// </summary>
    [STATestMethod]
    public void Period_Setter_RaisesPropertyChanged()
    {
        var id = new UI.InputData("PeriodID", _collection!);
        var raised = new List<string>();
        id.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        id.Period = 12;

        Assert.IsTrue(raised.Contains(nameof(UI.InputData.Period)));
        Assert.AreEqual(12, id.Period);
    }

    /// <summary>
    /// Verifies that the StartMonth setter raises PropertyChanged.
    /// </summary>
    [STATestMethod]
    public void StartMonth_Setter_RaisesPropertyChanged()
    {
        var id = new UI.InputData("StartMID", _collection!);
        var raised = new List<string>();
        id.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        id.StartMonth = 5;

        Assert.IsTrue(raised.Contains(nameof(UI.InputData.StartMonth)));
        Assert.AreEqual(5, id.StartMonth);
    }

    /// <summary>
    /// Verifies that setting StartMonth to an out-of-range value (0 or 13) leaves IsValid false.
    /// </summary>
    [STATestMethod]
    public void StartMonth_OutOfRange_FlipsIsValidFalse()
    {
        var id = new UI.InputData("StartMOORID", _collection!);
        id.StartMonth = 13; // out of range — must flip _startMonthValid to false

        // IsValid is the aggregate; it must reflect the invalid month even if other facets are good.
        Assert.IsFalse(id.IsValid);
    }

    /// <summary>
    /// Verifies that the EndMonth setter raises PropertyChanged.
    /// </summary>
    [STATestMethod]
    public void EndMonth_Setter_RaisesPropertyChanged()
    {
        var id = new UI.InputData("EndMID", _collection!);
        var raised = new List<string>();
        id.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        id.EndMonth = 7;

        Assert.IsTrue(raised.Contains(nameof(UI.InputData.EndMonth)));
        Assert.AreEqual(7, id.EndMonth);
    }

    /// <summary>
    /// Verifies that setting EndMonth to an out-of-range value flips IsValid false.
    /// </summary>
    [STATestMethod]
    public void EndMonth_OutOfRange_FlipsIsValidFalse()
    {
        var id = new UI.InputData("EndMOORID", _collection!);
        id.EndMonth = 0; // out of range

        Assert.IsFalse(id.IsValid);
    }

    /// <summary>
    /// Verifies that the BlockFunction setter raises PropertyChanged.
    /// </summary>
    [STATestMethod]
    public void BlockFunction_Setter_RaisesPropertyChanged()
    {
        var id = new UI.InputData("BlockFID", _collection!);
        var initialValue = id.BlockFunction;
        var raised = new List<string>();
        id.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        // Pick a value distinct from the default to guarantee the setter fires.
        var values = (Numerics.Data.BlockFunctionType[])Enum.GetValues(typeof(Numerics.Data.BlockFunctionType));
        var distinct = values.First(v => !v.Equals(initialValue));
        id.BlockFunction = distinct;

        Assert.IsTrue(raised.Contains(nameof(UI.InputData.BlockFunction)));
    }

    /// <summary>
    /// Verifies that the TimeBlock setter raises PropertyChanged.
    /// </summary>
    [STATestMethod]
    public void TimeBlock_Setter_RaisesPropertyChanged()
    {
        var id = new UI.InputData("TimeBlockID", _collection!);
        var initialValue = id.TimeBlock;
        var raised = new List<string>();
        id.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        var values = (Numerics.Data.TimeBlockWindow[])Enum.GetValues(typeof(Numerics.Data.TimeBlockWindow));
        var distinct = values.First(v => !v.Equals(initialValue));
        id.TimeBlock = distinct;

        Assert.IsTrue(raised.Contains(nameof(UI.InputData.TimeBlock)));
    }

    /// <summary>
    /// Verifies that the SmoothingFunction setter raises PropertyChanged.
    /// </summary>
    [STATestMethod]
    public void SmoothingFunction_Setter_RaisesPropertyChanged()
    {
        var id = new UI.InputData("SmoothFID", _collection!);
        var initialValue = id.SmoothingFunction;
        var raised = new List<string>();
        id.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        var values = (Numerics.Data.SmoothingFunctionType[])Enum.GetValues(typeof(Numerics.Data.SmoothingFunctionType));
        var distinct = values.First(v => !v.Equals(initialValue));
        id.SmoothingFunction = distinct;

        Assert.IsTrue(raised.Contains(nameof(UI.InputData.SmoothingFunction)));
    }

    /// <summary>
    /// Verifies that USGSSiteNumber setter raises PropertyChanged.
    /// </summary>
    [STATestMethod]
    public void USGSSiteNumber_Setter_RaisesPropertyChanged()
    {
        var id = new UI.InputData("USGSID", _collection!);
        var raised = new List<string>();
        id.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        id.USGSSiteNumber = "01234567";

        Assert.IsTrue(raised.Contains(nameof(UI.InputData.USGSSiteNumber)));
        Assert.AreEqual("01234567", id.USGSSiteNumber);
    }

    /// <summary>
    /// Verifies that <see cref="UI.InputData.ElementImageResourceKey"/> returns the
    /// expected resource key constant used by the App for theme-aware icon lookup.
    /// </summary>
    [STATestMethod]
    public void ElementImageResourceKey_ReturnsExpectedKey()
    {
        var id = new UI.InputData("ImgKeyID", _collection!);
        Assert.AreEqual("InputDataIcon", id.ElementImageResourceKey);
    }

    /// <summary>
    /// Verifies that <see cref="UI.InputData.IsValid"/> is <c>false</c> on a freshly constructed
    /// instance because required labels and the minimum data count are not satisfied.
    /// </summary>
    [STATestMethod]
    public void Constructor_IsValid_HasReproducibleInitialState()
    {
        var id = new UI.InputData("IsValidID", _collection!);

        Assert.IsFalse(id.IsValid);
    }

    /// <summary>
    /// Verifies that <see cref="UI.InputData.RaisePreviewSaved"/> does not throw or flip cancel
    /// when there are no PreviewObjectSaved subscribers.
    /// </summary>
    [STATestMethod]
    public void RaisePreviewSaved_NoSubscribers_DoesNotFlipCancel()
    {
        var id = new UI.InputData("PrevID", _collection!);
        bool cancel = false;

        id.RaisePreviewSaved(ref cancel);

        Assert.IsFalse(cancel);
    }

    /// <summary>
    /// Verifies that <see cref="UI.InputData.IsTimeSeriesInputValid"/> returns <c>true</c> for
    /// the default Manual exact-data method (no time-series-input gate applies).
    /// </summary>
    [STATestMethod]
    public void IsTimeSeriesInputValid_ManualMode_ReturnsTrue()
    {
        var id = new UI.InputData("TSValidID", _collection!);
        // ExactDataMethod default is Manual.
        Assert.IsTrue(id.IsTimeSeriesInputValid());
    }

    /// <summary>
    /// Verifies that <see cref="UI.InputData.ClearTimeSeriesResults"/> does not throw when
    /// no time-series-derived data exists.
    /// </summary>
    [STATestMethod]
    public void ClearTimeSeriesResults_NoDerivedData_DoesNotThrow()
    {
        var id = new UI.InputData("ClearTSID", _collection!);

        // Must not throw — already in the cleared state.
        id.ClearTimeSeriesResults();
    }

    /// <summary>
    /// Verifies that clearing derived exact data resets the stale MGBT threshold.
    /// </summary>
    [STATestMethod]
    public void ClearTimeSeriesResults_DerivedMgbtMode_ResetsLowOutlierThreshold()
    {
        var id = new UI.InputData("ClearMgbtID", _collection!);
        id.ExactDataMethod = UI.InputData.ExactDataEntryType.BlockSeries;
        id.UseMultipleGrubbsBeckTest = true;
        for (int i = 1; i <= 10; i++)
        {
            id.DataFrame.ExactSeries.Add(new RMC.BestFit.Models.ExactData(i, i * 10d));
        }
        id.DataFrame.LowOutlierThreshold = 25d;
        id.DataFrame.SetLowOutliersFromThreshold();

        id.ClearTimeSeriesResults();

        Assert.AreEqual(0d, id.DataFrame.LowOutlierThreshold, 1e-12);
        Assert.AreEqual(0, id.DataFrame.NumberOfLowOutliers);
    }

    /// <summary>
    /// Verifies that clearing derived exact data preserves a user-defined low-outlier threshold.
    /// </summary>
    [STATestMethod]
    public void ClearTimeSeriesResults_DerivedManualThresholdMode_PreservesLowOutlierThreshold()
    {
        var id = new UI.InputData("ClearManualThresholdID", _collection!);
        id.ExactDataMethod = UI.InputData.ExactDataEntryType.BlockSeries;
        id.UseMultipleGrubbsBeckTest = false;
        for (int i = 1; i <= 10; i++)
        {
            id.DataFrame.ExactSeries.Add(new RMC.BestFit.Models.ExactData(i, i * 10d));
        }
        id.DataFrame.LowOutlierThreshold = 25d;
        id.DataFrame.SetLowOutliersFromThreshold();

        id.ClearTimeSeriesResults();

        Assert.AreEqual(25d, id.DataFrame.LowOutlierThreshold, 1e-12);
        Assert.AreEqual(0, id.DataFrame.NumberOfLowOutliers);
    }

    /// <summary>
    /// Verifies that the static <see cref="UI.InputData.RequiredColumns"/> dictionary contains
    /// the canonical required columns used during SQLite Save/Open for InputData rows.
    /// </summary>
    [TestMethod]
    public void RequiredColumns_ContainsCoreSchemaColumns()
    {
        var cols = UI.InputData.RequiredColumns;

        Assert.IsTrue(cols.ContainsKey("Name"), "Name column required.");
        Assert.IsTrue(cols.ContainsKey("Description"), "Description column required.");
        Assert.AreEqual(typeof(string), cols["Name"]);
        Assert.AreEqual(typeof(string), cols["Description"]);
    }

    /// <summary>
    /// Verifies that setting ExactDataMethod fires PropertyChanged.
    /// </summary>
    [STATestMethod]
    public void ExactDataMethod_Setter_RaisesPropertyChanged()
    {
        var id = new UI.InputData("EDMID", _collection!);
        var raised = new List<string>();
        id.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        id.ExactDataMethod = UI.InputData.ExactDataEntryType.BlockSeries;

        Assert.IsTrue(raised.Contains(nameof(UI.InputData.ExactDataMethod)));
        Assert.AreEqual(UI.InputData.ExactDataEntryType.BlockSeries, id.ExactDataMethod);
    }

    /// <summary>
    /// Verifies that <see cref="UI.InputData.CanCopyFromExternal"/> is <c>false</c> for the
    /// PeaksOverThresholdSeries data method (peaks over threshold derives data from a time
    /// series and so cannot be copied externally).
    /// </summary>
    [STATestMethod]
    public void CanCopyFromExternal_PeaksOverThreshold_ReturnsFalse()
    {
        var id = new UI.InputData("CopyPOTID", _collection!);
        id.ExactDataMethod = UI.InputData.ExactDataEntryType.PeaksOverThresholdSeries;

        Assert.IsFalse(id.CanCopyFromExternal);
    }

    /// <summary>
    /// Verifies that <see cref="UI.InputData.CanCopyFromExternal"/> is <c>false</c> for the
    /// USGSPeakDischarge data method (data downloaded from USGS, not local copy).
    /// </summary>
    [STATestMethod]
    public void CanCopyFromExternal_USGSPeakDischarge_ReturnsFalse()
    {
        var id = new UI.InputData("CopyUSGSDID", _collection!);
        id.ExactDataMethod = UI.InputData.ExactDataEntryType.USGSPeakDischarge;

        Assert.IsFalse(id.CanCopyFromExternal);
    }

    /// <summary>
    /// Verifies that <see cref="UI.InputData.CanCopyFromExternal"/> is <c>false</c> for the
    /// USGSPeakStage data method.
    /// </summary>
    [STATestMethod]
    public void CanCopyFromExternal_USGSPeakStage_ReturnsFalse()
    {
        var id = new UI.InputData("CopyUSGSSID", _collection!);
        id.ExactDataMethod = UI.InputData.ExactDataEntryType.USGSPeakStage;

        Assert.IsFalse(id.CanCopyFromExternal);
    }

    /// <summary>
    /// Verifies that a complete calendar-year source does not raise the block-series coverage warning.
    /// </summary>
    /// <remarks>
    /// A daily record from January 1 through December 31 fully covers the selected calendar-year block.
    /// </remarks>
    [STATestMethod]
    public void BlockSeriesCompleteCalendarYear_DoesNotAddCoverageWarning()
    {
        var id = new UI.InputData("BlockCompleteCalendarID", _collection!);
        id.ExactDataMethod = UI.InputData.ExactDataEntryType.BlockSeries;
        id.TimeBlock = TimeBlockWindow.CalendarYear;
        id.TimeSeriesElement = MakeTimeSeriesElement(
            "CompleteCalendarTS",
            TimeInterval.OneDay,
            new DateTime(2021, 1, 1),
            Enumerable.Range(0, 365).Select(i => i + 1d).ToArray());

        Assert.IsTrue(id.IsTimeSeriesInputValid());
        Assert.IsFalse(MessengerHas(id, "ID-WNG-020"),
            "A fully covered calendar-year source must not raise the block-series coverage warning.");
    }

    /// <summary>
    /// Verifies that a source starting after the calendar-year boundary raises the coverage warning.
    /// </summary>
    /// <remarks>
    /// This covers the user-reported case where the first extracted annual maximum can be biased by
    /// an incomplete source year at the beginning of the record.
    /// </remarks>
    [STATestMethod]
    public void BlockSeriesPartialFirstCalendarYear_AddsCoverageWarning()
    {
        var id = new UI.InputData("BlockPartialFirstCalendarID", _collection!);
        id.ExactDataMethod = UI.InputData.ExactDataEntryType.BlockSeries;
        id.TimeBlock = TimeBlockWindow.CalendarYear;
        id.TimeSeriesElement = MakeTimeSeriesElement(
            "PartialFirstCalendarTS",
            TimeInterval.OneDay,
            new DateTime(2021, 2, 1),
            Enumerable.Range(0, 334).Select(i => i + 1d).ToArray());

        Assert.IsTrue(id.IsTimeSeriesInputValid());
        Assert.IsTrue(MessengerHas(id, "ID-WNG-020"),
            "A source beginning inside the first calendar year must raise the coverage warning.");
    }

    /// <summary>
    /// Verifies that a source ending before the water-year boundary raises the coverage warning.
    /// </summary>
    /// <remarks>
    /// This covers the common end-of-DSS-record case where a partial final water year can produce
    /// an artificially low block maximum.
    /// </remarks>
    [STATestMethod]
    public void BlockSeriesPartialLastWaterYear_AddsCoverageWarning()
    {
        var id = new UI.InputData("BlockPartialLastWaterID", _collection!);
        id.ExactDataMethod = UI.InputData.ExactDataEntryType.BlockSeries;
        id.TimeBlock = TimeBlockWindow.WaterYear;
        id.StartMonth = 10;
        id.TimeSeriesElement = MakeTimeSeriesElement(
            "PartialLastWaterTS",
            TimeInterval.OneDay,
            new DateTime(2020, 10, 1),
            Enumerable.Range(0, 426).Select(i => i + 1d).ToArray());

        Assert.IsTrue(id.IsTimeSeriesInputValid());
        Assert.IsTrue(MessengerHas(id, "ID-WNG-020"),
            "A source ending inside the final water year must raise the coverage warning.");
    }

    /// <summary>
    /// Verifies that explicit missing values in a complete block-series source raise the coverage warning.
    /// </summary>
    /// <remarks>
    /// DSS missing-value sentinels are converted to NaN during import, so this test exercises that warning path.
    /// </remarks>
    [STATestMethod]
    public void BlockSeriesMissingValue_AddsCoverageWarning()
    {
        var values = Enumerable.Range(0, 365).Select(i => i + 1d).ToArray();
        values[100] = double.NaN;

        var id = new UI.InputData("BlockMissingValueID", _collection!);
        id.ExactDataMethod = UI.InputData.ExactDataEntryType.BlockSeries;
        id.TimeBlock = TimeBlockWindow.CalendarYear;
        id.TimeSeriesElement = MakeTimeSeriesElement(
            "MissingValueCalendarTS",
            TimeInterval.OneDay,
            new DateTime(2021, 1, 1),
            values);

        Assert.IsTrue(id.IsTimeSeriesInputValid());
        Assert.IsTrue(MessengerHas(id, "ID-WNG-020"),
            "A source with NaN missing values must raise the block-series coverage warning.");
    }

    /// <summary>
    /// Verifies that omitted timestamps in a regular source raise the coverage warning.
    /// </summary>
    /// <remarks>
    /// This catches regular DSS records where a missing period is represented by a skipped ordinate.
    /// </remarks>
    [STATestMethod]
    public void BlockSeriesRegularTimestampGap_AddsCoverageWarning()
    {
        var id = new UI.InputData("BlockTimestampGapID", _collection!);
        id.ExactDataMethod = UI.InputData.ExactDataEntryType.BlockSeries;
        id.TimeBlock = TimeBlockWindow.CalendarYear;
        id.TimeSeriesElement = MakeDailyTimeSeriesWithGap(
            "TimestampGapCalendarTS",
            new DateTime(2021, 1, 1),
            365,
            150);

        Assert.IsTrue(id.IsTimeSeriesInputValid());
        Assert.IsTrue(MessengerHas(id, "ID-WNG-020"),
            "A regular source with an omitted timestamp must raise the block-series coverage warning.");
    }

    /// <summary>
    /// Verifies that the block-series coverage warning is cleared when the exact-data method changes.
    /// </summary>
    /// <remarks>
    /// The warning is only meaningful for time-series-derived block data and should not linger after
    /// switching back to manual exact data entry.
    /// </remarks>
    [STATestMethod]
    public void BlockSeriesCoverageWarning_SwitchingToManual_RemovesWarning()
    {
        var id = new UI.InputData("BlockWarningClearedID", _collection!);
        id.ExactDataMethod = UI.InputData.ExactDataEntryType.BlockSeries;
        id.TimeBlock = TimeBlockWindow.CalendarYear;
        id.TimeSeriesElement = MakeTimeSeriesElement(
            "WarningClearedCalendarTS",
            TimeInterval.OneDay,
            new DateTime(2021, 2, 1),
            Enumerable.Range(0, 334).Select(i => i + 1d).ToArray());

        Assert.IsTrue(id.IsTimeSeriesInputValid());
        Assert.IsTrue(MessengerHas(id, "ID-WNG-020"));

        id.ExactDataMethod = UI.InputData.ExactDataEntryType.Manual;
        Assert.IsTrue(id.IsTimeSeriesInputValid());

        Assert.IsFalse(MessengerHas(id, "ID-WNG-020"),
            "The block-series coverage warning must be removed when block-series input is no longer selected.");
    }

    /// <summary>
    /// Verifies that editing plot axis titles does not back-sync into data labels.
    /// </summary>
    [STATestMethod]
    public void AxisTitleEdits_DoNotMutateUnitLabelOrIndexLabel()
    {
        var id = new UI.InputData("AxisEditID", _collection!);

        GetAxis(id.ChronologyPlot, "Yaxis").Title = "2-Day Volume";
        GetAxis(id.ChronologyPlot, "Xaxis").Title = "Period of Record";

        Assert.AreEqual("Value", id.UnitLabel);
        Assert.AreEqual("Year", id.IndexLabel);
    }

    /// <summary>
    /// Verifies that source-label edits update automatic axis titles and preserve custom titles.
    /// </summary>
    [STATestMethod]
    public void LabelEdits_UpdateDefaultAxesAndPreserveCustomAxes()
    {
        var id = new UI.InputData("AxisDefaultID", _collection!);
        GetAxis(id.ChronologyPlot, "Yaxis").Title = "2-Day Volume";

        id.UnitLabel = "Flow (cfs)";
        id.IndexLabel = "Water Year";

        Assert.AreEqual("2-Day Volume", GetAxis(id.ChronologyPlot, "Yaxis").Title);
        Assert.AreEqual("Flow (cfs)", GetAxis(id.FrequencyPlot, "Yaxis").Title);
        Assert.AreEqual("Flow (cfs)", GetAxis(id.DensityPlot, "Xaxis").Title);
        Assert.AreEqual("Flow (cfs)", GetAxis(id.HistogramPlot, "Xaxis").Title);
        Assert.AreEqual("Water Year", GetAxis(id.ChronologyPlot, "Xaxis").Title);
    }

    /// <summary>
    /// Verifies that copied input data preserves user-customized axis titles.
    /// </summary>
    [STATestMethod]
    public void Copy_PreservesCustomAxisTitles()
    {
        var id = new UI.InputData("AxisCopyID", _collection!)
        {
            UnitLabel = "Flow (cfs)",
            IndexLabel = "Water Year"
        };
        GetAxis(id.ChronologyPlot, "Yaxis").Title = "2-Day Volume";
        GetAxis(id.ChronologyPlot, "Xaxis").Title = "Period of Record";

        var copy = (UI.InputData)id.Copy("AxisCopyClone");

        Assert.AreEqual("2-Day Volume", GetAxis(copy.ChronologyPlot, "Yaxis").Title);
        Assert.AreEqual("Period of Record", GetAxis(copy.ChronologyPlot, "Xaxis").Title);
    }

    /// <summary>
    /// Verifies that <see cref="UI.InputData.IsProcessed"/> defaults to <c>false</c> on a freshly
    /// constructed instance because no time-series-derived data has been processed yet.
    /// </summary>
    [STATestMethod]
    public void IsProcessed_DefaultIsFalse()
    {
        var id = new UI.InputData("ProcID", _collection!);
        Assert.IsFalse(id.IsProcessed);
    }

    /// <summary>
    /// Gets an axis by key from a plot.
    /// </summary>
    /// <param name="plot">The plot to inspect.</param>
    /// <param name="key">The axis key.</param>
    /// <returns>The matching axis.</returns>
    private static OxyPlot.Wpf.Axis GetAxis(OxyPlot.Wpf.Plot plot, string key)
    {
        return plot.Axes.First(axis => axis.Key == key);
    }
}
