using Numerics.Distributions;
using RMC.BestFit.Models;
using BestFitDataFrame = RMC.BestFit.Models.DataFrame;

namespace RMC.BestFit.Tests.Univariate;

/// <summary>
/// Verifies that log-space distribution families accept datasets whose values are mostly below 1,
/// where the mean of the log-transformed data is negative.
/// </summary>
/// <remarks>
/// The Numerics constraint helpers for LogNormal and LogPearsonTypeIII formerly floored the
/// log-space mean's lower bound at machine epsilon and collapsed its upper bound toward
/// ceil(mean + 1), so a sub-unity sample (for example snow-water-equivalent depths) seeded an
/// initial value outside its own bounds and an invalid uniform prior, and the univariate analysis
/// reported "the inputs are invalid" the moment the distribution was selected. The bounds are now
/// symmetric about zero like Normal's, and SetDefaultParameters additionally clamps any
/// out-of-bounds initial to the bound midpoint as defense in depth. LnNormal is parameterized by
/// the real-space mean, which is legitimately positive-only, and is covered here to pin that the
/// sibling family was never affected.
/// </remarks>
[TestClass]
public class SubUnityDataValidationTests
{
    /// <summary>Twelve positive values with a negative log10 mean (about -0.4).</summary>
    private static readonly double[] SubUnityFlows =
    {
        0.12, 0.31, 0.45, 0.08, 0.90, 1.4, 0.25, 0.6, 0.5, 0.75, 0.2, 0.33
    };

    /// <summary>
    /// Builds a frame over the shared sub-unity record.
    /// </summary>
    /// <returns>The data frame.</returns>
    private static BestFitDataFrame CreateFrame()
    {
        var frame = new BestFitDataFrame();
        for (int i = 0; i < SubUnityFlows.Length; i++)
            frame.ExactSeries.Add(new ExactData(i + 1, SubUnityFlows[i]));
        return frame;
    }

    /// <summary>
    /// Asserts the model validates with finite in-bounds parameters.
    /// </summary>
    /// <param name="type">The distribution family under test.</param>
    private static void AssertValidModel(UnivariateDistributionType type)
    {
        var model = new UnivariateDistribution(CreateFrame(), type);

        foreach (var trend in model.TrendModels)
        {
            foreach (var parameter in trend.Parameters)
            {
                Assert.IsTrue(double.IsFinite(parameter.Value), $"{type}: {parameter.Name} must be finite.");
                Assert.IsTrue(parameter.Value >= parameter.LowerBound && parameter.Value <= parameter.UpperBound,
                    $"{type}: {parameter.Name} must start inside its bounds.");
            }
        }
        var (isValid, messages) = model.Validate();
        Assert.IsTrue(isValid, $"{type}: " + string.Join(" | ", messages));
    }

    /// <summary>
    /// LogNormal accepts a sub-unity dataset with a negative log10 mean.
    /// </summary>
    [TestMethod]
    public void LogNormal_SubUnityData_IsValid() => AssertValidModel(UnivariateDistributionType.LogNormal);

    /// <summary>
    /// Log-Pearson Type III accepts a sub-unity dataset with a negative log10 mean.
    /// </summary>
    [TestMethod]
    public void LogPearsonTypeIII_SubUnityData_IsValid() => AssertValidModel(UnivariateDistributionType.LogPearsonTypeIII);

    /// <summary>
    /// Ln-Normal, parameterized by the real-space mean, was never affected and stays valid.
    /// </summary>
    [TestMethod]
    public void LnNormal_SubUnityData_IsValid() => AssertValidModel(UnivariateDistributionType.LnNormal);
}
