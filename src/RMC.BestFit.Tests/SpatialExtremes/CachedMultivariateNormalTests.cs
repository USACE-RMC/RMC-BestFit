using RMC.BestFit.Models.SpatialExtremes;

namespace RMC.BestFit.Tests.SpatialExtremesModels;

/// <summary>
/// Unit tests for the <see cref="CachedMultivariateNormal"/> class.
/// Tests the cached multivariate normal distribution for efficient likelihood computation.
/// </summary>
/// <remarks>
/// <para>
///     <b>Authors:</b>
/// </para>
/// <list type="bullet">
///     <item>Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil</item>
/// </list>
/// <para>
///     The <see cref="CachedMultivariateNormal"/> class provides an efficient implementation
///     of the multivariate normal distribution with cached Cholesky decomposition. This is
///     critical for performance in MCMC sampling where the covariance matrix is often reused.
/// </para>
/// </remarks>
[TestClass]
public class CachedMultivariateNormalTests
{
    #region Test Data Helpers

    /// <summary>
    /// Creates a simple identity covariance matrix.
    /// </summary>
    private static double[,] CreateIdentityCovariance(int n)
    {
        var cov = new double[n, n];
        for (int i = 0; i < n; i++)
            cov[i, i] = 1.0;
        return cov;
    }

    /// <summary>
    /// Creates a positive definite covariance matrix with specified diagonal and correlation.
    /// </summary>
    private static double[,] CreateCorrelatedCovariance(int n, double variance, double correlation)
    {
        var cov = new double[n, n];
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                if (i == j)
                    cov[i, j] = variance;
                else
                    cov[i, j] = variance * correlation;
            }
        }
        return cov;
    }

    /// <summary>
    /// Creates a zero mean vector.
    /// </summary>
    private static double[] CreateZeroMean(int n)
    {
        return new double[n];
    }

    #endregion

    #region Constructor Tests

    /// <summary>
    /// Tests constructor with dimension initializes correctly.
    /// </summary>
    [TestMethod]
    public void Constructor_WithDimension_InitializesCorrectly()
    {
        // Act
        var mvn = new CachedMultivariateNormal(3);

        // Assert
        Assert.AreEqual(3, mvn.Dimension, "Dimension should be 3.");
        Assert.IsFalse(mvn.IsCacheValid, "Cache should not be valid initially.");
    }

    /// <summary>
    /// Tests constructor with mean and covariance.
    /// </summary>
    [TestMethod]
    public void Constructor_WithMeanAndCovariance_InitializesCorrectly()
    {
        // Arrange
        var mean = new double[] { 1.0, 2.0, 3.0 };
        var cov = CreateIdentityCovariance(3);

        // Act
        var mvn = new CachedMultivariateNormal(mean, cov);

        // Assert
        Assert.AreEqual(3, mvn.Dimension);
        Assert.IsFalse(mvn.IsCacheValid, "Cache should not be valid initially.");
    }

    /// <summary>
    /// Tests constructor throws for null mean.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Constructor_NullMean_ThrowsArgumentNullException()
    {
        var cov = CreateIdentityCovariance(3);
        _ = new CachedMultivariateNormal(null!, cov);
    }

    /// <summary>
    /// Tests constructor throws for null covariance.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Constructor_NullCovariance_ThrowsArgumentNullException()
    {
        var mean = new double[] { 0.0, 0.0, 0.0 };
        _ = new CachedMultivariateNormal(mean, null!);
    }

    /// <summary>
    /// Tests constructor throws for dimension mismatch.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void Constructor_DimensionMismatch_ThrowsArgumentException()
    {
        var mean = new double[] { 0.0, 0.0 }; // 2D
        var cov = CreateIdentityCovariance(3); // 3D
        _ = new CachedMultivariateNormal(mean, cov);
    }

    /// <summary>
    /// Tests constructor throws for non-square covariance.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void Constructor_NonSquareCovariance_ThrowsArgumentException()
    {
        var mean = new double[] { 0.0, 0.0, 0.0 };
        var cov = new double[3, 2]; // Not square
        _ = new CachedMultivariateNormal(mean, cov);
    }

    #endregion

    #region SetMean Tests

    /// <summary>
    /// Tests SetMean updates the mean vector.
    /// </summary>
    [TestMethod]
    public void SetMean_UpdatesMeanVector()
    {
        // Arrange
        var mvn = new CachedMultivariateNormal(3);
        mvn.SetCovariance(CreateIdentityCovariance(3));
        var x = new double[] { 0.0, 0.0, 0.0 };

        // Get LogPDF with zero mean
        double logPdf1 = mvn.LogPDF(x);

        // Act
        mvn.SetMean(new double[] { 1.0, 1.0, 1.0 });
        double logPdf2 = mvn.LogPDF(x);

        // Assert - Different means should produce different densities
        Assert.AreNotEqual(logPdf1, logPdf2,
            "Different means should produce different log-densities.");
    }

    /// <summary>
    /// Tests SetMean throws for null.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void SetMean_Null_ThrowsArgumentNullException()
    {
        var mvn = new CachedMultivariateNormal(3);
        mvn.SetMean(null!);
    }

    /// <summary>
    /// Tests SetMean throws for wrong dimension.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void SetMean_WrongDimension_ThrowsArgumentException()
    {
        var mvn = new CachedMultivariateNormal(3);
        mvn.SetMean(new double[] { 0.0, 0.0 }); // Wrong dimension
    }

    #endregion

    #region SetCovariance Tests

    /// <summary>
    /// Tests SetCovariance updates the covariance and invalidates cache.
    /// </summary>
    [TestMethod]
    public void SetCovariance_UpdatesCovarianceAndInvalidatesCache()
    {
        // Arrange
        var mvn = new CachedMultivariateNormal(3);
        mvn.SetCovariance(CreateIdentityCovariance(3));

        // Force cache update
        mvn.LogPDF(new double[] { 0, 0, 0 });
        Assert.IsTrue(mvn.IsCacheValid, "Cache should be valid after LogPDF.");

        // Act
        mvn.SetCovariance(CreateCorrelatedCovariance(3, 2.0, 0.5));

        // Assert
        Assert.IsFalse(mvn.IsCacheValid, "Cache should be invalidated after SetCovariance.");
    }

    /// <summary>
    /// Tests SetCovariance throws for null.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void SetCovariance_Null_ThrowsArgumentNullException()
    {
        var mvn = new CachedMultivariateNormal(3);
        mvn.SetCovariance(null!);
    }

    /// <summary>
    /// Tests SetCovariance throws for wrong dimension.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void SetCovariance_WrongDimension_ThrowsArgumentException()
    {
        var mvn = new CachedMultivariateNormal(3);
        mvn.SetCovariance(CreateIdentityCovariance(4)); // Wrong dimension
    }

    #endregion

    #region LogPDF Tests

    /// <summary>
    /// Tests LogPDF returns finite value for valid inputs.
    /// </summary>
    [TestMethod]
    public void LogPDF_ValidInputs_ReturnsFiniteValue()
    {
        // Arrange
        var mean = CreateZeroMean(3);
        var cov = CreateIdentityCovariance(3);
        var mvn = new CachedMultivariateNormal(mean, cov);
        var x = new double[] { 0.0, 0.0, 0.0 };

        // Act
        double logPdf = mvn.LogPDF(x);

        // Assert
        Assert.IsFalse(double.IsNaN(logPdf), "LogPDF should not be NaN.");
        Assert.IsTrue(double.IsFinite(logPdf), "LogPDF should be finite.");
    }

    /// <summary>
    /// Tests LogPDF is maximum at the mean.
    /// </summary>
    [TestMethod]
    public void LogPDF_MaximumAtMean()
    {
        // Arrange
        var mean = new double[] { 1.0, 2.0, 3.0 };
        var cov = CreateIdentityCovariance(3);
        var mvn = new CachedMultivariateNormal(mean, cov);

        // Act
        double logPdfAtMean = mvn.LogPDF(mean);
        double logPdfOffMean = mvn.LogPDF(new double[] { 0.0, 0.0, 0.0 });

        // Assert
        Assert.IsTrue(logPdfAtMean > logPdfOffMean,
            "LogPDF should be maximum at the mean.");
    }

    /// <summary>
    /// Tests LogPDF throws for null input.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void LogPDF_NullInput_ThrowsArgumentNullException()
    {
        var mean = CreateZeroMean(3);
        var cov = CreateIdentityCovariance(3);
        var mvn = new CachedMultivariateNormal(mean, cov);
        mvn.LogPDF(null!);
    }

    /// <summary>
    /// Tests LogPDF throws for wrong dimension.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void LogPDF_WrongDimension_ThrowsArgumentException()
    {
        var mean = CreateZeroMean(3);
        var cov = CreateIdentityCovariance(3);
        var mvn = new CachedMultivariateNormal(mean, cov);
        mvn.LogPDF(new double[] { 0.0, 0.0 }); // Wrong dimension
    }

    /// <summary>
    /// Tests LogPDF for univariate case matches standard normal.
    /// </summary>
    [TestMethod]
    public void LogPDF_Univariate_MatchesStandardNormal()
    {
        // Arrange - Standard normal: N(0, 1)
        var mean = new double[] { 0.0 };
        var cov = new double[,] { { 1.0 } };
        var mvn = new CachedMultivariateNormal(mean, cov);

        // At x = 0: log(1/√(2π)) = -0.5 * log(2π)
        double x = 0.0;

        // Act
        double logPdf = mvn.LogPDF(new double[] { x });

        // Expected: -0.5 * log(2π) - 0.5 * x² = -0.5 * log(2π) ≈ -0.9189
        double expected = -0.5 * Math.Log(2 * Math.PI);

        // Assert
        Assert.AreEqual(expected, logPdf, 1e-10,
            "Univariate case should match standard normal formula.");
    }

    /// <summary>
    /// Tests LogPDF for identity covariance.
    /// </summary>
    [TestMethod]
    public void LogPDF_IdentityCovariance_KnownValue()
    {
        // Arrange - N(0, I) in 2D, at x = (1, 0)
        var mean = CreateZeroMean(2);
        var cov = CreateIdentityCovariance(2);
        var mvn = new CachedMultivariateNormal(mean, cov);
        var x = new double[] { 1.0, 0.0 };

        // Act
        double logPdf = mvn.LogPDF(x);

        // Expected: -0.5 * [n*log(2π) + log|I| + x'x]
        //         = -0.5 * [2*log(2π) + 0 + 1] = -0.5 * (2*log(2π) + 1)
        double expected = -0.5 * (2 * Math.Log(2 * Math.PI) + 1.0);

        // Assert
        Assert.AreEqual(expected, logPdf, 1e-10);
    }

    /// <summary>
    /// Tests LogPDF decreases as points move away from mean.
    /// </summary>
    [TestMethod]
    public void LogPDF_DecreasesAwayFromMean()
    {
        // Arrange
        var mean = CreateZeroMean(3);
        var cov = CreateIdentityCovariance(3);
        var mvn = new CachedMultivariateNormal(mean, cov);

        // Act
        double logPdf0 = mvn.LogPDF(new double[] { 0, 0, 0 });
        double logPdf1 = mvn.LogPDF(new double[] { 1, 0, 0 });
        double logPdf2 = mvn.LogPDF(new double[] { 2, 0, 0 });
        double logPdf5 = mvn.LogPDF(new double[] { 5, 0, 0 });

        // Assert
        Assert.IsTrue(logPdf0 > logPdf1, "LogPDF should decrease away from mean.");
        Assert.IsTrue(logPdf1 > logPdf2, "LogPDF should decrease monotonically.");
        Assert.IsTrue(logPdf2 > logPdf5, "LogPDF should continue decreasing.");
    }

    /// <summary>
    /// Tests LogPDF with correlated covariance.
    /// </summary>
    [TestMethod]
    public void LogPDF_CorrelatedCovariance_ReturnsFiniteValue()
    {
        // Arrange - Correlation of 0.5
        var mean = CreateZeroMean(3);
        var cov = CreateCorrelatedCovariance(3, 1.0, 0.5);
        var mvn = new CachedMultivariateNormal(mean, cov);

        // Act
        double logPdf = mvn.LogPDF(new double[] { 0.5, 0.5, 0.5 });

        // Assert
        Assert.IsFalse(double.IsNaN(logPdf), "LogPDF should not be NaN with correlated covariance.");
        Assert.IsTrue(double.IsFinite(logPdf), "LogPDF should be finite.");
    }

    /// <summary>
    /// Tests LogPDF returns <see cref="double.NegativeInfinity"/> for
    /// non-positive-definite covariance, per the CLAUDE.md numerical-pattern
    /// rule for impossible log-likelihood.
    /// </summary>
    [TestMethod]
    public void LogPDF_NonPositiveDefinite_ReturnsNegativeInfinity()
    {
        // Arrange - Non-positive definite covariance (correlation > 1)
        var mean = CreateZeroMean(2);
        var cov = new double[,]
        {
            { 1.0, 1.5 },
            { 1.5, 1.0 }  // This is not positive definite
        };
        var mvn = new CachedMultivariateNormal(mean, cov);

        // Act
        double logPdf = mvn.LogPDF(new double[] { 0.0, 0.0 });

        // Assert
        Assert.AreEqual(double.NegativeInfinity, logPdf,
            "Non-positive definite should return NegativeInfinity.");
    }

    #endregion

    #region PDF Tests

    /// <summary>
    /// Tests PDF returns positive value.
    /// </summary>
    [TestMethod]
    public void PDF_ValidInputs_ReturnsPositiveValue()
    {
        // Arrange
        var mean = CreateZeroMean(3);
        var cov = CreateIdentityCovariance(3);
        var mvn = new CachedMultivariateNormal(mean, cov);

        // Act
        double pdf = mvn.PDF(new double[] { 0.0, 0.0, 0.0 });

        // Assert
        Assert.IsTrue(pdf > 0, "PDF should be positive.");
    }

    /// <summary>
    /// Tests PDF is consistent with LogPDF.
    /// </summary>
    [TestMethod]
    public void PDF_ConsistentWithLogPDF()
    {
        // Arrange
        var mean = new double[] { 1.0, 2.0 };
        var cov = CreateCorrelatedCovariance(2, 2.0, 0.3);
        var mvn = new CachedMultivariateNormal(mean, cov);
        var x = new double[] { 1.5, 1.8 };

        // Act
        double pdf = mvn.PDF(x);
        double logPdf = mvn.LogPDF(x);

        // Assert
        Assert.AreEqual(Math.Exp(logPdf), pdf, 1e-10,
            "PDF should equal exp(LogPDF).");
    }

    /// <summary>
    /// Tests PDF returns 0 for non-positive-definite covariance.
    /// </summary>
    [TestMethod]
    public void PDF_NonPositiveDefinite_ReturnsZero()
    {
        // Arrange
        var mean = CreateZeroMean(2);
        var cov = new double[,]
        {
            { 1.0, 1.5 },
            { 1.5, 1.0 }
        };
        var mvn = new CachedMultivariateNormal(mean, cov);

        // Act
        double pdf = mvn.PDF(new double[] { 0.0, 0.0 });

        // Assert
        Assert.AreEqual(0.0, pdf, "Non-positive definite should return 0.");
    }

    #endregion

    #region GetLogDeterminant Tests

    /// <summary>
    /// Tests GetLogDeterminant for identity matrix is zero.
    /// </summary>
    [TestMethod]
    public void GetLogDeterminant_Identity_ReturnsZero()
    {
        // Arrange
        var mean = CreateZeroMean(3);
        var cov = CreateIdentityCovariance(3);
        var mvn = new CachedMultivariateNormal(mean, cov);

        // Act
        double logDet = mvn.GetLogDeterminant();

        // Assert - log|I| = 0
        Assert.AreEqual(0.0, logDet, 1e-10, "Log determinant of identity should be 0.");
    }

    /// <summary>
    /// Tests GetLogDeterminant for scaled identity matrix.
    /// </summary>
    [TestMethod]
    public void GetLogDeterminant_ScaledIdentity_ReturnsCorrectValue()
    {
        // Arrange - Σ = 4*I (3x3)
        var mean = CreateZeroMean(3);
        var cov = new double[,]
        {
            { 4.0, 0.0, 0.0 },
            { 0.0, 4.0, 0.0 },
            { 0.0, 0.0, 4.0 }
        };
        var mvn = new CachedMultivariateNormal(mean, cov);

        // Act
        double logDet = mvn.GetLogDeterminant();

        // Assert - log|4I| = log(4³) = 3*log(4)
        double expected = 3 * Math.Log(4);
        Assert.AreEqual(expected, logDet, 1e-10,
            "Log determinant should be 3*log(4).");
    }

    /// <summary>
    /// Tests GetLogDeterminant returns <see cref="double.NegativeInfinity"/>
    /// for non-positive-definite covariance, per CLAUDE.md numerical pattern.
    /// </summary>
    [TestMethod]
    public void GetLogDeterminant_NonPositiveDefinite_ReturnsNegativeInfinity()
    {
        // Arrange
        var mean = CreateZeroMean(2);
        var cov = new double[,]
        {
            { 1.0, 2.0 },
            { 2.0, 1.0 }
        };
        var mvn = new CachedMultivariateNormal(mean, cov);

        // Act
        double logDet = mvn.GetLogDeterminant();

        // Assert
        Assert.AreEqual(double.NegativeInfinity, logDet,
            "Non-positive definite should return NegativeInfinity.");
    }

    #endregion

    #region Cache Tests

    /// <summary>
    /// Tests cache is updated after first LogPDF call.
    /// </summary>
    [TestMethod]
    public void Cache_UpdatedAfterFirstLogPDF()
    {
        // Arrange
        var mean = CreateZeroMean(3);
        var cov = CreateIdentityCovariance(3);
        var mvn = new CachedMultivariateNormal(mean, cov);

        Assert.IsFalse(mvn.IsCacheValid, "Cache should not be valid initially.");

        // Act
        mvn.LogPDF(new double[] { 0, 0, 0 });

        // Assert
        Assert.IsTrue(mvn.IsCacheValid, "Cache should be valid after LogPDF.");
    }

    /// <summary>
    /// Tests cache is invalidated after SetCovariance.
    /// </summary>
    [TestMethod]
    public void Cache_InvalidatedAfterSetCovariance()
    {
        // Arrange
        var mvn = new CachedMultivariateNormal(3);
        mvn.SetCovariance(CreateIdentityCovariance(3));
        mvn.LogPDF(new double[] { 0, 0, 0 }); // Force cache update

        Assert.IsTrue(mvn.IsCacheValid, "Cache should be valid after LogPDF.");

        // Act
        mvn.SetCovariance(CreateCorrelatedCovariance(3, 2.0, 0.5));

        // Assert
        Assert.IsFalse(mvn.IsCacheValid, "Cache should be invalidated after SetCovariance.");
    }

    /// <summary>
    /// Tests InvalidateCache method.
    /// </summary>
    [TestMethod]
    public void InvalidateCache_InvalidatesCache()
    {
        // Arrange
        var mean = CreateZeroMean(3);
        var cov = CreateIdentityCovariance(3);
        var mvn = new CachedMultivariateNormal(mean, cov);
        mvn.LogPDF(new double[] { 0, 0, 0 }); // Force cache update

        Assert.IsTrue(mvn.IsCacheValid);

        // Act
        mvn.InvalidateCache();

        // Assert
        Assert.IsFalse(mvn.IsCacheValid);
    }

    /// <summary>
    /// Tests repeated LogPDF calls with same covariance are efficient.
    /// </summary>
    [TestMethod]
    public void Cache_RepeatedLogPDF_UsesCache()
    {
        // Arrange
        var mean = CreateZeroMean(3);
        var cov = CreateIdentityCovariance(3);
        var mvn = new CachedMultivariateNormal(mean, cov);

        var x1 = new double[] { 0.0, 0.0, 0.0 };
        var x2 = new double[] { 1.0, 1.0, 1.0 };
        var x3 = new double[] { 0.5, -0.5, 0.5 };

        // Act - Multiple calls should use cached Cholesky
        double logPdf1 = mvn.LogPDF(x1);
        Assert.IsTrue(mvn.IsCacheValid);

        double logPdf2 = mvn.LogPDF(x2);
        Assert.IsTrue(mvn.IsCacheValid);

        double logPdf3 = mvn.LogPDF(x3);
        Assert.IsTrue(mvn.IsCacheValid);

        // Assert - All should return finite values
        Assert.IsTrue(double.IsFinite(logPdf1));
        Assert.IsTrue(double.IsFinite(logPdf2));
        Assert.IsTrue(double.IsFinite(logPdf3));
    }

    #endregion

    #region Edge Cases

    /// <summary>
    /// Tests univariate case (dimension = 1).
    /// </summary>
    [TestMethod]
    public void Univariate_WorksCorrectly()
    {
        // Arrange - N(0, 4)
        var mean = new double[] { 0.0 };
        var cov = new double[,] { { 4.0 } };
        var mvn = new CachedMultivariateNormal(mean, cov);

        // Act
        double logPdf = mvn.LogPDF(new double[] { 0.0 });

        // Expected: -0.5 * [log(2π) + log(4) + 0] = -0.5 * log(8π)
        double expected = -0.5 * Math.Log(8 * Math.PI);

        // Assert
        Assert.AreEqual(expected, logPdf, 1e-10);
    }

    /// <summary>
    /// Tests higher dimensional case.
    /// </summary>
    [TestMethod]
    public void HigherDimension_WorksCorrectly()
    {
        // Arrange - 10D standard normal
        int n = 10;
        var mean = CreateZeroMean(n);
        var cov = CreateIdentityCovariance(n);
        var mvn = new CachedMultivariateNormal(mean, cov);

        // Act
        var x = new double[n];
        double logPdf = mvn.LogPDF(x);

        // Expected: -0.5 * n * log(2π)
        double expected = -0.5 * n * Math.Log(2 * Math.PI);

        // Assert
        Assert.AreEqual(expected, logPdf, 1e-10);
    }

    /// <summary>
    /// Tests with diagonal covariance (different variances).
    /// </summary>
    [TestMethod]
    public void DiagonalCovariance_DifferentVariances()
    {
        // Arrange - Σ = diag(1, 4, 9)
        var mean = CreateZeroMean(3);
        var cov = new double[,]
        {
            { 1.0, 0.0, 0.0 },
            { 0.0, 4.0, 0.0 },
            { 0.0, 0.0, 9.0 }
        };
        var mvn = new CachedMultivariateNormal(mean, cov);

        // Act
        double logPdf = mvn.LogPDF(new double[] { 0, 0, 0 });
        double logDet = mvn.GetLogDeterminant();

        // Expected log det: log(1*4*9) = log(36)
        Assert.AreEqual(Math.Log(36), logDet, 1e-10);

        // Assert
        Assert.IsTrue(double.IsFinite(logPdf));
    }

    /// <summary>
    /// Tests with high correlation (near singular).
    /// </summary>
    [TestMethod]
    public void HighCorrelation_StillWorkable()
    {
        // Arrange - Correlation of 0.95
        var mean = CreateZeroMean(3);
        var cov = CreateCorrelatedCovariance(3, 1.0, 0.95);
        var mvn = new CachedMultivariateNormal(mean, cov);

        // Act
        double logPdf = mvn.LogPDF(new double[] { 0, 0, 0 });

        // Assert - Should still be computable
        Assert.IsFalse(double.IsNaN(logPdf), "High correlation should still work.");
        Assert.IsTrue(double.IsFinite(logPdf), "LogPDF should be finite.");
    }

    /// <summary>
    /// Tests with very large values.
    /// </summary>
    [TestMethod]
    public void LargeValues_ReturnsFiniteResult()
    {
        // Arrange
        var mean = CreateZeroMean(3);
        var cov = CreateIdentityCovariance(3);
        var mvn = new CachedMultivariateNormal(mean, cov);

        // Act
        double logPdf = mvn.LogPDF(new double[] { 100.0, 100.0, 100.0 });

        // Assert - Should be very negative but finite
        Assert.IsTrue(double.IsFinite(logPdf), "Should handle large values.");
        Assert.IsTrue(logPdf < -1000, "LogPDF should be very negative for large values.");
    }

    /// <summary>
    /// Tests with very small variance.
    /// </summary>
    [TestMethod]
    public void SmallVariance_WorksCorrectly()
    {
        // Arrange - Very concentrated distribution
        var mean = CreateZeroMean(2);
        var cov = new double[,]
        {
            { 0.001, 0.0 },
            { 0.0, 0.001 }
        };
        var mvn = new CachedMultivariateNormal(mean, cov);

        // Act
        double logPdfAtMean = mvn.LogPDF(new double[] { 0, 0 });
        double logPdfAway = mvn.LogPDF(new double[] { 0.1, 0.1 });

        // Assert
        Assert.IsTrue(logPdfAtMean > logPdfAway,
            "Small variance should produce high peak at mean.");
    }

    /// <summary>
    /// Tests with very large variance.
    /// </summary>
    [TestMethod]
    public void LargeVariance_WorksCorrectly()
    {
        // Arrange - Very spread distribution
        var mean = CreateZeroMean(2);
        var cov = new double[,]
        {
            { 1000.0, 0.0 },
            { 0.0, 1000.0 }
        };
        var mvn = new CachedMultivariateNormal(mean, cov);

        // Act
        double logPdf = mvn.LogPDF(new double[] { 0, 0 });

        // Assert
        Assert.IsTrue(double.IsFinite(logPdf), "Large variance should still produce finite LogPDF.");
    }

    #endregion

    #region Numerical Stability Tests

    /// <summary>
    /// Tests numerical stability with ill-conditioned covariance.
    /// </summary>
    [TestMethod]
    public void IllConditioned_HandlesGracefully()
    {
        // Arrange - Nearly singular (but still valid)
        var mean = CreateZeroMean(2);
        var cov = new double[,]
        {
            { 1.0, 0.9999 },
            { 0.9999, 1.0 }
        };
        var mvn = new CachedMultivariateNormal(mean, cov);

        // Act
        double logPdf = mvn.LogPDF(new double[] { 0, 0 });

        // Assert - Should be computable (very small determinant)
        Assert.IsFalse(double.IsNaN(logPdf), "Nearly singular should still work.");
    }

    /// <summary>
    /// Tests that truly singular covariance is handled (returns
    /// <see cref="double.NegativeInfinity"/> per CLAUDE.md numerical pattern).
    /// </summary>
    [TestMethod]
    public void Singular_ReturnsNegativeInfinity()
    {
        // Arrange - Singular covariance (correlation = 1)
        var mean = CreateZeroMean(2);
        var cov = new double[,]
        {
            { 1.0, 1.0 },
            { 1.0, 1.0 }
        };
        var mvn = new CachedMultivariateNormal(mean, cov);

        // Act
        double logPdf = mvn.LogPDF(new double[] { 0, 0 });

        // Assert - Should return NegativeInfinity for singular matrix per CLAUDE.md
        Assert.AreEqual(double.NegativeInfinity, logPdf,
            "Singular covariance should return NegativeInfinity.");
    }

    #endregion
}
