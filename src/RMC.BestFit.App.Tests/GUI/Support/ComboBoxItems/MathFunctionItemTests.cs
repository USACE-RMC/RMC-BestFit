using Microsoft.VisualStudio.TestTools.UnitTesting;
using Numerics.Data;

namespace RMC.BestFit.App.Tests.GUI.Support.ComboBoxItems
{
    /// <summary>
    /// Unit tests for <see cref="RMC_BestFit.MathFunctionItem"/>.
    /// </summary>
    [TestClass]
    public class MathFunctionItemTests
    {
        /// <summary>
        /// Verifies that the constructor stores DisplayName and Value (ordinal 0) correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_SetsDisplayName()
        {
            var firstValue = (MathFunctionType)0;
            var item = new RMC_BestFit.MathFunctionItem("Function A", firstValue);
            Assert.AreEqual("Function A", item.DisplayName);
            Assert.AreEqual(firstValue, item.Value);
        }

        /// <summary>
        /// Verifies that the constructor stores a second enum ordinal correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_SecondOrdinal_SetsValue()
        {
            var secondValue = (MathFunctionType)1;
            var item = new RMC_BestFit.MathFunctionItem("Function B", secondValue);
            Assert.AreEqual("Function B", item.DisplayName);
            Assert.AreEqual(secondValue, item.Value);
        }

        /// <summary>
        /// Verifies that DisplayName and Value are mutable after construction.
        /// </summary>
        [TestMethod]
        public void Properties_AreMutable()
        {
            var first = (MathFunctionType)0;
            var second = (MathFunctionType)1;
            var item = new RMC_BestFit.MathFunctionItem("A", first);
            item.DisplayName = "B";
            item.Value = second;
            Assert.AreEqual("B", item.DisplayName);
            Assert.AreEqual(second, item.Value);
        }
    }
}
