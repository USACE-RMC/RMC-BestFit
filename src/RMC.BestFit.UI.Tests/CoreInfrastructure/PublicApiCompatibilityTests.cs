using System.IO;
using System.Reflection;
using RMC.BestFit.TestCommon;
using RMC.BestFit.UI;

namespace RMC.BestFit.UI.Tests.CoreInfrastructure;

/// <summary>
/// Protects the captured public and protected surface of the UI assembly.
/// </summary>
[TestClass]
public class PublicApiCompatibilityTests
{
    /// <summary>
    /// Verifies that the UI assembly matches its explicitly approved signature baseline.
    /// </summary>
    [TestMethod]
    public void PublicApi_MatchesCapturedBaseline()
    {
        Assembly testAssembly = typeof(PublicApiCompatibilityTests).Assembly;
        using Stream? baselineStream = testAssembly.GetManifestResourceStream(
            "RMC.BestFit.UI.Tests.CoreInfrastructure.PublicApiBaseline.txt");
        Assert.IsNotNull(baselineStream, "The embedded UI public API baseline is missing.");

        using var reader = new StreamReader(baselineStream);
        string expected = reader.ReadToEnd().Replace("\r\n", "\n", StringComparison.Ordinal);
        string actual = PublicApiContractSnapshot.Create(typeof(BestFitProject).Assembly);

        Assert.AreEqual(
            expected,
            actual,
            "The RMC.BestFit.UI public/protected API changed. Preserve the baseline unless the change has explicit compatibility approval.");
    }
}
