using RMC.BestFit.Api.Services.Exceptions;

namespace RMC.BestFit.Api.Tests.Services.Exceptions
{
    /// <summary>
    /// Unit tests for <see cref="ResourceNotFoundException"/> message construction.
    /// </summary>
    [TestClass]
    public class ResourceNotFoundExceptionTests
    {
        /// <summary>
        /// Verifies the plain-message constructor preserves the message.
        /// </summary>
        [TestMethod]
        public void Constructor_Message_Preserved()
        {
            var ex = new ResourceNotFoundException("missing");
            Assert.AreEqual("missing", ex.Message);
        }

        /// <summary>
        /// Verifies the type-and-id constructor includes both in the message.
        /// </summary>
        [TestMethod]
        public void Constructor_TypeAndId_BuildsActionableMessage()
        {
            var id = Guid.NewGuid();
            var ex = new ResourceNotFoundException("time series", id);
            StringAssert.Contains(ex.Message, "time series");
            StringAssert.Contains(ex.Message, id.ToString());
        }
    }
}
