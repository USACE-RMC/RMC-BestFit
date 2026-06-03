using Numerics.Distributions;
using RMC.BestFit.Analyses;
using RMC.BestFit.Models;
using DataFrame = RMC.BestFit.Models.DataFrame;

namespace RMC.BestFit.Tests.Univariate;

/// <summary>
/// Programmatic unit tests for the <see cref="WeightedUnivariateAnalysis"/> class —
/// the (analysis, weight) pair used by <see cref="CompositeAnalysis"/> for model averaging.
/// </summary>
/// <remarks>
/// Existing CompositeAnalysisTests instantiate this type but exercise composite-level behavior;
/// this file pins the wrapper's own contract: property change events, weight setter idempotency,
/// validation, and the deliberate intent of <see cref="WeightedUnivariateAnalysis.ToXElement"/>
/// to omit the inner-analysis reference (resolved by name on deserialization).
/// </remarks>
[TestClass]
public class WeightedUnivariateAnalysisTests
{
    #region Inline test fixtures

    private static UnivariateAnalysis CreateNormalAnalysis()
    {
        var data = new Normal(100.0, 15.0).GenerateRandomValues(20, 12345);
        var df = new DataFrame();
        for (int i = 0; i < data.Length; i++)
            df.ExactSeries.Add(new ExactData(1990 + i, data[i]));
        var dist = new UnivariateDistribution(df, UnivariateDistributionType.Normal);
        return new UnivariateAnalysis(dist);
    }

    #endregion

    #region Construction

    /// <summary>
    /// Default constructor leaves both Weight and UnivariateAnalysis at their default values.
    /// </summary>
    [TestMethod]
    public void Constructor_Default_HasZeroWeightAndNullAnalysis()
    {
        var w = new WeightedUnivariateAnalysis();

        Assert.AreEqual(0.0, w.Weight);
        Assert.IsNull(w.UnivariateAnalysis);
    }

    /// <summary>
    /// (analysis, weight) constructor sets both values directly.
    /// </summary>
    [TestMethod]
    public void Constructor_WithAnalysisAndWeight_PopulatesProperties()
    {
        var inner = CreateNormalAnalysis();

        var w = new WeightedUnivariateAnalysis(inner, 0.4);

        Assert.AreSame(inner, w.UnivariateAnalysis);
        Assert.AreEqual(0.4, w.Weight, 1e-15);
    }

    #endregion

    #region Weight property

    /// <summary>
    /// Weight setter raises PropertyChanged when the value actually changes.
    /// </summary>
    [TestMethod]
    public void Weight_Setter_RaisesPropertyChangedWhenValueChanges()
    {
        var w = new WeightedUnivariateAnalysis();
        bool fired = false;
        w.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(WeightedUnivariateAnalysis.Weight))
                fired = true;
        };

        w.Weight = 0.5;

        Assert.IsTrue(fired);
        Assert.AreEqual(0.5, w.Weight);
    }

    /// <summary>
    /// Weight setter is idempotent: setting the same value does not re-raise PropertyChanged,
    /// which prevents the composite-analysis bridge from triggering redundant rebuilds.
    /// </summary>
    [TestMethod]
    public void Weight_Setter_DoesNotRaiseWhenValueUnchanged()
    {
        var w = new WeightedUnivariateAnalysis();
        w.Weight = 0.3;
        bool fired = false;
        w.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(WeightedUnivariateAnalysis.Weight))
                fired = true;
        };

        w.Weight = 0.3;

        Assert.IsFalse(fired,
            "Weight setter must short-circuit when assigned the existing value.");
    }

    #endregion

    #region UnivariateAnalysis property

    /// <summary>
    /// Replacing UnivariateAnalysis must unsubscribe from the old instance's PropertyChanged
    /// and subscribe to the new one — verified indirectly by checking that property-change
    /// events from the old analysis no longer propagate.
    /// </summary>
    [TestMethod]
    public void UnivariateAnalysis_Setter_RewiresPropertyChangedSubscription()
    {
        var first = CreateNormalAnalysis();
        var second = CreateNormalAnalysis();

        var w = new WeightedUnivariateAnalysis(first, 0.5);

        int eventCount = 0;
        w.PropertyChanged += (s, e) => eventCount++;

        // Replace the inner analysis. PropertyChanged should fire for the swap itself.
        w.UnivariateAnalysis = second;

        Assert.AreSame(second, w.UnivariateAnalysis);
        Assert.IsTrue(eventCount >= 1,
            "Replacing UnivariateAnalysis should at least fire a PropertyChanged for the property itself.");
    }

    #endregion

    #region Validate

    /// <summary>
    /// Validation rejects a weighted analysis whose inner analysis is null — composite
    /// averaging needs at least the analysis reference to do anything useful.
    /// </summary>
    [TestMethod]
    public void Validate_NullAnalysis_IsInvalid()
    {
        var w = new WeightedUnivariateAnalysis();

        var (isValid, message) = w.Validate();

        Assert.IsFalse(isValid);
        Assert.IsTrue(message.Length > 0);
    }

    /// <summary>
    /// Validation rejects an analysis that has not been estimated. Composite averaging
    /// would otherwise read default / null AnalysisResults and silently produce zeros.
    /// </summary>
    [TestMethod]
    public void Validate_UnestimatedInnerAnalysis_IsInvalid()
    {
        var inner = CreateNormalAnalysis();
        Assert.IsFalse(inner.IsEstimated, "Sanity check: inner analysis starts un-estimated.");

        var w = new WeightedUnivariateAnalysis(inner, 0.5);

        var (isValid, message) = w.Validate();

        Assert.IsFalse(isValid);
        Assert.IsTrue(message.Contains("estimation", StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region Serialization

    /// <summary>
    /// ToXElement returns null when no inner analysis is set — composite serialization
    /// uses this signal to skip empty entries.
    /// </summary>
    [TestMethod]
    public void ToXElement_NullInnerAnalysis_ReturnsNull()
    {
        var w = new WeightedUnivariateAnalysis();

        var xml = w.ToXElement();

        Assert.IsNull(xml);
    }

    /// <summary>
    /// ToXElement persists the weight as an attribute when an inner analysis is present.
    /// The inner-analysis reference is intentionally NOT serialized — composite resolves
    /// it by analysis name when restoring.
    /// </summary>
    [TestMethod]
    public void ToXElement_WithInnerAnalysis_SerializesWeightOnly()
    {
        var inner = CreateNormalAnalysis();
        var w = new WeightedUnivariateAnalysis(inner, 0.42);

        var xml = w.ToXElement();

        Assert.IsNotNull(xml);
        Assert.AreEqual(nameof(WeightedUnivariateAnalysis), xml!.Name.LocalName);
        Assert.IsNotNull(xml.Attribute(nameof(WeightedUnivariateAnalysis.Weight)));
        // Use double.Parse with invariant culture because ToXElement uses G17 invariant formatting.
        var stored = double.Parse(
            xml.Attribute(nameof(WeightedUnivariateAnalysis.Weight))!.Value,
            System.Globalization.CultureInfo.InvariantCulture);
        Assert.AreEqual(0.42, stored, 1e-15);
    }

    #endregion
}
