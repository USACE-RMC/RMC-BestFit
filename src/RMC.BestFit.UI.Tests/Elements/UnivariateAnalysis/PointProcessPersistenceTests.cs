using DatabaseManager;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RMC.BestFit.Models;
using RMC.BestFit.UI;
using System.Data;
using System.IO;
using System.Xml.Linq;
using UiInputData = RMC.BestFit.UI.InputData;

namespace RMC.BestFit.UI.Tests.Elements.UnivariateAnalysis;

/// <summary>
/// Regression tests for preserving stored point-process inputs while opening legacy SQLite rows.
/// </summary>
[TestClass]
public class PointProcessPersistenceTests
{
    private const string StoredModelXml = """
        <PointProcessModel Threshold="1234" TotalYears="77" IsTotalYearsInferred="False" UseDefaults="True" IsSeasonal="False" TimeBlock="WaterYear" StartMonth="10" UseDefaultFlatPriors="False" UseJeffreysRuleForScale="True" EnableQuantilePriors="False" UseSingleQuantile="False">
          <Distribution Type="CompetingRisks" XTransform="None" ProbabilityTransform="NormalZ" MinimumOfRandomVariables="False" Dependency="Independent" Distributions="GeneralizedExtremeValue" Parameters="2000|500|0.1">
            <CorrelationMatrix><Correlation_Row>0</Correlation_Row></CorrelationMatrix>
          </Distribution>
          <Parameters>
            <ModelParameter OwnerName="" Name="Location (ξ)" Value="2000" LowerBound="-10000" UpperBound="10000" IsPositive="False" IsFixed="False"><Distribution Type="Uniform" Min="-10000" Max="10000" /></ModelParameter>
            <ModelParameter OwnerName="" Name="Scale (α)" Value="500" LowerBound="0.01" UpperBound="10000" IsPositive="True" IsFixed="False"><Distribution Type="Uniform" Min="0.01" Max="10000" /></ModelParameter>
            <ModelParameter OwnerName="" Name="Shape (κ)" Value="0.1" LowerBound="-10" UpperBound="10" IsPositive="False" IsFixed="False"><Distribution Type="Uniform" Min="-10" Max="10" /></ModelParameter>
          </Parameters>
          <QuantilePriors />
        </PointProcessModel>
        """;

    /// <summary>
    /// Verifies saved threshold, exposure, and exposure origin survive Open even when defaults are
    /// enabled and the current input data would infer different values.
    /// </summary>
    [STATestMethod]
    [DoNotParallelize]
    public void Open_UseDefaultsTrue_PreservesStoredThresholdExposureAndOrigin()
    {
        WithLegacyPointProcess(XElement.Parse(StoredModelXml), (analysis, _) =>
        {
            Assert.AreEqual(1234d, analysis.PointProcess.Threshold, 0d);
            Assert.AreEqual(77d, analysis.PointProcess.TotalYears, 0d);
            Assert.IsFalse(analysis.PointProcess.IsTotalYearsInferred);
            Assert.IsTrue(analysis.PointProcess.UseDefaults);

            analysis.Save();
            analysis.Open();

            Assert.AreEqual(1234d, analysis.PointProcess.Threshold, 0d);
            Assert.AreEqual(77d, analysis.PointProcess.TotalYears, 0d);
            Assert.IsFalse(analysis.PointProcess.IsTotalYearsInferred);
        });
    }

    /// <summary>
    /// Verifies only an absent threshold is inferred; saved exposure and origin remain unchanged.
    /// </summary>
    [STATestMethod]
    [DoNotParallelize]
    public void Open_MissingThreshold_InfersOnlyThreshold()
    {
        XElement xml = XElement.Parse(StoredModelXml);
        xml.Attribute(nameof(PointProcessModel.Threshold))!.Remove();

        WithLegacyPointProcess(xml, (analysis, _) =>
        {
            Assert.AreEqual(1000d, analysis.PointProcess.Threshold, 0d);
            Assert.AreEqual(77d, analysis.PointProcess.TotalYears, 0d);
            Assert.IsFalse(analysis.PointProcess.IsTotalYearsInferred);
        });

        Assert.IsNull(xml.Attribute(nameof(PointProcessModel.Threshold)), "Import must hydrate a working XML copy.");
    }

    /// <summary>
    /// Verifies an absent exposure is inferred without overwriting a present threshold or origin.
    /// </summary>
    [STATestMethod]
    [DoNotParallelize]
    public void Open_MissingExposure_PreservesPresentThresholdAndOrigin()
    {
        XElement xml = XElement.Parse(StoredModelXml);
        xml.Attribute(nameof(PointProcessModel.TotalYears))!.Remove();

        WithLegacyPointProcess(xml, (analysis, _) =>
        {
            Assert.AreEqual(1234d, analysis.PointProcess.Threshold, 0d);
            Assert.AreEqual(10d, analysis.PointProcess.TotalYears, 0d);
            Assert.IsFalse(analysis.PointProcess.IsTotalYearsInferred);
        });
    }

    /// <summary>
    /// Verifies absent exposure and origin fields are inferred using the existing event-span logic.
    /// </summary>
    [STATestMethod]
    [DoNotParallelize]
    public void Open_MissingExposureAndOrigin_InfersBothAbsentFields()
    {
        XElement xml = XElement.Parse(StoredModelXml);
        xml.Attribute(nameof(PointProcessModel.TotalYears))!.Remove();
        xml.Attribute(nameof(PointProcessModel.IsTotalYearsInferred))!.Remove();

        WithLegacyPointProcess(xml, (analysis, _) =>
        {
            Assert.AreEqual(1234d, analysis.PointProcess.Threshold, 0d);
            Assert.AreEqual(10d, analysis.PointProcess.TotalYears, 0d);
            Assert.IsTrue(analysis.PointProcess.IsTotalYearsInferred);
        });
    }

    /// <summary>
    /// Verifies the import-only preservation boundary does not disable normal default refresh
    /// after a later user edit re-enables the model's default inputs.
    /// </summary>
    [STATestMethod]
    [DoNotParallelize]
    public void Open_LaterDefaultsToggle_StillRefreshesFromInputData()
    {
        WithLegacyPointProcess(XElement.Parse(StoredModelXml), (analysis, _) =>
        {
            Assert.AreEqual(1234d, analysis.PointProcess.Threshold, 0d);
            Assert.AreEqual(77d, analysis.PointProcess.TotalYears, 0d);

            analysis.PointProcess.UseDefaults = false;
            analysis.PointProcess.UseDefaults = true;

            Assert.AreEqual(1000d, analysis.PointProcess.Threshold, 0d);
            Assert.AreEqual(10d, analysis.PointProcess.TotalYears, 0d);
            Assert.IsTrue(analysis.PointProcess.IsTotalYearsInferred);
        });
    }

    /// <summary>
    /// Opens a literal legacy model row with a prelinked input-data element in a temporary database.
    /// </summary>
    /// <param name="modelXml">The literal model payload to store.</param>
    /// <param name="assertions">Assertions performed after Open.</param>
    /// <remarks>
    /// Prelinking the input isolates the model-restoration boundary without mutating the singleton
    /// project's shared input-data collection. Full project reference resolution is covered by the
    /// supplied-project acceptance test.
    /// </remarks>
    private static void WithLegacyPointProcess(XElement modelXml, Action<PointProcessAnalysis, UiInputData> assertions)
    {
        string path = Path.Combine(Path.GetTempPath(), $"BestFit-LegacyPointProcess-{Guid.NewGuid():N}.db");
        BestFitProject project = BestFitProject.GetInstance();
        string previousPath = project.FullFileName;
        try
        {
            project.FullFileName = path;
            var dataFrame = new RMC.BestFit.Models.DataFrame();
            for (int index = 0; index < 10; index++)
                dataFrame.ExactSeries.Add(new ExactData(2000 + index, 1500d + 100d * index));
            var inputData = new UiInputData("LegacyInput", new InputDataCollection(project))
            {
                ExactDataMethod = UiInputData.ExactDataEntryType.PeaksOverThresholdSeries,
                Threshold = 1000d,
                DataFrame = dataFrame
            };
            var collection = new UnivariateAnalysisCollection(project);
            var analysis = new PointProcessAnalysis("LegacyPointProcess", collection);
            analysis.PointProcess.UseDefaultFlatPriors = false;
            analysis.InputData = inputData;

            var table = new DataTable(PointProcessAnalysis.CollectionName);
            table.Columns.Add("Name", typeof(string));
            table.Columns.Add("PointProcess", typeof(string));
            table.Columns.Add("ProbabilityOrdinates", typeof(string));
            table.Rows.Add("LegacyPointProcess", modelXml.ToString(), "0.5|0.9|0.99");
            var sqlite = new SQLiteManager(path);
            sqlite.Open();
            try
            {
                sqlite.SaveDataTable(table);
            }
            finally
            {
                sqlite.Close();
            }

            analysis.Open();
            assertions(analysis, inputData);
        }
        finally
        {
            project.FullFileName = previousPath;
            System.Data.SQLite.SQLiteConnection.ClearAllPools();
            foreach (string candidate in new[] { path, path + "-wal", path + "-shm" })
            {
                if (File.Exists(candidate)) File.Delete(candidate);
            }
        }
    }
}
