using Microsoft.VisualStudio.TestTools.UnitTesting;
using Numerics.Distributions.Copulas;

namespace RMC.BestFit.App.Tests.GUI.Support.ComboBoxItems
{
    /// <summary>
    /// Unit tests for <see cref="RMC_BestFit.CopulaEstimationItem"/>.
    /// </summary>
    [TestClass]
    public class CopulaEstimationItemTests
    {
        /// <summary>
        /// Verifies that the InferenceFromMargins item stores all properties correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_InferenceFromMargins_SetsAllProperties()
        {
            var item = new RMC_BestFit.CopulaEstimationItem(
                "Inference from Margins",
                CopulaEstimationMethod.InferenceFromMargins,
                "IFM method.");
            Assert.AreEqual("Inference from Margins", item.DisplayName);
            Assert.AreEqual(CopulaEstimationMethod.InferenceFromMargins, item.Value);
            Assert.AreEqual("IFM method.", item.ToolTip);
        }

        /// <summary>
        /// Verifies that the PseudoLikelihood item stores properties correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_PseudoLikelihood_SetsProperties()
        {
            var item = new RMC_BestFit.CopulaEstimationItem("Pseudo-Likelihood", CopulaEstimationMethod.PseudoLikelihood);
            Assert.AreEqual("Pseudo-Likelihood", item.DisplayName);
            Assert.AreEqual(CopulaEstimationMethod.PseudoLikelihood, item.Value);
        }

        /// <summary>
        /// Verifies that the tooltip defaults to empty string when not provided.
        /// </summary>
        [TestMethod]
        public void Constructor_NoTooltip_DefaultsToEmpty()
        {
            var item = new RMC_BestFit.CopulaEstimationItem("Pseudo-Likelihood", CopulaEstimationMethod.PseudoLikelihood);
            Assert.AreEqual(string.Empty, item.ToolTip);
        }
    }
}
