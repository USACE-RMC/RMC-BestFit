using Microsoft.VisualStudio.TestTools.UnitTesting;
using Numerics.Data;
using RMC.BestFit.UI;

namespace RMC.BestFit.App.Tests.GUI.Support.ComboBoxItems
{
    /// <summary>
    /// Unit tests for <see cref="RMC_BestFit.TimeSeriesTypeItem"/>.
    /// </summary>
    [TestClass]
    public class TimeSeriesTypeItemTests
    {
        /// <summary>
        /// Verifies that the DailyDischarge item stores all properties correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_DailyDischarge_SetsProperties()
        {
            var item = new RMC_BestFit.TimeSeriesTypeItem("Daily Discharge", TimeSeriesDownload.TimeSeriesType.DailyDischarge);
            Assert.AreEqual("Daily Discharge", item.DisplayName);
            Assert.AreEqual(TimeSeriesDownload.TimeSeriesType.DailyDischarge, item.Value);
        }

        /// <summary>
        /// Verifies that the DailyStage item stores properties correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_DailyStage_SetsProperties()
        {
            var item = new RMC_BestFit.TimeSeriesTypeItem("Daily Stage", TimeSeriesDownload.TimeSeriesType.DailyStage);
            Assert.AreEqual("Daily Stage", item.DisplayName);
            Assert.AreEqual(TimeSeriesDownload.TimeSeriesType.DailyStage, item.Value);
        }

        /// <summary>
        /// Verifies that the PeakDischarge item stores properties correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_PeakDischarge_SetsProperties()
        {
            var item = new RMC_BestFit.TimeSeriesTypeItem("Peak Discharge", TimeSeriesDownload.TimeSeriesType.PeakDischarge);
            Assert.AreEqual("Peak Discharge", item.DisplayName);
            Assert.AreEqual(TimeSeriesDownload.TimeSeriesType.PeakDischarge, item.Value);
        }

        /// <summary>
        /// Verifies that DisplayName and Value are mutable after construction.
        /// </summary>
        [TestMethod]
        public void Properties_AreMutable()
        {
            var item = new RMC_BestFit.TimeSeriesTypeItem("Daily Discharge", TimeSeriesDownload.TimeSeriesType.DailyDischarge);
            item.DisplayName = "Daily Stage";
            item.Value = TimeSeriesDownload.TimeSeriesType.DailyStage;
            Assert.AreEqual("Daily Stage", item.DisplayName);
            Assert.AreEqual(TimeSeriesDownload.TimeSeriesType.DailyStage, item.Value);
        }
    }
}
