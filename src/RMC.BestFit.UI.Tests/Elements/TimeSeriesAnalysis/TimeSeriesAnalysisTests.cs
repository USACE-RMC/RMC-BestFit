using DatabaseManager;
using FrameworkInterfaces;
using FrameworkInterfaces.Messaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Numerics.Data;
using RMC.BestFit.Models;
using RMC.BestFit.UI;
using System.Data;
using System.IO;

namespace RMC.BestFit.UI.Tests.Elements.TimeSeriesAnalysis;

/// <summary>
/// Unit tests for <see cref="UI.TimeSeriesAnalysis"/>, the UI wrapper for time series analysis.
/// </summary>
/// <remarks>
/// All tests require STA thread because the constructor creates OxyPlot WPF Plot objects.
/// Tests focus on: constructor defaults, property change notifications, IsEstimated flag,
/// TimeSeriesData, Covariates, and plot initialization. Save/Open/Delete are excluded (require SQLite).
/// </remarks>
[TestClass]
public class TimeSeriesAnalysisTests
{
    private static TimeSeriesAnalysisCollection? _collection;

    /// <summary>
    /// Creates a shared collection backed by the singleton BestFitProject.
    /// </summary>
    [ClassInitialize]
    public static void ClassInitialize(TestContext _)
    {
        _collection = new TimeSeriesAnalysisCollection(BestFitProject.GetInstance());
    }

    /// <summary>
    /// Creates a time-series element with deterministic regularly spaced values.
    /// </summary>
    /// <param name="name">The element name.</param>
    /// <param name="count">The number of observations to create.</param>
    /// <param name="interval">The interval between observations.</param>
    /// <param name="start">The first observation timestamp.</param>
    /// <returns>A populated time-series element.</returns>
    private static TimeSeriesElement CreateTimeSeriesElement(string name, int count, TimeInterval interval, DateTime start)
    {
        var element = new TimeSeriesElement(name);
        var series = new TimeSeries(interval);
        DateTime date = start;
        for (int i = 0; i < count; i++)
        {
            series.Add(new SeriesOrdinate<DateTime, double>(date, i + 1.0));
            date = TimeSeries.AddTimeInterval(date, interval);
        }

        element.TimeSeries = series;
        return element;
    }

    /// <summary>
    /// Creates a time-series element with a constant positive value at each regular time step.
    /// </summary>
    /// <param name="name">The element name.</param>
    /// <param name="count">The number of observations to create.</param>
    /// <param name="interval">The interval between observations.</param>
    /// <param name="start">The first observation timestamp.</param>
    /// <param name="value">The value assigned to each observation.</param>
    /// <returns>A populated constant time-series element.</returns>
    private static TimeSeriesElement CreateConstantTimeSeriesElement(string name, int count, TimeInterval interval, DateTime start, double value)
    {
        var element = new TimeSeriesElement(name);
        var series = new TimeSeries(interval);
        DateTime date = start;
        for (int i = 0; i < count; i++)
        {
            series.Add(new SeriesOrdinate<DateTime, double>(date, value));
            date = TimeSeries.AddTimeInterval(date, interval);
        }

        element.TimeSeries = series;
        return element;
    }


    /// <summary>
    /// Creates a finite regular time-series element that makes Box-Cox lambda fitting fail.
    /// </summary>
    /// <param name="name">The element name.</param>
    /// <returns>A populated time-series element.</returns>
    private static TimeSeriesElement CreateBoxCoxLambdaFailureTimeSeriesElement(string name)
    {
        var element = new TimeSeriesElement(name);
        var series = new TimeSeries(TimeInterval.OneYear);
        DateTime date = new DateTime(1960, 1, 1);
        for (int i = 0; i < 60; i++)
        {
            series.Add(new SeriesOrdinate<DateTime, double>(date, i == 0 ? 0.0 : 10.0));
            date = TimeSeries.AddTimeInterval(date, TimeInterval.OneYear);
        }

        element.TimeSeries = series;
        return element;
    }

    /// <summary>
    /// Verifies that the constructor stores the provided name.
    /// </summary>
    [STATestMethod]
    public void Constructor_StoresName()
    {
        var tsa = new UI.TimeSeriesAnalysis("TestTSA", _collection!);

        Assert.AreEqual("TestTSA", tsa.Name);
    }

    /// <summary>
    /// Verifies that <see cref="UI.TimeSeriesAnalysis.NameOnDisk"/> matches the constructor name.
    /// </summary>
    [STATestMethod]
    public void Constructor_NameOnDiskMatchesName()
    {
        var tsa = new UI.TimeSeriesAnalysis("DiskTSA", _collection!);

        Assert.AreEqual("DiskTSA", tsa.NameOnDisk);
    }

    /// <summary>
    /// Verifies that <see cref="UI.TimeSeriesAnalysis.IsEstimated"/> is <c>false</c> after construction.
    /// </summary>
    [STATestMethod]
    public void Constructor_IsEstimated_IsFalseInitially()
    {
        var tsa = new UI.TimeSeriesAnalysis("EstTSA", _collection!);

        Assert.IsFalse(tsa.IsEstimated);
    }

    /// <summary>
    /// Verifies that <see cref="UI.TimeSeriesAnalysis.TimeSeriesData"/> is <c>null</c> after construction.
    /// </summary>
    [STATestMethod]
    public void Constructor_TimeSeriesData_IsNullInitially()
    {
        var tsa = new UI.TimeSeriesAnalysis("TsDataTSA", _collection!);

        Assert.IsNull(tsa.TimeSeriesData);
    }

    /// <summary>
    /// Verifies that <see cref="UI.TimeSeriesAnalysis.Covariates"/> is not null and empty after construction.
    /// </summary>
    [STATestMethod]
    public void Constructor_Covariates_IsNotNullAndEmpty()
    {
        var tsa = new UI.TimeSeriesAnalysis("CovTSA", _collection!);

        Assert.IsNotNull(tsa.Covariates);
        Assert.AreEqual(0, tsa.Covariates.Count, "Covariates should be empty on construction.");
    }

    /// <summary>
    /// Verifies that <see cref="UI.TimeSeriesAnalysis.CanCopyFromExternal"/> returns <c>false</c>.
    /// </summary>
    [STATestMethod]
    public void CanCopyFromExternal_ReturnsFalse()
    {
        var tsa = new UI.TimeSeriesAnalysis("CopyTSA", _collection!);

        Assert.IsFalse(tsa.CanCopyFromExternal);
    }

    /// <summary>
    /// Verifies that all six plots are initialized by the constructor.
    /// </summary>
    [STATestMethod]
    public void Constructor_AllSixPlots_AreNotNull()
    {
        var tsa = new UI.TimeSeriesAnalysis("PlotsTSA", _collection!);

        Assert.IsNotNull(tsa.TimeSeriesPlot, "TimeSeriesPlot must not be null.");
        Assert.IsNotNull(tsa.ResidualPlot, "ResidualPlot must not be null.");
        Assert.IsNotNull(tsa.ResidualHistogramPlot, "ResidualHistogramPlot must not be null.");
        Assert.IsNotNull(tsa.ResidualQQPlot, "ResidualQQPlot must not be null.");
        Assert.IsNotNull(tsa.ResidualACFPlot, "ResidualACFPlot must not be null.");
        Assert.IsNotNull(tsa.ResidualPACFPlot, "ResidualPACFPlot must not be null.");
    }

    /// <summary>
    /// Verifies that residual diagnostic plots have the expected default axis labels.
    /// </summary>
    [STATestMethod]
    public void Constructor_ResidualDiagnosticPlots_HaveAxisLabels()
    {
        var tsa = new UI.TimeSeriesAnalysis("ResidualAxisTSA", _collection!);

        Assert.AreEqual("Residual", GetAxis(tsa.ResidualPlot, "Yaxis").Title);
        Assert.AreEqual("Density", GetAxis(tsa.ResidualHistogramPlot, "Yaxis").Title);
        Assert.AreEqual("Residuals", GetAxis(tsa.ResidualHistogramPlot, "Xaxis").Title);
        Assert.AreEqual("Quantile (Residuals)", GetAxis(tsa.ResidualQQPlot, "Yaxis").Title);
        Assert.AreEqual("Quantile (Standardized)", GetAxis(tsa.ResidualQQPlot, "Xaxis").Title);
    }

    /// <summary>
    /// Verifies that numeric axes on the main and residual plots use integer formatting by default.
    /// </summary>
    [STATestMethod]
    public void Constructor_TimeSeriesAndResidualAxes_HaveDefaultStringFormats()
    {
        var tsa = new UI.TimeSeriesAnalysis("AxisFormatTSA", _collection!);

        Assert.AreEqual("N0", GetAxis(tsa.TimeSeriesPlot, "Yaxis").StringFormat);
        Assert.AreEqual("N0", GetAxis(tsa.ResidualPlot, "Xaxis").StringFormat);
    }

    /// <summary>
    /// Verifies that <see cref="UI.TimeSeriesAnalysis.BayesianPlots"/> is not null after construction.
    /// </summary>
    [STATestMethod]
    public void Constructor_BayesianPlots_IsNotNull()
    {
        var tsa = new UI.TimeSeriesAnalysis("BpTSA", _collection!);

        Assert.IsNotNull(tsa.BayesianPlots);
    }

    /// <summary>
    /// Verifies that the Description setter raises PropertyChanged.
    /// </summary>
    [STATestMethod]
    public void Description_Setter_RaisesPropertyChanged()
    {
        var tsa = new UI.TimeSeriesAnalysis("PropTSA", _collection!);
        var raised = new List<string>();
        tsa.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        tsa.Description = "A description";

        Assert.IsTrue(raised.Contains(nameof(UI.TimeSeriesAnalysis.Description)));
    }

    /// <summary>
    /// Verifies that setting Description to the same value does NOT raise PropertyChanged.
    /// </summary>
    [STATestMethod]
    public void Description_SameValue_DoesNotRaisePropertyChanged()
    {
        var tsa = new UI.TimeSeriesAnalysis("PropTSA2", _collection!);
        tsa.Description = "Same";

        var raised = new List<string>();
        tsa.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        tsa.Description = "Same";

        CollectionAssert.DoesNotContain(raised, nameof(UI.TimeSeriesAnalysis.Description));
    }

    /// <summary>
    /// Verifies that <see cref="UI.TimeSeriesAnalysis.CreationDate"/> and <see cref="UI.TimeSeriesAnalysis.LastModified"/>
    /// are within a few seconds of construction time.
    /// </summary>
    [STATestMethod]
    public void Constructor_DatesAreRecent()
    {
        var before = DateTime.Now.AddSeconds(-5);
        var tsa = new UI.TimeSeriesAnalysis("DateTSA", _collection!);
        var after = DateTime.Now.AddSeconds(5);

        Assert.IsTrue(tsa.CreationDate >= before && tsa.CreationDate <= after);
        Assert.IsTrue(tsa.LastModified >= before && tsa.LastModified <= after);
    }

    /// <summary>
    /// Verifies that <see cref="UI.TimeSeriesAnalysis.CancelAnalysis"/> does not throw
    /// when no analysis is running.
    /// </summary>
    [STATestMethod]
    public void CancelAnalysis_WhenNotRunning_DoesNotThrow()
    {
        var tsa = new UI.TimeSeriesAnalysis("CancelTSA", _collection!);

        // Must not throw
        tsa.CancelAnalysis();
    }

    /// <summary>
    /// Verifies that <see cref="UI.TimeSeriesAnalysis.ClearResults"/> does not throw
    /// and leaves IsEstimated as false.
    /// </summary>
    [STATestMethod]
    public void ClearResults_WhenNotEstimated_DoesNotThrow()
    {
        var tsa = new UI.TimeSeriesAnalysis("ClearTSA", _collection!);

        // Must not throw
        tsa.ClearResults();

        Assert.IsFalse(tsa.IsEstimated);
    }

    /// <summary>
    /// Verifies that opening a project that stores the model under the pre-v2.0
    /// <c>ARMAX</c> column (rather than the new <c>ARIMAX</c> column) does not throw
    /// and clears the estimated state.
    /// </summary>
    /// <remarks>
    /// Regression guard for a <see cref="NullReferenceException"/> at
    /// <c>TimeSeriesAnalysisControl.UpdateTimeSeriesPlot</c>:
    /// before the fix, the legacy column was not detected, the <c>ARIMAX</c>
    /// lookup returned null, and the fallback constructed
    /// <c>new ARIMAXAnalysis(new ARIMAX())</c> with a null <c>TimeSeries</c> while
    /// leaving <c>IsEstimated</c> true (because <c>MCMCResults</c> still loaded).
    /// </remarks>
    [STATestMethod]
    public void Open_WithLegacyArmaxColumn_DiscardsResultsWithoutThrowing()
    {
        const string legacyArmaxXml =
            "<ARMAX TransformType=\"None\" IncludeIntercept=\"True\" IncludeSeasonality=\"False\" " +
            "TrendType=\"None\" AROrderP=\"1\" DiffOrderD=\"0\" MAOrderQ=\"0\" XOrderB=\"0\" " +
            "TrainingTimeSteps=\"4\" ForecastingTimeSteps=\"1\" UseDefaultTrainingSteps=\"True\">" +
            "<Parameters>" +
            "<ModelParameter Name=\"Intercept (μ)\" Value=\"5.0\" />" +
            "<ModelParameter Name=\"AR (𝚽₁)\" Value=\"0.45\" />" +
            "<ModelParameter Name=\"Sigma (σ)\" Value=\"1.0\" />" +
            "</Parameters>" +
            "</ARMAX>";

        string tempPath = Path.Combine(Path.GetTempPath(), $"rmcbf-tsa-test-{Guid.NewGuid():N}.bestfit");
        try
        {
            BuildLegacyTimeSeriesAnalysisDatabase(tempPath, "LegacyTSA", legacyArmaxXml);

            var analysis = new UI.TimeSeriesAnalysis("LegacyTSA", _collection!);

            using (var sqlite = new SQLiteManager(tempPath))
            {
                // Must not throw — was the original NullReferenceException source path.
                analysis.Open(sqlite);
            }

            Assert.IsNotNull(analysis.ARIMAX, "ARIMAX must never be null after Open.");
            Assert.IsFalse(analysis.IsEstimated,
                "Legacy MCMC results must be discarded, so IsEstimated must be false.");
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    /// <summary>
    /// Regression test: a SQLite project whose <c>ARIMAX</c> cell contains invalid XML
    /// must not cause <see cref="UI.TimeSeriesAnalysis.Open(SQLiteManager)"/> to throw.
    /// The parse is guarded with try/catch so an unparseable cell falls back to default state
    /// instead of aborting Open() partway.
    /// </summary>
    [STATestMethod]
    public void Open_WithCorruptArimaxXml_DoesNotThrow()
    {
        string tempPath = Path.Combine(Path.GetTempPath(), $"rmcbf-tsa-corrupt-{Guid.NewGuid():N}.bestfit");
        try
        {
            BuildTimeSeriesAnalysisDatabaseWithCorruptArimaxXml(tempPath, "CorruptTSA", "<not-a-valid-xml>");

            var analysis = new UI.TimeSeriesAnalysis("CorruptTSA", _collection!);

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

    /// <summary>
    /// Builds a minimal SQLite <c>.bestfit</c> fixture with a corrupt <c>ARIMAX</c> XML cell.
    /// The referenced input series is intentionally absent.
    /// </summary>
    private static void BuildTimeSeriesAnalysisDatabaseWithCorruptArimaxXml(string path, string analysisName, string corruptXml)
    {
        var anTable = new DataTable("Time Series Analysis");
        anTable.Columns.Add("Name", typeof(string));
        anTable.Columns.Add("Description", typeof(string));
        anTable.Columns.Add("CreationDate", typeof(string));
        anTable.Columns.Add("LastModified", typeof(string));
        anTable.Columns.Add("TimeSeriesData", typeof(string));
        anTable.Columns.Add("ARIMAX", typeof(string));

        using (var sqlite = new SQLiteManager(path))
        {
            sqlite.Open();
            sqlite.SaveDataTable(anTable);

            var anView = sqlite.GetTableManager("Time Series Analysis");
            anView.AddRow();
            anView.EditCell(0, "Name", analysisName);
            anView.EditCell(0, "Description", "");
            anView.EditCell(0, "CreationDate", DateTime.Now.ToString("o"));
            anView.EditCell(0, "LastModified", DateTime.Now.ToString("o"));
            anView.EditCell(0, "TimeSeriesData", "MissingSeries");
            anView.EditCell(0, "ARIMAX", corruptXml);
            anView.ApplyEdits();
            sqlite.Close();
        }
    }

    /// <summary>
    /// Builds a SQLite <c>.bestfit</c> file with only the pre-v2.0 schema for time-series
    /// analysis: an <c>ARMAX</c> TEXT column (not the new <c>ARIMAX</c>) holds the model XML.
    /// The referenced input series is intentionally omitted to keep the test self-contained
    /// without touching the singleton project state — the legacy detection path runs even
    /// when the input series cannot be resolved.
    /// </summary>
    private static void BuildLegacyTimeSeriesAnalysisDatabase(string path, string analysisName, string armaxXml)
    {
        var anTable = new DataTable("Time Series Analysis");
        anTable.Columns.Add("Name", typeof(string));
        anTable.Columns.Add("Description", typeof(string));
        anTable.Columns.Add("CreationDate", typeof(string));
        anTable.Columns.Add("LastModified", typeof(string));
        anTable.Columns.Add("TimeSeriesData", typeof(string));
        anTable.Columns.Add("ARMAX", typeof(string));

        using (var sqlite = new SQLiteManager(path))
        {
            sqlite.Open();
            sqlite.SaveDataTable(anTable);

            var anView = sqlite.GetTableManager("Time Series Analysis");
            anView.AddRow();
            anView.EditCell(0, "Name", analysisName);
            anView.EditCell(0, "Description", "");
            anView.EditCell(0, "CreationDate", DateTime.Now.ToString("o"));
            anView.EditCell(0, "LastModified", DateTime.Now.ToString("o"));
            anView.EditCell(0, "TimeSeriesData", "MissingSeries");
            anView.EditCell(0, "ARMAX", armaxXml);
            anView.ApplyEdits();
            sqlite.Close();
        }
    }

    /// <summary>
    /// Verifies that manually editing <see cref="UI.TimeSeriesAnalysis.TrainingTimeSteps"/>
    /// auto-flips <see cref="UI.TimeSeriesAnalysis.UseDefaultTrainingSteps"/> to <c>false</c>.
    /// </summary>
    /// <remarks>
    /// Regression guard: prior to this flip, a manual Training=N edit left the default-rule
    /// flag at <c>true</c>. Any subsequent model-internal reset path (TimeSeries setter,
    /// CollectionChanged, SetDefaultTrainingSteps) silently overwrote the user's value with
    /// floor(0.8·N), shifting the training window left by ~20%.
    /// </remarks>
    [STATestMethod]
    public void TrainingTimeSteps_ManualEdit_DisablesUseDefault()
    {
        var tsa = new UI.TimeSeriesAnalysis("ManualTrainTSA", _collection!);
        var tsElement = new TimeSeriesElement("ManualTrainTS");
        var baseDate = new DateTime(2020, 1, 1);
        for (int i = 0; i < 50; i++)
            tsElement.TimeSeries.Add(new SeriesOrdinate<DateTime, double>(baseDate.AddMonths(i), i + 1.0));
        tsa.TimeSeriesData = tsElement;

        // Precondition: fresh wrapper defaults to the 80% rule.
        Assert.IsTrue(tsa.UseDefaultTrainingSteps, "Precondition: UseDefaultTrainingSteps should start true.");

        tsa.TrainingTimeSteps = 50;

        Assert.IsFalse(tsa.UseDefaultTrainingSteps, "Manual Training edit must disable the 80% default rule.");
        Assert.AreEqual(50, tsa.TrainingTimeSteps, "Training value must stick at the user-specified N.");
        Assert.AreEqual(50, tsa.ARIMAX.TrainingTimeSteps, "Underlying ARIMAX model must match the UI wrapper value.");
    }

    /// <summary>
    /// Verifies that <see cref="UI.TimeSeriesAnalysis.UseDefaultTrainingSteps"/> stays <c>true</c>
    /// when <see cref="UI.TimeSeriesAnalysis.TrainingTimeSteps"/> is assigned its current value
    /// (no-op write should not disturb the flag).
    /// </summary>
    [STATestMethod]
    public void TrainingTimeSteps_NoOpWrite_DoesNotDisableUseDefault()
    {
        var tsa = new UI.TimeSeriesAnalysis("NoOpTrainTSA", _collection!);
        var tsElement = new TimeSeriesElement("NoOpTrainTS");
        var baseDate = new DateTime(2020, 1, 1);
        for (int i = 0; i < 50; i++)
            tsElement.TimeSeries.Add(new SeriesOrdinate<DateTime, double>(baseDate.AddMonths(i), i + 1.0));
        tsa.TimeSeriesData = tsElement;

        int currentTraining = tsa.TrainingTimeSteps;

        tsa.TrainingTimeSteps = currentTraining;

        Assert.IsTrue(tsa.UseDefaultTrainingSteps, "A no-op write must not disable the default rule.");
    }

    /// <summary>
    /// Verifies changing the selected input element resets training defaults while preserving forecast steps.
    /// </summary>
    [STATestMethod]
    public void TimeSeriesDataChanged_ResetsTrainingDefaultAndPreservesForecastSteps()
    {
        var tsa = new UI.TimeSeriesAnalysis("InputChangeTSA", _collection!);
        var first = CreateTimeSeriesElement("InputChangeFirst", 80, TimeInterval.OneMonth, new DateTime(2000, 1, 1));
        var second = CreateTimeSeriesElement("InputChangeSecond", 120, TimeInterval.OneMonth, new DateTime(2010, 1, 1));
        tsa.TimeSeriesData = first;
        tsa.ForecastSteps = 12;
        tsa.TrainingTimeSteps = 50;

        tsa.TimeSeriesData = second;

        Assert.IsTrue(tsa.UseDefaultTrainingSteps);
        Assert.AreEqual((int)Math.Floor(0.8 * second.TimeSeries.Count), tsa.TrainingTimeSteps);
        Assert.AreEqual(12, tsa.ForecastSteps);
        Assert.AreSame(second.TimeSeries, tsa.ARIMAX.TimeSeries);
    }

    /// <summary>
    /// Verifies replacing the selected element's underlying series refreshes the model input and training split.
    /// </summary>
    [STATestMethod]
    public void SelectedTimeSeriesReplaced_ResyncsModelAndResetsTrainingDefault()
    {
        var tsa = new UI.TimeSeriesAnalysis("UnderlyingChangeTSA", _collection!);
        var element = CreateTimeSeriesElement("UnderlyingChangeSeries", 80, TimeInterval.OneMonth, new DateTime(2000, 1, 1));
        var replacement = new TimeSeries(TimeInterval.OneYear);
        DateTime date = new DateTime(1950, 1, 1);
        for (int i = 0; i < 60; i++)
        {
            replacement.Add(new SeriesOrdinate<DateTime, double>(date, 100.0 + i));
            date = TimeSeries.AddTimeInterval(date, replacement.TimeInterval);
        }
        tsa.TimeSeriesData = element;
        tsa.ForecastSteps = 9;
        tsa.TrainingTimeSteps = 50;

        element.TimeSeries = replacement;

        Assert.IsTrue(tsa.UseDefaultTrainingSteps);
        Assert.AreEqual((int)Math.Floor(0.8 * replacement.Count), tsa.TrainingTimeSteps);
        Assert.AreEqual(9, tsa.ForecastSteps);
        Assert.AreSame(replacement, tsa.ARIMAX.TimeSeries);
    }

    /// <summary>
    /// Verifies that an otherwise valid irregular input series makes the analysis invalid.
    /// </summary>
    [STATestMethod]
    public void TimeSeriesData_IrregularInterval_IsInvalidForAnalysis()
    {
        var tsa = new UI.TimeSeriesAnalysis("IrregularIntervalTSA", _collection!);
        var element = new TimeSeriesElement("IrregularIntervalTS");
        var series = new TimeSeries(TimeInterval.Irregular);
        var start = new DateTime(2000, 1, 1);
        for (int i = 0; i < 50; i++)
            series.Add(new SeriesOrdinate<DateTime, double>(start.AddDays(i * i + 1), i + 1.0));
        element.TimeSeries = series;

        tsa.TimeSeriesData = element;

        Assert.IsFalse(tsa.IsValid);
        Assert.AreSame(series, tsa.ARIMAX.TimeSeries);
    }

    /// <summary>
    /// Verifies that direct ARIMAX Box-Cox edits re-sync model validation messages.
    /// </summary>
    [STATestMethod]
    public void ARIMAXTransformType_BoxCoxLambdaFailure_AddsValidationMessage()
    {
        var tsa = new UI.TimeSeriesAnalysis("BoxCoxFailureTSA", _collection!);

        try
        {
            var tsElement = CreateBoxCoxLambdaFailureTimeSeriesElement("BoxCoxFailureTS");
            tsa.TimeSeriesData = tsElement;

            tsa.ARIMAX.TransformType = RMC.BestFit.Models.Transform.BoxCox;

            Assert.IsFalse(tsa.IsValid);
            Assert.IsTrue(Messenger.GetInstance().AllMessageItems().Any(m =>
                ReferenceEquals(m.Source, tsa)
                && m.Code != null
                && m.Code.StartsWith("TSA-ERR-", StringComparison.Ordinal)),
                "Expected a TSA model-validation error for the analysis source.");
        }
        finally
        {
            Messenger.GetInstance().Clear(tsa);
        }
    }
    /// <summary>
    /// Verifies that <see cref="UI.TimeSeriesAnalysis.CovariateExtension"/> defaults to
    /// <see cref="RMC.BestFit.Models.ARIMAX.CovariateExtensionMethod.BlockBootstrap"/>.
    /// </summary>
    [STATestMethod]
    public void CovariateExtension_Default_IsBlockBootstrap()
    {
        var tsa = new UI.TimeSeriesAnalysis("CovExtTSA", _collection!);

        Assert.AreEqual(
            RMC.BestFit.Models.ARIMAX.CovariateExtensionMethod.BlockBootstrap,
            tsa.CovariateExtension,
            "Default CovariateExtension should be BlockBootstrap, matching the ARIMAX model default.");
    }

    /// <summary>
    /// Verifies that <see cref="UI.TimeSeriesAnalysis.CovariateExtension"/> round-trips through
    /// each enum value, raises <c>PropertyChanged</c>, and writes through to the underlying model.
    /// </summary>
    [STATestMethod]
    public void CovariateExtension_Setter_RoundTripsAndRaisesPropertyChanged()
    {
        var tsa = new UI.TimeSeriesAnalysis("CovExtRoundTSA", _collection!);
        var raised = new List<string>();
        tsa.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        tsa.CovariateExtension = RMC.BestFit.Models.ARIMAX.CovariateExtensionMethod.None;
        Assert.AreEqual(RMC.BestFit.Models.ARIMAX.CovariateExtensionMethod.None, tsa.CovariateExtension);
        Assert.AreEqual(RMC.BestFit.Models.ARIMAX.CovariateExtensionMethod.None, tsa.ARIMAX.CovariateExtension);

        tsa.CovariateExtension = RMC.BestFit.Models.ARIMAX.CovariateExtensionMethod.KNN;
        Assert.AreEqual(RMC.BestFit.Models.ARIMAX.CovariateExtensionMethod.KNN, tsa.CovariateExtension);
        Assert.AreEqual(RMC.BestFit.Models.ARIMAX.CovariateExtensionMethod.KNN, tsa.ARIMAX.CovariateExtension);

        tsa.CovariateExtension = RMC.BestFit.Models.ARIMAX.CovariateExtensionMethod.BlockBootstrap;
        Assert.AreEqual(RMC.BestFit.Models.ARIMAX.CovariateExtensionMethod.BlockBootstrap, tsa.CovariateExtension);
        Assert.AreEqual(RMC.BestFit.Models.ARIMAX.CovariateExtensionMethod.BlockBootstrap, tsa.ARIMAX.CovariateExtension);

        Assert.IsTrue(raised.Contains(nameof(UI.TimeSeriesAnalysis.CovariateExtension)),
            "PropertyChanged must fire for CovariateExtension changes.");
    }

    /// <summary>
    /// Verifies that setting <see cref="UI.TimeSeriesAnalysis.CovariateExtension"/> to its
    /// current value does NOT raise <c>PropertyChanged</c>.
    /// </summary>
    [STATestMethod]
    public void CovariateExtension_SameValue_DoesNotRaisePropertyChanged()
    {
        var tsa = new UI.TimeSeriesAnalysis("CovExtSameTSA", _collection!);
        tsa.CovariateExtension = RMC.BestFit.Models.ARIMAX.CovariateExtensionMethod.KNN;

        var raised = new List<string>();
        tsa.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        tsa.CovariateExtension = RMC.BestFit.Models.ARIMAX.CovariateExtensionMethod.KNN;

        CollectionAssert.DoesNotContain(raised, nameof(UI.TimeSeriesAnalysis.CovariateExtension));
    }

    /// <summary>
    /// Verifies that <see cref="UI.TimeSeriesAnalysis.InnerAnalysis"/> is not null after construction.
    /// </summary>
    [STATestMethod]
    public void InnerAnalysis_IsNotNull()
    {
        var tsa = new UI.TimeSeriesAnalysis("InnerTSA", _collection!);
        Assert.IsNotNull(tsa.InnerAnalysis);
    }

    /// <summary>
    /// Verifies that <see cref="UI.TimeSeriesAnalysis.AnalysisResults"/> is null until the analysis runs.
    /// </summary>
    [STATestMethod]
    public void AnalysisResults_BeforeRun_IsNull()
    {
        var tsa = new UI.TimeSeriesAnalysis("ResTSA", _collection!);
        Assert.IsNull(tsa.AnalysisResults);
    }

    /// <summary>
    /// Verifies that <see cref="UI.TimeSeriesAnalysis.RaisePreviewSaved"/> does not throw or flip
    /// cancel when there are no PreviewObjectSaved subscribers.
    /// </summary>
    [STATestMethod]
    public void RaisePreviewSaved_NoSubscribers_DoesNotFlipCancel()
    {
        var tsa = new UI.TimeSeriesAnalysis("PrevTSA", _collection!);
        bool cancel = false;

        tsa.RaisePreviewSaved(ref cancel);

        Assert.IsFalse(cancel);
    }

    /// <summary>
    /// Verifies that <see cref="UI.TimeSeriesAnalysis.ElementImageResourceKey"/> returns the expected key.
    /// </summary>
    [STATestMethod]
    public void ElementImageResourceKey_ReturnsExpectedKey()
    {
        var tsa = new UI.TimeSeriesAnalysis("ImgKeyTSA", _collection!);
        Assert.AreEqual("TimeSeriesAnalysisIcon", tsa.ElementImageResourceKey);
    }

    /// <summary>
    /// Verifies that <see cref="UI.TimeSeriesAnalysis.IsValid"/> is <c>false</c> on a freshly
    /// constructed instance because TimeSeriesData has not been assigned.
    /// </summary>
    [STATestMethod]
    public void Constructor_IsValid_IsFalseInitiallyDueToMissingData()
    {
        var tsa = new UI.TimeSeriesAnalysis("IsValidTSA", _collection!);
        Assert.IsFalse(tsa.IsValid);
    }

    /// <summary>
    /// Verifies that <see cref="UI.TimeSeriesAnalysis.Name"/> setter raises PropertyChanged on rename.
    /// </summary>
    [STATestMethod]
    public void Name_Setter_RaisesPropertyChanged()
    {
        var tsa = new UI.TimeSeriesAnalysis("NameTSA", _collection!);
        var raised = new List<string>();
        tsa.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        tsa.Name = "NameTSA-Renamed";

        Assert.IsTrue(raised.Contains(nameof(UI.TimeSeriesAnalysis.Name)));
        Assert.AreEqual("NameTSA-Renamed", tsa.Name);
    }

    /// <summary>
    /// Verifies that <see cref="UI.TimeSeriesAnalysis.BayesianPlots"/> is not null after construction.
    /// </summary>
    [STATestMethod]
    public void BayesianPlots_IsNotNull()
    {
        var tsa = new UI.TimeSeriesAnalysis("BPTSA", _collection!);
        Assert.IsNotNull(tsa.BayesianPlots);
    }

    /// <summary>
    /// Gets an axis from a plot by key.
    /// </summary>
    /// <param name="plot">The plot containing the target axis.</param>
    /// <param name="key">The axis key.</param>
    /// <returns>The matching axis.</returns>
    private static OxyPlot.Wpf.Axis GetAxis(OxyPlot.Wpf.Plot plot, string key)
    {
        return plot.Axes.First(a => a.Key == key);
    }
}
