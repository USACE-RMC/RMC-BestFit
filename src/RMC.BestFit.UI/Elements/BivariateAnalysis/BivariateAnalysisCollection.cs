using DatabaseManager;
using FrameworkInterfaces;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using RMC.BestFit.Models;

namespace RMC.BestFit.UI
{
    /// <summary>
    /// Represents a collection of bivariate-family analysis elements within a project.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    /// Hosts heterogeneous element types that share the bivariate context: today
    /// <see cref="BivariateAnalysis"/> (the upstream copula fit) and
    /// <see cref="CoincidentFrequencyAnalysis"/> (which consumes a fitted bivariate as input).
    /// Type discrimination follows the canonical Name + Type pattern used by
    /// <see cref="UnivariateAnalysisCollection"/>.
    /// </para>
    /// <para>
    /// Persistence path: the parent collection table holds (Name, Type) discriminator rows
    /// only — actual element data lives in the per-subtype <c>CollectionName</c> table for
    /// each element type. <see cref="Save"/> recreates the parent table from
    /// <see cref="ElementCollectionBase.ElementList"/> to preserve insertion order, then
    /// dispatches per-element <c>RaisePreviewSaved</c> + <c>Save</c> calls. Legacy
    /// single-table projects (data in the parent table) are migrated on first save —
    /// each element's <c>Open()</c> reads the legacy layout via fallback and marks itself
    /// dirty so the next <see cref="Save"/> writes the new two-table layout.
    /// </para>
    /// </remarks>
    public class BivariateAnalysisCollection : ElementCollectionBase
    {

        /// <summary>
        /// Constructs a new bivariate analysis collection with the specified parent project.
        /// </summary>
        /// <param name="parentProject">The parent project that owns this collection.</param>
        public BivariateAnalysisCollection(IProject parentProject) : base(parentProject) { }


        /// <summary>
        /// Gets the name of the element collection.
        /// </summary>
        public override string Name => "Bivariate Distribution Analysis";

        /// <summary>
        /// Maps bivariate-family runtime type names to their subtype storage tables.
        /// </summary>
        private static readonly IReadOnlyDictionary<string, string> s_subtypeTablesByClassName =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { nameof(BivariateAnalysis), BivariateAnalysis.CollectionName },
                { nameof(CoincidentFrequencyAnalysis), CoincidentFrequencyAnalysis.CollectionName }
            };

        /// <summary>
        /// Saves all elements in the collection to disk.
        /// </summary>
        /// <remarks>
        /// When the collection is dirty, the parent table is dropped and re-created with just
        /// Name + Type rows in <see cref="ElementCollectionBase.ElementList"/> order. Each
        /// element is then sent <c>RaisePreviewSaved</c> (so plot settings are captured) and,
        /// if dirty, asked to write itself to its per-subtype data table.
        /// </remarks>
        public override void Save()
        {
            bool cancel = false;
            RaisePreviewObjectSaved(this, ref cancel);
            if (cancel == true) return;

            _savingAll = true;
            try
            {
                // See if the collection is dirty and needs the parent table re-written.
                if (IsDirty == true)
                {
                    var sqLite = new SQLiteManager(ParentProject.FullFileName);
                    sqLite.Open();
                    try
                    {
                        // Delete and re-create the parent table so insertion order on disk matches
                        // ElementList order. Per-subtype data tables are unaffected.
                        if (sqLite.TableNames.Contains(Name))
                        {
                            sqLite.DeleteTable(Name);
                        }

                        var dataTable = new DataTable(Name);
                        dataTable.Columns.Add("Name", typeof(string));
                        dataTable.Columns.Add("Type", typeof(string));
                        foreach (var element in ElementList)
                        {
                            dataTable.Rows.Add(new[] { element.Name, element.GetType().ToString() });
                        }
                        sqLite.SaveDataTable(dataTable);

                        sqLite.Close();
                    }
                    finally
                    {
                        if (sqLite.DataBaseOpen) sqLite.Close();
                    }
                }

                // Dispatch per-element preview + save.
                for (int i = 0; i < ElementList.Count; i++)
                {
                    if (ElementList[i].GetType() == typeof(BivariateAnalysis))
                    {
                        ((BivariateAnalysis)ElementList[i]).RaisePreviewSaved(ref cancel);
                        if (cancel) continue;
                        if (ElementList[i].IsDirty) ElementList[i].Save();
                    }
                    else if (ElementList[i].GetType() == typeof(CoincidentFrequencyAnalysis))
                    {
                        ((CoincidentFrequencyAnalysis)ElementList[i]).RaisePreviewSaved(ref cancel);
                        if (cancel) continue;
                        if (ElementList[i].IsDirty) ElementList[i].Save();
                    }
                }

                SetIsDirty(false);
                RaiseObjectSaved();
            }
            finally
            {
                _savingAll = false;
            }
        }

        /// <summary>
        /// Loads all bivariate-family elements from disk into the collection.
        /// </summary>
        /// <remarks>
        /// Reads the parent table for (Name, Type) pairs and instantiates each element by Type.
        /// Rows missing a Type column (legacy single-table layout) default to
        /// <see cref="BivariateAnalysis"/> for backward compatibility — the element's own
        /// <c>Open()</c> handles the modern/legacy table fallback.
        /// </remarks>
        public override void Open()
        {
            _opening = true;
            var sqlite = new SQLiteManager(ParentProject.FullFileName);
            sqlite.Open();
            try
            {
                string[] names = Array.Empty<string>();
                string[] types = Array.Empty<string>();
                bool validateStoredRows = false;
                if (sqlite.TableNames.Contains(Name))
                {
                    var dtView = sqlite.GetTableManager(Name);
                    if (dtView.ColumnNames.Contains("Name"))
                    {
                        names = Array.ConvertAll(dtView.GetColumn("Name"), o => o?.ToString() ?? string.Empty);
                    }
                    if (dtView.ColumnNames.Contains("Type"))
                    {
                        types = Array.ConvertAll(dtView.GetColumn("Type"), o => o?.ToString() ?? string.Empty);
                        validateStoredRows = true;
                    }
                    else
                    {
                        // Legacy single-table layout — every row is a BivariateAnalysis.
                        types = new string[names.Length];
                        for (int i = 0; i < names.Length; i++) types[i] = nameof(BivariateAnalysis);
                    }
                }

                List<(string ElementName, string ElementType)> loadEntries =
                    CollectionPersistenceHelper.BuildTypedLoadEntries(
                        sqlite,
                        names,
                        types,
                        s_subtypeTablesByClassName,
                        validateStoredRows,
                        defaultElementType: nameof(BivariateAnalysis),
                        out bool needsIndexRewrite);

                foreach ((string elementName, string elementType) in loadEntries)
                {
                    IElement element;
                    string className = CollectionPersistenceHelper.GetClassName(elementType);
                    if (className == nameof(CoincidentFrequencyAnalysis))
                    {
                        element = new CoincidentFrequencyAnalysis(elementName, this, true);
                    }
                    else
                    {
                        // Default — empty / missing / BivariateAnalysis Type all map here.
                        element = new BivariateAnalysis(elementName, this, true);
                    }
                    Add(element);
                }

                sqlite.Close();
                SetIsDirty(needsIndexRewrite);
            }
            finally
            {
                if (sqlite.DataBaseOpen) sqlite.Close();
                _opening = false;
            }
        }

        /// <summary>
        /// Adds a bivariate-family analysis element to the collection.
        /// </summary>
        /// <param name="item">The element to add.</param>
        /// <remarks>
        /// Attaches property changed and deleted event handlers, saves the element to disk
        /// if it's a new element (not being loaded from file), and raises the element added event.
        /// </remarks>
        public override void Add(IElement item)
        {
            item.PropertyChanged += ElementPropertyChanged;
            item.Deleted += ElementDeleted;
            ElementList.Add(item);
            if (_opening == false)
            {
                var sqlite = new SQLiteManager(ParentProject.FullFileName);
                sqlite.Open();
                if (CollectionPersistenceHelper.StoredElementExists(
                        sqlite,
                        item.Name,
                        item.GetType().Name,
                        s_subtypeTablesByClassName) == false)
                {
                    item.Save();
                }
                sqlite.Close();
                SetIsDirty(true);
            }
            RaiseElementAddedEvent(item);
        }

        /// <summary>
        /// Copies a bivariate-family element from an external project file and inserts it into this collection.
        /// </summary>
        /// <param name="index">The zero-based index at which the element should be inserted.</param>
        /// <param name="elementName">The name of the element to copy from the external project.</param>
        /// <param name="elementType">The fully qualified type name of the element.</param>
        /// <param name="fullFileName">The full file path of the external project database.</param>
        public override void InsertFromExternalProject(int index, string elementName, string elementType, string fullFileName)
        {
            var sqlite = new SQLiteManager(fullFileName);
            string className = CollectionPersistenceHelper.GetClassName(elementType);

            IElement element;
            if (className == nameof(CoincidentFrequencyAnalysis))
            {
                var cfa = new CoincidentFrequencyAnalysis(elementName, this);
                cfa.Open(sqlite);
                element = cfa;
            }
            else if (className == nameof(BivariateAnalysis))
            {
                var ba = new BivariateAnalysis(elementName, this);
                ba.Open(sqlite);
                element = ba;
            }
            else
            {
                return;
            }
            Insert(index, element);
        }

        /// <summary>
        /// Inserts a bivariate-family element into the collection at the specified index.
        /// </summary>
        public override void Insert(int index, IElement item)
        {
            item.PropertyChanged += ElementPropertyChanged;
            item.Deleted += ElementDeleted;
            ElementList.Insert(index, item);
            if (_opening == false)
            {
                var sqlite = new SQLiteManager(ParentProject.FullFileName);
                sqlite.Open();
                if (CollectionPersistenceHelper.StoredElementExists(
                        sqlite,
                        item.Name,
                        item.GetType().Name,
                        s_subtypeTablesByClassName) == false)
                {
                    item.Save();
                }
                sqlite.Close();
                SetIsDirty(true);
            }
            RaiseElementAddedEvent(item);
        }

        /// <summary>
        /// Deletes the entire element collection and removes all associated tables from disk.
        /// </summary>
        /// <remarks>
        /// Drops the parent table plus every per-subtype data table owned by an element type
        /// in this collection. Mirrors <see cref="UnivariateAnalysisCollection.Delete"/>.
        /// </remarks>
        public override void Delete()
        {
            var sqlite = new SQLiteManager(ParentProject.FullFileName);
            sqlite.Open();
            sqlite.DeleteTable(Name);
            sqlite.DeleteTable(BivariateAnalysis.CollectionName);
            sqlite.DeleteTable(CoincidentFrequencyAnalysis.CollectionName);
            sqlite.Close();
        }
    }
}
