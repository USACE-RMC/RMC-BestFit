using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RMC.BestFit.App.Tests.GUI.Support
{
    /// <summary>
    /// Unit tests for <see cref="RMC_BestFit.FittedDistributionStatistic"/>.
    /// </summary>
    [TestClass]
    public class FittedDistributionStatisticTests
    {
        /// <summary>
        /// Verifies that the constructor stores Name correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_SetsName()
        {
            var values = new double[] { 1.0, 2.0 };
            var tips = new string[] { "tip1", "tip2" };
            var stat = new RMC_BestFit.FittedDistributionStatistic("AIC", values, tips);
            Assert.AreEqual("AIC", stat.Name);
        }

        /// <summary>
        /// Verifies that the constructor stores the Value array by reference.
        /// </summary>
        [TestMethod]
        public void Constructor_SetsValues()
        {
            var values = new double[] { 10.5, 20.3, -5.0 };
            var tips = new string[] { "a", "b", "c" };
            var stat = new RMC_BestFit.FittedDistributionStatistic("Parameters", values, tips);
            Assert.AreEqual(3, stat.Value.Length);
            Assert.AreEqual(10.5, stat.Value[0], 1e-12);
            Assert.AreEqual(20.3, stat.Value[1], 1e-12);
            Assert.AreEqual(-5.0, stat.Value[2], 1e-12);
        }

        /// <summary>
        /// Verifies that the constructor stores the ToolTip array by reference.
        /// </summary>
        [TestMethod]
        public void Constructor_SetsToolTips()
        {
            var values = new double[] { 1.0 };
            var tips = new string[] { "Location parameter" };
            var stat = new RMC_BestFit.FittedDistributionStatistic("mu", values, tips);
            Assert.AreEqual(1, stat.ToolTip.Length);
            Assert.AreEqual("Location parameter", stat.ToolTip[0]);
        }

        /// <summary>
        /// Verifies that a single-element statistic is created correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_SingleValue_IsAccepted()
        {
            var stat = new RMC_BestFit.FittedDistributionStatistic("LogL", new double[] { -123.4 }, new string[] { "Log-likelihood" });
            Assert.AreEqual(-123.4, stat.Value[0], 1e-12);
        }

        /// <summary>
        /// Verifies that an empty value array is accepted without error.
        /// </summary>
        [TestMethod]
        public void Constructor_EmptyArrays_AreAccepted()
        {
            var stat = new RMC_BestFit.FittedDistributionStatistic("Empty", new double[0], new string[0]);
            Assert.AreEqual(0, stat.Value.Length);
            Assert.AreEqual(0, stat.ToolTip.Length);
        }

        /// <summary>
        /// Verifies that Name, Value, and ToolTip are read-only properties (their constructor values persist).
        /// </summary>
        [TestMethod]
        public void Properties_RetainConstructorValues()
        {
            var values = new double[] { 5.5 };
            var tips = new string[] { "sigma" };
            var stat = new RMC_BestFit.FittedDistributionStatistic("Scale", values, tips);
            Assert.AreEqual("Scale", stat.Name);
            Assert.AreSame(values, stat.Value);
            Assert.AreSame(tips, stat.ToolTip);
        }
    }
}
