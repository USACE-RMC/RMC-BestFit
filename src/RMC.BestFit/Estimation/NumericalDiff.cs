using System;
using Numerics.Mathematics.LinearAlgebra;

namespace RMC.BestFit.Estimation
{
    /// <summary>
    /// Centralized numerical differentiation with adaptive step sizing and flat-spot detection.
    /// </summary>
    /// <remarks>
    /// <para>
    /// All methods use the (|θ| + 1) × RelStep formula for initial step sizes, matching the
    /// Numerics library convention. When a flat spot is detected (derivative effectively zero
    /// due to regime-switching discontinuities), the step is automatically escalated.
    /// </para>
    /// <para>
    /// This addresses distributions like LP3, PT3, GEV, GP, GN, and GL that switch to
    /// limit-form approximations when shape/skew parameters cross the NearZero = 1E-4
    /// threshold (defined in UnivariateDistributionBase).
    /// </para>
    /// </remarks>
    internal static class NumericalDiff
    {
        #region Constants

        /// <summary>
        /// Default relative step size for finite differences.
        /// </summary>
        internal const double DefaultRelStep = 1e-4;

        /// <summary>
        /// Default absolute minimum step size.
        /// </summary>
        internal const double DefaultAbsStep = 1e-8;

        /// <summary>
        /// Maximum step size for flat-spot retry escalation.
        /// </summary>
        internal const double MaxStep = 1e-2;

        /// <summary>
        /// Relative tolerance for detecting flat spots. If the central difference
        /// |f(θ+h) − f(θ−h)| is less than this fraction of the function scale,
        /// the derivative is considered effectively zero and the step is escalated.
        /// </summary>
        internal const double FlatSpotRelTol = 1e-12;

        /// <summary>
        /// Factor by which to multiply the step size on each flat-spot retry.
        /// </summary>
        internal const double StepGrowthFactor = 4.0;

        #endregion

        #region Step Size Computation

        /// <summary>
        /// Computes the initial step size for a single parameter.
        /// </summary>
        /// <param name="parameterValue">The current parameter value.</param>
        /// <returns>The initial step size h = max(RelStep × (|θ| + 1), AbsStep).</returns>
        /// <remarks>
        /// The (|θ| + 1) scale factor matches the Numerics library convention and ensures
        /// a minimum step of RelStep (1e-4) even when the parameter value is zero or very small.
        /// </remarks>
        internal static double InitialStep(double parameterValue)
        {
            return Math.Max(DefaultRelStep * (Math.Abs(parameterValue) + 1.0), DefaultAbsStep);
        }

        /// <summary>
        /// Computes initial step sizes for all parameters.
        /// </summary>
        /// <param name="parameters">The parameter values.</param>
        /// <returns>An array of initial step sizes, one per parameter.</returns>
        internal static double[] ComputeStepSizes(double[] parameters)
        {
            var h = new double[parameters.Length];
            for (int j = 0; j < parameters.Length; j++)
                h[j] = InitialStep(parameters[j]);
            return h;
        }

        #endregion

        #region Pointwise Gradients

        /// <summary>
        /// Computes pointwise gradients of a vector-valued function via central differences
        /// with flat-spot detection and step escalation.
        /// </summary>
        /// <param name="pointwiseFunc">Function mapping parameters to an array of n pointwise values
        /// (e.g., Model.PointwiseDataLogLikelihood).</param>
        /// <param name="parameters">The parameter values at which to evaluate.</param>
        /// <param name="n">The number of observations (length of pointwise array).</param>
        /// <param name="p">The number of parameters.</param>
        /// <returns>A jagged array [n][p] where result[i][j] = ∂f_i/∂θ_j.</returns>
        /// <remarks>
        /// Uses the central difference formula: ∂f/∂θⱼ ≈ [f(θ+hⱼeⱼ) − f(θ−hⱼeⱼ)] / (2hⱼ).
        /// When the total change across all observations is below FlatSpotRelTol, the step
        /// is multiplied by StepGrowthFactor and retried up to MaxStep.
        /// </remarks>
        internal static double[][] ComputePointwiseGradients(
            Func<double[], double[]> pointwiseFunc,
            double[] parameters, int n, int p)
        {
            var gradients = new double[n][];
            for (int i = 0; i < n; i++)
                gradients[i] = new double[p];

            var perturbed = (double[])parameters.Clone();

            for (int j = 0; j < p; j++)
            {
                double h = InitialStep(parameters[j]);
                double[]? forwardLL = null;
                double[]? backwardLL = null;

                while (h <= MaxStep)
                {
                    perturbed[j] = parameters[j] + h;
                    forwardLL = pointwiseFunc(perturbed);

                    perturbed[j] = parameters[j] - h;
                    backwardLL = pointwiseFunc(perturbed);

                    perturbed[j] = parameters[j];

                    if (!IsPointwiseFlat(forwardLL, backwardLL, n))
                        break;

                    h *= StepGrowthFactor;
                }

                double effectiveH = Math.Min(h, MaxStep);
                if (forwardLL != null && backwardLL != null)
                {
                    for (int i = 0; i < n; i++)
                        gradients[i][j] = (forwardLL[i] - backwardLL[i]) / (2.0 * effectiveH);
                }
            }

            return gradients;
        }

        /// <summary>
        /// Determines whether the pointwise central difference values indicate a flat spot.
        /// </summary>
        private static bool IsPointwiseFlat(double[] forward, double[] backward, int n)
        {
            double totalDiff = 0;
            double totalScale = 0;
            for (int i = 0; i < n; i++)
            {
                if (!IsFinite(forward[i]) || !IsFinite(backward[i]))
                    return false;
                totalDiff += Math.Abs(forward[i] - backward[i]);
                totalScale += Math.Max(Math.Abs(forward[i]), Math.Abs(backward[i]));
            }
            return totalDiff < FlatSpotRelTol * Math.Max(totalScale, 1.0);
        }

        #endregion

        #region Scalar Hessian

        /// <summary>
        /// Computes the Hessian matrix of a scalar function via central differences
        /// with flat-spot detection and step escalation.
        /// </summary>
        /// <param name="function">Scalar function f: R^p → R.</param>
        /// <param name="parameters">The parameter values at which to evaluate.</param>
        /// <param name="p">The number of parameters.</param>
        /// <param name="lowerBounds">Optional lower bounds for parameters.</param>
        /// <param name="upperBounds">Optional upper bounds for parameters.</param>
        /// <returns>The p × p Hessian matrix of second partial derivatives.</returns>
        internal static Matrix ComputeHessian(
            Func<double[], double> function,
            double[] parameters, int p,
            double[]? lowerBounds = null,
            double[]? upperBounds = null)
        {
            var hessian = new Matrix(p, p);
            var perturbed = (double[])parameters.Clone();
            double f0 = function(perturbed);

            // Compute effective step sizes per parameter (with flat-spot escalation for diagonals)
            var h = new double[p];
            for (int j = 0; j < p; j++)
                h[j] = AdaptiveHessianDiagStep(function, parameters, perturbed, j, f0, lowerBounds, upperBounds);

            // Diagonal entries: H[j,j] = (f(θ+h) − 2f(θ) + f(θ−h)) / h²
            for (int j = 0; j < p; j++)
            {
                double fp = EvalPerturbed(function, perturbed, parameters[j] + h[j], j, lowerBounds, upperBounds);
                double fm = EvalPerturbed(function, perturbed, parameters[j] - h[j], j, lowerBounds, upperBounds);
                hessian[j, j] = (fp - 2.0 * f0 + fm) / (h[j] * h[j]);
                perturbed[j] = parameters[j];
            }

            // Off-diagonal entries: H[j,k] = (fpp − fpm − fmp + fmm) / (4hⱼhₖ)
            for (int j = 0; j < p; j++)
            {
                for (int k = j + 1; k < p; k++)
                {
                    perturbed[j] = Clamp(parameters[j] + h[j], j, lowerBounds, upperBounds);
                    perturbed[k] = Clamp(parameters[k] + h[k], k, lowerBounds, upperBounds);
                    double fpp = function(perturbed);

                    perturbed[k] = Clamp(parameters[k] - h[k], k, lowerBounds, upperBounds);
                    double fpm = function(perturbed);

                    perturbed[j] = Clamp(parameters[j] - h[j], j, lowerBounds, upperBounds);
                    perturbed[k] = Clamp(parameters[k] + h[k], k, lowerBounds, upperBounds);
                    double fmp = function(perturbed);

                    perturbed[k] = Clamp(parameters[k] - h[k], k, lowerBounds, upperBounds);
                    double fmm = function(perturbed);

                    double hjk = (fpp - fpm - fmp + fmm) / (4.0 * h[j] * h[k]);
                    hessian[j, k] = hjk;
                    hessian[k, j] = hjk;

                    perturbed[j] = parameters[j];
                    perturbed[k] = parameters[k];
                }
            }

            return hessian;
        }

        /// <summary>
        /// Computes an adaptive step size for the j-th diagonal Hessian entry,
        /// escalating through flat spots until curvature is detected.
        /// </summary>
        private static double AdaptiveHessianDiagStep(
            Func<double[], double> function,
            double[] parameters, double[] perturbed,
            int j, double f0,
            double[]? lowerBounds, double[]? upperBounds)
        {
            double h = InitialStep(parameters[j]);

            while (h <= MaxStep)
            {
                double fp = EvalPerturbed(function, perturbed, parameters[j] + h, j, lowerBounds, upperBounds);
                double fm = EvalPerturbed(function, perturbed, parameters[j] - h, j, lowerBounds, upperBounds);
                perturbed[j] = parameters[j];

                if (!IsFinite(fp) || !IsFinite(fm))
                {
                    h *= 0.5;
                    if (h < DefaultAbsStep * 0.01)
                        return InitialStep(parameters[j]);
                    continue;
                }

                double secondDeriv = Math.Abs(fp - 2.0 * f0 + fm);
                double scale = Math.Max(Math.Abs(fp), Math.Max(Math.Abs(fm), Math.Max(Math.Abs(f0), 1.0)));

                if (secondDeriv >= FlatSpotRelTol * scale)
                    return h;

                h *= StepGrowthFactor;
            }

            return Math.Min(h, MaxStep);
        }

        #endregion

        #region Scalar Gradient

        /// <summary>
        /// Computes the gradient of a scalar function via central differences with
        /// flat-spot detection, step escalation, and optional boundary handling.
        /// </summary>
        /// <param name="function">Scalar function f: R^p → R.</param>
        /// <param name="parameters">The parameter values at which to evaluate.</param>
        /// <param name="lowerBounds">Optional lower bounds for parameters.</param>
        /// <param name="upperBounds">Optional upper bounds for parameters.</param>
        /// <returns>The gradient vector where result[j] = ∂f/∂θ_j.</returns>
        internal static double[] ComputeGradient(
            Func<double[], double> function,
            double[] parameters,
            double[]? lowerBounds = null,
            double[]? upperBounds = null)
        {
            int p = parameters.Length;
            var grad = new double[p];
            var perturbed = (double[])parameters.Clone();
            double f0 = function(perturbed);

            for (int j = 0; j < p; j++)
            {
                double h = InitialStep(parameters[j]);

                while (h <= MaxStep)
                {
                    double roomLeft = AvailableLeft(parameters, j, lowerBounds);
                    double roomRight = AvailableRight(parameters, j, upperBounds);

                    double derivative;
                    bool success;
                    bool isFlat;

                    if (roomLeft >= h && roomRight >= h)
                    {
                        double fp = EvalPerturbed(function, perturbed, parameters[j] + h, j, lowerBounds, upperBounds);
                        double fm = EvalPerturbed(function, perturbed, parameters[j] - h, j, lowerBounds, upperBounds);
                        perturbed[j] = parameters[j];
                        success = IsFinite(fp) && IsFinite(fm);
                        if (success)
                        {
                            derivative = (fp - fm) / (2.0 * h);
                            double diff = Math.Abs(fp - fm);
                            double scale = Math.Max(Math.Abs(fp), Math.Max(Math.Abs(fm), 1.0));
                            isFlat = diff < FlatSpotRelTol * scale;
                        }
                        else { derivative = 0; isFlat = false; }
                    }
                    else if (roomRight >= h)
                    {
                        double fp = EvalPerturbed(function, perturbed, parameters[j] + h, j, lowerBounds, upperBounds);
                        perturbed[j] = parameters[j];
                        success = IsFinite(fp);
                        if (success)
                        {
                            derivative = (fp - f0) / h;
                            double diff = Math.Abs(fp - f0);
                            double scale = Math.Max(Math.Abs(fp), Math.Max(Math.Abs(f0), 1.0));
                            isFlat = diff < FlatSpotRelTol * scale;
                        }
                        else { derivative = 0; isFlat = false; }
                    }
                    else if (roomLeft >= h)
                    {
                        double fm = EvalPerturbed(function, perturbed, parameters[j] - h, j, lowerBounds, upperBounds);
                        perturbed[j] = parameters[j];
                        success = IsFinite(fm);
                        if (success)
                        {
                            derivative = (f0 - fm) / h;
                            double diff = Math.Abs(f0 - fm);
                            double scale = Math.Max(Math.Abs(fm), Math.Max(Math.Abs(f0), 1.0));
                            isFlat = diff < FlatSpotRelTol * scale;
                        }
                        else { derivative = 0; isFlat = false; }
                    }
                    else
                    {
                        h *= 0.5;
                        continue;
                    }

                    if (!success) { h *= 0.5; continue; }
                    if (!isFlat) { grad[j] = derivative; break; }
                    h *= StepGrowthFactor;
                }
            }

            return grad;
        }

        #endregion

        #region Jacobian

        /// <summary>
        /// Computes the Jacobian matrix of a vector-valued function via central differences
        /// with flat-spot detection, step escalation, and optional boundary handling.
        /// </summary>
        /// <param name="function">Vector-valued function g: R^p → R^m.</param>
        /// <param name="parameters">The parameter values at which to evaluate.</param>
        /// <param name="m">The number of output components (rows of Jacobian).</param>
        /// <param name="lowerBounds">Optional lower bounds for parameters.</param>
        /// <param name="upperBounds">Optional upper bounds for parameters.</param>
        /// <returns>An m × p Jacobian matrix where J[i,j] = ∂g_i/∂θ_j.</returns>
        internal static double[,] ComputeJacobian(
            Func<double[], double[]> function,
            double[] parameters, int m,
            double[]? lowerBounds = null,
            double[]? upperBounds = null)
        {
            int p = parameters.Length;
            var J = new double[m, p];
            var perturbed = (double[])parameters.Clone();
            double[] g0 = function(perturbed);

            for (int j = 0; j < p; j++)
            {
                double h = InitialStep(parameters[j]);
                double[]? col = null;

                while (h <= MaxStep)
                {
                    double roomLeft = AvailableLeft(parameters, j, lowerBounds);
                    double roomRight = AvailableRight(parameters, j, upperBounds);

                    bool success;
                    bool isFlat;

                    if (roomLeft >= h && roomRight >= h)
                    {
                        perturbed[j] = Clamp(parameters[j] + h, j, lowerBounds, upperBounds);
                        double[] gPlus = function(perturbed);
                        perturbed[j] = Clamp(parameters[j] - h, j, lowerBounds, upperBounds);
                        double[] gMinus = function(perturbed);
                        perturbed[j] = parameters[j];

                        if (IsBad(gPlus, m) || IsBad(gMinus, m))
                        { success = false; isFlat = false; }
                        else
                        {
                            success = true;
                            double totalDiff = 0, totalScale = 0;
                            col = new double[m];
                            double twoH = 2.0 * h;
                            for (int i = 0; i < m; i++)
                            {
                                col[i] = (gPlus[i] - gMinus[i]) / twoH;
                                totalDiff += Math.Abs(gPlus[i] - gMinus[i]);
                                totalScale += Math.Max(Math.Abs(gPlus[i]), Math.Abs(gMinus[i]));
                            }
                            isFlat = totalDiff < FlatSpotRelTol * Math.Max(totalScale, 1.0);
                        }
                    }
                    else if (roomRight >= h)
                    {
                        perturbed[j] = Clamp(parameters[j] + h, j, lowerBounds, upperBounds);
                        double[] gPlus = function(perturbed);
                        perturbed[j] = parameters[j];

                        if (IsBad(gPlus, m))
                        { success = false; isFlat = false; }
                        else
                        {
                            success = true;
                            double totalDiff = 0, totalScale = 0;
                            col = new double[m];
                            for (int i = 0; i < m; i++)
                            {
                                col[i] = (gPlus[i] - g0[i]) / h;
                                totalDiff += Math.Abs(gPlus[i] - g0[i]);
                                totalScale += Math.Max(Math.Abs(gPlus[i]), Math.Abs(g0[i]));
                            }
                            isFlat = totalDiff < FlatSpotRelTol * Math.Max(totalScale, 1.0);
                        }
                    }
                    else if (roomLeft >= h)
                    {
                        perturbed[j] = Clamp(parameters[j] - h, j, lowerBounds, upperBounds);
                        double[] gMinus = function(perturbed);
                        perturbed[j] = parameters[j];

                        if (IsBad(gMinus, m))
                        { success = false; isFlat = false; }
                        else
                        {
                            success = true;
                            double totalDiff = 0, totalScale = 0;
                            col = new double[m];
                            for (int i = 0; i < m; i++)
                            {
                                col[i] = (g0[i] - gMinus[i]) / h;
                                totalDiff += Math.Abs(g0[i] - gMinus[i]);
                                totalScale += Math.Max(Math.Abs(gMinus[i]), Math.Abs(g0[i]));
                            }
                            isFlat = totalDiff < FlatSpotRelTol * Math.Max(totalScale, 1.0);
                        }
                    }
                    else
                    {
                        h *= 0.5;
                        continue;
                    }

                    if (!success) { h *= 0.5; continue; }
                    if (!isFlat) break;
                    h *= StepGrowthFactor;
                }

                if (col == null)
                {
                    // No step size produced a usable Jacobian column. Flagging this
                    // (rather than silently writing zeros) lets the caller see that
                    // numerical differentiation failed for parameter j.
                    System.Diagnostics.Debug.WriteLine(
                        $"NumericalDiff.Jacobian: no usable step in [MinStep, MaxStep] for parameter {j}; column left as zeros.");
                    col = new double[m];
                }
                for (int i = 0; i < m; i++)
                    J[i, j] = col[i];
            }

            return J;
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Evaluates the function with parameter j set to the given value (clamped to bounds).
        /// Caller must reset perturbed[j] after use.
        /// </summary>
        private static double EvalPerturbed(
            Func<double[], double> function,
            double[] perturbed, double value, int j,
            double[]? lowerBounds, double[]? upperBounds)
        {
            perturbed[j] = Clamp(value, j, lowerBounds, upperBounds);
            return function(perturbed);
        }

        /// <summary>
        /// Clamps a value to the bounds for parameter j.
        /// </summary>
        private static double Clamp(double value, int j, double[]? lowerBounds, double[]? upperBounds)
        {
            if (lowerBounds != null && value < lowerBounds[j])
                value = lowerBounds[j];
            if (upperBounds != null && value > upperBounds[j])
                value = upperBounds[j];
            return value;
        }

        /// <summary>
        /// Calculates available space below the current parameter value.
        /// </summary>
        private static double AvailableLeft(double[] parameters, int j, double[]? lowerBounds)
        {
            return lowerBounds == null ? double.PositiveInfinity : parameters[j] - lowerBounds[j];
        }

        /// <summary>
        /// Calculates available space above the current parameter value.
        /// </summary>
        private static double AvailableRight(double[] parameters, int j, double[]? upperBounds)
        {
            return upperBounds == null ? double.PositiveInfinity : upperBounds[j] - parameters[j];
        }

        /// <summary>
        /// Checks if a value is finite (not NaN and not Infinity).
        /// </summary>
        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        /// <summary>
        /// Checks if a vector contains any non-finite values.
        /// </summary>
        private static bool IsBad(double[] v, int expectedLength)
        {
            if (v == null || v.Length != expectedLength)
                return true;
            for (int i = 0; i < expectedLength; i++)
            {
                if (double.IsNaN(v[i]) || double.IsInfinity(v[i]))
                    return true;
            }
            return false;
        }

        #endregion
    }
}
