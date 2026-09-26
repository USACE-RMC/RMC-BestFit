using DatabaseManager;
using FrameworkInterfaces;
using FrameworkInterfaces.Messaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Numerics.Data;
using RMC.BestFit.Models;
using RMC.BestFit.UI;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Xml.Linq;

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
    /// Creates a time-series element with deterministic regularly spaced values, registered
    /// against the given <see cref="TimeSeriesCollection"/> instead of the dummy singleton-backed
    /// collection the parameterless <see cref="TimeSeriesElement"/> constructor would otherwise
    /// create.
    /// </summary>
    /// <param name="name">The element name.</param>
    /// <param name="count">The number of observations to create.</param>
    /// <param name="interval">The interval between observations.</param>
    /// <param name="start">The first observation timestamp.</param>
    /// <param name="parentCollection">
    /// The collection the element's <c>ParentCollection</c> resolves to. Must be supplied whenever
    /// the element is later saved (e.g. via <see cref="TimeSeriesCollection.Add"/>), since
    /// <see cref="UI.TimeSeriesElement.Save"/> persists to <c>ParentCollection.ParentProject</c>'s
    /// file, not the caller's own project reference.
    /// </param>
    /// <param name="valueAt">
    /// Optional value of the observation at each zero-based step; <see langword="null"/> uses
    /// <c>i + 1</c>.
    /// </param>
    /// <returns>A populated time-series element bound to <paramref name="parentCollection"/>.</returns>
    private static TimeSeriesElement CreateTimeSeriesElementInCollection(string name, int count, TimeInterval interval, DateTime start,
        TimeSeriesCollection parentCollection, Func<int, double>? valueAt = null)
    {
        var element = new TimeSeriesElement(name, parentCollection);
        var series = new TimeSeries(interval);
        DateTime date = start;
        for (int i = 0; i < count; i++)
        {
            series.Add(new SeriesOrdinate<DateTime, double>(date, valueAt?.Invoke(i) ?? i + 1.0));
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

    // -----------------------------------------------------------------------------------------
    // Task 2.9 / decision D2: warn when a time-series analysis opens with results computed by an
    // earlier version of RMC-BestFit. IsPreV201TransformResult is the internal predicate Open()
    // consults; it is exercised directly here with inline ARIMAX/XElement fixtures (no SQLite, no
    // optimizer), plus a small number of true Open()-level round trips for the cases that need to
    // prove the SQLite wiring itself (attribute detection on the persisted cell, message add/remove).
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// Verifies the warning never fires for an unestimated analysis, regardless of model shape.
    /// </summary>
    [STATestMethod]
    public void IsPreV201TransformResult_NotEstimated_ReturnsFalse()
    {
        var tsa = new UI.TimeSeriesAnalysis("PredNotEstTSA", _collection!);
        tsa.TimeSeriesData = CreateTimeSeriesElement("PredNotEstResponse", 30, TimeInterval.OneYear, new DateTime(1990, 1, 1));
        tsa.Covariates.Add(new CovariateData { TimeSeriesElement = CreateTimeSeriesElement("PredNotEstCovariate", 30, TimeInterval.OneYear, new DateTime(1990, 1, 1)) });
        XElement modelXml = tsa.ARIMAX.ToXElement();
        modelXml.Attribute(nameof(ARIMAX.TransformLambda))!.Remove();

        Assert.IsFalse(UI.TimeSeriesAnalysis.IsPreV201TransformResult(false, modelXml, tsa.ARIMAX));
    }

    /// <summary>
    /// Verifies a model saved by v2.0.1+ (the <c>TransformLambda</c> attribute is present) never
    /// warns, even when it has a covariate.
    /// </summary>
    [STATestMethod]
    public void IsPreV201TransformResult_TransformLambdaPresent_ReturnsFalse()
    {
        var tsa = new UI.TimeSeriesAnalysis("PredLambdaPresentTSA", _collection!);
        tsa.TimeSeriesData = CreateTimeSeriesElement("PredLambdaPresentResponse", 30, TimeInterval.OneYear, new DateTime(1990, 1, 1));
        tsa.Covariates.Add(new CovariateData { TimeSeriesElement = CreateTimeSeriesElement("PredLambdaPresentCovariate", 30, TimeInterval.OneYear, new DateTime(1990, 1, 1)) });
        XElement modelXml = tsa.ARIMAX.ToXElement();

        Assert.IsTrue(modelXml.Attribute(nameof(ARIMAX.TransformLambda)) != null,
            "Sanity check: production ToXElement() must write TransformLambda unconditionally.");
        Assert.IsFalse(UI.TimeSeriesAnalysis.IsPreV201TransformResult(true, modelXml, tsa.ARIMAX));
    }

    /// <summary>
    /// Verifies the primary case from the task brief: an estimated model with a covariate, saved
    /// without <c>TransformLambda</c> (a v2.0.0 save), must warn.
    /// </summary>
    [STATestMethod]
    public void IsPreV201TransformResult_EstimatedCovariateModelMissingTransformLambda_ReturnsTrue()
    {
        var tsa = new UI.TimeSeriesAnalysis("PredCovariateTSA", _collection!);
        tsa.TimeSeriesData = CreateTimeSeriesElement("PredCovariateResponse", 30, TimeInterval.OneYear, new DateTime(1990, 1, 1));
        tsa.Covariates.Add(new CovariateData { TimeSeriesElement = CreateTimeSeriesElement("PredCovariateCovariate", 30, TimeInterval.OneYear, new DateTime(1990, 1, 1)) });
        XElement modelXml = tsa.ARIMAX.ToXElement();
        modelXml.Attribute(nameof(ARIMAX.TransformLambda))!.Remove();

        Assert.IsTrue(UI.TimeSeriesAnalysis.IsPreV201TransformResult(true, modelXml, tsa.ARIMAX));
    }

    /// <summary>
    /// Verifies a covariate-free, undifferenced, untransformed model saved without
    /// <c>TransformLambda</c> does not warn: none of v2.0.1's changes (covariate alignment, fitted
    /// transform, differenced training and reintegration windows, narrower conditioning window)
    /// apply to it.
    /// </summary>
    [STATestMethod]
    public void IsPreV201TransformResult_NoCovariatesNoFittedTransform_ReturnsFalse()
    {
        var tsa = new UI.TimeSeriesAnalysis("PredNoCovNoneTSA", _collection!);
        tsa.TimeSeriesData = CreateTimeSeriesElement("PredNoCovNoneResponse", 30, TimeInterval.OneYear, new DateTime(1990, 1, 1));
        // Defaults: DiffOrderD = 0, TransformType = None, AROrderP = 1, MAOrderQ = 0, XOrderB = 0 (<= max(1, 0)).
        Assert.AreEqual(0, tsa.ARIMAX.DiffOrderD, "Precondition: the model is not differenced.");
        Assert.AreEqual(RMC.BestFit.Models.Transform.None, tsa.ARIMAX.TransformType, "Precondition: no transform.");
        Assert.IsTrue(tsa.ARIMAX.XOrderB <= Math.Max(tsa.ARIMAX.AROrderP, tsa.ARIMAX.MAOrderQ), "Precondition: XOrderB <= max(p, q).");
        XElement modelXml = tsa.ARIMAX.ToXElement();
        modelXml.Attribute(nameof(ARIMAX.TransformLambda))!.Remove();

        Assert.IsFalse(UI.TimeSeriesAnalysis.IsPreV201TransformResult(true, modelXml, tsa.ARIMAX));
    }

    /// <summary>
    /// Verifies a differenced (d = 1), covariate-free, untransformed model saved without
    /// <c>TransformLambda</c> warns: v2.0.0 trained differenced models on the first T differences
    /// instead of the T − d differences inside the raw training prefix (TR-041) and reintegrated
    /// their saved curves one step off (TR-037).
    /// </summary>
    [STATestMethod]
    public void IsPreV201TransformResult_DifferencedModelWithoutCovariatesOrFittedTransform_ReturnsTrue()
    {
        var tsa = new UI.TimeSeriesAnalysis("PredDifferencedTSA", _collection!);
        tsa.TimeSeriesData = CreateLevelTimeSeriesElement("PredDifferencedResponse");
        tsa.ARIMAX.DiffOrderD = 1;
        Assert.AreEqual(RMC.BestFit.Models.Transform.None, tsa.ARIMAX.TransformType, "Precondition: no transform.");
        Assert.IsTrue(tsa.ARIMAX.XOrderB <= Math.Max(tsa.ARIMAX.AROrderP, tsa.ARIMAX.MAOrderQ), "Precondition: XOrderB <= max(p, q).");
        XElement modelXml = tsa.ARIMAX.ToXElement();
        modelXml.Attribute(nameof(ARIMAX.TransformLambda))!.Remove();

        Assert.IsTrue(UI.TimeSeriesAnalysis.IsPreV201TransformResult(true, modelXml, tsa.ARIMAX));
    }

    /// <summary>
    /// Verifies a covariate-free model using a fitted Box-Cox transform warns, per the brief's
    /// "or its TransformType is Box-Cox or Yeo-Johnson" clause.
    /// </summary>
    [STATestMethod]
    public void IsPreV201TransformResult_NoCovariatesBoxCoxTransform_ReturnsTrue()
    {
        var tsa = new UI.TimeSeriesAnalysis("PredBoxCoxTSA", _collection!);
        tsa.TimeSeriesData = CreateTimeSeriesElement("PredBoxCoxResponse", 30, TimeInterval.OneYear, new DateTime(1990, 1, 1));
        tsa.ARIMAX.TransformType = RMC.BestFit.Models.Transform.BoxCox;
        XElement modelXml = tsa.ARIMAX.ToXElement();
        modelXml.Attribute(nameof(ARIMAX.TransformLambda))!.Remove();

        Assert.IsTrue(UI.TimeSeriesAnalysis.IsPreV201TransformResult(true, modelXml, tsa.ARIMAX));
    }

    /// <summary>
    /// Verifies the controller ruling's third clause: a covariate-free model where
    /// <c>XOrderB &gt; max(AROrderP, MAOrderQ)</c> warns, because Task 2.8 narrowed the
    /// conditioning window K for that combination.
    /// </summary>
    [STATestMethod]
    public void IsPreV201TransformResult_NoCovariatesNarrowerConditioningWindow_ReturnsTrue()
    {
        var tsa = new UI.TimeSeriesAnalysis("PredNarrowKTSA", _collection!);
        tsa.TimeSeriesData = CreateTimeSeriesElement("PredNarrowKResponse", 30, TimeInterval.OneYear, new DateTime(1990, 1, 1));
        // No covariates. AROrderP defaults to 1, MAOrderQ defaults to 0 -> max(p, q) = 1.
        tsa.ARIMAX.XOrderB = 2; // 2 > 1: Task 2.8 narrowed K from the pre-2.8 window for this case.
        XElement modelXml = tsa.ARIMAX.ToXElement();
        modelXml.Attribute(nameof(ARIMAX.TransformLambda))!.Remove();

        Assert.IsTrue(UI.TimeSeriesAnalysis.IsPreV201TransformResult(true, modelXml, tsa.ARIMAX));
    }

    /// <summary>
    /// Verifies the boundary of the third clause: <c>XOrderB == max(AROrderP, MAOrderQ)</c> uses
    /// the same conditioning window before and after Task 2.8, so it must not warn.
    /// </summary>
    [STATestMethod]
    public void IsPreV201TransformResult_NoCovariatesXOrderBAtBoundary_ReturnsFalse()
    {
        var tsa = new UI.TimeSeriesAnalysis("PredBoundaryKTSA", _collection!);
        tsa.TimeSeriesData = CreateTimeSeriesElement("PredBoundaryKResponse", 30, TimeInterval.OneYear, new DateTime(1990, 1, 1));
        tsa.ARIMAX.AROrderP = 2;
        tsa.ARIMAX.XOrderB = 2; // 2 <= max(2, 0) = 2: not narrower than before Task 2.8.
        XElement modelXml = tsa.ARIMAX.ToXElement();
        modelXml.Attribute(nameof(ARIMAX.TransformLambda))!.Remove();

        Assert.IsFalse(UI.TimeSeriesAnalysis.IsPreV201TransformResult(true, modelXml, tsa.ARIMAX));
    }

    /// <summary>
    /// Verifies a <see langword="null"/> model XElement (no ARIMAX XML was ever saved) is treated
    /// the same as a missing attribute, not as a reason to skip the check.
    /// </summary>
    [STATestMethod]
    public void IsPreV201TransformResult_NullModelXElement_TreatedAsMissingAttribute()
    {
        var tsa = new UI.TimeSeriesAnalysis("PredNullXmlTSA", _collection!);
        tsa.TimeSeriesData = CreateTimeSeriesElement("PredNullXmlResponse", 30, TimeInterval.OneYear, new DateTime(1990, 1, 1));
        tsa.Covariates.Add(new CovariateData { TimeSeriesElement = CreateTimeSeriesElement("PredNullXmlCovariate", 30, TimeInterval.OneYear, new DateTime(1990, 1, 1)) });

        Assert.IsTrue(UI.TimeSeriesAnalysis.IsPreV201TransformResult(true, null, tsa.ARIMAX));
    }

    /// <summary>
    /// Open()-level negative case: an unresolved input series ('MissingSeries') leaves the
    /// reconstructed analysis unestimated, so the pre-v2.0.1 transform-results warning must not
    /// appear. Exercises the real Open() wiring (as opposed to the predicate directly) for the
    /// "no results" case without needing a resolvable time series.
    /// </summary>
    [STATestMethod]
    public void Open_NoResults_DoesNotAddLegacyTransformWarning()
    {
        const string arimaxXmlWithoutTransformLambda =
            "<ARIMAX TransformType=\"None\" IncludeIntercept=\"True\" IncludeSeasonality=\"False\" " +
            "TrendType=\"None\" AROrderP=\"1\" DiffOrderD=\"0\" MAOrderQ=\"0\" XOrderB=\"0\" " +
            "TrainingTimeSteps=\"4\" UseDefaultTrainingSteps=\"True\">" +
            "<Parameters>" +
            "<ModelParameter Name=\"Intercept (μ)\" Value=\"5.0\" />" +
            "<ModelParameter Name=\"AR (𝚽₁)\" Value=\"0.45\" />" +
            "<ModelParameter Name=\"Sigma (σ)\" Value=\"1.0\" />" +
            "</Parameters>" +
            "</ARIMAX>";

        string tempPath = Path.Combine(Path.GetTempPath(), $"rmcbf-tsa-noresults-{Guid.NewGuid():N}.bestfit");
        try
        {
            BuildTimeSeriesAnalysisDatabaseWithCorruptArimaxXml(tempPath, "NoResultsTSA", arimaxXmlWithoutTransformLambda);

            var analysis = new UI.TimeSeriesAnalysis("NoResultsTSA", _collection!);
            using (var sqlite = new SQLiteManager(tempPath))
            {
                analysis.Open(sqlite);
            }

            Assert.IsFalse(analysis.IsEstimated,
                "Sanity check: the referenced input series is unresolved ('MissingSeries'), so nothing can be restored.");
            Assert.IsFalse(MessengerHas(analysis, "TSA-WRN-LEGACY-TRANSFORM"),
                "No results were restored, so the pre-v2.0.1 transform-results warning must not appear.");
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    /// <summary>
    /// Open()-level positive case: an estimated covariate model saved without
    /// <c>TransformLambda</c> (simulating a v2.0.0 save) must add the pre-v2.0.1
    /// transform-results warning, and the warning must disappear once results are cleared.
    /// </summary>
    [STATestMethod]
    public void Open_EstimatedCovariateModelMissingTransformLambda_AddsWarningThatClearsWithResults()
    {
        string path = Path.Combine(Path.GetTempPath(), $"BestFit-TSALegacyTransform-{Guid.NewGuid():N}.bestfit");
        try
        {
            UI.TimeSeriesAnalysis analysis = BuildAndOpenLegacyTransformFixture(
                path, "LegacyTransformTSA", includeCovariate: true, keepTransformLambda: false, isEstimated: true);

            Assert.IsTrue(analysis.IsEstimated, "Sanity check: Open() must restore IsEstimated=true from the AnalysisXml cell.");
            Assert.IsTrue(MessengerHas(analysis, "TSA-WRN-LEGACY-TRANSFORM"),
                "An estimated covariate model saved without TransformLambda must warn that results predate v2.0.1.");

            analysis.ClearResults();

            Assert.IsFalse(analysis.IsEstimated);
            Assert.IsFalse(MessengerHas(analysis, "TSA-WRN-LEGACY-TRANSFORM"),
                "Clearing the results must remove the warning immediately, not only after a successful re-run.");
        }
        finally
        {
            DeleteFixtureFile(path);
        }
    }

    /// <summary>
    /// Open()-level negative case: the same covariate model, but saved by the current version
    /// (<c>TransformLambda</c> present), must not warn.
    /// </summary>
    [STATestMethod]
    public void Open_EstimatedCovariateModelWithTransformLambda_DoesNotAddLegacyTransformWarning()
    {
        string path = Path.Combine(Path.GetTempPath(), $"BestFit-TSACurrentTransform-{Guid.NewGuid():N}.bestfit");
        try
        {
            UI.TimeSeriesAnalysis analysis = BuildAndOpenLegacyTransformFixture(
                path, "CurrentTransformTSA", includeCovariate: true, keepTransformLambda: true, isEstimated: true);

            Assert.IsTrue(analysis.IsEstimated, "Sanity check: Open() must restore IsEstimated=true from the AnalysisXml cell.");
            Assert.IsFalse(MessengerHas(analysis, "TSA-WRN-LEGACY-TRANSFORM"),
                "A model saved by the current version (TransformLambda present) must not warn.");
        }
        finally
        {
            DeleteFixtureFile(path);
        }
    }

    /// <summary>
    /// Open()-level negative case: an estimated covariate-free, untransformed model saved without
    /// <c>TransformLambda</c> must not warn, matching the brief's explicit acceptance criterion.
    /// </summary>
    [STATestMethod]
    public void Open_EstimatedModelWithoutCovariatesOrFittedTransform_DoesNotAddLegacyTransformWarning()
    {
        string path = Path.Combine(Path.GetTempPath(), $"BestFit-TSANoCovariate-{Guid.NewGuid():N}.bestfit");
        try
        {
            UI.TimeSeriesAnalysis analysis = BuildAndOpenLegacyTransformFixture(
                path, "NoCovariateTransformTSA", includeCovariate: false, keepTransformLambda: false, isEstimated: true);

            Assert.IsTrue(analysis.IsEstimated, "Sanity check: Open() must restore IsEstimated=true from the AnalysisXml cell.");
            Assert.IsFalse(MessengerHas(analysis, "TSA-WRN-LEGACY-TRANSFORM"),
                "A covariate-free, untransformed model is unaffected by v2.0.1's changes, so it must not warn " +
                "even though TransformLambda is absent.");
        }
        finally
        {
            DeleteFixtureFile(path);
        }
    }

    /// <summary>
    /// Open()-level positive case for differenced models: an estimated d = 1 model without
    /// covariates or a fitted transform, saved without <c>TransformLambda</c>, must warn, and the
    /// warning must name the corrected differenced training and reintegration windows.
    /// </summary>
    [STATestMethod]
    public void Open_EstimatedDifferencedModelMissingTransformLambda_AddsWarning()
    {
        string path = Path.Combine(Path.GetTempPath(), $"BestFit-TSALegacyDifferenced-{Guid.NewGuid():N}.bestfit");
        try
        {
            UI.TimeSeriesAnalysis analysis = BuildAndOpenLegacyTransformFixture(
                path, "LegacyDifferencedTSA", includeCovariate: false, keepTransformLambda: false, isEstimated: true,
                differenceOrder: 1);

            Assert.IsTrue(analysis.IsEstimated, "Sanity check: Open() must restore IsEstimated=true from the AnalysisXml cell.");
            Assert.AreEqual(1, analysis.ARIMAX.DiffOrderD, "Sanity check: the differenced model is restored.");
            BasicMessageItem? warning = MessengerMessage(analysis, "TSA-WRN-LEGACY-TRANSFORM");
            Assert.IsNotNull(warning, "An estimated differenced model saved before v2.0.1 must warn.");
            Assert.AreEqual(
                $"The results of time series analysis '{analysis.Name}' were computed by an earlier version of RMC-BestFit. " +
                "This version fits the transform exponent on the training window, aligns covariates by date, trains and " +
                "reintegrates differenced models on corrected windows, and uses a revised conditioning window, so " +
                "reprocessed forecasts would combine the saved results with different model settings. Re-run the " +
                "Bayesian analysis to refresh the results.",
                warning.Description);
        }
        finally
        {
            DeleteFixtureFile(path);
        }
    }

    /// <summary>
    /// Verifies the pre-v2.0.1 results warning survives a save that does not re-run the analysis.
    /// </summary>
    /// <remarks>
    /// The save rewrites the model XML with <c>TransformLambda</c>, so on the next open only the
    /// persisted marker can still report that the unchanged results predate v2.0.1.
    /// </remarks>
    [STATestMethod]
    public void Open_LegacyResultsSavedWithoutRerun_KeepsWarningOnReopen()
    {
        string path = Path.Combine(Path.GetTempPath(), $"BestFit-TSALegacySave-{Guid.NewGuid():N}.bestfit");
        try
        {
            UI.TimeSeriesAnalysis analysis = BuildAndOpenLegacyTransformFixture(
                path, "LegacySaveTSA", includeCovariate: true, keepTransformLambda: false, isEstimated: true);
            Assert.IsTrue(MessengerHas(analysis, "TSA-WRN-LEGACY-TRANSFORM"), "Precondition: the legacy results warn on open.");

            analysis.Description = "Edited after opening legacy results";
            Assert.IsTrue(analysis.IsEstimated, "A description edit keeps the results.");
            analysis.Save();

            StringAssert.Contains(ReadSavedCell(path, analysis.Name, "ARIMAX"), nameof(ARIMAX.TransformLambda),
                "Precondition: the save writes TransformLambda, so the model XML no longer reveals the legacy results.");
            UI.TimeSeriesAnalysis reopened = ReopenAnalysis(analysis, path);

            Assert.IsTrue(reopened.IsEstimated, "Sanity check: the saved results are restored.");
            Assert.IsTrue(MessengerHas(reopened, "TSA-WRN-LEGACY-TRANSFORM"),
                "Saving without a re-run leaves the legacy results in place, so the warning must survive the save.");
        }
        finally
        {
            DeleteFixtureFile(path);
        }
    }

    /// <summary>
    /// Verifies clearing the legacy results drops the persisted pre-v2.0.1 marker together with
    /// the warning, so a save after the clear does not warn on the next open.
    /// </summary>
    [STATestMethod]
    public void Open_LegacyResultsClearedAndSaved_DropsMarkerAndDoesNotWarnOnReopen()
    {
        string path = Path.Combine(Path.GetTempPath(), $"BestFit-TSALegacyClear-{Guid.NewGuid():N}.bestfit");
        try
        {
            UI.TimeSeriesAnalysis analysis = BuildAndOpenLegacyTransformFixture(
                path, "LegacyClearTSA", includeCovariate: true, keepTransformLambda: false, isEstimated: true);
            Assert.IsTrue(MessengerHas(analysis, "TSA-WRN-LEGACY-TRANSFORM"), "Precondition: the legacy results warn on open.");

            analysis.ClearResults();
            analysis.Save();

            string? marker = ReadSavedCell(path, analysis.Name, "PreV201Results");
            Assert.IsTrue(bool.TryParse(marker, out bool markerValue),
                $"The save must write the pre-v2.0.1 results marker (read '{marker ?? "<absent>"}').");
            Assert.IsFalse(markerValue, "The marker must drop with the results.");
            UI.TimeSeriesAnalysis reopened = ReopenAnalysis(analysis, path);
            Assert.IsFalse(reopened.IsEstimated, "Sanity check: the cleared results were saved.");
            Assert.IsFalse(MessengerHas(reopened, "TSA-WRN-LEGACY-TRANSFORM"),
                "Results cleared before the save cannot be legacy results, so the reopened analysis must not warn.");
        }
        finally
        {
            DeleteFixtureFile(path);
        }
    }

    /// <summary>
    /// Verifies an undo that rebuilds the model without its results also removes the pre-v2.0.1
    /// results warning.
    /// </summary>
    /// <remarks>
    /// Undoing a <see cref="UI.TimeSeriesAnalysis.CovariateExtension"/> edit rebuilds the inner
    /// analysis, whose covariates are reattached (clearing its results) before this element
    /// subscribes to it again. The edit itself only reprocesses the forecast, so the legacy results
    /// and their warning survive it; the test waits for that background reprocess before undoing.
    /// </remarks>
    [STATestMethod]
    public void CovariateExtensionUndo_OnLegacyResults_RemovesWarningOnceResultsAreGone()
    {
        string path = Path.Combine(Path.GetTempPath(), $"BestFit-TSALegacyUndo-{Guid.NewGuid():N}.bestfit");
        try
        {
            UI.TimeSeriesAnalysis analysis = BuildAndOpenLegacyTransformFixture(
                path, "LegacyUndoTSA", includeCovariate: true, keepTransformLambda: false, isEstimated: true);
            Assert.IsTrue(MessengerHas(analysis, "TSA-WRN-LEGACY-TRANSFORM"), "Precondition: the legacy results warn on open.");

            using (var reprocessed = new ManualResetEventSlim(false))
            {
                System.ComponentModel.PropertyChangedEventHandler onResults = (_, e) =>
                {
                    if (e.PropertyName == nameof(UI.TimeSeriesAnalysis.AnalysisResults)) reprocessed.Set();
                };
                analysis.PropertyChanged += onResults;
                try
                {
                    analysis.CovariateExtension = ARIMAX.CovariateExtensionMethod.KNN;
                    Assert.IsTrue(reprocessed.Wait(TimeSpan.FromSeconds(30)),
                        "The forecast reprocess scheduled by the covariate-extension edit must finish.");
                }
                finally
                {
                    analysis.PropertyChanged -= onResults;
                }
            }

            Assert.IsTrue(analysis.IsEstimated, "A covariate-extension edit keeps the fit.");
            Assert.IsTrue(MessengerHas(analysis, "TSA-WRN-LEGACY-TRANSFORM"),
                "The results are unchanged by the edit, so the warning still stands.");
            Assert.IsTrue(analysis.UndoManager.CanUndo, "Precondition: the edit is undoable.");

            analysis.UndoManager.Undo();

            Assert.AreEqual(ARIMAX.CovariateExtensionMethod.BlockBootstrap, analysis.CovariateExtension,
                "Undo restores the covariate-extension method.");
            Assert.IsFalse(analysis.IsEstimated, "The rebuilt analysis has no results.");
            Assert.IsFalse(MessengerHas(analysis, "TSA-WRN-LEGACY-TRANSFORM"),
                "Once the legacy results are gone the warning must go with them.");
        }
        finally
        {
            DeleteFixtureFile(path);
        }
    }

    /// <summary>
    /// Verifies <see cref="UI.TimeSeriesAnalysis.Open(SQLiteManager)"/> starts from a clean
    /// legacy-results state: after an element that showed the pre-v2.0.1 warning is opened again
    /// from a row saved by the current version, its next save must not mark the results as legacy.
    /// </summary>
    /// <remarks>
    /// The current-version row has no <c>Covariates</c> column, so nothing clears the replaced
    /// analysis during that Open, and only the reset at the start of Open drops the earlier
    /// warning's state.
    /// </remarks>
    [STATestMethod]
    public void Open_AgainFromCurrentVersionRow_DoesNotCarryLegacyStateIntoNextSave()
    {
        string legacyPath = Path.Combine(Path.GetTempPath(), $"BestFit-TSAReopenLegacy-{Guid.NewGuid():N}.bestfit");
        string currentPath = Path.Combine(Path.GetTempPath(), $"BestFit-TSAReopenCurrent-{Guid.NewGuid():N}.bestfit");
        try
        {
            UI.TimeSeriesAnalysis analysis = BuildAndOpenLegacyTransformFixture(
                legacyPath, "ReopenResetTSA", includeCovariate: false, keepTransformLambda: false, isEstimated: true,
                differenceOrder: 1);
            Assert.IsTrue(MessengerHas(analysis, "TSA-WRN-LEGACY-TRANSFORM"), "Precondition: the legacy results warn on open.");

            BuildTimeSeriesAnalysisRow(currentPath, analysis.Name, analysis.TimeSeriesData.Name, covariateName: null,
                analysis.ARIMAX.ToXElement().ToString(), "<ARIMAXAnalysis IsEstimated=\"True\" />");
            using (var sqlite = new SQLiteManager(currentPath))
            {
                analysis.Open(sqlite);
            }
            Assert.IsTrue(analysis.IsEstimated, "Sanity check: the current-version row restores results.");
            Assert.IsFalse(MessengerHas(analysis, "TSA-WRN-LEGACY-TRANSFORM"), "A row saved by the current version does not warn.");

            analysis.Save();
            UI.TimeSeriesAnalysis reopened = ReopenAnalysis(analysis, legacyPath);

            Assert.IsTrue(reopened.IsEstimated, "Sanity check: the saved results are restored.");
            Assert.IsFalse(MessengerHas(reopened, "TSA-WRN-LEGACY-TRANSFORM"),
                "The earlier legacy state must not survive Open into the next save.");
        }
        finally
        {
            DeleteFixtureFile(legacyPath);
            DeleteFixtureFile(currentPath);
        }
    }

    /// <summary>
    /// Opens a new time-series analysis element from the saved row of <paramref name="saved"/>,
    /// the way reopening the project would, leaving the original instance untouched.
    /// </summary>
    /// <param name="saved">The analysis whose row was saved; supplies the name and parent collection.</param>
    /// <param name="path">The project file the row was saved to.</param>
    /// <returns>A new analysis instance opened from <paramref name="path"/>.</returns>
    private static UI.TimeSeriesAnalysis ReopenAnalysis(UI.TimeSeriesAnalysis saved, string path)
    {
        var reopened = new UI.TimeSeriesAnalysis(saved.Name, saved.ParentCollection);
        using (var sqlite = new SQLiteManager(path))
        {
            reopened.Open(sqlite);
        }

        return reopened;
    }

    /// <summary>
    /// Reads one cell of a saved time-series analysis row as text.
    /// </summary>
    /// <param name="path">The project file to read.</param>
    /// <param name="analysisName">The name of the saved analysis row.</param>
    /// <param name="columnName">The column to read.</param>
    /// <returns>The cell text, or <see langword="null"/> when the column or the row is absent.</returns>
    private static string? ReadSavedCell(string path, string analysisName, string columnName)
    {
        using (var sqlite = new SQLiteManager(path))
        {
            sqlite.Open();
            try
            {
                DataTableView view = sqlite.GetTableManager("Time Series Analysis");
                if (!view.ColumnNames.Contains(columnName)) return null;
                int rowIndex = view.SearchColumn(0, view.NumberOfRows - 1, "Name", analysisName, true, true);
                return rowIndex < 0 ? null : view.GetCell(columnName, rowIndex)?.ToString();
            }
            finally
            {
                sqlite.Close();
            }
        }
    }

    /// <summary>
    /// Returns the active messenger item for the supplied source and code.
    /// </summary>
    /// <param name="source">The expected owner of the message.</param>
    /// <param name="code">The stable message code to find.</param>
    /// <returns>The matching message item, or <c>null</c> when no matching message is active.</returns>
    private static BasicMessageItem? MessengerMessage(object source, string code)
    {
        return Messenger.GetInstance().AllMessageItems()
            .OfType<BasicMessageItem>()
            .FirstOrDefault(m => ReferenceEquals(m.Source, source) && m.Code == code);
    }

    /// <summary>
    /// Returns whether the global messenger contains a message from the supplied source and code.
    /// </summary>
    /// <param name="source">The expected owner of the message.</param>
    /// <param name="code">The stable message code to find.</param>
    /// <returns><c>true</c> when a matching message is active; otherwise, <c>false</c>.</returns>
    private static bool MessengerHas(object source, string code)
    {
        return MessengerMessage(source, code) != null;
    }

    /// <summary>
    /// Creates a test-local project instance without replacing the application's singleton.
    /// </summary>
    /// <param name="path">The temporary project path.</param>
    /// <returns>An isolated project instance targeting the temporary copy.</returns>
    private static BestFitProject CreateIsolatedProject(string path)
    {
        var project = (BestFitProject)Activator.CreateInstance(typeof(BestFitProject), nonPublic: true)!;
        project.FullFileName = path;
        return project;
    }

    /// <summary>
    /// Deletes a temporary fixture file, retrying briefly if a pooled SQLite connection has not
    /// yet released its file handle.
    /// </summary>
    /// <param name="path">The file to delete, if it exists.</param>
    /// <remarks>
    /// <see cref="BuildAndOpenLegacyTransformFixture"/> opens several short-lived SQLite
    /// connections against the same file in sequence (element saves via
    /// <see cref="TimeSeriesCollection.Add"/>, the analysis row builder, then
    /// <see cref="UI.TimeSeriesAnalysis.Open(SQLiteManager)"/>). On Windows the pooled native
    /// handle can outlive the last <c>Close()</c> by a short interval, so a single
    /// <see cref="File.Delete(string)"/> immediately afterward can race it; clearing the pool and
    /// retrying after a short pause resolves the race deterministically.
    /// </remarks>
    private static void DeleteFixtureFile(string path)
    {
        const int maxAttempts = 5;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                SQLiteConnection.ClearAllPools();
                if (File.Exists(path)) File.Delete(path);
                return;
            }
            catch (IOException) when (attempt < maxAttempts)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                Thread.Sleep(50);
            }
        }
    }

    /// <summary>
    /// Builds and opens a real time-series analysis for the Task 2.9 legacy-transform-warning
    /// tests, using an isolated <see cref="BestFitProject"/> (never the shared application
    /// singleton) so the input series referenced by name in the saved cells can be resolved by
    /// the real <see cref="UI.TimeSeriesAnalysis.Open(SQLiteManager)"/> lookup.
    /// </summary>
    /// <param name="path">The temporary <c>.bestfit</c> file backing the isolated project.</param>
    /// <param name="analysisName">The analysis name, unique per test.</param>
    /// <param name="includeCovariate">Whether to attach one covariate series to the model.</param>
    /// <param name="keepTransformLambda">
    /// When <see langword="false"/>, the <c>TransformLambda</c> attribute is stripped from the
    /// saved <c>ARIMAX</c> cell to simulate a save made before v2.0.1 started writing it.
    /// </param>
    /// <param name="isEstimated">The <c>IsEstimated</c> value written to the saved <c>AnalysisXml</c> cell.</param>
    /// <param name="differenceOrder">
    /// The saved model's <see cref="ARIMAX.DiffOrderD"/>. A positive order uses a response with
    /// varying first differences (the default response is linear, so its differences are constant
    /// and would leave the differenced scale defaults degenerate).
    /// </param>
    /// <returns>The freshly constructed analysis, already opened from the fixture.</returns>
    private static UI.TimeSeriesAnalysis BuildAndOpenLegacyTransformFixture(
        string path, string analysisName, bool includeCovariate, bool keepTransformLambda, bool isEstimated,
        int differenceOrder = 0)
    {
        BestFitProject project = CreateIsolatedProject(path);
        var tsCollection = (TimeSeriesCollection)project.ElementCollections!.OfType<TimeSeriesCollection>().Single();
        var tsaCollection = (TimeSeriesAnalysisCollection)project.ElementCollections!.OfType<TimeSeriesAnalysisCollection>().Single();

        var start = new DateTime(1990, 1, 1);
        Func<int, double>? responseValue = differenceOrder > 0 ? i => 1000.0 + 25.0 * Math.Sin(i / 3.0) + i : null;
        TimeSeriesElement response = CreateTimeSeriesElementInCollection(analysisName + "-Response", 30, TimeInterval.OneYear, start, tsCollection, responseValue);
        tsCollection.Add(response);

        var arimax = new ARIMAX(response.TimeSeries);
        if (differenceOrder > 0)
            arimax.DiffOrderD = differenceOrder;
        string covariateName = "";
        if (includeCovariate)
        {
            TimeSeriesElement covariate = CreateTimeSeriesElementInCollection(analysisName + "-Covariate", 30, TimeInterval.OneYear, start, tsCollection);
            tsCollection.Add(covariate);
            covariateName = covariate.Name;
            arimax.SetCovariates(new List<TimeSeries> { covariate.TimeSeries });
        }

        XElement modelXml = arimax.ToXElement();
        if (!keepTransformLambda)
            modelXml.Attribute(nameof(ARIMAX.TransformLambda))!.Remove();

        string analysisXml = $"<ARIMAXAnalysis IsEstimated=\"{isEstimated}\" />";
        BuildTimeSeriesAnalysisRow(path, analysisName, response.Name, covariateName, modelXml.ToString(), analysisXml);

        var analysis = new UI.TimeSeriesAnalysis(analysisName, tsaCollection);
        using (var sqlite = new SQLiteManager(path))
        {
            analysis.Open(sqlite);
        }

        return analysis;
    }

    /// <summary>
    /// Builds the "Time Series Analysis" table row for the Task 2.9 fixtures, hand-writing only
    /// the cells that matter for the legacy-transform-warning check (mirrors the relevant subset
    /// of the cells <see cref="UI.TimeSeriesAnalysis.Save"/> writes).
    /// </summary>
    /// <param name="path">The <c>.bestfit</c> file to create the table in.</param>
    /// <param name="analysisName">The analysis name written to the <c>Name</c> cell.</param>
    /// <param name="timeSeriesDataName">The response element name written to the <c>TimeSeriesData</c> cell.</param>
    /// <param name="covariateName">
    /// The covariate element name written to the <c>Covariates</c> cell (empty for none), or
    /// <see langword="null"/> to omit the <c>Covariates</c> column entirely.
    /// </param>
    /// <param name="arimaxXml">The model XML written to the <c>ARIMAX</c> cell.</param>
    /// <param name="analysisXml">The inner-analysis XML written to the <c>AnalysisXml</c> cell.</param>
    private static void BuildTimeSeriesAnalysisRow(string path, string analysisName, string timeSeriesDataName,
        string? covariateName, string arimaxXml, string analysisXml)
    {
        var anTable = new DataTable("Time Series Analysis");
        anTable.Columns.Add("Name", typeof(string));
        anTable.Columns.Add("Description", typeof(string));
        anTable.Columns.Add("CreationDate", typeof(string));
        anTable.Columns.Add("LastModified", typeof(string));
        anTable.Columns.Add("TimeSeriesData", typeof(string));
        if (covariateName != null)
            anTable.Columns.Add("Covariates", typeof(string));
        anTable.Columns.Add("ARIMAX", typeof(string));
        anTable.Columns.Add("AnalysisXml", typeof(string));

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
            anView.EditCell(0, "TimeSeriesData", timeSeriesDataName);
            if (covariateName != null)
                anView.EditCell(0, "Covariates", covariateName);
            anView.EditCell(0, "ARIMAX", arimaxXml);
            anView.EditCell(0, "AnalysisXml", analysisXml);
            anView.ApplyEdits();
            sqlite.Close();
        }
    }

    /// <summary>
    /// Builds a minimal SQLite <c>.bestfit</c> fixture with a corrupt <c>ARIMAX</c> XML cell.
    /// The referenced input series is intentionally absent.
    /// </summary>
    /// <param name="path">The <c>.bestfit</c> file to create the table in.</param>
    /// <param name="analysisName">The analysis name written to the <c>Name</c> cell.</param>
    /// <param name="corruptXml">The text written to the <c>ARIMAX</c> cell; callers pass malformed
    /// XML, or model XML whose analysis cannot resolve its input series.</param>
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
    /// <param name="path">The <c>.bestfit</c> file to create the table in.</param>
    /// <param name="analysisName">The analysis name written to the <c>Name</c> cell.</param>
    /// <param name="armaxXml">The pre-v2.0 model XML written to the <c>ARMAX</c> cell.</param>
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
    /// Verifies UI copy and model-snapshot undo/redo preserve the effective manual transform
    /// exponent without adding a public UI wrapper property.
    /// </summary>
    [STATestMethod]
    public void ManualTransformLambda_CopyUndoAndRedoPreserveEffectiveState()
    {
        var tsa = new UI.TimeSeriesAnalysis("LambdaLifecycleTSA", _collection!);
        tsa.ARIMAX.TransformType = RMC.BestFit.Models.Transform.YeoJohnson;
        tsa.ARIMAX.SetTransformParameters(0.4, double.NaN);
        tsa.TimeSeriesData = CreateTimeSeriesElement(
            "LambdaLifecycleSeries",
            20,
            TimeInterval.OneYear,
            new DateTime(2000, 1, 1));
        tsa.ARIMAX.UseDefaultTrainingSteps = false;
        tsa.ARIMAX.TrainingTimeSteps = 16;

        tsa.ARIMAX.SetTransformParameters(0.75, double.PositiveInfinity);
        Assert.AreEqual(0.75, tsa.ARIMAX.TransformLambda, 1E-12);
        Assert.IsTrue(tsa.UndoManager.CanUndo);

        tsa.UndoManager.Undo();
        Assert.AreEqual(0.4, tsa.ARIMAX.TransformLambda, 1E-12);

        tsa.UndoManager.Redo();
        Assert.AreEqual(0.75, tsa.ARIMAX.TransformLambda, 1E-12);

        var copy = (UI.TimeSeriesAnalysis)tsa.Copy("LambdaLifecycleTSA-Copy");
        Assert.AreEqual(0.75, copy.ARIMAX.TransformLambda, 1E-12);
        Assert.AreEqual("True", copy.ARIMAX.ToXElement().Attribute("TransformLambdaIsManual")?.Value);
    }

    /// <summary>
    /// Verifies model-snapshot undo and UI copy keep a covariate model's coefficient value,
    /// bounds, and custom prior instead of rebuilding defaults when the covariates are reattached.
    /// </summary>
    [STATestMethod]
    public void CovariateModel_UndoAndCopyPreserveCustomParameters()
    {
        var tsa = new UI.TimeSeriesAnalysis("CovariatePriorLifecycleTSA", _collection!);
        var start = new DateTime(1990, 1, 1);
        tsa.TimeSeriesData = CreateTimeSeriesElement("CovariatePriorResponse", 30, TimeInterval.OneYear, start);
        tsa.Covariates.Add(new CovariateData
        {
            TimeSeriesElement = CreateTimeSeriesElement("CovariatePriorCovariate", 30, TimeInterval.OneYear, start)
        });
        tsa.ARIMAX.UseDefaultFlatPriors = false;
        ModelParameter beta = GetCovariateCoefficient(tsa.ARIMAX);
        beta.Value = 0.42;
        beta.LowerBound = -3.0;
        beta.UpperBound = 3.0;
        // The prior change is the recorded model edit, so its snapshot also holds the value and bounds.
        beta.PriorDistribution = new global::Numerics.Distributions.Normal(0.5, 0.1);

        // Undoing a later recorded change replays the snapshot that holds the custom coefficient.
        tsa.ARIMAX.UseJeffreysRuleForScale = !tsa.ARIMAX.UseJeffreysRuleForScale;
        Assert.IsTrue(tsa.UndoManager.CanUndo);
        tsa.UndoManager.Undo();
        AssertCustomizedCovariateCoefficient(GetCovariateCoefficient(tsa.ARIMAX), "after undo");

        var copy = (UI.TimeSeriesAnalysis)tsa.Copy("CovariatePriorLifecycleTSA-Copy");
        AssertCustomizedCovariateCoefficient(GetCovariateCoefficient(copy.ARIMAX), "in the copy");
    }

    /// <summary>
    /// Verifies that undoing a model edit made after a covariate swap does not carry the replaced
    /// covariate's coefficient, bounds, and prior over to the new covariate.
    /// </summary>
    /// <remarks>
    /// The swap rebuilds the default parameters without recording a model-undo step, so the undo
    /// baseline must follow the rebuilt model; otherwise undo replays the pre-swap vector onto the
    /// new covariate, which has the same parameter count.
    /// </remarks>
    [STATestMethod]
    public void CovariateSwap_ThenUndoOfModelEdit_KeepsNewCovariateDefaults()
    {
        var tsa = new UI.TimeSeriesAnalysis("CovariateSwapUndoTSA", _collection!);
        var start = new DateTime(1990, 1, 1);
        tsa.TimeSeriesData = CreateTimeSeriesElement("CovariateSwapResponse", 30, TimeInterval.OneYear, start);
        tsa.Covariates.Add(new CovariateData
        {
            TimeSeriesElement = CreateTimeSeriesElement("CovariateSwapA", 30, TimeInterval.OneYear, start)
        });
        tsa.ARIMAX.UseDefaultFlatPriors = false;
        ModelParameter beta = GetCovariateCoefficient(tsa.ARIMAX);
        beta.Value = 0.42;
        beta.LowerBound = -3.0;
        beta.UpperBound = 3.0;
        beta.PriorDistribution = new global::Numerics.Distributions.Normal(0.5, 0.1);

        tsa.Covariates[0].TimeSeriesElement = CreateTimeSeriesElement("CovariateSwapC", 30, TimeInterval.OneYear, start);
        tsa.ARIMAX.UseJeffreysRuleForScale = !tsa.ARIMAX.UseJeffreysRuleForScale;
        tsa.UndoManager.Undo();

        ModelParameter restored = GetCovariateCoefficient(tsa.ARIMAX);
        Assert.IsInstanceOfType(restored.PriorDistribution, typeof(global::Numerics.Distributions.Uniform),
            "Undo must not attach the replaced covariate's custom prior to the new covariate.");
        Assert.AreEqual(0.0, restored.Value, 0.0, "The new covariate keeps its default coefficient.");
    }

    /// <summary>
    /// Verifies that undoing and redoing a prior edit recorded before a covariate swap does not
    /// apply the replaced covariate's coefficient, bounds, and prior to the new covariate.
    /// </summary>
    /// <remarks>
    /// The swap itself is not an undo step, so steps recorded against the previous covariate stay
    /// on the stacks; their snapshots must only be reapplied to the covariates they were taken with.
    /// </remarks>
    [STATestMethod]
    public void CovariateSwap_ThenUndoAndRedoOfEarlierPriorEdit_KeepsNewCovariateDefaults()
    {
        var tsa = CreateAnalysisWithCustomizedCovariate("SwapRedoTSA", out _);
        tsa.Covariates[0].TimeSeriesElement = CreateTimeSeriesElement("SwapRedoTSA-C", 30, TimeInterval.OneYear, new DateTime(1990, 1, 1));

        tsa.UndoManager.Undo();
        tsa.UndoManager.Redo();

        Assert.IsInstanceOfType(GetCovariateCoefficient(tsa.ARIMAX).PriorDistribution, typeof(global::Numerics.Distributions.Uniform),
            "Redo must not apply the replaced covariate's custom prior to the new covariate.");
    }

    /// <summary>
    /// Verifies that undoing a model edit recorded before a covariate swap restores the edited
    /// setting without applying the replaced covariate's prior to the new covariate.
    /// </summary>
    [STATestMethod]
    public void CovariateSwap_ThenUndoOfEditRecordedBeforeSwap_KeepsNewCovariateDefaults()
    {
        var tsa = CreateAnalysisWithCustomizedCovariate("SwapUndoEarlierTSA", out _);
        bool originalJeffreys = tsa.ARIMAX.UseJeffreysRuleForScale;
        tsa.ARIMAX.UseJeffreysRuleForScale = !originalJeffreys;
        tsa.Covariates[0].TimeSeriesElement = CreateTimeSeriesElement("SwapUndoEarlierTSA-C", 30, TimeInterval.OneYear, new DateTime(1990, 1, 1));

        tsa.UndoManager.Undo();

        Assert.AreEqual(originalJeffreys, tsa.ARIMAX.UseJeffreysRuleForScale, "Undo restores the edited setting.");
        Assert.IsInstanceOfType(GetCovariateCoefficient(tsa.ARIMAX).PriorDistribution, typeof(global::Numerics.Distributions.Uniform),
            "Undo must not apply the replaced covariate's custom prior to the new covariate.");
    }

    /// <summary>
    /// Verifies that model-snapshot undo and redo leave only the current model subscribed to the
    /// live response and covariate series.
    /// </summary>
    [STATestMethod]
    public void ModelUndoAndRedo_LeaveOnlyTheCurrentModelSubscribedToLiveSeries()
    {
        var tsa = CreateAnalysisWithCustomizedCovariate("UndoSubscriptionTSA", out TimeSeriesElement covariate);
        TimeSeries response = tsa.TimeSeriesData.TimeSeries;
        tsa.ARIMAX.UseJeffreysRuleForScale = !tsa.ARIMAX.UseJeffreysRuleForScale;

        tsa.UndoManager.Undo();
        tsa.UndoManager.Redo();

        Assert.AreEqual(1, CountModelSubscribers(response), "Only the current model may stay on the response series.");
        Assert.AreEqual(1, CountModelSubscribers(covariate.TimeSeries), "Only the current model may stay on the covariate series.");
    }

    /// <summary>
    /// Verifies that metadata edits on a covariate series do not rebuild the model's parameters.
    /// </summary>
    /// <remarks>
    /// <see cref="CovariateData"/> forwards every property change of its series element; a
    /// description or unit-label edit is not a covariate change and must keep the fitted model.
    /// </remarks>
    [STATestMethod]
    public void CovariateSeriesMetadataEdit_KeepsCustomParameters()
    {
        var tsa = CreateAnalysisWithCustomizedCovariate("CovariateMetadataTSA", out TimeSeriesElement covariate);

        covariate.Description = "Edited covariate description";
        covariate.UnitLabel = "Edited unit";

        AssertCustomizedCovariateCoefficient(GetCovariateCoefficient(tsa.ARIMAX), "after a covariate metadata edit");
    }

    /// <summary>
    /// Verifies that assigning a covariate row the series it already holds keeps the model's
    /// parameters, as a data-binding write-back of an unchanged selection does.
    /// </summary>
    [STATestMethod]
    public void CovariateRowReassignedToSameSeries_KeepsCustomParameters()
    {
        var tsa = CreateAnalysisWithCustomizedCovariate("CovariateSameSeriesTSA", out TimeSeriesElement covariate);

        tsa.Covariates[0].TimeSeriesElement = covariate;

        AssertCustomizedCovariateCoefficient(GetCovariateCoefficient(tsa.ARIMAX), "after reassigning the same series");
    }

    /// <summary>
    /// Verifies that replacing a covariate element's series is still a covariate change that
    /// rebuilds the default coefficient.
    /// </summary>
    [STATestMethod]
    public void CovariateSeriesReplaced_RebuildsDefaultCoefficient()
    {
        var tsa = CreateAnalysisWithCustomizedCovariate("CovariateReplacedTSA", out TimeSeriesElement covariate);

        covariate.TimeSeries = CreateTimeSeriesElement("CovariateReplacementSource", 30, TimeInterval.OneYear, new DateTime(1990, 1, 1)).TimeSeries;

        ModelParameter beta = GetCovariateCoefficient(tsa.ARIMAX);
        Assert.AreEqual(0.0, beta.Value, 0.0);
        Assert.IsInstanceOfType(beta.PriorDistribution, typeof(global::Numerics.Distributions.Uniform));
    }

    /// <summary>
    /// Verifies that copying an analysis carries its model unchanged and leaves only the copy's
    /// model subscribed to the live response and covariate series.
    /// </summary>
    /// <remarks>
    /// Copy first attaches the inputs to the new element's default model and then replaces that
    /// model; a replaced model that stayed subscribed would keep reacting to every later data edit.
    /// </remarks>
    [STATestMethod]
    public void Copy_KeepsModelAndSubscribesOnlyTheCopiedModelToLiveSeries()
    {
        var tsa = CreateAnalysisWithCustomizedCovariate("CopySubscriptionTSA", out TimeSeriesElement covariate);
        tsa.ARIMAX.UseDefaultTrainingSteps = false;
        tsa.ARIMAX.TrainingTimeSteps = 24;
        tsa.ARIMAX.CovariateExtension = ARIMAX.CovariateExtensionMethod.None;
        TimeSeries response = tsa.TimeSeriesData.TimeSeries;
        int responseModels = CountModelSubscribers(response);
        int covariateModels = CountModelSubscribers(covariate.TimeSeries);

        var copy = (UI.TimeSeriesAnalysis)tsa.Copy("CopySubscriptionTSA-Copy");

        Assert.IsFalse(copy.ARIMAX.UseDefaultTrainingSteps, "The copy keeps the manual training window.");
        Assert.AreEqual(24, copy.ARIMAX.TrainingTimeSteps);
        Assert.AreEqual(ARIMAX.CovariateExtensionMethod.None, copy.ARIMAX.CovariateExtension);
        AssertCustomizedCovariateCoefficient(GetCovariateCoefficient(copy.ARIMAX), "in the copy");
        Assert.IsTrue(System.Xml.Linq.XNode.DeepEquals(tsa.ARIMAX.ToXElement(), copy.ARIMAX.ToXElement()),
            "The copy must carry the source model unchanged.");
        Assert.AreEqual(responseModels + 1, CountModelSubscribers(response),
            "Only the copy's model may join the response series.");
        Assert.AreEqual(covariateModels + 1, CountModelSubscribers(covariate.TimeSeries),
            "Only the copy's model may join the covariate series.");
    }

    /// <summary>
    /// Counts the ARIMAX models subscribed to a series' collection-change event.
    /// </summary>
    /// <param name="series">The series whose subscribers are counted.</param>
    /// <returns>The number of handlers whose target is an <see cref="ARIMAX"/> model.</returns>
    private static int CountModelSubscribers(TimeSeries series)
    {
        return ModelSubscriptionCounter.Count(series);
    }

    /// <summary>
    /// Creates a time-series analysis with one covariate whose coefficient carries the value 0.42,
    /// bounds [-3, 3], and a Normal(0.5, 0.1) prior.
    /// </summary>
    /// <param name="name">The analysis name, unique per test.</param>
    /// <param name="covariate">Receives the covariate series element.</param>
    /// <returns>The configured analysis, with default flat priors disabled.</returns>
    private static UI.TimeSeriesAnalysis CreateAnalysisWithCustomizedCovariate(string name, out TimeSeriesElement covariate)
    {
        var tsa = new UI.TimeSeriesAnalysis(name, _collection!);
        var start = new DateTime(1990, 1, 1);
        tsa.TimeSeriesData = CreateTimeSeriesElement(name + "-Response", 30, TimeInterval.OneYear, start);
        covariate = CreateTimeSeriesElement(name + "-Covariate", 30, TimeInterval.OneYear, start);
        tsa.Covariates.Add(new CovariateData { TimeSeriesElement = covariate });
        tsa.ARIMAX.UseDefaultFlatPriors = false;
        ModelParameter beta = GetCovariateCoefficient(tsa.ARIMAX);
        beta.Value = 0.42;
        beta.LowerBound = -3.0;
        beta.UpperBound = 3.0;
        beta.PriorDistribution = new global::Numerics.Distributions.Normal(0.5, 0.1);
        return tsa;
    }

    /// <summary>
    /// Verifies that redoing an AR-order change restores a parameter vector that fits the new
    /// model structure.
    /// </summary>
    /// <remarks>
    /// The order setter rebuilds the parameters before it notifies, so the recorded redo snapshot
    /// holds the new order with a vector that fits it; the restore's layout guard remains a
    /// backstop for a snapshot whose vector does not fit its structure.
    /// </remarks>
    [STATestMethod]
    public void AROrderChange_UndoThenRedo_RestoresConsistentParameterLayout()
    {
        var tsa = new UI.TimeSeriesAnalysis("AROrderRedoTSA", _collection!);
        tsa.TimeSeriesData = CreateTimeSeriesElement("AROrderRedoResponse", 30, TimeInterval.OneYear, new DateTime(1990, 1, 1));
        Assert.AreEqual(1, tsa.ARIMAX.AROrderP);
        tsa.ARIMAX.AROrderP = 2;
        Assert.AreEqual(4, tsa.ARIMAX.NumberOfParameters, "Intercept, two AR coefficients, and the scale.");

        tsa.UndoManager.Undo();
        Assert.AreEqual(3, tsa.ARIMAX.NumberOfParameters, "Intercept, one AR coefficient, and the scale.");
        tsa.UndoManager.Redo();

        Assert.AreEqual(2, tsa.ARIMAX.AROrderP);
        Assert.AreEqual(4, tsa.ARIMAX.NumberOfParameters, "Redo must restore a vector that fits AR(2).");
    }

    /// <summary>
    /// Verifies that undoing an edit made after a differencing-order change returns to the
    /// differenced model with the intercept bounds built for it.
    /// </summary>
    /// <remarks>
    /// The undo step of the later edit restores the state recorded when the differencing order
    /// changed. That state must hold the defaults rebuilt for d = 1, not the level model's
    /// intercept bounds carried over with the new order.
    /// </remarks>
    [STATestMethod]
    public void DiffOrderChange_ThenUndoOfLaterEdit_KeepsDifferencedInterceptBounds()
    {
        var tsa = new UI.TimeSeriesAnalysis("DiffOrderLaterUndoTSA", _collection!);
        tsa.TimeSeriesData = CreateLevelTimeSeriesElement("DiffOrderLaterUndoResponse");
        (double Lower, double Upper) levelBounds = GetInterceptBounds(tsa.ARIMAX);
        tsa.ARIMAX.DiffOrderD = 1;
        (double Lower, double Upper) differencedBounds = GetInterceptBounds(tsa.ARIMAX);
        Assert.AreNotEqual(levelBounds, differencedBounds, "Precondition: differencing changes the default intercept bounds.");

        tsa.ARIMAX.UseJeffreysRuleForScale = !tsa.ARIMAX.UseJeffreysRuleForScale;
        Assert.IsTrue(tsa.UndoManager.CanUndo);
        tsa.UndoManager.Undo();

        Assert.AreEqual(1, tsa.ARIMAX.DiffOrderD, "Undo of the later edit keeps the differencing order.");
        Assert.AreEqual(differencedBounds, GetInterceptBounds(tsa.ARIMAX),
            "Undo must restore the intercept bounds built for d = 1.");
    }

    /// <summary>
    /// Verifies that undoing and redoing a differencing-order change restores the intercept
    /// bounds built for each order.
    /// </summary>
    /// <remarks>
    /// The redo step restores the state recorded when the order changed, which must hold the
    /// defaults rebuilt for d = 1.
    /// </remarks>
    [STATestMethod]
    public void DiffOrderChange_UndoThenRedo_RestoresDifferencedInterceptBounds()
    {
        var tsa = new UI.TimeSeriesAnalysis("DiffOrderRedoTSA", _collection!);
        tsa.TimeSeriesData = CreateLevelTimeSeriesElement("DiffOrderRedoResponse");
        (double Lower, double Upper) levelBounds = GetInterceptBounds(tsa.ARIMAX);
        tsa.ARIMAX.DiffOrderD = 1;
        (double Lower, double Upper) differencedBounds = GetInterceptBounds(tsa.ARIMAX);
        Assert.AreNotEqual(levelBounds, differencedBounds, "Precondition: differencing changes the default intercept bounds.");

        tsa.UndoManager.Undo();
        Assert.AreEqual(0, tsa.ARIMAX.DiffOrderD);
        Assert.AreEqual(levelBounds, GetInterceptBounds(tsa.ARIMAX), "Undo must restore the intercept bounds built for d = 0.");
        tsa.UndoManager.Redo();

        Assert.AreEqual(1, tsa.ARIMAX.DiffOrderD);
        Assert.AreEqual(differencedBounds, GetInterceptBounds(tsa.ARIMAX),
            "Redo must restore the intercept bounds built for d = 1.");
    }

    /// <summary>
    /// Verifies that turning default flat priors on is an undoable step: undo restores the flag
    /// and the custom covariate prior, and redo restores the rebuilt default prior.
    /// </summary>
    /// <remarks>
    /// The redo step restores the state recorded on the flag's notification, so the defaults must
    /// already be rebuilt when the flag notifies; otherwise redo turns the flag on while keeping
    /// the custom prior.
    /// </remarks>
    [STATestMethod]
    public void DefaultFlatPriorsTurnedOn_UndoRestoresCustomPriorAndRedoRestoresDefaults()
    {
        var tsa = CreateAnalysisWithCustomizedCovariate("FlatPriorUndoTSA", out _);
        tsa.ARIMAX.UseDefaultFlatPriors = true;
        Assert.IsInstanceOfType(GetCovariateCoefficient(tsa.ARIMAX).PriorDistribution, typeof(global::Numerics.Distributions.Uniform),
            "Precondition: turning default flat priors on rebuilds the default prior.");

        tsa.UndoManager.Undo();
        Assert.IsFalse(tsa.ARIMAX.UseDefaultFlatPriors, "Undo must turn default flat priors back off.");
        AssertCustomizedCovariateCoefficient(GetCovariateCoefficient(tsa.ARIMAX), "undoing the flat-prior toggle");

        tsa.UndoManager.Redo();
        Assert.IsTrue(tsa.ARIMAX.UseDefaultFlatPriors, "Redo must turn default flat priors back on.");
        ModelParameter redone = GetCovariateCoefficient(tsa.ARIMAX);
        Assert.IsInstanceOfType(redone.PriorDistribution, typeof(global::Numerics.Distributions.Uniform),
            "Redo must restore the rebuilt default prior, not the custom prior.");
        Assert.AreEqual(0.0, redone.Value, 0.0, "Redo must restore the default coefficient value.");
    }

    /// <summary>
    /// Creates a 40-step annual time-series element whose level is near 1000 with visible
    /// variation (value = 1000 + 25·sin(i/3) + i).
    /// </summary>
    /// <param name="name">The element name.</param>
    /// <returns>A populated time-series element starting in 1980.</returns>
    /// <remarks>
    /// The level series and its first differences have very different means, so the default
    /// intercept bounds built for d = 0 and d = 1 differ.
    /// </remarks>
    private static TimeSeriesElement CreateLevelTimeSeriesElement(string name)
    {
        var element = new TimeSeriesElement(name);
        var series = new TimeSeries(TimeInterval.OneYear);
        DateTime date = new DateTime(1980, 1, 1);
        for (int i = 0; i < 40; i++)
        {
            series.Add(new SeriesOrdinate<DateTime, double>(date, 1000.0 + 25.0 * Math.Sin(i / 3.0) + i));
            date = TimeSeries.AddTimeInterval(date, TimeInterval.OneYear);
        }

        element.TimeSeries = series;
        return element;
    }

    /// <summary>
    /// Returns the lower and upper bounds of a model's intercept parameter.
    /// </summary>
    /// <param name="model">A model that includes an intercept.</param>
    /// <returns>The intercept's lower and upper bounds.</returns>
    private static (double Lower, double Upper) GetInterceptBounds(ARIMAX model)
    {
        ModelParameter intercept = model.Parameters.Single(p => p.Name.StartsWith("Intercept", StringComparison.Ordinal));
        return (intercept.LowerBound, intercept.UpperBound);
    }

    /// <summary>
    /// Returns the covariate coefficient of a model with exactly one zero-lag covariate.
    /// </summary>
    /// <param name="model">The model whose parameter list is searched.</param>
    /// <returns>The single covariate coefficient parameter.</returns>
    private static ModelParameter GetCovariateCoefficient(ARIMAX model)
    {
        return model.Parameters.Single(p => p.Name.StartsWith("Covariate", StringComparison.Ordinal));
    }

    /// <summary>
    /// Asserts that a covariate coefficient still carries the value 0.42, bounds [-3, 3], and
    /// Normal(0.5, 0.1) prior set by <see cref="CovariateModel_UndoAndCopyPreserveCustomParameters"/>.
    /// </summary>
    /// <param name="beta">The covariate coefficient to check.</param>
    /// <param name="context">Where the coefficient was read, for the failure message.</param>
    private static void AssertCustomizedCovariateCoefficient(ModelParameter beta, string context)
    {
        Assert.AreEqual(0.42, beta.Value, 0.0, $"The coefficient value must survive {context}.");
        Assert.AreEqual(-3.0, beta.LowerBound, 0.0, $"The lower bound must survive {context}.");
        Assert.AreEqual(3.0, beta.UpperBound, 0.0, $"The upper bound must survive {context}.");
        var prior = beta.PriorDistribution as global::Numerics.Distributions.Normal;
        Assert.IsNotNull(prior, $"The custom Normal prior must survive {context}.");
        Assert.AreEqual(0.5, prior.Mu, 0.0);
        Assert.AreEqual(0.1, prior.Sigma, 0.0);
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
