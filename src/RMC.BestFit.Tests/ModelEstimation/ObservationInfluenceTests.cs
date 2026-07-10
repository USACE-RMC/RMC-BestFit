using RMC.BestFit.Diagnostics;
using RMC.BestFit.Models;
using System.Xml.Linq;

namespace RMC.BestFit.Tests.ModelEstimation;

/// <summary>
/// Unit tests for the <c>ObservationInfluence</c> readonly struct.
/// Tests construction, category classification, serialization, and edge cases.
/// </summary>
[TestClass]
public class ObservationInfluenceTests
{
    #region Constructor Tests

    /// <summary>Verifies that constructor minimal parameters.</summary>
    [TestMethod]
    public void Test_Constructor_MinimalParameters()
    {
        var obs = new ObservationInfluence(0, 0.3, -2.0);

        Assert.AreEqual(0, obs.Index);
        Assert.AreEqual(0.3, obs.ParetoK);
        Assert.AreEqual(-2.0, obs.ElpdLoo);
        Assert.IsTrue(double.IsNaN(obs.Value));
        Assert.AreEqual(DataComponentType.Exact, obs.DataType);
        Assert.AreEqual(1, obs.Count);
        Assert.IsNull(obs.Name);
    }

    /// <summary>Verifies that constructor all parameters.</summary>
    [TestMethod]
    public void Test_Constructor_AllParameters()
    {
        var obs = new ObservationInfluence(
            index: 5,
            paretoK: 0.65,
            elpdLoo: -3.5,
            value: 150.0,
            dataType: DataComponentType.LeftCensored,
            count: 10,
            name: "1900-1950"
        );

        Assert.AreEqual(5, obs.Index);
        Assert.AreEqual(0.65, obs.ParetoK);
        Assert.AreEqual(-3.5, obs.ElpdLoo);
        Assert.AreEqual(150.0, obs.Value);
        Assert.AreEqual(DataComponentType.LeftCensored, obs.DataType);
        Assert.AreEqual(10, obs.Count);
        Assert.AreEqual("1900-1950", obs.Name);
    }

    #endregion

    #region Category Tests

    /// <summary>Verifies that category good.</summary>
    [TestMethod]
    public void Test_Category_Good()
    {
        var obs = new ObservationInfluence(0, 0.0, -1.0);
        Assert.AreEqual(ParetoKCategory.Good, obs.Category);

        obs = new ObservationInfluence(0, 0.49, -1.0);
        Assert.AreEqual(ParetoKCategory.Good, obs.Category);
    }

    /// <summary>Verifies that category OK.</summary>
    [TestMethod]
    public void Test_Category_OK()
    {
        var obs = new ObservationInfluence(0, 0.5, -1.0);
        Assert.AreEqual(ParetoKCategory.OK, obs.Category);

        obs = new ObservationInfluence(0, 0.69, -1.0);
        Assert.AreEqual(ParetoKCategory.OK, obs.Category);
    }

    /// <summary>Verifies that category bad.</summary>
    [TestMethod]
    public void Test_Category_Bad()
    {
        var obs = new ObservationInfluence(0, 0.7, -1.0);
        Assert.AreEqual(ParetoKCategory.Bad, obs.Category);

        obs = new ObservationInfluence(0, 0.99, -1.0);
        Assert.AreEqual(ParetoKCategory.Bad, obs.Category);
    }

    /// <summary>Verifies that category very bad.</summary>
    [TestMethod]
    public void Test_Category_VeryBad()
    {
        var obs = new ObservationInfluence(0, 1.0, -1.0);
        Assert.AreEqual(ParetoKCategory.VeryBad, obs.Category);

        obs = new ObservationInfluence(0, 2.5, -1.0);
        Assert.AreEqual(ParetoKCategory.VeryBad, obs.Category);
    }

    /// <summary>Verifies that category negative pareto k.</summary>
    [TestMethod]
    public void Test_Category_NegativeParetoK()
    {
        // Negative k should be Good
        var obs = new ObservationInfluence(0, -0.5, -1.0);
        Assert.AreEqual(ParetoKCategory.Good, obs.Category);
    }

    #endregion

    #region ToString Tests

    /// <summary>Verifies that to string with name.</summary>
    [TestMethod]
    public void Test_ToString_WithName()
    {
        var obs = new ObservationInfluence(0, 0.45, -2.5, 100.0, name: "Obs1");
        string result = obs.ToString();

        Assert.IsTrue(result.Contains("Obs1"));
        Assert.IsTrue(result.Contains("0.450"));
        Assert.IsTrue(result.Contains("Good"));
        Assert.IsTrue(result.Contains("-2.5"));
    }

    /// <summary>Verifies that to string without name uses index.</summary>
    [TestMethod]
    public void Test_ToString_WithoutName_UsesIndex()
    {
        var obs = new ObservationInfluence(7, 0.65, -1.5, 85.0);
        string result = obs.ToString();

        Assert.IsTrue(result.Contains("[7]"));
        Assert.IsTrue(result.Contains("OK"));
    }

    /// <summary>Verifies that to string includes category.</summary>
    [TestMethod]
    public void Test_ToString_IncludesCategory()
    {
        var badObs = new ObservationInfluence(0, 0.85, -3.0);
        Assert.IsTrue(badObs.ToString().Contains("Bad"));

        var veryBadObs = new ObservationInfluence(0, 1.5, -5.0);
        Assert.IsTrue(veryBadObs.ToString().Contains("VeryBad"));
    }

    #endregion

    #region Serialization Tests

    /// <summary>Verifies that to X element contains all attributes.</summary>
    [TestMethod]
    public void Test_ToXElement_ContainsAllAttributes()
    {
        var obs = new ObservationInfluence(3, 0.55, -2.5, 125.0, DataComponentType.Uncertain, 1, "Meas1");
        var xElement = obs.ToXElement();

        Assert.AreEqual("Observation", xElement.Name.LocalName);
        Assert.AreEqual("3", xElement.Attribute("Index")?.Value);
        Assert.IsNotNull(xElement.Attribute("ParetoK"));
        Assert.IsNotNull(xElement.Attribute("ElpdLoo"));
        Assert.IsNotNull(xElement.Attribute("Value"));
        Assert.AreEqual("Uncertain", xElement.Attribute("DataType")?.Value);
        Assert.AreEqual("1", xElement.Attribute("Count")?.Value);
        Assert.AreEqual("Meas1", xElement.Attribute("Name")?.Value);
    }

    /// <summary>Verifies that to X element no name attribute when null.</summary>
    [TestMethod]
    public void Test_ToXElement_NoNameAttribute_WhenNull()
    {
        var obs = new ObservationInfluence(0, 0.3, -1.0, 50.0);
        var xElement = obs.ToXElement();

        Assert.IsNull(xElement.Attribute("Name"));
    }

    /// <summary>Verifies that to X element no name attribute when empty.</summary>
    [TestMethod]
    public void Test_ToXElement_NoNameAttribute_WhenEmpty()
    {
        var obs = new ObservationInfluence(0, 0.3, -1.0, 50.0, name: "");
        var xElement = obs.ToXElement();

        Assert.IsNull(xElement.Attribute("Name"));
    }

    /// <summary>Verifies that from X element restores all properties.</summary>
    [TestMethod]
    public void Test_FromXElement_RestoresAllProperties()
    {
        var original = new ObservationInfluence(5, 0.72, -4.0, 200.0, DataComponentType.RightCensored, 3, "Post2000");
        var xElement = original.ToXElement();
        var restored = new ObservationInfluence(xElement);

        Assert.AreEqual(original.Index, restored.Index);
        Assert.AreEqual(original.ParetoK, restored.ParetoK, 1e-10);
        Assert.AreEqual(original.ElpdLoo, restored.ElpdLoo, 1e-10);
        Assert.AreEqual(original.Value, restored.Value, 1e-10);
        Assert.AreEqual(original.DataType, restored.DataType);
        Assert.AreEqual(original.Count, restored.Count);
        Assert.AreEqual(original.Name, restored.Name);
    }

    /// <summary>Verifies that from X element missing data type defaults to exact.</summary>
    [TestMethod]
    public void Test_FromXElement_MissingDataType_DefaultsToExact()
    {
        var xElement = new XElement("Observation",
            new XAttribute("Index", "0"),
            new XAttribute("ParetoK", "0.5"),
            new XAttribute("ElpdLoo", "-1.0"),
            new XAttribute("Value", "100")
        );

        var obs = new ObservationInfluence(xElement);

        Assert.AreEqual(DataComponentType.Exact, obs.DataType);
    }

    /// <summary>Verifies that from X element zero count sets to one.</summary>
    [TestMethod]
    public void Test_FromXElement_ZeroCount_SetsToOne()
    {
        var xElement = new XElement("Observation",
            new XAttribute("Index", "0"),
            new XAttribute("ParetoK", "0.5"),
            new XAttribute("ElpdLoo", "-1.0"),
            new XAttribute("Value", "100"),
            new XAttribute("Count", "0")
        );

        var obs = new ObservationInfluence(xElement);

        Assert.AreEqual(1, obs.Count);
    }

    #endregion

    #region Edge Case Tests

    /// <summary>Verifies that default struct has default values.</summary>
    [TestMethod]
    public void Test_DefaultStruct_HasDefaultValues()
    {
        ObservationInfluence defaultObs = default;

        Assert.AreEqual(0, defaultObs.Index);
        Assert.AreEqual(0.0, defaultObs.ParetoK);
        Assert.AreEqual(0.0, defaultObs.ElpdLoo);
        Assert.AreEqual(0.0, defaultObs.Value);
        Assert.AreEqual(DataComponentType.Exact, defaultObs.DataType);
        Assert.AreEqual(0, defaultObs.Count);
        Assert.IsNull(defaultObs.Name);
    }

    /// <summary>Verifies that negative index allowed.</summary>
    [TestMethod]
    public void Test_NegativeIndex_Allowed()
    {
        var obs = new ObservationInfluence(-1, 0.3, -1.0);
        Assert.AreEqual(-1, obs.Index);
    }

    /// <summary>Verifies that special double values.</summary>
    [TestMethod]
    public void Test_SpecialDoubleValues()
    {
        var obsNaN = new ObservationInfluence(0, double.NaN, double.NaN, double.NaN);
        Assert.IsTrue(double.IsNaN(obsNaN.ParetoK));
        Assert.IsTrue(double.IsNaN(obsNaN.ElpdLoo));
        Assert.IsTrue(double.IsNaN(obsNaN.Value));

        var obsInf = new ObservationInfluence(0, double.PositiveInfinity, double.NegativeInfinity);
        Assert.IsTrue(double.IsPositiveInfinity(obsInf.ParetoK));
        Assert.IsTrue(double.IsNegativeInfinity(obsInf.ElpdLoo));
    }

    /// <summary>Verifies that large count.</summary>
    [TestMethod]
    public void Test_LargeCount()
    {
        var obs = new ObservationInfluence(0, 0.3, -1.0, 50.0, DataComponentType.LeftCensored, 1000000);
        Assert.AreEqual(1000000, obs.Count);
    }

    /// <summary>Verifies that all data component types.</summary>
    [TestMethod]
    public void Test_AllDataComponentTypes()
    {
        var types = new[]
        {
            DataComponentType.Exact,
            DataComponentType.Uncertain,
            DataComponentType.Interval,
            DataComponentType.LeftCensored,
            DataComponentType.RightCensored
        };

        foreach (var type in types)
        {
            var obs = new ObservationInfluence(0, 0.3, -1.0, 100.0, type);
            Assert.AreEqual(type, obs.DataType);
        }
    }

    #endregion

    #region Value Semantics Tests

    /// <summary>Verifies that struct value semantics.</summary>
    [TestMethod]
    public void Test_StructValueSemantics()
    {
        var obs1 = new ObservationInfluence(0, 0.5, -2.0, 100.0, name: "Test");
        var obs2 = obs1; // Copy

        Assert.AreEqual(obs1.Index, obs2.Index);
        Assert.AreEqual(obs1.ParetoK, obs2.ParetoK);
        Assert.AreEqual(obs1.ElpdLoo, obs2.ElpdLoo);
        Assert.AreEqual(obs1.Value, obs2.Value);
        Assert.AreEqual(obs1.Name, obs2.Name);
    }

    #endregion
}
