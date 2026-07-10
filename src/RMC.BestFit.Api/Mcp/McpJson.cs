using System.Text.Json;
using System.Text.Json.Serialization;

namespace RMC.BestFit.Api.Mcp
{
    /// <summary>
    /// JSON serialization for MCP tool results, using exactly the same wire options as the REST
    /// controllers so both surfaces share one contract (camelCase, string enums, ignore-null,
    /// named floating-point literals).
    /// </summary>
    public static class McpJson
    {
        /// <summary>
        /// The serializer options matching the REST wire contract configured in Program.cs.
        /// </summary>
        public static readonly JsonSerializerOptions Options = CreateOptions();

        /// <summary>
        /// Builds the serializer options.
        /// </summary>
        /// <returns>The configured options.</returns>
        private static JsonSerializerOptions CreateOptions()
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
                WriteIndented = false
            };
            options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
            return options;
        }

        /// <summary>
        /// Serializes a response DTO to the JSON string returned as the MCP tool result.
        /// </summary>
        /// <typeparam name="T">The DTO type.</typeparam>
        /// <param name="value">The DTO to serialize.</param>
        /// <returns>The JSON text.</returns>
        public static string Serialize<T>(T value)
        {
            return JsonSerializer.Serialize(value, Options);
        }
    }
}
