using Hec.Dss;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RMC.BestFit.UI.Tests.Elements.TimeSeriesData;

/// <summary>
/// Exercises complete DSS retrieval across absent storage blocks using temporary native files.
/// </summary>
/// <remarks>Native DSS initialization and message configuration use process-wide state, so these
/// file integration tests must not run concurrently with other test methods.</remarks>
[TestClass]
[DoNotParallelize]
public class DssTimeSeriesReaderTests
{
    /// <summary>
    /// Verifies native block partitioning across every supported storage size and boundary dates.
    /// </summary>
    /// <param name="interval">The native interval token.</param>
    /// <param name="firstText">The first raw timestamp.</param>
    /// <param name="lastText">The last raw timestamp after absent blocks.</param>
    [TestMethod]
    [DataRow("1Minute", "2020-01-01T12:01:00", "2020-01-04T12:01:00")]
    [DataRow("5MIN", "2020-01-01T12:05:00", "2020-01-04T12:05:00")]
    [DataRow("15Minute", "2020-01-01T12:15:00", "2020-04-01T12:15:00")]
    [DataRow("30MIN", "2020-01-01T12:30:00", "2020-04-01T12:30:00")]
    [DataRow("1Hour", "2020-01-01T06:00:00", "2020-04-01T06:00:00")]
    [DataRow("6Hour", "2020-01-01T06:00:00", "2020-04-01T06:00:00")]
    [DataRow("12Hour", "2020-01-01T12:00:00", "2020-04-01T12:00:00")]
    [DataRow("1Day", "2000-02-29T00:00:00", "2020-02-29T00:00:00")]
    [DataRow("1Day", "2000-01-01T00:00:00", "2020-01-01T00:00:00")]
    [DataRow("1Day", "2000-01-01T06:00:00", "2020-01-01T06:00:00")]
    [DataRow("1Week", "2000-01-07T00:00:00", "2040-01-06T00:00:00")]
    [DataRow("1Month", "2000-02-01T00:00:00", "2040-02-01T00:00:00")]
    [DataRow("1Year", "2000-12-31T00:00:00", "2300-12-31T00:00:00")]
    public void Read_SparseNativeBlocks_PreservesRawObservations(string interval, string firstText, string lastText)
    {
        string filename = TemporaryDssPath();
        string pathname = $"/TEST/BLOCK/FLOW//{interval}/GAPS/";
        DateTime first = DateTime.Parse(firstText, CultureInfo.InvariantCulture);
        DateTime last = DateTime.Parse(lastText, CultureInfo.InvariantCulture);
        try
        {
            WriteSeparatedValues(filename, pathname, first, last);
            using var reader = new DssReader(filename, 0);
            var result = DssTimeSeriesReader.Read(reader, new DssPath(pathname));
            CollectionAssert.AreEqual(new[] { first, last }, result.Times);
            CollectionAssert.AreEqual(new[] { 0.0, 42.0 }, result.Values);
            Assert.AreEqual("INST-VAL", result.DataType);
            Assert.AreEqual("CFS", result.Units);
        }
        finally
        {
            if (File.Exists(filename)) File.Delete(filename);
        }
    }

    /// <summary>
    /// Verifies that concrete and condensed paths identify the entire logical series.
    /// </summary>
    [TestMethod]
    public void Read_PathFormsAndCase_PreserveCompleteSeriesIdentity()
    {
        string filename = TemporaryDssPath();
        const string pathname = "/TEST/IDENTITY/FLOW//1Day/GAPS/";
        try
        {
            WriteSeparatedValues(filename, pathname, new DateTime(2000, 3, 1), new DateTime(2040, 3, 1));
            using var reader = new DssReader(filename, 0);
            var catalog = reader.GetCatalog();
            foreach (DssPath path in new[] { new DssPath(pathname.ToLowerInvariant()), catalog.Single(), catalog.UnCondensedPaths.First() })
            {
                var result = DssTimeSeriesReader.Read(reader, path);
                CollectionAssert.AreEqual(new[] { 0.0, 42.0 }, result.Values);
                Assert.AreEqual(new DateTime(2040, 3, 1), result.Times.Last());
            }
        }
        finally
        {
            if (File.Exists(filename)) File.Delete(filename);
        }
    }

    /// <summary>
    /// Verifies that weekly block windows preserve all stored values and their series-wide time grid.
    /// </summary>
    /// <param name="year">The decade containing the first weekly block.</param>
    /// <param name="day">The first native timestamp's day of January.</param>
    [TestMethod]
    [DataRow(1900, 2)]
    [DataRow(1910, 7)]
    [DataRow(2000, 3)]
    [DataRow(2010, 7)]
    [DataRow(2040, 2)]
    public void Read_WeeklyBlockWindow_PreservesEveryStoredValueAndTimestamp(int year, int day)
    {
        string filename = TemporaryDssPath();
        const string pathname = "/TEST/WEEKLY/FLOW//1Week/BOUNDARY/";
        try
        {
            using (var writer = new DssWriter(filename, 0))
            {
                Assert.AreEqual(0, writer.Write(new Hec.Dss.TimeSeries(pathname,
                    Enumerable.Range(1, 523).Select(value => (double)value).ToArray(),
                    new DateTime(year, 1, day), "CFS", "INST-VAL")));
            }
            using var reader = new DssReader(filename, 0);
            var path = new DssPath(pathname);
            var result = DssTimeSeriesReader.Read(reader, path);
            Assert.AreEqual(523, result.Times.Length,
                $"Weekly merge returned {result.Times.Length} values: {string.Join(", ", result.Times.Zip(result.Values).TakeLast(5))}");
            CollectionAssert.AreEqual(Enumerable.Range(0, 523).Select(index => new DateTime(year, 1, day).AddDays(7 * index)).ToArray(), result.Times);
            CollectionAssert.AreEqual(Enumerable.Range(1, 523).Select(value => (double)value).ToArray(), result.Values);
        }
        finally
        {
            if (File.Exists(filename)) File.Delete(filename);
        }
    }

    /// <summary>
    /// Verifies an all-missing first weekly record does not prevent phase discovery in a later record.
    /// </summary>
    [TestMethod]
    public void Read_WeeklyEmptyFirstBlock_ContinuesToLaterData()
    {
        string filename = TemporaryDssPath();
        const string pathname = "/TEST/WEEKLY/FLOW//1Week/EMPTY-FIRST/";
        try
        {
            using (var writer = new DssWriter(filename, 0))
            {
                Assert.AreEqual(0, writer.Write(new Hec.Dss.TimeSeries(pathname,
                    new[] { -901.0 }, new DateTime(2000, 1, 7), "CFS", "INST-VAL")));
                Assert.AreEqual(0, writer.Write(new Hec.Dss.TimeSeries(pathname,
                    new[] { 20.0 }, new DateTime(2040, 1, 6), "CFS", "INST-VAL")));
            }
            using var reader = new DssReader(filename, 0);

            var result = DssTimeSeriesReader.Read(reader, new DssPath(pathname));

            CollectionAssert.AreEqual(new[] { new DateTime(2040, 1, 6) }, result.Times);
            CollectionAssert.AreEqual(new[] { 20.0 }, result.Values);
        }
        finally
        {
            if (File.Exists(filename)) File.Delete(filename);
        }
    }

    /// <summary>
    /// Verifies that irregular and pseudo-regular records retain explicit timestamps and missing values.
    /// </summary>
    /// <param name="interval">An irregular DSS E-part.</param>
    [TestMethod]
    [DataRow("IR-Year")]
    [DataRow("~1Day")]
    public void Read_IrregularRecords_PreserveNativeRetrieval(string interval)
    {
        string filename = TemporaryDssPath();
        var path = new DssPath($"/TEST/IRREGULAR/FLOW//{interval}/GAPS/");
        var expectedTimes = new[] { new DateTime(2000, 3, 1, 6, 0, 0), new DateTime(2012, 7, 5, 11, 23, 0), new DateTime(2040, 3, 1) };
        try
        {
            using (var writer = new DssWriter(filename, 0))
            {
                Assert.AreEqual(0, writer.Write(new Hec.Dss.TimeSeries
                {
                    Path = path, Times = expectedTimes, Values = new[] { 10.0, -901.0, 20.0 }, Units = "CFS", DataType = "INST-VAL",
                }));
            }
            using var reader = new DssReader(filename, 0);
            var native = reader.GetTimeSeries(path);
            var result = DssTimeSeriesReader.Read(reader, path);
            CollectionAssert.AreEqual(expectedTimes, result.Times);
            CollectionAssert.AreEqual(native.Values, result.Values);
        }
        finally
        {
            if (File.Exists(filename)) File.Delete(filename);
        }
    }

    /// <summary>
    /// Verifies that empty blocks do not end traversal or mix another logical series into the result.
    /// </summary>
    [TestMethod]
    public void ReadBlocks_EmptyAndUnrelatedBlocks_DoNotHideLaterData()
    {
        var path = new DssPath("/TEST/MERGE/FLOW//1Day/GAPS/");
        var catalog = Blocks(path, 2000, 2002, 2040).Concat(new[] { new DssPath("/TEST/OTHER/FLOW/01Jan2020/1Day/GAPS/") });
        var result = DssTimeSeriesReader.ReadBlocks(path, catalog, (block, start, end) =>
            block.DPartAsDateTime.Year == 2002 ? new Hec.Dss.TimeSeries { LocationInformation = new LocationInformation() }
                : MakeChunk(block.DPartAsDateTime.AddDays(1), block.DPartAsDateTime.Year));
        CollectionAssert.AreEqual(new[] { 2000.0, 2040.0 }, result.Values);
        Assert.AreEqual("CFS", result.Units);
    }

    /// <summary>
    /// Verifies that malformed data and conflicting metadata fail instead of producing a partial import.
    /// </summary>
    /// <param name="defect">The malformed native response to exercise.</param>
    [TestMethod]
    [DataRow("null")]
    [DataRow("null-times")]
    [DataRow("mismatched")]
    [DataRow("duplicate")]
    [DataRow("units")]
    [DataRow("type")]
    [DataRow("exception")]
    public void ReadBlocks_InvalidResponses_RejectPartialImport(string defect)
    {
        var path = new DssPath("/TEST/INVALID/FLOW//1Day/GAPS/");
        var error = Assert.ThrowsException<InvalidDataException>(() =>
            DssTimeSeriesReader.ReadBlocks(path, Blocks(path, 2000, 2040), (block, start, end) =>
            {
                var chunk = MakeChunk(block.DPartAsDateTime.AddDays(1), 1.0);
                if (block.DPartAsDateTime.Year == 2000) return chunk;
                switch (defect)
                {
                    case "null": return null!;
                    case "null-times": chunk.Times = null!; break;
                    case "mismatched": chunk.Values = Array.Empty<double>(); break;
                    case "duplicate": chunk.Times[0] = new DateTime(2000, 1, 2); break;
                    case "units": chunk.Units = "CMS"; break;
                    case "type": chunk.DataType = "PER-CUM"; break;
                    case "exception": throw new IOException("Cannot read block.");
                }
                return chunk;
            }));
        if (defect == "duplicate") StringAssert.Contains(error.Message, "duplicate timestamp");
        else StringAssert.Contains(error.Message, "01Jan2040");
    }

    /// <summary>
    /// Verifies that cancellation prevents both initial reads and continuation after a native read.
    /// </summary>
    /// <param name="cancelBeforeRead">Whether cancellation occurs before the first native operation.</param>
    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void ReadBlocks_Cancellation_DoesNotReturnPartialData(bool cancelBeforeRead)
    {
        using var cancellation = new CancellationTokenSource();
        if (cancelBeforeRead) cancellation.Cancel();
        var path = new DssPath("/TEST/CANCEL/FLOW//1Day/GAPS/");
        int calls = 0;
        Assert.ThrowsException<OperationCanceledException>(() =>
            DssTimeSeriesReader.ReadBlocks(path, Blocks(path, 2000, 2040), (block, start, end) =>
            {
                calls++;
                cancellation.Cancel();
                return MakeChunk(block.DPartAsDateTime.AddDays(1), 1.0);
            }, cancellation.Token));
        Assert.AreEqual(cancelBeforeRead ? 0 : 1, calls);
    }

    /// <summary>
    /// Pins the configured wrapper's distinction between successfully read missing blocks and failure responses.
    /// </summary>
    [TestMethod]
    public void Read_NativeEmptyResponses_ContinueMissingBlocksAndRejectFailures()
    {
        string filename = TemporaryDssPath();
        var path = new DssPath("/TEST/EMPTY/FLOW//1Day/GAPS/");
        try
        {
            WriteSeparatedValues(filename, path.FullPath, new DateTime(2000, 3, 1), new DateTime(2040, 3, 1));
            using (var writer = new DssWriter(filename, 0))
            {
                Assert.AreEqual(0, writer.Write(new Hec.Dss.TimeSeries(path.FullPath,
                    new[] { -901.0 }, new DateTime(2002, 3, 1), "CFS", "INST-VAL")));
            }
            using var reader = new DssReader(filename, 0);
            var catalog = reader.GetCatalog().UnCondensedPaths;
            var emptyPath = catalog.Single(block => block.DPartAsDateTime.Year == 2002);
            var missing = reader.GetTimeSeries(emptyPath, new DateTime(2002, 1, 1).AddSeconds(1), new DateTime(2003, 1, 1));
            Assert.AreEqual(0, missing.Values.Length);
            Assert.IsNotNull(missing.LocationInformation, "Successful native retrieval always sets location information before trimming.");
            CollectionAssert.AreEqual(new[] { 0.0, 42.0 }, DssTimeSeriesReader.Read(reader, path).Values);

            var failure = reader.GetEmptyTimeSeries(emptyPath);
            Assert.AreEqual(0, failure.Values.Length);
            Assert.IsNull(failure.LocationInformation, "The wrapper's native-error factory does not set location information.");
            var error = Assert.ThrowsException<InvalidDataException>(() =>
                DssTimeSeriesReader.ReadBlocks(path, catalog, (block, start, end) =>
                    block.DPartAsDateTime.Year == 2002 ? failure : reader.GetTimeSeries(block, start, end)));
            StringAssert.Contains(error.Message, emptyPath.FullPath);
        }
        finally
        {
            if (File.Exists(filename)) File.Delete(filename);
        }
    }

    /// <summary>
    /// Creates a synthetic raw block response for malformed-data tests.
    /// </summary>
    /// <param name="time">The unconverted DSS timestamp.</param>
    /// <param name="value">The native value.</param>
    /// <returns>A one-observation DSS response.</returns>
    private static Hec.Dss.TimeSeries MakeChunk(DateTime time, double value)
    {
        return new Hec.Dss.TimeSeries { Times = new[] { time }, Values = new[] { value }, Units = "CFS", DataType = "INST-VAL" };
    }

    /// <summary>
    /// Creates a concrete catalog whose deliberately reversed order must not affect the result.
    /// </summary>
    /// <param name="path">The logical series identity.</param>
    /// <param name="years">The cataloged yearly block starts.</param>
    /// <returns>The concrete yearly records.</returns>
    private static IEnumerable<DssPath> Blocks(DssPath path, params int[] years)
    {
        return years.Reverse().Select(year => new DssPath(path.Apart, path.Bpart, path.Cpart,
            $"01Jan{year}", path.Epart, path.Fpart));
    }

    /// <summary>
    /// Creates a unique temporary filename for a native DSS fixture.
    /// </summary>
    /// <returns>The temporary filename.</returns>
    private static string TemporaryDssPath()
    {
        return Path.Combine(Path.GetTempPath(), $"BestFit_DssReader_{Guid.NewGuid():N}.dss");
    }

    /// <summary>
    /// Writes two known observations separately so absent intervening blocks remain absent.
    /// </summary>
    /// <param name="filename">The temporary fixture filename.</param>
    /// <param name="pathname">The logical series identity.</param>
    /// <param name="first">The first raw timestamp.</param>
    /// <param name="last">The last raw timestamp.</param>
    private static void WriteSeparatedValues(string filename, string pathname, DateTime first, DateTime last)
    {
        using var writer = new DssWriter(filename, 0);
        Assert.AreEqual(0, writer.Write(new Hec.Dss.TimeSeries(pathname, new[] { 0.0 }, first, "CFS", "INST-VAL")));
        Assert.AreEqual(0, writer.Write(new Hec.Dss.TimeSeries(pathname, new[] { 42.0 }, last, "CFS", "INST-VAL")));
    }

    /// <summary>
    /// Verifies observations on both sides of an adjacent block boundary occur exactly once.
    /// </summary>
    /// <param name="interval">The native regular interval.</param>
    /// <param name="firstText">The raw timestamp immediately preceding the ending boundary.</param>
    [TestMethod]
    [DataRow("1Minute", "2020-01-01T23:59:00")]
    [DataRow("1Hour", "2020-01-31T23:00:00")]
    [DataRow("1Day", "1999-12-31T00:00:00")]
    [DataRow("1Day", "1999-12-31T06:00:00")]
    [DataRow("1Month", "2009-12-01T00:00:00")]
    [DataRow("1Year", "1999-12-31T00:00:00")]
    public void Read_AdjacentBlockBoundaries_PreservesEveryObservationOnce(string interval, string firstText)
    {
        string filename = TemporaryDssPath();
        var path = new DssPath($"/TEST/ADJACENT/FLOW//{interval}/BOUNDARY/");
        DateTime first = DateTime.Parse(firstText, CultureInfo.InvariantCulture);
        try
        {
            using (var writer = new DssWriter(filename, 0))
            {
                Assert.AreEqual(0, writer.Write(new Hec.Dss.TimeSeries(path.FullPath,
                    new[] { 10.0, 20.0, 30.0 }, first, "CFS", "INST-VAL")));
            }
            using var reader = new DssReader(filename, 0);
            var native = reader.GetTimeSeries(path);
            var result = DssTimeSeriesReader.Read(reader, path);
            CollectionAssert.AreEqual(new[] { 10.0, 20.0, 30.0 }, result.Values);
            CollectionAssert.AreEqual(native.Times, result.Times);
        }
        finally
        {
            if (File.Exists(filename)) File.Delete(filename);
        }
    }

    /// <summary>
    /// Verifies a complete import with two gaps survives the production project save/open path.
    /// </summary>
    [STATestMethod]
    [DoNotParallelize]
    public async Task Download_SaveReopen_PreservesValuesGapsAndPeriodConversion()
    {
        string filename = TemporaryDssPath();
        string projectFilename = Path.ChangeExtension(filename, ".bestfit");
        const string pathname = "/TEST/PERSISTENCE/FLOW//1Day/GAPS/";
        try
        {
            using (var writer = new DssWriter(filename, 0))
            {
                foreach (int year in new[] { 2000, 2002, 2014 })
                {
                    Assert.AreEqual(0, writer.Write(new Hec.Dss.TimeSeries(pathname,
                        new[] { (double)year, year + 0.5 }, new DateTime(year, 3, 1), "CFS", "PER-CUM")));
                }
            }
            var project = (BestFitProject)Activator.CreateInstance(typeof(BestFitProject), nonPublic: true)!;
            project.FullFileName = projectFilename;
            project.Name = Path.GetFileNameWithoutExtension(projectFilename);
            var collection = project.ElementCollections!.OfType<TimeSeriesCollection>().Single();
            var element = new TimeSeriesElement("Sparse seasons", collection)
            {
                EntryMethod = TimeSeriesElement.TimeSeriesEntryMethod.HECDSS,
                HECDSSFullFilename = filename,
                HECDSSDataPathname = pathname,
            };
            await element.Download();
            var expectedTimes = element.TimeSeries.Select(item => item.Index).ToArray();
            var expectedValues = element.TimeSeries.Select(item => item.Value).ToArray();
            CollectionAssert.AreEqual(new[] { 2000.0, 2000.5, 2002.0, 2002.5, 2014.0, 2014.5 },
                expectedValues.Where(value => !double.IsNaN(value)).ToArray());
            Assert.AreEqual(new DateTime(2000, 2, 29), expectedTimes.First());
            Assert.AreEqual(new DateTime(2014, 3, 1), expectedTimes.Last());
            collection.Add(element);
            project.Save();

            var reopened = (BestFitProject)Activator.CreateInstance(typeof(BestFitProject), nonPublic: true)!;
            reopened.FullFileName = projectFilename;
            reopened.Open();
            var restored = reopened.ElementCollections!.OfType<TimeSeriesCollection>().Single().OfType<TimeSeriesElement>().Single();
            CollectionAssert.AreEqual(expectedTimes, restored.TimeSeries.Select(item => item.Index).ToArray());
            CollectionAssert.AreEqual(expectedValues, restored.TimeSeries.Select(item => item.Value).ToArray());
            Assert.AreEqual(Numerics.Data.TimeInterval.OneDay, restored.TimeInterval);
            Assert.IsTrue(restored.TimeSeries.HasMissingValues);
            Assert.AreEqual("CFS", restored.UnitLabel);
            Assert.AreEqual(pathname, restored.HECDSSDataPathname);
        }
        finally
        {
            System.Data.SQLite.SQLiteConnection.ClearAllPools();
            foreach (string candidate in new[] { filename, projectFilename, projectFilename + "-wal", projectFilename + "-shm" })
            {
                if (File.Exists(candidate)) File.Delete(candidate);
            }
        }
    }

    /// <summary>
    /// Verifies a failed later native block resets the series and restores the previous undo state.
    /// </summary>
    /// <param name="undoEnabled">The undo state before downloading.</param>
    [STATestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task Download_ConflictingBlocks_ResetsSeriesAndRestoresUndo(bool undoEnabled)
    {
        string filename = TemporaryDssPath();
        const string pathname = "/TEST/FAILURE/FLOW//1Day/GAPS/";
        try
        {
            using (var writer = new DssWriter(filename, 0))
            {
                Assert.AreEqual(0, writer.Write(new Hec.Dss.TimeSeries(pathname,
                    new[] { 10.0 }, new DateTime(2000, 3, 1), "CFS", "INST-VAL")));
                Assert.AreEqual(0, writer.Write(new Hec.Dss.TimeSeries(pathname,
                    new[] { 20.0 }, new DateTime(2040, 3, 1), "CMS", "INST-VAL")));
            }
            var element = new TimeSeriesElement
            {
                EntryMethod = TimeSeriesElement.TimeSeriesEntryMethod.HECDSS,
                HECDSSFullFilename = filename,
                HECDSSDataPathname = pathname,
                IsUndoEnabled = undoEnabled,
            };
            var error = await Assert.ThrowsExceptionAsync<InvalidDataException>(() => element.Download());
            StringAssert.Contains(error.Message, "01Jan2040");
            Assert.AreEqual(1, element.TimeSeries.Count);
            Assert.IsTrue(double.IsNaN(element.TimeSeries[0].Value));
            Assert.AreEqual(undoEnabled, element.IsUndoEnabled);
        }
        finally
        {
            if (File.Exists(filename)) File.Delete(filename);
        }
    }

    /// <summary>
    /// Verifies weekly gaps survive the production download and period timestamps shift exactly once.
    /// </summary>
    [STATestMethod]
    public async Task Download_WeeklyMissingDecades_PreservesValuesAndShiftsPeriodsOnce()
    {
        string filename = TemporaryDssPath();
        const string pathname = "/TEST/WEEKLY-DOWNLOAD/FLOW//1Week/GAPS/";
        try
        {
            using (var writer = new DssWriter(filename, 0))
            {
                Assert.AreEqual(0, writer.Write(new Hec.Dss.TimeSeries(pathname,
                    new[] { 10.0 }, new DateTime(2000, 1, 7), "CFS", "PER-AVER")));
                Assert.AreEqual(0, writer.Write(new Hec.Dss.TimeSeries(pathname,
                    new[] { 20.0 }, new DateTime(2040, 1, 6), "CFS", "PER-AVER")));
            }
            var element = new TimeSeriesElement
            {
                EntryMethod = TimeSeriesElement.TimeSeriesEntryMethod.HECDSS,
                HECDSSFullFilename = filename,
                HECDSSDataPathname = pathname,
            };

            await element.Download();

            var valid = element.TimeSeries.Where(item => !double.IsNaN(item.Value)).ToArray();
            CollectionAssert.AreEqual(new[] { 10.0, 20.0 }, valid.Select(item => item.Value).ToArray());
            CollectionAssert.AreEqual(new[] { new DateTime(1999, 12, 31), new DateTime(2039, 12, 30) },
                valid.Select(item => item.Index).ToArray());
            Assert.AreEqual(Numerics.Data.TimeInterval.SevenDay, element.TimeInterval);
            Assert.IsTrue(element.TimeSeries.HasMissingValues);
        }
        finally
        {
            if (File.Exists(filename)) File.Delete(filename);
        }
    }

    /// <summary>
    /// Verifies that the production download includes observations beyond short and long gaps.
    /// </summary>
    /// <param name="lastYear">The year of the second separately stored daily block.</param>
    [STATestMethod]
    [DataRow(2002)]
    [DataRow(2012)]
    [DataRow(2040)]
    public async Task Download_MissingYearBlocks_PreservesBothSeasons(int lastYear)
    {
        string filename = Path.Combine(Path.GetTempPath(), $"BestFit_DssGap_{Guid.NewGuid():N}.dss");
        const string pathname = "/TEST/SEASON/FLOW//1Day/GAPS/";
        try
        {
            using (var writer = new DssWriter(filename, 0))
            {
                Assert.AreEqual(0, writer.Write(new Hec.Dss.TimeSeries(pathname,
                    new[] { 10.0, 11.0, 12.0 }, new DateTime(2000, 3, 1), "CFS", "PER-AVER")));
                Assert.AreEqual(0, writer.Write(new Hec.Dss.TimeSeries(pathname,
                    new[] { 20.0, 21.0, 22.0 }, new DateTime(lastYear, 3, 1), "CFS", "PER-AVER")));
            }

            var element = new TimeSeriesElement
            {
                EntryMethod = TimeSeriesElement.TimeSeriesEntryMethod.HECDSS,
                HECDSSFullFilename = filename,
                HECDSSDataPathname = pathname,
            };

            await element.Download();

            CollectionAssert.AreEqual(new[] { 10.0, 11.0, 12.0, 20.0, 21.0, 22.0 },
                element.TimeSeries.Where(x => !double.IsNaN(x.Value)).Select(x => x.Value).ToArray());
            Assert.AreEqual(new DateTime(2000, 2, 29), element.TimeSeries[0].Index);
            Assert.AreEqual(new DateTime(lastYear, 3, 2), element.TimeSeries.Last().Index);
            Assert.IsTrue(double.IsNaN(element.TimeSeries.Single(x => x.Index == new DateTime(2001, 7, 1)).Value));
            Assert.IsTrue(element.TimeSeries.HasMissingValues);
            Assert.AreEqual("CFS", element.UnitLabel);
        }
        finally
        {
            if (File.Exists(filename)) File.Delete(filename);
        }
    }
}
