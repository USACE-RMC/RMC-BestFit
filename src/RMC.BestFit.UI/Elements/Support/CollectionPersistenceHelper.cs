using DatabaseManager;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RMC.BestFit.UI
{
    /// <summary>
    /// Provides safe SQLite load-entry helpers for element collections.
    /// </summary>
    /// <remarks>
    /// Collection tables can become stale independently from subtype tables. These helpers
    /// implement a consistent first-row-wins repair policy while preserving true distinct
    /// element-name conflicts for the normal validation layer.
    /// </remarks>
    internal static class CollectionPersistenceHelper
    {
        /// <summary>
        /// Extracts an unqualified class name from a fully qualified type name.
        /// </summary>
        /// <param name="fullyQualifiedType">The fully qualified type name.</param>
        /// <returns>The unqualified class name, or an empty string when the input is blank.</returns>
        public static string GetClassName(string fullyQualifiedType)
        {
            if (string.IsNullOrWhiteSpace(fullyQualifiedType))
            {
                return string.Empty;
            }

            int lastDot = fullyQualifiedType.LastIndexOf('.');
            return lastDot >= 0 ? fullyQualifiedType.Substring(lastDot + 1) : fullyQualifiedType;
        }

        /// <summary>
        /// Builds load entries from a single-table collection using first duplicate name wins.
        /// </summary>
        /// <param name="dtView">The collection table view.</param>
        /// <param name="needsRewrite">Receives <c>true</c> when blank or duplicate rows were skipped.</param>
        /// <returns>The ordered loadable element names.</returns>
        public static List<string> BuildSingleTableLoadEntries(DataTableView dtView, out bool needsRewrite)
        {
            needsRewrite = false;
            var entries = new List<string>();
            var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < dtView.NumberOfRows; i++)
            {
                string elementName = ReadName(dtView, i);
                if (string.IsNullOrWhiteSpace(elementName))
                {
                    needsRewrite = true;
                    continue;
                }

                if (seenNames.Add(elementName) == false)
                {
                    needsRewrite = true;
                    continue;
                }

                entries.Add(elementName);
            }

            return entries;
        }

        /// <summary>
        /// Builds typed parent-index load entries using first duplicate name/type pair wins.
        /// </summary>
        /// <param name="sqlite">The SQLite project connection.</param>
        /// <param name="names">The parent-index names.</param>
        /// <param name="types">The parent-index type names.</param>
        /// <param name="subtypeTablesByClassName">A map from unqualified class name to subtype table name.</param>
        /// <param name="validateStoredRows">Whether each parent row must have a matching subtype row.</param>
        /// <param name="defaultElementType">Optional default type for legacy rows with missing type data.</param>
        /// <param name="needsRewrite">Receives <c>true</c> when malformed, duplicate, or orphan rows were skipped.</param>
        /// <returns>The ordered loadable element name/type pairs.</returns>
        public static List<(string ElementName, string ElementType)> BuildTypedLoadEntries(
            SQLiteManager sqlite,
            string[] names,
            string[] types,
            IReadOnlyDictionary<string, string> subtypeTablesByClassName,
            bool validateStoredRows,
            string defaultElementType,
            out bool needsRewrite)
        {
            needsRewrite = names.Length != types.Length;
            var entries = new List<(string ElementName, string ElementType)>();
            var seenEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int count = Math.Max(names.Length, types.Length);

            for (int i = 0; i < count; i++)
            {
                string elementName = i < names.Length ? names[i] ?? string.Empty : string.Empty;
                string elementType = i < types.Length ? types[i] ?? string.Empty : string.Empty;
                if (string.IsNullOrWhiteSpace(elementType))
                {
                    elementType = defaultElementType ?? string.Empty;
                }

                string className = GetClassName(elementType);
                if (string.IsNullOrWhiteSpace(elementName) ||
                    string.IsNullOrWhiteSpace(className) ||
                    subtypeTablesByClassName.ContainsKey(className) == false)
                {
                    needsRewrite = true;
                    continue;
                }

                string key = className + "\0" + elementName;
                if (seenEntries.Add(key) == false)
                {
                    needsRewrite = true;
                    continue;
                }

                if (validateStoredRows &&
                    StoredElementExists(sqlite, elementName, className, subtypeTablesByClassName) == false)
                {
                    needsRewrite = true;
                    continue;
                }

                entries.Add((elementName, elementType));
            }

            return entries;
        }

        /// <summary>
        /// Determines whether a table contains a row with the specified element name.
        /// </summary>
        /// <param name="sqlite">The SQLite project connection.</param>
        /// <param name="tableName">The table name to search.</param>
        /// <param name="elementName">The element name to find.</param>
        /// <returns><c>true</c> when a matching row exists; otherwise, <c>false</c>.</returns>
        public static bool NamedRowExists(SQLiteManager sqlite, string tableName, string elementName)
        {
            if (string.IsNullOrWhiteSpace(elementName) ||
                sqlite.TableNames.Contains(tableName) == false)
            {
                return false;
            }

            DataTableView dtView = sqlite.GetTableManager(tableName);
            if (dtView.ColumnNames.Contains("Name") == false)
            {
                return false;
            }

            return dtView.SearchColumn(0, dtView.NumberOfRows - 1, "Name", elementName, true, true) != -1;
        }

        /// <summary>
        /// Determines whether a typed element's subtype table contains the specified element.
        /// </summary>
        /// <param name="sqlite">The SQLite project connection.</param>
        /// <param name="elementName">The element name to find.</param>
        /// <param name="className">The unqualified element class name.</param>
        /// <param name="subtypeTablesByClassName">A map from unqualified class name to subtype table name.</param>
        /// <returns><c>true</c> when a matching subtype row exists; otherwise, <c>false</c>.</returns>
        public static bool StoredElementExists(
            SQLiteManager sqlite,
            string elementName,
            string className,
            IReadOnlyDictionary<string, string> subtypeTablesByClassName)
        {
            if (subtypeTablesByClassName.TryGetValue(className, out string tableName) == false)
            {
                return false;
            }

            return NamedRowExists(sqlite, tableName, elementName);
        }

        /// <summary>
        /// Reads an element name from a single-table collection row.
        /// </summary>
        /// <param name="dtView">The table view to read.</param>
        /// <param name="rowIndex">The row index to read.</param>
        /// <returns>The row name, or an empty string when absent.</returns>
        private static string ReadName(DataTableView dtView, int rowIndex)
        {
            if (dtView.ColumnNames.Contains("Name"))
            {
                return dtView.GetCell("Name", rowIndex)?.ToString() ?? string.Empty;
            }

            object[] row = dtView.GetRow(rowIndex);
            return row.Length != 0 ? row[0]?.ToString() ?? string.Empty : string.Empty;
        }
    }
}
