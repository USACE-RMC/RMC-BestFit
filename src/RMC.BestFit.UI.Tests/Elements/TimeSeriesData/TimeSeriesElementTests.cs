using DatabaseManager;
using FrameworkInterfaces.Messaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Numerics.Data;
using RMC.BestFit.UI;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace RMC.BestFit.UI.Tests.Elements.TimeSeriesData;

/// <summary>
/// Unit tests for <see cref="TimeSeriesElement"/>, the UI wrapper for time series data.
/// </summary>
/// <remarks>
/// Tests focus on: constructor defaults, name and description property change notifications,
/// UnitLabel property, IsValid state, and TimeSeries assignment.
/// SQLite-dependent methods (Save, Open, Delete) are excluded — covered by integration tests.
/// </remarks>
[TestClass]
public class TimeSeriesElementTests
{
    /// <summary>
    /// Creates an irregular time series from the supplied date-times.
    /// </summary>
    /// <param name="dates">The date-times to add to the series.</param>
    /// <returns>The populated irregular time series.</returns>
    private static TimeSeries CreateIrregularTimeSeries(params DateTime[] dates)
    {
        var series = new TimeSeries(TimeInterval.Irregular);
        for (int i = 0; i < dates.Length; i++)
        {
            series.Add(new SeriesOrdinate<DateTime, double>(dates[i], i + 1d));
        }

        return series;
    }

    /// <summary>
    /// Creates a regular daily time series from the supplied date-times.
    /// </summary>
    /// <param name="dates">The date-times to add to the series.</param>
    /// <returns>The populated regular time series.</returns>
    private static TimeSeries CreateRegularTimeSeries(params DateTime[] dates)
    {
        var series = new TimeSeries(TimeInterval.OneDay);
        for (int i = 0; i < dates.Length; i++)
        {
            series.Add(new SeriesOrdinate<DateTime, double>(dates[i], i + 1d));
        }

        return series;
    }

    /// <summary>
    /// Returns whether the global messenger contains a message from the supplied source and code.
    /// </summary>
    /// <param name="source">The element expected to own the message.</param>
    /// <param name="code">The stable message code to search for.</param>
    /// <returns><c>true</c> when a matching message is currently active; otherwise, <c>false</c>.</returns>
    private static bool MessengerHas(object source, string code)
    {
        return Messenger.GetInstance().AllMessageItems().Any(m => ReferenceEquals(m.Source, source) && m.Code == code);
    }

    /// <summary>
    /// Verifies that an element contains the expected irregular placeholder series.
    /// </summary>
    /// <param name="element">The element to inspect.</param>
    /// <remarks>
    /// Irregular placeholders are used for reset paths because the Numerics regular-grid
    /// constructor intentionally rejects <see cref="TimeInterval.Irregular"/>.
    /// </remarks>
    private static void AssertIrregularPlaceholder(TimeSeriesElement element)
    {
        Assert.AreEqual(TimeInterval.Irregular, element.TimeInterval);
        Assert.IsNotNull(element.TimeSeries);
        Assert.AreEqual(TimeInterval.Irregular, element.TimeSeries.TimeInterval);
        Assert.AreEqual(1, element.TimeSeries.Count);
        Assert.AreEqual(element.StartDateTime, element.TimeSeries[0].Index);
        Assert.IsTrue(double.IsNaN(element.TimeSeries[0].Value));
    }

    /// <summary>
    /// Verifies that the default constructor (no arguments) produces a non-null element with the
    /// expected default name.
    /// </summary>
    /// <remarks>Requires STA thread because OxyPlot.Wpf.Plot is a WPF UIElement.</remarks>
    [STATestMethod]
    public void DefaultConstructor_CreatesElementWithDefaultName()
    {
        var ts = new TimeSeriesElement();

        Assert.IsNotNull(ts);
        Assert.AreEqual("Time Series Data", ts.Name);
    }

    /// <summary>
    /// Verifies that constructing with a custom name stores the name correctly.
    /// </summary>
    /// <remarks>Requires STA thread because OxyPlot.Wpf.Plot is a WPF UIElement.</remarks>
    [STATestMethod]
    public void Constructor_WithName_StoresName()
    {
        var ts = new TimeSeriesElement("MyTimeSeries");

        Assert.AreEqual("MyTimeSeries", ts.Name);
    }

    /// <summary>
    /// Verifies that a freshly constructed element has a non-null <see cref="TimeSeries"/> property.
    /// </summary>
    /// <remarks>Requires STA thread because OxyPlot.Wpf.Plot is a WPF UIElement.</remarks>
    [STATestMethod]
    public void Constructor_TimeSeries_IsNotNull()
    {
        var ts = new TimeSeriesElement();

        Assert.IsNotNull(ts.TimeSeries);
    }

    /// <summary>
    /// Verifies that a freshly constructed element reports creation date and last modified dates
    /// that are recent (within the last minute).
    /// </summary>
    /// <remarks>Requires STA thread because OxyPlot.Wpf.Plot is a WPF UIElement.</remarks>
    [STATestMethod]
    public void Constructor_CreationDateAndLastModified_AreRecent()
    {
        var before = DateTime.Now.AddSeconds(-5);
        var ts = new TimeSeriesElement();
        var after = DateTime.Now.AddSeconds(5);

        Assert.IsTrue(ts.CreationDate >= before && ts.CreationDate <= after,
            "CreationDate must be close to construction time.");
        Assert.IsTrue(ts.LastModified >= before && ts.LastModified <= after,
            "LastModified must be close to construction time.");
    }

    /// <summary>
    /// Verifies that <see cref="TimeSeriesElement.CanCopyFromExternal"/> returns true.
    /// </summary>
    /// <remarks>Requires STA thread because OxyPlot.Wpf.Plot is a WPF UIElement.</remarks>
    [STATestMethod]
    public void CanCopyFromExternal_ReturnsTrue()
    {
        var ts = new TimeSeriesElement();

        Assert.IsTrue(ts.CanCopyFromExternal);
    }

    /// <summary>
    /// Verifies that setting <see cref="TimeSeriesElement.Description"/> to a non-empty string
    /// raises <see cref="System.ComponentModel.INotifyPropertyChanged.PropertyChanged"/>.
    /// </summary>
    /// <remarks>Requires STA thread because OxyPlot.Wpf.Plot is a WPF UIElement.</remarks>
    [STATestMethod]
    public void Description_Setter_RaisesPropertyChanged()
    {
        var ts = new TimeSeriesElement("TSElement");
        var raised = new List<string>();
        ts.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        ts.Description = "A useful description";

        Assert.IsTrue(raised.Contains(nameof(TimeSeriesElement.Description)),
            "PropertyChanged must fire for Description when value changes.");
    }

    /// <summary>
    /// Verifies that setting <see cref="TimeSeriesElement.Description"/> to the same existing value
    /// does NOT raise <see cref="System.ComponentModel.INotifyPropertyChanged.PropertyChanged"/>.
    /// </summary>
    /// <remarks>Requires STA thread because OxyPlot.Wpf.Plot is a WPF UIElement.</remarks>
    [STATestMethod]
    public void Description_SameValue_DoesNotRaisePropertyChanged()
    {
        var ts = new TimeSeriesElement("TSElement");
        ts.Description = "Initial";

        var raised = new List<string>();
        ts.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        ts.Description = "Initial"; // same value

        CollectionAssert.DoesNotContain(raised, nameof(TimeSeriesElement.Description),
            "PropertyChanged must NOT fire when Description is set to the same value.");
    }

    /// <summary>
    /// Verifies that the default unit label is not null or empty.
    /// </summary>
    /// <remarks>Requires STA thread because OxyPlot.Wpf.Plot is a WPF UIElement.</remarks>
    [STATestMethod]
    public void UnitLabel_DefaultValue_IsNotNullOrEmpty()
    {
        var ts = new TimeSeriesElement();

        Assert.IsFalse(string.IsNullOrEmpty(ts.UnitLabel),
            "UnitLabel must have a default non-empty value.");
    }

    /// <summary>
    /// Verifies that setting <see cref="TimeSeriesElement.UnitLabel"/> to a new value raises
    /// <see cref="System.ComponentModel.INotifyPropertyChanged.PropertyChanged"/>.
    /// </summary>
    /// <remarks>Requires STA thread because OxyPlot.Wpf.Plot is a WPF UIElement.</remarks>
    [STATestMethod]
    public void UnitLabel_Setter_RaisesPropertyChanged()
    {
        var ts = new TimeSeriesElement("TSLabel");
        var raised = new List<string>();
        ts.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        ts.UnitLabel = "m^3/s";

        Assert.IsTrue(raised.Contains(nameof(TimeSeriesElement.UnitLabel)),
            "PropertyChanged must fire for UnitLabel when value changes.");
    }

    /// <summary>
    /// Verifies that the default entry method is Manual.
    /// </summary>
    /// <remarks>Requires STA thread because OxyPlot.Wpf.Plot is a WPF UIElement.</remarks>
    [STATestMethod]
    public void EntryMethod_DefaultValue_IsManual()
    {
        var ts = new TimeSeriesElement();

        Assert.AreEqual(TimeSeriesElement.TimeSeriesEntryMethod.Manual, ts.EntryMethod);
    }

    /// <summary>
    /// Verifies that setting <see cref="TimeSeriesElement.EntryMethod"/> raises
    /// <see cref="System.ComponentModel.INotifyPropertyChanged.PropertyChanged"/>.
    /// </summary>
    /// <remarks>Requires STA thread because OxyPlot.Wpf.Plot is a WPF UIElement.</remarks>
    [STATestMethod]
    public void EntryMethod_Setter_RaisesPropertyChanged()
    {
        var ts = new TimeSeriesElement("TSEntry");
        var raised = new List<string>();
        ts.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        ts.EntryMethod = TimeSeriesElement.TimeSeriesEntryMethod.USGS;

        Assert.IsTrue(raised.Contains(nameof(TimeSeriesElement.EntryMethod)),
            "PropertyChanged must fire for EntryMethod.");
    }

    /// <summary>
    /// Verifies that changing to a non-manual entry method preserves an irregular placeholder.
    /// </summary>
    /// <remarks>
    /// This regression covers reset paths that previously called a Numerics regular-grid
    /// constructor with <see cref="TimeInterval.Irregular"/>.
    /// </remarks>
    [STATestMethod]
    public void EntryMethod_Setter_WithIrregularInterval_ResetsToIrregularPlaceholder()
    {
        var ts = new TimeSeriesElement("IrregularEntryReset");
        ts.TimeInterval = TimeInterval.Irregular;

        ts.EntryMethod = TimeSeriesElement.TimeSeriesEntryMethod.USGS;

        AssertIrregularPlaceholder(ts);
    }

    /// <summary>
    /// Verifies that changing the USGS site number can reset an irregular time series safely.
    /// </summary>
    /// <remarks>
    /// USGS instantaneous discharge/stage downloads are irregular. Editing the gage after such a
    /// download must not call a regular-grid constructor with an irregular interval.
    /// </remarks>
    [STATestMethod]
    public void USGSSiteNumber_Setter_WithIrregularInterval_ResetsToIrregularPlaceholder()
    {
        var ts = new TimeSeriesElement("IrregularUsgsReset")
        {
            EntryMethod = TimeSeriesElement.TimeSeriesEntryMethod.USGS
        };
        ts.TimeInterval = TimeInterval.Irregular;

        ts.USGSSiteNumber = "01646500";

        AssertIrregularPlaceholder(ts);
    }

    /// <summary>
    /// Verifies that the Name setter raises PropertyChanged when the name changes.
    /// </summary>
    /// <remarks>Requires STA thread because OxyPlot.Wpf.Plot is a WPF UIElement.</remarks>
    [STATestMethod]
    public void Name_Setter_RaisesPropertyChanged()
    {
        var ts = new TimeSeriesElement("OldName");
        var raised = new List<string>();
        ts.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        ts.Name = "NewName";

        Assert.IsTrue(raised.Contains(nameof(TimeSeriesElement.Name)),
            "PropertyChanged must fire for Name when value changes.");
    }

    /// <summary>
    /// Verifies that the Name setter with the same value does NOT raise PropertyChanged.
    /// </summary>
    /// <remarks>Requires STA thread because OxyPlot.Wpf.Plot is a WPF UIElement.</remarks>
    [STATestMethod]
    public void Name_SameValue_DoesNotRaisePropertyChanged()
    {
        var ts = new TimeSeriesElement("SameName");
        var raised = new List<string>();
        ts.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        ts.Name = "SameName";

        CollectionAssert.DoesNotContain(raised, nameof(TimeSeriesElement.Name),
            "PropertyChanged must NOT fire when Name is set to same value.");
    }

    /// <summary>
    /// Verifies that four OxyPlot plots are created by the constructor (TimeSeries, Seasonality, ACF, PACF).
    /// </summary>
    /// <remarks>Requires STA thread because OxyPlot.Wpf.Plot is a WPF UIElement.</remarks>
    [STATestMethod]
    public void Constructor_FourPlotsCreated_AreNotNull()
    {
        var ts = new TimeSeriesElement();

        Assert.IsNotNull(ts.TimeSeriesPlot, "TimeSeriesPlot must not be null.");
        Assert.IsNotNull(ts.SeasonalityPlot, "SeasonalityPlot must not be null.");
        Assert.IsNotNull(ts.ACFPlot, "ACFPlot must not be null.");
        Assert.IsNotNull(ts.PACFPlot, "PACFPlot must not be null.");
    }

    /// <summary>
    /// Verifies that NameOnDisk matches the name provided at construction.
    /// </summary>
    /// <remarks>Requires STA thread because OxyPlot.Wpf.Plot is a WPF UIElement.</remarks>
    [STATestMethod]
    public void NameOnDisk_MatchesConstructorName()
    {
        var ts = new TimeSeriesElement("MyDisk");

        Assert.AreEqual("MyDisk", ts.NameOnDisk);
    }

    /// <summary>
    /// Verifies that <see cref="TimeSeriesElement.Open()"/> loads time-series data from a legacy
    /// <c>TimeSeries</c> TEXT column when no <c>TimeSeriesCompressed</c> BLOB column exists.
    /// </summary>
    /// <remarks>
    /// Regression guard for a data-loss bug: Open() used to <c>EditCell</c>+<c>ApplyEdits</c> the
    /// legacy TEXT column to an empty string. The enclosing <see cref="TimeSeriesCollection.Save"/>
    /// skips clean elements, so the destructive migration was committed without the compressed
    /// BLOB ever being written — reopening lost every ordinate. The fix makes Open read-only.
    /// This test asserts both that data is loaded AND that the on-disk legacy column is untouched.
    /// </remarks>
    [STATestMethod]
    public void Open_WithLegacyTimeSeriesSchema_LoadsDataAndLeavesLegacyColumnUntouched()
    {
        const string legacyXml =
            "<TimeSeries TimeInterval=\"Irregular\">" +
            "<SeriesOrdinate Index=\"1953-01-19T00:00:00.0000000\" Value=\"8.72\" />" +
            "<SeriesOrdinate Index=\"1953-11-23T00:00:00.0000000\" Value=\"10.15\" />" +
            "<SeriesOrdinate Index=\"1954-04-02T00:00:00.0000000\" Value=\"6.48\" />" +
            "</TimeSeries>";

        string tempPath = Path.Combine(Path.GetTempPath(), $"rmcbf-test-{Guid.NewGuid():N}.bestfit");
        try
        {
            BuildLegacyTimeSeriesDatabase(tempPath, elementName: "LegacyTS", legacyXml);

            var element = new TimeSeriesElement("LegacyTS");

            using (var sqlite = new SQLiteManager(tempPath))
            {
                element.Open(sqlite);
            }

            Assert.IsNotNull(element.TimeSeries);
            Assert.AreEqual(3, element.TimeSeries.Count,
                "All 3 legacy ordinates should have been deserialized from the TimeSeries TEXT column.");
            Assert.AreEqual(8.72, element.TimeSeries[0].Value, 1e-12);
            Assert.AreEqual(10.15, element.TimeSeries[1].Value, 1e-12);
            Assert.AreEqual(6.48, element.TimeSeries[2].Value, 1e-12);
            Assert.AreEqual(TimeInterval.Irregular, element.TimeInterval);

            string persisted = ReadLegacyTimeSeriesCell(tempPath, "LegacyTS");
            Assert.AreEqual(legacyXml, persisted,
                "Open() must not mutate the legacy TimeSeries TEXT column — migration to the " +
                "compressed BLOB only happens during Save().");
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    /// <summary>
    /// Builds a SQLite <c>.bestfit</c> file with the pre-v2.0 "Time Series Data" schema: a
    /// <c>TimeSeries</c> TEXT column holds the serialized XML and no <c>TimeSeriesCompressed</c>
    /// BLOB column exists. One row is populated with the provided XML.
    /// </summary>
    private static void BuildLegacyTimeSeriesDatabase(string path, string elementName, string xmlPayload)
    {
        var dt = new DataTable("Time Series Data");
        dt.Columns.Add(nameof(TimeSeriesElement.Name), typeof(string));
        dt.Columns.Add(nameof(TimeSeriesElement.Description), typeof(string));
        dt.Columns.Add(nameof(TimeSeriesElement.CreationDate), typeof(string));
        dt.Columns.Add(nameof(TimeSeriesElement.LastModified), typeof(string));
        dt.Columns.Add(nameof(TimeSeriesElement.TimeSeries), typeof(string));
        dt.Columns.Add(nameof(TimeSeriesElement.UnitLabel), typeof(string));
        dt.Columns.Add(nameof(TimeSeriesElement.EntryMethod), typeof(string));
        dt.Columns.Add(nameof(TimeSeriesElement.SeriesType), typeof(string));
        dt.Columns.Add(nameof(TimeSeriesElement.TimeInterval), typeof(string));
        dt.Columns.Add(nameof(TimeSeriesElement.StartDateTime), typeof(string));

        using (var sqlite = new SQLiteManager(path))
        {
            sqlite.Open();
            sqlite.SaveDataTable(dt);
            var dtView = sqlite.GetTableManager("Time Series Data");
            dtView.AddRow();
            dtView.EditCell(0, nameof(TimeSeriesElement.Name), elementName);
            dtView.EditCell(0, nameof(TimeSeriesElement.Description), "");
            dtView.EditCell(0, nameof(TimeSeriesElement.CreationDate), DateTime.Now.ToString("o"));
            dtView.EditCell(0, nameof(TimeSeriesElement.LastModified), DateTime.Now.ToString("o"));
            dtView.EditCell(0, nameof(TimeSeriesElement.TimeSeries), xmlPayload);
            dtView.EditCell(0, nameof(TimeSeriesElement.UnitLabel), "Stage (ft)");
            dtView.EditCell(0, nameof(TimeSeriesElement.EntryMethod), "Manual");
            dtView.EditCell(0, nameof(TimeSeriesElement.SeriesType), "DailyDischarge");
            dtView.EditCell(0, nameof(TimeSeriesElement.TimeInterval), "Irregular");
            dtView.EditCell(0, nameof(TimeSeriesElement.StartDateTime), "1953-01-19T00:00:00.0000000");
            dtView.ApplyEdits();
            sqlite.Close();
        }
    }

    /// <summary>
    /// Verifies that <see cref="TimeSeriesElement.ElementImageResourceKey"/> returns the expected key.
    /// </summary>
    [STATestMethod]
    public void ElementImageResourceKey_ReturnsExpectedKey()
    {
        var ts = new TimeSeriesElement("ImgKeyTS");
        Assert.AreEqual("TimeSeriesDataIcon", ts.ElementImageResourceKey);
    }

    /// <summary>
    /// Verifies that <see cref="TimeSeriesElement.RaisePreviewSaved"/> does not throw or flip cancel
    /// when there are no PreviewObjectSaved subscribers.
    /// </summary>
    [STATestMethod]
    public void RaisePreviewSaved_NoSubscribers_DoesNotFlipCancel()
    {
        var ts = new TimeSeriesElement("PrevTS");
        bool cancel = false;

        ts.RaisePreviewSaved(ref cancel);

        Assert.IsFalse(cancel);
    }

    /// <summary>
    /// Verifies that the TimeInterval setter raises PropertyChanged when changed.
    /// </summary>
    [STATestMethod]
    public void TimeInterval_Setter_RaisesPropertyChanged()
    {
        var ts = new TimeSeriesElement("TIntTS");
        var initial = ts.TimeInterval;
        var raised = new List<string>();
        ts.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        var values = (TimeInterval[])Enum.GetValues(typeof(TimeInterval));
        var distinct = values.First(v => !v.Equals(initial));
        ts.TimeInterval = distinct;

        Assert.IsTrue(raised.Contains(nameof(TimeSeriesElement.TimeInterval)));
    }

    /// <summary>
    /// Verifies that the StartDateTime setter raises PropertyChanged when changed.
    /// </summary>
    [STATestMethod]
    public void StartDateTime_Setter_RaisesPropertyChanged()
    {
        var ts = new TimeSeriesElement("SDateTS");
        var raised = new List<string>();
        ts.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        ts.StartDateTime = new DateTime(2010, 5, 1);

        Assert.IsTrue(raised.Contains(nameof(TimeSeriesElement.StartDateTime)));
        Assert.AreEqual(new DateTime(2010, 5, 1), ts.StartDateTime);
    }

    /// <summary>
    /// Verifies that the HECDSSFullFilename setter raises PropertyChanged when changed.
    /// </summary>
    [STATestMethod]
    public void HECDSSFullFilename_Setter_RaisesPropertyChanged()
    {
        var ts = new TimeSeriesElement("DSSTS");
        var raised = new List<string>();
        ts.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        ts.HECDSSFullFilename = "C:\\path\\to\\file.dss";

        Assert.IsTrue(raised.Contains(nameof(TimeSeriesElement.HECDSSFullFilename)));
    }

    /// <summary>
    /// Verifies that the GHCNSiteNumber setter raises PropertyChanged when changed.
    /// </summary>
    [STATestMethod]
    public void GHCNSiteNumber_Setter_RaisesPropertyChanged()
    {
        var ts = new TimeSeriesElement("GHCNTS");
        var raised = new List<string>();
        ts.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        ts.GHCNSiteNumber = "12345678901";

        Assert.IsTrue(raised.Contains(nameof(TimeSeriesElement.GHCNSiteNumber)));
        Assert.AreEqual("12345678901", ts.GHCNSiteNumber);
    }

    /// <summary>
    /// Verifies that the USGSSiteNumber setter raises PropertyChanged when changed.
    /// </summary>
    [STATestMethod]
    public void USGSSiteNumber_Setter_RaisesPropertyChanged()
    {
        var ts = new TimeSeriesElement("USGSTS");
        var raised = new List<string>();
        ts.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        ts.USGSSiteNumber = "01234567";

        Assert.IsTrue(raised.Contains(nameof(TimeSeriesElement.USGSSiteNumber)));
        Assert.AreEqual("01234567", ts.USGSSiteNumber);
    }

    /// <summary>
    /// Verifies that the CHMNSiteNumber setter raises PropertyChanged when changed.
    /// </summary>
    [STATestMethod]
    public void CHMNSiteNumber_Setter_RaisesPropertyChanged()
    {
        var ts = new TimeSeriesElement("CHMNTS");
        var raised = new List<string>();
        ts.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        ts.CHMNSiteNumber = "ABC1234";

        Assert.IsTrue(raised.Contains(nameof(TimeSeriesElement.CHMNSiteNumber)));
    }

    /// <summary>
    /// Verifies that the ABOMSiteNumber setter raises PropertyChanged when changed.
    /// </summary>
    [STATestMethod]
    public void ABOMSiteNumber_Setter_RaisesPropertyChanged()
    {
        var ts = new TimeSeriesElement("ABOMTS");
        var raised = new List<string>();
        ts.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        ts.ABOMSiteNumber = "012345";

        Assert.IsTrue(raised.Contains(nameof(TimeSeriesElement.ABOMSiteNumber)));
    }

    /// <summary>
    /// Verifies that the HECDSSDataPathname setter raises PropertyChanged when changed.
    /// </summary>
    [STATestMethod]
    public void HECDSSDataPathname_Setter_RaisesPropertyChanged()
    {
        var ts = new TimeSeriesElement("DSSPathTS");
        var raised = new List<string>();
        ts.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        ts.HECDSSDataPathname = "/A/B/C/D/E/F/";

        Assert.IsTrue(raised.Contains(nameof(TimeSeriesElement.HECDSSDataPathname)));
    }

    /// <summary>
    /// Verifies that <see cref="TimeSeriesElement.IsValid"/> is <c>false</c> after
    /// construction because required labels and minimum time-series length are not satisfied.
    /// </summary>
    [STATestMethod]
    public void IsValid_AfterConstruction_IsFalse()
    {
        var ts = new TimeSeriesElement("ValidTS");

        Assert.IsFalse(ts.IsValid);
    }

    /// <summary>
    /// Verifies that an irregular time series with non-ascending backing date-times is invalid.
    /// </summary>
    [STATestMethod]
    public void IsValid_IrregularOutOfOrderDateTimes_IsFalseAndAddsMessage()
    {
        var start = new DateTime(2020, 1, 1);
        var ts = new TimeSeriesElement("OutOfOrderTS");
        try
        {
            ts.TimeSeries = CreateIrregularTimeSeries(start.AddDays(1), start.AddDays(2), start);

            Assert.IsFalse(ts.IsValid);
            Assert.IsTrue(MessengerHas(ts, "TS-ERR-013"));
        }
        finally
        {
            Messenger.GetInstance().Clear(ts);
        }
    }

    /// <summary>
    /// Verifies that correcting irregular date-time order clears the validation message.
    /// </summary>
    [STATestMethod]
    public void IsValid_IrregularDateTimesCorrected_ClearsOrderMessage()
    {
        var start = new DateTime(2020, 1, 1);
        var ts = new TimeSeriesElement("CorrectedOrderTS");
        try
        {
            ts.TimeSeries = CreateIrregularTimeSeries(start.AddDays(1), start.AddDays(2), start);
            Assert.IsTrue(MessengerHas(ts, "TS-ERR-013"));

            ts.TimeSeries = CreateIrregularTimeSeries(start, start.AddDays(1), start.AddDays(2));

            Assert.IsTrue(ts.IsValid);
            Assert.IsFalse(MessengerHas(ts, "TS-ERR-013"));
        }
        finally
        {
            Messenger.GetInstance().Clear(ts);
        }
    }

    /// <summary>
    /// Verifies that regular interval series do not use the manual irregular date-time order rule.
    /// </summary>
    [STATestMethod]
    public void IsValid_RegularSeriesOutOfOrderDateTimes_DoesNotAddOrderMessage()
    {
        var start = new DateTime(2020, 1, 1);
        var ts = new TimeSeriesElement("RegularOrderTS");
        try
        {
            ts.TimeSeries = CreateRegularTimeSeries(start.AddDays(1), start.AddDays(2), start);

            Assert.IsTrue(ts.IsValid);
            Assert.IsFalse(MessengerHas(ts, "TS-ERR-013"));
        }
        finally
        {
            Messenger.GetInstance().Clear(ts);
        }
    }

    /// <summary>
    /// Verifies that USGSRawText starts as an empty string.
    /// </summary>
    [STATestMethod]
    public void USGSRawText_DefaultIsEmpty()
    {
        var ts = new TimeSeriesElement("RawTS");

        Assert.IsNotNull(ts.USGSRawText);
        Assert.AreEqual(string.Empty, ts.USGSRawText);
    }

    /// <summary>
    /// Verifies that saved compressed USGS text remains compressed through project opening and a
    /// metadata-only save, then materializes with identical content on first access.
    /// </summary>
    /// <remarks>
    /// This guards the project-open optimization for full-period instantaneous responses. The test
    /// inspects private materialization state because reading <see cref="TimeSeriesElement.USGSRawText"/>
    /// is intentionally the operation that expands the payload.
    /// </remarks>
    [STATestMethod]
    [DoNotParallelize]
    public void Open_CompressedUSGSRawText_DefersMaterializationAndPreservesPayloadOnSave()
    {
        const string rawText = "# USGS raw response\nagency_cd\tsite_no\tvalue\nUSGS\t01646500\t123.4\n";
        byte[] compressed = Numerics.Tools.Compress(System.Text.Encoding.UTF8.GetBytes(rawText))
            ?? throw new InvalidOperationException("USGS test payload compression returned null.");

        using var scope = new ProjectFileScope();
        var collection = new TimeSeriesCollection(scope.Project);
        var seed = new TimeSeriesElement("LazyRawTS", collection)
        {
            TimeSeries = CreateIrregularTimeSeries(
                new DateTime(2020, 1, 1),
                new DateTime(2020, 1, 2),
                new DateTime(2020, 1, 3))
        };
        collection.Add(seed);
        WriteCompressedUSGSRawText(scope.ProjectPath, seed.Name, compressed);

        var openedCollection = new TimeSeriesCollection(scope.Project);
        openedCollection.Open();
        var opened = openedCollection.Cast<TimeSeriesElement>().Single();

        Assert.IsFalse(GetPrivateField<bool>(opened, "_usgsRawTextMaterialized"),
            "Opening the project must not expand compressed USGS response text.");
        Assert.AreEqual(3, opened.TimeSeries.Count,
            "Streaming compressed time-series deserialization must preserve every ordinate.");
        Assert.AreEqual(1d, opened.TimeSeries[0].Value, 1e-12);
        Assert.AreEqual(3d, opened.TimeSeries[2].Value, 1e-12);

        opened.Save();

        Assert.IsFalse(GetPrivateField<bool>(opened, "_usgsRawTextMaterialized"),
            "Saving an untouched lazy payload must not force decompression.");
        CollectionAssert.AreEqual(compressed, ReadCompressedUSGSRawText(scope.ProjectPath, seed.Name),
            "A metadata-only save must preserve the compressed response bytes.");
        Assert.AreEqual(rawText, opened.USGSRawText);
        Assert.IsTrue(GetPrivateField<bool>(opened, "_usgsRawTextMaterialized"));
    }

    /// <summary>
    /// Verifies that collections larger than the performance threshold retain complete
    /// per-ordinate notifications and collection undo/redo behavior.
    /// </summary>
    /// <remarks>
    /// The final ordinate is deliberately beyond 100,000 so a size-based tracking cutoff cannot
    /// satisfy the test. Replacement is used because it is the collection-level form of editing an
    /// ordinate and must restore the exact prior object through undo and redo.
    /// </remarks>
    [STATestMethod]
    public void LargeTimeSeries_AllOrdinatesRetainNotificationsAndUndoRedo()
    {
        const int count = 100_001;
        var series = new TimeSeries(TimeInterval.Irregular)
        {
            SuppressCollectionChanged = true
        };
        DateTime start = new DateTime(2000, 1, 1);
        for (int i = 0; i < count; i++)
        {
            series.Add(new SeriesOrdinate<DateTime, double>(start.AddMinutes(i), i));
        }
        series.SuppressCollectionChanged = false;

        var element = new TimeSeriesElement("LargeTrackingTS")
        {
            TimeSeries = series
        };
        element.UndoManager.Clear();

        bool lastOrdinateNotificationObserved = false;
        element.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(SeriesOrdinate<DateTime, double>.Value))
                lastOrdinateNotificationObserved = true;
        };
        series[count - 1].Value = -1d;
        Assert.IsTrue(lastOrdinateNotificationObserved,
            "The last ordinate must retain its PropertyChanged subscription regardless of collection size.");

        var original = series[count - 1];
        var replacement = new SeriesOrdinate<DateTime, double>(original.Index, 42d);
        series[count - 1] = replacement;

        Assert.IsTrue(element.UndoManager.CanUndo);
        Assert.AreSame(replacement, series[count - 1]);

        element.UndoManager.Undo();
        Assert.AreSame(original, series[count - 1]);
        Assert.IsTrue(element.UndoManager.CanRedo);

        element.UndoManager.Redo();
        Assert.AreSame(replacement, series[count - 1]);
    }

    /// <summary>
    /// Verifies the BestFit UI element can download full-period USGS instantaneous discharge data.
    /// </summary>
    /// <returns>A task that completes when the live download and assertions finish.</returns>
    /// <remarks>
    /// This live regression covers the exact app data path for USGS gage 01646500 instantaneous
    /// discharge. It intentionally exercises <see cref="TimeSeriesElement.Download(CancellationToken)"/>
    /// rather than calling Numerics directly so failures identify whether the BestFit UI layer can
    /// receive and assign the downloaded series.
    /// </remarks>
    [STATestMethod]
    [TestCategory("External")]
    [Timeout(300000)]
    public async Task Download_USGS01646500InstantaneousDischarge_LiveFullPor_Succeeds()
    {
        var ts = new TimeSeriesElement("USGS 01646500 Instantaneous Discharge")
        {
            EntryMethod = TimeSeriesElement.TimeSeriesEntryMethod.USGS,
            USGSSiteNumber = "01646500",
            SeriesType = TimeSeriesDownload.TimeSeriesType.InstantaneousDischarge
        };

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(4));
        var stopwatch = Stopwatch.StartNew();

        await ts.Download(cts.Token);

        stopwatch.Stop();
        Console.WriteLine($"Downloaded {ts.TimeSeries.Count:N0} ordinates in {stopwatch.Elapsed.TotalSeconds:N1} seconds.");

        Assert.AreEqual(TimeInterval.Irregular, ts.TimeInterval);
        Assert.AreEqual(TimeInterval.Irregular, ts.TimeSeries.TimeInterval);
        Assert.IsTrue(ts.TimeSeries.Count > 1_000_000, "Expected full-period instantaneous discharge to contain more than one million ordinates.");
        Assert.IsTrue(ts.TimeSeries.StartDate.Year <= 1973, $"Expected full-period record to start in the early 1970s. Actual start: {ts.TimeSeries.StartDate:o}.");
        Assert.IsTrue(ts.TimeSeries.EndDate >= DateTime.Today.AddDays(-2), $"Expected the record to include recent provisional data. Actual end: {ts.TimeSeries.EndDate:o}.");
        Assert.IsFalse(string.IsNullOrWhiteSpace(ts.USGSRawText), "Expected raw USGS RDB text to be retained.");
        StringAssert.Contains(ts.USGSRawText, "01646500");
        Assert.IsTrue(ts.IsValid, "Downloaded full-period instantaneous discharge should leave the element valid.");
    }

    /// <summary>
    /// Verifies the BestFit UI element can download full-period GHCN daily precipitation data.
    /// </summary>
    /// <returns>A task that completes when the live download and assertions finish.</returns>
    /// <remarks>
    /// This live regression covers the exact app data path for GHCN station USC00040741 daily
    /// precipitation. It exercises <see cref="TimeSeriesElement.Download(CancellationToken)"/> so
    /// failures identify whether the BestFit UI layer can receive and assign the downloaded series.
    /// </remarks>
    [STATestMethod]
    [TestCategory("External")]
    [Timeout(360000)]
    public async Task Download_GHCNUSC00040741DailyPrecipitation_LiveFullPor_Succeeds()
    {
        var ts = new TimeSeriesElement("GHCN USC00040741 Daily Precipitation")
        {
            EntryMethod = TimeSeriesElement.TimeSeriesEntryMethod.GHCN,
            GHCNSiteNumber = "USC00040741",
            SeriesType = TimeSeriesDownload.TimeSeriesType.DailyPrecipitation
        };

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        var stopwatch = Stopwatch.StartNew();

        await ts.Download(cts.Token);

        stopwatch.Stop();
        Console.WriteLine($"Downloaded {ts.TimeSeries.Count:N0} ordinates in {stopwatch.Elapsed.TotalSeconds:N1} seconds.");

        Assert.AreEqual(TimeInterval.OneDay, ts.TimeInterval);
        Assert.AreEqual(TimeInterval.OneDay, ts.TimeSeries.TimeInterval);
        Assert.IsTrue(ts.TimeSeries.Count > 20_000, "Expected full-period GHCN precipitation to contain more than twenty thousand ordinates.");
        Assert.IsTrue(ts.TimeSeries.StartDate.Year <= 1960, $"Expected full-period record to start by 1960. Actual start: {ts.TimeSeries.StartDate:o}.");
        Assert.IsTrue(ts.TimeSeries.EndDate >= DateTime.Today.AddMonths(-2), $"Expected the record to include recent GHCN data. Actual end: {ts.TimeSeries.EndDate:o}.");
        Assert.IsTrue(ts.TimeSeries.Any(o => !double.IsNaN(o.Value)), "Expected at least one valid precipitation value.");
        Assert.IsTrue(ts.IsValid, "Downloaded full-period GHCN precipitation should leave the element valid.");
    }

    /// <summary>
    /// Verifies that <see cref="TimeSeriesElement.IsPeakSeasonality"/> is <c>false</c>
    /// for the default daily-discharge series type.
    /// </summary>
    [STATestMethod]
    public void IsPeakSeasonality_DefaultDailyDischarge_IsFalse()
    {
        var ts = new TimeSeriesElement("PeakTS");

        Assert.IsFalse(ts.IsPeakSeasonality);
    }

    /// <summary>
    /// Verifies that editing a plot axis title does not back-sync into the unit label.
    /// </summary>
    [STATestMethod]
    public void AxisTitleEdit_DoesNotMutateUnitLabel()
    {
        var ts = new TimeSeriesElement("AxisEditTS");

        GetAxis(ts.TimeSeriesPlot, "Yaxis").Title = "2-Day Volume";

        Assert.AreEqual("Value", ts.UnitLabel);
    }

    /// <summary>
    /// Verifies that unit-label edits update automatic axes and preserve custom seasonality axes.
    /// </summary>
    [STATestMethod]
    public void UnitLabelEdit_UpdatesDefaultAxesAndPreservesCustomSeasonalityAxis()
    {
        var ts = new TimeSeriesElement("AxisDefaultTS");
        GetAxis(ts.SeasonalityPlot, "Yaxis").Title = "Seasonal Volume";

        ts.UnitLabel = "Flow (cfs)";

        Assert.AreEqual("Flow (cfs)", GetAxis(ts.TimeSeriesPlot, "Yaxis").Title);
        Assert.AreEqual("Seasonal Volume", GetAxis(ts.SeasonalityPlot, "Yaxis").Title);
    }

    /// <summary>
    /// Verifies that peak seasonality changes only automatic seasonality axis titles.
    /// </summary>
    [STATestMethod]
    public void SeriesTypeEdit_UpdatesDefaultSeasonalityAxisAndPreservesCustomAxis()
    {
        var ts = new TimeSeriesElement("PeakAxisTS");

        ts.SeriesType = TimeSeriesDownload.TimeSeriesType.PeakDischarge;
        Assert.AreEqual("Relative Frequency", GetAxis(ts.SeasonalityPlot, "Yaxis").Title);

        ts.SeriesType = TimeSeriesDownload.TimeSeriesType.DailyDischarge;
        Assert.AreEqual("Value", GetAxis(ts.SeasonalityPlot, "Yaxis").Title);

        GetAxis(ts.SeasonalityPlot, "Yaxis").Title = "Seasonal Volume";
        ts.SeriesType = TimeSeriesDownload.TimeSeriesType.PeakStage;

        Assert.AreEqual("Seasonal Volume", GetAxis(ts.SeasonalityPlot, "Yaxis").Title);
    }

    /// <summary>
    /// Verifies that copied time-series data preserves user-customized axis titles.
    /// </summary>
    [STATestMethod]
    public void Copy_PreservesCustomAxisTitles()
    {
        var ts = new TimeSeriesElement("AxisCopyTS")
        {
            UnitLabel = "Flow (cfs)"
        };
        GetAxis(ts.TimeSeriesPlot, "Yaxis").Title = "2-Day Volume";
        GetAxis(ts.SeasonalityPlot, "Yaxis").Title = "Seasonal Volume";

        var copy = (TimeSeriesElement)ts.Copy("AxisCopyClone");

        Assert.AreEqual("2-Day Volume", GetAxis(copy.TimeSeriesPlot, "Yaxis").Title);
        Assert.AreEqual("Seasonal Volume", GetAxis(copy.SeasonalityPlot, "Yaxis").Title);
    }

    /// <summary>
    /// Verifies that custom plot axis titles survive the real save/open path.
    /// </summary>
    [STATestMethod]
    [DoNotParallelize]
    public void SaveOpen_PreservesCustomAxisTitles()
    {
        using var scope = new ProjectFileScope();
        var collection = new TimeSeriesCollection(scope.Project);
        var seed = new TimeSeriesElement("AxisPersistenceTS", collection)
        {
            UnitLabel = "Flow (cfs)"
        };
        GetAxis(seed.TimeSeriesPlot, "Yaxis").Title = "2-Day Volume";
        GetAxis(seed.SeasonalityPlot, "Yaxis").Title = "Seasonal Volume";
        collection.Add(seed);

        var openedCollection = new TimeSeriesCollection(scope.Project);
        openedCollection.Open();
        var opened = openedCollection.Cast<TimeSeriesElement>().Single();

        Assert.AreEqual("Flow (cfs)", opened.UnitLabel);
        Assert.AreEqual("2-Day Volume", GetAxis(opened.TimeSeriesPlot, "Yaxis").Title);
        Assert.AreEqual("Seasonal Volume", GetAxis(opened.SeasonalityPlot, "Yaxis").Title);
    }

    /// <summary>
    /// Reads the <c>TimeSeries</c> TEXT cell for a given element from the "Time Series Data" table.
    /// </summary>
    private static string ReadLegacyTimeSeriesCell(string path, string elementName)
    {
        using (var sqlite = new SQLiteManager(path))
        {
            sqlite.Open();
            var dtView = sqlite.GetTableManager("Time Series Data");
            int rowIndex = dtView.SearchColumn(0, dtView.NumberOfRows - 1,
                nameof(TimeSeriesElement.Name), elementName, true, true);
            var cell = dtView.GetCell(nameof(TimeSeriesElement.TimeSeries), rowIndex);
            sqlite.Close();
            return cell?.ToString() ?? string.Empty;
        }
    }

    /// <summary>
    /// Writes a compressed USGS response into an existing time-series row.
    /// </summary>
    /// <param name="path">The SQLite project path.</param>
    /// <param name="elementName">The time-series row name.</param>
    /// <param name="compressed">The compressed response bytes.</param>
    private static void WriteCompressedUSGSRawText(string path, string elementName, byte[] compressed)
    {
        using var sqlite = new SQLiteManager(path);
        sqlite.Open();
        var dtView = sqlite.GetTableManager("Time Series Data");
        int rowIndex = dtView.SearchColumn(0, dtView.NumberOfRows - 1,
            nameof(TimeSeriesElement.Name), elementName, true, true);
        dtView.EditCell(rowIndex, "USGSRawTextCompressed", compressed);
        dtView.ApplyEdits();
        sqlite.Close();
    }

    /// <summary>
    /// Reads a compressed USGS response from an existing time-series row.
    /// </summary>
    /// <param name="path">The SQLite project path.</param>
    /// <param name="elementName">The time-series row name.</param>
    /// <returns>The compressed response bytes.</returns>
    private static byte[] ReadCompressedUSGSRawText(string path, string elementName)
    {
        using var sqlite = new SQLiteManager(path);
        sqlite.Open();
        var dtView = sqlite.GetTableManager("Time Series Data");
        int rowIndex = dtView.SearchColumn(0, dtView.NumberOfRows - 1,
            nameof(TimeSeriesElement.Name), elementName, true, true);
        var bytes = (byte[])dtView.GetCell("USGSRawTextCompressed", rowIndex);
        sqlite.Close();
        return bytes;
    }

    /// <summary>
    /// Reads a private instance field for state-oriented regression assertions.
    /// </summary>
    /// <typeparam name="T">The expected field type.</typeparam>
    /// <param name="target">The object containing the field.</param>
    /// <param name="fieldName">The private field name.</param>
    /// <returns>The field value.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the requested field cannot be found.</exception>
    private static T GetPrivateField<T>(object target, string fieldName)
    {
        var field = target.GetType().GetField(
            fieldName,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field == null)
            throw new InvalidOperationException($"Field '{fieldName}' was not found on {target.GetType().FullName}.");

        return (T)field.GetValue(target)!;
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

    /// <summary>
    /// Owns temporary project path mutation on the singleton project.
    /// </summary>
    private sealed class ProjectFileScope : IDisposable
    {
        private readonly string _previousFullFileName;

        /// <summary>
        /// Initializes a new temporary project-file scope.
        /// </summary>
        public ProjectFileScope()
        {
            Project = BestFitProject.GetInstance();
            _previousFullFileName = Project.FullFileName;
            ProjectPath = Path.Combine(Path.GetTempPath(), $"rmcbf-timeseries-persistence-{Guid.NewGuid():N}.bestfit");
            Project.FullFileName = ProjectPath;
        }

        /// <summary>
        /// Gets the singleton project instance under test.
        /// </summary>
        public BestFitProject Project { get; }

        /// <summary>
        /// Gets the temporary SQLite project path.
        /// </summary>
        public string ProjectPath { get; }

        /// <summary>
        /// Restores singleton project state and removes the temporary project file.
        /// </summary>
        public void Dispose()
        {
            Project.FullFileName = _previousFullFileName;
            ForceSetIsDirty(Project, false);
            if (File.Exists(ProjectPath))
            {
                File.Delete(ProjectPath);
            }
        }

        /// <summary>
        /// Invokes the protected dirty-state setter used to restore singleton project state.
        /// </summary>
        /// <param name="target">The saveable object whose dirty flag should be changed.</param>
        /// <param name="value">The dirty-state value to assign.</param>
        private static void ForceSetIsDirty(object target, bool value)
        {
            for (Type? type = target.GetType(); type != null; type = type.BaseType)
            {
                var method = type.GetMethod(
                    "SetIsDirty",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
                    binder: null,
                    types: new[] { typeof(bool) },
                    modifiers: null);
                if (method != null)
                {
                    method.Invoke(target, new object[] { value });
                    return;
                }
            }

            throw new InvalidOperationException($"SetIsDirty(bool) was not found on {target.GetType().FullName}.");
        }
    }
}
