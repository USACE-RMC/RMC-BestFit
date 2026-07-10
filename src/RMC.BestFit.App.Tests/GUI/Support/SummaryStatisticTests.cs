using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace RMC.BestFit.App.Tests.GUI.Support
{
    /// <summary>
    /// Unit tests for <see cref="RMC_BestFit.SummaryStatistic"/>.
    /// </summary>
    [TestClass]
    public class SummaryStatisticTests
    {
        /// <summary>
        /// Verifies that all properties are stored correctly when all arguments are provided.
        /// </summary>
        [TestMethod]
        public void Constructor_AllArguments_SetsAllProperties()
        {
            var stat = new RMC_BestFit.SummaryStatistic("Mean", 42.5, 43.0, "**");
            Assert.AreEqual("Mean", stat.Name);
            Assert.AreEqual(42.5, stat.Value, 1e-12);
            Assert.AreEqual(43.0, stat.Value2, 1e-12);
            Assert.AreEqual("**", stat.Significance);
        }

        /// <summary>
        /// Verifies that Value2 defaults to NaN when not provided.
        /// </summary>
        [TestMethod]
        public void Constructor_NoValue2_DefaultsToNaN()
        {
            var stat = new RMC_BestFit.SummaryStatistic("Mean", 10.0);
            Assert.IsTrue(double.IsNaN(stat.Value2));
        }

        /// <summary>
        /// Verifies that Significance defaults to empty string when not provided.
        /// </summary>
        [TestMethod]
        public void Constructor_NoSignificance_DefaultsToEmpty()
        {
            var stat = new RMC_BestFit.SummaryStatistic("Mean", 10.0);
            Assert.AreEqual(string.Empty, stat.Significance);
        }

        /// <summary>
        /// Verifies that the two-argument constructor sets Name and Value correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_TwoArguments_SetsNameAndValue()
        {
            var stat = new RMC_BestFit.SummaryStatistic("Median", 55.5);
            Assert.AreEqual("Median", stat.Name);
            Assert.AreEqual(55.5, stat.Value, 1e-12);
        }

        /// <summary>
        /// Verifies that all properties are mutable after construction.
        /// </summary>
        [TestMethod]
        public void Properties_AreMutable()
        {
            var stat = new RMC_BestFit.SummaryStatistic("Mean", 10.0);
            stat.Name = "Std Dev";
            stat.Value = 2.5;
            stat.Value2 = 3.0;
            stat.Significance = "*";
            Assert.AreEqual("Std Dev", stat.Name);
            Assert.AreEqual(2.5, stat.Value, 1e-12);
            Assert.AreEqual(3.0, stat.Value2, 1e-12);
            Assert.AreEqual("*", stat.Significance);
        }

        /// <summary>
        /// Verifies that a zero value is accepted.
        /// </summary>
        [TestMethod]
        public void Constructor_ZeroValue_IsAccepted()
        {
            var stat = new RMC_BestFit.SummaryStatistic("Min", 0.0);
            Assert.AreEqual(0.0, stat.Value, 1e-12);
        }

        /// <summary>
        /// Verifies that negative values are accepted.
        /// </summary>
        [TestMethod]
        public void Constructor_NegativeValue_IsAccepted()
        {
            var stat = new RMC_BestFit.SummaryStatistic("Skewness", -0.5);
            Assert.AreEqual(-0.5, stat.Value, 1e-12);
        }
    }
}
