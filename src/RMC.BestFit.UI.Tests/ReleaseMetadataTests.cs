using System;
using System.Diagnostics;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RMC.BestFit.UI;

namespace RMC.BestFit.UI.Tests;

/// <summary>
/// Verifies persisted project and UI assembly release metadata remain synchronized.
/// </summary>
[TestClass]
public class ReleaseMetadataTests
{
    /// <summary>
    /// Verifies newly saved projects identify the v2.0.0 serialized format version.
    /// </summary>
    [TestMethod]
    public void SoftwareVersion_UsesFinalReleaseSerializedValue()
    {
        Assert.AreEqual("2.0.0", BestFitProject.GetInstance().SoftwareVersion);
    }

    /// <summary>
    /// Verifies the UI assembly carries v2.0.0 informational and stable numeric versions.
    /// </summary>
    [TestMethod]
    public void UiAssemblyMetadata_UsesFinalReleaseValues()
    {
        Assembly assembly = typeof(BestFitProject).Assembly;
        string informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
        string fileVersion = FileVersionInfo.GetVersionInfo(assembly.Location).FileVersion!;

        Assert.AreEqual("2.0.0", informationalVersion);
        Assert.AreEqual(new Version(2, 0, 0, 0), assembly.GetName().Version);
        Assert.AreEqual("2.0.0.0", fileVersion);
    }
}
