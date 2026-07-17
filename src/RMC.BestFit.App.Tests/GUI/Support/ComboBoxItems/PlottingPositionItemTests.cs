using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RMC.BestFit.App.Tests.GUI.Support.ComboBoxItems
{
    /// <summary>
    /// Unit tests for <see cref="RMC_BestFit.PlottingPositionItem"/>.
    /// </summary>
    [TestClass]
    public class PlottingPositionItemTests
    {
        /// <summary>
        /// Verifies that the constructor stores all three arguments correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_SetsAllProperties()
        {
            var item = new RMC_BestFit.PlottingPositionItem("Weibull", 0.0, "alpha = 0");
            Assert.AreEqual("Weibull", item.DisplayName);
            Assert.AreEqual(0.0, item.Value, 1e-12);
            Assert.AreEqual("alpha = 0", item.ToolTip);
        }

        /// <summary>
        /// Verifies the Median plotting position formula (alpha = 0.3).
        /// </summary>
        [TestMethod]
        public void Constructor_MedianFormula_AlphaPoint3()
        {
            var item = new RMC_BestFit.PlottingPositionItem("Median", 0.3, "alpha = 0.3");
            Assert.AreEqual(0.3, item.Value, 1e-12);
        }

        /// <summary>
        /// Verifies the Cunnane plotting position formula (alpha = 0.4).
        /// </summary>
        [TestMethod]
        public void Constructor_CunnaneFormula_AlphaPoint4()
        {
            var item = new RMC_BestFit.PlottingPositionItem("Cunnane", 0.4, "alpha = 0.4");
            Assert.AreEqual(0.4, item.Value, 1e-12);
        }

        /// <summary>
        /// Verifies that DisplayName and ToolTip are mutable after construction.
        /// </summary>
        [TestMethod]
        public void Properties_AreMutable()
        {
            var item = new RMC_BestFit.PlottingPositionItem("Old", 0.0, "Old tip");
            item.DisplayName = "New";
            item.ToolTip = "New tip";
            item.Value = 0.5;
            Assert.AreEqual("New", item.DisplayName);
            Assert.AreEqual("New tip", item.ToolTip);
            Assert.AreEqual(0.5, item.Value, 1e-12);
        }

        /// <summary>
        /// Verifies that an empty tooltip is accepted without error.
        /// </summary>
        [TestMethod]
        public void Constructor_EmptyToolTip_IsAccepted()
        {
            var item = new RMC_BestFit.PlottingPositionItem("Blom", 0.375, string.Empty);
            Assert.AreEqual(string.Empty, item.ToolTip);
        }
    }
}
