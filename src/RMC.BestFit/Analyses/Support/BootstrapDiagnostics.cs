using System;
using System.Globalization;
using System.Threading;
using System.Xml.Linq;

namespace RMC.BestFit.Analyses
{
    /// <summary>
    /// Collects diagnostic counters from parametric bootstrap and pivot bootstrap methods.
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
    /// </remarks>
    public class BootstrapDiagnostics
    {
        #region Counters

        /// <summary>
        /// The total number of bootstrap replicates requested.
        /// </summary>
        private int _totalReplicates;

        /// <summary>
        /// The number of replicates that failed all retry attempts and fell back to parent parameters.
        /// </summary>
        private int _failedReplicates;

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
        /// Gets the number of replicates that failed all retry attempts and fell back to parent parameters.
        /// </summary>
        public int FailedReplicates => _failedReplicates;

        /// <summary>
        /// Gets the number of valid (successfully estimated) replicates.
        /// </summary>
        public int ValidReplicates => _totalReplicates - _failedReplicates;

        /// <summary>
        /// Gets the failure rate as a fraction (0 to 1).
        /// </summary>
        public double FailureRate => _totalReplicates > 0 ? (double)_failedReplicates / _totalReplicates : 0.0;

        /// <summary>
        /// Gets the cumulative number of retry attempts across all replicates.
        /// </summary>
        public int TotalRetries => _totalRetries;

        /// <summary>
        /// Gets the average number of retries per replicate.
        /// </summary>
        public double AverageRetries => _totalReplicates > 0 ? (double)_totalRetries / _totalReplicates : 0.0;

        /// <summary>
        /// Gets the cumulative number of GMM function evaluations across all bootstrap replicates.
        /// </summary>
        public int TotalFunctionEvaluations => _totalFunctionEvaluations;

        /// <summary>
        /// Gets the average number of GMM function evaluations per replicate.
        /// </summary>
        public double AverageFunctionEvaluations => _totalReplicates > 0 ? (double)_totalFunctionEvaluations / _totalReplicates : 0.0;

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
        /// Atomically increments the failed replicate counter by one.
        /// </summary>
        public void IncrementFailed()
        {
            Interlocked.Increment(ref _failedReplicates);
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
            element.SetAttributeValue(nameof(FailedReplicates), _failedReplicates.ToString(CultureInfo.InvariantCulture));
            element.SetAttributeValue(nameof(TotalRetries), _totalRetries.ToString(CultureInfo.InvariantCulture));
            element.SetAttributeValue(nameof(TotalFunctionEvaluations), _totalFunctionEvaluations.ToString(CultureInfo.InvariantCulture));
            element.SetAttributeValue(nameof(PivotRejections), _pivotRejections.ToString(CultureInfo.InvariantCulture));
            element.SetAttributeValue(nameof(MahalanobisRejections), _mahalanobisRejections.ToString(CultureInfo.InvariantCulture));
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
