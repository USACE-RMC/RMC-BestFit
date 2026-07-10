using RMC.BestFit.Diagnostics;
using RMC.BestFit.Models;
using System.Xml.Linq;

namespace RMC.BestFit.Tests.Diagnostics;

/// <summary>
/// Programmatic unit tests for the <c>InfluenceDiagnostics</c> class — the PSIS-LOO
/// diagnostic container exposing per-observation Pareto k values plus rolled-up summaries.
/// </summary>
/// <remarks>
/// Existing <c>ObservationInfluenceTests</c> covers the per-observation struct. This file
/// focuses on the container's responsibilities: empty-state defaults, summary-statistic
/// rollups, threshold-bucket counts, ordering helpers, reliability classification, and
/// XElement serialization. Computational tests using real MCMCResults remain in the
/// Verification project.
/// </remarks>
[TestClass]
public class InfluenceDiagnosticsTests
{
    #region Helpers

    /// <summary>
    /// Supports the <c>Obs</c> helper.
    /// </summary>
    /// <param name="index">The zero-based observation index.</param>
    /// <param name="k">The k value.</param>
    /// <param name="elpd">The elpd value.</param>
    /// <returns>The result.</returns>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static ObservationInfluence Obs(int index, double k, double elpd = -2.0)
        => new(index: index, paretoK: k, elpdLoo: elpd, value: index, dataType: DataComponentType.Exact, count: 1);

    #endregion

    #region Empty constructor

    /// <summary>
    /// Empty constructor leaves rollups as NaN (not zero) so a downstream "no observations"
    /// branch can be detected unambiguously — the design invariant is documented in the project coding standards.
    /// </summary>
    [TestMethod]
    public void DefaultConstructor_RollupsAreNaN_AndCountIsZero()
    {
        var diag = new InfluenceDiagnostics();

        Assert.AreEqual(0, diag.Count);
        Assert.IsTrue(double.IsNaN(diag.MeanParetoK));
        Assert.IsTrue(double.IsNaN(diag.MaxParetoK));
        Assert.AreEqual(0, diag.CountParetoKAbove05);
        Assert.AreEqual(0, diag.CountParetoKAbove07);
        Assert.AreEqual(0, diag.CountParetoKAbove10);
    }

    #endregion

    #region ObservationInfluence[] constructor

    /// <summary>
    /// Null observations array must throw rather than silently treated as empty — the
    /// callsites in BayesianAnalysis allocate a real array; null is always a bug.
    /// </summary>
    [TestMethod]
    public void Constructor_NullObservations_Throws()
    {
        Assert.ThrowsException<ArgumentNullException>(
            () => new InfluenceDiagnostics((ObservationInfluence[])null!));
    }

    /// <summary>
    /// Mean and Max must be computed from the observations and must skip NaN k values
    /// — otherwise a single failed Pareto fit on one obs would poison the whole rollup.
    /// </summary>
    [TestMethod]
    public void Constructor_FromObservations_ComputesSummaryStatistics()
    {
        // Mix of categories: 2 good, 1 OK, 1 bad, 1 very-bad.
        var arr = new[]
        {
            Obs(0, 0.2),  // Good
            Obs(1, 0.4),  // Good
            Obs(2, 0.6),  // OK   (>= 0.5)
            Obs(3, 0.8),  // Bad  (>= 0.7)
            Obs(4, 1.2),  // VeryBad (>= 1.0)
        };

        var diag = new InfluenceDiagnostics(arr);

        Assert.AreEqual(5, diag.Count);
        Assert.AreEqual((0.2 + 0.4 + 0.6 + 0.8 + 1.2) / 5.0, diag.MeanParetoK, 1e-12);
        Assert.AreEqual(1.2, diag.MaxParetoK, 1e-12);
        Assert.AreEqual(3, diag.CountParetoKAbove05, "k ≥ 0.5: 0.6, 0.8, 1.2");
        Assert.AreEqual(2, diag.CountParetoKAbove07, "k ≥ 0.7: 0.8, 1.2");
        Assert.AreEqual(1, diag.CountParetoKAbove10, "k ≥ 1.0: 1.2");
    }

    /// <summary>
    /// All-NaN Pareto k is a real failure mode (e.g., when all importance ratios degenerate);
    /// summary statistics must remain NaN rather than collapse to zero.
    /// </summary>
    [TestMethod]
    public void Constructor_AllNaN_LeavesRollupsAsNaN()
    {
        var arr = new[]
        {
            Obs(0, double.NaN),
            Obs(1, double.NaN),
        };

        var diag = new InfluenceDiagnostics(arr);

        Assert.IsTrue(double.IsNaN(diag.MeanParetoK));
        Assert.IsTrue(double.IsNaN(diag.MaxParetoK));
    }

    #endregion

    #region (paretoK, elpdLoo, dataComponents) constructor

    /// <summary>
    /// Length-mismatch between paretoK and elpdLoo arrays must throw — not silently truncate.
    /// </summary>
    [TestMethod]
    public void Constructor_FromArrays_LengthMismatch_Throws()
    {
        var paretoK = new[] { 0.1, 0.2, 0.3 };
        var elpd = new[] { -1.0, -2.0 };

        Assert.ThrowsException<ArgumentException>(
            () => new InfluenceDiagnostics(paretoK, elpd));
    }

    /// <summary>
    /// Null arrays in the (paretoK, elpdLoo) constructor must throw.
    /// </summary>
    [TestMethod]
    public void Constructor_FromArrays_NullArgs_Throw()
    {
        Assert.ThrowsException<ArgumentNullException>(
            () => new InfluenceDiagnostics(null!, new double[] { -1.0 }));

        Assert.ThrowsException<ArgumentNullException>(
            () => new InfluenceDiagnostics(new double[] { 0.1 }, null!));
    }

    /// <summary>
    /// (paretoK, elpdLoo) constructor produces one ObservationInfluence per index and computes
    /// the same rollup as the explicit-array constructor.
    /// </summary>
    [TestMethod]
    public void Constructor_FromArrays_BuildsObservations()
    {
        var paretoK = new[] { 0.3, 0.6, 0.8 };
        var elpd = new[] { -1.0, -1.5, -2.0 };

        var diag = new InfluenceDiagnostics(paretoK, elpd);

        Assert.AreEqual(3, diag.Count);
        for (int i = 0; i < diag.Count; i++)
        {
            Assert.AreEqual(i, diag[i].Index);
            Assert.AreEqual(paretoK[i], diag[i].ParetoK, 1e-15);
            Assert.AreEqual(elpd[i], diag[i].ElpdLoo, 1e-15);
        }
    }

    /// <summary>
    /// DataComponents-length mismatch must throw — silent length mismatch would attach
    /// the wrong DataComponent to each observation.
    /// </summary>
    [TestMethod]
    public void Constructor_FromArrays_DataComponentsLengthMismatch_Throws()
    {
        var paretoK = new[] { 0.3, 0.6 };
        var elpd = new[] { -1.0, -1.5 };
        var dataComponents = new List<DataComponent>
        {
            new(index: 0, logLikelihood: -1.0, value: 100.0),
        };

        Assert.ThrowsException<ArgumentException>(
            () => new InfluenceDiagnostics(paretoK, elpd, dataComponents));
    }

    #endregion

    #region ProportionProblematic / IsReliable

    /// <summary>
    /// ProportionProblematic = CountAbove07 / Count. Empty diagnostics return 0 by contract.
    /// </summary>
    [TestMethod]
    public void ProportionProblematic_EmptyDiagnostics_IsZero()
    {
        var diag = new InfluenceDiagnostics();

        Assert.AreEqual(0.0, diag.ProportionProblematic);
    }

    /// <summary>
    /// IsReliable must be false if any observation has k ≥ 1.0 — even when the proportion
    /// of problematic observations is otherwise low.
    /// </summary>
    [TestMethod]
    public void IsReliable_AnyVeryBadObs_IsFalse()
    {
        // 99 good, 1 very-bad — proportion is small, but the absolute presence of k ≥ 1.0
        // disqualifies PSIS-LOO reliability per the documented contract.
        var arr = new ObservationInfluence[100];
        for (int i = 0; i < 99; i++) arr[i] = Obs(i, 0.1);
        arr[99] = Obs(99, 1.5);

        var diag = new InfluenceDiagnostics(arr);

        Assert.IsFalse(diag.IsReliable);
    }

    /// <summary>
    /// IsReliable is true when no observations exceed k = 1.0 AND the proportion of
    /// k ≥ 0.7 observations is under 1%.
    /// </summary>
    [TestMethod]
    public void IsReliable_AllGood_IsTrue()
    {
        var arr = new ObservationInfluence[100];
        for (int i = 0; i < 100; i++) arr[i] = Obs(i, 0.2);

        var diag = new InfluenceDiagnostics(arr);

        Assert.IsTrue(diag.IsReliable);
    }

    #endregion

    #region GetMostInfluential / GetProblematic

    /// <summary>
    /// GetMostInfluentialObservations returns observations sorted by descending Pareto k.
    /// </summary>
    [TestMethod]
    public void GetMostInfluentialObservations_OrdersByDescendingParetoK()
    {
        var arr = new[]
        {
            Obs(0, 0.2),
            Obs(1, 0.9),
            Obs(2, 0.5),
            Obs(3, 0.1),
        };

        var diag = new InfluenceDiagnostics(arr);
        var top = diag.GetMostInfluentialObservations();

        Assert.AreEqual(0.9, top[0].ParetoK, 1e-12);
        Assert.AreEqual(0.5, top[1].ParetoK, 1e-12);
        Assert.AreEqual(0.2, top[2].ParetoK, 1e-12);
        Assert.AreEqual(0.1, top[3].ParetoK, 1e-12);
    }

    /// <summary>
    /// GetMostInfluentialObservations honors the topN limit and returns at most that many.
    /// </summary>
    [TestMethod]
    public void GetMostInfluentialObservations_RespectsTopN()
    {
        var arr = new[]
        {
            Obs(0, 0.1), Obs(1, 0.4), Obs(2, 0.7), Obs(3, 1.1),
        };

        var diag = new InfluenceDiagnostics(arr);
        var top2 = diag.GetMostInfluentialObservations(topN: 2);

        Assert.AreEqual(2, top2.Length);
        Assert.AreEqual(1.1, top2[0].ParetoK, 1e-12);
        Assert.AreEqual(0.7, top2[1].ParetoK, 1e-12);
    }

    /// <summary>
    /// GetProblematicObservations defaults to threshold 0.7 and returns observations
    /// at or above it, sorted by descending Pareto k.
    /// </summary>
    [TestMethod]
    public void GetProblematicObservations_DefaultThreshold_FiltersAndOrders()
    {
        var arr = new[]
        {
            Obs(0, 0.2), Obs(1, 0.6), Obs(2, 0.8), Obs(3, 1.2),
        };

        var diag = new InfluenceDiagnostics(arr);

        var problematic = diag.GetProblematicObservations();

        Assert.AreEqual(2, problematic.Length);
        Assert.AreEqual(1.2, problematic[0].ParetoK, 1e-12);
        Assert.AreEqual(0.8, problematic[1].ParetoK, 1e-12);
    }

    #endregion

    #region GetReliabilitySummary

    /// <summary>
    /// Reliability summary covers the four documented buckets — exercise each branch so
    /// a regression that flips the strings (used in user-facing reports) surfaces here.
    /// </summary>
    [TestMethod]
    public void GetReliabilitySummary_AllBranches_ReturnExpectedKeyword()
    {
        // Empty
        Assert.IsTrue(new InfluenceDiagnostics().GetReliabilitySummary()
            .Contains("No observations", StringComparison.OrdinalIgnoreCase));

        // GOOD
        var allGood = new[] { Obs(0, 0.1), Obs(1, 0.2) };
        Assert.IsTrue(new InfluenceDiagnostics(allGood).GetReliabilitySummary()
            .Contains("GOOD"));

        // OK (k in [0.5, 0.7))
        var ok = new[] { Obs(0, 0.5), Obs(1, 0.6) };
        Assert.IsTrue(new InfluenceDiagnostics(ok).GetReliabilitySummary()
            .Contains("OK"));

        // CAUTION (>=1% of obs at k>=0.7, no k>=1)
        var caution = new ObservationInfluence[100];
        for (int i = 0; i < 98; i++) caution[i] = Obs(i, 0.1);
        caution[98] = Obs(98, 0.8);
        caution[99] = Obs(99, 0.9);
        Assert.IsTrue(new InfluenceDiagnostics(caution).GetReliabilitySummary()
            .Contains("CAUTION"));

        // UNRELIABLE (any k >= 1.0)
        var unreliable = new[] { Obs(0, 1.5) };
        Assert.IsTrue(new InfluenceDiagnostics(unreliable).GetReliabilitySummary()
            .Contains("UNRELIABLE"));
    }

    #endregion

    #region Indexer

    /// <summary>
    /// Indexer exposes individual ObservationInfluence entries by index.
    /// </summary>
    [TestMethod]
    public void Indexer_ReturnsObservationByIndex()
    {
        var arr = new[] { Obs(0, 0.1), Obs(1, 0.5), Obs(2, 0.9) };
        var diag = new InfluenceDiagnostics(arr);

        Assert.AreEqual(0.1, diag[0].ParetoK, 1e-12);
        Assert.AreEqual(0.5, diag[1].ParetoK, 1e-12);
        Assert.AreEqual(0.9, diag[2].ParetoK, 1e-12);
    }

    #endregion

    #region XElement round-trip

    /// <summary>
    /// ToXElement / FromXElement must preserve the observation list and rolled-up
    /// summary statistics so report rendering doesn't trigger recomputation.
    /// </summary>
    [TestMethod]
    public void XmlSerialization_RoundTrip_PreservesObservationsAndSummaries()
    {
        var arr = new[]
        {
            Obs(0, 0.25),
            Obs(1, 0.55),
            Obs(2, 0.85),
            Obs(3, 1.05),
        };
        var original = new InfluenceDiagnostics(arr);

        var xml = original.ToXElement();
        var restored = new InfluenceDiagnostics(xml);

        Assert.AreEqual(original.Count, restored.Count);
        Assert.AreEqual(original.MeanParetoK, restored.MeanParetoK, 1e-12);
        Assert.AreEqual(original.MaxParetoK, restored.MaxParetoK, 1e-12);
        Assert.AreEqual(original.CountParetoKAbove05, restored.CountParetoKAbove05);
        Assert.AreEqual(original.CountParetoKAbove07, restored.CountParetoKAbove07);
        Assert.AreEqual(original.CountParetoKAbove10, restored.CountParetoKAbove10);

        for (int i = 0; i < original.Count; i++)
        {
            Assert.AreEqual(original[i].Index, restored[i].Index);
            Assert.AreEqual(original[i].ParetoK, restored[i].ParetoK, 1e-12);
            Assert.AreEqual(original[i].ElpdLoo, restored[i].ElpdLoo, 1e-12);
        }
    }

    /// <summary>
    /// XElement constructor must throw on null input.
    /// </summary>
    [TestMethod]
    public void XmlConstructor_NullXml_Throws()
    {
        Assert.ThrowsException<ArgumentNullException>(
            () => new InfluenceDiagnostics((XElement)null!));
    }

    #endregion
}
