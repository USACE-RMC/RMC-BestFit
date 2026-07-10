using Microsoft.VisualStudio.TestTools.UnitTesting;
using RMC.BestFit.Models;

namespace RMC.BestFit.App.Tests.GUI.Support.ComboBoxItems
{
    /// <summary>
    /// Unit tests for <see cref="RMC_BestFit.TrendTypeItem"/>.
    /// </summary>
    [TestClass]
    public class TrendTypeItemTests
    {
        /// <summary>
        /// Verifies that the None trend type stores all properties correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_None_SetsProperties()
        {
            var item = new RMC_BestFit.TrendTypeItem("None", ARIMAX.Trend.None);
            Assert.AreEqual("None", item.DisplayName);
            Assert.AreEqual(ARIMAX.Trend.None, item.Value);
        }

        /// <summary>
        /// Verifies that the Linear trend type stores all properties correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_Linear_SetsProperties()
        {
            var item = new RMC_BestFit.TrendTypeItem("Linear", ARIMAX.Trend.Linear);
            Assert.AreEqual("Linear", item.DisplayName);
            Assert.AreEqual(ARIMAX.Trend.Linear, item.Value);
        }

        /// <summary>
        /// Verifies that the Quadratic trend type stores all properties correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_Quadratic_SetsProperties()
        {
            var item = new RMC_BestFit.TrendTypeItem("Quadratic", ARIMAX.Trend.Quadratic);
            Assert.AreEqual("Quadratic", item.DisplayName);
            Assert.AreEqual(ARIMAX.Trend.Quadratic, item.Value);
        }

        /// <summary>
        /// Verifies that the Cubic trend type stores all properties correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_Cubic_SetsProperties()
        {
            var item = new RMC_BestFit.TrendTypeItem("Cubic", ARIMAX.Trend.Cubic);
            Assert.AreEqual("Cubic", item.DisplayName);
            Assert.AreEqual(ARIMAX.Trend.Cubic, item.Value);
        }

        /// <summary>
        /// Verifies that DisplayName and Value are mutable after construction.
        /// </summary>
        [TestMethod]
        public void Properties_AreMutable()
        {
            var item = new RMC_BestFit.TrendTypeItem("None", ARIMAX.Trend.None);
            item.DisplayName = "Linear";
            item.Value = ARIMAX.Trend.Linear;
            Assert.AreEqual("Linear", item.DisplayName);
            Assert.AreEqual(ARIMAX.Trend.Linear, item.Value);
        }
    }
}
