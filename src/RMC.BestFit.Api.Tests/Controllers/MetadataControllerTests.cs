using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RMC.BestFit.Api.Configuration;
using RMC.BestFit.Api.Controllers;
using RMC.BestFit.Api.DTOs;

namespace RMC.BestFit.Api.Tests.Controllers
{
    /// <summary>
    /// Unit tests for <see cref="MetadataController"/> discovery endpoints.
    /// </summary>
    [TestClass]
    public class MetadataControllerTests
    {
        /// <summary>
        /// The controller under test.
        /// </summary>
        private MetadataController _controller = null!;

        /// <summary>
        /// Creates a fresh controller before each test.
        /// </summary>
        [TestInitialize]
        public void Initialize()
        {
            _controller = new MetadataController(
                NullLogger<MetadataController>.Instance,
                Options.Create(new ApiOptions { MaxIterations = 1234, MaxConcurrentRuns = 3, MaxResources = 42 }));
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
        /// Verifies the distributions endpoint returns the supported distribution listing.
        /// </summary>
        [TestMethod]
        public async Task GetDistributions_Returns200WithListing()
        {
            var (status, body) = Unwrap(await _controller.GetDistributions());
            Assert.AreEqual(200, status);
            Assert.IsTrue(body.Distributions.Count >= 15);
        }

        /// <summary>
        /// Verifies the enums endpoint returns populated option lists.
        /// </summary>
        [TestMethod]
        public async Task GetEnumOptions_Returns200WithOptions()
        {
            var (status, body) = Unwrap(await _controller.GetEnumOptions());
            Assert.AreEqual(200, status);
            Assert.IsTrue(body.Samplers.Count > 0);
            Assert.IsTrue(body.UsgsSeriesTypes.Count > 0);
        }

        /// <summary>
        /// Verifies the defaults endpoint reports the default ordinates and the configured limits.
        /// </summary>
        [TestMethod]
        public async Task GetDefaults_ReportsOrdinatesAndLimits()
        {
            var (status, body) = Unwrap(await _controller.GetDefaults());
            Assert.AreEqual(200, status);
            Assert.IsTrue(body.ProbabilityOrdinates.Count > 0);
            Assert.AreEqual(1234, body.MaxIterations);
            Assert.AreEqual(3, body.MaxConcurrentRuns);
            Assert.AreEqual(42, body.MaxResources);
            Assert.IsTrue(body.Notes.Count > 0);
        }
    }
}
