using DatabaseManager;
using Numerics;
using Numerics.Distributions;
using Numerics.Sampling.MCMC;
using System;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;

namespace RMC.BestFit.UI
{
    /// <summary>
    /// Provides safe SQLite serialization helpers for analysis result artifacts.
    /// </summary>
    /// <remarks>
    /// The UI wrappers persist MCMC results as compressed byte arrays and post-processing
    /// results as XML strings. These helpers centralize the null/empty/corrupt-cell handling
    /// so project files with stale result columns do not abort Open().
    /// </remarks>
    internal static class AnalysisPersistenceHelper
    {
        /// <summary>
        /// Gets a string cell value from a table view.
        /// </summary>
        /// <param name="dtView">The table view to read.</param>
        /// <param name="columnName">The column name to read.</param>
        /// <param name="rowIndex">The row index to read.</param>
        /// <returns>The cell text, or an empty string when the column or value is absent.</returns>
        public static string GetString(DataTableView dtView, string columnName, int rowIndex)
        {
            if (dtView.ColumnNames.Contains(columnName) == false)
            {
                return string.Empty;
            }

            return dtView.GetCell(columnName, rowIndex)?.ToString() ?? string.Empty;
        }

        /// <summary>
        /// Attempts to parse an XML cell from a table view.
        /// </summary>
        /// <param name="dtView">The table view to read.</param>
        /// <param name="columnName">The column name to read.</param>
        /// <param name="rowIndex">The row index to read.</param>
        /// <param name="ownerName">The owning element name used in diagnostic messages.</param>
        /// <returns>The parsed XML element, or <c>null</c> when the cell is empty or invalid.</returns>
        public static XElement TryLoadXElement(DataTableView dtView, string columnName, int rowIndex, string ownerName)
        {
            string xml = GetString(dtView, columnName, rowIndex);
            if (string.IsNullOrWhiteSpace(xml))
            {
                return null;
            }

            try
            {
                return XElement.Parse(xml);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load {columnName} XML for '{ownerName}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Attempts to restore serialized MCMC results from a table row.
        /// </summary>
        /// <param name="dtView">The table view to read.</param>
        /// <param name="rowIndex">The row index to read.</param>
        /// <param name="ownerName">The owning element name used in diagnostic messages.</param>
        /// <param name="columnName">The MCMC results column name.</param>
        /// <returns>The restored MCMC results, or <c>null</c> when no valid blob exists.</returns>
        public static MCMCResults TryLoadMCMCResults(
            DataTableView dtView,
            int rowIndex,
            string ownerName,
            string columnName = nameof(MCMCResults))
        {
            if (dtView.ColumnNames.Contains(columnName) == false)
            {
                return null;
            }

            try
            {
                object cell = dtView.GetCell(columnName, rowIndex);
                if (cell is not byte[] compressedBytes || compressedBytes.Length == 0)
                {
                    return null;
                }

                byte[] bytes = Tools.Decompress(compressedBytes);
                if (bytes == null || bytes.Length == 0)
                {
                    return null;
                }

                return MCMCResults.FromByteArray(bytes);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load {columnName} for '{ownerName}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Attempts to restore uncertainty analysis results from a table row.
        /// </summary>
        /// <param name="dtView">The table view to read.</param>
        /// <param name="columnName">The results column name.</param>
        /// <param name="rowIndex">The row index to read.</param>
        /// <param name="ownerName">The owning element name used in diagnostic messages.</param>
        /// <returns>The restored uncertainty results, or <c>null</c> when no valid XML exists.</returns>
        public static UncertaintyAnalysisResults TryLoadAnalysisResults(
            DataTableView dtView,
            string columnName,
            int rowIndex,
            string ownerName)
        {
            XElement xElement = TryLoadXElement(dtView, columnName, rowIndex, ownerName);
            if (xElement == null)
            {
                return null;
            }

            try
            {
                return UncertaintyAnalysisResults.FromXElement(xElement);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load {columnName} for '{ownerName}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Serializes MCMC results for SQLite storage.
        /// </summary>
        /// <param name="results">The MCMC results to serialize.</param>
        /// <returns>A compressed byte array, or an empty byte array when <paramref name="results"/> is <c>null</c>.</returns>
        public static byte[] SerializeMCMCResults(MCMCResults results)
        {
            return results != null
                ? Tools.Compress(MCMCResults.ToByteArray(results))
                : Array.Empty<byte>();
        }

        /// <summary>
        /// Serializes uncertainty analysis results for SQLite storage.
        /// </summary>
        /// <param name="results">The uncertainty results to serialize.</param>
        /// <returns>An XML string, or an empty string when <paramref name="results"/> is <c>null</c>.</returns>
        public static string SerializeAnalysisResults(UncertaintyAnalysisResults results)
        {
            return results != null ? results.ToXElement().ToString() : string.Empty;
        }
    }
}
