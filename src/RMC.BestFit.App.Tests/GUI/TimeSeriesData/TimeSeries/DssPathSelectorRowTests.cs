using Hec.Dss;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RMC_BestFit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RMC.BestFit.App.Tests.GUI.TimeSeriesData.TimeSeries
{
    /// <summary>
    /// Unit and DSS round-trip tests for DSS pathname selector display-range helpers.
    /// </summary>
    [TestClass]
    public class DssPathSelectorRowTests
    {
        /// <summary>
        /// Verifies that a selector row preserves the dateless import pathname.
        /// </summary>
        [TestMethod]
        public void Constructor_WrapsCatalogPathAndDatelessImportPath()
        {
            var row = new DssPathSelectorRow(new DssPath("/A/B/C/01Jan1968-01Jan2023/1Day/F/"));

            Assert.AreEqual("A", row.Apart);
            Assert.AreEqual("B", row.Bpart);
            Assert.AreEqual("C", row.Cpart);
            Assert.AreEqual("01Jan1968-01Jan2023", row.DisplayDpart);
            Assert.AreEqual("01Jan1968-01Jan2023", row.Dpart);
            Assert.AreEqual("01Jan1968-01Jan2023", row.CatalogDpart);
            Assert.AreEqual("1Day", row.Epart);
            Assert.AreEqual("F", row.Fpart);
            Assert.AreEqual("/A/B/C//1Day/F/", row.DatelessPath);
            Assert.IsFalse(row.IsRangeResolved);
        }

        /// <summary>
        /// Verifies that plain range D-parts resolve to a concrete fallback pathname.
        /// </summary>
        [TestMethod]
        public void DisplayReadPath_PlainRangePathUsesFirstDpartDate()
        {
            var row = new DssPathSelectorRow(new DssPath("/A/B/C/01Jan1968-01Jan2023/1Day/F/"));

            Assert.AreEqual("/A/B/C/01Jan1968/1Day/F/", row.DisplayReadPath.FullPath);
            Assert.AreEqual("/A/B/C//1Day/F/", row.DatelessPath);
        }

        /// <summary>
        /// Verifies that condensed catalog paths resolve to their first concrete block path.
        /// </summary>
        [TestMethod]
        public void DisplayReadPath_CondensedPathUsesFirstConcreteDpart()
        {
            var catalogPath = new DssPathCondensed(
                "A",
                "B",
                "C",
                new List<string> { "01Jan1968", "01Jan1969", "01Jan1970" },
                "1Day",
                "F",
                RecordType.RegularTimeSeries);
            var row = new DssPathSelectorRow(catalogPath);

            Assert.AreEqual("/A/B/C/01Jan1968/1Day/F/", row.DisplayReadPath.FullPath);
            Assert.AreEqual("/A/B/C//1Day/F/", row.DatelessPath);
        }

        /// <summary>
        /// Verifies that resolved rows replace catalog block ranges without mutating the original row.
        /// </summary>
        [TestMethod]
        public void WithRangeResult_CreatesResolvedReplacementRow()
        {
            var row = new DssPathSelectorRow(new DssPath("/A/B/C/01Jan1968-01Jan2023/1Day/F/"));
            var result = DssPathRangeResult.FromEndpoints(
                new DateTime(1968, 7, 17),
                new DateTime(2023, 5, 22));

            var resolved = row.WithRangeResult(result);

            Assert.AreEqual("01Jan1968-01Jan2023", row.DisplayDpart);
            Assert.IsFalse(row.IsRangeResolved);
            Assert.AreEqual("17Jul1968-22May2023", resolved.DisplayDpart);
            Assert.AreEqual("/A/B/C/17Jul1968-22May2023/1Day/F/", resolved.DisplayPath);
            Assert.IsTrue(resolved.IsRangeResolved);
            StringAssert.Contains(resolved.RangeResolutionMessage, "Data range");
        }

        /// <summary>
        /// Verifies that DSS-returned first and last timestamps drive the display range.
        /// </summary>
        [TestMethod]
        public void FromTimeSeries_UsesReturnedFirstAndLastTimestamps()
        {
            var result = DssPathRangeResult.FromTimeSeries(new Hec.Dss.TimeSeries
            {
                Path = new DssPath("/A/B/C//1Day/F/"),
                Values = new[] { -901.0, 10.0, 11.0, -902.0 },
                Times = new[]
                {
                    new DateTime(1968, 7, 17),
                    new DateTime(1968, 7, 18),
                    new DateTime(2023, 5, 21),
                    new DateTime(2023, 5, 22),
                },
            });

            Assert.IsTrue(result.HasDisplayDpart);
            Assert.AreEqual("17Jul1968-22May2023", result.DisplayDpart);
        }

        /// <summary>
        /// Verifies that irregular timestamp ranges use actual returned timestamps.
        /// </summary>
        [TestMethod]
        public void FromTimeSeries_IrregularRecordDisplaysActualFirstAndLastTimestamps()
        {
            var result = DssPathRangeResult.FromTimeSeries(new Hec.Dss.TimeSeries
            {
                Path = new DssPath("/A/B/C//IR-Day/F/"),
                Values = new[] { -901.0, 10.0, -902.0 },
                Times = new[]
                {
                    new DateTime(2020, 1, 1),
                    new DateTime(2020, 7, 17),
                    new DateTime(2020, 12, 31),
                },
            });

            Assert.IsTrue(result.HasDisplayDpart);
            Assert.AreEqual("01Jan2020-31Dec2020", result.DisplayDpart);
        }

        /// <summary>
        /// Verifies that a one-timestamp record displays one date rather than a range.
        /// </summary>
        [TestMethod]
        public void FromTimes_SameTimestampDisplaysSingleDate()
        {
            var result = DssPathRangeResult.FromTimes(new[] { new DateTime(2020, 1, 1) });

            Assert.AreEqual("01Jan2020", result.DisplayDpart);
        }

        /// <summary>
        /// Verifies that non-midnight timestamps include time in the display range.
        /// </summary>
        [TestMethod]
        public void FromTimes_NonMidnightTimestampsIncludeTime()
        {
            var result = DssPathRangeResult.FromTimes(new[]
            {
                new DateTime(2020, 1, 1, 6, 30, 0),
                new DateTime(2020, 1, 1, 12, 45, 15),
            });

            Assert.AreEqual("01Jan2020 06:30-01Jan2020 12:45:15", result.DisplayDpart);
        }

        /// <summary>
        /// Verifies that empty range resolution displays a no-data marker.
        /// </summary>
        [TestMethod]
        public void FromTimes_EmptyTimestampsDisplaysNoData()
        {
            var result = DssPathRangeResult.FromTimes(Array.Empty<DateTime>());

            Assert.IsTrue(result.HasDisplayDpart);
            Assert.AreEqual("No data", result.DisplayDpart);
        }

        /// <summary>
        /// Verifies that the resolver tries catalog, dateless, then concrete block paths and caches by dateless pathname.
        /// </summary>
        [TestMethod]
        public void Resolve_UsesFallbackReadOrderAndCachesDatelessPath()
        {
            var calls = new List<string>();
            var catalogPath = new DssPath("/A/B/C/01Jan1968-01Jan2023/1Day/F/");
            var resolver = new DssPathRangeResolver(readPath =>
            {
                calls.Add(readPath.FullPath);
                if (calls.Count == 1)
                {
                    throw new InvalidOperationException("catalog read failed");
                }

                return new Hec.Dss.TimeSeries
                {
                    Times = new[] { new DateTime(1968, 7, 17), new DateTime(2023, 5, 22) },
                    Values = new[] { 1.0, 2.0 },
                };
            });

            var first = resolver.Resolve(catalogPath);
            var second = resolver.Resolve(catalogPath);

            Assert.AreEqual("17Jul1968-22May2023", first.DisplayDpart);
            Assert.AreSame(first, second);
            CollectionAssert.AreEqual(
                new[]
                {
                    "/A/B/C/01Jan1968-01Jan2023/1Day/F/",
                    "/A/B/C//1Day/F/",
                },
                calls);
            Assert.AreEqual(1, resolver.CachedRangeCount);
        }

        /// <summary>
        /// Verifies that all read failures preserve the catalog D-part when applied to a row.
        /// </summary>
        [TestMethod]
        public void Resolve_AllReadPathsFail_PreservesCatalogDpart()
        {
            var catalogPath = new DssPath("/A/B/C/01Jan1968-01Jan2023/1Day/F/");
            var resolver = new DssPathRangeResolver(readPath =>
                throw new InvalidOperationException($"failed {readPath.FullPath}"));

            var row = new DssPathSelectorRow(catalogPath).WithRangeResult(resolver.Resolve(catalogPath));

            Assert.AreEqual("01Jan1968-01Jan2023", row.DisplayDpart);
            StringAssert.Contains(row.RangeResolutionMessage, "Unable to resolve data range");
            StringAssert.Contains(row.RangeResolutionMessage, "/A/B/C/01Jan1968-01Jan2023/1Day/F/");
        }

        /// <summary>
        /// Verifies selector range display against a real DSS catalog/read round trip.
        /// </summary>
        [TestMethod]
        public void Resolve_RealDssRoundTrip_DisplaysActualReturnedRangeAndKeepsDatelessPath()
        {
            string dssFileName = Path.Combine(
                Path.GetTempPath(),
                $"BestFit_DssSelector_{Guid.NewGuid():N}.dss");
            try
            {
                DateTime startDate = new DateTime(1968, 7, 17);
                double[] values = Enumerable.Range(0, 370).Select(i => (double)i).ToArray();
                var source = new Hec.Dss.TimeSeries(
                    "//ALAMO/FLOW-RES IN//1Day/WCDU/",
                    values,
                    startDate,
                    "cfs",
                    "PER-AVER");

                using (var writer = new DssWriter(dssFileName, 0))
                {
                    Assert.AreEqual(0, writer.Write(source));
                }

                DssPath catalogPath;
                DssPathRangeResult result;
                using (var reader = new DssReader(dssFileName, 0))
                {
                    var matches = reader.GetCatalog().FilterByPart(
                        bPart: "ALAMO",
                        cPart: "FLOW-RES IN",
                        ePart: "1Day",
                        fPart: "WCDU");
                    Assert.AreEqual(1, matches.Count);

                    catalogPath = matches[0];
                    var resolver = new DssPathRangeResolver(readPath => reader.GetTimeSeries(readPath));
                    result = resolver.Resolve(catalogPath);
                }

                var row = new DssPathSelectorRow(catalogPath).WithRangeResult(result);

                Assert.AreNotEqual(catalogPath.Dpart, row.DisplayDpart);
                Assert.AreEqual("17Jul1968-21Jul1969", row.DisplayDpart);
                Assert.AreEqual("//ALAMO/FLOW-RES IN//1Day/WCDU/", row.DatelessPath);
                Assert.AreEqual("//ALAMO/FLOW-RES IN/17Jul1968-21Jul1969/1Day/WCDU/", row.DisplayPath);
            }
            finally
            {
                if (File.Exists(dssFileName))
                {
                    File.Delete(dssFileName);
                }
            }
        }
    }
}
