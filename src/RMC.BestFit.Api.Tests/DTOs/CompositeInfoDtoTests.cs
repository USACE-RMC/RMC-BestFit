using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.DTOs
{
    /// <summary>
    /// Unit tests for <see cref="CompositeInfoDto"/> and its supporting
    /// <see cref="CompositeComponentInfoDto"/>: defaults, JSON round-trip, and camelCase wire names.
    /// </summary>
    [TestClass]
    public class CompositeInfoDtoTests
    {
        /// <summary>
        /// Verifies the defaults: null settings and an empty component list.
        /// </summary>
        [TestMethod]
        public void Defaults_AreExpected()
        {
            var info = new CompositeInfoDto();
            Assert.IsNull(info.CompositeType);
            Assert.IsNull(info.AverageMethod);
            Assert.IsNull(info.Dependency);
            Assert.IsNull(info.IsMaximum);
            Assert.AreEqual(0, info.Components.Count);

            var component = new CompositeComponentInfoDto();
            Assert.IsNull(component.AnalysisId);
            Assert.IsNull(component.Name);
            Assert.IsNull(component.Kind);
            Assert.AreEqual(0d, component.Weight);
        }

        /// <summary>
        /// Verifies every property survives a JSON round-trip.
        /// </summary>
        [TestMethod]
        public void Roundtrip_PreservesValues()
        {
            var info = new CompositeInfoDto
            {
                CompositeType = "modelAverage",
                AverageMethod = "waic",
                Dependency = "independent",
                IsMaximum = true,
                Components = new List<CompositeComponentInfoDto>
                {
                    new() { AnalysisId = Guid.NewGuid(), Name = "gumbel fit", Kind = "univariate", Weight = 0.7 }
                }
            };

            var copy = TestJson.Roundtrip(info);

            Assert.AreEqual("modelAverage", copy.CompositeType);
            Assert.AreEqual("waic", copy.AverageMethod);
            Assert.AreEqual("independent", copy.Dependency);
            Assert.IsTrue(copy.IsMaximum);
            Assert.AreEqual(1, copy.Components.Count);
            Assert.AreEqual(0.7, copy.Components[0].Weight);
            Assert.AreEqual("univariate", copy.Components[0].Kind);
        }

        /// <summary>
        /// Verifies the serialized JSON uses camelCase wire names.
        /// </summary>
        [TestMethod]
        public void Serialize_UsesCamelCaseWireNames()
        {
            string json = TestJson.Serialize(new CompositeInfoDto
            {
                CompositeType = "mixture",
                Components = new List<CompositeComponentInfoDto> { new() { Weight = 0.5 } }
            });
            StringAssert.Contains(json, "\"compositeType\"");
            StringAssert.Contains(json, "\"components\"");
            StringAssert.Contains(json, "\"weight\"");
        }
    }
}
