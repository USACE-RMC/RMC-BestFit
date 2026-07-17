using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace RMC.BestFit.App.Tests.GUI.Support
{
    /// <summary>
    /// Unit tests for <see cref="RMC_BestFit.FrequencyCurvePoint"/>.
    /// </summary>
    [TestClass]
    public class FrequencyCurvePointTests
    {
        /// <summary>
        /// Verifies that the probability-based constructor stores all five arguments correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_Probability_SetsAllProperties()
        {
            var point = new RMC_BestFit.FrequencyCurvePoint(0.01, 15000.0, 5000.0, 12000.0, 10000.0);
            Assert.AreEqual(0.01, point.Probability, 1e-12);
            Assert.AreEqual(15000.0, point.Upper, 1e-6);
            Assert.AreEqual(5000.0, point.Lower, 1e-6);
            Assert.AreEqual(12000.0, point.Predictive, 1e-6);
            Assert.AreEqual(10000.0, point.Mode, 1e-6);
        }

        /// <summary>
        /// Verifies that the X-Y constructor stores all six arguments correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_XY_SetsAllProperties()
        {
            var point = new RMC_BestFit.FrequencyCurvePoint(2.0, 10000.0, 15000.0, 5000.0, 12000.0, 10000.0);
            Assert.AreEqual(2.0, point.X, 1e-12);
            Assert.AreEqual(10000.0, point.Y, 1e-6);
            Assert.AreEqual(15000.0, point.Upper, 1e-6);
            Assert.AreEqual(5000.0, point.Lower, 1e-6);
            Assert.AreEqual(12000.0, point.Predictive, 1e-6);
            Assert.AreEqual(10000.0, point.Mode, 1e-6);
        }

        /// <summary>
        /// Verifies that the DateTime constructor stores all five arguments correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_DateTime_SetsAllProperties()
        {
            var dt = new DateTime(2020, 5, 1);
            var point = new RMC_BestFit.FrequencyCurvePoint(dt, 15000.0, 5000.0, 12000.0, 10000.0);
            Assert.AreEqual(dt, point.DateTime);
            Assert.AreEqual(15000.0, point.Upper, 1e-6);
            Assert.AreEqual(5000.0, point.Lower, 1e-6);
            Assert.AreEqual(12000.0, point.Predictive, 1e-6);
            Assert.AreEqual(10000.0, point.Mode, 1e-6);
        }

        /// <summary>
        /// Verifies that the Probability constructor leaves X, Y, DateTime at their defaults.
        /// </summary>
        [TestMethod]
        public void Constructor_Probability_LeavesXYAtDefault()
        {
            var point = new RMC_BestFit.FrequencyCurvePoint(0.5, 2.0, 1.0, 1.5, 1.2);
            Assert.AreEqual(0.0, point.X, 1e-12);
            Assert.AreEqual(0.0, point.Y, 1e-12);
        }

        /// <summary>
        /// Verifies that all properties are mutable after construction.
        /// </summary>
        [TestMethod]
        public void Properties_AreMutable()
        {
            var point = new RMC_BestFit.FrequencyCurvePoint(0.01, 1.0, 0.5, 0.8, 0.7);
            point.Probability = 0.02;
            point.Upper = 2.0;
            point.Lower = 1.0;
            point.Predictive = 1.5;
            point.Mode = 1.2;
            point.X = 100.0;
            point.Y = 200.0;
            Assert.AreEqual(0.02, point.Probability, 1e-12);
            Assert.AreEqual(2.0, point.Upper, 1e-12);
            Assert.AreEqual(1.0, point.Lower, 1e-12);
            Assert.AreEqual(1.5, point.Predictive, 1e-12);
            Assert.AreEqual(1.2, point.Mode, 1e-12);
            Assert.AreEqual(100.0, point.X, 1e-12);
            Assert.AreEqual(200.0, point.Y, 1e-12);
        }

        /// <summary>
        /// Verifies that boundary probability values (0 and 1) are accepted.
        /// </summary>
        [TestMethod]
        public void Constructor_BoundaryProbabilities_AreAccepted()
        {
            var p0 = new RMC_BestFit.FrequencyCurvePoint(0.0, 0.0, 0.0, 0.0, 0.0);
            var p1 = new RMC_BestFit.FrequencyCurvePoint(1.0, 0.0, 0.0, 0.0, 0.0);
            Assert.AreEqual(0.0, p0.Probability, 1e-12);
            Assert.AreEqual(1.0, p1.Probability, 1e-12);
        }
    }
}
