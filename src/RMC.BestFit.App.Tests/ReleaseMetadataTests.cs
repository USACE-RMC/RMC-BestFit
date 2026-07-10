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
        /// Verifies the shell displays the beta.5 product version and release month.
        /// </summary>
        [TestMethod]
        public void ProductMetadata_UsesBeta5Values()
        {
            Assert.AreEqual("2.0-beta.5", BestFitApplication.ProductVersion);
            Assert.AreEqual("July 2026", BestFitApplication.ProductVersionDate);
        }

        /// <summary>
        /// Verifies update checks use the assembly informational version and require checksums.
        /// </summary>
        [TestMethod]
        public void UpdateOptions_UseBeta5AndRequireChecksum()
        {
            var options = BestFitApplication.CreateUpdateOptions();

            Assert.AreEqual("2.0.0-beta.5", options.CurrentVersion.ToString());
            Assert.IsTrue(options.RequireSha256Checksum);
            Assert.AreEqual("USACE-RMC", options.GitHubOwner);
            Assert.AreEqual("RMC-BestFit", options.GitHubRepo);
            Assert.AreEqual("RMC-BestFit.*.zip", options.AssetNamePattern);
        }

        /// <summary>
        /// Verifies all application assembly version attributes match the release convention.
        /// </summary>
        [TestMethod]
        public void AppAssemblyMetadata_UsesBeta5Values()
        {
            Assembly assembly = typeof(BestFitApplication).Assembly;
            string informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
            string fileVersion = FileVersionInfo.GetVersionInfo(assembly.Location).FileVersion!;

            Assert.AreEqual("2.0.0-beta.5", informationalVersion);
            Assert.AreEqual(new Version(2, 0, 0, 0), assembly.GetName().Version);
            Assert.AreEqual("2.0.0.0", fileVersion);
        }
    }
}
