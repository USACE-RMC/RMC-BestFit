using Microsoft.VisualStudio.TestTools.UnitTesting;
using RMC_BestFit;

namespace RMC.BestFit.App.Tests.GUI.PropertiesControls
{
    /// <summary>
    /// Tests the Training Steps box logic of the time-series analysis properties control.
    /// </summary>
    /// <remarks>
    /// The box's value is bound two-way to <c>TimeSeriesAnalysis.TrainingTimeSteps</c>, whose setter
    /// turns the default training rule off, so forcing a clamped value into the box writes a manual
    /// window back. The approved default window (Haden Smith, 26 September 2026) is not capped at
    /// the series length: a series shorter than its minimum fails validation instead of being fitted
    /// on a silently shortened manual window. With the default rule on, the box therefore neither
    /// clamps nor caps the window.
    /// </remarks>
    [TestClass]
    public class TimeSeriesAnalysisTrainingStepsTests
    {
        /// <summary>
        /// Verifies that the box leaves a default window unchanged, even one longer than the series,
        /// and widens its maximum so it displays that window.
        /// </summary>
        [TestMethod]
        public void DefaultRuleOn_LeavesTheModelWindowUnchanged()
        {
            Assert.IsNull(TimeSeriesAnalysisPropertiesControl.GetManualTrainingSteps(true, 18, 10, 15),
                "A default window longer than the series is not capped.");
            Assert.IsNull(TimeSeriesAnalysisPropertiesControl.GetManualTrainingSteps(true, 12, 10, 15));
            Assert.AreEqual(18, TimeSeriesAnalysisPropertiesControl.GetTrainingStepsMaximum(true, 18, 15),
                "The box can display a default window longer than the series.");
            Assert.AreEqual(15, TimeSeriesAnalysisPropertiesControl.GetTrainingStepsMaximum(true, 12, 15));
        }

        /// <summary>
        /// Verifies that a manual window is still clamped to the box's range, the series length at most.
        /// </summary>
        [TestMethod]
        public void ManualWindow_IsClampedToTheBoxRange()
        {
            Assert.AreEqual(15, TimeSeriesAnalysisPropertiesControl.GetManualTrainingSteps(false, 18, 10, 15));
            Assert.AreEqual(10, TimeSeriesAnalysisPropertiesControl.GetManualTrainingSteps(false, 4, 10, 15));
            Assert.AreEqual(12, TimeSeriesAnalysisPropertiesControl.GetManualTrainingSteps(false, 12, 10, 15));
            Assert.AreEqual(15, TimeSeriesAnalysisPropertiesControl.GetTrainingStepsMaximum(false, 18, 15));
        }
    }
}
