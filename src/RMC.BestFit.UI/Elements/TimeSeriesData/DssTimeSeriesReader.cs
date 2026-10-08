using Hec.Dss;
using Numerics.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using DssSeries = Hec.Dss.TimeSeries;

namespace RMC.BestFit.UI
{
    /// <summary>
    /// Reads complete DSS series without sizing a regular read across absent storage blocks.
    /// </summary>
    /// <remarks>
    /// The native size query counts stored blocks, whereas regular retrieval also emits missing
    /// timesteps between blocks. Explicit single-block windows avoid an undersized output buffer.
    /// Timestamp conversion and missing-timestep normalization remain owned by TimeSeriesElement.
    /// </remarks>
    internal static class DssTimeSeriesReader
    {
        /// <summary>
        /// Reads all cataloged blocks belonging to a logical time series.
        /// </summary>
        /// <param name="reader">The open reader, owned by the caller.</param>
        /// <param name="path">The concrete, condensed, or dateless pathname.</param>
        /// <param name="cancellationToken">Cancellation checked between native reads.</param>
        /// <returns>The combined raw DSS ordinates and compatible metadata.</returns>
        /// <exception cref="ArgumentNullException">Thrown when reader or path is null.</exception>
        /// <exception cref="InvalidDataException">Thrown when a block cannot be read or combined safely.</exception>
        /// <exception cref="OperationCanceledException">Thrown when cancellation is requested.</exception>
        internal static DssSeries Read(DssReader reader, DssPath path, CancellationToken cancellationToken = default)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));
            if (path == null) throw new ArgumentNullException(nameof(path));
            cancellationToken.ThrowIfCancellationRequested();

            // Irregular retrieval returns explicitly stored times rather than a dense gap grid.
            if (!path.IsRegular) return reader.GetTimeSeries(path);

            if (TimeSeriesElement.ResolveDssTimeInterval(new DssSeries { Path = path }) == TimeInterval.SevenDay)
            {
                DateTime? weeklyAnchor = FindWeeklyAnchor(reader, path, cancellationToken);
                if (!weeklyAnchor.HasValue) return reader.GetTimeSeries(path);
                return ReadBlocks(path, reader.GetCatalog().UnCondensedPaths,
                    (block, start, end) => reader.GetTimeSeries(block, start, end,
                        TimeWindow.TimeWindowBehavior.StrictlyInclusive, TimeWindow.ConsecutiveValueCompression.None),
                    cancellationToken, weeklyAnchor);
            }

            return ReadBlocks(path, reader.GetCatalog().UnCondensedPaths,
                (block, start, end) => reader.GetTimeSeries(block, start, end,
                    TimeWindow.TimeWindowBehavior.StrictlyInclusive, TimeWindow.ConsecutiveValueCompression.None),
                cancellationToken);
        }

        /// <summary>
        /// Combines regular block reads supplied by an open data source.
        /// </summary>
        /// <param name="path">The logical time-series identity.</param>
        /// <param name="catalog">The source's concrete catalog records.</param>
        /// <param name="readBlock">The operation that reads an explicit inclusive window.</param>
        /// <param name="cancellationToken">Cancellation checked before and after each read.</param>
        /// <param name="weeklyAnchor">The series-wide weekly grid phase, when applicable.</param>
        /// <returns>A chronologically ordered series retaining raw timestamps and missing flags.</returns>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        /// <exception cref="InvalidDataException">Thrown when catalog, arrays, or metadata are inconsistent.</exception>
        /// <exception cref="OperationCanceledException">Thrown when cancellation is requested.</exception>
        internal static DssSeries ReadBlocks(DssPath path, IEnumerable<DssPath> catalog,
            Func<DssPath, DateTime, DateTime, DssSeries> readBlock, CancellationToken cancellationToken = default,
            DateTime? weeklyAnchor = null)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (readBlock == null) throw new ArgumentNullException(nameof(readBlock));
            cancellationToken.ThrowIfCancellationRequested();

            var result = new DssSeries { Path = new DssPath(path.PathWithoutDate) };
            TimeInterval interval = TimeSeriesElement.ResolveDssTimeInterval(result);
            var blocks = catalog.Where(block => DssPath.EqualsDateLess(block, path))
                .OrderBy(block => block.DPartAsDateTime).ToList();
            if (blocks.Count == 0)
                throw new InvalidDataException($"No cataloged DSS blocks match '{path.FullPath}'.");

            var ordinates = new List<(DateTime Time, double Value)>();
            foreach (DssPath block in blocks)
            {
                cancellationToken.ThrowIfCancellationRequested();
                DssSeries chunk;
                try
                {
                    if (!block.TryParseDPartAsDateTime(out DateTime start))
                        throw new InvalidDataException("The catalog block does not have a valid start date.");

                    // DSS midnight at the ending boundary belongs to this block. Excluding
                    // the starting midnight avoids retrieving the previous block's last value.
                    DateTime readStart = start.AddSeconds(1);
                    DateTime blockEnd = GetBlockEnd(start, interval);
                    DateTime readEnd = blockEnd;
                    if (weeklyAnchor.HasValue && ReferenceEquals(block, blocks[0]))
                    {
                        readStart = NextWeeklyTime(start, weeklyAnchor.Value).AddSeconds(1);
                    }
                    chunk = readBlock(block, readStart, readEnd);
                    cancellationToken.ThrowIfCancellationRequested();
                    ValidateArrays(chunk);
                    ValidateEmptyBlock(chunk);
                    result.Units = MergeMetadata(result.Units, chunk.Units, "units");
                    result.DataType = MergeMetadata(result.DataType, chunk.DataType, "data types");
                    if (result.LocationInformation == null) result.LocationInformation = chunk.LocationInformation;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw new InvalidDataException($"Cannot import DSS block '{block.FullPath}': {ex.Message}", ex);
                }

                for (int i = 0; i < chunk.Values.Length; i++)
                {
                    if (!weeklyAnchor.HasValue ||
                        (chunk.Times[i] > block.DPartAsDateTime && chunk.Times[i] <= GetBlockEnd(block.DPartAsDateTime, interval)))
                    {
                        ordinates.Add((chunk.Times[i], chunk.Values[i]));
                    }
                }
            }

            ordinates.Sort((left, right) => left.Time.CompareTo(right.Time));
            for (int i = 1; i < ordinates.Count; i++)
            {
                if (ordinates[i].Time == ordinates[i - 1].Time)
                    throw new InvalidDataException($"DSS series '{path.FullPath}' returned duplicate timestamp '{ordinates[i].Time:o}'.");
            }
            cancellationToken.ThrowIfCancellationRequested();
            result.Times = ordinates.Select(ordinate => ordinate.Time).ToArray();
            result.Values = ordinates.Select(ordinate => ordinate.Value).ToArray();
            return result;
        }

        /// <summary>
        /// Finds the series-wide weekly phase from the first cataloged block containing data.
        /// </summary>
        /// <param name="reader">The open reader used for time-series retrieval.</param>
        /// <param name="path">The logical weekly pathname.</param>
        /// <param name="cancellationToken">Cancellation checked between record probes.</param>
        /// <returns>The first stored weekly timestamp, or null when every block is empty.</returns>
        /// <exception cref="InvalidDataException">Thrown when a catalog record cannot be inspected safely.</exception>
        /// <exception cref="OperationCanceledException">Thrown when cancellation is requested.</exception>
        /// <remarks>
        /// The configured Hec.Dss wrapper does not expose record-local date ranges. Its explicit
        /// weekly retrieval also derives the returned grid from the requested start date. The
        /// native record range locates a narrow probe window; the timestamp returned by the normal
        /// wrapper supplies the actual phase used for all subsequent block reads.
        /// </remarks>
        private static DateTime? FindWeeklyAnchor(DssReader reader, DssPath path, CancellationToken cancellationToken)
        {
            var blocks = reader.GetCatalog().UnCondensedPaths.Where(block => DssPath.EqualsDateLess(block, path))
                .OrderBy(block => block.DPartAsDateTime).ToList();
            if (blocks.Count == 0)
                throw new InvalidDataException($"No cataloged DSS blocks match '{path.FullPath}'.");
            using var rangeReader = new DssRecordRangeReader(reader.Filename);
            foreach (DssPath block in blocks)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    DateTime firstRangeDate = rangeReader.GetFirstValidDate(block);
                    DssSeries probe = reader.GetTimeSeries(block, firstRangeDate.AddDays(-1), firstRangeDate.AddDays(7),
                        TimeWindow.TimeWindowBehavior.StrictlyInclusive, TimeWindow.ConsecutiveValueCompression.None);
                    cancellationToken.ThrowIfCancellationRequested();
                    ValidateArrays(probe);
                    ValidateEmptyBlock(probe);
                    if (probe.Times.Length > 0) return probe.Times[0];
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw new InvalidDataException($"Cannot inspect DSS block '{block.FullPath}': {ex.Message}", ex);
                }
            }
            return null;
        }

        /// <summary>
        /// Gets the first weekly grid point strictly after a storage-block boundary.
        /// </summary>
        /// <param name="boundary">The starting boundary excluded from the block.</param>
        /// <param name="anchor">Any timestamp on the series-wide weekly grid.</param>
        /// <returns>The next timestamp on the anchored seven-day grid.</returns>
        private static DateTime NextWeeklyTime(DateTime boundary, DateTime anchor)
        {
            long weekTicks = 7 * TimeSpan.TicksPerDay;
            long remainder = PositiveRemainder(boundary.Ticks - anchor.Ticks, weekTicks);
            return boundary.AddTicks(remainder == 0 ? weekTicks : weekTicks - remainder);
        }

        /// <summary>
        /// Computes a nonnegative remainder for signed timestamp differences.
        /// </summary>
        /// <param name="value">The signed tick difference.</param>
        /// <param name="divisor">The positive grid length in ticks.</param>
        /// <returns>A value from zero through divisor minus one.</returns>
        private static long PositiveRemainder(long value, long divisor)
        {
            long remainder = value % divisor;
            return remainder < 0 ? remainder + divisor : remainder;
        }

        /// <summary>
        /// Gets the exclusive calendar successor of a regular DSS storage block's start date.
        /// </summary>
        /// <param name="start">The native catalog block start.</param>
        /// <param name="interval">The resolved regular data interval.</param>
        /// <returns>The next block boundary, included in the current block's read window.</returns>
        /// <exception cref="InvalidDataException">Thrown when the interval has no supported block size.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the boundary exceeds DateTime's range.</exception>
        /// <remarks>
        /// Uses calendar increments, not fixed numbers of seconds, to retain leap days and month lengths.
        /// Reference: USACE HEC-DSS C Programmer's Guide, Time Series Data, Regular-Interval Data.
        /// </remarks>
        private static DateTime GetBlockEnd(DateTime start, TimeInterval interval)
        {
            switch (interval)
            {
                case TimeInterval.OneMinute:
                case TimeInterval.FiveMinute:
                    return start.AddDays(1);
                case TimeInterval.FifteenMinute:
                case TimeInterval.ThirtyMinute:
                case TimeInterval.OneHour:
                case TimeInterval.SixHour:
                case TimeInterval.TwelveHour:
                    return start.AddMonths(1);
                case TimeInterval.OneDay:
                    return start.AddYears(1);
                case TimeInterval.SevenDay:
                case TimeInterval.OneMonth:
                case TimeInterval.OneQuarter:
                    return start.AddYears(10);
                case TimeInterval.OneYear:
                    return start.AddYears(100);
                default:
                    throw new InvalidDataException($"No regular DSS block size is defined for '{interval}'.");
            }
        }

        /// <summary>
        /// Rejects malformed native arrays before combining blocks.
        /// </summary>
        /// <param name="chunk">The returned block data.</param>
        /// <exception cref="InvalidDataException">Thrown when arrays are null or have different lengths.</exception>
        private static void ValidateArrays(DssSeries chunk)
        {
            if (chunk?.Times == null || chunk.Values == null)
                throw new InvalidDataException("DSS returned null Times or Values arrays.");
            if (chunk.Times.Length != chunk.Values.Length)
                throw new InvalidDataException("DSS returned mismatched Times and Values arrays.");
        }

        /// <summary>
        /// Rejects the configured wrapper's failure response without rejecting successfully read missing blocks.
        /// </summary>
        /// <param name="chunk">A block response whose arrays have already been validated.</param>
        /// <exception cref="InvalidDataException">Thrown when the native reader returned its empty failure response.</exception>
        /// <remarks>
        /// Hec.Dss.DssReader.GetTimeSeries assigns a nonnull LocationInformation after every successful
        /// native retrieval, even when no location metadata exists. Trim preserves that object for
        /// all-missing blocks. A failed native retrieval instead returns GetEmptyTimeSeries, which
        /// leaves LocationInformation null. Native regression tests pin this wrapper-specific contract;
        /// it must be revisited when upgrading Hec.Dss because no native status is exposed publicly.
        /// </remarks>
        private static void ValidateEmptyBlock(DssSeries chunk)
        {
            if (chunk.Values.Length == 0 && chunk.LocationInformation == null)
                throw new InvalidDataException("DSS returned an empty failure response for a cataloged block.");
        }

        /// <summary>
        /// Retains the first nonblank metadata value while rejecting incompatible later blocks.
        /// </summary>
        /// <param name="current">The metadata accumulated so far.</param>
        /// <param name="incoming">The next block's metadata.</param>
        /// <param name="label">The metadata name used in error messages.</param>
        /// <returns>The compatible nonblank metadata, or an empty string.</returns>
        /// <exception cref="InvalidDataException">Thrown when nonblank metadata differs between blocks.</exception>
        private static string MergeMetadata(string current, string incoming, string label)
        {
            string candidate = incoming?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(current)) return candidate;
            if (candidate.Length > 0 && !current.Equals(candidate, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"DSS blocks have conflicting {label}: '{current}' and '{candidate}'.");
            return current;
        }

        /// <summary>
        /// Exposes the native record-local range needed to establish a weekly series phase.
        /// </summary>
        private sealed class DssRecordRangeReader : DssReader
        {
            /// <summary>
            /// Opens a quiet, read-only handle to the same DSS file as the importing reader.
            /// </summary>
            /// <param name="filename">The DSS filename.</param>
            internal DssRecordRangeReader(string filename) : base(filename, 0) { }

            /// <summary>
            /// Gets the native date associated with the first valid value in one concrete record.
            /// </summary>
            /// <param name="path">The concrete catalog pathname.</param>
            /// <returns>A date near the first stored weekly timestamp for a narrow wrapper probe.</returns>
            /// <exception cref="InvalidDataException">Thrown when the native range cannot be read.</exception>
            internal DateTime GetFirstValidDate(DssPath path)
            {
                int firstJulian = 0, firstSeconds = 0, lastJulian = 0, lastSeconds = 0;
                int status = NativeMethods.GetTimeSeriesDateRange(dss, path.FullPath, 0,
                    ref firstJulian, ref firstSeconds, ref lastJulian, ref lastSeconds);
                if (status != 0)
                    throw new InvalidDataException($"HEC-DSS returned status {status} while reading the record range.");
                if (firstSeconds < 0 || firstSeconds > 86400)
                    throw new InvalidDataException($"HEC-DSS returned invalid first-value seconds '{firstSeconds}'.");
                return DateTime.FromOADate(firstJulian + 1d).AddSeconds(firstSeconds % 86400);
            }
        }

        /// <summary>
        /// Declares the configured HEC-DSS native record-range operation.
        /// </summary>
        private static class NativeMethods
        {
            /// <summary>
            /// Retrieves the first and last valid times for one concrete DSS record.
            /// </summary>
            /// <param name="dss">The open native DSS handle.</param>
            /// <param name="pathname">The concrete record pathname.</param>
            /// <param name="fullSet">Zero limits the range to the concrete record.</param>
            /// <param name="firstJulian">Receives the first value's Julian date.</param>
            /// <param name="firstSeconds">Receives its seconds within the HEC date.</param>
            /// <param name="lastJulian">Receives the last value's Julian date.</param>
            /// <param name="lastSeconds">Receives its seconds within the HEC date.</param>
            /// <returns>Zero on success; otherwise, a native HEC-DSS status code.</returns>
            [DllImport("hecdss", EntryPoint = "hec_dss_tsGetDateTimeRange", CharSet = CharSet.Ansi, ExactSpelling = true)]
            internal static extern int GetTimeSeriesDateRange(IntPtr dss, string pathname, int fullSet,
                ref int firstJulian, ref int firstSeconds, ref int lastJulian, ref int lastSeconds);
        }
    }
}
