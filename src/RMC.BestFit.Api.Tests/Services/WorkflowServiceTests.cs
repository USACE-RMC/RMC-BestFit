using Microsoft.Extensions.Options;
using Numerics.Distributions;
using RMC.BestFit.Api.Configuration;
using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Services;
using RMC.BestFit.Api.Services.Exceptions;
using RMC.BestFit.Api.Store;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.Services
{
    /// <summary>
    /// Unit tests for <see cref="WorkflowService"/> step composition and in-body failure
    /// reporting, using the faked USGS seam. Only failure paths are exercised — success paths
    /// require estimator runs, which unit tests never perform.
    /// </summary>
    [TestClass]
    public class WorkflowServiceTests
    {
        /// <summary>
        /// The store shared by the composed services.
        /// </summary>
        private InMemoryResourceStore _store = null!;

        /// <summary>
        /// The faked USGS seam.
        /// </summary>
        private FakeUsgsTimeSeriesService _usgs = null!;

        /// <summary>
        /// The service under test.
        /// </summary>
        private WorkflowService _service = null!;

        /// <summary>
        /// Creates a fresh composed service stack before each test.
        /// </summary>
        [TestInitialize]
        public void Initialize()
        {
            _store = new InMemoryResourceStore(Options.Create(new ApiOptions()));
            _usgs = new FakeUsgsTimeSeriesService { Result = TestSeries.DailyThreeWaterYears() };
            var timeSeries = new TimeSeriesService(_store, _usgs);
            var inputData = new InputDataService(_store, _usgs);
            var analyses = new AnalysisService(_store, Options.Create(new ApiOptions()));
            _service = new WorkflowService(timeSeries, inputData, analyses);
        }

        /// <summary>
        /// Verifies constructor null guards.
        /// </summary>
        [TestMethod]
        public void Constructor_NullDependencies_Throw()
        {
            var timeSeries = new TimeSeriesService(_store, _usgs);
            var inputData = new InputDataService(_store, _usgs);
            var analyses = new AnalysisService(_store, Options.Create(new ApiOptions()));
            Assert.ThrowsException<ArgumentNullException>(() => _ = new WorkflowService(null!, inputData, analyses));
            Assert.ThrowsException<ArgumentNullException>(() => _ = new WorkflowService(timeSeries, null!, analyses));
            Assert.ThrowsException<ArgumentNullException>(() => _ = new WorkflowService(timeSeries, inputData, null!));
        }

        /// <summary>
        /// Verifies a USGS download failure is reported in-body on the first step with no ids created.
        /// </summary>
        [TestMethod]
        public async Task PeakFrequency_DownloadFails_ReportsFirstStep()
        {
            _usgs.ExceptionToThrow = new UsgsDataNotFoundException("no data for site");
            var response = await _service.RunUsgsPeakFrequencyAsync(new UsgsPeakFrequencyWorkflowRequest { SiteNumber = "01646500" });

            Assert.IsFalse(response.Success);
            Assert.AreEqual("createInputData", response.FailedStep);
            StringAssert.Contains(response.ErrorMessage, "no data for site");
            Assert.IsNull(response.InputDataId);
            Assert.IsNull(response.AnalysisId);
            Assert.IsNull(response.Results);
        }

        /// <summary>
        /// Verifies a mid-workflow failure preserves the ids of resources created by the completed
        /// steps: the Bulletin 17C workflow with an unsupported distribution fails at analysis
        /// creation, after the input data was created.
        /// </summary>
        [TestMethod]
        public async Task Bulletin17C_UnsupportedDistribution_KeepsInputDataId()
        {
            _usgs.Result = TestSeries.IrregularPeaks();
            var response = await _service.RunUsgsBulletin17CAsync(new UsgsBulletin17CWorkflowRequest
            {
                SiteNumber = "01646500",
                Distribution = UnivariateDistributionType.GeneralizedExtremeValue
            });

            Assert.IsFalse(response.Success);
            Assert.AreEqual("createAnalysis", response.FailedStep);
            Assert.IsNotNull(response.InputDataId, "The input-data id from the completed step must be preserved.");
            Assert.IsNotNull(_store.GetInputData(response.InputDataId!.Value));
            Assert.IsNull(response.AnalysisId);
            Assert.IsNull(response.Results);
        }

        /// <summary>
        /// Verifies the block-maxima workflow reports a first-step download failure with the
        /// step name specific to that workflow.
        /// </summary>
        [TestMethod]
        public async Task BlockMaxFrequency_DownloadFails_ReportsDownloadStep()
        {
            _usgs.ExceptionToThrow = new UsgsUnavailableException("offline", statusCode: 503);
            var response = await _service.RunUsgsBlockMaxFrequencyAsync(new UsgsBlockMaxFrequencyWorkflowRequest { SiteNumber = "01646500" });

            Assert.IsFalse(response.Success);
            Assert.AreEqual("downloadTimeSeries", response.FailedStep);
            Assert.IsNull(response.TimeSeriesId);
        }

        /// <summary>
        /// Verifies the rating curve workflow fails on the second download when only the first
        /// succeeds, preserving the stage series id.
        /// </summary>
        [TestMethod]
        public async Task RatingCurve_SecondDownloadFails_KeepsStageId()
        {
            // The fake throws only on the second call (discharge).
            int calls = 0;
            var stageSeries = TestSeries.IrregularPeaks();
            var throwingFake = new SequencedFakeUsgs(() =>
            {
                calls++;
                if (calls >= 2) throw new UsgsDataNotFoundException("no measured discharge");
                return stageSeries;
            });
            var timeSeries = new TimeSeriesService(_store, throwingFake);
            var inputData = new InputDataService(_store, throwingFake);
            var analyses = new AnalysisService(_store, Options.Create(new ApiOptions()));
            var service = new WorkflowService(timeSeries, inputData, analyses);

            var response = await service.RunUsgsRatingCurveAsync(new UsgsRatingCurveWorkflowRequest { SiteNumber = "01646500" });

            Assert.IsFalse(response.Success);
            Assert.AreEqual("downloadDischarge", response.FailedStep);
            Assert.IsNotNull(response.StageTimeSeriesId);
            Assert.IsNull(response.DischargeTimeSeriesId);
            Assert.IsNull(response.AnalysisId);
        }

        /// <summary>
        /// Verifies client cancellation propagates (mapped to HTTP 499 by the controller) rather
        /// than being folded into the response body.
        /// </summary>
        [TestMethod]
        public async Task Workflow_Cancellation_Propagates()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            await Assert.ThrowsExceptionAsync<OperationCanceledException>(
                () => _service.RunUsgsPeakFrequencyAsync(new UsgsPeakFrequencyWorkflowRequest { SiteNumber = "01646500" }, cts.Token));
        }

        /// <summary>
        /// USGS fake whose per-call behavior is scripted by a delegate, used to fail a specific
        /// step in a multi-download workflow.
        /// </summary>
        private sealed class SequencedFakeUsgs : IUsgsTimeSeriesService
        {
            /// <summary>
            /// Produces the series for each call, or throws to script a failure.
            /// </summary>
            private readonly Func<Numerics.Data.TimeSeries> _next;

            /// <summary>
            /// Constructs the fake with the per-call script.
            /// </summary>
            /// <param name="next">The per-call series factory.</param>
            public SequencedFakeUsgs(Func<Numerics.Data.TimeSeries> next)
            {
                _next = next;
            }

            /// <inheritdoc/>
            public Task<(Numerics.Data.TimeSeries TimeSeries, string RawText)> DownloadAsync(
                string siteNumber,
                Numerics.Data.TimeSeriesDownload.TimeSeriesType seriesType,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult((_next(), "scripted"));
            }
        }
    }
}
