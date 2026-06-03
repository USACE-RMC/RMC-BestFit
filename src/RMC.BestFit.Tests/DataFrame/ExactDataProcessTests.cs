using Numerics.Data;
using RMC.BestFit;
using RMC.BestFit.Models;

namespace RMC.BestFit.Tests.InputDataFrame;

/// <summary>
/// Unit tests for <see cref="DataFrame"/> exact data processing methods.
/// Validates block series creation, peaks-over-threshold extraction, and USGS data integration.
/// </summary>
/// <remarks>
/// <para>
/// These tests follow patterns from the Numerics TimeSeries class unit tests and verify correct
/// implementation of annual maximum series extraction, water year processing, and threshold exceedance methods.
/// </para>
/// <para>
///     <b> Authors: </b>
///     <list type="bullet">
///     <item>Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil</item>
///     <item>Sadie Niblett, USACE Risk Management Center, sadie.s.niblett@usace.army.mil</item>
///     </list>
/// </para>
/// </remarks>
[TestClass]
public class ExactDataProcessTests
{
    /// <summary>
    /// Tests calendar year block series extraction for annual maximum values.
    /// </summary>
    /// <remarks>
    /// Validates that <see cref="DataFrame.CreateBlockSeries"/> correctly extracts maximum values
    /// over calendar year windows (January through December). Uses synthetic monthly data spanning
    /// multiple years and verifies the extracted annual maxima match expected values.
    /// </remarks>
    [TestMethod]
    public void Test_CalendarYearSeries()
    {
        var values = new double[] { 122d, 244d, 214d, 173d, 229d, 156d, 212d, 263d, 146d, 183d, 161d, 205d, 135d, 331d, 225d, 174d, 98.8d, 149d, 238d, 262d, 132d, 235d, 216d, 240d, 230d, 192d, 195d, 172d, 173d, 172d, 153d, 142d, 317d, 161d, 201d, 204d, 194d, 164d, 183d, 161d, 167d, 179d, 185d, 117d, 192d, 337d, 125d, 166d, 99.1d, 202d, 230d, 158d, 262d, 154d, 164d, 182d, 164d, 183d, 171d, 250d, 184d, 205d, 237d, 177d, 239d, 187d, 180d, 173d, 174d };
        var ts = new Numerics.Data.TimeSeries(TimeInterval.OneMonth, new DateTime(2023, 01, 01), values);

        var df = new DataFrame();
        df.CreateBlockSeries(ts, TimeBlockWindow.CalendarYear, BlockFunctionType.Maximum, SmoothingFunctionType.None, 1, 12, 1);

        // same values as annual max series test
        var maxVals = new double[] { 263, 331, 317, 337, 262, 239 };

        for (int i = 0; i < maxVals.Length; i++)
        {
            Assert.AreEqual(maxVals[i], df.ExactSeries[i].Value);
        }
    }

    /// <summary>
    /// Tests water year block series extraction for annual maximum values.
    /// </summary>
    /// <remarks>
    /// Validates that <see cref="DataFrame.CreateBlockSeries"/> correctly extracts maximum values
    /// over water year windows (October through September). Water years are commonly used in hydrologic
    /// analysis to align with the natural hydrologic cycle. Uses synthetic monthly data and verifies
    /// extracted maxima differ from calendar year results due to the shifted year boundary.
    /// </remarks>
    [TestMethod]
    public void Test_WaterYearSeries()
    {
        var values = new double[] { 122d, 244d, 214d, 173d, 229d, 156d, 212d, 263d, 146d, 183d, 161d, 205d, 135d, 331d, 225d, 174d, 98.8d, 149d, 238d, 262d, 132d, 235d, 216d, 240d, 230d, 192d, 195d, 172d, 173d, 172d, 153d, 142d, 317d, 161d, 201d, 204d, 194d, 164d, 183d, 161d, 167d, 179d, 185d, 117d, 192d, 337d, 125d, 166d, 99.1d, 202d, 230d, 158d, 262d, 154d, 164d, 182d, 164d, 183d, 171d, 250d, 184d, 205d, 237d, 177d, 239d, 187d, 180d, 173d, 174d };
        var ts = new TimeSeries(TimeInterval.OneMonth, new DateTime(2023, 01, 01), values);

        var df = new DataFrame();
        df.CreateBlockSeries(ts, TimeBlockWindow.WaterYear, BlockFunctionType.Maximum, SmoothingFunctionType.None, 10, 9, 1);

        var maxVals = new double[] { 263, 331, 317, 204, 337, 250 };
        for (int i = 0; i < maxVals.Length; i++)
        {
            Assert.AreEqual(maxVals[i], df.ExactSeries[i].Value);
        }
    }

    /// <summary>
    /// Tests custom year block series extraction with user-defined month boundaries.
    /// </summary>
    /// <remarks>
    /// Validates that <see cref="DataFrame.CreateBlockSeries"/> correctly handles custom year definitions
    /// with arbitrary start and end months. Tests both non-overlapping year definitions (matching calendar year)
    /// and overlapping windows (e.g., October through March). This flexibility allows analysts to define
    /// year boundaries that align with regional climate patterns or operational considerations.
    /// </remarks>
    [TestMethod]
    public void Test_CustomYearSeries()
    {
        var values = new double[] { 122d, 244d, 214d, 173d, 229d, 156d, 212d, 263d, 146d, 183d, 161d, 205d, 135d, 331d, 225d, 174d, 98.8d, 149d, 238d, 262d, 132d, 235d, 216d, 240d, 230d, 192d, 195d, 172d, 173d, 172d, 153d, 142d, 317d, 161d, 201d, 204d, 194d, 164d, 183d, 161d, 167d, 179d, 185d, 117d, 192d, 337d, 125d, 166d, 99.1d, 202d, 230d, 158d, 262d, 154d, 164d, 182d, 164d, 183d, 171d, 250d, 184d, 205d, 237d, 177d, 239d, 187d, 180d, 173d, 174d };
        var ts = new TimeSeries(TimeInterval.OneMonth, new DateTime(2023, 01, 01), values);
        var df = new DataFrame();

        // First test calendar year
        df.CreateBlockSeries(ts, TimeBlockWindow.CustomYear, BlockFunctionType.Maximum, SmoothingFunctionType.None, 1, 12, 1);
        var maxVals = new double[] { 263, 331, 317, 337, 262, 239 };
        for (int i = 0; i < maxVals.Length; i++)
        {
            Assert.AreEqual(maxVals[i], df.ExactSeries[i].Value);
        }

        // Next test custom overlapping year
        df.CreateBlockSeries(ts, TimeBlockWindow.CustomYear, BlockFunctionType.Maximum, SmoothingFunctionType.None, 10, 3, 1);
        maxVals = new double[] { 244, 331, 240, 204, 337, 250 };
        for (int i = 0; i < maxVals.Length; i++)
        {
            Assert.AreEqual(maxVals[i], df.ExactSeries[i].Value);
        }
    }


    /// <summary>
    /// Tests peaks-over-threshold (POT) extraction with cluster separation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Validates <see cref="DataFrame.CreatePeaksOverThresholdSeries"/> using the decluster algorithm
    /// from the POT R package (https://cran.r-project.org/web/packages/POT/). The method extracts values
    /// exceeding a specified threshold while ensuring temporal independence by requiring a minimum separation
    /// between consecutive peaks.
    /// </para>
    /// <para>
    /// Tests multiple threshold and minimum separation scenarios to verify correct peak identification
    /// and cluster removal. POT analysis is commonly used in flood frequency analysis as an alternative
    /// to annual maximum series, potentially providing more information when multiple events per year
    /// are significant.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void Test_PeaksOverThreshold()
    {
        var values = new double[] { 122, 244, 214, 173, 229, 156, 212, 263, 146, 183, 161, 205, 135, 331, 225, 174, 98.8, 149, 238, 262, 132, 235, 216, 240, 230, 192, 195, 172, 173, 172, 153, 142, 317, 161, 201, 204, 194, 164, 183, 161, 167, 179, 185, 117, 192, 337, 125, 166, 99.1, 202, 230, 158, 262, 154, 164, 182, 164, 183, 171, 250, 184, 205, 237, 177, 239, 187, 180, 173, 174 };
        var ts = new TimeSeries(TimeInterval.OneMonth, new DateTime(2023, 01, 01), values);
        var df = new DataFrame();

        // Threshold of 100, 2 min steps between
        var truePOT = new double[] { 331, 337, 262 };
        df.CreatePeaksOverThresholdSeries(ts, 100, 2);
        for (int i = 0; i < truePOT.Length; i++)
        {
            Assert.AreEqual(truePOT[i], df.ExactSeries[i].Value);
        }

        // Threshold of 90, 1 min steps between
        truePOT = new double[] { 337 };
        df.CreatePeaksOverThresholdSeries(ts, 90, 1);
        for (int i = 0; i < truePOT.Length; i++)
        {
            Assert.AreEqual(truePOT[i], df.ExactSeries[i].Value);
        }

        // Threshold of 150, 5 min steps between
        truePOT = new double[] { 331, 240, 317, 337 };
        df.CreatePeaksOverThresholdSeries(ts, 150, 5);
        for (int i = 0; i < truePOT.Length; i++)
        {
            Assert.AreEqual(truePOT[i], df.ExactSeries[i].Value);
        }

        // Threshold of 200, 2 min steps between
        truePOT = new double[] { 263, 331, 262, 317, 337, 250 };
        df.CreatePeaksOverThresholdSeries(ts, 200, 2);
        for (int i = 0; i < truePOT.Length; i++)
        {
            Assert.AreEqual(truePOT[i], df.ExactSeries[i].Value);
        }

    }

    /// <summary>
    /// Tests USGS peak discharge data retrieval from NWIS web services.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Integration test validating <see cref="DataFrame.CreateFromUSGS"/> for retrieving annual peak
    /// discharge records from the USGS National Water Information System (NWIS). Uses station 01614000
    /// (Potomac River near Washington, DC) as a test case.
    /// </para>
    /// <para>
    /// Verifies both the parsed time series data and raw text response are properly populated. This
    /// integration allows automated workflows to directly incorporate USGS streamflow data into
    /// frequency analysis without manual data entry.
    /// </para>
    /// </remarks>
    [TestMethod, TestCategory("Integration")]
    public async Task USGS_PeakDischarge_Works()
    {
        var df = new DataFrame();

        // USGS site number for Potomac River near Washington, DC.
        await df.CreateFromUSGS("01614000",TimeSeriesDownload.TimeSeriesType.PeakDischarge);

        Assert.IsNotNull(df.ExactSeries, "Time series is null.");
        Assert.IsNotNull(df.USGSRawText, "Raw text is null.");
    }

    /// <summary>
    /// Tests that invalid USGS station identifiers are properly rejected.
    /// </summary>
    /// <remarks>
    /// Validates <see cref="TimeSeriesDownload.FromUSGS"/> throws <see cref="ArgumentException"/>
    /// when provided with an invalid station number. USGS station identifiers must meet specific
    /// format requirements; this test ensures proper error handling for malformed inputs.
    /// </remarks>
    [TestMethod]
    public async Task USGS_InvalidStation_Throws()
    {
        await Assert.ThrowsExceptionAsync<ArgumentException>(async () =>
            await TimeSeriesDownload.FromUSGS("1134500"));
    }
}
