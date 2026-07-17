using Microsoft.Extensions.Options;
using Numerics.Data;
using RMC.BestFit.Api.Configuration;
using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Services;
using RMC.BestFit.Api.Services.Exceptions;
using RMC.BestFit.Api.Store;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.Services
{
    /// <summary>
    /// Unit tests for <see cref="TimeSeriesService"/> using the faked USGS seam.
    /// </summary>
    [TestClass]
    public class TimeSeriesServiceTests
    {
        /// <summary>
        /// The store backing the service under test.
        /// </summary>
        private InMemoryResourceStore _store = null!;

        /// <summary>
        /// The faked USGS seam.
        /// </summary>
        private FakeUsgsTimeSeriesService _usgs = null!;

        /// <summary>
        /// The service under test.
        /// </summary>
        private TimeSeriesService _service = null!;

        /// <summary>
        /// Creates a fresh store, fake, and service before each test.
        /// </summary>
        [TestInitialize]
        public void Initialize()
        {
            _store = new InMemoryResourceStore(Options.Create(new ApiOptions()));
            _usgs = new FakeUsgsTimeSeriesService { Result = TestSeries.DailyThreeWaterYears() };
            _service = new TimeSeriesService(_store, _usgs);
        }

        /// <summary>
        /// Verifies constructor null guards.
        /// </summary>
        [TestMethod]
        public void Constructor_NullDependencies_Throw()
        {
            Assert.ThrowsException<ArgumentNullException>(() => _ = new TimeSeriesService(null!, _usgs));
            Assert.ThrowsException<ArgumentNullException>(() => _ = new TimeSeriesService(_store, null!));
        }

        /// <summary>
        /// Verifies the USGS creation path stores the resource with provenance and a defaulted name.
        /// </summary>
        [TestMethod]
        public async Task CreateFromUsgsAsync_StoresResourceWithProvenance()
        {
            var request = new CreateUsgsTimeSeriesRequest
            {
                SiteNumber = "01646500",
                SeriesType = TimeSeriesDownload.TimeSeriesType.DailyDischarge
            };

            var resource = await _service.CreateFromUsgsAsync(request);

            Assert.AreEqual("01646500", _usgs.LastSiteNumber);
            Assert.AreEqual(TimeSeriesDownload.TimeSeriesType.DailyDischarge, _usgs.LastSeriesType);
            Assert.AreEqual(TimeSeriesSource.Usgs, resource.Source);
            Assert.AreEqual("01646500", resource.UsgsSiteNumber);
            StringAssert.Contains(resource.Name, "01646500");
            Assert.AreSame(resource, _store.GetTimeSeries(resource.Id));
            Assert.AreEqual(TestSeries.DailyThreeWaterYears().Count, resource.PointCount);
        }

        /// <summary>
        /// Verifies an explicit name overrides the generated default.
        /// </summary>
        [TestMethod]
        public async Task CreateFromUsgsAsync_ExplicitName_Preserved()
        {
            var request = new CreateUsgsTimeSeriesRequest { SiteNumber = "01646500", Name = "My gage" };
            var resource = await _service.CreateFromUsgsAsync(request);
            Assert.AreEqual("My gage", resource.Name);
        }

        /// <summary>
        /// Verifies manual creation sorts unsorted points by date.
        /// </summary>
        [TestMethod]
        public void CreateManual_SortsPointsByDate()
        {
            var request = new CreateManualTimeSeriesRequest
            {
                TimeInterval = TimeInterval.Irregular,
                Points = new List<TimeSeriesPointDto>
                {
                    new() { DateTime = new DateTime(2020, 3, 1), Value = 3d },
                    new() { DateTime = new DateTime(2020, 1, 1), Value = 1d },
                    new() { DateTime = new DateTime(2020, 2, 1), Value = 2d }
                }
            };

            var resource = _service.CreateManual(request);

            Assert.AreEqual(3, resource.PointCount);
            Assert.AreEqual(new DateTime(2020, 1, 1), resource.StartDate);
            Assert.AreEqual(new DateTime(2020, 3, 1), resource.EndDate);
            Assert.AreEqual(1d, resource.TimeSeries[0].Value);
            Assert.AreEqual(3d, resource.TimeSeries[2].Value);
            Assert.AreEqual(TimeSeriesSource.Manual, resource.Source);
        }

        /// <summary>
        /// Verifies manual creation rejects an empty point list.
        /// </summary>
        [TestMethod]
        public void CreateManual_NoPoints_Throws()
        {
            var request = new CreateManualTimeSeriesRequest { Points = new List<TimeSeriesPointDto>() };
            Assert.ThrowsException<ArgumentException>(() => _service.CreateManual(request));
        }

        /// <summary>
        /// Verifies Get throws the not-found exception for unknown ids.
        /// </summary>
        [TestMethod]
        public void Get_UnknownId_ThrowsNotFound()
        {
            Assert.ThrowsException<ResourceNotFoundException>(() => _service.Get(Guid.NewGuid()));
        }

        /// <summary>
        /// Verifies Delete removes the resource and throws for unknown ids.
        /// </summary>
        [TestMethod]
        public void Delete_RemovesResource_AndThrowsWhenMissing()
        {
            var resource = _service.CreateManual(new CreateManualTimeSeriesRequest
            {
                TimeInterval = TimeInterval.Irregular,
                Points = new List<TimeSeriesPointDto> { new() { DateTime = DateTime.UtcNow, Value = 1d } }
            });
            _service.Delete(resource.Id);
            Assert.AreEqual(0, _service.List().Count);
            Assert.ThrowsException<ResourceNotFoundException>(() => _service.Delete(resource.Id));
        }
    }
}
