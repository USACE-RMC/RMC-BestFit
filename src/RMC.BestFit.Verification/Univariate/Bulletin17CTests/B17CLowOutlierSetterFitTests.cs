using Numerics.Distributions;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;

namespace RMC.BestFit.Verification.Univariate.Bulletin17CTests;

/// <summary>
/// Verifies that a headless Bulletin 17C GMM fit over a censored (low-outlier) record succeeds
/// directly after a low-outlier setter, and matches the historical workaround of pairing the setter
/// with a manual plotting-position recompute.
/// </summary>
/// <remarks>
/// <para>
/// The low-outlier setters formerly left the Hirsch-Stedinger plotting positions at their defaults
/// for headless callers, so the censored-data (ROS) initial estimate regressed on infinite normal
/// scores and the fit either threw (pre-TR-087) or proceeded from the constraint-based fallback
/// initials (post-TR-087). Every verification fixture worked around this by calling
/// <c>CalculatePlottingPositions()</c> manually after the setter. The setters now perform that
/// refresh themselves, so the two construction orders must be exactly equivalent — this test's
/// oracle is that declared equivalence, parameter for parameter.
/// </para>
/// <para>
/// Mirrors the corehydro upstream report's headless reproduction (a short record with two low
/// floods flagged by MGBT, fit by GMM), which required the manual recompute to succeed at all.
/// </para>
/// </remarks>
[TestClass]
[DoNotParallelize]
public class B17CLowOutlierSetterFitTests
{
    /// <summary>Seventeen annual peaks with two low floods that MGBT flags.</summary>
    private static readonly double[] Flows =
    {
        9600, 12400, 8100, 15300, 7000, 11800, 9950, 13600, 8800, 10400,
        14700, 7600, 12100, 9100, 16200, 55, 90
    };

    /// <summary>
    /// Builds a frame over the shared record and applies the MGBT low-outlier setter.
    /// </summary>
    /// <param name="manualRecompute">Whether to pair the setter with the historical manual
    /// plotting-position recompute.</param>
    /// <returns>The prepared frame.</returns>
    private static DataFrame CreateFrame(bool manualRecompute)
    {
        var frame = new DataFrame();
        frame.ExactSeries.SuppressCollectionChanged = true;
        for (int i = 0; i < Flows.Length; i++)
            frame.ExactSeries.Add(new ExactData(i + 1, Flows[i]));
        frame.SetLowOutliersFromMGBT();
        if (manualRecompute)
            frame.CalculatePlottingPositions();
        return frame;
    }

    /// <summary>
    /// A setter-only fit succeeds and reproduces the setter-plus-manual-recompute fit exactly.
    /// </summary>
    [TestMethod]
    public void Gmm_SetterOnly_MatchesSetterPlusManualRecompute()
    {
        var setterOnlyFrame = CreateFrame(manualRecompute: false);
        var workaroundFrame = CreateFrame(manualRecompute: true);
        Assert.IsTrue(setterOnlyFrame.NumberOfLowOutliers >= 1, "Fixture precondition: low floods flagged.");
        Assert.AreEqual(workaroundFrame.NumberOfLowOutliers, setterOnlyFrame.NumberOfLowOutliers);

        var setterOnlyModel = new Bulletin17CDistribution(setterOnlyFrame, UnivariateDistributionType.LogPearsonTypeIII);
        var workaroundModel = new Bulletin17CDistribution(workaroundFrame, UnivariateDistributionType.LogPearsonTypeIII);

        var setterOnlyGmm = new GeneralizedMethodOfMoments(setterOnlyModel);
        setterOnlyGmm.Estimate();
        var workaroundGmm = new GeneralizedMethodOfMoments(workaroundModel);
        workaroundGmm.Estimate();

        Assert.IsTrue(setterOnlyGmm.IsEstimated, "The setter-only fit must succeed without a manual recompute.");
        Assert.IsTrue(workaroundGmm.IsEstimated, "The workaround fit must succeed.");
        Assert.AreEqual(workaroundModel.NumberOfParameters, setterOnlyModel.NumberOfParameters);
        for (int i = 0; i < workaroundModel.NumberOfParameters; i++)
        {
            Assert.AreEqual(workaroundGmm.BestParameterSet.Values[i], setterOnlyGmm.BestParameterSet.Values[i], 1E-12,
                $"Parameter[{i}] must match between the two construction orders.");
        }
    }
}
