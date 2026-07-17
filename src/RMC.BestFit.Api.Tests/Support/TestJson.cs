using System.Text.Json;
using System.Text.Json.Serialization;

namespace RMC.BestFit.Api.Tests.Support
{
    /// <summary>
    /// Shared JSON options for DTO round-trip tests, mirroring the serializer configuration in the
    /// API's Program.cs (camelCase, string enums, ignore-null, named floating-point literals).
    /// </summary>
    public static class TestJson
    {
        /// <summary>
        /// The serializer options matching the API's wire contract.
        /// </summary>
        public static readonly JsonSerializerOptions Options = CreateOptions();

        /// <summary>
        /// Builds the serializer options matching the API's Program.cs configuration.
        /// </summary>
        /// <returns>The configured options.</returns>
        private static JsonSerializerOptions CreateOptions()
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
            };
            options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
            return options;
        }

        /// <summary>
        /// Serializes a value and deserializes it back, returning the round-tripped instance.
        /// </summary>
        /// <typeparam name="T">The DTO type.</typeparam>
        /// <param name="value">The value to round-trip.</param>
        /// <returns>The deserialized copy.</returns>
        public static T Roundtrip<T>(T value)
        {
            string json = JsonSerializer.Serialize(value, Options);
            var result = JsonSerializer.Deserialize<T>(json, Options);
            Assert.IsNotNull(result, "Round-trip deserialization returned null.");
            return result;
        }

        /// <summary>
        /// Serializes a value to a JSON string using the API's wire options.
        /// </summary>
        /// <typeparam name="T">The DTO type.</typeparam>
        /// <param name="value">The value to serialize.</param>
        /// <returns>The JSON text.</returns>
        public static string Serialize<T>(T value)
        {
            return JsonSerializer.Serialize(value, Options);
        }
    }
}
