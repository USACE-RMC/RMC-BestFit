using RMC.BestFit.Api.Services.Exceptions;

namespace RMC.BestFit.Api.Tests.Services.Exceptions
{
    /// <summary>
    /// Unit tests for <see cref="UsgsUnavailableException"/> status-code carriage.
    /// </summary>
    [TestClass]
    public class UsgsUnavailableExceptionTests
    {
        /// <summary>
        /// Verifies the default status code is 502 (upstream error).
        /// </summary>
        [TestMethod]
        public void Constructor_DefaultStatusCode_Is502()
        {
            var ex = new UsgsUnavailableException("upstream failed");
            Assert.AreEqual(502, ex.StatusCode);
            Assert.AreEqual("upstream failed", ex.Message);
        }

        /// <summary>
        /// Verifies the status code and inner exception are preserved.
        /// </summary>
        [TestMethod]
        public void Constructor_CustomStatusCodeAndInner_Preserved()
        {
            var inner = new InvalidOperationException("No internet connection.");
            var ex = new UsgsUnavailableException("offline", statusCode: 503, innerException: inner);
            Assert.AreEqual(503, ex.StatusCode);
            Assert.AreSame(inner, ex.InnerException);
        }
    }
}
