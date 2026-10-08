using RMC.BestFit.TestCommon;

namespace RMC.BestFit.Tests.DistributionFitting;

/// <summary>Checks complete covariance propagation into recovery-test coordinates.</summary>
[TestClass]
public class CovarianceCoordinateTransformTests
{
    /// <summary>Checks Pearson transformation diagonals and nonzero cross terms.</summary>
    [TestMethod]
    public void PearsonMomentToMle_PropagatesCompleteCovariance()
    {
        double[,] actual = CovarianceCoordinateTransforms.PearsonMomentToMle(
            new[,] { { 4d, 1d, 0.5d }, { 1d, 9d, 2d }, { 0.5d, 2d, 16d } },
            2d,
            0.5d);

        AssertMatrixEquals(
            new[,] { { 4d, -3d, -32d }, { -3d, 281d, 4224d }, { -32d, 4224d, 65536d } },
            actual);
    }

    /// <summary>Checks LnNormal transformation diagonals and nonzero cross terms.</summary>
    [TestMethod]
    public void LnNormalPhysicalToLog_PropagatesCompleteCovariance()
    {
        double[,] actual = CovarianceCoordinateTransforms.LnNormalPhysicalToLog(
            new[,] { { 4d, 1.5d }, { 1.5d, 9d } },
            3d,
            2d);

        AssertMatrixEquals(
            new[,] { { 0.771860618014464d, -0.535174227481920d }, { -0.535174227481920d, 0.831032215647600d } },
            actual);
    }

    /// <summary>Asserts elementwise equality for a small deterministic covariance fixture.</summary>
    /// <param name="expected">Hand-derived expected matrix.</param>
    /// <param name="actual">Computed matrix.</param>
    private static void AssertMatrixEquals(double[,] expected, double[,] actual)
    {
        Assert.AreEqual(expected.GetLength(0), actual.GetLength(0));
        Assert.AreEqual(expected.GetLength(1), actual.GetLength(1));
        for (int row = 0; row < expected.GetLength(0); row++)
        {
            for (int column = 0; column < expected.GetLength(1); column++)
            {
                Assert.AreEqual(expected[row, column], actual[row, column], 1e-12, $"Mismatch at ({row}, {column}).");
            }
        }
    }
}
