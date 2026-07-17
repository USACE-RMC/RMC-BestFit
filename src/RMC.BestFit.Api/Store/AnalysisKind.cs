namespace RMC.BestFit.Api.Store
{
    /// <summary>
    /// Identifies which model-layer analysis type an analysis resource wraps.
    /// </summary>
    public enum AnalysisKind
    {
        /// <summary>
        /// Bayesian MCMC univariate frequency analysis (<see cref="RMC.BestFit.Analyses.UnivariateAnalysis"/>).
        /// </summary>
        Univariate,

        /// <summary>
        /// Bulletin 17C flood frequency analysis (<see cref="RMC.BestFit.Analyses.Bulletin17CAnalysis"/>).
        /// </summary>
        Bulletin17C,

        /// <summary>
        /// Bayesian stage-discharge rating curve analysis (<see cref="RMC.BestFit.Analyses.RatingCurveAnalysis"/>).
        /// </summary>
        RatingCurve,

        /// <summary>
        /// Bayesian MCMC mixture-distribution frequency analysis (<see cref="RMC.BestFit.Analyses.MixtureAnalysis"/>).
        /// </summary>
        Mixture,

        /// <summary>
        /// Bayesian MCMC peaks-over-threshold point process analysis (<see cref="RMC.BestFit.Analyses.PointProcessAnalysis"/>).
        /// </summary>
        PointProcess,

        /// <summary>
        /// Bayesian MCMC competing risks frequency analysis (<see cref="RMC.BestFit.Analyses.CompetingRiskAnalysis"/>).
        /// </summary>
        CompetingRisks,

        /// <summary>
        /// Composite analysis combining already-fitted component analyses
        /// (<see cref="RMC.BestFit.Analyses.CompositeAnalysis"/>).
        /// </summary>
        Composite,

        /// <summary>
        /// Parallel maximum-likelihood fit of many distributions with information-criterion
        /// ranking (<see cref="RMC.BestFit.Analyses.FittingAnalysis"/>).
        /// </summary>
        DistributionFitting,

        /// <summary>
        /// Bayesian MCMC copula analysis over two fitted marginal analyses
        /// (<see cref="RMC.BestFit.Analyses.BivariateAnalysis"/>).
        /// </summary>
        Bivariate,

        /// <summary>
        /// Coincident frequency analysis integrating a user-supplied response surface over a
        /// fitted bivariate analysis (<see cref="RMC.BestFit.Analyses.CoincidentFrequencyAnalysis"/>).
        /// </summary>
        CoincidentFrequency,

        /// <summary>
        /// Bayesian MCMC time-series analysis (AR, MA, ARIMA, or ARIMAX — discriminated by
        /// <see cref="AnalysisResource.TimeSeriesModel"/>).
        /// </summary>
        TimeSeries
    }
}
