using Microsoft.VisualStudio.TestTools.UnitTesting;
using RMC.BestFit.Models;

namespace RMC.BestFit.App.Tests.GUI.Support.ComboBoxItems
{
    /// <summary>
    /// Unit tests for <see cref="RMC_BestFit.CovariateExtensionItem"/>.
    /// </summary>
    [TestClass]
    public class CovariateExtensionItemTests
    {
        /// <summary>
        /// Verifies the constructor stores the display name through the public property.
        /// </summary>
        [TestMethod]
        public void Constructor_StoresDisplayName()
        {
            var item = new RMC_BestFit.CovariateExtensionItem("Block Bootstrap", ARIMAX.CovariateExtensionMethod.BlockBootstrap);
            Assert.AreEqual("Block Bootstrap", item.DisplayName);
        }

        /// <summary>
        /// Verifies the constructor stores the enumeration value through the public property.
        /// </summary>
        [TestMethod]
        public void Constructor_StoresValue()
        {
            var item = new RMC_BestFit.CovariateExtensionItem("KNN", ARIMAX.CovariateExtensionMethod.KNN);
            Assert.AreEqual(ARIMAX.CovariateExtensionMethod.KNN, item.Value);
        }

        /// <summary>
        /// Verifies <c>DisplayName</c> can be reassigned.
        /// </summary>
        [TestMethod]
        public void DisplayName_IsMutable()
        {
            var item = new RMC_BestFit.CovariateExtensionItem("A", ARIMAX.CovariateExtensionMethod.None);
            item.DisplayName = "B";
            Assert.AreEqual("B", item.DisplayName);
        }

        /// <summary>
        /// Verifies <c>Value</c> can be reassigned.
        /// </summary>
        [TestMethod]
        public void Value_IsMutable()
        {
            var item = new RMC_BestFit.CovariateExtensionItem("A", ARIMAX.CovariateExtensionMethod.None);
            item.Value = ARIMAX.CovariateExtensionMethod.BlockBootstrap;
            Assert.AreEqual(ARIMAX.CovariateExtensionMethod.BlockBootstrap, item.Value);
        }

        /// <summary>
        /// Verifies all three <see cref="ARIMAX.CovariateExtensionMethod"/> enum values round-trip.
        /// </summary>
        [TestMethod]
        public void Constructor_AllEnumValues_RoundTrip()
        {
            var none = new RMC_BestFit.CovariateExtensionItem("None", ARIMAX.CovariateExtensionMethod.None);
            var bb = new RMC_BestFit.CovariateExtensionItem("Block Bootstrap", ARIMAX.CovariateExtensionMethod.BlockBootstrap);
            var knn = new RMC_BestFit.CovariateExtensionItem("KNN", ARIMAX.CovariateExtensionMethod.KNN);
            Assert.AreEqual(ARIMAX.CovariateExtensionMethod.None, none.Value);
            Assert.AreEqual(ARIMAX.CovariateExtensionMethod.BlockBootstrap, bb.Value);
            Assert.AreEqual(ARIMAX.CovariateExtensionMethod.KNN, knn.Value);
        }
    }
}
