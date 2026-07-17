using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.DTOs
{
    /// <summary>
    /// Unit tests for <see cref="DistributionFitDto"/>: defaults, JSON round-trip, and camelCase
    /// wire names.
    /// </summary>
    [TestClass]
    public class DistributionFitDtoTests
    {
        /// <summary>
        /// Verifies the defaults: unranked, unnamed, unsuccessful with null criteria.
        /// </summary>
        [TestMethod]
        public void Defaults_AreExpected()
        {
            var fit = new DistributionFitDto();
            Assert.AreEqual(0, fit.Rank);
            Assert.IsNull(fit.Distribution);
            Assert.IsNull(fit.DisplayName);
            Assert.IsNull(fit.Parameters);
            Assert.IsNull(fit.Aic);
            Assert.IsNull(fit.Bic);
            Assert.IsNull(fit.Rmse);
            Assert.IsFalse(fit.FitSucceeded);
            Assert.IsNull(fit.ErrorMessage);
        }

        /// <summary>
        /// Verifies every property survives a JSON round-trip.
        /// </summary>
        [TestMethod]
        public void Roundtrip_PreservesValues()
        {
            var fit = new DistributionFitDto
            {
                Rank = 1,
                Distribution = "logPearsonTypeIII",
                DisplayName = "Log-Pearson Type III",
                Parameters = new List<ParameterValueDto> { new() { Name = "Mean (of log)", Value = 3.5 } },
                Aic = 250.4,
                Bic = 255.1,
                Rmse = 12.3,
                FitSucceeded = true
            };

            var copy = TestJson.Roundtrip(fit);

            Assert.AreEqual(1, copy.Rank);
            Assert.AreEqual("logPearsonTypeIII", copy.Distribution);
            Assert.AreEqual(1, copy.Parameters!.Count);
            Assert.AreEqual(250.4, copy.Aic);
            Assert.AreEqual(255.1, copy.Bic);
            Assert.AreEqual(12.3, copy.Rmse);
            Assert.IsTrue(copy.FitSucceeded);
        }

        /// <summary>
        /// Verifies the serialized JSON uses camelCase wire names and omits nulls.
        /// </summary>
        [TestMethod]
        public void Serialize_UsesCamelCaseWireNames()
        {
            string json = TestJson.Serialize(new DistributionFitDto { Rank = 2, Distribution = "gumbel", FitSucceeded = true });
            StringAssert.Contains(json, "\"rank\"");
            StringAssert.Contains(json, "\"distribution\"");
            StringAssert.Contains(json, "\"fitSucceeded\"");
            Assert.IsFalse(json.Contains("\"errorMessage\""), "Null error message should be omitted.");
        }
    }
}
