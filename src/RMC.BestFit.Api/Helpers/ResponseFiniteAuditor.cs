using System.Collections;
using System.Reflection;

namespace RMC.BestFit.Api.Helpers
{
    /// <summary>
    /// Walks a response DTO graph and reports any <see cref="double"/> values that are
    /// <see cref="double.PositiveInfinity"/> or <see cref="double.NegativeInfinity"/>.
    /// </summary>
    /// <remarks>
    /// Ported from the Dam Screening Tool API (SystemResponse.Api). NaN values are intentionally
    /// NOT flagged — NaN is a legitimate marker for missing observations in hydrologic time series
    /// (e.g., USGS gap fill) and serializes safely under
    /// <see cref="System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals"/>.
    /// Infinity, by contrast, indicates a computation defect and should fail loudly before
    /// serialization rather than mid-stream.
    /// </remarks>
    public static class ResponseFiniteAuditor
    {
        /// <summary>
        /// Maximum recursion depth. Value-type property chains cannot be cycle-broken by reference
        /// identity (e.g., <see cref="DateTime.Date"/> returns a fresh <see cref="DateTime"/>
        /// forever), so a hard depth cap backstops any leaf case the switch below misses.
        /// </summary>
        private const int MaxDepth = 64;

        /// <summary>
        /// Recursively audits <paramref name="root"/> for <c>±Infinity</c> double values.
        /// </summary>
        /// <param name="root">The response object graph to audit. May be null.</param>
        /// <returns>A list of "path = value" strings, empty if every double is finite or NaN.</returns>
        public static List<string> Audit(object? root)
        {
            var findings = new List<string>();
            if (root == null) return findings;
            var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
            Walk(root, rootPath: root.GetType().Name, findings, visited, depth: 0);
            return findings;
        }

        /// <summary>
        /// Recursively walks one node of the object graph, appending findings for any
        /// non-finite doubles and descending into dictionaries, arrays, sequences, and
        /// public instance properties.
        /// </summary>
        /// <param name="value">The current node value.</param>
        /// <param name="rootPath">The dotted/indexed path from the root to this node, used in findings.</param>
        /// <param name="findings">The accumulating list of findings.</param>
        /// <param name="visited">Reference-equality set used to break cycles on reference types.</param>
        /// <param name="depth">The current recursion depth, capped at <see cref="MaxDepth"/>.</param>
        private static void Walk(object? value, string rootPath, List<string> findings, HashSet<object> visited, int depth)
        {
            if (value == null || depth > MaxDepth) return;

            // Break cycles on reference types.
            if (!value.GetType().IsValueType)
            {
                if (!visited.Add(value)) return;
            }

            switch (value)
            {
                case double d:
                    if (double.IsInfinity(d)) findings.Add($"{rootPath} = {d}");
                    return;

                case float f:
                    if (float.IsInfinity(f)) findings.Add($"{rootPath} = {f}");
                    return;

                // Leaf values with no double content. Date/time structs are critical here: their
                // properties return fresh value-type instances (DateTime.Date → DateTime), which
                // the reference-identity cycle guard cannot break.
                case string:
                case bool:
                case char:
                case decimal:
                case DateTime:
                case DateTimeOffset:
                case TimeSpan:
                case DateOnly:
                case TimeOnly:
                case Guid:
                    return;
            }

            var type = value.GetType();
            if (type.IsPrimitive || type.IsEnum) return;

            if (value is IDictionary dict)
            {
                foreach (DictionaryEntry entry in dict)
                {
                    Walk(entry.Value, $"{rootPath}[{entry.Key}]", findings, visited, depth + 1);
                }
                return;
            }

            if (value is Array arr)
            {
                if (arr.Rank == 1)
                {
                    for (int i = 0; i < arr.Length; i++)
                    {
                        Walk(arr.GetValue(i), $"{rootPath}[{i}]", findings, visited, depth + 1);
                    }
                }
                else if (arr.Rank == 2)
                {
                    int rows = arr.GetLength(0);
                    int cols = arr.GetLength(1);
                    for (int r = 0; r < rows; r++)
                    {
                        for (int c = 0; c < cols; c++)
                        {
                            Walk(arr.GetValue(r, c), $"{rootPath}[{r},{c}]", findings, visited, depth + 1);
                        }
                    }
                }
                return;
            }

            if (value is IEnumerable seq)
            {
                int i = 0;
                foreach (var item in seq)
                {
                    Walk(item, $"{rootPath}[{i++}]", findings, visited, depth + 1);
                }
                return;
            }

            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (prop.GetIndexParameters().Length > 0) continue;
                object? propValue;
                try
                {
                    propValue = prop.GetValue(value);
                }
                catch
                {
                    // Property getters on foreign types may throw (e.g., not-yet-computed state);
                    // skip them rather than failing the audit.
                    continue;
                }
                Walk(propValue, $"{rootPath}.{prop.Name}", findings, visited, depth + 1);
            }
        }
    }
}
