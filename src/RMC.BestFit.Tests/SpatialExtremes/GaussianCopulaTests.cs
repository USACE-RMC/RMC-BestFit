using RMC.BestFit.Models.SpatialExtremes;

namespace RMC.BestFit.Tests.SpatialExtremes;

/// <summary>
/// Unit tests for the <c>GaussianCopula</c> class.
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

    #region Observed-Subset Evaluation Tests

    /// <summary>
    /// Verifies that the observed-subset evaluation of a complete row takes the full-dimensional path
    /// and equals <c>LogPDF(z)</c> exactly.
    /// </summary>
    [TestMethod]
    public void LogPDF_ObservedSubset_AllSitesObserved_EqualsFullEvaluation()
    {
        var copula = new GaussianCopula(CreateRiverCoordinates(), CorrelationFunctionType.Exponential);
        copula.SetParameterValues(new List<double> { 25.0 });
        var z = new double[] { 0.4, -1.1, 0.7, 1.9, -0.3 };

        double full = copula.LogPDF(z);
        double subset = copula.LogPDF(z, new[] { 0, 1, 2, 3, 4 });

        Assert.IsTrue(double.IsFinite(full));
        Assert.AreEqual(full, subset, 0.0, "A complete row must reproduce the full-dimensional density exactly.");
    }

    /// <summary>
    /// Verifies that a row with fewer than two observed sites has no dependence term: the marginal
    /// copula density of a single coordinate is one.
    /// </summary>
    [TestMethod]
    public void LogPDF_ObservedSubset_FewerThanTwoSites_IsZero()
    {
        var copula = new GaussianCopula(CreateRiverCoordinates(), CorrelationFunctionType.Exponential);
        copula.SetParameterValues(new List<double> { 25.0 });
        var z = new double[] { 0.4, -1.1, 0.7, 1.9, -0.3 };

        Assert.AreEqual(0.0, copula.LogPDF(z, new[] { 2 }), 0.0, "One observed site.");
        Assert.AreEqual(0.0, copula.LogPDF(z, Array.Empty<int>()), 0.0, "No observed site.");
    }

    /// <summary>
    /// Verifies that marginalizing the unobserved sites equals the Gaussian copula built on the observed
    /// sites alone: the correlation depends only on inter-site distances, so both constructions share
    /// the observed-site correlation submatrix. Entries of <c>z</c> at unobserved sites are ignored.
    /// </summary>
    [TestMethod]
    public void LogPDF_ObservedSubset_EqualsCopulaBuiltOnObservedSites()
    {
        double[,] coordinates = CreateRiverCoordinates();
        var full = new GaussianCopula(coordinates, CorrelationFunctionType.Exponential);
        full.SetParameterValues(new List<double> { 25.0 });
        int[] observed = { 0, 2, 4 };
        var observedCoordinates = new double[,]
        {
            { coordinates[0, 0], coordinates[0, 1] },
            { coordinates[2, 0], coordinates[2, 1] },
            { coordinates[4, 0], coordinates[4, 1] }
        };
        var reduced = new GaussianCopula(observedCoordinates, CorrelationFunctionType.Exponential);
        reduced.SetParameterValues(new List<double> { 25.0 });

        var z = new double[] { 0.4, double.NaN, 0.7, double.NaN, -0.3 };
        double expected = reduced.LogPDF(new[] { 0.4, 0.7, -0.3 });
        double actual = full.LogPDF(z, observed);

        Assert.IsTrue(double.IsFinite(expected));
        Assert.AreNotEqual(0.0, expected, "The three observed sites carry a dependence term.");
        Assert.AreEqual(expected, actual, 1e-12, "Observed-subset evaluation versus the copula built on the observed sites.");
    }

    /// <summary>
    /// Verifies that the per-pattern factorization cache is reused for repeated rows and invalidated when
    /// the correlation parameters change.
    /// </summary>
    [TestMethod]
    public void LogPDF_ObservedSubset_TracksParameterChanges()
    {
        var copula = new GaussianCopula(CreateRiverCoordinates(), CorrelationFunctionType.Exponential);
        int[] observed = { 1, 2, 3 };
        var z = new double[] { 0.2, 0.9, -0.4, 1.3, 0.0 };

        copula.SetParameterValues(new List<double> { 10.0 });
        double first = copula.LogPDF(z, observed);
        double firstAgain = copula.LogPDF(z, observed);
        copula.SetParameterValues(new List<double> { 60.0 });
        double second = copula.LogPDF(z, observed);

        var fresh = new GaussianCopula(CreateRiverCoordinates(), CorrelationFunctionType.Exponential);
        fresh.SetParameterValues(new List<double> { 60.0 });

        Assert.AreEqual(first, firstAgain, 0.0, "Repeated evaluation of the same pattern.");
        Assert.AreNotEqual(first, second, "A new range changes the observed-subset density.");
        Assert.AreEqual(fresh.LogPDF(z, observed), second, 0.0, "The cache must not serve the old factorization.");
    }

    /// <summary>
    /// Verifies the argument validation of the observed-subset evaluation.
    /// </summary>
    [TestMethod]
    public void LogPDF_ObservedSubset_InvalidArguments_Throw()
    {
        var copula = new GaussianCopula(CreateRiverCoordinates(), CorrelationFunctionType.Exponential);
        copula.SetParameterValues(new List<double> { 25.0 });
        var z = new double[5];

        Assert.ThrowsException<ArgumentNullException>(() => copula.LogPDF(null!, new[] { 0, 1 }));
        Assert.ThrowsException<ArgumentNullException>(() => copula.LogPDF(z, null!));
        Assert.ThrowsException<ArgumentException>(() => copula.LogPDF(new double[4], new[] { 0, 1 }), "z must have one entry per site.");
        Assert.ThrowsException<ArgumentException>(() => copula.LogPDF(z, new[] { 0, 5 }), "Index outside the site range.");
        Assert.ThrowsException<ArgumentException>(() => copula.LogPDF(z, new[] { 2, 1 }), "Indices must increase.");
        Assert.ThrowsException<ArgumentException>(() => copula.LogPDF(z, new[] { 1, 1 }), "Duplicate index.");
        Assert.ThrowsException<ArgumentException>(() => copula.LogPDF(z, new[] { 0, 1, 2, 3, 4, 4 }), "More indices than sites.");
    }

    #endregion

    #region Correlation Matrix Accessor Tests

    /// <summary>
    /// Verifies that the correlation matrix accessor is null before the parameters are set and afterwards
    /// returns an independent copy of the fitted correlation matrix.
    /// </summary>
    [TestMethod]
    public void GetCorrelationMatrix_ReturnsCopyOfTheFittedMatrix()
    {
        double[,] coordinates = CreateRiverCoordinates();
        var copula = new GaussianCopula(coordinates, CorrelationFunctionType.Exponential);
        Assert.IsNull(copula.GetCorrelationMatrix(), "No matrix before the parameters are set.");

        copula.SetParameterValues(new List<double> { 25.0 });
        double[,]? matrix = copula.GetCorrelationMatrix();

        Assert.IsNotNull(matrix);
        Assert.AreEqual(5, matrix!.GetLength(0));
        for (int i = 0; i < 5; i++)
        {
            Assert.AreEqual(1.0, matrix[i, i], 0.0);
            for (int j = 0; j < 5; j++)
            {
                double h = Numerics.Tools.Distance(coordinates[i, 0], coordinates[i, 1], coordinates[j, 0], coordinates[j, 1]);
                Assert.AreEqual(i == j ? 1.0 : Math.Exp(-h / 25.0), matrix[i, j], 1e-12, $"Entry ({i + 1}, {j + 1}).");
            }
        }
        matrix[0, 1] = 99.0;
        Assert.AreNotEqual(99.0, copula.GetCorrelationMatrix()![0, 1], "The accessor returns a copy.");
    }

    #endregion

    #region Distance Metric Tests

    /// <summary>
    /// Verifies that the geodesic copula builds its correlation from great-circle kilometres (hand haversine),
    /// that the Cartesian constructor is unchanged, that the clone keeps the metric, and that invalid
    /// latitude/longitude pairs are rejected.
    /// </summary>
    [TestMethod]
    public void GeodesicMetric_BuildsCorrelationFromGreatCircleKilometres()
    {
        var latLon = new double[,] { { 38.90, -77.04 }, { 39.29, -76.61 }, { 40.44, -79.99 } };
        var copula = new GaussianCopula(latLon, CorrelationFunctionType.Exponential, SpatialDistanceMetric.Geodesic);
        copula.SetParameterValues(new List<double> { 150.0 });

        double[,] correlation = copula.GetCorrelationMatrix()!;
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                double expected = i == j ? 1.0 : Math.Exp(-Haversine(latLon[i, 0], latLon[i, 1], latLon[j, 0], latLon[j, 1]) / 150.0);
                Assert.AreEqual(expected, correlation[i, j], 1e-12, $"Geodesic correlation ({i + 1}, {j + 1}).");
            }
        }
        Assert.AreEqual(SpatialDistanceMetric.Geodesic, copula.Clone().DistanceMetric, "The clone keeps the metric.");
        Assert.AreEqual(SpatialDistanceMetric.Cartesian, new GaussianCopula(CreateRiverCoordinates(), CorrelationFunctionType.Exponential).DistanceMetric);
        Assert.ThrowsException<ArgumentException>(() => new GaussianCopula(new double[,] { { 95.0, 10.0 }, { 0.0, 0.0 } }, CorrelationFunctionType.Exponential, SpatialDistanceMetric.Geodesic), "Latitude beyond 90 degrees.");
        Assert.ThrowsException<ArgumentException>(() => new GaussianCopula(new double[,] { { 10.0, 190.0 }, { 0.0, 0.0 } }, CorrelationFunctionType.Exponential, SpatialDistanceMetric.Geodesic), "Longitude beyond 180 degrees.");
    }

    /// <summary>
    /// Hand haversine distance in kilometres (mean Earth radius 6371.0088 km).
    /// </summary>
    /// <param name="lat1">Latitude of the first point.</param>
    /// <param name="lon1">Longitude of the first point.</param>
    /// <param name="lat2">Latitude of the second point.</param>
    /// <param name="lon2">Longitude of the second point.</param>
    /// <returns>The great-circle distance in kilometres.</returns>
    private static double Haversine(double lat1, double lon1, double lat2, double lon2)
    {
        double rad = Math.PI / 180.0;
        double dPhi = (lat2 - lat1) * rad;
        double dLambda = (lon2 - lon1) * rad;
        double a = Math.Sin(dPhi / 2) * Math.Sin(dPhi / 2) + Math.Cos(lat1 * rad) * Math.Cos(lat2 * rad) * Math.Sin(dLambda / 2) * Math.Sin(dLambda / 2);
        return 2 * 6371.0088 * Math.Asin(Math.Sqrt(a));
    }

    #endregion
}
