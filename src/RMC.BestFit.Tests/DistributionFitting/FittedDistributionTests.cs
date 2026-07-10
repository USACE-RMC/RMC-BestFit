using Numerics.Distributions;
using RMC.BestFit.Models;

namespace RMC.BestFit.Tests.DistributionFitting;

/// <summary>
/// Programmatic unit tests for the <c>FittedDistribution</c> result class —
/// the immutable record produced by <c>FittingAnalysis</c> for each distribution it tries.
/// </summary>
/// <remarks>
/// Covers construction, default values, ToXElement / FromXElement round-trip, the
/// ShowResults setter (the only mutable property and its PropertyChanged hook), and the
/// computed ToolTip string. Estimation tests live in <c>RMC.BestFit.Verification</c>.
/// </remarks>
[TestClass]
public class FittedDistributionTests
{
    #region Construction

    /// <summary>
    /// Default constructor with NaN diagnostics — the canonical state when a fit attempt
    /// has not yet been run for the given distribution.
    /// </summary>
    [TestMethod]
    public void Constructor_DefaultDiagnostics_AreNaN()
    {
        var dist = new Normal(100, 15);

        var fd = new FittedDistribution(dist);

        Assert.AreSame(dist, fd.Distribution);
        Assert.IsTrue(double.IsNaN(fd.AIC));
        Assert.IsTrue(double.IsNaN(fd.BIC));
        Assert.IsTrue(double.IsNaN(fd.RMSE));
        Assert.IsFalse(fd.FitSucceeded);
        Assert.IsFalse(fd.ShowResults);
        Assert.AreEqual(string.Empty, fd.ErrorMessage);
    }

    /// <summary>
    /// Constructor populates immutable diagnostics directly. AIC, BIC, RMSE, and
    /// FitSucceeded are init-only; only ShowResults is mutable post-construction.
    /// </summary>
    [TestMethod]
    public void Constructor_WithValues_PopulatesAllFields()
    {
        var dist = new Normal(100, 15);

        var fd = new FittedDistribution(dist, aic: 12.3, bic: 14.5, rmse: 2.1,
            fitSucceeded: true, showResults: true);

        Assert.AreEqual(12.3, fd.AIC);
        Assert.AreEqual(14.5, fd.BIC);
        Assert.AreEqual(2.1, fd.RMSE);
        Assert.IsTrue(fd.FitSucceeded);
        Assert.IsTrue(fd.ShowResults);
    }

    #endregion

    #region ShowResults setter

    /// <summary>
    /// ShowResults setter raises PropertyChanged when toggled. This drives the UI's
    /// "show this distribution on the plot" checkbox.
    /// </summary>
    [TestMethod]
    public void ShowResults_Setter_RaisesPropertyChange()
    {
        var fd = new FittedDistribution(new Normal(0, 1));
        bool fired = false;
        fd.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(FittedDistribution.ShowResults))
                fired = true;
        };

        fd.ShowResults = true;

        Assert.IsTrue(fd.ShowResults);
        Assert.IsTrue(fired);
    }

    /// <summary>
    /// ShowResults setter is idempotent — assigning the existing value must not fire
    /// PropertyChanged (so the UI doesn't redraw redundantly).
    /// </summary>
    [TestMethod]
    public void ShowResults_Setter_NoEventOnUnchangedValue()
    {
        var fd = new FittedDistribution(new Normal(0, 1));
        fd.ShowResults = true;
        bool fired = false;
        fd.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(FittedDistribution.ShowResults))
                fired = true;
        };

        fd.ShowResults = true;

        Assert.IsFalse(fired);
    }

    #endregion

    #region ToolTip

    /// <summary>
    /// ToolTip is a computed display string. With no distribution set, the contract is
    /// to produce the documented placeholder rather than crash.
    /// </summary>
    [TestMethod]
    public void ToolTip_NullDistribution_ReturnsPlaceholder()
    {
        // Disambiguate the overload — null! could resolve to either the (Distribution, ...)
        // or the (XElement) constructor. Cast forces selection of the Distribution overload.
        var fd = new FittedDistribution((Numerics.Distributions.UnivariateDistributionBase)null!);

        Assert.AreEqual("No distribution set", fd.ToolTip);
    }

    /// <summary>
    /// ToolTip with a fitted distribution must include the DisplayName and at least one
    /// parameter line. Pinning the structure protects the UI from silent regressions.
    /// </summary>
    [TestMethod]
    public void ToolTip_WithDistribution_IncludesDisplayNameAndParameters()
    {
        var dist = new Normal(100, 15);
        var fd = new FittedDistribution(dist);

        var tip = fd.ToolTip;

        Assert.IsTrue(tip.Contains(dist.DisplayName));
        // ParameterNames contains "Mean (μ)" and "Std Dev (σ)" in production; we just
        // check that at least one parameter row was appended.
        Assert.IsTrue(tip.Contains(Environment.NewLine),
            "ToolTip should append parameter lines on new lines.");
    }

    #endregion

    #region Serialization round-trip

    /// <summary>
    /// ToXElement / FromXElement must preserve diagnostics and the ShowResults flag.
    /// G17 invariant formatting is used internally to avoid culture-dependent precision loss.
    /// </summary>
    [TestMethod]
    public void XmlSerialization_RoundTrip_PreservesAllValues()
    {
        var original = new FittedDistribution(
            new Normal(100, 15),
            aic: 7.5,
            bic: 11.25,
            rmse: 0.789,
            fitSucceeded: true,
            showResults: true);

        var xml = original.ToXElement();
        var restored = new FittedDistribution(xml);

        Assert.AreEqual(original.AIC, restored.AIC, 1e-15);
        Assert.AreEqual(original.BIC, restored.BIC, 1e-15);
        Assert.AreEqual(original.RMSE, restored.RMSE, 1e-15);
        Assert.AreEqual(original.FitSucceeded, restored.FitSucceeded);
        Assert.AreEqual(original.ShowResults, restored.ShowResults);
        Assert.IsNotNull(restored.Distribution);
    }

    /// <summary>
    /// XElement round-trip with the default NaN diagnostics — ensures NaN is preserved
    /// (NaN.ToString round-trips on parse, but only with InvariantCulture).
    /// </summary>
    [TestMethod]
    public void XmlSerialization_NaNDiagnostics_RoundTrip()
    {
        var original = new FittedDistribution(new Normal(0, 1));

        var xml = original.ToXElement();
        var restored = new FittedDistribution(xml);

        Assert.IsTrue(double.IsNaN(restored.AIC));
        Assert.IsTrue(double.IsNaN(restored.BIC));
        Assert.IsTrue(double.IsNaN(restored.RMSE));
        Assert.IsFalse(restored.FitSucceeded);
        Assert.IsFalse(restored.ShowResults);
    }

    #endregion
}
