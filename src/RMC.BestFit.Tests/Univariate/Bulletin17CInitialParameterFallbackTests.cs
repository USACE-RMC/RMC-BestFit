using Numerics.Distributions;
using RMC.BestFit.Models;
using BestFitDataFrame = RMC.BestFit.Models.DataFrame;
using BestFitThresholdData = RMC.BestFit.Models.ThresholdData;

namespace RMC.BestFit.Tests.Univariate;

/// <summary>
/// Pins the Bulletin 17C initial-parameter fallback (TR-087): when the censored-data (ROS)
/// initial-moment estimate is unavailable, construction keeps the constraint-based initial values
/// and records a validation warning instead of leaving the model without parameters.
/// </summary>
/// <remarks>
/// A data frame populated with collection notifications suppressed never receives plotting
/// positions (every complement stays 1.0); the ROS regression then fails for low outliers and
/// the empirical-distribution fallback fails for thresholds. Until 22 August 2026 that failure was
/// swallowed and the model reported zero parameters, so the analysis later threw an index error.
/// </remarks>
[TestClass]
public class Bulletin17CInitialParameterFallbackTests
{
    /// <summary>Twenty positive annual peaks.</summary>
    private static readonly double[] Flows =
    {
        1250, 980, 2100, 760, 1430, 890, 3050, 1120, 640, 1780,
        2460, 1010, 830, 1560, 2900, 710, 1340, 1950, 1190, 2700
    };

    /// <summary>
    /// Builds a frame with suppressed notifications so that plotting positions remain at their defaults.
    /// </summary>
    /// <param name="lowOutliers">Whether to flag the flows below 800 as low outliers.</param>
    /// <param name="threshold">Whether to add a 100-year historical perception threshold.</param>
    /// <returns>The data frame.</returns>
    private static BestFitDataFrame CreateFrameWithoutPlottingPositions(bool lowOutliers, bool threshold)
    {
        var frame = new BestFitDataFrame();
        frame.ExactSeries.SuppressCollectionChanged = true;
        frame.ThresholdSeries.SuppressCollectionChanged = true;
        for (int i = 0; i < Flows.Length; i++)
            frame.ExactSeries.Add(new ExactData(i + 1, Flows[i]));
        if (lowOutliers)
        {
            frame.LowOutlierThreshold = 800.0;
            frame.SetLowOutliersFromThreshold();
        }
        if (threshold)
            frame.ThresholdSeries.Add(new BestFitThresholdData(1 - 100, 0, 2500.0) { NumberAbove = 3 });
        frame.ExactSeries.SuppressCollectionChanged = false;
        frame.ThresholdSeries.SuppressCollectionChanged = false;
        if (threshold)
            frame.ProcessThresholdSeries();
        return frame;
    }

    /// <summary>
    /// Asserts that the model has three finite in-bounds parameters and carries the ROS fallback warning.
    /// </summary>
    /// <param name="model">The constructed model.</param>
    private static void AssertFallbackParametersAndWarning(Bulletin17CDistribution model)
    {
        Assert.AreEqual(3, model.NumberOfParameters, "The model must keep its three parameters.");
        foreach (var parameter in model.Parameters)
        {
            Assert.IsTrue(double.IsFinite(parameter.Value), $"{parameter.Name} must have a finite initial value.");
            Assert.IsTrue(parameter.Value >= parameter.LowerBound && parameter.Value <= parameter.UpperBound,
                $"{parameter.Name} must start inside its bounds.");
        }

        var (isValid, messages) = model.Validate();
        Assert.IsTrue(isValid, "The fallback is a warning, not an error: " + string.Join(" | ", messages));
        Assert.IsTrue(messages.Any(m => m.StartsWith("Warning:", StringComparison.Ordinal) && m.Contains("ROS", StringComparison.Ordinal)),
            "A ROS fallback warning must be recorded: " + string.Join(" | ", messages));
    }

    /// <summary>
    /// Pre-flagged low outliers without plotting positions: constraint initials kept, warning recorded.
    /// </summary>
    [TestMethod]
    public void Construction_PreFlaggedLowOutliersWithoutPlottingPositions_KeepsConstraintInitialsAndWarns()
    {
        var frame = CreateFrameWithoutPlottingPositions(lowOutliers: true, threshold: false);
        Assert.IsTrue(frame.ExactSeries.All(d => d.PlottingPositionComplement == 1.0), "Fixture precondition: plotting positions were not computed.");
        Assert.IsTrue(frame.NumberOfLowOutliers > 0, "Fixture precondition: low outliers flagged.");

        var model = new Bulletin17CDistribution(frame, UnivariateDistributionType.LogPearsonTypeIII);

        AssertFallbackParametersAndWarning(model);
    }

    /// <summary>
    /// A historical threshold without plotting positions: constraint initials kept, warning recorded.
    /// </summary>
    [TestMethod]
    public void Construction_ThresholdWithoutPlottingPositions_KeepsConstraintInitialsAndWarns()
    {
        var frame = CreateFrameWithoutPlottingPositions(lowOutliers: false, threshold: true);
        Assert.IsTrue(frame.ExactSeries.All(d => d.PlottingPositionComplement == 1.0), "Fixture precondition: plotting positions were not computed.");

        var model = new Bulletin17CDistribution(frame, UnivariateDistributionType.LogPearsonTypeIII);

        AssertFallbackParametersAndWarning(model);
    }

    /// <summary>
    /// Once plotting positions exist the censored-data initial estimate is used and no warning is recorded.
    /// </summary>
    [TestMethod]
    public void Construction_AfterPlottingPositions_UsesCensoredInitialsWithoutWarning()
    {
        var frame = CreateFrameWithoutPlottingPositions(lowOutliers: true, threshold: false);
        frame.CalculatePlottingPositions();
        Assert.IsTrue(frame.ExactSeries.Any(d => d.PlottingPositionComplement < 1.0), "Fixture precondition: plotting positions computed.");

        var model = new Bulletin17CDistribution(frame, UnivariateDistributionType.LogPearsonTypeIII);

        Assert.AreEqual(3, model.NumberOfParameters);
        Assert.IsTrue(model.Parameters.All(p => double.IsFinite(p.Value)));
        var (isValid, messages) = model.Validate();
        Assert.IsTrue(isValid, string.Join(" | ", messages));
        Assert.IsFalse(messages.Any(m => m.StartsWith("Warning:", StringComparison.Ordinal) && m.Contains("ROS", StringComparison.Ordinal)),
            "No ROS fallback warning once plotting positions exist: " + string.Join(" | ", messages));
    }

    /// <summary>
    /// Recomputing the defaults after plotting positions are available clears the earlier warning.
    /// </summary>
    [TestMethod]
    public void SetDefaultParameters_AfterPlottingPositions_ClearsTheWarning()
    {
        var frame = CreateFrameWithoutPlottingPositions(lowOutliers: true, threshold: false);
        var model = new Bulletin17CDistribution(frame, UnivariateDistributionType.LogPearsonTypeIII);
        Assert.IsTrue(model.Validate().ValidationMessages.Any(m => m.StartsWith("Warning:", StringComparison.Ordinal)), "Fixture precondition: warning recorded.");

        frame.CalculatePlottingPositions();
        model.SetDefaultParameters();

        Assert.AreEqual(3, model.NumberOfParameters);
        Assert.IsFalse(model.Validate().ValidationMessages.Any(m => m.StartsWith("Warning:", StringComparison.Ordinal) && m.Contains("ROS", StringComparison.Ordinal)));
    }
}
