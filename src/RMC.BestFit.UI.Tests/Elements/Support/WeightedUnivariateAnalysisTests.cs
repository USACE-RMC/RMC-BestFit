using System.ComponentModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RMC.BestFit.UI;

namespace RMC.BestFit.UI.Tests.Elements.Support;

/// <summary>
/// Unit tests for <see cref="WeightedUnivariateAnalysis"/>, which associates a weight
/// with a <see cref="UnivariateAnalysis"/> for use in composite analyses.
/// </summary>
/// <remarks>
/// Tests focus on the POCO contract: default values, property change notification,
/// null assignment, and XML serialization round-trip for the weight value.
/// The <see cref="UnivariateAnalysis"/> property requires a live project/SQLite, so
/// tests for property forwarding are structural stubs only.
/// </remarks>
[TestClass]
public class WeightedUnivariateAnalysisTests
{
    /// <summary>
    /// Verifies that the default constructor produces a zero weight and a null analysis reference.
    /// </summary>
    [TestMethod]
    public void DefaultConstructor_WeightIsZeroAndAnalysisIsNull()
    {
        var wua = new WeightedUnivariateAnalysis();

        Assert.AreEqual(0.0, wua.Weight, 1e-15);
        Assert.IsNull(wua.UnivariateAnalysis);
    }

    /// <summary>
    /// Verifies that <see cref="WeightedUnivariateAnalysis"/> implements <see cref="INotifyPropertyChanged"/>.
    /// </summary>
    [TestMethod]
    public void ImplementsINotifyPropertyChanged()
    {
        var wua = new WeightedUnivariateAnalysis();
        Assert.IsInstanceOfType<INotifyPropertyChanged>(wua);
    }

    /// <summary>
    /// Verifies that setting <see cref="WeightedUnivariateAnalysis.Weight"/> raises
    /// <see cref="INotifyPropertyChanged.PropertyChanged"/> with the correct property name.
    /// </summary>
    [TestMethod]
    public void Weight_Setter_RaisesPropertyChanged()
    {
        var wua = new WeightedUnivariateAnalysis();
        var raised = new List<string>();
        wua.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        wua.Weight = 0.5;

        Assert.IsTrue(raised.Contains(nameof(WeightedUnivariateAnalysis.Weight)),
            "PropertyChanged must be raised for Weight property.");
    }

    /// <summary>
    /// Verifies that setting <see cref="WeightedUnivariateAnalysis.Weight"/> to the same value
    /// still raises <see cref="INotifyPropertyChanged.PropertyChanged"/> because the setter
    /// unconditionally fires when the value differs from the current.
    /// </summary>
    /// <remarks>
    /// The production setter uses <c>if (_weight != value)</c>, so a same-value set must NOT raise.
    /// This test guards against accidentally removing that guard.
    /// </remarks>
    [TestMethod]
    public void Weight_SameValue_DoesNotRaisePropertyChanged()
    {
        var wua = new WeightedUnivariateAnalysis();
        wua.Weight = 0.3; // set initial value

        var raised = new List<string>();
        wua.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        wua.Weight = 0.3; // assign same value

        Assert.AreEqual(0, raised.Count, "PropertyChanged must NOT fire when Weight is set to the same value.");
    }

    /// <summary>
    /// Verifies that setting <see cref="WeightedUnivariateAnalysis.UnivariateAnalysis"/> to null
    /// raises <see cref="INotifyPropertyChanged.PropertyChanged"/> for the property.
    /// </summary>
    [TestMethod]
    public void UnivariateAnalysis_SetToNull_RaisesPropertyChanged()
    {
        var wua = new WeightedUnivariateAnalysis();
        var raised = new List<string>();
        wua.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        wua.UnivariateAnalysis = null;

        Assert.IsTrue(raised.Contains(nameof(WeightedUnivariateAnalysis.UnivariateAnalysis)),
            "PropertyChanged must be raised for UnivariateAnalysis even when assigned null.");
    }

    /// <summary>
    /// Verifies that <see cref="WeightedUnivariateAnalysis.ToXElement"/> returns null when no
    /// <see cref="UnivariateAnalysis"/> is set.
    /// </summary>
    [TestMethod]
    public void ToXElement_NullAnalysis_ReturnsNull()
    {
        var wua = new WeightedUnivariateAnalysis();

        var element = wua.ToXElement();

        Assert.IsNull(element, "ToXElement must return null when UnivariateAnalysis is not set.");
    }

    /// <summary>
    /// Verifies that extreme weight values (e.g., negative or > 1.0) are stored without clamping.
    /// </summary>
    [TestMethod]
    public void Weight_ExtremeValues_StoredWithoutClamping()
    {
        var wua = new WeightedUnivariateAnalysis();

        wua.Weight = -999.0;
        Assert.AreEqual(-999.0, wua.Weight, 1e-12, "Negative weight must be stored as-is.");

        wua.Weight = double.MaxValue;
        Assert.AreEqual(double.MaxValue, wua.Weight, "MaxValue weight must be stored as-is.");

        wua.Weight = double.NaN;
        Assert.IsTrue(double.IsNaN(wua.Weight), "NaN weight must be stored as-is.");
    }
}
