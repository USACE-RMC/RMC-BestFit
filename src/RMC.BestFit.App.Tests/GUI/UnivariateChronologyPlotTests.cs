using Microsoft.VisualStudio.TestTools.UnitTesting;
using Numerics.Distributions;
using RMC_BestFit;

namespace RMC.BestFit.App.Tests.GUI
{
    /// <summary>
    /// Checks which chronology results the univariate analysis view draws.
    /// </summary>
    /// <remarks>
    /// The chronology plot redraws from change notifications raised on background reprocesses, so it must
    /// ignore results that are absent, incomplete, or misaligned instead of indexing into them.
    /// </remarks>
    [TestClass]
    public class UnivariateChronologyPlotTests
    {
        /// <summary>
        /// Draws only results whose point-estimate, mean, and two-column interval arrays exist and align.
        /// </summary>
        [TestMethod]
        public void IsChronologyDrawable_RequiresCompleteAlignedArrays()
        {
            Assert.IsFalse(UnivariateAnalysisControl.IsChronologyDrawable(null));
            Assert.IsFalse(UnivariateAnalysisControl.IsChronologyDrawable(
                new UncertaintyAnalysisResults { ModeCurve = new double[3] }),
                "A rebuild in progress has a point-estimate curve but no mean or intervals.");
            Assert.IsFalse(UnivariateAnalysisControl.IsChronologyDrawable(
                new UncertaintyAnalysisResults { ModeCurve = new double[4], MeanCurve = new double[3], ConfidenceIntervals = new double[3, 2] }),
                "A curve spanning a new extent cannot pair with the previous extent's intervals.");
            Assert.IsFalse(UnivariateAnalysisControl.IsChronologyDrawable(
                new UncertaintyAnalysisResults { ModeCurve = new double[3], MeanCurve = new double[3], ConfidenceIntervals = new double[3, 1] }),
                "The plot reads a lower and an upper bound for every step.");
            Assert.IsTrue(UnivariateAnalysisControl.IsChronologyDrawable(
                new UncertaintyAnalysisResults { ModeCurve = new double[3], MeanCurve = new double[3], ConfidenceIntervals = new double[3, 2] }));
        }
    }
}
