using System.Globalization;
using System.Text;

namespace RMC.BestFit.Models
{
    /// <summary>
    /// Formatting helpers for producing Unicode-subscript strings used in parameter names.
    /// </summary>
    /// <remarks>
    /// Maps ASCII digits 0-9 to their Unicode subscript counterparts U+2080..U+2089
    /// so that model parameter labels such as "AR (φ₁)", "Weight ₂", or "ε₁₂" render
    /// with proper typographic subscripts in the UI and in exported reports.
    /// </remarks>
    public static class SubscriptFormatter
    {
        private const string Digits = "0123456789";
        private const string Subscripts = "₀₁₂₃₄₅₆₇₈₉";

        /// <summary>
        /// Returns the Unicode-subscript representation of a non-negative integer.
        /// </summary>
        /// <param name="value">Integer to render. Negative values are prefixed with "-".</param>
        /// <returns>String of subscript code points, e.g. <c>12</c> → <c>"₁₂"</c>.</returns>
        public static string ToSubscript(int value)
        {
            var s = value.ToString(CultureInfo.InvariantCulture);
            var sb = new StringBuilder(s.Length);
            foreach (var c in s)
            {
                int idx = Digits.IndexOf(c);
                sb.Append(idx >= 0 ? Subscripts[idx] : c);
            }
            return sb.ToString();
        }
    }
}
