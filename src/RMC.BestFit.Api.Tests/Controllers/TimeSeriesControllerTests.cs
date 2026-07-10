using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Numerics.Data;
using RMC.BestFit.Api.Configuration;
using RMC.BestFit.Api.Controllers;
using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Services;
using RMC.BestFit.Api.Services.Exceptions;
using RMC.BestFit.Api.Store;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.Controllers
{
    /// <summary>
    /// Unit tests for <see cref="TimeSeriesController"/> status codes and payloads, using the real
    /// service over an in-memory store with the USGS seam faked.
    /// </summary>
    [TestClass]
    public class TimeSeriesControllerTests
    {
        /// <summary>
        /// The faked USGS seam.
        /// </summary>
        private FakeUsgsTimeSeriesService _usgs = null!;

        /// <summary>
        /// The controller under test.
        /// </summary>
        private TimeSeriesController _controller = null!;

        /// <summary>
        /// Creates a fresh controller stack before each test.
        /// </summary>
        [TestInitialize]
        public void Initialize()
        {
            var store = new InMemoryResourceStore(Options.Create(new ApiOptions()));
            _usgs = new FakeUsgsTimeSeriesService { Result = TestSeries.DailyThreeWaterYears() };
            var service = new TimeSeriesService(store, _usgs);
            _controller = new TimeSeriesController(NullLogger<TimeSeriesController>.Instance, service);
        }

        /// <summary>
        /// Extracts status code and body from an action result.
        /// </summary>
        /// <typeparam name="TResponse">The response DTO type.</typeparam>
        /// <param name="actionResult">The action result.</param>
        /// <returns>The status code and body.</returns>
        private static (int StatusCode, TResponse Body) Unwrap<TResponse>(ActionResult<TResponse> actionResult)
            where TResponse : ResponseBase
        {
            var objectResult = (ObjectResult)actionResult.Result!;
            return (objectResult.StatusCode!.Value, (TResponse)objectResult.Value!);
        }

        /// <summary>
        /// Verifies the USGS create endpoint returns 201 with the resource summary.
        /// </summary>
        [TestMethod]
        public async Task CreateFromUsgs_Returns201()
        {
            var (status, body) = Unwrap(await _controller.CreateFromUsgs(
                new CreateUsgsTimeSeriesRequest { SiteNumber = "01646500" }, CancellationToken.None));

            Assert.AreEqual(201, status);
            Assert.IsTrue(body.Success);
            Assert.IsNotNull(body.TimeSeries);
            Assert.AreEqual("01646500", body.TimeSeries.UsgsSiteNumber);
        }

        /// <summary>
        /// Verifies a USGS no-data failure surfaces as 404.
        /// </summary>
        [TestMethod]
        public async Task CreateFromUsgs_NoData_Returns404()
        {
            _usgs.ExceptionToThrow = new UsgsDataNotFoundException("no data");
            var (status, body) = Unwrap(await _controller.CreateFromUsgs(
                new CreateUsgsTimeSeriesRequest { SiteNumber = "01646500" }, CancellationToken.None));
            Assert.AreEqual(404, status);
            Assert.IsFalse(body.Success);
        }

        /// <summary>
        /// Verifies a USGS upstream failure surfaces with the exception's status code (502).
        /// </summary>
        [TestMethod]
        public async Task CreateFromUsgs_Upstream_Returns502()
        {
            _usgs.ExceptionToThrow = new UsgsUnavailableException("upstream", statusCode: 502);
            var (status, _) = Unwrap(await _controller.CreateFromUsgs(
                new CreateUsgsTimeSeriesRequest { SiteNumber = "01646500" }, CancellationToken.None));
            Assert.AreEqual(502, status);
        }

        /// <summary>
        /// Verifies manual create returns 201 and get/list/delete round-trip the resource.
        /// </summary>
        [TestMethod]
        public async Task Manual_Get_List_Delete_Lifecycle()
        {
            var (createStatus, created) = Unwrap(await _controller.CreateManual(new CreateManualTimeSeriesRequest
            {
                TimeInterval = TimeInterval.Irregular,
                Points = new List<TimeSeriesPointDto> { new() { DateTime = new DateTime(2020, 1, 1), Value = 5d } }
            }));
            Assert.AreEqual(201, createStatus);
            var id = created.TimeSeries!.Id;

            var (getStatus, got) = Unwrap(await _controller.Get(id, includePoints: true));
            Assert.AreEqual(200, getStatus);
            Assert.IsNotNull(got.Points);
            Assert.AreEqual(5d, got.Points[0].Value);

            var (listStatus, list) = Unwrap(await _controller.List());
            Assert.AreEqual(200, listStatus);
            Assert.AreEqual(1, list.Count);

            var (deleteStatus, deleted) = Unwrap(await _controller.Delete(id));
            Assert.AreEqual(200, deleteStatus);
            Assert.AreEqual(id, deleted.DeletedId);
            Assert.AreEqual("timeSeries", deleted.ResourceType);
        }

        /// <summary>
        /// Verifies unknown ids surface as 404 on get and delete.
        /// </summary>
        [TestMethod]
        public async Task Get_And_Delete_UnknownId_Return404()
        {
            var (getStatus, _) = Unwrap(await _controller.Get(Guid.NewGuid()));
            Assert.AreEqual(404, getStatus);

            var (deleteStatus, _) = Unwrap(await _controller.Delete(Guid.NewGuid()));
            Assert.AreEqual(404, deleteStatus);
        }
    }
}
