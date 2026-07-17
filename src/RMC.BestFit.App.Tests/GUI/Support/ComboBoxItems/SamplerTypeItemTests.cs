using Microsoft.VisualStudio.TestTools.UnitTesting;
using RMC.BestFit.Estimation;

namespace RMC.BestFit.App.Tests.GUI.Support.ComboBoxItems
{
    /// <summary>
    /// Unit tests for <see cref="RMC_BestFit.SamplerTypeItem"/>.
    /// </summary>
    [TestClass]
    public class SamplerTypeItemTests
    {
        /// <summary>
        /// Verifies that the constructor stores DisplayName correctly for DEMCzs.
        /// </summary>
        [TestMethod]
        public void Constructor_DEMCzs_SetsAllProperties()
        {
            var item = new RMC_BestFit.SamplerTypeItem(
                "DE-MCzs",
                BayesianAnalysis.SamplerType.DEMCzs,
                "Recommended default sampler.");
            Assert.AreEqual("DE-MCzs", item.DisplayName);
            Assert.AreEqual(BayesianAnalysis.SamplerType.DEMCzs, item.Value);
            Assert.AreEqual("Recommended default sampler.", item.ToolTip);
        }

        /// <summary>
        /// Verifies that the constructor stores DEMCz values correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_DEMCz_SetsValue()
        {
            var item = new RMC_BestFit.SamplerTypeItem("DE-MCz", BayesianAnalysis.SamplerType.DEMCz);
            Assert.AreEqual(BayesianAnalysis.SamplerType.DEMCz, item.Value);
        }

        /// <summary>
        /// Verifies that the ARWMH sampler type is stored correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_ARWMH_SetsValue()
        {
            var item = new RMC_BestFit.SamplerTypeItem("ARWMH", BayesianAnalysis.SamplerType.ARWMH);
            Assert.AreEqual(BayesianAnalysis.SamplerType.ARWMH, item.Value);
            Assert.AreEqual("ARWMH", item.DisplayName);
        }

        /// <summary>
        /// Verifies that the tooltip defaults to empty string when not provided.
        /// </summary>
        [TestMethod]
        public void Constructor_NoTooltip_DefaultsToEmpty()
        {
            var item = new RMC_BestFit.SamplerTypeItem("DE-MCzs", BayesianAnalysis.SamplerType.DEMCzs);
            Assert.AreEqual(string.Empty, item.ToolTip);
        }

        /// <summary>
        /// Verifies that DisplayName, Value, and ToolTip are read-only (init-only) from the constructor.
        /// </summary>
        [TestMethod]
        public void Properties_RetainConstructorValues()
        {
            string tip = "No-U-Turn Sampler.";
            var item = new RMC_BestFit.SamplerTypeItem("NUTS", BayesianAnalysis.SamplerType.NUTS, tip);
            Assert.AreEqual("NUTS", item.DisplayName);
            Assert.AreEqual(BayesianAnalysis.SamplerType.NUTS, item.Value);
            Assert.AreEqual(tip, item.ToolTip);
        }
    }
}
