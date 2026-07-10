using RMC.BestFit.Api.Services.Exceptions;

namespace RMC.BestFit.Api.Tests.Services.Exceptions
{
    /// <summary>
    /// Unit tests for <see cref="RequestValidationException"/> error-list carriage.
    /// </summary>
    [TestClass]
    public class RequestValidationExceptionTests
    {
        /// <summary>
        /// Verifies the error list is captured and the default summary message is used.
        /// </summary>
        [TestMethod]
        public void Constructor_Errors_CapturedWithDefaultMessage()
        {
            var ex = new RequestValidationException(new[] { "error one", "error two" });
            Assert.AreEqual(2, ex.Errors.Count);
            Assert.AreEqual("error one", ex.Errors[0]);
            StringAssert.Contains(ex.Message, "validationErrors");
        }

        /// <summary>
        /// Verifies a custom summary message overrides the default.
        /// </summary>
        [TestMethod]
        public void Constructor_CustomMessage_Preserved()
        {
            var ex = new RequestValidationException(new[] { "e" }, "custom summary");
            Assert.AreEqual("custom summary", ex.Message);
        }
    }
}
