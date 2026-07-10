using Numerics.Data;
using Numerics.Distributions;
using RMC.BestFit.Api.Mappers;
using RMC.BestFit.Api.Store;
using RMC.BestFit.Models;

namespace RMC.BestFit.Api.Tests.Mappers
{
    /// <summary>
    /// Unit tests for <see cref="InputDataMapper"/>: summary composition, observation extraction,
    /// and the method-specific option echoes.
    /// </summary>
    [TestClass]
    public class InputDataMapperTests
    {
        /// <summary>
        /// Builds a data frame with exact, interval, and threshold observations.
        /// </summary>
        /// <returns>The data frame.</returns>
        private static DataFrame CreateDataFrame()
        {
            var dataFrame = new DataFrame();
            dataFrame.ExactSeries.Add(new ExactData(2000, 100d, 0.5, isLowOutlier: true));
            dataFrame.ExactSeries.Add(new ExactData(2001, 200d));
            dataFrame.UncertainSeries.Add(new UncertainData(1875, new Triangular(400d, 550d, 900d)));
            dataFrame.IntervalSeries.Add(new IntervalData(1950, 200d, 250d, 300d));
            dataFrame.ThresholdSeries.Add(new ThresholdData(1900, 1949, 180d) { NumberAbove = 1 });
            return dataFrame;
        }

        /// <summary>
        /// Verifies the summary carries record composition, lambda, and the low-outlier count.
        /// </summary>
        [TestMethod]
        public void ToSummary_MapsCompositionAndCounts()
        {
            var resource = new InputDataResource
            {
                Name = "data",
                DataFrame = CreateDataFrame(),
                Method = InputDataMethod.Manual
            };

            var summary = InputDataMapper.ToSummary(resource);

            Assert.AreEqual(resource.Id, summary.Id);
            Assert.AreEqual("manual", summary.Method);
            Assert.AreEqual(2, summary.ExactCount);
            Assert.AreEqual(1, summary.UncertainCount);
            Assert.AreEqual(1, summary.IntervalCount);
            Assert.AreEqual(1, summary.ThresholdCount);
            Assert.AreEqual(1, summary.LowOutlierCount);
            Assert.AreEqual(resource.DataFrame.Lambda, summary.Lambda);
            Assert.IsNull(summary.BlockOptions);
            Assert.IsNull(summary.PotOptions);
        }

        /// <summary>
        /// Verifies the block-options echo is populated for block-maxima resources.
        /// </summary>
        [TestMethod]
        public void ToSummary_BlockMaxima_EchoesOptions()
        {
            var resource = new InputDataResource
            {
                Name = "block",
                DataFrame = CreateDataFrame(),
                Method = InputDataMethod.BlockMaxima,
                TimeBlock = TimeBlockWindow.WaterYear,
                BlockFunction = BlockFunctionType.Maximum,
                SmoothingFunction = SmoothingFunctionType.None,
                StartMonth = 10,
                EndMonth = 9,
                Period = 1
            };

            var summary = InputDataMapper.ToSummary(resource);

            Assert.IsNotNull(summary.BlockOptions);
            Assert.AreEqual("waterYear", summary.BlockOptions.TimeBlock);
            Assert.AreEqual("maximum", summary.BlockOptions.BlockFunction);
            Assert.AreEqual(10, summary.BlockOptions.StartMonth);
            Assert.IsNull(summary.PotOptions);
        }

        /// <summary>
        /// Verifies the POT-options echo is populated for peaks-over-threshold resources.
        /// </summary>
        [TestMethod]
        public void ToSummary_PeaksOverThreshold_EchoesOptions()
        {
            var resource = new InputDataResource
            {
                Name = "pot",
                DataFrame = CreateDataFrame(),
                Method = InputDataMethod.PeaksOverThreshold,
                Threshold = 400d,
                MinStepsBetweenPeaks = 7,
                SmoothingFunction = SmoothingFunctionType.None,
                Period = 1
            };

            var summary = InputDataMapper.ToSummary(resource);

            Assert.IsNotNull(summary.PotOptions);
            Assert.AreEqual(400d, summary.PotOptions.Threshold);
            Assert.AreEqual(7, summary.PotOptions.MinStepsBetweenPeaks);
            Assert.IsNull(summary.BlockOptions);
        }

        /// <summary>
        /// Verifies observation lists are only included when requested and carry the model values.
        /// </summary>
        [TestMethod]
        public void ToResourceResponse_IncludeData_ExtractsObservations()
        {
            var resource = new InputDataResource
            {
                Name = "data",
                DataFrame = CreateDataFrame(),
                Method = InputDataMethod.Manual
            };

            var withoutData = InputDataMapper.ToResourceResponse(resource);
            Assert.IsNull(withoutData.ExactData);
            Assert.IsNull(withoutData.UncertainData);
            Assert.IsNull(withoutData.IntervalData);
            Assert.IsNull(withoutData.ThresholdData);

            var withData = InputDataMapper.ToResourceResponse(resource, includeData: true);
            Assert.IsNotNull(withData.ExactData);
            Assert.AreEqual(2, withData.ExactData.Count);
            Assert.AreEqual(2000, withData.ExactData[0].Index);
            Assert.AreEqual(100d, withData.ExactData[0].Value);
            Assert.IsTrue(withData.ExactData[0].IsLowOutlier);

            Assert.IsNotNull(withData.UncertainData);
            Assert.AreEqual(1875, withData.UncertainData[0].Index);
            Assert.IsNotNull(withData.UncertainData[0].Distribution);
            Assert.AreEqual(UnivariateDistributionType.Triangular, withData.UncertainData[0].Distribution!.Type);
            CollectionAssert.AreEqual(new List<double> { 400d, 550d, 900d }, withData.UncertainData[0].Distribution!.Parameters);
            Assert.IsNotNull(withData.UncertainData[0].Value);

            Assert.IsNotNull(withData.IntervalData);
            Assert.AreEqual(200d, withData.IntervalData[0].LowerBound);
            Assert.AreEqual(300d, withData.IntervalData[0].UpperBound);

            Assert.IsNotNull(withData.ThresholdData);
            Assert.AreEqual(1900, withData.ThresholdData[0].StartIndex);
            Assert.AreEqual(1, withData.ThresholdData[0].NumberAbove);
        }

        /// <summary>
        /// Verifies the list response carries the count and one summary per resource.
        /// </summary>
        [TestMethod]
        public void ToListResponse_MapsAllResources()
        {
            var resources = new List<InputDataResource>
            {
                new() { Name = "a", DataFrame = CreateDataFrame(), Method = InputDataMethod.Manual },
                new() { Name = "b", DataFrame = CreateDataFrame(), Method = InputDataMethod.Manual }
            };
            var response = InputDataMapper.ToListResponse(resources);
            Assert.AreEqual(2, response.Count);
            Assert.AreEqual(2, response.InputData.Count);
        }
    }
}
