using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Store;

namespace RMC.BestFit.Api.Services
{
    /// <summary>
    /// Creates, validates, runs, and deletes analysis resources (univariate, Bulletin 17C, and
    /// rating curve). Shared by the REST controllers and the MCP tools; run methods are
    /// synchronous (they return when the estimation completes).
    /// </summary>
    public interface IAnalysisService
    {
        /// <summary>
        /// Creates a Bayesian MCMC univariate frequency analysis over a clone of the referenced
        /// input data.
        /// </summary>
        /// <param name="request">The creation request.</param>
        /// <returns>The created resource.</returns>
        /// <exception cref="Exceptions.ResourceNotFoundException">Thrown when the input-data resource does not exist.</exception>
        /// <exception cref="ArgumentException">Thrown when the distribution or options are invalid.</exception>
        AnalysisResource CreateUnivariate(CreateUnivariateAnalysisRequest request);

        /// <summary>
        /// Creates a Bulletin 17C analysis over a clone of the referenced input data.
        /// </summary>
        /// <param name="request">The creation request.</param>
        /// <returns>The created resource.</returns>
        /// <exception cref="Exceptions.ResourceNotFoundException">Thrown when the input-data resource does not exist.</exception>
        /// <exception cref="ArgumentException">Thrown when the distribution is not supported by Bulletin 17C.</exception>
        AnalysisResource CreateBulletin17C(CreateBulletin17CAnalysisRequest request);

        /// <summary>
        /// Creates a rating curve analysis over clones of the referenced stage and discharge series.
        /// </summary>
        /// <param name="request">The creation request.</param>
        /// <returns>The created resource.</returns>
        /// <exception cref="Exceptions.ResourceNotFoundException">Thrown when either time-series resource does not exist.</exception>
        /// <exception cref="ArgumentException">Thrown when the stage-grid options are inconsistent.</exception>
        AnalysisResource CreateRatingCurve(CreateRatingCurveAnalysisRequest request);

        /// <summary>
        /// Creates a Bayesian MCMC mixture-distribution analysis over a clone of the referenced
        /// input data.
        /// </summary>
        /// <param name="request">The creation request.</param>
        /// <returns>The created resource.</returns>
        /// <exception cref="Exceptions.ResourceNotFoundException">Thrown when the input-data resource does not exist.</exception>
        /// <exception cref="ArgumentException">Thrown when a component distribution or option is invalid.</exception>
        AnalysisResource CreateMixture(CreateMixtureAnalysisRequest request);

        /// <summary>
        /// Creates a Bayesian MCMC peaks-over-threshold point process analysis over a clone of
        /// the referenced input data.
        /// </summary>
        /// <param name="request">The creation request.</param>
        /// <returns>The created resource.</returns>
        /// <exception cref="Exceptions.ResourceNotFoundException">Thrown when the input-data resource does not exist.</exception>
        /// <exception cref="ArgumentException">Thrown when a threshold, record-span, or option value is invalid.</exception>
        AnalysisResource CreatePointProcess(CreatePointProcessAnalysisRequest request);

        /// <summary>
        /// Creates a Bayesian MCMC competing risks analysis over a clone of the referenced input data.
        /// </summary>
        /// <param name="request">The creation request.</param>
        /// <returns>The created resource.</returns>
        /// <exception cref="Exceptions.ResourceNotFoundException">Thrown when the input-data resource does not exist.</exception>
        /// <exception cref="ArgumentException">Thrown when a component distribution or option is invalid.</exception>
        AnalysisResource CreateCompetingRisks(CreateCompetingRisksAnalysisRequest request);

        /// <summary>
        /// Creates a composite analysis over LIVE references to existing component analyses.
        /// Components must be estimated by the time the composite runs, not at creation.
        /// </summary>
        /// <param name="request">The creation request.</param>
        /// <returns>The created resource.</returns>
        /// <exception cref="Exceptions.ResourceNotFoundException">Thrown when a component analysis does not exist.</exception>
        /// <exception cref="ArgumentException">Thrown when a component kind is not a valid composite component or mixture weights are invalid.</exception>
        AnalysisResource CreateComposite(CreateCompositeAnalysisRequest request);

        /// <summary>
        /// Creates a distribution-fitting analysis (parallel MLE with information-criterion
        /// ranking) over a clone of the referenced input data.
        /// </summary>
        /// <param name="request">The creation request.</param>
        /// <returns>The created resource.</returns>
        /// <exception cref="Exceptions.ResourceNotFoundException">Thrown when the input-data resource does not exist.</exception>
        /// <exception cref="ArgumentException">Thrown when a candidate distribution is not supported.</exception>
        AnalysisResource CreateDistributionFitting(CreateDistributionFittingAnalysisRequest request);

        /// <summary>
        /// Runs a distribution-fitting analysis synchronously and returns its ranked fits.
        /// </summary>
        /// <param name="id">The resource id.</param>
        /// <param name="cancellationToken">Client cancellation; aborts the fitting loop.</param>
        /// <returns>The ranked fitting results.</returns>
        /// <exception cref="Exceptions.ResourceNotFoundException">Thrown when no matching resource has the id.</exception>
        /// <exception cref="Exceptions.ResourceConflictException">Thrown when the analysis is already running.</exception>
        /// <exception cref="Exceptions.RequestValidationException">Thrown when the configuration fails validation.</exception>
        Task<DistributionFittingResultsResponse> RunDistributionFittingAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns the stored results of a previously run distribution-fitting analysis.
        /// </summary>
        /// <param name="id">The resource id.</param>
        /// <returns>The ranked fitting results.</returns>
        /// <exception cref="Exceptions.ResourceNotFoundException">Thrown when no matching resource has the id, or it has never been run.</exception>
        DistributionFittingResultsResponse GetDistributionFittingResults(Guid id);

        /// <summary>
        /// Creates a Bayesian MCMC bivariate copula analysis over LIVE references to two fitted
        /// marginal analyses. Marginals must be estimated by the time this analysis runs.
        /// </summary>
        /// <param name="request">The creation request.</param>
        /// <returns>The created resource.</returns>
        /// <exception cref="Exceptions.ResourceNotFoundException">Thrown when a marginal analysis does not exist.</exception>
        /// <exception cref="ArgumentException">Thrown when a marginal kind, the pairing overlap, or an option is invalid.</exception>
        AnalysisResource CreateBivariate(CreateBivariateAnalysisRequest request);

        /// <summary>
        /// Creates a coincident frequency analysis over a LIVE reference to a bivariate analysis
        /// plus a client-supplied response surface. The bivariate must be estimated by the time
        /// this analysis runs.
        /// </summary>
        /// <param name="request">The creation request.</param>
        /// <returns>The created resource.</returns>
        /// <exception cref="Exceptions.ResourceNotFoundException">Thrown when the bivariate analysis does not exist.</exception>
        /// <exception cref="ArgumentException">Thrown when the response surface shape or an option is invalid.</exception>
        AnalysisResource CreateCoincidentFrequency(CreateCoincidentFrequencyAnalysisRequest request);

        /// <summary>
        /// Runs a bivariate copula analysis synchronously and returns its joint-exceedance results.
        /// </summary>
        /// <param name="id">The resource id.</param>
        /// <param name="cancellationToken">Client cancellation; aborts the estimation.</param>
        /// <returns>The bivariate results.</returns>
        /// <exception cref="Exceptions.ResourceNotFoundException">Thrown when no matching resource has the id.</exception>
        /// <exception cref="Exceptions.ResourceConflictException">Thrown when the analysis or a marginal is already running.</exception>
        /// <exception cref="Exceptions.RequestValidationException">Thrown when the configuration fails validation.</exception>
        Task<BivariateResultsResponse> RunBivariateAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns the stored results of a previously run bivariate analysis.
        /// </summary>
        /// <param name="id">The resource id.</param>
        /// <returns>The bivariate results.</returns>
        /// <exception cref="Exceptions.ResourceNotFoundException">Thrown when no matching resource has the id, or it has never been run.</exception>
        BivariateResultsResponse GetBivariateResults(Guid id);

        /// <summary>
        /// Runs a coincident frequency analysis synchronously and returns its response frequency curve.
        /// </summary>
        /// <param name="id">The resource id.</param>
        /// <param name="cancellationToken">Client cancellation; aborts the aggregation.</param>
        /// <returns>The coincident frequency results.</returns>
        /// <exception cref="Exceptions.ResourceNotFoundException">Thrown when no matching resource has the id.</exception>
        /// <exception cref="Exceptions.ResourceConflictException">Thrown when the analysis, its bivariate, or a transitive marginal is already running.</exception>
        /// <exception cref="Exceptions.RequestValidationException">Thrown when the configuration fails validation.</exception>
        Task<CoincidentFrequencyResultsResponse> RunCoincidentFrequencyAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns the stored results of a previously run coincident frequency analysis.
        /// </summary>
        /// <param name="id">The resource id.</param>
        /// <returns>The coincident frequency results.</returns>
        /// <exception cref="Exceptions.ResourceNotFoundException">Thrown when no matching resource has the id, or it has never been run.</exception>
        CoincidentFrequencyResultsResponse GetCoincidentFrequencyResults(Guid id);

        /// <summary>
        /// Creates a Bayesian MCMC time-series analysis (AR, MA, ARIMA, or ARIMAX per the
        /// request's modelType) over clones of the referenced series and covariates.
        /// </summary>
        /// <param name="request">The creation request.</param>
        /// <returns>The created resource.</returns>
        /// <exception cref="Exceptions.ResourceNotFoundException">Thrown when the time-series or a covariate resource does not exist.</exception>
        /// <exception cref="ArgumentException">Thrown when a field does not apply to the model type or a value is out of range.</exception>
        AnalysisResource CreateTimeSeries(CreateTimeSeriesAnalysisRequest request);

        /// <summary>
        /// Runs a time-series analysis synchronously and returns its fitted-plus-forecast results.
        /// </summary>
        /// <param name="id">The resource id.</param>
        /// <param name="cancellationToken">Client cancellation; aborts the estimation.</param>
        /// <returns>The time-series results.</returns>
        /// <exception cref="Exceptions.ResourceNotFoundException">Thrown when no matching resource has the id.</exception>
        /// <exception cref="Exceptions.ResourceConflictException">Thrown when the analysis is already running.</exception>
        /// <exception cref="Exceptions.RequestValidationException">Thrown when the configuration fails validation.</exception>
        Task<TimeSeriesResultsResponse> RunTimeSeriesAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns the stored results of a previously run time-series analysis.
        /// </summary>
        /// <param name="id">The resource id.</param>
        /// <returns>The time-series results.</returns>
        /// <exception cref="Exceptions.ResourceNotFoundException">Thrown when no matching resource has the id, or it has never been run.</exception>
        TimeSeriesResultsResponse GetTimeSeriesResults(Guid id);

        /// <summary>
        /// Lists analysis resources ordered by creation time, optionally filtered by kind.
        /// </summary>
        /// <param name="kind">The kind to filter to, or null for all analyses.</param>
        /// <returns>The resources ordered by creation time.</returns>
        IReadOnlyList<AnalysisResource> List(AnalysisKind? kind = null);

        /// <summary>
        /// Returns the analysis resource with the given id.
        /// </summary>
        /// <param name="id">The resource id.</param>
        /// <param name="expectedKind">When set, the resource must be of this kind or the lookup fails.</param>
        /// <returns>The resource.</returns>
        /// <exception cref="Exceptions.ResourceNotFoundException">Thrown when no matching resource has the id.</exception>
        AnalysisResource Get(Guid id, AnalysisKind? expectedKind = null);

        /// <summary>
        /// Runs model-layer validation for the analysis without running it.
        /// </summary>
        /// <param name="id">The resource id.</param>
        /// <param name="expectedKind">When set, the resource must be of this kind.</param>
        /// <returns>The validation response.</returns>
        /// <exception cref="Exceptions.ResourceNotFoundException">Thrown when no matching resource has the id.</exception>
        ValidationResponse Validate(Guid id, AnalysisKind? expectedKind = null);

        /// <summary>
        /// Runs a frequency-kind analysis (univariate, Bulletin 17C, mixture, point process, or
        /// competing risks) synchronously and returns its frequency results.
        /// </summary>
        /// <param name="id">The resource id.</param>
        /// <param name="expectedKind">The expected kind, or null to accept any frequency kind.</param>
        /// <param name="cancellationToken">Client cancellation; aborts the estimation.</param>
        /// <returns>The frequency results.</returns>
        /// <exception cref="Exceptions.ResourceNotFoundException">Thrown when no matching resource has the id.</exception>
        /// <exception cref="Exceptions.ResourceConflictException">Thrown when the analysis is already running.</exception>
        /// <exception cref="Exceptions.RequestValidationException">Thrown when the configuration fails validation.</exception>
        Task<FrequencyResultsResponse> RunFrequencyAsync(Guid id, AnalysisKind? expectedKind, CancellationToken cancellationToken = default);

        /// <summary>
        /// Runs a rating curve analysis synchronously and returns its results.
        /// </summary>
        /// <param name="id">The resource id.</param>
        /// <param name="cancellationToken">Client cancellation; aborts the estimation.</param>
        /// <returns>The rating curve results.</returns>
        /// <exception cref="Exceptions.ResourceNotFoundException">Thrown when no matching resource has the id.</exception>
        /// <exception cref="Exceptions.ResourceConflictException">Thrown when the analysis is already running.</exception>
        /// <exception cref="Exceptions.RequestValidationException">Thrown when the configuration fails validation.</exception>
        Task<RatingCurveResultsResponse> RunRatingCurveAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns the stored frequency results of a previously run frequency-kind analysis.
        /// </summary>
        /// <param name="id">The resource id.</param>
        /// <param name="expectedKind">The expected kind, or null to accept any frequency kind.</param>
        /// <returns>The frequency results.</returns>
        /// <exception cref="Exceptions.ResourceNotFoundException">Thrown when no matching resource has the id, or it has never been run.</exception>
        FrequencyResultsResponse GetFrequencyResults(Guid id, AnalysisKind? expectedKind = null);

        /// <summary>
        /// Returns the stored results of a previously run rating curve analysis.
        /// </summary>
        /// <param name="id">The resource id.</param>
        /// <returns>The rating curve results.</returns>
        /// <exception cref="Exceptions.ResourceNotFoundException">Thrown when no matching resource has the id, or it has never been run.</exception>
        RatingCurveResultsResponse GetRatingCurveResults(Guid id);

        /// <summary>
        /// Deletes the analysis resource with the given id.
        /// </summary>
        /// <param name="id">The resource id.</param>
        /// <param name="expectedKind">When set, the resource must be of this kind.</param>
        /// <exception cref="Exceptions.ResourceNotFoundException">Thrown when no matching resource has the id.</exception>
        /// <exception cref="Exceptions.ResourceConflictException">Thrown when the analysis is currently running.</exception>
        void Delete(Guid id, AnalysisKind? expectedKind = null);
    }
}
