using Hec.Dss;
using System;
using System.Collections.Generic;

namespace RMC_BestFit
{
    /// <summary>
    /// Resolves and caches display ranges for DSS selector rows.
    /// </summary>
    /// <remarks>
    /// The cache is keyed by dateless pathname because that is the path passed to the
    /// RMC-BestFit import workflow. Reads try multiple DSS path forms because the HEC-DSS
    /// wrapper accepts different forms depending on catalog/path metadata.
    /// </remarks>
    internal sealed class DssPathRangeResolver
    {
        /// <summary>
        /// Delegate used to read a DSS time series by pathname.
        /// </summary>
        private readonly Func<DssPath, TimeSeries> _readTimeSeries;

        /// <summary>
        /// Cached range results by dateless pathname.
        /// </summary>
        private readonly Dictionary<string, DssPathRangeResult> _cache = new Dictionary<string, DssPathRangeResult>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Lock used to protect cache access from background range-resolution tasks.
        /// </summary>
        private readonly object _cacheLock = new object();

        /// <summary>
        /// Initializes a new instance of the <see cref="DssPathRangeResolver"/> class.
        /// </summary>
        /// <param name="readTimeSeries">The function used to read a DSS time series by pathname.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="readTimeSeries"/> is null.</exception>
        internal DssPathRangeResolver(Func<DssPath, TimeSeries> readTimeSeries)
        {
            _readTimeSeries = readTimeSeries ?? throw new ArgumentNullException(nameof(readTimeSeries));
        }

        /// <summary>
        /// Gets the number of cached dateless pathname ranges.
        /// </summary>
        internal int CachedRangeCount
        {
            get
            {
                lock (_cacheLock)
                {
                    return _cache.Count;
                }
            }
        }

        /// <summary>
        /// Clears all cached range results.
        /// </summary>
        internal void Clear()
        {
            lock (_cacheLock)
            {
                _cache.Clear();
            }
        }

        /// <summary>
        /// Resolves the display range for a DSS pathname.
        /// </summary>
        /// <param name="catalogPath">The DSS catalog pathname selected by the user.</param>
        /// <returns>The resolved display range result.</returns>
        internal DssPathRangeResult Resolve(DssPath catalogPath)
        {
            if (catalogPath == null)
            {
                return DssPathRangeResult.Error("Unable to resolve data range: DSS catalog pathname is missing.");
            }

            string datelessPath = catalogPath.PathWithoutDate;
            if (string.IsNullOrWhiteSpace(datelessPath))
            {
                return DssPathRangeResult.Error("Unable to resolve data range: DSS pathname is empty.");
            }

            lock (_cacheLock)
            {
                if (_cache.TryGetValue(datelessPath, out DssPathRangeResult cached))
                {
                    return cached;
                }
            }

            DssPathRangeResult result = ResolveUncached(catalogPath);

            lock (_cacheLock)
            {
                if (!_cache.ContainsKey(datelessPath))
                {
                    _cache[datelessPath] = result;
                }
                else
                {
                    result = _cache[datelessPath];
                }
            }

            return result;
        }

        /// <summary>
        /// Resolves a display range without checking or updating the cache.
        /// </summary>
        /// <param name="catalogPath">The DSS catalog pathname selected by the user.</param>
        /// <returns>The resolved display range result.</returns>
        private DssPathRangeResult ResolveUncached(DssPath catalogPath)
        {
            var failures = new List<string>();
            foreach (DssPath readPath in CreateReadPaths(catalogPath))
            {
                try
                {
                    DssPathRangeResult result = DssPathRangeResult.FromTimeSeries(_readTimeSeries(readPath));
                    if (result.HasDisplayDpart)
                    {
                        return result;
                    }

                    failures.Add($"{readPath.FullPath}: {result.Message}");
                    System.Diagnostics.Debug.WriteLine($"DssPathRangeResolver.Resolve '{readPath.FullPath}': {result.Message}");
                }
                catch (Exception ex)
                {
                    failures.Add($"{readPath.FullPath}: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"DssPathRangeResolver.Resolve '{readPath.FullPath}': {ex.Message}");
                }
            }

            string message = failures.Count == 0
                ? "Unable to resolve data range: no DSS read paths were available."
                : "Unable to resolve data range. " + string.Join(" | ", failures);
            return DssPathRangeResult.Error(message);
        }

        /// <summary>
        /// Creates the DSS path forms used to resolve a selector display range.
        /// </summary>
        /// <param name="catalogPath">The DSS catalog pathname selected by the user.</param>
        /// <returns>The ordered read path candidates.</returns>
        internal static IReadOnlyList<DssPath> CreateReadPaths(DssPath catalogPath)
        {
            if (catalogPath == null)
            {
                return Array.Empty<DssPath>();
            }

            var readPaths = new List<DssPath>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddReadPath(readPaths, seen, catalogPath);
            AddReadPath(readPaths, seen, new DssPath(catalogPath.PathWithoutDate));
            AddReadPath(readPaths, seen, DssPathSelectorRow.CreateDisplayReadPath(catalogPath));
            return readPaths;
        }

        /// <summary>
        /// Adds a read path when it has not already been added.
        /// </summary>
        /// <param name="readPaths">The read path list being built.</param>
        /// <param name="seen">The set of path strings already added.</param>
        /// <param name="path">The candidate path.</param>
        private static void AddReadPath(List<DssPath> readPaths, HashSet<string> seen, DssPath path)
        {
            if (path == null) return;

            string fullPath = path.FullPath;
            if (string.IsNullOrWhiteSpace(fullPath)) return;

            if (seen.Add(fullPath))
            {
                readPaths.Add(path);
            }
        }
    }
}
