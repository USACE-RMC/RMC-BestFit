using Numerics.Distributions;
using RMC.BestFit.Models.TrendFunctions;
using RMC.BestFit.Models.TrendFunctions.Support;

namespace RMC.BestFit.Tests.TrendFunctions;

/// <summary>
/// Unit tests for the <see cref="GeneralLinearFunction"/> class.
/// Tests the general linear model f(x) = β₀ + β₁x₁ + β₂x₂ + ... + βₚxₚ.
/// </summary>
[TestClass]
public class GeneralLinearFunctionTests
{
    #region Constructor Tests

    /// <summary>Verifies that constructor empty constructor creates intercept only model.</summary>
    [TestMethod]
    public void Test_Constructor_EmptyConstructor_CreatesInterceptOnlyModel()
    {
        // Act
        var model = new GeneralLinearFunction();

        // Assert
        Assert.IsNotNull(model);
        Assert.AreEqual(TrendModelType.GeneralLinear, model.Type);
        Assert.AreEqual(1, model.NumberOfParameters); // Just intercept
        Assert.AreEqual(0, model.NumberOfCovariates);
    }

    /// <summary>Verifies that constructor with owner name sets owner name.</summary>
    [TestMethod]
    public void Test_Constructor_WithOwnerName_SetsOwnerName()
    {
        // Act
        var model = new GeneralLinearFunction("Location");

        // Assert
        Assert.AreEqual("Location", model.OwnerName);
        Assert.AreEqual(1, model.NumberOfParameters);
    }

    /// <summary>Verifies that constructor with covariates creates correct parameters.</summary>
    [TestMethod]
    public void Test_Constructor_WithCovariates_CreatesCorrectParameters()
    {
        // Arrange - 3 sites, 2 covariates
        double[,] covariates = new double[,]
        {
            { 100.0, 0.5 },  // Site 1: elevation=100, latitude=0.5
            { 200.0, 0.6 },  // Site 2
            { 150.0, 0.55 }  // Site 3
        };

        // Act
        var model = new GeneralLinearFunction("Location", covariates);

        // Assert: intercept + 2 coefficients = 3 parameters
        Assert.AreEqual(3, model.NumberOfParameters);
        Assert.AreEqual(2, model.NumberOfCovariates);
        Assert.AreEqual(3, model.NumberOfObservations);
    }

    /// <summary>Verifies that constructor throws when null owner name.</summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Test_Constructor_NullOwnerName_Throws()
    {
        var model = new GeneralLinearFunction(null!);
    }

    /// <summary>Verifies that constructor X element restores model.</summary>
    [TestMethod]
    public void Test_Constructor_XElement_RestoresModel()
    {
        // Arrange
        double[,] covariates = new double[,] { { 1.0, 2.0 }, { 3.0, 4.0 } };
        var original = new GeneralLinearFunction("Location", covariates);
        original.Parameters[0].Value = 100.0;
        original.Parameters[1].Value = 0.5;
        original.Parameters[2].Value = -0.3;
        var xElement = original.ToXElement();

        // Act
        var restored = new GeneralLinearFunction(xElement);

        // Assert
        Assert.AreEqual("Location", restored.OwnerName);
        Assert.AreEqual(3, restored.NumberOfParameters);
        Assert.AreEqual(2, restored.NumberOfCovariates);
        Assert.AreEqual(100.0, restored.Parameters[0].Value, 1e-10);
        Assert.AreEqual(0.5, restored.Parameters[1].Value, 1e-10);
        Assert.AreEqual(-0.3, restored.Parameters[2].Value, 1e-10);
    }

    /// <summary>Verifies that constructor X element null throws.</summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Test_Constructor_XElement_NullThrows()
    {
        var model = new GeneralLinearFunction(null!);
    }

    #endregion

    #region Type Tests

    /// <summary>Verifies that type returns general linear.</summary>
    [TestMethod]
    public void Test_Type_ReturnsGeneralLinear()
    {
        var model = new GeneralLinearFunction();
        Assert.AreEqual(TrendModelType.GeneralLinear, model.Type);
    }

    #endregion

    #region SetDefaultParameters Tests

    /// <summary>Verifies that set default parameters intercept only.</summary>
    [TestMethod]
    public void Test_SetDefaultParameters_InterceptOnly()
    {
        var model = new GeneralLinearFunction();

        Assert.AreEqual(1, model.Parameters.Count);
        Assert.IsTrue(model.Parameters[0].Name.Contains("β₀") || model.Parameters[0].Name.Contains("β0"));
    }

    /// <summary>Verifies that set default parameters with covariates.</summary>
    [TestMethod]
    public void Test_SetDefaultParameters_WithCovariates()
    {
        double[,] covariates = new double[,] { { 1.0, 2.0, 3.0 } }; // 3 covariates
        var model = new GeneralLinearFunction("Scale", covariates);

        // 4 parameters: intercept + 3 coefficients
        Assert.AreEqual(4, model.Parameters.Count);
    }

    /// <summary>Verifies that set default parameters coefficient bounds.</summary>
    [TestMethod]
    public void Test_SetDefaultParameters_CoefficientBounds()
    {
        double[,] covariates = new double[,] { { 1.0 } };
        var model = new GeneralLinearFunction("Location", covariates);

        // Coefficient (not intercept) should have [-1, 1] bounds
        Assert.AreEqual(-1.0, model.Parameters[1].LowerBound);
        Assert.AreEqual(1.0, model.Parameters[1].UpperBound);
    }

    /// <summary>Verifies that set default parameters coefficient prior.</summary>
    [TestMethod]
    public void Test_SetDefaultParameters_CoefficientPrior()
    {
        double[,] covariates = new double[,] { { 1.0 } };
        var model = new GeneralLinearFunction("Location", covariates);

        Assert.IsInstanceOfType(model.Parameters[1].PriorDistribution, typeof(Uniform));
    }

    #endregion

    #region Covariates Property Tests

    /// <summary>Verifies that covariates set and get.</summary>
    [TestMethod]
    public void Test_Covariates_SetAndGet()
    {
        var model = new GeneralLinearFunction();
        double[,] covariates = new double[,] { { 1.0, 2.0 }, { 3.0, 4.0 } };

        model.Covariates = covariates;

        Assert.AreEqual(2, model.NumberOfCovariates);
        Assert.AreEqual(2, model.NumberOfObservations);
        // Parameters should be updated (intercept + 2 coefficients)
        Assert.AreEqual(3, model.NumberOfParameters);
    }

    /// <summary>Verifies that covariates set null resets to intercept only.</summary>
    [TestMethod]
    public void Test_Covariates_SetNull_ResetsToInterceptOnly()
    {
        double[,] covariates = new double[,] { { 1.0, 2.0 } };
        var model = new GeneralLinearFunction("Test", covariates);
        Assert.AreEqual(2, model.NumberOfCovariates);

        model.Covariates = null;

        Assert.AreEqual(0, model.NumberOfCovariates);
        Assert.AreEqual(1, model.NumberOfParameters);
    }

    /// <summary>Verifies that number of observations returns zero when no covariates.</summary>
    [TestMethod]
    public void Test_NumberOfObservations_NoCovariates_ReturnsZero()
    {
        var model = new GeneralLinearFunction();
        Assert.AreEqual(0, model.NumberOfObservations);
    }

    #endregion

    #region Predict Tests

    /// <summary>Verifies that predict returns intercept when intercept only.</summary>
    [TestMethod]
    public void Test_Predict_InterceptOnly_ReturnsIntercept()
    {
        var model = new GeneralLinearFunction();
        model.Parameters[0].Value = 50.0;

        Assert.AreEqual(50.0, model.Predict(0), 1e-10);
    }

    /// <summary>Verifies that predict returns linear combination when with covariates.</summary>
    [TestMethod]
    public void Test_Predict_WithCovariates_ReturnsLinearCombination()
    {
        // Arrange: 2 sites, 2 covariates
        double[,] covariates = new double[,]
        {
            { 10.0, 5.0 },   // Site 0
            { 20.0, 10.0 }   // Site 1
        };
        var model = new GeneralLinearFunction("Location", covariates);
        model.Parameters[0].Value = 100.0;  // β₀ (intercept)
        model.Parameters[1].Value = 0.5;    // β₁
        model.Parameters[2].Value = 2.0;    // β₂

        // y(0) = 100 + 0.5 * 10 + 2.0 * 5 = 100 + 5 + 10 = 115
        Assert.AreEqual(115.0, model.Predict(0), 1e-10);

        // y(1) = 100 + 0.5 * 20 + 2.0 * 10 = 100 + 10 + 20 = 130
        Assert.AreEqual(130.0, model.Predict(1), 1e-10);
    }

    /// <summary>Verifies that predict throws when index out of range.</summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void Test_Predict_IndexOutOfRange_Throws()
    {
        double[,] covariates = new double[,] { { 1.0 }, { 2.0 } };
        var model = new GeneralLinearFunction("Test", covariates);

        model.Predict(5); // Index 5 doesn't exist
    }

    /// <summary>Verifies that predict throws when negative index.</summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void Test_Predict_NegativeIndex_Throws()
    {
        double[,] covariates = new double[,] { { 1.0 } };
        var model = new GeneralLinearFunction("Test", covariates);

        model.Predict(-1);
    }

    #endregion

    #region PredictWithCovariates Tests

    /// <summary>Verifies that predict with covariates returns intercept when no covariates model.</summary>
    [TestMethod]
    public void Test_PredictWithCovariates_NoCovariatesModel_ReturnsIntercept()
    {
        var model = new GeneralLinearFunction();
        model.Parameters[0].Value = 75.0;

        Assert.AreEqual(75.0, model.PredictWithCovariates(null), 1e-10);
        Assert.AreEqual(75.0, model.PredictWithCovariates(Array.Empty<double>()), 1e-10);
    }

    /// <summary>Verifies that predict with covariates correct linear combination.</summary>
    [TestMethod]
    public void Test_PredictWithCovariates_CorrectLinearCombination()
    {
        double[,] covariates = new double[,] { { 1.0, 2.0 } }; // Define structure
        var model = new GeneralLinearFunction("Location", covariates);
        model.Parameters[0].Value = 10.0;   // β₀
        model.Parameters[1].Value = 2.0;    // β₁
        model.Parameters[2].Value = 3.0;    // β₂

        // Predict at ungauged site with covariates [5.0, 4.0]
        double[] newCovariates = { 5.0, 4.0 };
        double result = model.PredictWithCovariates(newCovariates);

        // y = 10 + 2*5 + 3*4 = 10 + 10 + 12 = 32
        Assert.AreEqual(32.0, result, 1e-10);
    }

    /// <summary>Verifies that predict with covariates throws when wrong length.</summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void Test_PredictWithCovariates_WrongLength_Throws()
    {
        double[,] covariates = new double[,] { { 1.0, 2.0 } }; // 2 covariates
        var model = new GeneralLinearFunction("Location", covariates);

        // Pass only 1 covariate when 2 expected
        model.PredictWithCovariates(new double[] { 5.0 });
    }

    /// <summary>Verifies that predict with covariates returns intercept when null or empty.</summary>
    [TestMethod]
    public void Test_PredictWithCovariates_NullOrEmpty_ReturnsIntercept()
    {
        double[,] covariates = new double[,] { { 1.0 } }; // 1 covariate defined
        var model = new GeneralLinearFunction("Location", covariates);
        model.Parameters[0].Value = 100.0;

        // Null or empty returns intercept (design decision to be lenient)
        // Note: This behavior may throw in strict mode
        double result = model.PredictWithCovariates(null);
        Assert.AreEqual(100.0, result, 1e-10);
    }

    /// <summary>Verifies that predict with covariates spatial prediction.</summary>
    [TestMethod]
    public void Test_PredictWithCovariates_SpatialPrediction()
    {
        // Simulate spatial prediction scenario
        // Sites have elevation and latitude
        double[,] gaugeSites = new double[,]
        {
            { 500.0, 35.0 },   // Site 1
            { 1000.0, 36.0 },  // Site 2
            { 750.0, 35.5 }    // Site 3
        };

        var model = new GeneralLinearFunction("Location", gaugeSites);
        model.Parameters[0].Value = 50.0;   // Intercept
        model.Parameters[1].Value = 0.01;   // Elevation effect
        model.Parameters[2].Value = 1.0;    // Latitude effect

        // Predict at ungauged site
        double[] ungaugedSite = { 800.0, 35.8 };
        double prediction = model.PredictWithCovariates(ungaugedSite);

        // y = 50 + 0.01*800 + 1.0*35.8 = 50 + 8 + 35.8 = 93.8
        Assert.AreEqual(93.8, prediction, 1e-10);
    }

    #endregion

    #region Clone Tests

    /// <summary>Verifies that clone creates independent copy.</summary>
    [TestMethod]
    public void Test_Clone_CreatesIndependentCopy()
    {
        double[,] covariates = new double[,] { { 1.0, 2.0 }, { 3.0, 4.0 } };
        var original = new GeneralLinearFunction("Location", covariates);
        original.Parameters[0].Value = 100.0;
        original.Parameters[1].Value = 0.5;
        original.Parameters[2].Value = -0.3;

        var clone = (GeneralLinearFunction)original.Clone();

        // Verify values match
        Assert.AreEqual(original.OwnerName, clone.OwnerName);
        Assert.AreEqual(original.NumberOfParameters, clone.NumberOfParameters);
        Assert.AreEqual(original.Parameters[0].Value, clone.Parameters[0].Value);

        // Verify independence
        original.Parameters[0].Value = 999.0;
        Assert.AreEqual(100.0, clone.Parameters[0].Value);
    }

    /// <summary>Verifies that clone clones covariate matrix.</summary>
    [TestMethod]
    public void Test_Clone_ClonesCovariateMatrix()
    {
        double[,] covariates = new double[,] { { 10.0, 20.0 } };
        var original = new GeneralLinearFunction("Test", covariates);

        var clone = (GeneralLinearFunction)original.Clone();

        // Verify covariate matrix is cloned
        Assert.AreEqual(original.NumberOfCovariates, clone.NumberOfCovariates);
        Assert.AreEqual(original.NumberOfObservations, clone.NumberOfObservations);

        // Modifying original covariates shouldn't affect clone
        covariates[0, 0] = 999.0;
        Assert.AreEqual(10.0, clone.Covariates![0, 0]);
    }

    /// <summary>Verifies that clone preserves prediction for .</summary>
    [TestMethod]
    public void Test_Clone_PreservesPrediction()
    {
        double[,] covariates = new double[,] { { 5.0, 3.0 } };
        var original = new GeneralLinearFunction("Location", covariates);
        original.Parameters[0].Value = 10.0;
        original.Parameters[1].Value = 2.0;
        original.Parameters[2].Value = 1.0;

        var clone = (GeneralLinearFunction)original.Clone();

        Assert.AreEqual(original.Predict(0), clone.Predict(0), 1e-10);
    }

    #endregion

    #region Serialization Tests

    /// <summary>Verifies that to X element contains type attribute.</summary>
    [TestMethod]
    public void Test_ToXElement_ContainsTypeAttribute()
    {
        var model = new GeneralLinearFunction();
        var xElement = model.ToXElement();

        Assert.AreEqual("GeneralLinear", xElement.Attribute("Type")?.Value);
    }

    /// <summary>Verifies that to X element contains covariate info.</summary>
    [TestMethod]
    public void Test_ToXElement_ContainsCovariateInfo()
    {
        double[,] covariates = new double[,] { { 1.0, 2.0 }, { 3.0, 4.0 } };
        var model = new GeneralLinearFunction("Location", covariates);

        var xElement = model.ToXElement();

        Assert.AreEqual("2", xElement.Attribute("CovariateRows")?.Value);
        Assert.AreEqual("2", xElement.Attribute("CovariateCols")?.Value);
        Assert.IsNotNull(xElement.Element("Covariates"));
    }

    /// <summary>Verifies that round trip preserves all properties for .</summary>
    [TestMethod]
    public void Test_RoundTrip_PreservesAllProperties()
    {
        double[,] covariates = new double[,] { { 1.5, 2.5 }, { 3.5, 4.5 } };
        var original = new GeneralLinearFunction("Scale", covariates);
        original.StartIndex = 100;
        original.UseDefaultFlatPriors = false;
        original.Parameters[0].Value = Math.PI;
        original.Parameters[1].Value = Math.E;
        original.Parameters[2].Value = 0.123;

        var xElement = original.ToXElement();
        var restored = new GeneralLinearFunction(xElement);

        Assert.AreEqual(original.OwnerName, restored.OwnerName);
        Assert.AreEqual(original.StartIndex, restored.StartIndex);
        Assert.AreEqual(original.UseDefaultFlatPriors, restored.UseDefaultFlatPriors);
        Assert.AreEqual(original.NumberOfCovariates, restored.NumberOfCovariates);
        Assert.AreEqual(original.NumberOfObservations, restored.NumberOfObservations);
        Assert.AreEqual(original.Parameters[0].Value, restored.Parameters[0].Value, 1e-10);
        Assert.AreEqual(original.Parameters[1].Value, restored.Parameters[1].Value, 1e-10);
        Assert.AreEqual(original.Parameters[2].Value, restored.Parameters[2].Value, 1e-10);
    }

    /// <summary>Verifies that round trip preserves covariate matrix for .</summary>
    [TestMethod]
    public void Test_RoundTrip_PreservesCovariateMatrix()
    {
        double[,] covariates = new double[,]
        {
            { 1.0, 2.0, 3.0 },
            { 4.0, 5.0, 6.0 },
            { 7.0, 8.0, 9.0 }
        };
        var original = new GeneralLinearFunction("Test", covariates);

        var xElement = original.ToXElement();
        var restored = new GeneralLinearFunction(xElement);

        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                Assert.AreEqual(covariates[i, j], restored.Covariates![i, j], 1e-10);
            }
        }
    }

    /// <summary>Verifies that round trip intercept only model.</summary>
    [TestMethod]
    public void Test_RoundTrip_InterceptOnlyModel()
    {
        var original = new GeneralLinearFunction();
        original.Parameters[0].Value = 42.0;

        var xElement = original.ToXElement();
        var restored = new GeneralLinearFunction(xElement);

        Assert.AreEqual(1, restored.NumberOfParameters);
        Assert.AreEqual(0, restored.NumberOfCovariates);
        Assert.AreEqual(42.0, restored.Parameters[0].Value, 1e-10);
    }

    #endregion

    #region PropertyChanged Tests

    /// <summary>Verifies that property changed owner name.</summary>
    [TestMethod]
    public void Test_PropertyChanged_OwnerName()
    {
        var model = new GeneralLinearFunction();
        string? changedProperty = null;
        model.PropertyChanged += (s, e) => changedProperty = e.PropertyName;

        model.OwnerName = "NewOwner";

        Assert.AreEqual(nameof(model.OwnerName), changedProperty);
    }

    /// <summary>Verifies that property changed covariates.</summary>
    [TestMethod]
    public void Test_PropertyChanged_Covariates()
    {
        var model = new GeneralLinearFunction();
        var changedProperties = new List<string>();
        model.PropertyChanged += (s, e) => changedProperties.Add(e.PropertyName!);

        model.Covariates = new double[,] { { 1.0 } };

        Assert.IsTrue(changedProperties.Contains(nameof(model.Covariates)));
        Assert.IsTrue(changedProperties.Contains(nameof(model.NumberOfCovariates)));
    }

    #endregion

    #region SetParameterValues Tests

    /// <summary>Verifies that set parameter values updates all parameters.</summary>
    [TestMethod]
    public void Test_SetParameterValues_UpdatesAllParameters()
    {
        double[,] covariates = new double[,] { { 1.0, 2.0 } };
        var model = new GeneralLinearFunction("Test", covariates);

        model.SetParameterValues(new double[] { 10.0, 20.0, 30.0 });

        Assert.AreEqual(10.0, model.Parameters[0].Value);
        Assert.AreEqual(20.0, model.Parameters[1].Value);
        Assert.AreEqual(30.0, model.Parameters[2].Value);
    }

    /// <summary>Verifies that set parameter values null throws.</summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Test_SetParameterValues_NullThrows()
    {
        var model = new GeneralLinearFunction();
        model.SetParameterValues(null!);
    }

    /// <summary>Verifies that set parameter values wrong length throws.</summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void Test_SetParameterValues_WrongLengthThrows()
    {
        double[,] covariates = new double[,] { { 1.0 } };
        var model = new GeneralLinearFunction("Test", covariates);

        // Model has 2 parameters, but we're passing 1
        model.SetParameterValues(new double[] { 10.0 });
    }

    #endregion

    #region Edge Cases

    /// <summary>Verifies that single covariate single observation.</summary>
    [TestMethod]
    public void Test_SingleCovariate_SingleObservation()
    {
        double[,] covariates = new double[,] { { 5.0 } };
        var model = new GeneralLinearFunction("Location", covariates);
        model.Parameters[0].Value = 10.0;
        model.Parameters[1].Value = 2.0;

        // y = 10 + 2 * 5 = 20
        Assert.AreEqual(20.0, model.Predict(0), 1e-10);
    }

    /// <summary>Verifies that many covariates.</summary>
    [TestMethod]
    public void Test_ManyCovariates()
    {
        // Test with 10 covariates
        double[,] covariates = new double[2, 10];
        for (int j = 0; j < 10; j++)
        {
            covariates[0, j] = j + 1;
            covariates[1, j] = (j + 1) * 2;
        }

        var model = new GeneralLinearFunction("Test", covariates);

        Assert.AreEqual(11, model.NumberOfParameters); // intercept + 10
        Assert.AreEqual(10, model.NumberOfCovariates);
    }

    /// <summary>Verifies that negative covariates.</summary>
    [TestMethod]
    public void Test_NegativeCovariates()
    {
        double[,] covariates = new double[,] { { -5.0, -3.0 } };
        var model = new GeneralLinearFunction("Test", covariates);
        model.Parameters[0].Value = 100.0;
        model.Parameters[1].Value = 1.0;
        model.Parameters[2].Value = 2.0;

        // y = 100 + 1*(-5) + 2*(-3) = 100 - 5 - 6 = 89
        Assert.AreEqual(89.0, model.Predict(0), 1e-10);
    }

    #endregion
}
