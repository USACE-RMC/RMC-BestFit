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
    /// Project-tree collection of univariate-family analysis elements: <see cref="UnivariateAnalysis"/>,
    /// <see cref="B17CAnalysis"/>, <see cref="MixtureAnalysis"/>, <see cref="PointProcessAnalysis"/>,
    /// and <see cref="CompositeAnalysis"/>. Persists a parent (Name, Type) discriminator table so the
    /// per-subtype data tables can be loaded into the correct concrete type on Open.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// </remarks>
    public class UnivariateAnalysisCollection : ElementCollectionBase
    {

        /// <summary>
        /// Constructs a new univariate analysis collection with the specified parent project.
        /// </summary>
        /// <param name="parentProject">The parent project that owns this collection.</param>
        public UnivariateAnalysisCollection(IProject parentProject) : base(parentProject) { }

        /// <summary>
        /// Gets the name of the element collection.
        /// </summary>
        public override string Name => "Univariate Distribution Analysis";

        /// <summary>
        /// Maps univariate-family element class names to their subtype SQLite tables.
        /// </summary>
        private static readonly IReadOnlyDictionary<string, string> s_subtypeTablesByClassName =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { nameof(UnivariateAnalysis), UnivariateAnalysis.CollectionName },
                { nameof(PointProcessAnalysis), PointProcessAnalysis.CollectionName },
                { nameof(MixtureAnalysis), MixtureAnalysis.CollectionName },
                { nameof(B17CAnalysis), B17CAnalysis.CollectionName },
                { nameof(CompositeAnalysis), CompositeAnalysis.CollectionName }
            };

        /// <summary>
        /// Saves all elements in the collection to disk.
        /// </summary>
        public override void Save()
        {
            bool cancel = false;
            RaisePreviewObjectSaved(this, ref cancel);
            if (cancel == true) return;

            _savingAll = true;
            try
            {
                // See if the collection is dirty and needs to be saved.
                if (IsDirty == true)
                {
                    // Create SQLite connection
                    var sqLite = new SQLiteManager(ParentProject.FullFileName);
                    sqLite.Open();
                    try
                    {
                        // Delete existing table and re-create as order of elements may have changed.
                        if (sqLite.TableNames.Contains(Name) == true)
                        {
                            sqLite.DeleteTable(Name);
                        }

                        // Create new table
                        DataTable dataTable = new DataTable(Name);
                        dataTable.Columns.Add("Name", typeof(string));
                        dataTable.Columns.Add("Type", typeof(string));
                        foreach (var element in ElementList)
                        {
                            dataTable.Rows.Add(new[] { element.Name, element.GetType().ToString() });
                        }
                        sqLite.SaveDataTable(dataTable);

                        // Close SQLite connection
                        sqLite.Close();
                    }
                    finally
                    {
                        if (sqLite.DataBaseOpen) sqLite.Close();
                    }
                }

                    // Save all non-composite analyses
                for (int i = 0; i < ElementList.Count; i++)
                {
                    if (ElementList[i].GetType() == typeof(UnivariateAnalysis))
                    {
                        // Always raise preview saved. This allows the plot settings to always be saved.
                        ((UnivariateAnalysis)ElementList[i]).RaisePreviewSaved(ref cancel);
                        if (cancel == true) continue;
                        // Only save if dirty
                        if (ElementList[i].IsDirty)
                            ElementList[i].Save();
                    }
                    else if (ElementList[i].GetType() == typeof(PointProcessAnalysis))
                    {
                        // Always raise preview saved. This allows the plot settings to always be saved.
                        ((PointProcessAnalysis)ElementList[i]).RaisePreviewSaved(ref cancel);
                        if (cancel == true) continue;
                        // Only save if dirty
                        if (ElementList[i].IsDirty)
                            ElementList[i].Save();
                    }
                    else if (ElementList[i].GetType() == typeof(MixtureAnalysis))
                    {
                        // Always raise preview saved. This allows the plot settings to always be saved.
                        ((MixtureAnalysis)ElementList[i]).RaisePreviewSaved(ref cancel);
                        if (cancel == true) continue;
                        // Only save if dirty
                        if (ElementList[i].IsDirty)
                            ElementList[i].Save();
                    }
                    else if (ElementList[i].GetType() == typeof(B17CAnalysis))
                    {
                        // Always raise preview saved. This allows the plot settings to always be saved.
                        ((B17CAnalysis)ElementList[i]).RaisePreviewSaved(ref cancel);
                        if (cancel == true) continue;
                        // Only save if dirty
                        if (ElementList[i].IsDirty)
                            ElementList[i].Save();
                    }

                }

                // Now save all the composite analyses
                for (int i = 0; i < ElementList.Count; i++)
                {
                    if (ElementList[i].GetType() == typeof(CompositeAnalysis))
                    {
                        // Always raise preview saved. This allows the plot settings to always be saved.
                        ((CompositeAnalysis)ElementList[i]).RaisePreviewSaved(ref cancel);
                        if (cancel == true) continue;
                        // Only save if dirty
                        if (ElementList[i].IsDirty)
                            ElementList[i].Save();
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
        /// Builds the ordered list of valid parent-index rows to load from disk.
        /// </summary>
        /// <param name="sqlite">The SQLite project connection.</param>
        /// <param name="names">The element names from the parent-index table.</param>
        /// <param name="types">The element type names from the parent-index table.</param>
        /// <param name="validateStoredRows">Whether subtype rows should be required for each index row.</param>
        /// <param name="needsIndexRewrite">
        /// Receives <c>true</c> when stale, malformed, duplicate, or orphan index rows were skipped.
        /// </param>
        /// <returns>The ordered list of loadable element name/type pairs.</returns>
        /// <remarks>
        /// Exact duplicate parent-index rows are treated as persistence noise because they point at the
        /// same subtype row. They are skipped and the collection is marked dirty so the next save rewrites
        /// the parent table with one row per loaded element.
        /// </remarks>
        internal static List<(string ElementName, string ElementType)> BuildLoadEntries(
            SQLiteManager sqlite,
            string[] names,
            string[] types,
            bool validateStoredRows,
            out bool needsIndexRewrite)
        {
            return CollectionPersistenceHelper.BuildTypedLoadEntries(
                sqlite,
                names,
                types,
                s_subtypeTablesByClassName,
                validateStoredRows,
                defaultElementType: null,
                out needsIndexRewrite);
        }

        /// <summary>
        /// Loads all elements in the collection from disk.
        /// </summary>
        public override void Open()
        {
            _opening = true;
            // Create SQLite connection
            var sqlite = new SQLiteManager(ParentProject.FullFileName);
            sqlite.Open();
            try
            {
                // First get the list of elements in the collection in order
                string[] names = new string[0];
                string[] types = new string[0];
                bool validateStoredRows = false;
                bool needsIndexRewrite = false;
                if (sqlite.TableNames.Contains(Name) == true)
                {
                    validateStoredRows = true;
                    DataTableView dtView = sqlite.GetTableManager(Name);
                    if (dtView.ColumnNames.Contains("Name"))
                    {
                        names = Array.ConvertAll(dtView.GetColumn("Name"), o => o.ToString());
                    }
                    if (dtView.ColumnNames.Contains("Type"))
                    {
                        types = Array.ConvertAll(dtView.GetColumn("Type"), o => o.ToString());
                    }
                }
                else
                {
                    // Opening from version 1.0
                    if (sqlite.TableNames.Contains("Bayesian Estimation Analysis"))
                    {
                        DataTableView dtView = sqlite.GetTableManager("Bayesian Estimation Analysis");
                        if (dtView.ColumnNames.Contains("Name"))
                        {
                            names = Array.ConvertAll(dtView.GetColumn("Name"), o => o.ToString());
                        }
                        types = new string[names.Length];
                        for (int i = 0; i < names.Length; i++)
                        {
                            types[i] = nameof(UnivariateAnalysis);
                        }
                    }

                }

                // Next get all of the elements
                var loadEntries = BuildLoadEntries(sqlite, names, types, validateStoredRows, out needsIndexRewrite);
                if (loadEntries.Count != 0)
                {
                    IUnivariate data;
                    // Open all non-Composite functions first
                    for (int i = 0; i < loadEntries.Count; i++)
                    {
                        string className = CollectionPersistenceHelper.GetClassName(loadEntries[i].ElementType);
                        if (className == nameof(UnivariateAnalysis))
                        {
                            data = new UnivariateAnalysis(loadEntries[i].ElementName, this, true);
                        }
                        else if (className == nameof(PointProcessAnalysis))
                        {
                            data = new PointProcessAnalysis(loadEntries[i].ElementName, this, true);
                        }
                        else if (className == nameof(MixtureAnalysis))
                        {
                            data = new MixtureAnalysis(loadEntries[i].ElementName, this, true);
                        }
                        else if (className == nameof(B17CAnalysis))
                        {
                            data = new B17CAnalysis(loadEntries[i].ElementName, this, true);
                        }
                        else
                        {
                            continue;
                        }
                        Add(data);
                    }
                    // Then open all composite functions
                    for (int i = 0; i < loadEntries.Count; i++)
                    {
                        if (CollectionPersistenceHelper.GetClassName(loadEntries[i].ElementType) == nameof(CompositeAnalysis))
                        {
                            data = new CompositeAnalysis(loadEntries[i].ElementName, this, true);
                            Insert(Math.Min(i, ElementList.Count), data);
                        }
                    }
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
        /// Adds an element to the collection.
        /// </summary>
        /// <param name="item">The element to add to the collection.</param>
        public override void Add(IElement item)
        {
            item.PropertyChanged += ElementPropertyChanged;
            item.Deleted += ElementDeleted;
            ElementList.Add((IUnivariate)item);
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
        /// Copies an element from an external project to disk within the current project.
        /// </summary>
        /// <param name="index">The zero-based index at which the element should be inserted.</param>
        /// <param name="elementName">The name of the element to copy.</param>
        /// <param name="elementType">The type of the element to copy.</param>
        /// <param name="fullFileName">The full file name of the project to copy from.</param>
        public override void InsertFromExternalProject(int index, string elementName, string elementType, string fullFileName)
        {
            // Cannot copy from external
            return;
        }

        /// <summary>
        /// Inserts an element into the collection at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index at which the element should be inserted.</param>
        /// <param name="item">The element to insert into the collection.</param>
        public override void Insert(int index, IElement item)
        {
            item.PropertyChanged += ElementPropertyChanged;
            item.Deleted += ElementDeleted;
            ElementList.Insert(index, (IUnivariate)item);
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
        /// Deletes the element collection and removes all data from disk.
        /// </summary>
        public override void Delete()
        {
            var sqlite = new SQLiteManager(ParentProject.FullFileName);
            sqlite.Open();
            sqlite.DeleteTable(Name);
            sqlite.DeleteTable(UnivariateAnalysis.CollectionName);
            sqlite.DeleteTable(PointProcessAnalysis.CollectionName);
            sqlite.DeleteTable(MixtureAnalysis.CollectionName);
            sqlite.DeleteTable(B17CAnalysis.CollectionName);
            sqlite.DeleteTable(CompositeAnalysis.CollectionName);
            sqlite.Close();
        }

    }
}
