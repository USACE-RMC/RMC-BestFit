using System;
using System.Collections.Generic;
using System.Globalization;

namespace RMC_BestFit
{
    /// <summary>
    /// Validates editable coincident-frequency response-surface grid cells.
    /// </summary>
    /// <remarks>
    /// The coincident-frequency grid stores response values, not probabilities. A valid
    /// cell must therefore be finite and consistent with the strictly increasing
    /// response surface required by the model-layer inversion algorithm.
    /// </remarks>
    internal static class CoincidentFrequencyResponseGridValidator
    {
        /// <summary>
        /// Converts a DataGrid/DataTable cell payload into a nullable double.
        /// </summary>
        /// <param name="value">The raw cell payload.</param>
        /// <returns>The converted double, or <c>null</c> when the cell is blank or cannot be converted.</returns>
        /// <remarks>
        /// Blank or non-convertible cells are kept distinct from numeric zero so the
        /// visual validation layer does not silently turn missing data into a valid
        /// response value.
        /// </remarks>
        internal static double? ConvertCellValue(object value)
        {
            if (value == null || value == DBNull.Value) return null;

            try
            {
                return Convert.ToDouble(value, CultureInfo.InvariantCulture);
            }
            catch (FormatException)
            {
                return null;
            }
            catch (InvalidCastException)
            {
                return null;
            }
            catch (OverflowException)
            {
                return null;
            }
        }

        /// <summary>
        /// Validates one cell in a response surface.
        /// </summary>
        /// <param name="response">The nullable response surface. A null entry means the cell is blank or non-numeric.</param>
        /// <param name="row">The zero-based row index.</param>
        /// <param name="column">The zero-based column index.</param>
        /// <returns>A validation result with a tooltip message for invalid cells.</returns>
        /// <remarks>
        /// The method only compares against finite neighboring values. Neighboring blank
        /// cells are marked invalid by their own validation result, while valid neighbors
        /// still provide strict-increase constraints for the current cell.
        /// </remarks>
        internal static ResponseGridCellValidationResult ValidateCell(double?[,] response, int row, int column)
        {
            if (response == null)
                return ResponseGridCellValidationResult.Invalid("The response surface is not available.");

            int rows = response.GetLength(0);
            int columns = response.GetLength(1);
            if (row < 0 || row >= rows || column < 0 || column >= columns)
                return ResponseGridCellValidationResult.Invalid("The response cell is outside the response surface.");

            double? maybeValue = response[row, column];
            if (!maybeValue.HasValue)
                return ResponseGridCellValidationResult.Invalid("The response value is required.");

            double value = maybeValue.Value;
            if (!IsFinite(value))
                return ResponseGridCellValidationResult.Invalid("The response value must be finite.");

            var messages = new List<string>();
            if (row > 0 && TryGetFinite(response[row - 1, column], out double previousX) && !(value > previousX))
                messages.Add("greater than the previous X-row value");
            if (row < rows - 1 && TryGetFinite(response[row + 1, column], out double nextX) && !(value < nextX))
                messages.Add("less than the next X-row value");
            if (column > 0 && TryGetFinite(response[row, column - 1], out double previousY) && !(value > previousY))
                messages.Add("greater than the previous Y-column value");
            if (column < columns - 1 && TryGetFinite(response[row, column + 1], out double nextY) && !(value < nextY))
                messages.Add("less than the next Y-column value");

            if (messages.Count == 0)
                return ResponseGridCellValidationResult.Valid;

            return ResponseGridCellValidationResult.Invalid(
                "The response surface must be strictly increasing; this value must be " +
                string.Join(", ", messages) + ".");
        }

        /// <summary>
        /// Determines whether a value is finite.
        /// </summary>
        /// <param name="value">The value to inspect.</param>
        /// <returns><c>true</c> when the value is neither NaN nor infinity; otherwise, <c>false</c>.</returns>
        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        /// <summary>
        /// Tries to extract a finite value from a nullable cell.
        /// </summary>
        /// <param name="value">The nullable value.</param>
        /// <param name="finiteValue">The finite value when conversion succeeds.</param>
        /// <returns><c>true</c> when the cell has a finite value; otherwise, <c>false</c>.</returns>
        private static bool TryGetFinite(double? value, out double finiteValue)
        {
            finiteValue = value.GetValueOrDefault();
            return value.HasValue && IsFinite(finiteValue);
        }
    }

    /// <summary>
    /// Represents the validation status of an editable response-surface grid cell.
    /// </summary>
    internal readonly struct ResponseGridCellValidationResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ResponseGridCellValidationResult"/> struct.
        /// </summary>
        /// <param name="isValid">Whether the cell is valid.</param>
        /// <param name="toolTip">The tooltip message to show when the cell is invalid.</param>
        private ResponseGridCellValidationResult(bool isValid, string toolTip)
        {
            IsValid = isValid;
            ToolTip = toolTip ?? string.Empty;
        }

        /// <summary>
        /// Gets a valid cell result.
        /// </summary>
        internal static ResponseGridCellValidationResult Valid => new ResponseGridCellValidationResult(true, string.Empty);

        /// <summary>
        /// Gets a value indicating whether the cell is valid.
        /// </summary>
        internal bool IsValid { get; }

        /// <summary>
        /// Gets the tooltip message to show when the cell is invalid.
        /// </summary>
        internal string ToolTip { get; }

        /// <summary>
        /// Creates an invalid cell result.
        /// </summary>
        /// <param name="toolTip">The tooltip message to show when the cell is invalid.</param>
        /// <returns>An invalid validation result.</returns>
        internal static ResponseGridCellValidationResult Invalid(string toolTip)
        {
            return new ResponseGridCellValidationResult(false, toolTip);
        }
    }
}
