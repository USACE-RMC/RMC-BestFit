using RMC.BestFit.Models;

namespace RMC.BestFit.Tests.CoreInfrastructure;

/// <summary>
/// Unit tests for the <c>PriorComponent</c> readonly struct.
/// Tests constructor behavior, property access, and edge cases.
/// </summary>
[TestClass]
public class PriorComponentTests
{
    #region Constructor Tests

    /// <summary>Verifies that constructor is parameter prior when default type.</summary>
    [TestMethod]
    public void Test_Constructor_DefaultType_IsParameterPrior()
    {
        // Arrange & Act
        var component = new PriorComponent("Location prior", -2.5);

        // Assert
        Assert.AreEqual("Location prior", component.Name);
        Assert.AreEqual(-2.5, component.LogLikelihood);
        Assert.AreEqual(PriorComponentType.ParameterPrior, component.Type);
    }

    /// <summary>Verifies that constructor with explicit type.</summary>
    [TestMethod]
    public void Test_Constructor_WithExplicitType()
    {
        // Arrange & Act
        var component = new PriorComponent("Scale prior (Jeffreys)", -1.2, PriorComponentType.JeffreysScalePrior);

        // Assert
        Assert.AreEqual("Scale prior (Jeffreys)", component.Name);
        Assert.AreEqual(-1.2, component.LogLikelihood);
        Assert.AreEqual(PriorComponentType.JeffreysScalePrior, component.Type);
    }

    /// <summary>Verifies that constructor quantile prior type.</summary>
    [TestMethod]
    public void Test_Constructor_QuantilePriorType()
    {
        var component = new PriorComponent("Q100 quantile prior", -0.5, PriorComponentType.QuantilePrior);
        Assert.AreEqual(PriorComponentType.QuantilePrior, component.Type);
    }

    /// <summary>Verifies that constructor spatial error type.</summary>
    [TestMethod]
    public void Test_Constructor_SpatialErrorType()
    {
        var component = new PriorComponent("Spatial location error", -3.0, PriorComponentType.SpatialError);
        Assert.AreEqual(PriorComponentType.SpatialError, component.Type);
    }

    /// <summary>Verifies that constructor jacobian type.</summary>
    [TestMethod]
    public void Test_Constructor_JacobianType()
    {
        var component = new PriorComponent("Log transform Jacobian", 0.1, PriorComponentType.Jacobian);
        Assert.AreEqual(PriorComponentType.Jacobian, component.Type);
    }

    /// <summary>Verifies that constructor other penalty type.</summary>
    [TestMethod]
    public void Test_Constructor_OtherPenaltyType()
    {
        var component = new PriorComponent("Regularization term", -0.8, PriorComponentType.OtherPenalty);
        Assert.AreEqual(PriorComponentType.OtherPenalty, component.Type);
    }

    #endregion

    #region ToString Tests

    /// <summary>Verifies that to string formats correctly.</summary>
    [TestMethod]
    public void Test_ToString_FormatsCorrectly()
    {
        var component = new PriorComponent("Location prior", -2.5123);
        string result = component.ToString();

        Assert.IsTrue(result.Contains("Location prior"));
        Assert.IsTrue(result.Contains("-2.5123"));
        Assert.IsTrue(result.Contains(":"));
    }

    /// <summary>Verifies that to string zero log likelihood.</summary>
    [TestMethod]
    public void Test_ToString_ZeroLogLikelihood()
    {
        var component = new PriorComponent("Flat prior", 0.0);
        string result = component.ToString();

        Assert.IsTrue(result.Contains("Flat prior"));
        Assert.IsTrue(result.Contains("0.0000"));
    }

    /// <summary>Verifies that to string positive log likelihood.</summary>
    [TestMethod]
    public void Test_ToString_PositiveLogLikelihood()
    {
        var component = new PriorComponent("Jacobian", 1.5);
        string result = component.ToString();

        Assert.IsTrue(result.Contains("1.5000"));
    }

    #endregion

    #region Edge Case Tests

    /// <summary>Verifies that null name allowed.</summary>
    [TestMethod]
    public void Test_NullName_Allowed()
    {
        var component = new PriorComponent(null!, -1.0);
        Assert.IsNull(component.Name);
    }

    /// <summary>Verifies that empty name allowed.</summary>
    [TestMethod]
    public void Test_EmptyName_Allowed()
    {
        var component = new PriorComponent("", -1.0);
        Assert.AreEqual("", component.Name);
    }

    /// <summary>Verifies that negative infinity log likelihood.</summary>
    [TestMethod]
    public void Test_NegativeInfinityLogLikelihood()
    {
        var component = new PriorComponent("Impossible prior", double.NegativeInfinity);
        Assert.AreEqual(double.NegativeInfinity, component.LogLikelihood);
    }

    /// <summary>Verifies that na n log likelihood.</summary>
    [TestMethod]
    public void Test_NaNLogLikelihood()
    {
        var component = new PriorComponent("Invalid prior", double.NaN);
        Assert.IsTrue(double.IsNaN(component.LogLikelihood));
    }

    /// <summary>Verifies that very large negative log likelihood.</summary>
    [TestMethod]
    public void Test_VeryLargeNegativeLogLikelihood()
    {
        var component = new PriorComponent("Very low probability", -1e10);
        Assert.AreEqual(-1e10, component.LogLikelihood);
    }

    /// <summary>Verifies that positive log likelihood allowed.</summary>
    [TestMethod]
    public void Test_PositiveLogLikelihood_Allowed()
    {
        // Log-densities can be positive (e.g., for concentrated priors)
        var component = new PriorComponent("Concentrated prior", 5.0);
        Assert.AreEqual(5.0, component.LogLikelihood);
    }

    #endregion

    #region Struct Behavior Tests

    /// <summary>Verifies that default struct has default values.</summary>
    [TestMethod]
    public void Test_DefaultStruct_HasDefaultValues()
    {
        PriorComponent defaultComponent = default;

        Assert.IsNull(defaultComponent.Name);
        Assert.AreEqual(0.0, defaultComponent.LogLikelihood);
        Assert.AreEqual(PriorComponentType.ParameterPrior, defaultComponent.Type);
    }

    /// <summary>Verifies that struct value semantics.</summary>
    [TestMethod]
    public void Test_StructValueSemantics()
    {
        var component1 = new PriorComponent("Prior", -1.0, PriorComponentType.ParameterPrior);
        var component2 = component1; // Copy

        // Both should have same values (struct copy)
        Assert.AreEqual(component1.Name, component2.Name);
        Assert.AreEqual(component1.LogLikelihood, component2.LogLikelihood);
        Assert.AreEqual(component1.Type, component2.Type);
    }

    #endregion

    #region Enum Coverage Tests

    /// <summary>Verifies that all prior component types.</summary>
    [TestMethod]
    public void Test_AllPriorComponentTypes()
    {
        // Test that all enum values can be used
        var types = new[]
        {
            PriorComponentType.ParameterPrior,
            PriorComponentType.QuantilePrior,
            PriorComponentType.JeffreysScalePrior,
            PriorComponentType.SpatialError,
            PriorComponentType.Jacobian,
            PriorComponentType.OtherPenalty
        };

        foreach (var type in types)
        {
            var component = new PriorComponent($"Test {type}", -1.0, type);
            Assert.AreEqual(type, component.Type);
        }
    }

    #endregion
}
