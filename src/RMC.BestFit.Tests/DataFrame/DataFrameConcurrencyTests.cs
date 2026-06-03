using Numerics.Distributions;
using RMC.BestFit.Models;

namespace RMC.BestFit.Tests.InputDataFrame;

/// <summary>
/// Concurrency stress tests for <see cref="DataFrame.CreateFullTimeSeries"/>,
/// <see cref="DataFrame.ProcessThresholdSeries"/>, the <see cref="DataFrame.FullTimeSeries"/>
/// getter, and <see cref="DataFrame.JackKnife"/>.
/// </summary>
/// <remarks>
/// <para>
/// These tests protect the parallel batch-run scenario where multiple
/// <c>UnivariateAnalysis</c> instances share a single <see cref="DataFrame"/> and call
/// the above members concurrently. Pre-fix, the shared <c>_fullTimeSeries</c> list was
/// mutated in-place (Clear → Add → Sort), which crashed <see cref="List{Data}.Sort"/>
/// with <see cref="ArgumentException"/> / <see cref="InvalidOperationException"/>
/// when two threads raced on it.
/// </para>
/// <para>
/// The fix pattern under test: build a new list locally, sort it, then publish the
/// reference atomically under <c>_syncRoot</c>. Readers that captured the prior
/// reference keep iterating safely.
/// </para>
/// </remarks>
[TestClass]
public class DataFrameConcurrencyTests
{
    #region Fixtures

    /// <summary>
    /// Builds a mixed-series DataFrame exercising every branch of <see cref="DataFrame.CreateFullTimeSeries"/>:
    /// exact, uncertain, interval, and threshold data with non-overlapping index ranges.
    /// </summary>
    private static DataFrame CreateMixedFixture()
    {
        var df = new DataFrame();
        df.ExactSeries = new ExactSeries(
        [
            45000, 38000, 52000, 61000, 33000, 49000, 55000, 42000, 67000, 39000
        ]);
        // Uncertain data — historical flood observations with measurement error (distinct indices)
        df.UncertainSeries.Add(new UncertainData(1889, new Normal(85000, 10000)));
        df.UncertainSeries.Add(new UncertainData(1913, new Normal(75000, 8000)));
        // Interval data — paleoflood events (distinct indices)
        df.IntervalSeries.Add(new IntervalData(1500, 60000, 80000, 100000));
        df.IntervalSeries.Add(new IntervalData(1700, 50000, 70000, 90000));
        // Threshold data — historical perception threshold (distinct range)
        df.ThresholdSeries.Add(new ThresholdData(1850, 1870, 40000) { NumberAbove = 3 });
        return df;
    }

    /// <summary>
    /// Parallel stress driver. Runs <paramref name="action"/> across many iterations with
    /// maximum CPU concurrency and fails the calling test on any thrown exception.
    /// </summary>
    private static void RunParallel(int iterations, Action<int> action)
    {
        var options = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };
        var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();
        Parallel.For(0, iterations, options, i =>
        {
            try { action(i); }
            catch (Exception ex) { exceptions.Add(ex); }
        });
        if (!exceptions.IsEmpty)
        {
            throw new AggregateException(
                $"{exceptions.Count} of {iterations} iterations failed.",
                exceptions);
        }
    }

    #endregion

    #region CreateFullTimeSeries

    /// <summary>
    /// Many threads hammer <see cref="DataFrame.CreateFullTimeSeries"/> on the same instance.
    /// Pre-fix this reliably throws from <see cref="List{Data}.Sort"/> or enumeration.
    /// </summary>
    [TestMethod]
    public void CreateFullTimeSeries_ParallelCalls_DoesNotThrow()
    {
        var df = CreateMixedFixture();
        df.ProcessThresholdSeries();

        RunParallel(500, _ => df.CreateFullTimeSeries());

        Assert.AreEqual(df.TotalRecordLength(), df.FullTimeSeries.Count,
            "Full time series length must equal total record length after concurrent rebuilds.");
    }

    /// <summary>
    /// After concurrent rebuilds, the published snapshot must contain every expected index
    /// exactly once and be strictly sorted by <see cref="Data.Index"/>.
    /// </summary>
    [TestMethod]
    public void CreateFullTimeSeries_ParallelCalls_ListContentsDeterministic()
    {
        var df = CreateMixedFixture();
        df.ProcessThresholdSeries();

        // Control result (single-threaded) for comparison.
        df.CreateFullTimeSeries();
        var expectedIndices = df.FullTimeSeries.Select(d => d.Index).OrderBy(i => i).ToList();

        RunParallel(500, _ => df.CreateFullTimeSeries());

        var actual = df.FullTimeSeries;
        Assert.AreEqual(expectedIndices.Count, actual.Count);
        for (int i = 1; i < actual.Count; i++)
        {
            Assert.IsTrue(actual[i - 1].Index <= actual[i].Index,
                $"Full time series not sorted at position {i}: {actual[i - 1].Index} > {actual[i].Index}.");
        }
        CollectionAssert.AreEqual(expectedIndices, actual.Select(d => d.Index).ToList(),
            "Concurrent rebuilds must converge on the same index sequence as a single-threaded rebuild.");
    }

    #endregion

    #region ProcessThresholdSeries

    /// <summary>
    /// <see cref="DataFrame.ProcessThresholdSeries"/> is deterministic given fixed input series,
    /// so concurrent calls must converge on the same <c>NumberAbove</c>/<c>NumberBelow</c> as a
    /// single-threaded control call.
    /// </summary>
    [TestMethod]
    public void ProcessThresholdSeries_ParallelCalls_IdempotentResult()
    {
        // Control
        var control = CreateMixedFixture();
        control.ProcessThresholdSeries();
        var controlThreshold = (ThresholdData)control.ThresholdSeries[0];
        int expectedAbove = controlThreshold.NumberAbove;
        int expectedBelow = controlThreshold.NumberBelow;

        // Stress
        var df = CreateMixedFixture();
        RunParallel(500, _ => df.ProcessThresholdSeries());

        var actualThreshold = (ThresholdData)df.ThresholdSeries[0];
        Assert.AreEqual(expectedAbove, actualThreshold.NumberAbove);
        Assert.AreEqual(expectedBelow, actualThreshold.NumberBelow);
    }

    #endregion

    #region Interleaved

    /// <summary>
    /// Interleaves <see cref="DataFrame.ProcessThresholdSeries"/> and
    /// <see cref="DataFrame.CreateFullTimeSeries"/> across threads. The shared
    /// <c>_syncRoot</c> must give <c>CreateFullTimeSeries</c> a fully-processed threshold
    /// state (no torn <c>NumberAbove</c>/<c>NumberBelow</c> reads).
    /// </summary>
    [TestMethod]
    public void ProcessThresholdSeries_and_CreateFullTimeSeries_Interleaved_NoCorruption()
    {
        var df = CreateMixedFixture();

        RunParallel(500, i =>
        {
            if ((i & 1) == 0) df.ProcessThresholdSeries();
            else df.CreateFullTimeSeries();
        });

        // Ensure the final visible state is consistent (not torn): rebuild once and compare counts.
        df.ProcessThresholdSeries();
        df.CreateFullTimeSeries();
        Assert.AreEqual(df.TotalRecordLength(), df.FullTimeSeries.Count);

        var list = df.FullTimeSeries;
        for (int k = 1; k < list.Count; k++)
        {
            Assert.IsTrue(list[k - 1].Index <= list[k].Index,
                $"Full time series not sorted at position {k}.");
        }
    }

    #endregion

    #region FullTimeSeries Getter

    /// <summary>
    /// Many readers call <see cref="DataFrame.FullTimeSeries"/> concurrently while a writer
    /// thread toggles the ExactSeries count to force <c>TotalRecordLength()</c> shifts that
    /// trigger lazy rebuilds. Each reader must see a self-consistent snapshot (sorted; count
    /// matches one of the valid states).
    /// </summary>
    [TestMethod]
    public void FullTimeSeries_Getter_ParallelReads_ReturnsConsistentList()
    {
        var df = CreateMixedFixture();
        df.ProcessThresholdSeries();
        df.CreateFullTimeSeries();
        int stableCount = df.TotalRecordLength();

        var cts = new CancellationTokenSource();
        var extraRow = new ExactData(2020, 48500);

        // Writer: flips a row in/out so TotalRecordLength alternates between stableCount and stableCount + 1.
        var writer = Task.Run(() =>
        {
            while (!cts.IsCancellationRequested)
            {
                df.ExactSeries.Add(extraRow);
                df.ExactSeries.Remove(extraRow);
            }
        });

        try
        {
            RunParallel(2000, _ =>
            {
                var snapshot = df.FullTimeSeries;
                // Count may be stableCount or stableCount + 1 depending on which writer phase we caught.
                Assert.IsTrue(
                    snapshot.Count == stableCount || snapshot.Count == stableCount + 1,
                    $"Reader saw inconsistent FullTimeSeries count {snapshot.Count}; expected {stableCount} or {stableCount + 1}.");
                for (int k = 1; k < snapshot.Count; k++)
                {
                    Assert.IsTrue(snapshot[k - 1].Index <= snapshot[k].Index,
                        $"FullTimeSeries snapshot not sorted at position {k}.");
                }
            });
        }
        finally
        {
            cts.Cancel();
            writer.Wait(TimeSpan.FromSeconds(5));
        }
    }

    #endregion

    #region JackKnife (Clone regression)

    /// <summary>
    /// <see cref="DataFrame.JackKnife"/> used to share <c>_fullTimeSeries</c> with the parent
    /// via <see cref="DataFrame.Clone"/>, so calling <c>Clear</c> on the clone's full time series
    /// corrupted the parent. This regression test runs many parallel jackknifes and asserts the
    /// original DataFrame's <see cref="DataFrame.FullTimeSeries"/> is unchanged.
    /// </summary>
    [TestMethod]
    public void JackKnife_ParallelCalls_DoesNotCorruptOriginal()
    {
        var df = new DataFrame();
        df.ExactSeries = new ExactSeries(
        [
            45000, 38000, 52000, 61000, 33000, 49000, 55000, 42000, 67000, 39000,
            48000, 51000, 36000, 58000, 44000, 53000, 47000, 62000, 41000, 50000
        ]);
        df.CreateFullTimeSeries();
        var originalIndices = df.FullTimeSeries.Select(d => d.Index).ToList();
        var originalCount = df.FullTimeSeries.Count;

        RunParallel(originalCount, i => { var _ = df.JackKnife(i); });

        Assert.AreEqual(originalCount, df.FullTimeSeries.Count,
            "Parent DataFrame's FullTimeSeries count changed after parallel jackknifes.");
        CollectionAssert.AreEqual(originalIndices, df.FullTimeSeries.Select(d => d.Index).ToList(),
            "Parent DataFrame's FullTimeSeries indices changed after parallel jackknifes.");
    }

    #endregion
}
