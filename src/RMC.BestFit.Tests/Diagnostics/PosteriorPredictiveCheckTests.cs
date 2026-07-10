using Numerics.Distributions;
using Numerics.Mathematics.Optimization;
using RMC.BestFit.Diagnostics;
using RMC.BestFit.Models;
using BestFitDataFrame = RMC.BestFit.Models.DataFrame;

namespace RMC.BestFit.Tests.Diagnostics;

/// <summary>
/// Fast structural unit tests for the <c>PosteriorPredictiveCheck</c> class.
/// Replicate-generation and p-value tests live in <c>RMC.BestFit.Verification</c>.
/// </summary>
[TestClass]
public class PosteriorPredictiveCheckTests
{
    /// <summary>
    /// Creates normal Model.
    /// </summary>
    /// <returns>The created test object.</returns>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static UnivariateDistribution MakeNormalModel()
    {
        var df = new BestFitDataFrame { ExactSeries = new ExactSeries(
            new double[] { 12500, 15300, 8900, 22100, 18700, 14200, 9800, 28500, 17400, 11600 }) };
        return new UnivariateDistribution(df, UnivariateDistributionType.Normal);
    }

    /// <summary>
    /// Creates posterior Samples.
    /// </summary>
    /// <param name="count">The number of values to create.</param>
    /// <returns>The created test object.</returns>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static IList<ParameterSet> MakePosteriorSamples(int count)
    {
        var samples = new List<ParameterSet>();
        for (int i = 0; i < count; i++)
        {
            samples.Add(new ParameterSet([100.0 + 0.01 * i, 15.0 + 0.005 * i], 0.0));
        }
        return samples;
    }

    /// <summary>Verifies that constructor succeeds when parameter set list.</summary>
    [TestMethod]
    public void Test_Constructor_ParameterSetList_Succeeds()
    {
        var model = MakeNormalModel();
        var samples = MakePosteriorSamples(50);
        var observed = new double[] { 12500, 15300, 8900 };

        var check = new PosteriorPredictiveCheck(model, samples, observed);

        Assert.AreSame(model, check.Model);
    }

    /// <summary>Verifies that constructor throws when null model.</summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Test_Constructor_NullModel_Throws()
    {
        _ = new PosteriorPredictiveCheck(null!, MakePosteriorSamples(10), new double[] { 1, 2, 3 });
    }

    /// <summary>Verifies that constructor throws when null samples.</summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Test_Constructor_NullSamples_Throws()
    {
        _ = new PosteriorPredictiveCheck(MakeNormalModel(), (IList<ParameterSet>)null!, new double[] { 1, 2, 3 });
    }

    /// <summary>Verifies that constructor throws when null observed data.</summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Test_Constructor_NullObservedData_Throws()
    {
        _ = new PosteriorPredictiveCheck(MakeNormalModel(), MakePosteriorSamples(10), null!);
    }

    /// <summary>Verifies that constructor throws when empty samples.</summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void Test_Constructor_EmptySamples_Throws()
    {
        _ = new PosteriorPredictiveCheck(MakeNormalModel(), new List<ParameterSet>(), new double[] { 1, 2, 3 });
    }

    /// <summary>Verifies that constructor throws when empty observed.</summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void Test_Constructor_EmptyObserved_Throws()
    {
        _ = new PosteriorPredictiveCheck(MakeNormalModel(), MakePosteriorSamples(10), Array.Empty<double>());
    }
}
