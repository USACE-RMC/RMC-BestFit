using Microsoft.VisualStudio.TestTools.UnitTesting;
using Numerics.Distributions;

namespace RMC.BestFit.App.Tests.GUI.Support.ComboBoxItems
{
    /// <summary>
    /// Unit tests for <see cref="RMC_BestFit.ParameterPriorItem"/>.
    /// </summary>
    [TestClass]
    public class ParameterPriorItemTests
    {
        /// <summary>
        /// Verifies the constructor stores the parameter <c>Name</c> correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_StoresName()
        {
            var dist = new Normal(0, 1);
            var item = new RMC_BestFit.ParameterPriorItem("mu", dist, "Location");
            Assert.AreEqual("mu", item.Name);
        }

        /// <summary>
        /// Verifies the constructor stores the prior <c>Distribution</c> reference correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_StoresDistribution()
        {
            var dist = new Normal(0, 1);
            var item = new RMC_BestFit.ParameterPriorItem("mu", dist, "Location");
            Assert.AreSame(dist, item.Distribution);
        }

        /// <summary>
        /// Verifies the constructor stores the display <c>Label</c> correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_StoresLabel()
        {
            var item = new RMC_BestFit.ParameterPriorItem("mu", new Normal(0, 1), "Location");
            Assert.AreEqual("Location", item.Label);
        }

        /// <summary>
        /// Verifies <c>Name</c> can be reassigned (mutable property semantics).
        /// </summary>
        [TestMethod]
        public void Name_IsMutable()
        {
            var item = new RMC_BestFit.ParameterPriorItem("a", new Normal(0, 1), "x");
            item.Name = "b";
            Assert.AreEqual("b", item.Name);
        }

        /// <summary>
        /// Verifies <c>Distribution</c> can be reassigned (mutable property semantics).
        /// </summary>
        [TestMethod]
        public void Distribution_IsMutable()
        {
            var item = new RMC_BestFit.ParameterPriorItem("a", new Normal(0, 1), "x");
            var newDist = new Normal(5, 2);
            item.Distribution = newDist;
            Assert.AreSame(newDist, item.Distribution);
        }

        /// <summary>
        /// Verifies <c>Label</c> can be reassigned (mutable property semantics).
        /// </summary>
        [TestMethod]
        public void Label_IsMutable()
        {
            var item = new RMC_BestFit.ParameterPriorItem("a", new Normal(0, 1), "x");
            item.Label = "Updated";
            Assert.AreEqual("Updated", item.Label);
        }
    }
}
