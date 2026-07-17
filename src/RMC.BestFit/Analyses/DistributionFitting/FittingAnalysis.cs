using Numerics;
using Numerics.Data;
using Numerics.Data.Statistics;
using Numerics.Distributions;
using Numerics.Mathematics.Optimization;
using Numerics.Utilities;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Xml.Linq;

namespace RMC.BestFit.Analyses
{
    /// <summary>
    /// Performs maximum likelihood distribution fitting for a set of candidate
    /// univariate distributions given an input <see cref="DataFrame"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    /// This analysis fits each distribution in <see cref="DistributionList"/> to the
    /// supplied <see cref="DataFrame"/> using maximum likelihood estimation. For each
    /// candidate distribution, it computes:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Akaike Information Criterion (AIC).</description></item>
    /// <item><description>Bayesian Information Criterion (BIC) based on the effective record length.</description></item>
    /// <item><description>Root mean squared error (RMSE) between plotting positions and model CDF values.</description></item>
    /// </list>
    /// <para>
    /// The fitting is performed in parallel across candidate distributions. The class
    /// implements <see cref="IAnalysis"/> and <see cref="IProbabilityOrdinates"/>
    /// so it can be used in analysis workflows and share a common representation
    /// of probability ordinates.
    /// </para>
    /// </remarks>
    public class FittingAnalysis : AnalysisBase, IProbabilityOrdinates
    {
        #region Construction

        /// <summary>
        /// Initializes a new instance of the <c>FittingAnalysis</c> class.
        /// </summary>
        public FittingAnalysis() { }

        /// <summary>
        /// Initializes a new instance of the <c>FittingAnalysis</c> class
        /// for the specified input data frame.
        /// </summary>
        /// <param name="dataFrame">
        /// The input <see cref="DataFrame"/> containing exact, interval, uncertain,
        /// and threshold data to be used for distribution fitting.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="dataFrame"/> is <c>null</c>.
        /// </exception>
        public FittingAnalysis(DataFrame dataFrame)
        {
            DataFrame = dataFrame ?? throw new ArgumentNullException(nameof(dataFrame));
            ProbabilityOrdinates = new ProbabilityOrdinates();
            ClearResults();
        }

        /// <summary>
        /// Initializes a new instance of the <c>FittingAnalysis</c> class
        /// by deserializing from an <see cref="XElement"/>.
        /// </summary>
        /// <param name="dataFrame">
        /// The input <see cref="DataFrame"/> associated with this analysis. The
        /// serialized XML stores analysis configuration and results, but not the
        /// raw input data.
        /// </param>
        /// <param name="xElement">
        /// The XML element from which to restore the analysis configuration and
        /// fitted results.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="dataFrame"/> or <paramref name="xElement"/> is <c>null</c>.
        /// </exception>
        public FittingAnalysis(DataFrame dataFrame, XElement xElement)
        {
            DataFrame = dataFrame ?? throw new ArgumentNullException(nameof(dataFrame));
            if (xElement == null) throw new ArgumentNullException(nameof(xElement));

            // Initialize probability ordinates
            ProbabilityOrdinates = new ProbabilityOrdinates();

            // Probability ordinates
            var probElement = xElement.Element("ProbabilityOrdinates");
            if (probElement != null)
            {
                var probText = (string)probElement;
                ProbabilityOrdinates.FromDelimitedString(probText, ProbabilityOrdinates.DefaultDelimiter);
            }

            // Read IsEstimated attribute (serialized by ToXElement)
            var isEstimatedAttr = xElement.Attribute("IsEstimated");
            bool wasEstimated = isEstimatedAttr != null && bool.TryParse(isEstimatedAttr.Value, out var parsed) && parsed;

            // Initialize fitted distributions to defaults
            ClearResults();

            // Fitted distributions (optional) - only restore if analysis was actually estimated
            var fittedRoot = xElement.Element("FittedDistributions");
            if (fittedRoot != null && wasEstimated)
            {
                var list = new List<FittedDistribution>();
                foreach (var fdElement in fittedRoot.Elements("FittedDistribution"))
                {
                    // Assumes FittedDistribution has a constructor taking XElement
                    list.Add(new FittedDistribution(fdElement));
                }

                if (list.Count == DistributionList.Count)
                {
                    _fittedDistributions = list;
                    _isEstimated = true;
                }
            }
        }

        #endregion

        #region Members


        private DataFrame _dataFrame = null!;
        private ProbabilityOrdinates _probabilityOrdinates = new ProbabilityOrdinates();
        private List<FittedDistribution> _fittedDistributions = new List<FittedDistribution>();

        /// <summary>
        /// Gets or sets the input data frame used for distribution fitting.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When the data frame changes, the analysis subscribes to its
        /// <see cref="DataFrame.PropertyChanged"/> event and calls
        /// <see cref="ClearResults"/> if any property other than plotting
        /// positions changes.
        /// </para>
        /// </remarks>
        public DataFrame DataFrame
        {
            get { return _dataFrame; }
            set
            {
                if (_dataFrame != null)
                    _dataFrame.PropertyChanged -= DataFrame_PropertyChanged;

                _dataFrame = value;

                if (_dataFrame != null)
                {
                    _dataFrame.PropertyChanged += DataFrame_PropertyChanged;
                    _dataFrame.ProcessThresholdSeries();
                }

                ClearResults();
                RaisePropertyChange(nameof(DataFrame));

            }
        }

        ///<inheritdoc/>
        public ProbabilityOrdinates ProbabilityOrdinates
        {
            get => _probabilityOrdinates;
            set
            {
                if (_probabilityOrdinates != null)
                    _probabilityOrdinates.CollectionChanged -= ProbabilityOrdinates_CollectionChanged;

                _probabilityOrdinates = value ?? new ProbabilityOrdinates();

                _probabilityOrdinates.CollectionChanged += ProbabilityOrdinates_CollectionChanged;

                ClearResults();
                RaisePropertyChange(nameof(ProbabilityOrdinates));
            }
        }

        /// <summary>
        /// Gets the list of fitted distributions and their associated metrics.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The collection contains one <see cref="FittedDistribution"/> instance
        /// per candidate distribution in <see cref="DistributionList"/>.
        /// </para>
        /// </remarks>
        public List<FittedDistribution> FittedDistributions => _fittedDistributions;


        /// <summary>
        /// Gets the list of candidate distributions available for fitting.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This list defines which distributions are fitted when
        /// <see cref="RunAsync(SafeProgressReporter)"/> is called. The default
        /// list contains all 15 supported univariate distributions.
        /// </para>
        /// <para>
        /// Property (with a private setter) rather than a public field — encapsulating the
        /// candidate set behind a property prevents external code from replacing the list
        /// reference wholesale, which would silently invalidate any in-flight fit. Existing
        /// callers that mutate the list contents via <c>Add</c> / <c>Remove</c> still work.
        /// </para>
        /// </remarks>
        public List<UnivariateDistributionBase> DistributionList { get; private set; } = new List<UnivariateDistributionBase>()
        { new Exponential(),
          new GammaDistribution(),
          new GeneralizedExtremeValue(),
          new GeneralizedLogistic(),
          new GeneralizedNormal(),
          new GeneralizedPareto(),
          new Gumbel(),
          new KappaFour(),
          new LnNormal(),
          new Logistic(),
          new LogNormal(),
          new LogPearsonTypeIII(),
          new Normal(),
          new PearsonTypeIII(),
          new Weibull()
        };

        #endregion

        #region Methods

        /// <summary>
        /// Handles <see cref="DataFrame.PropertyChanged"/> events. Any change to
        /// the data frame other than plotting positions will clear the current
        /// fitting results.
        /// </summary>
        private void DataFrame_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(DataFrame.PlottingParameter) &&
                e.PropertyName != nameof(Data.PlottingPosition))
            {
                ClearResults();
                RaisePropertyChange(nameof(DataFrame));
            }
        }

        /// <summary>
        /// Handles changes to the <see cref="ProbabilityOrdinates"/> collection.
        /// </summary>
        /// <remarks>
        /// Probability ordinates do not affect the MLE fit stored in
        /// <see cref="FittedDistributions"/> — they are consumed only by the App-layer
        /// plot/table rendering (see FittingAnalysisControl). So this handler simply
        /// notifies listeners that ordinates changed; it does not touch fit state.
        /// </remarks>
        private void ProbabilityOrdinates_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            RaisePropertyChange(nameof(ProbabilityOrdinates));
        }

        /// <summary>
        /// Clears the fitting analysis results and reinitializes the
        /// <see cref="FittedDistributions"/> collection with default entries.
        /// </summary>
        public void ClearResults()
        {
            _fittedDistributions = new List<FittedDistribution>();
            for (int i = 0; i < DistributionList.Count; i++)
            {
                _fittedDistributions.Add(new FittedDistribution(DistributionList[i].Clone()));
            }
            IsEstimated = false;
        }

        /// <inheritdoc/>
        public override async Task RunAsync(SafeProgressReporter? progressReporter = null)
        {
            if (DataFrame == null)
                throw new InvalidOperationException("DataFrame must be set before running the fitting analysis.");

            if (Validate().IsValid == false)
                throw new InvalidOperationException("Analysis is not valid. Please check the configuration before running the analysis.");

            // 1. Preview event, allow GUI to cancel before we start
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

            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();
            var token = _cancellationTokenSource.Token;

            ClearResults();
            progressReporter?.IndicateTaskStart();
            AnalysisProgress.ReportStarting(progressReporter);

            int iteration = 0;
            int N = DistributionList.Count;

            bool wasCanceled = false;
            Exception? error = null;

            try
            {
                await Task.Run(() =>
                {
                    try
                    {
                        // CancellationToken in ParallelOptions prevents Parallel.For from
                        // starting new iterations after cancellation is requested. This is
                        // the only mechanism that stops queued iterations from running.
                        var options = AnalysisProgress.CreateParallelOptions(token);

                        Parallel.For(0, N, options, idx =>
                        {
                            // Cooperative check for iterations already dequeued
                            if (token.IsCancellationRequested) return;

                            try
                            {
                                var dist = new UnivariateDistribution(DataFrame, DistributionList[idx]);
                                dist.UseJeffreysRuleForScale = false; // To be consistent with MLE
                                dist.SetDefaultParameters();

                                var mle = new MaximumLikelihood(dist, OptimizationMethod.DifferentialEvolution)
                                {
                                    ReportFailure = true,
                                    ComputeHessian = false,
                                    // Set optimizer options for robustness
                                    Optimizer = { MaxIterations = 10000, MaxFunctionEvaluations = 100000 }
                                };

                                mle.Estimate();

                                // Check cancellation after MLE completes to skip result storage
                                if (token.IsCancellationRequested) return;

                                if (mle.IsEstimated)
                                {
                                    dist.Distribution.SetParameters(mle.BestParameterSet.Values);

                                    var aic = mle.GetAIC();
                                    var bic = mle.GetBIC(DataFrame.TotalRecordLength());

                                    var values = DataFrame.ExactSeries.ValuesToList();
                                    values.AddRange(DataFrame.UncertainSeries.ValuesToList());
                                    values.AddRange(DataFrame.IntervalSeries.ValuesToList());

                                    var probs = DataFrame.ExactSeries.Select(x => x.PlottingPositionComplement).ToList();
                                    probs.AddRange(DataFrame.UncertainSeries.Select(x => x.PlottingPositionComplement));
                                    probs.AddRange(DataFrame.IntervalSeries.Select(x => x.PlottingPositionComplement));

                                    var rmse = GoodnessOfFit.RMSE(values, probs, dist.Distribution);

                                    bool success = Tools.IsFinite(aic) && Tools.IsFinite(bic) && Tools.IsFinite(rmse);

                                    _fittedDistributions[idx] = new FittedDistribution(
                                        dist.Distribution.Clone(),
                                        aic,
                                        bic,
                                        rmse,
                                        success,
                                        success);
                                }
                                else
                                {
                                    _fittedDistributions[idx] = new FittedDistribution(dist.Distribution.Clone());
                                }

                                int currentIteration = Interlocked.Increment(ref iteration);
                                progressReporter?.ReportProgress(AnalysisProgress.EstimationComplete * currentIteration / N);
                            }
                            catch (Exception ex)
                            {
                                // Fitting failed for this distribution - record the failure
                                // diagnostic on the FittedDistribution so consumers can
                                // surface the reason without re-running the fit.
                                Debug.WriteLine($"Distribution fitting failed for {DistributionList[idx].Type}: {ex.Message}");
                                if (_fittedDistributions[idx] != null)
                                {
                                    _fittedDistributions[idx].ErrorMessage = ex.Message;
                                }

                                // Ensure progress is still reported
                                int currentIteration = Interlocked.Increment(ref iteration);
                                progressReporter?.ReportProgress(AnalysisProgress.EstimationComplete * currentIteration / N);
                            }
                        });
                    }
                    catch (OperationCanceledException)
                    {
                        // Caught inside Task.Run so the exception does not bubble up
                        // through await to the debugger. Parallel.For throws this when
                        // its CancellationToken fires.
                        wasCanceled = true;
                    }

                    // Also set flag for cooperative cancellation path
                    if (token.IsCancellationRequested)
                        wasCanceled = true;
                });

                if (!wasCanceled)
                {
                    AnalysisProgress.ReportProcessingResults(progressReporter);
                    IsEstimated = true;
                    AnalysisProgress.ReportComplete(progressReporter);
                }
                else
                {
                    IsEstimated = false;
                }
            }
            catch (OperationCanceledException)
            {
                // Safety net — should not be reached since the exception is caught inside Task.Run
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

        ///<inheritdoc/>
        public override (bool IsValid, List<string> ValidationMessages) Validate()
        {
            bool isValid = true;
            var messageList = new List<string>();

            // Validate data frame
            var dataValid = DataFrame.Validate();
            if (!dataValid.IsValid)
            {
                isValid = false;
                messageList.AddRange(dataValid.ValidationMessages);
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

        /// <summary>
        /// Serializes the fitting analysis configuration and results to an XML element.
        /// </summary>
        /// <returns>
        /// An <see cref="XElement"/> representing the current analysis configuration
        /// and, if available, the fitted distributions.
        /// </returns>
        /// <remarks>
        /// <para>
        /// The XML representation does not include the underlying <see cref="DataFrame"/>.
        /// It stores:
        /// </para>
        /// <list type="bullet">
        /// <item><description><c>ProbabilityOrdinates</c> as a delimited string.</description></item>
        /// <item><description><c>FittedDistributions</c> as a collection of child elements (optional).</description></item>
        /// </list>
        /// </remarks>
        public XElement ToXElement()
        {
            var root = new XElement("FittingAnalysis",
                new XElement("ProbabilityOrdinates",
                    ProbabilityOrdinates.ToDelimitedString(ProbabilityOrdinates.DefaultDelimiter)),
                new XAttribute("IsEstimated", IsEstimated));

            var fittedRoot = new XElement("FittedDistributions");
            foreach (var fd in _fittedDistributions)
            {
                // Assumes FittedDistribution has a ToXElement() method and uses the name "FittedDistribution"
                fittedRoot.Add(fd.ToXElement());
            }

            root.Add(fittedRoot);

            return root;
        }

        #endregion

    }
}
