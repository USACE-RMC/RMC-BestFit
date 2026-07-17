using RMC.BestFit.Api.Configuration;

namespace RMC.BestFit.Api.Tests.Configuration
{
    /// <summary>
    /// Unit tests for <see cref="ApiOptions"/> defaults and property round-trips.
    /// </summary>
    [TestClass]
    public class ApiOptionsTests
    {
        /// <summary>
        /// Verifies the documented defaults.
        /// </summary>
        [TestMethod]
        public void Defaults_MatchDocumentation()
        {
            var options = new ApiOptions();
            Assert.AreEqual(500, options.MaxResources);
            Assert.AreEqual(2, options.MaxConcurrentRuns);
            Assert.AreEqual(500_000, options.MaxIterations);
            Assert.AreEqual("Api", ApiOptions.SectionName);
        }

        /// <summary>
        /// Verifies the properties round-trip assigned values.
        /// </summary>
        [TestMethod]
        public void Properties_RoundTrip()
        {
            var options = new ApiOptions { MaxResources = 10, MaxConcurrentRuns = 1, MaxIterations = 1000 };
            Assert.AreEqual(10, options.MaxResources);
            Assert.AreEqual(1, options.MaxConcurrentRuns);
            Assert.AreEqual(1000, options.MaxIterations);
        }
    }
}
