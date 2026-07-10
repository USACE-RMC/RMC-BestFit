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
    /// Verifies newly saved projects identify the beta.5 serialized format version.
    /// </summary>
    [TestMethod]
    public void SoftwareVersion_UsesBeta5SerializedValue()
    {
        Assert.AreEqual("2.0 Beta-5", BestFitProject.GetInstance().SoftwareVersion);
    }

    /// <summary>
    /// Verifies the UI assembly carries beta.5 informational and stable numeric versions.
    /// </summary>
    [TestMethod]
    public void UiAssemblyMetadata_UsesBeta5Values()
    {
        Assembly assembly = typeof(BestFitProject).Assembly;
        string informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
        string fileVersion = FileVersionInfo.GetVersionInfo(assembly.Location).FileVersion!;

        Assert.AreEqual("2.0.0-beta.5", informationalVersion);
        Assert.AreEqual(new Version(2, 0, 0, 0), assembly.GetName().Version);
        Assert.AreEqual("2.0.0.0", fileVersion);
    }
}
