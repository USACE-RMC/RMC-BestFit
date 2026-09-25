using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RMC.BestFit.Api.Configuration;
using RMC.BestFit.Api.Controllers;
using RMC.BestFit.Api.Services;
using RMC.BestFit.Api.Store;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.Controllers
{
    [TestClass]
    public class AnalysisPlotSourceControllerTests
    {
        [TestMethod]
        public async Task Get_Returns404ForNeverRunAnd409ForBusy()
        {
            var store = new InMemoryResourceStore(Options.Create(new ApiOptions()));
            var resource = store.AddAnalysis(TestAnalyses.CreateUnivariateResource());
            var service = new AnalysisService(store, Options.Create(new ApiOptions()));
            var controller = new AnalysisPlotSourceController(NullLogger<AnalysisPlotSourceController>.Instance, service);

            var missing = await controller.Get(resource.Id, false);
            Assert.AreEqual(404, ((Microsoft.AspNetCore.Mvc.ObjectResult)missing.Result!).StatusCode);

            resource.RunLock.Wait();
            try
            {
                var busy = await controller.Get(resource.Id, false);
                Assert.AreEqual(409, ((Microsoft.AspNetCore.Mvc.ObjectResult)busy.Result!).StatusCode);
            }
            finally
            {
                resource.RunLock.Release();
            }
        }
    }
}
