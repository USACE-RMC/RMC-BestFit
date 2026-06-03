using Numerics.Distributions;
using RMC.BestFit.Models;

namespace RMC.BestFit.Tests.InputDataFrame;

/// <summary>
/// Unit tests for the <see cref="ThresholdDiagnostics"/> class.
/// Validates the Mean Residual Life and Parameter Stability computations against known
/// theoretical results and edge cases.
/// </summary>
/// <remarks>
/// <para>
///     <b> Authors: </b>
///     <list type="bullet">
///     <item>Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil</item>
///     </list>
/// </para>
/// <para>
/// The threshold diagnostic plots are used to guide threshold selection in Peaks-Over-Threshold
/// (POT) analysis. The MRL plot should be approximately linear above the true threshold for a
/// valid GPD model, and the parameter stability plots should show approximately constant
/// modified scale and shape parameters.
/// </para>
/// <para>
/// References:
/// Coles, S. (2001). An Introduction to Statistical Modelling of Extreme Values. Springer. Section 4.3.
/// R POT package: mrlplot(), tcplot().
/// </para>
/// </remarks>
[TestClass]
public class ThresholdDiagnosticsTests
{
    #region MRL Tests

    /// <summary>
    /// Verifies that the mean residual life is approximately constant for Exponential(1) data.
    /// For an Exponential distribution with rate lambda=1, the mean excess above any threshold u is
    /// E[X - u | X > u] = 1/lambda = 1.0, regardless of u. This is the memoryless property.
    /// </summary>
    [TestMethod]
    public void MRL_Exponential_MeanExcessIsConstant()
    {
        // Arrange — generate Exponential(lambda=1) data using inverse CDF
        int n = 10000;
        var rng = new Random(12345);
        var exp = new Exponential(1.0);
        var data = new double[n];
        for (int i = 0; i < n; i++)
            data[i] = exp.InverseCDF(rng.NextDouble());

        double uMin = 0.5;
        double uMax = 3.0;

        // Act
        var result = ThresholdDiagnostics.ComputeMeanResidualLife(data, uMin, uMax, nThresholds: 50);

        // Assert — all mean excess values should be approximately 1.0
        Assert.IsTrue(result.Points.Count > 0, "Should have at least one MRL point.");
        foreach (var point in result.Points)
        {
            Assert.AreEqual(1.0, point.MeanExcess, 0.15,
                $"Mean excess at threshold {point.Threshold:F3} should be ~1.0 (memoryless property), got {point.MeanExcess:F4}.");
        }
    }

    /// <summary>
    /// Verifies that confidence intervals contain the true mean excess for Exponential data.
    /// At 95% confidence, the true value (1.0) should be within the CI for at least 90% of thresholds
    /// (allowing for sampling variability).
    /// </summary>
    [TestMethod]
    public void MRL_Exponential_CIContainsTrueValue()
    {
        // Arrange
        int n = 10000;
        var rng = new Random(54321);
        var exp = new Exponential(1.0);
        var data = new double[n];
        for (int i = 0; i < n; i++)
            data[i] = exp.InverseCDF(rng.NextDouble());

        // Act
        var result = ThresholdDiagnostics.ComputeMeanResidualLife(data, 0.5, 2.5, nThresholds: 40);

        // Assert — 95% CI should contain 1.0 for most thresholds
        int containsTrue = 0;
        foreach (var point in result.Points)
        {
            if (point.LowerCI <= 1.0 && point.UpperCI >= 1.0)
                containsTrue++;
        }

        double coverage = (double)containsTrue / result.Points.Count;
        Assert.IsTrue(coverage >= 0.90,
            $"95% CI should contain true mean excess (1.0) for at least 90% of thresholds, got {coverage:P1}.");
    }

    /// <summary>
    /// Verifies that CI width narrows with larger sample sizes due to reduced sampling variability.
    /// </summary>
    [TestMethod]
    public void MRL_CIWidthNarrowsWithSampleSize()
    {
        // Arrange — small sample
        var rng1 = new Random(11111);
        var exp = new Exponential(1.0);
        var dataSmall = new double[200];
        for (int i = 0; i < dataSmall.Length; i++)
            dataSmall[i] = exp.InverseCDF(rng1.NextDouble());

        // Arrange — large sample
        var rng2 = new Random(22222);
        var dataLarge = new double[10000];
        for (int i = 0; i < dataLarge.Length; i++)
            dataLarge[i] = exp.InverseCDF(rng2.NextDouble());

        // Act
        var resultSmall = ThresholdDiagnostics.ComputeMeanResidualLife(dataSmall, 0.5, 1.5, nThresholds: 10);
        var resultLarge = ThresholdDiagnostics.ComputeMeanResidualLife(dataLarge, 0.5, 1.5, nThresholds: 10);

        // Assert — average CI width should be smaller for large sample
        double avgWidthSmall = resultSmall.Points.Average(p => p.UpperCI - p.LowerCI);
        double avgWidthLarge = resultLarge.Points.Average(p => p.UpperCI - p.LowerCI);

        Assert.IsTrue(avgWidthLarge < avgWidthSmall,
            $"Large sample CI width ({avgWidthLarge:F4}) should be narrower than small sample ({avgWidthSmall:F4}).");
    }

    /// <summary>
    /// Verifies that thresholds with fewer than 5 exceedances are excluded from the MRL result.
    /// </summary>
    [TestMethod]
    public void MRL_SkipsThresholdsWithFewExceedances()
    {
        // Arrange — small dataset where high thresholds will have < 5 exceedances
        var data = new double[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

        // Act — use a range that includes thresholds near the max (few exceedances)
        var result = ThresholdDiagnostics.ComputeMeanResidualLife(data, 1.0, 9.5, nThresholds: 20);

        // Assert — all returned points should have at least 5 exceedances
        foreach (var point in result.Points)
        {
            Assert.IsTrue(point.ExceedanceCount >= 5,
                $"Point at threshold {point.Threshold:F3} has only {point.ExceedanceCount} exceedances (minimum is 5).");
        }
    }

    #endregion

    #region Parameter Stability Tests

    /// <summary>
    /// Verifies that the GPD modified scale and shape are approximately constant across thresholds
    /// for data drawn from a known GPD distribution.
    /// For GPD(sigma=10, kappa=0.1) data, the modified scale sigma* = sigma - kappa*u should be
    /// approximately constant = sigma = 10 when using the true location as baseline.
    /// </summary>
    [TestMethod]
    public void ParameterStability_GPDData_ParametersAreStable()
    {
        // Arrange — generate GPD(xi=0, alpha=10, kappa=0.1) data
        int n = 5000;
        var rng = new Random(99999);
        var gpd = new GeneralizedPareto(0.0, 10.0, 0.1);
        var data = new double[n];
        for (int i = 0; i < n; i++)
            data[i] = gpd.InverseCDF(rng.NextDouble());

        var sorted = data.OrderBy(v => v).ToArray();
        double uMin = sorted[(int)(n * 0.1)];
        double uMax = sorted[(int)(n * 0.7)];

        // Act
        var result = ThresholdDiagnostics.ComputeParameterStability(data, uMin, uMax, nThresholds: 30);

        // Assert — shape should be approximately constant (~0.1)
        Assert.IsTrue(result.Points.Count > 5, "Should have multiple stability points.");

        // Check that shape values don't vary too wildly
        var shapes = result.Points.Select(p => p.Shape).ToList();
        double meanShape = shapes.Average();
        double maxDeviation = shapes.Max(s => Math.Abs(s - meanShape));

        Assert.AreEqual(0.1, meanShape, 0.1,
            $"Mean shape across thresholds should be ~0.1, got {meanShape:F4}.");
        Assert.IsTrue(maxDeviation < 0.3,
            $"Shape should be approximately stable. Max deviation from mean = {maxDeviation:F4}.");
    }

    /// <summary>
    /// Verifies that thresholds with fewer than 10 exceedances are excluded from parameter stability results.
    /// </summary>
    [TestMethod]
    public void ParameterStability_SkipsThresholdsWithFewExceedances()
    {
        // Arrange — small dataset
        var data = new double[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 };

        // Act — use a range where high thresholds will have < 10 exceedances
        var result = ThresholdDiagnostics.ComputeParameterStability(data, 1.0, 12.0, nThresholds: 20);

        // Assert — all returned points should have at least 10 exceedances
        foreach (var point in result.Points)
        {
            Assert.IsTrue(point.ExceedanceCount >= 10,
                $"Point at threshold {point.Threshold:F3} has only {point.ExceedanceCount} exceedances (minimum is 10).");
        }
    }

    /// <summary>
    /// Verifies that parameter stability CI contains the true shape value for most thresholds.
    /// </summary>
    [TestMethod]
    public void ParameterStability_ShapeCIContainsTrueValue()
    {
        // Arrange — generate GPD(xi=0, alpha=10, kappa=0.1) data
        int n = 5000;
        var rng = new Random(77777);
        var gpd = new GeneralizedPareto(0.0, 10.0, 0.1);
        var data = new double[n];
        for (int i = 0; i < n; i++)
            data[i] = gpd.InverseCDF(rng.NextDouble());

        var sorted = data.OrderBy(v => v).ToArray();
        double uMin = sorted[(int)(n * 0.1)];
        double uMax = sorted[(int)(n * 0.5)];

        // Act
        var result = ThresholdDiagnostics.ComputeParameterStability(data, uMin, uMax, nThresholds: 20);

        // Assert — 95% CI should contain true shape (0.1) for most thresholds
        int containsTrue = 0;
        foreach (var point in result.Points)
        {
            if (point.ShapeLowerCI <= 0.1 && point.ShapeUpperCI >= 0.1)
                containsTrue++;
        }

        double coverage = (double)containsTrue / result.Points.Count;
        Assert.IsTrue(coverage >= 0.80,
            $"95% CI should contain true shape (0.1) for at least 80% of thresholds, got {coverage:P1}.");
    }

    #endregion

    #region Input Validation Tests

    /// <summary>
    /// Verifies that null data throws ArgumentNullException for MRL computation.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void MRL_NullData_ThrowsArgumentNullException()
    {
        ThresholdDiagnostics.ComputeMeanResidualLife(null!, 0, 1);
    }

    /// <summary>
    /// Verifies that empty data throws ArgumentException for MRL computation.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void MRL_EmptyData_ThrowsArgumentException()
    {
        ThresholdDiagnostics.ComputeMeanResidualLife(Array.Empty<double>(), 0, 1);
    }

    /// <summary>
    /// Verifies that uMax less than or equal to uMin throws ArgumentException.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void MRL_UMaxLessThanUMin_ThrowsArgumentException()
    {
        ThresholdDiagnostics.ComputeMeanResidualLife(new double[] { 1, 2, 3 }, 5.0, 3.0);
    }

    /// <summary>
    /// Verifies that nThresholds less than 2 throws ArgumentException.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void MRL_NThresholdsLessThanTwo_ThrowsArgumentException()
    {
        ThresholdDiagnostics.ComputeMeanResidualLife(new double[] { 1, 2, 3 }, 0.5, 2.5, nThresholds: 1);
    }

    /// <summary>
    /// Verifies that invalid confidence level throws ArgumentException.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void MRL_InvalidConfidenceLevel_ThrowsArgumentException()
    {
        ThresholdDiagnostics.ComputeMeanResidualLife(new double[] { 1, 2, 3 }, 0.5, 2.5, confidenceLevel: 1.5);
    }

    /// <summary>
    /// Verifies that null data throws ArgumentNullException for parameter stability computation.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void ParameterStability_NullData_ThrowsArgumentNullException()
    {
        ThresholdDiagnostics.ComputeParameterStability(null!, 0, 1);
    }

    /// <summary>
    /// Verifies that empty data throws ArgumentException for parameter stability computation.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void ParameterStability_EmptyData_ThrowsArgumentException()
    {
        ThresholdDiagnostics.ComputeParameterStability(Array.Empty<double>(), 0, 1);
    }

    #endregion

    #region Result Structure Tests

    /// <summary>
    /// Verifies that MRL points are ordered by increasing threshold value.
    /// </summary>
    [TestMethod]
    public void MRL_PointsAreOrderedByThreshold()
    {
        // Arrange
        var rng = new Random(33333);
        var exp = new Exponential(1.0);
        var data = new double[1000];
        for (int i = 0; i < data.Length; i++)
            data[i] = exp.InverseCDF(rng.NextDouble());

        // Act
        var result = ThresholdDiagnostics.ComputeMeanResidualLife(data, 0.5, 3.0, nThresholds: 30);

        // Assert
        for (int i = 1; i < result.Points.Count; i++)
        {
            Assert.IsTrue(result.Points[i].Threshold > result.Points[i - 1].Threshold,
                "MRL points should be ordered by increasing threshold.");
        }
    }

    /// <summary>
    /// Verifies that the MRL confidence interval lower bound is less than the upper bound.
    /// </summary>
    [TestMethod]
    public void MRL_LowerCILessThanUpperCI()
    {
        // Arrange
        var rng = new Random(44444);
        var exp = new Exponential(1.0);
        var data = new double[2000];
        for (int i = 0; i < data.Length; i++)
            data[i] = exp.InverseCDF(rng.NextDouble());

        // Act
        var result = ThresholdDiagnostics.ComputeMeanResidualLife(data, 0.5, 3.0, nThresholds: 30);

        // Assert
        foreach (var point in result.Points)
        {
            Assert.IsTrue(point.LowerCI < point.UpperCI,
                $"Lower CI ({point.LowerCI:F4}) should be less than Upper CI ({point.UpperCI:F4}) at threshold {point.Threshold:F3}.");
            Assert.IsTrue(point.LowerCI <= point.MeanExcess && point.MeanExcess <= point.UpperCI,
                $"Mean excess ({point.MeanExcess:F4}) should be between Lower CI ({point.LowerCI:F4}) and Upper CI ({point.UpperCI:F4}).");
        }
    }

    /// <summary>
    /// Verifies that parameter stability points contain valid (non-NaN, non-Infinity) values.
    /// </summary>
    [TestMethod]
    public void ParameterStability_ResultsAreFinite()
    {
        // Arrange
        int n = 2000;
        var rng = new Random(55555);
        var gpd = new GeneralizedPareto(0.0, 10.0, 0.05);
        var data = new double[n];
        for (int i = 0; i < n; i++)
            data[i] = gpd.InverseCDF(rng.NextDouble());

        var sorted = data.OrderBy(v => v).ToArray();
        double uMin = sorted[(int)(n * 0.1)];
        double uMax = sorted[(int)(n * 0.5)];

        // Act
        var result = ThresholdDiagnostics.ComputeParameterStability(data, uMin, uMax, nThresholds: 20);

        // Assert
        foreach (var point in result.Points)
        {
            Assert.IsFalse(double.IsNaN(point.ModifiedScale), "Modified scale should not be NaN.");
            Assert.IsFalse(double.IsInfinity(point.ModifiedScale), "Modified scale should not be Infinity.");
            Assert.IsFalse(double.IsNaN(point.Shape), "Shape should not be NaN.");
            Assert.IsFalse(double.IsInfinity(point.Shape), "Shape should not be Infinity.");
            Assert.IsTrue(point.ModifiedScaleLowerCI < point.ModifiedScaleUpperCI,
                "Modified scale lower CI should be less than upper CI.");
            Assert.IsTrue(point.ShapeLowerCI < point.ShapeUpperCI,
                "Shape lower CI should be less than upper CI.");
        }
    }

    #endregion
}
