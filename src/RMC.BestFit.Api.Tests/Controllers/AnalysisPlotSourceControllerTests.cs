using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RMC.BestFit.Api.Configuration;
using RMC.BestFit.Api.Controllers;
using RMC.BestFit.Api.Services;
using RMC.BestFit.Api.Store;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.Controllers
{
    /// <summary>Checks status and wire serialization for plot-source errors without estimation.</summary>
    [TestClass]
    public class AnalysisPlotSourceControllerTests
    {
        /// <summary>Unavailable and busy results must remain serializable error responses.</summary>
        [TestMethod]
        public async Task Get_Returns404ForNeverRunAnd409ForBusy()
        {
            var store = new InMemoryResourceStore(Options.Create(new ApiOptions()));
            var resource = store.AddAnalysis(TestAnalyses.CreateUnivariateResource());
            var service = new AnalysisService(store, Options.Create(new ApiOptions()));
            var controller = new AnalysisPlotSourceController(NullLogger<AnalysisPlotSourceController>.Instance, service);

            var missing = await controller.Get(resource.Id, false);
            Assert.AreEqual(404, ((Microsoft.AspNetCore.Mvc.ObjectResult)missing.Result!).StatusCode);
            using var missingJson = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(
                ((Microsoft.AspNetCore.Mvc.ObjectResult)missing.Result!).Value));
            Assert.AreEqual(System.Text.Json.JsonValueKind.Null, missingJson.RootElement.GetProperty("results").ValueKind);

            resource.RunLock.Wait();
            try
            {
                var busy = await controller.Get(resource.Id, false);
                Assert.AreEqual(409, ((Microsoft.AspNetCore.Mvc.ObjectResult)busy.Result!).StatusCode);
                using var busyJson = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(
                    ((Microsoft.AspNetCore.Mvc.ObjectResult)busy.Result!).Value));
                Assert.AreEqual(System.Text.Json.JsonValueKind.Null, busyJson.RootElement.GetProperty("results").ValueKind);
            }
            finally
            {
                resource.RunLock.Release();
            }
        }
    }
}
