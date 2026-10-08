namespace RMC.BestFit.Estimation;

/// <summary>
/// Supplies deterministic successful warm starts for likelihood-profile evaluations.
/// </summary>
/// <remarks>
/// Each successful profile evaluation contributes its optimized parameter vector at the fixed coordinate.
/// Later coordinates try those vectors in nearest-coordinate order, so an order-dependent optimizer failure
/// can retry another known successful start without relaxing the profile optimizer's convergence requirement.
/// </remarks>
internal sealed class ProfileLikelihoodWarmStartCache
{
    private readonly List<(double FixedValue, double[] Parameters)> _successfulStarts = new();

    /// <summary>
    /// Initializes the cache with the full parameter vector at the estimated coordinate.
    /// </summary>
    /// <param name="estimatedValue">The fixed parameter value at the estimated full parameter vector.</param>
    /// <param name="estimatedParameters">The successful full parameter vector at <paramref name="estimatedValue"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="estimatedParameters"/> is null.</exception>
    /// <remarks>
    /// The supplied vector is copied so caller mutation cannot change later profile warm starts.
    /// </remarks>
    internal ProfileLikelihoodWarmStartCache(double estimatedValue, IReadOnlyList<double> estimatedParameters)
    {
        ArgumentNullException.ThrowIfNull(estimatedParameters);
        RecordSuccessfulStart(estimatedValue, estimatedParameters);
    }

    /// <summary>
    /// Evaluates a profile coordinate using successful warm starts in deterministic nearest-first order.
    /// </summary>
    /// <param name="fixedValue">The coordinate at which the profile parameter is held fixed.</param>
    /// <param name="evaluator">Runs the strict profile optimization from a supplied successful start.</param>
    /// <returns>The finite-profile difference reported by the successful evaluator.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="evaluator"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when no cached successful warm start produces a strict profile solution.</exception>
    /// <remarks>
    /// A failed attempt remains excluded from the cache. The first successful result is copied into the
    /// cache at <paramref name="fixedValue"/>, allowing later Brent interior coordinates to start from
    /// the nearest known converged profile point rather than from a single mutable previous solve.
    /// </remarks>
    internal double Evaluate(
        double fixedValue,
        Func<IReadOnlyList<double>, (double Difference, double[] Parameters)> evaluator)
    {
        ArgumentNullException.ThrowIfNull(evaluator);

        InvalidOperationException? firstFailure = null;
        foreach (var start in _successfulStarts
            .OrderBy(start => Math.Abs(start.FixedValue - fixedValue))
            .ThenBy(start => start.FixedValue))
        {
            try
            {
                var result = evaluator(start.Parameters);
                RecordSuccessfulStart(fixedValue, result.Parameters);
                return result.Difference;
            }
            catch (InvalidOperationException exception)
            {
                firstFailure ??= exception;
            }
        }

        throw new InvalidOperationException(
            $"Unable to profile parameter at fixed value {fixedValue} from any successful warm start.",
            firstFailure);
    }

    /// <summary>
    /// Adds or replaces a successful warm start at one fixed coordinate.
    /// </summary>
    /// <param name="fixedValue">The coordinate associated with the successful parameter vector.</param>
    /// <param name="parameters">The successful full parameter vector to cache.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="parameters"/> is null.</exception>
    /// <remarks>
    /// Exact coordinate replacement bounds cache growth when a root finder revisits a profile coordinate.
    /// </remarks>
    private void RecordSuccessfulStart(double fixedValue, IReadOnlyList<double> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        double[] copiedParameters = parameters.ToArray();
        int existingIndex = _successfulStarts.FindIndex(start => start.FixedValue == fixedValue);
        if (existingIndex >= 0)
        {
            _successfulStarts[existingIndex] = (fixedValue, copiedParameters);
            return;
        }

        _successfulStarts.Add((fixedValue, copiedParameters));
    }
}
