using System.Collections.Specialized;
using Numerics.Data;
using BestFitThresholdSeries = RMC.BestFit.Models.ThresholdSeries;
using BestFitThresholdData = RMC.BestFit.Models.ThresholdData;
using RMC.BestFit.Models;

namespace RMC.BestFit.Tests.DataFrame;

/// <summary>
/// Unit tests for the <c>ThresholdSeries</c> class.
/// Tests construction, sorting, validation, and serialization.
/// </summary>
/// <remarks>
/// BestFitThresholdSeries holds historical perception-threshold periods. <c>MinimumIndex</c> /
/// <c>MaximumIndex</c> reflect the period span (StartIndex / EndIndex) rather than a
/// single observation index — different from the other series classes.
/// </remarks>
[TestClass]
public class ThresholdSeriesTests
{
    /// <summary>
    /// Creates threshold.
    /// </summary>
    /// <param name="start">The start value.</param>
    /// <param name="end">The end value.</param>
    /// <param name="threshold">The threshold value.</param>
    /// <param name="numberAbove">The number of threshold exceedances.</param>
    /// <returns>The created test object.</returns>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static BestFitThresholdData MakeThreshold(int start, int end, double threshold, int numberAbove = 0)
        => new BestFitThresholdData(start, end, threshold) { NumberAbove = numberAbove };

    #region Construction

    /// <summary>Verifies that constructor empty has zero count.</summary>
    [TestMethod]
    public void Test_Constructor_Empty_HasZeroCount()
    {
        var series = new BestFitThresholdSeries();

        Assert.AreEqual(0, series.Count);
    }

    /// <summary>Verifies that constructor from list clones entries.</summary>
    [TestMethod]
    public void Test_Constructor_FromList_ClonesEntries()
    {
        var source = new List<BestFitThresholdData> { MakeThreshold(1850, 1920, 40_000, 5) };

        var series = new BestFitThresholdSeries(source);

        Assert.AreEqual(1, series.Count);
        // Replace source: series should still hold its own clone.
        source[0] = MakeThreshold(0, 1, 0);
        var stored = (BestFitThresholdData)series[0];
        Assert.AreEqual(1850, stored.StartIndex);
        Assert.AreEqual(1920, stored.EndIndex);
        Assert.AreEqual(40_000, stored.Value, 1e-12);
        Assert.AreEqual(5, stored.NumberAbove);
    }

    /// <summary>Verifies that constructor round trips values for from X element.</summary>
    [TestMethod]
    public void Test_Constructor_FromXElement_RoundTripsValues()
    {
        var original = new BestFitThresholdSeries([MakeThreshold(1850, 1920, 40_000, 5)]);

        var restored = new BestFitThresholdSeries(original.ToXElement());

        Assert.AreEqual(1, restored.Count);
        var stored = (BestFitThresholdData)restored[0];
        Assert.AreEqual(1850, stored.StartIndex);
        Assert.AreEqual(1920, stored.EndIndex);
        Assert.AreEqual(40_000, stored.Value, 1e-12);
    }

    #endregion

    #region Summary statistics

    /// <summary>Verifies that minimum value returns max sentinel when empty.</summary>
    [TestMethod]
    public void Test_MinimumValue_Empty_ReturnsMaxSentinel()
    {
        var series = new BestFitThresholdSeries();

        Assert.AreEqual(double.MaxValue, series.MinimumValue());
    }

    /// <summary>Verifies that maximum value returns min sentinel when empty.</summary>
    [TestMethod]
    public void Test_MaximumValue_Empty_ReturnsMinSentinel()
    {
        var series = new BestFitThresholdSeries();

        Assert.AreEqual(double.MinValue, series.MaximumValue());
    }

    /// <summary>Verifies that min max value returns correct extremes when populated.</summary>
    [TestMethod]
    public void Test_MinMaxValue_Populated_ReturnsCorrectExtremes()
    {
        var series = new BestFitThresholdSeries([
            MakeThreshold(1800, 1850, 30_000),
            MakeThreshold(1851, 1900, 50_000),  // largest
            MakeThreshold(1901, 1950, 40_000)
        ]);

        Assert.AreEqual(30_000, series.MinimumValue(), 1e-12);
        Assert.AreEqual(50_000, series.MaximumValue(), 1e-12);
    }

    /// <summary>Verifies that min index uses start index.</summary>
    [TestMethod]
    public void Test_MinIndex_UsesStartIndex()
    {
        var series = new BestFitThresholdSeries([
            MakeThreshold(1851, 1900, 50_000),
            MakeThreshold(1800, 1850, 30_000)   // earliest StartIndex
        ]);

        Assert.AreEqual(1800, series.MinimumIndex());
    }

    /// <summary>Verifies that max index uses end index.</summary>
    [TestMethod]
    public void Test_MaxIndex_UsesEndIndex()
    {
        var series = new BestFitThresholdSeries([
            MakeThreshold(1800, 1850, 30_000),
            MakeThreshold(1851, 1900, 50_000)   // latest EndIndex
        ]);

        Assert.AreEqual(1900, series.MaximumIndex());
    }

    /// <summary>Verifies that min max index returns sentinels when empty.</summary>
    [TestMethod]
    public void Test_MinMaxIndex_Empty_ReturnsSentinels()
    {
        var series = new BestFitThresholdSeries();

        Assert.AreEqual(-100000, series.MinimumIndex());
        Assert.AreEqual(100000, series.MaximumIndex());
    }

    #endregion

    #region Sorting

    /// <summary>Verifies that sort by index ascending orders by start index.</summary>
    [TestMethod]
    public void Test_SortByIndex_Ascending_OrdersByStartIndex()
    {
        var series = new BestFitThresholdSeries([
            MakeThreshold(1900, 1950, 1.0),
            MakeThreshold(1800, 1850, 1.0),
            MakeThreshold(1851, 1899, 1.0)
        ]);

        series.SortByIndex(SortOrder.Ascending);

        Assert.AreEqual(1800, ((BestFitThresholdData)series[0]).StartIndex);
        Assert.AreEqual(1851, ((BestFitThresholdData)series[1]).StartIndex);
        Assert.AreEqual(1900, ((BestFitThresholdData)series[2]).StartIndex);
    }

    /// <summary>Verifies that sort descending orders by value descending.</summary>
    [TestMethod]
    public void Test_Sort_Descending_OrdersByValueDescending()
    {
        var series = new BestFitThresholdSeries([
            MakeThreshold(1800, 1850, 30_000),
            MakeThreshold(1851, 1900, 50_000),
            MakeThreshold(1901, 1950, 40_000)
        ]);

        series.Sort(SortOrder.Descending);

        Assert.AreEqual(50_000, series[0].Value, 1e-12);
        Assert.AreEqual(40_000, series[1].Value, 1e-12);
        Assert.AreEqual(30_000, series[2].Value, 1e-12);
    }

    #endregion

    #region Validation

    /// <summary>Verifies that validate returns valid when empty series.</summary>
    [TestMethod]
    public void Test_Validate_EmptySeries_ReturnsValid()
    {
        var series = new BestFitThresholdSeries();

        var (isValid, messages) = series.Validate();

        Assert.IsTrue(isValid);
        Assert.AreEqual(0, messages.Count);
    }

    /// <summary>Verifies that validate returns valid when non overlapping periods.</summary>
    [TestMethod]
    public void Test_Validate_NonOverlappingPeriods_ReturnsValid()
    {
        var series = new BestFitThresholdSeries([
            MakeThreshold(1800, 1850, 30_000, 1),
            MakeThreshold(1851, 1900, 40_000, 2)
        ]);

        var (isValid, _) = series.Validate();

        Assert.IsTrue(isValid);
    }

    /// <summary>Verifies that validate overlapping periods fails validation.</summary>
    [TestMethod]
    public void Test_Validate_OverlappingPeriods_FailsValidation()
    {
        var series = new BestFitThresholdSeries([
            MakeThreshold(1800, 1900, 30_000, 1),
            MakeThreshold(1850, 1950, 40_000, 2)   // overlaps 1850–1900
        ]);

        var (isValid, messages) = series.Validate();

        Assert.IsFalse(isValid);
        Assert.IsTrue(messages.Any(m => m.Contains("overlap")));
    }

    #endregion

    #region Cloning and conversion

    /// <summary>Verifies that clone produces independent copy.</summary>
    [TestMethod]
    public void Test_Clone_ProducesIndependentCopy()
    {
        var original = new BestFitThresholdSeries([MakeThreshold(1800, 1850, 30_000)]);

        var clone = original.Clone();
        clone.Add(MakeThreshold(1851, 1900, 40_000));

        Assert.AreEqual(1, original.Count);
        Assert.AreEqual(2, clone.Count);
    }

    /// <summary>Verifies that to list returns cloned items.</summary>
    [TestMethod]
    public void Test_ToList_ReturnsClonedItems()
    {
        var series = new BestFitThresholdSeries([MakeThreshold(1800, 1850, 30_000)]);

        var list = series.ToList();
        list[0] = MakeThreshold(0, 1, 999);

        Assert.AreEqual(1800, ((BestFitThresholdData)series[0]).StartIndex);
    }

    /// <summary>Verifies that to X element preserves number above for round trip.</summary>
    [TestMethod]
    public void Test_ToXElement_RoundTrip_PreservesNumberAbove()
    {
        var original = new BestFitThresholdSeries([MakeThreshold(1850, 1920, 40_000, 5)]);

        var restored = new BestFitThresholdSeries(original.ToXElement());

        Assert.AreEqual(5, ((BestFitThresholdData)restored[0]).NumberAbove);
    }

    #endregion

    #region Collection-changed notifications

    /// <summary>Verifies that add raises collection changed event.</summary>
    [TestMethod]
    public void Test_Add_RaisesCollectionChangedEvent()
    {
        var series = new BestFitThresholdSeries();
        NotifyCollectionChangedEventArgs? captured = null;
        series.CollectionChanged += (_, e) => captured = e;

        series.Add(MakeThreshold(1800, 1850, 30_000));

        Assert.IsNotNull(captured);
        Assert.AreEqual(NotifyCollectionChangedAction.Add, captured!.Action);
    }

    #endregion
}
