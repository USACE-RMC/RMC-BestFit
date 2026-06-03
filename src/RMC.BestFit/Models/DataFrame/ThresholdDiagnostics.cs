using Numerics.Distributions;

namespace RMC.BestFit.Models
{
    /// <summary>
    /// Provides threshold selection diagnostics for Peaks-Over-Threshold (POT) analysis.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class implements two standard diagnostic tools for selecting an appropriate threshold
    /// in POT / exceedance modeling:
    /// </para>
    /// <list type="number">
    /// <item><description>
    /// <b>Mean Residual Life Plot (MRL)</b> — plots the sample mean of excesses above each candidate
    /// threshold. Under a valid GPD model, the mean excess function is linear in the threshold.
    /// The user should look for the lowest threshold above which the plot is approximately linear.
    /// </description></item>
    /// <item><description>
    /// <b>Parameter Stability Plot</b> — fits the Generalized Pareto Distribution (GPD) by maximum
    /// likelihood at each candidate threshold and plots the modified scale (sigma* = alpha - kappa * u)
    /// and shape (kappa) parameters. Under a valid GPD model, both should be approximately constant
    /// above the true threshold.
    /// </description></item>
    /// </list>
    /// <para>
    /// References:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Coles, S. (2001). <i>An Introduction to Statistical Modelling of Extreme Values</i>. Springer. Section 4.3.</description></item>
    /// <item><description>Davison, A.C. and Smith, R.L. (1990). "Models for Exceedances over High Thresholds." <i>JRSS-B</i>, 52(3), 393-442.</description></item>
    /// <item><description>R POT package: <c>mrlplot()</c>, <c>tcplot()</c>. https://cran.r-project.org/package=POT</description></item>
    /// </list>
    /// </remarks>
    public static class ThresholdDiagnostics
    {
        /// <summary>
        /// The minimum number of exceedances required to compute a mean residual life point.
        /// </summary>
        private const int MinExceedancesForMRL = 5;

        /// <summary>
        /// The minimum number of exceedances required to fit a GPD for parameter stability analysis.
        /// </summary>
        private const int MinExceedancesForGPD = 10;

        /// <summary>
        /// Computes the Mean Residual Life (MRL) plot data for a range of candidate thresholds.
        /// </summary>
        /// <param name="data">The complete data series (e.g., time series values). Must not be null or empty.</param>
        /// <param name="uMin">The minimum candidate threshold value.</param>
        /// <param name="uMax">The maximum candidate threshold value. Must be greater than <paramref name="uMin"/>.</param>
        /// <param name="nThresholds">The number of equally spaced candidate thresholds to evaluate. Default is 100.</param>
        /// <param name="confidenceLevel">The confidence level for the confidence interval (e.g., 0.95 for 95% CI). Default is 0.95.</param>
        /// <returns>A <see cref="MeanResidualLifeResult"/> containing the computed MRL points.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="data"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="data"/> is empty,
        /// <paramref name="uMax"/> is not greater than <paramref name="uMin"/>,
        /// <paramref name="nThresholds"/> is less than 2, or
        /// <paramref name="confidenceLevel"/> is not in (0, 1).</exception>
        /// <remarks>
        /// <para>
        /// For each candidate threshold u, the method computes the sample mean of excesses (x_i - u)
        /// for all x_i > u. Under a valid Generalized Pareto model, E[X - u | X > u] is linear in u.
        /// Confidence intervals are computed using the Central Limit Theorem:
        /// </para>
        /// <para>
        /// CI = mean_excess +/- z * sd / sqrt(n_u)
        /// </para>
        /// <para>
        /// where z is the standard normal quantile and n_u is the number of exceedances.
        /// Thresholds with fewer than 5 exceedances are skipped.
        /// </para>
        /// <para>
        /// Reference: R POT package <c>mrlplot()</c>; Coles (2001), Section 4.3.1.
        /// </para>
        /// </remarks>
        public static MeanResidualLifeResult ComputeMeanResidualLife(
            IList<double> data, double uMin, double uMax,
            int nThresholds = 100, double confidenceLevel = 0.95)
        {
            ValidateInputs(data, uMin, uMax, nThresholds, confidenceLevel);

            double z = Normal.StandardZ((1.0 + confidenceLevel) / 2.0);
            double step = (uMax - uMin) / (nThresholds - 1);
            var points = new List<MRLPoint>();

            for (int i = 0; i < nThresholds; i++)
            {
                double u = uMin + i * step;
                var excesses = ComputeExcesses(data, u);
                int nU = excesses.Count;

                if (nU < MinExceedancesForMRL)
                    continue;

                double meanExcess = ComputeMean(excesses);
                double sd = ComputeStandardDeviation(excesses, meanExcess);
                double se = sd / Math.Sqrt(nU);

                points.Add(new MRLPoint(
                    threshold: u,
                    meanExcess: meanExcess,
                    lowerCI: meanExcess - z * se,
                    upperCI: meanExcess + z * se,
                    exceedanceCount: nU));
            }

            return new MeanResidualLifeResult(points);
        }

        /// <summary>
        /// Computes the GPD parameter stability plot data for a range of candidate thresholds.
        /// </summary>
        /// <param name="data">The complete data series (e.g., time series values). Must not be null or empty.</param>
        /// <param name="uMin">The minimum candidate threshold value.</param>
        /// <param name="uMax">The maximum candidate threshold value. Must be greater than <paramref name="uMin"/>.</param>
        /// <param name="nThresholds">The number of equally spaced candidate thresholds to evaluate. Default is 50.</param>
        /// <param name="confidenceLevel">The confidence level for the confidence interval (e.g., 0.95 for 95% CI). Default is 0.95.</param>
        /// <returns>A <see cref="ParameterStabilityResult"/> containing the computed stability points.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="data"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="data"/> is empty,
        /// <paramref name="uMax"/> is not greater than <paramref name="uMin"/>,
        /// <paramref name="nThresholds"/> is less than 2, or
        /// <paramref name="confidenceLevel"/> is not in (0, 1).</exception>
        /// <remarks>
        /// <para>
        /// For each candidate threshold u, the method fits a Generalized Pareto Distribution (GPD)
        /// to the excesses (x_i - u) by maximum likelihood estimation. Two quantities are plotted:
        /// </para>
        /// <list type="bullet">
        /// <item><description>
        /// <b>Modified scale:</b> sigma* = alpha_hat - kappa_hat * u. Under a valid GPD model,
        /// sigma* should be constant across thresholds. The standard error is computed via the
        /// delta method: se(sigma*) = sqrt(Var(alpha) - 2*u*Cov(alpha, kappa) + u^2*Var(kappa)).
        /// </description></item>
        /// <item><description>
        /// <b>Shape:</b> kappa_hat. Under a valid GPD model, the shape parameter should be
        /// constant across thresholds.
        /// </description></item>
        /// </list>
        /// <para>
        /// Thresholds with fewer than 10 exceedances are skipped. Thresholds where MLE fails
        /// (e.g., due to numerical issues at extreme thresholds) are also skipped.
        /// </para>
        /// <para>
        /// Reference: R POT package <c>tcplot()</c>; Coles (2001), Section 4.3.2.
        /// </para>
        /// </remarks>
        public static ParameterStabilityResult ComputeParameterStability(
            IList<double> data, double uMin, double uMax,
            int nThresholds = 50, double confidenceLevel = 0.95)
        {
            ValidateInputs(data, uMin, uMax, nThresholds, confidenceLevel);

            double z = Normal.StandardZ((1.0 + confidenceLevel) / 2.0);
            double step = (uMax - uMin) / (nThresholds - 1);
            var points = new List<StabilityPoint>();

            for (int i = 0; i < nThresholds; i++)
            {
                double u = uMin + i * step;
                var excesses = ComputeExcesses(data, u);
                int nU = excesses.Count;

                if (nU < MinExceedancesForGPD)
                    continue;

                try
                {
                    // Fit GPD by MLE to the exceedances
                    var gpd = new GeneralizedPareto();
                    gpd.Estimate(excesses.ToArray(), ParameterEstimationMethod.MaximumLikelihood);

                    double alpha = gpd.Alpha;  // scale
                    double kappa = gpd.Kappa;  // shape

                    // Get the parameter covariance matrix (3x3)
                    // [0,0]=Var(xi), [1,1]=Var(alpha), [2,2]=Var(kappa), [1,2]=Cov(alpha,kappa)
                    var covar = gpd.ParameterCovariance(nU, ParameterEstimationMethod.MaximumLikelihood);

                    double varAlpha = covar[1, 1];
                    double varKappa = covar[2, 2];
                    double covAlphaKappa = covar[1, 2];

                    // Modified scale: sigma* = alpha - kappa * u
                    double modifiedScale = alpha - kappa * u;

                    // Standard error of sigma* via delta method:
                    // se(sigma*) = sqrt(Var(alpha) - 2*u*Cov(alpha,kappa) + u^2*Var(kappa))
                    double varStar = varAlpha - 2.0 * u * covAlphaKappa + u * u * varKappa;

                    // Guard against numerical issues yielding negative variance
                    double seStar = varStar > 0 ? Math.Sqrt(varStar) : 0.0;
                    double seKappa = varKappa > 0 ? Math.Sqrt(varKappa) : 0.0;

                    points.Add(new StabilityPoint(
                        threshold: u,
                        modifiedScale: modifiedScale,
                        modifiedScaleLowerCI: modifiedScale - z * seStar,
                        modifiedScaleUpperCI: modifiedScale + z * seStar,
                        shape: kappa,
                        shapeLowerCI: kappa - z * seKappa,
                        shapeUpperCI: kappa + z * seKappa,
                        exceedanceCount: nU));
                }
                catch
                {
                    // MLE may fail for extreme thresholds — skip this threshold
                    System.Diagnostics.Debug.WriteLine($"ThresholdDiagnostics: GPD MLE failed at threshold u={u:F4} with {nU} exceedances.");
                    continue;
                }
            }

            return new ParameterStabilityResult(points);
        }

        /// <summary>
        /// Validates the common input parameters for threshold diagnostic computations.
        /// </summary>
        /// <param name="data">The data series to validate.</param>
        /// <param name="uMin">The minimum threshold.</param>
        /// <param name="uMax">The maximum threshold.</param>
        /// <param name="nThresholds">The number of thresholds.</param>
        /// <param name="confidenceLevel">The confidence level.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="data"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when inputs are invalid.</exception>
        private static void ValidateInputs(IList<double> data, double uMin, double uMax, int nThresholds, double confidenceLevel)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data), "Data must not be null.");
            if (data.Count == 0)
                throw new ArgumentException("Data must not be empty.", nameof(data));
            if (uMax <= uMin)
                throw new ArgumentException($"uMax ({uMax}) must be greater than uMin ({uMin}).", nameof(uMax));
            if (nThresholds < 2)
                throw new ArgumentException("nThresholds must be at least 2.", nameof(nThresholds));
            if (confidenceLevel <= 0.0 || confidenceLevel >= 1.0)
                throw new ArgumentException("confidenceLevel must be in the open interval (0, 1).", nameof(confidenceLevel));
        }

        /// <summary>
        /// Computes the list of exceedances (x_i - u) for all data values strictly greater than the threshold.
        /// </summary>
        /// <param name="data">The data series.</param>
        /// <param name="threshold">The threshold value u.</param>
        /// <returns>A list of excess values above the threshold.</returns>
        private static List<double> ComputeExcesses(IList<double> data, double threshold)
        {
            var excesses = new List<double>();
            for (int i = 0; i < data.Count; i++)
            {
                if (data[i] > threshold)
                    excesses.Add(data[i] - threshold);
            }
            return excesses;
        }

        /// <summary>
        /// Computes the arithmetic mean of a list of values.
        /// </summary>
        /// <param name="values">The values to average. Must not be empty.</param>
        /// <returns>The arithmetic mean.</returns>
        private static double ComputeMean(List<double> values)
        {
            double sum = 0;
            for (int i = 0; i < values.Count; i++)
                sum += values[i];
            return sum / values.Count;
        }

        /// <summary>
        /// Computes the sample standard deviation of a list of values given a precomputed mean.
        /// </summary>
        /// <param name="values">The values. Must have at least 2 elements for a meaningful result.</param>
        /// <param name="mean">The precomputed mean of the values.</param>
        /// <returns>The sample standard deviation (using Bessel's correction, n-1 denominator).
        /// Returns 0 if fewer than 2 values are provided.</returns>
        private static double ComputeStandardDeviation(List<double> values, double mean)
        {
            if (values.Count < 2)
                return 0.0;

            double sumSq = 0;
            for (int i = 0; i < values.Count; i++)
            {
                double diff = values[i] - mean;
                sumSq += diff * diff;
            }
            return Math.Sqrt(sumSq / (values.Count - 1));
        }
    }

    /// <summary>
    /// Represents a single point on a Mean Residual Life (MRL) plot.
    /// </summary>
    /// <remarks>
    /// Each point corresponds to one candidate threshold value and contains the sample mean
    /// of excesses above that threshold, along with confidence bounds computed using the
    /// Central Limit Theorem.
    /// </remarks>
    public class MRLPoint
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MRLPoint"/> class.
        /// </summary>
        /// <param name="threshold">The candidate threshold value u.</param>
        /// <param name="meanExcess">The sample mean of exceedances (x_i - u) for x_i > u.</param>
        /// <param name="lowerCI">The lower bound of the confidence interval.</param>
        /// <param name="upperCI">The upper bound of the confidence interval.</param>
        /// <param name="exceedanceCount">The number of observations exceeding the threshold.</param>
        public MRLPoint(double threshold, double meanExcess, double lowerCI, double upperCI, int exceedanceCount)
        {
            Threshold = threshold;
            MeanExcess = meanExcess;
            LowerCI = lowerCI;
            UpperCI = upperCI;
            ExceedanceCount = exceedanceCount;
        }

        /// <summary>
        /// Gets the candidate threshold value u.
        /// </summary>
        public double Threshold { get; }

        /// <summary>
        /// Gets the sample mean of exceedances (x_i - u) for all x_i > u.
        /// </summary>
        public double MeanExcess { get; }

        /// <summary>
        /// Gets the lower bound of the confidence interval for the mean excess.
        /// </summary>
        public double LowerCI { get; }

        /// <summary>
        /// Gets the upper bound of the confidence interval for the mean excess.
        /// </summary>
        public double UpperCI { get; }

        /// <summary>
        /// Gets the number of observations exceeding the threshold.
        /// </summary>
        public int ExceedanceCount { get; }
    }

    /// <summary>
    /// Contains the results of a Mean Residual Life (MRL) computation across a range of thresholds.
    /// </summary>
    /// <remarks>
    /// The MRL plot is a standard diagnostic for selecting a threshold in Peaks-Over-Threshold analysis.
    /// Under a valid Generalized Pareto model, the mean excess function E[X - u | X > u] is linear
    /// in u. The user should select the lowest threshold above which the plot appears approximately linear.
    /// <para>
    /// Reference: Coles, S. (2001). <i>An Introduction to Statistical Modelling of Extreme Values</i>.
    /// Springer. Section 4.3.1.
    /// </para>
    /// </remarks>
    public class MeanResidualLifeResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MeanResidualLifeResult"/> class.
        /// </summary>
        /// <param name="points">The list of MRL points for each evaluated threshold.</param>
        public MeanResidualLifeResult(List<MRLPoint> points)
        {
            Points = points ?? new List<MRLPoint>();
        }

        /// <summary>
        /// Gets the list of MRL points, one per evaluated threshold.
        /// </summary>
        /// <remarks>
        /// Points are ordered by increasing threshold value. Thresholds with fewer than 5 exceedances
        /// are excluded from the list.
        /// </remarks>
        public List<MRLPoint> Points { get; }
    }

    /// <summary>
    /// Represents a single point on a GPD parameter stability plot.
    /// </summary>
    /// <remarks>
    /// Each point corresponds to one candidate threshold value and contains the GPD parameters
    /// (modified scale and shape) estimated by maximum likelihood, along with confidence intervals
    /// derived from the asymptotic covariance matrix via the delta method.
    /// </remarks>
    public class StabilityPoint
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="StabilityPoint"/> class.
        /// </summary>
        /// <param name="threshold">The candidate threshold value u.</param>
        /// <param name="modifiedScale">The modified scale parameter: sigma* = alpha - kappa * u.</param>
        /// <param name="modifiedScaleLowerCI">The lower confidence bound for the modified scale.</param>
        /// <param name="modifiedScaleUpperCI">The upper confidence bound for the modified scale.</param>
        /// <param name="shape">The shape parameter kappa estimated by MLE.</param>
        /// <param name="shapeLowerCI">The lower confidence bound for the shape parameter.</param>
        /// <param name="shapeUpperCI">The upper confidence bound for the shape parameter.</param>
        /// <param name="exceedanceCount">The number of observations exceeding the threshold.</param>
        public StabilityPoint(double threshold, double modifiedScale, double modifiedScaleLowerCI, double modifiedScaleUpperCI,
            double shape, double shapeLowerCI, double shapeUpperCI, int exceedanceCount)
        {
            Threshold = threshold;
            ModifiedScale = modifiedScale;
            ModifiedScaleLowerCI = modifiedScaleLowerCI;
            ModifiedScaleUpperCI = modifiedScaleUpperCI;
            Shape = shape;
            ShapeLowerCI = shapeLowerCI;
            ShapeUpperCI = shapeUpperCI;
            ExceedanceCount = exceedanceCount;
        }

        /// <summary>
        /// Gets the candidate threshold value u.
        /// </summary>
        public double Threshold { get; }

        /// <summary>
        /// Gets the modified scale parameter: sigma* = alpha_hat - kappa_hat * u.
        /// </summary>
        /// <remarks>
        /// Under a valid GPD model, the modified scale should be approximately constant across thresholds.
        /// This re-parameterization removes the expected linear trend in the scale parameter, making it
        /// easier to visually assess threshold stability.
        /// </remarks>
        public double ModifiedScale { get; }

        /// <summary>
        /// Gets the lower bound of the confidence interval for the modified scale.
        /// </summary>
        /// <remarks>
        /// Computed via the delta method: se(sigma*) = sqrt(Var(alpha) - 2*u*Cov(alpha,kappa) + u^2*Var(kappa)).
        /// </remarks>
        public double ModifiedScaleLowerCI { get; }

        /// <summary>
        /// Gets the upper bound of the confidence interval for the modified scale.
        /// </summary>
        public double ModifiedScaleUpperCI { get; }

        /// <summary>
        /// Gets the GPD shape parameter (kappa) estimated by maximum likelihood.
        /// </summary>
        /// <remarks>
        /// Under a valid GPD model, the shape parameter should be approximately constant across thresholds.
        /// </remarks>
        public double Shape { get; }

        /// <summary>
        /// Gets the lower bound of the confidence interval for the shape parameter.
        /// </summary>
        public double ShapeLowerCI { get; }

        /// <summary>
        /// Gets the upper bound of the confidence interval for the shape parameter.
        /// </summary>
        public double ShapeUpperCI { get; }

        /// <summary>
        /// Gets the number of observations exceeding the threshold.
        /// </summary>
        public int ExceedanceCount { get; }
    }

    /// <summary>
    /// Contains the results of a GPD parameter stability computation across a range of thresholds.
    /// </summary>
    /// <remarks>
    /// The parameter stability plot is a standard diagnostic for threshold selection in POT analysis.
    /// Under a valid GPD model above the true threshold, the modified scale (sigma* = alpha - kappa * u)
    /// and shape (kappa) should be approximately constant. The user should select the lowest threshold
    /// above which both parameters appear stable.
    /// <para>
    /// Reference: Coles, S. (2001). <i>An Introduction to Statistical Modelling of Extreme Values</i>.
    /// Springer. Section 4.3.2.
    /// </para>
    /// </remarks>
    public class ParameterStabilityResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ParameterStabilityResult"/> class.
        /// </summary>
        /// <param name="points">The list of stability points for each evaluated threshold.</param>
        public ParameterStabilityResult(List<StabilityPoint> points)
        {
            Points = points ?? new List<StabilityPoint>();
        }

        /// <summary>
        /// Gets the list of stability points, one per evaluated threshold.
        /// </summary>
        /// <remarks>
        /// Points are ordered by increasing threshold value. Thresholds with fewer than 10 exceedances
        /// or where MLE failed are excluded from the list.
        /// </remarks>
        public List<StabilityPoint> Points { get; }
    }
}
