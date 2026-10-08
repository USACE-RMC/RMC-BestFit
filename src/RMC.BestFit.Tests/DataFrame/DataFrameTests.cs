using Numerics.Distributions;
using RMC.BestFit.Models;
using BestFitDataFrame = RMC.BestFit.Models.DataFrame;
using BestFitThresholdData = RMC.BestFit.Models.ThresholdData;
using BestFitThresholdSeries = RMC.BestFit.Models.ThresholdSeries;

namespace RMC.BestFit.Tests.DataFrame;

/// <summary>
/// Unit tests for the <c>DataFrame</c> class.
/// Tests data series management, property change notifications, validation, and serialization.
/// </summary>
/// <remarks>
/// <para>
///     <b> Authors: </b>
///     <list type="bullet">
///     <item>Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil</item>
///     </list>
/// </para>
/// <para>
/// The <c>DataFrame</c> is the fundamental input data structure for flood frequency analysis,
/// supporting exact observations, uncertain data with measurement error distributions, interval-censored
/// data from paleofloods, and historical threshold information.
/// </para>
/// </remarks>
[TestClass]
public class DataFrameTests
{
    #region Test Data Helpers

    /// <summary>
    /// Sample annual peak flow data (cfs) for testing.
    /// </summary>
    private static readonly double[] SampleAnnualPeaks =
    [
        45000, 38000, 52000, 61000, 33000, 49000, 55000, 42000, 67000, 39000,
        48000, 51000, 36000, 58000, 44000, 53000, 47000, 62000, 41000, 50000,
        37000, 54000, 46000, 59000, 43000, 56000, 40000, 63000, 35000, 57000
    ];

    /// <summary>
    /// Creates a test BestFitDataFrame with exact data.
    /// </summary>
    private static BestFitDataFrame CreateTestDataFrame(int count = 30)
    {
        var df = new BestFitDataFrame();
        var data = SampleAnnualPeaks.Take(count).ToArray();
        df.ExactSeries = new ExactSeries(data);
        return df;
    }

    /// <summary>
    /// Creates a test BestFitDataFrame with mixed data types.
    /// </summary>
    private static BestFitDataFrame CreateMixedDataFrame()
    {
        var df = new BestFitDataFrame();

        // Exact observations (systematic record)
        df.ExactSeries = new ExactSeries([45000, 52000, 61000, 49000, 55000, 67000, 48000, 58000, 53000, 62000]);

        // Uncertain data (historical flood with measurement error)
        df.UncertainSeries.Add(new UncertainData(1889, new Normal(85000, 10000)));
        df.UncertainSeries.Add(new UncertainData(1913, new Normal(75000, 8000)));

        // Interval data (paleoflood)
        df.IntervalSeries.Add(new IntervalData(1500, 60000, 80000, 100000));
        df.IntervalSeries.Add(new IntervalData(1700, 50000, 70000, 90000));

        // Threshold data (historical period with perception threshold).
        // NumberAbove is user-set; NumberBelow is auto-derived by BestFitDataFrame on Add.
        var threshold = new BestFitThresholdData(1850, 1920, 40000) { NumberAbove = 5 };
        df.ThresholdSeries.Add(threshold);

        return df;
    }

    #endregion

    #region Construction Tests

    /// <summary>
    /// Tests that the empty constructor creates an empty data frame.
    /// </summary>
    [TestMethod]
    public void Constructor_Empty_CreatesEmptyDataFrame()
    {
        var df = new BestFitDataFrame();

        Assert.IsNotNull(df);
        Assert.IsNotNull(df.ExactSeries);
        Assert.IsNotNull(df.UncertainSeries);
        Assert.IsNotNull(df.IntervalSeries);
        Assert.IsNotNull(df.ThresholdSeries);
        Assert.AreEqual(0, df.ExactSeries.Count);
        Assert.AreEqual(0, df.UncertainSeries.Count);
        Assert.AreEqual(0, df.IntervalSeries.Count);
        Assert.AreEqual(0, df.ThresholdSeries.Count);
    }

    /// <summary>
    /// Tests that the constructor initializes default property values.
    /// </summary>
    [TestMethod]
    public void Constructor_Empty_InitializesDefaultProperties()
    {
        var df = new BestFitDataFrame();

        Assert.AreEqual(0.0, df.PlottingParameter, "Default plotting parameter should be 0 (Weibull).");
        Assert.AreEqual(0, df.NumberOfLowOutliers);
        Assert.AreEqual(0.0, df.LowOutlierThreshold);
        Assert.AreEqual(1.0, df.Lambda, "Default Lambda should be 1.0.");
    }

    /// <summary>
    /// Tests that the XElement constructor restores a saved data frame.
    /// </summary>
    [TestMethod]
    public void Constructor_WithXElement_RestoresDataFrame()
    {
        var original = CreateMixedDataFrame();
        var xElement = original.ToXElement();

        var restored = new BestFitDataFrame(xElement);

        Assert.AreEqual(original.ExactSeries.Count, restored.ExactSeries.Count);
        Assert.AreEqual(original.UncertainSeries.Count, restored.UncertainSeries.Count);
        Assert.AreEqual(original.IntervalSeries.Count, restored.IntervalSeries.Count);
        Assert.AreEqual(original.ThresholdSeries.Count, restored.ThresholdSeries.Count);
    }

    #endregion

    #region ExactSeries Tests

    /// <summary>
    /// Tests that ExactSeries stores data correctly.
    /// </summary>
    [TestMethod]
    public void ExactSeries_SetAndGet_StoresDataCorrectly()
    {
        var df = new BestFitDataFrame();
        df.ExactSeries = new ExactSeries(SampleAnnualPeaks);

        Assert.AreEqual(SampleAnnualPeaks.Length, df.ExactSeries.Count);
        for (int i = 0; i < SampleAnnualPeaks.Length; i++)
        {
            Assert.AreEqual(SampleAnnualPeaks[i], df.ExactSeries[i].Value, 1e-10,
                $"Data value at index {i} not stored correctly.");
        }
    }

    /// <summary>
    /// Tests that replacing an exact series calculates Weibull plotting positions once the new
    /// observations are attached to the data frame.
    /// </summary>
    [TestMethod]
    public void ExactSeries_Replacement_CalculatesWeibullPlottingPositions()
    {
        var df = new BestFitDataFrame
        {
            ExactSeries = new ExactSeries([10d, 30d, 20d])
        };

        var positionsByValue = df.ExactSeries.ToDictionary(item => item.Value, item => item.PlottingPosition);
        Assert.AreEqual(0.75d, positionsByValue[10d], 1E-12d);
        Assert.AreEqual(0.25d, positionsByValue[30d], 1E-12d);
        Assert.AreEqual(0.50d, positionsByValue[20d], 1E-12d);
        Assert.IsTrue(df.ExactSeries.All(item => item.PlottingPosition > 0d && item.PlottingPosition < 1d));
    }

    /// <summary>
    /// Tests that ExactSeries handles small samples.
    /// </summary>
    [TestMethod]
    public void ExactSeries_SmallSample_StoresCorrectly()
    {
        var df = new BestFitDataFrame();
        var smallSample = new double[] { 45000, 52000, 61000, 49000, 55000 };
        df.ExactSeries = new ExactSeries(smallSample);

        Assert.AreEqual(5, df.ExactSeries.Count);
    }

    /// <summary>
    /// Tests that ExactSeries handles single value.
    /// </summary>
    [TestMethod]
    public void ExactSeries_SingleValue_StoresCorrectly()
    {
        var df = new BestFitDataFrame();
        df.ExactSeries = new ExactSeries([42000.0]);

        Assert.AreEqual(1, df.ExactSeries.Count);
        Assert.AreEqual(42000.0, df.ExactSeries[0].Value);
    }

    /// <summary>
    /// Tests that ExactSeries handles empty array.
    /// </summary>
    [TestMethod]
    public void ExactSeries_EmptyArray_StoresCorrectly()
    {
        var df = new BestFitDataFrame();
        df.ExactSeries = new ExactSeries(Array.Empty<double>());

        Assert.AreEqual(0, df.ExactSeries.Count);
    }

    /// <summary>
    /// Tests that ValuesToArray returns correct values.
    /// </summary>
    [TestMethod]
    public void ExactSeries_ValuesToArray_ReturnsCorrectValues()
    {
        var df = CreateTestDataFrame(10);

        var values = df.ExactSeries.ValuesToArray();

        Assert.AreEqual(10, values.Length);
        for (int i = 0; i < 10; i++)
        {
            Assert.AreEqual(SampleAnnualPeaks[i], values[i], 1e-10);
        }
    }

    #endregion

    #region UncertainSeries Tests

    /// <summary>
    /// Tests that UncertainSeries stores uncertain data correctly.
    /// </summary>
    [TestMethod]
    public void UncertainSeries_AddUncertainData_StoresCorrectly()
    {
        var df = new BestFitDataFrame();
        var uncertainData = new UncertainData(1889, new Normal(85000, 10000));

        df.UncertainSeries.Add(uncertainData);

        Assert.AreEqual(1, df.UncertainSeries.Count);
        Assert.AreEqual(85000, df.UncertainSeries[0].Value, 1e-10);
    }

    /// <summary>
    /// Tests that UncertainSeries handles multiple uncertain observations.
    /// </summary>
    [TestMethod]
    public void UncertainSeries_MultipleItems_StoresCorrectly()
    {
        var df = new BestFitDataFrame();
        df.UncertainSeries.Add(new UncertainData(1889, new Normal(85000, 10000)));
        df.UncertainSeries.Add(new UncertainData(1913, new Normal(75000, 8000)));
        df.UncertainSeries.Add(new UncertainData(1927, new Normal(90000, 12000)));

        Assert.AreEqual(3, df.UncertainSeries.Count);
    }

    /// <summary>
    /// Tests that UncertainSeries can use different distribution types.
    /// </summary>
    [TestMethod]
    public void UncertainSeries_DifferentDistributions_StoresCorrectly()
    {
        var df = new BestFitDataFrame();
        df.UncertainSeries.Add(new UncertainData(1889, new Normal(85000, 10000)));
        df.UncertainSeries.Add(new UncertainData(1913, new LogNormal(11.2, 0.3)));
        df.UncertainSeries.Add(new UncertainData(1927, new Triangular(70000, 90000, 110000)));

        Assert.AreEqual(3, df.UncertainSeries.Count);
        Assert.IsInstanceOfType(((UncertainData)df.UncertainSeries[0]).Distribution, typeof(Normal));
        Assert.IsInstanceOfType(((UncertainData)df.UncertainSeries[1]).Distribution, typeof(LogNormal));
        Assert.IsInstanceOfType(((UncertainData)df.UncertainSeries[2]).Distribution, typeof(Triangular));
    }

    #endregion

    #region IntervalSeries Tests

    /// <summary>
    /// Tests that IntervalSeries stores interval data correctly.
    /// </summary>
    [TestMethod]
    public void IntervalSeries_AddIntervalData_StoresCorrectly()
    {
        var df = new BestFitDataFrame();
        var intervalData = new IntervalData(1500, 60000, 80000, 100000);

        df.IntervalSeries.Add(intervalData);

        Assert.AreEqual(1, df.IntervalSeries.Count);
        var intervalItem = (IntervalData)df.IntervalSeries[0];
        Assert.AreEqual(60000, intervalItem.LowerValue);
        Assert.AreEqual(80000, intervalItem.Value);
        Assert.AreEqual(100000, intervalItem.UpperValue);
    }

    /// <summary>
    /// Tests that IntervalSeries handles multiple intervals.
    /// </summary>
    [TestMethod]
    public void IntervalSeries_MultipleItems_StoresCorrectly()
    {
        var df = new BestFitDataFrame();
        df.IntervalSeries.Add(new IntervalData(1500, 60000, 80000, 100000));
        df.IntervalSeries.Add(new IntervalData(1700, 50000, 70000, 90000));
        df.IntervalSeries.Add(new IntervalData(1800, 40000, 60000, 80000));

        Assert.AreEqual(3, df.IntervalSeries.Count);
    }

    #endregion

    #region BestFitThresholdSeries Tests

    /// <summary>
    /// Tests that BestFitThresholdSeries stores threshold data correctly.
    /// </summary>
    /// <remarks>
    /// Users only set <c>NumberAbove</c> on a <c>ThresholdData</c>. <c>NumberBelow</c>
    /// is a derived value — <c>DataFrame.ProcessThresholdSeries</c> (triggered by
    /// <c>DataFrame.ThresholdSeries</c> <c>Add</c>) computes it as
    /// <c>Duration − NumberAbove − (overlapping exact/interval/uncertain data)</c>.
    /// This test verifies the stored scalar fields (Value, Start/EndIndex, NumberAbove)
    /// and the automatic derivation of NumberBelow.
    /// </remarks>
    [TestMethod]
    public void ThresholdSeries_AddThresholdData_StoresCorrectly()
    {
        var df = new BestFitDataFrame();
        // User sets only NumberAbove (5 historical exceedances out of 71 years).
        var thresholdData = new BestFitThresholdData(1850, 1920, 40000) { NumberAbove = 5 };

        df.ThresholdSeries.Add(thresholdData);

        Assert.AreEqual(1, df.ThresholdSeries.Count);
        var stored = (BestFitThresholdData)df.ThresholdSeries[0];
        Assert.AreEqual(1850, stored.StartIndex);
        Assert.AreEqual(1920, stored.EndIndex);
        Assert.AreEqual(40000, stored.Value);
        Assert.AreEqual(5, stored.NumberAbove, "NumberAbove is user-set and preserved.");
        // Duration = 1920 - 1850 + 1 = 71. With no overlapping explicit data,
        // NumberBelow = Duration - NumberAbove = 71 - 5 = 66.
        Assert.AreEqual(66, stored.NumberBelow,
            "NumberBelow is derived by DataFrame as Duration - NumberAbove - overlapping data.");
    }

    /// <summary>
    /// Tests that BestFitThresholdSeries handles multiple thresholds.
    /// </summary>
    [TestMethod]
    public void ThresholdSeries_MultipleThresholds_StoresCorrectly()
    {
        var df = new BestFitDataFrame();
        df.ThresholdSeries.Add(new BestFitThresholdData(1800, 1850, 30000) { NumberAbove = 3 });
        df.ThresholdSeries.Add(new BestFitThresholdData(1851, 1900, 40000) { NumberAbove = 2 });
        df.ThresholdSeries.Add(new BestFitThresholdData(1901, 1950, 50000) { NumberAbove = 1 });

        Assert.AreEqual(3, df.ThresholdSeries.Count);
    }

    /// <summary>
    /// Tests that BestFitThresholdSeries handles above threshold counts.
    /// </summary>
    [TestMethod]
    public void ThresholdSeries_NumberAbove_StoresCorrectly()
    {
        var df = new BestFitDataFrame();
        var threshold = new BestFitThresholdData(1800, 1900, 100000) { NumberAbove = 5 };
        df.ThresholdSeries.Add(threshold);

        Assert.AreEqual(5, ((BestFitThresholdData)df.ThresholdSeries[0]).NumberAbove);
    }

    #endregion

    #region Combined Data Tests

    /// <summary>
    /// Tests that BestFitDataFrame handles combined exact and threshold data.
    /// </summary>
    [TestMethod]
    public void DataFrame_CombinedExactAndThreshold_StoresCorrectly()
    {
        var df = CreateTestDataFrame(20);
        // NumberBelow is auto-derived by BestFitDataFrame; user sets only NumberAbove.
        var threshold = new BestFitThresholdData(1850, 1920, 35000) { NumberAbove = 3 };
        df.ThresholdSeries.Add(threshold);

        Assert.AreEqual(20, df.ExactSeries.Count);
        Assert.AreEqual(1, df.ThresholdSeries.Count);
    }

    /// <summary>
    /// Tests that BestFitDataFrame handles all data types together.
    /// </summary>
    [TestMethod]
    public void DataFrame_AllDataTypes_StoresCorrectly()
    {
        var df = CreateMixedDataFrame();

        Assert.IsTrue(df.ExactSeries.Count > 0);
        Assert.IsTrue(df.UncertainSeries.Count > 0);
        Assert.IsTrue(df.IntervalSeries.Count > 0);
        Assert.IsTrue(df.ThresholdSeries.Count > 0);
    }

    #endregion

    #region Property Change Tests

    /// <summary>
    /// Tests that PlottingParameter change raises PropertyChanged.
    /// </summary>
    [TestMethod]
    public void PlottingParameter_Change_RaisesPropertyChanged()
    {
        var df = CreateTestDataFrame();
        bool propertyChanged = false;
        df.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(BestFitDataFrame.PlottingParameter))
                propertyChanged = true;
        };

        df.PlottingParameter = 0.44;  // Gringorten

        Assert.IsTrue(propertyChanged, "PropertyChanged should be raised for PlottingParameter.");
    }

    /// <summary>
    /// Tests that setting same PlottingParameter value does not raise PropertyChanged.
    /// </summary>
    [TestMethod]
    public void PlottingParameter_SetSameValue_DoesNotRaisePropertyChanged()
    {
        var df = CreateTestDataFrame();
        df.PlottingParameter = 0.44;
        bool propertyChanged = false;
        df.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(BestFitDataFrame.PlottingParameter))
                propertyChanged = true;
        };

        df.PlottingParameter = 0.44;  // Same value

        Assert.IsFalse(propertyChanged, "PropertyChanged should not be raised for same value.");
    }

    /// <summary>
    /// Tests that LowOutlierThreshold change raises PropertyChanged.
    /// </summary>
    [TestMethod]
    public void LowOutlierThreshold_Change_RaisesPropertyChanged()
    {
        var df = CreateTestDataFrame();
        bool propertyChanged = false;
        df.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(BestFitDataFrame.LowOutlierThreshold))
                propertyChanged = true;
        };

        df.LowOutlierThreshold = 10000.0;

        Assert.IsTrue(propertyChanged, "PropertyChanged should be raised for LowOutlierThreshold.");
    }

    /// <summary>
    /// Tests that ExactSeries change raises PropertyChanged.
    /// </summary>
    [TestMethod]
    public void ExactSeries_Change_RaisesPropertyChanged()
    {
        var df = new BestFitDataFrame();
        bool propertyChanged = false;
        df.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(BestFitDataFrame.ExactSeries))
                propertyChanged = true;
        };

        df.ExactSeries = new ExactSeries(SampleAnnualPeaks);

        Assert.IsTrue(propertyChanged, "PropertyChanged should be raised for ExactSeries.");
    }

    /// <summary>
    /// Tests that adding to ExactSeries raises PropertyChanged.
    /// </summary>
    [TestMethod]
    public void ExactSeries_AddItem_RaisesPropertyChanged()
    {
        var df = CreateTestDataFrame();
        bool propertyChanged = false;
        df.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(BestFitDataFrame.ExactSeries))
                propertyChanged = true;
        };

        df.ExactSeries.Add(new ExactData(2024, 75000));

        Assert.IsTrue(propertyChanged, "PropertyChanged should be raised when adding to ExactSeries.");
    }

    /// <summary>
    /// Tests that adding to UncertainSeries raises PropertyChanged.
    /// </summary>
    [TestMethod]
    public void UncertainSeries_AddItem_RaisesPropertyChanged()
    {
        var df = new BestFitDataFrame();
        bool propertyChanged = false;
        df.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(BestFitDataFrame.UncertainSeries))
                propertyChanged = true;
        };

        df.UncertainSeries.Add(new UncertainData(1889, new Normal(85000, 10000)));

        Assert.IsTrue(propertyChanged, "PropertyChanged should be raised when adding to UncertainSeries.");
    }

    /// <summary>
    /// Tests that adding to IntervalSeries raises PropertyChanged.
    /// </summary>
    [TestMethod]
    public void IntervalSeries_AddItem_RaisesPropertyChanged()
    {
        var df = new BestFitDataFrame();
        bool propertyChanged = false;
        df.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(BestFitDataFrame.IntervalSeries))
                propertyChanged = true;
        };

        df.IntervalSeries.Add(new IntervalData(1500, 60000, 80000, 100000));

        Assert.IsTrue(propertyChanged, "PropertyChanged should be raised when adding to IntervalSeries.");
    }

    /// <summary>
    /// Tests that adding to BestFitThresholdSeries raises PropertyChanged.
    /// </summary>
    [TestMethod]
    public void ThresholdSeries_AddItem_RaisesPropertyChanged()
    {
        var df = new BestFitDataFrame();
        bool propertyChanged = false;
        df.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(BestFitDataFrame.ThresholdSeries))
                propertyChanged = true;
        };

        df.ThresholdSeries.Add(new BestFitThresholdData(1850, 1920, 40000));

        Assert.IsTrue(propertyChanged, "PropertyChanged should be raised when adding to ThresholdSeries.");
    }

    #endregion

    #region Lambda Tests

    /// <summary>
    /// Tests that Lambda is calculated correctly for exact data.
    /// </summary>
    [TestMethod]
    public void Lambda_ExactDataOnly_CalculatedCorrectly()
    {
        var df = CreateTestDataFrame(30);

        // Lambda should be 1 for annual maxima (one event per year)
        Assert.AreEqual(1.0, df.Lambda, 0.01, "Lambda should be 1.0 for annual maxima.");
    }

    #endregion

    #region TotalRecordLength Tests

    /// <summary>
    /// Tests that TotalRecordLength returns correct value for exact data only.
    /// </summary>
    [TestMethod]
    public void TotalRecordLength_ExactDataOnly_ReturnsCorrectValue()
    {
        var df = CreateTestDataFrame(30);

        int totalLength = df.TotalRecordLength();

        Assert.AreEqual(30, totalLength);
    }

    /// <summary>
    /// Tests that TotalRecordLength includes threshold data.
    /// </summary>
    [TestMethod]
    public void TotalRecordLength_WithThresholdData_ReturnsCorrectValue()
    {
        var df = CreateTestDataFrame(30);
        // NumberBelow is auto-derived by BestFitDataFrame; user sets only NumberAbove.
        var threshold = new BestFitThresholdData(1850, 1920, 40000) { NumberAbove = 3 };
        df.ThresholdSeries.Add(threshold);

        int totalLength = df.TotalRecordLength();

        // Should include both exact data and threshold period
        Assert.IsTrue(totalLength > 30);
    }

    #endregion

    #region Plotting Position Tests

    /// <summary>
    /// Tests that different plotting parameters produce different positions.
    /// </summary>
    [TestMethod]
    public void PlottingParameter_DifferentValues_ProduceDifferentPositions()
    {
        var df1 = CreateTestDataFrame(10);
        df1.PlottingParameter = 0.0;  // Weibull

        var df2 = CreateTestDataFrame(10);
        df2.PlottingParameter = 0.44;  // Gringorten

        // Get plotting positions for the largest value
        var sortedValues1 = df1.ExactSeries.OrderByDescending(x => x.Value).ToList();
        var sortedValues2 = df2.ExactSeries.OrderByDescending(x => x.Value).ToList();

        Assert.AreNotEqual(sortedValues1[0].PlottingPosition, sortedValues2[0].PlottingPosition,
            "Different plotting parameters should produce different positions.");
    }

    /// <summary>
    /// Tests that Weibull plotting positions are calculated correctly.
    /// </summary>
    [TestMethod]
    public void PlottingParameter_Weibull_CalculatesCorrectly()
    {
        var df = CreateTestDataFrame(10);

        // Weibull formula: p = i / (n + 1) where i is rank in descending order
        // of non-exceedance. For n=10 exact values, the largest value (rank 1 descending)
        // has p = 1/11 ≈ 0.0909 (smallest plotting position).
        var sortedByValue = df.ExactSeries.OrderByDescending(x => x.Value).ToList();
        double expectedLowestPP = 1.0 / 11.0;

        Assert.AreEqual(expectedLowestPP, sortedByValue[0].PlottingPosition, 0.01,
            "Weibull plotting position for the largest value should be i/(n+1) = 1/11.");
    }

    #endregion

    #region Serialization Tests

    /// <summary>
    /// Tests that ToXElement creates valid XML.
    /// </summary>
    [TestMethod]
    public void ToXElement_CreatesValidXml()
    {
        var df = CreateTestDataFrame(10);

        var xElement = df.ToXElement();

        Assert.IsNotNull(xElement);
        Assert.AreEqual("DataFrame", xElement.Name.LocalName);
    }

    /// <summary>
    /// Tests that XML round-trip preserves exact data.
    /// </summary>
    [TestMethod]
    public void XmlRoundTrip_PreservesExactData()
    {
        var original = CreateTestDataFrame(15);

        var xElement = original.ToXElement();
        var restored = new BestFitDataFrame(xElement);

        var originalValues = original.ExactSeries.ValuesToArray();
        var restoredValues = restored.ExactSeries.ValuesToArray();

        Assert.AreEqual(originalValues.Length, restoredValues.Length);
        for (int i = 0; i < originalValues.Length; i++)
        {
            Assert.AreEqual(originalValues[i], restoredValues[i], 1e-10);
        }
    }

    /// <summary>
    /// Tests that XML construction preserves persisted plotting positions instead of recalculating
    /// them during the four data-series replacements.
    /// </summary>
    [TestMethod]
    public void XmlRoundTrip_PreservesCustomPlottingPositionsWithoutRecalculation()
    {
        var original = new BestFitDataFrame
        {
            ExactSeries = new ExactSeries([10d, 20d, 30d])
        };
        double[] persistedPositions = [0.17d, 0.43d, 0.81d];
        for (int i = 0; i < persistedPositions.Length; i++)
            original.ExactSeries[i].PlottingPosition = persistedPositions[i];

        var restored = new BestFitDataFrame(original.ToXElement());

        Assert.AreEqual(persistedPositions.Length, restored.ExactSeries.Count);
        for (int i = 0; i < persistedPositions.Length; i++)
        {
            Assert.AreEqual(
                persistedPositions[i],
                restored.ExactSeries[i].PlottingPosition,
                1E-15d,
                $"Serialized plotting position {i} was recalculated during construction.");
        }
    }

    /// <summary>
    /// Tests that XML round-trip preserves uncertain data.
    /// </summary>
    [TestMethod]
    public void XmlRoundTrip_PreservesUncertainData()
    {
        var original = new BestFitDataFrame();
        original.UncertainSeries.Add(new UncertainData(1889, new Normal(85000, 10000)));
        original.UncertainSeries.Add(new UncertainData(1913, new Normal(75000, 8000)));

        var xElement = original.ToXElement();
        var restored = new BestFitDataFrame(xElement);

        Assert.AreEqual(2, restored.UncertainSeries.Count);
        Assert.AreEqual(85000, restored.UncertainSeries[0].Value, 100);
        Assert.AreEqual(75000, restored.UncertainSeries[1].Value, 100);
    }

    /// <summary>
    /// Tests that XML round-trip preserves interval data.
    /// </summary>
    [TestMethod]
    public void XmlRoundTrip_PreservesIntervalData()
    {
        var original = new BestFitDataFrame();
        original.IntervalSeries.Add(new IntervalData(1500, 60000, 80000, 100000));

        var xElement = original.ToXElement();
        var restored = new BestFitDataFrame(xElement);

        Assert.AreEqual(1, restored.IntervalSeries.Count);
        var restoredInterval = (IntervalData)restored.IntervalSeries[0];
        Assert.AreEqual(60000, restoredInterval.LowerValue);
        Assert.AreEqual(100000, restoredInterval.UpperValue);
    }

    /// <summary>
    /// Tests that XML round-trip preserves threshold data.
    /// </summary>
    /// <remarks>
    /// Users set <c>NumberAbove</c>; <c>NumberBelow</c> is auto-derived by BestFitDataFrame on Add.
    /// After serialization/deserialization, all stored fields (Value, Start/EndIndex,
    /// NumberAbove, NumberBelow) round-trip faithfully via the XML attributes.
    /// </remarks>
    [TestMethod]
    public void XmlRoundTrip_PreservesThresholdData()
    {
        var original = new BestFitDataFrame();
        var threshold = new BestFitThresholdData(1850, 1920, 40000) { NumberAbove = 5 };
        original.ThresholdSeries.Add(threshold);
        // BestFitDataFrame.Add triggered derivation of NumberBelow = 71 - 5 = 66.
        var originalThreshold = (BestFitThresholdData)original.ThresholdSeries[0];
        int expectedNumberBelow = originalThreshold.NumberBelow;

        var xElement = original.ToXElement();
        var restored = new BestFitDataFrame(xElement);

        Assert.AreEqual(1, restored.ThresholdSeries.Count);
        var restoredThreshold = (BestFitThresholdData)restored.ThresholdSeries[0];
        Assert.AreEqual(40000, restoredThreshold.Value);
        Assert.AreEqual(5, restoredThreshold.NumberAbove);
        Assert.AreEqual(expectedNumberBelow, restoredThreshold.NumberBelow,
            "Both NumberAbove (user-set) and NumberBelow (derived before save) must round-trip.");
    }

    /// <summary>
    /// Tests that XML round-trip preserves all data types.
    /// </summary>
    [TestMethod]
    public void XmlRoundTrip_PreservesAllDataTypes()
    {
        var original = CreateMixedDataFrame();

        var xElement = original.ToXElement();
        var restored = new BestFitDataFrame(xElement);

        Assert.AreEqual(original.ExactSeries.Count, restored.ExactSeries.Count);
        Assert.AreEqual(original.UncertainSeries.Count, restored.UncertainSeries.Count);
        Assert.AreEqual(original.IntervalSeries.Count, restored.IntervalSeries.Count);
        Assert.AreEqual(original.ThresholdSeries.Count, restored.ThresholdSeries.Count);
    }

    /// <summary>
    /// Tests that XML round-trip preserves PlottingParameter.
    /// </summary>
    [TestMethod]
    public void XmlRoundTrip_PreservesPlottingParameter()
    {
        var original = CreateTestDataFrame();
        original.PlottingParameter = 0.44;

        var xElement = original.ToXElement();
        var restored = new BestFitDataFrame(xElement);

        Assert.AreEqual(0.44, restored.PlottingParameter, 1e-10);
    }

    /// <summary>
    /// Tests that XML round-trip preserves LowOutlierThreshold.
    /// </summary>
    [TestMethod]
    public void XmlRoundTrip_PreservesLowOutlierThreshold()
    {
        var original = CreateTestDataFrame();
        original.LowOutlierThreshold = 15000.0;

        var xElement = original.ToXElement();
        var restored = new BestFitDataFrame(xElement);

        Assert.AreEqual(15000.0, restored.LowOutlierThreshold, 1e-10);
    }

    #endregion

    #region Edge Cases

    /// <summary>
    /// Tests that BestFitDataFrame handles negative values.
    /// </summary>
    [TestMethod]
    public void DataFrame_WithNegativeValues_HandlesCorrectly()
    {
        var df = new BestFitDataFrame();
        var dataWithNegatives = new double[] { -10, -5, 0, 5, 10, 15, 20, 25, 30, 35 };
        df.ExactSeries = new ExactSeries(dataWithNegatives);

        var values = df.ExactSeries.ValuesToArray();
        Assert.IsTrue(values.Any(v => v < 0), "Should contain negative values.");
    }

    /// <summary>
    /// Tests that BestFitDataFrame handles large datasets.
    /// </summary>
    [TestMethod]
    public void DataFrame_LargeDataset_HandlesCorrectly()
    {
        var df = new BestFitDataFrame();
        var largeData = Enumerable.Range(1, 1000).Select(i => (double)(30000 + i * 100)).ToArray();
        df.ExactSeries = new ExactSeries(largeData);

        Assert.AreEqual(1000, df.ExactSeries.Count);
    }

    /// <summary>
    /// Tests that BestFitDataFrame handles special double values.
    /// </summary>
    [TestMethod]
    public void DataFrame_SpecialDoubleValues_HandlesCorrectly()
    {
        var df = new BestFitDataFrame();
        double[] dataWithSpecialValues = [1.0, 2.0, double.NaN, 4.0, double.PositiveInfinity];
        df.ExactSeries = new ExactSeries(dataWithSpecialValues);

        Assert.AreEqual(5, df.ExactSeries.Count);
        var values = df.ExactSeries.ValuesToArray();
        Assert.IsTrue(double.IsNaN(values[2]));
    }

    /// <summary>
    /// Tests that BestFitDataFrame handles very small values.
    /// </summary>
    [TestMethod]
    public void DataFrame_VerySmallValues_HandlesCorrectly()
    {
        var df = new BestFitDataFrame();
        var smallData = new double[] { 1e-10, 1e-9, 1e-8, 1e-7, 1e-6, 1e-5, 1e-4, 1e-3, 1e-2, 1e-1 };
        df.ExactSeries = new ExactSeries(smallData);

        Assert.AreEqual(10, df.ExactSeries.Count);
        Assert.AreEqual(1e-10, df.ExactSeries[0].Value, 1e-15);
    }

    /// <summary>
    /// Tests that BestFitDataFrame handles very large values.
    /// </summary>
    [TestMethod]
    public void DataFrame_VeryLargeValues_HandlesCorrectly()
    {
        var df = new BestFitDataFrame();
        var largeData = new double[] { 1e10, 1e11, 1e12, 1e13, 1e14, 1e15, 1e16, 1e17, 1e18, 1e19 };
        df.ExactSeries = new ExactSeries(largeData);

        Assert.AreEqual(10, df.ExactSeries.Count);
        Assert.AreEqual(1e19, df.ExactSeries[9].Value, 1e14);
    }

    /// <summary>
    /// Tests that BestFitDataFrame handles identical values.
    /// </summary>
    [TestMethod]
    public void DataFrame_IdenticalValues_HandlesCorrectly()
    {
        var df = new BestFitDataFrame();
        var identicalData = Enumerable.Repeat(50000.0, 20).ToArray();
        df.ExactSeries = new ExactSeries(identicalData);

        Assert.AreEqual(20, df.ExactSeries.Count);
        Assert.IsTrue(df.ExactSeries.All(x => x.Value == 50000.0));
    }

    #endregion

    #region Integration Tests

    /// <summary>
    /// Tests that creating a BestFitDataFrame with various plotting parameters works.
    /// </summary>
    [TestMethod]
    [DataRow(0.0, "Weibull")]
    [DataRow(0.40, "Cunnane")]
    [DataRow(0.44, "Gringorten")]
    [DataRow(0.50, "Hazen")]
    public void DataFrame_VariousPlottingParameters_WorksCorrectly(double param, string name)
    {
        var df = CreateTestDataFrame();
        df.PlottingParameter = param;

        Assert.AreEqual(param, df.PlottingParameter, $"PlottingParameter for {name} not set correctly.");
    }

    /// <summary>
    /// Tests that clearing and rebuilding series works correctly.
    /// </summary>
    [TestMethod]
    public void DataFrame_ClearAndRebuild_WorksCorrectly()
    {
        var df = CreateMixedDataFrame();

        // Clear exact series
        df.ExactSeries.Clear();
        Assert.AreEqual(0, df.ExactSeries.Count);

        // Add new data
        df.ExactSeries = new ExactSeries([100, 200, 300, 400, 500, 600, 700, 800, 900, 1000]);
        Assert.AreEqual(10, df.ExactSeries.Count);
    }

    /// <summary>
    /// Tests that modifying data item raises property changed when items are added via Add().
    /// </summary>
    /// <remarks>
    /// PropertyChanged propagation only works when items are added via Add() or deserialized
    /// from XML. Direct assignment of a new series does not hook up item-level handlers.
    /// </remarks>
    [TestMethod]
    public void DataFrame_ModifyDataItem_RaisesPropertyChanged()
    {
        // Create BestFitDataFrame and add items via Add() to properly hook up PropertyChanged handlers
        var df = new BestFitDataFrame();
        foreach (var value in SampleAnnualPeaks.Take(10))
        {
            df.ExactSeries.Add(new ExactData(df.ExactSeries.Count, value));
        }

        bool propertyChanged = false;
        df.PropertyChanged += (s, e) => propertyChanged = true;

        // Modify an individual data item
        df.ExactSeries[0].Value = 99999;

        Assert.IsTrue(propertyChanged, "PropertyChanged should be raised when data item is modified.");
    }

    #endregion

    #region Flood Frequency Analysis Scenarios

    /// <summary>
    /// Tests a typical systematic record scenario.
    /// </summary>
    [TestMethod]
    public void Scenario_SystematicRecord_WorksCorrectly()
    {
        // 50 years of annual peak flow data
        var df = new BestFitDataFrame();
        var data = Enumerable.Range(1970, 50).Select(year =>
            new ExactData(year, 30000 + new Random(year).NextDouble() * 40000)).ToList();

        foreach (var d in data)
            df.ExactSeries.Add(d);

        Assert.AreEqual(50, df.ExactSeries.Count);
        var (isValid, _) = df.Validate();
        Assert.IsTrue(isValid);
    }

    /// <summary>
    /// Tests a systematic record with historical flood scenario.
    /// </summary>
    [TestMethod]
    public void Scenario_SystematicWithHistorical_WorksCorrectly()
    {
        var df = CreateTestDataFrame(30);

        // Add historical flood with uncertainty
        df.UncertainSeries.Add(new UncertainData(1889, new Normal(95000, 15000)));

        // Add historical threshold
        var threshold = new BestFitThresholdData(1850, 1920, 80000);
        df.ThresholdSeries.Add(threshold);

        Assert.AreEqual(30, df.ExactSeries.Count);
        Assert.AreEqual(1, df.UncertainSeries.Count);
        Assert.AreEqual(1, df.ThresholdSeries.Count);
    }

    /// <summary>
    /// Tests a paleoflood scenario with interval-censored data.
    /// </summary>
    [TestMethod]
    public void Scenario_Paleoflood_WorksCorrectly()
    {
        var df = CreateTestDataFrame(30);

        // Add paleoflood estimates (interval-censored)
        df.IntervalSeries.Add(new IntervalData(1200, 80000, 100000, 120000));
        df.IntervalSeries.Add(new IntervalData(1450, 90000, 110000, 130000));
        df.IntervalSeries.Add(new IntervalData(1650, 70000, 90000, 110000));

        // Add non-exceedance bound
        var threshold = new BestFitThresholdData(1000, 2000, 150000);
        df.ThresholdSeries.Add(threshold);

        Assert.AreEqual(30, df.ExactSeries.Count);
        Assert.AreEqual(3, df.IntervalSeries.Count);
        Assert.AreEqual(1, df.ThresholdSeries.Count);
    }

    #region Threshold Processing Regression

    /// <summary>
    /// Verifies repeated processing remains idempotent and an explicit-data mutation recomputes
    /// effective counts from the original user input with one aggregate threshold notification.
    /// </summary>
    [TestMethod]
    public void ProcessThresholdSeries_RepeatedAndInputMutated_RemainsIdempotent()
    {
        var frame = new BestFitDataFrame();
        var threshold = new BestFitThresholdData(0, 2, 100.0) { NumberAbove = 2 };
        frame.ThresholdSeries.Add(threshold);
        var exact = new ExactData { Index = 1, Value = 150.0 };
        frame.ExactSeries.Add(exact);
        frame.ProcessThresholdSeries();

        Assert.AreEqual(2, threshold.SourceNumberAbove);
        Assert.AreEqual(0, threshold.NumberAbove);
        Assert.AreEqual(0, threshold.NumberBelow);

        for (int i = 0; i < 20; i++)
            frame.ProcessThresholdSeries();

        Assert.AreEqual(0, threshold.NumberAbove);
        Assert.AreEqual(0, threshold.NumberBelow);

        int thresholdNotifications = 0;
        frame.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(BestFitDataFrame.ThresholdSeries))
                thresholdNotifications++;
        };

        frame.ExactSeries.Remove(exact);

        Assert.AreEqual(2, threshold.SourceNumberAbove);
        Assert.AreEqual(2, threshold.NumberAbove);
        Assert.AreEqual(1, threshold.NumberBelow);
        Assert.AreEqual(1, thresholdNotifications);

        frame.ProcessThresholdSeries();
        Assert.AreEqual(1, thresholdNotifications,
            "An unchanged pass must not emit another aggregate threshold notification.");
    }

    /// <summary>
    /// Verifies a DataFrame XML round-trip rebuilds effective threshold counts from the source
    /// NumberAbove value stored under the existing schema.
    /// </summary>
    [TestMethod]
    public void XmlRoundTrip_ProcessedThreshold_RebuildsEffectiveCountsFromSource()
    {
        var frame = new BestFitDataFrame();
        var threshold = new BestFitThresholdData(0, 2, 100.0) { NumberAbove = 2 };
        frame.ThresholdSeries.Add(threshold);
        frame.ExactSeries.Add(new ExactData { Index = 1, Value = 150.0 });
        frame.ProcessThresholdSeries();

        var restoredFrame = new BestFitDataFrame(frame.ToXElement());
        var restored = (BestFitThresholdData)restoredFrame.ThresholdSeries[0];

        Assert.AreEqual(2, restored.SourceNumberAbove);
        Assert.AreEqual(0, restored.NumberAbove);
        Assert.AreEqual(0, restored.NumberBelow);
    }

    #endregion

    #endregion
}
