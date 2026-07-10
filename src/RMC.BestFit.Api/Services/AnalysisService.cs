using System.Diagnostics;
using Microsoft.Extensions.Options;
using Numerics.Data;
using Numerics.Distributions;
using RMC.BestFit.Analyses;
using RMC.BestFit.Api.Configuration;
using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Helpers;
using RMC.BestFit.Api.Mappers;
using RMC.BestFit.Api.Services.Exceptions;
using RMC.BestFit.Api.Store;
using RMC.BestFit.Models;

namespace RMC.BestFit.Api.Services
{
    /// <summary>
    /// Default <see cref="IAnalysisService"/> implementation. Analyses clone their inputs at
    /// creation (data frames via XElement round-trip, time series via ordinate copy) so deleting
    /// or replacing upstream resources can never corrupt an existing fit. Runs are serialized per
    /// analysis (single-entry lock → HTTP 409 on conflict) and throttled globally
    /// (<see cref="ApiOptions.MaxConcurrentRuns"/>) because each MCMC run parallelizes internally.
    /// </summary>
    public partial class AnalysisService : IAnalysisService
    {
        /// <summary>
        /// The in-memory resource store.
        /// </summary>
        private readonly IResourceStore _store;

        /// <summary>
        /// The configured API limits (iteration cap, run throttle).
        /// </summary>
        private readonly ApiOptions _options;

        /// <summary>
        /// Global throttle bounding concurrent estimation runs across all analyses.
        /// </summary>
        private readonly SemaphoreSlim _runThrottle;

        /// <summary>
        /// Constructs the service.
        /// </summary>
        /// <param name="store">The in-memory resource store.</param>
        /// <param name="options">The configured API limits.</param>
        /// <exception cref="ArgumentNullException">Thrown when a dependency is null.</exception>
        public AnalysisService(IResourceStore store, IOptions<ApiOptions> options)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            if (options is null) throw new ArgumentNullException(nameof(options));
            _options = options.Value;
            _runThrottle = new SemaphoreSlim(Math.Max(1, _options.MaxConcurrentRuns), Math.Max(1, _options.MaxConcurrentRuns));
        }

        /// <inheritdoc/>
        public AnalysisResource CreateUnivariate(CreateUnivariateAnalysisRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            var input = _store.GetInputData(request.InputDataId)
                ?? throw new ResourceNotFoundException("input data", request.InputDataId);

            if (!UnivariateDistribution.IsSupportedDistributionType(request.Distribution))
            {
                throw new ArgumentException(
                    $"Distribution '{request.Distribution}' is not supported by the univariate analysis. See GET api/metadata/distributions.");
            }

            var distribution = new UnivariateDistribution(input.DataFrame.Clone(), request.Distribution);
            var analysis = new UnivariateAnalysis(distribution);
            ApplyProbabilityOrdinates(request.ProbabilityOrdinates, analysis.ProbabilityOrdinates);
            BayesianOptionsMapper.Apply(analysis.BayesianAnalysis, request.BayesianOptions, _options.MaxIterations);
            // Priors are applied last: the model is fully configured, so nothing after this point
            // triggers the default-parameter rebuild that would wipe them.
            PriorMapper.ApplyParameterPriors(distribution, request.ParameterPriors);
            PriorMapper.ApplyQuantilePriors(distribution, request.QuantilePriors, request.UseSingleQuantile);

            var resource = new AnalysisResource
            {
                Name = string.IsNullOrWhiteSpace(request.Name)
                    ? $"{MetadataMapper.ToDisplayName(request.Distribution)} analysis of {input.Name}"
                    : request.Name,
                Description = request.Description,
                Kind = AnalysisKind.Univariate,
                Univariate = analysis,
                InputDataId = input.Id
            };
            return _store.AddAnalysis(resource);
        }

        /// <inheritdoc/>
        public AnalysisResource CreateBulletin17C(CreateBulletin17CAnalysisRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            var input = _store.GetInputData(request.InputDataId)
                ?? throw new ResourceNotFoundException("input data", request.InputDataId);

            if (!Bulletin17CDistribution.IsSupportedDistributionType(request.Distribution))
            {
                throw new ArgumentException(
                    $"Distribution '{request.Distribution}' is not supported by the Bulletin 17C analysis. " +
                    "Supported: logPearsonTypeIII, pearsonTypeIII, logNormal, normal, gammaDistribution, exponential.");
            }

            var distribution = new Bulletin17CDistribution(input.DataFrame.Clone(), request.Distribution);
            var analysis = new Bulletin17CAnalysis(distribution);
            if (request.UncertaintyMethod.HasValue)
            {
                analysis.UncertaintyMethod = request.UncertaintyMethod.Value;
            }
            ApplyProbabilityOrdinates(request.ProbabilityOrdinates, analysis.ProbabilityOrdinates);
            PriorMapper.ApplyPenalties(distribution, request.ParameterPenalties, request.QuantilePenalties);

            // The Expected Moments Algorithm has no measurement-error likelihood, so uncertain
            // observations are silently skipped by the model. Never silent here: record a warning
            // that survives on the resource for create/get/validate responses.
            var warnings = new List<string>();
            if (input.DataFrame.UncertainSeries.Count > 0)
            {
                warnings.Add(
                    $"The input data contains {input.DataFrame.UncertainSeries.Count} uncertain observation(s). " +
                    "The Bulletin 17C Expected Moments Algorithm does not use uncertain data, so these observations " +
                    "are ignored by this analysis. Use the univariate analysis (POST api/analyses/univariate) for " +
                    "full measurement-error propagation.");
            }

            var resource = new AnalysisResource
            {
                Name = string.IsNullOrWhiteSpace(request.Name) ? $"Bulletin 17C analysis of {input.Name}" : request.Name,
                Description = request.Description,
                Kind = AnalysisKind.Bulletin17C,
                Bulletin17C = analysis,
                InputDataId = input.Id,
                CreationWarnings = warnings
            };
            return _store.AddAnalysis(resource);
        }

        /// <inheritdoc/>
        public AnalysisResource CreateRatingCurve(CreateRatingCurveAnalysisRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            var stage = _store.GetTimeSeries(request.StageTimeSeriesId)
                ?? throw new ResourceNotFoundException("stage time series", request.StageTimeSeriesId);
            var discharge = _store.GetTimeSeries(request.DischargeTimeSeriesId)
                ?? throw new ResourceNotFoundException("discharge time series", request.DischargeTimeSeriesId);

            if (request.MinStage.HasValue != request.MaxStage.HasValue)
            {
                throw new ArgumentException("minStage and maxStage must be supplied together to customize the stage grid.");
            }
            if (request.MinStage.HasValue && request.MinStage.Value >= request.MaxStage!.Value)
            {
                throw new ArgumentException("minStage must be less than maxStage.");
            }

            var ratingCurve = new RatingCurve(CloneSeries(stage.TimeSeries), CloneSeries(discharge.TimeSeries), request.NumberOfSegments);
            var analysis = new RatingCurveAnalysis(ratingCurve);

            if (request.MinStage.HasValue)
            {
                analysis.UseDefaultStageBins = false;
                analysis.MinStage = request.MinStage.Value;
                analysis.MaxStage = request.MaxStage!.Value;
            }
            if (request.StageBins.HasValue)
            {
                analysis.StageBins = request.StageBins.Value;
            }
            BayesianOptionsMapper.Apply(analysis.BayesianAnalysis, request.BayesianOptions, _options.MaxIterations);
            // Priors are applied last so nothing after this point can rebuild the parameter list.
            PriorMapper.ApplyParameterPriors(ratingCurve, request.ParameterPriors);

            var resource = new AnalysisResource
            {
                Name = string.IsNullOrWhiteSpace(request.Name) ? $"Rating curve: {stage.Name} / {discharge.Name}" : request.Name,
                Description = request.Description,
                Kind = AnalysisKind.RatingCurve,
                RatingCurve = analysis,
                StageTimeSeriesId = stage.Id,
                DischargeTimeSeriesId = discharge.Id
            };
            return _store.AddAnalysis(resource);
        }

        /// <inheritdoc/>
        public IReadOnlyList<AnalysisResource> List(AnalysisKind? kind = null)
        {
            var analyses = _store.ListAnalyses();
            return kind == null ? analyses : analyses.Where(a => a.Kind == kind.Value).ToList();
        }

        /// <inheritdoc/>
        public AnalysisResource Get(Guid id, AnalysisKind? expectedKind = null)
        {
            var resource = _store.GetAnalysis(id);
            if (resource == null || (expectedKind.HasValue && resource.Kind != expectedKind.Value))
            {
                throw new ResourceNotFoundException(DescribeKind(expectedKind), id);
            }
            return resource;
        }

        /// <inheritdoc/>
        public ValidationResponse Validate(Guid id, AnalysisKind? expectedKind = null)
        {
            var resource = Get(id, expectedKind);
            var (isValid, messages) = AnalysisRunHelper.Validate(resource);
            return new ValidationResponse
            {
                IsValid = isValid,
                Errors = isValid ? new List<string>() : messages,
                Warnings = resource.CreationWarnings.ToList()
            };
        }

        /// <summary>
        /// Kind-specific state synchronization immediately before a run. A coincident frequency
        /// analysis pulls the marginal posterior chains from its LIVE upstream resources here (the
        /// UI does the same before each run): a chain is available only when the marginal is a
        /// plain univariate analysis that has been estimated; otherwise uncertainty comes from the
        /// copula chain alone.
        /// </summary>
        /// <param name="resource">The analysis resource about to run.</param>
        private static void PrepareForRun(AnalysisResource resource)
        {
            if (resource.Kind != AnalysisKind.CoincidentFrequency)
            {
                return;
            }
            var analysis = resource.CoincidentFrequency!;
            var bivariate = resource.BivariateResource;
            analysis.MarginalXChain = GetMarginalChain(bivariate?.MarginalXResource);
            analysis.MarginalYChain = GetMarginalChain(bivariate?.MarginalYResource);
        }

        /// <summary>
        /// Returns a marginal resource's posterior chain when it is an estimated plain univariate
        /// analysis; otherwise null.
        /// </summary>
        /// <param name="marginal">The marginal analysis resource, or null.</param>
        /// <returns>The MCMC results, or null.</returns>
        private static Numerics.Sampling.MCMC.MCMCResults? GetMarginalChain(AnalysisResource? marginal)
        {
            if (marginal is { Kind: AnalysisKind.Univariate } && marginal.Analysis.IsEstimated)
            {
                return marginal.Univariate!.BayesianAnalysis.Results;
            }
            return null;
        }

        /// <inheritdoc/>
        public async Task<FrequencyResultsResponse> RunFrequencyAsync(Guid id, AnalysisKind? expectedKind, CancellationToken cancellationToken = default)
        {
            var resource = Get(id, expectedKind);
            if (!IsFrequencyKind(resource.Kind))
            {
                throw new ResourceNotFoundException(DescribeKind(expectedKind), id);
            }
            await RunCoreAsync(resource, cancellationToken);
            return ResultsMapper.ToFrequencyResults(resource);
        }

        /// <inheritdoc/>
        public async Task<RatingCurveResultsResponse> RunRatingCurveAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var resource = Get(id, AnalysisKind.RatingCurve);
            await RunCoreAsync(resource, cancellationToken);
            return ResultsMapper.ToRatingCurveResults(resource);
        }

        /// <inheritdoc/>
        public FrequencyResultsResponse GetFrequencyResults(Guid id, AnalysisKind? expectedKind = null)
        {
            var resource = Get(id, expectedKind);
            if (!IsFrequencyKind(resource.Kind) || !resource.Analysis.IsEstimated)
            {
                throw new ResourceNotFoundException(
                    $"The analysis '{id}' has no frequency results. Run it first (POST .../{id}/run).");
            }
            return ResultsMapper.ToFrequencyResults(resource);
        }

        /// <inheritdoc/>
        public RatingCurveResultsResponse GetRatingCurveResults(Guid id)
        {
            var resource = Get(id, AnalysisKind.RatingCurve);
            if (!resource.Analysis.IsEstimated)
            {
                throw new ResourceNotFoundException(
                    $"The analysis '{id}' has no results. Run it first (POST .../{id}/run).");
            }
            return ResultsMapper.ToRatingCurveResults(resource);
        }

        /// <inheritdoc/>
        public async Task<DistributionFittingResultsResponse> RunDistributionFittingAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var resource = Get(id, AnalysisKind.DistributionFitting);
            await RunCoreAsync(resource, cancellationToken);
            return DistributionFittingResultsMapper.ToResults(resource);
        }

        /// <inheritdoc/>
        public DistributionFittingResultsResponse GetDistributionFittingResults(Guid id)
        {
            var resource = Get(id, AnalysisKind.DistributionFitting);
            if (!resource.Analysis.IsEstimated)
            {
                throw new ResourceNotFoundException(
                    $"The analysis '{id}' has no results. Run it first (POST .../{id}/run).");
            }
            return DistributionFittingResultsMapper.ToResults(resource);
        }

        /// <inheritdoc/>
        public async Task<BivariateResultsResponse> RunBivariateAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var resource = Get(id, AnalysisKind.Bivariate);
            await RunCoreAsync(resource, cancellationToken);
            return BivariateResultsMapper.ToBivariateResults(resource);
        }

        /// <inheritdoc/>
        public BivariateResultsResponse GetBivariateResults(Guid id)
        {
            var resource = Get(id, AnalysisKind.Bivariate);
            if (!resource.Analysis.IsEstimated)
            {
                throw new ResourceNotFoundException(
                    $"The analysis '{id}' has no results. Run it first (POST .../{id}/run).");
            }
            return BivariateResultsMapper.ToBivariateResults(resource);
        }

        /// <inheritdoc/>
        public async Task<CoincidentFrequencyResultsResponse> RunCoincidentFrequencyAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var resource = Get(id, AnalysisKind.CoincidentFrequency);
            await RunCoreAsync(resource, cancellationToken);
            return BivariateResultsMapper.ToCoincidentFrequencyResults(resource);
        }

        /// <inheritdoc/>
        public CoincidentFrequencyResultsResponse GetCoincidentFrequencyResults(Guid id)
        {
            var resource = Get(id, AnalysisKind.CoincidentFrequency);
            if (!resource.Analysis.IsEstimated)
            {
                throw new ResourceNotFoundException(
                    $"The analysis '{id}' has no results. Run it first (POST .../{id}/run).");
            }
            return BivariateResultsMapper.ToCoincidentFrequencyResults(resource);
        }

        /// <inheritdoc/>
        public async Task<TimeSeriesResultsResponse> RunTimeSeriesAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var resource = Get(id, AnalysisKind.TimeSeries);
            await RunCoreAsync(resource, cancellationToken);
            return TimeSeriesResultsMapper.ToResults(resource);
        }

        /// <inheritdoc/>
        public TimeSeriesResultsResponse GetTimeSeriesResults(Guid id)
        {
            var resource = Get(id, AnalysisKind.TimeSeries);
            if (!resource.Analysis.IsEstimated)
            {
                throw new ResourceNotFoundException(
                    $"The analysis '{id}' has no results. Run it first (POST .../{id}/run).");
            }
            return TimeSeriesResultsMapper.ToResults(resource);
        }

        /// <inheritdoc/>
        public void Delete(Guid id, AnalysisKind? expectedKind = null)
        {
            var resource = Get(id, expectedKind);
            if (!resource.RunLock.Wait(0))
            {
                throw new ResourceConflictException(
                    $"The analysis '{id}' is currently running and cannot be deleted. Cancel the run (disconnect) or wait for it to finish.");
            }
            try
            {
                _store.DeleteAnalysis(id);
            }
            finally
            {
                resource.RunLock.Release();
            }
        }

        /// <summary>
        /// Shared run orchestration: per-analysis single-entry lock (409 on conflict), global run
        /// throttle, pre-run model validation (400), state bookkeeping, and outcome normalization
        /// via <see cref="AnalysisRunHelper.ExecuteAsync"/>.
        /// </summary>
        /// <param name="resource">The analysis resource to run.</param>
        /// <param name="cancellationToken">Client cancellation.</param>
        /// <returns>A task completing when the run has succeeded.</returns>
        /// <exception cref="ResourceConflictException">Thrown when the analysis is already running.</exception>
        /// <exception cref="RequestValidationException">Thrown when the configuration fails validation.</exception>
        private async Task RunCoreAsync(AnalysisResource resource, CancellationToken cancellationToken)
        {
            if (!resource.RunLock.Wait(0))
            {
                throw new ResourceConflictException(
                    $"The analysis '{resource.Id}' is already running. Wait for it to finish before starting another run.");
            }
            List<AnalysisResource>? componentLocks = null;
            try
            {
                // Composites read their components' live posteriors during the run; excluding
                // component runs for the duration keeps that read consistent. Acquired before
                // validation so a busy component reports 409 deterministically rather than racing
                // the children-estimated validation check.
                componentLocks = AcquireComponentLocks(resource);

                await _runThrottle.WaitAsync(cancellationToken);
                try
                {
                    var (isValid, messages) = AnalysisRunHelper.Validate(resource);
                    if (!isValid)
                    {
                        throw new RequestValidationException(messages, "The analysis configuration is invalid. See validationErrors.");
                    }
                    PrepareForRun(resource);

                    resource.State = AnalysisRunState.Running;
                    resource.LastError = null;
                    var stopwatch = Stopwatch.StartNew();
                    try
                    {
                        await AnalysisRunHelper.ExecuteAsync(resource, cancellationToken);
                        resource.State = AnalysisRunState.Succeeded;
                    }
                    catch (OperationCanceledException)
                    {
                        resource.State = AnalysisRunState.Cancelled;
                        resource.LastError = "The run was cancelled by the client.";
                        throw;
                    }
                    catch (Exception ex)
                    {
                        resource.State = AnalysisRunState.Failed;
                        resource.LastError = ex.Message;
                        throw;
                    }
                    finally
                    {
                        stopwatch.Stop();
                        resource.LastRunUtc = DateTime.UtcNow;
                        resource.LastRunMs = stopwatch.ElapsedMilliseconds;
                    }
                }
                finally
                {
                    _runThrottle.Release();
                }
            }
            finally
            {
                if (componentLocks != null)
                {
                    foreach (var component in componentLocks)
                    {
                        component.RunLock.Release();
                    }
                }
                resource.RunLock.Release();
            }
        }

        /// <summary>
        /// Try-acquires the run lock of every component resource the analysis consumes, so
        /// component posteriors cannot change mid-run. Rolls back already-acquired locks and
        /// reports a conflict when any component is busy.
        /// </summary>
        /// <param name="resource">The analysis resource about to run.</param>
        /// <returns>The components whose locks were acquired (empty for component-free kinds).</returns>
        /// <exception cref="ResourceConflictException">Thrown when a component analysis is currently running.</exception>
        private static List<AnalysisResource> AcquireComponentLocks(AnalysisResource resource)
        {
            var acquired = new List<AnalysisResource>();
            foreach (var component in GetComponentResources(resource))
            {
                if (!component.RunLock.Wait(0))
                {
                    foreach (var held in acquired)
                    {
                        held.RunLock.Release();
                    }
                    throw new ResourceConflictException(
                        $"The component analysis '{component.Id}' ('{component.Name}') is currently running. " +
                        "Wait for it to finish before running this analysis.");
                }
                acquired.Add(component);
            }
            return acquired;
        }

        /// <summary>
        /// Lists the LIVE component resources an analysis reads while it runs, de-duplicated by
        /// id (a component listed twice must not conflict with itself).
        /// </summary>
        /// <param name="resource">The analysis resource.</param>
        /// <returns>The distinct component resources; empty for kinds without components.</returns>
        private static IReadOnlyList<AnalysisResource> GetComponentResources(AnalysisResource resource)
        {
            if (resource.ComponentResources == null || resource.ComponentResources.Count == 0)
            {
                return Array.Empty<AnalysisResource>();
            }
            return resource.ComponentResources
                .GroupBy(r => r.Id)
                .Select(g => g.First())
                .ToList();
        }

        /// <summary>
        /// Validates client-supplied exceedance-probability ordinates and replaces the analysis's
        /// default ordinates in place (the property is read-only on some analyses). Values are
        /// de-duplicated and sorted ascending because the model requires strictly increasing
        /// ordinates; clients should not need to know that convention. No-op when the client did
        /// not supply ordinates (model defaults apply).
        /// </summary>
        /// <param name="ordinates">The client-supplied ordinates, or null.</param>
        /// <param name="target">The analysis's ordinate collection to replace.</param>
        /// <exception cref="ArgumentException">Thrown when any ordinate is outside (0, 1).</exception>
        private static void ApplyProbabilityOrdinates(List<double>? ordinates, ProbabilityOrdinates target)
        {
            if (ordinates == null || ordinates.Count == 0) return;
            if (ordinates.Any(p => p <= 0d || p >= 1d || double.IsNaN(p)))
            {
                throw new ArgumentException("Each probability ordinate must be strictly between 0 and 1 (annual exceedance probability).");
            }
            target.Clear();
            target.AddRange(ordinates.Distinct().OrderBy(p => p));
        }

        /// <summary>
        /// Copies a time series ordinate-by-ordinate so the analysis owns data independent of the
        /// source resource.
        /// </summary>
        /// <param name="source">The series to copy.</param>
        /// <returns>An independent copy with the same interval and ordinates.</returns>
        private static TimeSeries CloneSeries(TimeSeries source)
        {
            var clone = new TimeSeries(source.TimeInterval);
            foreach (var ordinate in source)
            {
                clone.Add(new SeriesOrdinate<DateTime, double>(ordinate.Index, ordinate.Value));
            }
            return clone;
        }

        /// <summary>
        /// Determines whether a kind produces frequency results (AEP-aligned curves with
        /// <c>[p, 2]</c> confidence intervals served by the shared frequency run/results paths).
        /// </summary>
        /// <param name="kind">The analysis kind.</param>
        /// <returns>True for the frequency-curve kinds.</returns>
        private static bool IsFrequencyKind(AnalysisKind kind)
        {
            return kind is AnalysisKind.Univariate or AnalysisKind.Bulletin17C or AnalysisKind.Mixture
                or AnalysisKind.PointProcess or AnalysisKind.CompetingRisks or AnalysisKind.Composite;
        }

        /// <summary>
        /// Builds the user-facing resource-type phrase for not-found messages.
        /// </summary>
        /// <param name="kind">The expected kind, or null for any analysis.</param>
        /// <returns>The phrase (e.g., "univariate analysis").</returns>
        private static string DescribeKind(AnalysisKind? kind)
        {
            return kind switch
            {
                AnalysisKind.Univariate => "univariate analysis",
                AnalysisKind.Bulletin17C => "Bulletin 17C analysis",
                AnalysisKind.RatingCurve => "rating curve analysis",
                AnalysisKind.Mixture => "mixture analysis",
                AnalysisKind.PointProcess => "point process analysis",
                AnalysisKind.CompetingRisks => "competing risks analysis",
                AnalysisKind.Composite => "composite analysis",
                AnalysisKind.DistributionFitting => "distribution fitting analysis",
                AnalysisKind.Bivariate => "bivariate analysis",
                AnalysisKind.CoincidentFrequency => "coincident frequency analysis",
                AnalysisKind.TimeSeries => "time series analysis",
                _ => "analysis"
            };
        }
    }
}
