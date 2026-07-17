using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RMC.BestFit.Api.Configuration;
using RMC.BestFit.Api.Controllers;
using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Store;
using RMC.BestFit.Api.Tests.Support;
using RMC.BestFit.Models;

namespace RMC.BestFit.Api.Tests.Controllers
{
    /// <summary>
    /// Unit tests for <see cref="ResourcesController"/>: the cross-cutting overview.
    /// </summary>
    [TestClass]
    public class ResourcesControllerTests
    {
        /// <summary>
        /// Verifies the overview reports per-type counts and one summary per resource.
        /// </summary>
        [TestMethod]
        public async Task GetOverview_ReportsCountsAndSummaries()
        {
            var store = new InMemoryResourceStore(Options.Create(new ApiOptions()));
            store.AddTimeSeries(new TimeSeriesResource(TestSeries.IrregularPeaks()) { Name = "ts", Source = TimeSeriesSource.Manual });
            store.AddInputData(new InputDataResource { Name = "id", DataFrame = new DataFrame(), Method = InputDataMethod.Manual });
            var controller = new ResourcesController(NullLogger<ResourcesController>.Instance, store);

            var actionResult = await controller.GetOverview();
            var objectResult = (ObjectResult)actionResult.Result!;
            var body = (ResourcesOverviewResponse)objectResult.Value!;

            Assert.AreEqual(200, objectResult.StatusCode);
            Assert.AreEqual(1, body.TimeSeriesCount);
            Assert.AreEqual(1, body.InputDataCount);
            Assert.AreEqual(0, body.AnalysisCount);
            Assert.AreEqual(2, body.Resources.Count);
            Assert.IsTrue(body.Resources.All(r => r.Detail != null && r.Name != null));
        }

        /// <summary>
        /// Verifies an empty store yields an empty overview rather than an error.
        /// </summary>
        [TestMethod]
        public async Task GetOverview_EmptyStore_ReturnsEmptyListing()
        {
            var store = new InMemoryResourceStore(Options.Create(new ApiOptions()));
            var controller = new ResourcesController(NullLogger<ResourcesController>.Instance, store);

            var actionResult = await controller.GetOverview();
            var body = (ResourcesOverviewResponse)((ObjectResult)actionResult.Result!).Value!;

            Assert.AreEqual(0, body.Resources.Count);
        }
    }
}
