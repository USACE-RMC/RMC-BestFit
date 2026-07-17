using Microsoft.VisualStudio.TestTools.UnitTesting;
using RMC.BestFit.Estimation;

namespace RMC.BestFit.App.Tests.GUI.Support.ComboBoxItems
{
    /// <summary>
    /// Unit tests for <see cref="RMC_BestFit.GMMEstimationTypeItem"/>.
    /// </summary>
    [TestClass]
    public class GMMEstimationTypeItemTests
    {
        /// <summary>
        /// Verifies that the OneStep item stores all properties correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_OneStep_SetsAllProperties()
        {
            var item = new RMC_BestFit.GMMEstimationTypeItem("One-Step", GeneralizedMethodOfMoments.GMMEstimationStrategy.OneStep, "One-step GMM.");
            Assert.AreEqual("One-Step", item.DisplayName);
            Assert.AreEqual(GeneralizedMethodOfMoments.GMMEstimationStrategy.OneStep, item.Value);
            Assert.AreEqual("One-step GMM.", item.ToolTip);
        }

        /// <summary>
        /// Verifies that the TwoStep item stores properties correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_TwoStep_SetsProperties()
        {
            var item = new RMC_BestFit.GMMEstimationTypeItem("Two-Step", GeneralizedMethodOfMoments.GMMEstimationStrategy.TwoStep);
            Assert.AreEqual("Two-Step", item.DisplayName);
            Assert.AreEqual(GeneralizedMethodOfMoments.GMMEstimationStrategy.TwoStep, item.Value);
        }

        /// <summary>
        /// Verifies that the Iterative item stores properties correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_Iterative_SetsProperties()
        {
            var item = new RMC_BestFit.GMMEstimationTypeItem("Iterative", GeneralizedMethodOfMoments.GMMEstimationStrategy.Iterative);
            Assert.AreEqual("Iterative", item.DisplayName);
            Assert.AreEqual(GeneralizedMethodOfMoments.GMMEstimationStrategy.Iterative, item.Value);
        }

        /// <summary>
        /// Verifies that the tooltip defaults to empty string when not provided.
        /// </summary>
        [TestMethod]
        public void Constructor_NoTooltip_DefaultsToEmpty()
        {
            var item = new RMC_BestFit.GMMEstimationTypeItem("One-Step", GeneralizedMethodOfMoments.GMMEstimationStrategy.OneStep);
            Assert.AreEqual(string.Empty, item.ToolTip);
        }
    }
}
