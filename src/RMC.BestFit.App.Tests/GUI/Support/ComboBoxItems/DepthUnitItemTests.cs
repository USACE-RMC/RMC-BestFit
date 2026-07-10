using Microsoft.VisualStudio.TestTools.UnitTesting;
using Numerics.Data;
using RMC.BestFit.UI;

namespace RMC.BestFit.App.Tests.GUI.Support.ComboBoxItems
{
    /// <summary>
    /// Unit tests for <see cref="RMC_BestFit.DepthUnitItem"/>.
    /// </summary>
    [TestClass]
    public class DepthUnitItemTests
    {
        /// <summary>
        /// Verifies that the Millimeters depth unit item stores all properties correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_Millimeters_SetsProperties()
        {
            var item = new RMC_BestFit.DepthUnitItem("Millimeters", TimeSeriesDownload.DepthUnit.Millimeters);
            Assert.AreEqual("Millimeters", item.DisplayName);
            Assert.AreEqual(TimeSeriesDownload.DepthUnit.Millimeters, item.Value);
        }

        /// <summary>
        /// Verifies that the Centimeters depth unit item stores properties correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_Centimeters_SetsProperties()
        {
            var item = new RMC_BestFit.DepthUnitItem("Centimeters", TimeSeriesDownload.DepthUnit.Centimeters);
            Assert.AreEqual("Centimeters", item.DisplayName);
            Assert.AreEqual(TimeSeriesDownload.DepthUnit.Centimeters, item.Value);
        }

        /// <summary>
        /// Verifies that the Inches depth unit item stores properties correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_Inches_SetsProperties()
        {
            var item = new RMC_BestFit.DepthUnitItem("Inches", TimeSeriesDownload.DepthUnit.Inches);
            Assert.AreEqual("Inches", item.DisplayName);
            Assert.AreEqual(TimeSeriesDownload.DepthUnit.Inches, item.Value);
        }

        /// <summary>
        /// Verifies that DisplayName and Value are mutable after construction.
        /// </summary>
        [TestMethod]
        public void Properties_AreMutable()
        {
            var item = new RMC_BestFit.DepthUnitItem("Millimeters", TimeSeriesDownload.DepthUnit.Millimeters);
            item.DisplayName = "Inches";
            item.Value = TimeSeriesDownload.DepthUnit.Inches;
            Assert.AreEqual("Inches", item.DisplayName);
            Assert.AreEqual(TimeSeriesDownload.DepthUnit.Inches, item.Value);
        }
    }
}
