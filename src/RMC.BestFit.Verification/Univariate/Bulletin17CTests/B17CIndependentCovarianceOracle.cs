namespace RMC.BestFit.Verification.Univariate.Bulletin17CTests;

/// <summary>Identifies a complete-data family in the independent Bulletin 17C covariance derivation.</summary>
internal enum B17CCovarianceFamily
{
    /// <summary>Two-parameter shifted Exponential in natural response space.</summary>
    Exponential,

    /// <summary>Two-parameter Gamma in `(Theta scale, Kappa shape)` coordinates.</summary>
    Gamma,

    /// <summary>Two-parameter Normal in natural response space.</summary>
    Normal,

    /// <summary>Three-parameter Pearson Type III in natural response space.</summary>
    PearsonTypeIII,

    /// <summary>Two-parameter Normal applied to base-10 logarithms.</summary>
    LogNormal,

    /// <summary>Three-parameter Pearson Type III applied to base-10 logarithms.</summary>
    LogPearsonTypeIII
}

/// <summary>Implements a source-backed complete-data GMM covariance oracle independently of production code.</summary>
/// <remarks>
/// The derivation uses the just-identified sandwich `D^-1 S D^-T / N`, published central moments,
/// and the B17C centered-moment Bessel factors. It calls neither RMC.BestFit nor Numerics covariance,
/// moment-conversion, numerical-differentiation, or matrix routines. References: England et al.
/// (2019), Bulletin 17C version 1.1, and Cohn, Lane, and Stedinger (2001), WRR 37(6), 1695-1706.
/// </remarks>
internal static class B17CIndependentCovarianceOracle
{
    /// <summary>Evaluates the independent sandwich at a fitted vector and its actual complete sample.</summary>
    /// <param name="family">The declared family and coordinate system.</param>
    /// <param name="parameters">The fitted parameter vector in the declared order.</param>
    /// <param name="observations">Raw observations; log families are transformed internally with `log10`.</param>
    /// <returns>The independently calculated finite-sample sandwich covariance.</returns>
    internal static double[,] Evaluate(
        B17CCovarianceFamily family,
        IReadOnlyList<double> parameters,
        IReadOnlyList<double> observations)
    {
        int sampleSize = observations.Count;
        if (sampleSize < 3)
            throw new ArgumentOutOfRangeException(nameof(observations), "At least three complete observations are required.");

        double[] sample = family is B17CCovarianceFamily.LogNormal or B17CCovarianceFamily.LogPearsonTypeIII
            ? observations.Select(Math.Log10).ToArray()
            : observations.ToArray();
        FamilyMoments(family, parameters, out double mean, out double variance, out double third,
            out double fourth, out double fifth, out double sixth);
        double sampleMean = sample.Average();
        double sampleSecond = sample.Average(value => Math.Pow(value - mean, 2d));
        double sampleThird = sample.Average(value => Math.Pow(value - mean, 3d));
        double c2 = sampleSize / (double)(sampleSize - 1);
        double c3 = sampleSize * sampleSize / (double)((sampleSize - 1) * (sampleSize - 2));
        double[] momentMean =
        [
            sampleMean - mean,
            c2 * sampleSecond - variance,
            c3 * sampleThird - third
        ];
        double[,] jacobian = Jacobian(family, parameters, sampleMean - mean, sampleSecond, c2, c3);
        double[,] momentCovariance = MomentCovariance(
            parameters.Count,
            variance,
            third,
            fourth,
            fifth,
            sixth,
            momentMean);
        return Sandwich(jacobian, momentCovariance, sampleSize);
    }

    /// <summary>Evaluates the ideal moment-solution sandwich used by the frozen Python artifact.</summary>
    /// <param name="family">The declared family and coordinate system.</param>
    /// <param name="parameters">The fixed reference parameter vector.</param>
    /// <param name="sampleSize">The fixed reference sample size.</param>
    /// <returns>The analytical sandwich when all estimated moments equal their parent moments.</returns>
    internal static double[,] EvaluateAtMomentSolution(
        B17CCovarianceFamily family,
        IReadOnlyList<double> parameters,
        int sampleSize)
    {
        FamilyMoments(family, parameters, out _, out double variance, out double third,
            out double fourth, out double fifth, out double sixth);
        double c2 = sampleSize / (double)(sampleSize - 1);
        double c3 = sampleSize * sampleSize / (double)((sampleSize - 1) * (sampleSize - 2));
        double[,] jacobian = Jacobian(family, parameters, 0d, variance / c2, c2, c3);
        double[,] momentCovariance = MomentCovariance(
            parameters.Count,
            variance,
            third,
            fourth,
            fifth,
            sixth,
            new double[3]);
        return Sandwich(jacobian, momentCovariance, sampleSize);
    }

    /// <summary>Returns model central moments two through six and the model mean.</summary>
    /// <param name="family">The declared family.</param>
    /// <param name="parameters">Parameters in the declared family order.</param>
    /// <param name="mean">Model mean in natural or base-10 log space.</param>
    /// <param name="variance">Second central moment.</param>
    /// <param name="third">Third central moment.</param>
    /// <param name="fourth">Fourth central moment.</param>
    /// <param name="fifth">Fifth central moment.</param>
    /// <param name="sixth">Sixth central moment.</param>
    private static void FamilyMoments(
        B17CCovarianceFamily family,
        IReadOnlyList<double> parameters,
        out double mean,
        out double variance,
        out double third,
        out double fourth,
        out double fifth,
        out double sixth)
    {
        double sigma;
        double skew;
        switch (family)
        {
            case B17CCovarianceFamily.Exponential:
                mean = parameters[0] + parameters[1];
                sigma = parameters[1];
                skew = 2d;
                break;
            case B17CCovarianceFamily.Gamma:
                mean = parameters[0] * parameters[1];
                sigma = parameters[0] * Math.Sqrt(parameters[1]);
                skew = 2d / Math.Sqrt(parameters[1]);
                break;
            case B17CCovarianceFamily.Normal:
            case B17CCovarianceFamily.LogNormal:
                mean = parameters[0];
                sigma = parameters[1];
                skew = 0d;
                break;
            case B17CCovarianceFamily.PearsonTypeIII:
            case B17CCovarianceFamily.LogPearsonTypeIII:
                mean = parameters[0];
                sigma = parameters[1];
                skew = parameters[2];
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(family));
        }

        double sigma2 = sigma * sigma;
        double sigma3 = sigma2 * sigma;
        double sigma4 = sigma2 * sigma2;
        double sigma5 = sigma4 * sigma;
        double sigma6 = sigma4 * sigma2;
        double skew2 = skew * skew;
        variance = sigma2;
        third = skew * sigma3;
        fourth = sigma4 * (3d + 1.5d * skew2);
        fifth = sigma5 * skew * (10d + 3d * skew2);
        sixth = sigma6 * (15d + 32.5d * skew2 + 7.5d * skew2 * skew2);
    }

    /// <summary>Builds the analytical finite-sample Jacobian of the centered moment conditions.</summary>
    /// <param name="family">The declared family.</param>
    /// <param name="parameters">Fitted parameters.</param>
    /// <param name="meanResidual">Sample mean minus fitted model mean.</param>
    /// <param name="sampleSecond">Sample second central moment about the fitted model mean.</param>
    /// <param name="c2">Second-moment Bessel factor.</param>
    /// <param name="c3">Third-moment Bessel factor.</param>
    /// <returns>The two- or three-coordinate moment Jacobian.</returns>
    private static double[,] Jacobian(
        B17CCovarianceFamily family,
        IReadOnlyList<double> parameters,
        double meanResidual,
        double sampleSecond,
        double c2,
        double c3)
    {
        if (family == B17CCovarianceFamily.Exponential)
        {
            double alpha = parameters[1];
            return new[,]
            {
                { -1d, -1d },
                { -2d * c2 * meanResidual, -2d * c2 * meanResidual - 2d * alpha }
            };
        }

        if (family == B17CCovarianceFamily.Gamma)
        {
            double theta = parameters[0];
            double kappa = parameters[1];
            return new[,]
            {
                { -kappa, -theta },
                {
                    -2d * c2 * meanResidual * kappa - 2d * theta * kappa,
                    -2d * c2 * meanResidual * theta - theta * theta
                }
            };
        }

        double sigma = parameters[1];
        bool threeCoordinates = family is B17CCovarianceFamily.PearsonTypeIII or B17CCovarianceFamily.LogPearsonTypeIII;
        if (!threeCoordinates)
        {
            return new[,]
            {
                { -1d, 0d },
                { -2d * c2 * meanResidual, -2d * sigma }
            };
        }

        double skew = parameters[2];
        return new[,]
        {
            { -1d, 0d, 0d },
            { -2d * c2 * meanResidual, -2d * sigma, 0d },
            { -3d * c3 * sampleSecond, -3d * skew * sigma * sigma, -sigma * sigma * sigma }
        };
    }

    /// <summary>Builds `S = E[g g'] - E[g]E[g']` from central moments.</summary>
    /// <param name="dimension">Number of fitted coordinates.</param>
    /// <param name="mu2">Second central moment.</param>
    /// <param name="mu3">Third central moment.</param>
    /// <param name="mu4">Fourth central moment.</param>
    /// <param name="mu5">Fifth central moment.</param>
    /// <param name="mu6">Sixth central moment.</param>
    /// <param name="momentMean">Mean moment-condition vector.</param>
    /// <returns>The centered moment covariance.</returns>
    private static double[,] MomentCovariance(
        int dimension,
        double mu2,
        double mu3,
        double mu4,
        double mu5,
        double mu6,
        IReadOnlyList<double> momentMean)
    {
        var covariance = new double[dimension, dimension];
        covariance[0, 0] = mu2;
        covariance[0, 1] = covariance[1, 0] = mu3;
        covariance[1, 1] = mu4 - mu2 * mu2;
        if (dimension == 3)
        {
            covariance[0, 2] = covariance[2, 0] = mu4;
            covariance[1, 2] = covariance[2, 1] = mu5 - mu2 * mu3;
            covariance[2, 2] = mu6 - mu3 * mu3;
        }

        for (int i = 0; i < dimension; i++)
        {
            for (int j = 0; j < dimension; j++)
                covariance[i, j] -= momentMean[i] * momentMean[j];
        }
        return covariance;
    }

    /// <summary>Forms `D^-1 S D^-T / N` using local dense matrix arithmetic.</summary>
    /// <param name="jacobian">Moment Jacobian.</param>
    /// <param name="momentCovariance">Moment covariance.</param>
    /// <param name="sampleSize">Complete-data sample size.</param>
    /// <returns>The independent sandwich covariance.</returns>
    private static double[,] Sandwich(double[,] jacobian, double[,] momentCovariance, int sampleSize)
    {
        double[,] inverse = Inverse(jacobian);
        double[,] covariance = Multiply(Multiply(inverse, momentCovariance), Transpose(inverse));
        int dimension = covariance.GetLength(0);
        for (int i = 0; i < dimension; i++)
        {
            for (int j = 0; j < dimension; j++)
                covariance[i, j] /= sampleSize;
        }
        return covariance;
    }

    /// <summary>Inverts a small dense matrix by Gauss-Jordan elimination with partial pivoting.</summary>
    /// <param name="matrix">Square matrix.</param>
    /// <returns>The matrix inverse.</returns>
    private static double[,] Inverse(double[,] matrix)
    {
        int dimension = matrix.GetLength(0);
        var augmented = new double[dimension, 2 * dimension];
        for (int i = 0; i < dimension; i++)
        {
            for (int j = 0; j < dimension; j++)
            {
                augmented[i, j] = matrix[i, j];
                augmented[i, dimension + j] = i == j ? 1d : 0d;
            }
        }

        for (int column = 0; column < dimension; column++)
        {
            int pivot = column;
            for (int row = column + 1; row < dimension; row++)
            {
                if (Math.Abs(augmented[row, column]) > Math.Abs(augmented[pivot, column]))
                    pivot = row;
            }
            if (Math.Abs(augmented[pivot, column]) <= 1E-30)
                throw new InvalidOperationException("The independent covariance Jacobian is singular.");
            if (pivot != column)
            {
                for (int j = 0; j < 2 * dimension; j++)
                    (augmented[column, j], augmented[pivot, j]) = (augmented[pivot, j], augmented[column, j]);
            }
            double divisor = augmented[column, column];
            for (int j = 0; j < 2 * dimension; j++)
                augmented[column, j] /= divisor;
            for (int row = 0; row < dimension; row++)
            {
                if (row == column)
                    continue;
                double multiplier = augmented[row, column];
                for (int j = 0; j < 2 * dimension; j++)
                    augmented[row, j] -= multiplier * augmented[column, j];
            }
        }

        var inverse = new double[dimension, dimension];
        for (int i = 0; i < dimension; i++)
        {
            for (int j = 0; j < dimension; j++)
                inverse[i, j] = augmented[i, dimension + j];
        }
        return inverse;
    }

    /// <summary>Multiplies two dense matrices.</summary>
    /// <param name="left">Left matrix.</param>
    /// <param name="right">Right matrix.</param>
    /// <returns>The matrix product.</returns>
    private static double[,] Multiply(double[,] left, double[,] right)
    {
        int rows = left.GetLength(0);
        int shared = left.GetLength(1);
        int columns = right.GetLength(1);
        var product = new double[rows, columns];
        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < columns; j++)
            {
                for (int k = 0; k < shared; k++)
                    product[i, j] += left[i, k] * right[k, j];
            }
        }
        return product;
    }

    /// <summary>Transposes a dense matrix.</summary>
    /// <param name="matrix">Input matrix.</param>
    /// <returns>The transposed matrix.</returns>
    private static double[,] Transpose(double[,] matrix)
    {
        var transpose = new double[matrix.GetLength(1), matrix.GetLength(0)];
        for (int i = 0; i < matrix.GetLength(0); i++)
        {
            for (int j = 0; j < matrix.GetLength(1); j++)
                transpose[j, i] = matrix[i, j];
        }
        return transpose;
    }
}
