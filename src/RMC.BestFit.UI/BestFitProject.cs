using DatabaseManager;
using FrameworkInterfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using RMC.BestFit.Models;

namespace RMC.BestFit.UI
{

    /// <summary>
    /// The main project class. This class is a singleton that inherits from <see cref="ProjectBase"/>
    /// to gain undo/redo support for user-editable properties.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// </remarks>
    [Category("Project")]
    [DisplayName("RMC-BestFit Project")]
    [Description("The RMC-BestFit project includes time series data, input data, distribution fitting analyses, and Bayesian analyses for both univariate and bivariate distributions.")]
    [Browsable(true)]
    public class BestFitProject : ProjectBase
    {
        #region Construction

        /// <summary>
        /// Constructs a new instance of the <see cref="BestFitProject"/> class.
        /// </summary>
        private BestFitProject()
        {
            // Add messages
            _messages = new List<BasicMessageItem>();
            _messenger = FrameworkInterfaces.Messaging.Messenger.GetInstance();
            _descriptionMsg = new BasicMessageItem(MessageType.Message, "The project does not have a description.", this, "Project", Name, nameof(Description), "P-MSG-001");
            _noNameMsg = new BasicMessageItem(MessageType.Error, "The name of the project cannot be blank.", this, "Project", Name, nameof(Name), "P-ERR-001");
            _longNameMsg = new BasicMessageItem(MessageType.Error, "The name of the project cannot exceed 50 characters.", this, "Project", Name, nameof(Name), "P-ERR-002");
            _dupNameMsg = new BasicMessageItem(MessageType.Error, $"A project with the name '{Name}' already exists in this directory and must be unique.", this, "Project", Name, nameof(Name), "P-ERR-003");
            _badCharMsg = new BasicMessageItem(MessageType.Error, "Invalid character in project name.", this, "Project", Name, nameof(Name), "P-ERR-004");

            // Add collections
            _timeSeriesCollection = new TimeSeriesCollection(this);
            _inputDataCollection = new InputDataCollection(this);
            _fittingAnalysisCollection = new FittingAnalysisCollection(this);
            _univariateAnalysisCollection = new UnivariateAnalysisCollection(this);
            _bivariateAnalysisCollection = new BivariateAnalysisCollection(this);
            _ratingCurveAnalysisCollection = new RatingCurveAnalysisCollection(this);
            _timeSeriesAnalysisCollection = new TimeSeriesAnalysisCollection(this);
            //
            _readOnlyElementCollections = new ReadOnlyCollection<IElementCollection>(new IElementCollection[]
            { _timeSeriesCollection,
              _inputDataCollection,
              _fittingAnalysisCollection,
              _univariateAnalysisCollection,
              _bivariateAnalysisCollection,
              _ratingCurveAnalysisCollection,
              _timeSeriesAnalysisCollection
            });

            // Subscribe to collection saved events
            SubscribeCollectionEvents();

        }

        /// <summary>
        /// Returns the singleton instance of the <see cref="BestFitProject"/> class.
        /// </summary>
        /// <returns>The singleton <see cref="BestFitProject"/> instance.</returns>
        /// <remarks>
        /// Uses <see cref="System.Threading.LazyThreadSafetyMode.ExecutionAndPublication"/> so that
        /// concurrent first-callers (e.g., a background <see cref="TimeSeriesElement"/> constructor
        /// followed by the dispatcher constructor) do not race to create two project instances.
        /// </remarks>
        public static BestFitProject GetInstance() => _lazyInstance.Value;


        #endregion

        #region Members

        /// <summary>
        /// The name of the SQLite table storing project metadata.
        /// </summary>
        private string _tableName = "Project";

        /// <summary>
        /// Thread-safe singleton holder. <see cref="System.Threading.LazyThreadSafetyMode.ExecutionAndPublication"/>
        /// ensures the constructor runs exactly once even under concurrent first-access.
        /// </summary>
        private static readonly System.Lazy<BestFitProject> _lazyInstance =
            new System.Lazy<BestFitProject>(
                () => new BestFitProject(),
                System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);

        /// <summary>
        /// Indicates whether the project name is valid.
        /// </summary>
        private bool _nameValid = false;

        /// <summary>
        /// The array of invalid characters that cannot be used in project names.
        /// </summary>
        private static readonly char[] _InvalidNameCharacters = new List<char>(Path.GetInvalidFileNameChars()) { '\'', '[', ']' }.ToArray();

        /// <summary>
        /// Delegate method to raise event when a version 1.0 project is opened.
        /// </summary>
        /// <param name="isVersion1">Determines if opening from version 1.0.</param>
        /// <param name="cancel">Determines if opening from version 1.0 should be canceled.</param>
        public delegate void Version1EventHandler(bool isVersion1, ref bool cancel);

        /// <summary>
        /// Occurs when a version 1.0 project is opened.
        /// </summary>
        public event Version1EventHandler OpenedVersion1;

        /// <summary>
        /// The list of message items for validation and error reporting.
        /// </summary>
        private List<BasicMessageItem> _messages;

        /// <summary>
        /// The messenger instance for managing and broadcasting messages.
        /// </summary>
        private FrameworkInterfaces.Messaging.Messenger _messenger;

        /// <summary>
        /// Message item for missing project description.
        /// </summary>
        private BasicMessageItem _descriptionMsg;

        /// <summary>
        /// Error message item for blank project name.
        /// </summary>
        private BasicMessageItem _noNameMsg;

        /// <summary>
        /// Error message item for project name exceeding maximum length.
        /// </summary>
        private BasicMessageItem _longNameMsg;

        /// <summary>
        /// Error message item for duplicate project name in directory.
        /// </summary>
        private BasicMessageItem _dupNameMsg;

        /// <summary>
        /// Error message item for invalid characters in project name.
        /// </summary>
        private BasicMessageItem _badCharMsg;

        /// <summary>
        /// The collection of time series data elements.
        /// </summary>
        private TimeSeriesCollection _timeSeriesCollection;

        /// <summary>
        /// The collection of input data elements.
        /// </summary>
        private InputDataCollection _inputDataCollection;

        /// <summary>
        /// The collection of fitting analysis elements.
        /// </summary>
        private FittingAnalysisCollection _fittingAnalysisCollection;

        /// <summary>
        /// The collection of univariate analysis elements.
        /// </summary>
        private UnivariateAnalysisCollection _univariateAnalysisCollection;

        /// <summary>
        /// The collection of bivariate analysis elements.
        /// </summary>
        private BivariateAnalysisCollection _bivariateAnalysisCollection;

        /// <summary>
        /// The collection of rating curve analysis elements.
        /// </summary>
        private RatingCurveAnalysisCollection _ratingCurveAnalysisCollection;

        /// <summary>
        /// The collection of time series analysis elements.
        /// </summary>
        private TimeSeriesAnalysisCollection _timeSeriesAnalysisCollection;

        /// <summary>
        /// Gets or sets the project name.
        /// </summary>
        [Category("Meta Data")]
        [DisplayName("Name")]
        [Description("The name of the project.")]
        [Browsable(true)]
        public override string Name
        {
            get { return _name; }
            set
            {
                if (_name != value)
                {
                    var oldValue = _name;
                    foreach (var item in _messages)
                        item.SourceName = value;
                    _name = value;

                    ValidateName();

                    RecordPropertyChange(nameof(Name), oldValue, value);
                }
            }
        }

        /// <summary>
        /// Gets or sets the project description.
        /// </summary>
        [Category("Meta Data")]
        [DisplayName("Description")]
        [Description("The description for the project.")]
        [Browsable(true)]
        public override string Description
        {
            get { return _description; }
            set
            {
                if (_description != value)
                {
                    var oldValue = _description;
                    _description = value;
                    if (string.IsNullOrEmpty(_description))
                        _messenger.Add(_descriptionMsg);
                    else
                        _messenger.Remove(_descriptionMsg);
                    RecordPropertyChange(nameof(Description), oldValue, value);
                }
            }
        }

        /// <summary>
        /// Determines if the project is valid.
        /// </summary>
        /// <returns><c>true</c> if the project name passes all validation checks; otherwise, <c>false</c>.</returns>
        public override bool IsValid()
        {
            return _nameValid;
        }

        /// <summary>
        /// Gets the version of the software the project was last edited with.
        /// </summary>
        [Category("Meta Data")]
        [DisplayName("Software Version")]
        [Description("The version of RMC-BestFit used to last modify the project file.")]
        [Browsable(true)]
        public override string SoftwareVersion
        {
            get { return "2.0.0"; }
        }

        private static readonly System.Lazy<System.Windows.Media.ImageSource> s_projectIcon = new(() =>
        {
            try
            {
                var img = new System.Windows.Media.Imaging.BitmapImage(new System.Uri("pack://application:,,,/RMC.BestFit.UI;component/Resources/BestFit_Icon.ico"));
                img.Freeze();
                return img;
            }
            catch (System.Exception ex)
            {
                // pack:// URIs require a WPF Application instance. Unit tests and
                // non-WPF hosts (pythonnet, batch CLIs) hit this. Return null rather
                // than throwing so the project model is still usable in those contexts.
                System.Diagnostics.Debug.WriteLine($"BestFitProject.s_projectIcon: {ex.Message}");
                return null;
            }
        });

        /// <summary>
        /// Gets the project image as an <see cref="System.Windows.Media.ImageSource"/>. Used by the
        /// project tree node to render the project icon. Returns <c>null</c> when running outside a
        /// WPF Application context (e.g. unit tests, pythonnet); the project model itself remains
        /// fully usable.
        /// </summary>
        public override System.Windows.Media.ImageSource ProjectImage => s_projectIcon.Value;

        /// <summary>
        /// Shared property to get the default invalid characters for project names.
        /// Invalid characters include invalid file name characters, apostrophe, left bracket, and right bracket.
        /// </summary>
        public static char[] InvalidNameCharacters
        {
            get { return _InvalidNameCharacters; }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Enumerates all element collections owned by the project.
        /// Used by the subscribe / unsubscribe helpers to avoid repeating the list of seven.
        /// </summary>
        private IEnumerable<IElementCollection> AllCollections()
        {
            yield return _timeSeriesCollection;
            yield return _inputDataCollection;
            yield return _fittingAnalysisCollection;
            yield return _univariateAnalysisCollection;
            yield return _bivariateAnalysisCollection;
            yield return _ratingCurveAnalysisCollection;
            yield return _timeSeriesAnalysisCollection;
        }

        /// <summary>
        /// Subscribes to every per-collection event the project needs to observe.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Called from the constructor and after <see cref="Open"/> / <see cref="CreateNew"/> /
        /// <see cref="CreateNewDummyProject"/> to (re)establish event handlers after
        /// <see cref="Close"/> detaches them.
        /// </para>
        /// <para>
        /// Three subscriptions per collection:
        /// <list type="bullet">
        /// <item><description><see cref="ISave.ObjectSaved"/> — drives the
        ///   <see cref="ProjectBase.ElementCollection_Saved"/> cascade that stamps the
        ///   project's <see cref="IMetaData.LastModified"/> after any child actually saves.</description></item>
        /// <item><description><see cref="IElementCollection.ElementIsDirtyChanged"/> — aggregates
        ///   element-level dirty transitions into <see cref="ISave.IsDirty"/> at the project level.</description></item>
        /// <item><description><see cref="INotifyPropertyChanged.PropertyChanged"/> — observes the
        ///   collection's own <see cref="ISave.IsDirty"/> transitions (structural changes:
        ///   Add / Remove / Move / Sort) and aggregates those into the project too.</description></item>
        /// </list>
        /// </para>
        /// </remarks>
        private void SubscribeCollectionEvents()
        {
            foreach (var collection in AllCollections())
            {
                collection.ObjectSaved += ElementCollection_Saved;
                collection.ElementIsDirtyChanged += OnElementIsDirtyChanged;
                collection.PropertyChanged += OnCollectionPropertyChanged;
            }
        }

        /// <summary>
        /// Unsubscribes from every per-collection event subscribed by
        /// <see cref="SubscribeCollectionEvents"/>.
        /// </summary>
        /// <remarks>
        /// Called from <see cref="Close"/> to ensure a symmetric subscribe / unsubscribe lifecycle.
        /// Missing any of these would leak handlers and keep the project reachable after close.
        /// </remarks>
        private void UnsubscribeCollectionEvents()
        {
            foreach (var collection in AllCollections())
            {
                collection.ObjectSaved -= ElementCollection_Saved;
                collection.ElementIsDirtyChanged -= OnElementIsDirtyChanged;
                collection.PropertyChanged -= OnCollectionPropertyChanged;
            }
        }

        /// <summary>
        /// Handles <see cref="IElementCollection.ElementIsDirtyChanged"/> from any
        /// owned collection. The collection raises this when one of its elements
        /// transitions from <see cref="ISave.IsDirty"/> <c>false</c> to <c>true</c>;
        /// the project aggregates that into its own <see cref="ISave.IsDirty"/> so
        /// Save-button gates and "needs save" bindings can rely on a single flag.
        /// </summary>
        /// <remarks>
        /// Short-circuits while the project is loading (<see cref="ProjectBase._openingProject"/>)
        /// — any transient dirty events fired during deserialization are not user edits.
        /// </remarks>
        private void OnElementIsDirtyChanged(object sender, EventArgs e)
        {
            if (_openingProject) return;
            SetIsDirty(true);
        }

        /// <summary>
        /// Handles <see cref="INotifyPropertyChanged.PropertyChanged"/> from any owned
        /// collection, aggregating the collection's own <see cref="ISave.IsDirty"/>
        /// transitions (structural changes: Add / Remove / Move / Sort) into the project.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Only responds to <c>nameof(IsDirty)</c> events where the collection is now dirty.
        /// Other property names (e.g., forwarded element <c>Name</c> changes) and
        /// <c>false</c> transitions are ignored — <c>Save()</c> handles its own cleanup
        /// at each level.
        /// </para>
        /// <para>
        /// Short-circuits while the project is loading (<see cref="ProjectBase._openingProject"/>).
        /// </para>
        /// </remarks>
        private void OnCollectionPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_openingProject) return;
            if (e.PropertyName != nameof(IElementCollection.IsDirty)) return;
            if (sender is IElementCollection collection && collection.IsDirty)
                SetIsDirty(true);
        }

        /// <summary>
        /// Validates the project name against all naming rules and updates validation messages.
        /// </summary>
        /// <remarks>
        /// Checks the following conditions:
        /// <list type="bullet">
        /// <item><description>Name cannot be blank or null.</description></item>
        /// <item><description>Name cannot exceed 50 characters.</description></item>
        /// <item><description>Name must be unique in the project directory.</description></item>
        /// <item><description>Name cannot contain invalid file name characters.</description></item>
        /// </list>
        /// </remarks>
        private void ValidateName()
        {
            _nameValid = true;

            // Check if name is nothing.
            if (Name == "" || Name == null)
            {
                _nameValid = false;
                _messenger.Add(_noNameMsg);
            }
            else
            {
                _messenger.Remove(_noNameMsg);
            }

            // Check the length of the name.
            if (Name != null && Name.Length > 50)
            {
                _nameValid = false;
                _messenger.Add(_longNameMsg);
            }
            else
            {
                _messenger.Remove(_longNameMsg);
            }

            // Get list of invalid names, can't allow duplicate naming.
            if (FileDirectory != null)
            {
                string[] fullfilepaths = Directory.GetFiles(FileDirectory, "*.bestfit");
                List<string> invalidNames = new List<string>();
                for (int i = 0; i < fullfilepaths.Count(); i++)
                {
                    if (fullfilepaths[i] != FullFileName)
                        invalidNames.Add(Path.GetFileNameWithoutExtension(fullfilepaths[i]));
                }

                if (invalidNames.Contains(Name, StringComparer.OrdinalIgnoreCase))
                {
                    _nameValid = false;
                    _dupNameMsg.Description = $"A project with the name '{Name}' already exists in this directory and must be unique.";
                    _messenger.Add(_dupNameMsg);
                }
                else
                {
                    _messenger.Remove(_dupNameMsg);
                }
            }

            // Check if there are bad characters.
            _messenger.Remove(_badCharMsg);
            if (Name != null)
            {
                foreach (char badChar in BestFitProject.InvalidNameCharacters)
                {
                    if (Name.Contains(badChar))
                    {
                        _nameValid = false;
                        string badCharacters = "<>:" + (char)34 + @"/\|?*";
                        _badCharMsg.Description = $"Invalid character in project name: '{badChar}'. Invalid characters are: {badCharacters}";
                        _messenger.Add(_badCharMsg);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Gets the required columns for the SQLite project table.
        /// </summary>
        /// <remarks>
        /// If you want to add a new column, add it to the end of the dictionary to maintain
        /// backward compatibility with existing project files.
        /// </remarks>
        private static Dictionary<string, Type> RequiredColumns { get; } = new Dictionary<string, Type>() {
            { nameof(Name), typeof(string) },
            { nameof(Description), typeof(string) },
            { nameof(CreationDate), typeof(string) },
            { nameof(LastModified), typeof(string) },
            { nameof(FullFileName), typeof(string) },
            { nameof(SoftwareVersion), typeof(string) },
            { nameof(AvalonDockLayout), typeof(string) },
            { nameof(ProjectExplorerLayout), typeof(string) },
            // Dual-buffer backups: before overwriting either layout column we copy
            // the previous valid value here, so the Load path can roll back to the
            // last-known-good layout if the current column is corrupted or parses empty.
            { AvalonDockLayoutPreviousColumn, typeof(string) },
            { ProjectExplorerLayoutPreviousColumn, typeof(string) } };

        /// <summary>
        /// SQLite column name storing the last-known-good <see cref="ProjectBase.AvalonDockLayout"/>.
        /// </summary>
        private const string AvalonDockLayoutPreviousColumn = "AvalonDockLayoutPrevious";

        /// <summary>
        /// SQLite column name storing the last-known-good <see cref="ProjectBase.ProjectExplorerLayout"/>.
        /// </summary>
        private const string ProjectExplorerLayoutPreviousColumn = "ProjectExplorerLayoutPrevious";

        /// <summary>
        /// Creates or updates the SQLite project table with required columns.
        /// </summary>
        /// <param name="sqlite">The SQLite database manager instance.</param>
        /// <remarks>
        /// This method ensures the project table exists with all required columns. If the table doesn't exist,
        /// it creates it. If the table exists but is missing columns, it adds them. This supports forward
        /// compatibility when opening older project files.
        /// </remarks>
        private void CreateTable(SQLiteManager sqlite)
        {
            if (sqlite.TableNames.Contains(_tableName) == false)
            {
                // If the table does not exist, then create the table
                var dataTable = new DataTable(_tableName);
                foreach (KeyValuePair<string, Type> column in RequiredColumns)
                    dataTable.Columns.Add(column.Key, column.Value);
                sqlite.SaveDataTable(dataTable);

            }
            else
            {
                // Add any required columns that don't exist
                var dt = sqlite.GetTableManager(_tableName);
                int columnIndex;
                foreach (KeyValuePair<string, Type> column in RequiredColumns)
                {
                    columnIndex = Array.IndexOf(dt.ColumnNames, column.Key);
                    // If the column doesn't exist in the database then create it.
                    if (columnIndex < 0)
                    {
                        dt.AddColumn(column.Key, column.Value);
                    }
                    else
                    {
                        if (dt.ColumnTypes[columnIndex] != column.Value)
                        {
                            dt.DeleteColumn(columnIndex);
                            dt.AddColumn(column.Key, column.Value);
                        }
                    }
                }
                dt.ApplyEdits();
            }
        }

        /// <summary>
        /// Creates a new project file.
        /// </summary>
        /// <param name="newFullFileName">Full project file name.</param>
        public override void CreateNew(string newFullFileName)
        {
            IsUndoEnabled = false;
            try
            {
                // Set project meta data and properties
                FullFileName = newFullFileName;
                Name = Path.GetFileNameWithoutExtension(newFullFileName);
                Description = "";
                CreationDate = DateTime.Now;
                LastModified = DateTime.Now;

                // Load element collections
                _readOnlyElementCollections = new ReadOnlyCollection<IElementCollection>(new IElementCollection[]
                { _timeSeriesCollection,
                  _inputDataCollection,
                  _fittingAnalysisCollection,
                  _univariateAnalysisCollection,
                  _bivariateAnalysisCollection,
                  _ratingCurveAnalysisCollection,
                  _timeSeriesAnalysisCollection
                });

                // Re-subscribe to collection events
                SubscribeCollectionEvents();

                Save();
            }
            finally
            {
                IsUndoEnabled = true;
                ClearUndoHistory();
            }
        }

        /// <summary>
        /// Creates a new dummy project.
        /// </summary>
        public void CreateNewDummyProject()
        {
            IsUndoEnabled = false;
            try
            {
                // Set project meta data and properties
                FullFileName = System.IO.Path.GetTempPath() + Guid.NewGuid().ToString() + ".bestfit";
                Name = "Blank Project";
                Description = "This is a blank project file.";
                CreationDate = DateTime.Now;
                LastModified = DateTime.Now;

                // Load element collections
                _readOnlyElementCollections = new ReadOnlyCollection<IElementCollection>(new IElementCollection[]
                { _timeSeriesCollection,
                  _inputDataCollection,
                  _fittingAnalysisCollection,
                  _univariateAnalysisCollection,
                  _bivariateAnalysisCollection,
                  _ratingCurveAnalysisCollection,
                  _timeSeriesAnalysisCollection
                });

                // Re-subscribe to collection events
                SubscribeCollectionEvents();

                Save();
            }
            finally
            {
                IsUndoEnabled = true;
                ClearUndoHistory();
            }
        }


        /// <summary>
        /// Opens an existing project file.
        /// </summary>
        public override void Open()
        {
            _openingProject = true;
            IsUndoEnabled = false;
            SetIsDirty(false);

            bool userCanceledLegacyMigration = false;

            try
            {
                bool cancel = false;
                string version = "";

                var sqlite = new SQLiteManager(FullFileName);
                sqlite.Open();

                // The user might have renamed the SQLite file from Windows Explorer.
                // In this case, the FullFileName will not match the meta data stored within the database.
                // We need to check if the values are different, and if so, update the meta data table.
                string winExpName = Path.GetFileNameWithoutExtension(FullFileName);
                string winExpFullFileName = FullFileName;

                var dtView = sqlite.GetTableManager(_tableName);
                if (dtView.NumberOfRows != 1)
                {
                    sqlite.Close();
                }
                else
                {
                    // Get name and full file name using backing fields to avoid undo recording
                    if (dtView.ColumnNames.Contains(nameof(Name))) _name = dtView.GetCell(nameof(Name), 0).ToString();
                    if (dtView.ColumnNames.Contains(nameof(FullFileName))) _fullFileName = dtView.GetCell(nameof(FullFileName), 0).ToString();

                    // Check if names are different (use property setters for validation)
                    if (FullFileName != winExpFullFileName)
                    {
                        FullFileName = winExpFullFileName;
                        dtView.EditCell(0, nameof(FullFileName), winExpFullFileName);
                        dtView.ApplyEdits();
                    }
                    if (_name != winExpName)
                    {
                        _name = winExpName;
                        foreach (var item in _messages)
                            item.SourceName = _name;
                        ValidateName();
                        dtView.EditCell(0, nameof(Name), winExpName);
                        dtView.ApplyEdits();
                    }
                    // Get the rest of the properties using backing fields
                    if (dtView.ColumnNames.Contains(nameof(Description))) _description = dtView.GetCell(nameof(Description), 0).ToString();
                    if (dtView.ColumnNames.Contains(nameof(CreationDate))) CreationDate = FrameworkInterfaces.Utilities.Tools.DateFromString(dtView.GetCell(nameof(CreationDate), 0).ToString()) ?? DateTime.MinValue;
                    if (dtView.ColumnNames.Contains(nameof(LastModified))) LastModified = FrameworkInterfaces.Utilities.Tools.DateFromString(dtView.GetCell(nameof(LastModified), 0).ToString()) ?? DateTime.MinValue;
                    if (dtView.ColumnNames.Contains(nameof(AvalonDockLayout))) AvalonDockLayout = dtView.GetCell(nameof(AvalonDockLayout), 0).ToString();
                    if (dtView.ColumnNames.Contains(nameof(ProjectExplorerLayout))) _projectExplorerLayout = dtView.GetCell(nameof(ProjectExplorerLayout), 0).ToString();
                    // Dual-buffer backups. These columns only exist on projects saved by
                    // versions of BestFit with the crash-recovery patch; older files will
                    // simply leave them empty until the next save.
                    if (dtView.ColumnNames.Contains(AvalonDockLayoutPreviousColumn))
                        _avalonDockLayoutPrevious = dtView.GetCell(AvalonDockLayoutPreviousColumn, 0)?.ToString() ?? string.Empty;
                    if (dtView.ColumnNames.Contains(ProjectExplorerLayoutPreviousColumn))
                        _projectExplorerLayoutPrevious = dtView.GetCell(ProjectExplorerLayoutPreviousColumn, 0)?.ToString() ?? string.Empty;
                    // Opening from disk is not a layout edit; clear the flag that the
                    // AvalonDockLayout setter above may have just flipped.
                    LayoutDirty = false;

                    // Check the version
                    if (dtView.ColumnNames.Contains(nameof(BestFitProject.SoftwareVersion))) version = dtView.GetCell(nameof(BestFitProject.SoftwareVersion), 0).ToString();
                    if (version == "1.0")
                    {
                        // Raise the opened from version 1.0 event.
                        OpenedVersion1?.Invoke(true, ref cancel);
                    }

                    sqlite.Close();
                }

                // Exit if the user canceled opening from version 1.0.
                // Cleanup happens in the outer finally; CreateNewDummyProject is called
                // after the finally so it can install fresh state without fighting cleanup.
                if (cancel == true)
                {
                    userCanceledLegacyMigration = true;
                    return;
                }

                // Validate description message
                if (_description == "" || _description == null)
                    _messenger.Add(_descriptionMsg);
                else
                    _messenger.Remove(_descriptionMsg);

                // Load element collections
                _timeSeriesCollection.Open();
                _inputDataCollection.Open();
                _fittingAnalysisCollection.Open();
                _univariateAnalysisCollection.Open();
                _bivariateAnalysisCollection.Open();
                _ratingCurveAnalysisCollection.Open();
                _timeSeriesAnalysisCollection.Open();
                // Spatial Extremes

                // Add to list
                _readOnlyElementCollections = new ReadOnlyCollection<IElementCollection>(new IElementCollection[]
                { _timeSeriesCollection,
                  _inputDataCollection,
                  _fittingAnalysisCollection,
                  _univariateAnalysisCollection,
                  _bivariateAnalysisCollection,
                  _ratingCurveAnalysisCollection,
                  _timeSeriesAnalysisCollection
                });

                // Re-subscribe to collection events after loading
                SubscribeCollectionEvents();

                NameOnDisk = Name;

                // Raise PropertyChanged for properties loaded via backing fields
                // so that WPF bindings (e.g., ProjectNode header) update correctly.
                RaisePropertyChange(nameof(Name), false);
                RaisePropertyChange(nameof(Description), false);
            }
            finally
            {
                _openingProject = false;
                IsUndoEnabled = true;
                ClearUndoHistory();
                SetIsDirty(false);
            }

            if (userCanceledLegacyMigration) CreateNewDummyProject();
        }

        /// <summary>
        /// Saves the project to disk.
        /// </summary>
        /// <remarks>
        /// Two paths are possible:
        /// <list type="bullet">
        /// <item><description><b>Layout-only save</b> (<c>IsDirty == false</c> and <c>LayoutDirty == true</c>):
        /// only the two layout columns are rewritten. <c>LastModified</c> is NOT stamped and the element
        /// collections are not iterated. This lets window-layout changes persist without drifting the
        /// project's modification timestamp.</description></item>
        /// <item><description><b>Full save</b> (all other cases): project row, layout, and element
        /// collections all written. <c>LastModified</c> is stamped only when <c>IsDirty == true</c>;
        /// otherwise it is left unchanged so that saving a project with only dirty children stamps
        /// <c>LastModified</c> via the <c>ElementCollection_Saved</c> cascade instead of here.</description></item>
        /// </list>
        /// </remarks>
        public override void Save()
        {
            if (FullFileName == null) return;

            bool layoutOnly = !IsDirty && LayoutDirty;

            // Filename-rename check is meaningful only when we are writing project meta.
            if (!layoutOnly)
            {
                string winExpName = Path.GetFileNameWithoutExtension(FullFileName);
                string winExpFullFileName = FullFileName;
                if (winExpName != Name && Name != "Blank Project")
                {
                    FullFileName = Path.Combine(FileDirectory, Name + ".bestfit");
                    File.Move(winExpFullFileName, FullFileName);
                }
            }

            var sqlite = new SQLiteManager(FullFileName);
            sqlite.Open();
            try
            {
                if (layoutOnly)
                {
                    // Layout-only save: only persist the two layout columns.
                    // Do not stamp LastModified, do not iterate element collections.
                    CreateTable(sqlite);
                    var layoutView = sqlite.GetTableManager(_tableName);
                    if (layoutView.NumberOfRows == 0) layoutView.AddRow();
                    WriteLayoutWithBackup(layoutView, nameof(AvalonDockLayout), AvalonDockLayoutPreviousColumn, AvalonDockLayout);
                    WriteLayoutWithBackup(layoutView, nameof(ProjectExplorerLayout), ProjectExplorerLayoutPreviousColumn, ProjectExplorerLayout);
                    layoutView.ApplyEdits();
                    sqlite.Close();
                    LayoutDirty = false;
                    return;
                }

                // Only update LastModified if user data has actually changed at the
                // project level. Saves triggered solely by dirty children end up
                // stamping LastModified via the ElementCollection_Saved handler.
                if (IsDirty)
                    LastModified = DateTime.Now;
                CreateTable(sqlite);

                var dtView = sqlite.GetTableManager(_tableName);
                if (dtView.NumberOfRows == 0) dtView.AddRow();
                dtView.EditCell(0, nameof(Name), Name);
                dtView.EditCell(0, nameof(Description), Description);
                dtView.EditCell(0, nameof(CreationDate), FrameworkInterfaces.Utilities.Tools.DateToUniversalString(CreationDate));
                dtView.EditCell(0, nameof(LastModified), FrameworkInterfaces.Utilities.Tools.DateToUniversalString(LastModified));
                dtView.EditCell(0, nameof(FullFileName), FullFileName);
                dtView.EditCell(0, nameof(SoftwareVersion), SoftwareVersion);
                WriteLayoutWithBackup(dtView, nameof(AvalonDockLayout), AvalonDockLayoutPreviousColumn, AvalonDockLayout);
                WriteLayoutWithBackup(dtView, nameof(ProjectExplorerLayout), ProjectExplorerLayoutPreviousColumn, ProjectExplorerLayout);
                dtView.ApplyEdits();

                // Next, save element collections
                for (int i = 0; i < ElementCollections.Count; i++)
                    ElementCollections[i].Save();

                sqlite.Close();
                SetIsDirty(false);
                LayoutDirty = false;
                MarkUndoSavePoint();
                RaiseObjectSaved(this);
            }
            finally
            {
                // Defensive close — handles the case where any of the dtView edits,
                // child collection saves, or post-success state changes throw.
                if (sqlite.DataBaseOpen) sqlite.Close();
            }
        }

        /// <summary>
        /// Writes a layout XML string to <paramref name="currentColumn"/> while rotating the
        /// previous on-disk value into <paramref name="previousColumn"/>, and skipping the write
        /// entirely if <paramref name="newValue"/> fails XML validation.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Provides crash-recovery for both <see cref="ProjectBase.AvalonDockLayout"/> and
        /// <see cref="ProjectBase.ProjectExplorerLayout"/>. The dual-buffer pattern lets the
        /// load path roll back to a last-known-good layout if the current column is corrupted,
        /// instead of falling back to the element-collection or embedded-resource default.
        /// </para>
        /// <para>
        /// The validation step (<see cref="IsWellFormedLayoutXml"/>) prevents a corrupted XML
        /// string from overwriting a valid on-disk layout. Keeping the existing value is
        /// strictly better than replacing it with garbage.
        /// </para>
        /// <para>
        /// Empty or null <paramref name="newValue"/> is treated as "no layout captured yet" and
        /// is written as an empty string; the previous column is not rotated in that case.
        /// </para>
        /// </remarks>
        /// <param name="dtView">The open project table view.</param>
        /// <param name="currentColumn">Column storing the current layout XML.</param>
        /// <param name="previousColumn">Column storing the previous (backup) layout XML.</param>
        /// <param name="newValue">The new layout XML to persist.</param>
        private static void WriteLayoutWithBackup(
            DatabaseManager.DataTableView dtView,
            string currentColumn,
            string previousColumn,
            string newValue)
        {
            // Empty / null layout means "nothing captured yet" — don't rotate, just clear.
            if (string.IsNullOrEmpty(newValue))
            {
                if (dtView.ColumnNames.Contains(currentColumn))
                    dtView.EditCell(0, currentColumn, newValue ?? string.Empty);
                return;
            }

            // If the new layout is malformed, refuse to overwrite. The old on-disk value
            // is strictly more useful than a corrupt replacement.
            if (!IsWellFormedLayoutXml(newValue))
            {
                System.Diagnostics.Debug.WriteLine(
                    $"BestFitProject: refusing to write malformed layout to '{currentColumn}'; preserving previous on-disk value.");
                return;
            }

            // Rotate current → previous if (a) there IS an existing value, (b) it is valid,
            // and (c) it actually differs from the new value. Writing a malformed existing
            // value into the backup column would poison the recovery buffer.
            if (dtView.ColumnNames.Contains(currentColumn) && dtView.ColumnNames.Contains(previousColumn))
            {
                string existing = dtView.GetCell(currentColumn, 0)?.ToString();
                if (!string.IsNullOrEmpty(existing) && existing != newValue && IsWellFormedLayoutXml(existing))
                {
                    dtView.EditCell(0, previousColumn, existing);
                }
            }

            if (dtView.ColumnNames.Contains(currentColumn))
                dtView.EditCell(0, currentColumn, newValue);
        }

        /// <summary>
        /// Checks whether the given string parses as well-formed XML. Used to validate
        /// layout XML before persisting it to disk.
        /// </summary>
        /// <remarks>
        /// This is a well-formedness check only, not schema validation — the AvalonDock
        /// and Project-Explorer load paths handle schema-level errors by falling back
        /// to the dual-buffer previous column and then to the default layout. Catching
        /// malformed XML here is enough to prevent the most common corruption mode
        /// (partial write, encoding mismatch, injected garbage).
        /// </remarks>
        /// <param name="xml">The candidate XML string.</param>
        /// <returns><c>true</c> if the string is well-formed XML; otherwise <c>false</c>.</returns>
        internal static bool IsWellFormedLayoutXml(string xml)
        {
            if (string.IsNullOrEmpty(xml)) return false;
            try
            {
                System.Xml.Linq.XElement.Parse(xml);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Closes the project and disposes of any virtual memory.
        /// </summary>
        public override void Close()
        {
            // Unsubscribe from collection events before clearing
            UnsubscribeCollectionEvents();

            // Clear all project element collections
            _timeSeriesCollection.Clear();
            _inputDataCollection.Clear();
            _fittingAnalysisCollection.Clear();
            _univariateAnalysisCollection.Clear();
            _bivariateAnalysisCollection.Clear();
            _ratingCurveAnalysisCollection.Clear();
            _timeSeriesAnalysisCollection.Clear();
        }

        /// <summary>
        /// Compacts the project SQLite file.
        /// </summary>
        public override void Compact()
        {
            // A basic connection is set so I can get the size of the "-journal" temp file.
            var sqlite = new SQLiteManager(FullFileName);
            SQLiteConnectionStringBuilder connectionBuilder = new SQLiteConnectionStringBuilder();
            connectionBuilder.Version = 3;
            connectionBuilder.DataSource = FullFileName;
            sqlite.SetDatabaseConnection(connectionBuilder);
            sqlite.Vacuum();
        }

        /// <summary>
        /// Optimizes the project SQLite file.
        /// </summary>
        public override void Optimize()
        {
            var sqlite = new SQLiteManager(FullFileName);
            sqlite.Optimize();
        }

        #endregion
    }
}
