namespace RMC.BestFit.TestCommon;

/// <summary>
/// Provides deterministic covariance-coordinate transformations shared by fast and verification tests.
/// </summary>
internal static class CovarianceCoordinateTransforms
{
    /// <summary>Transforms a Pearson-family covariance from moment coordinates to MLE coordinates.</summary>
    /// <param name="covariance">Covariance in (Mu, Sigma, Gamma) coordinates.</param>
    /// <param name="sigma">Fitted standard deviation.</param>
    /// <param name="gamma">Fitted skew.</param>
    /// <returns>Covariance in (Mu, 1/Beta, Alpha) coordinates.</returns>
    internal static double[,] PearsonMomentToMle(double[,] covariance, double sigma, double gamma)
    {
        double[,] jacobian =
        {
            { 1d, 0d, 0d },
            { 0d, -2d / (sigma * sigma * gamma), -2d / (sigma * gamma * gamma) },
            { 0d, 0d, -8d / (gamma * gamma * gamma) }
        };
        return Transform(covariance, jacobian);
    }

    /// <summary>Transforms an LnNormal covariance from physical to logarithmic coordinates.</summary>
    /// <param name="covariance">Covariance in physical (Mean, StandardDeviation) coordinates.</param>
    /// <param name="mean">Fitted physical mean.</param>
    /// <param name="standardDeviation">Fitted physical standard deviation.</param>
    /// <returns>Covariance in (Mu, SigmaSquared) coordinates.</returns>
    internal static double[,] LnNormalPhysicalToLog(double[,] covariance, double mean, double standardDeviation)
    {
        double squaredMean = mean * mean;
        double squaredStandardDeviation = standardDeviation * standardDeviation;
        double denominator = squaredMean + squaredStandardDeviation;
        double[,] jacobian =
        {
            { (squaredMean + 2d * squaredStandardDeviation) / (mean * denominator), -standardDeviation / denominator },
            { -2d * squaredStandardDeviation / (mean * denominator), 2d * standardDeviation / denominator }
        };
        return Transform(covariance, jacobian);
    }

    /// <summary>Computes the complete first-order covariance transformation J C J-transpose.</summary>
    /// <param name="covariance">Covariance in the source coordinates.</param>
    /// <param name="jacobian">Jacobian from source to target coordinates.</param>
    /// <returns>Covariance in the target coordinates.</returns>
    private static double[,] Transform(double[,] covariance, double[,] jacobian)
    {
        int targetCount = jacobian.GetLength(0);
        int sourceCount = jacobian.GetLength(1);
        var transformed = new double[targetCount, targetCount];
        for (int row = 0; row < targetCount; row++)
        {
            for (int column = 0; column < targetCount; column++)
            {
                double value = 0d;
                for (int left = 0; left < sourceCount; left++)
                {
                    for (int right = 0; right < sourceCount; right++)
                    {
                        value += jacobian[row, left] * covariance[left, right] * jacobian[column, right];
                    }
                }
                transformed[row, column] = value;
            }
        }
        return transformed;
    }
}
