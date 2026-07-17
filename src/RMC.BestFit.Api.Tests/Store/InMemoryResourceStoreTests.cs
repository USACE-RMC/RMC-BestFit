using Microsoft.Extensions.Options;
using RMC.BestFit.Api.Configuration;
using RMC.BestFit.Api.Services.Exceptions;
using RMC.BestFit.Api.Store;
using RMC.BestFit.Api.Tests.Support;
using RMC.BestFit.Models;

namespace RMC.BestFit.Api.Tests.Store
{
    /// <summary>
    /// Unit tests for <see cref="InMemoryResourceStore"/>: add/get/list/delete per resource type,
    /// capacity enforcement, cross-type isolation, and concurrent adds.
    /// </summary>
    [TestClass]
    public class InMemoryResourceStoreTests
    {
        /// <summary>
        /// Builds a store with the given capacity.
        /// </summary>
        /// <param name="maxResources">The capacity cap.</param>
        /// <returns>A fresh store.</returns>
        private static InMemoryResourceStore CreateStore(int maxResources = 500)
        {
            return new InMemoryResourceStore(Options.Create(new ApiOptions { MaxResources = maxResources }));
        }

        /// <summary>
        /// Builds a minimal time-series resource for store tests.
        /// </summary>
        /// <param name="name">The resource name.</param>
        /// <returns>The resource.</returns>
        private static TimeSeriesResource CreateTimeSeriesResource(string name = "ts")
        {
            return new TimeSeriesResource(TestSeries.IrregularPeaks()) { Name = name, Source = TimeSeriesSource.Manual };
        }

        /// <summary>
        /// Builds a minimal input-data resource for store tests.
        /// </summary>
        /// <param name="name">The resource name.</param>
        /// <returns>The resource.</returns>
        private static InputDataResource CreateInputDataResource(string name = "id")
        {
            return new InputDataResource { Name = name, DataFrame = new DataFrame(), Method = InputDataMethod.Manual };
        }

        /// <summary>
        /// Verifies the constructor rejects a null options argument.
        /// </summary>
        [TestMethod]
        public void Constructor_NullOptions_Throws()
        {
            Assert.ThrowsException<ArgumentNullException>(() => _ = new InMemoryResourceStore(null!));
        }

        /// <summary>
        /// Verifies add/get round-trips a time-series resource and the id lookup returns the same instance.
        /// </summary>
        [TestMethod]
        public void AddTimeSeries_ThenGet_ReturnsSameInstance()
        {
            var store = CreateStore();
            var resource = CreateTimeSeriesResource();
            store.AddTimeSeries(resource);
            Assert.AreSame(resource, store.GetTimeSeries(resource.Id));
            Assert.AreEqual(1, store.TotalCount);
        }

        /// <summary>
        /// Verifies lookups with unknown ids return null for all three resource types.
        /// </summary>
        [TestMethod]
        public void Get_UnknownId_ReturnsNull()
        {
            var store = CreateStore();
            Assert.IsNull(store.GetTimeSeries(Guid.NewGuid()));
            Assert.IsNull(store.GetInputData(Guid.NewGuid()));
            Assert.IsNull(store.GetAnalysis(Guid.NewGuid()));
        }

        /// <summary>
        /// Verifies a time-series id cannot be resolved through the input-data lookup (type isolation).
        /// </summary>
        [TestMethod]
        public void Get_WrongTypeLookup_ReturnsNull()
        {
            var store = CreateStore();
            var resource = CreateTimeSeriesResource();
            store.AddTimeSeries(resource);
            Assert.IsNull(store.GetInputData(resource.Id));
        }

        /// <summary>
        /// Verifies listings are ordered by creation time.
        /// </summary>
        [TestMethod]
        public void ListTimeSeries_OrdersByCreationTime()
        {
            var store = CreateStore();
            var first = CreateTimeSeriesResource("first");
            var second = CreateTimeSeriesResource("second");
            store.AddTimeSeries(second);
            store.AddTimeSeries(first);
            var list = store.ListTimeSeries();
            Assert.AreEqual(2, list.Count);
            Assert.IsTrue(list[0].CreatedUtc <= list[1].CreatedUtc);
        }

        /// <summary>
        /// Verifies delete removes the resource and reports false for unknown ids.
        /// </summary>
        [TestMethod]
        public void Delete_RemovesResource_AndReportsMissing()
        {
            var store = CreateStore();
            var resource = CreateInputDataResource();
            store.AddInputData(resource);
            Assert.IsTrue(store.DeleteInputData(resource.Id));
            Assert.IsFalse(store.DeleteInputData(resource.Id));
            Assert.IsNull(store.GetInputData(resource.Id));
        }

        /// <summary>
        /// Verifies creations beyond the configured capacity are rejected with the conflict exception.
        /// </summary>
        [TestMethod]
        public void Add_BeyondCapacity_ThrowsConflict()
        {
            var store = CreateStore(maxResources: 2);
            store.AddTimeSeries(CreateTimeSeriesResource());
            store.AddInputData(CreateInputDataResource());
            Assert.ThrowsException<ResourceConflictException>(() => store.AddTimeSeries(CreateTimeSeriesResource()));
        }

        /// <summary>
        /// Verifies concurrent adds are all retained (thread safety of the underlying dictionaries).
        /// </summary>
        [TestMethod]
        public void Add_Concurrently_RetainsAllResources()
        {
            var store = CreateStore(maxResources: 1000);
            Parallel.For(0, 100, i => store.AddTimeSeries(CreateTimeSeriesResource($"ts-{i}")));
            Assert.AreEqual(100, store.ListTimeSeries().Count);
            Assert.AreEqual(100, store.TotalCount);
        }
    }
}
