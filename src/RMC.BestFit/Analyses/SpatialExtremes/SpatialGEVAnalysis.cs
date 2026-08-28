using Numerics;
using Numerics.Data;
using Numerics.Data.Statistics;
using Numerics.Distributions;
using Numerics.Mathematics.LinearAlgebra;
using Numerics.Mathematics.Optimization;
using Numerics.Sampling;
using Numerics.Sampling.MCMC;
using Numerics.Utilities;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models.SpatialExtremes;
using RMC.BestFit.Models.TrendFunctions;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Diagnostics;
using System.Xml.Linq;

namespace RMC.BestFit.Analyses
{
    /// <summary>
    /// Specifies the method used for confidence interval computation in spatial GEV analysis.
    /// </summary>
    public enum SpatialGEVUncertaintyMethod
    {
        /// <summary>
        /// Standard Bayesian posterior credible intervals from MCMC samples.
        /// Uses copula + regression errors for fully Bayesian uncertainty propagation.
        /// </summary>
        BayesianPosterior,

        /// <summary>
        /// Bayesian posterior with variance inflation based on effective sample size.
        /// Applies a scalar correction factor to account for intersite correlation.
        /// </summary>
        BayesianInflated,

        /// <summary>
        /// MLE with Godambe sandwich covariance for robust standard errors.
        /// Accounts for model misspecification and correlation.
        /// </summary>
        GodambeSandwich,

        /// <summary>
        /// Spatial block bootstrap for empirical confidence intervals.
        /// Preserves spatial correlation structure in resampling.
        /// </summary>
        SpatialBootstrap
    }

    /// <summary>
    /// Performs Bayesian MCMC estimation for a Spatial GEV (Generalized Extreme Value) model.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     <b>Authors:</b>
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    /// This analysis implements regional frequency analysis using a hierarchical Bayesian spatial model
    /// for extreme value data. The model assumes GEV distributions at each site with spatially-varying
    /// parameters that follow trend surfaces with optional spatially-correlated errors.
    /// </para>
    /// <para>
    /// Key features:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Bayesian MCMC estimation of all model parameters.</description></item>
    /// <item><description>Site-specific quantile estimation with full uncertainty quantification.</description></item>
    /// <item><description>Spatial interpolation to ungauged locations.</description></item>
    /// <item><description>Leave-one-site-out cross-validation for model assessment.</description></item>
    /// <item><description>Regional pooling of information across sites.</description></item>
    /// </list>
    /// <para>
    ///     <b>References:</b>
    ///     - Renard, B., et al. (2006). Use of a Gaussian copula for multivariate extreme value analysis.
    ///     - Cooley, D., et al. (2007). Bayesian spatial modeling of extreme precipitation return levels.
    ///     - Davison, A.C., et al. (2012). Statistical modeling of spatial extremes.
    /// </para>
    /// </remarks>
    public class SpatialGEVAnalysis : AnalysisBase, IBayesianAnalysis
    {
        #region Construction

        /// <summary>
        /// Initializes a new instance of the <see cref="SpatialGEVAnalysis"/> class
        /// for the specified SpatialGEV model.
        /// </summary>
        /// <param name="spatialGEV">
        /// The <see cref="SpatialGEV"/> model to be estimated.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="spatialGEV"/> is <c>null</c>.
        /// </exception>
        public SpatialGEVAnalysis(SpatialGEV spatialGEV)
        {
            SpatialGEV = spatialGEV ?? throw new ArgumentNullException(nameof(spatialGEV));
            BayesianAnalysis = new BayesianAnalysis(spatialGEV);
            SetDefaultProbabilityOrdinates();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SpatialGEVAnalysis"/> class
        /// by deserializing from an <see cref="XElement"/>.
        /// </summary>
        /// <param name="spatialGEV">
        /// The <see cref="SpatialGEV"/> model associated with this analysis.
        /// </param>
        /// <param name="xElement">
        /// The XML element from which to restore the analysis configuration and results.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="spatialGEV"/> or <paramref name="xElement"/> is <c>null</c>.
        /// </exception>
        public SpatialGEVAnalysis(SpatialGEV spatialGEV, XElement xElement)
        {
            if (xElement == null) throw new ArgumentNullException(nameof(xElement));

            SpatialGEV = spatialGEV ?? throw new ArgumentNullException(nameof(spatialGEV));

            // Probability ordinates set FIRST. The setter calls ClearResults(), which
            // is null-safe against the not-yet-assigned BayesianAnalysis (uses the
            // null-conditional `BayesianAnalysis?.ClearResults()`), so this order is safe
            // and matches the convention used by the other analyses in the project.
            ProbabilityOrdinates = new ProbabilityOrdinates();
            var probElement = xElement.Element("ProbabilityOrdinates");
            if (probElement != null)
            {
                var probText = (string)probElement;
                ProbabilityOrdinates.FromDelimitedString(probText, ProbabilityOrdinates.DefaultDelimiter);
            }

            // BayesianAnalysis set SECOND, after ProbabilityOrdinates is in place.
            var bayesElement = xElement.Element("BayesianAnalysis");
            if (bayesElement != null)
            {
                BayesianAnalysis = new BayesianAnalysis(spatialGEV, bayesElement);
            }
            else
            {
                BayesianAnalysis = new BayesianAnalysis(spatialGEV);
            }

            // Uncertainty settings (optional attributes; legacy projects keep the defaults)
            var methodAttr = xElement.Attribute(nameof(UncertaintyMethod));
            if (methodAttr != null && Enum.TryParse(methodAttr.Value, out SpatialGEVUncertaintyMethod method))
                UncertaintyMethod = method;
            var residualAttr = xElement.Attribute(nameof(SampleConditionalResidual));
            if (residualAttr != null && bool.TryParse(residualAttr.Value, out bool residual))
                SampleConditionalResidual = residual;
            var replicatesAttr = xElement.Attribute(nameof(BootstrapReplicates));
            if (replicatesAttr != null && int.TryParse(replicatesAttr.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int replicates) && replicates > 0)
                _bootstrapReplicates = replicates;
            var blockAttr = xElement.Attribute(nameof(BootstrapBlockSize));
            if (blockAttr != null && int.TryParse(blockAttr.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int block) && block >= 0)
                _bootstrapBlockSize = block;

            // Check if estimated
            var isEstimatedAttr = xElement.Attribute("IsEstimated");
            if (isEstimatedAttr != null && bool.TryParse(isEstimatedAttr.Value, out bool isEst))
            {
                _isEstimated = isEst;
            }
        }

        #endregion

        #region Members

        private SpatialGEV _spatialGEV = null!;
        private BayesianAnalysis _bayesianAnalysis = null!;
        private ProbabilityOrdinates _probabilityOrdinates = null!;

        /// <summary>
        /// Gets or sets the Spatial GEV model.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When the model changes, the analysis subscribes to its
        /// <c>PropertyChanged</c> event and updates
        /// the associated <see cref="BayesianAnalysis"/> model reference.
        /// </para>
        /// </remarks>
        public SpatialGEV SpatialGEV
        {
            get { return _spatialGEV; }
            private set
            {
                if (_spatialGEV != null)
                {
                    _spatialGEV.PropertyChanged -= Model_PropertyChanged;
                }

                _spatialGEV = value;

                if (_spatialGEV != null)
                {
                    if (_bayesianAnalysis != null)
                        _bayesianAnalysis.Model = _spatialGEV;

                    _spatialGEV.PropertyChanged += Model_PropertyChanged;
                }

                RaisePropertyChange(nameof(SpatialGEV));
            }
        }

        /// <summary>
        /// Gets the Bayesian MCMC analysis object.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The <see cref="BayesianAnalysis"/> handles all MCMC simulation, convergence diagnostics,
        /// and posterior sampling for the <see cref="SpatialGEV"/> model.
        /// </para>
        /// </remarks>
        public BayesianAnalysis BayesianAnalysis
        {
            get { return _bayesianAnalysis; }
            private set
            {
                if (_bayesianAnalysis != null)
                {
                    _bayesianAnalysis.PropertyChanged -= BayesianAnalysis_PropertyChanged;
                }

                _bayesianAnalysis = value;

                if (_bayesianAnalysis != null)
                {
                    _bayesianAnalysis.PropertyChanged += BayesianAnalysis_PropertyChanged;
                }

                RaisePropertyChange(nameof(BayesianAnalysis));
            }
        }

        /// <summary>
        /// Gets or sets the probability ordinates for quantile estimation.
        /// </summary>
        /// <remarks>
        /// <para>
        /// These ordinates define the exceedance probabilities (or return periods) at which
        /// quantiles are computed for all sites after the analysis completes.
        /// </para>
        /// </remarks>
        [Category("Output")]
        [DisplayName("Probability Ordinates")]
        [Description("The probability ordinates (exceedance probabilities) at which to compute quantiles.")]
        [Browsable(true)]
        public ProbabilityOrdinates ProbabilityOrdinates
        {
            get { return _probabilityOrdinates; }
            set
            {
                if (_probabilityOrdinates == value) return;

                if (_probabilityOrdinates != null)
                    _probabilityOrdinates.CollectionChanged -= ProbabilityOrdinates_CollectionChanged;

                _probabilityOrdinates = value ?? new ProbabilityOrdinates();
                _probabilityOrdinates.CollectionChanged += ProbabilityOrdinates_CollectionChanged;

                RaisePropertyChange(nameof(ProbabilityOrdinates));

                // Reprocess or clear derived results based on current state.
                // See ProbabilityOrdinates_CollectionChanged for the same logic on in-place edits.
                HandleOrdinatesChanged();
            }
        }

        /// <summary>
        /// Gets the uncertainty analysis results containing quantile estimates across all sites.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This property is <c>null</c> until <see cref="RunAsync"/> completes successfully.
        /// The results contain the aggregated (regional) frequency curve and uncertainty bounds.
        /// For site-specific results, use <see cref="SiteResults"/>.
        /// </para>
        /// </remarks>
        public UncertaintyAnalysisResults? AnalysisResults { get; private set; }

        /// <summary>
        /// Gets the site-specific analysis results containing GEV parameters and quantile curves for each site.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This array contains one <see cref="SpatialGEVSiteResults"/> object per site,
        /// each with the site's GEV parameters (mean and credible intervals) and quantile curves.
        /// </para>
        /// </remarks>
        public SpatialGEVSiteResults[]? SiteResults { get; private set; }

        /// <summary>
        /// Gets the cross-validation results from leave-one-site-out analysis.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This property is <c>null</c> unless <see cref="RunCrossValidationAsync"/> has been called.
        /// Contains prediction errors and skill scores for model validation.
        /// </para>
        /// </remarks>
        public SpatialGEVCrossValidationResults? CrossValidationResults { get; private set; }

        /// <summary>
        /// Gets or sets the method used for confidence interval computation.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The default method is <see cref="SpatialGEVUncertaintyMethod.BayesianPosterior"/>, which uses
        /// standard Bayesian credible intervals from MCMC samples. For proper CI coverage with
        /// correlated spatial data, ensure the model is configured with copula dependence and
        /// spatial regression errors via <see cref="SpatialGEV.ConfigureForProperCoverage"/>.
        /// </para>
        /// <para>
        /// Alternative methods include variance inflation based on effective sample size,
        /// Godambe sandwich covariance for MLE, and spatial block bootstrap.
        /// </para>
        /// </remarks>
        [Category("Output")]
        [DisplayName("Uncertainty Method")]
        [Description("The method used for confidence interval computation.")]
        [Browsable(true)]
        public SpatialGEVUncertaintyMethod UncertaintyMethod { get; set; } = SpatialGEVUncertaintyMethod.BayesianPosterior;

        /// <summary>
        /// Gets the uncertainty method the most recent run actually applied to the site and regional
        /// results, or <c>null</c> before a run or after <see cref="ClearResults"/>.
        /// </summary>
        [Category("Output")]
        [DisplayName("Applied Uncertainty Method")]
        [Description("The uncertainty method that produced the current interval bounds: Bayesian posterior intervals, posterior intervals widened by the variance inflation factor, Gaussian parameter draws from the Godambe sandwich covariance at the MAP, or temporal block bootstrap percentile intervals. Null until an analysis has run.")]
        [Browsable(true)]
        public SpatialGEVUncertaintyMethod? AppliedUncertaintyMethod { get; private set; }

        /// <summary>
        /// Gets or sets whether an ungauged-site prediction adds a draw of the conditional Gaussian-process
        /// residual (conditional variance of the latent error at the location) to the conditional mean for
        /// every posterior draw. Default is <c>true</c>; <c>false</c> uses the conditional mean only.
        /// </summary>
        /// <remarks>
        /// The residual draws come from a generator seeded with <see cref="BayesianAnalysis"/>'s
        /// <c>PRNGSeed</c>, so predictions are reproducible. With <c>false</c> the predictive interval
        /// omits the spatial-interpolation uncertainty of the latent errors and reflects parameter
        /// uncertainty only.
        /// </remarks>
        [Category("Output")]
        [DisplayName("Sample Conditional Residual")]
        [Description("When true (default), ungauged-site predictions add a seeded draw of the conditional Gaussian-process residual at the location to each posterior draw, so the predictive interval includes the spatial-interpolation uncertainty of the latent errors; set false to predict with the conditional mean only (parameter uncertainty only).")]
        [Browsable(true)]
        public bool SampleConditionalResidual { get; set; } = true;

        /// <summary>
        /// Gets or sets the number of bootstrap replicates used when <see cref="UncertaintyMethod"/> is
        /// <see cref="SpatialGEVUncertaintyMethod.SpatialBootstrap"/>. Default is 200.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is not positive.</exception>
        [Category("Output")]
        [DisplayName("Bootstrap Replicates")]
        [Description("The number of temporal block-bootstrap replicates (each a maximum a posteriori refit of a resampled data set) used for bootstrap intervals when the uncertainty method is SpatialBootstrap. Default 200; at least 50% of the replicates must succeed. Larger values narrow the Monte Carlo error of the percentile intervals at proportional cost.")]
        [Browsable(true)]
        public int BootstrapReplicates
        {
            get => _bootstrapReplicates;
            set
            {
                if (value <= 0)
                    throw new ArgumentOutOfRangeException(nameof(value), "At least one bootstrap replicate is required.");
                _bootstrapReplicates = value;
                RaisePropertyChange(nameof(BootstrapReplicates));
            }
        }

        /// <summary>
        /// Gets or sets the number of consecutive rows (years) per bootstrap block; 0 (default) uses the
        /// cube root of the number of rows, rounded up.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is negative.</exception>
        [Category("Output")]
        [DisplayName("Bootstrap Block Size")]
        [Description("The number of consecutive rows (years) resampled together in the temporal block bootstrap; blocks preserve short-range serial dependence between years while every site is kept in each row. 0 (default) uses the cube root of the number of rows, rounded up; 1 is an ordinary row bootstrap.")]
        [Browsable(true)]
        public int BootstrapBlockSize
        {
            get => _bootstrapBlockSize;
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(value), "The block size cannot be negative.");
                _bootstrapBlockSize = value;
                RaisePropertyChange(nameof(BootstrapBlockSize));
            }
        }

        /// <summary>
        /// Gets the replicate accounting of the most recent spatial bootstrap run, or <c>null</c> when no
        /// bootstrap has run since the results were cleared.
        /// </summary>
        public SpatialGEVBootstrapResults? BootstrapResults { get; private set; }

        /// <summary>
        /// The minimum fraction of bootstrap replicates that must succeed for intervals to be reported.
        /// </summary>
        private const double MinimumBootstrapSuccessFraction = 0.5;

        private int _bootstrapReplicates = 200;
        private int _bootstrapBlockSize;

        /// <summary>
        /// The regional mean quantile of every retained draw [probabilities × draws] from the most recent
        /// site-result construction; the regional curve's posterior summaries derive from it.
        /// </summary>
        private double[,]? _regionalQuantileDraws;

        /// <summary>
        /// Gets the Godambe (sandwich) covariance matrix after calling <see cref="ComputeGodambeCovariance"/>,
        /// or <c>null</c> before the first computation, after <see cref="ClearResults"/>, or when the most
        /// recent computation failed (see <see cref="GodambeCovarianceStatus"/>).
        /// </summary>
        public double[,]? GodambeCovariance { get; private set; }

        /// <summary>
        /// Gets the outcome of the most recent <see cref="ComputeGodambeCovariance"/> call:
        /// <see cref="CovarianceComputationStatus.NotComputed"/> before any call or after
        /// <see cref="ClearResults"/>, <see cref="CovarianceComputationStatus.Available"/> when
        /// <see cref="GodambeCovariance"/> holds a finite matrix with positive variances, and
        /// <see cref="CovarianceComputationStatus.Failed"/> when the computation failed and
        /// <see cref="GodambeCovariance"/> is <c>null</c>.
        /// </summary>
        public CovarianceComputationStatus GodambeCovarianceStatus { get; private set; } = CovarianceComputationStatus.NotComputed;

        /// <summary>
        /// Gets the diagnostic text of the most recent failed <see cref="ComputeGodambeCovariance"/>
        /// call, or <c>null</c> when the computation succeeded or has not been attempted.
        /// </summary>
        public string? GodambeCovarianceDiagnostic { get; private set; }

        /// <summary>
        /// Gets the variance inflation factor computed from intersite correlation.
        /// </summary>
        public double VarianceInflationFactor { get; private set; } = 1.0;

        #endregion

        #region Methods

        /// <summary>
        /// Handles property changes on the <see cref="SpatialGEV"/> model.
        /// Only structurally destructive changes (parameters, trend functions, dependence /
        /// error / link toggles, site weights) clear results. All other notifications are
        /// propagated for UI binding without invalidating the fit.
        /// </summary>
        private void Model_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SpatialGEV.Parameters) ||
                e.PropertyName == nameof(SpatialGEV.SetDefaultParameters) ||
                e.PropertyName == nameof(SpatialGEV.Location) ||
                e.PropertyName == nameof(SpatialGEV.Scale) ||
                e.PropertyName == nameof(SpatialGEV.Shape) ||
                e.PropertyName == nameof(SpatialGEV.UseCopulaDependence) ||
                e.PropertyName == nameof(SpatialGEV.UseLocationErrors) ||
                e.PropertyName == nameof(SpatialGEV.UseScaleErrors) ||
                e.PropertyName == nameof(SpatialGEV.UseShapeErrors) ||
                e.PropertyName == nameof(SpatialGEV.UseLogLinkForLocation) ||
                e.PropertyName == nameof(SpatialGEV.UseLogLinkForScale) ||
                e.PropertyName == nameof(SpatialGEV.SiteWeights))
            {
                if (BayesianAnalysis.UseSimulationDefaults)
                    BayesianAnalysis.SetDefaultSimulationOptions();

                if (BayesianAnalysis.UseAdvancedSimulationDefaults)
                    BayesianAnalysis.SetDefaultAdvancedSimulationOptions();

                ClearResults();
            }

            RaisePropertyChange(e.PropertyName);
        }

        /// <summary>
        /// Handles property changes on the <see cref="BayesianAnalysis"/> object.
        /// Updates point estimate results when the point estimator type changes.
        /// CredibleIntervalWidth changes reprocess derived uncertainty results without
        /// re-running the chain (the MCMC output is independent of the CI level).
        /// </summary>
        private void BayesianAnalysis_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(BayesianAnalysis.IsEstimated))
            {
                if (BayesianAnalysis.IsEstimated == false)
                {
                    ClearResults();
                }
                RaisePropertyChange(e.PropertyName);
            }
            else if (e.PropertyName == nameof(BayesianAnalysis.PointEstimator))
            {
                ReprocessIfEstimated(UpdatePointEstimateResultsAsync);
                RaisePropertyChange(e.PropertyName);
            }
            else if (e.PropertyName == nameof(BayesianAnalysis.CredibleIntervalWidth))
            {
                ReprocessIfEstimated(CreateUncertaintyAnalysisResultsAsync);
                RaisePropertyChange(e.PropertyName);
            }
            else
            {
                RaisePropertyChange(e.PropertyName);
            }
        }

        /// <summary>
        /// Sets the default probability ordinates for quantile estimation.
        /// </summary>
        private void SetDefaultProbabilityOrdinates()
        {
            // Use the setter so the CollectionChanged subscription is wired.
            ProbabilityOrdinates = new ProbabilityOrdinates();
        }

        /// <summary>
        /// Clears all analysis results and resets the <c>IsEstimated</c> flag.
        /// </summary>
        public void ClearResults()
        {
            BayesianAnalysis?.ClearResults();
            AnalysisResults = null;
            SiteResults = null;
            CrossValidationResults = null;
            GodambeCovariance = null;
            GodambeCovarianceStatus = CovarianceComputationStatus.NotComputed;
            GodambeCovarianceDiagnostic = null;
            AppliedUncertaintyMethod = null;
            BootstrapResults = null;
            _regionalQuantileDraws = null;
            RaisePropertyChange(nameof(AnalysisResults));
            RaisePropertyChange(nameof(SiteResults));
            RaisePropertyChange(nameof(CrossValidationResults));
            IsEstimated = false;
        }

        /// <summary>
        /// Clears <see cref="AnalysisResults"/> and <see cref="SiteResults"/> only — the outputs
        /// whose quantile arrays are keyed on <see cref="ProbabilityOrdinates"/>. Leaves the
        /// Bayesian MCMC output and <c>IsEstimated</c> intact so the fit can be reused
        /// once valid ordinates are restored.
        /// </summary>
        public void ClearUncertaintyAnalysisResults()
        {
            AnalysisResults = null;
            SiteResults = null;
            RaisePropertyChange(nameof(AnalysisResults));
            RaisePropertyChange(nameof(SiteResults));
        }

        /// <summary>
        /// Handles changes to the <see cref="ProbabilityOrdinates"/> collection (in-place edits).
        /// </summary>
        /// <remarks>
        /// Ordinates drive the quantile arrays in <see cref="SiteResults"/> and the aggregated
        /// curves in <see cref="AnalysisResults"/>. They do not affect the Bayesian MCMC output
        /// or <c>IsEstimated</c>. See <see cref="HandleOrdinatesChanged"/> for the
        /// reprocess-or-clear logic shared with the property setter.
        /// </remarks>
        private void ProbabilityOrdinates_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            RaisePropertyChange(nameof(ProbabilityOrdinates));
            HandleOrdinatesChanged();
        }

        /// <summary>
        /// Shared reprocess-or-clear logic for ordinate changes. Called from the property setter
        /// (full replacement) and the CollectionChanged handler (in-place edits).
        /// </summary>
        /// <remarks>
        /// SpatialGEV's reprocess chains two awaits (per-site results then aggregated
        /// uncertainty) — wraps them in an async lambda passed to the shared
        /// <see cref="AnalysisBase.ReprocessIfEstimated"/> helper for consistent
        /// fire-and-forget exception logging.
        /// </remarks>
        private void HandleOrdinatesChanged()
        {
            if (ProbabilityOrdinates.Validate().IsValid)
            {
                ReprocessIfEstimated(RebuildPosteriorResultsAsync);
            }
            else if (IsEstimated)
            {
                ClearUncertaintyAnalysisResults();
            }
        }

        /// <inheritdoc/>
        public override async Task RunAsync(SafeProgressReporter? progressReporter = null)
        {
            if (Validate().IsValid == false)
                throw new InvalidOperationException("Analysis is not valid. Please check the configuration before running the analysis.");

            // Preview event, allow GUI to cancel before we start
            var previewArgs = new CancelEventArgs();
            OnAnalysisStarting(previewArgs);
            if (previewArgs.Cancel)
            {
                OnAnalysisCompleted(new AnalysisRunCompletedEventArgs(
                    wasCanceled: true,
                    succeeded: false,
                    error: null));
                return;
            }

            // Set up cancellation token
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();
            var token = _cancellationTokenSource.Token;

            // Wait for any in-flight reprocess to finish before clearing results and
            // starting a new MCMC run. Without this gate, a fire-and-forget reprocess
            // (triggered by a prior property change via ReprocessIfEstimated) can be
            // inside its parallel loop when ClearResults() nulls AnalysisResults —
            // producing an NRE on the next AnalysisResults dereference inside the loop body.
            await _reprocessGate.WaitAsync();
            try
            {
                ClearResults();
                progressReporter?.IndicateTaskStart();
                AnalysisProgress.ReportStarting(progressReporter);

                bool wasCanceled = false;
                Exception? error = null;

                try
                {
                    // Run Bayesian analysis
                    await BayesianAnalysis.RunAsync(AnalysisProgress.CreateEstimatorReporter(progressReporter, nameof(BayesianAnalysis)), false);

                    // Post-process
                    if (BayesianAnalysis.IsEstimated == true)
                    {
                        AnalysisProgress.ReportProcessingResults(progressReporter);
                        await CreateSiteResultsAsync();
                        await CreateUncertaintyAnalysisResultsAsync();
                    }

                    // Conditional set — BayesianAnalysis.IsEstimated is false on soft-failure paths
                    // (sampler returns without setting IsEstimated). Setting unconditionally would
                    // silently report success even when the chain failed.
                    IsEstimated = BayesianAnalysis.IsEstimated;
                    if (IsEstimated)
                    {
                        // Apply the selected uncertainty method; a method that cannot be applied throws
                        // and the run is reported as failed (no silent fallback to the posterior).
                        await ApplyUncertaintyMethodAsync(progressReporter);
                        AnalysisProgress.ReportComplete(progressReporter);
                    }
                }
                catch (OperationCanceledException)
                {
                    wasCanceled = true;
                    IsEstimated = false;
                }
                catch (Exception ex)
                {
                    error = ex;
                    IsEstimated = false;
                }
                finally
                {
                    progressReporter?.IndicateTaskEnded();

                    bool succeeded = IsEstimated && !wasCanceled && error == null;

                    OnAnalysisCompleted(new AnalysisRunCompletedEventArgs(
                        wasCanceled: wasCanceled,
                        succeeded: succeeded,
                        error: error));
                }
            }
            finally
            {
                _reprocessGate.Release();
            }
        }

        /// <inheritdoc/>
        public override void CancelAnalysis()
        {
            base.CancelAnalysis();
            BayesianAnalysis.CancelSimulation();
        }

        /// <summary>
        /// Rebuilds the site results and the regional results from the current posterior: the
        /// reprocessor of ordinate changes, also used by deterministic tests with injected results.
        /// </summary>
        internal async Task RebuildPosteriorResultsAsync()
        {
            await CreateSiteResultsAsync();
            await CreateUncertaintyAnalysisResultsAsync();
        }

        /// <summary>
        /// Creates site-specific results from the Bayesian posterior samples.
        /// </summary>
        private async Task CreateSiteResultsAsync()
        {
            if (BayesianAnalysis == null || BayesianAnalysis.IsEstimated == false || BayesianAnalysis.Results == null)
            {
                return;
            }

            MCMCResults results = BayesianAnalysis.Results;
            int drawCount = Math.Min(BayesianAnalysis.OutputLength, results.Output.Count);
            await CreateSiteResultsFromDrawsAsync(
                index => results.Output[index].Values,
                drawCount,
                results.MAP.Values,
                SpatialGEVUncertaintyMethod.BayesianPosterior);
        }

        /// <summary>
        /// Builds the site results and the per-draw regional mean quantiles from a parameter draw set.
        /// </summary>
        /// <param name="draw">Returns the parameter vector of a draw by index.</param>
        /// <param name="drawCount">The number of draws.</param>
        /// <param name="pointEstimate">The parameter vector of the point (mode) curves.</param>
        /// <param name="method">The uncertainty method recorded on every site result.</param>
        /// <remarks>
        /// The draws are the retained posterior sample for the Bayesian methods and seeded Gaussian draws
        /// from the Godambe sandwich covariance for <see cref="SpatialGEVUncertaintyMethod.GodambeSandwich"/>.
        /// For every probability the regional mean of the site quantiles is stored per draw so the regional
        /// curve can report the posterior summaries of that statistic.
        /// </remarks>
        private async Task CreateSiteResultsFromDrawsAsync(Func<int, double[]> draw, int drawCount, double[] pointEstimate, SpatialGEVUncertaintyMethod method)
        {
            await Task.Run(() =>
            {
                int nSites = SpatialGEV.Sites;
                var siteResults = new SpatialGEVSiteResults[nSites];

                double alpha = 1 - BayesianAnalysis.CredibleIntervalWidth;
                var probs = ProbabilityOrdinates.ToArray();
                int nProbs = probs.Length;
                var regionalDraws = new double[nProbs, drawCount];

                for (int j = 0; j < nSites; j++)
                {
                    siteResults[j] = new SpatialGEVSiteResults
                    {
                        SiteIndex = j,
                        Coordinate = new double[] { SpatialGEV.Coordinates[j, 0], SpatialGEV.Coordinates[j, 1] },
                        UncertaintyMethod = method
                    };

                    // Arrays for GEV parameters across draws
                    var xiVals = new double[drawCount];
                    var alphaVals = new double[drawCount];
                    var kappaVals = new double[drawCount];

                    // Arrays for quantiles at each probability
                    var quantiles = new double[nProbs, drawCount];

                    // Compute for each draw
                    Parallel.For(0, drawCount, AnalysisProgress.CreateParallelOptions(), idx =>
                    {
                        var tempModel = (SpatialGEV)SpatialGEV.Clone();
                        tempModel.SetParameterValues(draw(idx));

                        var gevParams = tempModel.GetGEVParameters(j);
                        xiVals[idx] = gevParams[0];
                        alphaVals[idx] = gevParams[1];
                        kappaVals[idx] = gevParams[2];

                        // Compute quantiles
                        for (int p = 0; p < nProbs; p++)
                        {
                            quantiles[p, idx] = tempModel.InverseCDF(1 - probs[p], j);
                        }
                    });

                    // Regional mean quantile of every draw (accumulated over the sites)
                    for (int p = 0; p < nProbs; p++)
                    {
                        for (int idx = 0; idx < drawCount; idx++)
                            regionalDraws[p, idx] += quantiles[p, idx] / nSites;
                    }

                    // Compute summary statistics for GEV parameters
                    Array.Sort(xiVals);
                    Array.Sort(alphaVals);
                    Array.Sort(kappaVals);

                    siteResults[j].LocationMean = Statistics.ParallelMean(xiVals);
                    siteResults[j].LocationLower = Statistics.Percentile(xiVals, alpha / 2d, true);
                    siteResults[j].LocationUpper = Statistics.Percentile(xiVals, 1 - alpha / 2d, true);

                    siteResults[j].ScaleMean = Statistics.ParallelMean(alphaVals);
                    siteResults[j].ScaleLower = Statistics.Percentile(alphaVals, alpha / 2d, true);
                    siteResults[j].ScaleUpper = Statistics.Percentile(alphaVals, 1 - alpha / 2d, true);

                    siteResults[j].ShapeMean = Statistics.ParallelMean(kappaVals);
                    siteResults[j].ShapeLower = Statistics.Percentile(kappaVals, alpha / 2d, true);
                    siteResults[j].ShapeUpper = Statistics.Percentile(kappaVals, 1 - alpha / 2d, true);

                    // Compute quantile curves
                    siteResults[j].Probabilities = probs;
                    siteResults[j].QuantileMean = new double[nProbs];
                    siteResults[j].QuantileLower = new double[nProbs];
                    siteResults[j].QuantileUpper = new double[nProbs];
                    siteResults[j].QuantileMode = new double[nProbs];

                    // Point estimate using the supplied parameter vector
                    SpatialGEV.SetParameterValues(pointEstimate);

                    for (int p = 0; p < nProbs; p++)
                    {
                        var qVals = quantiles.GetRow(p);
                        Array.Sort(qVals);
                        siteResults[j].QuantileMean[p] = Statistics.ParallelMean(qVals);
                        siteResults[j].QuantileLower[p] = Statistics.Percentile(qVals, alpha / 2d, true);
                        siteResults[j].QuantileUpper[p] = Statistics.Percentile(qVals, 1 - alpha / 2d, true);
                        siteResults[j].QuantileMode[p] = SpatialGEV.InverseCDF(1 - probs[p], j);
                    }
                }

                _regionalQuantileDraws = regionalDraws;
                SiteResults = siteResults;
            });

            RaisePropertyChange(nameof(SiteResults));
        }

        /// <summary>
        /// Creates the regional uncertainty analysis results: the posterior summaries of the per-draw
        /// regional mean quantile (mean curve and equal-tailed bounds), the regional mean of the site point
        /// curves (mode curve), and the information criteria.
        /// </summary>
        /// <remarks>
        /// The regional bounds are posterior quantiles of the regional statistic computed within each draw,
        /// so cross-site posterior dependence is retained; they are not averages of the site interval
        /// endpoints. The mean curve equals the regional mean of the site posterior-mean quantiles because
        /// the mean is linear.
        /// </remarks>
        private async Task CreateUncertaintyAnalysisResultsAsync()
        {
            AnalysisResults = null;
            if (BayesianAnalysis == null || BayesianAnalysis.IsEstimated == false ||
                BayesianAnalysis.Results == null || SiteResults == null)
            {
                RaisePropertyChange(nameof(AnalysisResults));
                return;
            }

            // Capture local references to avoid null warnings in closure
            var siteResults = SiteResults;
            var regionalDraws = _regionalQuantileDraws;

            await Task.Run(() =>
            {
                var probs = ProbabilityOrdinates.ToArray();
                int n = probs.Length;
                double alpha = 1 - BayesianAnalysis.CredibleIntervalWidth;

                AnalysisResults = new UncertaintyAnalysisResults();
                AnalysisResults.ModeCurve = new double[n];
                AnalysisResults.MeanCurve = new double[n];
                AnalysisResults.ConfidenceIntervals = new double[n, 3];

                bool drawsAvailable = regionalDraws != null && regionalDraws.GetLength(0) == n && regionalDraws.GetLength(1) > 0;
                for (int p = 0; p < n; p++)
                {
                    double sumMode = 0;
                    for (int j = 0; j < SpatialGEV.Sites; j++)
                        sumMode += siteResults[j].QuantileMode[p];
                    AnalysisResults.ModeCurve[p] = sumMode / SpatialGEV.Sites;
                    AnalysisResults.ConfidenceIntervals[p, 0] = probs[p];

                    if (drawsAvailable)
                    {
                        int drawCount = regionalDraws!.GetLength(1);
                        var values = new double[drawCount];
                        for (int idx = 0; idx < drawCount; idx++)
                            values[idx] = regionalDraws[p, idx];
                        AnalysisResults.MeanCurve[p] = Statistics.ParallelMean(values);
                        Array.Sort(values);
                        AnalysisResults.ConfidenceIntervals[p, 1] = Statistics.Percentile(values, alpha / 2d, true);
                        AnalysisResults.ConfidenceIntervals[p, 2] = Statistics.Percentile(values, 1 - alpha / 2d, true);
                    }
                    else
                    {
                        // No draw set behind the current site results: report the regional mean of the site
                        // posterior means and leave the bounds undefined rather than averaging endpoints.
                        double sumMean = 0;
                        for (int j = 0; j < SpatialGEV.Sites; j++)
                            sumMean += siteResults[j].QuantileMean[p];
                        AnalysisResults.MeanCurve[p] = sumMean / SpatialGEV.Sites;
                        AnalysisResults.ConfidenceIntervals[p, 1] = double.NaN;
                        AnalysisResults.ConfidenceIntervals[p, 2] = double.NaN;
                    }
                }

                // AIC/BIC use the observation log likelihood at MAP with one nonempty row/year
                // block per BIC observation (see ComputeInformationCriteria).
                var (aic, bic, _) = ComputeInformationCriteria(SpatialGEV, BayesianAnalysis.Results.MAP.Values);
                AnalysisResults.AIC = aic;
                AnalysisResults.BIC = bic;
                AnalysisResults.DIC = BayesianAnalysis.DIC;
            });

            RaisePropertyChange(nameof(AnalysisResults));
        }

        /// <summary>
        /// Applies the selected <see cref="UncertaintyMethod"/> to the freshly built posterior results and
        /// records the method on the analysis and on every site result.
        /// </summary>
        /// <param name="progressReporter">Optional progress reporter for the bootstrap replicates.</param>
        /// <exception cref="InvalidOperationException">Thrown when the Godambe covariance is unavailable or the
        /// bootstrap does not reach its minimum success fraction; the run is then reported as failed.</exception>
        internal async Task ApplyUncertaintyMethodAsync(SafeProgressReporter? progressReporter)
        {
            switch (UncertaintyMethod)
            {
                case SpatialGEVUncertaintyMethod.BayesianPosterior:
                    break;
                case SpatialGEVUncertaintyMethod.BayesianInflated:
                    InflatePosteriorCovariance();
                    InflateRegionalIntervals();
                    break;
                case SpatialGEVUncertaintyMethod.GodambeSandwich:
                    await CreateGodambeSiteResultsAsync();
                    await CreateUncertaintyAnalysisResultsAsync();
                    break;
                case SpatialGEVUncertaintyMethod.SpatialBootstrap:
                    await RunSpatialBootstrapAsync(BootstrapReplicates, BootstrapBlockSize, progressReporter);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported uncertainty method {UncertaintyMethod}.");
            }

            AppliedUncertaintyMethod = UncertaintyMethod;
            if (SiteResults != null)
            {
                foreach (var site in SiteResults)
                    site.UncertaintyMethod = UncertaintyMethod;
            }
            RaisePropertyChange(nameof(AppliedUncertaintyMethod));
        }

        /// <summary>
        /// Widens the regional credible bounds around the regional mean curve by the square root of the
        /// variance inflation factor, matching <see cref="InflatePosteriorCovariance"/> for the site results.
        /// </summary>
        private void InflateRegionalIntervals()
        {
            if (AnalysisResults?.ConfidenceIntervals == null || AnalysisResults.MeanCurve == null)
                return;
            double sqrtVIF = Math.Sqrt(VarianceInflationFactor);
            for (int p = 0; p < AnalysisResults.MeanCurve.Length; p++)
            {
                double mid = AnalysisResults.MeanCurve[p];
                double halfWidth = (AnalysisResults.ConfidenceIntervals[p, 2] - AnalysisResults.ConfidenceIntervals[p, 1]) / 2;
                AnalysisResults.ConfidenceIntervals[p, 1] = mid - halfWidth * sqrtVIF;
                AnalysisResults.ConfidenceIntervals[p, 2] = mid + halfWidth * sqrtVIF;
            }
            RaisePropertyChange(nameof(AnalysisResults));
        }

        /// <summary>
        /// Builds the site results from seeded Gaussian parameter draws N(MAP, Σ) with the Godambe sandwich
        /// covariance Σ at the MAP, propagated through the same site-result machinery as the posterior.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the covariance is unavailable or not positive definite.</exception>
        /// <remarks>
        /// Draws are truncated to the parameter bounds coordinate by coordinate. The number of draws is
        /// <see cref="BayesianAnalysis"/>'s <c>OutputLength</c> and the generator is seeded with its
        /// <c>PRNGSeed</c>, so the results are reproducible.
        /// </remarks>
        private async Task CreateGodambeSiteResultsAsync()
        {
            double[] map = BayesianAnalysis.Results!.MAP.Values;
            double[,]? covariance = ComputeGodambeCovariance(map);
            if (covariance == null)
                throw new InvalidOperationException("The Godambe sandwich covariance is unavailable: " + GodambeCovarianceDiagnostic);
            var L = FactorGodambeCovariance(covariance);

            int k = map.Length;
            int drawCount = BayesianAnalysis.OutputLength;
            var prng = new MersenneTwister(BayesianAnalysis.PRNGSeed);
            var draws = new double[drawCount][];
            var z = new double[k];
            for (int d = 0; d < drawCount; d++)
            {
                for (int i = 0; i < k; i++)
                    z[i] = Normal.StandardZ(prng.NextDouble());
                var theta = new double[k];
                for (int i = 0; i < k; i++)
                {
                    double value = map[i];
                    for (int c = 0; c <= i; c++)
                        value += L[i, c] * z[c];
                    var parameter = SpatialGEV.Parameters[i];
                    theta[i] = Math.Min(Math.Max(value, parameter.LowerBound), parameter.UpperBound);
                }
                draws[d] = theta;
            }

            await CreateSiteResultsFromDrawsAsync(index => draws[index], drawCount, map, SpatialGEVUncertaintyMethod.GodambeSandwich);
        }

        /// <summary>
        /// Factors a Godambe sandwich covariance for Gaussian parameter draws.
        /// </summary>
        /// <param name="covariance">The Godambe sandwich covariance matrix.</param>
        /// <returns>The lower-triangular Cholesky factor.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the covariance is not positive definite.
        /// </exception>
        internal static Matrix FactorGodambeCovariance(double[,] covariance)
        {
            try
            {
                return new CholeskyDecomposition(new Matrix(covariance)).L;
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    "The Godambe sandwich covariance is not positive definite; Gaussian parameter draws are unavailable.",
                    exception);
            }
        }

        /// <summary>
        /// Computes the AIC and BIC of the spatial model from the observation log likelihood at the
        /// supplied parameter vector, counting one nonempty row/year block per BIC observation.
        /// </summary>
        /// <param name="model">The spatial GEV model.</param>
        /// <param name="parameters">The parameter vector (the MAP estimate in production).</param>
        /// <returns>The AIC, the BIC (NaN when no row has data), and the number of nonempty row/year blocks.</returns>
        /// <remarks>
        /// Each row/year is one multivariate observation whose sites are contemporaneously dependent, so
        /// the BIC sample size is the number of rows with at least one observed site rather than the
        /// number of site cells; fully missing rows contribute nothing to the likelihood and are not
        /// counted. The observation log likelihood excludes the latent-error process densities, which are
        /// prior structure.
        /// </remarks>
        internal static (double AIC, double BIC, int ObservationBlocks) ComputeInformationCriteria(SpatialGEV model, double[] parameters)
        {
            double logLikelihood = model.DataLogLikelihood(parameters);
            int observationBlocks = 0;
            for (int observation = 0; observation < model.Observations; observation++)
            {
                for (int site = 0; site < model.Sites; site++)
                {
                    if (!double.IsNaN(model.AtSiteData[observation, site]))
                    {
                        observationBlocks++;
                        break;
                    }
                }
            }

            double aic = GoodnessOfFit.AIC(model.NumberOfParameters, logLikelihood);
            double bic = observationBlocks > 0
                ? GoodnessOfFit.BIC(observationBlocks, model.NumberOfParameters, logLikelihood)
                : double.NaN;
            return (aic, bic, observationBlocks);
        }

        /// <summary>
        /// Updates the point estimate results using the current <see cref="BayesianAnalysis.PointEstimator"/>.
        /// </summary>
        public async Task UpdatePointEstimateResultsAsync()
        {
            if (BayesianAnalysis == null || BayesianAnalysis.IsEstimated == false ||
                BayesianAnalysis.Results == null || SiteResults == null)
            {
                return;
            }

            await Task.Run(() =>
            {
                double[] parameters;
                if (BayesianAnalysis.PointEstimator == BayesianAnalysis.PointEstimateType.PosteriorMean)
                {
                    parameters = BayesianAnalysis.Results.PosteriorMean.Values;
                }
                else
                {
                    parameters = BayesianAnalysis.Results.MAP.Values;
                }
                SpatialGEV.SetParameterValues(parameters);

                var probs = ProbabilityOrdinates.ToArray();

                // Update mode curves for each site
                for (int j = 0; j < SpatialGEV.Sites; j++)
                {
                    var gevParams = SpatialGEV.GetGEVParameters(j);
                    for (int p = 0; p < probs.Length; p++)
                    {
                        SiteResults[j].QuantileMode[p] = SpatialGEV.InverseCDF(1 - probs[p], j);
                    }
                }

                // Update regional mode curve
                if (AnalysisResults?.ModeCurve != null)
                {
                    for (int p = 0; p < probs.Length; p++)
                    {
                        double sum = 0;
                        for (int j = 0; j < SpatialGEV.Sites; j++)
                            sum += SiteResults[j].QuantileMode[p];
                        AnalysisResults.ModeCurve[p] = sum / SpatialGEV.Sites;
                    }
                }
            });

            RaisePropertyChange(nameof(SiteResults));
            RaisePropertyChange(nameof(AnalysisResults));
        }

        /// <summary>
        /// Computes quantiles for a specific site with uncertainty bounds.
        /// </summary>
        /// <param name="siteIndex">The site index (0-based).</param>
        /// <param name="exceedanceProbabilities">Array of exceedance probabilities.</param>
        /// <returns>
        /// A tuple containing arrays of (quantile mean, lower bound, upper bound) at each probability.
        /// </returns>
        /// <exception cref="InvalidOperationException">Thrown if the analysis has not been run.</exception>
        public (double[] Mean, double[] Lower, double[] Upper) GetSiteQuantiles(int siteIndex, double[] exceedanceProbabilities)
        {
            if (!IsEstimated || BayesianAnalysis?.Results == null)
                throw new InvalidOperationException("Analysis must be run before computing quantiles.");

            if (siteIndex < 0 || siteIndex >= SpatialGEV.Sites)
                throw new ArgumentOutOfRangeException(nameof(siteIndex));

            var realz = BayesianAnalysis.OutputLength;
            double alpha = 1 - BayesianAnalysis.CredibleIntervalWidth;
            int nProbs = exceedanceProbabilities.Length;

            var mean = new double[nProbs];
            var lower = new double[nProbs];
            var upper = new double[nProbs];

            var quantiles = new double[nProbs, realz];

            Parallel.For(0, realz, AnalysisProgress.CreateParallelOptions(), idx =>
            {
                var tempModel = (SpatialGEV)SpatialGEV.Clone();
                tempModel.SetParameterValues(BayesianAnalysis.Results.Output[idx].Values);

                for (int p = 0; p < nProbs; p++)
                {
                    quantiles[p, idx] = tempModel.InverseCDF(1 - exceedanceProbabilities[p], siteIndex);
                }
            });

            for (int p = 0; p < nProbs; p++)
            {
                var qVals = quantiles.GetRow(p);
                Array.Sort(qVals);
                mean[p] = Statistics.ParallelMean(qVals);
                lower[p] = Statistics.Percentile(qVals, alpha / 2d, true);
                upper[p] = Statistics.Percentile(qVals, 1 - alpha / 2d, true);
            }

            return (mean, lower, upper);
        }

        /// <summary>
        /// Predicts GEV parameters and quantiles at an ungauged location using spatial interpolation.
        /// </summary>
        /// <param name="coordinates">The coordinates of the ungauged location in the model's distance metric:
        /// [X, Y] for Cartesian or [latitude, longitude] in decimal degrees for geodesic.</param>
        /// <param name="covariates">The covariate values at the ungauged location, applied to every trend
        /// model that has covariates (the location, scale, and shape trends must then share the covariate
        /// definition); null is accepted only when no trend model has covariates.</param>
        /// <param name="exceedanceProbabilities">Array of exceedance probabilities for quantile estimation.</param>
        /// <returns>
        /// A <see cref="SpatialGEVSiteResults"/> object containing predicted GEV parameters and quantile curves.
        /// </returns>
        /// <exception cref="InvalidOperationException">Thrown if the analysis has not been run.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="coordinates"/> is not a two-element
        /// array, or when a trend model has covariates and <paramref name="covariates"/> is null, empty, or
        /// of the wrong length.</exception>
        /// <remarks>
        /// <para>
        /// This method uses the posterior samples to propagate uncertainty to the ungauged location.
        /// The prediction uses the trend surface evaluated at the provided coordinates and covariates.
        /// If spatial regression errors are enabled, an inverse-distance interpolation of the sampled
        /// latent errors is used for every posterior draw.
        /// </para>
        /// </remarks>
        public SpatialGEVSiteResults PredictAtUngaugedLocation(double[] coordinates, double[]? covariates, double[] exceedanceProbabilities)
        {
            if (!IsEstimated || BayesianAnalysis?.Results == null)
                throw new InvalidOperationException("Analysis must be run before predicting at ungauged locations.");

            if (coordinates == null || coordinates.Length != 2)
                throw new ArgumentException("Coordinates must be a 2-element array [X, Y].", nameof(coordinates));

            return PredictFromPosterior(
                SpatialGEV,
                BayesianAnalysis.Results,
                BayesianAnalysis.OutputLength,
                1 - BayesianAnalysis.CredibleIntervalWidth,
                coordinates,
                covariates,
                covariates,
                covariates,
                exceedanceProbabilities,
                SampleConditionalResidual,
                BayesianAnalysis.PRNGSeed);
        }

        /// <summary>
        /// Predicts the GEV parameters and quantiles at a location from a posterior sample of a spatial
        /// model: the trend surfaces evaluated at the supplied covariates, the conditional Gaussian-process
        /// prediction of every enabled latent error for each draw (optionally with a seeded conditional
        /// residual), and posterior summaries of the parameters and quantiles.
        /// </summary>
        /// <param name="model">The fitted model (the analysis model or a cross-validation fold's reduced model).</param>
        /// <param name="results">The posterior sample of <paramref name="model"/>.</param>
        /// <param name="outputLength">The number of retained draws to use (capped at the sample size).</param>
        /// <param name="alpha">One minus the credible-interval width.</param>
        /// <param name="coordinates">The location [X, Y].</param>
        /// <param name="locationCovariates">The location-trend covariate values, or null for an intercept-only trend.</param>
        /// <param name="scaleCovariates">The scale-trend covariate values, or null for an intercept-only trend.</param>
        /// <param name="shapeCovariates">The shape-trend covariate values, or null for an intercept-only trend.</param>
        /// <param name="exceedanceProbabilities">The exceedance probabilities of the quantile curve.</param>
        /// <param name="sampleConditionalResidual">Whether each draw adds a conditional Gaussian-process residual to the conditional mean.</param>
        /// <param name="seed">The seed of the residual draws.</param>
        /// <returns>The site results with <c>SiteIndex = -1</c>.</returns>
        /// <exception cref="ArgumentException">Thrown when a trend model has covariates and its covariate
        /// values are null, empty, or of the wrong length.</exception>
        /// <remarks>
        /// For every draw the latent error at the location is the simple-kriging conditional mean
        /// <c>k*ᵀK⁻¹ε</c> of that draw's error model (<see cref="SpatialRegressionErrors.GetKrigingPrediction"/>),
        /// plus, when requested, a residual drawn from N(0, σ² − k*ᵀK⁻¹k*) with standard-normal scores
        /// generated sequentially from the seed before the parallel loop, so the prediction is reproducible.
        /// </remarks>
        private static SpatialGEVSiteResults PredictFromPosterior(
            SpatialGEV model,
            MCMCResults results,
            int outputLength,
            double alpha,
            double[] coordinates,
            double[]? locationCovariates,
            double[]? scaleCovariates,
            double[]? shapeCovariates,
            double[] exceedanceProbabilities,
            bool sampleConditionalResidual,
            int seed)
        {
            ValidateCovariateVector(model.Location, locationCovariates, "location");
            ValidateCovariateVector(model.Scale, scaleCovariates, "scale");
            ValidateCovariateVector(model.Shape, shapeCovariates, "shape");

            int realz = Math.Min(outputLength, results.Output.Count);
            int nProbs = exceedanceProbabilities.Length;

            var result = new SpatialGEVSiteResults
            {
                SiteIndex = -1, // Ungauged
                Coordinate = coordinates
            };

            // Arrays for GEV parameters
            var xiVals = new double[realz];
            var alphaVals = new double[realz];
            var kappaVals = new double[realz];
            var quantiles = new double[nProbs, realz];

            // Standard-normal scores of the conditional residuals (location, scale, shape) per draw,
            // generated sequentially so the parallel evaluation is reproducible.
            var residualScores = new double[realz, 3];
            if (sampleConditionalResidual)
            {
                var prng = new MersenneTwister(seed);
                for (int idx = 0; idx < realz; idx++)
                {
                    for (int field = 0; field < 3; field++)
                        residualScores[idx, field] = Normal.StandardZ(prng.NextDouble());
                }
            }

            Parallel.For(0, realz, AnalysisProgress.CreateParallelOptions(), idx =>
            {
                var tempModel = (SpatialGEV)model.Clone();
                tempModel.SetParameterValues(results.Output[idx].Values);

                // Evaluate trend at ungauged location
                double xi = tempModel.Location.PredictWithCovariates(locationCovariates);
                double scl = tempModel.Scale.PredictWithCovariates(scaleCovariates);
                double kappa = tempModel.Shape.PredictWithCovariates(shapeCovariates);

                // Apply link functions
                if (tempModel.UseLogLinkForLocation)
                    xi = Math.Exp(xi);
                if (tempModel.UseLogLinkForScale)
                    scl = Math.Exp(scl);

                // Conditional Gaussian-process prediction of the enabled latent errors for this draw
                if (tempModel.UseLocationErrors && tempModel.LocationErrors != null)
                {
                    double error = ConditionalError(tempModel.LocationErrors, coordinates, sampleConditionalResidual, residualScores[idx, 0]);
                    if (tempModel.UseLogLinkForLocation)
                        xi *= Math.Exp(error);
                    else
                        xi += error;
                }

                if (tempModel.UseScaleErrors && tempModel.ScaleErrors != null)
                {
                    double error = ConditionalError(tempModel.ScaleErrors, coordinates, sampleConditionalResidual, residualScores[idx, 1]);
                    if (tempModel.UseLogLinkForScale)
                        scl *= Math.Exp(error);
                    else
                        scl = Math.Max(scl + error, Tools.DoubleMachineEpsilon);
                }

                if (tempModel.UseShapeErrors && tempModel.ShapeErrors != null)
                {
                    kappa += ConditionalError(tempModel.ShapeErrors, coordinates, sampleConditionalResidual, residualScores[idx, 2]);
                }

                xiVals[idx] = xi;
                alphaVals[idx] = scl;
                kappaVals[idx] = kappa;

                // Compute quantiles
                var gev = new GeneralizedExtremeValue(xi, scl, kappa);
                for (int p = 0; p < nProbs; p++)
                {
                    quantiles[p, idx] = gev.InverseCDF(1 - exceedanceProbabilities[p]);
                }
            });

            // Compute summary statistics
            Array.Sort(xiVals);
            Array.Sort(alphaVals);
            Array.Sort(kappaVals);

            result.LocationMean = Statistics.ParallelMean(xiVals);
            result.LocationLower = Statistics.Percentile(xiVals, alpha / 2d, true);
            result.LocationUpper = Statistics.Percentile(xiVals, 1 - alpha / 2d, true);

            result.ScaleMean = Statistics.ParallelMean(alphaVals);
            result.ScaleLower = Statistics.Percentile(alphaVals, alpha / 2d, true);
            result.ScaleUpper = Statistics.Percentile(alphaVals, 1 - alpha / 2d, true);

            result.ShapeMean = Statistics.ParallelMean(kappaVals);
            result.ShapeLower = Statistics.Percentile(kappaVals, alpha / 2d, true);
            result.ShapeUpper = Statistics.Percentile(kappaVals, 1 - alpha / 2d, true);

            result.Probabilities = exceedanceProbabilities;
            result.QuantileMean = new double[nProbs];
            result.QuantileLower = new double[nProbs];
            result.QuantileUpper = new double[nProbs];
            result.QuantileMode = new double[nProbs];

            for (int p = 0; p < nProbs; p++)
            {
                var qVals = quantiles.GetRow(p);
                Array.Sort(qVals);
                result.QuantileMean[p] = Statistics.ParallelMean(qVals);
                result.QuantileLower[p] = Statistics.Percentile(qVals, alpha / 2d, true);
                result.QuantileUpper[p] = Statistics.Percentile(qVals, 1 - alpha / 2d, true);
                result.QuantileMode[p] = Statistics.Percentile(qVals, 0.5);
            }

            return result;
        }

        /// <summary>
        /// Evaluates the latent error of one error model at a location for one draw: the simple-kriging
        /// conditional mean plus, when requested, a residual scaled by the conditional standard deviation.
        /// </summary>
        /// <param name="errors">The draw's error model (parameters already applied).</param>
        /// <param name="coordinates">The location [X, Y].</param>
        /// <param name="sampleConditionalResidual">Whether to add the conditional residual.</param>
        /// <param name="standardScore">The standard-normal score of the residual.</param>
        /// <returns>The latent error at the location.</returns>
        private static double ConditionalError(SpatialRegressionErrors errors, double[] coordinates, bool sampleConditionalResidual, double standardScore)
        {
            var (mean, variance) = errors.GetKrigingPrediction(coordinates);
            return sampleConditionalResidual ? mean + Math.Sqrt(Math.Max(variance, 0.0)) * standardScore : mean;
        }

        /// <summary>
        /// Validates the covariate values supplied for one trend model before a posterior prediction.
        /// </summary>
        /// <param name="trend">The trend model.</param>
        /// <param name="covariates">The covariate values, or null.</param>
        /// <param name="name">The trend name used in the message.</param>
        /// <exception cref="ArgumentException">Thrown when the trend has covariates and the values are missing or of the wrong length.</exception>
        private static void ValidateCovariateVector(GeneralLinearFunction trend, double[]? covariates, string name)
        {
            if (trend.NumberOfCovariates == 0)
                return;
            if (covariates == null || covariates.Length == 0)
            {
                throw new ArgumentException(
                    $"The {name} trend has {trend.NumberOfCovariates} covariate(s); covariate values at the prediction location are required.",
                    nameof(covariates));
            }
            if (covariates.Length != trend.NumberOfCovariates)
            {
                throw new ArgumentException(
                    $"The {name} trend expects {trend.NumberOfCovariates} covariate(s) but received {covariates.Length}.",
                    nameof(covariates));
            }
        }

        /// <summary>
        /// Extracts one site's covariate row from a trend model's stored covariate matrix.
        /// </summary>
        /// <param name="trend">The trend model.</param>
        /// <param name="site">The site index.</param>
        /// <returns>The covariate row, or null when the trend has no covariates.</returns>
        private static double[]? CovariateRow(GeneralLinearFunction trend, int site)
        {
            double[,]? covariates = trend.Covariates;
            if (covariates == null || covariates.GetLength(1) == 0)
                return null;
            var row = new double[covariates.GetLength(1)];
            for (int k = 0; k < row.Length; k++)
                row[k] = covariates[site, k];
            return row;
        }

        /// <summary>
        /// Runs leave-one-site-out cross-validation to assess model predictive performance.
        /// </summary>
        /// <param name="progressReporter">Optional progress reporter for tracking cross-validation progress.</param>
        /// <returns>
        /// A task that completes when cross-validation is finished.
        /// Results are available via the <see cref="CrossValidationResults"/> property.
        /// </returns>
        /// <exception cref="InvalidOperationException">Thrown when the analysis is not valid, or when no fold
        /// produces a prediction (an empty validation is never reported as a result).</exception>
        /// <remarks>
        /// <para>
        /// For each site, this method:
        /// 1. Builds the training model without the site (data column, coordinates, covariate row, copula
        ///    dimension, and latent error removed) through <c>SpatialGEV.CreateReducedModel</c>.
        /// 2. Fits it with a Bayesian analysis carrying this analysis's sampler settings and seed.
        /// 3. Predicts the quantiles at the held-out site from the fold posterior, using the site's own
        ///    covariate row for every covariate trend.
        /// 4. Compares the predictions to the site's at-site maximum-likelihood GEV quantiles.
        /// </para>
        /// <para>
        /// The analysis model and its posterior are never modified, so the results survive the run. A
        /// fold that cannot be scored is recorded in <see cref="SpatialGEVCrossValidationResults.FoldStatus"/>
        /// with NaN metrics and a message, and the aggregate metrics average the successful folds only.
        /// </para>
        /// <para>
        /// This provides an estimate of how well the model generalizes to ungauged locations.
        /// </para>
        /// </remarks>
        public async Task RunCrossValidationAsync(SafeProgressReporter? progressReporter = null)
        {
            if (Validate().IsValid == false)
                throw new InvalidOperationException("Model validation failed.");

            int sites = SpatialGEV.Sites;
            var results = new SpatialGEVCrossValidationResults
            {
                SitePredictionErrors = new double[sites],
                SiteRMSE = new double[sites],
                SiteBias = new double[sites],
                SiteCRPS = new double[sites], // CRPS not yet computed (always zero); see remarks on the property.
                FoldStatus = new SpatialGEVCrossValidationFoldStatus[sites],
                FoldMessages = new string[sites],
                TotalFolds = sites
            };
            for (int j = 0; j < sites; j++)
            {
                results.SitePredictionErrors[j] = double.NaN;
                results.SiteRMSE[j] = double.NaN;
                results.SiteBias[j] = double.NaN;
                results.FoldMessages[j] = string.Empty;
            }
            CrossValidationResults = null;

            var probs = new double[] { 0.5, 0.2, 0.1, 0.04, 0.02, 0.01 }; // T=2, 5, 10, 25, 50, 100
            var successfulErrors = new List<double>();
            var successfulBias = new List<double>();

            for (int j = 0; j < sites; j++)
            {
                progressReporter?.ReportProgress((int)(100.0 * j / sites));

                // Observed data at the held-out site
                var siteData = new List<double>();
                for (int i = 0; i < SpatialGEV.Observations; i++)
                {
                    if (!double.IsNaN(SpatialGEV.AtSiteData[i, j]))
                        siteData.Add(SpatialGEV.AtSiteData[i, j]);
                }
                if (siteData.Count == 0)
                {
                    results.FoldStatus[j] = SpatialGEVCrossValidationFoldStatus.NoObservations;
                    results.FoldMessages[j] = "The held-out site has no finite observation.";
                    continue;
                }

                // Training model without the held-out site, fitted with this analysis's settings and seed
                SpatialGEV fold;
                BayesianAnalysis foldBayes;
                try
                {
                    fold = SpatialGEV.CreateReducedModel(j);
                    var (foldValid, foldMessages) = fold.Validate();
                    if (!foldValid)
                    {
                        results.FoldStatus[j] = SpatialGEVCrossValidationFoldStatus.FitFailed;
                        results.FoldMessages[j] = "The reduced training model is not valid: " + string.Join(" ", foldMessages);
                        continue;
                    }
                    foldBayes = CreateFoldAnalysis(fold);
                    await foldBayes.RunAsync(null, false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    results.FoldStatus[j] = SpatialGEVCrossValidationFoldStatus.FitFailed;
                    results.FoldMessages[j] = "The reduced training model could not be fitted: " + ex.Message;
                    continue;
                }

                if (!foldBayes.IsEstimated || foldBayes.Results == null)
                {
                    results.FoldStatus[j] = SpatialGEVCrossValidationFoldStatus.FitFailed;
                    results.FoldMessages[j] = foldBayes.LastError != null
                        ? "The fold sampler failed: " + foldBayes.LastError.Message
                        : "The fold sampler did not produce an estimate.";
                    continue;
                }

                // Predict at the held-out site from the fold posterior with the site's own covariate rows
                try
                {
                    var coords = new double[] { SpatialGEV.Coordinates[j, 0], SpatialGEV.Coordinates[j, 1] };
                    SpatialGEVSiteResults prediction = PredictFromPosterior(
                        fold,
                        foldBayes.Results,
                        foldBayes.OutputLength,
                        1 - foldBayes.CredibleIntervalWidth,
                        coords,
                        CovariateRow(SpatialGEV.Location, j),
                        CovariateRow(SpatialGEV.Scale, j),
                        CovariateRow(SpatialGEV.Shape, j),
                        probs,
                        SampleConditionalResidual,
                        foldBayes.PRNGSeed);

                    // Compare to the at-site maximum-likelihood GEV of the held-out site
                    var gev = new GeneralizedExtremeValue();
                    gev.Estimate(siteData, ParameterEstimationMethod.MaximumLikelihood);
                    double obsQ100 = gev.InverseCDF(0.99);
                    double predQ100 = prediction.QuantileMean[5]; // T=100

                    double sumSqErr = 0;
                    for (int p = 0; p < probs.Length; p++)
                    {
                        double obsQ = gev.InverseCDF(1 - probs[p]);
                        double predQ = prediction.QuantileMean[p];
                        sumSqErr += (predQ - obsQ) * (predQ - obsQ);
                    }

                    double error = predQ100 - obsQ100;
                    double bias = (predQ100 - obsQ100) / obsQ100;
                    double rmse = Math.Sqrt(sumSqErr / probs.Length);
                    if (!Tools.IsFinite(error) || !Tools.IsFinite(bias) || !Tools.IsFinite(rmse))
                    {
                        results.FoldStatus[j] = SpatialGEVCrossValidationFoldStatus.PredictionFailed;
                        results.FoldMessages[j] = "The held-out prediction or its at-site comparison is not finite.";
                        continue;
                    }

                    results.SitePredictionErrors[j] = error;
                    results.SiteBias[j] = bias;
                    results.SiteRMSE[j] = rmse;
                    results.FoldStatus[j] = SpatialGEVCrossValidationFoldStatus.Succeeded;
                    successfulErrors.Add(error);
                    successfulBias.Add(bias);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    results.FoldStatus[j] = SpatialGEVCrossValidationFoldStatus.PredictionFailed;
                    results.FoldMessages[j] = "The held-out prediction failed: " + ex.Message;
                }
            }

            results.SuccessfulFolds = successfulErrors.Count;
            if (results.SuccessfulFolds == 0)
            {
                var reasons = new List<string>();
                for (int j = 0; j < sites; j++)
                    reasons.Add($"site {j + 1}: {results.FoldStatus[j]} - {results.FoldMessages[j]}");
                throw new InvalidOperationException(
                    "Leave-one-site-out cross-validation produced no successful fold. " + string.Join(" ", reasons));
            }

            // Aggregate metrics over the successful folds only
            results.MeanAbsoluteError = Statistics.ParallelMean(successfulErrors.Select(Math.Abs).ToArray());
            results.RootMeanSquareError = Math.Sqrt(Statistics.ParallelMean(successfulErrors.Select(e => e * e).ToArray()));
            results.MeanBias = Statistics.ParallelMean(successfulBias.ToArray());

            CrossValidationResults = results;
            progressReporter?.ReportProgress(100);
            RaisePropertyChange(nameof(CrossValidationResults));
        }

        /// <summary>
        /// Creates the Bayesian analysis of a cross-validation fold: the fold model with this analysis's
        /// sampler type, defaults policy, seed, interval width, output length, and point estimator, and its
        /// explicit iteration, chain, thinning, and tuning settings whenever the defaults are not in use.
        /// </summary>
        /// <param name="fold">The reduced training model.</param>
        /// <returns>The fold analysis, ready to run.</returns>
        /// <remarks>
        /// When the simulation defaults are in use they resolve against the fold model's own parameter
        /// count, exactly as a fresh analysis of that model would resolve them.
        /// </remarks>
        private BayesianAnalysis CreateFoldAnalysis(SpatialGEV fold)
        {
            var foldBayes = new BayesianAnalysis(fold)
            {
                UseSimulationDefaults = BayesianAnalysis.UseSimulationDefaults,
                UseAdvancedSimulationDefaults = BayesianAnalysis.UseAdvancedSimulationDefaults
            };
            foldBayes.Type = BayesianAnalysis.Type;
            foldBayes.PRNGSeed = BayesianAnalysis.PRNGSeed;
            foldBayes.CredibleIntervalWidth = BayesianAnalysis.CredibleIntervalWidth;
            foldBayes.OutputLength = BayesianAnalysis.OutputLength;
            foldBayes.PointEstimator = BayesianAnalysis.PointEstimator;

            if (!BayesianAnalysis.UseSimulationDefaults)
            {
                foldBayes.NumberOfChains = BayesianAnalysis.NumberOfChains;
                foldBayes.ThinningInterval = BayesianAnalysis.ThinningInterval;
                foldBayes.WarmupIterations = BayesianAnalysis.WarmupIterations;
                foldBayes.Iterations = BayesianAnalysis.Iterations;
                foldBayes.InitialIterations = BayesianAnalysis.InitialIterations;
            }

            if (!BayesianAnalysis.UseAdvancedSimulationDefaults)
            {
                foldBayes.Jump = BayesianAnalysis.Jump;
                foldBayes.JumpThreshold = BayesianAnalysis.JumpThreshold;
                foldBayes.SnookerThreshold = BayesianAnalysis.SnookerThreshold;
                foldBayes.Noise = BayesianAnalysis.Noise;
                foldBayes.Scale = BayesianAnalysis.Scale;
                foldBayes.Beta = BayesianAnalysis.Beta;
                foldBayes.MaxTreeDepth = BayesianAnalysis.MaxTreeDepth;
            }

            return foldBayes;
        }

        /// <summary>
        /// Computes the regional growth curve (dimensionless frequency curve) based on index flood methodology.
        /// </summary>
        /// <param name="exceedanceProbabilities">Array of exceedance probabilities.</param>
        /// <returns>
        /// A tuple containing the exceedance probabilities and the growth factors (quantile / index flood).
        /// </returns>
        /// <remarks>
        /// <para>
        /// The regional growth curve represents quantiles normalized by the index flood (typically the median
        /// or mean annual flood). This allows comparison of flood frequency characteristics across sites
        /// with different magnitudes.
        /// </para>
        /// </remarks>
        public (double[] Probabilities, double[] GrowthFactors, double[] Lower, double[] Upper) GetRegionalGrowthCurve(double[] exceedanceProbabilities)
        {
            if (!IsEstimated || SiteResults == null)
                throw new InvalidOperationException("Analysis must be run before computing growth curves.");

            int nProbs = exceedanceProbabilities.Length;
            var growthMean = new double[nProbs];
            var growthLower = new double[nProbs];
            var growthUpper = new double[nProbs];

            // Compute growth factors for each site, then average
            for (int j = 0; j < SpatialGEV.Sites; j++)
            {
                // Index flood = median (T=2)
                int medianIdx = Array.IndexOf(SiteResults[j].Probabilities, 0.5);
                double indexFlood;
                if (medianIdx >= 0 && medianIdx < SiteResults[j].QuantileMean.Length)
                {
                    indexFlood = SiteResults[j].QuantileMean[medianIdx];
                }
                else
                {
                    // Fallback: use first quantile if median not found
                    indexFlood = SiteResults[j].QuantileMean.Length > 0 ? SiteResults[j].QuantileMean[0] : 1.0;
                }
                if (indexFlood <= 0) indexFlood = SiteResults[j].QuantileMean.Length > 0 ? SiteResults[j].QuantileMean[0] : 1.0;

                for (int p = 0; p < nProbs; p++)
                {
                    // Find or interpolate quantile at this probability
                    var (mean, lower, upper) = GetSiteQuantiles(j, new double[] { exceedanceProbabilities[p] });
                    growthMean[p] += mean[0] / indexFlood;
                    growthLower[p] += lower[0] / indexFlood;
                    growthUpper[p] += upper[0] / indexFlood;
                }
            }

            // Average across sites
            for (int p = 0; p < nProbs; p++)
            {
                growthMean[p] /= SpatialGEV.Sites;
                growthLower[p] /= SpatialGEV.Sites;
                growthUpper[p] /= SpatialGEV.Sites;
            }

            return (exceedanceProbabilities, growthMean, growthLower, growthUpper);
        }

        /// <summary>
        /// Computes the Godambe (sandwich) covariance matrix for robust standard errors.
        /// </summary>
        /// <param name="parameters">The MLE or MAP parameter values. If null, uses the current MAP estimate.</param>
        /// <returns>
        /// The Godambe covariance matrix [nParams × nParams], or <c>null</c> when the computation fails;
        /// <see cref="GodambeCovarianceStatus"/> and <see cref="GodambeCovarianceDiagnostic"/> describe
        /// the outcome.
        /// </returns>
        /// <exception cref="InvalidOperationException">Thrown when <paramref name="parameters"/> is null
        /// and no MAP estimate is available.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="parameters"/> does not hold one
        /// value per model parameter.</exception>
        /// <remarks>
        /// <para>
        /// The Godambe sandwich estimator provides robust standard errors that account for
        /// model misspecification and correlation in the data:
        /// </para>
        /// <para>
        /// Var(θ̂) = H(θ)⁻¹ J(θ) H(θ)⁻¹
        /// </para>
        /// <para>
        /// where H is the sensitivity matrix (the central-difference Hessian of the observation log
        /// likelihood <see cref="SpatialGEV.DataLogLikelihood"/>) and J is the variability matrix (the
        /// sum of outer products of the row/year score vectors obtained from
        /// <see cref="SpatialGEV.PointwiseDataLogLikelihood"/>). The scalar log likelihood is the sum of
        /// the row/year terms, so both matrices derive from the same estimating equations; the latent
        /// error process densities, which are prior structure, enter neither matrix.
        /// </para>
        /// <para>
        /// A non-finite likelihood evaluation, a singular sensitivity matrix, or a non-finite or
        /// non-positive-variance result is a failure: the method returns <c>null</c>, clears
        /// <see cref="GodambeCovariance"/>, and reports <see cref="CovarianceComputationStatus.Failed"/>
        /// with a diagnostic. No substitute matrix is returned.
        /// </para>
        /// <para>
        ///     <b>References:</b>
        ///     - Godambe, V.P. (1960). An optimum property of regular maximum likelihood estimation.
        ///       Annals of Mathematical Statistics, 31(4), 1208-1211.
        ///     - Varin, C., Reid, N., Firth, D. (2011). An overview of composite likelihood methods.
        ///       Statistica Sinica, 21, 5-42.
        /// </para>
        /// </remarks>
        public double[,]? ComputeGodambeCovariance(double[]? parameters = null)
        {
            if (parameters == null)
            {
                if (BayesianAnalysis?.Results?.MAP == null)
                    throw new InvalidOperationException("Analysis must be run before computing Godambe covariance.");
                parameters = BayesianAnalysis.Results.MAP.Values;
            }

            if (parameters.Length != SpatialGEV.NumberOfParameters)
            {
                throw new ArgumentException(
                    $"Expected {SpatialGEV.NumberOfParameters} parameter values but got {parameters.Length}.",
                    nameof(parameters));
            }

            int nParams = parameters.Length;
            double eps = 1e-5;

            // Sensitivity matrix H: central-difference Hessian of the observation log likelihood.
            var H = new double[nParams, nParams];
            double f0 = SpatialGEV.DataLogLikelihood(parameters);
            if (!Tools.IsFinite(f0))
                return FailGodambeCovariance("The observation log likelihood is not finite at the evaluation point.");

            for (int i = 0; i < nParams; i++)
            {
                for (int j = i; j < nParams; j++)
                {
                    double hi = Math.Abs(parameters[i]) * eps + eps;
                    double hj = Math.Abs(parameters[j]) * eps + eps;

                    if (i == j)
                    {
                        // Diagonal: d²L/dθᵢ²
                        var pPlusI = (double[])parameters.Clone();
                        var pMinusI = (double[])parameters.Clone();
                        pPlusI[i] += hi;
                        pMinusI[i] -= hi;
                        double fPlusI = SpatialGEV.DataLogLikelihood(pPlusI);
                        double fMinusI = SpatialGEV.DataLogLikelihood(pMinusI);
                        H[i, i] = (fPlusI - 2 * f0 + fMinusI) / (hi * hi);
                    }
                    else
                    {
                        // Off-diagonal: d²L/dθᵢdθⱼ
                        var pPlusIJ = (double[])parameters.Clone();
                        var pMinusIJ = (double[])parameters.Clone();
                        var pPlusIMinusJ = (double[])parameters.Clone();
                        var pMinusIPlusJ = (double[])parameters.Clone();
                        pPlusIJ[i] += hi;
                        pPlusIJ[j] += hj;
                        pMinusIJ[i] -= hi;
                        pMinusIJ[j] -= hj;
                        pPlusIMinusJ[i] += hi;
                        pPlusIMinusJ[j] -= hj;
                        pMinusIPlusJ[i] -= hi;
                        pMinusIPlusJ[j] += hj;

                        double fPlusIJ = SpatialGEV.DataLogLikelihood(pPlusIJ);
                        double fMinusIJ = SpatialGEV.DataLogLikelihood(pMinusIJ);
                        double fPlusIMinusJ = SpatialGEV.DataLogLikelihood(pPlusIMinusJ);
                        double fMinusIPlusJ = SpatialGEV.DataLogLikelihood(pMinusIPlusJ);

                        H[i, j] = (fPlusIJ - fPlusIMinusJ - fMinusIPlusJ + fMinusIJ) / (4 * hi * hj);
                        H[j, i] = H[i, j];
                    }

                    if (!Tools.IsFinite(H[i, j]))
                    {
                        return FailGodambeCovariance(
                            $"The Hessian entry ({i + 1}, {j + 1}) of the observation log likelihood is not finite.");
                    }
                }
            }

            // Variability matrix J: sum over row/year blocks of the outer products of the score vectors,
            // each score obtained by central differences of the pointwise (row/year) log likelihood.
            int observationCount = SpatialGEV.Observations;
            var scores = new double[observationCount, nParams];
            for (int k = 0; k < nParams; k++)
            {
                var pPlus = (double[])parameters.Clone();
                var pMinus = (double[])parameters.Clone();
                double h = Math.Abs(parameters[k]) * eps + eps;
                pPlus[k] += h;
                pMinus[k] -= h;

                var llPlus = SpatialGEV.PointwiseDataLogLikelihood(pPlus);
                var llMinus = SpatialGEV.PointwiseDataLogLikelihood(pMinus);

                for (int obs = 0; obs < observationCount; obs++)
                {
                    scores[obs, k] = (llPlus[obs] - llMinus[obs]) / (2 * h);
                    if (!Tools.IsFinite(scores[obs, k]))
                    {
                        return FailGodambeCovariance(
                            $"The score of row {obs + 1} with respect to parameter {k + 1} is not finite.");
                    }
                }
            }

            var J = new double[nParams, nParams];
            for (int obs = 0; obs < observationCount; obs++)
            {
                for (int i = 0; i < nParams; i++)
                {
                    for (int j = 0; j < nParams; j++)
                    {
                        J[i, j] += scores[obs, i] * scores[obs, j];
                    }
                }
            }

            // Invert H; a singular sensitivity matrix leaves the sandwich undefined.
            var HInv = InvertMatrix(H);
            if (HInv == null)
            {
                return FailGodambeCovariance(
                    "The sensitivity matrix (Hessian of the observation log likelihood) is singular; the sandwich covariance is undefined.");
            }

            // Compute sandwich: H⁻¹ J H⁻¹
            var covariance = MultiplyMatrices(MultiplyMatrices(HInv, J), HInv);
            for (int i = 0; i < nParams; i++)
            {
                for (int j = 0; j < nParams; j++)
                {
                    if (!Tools.IsFinite(covariance[i, j]))
                        return FailGodambeCovariance($"The sandwich covariance entry ({i + 1}, {j + 1}) is not finite.");
                }

                if (covariance[i, i] <= 0)
                    return FailGodambeCovariance($"The sandwich covariance has a non-positive variance for parameter {i + 1}.");
            }

            GodambeCovariance = covariance;
            GodambeCovarianceStatus = CovarianceComputationStatus.Available;
            GodambeCovarianceDiagnostic = null;
            return covariance;
        }

        /// <summary>
        /// Records a failed Godambe covariance computation and returns <c>null</c>.
        /// </summary>
        /// <param name="diagnostic">The reason the computation failed.</param>
        /// <returns><c>null</c>, so callers can return the result directly.</returns>
        private double[,]? FailGodambeCovariance(string diagnostic)
        {
            GodambeCovariance = null;
            GodambeCovarianceStatus = CovarianceComputationStatus.Failed;
            GodambeCovarianceDiagnostic = diagnostic;
            System.Diagnostics.Debug.WriteLine($"SpatialGEVAnalysis.ComputeGodambeCovariance: {diagnostic}");
            return null;
        }

        /// <summary>
        /// Computes the variance inflation factor and applies it to inflate posterior variance.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When using independence likelihood with correlated data, the posterior variance
        /// is underestimated by a factor approximately equal to the variance inflation factor:
        /// </para>
        /// <para>
        /// VIF = 1 + (n_sites - 1) * ρ̄
        /// </para>
        /// <para>
        /// where ρ̄ is the average intersite correlation. This method computes the VIF and
        /// adjusts the site results accordingly.
        /// </para>
        /// <para>
        /// <b>Note:</b> This is a frequentist post-hoc correction. For proper Bayesian
        /// uncertainty, use copula dependence with spatial regression errors instead.
        /// </para>
        /// </remarks>
        /// <returns>The variance inflation factor.</returns>
        public double InflatePosteriorCovariance()
        {
            // Compute VIF from model
            VarianceInflationFactor = SpatialGEV.ComputeVarianceInflationFactor();

            if (SiteResults == null || !IsEstimated)
                return VarianceInflationFactor;

            double sqrtVIF = Math.Sqrt(VarianceInflationFactor);

            // Inflate credible intervals for each site
            foreach (var site in SiteResults)
            {
                // For GEV parameters: widen the CI by sqrt(VIF)
                double locMid = site.LocationMean;
                double locHalfWidth = (site.LocationUpper - site.LocationLower) / 2;
                site.LocationLower = locMid - locHalfWidth * sqrtVIF;
                site.LocationUpper = locMid + locHalfWidth * sqrtVIF;

                double sclMid = site.ScaleMean;
                double sclHalfWidth = (site.ScaleUpper - site.ScaleLower) / 2;
                site.ScaleLower = Math.Max(sclMid - sclHalfWidth * sqrtVIF, 0);
                site.ScaleUpper = sclMid + sclHalfWidth * sqrtVIF;

                double shpMid = site.ShapeMean;
                double shpHalfWidth = (site.ShapeUpper - site.ShapeLower) / 2;
                site.ShapeLower = shpMid - shpHalfWidth * sqrtVIF;
                site.ShapeUpper = shpMid + shpHalfWidth * sqrtVIF;

                // Inflate quantile CIs
                if (site.QuantileMean != null && site.QuantileLower != null && site.QuantileUpper != null)
                {
                    for (int p = 0; p < site.QuantileMean.Length; p++)
                    {
                        double qMid = site.QuantileMean[p];
                        double qHalfWidth = (site.QuantileUpper[p] - site.QuantileLower[p]) / 2;
                        site.QuantileLower[p] = qMid - qHalfWidth * sqrtVIF;
                        site.QuantileUpper[p] = qMid + qHalfWidth * sqrtVIF;
                    }
                }
            }

            RaisePropertyChange(nameof(SiteResults));
            RaisePropertyChange(nameof(VarianceInflationFactor));

            return VarianceInflationFactor;
        }

        /// <summary>
        /// Runs a temporal block bootstrap to compute empirical confidence intervals.
        /// </summary>
        /// <param name="nBootstrap">Number of bootstrap replicates. Default is 200.</param>
        /// <param name="blockSize">Number of consecutive rows (years) per resampled block. If 0, uses the cube root of the number of rows, rounded up.</param>
        /// <param name="progressReporter">Optional progress reporter.</param>
        /// <returns>
        /// A task that completes when bootstrap is finished. The percentile intervals replace the bounds of
        /// <see cref="SiteResults"/> and of the regional curve in <see cref="AnalysisResults"/>, and
        /// <see cref="BootstrapResults"/> records the replicate accounting.
        /// </returns>
        /// <exception cref="InvalidOperationException">Thrown when the analysis has not been run, or when fewer
        /// than half of the replicates produce a usable refit.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="nBootstrap"/> is not positive.</exception>
        /// <remarks>
        /// <para>
        /// Rows (years) are resampled with replacement in contiguous blocks, wrapping at the end of the record,
        /// while every site is kept in each resampled row, so the intersite dependence, coordinates, and
        /// covariate rows of the network are preserved. Each replicate model
        /// (<c>SpatialGEV.CreateResampledModel</c>) carries the full model's settings and priors and is
        /// refitted by maximum a posteriori estimation warm-started at the full-model MAP. Replicates whose
        /// refit fails or is not finite are excluded; at least half of the replicates must succeed.
        /// </para>
        /// <para>
        /// The resampling is seeded with <see cref="BayesianAnalysis"/>'s <c>PRNGSeed</c>, so the replicates
        /// are reproducible. The point estimates of the results are unchanged; only the interval bounds are
        /// replaced.
        /// </para>
        /// <para>
        ///     <b>References:</b>
        ///     - Lahiri, S.N. (2003). Resampling Methods for Dependent Data. Springer.
        ///     - Politis, D.N., Romano, J.P. (1994). The stationary bootstrap. JASA.
        /// </para>
        /// </remarks>
        public async Task RunSpatialBootstrapAsync(int nBootstrap = 200, int blockSize = 0, SafeProgressReporter? progressReporter = null)
        {
            if (!IsEstimated || BayesianAnalysis?.Results == null || SiteResults == null)
                throw new InvalidOperationException("Analysis must be run before bootstrap.");
            if (nBootstrap <= 0)
                throw new ArgumentOutOfRangeException(nameof(nBootstrap), "At least one bootstrap replicate is required.");

            int observations = SpatialGEV.Observations;
            if (blockSize <= 0)
                blockSize = Math.Max(1, (int)Math.Ceiling(Math.Pow(observations, 1.0 / 3.0)));
            blockSize = Math.Min(blockSize, observations);

            int seed = BayesianAnalysis.PRNGSeed;
            var prng = new MersenneTwister(seed);
            var probs = ProbabilityOrdinates.ToArray();
            int nProbs = probs.Length;
            int nSites = SpatialGEV.Sites;
            double[] fullMap = BayesianAnalysis.Results.MAP.Values;
            var siteResults = SiteResults;

            // Replicate statistics (NaN for a failed replicate)
            var bootQuantiles = new double[nSites, nProbs, nBootstrap];
            var bootLocation = new double[nSites, nBootstrap];
            var bootScale = new double[nSites, nBootstrap];
            var bootShape = new double[nSites, nBootstrap];
            var bootRegional = new double[nProbs, nBootstrap];
            int successful = 0;

            await Task.Run(() =>
            {
                for (int b = 0; b < nBootstrap; b++)
                {
                    progressReporter?.ReportProgress((int)(100.0 * b / nBootstrap));
                    int[] rows = BuildBlockBootstrapRows(observations, blockSize, prng);
                    bool usable = false;
                    try
                    {
                        SpatialGEV replicate = SpatialGEV.CreateResampledModel(rows);
                        replicate.SetParameterValues(fullMap);
                        var map = new MaximumAPosteriori(replicate, OptimizationMethod.DifferentialEvolution, fullMap)
                        {
                            ComputeHessian = false
                        };
                        if (map.Estimate() && map.IsEstimated)
                        {
                            replicate.SetParameterValues(map.BestParameterSet.Values);
                            usable = true;
                            for (int j = 0; j < nSites && usable; j++)
                            {
                                var gevParams = replicate.GetGEVParameters(j);
                                bootLocation[j, b] = gevParams[0];
                                bootScale[j, b] = gevParams[1];
                                bootShape[j, b] = gevParams[2];
                                if (!Tools.IsFinite(gevParams[0]) || !Tools.IsFinite(gevParams[1]) || !Tools.IsFinite(gevParams[2]))
                                    usable = false;
                                for (int p = 0; p < nProbs && usable; p++)
                                {
                                    bootQuantiles[j, p, b] = replicate.InverseCDF(1 - probs[p], j);
                                    if (!Tools.IsFinite(bootQuantiles[j, p, b]))
                                        usable = false;
                                }
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"SpatialGEVAnalysis.Bootstrap: replicate {b + 1} failed: {ex.Message}");
                        usable = false;
                    }

                    if (usable)
                    {
                        successful++;
                        for (int p = 0; p < nProbs; p++)
                        {
                            double sum = 0;
                            for (int j = 0; j < nSites; j++)
                                sum += bootQuantiles[j, p, b];
                            bootRegional[p, b] = sum / nSites;
                        }
                    }
                    else
                    {
                        for (int j = 0; j < nSites; j++)
                        {
                            bootLocation[j, b] = double.NaN;
                            bootScale[j, b] = double.NaN;
                            bootShape[j, b] = double.NaN;
                            for (int p = 0; p < nProbs; p++)
                                bootQuantiles[j, p, b] = double.NaN;
                        }
                        for (int p = 0; p < nProbs; p++)
                            bootRegional[p, b] = double.NaN;
                    }
                }
            });

            BootstrapResults = new SpatialGEVBootstrapResults
            {
                RequestedReplicates = nBootstrap,
                SuccessfulReplicates = successful,
                BlockSize = blockSize,
                Seed = seed,
                MinimumSuccessFraction = MinimumBootstrapSuccessFraction,
                Scheme = "Temporal block bootstrap: rows (years) resampled with replacement in contiguous blocks with wrap-around, all sites retained; maximum a posteriori refit per replicate warm-started at the full-model MAP"
            };
            RaisePropertyChange(nameof(BootstrapResults));

            int required = (int)Math.Ceiling(MinimumBootstrapSuccessFraction * nBootstrap);
            if (successful < required)
            {
                throw new InvalidOperationException(
                    $"The spatial bootstrap produced {successful} successful replicate(s) of {nBootstrap}; at least {required} are required for percentile intervals.");
            }

            // Percentile intervals over the successful replicates
            double alpha = 1 - BayesianAnalysis.CredibleIntervalWidth;
            for (int j = 0; j < nSites; j++)
            {
                var validLoc = Finite(bootLocation, j);
                var validScl = Finite(bootScale, j);
                var validShp = Finite(bootShape, j);
                siteResults[j].LocationLower = Statistics.Percentile(validLoc, alpha / 2, true);
                siteResults[j].LocationUpper = Statistics.Percentile(validLoc, 1 - alpha / 2, true);
                siteResults[j].ScaleLower = Statistics.Percentile(validScl, alpha / 2, true);
                siteResults[j].ScaleUpper = Statistics.Percentile(validScl, 1 - alpha / 2, true);
                siteResults[j].ShapeLower = Statistics.Percentile(validShp, alpha / 2, true);
                siteResults[j].ShapeUpper = Statistics.Percentile(validShp, 1 - alpha / 2, true);
                for (int p = 0; p < nProbs; p++)
                {
                    var validQ = new List<double>();
                    for (int b = 0; b < nBootstrap; b++)
                    {
                        if (!double.IsNaN(bootQuantiles[j, p, b]))
                            validQ.Add(bootQuantiles[j, p, b]);
                    }
                    validQ.Sort();
                    siteResults[j].QuantileLower[p] = Statistics.Percentile(validQ.ToArray(), alpha / 2, true);
                    siteResults[j].QuantileUpper[p] = Statistics.Percentile(validQ.ToArray(), 1 - alpha / 2, true);
                }
                siteResults[j].UncertaintyMethod = SpatialGEVUncertaintyMethod.SpatialBootstrap;
            }

            if (AnalysisResults?.ConfidenceIntervals != null)
            {
                for (int p = 0; p < nProbs && p < AnalysisResults.ConfidenceIntervals.GetLength(0); p++)
                {
                    var validR = new List<double>();
                    for (int b = 0; b < nBootstrap; b++)
                    {
                        if (!double.IsNaN(bootRegional[p, b]))
                            validR.Add(bootRegional[p, b]);
                    }
                    validR.Sort();
                    AnalysisResults.ConfidenceIntervals[p, 1] = Statistics.Percentile(validR.ToArray(), alpha / 2, true);
                    AnalysisResults.ConfidenceIntervals[p, 2] = Statistics.Percentile(validR.ToArray(), 1 - alpha / 2, true);
                }
            }

            RaisePropertyChange(nameof(SiteResults));
            RaisePropertyChange(nameof(AnalysisResults));
        }

        /// <summary>
        /// Collects the finite replicate values of one site, sorted ascending.
        /// </summary>
        /// <param name="values">The replicate matrix [sites × replicates].</param>
        /// <param name="site">The site index.</param>
        /// <returns>The sorted finite values.</returns>
        private static double[] Finite(double[,] values, int site)
        {
            var list = new List<double>();
            for (int b = 0; b < values.GetLength(1); b++)
            {
                if (!double.IsNaN(values[site, b]))
                    list.Add(values[site, b]);
            }
            list.Sort();
            return list.ToArray();
        }

        /// <summary>
        /// Draws the source rows of one temporal block-bootstrap replicate: contiguous blocks of
        /// <paramref name="blockSize"/> rows starting at uniformly random rows, wrapping at the end of the
        /// record, concatenated until <paramref name="observations"/> rows are drawn.
        /// </summary>
        /// <param name="observations">The number of rows in the record.</param>
        /// <param name="blockSize">The rows per block (at least 1).</param>
        /// <param name="prng">The seeded generator.</param>
        /// <returns>The source row of each replicate row.</returns>
        internal static int[] BuildBlockBootstrapRows(int observations, int blockSize, MersenneTwister prng)
        {
            var rows = new List<int>(observations);
            while (rows.Count < observations)
            {
                int start = prng.Next(observations);
                for (int k = 0; k < blockSize && rows.Count < observations; k++)
                    rows.Add((start + k) % observations);
            }
            return rows.ToArray();
        }

        /// <summary>
        /// Inverts a matrix using Gaussian elimination with partial pivoting.
        /// </summary>
        private static double[,]? InvertMatrix(double[,] A)
        {
            int n = A.GetLength(0);
            var aug = new double[n, 2 * n];

            // Create augmented matrix [A | I]
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                    aug[i, j] = A[i, j];
                aug[i, i + n] = 1;
            }

            // Gaussian elimination with partial pivoting
            for (int col = 0; col < n; col++)
            {
                // Find pivot
                int maxRow = col;
                for (int row = col + 1; row < n; row++)
                {
                    if (Math.Abs(aug[row, col]) > Math.Abs(aug[maxRow, col]))
                        maxRow = row;
                }

                // Swap rows
                for (int j = 0; j < 2 * n; j++)
                {
                    double temp = aug[col, j];
                    aug[col, j] = aug[maxRow, j];
                    aug[maxRow, j] = temp;
                }

                // Check for singular matrix
                if (Math.Abs(aug[col, col]) < 1e-12)
                    return null;

                // Scale row
                double scale = aug[col, col];
                for (int j = 0; j < 2 * n; j++)
                    aug[col, j] /= scale;

                // Eliminate column
                for (int row = 0; row < n; row++)
                {
                    if (row != col)
                    {
                        double factor = aug[row, col];
                        for (int j = 0; j < 2 * n; j++)
                            aug[row, j] -= factor * aug[col, j];
                    }
                }
            }

            // Extract inverse from augmented matrix
            var inv = new double[n, n];
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                    inv[i, j] = aug[i, j + n];

            return inv;
        }

        /// <summary>
        /// Multiplies two matrices.
        /// </summary>
        private static double[,] MultiplyMatrices(double[,] A, double[,] B)
        {
            int m = A.GetLength(0);
            int n = B.GetLength(1);
            int k = A.GetLength(1);
            var C = new double[m, n];

            for (int i = 0; i < m; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    double sum = 0;
                    for (int l = 0; l < k; l++)
                        sum += A[i, l] * B[l, j];
                    C[i, j] = sum;
                }
            }

            return C;
        }

        /// <inheritdoc/>
        public override (bool IsValid, List<string> ValidationMessages) Validate()
        {
            bool isValid = true;
            var messageList = new List<string>();

            // Validate model
            var modelValid = SpatialGEV.Validate();
            if (!modelValid.IsValid)
            {
                isValid = false;
                messageList.AddRange(modelValid.ValidationMessages);
            }

            // Validate probability ordinates
            if (ProbabilityOrdinates == null)
            {
                isValid = false;
                messageList.Add("Error: Probability ordinates must be specified.");
            }

            // Validate Bayesian analysis
            var bayesValid = BayesianAnalysis.Validate();
            if (!bayesValid.IsValid)
            {
                isValid = false;
                messageList.AddRange(bayesValid.ValidationMessages);
            }

            return (isValid, messageList);
        }

        /// <summary>
        /// Serializes the analysis configuration and results to an XML element.
        /// </summary>
        /// <returns>
        /// An <see cref="XElement"/> representing the current analysis configuration
        /// and, if available, the Bayesian analysis results.
        /// </returns>
        public XElement ToXElement()
        {
            var root = new XElement("SpatialGEVAnalysis",
                new XAttribute("IsEstimated", IsEstimated),
                new XAttribute(nameof(UncertaintyMethod), UncertaintyMethod.ToString()),
                new XAttribute(nameof(SampleConditionalResidual), SampleConditionalResidual.ToString()),
                new XAttribute(nameof(BootstrapReplicates), BootstrapReplicates.ToString(CultureInfo.InvariantCulture)),
                new XAttribute(nameof(BootstrapBlockSize), BootstrapBlockSize.ToString(CultureInfo.InvariantCulture)),
                new XElement("ProbabilityOrdinates",
                    ProbabilityOrdinates.ToDelimitedString(ProbabilityOrdinates.DefaultDelimiter)));


            // Bayesian analysis
            if (BayesianAnalysis != null)
            {
                root.Add(BayesianAnalysis.ToXElement());
            }

            return root;
        }

        #endregion
    }
}
