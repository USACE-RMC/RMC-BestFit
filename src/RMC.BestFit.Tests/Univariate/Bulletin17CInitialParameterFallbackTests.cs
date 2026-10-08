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
/// The degenerate state these tests construct is a frame whose low-outlier count is set while its
/// plotting positions were never computed (every complement stays 1.0) — the state a legacy
/// project file deserializes into, since the XElement constructor restores NumberOfLowOutliers and
/// preserves stored positions verbatim. The ROS regression then fails for low outliers and the
/// empirical-distribution fallback fails for thresholds. Until 22 August 2026 that failure was
/// swallowed and the model reported zero parameters, so the analysis later threw an index error.
/// The low-outlier setters now recompute plotting positions themselves, so the fixture resets the
/// positions after calling the setter to reproduce the never-computed state directly.
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
            // The setter now computes plotting positions on return. Reset them to their defaults to
            // reproduce the degenerate state the TR-087 fallback exists for: a flagged low-outlier
            // count without computed positions, as a legacy project file deserializes. The write is
            // side-effect-free here — the "PlottingPosition" property name is filtered by the frame's
            // data-edit handlers, and these items were added under suppressed notifications.
            foreach (var d in frame.ExactSeries)
                d.PlottingPosition = 0d;
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
    /// A headless caller who runs a low-outlier setter gets censored-data initials with no manual
    /// plotting-position call and no fallback warning.
    /// </summary>
    /// <remarks>
    /// This is the contract the low-outlier setters now provide: they refresh the Hirsch-Stedinger
    /// positions themselves, so the censored-data (ROS) initial estimate succeeds where it formerly
    /// regressed on infinite normal scores and fell back to constraint initials with a warning.
    /// Mirrors the corehydro report's headless reproduction, which needed a manual
    /// CalculatePlottingPositions() call between the setter and the fit to succeed.
    /// </remarks>
    [TestMethod]
    public void Construction_AfterLowOutlierSetter_UsesCensoredInitialsWithoutManualPositionCall()
    {
        var frame = new BestFitDataFrame();
        frame.ExactSeries.SuppressCollectionChanged = true;
        var flows = new double[]
        {
            9600, 12400, 8100, 15300, 7000, 11800, 9950, 13600, 8800, 10400,
            14700, 7600, 12100, 9100, 16200, 55, 90
        };
        for (int i = 0; i < flows.Length; i++)
            frame.ExactSeries.Add(new ExactData(i + 1, flows[i]));

        frame.SetLowOutliersFromMGBT();
        Assert.IsTrue(frame.NumberOfLowOutliers >= 1, "Fixture precondition: the low floods are flagged.");
        Assert.IsTrue(frame.ExactSeries.Any(d => d.PlottingPositionComplement < 1.0),
            "The setter must leave plotting positions computed.");

        var rosMoments = frame.GetNonparametricMomentsROS(useLog10Values: true);
        Assert.IsNotNull(rosMoments);
        Assert.IsTrue(rosMoments.All(double.IsFinite), "The ROS moments must be finite from computed positions.");

        var model = new Bulletin17CDistribution(frame, UnivariateDistributionType.LogPearsonTypeIII);

        Assert.AreEqual(3, model.NumberOfParameters);
        foreach (var parameter in model.Parameters)
        {
            Assert.IsTrue(double.IsFinite(parameter.Value));
            Assert.IsTrue(parameter.Value >= parameter.LowerBound && parameter.Value <= parameter.UpperBound);
        }
        var (isValid, messages) = model.Validate();
        Assert.IsTrue(isValid, string.Join(" | ", messages));
        Assert.IsFalse(messages.Any(m => m.StartsWith("Warning:", StringComparison.Ordinal) && m.Contains("ROS", StringComparison.Ordinal)),
            "No ROS fallback warning: the setter left usable positions. " + string.Join(" | ", messages));
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
