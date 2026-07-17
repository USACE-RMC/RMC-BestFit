using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RMC.BestFit.App.Tests.GUI.Support.ComboBoxItems
{
    /// <summary>
    /// Unit tests for <see cref="RMC_BestFit.MonthItem"/>.
    /// </summary>
    [TestClass]
    public class MonthItemTests
    {
        /// <summary>
        /// Verifies that the constructor stores the display name correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_SetsDisplayName()
        {
            var item = new RMC_BestFit.MonthItem("January", 1);
            Assert.AreEqual("January", item.DisplayName);
        }

        /// <summary>
        /// Verifies that the constructor stores the numeric month value correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_SetsValue()
        {
            var item = new RMC_BestFit.MonthItem("January", 1);
            Assert.AreEqual(1, item.Value);
        }

        /// <summary>
        /// Verifies construction for every calendar month (1–12).
        /// </summary>
        [TestMethod]
        public void Constructor_AllTwelveMonths_RoundTrip()
        {
            string[] names = { "January", "February", "March", "April", "May", "June",
                               "July", "August", "September", "October", "November", "December" };
            for (int m = 1; m <= 12; m++)
            {
                var item = new RMC_BestFit.MonthItem(names[m - 1], m);
                Assert.AreEqual(names[m - 1], item.DisplayName, $"Month {m}: DisplayName mismatch");
                Assert.AreEqual(m, item.Value, $"Month {m}: Value mismatch");
            }
        }

        /// <summary>
        /// Verifies that the DisplayName setter allows mutation after construction.
        /// </summary>
        [TestMethod]
        public void DisplayName_CanBeUpdated()
        {
            var item = new RMC_BestFit.MonthItem("Jan", 1);
            item.DisplayName = "January";
            Assert.AreEqual("January", item.DisplayName);
        }

        /// <summary>
        /// Verifies that the Value setter allows mutation after construction.
        /// </summary>
        [TestMethod]
        public void Value_CanBeUpdated()
        {
            var item = new RMC_BestFit.MonthItem("January", 1);
            item.Value = 2;
            Assert.AreEqual(2, item.Value);
        }

        /// <summary>
        /// Verifies that an empty display name is accepted without error.
        /// </summary>
        [TestMethod]
        public void Constructor_EmptyDisplayName_IsAccepted()
        {
            var item = new RMC_BestFit.MonthItem(string.Empty, 0);
            Assert.AreEqual(string.Empty, item.DisplayName);
            Assert.AreEqual(0, item.Value);
        }
    }
}
