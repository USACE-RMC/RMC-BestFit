namespace RMC.BestFit.Api.Store
{
    /// <summary>
    /// Identifies how a time-series resource was created.
    /// </summary>
    public enum TimeSeriesSource
    {
        /// <summary>
        /// The time series was supplied point-by-point by the client.
        /// </summary>
        Manual,

        /// <summary>
        /// The time series was downloaded from the USGS water services.
        /// </summary>
        Usgs
    }
}
