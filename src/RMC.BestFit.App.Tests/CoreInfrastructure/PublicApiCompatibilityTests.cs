using System;
using System.IO;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RMC.BestFit.TestCommon;

namespace RMC.BestFit.App.Tests.CoreInfrastructure
{
    /// <summary>
    /// Protects the captured public and protected surface of the App assembly.
    /// </summary>
    [TestClass]
    public class PublicApiCompatibilityTests
    {
        /// <summary>
        /// Verifies that the App assembly matches its explicitly approved signature baseline.
        /// </summary>
        [TestMethod]
        public void PublicApi_MatchesCapturedBaseline()
        {
            Assembly testAssembly = typeof(PublicApiCompatibilityTests).Assembly;
            using Stream baselineStream = testAssembly.GetManifestResourceStream(
                "RMC.BestFit.App.Tests.CoreInfrastructure.PublicApiBaseline.txt");
            Assert.IsNotNull(baselineStream, "The embedded App public API baseline is missing.");

            using var reader = new StreamReader(baselineStream);
            string expected = reader.ReadToEnd().Replace("\r\n", "\n", StringComparison.Ordinal);
            string actual = PublicApiContractSnapshot.Create(typeof(RMC_BestFit.TransformTypeItem).Assembly);

            Assert.AreEqual(
                expected,
                actual,
                "The RMC.BestFit.App public/protected API changed. Preserve the baseline unless the change has explicit compatibility approval.");
        }
    }
}
