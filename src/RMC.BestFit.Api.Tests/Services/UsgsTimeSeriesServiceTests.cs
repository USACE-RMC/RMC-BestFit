using Numerics.Data;
using RMC.BestFit.Api.Services;
using RMC.BestFit.Api.Services.Exceptions;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.Services
{
    /// <summary>
    /// Unit tests for <see cref="UsgsTimeSeriesService"/> exception translation, exercised through
    /// the injectable download delegate (no network access).
    /// </summary>
    [TestClass]
    public class UsgsTimeSeriesServiceTests
    {
        /// <summary>
        /// Builds a service whose download delegate throws the given exception.
        /// </summary>
        /// <param name="exception">The exception the underlying download throws.</param>
        /// <returns>The service under test.</returns>
        private static UsgsTimeSeriesService CreateThrowing(Exception exception)
        {
            return new UsgsTimeSeriesService((_, _) => throw exception);
        }

        /// <summary>
        /// Verifies the constructor rejects a null delegate.
        /// </summary>
        [TestMethod]
        public void Constructor_NullDelegate_Throws()
        {
            Assert.ThrowsException<ArgumentNullException>(() => _ = new UsgsTimeSeriesService(null!));
        }

        /// <summary>
        /// Verifies a successful download returns the series and raw text unchanged.
        /// </summary>
        [TestMethod]
        public async Task DownloadAsync_Success_ReturnsSeriesAndRawText()
        {
            var series = TestSeries.IrregularPeaks();
            var service = new UsgsTimeSeriesService((_, _) => Task.FromResult((series, "raw")));
            var (result, rawText) = await service.DownloadAsync("01646500", TimeSeriesDownload.TimeSeriesType.PeakDischarge);
            Assert.AreSame(series, result);
            Assert.AreEqual("raw", rawText);
        }

        /// <summary>
        /// Verifies an invalid-request failure (bad site number) is rethrown as ArgumentException (HTTP 400).
        /// </summary>
        [TestMethod]
        public async Task DownloadAsync_ArgumentException_Rethrown()
        {
            var service = CreateThrowing(new ArgumentException("The USGS site number must be 8 digits."));
            await Assert.ThrowsExceptionAsync<ArgumentException>(
                () => service.DownloadAsync("123", TimeSeriesDownload.TimeSeriesType.DailyDischarge));
        }

        /// <summary>
        /// Verifies the no-internet failure maps to <see cref="UsgsUnavailableException"/> with status 503.
        /// </summary>
        [TestMethod]
        public async Task DownloadAsync_NoInternet_Maps503()
        {
            var service = CreateThrowing(new InvalidOperationException("No internet connection."));
            var ex = await Assert.ThrowsExceptionAsync<UsgsUnavailableException>(
                () => service.DownloadAsync("01646500", TimeSeriesDownload.TimeSeriesType.DailyDischarge));
            Assert.AreEqual(503, ex.StatusCode);
        }

        /// <summary>
        /// Verifies the no-data failure maps to <see cref="UsgsDataNotFoundException"/> (HTTP 404).
        /// </summary>
        [TestMethod]
        public async Task DownloadAsync_NoDataFound_MapsNotFound()
        {
            var service = CreateThrowing(new Exception("No data found matching all criteria."));
            var ex = await Assert.ThrowsExceptionAsync<UsgsDataNotFoundException>(
                () => service.DownloadAsync("01646500", TimeSeriesDownload.TimeSeriesType.PeakDischarge));
            StringAssert.Contains(ex.Message, "01646500");
        }

        /// <summary>
        /// Verifies any other upstream failure maps to <see cref="UsgsUnavailableException"/> with status 502.
        /// </summary>
        [TestMethod]
        public async Task DownloadAsync_UpstreamError_Maps502()
        {
            var service = CreateThrowing(new Exception("USGS returned status 500: server error"));
            var ex = await Assert.ThrowsExceptionAsync<UsgsUnavailableException>(
                () => service.DownloadAsync("01646500", TimeSeriesDownload.TimeSeriesType.DailyDischarge));
            Assert.AreEqual(502, ex.StatusCode);
        }

        /// <summary>
        /// Verifies an empty downloaded series maps to <see cref="UsgsDataNotFoundException"/>.
        /// </summary>
        [TestMethod]
        public async Task DownloadAsync_EmptySeries_MapsNotFound()
        {
            var service = new UsgsTimeSeriesService((_, _) => Task.FromResult((new TimeSeries(TimeInterval.OneDay), "raw")));
            await Assert.ThrowsExceptionAsync<UsgsDataNotFoundException>(
                () => service.DownloadAsync("01646500", TimeSeriesDownload.TimeSeriesType.DailyDischarge));
        }

        /// <summary>
        /// Verifies a pre-cancelled token short-circuits with <see cref="OperationCanceledException"/>
        /// before the download runs.
        /// </summary>
        [TestMethod]
        public async Task DownloadAsync_PreCancelled_Throws()
        {
            bool called = false;
            var service = new UsgsTimeSeriesService((_, _) =>
            {
                called = true;
                return Task.FromResult((TestSeries.IrregularPeaks(), "raw"));
            });
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            await Assert.ThrowsExceptionAsync<OperationCanceledException>(
                () => service.DownloadAsync("01646500", TimeSeriesDownload.TimeSeriesType.DailyDischarge, cts.Token));
            Assert.IsFalse(called);
        }
    }
}
