using Microsoft.VisualStudio.TestTools.UnitTesting;
using ModelAnalyses = RMC.BestFit.Analyses;

namespace RMC.BestFit.App.Tests.GUI.Support.ComboBoxItems
{
    /// <summary>
    /// Unit tests for <see cref="RMC_BestFit.CompositeTypeItem"/>.
    /// </summary>
    [TestClass]
    public class CompositeTypeItemTests
    {
        /// <summary>
        /// Verifies that the CompetingRisks item stores all properties correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_CompetingRisks_SetsAllProperties()
        {
            string tip = "Competing risks model.";
            var item = new RMC_BestFit.CompositeTypeItem("Competing Risks", ModelAnalyses.CompositeType.CompetingRisks, tip);
            Assert.AreEqual("Competing Risks", item.DisplayName);
            Assert.AreEqual(ModelAnalyses.CompositeType.CompetingRisks, item.Value);
            Assert.AreEqual(tip, item.ToolTip);
        }

        /// <summary>
        /// Verifies that the Mixture composite type stores properties correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_Mixture_SetsProperties()
        {
            var item = new RMC_BestFit.CompositeTypeItem("Mixture Distribution", ModelAnalyses.CompositeType.Mixture);
            Assert.AreEqual("Mixture Distribution", item.DisplayName);
            Assert.AreEqual(ModelAnalyses.CompositeType.Mixture, item.Value);
        }

        /// <summary>
        /// Verifies that the ModelAverage composite type stores properties correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_ModelAverage_SetsProperties()
        {
            var item = new RMC_BestFit.CompositeTypeItem("Model Average", ModelAnalyses.CompositeType.ModelAverage);
            Assert.AreEqual("Model Average", item.DisplayName);
            Assert.AreEqual(ModelAnalyses.CompositeType.ModelAverage, item.Value);
        }

        /// <summary>
        /// Verifies that the tooltip defaults to empty string when not provided.
        /// </summary>
        [TestMethod]
        public void Constructor_NoTooltip_DefaultsToEmpty()
        {
            var item = new RMC_BestFit.CompositeTypeItem("Mixture Distribution", ModelAnalyses.CompositeType.Mixture);
            Assert.AreEqual(string.Empty, item.ToolTip);
        }
    }
}
