using Numerics.Mathematics.Optimization;
using System;
using System.Globalization;
using System.Threading;
using System.Xml.Linq;

namespace RMC.BestFit.Analyses
{
    /// <summary>
    /// Collects diagnostic counters from the Bulletin 17C uncertainty sampling methods
    /// (parametric bootstrap, pivot bootstrap, and multivariate normal sampling).
    /// </summary>
    /// <remarks>
    /// <para>
    ///     <b>Authors:</b>
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    ///     All integer counters are updated with <see cref="Interlocked"/> operations for thread safety
    ///     during parallel bootstrap loops. Timing fields are set from <see cref="System.Diagnostics.Stopwatch"/>
    ///     measurements on the calling thread.
    /// </para>
    /// <para>
    ///     Ordinary and pivotal bootstrap refits use bounded retries. After those retries are
    ///     exhausted, phase one substitutes the fitted parent parameters (and, for the pivotal
    ///     method, parent covariance) so downstream processing still receives the configured
    ///     output length. <see cref="FailedReplicates"/> counts those substitutions. Later pivotal
    ///     transform failures can still reduce <see cref="RetainedReplicates"/> and prevent the
    ///     all-or-nothing uncertainty result from being published.
    /// </para>
    /// </remarks>
    public class BootstrapDiagnostics
    {
        #region Counters

        /// <summary>
        /// The total number of bootstrap replicates requested.
        /// </summary>
        private int _totalReplicates;

        /// <summary>
        /// The total number of candidate replicates evaluated, including replacements.
        /// A value of -1 indicates a legacy record where this count was not stored.
        /// </summary>
        private int _attemptedReplicates = -1;

        /// <summary>
        /// The number of requested replicates whose phase-one refit exhausted every retry and was
        /// replaced by the fitted parent parameters to preserve the configured output length.
        /// </summary>
        private int _failedReplicates;

        /// <summary>
        /// The number of parameter sets actually delivered to the results, or -1 when not recorded.
        /// </summary>
        private int _retainedReplicates = -1;

        /// <summary>
        /// The number of pivot draws discarded because the link-space transform failed
        /// (e.g., a replicate covariance that could not be factored) rather than because
        /// the standardized pivot exceeded the z-limit.
        /// </summary>
        private int _transformFailures;

        /// <summary>
        /// The number of replicate GMM attempts that ended with <see cref="OptimizationStatus.Success"/>.
        /// </summary>
        private int _statusSuccessCount;

        /// <summary>
        /// The number of replicate GMM attempts that ended with <see cref="OptimizationStatus.MaximumIterationsReached"/>.
        /// </summary>
        private int _statusMaximumIterationsCount;

        /// <summary>
        /// The number of replicate GMM attempts that ended with <see cref="OptimizationStatus.MaximumFunctionEvaluationsReached"/>.
        /// </summary>
        private int _statusMaximumFunctionEvaluationsCount;

        /// <summary>
        /// The number of replicate GMM attempts that ended with <see cref="OptimizationStatus.Failure"/>.
        /// </summary>
        private int _statusFailureCount;

        /// <summary>
        /// The number of replicate GMM attempts that ended with <see cref="OptimizationStatus.None"/>.
        /// </summary>
        private int _statusNoneCount;

        /// <summary>
        /// The number of GMM optimization passes that fell back from BFGS to Nelder-Mead.
        /// </summary>
        private int _optimizerFallbacks;

        /// <summary>
        /// The cumulative number of retry attempts across all replicates (including the successful final attempt).
        /// </summary>
        private int _totalRetries;

        /// <summary>
        /// The cumulative number of GMM function evaluations across all bootstrap replicates.
        /// </summary>
        private int _totalFunctionEvaluations;

        /// <summary>
        /// The number of pivot draws rejected because standardized pivots exceeded the z-limit (pivot bootstrap only).
        /// </summary>
        private int _pivotRejections;

        /// <summary>
        /// The number of bootstrap replicates rejected because their Mahalanobis distance from the parent
        /// estimate exceeded the χ²(p, 0.999) threshold, indicating a degenerate local optimum.
        /// </summary>
        private int _mahalanobisRejections;

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the total number of bootstrap replicates requested.
        /// </summary>
        public int TotalReplicates
        {
            get => _totalReplicates;
            set => _totalReplicates = value;
        }

        /// <summary>
        /// Gets the total number of candidate replicates evaluated, including replacements.
        /// </summary>
        /// <remarks>
        /// Diagnostics serialized before this counter was introduced fall back to
        /// <see cref="TotalReplicates"/>.
        /// </remarks>
        public int AttemptedReplicates => _attemptedReplicates >= 0 ? _attemptedReplicates : _totalReplicates;

        /// <summary>
        /// Gets the number of requested replicates whose phase-one refit exhausted every retry and
        /// was replaced by the fitted parent parameters. Such replacements remain in the delivered
        /// ordinary-bootstrap sample and enter pivotal phase two.
        /// </summary>
        public int FailedReplicates => _failedReplicates;

        /// <summary>
        /// Gets the number of valid (successfully estimated) replicates.
        /// </summary>
        public int ValidReplicates => Math.Max(0, AttemptedReplicates - _failedReplicates);

        /// <summary>
        /// Gets or sets the number of parameter sets actually delivered to the results.
        /// </summary>
        /// <remarks>
        /// For the pivot bootstrap this is smaller than <see cref="ValidReplicates"/> when
        /// z-limit rejections or transform failures drop draws after the fitting phase. When
        /// the value was never recorded (legacy serialized diagnostics), the getter falls back
        /// to <see cref="ValidReplicates"/>.
        /// </remarks>
        public int RetainedReplicates
        {
            get => _retainedReplicates >= 0 ? _retainedReplicates : ValidReplicates;
            set => _retainedReplicates = value;
        }

        /// <summary>
        /// Gets the number of pivot draws discarded because the link-space transform failed
        /// (as opposed to a standardized pivot exceeding the z-limit).
        /// Only applicable to the pivot (bias-corrected) bootstrap method.
        /// </summary>
        public int TransformFailures => _transformFailures;

        /// <summary>
        /// Gets the number of replicate GMM attempts that ended with <see cref="OptimizationStatus.Success"/>.
        /// </summary>
        /// <remarks>
        /// The status counters record every attempt, including retries, so their sum is
        /// approximately <see cref="TotalReplicates"/> plus <see cref="TotalRetries"/> (attempts
        /// that threw before the optimizer finished are not recorded). They exist to make the
        /// replicate acceptance gate observable: a large max-iterations count with few failures
        /// indicates best-effort terminations that are accepted, not rejected.
        /// </remarks>
        public int StatusSuccessCount => _statusSuccessCount;

        /// <summary>
        /// Gets the number of replicate GMM attempts that ended with <see cref="OptimizationStatus.MaximumIterationsReached"/>.
        /// </summary>
        public int StatusMaximumIterationsCount => _statusMaximumIterationsCount;

        /// <summary>
        /// Gets the number of replicate GMM attempts that ended with <see cref="OptimizationStatus.MaximumFunctionEvaluationsReached"/>.
        /// </summary>
        public int StatusMaximumFunctionEvaluationsCount => _statusMaximumFunctionEvaluationsCount;

        /// <summary>
        /// Gets the number of replicate GMM attempts that ended with <see cref="OptimizationStatus.Failure"/>.
        /// </summary>
        public int StatusFailureCount => _statusFailureCount;

        /// <summary>
        /// Gets the number of replicate GMM attempts that ended with <see cref="OptimizationStatus.None"/>.
        /// </summary>
        public int StatusNoneCount => _statusNoneCount;

        /// <summary>
        /// Gets the number of GMM optimization passes that fell back from BFGS to Nelder-Mead.
        /// </summary>
        public int OptimizerFallbacks => _optimizerFallbacks;

        /// <summary>
        /// Gets the failure rate as a fraction (0 to 1).
        /// </summary>
        public double FailureRate => AttemptedReplicates > 0 ? (double)_failedReplicates / AttemptedReplicates : 0.0;

        /// <summary>
        /// Gets the cumulative number of retry attempts across all replicates.
        /// </summary>
        public int TotalRetries => _totalRetries;

        /// <summary>
        /// Gets the average number of retries per replicate.
        /// </summary>
        public double AverageRetries => AttemptedReplicates > 0 ? (double)_totalRetries / AttemptedReplicates : 0.0;

        /// <summary>
        /// Gets the cumulative number of GMM function evaluations across all bootstrap replicates.
        /// </summary>
        public int TotalFunctionEvaluations => _totalFunctionEvaluations;

        /// <summary>
        /// Gets the average number of GMM function evaluations per replicate.
        /// </summary>
        public double AverageFunctionEvaluations => AttemptedReplicates > 0 ? (double)_totalFunctionEvaluations / AttemptedReplicates : 0.0;

        /// <summary>
        /// Gets the number of pivot draws rejected because standardized pivots exceeded the z-limit.
        /// Only applicable to the pivot (bias-corrected) bootstrap method.
        /// </summary>
        public int PivotRejections => _pivotRejections;

        /// <summary>
        /// Gets the pivot rejection rate as a fraction (0 to 1).
        /// Only meaningful for the pivot (bias-corrected) bootstrap method.
        /// </summary>
        public double PivotRejectionRate => _totalReplicates > 0 ? (double)_pivotRejections / _totalReplicates : 0.0;

        /// <summary>
        /// Gets the number of bootstrap replicates rejected via Mahalanobis distance outlier detection.
        /// </summary>
        public int MahalanobisRejections => _mahalanobisRejections;

        /// <summary>
        /// Gets the Mahalanobis rejection rate as a fraction (0 to 1).
        /// </summary>
        public double MahalanobisRejectionRate => _totalReplicates > 0 ? (double)_mahalanobisRejections / _totalReplicates : 0.0;

        /// <summary>
        /// Gets or sets the elapsed time for Phase 1 (bootstrap fitting).
        /// </summary>
        public TimeSpan Phase1Time { get; set; }

        /// <summary>
        /// Gets or sets the elapsed time for Phase 2 (link function fitting). Pivot bootstrap only.
        /// </summary>
        public TimeSpan Phase2Time { get; set; }

        /// <summary>
        /// Gets or sets the elapsed time for Phase 3 (pivot draw generation). Pivot bootstrap only.
        /// </summary>
        public TimeSpan Phase3Time { get; set; }

        #endregion

        #region Thread-Safe Increment Methods

        /// <summary>
        /// Atomically increments the discarded replicate counter by one.
        /// </summary>
        /// <remarks>
        /// Called when a replicate has exhausted every retry attempt and is dropped from
        /// the delivered sample.
        /// </remarks>
        public void IncrementFailed()
        {
            Interlocked.Increment(ref _failedReplicates);
        }

        /// <summary>
        /// Atomically increments the attempted candidate counter by one.
        /// </summary>
        public void IncrementAttempted()
        {
            if (Volatile.Read(ref _attemptedReplicates) < 0)
            {
                Interlocked.CompareExchange(ref _attemptedReplicates, 0, -1);
            }

            Interlocked.Increment(ref _attemptedReplicates);
        }

        /// <summary>
        /// Atomically adds optimizer fallback events to the diagnostic counter.
        /// </summary>
        /// <param name="count">The number of fallback events to add.</param>
        public void AddOptimizerFallbacks(int count)
        {
            if (count > 0)
            {
                Interlocked.Add(ref _optimizerFallbacks, count);
            }
        }

        /// <summary>
        /// Atomically increments the pivot transform failure counter by one.
        /// </summary>
        /// <remarks>
        /// Called when a pivot draw is discarded because the link-space transform threw
        /// (e.g., an unfactorable replicate covariance), as opposed to a z-limit rejection.
        /// </remarks>
        public void IncrementTransformFailure()
        {
            Interlocked.Increment(ref _transformFailures);
        }

        /// <summary>
        /// Atomically records the terminal optimizer status of one replicate GMM attempt.
        /// </summary>
        /// <param name="status">The final <see cref="OptimizationStatus"/> reported by the replicate estimator.</param>
        /// <remarks>
        /// Recorded per attempt (including retries) so the report can show the distribution of
        /// optimizer outcomes behind the replicate acceptance gate.
        /// </remarks>
        public void RecordGMMStatus(OptimizationStatus status)
        {
            switch (status)
            {
                case OptimizationStatus.Success:
                    Interlocked.Increment(ref _statusSuccessCount);
                    break;
                case OptimizationStatus.MaximumIterationsReached:
                    Interlocked.Increment(ref _statusMaximumIterationsCount);
                    break;
                case OptimizationStatus.MaximumFunctionEvaluationsReached:
                    Interlocked.Increment(ref _statusMaximumFunctionEvaluationsCount);
                    break;
                case OptimizationStatus.Failure:
                    Interlocked.Increment(ref _statusFailureCount);
                    break;
                default:
                    Interlocked.Increment(ref _statusNoneCount);
                    break;
            }
        }

        /// <summary>
        /// Atomically increments the retry counter by the specified amount.
        /// </summary>
        /// <param name="count">The number of retries to add.</param>
        public void AddRetries(int count)
        {
            Interlocked.Add(ref _totalRetries, count);
        }

        /// <summary>
        /// Atomically increments the function evaluation counter by the specified amount.
        /// </summary>
        /// <param name="count">The number of function evaluations to add.</param>
        public void AddFunctionEvaluations(int count)
        {
            Interlocked.Add(ref _totalFunctionEvaluations, count);
        }

        /// <summary>
        /// Atomically increments the pivot rejection counter by one.
        /// </summary>
        public void IncrementPivotRejection()
        {
            Interlocked.Increment(ref _pivotRejections);
        }

        /// <summary>
        /// Atomically increments the Mahalanobis outlier rejection counter by one.
        /// </summary>
        public void IncrementMahalanobisRejection()
        {
            Interlocked.Increment(ref _mahalanobisRejections);
        }

        #endregion

        #region Serialization

        /// <summary>
        /// Serializes the bootstrap diagnostics to an <see cref="XElement"/>.
        /// </summary>
        /// <returns>An XML element containing the serialized diagnostics.</returns>
        public XElement ToXElement()
        {
            var element = new XElement(nameof(BootstrapDiagnostics));
            element.SetAttributeValue(nameof(TotalReplicates), _totalReplicates.ToString(CultureInfo.InvariantCulture));
            element.SetAttributeValue(nameof(AttemptedReplicates), _attemptedReplicates.ToString(CultureInfo.InvariantCulture));
            element.SetAttributeValue(nameof(FailedReplicates), _failedReplicates.ToString(CultureInfo.InvariantCulture));
            element.SetAttributeValue(nameof(TotalRetries), _totalRetries.ToString(CultureInfo.InvariantCulture));
            element.SetAttributeValue(nameof(TotalFunctionEvaluations), _totalFunctionEvaluations.ToString(CultureInfo.InvariantCulture));
            element.SetAttributeValue(nameof(PivotRejections), _pivotRejections.ToString(CultureInfo.InvariantCulture));
            element.SetAttributeValue(nameof(MahalanobisRejections), _mahalanobisRejections.ToString(CultureInfo.InvariantCulture));
            // The raw backing value is persisted (including the -1 "not recorded" sentinel)
            // so the legacy fallback to ValidReplicates survives a round-trip.
            element.SetAttributeValue(nameof(RetainedReplicates), _retainedReplicates.ToString(CultureInfo.InvariantCulture));
            element.SetAttributeValue(nameof(TransformFailures), _transformFailures.ToString(CultureInfo.InvariantCulture));
            element.SetAttributeValue(nameof(StatusSuccessCount), _statusSuccessCount.ToString(CultureInfo.InvariantCulture));
            element.SetAttributeValue(nameof(StatusMaximumIterationsCount), _statusMaximumIterationsCount.ToString(CultureInfo.InvariantCulture));
            element.SetAttributeValue(nameof(StatusMaximumFunctionEvaluationsCount), _statusMaximumFunctionEvaluationsCount.ToString(CultureInfo.InvariantCulture));
            element.SetAttributeValue(nameof(StatusFailureCount), _statusFailureCount.ToString(CultureInfo.InvariantCulture));
            element.SetAttributeValue(nameof(StatusNoneCount), _statusNoneCount.ToString(CultureInfo.InvariantCulture));
            element.SetAttributeValue(nameof(OptimizerFallbacks), _optimizerFallbacks.ToString(CultureInfo.InvariantCulture));
            element.SetAttributeValue(nameof(Phase1Time), Phase1Time.Ticks.ToString(CultureInfo.InvariantCulture));
            element.SetAttributeValue(nameof(Phase2Time), Phase2Time.Ticks.ToString(CultureInfo.InvariantCulture));
            element.SetAttributeValue(nameof(Phase3Time), Phase3Time.Ticks.ToString(CultureInfo.InvariantCulture));
            return element;
        }

        /// <summary>
        /// Deserializes bootstrap diagnostics from an <see cref="XElement"/>.
        /// </summary>
        /// <param name="element">The XML element containing the serialized diagnostics.</param>
        /// <returns>A new <see cref="BootstrapDiagnostics"/> instance, or null if the element is null.</returns>
        public static BootstrapDiagnostics? FromXElement(XElement? element)
        {
            if (element == null) return null;

            var diag = new BootstrapDiagnostics();

            if (int.TryParse(element.Attribute(nameof(TotalReplicates))?.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out int total))
                diag._totalReplicates = total;
            if (int.TryParse(element.Attribute(nameof(AttemptedReplicates))?.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out int attempted))
                diag._attemptedReplicates = attempted;
            if (int.TryParse(element.Attribute(nameof(FailedReplicates))?.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out int failed))
                diag._failedReplicates = failed;
            if (int.TryParse(element.Attribute(nameof(TotalRetries))?.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out int retries))
                diag._totalRetries = retries;
            if (int.TryParse(element.Attribute(nameof(TotalFunctionEvaluations))?.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out int funcEvals))
                diag._totalFunctionEvaluations = funcEvals;
            if (int.TryParse(element.Attribute(nameof(PivotRejections))?.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out int pivots))
                diag._pivotRejections = pivots;
            if (int.TryParse(element.Attribute(nameof(MahalanobisRejections))?.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out int mahal))
                diag._mahalanobisRejections = mahal;
            // New attributes are optional so diagnostics saved by earlier versions restore
            // with their defaults (RetainedReplicates falls back to ValidReplicates).
            if (int.TryParse(element.Attribute(nameof(RetainedReplicates))?.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out int retained))
                diag._retainedReplicates = retained;
            if (int.TryParse(element.Attribute(nameof(TransformFailures))?.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out int transformFails))
                diag._transformFailures = transformFails;
            if (int.TryParse(element.Attribute(nameof(StatusSuccessCount))?.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out int statusSuccess))
                diag._statusSuccessCount = statusSuccess;
            if (int.TryParse(element.Attribute(nameof(StatusMaximumIterationsCount))?.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out int statusMaxIter))
                diag._statusMaximumIterationsCount = statusMaxIter;
            if (int.TryParse(element.Attribute(nameof(StatusMaximumFunctionEvaluationsCount))?.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out int statusMaxEvals))
                diag._statusMaximumFunctionEvaluationsCount = statusMaxEvals;
            if (int.TryParse(element.Attribute(nameof(StatusFailureCount))?.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out int statusFailure))
                diag._statusFailureCount = statusFailure;
            if (int.TryParse(element.Attribute(nameof(StatusNoneCount))?.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out int statusNone))
                diag._statusNoneCount = statusNone;
            if (int.TryParse(element.Attribute(nameof(OptimizerFallbacks))?.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out int optimizerFallbacks))
                diag._optimizerFallbacks = optimizerFallbacks;
            if (long.TryParse(element.Attribute(nameof(Phase1Time))?.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out long p1Ticks))
                diag.Phase1Time = TimeSpan.FromTicks(p1Ticks);
            if (long.TryParse(element.Attribute(nameof(Phase2Time))?.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out long p2Ticks))
                diag.Phase2Time = TimeSpan.FromTicks(p2Ticks);
            if (long.TryParse(element.Attribute(nameof(Phase3Time))?.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out long p3Ticks))
                diag.Phase3Time = TimeSpan.FromTicks(p3Ticks);

            return diag;
        }

        #endregion
    }
}
