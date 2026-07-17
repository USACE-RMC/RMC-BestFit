using Microsoft.VisualStudio.TestTools.UnitTesting;
using Numerics.Distributions.Copulas;

namespace RMC.BestFit.App.Tests.GUI.Support.ComboBoxItems
{
    /// <summary>
    /// Unit tests for <see cref="RMC_BestFit.CopulaItem"/>.
    /// </summary>
    [TestClass]
    public class CopulaItemTests
    {
        /// <summary>
        /// Verifies that the constructor stores DisplayName correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_SetsDisplayName()
        {
            var item = new RMC_BestFit.CopulaItem("Normal", CopulaType.Normal);
            Assert.AreEqual("Normal", item.DisplayName);
        }

        /// <summary>
        /// Verifies that the constructor stores the CopulaType value correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_SetsValue()
        {
            var item = new RMC_BestFit.CopulaItem("Normal", CopulaType.Normal);
            Assert.AreEqual(CopulaType.Normal, item.Value);
        }

        /// <summary>
        /// Verifies round-trip construction for the Frank copula type.
        /// </summary>
        [TestMethod]
        public void Constructor_Frank_RoundTrip()
        {
            var item = new RMC_BestFit.CopulaItem("Frank", CopulaType.Frank);
            Assert.AreEqual("Frank", item.DisplayName);
            Assert.AreEqual(CopulaType.Frank, item.Value);
        }

        /// <summary>
        /// Verifies round-trip construction for the Gumbel copula type.
        /// </summary>
        [TestMethod]
        public void Constructor_Gumbel_RoundTrip()
        {
            var item = new RMC_BestFit.CopulaItem("Gumbel", CopulaType.Gumbel);
            Assert.AreEqual("Gumbel", item.DisplayName);
            Assert.AreEqual(CopulaType.Gumbel, item.Value);
        }

        /// <summary>
        /// Verifies round-trip construction for the Student's t copula type.
        /// Guards the 2-parameter copula option against accidental removal from
        /// the dropdown list.
        /// </summary>
        [TestMethod]
        public void Constructor_StudentT_RoundTrip()
        {
            var item = new RMC_BestFit.CopulaItem("Student's t", CopulaType.StudentT);
            Assert.AreEqual("Student's t", item.DisplayName);
            Assert.AreEqual(CopulaType.StudentT, item.Value);
        }

        /// <summary>
        /// Verifies that DisplayName and Value are mutable after construction.
        /// </summary>
        [TestMethod]
        public void Properties_AreMutable()
        {
            var item = new RMC_BestFit.CopulaItem("Normal", CopulaType.Normal);
            item.DisplayName = "Clayton";
            item.Value = CopulaType.Clayton;
            Assert.AreEqual("Clayton", item.DisplayName);
            Assert.AreEqual(CopulaType.Clayton, item.Value);
        }
    }
}
