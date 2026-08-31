namespace RMC.BestFit.Verification.RatingCurve;

/// <summary>
/// Verifies maximum-likelihood recovery for the minimal rating-curve interaction matrix.
/// </summary>
/// <remarks>
/// Each retained fixture contains exactly 1,000 generated stage-discharge pairs and preserves
/// its historical parent and seed while using Differential Evolution with default optimizer tolerances.
/// Identifiable coordinates use the common observed-information 95% standardized-error rule.
/// Response recovery separately propagates the full observed-information covariance and the fitted
/// log10 residual law into a simultaneous 95% predictive band over the predeclared stage grid.
/// </remarks>
[TestClass]
public class RatingCurveMLERecoveryTests
{
    /// <summary>Recovers the standard single-control parent and identified curve ordinates.</summary>
    [TestMethod]
    public void Test_EstimateParameters_SingleSegment_Default()
        => RatingCurveRecoveryAcceptance.RunMaximumLikelihood(
            RatingCurveRecoveryAcceptance.SingleSegmentDefault());

    /// <summary>Recovers the low-noise single-control parent and identified curve ordinates.</summary>
    [TestMethod]
    public void Test_EstimateParameters_SingleSegment_LowNoise()
        => RatingCurveRecoveryAcceptance.RunMaximumLikelihood(
            RatingCurveRecoveryAcceptance.SingleSegmentLowNoise());

    /// <summary>Recovers the wide-range single-control parent and identified curve ordinates.</summary>
    [TestMethod]
    public void Test_EstimateParameters_SingleSegment_WideRange()
        => RatingCurveRecoveryAcceptance.RunMaximumLikelihood(
            RatingCurveRecoveryAcceptance.SingleSegmentWideRange());

    /// <summary>Recovers the bankfull-transition scale and identified two-control curve ordinates.</summary>
    [TestMethod]
    public void Test_EstimateParameters_TwoSegment_BankfullTransition()
        => RatingCurveRecoveryAcceptance.RunMaximumLikelihood(
            RatingCurveRecoveryAcceptance.TwoSegmentBankfullTransition());

    /// <summary>Recovers the error scale and identified three-control curve ordinates.</summary>
    [TestMethod]
    public void Test_EstimateParameters_ThreeSegment_MultipleControl()
        => RatingCurveRecoveryAcceptance.RunMaximumLikelihood(
            RatingCurveRecoveryAcceptance.ThreeSegmentMultipleControl());
}
