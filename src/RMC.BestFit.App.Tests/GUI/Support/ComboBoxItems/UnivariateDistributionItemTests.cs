using Microsoft.VisualStudio.TestTools.UnitTesting;
using Numerics.Distributions;

namespace RMC.BestFit.App.Tests.GUI.Support.ComboBoxItems
{
    /// <summary>
    /// Unit tests for <see cref="RMC_BestFit.UnivariateDistributionItem"/>.
    /// </summary>
    [TestClass]
    public class UnivariateDistributionItemTests
    {
        /// <summary>
        /// Verifies that the constructor stores DisplayName and Value for the Normal distribution.
        /// </summary>
        [TestMethod]
        public void Constructor_Normal_SetsProperties()
        {
            var item = new RMC_BestFit.UnivariateDistributionItem("Normal", UnivariateDistributionType.Normal);
            Assert.AreEqual("Normal", item.DisplayName);
            Assert.AreEqual(UnivariateDistributionType.Normal, item.Value);
        }

        /// <summary>
        /// Verifies that the constructor stores properties for Log-Pearson Type III.
        /// </summary>
        [TestMethod]
        public void Constructor_LogPearsonTypeIII_SetsProperties()
        {
            var item = new RMC_BestFit.UnivariateDistributionItem("Log-Pearson Type III", UnivariateDistributionType.LogPearsonTypeIII);
            Assert.AreEqual("Log-Pearson Type III", item.DisplayName);
            Assert.AreEqual(UnivariateDistributionType.LogPearsonTypeIII, item.Value);
        }

        /// <summary>
        /// Verifies that the constructor stores properties for GEV.
        /// </summary>
        [TestMethod]
        public void Constructor_GeneralizedExtremeValue_SetsProperties()
        {
            var item = new RMC_BestFit.UnivariateDistributionItem("Generalized Extreme Value", UnivariateDistributionType.GeneralizedExtremeValue);
            Assert.AreEqual("Generalized Extreme Value", item.DisplayName);
            Assert.AreEqual(UnivariateDistributionType.GeneralizedExtremeValue, item.Value);
        }

        /// <summary>
        /// Verifies that DisplayName and Value are mutable after construction.
        /// </summary>
        [TestMethod]
        public void Properties_AreMutable()
        {
            var item = new RMC_BestFit.UnivariateDistributionItem("Normal", UnivariateDistributionType.Normal);
            item.DisplayName = "Weibull";
            item.Value = UnivariateDistributionType.Weibull;
            Assert.AreEqual("Weibull", item.DisplayName);
            Assert.AreEqual(UnivariateDistributionType.Weibull, item.Value);
        }

        /// <summary>
        /// Verifies construction for all 15 supported distributions round-trips correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_AllSupportedDistributions_RoundTrip()
        {
            var pairs = new System.Collections.Generic.List<(string, UnivariateDistributionType)>
            {
                ("Exponential", UnivariateDistributionType.Exponential),
                ("Gamma", UnivariateDistributionType.GammaDistribution),
                ("GEV", UnivariateDistributionType.GeneralizedExtremeValue),
                ("GLogistic", UnivariateDistributionType.GeneralizedLogistic),
                ("GNormal", UnivariateDistributionType.GeneralizedNormal),
                ("GPareto", UnivariateDistributionType.GeneralizedPareto),
                ("Gumbel", UnivariateDistributionType.Gumbel),
                ("LnNormal", UnivariateDistributionType.LnNormal),
                ("LogNormal", UnivariateDistributionType.LogNormal),
                ("LP3", UnivariateDistributionType.LogPearsonTypeIII),
                ("Normal", UnivariateDistributionType.Normal),
                ("PT3", UnivariateDistributionType.PearsonTypeIII),
                ("Weibull", UnivariateDistributionType.Weibull),
            };
            foreach (var (name, type) in pairs)
            {
                var item = new RMC_BestFit.UnivariateDistributionItem(name, type);
                Assert.AreEqual(name, item.DisplayName, $"DisplayName mismatch for {type}");
                Assert.AreEqual(type, item.Value, $"Value mismatch for {type}");
            }
        }
    }
}
