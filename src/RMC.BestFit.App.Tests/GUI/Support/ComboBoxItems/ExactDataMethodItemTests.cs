using Microsoft.VisualStudio.TestTools.UnitTesting;
using RMC.BestFit.UI;

namespace RMC.BestFit.App.Tests.GUI.Support.ComboBoxItems
{
    /// <summary>
    /// Unit tests for <see cref="RMC_BestFit.ExactDataMethodItem"/>.
    /// </summary>
    [TestClass]
    public class ExactDataMethodItemTests
    {
        /// <summary>
        /// Verifies that the Manual entry method item stores all properties correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_Manual_SetsProperties()
        {
            var item = new RMC_BestFit.ExactDataMethodItem("Manual Entry", InputData.ExactDataEntryType.Manual);
            Assert.AreEqual("Manual Entry", item.DisplayName);
            Assert.AreEqual(InputData.ExactDataEntryType.Manual, item.Value);
        }

        /// <summary>
        /// Verifies that the BlockSeries entry method item stores properties correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_BlockSeries_SetsProperties()
        {
            var item = new RMC_BestFit.ExactDataMethodItem("Block Series", InputData.ExactDataEntryType.BlockSeries);
            Assert.AreEqual("Block Series", item.DisplayName);
            Assert.AreEqual(InputData.ExactDataEntryType.BlockSeries, item.Value);
        }

        /// <summary>
        /// Verifies that the PeaksOverThresholdSeries entry method item stores properties correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_PeaksOverThreshold_SetsProperties()
        {
            var item = new RMC_BestFit.ExactDataMethodItem("Peaks-Over-Threshold Series", InputData.ExactDataEntryType.PeaksOverThresholdSeries);
            Assert.AreEqual("Peaks-Over-Threshold Series", item.DisplayName);
            Assert.AreEqual(InputData.ExactDataEntryType.PeaksOverThresholdSeries, item.Value);
        }

        /// <summary>
        /// Verifies that the USGSPeakDischarge entry method item stores properties correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_USGSPeakDischarge_SetsProperties()
        {
            var item = new RMC_BestFit.ExactDataMethodItem("USGS Peak Discharge", InputData.ExactDataEntryType.USGSPeakDischarge);
            Assert.AreEqual("USGS Peak Discharge", item.DisplayName);
            Assert.AreEqual(InputData.ExactDataEntryType.USGSPeakDischarge, item.Value);
        }

        /// <summary>
        /// Verifies that DisplayName and Value are mutable after construction.
        /// </summary>
        [TestMethod]
        public void Properties_AreMutable()
        {
            var item = new RMC_BestFit.ExactDataMethodItem("Manual Entry", InputData.ExactDataEntryType.Manual);
            item.DisplayName = "Block Series";
            item.Value = InputData.ExactDataEntryType.BlockSeries;
            Assert.AreEqual("Block Series", item.DisplayName);
            Assert.AreEqual(InputData.ExactDataEntryType.BlockSeries, item.Value);
        }
    }
}
