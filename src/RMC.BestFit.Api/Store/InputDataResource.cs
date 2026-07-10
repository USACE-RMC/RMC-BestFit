using Numerics.Data;
using RMC.BestFit.Models;

namespace RMC.BestFit.Api.Store
{
    /// <summary>
    /// A server-side input-data resource wrapping a model-layer <see cref="RMC.BestFit.Models.DataFrame"/>
    /// together with provenance describing how the exact series was produced (manual entry,
    /// block maxima or peaks-over-threshold from a linked time series, or a direct USGS peak download).
    /// </summary>
    public class InputDataResource : ResourceBase
    {
        /// <summary>
        /// The model-layer data frame holding the exact / interval / threshold series and the
        /// plotting-position and record-length settings. Treated as immutable after creation;
        /// analyses clone it when they are created.
        /// </summary>
        public required DataFrame DataFrame { get; init; }

        /// <summary>
        /// How the exact series was created.
        /// </summary>
        public required InputDataMethod Method { get; init; }

        /// <summary>
        /// The id of the time-series resource the exact series was derived from, when
        /// <see cref="Method"/> is block maxima or peaks-over-threshold. The referenced resource
        /// may since have been deleted; the id is provenance only.
        /// </summary>
        public Guid? SourceTimeSeriesId { get; init; }

        /// <summary>
        /// The USGS site number the peak data was downloaded from, when <see cref="Method"/> is a
        /// direct USGS peak download.
        /// </summary>
        public string? UsgsSiteNumber { get; init; }

        /// <summary>
        /// The time-block window used for block-maxima extraction (e.g., water year), when applicable.
        /// </summary>
        public TimeBlockWindow? TimeBlock { get; init; }

        /// <summary>
        /// The block function (maximum, minimum, ...) used for block-maxima extraction, when applicable.
        /// </summary>
        public BlockFunctionType? BlockFunction { get; init; }

        /// <summary>
        /// The smoothing function applied to the source series before extraction, when applicable.
        /// </summary>
        public SmoothingFunctionType? SmoothingFunction { get; init; }

        /// <summary>
        /// The starting month of the custom (or water) year window, when applicable.
        /// </summary>
        public int? StartMonth { get; init; }

        /// <summary>
        /// The ending month of the custom year window, when applicable.
        /// </summary>
        public int? EndMonth { get; init; }

        /// <summary>
        /// The smoothing period (in time steps) applied to the source series, when applicable.
        /// </summary>
        public int? Period { get; init; }

        /// <summary>
        /// The threshold value used for peaks-over-threshold extraction, when applicable.
        /// </summary>
        public double? Threshold { get; init; }

        /// <summary>
        /// The minimum number of time steps required between independent peaks for
        /// peaks-over-threshold extraction, when applicable.
        /// </summary>
        public int? MinStepsBetweenPeaks { get; init; }
    }
}
