using System.Reflection;
using RMC.BestFit.Models;

namespace RMC.BestFit.Tests.CoreInfrastructure;

/// <summary>
/// Protects the captured public surface of the production model library.
/// </summary>
[TestClass]
public class PublicApiCompatibilityTests
{
    /// <summary>
    /// Verifies that no public signature or enum value changed without deliberate baseline review.
    /// </summary>
    [TestMethod]
    public void PublicApi_MatchesCapturedBaseline()
    {
        Assembly testAssembly = typeof(PublicApiCompatibilityTests).Assembly;
        using Stream? baselineStream = testAssembly.GetManifestResourceStream("RMC.BestFit.Tests.CoreInfrastructure.PublicApiBaseline.txt");
        Assert.IsNotNull(baselineStream, "The embedded public API baseline is missing.");

        using var reader = new StreamReader(baselineStream);
        string expected = reader.ReadToEnd().Replace("\r\n", "\n", StringComparison.Ordinal);
        string actual = PublicApiSnapshot.Create(typeof(UnivariateDistribution).Assembly);

        Assert.AreEqual(
            expected,
            actual,
            "The RMC.BestFit public API changed. Review binary/source/serialization compatibility and update the baseline only after explicit approval.");
    }
}