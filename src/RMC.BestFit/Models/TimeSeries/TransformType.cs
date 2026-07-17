namespace RMC.BestFit.Models
{
    /// <summary>
    /// Enumeration of time series transform types.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// </remarks>
    public enum Transform
    {
        /// <summary>
        /// No transformation is applied.
        /// </summary>
        None,

        /// <summary>
        /// Natural logarithm transformation.
        /// </summary>
        Logarithmic,

        /// <summary>
        /// Box-Cox power transformation.
        /// </summary>
        BoxCox,

        /// <summary>
        /// Yeo-Johnson power transformation.
        /// </summary>
        YeoJohnson
    }
}
