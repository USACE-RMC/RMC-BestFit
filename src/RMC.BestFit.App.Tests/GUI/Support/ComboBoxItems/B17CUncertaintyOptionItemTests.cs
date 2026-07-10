using Microsoft.VisualStudio.TestTools.UnitTesting;
using RMC.BestFit.Analyses;

namespace RMC.BestFit.App.Tests.GUI.Support.ComboBoxItems
{
    /// <summary>
    /// Unit tests for <see cref="RMC_BestFit.B17CUncertaintyOptionItem"/>.
    /// </summary>
    [TestClass]
    public class B17CUncertaintyOptionItemTests
    {
        /// <summary>
        /// Verifies that the MultivariateNormal item stores all properties correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_MultivariateNormal_SetsAllProperties()
        {
            var item = new RMC_BestFit.B17CUncertaintyOptionItem(
                "Multivariate Normal",
                UncertaintyMethod.MultivariateNormal,
                "MVN method.");
            Assert.AreEqual("Multivariate Normal", item.DisplayName);
            Assert.AreEqual(UncertaintyMethod.MultivariateNormal, item.Value);
            Assert.AreEqual("MVN method.", item.ToolTip);
        }

        /// <summary>
        /// Verifies that the Bootstrap item stores properties correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_Bootstrap_SetsProperties()
        {
            var item = new RMC_BestFit.B17CUncertaintyOptionItem("Bootstrap", UncertaintyMethod.Bootstrap);
            Assert.AreEqual("Bootstrap", item.DisplayName);
            Assert.AreEqual(UncertaintyMethod.Bootstrap, item.Value);
        }

        /// <summary>
        /// Verifies that the BiasCorrectedBootstrap item stores properties correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_BiasCorrectedBootstrap_SetsProperties()
        {
            var item = new RMC_BestFit.B17CUncertaintyOptionItem("Bias-Corrected Bootstrap", UncertaintyMethod.BiasCorrectedBootstrap);
            Assert.AreEqual("Bias-Corrected Bootstrap", item.DisplayName);
            Assert.AreEqual(UncertaintyMethod.BiasCorrectedBootstrap, item.Value);
        }

        /// <summary>
        /// Verifies that the LinkedMultivariateNormal item stores properties correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_LinkedMultivariateNormal_SetsProperties()
        {
            var item = new RMC_BestFit.B17CUncertaintyOptionItem("Linked Multivariate Normal", UncertaintyMethod.LinkedMultivariateNormal);
            Assert.AreEqual("Linked Multivariate Normal", item.DisplayName);
            Assert.AreEqual(UncertaintyMethod.LinkedMultivariateNormal, item.Value);
        }

        /// <summary>
        /// Verifies that the tooltip defaults to empty string when not provided.
        /// </summary>
        [TestMethod]
        public void Constructor_NoTooltip_DefaultsToEmpty()
        {
            var item = new RMC_BestFit.B17CUncertaintyOptionItem("Bootstrap", UncertaintyMethod.Bootstrap);
            Assert.AreEqual(string.Empty, item.ToolTip);
        }
    }
}
