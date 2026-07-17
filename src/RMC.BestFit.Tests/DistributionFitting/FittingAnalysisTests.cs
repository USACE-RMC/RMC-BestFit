using Numerics.Data;
using Numerics.Distributions;
using RMC.BestFit.Analyses;
using RMC.BestFit.Models;
using BestFitDataFrame = RMC.BestFit.Models.DataFrame;

namespace RMC.BestFit.Tests.DistributionFitting;

/// <summary>
/// Programmatic unit tests for the <c>FittingAnalysis</c> class.
/// </summary>
/// <remarks>
/// <para>
///     <b>Authors:</b>
/// </para>
/// <list type="bullet">
///     <item>Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil</item>
/// </list>
/// <para>
/// Constructor validation, property round-trips, XML serialization (without estimation),
/// and Phase 1 behavior: probability ordinate changes must NOT clear the MLE fit.
/// Computational/MLE tests (RunAsync, parity against published results) live in
/// <c>RMC.BestFit.Verification/DistributionFitting/FittingAnalysisTests.cs</c>.
/// </para>
/// </remarks>
[TestClass]
public class FittingAnalysisTests
{
    #region Test Data Helpers

    /// <summary>
    /// Sample annual peak flow data (cfs) for testing.
    /// </summary>
    private static readonly double[] SampleAnnualPeaks =
    [
        45000, 38000, 52000, 61000, 33000, 49000, 55000, 42000, 67000, 39000,
        48000, 51000, 36000, 58000, 44000, 53000, 47000, 62000, 41000, 50000,
        37000, 54000, 46000, 59000, 43000, 56000, 40000, 63000, 35000, 57000
    ];

    /// <summary>
    /// Creates a test BestFitDataFrame with exact data.
    /// </summary>
    private static BestFitDataFrame CreateTestDataFrame(int count = 30)
    {
        var df = new BestFitDataFrame();
        var data = SampleAnnualPeaks.Take(count).ToArray();
        df.ExactSeries = new ExactSeries(data);
        return df;
    }

    /// <summary>
    /// Creates a small inline BestFitDataFrame used by the legacy Phase 1 tests below.
    /// </summary>
    private static BestFitDataFrame CreateSmallTestDataFrame()
    {
        var df = new BestFitDataFrame();
        var values = new[] { 12500.0, 15300, 8900, 22100, 18700, 14200, 9800, 28500, 17400, 11600 };
        for (int i = 0; i < values.Length; i++)
            df.ExactSeries.Add(new ExactData(1990 + i, values[i]));
        return df;
    }

    /// <summary>
    /// Creates a test FittingAnalysis with sample data.
    /// </summary>
    private static FittingAnalysis CreateTestFittingAnalysis()
    {
        var df = CreateTestDataFrame();
        return new FittingAnalysis(df);
    }

    #endregion

    #region Constructor Tests

    /// <summary>
    /// Tests that the constructor with BestFitDataFrame creates a valid analysis.
    /// </summary>
    [TestMethod]
    public void Constructor_WithDataFrame_CreatesValidAnalysis()
    {
        var df = CreateTestDataFrame();

        var analysis = new FittingAnalysis(df);

        Assert.IsNotNull(analysis);
        Assert.AreSame(df, analysis.DataFrame);
        Assert.IsFalse(analysis.IsEstimated);
    }

    /// <summary>
    /// Tests that the constructor initializes the distribution list.
    /// </summary>
    [TestMethod]
    public void Constructor_InitializesDistributionList()
    {
        var analysis = CreateTestFittingAnalysis();

        Assert.IsNotNull(analysis.DistributionList);
        Assert.AreEqual(15, analysis.DistributionList.Count, "Should have 15 candidate distributions.");
    }

    /// <summary>
    /// Tests that the constructor initializes FittedDistributions.
    /// </summary>
    [TestMethod]
    public void Constructor_InitializesFittedDistributions()
    {
        var analysis = CreateTestFittingAnalysis();

        Assert.IsNotNull(analysis.FittedDistributions);
        Assert.AreEqual(15, analysis.FittedDistributions.Count, "Should have 15 fitted distribution entries.");
    }

    /// <summary>
    /// Tests that the constructor initializes ProbabilityOrdinates.
    /// </summary>
    [TestMethod]
    public void Constructor_InitializesProbabilityOrdinates()
    {
        var analysis = CreateTestFittingAnalysis();

        Assert.IsNotNull(analysis.ProbabilityOrdinates);
    }

    /// <summary>
    /// Tests that the constructor throws ArgumentNullException for null BestFitDataFrame.
    /// </summary>
    [TestMethod]
    public void Constructor_WithNullDataFrame_ThrowsArgumentNullException()
    {
        Assert.ThrowsException<ArgumentNullException>(() => new FittingAnalysis(null!));
    }

    /// <summary>
    /// Tests that the XElement constructor throws for null BestFitDataFrame.
    /// </summary>
    [TestMethod]
    public void Constructor_WithXElement_NullDataFrame_ThrowsArgumentNullException()
    {
        var df = CreateTestDataFrame();
        var analysis = new FittingAnalysis(df);
        var xElement = analysis.ToXElement();

        Assert.ThrowsException<ArgumentNullException>(() => new FittingAnalysis(null!, xElement));
    }

    /// <summary>
    /// Tests that the XElement constructor throws for null XElement.
    /// </summary>
    [TestMethod]
    public void Constructor_WithXElement_NullXElement_ThrowsArgumentNullException()
    {
        var df = CreateTestDataFrame();

        Assert.ThrowsException<ArgumentNullException>(() => new FittingAnalysis(df, null!));
    }

    #endregion

    #region Serialization Tests

    /// <summary>
    /// Tests that ToXElement creates valid XML.
    /// </summary>
    [TestMethod]
    public void ToXElement_CreatesValidXml()
    {
        var analysis = CreateTestFittingAnalysis();

        var xElement = analysis.ToXElement();

        Assert.IsNotNull(xElement);
        Assert.AreEqual("FittingAnalysis", xElement.Name.LocalName);
    }

    /// <summary>
    /// Tests that XML round-trip preserves configuration (without running analysis).
    /// </summary>
    [TestMethod]
    public void XmlRoundTrip_PreservesConfiguration()
    {
        var df = CreateTestDataFrame();
        var original = new FittingAnalysis(df);
        original.ProbabilityOrdinates.AddRange([0.99, 0.98, 0.96, 0.90, 0.50]);

        var xElement = original.ToXElement();
        var restored = new FittingAnalysis(df, xElement);

        Assert.AreEqual(original.IsEstimated, restored.IsEstimated);
        Assert.AreEqual(original.ProbabilityOrdinates.Count, restored.ProbabilityOrdinates.Count);
        for (int i = 0; i < original.ProbabilityOrdinates.Count; i++)
        {
            Assert.AreEqual(original.ProbabilityOrdinates[i], restored.ProbabilityOrdinates[i], 1e-10);
        }
    }

    /// <summary>
    /// Tests that XML contains ProbabilityOrdinates element.
    /// </summary>
    [TestMethod]
    public void ToXElement_ContainsProbabilityOrdinates()
    {
        var analysis = CreateTestFittingAnalysis();
        analysis.ProbabilityOrdinates.AddRange([0.99, 0.98, 0.96]);

        var xElement = analysis.ToXElement();

        var probElement = xElement.Element("ProbabilityOrdinates");
        Assert.IsNotNull(probElement);
    }

    /// <summary>
    /// Tests that XML contains FittedDistributions element.
    /// </summary>
    [TestMethod]
    public void ToXElement_ContainsFittedDistributions()
    {
        var analysis = CreateTestFittingAnalysis();

        var xElement = analysis.ToXElement();

        var fittedElement = xElement.Element("FittedDistributions");
        Assert.IsNotNull(fittedElement);
    }

    #endregion

    #region Property Change Tests

    /// <summary>
    /// Tests that BestFitDataFrame property change raises PropertyChanged.
    /// </summary>
    [TestMethod]
    public void DataFrame_Change_RaisesPropertyChanged()
    {
        var analysis = CreateTestFittingAnalysis();
        bool propertyChanged = false;
        analysis.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(FittingAnalysis.DataFrame))
                propertyChanged = true;
        };

        analysis.DataFrame = CreateTestDataFrame(20);

        Assert.IsTrue(propertyChanged, "PropertyChanged should be raised for DataFrame.");
    }

    #endregion

    #region CancelAnalysis Tests

    /// <summary>
    /// Tests that CancelAnalysis is safe when not running.
    /// </summary>
    [TestMethod]
    public void CancelAnalysis_WhenNotRunning_IsSafe()
    {
        var analysis = CreateTestFittingAnalysis();

        // Should not throw
        analysis.CancelAnalysis();

        Assert.IsFalse(analysis.IsEstimated);
    }

    #endregion

    #region DistributionList Tests

    /// <summary>
    /// Tests that DistributionList contains all 15 distributions.
    /// </summary>
    [TestMethod]
    public void DistributionList_ContainsAll15Distributions()
    {
        var analysis = CreateTestFittingAnalysis();

        var types = analysis.DistributionList.Select(d => d.Type).ToList();

        Assert.IsTrue(types.Contains(UnivariateDistributionType.Exponential));
        Assert.IsTrue(types.Contains(UnivariateDistributionType.GammaDistribution));
        Assert.IsTrue(types.Contains(UnivariateDistributionType.GeneralizedExtremeValue));
        Assert.IsTrue(types.Contains(UnivariateDistributionType.GeneralizedLogistic));
        Assert.IsTrue(types.Contains(UnivariateDistributionType.GeneralizedNormal));
        Assert.IsTrue(types.Contains(UnivariateDistributionType.GeneralizedPareto));
        Assert.IsTrue(types.Contains(UnivariateDistributionType.Gumbel));
        Assert.IsTrue(types.Contains(UnivariateDistributionType.KappaFour));
        Assert.IsTrue(types.Contains(UnivariateDistributionType.LnNormal));
        Assert.IsTrue(types.Contains(UnivariateDistributionType.Logistic));
        Assert.IsTrue(types.Contains(UnivariateDistributionType.LogNormal));
        Assert.IsTrue(types.Contains(UnivariateDistributionType.LogPearsonTypeIII));
        Assert.IsTrue(types.Contains(UnivariateDistributionType.Normal));
        Assert.IsTrue(types.Contains(UnivariateDistributionType.PearsonTypeIII));
        Assert.IsTrue(types.Contains(UnivariateDistributionType.Weibull));
    }

    #endregion

    #region Phase 1: ProbabilityOrdinates Behavior Without Estimation

    /// <summary>
    /// Tests that changing ProbabilityOrdinates on a fresh (not-estimated) FittingAnalysis is a no-op.
    /// </summary>
    [TestMethod]
    public void ProbabilityOrdinatesChange_WhenNotEstimated_DoesNotThrow()
    {
        var df = CreateSmallTestDataFrame();
        var analysis = new FittingAnalysis(df);
        analysis.ProbabilityOrdinates.Clear();

        analysis.ProbabilityOrdinates.Add(0.5);
        analysis.ProbabilityOrdinates.Add(0.1);
        analysis.ProbabilityOrdinates.Add(0.01);

        Assert.IsFalse(analysis.IsEstimated, "IsEstimated should remain false.");
        Assert.AreEqual(3, analysis.ProbabilityOrdinates.Count);
    }

    /// <summary>
    /// Tests that FittedDistributions collection (initialized to default, unfitted distributions)
    /// is reference-equal before and after a ProbabilityOrdinates change — confirming ordinates
    /// do not trigger ClearResults() in the model layer (Phase 1 fix).
    /// </summary>
    [TestMethod]
    public void ProbabilityOrdinatesChange_DoesNotReplaceFittedDistributionsList()
    {
        var df = CreateSmallTestDataFrame();
        var analysis = new FittingAnalysis(df);
        var before = analysis.FittedDistributions;

        analysis.ProbabilityOrdinates.Add(0.005);

        Assert.AreSame(before, analysis.FittedDistributions,
            "FittedDistributions list must be reference-equal — ordinate change should not reset the fit state.");
    }

    /// <summary>
    /// Tests that ProbabilityOrdinates change fires PropertyChanged(nameof(ProbabilityOrdinates))
    /// so the App-layer control can refresh its frequency plot and summary-statistics table.
    /// </summary>
    [TestMethod]
    public void ProbabilityOrdinatesChange_RaisesProbabilityOrdinatesPropertyChanged()
    {
        var df = CreateSmallTestDataFrame();
        var analysis = new FittingAnalysis(df);
        bool notified = false;
        analysis.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(FittingAnalysis.ProbabilityOrdinates)) notified = true;
        };

        analysis.ProbabilityOrdinates.Add(0.001);

        Assert.IsTrue(notified, "ProbabilityOrdinates PropertyChanged should fire so UI can refresh.");
    }

    /// <summary>
    /// Tests that ProbabilityOrdinates does NOT fire an AnalysisResults PropertyChanged notification,
    /// since FittingAnalysis has no AnalysisResults field (it uses FittedDistributions instead).
    /// </summary>
    [TestMethod]
    public void ProbabilityOrdinatesChange_DoesNotFireSpuriousFittedDistributionsChange()
    {
        var df = CreateSmallTestDataFrame();
        var analysis = new FittingAnalysis(df);
        bool fittedDistChanged = false;
        bool isEstimatedChanged = false;
        analysis.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(FittingAnalysis.FittedDistributions)) fittedDistChanged = true;
            if (e.PropertyName == nameof(FittingAnalysis.IsEstimated)) isEstimatedChanged = true;
        };

        analysis.ProbabilityOrdinates.Add(0.5);

        Assert.IsFalse(fittedDistChanged, "FittedDistributions PropertyChanged should NOT fire for ordinate change.");
        Assert.IsFalse(isEstimatedChanged, "IsEstimated PropertyChanged should NOT fire for ordinate change.");
    }

    #endregion
}
