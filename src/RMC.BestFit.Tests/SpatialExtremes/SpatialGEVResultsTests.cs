using RMC.BestFit.Analyses;

namespace RMC.BestFit.Tests.SpatialExtremes;

/// <summary>
/// Unit tests for <c>SpatialGEVSiteResults</c> and <c>SpatialGEVCrossValidationResults</c> classes.
/// </summary>
/// <remarks>
/// <para>
///     <b>Authors:</b>
/// </para>
/// <list type="bullet">
///     <item>Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil</item>
/// </list>
/// <para>
///     These classes store results from spatial GEV analysis including:
///     - Site-specific GEV parameter estimates with uncertainty bounds
///     - Quantile curves at specified exceedance probabilities
///     - Leave-one-site-out cross-validation metrics
/// </para>
/// </remarks>
[TestClass]
public class SpatialGEVResultsTests
{
    #region SpatialGEVSiteResults Tests

    /// <summary>
    /// Tests SpatialGEVSiteResults default construction.
    /// </summary>
    [TestMethod]
    public void SiteResults_DefaultConstruction_HasDefaultValues()
    {
        // Act
        var results = new SpatialGEVSiteResults();

        // Assert
        Assert.AreEqual(0, results.SiteIndex, "Default SiteIndex should be 0.");
        Assert.IsNotNull(results.Coordinate, "Coordinate should not be null.");
        Assert.AreEqual(0, results.Coordinate.Length, "Default Coordinate should be empty.");
        Assert.AreEqual(0.0, results.LocationMean, "Default LocationMean should be 0.");
        Assert.AreEqual(0.0, results.ScaleMean, "Default ScaleMean should be 0.");
        Assert.AreEqual(0.0, results.ShapeMean, "Default ShapeMean should be 0.");
    }

    /// <summary>
    /// Tests SpatialGEVSiteResults property assignment for site index.
    /// </summary>
    [TestMethod]
    public void SiteResults_SiteIndex_CanBeSet()
    {
        // Arrange
        var results = new SpatialGEVSiteResults();

        // Act
        results.SiteIndex = 5;

        // Assert
        Assert.AreEqual(5, results.SiteIndex);
    }

    /// <summary>
    /// Tests SpatialGEVSiteResults SiteIndex = -1 indicates ungauged location.
    /// </summary>
    [TestMethod]
    public void SiteResults_SiteIndexNegativeOne_IndicatesUngauged()
    {
        // Arrange & Act
        var results = new SpatialGEVSiteResults
        {
            SiteIndex = -1,
            Coordinate = new double[] { 25.0, 10.0 }
        };

        // Assert
        Assert.AreEqual(-1, results.SiteIndex, "SiteIndex -1 should indicate ungauged.");
        Assert.AreEqual(2, results.Coordinate.Length, "Should have 2D coordinate.");
    }

    /// <summary>
    /// Tests SpatialGEVSiteResults coordinate property.
    /// </summary>
    [TestMethod]
    public void SiteResults_Coordinate_CanBeSet()
    {
        // Arrange
        var results = new SpatialGEVSiteResults();

        // Act
        results.Coordinate = new double[] { 100.5, 200.3 };

        // Assert
        Assert.AreEqual(100.5, results.Coordinate[0], 1e-10);
        Assert.AreEqual(200.3, results.Coordinate[1], 1e-10);
    }

    /// <summary>
    /// Tests SpatialGEVSiteResults location parameter properties.
    /// </summary>
    [TestMethod]
    public void SiteResults_LocationParameters_CanBeSet()
    {
        // Arrange
        var results = new SpatialGEVSiteResults();

        // Act
        results.LocationMean = 10000.0;
        results.LocationLower = 9000.0;
        results.LocationUpper = 11000.0;

        // Assert
        Assert.AreEqual(10000.0, results.LocationMean, 1e-10);
        Assert.AreEqual(9000.0, results.LocationLower, 1e-10);
        Assert.AreEqual(11000.0, results.LocationUpper, 1e-10);
        Assert.IsTrue(results.LocationLower < results.LocationMean,
            "Lower bound should be less than mean.");
        Assert.IsTrue(results.LocationMean < results.LocationUpper,
            "Mean should be less than upper bound.");
    }

    /// <summary>
    /// Tests SpatialGEVSiteResults scale parameter properties.
    /// </summary>
    [TestMethod]
    public void SiteResults_ScaleParameters_CanBeSet()
    {
        // Arrange
        var results = new SpatialGEVSiteResults();

        // Act
        results.ScaleMean = 3000.0;
        results.ScaleLower = 2500.0;
        results.ScaleUpper = 3500.0;

        // Assert
        Assert.AreEqual(3000.0, results.ScaleMean, 1e-10);
        Assert.AreEqual(2500.0, results.ScaleLower, 1e-10);
        Assert.AreEqual(3500.0, results.ScaleUpper, 1e-10);
        Assert.IsTrue(results.ScaleLower > 0, "Scale lower bound should be positive.");
    }

    /// <summary>
    /// Tests SpatialGEVSiteResults shape parameter properties.
    /// </summary>
    [TestMethod]
    public void SiteResults_ShapeParameters_CanBeSet()
    {
        // Arrange
        var results = new SpatialGEVSiteResults();

        // Act
        results.ShapeMean = -0.15;
        results.ShapeLower = -0.25;
        results.ShapeUpper = -0.05;

        // Assert
        Assert.AreEqual(-0.15, results.ShapeMean, 1e-10);
        Assert.AreEqual(-0.25, results.ShapeLower, 1e-10);
        Assert.AreEqual(-0.05, results.ShapeUpper, 1e-10);
    }

    /// <summary>
    /// Tests SpatialGEVSiteResults probability ordinates property.
    /// </summary>
    [TestMethod]
    public void SiteResults_Probabilities_CanBeSet()
    {
        // Arrange
        var results = new SpatialGEVSiteResults();

        // Act
        results.Probabilities = new double[] { 0.5, 0.1, 0.04, 0.02, 0.01, 0.002 };

        // Assert
        Assert.AreEqual(6, results.Probabilities.Length, "Should have 6 probabilities.");
        Assert.AreEqual(0.5, results.Probabilities[0], 1e-10, "First should be 0.5 (T=2).");
        Assert.AreEqual(0.002, results.Probabilities[5], 1e-10, "Last should be 0.002 (T=500).");
    }

    /// <summary>
    /// Tests SpatialGEVSiteResults quantile arrays can be set.
    /// </summary>
    [TestMethod]
    public void SiteResults_QuantileArrays_CanBeSet()
    {
        // Arrange
        var results = new SpatialGEVSiteResults();
        int nProbs = 5;

        // Act
        results.Probabilities = new double[nProbs];
        results.QuantileMean = new double[nProbs];
        results.QuantileLower = new double[nProbs];
        results.QuantileUpper = new double[nProbs];
        results.QuantileMode = new double[nProbs];

        // Set values for T=100 (p=0.01) position
        results.Probabilities[4] = 0.01;
        results.QuantileMean[4] = 25000.0;
        results.QuantileLower[4] = 22000.0;
        results.QuantileUpper[4] = 30000.0;
        results.QuantileMode[4] = 24500.0;

        // Assert
        Assert.AreEqual(5, results.QuantileMean.Length);
        Assert.AreEqual(25000.0, results.QuantileMean[4], 1e-10);
        Assert.AreEqual(22000.0, results.QuantileLower[4], 1e-10);
        Assert.AreEqual(30000.0, results.QuantileUpper[4], 1e-10);
        Assert.AreEqual(24500.0, results.QuantileMode[4], 1e-10);
    }

    /// <summary>
    /// Tests SpatialGEVSiteResults quantile ordering is consistent.
    /// </summary>
    [TestMethod]
    public void SiteResults_QuantileOrdering_IsConsistent()
    {
        // Arrange - Create realistic results
        var results = new SpatialGEVSiteResults
        {
            Probabilities = new double[] { 0.5, 0.1, 0.01 },  // T=2, T=10, T=100
            QuantileMean = new double[] { 10000, 15000, 25000 }  // Increasing with return period
        };

        // Assert - Higher return periods should have higher quantiles
        Assert.IsTrue(results.QuantileMean[0] < results.QuantileMean[1],
            "T=2 quantile should be < T=10 quantile.");
        Assert.IsTrue(results.QuantileMean[1] < results.QuantileMean[2],
            "T=10 quantile should be < T=100 quantile.");
    }

    /// <summary>
    /// Tests SpatialGEVSiteResults credible interval ordering.
    /// </summary>
    [TestMethod]
    public void SiteResults_CredibleInterval_LowerLessThanUpper()
    {
        // Arrange
        var results = new SpatialGEVSiteResults
        {
            QuantileLower = new double[] { 8000, 12000, 20000 },
            QuantileMean = new double[] { 10000, 15000, 25000 },
            QuantileUpper = new double[] { 12000, 18000, 32000 }
        };

        // Assert
        for (int i = 0; i < 3; i++)
        {
            Assert.IsTrue(results.QuantileLower[i] < results.QuantileMean[i],
                $"Lower[{i}] should be < Mean[{i}].");
            Assert.IsTrue(results.QuantileMean[i] < results.QuantileUpper[i],
                $"Mean[{i}] should be < Upper[{i}].");
        }
    }

    /// <summary>
    /// Tests SpatialGEVSiteResults can represent gauged site.
    /// </summary>
    [TestMethod]
    public void SiteResults_GaugedSite_HasValidIndex()
    {
        // Arrange & Act
        var results = new SpatialGEVSiteResults
        {
            SiteIndex = 3,
            Coordinate = new double[] { 50.0, 25.0 },
            LocationMean = 12000.0,
            ScaleMean = 3500.0,
            ShapeMean = -0.1
        };

        // Assert
        Assert.IsTrue(results.SiteIndex >= 0, "Gauged site should have non-negative index.");
    }

    /// <summary>
    /// Tests SpatialGEVSiteResults can represent ungauged site.
    /// </summary>
    [TestMethod]
    public void SiteResults_UngaugedSite_HasNegativeOneIndex()
    {
        // Arrange & Act
        var results = new SpatialGEVSiteResults
        {
            SiteIndex = -1,
            Coordinate = new double[] { 75.0, 30.0 },
            LocationMean = 14000.0,
            ScaleMean = 4000.0,
            ShapeMean = -0.12
        };

        // Assert
        Assert.AreEqual(-1, results.SiteIndex, "Ungauged site should have index -1.");
        Assert.IsTrue(results.Coordinate.Length == 2, "Should still have valid coordinates.");
    }

    #endregion

    #region SpatialGEVCrossValidationResults Tests

    /// <summary>
    /// Tests SpatialGEVCrossValidationResults default construction.
    /// </summary>
    [TestMethod]
    public void CrossValidation_DefaultConstruction_HasDefaultValues()
    {
        // Act
        var results = new SpatialGEVCrossValidationResults();

        // Assert
        Assert.IsNotNull(results.SitePredictionErrors, "SitePredictionErrors should not be null.");
        Assert.IsNotNull(results.SiteRMSE, "SiteRMSE should not be null.");
        Assert.IsNotNull(results.SiteBias, "SiteBias should not be null.");
        Assert.IsNotNull(results.SiteCRPS, "SiteCRPS should not be null.");
        Assert.AreEqual(0, results.SitePredictionErrors.Length, "Default arrays should be empty.");
        Assert.AreEqual(0.0, results.MeanAbsoluteError, "Default MAE should be 0.");
        Assert.AreEqual(0.0, results.RootMeanSquareError, "Default RMSE should be 0.");
        Assert.AreEqual(0.0, results.MeanBias, "Default bias should be 0.");
    }

    /// <summary>
    /// Tests SpatialGEVCrossValidationResults site prediction errors.
    /// </summary>
    [TestMethod]
    public void CrossValidation_SitePredictionErrors_CanBeSet()
    {
        // Arrange
        var results = new SpatialGEVCrossValidationResults();

        // Act - Set errors for 5 sites
        results.SitePredictionErrors = new double[] { 500, -300, 800, -100, 200 };

        // Assert
        Assert.AreEqual(5, results.SitePredictionErrors.Length);
        Assert.AreEqual(500, results.SitePredictionErrors[0]);
        Assert.AreEqual(-300, results.SitePredictionErrors[1]);
    }

    /// <summary>
    /// Tests SpatialGEVCrossValidationResults site RMSE.
    /// </summary>
    [TestMethod]
    public void CrossValidation_SiteRMSE_CanBeSet()
    {
        // Arrange
        var results = new SpatialGEVCrossValidationResults();

        // Act
        results.SiteRMSE = new double[] { 1500, 2000, 1800, 2200, 1600 };

        // Assert
        Assert.AreEqual(5, results.SiteRMSE.Length);
        foreach (var rmse in results.SiteRMSE)
        {
            Assert.IsTrue(rmse >= 0, "RMSE should be non-negative.");
        }
    }

    /// <summary>
    /// Tests SpatialGEVCrossValidationResults site bias.
    /// </summary>
    [TestMethod]
    public void CrossValidation_SiteBias_CanBeSet()
    {
        // Arrange
        var results = new SpatialGEVCrossValidationResults();

        // Act - Relative bias (can be positive or negative)
        results.SiteBias = new double[] { 0.05, -0.03, 0.08, -0.02, 0.01 };

        // Assert
        Assert.AreEqual(5, results.SiteBias.Length);
        Assert.AreEqual(0.05, results.SiteBias[0], 1e-10);
        Assert.AreEqual(-0.03, results.SiteBias[1], 1e-10);
    }

    /// <summary>
    /// Tests SpatialGEVCrossValidationResults site CRPS.
    /// </summary>
    [TestMethod]
    public void CrossValidation_SiteCRPS_CanBeSet()
    {
        // Arrange
        var results = new SpatialGEVCrossValidationResults();

        // Act - CRPS should be non-negative (lower is better)
        results.SiteCRPS = new double[] { 1200, 1500, 1100, 1800, 1300 };

        // Assert
        Assert.AreEqual(5, results.SiteCRPS.Length);
        foreach (var crps in results.SiteCRPS)
        {
            Assert.IsTrue(crps >= 0, "CRPS should be non-negative.");
        }
    }

    /// <summary>
    /// Tests SpatialGEVCrossValidationResults aggregate metrics.
    /// </summary>
    [TestMethod]
    public void CrossValidation_AggregateMetrics_CanBeSet()
    {
        // Arrange
        var results = new SpatialGEVCrossValidationResults();

        // Act
        results.MeanAbsoluteError = 450.0;
        results.RootMeanSquareError = 620.0;
        results.MeanBias = 0.02;

        // Assert
        Assert.AreEqual(450.0, results.MeanAbsoluteError, 1e-10);
        Assert.AreEqual(620.0, results.RootMeanSquareError, 1e-10);
        Assert.AreEqual(0.02, results.MeanBias, 1e-10);
    }

    /// <summary>
    /// Tests that RMSE is at least as large as MAE.
    /// </summary>
    [TestMethod]
    public void CrossValidation_RMSE_AtLeastAsLargeAsMAE()
    {
        // Arrange - Create results with consistent metrics
        var results = new SpatialGEVCrossValidationResults
        {
            SitePredictionErrors = new double[] { 500, -300, 800, -100, 200 },
            MeanAbsoluteError = 380.0,  // Average of |500|, |-300|, |800|, |-100|, |200| = 380
            RootMeanSquareError = 436.8 // sqrt(mean of squares)
        };

        // Assert - RMSE >= MAE is a mathematical property
        Assert.IsTrue(results.RootMeanSquareError >= results.MeanAbsoluteError,
            "RMSE should be at least as large as MAE.");
    }

    /// <summary>
    /// Tests SpatialGEVCrossValidationResults with perfect predictions.
    /// </summary>
    [TestMethod]
    public void CrossValidation_PerfectPredictions_ZeroError()
    {
        // Arrange & Act
        var results = new SpatialGEVCrossValidationResults
        {
            SitePredictionErrors = new double[] { 0, 0, 0, 0, 0 },
            SiteRMSE = new double[] { 0, 0, 0, 0, 0 },
            SiteBias = new double[] { 0, 0, 0, 0, 0 },
            SiteCRPS = new double[] { 0, 0, 0, 0, 0 },
            MeanAbsoluteError = 0,
            RootMeanSquareError = 0,
            MeanBias = 0
        };

        // Assert
        Assert.AreEqual(0, results.MeanAbsoluteError);
        Assert.AreEqual(0, results.RootMeanSquareError);
        Assert.AreEqual(0, results.MeanBias);
    }

    /// <summary>
    /// Tests SpatialGEVCrossValidationResults identifies problem sites.
    /// </summary>
    [TestMethod]
    public void CrossValidation_IdentifyProblemSites_HighError()
    {
        // Arrange - One site with much higher error
        var results = new SpatialGEVCrossValidationResults
        {
            SiteRMSE = new double[] { 1000, 1200, 5000, 1100, 900 }  // Site 2 has high error
        };

        // Act - Find site with maximum RMSE
        int problemSite = 0;
        double maxRmse = results.SiteRMSE[0];
        for (int i = 1; i < results.SiteRMSE.Length; i++)
        {
            if (results.SiteRMSE[i] > maxRmse)
            {
                maxRmse = results.SiteRMSE[i];
                problemSite = i;
            }
        }

        // Assert
        Assert.AreEqual(2, problemSite, "Site 2 should be identified as the problem site.");
        Assert.AreEqual(5000, maxRmse);
    }

    /// <summary>
    /// Tests SpatialGEVCrossValidationResults systematic bias detection.
    /// </summary>
    [TestMethod]
    public void CrossValidation_SystematicBias_AllSameSign()
    {
        // Arrange - All positive bias indicates systematic over-prediction
        var results = new SpatialGEVCrossValidationResults
        {
            SiteBias = new double[] { 0.05, 0.08, 0.03, 0.07, 0.04 },
            MeanBias = 0.054  // Average
        };

        // Assert - All biases positive
        bool allPositive = results.SiteBias.All(b => b > 0);
        Assert.IsTrue(allPositive, "All positive biases indicate systematic over-prediction.");
        Assert.IsTrue(results.MeanBias > 0, "Mean bias should be positive.");
    }

    /// <summary>
    /// Tests SpatialGEVCrossValidationResults with varying site errors.
    /// </summary>
    [TestMethod]
    public void CrossValidation_VariedSitePerformance_CapturedCorrectly()
    {
        // Arrange
        var results = new SpatialGEVCrossValidationResults
        {
            SitePredictionErrors = new double[] { 200, -800, 100, 500, -300 },
            SiteRMSE = new double[] { 500, 1200, 400, 800, 600 },
            SiteBias = new double[] { 0.02, -0.08, 0.01, 0.05, -0.03 },
            SiteCRPS = new double[] { 400, 1000, 350, 650, 500 }
        };

        // Assert - Site 1 (index 1) performs worst on most metrics
        Assert.AreEqual(results.SiteRMSE.Max(), results.SiteRMSE[1],
            "Site 1 should have highest RMSE.");
        Assert.AreEqual(results.SiteCRPS.Max(), results.SiteCRPS[1],
            "Site 1 should have highest CRPS.");
    }

    #endregion

    #region Integration Tests

    /// <summary>
    /// Tests that results classes can be used together.
    /// </summary>
    [TestMethod]
    public void Results_CanBeUsedTogether()
    {
        // Arrange
        int nSites = 5;
        var siteResults = new SpatialGEVSiteResults[nSites];
        for (int i = 0; i < nSites; i++)
        {
            siteResults[i] = new SpatialGEVSiteResults
            {
                SiteIndex = i,
                Coordinate = new double[] { i * 10.0, 0.0 },
                LocationMean = 10000 + i * 2000,
                ScaleMean = 2500 + i * 500,
                ShapeMean = -0.1 - i * 0.02
            };
        }

        var cvResults = new SpatialGEVCrossValidationResults
        {
            SitePredictionErrors = new double[nSites],
            SiteRMSE = new double[nSites],
            SiteBias = new double[nSites],
            SiteCRPS = new double[nSites]
        };

        for (int i = 0; i < nSites; i++)
        {
            cvResults.SitePredictionErrors[i] = (i - 2) * 100; // Centered around 0
            cvResults.SiteRMSE[i] = 500 + i * 50;
        }

        // Assert
        Assert.AreEqual(nSites, siteResults.Length);
        Assert.AreEqual(nSites, cvResults.SitePredictionErrors.Length);

        // Site results are independent
        for (int i = 0; i < nSites; i++)
        {
            Assert.AreEqual(i, siteResults[i].SiteIndex);
        }
    }

    /// <summary>
    /// Tests results can represent regional frequency analysis output.
    /// </summary>
    [TestMethod]
    public void Results_RegionalFrequencyAnalysis_Representation()
    {
        // Arrange - Typical regional FA results
        double[] probs = { 0.5, 0.2, 0.1, 0.04, 0.02, 0.01 };  // T = 2, 5, 10, 25, 50, 100
        int nSites = 3;

        var allResults = new SpatialGEVSiteResults[nSites];
        for (int site = 0; site < nSites; site++)
        {
            allResults[site] = new SpatialGEVSiteResults
            {
                SiteIndex = site,
                Probabilities = (double[])probs.Clone(),
                QuantileMean = new double[probs.Length],
                QuantileLower = new double[probs.Length],
                QuantileUpper = new double[probs.Length]
            };

            // Fill in typical quantile values (increasing with site and return period)
            double baseFlow = 5000 * (site + 1);
            for (int p = 0; p < probs.Length; p++)
            {
                double returnPeriod = 1.0 / probs[p];
                allResults[site].QuantileMean[p] = baseFlow * (1 + 0.5 * Math.Log(returnPeriod));
                allResults[site].QuantileLower[p] = allResults[site].QuantileMean[p] * 0.8;
                allResults[site].QuantileUpper[p] = allResults[site].QuantileMean[p] * 1.25;
            }
        }

        // Assert - Check structure and ordering
        for (int site = 0; site < nSites; site++)
        {
            Assert.AreEqual(6, allResults[site].Probabilities.Length);

            // Quantiles should increase with return period (decreasing probability)
            for (int p = 1; p < probs.Length; p++)
            {
                Assert.IsTrue(allResults[site].QuantileMean[p] > allResults[site].QuantileMean[p - 1],
                    $"Site {site}: Quantiles should increase with return period.");
            }
        }
    }

    /// <summary>
    /// Tests results for ungauged location prediction.
    /// </summary>
    [TestMethod]
    public void Results_UngaugedPrediction_StructureCorrect()
    {
        // Arrange - Prediction at ungauged location
        var ungaugedResult = new SpatialGEVSiteResults
        {
            SiteIndex = -1,  // Indicates ungauged
            Coordinate = new double[] { 25.5, 12.3 },
            LocationMean = 11500,
            LocationLower = 10200,
            LocationUpper = 13000,
            ScaleMean = 3200,
            ScaleLower = 2700,
            ScaleUpper = 3800,
            ShapeMean = -0.12,
            ShapeLower = -0.20,
            ShapeUpper = -0.05,
            Probabilities = new double[] { 0.01 },
            QuantileMean = new double[] { 28000 },
            QuantileLower = new double[] { 23000 },
            QuantileUpper = new double[] { 35000 }
        };

        // Assert
        Assert.AreEqual(-1, ungaugedResult.SiteIndex, "Ungauged should have index -1.");
        Assert.AreEqual(25.5, ungaugedResult.Coordinate[0], 1e-10);
        Assert.IsTrue(ungaugedResult.LocationLower < ungaugedResult.LocationMean);
        Assert.IsTrue(ungaugedResult.QuantileLower[0] < ungaugedResult.QuantileMean[0]);
    }

    #endregion

    #region Edge Cases

    /// <summary>
    /// Tests SiteResults with empty arrays.
    /// </summary>
    [TestMethod]
    public void SiteResults_EmptyArrays_HandleCorrectly()
    {
        // Act
        var results = new SpatialGEVSiteResults
        {
            Probabilities = Array.Empty<double>(),
            QuantileMean = Array.Empty<double>(),
            QuantileLower = Array.Empty<double>(),
            QuantileUpper = Array.Empty<double>(),
            QuantileMode = Array.Empty<double>()
        };

        // Assert
        Assert.AreEqual(0, results.Probabilities.Length);
        Assert.AreEqual(0, results.QuantileMean.Length);
    }

    /// <summary>
    /// Tests CrossValidation with single site.
    /// </summary>
    [TestMethod]
    public void CrossValidation_SingleSite_HandleCorrectly()
    {
        // Arrange & Act
        var results = new SpatialGEVCrossValidationResults
        {
            SitePredictionErrors = new double[] { 250 },
            SiteRMSE = new double[] { 500 },
            SiteBias = new double[] { 0.025 },
            SiteCRPS = new double[] { 400 },
            MeanAbsoluteError = 250,
            RootMeanSquareError = 500,
            MeanBias = 0.025
        };

        // Assert
        Assert.AreEqual(1, results.SitePredictionErrors.Length);
        Assert.AreEqual(results.SitePredictionErrors[0], results.MeanAbsoluteError,
            "With single site, aggregate should equal site value.");
    }

    /// <summary>
    /// Tests results with very large values.
    /// </summary>
    [TestMethod]
    public void Results_LargeValues_HandleCorrectly()
    {
        // Arrange & Act
        var results = new SpatialGEVSiteResults
        {
            LocationMean = 1e8,  // Very large flood
            ScaleMean = 1e7,
            ShapeMean = -0.3
        };

        // Assert
        Assert.AreEqual(1e8, results.LocationMean, 1e-10);
        Assert.AreEqual(1e7, results.ScaleMean, 1e-10);
    }

    /// <summary>
    /// Tests results with very small values.
    /// </summary>
    [TestMethod]
    public void Results_SmallValues_HandleCorrectly()
    {
        // Arrange & Act
        var results = new SpatialGEVSiteResults
        {
            LocationMean = 0.001,  // Very small measurements
            ScaleMean = 0.0005,
            ShapeMean = 0.0
        };

        // Assert
        Assert.AreEqual(0.001, results.LocationMean, 1e-10);
        Assert.AreEqual(0.0005, results.ScaleMean, 1e-10);
    }

    #endregion
}
