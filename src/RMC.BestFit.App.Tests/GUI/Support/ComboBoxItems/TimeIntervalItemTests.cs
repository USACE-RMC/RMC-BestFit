using Microsoft.VisualStudio.TestTools.UnitTesting;
using Numerics.Data;

namespace RMC.BestFit.App.Tests.GUI.Support.ComboBoxItems
{
    /// <summary>
    /// Unit tests for <see cref="RMC_BestFit.TimeIntervalItem"/>.
    /// </summary>
    [TestClass]
    public class TimeIntervalItemTests
    {
        /// <summary>
        /// Verifies that the constructor stores DisplayName correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_SetsDisplayName()
        {
            var item = new RMC_BestFit.TimeIntervalItem("1-Year", TimeInterval.OneYear);
            Assert.AreEqual("1-Year", item.DisplayName);
        }

        /// <summary>
        /// Verifies that the constructor stores the TimeInterval value correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_SetsValue()
        {
            var item = new RMC_BestFit.TimeIntervalItem("1-Year", TimeInterval.OneYear);
            Assert.AreEqual(TimeInterval.OneYear, item.Value);
        }

        /// <summary>
        /// Verifies that a round-trip construction for OneDay works correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_OneDay_RoundTrip()
        {
            var item = new RMC_BestFit.TimeIntervalItem("1-Day", TimeInterval.OneDay);
            Assert.AreEqual("1-Day", item.DisplayName);
            Assert.AreEqual(TimeInterval.OneDay, item.Value);
        }

        /// <summary>
        /// Verifies that a round-trip construction for OneHour works correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_OneHour_RoundTrip()
        {
            var item = new RMC_BestFit.TimeIntervalItem("1-Hr", TimeInterval.OneHour);
            Assert.AreEqual("1-Hr", item.DisplayName);
            Assert.AreEqual(TimeInterval.OneHour, item.Value);
        }

        /// <summary>
        /// Verifies that a round-trip construction for OneMonth works correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_OneMonth_RoundTrip()
        {
            var item = new RMC_BestFit.TimeIntervalItem("1-Month", TimeInterval.OneMonth);
            Assert.AreEqual("1-Month", item.DisplayName);
            Assert.AreEqual(TimeInterval.OneMonth, item.Value);
        }

        /// <summary>
        /// Verifies that DisplayName and Value are mutable after construction.
        /// </summary>
        [TestMethod]
        public void Properties_AreMutable()
        {
            var item = new RMC_BestFit.TimeIntervalItem("1-Year", TimeInterval.OneYear);
            item.DisplayName = "15-Min";
            item.Value = TimeInterval.FifteenMinute;
            Assert.AreEqual("15-Min", item.DisplayName);
            Assert.AreEqual(TimeInterval.FifteenMinute, item.Value);
        }
    }
}
