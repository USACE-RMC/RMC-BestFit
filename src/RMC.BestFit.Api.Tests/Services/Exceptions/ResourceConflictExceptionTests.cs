using RMC.BestFit.Api.Services.Exceptions;

namespace RMC.BestFit.Api.Tests.Services.Exceptions
{
    /// <summary>
    /// Unit tests for <see cref="ResourceConflictException"/> message construction.
    /// </summary>
    [TestClass]
    public class ResourceConflictExceptionTests
    {
        /// <summary>
        /// Verifies the constructor preserves the message.
        /// </summary>
        [TestMethod]
        public void Constructor_Message_Preserved()
        {
            var ex = new ResourceConflictException("conflict");
            Assert.AreEqual("conflict", ex.Message);
        }
    }
}
