using Numerics;
using Numerics.Data;
using Numerics.Data.Statistics;
using Numerics.Distributions;
using Numerics.Sampling;
using Numerics.Utilities;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models.SpatialExtremes;
using System.Collections.Specialized;
using System.ComponentModel;
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
        /// <see cref="SpatialGEV.PropertyChanged"/> event and updates
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
        /// Gets the Godambe (sandwich) covariance matrix after calling <see cref="ComputeGodambeCovariance"/>.
        /// </summary>
        public double[,]? GodambeCovariance { get; private set; }

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
        /// Clears all analysis results and resets the <see cref="IsEstimated"/> flag.
        /// </summary>
        public void ClearResults()
        {
            BayesianAnalysis?.ClearResults();
            AnalysisResults = null;
            SiteResults = null;
            CrossValidationResults = null;
            RaisePropertyChange(nameof(AnalysisResults));
            RaisePropertyChange(nameof(SiteResults));
            RaisePropertyChange(nameof(CrossValidationResults));
            IsEstimated = false;
        }

        /// <summary>
        /// Clears <see cref="AnalysisResults"/> and <see cref="SiteResults"/> only — the outputs
        /// whose quantile arrays are keyed on <see cref="ProbabilityOrdinates"/>. Leaves the
        /// Bayesian MCMC output and <see cref="IsEstimated"/> intact so the fit can be reused
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
        /// or <see cref="IsEstimated"/>. See <see cref="HandleOrdinatesChanged"/> for the
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
                ReprocessIfEstimated(async () =>
                {
                    await CreateSiteResultsAsync();
                    await CreateUncertaintyAnalysisResultsAsync();
                });
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

                bool wasCanceled = false;
                Exception? error = null;

                try
                {
                    // Run Bayesian analysis
                    await BayesianAnalysis.RunAsync(progressReporter, false);

                    // Post-process
                    if (BayesianAnalysis.IsEstimated == true)
                    {
                        progressReporter?.ReportProgress(100);
                        await CreateSiteResultsAsync();
                        await CreateUncertaintyAnalysisResultsAsync();
                    }

                    // Conditional set — BayesianAnalysis.IsEstimated is false on soft-failure paths
                    // (sampler returns without setting IsEstimated). Setting unconditionally would
                    // silently report success even when the chain failed.
                    IsEstimated = BayesianAnalysis.IsEstimated;
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
        /// Creates site-specific results from the Bayesian posterior samples.
        /// </summary>
        private async Task CreateSiteResultsAsync()
        {
            if (BayesianAnalysis == null || BayesianAnalysis.IsEstimated == false || BayesianAnalysis.Results == null)
            {
                return;
            }

            await Task.Run(() =>
            {
                int nSites = SpatialGEV.Sites;
                SiteResults = new SpatialGEVSiteResults[nSites];

                var prng = new MersenneTwister(BayesianAnalysis.PRNGSeed);
                var realz = BayesianAnalysis.OutputLength;
                double alpha = 1 - BayesianAnalysis.CredibleIntervalWidth;
                var probs = ProbabilityOrdinates.ToArray();
                int nProbs = probs.Length;

                for (int j = 0; j < nSites; j++)
                {
                    SiteResults[j] = new SpatialGEVSiteResults
                    {
                        SiteIndex = j,
                        Coordinate = new double[] { SpatialGEV.Coordinates[j, 0], SpatialGEV.Coordinates[j, 1] }
                    };

                    // Arrays for GEV parameters across realizations
                    var xiVals = new double[realz];
                    var alphaVals = new double[realz];
                    var kappaVals = new double[realz];

                    // Arrays for quantiles at each probability
                    var quantiles = new double[nProbs, realz];

                    // Compute for each posterior sample
                    Parallel.For(0, realz, idx =>
                    {
                        var tempModel = (SpatialGEV)SpatialGEV.Clone();
                        tempModel.SetParameterValues(BayesianAnalysis.Results.Output[idx].Values);

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

                    // Compute summary statistics for GEV parameters
                    Array.Sort(xiVals);
                    Array.Sort(alphaVals);
                    Array.Sort(kappaVals);

                    SiteResults[j].LocationMean = Statistics.ParallelMean(xiVals);
                    SiteResults[j].LocationLower = Statistics.Percentile(xiVals, alpha / 2d, true);
                    SiteResults[j].LocationUpper = Statistics.Percentile(xiVals, 1 - alpha / 2d, true);

                    SiteResults[j].ScaleMean = Statistics.ParallelMean(alphaVals);
                    SiteResults[j].ScaleLower = Statistics.Percentile(alphaVals, alpha / 2d, true);
                    SiteResults[j].ScaleUpper = Statistics.Percentile(alphaVals, 1 - alpha / 2d, true);

                    SiteResults[j].ShapeMean = Statistics.ParallelMean(kappaVals);
                    SiteResults[j].ShapeLower = Statistics.Percentile(kappaVals, alpha / 2d, true);
                    SiteResults[j].ShapeUpper = Statistics.Percentile(kappaVals, 1 - alpha / 2d, true);

                    // Compute quantile curves
                    SiteResults[j].Probabilities = probs;
                    SiteResults[j].QuantileMean = new double[nProbs];
                    SiteResults[j].QuantileLower = new double[nProbs];
                    SiteResults[j].QuantileUpper = new double[nProbs];
                    SiteResults[j].QuantileMode = new double[nProbs];

                    // Point estimate using MAP
                    SpatialGEV.SetParameterValues(BayesianAnalysis.Results.MAP.Values);

                    for (int p = 0; p < nProbs; p++)
                    {
                        var qVals = quantiles.GetRow(p);
                        Array.Sort(qVals);
                        SiteResults[j].QuantileMean[p] = Statistics.ParallelMean(qVals);
                        SiteResults[j].QuantileLower[p] = Statistics.Percentile(qVals, alpha / 2d, true);
                        SiteResults[j].QuantileUpper[p] = Statistics.Percentile(qVals, 1 - alpha / 2d, true);
                        SiteResults[j].QuantileMode[p] = SpatialGEV.InverseCDF(1 - probs[p], j);
                    }
                }
            });

            RaisePropertyChange(nameof(SiteResults));
        }

        /// <summary>
        /// Creates the regional uncertainty analysis results from the Bayesian posterior samples.
        /// </summary>
        private async Task CreateUncertaintyAnalysisResultsAsync()
        {
            AnalysisResults = null;
            if (BayesianAnalysis == null || BayesianAnalysis.IsEstimated == false ||
                BayesianAnalysis.Results == null || SiteResults == null)
            {
                RaisePropertyChange(nameof(AnalysisResults));
                return;
            }

            // Capture local reference to avoid null warnings in closure
            var siteResults = SiteResults;

            await Task.Run(() =>
            {
                var probs = ProbabilityOrdinates.ToArray();
                int n = probs.Length;

                AnalysisResults = new UncertaintyAnalysisResults();
                AnalysisResults.ModeCurve = new double[n];
                AnalysisResults.MeanCurve = new double[n];
                AnalysisResults.ConfidenceIntervals = new double[n, 3];

                // For regional results, average across all sites
                for (int p = 0; p < n; p++)
                {
                    double sumMode = 0, sumMean = 0, sumLower = 0, sumUpper = 0;
                    for (int j = 0; j < SpatialGEV.Sites; j++)
                    {
                        sumMode += siteResults[j].QuantileMode[p];
                        sumMean += siteResults[j].QuantileMean[p];
                        sumLower += siteResults[j].QuantileLower[p];
                        sumUpper += siteResults[j].QuantileUpper[p];
                    }
                    AnalysisResults.ModeCurve[p] = sumMode / SpatialGEV.Sites;
                    AnalysisResults.MeanCurve[p] = sumMean / SpatialGEV.Sites;
                    AnalysisResults.ConfidenceIntervals[p, 0] = probs[p];
                    AnalysisResults.ConfidenceIntervals[p, 1] = sumLower / SpatialGEV.Sites;
                    AnalysisResults.ConfidenceIntervals[p, 2] = sumUpper / SpatialGEV.Sites;
                }

                // Goodness-of-fit metrics
                // AIC/BIC at MAP using full LogLikelihood (data + prior).
                double mapLogLH = SpatialGEV.LogLikelihood(BayesianAnalysis.Results.MAP.Values);
                AnalysisResults.AIC = GoodnessOfFit.AIC(SpatialGEV.NumberOfParameters, mapLogLH);
                AnalysisResults.BIC = GoodnessOfFit.BIC(SpatialGEV.Sites * SpatialGEV.Observations, SpatialGEV.NumberOfParameters, mapLogLH);
                AnalysisResults.DIC = BayesianAnalysis.DIC;
            });

            RaisePropertyChange(nameof(AnalysisResults));
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

            Parallel.For(0, realz, idx =>
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
        /// <param name="coordinates">The coordinates [X, Y] or [Lat, Lon] of the ungauged location.</param>
        /// <param name="covariates">Optional covariate values at the ungauged location for the trend models.</param>
        /// <param name="exceedanceProbabilities">Array of exceedance probabilities for quantile estimation.</param>
        /// <returns>
        /// A <see cref="SpatialGEVSiteResults"/> object containing predicted GEV parameters and quantile curves.
        /// </returns>
        /// <exception cref="InvalidOperationException">Thrown if the analysis has not been run.</exception>
        /// <remarks>
        /// <para>
        /// This method uses the posterior samples to propagate uncertainty to the ungauged location.
        /// The prediction uses the trend surface evaluated at the provided coordinates and covariates.
        /// If spatial regression errors are enabled, a kriging-type interpolation is used for the errors.
        /// </para>
        /// </remarks>
        public SpatialGEVSiteResults PredictAtUngaugedLocation(double[] coordinates, double[]? covariates, double[] exceedanceProbabilities)
        {
            if (!IsEstimated || BayesianAnalysis?.Results == null)
                throw new InvalidOperationException("Analysis must be run before predicting at ungauged locations.");

            if (coordinates == null || coordinates.Length != 2)
                throw new ArgumentException("Coordinates must be a 2-element array [X, Y].", nameof(coordinates));

            var realz = BayesianAnalysis.OutputLength;
            double alpha = 1 - BayesianAnalysis.CredibleIntervalWidth;
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

            // Use inverse-distance weighting for spatial interpolation of errors
            var distances = new double[SpatialGEV.Sites];
            double sumInvDist = 0;
            for (int j = 0; j < SpatialGEV.Sites; j++)
            {
                double dx = coordinates[0] - SpatialGEV.Coordinates[j, 0];
                double dy = coordinates[1] - SpatialGEV.Coordinates[j, 1];
                distances[j] = Math.Max(Math.Sqrt(dx * dx + dy * dy), 1e-10);
                sumInvDist += 1.0 / distances[j];
            }

            Parallel.For(0, realz, idx =>
            {
                var tempModel = (SpatialGEV)SpatialGEV.Clone();
                tempModel.SetParameterValues(BayesianAnalysis.Results.Output[idx].Values);

                // Evaluate trend at ungauged location
                double xi = tempModel.Location.PredictWithCovariates(covariates);
                double scl = tempModel.Scale.PredictWithCovariates(covariates);
                double kappa = tempModel.Shape.PredictWithCovariates(covariates);

                // Apply link functions
                if (tempModel.UseLogLinkForLocation)
                    xi = Math.Exp(xi);
                if (tempModel.UseLogLinkForScale)
                    scl = Math.Exp(scl);

                // Interpolate spatial errors if enabled (IDW)
                if (tempModel.UseLocationErrors && tempModel.LocationErrors != null)
                {
                    double errSum = 0;
                    for (int j = 0; j < tempModel.Sites; j++)
                        errSum += tempModel.LocationErrors.GetError(j) / distances[j];
                    double interpErr = errSum / sumInvDist;
                    if (tempModel.UseLogLinkForLocation)
                        xi *= Math.Exp(interpErr);
                    else
                        xi += interpErr;
                }

                if (tempModel.UseScaleErrors && tempModel.ScaleErrors != null)
                {
                    double errSum = 0;
                    for (int j = 0; j < tempModel.Sites; j++)
                        errSum += tempModel.ScaleErrors.GetError(j) / distances[j];
                    double interpErr = errSum / sumInvDist;
                    if (tempModel.UseLogLinkForScale)
                        scl *= Math.Exp(interpErr);
                    else
                        scl = Math.Max(scl + interpErr, Tools.DoubleMachineEpsilon);
                }

                if (tempModel.UseShapeErrors && tempModel.ShapeErrors != null)
                {
                    double errSum = 0;
                    for (int j = 0; j < tempModel.Sites; j++)
                        errSum += tempModel.ShapeErrors.GetError(j) / distances[j];
                    kappa += errSum / sumInvDist;
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
        /// Runs leave-one-site-out cross-validation to assess model predictive performance.
        /// </summary>
        /// <param name="progressReporter">Optional progress reporter for tracking cross-validation progress.</param>
        /// <returns>
        /// A task that completes when cross-validation is finished.
        /// Results are available via the <see cref="CrossValidationResults"/> property.
        /// </returns>
        /// <remarks>
        /// <para>
        /// For each site, this method:
        /// 1. Excludes the site by setting its weight to zero.
        /// 2. Re-runs the Bayesian analysis.
        /// 3. Predicts quantiles at the excluded site.
        /// 4. Compares predictions to observed data.
        /// </para>
        /// <para>
        /// This provides an estimate of how well the model generalizes to ungauged locations.
        /// </para>
        /// </remarks>
        public async Task RunCrossValidationAsync(SafeProgressReporter? progressReporter = null)
        {
            if (Validate().IsValid == false)
                throw new InvalidOperationException("Model validation failed.");

            CrossValidationResults = new SpatialGEVCrossValidationResults
            {
                SitePredictionErrors = new double[SpatialGEV.Sites],
                SiteRMSE = new double[SpatialGEV.Sites],
                SiteBias = new double[SpatialGEV.Sites],
                SiteCRPS = new double[SpatialGEV.Sites] // CRPS not yet computed (always zero); see remarks on the property.
            };

            var originalWeights = (double[])SpatialGEV.SiteWeights.Clone();

            try
            {
                for (int j = 0; j < SpatialGEV.Sites; j++)
                {
                    progressReporter?.ReportProgress((int)(100.0 * j / SpatialGEV.Sites));

                    // Set weight to zero for left-out site
                    for (int k = 0; k < SpatialGEV.Sites; k++)
                        SpatialGEV.SiteWeights[k] = k == j ? 0.0 : originalWeights[k];

                    // Clear and re-run
                    if (BayesianAnalysis is null)
                        continue;
                    BayesianAnalysis.ClearResults();
                    await BayesianAnalysis.RunAsync(null, false);

                    if (!BayesianAnalysis.IsEstimated)
                        continue;

                    // Predict at left-out site
                    var coords = new double[] { SpatialGEV.Coordinates[j, 0], SpatialGEV.Coordinates[j, 1] };
                    var probs = new double[] { 0.5, 0.2, 0.1, 0.04, 0.02, 0.01 }; // T=2, 5, 10, 25, 50, 100

                    var prediction = PredictAtUngaugedLocation(coords, null, probs);

                    // Compare to observed data at this site
                    var siteData = new List<double>();
                    for (int i = 0; i < SpatialGEV.Observations; i++)
                    {
                        if (!double.IsNaN(SpatialGEV.AtSiteData[i, j]))
                            siteData.Add(SpatialGEV.AtSiteData[i, j]);
                    }

                    if (siteData.Count > 0)
                    {
                        // Compute prediction error as difference in T=100 quantile
                        var gev = new GeneralizedExtremeValue();
                        gev.Estimate(siteData, ParameterEstimationMethod.MaximumLikelihood);
                        double obsQ100 = gev.InverseCDF(0.99);
                        double predQ100 = prediction.QuantileMean[5]; // T=100

                        CrossValidationResults.SitePredictionErrors[j] = predQ100 - obsQ100;
                        CrossValidationResults.SiteBias[j] = (predQ100 - obsQ100) / obsQ100;

                        // Compute RMSE over multiple quantiles
                        double sumSqErr = 0;
                        for (int p = 0; p < probs.Length; p++)
                        {
                            double obsQ = gev.InverseCDF(1 - probs[p]);
                            double predQ = prediction.QuantileMean[p];
                            sumSqErr += (predQ - obsQ) * (predQ - obsQ);
                        }
                        CrossValidationResults.SiteRMSE[j] = Math.Sqrt(sumSqErr / probs.Length);
                    }
                }

                // Restore original weights
                SpatialGEV.SiteWeights = originalWeights;

                // Compute overall metrics
                CrossValidationResults.MeanAbsoluteError = Statistics.ParallelMean(
                    CrossValidationResults.SitePredictionErrors.Select(Math.Abs).ToArray());
                CrossValidationResults.RootMeanSquareError = Math.Sqrt(Statistics.ParallelMean(
                    CrossValidationResults.SitePredictionErrors.Select(e => e * e).ToArray()));
                CrossValidationResults.MeanBias = Statistics.ParallelMean(CrossValidationResults.SiteBias);

                // Re-run full analysis
                BayesianAnalysis?.ClearResults();
                await RunAsync(null);
            }
            finally
            {
                SpatialGEV.SiteWeights = originalWeights;
            }

            RaisePropertyChange(nameof(CrossValidationResults));
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
        /// <returns>The Godambe covariance matrix [nParams × nParams].</returns>
        /// <remarks>
        /// <para>
        /// The Godambe sandwich estimator provides robust standard errors that account for
        /// model misspecification and correlation in the data:
        /// </para>
        /// <para>
        /// Var(θ̂) = H(θ)⁻¹ J(θ) H(θ)⁻¹
        /// </para>
        /// <para>
        /// where H is the Hessian (sensitivity matrix) and J is the variability matrix computed
        /// from the outer product of the score vectors.
        /// </para>
        /// <para>
        ///     <b>References:</b>
        ///     - Godambe, V.P. (1960). An optimum property of regular maximum likelihood estimation.
        ///       Annals of Mathematical Statistics, 31(4), 1208-1211.
        ///     - Varin, C., Reid, N., Firth, D. (2011). An overview of composite likelihood methods.
        ///       Statistica Sinica, 21, 5-42.
        /// </para>
        /// </remarks>
        public double[,] ComputeGodambeCovariance(double[]? parameters = null)
        {
            if (parameters == null)
            {
                if (BayesianAnalysis?.Results?.MAP == null)
                    throw new InvalidOperationException("Analysis must be run before computing Godambe covariance.");
                parameters = BayesianAnalysis.Results.MAP.Values;
            }

            int nParams = parameters.Length;
            double eps = 1e-5;

            // Compute Hessian H (second derivative of log-likelihood)
            var H = new double[nParams, nParams];
            double f0 = SpatialGEV.DataLogLikelihood(parameters);

            for (int i = 0; i < nParams; i++)
            {
                for (int j = i; j < nParams; j++)
                {
                    // Central difference approximation
                    var pPlusI = (double[])parameters.Clone();
                    var pPlusJ = (double[])parameters.Clone();
                    var pMinusI = (double[])parameters.Clone();
                    var pMinusJ = (double[])parameters.Clone();
                    var pPlusIJ = (double[])parameters.Clone();
                    var pMinusIJ = (double[])parameters.Clone();
                    var pPlusIMinusJ = (double[])parameters.Clone();
                    var pMinusIPlusJ = (double[])parameters.Clone();

                    double hi = Math.Abs(parameters[i]) * eps + eps;
                    double hj = Math.Abs(parameters[j]) * eps + eps;

                    if (i == j)
                    {
                        // Diagonal: d²L/dθᵢ²
                        pPlusI[i] += hi;
                        pMinusI[i] -= hi;
                        double fPlusI = SpatialGEV.DataLogLikelihood(pPlusI);
                        double fMinusI = SpatialGEV.DataLogLikelihood(pMinusI);
                        H[i, i] = (fPlusI - 2 * f0 + fMinusI) / (hi * hi);
                    }
                    else
                    {
                        // Off-diagonal: d²L/dθᵢdθⱼ
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
                }
            }

            // Compute J (variability matrix) from outer product of score vectors
            // J = Σᵢ sᵢ sᵢᵀ where sᵢ is the score for observation i
            var J = new double[nParams, nParams];
            var pointwiseLL = SpatialGEV.PointwiseDataLogLikelihood(parameters);

            for (int obs = 0; obs < pointwiseLL.Length; obs++)
            {
                // Compute score vector for this observation
                var score = new double[nParams];
                for (int k = 0; k < nParams; k++)
                {
                    var pPlus = (double[])parameters.Clone();
                    var pMinus = (double[])parameters.Clone();
                    double h = Math.Abs(parameters[k]) * eps + eps;
                    pPlus[k] += h;
                    pMinus[k] -= h;

                    var llPlus = SpatialGEV.PointwiseDataLogLikelihood(pPlus);
                    var llMinus = SpatialGEV.PointwiseDataLogLikelihood(pMinus);

                    score[k] = (llPlus[obs] - llMinus[obs]) / (2 * h);
                }

                // Add outer product to J
                for (int i = 0; i < nParams; i++)
                {
                    for (int j = 0; j < nParams; j++)
                    {
                        J[i, j] += score[i] * score[j];
                    }
                }
            }

            // Invert H
            var HInv = InvertMatrix(H);
            if (HInv == null)
            {
                // Return J if H is singular
                GodambeCovariance = J;
                return J;
            }

            // Compute sandwich: H⁻¹ J H⁻¹
            var temp = MultiplyMatrices(HInv, J);
            GodambeCovariance = MultiplyMatrices(temp, HInv);

            return GodambeCovariance;
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
        /// Runs spatial block bootstrap to compute empirical confidence intervals.
        /// </summary>
        /// <param name="nBootstrap">Number of bootstrap replicates. Default is 200.</param>
        /// <param name="blockSize">Spatial block size (number of sites per block). If 0, uses sqrt(Sites).</param>
        /// <param name="progressReporter">Optional progress reporter.</param>
        /// <returns>
        /// A task that completes when bootstrap is finished. Results are stored in
        /// <see cref="SiteResults"/> with updated confidence bounds.
        /// </returns>
        /// <remarks>
        /// <para>
        /// Spatial block bootstrap preserves the spatial correlation structure by resampling
        /// blocks of spatially contiguous sites rather than individual sites. This provides
        /// valid confidence intervals when data are spatially correlated.
        /// </para>
        /// <para>
        /// The method:
        /// 1. Divides sites into spatial blocks based on proximity
        /// 2. For each bootstrap replicate, resamples blocks with replacement
        /// 3. Re-estimates the model on resampled data
        /// 4. Computes quantiles from the bootstrap distribution
        /// </para>
        /// <para>
        ///     <b>References:</b>
        ///     - Lahiri, S.N. (2003). Resampling Methods for Dependent Data. Springer.
        ///     - Politis, D.N., Romano, J.P. (1994). The stationary bootstrap. JASA.
        /// </para>
        /// </remarks>
        public async Task RunSpatialBootstrapAsync(int nBootstrap = 200, int blockSize = 0, SafeProgressReporter? progressReporter = null)
        {
            if (!IsEstimated || BayesianAnalysis?.Results == null)
                throw new InvalidOperationException("Analysis must be run before bootstrap.");

            if (blockSize <= 0)
                blockSize = Math.Max(2, (int)Math.Sqrt(SpatialGEV.Sites));

            var prng = new MersenneTwister(BayesianAnalysis.PRNGSeed);
            var probs = ProbabilityOrdinates.ToArray();
            int nProbs = probs.Length;
            int nSites = SpatialGEV.Sites;

            // Create spatial blocks using k-means-like clustering on coordinates
            var blockAssignments = CreateSpatialBlocks(blockSize);
            int nBlocks = blockAssignments.Max() + 1;

            // Arrays to store bootstrap quantiles for each site
            var bootQuantiles = new double[nSites, nProbs, nBootstrap];
            var bootLocation = new double[nSites, nBootstrap];
            var bootScale = new double[nSites, nBootstrap];
            var bootShape = new double[nSites, nBootstrap];

            await Task.Run(() =>
            {
                for (int b = 0; b < nBootstrap; b++)
                {
                    progressReporter?.ReportProgress((int)(100.0 * b / nBootstrap));

                    // Resample blocks
                    var resampledBlocks = new List<int>();
                    while (resampledBlocks.Count < nBlocks)
                    {
                        int block = prng.Next(nBlocks);
                        resampledBlocks.Add(block);
                    }

                    // Get sites in resampled blocks
                    var resampledSites = new List<int>();
                    foreach (int block in resampledBlocks)
                    {
                        for (int j = 0; j < nSites; j++)
                        {
                            if (blockAssignments[j] == block)
                                resampledSites.Add(j);
                        }
                    }

                    // Create bootstrap data matrix
                    var bootData = new double[SpatialGEV.Observations, nSites];
                    for (int i = 0; i < SpatialGEV.Observations; i++)
                    {
                        for (int j = 0; j < nSites; j++)
                        {
                            // Use resampled site data (with replacement)
                            int srcSite = resampledSites[j % resampledSites.Count];
                            bootData[i, j] = SpatialGEV.AtSiteData[i, srcSite];
                        }
                    }

                    // Create bootstrap model and estimate
                    var bootModel = (SpatialGEV)SpatialGEV.Clone();

                    // For speed, use a short MCMC run
                    var bootBayes = new BayesianAnalysis(bootModel)
                    {
                        Iterations = 500,
                        WarmupIterations = 250,
                        ThinningInterval = 5
                    };

                    try
                    {
                        bootBayes.RunAsync(null, false).GetAwaiter().GetResult();

                        if (bootBayes.IsEstimated && bootBayes.Results != null)
                        {
                            bootModel.SetParameterValues(bootBayes.Results.MAP.Values);

                            // Store results for each site
                            for (int j = 0; j < nSites; j++)
                            {
                                var gevParams = bootModel.GetGEVParameters(j);
                                bootLocation[j, b] = gevParams[0];
                                bootScale[j, b] = gevParams[1];
                                bootShape[j, b] = gevParams[2];

                                for (int p = 0; p < nProbs; p++)
                                {
                                    bootQuantiles[j, p, b] = bootModel.InverseCDF(1 - probs[p], j);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Mark failed bootstrap replicate with NaN.
                        Debug.WriteLine($"SpatialGEVAnalysis.Bootstrap: replicate {b} failed, marked NaN: {ex.Message}");
                        for (int j = 0; j < nSites; j++)
                        {
                            bootLocation[j, b] = double.NaN;
                            bootScale[j, b] = double.NaN;
                            bootShape[j, b] = double.NaN;
                            for (int p = 0; p < nProbs; p++)
                                bootQuantiles[j, p, b] = double.NaN;
                        }
                    }
                }

                // Compute bootstrap confidence intervals
                double alpha = 1 - BayesianAnalysis.CredibleIntervalWidth;

                for (int j = 0; j < nSites; j++)
                {
                    // Get valid bootstrap samples (non-NaN)
                    var validLoc = new List<double>();
                    var validScl = new List<double>();
                    var validShp = new List<double>();

                    for (int b = 0; b < nBootstrap; b++)
                    {
                        if (!double.IsNaN(bootLocation[j, b]))
                        {
                            validLoc.Add(bootLocation[j, b]);
                            validScl.Add(bootScale[j, b]);
                            validShp.Add(bootShape[j, b]);
                        }
                    }

                    if (validLoc.Count >= 10 && SiteResults != null)
                    {
                        validLoc.Sort();
                        validScl.Sort();
                        validShp.Sort();

                        SiteResults[j].LocationLower = Statistics.Percentile(validLoc.ToArray(), alpha / 2, true);
                        SiteResults[j].LocationUpper = Statistics.Percentile(validLoc.ToArray(), 1 - alpha / 2, true);
                        SiteResults[j].ScaleLower = Statistics.Percentile(validScl.ToArray(), alpha / 2, true);
                        SiteResults[j].ScaleUpper = Statistics.Percentile(validScl.ToArray(), 1 - alpha / 2, true);
                        SiteResults[j].ShapeLower = Statistics.Percentile(validShp.ToArray(), alpha / 2, true);
                        SiteResults[j].ShapeUpper = Statistics.Percentile(validShp.ToArray(), 1 - alpha / 2, true);

                        // Quantile CIs
                        for (int p = 0; p < nProbs; p++)
                        {
                            var validQ = new List<double>();
                            for (int b = 0; b < nBootstrap; b++)
                            {
                                if (!double.IsNaN(bootQuantiles[j, p, b]))
                                    validQ.Add(bootQuantiles[j, p, b]);
                            }

                            if (validQ.Count >= 10)
                            {
                                validQ.Sort();
                                SiteResults[j].QuantileLower[p] = Statistics.Percentile(validQ.ToArray(), alpha / 2, true);
                                SiteResults[j].QuantileUpper[p] = Statistics.Percentile(validQ.ToArray(), 1 - alpha / 2, true);
                            }
                        }
                    }
                }
            });

            RaisePropertyChange(nameof(SiteResults));
        }

        /// <summary>
        /// Creates spatial blocks for bootstrap resampling using k-means-like clustering.
        /// </summary>
        /// <param name="blockSize">Target number of sites per block.</param>
        /// <returns>Array of block assignments for each site.</returns>
        private int[] CreateSpatialBlocks(int blockSize)
        {
            int nSites = SpatialGEV.Sites;
            int nBlocks = Math.Max(1, (nSites + blockSize - 1) / blockSize);
            var assignments = new int[nSites];

            // Simple greedy spatial clustering
            var unassigned = new List<int>(Enumerable.Range(0, nSites));
            var prng = new MersenneTwister(12345);

            for (int block = 0; block < nBlocks && unassigned.Count > 0; block++)
            {
                // Start with random unassigned site
                int startIdx = prng.Next(unassigned.Count);
                int startSite = unassigned[startIdx];
                unassigned.RemoveAt(startIdx);
                assignments[startSite] = block;

                // Add nearest unassigned sites to this block
                int siteCount = 1;
                while (siteCount < blockSize && unassigned.Count > 0)
                {
                    // Find nearest unassigned site to centroid of current block
                    double minDist = double.MaxValue;
                    int nearestIdx = -1;

                    foreach (int idx in Enumerable.Range(0, unassigned.Count))
                    {
                        int site = unassigned[idx];
                        double dist = Tools.Distance(
                            SpatialGEV.Coordinates[site, 0], SpatialGEV.Coordinates[site, 1],
                            SpatialGEV.Coordinates[startSite, 0], SpatialGEV.Coordinates[startSite, 1]);

                        if (dist < minDist)
                        {
                            minDist = dist;
                            nearestIdx = idx;
                        }
                    }

                    if (nearestIdx >= 0)
                    {
                        assignments[unassigned[nearestIdx]] = block;
                        unassigned.RemoveAt(nearestIdx);
                        siteCount++;
                    }
                }
            }

            return assignments;
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
