using System.Data;
using System.IO;
using System.Reflection;
using DatabaseManager;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RMC.BestFit.UI;

namespace RMC.BestFit.UI.Tests.Elements;

/// <summary>
/// Tests the dual-buffer layout backup and validation helpers on
/// <see cref="BestFitProject"/>. These safeguards prevent layout corruption from an
/// unexpected crash mid-save and preserve user-organized Project Explorer groupings
/// even when the currently-saved layout XML is malformed.
/// </summary>
[TestClass]
public class BestFitProjectLayoutBackupTests
{
    /// <summary>
    /// Well-formed XML must pass the pre-write validation check.
    /// </summary>
    [TestMethod]
    public void IsWellFormedLayoutXml_WellFormedXml_ReturnsTrue()
    {
        const string xml = "<Node NodeType=\"ProjectNode\" Name=\"Project\" IsExpanded=\"true\" />";
        var result = InvokeIsWellFormedLayoutXml(xml);
        Assert.IsTrue(result);
    }

    /// <summary>
    /// Malformed XML (missing closing bracket) must fail the pre-write validation check
    /// so that a corrupt string never overwrites a valid on-disk layout.
    /// </summary>
    [TestMethod]
    public void IsWellFormedLayoutXml_MalformedXml_ReturnsFalse()
    {
        const string xml = "<Node NodeType=\"ProjectNode\" Name=\"Project\" IsExpanded=\"true\" ";
        var result = InvokeIsWellFormedLayoutXml(xml);
        Assert.IsFalse(result);
    }

    /// <summary>
    /// Null and empty inputs must return <c>false</c> — there is no layout to validate.
    /// </summary>
    [TestMethod]
    public void IsWellFormedLayoutXml_NullOrEmpty_ReturnsFalse()
    {
        Assert.IsFalse(InvokeIsWellFormedLayoutXml(string.Empty));
        Assert.IsFalse(InvokeIsWellFormedLayoutXml(null!));
    }

    /// <summary>
    /// Obviously non-XML content (binary / random bytes) must fail validation.
    /// </summary>
    [TestMethod]
    public void IsWellFormedLayoutXml_NonXml_ReturnsFalse()
    {
        Assert.IsFalse(InvokeIsWellFormedLayoutXml("this is not xml"));
        Assert.IsFalse(InvokeIsWellFormedLayoutXml("<<<broken"));
    }

    /// <summary>
    /// On the first write, with an empty on-disk current value, the new layout lands
    /// in the current column and the previous column stays empty (nothing valid to rotate).
    /// </summary>
    [TestMethod]
    public void WriteLayoutWithBackup_FirstWrite_PopulatesCurrentLeavesPreviousEmpty()
    {
        string path = NewTempFile();
        try
        {
            using var sqlite = new SQLiteManager(path);
            sqlite.Open();
            var dtView = CreateLayoutTable(sqlite);

            const string newXml = "<Root Version=\"1\" />";
            InvokeWriteLayoutWithBackup(dtView, "CurrentCol", "PreviousCol", newXml);
            dtView.ApplyEdits();

            Assert.AreEqual(newXml, dtView.GetCell("CurrentCol", 0)?.ToString());
            Assert.AreEqual(string.Empty, dtView.GetCell("PreviousCol", 0)?.ToString() ?? string.Empty);
        }
        finally { File.Delete(path); }
    }

    /// <summary>
    /// A second write with a valid new layout must rotate the existing valid current
    /// value into the previous column. This is the core crash-recovery behavior.
    /// </summary>
    [TestMethod]
    public void WriteLayoutWithBackup_SecondWrite_RotatesCurrentIntoPrevious()
    {
        string path = NewTempFile();
        try
        {
            using var sqlite = new SQLiteManager(path);
            sqlite.Open();
            var dtView = CreateLayoutTable(sqlite);

            const string firstXml = "<Root Version=\"1\" />";
            const string secondXml = "<Root Version=\"2\" />";

            InvokeWriteLayoutWithBackup(dtView, "CurrentCol", "PreviousCol", firstXml);
            dtView.ApplyEdits();

            InvokeWriteLayoutWithBackup(dtView, "CurrentCol", "PreviousCol", secondXml);
            dtView.ApplyEdits();

            Assert.AreEqual(secondXml, dtView.GetCell("CurrentCol", 0)?.ToString(), "Current column must hold the newest layout.");
            Assert.AreEqual(firstXml, dtView.GetCell("PreviousCol", 0)?.ToString(), "Previous column must hold the prior layout for crash recovery.");
        }
        finally { File.Delete(path); }
    }

    /// <summary>
    /// Writing the same layout twice must not disturb the previous column — rotating
    /// on no-op writes would needlessly clobber an older, useful backup.
    /// </summary>
    [TestMethod]
    public void WriteLayoutWithBackup_SameValueTwice_DoesNotRotate()
    {
        string path = NewTempFile();
        try
        {
            using var sqlite = new SQLiteManager(path);
            sqlite.Open();
            var dtView = CreateLayoutTable(sqlite);

            const string firstXml = "<Root Version=\"1\" />";
            const string secondXml = "<Root Version=\"2\" />";

            InvokeWriteLayoutWithBackup(dtView, "CurrentCol", "PreviousCol", firstXml);
            dtView.ApplyEdits();

            InvokeWriteLayoutWithBackup(dtView, "CurrentCol", "PreviousCol", secondXml);
            dtView.ApplyEdits();

            // Third write: same as second — previous should still be firstXml, not secondXml.
            InvokeWriteLayoutWithBackup(dtView, "CurrentCol", "PreviousCol", secondXml);
            dtView.ApplyEdits();

            Assert.AreEqual(secondXml, dtView.GetCell("CurrentCol", 0)?.ToString());
            Assert.AreEqual(firstXml, dtView.GetCell("PreviousCol", 0)?.ToString(), "No-op write must not rotate the backup column.");
        }
        finally { File.Delete(path); }
    }

    /// <summary>
    /// A malformed new layout must be rejected outright — neither the current nor the
    /// previous column should be touched. This is the validation guard that prevents
    /// corrupted XML from poisoning the on-disk state.
    /// </summary>
    [TestMethod]
    public void WriteLayoutWithBackup_MalformedNewValue_SkipsWrite()
    {
        string path = NewTempFile();
        try
        {
            using var sqlite = new SQLiteManager(path);
            sqlite.Open();
            var dtView = CreateLayoutTable(sqlite);

            const string goodXml = "<Root Version=\"1\" />";
            const string badXml = "<Root Version=\"2\" ";  // unclosed

            InvokeWriteLayoutWithBackup(dtView, "CurrentCol", "PreviousCol", goodXml);
            dtView.ApplyEdits();

            InvokeWriteLayoutWithBackup(dtView, "CurrentCol", "PreviousCol", badXml);
            dtView.ApplyEdits();

            Assert.AreEqual(goodXml, dtView.GetCell("CurrentCol", 0)?.ToString(), "Malformed XML must not overwrite a valid current value.");
            Assert.AreEqual(string.Empty, dtView.GetCell("PreviousCol", 0)?.ToString() ?? string.Empty, "A rejected write must not rotate the backup.");
        }
        finally { File.Delete(path); }
    }

    /// <summary>
    /// If the existing on-disk current value is itself malformed, it must NOT be
    /// rotated into the backup column. Poisoning the recovery buffer with garbage
    /// would defeat the whole purpose of the dual-buffer scheme.
    /// </summary>
    [TestMethod]
    public void WriteLayoutWithBackup_ExistingCurrentIsMalformed_DoesNotRotateIt()
    {
        string path = NewTempFile();
        try
        {
            using var sqlite = new SQLiteManager(path);
            sqlite.Open();
            var dtView = CreateLayoutTable(sqlite);

            // Seed the current column with malformed XML directly (simulating a prior
            // pre-validation write or external corruption).
            dtView.EditCell(0, "CurrentCol", "<not-well-formed");
            dtView.ApplyEdits();

            const string goodXml = "<Root Version=\"1\" />";
            InvokeWriteLayoutWithBackup(dtView, "CurrentCol", "PreviousCol", goodXml);
            dtView.ApplyEdits();

            Assert.AreEqual(goodXml, dtView.GetCell("CurrentCol", 0)?.ToString());
            Assert.AreEqual(string.Empty, dtView.GetCell("PreviousCol", 0)?.ToString() ?? string.Empty,
                "Malformed existing current value must not be rotated into the backup.");
        }
        finally { File.Delete(path); }
    }

    /// <summary>
    /// Empty or null new-value input clears the current column and leaves the previous
    /// column alone. "No layout" is not the same as corrupt — it's a legitimate state.
    /// </summary>
    [TestMethod]
    public void WriteLayoutWithBackup_EmptyNewValue_ClearsCurrent()
    {
        string path = NewTempFile();
        try
        {
            using var sqlite = new SQLiteManager(path);
            sqlite.Open();
            var dtView = CreateLayoutTable(sqlite);

            const string firstXml = "<Root Version=\"1\" />";
            InvokeWriteLayoutWithBackup(dtView, "CurrentCol", "PreviousCol", firstXml);
            dtView.ApplyEdits();

            InvokeWriteLayoutWithBackup(dtView, "CurrentCol", "PreviousCol", string.Empty);
            dtView.ApplyEdits();

            Assert.AreEqual(string.Empty, dtView.GetCell("CurrentCol", 0)?.ToString() ?? string.Empty);
            // Previous column stays whatever it was before — empty in this case.
            Assert.AreEqual(string.Empty, dtView.GetCell("PreviousCol", 0)?.ToString() ?? string.Empty);
        }
        finally { File.Delete(path); }
    }

    // --- Helpers -------------------------------------------------------------

    /// <summary>
    /// Supports the <c>NewTempFile</c> helper.
    /// </summary>
    /// <returns>The formatted text.</returns>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static string NewTempFile() =>
        Path.Combine(Path.GetTempPath(), $"rmcbf-layoutbackup-{Guid.NewGuid():N}.bestfit");

    /// <summary>
    /// Creates a two-column table matching the layout/backup schema used by
    /// <c>WriteLayoutWithBackup</c> and returns the open view.
    /// </summary>
    private static DataTableView CreateLayoutTable(SQLiteManager sqlite)
    {
        var table = new DataTable("Project");
        table.Columns.Add("CurrentCol", typeof(string));
        table.Columns.Add("PreviousCol", typeof(string));
        sqlite.SaveDataTable(table);

        var view = sqlite.GetTableManager("Project");
        view.AddRow();
        view.EditCell(0, "CurrentCol", string.Empty);
        view.EditCell(0, "PreviousCol", string.Empty);
        view.ApplyEdits();
        return view;
    }

    /// <summary>
    /// Supports the <c>InvokeIsWellFormedLayoutXml</c> helper.
    /// </summary>
    /// <param name="xml">The XML text to evaluate.</param>
    /// <returns>A value indicating whether the condition is satisfied.</returns>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static bool InvokeIsWellFormedLayoutXml(string xml)
    {
        var method = typeof(BestFitProject).GetMethod(
            "IsWellFormedLayoutXml",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(method, "IsWellFormedLayoutXml must be accessible via reflection.");
        return (bool)method!.Invoke(null, new object?[] { xml })!;
    }

    /// <summary>
    /// Supports the <c>InvokeWriteLayoutWithBackup</c> helper.
    /// </summary>
    /// <param name="dtView">The data table view under test.</param>
    /// <param name="currentCol">The current column name.</param>
    /// <param name="previousCol">The previous column name.</param>
    /// <param name="newValue">The replacement column value.</param>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static void InvokeWriteLayoutWithBackup(DataTableView dtView, string currentCol, string previousCol, string newValue)
    {
        var method = typeof(BestFitProject).GetMethod(
            "WriteLayoutWithBackup",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(method, "WriteLayoutWithBackup must be accessible via reflection.");
        method!.Invoke(null, new object?[] { dtView, currentCol, previousCol, newValue });
    }
}
