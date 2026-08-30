using Microsoft.VisualStudio.TestTools.UnitTesting;
using Numerics;

namespace RMC.BestFit.Verification.Recovery;

/// <summary>
/// Contains the common, test-only acceptance rules for generated-parent recovery evidence.
/// </summary>
/// <remarks>
/// The methods assert only predeclared uncertainty statements. They do not calculate uncertainty,
/// select optimizers or samplers, alter seeds, or introduce a production dependency.
/// </remarks>
public static class RecoveryAcceptance
{
    /// <summary>Gets the two-sided standard-Normal 95 percent cutoff.</summary>
    public const double NinetyFivePercentStandardNormalCutoff = 1.96d;

    /// <summary>Gets the maximum permitted split-R-hat for a monitored Bayesian coordinate.</summary>
    public const double MaximumRhat = 1.10d;

    /// <summary>Gets the minimum effective sample size for a monitored Bayesian coordinate.</summary>
    public const double MinimumEffectiveSampleSize = 100d;

    /// <summary>Gets the conditional secondary point or curve criterion as a relative error.</summary>
    public const double SecondaryRelativeCriterion = 0.05d;

    /// <summary>
    /// Requires a frequentist 95 percent confidence or profile interval to include its generating parent.
    /// </summary>
    /// <param name="coordinate">Name of the fitted coordinate.</param>
    /// <param name="parent">Generating-parent value.</param>
    /// <param name="lower">Predeclared lower confidence or profile limit.</param>
    /// <param name="upper">Predeclared upper confidence or profile limit.</param>
    /// <remarks>
    /// This is the primary frequentist recovery rule. Callers must predeclare the interval source
    /// and width in their cell documentation; this helper only verifies finite, ordered limits and
    /// parent inclusion and does not select an interval method.
    /// </remarks>
    public static void AssertFrequentistParentInInterval(string coordinate, double parent, double lower, double upper)
    {
        Assert.IsTrue(Tools.IsFinite(lower) && Tools.IsFinite(upper) && lower <= upper,
            $"{coordinate} must provide a finite ordered 95% uncertainty interval.");
        Assert.IsTrue(lower <= parent && parent <= upper,
            $"The 95% interval [{lower:G17}, {upper:G17}] for {coordinate} does not contain parent {parent:G17}.");
    }

    /// <summary>
    /// Requires the absolute standardized parent error to be no larger than 1.96.
    /// </summary>
    /// <param name="coordinate">Name of the fitted coordinate.</param>
    /// <param name="estimate">Fitted coordinate value.</param>
    /// <param name="parent">Generating-parent value.</param>
    /// <param name="standardError">Defensible predeclared standard error.</param>
    /// <remarks>
    /// The cutoff is the two-sided standard-Normal 95 percent value. The caller remains
    /// responsible for identifying the fitted coordinate, parent, sample design, and estimator
    /// covariance or observed-information source that supplied <paramref name="standardError"/>.
    /// </remarks>
    public static void AssertFrequentistStandardizedError(string coordinate, double estimate, double parent, double standardError)
    {
        Assert.IsTrue(Tools.IsFinite(standardError) && standardError > 0d,
            $"{coordinate} must provide a finite positive predeclared standard error.");
        double standardizedError = Math.Abs(estimate - parent) / standardError;
        Assert.IsTrue(standardizedError <= NinetyFivePercentStandardNormalCutoff,
            $"{coordinate} estimate {estimate:G17}, parent {parent:G17}, and observed-information " +
            $"standard error {standardError:G17} give standardized parent error " +
            $"{standardizedError:G17}, exceeding {NinetyFivePercentStandardNormalCutoff:G17}.");
    }

    /// <summary>
    /// Requires central-95% posterior inclusion and all coordinate-level convergence diagnostics.
    /// </summary>
    /// <param name="coordinate">Name of the monitored posterior coordinate.</param>
    /// <param name="parent">Generating-parent value.</param>
    /// <param name="lower">Central-95% posterior lower limit.</param>
    /// <param name="upper">Central-95% posterior upper limit.</param>
    /// <param name="rhat">Split-R-hat diagnostic.</param>
    /// <param name="effectiveSampleSize">Effective sample size diagnostic.</param>
    /// <remarks>
    /// Bayesian cells combine central-95% parent inclusion with per-coordinate convergence checks;
    /// they must document their parent, posterior coordinate, chain design, and any response-grid
    /// treatment separately.
    /// </remarks>
    public static void AssertBayesianRecovery(string coordinate, double parent, double lower, double upper, double rhat, double effectiveSampleSize)
    {
        AssertFrequentistParentInInterval(coordinate, parent, lower, upper);
        Assert.IsTrue(Tools.IsFinite(rhat) && rhat < MaximumRhat,
            $"{coordinate} R-hat {rhat:G17} must be below {MaximumRhat:G17}.");
        Assert.IsTrue(Tools.IsFinite(effectiveSampleSize) && effectiveSampleSize >= MinimumEffectiveSampleSize,
            $"{coordinate} ESS {effectiveSampleSize:G17} must be at least {MinimumEffectiveSampleSize:G17}.");
    }

    /// <summary>
    /// Applies the optional 5 percent point/curve check only when its existing 95 percent band is narrower than five percent of a nonzero parent.
    /// </summary>
    /// <param name="label">Name of the point or curve ordinate.</param>
    /// <param name="estimate">Fitted point or ordinate.</param>
    /// <param name="parent">Generating-parent point or ordinate.</param>
    /// <param name="lower">Existing 95 percent lower uncertainty limit.</param>
    /// <param name="upper">Existing 95 percent upper uncertainty limit.</param>
    /// <remarks>
    /// This is secondary to a valid predeclared 95 percent band. It is evaluated only for finite,
    /// ordered bands around a finite nonzero parent when that band is narrower than five percent of
    /// the parent magnitude; it never replaces interval or identified-response acceptance.
    /// </remarks>
    public static void AssertSecondaryPointCriterionWhenResolved(string label, double estimate, double parent, double lower, double upper)
    {
        Assert.IsTrue(Tools.IsFinite(estimate) && Tools.IsFinite(parent),
            $"{label} must provide finite fitted and parent values before secondary evaluation.");
        Assert.IsTrue(Tools.IsFinite(lower) && Tools.IsFinite(upper) && lower <= upper,
            $"{label} must provide a finite ordered 95% uncertainty interval before secondary evaluation.");
        if (parent == 0d)
            return;

        double parentMagnitude = Math.Abs(parent);
        if (upper - lower >= SecondaryRelativeCriterion * parentMagnitude)
            return;

        Assert.IsTrue(Math.Abs(estimate - parent) <= SecondaryRelativeCriterion * parentMagnitude,
            $"{label} is resolved to a <5% 95% band but differs from its parent by more than 5%.");
    }

    /// <summary>
    /// Requires a preidentified response-grid ordinate to include its generating response in its 95 percent band.
    /// </summary>
    /// <param name="label">Predeclared response-grid ordinate label.</param>
    /// <param name="parentResponse">Generating response at the identified ordinate.</param>
    /// <param name="lower">95 percent lower response limit.</param>
    /// <param name="upper">95 percent upper response limit.</param>
    /// <remarks>
    /// Use this only when a cell has predeclared a scientifically identified response ordinate and
    /// its uncertainty source. The caller must state the response coordinate, parent response,
    /// sample design, 95 percent band construction, and whether the secondary five-percent rule
    /// applies; this helper deliberately performs no response-grid selection.
    /// </remarks>
    public static void AssertIdentifiedResponseGrid(string label, double parentResponse, double lower, double upper)
        => AssertFrequentistParentInInterval(label, parentResponse, lower, upper);
}
