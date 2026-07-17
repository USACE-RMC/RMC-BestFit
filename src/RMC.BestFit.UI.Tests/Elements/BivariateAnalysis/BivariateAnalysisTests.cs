using DatabaseManager;
using FrameworkInterfaces;
using FrameworkInterfaces.Messaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Numerics.Data;
using Numerics.Distributions;
using Numerics.Distributions.Copulas;
using Numerics.Mathematics.Optimization;
using Numerics.Sampling.MCMC;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using RMC.BestFit.UI;
using System.Data;
using System.IO;
using System.Reflection;
using System.Xml.Linq;
using ModelAnalyses = RMC.BestFit.Analyses;

namespace RMC.BestFit.UI.Tests.Elements.BivariateAnalysis;

/// <summary>
/// Unit tests for <see cref="UI.BivariateAnalysis"/>, the UI wrapper for bivariate distribution analysis.
/// </summary>
/// <remarks>
/// All tests require STA thread because the constructor creates OxyPlot WPF Plot objects.
/// Tests focus on: constructor defaults, property change notifications, IsEstimated flag,
/// MarginalX/MarginalY, and plot initialization.
/// Save/Open/Delete are excluded (require SQLite project file).
/// </remarks>
[TestClass]
public class BivariateAnalysisTests
{
    private static BivariateAnalysisCollection? _collection;
    private static UnivariateAnalysisCollection? _univariateCollection;
    private static InputDataCollection? _inputDataCollection;

    /// <summary>
    /// Creates shared collections backed by the singleton BestFitProject.
    /// </summary>
    [ClassInitialize]
    public static void ClassInitialize(TestContext _)
    {
        var project = BestFitProject.GetInstance();
        _collection = new BivariateAnalysisCollection(project);
        _univariateCollection = new UnivariateAnalysisCollection(project);
        _inputDataCollection = new InputDataCollection(project);
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
    /// Creates an estimated UI univariate analysis backed by exact data at the supplied indexes.
    /// </summary>
    /// <param name="name">The analysis name.</param>
    /// <param name="indexes">The exact-data indexes to assign to the fixture rows.</param>
    /// <param name="lowOutlierIndexes">Indexes to flag as low outliers.</param>
    /// <returns>A valid univariate analysis whose marginal model can feed a bivariate analysis.</returns>
    private static UI.UnivariateAnalysis CreateEstimatedNormalMarginal(
        string name,
        IReadOnlyList<int> indexes,
        ISet<int>? lowOutlierIndexes = null)
    {
        var inputData = new UI.InputData($"{name}Data", _inputDataCollection!);
        var dataFrame = new DataFrame();
        for (int i = 0; i < indexes.Count; i++)
        {
            bool isLowOutlier = lowOutlierIndexes?.Contains(indexes[i]) == true;
            dataFrame.ExactSeries.Add(new ExactData(indexes[i], 100.0 + i, isLowOutlier: isLowOutlier));
        }

        dataFrame.CalculatePlottingPositions();
        inputData.DataFrame = dataFrame;

        var analysis = new UI.UnivariateAnalysis(name, _univariateCollection!);
        analysis.InputData = inputData;
        analysis.UnivariateDistribution.DistributionType = UnivariateDistributionType.Normal;
        analysis.UnivariateDistribution.SetParameterValues([100.0, 10.0]);
        MarkEstimated(analysis);

        return analysis;
    }

    /// <summary>
    /// Marks a univariate analysis as estimated without running an estimator.
    /// </summary>
    /// <param name="analysis">The analysis whose model-layer estimated flag should be set.</param>
    private static void MarkEstimated(UI.UnivariateAnalysis analysis)
    {
        var isEstimatedField = typeof(ModelAnalyses.AnalysisBase).GetField(
            "_isEstimated",
            BindingFlags.Instance | BindingFlags.NonPublic);
        isEstimatedField!.SetValue(analysis.InnerAnalysis, true);
    }

    /// <summary>
    /// Verifies that the constructor succeeds and stores the name.
    /// </summary>
    [STATestMethod]
    public void Constructor_StoresName()
    {
        var ba = new UI.BivariateAnalysis("TestBA", _collection!);

        Assert.AreEqual("TestBA", ba.Name);
    }

    /// <summary>
    /// Verifies that <see cref="UI.BivariateAnalysis.NameOnDisk"/> matches the constructor name.
    /// </summary>
    [STATestMethod]
    public void Constructor_NameOnDiskMatchesName()
    {
        var ba = new UI.BivariateAnalysis("DiskBA", _collection!);

        Assert.AreEqual("DiskBA", ba.NameOnDisk);
    }

    /// <summary>
    /// Verifies that <see cref="UI.BivariateAnalysis.IsEstimated"/> is <c>false</c> after construction.
    /// </summary>
    [STATestMethod]
    public void Constructor_IsEstimated_IsFalseInitially()
    {
        var ba = new UI.BivariateAnalysis("EstBA", _collection!);

        Assert.IsFalse(ba.IsEstimated);
    }

    /// <summary>
    /// Verifies that <see cref="UI.BivariateAnalysis.MarginalX"/> is <c>null</c> after construction.
    /// </summary>
    [STATestMethod]
    public void Constructor_MarginalX_IsNullInitially()
    {
        var ba = new UI.BivariateAnalysis("MargXBA", _collection!);

        Assert.IsNull(ba.MarginalX);
    }

    /// <summary>
    /// Verifies that <see cref="UI.BivariateAnalysis.MarginalY"/> is <c>null</c> after construction.
    /// </summary>
    [STATestMethod]
    public void Constructor_MarginalY_IsNullInitially()
    {
        var ba = new UI.BivariateAnalysis("MargYBA", _collection!);

        Assert.IsNull(ba.MarginalY);
    }

    /// <summary>
    /// Verifies that <see cref="UI.BivariateAnalysis.BivariateDistribution"/> is not null after construction.
    /// </summary>
    [STATestMethod]
    public void Constructor_BivariateDistribution_IsNotNull()
    {
        var ba = new UI.BivariateAnalysis("DistBA", _collection!);

        Assert.IsNotNull(ba.BivariateDistribution);
    }

    /// <summary>
    /// Verifies that <see cref="UI.BivariateAnalysis.BayesianAnalysis"/> is not null after construction.
    /// </summary>
    [STATestMethod]
    public void Constructor_BayesianAnalysis_IsNotNull()
    {
        var ba = new UI.BivariateAnalysis("BaBA", _collection!);

        Assert.IsNotNull(ba.BayesianAnalysis);
    }

    /// <summary>
    /// Verifies that <see cref="UI.BivariateAnalysis.CanCopyFromExternal"/> returns <c>false</c>.
    /// </summary>
    [STATestMethod]
    public void CanCopyFromExternal_ReturnsFalse()
    {
        var ba = new UI.BivariateAnalysis("CopyBA", _collection!);

        Assert.IsFalse(ba.CanCopyFromExternal);
    }

    /// <summary>
    /// Verifies that the Description setter raises PropertyChanged.
    /// </summary>
    [STATestMethod]
    public void Description_Setter_RaisesPropertyChanged()
    {
        var ba = new UI.BivariateAnalysis("PropBA", _collection!);
        var raised = new List<string>();
        ba.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        ba.Description = "New Description";

        Assert.IsTrue(raised.Contains(nameof(UI.BivariateAnalysis.Description)));
    }

    /// <summary>
    /// Verifies that setting Description to the same value does NOT raise PropertyChanged.
    /// </summary>
    [STATestMethod]
    public void Description_SameValue_DoesNotRaisePropertyChanged()
    {
        var ba = new UI.BivariateAnalysis("PropBA2", _collection!);
        ba.Description = "Same";

        var raised = new List<string>();
        ba.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        ba.Description = "Same";

        CollectionAssert.DoesNotContain(raised, nameof(UI.BivariateAnalysis.Description));
    }

    /// <summary>
    /// Verifies that <see cref="UI.BivariateAnalysis.CreationDate"/> and <see cref="UI.BivariateAnalysis.LastModified"/>
    /// are within a few seconds of construction time.
    /// </summary>
    [STATestMethod]
    public void Constructor_DatesAreRecent()
    {
        var before = DateTime.Now.AddSeconds(-5);
        var ba = new UI.BivariateAnalysis("DateBA", _collection!);
        var after = DateTime.Now.AddSeconds(5);

        Assert.IsTrue(ba.CreationDate >= before && ba.CreationDate <= after);
        Assert.IsTrue(ba.LastModified >= before && ba.LastModified <= after);
    }

    /// <summary>
    /// Verifies that setting MarginalX to a value fires PropertyChanged.
    /// </summary>
    [STATestMethod]
    public void MarginalX_Setter_RaisesPropertyChanged()
    {
        var ba = new UI.BivariateAnalysis("MargXPropBA", _collection!);
        var ua = new UI.UnivariateAnalysis("UAmargX", _univariateCollection!);

        var raised = new List<string>();
        ba.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        ba.MarginalX = ua;

        Assert.IsTrue(raised.Contains(nameof(UI.BivariateAnalysis.MarginalX)));
    }

    /// <summary>
    /// Verifies that setting MarginalY to a value fires PropertyChanged.
    /// </summary>
    [STATestMethod]
    public void MarginalY_Setter_RaisesPropertyChanged()
    {
        var ba = new UI.BivariateAnalysis("MargYPropBA", _collection!);
        var ua = new UI.UnivariateAnalysis("UAmargY", _univariateCollection!);

        var raised = new List<string>();
        ba.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        ba.MarginalY = ua;

        Assert.IsTrue(raised.Contains(nameof(UI.BivariateAnalysis.MarginalY)));
    }

    /// <summary>
    /// Partial exact-data index overlap raises the advisory warning without blocking the fit.
    /// </summary>
    [STATestMethod]
    public void Validate_PartialMarginalOverlap_AddsWarning_NoOverlapError()
    {
        var ba = new UI.BivariateAnalysis("PartialOverlapBA", _collection!);
        ba.Description = "Configured bivariate analysis.";

        ba.MarginalX = CreateEstimatedNormalMarginal(
            "PartialOverlapX",
            Enumerable.Range(0, 100).ToArray());
        ba.MarginalY = CreateEstimatedNormalMarginal(
            "PartialOverlapY",
            Enumerable.Range(60, 100).ToArray());

        var warning = MessengerMessage(ba, "BDA-WRN-012");

        Assert.IsNotNull(warning, "Partial exact-data overlap should raise BDA-WRN-012.");
        Assert.IsFalse(MessengerHas(ba, "BDA-ERR-010"),
            "40 paired observations is above the 10-pair minimum, so no overlap error should be raised.");
        Assert.IsTrue(ba.IsValid, "Partial overlap warning must not make the bivariate analysis invalid.");
        StringAssert.Contains(warning!.Description, "40 paired observations");
        StringAssert.Contains(warning.Description, "100 X exact");
        StringAssert.Contains(warning.Description, "100 Y exact");
    }

    /// <summary>
    /// Perfect exact-data index overlap raises no partial-overlap warning.
    /// </summary>
    [STATestMethod]
    public void Validate_ExactMarginalOverlap_NoPartialOverlapWarning()
    {
        var ba = new UI.BivariateAnalysis("ExactOverlapBA", _collection!);
        ba.Description = "Configured bivariate analysis.";
        var indexes = Enumerable.Range(0, 100).ToArray();

        ba.MarginalX = CreateEstimatedNormalMarginal("ExactOverlapX", indexes);
        ba.MarginalY = CreateEstimatedNormalMarginal("ExactOverlapY", indexes);

        Assert.IsFalse(MessengerHas(ba, "BDA-WRN-012"),
            "Perfect exact-data index overlap should not raise a partial-overlap warning.");
        Assert.IsFalse(MessengerHas(ba, "BDA-ERR-010"),
            "Perfect exact-data index overlap should not raise the insufficient-overlap error.");
        Assert.IsTrue(ba.IsValid, "Exact overlap with valid marginals should leave the analysis valid.");
    }

    /// <summary>
    /// Verifies that <see cref="UI.BivariateAnalysis.CancelAnalysis"/> does not throw
    /// when no analysis is running.
    /// </summary>
    [STATestMethod]
    public void CancelAnalysis_WhenNotRunning_DoesNotThrow()
    {
        var ba = new UI.BivariateAnalysis("CancelBA", _collection!);

        // Must not throw
        ba.CancelAnalysis();
    }

    /// <summary>
    /// Verifies that <see cref="UI.BivariateAnalysis.ClearResults"/> does not throw
    /// when no analysis has been performed.
    /// </summary>
    [STATestMethod]
    public void ClearResults_WhenNotEstimated_DoesNotThrow()
    {
        var ba = new UI.BivariateAnalysis("ClearBA", _collection!);

        // Must not throw
        ba.ClearResults();

        Assert.IsFalse(ba.IsEstimated);
    }

    /// <summary>
    /// Verifies that switching the inner BivariateDistribution to the Student's t copula
    /// does not throw from the UI wrapper. The wrapper does not hardcode a parameter count,
    /// so a 2-parameter copula should round-trip through it cleanly.
    /// </summary>
    [STATestMethod]
    public void CopulaType_SwitchToStudentT_DoesNotThrow()
    {
        var ba = new UI.BivariateAnalysis("StudentTBA", _collection!);

        ba.BivariateDistribution.CopulaType = Numerics.Distributions.Copulas.CopulaType.StudentT;

        Assert.AreEqual(Numerics.Distributions.Copulas.CopulaType.StudentT,
            ba.BivariateDistribution.CopulaType);
    }

    /// <summary>
    /// Regression test: a SQLite project whose <c>BivariateDistribution</c> cell contains
    /// invalid XML must not cause <see cref="UI.BivariateAnalysis.Open(SQLiteManager)"/> to throw.
    /// The parse is guarded with try/catch so an unparseable cell falls back to default state
    /// instead of aborting Open() partway.
    /// </summary>
    [STATestMethod]
    public void Open_WithCorruptBivariateDistributionXml_DoesNotThrow()
    {
        string tempPath = Path.Combine(Path.GetTempPath(), $"rmcbf-ba-test-{Guid.NewGuid():N}.bestfit");
        try
        {
            BuildBivariateAnalysisDatabaseWithCorruptXml(tempPath, "CorruptBA", "<not-a-valid-xml>");

            var analysis = new UI.BivariateAnalysis("CorruptBA", _collection!);

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
    /// Verifies that <see cref="UI.BivariateAnalysis.InnerAnalysis"/> is not null after construction.
    /// </summary>
    [STATestMethod]
    public void InnerAnalysis_IsNotNull()
    {
        var ba = new UI.BivariateAnalysis("InnerBA", _collection!);
        Assert.IsNotNull(ba.InnerAnalysis);
    }

    /// <summary>
    /// Verifies that <see cref="UI.BivariateAnalysis.AnalysisResults"/> is null until the analysis runs.
    /// </summary>
    [STATestMethod]
    public void AnalysisResults_BeforeRun_IsNull()
    {
        var ba = new UI.BivariateAnalysis("ResBA", _collection!);
        Assert.IsNull(ba.AnalysisResults);
    }

    /// <summary>
    /// Verifies that <see cref="UI.BivariateAnalysis.CopulaPlot"/> is not null after construction.
    /// </summary>
    [STATestMethod]
    public void CopulaPlot_IsNotNull()
    {
        var ba = new UI.BivariateAnalysis("CopulaPlotBA", _collection!);
        Assert.IsNotNull(ba.CopulaPlot);
    }

    /// <summary>
    /// Verifies that <see cref="UI.BivariateAnalysis.BayesianPlots"/> is not null.
    /// </summary>
    [STATestMethod]
    public void BayesianPlots_IsNotNull()
    {
        var ba = new UI.BivariateAnalysis("BPBA", _collection!);
        Assert.IsNotNull(ba.BayesianPlots);
    }

    /// <summary>
    /// Verifies that <see cref="UI.BivariateAnalysis.RaisePreviewSaved"/> does not throw or flip
    /// cancel when there are no PreviewObjectSaved subscribers.
    /// </summary>
    [STATestMethod]
    public void RaisePreviewSaved_NoSubscribers_DoesNotFlipCancel()
    {
        var ba = new UI.BivariateAnalysis("PrevBA", _collection!);
        bool cancel = false;

        ba.RaisePreviewSaved(ref cancel);

        Assert.IsFalse(cancel);
    }

    /// <summary>
    /// Verifies that <see cref="UI.BivariateAnalysis.ElementImageResourceKey"/> returns the
    /// expected resource key constant.
    /// </summary>
    [STATestMethod]
    public void ElementImageResourceKey_ReturnsExpectedKey()
    {
        var ba = new UI.BivariateAnalysis("ImgKeyBA", _collection!);
        Assert.AreEqual("BivariateAnalysisIcon", ba.ElementImageResourceKey);
    }

    /// <summary>
    /// Verifies the static <see cref="UI.BivariateAnalysis.CollectionName"/> SQLite-table-name constant.
    /// </summary>
    [TestMethod]
    public void CollectionName_IsExpectedConstant()
    {
        Assert.AreEqual("<Bivariate Distribution>", UI.BivariateAnalysis.CollectionName);
    }

    /// <summary>
    /// Verifies that <see cref="UI.BivariateAnalysis.IsValid"/> is <c>false</c> on a freshly
    /// constructed instance because marginals have not been assigned.
    /// </summary>
    [STATestMethod]
    public void Constructor_IsValid_IsFalseInitiallyDueToMissingMarginals()
    {
        var ba = new UI.BivariateAnalysis("IsValidBA", _collection!);
        Assert.IsFalse(ba.IsValid);
    }

    /// <summary>
    /// Verifies that <see cref="UI.BivariateAnalysis.Name"/> setter raises PropertyChanged on rename.
    /// </summary>
    [STATestMethod]
    public void Name_Setter_RaisesPropertyChanged()
    {
        var ba = new UI.BivariateAnalysis("NameBA", _collection!);
        var raised = new List<string>();
        ba.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        ba.Name = "NameBA-Renamed";

        Assert.IsTrue(raised.Contains(nameof(UI.BivariateAnalysis.Name)));
        Assert.AreEqual("NameBA-Renamed", ba.Name);
    }

    /// <summary>
    /// Verifies that the XY ordinate undo restore callback preserves the MCMC results.
    /// </summary>
    [STATestMethod]
    public void RestoreXYOrdinatesFromSnapshot_PreservesMcmcResults()
    {
        var ba = new UI.BivariateAnalysis("UndoXYBA", _collection!);
        var inner = (ModelAnalyses.BivariateAnalysis)ba.InnerAnalysis;
        ConfigureInnerForSyntheticBivariateResults(inner);
        var resultsBefore = inner.BayesianAnalysis.Results;
        Assert.IsNotNull(resultsBefore);

        var snapshot = CreateUndoXYOrdinates().SaveToXElement();
        InvokeRestoreXYOrdinatesFromSnapshot(ba, snapshot);

        Assert.AreSame(resultsBefore, inner.BayesianAnalysis.Results,
            "Undo replay of XY ordinates must not call ClearResults on the fitted chain.");
        Assert.IsTrue(inner.IsEstimated, "Analysis estimated state must survive XY ordinate undo replay.");
        Assert.IsTrue(inner.BayesianAnalysis.IsEstimated, "BayesianAnalysis estimated state must survive XY ordinate undo replay.");
        Assert.AreEqual(2, ba.XYOrdinates.Count);
        Assert.AreEqual(105.0, ba.XYOrdinates[1].X, 1e-12);
    }

    /// <summary>
    /// Builds a minimal SQLite <c>.bestfit</c> fixture with a corrupt
    /// <c>BivariateDistribution</c> XML cell. Referenced marginals are intentionally
    /// absent so the parse-guard short-circuit is bypassed or silently caught.
    /// </summary>
    private static void BuildBivariateAnalysisDatabaseWithCorruptXml(string path, string analysisName, string corruptXml)
    {
        var anTable = new DataTable("Bivariate Distribution Analysis");
        anTable.Columns.Add("Name", typeof(string));
        anTable.Columns.Add("Description", typeof(string));
        anTable.Columns.Add("CreationDate", typeof(string));
        anTable.Columns.Add("LastModified", typeof(string));
        anTable.Columns.Add("MarginalX", typeof(string));
        anTable.Columns.Add("MarginalY", typeof(string));
        anTable.Columns.Add("BivariateDistribution", typeof(string));

        using (var sqlite = new SQLiteManager(path))
        {
            sqlite.Open();
            sqlite.SaveDataTable(anTable);

            var anView = sqlite.GetTableManager("Bivariate Distribution Analysis");
            anView.AddRow();
            anView.EditCell(0, "Name", analysisName);
            anView.EditCell(0, "Description", "");
            anView.EditCell(0, "CreationDate", DateTime.Now.ToString("o"));
            anView.EditCell(0, "LastModified", DateTime.Now.ToString("o"));
            anView.EditCell(0, "MarginalX", "MissingMarginal");
            anView.EditCell(0, "MarginalY", "MissingMarginal");
            anView.EditCell(0, "BivariateDistribution", corruptXml);
            anView.ApplyEdits();
            sqlite.Close();
        }
    }

    /// <summary>
    /// Configures the model-layer inner analysis with valid marginals and injected MCMC results.
    /// </summary>
    /// <param name="inner">The model-layer bivariate analysis to configure.</param>
    private static void ConfigureInnerForSyntheticBivariateResults(ModelAnalyses.BivariateAnalysis inner)
    {
        const int SampleSize = 1000;
        inner.BivariateDistribution.MarginalX = MakeMarginal(
            [98.1, 102.7, 115.3, 88.4, 104.9, 92.0, 110.5, 99.2, 107.6, 101.3],
            100.0,
            10.0);
        inner.BivariateDistribution.MarginalY = MakeMarginal(
            [75.2, 82.1, 93.6, 68.7, 84.3, 72.5, 90.4, 78.9, 88.5, 81.0],
            80.0,
            8.0);
        inner.BivariateDistribution.CopulaType = CopulaType.Normal;
        inner.BivariateDistribution.SetDefaultParameters();
        inner.BayesianAnalysis.OutputLength = SampleSize;
        inner.BayesianAnalysis.SetCustomMCMCResults(
            BuildSyntheticCopulaResults(SampleSize),
            skipInformationCriteria: true);

        var isEstField = typeof(ModelAnalyses.AnalysisBase).GetField("_isEstimated",
            BindingFlags.Instance | BindingFlags.NonPublic);
        isEstField!.SetValue(inner, true);
    }

    /// <summary>
    /// Creates a fitted Normal marginal from inline exact observations.
    /// </summary>
    /// <param name="data">The exact observations for the marginal.</param>
    /// <param name="mean">The Normal location parameter.</param>
    /// <param name="standardDeviation">The Normal scale parameter.</param>
    /// <returns>A valid univariate distribution model.</returns>
    private static UnivariateDistribution MakeMarginal(double[] data, double mean, double standardDeviation)
    {
        var df = new DataFrame();
        df.ExactSeries = new ExactSeries(data);
        df.CalculatePlottingPositions();
        for (int i = 0; i < data.Length; i++)
            ((ExactData)df.ExactSeries[i]).Index = i;

        var dist = new UnivariateDistribution(df, UnivariateDistributionType.Normal);
        dist.SetParameterValues([mean, standardDeviation]);
        return dist;
    }

    /// <summary>
    /// Builds deterministic synthetic posterior draws for a one-parameter Gaussian copula.
    /// </summary>
    /// <param name="sampleSize">Number of posterior draws to create.</param>
    /// <returns>Synthetic MCMC results for the configured copula.</returns>
    private static MCMCResults BuildSyntheticCopulaResults(int sampleSize)
    {
        var output = new List<ParameterSet>(sampleSize);
        for (int i = 0; i < sampleSize; i++)
        {
            double rho = 0.25 + 0.05 * Math.Sin(i);
            output.Add(new ParameterSet([rho], 0.0));
        }

        return new MCMCResults(new ParameterSet([0.25], 0.0), output, alpha: 0.10);
    }

    /// <summary>
    /// Creates the XY ordinate snapshot used to simulate an undo callback.
    /// </summary>
    /// <returns>A valid two-row XY ordinate collection.</returns>
    private static UncertainOrderedPairedData CreateUndoXYOrdinates()
    {
        return new UncertainOrderedPairedData(
            new List<UncertainOrdinate>
            {
                new UncertainOrdinate(90.0, new Deterministic(70.0)),
                new UncertainOrdinate(105.0, new Deterministic(82.0))
            },
            false, SortOrder.Ascending,
            false, SortOrder.Ascending,
            UnivariateDistributionType.Deterministic);
    }

    /// <summary>
    /// Invokes the private XY restore method used by the recorded undo action.
    /// </summary>
    /// <param name="analysis">The UI analysis whose undo callback is being exercised.</param>
    /// <param name="snapshot">The serialized XY ordinate snapshot to restore.</param>
    private static void InvokeRestoreXYOrdinatesFromSnapshot(UI.BivariateAnalysis analysis, XElement snapshot)
    {
        var method = typeof(UI.BivariateAnalysis).GetMethod(
            "RestoreXYOrdinatesFromSnapshot",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method);
        method!.Invoke(analysis, [snapshot]);
    }
}
