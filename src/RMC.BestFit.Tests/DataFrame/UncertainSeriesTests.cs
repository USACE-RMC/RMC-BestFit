using System.Collections.Specialized;
using Numerics.Data;
using Numerics.Distributions;
using RMC.BestFit.Models;

namespace RMC.BestFit.Tests.InputDataFrame;

/// <summary>
/// Unit tests for the <see cref="UncertainSeries"/> class.
/// Tests construction, sorting, validation, and serialization.
/// </summary>
/// <remarks>
/// UncertainSeries holds observations with measurement uncertainty modeled as a distribution
/// (e.g., a paleoflood estimate with a Normal error). <c>MinimumValue</c> /
/// <c>MaximumValue</c> use the underlying distribution's lower / upper extents.
/// </remarks>
[TestClass]
public class UncertainSeriesTests
{
    private static UncertainData MakeUncertain(int index, double mean, double stdev)
        => new UncertainData(index, new Normal(mean, stdev));

    #region Construction

    /// <summary>Verifies that constructor empty has zero count.</summary>
    [TestMethod]
    public void Test_Constructor_Empty_HasZeroCount()
    {
        var series = new UncertainSeries();

        Assert.AreEqual(0, series.Count);
    }

    /// <summary>Verifies that constructor from list clones entries.</summary>
    [TestMethod]
    public void Test_Constructor_FromList_ClonesEntries()
    {
        var source = new List<UncertainData> { MakeUncertain(1889, 85_000, 10_000) };

        var series = new UncertainSeries(source);

        Assert.AreEqual(1, series.Count);
        // Replacing the source entry must not propagate to the series.
        source[0] = MakeUncertain(0, 0, 1);
        Assert.AreEqual(1889, series[0].Index);
    }

    /// <summary>Verifies that constructor round trips values for from X element.</summary>
    [TestMethod]
    public void Test_Constructor_FromXElement_RoundTripsValues()
    {
        var original = new UncertainSeries([MakeUncertain(1889, 85_000, 10_000)]);

        var restored = new UncertainSeries(original.ToXElement());

        Assert.AreEqual(1, restored.Count);
        Assert.AreEqual(1889, restored[0].Index);
        Assert.AreEqual(85_000, restored[0].Value, 1e-9);
    }

    #endregion

    #region Summary statistics

    /// <summary>Verifies that minimum value returns max sentinel when empty.</summary>
    [TestMethod]
    public void Test_MinimumValue_Empty_ReturnsMaxSentinel()
    {
        var series = new UncertainSeries();

        Assert.AreEqual(double.MaxValue, series.MinimumValue());
    }

    /// <summary>Verifies that maximum value returns min sentinel when empty.</summary>
    [TestMethod]
    public void Test_MaximumValue_Empty_ReturnsMinSentinel()
    {
        var series = new UncertainSeries();

        Assert.AreEqual(double.MinValue, series.MaximumValue());
    }

    /// <summary>Verifies that min max index returns sentinels when empty.</summary>
    [TestMethod]
    public void Test_MinMaxIndex_Empty_ReturnsSentinels()
    {
        var series = new UncertainSeries();

        Assert.AreEqual(-100000, series.MinimumIndex());
        Assert.AreEqual(100000, series.MaximumIndex());
    }

    /// <summary>Verifies that min max index returns correct extremes when populated.</summary>
    [TestMethod]
    public void Test_MinMaxIndex_Populated_ReturnsCorrectExtremes()
    {
        var series = new UncertainSeries([
            MakeUncertain(1900, 1, 1),
            MakeUncertain(1880, 1, 1),
            MakeUncertain(1920, 1, 1)
        ]);

        Assert.AreEqual(1880, series.MinimumIndex());
        Assert.AreEqual(1920, series.MaximumIndex());
    }

    #endregion

    #region Sorting

    /// <summary>Verifies that sort by index ascending orders by index.</summary>
    [TestMethod]
    public void Test_SortByIndex_Ascending_OrdersByIndex()
    {
        var series = new UncertainSeries([
            MakeUncertain(1900, 1, 1),
            MakeUncertain(1880, 1, 1),
            MakeUncertain(1920, 1, 1)
        ]);

        series.SortByIndex(SortOrder.Ascending);

        Assert.AreEqual(1880, series[0].Index);
        Assert.AreEqual(1900, series[1].Index);
        Assert.AreEqual(1920, series[2].Index);
    }

    /// <summary>Verifies that sort ascending orders by value.</summary>
    [TestMethod]
    public void Test_Sort_Ascending_OrdersByValue()
    {
        var series = new UncertainSeries([
            MakeUncertain(1900, 50_000, 1),
            MakeUncertain(1880, 30_000, 1),
            MakeUncertain(1920, 40_000, 1)
        ]);

        series.Sort(SortOrder.Ascending);

        Assert.AreEqual(30_000, series[0].Value, 1e-9);
        Assert.AreEqual(40_000, series[1].Value, 1e-9);
        Assert.AreEqual(50_000, series[2].Value, 1e-9);
    }

    #endregion

    #region Validation

    /// <summary>Verifies that validate returns valid when empty series no data frame.</summary>
    [TestMethod]
    public void Test_Validate_EmptySeries_NoDataFrame_ReturnsValid()
    {
        var series = new UncertainSeries();

        var (isValid, messages) = series.Validate(null!);

        Assert.IsTrue(isValid);
        Assert.AreEqual(0, messages.Count);
    }

    /// <summary>Verifies that validate overlaps with exact series fails validation.</summary>
    [TestMethod]
    public void Test_Validate_OverlapsWithExactSeries_FailsValidation()
    {
        var df = new DataFrame
        {
            ExactSeries = new ExactSeries([new ExactData(1889, 50_000)])
        };
        df.UncertainSeries.Add(MakeUncertain(1889, 85_000, 10_000));

        var (isValid, messages) = df.UncertainSeries.Validate(df);

        Assert.IsFalse(isValid);
        Assert.IsTrue(messages.Any(m => m.Contains("overlaps with exact")));
    }

    #endregion

    #region Cloning and conversion

    /// <summary>Verifies that clone produces independent copy.</summary>
    [TestMethod]
    public void Test_Clone_ProducesIndependentCopy()
    {
        var original = new UncertainSeries([MakeUncertain(1889, 85_000, 10_000)]);

        var clone = original.Clone();
        clone.Add(MakeUncertain(1900, 60_000, 5_000));

        Assert.AreEqual(1, original.Count);
        Assert.AreEqual(2, clone.Count);
    }

    /// <summary>Verifies that to list returns cloned items.</summary>
    [TestMethod]
    public void Test_ToList_ReturnsClonedItems()
    {
        var series = new UncertainSeries([MakeUncertain(1889, 85_000, 10_000)]);

        var list = series.ToList();
        list[0] = MakeUncertain(2000, 1, 1);

        Assert.AreEqual(1889, series[0].Index);
    }

    /// <summary>Verifies that to X element preserves data for round trip.</summary>
    [TestMethod]
    public void Test_ToXElement_RoundTrip_PreservesData()
    {
        var original = new UncertainSeries([
            MakeUncertain(1889, 85_000, 10_000),
            MakeUncertain(1913, 75_000, 8_000)
        ]);

        var restored = new UncertainSeries(original.ToXElement());

        Assert.AreEqual(original.Count, restored.Count);
        for (int i = 0; i < original.Count; i++)
            Assert.AreEqual(original[i].Index, restored[i].Index);
    }

    #endregion

    #region Collection-changed notifications

    /// <summary>Verifies that add raises collection changed event.</summary>
    [TestMethod]
    public void Test_Add_RaisesCollectionChangedEvent()
    {
        var series = new UncertainSeries();
        NotifyCollectionChangedEventArgs? captured = null;
        series.CollectionChanged += (_, e) => captured = e;

        series.Add(MakeUncertain(1889, 85_000, 10_000));

        Assert.IsNotNull(captured);
        Assert.AreEqual(NotifyCollectionChangedAction.Add, captured!.Action);
    }

    #endregion
}
