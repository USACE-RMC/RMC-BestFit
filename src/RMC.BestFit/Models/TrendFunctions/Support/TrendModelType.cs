namespace RMC.BestFit.Models.TrendFunctions.Support
{
    /// <summary>
    /// Enumeration of supported trend model types used to represent
    /// how distribution parameters vary with time.
    /// </summary>
    public enum TrendModelType
    {
        /// <summary>
        /// Constant in time.
        /// </summary>
        Constant,

        /// <summary>
        /// Cubic polynomial in time.
        /// </summary>
        Cubic,

        /// <summary>
        /// Exponential function in time.
        /// </summary>
        Exponential,

        /// <summary>
        /// Linear function in time.
        /// </summary>
        Linear,

        /// <summary>
        /// Logistic (sigmoid) function in time.
        /// </summary>
        Logistic,

        /// <summary>
        /// Power-law function in time.
        /// </summary>
        Power,

        /// <summary>
        /// Quadratic polynomial in time.
        /// </summary>
        Quadratic,

        /// <summary>
        /// Reciprocal function in time.
        /// </summary>
        Reciprocal,

        /// <summary>
        /// Sinusoidal function in time.
        /// </summary>
        Sinusoidal,

        /// <summary>
        /// Step function with a single change point.
        /// </summary>
        StepFunction,

        /// <summary>
        /// General linear function with arbitrary covariates.
        /// Used for spatial regression surfaces and covariate modeling.
        /// </summary>
        GeneralLinear
    }
}

