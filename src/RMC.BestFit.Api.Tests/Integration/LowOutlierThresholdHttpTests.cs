using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace RMC.BestFit.Api.Tests.Integration
{
    /// <summary>
    /// Tests the manual input-data endpoint's application of <c>lowOutlierThreshold</c> over HTTP
    /// (Task 2.10 / decision D5, approved 25 September 2026), without fitting or network calls.
    /// </summary>
    [TestClass]
    public class LowOutlierThresholdHttpTests
    {
        /// <summary>Builds the 15-observation fixture shared by these tests as anonymous wire objects.</summary>
        /// <returns>The exact-data payload: two zeros followed by thirteen values from 140 to 380.</returns>
        private static object[] Data() =>
            Enumerable.Range(0, 15).Select(i => new { index = 2000 + i, value = i < 2 ? 0 : 100 + i * 20 }).ToArray<object>();

        /// <summary>Flags and counts observations below the threshold, reflected in the JSON response.</summary>
        [TestMethod]
        public async Task Manual_WithThreshold_FlagsBelowThresholdAndReturnsCount()
        {
            using var factory = new WebApplicationFactory<Program>();
            using var client = factory.CreateClient();
            using var response = await client.PostAsJsonAsync("/api/inputdata/manual", new { exactData = Data(), lowOutlierThreshold = 50 });
            response.EnsureSuccessStatusCode();
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var inputData = json.RootElement.GetProperty("inputData");
            Assert.AreEqual(2, inputData.GetProperty("lowOutlierCount").GetInt32());
            Assert.AreEqual(50, inputData.GetProperty("lowOutlierThreshold").GetDouble());
        }

        /// <summary>A threshold that would censor more than half the record is rejected with the data frame's message.</summary>
        [TestMethod]
        public async Task Manual_ThresholdAboveFiftyPercentLimit_ReturnsBadRequest()
        {
            using var factory = new WebApplicationFactory<Program>();
            using var client = factory.CreateClient();
            using var response = await client.PostAsJsonAsync("/api/inputdata/manual", new { exactData = Data(), lowOutlierThreshold = 1000 });
            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            StringAssert.Contains(json.RootElement.GetProperty("errorMessage").GetString(), "cannot censor more than 50%");
        }

        /// <summary>A preflagged observation the threshold would not flag is rejected as contradictory before storing anything.</summary>
        [TestMethod]
        public async Task Manual_PreflaggedValueAboveThreshold_ReturnsBadRequest()
        {
            using var factory = new WebApplicationFactory<Program>();
            using var client = factory.CreateClient();
            var data = new object[]
            {
                new { index = 2000, value = 0 },
                new { index = 2001, value = 100, isLowOutlier = true }
            };
            using var response = await client.PostAsJsonAsync("/api/inputdata/manual", new { exactData = data, lowOutlierThreshold = 50 });
            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }
}
