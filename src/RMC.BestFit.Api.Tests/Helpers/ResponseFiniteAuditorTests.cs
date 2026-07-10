using RMC.BestFit.Api.Helpers;

namespace RMC.BestFit.Api.Tests.Helpers
{
    /// <summary>
    /// Unit tests for <see cref="ResponseFiniteAuditor"/>: infinity detection in nested objects,
    /// arrays, and dictionaries; NaN tolerance; and cycle safety.
    /// </summary>
    [TestClass]
    public class ResponseFiniteAuditorTests
    {
        /// <summary>
        /// A small DTO graph for auditing, with nested object, arrays, and dictionary members.
        /// </summary>
        private sealed class Node
        {
            /// <summary>
            /// A scalar double member.
            /// </summary>
            public double Scalar { get; set; }

            /// <summary>
            /// A one-dimensional array member.
            /// </summary>
            public double[]? Values { get; set; }

            /// <summary>
            /// A two-dimensional array member.
            /// </summary>
            public double[,]? Grid { get; set; }

            /// <summary>
            /// A dictionary member.
            /// </summary>
            public Dictionary<string, double>? Map { get; set; }

            /// <summary>
            /// A child node (used to build cycles).
            /// </summary>
            public Node? Child { get; set; }

            /// <summary>
            /// A date-time member (regression: DateTime.Date chains must not recurse infinitely).
            /// </summary>
            public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

            /// <summary>
            /// A nullable date-time member.
            /// </summary>
            public DateTime? StartDate { get; set; } = DateTime.UtcNow;
        }

        /// <summary>
        /// Verifies a fully finite graph yields no findings, and null roots are tolerated.
        /// </summary>
        [TestMethod]
        public void Audit_FiniteGraph_NoFindings()
        {
            var node = new Node { Scalar = 1d, Values = new[] { 1d, 2d }, Map = new Dictionary<string, double> { ["a"] = 3d } };
            Assert.AreEqual(0, ResponseFiniteAuditor.Audit(node).Count);
            Assert.AreEqual(0, ResponseFiniteAuditor.Audit(null).Count);
        }

        /// <summary>
        /// Verifies NaN values are intentionally NOT flagged (missing-data marker).
        /// </summary>
        [TestMethod]
        public void Audit_NaN_IsIgnored()
        {
            var node = new Node { Scalar = double.NaN, Values = new[] { double.NaN } };
            Assert.AreEqual(0, ResponseFiniteAuditor.Audit(node).Count);
        }

        /// <summary>
        /// Verifies infinities are reported with paths from scalars, 1D and 2D arrays, and dictionaries.
        /// </summary>
        [TestMethod]
        public void Audit_Infinity_ReportedWithPaths()
        {
            var node = new Node
            {
                Scalar = double.PositiveInfinity,
                Values = new[] { 1d, double.NegativeInfinity },
                Grid = new[,] { { 1d, double.PositiveInfinity } },
                Map = new Dictionary<string, double> { ["bad"] = double.PositiveInfinity }
            };

            var findings = ResponseFiniteAuditor.Audit(node);

            Assert.AreEqual(4, findings.Count);
            Assert.IsTrue(findings.Any(f => f.Contains(".Scalar")));
            Assert.IsTrue(findings.Any(f => f.Contains(".Values[1]")));
            Assert.IsTrue(findings.Any(f => f.Contains(".Grid[0,1]")));
            Assert.IsTrue(findings.Any(f => f.Contains(".Map[bad]")));
        }

        /// <summary>
        /// Verifies reference cycles do not cause infinite recursion.
        /// </summary>
        [TestMethod]
        public void Audit_Cycle_Terminates()
        {
            var a = new Node { Scalar = double.PositiveInfinity };
            var b = new Node { Child = a };
            a.Child = b;
            var findings = ResponseFiniteAuditor.Audit(a);
            Assert.AreEqual(1, findings.Count);
        }

        /// <summary>
        /// Regression: DateTime members must be treated as leaves. DateTime.Date returns a fresh
        /// DateTime whose Date returns another, so walking its properties recurses without bound
        /// (the reference-identity cycle guard cannot break value-type chains).
        /// </summary>
        [TestMethod]
        public void Audit_DateTimeMembers_TerminateWithoutFindings()
        {
            var node = new Node { Scalar = 1d };
            var findings = ResponseFiniteAuditor.Audit(node);
            Assert.AreEqual(0, findings.Count);
        }
    }
}
