using Microsoft.Extensions.Options;
using RMC.BestFit.Api.Configuration;
using RMC.BestFit.Api.Mcp;
using RMC.BestFit.Api.Services;
using RMC.BestFit.Api.Services.Exceptions;
using RMC.BestFit.Api.Store;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.Mcp
{
    [TestClass]
    public class PlotSourceToolsTests
    {
        [TestMethod]
        public void GetAnalysisPlotSource_RejectsNeverRunWithoutEstimating()
        {
            var store = new InMemoryResourceStore(Options.Create(new ApiOptions()));
            var resource = store.AddAnalysis(TestAnalyses.CreateUnivariateResource());
            var tools = new AnalysisTools(new AnalysisService(store, Options.Create(new ApiOptions())));
            Assert.ThrowsException<ResourceNotFoundException>(() => tools.GetAnalysisPlotSource(resource.Id));
            Assert.AreEqual(AnalysisRunState.Created, resource.State);
        }
    }
}
