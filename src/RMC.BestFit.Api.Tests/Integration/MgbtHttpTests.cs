using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Numerics.Data;
using RMC.BestFit.Api.Services;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.Integration
{
    /// <summary>Tests MGBT HTTP binding and failure reporting without fitting or network calls.</summary>
    [TestClass]
    public class MgbtHttpTests
    {
        /// <summary>Checks opt-in, omitted default, and conflicting manual settings over HTTP.</summary>
        [TestMethod]
        public async Task Manual_ScreeningAndConflict_AreVisibleOverHttp()
        {
            using var factory = new WebApplicationFactory<Program>();
            using var client = factory.CreateClient();
            var data = Enumerable.Range(0, 15).Select(i => new { index = 2000 + i, value = i < 2 ? 0 : 100 + i * 20 }).ToArray();
            foreach (bool enabled in new[] { false, true })
            {
                using var response = await client.PostAsJsonAsync("/api/inputdata/manual", new { exactData = data, useMultipleGrubbsBeckTest = enabled });
                response.EnsureSuccessStatusCode();
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                Assert.AreEqual(enabled ? 2 : 0, json.RootElement.GetProperty("inputData").GetProperty("lowOutlierCount").GetInt32());
            }
            using var conflict = await client.PostAsJsonAsync("/api/inputdata/manual", new { exactData = data, useMultipleGrubbsBeckTest = true, lowOutlierThreshold = 0 });
            Assert.AreEqual(HttpStatusCode.BadRequest, conflict.StatusCode);
        }

        /// <summary>Checks insufficient USGS peaks stop the workflow at input creation.</summary>
        [TestMethod]
        public async Task Workflow_InsufficientPeaks_ReportsInputFailure()
        {
            var peaks = new TimeSeries(TimeInterval.Irregular);
            for (int i = 0; i < 9; i++)
                peaks.Add(new SeriesOrdinate<DateTime, double>(new DateTime(2000 + i, 5, 1), 100 + i * 20));
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
                builder.ConfigureServices(services => services.AddSingleton<IUsgsTimeSeriesService>(
                    new FakeUsgsTimeSeriesService { Result = peaks })));
            using var client = factory.CreateClient();
            // Invalid distribution is a second guard against accidentally running an estimator.
            using var response = await client.PostAsJsonAsync("/api/workflows/usgs-bulletin17c",
                new { siteNumber = "01646500", useMultipleGrubbsBeckTest = true, distribution = "generalizedExtremeValue" });
            response.EnsureSuccessStatusCode();
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.IsFalse(json.RootElement.GetProperty("success").GetBoolean());
            Assert.AreEqual("createInputData", json.RootElement.GetProperty("failedStep").GetString());
        }
    }
}
