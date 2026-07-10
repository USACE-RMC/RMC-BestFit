using Microsoft.VisualStudio.TestTools.UnitTesting;
using RMC.BestFit.Models;

namespace RMC.BestFit.App.Tests.GUI.Support.ComboBoxItems
{
    /// <summary>
    /// Unit tests for <see cref="RMC_BestFit.TransformTypeItem"/>.
    /// </summary>
    [TestClass]
    public class TransformTypeItemTests
    {
        /// <summary>
        /// Verifies that the constructor stores DisplayName correctly for the None transform.
        /// </summary>
        [TestMethod]
        public void Constructor_None_SetsProperties()
        {
            var item = new RMC_BestFit.TransformTypeItem("None", Transform.None);
            Assert.AreEqual("None", item.DisplayName);
            Assert.AreEqual(Transform.None, item.Value);
        }

        /// <summary>
        /// Verifies that the Logarithmic transform is stored correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_Logarithmic_SetsProperties()
        {
            var item = new RMC_BestFit.TransformTypeItem("Logarithmic", Transform.Logarithmic);
            Assert.AreEqual("Logarithmic", item.DisplayName);
            Assert.AreEqual(Transform.Logarithmic, item.Value);
        }

        /// <summary>
        /// Verifies that the Box-Cox transform is stored correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_BoxCox_SetsProperties()
        {
            var item = new RMC_BestFit.TransformTypeItem("Box-Cox", Transform.BoxCox);
            Assert.AreEqual("Box-Cox", item.DisplayName);
            Assert.AreEqual(Transform.BoxCox, item.Value);
        }

        /// <summary>
        /// Verifies that the Yeo-Johnson transform is stored correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_YeoJohnson_SetsProperties()
        {
            var item = new RMC_BestFit.TransformTypeItem("Yeo-Johnson", Transform.YeoJohnson);
            Assert.AreEqual("Yeo-Johnson", item.DisplayName);
            Assert.AreEqual(Transform.YeoJohnson, item.Value);
        }

        /// <summary>
        /// Verifies that DisplayName and Value are mutable after construction.
        /// </summary>
        [TestMethod]
        public void Properties_AreMutable()
        {
            var item = new RMC_BestFit.TransformTypeItem("None", Transform.None);
            item.DisplayName = "Logarithmic";
            item.Value = Transform.Logarithmic;
            Assert.AreEqual("Logarithmic", item.DisplayName);
            Assert.AreEqual(Transform.Logarithmic, item.Value);
        }
    }
}
