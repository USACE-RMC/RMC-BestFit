using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.Reflection;
using BestFitApplication = RMC_BestFit.App;

namespace RMC.BestFit.App.Tests
{
    /// <summary>
    /// Verifies the application release metadata and updater configuration remain synchronized.
    /// </summary>
    [TestClass]
    public class ReleaseMetadataTests
    {
        /// <summary>
        /// Verifies the shell displays the v2.0.0 product version and release month.
        /// </summary>
        [TestMethod]
        public void ProductMetadata_UsesFinalReleaseValues()
        {
            Assert.AreEqual("2.0.0", BestFitApplication.ProductVersion);
            Assert.AreEqual("July 2026", BestFitApplication.ProductVersionDate);
        }

        /// <summary>
        /// Verifies update checks use the assembly informational version and require checksums.
        /// </summary>
        [TestMethod]
        public void UpdateOptions_UseFinalReleaseAndRequireChecksum()
        {
            var options = BestFitApplication.CreateUpdateOptions();

            Assert.AreEqual("2.0.0", options.CurrentVersion.ToString());
            Assert.IsTrue(options.RequireSha256Checksum);
            Assert.AreEqual("USACE-RMC", options.GitHubOwner);
            Assert.AreEqual("RMC-BestFit", options.GitHubRepo);
            Assert.AreEqual("RMC-BestFit.*.zip", options.AssetNamePattern);
            Assert.IsFalse(options.IncludePreReleases);
        }

        /// <summary>
        /// Verifies all application assembly version attributes match the release convention.
        /// </summary>
        [TestMethod]
        public void AppAssemblyMetadata_UsesFinalReleaseValues()
        {
            Assembly assembly = typeof(BestFitApplication).Assembly;
            string informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
            string fileVersion = FileVersionInfo.GetVersionInfo(assembly.Location).FileVersion!;

            Assert.AreEqual("2.0.0", informationalVersion);
            Assert.AreEqual(new Version(2, 0, 0, 0), assembly.GetName().Version);
            Assert.AreEqual("2.0.0.0", fileVersion);
        }
    }
}
