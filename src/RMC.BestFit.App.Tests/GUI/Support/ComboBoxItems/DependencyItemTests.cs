using Microsoft.VisualStudio.TestTools.UnitTesting;
using Numerics.Data.Statistics;

namespace RMC.BestFit.App.Tests.GUI.Support.ComboBoxItems
{
    /// <summary>
    /// Unit tests for <see cref="RMC_BestFit.DependencyItem"/>.
    /// </summary>
    [TestClass]
    public class DependencyItemTests
    {
        /// <summary>
        /// Verifies that the Independent dependency item stores all properties correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_Independent_SetsAllProperties()
        {
            var item = new RMC_BestFit.DependencyItem("Independent", Probability.DependencyType.Independent, "Treat as independent.");
            Assert.AreEqual("Independent", item.DisplayName);
            Assert.AreEqual(Probability.DependencyType.Independent, item.Value);
            Assert.AreEqual("Treat as independent.", item.ToolTip);
        }

        /// <summary>
        /// Verifies that the PerfectlyNegative dependency item stores properties correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_PerfectlyNegative_SetsProperties()
        {
            var item = new RMC_BestFit.DependencyItem("Perfectly Negative", Probability.DependencyType.PerfectlyNegative);
            Assert.AreEqual("Perfectly Negative", item.DisplayName);
            Assert.AreEqual(Probability.DependencyType.PerfectlyNegative, item.Value);
        }

        /// <summary>
        /// Verifies that the PerfectlyPositive dependency item stores properties correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_PerfectlyPositive_SetsProperties()
        {
            var item = new RMC_BestFit.DependencyItem("Perfectly Positive", Probability.DependencyType.PerfectlyPositive);
            Assert.AreEqual("Perfectly Positive", item.DisplayName);
            Assert.AreEqual(Probability.DependencyType.PerfectlyPositive, item.Value);
        }

        /// <summary>
        /// Verifies that the tooltip defaults to empty string when not provided.
        /// </summary>
        [TestMethod]
        public void Constructor_NoTooltip_DefaultsToEmpty()
        {
            var item = new RMC_BestFit.DependencyItem("Independent", Probability.DependencyType.Independent);
            Assert.AreEqual(string.Empty, item.ToolTip);
        }
    }
}
