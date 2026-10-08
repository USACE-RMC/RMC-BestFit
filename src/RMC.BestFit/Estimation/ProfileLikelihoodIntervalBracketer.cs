using Numerics.Mathematics.RootFinding;

namespace RMC.BestFit.Estimation;

/// <summary>
/// Finds finite, model-constrained threshold crossings for likelihood-profile intervals.
/// </summary>
/// <remarks>
/// The helper probes outward from an estimated parameter value without evaluating a nonfinite model
/// bound. Once two finite profile evaluations straddle the threshold, it verifies that finite bracket
/// with <see cref="Brent.Bracket"/> and solves it with <see cref="Brent.Solve"/> using their existing
/// defaults. A finite model bound is returned unchanged when its finite profile value does not cross
/// the threshold; unavailable bounds instead fail explicitly rather than being passed to Brent.
/// </remarks>
internal static class ProfileLikelihoodIntervalBracketer
{
    private const int MaximumFiniteProbeCount = 64;
    private const double ProbeExpansionFactor = 1.6d;

    /// <summary>
    /// Finds one side of a likelihood-profile threshold crossing.
    /// </summary>
    /// <param name="profileDifference">Returns the profiled objective minus its threshold.</param>
    /// <param name="estimate">The finite estimated parameter value at the center of the search.</param>
    /// <param name="modelBound">The model bound on the requested side, which may be nonfinite.</param>
    /// <param name="isLower">True to search from the estimate toward a lower bound; otherwise searches upward.</param>
    /// <returns>The finite threshold crossing, or the finite model bound when no crossing occurs before it.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="profileDifference"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the estimate or finite model bound is invalid for the requested side.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a finite profile value or finite threshold-crossing bracket cannot be found.</exception>
    /// <remarks>
    /// The profile function is evaluated only at finite coordinates inside the requested model-bound
    /// interval. This protects profile interval construction when an exact parameter bound is infinite
    /// or when evaluating the profile at that exact bound is not supported by the model.
    /// </remarks>
    internal static double FindThresholdCrossing(
        Func<double, double> profileDifference,
        double estimate,
        double modelBound,
        bool isLower)
    {
        ArgumentNullException.ThrowIfNull(profileDifference);

        if (!double.IsFinite(estimate))
            throw new ArgumentOutOfRangeException(nameof(estimate), "The estimated parameter value must be finite.");

        if (double.IsFinite(modelBound) && (isLower ? modelBound > estimate : modelBound < estimate))
        {
            throw new ArgumentOutOfRangeException(
                nameof(modelBound),
                "The model bound must lie on the requested side of the estimated parameter value.");
        }

        if (modelBound == estimate)
            return estimate;

        double estimateDifference = EvaluateFiniteProfileDifference(profileDifference, estimate);
        if (estimateDifference == 0d)
            return estimate;

        double distanceToFiniteBound = double.IsFinite(modelBound)
            ? Math.Abs(modelBound - estimate)
            : double.PositiveInfinity;
        double step = GetInitialProbeStep(estimate, distanceToFiniteBound);
        double lastFiniteValue = estimate;
        double lastFiniteDifference = estimateDifference;

        for (int probe = 0; probe < MaximumFiniteProbeCount; probe++)
        {
            double candidate = isLower ? estimate - step : estimate + step;
            if (!double.IsFinite(candidate))
                break;

            bool reachedFiniteBound = false;
            if (double.IsFinite(modelBound))
            {
                if (isLower && candidate < modelBound)
                {
                    candidate = modelBound;
                    reachedFiniteBound = true;
                }
                else if (!isLower && candidate > modelBound)
                {
                    candidate = modelBound;
                    reachedFiniteBound = true;
                }
            }

            double candidateDifference;
            try
            {
                candidateDifference = EvaluateFiniteProfileDifference(profileDifference, candidate);
            }
            catch (InvalidOperationException exception)
            {
                return BackOffFromUnavailableProbe(
                    profileDifference,
                    lastFiniteValue,
                    lastFiniteDifference,
                    candidate,
                    modelBound,
                    isLower,
                    exception);
            }

            if (candidateDifference == 0d)
                return candidate;

            if (HaveOppositeSigns(lastFiniteDifference, candidateDifference))
            {
                return SolveFiniteBracket(
                    profileDifference,
                    lastFiniteValue,
                    lastFiniteDifference,
                    candidate,
                    candidateDifference,
                    modelBound,
                    isLower);
            }

            if (reachedFiniteBound)
                return modelBound;

            lastFiniteValue = candidate;
            lastFiniteDifference = candidateDifference;
            step *= ProbeExpansionFactor;
        }

        throw new InvalidOperationException(
            "A finite likelihood-profile threshold-crossing bracket could not be found within the model bounds.");
    }

    /// <summary>
    /// Evaluates a profile difference and rejects unavailable objective values.
    /// </summary>
    /// <param name="profileDifference">Returns the profiled objective minus its threshold.</param>
    /// <param name="value">The finite model-constrained parameter value to evaluate.</param>
    /// <returns>The finite profile difference.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the profile evaluation throws or produces a nonfinite result.</exception>
    /// <remarks>
    /// This conversion keeps nonfinite profile responses out of the root bracketing and solving APIs.
    /// </remarks>
    private static double EvaluateFiniteProfileDifference(Func<double, double> profileDifference, double value)
    {
        try
        {
            double difference = profileDifference(value);
            if (!double.IsFinite(difference))
                throw new InvalidOperationException("The profile evaluation did not return a finite value.");

            return difference;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException("The profile evaluation could not be completed.", exception);
        }
    }

    /// <summary>
    /// Backs off from an unavailable outward probe to recover a finite threshold-crossing bracket.
    /// </summary>
    /// <param name="profileDifference">Returns the profiled objective minus its threshold.</param>
    /// <param name="lastFiniteValue">The outermost finite profile coordinate evaluated so far.</param>
    /// <param name="lastFiniteDifference">The finite profile difference at <paramref name="lastFiniteValue"/>.</param>
    /// <param name="unavailableValue">The finite coordinate whose profile evaluation was unavailable.</param>
    /// <param name="modelBound">The model bound on the requested side.</param>
    /// <param name="isLower">True when the search proceeds downward; otherwise it proceeds upward.</param>
    /// <param name="unavailableException">The unavailable probe failure retained as diagnostic context.</param>
    /// <returns>The finite threshold crossing found before the unavailable coordinate.</returns>
    /// <exception cref="InvalidOperationException">Thrown when finite back-off cannot produce a threshold-crossing bracket.</exception>
    /// <remarks>
    /// The interval is bisected toward the unavailable coordinate. Each unavailable midpoint becomes the
    /// new outer edge; each finite midpoint becomes the new inner edge until a finite sign change is
    /// available to Brent. This preserves the model-constrained search direction for either interval side.
    /// </remarks>
    private static double BackOffFromUnavailableProbe(
        Func<double, double> profileDifference,
        double lastFiniteValue,
        double lastFiniteDifference,
        double unavailableValue,
        double modelBound,
        bool isLower,
        Exception unavailableException)
    {
        for (int probe = 0; probe < MaximumFiniteProbeCount; probe++)
        {
            double midpoint = 0.5d * lastFiniteValue + 0.5d * unavailableValue;
            if (!double.IsFinite(midpoint) || midpoint == lastFiniteValue || midpoint == unavailableValue)
                break;

            double midpointDifference;
            try
            {
                midpointDifference = EvaluateFiniteProfileDifference(profileDifference, midpoint);
            }
            catch (InvalidOperationException)
            {
                unavailableValue = midpoint;
                continue;
            }

            if (midpointDifference == 0d)
                return midpoint;

            if (HaveOppositeSigns(lastFiniteDifference, midpointDifference))
            {
                return SolveFiniteBracket(
                    profileDifference,
                    lastFiniteValue,
                    lastFiniteDifference,
                    midpoint,
                    midpointDifference,
                    modelBound,
                    isLower);
            }

            lastFiniteValue = midpoint;
            lastFiniteDifference = midpointDifference;
        }

        throw new InvalidOperationException(
            "A finite likelihood-profile threshold-crossing bracket could not be found within the model bounds.",
            unavailableException);
    }

    /// <summary>
    /// Gets the first finite outward probe magnitude.
    /// </summary>
    /// <param name="estimate">The finite estimated parameter value.</param>
    /// <param name="distanceToFiniteBound">The finite distance to a model bound, or positive infinity.</param>
    /// <returns>A positive finite probe magnitude.</returns>
    /// <remarks>
    /// A scale-relative probe avoids starting at zero while a finite model-bound distance prevents the
    /// first evaluation from jumping directly to a possibly unavailable endpoint.
    /// </remarks>
    private static double GetInitialProbeStep(double estimate, double distanceToFiniteBound)
    {
        double scale = Math.Max(1d, Math.Abs(estimate) * 0.1d);
        return double.IsFinite(distanceToFiniteBound)
            ? Math.Min(scale, distanceToFiniteBound * 0.5d)
            : scale;
    }

    /// <summary>
    /// Determines whether two finite values straddle zero.
    /// </summary>
    /// <param name="first">The first finite value.</param>
    /// <param name="second">The second finite value.</param>
    /// <returns>True when the values have opposite signs.</returns>
    /// <remarks>
    /// Sign comparison avoids multiplying values, which can underflow before the bracketing decision.
    /// </remarks>
    private static bool HaveOppositeSigns(double first, double second)
    {
        return (first < 0d && second > 0d) || (first > 0d && second < 0d);
    }

    /// <summary>
    /// Verifies and solves a finite threshold-crossing bracket.
    /// </summary>
    /// <param name="profileDifference">Returns the profiled objective minus its threshold.</param>
    /// <param name="estimate">The finite estimated parameter value.</param>
    /// <param name="estimateDifference">The already observed finite profile difference at <paramref name="estimate"/>.</param>
    /// <param name="candidate">The finite probe on the requested side that straddles the threshold.</param>
    /// <param name="candidateDifference">The already observed finite profile difference at <paramref name="candidate"/>.</param>
    /// <param name="modelBound">The model bound on the requested side.</param>
    /// <param name="isLower">True when the candidate is below the estimate; otherwise it is above.</param>
    /// <returns>The finite root returned by Brent's solver.</returns>
    /// <exception cref="InvalidOperationException">Thrown when Brent cannot retain a finite model-constrained bracket or root.</exception>
    /// <remarks>
    /// <see cref="Brent.Bracket"/> is applied only after finite straddling endpoints have been found, so
    /// its expansion loop returns before moving either endpoint outside the model-constrained interval.
    /// </remarks>
    private static double SolveFiniteBracket(
        Func<double, double> profileDifference,
        double estimate,
        double estimateDifference,
        double candidate,
        double candidateDifference,
        double modelBound,
        bool isLower)
    {
        double lower = isLower ? candidate : estimate;
        double upper = isLower ? estimate : candidate;
        double lowerDifference = isLower ? candidateDifference : estimateDifference;
        double upperDifference = isLower ? estimateDifference : candidateDifference;
        double initialLower = lower;
        double initialUpper = upper;

        double FiniteBracketDifference(double value)
        {
            if (value == initialLower)
                return lowerDifference;
            if (value == initialUpper)
                return upperDifference;

            if (!double.IsFinite(value) || value < initialLower || value > initialUpper)
            {
                throw new InvalidOperationException(
                    "Brent attempted to evaluate the likelihood profile outside its finite model-constrained bracket.");
            }

            return EvaluateFiniteProfileDifference(profileDifference, value);
        }

        bool bracketed = Brent.Bracket(FiniteBracketDifference, ref lower, ref upper, out _, out _);
        if (!bracketed || !double.IsFinite(lower) || !double.IsFinite(upper) ||
            (double.IsFinite(modelBound) && (isLower ? lower < modelBound : upper > modelBound)))
        {
            throw new InvalidOperationException(
                "A finite likelihood-profile threshold-crossing bracket could not be retained within the model bounds.");
        }

        double root = Brent.Solve(FiniteBracketDifference, lower, upper);
        if (!double.IsFinite(root) ||
            (double.IsFinite(modelBound) && (isLower ? root < modelBound || root > estimate : root > modelBound || root < estimate)))
        {
            throw new InvalidOperationException(
                "Brent did not return a finite likelihood-profile threshold crossing within the model bounds.");
        }

        return root;
    }
}
