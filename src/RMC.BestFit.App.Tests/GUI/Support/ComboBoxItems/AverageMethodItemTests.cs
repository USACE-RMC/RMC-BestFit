using Microsoft.VisualStudio.TestTools.UnitTesting;
using ModelAnalyses = RMC.BestFit.Analyses;

namespace RMC.BestFit.App.Tests.GUI.Support.ComboBoxItems
{
    /// <summary>
    /// Unit tests for <see cref="RMC_BestFit.AverageMethodItem"/>.
    /// </summary>
    [TestClass]
    public class AverageMethodItemTests
    {
        /// <summary>
        /// Verifies that the AIC average method item stores all properties correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_AIC_SetsAllProperties()
        {
            var item = new RMC_BestFit.AverageMethodItem("AIC", ModelAnalyses.AverageMethod.AIC, "Weighted by AIC.");
            Assert.AreEqual("AIC", item.DisplayName);
            Assert.AreEqual(ModelAnalyses.AverageMethod.AIC, item.Value);
            Assert.AreEqual("Weighted by AIC.", item.ToolTip);
        }

        /// <summary>
        /// Verifies that the BIC average method item stores properties correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_BIC_SetsProperties()
        {
            var item = new RMC_BestFit.AverageMethodItem("BIC", ModelAnalyses.AverageMethod.BIC);
            Assert.AreEqual("BIC", item.DisplayName);
            Assert.AreEqual(ModelAnalyses.AverageMethod.BIC, item.Value);
        }

        /// <summary>
        /// Verifies that the tooltip defaults to empty string when not provided.
        /// </summary>
        [TestMethod]
        public void Constructor_NoTooltip_DefaultsToEmpty()
        {
            var item = new RMC_BestFit.AverageMethodItem("Equal", ModelAnalyses.AverageMethod.Equal);
            Assert.AreEqual(string.Empty, item.ToolTip);
        }

        /// <summary>
        /// Verifies round-trip construction for all model-averaging methods used in the UI.
        /// </summary>
        [TestMethod]
        public void Constructor_AllAverageMethods_RoundTrip()
        {
            var pairs = new System.Collections.Generic.List<(string, ModelAnalyses.AverageMethod)>
            {
                ("AIC",    ModelAnalyses.AverageMethod.AIC),
                ("BIC",    ModelAnalyses.AverageMethod.BIC),
                ("DIC",    ModelAnalyses.AverageMethod.DIC),
                ("WAIC",   ModelAnalyses.AverageMethod.WAIC),
                ("LOO-CV", ModelAnalyses.AverageMethod.LOOIC),
                ("RMSE",   ModelAnalyses.AverageMethod.RMSE),
                ("Equal",  ModelAnalyses.AverageMethod.Equal),
            };
            foreach (var (name, method) in pairs)
            {
                var item = new RMC_BestFit.AverageMethodItem(name, method);
                Assert.AreEqual(name, item.DisplayName, $"DisplayName mismatch for {method}");
                Assert.AreEqual(method, item.Value, $"Value mismatch for {method}");
            }
        }
    }
}
