using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RMC.BestFit.App.Tests.GUI.Support.ComboBoxItems
{
    /// <summary>
    /// Unit tests for <see cref="RMC_BestFit.CredibleIntervalItem"/>.
    /// </summary>
    [TestClass]
    public class CredibleIntervalItemTests
    {
        /// <summary>
        /// Verifies that the 90% credible interval item stores its properties correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_90Percent_StoresCorrectly()
        {
            var item = new RMC_BestFit.CredibleIntervalItem("90%", 0.90);
            Assert.AreEqual("90%", item.DisplayName);
            Assert.AreEqual(0.90, item.Value, 1e-12);
        }

        /// <summary>
        /// Verifies that the 95% credible interval item stores its properties correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_95Percent_StoresCorrectly()
        {
            var item = new RMC_BestFit.CredibleIntervalItem("95%", 0.95);
            Assert.AreEqual("95%", item.DisplayName);
            Assert.AreEqual(0.95, item.Value, 1e-12);
        }

        /// <summary>
        /// Verifies that the 99% credible interval item stores its properties correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_99Percent_StoresCorrectly()
        {
            var item = new RMC_BestFit.CredibleIntervalItem("99%", 0.99);
            Assert.AreEqual("99%", item.DisplayName);
            Assert.AreEqual(0.99, item.Value, 1e-12);
        }

        /// <summary>
        /// Verifies that DisplayName and Value are mutable after construction.
        /// </summary>
        [TestMethod]
        public void Properties_AreMutable()
        {
            var item = new RMC_BestFit.CredibleIntervalItem("90%", 0.90);
            item.DisplayName = "99%";
            item.Value = 0.99;
            Assert.AreEqual("99%", item.DisplayName);
            Assert.AreEqual(0.99, item.Value, 1e-12);
        }

        /// <summary>
        /// Verifies that a zero value (degenerate interval) is accepted.
        /// </summary>
        [TestMethod]
        public void Constructor_ZeroValue_IsAccepted()
        {
            var item = new RMC_BestFit.CredibleIntervalItem("0%", 0.0);
            Assert.AreEqual(0.0, item.Value, 1e-12);
        }
    }
}
