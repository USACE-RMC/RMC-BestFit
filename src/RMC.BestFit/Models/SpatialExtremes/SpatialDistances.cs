using Numerics;

namespace RMC.BestFit.Models.SpatialExtremes
{
    /// <summary>
    /// Distance computations of the spatial GEV network for the supported <see cref="SpatialDistanceMetric"/> values.
    /// </summary>
    /// <remarks>
    /// The Cartesian metric delegates to Numerics <c>Tools.Distance</c> (planar Euclidean), so projected
    /// networks are computed exactly as before the metric option existed. The geodesic metric uses the
    /// haversine great-circle formula with the mean Earth radius 6371.0088 km.
    /// </remarks>
    internal static class SpatialDistances
    {
        /// <summary>
        /// The mean Earth radius in kilometres used by the geodesic metric.
        /// </summary>
        internal const double EarthRadiusKilometres = 6371.0088;

        /// <summary>
        /// Computes the separation between two points under a distance metric.
        /// </summary>
        /// <param name="metric">The distance metric.</param>
        /// <param name="first1">First coordinate of the first point (X, or latitude in degrees).</param>
        /// <param name="first2">Second coordinate of the first point (Y, or longitude in degrees).</param>
        /// <param name="second1">First coordinate of the second point.</param>
        /// <param name="second2">Second coordinate of the second point.</param>
        /// <returns>The separation in the coordinate unit (Cartesian) or in kilometres (geodesic).</returns>
        internal static double Distance(SpatialDistanceMetric metric, double first1, double first2, double second1, double second2)
        {
            if (metric == SpatialDistanceMetric.Geodesic)
                return Haversine(first1, first2, second1, second2);
            return Tools.Distance(first1, first2, second1, second2);
        }

        /// <summary>
        /// Computes the great-circle distance in kilometres between two (latitude, longitude) points in decimal degrees.
        /// </summary>
        /// <param name="latitude1">Latitude of the first point.</param>
        /// <param name="longitude1">Longitude of the first point.</param>
        /// <param name="latitude2">Latitude of the second point.</param>
        /// <param name="longitude2">Longitude of the second point.</param>
        /// <returns>The haversine distance in kilometres.</returns>
        internal static double Haversine(double latitude1, double longitude1, double latitude2, double longitude2)
        {
            double phi1 = latitude1 * Math.PI / 180.0;
            double phi2 = latitude2 * Math.PI / 180.0;
            double deltaPhi = (latitude2 - latitude1) * Math.PI / 180.0;
            double deltaLambda = (longitude2 - longitude1) * Math.PI / 180.0;
            double sinPhi = Math.Sin(deltaPhi / 2.0);
            double sinLambda = Math.Sin(deltaLambda / 2.0);
            double a = sinPhi * sinPhi + Math.Cos(phi1) * Math.Cos(phi2) * sinLambda * sinLambda;
            return 2.0 * EarthRadiusKilometres * Math.Asin(Math.Min(1.0, Math.Sqrt(a)));
        }

        /// <summary>
        /// Validates a coordinate matrix for a distance metric.
        /// </summary>
        /// <param name="coordinates">The coordinates [points × 2].</param>
        /// <param name="metric">The distance metric.</param>
        /// <param name="parameterName">The argument name reported in the exception.</param>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not finite, or when the geodesic metric
        /// receives a latitude outside ±90 degrees or a longitude outside ±180 degrees.</exception>
        internal static void ValidateCoordinates(double[,] coordinates, SpatialDistanceMetric metric, string parameterName)
        {
            for (int i = 0; i < coordinates.GetLength(0); i++)
            {
                double first = coordinates[i, 0];
                double second = coordinates[i, 1];
                if (!Tools.IsFinite(first) || !Tools.IsFinite(second))
                    throw new ArgumentException($"Coordinate {i + 1} is not finite.", parameterName);
                if (metric == SpatialDistanceMetric.Geodesic && (Math.Abs(first) > 90.0 || Math.Abs(second) > 180.0))
                {
                    throw new ArgumentException(
                        $"Coordinate {i + 1} ({first}, {second}) is not a (latitude, longitude) pair in decimal degrees; the geodesic metric requires |latitude| <= 90 and |longitude| <= 180.",
                        parameterName);
                }
            }
        }
    }
}
