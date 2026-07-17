using System.Collections.Specialized;
using System.Xml.Linq;
using Numerics.Data;
using RMC.BestFit.Models;

namespace RMC.BestFit.Tests.DataFrame;

/// <summary>
/// Unit tests for the <c>ExactSeries</c> class.
/// Tests construction, mutation, querying, sorting, validation, and serialization.
/// </summary>
/// <remarks>
/// ExactSeries is the data collection for systematic-record observations. It exposes
/// summary statistics (median / min / max / index span) used downstream by plotting
/// and threshold validation, plus serialization for project persistence.
/// </remarks>
[TestClass]
public class ExactSeriesTests
{
    #region Construction

    /// <summary>Verifies that constructor empty has zero count.</summary>
    [TestMethod]
    public void Test_Constructor_Empty_HasZeroCount()
    {
        var series = new ExactSeries();

        Assert.AreEqual(0, series.Count);
        Assert.IsFalse(series.EnforceUniqueIndex);
    }

    /// <summary>Verifies that constructor from values populates with sequential indexes.</summary>
    [TestMethod]
    public void Test_Constructor_FromValues_PopulatesWithSequentialIndexes()
    {
        var values = new double[] { 10.0, 20.0, 30.0 };

        var series = new ExactSeries(values);

        Assert.AreEqual(3, series.Count);
        Assert.AreEqual(0, series[0].Index);
        Assert.AreEqual(10.0, series[0].Value);
        Assert.AreEqual(2, series[2].Index);
        Assert.AreEqual(30.0, series[2].Value);
    }

    /// <summary>Verifies that constructor from exact data list clones entries.</summary>
    [TestMethod]
    public void Test_Constructor_FromExactDataList_ClonesEntries()
    {
        var source = new List<ExactData>
        {
            new ExactData(1985, 75000.0),
            new ExactData(1986, 82000.0)
        };

        var series = new ExactSeries(source);

        Assert.AreEqual(2, series.Count);
        // Mutate the source after construction; the series must hold its own clones.
        source[0] = new ExactData(2000, 1.0);
        Assert.AreEqual(1985, series[0].Index, "Series should hold cloned items, not source references.");
        Assert.AreEqual(75000.0, series[0].Value);
    }

    /// <summary>Verifies that constructor round trips values for from X element.</summary>
    [TestMethod]
    public void Test_Constructor_FromXElement_RoundTripsValues()
    {
        var original = new ExactSeries([new ExactData(1980, 50_000.0), new ExactData(1981, 60_000.0)]);
        var xml = original.ToXElement();

        var restored = new ExactSeries(xml);

        Assert.AreEqual(2, restored.Count);
        Assert.AreEqual(1980, restored[0].Index);
        Assert.AreEqual(50_000.0, restored[0].Value);
        Assert.AreEqual(60_000.0, restored[1].Value);
    }

    #endregion

    #region Summary statistics

    /// <summary>Verifies that median value returns middle value when odd count.</summary>
    [TestMethod]
    public void Test_MedianValue_OddCount_ReturnsMiddleValue()
    {
        var series = new ExactSeries([1.0, 5.0, 3.0]); // sorted: 1, 3, 5

        Assert.AreEqual(3.0, series.MedianValue, 1e-12);
    }

    /// <summary>Verifies that median value returns average of two middle when even count.</summary>
    [TestMethod]
    public void Test_MedianValue_EvenCount_ReturnsAverageOfTwoMiddle()
    {
        var series = new ExactSeries([4.0, 1.0, 2.0, 3.0]); // sorted: 1, 2, 3, 4

        Assert.AreEqual(2.5, series.MedianValue, 1e-12);
    }

    /// <summary>Verifies that median value returns zero when empty.</summary>
    [TestMethod]
    public void Test_MedianValue_Empty_ReturnsZero()
    {
        var series = new ExactSeries();

        Assert.AreEqual(0.0, series.MedianValue);
    }

    /// <summary>Verifies that median value returns that value when single value.</summary>
    [TestMethod]
    public void Test_MedianValue_SingleValue_ReturnsThatValue()
    {
        var series = new ExactSeries([42.0]);

        Assert.AreEqual(42.0, series.MedianValue);
    }

    /// <summary>Verifies that upper middle value returns value at mid index when even count.</summary>
    [TestMethod]
    public void Test_UpperMiddleValue_EvenCount_ReturnsValueAtMidIndex()
    {
        // For even N=4 the upper-middle is the value at index N/2 = 2 of the sorted array.
        var series = new ExactSeries([4.0, 1.0, 2.0, 3.0]); // sorted: 1, 2, 3, 4

        Assert.AreEqual(3.0, series.UpperMiddleValue);
    }

    /// <summary>Verifies that upper middle value odd count equals median.</summary>
    [TestMethod]
    public void Test_UpperMiddleValue_OddCount_EqualsMedian()
    {
        var series = new ExactSeries([1.0, 5.0, 3.0]);

        Assert.AreEqual(series.MedianValue, series.UpperMiddleValue);
    }

    /// <summary>Verifies that minimum value returns max sentinel when empty.</summary>
    [TestMethod]
    public void Test_MinimumValue_Empty_ReturnsMaxSentinel()
    {
        var series = new ExactSeries();

        Assert.AreEqual(double.MaxValue, series.MinimumValue());
    }

    /// <summary>Verifies that maximum value returns min sentinel when empty.</summary>
    [TestMethod]
    public void Test_MaximumValue_Empty_ReturnsMinSentinel()
    {
        var series = new ExactSeries();

        Assert.AreEqual(double.MinValue, series.MaximumValue());
    }

    /// <summary>Verifies that min max value returns correct extremes when populated.</summary>
    [TestMethod]
    public void Test_MinMaxValue_Populated_ReturnsCorrectExtremes()
    {
        var series = new ExactSeries([7.0, 2.0, 9.0, 4.0]);

        Assert.AreEqual(2.0, series.MinimumValue());
        Assert.AreEqual(9.0, series.MaximumValue());
    }

    /// <summary>Verifies that min max index returns sentinels when empty.</summary>
    [TestMethod]
    public void Test_MinMaxIndex_Empty_ReturnsSentinels()
    {
        var series = new ExactSeries();

        Assert.AreEqual(-100000, series.MinimumIndex());
        Assert.AreEqual(100000, series.MaximumIndex());
    }

    /// <summary>Verifies that index span returns zero when empty.</summary>
    [TestMethod]
    public void Test_IndexSpan_Empty_ReturnsZero()
    {
        var series = new ExactSeries();

        Assert.AreEqual(0, series.IndexSpan());
    }

    /// <summary>Verifies that index span returns max minus min plus one when populated.</summary>
    [TestMethod]
    public void Test_IndexSpan_Populated_ReturnsMaxMinusMinPlusOne()
    {
        var series = new ExactSeries([
            new ExactData(1985, 1.0),
            new ExactData(1990, 2.0),
            new ExactData(1995, 3.0)
        ]);

        Assert.AreEqual(11, series.IndexSpan(), "Span = 1995 - 1985 + 1.");
    }

    /// <summary>Verifies that unique indices counts distinct indexes.</summary>
    [TestMethod]
    public void Test_UniqueIndices_CountsDistinctIndexes()
    {
        var series = new ExactSeries([
            new ExactData(1985, 1.0),
            new ExactData(1985, 2.0), // duplicate index, distinct value
            new ExactData(1990, 3.0)
        ]);

        Assert.AreEqual(2, series.UniqueIndices());
    }

    #endregion

    #region Sorting

    /// <summary>Verifies that sort by index ascending orders by index.</summary>
    [TestMethod]
    public void Test_SortByIndex_Ascending_OrdersByIndex()
    {
        var series = new ExactSeries([
            new ExactData(1990, 1.0),
            new ExactData(1980, 2.0),
            new ExactData(1985, 3.0)
        ]);

        series.SortByIndex(SortOrder.Ascending);

        Assert.AreEqual(1980, series[0].Index);
        Assert.AreEqual(1985, series[1].Index);
        Assert.AreEqual(1990, series[2].Index);
    }

    /// <summary>Verifies that sort by index descending orders by index descending.</summary>
    [TestMethod]
    public void Test_SortByIndex_Descending_OrdersByIndexDescending()
    {
        var series = new ExactSeries([
            new ExactData(1985, 1.0),
            new ExactData(1990, 2.0),
            new ExactData(1980, 3.0)
        ]);

        series.SortByIndex(SortOrder.Descending);

        Assert.AreEqual(1990, series[0].Index);
        Assert.AreEqual(1980, series[2].Index);
    }

    /// <summary>Verifies that sort ascending orders by value.</summary>
    [TestMethod]
    public void Test_Sort_Ascending_OrdersByValue()
    {
        var series = new ExactSeries([5.0, 1.0, 3.0]);

        series.Sort(SortOrder.Ascending);

        Assert.AreEqual(1.0, series[0].Value);
        Assert.AreEqual(3.0, series[1].Value);
        Assert.AreEqual(5.0, series[2].Value);
    }

    /// <summary>Verifies that sort descending orders by value descending.</summary>
    [TestMethod]
    public void Test_Sort_Descending_OrdersByValueDescending()
    {
        var series = new ExactSeries([1.0, 5.0, 3.0]);

        series.Sort(SortOrder.Descending);

        Assert.AreEqual(5.0, series[0].Value);
        Assert.AreEqual(1.0, series[2].Value);
    }

    #endregion

    #region Validation

    /// <summary>Verifies that validate returns valid when empty series.</summary>
    [TestMethod]
    public void Test_Validate_EmptySeries_ReturnsValid()
    {
        var series = new ExactSeries();

        var (isValid, messages) = series.Validate();

        Assert.IsTrue(isValid);
        Assert.AreEqual(0, messages.Count);
    }

    /// <summary>Verifies that validate duplicate indexes rejected when enforced.</summary>
    [TestMethod]
    public void Test_Validate_DuplicateIndexes_RejectedWhenEnforced()
    {
        var series = new ExactSeries([
            new ExactData(1985, 1.0),
            new ExactData(1985, 2.0)
        ]) { EnforceUniqueIndex = true };

        var (isValid, messages) = series.Validate();

        Assert.IsFalse(isValid);
        Assert.IsTrue(messages.Any(m => m.Contains("Duplicate")));
    }

    /// <summary>Verifies that validate duplicate indexes accepted when not enforced.</summary>
    [TestMethod]
    public void Test_Validate_DuplicateIndexes_AcceptedWhenNotEnforced()
    {
        var series = new ExactSeries([
            new ExactData(1985, 1.0),
            new ExactData(1985, 2.0)
        ]);

        var (isValid, _) = series.Validate();

        Assert.IsTrue(isValid, "Duplicate indexes should be allowed when EnforceUniqueIndex is false.");
    }

    #endregion

    #region Cloning and conversion

    /// <summary>Verifies that clone produces independent copy.</summary>
    [TestMethod]
    public void Test_Clone_ProducesIndependentCopy()
    {
        var original = new ExactSeries([1.0, 2.0, 3.0]);

        var clone = original.Clone();
        clone.Add(new ExactData(99, 99.0));

        Assert.AreEqual(3, original.Count, "Mutation on clone must not propagate to original.");
        Assert.AreEqual(4, clone.Count);
    }

    /// <summary>Verifies that to list returns cloned items.</summary>
    [TestMethod]
    public void Test_ToList_ReturnsClonedItems()
    {
        var series = new ExactSeries([new ExactData(1985, 1.0)]);

        var list = series.ToList();
        list[0] = new ExactData(2000, 999.0);

        Assert.AreEqual(1985, series[0].Index, "ToList must return cloned items.");
    }

    /// <summary>Verifies that values to array returns values in order.</summary>
    [TestMethod]
    public void Test_ValuesToArray_ReturnsValuesInOrder()
    {
        var series = new ExactSeries([10.0, 20.0, 30.0]);

        var values = series.ValuesToArray();

        CollectionAssert.AreEqual(new[] { 10.0, 20.0, 30.0 }, values);
    }

    /// <summary>Verifies that to X element preserves data for round trip.</summary>
    [TestMethod]
    public void Test_ToXElement_RoundTrip_PreservesData()
    {
        var original = new ExactSeries([
            new ExactData(1980, 50_000.0, 0.1, false),
            new ExactData(1981, 60_000.0, 0.2, true)
        ]);

        var xml = original.ToXElement();
        var restored = new ExactSeries(xml);

        Assert.AreEqual(original.Count, restored.Count);
        for (int i = 0; i < original.Count; i++)
        {
            Assert.AreEqual(original[i].Index, restored[i].Index);
            Assert.AreEqual(original[i].Value, restored[i].Value, 1e-12);
        }
    }

    #endregion

    #region Collection-changed notifications

    /// <summary>Verifies that add raises collection changed event.</summary>
    [TestMethod]
    public void Test_Add_RaisesCollectionChangedEvent()
    {
        var series = new ExactSeries();
        NotifyCollectionChangedEventArgs? captured = null;
        series.CollectionChanged += (_, e) => captured = e;

        series.Add(new ExactData(0, 1.0));

        Assert.IsNotNull(captured);
        Assert.AreEqual(NotifyCollectionChangedAction.Add, captured!.Action);
    }

    /// <summary>Verifies that suppress collection changed blocks events.</summary>
    [TestMethod]
    public void Test_SuppressCollectionChanged_BlocksEvents()
    {
        var series = new ExactSeries { SuppressCollectionChanged = true };
        bool fired = false;
        series.CollectionChanged += (_, _) => fired = true;

        series.Add(new ExactData(0, 1.0));

        Assert.IsFalse(fired, "Events must be suppressed when SuppressCollectionChanged = true.");
    }

    #endregion
}
