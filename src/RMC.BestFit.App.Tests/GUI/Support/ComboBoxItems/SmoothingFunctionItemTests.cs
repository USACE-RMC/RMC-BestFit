using Microsoft.VisualStudio.TestTools.UnitTesting;
using Numerics.Data;

namespace RMC.BestFit.App.Tests.GUI.Support.ComboBoxItems
{
    /// <summary>
    /// Unit tests for <see cref="RMC_BestFit.SmoothingFunctionItem"/>.
    /// </summary>
    [TestClass]
    public class SmoothingFunctionItemTests
    {
        /// <summary>
        /// Verifies that the None smoothing function item stores all properties correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_None_SetsProperties()
        {
            var item = new RMC_BestFit.SmoothingFunctionItem("None", SmoothingFunctionType.None);
            Assert.AreEqual("None", item.DisplayName);
            Assert.AreEqual(SmoothingFunctionType.None, item.Value);
        }

        /// <summary>
        /// Verifies that the MovingAverage smoothing function item stores properties correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_MovingAverage_SetsProperties()
        {
            var item = new RMC_BestFit.SmoothingFunctionItem("Moving Average", SmoothingFunctionType.MovingAverage);
            Assert.AreEqual("Moving Average", item.DisplayName);
            Assert.AreEqual(SmoothingFunctionType.MovingAverage, item.Value);
        }

        /// <summary>
        /// Verifies that the MovingSum smoothing function item stores properties correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_MovingSum_SetsProperties()
        {
            var item = new RMC_BestFit.SmoothingFunctionItem("Moving Sum", SmoothingFunctionType.MovingSum);
            Assert.AreEqual("Moving Sum", item.DisplayName);
            Assert.AreEqual(SmoothingFunctionType.MovingSum, item.Value);
        }

        /// <summary>
        /// Verifies that the Difference smoothing function item stores properties correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_Difference_SetsProperties()
        {
            var item = new RMC_BestFit.SmoothingFunctionItem("Difference", SmoothingFunctionType.Difference);
            Assert.AreEqual("Difference", item.DisplayName);
            Assert.AreEqual(SmoothingFunctionType.Difference, item.Value);
        }

        /// <summary>
        /// Verifies that DisplayName and Value are mutable after construction.
        /// </summary>
        [TestMethod]
        public void Properties_AreMutable()
        {
            var item = new RMC_BestFit.SmoothingFunctionItem("None", SmoothingFunctionType.None);
            item.DisplayName = "Moving Average";
            item.Value = SmoothingFunctionType.MovingAverage;
            Assert.AreEqual("Moving Average", item.DisplayName);
            Assert.AreEqual(SmoothingFunctionType.MovingAverage, item.Value);
        }
    }
}
