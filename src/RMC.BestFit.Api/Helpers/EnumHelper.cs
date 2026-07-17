using System.Text.Json;

namespace RMC.BestFit.Api.Helpers
{
    /// <summary>
    /// Helpers for presenting enum values in the API's camelCase JSON contract.
    /// </summary>
    /// <remarks>
    /// The API serializes enums as camelCase strings via
    /// <see cref="System.Text.Json.Serialization.JsonStringEnumConverter"/>. The metadata/discovery
    /// endpoints list the accepted values for each enum so MCP agents can construct valid requests;
    /// these helpers keep that listing consistent with the serializer's naming policy.
    /// </remarks>
    public static class EnumHelper
    {
        /// <summary>
        /// Converts a single enum member name to the camelCase form used on the wire.
        /// </summary>
        /// <param name="name">The enum member name (e.g., "WaterYear").</param>
        /// <returns>The camelCase form (e.g., "waterYear").</returns>
        public static string ToCamelCase(string name)
        {
            return JsonNamingPolicy.CamelCase.ConvertName(name);
        }

        /// <summary>
        /// Returns the camelCase names of all members of the enum type, in declaration order.
        /// </summary>
        /// <typeparam name="TEnum">The enum type to enumerate.</typeparam>
        /// <returns>A list of camelCase member names matching the API's JSON contract.</returns>
        public static List<string> CamelCaseNames<TEnum>() where TEnum : struct, Enum
        {
            return Enum.GetNames<TEnum>().Select(ToCamelCase).ToList();
        }

        /// <summary>
        /// Parses a camelCase (or any-case) enum string from an MCP tool parameter, falling back
        /// to a default when the value is null or blank.
        /// </summary>
        /// <typeparam name="TEnum">The enum type to parse.</typeparam>
        /// <param name="value">The string value, or null/blank for the default.</param>
        /// <param name="defaultValue">The value used when <paramref name="value"/> is null or blank.</param>
        /// <returns>The parsed enum value.</returns>
        /// <exception cref="ArgumentException">Thrown when the value does not name an enum member; the message lists the accepted values.</exception>
        public static TEnum ParseOrDefault<TEnum>(string? value, TEnum defaultValue) where TEnum : struct, Enum
        {
            if (string.IsNullOrWhiteSpace(value)) return defaultValue;
            if (Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed)) return parsed;
            throw new ArgumentException(
                $"'{value}' is not a valid {typeof(TEnum).Name}. Accepted values: {string.Join(", ", CamelCaseNames<TEnum>())}.");
        }

        /// <summary>
        /// Parses a camelCase (or any-case) enum string from an MCP tool parameter, returning null
        /// when the value is null or blank (so a model-layer default stays authoritative).
        /// </summary>
        /// <typeparam name="TEnum">The enum type to parse.</typeparam>
        /// <param name="value">The string value, or null/blank for null.</param>
        /// <returns>The parsed enum value, or null.</returns>
        /// <exception cref="ArgumentException">Thrown when the value does not name an enum member; the message lists the accepted values.</exception>
        public static TEnum? ParseOrNull<TEnum>(string? value) where TEnum : struct, Enum
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            if (Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed)) return parsed;
            throw new ArgumentException(
                $"'{value}' is not a valid {typeof(TEnum).Name}. Accepted values: {string.Join(", ", CamelCaseNames<TEnum>())}.");
        }
    }
}
