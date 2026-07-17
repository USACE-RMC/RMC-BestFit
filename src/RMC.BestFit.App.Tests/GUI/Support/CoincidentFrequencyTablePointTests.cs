using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RMC.BestFit.App.Tests.GUI.Support
{
    /// <summary>
    /// Unit tests for <see cref="RMC_BestFit.CoincidentFrequencyTablePoint"/>.
    /// </summary>
    [TestClass]
    public class CoincidentFrequencyTablePointTests
    {
        /// <summary>
        /// Verifies the constructor stores all five values into their corresponding public properties.
        /// </summary>
        [TestMethod]
        public void Constructor_StoresAllProperties()
        {
            var point = new RMC_BestFit.CoincidentFrequencyTablePoint(z: 100.0, lower: 0.001, upper: 0.05, predictive: 0.01, mode: 0.008);
            Assert.AreEqual(100.0, point.Z, 1e-12);
            Assert.AreEqual(0.001, point.Lower, 1e-12);
            Assert.AreEqual(0.05, point.Upper, 1e-12);
            Assert.AreEqual(0.01, point.Predictive, 1e-12);
            Assert.AreEqual(0.008, point.Mode, 1e-12);
        }

        /// <summary>
        /// Verifies <c>Z</c> can be reassigned (mutable property semantics).
        /// </summary>
        [TestMethod]
        public void Z_CanBeReassigned()
        {
            var point = new RMC_BestFit.CoincidentFrequencyTablePoint(0, 0, 0, 0, 0);
            point.Z = 42.0;
            Assert.AreEqual(42.0, point.Z, 1e-12);
        }

        /// <summary>
        /// Verifies <c>Lower</c> can be reassigned.
        /// </summary>
        [TestMethod]
        public void Lower_CanBeReassigned()
        {
            var point = new RMC_BestFit.CoincidentFrequencyTablePoint(0, 0, 0, 0, 0);
            point.Lower = 0.001;
            Assert.AreEqual(0.001, point.Lower, 1e-12);
        }

        /// <summary>
        /// Verifies <c>Upper</c> can be reassigned.
        /// </summary>
        [TestMethod]
        public void Upper_CanBeReassigned()
        {
            var point = new RMC_BestFit.CoincidentFrequencyTablePoint(0, 0, 0, 0, 0);
            point.Upper = 0.999;
            Assert.AreEqual(0.999, point.Upper, 1e-12);
        }

        /// <summary>
        /// Verifies <c>Predictive</c> can be reassigned.
        /// </summary>
        [TestMethod]
        public void Predictive_CanBeReassigned()
        {
            var point = new RMC_BestFit.CoincidentFrequencyTablePoint(0, 0, 0, 0, 0);
            point.Predictive = 0.05;
            Assert.AreEqual(0.05, point.Predictive, 1e-12);
        }

        /// <summary>
        /// Verifies <c>Mode</c> can be reassigned.
        /// </summary>
        [TestMethod]
        public void Mode_CanBeReassigned()
        {
            var point = new RMC_BestFit.CoincidentFrequencyTablePoint(0, 0, 0, 0, 0);
            point.Mode = 0.01;
            Assert.AreEqual(0.01, point.Mode, 1e-12);
        }
    }
}
