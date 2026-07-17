using Numerics.Data;
using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Services.Exceptions;
using RMC.BestFit.Api.Store;

namespace RMC.BestFit.Api.Services
{
    /// <summary>
    /// Default <see cref="IWorkflowService"/> implementation. Each workflow runs the standard
    /// resource pipeline through the shared services; when a step throws, the exception is folded
    /// into the response (success=false, failedStep, errorMessage, validation errors) while the
    /// ids of already-created resources are preserved. Client cancellation is the only exception
    /// allowed to propagate (the controller maps it to HTTP 499).
    /// </summary>
    public class WorkflowService : IWorkflowService
    {
        /// <summary>
        /// The time-series service (USGS downloads).
        /// </summary>
        private readonly ITimeSeriesService _timeSeries;

        /// <summary>
        /// The input-data service (peaks and block maxima).
        /// </summary>
        private readonly IInputDataService _inputData;

        /// <summary>
        /// The analysis service (create + run).
        /// </summary>
        private readonly IAnalysisService _analyses;

        /// <summary>
        /// Constructs the service.
        /// </summary>
        /// <param name="timeSeries">The time-series service.</param>
        /// <param name="inputData">The input-data service.</param>
        /// <param name="analyses">The analysis service.</param>
        /// <exception cref="ArgumentNullException">Thrown when a dependency is null.</exception>
        public WorkflowService(ITimeSeriesService timeSeries, IInputDataService inputData, IAnalysisService analyses)
        {
            _timeSeries = timeSeries ?? throw new ArgumentNullException(nameof(timeSeries));
            _inputData = inputData ?? throw new ArgumentNullException(nameof(inputData));
            _analyses = analyses ?? throw new ArgumentNullException(nameof(analyses));
        }

        /// <inheritdoc/>
        public async Task<UsgsFrequencyWorkflowResponse> RunUsgsPeakFrequencyAsync(UsgsPeakFrequencyWorkflowRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            var response = new UsgsFrequencyWorkflowResponse();

            // Step 1: download the annual peaks directly into an input-data resource.
            if (!await TryStepAsync(response, "createInputData", async () =>
            {
                var inputData = await _inputData.CreateFromUsgsPeaksAsync(new CreateUsgsPeaksInputDataRequest
                {
                    SiteNumber = request.SiteNumber,
                    SeriesType = request.SeriesType,
                    Name = request.Name == null ? null : $"{request.Name} - input data"
                }, cancellationToken);
                response.InputDataId = inputData.Id;
            })) return response;

            // Step 2: create the analysis over a clone of the input data.
            if (!TryStep(response, "createAnalysis", () =>
            {
                var analysis = _analyses.CreateUnivariate(new CreateUnivariateAnalysisRequest
                {
                    InputDataId = response.InputDataId!.Value,
                    Distribution = request.Distribution,
                    ProbabilityOrdinates = request.ProbabilityOrdinates,
                    BayesianOptions = request.BayesianOptions,
                    Name = request.Name
                });
                response.AnalysisId = analysis.Id;
            })) return response;

            // Step 3: run and attach the results.
            await TryStepAsync(response, "runAnalysis", async () =>
            {
                response.Results = await _analyses.RunFrequencyAsync(response.AnalysisId!.Value, AnalysisKind.Univariate, cancellationToken);
            });
            return response;
        }

        /// <inheritdoc/>
        public async Task<UsgsFrequencyWorkflowResponse> RunUsgsBlockMaxFrequencyAsync(UsgsBlockMaxFrequencyWorkflowRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            var response = new UsgsFrequencyWorkflowResponse();

            // Step 1: download the daily record as a time-series resource.
            if (!await TryStepAsync(response, "downloadTimeSeries", async () =>
            {
                var timeSeries = await _timeSeries.CreateFromUsgsAsync(new CreateUsgsTimeSeriesRequest
                {
                    SiteNumber = request.SiteNumber,
                    SeriesType = request.SeriesType,
                    Name = request.Name == null ? null : $"{request.Name} - daily series"
                }, cancellationToken);
                response.TimeSeriesId = timeSeries.Id;
            })) return response;

            // Step 2: extract block maxima into an input-data resource.
            if (!TryStep(response, "createInputData", () =>
            {
                var inputData = _inputData.CreateBlockMax(new CreateBlockMaxInputDataRequest
                {
                    TimeSeriesId = response.TimeSeriesId!.Value,
                    TimeBlock = request.TimeBlock,
                    BlockFunction = request.BlockFunction,
                    SmoothingFunction = request.SmoothingFunction,
                    StartMonth = request.StartMonth,
                    EndMonth = request.EndMonth,
                    Period = request.Period,
                    Name = request.Name == null ? null : $"{request.Name} - block maxima"
                });
                response.InputDataId = inputData.Id;
            })) return response;

            // Step 3: create the analysis.
            if (!TryStep(response, "createAnalysis", () =>
            {
                var analysis = _analyses.CreateUnivariate(new CreateUnivariateAnalysisRequest
                {
                    InputDataId = response.InputDataId!.Value,
                    Distribution = request.Distribution,
                    ProbabilityOrdinates = request.ProbabilityOrdinates,
                    BayesianOptions = request.BayesianOptions,
                    Name = request.Name
                });
                response.AnalysisId = analysis.Id;
            })) return response;

            // Step 4: run and attach the results.
            await TryStepAsync(response, "runAnalysis", async () =>
            {
                response.Results = await _analyses.RunFrequencyAsync(response.AnalysisId!.Value, AnalysisKind.Univariate, cancellationToken);
            });
            return response;
        }

        /// <inheritdoc/>
        public async Task<UsgsFrequencyWorkflowResponse> RunUsgsBulletin17CAsync(UsgsBulletin17CWorkflowRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            var response = new UsgsFrequencyWorkflowResponse();

            // Step 1: download the annual peaks directly into an input-data resource.
            if (!await TryStepAsync(response, "createInputData", async () =>
            {
                var inputData = await _inputData.CreateFromUsgsPeaksAsync(new CreateUsgsPeaksInputDataRequest
                {
                    SiteNumber = request.SiteNumber,
                    SeriesType = request.SeriesType,
                    Name = request.Name == null ? null : $"{request.Name} - input data"
                }, cancellationToken);
                response.InputDataId = inputData.Id;
            })) return response;

            // Step 2: create the Bulletin 17C analysis.
            if (!TryStep(response, "createAnalysis", () =>
            {
                var analysis = _analyses.CreateBulletin17C(new CreateBulletin17CAnalysisRequest
                {
                    InputDataId = response.InputDataId!.Value,
                    Distribution = request.Distribution,
                    UncertaintyMethod = request.UncertaintyMethod,
                    ProbabilityOrdinates = request.ProbabilityOrdinates,
                    Name = request.Name
                });
                response.AnalysisId = analysis.Id;
            })) return response;

            // Step 3: run and attach the results.
            await TryStepAsync(response, "runAnalysis", async () =>
            {
                response.Results = await _analyses.RunFrequencyAsync(response.AnalysisId!.Value, AnalysisKind.Bulletin17C, cancellationToken);
            });
            return response;
        }

        /// <inheritdoc/>
        public async Task<UsgsRatingCurveWorkflowResponse> RunUsgsRatingCurveAsync(UsgsRatingCurveWorkflowRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            var response = new UsgsRatingCurveWorkflowResponse();

            // Step 1: download the discrete stage measurements.
            if (!await TryStepAsync(response, "downloadStage", async () =>
            {
                var stage = await _timeSeries.CreateFromUsgsAsync(new CreateUsgsTimeSeriesRequest
                {
                    SiteNumber = request.SiteNumber,
                    SeriesType = TimeSeriesDownload.TimeSeriesType.MeasuredStage,
                    Name = request.Name == null ? null : $"{request.Name} - measured stage"
                }, cancellationToken);
                response.StageTimeSeriesId = stage.Id;
            })) return response;

            // Step 2: download the discrete discharge measurements.
            if (!await TryStepAsync(response, "downloadDischarge", async () =>
            {
                var discharge = await _timeSeries.CreateFromUsgsAsync(new CreateUsgsTimeSeriesRequest
                {
                    SiteNumber = request.SiteNumber,
                    SeriesType = TimeSeriesDownload.TimeSeriesType.MeasuredDischarge,
                    Name = request.Name == null ? null : $"{request.Name} - measured discharge"
                }, cancellationToken);
                response.DischargeTimeSeriesId = discharge.Id;
            })) return response;

            // Step 3: create the rating curve analysis over the aligned pairs.
            if (!TryStep(response, "createAnalysis", () =>
            {
                var analysis = _analyses.CreateRatingCurve(new CreateRatingCurveAnalysisRequest
                {
                    StageTimeSeriesId = response.StageTimeSeriesId!.Value,
                    DischargeTimeSeriesId = response.DischargeTimeSeriesId!.Value,
                    NumberOfSegments = request.NumberOfSegments,
                    MinStage = request.MinStage,
                    MaxStage = request.MaxStage,
                    StageBins = request.StageBins,
                    BayesianOptions = request.BayesianOptions,
                    Name = request.Name
                });
                response.AnalysisId = analysis.Id;
            })) return response;

            // Step 4: run and attach the results.
            await TryStepAsync(response, "runAnalysis", async () =>
            {
                response.Results = await _analyses.RunRatingCurveAsync(response.AnalysisId!.Value, cancellationToken);
            });
            return response;
        }

        /// <summary>
        /// Runs one asynchronous workflow step, folding any failure into the response and
        /// reporting whether the workflow should continue. Cancellation propagates.
        /// </summary>
        /// <param name="response">The workflow response accumulating ids and failure state.</param>
        /// <param name="stepName">The step name recorded on failure.</param>
        /// <param name="step">The step body.</param>
        /// <returns>True to continue with the next step; false when the step failed.</returns>
        private static async Task<bool> TryStepAsync(ResponseBase response, string stepName, Func<Task> step)
        {
            try
            {
                await step();
                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                RecordFailure(response, stepName, ex);
                return false;
            }
        }

        /// <summary>
        /// Runs one synchronous workflow step, folding any failure into the response and
        /// reporting whether the workflow should continue.
        /// </summary>
        /// <param name="response">The workflow response accumulating ids and failure state.</param>
        /// <param name="stepName">The step name recorded on failure.</param>
        /// <param name="step">The step body.</param>
        /// <returns>True to continue with the next step; false when the step failed.</returns>
        private static bool TryStep(ResponseBase response, string stepName, Action step)
        {
            try
            {
                step();
                return true;
            }
            catch (Exception ex)
            {
                RecordFailure(response, stepName, ex);
                return false;
            }
        }

        /// <summary>
        /// Records a step failure on the response: success flag, failed step, error message, and
        /// validation errors when the failure carries them.
        /// </summary>
        /// <param name="response">The workflow response.</param>
        /// <param name="stepName">The failed step name.</param>
        /// <param name="exception">The failure.</param>
        private static void RecordFailure(ResponseBase response, string stepName, Exception exception)
        {
            response.Success = false;
            response.ErrorMessage = $"Workflow step '{stepName}' failed: {exception.Message}";
            if (exception is RequestValidationException validation)
            {
                response.ValidationErrors = validation.Errors.ToList();
            }
            switch (response)
            {
                case UsgsFrequencyWorkflowResponse frequency:
                    frequency.FailedStep = stepName;
                    break;
                case UsgsRatingCurveWorkflowResponse rating:
                    rating.FailedStep = stepName;
                    break;
                default:
                    break;
            }
        }
    }
}
