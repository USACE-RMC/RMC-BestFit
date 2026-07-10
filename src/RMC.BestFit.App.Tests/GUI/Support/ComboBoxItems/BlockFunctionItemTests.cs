using Microsoft.VisualStudio.TestTools.UnitTesting;
using Numerics.Data;

namespace RMC.BestFit.App.Tests.GUI.Support.ComboBoxItems
{
    /// <summary>
    /// Unit tests for <see cref="RMC_BestFit.BlockFunctionItem"/>.
    /// </summary>
    [TestClass]
    public class BlockFunctionItemTests
    {
        /// <summary>
        /// Verifies that the constructor stores DisplayName correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_SetsDisplayName()
        {
            var item = new RMC_BestFit.BlockFunctionItem("Maximum", BlockFunctionType.Maximum);
            Assert.AreEqual("Maximum", item.DisplayName);
        }

        /// <summary>
        /// Verifies that the constructor stores the BlockFunctionType value correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_SetsValue()
        {
            var item = new RMC_BestFit.BlockFunctionItem("Maximum", BlockFunctionType.Maximum);
            Assert.AreEqual(BlockFunctionType.Maximum, item.Value);
        }

        /// <summary>
        /// Verifies round-trip construction for the Minimum function type.
        /// </summary>
        [TestMethod]
        public void Constructor_Minimum_RoundTrip()
        {
            var item = new RMC_BestFit.BlockFunctionItem("Minimum", BlockFunctionType.Minimum);
            Assert.AreEqual("Minimum", item.DisplayName);
            Assert.AreEqual(BlockFunctionType.Minimum, item.Value);
        }

        /// <summary>
        /// Verifies that DisplayName and Value are mutable after construction.
        /// </summary>
        [TestMethod]
        public void Properties_AreMutable()
        {
            var item = new RMC_BestFit.BlockFunctionItem("Maximum", BlockFunctionType.Maximum);
            item.DisplayName = "Average";
            item.Value = BlockFunctionType.Average;
            Assert.AreEqual("Average", item.DisplayName);
            Assert.AreEqual(BlockFunctionType.Average, item.Value);
        }
    }
}
