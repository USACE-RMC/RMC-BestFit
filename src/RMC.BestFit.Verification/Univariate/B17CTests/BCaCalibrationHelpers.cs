using Numerics.Data.Statistics;
using Numerics.Distributions;

namespace RMC.BestFit.Verification.Univariate.B17CTests;

/// <summary>
/// Shared helpers for BCa bootstrap calibration experiments across all distribution types.
/// </summary>
/// <remarks>
/// <para>
/// Provides distribution-agnostic BCa confidence interval computation, jackknife acceleration,
/// and asymmetry ratio calculation. Each distribution-specific test class supplies its own
/// jackknife leave-one-out estimates (since re-estimation requires the specific distribution type).
/// </para>
/// <para>
/// Reference: Efron, B. (1987). Better Bootstrap Confidence Intervals. JASA, 82(397), 171-185.
/// </para>
/// </remarks>
public static class BCaCalibrationHelpers
{
    /// <summary>
    /// Default confidence level for BCa CIs (0.1 gives 90% CI).
    /// </summary>
    public const double DefaultAlpha = 0.1;

    #region BCa Parameter CI

    /// <summary>
    /// Computes the BCa-corrected confidence interval for a single parameter
    /// from bootstrap draws and pre-computed jackknife estimates.
    /// </summary>
    /// <param name="bootValues">Bootstrap parameter draws (NaN-filtered, not necessarily sorted).</param>
    /// <param name="pointEstimate">The MOM point estimate of the parameter.</param>
    /// <param name="jackEstimates">Leave-one-out jackknife parameter estimates (may contain NaN).</param>
    /// <param name="alpha">Significance level (default 0.1 for 90% CI).</param>
    /// <returns>BCa-corrected CI bounds and diagnostic values (z0, acceleration).</returns>
    /// <remarks>
    /// <para>
    /// Implements the BCa percentile method of Efron (1987) applied to distribution parameters.
    /// The method adjusts raw bootstrap percentiles using:
    /// </para>
    /// <list type="bullet">
    ///     <item><description>z0 (bias correction): measures median bias of bootstrap distribution
    ///     relative to the point estimate.</description></item>
    ///     <item><description>a (acceleration): measures rate of change of standard error,
    ///     computed from the jackknife influence function.</description></item>
    /// </list>
    /// <para>
    /// Adjusted percentile: P_adj = Phi(z0 + (z0 + z_alpha) / (1 - a * (z0 + z_alpha)))
    /// </para>
    /// </remarks>
    public static (double Lower, double Upper, double Z0, double Accel) BCaParameterCI(
        double[] bootValues,
        double pointEstimate,
        double[] jackEstimates,
        double alpha = DefaultAlpha)
    {
        double ciLo = alpha / 2.0;
        double ciHi = 1.0 - alpha / 2.0;

        // Sort bootstrap values for percentile lookup
        var sorted = (double[])bootValues.Clone();
        Array.Sort(sorted);

        // z0: bias correction — proportion of bootstrap values at or below point estimate
        double p0 = (sorted.Count(v => v <= pointEstimate) + 1.0) / (sorted.Length + 1.0);
        double z0 = Normal.StandardZ(p0);

        // Acceleration from pre-computed jackknife estimates
        double accel = JackknifeAcceleration(jackEstimates);

        // BCa-adjusted percentiles
        double zLo = Normal.StandardZ(ciLo);
        double zHi = Normal.StandardZ(ciHi);

        double numLo = z0 + zLo;
        double numHi = z0 + zHi;

        double denLo = 1.0 - accel * numLo;
        double denHi = 1.0 - accel * numHi;

        // Guard against division by zero or near-zero
        double bcLo = Math.Abs(denLo) > 1e-10
            ? Normal.StandardCDF(z0 + numLo / denLo)
            : ciLo;
        double bcHi = Math.Abs(denHi) > 1e-10
            ? Normal.StandardCDF(z0 + numHi / denHi)
            : ciHi;

        // Clamp to valid percentile range
        bcLo = Math.Max(0.001, Math.Min(0.999, bcLo));
        bcHi = Math.Max(0.001, Math.Min(0.999, bcHi));

        double lower = Statistics.Percentile(sorted, bcLo, dataIsSorted: true);
        double upper = Statistics.Percentile(sorted, bcHi, dataIsSorted: true);

        return (lower, upper, z0, accel);
    }

    #endregion

    #region Jackknife Acceleration

    /// <summary>
    /// Computes the jackknife acceleration constant from pre-computed leave-one-out estimates.
    /// </summary>
    /// <param name="jackEstimates">Leave-one-out parameter estimates (may contain NaN for failed fits).</param>
    /// <returns>The acceleration constant a = I3 / (6 * I2^{3/2}).</returns>
    /// <remarks>
    /// <para>
    /// The acceleration measures the skewness of the jackknife influence function,
    /// which determines how the standard error of the estimator changes with the parameter value.
    /// A positive acceleration means the upper tail of the CI stretches more than the lower.
    /// </para>
    /// </remarks>
    public static double JackknifeAcceleration(double[] jackEstimates)
    {
        var validJack = jackEstimates.Where(x => !double.IsNaN(x)).ToArray();
        if (validJack.Length < 3) return 0.0;

        double jackMean = validJack.Average();
        double I2 = 0, I3 = 0;
        foreach (double val in validJack)
        {
            double diff = jackMean - val;
            I2 += diff * diff;
            I3 += diff * diff * diff;
        }

        double denom = 6.0 * Math.Pow(I2, 1.5);
        return Math.Abs(denom) > 1e-30 ? I3 / denom : 0.0;
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Extracts a column from a 2D array, applying a filter predicate.
    /// </summary>
    /// <param name="matrix">The 2D parameter matrix [B x p].</param>
    /// <param name="colIndex">The column index to extract.</param>
    /// <param name="filter">Predicate to filter valid values.</param>
    /// <returns>Filtered 1D array of values from the specified column.</returns>
    public static double[] ExtractColumn(double[,] matrix, int colIndex, Func<double, bool> filter)
    {
        int rows = matrix.GetLength(0);
        var values = new List<double>(rows);
        for (int i = 0; i < rows; i++)
        {
            double v = matrix[i, colIndex];
            if (filter(v))
                values.Add(v);
        }
        return values.ToArray();
    }

    /// <summary>
    /// Computes the CI asymmetry ratio R = (upper - point) / (point - lower).
    /// Returns NaN if the denominator is near zero.
    /// </summary>
    /// <param name="lower">Lower CI bound.</param>
    /// <param name="upper">Upper CI bound.</param>
    /// <param name="point">Point estimate.</param>
    /// <returns>Asymmetry ratio, or NaN if degenerate.</returns>
    public static double ComputeAsymmetryRatio(double lower, double upper, double point)
    {
        double denom = point - lower;
        if (Math.Abs(denom) < 1e-12) return double.NaN;
        return (upper - point) / denom;
    }

    /// <summary>
    /// Computes leave-one-out jackknife parameter estimates for a given distribution type.
    /// Each iteration removes one observation, re-estimates via MOM, and records the parameter value.
    /// </summary>
    /// <param name="sampleData">The original data sample.</param>
    /// <param name="paramIndex">Index of the parameter in GetParameters.</param>
    /// <param name="createAndEstimate">
    /// Factory function: given a sample list, creates a distribution, estimates via MOM,
    /// and returns the GetParameters array. Returns null if estimation fails.
    /// </param>
    /// <returns>Array of N jackknife estimates (NaN for failed fits).</returns>
    public static double[] ComputeJackknifeEstimates(
        IList<double> sampleData,
        int paramIndex,
        Func<List<double>, double[]?> createAndEstimate)
    {
        int N = sampleData.Count;
        var jackEstimates = new double[N];

        for (int j = 0; j < N; j++)
        {
            var jackSample = new List<double>(sampleData);
            jackSample.RemoveAt(j);

            try
            {
                var parameters = createAndEstimate(jackSample);
                jackEstimates[j] = parameters != null ? parameters[paramIndex] : double.NaN;
            }
            catch
            {
                jackEstimates[j] = double.NaN;
            }
        }

        return jackEstimates;
    }

    #endregion
}
