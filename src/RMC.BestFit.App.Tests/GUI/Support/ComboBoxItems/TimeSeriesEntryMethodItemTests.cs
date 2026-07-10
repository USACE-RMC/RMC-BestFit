using Microsoft.VisualStudio.TestTools.UnitTesting;
using RMC.BestFit.UI;

namespace RMC.BestFit.App.Tests.GUI.Support.ComboBoxItems
{
    /// <summary>
    /// Unit tests for <see cref="RMC_BestFit.TimeSeriesEntryMethodItem"/>.
    /// </summary>
    [TestClass]
    public class TimeSeriesEntryMethodItemTests
    {
        /// <summary>
        /// Verifies that the Manual entry method item stores all properties correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_Manual_SetsAllProperties()
        {
            var item = new RMC_BestFit.TimeSeriesEntryMethodItem(
                "Manual Entry",
                TimeSeriesElement.TimeSeriesEntryMethod.Manual,
                "Manually enter time series data");
            Assert.AreEqual("Manual Entry", item.DisplayName);
            Assert.AreEqual(TimeSeriesElement.TimeSeriesEntryMethod.Manual, item.Value);
            Assert.AreEqual("Manually enter time series data", item.ToolTip);
        }

        /// <summary>
        /// Verifies that the HECDSS entry method stores properties correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_HECDSS_SetsProperties()
        {
            var item = new RMC_BestFit.TimeSeriesEntryMethodItem("HEC-DSS", TimeSeriesElement.TimeSeriesEntryMethod.HECDSS);
            Assert.AreEqual("HEC-DSS", item.DisplayName);
            Assert.AreEqual(TimeSeriesElement.TimeSeriesEntryMethod.HECDSS, item.Value);
        }

        /// <summary>
        /// Verifies that the USGS entry method stores properties correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_USGS_SetsProperties()
        {
            var item = new RMC_BestFit.TimeSeriesEntryMethodItem("USGS", TimeSeriesElement.TimeSeriesEntryMethod.USGS);
            Assert.AreEqual("USGS", item.DisplayName);
            Assert.AreEqual(TimeSeriesElement.TimeSeriesEntryMethod.USGS, item.Value);
        }

        /// <summary>
        /// Verifies that the tooltip defaults to empty string when not provided.
        /// </summary>
        [TestMethod]
        public void Constructor_NoTooltip_DefaultsToEmpty()
        {
            var item = new RMC_BestFit.TimeSeriesEntryMethodItem("Manual Entry", TimeSeriesElement.TimeSeriesEntryMethod.Manual);
            Assert.AreEqual(string.Empty, item.ToolTip);
        }

        /// <summary>
        /// Verifies that DisplayName, Value, and ToolTip are mutable after construction.
        /// </summary>
        [TestMethod]
        public void Properties_AreMutable()
        {
            var item = new RMC_BestFit.TimeSeriesEntryMethodItem("Manual Entry", TimeSeriesElement.TimeSeriesEntryMethod.Manual);
            item.DisplayName = "HEC-DSS";
            item.Value = TimeSeriesElement.TimeSeriesEntryMethod.HECDSS;
            item.ToolTip = "DSS tip";
            Assert.AreEqual("HEC-DSS", item.DisplayName);
            Assert.AreEqual(TimeSeriesElement.TimeSeriesEntryMethod.HECDSS, item.Value);
            Assert.AreEqual("DSS tip", item.ToolTip);
        }
    }
}
