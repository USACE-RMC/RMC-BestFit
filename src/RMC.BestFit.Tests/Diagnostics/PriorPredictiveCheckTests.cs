using Numerics.Distributions;
using RMC.BestFit.Diagnostics;
using RMC.BestFit.Models;

namespace RMC.BestFit.Tests.Diagnostics;

/// <summary>
/// Fast structural unit tests for the <see cref="PriorPredictiveCheck"/> class.
/// Replicate-generation tests live in <c>RMC.BestFit.Verification</c>.
/// </summary>
[TestClass]
public class PriorPredictiveCheckTests
{
    private static UnivariateDistribution MakeNormalModel()
    {
        var df = new DataFrame { ExactSeries = new ExactSeries(
            new double[] { 12500, 15300, 8900, 22100, 18700, 14200, 9800, 28500, 17400, 11600 }) };
        return new UnivariateDistribution(df, UnivariateDistributionType.Normal);
    }

    /// <summary>Verifies that constructor succeeds when with simulatable model.</summary>
    [TestMethod]
    public void Test_Constructor_WithSimulatableModel_Succeeds()
    {
        var model = MakeNormalModel();

        var check = new PriorPredictiveCheck(model);

        Assert.AreSame(model, check.Model);
    }

    /// <summary>Verifies that constructor throws when null model.</summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Test_Constructor_NullModel_Throws()
    {
        _ = new PriorPredictiveCheck(null!);
    }

    /// <summary>Verifies that defaults number of draws and seed.</summary>
    [TestMethod]
    public void Test_Defaults_NumberOfDraws_And_Seed()
    {
        var check = new PriorPredictiveCheck(MakeNormalModel());

        Assert.AreEqual(1000, check.NumberOfDraws);
        Assert.AreEqual(12345, check.Seed);
    }

    /// <summary>Verifies that number of draws setter roundtrips.</summary>
    [TestMethod]
    public void Test_NumberOfDraws_Setter_Roundtrips()
    {
        var check = new PriorPredictiveCheck(MakeNormalModel());

        check.NumberOfDraws = 500;

        Assert.AreEqual(500, check.NumberOfDraws);
    }

    /// <summary>Verifies that number of draws throws when less than one.</summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void Test_NumberOfDraws_LessThanOne_Throws()
    {
        var check = new PriorPredictiveCheck(MakeNormalModel());

        check.NumberOfDraws = 0;
    }

    /// <summary>Verifies that seed setter roundtrips.</summary>
    [TestMethod]
    public void Test_Seed_Setter_Roundtrips()
    {
        var check = new PriorPredictiveCheck(MakeNormalModel());

        check.Seed = 99;

        Assert.AreEqual(99, check.Seed);
    }
}
