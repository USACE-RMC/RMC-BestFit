using Numerics.Distributions;
using RMC.BestFit.Analyses;
using RMC.BestFit.Api.Helpers;
using RMC.BestFit.Api.Store;
using RMC.BestFit.Api.Tests.Support;
using RMC.BestFit.Models;

namespace RMC.BestFit.Api.Tests.Helpers
{
    /// <summary>
    /// Unit tests for <see cref="AnalysisRunHelper"/>: the outcome-normalization contract
    /// (exercised with hand-built completion args — no estimator runs) and validation dispatch.
    /// </summary>
    [TestClass]
    public class AnalysisRunHelperTests
    {
        /// <summary>
        /// Verifies validation dispatches to the wrapped analysis for each kind.
        /// </summary>
        [TestMethod]
        public void Validate_DispatchesPerKind()
        {
            Assert.IsTrue(AnalysisRunHelper.Validate(TestAnalyses.CreateUnivariateResource()).IsValid);
            Assert.IsTrue(AnalysisRunHelper.Validate(TestAnalyses.CreateBulletin17CResource()).IsValid);
            Assert.IsTrue(AnalysisRunHelper.Validate(TestAnalyses.CreateRatingCurveResource()).IsValid);
        }

        /// <summary>
        /// Verifies an invalid configuration (decreasing probability ordinates, which the model
        /// requires to be strictly increasing) is reported invalid with messages.
        /// </summary>
        [TestMethod]
        public void Validate_DecreasingOrdinates_Invalid()
        {
            var resource = TestAnalyses.CreateUnivariateResource();
            resource.Univariate!.ProbabilityOrdinates.Clear();
            resource.Univariate.ProbabilityOrdinates.AddRange(new[] { 0.5, 0.1 });
            var (isValid, messages) = AnalysisRunHelper.Validate(resource);
            Assert.IsFalse(isValid);
            Assert.IsTrue(messages.Count > 0);
        }

        /// <summary>
        /// Verifies a successful completion passes the outcome check silently.
        /// </summary>
        [TestMethod]
        public void ThrowIfRunFailed_Success_NoThrow()
        {
            var completion = new AnalysisRunCompletedEventArgs(wasCanceled: false, succeeded: true, error: null);
            AnalysisRunHelper.ThrowIfRunFailed(completion, isEstimated: true, CancellationToken.None);
        }

        /// <summary>
        /// Verifies a cancelled completion maps to <see cref="OperationCanceledException"/> (HTTP 499).
        /// </summary>
        [TestMethod]
        public void ThrowIfRunFailed_Cancelled_Throws499Path()
        {
            var completion = new AnalysisRunCompletedEventArgs(wasCanceled: true, succeeded: false, error: null);
            Assert.ThrowsException<OperationCanceledException>(
                () => AnalysisRunHelper.ThrowIfRunFailed(completion, isEstimated: false, CancellationToken.None));
        }

        /// <summary>
        /// Verifies a request-token cancellation maps to <see cref="OperationCanceledException"/>
        /// even when the completion args did not record it.
        /// </summary>
        [TestMethod]
        public void ThrowIfRunFailed_TokenCancelled_Throws499Path()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            var completion = new AnalysisRunCompletedEventArgs(wasCanceled: false, succeeded: true, error: null);
            Assert.ThrowsException<OperationCanceledException>(
                () => AnalysisRunHelper.ThrowIfRunFailed(completion, isEstimated: true, cts.Token));
        }

        /// <summary>
        /// Verifies a captured model error surfaces with its message (HTTP 500 path). This is the
        /// exception-swallowing pattern: the model reports failures only through the event args.
        /// </summary>
        [TestMethod]
        public void ThrowIfRunFailed_CapturedError_ThrowsWithMessage()
        {
            var completion = new AnalysisRunCompletedEventArgs(wasCanceled: false, succeeded: false, error: new InvalidOperationException("sampler exploded"));
            var ex = Assert.ThrowsException<InvalidOperationException>(
                () => AnalysisRunHelper.ThrowIfRunFailed(completion, isEstimated: false, CancellationToken.None));
            StringAssert.Contains(ex.Message, "sampler exploded");
        }

        /// <summary>
        /// Verifies the silent-failure paths (no event fired, unsuccessful completion, or an
        /// unestimated fit) all throw rather than returning a 200-with-no-results.
        /// </summary>
        [TestMethod]
        public void ThrowIfRunFailed_SilentFailures_Throw()
        {
            Assert.ThrowsException<InvalidOperationException>(
                () => AnalysisRunHelper.ThrowIfRunFailed(null, isEstimated: false, CancellationToken.None));

            var unsuccessful = new AnalysisRunCompletedEventArgs(wasCanceled: false, succeeded: false, error: null);
            Assert.ThrowsException<InvalidOperationException>(
                () => AnalysisRunHelper.ThrowIfRunFailed(unsuccessful, isEstimated: false, CancellationToken.None));

            var succeededButNotEstimated = new AnalysisRunCompletedEventArgs(wasCanceled: false, succeeded: true, error: null);
            Assert.ThrowsException<InvalidOperationException>(
                () => AnalysisRunHelper.ThrowIfRunFailed(succeededButNotEstimated, isEstimated: false, CancellationToken.None));
        }
    }
}
