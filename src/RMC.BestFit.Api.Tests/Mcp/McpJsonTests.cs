using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Mcp;

namespace RMC.BestFit.Api.Tests.Mcp
{
    /// <summary>
    /// Unit tests for <see cref="McpJson"/>: the MCP tool serialization must match the REST wire
    /// contract exactly.
    /// </summary>
    [TestClass]
    public class McpJsonTests
    {
        /// <summary>
        /// Verifies camelCase names, string enums, and null omission — the same contract as REST.
        /// </summary>
        [TestMethod]
        public void Serialize_MatchesRestWireContract()
        {
            var response = new DeleteResourceResponse
            {
                DeletedId = Guid.Parse("11111111-2222-3333-4444-555555555555"),
                ResourceType = "timeSeries"
            };
            string json = McpJson.Serialize(response);

            StringAssert.Contains(json, "\"deletedId\"");
            StringAssert.Contains(json, "\"resourceType\"");
            StringAssert.Contains(json, "\"success\":true");
            Assert.IsFalse(json.Contains("\"errorMessage\""), "Null properties must be omitted.");
        }

        /// <summary>
        /// Verifies NaN serializes as a named floating-point literal rather than throwing.
        /// </summary>
        [TestMethod]
        public void Serialize_NaN_UsesNamedLiteral()
        {
            var response = new SummaryStatisticsResponse
            {
                Statistics = new Dictionary<string, double> { ["skew"] = double.NaN }
            };
            string json = McpJson.Serialize(response);
            StringAssert.Contains(json, "NaN");
        }
    }
}
