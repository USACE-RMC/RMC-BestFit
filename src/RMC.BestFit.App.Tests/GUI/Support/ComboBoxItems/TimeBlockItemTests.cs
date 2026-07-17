using Microsoft.VisualStudio.TestTools.UnitTesting;
using Numerics.Data;

namespace RMC.BestFit.App.Tests.GUI.Support.ComboBoxItems
{
    /// <summary>
    /// Unit tests for <see cref="RMC_BestFit.TimeBlockItem"/>.
    /// </summary>
    [TestClass]
    public class TimeBlockItemTests
    {
        /// <summary>
        /// Verifies that the CalendarYear time block item stores all properties correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_CalendarYear_SetsProperties()
        {
            var item = new RMC_BestFit.TimeBlockItem("Calendar Year", TimeBlockWindow.CalendarYear);
            Assert.AreEqual("Calendar Year", item.DisplayName);
            Assert.AreEqual(TimeBlockWindow.CalendarYear, item.Value);
        }

        /// <summary>
        /// Verifies that the WaterYear time block item stores properties correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_WaterYear_SetsProperties()
        {
            var item = new RMC_BestFit.TimeBlockItem("Water Year", TimeBlockWindow.WaterYear);
            Assert.AreEqual("Water Year", item.DisplayName);
            Assert.AreEqual(TimeBlockWindow.WaterYear, item.Value);
        }

        /// <summary>
        /// Verifies that the CustomYear time block item stores properties correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_CustomYear_SetsProperties()
        {
            var item = new RMC_BestFit.TimeBlockItem("Custom Year", TimeBlockWindow.CustomYear);
            Assert.AreEqual("Custom Year", item.DisplayName);
            Assert.AreEqual(TimeBlockWindow.CustomYear, item.Value);
        }

        /// <summary>
        /// Verifies that DisplayName and Value are mutable after construction.
        /// </summary>
        [TestMethod]
        public void Properties_AreMutable()
        {
            var item = new RMC_BestFit.TimeBlockItem("Calendar Year", TimeBlockWindow.CalendarYear);
            item.DisplayName = "Water Year";
            item.Value = TimeBlockWindow.WaterYear;
            Assert.AreEqual("Water Year", item.DisplayName);
            Assert.AreEqual(TimeBlockWindow.WaterYear, item.Value);
        }
    }
}
