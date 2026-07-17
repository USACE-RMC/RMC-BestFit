using Microsoft.VisualStudio.TestTools.UnitTesting;
using RMC.BestFit.Models.TrendFunctions.Support;

namespace RMC.BestFit.App.Tests.GUI.Support.ComboBoxItems
{
    /// <summary>
    /// Unit tests for <see cref="RMC_BestFit.TrendModelItem"/>.
    /// </summary>
    [TestClass]
    public class TrendModelItemTests
    {
        /// <summary>
        /// Verifies that the Constant trend model stores all properties correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_Constant_SetsProperties()
        {
            var item = new RMC_BestFit.TrendModelItem("Constant", TrendModelType.Constant);
            Assert.AreEqual("Constant", item.DisplayName);
            Assert.AreEqual(TrendModelType.Constant, item.Value);
        }

        /// <summary>
        /// Verifies that the Linear trend model stores all properties correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_Linear_SetsProperties()
        {
            var item = new RMC_BestFit.TrendModelItem("Linear", TrendModelType.Linear);
            Assert.AreEqual("Linear", item.DisplayName);
            Assert.AreEqual(TrendModelType.Linear, item.Value);
        }

        /// <summary>
        /// Verifies that the Quadratic trend model stores all properties correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_Quadratic_SetsProperties()
        {
            var item = new RMC_BestFit.TrendModelItem("Quadratic", TrendModelType.Quadratic);
            Assert.AreEqual("Quadratic", item.DisplayName);
            Assert.AreEqual(TrendModelType.Quadratic, item.Value);
        }

        /// <summary>
        /// Verifies that DisplayName and Value are mutable after construction.
        /// </summary>
        [TestMethod]
        public void Properties_AreMutable()
        {
            var item = new RMC_BestFit.TrendModelItem("Constant", TrendModelType.Constant);
            item.DisplayName = "Exponential";
            item.Value = TrendModelType.Exponential;
            Assert.AreEqual("Exponential", item.DisplayName);
            Assert.AreEqual(TrendModelType.Exponential, item.Value);
        }

        /// <summary>
        /// Verifies round-trip construction for all named trend model types used in the UI.
        /// </summary>
        [TestMethod]
        public void Constructor_AllTrendModelTypes_RoundTrip()
        {
            var pairs = new System.Collections.Generic.List<(string, TrendModelType)>
            {
                ("Constant", TrendModelType.Constant),
                ("Cubic", TrendModelType.Cubic),
                ("Exponential", TrendModelType.Exponential),
                ("Linear", TrendModelType.Linear),
                ("Logistic", TrendModelType.Logistic),
                ("Power", TrendModelType.Power),
                ("Quadratic", TrendModelType.Quadratic),
                ("Sinusoidal", TrendModelType.Sinusoidal),
                ("Step Function", TrendModelType.StepFunction),
            };
            foreach (var (name, type) in pairs)
            {
                var item = new RMC_BestFit.TrendModelItem(name, type);
                Assert.AreEqual(name, item.DisplayName, $"DisplayName mismatch for {type}");
                Assert.AreEqual(type, item.Value, $"Value mismatch for {type}");
            }
        }
    }
}
