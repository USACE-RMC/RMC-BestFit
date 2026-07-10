using RMC.BestFit.Diagnostics;
using RMC.BestFit.Models;

namespace RMC.BestFit.Verification.ModelEstimation;

/// <summary>
/// Unit tests for the <see cref="InfluenceDiagnostics"/> class.
/// Tests construction, PSIS-LOO diagnostics, serialization, and edge cases.
/// </summary>
[TestClass]
public class InfluenceDiagnosticsTests
{
    #region Constructor Tests

    /// <summary>
    /// Verifies <c>Test_EmptyConstructor_CreatesEmptyDiagnostics</c>.
    /// </summary>
    [TestMethod]
    public void Test_EmptyConstructor_CreatesEmptyDiagnostics()
    {
        var diagnostics = new InfluenceDiagnostics();

        Assert.AreEqual(0, diagnostics.Count);
        Assert.IsNotNull(diagnostics.Observations);
        Assert.AreEqual(0, diagnostics.Observations.Length);
        Assert.IsTrue(double.IsNaN(diagnostics.MeanParetoK));
        Assert.IsTrue(double.IsNaN(diagnostics.MaxParetoK));
    }

    /// <summary>
    /// Verifies <c>Test_Constructor_WithObservationArray</c>.
    /// </summary>
    [TestMethod]
    public void Test_Constructor_WithObservationArray()
    {
        var observations = new ObservationInfluence[]
        {
            new(0, 0.2, -2.0, 100.0),
            new(1, 0.4, -1.5, 105.0),
            new(2, 0.6, -2.5, 95.0)
        };

        var diagnostics = new InfluenceDiagnostics(observations);

        Assert.AreEqual(3, diagnostics.Count);
        Assert.AreEqual((0.2 + 0.4 + 0.6) / 3, diagnostics.MeanParetoK, 1e-10);
        Assert.AreEqual(0.6, diagnostics.MaxParetoK);
    }

    /// <summary>
    /// Verifies <c>Test_Constructor_NullObservations_ThrowsException</c>.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Test_Constructor_NullObservations_ThrowsException()
    {
        _ = new InfluenceDiagnostics((ObservationInfluence[])null!);
    }

    /// <summary>
    /// Verifies <c>Test_Constructor_WithParetoKAndElpdArrays</c>.
    /// </summary>
    [TestMethod]
    public void Test_Constructor_WithParetoKAndElpdArrays()
    {
        double[] paretoK = [0.1, 0.3, 0.5, 0.8, 1.2];
        double[] elpdLoo = [-1.0, -1.5, -2.0, -3.0, -5.0];

        var diagnostics = new InfluenceDiagnostics(paretoK, elpdLoo);

        Assert.AreEqual(5, diagnostics.Count);
        Assert.AreEqual(1.2, diagnostics.MaxParetoK);
        Assert.AreEqual(3, diagnostics.CountParetoKAbove05); // 0.5, 0.8, and 1.2
        Assert.AreEqual(2, diagnostics.CountParetoKAbove07); // 0.8 and 1.2
        Assert.AreEqual(1, diagnostics.CountParetoKAbove10); // 1.2
    }

    /// <summary>
    /// Verifies <c>Test_Constructor_MismatchedArrayLengths_ThrowsException</c>.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void Test_Constructor_MismatchedArrayLengths_ThrowsException()
    {
        double[] paretoK = [0.1, 0.3, 0.5];
        double[] elpdLoo = [-1.0, -1.5];

        _ = new InfluenceDiagnostics(paretoK, elpdLoo);
    }

    /// <summary>
    /// Verifies <c>Test_Constructor_WithDataComponents</c>.
    /// </summary>
    [TestMethod]
    public void Test_Constructor_WithDataComponents()
    {
        double[] paretoK = [0.2, 0.6];
        double[] elpdLoo = [-1.0, -2.0];
        var dataComponents = new List<DataComponent>
        {
            new(0, -1.0, 100.0, "Obs1"),
            new(1, -2.0, 150.0, DataComponentType.LeftCensored, 5, "Thresh1")
        };

        var diagnostics = new InfluenceDiagnostics(paretoK, elpdLoo, dataComponents);

        Assert.AreEqual(2, diagnostics.Count);
        Assert.AreEqual(100.0, diagnostics[0].Value);
        Assert.AreEqual(150.0, diagnostics[1].Value);
        Assert.AreEqual(DataComponentType.Exact, diagnostics[0].DataType);
        Assert.AreEqual(DataComponentType.LeftCensored, diagnostics[1].DataType);
        Assert.AreEqual(5, diagnostics[1].Count);
    }

    #endregion

    #region Summary Statistics Tests

    /// <summary>
    /// Verifies <c>Test_CountParetoKAbove05_CorrectCounting</c>.
    /// </summary>
    [TestMethod]
    public void Test_CountParetoKAbove05_CorrectCounting()
    {
        double[] paretoK = [0.49, 0.50, 0.51, 0.69, 0.70, 0.71, 0.99, 1.0, 1.5];
        double[] elpdLoo = new double[9];

        var diagnostics = new InfluenceDiagnostics(paretoK, elpdLoo);

        // >= 0.5: 0.50, 0.51, 0.69, 0.70, 0.71, 0.99, 1.0, 1.5 = 8
        Assert.AreEqual(8, diagnostics.CountParetoKAbove05);
    }

    /// <summary>
    /// Verifies <c>Test_CountParetoKAbove07_CorrectCounting</c>.
    /// </summary>
    [TestMethod]
    public void Test_CountParetoKAbove07_CorrectCounting()
    {
        double[] paretoK = [0.49, 0.50, 0.51, 0.69, 0.70, 0.71, 0.99, 1.0, 1.5];
        double[] elpdLoo = new double[9];

        var diagnostics = new InfluenceDiagnostics(paretoK, elpdLoo);

        // >= 0.7: 0.70, 0.71, 0.99, 1.0, 1.5 = 5
        Assert.AreEqual(5, diagnostics.CountParetoKAbove07);
    }

    /// <summary>
    /// Verifies <c>Test_CountParetoKAbove10_CorrectCounting</c>.
    /// </summary>
    [TestMethod]
    public void Test_CountParetoKAbove10_CorrectCounting()
    {
        double[] paretoK = [0.49, 0.50, 0.51, 0.69, 0.70, 0.71, 0.99, 1.0, 1.5];
        double[] elpdLoo = new double[9];

        var diagnostics = new InfluenceDiagnostics(paretoK, elpdLoo);

        // >= 1.0: 1.0, 1.5 = 2
        Assert.AreEqual(2, diagnostics.CountParetoKAbove10);
    }

    /// <summary>
    /// Verifies <c>Test_ProportionProblematic_CalculatedCorrectly</c>.
    /// </summary>
    [TestMethod]
    public void Test_ProportionProblematic_CalculatedCorrectly()
    {
        double[] paretoK = [0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 1.0];
        double[] elpdLoo = new double[10];

        var diagnostics = new InfluenceDiagnostics(paretoK, elpdLoo);

        // 4 out of 10 have k >= 0.7 (0.7, 0.8, 0.9, 1.0)
        Assert.AreEqual(0.4, diagnostics.ProportionProblematic, 1e-10);
    }

    /// <summary>
    /// Verifies <c>Test_IsReliable_AllGood</c>.
    /// </summary>
    [TestMethod]
    public void Test_IsReliable_AllGood()
    {
        double[] paretoK = [0.1, 0.2, 0.3, 0.4, 0.45];
        double[] elpdLoo = new double[5];

        var diagnostics = new InfluenceDiagnostics(paretoK, elpdLoo);

        Assert.IsTrue(diagnostics.IsReliable);
    }

    /// <summary>
    /// Verifies <c>Test_IsReliable_WithVeryBadObservation_ReturnsFalse</c>.
    /// </summary>
    [TestMethod]
    public void Test_IsReliable_WithVeryBadObservation_ReturnsFalse()
    {
        // Even one k >= 1.0 makes it unreliable
        double[] paretoK = [0.1, 0.2, 0.3, 0.4, 1.0];
        double[] elpdLoo = new double[5];

        var diagnostics = new InfluenceDiagnostics(paretoK, elpdLoo);

        Assert.IsFalse(diagnostics.IsReliable);
    }

    /// <summary>
    /// Verifies <c>Test_IsReliable_HighProportionProblematic_ReturnsFalse</c>.
    /// </summary>
    [TestMethod]
    public void Test_IsReliable_HighProportionProblematic_ReturnsFalse()
    {
        // More than 1% have k >= 0.7
        double[] paretoK = Enumerable.Repeat(0.8, 10).ToArray();
        double[] elpdLoo = new double[10];

        var diagnostics = new InfluenceDiagnostics(paretoK, elpdLoo);

        Assert.IsFalse(diagnostics.IsReliable);
    }

    #endregion

    #region Query Methods Tests

    /// <summary>
    /// Verifies <c>Test_GetMostInfluentialObservations_ReturnsTopN</c>.
    /// </summary>
    [TestMethod]
    public void Test_GetMostInfluentialObservations_ReturnsTopN()
    {
        double[] paretoK = [0.9, 0.1, 0.5, 0.3, 0.8];
        double[] elpdLoo = new double[5];

        var diagnostics = new InfluenceDiagnostics(paretoK, elpdLoo);

        var top3 = diagnostics.GetMostInfluentialObservations(3);

        Assert.AreEqual(3, top3.Length);
        Assert.AreEqual(0.9, top3[0].ParetoK); // Index 0
        Assert.AreEqual(0.8, top3[1].ParetoK); // Index 4
        Assert.AreEqual(0.5, top3[2].ParetoK); // Index 2
    }

    /// <summary>
    /// Verifies <c>Test_GetMostInfluentialObservations_DefaultReturnsAll</c>.
    /// </summary>
    [TestMethod]
    public void Test_GetMostInfluentialObservations_DefaultReturnsAll()
    {
        double[] paretoK = [0.1, 0.2, 0.3];
        double[] elpdLoo = new double[3];

        var diagnostics = new InfluenceDiagnostics(paretoK, elpdLoo);

        var all = diagnostics.GetMostInfluentialObservations();

        Assert.AreEqual(3, all.Length);
    }

    /// <summary>
    /// Verifies <c>Test_GetMostInfluentialObservations_EmptyDiagnostics</c>.
    /// </summary>
    [TestMethod]
    public void Test_GetMostInfluentialObservations_EmptyDiagnostics()
    {
        var diagnostics = new InfluenceDiagnostics();

        var result = diagnostics.GetMostInfluentialObservations(5);

        Assert.AreEqual(0, result.Length);
    }

    /// <summary>
    /// Verifies <c>Test_GetProblematicObservations_DefaultThreshold</c>.
    /// </summary>
    [TestMethod]
    public void Test_GetProblematicObservations_DefaultThreshold()
    {
        double[] paretoK = [0.1, 0.5, 0.7, 0.8, 1.2];
        double[] elpdLoo = new double[5];

        var diagnostics = new InfluenceDiagnostics(paretoK, elpdLoo);

        var problematic = diagnostics.GetProblematicObservations(); // Default threshold 0.7

        Assert.AreEqual(3, problematic.Length);
        Assert.AreEqual(1.2, problematic[0].ParetoK);
        Assert.AreEqual(0.8, problematic[1].ParetoK);
        Assert.AreEqual(0.7, problematic[2].ParetoK);
    }

    /// <summary>
    /// Verifies <c>Test_GetProblematicObservations_CustomThreshold</c>.
    /// </summary>
    [TestMethod]
    public void Test_GetProblematicObservations_CustomThreshold()
    {
        double[] paretoK = [0.1, 0.5, 0.7, 0.8, 1.2];
        double[] elpdLoo = new double[5];

        var diagnostics = new InfluenceDiagnostics(paretoK, elpdLoo);

        var problematic = diagnostics.GetProblematicObservations(0.5);

        Assert.AreEqual(4, problematic.Length); // 0.5, 0.7, 0.8, 1.2
    }

    /// <summary>
    /// Verifies <c>Test_Indexer_ReturnsCorrectObservation</c>.
    /// </summary>
    [TestMethod]
    public void Test_Indexer_ReturnsCorrectObservation()
    {
        double[] paretoK = [0.1, 0.5, 0.9];
        double[] elpdLoo = [-1.0, -2.0, -3.0];

        var diagnostics = new InfluenceDiagnostics(paretoK, elpdLoo);

        Assert.AreEqual(0.5, diagnostics[1].ParetoK);
        Assert.AreEqual(-3.0, diagnostics[2].ElpdLoo);
    }

    /// <summary>
    /// Verifies <c>Test_Indexer_OutOfRange_ThrowsException</c>.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(IndexOutOfRangeException))]
    public void Test_Indexer_OutOfRange_ThrowsException()
    {
        double[] paretoK = [0.1, 0.5];
        double[] elpdLoo = [-1.0, -2.0];

        var diagnostics = new InfluenceDiagnostics(paretoK, elpdLoo);
        _ = diagnostics[5];
    }

    #endregion

    #region Reliability Summary Tests

    /// <summary>
    /// Verifies <c>Test_GetReliabilitySummary_Good</c>.
    /// </summary>
    [TestMethod]
    public void Test_GetReliabilitySummary_Good()
    {
        double[] paretoK = [0.1, 0.2, 0.3, 0.4];
        double[] elpdLoo = new double[4];

        var diagnostics = new InfluenceDiagnostics(paretoK, elpdLoo);

        var summary = diagnostics.GetReliabilitySummary();

        Assert.IsTrue(summary.Contains("GOOD"));
    }

    /// <summary>
    /// Verifies <c>Test_GetReliabilitySummary_OK</c>.
    /// </summary>
    [TestMethod]
    public void Test_GetReliabilitySummary_OK()
    {
        double[] paretoK = [0.1, 0.2, 0.3, 0.55]; // One moderate influence
        double[] elpdLoo = new double[4];

        var diagnostics = new InfluenceDiagnostics(paretoK, elpdLoo);

        var summary = diagnostics.GetReliabilitySummary();

        Assert.IsTrue(summary.Contains("OK"));
    }

    /// <summary>
    /// Verifies <c>Test_GetReliabilitySummary_Caution</c>.
    /// </summary>
    [TestMethod]
    public void Test_GetReliabilitySummary_Caution()
    {
        // More than 1% above 0.7 (but less than 1.0)
        double[] paretoK = [0.1, 0.75, 0.8];
        double[] elpdLoo = new double[3];

        var diagnostics = new InfluenceDiagnostics(paretoK, elpdLoo);

        var summary = diagnostics.GetReliabilitySummary();

        Assert.IsTrue(summary.Contains("CAUTION"));
    }

    /// <summary>
    /// Verifies <c>Test_GetReliabilitySummary_Unreliable</c>.
    /// </summary>
    [TestMethod]
    public void Test_GetReliabilitySummary_Unreliable()
    {
        double[] paretoK = [0.1, 0.3, 1.5]; // One k >= 1.0
        double[] elpdLoo = new double[3];

        var diagnostics = new InfluenceDiagnostics(paretoK, elpdLoo);

        var summary = diagnostics.GetReliabilitySummary();

        Assert.IsTrue(summary.Contains("UNRELIABLE"));
    }

    /// <summary>
    /// Verifies <c>Test_GetReliabilitySummary_Empty</c>.
    /// </summary>
    [TestMethod]
    public void Test_GetReliabilitySummary_Empty()
    {
        var diagnostics = new InfluenceDiagnostics();

        var summary = diagnostics.GetReliabilitySummary();

        Assert.IsTrue(summary.Contains("No observations"));
    }

    #endregion

    #region Serialization Tests

    /// <summary>
    /// Verifies <c>Test_ToXElement_ContainsAllAttributes</c>.
    /// </summary>
    [TestMethod]
    public void Test_ToXElement_ContainsAllAttributes()
    {
        double[] paretoK = [0.2, 0.6];
        double[] elpdLoo = [-1.0, -2.0];

        var diagnostics = new InfluenceDiagnostics(paretoK, elpdLoo);

        var xElement = diagnostics.ToXElement();

        Assert.AreEqual("InfluenceDiagnostics", xElement.Name.LocalName);
        Assert.IsNotNull(xElement.Attribute("MeanParetoK"));
        Assert.IsNotNull(xElement.Attribute("MaxParetoK"));
        Assert.IsNotNull(xElement.Attribute("CountParetoKAbove05"));
        Assert.IsNotNull(xElement.Attribute("CountParetoKAbove07"));
        Assert.IsNotNull(xElement.Attribute("CountParetoKAbove10"));
        Assert.AreEqual(2, xElement.Elements("Observation").Count());
    }

    /// <summary>
    /// Verifies <c>Test_FromXElement_RestoresAllProperties</c>.
    /// </summary>
    [TestMethod]
    public void Test_FromXElement_RestoresAllProperties()
    {
        double[] paretoK = [0.2, 0.6, 0.85];
        double[] elpdLoo = [-1.0, -2.0, -3.0];
        var dataComponents = new List<DataComponent>
        {
            new(0, -1.0, 100.0, "Obs1"),
            new(1, -2.0, 150.0, "Obs2"),
            new(2, -3.0, 200.0, "Obs3")
        };

        var original = new InfluenceDiagnostics(paretoK, elpdLoo, dataComponents);
        var xElement = original.ToXElement();
        var restored = new InfluenceDiagnostics(xElement);

        Assert.AreEqual(original.Count, restored.Count);
        Assert.AreEqual(original.MeanParetoK, restored.MeanParetoK, 1e-10);
        Assert.AreEqual(original.MaxParetoK, restored.MaxParetoK, 1e-10);
        Assert.AreEqual(original.CountParetoKAbove05, restored.CountParetoKAbove05);
        Assert.AreEqual(original.CountParetoKAbove07, restored.CountParetoKAbove07);
        Assert.AreEqual(original.CountParetoKAbove10, restored.CountParetoKAbove10);

        for (int i = 0; i < original.Count; i++)
        {
            Assert.AreEqual(original[i].Index, restored[i].Index);
            Assert.AreEqual(original[i].ParetoK, restored[i].ParetoK, 1e-10);
            Assert.AreEqual(original[i].ElpdLoo, restored[i].ElpdLoo, 1e-10);
        }
    }

    /// <summary>
    /// Verifies <c>Test_FromXElement_NullElement_ThrowsException</c>.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Test_FromXElement_NullElement_ThrowsException()
    {
        _ = new InfluenceDiagnostics((System.Xml.Linq.XElement)null!);
    }

    #endregion

    #region Edge Case Tests

    /// <summary>
    /// Verifies <c>Test_AllNaNParetoK</c>.
    /// </summary>
    [TestMethod]
    public void Test_AllNaNParetoK()
    {
        double[] paretoK = [double.NaN, double.NaN, double.NaN];
        double[] elpdLoo = new double[3];

        var diagnostics = new InfluenceDiagnostics(paretoK, elpdLoo);

        Assert.IsTrue(double.IsNaN(diagnostics.MeanParetoK));
        Assert.AreEqual(0, diagnostics.CountParetoKAbove05);
        Assert.AreEqual(0, diagnostics.CountParetoKAbove07);
        Assert.AreEqual(0, diagnostics.CountParetoKAbove10);
    }

    /// <summary>
    /// Verifies <c>Test_NegativeParetoK</c>.
    /// </summary>
    [TestMethod]
    public void Test_NegativeParetoK()
    {
        // Negative Pareto k can occur in rare cases
        double[] paretoK = [-0.1, 0.2, 0.3];
        double[] elpdLoo = new double[3];

        var diagnostics = new InfluenceDiagnostics(paretoK, elpdLoo);

        Assert.AreEqual(-0.1, diagnostics[0].ParetoK);
        Assert.AreEqual(ParetoKCategory.Good, diagnostics[0].Category);
    }

    /// <summary>
    /// Verifies <c>Test_SingleObservation</c>.
    /// </summary>
    [TestMethod]
    public void Test_SingleObservation()
    {
        double[] paretoK = [0.5];
        double[] elpdLoo = [-1.0];

        var diagnostics = new InfluenceDiagnostics(paretoK, elpdLoo);

        Assert.AreEqual(1, diagnostics.Count);
        Assert.AreEqual(0.5, diagnostics.MeanParetoK);
        Assert.AreEqual(0.5, diagnostics.MaxParetoK);
    }

    /// <summary>
    /// Verifies <c>Test_VeryLargeParetoK</c>.
    /// </summary>
    [TestMethod]
    public void Test_VeryLargeParetoK()
    {
        double[] paretoK = [0.1, 0.2, 10.0]; // Extremely high k
        double[] elpdLoo = new double[3];

        var diagnostics = new InfluenceDiagnostics(paretoK, elpdLoo);

        Assert.AreEqual(10.0, diagnostics.MaxParetoK);
        Assert.AreEqual(ParetoKCategory.VeryBad, diagnostics[2].Category);
    }

    #endregion
}
