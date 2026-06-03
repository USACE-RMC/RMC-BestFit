namespace RMC.BestFit.Models
{
    /// <summary>
    /// Represents a single component of the prior log-likelihood for influence diagnostics.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    /// Prior components include parameter priors, quantile priors, Jeffreys scale priors,
    /// and other penalty terms. Each component has a name identifying its source and
    /// a log-likelihood contribution to the total prior.
    /// </para>
    /// <para>
    /// This is a readonly struct for memory efficiency in parallel loops. Structs provide
    /// better cache locality and avoid GC pressure when processing thousands of MCMC samples.
    /// </para>
    /// </remarks>
    public readonly struct PriorComponent
    {
        /// <summary>
        /// Creates a new prior component with the specified name and log-likelihood.
        /// </summary>
        /// <param name="name">The descriptive name of this prior component.</param>
        /// <param name="logLikelihood">The log-likelihood contribution of this component.</param>
        /// <param name="type">The type of prior component.</param>
        public PriorComponent(string name, double logLikelihood, PriorComponentType type = PriorComponentType.ParameterPrior)
        {
            Name = name;
            LogLikelihood = logLikelihood;
            Type = type;
        }

        /// <summary>
        /// Gets the descriptive name of this prior component.
        /// </summary>
        /// <remarks>
        /// Examples: "Location prior", "Scale prior (Jeffreys)", "Q100 quantile prior", "Spatial location error".
        /// </remarks>
        public string Name { get; }

        /// <summary>
        /// Gets the log-likelihood contribution of this prior component.
        /// </summary>
        public double LogLikelihood { get; }

        /// <summary>
        /// Gets the type of this prior component.
        /// </summary>
        public PriorComponentType Type { get; }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"{Name}: {LogLikelihood:F4}";
        }
    }

    /// <summary>
    /// Enumerates the types of prior components for categorization in diagnostics.
    /// </summary>
    public enum PriorComponentType
    {
        /// <summary>
        /// Prior distribution on a model parameter.
        /// </summary>
        ParameterPrior,

        /// <summary>
        /// Quantile-based prior (e.g., on the 100-year flood).
        /// </summary>
        QuantilePrior,

        /// <summary>
        /// Jeffreys non-informative prior on scale parameters.
        /// </summary>
        JeffreysScalePrior,

        /// <summary>
        /// Spatial error penalty term.
        /// </summary>
        SpatialError,

        /// <summary>
        /// Jacobian term from parameter transformation.
        /// </summary>
        Jacobian,

        /// <summary>
        /// Other penalty or regularization term.
        /// </summary>
        OtherPenalty
    }
}
