using RMC.BestFit.Models;

namespace RMC.BestFit.Tests.CoreInfrastructure;

/// <summary>
/// Unit tests for the <c>DataComponent</c> readonly struct.
/// Tests constructor behavior, property access, and edge cases.
/// </summary>
[TestClass]
public class DataComponentTests
{
    #region Constructor Tests

    /// <summary>Verifies that simple constructor sets properties correctly.</summary>
    [TestMethod]
    public void Test_SimpleConstructor_SetsPropertiesCorrectly()
    {
        // Arrange & Act
        var component = new DataComponent(0, -2.5, 100.0, "Test");

        // Assert
        Assert.AreEqual(0, component.Index);
        Assert.AreEqual(-2.5, component.LogLikelihood);
        Assert.AreEqual(100.0, component.Value);
        Assert.AreEqual("Test", component.Name);
        Assert.AreEqual(DataComponentType.Exact, component.Type);
        Assert.AreEqual(1, component.Count);
    }

    /// <summary>Verifies that simple constructor null name allowed.</summary>
    [TestMethod]
    public void Test_SimpleConstructor_NullName_Allowed()
    {
        // Arrange & Act
        var component = new DataComponent(0, -1.0, 50.0);

        // Assert
        Assert.IsNull(component.Name);
        Assert.AreEqual(DataComponentType.Exact, component.Type);
    }

    /// <summary>Verifies that full constructor sets all properties.</summary>
    [TestMethod]
    public void Test_FullConstructor_SetsAllProperties()
    {
        // Arrange & Act
        var component = new DataComponent(5, -3.0, 75.0, DataComponentType.LeftCensored, 10, "1900-1950");

        // Assert
        Assert.AreEqual(5, component.Index);
        Assert.AreEqual(-3.0, component.LogLikelihood);
        Assert.AreEqual(75.0, component.Value);
        Assert.AreEqual(DataComponentType.LeftCensored, component.Type);
        Assert.AreEqual(10, component.Count);
        Assert.AreEqual("1900-1950", component.Name);
    }

    /// <summary>Verifies that full constructor interval type.</summary>
    [TestMethod]
    public void Test_FullConstructor_IntervalType()
    {
        // Arrange & Act
        var component = new DataComponent(2, -1.5, 125.0, DataComponentType.Interval, 1, "[100-150]");

        // Assert
        Assert.AreEqual(DataComponentType.Interval, component.Type);
        Assert.AreEqual(125.0, component.Value); // Midpoint
    }

    /// <summary>Verifies that full constructor uncertain type.</summary>
    [TestMethod]
    public void Test_FullConstructor_UncertainType()
    {
        // Arrange & Act
        var component = new DataComponent(3, -2.0, 80.0, DataComponentType.Uncertain, 1, "Mean±5");

        // Assert
        Assert.AreEqual(DataComponentType.Uncertain, component.Type);
        Assert.AreEqual(80.0, component.Value); // Mean of uncertainty
    }

    #endregion

    #region IsCensored Tests

    /// <summary>Verifies that is censored returns false when exact type.</summary>
    [TestMethod]
    public void Test_IsCensored_ExactType_ReturnsFalse()
    {
        var component = new DataComponent(0, -1.0, 100.0, DataComponentType.Exact, 1);
        Assert.IsFalse(component.IsCensored);
    }

    /// <summary>Verifies that is censored returns false when uncertain type.</summary>
    [TestMethod]
    public void Test_IsCensored_UncertainType_ReturnsFalse()
    {
        var component = new DataComponent(0, -1.0, 100.0, DataComponentType.Uncertain, 1);
        Assert.IsFalse(component.IsCensored);
    }

    /// <summary>Verifies that is censored returns false when interval type.</summary>
    [TestMethod]
    public void Test_IsCensored_IntervalType_ReturnsFalse()
    {
        var component = new DataComponent(0, -1.0, 100.0, DataComponentType.Interval, 1);
        Assert.IsFalse(component.IsCensored);
    }

    /// <summary>Verifies that is censored returns true when left censored type.</summary>
    [TestMethod]
    public void Test_IsCensored_LeftCensoredType_ReturnsTrue()
    {
        var component = new DataComponent(0, -1.0, 100.0, DataComponentType.LeftCensored, 5);
        Assert.IsTrue(component.IsCensored);
    }

    /// <summary>Verifies that is censored returns true when right censored type.</summary>
    [TestMethod]
    public void Test_IsCensored_RightCensoredType_ReturnsTrue()
    {
        var component = new DataComponent(0, -1.0, 100.0, DataComponentType.RightCensored, 3);
        Assert.IsTrue(component.IsCensored);
    }

    #endregion

    #region IsThreshold Tests

    /// <summary>Verifies that is threshold returns true when censored with count greater than1.</summary>
    [TestMethod]
    public void Test_IsThreshold_CensoredWithCountGreaterThan1_ReturnsTrue()
    {
        var component = new DataComponent(0, -1.0, 50.0, DataComponentType.LeftCensored, 10);
        Assert.IsTrue(component.IsThreshold);
    }

    /// <summary>Verifies that is threshold returns false when censored with count equal1.</summary>
    [TestMethod]
    public void Test_IsThreshold_CensoredWithCountEqual1_ReturnsFalse()
    {
        var component = new DataComponent(0, -1.0, 50.0, DataComponentType.LeftCensored, 1);
        Assert.IsFalse(component.IsThreshold);
    }

    /// <summary>Verifies that is threshold returns false when not censored with high count.</summary>
    [TestMethod]
    public void Test_IsThreshold_NotCensoredWithHighCount_ReturnsFalse()
    {
        // Even with Count > 1, non-censored types shouldn't be thresholds
        var component = new DataComponent(0, -1.0, 50.0, DataComponentType.Exact, 10);
        Assert.IsFalse(component.IsThreshold);
    }

    #endregion

    #region ToString Tests

    /// <summary>Verifies that to string exact type formats correctly.</summary>
    [TestMethod]
    public void Test_ToString_ExactType_FormatsCorrectly()
    {
        var component = new DataComponent(0, -2.5, 100.0, DataComponentType.Exact, 1, "Obs1");
        string result = component.ToString();
        Assert.IsTrue(result.Contains("Obs1"));
        Assert.IsTrue(result.Contains("100"));
        Assert.IsTrue(result.Contains("-2.5"));
    }

    /// <summary>Verifies that to string uncertain type includes tilde.</summary>
    [TestMethod]
    public void Test_ToString_UncertainType_IncludesTilde()
    {
        var component = new DataComponent(0, -2.0, 85.0, DataComponentType.Uncertain, 1, "Meas1");
        string result = component.ToString();
        Assert.IsTrue(result.Contains("~"));
    }

    /// <summary>Verifies that to string interval type includes brackets.</summary>
    [TestMethod]
    public void Test_ToString_IntervalType_IncludesBrackets()
    {
        var component = new DataComponent(0, -1.5, 75.0, DataComponentType.Interval, 1, "Int1");
        string result = component.ToString();
        Assert.IsTrue(result.Contains("[") && result.Contains("]"));
    }

    /// <summary>Verifies that to string left censored includes less than.</summary>
    [TestMethod]
    public void Test_ToString_LeftCensored_IncludesLessThan()
    {
        var component = new DataComponent(0, -3.0, 50.0, DataComponentType.LeftCensored, 15, "1900-1950");
        string result = component.ToString();
        Assert.IsTrue(result.Contains("<"));
        Assert.IsTrue(result.Contains("n=15"));
    }

    /// <summary>Verifies that to string right censored includes greater than.</summary>
    [TestMethod]
    public void Test_ToString_RightCensored_IncludesGreaterThan()
    {
        var component = new DataComponent(0, -2.0, 200.0, DataComponentType.RightCensored, 5, "1980-2000");
        string result = component.ToString();
        Assert.IsTrue(result.Contains(">"));
        Assert.IsTrue(result.Contains("n=5"));
    }

    /// <summary>Verifies that to string no name uses index.</summary>
    [TestMethod]
    public void Test_ToString_NoName_UsesIndex()
    {
        var component = new DataComponent(7, -1.0, 50.0);
        string result = component.ToString();
        Assert.IsTrue(result.Contains("[7]"));
    }

    #endregion

    #region Edge Case Tests

    /// <summary>Verifies that negative index allowed.</summary>
    [TestMethod]
    public void Test_NegativeIndex_Allowed()
    {
        // Edge case: negative index (shouldn't happen but struct allows it)
        var component = new DataComponent(-1, -1.0, 100.0);
        Assert.AreEqual(-1, component.Index);
    }

    /// <summary>Verifies that zero log likelihood allowed.</summary>
    [TestMethod]
    public void Test_ZeroLogLikelihood_Allowed()
    {
        var component = new DataComponent(0, 0.0, 100.0);
        Assert.AreEqual(0.0, component.LogLikelihood);
    }

    /// <summary>Verifies that positive log likelihood allowed.</summary>
    [TestMethod]
    public void Test_PositiveLogLikelihood_Allowed()
    {
        // While unusual, positive log-likelihoods can occur
        var component = new DataComponent(0, 0.5, 100.0);
        Assert.AreEqual(0.5, component.LogLikelihood);
    }

    /// <summary>Verifies that negative infinity log likelihood.</summary>
    [TestMethod]
    public void Test_NegativeInfinityLogLikelihood()
    {
        var component = new DataComponent(0, double.NegativeInfinity, 100.0);
        Assert.AreEqual(double.NegativeInfinity, component.LogLikelihood);
    }

    /// <summary>Verifies that na n log likelihood.</summary>
    [TestMethod]
    public void Test_NaNLogLikelihood()
    {
        var component = new DataComponent(0, double.NaN, 100.0);
        Assert.IsTrue(double.IsNaN(component.LogLikelihood));
    }

    /// <summary>Verifies that negative value allowed.</summary>
    [TestMethod]
    public void Test_NegativeValue_Allowed()
    {
        var component = new DataComponent(0, -1.0, -50.0);
        Assert.AreEqual(-50.0, component.Value);
    }

    /// <summary>Verifies that zero count allowed.</summary>
    [TestMethod]
    public void Test_ZeroCount_Allowed()
    {
        var component = new DataComponent(0, -1.0, 50.0, DataComponentType.LeftCensored, 0);
        Assert.AreEqual(0, component.Count);
        Assert.IsFalse(component.IsThreshold); // Count must be > 1 for threshold
    }

    /// <summary>Verifies that empty name treated as null.</summary>
    [TestMethod]
    public void Test_EmptyName_TreatedAsNull()
    {
        var component = new DataComponent(0, -1.0, 50.0, "");
        string result = component.ToString();
        // Empty string is treated as null via string.IsNullOrEmpty, so it uses index format
        Assert.IsTrue(result.Contains("[0]"));
    }

    #endregion

    #region Value Equality Tests (Struct Behavior)

    /// <summary>Verifies that struct value equality.</summary>
    [TestMethod]
    public void Test_StructValueEquality()
    {
        var component1 = new DataComponent(0, -1.0, 100.0, DataComponentType.Exact, 1, "Test");
        var component2 = new DataComponent(0, -1.0, 100.0, DataComponentType.Exact, 1, "Test");

        // Structs with same values should be equal
        Assert.AreEqual(component1.Index, component2.Index);
        Assert.AreEqual(component1.LogLikelihood, component2.LogLikelihood);
        Assert.AreEqual(component1.Value, component2.Value);
        Assert.AreEqual(component1.Type, component2.Type);
        Assert.AreEqual(component1.Count, component2.Count);
        Assert.AreEqual(component1.Name, component2.Name);
    }

    /// <summary>Verifies that default struct values.</summary>
    [TestMethod]
    public void Test_DefaultStructValues()
    {
        // Default struct should have default values
        DataComponent defaultComponent = default;

        Assert.AreEqual(0, defaultComponent.Index);
        Assert.AreEqual(0.0, defaultComponent.LogLikelihood);
        Assert.AreEqual(0.0, defaultComponent.Value);
        Assert.AreEqual(DataComponentType.Exact, defaultComponent.Type); // Enum default is 0 = Exact
        Assert.AreEqual(0, defaultComponent.Count);
        Assert.IsNull(defaultComponent.Name);
    }

    #endregion
}
