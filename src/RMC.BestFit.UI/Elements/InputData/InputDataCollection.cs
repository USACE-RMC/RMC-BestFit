using DatabaseManager;
using FrameworkInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using RMC.BestFit.Models;

namespace RMC.BestFit.UI
{
    /// <summary>
    /// The input data collection.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// </remarks>
    public class InputDataCollection : ElementCollectionBase
    {
        /// <summary>
        /// Construct new element collection with name and parent project.
        /// </summary>
        /// <param name="parentProject">Parent project of the collection.</param>
        public InputDataCollection(IProject parentProject) : base(parentProject) { }

        /// <summary>
        /// The name of the element collection.
        /// </summary>
        public override string Name => "Input Data";

        /// <summary>
        /// Tracks whether the loaded collection table contained blank or duplicate rows.
        /// </summary>
        private bool _needsTableCompaction;

        /// <summary>
        /// Saves all input data elements in the collection to disk.
        /// </summary>
        /// <remarks>
        /// This method raises preview events for each element, allowing cancellation, and only saves elements that are marked as dirty.
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
                        ((InputData)ElementList[i]).RaisePreviewSaved(ref cancel);
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
                        ((InputData)ElementList[i]).RaisePreviewSaved(ref cancel);
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
        /// Loads all input data elements from disk into the collection.
        /// </summary>
        /// <remarks>
        /// This method reads the SQLite database table and creates InputData instances for each row found.
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
                        var element = new InputData(elementName, this, true);
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
        /// Adds an input data element to the collection.
        /// </summary>
        /// <param name="item">The InputData element to add to the collection.</param>
        /// <remarks>
        /// This method subscribes to property change and deletion events, saves the element to disk if needed, and raises the ElementAdded event.
        /// </remarks>
        public override void Add(IElement item)
        {
            item.PropertyChanged += ElementPropertyChanged;
            item.Deleted += ElementDeleted;
            ElementList.Add((InputData)item);
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
        /// Copies an input data element from an external project and inserts it into this collection at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index at which the element should be inserted.</param>
        /// <param name="elementName">The name of the element to copy.</param>
        /// <param name="elementType">The fully qualified type name of the element.</param>
        /// <param name="fullFileName">The full file path to the external project database.</param>
        public override void InsertFromExternalProject(int index, string elementName, string elementType, string fullFileName)
        {
            var sqlite = new SQLiteManager(fullFileName);
            if (CollectionPersistenceHelper.GetClassName(elementType) == nameof(InputData))
            {
                var element = new InputData(elementName, this);
                element.Open(sqlite);
                Insert(index, element);
            }
        }

        /// <summary>
        /// Inserts an input data element into the collection at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index at which the element should be inserted.</param>
        /// <param name="item">The InputData element to insert into the collection.</param>
        /// <remarks>
        /// This method subscribes to property change and deletion events, saves the element to disk if needed, and raises the ElementAdded event.
        /// </remarks>
        public override void Insert(int index, IElement item)
        {
            item.PropertyChanged += ElementPropertyChanged;
            item.Deleted += ElementDeleted;
            ElementList.Insert(index, (InputData)item);
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
        /// Deletes the entire input data collection and removes all associated data from disk.
        /// </summary>
        /// <remarks>
        /// This method permanently removes the collection's database table and all input data elements it contains.
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
