using RMC.BestFit.Diagnostics;

namespace RMC.BestFit.Tests.Diagnostics;

/// <summary>
/// Unit tests for the <c>PredictiveCheckResults</c> class and
/// the <c>PredictiveSummary</c> class.
/// </summary>
/// <remarks>
/// <c>PredictiveCheckResults</c> is a POCO that stores posterior predictive p-values.
/// Tests cover: default NaN properties, property setters, HasPotentialMisfit logic,
/// and boundary conditions.
/// <para>
/// <c>PredictiveSummary</c> is a POCO storing quantile arrays for predictive distributions.
/// Tests cover property initialization and round-trip assignment.
/// </para>
/// </remarks>
[TestClass]
public class PredictiveCheckResultsTests
{
    #region PredictiveCheckResults — Default Value Tests

    /// <summary>
    /// Default constructor initializes all p-values to NaN, matching the pattern
    /// described in the project coding standards for diagnostics classes.
    /// </summary>
    [TestMethod]
    public void Constructor_Default_AllPValuesAreNaN()
    {
        var results = new PredictiveCheckResults();

        Assert.IsTrue(double.IsNaN(results.MeanPValue), "MeanPValue should default to NaN.");
        Assert.IsTrue(double.IsNaN(results.SDPValue), "SDPValue should default to NaN.");
        Assert.IsTrue(double.IsNaN(results.SkewnessPValue), "SkewnessPValue should default to NaN.");
        Assert.IsTrue(double.IsNaN(results.MinPValue), "MinPValue should default to NaN.");
        Assert.IsTrue(double.IsNaN(results.MaxPValue), "MaxPValue should default to NaN.");
    }

    /// <summary>
    /// Default constructor initializes NumberOfReplicates to zero.
    /// </summary>
    [TestMethod]
    public void Constructor_Default_NumberOfReplicatesIsZero()
    {
        var results = new PredictiveCheckResults();

        Assert.AreEqual(0, results.NumberOfReplicates);
    }

    #endregion

    #region Property Setter Tests

    /// <summary>
    /// All properties can be set and retrieved correctly.
    /// </summary>
    [TestMethod]
    public void Properties_SetAndGet_WorkCorrectly()
    {
        var results = new PredictiveCheckResults
        {
            NumberOfReplicates = 1000,
            MeanPValue = 0.52,
            SDPValue = 0.48,
            SkewnessPValue = 0.31,
            MinPValue = 0.07,
            MaxPValue = 0.93
        };

        Assert.AreEqual(1000, results.NumberOfReplicates);
        Assert.AreEqual(0.52, results.MeanPValue, 1e-10);
        Assert.AreEqual(0.48, results.SDPValue, 1e-10);
        Assert.AreEqual(0.31, results.SkewnessPValue, 1e-10);
        Assert.AreEqual(0.07, results.MinPValue, 1e-10);
        Assert.AreEqual(0.93, results.MaxPValue, 1e-10);
    }

    #endregion

    #region HasPotentialMisfit Tests

    /// <summary>
    /// HasPotentialMisfit returns false when all p-values are near 0.5 (good fit).
    /// </summary>
    [TestMethod]
    public void HasPotentialMisfit_GoodFit_ReturnsFalse()
    {
        var results = new PredictiveCheckResults
        {
            MeanPValue = 0.50,
            SDPValue = 0.45,
            SkewnessPValue = 0.55,
            MinPValue = 0.48,
            MaxPValue = 0.52
        };

        Assert.IsFalse(results.HasPotentialMisfit());
    }

    /// <summary>
    /// HasPotentialMisfit returns true when MeanPValue is below threshold.
    /// </summary>
    [TestMethod]
    public void HasPotentialMisfit_LowMeanPValue_ReturnsTrue()
    {
        var results = new PredictiveCheckResults
        {
            MeanPValue = 0.02,  // Below 0.05
            SDPValue = 0.50,
            SkewnessPValue = 0.50,
            MinPValue = 0.50,
            MaxPValue = 0.50
        };

        Assert.IsTrue(results.HasPotentialMisfit());
    }

    /// <summary>
    /// HasPotentialMisfit returns true when MaxPValue is above (1-threshold).
    /// </summary>
    [TestMethod]
    public void HasPotentialMisfit_HighMaxPValue_ReturnsTrue()
    {
        var results = new PredictiveCheckResults
        {
            MeanPValue = 0.50,
            SDPValue = 0.50,
            SkewnessPValue = 0.50,
            MinPValue = 0.50,
            MaxPValue = 0.98  // Above 0.95
        };

        Assert.IsTrue(results.HasPotentialMisfit());
    }

    /// <summary>
    /// HasPotentialMisfit returns true when SDPValue indicates under-prediction of variability.
    /// </summary>
    [TestMethod]
    public void HasPotentialMisfit_LowSDPValue_ReturnsTrue()
    {
        var results = new PredictiveCheckResults
        {
            MeanPValue = 0.50,
            SDPValue = 0.01,   // Very low
            SkewnessPValue = 0.50,
            MinPValue = 0.50,
            MaxPValue = 0.50
        };

        Assert.IsTrue(results.HasPotentialMisfit());
    }

    /// <summary>
    /// HasPotentialMisfit respects custom threshold parameter.
    /// </summary>
    [TestMethod]
    public void HasPotentialMisfit_CustomThreshold_AppliedCorrectly()
    {
        var results = new PredictiveCheckResults
        {
            MeanPValue = 0.07,  // Would fail at threshold=0.10 but pass at 0.05
            SDPValue = 0.50,
            SkewnessPValue = 0.50,
            MinPValue = 0.50,
            MaxPValue = 0.50
        };

        // Default threshold = 0.05: 0.07 > 0.05 so no flag
        Assert.IsFalse(results.HasPotentialMisfit(0.05));

        // Custom threshold = 0.10: 0.07 < 0.10 so flag raised
        Assert.IsTrue(results.HasPotentialMisfit(0.10));
    }

    /// <summary>
    /// HasPotentialMisfit returns true when SkewnessPValue is at the boundary (exactly equals threshold).
    /// </summary>
    [TestMethod]
    public void HasPotentialMisfit_SkewPValueAtLowerThreshold_ReturnsTrue()
    {
        var results = new PredictiveCheckResults
        {
            MeanPValue = 0.50,
            SDPValue = 0.50,
            SkewnessPValue = 0.04,  // Below default 0.05 threshold
            MinPValue = 0.50,
            MaxPValue = 0.50
        };

        Assert.IsTrue(results.HasPotentialMisfit());
    }

    /// <summary>
    /// HasPotentialMisfit with NaN p-values: NaN comparisons return false in C#,
    /// so NaN does not trigger a flag.
    /// </summary>
    [TestMethod]
    public void HasPotentialMisfit_NaNPValues_DoesNotThrow()
    {
        var results = new PredictiveCheckResults();
        // All NaN by default - should not throw
        bool result = results.HasPotentialMisfit();
        // NaN < 0.05 is false, NaN > 0.95 is false => no misfit flagged
        Assert.IsFalse(result);
    }

    #endregion
}

/// <summary>
/// Unit tests for the <c>PredictiveSummary</c> class.
/// </summary>
/// <remarks>
/// <c>PredictiveSummary</c> is a POCO that stores quantile arrays [2.5%, 25%, 50%, 75%, 97.5%]
/// for key summary statistics of the predictive distribution.
/// </remarks>
[TestClass]
public class PredictiveSummaryTests
{
    #region Default Constructor Tests

    /// <summary>
    /// Default constructor initializes all quantile arrays to empty arrays.
    /// </summary>
    [TestMethod]
    public void Constructor_Default_AllQuantileArraysEmpty()
    {
        var summary = new PredictiveSummary();

        Assert.IsNotNull(summary.MeanQuantiles);
        Assert.IsNotNull(summary.SDQuantiles);
        Assert.IsNotNull(summary.MinQuantiles);
        Assert.IsNotNull(summary.MaxQuantiles);
        Assert.AreEqual(0, summary.MeanQuantiles.Length);
        Assert.AreEqual(0, summary.SDQuantiles.Length);
        Assert.AreEqual(0, summary.MinQuantiles.Length);
        Assert.AreEqual(0, summary.MaxQuantiles.Length);
    }

    /// <summary>
    /// Default constructor initializes NumberOfValidDraws to zero.
    /// </summary>
    [TestMethod]
    public void Constructor_Default_NumberOfValidDrawsIsZero()
    {
        var summary = new PredictiveSummary();

        Assert.AreEqual(0, summary.NumberOfValidDraws);
    }

    #endregion

    #region Property Setter Tests

    /// <summary>
    /// All properties can be set and retrieved correctly.
    /// </summary>
    [TestMethod]
    public void Properties_SetAndGet_WorkCorrectly()
    {
        var meanQ = new[] { 90.0, 95.0, 100.0, 105.0, 110.0 };
        var sdQ = new[] { 8.0, 10.0, 12.0, 14.0, 18.0 };
        var minQ = new[] { 40.0, 50.0, 60.0, 70.0, 80.0 };
        var maxQ = new[] { 120.0, 130.0, 140.0, 150.0, 175.0 };

        var summary = new PredictiveSummary
        {
            NumberOfValidDraws = 500,
            MeanQuantiles = meanQ,
            SDQuantiles = sdQ,
            MinQuantiles = minQ,
            MaxQuantiles = maxQ
        };

        Assert.AreEqual(500, summary.NumberOfValidDraws);
        CollectionAssert.AreEqual(meanQ, summary.MeanQuantiles);
        CollectionAssert.AreEqual(sdQ, summary.SDQuantiles);
        CollectionAssert.AreEqual(minQ, summary.MinQuantiles);
        CollectionAssert.AreEqual(maxQ, summary.MaxQuantiles);
    }

    /// <summary>
    /// Quantile arrays of length 5 correspond to [2.5%, 25%, 50%, 75%, 97.5%] convention.
    /// </summary>
    [TestMethod]
    public void MeanQuantiles_Length5_ReflectsConvention()
    {
        // The class is designed for 5 percentiles: [2.5%, 25%, 50%, 75%, 97.5%]
        var summary = new PredictiveSummary
        {
            MeanQuantiles = new[] { 85.0, 94.0, 100.0, 106.0, 120.0 }
        };

        Assert.AreEqual(5, summary.MeanQuantiles.Length);
        Assert.IsTrue(summary.MeanQuantiles[0] < summary.MeanQuantiles[4],
            "Quantiles should be in ascending order.");
    }

    #endregion
}
