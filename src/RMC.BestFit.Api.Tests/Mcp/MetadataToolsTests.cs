using System.Text.Json;
using Microsoft.Extensions.Options;
using RMC.BestFit.Api.Configuration;
using RMC.BestFit.Api.Mcp;
using RMC.BestFit.Api.Services.Exceptions;
using RMC.BestFit.Api.Store;
using RMC.BestFit.Api.Tests.Support;
using RMC.BestFit.Models;

namespace RMC.BestFit.Api.Tests.Mcp
{
    /// <summary>
    /// Unit tests for <see cref="MetadataTools"/>: metadata payload, resource overview, and typed deletion.
    /// </summary>
    [TestClass]
    public class MetadataToolsTests
    {
        /// <summary>
        /// The store backing the tools.
        /// </summary>
        private InMemoryResourceStore _store = null!;

        /// <summary>
        /// The tools under test.
        /// </summary>
        private MetadataTools _tools = null!;

        /// <summary>
        /// Creates a fresh store and tool instance before each test.
        /// </summary>
        [TestInitialize]
        public void Initialize()
        {
            _store = new InMemoryResourceStore(Options.Create(new ApiOptions()));
            _tools = new MetadataTools(_store, Options.Create(new ApiOptions()));
        }

        /// <summary>
        /// Verifies the metadata JSON carries distributions, enum lists, and defaults.
        /// </summary>
        [TestMethod]
        public void GetMetadata_CarriesDistributionsEnumsAndDefaults()
        {
            string json = _tools.GetMetadata();
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            Assert.IsTrue(root.GetProperty("distributions").GetArrayLength() >= 15);
            Assert.IsTrue(root.GetProperty("enums").GetProperty("samplers").GetArrayLength() > 0);
            Assert.IsTrue(root.GetProperty("defaults").GetProperty("probabilityOrdinates").GetArrayLength() > 0);
        }

        /// <summary>
        /// Verifies the resource overview lists stored resources with counts.
        /// </summary>
        [TestMethod]
        public void ListResources_ListsStoredResources()
        {
            _store.AddTimeSeries(new TimeSeriesResource(TestSeries.IrregularPeaks()) { Name = "ts", Source = TimeSeriesSource.Manual });
            _store.AddInputData(new InputDataResource { Name = "id", DataFrame = new DataFrame(), Method = InputDataMethod.Manual });

            string json = _tools.ListResources();
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            Assert.AreEqual(1, root.GetProperty("timeSeriesCount").GetInt32());
            Assert.AreEqual(1, root.GetProperty("inputDataCount").GetInt32());
            Assert.AreEqual(2, root.GetProperty("resources").GetArrayLength());
        }

        /// <summary>
        /// Verifies typed deletion removes the resource and guards type/id errors.
        /// </summary>
        [TestMethod]
        public void DeleteResource_RemovesByType_AndGuards()
        {
            var resource = _store.AddTimeSeries(new TimeSeriesResource(TestSeries.IrregularPeaks()) { Name = "ts", Source = TimeSeriesSource.Manual });

            string json = _tools.DeleteResource("timeSeries", resource.Id);
            StringAssert.Contains(json, resource.Id.ToString());
            Assert.IsNull(_store.GetTimeSeries(resource.Id));

            Assert.ThrowsException<ArgumentException>(() => _tools.DeleteResource("bogus", Guid.NewGuid()));
            Assert.ThrowsException<ResourceNotFoundException>(() => _tools.DeleteResource("analysis", Guid.NewGuid()));
        }
    }
}
