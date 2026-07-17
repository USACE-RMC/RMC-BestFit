using Microsoft.VisualStudio.TestTools.UnitTesting;
using Numerics.Data;
using RMC.BestFit.UI;
using System;

namespace RMC.BestFit.UI.Tests.Elements.TimeSeriesData;

/// <summary>
/// Unit tests for <see cref="TimeSeriesElement.BuildFromDssTimeSeries"/>, the helper that
/// converts a <see cref="Hec.Dss.TimeSeries"/> into a <see cref="TimeSeries"/> while
/// substituting <see cref="double.NaN"/> for DSS missing-value sentinels.
/// </summary>
/// <remarks>
/// Tests cover the four sentinel constants recognized by <c>Hec.Dss.DssReader.IsValid</c>
/// (-901.0, -902.0, -3.402823466e+38, -3.4028234663852886E+38), regular DSS padding and
/// omitted timestep normalization, irregular timestamp preservation, DSS E-part interval
/// resolution, and defensive checks for malformed DSS arrays. The integration test
/// <c>BuildFromDss_HasMissingValuesFiresAfterImport</c> exercises the warning pipeline
/// end-to-end through <see cref="TimeSeriesElement.SetIsValid"/>.
/// </remarks>
[TestClass]
public class TimeSeriesElementDssImportTests
{
    /// <summary>
    /// Creates dss Series.
    /// </summary>
    /// <param name="values">The input values.</param>
    /// <param name="times">The times value.</param>
    /// <param name="path">The DSS path assigned to the series.</param>
    /// <param name="dataType">The DSS data type assigned to the series.</param>
    /// <returns>The created test object.</returns>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static Hec.Dss.TimeSeries MakeDssSeries(
        double[] values,
        DateTime[] times,
        string path = "/A/B/C//1Day/F/",
        string dataType = "")
    {
        var dssTs = new Hec.Dss.TimeSeries
        {
            Values = values,
            Times = times,
            Path = new Hec.Dss.DssPath(path),
            DataType = dataType,
        };
        return dssTs;
    }

    /// <summary>
    /// Verifies that DSS -901 missing values are converted to NaN.
    /// </summary>
    [TestMethod]
    public void BuildFromDss_ConvertsMinus901ToNaN()
    {
        var t0 = new DateTime(2020, 1, 1);
        var dssTs = MakeDssSeries(
            values: new[] { 1.0, -901.0, 3.0 },
            times: new[] { t0, t0.AddDays(1), t0.AddDays(2) });

        var ts = TimeSeriesElement.BuildFromDssTimeSeries(dssTs, TimeInterval.OneDay);

        Assert.AreEqual(3, ts.Count);
        Assert.AreEqual(1.0, ts[0].Value, 1e-12);
        Assert.IsTrue(double.IsNaN(ts[1].Value), "MISSING_VALUE (-901.0) must be converted to NaN.");
        Assert.AreEqual(3.0, ts[2].Value, 1e-12);
    }

    /// <summary>
    /// Verifies that DSS -902 missing records are converted to NaN.
    /// </summary>
    [TestMethod]
    public void BuildFromDss_ConvertsMinus902ToNaN()
    {
        var t0 = new DateTime(2020, 1, 1);
        var dssTs = MakeDssSeries(
            values: new[] { 1.0, -902.0, 3.0 },
            times: new[] { t0, t0.AddDays(1), t0.AddDays(2) });

        var ts = TimeSeriesElement.BuildFromDssTimeSeries(dssTs, TimeInterval.OneDay);

        Assert.AreEqual(3, ts.Count);
        Assert.AreEqual(1.0, ts[0].Value, 1e-12);
        Assert.IsTrue(double.IsNaN(ts[1].Value), "MISSING_RECORD (-902.0) must be converted to NaN.");
        Assert.AreEqual(3.0, ts[2].Value, 1e-12);
    }

    /// <summary>
    /// Verifies that the DSS undefined-double sentinel is converted to NaN.
    /// </summary>
    [TestMethod]
    public void BuildFromDss_ConvertsUndefinedDoubleToNaN()
    {
        var t0 = new DateTime(2020, 1, 1);
        var dssTs = MakeDssSeries(
            values: new[] { 1.0, -3.402823466e+38, 3.0 },
            times: new[] { t0, t0.AddDays(1), t0.AddDays(2) });

        var ts = TimeSeriesElement.BuildFromDssTimeSeries(dssTs, TimeInterval.OneDay);

        Assert.AreEqual(3, ts.Count);
        Assert.AreEqual(1.0, ts[0].Value, 1e-12);
        Assert.IsTrue(double.IsNaN(ts[1].Value), "UNDEFINED_DOUBLE (-3.402823466e+38) must be converted to NaN.");
        Assert.AreEqual(3.0, ts[2].Value, 1e-12);
    }

    /// <summary>
    /// Verifies that the alternate DSS undefined-double sentinel is converted to NaN.
    /// </summary>
    [TestMethod]
    public void BuildFromDss_ConvertsUndefinedDouble2ToNaN()
    {
        var t0 = new DateTime(2020, 1, 1);
        var dssTs = MakeDssSeries(
            values: new[] { 1.0, -3.4028234663852886E+38, 3.0 },
            times: new[] { t0, t0.AddDays(1), t0.AddDays(2) });

        var ts = TimeSeriesElement.BuildFromDssTimeSeries(dssTs, TimeInterval.OneDay);

        Assert.AreEqual(3, ts.Count);
        Assert.AreEqual(1.0, ts[0].Value, 1e-12);
        Assert.IsTrue(double.IsNaN(ts[1].Value), "UNDEFINED_DOUBLE_2 (-3.4028234663852886E+38) must be converted to NaN.");
        Assert.AreEqual(3.0, ts[2].Value, 1e-12);
    }

    /// <summary>
    /// Verifies that valid zero values are preserved.
    /// </summary>
    [TestMethod]
    public void BuildFromDss_PreservesValidZero()
    {
        var t0 = new DateTime(2020, 1, 1);
        var dssTs = MakeDssSeries(
            values: new[] { 0.0, 1.0, 0.0 },
            times: new[] { t0, t0.AddDays(1), t0.AddDays(2) });

        var ts = TimeSeriesElement.BuildFromDssTimeSeries(dssTs, TimeInterval.OneDay);

        Assert.AreEqual(3, ts.Count);
        Assert.AreEqual(0.0, ts[0].Value, 1e-12);
        Assert.AreEqual(1.0, ts[1].Value, 1e-12);
        Assert.AreEqual(0.0, ts[2].Value, 1e-12);
        Assert.IsFalse(double.IsNaN(ts[0].Value), "Valid zero must NOT be converted to NaN.");
        Assert.IsFalse(double.IsNaN(ts[2].Value), "Valid zero must NOT be converted to NaN.");
    }

    /// <summary>
    /// Verifies that irregular DSS intervals are preserved.
    /// </summary>
    [TestMethod]
    public void BuildFromDss_PreservesIrregularInterval()
    {
        var t0 = new DateTime(2020, 1, 1);
        var dssTs = MakeDssSeries(
            values: new[] { 10.0, 20.0, 30.0, 40.0, 50.0 },
            times: new[]
            {
                t0,
                t0.AddDays(3),
                t0.AddDays(7),
                t0.AddDays(15),
                t0.AddDays(40),
            });

        var ts = TimeSeriesElement.BuildFromDssTimeSeries(dssTs, TimeInterval.Irregular);

        Assert.AreEqual(TimeInterval.Irregular, ts.TimeInterval,
            "Irregular interval must be preserved exactly.");
        Assert.AreEqual(5, ts.Count);
        Assert.AreEqual(t0, ts[0].Index);
        Assert.AreEqual(t0.AddDays(3), ts[1].Index);
        Assert.AreEqual(t0.AddDays(40), ts[4].Index);
        Assert.AreEqual(50.0, ts[4].Value, 1e-12);
    }

    /// <summary>
    /// Verifies that regular DSS intervals are preserved.
    /// </summary>
    [TestMethod]
    public void BuildFromDss_PreservesRegularInterval()
    {
        var t0 = new DateTime(2020, 1, 1);
        var values = new double[10];
        var times = new DateTime[10];
        for (int i = 0; i < 10; i++)
        {
            values[i] = i + 1.0;
            times[i] = t0.AddDays(i);
        }
        var dssTs = MakeDssSeries(values, times);

        var ts = TimeSeriesElement.BuildFromDssTimeSeries(dssTs, TimeInterval.OneDay);

        Assert.AreEqual(TimeInterval.OneDay, ts.TimeInterval);
        Assert.AreEqual(10, ts.Count);
        for (int i = 0; i < 10; i++)
        {
            Assert.AreEqual(t0.AddDays(i), ts[i].Index);
            Assert.AreEqual(i + 1.0, ts[i].Value, 1e-12);
        }
    }

    /// <summary>
    /// Verifies that the reported DSS midnight boundary is converted to the represented day.
    /// </summary>
    /// <remarks>
    /// HEC-DSS renders .NET midnight as 2400 on the preceding date. This test recreates the
    /// reviewer example and confirms that period-average import uses beginning-of-period indexing.
    /// </remarks>
    [TestMethod]
    public void BuildFromDss_PeriodAverageDaily_Converts2400ToRepresentedDay()
    {
        var returnedMidnight = new DateTime(1905, 10, 2, 0, 0, 0);
        Hec.Dss.Time.DateTimeToHecDateTime(returnedMidnight, out string dssDate, out string dssTime);
        var dssTs = MakeDssSeries(
            values: new[] { 1950.0, 1950.0 },
            times: new[] { returnedMidnight, returnedMidnight.AddDays(1) },
            dataType: "PER-AVER");

        var ts = TimeSeriesElement.BuildFromDssTimeSeries(dssTs, TimeInterval.OneDay);

        Assert.AreEqual("01Oct1905", dssDate);
        Assert.AreEqual("2400", dssTime);
        Assert.AreEqual(new DateTime(1905, 10, 1), ts.StartDate);
        Assert.AreEqual(1950.0, ts[0].Value, 1e-12);
        Assert.AreEqual(new DateTime(1905, 10, 2), ts[1].Index);
    }

    /// <summary>
    /// Verifies that both supported DSS period data types are matched after normalization.
    /// </summary>
    /// <param name="dataType">The DSS period data type to exercise.</param>
    [DataTestMethod]
    [DataRow(" per-aver ")]
    [DataRow("per-cum")]
    public void BuildFromDss_PeriodDataTypes_SubtractResolvedSubdailyInterval(string dataType)
    {
        var returnedTime = new DateTime(2020, 1, 2, 0, 0, 0);
        var dssTs = MakeDssSeries(
            values: new[] { 10.0 },
            times: new[] { returnedTime },
            path: "/A/B/C//1Hour/F/",
            dataType: dataType);

        var ts = TimeSeriesElement.BuildFromDssTimeSeries(dssTs, TimeInterval.OneHour);

        Assert.AreEqual(returnedTime.AddHours(-1), ts.StartDate);
        Assert.AreEqual(10.0, ts[0].Value, 1e-12);
    }

    /// <summary>
    /// Verifies that calendar-based period records subtract the full resolved calendar interval.
    /// </summary>
    [TestMethod]
    public void BuildFromDss_PeriodAverageMonthly_SubtractsCalendarInterval()
    {
        var dssTs = MakeDssSeries(
            values: new[] { 1.0, 2.0, 3.0 },
            times: new[]
            {
                new DateTime(2020, 2, 1),
                new DateTime(2020, 3, 1),
                new DateTime(2020, 4, 1),
            },
            path: "/A/B/C//1Month/F/",
            dataType: "PER-AVER");

        var ts = TimeSeriesElement.BuildFromDssTimeSeries(dssTs, TimeInterval.OneMonth);

        Assert.AreEqual(new DateTime(2020, 1, 1), ts[0].Index);
        Assert.AreEqual(new DateTime(2020, 2, 1), ts[1].Index);
        Assert.AreEqual(new DateTime(2020, 3, 1), ts[2].Index);
    }

    /// <summary>
    /// Verifies that non-period DSS data types preserve their returned timestamps.
    /// </summary>
    /// <param name="dataType">The DSS data type to exercise.</param>
    [DataTestMethod]
    [DataRow("INST-VAL")]
    [DataRow("INST-CUM")]
    [DataRow("")]
    [DataRow("UNKNOWN")]
    public void BuildFromDss_NonPeriodDataTypes_PreserveReturnedTimestamp(string dataType)
    {
        var returnedTime = new DateTime(2020, 1, 2, 0, 0, 0);
        var dssTs = MakeDssSeries(
            values: new[] { 10.0 },
            times: new[] { returnedTime },
            dataType: dataType);

        var ts = TimeSeriesElement.BuildFromDssTimeSeries(dssTs, TimeInterval.OneDay);

        Assert.AreEqual(returnedTime, ts.StartDate);
    }

    /// <summary>
    /// Verifies that irregular period records retain their explicit observation times.
    /// </summary>
    [TestMethod]
    public void BuildFromDss_IrregularPeriodData_PreservesReturnedTimestamps()
    {
        var t0 = new DateTime(2020, 1, 2, 0, 0, 0);
        var dssTs = MakeDssSeries(
            values: new[] { 10.0, 20.0 },
            times: new[] { t0, t0.AddDays(3) },
            path: "/A/B/C//IR-Day/F/",
            dataType: "PER-AVER");

        var ts = TimeSeriesElement.BuildFromDssTimeSeries(dssTs, TimeInterval.Irregular);

        Assert.AreEqual(t0, ts[0].Index);
        Assert.AreEqual(t0.AddDays(3), ts[1].Index);
    }

    /// <summary>
    /// Verifies that a period timestamp too early to shift reports date underflow.
    /// </summary>
    [TestMethod]
    public void BuildFromDss_PeriodTimestampAtMinimumDate_Throws()
    {
        var dssTs = MakeDssSeries(
            values: new[] { 10.0 },
            times: new[] { DateTime.MinValue },
            dataType: "PER-AVER");

        Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
            TimeSeriesElement.BuildFromDssTimeSeries(dssTs, TimeInterval.OneDay));
    }

    /// <summary>
    /// Verifies that a regular DSS record starts at the first valid value rather than DSS block padding.
    /// </summary>
    [TestMethod]
    public void BuildFromDss_RegularSeriesPreservesNonJanuaryFirstValidDate()
    {
        var dssTs = MakeDssSeries(
            values: new[] { -901.0, 10.0, 11.0, -902.0 },
            times: new[]
            {
                new DateTime(1968, 1, 1),
                new DateTime(1968, 7, 16),
                new DateTime(1968, 7, 17),
                new DateTime(1968, 12, 31),
            });

        var ts = TimeSeriesElement.BuildFromDssTimeSeries(dssTs, TimeInterval.OneDay);

        Assert.AreEqual(2, ts.Count);
        Assert.AreEqual(new DateTime(1968, 7, 16), ts.StartDate);
        Assert.AreEqual(10.0, ts[0].Value, 1e-12);
        Assert.AreEqual(new DateTime(1968, 7, 17), ts.EndDate);
    }

    /// <summary>
    /// Verifies that leading and trailing missing DSS values are trimmed from regular records.
    /// </summary>
    [TestMethod]
    public void BuildFromDss_RegularSeriesTrimsBlockPadding()
    {
        var t0 = new DateTime(2020, 1, 1);
        var dssTs = MakeDssSeries(
            values: new[] { -901.0, 1.0, 2.0, -902.0 },
            times: new[] { t0, t0.AddDays(1), t0.AddDays(2), t0.AddDays(3) });

        var ts = TimeSeriesElement.BuildFromDssTimeSeries(dssTs, TimeInterval.OneDay);

        Assert.AreEqual(2, ts.Count);
        Assert.AreEqual(t0.AddDays(1), ts[0].Index);
        Assert.AreEqual(1.0, ts[0].Value, 1e-12);
        Assert.AreEqual(t0.AddDays(2), ts[1].Index);
        Assert.AreEqual(2.0, ts[1].Value, 1e-12);
    }

    /// <summary>
    /// Verifies that omitted regular DSS timesteps are inserted as NaN rows.
    /// </summary>
    [TestMethod]
    public void BuildFromDss_RegularSeriesFillsOmittedTimestepsWithNaN()
    {
        var t0 = new DateTime(2020, 1, 1);
        var dssTs = MakeDssSeries(
            values: new[] { 1.0, 3.0 },
            times: new[] { t0, t0.AddDays(2) });

        var ts = TimeSeriesElement.BuildFromDssTimeSeries(dssTs, TimeInterval.OneDay);

        Assert.AreEqual(3, ts.Count);
        Assert.AreEqual(t0, ts[0].Index);
        Assert.AreEqual(1.0, ts[0].Value, 1e-12);
        Assert.AreEqual(t0.AddDays(1), ts[1].Index);
        Assert.IsTrue(double.IsNaN(ts[1].Value), "Omitted DSS timesteps must be imported as NaN.");
        Assert.AreEqual(t0.AddDays(2), ts[2].Index);
        Assert.AreEqual(3.0, ts[2].Value, 1e-12);
    }

    /// <summary>
    /// Verifies that irregular DSS imports preserve missing ordinates instead of trimming them.
    /// </summary>
    [TestMethod]
    public void BuildFromDss_IrregularSeriesDoesNotTrimMissingSentinels()
    {
        var t0 = new DateTime(2020, 1, 1);
        var dssTs = MakeDssSeries(
            values: new[] { -901.0, 10.0, -902.0 },
            times: new[] { t0, t0.AddDays(3), t0.AddDays(7) },
            path: "/A/B/C//IR-Day/F/");

        var ts = TimeSeriesElement.BuildFromDssTimeSeries(dssTs, TimeInterval.Irregular);

        Assert.AreEqual(TimeInterval.Irregular, ts.TimeInterval);
        Assert.AreEqual(3, ts.Count);
        Assert.AreEqual(t0, ts[0].Index);
        Assert.IsTrue(double.IsNaN(ts[0].Value), "Irregular leading missing values are timestamped ordinates.");
        Assert.AreEqual(t0.AddDays(7), ts[2].Index);
        Assert.IsTrue(double.IsNaN(ts[2].Value), "Irregular trailing missing values are timestamped ordinates.");
    }

    /// <summary>
    /// Verifies that irregular DSS imports do not fill omitted timestamps.
    /// </summary>
    [TestMethod]
    public void BuildFromDss_IrregularSeriesDoesNotFillOmittedDates()
    {
        var t0 = new DateTime(2020, 1, 1);
        var dssTs = MakeDssSeries(
            values: new[] { 1.0, 3.0 },
            times: new[] { t0, t0.AddDays(2) },
            path: "/A/B/C//IR-Day/F/");

        var ts = TimeSeriesElement.BuildFromDssTimeSeries(dssTs, TimeInterval.Irregular);

        Assert.AreEqual(2, ts.Count);
        Assert.AreEqual(t0, ts[0].Index);
        Assert.AreEqual(t0.AddDays(2), ts[1].Index);
    }

    /// <summary>
    /// Verifies that duplicate DSS timestamps are rejected.
    /// </summary>
    [TestMethod]
    public void BuildFromDss_DuplicateTimestamps_Throws()
    {
        var t0 = new DateTime(2020, 1, 1);
        var dssTs = MakeDssSeries(
            values: new[] { 1.0, 2.0 },
            times: new[] { t0, t0 });

        var ex = Assert.ThrowsException<Exception>(() =>
            TimeSeriesElement.BuildFromDssTimeSeries(dssTs, TimeInterval.OneDay));

        StringAssert.Contains(ex.Message, "duplicate timestamp");
    }

    /// <summary>
    /// Verifies that regular DSS records with no valid values fail clearly.
    /// </summary>
    [TestMethod]
    public void BuildFromDss_AllMissingRegularSeries_Throws()
    {
        var t0 = new DateTime(2020, 1, 1);
        var dssTs = MakeDssSeries(
            values: new[] { -901.0, -902.0 },
            times: new[] { t0, t0.AddDays(1) });

        var ex = Assert.ThrowsException<Exception>(() =>
            TimeSeriesElement.BuildFromDssTimeSeries(dssTs, TimeInterval.OneDay));

        StringAssert.Contains(ex.Message, "does not contain any valid data values");
    }

    /// <summary>
    /// Verifies that DSS E-parts resolve to supported RMC-BestFit intervals.
    /// </summary>
    [TestMethod]
    public void ResolveDssTimeInterval_MapsSupportedRegularAliases()
    {
        var t0 = new DateTime(2020, 1, 1);

        Assert.AreEqual(TimeInterval.OneMinute, TimeSeriesElement.ResolveDssTimeInterval(
            MakeDssSeries(new[] { 1.0 }, new[] { t0 }, "/A/B/C//1MIN/F/")));
        Assert.AreEqual(TimeInterval.FiveMinute, TimeSeriesElement.ResolveDssTimeInterval(
            MakeDssSeries(new[] { 1.0 }, new[] { t0 }, "/A/B/C//5Minute/F/")));
        Assert.AreEqual(TimeInterval.FifteenMinute, TimeSeriesElement.ResolveDssTimeInterval(
            MakeDssSeries(new[] { 1.0 }, new[] { t0 }, "/A/B/C//15MIN/F/")));
        Assert.AreEqual(TimeInterval.ThirtyMinute, TimeSeriesElement.ResolveDssTimeInterval(
            MakeDssSeries(new[] { 1.0 }, new[] { t0 }, "/A/B/C//30Minute/F/")));
        Assert.AreEqual(TimeInterval.OneHour, TimeSeriesElement.ResolveDssTimeInterval(
            MakeDssSeries(new[] { 1.0 }, new[] { t0 }, "/A/B/C//1Hour/F/")));
        Assert.AreEqual(TimeInterval.SixHour, TimeSeriesElement.ResolveDssTimeInterval(
            MakeDssSeries(new[] { 1.0 }, new[] { t0 }, "/A/B/C//6HOUR/F/")));
        Assert.AreEqual(TimeInterval.TwelveHour, TimeSeriesElement.ResolveDssTimeInterval(
            MakeDssSeries(new[] { 1.0 }, new[] { t0 }, "/A/B/C//12Hour/F/")));
        Assert.AreEqual(TimeInterval.OneDay, TimeSeriesElement.ResolveDssTimeInterval(
            MakeDssSeries(new[] { 1.0 }, new[] { t0 }, "/A/B/C//1Day/F/")));
        Assert.AreEqual(TimeInterval.SevenDay, TimeSeriesElement.ResolveDssTimeInterval(
            MakeDssSeries(new[] { 1.0 }, new[] { t0 }, "/A/B/C//1WEEK/F/")));
        Assert.AreEqual(TimeInterval.OneMonth, TimeSeriesElement.ResolveDssTimeInterval(
            MakeDssSeries(new[] { 1.0 }, new[] { t0 }, "/A/B/C//1MON/F/")));
        Assert.AreEqual(TimeInterval.OneQuarter, TimeSeriesElement.ResolveDssTimeInterval(
            MakeDssSeries(new[] { 1.0 }, new[] { t0 }, "/A/B/C//3Month/F/")));
        Assert.AreEqual(TimeInterval.OneYear, TimeSeriesElement.ResolveDssTimeInterval(
            MakeDssSeries(new[] { 1.0 }, new[] { t0 }, "/A/B/C//1Year/F/")));
    }

    /// <summary>
    /// Verifies that DSS irregular E-parts resolve to irregular even when timestamps are evenly spaced.
    /// </summary>
    [TestMethod]
    public void ResolveDssTimeInterval_MapsIrregularEParts()
    {
        var t0 = new DateTime(2020, 1, 1);

        Assert.AreEqual(TimeInterval.Irregular, TimeSeriesElement.ResolveDssTimeInterval(
            MakeDssSeries(new[] { 1.0, 2.0 }, new[] { t0, t0.AddDays(1) }, "/A/B/C//IR-Day/F/")));
        Assert.AreEqual(TimeInterval.Irregular, TimeSeriesElement.ResolveDssTimeInterval(
            MakeDssSeries(new[] { 1.0, 2.0 }, new[] { t0, t0.AddDays(1) }, "/A/B/C//~1Day/F/")));
    }

    /// <summary>
    /// Verifies that unsupported regular DSS E-parts fail clearly.
    /// </summary>
    [TestMethod]
    public void ResolveDssTimeInterval_UnsupportedRegularEPart_Throws()
    {
        var t0 = new DateTime(2020, 1, 1);
        var dssTs = MakeDssSeries(new[] { 1.0 }, new[] { t0 }, "/A/B/C//8Hour/F/");

        var ex = Assert.ThrowsException<Exception>(() =>
            TimeSeriesElement.ResolveDssTimeInterval(dssTs));

        StringAssert.Contains(ex.Message, "not supported");
    }

    /// <summary>
    /// Verifies that mismatched DSS value and time arrays throw.
    /// </summary>
    [TestMethod]
    public void BuildFromDss_MismatchedArrayLengths_Throws()
    {
        var t0 = new DateTime(2020, 1, 1);
        var dssTs = MakeDssSeries(
            values: new[] { 1.0, 2.0, 3.0, 4.0 },
            times: new[] { t0, t0.AddDays(1), t0.AddDays(2) });

        var ex = Assert.ThrowsException<Exception>(() =>
            TimeSeriesElement.BuildFromDssTimeSeries(dssTs, TimeInterval.OneDay));

        StringAssert.Contains(ex.Message, "mismatched arrays",
            "Mismatched length error must mention 'mismatched arrays' for clarity.");
    }

    /// <summary>
    /// Verifies that null DSS input throws.
    /// </summary>
    [TestMethod]
    public void BuildFromDss_NullInput_Throws()
    {
        Assert.ThrowsException<ArgumentNullException>(() =>
            TimeSeriesElement.BuildFromDssTimeSeries(null!, TimeInterval.OneDay));
    }

    /// <summary>
    /// Integration test: confirms the existing TS-WNG-005 warning pipeline (driven by
    /// <c>Numerics.Data.TimeSeries.HasMissingValues</c>) fires correctly once sentinels
    /// have been converted to NaN.
    /// </summary>
    [STATestMethod]
    public void BuildFromDss_HasMissingValuesFiresAfterImport()
    {
        var t0 = new DateTime(2020, 1, 1);
        var dssTs = MakeDssSeries(
            values: new[] { 1.0, -901.0, 3.0, 4.0 },
            times: new[] { t0, t0.AddDays(1), t0.AddDays(2), t0.AddDays(3) });

        var element = new TimeSeriesElement();
        element.TimeSeries = TimeSeriesElement.BuildFromDssTimeSeries(dssTs, TimeInterval.OneDay);

        Assert.IsTrue(element.TimeSeries.HasMissingValues,
            "After sentinel→NaN conversion, HasMissingValues must report true so TS-WNG-005 fires.");
    }
}
