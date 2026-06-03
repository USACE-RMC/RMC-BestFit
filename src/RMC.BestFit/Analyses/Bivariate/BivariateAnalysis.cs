using Numerics;
using Numerics.Data;
using Numerics.Data.Statistics;
using Numerics.Distributions;
using Numerics.Distributions.Copulas;
using Numerics.Sampling.MCMC;
using Numerics.Utilities;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using System.ComponentModel;
using System.Diagnostics;
using System.Xml.Linq;

namespace RMC.BestFit.Analyses
{
    /// <summary>
    /// Performs Bayesian MCMC estimation for a bivariate distribution (copula).
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// </remarks>
    public class BivariateAnalysis : AnalysisBase, IBayesianAnalysis
    {
        #region Construction

        /// <summary>
        /// Initializes a new instance of the <see cref="BivariateAnalysis"/> class.
        /// </summary>
        /// <param name="bivariateDistribution">The <see cref="BivariateDistribution"/> to be estimated.</param>
        public BivariateAnalysis(BivariateDistribution bivariateDistribution)
        {
            BivariateDistribution = bivariateDistribution ?? throw new ArgumentNullException(nameof(bivariateDistribution));
            BayesianAnalysis = new BayesianAnalysis(bivariateDistribution);
            XYOrdinates = new UncertainOrderedPairedData(new List<UncertainOrdinate>() { new UncertainOrdinate(0, new Deterministic(0)) }, false, SortOrder.Ascending, false, SortOrder.Ascending, UnivariateDistributionType.Deterministic);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BivariateAnalysis"/> class from an <see cref="XElement"/>.
        /// </summary>
        /// <param name="bivariateDistribution">The <see cref="BivariateDistribution"/> associated with this analysis.</param>
        /// <param name="xElement">The XML element containing the serialized state.</param>
        /// <param name="mcmcResults">Optional MCMC results to restore from a previous estimation.</param>
        /// <param name="analysisResults">Optional analysis results to restore from a previous estimation.</param>
        public BivariateAnalysis(BivariateDistribution bivariateDistribution, XElement xElement,
                                 MCMCResults? mcmcResults = null,
                                 UncertaintyAnalysisResults? analysisResults = null)
        {
            if (xElement == null) throw new ArgumentNullException(nameof(xElement));
            BivariateDistribution = bivariateDistribution ?? throw new ArgumentNullException(nameof(bivariateDistribution));

            // XY Ordinates
            var xyElement = xElement.Element(nameof(XYOrdinates));
            if (xyElement != null)
            {
                XYOrdinates = new UncertainOrderedPairedData(xyElement);
            }
            else
            {
                XYOrdinates = new UncertainOrderedPairedData(new List<UncertainOrdinate>() { new UncertainOrdinate(0, new Deterministic(0)) }, false, SortOrder.Ascending, false, SortOrder.Ascending, UnivariateDistributionType.Deterministic);
            }

            // Bayesian analysis
            var bayesElement = xElement.Element("BayesianAnalysis");
            if (bayesElement != null)
            {
                BayesianAnalysis = new BayesianAnalysis(bivariateDistribution, bayesElement);
            }
            else
            {
                BayesianAnalysis = new BayesianAnalysis(bivariateDistribution);
            }

            // Check if estimated
            var isEstimatedAttr = xElement.Attribute("IsEstimated");
            if (isEstimatedAttr != null && bool.TryParse(isEstimatedAttr.Value, out bool isEst))
            {
                _isEstimated = isEst;
            }

            // Restore MCMC results if provided
            if (mcmcResults != null)
                _bayesianAnalysis.SetCustomMCMCResults(mcmcResults, skipInformationCriteria: true);

            // Restore analysis results
            AnalysisResults = analysisResults;
        }

        #endregion

        #region Members

        private BivariateDistribution _bivariateDistribution = null!;
        private BayesianAnalysis _bayesianAnalysis = null!;
        private UncertainOrderedPairedData _xyOrdinates = null!;

        /// <summary>
        /// Gets or sets the bivariate distribution model.
        /// </summary>
        public BivariateDistribution BivariateDistribution
        {
            get { return _bivariateDistribution; }
            private set
            {
                if (_bivariateDistribution != null)
                    _bivariateDistribution.PropertyChanged -= Model_PropertyChanged;

                _bivariateDistribution = value;

                if (_bivariateDistribution != null)
                {
                    if (_bayesianAnalysis != null)
                        _bayesianAnalysis.Model = _bivariateDistribution;
                    _bivariateDistribution.PropertyChanged += Model_PropertyChanged;
                }

                RaisePropertyChange(nameof(BivariateDistribution));
            }
        }

        /// <summary>
        /// Gets the Bayesian MCMC analysis object.
        /// </summary>
        public BayesianAnalysis BayesianAnalysis
        {
            get { return _bayesianAnalysis; }
            private set
            {
                if (_bayesianAnalysis != null)
                    _bayesianAnalysis.PropertyChanged -= BayesianAnalysis_PropertyChanged;

                _bayesianAnalysis = value;

                if (_bayesianAnalysis != null)
                    _bayesianAnalysis.PropertyChanged += BayesianAnalysis_PropertyChanged;

                RaisePropertyChange(nameof(BayesianAnalysis));
            }
        }

        /// <summary>
        /// Gets or sets the X-Y ordinates for computing joint exceedance probabilities.
        /// </summary>
        /// <remarks>
        /// Changing the ordinate grid only affects the post-processing of an already-fit
        /// MCMC chain. Reprocesses derived results in the background if the analysis is
        /// estimated and the new ordinates are valid; clears <see cref="AnalysisResults"/>
        /// only otherwise.
        /// </remarks>
        public UncertainOrderedPairedData XYOrdinates
        {
            get { return _xyOrdinates; }
            set
            {
                _xyOrdinates = value;
                RaisePropertyChange(nameof(XYOrdinates));
                ReprocessOrClearXYOrdinates();
            }
        }

        /// <summary>
        /// Gets the uncertainty analysis results.
        /// </summary>
        public UncertaintyAnalysisResults? AnalysisResults { get; private set; }

        #endregion

        #region Methods

        /// <summary>
        /// Handles property changes on the <see cref="BivariateDistribution"/> model.
        /// Only structurally destructive changes (parameters, copula choice, marginals,
        /// estimation method) clear results. All other notifications are propagated for
        /// UI binding without invalidating the fit.
        /// </summary>
        private void Model_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(BivariateDistribution.Parameters) ||
                e.PropertyName == nameof(BivariateDistribution.SetDefaultParameters))
            {
                if (BayesianAnalysis.UseSimulationDefaults)
                    BayesianAnalysis.SetDefaultSimulationOptions();
                if (BayesianAnalysis.UseAdvancedSimulationDefaults)
                    BayesianAnalysis.SetDefaultAdvancedSimulationOptions();
                ClearResults();
            }
            else if (e.PropertyName == nameof(BivariateDistribution.Copula) ||
                     e.PropertyName == nameof(BivariateDistribution.CopulaType) ||
                     e.PropertyName == nameof(BivariateDistribution.MarginalX) ||
                     e.PropertyName == nameof(BivariateDistribution.MarginalY) ||
                     e.PropertyName == nameof(BivariateDistribution.CopulaEstimationMethod))
            {
                ClearResults();
            }

            RaisePropertyChange(e.PropertyName);
        }

        /// <summary>
        /// Handles property changes on the <see cref="BayesianAnalysis"/> object.
        /// Loss of estimated state clears results; PointEstimator changes update the
        /// point-estimate output without re-running the chain; CredibleIntervalWidth
        /// changes reprocess derived joint-frequency results without re-running the chain.
        /// All other notifications propagate for UI binding.
        /// </summary>
        private void BayesianAnalysis_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(BayesianAnalysis.IsEstimated))
            {
                if (BayesianAnalysis.IsEstimated == false)
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

        /// <summary>
        /// Clears all analysis results.
        /// </summary>
        public void ClearResults()
        {
            BayesianAnalysis?.ClearResults();
            AnalysisResults = null;
            RaisePropertyChange(nameof(AnalysisResults));
            IsEstimated = false;
        }

        /// <summary>
        /// Clears <see cref="AnalysisResults"/> only â€” the joint exceedance output whose
        /// evaluation grid is <see cref="XYOrdinates"/>.
        /// </summary>
        /// <remarks>
        /// Leaves the Bayesian MCMC output (<see cref="BayesianAnalysis"/>.Results) and
        /// <see cref="IsEstimated"/> intact. Called when ordinates become invalid â€” the
        /// fit survives and reprocesses on the next valid ordinate change.
        /// </remarks>
        public void ClearFrequencyAnalysisResults()
        {
            AnalysisResults = null;
            RaisePropertyChange(nameof(AnalysisResults));
        }

        /// <summary>
        /// Reprocess the joint frequency output in the background after an
        /// <see cref="XYOrdinates"/> change, or null out <see cref="AnalysisResults"/>
        /// if the new ordinate grid is invalid. Preserves the underlying MCMC fit in
        /// either case.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Validity: <see cref="XYOrdinates"/> non-null with at least one element. Reprocess
        /// is fire-and-forget on the default task scheduler â€” exceptions are logged via
        /// <see cref="Debug"/> and do not propagate to the setter.
        /// </para>
        /// <para>
        /// Public so the UI-layer wrapper class can invoke this from its
        /// <c>XYOrdinates_CollectionChanged</c> handler when individual rows are edited
        /// (CollectionChanged on the existing instance does not flow through the property
        /// setter on this class).
        /// </para>
        /// </remarks>
        public void ReprocessOrClearXYOrdinates()
        {
            if (!IsEstimated) return;

            // Field is non-nullable (`null!`); only the count guard is meaningful.
            bool valid = _xyOrdinates is { Count: > 0 };

            if (valid)
                ReprocessIfEstimated(CreateFrequencyAnalysisResultsAsync);
            else
            {
                ClearFrequencyAnalysisResults();
            }
        }

        /// <inheritdoc/>
        public override async Task RunAsync(SafeProgressReporter? progressReporter = null)
        {
            if (Validate().IsValid == false)
                throw new InvalidOperationException("Analysis is not valid.");

            var previewArgs = new CancelEventArgs();
            OnAnalysisStarting(previewArgs);
            if (previewArgs.Cancel)
            {
                OnAnalysisCompleted(new AnalysisRunCompletedEventArgs(true, false, null));
                return;
            }

            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();

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
                    BivariateDistribution.SetSampleData();
                    await BayesianAnalysis.RunAsync(progressReporter, false);

                    if (BayesianAnalysis.IsEstimated == true)
                    {
                        progressReporter?.ReportProgress(100);
                        await CreateFrequencyAnalysisResultsAsync();
                    }

                    // Conditional set â€” BayesianAnalysis.IsEstimated is false on soft-failure paths
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
                    OnAnalysisCompleted(new AnalysisRunCompletedEventArgs(wasCanceled, IsEstimated && !wasCanceled && error == null, error));
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
        /// Updates the point estimate results.
        /// </summary>
        public async Task UpdatePointEstimateResultsAsync()
        {
            if (BayesianAnalysis == null || BayesianAnalysis.IsEstimated == false ||
                BayesianAnalysis.Results == null || AnalysisResults == null)
                return;

            await Task.Run(() =>
            {
                // Set the point estimator
                if (BayesianAnalysis.PointEstimator == BayesianAnalysis.PointEstimateType.PosteriorMean)
                    BivariateDistribution.SetParameterValues(BayesianAnalysis.Results.PosteriorMean.Values);
                else
                    BivariateDistribution.SetParameterValues(BayesianAnalysis.Results.MAP.Values);

                // Validate required components are not null
                var marginalX = BivariateDistribution.MarginalX;
                var marginalY = BivariateDistribution.MarginalY;
                var marginalXDist = marginalX?.Distribution;
                var marginalYDist = marginalY?.Distribution;
                var copula = BivariateDistribution.Copula;
                if (marginalX is null || marginalY is null ||
                    marginalXDist is null || marginalYDist is null || copula is null)
                    return;

                // Update mode curve
                int n = XYOrdinates.Count;
                AnalysisResults.ModeCurve = new double[n];
                for (int i = 0; i < n; i++)
                {
                    var u = marginalXDist.CDF(XYOrdinates[i].X);
                    var v = marginalYDist.CDF(XYOrdinates[i].Y!.Mean);
                    AnalysisResults.ModeCurve[i] = copula.ANDJointExceedanceProbability(u, v);
                }

                // Compute RMSE
                var dataX = marginalX.DataFrame.ExactSeries.Where(y => !((ExactData)y).IsLowOutlier).ToList();
                var dataY = marginalY.DataFrame.ExactSeries.Where(y => !((ExactData)y).IsLowOutlier).ToList();
                var Fx = new List<double>();
                var Fy = new List<double>();
                // Two-pointer linear merge â€” both series are sorted by Index, so we
                // pair matching observations in O(n + m) instead of the previous O(n Ã— m).
                {
                    int ii = 0, jj = 0;
                    while (ii < dataX.Count && jj < dataY.Count)
                    {
                        int idxX = dataX[ii].Index;
                        int idxY = dataY[jj].Index;
                        if (idxX == idxY)
                        {
                            Fx.Add(dataX[ii].PlottingPositionComplement);
                            Fy.Add(dataY[jj].PlottingPositionComplement);
                            ii++; jj++;
                        }
                        else if (idxX < idxY) ii++;
                        else jj++;
                    }
                }

                n = Fx.Count;
                if (n < 2)
                {
                    // RMSE requires at least 2 matched observations. With 0 or 1 the
                    // (n âˆ’ 1) divisor would produce Â±Inf or NaN; return NaN explicitly so
                    // the UI can render a "â€”" rather than a misleading number.
                    AnalysisResults.RMSE = double.NaN;
                }
                else
                {
                    double sse = 0;
                    for (int i = 0; i < n; i++)
                    {
                        double cdf = BivariateDistribution.Copula.CDF(Fx[i], Fy[i]);
                        double ecdf = 0;
                        for (int j = 0; j < n; j++)
                        {
                            if (Fx[j] <= Fx[i] && Fy[j] <= Fy[i])
                                ecdf++;
                        }
                        ecdf /= n;
                        sse += Tools.Sqr(cdf - ecdf);
                    }

                    AnalysisResults.RMSE = Math.Sqrt(sse / (n - 1));
                }
                // AIC and BIC are computed at the MAP estimate using the full
                // log-likelihood (data + prior). With uniform priors this matches
                // the conventional MLE-based AIC/BIC; with informative priors the
                // metric reflects the prior contribution as well â€” intentional in
                // a Bayesian-first framework where model comparison includes priors.
                double mapLogLH = BivariateDistribution.LogLikelihood(BayesianAnalysis.Results.MAP.Values);
                AnalysisResults.AIC = GoodnessOfFit.AIC(BivariateDistribution.Copula.NumberOfCopulaParameters, mapLogLH);
                AnalysisResults.BIC = GoodnessOfFit.BIC(n, BivariateDistribution.Copula.NumberOfCopulaParameters, mapLogLH);
                AnalysisResults.DIC = BayesianAnalysis.DIC;
            });

            RaisePropertyChange(nameof(AnalysisResults));
        }

        /// <summary>
        /// Creates the frequency analysis results.
        /// </summary>
        public async Task CreateFrequencyAnalysisResultsAsync()
        {
            AnalysisResults = null;
            if (BayesianAnalysis == null || BayesianAnalysis.IsEstimated == false || BayesianAnalysis.Results == null)
            {
                RaisePropertyChange(nameof(AnalysisResults));
                return;
            }

            await Task.Run(() =>
            {
                BivariateDistribution.SetParameterValues(BayesianAnalysis.Results.MAP.Values);

                // Validate required components are not null
                var marginalXDist = BivariateDistribution.MarginalX?.Distribution;
                var marginalYDist = BivariateDistribution.MarginalY?.Distribution;
                var copulaTemplate = BivariateDistribution.Copula;
                if (marginalXDist is null || marginalYDist is null || copulaTemplate is null)
                {
                    return;
                }

                int n = XYOrdinates.Count;
                AnalysisResults = new UncertaintyAnalysisResults();
                AnalysisResults.ModeCurve = new double[n];
                AnalysisResults.MeanCurve = new double[n];
                AnalysisResults.ConfidenceIntervals = new double[n, 2];

                int realz = BayesianAnalysis.OutputLength;
                double alpha = 1 - BayesianAnalysis.CredibleIntervalWidth;

                for (int i = 0; i < n; i++)
                {
                    var u = marginalXDist.CDF(XYOrdinates[i].X);
                    var v = marginalYDist.CDF(XYOrdinates[i].Y!.Mean);
                    AnalysisResults.ModeCurve[i] = copulaTemplate.ANDJointExceedanceProbability(u, v);

                    var p = new double[realz];
                    Parallel.For(0, realz, idx =>
                    {
                        var copula = copulaTemplate.Clone();
                        copula.SetCopulaParameters(BayesianAnalysis.Results.Output[idx].Values);
                        copula.MarginalDistributionX = marginalXDist.Clone();
                        copula.MarginalDistributionY = marginalYDist.Clone();
                        var ui = copula.MarginalDistributionX.CDF(XYOrdinates[i].X);
                        var vi = copula.MarginalDistributionY.CDF(XYOrdinates[i].Y!.Mean);
                        p[idx] = copula.ANDJointExceedanceProbability(ui, vi);
                    });

                    AnalysisResults.MeanCurve[i] = Statistics.ParallelMean(p);
                    Array.Sort(p);
                    AnalysisResults.ConfidenceIntervals[i, 0] = Statistics.Percentile(p, alpha / 2d, true);
                    AnalysisResults.ConfidenceIntervals[i, 1] = Statistics.Percentile(p, 1d - alpha / 2d, true);
                }
            });

            await UpdatePointEstimateResultsAsync();
        }

        /// <inheritdoc/>
        public override (bool IsValid, List<string> ValidationMessages) Validate()
        {
            bool isValid = true;
            var messageList = new List<string>();

            var distValid = BivariateDistribution.Validate();
            if (!distValid.IsValid)
            {
                isValid = false;
                messageList.AddRange(distValid.ValidationMessages);
            }

            var bayesValid = BayesianAnalysis.Validate();
            if (!bayesValid.IsValid)
            {
                isValid = false;
                messageList.AddRange(bayesValid.ValidationMessages);
            }

            return (isValid, messageList);
        }

        /// <summary>
        /// Serializes the analysis to an XML element.
        /// </summary>
        public XElement ToXElement()
        {
            var root = new XElement("BivariateAnalysis",
                new XAttribute("IsEstimated", IsEstimated));

            root.Add(XYOrdinates.SaveToXElement());
            root.Add(BayesianAnalysis.ToXElement());

            return root;
        }

        #endregion
    }
}
