using Microsoft.VisualStudio.TestTools.UnitTesting;
using Numerics.Data;
using RMC_BestFit;
using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace RMC.BestFit.App.Tests.GUI
{
    /// <summary>
    /// Unit tests for regular time-series row edit warning decisions.
    /// </summary>
    /// <remarks>
    /// These tests exercise the helper logic directly so the row-edit guardrails remain covered
    /// without automating modal WPF message boxes.
    /// </remarks>
    [TestClass]
    public class TimeSeriesControlRowEditWarningTests
    {
        /// <summary>
        /// Verifies valid row deletion from a regular time series requires confirmation.
        /// </summary>
        [TestMethod]
        public void ShouldConfirmRegularRowDelete_RegularSeriesWithValidRow_ReturnsTrue()
        {
            bool shouldConfirm = TimeSeriesControl.ShouldConfirmRegularRowDelete(
                TimeInterval.OneDay,
                10,
                new[] { 2, 4 });

            Assert.IsTrue(shouldConfirm);
        }

        /// <summary>
        /// Verifies row deletion from an irregular time series does not require confirmation.
        /// </summary>
        [TestMethod]
        public void ShouldConfirmRegularRowDelete_IrregularSeries_ReturnsFalse()
        {
            bool shouldConfirm = TimeSeriesControl.ShouldConfirmRegularRowDelete(
                TimeInterval.Irregular,
                10,
                new[] { 2, 4 });

            Assert.IsFalse(shouldConfirm);
        }

        /// <summary>
        /// Verifies invalid delete indices do not require confirmation unless a valid index is included.
        /// </summary>
        [TestMethod]
        public void ShouldConfirmRegularRowDelete_InvalidIndicesOnly_ReturnsFalse()
        {
            bool shouldConfirm = TimeSeriesControl.ShouldConfirmRegularRowDelete(
                TimeInterval.OneDay,
                10,
                new[] { -1, 10, 11 });

            Assert.IsFalse(shouldConfirm);
        }

        /// <summary>
        /// Verifies mixed valid and invalid delete indices require confirmation for a regular time series.
        /// </summary>
        [TestMethod]
        public void ShouldConfirmRegularRowDelete_MixedValidAndInvalidIndices_ReturnsTrue()
        {
            bool shouldConfirm = TimeSeriesControl.ShouldConfirmRegularRowDelete(
                TimeInterval.OneDay,
                10,
                new[] { -1, 9, 10 });

            Assert.IsTrue(shouldConfirm);
        }

        /// <summary>
        /// Verifies inserting before the end of a regular time series requires confirmation.
        /// </summary>
        [TestMethod]
        public void ShouldConfirmRegularRowInsert_RegularSeriesBeforeEnd_ReturnsTrue()
        {
            bool shouldConfirm = TimeSeriesControl.ShouldConfirmRegularRowInsert(
                TimeInterval.OneDay,
                10,
                5,
                2);

            Assert.IsTrue(shouldConfirm);
        }

        /// <summary>
        /// Verifies inserting at the first row of a regular time series requires confirmation.
        /// </summary>
        [TestMethod]
        public void ShouldConfirmRegularRowInsert_RegularSeriesAtFirstRow_ReturnsTrue()
        {
            bool shouldConfirm = TimeSeriesControl.ShouldConfirmRegularRowInsert(
                TimeInterval.OneDay,
                10,
                0,
                1);

            Assert.IsTrue(shouldConfirm);
        }

        /// <summary>
        /// Verifies appending to a regular time series does not require confirmation.
        /// </summary>
        [TestMethod]
        public void ShouldConfirmRegularRowInsert_RegularSeriesAtEnd_ReturnsFalse()
        {
            bool shouldConfirm = TimeSeriesControl.ShouldConfirmRegularRowInsert(
                TimeInterval.OneDay,
                10,
                10,
                2);

            Assert.IsFalse(shouldConfirm);
        }

        /// <summary>
        /// Verifies adding rows to an empty regular time series does not require confirmation.
        /// </summary>
        [TestMethod]
        public void ShouldConfirmRegularRowInsert_EmptyRegularSeries_ReturnsFalse()
        {
            bool shouldConfirm = TimeSeriesControl.ShouldConfirmRegularRowInsert(
                TimeInterval.OneDay,
                0,
                0,
                2);

            Assert.IsFalse(shouldConfirm);
        }

        /// <summary>
        /// Verifies inserting into an irregular time series does not require confirmation.
        /// </summary>
        [TestMethod]
        public void ShouldConfirmRegularRowInsert_IrregularSeries_ReturnsFalse()
        {
            bool shouldConfirm = TimeSeriesControl.ShouldConfirmRegularRowInsert(
                TimeInterval.Irregular,
                10,
                5,
                2);

            Assert.IsFalse(shouldConfirm);
        }

        /// <summary>
        /// Verifies zero-row insert requests do not require confirmation.
        /// </summary>
        [TestMethod]
        public void ShouldConfirmRegularRowInsert_ZeroRows_ReturnsFalse()
        {
            bool shouldConfirm = TimeSeriesControl.ShouldConfirmRegularRowInsert(
                TimeInterval.OneDay,
                10,
                5,
                0);

            Assert.IsFalse(shouldConfirm);
        }

        /// <summary>
        /// Verifies negative insert indices do not require confirmation.
        /// </summary>
        [TestMethod]
        public void ShouldConfirmRegularRowInsert_NegativeStartIndex_ReturnsFalse()
        {
            bool shouldConfirm = TimeSeriesControl.ShouldConfirmRegularRowInsert(
                TimeInterval.OneDay,
                10,
                -1,
                1);

            Assert.IsFalse(shouldConfirm);
        }

        /// <summary>
        /// Verifies the delete warning explains date recomputation and the missing-value alternative.
        /// </summary>
        [TestMethod]
        public void RegularRowDeleteWarningMessage_ExplainsDateRecomputationAndMissingValueAlternative()
        {
            StringAssert.Contains(TimeSeriesControl.RegularRowDeleteWarningMessage, "recompute the Date Time column");
            StringAssert.Contains(TimeSeriesControl.RegularRowDeleteWarningMessage, "clear the selected Value cells");
            StringAssert.Contains(TimeSeriesControl.RegularRowDeleteWarningTitle, "Delete Rows");
        }

        /// <summary>
        /// Verifies the insert warning explains date recomputation and the append alternative.
        /// </summary>
        [TestMethod]
        public void RegularRowInsertWarningMessage_ExplainsDateRecomputationAndAppendAlternative()
        {
            StringAssert.Contains(TimeSeriesControl.RegularRowInsertWarningMessage, "recompute the Date Time column");
            StringAssert.Contains(TimeSeriesControl.RegularRowInsertWarningMessage, "Add Row(s)");
            StringAssert.Contains(TimeSeriesControl.RegularRowInsertWarningTitle, "Insert Rows");
        }

        /// <summary>
        /// Verifies the row-edit confirmations use the shared application message box implementation.
        /// </summary>
        [TestMethod]
        public void RegularRowEditConfirmations_UseGenericControlsMessageBox()
        {
            string source = ReadTimeSeriesControlSource();

            Assert.AreEqual(2, CountOccurrences(source, "GenericControls.MessageBox.Show("));
            Assert.IsFalse(source.Contains("System.Windows.MessageBox.Show(", StringComparison.Ordinal));
        }

        /// <summary>
        /// Counts non-overlapping occurrences of a pattern.
        /// </summary>
        /// <param name="source">The source text to inspect.</param>
        /// <param name="pattern">The pattern to count.</param>
        /// <returns>The number of occurrences.</returns>
        private static int CountOccurrences(string source, string pattern)
        {
            return source
                .Split(new[] { pattern }, StringSplitOptions.None)
                .Length - 1;
        }

        /// <summary>
        /// Reads the TimeSeriesControl C# source file.
        /// </summary>
        /// <param name="sourceFilePath">The compiler-provided source path for this test file.</param>
        /// <returns>The source text for <c>TimeSeriesControl.xaml.cs</c>.</returns>
        private static string ReadTimeSeriesControlSource([CallerFilePath] string sourceFilePath = "")
        {
            return File.ReadAllText(Path.Combine(
                FindRepoRoot(sourceFilePath),
                "src",
                "RMC.BestFit.App",
                "GUI",
                "TimeSeriesData",
                "TimeSeries",
                "TimeSeriesControl.xaml.cs"));
        }

        /// <summary>
        /// Finds the repository root from the test source directory.
        /// </summary>
        /// <param name="sourceFilePath">The compiler-provided source path for this test file.</param>
        /// <returns>The absolute repository root.</returns>
        /// <exception cref="DirectoryNotFoundException">Thrown when the repository root cannot be found.</exception>
        private static string FindRepoRoot(string sourceFilePath)
        {
            DirectoryInfo directory = new DirectoryInfo(Path.GetDirectoryName(sourceFilePath) ?? Directory.GetCurrentDirectory());
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "RMC.BestFit.sln")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException("Could not locate the RMC.BestFit repository root.");
        }
    }
}
