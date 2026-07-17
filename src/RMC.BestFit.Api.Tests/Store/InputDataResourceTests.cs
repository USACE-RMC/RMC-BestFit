using Numerics.Data;
using RMC.BestFit.Api.Store;
using RMC.BestFit.Models;

namespace RMC.BestFit.Api.Tests.Store
{
    /// <summary>
    /// Unit tests for <see cref="InputDataResource"/> construction and provenance properties.
    /// </summary>
    [TestClass]
    public class InputDataResourceTests
    {
        /// <summary>
        /// Verifies the required properties and provenance init-properties round-trip.
        /// </summary>
        [TestMethod]
        public void Properties_RoundTrip()
        {
            var dataFrame = new DataFrame();
            var sourceId = Guid.NewGuid();
            var resource = new InputDataResource
            {
                Name = "block max",
                Description = "annual maxima",
                DataFrame = dataFrame,
                Method = InputDataMethod.BlockMaxima,
                SourceTimeSeriesId = sourceId,
                TimeBlock = TimeBlockWindow.WaterYear,
                BlockFunction = BlockFunctionType.Maximum,
                SmoothingFunction = SmoothingFunctionType.None,
                StartMonth = 10,
                EndMonth = 9,
                Period = 1
            };

            Assert.AreEqual("block max", resource.Name);
            Assert.AreEqual("annual maxima", resource.Description);
            Assert.AreSame(dataFrame, resource.DataFrame);
            Assert.AreEqual(InputDataMethod.BlockMaxima, resource.Method);
            Assert.AreEqual(sourceId, resource.SourceTimeSeriesId);
            Assert.AreEqual(TimeBlockWindow.WaterYear, resource.TimeBlock);
            Assert.AreEqual(BlockFunctionType.Maximum, resource.BlockFunction);
            Assert.AreEqual(SmoothingFunctionType.None, resource.SmoothingFunction);
            Assert.AreEqual(10, resource.StartMonth);
            Assert.AreEqual(9, resource.EndMonth);
            Assert.AreEqual(1, resource.Period);
            Assert.AreNotEqual(Guid.Empty, resource.Id);
        }

        /// <summary>
        /// Verifies optional provenance fields default to null for a manual resource.
        /// </summary>
        [TestMethod]
        public void Defaults_ManualResource_HasNullProvenance()
        {
            var resource = new InputDataResource { Name = "manual", DataFrame = new DataFrame(), Method = InputDataMethod.Manual };
            Assert.IsNull(resource.SourceTimeSeriesId);
            Assert.IsNull(resource.UsgsSiteNumber);
            Assert.IsNull(resource.TimeBlock);
            Assert.IsNull(resource.Threshold);
            Assert.IsNull(resource.MinStepsBetweenPeaks);
            Assert.IsNull(resource.Description);
        }
    }
}
