using Numerics.Data;
using Numerics.Distributions;
using Numerics.Distributions.Copulas;
using Numerics.Data.Statistics;
using Numerics.Sampling.MCMC;
using Numerics.Utilities;
using RMC.BestFit.Estimation;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Xml.Linq;

namespace RMC.BestFit.Analyses
{
    /// <summary>
    /// Performs coincident frequency analysis on an externally-supplied bivariate
    /// response surface using the law of total probability and the fitted bivariate
    /// copula's conditional CDF.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    /// Given a fitted <see cref="BivariateAnalysis"/> (marginals X, Y and copula C) and
    /// an external response surface Z = f(X, Y) tabulated on an M � N grid, the analysis
    /// produces a stage-frequency curve P(Z = z) with posterior uncertainty bands.
    /// </para>
    /// <para>
    /// The integration uses the chain-rule decomposition P(A,B) = P(A|B) P(B) materialised
    /// per Y column: for each Z output bin <i>z</i> and each Y column <i>j</i>, the X value
    /// x*(z, j) that produces output <i>z</i> is found by linear interpolation of
    /// (XValues, BivariateResponse[*, j]); the contribution of that column to F_Z(z) is
    /// the 2-point copula CDF difference C(F_X(x*), F_Y(yEdge_{j+1})) - C(F_X(x*), F_Y(yEdge_j)).
    /// Summing over Y columns gives F_Z(z); 1 - F_Z(z) is the AEP at that bin.
    /// </para>
    /// <para>
    /// Per-realisation uncertainty is obtained by looping over posterior samples from the
    /// three independent MCMC chains (marginal X, marginal Y, copula). Chains are paired by
    /// index; the loop length is the minimum of the three chain lengths. If any of the
    /// marginal chains is null, the algorithm falls back to point-estimate marginals and
    /// only the copula varies across realisations.
    /// </para>
    /// </remarks>
    public class CoincidentFrequencyAnalysis : AnalysisBase
    {
        #region Construction

        /// <summary>
        /// Initializes a new instance of the <see cref="CoincidentFrequencyAnalysis"/> class
        /// with empty inputs.
        /// </summary>
        public CoincidentFrequencyAnalysis()
        {
            BayesianAnalysis = new BayesianAnalysis();
            BayesianAnalysis.PropertyChanged += BayesianAnalysis_PropertyChanged;
            XValues = Array.Empty<double>();
            YValues = Array.Empty<double>();
            BivariateResponse = new double[0, 0];
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CoincidentFrequencyAnalysis"/> class
        /// with a fitted <see cref="BivariateAnalysis"/> and a response surface.
        /// </summary>
        /// <param name="bivariateAnalysis">The fitted bivariate analysis containing marginals and copula.</param>
        /// <param name="xValues">The X (primary) ordinates, strictly ascending. Length M = rows of <paramref name="bivariateResponse"/>.</param>
        /// <param name="yValues">The Y (secondary) ordinates, strictly ascending. Length N = columns of <paramref name="bivariateResponse"/>.</param>
        /// <param name="bivariateResponse">The response surface Z[i, j] (M � N), strictly increasing along both axes.</param>
        /// <exception cref="ArgumentNullException">Any argument is null.</exception>
        public CoincidentFrequencyAnalysis(BivariateAnalysis bivariateAnalysis, double[] xValues, double[] yValues, double[,] bivariateResponse)
        {
            BayesianAnalysis = new BayesianAnalysis();
            BayesianAnalysis.PropertyChanged += BayesianAnalysis_PropertyChanged;
            BivariateAnalysis = bivariateAnalysis ?? throw new ArgumentNullException(nameof(bivariateAnalysis));
            XValues = xValues ?? throw new ArgumentNullException(nameof(xValues));
            YValues = yValues ?? throw new ArgumentNullException(nameof(yValues));
            BivariateResponse = bivariateResponse ?? throw new ArgumentNullException(nameof(bivariateResponse));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CoincidentFrequencyAnalysis"/> class
        /// from a serialized <see cref="XElement"/> and a fitted bivariate analysis.
        /// </summary>
        /// <param name="bivariateAnalysis">The fitted bivariate analysis (the upstream input � not serialized here).</param>
        /// <param name="xElement">The XML element containing the persisted state.</param>
        /// <exception cref="ArgumentNullException">Either argument is null.</exception>
        public CoincidentFrequencyAnalysis(BivariateAnalysis bivariateAnalysis, XElement xElement)
        {
            if (xElement == null) throw new ArgumentNullException(nameof(xElement));

            // BayesianAnalysis settings (CredibleIntervalWidth, OutputLength, PointEstimator).
            // Mirrors CompositeAnalysis: presentation-only, no MCMC chain on CFA itself.
            var bayesElement = xElement.Element(nameof(BayesianAnalysis));
            BayesianAnalysis = bayesElement != null
                ? new BayesianAnalysis(bayesElement)
                : new BayesianAnalysis();
            BayesianAnalysis.PropertyChanged += BayesianAnalysis_PropertyChanged;

            BivariateAnalysis = bivariateAnalysis ?? throw new ArgumentNullException(nameof(bivariateAnalysis));

            // X Values
            var xValuesElement = xElement.Element(nameof(XValues));
            XValues = ParseDoubleArray(xValuesElement?.Value);

            // Y Values
            var yValuesElement = xElement.Element(nameof(YValues));
            YValues = ParseDoubleArray(yValuesElement?.Value);

            // Bivariate response (legacy schema may still use "ZTable")
            var responseElement = xElement.Element(nameof(BivariateResponse)) ?? xElement.Element("ZTable");
            if (responseElement != null && XValues.Length > 0 && YValues.Length > 0)
            {
                int rows = XValues.Length;
                int cols = YValues.Length;
                var flat = ParseDoubleArray(responseElement.Value);
                BivariateResponse = new double[rows, cols];
                int idx = 0;
                for (int i = 0; i < rows && idx < flat.Length; i++)
                {
                    for (int j = 0; j < cols && idx < flat.Length; j++)
                    {
                        BivariateResponse[i, j] = flat[idx++];
                    }
                }
            }
            else
            {
                BivariateResponse = new double[0, 0];
            }

            // Number of bins
            var binsAttr = xElement.Attribute(nameof(NumberOfBins));
            if (binsAttr != null && int.TryParse(binsAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out int bins))
            {
                _numberOfBins = bins;
            }

            // Is estimated
            var isEstimatedAttr = xElement.Attribute("IsEstimated");
            if (isEstimatedAttr != null && bool.TryParse(isEstimatedAttr.Value, out bool isEst))
            {
                _isEstimated = isEst;
            }
        }

        #endregion

        #region Members

        private BivariateAnalysis _bivariateAnalysis = null!;
        private double[] _xValues = null!;
        private double[] _yValues = null!;
        private double[,] _bivariateResponse = null!;
        private int _numberOfBins = 50;
        private MCMCResults? _marginalXChain;
        private MCMCResults? _marginalYChain;
        private int[][]? _posteriorRandomIndexes;
        private int[]? _posteriorRandomIndexSourceCounts;
        private int _posteriorRandomIndexSeed = -1;

        /// <summary>
        /// Gets the Bayesian result settings (CredibleIntervalWidth, OutputLength,
        /// PointEstimator, and PRNGSeed).
        /// CFA does not own an MCMC chain; this object holds presentation and result-generation settings.
        /// PRNGSeed controls cross-source posterior resampling; the remaining values control presentation.
        /// </summary>
        public BayesianAnalysis BayesianAnalysis { get; private set; } = null!;

        /// <summary>
        /// Gets or sets the upstream <see cref="BivariateAnalysis"/> that supplies the fitted
        /// marginals and copula. Must be estimated before <see cref="RunAsync"/> can produce
        /// meaningful results.
        /// </summary>
        public BivariateAnalysis BivariateAnalysis
        {
            get { return _bivariateAnalysis; }
            set
            {
                if (_bivariateAnalysis != null)
                    _bivariateAnalysis.PropertyChanged -= BivariateAnalysis_PropertyChanged;

                _bivariateAnalysis = value;

                if (_bivariateAnalysis != null)
                    _bivariateAnalysis.PropertyChanged += BivariateAnalysis_PropertyChanged;

                ClearResults();
                RaisePropertyChange(nameof(BivariateAnalysis));
            }
        }

        /// <summary>
        /// Gets or sets the primary ordinates (X axis). Must be strictly ascending.
        /// Length M defines the number of rows of <see cref="BivariateResponse"/>.
        /// </summary>
        public double[] XValues
        {
            get { return _xValues; }
            set
            {
                _xValues = value;
                ClearResults();
                RaisePropertyChange(nameof(XValues));
            }
        }

        /// <summary>
        /// Gets or sets the secondary ordinates (Y axis). Must be strictly ascending.
        /// Length N defines the number of columns of <see cref="BivariateResponse"/>.
        /// </summary>
        public double[] YValues
        {
            get { return _yValues; }
            set
            {
                _yValues = value;
                ClearResults();
                RaisePropertyChange(nameof(YValues));
            }
        }

        /// <summary>
        /// Gets or sets the response surface Z[i, j] indexed by (X primary row i, Y secondary
        /// column j). Must be strictly increasing along both axes for the per-Y-column
        /// inversion to be well-defined.
        /// </summary>
        public double[,] BivariateResponse
        {
            get { return _bivariateResponse; }
            set
            {
                _bivariateResponse = value;
                ClearResults();
                RaisePropertyChange(nameof(BivariateResponse));
            }
        }

        /// <summary>
        /// Gets or sets the number of evenly-spaced Z output bins. Default is 50.
        /// Range checking (5 = N = 1000) and slow-run warning (N &gt; 100) are surfaced
        /// through <see cref="Validate"/> rather than the setter, matching the rest of
        /// the library.
        /// </summary>
        public int NumberOfBins
        {
            get { return _numberOfBins; }
            set
            {
                if (_numberOfBins != value)
                {
                    _numberOfBins = value;
                    ClearResults();
                    RaisePropertyChange(nameof(NumberOfBins));
                }
            }
        }

        /// <summary>
        /// Gets or sets the X marginal posterior MCMC samples. When non-null, each
        /// realisation uses an independently randomized, without-replacement index into
        /// <see cref="MCMCResults.Output"/>. When null, the algorithm uses the point-estimate
        /// marginal X.
        /// </summary>
        public MCMCResults? MarginalXChain
        {
            get { return _marginalXChain; }
            set
            {
                if (ReferenceEquals(_marginalXChain, value)) return;
                _marginalXChain = value;
                ClearResults();
                RaisePropertyChange(nameof(MarginalXChain));
            }
        }

        /// <summary>
        /// Gets or sets the Y marginal posterior MCMC samples. Same semantics as
        /// <see cref="MarginalXChain"/>.
        /// </summary>
        public MCMCResults? MarginalYChain
        {
            get { return _marginalYChain; }
            set
            {
                if (ReferenceEquals(_marginalYChain, value)) return;
                _marginalYChain = value;
                ClearResults();
                RaisePropertyChange(nameof(MarginalYChain));
            }
        }

        /// <summary>
        /// Gets the uncertainty analysis results: ModeCurve, MeanCurve, ConfidenceIntervals
        /// indexed by Z output bin (length <see cref="NumberOfBins"/>).
        /// </summary>
        public UncertaintyAnalysisResults? AnalysisResults { get; private set; }

        /// <summary>
        /// Gets the Z output bin values where AEPs are evaluated. Endpoint-inclusive linspace
        /// of length <see cref="NumberOfBins"/> covering the min and max of
        /// <see cref="BivariateResponse"/>. Populated alongside <see cref="AnalysisResults"/>.
        /// </summary>
        public double[]? ZOutputValues { get; private set; }

        #endregion

        #region Methods

        /// <summary>
        /// Supports the <c>BivariateAnalysis_PropertyChanged</c> helper.
        /// </summary>
        /// <param name="sender">The event source.</param>
        /// <param name="e">The event data.</param>
        /// <remarks>
        /// This member supports the owning analysis or model implementation.
        /// </remarks>
        private void BivariateAnalysis_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(BivariateAnalysis.IsEstimated))
            {
                if (BivariateAnalysis?.IsEstimated == false)
                    ClearResults();
                RaisePropertyChange(e.PropertyName);
            }
        }

        /// <summary>
        /// Handles property changes on the <see cref="BayesianAnalysis"/> object.
        /// Loss of estimated state clears results; PointEstimator changes update the
        /// point-estimate output without re-running the chain; CredibleIntervalWidth
        /// changes clear derived CFA results because the per-realisation AEP matrix is
        /// not cached and the user must rerun explicitly. PRNGSeed changes also clear the
        /// cached cross-source posterior pairing and derived results.
        /// All other notifications propagate for UI binding.
        /// </summary>
        private void BayesianAnalysis_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Estimation.BayesianAnalysis.IsEstimated))
            {
                if (BayesianAnalysis.IsEstimated == false)
                    ClearResults();
                RaisePropertyChange(e.PropertyName);
            }
            else if (e.PropertyName == nameof(Estimation.BayesianAnalysis.PointEstimator))
            {
                ReprocessIfEstimated(UpdatePointEstimateResultsAsync);
                RaisePropertyChange(e.PropertyName);
            }
            else if (e.PropertyName == nameof(Estimation.BayesianAnalysis.CredibleIntervalWidth))
            {
                ClearResults();
                RaisePropertyChange(e.PropertyName);
            }
            else if (e.PropertyName == nameof(Estimation.BayesianAnalysis.PRNGSeed))
            {
                ClearResults();
                RaisePropertyChange(e.PropertyName);
            }
            else
            {
                RaisePropertyChange(e.PropertyName);
            }
        }

        /// <summary>
        /// Returns the cached posterior-index mapping when it matches the current source
        /// counts and seed, or creates a new mapping when the cache is absent or stale.
        /// </summary>
        /// <param name="copulaResults">The required upstream copula posterior results.</param>
        /// <returns>
        /// Index rows in semantic order: copula, X marginal when present, and Y marginal
        /// when present; or <c>null</c> when any supplied source has no retained output.
        /// </returns>
        /// <remarks>
        /// The mapping is generated before parallel processing so no random-number generator
        /// is shared by worker threads. It is not serialized; deterministic regeneration from
        /// the saved seed preserves the same finite pairing after a project is reopened.
        /// </remarks>
        private int[][]? GetOrCreatePosteriorRandomIndexes(MCMCResults copulaResults)
        {
            var sourceOutputCounts = new List<int>
            {
                copulaResults.Output?.Count ?? 0
            };
            if (MarginalXChain != null)
                sourceOutputCounts.Add(MarginalXChain.Output?.Count ?? 0);
            if (MarginalYChain != null)
                sourceOutputCounts.Add(MarginalYChain.Output?.Count ?? 0);

            if (sourceOutputCounts.Any(count => count <= 0)) return null;

            bool cacheMatches = _posteriorRandomIndexes != null &&
                _posteriorRandomIndexSourceCounts != null &&
                _posteriorRandomIndexSeed == BayesianAnalysis.PRNGSeed &&
                _posteriorRandomIndexSourceCounts.SequenceEqual(sourceOutputCounts);
            if (cacheMatches) return _posteriorRandomIndexes;

            _posteriorRandomIndexes = PosteriorIndexResampler.CreateRandomIndexes(
                sourceOutputCounts,
                BayesianAnalysis.PRNGSeed);
            _posteriorRandomIndexSourceCounts = sourceOutputCounts.ToArray();
            _posteriorRandomIndexSeed = BayesianAnalysis.PRNGSeed;
            return _posteriorRandomIndexes;
        }

        /// <summary>
        /// Clears the transient posterior-index mapping and its cache key.
        /// </summary>
        /// <remarks>
        /// Posterior index arrays are derived state. They are regenerated from the current
        /// posterior source counts and <see cref="BayesianAnalysis.PRNGSeed"/> when needed.
        /// </remarks>
        private void InvalidatePosteriorRandomIndexes()
        {
            _posteriorRandomIndexes = null;
            _posteriorRandomIndexSourceCounts = null;
            _posteriorRandomIndexSeed = -1;
        }

        /// <summary>
        /// Clears <see cref="AnalysisResults"/> and <see cref="ZOutputValues"/> and resets
        /// <see cref="AnalysisBase.IsEstimated"/> to false.
        /// </summary>
        public void ClearResults()
        {
            InvalidatePosteriorRandomIndexes();
            AnalysisResults = null;
            ZOutputValues = null;
            RaisePropertyChange(nameof(AnalysisResults));
            RaisePropertyChange(nameof(ZOutputValues));
            IsEstimated = false;
        }

        /// <inheritdoc/>
        public override async Task RunAsync(SafeProgressReporter? progressReporter = null)
        {
            var (isValid, messages) = Validate();
            if (!isValid)
                throw new InvalidOperationException(string.Join("; ", messages));

            var previewArgs = new CancelEventArgs();
            OnAnalysisStarting(previewArgs);
            if (previewArgs.Cancel)
            {
                OnAnalysisCompleted(new AnalysisRunCompletedEventArgs(true, false, null));
                return;
            }

            var cancellationToken = ResetCancellationToken();

            // Wait for any in-flight reprocess to finish before clearing results and
            // starting a new MCMC run. Without this gate, a fire-and-forget reprocess
            // (triggered by a prior property change via ReprocessIfEstimated) can be
            // inside its parallel loop when ClearResults() nulls AnalysisResults �
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
                    // Set IsEstimated BEFORE creating results so that when AnalysisResults
                    // PropertyChanged fires inside CreateFrequencyAnalysisResultsAsync, the
                    // App control's BindSummaryStatisticsDataGrid gate (Element.IsEstimated == true)
                    // sees the analysis as estimated and populates summary stats. Without this, the
                    // batch path � which calls inner.RunAsync directly without the UI wrapper's
                    // post-await RaisePropertyChange � leaves the summary grid empty. Mirrors the
                    // B17C pattern (Bulletin17CAnalysis.cs:558).
                    _isEstimated = true;
                    await CreateFrequencyAnalysisResultsAsync(progressReporter, cancellationToken);
                    RaisePropertyChange(nameof(IsEstimated));
                    AnalysisProgress.ReportComplete(progressReporter);
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
                    OnAnalysisCompleted(new AnalysisRunCompletedEventArgs(
                        wasCanceled, IsEstimated && !wasCanceled && error == null, error));
                }
            }
            finally
            {
                _reprocessGate.Release();
            }
        }

        /// <summary>
        /// Computes the coincident frequency curve via per-Y-column inversion of the
        /// response surface and 2-point copula conditional CDF differences, looped over
        /// posterior MCMC realisations to produce uncertainty bands.
        /// </summary>
        /// <param name="progressReporter">Optional progress reporter (0�100).</param>
        /// <param name="cancellationToken">Token used to cancel the per-realisation
        /// parallel loop and the surrounding aggregation. When triggered, the task
        /// throws <see cref="OperationCanceledException"/> before any partial
        /// <c>aepStore</c> is aggregated, so the caller's catch path can mark the
        /// analysis as not estimated without exposing partial results. Mirrors the
        /// pattern in <c>Bulletin17CAnalysis</c>'s bootstrap loops.</param>
        public async Task CreateFrequencyAnalysisResultsAsync(SafeProgressReporter? progressReporter = null, CancellationToken cancellationToken = default)
        {
            AnalysisResults = null;
            ZOutputValues = null;

            if (BivariateAnalysis == null || BivariateAnalysis.BivariateDistribution == null)
            {
                RaisePropertyChange(nameof(AnalysisResults));
                RaisePropertyChange(nameof(ZOutputValues));
                return;
            }

            await Task.Run(() =>
            {
                int M = XValues.Length;
                int N = YValues.Length;
                int K = NumberOfBins;
                double alpha = 1d - BayesianAnalysis.CredibleIntervalWidth;

                // Step 1 � Z output bins (endpoint-inclusive linspace).
                ZOutputValues = BuildZOutputBins(BivariateResponse, K);

                // Step 2 � Y bin edges (midpoints with �8 at ends).
                var yEdges = ComputeYBinEdges(YValues);

                // Cancellation checkpoint: if the user cancelled before the parallel
                // loop kicks off, throw here so the bin/edge setup work isn't followed
                // by a wasted Parallel.For setup.
                cancellationToken.ThrowIfCancellationRequested();

                // Determine realisation count: copula chain is the only required source
                // (marginal chains are optional � fall back to point-estimate marginals if missing).
                var copulaResults = BivariateAnalysis.BayesianAnalysis?.Results;
                int[][]? randomIndexes = copulaResults == null
                    ? null
                    : GetOrCreatePosteriorRandomIndexes(copulaResults);
                int realz = randomIndexes?[0].Length ?? 0;
                int marginalXSourceRow = MarginalXChain == null ? -1 : 1;
                int marginalYSourceRow = MarginalYChain == null
                    ? -1
                    : 1 + (MarginalXChain == null ? 0 : 1);

                AnalysisResults = new UncertaintyAnalysisResults
                {
                    ModeCurve = ComputePointEstimateAEPCurve(yEdges),
                    MeanCurve = new double[K],
                    ConfidenceIntervals = new double[K, 2],
                };

                if (realz <= 0)
                {
                    // Point-estimate-only path. Mean curve == mode curve, CIs are NaN bands.
                    Array.Copy(AnalysisResults.ModeCurve!, AnalysisResults.MeanCurve!, K);
                    for (int k = 0; k < K; k++)
                    {
                        AnalysisResults.ConfidenceIntervals![k, 0] = double.NaN;
                        AnalysisResults.ConfidenceIntervals![k, 1] = double.NaN;
                    }
                    AnalysisProgress.ReportProcessingResults(progressReporter);
                    return;
                }

                // Steps 3�4 � Per-realisation loop.
                // aepStore[k][r] = AEP at output bin k under realisation r.
                var aepStore = new double[K][];
                for (int k = 0; k < K; k++) aepStore[k] = new double[realz];

                var options = AnalysisProgress.CreateParallelOptions(cancellationToken);

                int progressCounter = 0;
                Parallel.For(0, realz, options, r =>
                {
                    // ThrowIfCancellationRequested fires immediately when the token is
                    // signaled. The OperationCanceledException pops as a "first-chance
                    // exception" in the Visual Studio debugger when CLR exceptions are
                    // enabled in Debug ? Windows ? Exception Settings, but the outer
                    // catch in RunAsync (CoincidentFrequencyAnalysis.cs:382) handles it
                    // correctly � the user sees a clean cancel in Release mode and when
                    // running outside the debugger.
                    options.CancellationToken.ThrowIfCancellationRequested();

                    double[]? mxParams = MarginalXChain == null
                        ? null
                        : MarginalXChain.Output[randomIndexes![marginalXSourceRow][r]].Values;
                    double[]? myParams = MarginalYChain == null
                        ? null
                        : MarginalYChain.Output[randomIndexes![marginalYSourceRow][r]].Values;
                    var copulaParameters = copulaResults!.Output[randomIndexes![0][r]].Values;
                    var aep = ComputeAEPCurveForDraw(copulaParameters, mxParams, myParams, yEdges);
                    for (int k = 0; k < K; k++) aepStore[k][r] = aep[k];

                    int done = Interlocked.Increment(ref progressCounter);
                    if (done % Math.Max(1, realz / 20) == 0)
                        progressReporter?.ReportProgress((int)(99.0 * done / realz));
                });

                // Defensive checkpoint between the parallel loop and aggregation: if
                // cancellation fires after the last worker exits but before aggregation
                // starts, abort cleanly rather than aggregating a fully-populated store
                // for results the caller will discard.
                cancellationToken.ThrowIfCancellationRequested();

                // Aggregate.
                for (int k = 0; k < K; k++)
                {
                    var aeps = aepStore[k];
                    AnalysisResults.MeanCurve![k] = Statistics.ParallelMean(aeps);
                    Array.Sort(aeps);
                    AnalysisResults.ConfidenceIntervals![k, 0] = Statistics.Percentile(aeps, alpha / 2d, true);
                    AnalysisResults.ConfidenceIntervals![k, 1] = Statistics.Percentile(aeps, 1d - alpha / 2d, true);
                }
            }, cancellationToken);

            RaisePropertyChange(nameof(AnalysisResults));
            RaisePropertyChange(nameof(ZOutputValues));
        }

        /// <summary>
        /// Updates the point-estimate AEP curve using the current
        /// <see cref="BayesianAnalysis.PointEstimator"/> without recomputing posterior bands.
        /// </summary>
        /// <returns>A task that completes when the point-estimate curve has been refreshed.</returns>
        /// <remarks>
        /// Credible intervals and posterior means are left intact. This mirrors the
        /// lightweight point-estimate update used by other Bayesian analyses.
        /// </remarks>
        public async Task UpdatePointEstimateResultsAsync()
        {
            if (!IsEstimated || AnalysisResults == null || ZOutputValues == null)
                return;

            await Task.Run(() =>
            {
                var yEdges = ComputeYBinEdges(YValues);
                AnalysisResults.ModeCurve = ComputePointEstimateAEPCurve(yEdges);
            });

            RaisePropertyChange(nameof(AnalysisResults));
        }

        /// <summary>
        /// Builds the Z output bin grid: endpoint-inclusive evenly-spaced values from min to
        /// max of the response surface, with K = numBins points.
        /// </summary>
        private static double[] BuildZOutputBins(double[,] response, int numBins)
        {
            double zMin = double.MaxValue;
            double zMax = double.MinValue;
            int rows = response.GetLength(0);
            int cols = response.GetLength(1);
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    if (response[i, j] < zMin) zMin = response[i, j];
                    if (response[i, j] > zMax) zMax = response[i, j];
                }
            }

            var bins = new double[numBins];
            if (numBins == 1 || zMax <= zMin)
            {
                for (int k = 0; k < numBins; k++) bins[k] = zMin;
                return bins;
            }
            double step = (zMax - zMin) / (numBins - 1);
            for (int k = 0; k < numBins; k++) bins[k] = zMin + k * step;
            return bins;
        }

        /// <summary>
        /// Computes Y bin edges as midpoints between consecutive Y values, with
        /// negative / positive infinity at the ends (length N + 1).
        /// </summary>
        private static double[] ComputeYBinEdges(double[] yValues)
        {
            int n = yValues.Length;
            var edges = new double[n + 1];
            edges[0] = double.NegativeInfinity;
            edges[n] = double.PositiveInfinity;
            for (int j = 1; j < n; j++)
                edges[j] = (yValues[j - 1] + yValues[j]) / 2d;
            return edges;
        }

        /// <summary>
        /// Computes F_Z(z) for a single Z output bin by total probability over Y columns:
        /// F_Z(z) = S_j [ C(F_X(x*_j), v_{j+1}) - C(F_X(x*_j), v_j) ], where v_0 = 0,
        /// v_N = 1, and v_j = F_Y(midpoint(y_{j-1}, y_j)) for 1 = j = N - 1. The Y-tail
        /// mass (Y &lt; y_0 and Y &gt; y_{N-1}) is folded into bins j = 0 and j = N - 1
        /// respectively so the integration is collectively exhaustive over Y.
        /// </summary>
        /// <param name="z">The Z output bin value.</param>
        /// <param name="response">The response surface BivariateResponse[i, j].</param>
        /// <param name="xZetas">Pre-computed F?�(F_X(xValues[i])) for i = 0..M - 1. The
        /// column inversion interpolates linearly in (z, ?) space, the standard Normal-Z
        /// probability-paper convention used elsewhere in BestFit.</param>
        /// <param name="vEdges">Pre-computed Y bin edges in copula space (length N + 1).
        /// vEdges[0] = 0 and vEdges[N] = 1; interior values are F_Y at midpoints between
        /// consecutive y values.</param>
        /// <param name="copula">The copula with parameters set for this realisation.</param>
        /// <remarks>
        /// <para>
        /// For each Y column j the algorithm finds x*(z, j) by interpolating linearly along
        /// (response[*, j], xZetas) � i.e., the response value vs the Normal Z-variate of
        /// F_X(x_i). When z falls outside the column's response range it uses linear
        /// extrapolation along the nearest segment. The result is converted back to a
        /// probability via F.
        /// </para>
        /// <para>
        /// The contribution of column j is C(u, v_{j+1}) - C(u, v_j) (a 2-point copula CDF
        /// difference). The boundary identities C(u, 0) = 0 and C(u, 1) = u are applied
        /// analytically for j = 0 and j = N - 1 to avoid passing 0 or 1 to copula.CDF, which
        /// elliptical copulas compute via F?� internally.
        /// </para>
        /// <para>
        /// Mass conservation: at z = +8 every column has u = 1, so the sum collapses to
        /// v_1 + S_{j=1}^{N-2}(v_{j+1} - v_j) + (1 - v_{N-1}) = 1. At z = -8 every column
        /// has u = 0 and F_Z = 0.
        /// </para>
        /// </remarks>
        private static double ComputeFZAtBin(double z, double[,] response, double[] xZetas, double[] vEdges,
            BivariateCopula copula)
        {
            int M = response.GetLength(0);
            int N = response.GetLength(1);
            const double UEpsilon = 1e-12;

            double fz = 0d;

            // Sum over Y columns: P(Z = z) = S_j P(X = x*_j, Y ? bin_j) via the copula.
            for (int j = 0; j < N; j++)
            {
                double u = ClampUnit(FindUStarInColumn(z, j, response, M, xZetas), UEpsilon);

                // Upper-edge term: C(u, vEdges[j+1]). For j = N - 1, vEdges[N] = 1 ? C(u, 1) = u.
                double upperTerm = (j == N - 1) ? u : copula.CDF(u, vEdges[j + 1]);
                // Lower-edge term: C(u, vEdges[j]). For j = 0, vEdges[0] = 0 ? C(u, 0) = 0.
                double lowerTerm = (j == 0) ? 0d : copula.CDF(u, vEdges[j]);
                double contribution = upperTerm - lowerTerm;

                if (contribution > 0d) fz += contribution;
            }

            if (fz < 0d) fz = 0d;
            if (fz > 1d) fz = 1d;
            return fz;
        }

        /// <summary>
        /// Finds u = F_X(x*) where x* is the X-axis solution to response(x*, yValues[colIdx]) = z,
        /// interpolating linearly in (response, ?) space where ? = F?�(F_X(x)) is the Normal
        /// Z-variate. Linear extrapolation along the first or last segment is used when z
        /// falls outside the column's response range. The result is converted back via F.
        /// </summary>
        /// <param name="z">The Z value to invert.</param>
        /// <param name="colIdx">Column index (Y position) into the response surface.</param>
        /// <param name="response">The response surface.</param>
        /// <param name="M">Length of the X axis (rows of <paramref name="response"/>).</param>
        /// <param name="xZetas">Pre-computed Normal Z-variate of F_X at each X grid point �
        /// see <see cref="BuildXZetas"/>. Length must equal <paramref name="M"/>.</param>
        /// <returns>u = F(?*) ? [0, 1].</returns>
        /// <remarks>Interpolating in (z, ?) rather than (z, x) follows the project's
        /// standard Normal-Z probability-paper convention. For Normal marginals the two
        /// schemes coincide because ? is a linear function of x; for non-Normal marginals
        /// (LP3, GEV, Gumbel) the Normal-Z form is more accurate at sparse-grid resolutions.</remarks>
        private static double FindUStarInColumn(double z, int colIdx, double[,] response, int M, double[] xZetas)
        {
            double zFirst = response[0, colIdx];
            double zLast = response[M - 1, colIdx];
            double zetaStar;

            if (z <= zFirst)
            {
                // Linear extrapolation down using slope of first segment.
                double dz = response[1, colIdx] - zFirst;
                zetaStar = (dz > 0d)
                    ? xZetas[0] + (xZetas[1] - xZetas[0]) * (z - zFirst) / dz
                    : xZetas[0];
            }
            else if (z >= zLast)
            {
                // Linear extrapolation up using slope of last segment.
                double dz = zLast - response[M - 2, colIdx];
                zetaStar = (dz > 0d)
                    ? xZetas[M - 1] + (xZetas[M - 1] - xZetas[M - 2]) * (z - zLast) / dz
                    : xZetas[M - 1];
            }
            else
            {
                // Linear interpolation within the column.
                int i = 0;
                for (int k = 0; k < M - 1; k++)
                {
                    if (z >= response[k, colIdx] && z <= response[k + 1, colIdx])
                    {
                        i = k;
                        break;
                    }
                }
                double zLow = response[i, colIdx];
                double zHigh = response[i + 1, colIdx];
                zetaStar = (zHigh > zLow)
                    ? xZetas[i] + (z - zLow) * (xZetas[i + 1] - xZetas[i]) / (zHigh - zLow)
                    : xZetas[i];
            }

            return Normal.StandardCDF(zetaStar);
        }

        /// <summary>
        /// Pre-computes ?_i = F?�(F_X(xValues[i])) for use as the probability-axis ordinate
        /// in <see cref="FindUStarInColumn"/>. Called once per posterior draw in
        /// <see cref="ComputeAEPCurveForDraw"/>; the result is reused across all Z output bins.
        /// </summary>
        /// <param name="xValues">X ordinates (strictly ascending).</param>
        /// <param name="marginalX">Marginal X distribution for this realisation.</param>
        /// <returns>Array of Normal Z-variates of F_X at each X grid point.</returns>
        private static double[] BuildXZetas(double[] xValues, IUnivariateDistribution marginalX)
        {
            const double UEpsilon = 1e-12;
            var zetas = new double[xValues.Length];
            for (int i = 0; i < xValues.Length; i++)
            {
                double u = ClampUnit(marginalX.CDF(xValues[i]), UEpsilon);
                zetas[i] = Normal.StandardZ(u);
            }
            return zetas;
        }

        /// <summary>
        /// Pre-computes the Y bin edges in copula space (length N + 1). vEdges[0] = 0 and
        /// vEdges[N] = 1 fold the Y-tail mass into bins j = 0 and j = N - 1 respectively.
        /// Interior edges are F_Y at the midpoint between consecutive y values
        /// (yEdges[1..N-1] from <see cref="ComputeYBinEdges"/>).
        /// </summary>
        /// <param name="yValues">Y ordinates (strictly ascending).</param>
        /// <param name="yEdges">Y bin edges in real space (length N + 1).</param>
        /// <param name="marginalY">Marginal Y distribution for this realisation.</param>
        /// <returns>Array of Y bin edges in copula space.</returns>
        private static double[] BuildVEdges(double[] yValues, double[] yEdges, IUnivariateDistribution marginalY)
        {
            const double UEpsilon = 1e-12;
            int N = yValues.Length;
            var v = new double[N + 1];
            v[0] = 0d;
            v[N] = 1d;
            for (int j = 1; j < N; j++)
            {
                v[j] = ClampUnit(marginalY.CDF(yEdges[j]), UEpsilon);
            }
            return v;
        }

        /// <summary>
        /// Clamps a value to the open interval (eps, 1 - eps) so it is safe to pass to a
        /// copula CDF that computes Phi^-1 internally (Normal, Student-t).
        /// </summary>
        private static double ClampUnit(double x, double eps)
        {
            if (x < eps) return eps;
            if (x > 1d - eps) return 1d - eps;
            return x;
        }

        /// <summary>
        /// Computes the AEP curve over <see cref="ZOutputValues"/> for a single posterior draw.
        /// Helper shared by <see cref="CreateFrequencyAnalysisResultsAsync"/>'s parallel loop,
        /// <see cref="GetPointEstimateDistribution"/>, and <see cref="GetEmpiricalDistribution"/>.
        /// </summary>
        /// <param name="copulaParameters">Copula parameters; null leaves the cloned upstream copula unchanged.</param>
        /// <param name="marginalXParameters">Marginal X parameters; null leaves the cloned upstream marginal X unchanged.</param>
        /// <param name="marginalYParameters">Marginal Y parameters; null leaves the cloned upstream marginal Y unchanged.</param>
        /// <param name="yEdges">Pre-computed Y bin edges (length N + 1) � see <see cref="ComputeYBinEdges"/>.</param>
        /// <returns>AEP[k] = 1 - F_Z(<see cref="ZOutputValues"/>[k]) for k in [0, K).</returns>
        /// <remarks>Each call clones the upstream copula and marginals so the helper is safe under
        /// the parallel realisation loop in <see cref="CreateFrequencyAnalysisResultsAsync"/>.</remarks>
        private double[] ComputeAEPCurveForDraw(double[]? copulaParameters,
                                                  double[]? marginalXParameters,
                                                  double[]? marginalYParameters,
                                                  double[] yEdges)
        {
            int K = ZOutputValues!.Length;
            var copula = (BivariateCopula)BivariateAnalysis.BivariateDistribution.Copula.Clone();
            if (copulaParameters != null) copula.SetCopulaParameters(copulaParameters);

            var mX = BivariateAnalysis.BivariateDistribution.MarginalX.Distribution!.Clone();
            if (marginalXParameters != null) mX.SetParameters(marginalXParameters);

            var mY = BivariateAnalysis.BivariateDistribution.MarginalY.Distribution!.Clone();
            if (marginalYParameters != null) mY.SetParameters(marginalYParameters);

            // Pre-compute the X probability-axis ordinates (Normal Z-variates of F_X(x_i))
            // and the Y bin edges in copula space ONCE per draw � both are constant across
            // Z output bins and reused inside every ComputeFZAtBin call.
            var xZetas = BuildXZetas(XValues, mX);
            var vEdges = BuildVEdges(YValues, yEdges, mY);

            var aep = new double[K];
            for (int k = 0; k < K; k++)
            {
                double fz = ComputeFZAtBin(ZOutputValues[k], BivariateResponse, xZetas, vEdges, copula);
                aep[k] = 1d - fz;
            }
            return aep;
        }

        /// <summary>
        /// Computes the point-estimate AEP curve over <see cref="ZOutputValues"/> for the
        /// current <see cref="BayesianAnalysis.PointEstimator"/> setting.
        /// </summary>
        /// <param name="yEdges">Pre-computed Y bin edges used to integrate the response surface.</param>
        /// <returns>AEP[k] = 1 - F_Z(<see cref="ZOutputValues"/>[k]) for each output bin.</returns>
        /// <remarks>
        /// If posterior samples are unavailable, the method falls back to the current upstream
        /// copula and marginal parameter values, preserving the deterministic point-estimate-only
        /// path used by unit tests and simple configured analyses.
        /// </remarks>
        private double[] ComputePointEstimateAEPCurve(double[] yEdges)
        {
            double[]? copulaParameters = null;
            double[]? marginalXParameters = null;
            double[]? marginalYParameters = null;

            var copulaResults = BivariateAnalysis?.BayesianAnalysis?.Results;
            if (BayesianAnalysis.PointEstimator == Estimation.BayesianAnalysis.PointEstimateType.PosteriorMean)
            {
                copulaParameters = copulaResults?.PosteriorMean.Values;
                marginalXParameters = MarginalXChain?.PosteriorMean.Values;
                marginalYParameters = MarginalYChain?.PosteriorMean.Values;
            }
            else
            {
                copulaParameters = copulaResults?.MAP.Values;
                marginalXParameters = MarginalXChain?.MAP.Values;
                marginalYParameters = MarginalYChain?.MAP.Values;
            }

            return ComputeAEPCurveForDraw(copulaParameters, marginalXParameters, marginalYParameters, yEdges);
        }

        /// <summary>
        /// Returns an empirical distribution over Z (response value) and AEP (= 1 - F_Z) for the
        /// upstream <see cref="BivariateAnalysis"/>'s point estimate. The choice of point estimate
        /// is driven by <see cref="BayesianAnalysis.PointEstimator"/>:
        /// <see cref="BayesianAnalysis.PointEstimateType.PosteriorMean"/> rebuilds copula + marginals
        /// from each chain's <c>PosteriorMean.Values</c>;
        /// <see cref="BayesianAnalysis.PointEstimateType.PosteriorMode"/> reuses the already-computed
        /// <see cref="UncertaintyAnalysisResults.ModeCurve"/>.
        /// </summary>
        /// <returns>The empirical Z?AEP distribution, or null when upstream is not estimated, chains
        /// are unavailable, results are missing, or the constructor rejects the data.</returns>
        /// <remarks>Mirrors <c>UnivariateAnalysis.GetPointEstimateDistribution</c>, but returns
        /// an <see cref="EmpiricalDistribution"/> rather than a parametric distribution because CFA's
        /// output is a tabulated Z-vs-AEP curve. The probability axis is in descending sort order
        /// (AEP), passed through <see cref="EmpiricalDistribution(IList{double}, IList{double}, SortOrder, SortOrder)"/>.
        /// Consumers that want moments call <c>.CentralMoments(1000)</c> on the returned object.</remarks>
        public EmpiricalDistribution? GetPointEstimateDistribution()
        {
            if (BivariateAnalysis?.BayesianAnalysis == null
                || BivariateAnalysis.BayesianAnalysis.IsEstimated == false
                || ZOutputValues == null
                || AnalysisResults == null)
                return null;

            try
            {
                var yEdges = ComputeYBinEdges(YValues);
                var aepCurve = ComputePointEstimateAEPCurve(yEdges);
                return new EmpiricalDistribution(ZOutputValues, aepCurve,
                    SortOrder.Ascending, SortOrder.Descending);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CoincidentFrequencyAnalysis.GetPointEstimateDistribution: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Returns an empirical distribution over Z (response value) and AEP (= 1 - F_Z) for posterior
        /// realization <paramref name="index"/>. The realization count equals the shortest
        /// supplied retained chain. Missing marginal chains use the point-estimate fallback
        /// inside <see cref="ComputeAEPCurveForDraw"/> and do not constrain that count.
        /// </summary>
        /// <param name="index">Posterior realisation index in [0, realz).</param>
        /// <returns>The empirical Z?AEP distribution for the requested draw, or null when upstream is
        /// not estimated, <see cref="ZOutputValues"/> is null, the index is out of range, or the
        /// constructor rejects the data.</returns>
        /// <remarks>
        /// Mirrors <see cref="UnivariateAnalysis.GetDistribution(int)"/>. The probability
        /// axis is in descending sort order (AEP). The accessor reuses the cached randomized
        /// copula/X/Y mapping used to construct <see cref="AnalysisResults"/>.
        /// </remarks>
        public EmpiricalDistribution? GetEmpiricalDistribution(int index)
        {
            if (BivariateAnalysis?.BayesianAnalysis == null
                || BivariateAnalysis.BayesianAnalysis.IsEstimated == false
                || BivariateAnalysis.BayesianAnalysis.Results == null
                || ZOutputValues == null)
                return null;

            var copulaResults = BivariateAnalysis.BayesianAnalysis.Results;
            int[][]? randomIndexes = GetOrCreatePosteriorRandomIndexes(copulaResults);
            int realz = randomIndexes?[0].Length ?? 0;

            if (index < 0 || index >= realz) return null;

            int marginalXSourceRow = MarginalXChain == null ? -1 : 1;
            int marginalYSourceRow = MarginalYChain == null
                ? -1
                : 1 + (MarginalXChain == null ? 0 : 1);
            var copulaParams = copulaResults.Output![randomIndexes![0][index]].Values;
            var mxParams = MarginalXChain == null
                ? null
                : MarginalXChain.Output[randomIndexes[marginalXSourceRow][index]].Values;
            var myParams = MarginalYChain == null
                ? null
                : MarginalYChain.Output[randomIndexes[marginalYSourceRow][index]].Values;

            var yEdges = ComputeYBinEdges(YValues);
            var aepCurve = ComputeAEPCurveForDraw(copulaParams, mxParams, myParams, yEdges);

            try
            {
                return new EmpiricalDistribution(ZOutputValues, aepCurve,
                    SortOrder.Ascending, SortOrder.Descending);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CoincidentFrequencyAnalysis.GetEmpiricalDistribution: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Sets <see cref="AnalysisResults"/> directly. Used by the UI wrapper's <c>Open()</c>
        /// to restore deserialized results without re-running the analysis.
        /// </summary>
        /// <param name="results">The deserialized results, or null to clear.</param>
        public void SetAnalysisResults(UncertaintyAnalysisResults? results)
        {
            AnalysisResults = results;
            RaisePropertyChange(nameof(AnalysisResults));
        }

        /// <summary>
        /// Restores previously saved analysis results from deserialization. Sets
        /// <see cref="AnalysisBase.IsEstimated"/> to <c>true</c> when <paramref name="results"/>
        /// is non-null so consumers see the persisted curves as a successfully estimated state.
        /// </summary>
        /// <remarks>
        /// Mirrors <c>RestoreAnalysisResults</c>. The plain
        /// <see cref="SetAnalysisResults"/> setter is preserved for in-flight UI updates that
        /// must not flip the IsEstimated flag.
        /// </remarks>
        /// <param name="results">The deserialized analysis results.</param>
        public void RestoreAnalysisResults(UncertaintyAnalysisResults? results)
        {
            AnalysisResults = results;
            IsEstimated = results != null;
            RaisePropertyChange(nameof(AnalysisResults));
            RaisePropertyChange(nameof(IsEstimated));
        }

        /// <summary>
        /// Sets <see cref="ZOutputValues"/> directly. Used by the UI wrapper's <c>Open()</c>
        /// to restore the deserialized Z bin grid without re-running the analysis.
        /// </summary>
        /// <param name="zValues">The deserialized Z bin values, or null to clear.</param>
        public void SetZOutputValues(double[]? zValues)
        {
            ZOutputValues = zValues;
            RaisePropertyChange(nameof(ZOutputValues));
        }

        /// <inheritdoc/>
        public override (bool IsValid, List<string> ValidationMessages) Validate()
        {
            bool isValid = true;
            var messages = new List<string>();

            // BivariateAnalysis presence.
            if (BivariateAnalysis == null)
            {
                isValid = false;
                messages.Add("Bivariate analysis is required.");
                return (false, messages);
            }
            if (BivariateAnalysis.BivariateDistribution == null)
            {
                isValid = false;
                messages.Add("Bivariate analysis must contain a bivariate distribution.");
                return (false, messages);
            }
            if (BivariateAnalysis.BivariateDistribution.MarginalX?.Distribution is null ||
                BivariateAnalysis.BivariateDistribution.MarginalY?.Distribution is null ||
                BivariateAnalysis.BivariateDistribution.Copula is null)
            {
                isValid = false;
                messages.Add("Bivariate analysis must have valid marginals and copula.");
                return (false, messages);
            }
            if (!BivariateAnalysis.IsEstimated)
            {
                isValid = false;
                messages.Add("Bivariate analysis has not been estimated yet. Run the bivariate analysis (or include it in the batch) before running the coincident frequency analysis.");
                return (false, messages);
            }

            // X / Y ordinate validation.
            if (XValues == null || XValues.Length < 2)
            {
                isValid = false;
                messages.Add("At least 2 X (primary) values are required.");
            }
            else
            {
                for (int i = 1; i < XValues.Length; i++)
                {
                    if (!(XValues[i] > XValues[i - 1]))
                    {
                        isValid = false;
                        messages.Add("X (primary) values must be strictly ascending.");
                        break;
                    }
                }
            }

            if (YValues == null || YValues.Length < 2)
            {
                isValid = false;
                messages.Add("At least 2 Y (secondary) values are required.");
            }
            else
            {
                for (int j = 1; j < YValues.Length; j++)
                {
                    if (!(YValues[j] > YValues[j - 1]))
                    {
                        isValid = false;
                        messages.Add("Y (secondary) values must be strictly ascending.");
                        break;
                    }
                }
            }

            // BivariateResponse validation.
            if (BivariateResponse == null)
            {
                isValid = false;
                messages.Add("Bivariate response surface is required.");
            }
            else if (XValues != null && YValues != null)
            {
                int rows = BivariateResponse.GetLength(0);
                int cols = BivariateResponse.GetLength(1);
                if (rows != XValues.Length)
                {
                    isValid = false;
                    messages.Add($"Bivariate response must have {XValues.Length} rows (one per X value).");
                }
                if (cols != YValues.Length)
                {
                    isValid = false;
                    messages.Add($"Bivariate response must have {YValues.Length} columns (one per Y value).");
                }

                // No NaN / infinity.
                bool sawBadValue = false;
                for (int i = 0; i < rows && !sawBadValue; i++)
                {
                    for (int j = 0; j < cols && !sawBadValue; j++)
                    {
                        if (double.IsNaN(BivariateResponse[i, j]) || double.IsInfinity(BivariateResponse[i, j]))
                            sawBadValue = true;
                    }
                }
                if (sawBadValue)
                {
                    isValid = false;
                    messages.Add("Bivariate response contains NaN or infinity values.");
                }

                // Per-column monotonicity (along X) � required for X inversion.
                if (rows == XValues.Length && cols == YValues.Length)
                {
                    bool columnMonotonic = true;
                    for (int j = 0; j < cols && columnMonotonic; j++)
                    {
                        for (int i = 1; i < rows; i++)
                        {
                            if (!(BivariateResponse[i, j] > BivariateResponse[i - 1, j]))
                            {
                                columnMonotonic = false;
                                break;
                            }
                        }
                    }
                    if (!columnMonotonic)
                    {
                        isValid = false;
                        messages.Add("Bivariate response must be strictly increasing along X (each column).");
                    }

                    // Per-row monotonicity (along Y) � required for physical sense.
                    bool rowMonotonic = true;
                    for (int i = 0; i < rows && rowMonotonic; i++)
                    {
                        for (int j = 1; j < cols; j++)
                        {
                            if (!(BivariateResponse[i, j] > BivariateResponse[i, j - 1]))
                            {
                                rowMonotonic = false;
                                break;
                            }
                        }
                    }
                    if (!rowMonotonic)
                    {
                        isValid = false;
                        messages.Add("Bivariate response must be strictly increasing along Y (each row).");
                    }
                }
            }

            // NumberOfBins range (errors).
            if (NumberOfBins < 5)
            {
                isValid = false;
                messages.Add("Error: NumberOfBins must be at least 5.");
            }
            else if (NumberOfBins > 1000)
            {
                isValid = false;
                messages.Add("Error: NumberOfBins must be at most 1000.");
            }
            else if (NumberOfBins > 100)
            {
                // Non-blocking warning � large bin counts are valid but slow.
                messages.Add("Warning: NumberOfBins > 100 may result in slow run times.");
            }

            if (BayesianAnalysis.PRNGSeed < 0)
            {
                isValid = false;
                messages.Add("The posterior-resampling PRNG seed must be nonnegative.");
            }

            return (isValid, messages);
        }

        /// <summary>
        /// Serializes the analysis state to an <see cref="XElement"/>.
        /// </summary>
        public XElement ToXElement()
        {
            var root = new XElement(nameof(CoincidentFrequencyAnalysis),
                new XAttribute("IsEstimated", IsEstimated),
                new XAttribute(nameof(NumberOfBins), NumberOfBins));

            // BayesianAnalysis settings (CredibleIntervalWidth, OutputLength, PointEstimator)
            root.Add(BayesianAnalysis.ToXElement());

            if (XValues != null && XValues.Length > 0)
                root.Add(new XElement(nameof(XValues), JoinDoubles(XValues)));

            if (YValues != null && YValues.Length > 0)
                root.Add(new XElement(nameof(YValues), JoinDoubles(YValues)));

            if (BivariateResponse != null && BivariateResponse.Length > 0)
            {
                int rows = BivariateResponse.GetLength(0);
                int cols = BivariateResponse.GetLength(1);
                var values = new List<double>(rows * cols);
                for (int i = 0; i < rows; i++)
                    for (int j = 0; j < cols; j++)
                        values.Add(BivariateResponse[i, j]);
                root.Add(new XElement(nameof(BivariateResponse), JoinDoubles(values)));
            }

            return root;
        }

        /// <summary>
        /// Joins doubles.
        /// </summary>
        /// <param name="values">The input values.</param>
        /// <returns>The joined text.</returns>
        /// <remarks>
        /// This member supports the owning analysis or model implementation.
        /// </remarks>
        private static string JoinDoubles(IEnumerable<double> values)
        {
            return string.Join(",", values.Select(d => d.ToString("G17", CultureInfo.InvariantCulture)));
        }

        /// <summary>
        /// Parses double Array.
        /// </summary>
        /// <param name="csv">The csv value.</param>
        /// <returns>The parsed values.</returns>
        /// <remarks>
        /// This member supports the owning analysis or model implementation.
        /// </remarks>
        private static double[] ParseDoubleArray(string? csv)
        {
            if (string.IsNullOrWhiteSpace(csv)) return Array.Empty<double>();
            return csv.Split(',')
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => double.Parse(s.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture))
                .ToArray();
        }

        #endregion
    }
}
