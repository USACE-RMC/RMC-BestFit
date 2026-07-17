using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.DTOs
{
    /// <summary>
    /// Unit tests for <see cref="InformationCriteriaDto"/>: defaults, JSON round-trip, and
    /// camelCase wire names.
    /// </summary>
    [TestClass]
    public class InformationCriteriaDtoTests
    {
        /// <summary>
        /// Verifies every criterion defaults to null so values undefined for the estimation
        /// method are omitted.
        /// </summary>
        [TestMethod]
        public void Defaults_AreExpected()
        {
            var criteria = new InformationCriteriaDto();
            Assert.IsNull(criteria.Aic);
            Assert.IsNull(criteria.Bic);
            Assert.IsNull(criteria.Dic);
            Assert.IsNull(criteria.Waic);
            Assert.IsNull(criteria.WaicPD);
            Assert.IsNull(criteria.Looic);
            Assert.IsNull(criteria.LooicSE);
            Assert.IsNull(criteria.Rmse);
            Assert.IsNull(criteria.Erl);
        }

        /// <summary>
        /// Verifies every criterion survives a JSON round-trip.
        /// </summary>
        [TestMethod]
        public void Roundtrip_PreservesValues()
        {
            var criteria = new InformationCriteriaDto
            {
                Aic = 512.4,
                Bic = 518.9,
                Dic = 510.2,
                Waic = 511.7,
                WaicPD = 2.9,
                Looic = 511.9,
                LooicSE = 7.4,
                Rmse = 1250.5,
                Erl = 96.5
            };

            var copy = TestJson.Roundtrip(criteria);

            Assert.AreEqual(512.4, copy.Aic);
            Assert.AreEqual(518.9, copy.Bic);
            Assert.AreEqual(510.2, copy.Dic);
            Assert.AreEqual(511.7, copy.Waic);
            Assert.AreEqual(2.9, copy.WaicPD);
            Assert.AreEqual(511.9, copy.Looic);
            Assert.AreEqual(7.4, copy.LooicSE);
            Assert.AreEqual(1250.5, copy.Rmse);
            Assert.AreEqual(96.5, copy.Erl);
        }

        /// <summary>
        /// Verifies the serialized JSON uses camelCase wire names and omits null criteria.
        /// </summary>
        [TestMethod]
        public void Serialize_UsesCamelCaseWireNames()
        {
            string json = TestJson.Serialize(new InformationCriteriaDto
            {
                Aic = 1.0,
                WaicPD = 2.0,
                LooicSE = 3.0
            });
            StringAssert.Contains(json, "\"aic\"");
            StringAssert.Contains(json, "\"waicPD\"");
            StringAssert.Contains(json, "\"looicSE\"");
            Assert.IsFalse(json.Contains("\"rmse\""), "Null rmse should be omitted.");

            string defaultJson = TestJson.Serialize(new InformationCriteriaDto());
            Assert.AreEqual("{}", defaultJson, "An all-default criteria object should serialize empty.");
        }
    }
}
