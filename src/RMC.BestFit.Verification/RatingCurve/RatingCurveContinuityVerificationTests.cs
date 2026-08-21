using BestFitRatingCurve = RMC.BestFit.Models.RatingCurve;

namespace RMC.BestFit.Verification.RatingCurve;

/// <summary>
/// Analytical two-sided continuity verification of the addition-mode rating curve at its
/// activation stages (TR-044).
/// </summary>
/// <remarks>
/// <para>
/// Control <c>k</c> contributes <c>10^{a_k} (h - h_k)^{beta_k}</c> for <c>h &gt; h_k</c> and nothing
/// at or below <c>h_k</c>. Immediately above the activation stage the added contribution equals
/// <c>10^{a_k} epsilon^{beta_k}</c>, which vanishes as <c>epsilon</c> shrinks exactly when
/// <c>beta_k &gt; 0</c> and equals the full coefficient <c>10^{a_k}</c> when <c>beta_k = 0</c>.
/// The curve is therefore continuous over the admissible parameter space only if every exponent
/// bound excludes zero.
/// </para>
/// <para>
/// The checks embed the analytical formulas directly; no external artifact is required. The example
/// two- and three-segment fixtures from <c>examples/6-rating-curve-analysis</c> supply the stage
/// data that calibrate the default bounds.
/// </para>
/// </remarks>
[TestClass]
public class RatingCurveContinuityVerificationTests
{
    /// <summary>Relative tolerance for pure double-precision algebra.</summary>
    private const double AlgebraicRelativeTolerance = 1e-10;

    /// <summary>Relative decrease the added-control increment must show between the two limit offsets.</summary>
    private const double LimitDecreaseFactor = 0.999;

    /// <summary>The larger limit offset.</summary>
    private const double OuterOffset = 1e-6;

    /// <summary>The smaller limit offset.</summary>
    private const double InnerOffset = 1e-12;

    /// <summary>Positive exponents used for the analytical increment check.</summary>
    private static readonly double[] TestExponents = [0.1, 1.0, 1.67, 2.5];

    /// <summary>Offsets used for the analytical increment check.</summary>
    private static readonly double[] TestOffsets = [1e-3, 1e-6, 1e-9];

    /// <summary>
    /// The two-sided discharge increment across the second activation stage equals the smooth
    /// first-control increment plus the analytical added-control term <c>10^{a_2} epsilon^{beta_2}</c>.
    /// </summary>
    [TestMethod]
    public void AddedControl_TwoSidedIncrementAtActivation_MatchesAnalyticalPowerLaw()
    {
        var example = RatingCurveExampleFixtures.LoadCase("two_segment");
        BestFitRatingCurve model = RatingCurveExampleFixtures.CreateModel(example);
        double[] theta = (double[])example.TrueParameters.Clone();
        double h1 = theta[0];
        double a1 = theta[1];
        double b1 = theta[2];
        double h2 = theta[3];
        double a2 = theta[4];

        foreach (double beta2 in TestExponents)
        {
            theta[5] = beta2;
            foreach (double epsilon in TestOffsets)
            {
                // Evaluate the analytical formula on the same floating-point stage values the model
                // receives, so the representation error of h2 + epsilon does not enter the comparison.
                double stageAbove = h2 + epsilon;
                double stageBelow = h2 - epsilon;
                double above = model.Predict(theta, stageAbove);
                double below = model.Predict(theta, stageBelow);
                double channelIncrement = Math.Pow(10.0, a1) * (Math.Pow(stageAbove - h1, b1) - Math.Pow(stageBelow - h1, b1));
                double expected = channelIncrement + Math.Pow(10.0, a2) * Math.Pow(stageAbove - h2, beta2);
                double scale = Math.Abs(above) + Math.Abs(below);
                Assert.AreEqual(
                    expected,
                    above - below,
                    AlgebraicRelativeTolerance * scale,
                    $"beta2 = {beta2}, epsilon = {epsilon}: two-sided increment at the second activation stage.");
            }
        }
    }

    /// <summary>
    /// With a zero exponent the added control jumps by its full coefficient immediately above the
    /// activation stage; this is the discontinuity the exponent bound must exclude.
    /// </summary>
    [TestMethod]
    public void ZeroExponent_AddedControlJumpsByItsCoefficientAtActivation()
    {
        var example = RatingCurveExampleFixtures.LoadCase("two_segment");
        BestFitRatingCurve model = RatingCurveExampleFixtures.CreateModel(example);
        double[] theta = (double[])example.TrueParameters.Clone();
        theta[5] = 0.0;
        double h1 = theta[0];
        double a1 = theta[1];
        double b1 = theta[2];
        double h2 = theta[3];
        double coefficient = Math.Pow(10.0, theta[4]);

        double channelIncrement = Math.Pow(10.0, a1) * (Math.Pow(h2 + InnerOffset - h1, b1) - Math.Pow(h2 - InnerOffset - h1, b1));
        double jump = model.Predict(theta, h2 + InnerOffset) - model.Predict(theta, h2 - InnerOffset) - channelIncrement;

        Assert.AreEqual(
            coefficient,
            jump,
            1e-9 * coefficient,
            "A zero exponent must jump by 10^a2 immediately above the activation stage.");
    }

    /// <summary>One-segment default exponent bounds exclude the discontinuous zero exponent.</summary>
    [TestMethod]
    public void DefaultExponentLowerBounds_AreStrictlyPositive_OneSegment() =>
        AssertExponentBoundsExcludeZero("one_segment");

    /// <summary>Two-segment default exponent bounds exclude the discontinuous zero exponent.</summary>
    [TestMethod]
    public void DefaultExponentLowerBounds_AreStrictlyPositive_TwoSegment() =>
        AssertExponentBoundsExcludeZero("two_segment");

    /// <summary>Three-segment default exponent bounds exclude the discontinuous zero exponent.</summary>
    [TestMethod]
    public void DefaultExponentLowerBounds_AreStrictlyPositive_ThreeSegment() =>
        AssertExponentBoundsExcludeZero("three_segment");

    /// <summary>
    /// With every exponent set to its default lower bound, each control's added increment across its
    /// activation stage decreases as the offset shrinks, which holds exactly when the bound is positive.
    /// </summary>
    [TestMethod]
    public void ExponentsAtDefaultLowerBound_AddedControlIncrementsVanishAtActivation()
    {
        var example = RatingCurveExampleFixtures.LoadCase("three_segment");
        BestFitRatingCurve model = RatingCurveExampleFixtures.CreateModel(example);
        double[] theta = (double[])example.TrueParameters.Clone();
        for (int control = 0; control < example.Segments; control++)
        {
            theta[3 * control + 2] = model.Parameters[3 * control + 2].LowerBound;
        }

        for (int control = 0; control < example.Segments; control++)
        {
            double outer = AddedControlIncrement(model, theta, control, OuterOffset);
            double inner = AddedControlIncrement(model, theta, control, InnerOffset);
            Assert.IsTrue(
                inner <= LimitDecreaseFactor * outer,
                $"Control {control + 1} with exponent at its default lower bound {theta[3 * control + 2]:G17}: "
                + $"the added increment is {inner:G17} at offset {InnerOffset} versus {outer:G17} at offset {OuterOffset}; "
                + "a continuous curve requires the increment to vanish as the offset shrinks (TR-044).");
        }
    }

    /// <summary>
    /// Computes the increment contributed by one control across its activation stage, removing the
    /// smooth analytical increment of the controls already active below it.
    /// </summary>
    /// <param name="model">The rating-curve model.</param>
    /// <param name="theta">The BestFit-layout parameter vector.</param>
    /// <param name="control">The zero-based control index.</param>
    /// <param name="epsilon">The two-sided offset.</param>
    /// <returns>The added-control increment across the activation stage.</returns>
    private static double AddedControlIncrement(BestFitRatingCurve model, double[] theta, int control, double epsilon)
    {
        double activation = theta[3 * control];
        double above = model.Predict(theta, activation + epsilon);
        double below = model.Predict(theta, activation - epsilon);
        double smooth = 0.0;
        for (int lower = 0; lower < control; lower++)
        {
            double offset = theta[3 * lower];
            double coefficient = Math.Pow(10.0, theta[3 * lower + 1]);
            double exponent = theta[3 * lower + 2];
            smooth += coefficient * (Math.Pow(activation + epsilon - offset, exponent) - Math.Pow(activation - epsilon - offset, exponent));
        }
        return above - below - smooth;
    }

    /// <summary>
    /// Asserts that every default exponent bound and prior support of an example model excludes zero.
    /// </summary>
    /// <param name="key">The example case key.</param>
    private static void AssertExponentBoundsExcludeZero(string key)
    {
        var example = RatingCurveExampleFixtures.LoadCase(key);
        BestFitRatingCurve model = RatingCurveExampleFixtures.CreateModel(example);
        for (int index = 0; index < model.NumberOfParameters; index++)
        {
            if (!example.ParameterNames[index].StartsWith("beta", StringComparison.Ordinal))
            {
                continue;
            }

            var parameter = model.Parameters[index];
            Assert.IsTrue(
                parameter.LowerBound > 0.0,
                $"{key}: {parameter.Name} lower bound {parameter.LowerBound:G17} admits a zero exponent, "
                + "for which the added control jumps by 10^a at its activation stage (TR-044).");
            double priorMinimum = parameter.PriorDistribution?.Minimum ?? double.NaN;
            Assert.IsTrue(
                priorMinimum > 0.0,
                $"{key}: {parameter.Name} prior support minimum {priorMinimum:G17} admits a zero exponent (TR-044).");
        }
    }
}
