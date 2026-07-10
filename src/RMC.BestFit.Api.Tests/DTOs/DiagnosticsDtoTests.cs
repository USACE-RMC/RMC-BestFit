using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.DTOs
{
    /// <summary>
    /// Unit tests for <see cref="DiagnosticsDto"/>: defaults, JSON round-trip, and camelCase
    /// wire names.
    /// </summary>
    [TestClass]
    public class DiagnosticsDtoTests
    {
        /// <summary>
        /// Verifies a new diagnostics object defaults to null sampler settings (non-chain methods)
        /// with an empty convergence-warning list.
        /// </summary>
        [TestMethod]
        public void Defaults_AreExpected()
        {
            var diagnostics = new DiagnosticsDto();
            Assert.IsNull(diagnostics.Sampler);
            Assert.IsNull(diagnostics.Iterations);
            Assert.IsNull(diagnostics.WarmupIterations);
            Assert.IsNull(diagnostics.NumberOfChains);
            Assert.IsNull(diagnostics.AcceptanceRates);
            Assert.IsNotNull(diagnostics.ConvergenceWarnings);
            Assert.AreEqual(0, diagnostics.ConvergenceWarnings.Count);
            Assert.IsNull(diagnostics.ElapsedMs);
        }

        /// <summary>
        /// Verifies every property, including the per-chain acceptance rates, survives a JSON round-trip.
        /// </summary>
        [TestMethod]
        public void Roundtrip_PreservesValues()
        {
            var diagnostics = new DiagnosticsDto
            {
                Sampler = "demCzs",
                Iterations = 20000,
                WarmupIterations = 10000,
                NumberOfChains = 4,
                AcceptanceRates = new List<double> { 0.28, 0.31, 0.27, 0.3 },
                ConvergenceWarnings = new List<string> { "R-hat above 1.1 for Sigma." },
                ElapsedMs = 4210
            };

            var copy = TestJson.Roundtrip(diagnostics);

            Assert.AreEqual("demCzs", copy.Sampler);
            Assert.AreEqual(20000, copy.Iterations);
            Assert.AreEqual(10000, copy.WarmupIterations);
            Assert.AreEqual(4, copy.NumberOfChains);
            CollectionAssert.AreEqual(diagnostics.AcceptanceRates, copy.AcceptanceRates);
            CollectionAssert.AreEqual(diagnostics.ConvergenceWarnings, copy.ConvergenceWarnings);
            Assert.AreEqual(4210L, copy.ElapsedMs);
        }

        /// <summary>
        /// Verifies the serialized JSON uses camelCase wire names and omits null fields.
        /// </summary>
        [TestMethod]
        public void Serialize_UsesCamelCaseWireNames()
        {
            string json = TestJson.Serialize(new DiagnosticsDto
            {
                Sampler = "demCzs",
                ElapsedMs = 15
            });
            StringAssert.Contains(json, "\"sampler\"");
            StringAssert.Contains(json, "\"convergenceWarnings\"");
            StringAssert.Contains(json, "\"elapsedMs\"");
            Assert.IsFalse(json.Contains("\"acceptanceRates\""), "Null acceptanceRates should be omitted.");
            Assert.IsFalse(json.Contains("\"iterations\""), "Null iterations should be omitted.");
        }
    }
}
