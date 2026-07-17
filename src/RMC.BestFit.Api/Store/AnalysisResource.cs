using RMC.BestFit.Analyses;

namespace RMC.BestFit.Api.Store
{
    /// <summary>
    /// A server-side analysis resource wrapping exactly one model-layer analysis together with
    /// provenance ids and run state.
    /// </summary>
    /// <remarks>
    /// The wrapped analysis owns clones of its input data, so deleting the upstream time-series or
    /// input-data resources never invalidates an existing analysis. Run state fields are the only
    /// mutable part of any resource; they are guarded by <see cref="RunLock"/> so concurrent run
    /// requests for the same analysis are rejected rather than interleaved.
    /// </remarks>
    public class AnalysisResource : ResourceBase
    {
        /// <summary>
        /// Which model-layer analysis type this resource wraps. Exactly one typed analysis
        /// property matching the kind is non-null.
        /// </summary>
        public required AnalysisKind Kind { get; init; }

        /// <summary>
        /// The wrapped Bayesian MCMC univariate frequency analysis, when <see cref="Kind"/> is Univariate.
        /// </summary>
        public UnivariateAnalysis? Univariate { get; init; }

        /// <summary>
        /// The wrapped Bulletin 17C analysis, when <see cref="Kind"/> is Bulletin17C.
        /// </summary>
        public Bulletin17CAnalysis? Bulletin17C { get; init; }

        /// <summary>
        /// The wrapped rating curve analysis, when <see cref="Kind"/> is RatingCurve.
        /// </summary>
        public RatingCurveAnalysis? RatingCurve { get; init; }

        /// <summary>
        /// The wrapped mixture-distribution analysis, when <see cref="Kind"/> is Mixture.
        /// </summary>
        public MixtureAnalysis? Mixture { get; init; }

        /// <summary>
        /// The wrapped peaks-over-threshold point process analysis, when <see cref="Kind"/> is PointProcess.
        /// </summary>
        public PointProcessAnalysis? PointProcess { get; init; }

        /// <summary>
        /// The wrapped competing risks analysis, when <see cref="Kind"/> is CompetingRisks.
        /// </summary>
        public CompetingRiskAnalysis? CompetingRisks { get; init; }

        /// <summary>
        /// The wrapped composite analysis, when <see cref="Kind"/> is Composite.
        /// </summary>
        public CompositeAnalysis? Composite { get; init; }

        /// <summary>
        /// The wrapped distribution-fitting analysis, when <see cref="Kind"/> is DistributionFitting.
        /// </summary>
        public FittingAnalysis? DistributionFitting { get; init; }

        /// <summary>
        /// The wrapped bivariate copula analysis, when <see cref="Kind"/> is Bivariate.
        /// </summary>
        public BivariateAnalysis? Bivariate { get; init; }

        /// <summary>
        /// The wrapped coincident frequency analysis, when <see cref="Kind"/> is CoincidentFrequency.
        /// </summary>
        public CoincidentFrequencyAnalysis? CoincidentFrequency { get; init; }

        /// <summary>
        /// Which time-series model family a TimeSeries-kind resource wraps; null for other kinds.
        /// Exactly one of <see cref="Ar"/>, <see cref="Ma"/>, <see cref="Arima"/>, or
        /// <see cref="Arimax"/> matches it.
        /// </summary>
        public TimeSeriesModelType? TimeSeriesModel { get; init; }

        /// <summary>
        /// The wrapped autoregressive analysis, when <see cref="TimeSeriesModel"/> is Ar.
        /// </summary>
        public ARAnalysis? Ar { get; init; }

        /// <summary>
        /// The wrapped moving-average analysis, when <see cref="TimeSeriesModel"/> is Ma.
        /// </summary>
        public MAAnalysis? Ma { get; init; }

        /// <summary>
        /// The wrapped ARIMA analysis, when <see cref="TimeSeriesModel"/> is Arima.
        /// </summary>
        public ARIMAAnalysis? Arima { get; init; }

        /// <summary>
        /// The wrapped ARIMAX analysis, when <see cref="TimeSeriesModel"/> is Arimax.
        /// </summary>
        public ARIMAXAnalysis? Arimax { get; init; }

        /// <summary>
        /// The id of the time-series resource a TimeSeries-kind analysis was created from.
        /// Provenance only.
        /// </summary>
        public Guid? TimeSeriesId { get; init; }

        /// <summary>
        /// The ids of the covariate time-series resources of an ARIMAX analysis, in covariate
        /// order. Provenance only — the covariate data is cloned at creation.
        /// </summary>
        public List<Guid>? CovariateTimeSeriesIds { get; init; }

        /// <summary>
        /// LIVE references to the component analysis resources this analysis consumes
        /// (composite components; a bivariate's two marginals; a coincident frequency analysis's
        /// bivariate plus its transitive marginals). Held so the component objects and their run
        /// locks stay reachable even if the components are later deleted from the store.
        /// Component runs are excluded while this analysis runs (the run path try-acquires every
        /// component's <see cref="RunLock"/>), and re-running a component intentionally changes
        /// this analysis's next run — the documented exception to the clone-at-create invariant.
        /// </summary>
        public IReadOnlyList<AnalysisResource>? ComponentResources { get; init; }

        /// <summary>
        /// The LIVE marginal-X analysis resource of a bivariate analysis, for chain access and
        /// results metadata; null for other kinds.
        /// </summary>
        public AnalysisResource? MarginalXResource { get; init; }

        /// <summary>
        /// The LIVE marginal-Y analysis resource of a bivariate analysis; null for other kinds.
        /// </summary>
        public AnalysisResource? MarginalYResource { get; init; }

        /// <summary>
        /// The LIVE upstream bivariate analysis resource of a coincident frequency analysis;
        /// null for other kinds.
        /// </summary>
        public AnalysisResource? BivariateResource { get; init; }

        /// <summary>
        /// The id of the marginal-X analysis resource (bivariate analyses). Provenance only.
        /// </summary>
        public Guid? MarginalXAnalysisId { get; init; }

        /// <summary>
        /// The id of the marginal-Y analysis resource (bivariate analyses). Provenance only.
        /// </summary>
        public Guid? MarginalYAnalysisId { get; init; }

        /// <summary>
        /// The id of the upstream bivariate analysis resource (coincident frequency analyses).
        /// Provenance only.
        /// </summary>
        public Guid? BivariateAnalysisId { get; init; }

        /// <summary>
        /// The ids of the component analysis resources, aligned with the composite's component
        /// order. Provenance only — a referenced resource may since have been deleted.
        /// </summary>
        public List<Guid>? ComponentAnalysisIds { get; init; }

        /// <summary>
        /// The id of the input-data resource the analysis was created from (univariate and
        /// Bulletin 17C analyses). Provenance only — the referenced resource may since have been deleted.
        /// </summary>
        public Guid? InputDataId { get; init; }

        /// <summary>
        /// The id of the stage time-series resource the analysis was created from (rating curve
        /// analyses). Provenance only.
        /// </summary>
        public Guid? StageTimeSeriesId { get; init; }

        /// <summary>
        /// The id of the discharge time-series resource the analysis was created from (rating curve
        /// analyses). Provenance only.
        /// </summary>
        public Guid? DischargeTimeSeriesId { get; init; }

        /// <summary>
        /// Non-fatal warnings recorded when the resource was created (e.g., a Bulletin 17C
        /// analysis over input data containing uncertain observations, which its Expected Moments
        /// Algorithm does not use). Surfaced on the summary and validate responses.
        /// </summary>
        public IReadOnlyList<string> CreationWarnings { get; init; } = Array.Empty<string>();

        /// <summary>
        /// The lifecycle state of the most recent (or in-flight) run.
        /// </summary>
        public AnalysisRunState State { get; set; } = AnalysisRunState.Created;

        /// <summary>
        /// The UTC timestamp at which the most recent run finished, or null when never run.
        /// </summary>
        public DateTime? LastRunUtc { get; set; }

        /// <summary>
        /// The error message from the most recent failed run, or null.
        /// </summary>
        public string? LastError { get; set; }

        /// <summary>
        /// The wall-clock duration of the most recent run in milliseconds, or null when never run.
        /// </summary>
        public long? LastRunMs { get; set; }

        /// <summary>
        /// Single-entry lock ensuring only one run executes per analysis at a time. Run requests
        /// that cannot acquire it immediately are rejected with HTTP 409.
        /// </summary>
        public SemaphoreSlim RunLock { get; } = new SemaphoreSlim(1, 1);

        /// <summary>
        /// The wrapped analysis viewed through the shared <see cref="AnalysisBase"/> surface
        /// (RunAsync, CancelAnalysis, IsEstimated), regardless of kind.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the typed analysis field matching <see cref="Kind"/> is null, which indicates
        /// a construction bug in the service layer.
        /// </exception>
        public AnalysisBase Analysis => Kind switch
        {
            AnalysisKind.Univariate => Univariate ?? throw new InvalidOperationException("Analysis resource of kind Univariate has no univariate analysis attached."),
            AnalysisKind.Bulletin17C => Bulletin17C ?? throw new InvalidOperationException("Analysis resource of kind Bulletin17C has no Bulletin 17C analysis attached."),
            AnalysisKind.RatingCurve => RatingCurve ?? throw new InvalidOperationException("Analysis resource of kind RatingCurve has no rating curve analysis attached."),
            AnalysisKind.Mixture => Mixture ?? throw new InvalidOperationException("Analysis resource of kind Mixture has no mixture analysis attached."),
            AnalysisKind.PointProcess => PointProcess ?? throw new InvalidOperationException("Analysis resource of kind PointProcess has no point process analysis attached."),
            AnalysisKind.CompetingRisks => CompetingRisks ?? throw new InvalidOperationException("Analysis resource of kind CompetingRisks has no competing risks analysis attached."),
            AnalysisKind.Composite => Composite ?? throw new InvalidOperationException("Analysis resource of kind Composite has no composite analysis attached."),
            AnalysisKind.DistributionFitting => DistributionFitting ?? throw new InvalidOperationException("Analysis resource of kind DistributionFitting has no fitting analysis attached."),
            AnalysisKind.Bivariate => Bivariate ?? throw new InvalidOperationException("Analysis resource of kind Bivariate has no bivariate analysis attached."),
            AnalysisKind.CoincidentFrequency => CoincidentFrequency ?? throw new InvalidOperationException("Analysis resource of kind CoincidentFrequency has no coincident frequency analysis attached."),
            AnalysisKind.TimeSeries => TimeSeriesModel switch
            {
                TimeSeriesModelType.Ar => Ar ?? throw new InvalidOperationException("Analysis resource of kind TimeSeries (ar) has no AR analysis attached."),
                TimeSeriesModelType.Ma => Ma ?? throw new InvalidOperationException("Analysis resource of kind TimeSeries (ma) has no MA analysis attached."),
                TimeSeriesModelType.Arima => Arima ?? throw new InvalidOperationException("Analysis resource of kind TimeSeries (arima) has no ARIMA analysis attached."),
                TimeSeriesModelType.Arimax => Arimax ?? throw new InvalidOperationException("Analysis resource of kind TimeSeries (arimax) has no ARIMAX analysis attached."),
                _ => throw new InvalidOperationException("Analysis resource of kind TimeSeries has no model discriminator.")
            },
            _ => throw new InvalidOperationException($"Unknown analysis kind '{Kind}'.")
        };
    }
}
