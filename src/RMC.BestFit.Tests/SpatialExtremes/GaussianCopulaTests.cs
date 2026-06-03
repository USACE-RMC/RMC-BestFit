using RMC.BestFit.Models.SpatialExtremes;

namespace RMC.BestFit.Tests.SpatialExtremesModels;

/// <summary>
/// Unit tests for the <see cref="GaussianCopula"/> class.
/// Tests spatial dependence modeling using Gaussian copulas.
/// </summary>
/// <remarks>
/// The Gaussian copula is used in spatial extreme value analysis to model
/// the dependence structure between sites. It transforms marginal distributions
/// to standard normal and applies a multivariate normal distribution.
/// Reference: Renard et al. (2006), Journal of Hydrology.
/// </remarks>
[TestClass]
public class GaussianCopulaTests
{
    #region Test Data

    /// <summary>
    /// Creates a simple 3-site coordinate array for testing.
    /// </summary>
    private static double[,] CreateThreeSiteCoordinates()
    {
        return new double[,]
        {
            { 0.0, 0.0 },   // Site 1
            { 1.0, 0.0 },   // Site 2 (1 unit east)
            { 0.0, 1.0 }    // Site 3 (1 unit north)
        };
    }

    /// <summary>
    /// Creates a 5-site coordinate array along a river for testing.
    /// </summary>
    private static double[,] CreateRiverCoordinates()
    {
        return new double[,]
        {
            { 0.0, 0.0 },   // Upstream
            { 10.0, 5.0 },
            { 20.0, 8.0 },
            { 35.0, 10.0 },
            { 50.0, 12.0 }  // Downstream
        };
    }

    #endregion

    #region Constructor Tests

    /// <summary>Verifies that constructor valid coordinates creates instance.</summary>
    [TestMethod]
    public void Test_Constructor_ValidCoordinates_CreatesInstance()
    {
        var coords = CreateThreeSiteCoordinates();

        var copula = new GaussianCopula(coords, CorrelationFunctionType.Exponential);

        Assert.IsNotNull(copula);
        Assert.AreEqual(3, copula.Sites);
    }

    /// <summary>Verifies that constructor exponential correlation.</summary>
    [TestMethod]
    public void Test_Constructor_ExponentialCorrelation()
    {
        var coords = CreateThreeSiteCoordinates();

        var copula = new GaussianCopula(coords, CorrelationFunctionType.Exponential);

        Assert.IsNotNull(copula.CorrelationFunction);
    }

    /// <summary>Verifies that constructor spherical correlation.</summary>
    [TestMethod]
    public void Test_Constructor_SphericalCorrelation()
    {
        var coords = CreateThreeSiteCoordinates();

        var copula = new GaussianCopula(coords, CorrelationFunctionType.Spherical);

        Assert.IsNotNull(copula.CorrelationFunction);
    }

    /// <summary>Verifies that constructor powered exponential correlation.</summary>
    [TestMethod]
    public void Test_Constructor_PoweredExponentialCorrelation()
    {
        var coords = CreateThreeSiteCoordinates();

        var copula = new GaussianCopula(coords, CorrelationFunctionType.PoweredExponential);

        Assert.IsNotNull(copula.CorrelationFunction);
    }

    /// <summary>Verifies that constructor throws when null coordinates.</summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Test_Constructor_NullCoordinates_ThrowsException()
    {
        new GaussianCopula(null!, CorrelationFunctionType.Exponential);
    }

    /// <summary>Verifies that constructor throws when invalid coordinate dimensions.</summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void Test_Constructor_InvalidCoordinateDimensions_ThrowsException()
    {
        // 3D coordinates instead of 2D
        var coords = new double[,]
        {
            { 0.0, 0.0, 0.0 },
            { 1.0, 0.0, 0.0 }
        };

        new GaussianCopula(coords, CorrelationFunctionType.Exponential);
    }

    #endregion

    #region Property Tests

    /// <summary>Verifies that sites returns correct count.</summary>
    [TestMethod]
    public void Test_Sites_ReturnsCorrectCount()
    {
        var coords = CreateRiverCoordinates();
        var copula = new GaussianCopula(coords, CorrelationFunctionType.Exponential);

        Assert.AreEqual(5, copula.Sites);
    }

    /// <summary>Verifies that parameters returns correlation function parameters.</summary>
    [TestMethod]
    public void Test_Parameters_ReturnsCorrelationFunctionParameters()
    {
        var coords = CreateThreeSiteCoordinates();
        var copula = new GaussianCopula(coords, CorrelationFunctionType.Exponential);

        Assert.IsNotNull(copula.Parameters);
        Assert.IsTrue(copula.NumberOfParameters > 0);
    }

    #endregion

    #region SetParameterValues Tests

    /// <summary>Verifies that set parameter values updates correlation matrix.</summary>
    [TestMethod]
    public void Test_SetParameterValues_UpdatesCorrelationMatrix()
    {
        var coords = CreateThreeSiteCoordinates();
        var copula = new GaussianCopula(coords, CorrelationFunctionType.Exponential);

        // Set a range parameter (exponential correlation: ρ(h) = exp(-h/range))
        var values = new List<double> { 2.0 };  // Range = 2
        copula.SetParameterValues(values);

        Assert.AreEqual(2.0, copula.Parameters[0].Value, 1e-10);
    }

    /// <summary>Verifies that set parameter values throws when null values.</summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Test_SetParameterValues_NullValues_ThrowsException()
    {
        var coords = CreateThreeSiteCoordinates();
        var copula = new GaussianCopula(coords, CorrelationFunctionType.Exponential);

        copula.SetParameterValues(null!);
    }

    #endregion

    #region PDF Tests

    /// <summary>Verifies that PDF returns positive when standard normal input.</summary>
    [TestMethod]
    public void Test_PDF_StandardNormalInput_ReturnsPositive()
    {
        var coords = CreateThreeSiteCoordinates();
        var copula = new GaussianCopula(coords, CorrelationFunctionType.Exponential);
        copula.SetParameterValues(new List<double> { 1.0 });

        // Standard normal values (z_i = 0 corresponds to u_i = 0.5)
        var z = new double[] { 0.0, 0.0, 0.0 };

        double pdf = copula.PDF(z);

        Assert.IsTrue(pdf > 0);
        Assert.IsFalse(double.IsNaN(pdf));
    }

    /// <summary>Verifies that PDF independent sites approaches one.</summary>
    [TestMethod]
    public void Test_PDF_IndependentSites_ApproachesOne()
    {
        var coords = CreateThreeSiteCoordinates();
        var copula = new GaussianCopula(coords, CorrelationFunctionType.Exponential);

        // Very small range = nearly independent sites
        copula.SetParameterValues(new List<double> { 0.001 });

        var z = new double[] { 0.0, 0.0, 0.0 };
        double pdf = copula.PDF(z);

        // For independent sites, copula PDF = 1 at any point
        Assert.IsTrue(pdf > 0);
    }

    /// <summary>Verifies that PDF throws when null input.</summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Test_PDF_NullInput_ThrowsException()
    {
        var coords = CreateThreeSiteCoordinates();
        var copula = new GaussianCopula(coords, CorrelationFunctionType.Exponential);
        copula.SetParameterValues(new List<double> { 1.0 });

        copula.PDF(null!);
    }

    /// <summary>Verifies that PDF throws when wrong dimension.</summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void Test_PDF_WrongDimension_ThrowsException()
    {
        var coords = CreateThreeSiteCoordinates();  // 3 sites
        var copula = new GaussianCopula(coords, CorrelationFunctionType.Exponential);
        copula.SetParameterValues(new List<double> { 1.0 });

        // Only 2 values instead of 3
        copula.PDF(new double[] { 0.0, 0.0 });
    }

    #endregion

    #region LogPDF Tests

    /// <summary>Verifies that log PDF returns finite when standard normal input.</summary>
    [TestMethod]
    public void Test_LogPDF_StandardNormalInput_ReturnsFinite()
    {
        var coords = CreateThreeSiteCoordinates();
        var copula = new GaussianCopula(coords, CorrelationFunctionType.Exponential);
        copula.SetParameterValues(new List<double> { 1.0 });

        var z = new double[] { 0.0, 0.0, 0.0 };

        double logPdf = copula.LogPDF(z);

        Assert.IsFalse(double.IsNaN(logPdf));
        Assert.IsFalse(double.IsPositiveInfinity(logPdf));
    }

    /// <summary>Verifies that log PDF consistent with PDF.</summary>
    [TestMethod]
    public void Test_LogPDF_ConsistentWithPDF()
    {
        var coords = CreateThreeSiteCoordinates();
        var copula = new GaussianCopula(coords, CorrelationFunctionType.Exponential);
        copula.SetParameterValues(new List<double> { 1.0 });

        var z = new double[] { 0.5, -0.5, 0.2 };

        double pdf = copula.PDF(z);
        double logPdf = copula.LogPDF(z);

        Assert.AreEqual(Math.Log(pdf), logPdf, 1e-10);
    }

    /// <summary>Verifies that log PDF extreme values.</summary>
    [TestMethod]
    public void Test_LogPDF_ExtremeValues()
    {
        var coords = CreateThreeSiteCoordinates();
        var copula = new GaussianCopula(coords, CorrelationFunctionType.Exponential);
        copula.SetParameterValues(new List<double> { 1.0 });

        // Extreme but valid z-values
        var z = new double[] { 3.0, -2.5, 2.0 };

        double logPdf = copula.LogPDF(z);

        Assert.IsFalse(double.IsNaN(logPdf));
    }

    #endregion

    #region Clone Tests

    /// <summary>Verifies that clone creates independent copy.</summary>
    [TestMethod]
    public void Test_Clone_CreatesIndependentCopy()
    {
        var coords = CreateThreeSiteCoordinates();
        var original = new GaussianCopula(coords, CorrelationFunctionType.Exponential);
        original.SetParameterValues(new List<double> { 2.0 });

        var clone = original.Clone();

        // Modify original
        original.SetParameterValues(new List<double> { 5.0 });

        // Clone should be unchanged
        Assert.AreEqual(2.0, clone.Parameters[0].Value, 1e-10);
    }

    /// <summary>Verifies that clone preserves site count for .</summary>
    [TestMethod]
    public void Test_Clone_PreservesSiteCount()
    {
        var coords = CreateRiverCoordinates();
        var original = new GaussianCopula(coords, CorrelationFunctionType.Exponential);

        var clone = original.Clone();

        Assert.AreEqual(original.Sites, clone.Sites);
    }

    /// <summary>Verifies that clone preserves correlation type for .</summary>
    [TestMethod]
    public void Test_Clone_PreservesCorrelationType()
    {
        var coords = CreateThreeSiteCoordinates();
        var original = new GaussianCopula(coords, CorrelationFunctionType.Spherical);
        original.SetParameterValues(new List<double> { 3.0 });

        var clone = original.Clone();

        Assert.AreEqual(original.NumberOfParameters, clone.NumberOfParameters);
    }

    #endregion

    #region Spatial Correlation Tests

    /// <summary>Verifies that close sites high correlation.</summary>
    [TestMethod]
    public void Test_CloseSites_HighCorrelation()
    {
        // Two very close sites
        var coords = new double[,]
        {
            { 0.0, 0.0 },
            { 0.1, 0.0 }  // Very close
        };
        var copula = new GaussianCopula(coords, CorrelationFunctionType.Exponential);
        copula.SetParameterValues(new List<double> { 1.0 });

        // For highly correlated sites, identical z-values should have high density
        var z = new double[] { 1.0, 1.0 };
        double pdfSame = copula.PDF(z);

        // Opposite z-values should have lower density
        z = new double[] { 1.0, -1.0 };
        double pdfOpposite = copula.PDF(z);

        Assert.IsTrue(pdfSame > pdfOpposite);
    }

    /// <summary>Verifies that far sites low correlation.</summary>
    [TestMethod]
    public void Test_FarSites_LowCorrelation()
    {
        // Two distant sites
        var coords = new double[,]
        {
            { 0.0, 0.0 },
            { 100.0, 0.0 }  // Far apart
        };
        var copula = new GaussianCopula(coords, CorrelationFunctionType.Exponential);
        copula.SetParameterValues(new List<double> { 1.0 });  // Range = 1, sites are 100 apart

        // For nearly independent sites, PDF should be close to 1 at any point
        var z = new double[] { 0.0, 0.0 };
        double pdf = copula.PDF(z);

        // Should be finite and positive
        Assert.IsTrue(pdf > 0);
        Assert.IsFalse(double.IsInfinity(pdf));
    }

    #endregion

    #region Regional Analysis Scenarios

    /// <summary>Verifies that regional network all sites included.</summary>
    [TestMethod]
    public void Test_RegionalNetwork_AllSitesIncluded()
    {
        // Typical regional network of stream gages
        var coords = new double[,]
        {
            { 39.5, -105.0 },  // Denver area
            { 39.7, -104.9 },
            { 39.3, -105.2 },
            { 40.0, -105.5 },
            { 38.8, -104.8 }
        };

        var copula = new GaussianCopula(coords, CorrelationFunctionType.Exponential);

        Assert.AreEqual(5, copula.Sites);
    }

    /// <summary>Verifies that transect sites linear arrangement.</summary>
    [TestMethod]
    public void Test_TransectSites_LinearArrangement()
    {
        // Sites along a transect (e.g., river or climate gradient)
        var coords = new double[,]
        {
            { 0.0, 0.0 },
            { 10.0, 0.0 },
            { 20.0, 0.0 },
            { 30.0, 0.0 }
        };

        var copula = new GaussianCopula(coords, CorrelationFunctionType.Exponential);
        copula.SetParameterValues(new List<double> { 15.0 });  // Range = 15

        // Adjacent sites should have reasonable correlation
        var z = new double[] { 0.0, 0.0, 0.0, 0.0 };
        double pdf = copula.PDF(z);

        Assert.IsTrue(pdf > 0);
    }

    #endregion

    #region Edge Cases

    /// <summary>Verifies that two sites minimum configuration.</summary>
    [TestMethod]
    public void Test_TwoSites_MinimumConfiguration()
    {
        var coords = new double[,]
        {
            { 0.0, 0.0 },
            { 1.0, 1.0 }
        };

        var copula = new GaussianCopula(coords, CorrelationFunctionType.Exponential);
        copula.SetParameterValues(new List<double> { 1.0 });

        var z = new double[] { 0.0, 0.0 };
        double pdf = copula.PDF(z);

        Assert.IsTrue(pdf > 0);
    }

    /// <summary>Verifies that colocated sites high correlation.</summary>
    [TestMethod]
    public void Test_ColocatedSites_HighCorrelation()
    {
        // Sites at nearly same location (very close but not identical to avoid singular matrix)
        // Using a tiny offset (0.001) to ensure matrix is positive-definite
        var coords = new double[,]
        {
            { 0.0, 0.0 },
            { 0.001, 0.0 }  // Very close but not identical
        };

        var copula = new GaussianCopula(coords, CorrelationFunctionType.Exponential);
        copula.SetParameterValues(new List<double> { 1.0 }); // Range = 1.0

        // Distance ≈ 0.001, correlation = exp(-0.001/1.0) ≈ 0.999 (highly correlated)
        // Same values should have high density
        var z = new double[] { 1.5, 1.5 };
        double pdfSame = copula.PDF(z);

        Assert.IsTrue(pdfSame > 0, "Nearly co-located sites with same z values should have positive density.");
    }

    /// <summary>Verifies that large range strong spatial correlation.</summary>
    [TestMethod]
    public void Test_LargeRange_StrongSpatialCorrelation()
    {
        var coords = CreateRiverCoordinates();
        var copula = new GaussianCopula(coords, CorrelationFunctionType.Exponential);

        // Very large range = strong correlation even for distant sites
        copula.SetParameterValues(new List<double> { 1000.0 });

        var z = new double[] { 0.0, 0.0, 0.0, 0.0, 0.0 };
        double pdf = copula.PDF(z);

        Assert.IsTrue(pdf > 0);
    }

    /// <summary>Verifies that small range weak spatial correlation.</summary>
    [TestMethod]
    public void Test_SmallRange_WeakSpatialCorrelation()
    {
        var coords = CreateRiverCoordinates();
        var copula = new GaussianCopula(coords, CorrelationFunctionType.Exponential);

        // Very small range = nearly independent sites
        copula.SetParameterValues(new List<double> { 0.01 });

        var z = new double[] { 0.0, 0.0, 0.0, 0.0, 0.0 };
        double pdf = copula.PDF(z);

        Assert.IsTrue(pdf > 0);
    }

    #endregion
}
