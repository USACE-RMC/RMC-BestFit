using Numerics.Data;
using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Mappers;
using RMC.BestFit.Api.Services.Exceptions;
using RMC.BestFit.Api.Store;
using RMC.BestFit.Models;

namespace RMC.BestFit.Api.Services
{
    /// <summary>
    /// Default <see cref="IInputDataService"/> implementation. Builds model-layer
    /// <see cref="DataFrame"/> instances using the same series-population sequence as the model's
    /// own factory methods (suppress collection events, populate, raise a single reset) so lambda
    /// and plotting positions are recomputed exactly as in the desktop application.
    /// </summary>
    public class InputDataService : IInputDataService
    {
        /// <summary>
        /// The in-memory resource store.
        /// </summary>
        private readonly IResourceStore _store;

        /// <summary>
        /// The USGS download adapter (mockable seam for tests).
        /// </summary>
        private readonly IUsgsTimeSeriesService _usgs;

        /// <summary>
        /// Constructs the service.
        /// </summary>
        /// <param name="store">The in-memory resource store.</param>
        /// <param name="usgs">The USGS download adapter.</param>
        /// <exception cref="ArgumentNullException">Thrown when either dependency is null.</exception>
        public InputDataService(IResourceStore store, IUsgsTimeSeriesService usgs)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _usgs = usgs ?? throw new ArgumentNullException(nameof(usgs));
        }

        /// <inheritdoc/>
        public InputDataResource CreateManual(CreateManualInputDataRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (request.ExactData == null || request.ExactData.Count == 0)
            {
                throw new ArgumentException("At least one exact observation is required.", nameof(request));
            }

            var dataFrame = new DataFrame();
            if (request.PlottingParameter.HasValue) dataFrame.PlottingParameter = request.PlottingParameter.Value;
            if (request.LowOutlierThreshold.HasValue) dataFrame.LowOutlierThreshold = request.LowOutlierThreshold.Value;

            // Populate the exact series with a single reset so the data frame recomputes lambda and
            // plotting positions once, matching the model's own factory methods.
            dataFrame.ExactSeries.SuppressCollectionChanged = true;
            foreach (var observation in request.ExactData)
            {
                int index = observation.Index
                    ?? observation.DateTime?.Year
                    ?? throw new ArgumentException("Each exact observation requires either 'index' (water year) or 'dateTime'.");
                dataFrame.ExactSeries.Add(new ExactData(index, observation.Value, 0d, observation.IsLowOutlier));
            }
            dataFrame.ExactSeries.SuppressCollectionChanged = false;
            dataFrame.ExactSeries.RaiseCollectionChangedReset();

            if (request.UncertainData is { Count: > 0 })
            {
                dataFrame.UncertainSeries.SuppressCollectionChanged = true;
                for (int i = 0; i < request.UncertainData.Count; i++)
                {
                    var observation = request.UncertainData[i];
                    int index = observation.Index
                        ?? observation.DateTime?.Year
                        ?? throw new ArgumentException($"uncertainData[{i}]: each uncertain observation requires either 'index' (water year) or 'dateTime'.");
                    var distribution = DistributionSpecMapper.ToDistribution(observation.Distribution, $"uncertainData[{i}].distribution");
                    dataFrame.UncertainSeries.Add(new UncertainData(index, distribution));
                }
                dataFrame.UncertainSeries.SuppressCollectionChanged = false;
                dataFrame.UncertainSeries.RaiseCollectionChangedReset();
            }

            if (request.IntervalData is { Count: > 0 })
            {
                dataFrame.IntervalSeries.SuppressCollectionChanged = true;
                foreach (var interval in request.IntervalData)
                {
                    double value = interval.Value ?? (interval.LowerBound + interval.UpperBound) / 2d;
                    dataFrame.IntervalSeries.Add(new IntervalData(interval.Index, interval.LowerBound, value, interval.UpperBound));
                }
                dataFrame.IntervalSeries.SuppressCollectionChanged = false;
                dataFrame.IntervalSeries.RaiseCollectionChangedReset();
            }

            if (request.ThresholdData is { Count: > 0 })
            {
                dataFrame.ThresholdSeries.SuppressCollectionChanged = true;
                foreach (var threshold in request.ThresholdData)
                {
                    dataFrame.ThresholdSeries.Add(new ThresholdData(threshold.StartIndex, threshold.EndIndex, threshold.Value)
                    {
                        NumberAbove = threshold.NumberAbove
                    });
                }
                dataFrame.ThresholdSeries.SuppressCollectionChanged = false;
                dataFrame.ThresholdSeries.RaiseCollectionChangedReset();
            }

            // The reset handler recomputes lambda as events/span; an explicit client value (e.g.,
            // for manually entered peaks-over-threshold data) overrides it.
            if (request.Lambda.HasValue) dataFrame.SetLambda(request.Lambda.Value);

            ThrowIfInvalid(dataFrame);

            var resource = new InputDataResource
            {
                Name = string.IsNullOrWhiteSpace(request.Name) ? "Manual input data" : request.Name,
                Description = request.Description,
                DataFrame = dataFrame,
                Method = InputDataMethod.Manual
            };
            return _store.AddInputData(resource);
        }

        /// <inheritdoc/>
        public InputDataResource CreateBlockMax(CreateBlockMaxInputDataRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            var source = _store.GetTimeSeries(request.TimeSeriesId)
                ?? throw new ResourceNotFoundException("time series", request.TimeSeriesId);

            var dataFrame = new DataFrame();
            dataFrame.CreateBlockSeries(source.TimeSeries, request.TimeBlock, request.BlockFunction,
                request.SmoothingFunction, request.StartMonth, request.EndMonth, request.Period);

            if (dataFrame.ExactSeries.Count == 0)
            {
                throw new RequestValidationException(
                    new[] { $"Block-maxima extraction produced no observations from time series '{source.Name}'. The series may be too short to contain a complete {request.TimeBlock} block." });
            }

            ThrowIfInvalid(dataFrame);

            var resource = new InputDataResource
            {
                Name = string.IsNullOrWhiteSpace(request.Name) ? $"Block maxima of {source.Name}" : request.Name,
                Description = request.Description,
                DataFrame = dataFrame,
                Method = InputDataMethod.BlockMaxima,
                SourceTimeSeriesId = source.Id,
                TimeBlock = request.TimeBlock,
                BlockFunction = request.BlockFunction,
                SmoothingFunction = request.SmoothingFunction,
                StartMonth = request.StartMonth,
                EndMonth = request.EndMonth,
                Period = request.Period
            };
            return _store.AddInputData(resource);
        }

        /// <inheritdoc/>
        public InputDataResource CreatePeaksOverThreshold(CreatePotInputDataRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            var source = _store.GetTimeSeries(request.TimeSeriesId)
                ?? throw new ResourceNotFoundException("time series", request.TimeSeriesId);

            var dataFrame = new DataFrame();
            dataFrame.CreatePeaksOverThresholdSeries(source.TimeSeries, request.Threshold,
                request.MinStepsBetweenPeaks, request.SmoothingFunction, request.Period);

            // The model's reset handler recomputes lambda as events / span-of-peak-years; an
            // explicit client value (e.g., events / full record length) overrides it.
            if (request.Lambda.HasValue) dataFrame.SetLambda(request.Lambda.Value);

            if (dataFrame.ExactSeries.Count == 0)
            {
                throw new RequestValidationException(
                    new[] { $"Peaks-over-threshold extraction produced no events from time series '{source.Name}' at threshold {request.Threshold}. Lower the threshold." });
            }

            ThrowIfInvalid(dataFrame);

            var resource = new InputDataResource
            {
                Name = string.IsNullOrWhiteSpace(request.Name) ? $"POT of {source.Name}" : request.Name,
                Description = request.Description,
                DataFrame = dataFrame,
                Method = InputDataMethod.PeaksOverThreshold,
                SourceTimeSeriesId = source.Id,
                Threshold = request.Threshold,
                MinStepsBetweenPeaks = request.MinStepsBetweenPeaks,
                SmoothingFunction = request.SmoothingFunction,
                Period = request.Period
            };
            return _store.AddInputData(resource);
        }

        /// <inheritdoc/>
        public async Task<InputDataResource> CreateFromUsgsPeaksAsync(CreateUsgsPeaksInputDataRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (request.SeriesType != TimeSeriesDownload.TimeSeriesType.PeakDischarge &&
                request.SeriesType != TimeSeriesDownload.TimeSeriesType.PeakStage)
            {
                throw new ArgumentException(
                    "The series type must be peakDischarge or peakStage. For daily records, create a time series (POST api/timeseries/usgs) and extract block maxima (POST api/inputdata/block-max).",
                    nameof(request));
            }

            // Download through the adapter seam (rather than DataFrame.CreateFromUSGS, which calls
            // the static downloader directly) so tests can fake the network; the series population
            // below mirrors DataFrame.CreateFromUSGS exactly.
            var (timeSeries, _) = await _usgs.DownloadAsync(request.SiteNumber, request.SeriesType, cancellationToken);

            var dataFrame = new DataFrame();
            dataFrame.ExactSeries.SuppressCollectionChanged = true;
            for (int i = 0; i < timeSeries.Count; i++)
            {
                dataFrame.ExactSeries.Add(new ExactData(timeSeries[i].Index, timeSeries[i].Value));
            }
            dataFrame.SetLambda(1);
            dataFrame.ExactSeries.SuppressCollectionChanged = false;
            dataFrame.ExactSeries.RaiseCollectionChangedReset();

            ThrowIfInvalid(dataFrame);

            var resource = new InputDataResource
            {
                Name = string.IsNullOrWhiteSpace(request.Name) ? $"USGS {request.SiteNumber} peaks" : request.Name,
                Description = request.Description,
                DataFrame = dataFrame,
                Method = request.SeriesType == TimeSeriesDownload.TimeSeriesType.PeakStage
                    ? InputDataMethod.UsgsPeakStage
                    : InputDataMethod.UsgsPeakDischarge,
                UsgsSiteNumber = request.SiteNumber
            };
            return _store.AddInputData(resource);
        }

        /// <inheritdoc/>
        public IReadOnlyList<InputDataResource> List()
        {
            return _store.ListInputData();
        }

        /// <inheritdoc/>
        public InputDataResource Get(Guid id)
        {
            return _store.GetInputData(id) ?? throw new ResourceNotFoundException("input data", id);
        }

        /// <inheritdoc/>
        public Dictionary<string, double> GetSummaryStatistics(Guid id)
        {
            return Get(id).DataFrame.SummaryStatisticsAllData();
        }

        /// <inheritdoc/>
        public void Delete(Guid id)
        {
            if (!_store.DeleteInputData(id))
            {
                throw new ResourceNotFoundException("input data", id);
            }
        }

        /// <summary>
        /// Runs model-layer validation on a freshly built data frame and converts a failure into
        /// the API's validation exception (HTTP 400 with the individual messages).
        /// </summary>
        /// <param name="dataFrame">The data frame to validate.</param>
        /// <exception cref="RequestValidationException">Thrown when validation reports errors.</exception>
        private static void ThrowIfInvalid(DataFrame dataFrame)
        {
            var (isValid, messages) = dataFrame.Validate();
            if (!isValid)
            {
                throw new RequestValidationException(messages);
            }
        }
    }
}
