namespace RMC.BestFit.Estimation
{
    /// <summary>
    /// Enumeration of optimization methods.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// </remarks>
    public enum OptimizationMethod
    {
        /// <summary>
        /// The Brent method for single parameter models.
        /// </summary>
        Brent,
        /// <summary>
        /// The Broyden-Fletcher-Goldfarb-Shanno (BFGS) optimization method.
        /// </summary>
        BFGS,
        /// <summary>
        /// The Nelder-Mead downhill simplex method.
        /// </summary>
        NelderMead,
        /// <summary>
        /// The Powell optimization method. 
        /// </summary>
        Powell,
        /// <summary>
        /// The Differential Evolution (DE) global optimization method.
        /// </summary>
        DifferentialEvolution,
        /// <summary>
        /// The multi-level single linkage (MLSL) global optimization method.
        /// </summary>
        MultilevelSingleLinkage
    }
}
