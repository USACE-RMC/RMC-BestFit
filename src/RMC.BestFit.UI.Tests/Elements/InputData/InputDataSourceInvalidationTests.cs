using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Numerics.Data;
using RMC.BestFit.Models;
using RMC.BestFit.UI;

namespace RMC.BestFit.UI.Tests.Elements.InputData;

/// <summary>Checks source-data invalidation and notification contracts for derived input data.</summary>
/// <remarks>Uses small inline source series and deterministic peak extraction; no fitting or sampling occurs.</remarks>
[TestClass]
[DoNotParallelize]
public class InputDataSourceInvalidationTests
{
    /// <summary>Source edits invalidate extracted events and notify diagnostics in processed and unprocessed states.</summary>
    /// <param name="operation">The real source edit to perform.</param>
    /// <param name="processed">Whether a prior peak extraction exists.</param>
    /// <remarks>The original event remains observable, and the canonical TimeSeries notification refreshes existing App consumers.</remarks>
    [STATestMethod]
    [DataRow("Value", true)]
    [DataRow("Value", false)]
    [DataRow("Index", true)]
    [DataRow("Index", false)]
    [DataRow("Add", true)]
    [DataRow("Add", false)]
    [DataRow("Remove", true)]
    [DataRow("Remove", false)]
    [DataRow("Replace", true)]
    [DataRow("Replace", false)]
    [DataRow("Clear", true)]
    [DataRow("Clear", false)]
    [DataRow("Reset", true)]
    [DataRow("Reset", false)]
    [DataRow("Reorder", true)]
    [DataRow("Reorder", false)]
    public void SourceEdit_InvalidatesDerivedResultsAndForwardsNotifications(string operation, bool processed)
    {
        var input = CreateInput(processed);
        var notifications = new List<string>();
        input.PropertyChanged += (_, e) => notifications.Add(e.PropertyName!);
        var sourceNotifications = new List<string>();
        input.TimeSeriesElement.PropertyChanged += (_, e) => sourceNotifications.Add(e.PropertyName!);

        ApplyEdit(input.TimeSeriesElement.TimeSeries, operation);

        Assert.IsFalse(input.IsProcessed, operation + " must invalidate the processed flag.");
        Assert.AreEqual(0, input.DataFrame.ExactSeries.Count, operation + " must remove stale extracted peaks.");
        string originalEvent = operation is "Value" or "Index" ? operation : "TimeSeriesCollection";
        CollectionAssert.Contains(sourceNotifications, originalEvent, "The source's existing notification must remain intact.");
        CollectionAssert.Contains(notifications, originalEvent, "InputData must still forward the original source notification.");
        CollectionAssert.Contains(notifications, nameof(TimeSeriesElement.TimeSeries),
            "Diagnostics must receive the canonical source-data notification even if IsProcessed was already false.");
    }

    /// <summary>Batch edits defer invalidation until the collection publishes its final reset.</summary>
    /// <remarks>This preserves bulk-paste and undo restoration suppression semantics.</remarks>
    [STATestMethod]
    public void SuppressedSourceEdit_DefersInvalidationUntilReset()
    {
        var input = CreateInput(true);
        var series = input.TimeSeriesElement.TimeSeries;
        var notifications = new List<string>();
        input.PropertyChanged += (_, e) => notifications.Add(e.PropertyName!);
        int count = input.DataFrame.ExactSeries.Count;
        series.SuppressCollectionChanged = true;
        try { series[0].Value += 5d; }
        finally { series.SuppressCollectionChanged = false; }
        Assert.IsTrue(input.IsProcessed);
        Assert.AreEqual(count, input.DataFrame.ExactSeries.Count);
        Assert.IsFalse(notifications.Contains(nameof(TimeSeriesElement.TimeSeries)));

        series.RaiseCollectionChangedReset();

        Assert.IsFalse(input.IsProcessed);
        Assert.AreEqual(0, input.DataFrame.ExactSeries.Count);
        CollectionAssert.Contains(notifications, nameof(TimeSeriesElement.TimeSeries));
    }

    /// <summary>Replacing either the selected element or its series detaches the previous data source.</summary>
    /// <param name="replaceElement">True replaces the selected element; false replaces its underlying series.</param>
    [STATestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void SourceReplacement_DetachesOldDataAndTracksNewData(bool replaceElement)
    {
        var input = CreateInput(true);
        var oldElement = input.TimeSeriesElement;
        var oldSeries = oldElement.TimeSeries;
        if (replaceElement)
            input.TimeSeriesElement = CreateSource("Replacement source");
        else
            oldElement.TimeSeries = CreateSource("Replacement series").TimeSeries;
        Assert.IsFalse(input.IsProcessed);
        Assert.AreEqual(0, input.DataFrame.ExactSeries.Count);
        Process(input);
        var notifications = new List<string>();
        input.PropertyChanged += (_, e) => notifications.Add(e.PropertyName!);

        oldSeries[0].Value += 1d;
        oldSeries.Add(new SeriesOrdinate<DateTime, double>(new DateTime(2020, 3, 1), 99d));

        Assert.IsTrue(input.IsProcessed, "Edits to detached sources cannot invalidate the new extraction.");
        Assert.IsFalse(notifications.Contains(nameof(TimeSeriesElement.TimeSeries)));
        Assert.IsFalse(notifications.Contains("Value"));
        Assert.IsFalse(notifications.Contains("TimeSeriesCollection"));

        input.TimeSeriesElement.TimeSeries[0].Value += 2d;

        Assert.IsFalse(input.IsProcessed);
        Assert.AreEqual(0, input.DataFrame.ExactSeries.Count);
        CollectionAssert.Contains(notifications, "Value");
        CollectionAssert.Contains(notifications, nameof(TimeSeriesElement.TimeSeries));
    }

    /// <summary>Removed source ordinates cannot invalidate a later extraction when edited independently.</summary>
    [STATestMethod]
    public void RemovedSourceOrdinate_NoLongerInvalidatesCurrentExtraction()
    {
        var input = CreateInput(true);
        var removed = input.TimeSeriesElement.TimeSeries[^1];
        input.TimeSeriesElement.TimeSeries.RemoveAt(input.TimeSeriesElement.TimeSeries.Count - 1);
        Process(input);
        var notifications = new List<string>();
        input.PropertyChanged += (_, e) => notifications.Add(e.PropertyName!);

        removed.Value += 1d;

        Assert.IsTrue(input.IsProcessed);
        Assert.IsFalse(notifications.Contains("Value"));
        Assert.IsFalse(notifications.Contains(nameof(TimeSeriesElement.TimeSeries)));
    }

    /// <summary>Automatic invalidation preserves unrelated input undo history and never records stale derived peaks.</summary>
    /// <param name="undoEnabled">The input's undo-recording state during source invalidation.</param>
    /// <param name="replaceSeries">True replaces the series; false edits an existing source value.</param>
    /// <remarks>The source owns the user edit. The dependent input's undo stack must contain only the earlier name change.</remarks>
    [STATestMethod]
    [DataRow(true, false)]
    [DataRow(false, false)]
    [DataRow(true, true)]
    [DataRow(false, true)]
    public void SourceInvalidation_PreservesInputUndoHistoryWithoutRecordingDerivedClears(bool undoEnabled, bool replaceSeries)
    {
        var input = CreateInput(true);
        input.UndoManager.Clear();
        string originalName = input.Name;
        string changedName = originalName + " renamed";
        input.Name = changedName;
        input.TimeSeriesElement.UndoManager.Clear();
        Assert.IsTrue(input.UndoManager.CanUndo, "The unrelated input name action must survive clearing source history.");
        input.IsUndoEnabled = undoEnabled;

        if (replaceSeries)
            input.TimeSeriesElement.TimeSeries = CreateSource("Replacement for undo test").TimeSeries;
        else
            input.TimeSeriesElement.TimeSeries[0].Value += 1d;

        Assert.AreEqual(undoEnabled, input.IsUndoEnabled, "Automatic clearing must restore the caller's recording state.");
        Assert.IsFalse(input.IsProcessed);
        Assert.AreEqual(0, input.DataFrame.ExactSeries.Count);
        Assert.IsTrue(input.UndoManager.CanUndo);
        input.UndoManager.Undo();
        Assert.AreEqual(originalName, input.Name, "One undo must restore the unrelated name, not obsolete extracted peaks.");
        Assert.AreEqual(0, input.DataFrame.ExactSeries.Count, "Undo must not resurrect peaks from the previous source data.");
        Assert.IsFalse(input.IsProcessed);
        Assert.IsFalse(input.UndoManager.CanUndo, "Invalidation must not append dependent DataFrame actions.");
        Assert.AreEqual(undoEnabled, input.IsUndoEnabled);
        Assert.IsTrue(input.UndoManager.CanRedo);
        input.UndoManager.Redo();
        Assert.AreEqual(changedName, input.Name);
        Assert.AreEqual(0, input.DataFrame.ExactSeries.Count);
        Assert.IsFalse(input.IsProcessed);
        Assert.AreEqual(undoEnabled, input.IsUndoEnabled);
    }

    /// <summary>Reset-style removals detach retained old ordinates before a fresh extraction is built.</summary>
    /// <param name="operation">Clear/refill, suppressed removal, or suppressed replacement with a distinct equal-valued ordinate.</param>
    /// <remarks>Reset events have no OldItems, so rebinding only current rows leaves an obsolete ordinate subscribed.</remarks>
    [STATestMethod]
    [DataRow("Clear")]
    [DataRow("SuppressedRemove")]
    [DataRow("SuppressedEqualReplacement")]
    public void ResetRemoval_DetachesRetainedOrdinateFromLaterExtraction(string operation)
    {
        var input = CreateInput(true);
        var source = input.TimeSeriesElement;
        var series = source.TimeSeries;
        var removed = series[^1];
        if (operation == "Clear")
        {
            series.Clear();
            for (int i = 0; i < 40; i++)
                series.Add(new SeriesOrdinate<DateTime, double>(new DateTime(2021, 1, 1).AddDays(i),
                    i % 10 == 5 ? 150d + i : 10d));
        }
        else
        {
            series.SuppressCollectionChanged = true;
            try
            {
                series.RemoveAt(series.Count - 1);
                if (operation == "SuppressedEqualReplacement")
                {
                    var replacement = new SeriesOrdinate<DateTime, double>(removed.Index, removed.Value);
                    Assert.IsTrue(removed.Equals(replacement), "The replacement deliberately compares equal by value.");
                    Assert.AreNotSame(removed, replacement);
                    series.Add(replacement);
                }
            }
            finally { series.SuppressCollectionChanged = false; }
            series.RaiseCollectionChangedReset();
        }
        Process(input);
        var extracted = input.DataFrame.ExactSeries.ToArray();
        var inputNotifications = new List<string>();
        var sourceNotifications = new List<string>();
        input.PropertyChanged += (_, e) => inputNotifications.Add(e.PropertyName!);
        source.PropertyChanged += (_, e) => sourceNotifications.Add(e.PropertyName!);

        if (operation == "SuppressedEqualReplacement")
        {
            // Make the first delivered change restore value equality with the live replacement.
            // A value-based membership check would wrongly accept this obsolete object.
            series.SuppressCollectionChanged = true;
            try { removed.Value -= 1d; }
            finally { series.SuppressCollectionChanged = false; }
        }
        removed.Value += 1d;
        removed.Index = removed.Index.AddDays(1);

        Assert.IsTrue(input.IsProcessed, "An ordinate removed before Reset cannot invalidate a later extraction.");
        CollectionAssert.AreEqual(extracted, input.DataFrame.ExactSeries.ToArray());
        Assert.IsFalse(sourceNotifications.Contains("Value"));
        Assert.IsFalse(sourceNotifications.Contains("Index"));
        Assert.IsFalse(inputNotifications.Contains("Value"));
        Assert.IsFalse(inputNotifications.Contains("Index"));
        Assert.IsFalse(inputNotifications.Contains(nameof(TimeSeriesElement.TimeSeries)));
        series[^1].Value += 2d;
        Assert.IsFalse(input.IsProcessed, "The current ordinate must remain subscribed, including an equal-valued replacement.");
        CollectionAssert.Contains(inputNotifications, nameof(TimeSeriesElement.TimeSeries));
    }

    /// <summary>A retained reset-removed ordinate cannot publish changes after its former source is set to null.</summary>
    [STATestMethod]
    public void ResetRemovedOrdinate_AfterNullSource_DoesNotNotifyOrThrow()
    {
        var input = CreateInput(true);
        var source = input.TimeSeriesElement;
        var removed = source.TimeSeries[0];
        source.TimeSeries.Clear();
        source.TimeSeries = null!;
        var notifications = new List<string>();
        source.PropertyChanged += (_, e) => notifications.Add(e.PropertyName!);

        removed.Value += 1d;

        Assert.IsFalse(notifications.Contains("Value"));
        Assert.IsFalse(input.IsProcessed);
        Assert.AreEqual(0, input.DataFrame.ExactSeries.Count);
    }

    /// <summary>Removing the source series invalidates a previous extraction and notifies existing consumers.</summary>
    /// <remarks>A null replacement is an absent source, not permission to retain old peaks.</remarks>
    [STATestMethod]
    public void NullSourceSeriesReplacement_InvalidatesPriorExtraction()
    {
        var input = CreateInput(true);
        var notifications = new List<string>();
        input.PropertyChanged += (_, e) => notifications.Add(e.PropertyName!);

        input.TimeSeriesElement.TimeSeries = null!;

        Assert.IsFalse(input.IsProcessed);
        Assert.AreEqual(0, input.DataFrame.ExactSeries.Count);
        CollectionAssert.Contains(notifications, nameof(TimeSeriesElement.TimeSeries));
    }

    /// <summary>Manual observations survive edits to an associated source series.</summary>
    [STATestMethod]
    public void ManualInput_SourceEditPreservesEnteredObservations()
    {
        var input = CreateInput(false);
        input.ExactDataMethod = UI.InputData.ExactDataEntryType.Manual;
        var entered = new ExactData(2000, 123d);
        input.DataFrame.ExactSeries.Add(entered);

        input.TimeSeriesElement.TimeSeries[0].Value += 1d;

        Assert.AreEqual(1, input.DataFrame.ExactSeries.Count);
        Assert.AreSame(entered, input.DataFrame.ExactSeries[0]);
    }

    /// <summary>Downloaded USGS observations never derive from a linked series, so source changes keep them.</summary>
    /// <param name="method">The USGS entry method whose downloaded observations must survive.</param>
    /// <param name="replaceSeries">True to replace the source series; false to edit one of its values in place.</param>
    /// <remarks>
    /// Switching an input from block or peaks-over-threshold extraction to USGS download keeps the saved source
    /// reference, so edits to that series still reach this input.
    /// </remarks>
    [STATestMethod]
    [DataRow(UI.InputData.ExactDataEntryType.USGSPeakDischarge, false)]
    [DataRow(UI.InputData.ExactDataEntryType.USGSPeakDischarge, true)]
    [DataRow(UI.InputData.ExactDataEntryType.USGSPeakStage, false)]
    [DataRow(UI.InputData.ExactDataEntryType.USGSPeakStage, true)]
    public void UsgsInput_SourceChangePreservesDownloadedObservations(UI.InputData.ExactDataEntryType method, bool replaceSeries)
    {
        var input = CreateInput(false);
        input.ExactDataMethod = method;
        var downloaded = new ExactData(2000, 123d);
        input.DataFrame.ExactSeries.Add(downloaded);

        if (replaceSeries)
            input.TimeSeriesElement.TimeSeries = CreateSource("Unrelated replacement").TimeSeries;
        else
            input.TimeSeriesElement.TimeSeries[0].Value += 1d;

        Assert.AreEqual(1, input.DataFrame.ExactSeries.Count);
        Assert.AreSame(downloaded, input.DataFrame.ExactSeries[0]);
    }

    /// <summary>Source metadata edits retain valid extracted values and their notification identity.</summary>
    [STATestMethod]
    public void SourceMetadataChange_PreservesExtractionAndOriginalNotification()
    {
        var input = CreateInput(true);
        var original = input.DataFrame.ExactSeries.ToArray();
        var notifications = new List<string>();
        input.PropertyChanged += (_, e) => notifications.Add(e.PropertyName!);

        input.TimeSeriesElement.Name = "Renamed source";

        Assert.IsTrue(input.IsProcessed);
        CollectionAssert.AreEqual(original, input.DataFrame.ExactSeries.ToArray());
        CollectionAssert.Contains(notifications, nameof(TimeSeriesElement.Name));
        Assert.IsFalse(notifications.Contains(nameof(TimeSeriesElement.TimeSeries)), "Metadata does not change source observations.");
    }

    /// <summary>Collection-edit undo and redo retain source identity and invalidate newly extracted results each time.</summary>
    /// <remarks>Uses the real source collection's undo bridge and its reset notification path.</remarks>
    [STATestMethod]
    public void SourceReplacementUndoRedo_PreservesSourceHistoryAndInvalidatesExtraction()
    {
        var input = CreateInput(true);
        var source = input.TimeSeriesElement;
        source.UndoManager.Clear();
        var original = source.TimeSeries[0];
        var replacement = new SeriesOrdinate<DateTime, double>(original.Index, original.Value + 5d);
        source.TimeSeries[0] = replacement;
        Assert.IsFalse(input.IsProcessed);
        Assert.IsTrue(source.UndoManager.CanUndo);
        Process(input);

        source.UndoManager.Undo();

        Assert.AreSame(original, source.TimeSeries[0]);
        Assert.IsFalse(input.IsProcessed);
        Assert.AreEqual(0, input.DataFrame.ExactSeries.Count);
        Assert.IsTrue(source.UndoManager.CanRedo);
        Process(input);

        source.UndoManager.Redo();

        Assert.AreSame(replacement, source.TimeSeries[0]);
        Assert.IsFalse(input.IsProcessed);
        Assert.AreEqual(0, input.DataFrame.ExactSeries.Count);
    }

    /// <summary>Creates a POT input element with optional deterministic extraction.</summary>
    /// <param name="processed">Whether to extract peaks before returning.</param>
    /// <returns>The local input element.</returns>
    private static UI.InputData CreateInput(bool processed)
    {
        var input = new UI.InputData("Source invalidation", new InputDataCollection(BestFitProject.GetInstance()))
        {
            ExactDataMethod = UI.InputData.ExactDataEntryType.PeaksOverThresholdSeries,
            TimeSeriesElement = CreateSource("Initial source"),
            Threshold = 50d,
            MinStepsBetweenPeaks = 3
        };
        if (processed) Process(input);
        return input;
    }

    /// <summary>Creates a short series with four separated peaks and no missing observations.</summary>
    /// <param name="name">The source element name.</param>
    /// <returns>The source with deterministic daily values.</returns>
    private static TimeSeriesElement CreateSource(string name)
    {
        var values = Enumerable.Range(0, 40).Select(i => i % 10 == 5 ? 100d + i : 10d).ToArray();
        return new TimeSeriesElement(name)
        {
            TimeSeries = new TimeSeries(TimeInterval.OneDay, new DateTime(2020, 1, 1), values)
        };
    }

    /// <summary>Runs deterministic peak extraction and checks fixture preconditions.</summary>
    /// <param name="input">The input element to process.</param>
    private static void Process(UI.InputData input)
    {
        input.CreatePeaksOverThresholdSeries();
        Assert.IsTrue(input.IsProcessed, "Fixture extraction must succeed.");
        Assert.IsTrue(input.DataFrame.ExactSeries.Count > 0, "Fixture must have stale peaks to invalidate.");
    }

    /// <summary>Applies a real ordinate or collection mutation to the source series.</summary>
    /// <param name="series">The source series.</param>
    /// <param name="operation">The requested mutation.</param>
    /// <exception cref="ArgumentException">Thrown for an unknown operation.</exception>
    /// <remarks>Numerics Series has no Move API; reordering uses a suppressed remove/insert followed by reset.</remarks>
    private static void ApplyEdit(TimeSeries series, string operation)
    {
        switch (operation)
        {
            case "Value": series[0].Value += 1d; break;
            case "Index": series[0].Index = series[0].Index.AddHours(-1); break;
            case "Add": series.Add(new SeriesOrdinate<DateTime, double>(series[^1].Index.AddDays(1), 90d)); break;
            case "Remove": series.RemoveAt(series.Count - 1); break;
            case "Replace": series[0] = new SeriesOrdinate<DateTime, double>(series[0].Index, 99d); break;
            case "Clear": series.Clear(); break;
            case "Reset":
            case "Reorder":
                series.SuppressCollectionChanged = true;
                try
                {
                    if (operation == "Reset") series[0].Value += 1d;
                    else
                    {
                        var item = series[0];
                        series.RemoveAt(0);
                        series.Insert(1, item);
                    }
                }
                finally { series.SuppressCollectionChanged = false; }
                series.RaiseCollectionChangedReset();
                break;
            default: throw new ArgumentException("Unknown source edit.", nameof(operation));
        }
    }
}
