using Microsoft.VisualStudio.TestTools.UnitTesting;
using RMC.BestFit.Estimation;

namespace RMC.BestFit.App.Tests.GUI.Support.ComboBoxItems
{
    /// <summary>
    /// Unit tests for <see cref="RMC_BestFit.PointEstimatorItem"/>.
    /// </summary>
    [TestClass]
    public class PointEstimatorItemTests
    {
        /// <summary>
        /// Verifies that the PosteriorMean item stores all properties correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_PosteriorMean_SetsAllProperties()
        {
            string tip = "Average of all posterior samples.";
            var item = new RMC_BestFit.PointEstimatorItem(
                "Posterior Mean",
                BayesianAnalysis.PointEstimateType.PosteriorMean,
                tip);
            Assert.AreEqual("Posterior Mean", item.DisplayName);
            Assert.AreEqual(BayesianAnalysis.PointEstimateType.PosteriorMean, item.Value);
            Assert.AreEqual(tip, item.ToolTip);
        }

        /// <summary>
        /// Verifies that the PosteriorMode item stores all properties correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_PosteriorMode_SetsValue()
        {
            var item = new RMC_BestFit.PointEstimatorItem(
                "Posterior Mode",
                BayesianAnalysis.PointEstimateType.PosteriorMode);
            Assert.AreEqual(BayesianAnalysis.PointEstimateType.PosteriorMode, item.Value);
            Assert.AreEqual("Posterior Mode", item.DisplayName);
        }

        /// <summary>
        /// Verifies that the tooltip defaults to empty string when not provided.
        /// </summary>
        [TestMethod]
        public void Constructor_NoTooltip_DefaultsToEmpty()
        {
            var item = new RMC_BestFit.PointEstimatorItem("Posterior Mean", BayesianAnalysis.PointEstimateType.PosteriorMean);
            Assert.AreEqual(string.Empty, item.ToolTip);
        }

        /// <summary>
        /// Verifies that properties are read-only and retain their constructor values.
        /// </summary>
        [TestMethod]
        public void Properties_RetainConstructorValues()
        {
            var item = new RMC_BestFit.PointEstimatorItem(
                "Posterior Mode",
                BayesianAnalysis.PointEstimateType.PosteriorMode,
                "MAP estimate.");
            Assert.AreEqual("Posterior Mode", item.DisplayName);
            Assert.AreEqual(BayesianAnalysis.PointEstimateType.PosteriorMode, item.Value);
            Assert.AreEqual("MAP estimate.", item.ToolTip);
        }
    }
}
