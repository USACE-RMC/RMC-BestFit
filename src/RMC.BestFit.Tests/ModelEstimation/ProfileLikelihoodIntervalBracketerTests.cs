using RMC.BestFit.Estimation;

namespace RMC.BestFit.Tests.ModelEstimation;

/// <summary>
/// Fast deterministic tests for finite likelihood-profile threshold bracketing.
/// </summary>
/// <remarks>
/// These tests isolate scalar root bracketing from model optimization so they can prove that
/// likelihood-profile intervals never pass a nonfinite or unavailable model endpoint to Brent's solver.
/// </remarks>
[TestClass]
public class ProfileLikelihoodIntervalBracketerTests
{
    /// <summary>
    /// Verifies that a lower threshold crossing is found before a nonfinite lower model bound.
    /// </summary>
    [TestMethod]
    public void FindThresholdCrossing_LowerNonfiniteModelBound_FindsFiniteRoot()
    {
        double root = ProfileLikelihoodIntervalBracketer.FindThresholdCrossing(
            value => value + 2d,
            estimate: 0d,
            modelBound: double.NegativeInfinity,
            isLower: true);

        Assert.AreEqual(-2d, root, 1e-6);
        Assert.IsTrue(double.IsFinite(root));
    }

    /// <summary>
    /// Verifies that an upper threshold crossing is found before a throwing finite model bound.
    /// </summary>
    [TestMethod]
    public void FindThresholdCrossing_UpperThrowingModelBound_FindsFiniteRoot()
    {
        double root = ProfileLikelihoodIntervalBracketer.FindThresholdCrossing(
            value =>
            {
                if (value >= 2.3d)
                    throw new InvalidOperationException("The exact model bound is unavailable.");

                return 2d - value;
            },
            estimate: 0d,
            modelBound: 3d,
            isLower: false);

        Assert.AreEqual(2d, root, 1e-6);
        Assert.IsTrue(double.IsFinite(root));
    }

    /// <summary>
    /// Verifies that Brent receives the originally observed finite endpoint differences when profile warm starts mutate.
    /// </summary>
    [TestMethod]
    public void FindThresholdCrossing_StatefulEndpointCallback_UsesObservedFiniteBracketValues()
    {
        int lowerEndpointEvaluations = 0;
        int upperEndpointEvaluations = 0;

        double root = ProfileLikelihoodIntervalBracketer.FindThresholdCrossing(
            value =>
            {
                if (Math.Abs(value - 1.6d) < 1e-12)
                {
                    lowerEndpointEvaluations++;
                    return 0.4d;
                }

                if (Math.Abs(value - 2.56d) < 1e-12)
                {
                    upperEndpointEvaluations++;
                    if (upperEndpointEvaluations > 1)
                        throw new InvalidOperationException("Warm-start mutation invalidated the repeated endpoint evaluation.");

                    return -0.56d;
                }

                if (value > 3d)
                    throw new InvalidOperationException("Brent expanded outside the finite bracket.");

                return 2d - value;
            },
            estimate: 0d,
            modelBound: 3d,
            isLower: false);

        Assert.AreEqual(2d, root, 1e-6);
        Assert.AreEqual(1, lowerEndpointEvaluations);
        Assert.AreEqual(1, upperEndpointEvaluations);
    }

    /// <summary>
    /// Verifies that a finite lower model bound is returned when the profile never crosses the threshold.
    /// </summary>
    [TestMethod]
    public void FindThresholdCrossing_FiniteLowerBoundWithoutCrossing_ReturnsBoundWithoutLeavingModelRange()
    {
        var evaluatedValues = new List<double>();

        double result = ProfileLikelihoodIntervalBracketer.FindThresholdCrossing(
            value =>
            {
                evaluatedValues.Add(value);
                return 1d;
            },
            estimate: 0d,
            modelBound: -3d,
            isLower: true);

        Assert.AreEqual(-3d, result);
        Assert.IsTrue(evaluatedValues.All(value => value >= -3d && value <= 0d));
    }

    /// <summary>
    /// Verifies that a nonfinite model bound without a finite threshold crossing fails explicitly.
    /// </summary>
    [TestMethod]
    public void FindThresholdCrossing_NonfiniteBoundWithoutCrossing_ThrowsInvalidOperationException()
    {
        Assert.ThrowsException<InvalidOperationException>(() =>
            ProfileLikelihoodIntervalBracketer.FindThresholdCrossing(
                value => 1d,
                estimate: 0d,
                modelBound: double.PositiveInfinity,
                isLower: false));
    }

    /// <summary>
    /// Verifies that a failed nearest profile warm start retries the next nearest successful start.
    /// </summary>
    [TestMethod]
    public void ProfileLikelihoodWarmStartCache_NearestStartFails_RetriesNextNearestSuccessfulStart()
    {
        var cache = new ProfileLikelihoodWarmStartCache(0d, new double[] { 0d });
        _ = cache.Evaluate(
            fixedValue: 1d,
            startingParameters => (Difference: 1d, Parameters: new double[] { 1d }));

        var attemptedStarts = new List<double>();
        double difference = cache.Evaluate(
            fixedValue: 0.75d,
            startingParameters =>
            {
                attemptedStarts.Add(startingParameters[0]);
                if (startingParameters[0] == 1d)
                    throw new InvalidOperationException("The nearest warm start did not converge.");

                return (Difference: -0.25d, Parameters: new double[] { 0.75d });
            });

        Assert.AreEqual(-0.25d, difference);
        CollectionAssert.AreEqual(new double[] { 1d, 0d }, attemptedStarts);
    }
}
