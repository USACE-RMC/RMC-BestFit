using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RMC.BestFit.App.Tests.GUI.Support.DataGrids
{
    /// <summary>
    /// Unit tests for <see cref="RMC_BestFit.DSSPathnameItem"/>.
    /// </summary>
    [TestClass]
    public class DSSPathnameItemTests
    {
        /// <summary>
        /// Verifies the constructor stores all six DSS pathname parts in their respective public fields.
        /// </summary>
        [TestMethod]
        public void Constructor_StoresAllSixParts()
        {
            var item = new RMC_BestFit.DSSPathnameItem("A", "B", "C", "D", "E", "F");
            Assert.AreEqual("A", item.PartA);
            Assert.AreEqual("B", item.PartB);
            Assert.AreEqual("C", item.PartC);
            Assert.AreEqual("D", item.PartD);
            Assert.AreEqual("E", item.PartE);
            Assert.AreEqual("F", item.PartF);
        }

        /// <summary>
        /// Verifies the parts can be reassigned (public-field semantics).
        /// </summary>
        [TestMethod]
        public void PartA_CanBeReassigned()
        {
            var item = new RMC_BestFit.DSSPathnameItem("A", "B", "C", "D", "E", "F");
            item.PartA = "Updated";
            Assert.AreEqual("Updated", item.PartA);
        }

        /// <summary>
        /// Verifies that empty strings round-trip correctly through the constructor.
        /// </summary>
        [TestMethod]
        public void Constructor_EmptyStrings_AreAccepted()
        {
            var item = new RMC_BestFit.DSSPathnameItem("", "", "", "", "", "");
            Assert.AreEqual(string.Empty, item.PartA);
            Assert.AreEqual(string.Empty, item.PartF);
        }

        /// <summary>
        /// Verifies that null strings round-trip correctly through the constructor.
        /// </summary>
        [TestMethod]
        public void Constructor_NullStrings_AreAccepted()
        {
            var item = new RMC_BestFit.DSSPathnameItem(null, null, null, null, null, null);
            Assert.IsNull(item.PartA);
            Assert.IsNull(item.PartF);
        }
    }
}
