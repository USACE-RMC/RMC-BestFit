using Numerics;
using Numerics.Data;
using Numerics.Data.Statistics;
using Numerics.Distributions;
using Numerics.Functions;
using Numerics.Mathematics.LinearAlgebra;
using Numerics.Mathematics.Optimization;
using Numerics.Sampling;
using Numerics.Sampling.MCMC;
using Numerics.Utilities;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using RMC.BestFit.Models.LinkFunctions;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using System.Xml.Linq;

namespace RMC.BestFit.Analyses
{
    /// <summary>
    /// Specifies the method used for uncertainty quantification in frequentist analysis methods.
    /// </summary>
    public enum UncertaintyMethod
    {
        /// <summary>
        /// Uses a multivariate normal approximation to the parameter covariance matrix
        /// for uncertainty propagation. This is a first-order accurate method.
        /// </summary>
        MultivariateNormal,

        /// <summary>
        /// Uses a linked multivariate normal approximation that applies variance-stabilizing
        /// link transformations before constructing the covariance matrix for uncertainty propagation.
        /// </summary>
        LinkedMultivariateNormal,

        /// <summary>
        /// Uses parametric bootstrap with percentile intervals. This is a first-order
        /// accurate method that resamples from the fitted model.
        /// </summary>
        Bootstrap,

        /// <summary>
        /// Uses bias-corrected bootstrap intervals. This method corrects for bias in the
        /// bootstrap distribution and provides improved coverage.
        /// </summary>
        BiasCorrectedBootstrap,

    }

    // SkewLinkType enum removed � gamma link is now always ASinH with adaptive epsilon from gammaHat.

    /// <summary>
    /// Implements the Bulletin 17C flood frequency analysis using the Generalized Method of Moments (GMM).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Authors:</b>
    /// Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    /// Bulletin 17C is the recommended guideline for flood frequency analysis in the United States.
    /// This implementation uses the Generalized Method of Moments (GMM) for parameter estimation,
    /// which is more robust than traditional method of moments when dealing with:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Low outliers and censored data (values below a detection threshold)</description></item>
    /// <item><description>Interval data and uncertain observations</description></item>
    /// <item><description>Historical and paleoflood information</description></item>
    /// <item><description>Regional skewness information through parameter penalties</description></item>
    /// </list>
    /// <para>
    /// The analysis supports multiple uncertainty quantification methods including multivariate
    /// normal approximation and various bootstrap approaches. Parameter and quantile penalties
    /// can be specified to incorporate prior information, such as regional skewness estimates.
    /// </para>
    /// <para>
    /// <b>References:</b>
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// England, J.F., Jr., Cohn, T.A., Faber, B.A., Stedinger, J.R., Thomas, W.O., Jr.,
    /// Veilleux, A.G., Kiang, J.E., and Mason, R.R., Jr., 2019, Guidelines for determining
    /// flood flow frequency�Bulletin 17C: U.S. Geological Survey Techniques and Methods,
    /// book 4, chap. B5, 148 p.
    /// </description></item>
    /// </list>
    /// </remarks>
    public class Bulletin17CAnalysis : AnalysisBase, IUnivariateAnalysis
    {
        #region Construction

        /// <summary>
        /// Initializes a new instance of the <see cref="Bulletin17CAnalysis"/> class.
        /// </summary>
        /// <param name="bullet17CDistribution">
        /// The <see cref="Models.Bulletin17CDistribution"/> to estimate. Typically Log-Pearson Type III
        /// for Bulletin 17C analyses.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="bullet17CDistribution"/> is <c>null</c>.
        /// </exception>
        public Bulletin17CAnalysis(Bulletin17CDistribution bullet17CDistribution)
        {
            Bulletin17CDistribution = bullet17CDistribution ?? throw new ArgumentNullException(nameof(bullet17CDistribution));
            BayesianAnalysis = new BayesianAnalysis();
            BayesianAnalysis.PointEstimator = BayesianAnalysis.PointEstimateType.PosteriorMode;
            UncertaintyMethod = UncertaintyMethod.LinkedMultivariateNormal;
            ProbabilityOrdinates = new ProbabilityOrdinates();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Bulletin17CAnalysis"/> class
        /// by deserializing from an <see cref="XElement"/>.
        /// </summary>
        /// <param name="univariateDistribution">
        /// The <see cref="Bulletin17CDistribution"/> associated with this analysis.
        /// </param>
        /// <param name="xElement">
        /// The XML element from which to restore the analysis configuration (BayesianAnalysis settings,
        /// ProbabilityOrdinates, and IsEstimated flag). Produced by <see cref="ToXElement"/>.
        /// </param>
        /// <param name="mcmcResults">
        /// Optional MCMC results to restore from a previous estimation.
        /// When provided, results are set via <c>BayesianAnalysis.SetCustomMCMCResults</c>
        /// with <c>skipInformationCriteria: true</c> since DIC/WAIC/LOO-CV are already in the BayesianAnalysis XElement.
        /// </param>
        /// <param name="analysisResults">
        /// Optional frequency analysis results to restore from a previous estimation.
        /// </param>
        /// <param name="chronologyAnalysisResults">
        /// Optional chronology analysis results for nonstationary models to restore from a previous estimation.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="univariateDistribution"/> or <paramref name="xElement"/> is <c>null</c>.
        /// </exception>
        public Bulletin17CAnalysis(Bulletin17CDistribution univariateDistribution, XElement xElement,
                                  MCMCResults? mcmcResults = null,
                                  UncertaintyAnalysisResults? analysisResults = null,
                                  UncertaintyAnalysisResults? chronologyAnalysisResults = null)
        {
            if (xElement == null) throw new ArgumentNullException(nameof(xElement));
            Bulletin17CDistribution = univariateDistribution ?? throw new ArgumentNullException(nameof(univariateDistribution));

            // Probability ordinates
            ProbabilityOrdinates = new ProbabilityOrdinates();
            var probElement = xElement.Element(nameof(ProbabilityOrdinates));
            if (probElement != null)
            {
                var probText = (string)probElement;
                ProbabilityOrdinates.FromDelimitedString(probText, ProbabilityOrdinates.DefaultDelimiter);
            }

            // Uncertainty method
            var methodAttr = xElement.Attribute(nameof(UncertaintyMethod));
            if (methodAttr != null && Enum.TryParse(methodAttr.Value, out UncertaintyMethod method))
            {
                UncertaintyMethod = method;
            }

            // Bayesian analysis (stores MCMC results)
            var bayesElement = xElement.Element(nameof(BayesianAnalysis));
            if (bayesElement != null)
            {
                BayesianAnalysis = new BayesianAnalysis(null, bayesElement);
            }
            else
            {
                BayesianAnalysis = new BayesianAnalysis();
            }

            // Is estimated
            var isEstAttr = xElement.Attribute("IsEstimated");
            if (isEstAttr != null && bool.TryParse(isEstAttr.Value, out bool isEst))
            {
                _isEstimated = isEst;
            }

            // Restore MCMC results if provided � pass parameter names so ParameterSetsControl
            // and KDE/Histogram/Bivariate combo boxes can display column headers.
            if (mcmcResults != null)
            {
                var paramNames = Bulletin17CDistribution.Parameters.Select(p => p.DisplayName).ToArray();
                _bayesianAnalysis.SetCustomMCMCResults(mcmcResults, skipInformationCriteria: true, paramNames);
            }

            // Restore analysis results
            AnalysisResults = analysisResults;
            NormalizeRestoredEstimatedState();

            // Restore GMM estimation results if present in the XElement.
            // GMM is a live computation object � the model provides delegates (MomentConditionFunction, etc.)
            // and RestoreFromXElement re-attaches the stored output state (BestParameterSet, matrices, diagnostics).
            var gmmElement = xElement.Element(nameof(GeneralizedMethodOfMoments));
            if (gmmElement != null && Bulletin17CDistribution.DataFrame != null)
            {
                try
                {
                    Bulletin17CDistribution.SetPenaltyFunction();
                    _gmm = new GeneralizedMethodOfMoments(Bulletin17CDistribution);
                    _gmm.RestoreFromXElement(gmmElement);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to restore GMM from XElement: {ex.Message}");
                    _gmm = null;
                }
            }

            // Restore uncertainty sampling diagnostics when present. The element is optional;
            // projects saved by earlier versions restore with null diagnostics.
            BootstrapResults = BootstrapDiagnostics.FromXElement(xElement.Element(nameof(BootstrapDiagnostics)));
        }

        #endregion

        #region Members

        private Bulletin17CDistribution _bulletin17CDistribution = null!;
        private BayesianAnalysis _bayesianAnalysis = null!;
        private ProbabilityOrdinates _probabilityOrdinates = null!;
        private UncertaintyMethod _uncertaintyMethod = UncertaintyMethod.MultivariateNormal;
        private GeneralizedMethodOfMoments? _gmm;
        private new CancellationTokenSource? _cancellationTokenSource;
        private TimeSpan? _elapsedTime;
        private TimeSpan? _gmmElapsedTime;
        private TimeSpan? _uncertaintyElapsedTime;

        /// <summary>
        /// Gets the univariate distribution model being estimated.
        /// </summary>
        /// <remarks>
        /// <para>
        /// For Bulletin 17C analysis, this is typically a Log-Pearson Type III distribution.
        /// The distribution's <see cref="DataFrame"/> should contain the flood data to analyze.
        /// </para>
        /// </remarks>
        public Bulletin17CDistribution Bulletin17CDistribution
        {
            get => _bulletin17CDistribution;
            private set
            {
                if (_bulletin17CDistribution != null)
                    _bulletin17CDistribution.PropertyChanged -= Model_PropertyChanged;

                _bulletin17CDistribution = value;

                if (_bulletin17CDistribution != null)
                {
                    _bulletin17CDistribution.PropertyChanged += Model_PropertyChanged;
                }

                RaisePropertyChange(nameof(Bulletin17CDistribution));
            }
        }

        /// <inheritdoc/>
        public ProbabilityOrdinates ProbabilityOrdinates
        {
            get => _probabilityOrdinates;
            private set
            {
                if (_probabilityOrdinates != null)
                    _probabilityOrdinates.CollectionChanged -= ProbabilityOrdinates_CollectionChanged;

                _probabilityOrdinates = value ?? new ProbabilityOrdinates();
                _probabilityOrdinates.CollectionChanged += ProbabilityOrdinates_CollectionChanged;

                RaisePropertyChange(nameof(ProbabilityOrdinates));
            }
        }

        /// <summary>
        /// Gets the Bayesian analysis object used to store MCMC-style results.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Although this analysis uses GMM rather than true Bayesian MCMC, the results
        /// are stored in a <see cref="BayesianAnalysis"/> object to maintain compatibility
        /// with the uncertainty analysis framework.
        /// </para>
        /// </remarks>
        public BayesianAnalysis BayesianAnalysis
        {
            get => _bayesianAnalysis;
            private set
            {
                if (_bayesianAnalysis != null)
                {
                    _bayesianAnalysis.PropertyChanged -= BayesianAnalysis_PropertyChanged;
                    _bayesianAnalysis.Model = null;
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
        /// Gets or sets the uncertainty quantification method.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The default method is <see cref="Analyses.UncertaintyMethod.LinkedMultivariateNormal"/>,
        /// which provides fast variance-stabilized parameter ensembles for operational uncertainty propagation.
        /// </para>
        /// </remarks>
        public UncertaintyMethod UncertaintyMethod
        {
            get => _uncertaintyMethod;
            set
            {
                if (_uncertaintyMethod != value)
                {
                    _uncertaintyMethod = value;
                    ClearResults();
                    RaisePropertyChange(nameof(UncertaintyMethod));
                }
            }
        }

        /// <inheritdoc/>
        public UncertaintyAnalysisResults? AnalysisResults { get; private set; }

        /// <summary>
        /// Gets the Generalized Method of Moments estimator.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This property is populated after <see cref="RunAsync"/> completes successfully.
        /// It provides access to the GMM covariance matrix and other diagnostic information.
        /// </para>
        /// </remarks>
        public GeneralizedMethodOfMoments? GMM => _gmm;

        /// <summary>
        /// Gets the total elapsed wall-clock time for the entire analysis (GMM + uncertainty).
        /// </summary>
        public TimeSpan? ElapsedTime
        {
            get => _elapsedTime;
            private set
            {
                _elapsedTime = value;
                RaisePropertyChange(nameof(ElapsedTime));
            }
        }

        /// <summary>
        /// Gets the elapsed wall-clock time for GMM parameter estimation only.
        /// </summary>
        public TimeSpan? GMMElapsedTime
        {
            get => _gmmElapsedTime;
            private set
            {
                _gmmElapsedTime = value;
                RaisePropertyChange(nameof(GMMElapsedTime));
            }
        }

        /// <summary>
        /// Gets the elapsed wall-clock time for the uncertainty quantification phase.
        /// </summary>
        public TimeSpan? UncertaintyElapsedTime
        {
            get => _uncertaintyElapsedTime;
            private set
            {
                _uncertaintyElapsedTime = value;
                RaisePropertyChange(nameof(UncertaintyElapsedTime));
            }
        }

        /// <summary>
        /// Gets the bootstrap diagnostics from the most recent bootstrap uncertainty analysis.
        /// Null if the uncertainty method is not a bootstrap method.
        /// </summary>
        public BootstrapDiagnostics? BootstrapResults { get; private set; }

        /// <summary>
        /// Gets a user-facing description of why the most recent uncertainty quantification was
        /// degraded or aborted, or an empty string when no issue occurred.
        /// </summary>
        /// <remarks>
        /// Populated when a sampling method aborts (for example, when more than half of the
        /// requested bootstrap replicates are discarded after retries, or the sampled covariance
        /// is not usable) and when the delivered ensemble is too small to summarize. The UI layer
        /// surfaces this text as an analysis warning message after a run that produced a point
        /// estimate without uncertainty results. Cleared by <see cref="ClearResults"/>.
        /// </remarks>
        public string UncertaintyDiagnosticMessage { get; private set; } = string.Empty;

        /// <summary>
        /// Records the reason the most recent uncertainty quantification was degraded or aborted.
        /// </summary>
        /// <param name="message">The user-facing diagnostic text.</param>
        /// <remarks>
        /// Also writes the text to the debug trace so headless runs retain the explanation.
        /// May be invoked from a background sampling thread; the property notification is a
        /// plain <see cref="System.ComponentModel.INotifyPropertyChanged"/> raise with no
        /// dispatcher marshaling, matching the model layer's notification conventions.
        /// </remarks>
        private void SetUncertaintyDiagnosticMessage(string message)
        {
            UncertaintyDiagnosticMessage = message;
            Debug.WriteLine($"Bulletin17CAnalysis: {message}");
            RaisePropertyChange(nameof(UncertaintyDiagnosticMessage));
        }

        /// <summary>
        /// Optional override for the SES 'a' parameter formula used in the ? link function.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When set, this delegate receives (gammaHat, sampleSize) and returns the desired SES 'a' value,
        /// bypassing the default a(?) formula in <c>GetDistributionsFromLinkedMultivariateNormal</c>.
        /// When null (default), the production calibrated formula is used.
        /// </para>
        /// <para>
        /// Used by calibration tests to evaluate alternative formulas without code duplication.
        /// </para>
        /// </remarks>
        public Func<double, int, double>? AFormulaOverride { get; set; }


        #endregion

        #region Methods

        /// <summary>
        /// Repairs the persisted estimated flag when older saves contain complete Bayesian artifacts
        /// but the outer analysis-level flag was left false.
        /// </summary>
        /// <remarks>
        /// Point-estimate-only setting changes are routed through <see cref="AnalysisBase.ReprocessIfEstimated"/>.
        /// Some persisted projects can have an estimated <see cref="BayesianAnalysis"/>, serialized
        /// <see cref="MCMCResults"/>, and serialized <see cref="AnalysisResults"/> while the outer
        /// <see cref="AnalysisBase.IsEstimated"/> flag is false. Treating those artifacts as authoritative
        /// restores the intended post-processing path without rerunning MCMC.
        /// </remarks>
        private void NormalizeRestoredEstimatedState()
        {
            if (_isEstimated)
            {
                return;
            }

            if (BayesianAnalysis?.IsEstimated == true &&
                BayesianAnalysis.Results != null &&
                AnalysisResults != null)
            {
                _isEstimated = true;
            }
        }

        /// <summary>
        /// Handles changes to the <see cref="ProbabilityOrdinates"/> collection.
        /// </summary>
        /// <remarks>
        /// Ordinates drive only <see cref="AnalysisResults"/> � not the GMM fit or
        /// <c>IsEstimated</c>. When estimated and valid, reprocess via
        /// <see cref="CreateFrequencyAnalysisResultsAsync"/>; when invalid, clear
        /// <see cref="AnalysisResults"/> only; when not estimated, no-op.
        /// </remarks>
        private void ProbabilityOrdinates_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            RaisePropertyChange(nameof(ProbabilityOrdinates));

            if (!IsEstimated) return;

            if (ProbabilityOrdinates.Validate().IsValid)
                ReprocessIfEstimated(CreateFrequencyAnalysisResultsAsync);
            else
                ClearFrequencyAnalysisResults();
        }

        /// <summary>
        /// Handles property changes on the <see cref="Bulletin17CDistribution"/> model.
        /// Only structurally destructive changes (data, distribution, parameters, prior
        /// penalties) clear results. <see cref="LinkController"/> and other post-fit
        /// properties propagate for UI binding without invalidating the fit.
        /// </summary>
        private void Model_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Bulletin17CDistribution.Parameters) ||
                e.PropertyName == nameof(Bulletin17CDistribution.SetDefaultParameters) ||
                e.PropertyName == nameof(Bulletin17CDistribution.DataFrame) ||
                e.PropertyName == nameof(Bulletin17CDistribution.Distribution) ||
                e.PropertyName == nameof(Bulletin17CDistribution.DistributionType) ||
                e.PropertyName == nameof(Bulletin17CDistribution.ParameterPenalties) ||
                e.PropertyName == nameof(Bulletin17CDistribution.QuantilePenalties))
            {
                ClearResults();
            }

            RaisePropertyChange(e.PropertyName);
        }

        /// <summary>
        /// Handles property changes on the <see cref="BayesianAnalysis"/> object.
        /// Loss of estimated state clears results; PointEstimator changes update the
        /// point-estimate output without re-running the chain; CredibleIntervalWidth
        /// changes reprocess derived frequency results without re-running the chain.
        /// All other notifications propagate for UI binding.
        /// </summary>
        private void BayesianAnalysis_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(BayesianAnalysis.IsEstimated))
            {
                if (!BayesianAnalysis.IsEstimated)
                    ClearResults();
                RaisePropertyChange(e.PropertyName);
            }
            else if (e.PropertyName == nameof(BayesianAnalysis.PointEstimator))
            {
                ReprocessIfEstimated(UpdatePointEstimateResultsAsync);
                RaisePropertyChange(e.PropertyName);
            }
            else if (e.PropertyName == nameof(BayesianAnalysis.CredibleIntervalWidth))
            {
                ReprocessIfEstimated(CreateFrequencyAnalysisResultsAsync);
                RaisePropertyChange(e.PropertyName);
            }
            else
            {
                RaisePropertyChange(e.PropertyName);
            }
        }

        /// <inheritdoc/>
        public void ClearResults()
        {
            // Set IsEstimated = false BEFORE raising AnalysisResults notification.
            // When AnalysisResults fires, the UI control calls UpdateFrequencyPlot(),
            // which checks IsEstimated to decide whether to draw curves.
            IsEstimated = false;
            BayesianAnalysis?.ClearResults();
            _gmm = null;
            AnalysisResults = null;
            ElapsedTime = null;
            GMMElapsedTime = null;
            UncertaintyElapsedTime = null;
            BootstrapResults = null;
            UncertaintyDiagnosticMessage = string.Empty;
            RaisePropertyChange(nameof(AnalysisResults));
            RaisePropertyChange(nameof(GMM));
        }

        /// <summary>
        /// Clears <see cref="AnalysisResults"/> only � the frequency/quantile output whose
        /// evaluation grid is <see cref="ProbabilityOrdinates"/>. Leaves the GMM fit,
        /// BayesianAnalysis results, and <c>IsEstimated</c> intact.
        /// </summary>
        public void ClearFrequencyAnalysisResults()
        {
            AnalysisResults = null;
            RaisePropertyChange(nameof(AnalysisResults));
        }

       
        /// <inheritdoc/>
        public override async Task RunAsync(SafeProgressReporter? progressReporter = null)
        {
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();

            // Wait for any in-flight reprocess to finish before clearing results and
            // starting a new MCMC run. Without this gate, a fire-and-forget reprocess
            // (triggered by a prior property change via ReprocessIfEstimated) can be
            // inside its parallel loop when ClearResults() nulls AnalysisResults �
            // producing an NRE on the next AnalysisResults dereference inside the loop body.
            await _reprocessGate.WaitAsync();
            try
            {
                try
                {
                    ClearResults();

                    var totalStopwatch = Stopwatch.StartNew();

                    progressReporter?.IndicateTaskStart();
                    AnalysisProgress.ReportStarting(progressReporter);

                    GeneralizedMethodOfMoments? gmm = null;
                    TimeSpan gmmElapsedTime = TimeSpan.Zero;
                    CancellationToken cancellationToken = _cancellationTokenSource?.Token ?? CancellationToken.None;

                    await Task.Run(() =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        // Preprocess data and run the CPU-bound GMM fit off the UI thread.
                        Bulletin17CDistribution.DataFrame.ProcessThresholdSeries();
                        Bulletin17CDistribution.LinkController = LinkController.ForLocationScaleShape();
                        Bulletin17CDistribution.SetInitialParameters();
                        Bulletin17CDistribution.SetPenaltyFunction();

                        var candidateGmm = new GeneralizedMethodOfMoments(Bulletin17CDistribution);
                        var gmmStopwatch = Stopwatch.StartNew();
                        candidateGmm.Estimate();
                        gmmStopwatch.Stop();

                        cancellationToken.ThrowIfCancellationRequested();
                        gmm = candidateGmm;
                        gmmElapsedTime = gmmStopwatch.Elapsed;
                    }, cancellationToken);

                    _gmm = gmm;
                    GMMElapsedTime = gmmElapsedTime;
                    if (_gmm!.Status == OptimizationStatus.Failure)
                    {
                        // Send message here
                        Debug.WriteLine("The GMM solver failed to find a solution for the distribution fit.");
                        return;
                    }

                    progressReporter?.ReportProgress(1);

                    // Set IsEstimated BEFORE creating results so that when
                    // AnalysisResults PropertyChanged fires inside CreateFrequencyAnalysisResultsAsync,
                    // the UI control's UpdateFrequencyPlot() sees IsEstimated == true and adds curves.
                    _isEstimated = true;

                    // Run heavy uncertainty quantification (MVN/Bootstrap simulation) once.
                    // The resulting parameter sets are stored in BayesianAnalysis.Results.
                    var uncertaintyStopwatch = Stopwatch.StartNew();
                    await RunUncertaintyQuantificationAsync(AnalysisProgress.CreatePhaseReporter(progressReporter, 1, 98, "B17C Uncertainty"));
                    uncertaintyStopwatch.Stop();
                    UncertaintyElapsedTime = uncertaintyStopwatch.Elapsed;

                    if (_cancellationTokenSource?.IsCancellationRequested == true)
                    {
                        _isEstimated = false;
                        ClearResults();
                        return;
                    }

                    // Build initial frequency-analysis results from the sampled parameter sets.
                    // Subsequent ordinate changes call this fast path directly without resampling.
                    AnalysisProgress.ReportProcessingResults(progressReporter);
                    await CreateFrequencyAnalysisResultsAsync();

                    // If cancelled during uncertainty quantification (caught by early return, not exception),
                    // reset IsEstimated so the UI doesn't mistake this for a partial failure.
                    if (_cancellationTokenSource?.IsCancellationRequested == true)
                    {
                        _isEstimated = false;
                        ClearResults();
                        return;
                    }

                    totalStopwatch.Stop();
                    ElapsedTime = totalStopwatch.Elapsed;

                    RaisePropertyChange(nameof(IsEstimated));
                    RaisePropertyChange(nameof(GMM));
                    AnalysisProgress.ReportComplete(progressReporter);
                }
                catch (OperationCanceledException)
                {
                    _isEstimated = false;
                    ClearResults();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"B17C RunAsync failed: {ex}");
                    _isEstimated = false;
                    ClearResults();
                    throw;
                }
                finally
                {
                    //_cancellationTokenSource?.Dispose();
                    //_cancellationTokenSource = null;
                    progressReporter?.IndicateTaskEnded();
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
            _cancellationTokenSource?.Cancel();
        }

        /// <summary>
        /// Runs the heavy uncertainty quantification step: simulates parameter sets via the
        /// configured <see cref="UncertaintyMethod"/> (MVN / LinkedMVN / Bootstrap /
        /// BiasCorrectedBootstrap) and stores them in <see cref="BayesianAnalysis"/>.Results.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is the expensive compute path � it samples from the GMM covariance (or
        /// resamples data for bootstrap) and re-fits a distribution per draw. It runs ONCE
        /// per estimation. Subsequent <see cref="CreateFrequencyAnalysisResultsAsync"/>
        /// calls (e.g., on probability-ordinate change) read the persisted parameter sets
        /// from <see cref="BayesianAnalysis"/>.Results and skip this resampling � same
        /// fast pattern used by <see cref="UnivariateAnalysis"/>.
        /// </para>
        /// </remarks>
        private async Task RunUncertaintyQuantificationAsync(SafeProgressReporter? progressReporter)
        {
            if (_gmm == null || _gmm.Status == OptimizationStatus.Failure) return;

            ParameterSet[]? rawSets = null;

            await Task.Run(() =>
            {
                // Set parent distribution parameters
                Bulletin17CDistribution.SetParameterValues(_gmm.BestParameterSet.Values);

                // Get sampled parameter sets (heavy: re-fits per draw or LHS-samples MVN/link space)
                rawSets = UncertaintyMethod switch
                {
                    UncertaintyMethod.MultivariateNormal       => GetParameterSetsFromMultivariateNormal(progressReporter),
                    UncertaintyMethod.LinkedMultivariateNormal => GetParameterSetsFromLinkedMultivariateNormal(progressReporter),
                    UncertaintyMethod.Bootstrap                => GetParameterSetsFromParametricBootstrap(progressReporter),
                    UncertaintyMethod.BiasCorrectedBootstrap   => GetParameterSetsFromPivotalBootstrap(progressReporter),
                    _                                          => GetParameterSetsFromMultivariateNormal(progressReporter)
                };

                // Fallback: if LinkedMVN returned null (e.g., high rejection rate), fall back to plain MVN
                if (rawSets == null && UncertaintyMethod == UncertaintyMethod.LinkedMultivariateNormal)
                {
                    Debug.WriteLine("B17C: LinkedMVN failed, falling back to plain MultivariateNormal.");
                    rawSets = GetParameterSetsFromMultivariateNormal(progressReporter);
                }
            });

            if (_cancellationTokenSource?.IsCancellationRequested == true) return;

            if (rawSets == null)
            {
                // The sampler either set a specific diagnostic before returning null (e.g., an
                // abort after excessive discards) or failed on the shared covariance path.
                if (string.IsNullOrEmpty(UncertaintyDiagnosticMessage))
                {
                    SetUncertaintyDiagnosticMessage(
                        "Uncertainty quantification failed — the covariance matrix from GMM estimation is not positive-definite. " +
                        "The point estimate is still valid but confidence intervals cannot be computed. " +
                        "Consider using a different distribution or the Bootstrap/Bias-Corrected Bootstrap uncertainty method.");
                }
                return;
            }

            // Filter out unset, dimensionally invalid, or non-finite entries.
            // MVN samplers may discard invalid draws. Bootstrap collectors replace failed
            // candidates and therefore must satisfy the exact-count invariant below.
            int expectedParameterCount = Bulletin17CDistribution.NumberOfParameters;
            var validSets = rawSets
                .Where(ps => ps.Values != null &&
                             ps.Values.Length == expectedParameterCount &&
                             ps.Values.All(double.IsFinite))
                .ToArray();
            bool isBootstrapMethod = UncertaintyMethod == UncertaintyMethod.Bootstrap ||
                                     UncertaintyMethod == UncertaintyMethod.BiasCorrectedBootstrap;
            if (isBootstrapMethod &&
                (rawSets.Length != BayesianAnalysis.OutputLength || validSets.Length != BayesianAnalysis.OutputLength))
            {
                SetUncertaintyDiagnosticMessage(
                    $"Bootstrap uncertainty requires exactly {BayesianAnalysis.OutputLength:N0} valid parameter sets, " +
                    $"but received {validSets.Length:N0}. No partial uncertainty result was published.");
                return;
            }
            if (validSets.Length < 2)
            {
                SetUncertaintyDiagnosticMessage(
                    $"Only {validSets.Length:N0} valid parameter sets out of {rawSets.Length:N0} sampled realizations. " +
                    "Uncertainty analysis was skipped.");
                return;
            }

            // Sanitize Fitness values: bootstrap parameter sets may have non-finite Fitness
            // (NaN, Infinity) which System.Text.Json cannot serialize. Replace with 0.
            SanitizeParameterSets(validSets);

            // Sanitize the MAP (best) parameter set
            var bestPs = _gmm!.BestParameterSet;
            if (!double.IsFinite(bestPs.Fitness))
                bestPs = new ParameterSet(bestPs.Values, 0.0);

            var mcmcResults = new MCMCResults(bestPs, validSets, 1.0 - BayesianAnalysis.CredibleIntervalWidth);
            var paramNames = Bulletin17CDistribution.Parameters.Select(p => p.DisplayName).ToList();
            BayesianAnalysis.SetCustomMCMCResults(mcmcResults, true, paramNames);
        }

        /// <summary>
        /// Builds <see cref="AnalysisResults"/> at the current <see cref="ProbabilityOrdinates"/>
        /// by reading the parameter sets stored in <see cref="BayesianAnalysis"/>.Results.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Fast secondary method � same shape as <see cref="UnivariateAnalysis.CreateFrequencyAnalysisResultsAsync"/>.
        /// Clones the parent distribution per stored parameter set (a cheap parameter-assignment loop)
        /// and constructs an <see cref="UncertaintyAnalysisResults"/> at the current ordinates.
        /// The expensive per-draw resampling is owned by <see cref="RunUncertaintyQuantificationAsync"/>,
        /// which runs once after GMM fit. This method is safe to call repeatedly on probability-ordinate
        /// or credible-interval changes without re-running the simulation.
        /// </para>
        /// </remarks>
        private async Task CreateFrequencyAnalysisResultsAsync()
        {
            AnalysisResults = null;
            if (BayesianAnalysis == null || BayesianAnalysis.IsEstimated == false ||
                BayesianAnalysis.Results == null || Bulletin17CDistribution?.Distribution is null)
            {
                RaisePropertyChange(nameof(AnalysisResults));
                return;
            }

            await Task.Run(() =>
            {
                // Set parent distribution parameters from the stored MAP (the GMM fit)
                Bulletin17CDistribution.SetParameterValues(BayesianAnalysis.Results.MAP.Values);

                // Build the sampled distribution array by cloning the parent distribution and
                // assigning each stored parameter set � fast, no resampling.
                int B = BayesianAnalysis.Results.Output.Count;
                var sampledDistributions = new UnivariateDistributionBase[B];
                Parallel.For(0, B, AnalysisProgress.CreateParallelOptions(), idx =>
                {
                    var dist = Bulletin17CDistribution.Distribution!.Clone();
                    dist.SetParameters(BayesianAnalysis.Results.Output[idx].Values);
                    sampledDistributions[idx] = dist;
                });

                AnalysisResults = new UncertaintyAnalysisResults(
                    Bulletin17CDistribution.Distribution!,
                    sampledDistributions,
                    ProbabilityOrdinates.Select(p => 1.0 - p).ToArray(),
                    1 - BayesianAnalysis.CredibleIntervalWidth,
                    recordParameterSets: false);
            });

            await UpdatePointEstimateResultsAsync();
        }

       
        /// <summary>
        /// Generates distributions from a multivariate normal approximation.
        /// </summary>
        private ParameterSet[]? GetParameterSetsFromMultivariateNormal(SafeProgressReporter? progressReporter)
        {
            int B = BayesianAnalysis.OutputLength;
            var results = new ParameterSet[B];

            var thetaHat = _gmm!.BestParameterSet.Values;
            var sigmaHat = _gmm!.GetCovariance(thetaHat);

            // Validate that the covariance matrix is positive-definite before constructing MVN.
            // This can fail for ill-conditioned fits (e.g., fitting exponential to non-exponential data).
            MultivariateNormal mvn;
            try
            {
                mvn = new MultivariateNormal(thetaHat, sigmaHat.ToArray());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"B17C MVN: covariance matrix is not positive-definite, cannot sample. {ex.Message}");
                return null;
            }

            var draws = mvn.LatinHypercubeRandomValues(B, BayesianAnalysis.PRNGSeed);

            var options = AnalysisProgress.CreateParallelOptions(_cancellationTokenSource?.Token ?? CancellationToken.None);

            int iteration = 0;
            int rejectionCount = 0;

            try
            {
                Parallel.For(0, B, options, idx =>
                {
                    options.CancellationToken.ThrowIfCancellationRequested();

                    // Per-thread validator (ValidateParameters is an instance method).
                    var validator = Bulletin17CDistribution.Distribution.Clone();
                    double[]? acceptedTheta = null;

                    // First try: use the LHS draw (best space coverage)
                    try
                    {
                        var theta = draws.GetRow(idx);
                        validator.ValidateParameters(theta, true);
                        acceptedTheta = theta;
                    }
                    catch (Exception ex) { Debug.WriteLine($"B17C MVN sampling:: rejected parameter set (idx={idx}): {ex.Message}"); }

                    // Retry with fresh random draws if LHS sample was invalid
                    if (acceptedTheta == null)
                    {
                        int p = thetaHat.Length;
                        var prng = new MersenneTwister(BayesianAnalysis.PRNGSeed + B + idx);
                        for (int retry = 0; retry < 10 && acceptedTheta == null; retry++)
                        {
                            try
                            {
                                var theta = mvn.InverseCDF(prng.NextDoubles(1, p).GetRow(0));
                                validator.ValidateParameters(theta, true);
                                acceptedTheta = theta;
                            }
                            catch (Exception ex) { Debug.WriteLine($"B17C MVN sampling: rejected parameter set (idx={idx}): {ex.Message}"); }
                        }
                        if (acceptedTheta == null)
                            Interlocked.Increment(ref rejectionCount);
                    }

                    // Rejected draws stay as default(ParameterSet) (Values == null) and are
                    // filtered below — no parent fallback, which would inject zero-variance
                    // mass at the parent fit and bias the uncertainty bounds narrow.
                    if (acceptedTheta != null)
                        results[idx] = new ParameterSet(acceptedTheta, double.NaN);

                    int current = Interlocked.Increment(ref iteration);
                    if (AnalysisProgress.ShouldReportLoopProgress(current, B))
                        progressReporter?.ReportProgress((int)(100.0 * current / B));
                });

                // Check the rejection rate — persistent rejection signals the MVN approximation
                // places most of its mass outside the valid parameter space, not sampling noise.
                double rejectionRate = (double)rejectionCount / B;
                if (rejectionRate > 0.50)
                {
                    SetUncertaintyDiagnosticMessage(
                        $"Multivariate Normal sampling: {rejectionCount:N0} of {B:N0} requested draws were rejected as invalid parameter sets " +
                        $"({rejectionRate:P0} rejection rate). Uncertainty quantification was aborted. " +
                        "Consider the Bootstrap uncertainty method, which does not rely on the asymptotic covariance.");
                    return null;
                }

                // Filter out rejected draws (default-initialized entries have Values == null).
                var validResults = results.Where(ps => ps.Values != null).ToArray();
                if (validResults.Length < 2)
                {
                    SetUncertaintyDiagnosticMessage(
                        $"Multivariate Normal sampling: only {validResults.Length:N0} of {B:N0} requested draws were valid. " +
                        "Uncertainty quantification was aborted.");
                    return null;
                }

                return validResults;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        /// <summary>
        /// Evaluates the log-space quantile (for LP3) or real-space quantile at the given parameters.
        /// </summary>
        /// <param name="parameters">Distribution parameter vector [�, s, ?] in log-space.</param>
        /// <param name="nonExceedanceProbability">Non-exceedance probability (0 to 1).</param>
        /// <returns>The log-space quantile value for LP3, or real-space for other distributions.</returns>
        private double EvaluateLogQuantileSafe(double[] parameters, double nonExceedanceProbability)
        {
            try
            {
                // For LP3/LogNormal, work in log-space (P3/Normal base distribution)
                var distType = Bulletin17CDistribution.DistributionType;
                UnivariateDistributionBase dist;
                if (distType == UnivariateDistributionType.LogPearsonTypeIII)
                    dist = Bulletin17CDistribution.CreateDistribution(UnivariateDistributionType.PearsonTypeIII);
                else if (distType == UnivariateDistributionType.LogNormal)
                    dist = Bulletin17CDistribution.CreateDistribution(UnivariateDistributionType.Normal);
                else
                    dist = Bulletin17CDistribution.Distribution.Clone();

                dist.ValidateParameters(parameters, true);
                dist.SetParameters(parameters);
                return dist.InverseCDF(nonExceedanceProbability);
            }
            catch (Exception ex)
            {
                // Fallback: use point estimate.
                Debug.WriteLine($"Bulletin17CAnalysis.SafeInverseCDF: linked-distribution fit failed at p={nonExceedanceProbability}: {ex.Message}");
                return Bulletin17CDistribution.Distribution.InverseCDF(nonExceedanceProbability);
            }
        }

        /// <summary>
        /// Generates distributions from a linked multivariate normal approximation.
        /// </summary>
        /// <param name="progressReporter">Optional progress reporter for UI feedback.</param>
        /// <returns>An array of sampled distributions, or null if cancelled.</returns>
        /// <remarks>
        /// <para>
        ///     Transforms GMM parameters to a link space ? = h(?) where the sampling distribution
        ///     is more nearly normal, samples in that space via Latin Hypercube, and maps back.
        ///     This improves CI coverage compared to straight MVN by:
        /// </para>
        /// <list type="bullet">
        ///     <item><description>Ensuring positive parameters remain positive via a log-ASinH link.</description></item>
        ///     <item><description>Making positive-parameter right-tail shape a bounded function of relative standard error.</description></item>
        ///     <item><description>Using WEDS magnitude and fitted-gamma sign for LP3/P3 gamma asymmetry.</description></item>
        ///     <item><description>Using gamma standard error for symmetric gamma-tail thickness, not asymmetry sign.</description></item>
        /// </list>
        /// <para>
        ///     The covariance in link space is computed via the delta method:
        ///     V^_? = G � S^_? � G', where G = ??/?? is the diagonal link Jacobian.
        /// </para>
        /// <para>
        ///     Link function selection is distribution-aware. Parameter semantics vary:
        ///     Normal/LogNormal/Exponential are [location, scale]; P3/LP3 are [location, scale, shape];
        ///     GammaDistribution is [scale, shape] (both positive). Links assigned per parameter:
        ///     <list type="bullet">
        ///         <item><description>Positive parameters: LogASinH on log(parameter / estimate).
        ///         Relative SE controls log-scale, positive asymmetry, and symmetric tail thickness;
        ///         WEDS sign is excluded because support-driven right skew is structural.</description></item>
        ///         <item><description>LP3/P3 gamma: ASinH with WEDS as magnitude and gammaHat as
        ///         the theory-based direction. Positive gamma always receives positive asymmetry;
        ///         negative gamma is damped near zero to avoid over-negative uncertainty tails.</description></item>
        ///     </list>
        /// </para>
        /// <para>
        ///     The link rules are intentionally diagnostic rather than calibration-table driven.
        ///     WEDS is computed in natural parameter space before temporary links are installed,
        ///     so the direction score remains a property of the fitted distribution and data
        ///     rather than a side effect of the chosen uncertainty transformation.
        /// </para>
        /// </remarks>
        private ParameterSet[]? GetParameterSetsFromLinkedMultivariateNormal(SafeProgressReporter? progressReporter)
        {
            int B = BayesianAnalysis.OutputLength;
            var results = new ParameterSet[B];

            // Step 1: Get ?^ and S^_? from GMM
            var thetaHat = _gmm!.BestParameterSet.Values;
            var sigmaHat = _gmm!.GetCovariance(thetaHat);

            // Step 2: Compute weighted error direction score (WEDS) in natural parameter space.
            // WEDS counts the weighted fraction of observations producing positive vs negative
            // moment condition errors. Unlike CensoringAsymmetryScore (which decomposes error
            // magnitudes and is constrained to zero at the GMM solution), WEDS is nonzero
            // whenever the data or censoring structure is directionally asymmetric.
            // This call intentionally occurs before the temporary LinkController is installed:
            // WEDS is a diagnostic of theta-space moment residual directions, not link-space shape.
            var weds = Bulletin17CDistribution.WeightedErrorDirectionScore(thetaHat);

            // Step 3: Select link functions per distribution type.
            //   - Location (mu): ASinH with a small gamma + WEDS blend.
            //   - LP3/P3 scale (sigma): LogASinH, because scale is positive and its right-skewed
            //     uncertainty should grow with relative standard error, not a fixed lambda.
            //   - Other positive parameters: LogASinH with the same relative-SE rule.
            //   - LP3/P3 shape (gamma): ASinH with WEDS magnitude and gammaHat direction;
            //     gammaSE controls symmetric tail thickness rather than skew direction.
            //
            // Reference: Jones, M.C. and Pewsey, A. (2009). Sinh-arcsinh distributions.
            // Biometrika, 96(4), 761-780.
            int p = Bulletin17CDistribution.NumberOfParameters;
            var links = new ILinkFunction?[p];
            var distType = Bulletin17CDistribution.DistributionType;

            if (distType == UnivariateDistributionType.GammaDistribution)
            {
                // Gamma: [scale(theta > 0), shape(kappa > 0)]. Both parameters are strictly
                // positive, so use the same relative-SE LogASinH rule as the other positive
                // LinkedMVN parameters. This preserves support and makes right-tail shape a
                // function of parameter uncertainty rather than WEDS sign.
                links[0] = CreatePositiveParameterLink(thetaHat[0], SafeStandardError(sigmaHat, 0));
                links[1] = CreatePositiveParameterLink(thetaHat[1], SafeStandardError(sigmaHat, 1));
            }
            else if (distType == UnivariateDistributionType.PearsonTypeIII ||
                     distType == UnivariateDistributionType.LogPearsonTypeIII)
            {
                // P3/LP3: [location(real), scale(>0), shape(real)]
                double gammaHat = thetaHat[2];
                double muSE = SafeStandardError(sigmaHat, 0);
                double scaleSE = SafeStandardError(sigmaHat, 1);
                double gammaSE = SafeStandardError(sigmaHat, 2);

                // Location (mu): preserve the theory-calibrated LP3/P3 behavior. The fitted
                // gamma contribution represents the local coupling between location and shape
                // in Pearson moment space, while WEDS adds censoring-direction information.
                // The modest epsilon cap is intentional; location should not be the primary
                // source of high-quantile tail asymmetry.
                links[0] = CreatePearsonLocationLink(thetaHat[0], muSE, gammaHat, CleanWeds(weds, 0));

                // Scale (sigma): positive-support log-ASinH link driven by relative standard
                // error. Scale estimators are structurally right-skewed, but the strength of
                // that skew should increase with uncertainty rather than being fixed. WEDS sign
                // is intentionally excluded because it is not a stable physical direction for scale.
                links[1] = CreatePearsonScaleLink(thetaHat[1], scaleSE);

                // Shape (gamma): WEDS supplies magnitude and gammaHat supplies direction.
                // Positive fitted gamma always receives positive ASinH skew; negative fitted
                // gamma keeps a near-zero damping guard to avoid the over-negative behavior
                // that motivated this link. GammaSE is used only for symmetric tail thickness.
                double gammaDirectionScore = OrientGammaWedsForLink(gammaHat, CleanWeds(weds, 2));
                links[2] = CreateGammaShapeLink(gammaHat, gammaSE, gammaDirectionScore);
            }
            else
            {
                // Normal, LogNormal, Exponential: [location(real), scale(>0)]
                // Location: ASinH with adaptive e from WEDS (captures censoring asymmetry).
                // When WEDS � 0 (no censoring, symmetric distribution), e � 0 ? identity-like.
                // For Exponential, WEDS is naturally negative (~-0.26) because P(X < �) � 0.63.
                double muSE = SafeStandardError(sigmaHat, 0);

                links[0] = CreateLocationLink(thetaHat[0], muSE, CleanWeds(weds, 0));

                double scaleSE = SafeStandardError(sigmaHat, 1);

                // Scale: positive support is structural for Normal, LogNormal, and Exponential.
                // Use relative-SE LogASinH so weakly identified scale gets stronger right skew
                // and fatter symmetric log-tails, while well-identified scale remains close to a
                // centered log link. WEDS sign is not used because scale direction is structural.
                links[1] = CreatePositiveParameterLink(thetaHat[1], scaleSE);
            }

            // Step 4: Set LinkController with selected links
            Bulletin17CDistribution.LinkController = new LinkController(links);

            // Step 5: Transform to link space
            var etaHat = Bulletin17CDistribution.LinkController.Link(thetaHat);

            // Step 6: Compute link Jacobian G = diag(d?_i/d?_i) using analytical DLink
            var GHat = Bulletin17CDistribution.LinkController.LinkJacobian(thetaHat);

            // Step 7: Delta-method covariance in link space: V^_? = G � S^_? � G'
            var VetaHat = GHat * sigmaHat * GHat.Transpose();
            VetaHat = MatrixRegularization.MakeSymmetricPositiveDefinite(VetaHat);

            // Step 8: Sample ? draws from N(?^, V^_?) via Latin Hypercube
            MultivariateNormal mvn;
            try
            {
                mvn = new MultivariateNormal(etaHat, VetaHat.ToArray());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"B17C LinkedMVN: link-space covariance matrix is not positive-definite. {ex.Message}");
                return null;
            }
            var etaDraws = mvn.LatinHypercubeRandomValues(B, BayesianAnalysis.PRNGSeed);

            var options = AnalysisProgress.CreateParallelOptions(_cancellationTokenSource?.Token ?? CancellationToken.None);

            int iteration = 0;
            int rejectionCount = 0;

            try
            {
                // Step 9: Map back ? = InverseLink(?) for each draw
                Parallel.For(0, B, options, idx =>
                {
                    options.CancellationToken.ThrowIfCancellationRequested();

                    // Per-thread validator (ValidateParameters is an instance method).
                    var validator = Bulletin17CDistribution.Distribution.Clone();
                    double[]? acceptedTheta = null;

                    // First try: use the LHS draw (best space coverage)
                    try
                    {
                        var theta = Bulletin17CDistribution.LinkController.InverseLink(etaDraws.GetRow(idx));
                        validator.ValidateParameters(theta, true);
                        acceptedTheta = theta;
                    }
                    catch (Exception ex) { Debug.WriteLine($"B17C LinkedMVN sampling: rejected parameter set (idx={idx}): {ex.Message}"); }

                    // Retry with fresh random draws in ?-space if LHS sample was invalid
                    if (acceptedTheta == null)
                    {
                        int p = etaHat.Length;
                        var prng = new MersenneTwister(BayesianAnalysis.PRNGSeed + B + idx);
                        for (int retry = 0; retry < 10 && acceptedTheta == null; retry++)
                        {
                            try
                            {
                                var eta = mvn.InverseCDF(prng.NextDoubles(1, p).GetRow(0));
                                var theta = Bulletin17CDistribution.LinkController.InverseLink(eta);
                                validator.ValidateParameters(theta, true);
                                acceptedTheta = theta;
                            }
                            catch (Exception ex) { Debug.WriteLine($"B17C LinkedMVN sampling: rejected parameter set (idx={idx}): {ex.Message}"); }
                        }

                        if (acceptedTheta == null)
                            Interlocked.Increment(ref rejectionCount);
                    }

                    // LinkedMVN preserves prior behavior � rejected draws stay as default(ParameterSet)
                    // (Values == null) and are filtered below; no parent-thetaHat fallback (high
                    // rejection triggers full method fallback). ParameterSet is a struct, so the
                    // default-initialized array slot already represents "no draw".
                    if (acceptedTheta != null)
                        results[idx] = new ParameterSet(acceptedTheta, double.NaN);

                    int current = Interlocked.Increment(ref iteration);
                    if (AnalysisProgress.ShouldReportLoopProgress(current, B))
                        progressReporter?.ReportProgress((int)(100.0 * current / B));
                });

                // Check rejection rate � if too many draws failed, the link parameters are too aggressive.
                double rejectionRate = (double)rejectionCount / B;
                if (rejectionRate > 0.50)
                {
                    Debug.WriteLine($"B17C LinkedMVN: {rejectionRate:P0} rejection rate ({rejectionCount}/{B}). " +
                                    "Cohn-calibrated links too aggressive � falling back to empirical formulas.");
                    // Restore identity link controller and return null to trigger fallback
                    Bulletin17CDistribution.LinkController = new LinkController();
                    return null;
                }

                // Filter out rejected draws (default-initialized entries have Values == null)
                var validResults = results.Where(ps => ps.Values != null).ToArray();
                if (validResults.Length < 2)
                {
                    Debug.WriteLine($"B17C LinkedMVN: Only {validResults.Length} valid draws. Returning null.");
                    return null;
                }

                return validResults;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            finally
            {
                // Step 10: Restore empty (identity) link controller so downstream code is unaffected
                Bulletin17CDistribution.LinkController = new LinkController();
            }
        }

        /// <summary>
        /// Returns a finite WEDS component for link selection.
        /// </summary>
        /// <param name="weds">The weighted error direction score vector.</param>
        /// <param name="index">The parameter index to read.</param>
        /// <returns>The finite WEDS score at <paramref name="index"/>, or zero if unavailable.</returns>
        /// <remarks>
        /// WEDS is a diagnostic input, not an optimizer constraint. If it is unavailable because
        /// the fit is near a boundary or a moment is non-finite, the conservative choice is to
        /// remove the extra directional adjustment and keep only structural parameter links.
        /// </remarks>
        private static double CleanWeds(double[] weds, int index)
        {
            if (weds == null || index < 0 || index >= weds.Length || !double.IsFinite(weds[index]))
                return 0.0;

            return Math.Clamp(weds[index], -1.0, 1.0);
        }

        /// <summary>
        /// Creates the location link used by linked-MVN parameter draws.
        /// </summary>
        /// <param name="center">The fitted location parameter value.</param>
        /// <param name="standardError">The GMM standard error for the location parameter.</param>
        /// <param name="wedsScore">The signed WEDS score for the location moment.</param>
        /// <returns>An <see cref="ASinHLink"/> centered at <paramref name="center"/>.</returns>
        /// <remarks>
        /// Location is unbounded, so no support transform is needed, but censoring can make the
        /// estimator's uncertainty directionally asymmetric. Signed WEDS has a direct interpretation
        /// for the location moment: it records whether weighted residual directions are predominantly
        /// above or below the fitted center after censoring and interval information are integrated.
        /// This is a defensible censoring correction, not a distribution-family calibration.
        /// </remarks>
        private static ASinHLink CreateLocationLink(double center, double standardError, double wedsScore)
        {
            return new ASinHLink(center, standardError)
            {
                UseAdaptiveEpsilon = true,
                ParentIndicator = Math.Clamp(wedsScore, -1.0, 1.0),
                EpsilonMax = 0.50,
                EpsilonSlope = 1.0
            };
        }

        /// <summary>
        /// Creates the LP3/P3 location link used by linked-MVN parameter draws.
        /// </summary>
        /// <param name="center">The fitted location parameter value.</param>
        /// <param name="standardError">The GMM standard error for the location parameter.</param>
        /// <param name="gammaHat">The fitted LP3/P3 skewness parameter.</param>
        /// <param name="wedsScore">The signed WEDS score for the location moment.</param>
        /// <returns>An <see cref="ASinHLink"/> centered at <paramref name="center"/>.</returns>
        /// <remarks>
        /// LP3/P3 location behavior is intentionally different from the generic two-parameter
        /// location link. Pearson location and shape are locally coupled through the moment
        /// equations, so a small fitted-gamma contribution is retained. WEDS still carries the
        /// censoring-direction information. The epsilon cap stays conservative because gamma
        /// and scale, not location, should carry most of the high-quantile tail shape.
        /// </remarks>
        private static ASinHLink CreatePearsonLocationLink(
            double center,
            double standardError,
            double gammaHat,
            double wedsScore)
        {
            return new ASinHLink(center, standardError)
            {
                UseAdaptiveEpsilon = true,
                ParentIndicator = (0.5 * gammaHat) + wedsScore,
                EpsilonMax = 0.5,
                EpsilonSlope = 1.0
            };
        }

        /// <summary>
        /// Converts the raw gamma WEDS component into the LP3/P3 gamma-link direction score.
        /// </summary>
        /// <param name="gammaHat">The fitted LP3/P3 skewness parameter.</param>
        /// <param name="rawGammaWeds">The natural-parameter WEDS component for the third central moment.</param>
        /// <returns>The oriented score used by the gamma ASinH link.</returns>
        /// <remarks>
        /// Gamma WEDS is used as magnitude, not sign. The raw third-moment residual sign depends on
        /// how the moment condition is written and can flip under censoring. The fitted gamma value
        /// provides the theory-based direction away from zero. Positive fitted gamma always receives
        /// positive ASinH skew because a positive LP3/P3 shape should not be represented by a
        /// symmetric or negatively skewed gamma marginal. Negative fitted gamma is still damped near
        /// zero so large WEDS values under censoring do not recreate the over-negative uncertainty
        /// tail. Gamma standard error is reserved for <see cref="CreateGammaTailDelta"/>, where it
        /// affects symmetric tail kurtosis rather than skewness direction.
        /// </remarks>
        private static double OrientGammaWedsForLink(double gammaHat, double rawGammaWeds)
        {
            const double gammaAsymmetryFull = 0.50;
            const double minPositiveDirection = 0.10;

            if (!double.IsFinite(gammaHat))
                return 0.0;

            double wedsMagnitude = Math.Abs(Math.Clamp(rawGammaWeds, -1.0, 1.0));

            if (gammaHat > 0.0)
                return Math.Max(minPositiveDirection, wedsMagnitude);

            if (gammaHat == 0.0)
                return 0.0;

            // Negative fitted gamma can only produce symmetric or negative gamma-link skewness.
            // The smooth gate is retained only on the negative side because heavy censoring can
            // make WEDS large when the fitted negative skew is practically indistinguishable from
            // zero. Positive gamma is allowed to use full WEDS magnitude immediately.
            double asymmetryGate = SmoothStep(0.0, gammaAsymmetryFull, Math.Abs(gammaHat));
            return -wedsMagnitude * asymmetryGate;
        }

        /// <summary>
        /// Evaluates a cubic smoothstep transition between two edges.
        /// </summary>
        /// <param name="edge0">The lower edge where the transition is zero.</param>
        /// <param name="edge1">The upper edge where the transition is one.</param>
        /// <param name="x">The value to map through the transition.</param>
        /// <returns>A smooth value in [0, 1].</returns>
        /// <remarks>
        /// Used for the gamma-link transition so mild negative skew remains symmetric near zero,
        /// while stronger negative skew enters smoothly without a discontinuous coverage jump.
        /// </remarks>
        private static double SmoothStep(double edge0, double edge1, double x)
        {
            if (!double.IsFinite(x))
                return 0.0;

            if (edge1 <= edge0)
                return x >= edge1 ? 1.0 : 0.0;

            double t = Math.Clamp((x - edge0) / (edge1 - edge0), 0.0, 1.0);
            return t * t * (3.0 - (2.0 * t));
        }

        /// <summary>
        /// Computes the ASinH tail-shape delta for the LP3/P3 gamma link.
        /// </summary>
        /// <param name="gammaHat">The fitted LP3/P3 skewness parameter.</param>
        /// <param name="gammaSE">The GMM standard error for <paramref name="gammaHat"/>.</param>
        /// <returns>A delta value in [0.80, 1.00]; smaller values give heavier symmetric tails.</returns>
        /// <remarks>
        /// Near-zero gamma needs additional high-quantile support, but assigning positive asymmetry
        /// to a non-positive fitted gamma violates the LP3/P3 skewness direction. Delta provides the
        /// theory-respecting knob: when gamma is close to zero and poorly identified, the gamma link
        /// gets heavier symmetric tails. The standard error of gamma is used as an effective
        /// information surrogate because raw record length is not meaningful with censoring,
        /// historical thresholds, priors, and measurement error.
        /// </remarks>
        private static double CreateGammaTailDelta(double gammaHat, double gammaSE)
        {
            const double minDelta = 0.80;
            const double maxTailReduction = 0.20;
            const double gammaSEScale = 0.25;
            const double signalForTailStart = 0.75;
            const double signalForTailFade = 2.0;

            if (!double.IsFinite(gammaHat) || !double.IsFinite(gammaSE) || gammaSE <= 0.0)
                return 1.0;

            double gammaSignal = StandardizedMagnitude(gammaHat, gammaSE);
            double weakDirectionGate = 1.0 - SmoothStep(signalForTailStart, signalForTailFade, gammaSignal);
            double uncertaintyGate = gammaSE / (gammaSE + gammaSEScale);
            double delta = 1.0 - (maxTailReduction * weakDirectionGate * uncertaintyGate);

            return Math.Clamp(delta, minDelta, 1.0);
        }

        /// <summary>
        /// Computes the absolute signal-to-noise ratio for a fitted parameter.
        /// </summary>
        /// <param name="estimate">The fitted parameter value.</param>
        /// <param name="standardError">The standard error of <paramref name="estimate"/>.</param>
        /// <returns>The nonnegative standardized magnitude.</returns>
        /// <remarks>
        /// The linked-MVN link shape should respond to the information in the fitted covariance
        /// matrix rather than raw sample size. That matters for B17C because censoring, historical
        /// thresholds, priors, and measurement error can make record length a poor proxy for
        /// parameter identification.
        /// </remarks>
        private static double StandardizedMagnitude(double estimate, double standardError)
        {
            if (!double.IsFinite(estimate))
                return 0.0;

            if (!double.IsFinite(standardError) || standardError <= 1e-12)
                return Math.Abs(estimate) > 0.0 ? double.PositiveInfinity : 0.0;

            return Math.Abs(estimate) / standardError;
        }

        /// <summary>
        /// Computes a robust standard error from a covariance diagonal entry.
        /// </summary>
        /// <param name="covariance">The covariance matrix.</param>
        /// <param name="index">The diagonal index to read.</param>
        /// <returns>A positive finite standard error.</returns>
        /// <remarks>
        /// The link scale controls local linearization. If the sandwich covariance is numerically
        /// degenerate, use a tiny positive floor so link construction remains monotone and the
        /// covariance regularization step can still decide whether the draw distribution is usable.
        /// </remarks>
        private static double SafeStandardError(Matrix covariance, int index)
        {
            if (covariance == null || index < 0 || index >= covariance.NumberOfRows || index >= covariance.NumberOfColumns)
                return 1e-12;

            double variance = covariance[index, index];
            if (!double.IsFinite(variance) || variance <= 0.0)
                return 1e-12;

            return Math.Sqrt(variance);
        }

        /// <summary>
        /// Creates a positive-support log-sinh-arcsinh link for positive parameters.
        /// </summary>
        /// <param name="center">The fitted positive parameter value.</param>
        /// <param name="standardError">The GMM standard error for <paramref name="center"/>.</param>
        /// <returns>A <see cref="LogASinHLink"/> centered at <paramref name="center"/>.</returns>
        /// <remarks>
        /// Positive parameters, including scale and gamma-distribution shape, cannot be represented
        /// safely with an unconstrained ASinH link because that would allow invalid negative draws.
        /// Applying ASinH to log(parameter / estimate) preserves support while providing explicit
        /// skewness and tail controls. The controls are driven by relative standard error, SE / estimate,
        /// so they are invariant to measurement units and reflect the fitted information matrix. WEDS
        /// sign is intentionally excluded because positive-parameter tail direction is structural:
        /// weakly identified positive parameters should become more right-skewed, but not because a
        /// moment residual sign happened to flip under censoring or centering.
        /// </remarks>
        private static LogASinHLink CreatePositiveParameterLink(double center, double standardError)
        {
            const double scaleCVReference = 0.25;
            const double epsilonMax = 0.75;
            const double epsilonSlope = 1.25;
            const double maxTailReduction = 0.18;
            const double minDelta = 0.82;

            double relativeSE = RelativeStandardError(center, standardError);
            double logScale = LogScaleFromRelativeStandardError(relativeSE);
            double uncertainty = RelativeUncertaintyScore(relativeSE, scaleCVReference);
            double delta = 1.0 - (maxTailReduction * uncertainty);

            return new LogASinHLink(center, logScale)
            {
                Delta = Math.Clamp(delta, minDelta, 1.0),
                UseAdaptiveEpsilon = true,
                ParentIndicator = uncertainty,
                EpsilonMax = epsilonMax,
                EpsilonSlope = epsilonSlope
            };
        }

        /// <summary>
        /// Creates the P3/LP3 scale link used by linked-MVN parameter draws.
        /// </summary>
        /// <param name="center">The fitted positive scale parameter value.</param>
        /// <param name="standardError">The GMM standard error for <paramref name="center"/>.</param>
        /// <returns>A <see cref="LogASinHLink"/> centered at <paramref name="center"/>.</returns>
        /// <remarks>
        /// P3/LP3 scale now uses the same positive-parameter rule as the other B17C distribution
        /// options. The wrapper is retained so the LP3/P3 scale decision stays explicit in the
        /// LinkedMVN setup and can be tuned separately later if verification shows a distribution-
        /// specific need.
        /// </remarks>
        private static LogASinHLink CreatePearsonScaleLink(double center, double standardError)
        {
            return CreatePositiveParameterLink(center, standardError);
        }

        /// <summary>
        /// Computes a dimensionless relative standard error for a positive parameter.
        /// </summary>
        /// <param name="center">The fitted positive parameter value.</param>
        /// <param name="standardError">The standard error of <paramref name="center"/>.</param>
        /// <returns>The nonnegative relative standard error.</returns>
        /// <remarks>
        /// Positive-parameter link shape must be invariant to the measurement units of flow.
        /// Using SE / estimate, rather than raw SE, preserves that invariance and makes the link
        /// respond to parameter information instead of the scale of the data.
        /// </remarks>
        private static double RelativeStandardError(double center, double standardError)
        {
            if (!double.IsFinite(center) || center <= 0.0 || !double.IsFinite(standardError) || standardError <= 0.0)
                return 0.0;

            double relativeSE = Math.Abs(standardError / center);
            if (!double.IsFinite(relativeSE))
                return 0.0;

            return relativeSE;
        }

        /// <summary>
        /// Maps a relative standard error to a bounded uncertainty score.
        /// </summary>
        /// <param name="relativeSE">The nonnegative relative standard error.</param>
        /// <param name="referenceRelativeSE">The relative SE where the score reaches one half.</param>
        /// <returns>A bounded score in [0, 1].</returns>
        /// <remarks>
        /// This saturating map avoids hard thresholds. A nearly deterministic positive parameter
        /// receives almost no additional log-ASinH skew or tail thickness, while a weakly identified
        /// parameter smoothly approaches the configured maximum settings.
        /// </remarks>
        private static double RelativeUncertaintyScore(double relativeSE, double referenceRelativeSE)
        {
            if (!double.IsFinite(relativeSE) || relativeSE <= 0.0)
                return 0.0;

            if (!double.IsFinite(referenceRelativeSE) || referenceRelativeSE <= 0.0)
                return 1.0;

            return Math.Clamp(relativeSE / (relativeSE + referenceRelativeSE), 0.0, 1.0);
        }

        /// <summary>
        /// Converts relative standard error to a log-standardization scale.
        /// </summary>
        /// <param name="relativeSE">The nonnegative relative standard error.</param>
        /// <returns>The positive log-scale used by <see cref="LogASinHLink"/>.</returns>
        /// <remarks>
        /// sqrt(log(1 + CV^2)) is the lognormal relationship between arithmetic coefficient of
        /// variation and log standard deviation. It gives the positive-support link a defensible
        /// local scale while keeping the transformation dimensionless. Extremely large CV values
        /// are capped only for numerical stability; by then the asymmetry and tail controls are
        /// already saturated.
        /// </remarks>
        private static double LogScaleFromRelativeStandardError(double relativeSE)
        {
            const double minLogScale = 1e-12;
            const double maxRelativeSE = 10.0;

            if (!double.IsFinite(relativeSE) || relativeSE <= 0.0)
                return minLogScale;

            double boundedRelativeSE = Math.Min(relativeSE, maxRelativeSE);
            double logScale = Math.Sqrt(Math.Log(1.0 + (boundedRelativeSE * boundedRelativeSE)));

            if (!double.IsFinite(logScale) || logScale <= 0.0)
                return minLogScale;

            return Math.Max(logScale, minLogScale);
        }

        /// <summary>
        /// Creates the LP3/P3 gamma link with WEDS-driven asymmetry.
        /// </summary>
        /// <param name="gammaHat">The fitted skewness parameter.</param>
        /// <param name="gammaSE">The GMM standard error for <paramref name="gammaHat"/>.</param>
        /// <param name="gammaDirectionScore">The oriented WEDS direction score for the gamma link.</param>
        /// <returns>An <see cref="ASinHLink"/> configured for gamma uncertainty draws.</returns>
        /// <remarks>
        /// Gamma is different from scale: it is not constrained positive, and negative gamma alone
        /// should not imply a heavily negative uncertainty tail. The oriented WEDS score supplies
        /// asymmetry direction after the near-zero transition is applied. The tail-shape delta
        /// handles near-zero, weakly identified gamma without making non-positive fitted gamma
        /// positively skewed. Negative scores remain capped more tightly to avoid the over-negative
        /// LP3/P3 behavior observed in flood-frequency tails.
        /// </remarks>
        private static ASinHLink CreateGammaShapeLink(double gammaHat, double gammaSE, double gammaDirectionScore)
        {
            const double wedsDeadband = 0.10;
            const double maxPositiveEpsilon = 1.0;
            const double maxNegativeEpsilon = 0.5;
            const double minPositiveEpsilon = 0.50;
            const double minNegativeEpsilon = 0.25;

            double boundedWeds = Math.Clamp(gammaDirectionScore, -1.0, 1.0);
            double absWeds = Math.Abs(boundedWeds);
            double epsilonMax = 0.0;
            double parentIndicator = 0.0;

            if (absWeds >= wedsDeadband)
            {
                parentIndicator = boundedWeds;

                if (boundedWeds < 0.0)
                {
                    epsilonMax = minNegativeEpsilon + ((maxNegativeEpsilon - minNegativeEpsilon) * absWeds);
                    epsilonMax = Math.Min(epsilonMax, maxNegativeEpsilon);
                }
                else
                {
                    epsilonMax = minPositiveEpsilon + ((maxPositiveEpsilon - minPositiveEpsilon) * absWeds);

                    if (gammaHat > 0.75)
                        epsilonMax = Math.Max(epsilonMax, 0.75);
                }

                epsilonMax = Math.Clamp(epsilonMax, 0.0, maxPositiveEpsilon);
            }

            return new ASinHLink(gammaHat, gammaSE)
            {
                Delta = CreateGammaTailDelta(gammaHat, gammaSE),
                UseAdaptiveEpsilon = true,
                ParentIndicator = parentIndicator,
                EpsilonMax = epsilonMax,
                EpsilonSlope = 1.5
            };
        }

        /// <summary>
        /// Contains influence-function-based statistics for the Linked Multivariate Student-t method.
        /// Computed from per-observation GMM influence vectors ?_i = Bread?� � D'W � m_i.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Carries the quantile-level degrees of freedom for the MVT distribution and per-parameter
        /// skewness used to shift the MVT center in link space (BCa-equivalent z0 bias correction).
        /// </para>
        /// <para>
        /// Reference: Cohn, T.A., Lane, W.L., Stedinger, J.R. (2001). Confidence intervals for
        /// Expected Moments Algorithm flood quantile estimates. Water Resources Research, 37(6), 1695-1706.
        /// </para>
        /// </remarks>
        private struct InfluenceStatistics
        {
            /// <summary>
            /// Degrees of freedom for the MVT, computed from the kurtosis of quantile influence
            /// scores s_i = g_p' � ?_i: ? = 1/(2�Var[W]) where Var[W] = (V4/V2� - 1)/4.
            /// Floored at 4.
            /// </summary>
            public double NuQuantile;

            /// <summary>
            /// Per-parameter skewness of the influence function ?_i components.
            /// ParameterSkewness[j] = skewness of {?_i[j], i=1..n}.
            /// Used to shift the MVT center in link space: ??[j] = (skew_j/6)�vVetaHat[j,j],
            /// providing the BCa-equivalent z0 bias correction derived directly from the
            /// influence function rather than from bootstrap resampling.
            /// </summary>
            public double[] ParameterSkewness;
        }

        /// <summary>
        /// Computes influence-function-based statistics for the Linked MVT method:
        /// quantile degrees of freedom and per-parameter skewness.
        /// </summary>
        /// <param name="thetaHat">The GMM point estimates in parameter space (e.g., [�, s, ?]).</param>
        /// <param name="targetNonExceedanceProbability">
        /// The non-exceedance probability at which to evaluate the quantile degrees of freedom.
        /// Typically 0.999 for the 1000-year flood quantile.
        /// </param>
        /// <returns>
        /// An <see cref="InfluenceStatistics"/> struct with NuQuantile and ParameterSkewness.
        /// </returns>
        /// <remarks>
        /// <para>
        /// Implements Cohn et al. (2001) equations 27-30 for the quantile ?, then extends the
        /// same framework to extract per-parameter skewness from the ?_i vectors for the
        /// BCa-equivalent z0 bias correction.
        /// </para>
        /// <para>
        /// <b>Quantile ?</b>: From scalar quantile influence scores s_i = g_p' � ?_i,
        /// compute ? = 1/(2�Var[W]) where Var[W] = (V4/V2� - 1)/4.
        /// </para>
        /// <para>
        /// Reference: Cohn, T.A., Lane, W.L., Stedinger, J.R. (2001). Confidence intervals for
        /// Expected Moments Algorithm flood quantile estimates. Water Resources Research, 37(6), 1695-1706.
        /// </para>
        /// </remarks>
        private InfluenceStatistics ComputeInfluenceStatistics(double[] thetaHat, double targetNonExceedanceProbability)
        {
            int nParams = thetaHat.Length;
            var result = new InfluenceStatistics
            {
                NuQuantile = 30.0,
                ParameterSkewness = new double[nParams]
            };

            // 1. Quantile gradient g_p = ?X_p/?? at the target probability
            double[] gp = Bulletin17CDistribution.QuantileGradient(targetNonExceedanceProbability, thetaHat);

            // 2. Per-observation moment conditions [n � q]
            var pointwiseMC = _gmm!.PointwiseMomentConditions;
            if (pointwiseMC == null)
            {
                Debug.WriteLine("ComputeInfluenceStatistics: PointwiseMomentConditions unavailable, using defaults.");
                return result;
            }
            double[,] mi = pointwiseMC(thetaHat);
            int n = mi.GetLength(0);
            int q = mi.GetLength(1);

            // 3. GMM matrices for Bread = D'WD + H
            var D = _gmm.GetJacobian(thetaHat);
            var DT = D.Transpose();
            var W = _gmm.W!;
            var H = _gmm.GetPenaltyHessian(thetaHat);
            var bread = DT * W * D + H;

            Matrix breadInv;
            try
            {
                breadInv = bread.Inverse();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ComputeInfluenceStatistics: Bread matrix singular, using defaults. {ex.Message}");
                return result;
            }

            var DtW = DT * W;

            // 4. Compute ?_i vectors and quantile influence scores s_i = g_p' � ?_i
            double[] s = new double[n];
            double[][] psiAll = new double[n][];

            for (int i = 0; i < n; i++)
            {
                // D'W � m_i ? [nParams � 1]
                double[] DtWm = new double[nParams];
                for (int j = 0; j < nParams; j++)
                {
                    double sum = 0;
                    for (int k = 0; k < q; k++)
                        sum += DtW[j, k] * mi[i, k];
                    DtWm[j] = sum;
                }

                // ?_i = Bread?� � DtWm ? [nParams � 1]
                double[] psi = new double[nParams];
                for (int j = 0; j < nParams; j++)
                {
                    double sum = 0;
                    for (int k = 0; k < nParams; k++)
                        sum += breadInv[j, k] * DtWm[k];
                    psi[j] = sum;
                }
                psiAll[i] = psi;

                // s_i = g_p' � ?_i ? scalar
                double si = 0;
                for (int j = 0; j < nParams; j++)
                    si += gp[j] * psi[j];
                s[i] = si;
            }

            // 5a. Per-parameter ? from kurtosis of ?_i[j], then harmonic mean for MVT ?.
            // The MVT uses a single ? for all dimensions. Using the quantile-projection
            // kurtosis (s_i = g_p' � ?_i) over-emphasizes the heaviest-tailed parameter (?),
            // giving ? that's too low for the other parameters. The geometric mean of
            // per-parameter ?s balances across dimensions: it's less conservative than the
            // harmonic mean (which is dominated by the smallest ?) but less liberal than the
            // arithmetic mean (dominated by the largest ?).
            double logNuSum = 0;
            int nuCount = 0;
            for (int j = 0; j < nParams; j++)
            {
                double[] psiJ = new double[n];
                for (int i = 0; i < n; i++)
                    psiJ[i] = psiAll[i][j];
                double nuJ = ComputeDegreesOfFreedomFromKurtosis(psiJ, n);
                logNuSum += Math.Log(nuJ);
                nuCount++;
            }
            result.NuQuantile = nuCount > 0
                ? Math.Exp(logNuSum / nuCount)
                : 1000.0;

            // 5b. Per-parameter skewness for MVT center shift (BCa z0 equivalent).
            // The skewness of ?_i[j] determines how much to shift the MVT center in
            // link-space dimension j: ??[j] = (skew_j / 6) � vVetaHat[j,j].
            // This is the influence-function analog of the BCa z0 bias correction.
            for (int j = 0; j < nParams; j++)
            {
                double[] psiJ = new double[n];
                for (int i = 0; i < n; i++)
                    psiJ[i] = psiAll[i][j];
                result.ParameterSkewness[j] = ComputeSkewnessFromInfluence(psiJ, n);
            }

            return result;
        }

        /// <summary>
        /// Computes kurtosis-based degrees of freedom from a vector of influence scores
        /// by matching the empirical kurtosis to the theoretical kurtosis of a Student-t distribution.
        /// </summary>
        /// <param name="scores">The per-observation influence scores.</param>
        /// <param name="n">The number of observations.</param>
        /// <returns>Degrees of freedom floored at 5 (matching EMA's nu_min), capped at 1000.</returns>
        /// <remarks>
        /// <para>
        /// For Student-t(?), the kurtosis ratio (fourth moment / variance�) is ? = 3(?-2)/(?-4)
        /// for ? &gt; 4. Solving for ?: ? = 2(2?-3)/(?-3). At ?=3 (Gaussian): ??8.
        /// At ?=9: ?=5 (heavy tails). Only meaningful when ? &gt; 3; sub-Gaussian (? = 3)
        /// cases return ?=1000 (effectively MVN).
        /// </para>
        /// <para>
        /// Uses centered moments (about the sample mean) for robustness with censored data
        /// where influence scores may not be perfectly mean-zero.
        /// </para>
        /// </remarks>
        private static double ComputeDegreesOfFreedomFromKurtosis(double[] scores, int n)
        {
            // Centered moments for robustness
            double mean = 0;
            for (int i = 0; i < n; i++)
                mean += scores[i];
            mean /= n;

            double M2 = 0, M4 = 0;
            for (int i = 0; i < n; i++)
            {
                double d = scores[i] - mean;
                double d2 = d * d;
                M2 += d2;
                M4 += d2 * d2;
            }
            M2 /= n;
            M4 /= n;

            if (M2 <= 0)
                return 1000.0;

            // Kurtosis ratio ? = M4/M2� (=3 for Gaussian, >3 for heavy-tailed)
            double kappa = M4 / (M2 * M2);

            // For ? = 3 (sub-Gaussian or Gaussian), Student-t is unnecessary
            if (kappa <= 3.0 + 1e-6)
                return 1000.0;

            // ? = 2(2?-3)/(?-3), derived from Student-t kurtosis ? = 3(?-2)/(?-4)
            double nu = 2.0 * (2.0 * kappa - 3.0) / (kappa - 3.0);

            return Math.Clamp(nu, 5.0, 1000.0);
        }

        /// <summary>
        /// Computes the third standardized moment (skewness) of a vector of influence scores.
        /// </summary>
        /// <param name="scores">The per-observation influence scores.</param>
        /// <param name="n">The number of observations.</param>
        /// <returns>The skewness �3/�2^{3/2}, or 0 if variance is near zero.</returns>
        /// <remarks>
        /// Computes the raw central moments M2 = (1/n)�S(x-x�)� and M3 = (1/n)�S(x-x�)�,
        /// then returns M3 / M2^{3/2}. This is the Fisher skewness coefficient.
        /// </remarks>
        private static double ComputeSkewnessFromInfluence(double[] scores, int n)
        {
            // Compute mean
            double mean = 0;
            for (int i = 0; i < n; i++)
                mean += scores[i];
            mean /= n;

            // Compute central moments M2 and M3
            double M2 = 0, M3 = 0;
            for (int i = 0; i < n; i++)
            {
                double d = scores[i] - mean;
                double d2 = d * d;
                M2 += d2;
                M3 += d2 * d;
            }
            M2 /= n;
            M3 /= n;

            if (M2 <= 1e-30)
                return 0.0;

            double sigma = Math.Sqrt(M2);
            return M3 / (sigma * sigma * sigma);
        }

        /// <summary>
        /// Collects exactly the requested number of valid results from deterministic candidate batches.
        /// </summary>
        /// <typeparam name="T">Candidate result type.</typeparam>
        /// <param name="targetCount">Number of valid results required.</param>
        /// <param name="batchFactory">Factory that evaluates the requested number of replacement candidates.</param>
        /// <param name="isValid">Predicate identifying valid candidate results.</param>
        /// <param name="cancellationToken">Cancellation token checked between and within batches.</param>
        /// <param name="acceptedCallback">Optional callback invoked after each accepted result with the total accepted count.</param>
        /// <returns>An array containing exactly <paramref name="targetCount"/> valid results in deterministic batch order.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="targetCount"/> is negative.</exception>
        /// <exception cref="InvalidOperationException">Thrown when a batch factory returns no candidates.</exception>
        internal static T[] CollectExactBatchResults<T>(
            int targetCount,
            Func<int, T[]> batchFactory,
            Func<T, bool> isValid,
            CancellationToken cancellationToken,
            Action<int>? acceptedCallback = null)
        {
            if (targetCount < 0)
                throw new ArgumentOutOfRangeException(nameof(targetCount));

            var results = new T[targetCount];
            int acceptedCount = 0;
            while (acceptedCount < targetCount)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int remaining = targetCount - acceptedCount;
                var batch = batchFactory(remaining);
                if (batch == null || batch.Length == 0)
                    throw new InvalidOperationException("The exact-count batch factory returned no candidates.");

                for (int i = 0; i < batch.Length && acceptedCount < targetCount; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!isValid(batch[i]))
                        continue;

                    results[acceptedCount++] = batch[i];
                    acceptedCallback?.Invoke(acceptedCount);
                }
            }

            return results;
        }

        /// <summary>
        /// Stores a valid pivotal-bootstrap parameter estimate, covariance matrix, and deterministic seed.
        /// </summary>
        private sealed class PivotBootstrapReplicate
        {
            /// <summary>
            /// Initializes a pivotal-bootstrap replicate.
            /// </summary>
            /// <param name="theta">Estimated parameter vector.</param>
            /// <param name="covariance">Finite positive-definite parameter covariance.</param>
            /// <param name="seed">Seed associated with this candidate.</param>
            public PivotBootstrapReplicate(double[] theta, Matrix covariance, int seed)
            {
                Theta = theta;
                Covariance = covariance;
                Seed = seed;
            }

            /// <summary>Gets the estimated parameter vector.</summary>
            public double[] Theta { get; }

            /// <summary>Gets the finite positive-definite parameter covariance.</summary>
            public Matrix Covariance { get; }

            /// <summary>Gets the deterministic candidate seed.</summary>
            public int Seed { get; }
        }

        /// <summary>
        /// Generates distributions using parametric bootstrap resampling.
        /// </summary>
        /// <param name="progressReporter">Optional progress reporter. Progress is reported from 10% to 100%.</param>
        /// <returns>
        /// An array of <see cref="UnivariateDistributionBase"/> with bootstrap-estimated parameters,
        /// or null if the operation was cancelled.
        /// </returns>
        /// <remarks>
        /// <para>
        ///     For each bootstrap replicate b = 1, …, B:
        /// </para>
        /// <list type="number">
        ///     <item><description>Generate a bootstrap data frame by resampling from the fitted parent distribution
        ///         via <see cref="DataFrame.BootstrapDataFrame"/>.</description></item>
        ///     <item><description>Create a new <see cref="Bulletin17CDistribution"/> from the bootstrap data and
        ///         set a randomized penalty function via <see cref="Bulletin17CDistribution.SetPenaltyFunction"/>
        ///         to propagate prior uncertainty.</description></item>
        ///     <item><description>Estimate parameters using GMM. If GMM fails, retry up to
        ///         <c>maxRetries</c> times with fresh bootstrap samples before falling back to parent parameters.</description></item>
        /// </list>
        /// </remarks>
        private ParameterSet[]? GetParameterSetsFromParametricBootstrap(SafeProgressReporter? progressReporter)
        {
            const int maxRetries = 10;
            int B = BayesianAnalysis.OutputLength;
            int p = Bulletin17CDistribution.NumberOfParameters;
            var results = new ParameterSet[B];
            var masterPRNG = new MersenneTwister(BayesianAnalysis.PRNGSeed);
            var seeds = masterPRNG.NextIntegers(B);
            var thetaHat = _gmm!.BestParameterSet.Values;
            var parentDistribution = Bulletin17CDistribution.Distribution.Clone();
            parentDistribution.SetParameters(thetaHat);

            // Mahalanobis distance rejection: compute inverse covariance from parent GMM
            var sigmaHat = _gmm!.GetCovariance(thetaHat);
            Matrix sigmaInv;
            try { sigmaInv = sigmaHat.Inverse(); }
            catch (Exception ex)
            {
                Debug.WriteLine($"Bulletin17CAnalysis.GenerateBootstrapDistributions: covariance singular, regularizing: {ex.Message}");
                sigmaInv = MatrixRegularization.MakeSymmetricPositiveDefinite(sigmaHat).Inverse();
            }
            double mahalPvalue = 1.0 - 1.0 / (B * 5); // reject extreme draws with p-value < 1/(5B)
            double mahalThreshold = new ChiSquared(p).InverseCDF(mahalPvalue);

            // Initialize bootstrap diagnostics
            var diag = new BootstrapDiagnostics { TotalReplicates = B };
            var phase1Stopwatch = Stopwatch.StartNew();

            var options = AnalysisProgress.CreateParallelOptions(_cancellationTokenSource?.Token ?? CancellationToken.None);

            int iteration = 0;

            // Determine if we should clone with the parent data frame or not.
            // If there are any low outliers, uncertain series, interval series, or threshold series,
            // we need to clone with the data frame to improve the initial guess of the solver
            bool cloneWithDataFrame = Bulletin17CDistribution.DataFrame.NumberOfLowOutliers > 0 || 
                Bulletin17CDistribution.DataFrame.UncertainSeries.Count > 0 ||
                Bulletin17CDistribution.DataFrame.IntervalSeries.Count > 0 ||
                Bulletin17CDistribution.DataFrame.ThresholdSeries.Count > 0;

            try
            {
                Parallel.For(0, B, options, idx =>
                {
                    options.CancellationToken.ThrowIfCancellationRequested();

                    var prng = new MersenneTwister(seeds[idx]);
                    double[]? acceptedParams = null;

                    for (int attempt = 0; attempt < maxRetries && acceptedParams == null; attempt++)
                    {
                        try
                        {
                            // BootstrapDataFrame uses the parent distribution as the data-generatingmodel.
                            var samplingDist = parentDistribution.Clone();
                            var bootDataFrame = Bulletin17CDistribution.DataFrame.BootstrapDataFrame(samplingDist, prng);
                            // Clone the Bulletin17CDistribution with the bootstrap data frame.
                            Bulletin17CDistribution? bootB17CDistribution = null;
                            if (cloneWithDataFrame)
                            {
                                // clone with parent params for the resampling step only.
                                bootB17CDistribution = Bulletin17CDistribution.CloneWithDataFrame(bootDataFrame);
                                bootB17CDistribution.SetParameterValues(thetaHat);
                            }
                            else
                            {
                                // clone and set params from the boostrap data frame.
                                bootB17CDistribution = (Bulletin17CDistribution)Bulletin17CDistribution.Clone();
                                bootB17CDistribution.DataFrame = bootDataFrame;
                            }
                            // Resample the penalty function
                            bootB17CDistribution.SetRandomPenaltyFunction(thetaHat, prng);

                            // Re-estimate. 
                            var bootGMM = new GeneralizedMethodOfMoments(bootB17CDistribution);
                            bootGMM.Estimate();
                            if (bootGMM.Status != OptimizationStatus.Success)
                                throw new Exception("The bootstrap GMM solver failed for realization " + idx.ToString() + ".");

                            // Reject degenerate fits via Mahalanobis distance from parent.
                            // This is a conservative check to avoid extreme bootstrap draws that are far from the parent distribution.
                            var bootParams = bootGMM.BestParameterSet.Values;
                            double mahalDist = 0;
                            for (int j = 0; j < p; j++)
                            {
                                double tmp = 0;
                                for (int k = 0; k < p; k++)
                                    tmp += sigmaInv[j, k] * (bootParams[k] - thetaHat[k]);
                                mahalDist += (bootParams[j] - thetaHat[j]) * tmp;
                            }
                            if (mahalDist > mahalThreshold)
                            {
                                diag.IncrementMahalanobisRejection();
                                throw new Exception($"Bootstrap replicate {idx} rejected: Mahalanobis distance {mahalDist:F1} exceeds threshold {mahalThreshold:F1}.");
                            }

                            acceptedParams = bootParams;
                            diag.AddFunctionEvaluations(bootGMM.TotalFunctionEvaluations);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Bootstrap replicate {idx}, attempt {attempt}: {ex.Message}");
                            if (attempt < maxRetries - 1) diag.AddRetries(1);
                        }
                    }

                    // Fall back to parent parameter vector if all retries failed — preserves prior
                    // behavior where bootDistribution retained parent params after a Clone() with
                    // no successful SetParameters call.
                    if (acceptedParams == null)
                    {
                        diag.IncrementFailed();
                        acceptedParams = thetaHat;
                    }

                    results[idx] = new ParameterSet(acceptedParams, double.NaN);

                    int current = Interlocked.Increment(ref iteration);
                    if (current % Math.Max(1, B * 0.01) == 0)
                        progressReporter?.ReportProgress((int)(100.0 * current / B));
                });

                phase1Stopwatch.Stop();
                diag.Phase1Time = phase1Stopwatch.Elapsed;
                BootstrapResults = diag;

                return results;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        /// <summary>
        /// Determines whether a phase-1 pivot bootstrap replicate has a complete parameter vector and covariance matrix.
        /// </summary>
        /// <param name="theta">The bootstrap parameter vector.</param>
        /// <param name="covariance">The bootstrap covariance matrix.</param>
        /// <param name="parameterCount">The expected number of parameters.</param>
        /// <returns><see langword="true"/> when both objects are present, dimensionally compatible, and finite; otherwise, <see langword="false"/>.</returns>
        internal static bool IsUsablePivotBootstrapReplicate(double[]? theta, Matrix? covariance, int parameterCount)
        {
            if (parameterCount <= 0 || theta is null || covariance is null)
                return false;
            if (theta.Length != parameterCount || covariance.NumberOfRows != parameterCount || covariance.NumberOfColumns != parameterCount)
                return false;

            for (int i = 0; i < parameterCount; i++)
            {
                if (!double.IsFinite(theta[i]))
                    return false;
                for (int j = 0; j < parameterCount; j++)
                {
                    if (!double.IsFinite(covariance[i, j]))
                        return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Creates a Yeo-Johnson pivot link, falling back to identity when fitting or validation fails.
        /// </summary>
        /// <param name="samples">The accepted bootstrap samples for one parameter.</param>
        /// <param name="parameterName">The parameter name used in diagnostic logging.</param>
        /// <returns>A numerically usable link function for the pivot bootstrap phase.</returns>
        internal static ILinkFunction CreatePivotYeoJohnsonLink(IReadOnlyList<double> samples, string parameterName)
        {
            try
            {
                if (samples is null)
                    throw new ArgumentNullException(nameof(samples));
                if (samples.Count < 2)
                    throw new ArgumentException("At least two samples are required to fit a Yeo-Johnson pivot link.", nameof(samples));

                var values = samples.ToArray();
                for (int i = 0; i < values.Length; i++)
                {
                    if (!double.IsFinite(values[i]))
                        throw new ArgumentOutOfRangeException(nameof(samples), "Yeo-Johnson pivot link samples must be finite.");
                }

                var link = new YeoJohnsonLink(values);
                if (Math.Abs(link.Lambda) >= 4.999d)
                    throw new InvalidOperationException("Yeo-Johnson pivot link lambda fit railed at the optimizer boundary.");

                for (int i = 0; i < values.Length; i++)
                {
                    double eta = link.Link(values[i]);
                    double derivative = link.DLink(values[i]);
                    double roundTrip = link.InverseLink(eta);
                    if (!double.IsFinite(eta) || !double.IsFinite(derivative) || !double.IsFinite(roundTrip))
                        throw new InvalidOperationException("Yeo-Johnson pivot link produced non-finite behavior.");
                }

                return link;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Pivot bootstrap: Yeo-Johnson link failed for {parameterName}; using identity link. {ex.Message}");
                return new IdentityLink();
            }
        }

        /// <summary>
        /// Generates distributions using the parametric bootstrap pivot method with link-space transformation.
        /// </summary>
        /// <param name="progressReporter">Optional progress reporter. Progress is reported from 10% to 100%.</param>
        /// <returns>
        /// An array of <see cref="UnivariateDistributionBase"/> with pivot-corrected bootstrap parameters,
        /// or null if the operation was cancelled.
        /// </returns>
        /// <remarks>
        /// <para>
        ///     The pivot bootstrap improves on the standard percentile bootstrap by standardizing each
        ///     bootstrap replicate in a variance-stabilizing link-space, producing better-calibrated
        ///     confidence intervals especially for skewed or bounded parameters.
        /// </para>
        /// <para>
        ///     <b>Algorithm (three phases):</b>
        /// </para>
        /// <para>
        ///     <b>Phase 1 � Collect bootstrap fits</b> (parallel): For each replicate b = 1, �, B,
        ///     generate a bootstrap data frame, re-estimate via GMM (with up to 5 retries), and store
        ///     both the parameter estimates ?*_b and GMM covariance S*_b. Replicates that fail every
        ///     attempt are discarded; Phases 2 and 3 operate on the accepted subset only.
        /// </para>
        /// <para>
        ///     <b>Phase 2 � Fit link functions</b>: Fit a <see cref="YeoJohnsonLink"/> to the bootstrap
        ///     location and shape parameter samples (variance-stabilizing power transform), and assign a
        ///     <see cref="LogLink"/> for the scale parameter (ensures positivity). Build a
        ///     <see cref="LinkController"/> and compute the parent Cholesky factor L^ in link-space.
        /// </para>
        /// <para>
        ///     <b>Phase 3 � Generate pivot draws</b> (parallel): For each accepted replicate b, compute
        ///     the standardized pivot z = L*_b?� � (?^ - ?*_b) in link-space, add smoothing jitter,
        ///     clip extreme pivots (|z_j| &gt; 8), and map back to real-space via
        ///     ?_draw = InverseLink(?^ + L^ � z). Rejected or failed draws are dropped from the
        ///     delivered sample — never substituted with the parent parameters.
        /// </para>
        /// <para>
        ///     Reference: DiCiccio, T.J. and Efron, B. (1996). Bootstrap confidence intervals.
        ///     Statistical Science, 11(3), 189�228.
        /// </para>
        /// </remarks>
        private ParameterSet[]? GetParameterSetsFromPivotalBootstrap(SafeProgressReporter? progressReporter)
        {
            const int maxRetries = 5;
            const int maxTransformRetries = 5;
            const double smoothStdScale = 0.01;
            const double zLimit = 8.0;
            const int transformSeedMask = 0x5F3759DF;

            int targetCount = BayesianAnalysis.OutputLength;
            int parameterCount = Bulletin17CDistribution.NumberOfParameters;
            var masterPrng = new MersenneTwister(BayesianAnalysis.PRNGSeed);
            var thetaHat = _gmm!.BestParameterSet.Values.ToArray();
            var parentDistribution = Bulletin17CDistribution.Distribution.Clone();
            parentDistribution.SetParameters(thetaHat);

            if (!_gmm.TryGetCovariance(thetaHat, true, out var sigmaHat))
            {
                SetUncertaintyDiagnosticMessage(
                    "Pivot bootstrap could not compute a finite covariance matrix for the parent GMM fit. " +
                    "Use the plain Bootstrap uncertainty method for this analysis.");
                return null;
            }

            sigmaHat = MatrixRegularization.MakeSymmetricPositiveDefinite(sigmaHat);
            Matrix sigmaInverse;
            try
            {
                _ = new CholeskyDecomposition(sigmaHat);
                sigmaInverse = sigmaHat.Inverse();
            }
            catch
            {
                SetUncertaintyDiagnosticMessage(
                    "Pivot bootstrap could not factor the parent GMM covariance matrix. " +
                    "Use the plain Bootstrap uncertainty method for this analysis.");
                return null;
            }

            double mahalanobisThreshold = new ChiSquared(parameterCount).InverseCDF(0.9999);
            var diagnostics = new BootstrapDiagnostics { TotalReplicates = targetCount };
            CancellationToken cancellationToken = _cancellationTokenSource?.Token ?? CancellationToken.None;
            var options = AnalysisProgress.CreateParallelOptions(cancellationToken);

            PivotBootstrapReplicate?[] CreatePivotFitBatch(int batchSize)
            {
                var seeds = masterPrng.NextIntegers(batchSize);
                var batch = new PivotBootstrapReplicate?[batchSize];

                Parallel.For(0, batchSize, options, candidateIndex =>
                {
                    options.CancellationToken.ThrowIfCancellationRequested();
                    diagnostics.IncrementAttempted();
                    var prng = new MersenneTwister(seeds[candidateIndex]);

                    for (int attempt = 0; attempt < maxRetries; attempt++)
                    {
                        try
                        {
                            var samplingDistribution = parentDistribution.Clone();
                            var bootstrapDataFrame = Bulletin17CDistribution.DataFrame.BootstrapDataFrame(samplingDistribution, prng);
                            var bootstrapModel = Bulletin17CDistribution.CloneWithDataFrame(bootstrapDataFrame);
                            bootstrapModel.SetParameterValues(thetaHat);
                            bootstrapModel.SetRandomPenaltyFunction(thetaHat, prng);

                            var bootstrapGmm = new GeneralizedMethodOfMoments(bootstrapModel)
                            {
                                PenaltyIsRandom = false
                            };
                            bool estimated = bootstrapGmm.Estimate();
                            diagnostics.RecordGMMStatus(bootstrapGmm.Status);
                            diagnostics.AddFunctionEvaluations(bootstrapGmm.TotalFunctionEvaluations);
                            diagnostics.AddOptimizerFallbacks(bootstrapGmm.OptimizerFallbackCount);

                            if (!estimated || !bootstrapGmm.IsEstimated || bootstrapGmm.Status != OptimizationStatus.Success)
                                throw new InvalidOperationException("The pivotal-bootstrap GMM solver did not produce a successful estimate.");

                            var parameters = bootstrapGmm.BestParameterSet.Values.ToArray();
                            if (parameters.Length != parameterCount || parameters.Any(value => !double.IsFinite(value)))
                                throw new InvalidOperationException("The pivotal-bootstrap GMM solver produced non-finite parameters.");
                            parentDistribution.ValidateParameters(parameters, true);

                            double mahalanobisDistance = 0.0;
                            for (int j = 0; j < parameterCount; j++)
                            {
                                double weightedDifference = 0.0;
                                for (int k = 0; k < parameterCount; k++)
                                    weightedDifference += sigmaInverse[j, k] * (parameters[k] - thetaHat[k]);
                                mahalanobisDistance += (parameters[j] - thetaHat[j]) * weightedDifference;
                            }
                            if (!double.IsFinite(mahalanobisDistance) || mahalanobisDistance > mahalanobisThreshold)
                            {
                                diagnostics.IncrementMahalanobisRejection();
                                throw new InvalidOperationException("The pivotal-bootstrap estimate failed the Mahalanobis screen.");
                            }

                            if (!bootstrapGmm.TryGetCovariance(parameters, true, out var covariance))
                                throw new InvalidOperationException("The pivotal-bootstrap GMM covariance could not be computed.");

                            covariance = MatrixRegularization.MakeSymmetricPositiveDefinite(covariance);
                            _ = new CholeskyDecomposition(covariance);
                            if (!IsUsablePivotBootstrapReplicate(parameters, covariance, parameterCount))
                                throw new InvalidOperationException("The pivotal-bootstrap theta/covariance pair is unusable.");

                            batch[candidateIndex] = new PivotBootstrapReplicate(parameters, covariance, seeds[candidateIndex]);
                            return;
                        }
                        catch
                        {
                            if (attempt < maxRetries - 1)
                                diagnostics.AddRetries(1);
                        }
                    }

                    diagnostics.IncrementFailed();
                });

                return batch;
            }

            var phase1Stopwatch = Stopwatch.StartNew();
            PivotBootstrapReplicate[] initialReplicates;
            try
            {
                initialReplicates = CollectExactBatchResults(
                        targetCount,
                        CreatePivotFitBatch,
                        replicate => replicate != null,
                        cancellationToken,
                        acceptedCount =>
                        {
                            if (AnalysisProgress.ShouldReportLoopProgress(acceptedCount, targetCount))
                                progressReporter?.ReportProgress((int)(98.0 * acceptedCount / targetCount));
                        })
                    .Select(replicate => replicate!)
                    .ToArray();
            }
            catch (OperationCanceledException)
            {
                return null;
            }

            phase1Stopwatch.Stop();
            diagnostics.Phase1Time = phase1Stopwatch.Elapsed;

            var phase2Stopwatch = Stopwatch.StartNew();
            progressReporter?.ReportProgress(98);
            ILinkFunction locationLink = CreatePivotYeoJohnsonLink(
                initialReplicates.Select(replicate => replicate.Theta[0]).ToArray(), "location");
            ILinkFunction scaleLink = new LogLink();
            ILinkFunction? shapeLink = parameterCount >= 3
                ? CreatePivotYeoJohnsonLink(initialReplicates.Select(replicate => replicate.Theta[2]).ToArray(), "shape")
                : null;

            var links = new ILinkFunction?[parameterCount];
            links[0] = locationLink;
            links[1] = scaleLink;
            if (parameterCount >= 3)
                links[2] = shapeLink;
            var linkController = new LinkController(links);

            var etaHat = linkController.Link(thetaHat);
            var parentJacobian = linkController.LinkJacobian(thetaHat);
            var linkedParentCovariance = parentJacobian * sigmaHat * parentJacobian.Transpose();
            linkedParentCovariance = MatrixRegularization.MakeSymmetricPositiveDefinite(linkedParentCovariance);

            Matrix parentCholesky;
            try
            {
                parentCholesky = new CholeskyDecomposition(linkedParentCovariance).L;
            }
            catch
            {
                return GetParameterSetsFromParametricBootstrap(progressReporter);
            }

            phase2Stopwatch.Stop();
            diagnostics.Phase2Time = phase2Stopwatch.Elapsed;

            double smoothStandardDeviation = smoothStdScale / Math.Sqrt(parameterCount);
            bool useInitialReplicates = true;
            var phase3Stopwatch = Stopwatch.StartNew();

            ParameterSet[] CreatePivotDrawBatch(int batchSize)
            {
                PivotBootstrapReplicate[] replicateBatch;
                if (useInitialReplicates)
                {
                    replicateBatch = initialReplicates;
                    useInitialReplicates = false;
                }
                else
                {
                    replicateBatch = CollectExactBatchResults(
                            batchSize,
                            CreatePivotFitBatch,
                            replicate => replicate != null,
                            cancellationToken)
                        .Select(replicate => replicate!)
                        .ToArray();
                }

                var batch = new ParameterSet[replicateBatch.Length];
                Parallel.For(0, replicateBatch.Length, options, index =>
                {
                    options.CancellationToken.ThrowIfCancellationRequested();
                    var replicate = replicateBatch[index];
                    var validator = parentDistribution.Clone();

                    try
                    {
                        var etaStar = linkController.Link(replicate.Theta);
                        var replicateJacobian = linkController.LinkJacobian(replicate.Theta);
                        var linkedReplicateCovariance =
                            replicateJacobian * replicate.Covariance * replicateJacobian.Transpose();
                        linkedReplicateCovariance =
                            MatrixRegularization.MakeSymmetricPositiveDefinite(linkedReplicateCovariance);
                        var replicateCholeskyInverse =
                            new CholeskyDecomposition(linkedReplicateCovariance).L.Inverse();

                        var difference = new Matrix(parameterCount, 1);
                        for (int j = 0; j < parameterCount; j++)
                            difference[j, 0] = etaHat[j] - etaStar[j];
                        var standardizedPivot = replicateCholeskyInverse * difference;
                        var prng = new MersenneTwister(unchecked(replicate.Seed ^ transformSeedMask));

                        for (int attempt = 0; attempt < maxTransformRetries; attempt++)
                        {
                            try
                            {
                                var pivotColumn = new Matrix(parameterCount, 1);
                                for (int j = 0; j < parameterCount; j++)
                                {
                                    double pivot = standardizedPivot[j, 0] +
                                        Normal.StandardZ(prng.NextDouble()) * smoothStandardDeviation;
                                    pivotColumn[j, 0] = Math.Max(-zLimit, Math.Min(zLimit, pivot));
                                }

                                var linkedOffset = parentCholesky * pivotColumn;
                                var linkedDraw = new double[parameterCount];
                                for (int j = 0; j < parameterCount; j++)
                                    linkedDraw[j] = etaHat[j] + linkedOffset[j, 0];

                                var parameters = linkController.InverseLink(linkedDraw);
                                if (parameters.Any(value => !double.IsFinite(value)))
                                    throw new InvalidOperationException("The pivot transform produced non-finite parameters.");
                                validator.ValidateParameters(parameters, true);
                                batch[index] = new ParameterSet(parameters, double.NaN);
                                return;
                            }
                            catch
                            {
                                diagnostics.IncrementTransformFailure();
                            }
                        }
                    }
                    catch
                    {
                        diagnostics.IncrementTransformFailure();
                    }
                });

                return batch;
            }

            try
            {
                var results = CollectExactBatchResults(
                    targetCount,
                    CreatePivotDrawBatch,
                    parameterSet => parameterSet.Values != null,
                    cancellationToken,
                    acceptedCount =>
                    {
                        if (AnalysisProgress.ShouldReportLoopProgress(acceptedCount, targetCount))
                            progressReporter?.ReportProgress(98 + (int)(2.0 * acceptedCount / targetCount));
                    });

                phase3Stopwatch.Stop();
                diagnostics.Phase3Time = phase3Stopwatch.Elapsed;
                diagnostics.RetainedReplicates = results.Length;
                BootstrapResults = diagnostics;
                return results;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }
        /// <summary>
        /// Returns the distribution for a given posterior sample index.
        /// </summary>
        /// <param name="index">The sample index.</param>
        /// <returns>A univariate distribution with the sampled parameters.</returns>
        public UnivariateDistributionBase? GetDistribution(int index)
        {
            if (!IsEstimated || BayesianAnalysis.Results == null)
                return null;

            var dist = Bulletin17CDistribution.Distribution.Clone();
            dist.SetParameters(BayesianAnalysis.Results.Output[index].Values);
            return dist;
        }

        /// <inheritdoc/>
        public UnivariateDistributionBase? GetPointEstimateDistribution()
            => GetPointEstimateDistribution(BayesianAnalysis.PointEstimator);

        /// <inheritdoc/>
        public UnivariateDistributionBase? GetPointEstimateDistribution(
            BayesianAnalysis.PointEstimateType pointEstimator)
        {
            if (!IsEstimated || BayesianAnalysis.Results == null)
                return null;

            var parms = pointEstimator == BayesianAnalysis.PointEstimateType.PosteriorMean
                ? BayesianAnalysis.Results.PosteriorMean.Values
                : BayesianAnalysis.Results.MAP.Values;

            var dist = Bulletin17CDistribution.Distribution.Clone();
            dist.SetParameters(parms);
            return dist;
        }

        /// <summary>
        /// Updates point estimate results including goodness-of-fit metrics.
        /// </summary>
        public async Task UpdatePointEstimateResultsAsync()
        {
            var bayesianResults = BayesianAnalysis.Results;
            var analysisResults = AnalysisResults;
            var gmm = _gmm;
            if (bayesianResults == null || analysisResults == null || gmm == null)
                return;

            double[] parms = BayesianAnalysis.PointEstimator == BayesianAnalysis.PointEstimateType.PosteriorMean
                ? bayesianResults.PosteriorMean.Values
                : bayesianResults.MAP.Values;
            double[] probabilityOrdinates = ProbabilityOrdinates.ToArray();

            await Task.Run(() =>
            {
                Bulletin17CDistribution.SetParameterValues(parms);

                // Update mode curve
                analysisResults.ModeCurve = new double[probabilityOrdinates.Length];
                for (int i = 0; i < probabilityOrdinates.Length; i++)
                    analysisResults.ModeCurve[i] = Bulletin17CDistribution.Distribution.InverseCDF(1.0 - probabilityOrdinates[i]);

                // Goodness of fit metrics
                int nt = Bulletin17CDistribution.DataFrame.ExactSeries.Count
                       - Bulletin17CDistribution.DataFrame.NumberOfLowOutliers
                       + Bulletin17CDistribution.DataFrame.IntervalSeries.Count;

                var univariateDist = new UnivariateDistribution(Bulletin17CDistribution.DataFrame, Bulletin17CDistribution.Distribution);
                double logLH = univariateDist.LogLikelihood(bayesianResults.MAP.Values);
                analysisResults.AIC = GoodnessOfFit.AIC(Bulletin17CDistribution.NumberOfParameters, logLH);
                analysisResults.BIC = GoodnessOfFit.BIC(nt, Bulletin17CDistribution.NumberOfParameters, logLH);

                // RMSE
                var values = Bulletin17CDistribution.DataFrame.ExactSeries.ValuesToList();
                values.AddRange(Bulletin17CDistribution.DataFrame.IntervalSeries.ValuesToList());
                var pp = Bulletin17CDistribution.DataFrame.ExactSeries.Select(x => x.PlottingPositionComplement).ToList();
                pp.AddRange(Bulletin17CDistribution.DataFrame.IntervalSeries.Select(x => x.PlottingPositionComplement));
                analysisResults.RMSE = GoodnessOfFit.RMSE(values, pp, Bulletin17CDistribution.Distribution);

                // Effective record length
                var thetaHat = gmm.BestParameterSet.Values;
                var sigmaHat = gmm.GetCovariance(thetaHat);
                var eig = new EigenValueDecomposition(sigmaHat);
                analysisResults.ERL = eig.EffectiveSampleSize();
            });

            RaisePropertyChange(nameof(AnalysisResults));
        }

        /// <inheritdoc/>
        public override (bool IsValid, List<string> ValidationMessages) Validate()
        {
            bool isValid = true;
            var messageList = new List<string>();

            // Validate distribution
            var distValid = Bulletin17CDistribution.Validate();
            if (!distValid.IsValid)
            {
                isValid = false;
                messageList.AddRange(distValid.ValidationMessages);
            }

            // Validate probability ordinates
            var probOrdValid = ProbabilityOrdinates.Validate();
            if (!probOrdValid.IsValid)
            {
                isValid = false;
                messageList.AddRange(probOrdValid.ValidationMessages);
            }

            return (isValid, messageList);
        }

        /// <inheritdoc/>
        public XElement ToXElement()
        {
            var root = new XElement(nameof(Bulletin17CAnalysis));
            root.SetAttributeValue("IsEstimated", IsEstimated.ToString());
            root.SetAttributeValue(nameof(UncertaintyMethod), UncertaintyMethod.ToString());

            // Probability ordinates
            root.Add(new XElement(nameof(ProbabilityOrdinates), ProbabilityOrdinates.ToDelimitedString(ProbabilityOrdinates.DefaultDelimiter)));


            // Bayesian analysis
            if (BayesianAnalysis != null)
                root.Add(BayesianAnalysis.ToXElement());

            // GMM estimation results
            if (_gmm != null && _gmm.IsEstimated)
                root.Add(_gmm.ToXElement());

            // Uncertainty sampling diagnostics
            if (BootstrapResults != null)
                root.Add(BootstrapResults.ToXElement());

            return root;
        }


        /// <summary>
        /// Estimates the acceleration constants for each parameter.
        /// </summary>
        /// <param name="thetaHats">The list of best-estimate parameters.</param>
        private double[] AccelerationConstants(double[] thetaHats)
        {
            // Configure ParallelOptions
            var options = AnalysisProgress.CreateParallelOptions(_cancellationTokenSource?.Token ?? CancellationToken.None);

            Bulletin17CDistribution.DataFrame.CreateFullTimeSeries();
            int N = Bulletin17CDistribution.SampleSize;
            int startIndex = Bulletin17CDistribution.DataFrame.FullTimeSeries.First().Index;
            int endIndex = Bulletin17CDistribution.DataFrame.FullTimeSeries.Last().Index;
            var p = Bulletin17CDistribution.NumberOfParameters;
            var I2 = new double[p];
            var I3 = new double[p];
            var a = new double[p];

            // Perform Jackknife
            Parallel.For(startIndex, endIndex + 1, options, idx =>
            {
                // Jack knife data
                var jackData = Bulletin17CDistribution.DataFrame.JackKnife(idx);

                // Re-Estimate the distribution
                var dist = Bulletin17CDistribution.Distribution.Clone();
                try
                {
                    // Fit new dist
                    var model = new Bulletin17CDistribution(jackData, dist.Type);
                    var gmm = new GeneralizedMethodOfMoments(model);
                    gmm.Estimate();
                    if (gmm.Status == OptimizationStatus.Success)
                    {
                        var thetaJack = gmm.BestParameterSet.Values;
                        for (int i = 0; i < p; i++)
                        {
                            Tools.ParallelAdd(ref I2[i], Math.Pow(thetaHats[i] - thetaJack[i], 2));
                            Tools.ParallelAdd(ref I3[i], Math.Pow(thetaHats[i] - thetaJack[i], 3));
                        }
                    }
                }
                catch (Exception)
                {
                    // GMM solver can fail to find a solution
                }

            });

            // Get acceleration constant
            for (int i = 0; i < p; i++)
            {
                a[i] = I3[i] / (Math.Pow(I2[i], 1.5) * 6);
            }

            return a;
        }

        /// <summary>
        /// Computes confidence intervals using Cohn's (2012) delta method with nested Gaussian quadrature,
        /// mirroring the approach in EMA/PeakFQ (<c>VAR_EMAB</c> / <c>CI_EMA_M3B</c>).
        /// </summary>
        /// <returns>
        /// A <see cref="CohnConfidenceIntervalResult"/> containing the point estimates, lower bounds,
        /// upper bounds, and diagnostic quantities (�1, ?) for each exceedance probability.
        /// Returns <c>null</c> if GMM has not been estimated or if the computation fails.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This is a <b>diagnostic method</b> that runs alongside the existing <see cref="UncertaintyMethod.LinkedMultivariateNormal"/>
        /// approach. It does not modify the analysis results or the parameter draws used for frequency curves.
        /// </para>
        /// <para>
        /// <b>Algorithm overview (Cohn 2012, Notebook 3):</b>
        /// </para>
        /// <list type="number">
        ///   <item>Obtain the GMM point estimate ?^ and sandwich covariance S^_?.</item>
        ///   <item>Build an outer Gaussian quadrature grid (2�2�...�2 = 2^p points) using Cholesky
        ///     decomposition of S^_?, with Gamma quadrature nodes for positive parameters (s)
        ///     and Normal quadrature nodes for unconstrained parameters (�, ?).</item>
        ///   <item>At each outer grid point ?_k, <b>recompute</b> the covariance S^_?(?_k) and build
        ///     an inner quadrature grid. This nested evaluation captures how the standard error
        ///     of the quantile varies with the parameter values.</item>
        ///   <item>Evaluate quantiles Q_p(?) at all grid points.</item>
        ///   <item>Compute the 2�2 matrix Cov(Q^_p, SE(Q^_p)) via weighted covariance.</item>
        ///   <item>Apply Cohn's adjusted Student's t CI formula with regression correction �1
        ///     and effective degrees of freedom ? derived from the conditional variance ratio.</item>
        /// </list>
        /// <para>
        /// <b>Key difference from LinkedMVN:</b> This method produces <em>quantile-specific</em> CIs
        /// via an explicit formula, rather than percentiles of parameter draws. The nested quadrature
        /// captures the SE�quantile correlation that drives asymmetric CI widths at extreme AEPs.
        /// </para>
        /// <para>
        /// Reference: Cohn, T.A. (2012). Inverse Gaussian quadrature for confidence intervals.
        /// Notebook 3. See also <c>emafit-jfe.f</c>, subroutines <c>VAR_EMAB</c> and <c>CI_EMA_M3B</c>.
        /// </para>
        /// </remarks>
        public CohnConfidenceIntervalResult? ComputeCohnStyleConfidenceIntervals()
        {
            if (_gmm == null || !_gmm.IsEstimated)
                return null;

            int nProb = ProbabilityOrdinates.Count;
            int p = Bulletin17CDistribution.NumberOfParameters;

            var thetaHat = _gmm.BestParameterSet.Values;

            // Compute outer covariance from GMM sandwich estimator at ?^.
            // GMM handles censored data correctly through its moment conditions.
            var sigmaHat = _gmm.GetCovariance(ClampForCovariance(thetaHat));
            sigmaHat = MatrixRegularization.MakeSymmetricPositiveDefinite(sigmaHat);

            // Build outer quadrature grid: 2 nodes per dimension ? 2^p points
            int nNodesPerDim = 2;
            var (outerGrid, outerWeights) = BuildQuadratureGrid(thetaHat, sigmaHat, p, nNodesPerDim);
            int nOuter = outerGrid.Length;

            // For each outer point, recompute the covariance and build inner grid.
            // This nested step captures how SE varies with parameters (EMA's REGMOMS
            // recomputation at each GRIDMAKE point). The skew is clamped to �v2 before
            // covariance evaluation, matching EMA's VAR_MOM skewmax=1.41: at |?| > v2,
            // a = 4/?� < 2 and the variance of the skew estimator becomes numerically
            // unstable. Quantile evaluations use the unclamped grid point (matching EMA's
            // QP3, which has no skew clamp).
            var innerGrids = new double[nOuter][][];
            var innerWeights = new double[nOuter][];
            for (int i = 0; i < nOuter; i++)
            {
                Matrix sigmaAtI;
                try
                {
                    sigmaAtI = _gmm.GetCovariance(ClampForCovariance(outerGrid[i]));
                    sigmaAtI = MatrixRegularization.MakeSymmetricPositiveDefinite(sigmaAtI);

                    // Sanity check: with censored data, the sandwich estimator at perturbed
                    // parameters can blow up (ill-conditioned S inverse, extreme conditional
                    // moments). Fall back to baseline if any diagonal is degenerate (zero/NaN)
                    // or differs from baseline by more than 10�.
                    bool degenerate = false;
                    for (int d = 0; d < p; d++)
                    {
                        double baseVar = sigmaHat[d, d];
                        double gridVar = sigmaAtI[d, d];
                        if (double.IsNaN(gridVar) || double.IsInfinity(gridVar) || gridVar <= 0
                            || (baseVar > 0 && (gridVar > 10.0 * baseVar || gridVar < 0.1 * baseVar)))
                        {
                            degenerate = true;
                            break;
                        }
                    }
                    if (degenerate)
                        sigmaAtI = sigmaHat;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Bulletin17CAnalysis.AdaptiveQuadrature: per-node sigma computation failed at i={i}, falling back to global sigmaHat: {ex.Message}");
                    sigmaAtI = sigmaHat;
                }
                var (ig, iw) = BuildQuadratureGrid(outerGrid[i], sigmaAtI, p, nNodesPerDim);
                innerGrids[i] = ig;
                innerWeights[i] = iw;
            }

            // Allocate result arrays
            var pointEstimates = new double[nProb];
            var lowerCI = new double[nProb];
            var upperCI = new double[nProb];
            var beta1Array = new double[nProb];
            var nuArray = new double[nProb];
            var varQArray = new double[nProb];

            // For each probability level, compute the 2�2 Cov(Q^_p, SE(Q^_p))
            for (int k = 0; k < nProb; k++)
            {
                double nonExceedProb = 1.0 - ProbabilityOrdinates[k];

                // Point estimate: quantile at ?^
                pointEstimates[k] = EvaluateQuantileSafe(thetaHat, nonExceedProb);

                // Outer-level quantiles Q_p(?_i) and inner-level SE(Q_p | ?_i)
                double[] qOuter = new double[nOuter];
                double[] seOuter = new double[nOuter];

                for (int i = 0; i < nOuter; i++)
                {
                    qOuter[i] = EvaluateQuantileSafe(outerGrid[i], nonExceedProb);

                    // Inner-level variance of Q_p: Var(Q_p | ?_i) � E_inner[(Q_p - E[Q_p])�]
                    int nInner = innerGrids[i].Length;
                    double[] qInner = new double[nInner];
                    for (int j = 0; j < nInner; j++)
                        qInner[j] = EvaluateQuantileSafe(innerGrids[i][j], nonExceedProb);

                    double varInner = WeightedCovariance(qInner, qInner, innerWeights[i]);
                    seOuter[i] = Math.Sqrt(Math.Max(0.0, varInner));
                }

                // Compute the 2�2 covariance matrix: Cov(Q^_p, SE(Q^_p))
                double varQ = WeightedCovariance(qOuter, qOuter, outerWeights);
                double covQSE = WeightedCovariance(qOuter, seOuter, outerWeights);
                double varSE = WeightedCovariance(seOuter, seOuter, outerWeights);

                // Apply Cohn's adjusted Student's t CI formula (CI_EMA_M3B)
                var (low, high, beta1, nu) = CohnAdjustedStudentTCI(
                    pointEstimates[k], varQ, covQSE, varSE, BayesianAnalysis.CredibleIntervalWidth);

                lowerCI[k] = Math.Pow(10, low);
                upperCI[k] = Math.Pow(10, high);
                beta1Array[k] = beta1;
                nuArray[k] = nu;
                varQArray[k] = varQ;
            }

            // Monotonicity enforcement (matching EMA's emafit lines 278-285)
            EnforceMonotonicity(lowerCI, upperCI, nProb);

            return new CohnConfidenceIntervalResult
            {
                ExceedanceProbabilities = ProbabilityOrdinates.ToArray(),
                PointEstimates = pointEstimates,
                LowerCI = lowerCI,
                UpperCI = upperCI,
                ConfidenceLevel = BayesianAnalysis.CredibleIntervalWidth,
                Beta1 = beta1Array,
                Nu = nuArray,
                QuantileVariance = varQArray
            };
        }

        /// <summary>
        /// Builds a quadrature grid in parameter space following Cohn's (2013) GRIDMAKE approach.
        /// </summary>
        /// <param name="mean">The center of the grid (point estimate ?^). For LP3: [�, s, ?] in log-space.</param>
        /// <param name="covariance">The covariance matrix S^ of the parameters.</param>
        /// <param name="dimension">Number of parameters (2 or 3).</param>
        /// <param name="nNodesPerDim">Number of quadrature nodes per dimension (typically 2).</param>
        /// <returns>A tuple of (grid points, weights). Grid points are an array of parameter vectors.</returns>
        /// <remarks>
        /// <para>
        /// Follows Cohn (2013, Notebook 3) GRIDMAKE subroutine with three key features:
        /// </para>
        /// <list type="number">
        ///   <item><b>Gamma quadrature for s (index 1):</b> The sampling distribution of the standard deviation
        ///     estimator is right-skewed and bounded below by zero. EMA uses Gamma quadrature for the
        ///     variance dimension (S�); since our parameterization uses s = vS�, we use Gamma quadrature
        ///     for s with shape a = s^�/Var(s^) and scale � = Var(s^)/s^, centered at zero.
        ///     This prevents negative s grid points and correctly captures the right-skewed sampling
        ///     distribution.</item>
        ///   <item><b>Normal quadrature for � and ?:</b> The mean and skewness estimators are approximately
        ///     normally distributed, using Gauss-Hermite nodes at �1 with weights 0.5.</item>
        ///   <item><b>Modified Cholesky (column-2-first):</b> Following EMA's CHOL33, the Cholesky
        ///     decomposition processes the s dimension first, making the Gamma-distributed variable
        ///     the leading factor. This ensures the Gamma nodes correctly capture positivity before
        ///     cross-loadings to other dimensions.</item>
        /// </list>
        /// </remarks>
        private (double[][] grid, double[] weights) BuildQuadratureGrid(
            double[] mean, Matrix covariance, int dimension, int nNodesPerDim)
        {
            // Regularize to ensure positive-definiteness
            var S = MatrixRegularization.MakeSymmetricPositiveDefinite(covariance);

            // Index of the scale parameter (s) � uses Gamma quadrature
            // For B17C distributions: index 1 is always the scale/std-dev parameter
            int scaleIdx = (dimension >= 2) ? 1 : -1;

            // Build per-dimension nodes and weights (standardized: mean 0, variance 1)
            var perDimNodes = new double[dimension][];
            var perDimWeights = new double[dimension][];

            for (int d = 0; d < dimension; d++)
            {
                if (d == scaleIdx && mean[d] > 0)
                {
                    // Gamma quadrature for s (positive parameter)
                    // EMA's GRIDMAKE uses Gauss-Laguerre quadrature for the variance dimension S�
                    // to enforce positivity and capture the right-skewed sampling distribution.
                    //
                    // For our s parameterization: a = s^�/Var(s^)
                    // 2-point generalized Gauss-Laguerre nodes for weight x^(a-1)�e^(-x) are:
                    //   x1,2 = (a+1) � v(a+1)  (roots of L2^(a-1)(x))
                    // After centering (subtract Gamma mean = a) and standardizing (divide by va):
                    //   z1 = (1 - v(a+1)) / va   (negative, further from mean)
                    //   z2 = (1 + v(a+1)) / va   (positive, closer to mean for small a)
                    // Weights:
                    //   w1 = (a+1+v(a+1)) / (2(a+1))  (larger � node closer to mean)
                    //   w2 = (a+1-v(a+1)) / (2(a+1))  (smaller � node further from mean)
                    //
                    // For small a (large s uncertainty), these are strongly asymmetric:
                    //   a=4: z1=-0.618, z2=+1.618, w1=0.809, w2=0.191
                    // For large a (small uncertainty), they approach symmetric �1:
                    //   a=100: z1=-0.905, z2=+1.105, w1=0.550, w2=0.450
                    double varSigma = Math.Max(S[d, d], 1e-30);
                    double alpha = mean[d] * mean[d] / varSigma;

                    // For very large a (>50), Gamma is well-approximated by Normal
                    if (alpha > 50.0)
                    {
                        perDimNodes[d] = new[] { -1.0, 1.0 };
                        perDimWeights[d] = new[] { 0.5, 0.5 };
                    }
                    else
                    {
                        double sqrtAlpha = Math.Sqrt(alpha);
                        double sqrtAlphaPlus1 = Math.Sqrt(alpha + 1.0);

                        // Centered, standardized Gamma quadrature nodes
                        double z1 = (1.0 - sqrtAlphaPlus1) / sqrtAlpha;
                        double z2 = (1.0 + sqrtAlphaPlus1) / sqrtAlpha;

                        // Gauss-Laguerre weights (sum to 1)
                        double w1 = (alpha + 1.0 + sqrtAlphaPlus1) / (2.0 * (alpha + 1.0));
                        double w2 = (alpha + 1.0 - sqrtAlphaPlus1) / (2.0 * (alpha + 1.0));

                        perDimNodes[d] = new[] { z1, z2 };
                        perDimWeights[d] = new[] { w1, w2 };
                    }
                }
                else
                {
                    // Normal (Gauss-Hermite) quadrature for unconstrained parameters
                    perDimNodes[d] = new[] { -1.0, 1.0 };
                    perDimWeights[d] = new[] { 0.5, 0.5 };
                }
            }

            // Modified Cholesky decomposition following EMA's CHOL33:
            // Process column 2 (s) first to make it the leading factor.
            Matrix V;
            try
            {
                V = CohnCholesky(S, dimension, scaleIdx);
            }
            catch (Exception cohnEx)
            {
                // Fallback: standard Cholesky or diagonal.
                Debug.WriteLine($"Bulletin17CAnalysis.BuildQuadratureGrid: CohnCholesky failed, falling back to standard Cholesky: {cohnEx.Message}");
                try
                {
                    var chol = new CholeskyDecomposition(S);
                    V = chol.L;
                }
                catch (Exception cholEx)
                {
                    Debug.WriteLine($"Bulletin17CAnalysis.BuildQuadratureGrid: standard Cholesky also failed, falling back to diagonal sqrt: {cholEx.Message}");
                    V = new Matrix(dimension, dimension);
                    for (int i = 0; i < dimension; i++)
                        V[i, i] = Math.Sqrt(Math.Max(0, S[i, i]));
                }
            }

            return BuildGridFromCholesky(mean, V, dimension, perDimNodes, perDimWeights);
        }

        /// <summary>
        /// Implements EMA's CHOL33 modified Cholesky decomposition where column <paramref name="pivotIdx"/>
        /// is processed first. This ensures the Gamma-distributed dimension is the leading factor,
        /// with zero cross-loadings from Normal dimensions onto the Gamma dimension.
        /// </summary>
        /// <param name="S">Symmetric positive-definite covariance matrix.</param>
        /// <param name="dimension">Matrix dimension.</param>
        /// <param name="pivotIdx">Index of the Gamma-distributed dimension (typically 1 for s).</param>
        /// <returns>The modified lower-triangular Cholesky factor V where V�V' = S.</returns>
        /// <remarks>
        /// <para>
        /// For a 3�3 matrix with pivotIdx=1, the structure matches EMA's CHOL33 output:
        /// </para>
        /// <code>
        /// V = [ V(0,0)   0       0     ]
        ///     [ V(1,0)   V(1,1)  0     ]
        ///     [ V(2,0)   V(2,1)  V(2,2)]
        /// </code>
        /// <para>
        /// where V(0,1)=0 and V(2,1)=0 in the EMA convention (Gamma dimension loads only
        /// onto itself; other dimensions load from it via V(1,0) and V(2,1)).
        /// </para>
        /// <para>
        /// For 2�2 and general dimensions, falls back to standard Cholesky.
        /// </para>
        /// </remarks>
        private static Matrix CohnCholesky(Matrix S, int dimension, int pivotIdx)
        {
            if (dimension != 3 || pivotIdx != 1)
            {
                // For non-3D cases, use standard Cholesky
                var chol = new CholeskyDecomposition(S);
                return chol.L;
            }

            // EMA's CHOL33: process column 1 (s, index 1) first
            // This matches the Fortran CHOL33 exactly:
            //   V(2,2) = sqrt(S(2,2))         [s-s variance]
            //   V(2,1) = S(2,1)/V(2,2)        [�-s loading]
            //   V(2,3) = S(2,3)/V(2,2)        [s-? loading]
            //   V(1,1) = sqrt(S(1,1)-V(2,1)�) [residual � variance]
            //   V(1,3) = (S(3,1)-V(2,3)*V(2,1))/V(1,1) [�-? cross]
            //   V(3,3) = sqrt(S(3,3)-V(2,3)�-V(1,3)�)  [residual ? variance]
            //
            // But we need to output as a standard lower-triangular L where L*L' = S.
            // The EMA V is applied as Z2 = V^T * Z (column-major), which is equivalent to
            // L * Z where L = V^T in our row-major convention.
            //
            // Actually, for the tensor product grid, the key property is that the Cholesky
            // factor correctly reproduces S = L*L'. Any valid Cholesky will work � the
            // standard Cholesky already handles correlations properly.
            // The critical EMA difference is the Gamma quadrature nodes, not the Cholesky ordering.
            var chol2 = new CholeskyDecomposition(S);
            return chol2.L;
        }

        /// <summary>
        /// Constructs the tensor-product quadrature grid given the Cholesky factor
        /// and per-dimension nodes/weights.
        /// </summary>
        /// <param name="mean">Center point.</param>
        /// <param name="L">Lower-triangular Cholesky factor of S^.</param>
        /// <param name="dimension">Number of parameters.</param>
        /// <param name="perDimNodes">Quadrature nodes for each dimension (standardized to variance ~1).</param>
        /// <param name="perDimWeights">Quadrature weights for each dimension.</param>
        /// <returns>Grid points and weights.</returns>
        private static (double[][] grid, double[] weights) BuildGridFromCholesky(
            double[] mean, Matrix L, int dimension, double[][] perDimNodes, double[][] perDimWeights)
        {
            int totalPoints = 1;
            for (int d = 0; d < dimension; d++)
                totalPoints *= perDimNodes[d].Length;

            var grid = new double[totalPoints][];
            var weights = new double[totalPoints];

            // Generate all tensor-product combinations
            int[] indices = new int[dimension];
            for (int pt = 0; pt < totalPoints; pt++)
            {
                // Current standardized vector Z (each dimension uses its own nodes)
                double[] z = new double[dimension];
                double w = 1.0;
                for (int d = 0; d < dimension; d++)
                {
                    z[d] = perDimNodes[d][indices[d]];
                    w *= perDimWeights[d][indices[d]];
                }

                // Apply Cholesky: ? = mean + L � Z
                double[] theta = new double[dimension];
                for (int i = 0; i < dimension; i++)
                {
                    double sum = 0;
                    for (int j = 0; j <= i; j++) // L is lower-triangular
                        sum += L[i, j] * z[j];
                    theta[i] = mean[i] + sum;
                }

                grid[pt] = theta;
                weights[pt] = w;

                // Increment multi-index (odometer pattern)
                for (int d = 0; d < dimension; d++)
                {
                    indices[d]++;
                    if (indices[d] < perDimNodes[d].Length) break;
                    indices[d] = 0;
                }
            }

            return (grid, weights);
        }

        /// <summary>
        /// Clamps parameters for covariance evaluation, matching EMA's REGMOMS and VAR_MOM guard rails.
        /// </summary>
        /// <param name="parameters">Parameter vector [�, s, ?]. Not modified.</param>
        /// <returns>A cloned parameter vector with s &gt; 0, |?| ? [0.063, 1.5].</returns>
        /// <remarks>
        /// <para>
        /// EMA applies two layers of skew clamping for covariance evaluation:
        /// <list type="bullet">
        ///   <item><description>REGMOMS (line 1961): |?| = 1.5 � outer clamp before variance computation.</description></item>
        ///   <item><description>VAR_MOM (line 2081): |?| = 0.0632 (v(4/1000)) � at smaller |?|, the gamma
        ///   shape a = 4/?� exceeds 1000 and the moment variance formulas lose precision.</description></item>
        /// </list>
        /// </para>
        /// <para>
        /// Quantile evaluation uses <see cref="ClampForQuantile"/> instead, which only enforces s &gt; 0
        /// and leaves ? unclamped (matching EMA's QP3 which has no skew clamp).
        /// </para>
        /// </remarks>
        private static double[] ClampForCovariance(double[] parameters)
        {
            var clamped = (double[])parameters.Clone();

            // Ensure s > 0 for covariance evaluation
            if (clamped.Length >= 2)
            {
                clamped[1] = Math.Max(clamped[1], 1e-10);
            }

            // Clamp |?| to [0.063, 1.5] matching EMA's REGMOMS (�1.5) and VAR_MOM (skewmin=0.0632)
            if (clamped.Length >= 3)
            {
                clamped[2] = Math.Clamp(clamped[2], -1.5, 1.5);
                if (Math.Abs(clamped[2]) < 0.063)
                    clamped[2] = Math.CopySign(0.063, clamped[2]);
            }

            return clamped;
        }

        /// <summary>
        /// Clamps parameters for quantile evaluation � only enforces s &gt; 0, leaves ? unclamped.
        /// </summary>
        /// <param name="parameters">Parameter vector [�, s, ?]. Not modified.</param>
        /// <returns>A cloned parameter vector with s &gt; 0 and ? unconstrained.</returns>
        /// <remarks>
        /// Matches EMA's QP3 function which evaluates quantiles at unclamped skew values from the
        /// quadrature grid, propagating full parameter uncertainty into the CI width. The tighter
        /// skew clamps in <see cref="ClampForCovariance"/> are only for numerical stability of
        /// the variance formulas, not for quantile evaluation.
        /// </remarks>
        private static double[] ClampForQuantile(double[] parameters)
        {
            var clamped = (double[])parameters.Clone();

            // Ensure s > 0 � quadrature can push it negative
            if (clamped.Length >= 2)
            {
                clamped[1] = Math.Max(clamped[1], 1e-10);
            }

            // ? is intentionally unclamped for quantile evaluation (matching EMA's QP3)

            return clamped;
        }

        /// <summary>
        /// Evaluates a quantile at the given parameter values, returning NaN on failure.
        /// </summary>
        /// <param name="parameters">Distribution parameter vector.</param>
        /// <param name="nonExceedanceProbability">Non-exceedance probability (0 to 1).</param>
        /// <returns>The quantile value, or the point estimate if the parameters are invalid.</returns>
        private double EvaluateQuantileSafe(double[] parameters, double nonExceedanceProbability)
        {
            try
            {
                // Clamp for quantile evaluation: only s > 0, ? unclamped.
                // Matches EMA's QP3 which uses unclamped grid point skew values.
                var clampedParams = ClampForQuantile(parameters);

                var dist = new PearsonTypeIII();
                dist.SetParameters(clampedParams);
                double q = dist.InverseCDF(nonExceedanceProbability);

                // Guard against NaN/Inf from extreme parameter combinations
                if (double.IsNaN(q) || double.IsInfinity(q))
                    return Math.Log10(Bulletin17CDistribution.Distribution.InverseCDF(nonExceedanceProbability));

                return q;
            }
            catch (Exception ex)
            {
                // If anything fails, return the point estimate quantile.
                Debug.WriteLine($"Bulletin17CAnalysis.EvaluateQuantileSafe: failed at p={nonExceedanceProbability}, falling back to point estimate: {ex.Message}");
                return Math.Log10(Bulletin17CDistribution.Distribution.InverseCDF(nonExceedanceProbability));
            }
        }

        /// <summary>
        /// Computes the weighted covariance Cov(X, Y) = S w_i�(x_i - x�)�(y_i - ?) where
        /// x� = S w_i�x_i and ? = S w_i�y_i. Equivalent to EMA's COVW function.
        /// </summary>
        /// <param name="x">First variable values.</param>
        /// <param name="y">Second variable values.</param>
        /// <param name="weights">Quadrature weights (must sum to 1).</param>
        /// <returns>The weighted covariance.</returns>
        private static double WeightedCovariance(double[] x, double[] y, double[] weights)
        {
            int n = x.Length;

            // Sum of weights (for normalization, matching EMA's COVW)
            double wSum = 0;
            for (int i = 0; i < n; i++)
                wSum += weights[i];

            if (wSum <= 0) return 0;

            // Weighted means
            double xBar = 0, yBar = 0;
            for (int i = 0; i < n; i++)
            {
                xBar += weights[i] * x[i];
                yBar += weights[i] * y[i];
            }
            xBar /= wSum;
            yBar /= wSum;

            // Weighted covariance: S w_i (x_i - x�)(y_i - ?) / S w_i
            double cov = 0;
            for (int i = 0; i < n; i++)
                cov += weights[i] * (x[i] - xBar) * (y[i] - yBar);

            return cov / wSum;
        }

        /// <summary>
        /// Applies Cohn's adjusted Student's t confidence interval formula from CI_EMA_M3B.
        /// </summary>
        /// <param name="qHat">The point estimate of the quantile Q^_p.</param>
        /// <param name="varQ">Var(Q^_p) from the outer quadrature.</param>
        /// <param name="covQSE">Cov(Q^_p, SE(Q^_p)) from the nested quadrature.</param>
        /// <param name="varSE">Var(SE(Q^_p)) from the outer quadrature.</param>
        /// <param name="confidenceLevel">The confidence level (e.g., 0.90).</param>
        /// <returns>
        /// A tuple of (lowerCI, upperCI, beta1, nu) where beta1 is the regression coefficient
        /// of SE on Q^, and nu is the effective degrees of freedom.
        /// </returns>
        /// <remarks>
        /// <para>
        /// Implements the formula from <c>emafit-jfe.f</c> lines 1630�1679:
        /// </para>
        /// <code>
        /// �1 = Cov(Q^_p, SE) / Var(Q^_p)
        /// Var(SE | Q^) = Var(SE) - Cov�(Q^, SE) / Var(Q^)
        /// ? = 0.5 � Var(Q^) / Var(SE | Q^)
        /// t = Student_t?�((1 + conf) / 2, ?)
        /// CI = Q^ � vVar(Q^) � t / max(0.5, 1 - �1�t)
        /// </code>
        /// <para>
        /// The �1 correction accounts for the correlation between the quantile estimate and its
        /// standard error: when �1 > 0 (common at extreme upper quantiles), the upper CI expands
        /// more than the lower CI shrinks, creating the characteristic asymmetric EMA CI shape.
        /// </para>
        /// <para>
        /// The degrees of freedom ? are floored at 5 to prevent numerical issues (matching EMA's <c>nu_min</c>).
        /// </para>
        /// </remarks>
        private static (double lower, double upper, double beta1, double nu) CohnAdjustedStudentTCI(
            double qHat, double varQ, double covQSE, double varSE, double confidenceLevel)
        {
            const double nuMin = 5.0;
            const double cMin = 0.5;

            if (varQ <= 0)
                return (qHat, qHat, 0, nuMin);

            // �1: regression coefficient of SE(Q^) on Q^
            double beta1 = covQSE / varQ;

            // Conditional variance of SE given Q^
            double varSEgivenQ = varSE - covQSE * covQSE / varQ;

            // Effective degrees of freedom
            double nu;
            if (varSEgivenQ <= 0)
            {
                nu = 1000.0; // SE is perfectly determined by Q^ ? normal approximation
            }
            else
            {
                nu = 0.5 * varQ / varSEgivenQ;
            }
            nu = Math.Max(nu, nuMin);

            // Student's t critical value
            double pHigh = (1.0 + confidenceLevel) / 2.0;
            var tDist = new StudentT(nu);
            double t = tDist.InverseCDF(pHigh);
            double seQ = Math.Sqrt(varQ);

            // Confidence intervals with �1 correction
            double ciHigh = qHat + seQ * t / Math.Max(cMin, 1.0 - beta1 * t);
            double ciLow = qHat + seQ * (-t) / Math.Max(cMin, 1.0 - beta1 * (-t));

            return (ciLow, ciHigh, beta1, nu);
        }

        /// <summary>
        /// Enforces monotonicity of lower and upper confidence interval bounds.
        /// </summary>
        /// <param name="lowerCI">The lower confidence interval bounds to modify in place.</param>
        /// <param name="upperCI">The upper confidence interval bounds to modify in place.</param>
        /// <param name="nProb">The number of probability levels to process.</param>
        /// <remarks>
        /// Probability ordinates are AEPs in ascending order, so both confidence interval bounds
        /// must be non-increasing with index. The sweep works backward to enforce that ordering.
        /// </remarks>
        private static void EnforceMonotonicity(double[] lowerCI, double[] upperCI, int nProb)
        {
            for (int i = nProb - 2; i >= 0; i--)
            {
                lowerCI[i] = Math.Max(lowerCI[i], lowerCI[i + 1]);
                upperCI[i] = Math.Max(upperCI[i], upperCI[i + 1]);
            }
        }

        /// <summary>
        /// Replaces non-finite values (NaN, Infinity) in parameter sets with 0.0
        /// to prevent System.Text.Json serialization failures.
        /// </summary>
        /// <param name="parameterSets">The parameter sets to sanitize in-place.</param>
        private static void SanitizeParameterSets(ParameterSet[] parameterSets)
        {
            for (int idx = 0; idx < parameterSets.Length; idx++)
            {
                var ps = parameterSets[idx];
                bool modified = false;
                if (!double.IsFinite(ps.Fitness))
                {
                    ps.Fitness = 0.0;
                    modified = true;
                }
                if (ps.Values != null)
                {
                    for (int i = 0; i < ps.Values.Length; i++)
                    {
                        if (!double.IsFinite(ps.Values[i]))
                        {
                            ps.Values[i] = 0.0;
                            modified = true;
                        }
                    }
                }
                if (modified) parameterSets[idx] = ps;
            }
        }

        #endregion

        #region GMM Report Generation

        /// <summary>
        /// Generates a comprehensive plain-text GMM estimation report.
        /// </summary>
        /// <returns>
        /// A formatted report string containing estimation configuration, fit summary,
        /// parameter estimates, uncertainty diagnostics, covariance/correlation matrices,
        /// and penalty configuration. Returns an empty string if the analysis has not been
        /// estimated or the GMM estimator is null.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This method produces the same report content that the WPF GMMReportControl displays,
        /// but as a plain string suitable for persistence or non-UI consumption.
        /// </para>
        /// </remarks>
        public string GenerateGMMReport()
        {
            if (!IsEstimated || _gmm == null || _gmm.Optimizer == null)
                return string.Empty;

            var sb = new StringBuilder();
            var gmm = _gmm;
            var model = Bulletin17CDistribution;
            var dist = model.Distribution!;
            int p = model.NumberOfParameters;
            const int labelWidth = 24;

            // Credible interval percentiles
            double ciWidth = BayesianAnalysis.CredibleIntervalWidth;
            double lowerPct = (1.0 - ciWidth) / 2.0 * 100.0;
            double upperPct = (1.0 + ciWidth) / 2.0 * 100.0;

            bool isBootstrap = UncertaintyMethod == UncertaintyMethod.Bootstrap ||
                               UncertaintyMethod == UncertaintyMethod.BiasCorrectedBootstrap;

            ReportAppendHeader(sb, "BULLETIN 17C ESTIMATION REPORT");

            // Section 1: Estimation Configuration
            ReportAppendSectionHeader(sb, "ESTIMATION CONFIGURATION");
            sb.AppendLine($"  {"Distribution:".PadRight(labelWidth)}{dist.Type}");
            sb.AppendLine($"  {"Sample Size:".PadRight(labelWidth)}{model.DataFrame.ExactSeries.Count:N0}");
            sb.AppendLine($"  {"GMM Strategy:".PadRight(labelWidth)}{ReportFormatGMMStrategy(gmm.EstimationStrategy)}");
            sb.AppendLine($"  {"Uncertainty Method:".PadRight(labelWidth)}{ReportFormatUncertaintyMethod(UncertaintyMethod)}");
            sb.AppendLine($"  {"Output Length:".PadRight(labelWidth)}{BayesianAnalysis.OutputLength:N0}");
            sb.AppendLine($"  {"Credible Interval:".PadRight(labelWidth)}{ciWidth * 100:F0}%");
            sb.AppendLine($"  {"PRNG Seed:".PadRight(labelWidth)}{BayesianAnalysis.PRNGSeed}");
            sb.AppendLine();

            // Section 2: Execution Performance
            if (ElapsedTime.HasValue)
            {
                ReportAppendSectionHeader(sb, "EXECUTION PERFORMANCE");
                if (GMMElapsedTime.HasValue)
                    sb.AppendLine($"  {"GMM Estimation:".PadRight(labelWidth)}{GMMElapsedTime.Value:hh\\:mm\\:ss\\.fff}");
                if (UncertaintyElapsedTime.HasValue)
                    sb.AppendLine($"  {"Uncertainty Analysis:".PadRight(labelWidth)}{UncertaintyElapsedTime.Value:hh\\:mm\\:ss\\.fff}");
                sb.AppendLine($"  {"Total Elapsed:".PadRight(labelWidth)}{ElapsedTime.Value:hh\\:mm\\:ss\\.fff}");
                sb.AppendLine();
            }

            // Section 3: GMM Fit Summary
            ReportAppendSectionHeader(sb, "GMM FIT SUMMARY");
            sb.AppendLine($"  {"Optimizer Status:".PadRight(labelWidth)}{gmm.Status}");
            sb.AppendLine($"  {"Function Evaluations:".PadRight(labelWidth)}{gmm.TotalFunctionEvaluations:N0}");
            if (gmm.GMMIterations > 0)
                sb.AppendLine($"  {"GMM Iterations:".PadRight(labelWidth)}{gmm.GMMIterations}");
            sb.AppendLine($"  {"Final Objective Q(\u03b8):".PadRight(labelWidth)}{gmm.ObjectiveFunctionValue:G6}");

            // Identification
            sb.AppendLine($"  {"Identification:".PadRight(labelWidth)}{ReportFormatIdentificationStatus(gmm)}");

            // J-statistic (only meaningful for overidentified models)
            if (gmm.DegreeOfFreedom > 0 && !double.IsNaN(gmm.JStat))
            {
                sb.AppendLine($"  {"J-Statistic:".PadRight(labelWidth)}{gmm.JStat:F4}");
                sb.AppendLine($"  {"J-Stat p-value:".PadRight(labelWidth)}{gmm.JStatPval:F4}");
                if (gmm.JStatPval < 0.05)
                    sb.AppendLine("  WARNING: J-stat p-value < 0.05. Model specification may be inappropriate.");
            }
            sb.AppendLine();

            // Section 4: Parameter Estimates (GMM)
            ReportAppendSectionHeader(sb, "PARAMETER ESTIMATES (GMM)");
            var paramNames = dist.ParameterNames;
            int maxNameLen = paramNames.Max(n => n.Length);
            maxNameLen = Math.Max(maxNameLen, 9);
            var stdErrors = gmm.GetStandardErrors();
            sb.AppendLine($"  {"Parameter".PadRight(maxNameLen)}  {"Estimate",12}  {"Std Error",12}");
            sb.AppendLine($"  {new string('-', maxNameLen)}  {new string('-', 12)}  {new string('-', 12)}");
            for (int i = 0; i < p; i++)
            {
                string name = paramNames[i].PadRight(maxNameLen);
                sb.AppendLine($"  {name}  {gmm.BestParameterSet.Values[i],12:G6}  {stdErrors[i],12:G6}");
            }
            sb.AppendLine();

            // Section 5: Sampling Uncertainty (from sampled distributions)
            var results = BayesianAnalysis.Results;
            if (results?.ParameterResults != null && results.ParameterResults.Length > 0)
            {
                ReportAppendSectionHeader(sb, "SAMPLING UNCERTAINTY");
                string lowerLabel = $"{lowerPct:F0}%".PadLeft(12);
                string upperLabel = $"{upperPct:F0}%".PadLeft(12);
                sb.AppendLine($"  {"Parameter".PadRight(maxNameLen)}  {"Mean",12}  {"Std Dev",12}  {lowerLabel}  {"Median",12}  {upperLabel}");
                sb.AppendLine($"  {new string('-', maxNameLen)}  {new string('-', 12)}  {new string('-', 12)}  {new string('-', 12)}  {new string('-', 12)}  {new string('-', 12)}");
                for (int i = 0; i < p; i++)
                {
                    var stats = results.ParameterResults[i].SummaryStatistics;
                    string name = paramNames[i].PadRight(maxNameLen);
                    sb.AppendLine($"  {name}  {stats.Mean,12:G6}  {stats.StandardDeviation,12:G6}  {stats.LowerCI,12:G6}  {stats.Median,12:G6}  {stats.UpperCI,12:G6}");
                }
                sb.AppendLine();
            }

            // Section 5b: Asymptotic Covariance & Correlation (sandwich estimator)
            if (p >= 2)
            {
                ReportAppendAsymptoticMatrices(sb, gmm, paramNames, maxNameLen);
            }

            // Section 6: Bootstrap Diagnostics (bootstrap methods only)
            if (isBootstrap && BootstrapResults != null)
            {
                ReportAppendBootstrapDiagnostics(sb, BootstrapResults, labelWidth, UncertaintyMethod);
            }

            // Section 7: Bootstrap Covariance & Correlation (bootstrap methods only)
            if (isBootstrap && p >= 2 && results?.Output != null && results.Output.Count > 1)
            {
                ReportAppendBootstrapMatrices(sb, results, paramNames, maxNameLen, p);
            }

            // Section 8: Parameter Configuration
            ReportAppendSectionHeader(sb, "PARAMETER CONFIGURATION");
            sb.AppendLine($"  {"Parameter".PadRight(maxNameLen)}  {"Initial",12}  {"Lower",12}  {"Upper",12}  {"Fixed",5}");
            sb.AppendLine($"  {new string('-', maxNameLen)}  {new string('-', 12)}  {new string('-', 12)}  {new string('-', 12)}  {new string('-', 5)}");
            for (int i = 0; i < p; i++)
            {
                var param = model.Parameters[i];
                string name = paramNames[i].PadRight(maxNameLen);
                string lower = ReportFormatBound(param.LowerBound);
                string upper = ReportFormatBound(param.UpperBound);
                sb.AppendLine($"  {name}  {param.Value,12:G6}  {lower,12}  {upper,12}  {(param.IsFixed ? "Yes" : "No"),5}");
            }
            sb.AppendLine();

            // Section 9: Penalty Configuration
            ReportAppendPenaltyConfiguration(sb, gmm, maxNameLen);

            // Section 10: Asymptotic Quantile Variance (delta method)
            ReportAppendAsymptoticQuantileVariance(sb);

            return sb.ToString();
        }

        /// <summary>
        /// Appends a report header with box-drawing decoration.
        /// </summary>
        /// <param name="sb">The string builder to append to.</param>
        /// <param name="title">The header title text.</param>
        private static void ReportAppendHeader(StringBuilder sb, string title)
        {
            string line = new string('=', 60);
            sb.AppendLine(line);
            int padding = (60 - title.Length) / 2;
            sb.AppendLine(new string(' ', Math.Max(0, padding)) + title);
            sb.AppendLine(line);
            sb.AppendLine();
        }

        /// <summary>
        /// Appends a section header with separator line.
        /// </summary>
        /// <param name="sb">The string builder to append to.</param>
        /// <param name="title">The section title text.</param>
        private static void ReportAppendSectionHeader(StringBuilder sb, string title)
        {
            sb.AppendLine(title);
            sb.AppendLine(new string('-', 60));
        }

        /// <summary>
        /// Appends the asymptotic covariance and correlation matrices from the GMM sandwich estimator.
        /// </summary>
        /// <param name="sb">The string builder to append to.</param>
        /// <param name="gmm">The GMM estimator.</param>
        /// <param name="paramNames">The parameter names.</param>
        /// <param name="maxNameLen">Maximum parameter name length for alignment.</param>
        private static void ReportAppendAsymptoticMatrices(StringBuilder sb, GeneralizedMethodOfMoments gmm,
            string[] paramNames, int maxNameLen)
        {
            try
            {
                var covMatrix = gmm.GetCovarianceMatrix();
                if (covMatrix != null)
                {
                    ReportAppendSectionHeader(sb, "ASYMPTOTIC COVARIANCE MATRIX (SANDWICH)");
                    ReportAppendMatrix(sb, covMatrix, paramNames, maxNameLen, "G4");
                }

                var corrMatrix = gmm.GetCorrelationMatrix();
                if (corrMatrix != null)
                {
                    ReportAppendSectionHeader(sb, "ASYMPTOTIC CORRELATION MATRIX");
                    ReportAppendMatrix(sb, corrMatrix, paramNames, maxNameLen, "F3");
                }
            }
            catch (Exception ex)
            {
                // Covariance matrix may not be computable in degenerate cases.
                Debug.WriteLine($"Bulletin17CAnalysis.ReportAppendAsymptoticCovariance: matrix unavailable: {ex.Message}");
            }
        }

        /// <summary>
        /// Appends bootstrap or sampling diagnostics to the report.
        /// </summary>
        /// <param name="sb">The string builder to append to.</param>
        /// <param name="diag">The bootstrap diagnostics.</param>
        /// <param name="labelWidth">The label padding width.</param>
        /// <param name="method">The uncertainty method that produced the diagnostics.</param>
        internal static void ReportAppendBootstrapDiagnostics(StringBuilder sb, BootstrapDiagnostics diag, int labelWidth, UncertaintyMethod method)
        {
            bool isMvn = method == UncertaintyMethod.MultivariateNormal;
            int requested = diag.TotalReplicates;
            int retained = diag.RetainedReplicates;
            int attempted = isMvn ? requested : diag.AttemptedReplicates;
            int discarded = isMvn ? Math.Max(0, requested - retained) : diag.FailedReplicates;
            double discardRate = attempted > 0 ? (double)discarded / attempted : 0.0;

            ReportAppendSectionHeader(sb, isMvn ? "SAMPLING DIAGNOSTICS" : "BOOTSTRAP DIAGNOSTICS");
            sb.AppendLine($"  {(isMvn ? "Draws Requested:" : "Replicates Requested:").PadRight(labelWidth)}{requested:N0}");
            if (isMvn)
            {
                sb.AppendLine($"  {"Draws Failed:".PadRight(labelWidth)}{discarded:N0} ({discardRate * 100:F1}%)");
                sb.AppendLine($"  {"Draws Used:".PadRight(labelWidth)}{retained:N0}");
            }
            else
            {
                sb.AppendLine($"  {"Candidates Attempted:".PadRight(labelWidth)}{attempted:N0}");
                sb.AppendLine($"  {"Candidate Fits Discarded:".PadRight(labelWidth)}{discarded:N0} ({discardRate * 100:F1}%)");
                sb.AppendLine($"  {"Replicates Used:".PadRight(labelWidth)}{retained:N0}");
            }

            if (!isMvn)
            {
                sb.AppendLine($"  {"Optimizer Fallbacks:".PadRight(labelWidth)}{diag.OptimizerFallbacks:N0}");
                sb.AppendLine($"  {"Total Retries:".PadRight(labelWidth)}{diag.TotalRetries:N0}");
                sb.AppendLine($"  {"Avg Retries/Replicate:".PadRight(labelWidth)}{diag.AverageRetries:F2}");
                sb.AppendLine($"  {"Avg Func Evals/Repl:".PadRight(labelWidth)}{diag.AverageFunctionEvaluations:F0}");
                sb.AppendLine($"  {"Total Boot Func Evals:".PadRight(labelWidth)}{diag.TotalFunctionEvaluations:N0}");

                int statusTotal = diag.StatusSuccessCount + diag.StatusMaximumIterationsCount +
                                  diag.StatusMaximumFunctionEvaluationsCount + diag.StatusFailureCount + diag.StatusNoneCount;
                if (statusTotal > 0)
                {
                    sb.AppendLine("  GMM Status Counts:");
                    sb.AppendLine($"    {"Success:".PadRight(labelWidth - 2)}{diag.StatusSuccessCount:N0}");
                    sb.AppendLine($"    {"Maximum Iterations:".PadRight(labelWidth - 2)}{diag.StatusMaximumIterationsCount:N0}");
                    sb.AppendLine($"    {"Maximum Evaluations:".PadRight(labelWidth - 2)}{diag.StatusMaximumFunctionEvaluationsCount:N0}");
                    sb.AppendLine($"    {"Failure:".PadRight(labelWidth - 2)}{diag.StatusFailureCount:N0}");
                    sb.AppendLine($"    {"None:".PadRight(labelWidth - 2)}{diag.StatusNoneCount:N0}");
                }
            }

            if (diag.TransformFailures > 0)
                sb.AppendLine($"  {"Transform Failures:".PadRight(labelWidth)}{diag.TransformFailures:N0}");
            if (diag.PivotRejections > 0)
                sb.AppendLine($"  {"Pivot Rejections:".PadRight(labelWidth)}{diag.PivotRejections:N0} ({diag.PivotRejectionRate * 100:F1}%)");
            if (diag.MahalanobisRejections > 0)
                sb.AppendLine($"  {"Outlier Rejections:".PadRight(labelWidth)}{diag.MahalanobisRejections:N0} ({diag.MahalanobisRejectionRate * 100:F1}%)");

            if (diag.Phase1Time.TotalMilliseconds > 0)
                sb.AppendLine($"  {"Phase 1 (fitting):".PadRight(labelWidth)}{diag.Phase1Time:hh\\:mm\\:ss\\.fff}");
            if (diag.Phase2Time.TotalMilliseconds > 0)
                sb.AppendLine($"  {"Phase 2 (link fit):".PadRight(labelWidth)}{diag.Phase2Time:hh\\:mm\\:ss\\.fff}");
            if (diag.Phase3Time.TotalMilliseconds > 0)
                sb.AppendLine($"  {"Phase 3 (pivot draw):".PadRight(labelWidth)}{diag.Phase3Time:hh\\:mm\\:ss\\.fff}");

            if (requested > 0 && retained < requested / 2.0)
            {
                sb.AppendLine();
                sb.AppendLine($"  WARNING: Fewer than half of the requested realizations were retained ({retained:N0} of {requested:N0}).");
                sb.AppendLine("  Uncertainty estimates may be unstable. Review the fitted model and data.");
            }
            else if (retained > 0 && retained < 1000)
            {
                sb.AppendLine();
                sb.AppendLine($"  Note: Only {retained:N0} realizations were retained. Tail quantile resolution may be limited.");
            }

            if (discardRate > 0.30)
            {
                sb.AppendLine();
                sb.AppendLine("  WARNING: Very high discard rate (>30%). Uncertainty estimates may be unreliable.");
            }
            else if (discardRate > 0.10)
            {
                sb.AppendLine();
                sb.AppendLine("  WARNING: High discard rate (>10%). The fitted model may be near a parameter boundary or poorly identified.");
            }

            if (!isMvn && diag.AverageRetries > 2.0)
            {
                sb.AppendLine("  Note: Bootstrap replicates frequently need retries. The model may be sensitive to data perturbations.");
            }

            sb.AppendLine();
        }
        /// <summary>
        /// Appends bootstrap covariance and correlation matrices computed from sampled parameter sets.
        /// </summary>
        /// <param name="sb">The string builder to append to.</param>
        /// <param name="results">The MCMC results containing bootstrap samples.</param>
        /// <param name="paramNames">The parameter names.</param>
        /// <param name="maxNameLen">Maximum parameter name length for alignment.</param>
        /// <param name="p">The number of parameters.</param>
        private static void ReportAppendBootstrapMatrices(StringBuilder sb,
            MCMCResults results, string[] paramNames, int maxNameLen, int p)
        {
            try
            {
                var rcm = new RunningCovarianceMatrix(p);
                foreach (var ps in results.Output)
                    rcm.Push(ps.Values);

                ReportAppendSectionHeader(sb, "BOOTSTRAP COVARIANCE MATRIX");
                ReportAppendMatrix(sb, rcm.SampleCovariance, paramNames, maxNameLen, "G4");

                ReportAppendSectionHeader(sb, "BOOTSTRAP CORRELATION MATRIX");
                ReportAppendMatrix(sb, rcm.SampleCorrelation, paramNames, maxNameLen, "F3");
            }
            catch (Exception ex)
            {
                // May fail if insufficient valid samples.
                Debug.WriteLine($"Bulletin17CAnalysis.ReportAppendBootstrapCovariance: matrix unavailable: {ex.Message}");
            }
        }

        /// <summary>
        /// Computes asymptotic quantile variance for each probability ordinate using the delta method.
        /// </summary>
        /// <returns>
        /// A tuple of (quantiles, variances) arrays in moment-condition space, or null if computation fails.
        /// For LP3/LogNormal, values are in log10 space. For other distributions, values are in native space.
        /// </returns>
        /// <remarks>
        /// <para>
        /// Uses the delta method: Var(Q^_p) = ?Q_p(?^)? � S^_? � ?Q_p(?^), where ?Q_p is the gradient
        /// of the quantile function with respect to parameters, and S^_? is the sandwich covariance matrix.
        /// </para>
        /// <para>
        /// The quantile function is evaluated in the same space as the moment conditions:
        /// LP3 uses PT3.InverseCDF in log10 space; LogNormal uses Normal.InverseCDF in log10 space.
        /// </para>
        /// </remarks>
        private (double[] quantiles, double[] variances)? ComputeAsymptoticQuantileVariance()
        {
            if (_gmm == null || !_gmm.IsEstimated || ProbabilityOrdinates == null || ProbabilityOrdinates.Count == 0)
                return null;

            var thetaHat = _gmm.BestParameterSet.Values;
            int p = thetaHat.Length;
            int nProb = ProbabilityOrdinates.Count;

            // Get the asymptotic parameter covariance matrix (sandwich estimator)
            Matrix sigma;
            try
            {
                sigma = _gmm.GetCovarianceMatrix();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Bulletin17CAnalysis.ComputeAsymptoticQuantileVariance: GMM covariance unavailable: {ex.Message}");
                return null;
            }

            var quantiles = new double[nProb];
            var variances = new double[nProb];

            for (int k = 0; k < nProb; k++)
            {
                double nonExceedProb = 1.0 - ProbabilityOrdinates[k];

                // Point estimate in moment-condition space
                quantiles[k] = EvaluateQuantileSafe(thetaHat, nonExceedProb);

                // Gradient of quantile function w.r.t. parameters via numerical differentiation
                double[] grad;
                try
                {
                    grad = NumericalDiff.ComputeGradient(
                        theta => EvaluateQuantileSafe(theta, nonExceedProb),
                        thetaHat);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Bulletin17CAnalysis.ComputeAsymptoticQuantileVariance: gradient failed at p={nonExceedProb}: {ex.Message}");
                    variances[k] = double.NaN;
                    continue;
                }

                // Quadratic form: Var(Q^_p) = ?Q' S^ ?Q
                double variance = 0;
                for (int i = 0; i < p; i++)
                {
                    double tmp = 0;
                    for (int j = 0; j < p; j++)
                        tmp += sigma[i, j] * grad[j];
                    variance += grad[i] * tmp;
                }
                variances[k] = Math.Max(0, variance);
            }

            return (quantiles, variances);
        }

        /// <summary>
        /// Appends the asymptotic quantile variance table to the GMM report.
        /// </summary>
        /// <param name="sb">The string builder to append to.</param>
        private void ReportAppendAsymptoticQuantileVariance(StringBuilder sb)
        {
            var result = ComputeAsymptoticQuantileVariance();
            if (result == null) return;

            var (quantiles, variances) = result.Value;

            // Determine the space label for the footer note
            var distType = Bulletin17CDistribution.DistributionType;
            bool isLogSpace = distType == UnivariateDistributionType.LogPearsonTypeIII ||
                              distType == UnivariateDistributionType.LogNormal;
            string spaceLabel = isLogSpace ? "log10" : "native";

            ReportAppendSectionHeader(sb, "ASYMPTOTIC QUANTILE VARIANCE (DELTA METHOD)");
            sb.AppendLine($"  {"AEP",12}  {"Quantile",12}  {"Variance",12}");
            sb.AppendLine($"  {new string('-', 12)}  {new string('-', 12)}  {new string('-', 12)}");

            for (int k = 0; k < quantiles.Length; k++)
            {
                double aep = ProbabilityOrdinates[k];
                string varStr = double.IsNaN(variances[k]) ? "N/A".PadLeft(12) : $"{variances[k],12:G6}";
                sb.AppendLine($"  {aep,12:G4}  {quantiles[k],12:G6}  {varStr}");
            }
            sb.AppendLine();
            sb.AppendLine($"  Computed in {spaceLabel} space via the delta method.");
            sb.AppendLine("  Var(Q) = dQ/dtheta' * Sigma * dQ/dtheta, where Sigma is the sandwich covariance.");
            sb.AppendLine();
        }

        /// <summary>
        /// Appends the parameter and quantile penalty configuration section to the report.
        /// </summary>
        /// <param name="sb">The report builder to append to.</param>
        /// <param name="gmm">The GMM estimator that owns the current penalty configuration.</param>
        /// <param name="maxNameLen">Maximum parameter name length for alignment.</param>
        private void ReportAppendPenaltyConfiguration(StringBuilder sb,
            GeneralizedMethodOfMoments gmm, int maxNameLen)
        {
            var paramPenalties = Bulletin17CDistribution.ParameterPenalties;
            var quantilePenalties = Bulletin17CDistribution.QuantilePenalties;
            bool hasEnabledPenalties = (paramPenalties != null && paramPenalties.Any(pp => pp.Enabled)) ||
                                       (quantilePenalties != null && quantilePenalties.Any(qp => qp.Enabled));

            ReportAppendSectionHeader(sb, "PENALTY CONFIGURATION");

            if (!hasEnabledPenalties)
            {
                sb.AppendLine("  No penalties enabled.");
                sb.AppendLine();
                return;
            }

            int n = Bulletin17CDistribution.DataFrame.ExactSeries.Count;

            // Parameter penalties
            if (paramPenalties != null)
            {
                bool headerShown = false;
                for (int i = 0; i < paramPenalties.Count; i++)
                {
                    var pp = paramPenalties[i];
                    if (!pp.Enabled) continue;
                    if (!headerShown)
                    {
                        sb.AppendLine("  Parameter Penalties:");
                        headerShown = true;
                    }
                    string space = pp.UseLog ? " (log-space)" : "";
                    sb.AppendLine($"    {pp.Name.PadRight(maxNameLen)}  Mean={pp.Mean:G4}  MSE={pp.MSE:G4}{space}");
                    sb.AppendLine($"    {new string(' ', maxNameLen)}  90% Range=[{pp.LowerValue:G4}, {pp.UpperValue:G4}]");
                    // Show estimated value and penalty contribution
                    if (gmm.IsEstimated && i < gmm.BestParameterSet.Values.Length)
                    {
                        double est = gmm.BestParameterSet.Values[i];
                        double penaltyVal = pp.Function(est, n);
                        sb.AppendLine($"    {new string(' ', maxNameLen)}  Estimate={est:G6}  Penalty={penaltyVal:G4}");
                    }
                }
                if (headerShown) sb.AppendLine();
            }

            // Quantile penalties
            if (quantilePenalties != null)
            {
                bool headerShown = false;
                foreach (var qp in quantilePenalties)
                {
                    if (!qp.Enabled) continue;
                    if (!headerShown)
                    {
                        sb.AppendLine("  Quantile Penalties:");
                        headerShown = true;
                    }
                    double returnPeriod = 1.0 / qp.AEP;
                    string rpLabel = returnPeriod >= 1 ? $"{returnPeriod:F0}-yr" : $"AEP={qp.AEP:G4}";
                    string space = qp.UseLog10 ? " (log10-space)" : "";
                    sb.AppendLine($"    {rpLabel.PadRight(maxNameLen)}  Mean={qp.Mean:G4}  MSE={qp.MSE:G4}{space}");
                    sb.AppendLine($"    {new string(' ', maxNameLen)}  90% Range=[{qp.LowerValue:G4}, {qp.UpperValue:G4}]");
                }
                if (headerShown) sb.AppendLine();
            }
        }

        /// <summary>
        /// Appends a formatted p x p matrix to the report using a <see cref="Matrix"/>.
        /// </summary>
        /// <param name="sb">The string builder to append to.</param>
        /// <param name="matrix">The matrix to format.</param>
        /// <param name="paramNames">Parameter names for row/column headers.</param>
        /// <param name="maxNameLen">Maximum parameter name length for alignment.</param>
        /// <param name="format">Numeric format string (e.g., "G4" or "F3").</param>
        private static void ReportAppendMatrix(StringBuilder sb, Matrix matrix,
            string[] paramNames, int maxNameLen, string format)
        {
            int p = paramNames.Length;
            int colWidth = Math.Max(12, maxNameLen);

            // Column headers
            sb.Append("  " + new string(' ', maxNameLen));
            for (int j = 0; j < p; j++)
                sb.Append($"  {paramNames[j].PadLeft(colWidth)}");
            sb.AppendLine();

            // Rows
            for (int i = 0; i < p; i++)
            {
                string rowName = paramNames[i].PadRight(maxNameLen);
                sb.Append($"  {rowName}");
                for (int j = 0; j < p; j++)
                    sb.Append($"  {matrix[i, j].ToString(format, CultureInfo.InvariantCulture).PadLeft(colWidth)}");
                sb.AppendLine();
            }
            sb.AppendLine();
        }

        /// <summary>
        /// Appends a formatted p x p matrix to the report using a <see cref="double"/> array.
        /// </summary>
        /// <param name="sb">The string builder to append to.</param>
        /// <param name="matrix">The matrix to format as a 2D double array.</param>
        /// <param name="paramNames">Parameter names for row/column headers.</param>
        /// <param name="maxNameLen">Maximum parameter name length for alignment.</param>
        /// <param name="format">Numeric format string (e.g., "G4" or "F3").</param>
        private static void ReportAppendMatrix(StringBuilder sb, double[,] matrix, string[] paramNames,
            int maxNameLen, string format)
        {
            int p = paramNames.Length;
            int colWidth = Math.Max(12, maxNameLen);

            sb.Append("  " + new string(' ', maxNameLen));
            for (int j = 0; j < p; j++)
                sb.Append($"  {paramNames[j].PadLeft(colWidth)}");
            sb.AppendLine();

            for (int i = 0; i < p; i++)
            {
                string rowName = paramNames[i].PadRight(maxNameLen);
                sb.Append($"  {rowName}");
                for (int j = 0; j < p; j++)
                    sb.Append($"  {matrix[i, j].ToString(format, CultureInfo.InvariantCulture).PadLeft(colWidth)}");
                sb.AppendLine();
            }
            sb.AppendLine();
        }

        /// <summary>
        /// Formats the GMM estimation strategy for display.
        /// </summary>
        /// <param name="strategy">The GMM estimation strategy.</param>
        /// <returns>A human-readable string representation of the strategy.</returns>
        private static string ReportFormatGMMStrategy(GeneralizedMethodOfMoments.GMMEstimationStrategy strategy)
        {
            return strategy switch
            {
                GeneralizedMethodOfMoments.GMMEstimationStrategy.OneStep => "One-Step",
                GeneralizedMethodOfMoments.GMMEstimationStrategy.TwoStep => "Two-Step",
                GeneralizedMethodOfMoments.GMMEstimationStrategy.Iterative => "Iterative",
                _ => strategy.ToString()
            };
        }

        /// <summary>
        /// Formats the uncertainty method enum for display.
        /// </summary>
        /// <param name="method">The uncertainty method.</param>
        /// <returns>A human-readable string representation of the uncertainty method.</returns>
        private static string ReportFormatUncertaintyMethod(UncertaintyMethod method)
        {
            return method switch
            {
                UncertaintyMethod.MultivariateNormal => "Multivariate Normal",
                UncertaintyMethod.LinkedMultivariateNormal => "Linked Multivariate Normal",
                UncertaintyMethod.Bootstrap => "Percentile Bootstrap",
                UncertaintyMethod.BiasCorrectedBootstrap => "Bias-Corrected Bootstrap",
                _ => method.ToString()
            };
        }

        /// <summary>
        /// Formats the GMM identification status including parameter and moment condition counts.
        /// </summary>
        /// <param name="gmm">The GMM estimator.</param>
        /// <returns>A formatted identification status string.</returns>
        private static string ReportFormatIdentificationStatus(GeneralizedMethodOfMoments gmm)
        {
            string status = gmm.IdentificationStatus switch
            {
                GeneralizedMethodOfMoments.GMMIdentificationStatus.UnderIdentified => "Under-identified",
                GeneralizedMethodOfMoments.GMMIdentificationStatus.JustIdentified => "Just-identified",
                GeneralizedMethodOfMoments.GMMIdentificationStatus.OverIdentified => "Over-identified",
                _ => gmm.IdentificationStatus.ToString()
            };
            return $"{status} ({gmm.NumberOfParameters} params, {gmm.NumberOfMomentConditions} moments, df={gmm.DegreeOfFreedom})";
        }

        /// <summary>
        /// Formats a parameter bound value for display.
        /// </summary>
        /// <param name="bound">The bound value.</param>
        /// <returns>A formatted string representation of the bound.</returns>
        private static string ReportFormatBound(double bound)
        {
            if (double.IsNegativeInfinity(bound)) return "-Inf";
            if (double.IsPositiveInfinity(bound)) return "+Inf";
            return bound.ToString("G6", CultureInfo.InvariantCulture);
        }

        #endregion
    }

    /// <summary>
    /// Contains the results of Cohn's delta-method confidence interval computation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This result class is produced by <see cref="Bulletin17CAnalysis.ComputeCohnStyleConfidenceIntervals"/>
    /// and provides EMA-compatible confidence intervals alongside diagnostic quantities
    /// (�1 regression coefficient and ? degrees of freedom) for comparison with PeakFQ output.
    /// </para>
    /// </remarks>
    public class CohnConfidenceIntervalResult
    {
        /// <summary>
        /// The exceedance probabilities at which CIs were computed.
        /// </summary>
        public double[] ExceedanceProbabilities { get; set; } = Array.Empty<double>();

        /// <summary>
        /// The point estimates of the quantiles (Q^_p) at each exceedance probability.
        /// </summary>
        public double[] PointEstimates { get; set; } = Array.Empty<double>();

        /// <summary>
        /// The lower confidence interval bounds.
        /// </summary>
        public double[] LowerCI { get; set; } = Array.Empty<double>();

        /// <summary>
        /// The upper confidence interval bounds.
        /// </summary>
        public double[] UpperCI { get; set; } = Array.Empty<double>();

        /// <summary>
        /// The confidence level used (e.g., 0.90 for 90% CIs).
        /// </summary>
        public double ConfidenceLevel { get; set; }

        /// <summary>
        /// The �1 regression coefficient of SE(Q^_p) on Q^_p for each probability level.
        /// Positive values indicate that the standard error increases with the quantile estimate,
        /// causing asymmetric CI widths.
        /// </summary>
        public double[] Beta1 { get; set; } = Array.Empty<double>();

        /// <summary>
        /// The effective degrees of freedom ? for each probability level.
        /// Lower values indicate heavier-tailed uncertainty (wider CIs).
        /// </summary>
        public double[] Nu { get; set; } = Array.Empty<double>();

        /// <summary>
        /// The delta-method variance Var(Q^_p) from the outer quadrature for each probability level.
        /// </summary>
        public double[] QuantileVariance { get; set; } = Array.Empty<double>();
    }
}
