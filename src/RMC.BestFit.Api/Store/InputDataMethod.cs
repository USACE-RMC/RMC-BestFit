namespace RMC.BestFit.Api.Store
{
    /// <summary>
    /// Identifies how an input-data resource's exact series was created.
    /// </summary>
    public enum InputDataMethod
    {
        /// <summary>
        /// Observations were supplied directly by the client.
        /// </summary>
        Manual,

        /// <summary>
        /// Annual (or other time-block) maxima were extracted from a linked time-series resource.
        /// </summary>
        BlockMaxima,

        /// <summary>
        /// Independent peaks exceeding a threshold were extracted from a linked time-series resource.
        /// </summary>
        PeaksOverThreshold,

        /// <summary>
        /// Annual peak discharge values were downloaded directly from the USGS peak-flow file.
        /// </summary>
        UsgsPeakDischarge,

        /// <summary>
        /// Annual peak stage values were downloaded directly from the USGS peak-flow file.
        /// </summary>
        UsgsPeakStage
    }
}
