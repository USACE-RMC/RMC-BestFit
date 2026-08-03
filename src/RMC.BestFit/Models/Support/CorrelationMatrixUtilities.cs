using Numerics;
using Numerics.Mathematics;
using Numerics.Mathematics.LinearAlgebra;
using System.Globalization;
using System.Xml.Linq;

namespace RMC.BestFit.Models
{
    /// <summary>
    /// Provides shared validation, ownership, and XML helpers for correlation matrices.
    /// </summary>
    /// <remarks>
    /// Correlation matrices used by competing-risk Gaussian copulas must be finite,
    /// symmetric, positive definite, and dimensionally compatible with their marginals.
    /// </remarks>
    internal static class CorrelationMatrixUtilities
    {
        /// <summary>
        /// Tolerance used by the established Numerics competing-risk XML contract for
        /// unit-diagonal and symmetry checks.
        /// </summary>
        private const double MatrixTolerance = 1E-12;

        /// <summary>
        /// Creates an owned copy of a correlation matrix.
        /// </summary>
        /// <param name="matrix">The matrix to copy.</param>
        /// <returns>A cloned matrix, or <see langword="null"/> when the input is null.</returns>
        internal static double[,]? Clone(double[,]? matrix)
        {
            return matrix == null ? null : (double[,])matrix.Clone();
        }

        /// <summary>
        /// Determines whether two optional matrices contain exactly the same values.
        /// </summary>
        /// <param name="left">The first matrix.</param>
        /// <param name="right">The second matrix.</param>
        /// <returns><see langword="true"/> when both matrices are null or exactly equal.</returns>
        internal static bool AreEqual(double[,]? left, double[,]? right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left == null || right == null) return false;
            if (left.GetLength(0) != right.GetLength(0) || left.GetLength(1) != right.GetLength(1)) return false;

            for (int row = 0; row < left.GetLength(0); row++)
            {
                for (int column = 0; column < left.GetLength(1); column++)
                {
                    if (left[row, column] != right[row, column]) return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Validates a correlation matrix without modifying it.
        /// </summary>
        /// <param name="matrix">The matrix to validate.</param>
        /// <param name="expectedDimension">The required dimension, or null when only structural validation is needed.</param>
        /// <param name="required">Whether a null matrix is invalid.</param>
        /// <param name="error">The validation error when validation fails.</param>
        /// <returns><see langword="true"/> when the matrix satisfies the requested contract.</returns>
        internal static bool TryValidate(
            double[,]? matrix,
            int? expectedDimension,
            bool required,
            out string? error)
        {
            error = null;
            if (matrix == null)
            {
                if (required)
                {
                    error = "A correlation-matrix dependency requires a correlation matrix.";
                    return false;
                }

                return true;
            }

            int rows = matrix.GetLength(0);
            int columns = matrix.GetLength(1);
            if (rows == 0 || rows != columns)
            {
                error = "The correlation matrix must be non-empty and square.";
                return false;
            }
            if (expectedDimension.HasValue && rows != expectedDimension.Value)
            {
                error = "The correlation matrix dimensions must match the number of components.";
                return false;
            }

            for (int row = 0; row < rows; row++)
            {
                if (Math.Abs(matrix[row, row] - 1d) > MatrixTolerance)
                {
                    error = "The correlation matrix must have unit diagonal entries.";
                    return false;
                }

                for (int column = 0; column < columns; column++)
                {
                    double value = matrix[row, column];
                    if (!Tools.IsFinite(value) || value < -1d || value > 1d)
                    {
                        error = "The correlation matrix must contain finite values between -1 and 1.";
                        return false;
                    }
                    if (column > row && Math.Abs(value - matrix[column, row]) > MatrixTolerance)
                    {
                        error = "The correlation matrix must be symmetric.";
                        return false;
                    }
                }
            }

            try
            {
                var cholesky = new CholeskyDecomposition(new Matrix(matrix));
                if (!cholesky.IsPositiveDefinite)
                {
                    error = "The correlation matrix must be positive definite.";
                    return false;
                }
            }
            catch (Exception exception)
            {
                error = $"The correlation matrix must be positive definite. {exception.Message}";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Serializes a correlation matrix using the established Numerics row format.
        /// </summary>
        /// <param name="matrix">The matrix to serialize.</param>
        /// <returns>An XML element containing invariant-culture matrix rows.</returns>
        internal static XElement ToXElement(double[,] matrix)
        {
            var element = new XElement("CorrelationMatrix");
            for (int row = 0; row < matrix.GetLength(0); row++)
            {
                var entries = new string[matrix.GetLength(1)];
                for (int column = 0; column < matrix.GetLength(1); column++)
                    entries[column] = matrix[row, column].ToString("G17", CultureInfo.InvariantCulture);
                element.Add(new XElement("Correlation_Row", string.Join("|", entries)));
            }

            return element;
        }

        /// <summary>
        /// Deserializes and validates a correlation matrix from the established Numerics row format.
        /// </summary>
        /// <param name="element">The correlation-matrix XML element.</param>
        /// <returns>The validated matrix.</returns>
        /// <exception cref="ArgumentException">Thrown when the serialized matrix is malformed or invalid.</exception>
        internal static double[,] FromXElement(XElement element)
        {
            ArgumentNullException.ThrowIfNull(element);
            XElement[] rows = element.Elements("Correlation_Row").ToArray();
            if (rows.Length == 0)
                throw new ArgumentException("The serialized correlation matrix must contain at least one row.", nameof(element));

            var matrix = new double[rows.Length, rows.Length];
            for (int row = 0; row < rows.Length; row++)
            {
                string[] entries = rows[row].Value.Split('|');
                if (entries.Length != rows.Length)
                    throw new ArgumentException("The serialized correlation matrix must be square.", nameof(element));

                for (int column = 0; column < entries.Length; column++)
                {
                    if (!double.TryParse(entries[column], NumberStyles.Any, CultureInfo.InvariantCulture, out matrix[row, column]))
                        throw new ArgumentException("The serialized correlation matrix contains an invalid value.", nameof(element));
                }
            }

            if (!TryValidate(matrix, null, true, out string? error))
                throw new ArgumentException(error, nameof(element));

            return matrix;
        }
    }
}
