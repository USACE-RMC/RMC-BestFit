namespace RMC.BestFit.Api.Store
{
    /// <summary>
    /// Identifies which time-series model family a TimeSeries-kind analysis resource wraps —
    /// the discriminator of the single api/analyses/timeseries route.
    /// </summary>
    public enum TimeSeriesModelType
    {
        /// <summary>
        /// Autoregressive model AR(p) (<see cref="RMC.BestFit.Models.AutoRegressive"/>).
        /// </summary>
        Ar,

        /// <summary>
        /// Moving average model MA(q) (<see cref="RMC.BestFit.Models.MovingAverage"/>).
        /// </summary>
        Ma,

        /// <summary>
        /// Autoregressive integrated moving average model ARIMA(p,d,q)
        /// (<see cref="RMC.BestFit.Models.ARIMA"/>).
        /// </summary>
        Arima,

        /// <summary>
        /// ARIMA with exogenous covariates, trend, and seasonality ARIMAX(p,d,q,b)
        /// (<see cref="RMC.BestFit.Models.ARIMAX"/>).
        /// </summary>
        Arimax
    }
}
