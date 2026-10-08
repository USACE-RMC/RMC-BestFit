using Microsoft.Extensions.Options;
using Numerics.Data;
using Numerics.Distributions;
using RMC.BestFit.Api.Configuration;
using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Services;
using RMC.BestFit.Api.Services.Exceptions;
using RMC.BestFit.Api.Store;
using RMC.BestFit.Api.Tests.Support;
using RMC.BestFit.Models;

namespace RMC.BestFit.Api.Tests.Services
{
    /// <summary>
    /// Unit tests for <see cref="InputDataService"/>: manual data-frame assembly, block-maxima and
    /// peaks-over-threshold extraction against a synthetic series with planted maxima, and the
    /// direct USGS peak path through the faked download seam. No estimators are run.
    /// </summary>
    [TestClass]
    public class InputDataServiceTests
    {
        /// <summary>
        /// The store backing the service under test.
        /// </summary>
        private InMemoryResourceStore _store = null!;

        /// <summary>
        /// The faked USGS seam.
        /// </summary>
        private FakeUsgsTimeSeriesService _usgs = null!;

        /// <summary>
        /// The service under test.
        /// </summary>
        private InputDataService _service = null!;

        /// <summary>
        /// Creates a fresh store, fake, and service before each test.
        /// </summary>
        [TestInitialize]
        public void Initialize()
        {
            _store = new InMemoryResourceStore(Options.Create(new ApiOptions()));
            _usgs = new FakeUsgsTimeSeriesService();
            _service = new InputDataService(_store, _usgs);
        }

        /// <summary>
        /// Adds the synthetic three-water-year daily series to the store as a resource.
        /// </summary>
        /// <returns>The stored time-series resource.</returns>
        private TimeSeriesResource AddDailySeries()
        {
            var resource = new TimeSeriesResource(TestSeries.DailyThreeWaterYears())
            {
                Name = "daily",
                Source = TimeSeriesSource.Manual
            };
            return _store.AddTimeSeries(resource);
        }

        /// <summary>
        /// Verifies constructor null guards.
        /// </summary>
        [TestMethod]
        public void Constructor_NullDependencies_Throw()
        {
            Assert.ThrowsException<ArgumentNullException>(() => _ = new InputDataService(null!, _usgs));
            Assert.ThrowsException<ArgumentNullException>(() => _ = new InputDataService(_store, null!));
        }

        /// <summary>
        /// Verifies manual creation populates exact observations, computes plotting positions, and
        /// resolves indices from either 'index' or 'dateTime'.
        /// </summary>
        [TestMethod]
        public void CreateManual_PopulatesExactSeriesAndPlottingPositions()
        {
            var request = new CreateManualInputDataRequest
            {
                ExactData = new List<ExactObservationDto>
                {
                    new() { Index = 2000, Value = 100d },
                    new() { DateTime = new DateTime(2001, 5, 1), Value = 200d },
                    new() { Index = 2002, Value = 150d }
                }
            };

            var resource = _service.CreateManual(request);
            var dataFrame = resource.DataFrame;

            Assert.AreEqual(InputDataMethod.Manual, resource.Method);
            Assert.AreEqual(3, dataFrame.ExactSeries.Count);
            foreach (ExactData data in dataFrame.ExactSeries)
            {
                Assert.IsTrue(data.PlottingPosition > 0d && data.PlottingPosition < 1d,
                    $"Plotting position {data.PlottingPosition} for index {data.Index} was not computed.");
            }
        }

        /// <summary>
        /// Verifies an exact observation without index or dateTime is rejected.
        /// </summary>
        [TestMethod]
        public void CreateManual_ObservationWithoutIndexOrDate_Throws()
        {
            var request = new CreateManualInputDataRequest
            {
                ExactData = new List<ExactObservationDto> { new() { Value = 100d } }
            };
            Assert.ThrowsException<ArgumentException>(() => _service.CreateManual(request));
        }

        /// <summary>
        /// Verifies interval data defaults its representative value to the midpoint, and threshold
        /// records carry the number-above count.
        /// </summary>
        [TestMethod]
        public void CreateManual_IntervalAndThresholdData_Populated()
        {
            var request = new CreateManualInputDataRequest
            {
                ExactData = new List<ExactObservationDto>
                {
                    new() { Index = 2000, Value = 100d },
                    new() { Index = 2001, Value = 140d }
                },
                IntervalData = new List<IntervalObservationDto>
                {
                    new() { Index = 1950, LowerBound = 200d, UpperBound = 300d }
                },
                ThresholdData = new List<ThresholdObservationDto>
                {
                    new() { StartIndex = 1900, EndIndex = 1949, Value = 180d, NumberAbove = 1 }
                }
            };

            var resource = _service.CreateManual(request);
            var dataFrame = resource.DataFrame;

            Assert.AreEqual(1, dataFrame.IntervalSeries.Count);
            var interval = (IntervalData)dataFrame.IntervalSeries[0];
            Assert.AreEqual(250d, interval.Value);

            Assert.AreEqual(1, dataFrame.ThresholdSeries.Count);
            var threshold = (ThresholdData)dataFrame.ThresholdSeries[0];
            Assert.AreEqual(1, threshold.NumberAbove);
            Assert.AreEqual(50, threshold.Duration);
        }

        /// <summary>
        /// Verifies an explicit lambda overrides the recomputed events-per-span value.
        /// </summary>
        [TestMethod]
        public void CreateManual_ExplicitLambda_Overrides()
        {
            var request = new CreateManualInputDataRequest
            {
                ExactData = new List<ExactObservationDto>
                {
                    new() { Index = 2000, Value = 100d },
                    new() { Index = 2000, Value = 140d },
                    new() { Index = 2001, Value = 120d }
                },
                Lambda = 1.5
            };
            var resource = _service.CreateManual(request);
            Assert.AreEqual(1.5, resource.DataFrame.Lambda);
        }

        /// <summary>
        /// Verifies invalid manual data (start index after end index on a threshold) surfaces the
        /// model-layer validation messages as a request validation failure.
        /// </summary>
        [TestMethod]
        public void CreateManual_InvalidThreshold_ThrowsRequestValidation()
        {
            var request = new CreateManualInputDataRequest
            {
                ExactData = new List<ExactObservationDto> { new() { Index = 2000, Value = 100d } },
                ThresholdData = new List<ThresholdObservationDto>
                {
                    new() { StartIndex = 1950, EndIndex = 1900, Value = 180d }
                }
            };
            var ex = Assert.ThrowsException<RequestValidationException>(() => _service.CreateManual(request));
            Assert.IsTrue(ex.Errors.Count > 0);
        }

        /// <summary>
        /// Verifies water-year block-maxima extraction recovers exactly the planted annual maxima
        /// of the synthetic daily series.
        /// </summary>
        [TestMethod]
        public void CreateBlockMax_RecoversPlantedWaterYearMaxima()
        {
            var source = AddDailySeries();
            var request = new CreateBlockMaxInputDataRequest { TimeSeriesId = source.Id };

            var resource = _service.CreateBlockMax(request);
            var dataFrame = resource.DataFrame;

            Assert.AreEqual(InputDataMethod.BlockMaxima, resource.Method);
            Assert.AreEqual(source.Id, resource.SourceTimeSeriesId);
            Assert.AreEqual(TestSeries.PlantedMaxima.Count, dataFrame.ExactSeries.Count);

            var values = new List<double>();
            foreach (ExactData data in dataFrame.ExactSeries) values.Add(data.Value);
            CollectionAssert.AreEquivalent(TestSeries.PlantedMaxima.Values.ToList(), values);
        }

        /// <summary>
        /// Verifies block-maxima extraction fails with 404 semantics when the source id is unknown.
        /// </summary>
        [TestMethod]
        public void CreateBlockMax_UnknownTimeSeries_ThrowsNotFound()
        {
            var request = new CreateBlockMaxInputDataRequest { TimeSeriesId = Guid.NewGuid() };
            Assert.ThrowsException<ResourceNotFoundException>(() => _service.CreateBlockMax(request));
        }

        /// <summary>
        /// Verifies peaks-over-threshold extraction finds the planted peaks and reports the
        /// model-computed lambda.
        /// </summary>
        [TestMethod]
        public void CreatePeaksOverThreshold_FindsPlantedPeaks_AndComputesLambda()
        {
            var source = AddDailySeries();
            var request = new CreatePotInputDataRequest
            {
                TimeSeriesId = source.Id,
                Threshold = 400d,
                MinStepsBetweenPeaks = 7
            };

            var resource = _service.CreatePeaksOverThreshold(request);
            var dataFrame = resource.DataFrame;

            Assert.AreEqual(InputDataMethod.PeaksOverThreshold, resource.Method);
            Assert.AreEqual(3, dataFrame.ExactSeries.Count);

            // Lambda is events per observed year of the source record (3 peaks over the four
            // inclusive record years 1990-1993 = 0.75), not events per span of the retained peak
            // years (1991-1993 = 1.0). Clients needing a different rate pass an explicit lambda.
            Assert.AreEqual(0.75, dataFrame.Lambda, 1e-9);
            Assert.AreEqual(400d, resource.Threshold);
        }

        /// <summary>
        /// Verifies an explicit lambda on the POT request overrides the model-computed value.
        /// </summary>
        [TestMethod]
        public void CreatePeaksOverThreshold_ExplicitLambda_Overrides()
        {
            var source = AddDailySeries();
            var request = new CreatePotInputDataRequest
            {
                TimeSeriesId = source.Id,
                Threshold = 400d,
                MinStepsBetweenPeaks = 7,
                Lambda = 0.75
            };
            var resource = _service.CreatePeaksOverThreshold(request);
            Assert.AreEqual(0.75, resource.DataFrame.Lambda, 1e-9);
        }

        /// <summary>
        /// Verifies a threshold above every value produces a request validation failure rather
        /// than an empty resource.
        /// </summary>
        [TestMethod]
        public void CreatePeaksOverThreshold_ThresholdAboveAllValues_ThrowsRequestValidation()
        {
            var source = AddDailySeries();
            var request = new CreatePotInputDataRequest { TimeSeriesId = source.Id, Threshold = 10_000d };
            Assert.ThrowsException<RequestValidationException>(() => _service.CreatePeaksOverThreshold(request));
        }

        /// <summary>
        /// Verifies the direct USGS peak path populates exact observations from the downloaded
        /// series through the faked seam.
        /// </summary>
        [TestMethod]
        public async Task CreateFromUsgsPeaksAsync_PopulatesExactSeries()
        {
            _usgs.Result = TestSeries.IrregularPeaks();
            var request = new CreateUsgsPeaksInputDataRequest { SiteNumber = "01646500" };

            var resource = await _service.CreateFromUsgsPeaksAsync(request);

            Assert.AreEqual("01646500", _usgs.LastSiteNumber);
            Assert.AreEqual(TimeSeriesDownload.TimeSeriesType.PeakDischarge, _usgs.LastSeriesType);
            Assert.AreEqual(InputDataMethod.UsgsPeakDischarge, resource.Method);
            Assert.AreEqual("01646500", resource.UsgsSiteNumber);
            Assert.AreEqual(10, resource.DataFrame.ExactSeries.Count);
        }

        /// <summary>
        /// Verifies the peak-stage series type maps to the peak-stage method discriminator.
        /// </summary>
        [TestMethod]
        public async Task CreateFromUsgsPeaksAsync_PeakStage_MapsMethod()
        {
            _usgs.Result = TestSeries.IrregularPeaks();
            var request = new CreateUsgsPeaksInputDataRequest
            {
                SiteNumber = "01646500",
                SeriesType = TimeSeriesDownload.TimeSeriesType.PeakStage
            };
            var resource = await _service.CreateFromUsgsPeaksAsync(request);
            Assert.AreEqual(InputDataMethod.UsgsPeakStage, resource.Method);
        }

        /// <summary>
        /// Verifies non-peak series types are rejected before any download occurs.
        /// </summary>
        [TestMethod]
        public async Task CreateFromUsgsPeaksAsync_NonPeakSeriesType_Throws()
        {
            var request = new CreateUsgsPeaksInputDataRequest
            {
                SiteNumber = "01646500",
                SeriesType = TimeSeriesDownload.TimeSeriesType.DailyDischarge
            };
            await Assert.ThrowsExceptionAsync<ArgumentException>(() => _service.CreateFromUsgsPeaksAsync(request));
            Assert.IsNull(_usgs.LastSiteNumber);
        }

        /// <summary>
        /// Verifies summary statistics are computed for a stored resource and Get/Delete enforce
        /// not-found semantics.
        /// </summary>
        [TestMethod]
        public void SummaryStatistics_Get_Delete_Lifecycle()
        {
            var resource = _service.CreateManual(new CreateManualInputDataRequest
            {
                ExactData = new List<ExactObservationDto>
                {
                    new() { Index = 2000, Value = 100d },
                    new() { Index = 2001, Value = 200d },
                    new() { Index = 2002, Value = 300d }
                }
            });

            var statistics = _service.GetSummaryStatistics(resource.Id);
            Assert.IsTrue(statistics.Count > 0);

            Assert.AreSame(resource, _service.Get(resource.Id));
            _service.Delete(resource.Id);
            Assert.ThrowsException<ResourceNotFoundException>(() => _service.Get(resource.Id));
            Assert.ThrowsException<ResourceNotFoundException>(() => _service.GetSummaryStatistics(resource.Id));
            Assert.ThrowsException<ResourceNotFoundException>(() => _service.Delete(resource.Id));
        }

        /// <summary>
        /// Verifies manual creation builds the uncertain series from measurement-error
        /// distribution specs, with the nominal value taken from the distribution mean.
        /// </summary>
        [TestMethod]
        public void CreateManual_BuildsUncertainSeries()
        {
            var resource = _service.CreateManual(new CreateManualInputDataRequest
            {
                ExactData = new List<ExactObservationDto>
                {
                    new() { Index = 2000, Value = 100d },
                    new() { Index = 2001, Value = 200d },
                    new() { Index = 2002, Value = 300d }
                },
                UncertainData = new List<UncertainObservationDto>
                {
                    new()
                    {
                        Index = 1875,
                        Distribution = new DistributionSpecDto
                        {
                            Type = UnivariateDistributionType.Triangular,
                            Parameters = new List<double> { 400d, 550d, 900d }
                        }
                    },
                    new()
                    {
                        DateTime = new DateTime(1902, 6, 1),
                        Distribution = new DistributionSpecDto
                        {
                            Type = UnivariateDistributionType.Normal,
                            Parameters = new List<double> { 500d, 50d }
                        }
                    }
                }
            });

            var series = resource.DataFrame.UncertainSeries;
            Assert.AreEqual(2, series.Count);
            var first = (UncertainData)series[0];
            Assert.AreEqual(1875, first.Index);
            Assert.AreEqual(UnivariateDistributionType.Triangular, first.Distribution.Type);
            var second = (UncertainData)series[1];
            Assert.AreEqual(1902, second.Index, "The dateTime year must become the time index.");
            Assert.AreEqual(500d, second.Value, 1e-9, "The nominal value must be the distribution mean.");
        }

        /// <summary>
        /// Verifies uncertain-data validation failures surface as request errors: a missing
        /// index/date, an invalid distribution spec, and an index overlapping an exact observation.
        /// </summary>
        [TestMethod]
        public void CreateManual_UncertainValidation_Throws()
        {
            var exact = new List<ExactObservationDto>
            {
                new() { Index = 2000, Value = 100d },
                new() { Index = 2001, Value = 200d }
            };
            var goodSpec = new DistributionSpecDto { Type = UnivariateDistributionType.Normal, Parameters = new List<double> { 500d, 50d } };

            var missingIndex = Assert.ThrowsException<ArgumentException>(() => _service.CreateManual(new CreateManualInputDataRequest
            {
                ExactData = exact,
                UncertainData = new List<UncertainObservationDto> { new() { Distribution = goodSpec } }
            }));
            StringAssert.Contains(missingIndex.Message, "uncertainData[0]");

            var badSpec = Assert.ThrowsException<ArgumentException>(() => _service.CreateManual(new CreateManualInputDataRequest
            {
                ExactData = exact,
                UncertainData = new List<UncertainObservationDto>
                {
                    new() { Index = 1875, Distribution = new DistributionSpecDto { Type = UnivariateDistributionType.Normal, Parameters = new List<double> { 500d } } }
                }
            }));
            StringAssert.Contains(badSpec.Message, "uncertainData[0].distribution");

            // An uncertain observation may not share a time index with an exact observation —
            // the data frame's own validation reports it (HTTP 400 via RequestValidationException).
            Assert.ThrowsException<RequestValidationException>(() => _service.CreateManual(new CreateManualInputDataRequest
            {
                ExactData = exact,
                UncertainData = new List<UncertainObservationDto> { new() { Index = 2000, Distribution = goodSpec } }
            }));
        }
    }
}
