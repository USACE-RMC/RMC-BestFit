using DatabaseManager;
using FrameworkInterfaces;
using System;
using System.Linq;
using RMC.BestFit.Models;

namespace RMC.BestFit.UI
{

    /// <summary>
    /// Represents a collection of rating curve analysis elements within a project.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     <b>Authors:</b>
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    ///     This collection manages multiple rating curve analysis instances, providing methods for adding,
    ///     removing, loading, and saving analysis elements to and from the project database.
    /// </para>
    /// </remarks>
    public class RatingCurveAnalysisCollection : ElementCollectionBase
    {
        /// <summary>
        /// Constructs a new rating curve analysis collection with the specified parent project.
        /// </summary>
        /// <param name="parentProject">The parent project that contains this collection.</param>
        public RatingCurveAnalysisCollection(IProject parentProject) : base(parentProject) { }

        /// <summary>
        /// Gets the name of the element collection.
        /// </summary>
        public override string Name => "Rating Curve Analysis";

        /// <summary>
        /// Tracks whether the loaded collection table contained blank or duplicate rows.
        /// </summary>
        private bool _needsTableCompaction;

        /// <summary>
        /// Saves all elements in the collection to the project database file on disk.
        /// </summary>
        /// <remarks>
        /// This method raises a preview saved event before saving, allowing cancellation of the save operation.
        /// Only elements marked as dirty are saved to disk. Plot settings are always saved for all elements.
        /// </remarks>
        public override void Save()
        {
            bool cancel = false;
            RaisePreviewObjectSaved(this, ref cancel);
            if (cancel == true) return;

            _savingAll = true;
            try
            {
                if (_needsTableCompaction)
                {
                    for (int i = 0; i < ElementList.Count; i++)
                    {
                        ((RatingCurveAnalysis)ElementList[i]).RaisePreviewSaved(ref cancel);
                        if (cancel == true) return;
                    }

                    var sqlite = new SQLiteManager(ParentProject.FullFileName);
                    sqlite.Open();
                    try
                    {
                        if (sqlite.TableNames.Contains(Name))
                        {
                            sqlite.DeleteTable(Name);
                        }
                    }
                    finally
                    {
                        if (sqlite.DataBaseOpen) sqlite.Close();
                    }

                    for (int i = 0; i < ElementList.Count; i++)
                    {
                        ElementList[i].Save();
                    }

                    _needsTableCompaction = false;
                }
                else
                {
                    // Save all elements.
                    for (int i = 0; i < ElementList.Count; i++)
                    {
                        // Always raise preview saved. This allows the plot settings to always be saved.
                        ((RatingCurveAnalysis)ElementList[i]).RaisePreviewSaved(ref cancel);
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
        /// Loads all rating curve analysis elements from the project database file on disk into the collection.
        /// </summary>
        /// <remarks>
        /// This method opens the SQLite database and reads all rows from the rating curve analysis table,
        /// creating a new RatingCurveAnalysis instance for each row and adding it to the collection.
        /// </remarks>
        public override void Open()
        {
            _opening = true;
            var sqlite = new SQLiteManager(ParentProject.FullFileName);
            sqlite.Open();
            try
            {
                if (sqlite.TableNames.Contains(Name) == true)
                {
                    var dtView = sqlite.GetTableManager(Name);
                    bool needsRewrite;
                    foreach (string elementName in CollectionPersistenceHelper.BuildSingleTableLoadEntries(dtView, out needsRewrite))
                    {
                        var element = new RatingCurveAnalysis(elementName, this, true);
                        Add(element);
                    }

                    _needsTableCompaction = needsRewrite;
                }
                sqlite.Close();
                SetIsDirty(_needsTableCompaction);
            }
            finally
            {
                if (sqlite.DataBaseOpen) sqlite.Close();
                _opening = false;
            }
        }

        /// <summary>
        /// Adds a rating curve analysis element to the collection.
        /// </summary>
        /// <param name="item">The rating curve analysis element to add.</param>
        /// <remarks>
        /// This method subscribes to the element's PropertyChanged and Deleted events.
        /// If the collection is not in the process of opening, the element is immediately saved to disk.
        /// </remarks>
        public override void Add(IElement item)
        {
            item.PropertyChanged += ElementPropertyChanged;
            item.Deleted += ElementDeleted;
            ElementList.Add((RatingCurveAnalysis)item);
            if (_opening == false)
            {
                var sqlite = new SQLiteManager(ParentProject.FullFileName);
                sqlite.Open();
                if (CollectionPersistenceHelper.NamedRowExists(sqlite, Name, item.Name) == false)
                {
                    item.Save();
                }
                sqlite.Close();
                SetIsDirty(true);
            }
            RaiseElementAddedEvent(item);
        }

        /// <summary>
        /// Copies a rating curve analysis element from an external project database and inserts it into the collection.
        /// </summary>
        /// <param name="index">The zero-based index at which the element should be inserted.</param>
        /// <param name="elementName">The name of the element to copy from the external project.</param>
        /// <param name="elementType">The fully qualified type name of the element. Must match RatingCurveAnalysis.</param>
        /// <param name="fullFileName">The full file path of the external project database.</param>
        /// <remarks>
        /// The element is loaded from the external database and inserted at the specified index in the collection.
        /// </remarks>
        public override void InsertFromExternalProject(int index, string elementName, string elementType, string fullFileName)
        {
            var sqlite = new SQLiteManager(fullFileName);
            if (CollectionPersistenceHelper.GetClassName(elementType) == nameof(RatingCurveAnalysis))
            {
                var element = new RatingCurveAnalysis(elementName, this);
                element.Open(sqlite);
                Insert(index, element);
            }
        }

        /// <summary>
        /// Inserts a rating curve analysis element into the collection at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index at which the element should be inserted.</param>
        /// <param name="item">The rating curve analysis element to insert.</param>
        /// <remarks>
        /// This method subscribes to the element's PropertyChanged and Deleted events.
        /// If the collection is not in the process of opening, the element is immediately saved to disk.
        /// </remarks>
        public override void Insert(int index, IElement item)
        {
            item.PropertyChanged += ElementPropertyChanged;
            item.Deleted += ElementDeleted;
            ElementList.Insert(index, (RatingCurveAnalysis)item);
            if (_opening == false)
            {
                var sqlite = new SQLiteManager(ParentProject.FullFileName);
                sqlite.Open();
                if (CollectionPersistenceHelper.NamedRowExists(sqlite, Name, item.Name) == false)
                {
                    item.Save();
                }
                sqlite.Close();
                SetIsDirty(true);
            }
            RaiseElementAddedEvent(item);
        }

        /// <summary>
        /// Deletes the entire rating curve analysis collection table from the project database.
        /// </summary>
        /// <remarks>
        /// This method permanently removes all rating curve analysis data from the database.
        /// Use with caution as this operation cannot be undone.
        /// </remarks>
        public override void Delete()
        {
            var sqlite = new SQLiteManager(ParentProject.FullFileName);
            sqlite.Open();
            sqlite.DeleteTable(Name);
            sqlite.Close();
        }

    }
}
