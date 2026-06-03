namespace RMC.BestFit.Models
{
    /// <summary>
    /// Represents a single data point's contribution to the log-likelihood for influence diagnostics.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    /// Data components track the pointwise log-likelihood contribution of each observation,
    /// along with metadata describing the data type and representative value. This enables
    /// influence diagnostics that identify which observations have the largest impact on
    /// parameter estimates.
    /// </para>
    /// <para>
    /// This is a readonly struct for memory efficiency in parallel loops. Structs provide
    /// better cache locality and avoid GC pressure when processing thousands of MCMC samples.
    /// </para>
    /// </remarks>
    public readonly struct DataComponent
    {
        /// <summary>
        /// Creates a new data component for an exact observation.
        /// </summary>
        /// <param name="index">The zero-based index of this observation in the data.</param>
        /// <param name="logLikelihood">The log-likelihood contribution of this observation.</param>
        /// <param name="value">The observed value.</param>
        /// <param name="name">Optional label for this observation.</param>
        public DataComponent(int index, double logLikelihood, double value, string? name = null)
        {
            Index = index;
            LogLikelihood = logLikelihood;
            Value = value;
            Count = 1;
            Name = name;
            Type = DataComponentType.Exact;
        }

        /// <summary>
        /// Creates a new data component with full specification.
        /// </summary>
        /// <param name="index">The zero-based index of this observation in the data.</param>
        /// <param name="logLikelihood">The log-likelihood contribution of this observation.</param>
        /// <param name="value">The representative value (exact, mean, midpoint, or threshold).</param>
        /// <param name="type">The type of data observation.</param>
        /// <param name="count">The count for threshold observations (number below or above).</param>
        /// <param name="name">Optional label for this observation (e.g., "1900-1950" for thresholds).</param>
        public DataComponent(int index, double logLikelihood, double value, DataComponentType type, int count = 1, string? name = null)
        {
            Index = index;
            LogLikelihood = logLikelihood;
            Value = value;
            Type = type;
            Count = count;
            Name = name;
        }

        /// <summary>
        /// Gets the zero-based index of this observation in the data.
        /// </summary>
        public int Index { get; }

        /// <summary>
        /// Gets the log-likelihood contribution of this observation.
        /// </summary>
        public double LogLikelihood { get; }

        /// <summary>
        /// Gets the representative value for this observation.
        /// </summary>
        /// <remarks>
        /// <para>For <see cref="DataComponentType.Exact"/>: the observed value.</para>
        /// <para>For <see cref="DataComponentType.Uncertain"/>: the mean of the uncertainty distribution.</para>
        /// <para>For <see cref="DataComponentType.Interval"/>: the midpoint of the interval.</para>
        /// <para>For <see cref="DataComponentType.LeftCensored"/>: the threshold value (upper bound).</para>
        /// <para>For <see cref="DataComponentType.RightCensored"/>: the threshold value (lower bound).</para>
        /// </remarks>
        public double Value { get; }

        /// <summary>
        /// Gets the type of data observation.
        /// </summary>
        public DataComponentType Type { get; }

        /// <summary>
        /// Gets the count associated with this observation.
        /// </summary>
        /// <remarks>
        /// For exact, uncertain, and interval observations, this is typically 1.
        /// For threshold observations, this is the number of values below (left-censored)
        /// or above (right-censored) the threshold.
        /// </remarks>
        public int Count { get; }

        /// <summary>
        /// Gets the optional label for this observation.
        /// </summary>
        /// <remarks>
        /// Useful for threshold observations to indicate the time period covered
        /// (e.g., "1900-1950") or other identifying information.
        /// </remarks>
        public string? Name { get; }

        /// <summary>
        /// Gets a value indicating whether this is a censored observation.
        /// </summary>
        public bool IsCensored => Type == DataComponentType.LeftCensored || Type == DataComponentType.RightCensored;

        /// <summary>
        /// Gets a value indicating whether this observation represents grouped/threshold data.
        /// </summary>
        public bool IsThreshold => IsCensored && Count > 1;

        /// <inheritdoc/>
        public override string ToString()
        {
            var nameStr = string.IsNullOrEmpty(Name) ? $"[{Index}]" : Name;
            return Type switch
            {
                DataComponentType.Exact => $"{nameStr}: {Value:G4} = {LogLikelihood:F4}",
                DataComponentType.Uncertain => $"{nameStr}: ~{Value:G4} = {LogLikelihood:F4}",
                DataComponentType.Interval => $"{nameStr}: [{Value:G4}] = {LogLikelihood:F4}",
                DataComponentType.LeftCensored => $"{nameStr}: <{Value:G4} (n={Count}) = {LogLikelihood:F4}",
                DataComponentType.RightCensored => $"{nameStr}: >{Value:G4} (n={Count}) = {LogLikelihood:F4}",
                _ => $"{nameStr}: {Value:G4} = {LogLikelihood:F4}"
            };
        }
    }

    /// <summary>
    /// Enumerates the types of data observations, matching DataFrame data types.
    /// </summary>
    public enum DataComponentType
    {
        /// <summary>
        /// An exact, precisely observed value.
        /// </summary>
        Exact,

        /// <summary>
        /// An uncertain value with measurement error (represented by its mean).
        /// </summary>
        Uncertain,

        /// <summary>
        /// An interval-censored value (represented by its midpoint).
        /// </summary>
        Interval,

        /// <summary>
        /// A left-censored value (threshold with count of values below).
        /// </summary>
        LeftCensored,

        /// <summary>
        /// A right-censored value (threshold with count of values above).
        /// </summary>
        RightCensored
    }
}
