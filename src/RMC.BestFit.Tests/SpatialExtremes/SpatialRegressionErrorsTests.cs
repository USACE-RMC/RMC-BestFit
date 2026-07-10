using RMC.BestFit.Models.SpatialExtremes;

namespace RMC.BestFit.Tests.SpatialExtremes;

/// <summary>
/// Unit tests for the <c>SpatialRegressionErrors</c> class.
/// Tests the Gaussian Process model for spatially correlated regression errors.
/// </summary>
/// <remarks>
/// <para>
///     <b>Authors:</b>
/// </para>
/// <list type="bullet">
///     <item>Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil</item>
/// </list>
/// <para>
///     The <c>SpatialRegressionErrors</c> class models spatially correlated errors in
///     regression parameters following Renard's BHM framework. The errors follow a multivariate
///     normal distribution: ε ~ MVN(0, Σ) where Σ_ij = σ² * ρ(h_ij).
/// </para>
/// <para>
///     Parameter structure: [σ, correlation_params..., ε_1, ε_2, ..., ε_n]
/// </para>
/// </remarks>
[TestClass]
public class SpatialRegressionErrorsTests
{
    #region Test Data Helpers

    /// <summary>
    /// Creates test coordinates for 3 sites.
    /// </summary>
    private static double[,] CreateThreeSiteCoordinates()
    {
        return new double[,]
        {
            { 0.0, 0.0 },
            { 1.0, 0.0 },
            { 0.0, 1.0 }
        };
    }

    /// <summary>
    /// Creates test coordinates for 5 sites along a line.
    /// </summary>
    private static double[,] CreateFiveSiteCoordinates()
    {
        return new double[,]
        {
            { 0.0, 0.0 },
            { 10.0, 0.0 },
            { 20.0, 0.0 },
            { 30.0, 0.0 },
            { 40.0, 0.0 }
        };
    }

    /// <summary>
    /// Creates test coordinates for 2 sites (minimum).
    /// </summary>
    private static double[,] CreateMinimalCoordinates()
    {
        return new double[,]
        {
            { 0.0, 0.0 },
            { 5.0, 0.0 }
        };
    }

    #endregion

    #region Constructor Tests

    /// <summary>
    /// Tests constructor with valid inputs initializes correctly.
    /// </summary>
    [TestMethod]
    public void Constructor_ValidInputs_InitializesCorrectly()
    {
        // Arrange
        var coords = CreateThreeSiteCoordinates();

        // Act
        var errors = new SpatialRegressionErrors(coords, CorrelationFunctionType.Exponential);

        // Assert
        Assert.IsNotNull(errors, "Object should not be null.");
        Assert.AreEqual(3, errors.Sites, "Should have 3 sites.");
        Assert.IsNotNull(errors.Parameters, "Parameters should not be null.");
        Assert.IsNotNull(errors.ErrorParameters, "ErrorParameters should not be null.");
        Assert.IsNotNull(errors.CorrelationFunction, "CorrelationFunction should not be null.");
    }

    /// <summary>
    /// Tests constructor with Exponential correlation type.
    /// </summary>
    [TestMethod]
    public void Constructor_ExponentialCorrelation_CreatesCorrectFunction()
    {
        // Arrange
        var coords = CreateThreeSiteCoordinates();

        // Act
        var errors = new SpatialRegressionErrors(coords, CorrelationFunctionType.Exponential);

        // Assert
        Assert.IsInstanceOfType(errors.CorrelationFunction, typeof(BasicExponential));
    }

    /// <summary>
    /// Tests constructor with PoweredExponential correlation type.
    /// </summary>
    [TestMethod]
    public void Constructor_PoweredExponentialCorrelation_CreatesCorrectFunction()
    {
        // Arrange
        var coords = CreateThreeSiteCoordinates();

        // Act
        var errors = new SpatialRegressionErrors(coords, CorrelationFunctionType.PoweredExponential);

        // Assert
        Assert.IsInstanceOfType(errors.CorrelationFunction, typeof(PoweredExponential));
    }

    /// <summary>
    /// Tests constructor with Spherical correlation type.
    /// </summary>
    [TestMethod]
    public void Constructor_SphericalCorrelation_CreatesCorrectFunction()
    {
        // Arrange
        var coords = CreateThreeSiteCoordinates();

        // Act
        var errors = new SpatialRegressionErrors(coords, CorrelationFunctionType.Spherical);

        // Assert
        Assert.IsInstanceOfType(errors.CorrelationFunction, typeof(Spherical));
    }

    /// <summary>
    /// Tests constructor throws for null coordinates.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Constructor_NullCoordinates_ThrowsArgumentNullException()
    {
        _ = new SpatialRegressionErrors(null!, CorrelationFunctionType.Exponential);
    }

    /// <summary>
    /// Tests constructor throws for invalid coordinate dimensions.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void Constructor_InvalidCoordinateDimensions_ThrowsArgumentException()
    {
        // 1D coordinates instead of 2D
        var coords = new double[,] { { 0.0 }, { 1.0 } };
        _ = new SpatialRegressionErrors(coords, CorrelationFunctionType.Exponential);
    }

    /// <summary>
    /// Tests constructor throws for 3D coordinates.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void Constructor_3DCoordinates_ThrowsArgumentException()
    {
        var coords = new double[,] { { 0.0, 0.0, 0.0 }, { 1.0, 0.0, 0.0 } };
        _ = new SpatialRegressionErrors(coords, CorrelationFunctionType.Exponential);
    }

    /// <summary>
    /// Tests constructor with custom max error bound.
    /// </summary>
    [TestMethod]
    public void Constructor_CustomMaxError_UsesCustomBound()
    {
        // Arrange
        var coords = CreateThreeSiteCoordinates();
        double maxError = 25.0;

        // Act
        var errors = new SpatialRegressionErrors(coords, CorrelationFunctionType.Exponential, maxError);

        // Assert
        // First parameter (σ) should have upper bound = maxError
        Assert.AreEqual(maxError, errors.Parameters[0].UpperBound, 1e-10,
            "Sigma upper bound should equal maxError.");
    }

    #endregion

    #region Parameter Tests

    /// <summary>
    /// Tests parameter structure: [σ, correlation_params, ε_1...ε_n].
    /// </summary>
    [TestMethod]
    public void Parameters_Structure_IsCorrect()
    {
        // Arrange
        var coords = CreateThreeSiteCoordinates();
        var errors = new SpatialRegressionErrors(coords, CorrelationFunctionType.Exponential);

        // Assert
        // Exponential: 1 sigma + 1 range + 3 site errors = 5 total
        Assert.AreEqual(5, errors.NumberOfParameters, "Should have 5 parameters.");
        Assert.AreEqual("Error Scale (σ)", errors.Parameters[0].Name, "First param should be sigma.");
        Assert.AreEqual("Range", errors.Parameters[1].Name, "Second param should be Range.");
        Assert.AreEqual("ε₁", errors.Parameters[2].Name, "Third param should be ε₁.");
        Assert.AreEqual("ε₂", errors.Parameters[3].Name, "Fourth param should be ε₂.");
        Assert.AreEqual("ε₃", errors.Parameters[4].Name, "Fifth param should be ε₃.");
    }

    /// <summary>
    /// Tests parameter structure with PoweredExponential (2 correlation params).
    /// </summary>
    [TestMethod]
    public void Parameters_PoweredExponential_HasExtraParameter()
    {
        // Arrange
        var coords = CreateThreeSiteCoordinates();
        var errors = new SpatialRegressionErrors(coords, CorrelationFunctionType.PoweredExponential);

        // Assert
        // PoweredExponential: 1 sigma + 2 corr params (range, smoothness) + 3 site errors = 6 total
        Assert.AreEqual(6, errors.NumberOfParameters, "Should have 6 parameters.");
    }

    /// <summary>
    /// Tests ErrorParameters contains only the error terms.
    /// </summary>
    [TestMethod]
    public void ErrorParameters_ContainsOnlyErrors()
    {
        // Arrange
        var coords = CreateThreeSiteCoordinates();
        var errors = new SpatialRegressionErrors(coords, CorrelationFunctionType.Exponential);

        // Assert
        Assert.AreEqual(3, errors.ErrorParameters.Count, "Should have 3 error parameters.");
        foreach (var param in errors.ErrorParameters)
        {
            Assert.IsTrue(param.Name.StartsWith("ε"), $"Error param '{param.Name}' should start with ε.");
        }
    }

    /// <summary>
    /// Tests default error parameters are initialized to zero.
    /// </summary>
    [TestMethod]
    public void DefaultParameters_ErrorsAreZero()
    {
        // Arrange
        var coords = CreateThreeSiteCoordinates();
        var errors = new SpatialRegressionErrors(coords, CorrelationFunctionType.Exponential);

        // Assert
        foreach (var param in errors.ErrorParameters)
        {
            Assert.AreEqual(0.0, param.Value, 1e-10, $"{param.Name} should default to 0.");
        }
    }

    #endregion

    #region SetParameterValues Tests

    /// <summary>
    /// Tests SetParameterValues with valid values.
    /// </summary>
    [TestMethod]
    public void SetParameterValues_ValidValues_UpdatesAllParameters()
    {
        // Arrange
        var coords = CreateThreeSiteCoordinates();
        var errors = new SpatialRegressionErrors(coords, CorrelationFunctionType.Exponential);

        // [σ=2, range=5, ε_1=0.5, ε_2=-0.3, ε_3=0.1]
        var values = new List<double> { 2.0, 5.0, 0.5, -0.3, 0.1 };

        // Act
        errors.SetParameterValues(values);

        // Assert
        Assert.AreEqual(2.0, errors.Parameters[0].Value, 1e-10, "Sigma should be 2.0.");
        Assert.AreEqual(5.0, errors.Parameters[1].Value, 1e-10, "Range should be 5.0.");
        Assert.AreEqual(0.5, errors.ErrorParameters[0].Value, 1e-10, "ε_1 should be 0.5.");
        Assert.AreEqual(-0.3, errors.ErrorParameters[1].Value, 1e-10, "ε_2 should be -0.3.");
        Assert.AreEqual(0.1, errors.ErrorParameters[2].Value, 1e-10, "ε_3 should be 0.1.");
    }

    /// <summary>
    /// Tests SetParameterValues throws for null values.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void SetParameterValues_Null_ThrowsArgumentNullException()
    {
        var coords = CreateThreeSiteCoordinates();
        var errors = new SpatialRegressionErrors(coords, CorrelationFunctionType.Exponential);
        errors.SetParameterValues(null!);
    }

    /// <summary>
    /// Tests SetParameterValues throws for wrong count.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void SetParameterValues_WrongCount_ThrowsArgumentException()
    {
        var coords = CreateThreeSiteCoordinates();
        var errors = new SpatialRegressionErrors(coords, CorrelationFunctionType.Exponential);
        errors.SetParameterValues(new List<double> { 1.0, 2.0 }); // Too few
    }

    /// <summary>
    /// Tests SetParameterValues with PoweredExponential.
    /// </summary>
    [TestMethod]
    public void SetParameterValues_PoweredExponential_UpdatesAllParameters()
    {
        // Arrange
        var coords = CreateThreeSiteCoordinates();
        var errors = new SpatialRegressionErrors(coords, CorrelationFunctionType.PoweredExponential);

        // [σ=2, range=5, smoothness=1.5, ε_1=0.5, ε_2=-0.3, ε_3=0.1]
        var values = new List<double> { 2.0, 5.0, 1.5, 0.5, -0.3, 0.1 };

        // Act
        errors.SetParameterValues(values);

        // Assert
        Assert.AreEqual(2.0, errors.Parameters[0].Value, 1e-10);
        Assert.AreEqual(5.0, errors.Parameters[1].Value, 1e-10);
        Assert.AreEqual(1.5, errors.Parameters[2].Value, 1e-10);
        Assert.AreEqual(0.5, errors.ErrorParameters[0].Value, 1e-10);
    }

    #endregion

    #region GetError Tests

    /// <summary>
    /// Tests GetError returns correct error for each site.
    /// </summary>
    [TestMethod]
    public void GetError_ReturnsCorrectValue()
    {
        // Arrange
        var coords = CreateThreeSiteCoordinates();
        var errors = new SpatialRegressionErrors(coords, CorrelationFunctionType.Exponential);
        errors.SetParameterValues(new List<double> { 2.0, 5.0, 0.5, -0.3, 0.1 });

        // Act & Assert
        Assert.AreEqual(0.5, errors.GetError(0), 1e-10, "Site 0 error should be 0.5.");
        Assert.AreEqual(-0.3, errors.GetError(1), 1e-10, "Site 1 error should be -0.3.");
        Assert.AreEqual(0.1, errors.GetError(2), 1e-10, "Site 2 error should be 0.1.");
    }

    /// <summary>
    /// Tests GetError throws for negative site index.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void GetError_NegativeIndex_ThrowsArgumentOutOfRangeException()
    {
        var coords = CreateThreeSiteCoordinates();
        var errors = new SpatialRegressionErrors(coords, CorrelationFunctionType.Exponential);
        errors.GetError(-1);
    }

    /// <summary>
    /// Tests GetError throws for index beyond range.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void GetError_IndexBeyondRange_ThrowsArgumentOutOfRangeException()
    {
        var coords = CreateThreeSiteCoordinates();
        var errors = new SpatialRegressionErrors(coords, CorrelationFunctionType.Exponential);
        errors.GetError(3); // Only 0, 1, 2 valid
    }

    #endregion

    #region PDF/LogPDF Tests

    /// <summary>
    /// Tests LogPDF returns finite value for valid parameters.
    /// </summary>
    [TestMethod]
    public void LogPDF_ValidParameters_ReturnsFiniteValue()
    {
        // Arrange
        var coords = CreateThreeSiteCoordinates();
        var errors = new SpatialRegressionErrors(coords, CorrelationFunctionType.Exponential);
        errors.SetParameterValues(new List<double> { 1.0, 2.0, 0.0, 0.0, 0.0 });

        // Act
        double logPdf = errors.LogPDF();

        // Assert
        Assert.IsFalse(double.IsNaN(logPdf), "LogPDF should not be NaN.");
        Assert.IsTrue(double.IsFinite(logPdf), "LogPDF should be finite.");
    }

    /// <summary>
    /// Tests LogPDF is maximum when all errors are zero.
    /// </summary>
    [TestMethod]
    public void LogPDF_ZeroErrors_HasHigherDensity()
    {
        // Arrange
        var coords = CreateThreeSiteCoordinates();
        var errors = new SpatialRegressionErrors(coords, CorrelationFunctionType.Exponential);

        // Zero errors
        errors.SetParameterValues(new List<double> { 1.0, 2.0, 0.0, 0.0, 0.0 });
        double logPdfZero = errors.LogPDF();

        // Non-zero errors
        errors.SetParameterValues(new List<double> { 1.0, 2.0, 0.5, -0.3, 0.8 });
        double logPdfNonZero = errors.LogPDF();

        // Assert
        Assert.IsTrue(logPdfZero > logPdfNonZero,
            "Zero mean errors should have higher log-density.");
    }

    /// <summary>
    /// Tests PDF returns positive value.
    /// </summary>
    [TestMethod]
    public void PDF_ValidParameters_ReturnsPositiveValue()
    {
        // Arrange
        var coords = CreateThreeSiteCoordinates();
        var errors = new SpatialRegressionErrors(coords, CorrelationFunctionType.Exponential);
        errors.SetParameterValues(new List<double> { 1.0, 2.0, 0.1, -0.1, 0.05 });

        // Act
        double pdf = errors.PDF();

        // Assert
        Assert.IsTrue(pdf > 0, "PDF should be positive.");
        Assert.IsFalse(double.IsNaN(pdf), "PDF should not be NaN.");
    }

    /// <summary>
    /// Tests PDF is consistent with LogPDF.
    /// </summary>
    [TestMethod]
    public void PDF_ConsistentWithLogPDF()
    {
        // Arrange
        var coords = CreateThreeSiteCoordinates();
        var errors = new SpatialRegressionErrors(coords, CorrelationFunctionType.Exponential);
        errors.SetParameterValues(new List<double> { 1.0, 2.0, 0.1, -0.1, 0.05 });

        // Act
        double pdf = errors.PDF();
        double logPdf = errors.LogPDF();

        // Assert
        Assert.AreEqual(Math.Log(pdf), logPdf, 1e-10,
            "log(PDF) should equal LogPDF.");
    }

    /// <summary>
    /// Tests LogPDF with different correlation functions.
    /// </summary>
    [TestMethod]
    public void LogPDF_DifferentCorrelations_AllReturnFiniteValues()
    {
        // Arrange
        var coords = CreateThreeSiteCoordinates();
        var types = new[] {
            CorrelationFunctionType.Exponential,
            CorrelationFunctionType.PoweredExponential,
            CorrelationFunctionType.Spherical
        };

        // Act & Assert
        foreach (var type in types)
        {
            var errors = new SpatialRegressionErrors(coords, type);
            var values = errors.Parameters.Select(p => p.Value).ToList();
            errors.SetParameterValues(values);

            double logPdf = errors.LogPDF();

            Assert.IsFalse(double.IsNaN(logPdf),
                $"LogPDF with {type} should not be NaN.");
            Assert.IsTrue(double.IsFinite(logPdf),
                $"LogPDF with {type} should be finite.");
        }
    }

    /// <summary>
    /// Tests that larger sigma produces flatter density.
    /// </summary>
    [TestMethod]
    public void LogPDF_LargerSigma_LowerDensityAtMean()
    {
        // Arrange
        var coords = CreateThreeSiteCoordinates();

        var smallSigma = new SpatialRegressionErrors(coords, CorrelationFunctionType.Exponential);
        smallSigma.SetParameterValues(new List<double> { 0.5, 2.0, 0.0, 0.0, 0.0 });

        var largeSigma = new SpatialRegressionErrors(coords, CorrelationFunctionType.Exponential);
        largeSigma.SetParameterValues(new List<double> { 5.0, 2.0, 0.0, 0.0, 0.0 });

        // Act
        double logPdfSmall = smallSigma.LogPDF();
        double logPdfLarge = largeSigma.LogPDF();

        // Assert - Smaller sigma has higher density at mean
        Assert.IsTrue(logPdfSmall > logPdfLarge,
            "Smaller sigma should have higher density at mean.");
    }

    #endregion

    #region Clone Tests

    /// <summary>
    /// Tests Clone creates independent copy.
    /// </summary>
    [TestMethod]
    public void Clone_CreatesIndependentCopy()
    {
        // Arrange
        var coords = CreateThreeSiteCoordinates();
        var original = new SpatialRegressionErrors(coords, CorrelationFunctionType.Exponential);
        original.SetParameterValues(new List<double> { 2.0, 5.0, 0.5, -0.3, 0.1 });

        // Act
        var clone = original.Clone();

        // Modify original
        original.SetParameterValues(new List<double> { 10.0, 10.0, 1.0, 1.0, 1.0 });

        // Assert
        Assert.AreEqual(2.0, clone.Parameters[0].Value, 1e-10,
            "Clone sigma should be independent.");
        Assert.AreEqual(0.5, clone.GetError(0), 1e-10,
            "Clone error should be independent.");
    }

    /// <summary>
    /// Tests Clone preserves site count.
    /// </summary>
    [TestMethod]
    public void Clone_PreservesSiteCount()
    {
        // Arrange
        var coords = CreateFiveSiteCoordinates();
        var original = new SpatialRegressionErrors(coords, CorrelationFunctionType.Spherical);

        // Act
        var clone = original.Clone();

        // Assert
        Assert.AreEqual(original.Sites, clone.Sites);
        Assert.AreEqual(original.NumberOfParameters, clone.NumberOfParameters);
    }

    /// <summary>
    /// Tests Clone preserves bounds.
    /// </summary>
    [TestMethod]
    public void Clone_PreservesBounds()
    {
        // Arrange
        var coords = CreateThreeSiteCoordinates();
        var original = new SpatialRegressionErrors(coords, CorrelationFunctionType.Exponential, maxError: 50.0);

        // Act
        var clone = original.Clone();

        // Assert
        Assert.AreEqual(original.Parameters[0].UpperBound, clone.Parameters[0].UpperBound, 1e-10);
        Assert.AreEqual(original.Parameters[0].LowerBound, clone.Parameters[0].LowerBound, 1e-10);
    }

    /// <summary>
    /// Tests Clone creates valid MVN that can compute PDF.
    /// </summary>
    [TestMethod]
    public void Clone_CreatesValidMVN()
    {
        // Arrange
        var coords = CreateThreeSiteCoordinates();
        var original = new SpatialRegressionErrors(coords, CorrelationFunctionType.Exponential);
        original.SetParameterValues(new List<double> { 1.5, 3.0, 0.2, -0.1, 0.3 });

        // Act
        var clone = original.Clone();
        double logPdfOriginal = original.LogPDF();
        double logPdfClone = clone.LogPDF();

        // Assert
        Assert.AreEqual(logPdfOriginal, logPdfClone, 1e-10,
            "Clone should produce same LogPDF.");
    }

    #endregion

    #region SetDefaultParameters Tests

    /// <summary>
    /// Tests SetDefaultParameters creates valid initial state.
    /// </summary>
    [TestMethod]
    public void SetDefaultParameters_CreatesValidState()
    {
        // Arrange
        var coords = CreateThreeSiteCoordinates();
        var errors = new SpatialRegressionErrors(coords, CorrelationFunctionType.Exponential);

        // Act
        errors.SetDefaultParameters(10.0);

        // Assert
        Assert.IsNotNull(errors.Parameters);
        Assert.AreEqual(5, errors.NumberOfParameters);
        Assert.IsTrue(errors.Parameters[0].Value > 0, "Sigma should be positive.");

        foreach (var param in errors.ErrorParameters)
        {
            Assert.AreEqual(0.0, param.Value, 1e-10, "Default errors should be 0.");
        }
    }

    /// <summary>
    /// Tests SetDefaultParameters with different max error values.
    /// </summary>
    [TestMethod]
    public void SetDefaultParameters_RespectsMaxErrorBound()
    {
        // Arrange
        var coords = CreateThreeSiteCoordinates();
        var errors = new SpatialRegressionErrors(coords, CorrelationFunctionType.Exponential);

        // Act
        errors.SetDefaultParameters(20.0);

        // Assert
        Assert.AreEqual(20.0, errors.Parameters[0].UpperBound, 1e-10);
        foreach (var param in errors.ErrorParameters)
        {
            Assert.AreEqual(-20.0, param.LowerBound, 1e-10);
            Assert.AreEqual(20.0, param.UpperBound, 1e-10);
        }
    }

    #endregion

    #region Distance Matrix Tests

    /// <summary>
    /// Tests that distance matrix is computed correctly.
    /// </summary>
    [TestMethod]
    public void DistanceMatrix_ComputedCorrectly()
    {
        // Arrange - Triangle: sites at (0,0), (3,0), (0,4)
        var coords = new double[,]
        {
            { 0.0, 0.0 },
            { 3.0, 0.0 },
            { 0.0, 4.0 }
        };
        var errors = new SpatialRegressionErrors(coords, CorrelationFunctionType.Exponential);

        // The distance matrix affects the covariance, so we test indirectly
        // by checking that LogPDF changes when site positions change spatial correlation

        errors.SetParameterValues(new List<double> { 1.0, 10.0, 0.5, 0.5, 0.5 }); // All errors = 0.5
        double logPdf1 = errors.LogPDF();

        // Create new errors with farther sites
        var farCoords = new double[,]
        {
            { 0.0, 0.0 },
            { 100.0, 0.0 },
            { 0.0, 100.0 }
        };
        var farErrors = new SpatialRegressionErrors(farCoords, CorrelationFunctionType.Exponential);
        farErrors.SetParameterValues(new List<double> { 1.0, 10.0, 0.5, 0.5, 0.5 });
        double logPdf2 = farErrors.LogPDF();

        // Assert - Different distances should produce different correlations and hence different densities
        Assert.AreNotEqual(logPdf1, logPdf2,
            "Different site distances should produce different log-densities.");
    }

    #endregion

    #region Edge Cases

    /// <summary>
    /// Tests with minimum 2 sites.
    /// </summary>
    [TestMethod]
    public void MinimalSites_WorksCorrectly()
    {
        // Arrange
        var coords = CreateMinimalCoordinates();
        var errors = new SpatialRegressionErrors(coords, CorrelationFunctionType.Exponential);

        // Act
        Assert.AreEqual(2, errors.Sites);
        Assert.AreEqual(4, errors.NumberOfParameters); // σ + range + 2 errors

        errors.SetParameterValues(new List<double> { 1.0, 5.0, 0.1, -0.1 });
        double logPdf = errors.LogPDF();

        // Assert
        Assert.IsTrue(double.IsFinite(logPdf));
    }

    /// <summary>
    /// Tests with co-located sites.
    /// </summary>
    [TestMethod]
    public void ColocatedSites_HandledCorrectly()
    {
        // Arrange
        var coords = new double[,]
        {
            { 0.0, 0.0 },
            { 0.0, 0.0 }  // Same location
        };
        var errors = new SpatialRegressionErrors(coords, CorrelationFunctionType.Exponential);
        errors.SetParameterValues(new List<double> { 1.0, 5.0, 0.0, 0.0 });

        // Act
        double logPdf = errors.LogPDF();

        // Assert - Should be computable (correlation = 1 for same location)
        Assert.IsFalse(double.IsNaN(logPdf), "LogPDF should handle co-located sites.");
    }

    /// <summary>
    /// Tests with many sites.
    /// </summary>
    [TestMethod]
    public void ManySites_WorksCorrectly()
    {
        // Arrange - 10 sites in a row
        var coords = new double[10, 2];
        for (int i = 0; i < 10; i++)
        {
            coords[i, 0] = i * 5.0;
            coords[i, 1] = 0.0;
        }

        var errors = new SpatialRegressionErrors(coords, CorrelationFunctionType.Exponential);

        // Act
        Assert.AreEqual(10, errors.Sites);
        Assert.AreEqual(12, errors.NumberOfParameters); // σ + range + 10 errors

        var values = errors.Parameters.Select(p => p.Value).ToList();
        errors.SetParameterValues(values);
        double logPdf = errors.LogPDF();

        // Assert
        Assert.IsTrue(double.IsFinite(logPdf), "LogPDF should work with many sites.");
    }

    /// <summary>
    /// Tests with very small sigma.
    /// </summary>
    [TestMethod]
    public void VerySmallSigma_HandledCorrectly()
    {
        // Arrange
        var coords = CreateThreeSiteCoordinates();
        var errors = new SpatialRegressionErrors(coords, CorrelationFunctionType.Exponential);
        errors.SetParameterValues(new List<double> { 0.001, 2.0, 0.0, 0.0, 0.0 });

        // Act
        double logPdf = errors.LogPDF();

        // Assert
        Assert.IsTrue(double.IsFinite(logPdf), "LogPDF should handle very small sigma.");
    }

    /// <summary>
    /// Tests with large errors (relative to sigma).
    /// </summary>
    [TestMethod]
    public void LargeErrors_ProduceLowDensity()
    {
        // Arrange
        var coords = CreateThreeSiteCoordinates();

        var smallErrors = new SpatialRegressionErrors(coords, CorrelationFunctionType.Exponential);
        smallErrors.SetParameterValues(new List<double> { 1.0, 2.0, 0.1, 0.1, 0.1 });

        var largeErrors = new SpatialRegressionErrors(coords, CorrelationFunctionType.Exponential);
        largeErrors.SetParameterValues(new List<double> { 1.0, 2.0, 5.0, 5.0, 5.0 });

        // Act
        double logPdfSmall = smallErrors.LogPDF();
        double logPdfLarge = largeErrors.LogPDF();

        // Assert
        Assert.IsTrue(logPdfSmall > logPdfLarge,
            "Small errors should have higher density than large errors.");
    }

    #endregion

    #region Spatial Correlation Tests

    /// <summary>
    /// Tests that closer sites produce higher correlation (and different density).
    /// </summary>
    [TestMethod]
    public void CloserSites_HigherCorrelation()
    {
        // Arrange - Close sites
        var closeCoords = new double[,]
        {
            { 0.0, 0.0 },
            { 0.1, 0.0 },
            { 0.2, 0.0 }
        };

        // Far sites
        var farCoords = new double[,]
        {
            { 0.0, 0.0 },
            { 100.0, 0.0 },
            { 200.0, 0.0 }
        };

        var closeErrors = new SpatialRegressionErrors(closeCoords, CorrelationFunctionType.Exponential);
        var farErrors = new SpatialRegressionErrors(farCoords, CorrelationFunctionType.Exponential);

        // Set same parameter values - positive errors at all sites
        var values = new List<double> { 1.0, 1.0, 0.5, 0.5, 0.5 };
        closeErrors.SetParameterValues(values);
        farErrors.SetParameterValues(values);

        // Act
        double logPdfClose = closeErrors.LogPDF();
        double logPdfFar = farErrors.LogPDF();

        // Assert - Close correlated sites should have higher density for similar errors
        Assert.IsTrue(logPdfClose > logPdfFar,
            "Close sites with similar errors should have higher density.");
    }

    #endregion
}
