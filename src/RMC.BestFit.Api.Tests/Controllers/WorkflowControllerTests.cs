using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
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
    /// Unit tests for <see cref="WorkflowController"/>: step failures return HTTP 200 with the
    /// in-body failure contract (success=false, failedStep, partial ids), and cancellation maps
    /// to 499. Success paths are not exercised (they run estimators).
    /// </summary>
    [TestClass]
    public class WorkflowControllerTests
    {
        /// <summary>
        /// The faked USGS seam.
        /// </summary>
        private FakeUsgsTimeSeriesService _usgs = null!;

        /// <summary>
        /// The controller under test.
        /// </summary>
        private WorkflowController _controller = null!;

        /// <summary>
        /// Creates a fresh controller stack before each test.
        /// </summary>
        [TestInitialize]
        public void Initialize()
        {
            var store = new InMemoryResourceStore(Options.Create(new ApiOptions()));
            _usgs = new FakeUsgsTimeSeriesService { Result = TestSeries.IrregularPeaks() };
            var timeSeries = new TimeSeriesService(store, _usgs);
            var inputData = new InputDataService(store, _usgs);
            var analyses = new AnalysisService(store, Options.Create(new ApiOptions()));
            var workflows = new WorkflowService(timeSeries, inputData, analyses);
            _controller = new WorkflowController(NullLogger<WorkflowController>.Instance, workflows);
        }

        /// <summary>
        /// Verifies a step failure returns HTTP 200 with the in-body failure contract and the
        /// shared timing/timestamp fields stamped by the controller pipeline.
        /// </summary>
        [TestMethod]
        public async Task Workflow_StepFailure_Returns200WithInBodyFailure()
        {
            _usgs.ExceptionToThrow = new UsgsDataNotFoundException("no data");
            var actionResult = await _controller.RunUsgsPeakFrequency(
                new UsgsPeakFrequencyWorkflowRequest { SiteNumber = "01646500" }, CancellationToken.None);
            var objectResult = (ObjectResult)actionResult.Result!;
            var body = (UsgsFrequencyWorkflowResponse)objectResult.Value!;

            Assert.AreEqual(200, objectResult.StatusCode);
            Assert.IsFalse(body.Success);
            Assert.AreEqual("createInputData", body.FailedStep);
            Assert.IsNotNull(body.ErrorMessage);
            Assert.IsNotNull(body.Timestamp);
            Assert.IsNotNull(body.ComputationTimeMs);
        }

        /// <summary>
        /// Verifies client cancellation maps to HTTP 499 through the controller pipeline.
        /// </summary>
        [TestMethod]
        public async Task Workflow_Cancelled_Returns499()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            var actionResult = await _controller.RunUsgsBulletin17C(
                new UsgsBulletin17CWorkflowRequest { SiteNumber = "01646500" }, cts.Token);
            var objectResult = (ObjectResult)actionResult.Result!;
            Assert.AreEqual(499, objectResult.StatusCode);
        }

        /// <summary>
        /// Verifies the rating curve workflow surface returns the in-body failure contract as well.
        /// </summary>
        [TestMethod]
        public async Task RatingCurveWorkflow_StepFailure_Returns200WithInBodyFailure()
        {
            _usgs.ExceptionToThrow = new UsgsUnavailableException("offline", statusCode: 503);
            var actionResult = await _controller.RunUsgsRatingCurve(
                new UsgsRatingCurveWorkflowRequest { SiteNumber = "01646500" }, CancellationToken.None);
            var objectResult = (ObjectResult)actionResult.Result!;
            var body = (UsgsRatingCurveWorkflowResponse)objectResult.Value!;

            Assert.AreEqual(200, objectResult.StatusCode);
            Assert.IsFalse(body.Success);
            Assert.AreEqual("downloadStage", body.FailedStep);
        }
    }
}
