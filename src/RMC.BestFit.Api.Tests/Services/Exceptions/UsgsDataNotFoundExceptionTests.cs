using RMC.BestFit.Api.Services.Exceptions;

namespace RMC.BestFit.Api.Tests.Services.Exceptions
{
    /// <summary>
    /// Unit tests for <see cref="UsgsDataNotFoundException"/> message construction.
    /// </summary>
    [TestClass]
    public class UsgsDataNotFoundExceptionTests
    {
        /// <summary>
        /// Verifies the constructor preserves the message.
        /// </summary>
        [TestMethod]
        public void Constructor_Message_Preserved()
        {
            var ex = new UsgsDataNotFoundException("no data");
            Assert.AreEqual("no data", ex.Message);
        }
    }
}
