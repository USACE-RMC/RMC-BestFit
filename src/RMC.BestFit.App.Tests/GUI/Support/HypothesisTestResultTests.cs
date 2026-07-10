using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RMC.BestFit.App.Tests.GUI.Support
{
    /// <summary>
    /// Unit tests for <see cref="RMC_BestFit.HypothesisTestResult"/>.
    /// </summary>
    [TestClass]
    public class HypothesisTestResultTests
    {
        /// <summary>
        /// Verifies that all four properties are stored correctly when all arguments are provided.
        /// </summary>
        [TestMethod]
        public void Constructor_AllArguments_SetsAllProperties()
        {
            var result = new RMC_BestFit.HypothesisTestResult(
                "Kolmogorov-Smirnov",
                "0.045",
                "0.05",
                "Fail to reject H0");
            Assert.AreEqual("Kolmogorov-Smirnov", result.Name);
            Assert.AreEqual("0.045", result.PValue);
            Assert.AreEqual("0.05", result.Significance);
            Assert.AreEqual("Fail to reject H0", result.Inference);
        }

        /// <summary>
        /// Verifies that Significance defaults to empty string when omitted.
        /// </summary>
        [TestMethod]
        public void Constructor_NoSignificance_DefaultsToEmpty()
        {
            var result = new RMC_BestFit.HypothesisTestResult("Anderson-Darling", "0.12");
            Assert.AreEqual(string.Empty, result.Significance);
        }

        /// <summary>
        /// Verifies that Inference defaults to empty string when omitted.
        /// </summary>
        [TestMethod]
        public void Constructor_NoInference_DefaultsToEmpty()
        {
            var result = new RMC_BestFit.HypothesisTestResult("Chi-Square", "0.22");
            Assert.AreEqual(string.Empty, result.Inference);
        }

        /// <summary>
        /// Verifies that Name and PValue store the minimum two-argument construction correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_TwoArguments_SetsNameAndPValue()
        {
            var result = new RMC_BestFit.HypothesisTestResult("Chi-Square", "0.12");
            Assert.AreEqual("Chi-Square", result.Name);
            Assert.AreEqual("0.12", result.PValue);
        }

        /// <summary>
        /// Verifies that all properties are mutable after construction.
        /// </summary>
        [TestMethod]
        public void Properties_AreMutable()
        {
            var result = new RMC_BestFit.HypothesisTestResult("KS", "0.1");
            result.Name = "AD";
            result.PValue = "0.2";
            result.Significance = "0.05";
            result.Inference = "Reject H0";
            Assert.AreEqual("AD", result.Name);
            Assert.AreEqual("0.2", result.PValue);
            Assert.AreEqual("0.05", result.Significance);
            Assert.AreEqual("Reject H0", result.Inference);
        }
    }
}
