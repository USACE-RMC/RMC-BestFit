using DatabaseManager;
using FrameworkInterfaces;
using System;
using System.Linq;
using RMC.BestFit.Models;

namespace RMC.BestFit.UI
{
    /// <summary>
    /// The time series collection.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// </remarks>
    public class TimeSeriesCollection : ElementCollectionBase
    {
        /// <summary>
        /// Construct new element collection with name and parent project.
        /// </summary>
        /// <param name="parentProject">Parent project of the collection.</param>
        public TimeSeriesCollection(IProject parentProject) : base(parentProject) { }

        /// <summary>
        /// The name of the element collection.
        /// </summary>
        public override string Name => "Time Series Data";

        /// <summary>
        /// Tracks whether the loaded collection table contained blank or duplicate rows.
        /// </summary>
        private bool _needsTableCompaction;

        /// <summary>
        /// Save all elements in the collection to disk.
        /// </summary>
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
                        ((TimeSeriesElement)ElementList[i]).RaisePreviewSaved(ref cancel);
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
                        ((TimeSeriesElement)ElementList[i]).RaisePreviewSaved(ref cancel);
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
        /// Load all elements into the collection from disk.
        /// </summary>
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
                        var element = new TimeSeriesElement(elementName, this, true);
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
        /// Add element to the collection.
        /// </summary>
        /// <param name="item">Element to add.</param>
        public override void Add(IElement item)
        {
            item.PropertyChanged += ElementPropertyChanged;
            item.Deleted += ElementDeleted;
            ElementList.Add((TimeSeriesElement)item);
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
        /// Copy the element from an external project to disk within the current project.
        /// </summary>
        /// <param name="index">The zero-based index at which the element should be inserted.</param>
        /// <param name="elementName">The element name.</param>
        /// <param name="elementType">The element type.</param>
        /// <param name="fullFileName">The full file name of the project to copy from.</param>
        public override void InsertFromExternalProject(int index, string elementName, string elementType, string fullFileName)
        {
            var sqlite = new SQLiteManager(fullFileName);
            if (CollectionPersistenceHelper.GetClassName(elementType) == nameof(TimeSeriesElement))
            {
                var element = new TimeSeriesElement(elementName, this);
                element.Open(sqlite);
                Insert(index, element);
            }
        }

        /// <summary>
        /// Inserts an element into the collection at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index at which the element should be inserted.</param>
        /// <param name="item">Element to insert.</param>
        public override void Insert(int index, IElement item)
        {
            item.PropertyChanged += ElementPropertyChanged;
            item.Deleted += ElementDeleted;
            ElementList.Insert(index, (TimeSeriesElement)item);
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
        /// Delete the element collection and remove all data from disk.
        /// </summary>
        public override void Delete()
        {
            var sqlite = new SQLiteManager(ParentProject.FullFileName);
            sqlite.Open();
            sqlite.DeleteTable(Name);
            sqlite.Close();
        }

    }
}
