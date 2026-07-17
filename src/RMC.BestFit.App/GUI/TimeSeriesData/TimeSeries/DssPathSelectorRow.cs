using Hec.Dss;
using System;

namespace RMC_BestFit
{
    /// <summary>
    /// View-model row used by the DSS pathname selector grid.
    /// </summary>
    /// <remarks>
    /// The row preserves the original DSS catalog path while allowing the D-part displayed in
    /// the selector to be replaced by the actual data range that will be imported.
    /// </remarks>
    internal sealed class DssPathSelectorRow
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DssPathSelectorRow"/> class.
        /// </summary>
        /// <param name="catalogPath">The DSS catalog path wrapped by this row.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="catalogPath"/> is null.</exception>
        internal DssPathSelectorRow(DssPath catalogPath)
            : this(catalogPath, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DssPathSelectorRow"/> class.
        /// </summary>
        /// <param name="catalogPath">The DSS catalog path wrapped by this row.</param>
        /// <param name="rangeResult">The optional resolved range used for display.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="catalogPath"/> is null.</exception>
        private DssPathSelectorRow(DssPath catalogPath, DssPathRangeResult rangeResult)
        {
            CatalogPath = catalogPath ?? throw new ArgumentNullException(nameof(catalogPath));
            Apart = CatalogPath.Apart;
            Bpart = CatalogPath.Bpart;
            Cpart = CatalogPath.Cpart;
            CatalogDpart = CatalogPath.Dpart;
            DisplayDpart = rangeResult?.HasDisplayDpart == true ? rangeResult.DisplayDpart : CatalogDpart;
            Epart = CatalogPath.Epart;
            Fpart = CatalogPath.Fpart;
            RangeResolutionMessage = rangeResult?.Message ?? $"Catalog block range: {CatalogDpart}";
            IsRangeResolved = rangeResult != null;
        }

        /// <summary>
        /// Gets whether actual DSS timestamps have been resolved for this row.
        /// </summary>
        internal bool IsRangeResolved { get; }

        /// <summary>
        /// Gets the underlying DSS catalog path.
        /// </summary>
        internal DssPath CatalogPath { get; }

        /// <summary>
        /// Gets the original catalog D-part used for filtering and diagnostics.
        /// </summary>
        internal string CatalogDpart { get; }

        /// <summary>
        /// Gets the dateless pathname used by the import workflow.
        /// </summary>
        internal string DatelessPath
        {
            get { return CatalogPath.PathWithoutDate; }
        }

        /// <summary>
        /// Gets the first concrete DSS pathname used by one display range fallback.
        /// </summary>
        /// <remarks>
        /// Kept as a convenience for diagnostics and tests. The resolver itself tries several
        /// path forms, including catalog and dateless paths.
        /// </remarks>
        internal DssPath DisplayReadPath
        {
            get { return CreateDisplayReadPath(CatalogPath); }
        }

        /// <summary>
        /// Gets DSS part A.
        /// </summary>
        public string Apart { get; }

        /// <summary>
        /// Gets DSS part B.
        /// </summary>
        public string Bpart { get; }

        /// <summary>
        /// Gets DSS part C.
        /// </summary>
        public string Cpart { get; }

        /// <summary>
        /// Gets the DSS part D shown in the selector grid.
        /// </summary>
        public string DisplayDpart { get; }

        /// <summary>
        /// Gets the DSS part D shown in the selector grid.
        /// </summary>
        /// <remarks>
        /// Retained as an alias for existing XAML/test bindings.
        /// </remarks>
        public string Dpart
        {
            get { return DisplayDpart; }
        }

        /// <summary>
        /// Gets DSS part E.
        /// </summary>
        public string Epart { get; }

        /// <summary>
        /// Gets DSS part F.
        /// </summary>
        public string Fpart { get; }

        /// <summary>
        /// Gets the display pathname shown in the selector preview.
        /// </summary>
        public string DisplayPath
        {
            get { return new DssPath(Apart, Bpart, Cpart, DisplayDpart, Epart, Fpart, CatalogPath.RecordType).FullPath; }
        }

        /// <summary>
        /// Gets the range-resolution message shown as diagnostic text.
        /// </summary>
        public string RangeResolutionMessage { get; private set; }

        /// <summary>
        /// Creates a new row with resolved display range state.
        /// </summary>
        /// <param name="result">The resolved range result.</param>
        /// <returns>A new selector row with display values derived from <paramref name="result"/>.</returns>
        internal DssPathSelectorRow WithRangeResult(DssPathRangeResult result)
        {
            return result == null ? this : new DssPathSelectorRow(CatalogPath, result);
        }

        /// <summary>
        /// Creates the concrete DSS pathname used to read actual timestamps for display.
        /// </summary>
        /// <param name="catalogPath">The catalog pathname represented by the selector row.</param>
        /// <returns>A pathname with a concrete D-part suitable for DSS time-series reads.</returns>
        /// <remarks>
        /// Condensed and range D-parts are display/catalog concepts. The reader is more reliable
        /// when given the first concrete block D-part and can still retrieve the whole record.
        /// </remarks>
        internal static DssPath CreateDisplayReadPath(DssPath catalogPath)
        {
            if (catalogPath is DssPathCondensed condensedPath && condensedPath.ComprisedDParts.Count > 0)
            {
                return new DssPath(
                    condensedPath.Apart,
                    condensedPath.Bpart,
                    condensedPath.Cpart,
                    condensedPath.ComprisedDParts[0],
                    condensedPath.Epart,
                    condensedPath.Fpart,
                    condensedPath.RecordType);
            }

            if (catalogPath.IsDPartARange())
            {
                string firstDpart = catalogPath.Dpart.Split('-')[0].Trim();
                return new DssPath(
                    catalogPath.Apart,
                    catalogPath.Bpart,
                    catalogPath.Cpart,
                    firstDpart,
                    catalogPath.Epart,
                    catalogPath.Fpart,
                    catalogPath.RecordType);
            }

            return catalogPath;
        }
    }
}
