namespace RMC.BestFit.Verification.RatingCurve;

/// <summary>
/// Verifies Bayesian recovery for the minimal rating-curve interaction matrix.
/// </summary>
/// <remarks>
/// Each retained fixture contains exactly 1,000 generated stage-discharge pairs and preserves
/// its historical parent, seed, prior flags, and production sampler defaults. Identifiable
/// coordinates use central-95% posterior inclusion with R-hat below 1.10 and ESS at least 100
/// for every sampled coordinate. Response recovery adds the draw-specific log10 residual to
/// retained posterior draws and uses a MAP-centered simultaneous 95% posterior-predictive band.
/// </remarks>
[TestClass]
public class RatingCurveBayesianRecoveryTests
{
    /// <summary>Recovers the standard single-control posterior and identified curve ordinates.</summary>
    /// <returns>A task representing the production Bayesian run.</returns>
    [TestMethod]
    public Task Test_EstimateParameters_SingleSegment_Default()
        => RatingCurveRecoveryAcceptance.RunBayesianAsync(
            RatingCurveRecoveryAcceptance.SingleSegmentDefault());

    /// <summary>Recovers the low-noise single-control posterior and identified curve ordinates.</summary>
    /// <returns>A task representing the production Bayesian run.</returns>
    [TestMethod]
    public Task Test_EstimateParameters_SingleSegment_LowNoise()
        => RatingCurveRecoveryAcceptance.RunBayesianAsync(
            RatingCurveRecoveryAcceptance.SingleSegmentLowNoise());

    /// <summary>Recovers the wide-range single-control posterior and identified curve ordinates.</summary>
    /// <returns>A task representing the production Bayesian run.</returns>
    [TestMethod]
    public Task Test_EstimateParameters_SingleSegment_WideRange()
        => RatingCurveRecoveryAcceptance.RunBayesianAsync(
            RatingCurveRecoveryAcceptance.SingleSegmentWideRange());

    /// <summary>Recovers the bankfull-transition scale and identified posterior curve ordinates.</summary>
    /// <returns>A task representing the production Bayesian run.</returns>
    [TestMethod]
    public Task Test_EstimateParameters_TwoSegment_BankfullTransition()
        => RatingCurveRecoveryAcceptance.RunBayesianAsync(
            RatingCurveRecoveryAcceptance.TwoSegmentBankfullTransition());

    /// <summary>Recovers the error scale and identified three-control posterior curve ordinates.</summary>
    /// <returns>A task representing the production Bayesian run.</returns>
    [TestMethod]
    public Task Test_EstimateParameters_ThreeSegment_MultipleControl()
        => RatingCurveRecoveryAcceptance.RunBayesianAsync(
            RatingCurveRecoveryAcceptance.ThreeSegmentMultipleControl());
}
