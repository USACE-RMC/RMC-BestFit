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
        None,
        Logarithmic,
        BoxCox,
        YeoJohnson
    }
}
