using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Globalization;

namespace RMC.BestFit.App.Tests.GUI.Support
{
    /// <summary>
    /// Unit tests for <see cref="RMC_BestFit.BatchRunStatusToIconConverter"/>.
    /// </summary>
    /// <remarks>
    /// Tests cover the input-type guard, resource-key mapping, and the one-way
    /// <c>ConvertBack</c> contract without requiring a WPF <c>Application</c>.
    /// </remarks>
    [TestClass]
    public class BatchRunStatusToIconConverterTests
    {
        /// <summary>
        /// Verifies <see cref="RMC_BestFit.BatchRunStatusToIconConverter.Convert"/> returns
        /// <c>null</c> for any value that is not a <see cref="RMC_BestFit.BatchRunStatus"/>.
        /// This branch is exercised before any <c>Application.Current</c> access, so it works
        /// in unit-test contexts without a WPF Application.
        /// </summary>
        [TestMethod]
        public void Convert_NonStatusValue_ReturnsNull()
        {
            var converter = new RMC_BestFit.BatchRunStatusToIconConverter();
            object result = converter.Convert("not a status", typeof(object), null, CultureInfo.InvariantCulture);
            Assert.IsNull(result);
        }

        /// <summary>
        /// Verifies <c>Convert(null, ...)</c> returns <c>null</c> via the type-pattern guard.
        /// </summary>
        [TestMethod]
        public void Convert_NullValue_ReturnsNull()
        {
            var converter = new RMC_BestFit.BatchRunStatusToIconConverter();
            object result = converter.Convert(null, typeof(object), null, CultureInfo.InvariantCulture);
            Assert.IsNull(result);
        }

        /// <summary>
        /// Verifies <c>Convert</c> returns <c>null</c> for an integer that is not a defined
        /// <see cref="RMC_BestFit.BatchRunStatus"/> enum value (boxes as int, fails the type check).
        /// </summary>
        [TestMethod]
        public void Convert_IntValue_ReturnsNull()
        {
            var converter = new RMC_BestFit.BatchRunStatusToIconConverter();
            object result = converter.Convert(42, typeof(object), null, CultureInfo.InvariantCulture);
            Assert.IsNull(result);
        }

        /// <summary>
        /// Verifies each valid <see cref="RMC_BestFit.BatchRunStatus"/> maps to the expected
        /// application resource key.
        /// </summary>
        /// <param name="status">The status to map.</param>
        /// <param name="expected">The expected icon resource key.</param>
        [DataTestMethod]
        [DataRow(RMC_BestFit.BatchRunStatus.Pending, "StatusPendingIcon")]
        [DataRow(RMC_BestFit.BatchRunStatus.Running, "StatusRunningIcon")]
        [DataRow(RMC_BestFit.BatchRunStatus.Succeeded, "StatusSucceededIcon")]
        [DataRow(RMC_BestFit.BatchRunStatus.Failed, "StatusFailedIcon")]
        [DataRow(RMC_BestFit.BatchRunStatus.Canceled, "StatusCanceledIcon")]
        public void ForStatus_ValidStatus_ReturnsResourceKey(RMC_BestFit.BatchRunStatus status, string expected)
        {
            Assert.AreEqual(expected, RMC_BestFit.BatchRunStatusIconKeys.ForStatus(status));
        }

        /// <summary>
        /// Verifies unknown enum values are rejected by the pure resource-key mapper.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void ForStatus_UnknownStatus_Throws()
        {
            RMC_BestFit.BatchRunStatusIconKeys.ForStatus((RMC_BestFit.BatchRunStatus)999);
        }

        /// <summary>
        /// Verifies <see cref="RMC_BestFit.BatchRunStatusToIconConverter.ConvertBack"/> always
        /// throws <see cref="NotSupportedException"/> (one-way converter contract).
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(NotSupportedException))]
        public void ConvertBack_AlwaysThrowsNotSupported()
        {
            var converter = new RMC_BestFit.BatchRunStatusToIconConverter();
            converter.ConvertBack("anything", typeof(object), null, CultureInfo.InvariantCulture);
        }
    }
}
