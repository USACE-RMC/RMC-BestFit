namespace RMC.BestFit.Models.SpatialExtremes
{
    /// <summary>
    /// The distance metric of a spatial GEV network: how the site coordinates are turned into the
    /// separations that the correlation functions, latent-error covariances, and kriging use.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="Cartesian"/> is the historical behavior: planar Euclidean distance between projected
    /// coordinates, in whatever linear unit the coordinates carry (the default correlation-range prior
    /// Uniform(ε, 500) is then in that unit). <see cref="Geodesic"/> interprets each coordinate row as
    /// (latitude, longitude) in decimal degrees and uses the great-circle (haversine) distance in
    /// kilometres on a sphere of mean radius 6371.0088 km, so the range prior Uniform(ε, 500) is in
    /// kilometres; latitudes outside ±90° or longitudes outside ±180° are rejected.
    /// </para>
    /// </remarks>
    public enum SpatialDistanceMetric
    {
        /// <summary>
        /// Planar Euclidean distance between projected (X, Y) coordinates in a common linear unit.
        /// </summary>
        Cartesian = 0,

        /// <summary>
        /// Great-circle (haversine) distance in kilometres between (latitude, longitude) coordinates in decimal degrees.
        /// </summary>
        Geodesic = 1
    }
}
