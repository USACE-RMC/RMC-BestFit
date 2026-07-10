using System.Collections.Specialized;
using Numerics.Data;
using RMC.BestFit.Models;
using BestFitDataFrame = RMC.BestFit.Models.DataFrame;

namespace RMC.BestFit.Tests.DataFrame;

/// <summary>
/// Unit tests for the <c>IntervalSeries</c> class.
/// Tests construction, mutation, querying, sorting, validation, and serialization.
/// </summary>
/// <remarks>
/// IntervalSeries holds interval-censored observations (e.g., paleoflood estimates) where the
/// true value lies in <c>[LowerValue, UpperValue]</c>. Validate accepts a parent
/// <c>DataFrame</c> so it can detect overlap with exact / uncertain series.
/// </remarks>
[TestClass]
public class IntervalSeriesTests
{
    /// <summary>
    /// Creates interval.
    /// </summary>
    /// <param name="index">The zero-based observation index.</param>
    /// <param name="lower">The lower bound.</param>
    /// <param name="value">The value to evaluate.</param>
    /// <param name="upper">The upper bound.</param>
    /// <returns>The created test object.</returns>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static IntervalData MakeInterval(int index, double lower, double value, double upper)
        => new IntervalData(index, lower, value, upper);

    #region Construction

    /// <summary>Verifies that constructor empty has zero count.</summary>
    [TestMethod]
    public void Test_Constructor_Empty_HasZeroCount()
    {
        var series = new IntervalSeries();

        Assert.AreEqual(0, series.Count);
    }

    /// <summary>Verifies that constructor from list clones entries.</summary>
    [TestMethod]
    public void Test_Constructor_FromList_ClonesEntries()
    {
        var source = new List<IntervalData> { MakeInterval(1900, 5_000, 7_000, 9_000) };

        var series = new IntervalSeries(source);

        Assert.AreEqual(1, series.Count);
        // Replacing the source entry must not alter the series.
        source[0] = MakeInterval(2000, 1, 2, 3);
        Assert.AreEqual(1900, series[0].Index);
    }

    /// <summary>Verifies that constructor round trips values for from X element.</summary>
    [TestMethod]
    public void Test_Constructor_FromXElement_RoundTripsValues()
    {
        var original = new IntervalSeries([
            MakeInterval(1500, 60_000, 80_000, 100_000),
            MakeInterval(1700, 50_000, 70_000,  90_000)
        ]);

        var restored = new IntervalSeries(original.ToXElement());

        Assert.AreEqual(2, restored.Count);
        Assert.AreEqual(60_000, ((IntervalData)restored[0]).LowerValue, 1e-12);
        Assert.AreEqual(100_000, ((IntervalData)restored[0]).UpperValue, 1e-12);
    }

    #endregion

    #region Summary statistics

    /// <summary>Verifies that minimum value returns max sentinel when empty.</summary>
    [TestMethod]
    public void Test_MinimumValue_Empty_ReturnsMaxSentinel()
    {
        var series = new IntervalSeries();

        Assert.AreEqual(double.MaxValue, series.MinimumValue());
    }

    /// <summary>Verifies that maximum value returns min sentinel when empty.</summary>
    [TestMethod]
    public void Test_MaximumValue_Empty_ReturnsMinSentinel()
    {
        var series = new IntervalSeries();

        Assert.AreEqual(double.MinValue, series.MaximumValue());
    }

    /// <summary>Verifies that minimum value uses lower value across entries.</summary>
    [TestMethod]
    public void Test_MinimumValue_UsesLowerValueAcrossEntries()
    {
        var series = new IntervalSeries([
            MakeInterval(1500, 60_000, 80_000, 100_000),
            MakeInterval(1700, 50_000, 70_000,  90_000)  // smallest LowerValue
        ]);

        Assert.AreEqual(50_000, series.MinimumValue(), 1e-12);
    }

    /// <summary>Verifies that maximum value uses upper value across entries.</summary>
    [TestMethod]
    public void Test_MaximumValue_UsesUpperValueAcrossEntries()
    {
        var series = new IntervalSeries([
            MakeInterval(1500, 60_000, 80_000, 100_000),  // largest UpperValue
            MakeInterval(1700, 50_000, 70_000,  90_000)
        ]);

        Assert.AreEqual(100_000, series.MaximumValue(), 1e-12);
    }

    /// <summary>Verifies that min max index returns sentinels when empty.</summary>
    [TestMethod]
    public void Test_MinMaxIndex_Empty_ReturnsSentinels()
    {
        var series = new IntervalSeries();

        Assert.AreEqual(-100000, series.MinimumIndex());
        Assert.AreEqual(100000, series.MaximumIndex());
    }

    #endregion

    #region Sorting

    /// <summary>Verifies that sort by index ascending orders by index.</summary>
    [TestMethod]
    public void Test_SortByIndex_Ascending_OrdersByIndex()
    {
        var series = new IntervalSeries([
            MakeInterval(1700, 1, 2, 3),
            MakeInterval(1500, 1, 2, 3),
            MakeInterval(1800, 1, 2, 3)
        ]);

        series.SortByIndex(SortOrder.Ascending);

        Assert.AreEqual(1500, series[0].Index);
        Assert.AreEqual(1700, series[1].Index);
        Assert.AreEqual(1800, series[2].Index);
    }

    /// <summary>Verifies that sort ascending orders by value.</summary>
    [TestMethod]
    public void Test_Sort_Ascending_OrdersByValue()
    {
        var series = new IntervalSeries([
            MakeInterval(1500, 1, 5, 10),
            MakeInterval(1700, 1, 1, 10),
            MakeInterval(1800, 1, 3, 10)
        ]);

        series.Sort(SortOrder.Ascending);

        Assert.AreEqual(1.0, series[0].Value);
        Assert.AreEqual(3.0, series[1].Value);
        Assert.AreEqual(5.0, series[2].Value);
    }

    #endregion

    #region Validation

    /// <summary>Verifies that validate returns valid when empty series no data frame.</summary>
    [TestMethod]
    public void Test_Validate_EmptySeries_NoDataFrame_ReturnsValid()
    {
        var series = new IntervalSeries();

        var (isValid, messages) = series.Validate(null!);

        Assert.IsTrue(isValid);
        Assert.AreEqual(0, messages.Count);
    }

    /// <summary>Verifies that validate overlaps with exact series fails validation.</summary>
    [TestMethod]
    public void Test_Validate_OverlapsWithExactSeries_FailsValidation()
    {
        var df = new BestFitDataFrame
        {
            ExactSeries = new ExactSeries([new ExactData(1500, 70_000)])
        };
        df.IntervalSeries.Add(MakeInterval(1500, 60_000, 80_000, 100_000));

        var (isValid, messages) = df.IntervalSeries.Validate(df);

        Assert.IsFalse(isValid);
        Assert.IsTrue(messages.Any(m => m.Contains("overlaps with exact")));
    }

    #endregion

    #region Cloning and conversion

    /// <summary>Verifies that clone produces independent copy.</summary>
    [TestMethod]
    public void Test_Clone_ProducesIndependentCopy()
    {
        var original = new IntervalSeries([MakeInterval(1500, 1, 2, 3)]);

        var clone = original.Clone();
        clone.Add(MakeInterval(2000, 4, 5, 6));

        Assert.AreEqual(1, original.Count);
        Assert.AreEqual(2, clone.Count);
    }

    /// <summary>Verifies that to list returns cloned items.</summary>
    [TestMethod]
    public void Test_ToList_ReturnsClonedItems()
    {
        var series = new IntervalSeries([MakeInterval(1500, 1, 2, 3)]);

        var list = series.ToList();
        list[0] = MakeInterval(2000, 9, 9, 9);

        Assert.AreEqual(1500, series[0].Index);
    }

    /// <summary>Verifies that to X element preserves data for round trip.</summary>
    [TestMethod]
    public void Test_ToXElement_RoundTrip_PreservesData()
    {
        var original = new IntervalSeries([MakeInterval(1500, 60_000, 80_000, 100_000)]);

        var restored = new IntervalSeries(original.ToXElement());

        Assert.AreEqual(1, restored.Count);
        var data = (IntervalData)restored[0];
        Assert.AreEqual(60_000, data.LowerValue, 1e-12);
        Assert.AreEqual(80_000, data.Value, 1e-12);
        Assert.AreEqual(100_000, data.UpperValue, 1e-12);
    }

    #endregion

    #region Collection-changed notifications

    /// <summary>Verifies that add raises collection changed event.</summary>
    [TestMethod]
    public void Test_Add_RaisesCollectionChangedEvent()
    {
        var series = new IntervalSeries();
        NotifyCollectionChangedEventArgs? captured = null;
        series.CollectionChanged += (_, e) => captured = e;

        series.Add(MakeInterval(1500, 1, 2, 3));

        Assert.IsNotNull(captured);
        Assert.AreEqual(NotifyCollectionChangedAction.Add, captured!.Action);
    }

    #endregion
}
