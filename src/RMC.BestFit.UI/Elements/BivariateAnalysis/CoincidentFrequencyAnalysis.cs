using DatabaseManager;
using FrameworkInterfaces;
using FrameworkInterfaces.Messaging;
using FrameworkInterfaces.Undo;
using FrameworkInterfaces.Undo.Actions;
using Numerics;
using Numerics.Distributions;
using Numerics.Sampling.MCMC;
using Numerics.Utilities;
using OxyPlot.Wpf;
using OxyPlot.Wpf.Serialization;
using RMC.BestFit.Estimation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Xml.Linq;
using ModelAnalyses = RMC.BestFit.Analyses;

namespace RMC.BestFit.UI
{
    /// <summary>
    /// UI wrapper for the model-layer <see cref="ModelAnalyses.CoincidentFrequencyAnalysis"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    /// <b>No <see cref="BayesianController"/>:</b> Like <see cref="CompositeAnalysis"/>,
    /// this analysis runs no MCMC chain of its own — it consumes posterior samples from
    /// the upstream <see cref="BivariateAnalysis"/> (and its underlying marginal
    /// <see cref="UnivariateAnalysis"/> chains). Only a single Frequency plot is
    /// exposed. There is no 7-plot Bayesian diagnostic suite.
    /// </para>
    /// <para>
    /// Persistence uses the canonical two-table pattern: the parent collection table
    /// (<c>"Bivariate Distribution Analysis"</c>) holds (Name, Type) discriminator rows;
    /// per-element data lives in the per-subtype <see cref="CollectionName"/> table.
    /// </para>
    /// </remarks>
    [Category("General")]
    [DisplayName("Coincident Frequency Analysis")]
    [Description("Computes a frequency curve from a fitted bivariate distribution and an externally derived response surface.")]
    [Browsable(true)]
    public class CoincidentFrequencyAnalysis : ElementBase, IAnalysisElement
    {

        #region Construction

        /// <summary>
        /// Constructs a new coincident frequency analysis element.
        /// </summary>
        /// <param name="name">The element name.</param>
        /// <param name="parentCollection">The parent <see cref="BivariateAnalysisCollection"/>.</param>
        /// <param name="openFromFile">If true, the constructor calls <see cref="Open()"/> to load persisted state.</param>
        public CoincidentFrequencyAnalysis(string name, IElementCollection parentCollection, bool openFromFile = false) : base(name, parentCollection)
        {
            IsUndoEnabled = false;
            try
            {
                _nameOnDisk = name;
                _creationDate = DateTime.Now;
                _lastModified = DateTime.Now;

                // Plot
                _frequencyPlot = CreateDefaultFrequencyPlot();

                // Inner model analysis
                _innerAnalysis = new ModelAnalyses.CoincidentFrequencyAnalysis();
                SubscribeInnerAnalysis();

                // UI ordinate collections
                _xValues = new ObservableCollection<double>();
                _yValues = new ObservableCollection<double>();
                _xValues.CollectionChanged += XValues_CollectionChanged;
                _yValues.CollectionChanged += YValues_CollectionChanged;

                // Messages
                _descriptionMsg = new BasicMessageItem(MessageType.Message,
                    "The coincident frequency analysis does not have a description.",
                    this, ParentCollection.Name, Name, nameof(Description), "CFA-MSG-001");
                _bivariateNullMsg = new BasicMessageItem(MessageType.Error,
                    "Bivariate analysis is missing. Please select a fitted bivariate analysis.",
                    this, ParentCollection.Name, Name, nameof(BivariateAnalysis), "CFA-ERR-002");
                _bivariateNotEstimatedMsg = new BasicMessageItem(MessageType.Error,
                    "Selected bivariate analysis has not been estimated yet. Run the bivariate analysis (or include it in the batch) before running the coincident frequency analysis.",
                    this, ParentCollection.Name, Name, nameof(BivariateAnalysis), "CFA-ERR-003");
                _bivariateInvalidMsg = new BasicMessageItem(MessageType.Error,
                    "Selected bivariate analysis is invalid.",
                    this, ParentCollection.Name, Name, nameof(BivariateAnalysis), "CFA-ERR-004");
                _inputDataInValidMsg = new BasicMessageItem(MessageType.Error,
                    "The selected input data is invalid.",
                    this, ParentCollection.Name, Name, nameof(InputData), "CFA-ERR-005");
                _ordinatesInvalidMsg = new BasicMessageItem(MessageType.Error,
                    "X / Y ordinates or the bivariate response surface are invalid (need at least 2 strictly-ascending values per axis with matching response dimensions).",
                    this, ParentCollection.Name, Name, nameof(BivariateResponse), "CFA-ERR-006");
                _bayesianOptionsInvalidMsg = new BasicMessageItem(MessageType.Error,
                    "Bayesian analysis output options or number of bins are invalid.",
                    this, ParentCollection.Name, Name, nameof(BayesianAnalysis), "CFA-ERR-007");

                _messages = new List<BasicMessageItem>
                {
                    _descriptionMsg,
                    _bivariateNullMsg,
                    _bivariateNotEstimatedMsg,
                    _bivariateInvalidMsg,
                    _inputDataInValidMsg,
                    _ordinatesInvalidMsg,
                    _bayesianOptionsInvalidMsg,
                };

                _messenger = Messenger.GetInstance();
                _validationAdapter = new ValidationMessageAdapter(_messenger, this, ParentCollection.Name, "CFA");
                // Defer the empty-description message until after Open() so a freshly-opened
                // element doesn't show the message during construction only to have Open()
                // immediately remove it. New elements (openFromFile == false) still need it
                // surfaced — branch below adds it once after Open() decides the description value.
                // _bivariateNullMsg is added by the terminal SetIsValid() call below
                // via ValidateBivariateAnalysis() — no eager add needed here.

                if (openFromFile)
                {
                    Open();
                }
                else
                {
                    // Seed one default ordinate in each axis so a freshly-created CFA
                    // shows a populated row in the X / Y ValidationDataGrids. Without
                    // this the DataGrids start empty and the user must click the
                    // click-to-add row before entering values. CollectionChanged is
                    // already wired but IsUndoEnabled == false here, so no undo entries
                    // are recorded and the terminal SetIsDirty(false) below leaves the
                    // element clean.
                    _xValues.Add(0d);
                    _yValues.Add(0d);
                }

                if (string.IsNullOrEmpty(_description))
                    _messenger.Add(_descriptionMsg);

                _nameValid = ValidateName(BestFitProject.InvalidNameCharacters, 50, "CFA");
                ValidateBivariateAnalysis();
                ValidateOrdinates();
                ValidateBayesianOptions();
                SetIsValid();
                // Not a v1 feature — no legacy migration path, so no openedFromV1 flag.
                SetIsDirty(false);
            }
            finally
            {
                SetupBridges();
                IsUndoEnabled = true;
                ClearUndoHistory();
            }
        }

        #endregion

        #region Members

        #region IMetaData Properties

        /// <summary>
        /// Gets or sets the element name.
        /// </summary>
        [Category("Meta Data")]
        [DisplayName("Name")]
        [Description("Unique label identifying this coincident frequency analysis; max 50 characters.")]
        [Browsable(true)]
        public override string Name
        {
            get { return _name; }
            set
            {
                if (_name != value)
                {
                    var old = _name;
                    foreach (var item in _messages)
                        item.SourceName = value;

                    _name = value;
                    _nameValid = ValidateName(BestFitProject.InvalidNameCharacters, 50, "CFA");
                    SetIsValid();
                    RecordPropertyChange(nameof(Name), old, value);
                }
            }
        }

        /// <summary>
        /// Gets or sets the element description.
        /// </summary>
        [Category("Meta Data")]
        [DisplayName("Description")]
        [Description("Free-text annotation describing this coincident frequency analysis.")]
        [Browsable(true)]
        public override string Description
        {
            get { return _description; }
            set
            {
                if (_description != value)
                {
                    var old = _description;
                    _description = value;
                    if (string.IsNullOrEmpty(_description))
                        _messenger.Add(_descriptionMsg);
                    else
                        _messenger.Remove(_descriptionMsg);
                    RecordPropertyChange(nameof(Description), old, value);
                }
            }
        }

        /// <summary>
        /// Gets the element creation date.
        /// </summary>
        [Category("Meta Data")]
        [DisplayName("Creation Date")]
        [Description("The date and time when the analysis was first created.")]
        [Browsable(true)]
        public override DateTime CreationDate => _creationDate;

        /// <summary>
        /// Gets the date when the element was last modified.
        /// </summary>
        [Category("Meta Data")]
        [DisplayName("Last Edited")]
        [Description("The date and time when the analysis was last modified.")]
        [Browsable(true)]
        public override DateTime LastModified => _lastModified;

        #endregion

        #region IElement Properties

        /// <inheritdoc/>
        public override string NameOnDisk => _nameOnDisk;

        /// <inheritdoc/>
        public override System.Windows.Media.ImageSource ElementImage =>
            System.Windows.Application.Current?.TryFindResource("CoincidentFrequencyAnalysisIcon") as System.Windows.Media.ImageSource;

        /// <summary>
        /// Resource key for the element icon, supporting dynamic theme updates.
        /// </summary>
        public string ElementImageResourceKey => "CoincidentFrequencyAnalysisIcon";

        /// <inheritdoc/>
        public override bool CanCopyFromExternal => false;

        /// <inheritdoc/>
        public override bool IsValid => _isValid;

        /// <summary>
        /// Gets a value indicating whether this analysis can be included in a batch run.
        /// </summary>
        /// <remarks>
        /// True when either <see cref="IsValid"/> is already true, or the analysis is otherwise
        /// configured (Name + BivariateAnalysis selected + model-shape valid) and the only blocking
        /// reason is that the upstream <see cref="BivariateAnalysis"/> has not yet been estimated.
        /// The model-layer <c>BatchAnalysisRunner</c> orders CFAs into Phase 2 so an un-estimated
        /// upstream BA included in the same batch is fitted in Phase 1 first.
        /// </remarks>
        public bool IsBatchEligible => IsValid
            || (_nameValid && _ordinatesValid && _bayesianOptionsValid && _bivariateConfigValid);

        #endregion

        #region Fields

        /// <summary>
        /// Per-subtype SQLite table name. Matches the multi-type collection convention used by
        /// CompositeAnalysis ("&lt;Composite Distribution&gt;") inside UnivariateAnalysisCollection.
        /// </summary>
        public static string CollectionName => "<Coincident Frequency>";

        // Inner model
        /// <summary>The model-layer analysis instance that performs the integration. Nulled in <see cref="Delete"/>; accessors null-guard.</summary>
        private ModelAnalyses.CoincidentFrequencyAnalysis _innerAnalysis;

        /// <inheritdoc/>
        public ModelAnalyses.IAnalysis InnerAnalysis => _innerAnalysis;

        // Validation messages
        /// <summary>The list of message items this element owns; used by the Name setter to bulk-update SourceName.</summary>
        private readonly List<BasicMessageItem> _messages;
        /// <summary>The shared messenger singleton used to publish messages to the project-wide message bar.</summary>
        private readonly Messenger _messenger;
        /// <summary>Bridge that maps model-layer <see cref="ModelAnalyses.IAnalysis.Validate"/> results onto messenger entries.</summary>
        private readonly ValidationMessageAdapter _validationAdapter;
        /// <summary>Informational message shown when no <see cref="Description"/> has been set.</summary>
        private readonly BasicMessageItem _descriptionMsg;
        /// <summary>Error message shown when no upstream <see cref="BivariateAnalysis"/> has been selected.</summary>
        private readonly BasicMessageItem _bivariateNullMsg;
        /// <summary>Error message shown when the upstream <see cref="BivariateAnalysis"/> exists but has not been estimated.</summary>
        private readonly BasicMessageItem _bivariateNotEstimatedMsg;
        /// <summary>Error message shown when the upstream <see cref="BivariateAnalysis"/> is configured but itself invalid.</summary>
        private readonly BasicMessageItem _bivariateInvalidMsg;
        /// <summary>Error message shown when the optional <see cref="InputData"/> overlay is invalid.</summary>
        private readonly BasicMessageItem _inputDataInValidMsg;
        /// <summary>Error message shown when XValues / YValues / BivariateResponse fail structural validation.</summary>
        private readonly BasicMessageItem _ordinatesInvalidMsg;
        /// <summary>Error message shown when NumberOfBins or BayesianAnalysis output options are invalid.</summary>
        private readonly BasicMessageItem _bayesianOptionsInvalidMsg;

        // Validity flags
        /// <summary>True when <see cref="Name"/> passes ValidateName checks.</summary>
        private bool _nameValid = false;
        /// <summary>True when the upstream <see cref="BivariateAnalysis"/> is non-null, valid, and estimated. Drives <see cref="IsValid"/>.</summary>
        private bool _bivariateValid = false;
        /// <summary>True when the upstream <see cref="BivariateAnalysis"/> is non-null and its own configuration is valid (does NOT require it to be estimated). Drives <see cref="IsBatchEligible"/>; mirrors CompositeAnalysis._analysesConfigValid.</summary>
        private bool _bivariateConfigValid = false;
        /// <summary>True when XValues / YValues / BivariateResponse have valid shape (length ≥ 2, strictly ascending, dimensions match).</summary>
        private bool _ordinatesValid = true;
        /// <summary>True when NumberOfBins ≥ 2 and BayesianAnalysis output options (CredibleIntervalWidth, OutputLength, PointEstimator) are valid.</summary>
        private bool _bayesianOptionsValid = true;
        /// <summary>True when <see cref="InputData"/> is unset or itself <see cref="IElement.IsValid"/>; defaults to true (overlay is optional).</summary>
        private bool _inputDataValid = true;

        // Optional plot-overlay input
        /// <summary>Optional InputData rendered as an observed-data overlay on the Frequency Plot. Null indicates no overlay.</summary>
        private InputData _inputData;

        // Upstream link
        /// <summary>The upstream <see cref="BivariateAnalysis"/> whose marginals + copula drive the integration.</summary>
        private BivariateAnalysis _bivariateAnalysis;

        // UI-owned mutable inputs
        /// <summary>Backing collection for <see cref="XValues"/> (response-surface row ordinates).</summary>
        private ObservableCollection<double> _xValues;
        /// <summary>Backing collection for <see cref="YValues"/> (response-surface column ordinates).</summary>
        private ObservableCollection<double> _yValues;

        // Plot
        /// <summary>The Stage-Frequency plot owned by this element. Hosted in App via <see cref="FrequencyPlot"/>.</summary>
        private Plot _frequencyPlot;
        /// <summary>Undo bridge for visual edits to <see cref="_frequencyPlot"/>.</summary>
        private PlotUndoManager _frequencyPlotUndo;

        // Undo bridges for the X / Y ordinate collections. Mirror the canonical pattern from
        // UnivariateAnalysis / CompositeAnalysis where ProbabilityOrdinates is wrapped by a
        // single UndoableCollectionBridge — Add / Remove / Replace / Reset are captured
        // automatically. Created in SetupBridges, disposed in DisposeBridges.
        /// <summary>Undo bridge for <see cref="XValues"/>.</summary>
        private UndoableCollectionBridge<double> _xValuesBridge;
        /// <summary>Undo bridge for <see cref="YValues"/>.</summary>
        private UndoableCollectionBridge<double> _yValuesBridge;
        /// <summary>Rolling baseline for BivariateResponse 2D-array undo recording.</summary>
        private XElement _bivariateResponseSnapshot;

        /// <summary>
        /// Undo bridge for <see cref="BayesianAnalysis"/> result settings the user can edit
        /// from the <c>BayesianOutputControl</c> combos (<c>CredibleIntervalWidth</c>,
        /// <c>OutputLength</c>, <c>PointEstimator</c>, and <c>PRNGSeed</c>). CFA does not run its own MCMC chain,
        /// so the broader simulation/advanced bridges in <see cref="BayesianController"/> do
        /// not apply — only this scoped settings bridge is needed. Mirrors the canonical
        /// pattern used by <see cref="CompositeAnalysis"/>.
        /// </summary>
        private UndoableStateBridge _bayesianSettingsBridge;

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the input data used as an observed-data overlay on the Frequency Plot.
        /// Optional — null indicates no overlay.
        /// </summary>
        [Category("General")]
        [DisplayName("Input Data")]
        [Description("Input data plotted as an observed-data overlay on the Frequency Plot.")]
        [Browsable(true)]
        public InputData InputData
        {
            get { return _inputData; }
            set
            {
                if (_inputData == value) return;
                var old = _inputData;

                if (_inputData != null)
                {
                    _inputData.PropertyChanged -= InputDataChanged;
                    _inputData.Deleted -= OnInputDataDeleted;
                }

                _inputData = value;

                if (_inputData != null)
                {
                    _inputData.PropertyChanged += InputDataChanged;
                    _inputData.Deleted += OnInputDataDeleted;
                }

                // Optional overlay — invalidity surfaces a message but does not block estimation.
                _inputDataValid = true;
                _messenger.Remove(_inputDataInValidMsg);
                if (_inputData != null && _inputData.IsValid == false)
                {
                    _inputDataValid = false;
                    _messenger.Add(_inputDataInValidMsg);
                }

                SetIsValid();
                RecordPropertyChange(nameof(InputData), old, value);
            }
        }

        /// <summary>
        /// Gets or sets the upstream <see cref="BivariateAnalysis"/> that supplies the
        /// fitted marginals and copula. Must be estimated before this analysis can produce
        /// uncertainty bands.
        /// </summary>
        [Category("General")]
        [DisplayName("Bivariate Analysis")]
        [Description("The fitted bivariate distribution analysis whose marginals and copula drive the integration.")]
        [Browsable(true)]
        public BivariateAnalysis BivariateAnalysis
        {
            get { return _bivariateAnalysis; }
            set
            {
                if (_bivariateAnalysis == value) return;
                var old = _bivariateAnalysis;

                if (_bivariateAnalysis != null)
                {
                    _bivariateAnalysis.PropertyChanged -= BivariateAnalysis_PropertyChanged;
                    _bivariateAnalysis.Deleted -= OnBivariateAnalysisDeleted;
                }

                _bivariateAnalysis = value;

                if (_bivariateAnalysis != null)
                {
                    _bivariateAnalysis.PropertyChanged += BivariateAnalysis_PropertyChanged;
                    _bivariateAnalysis.Deleted += OnBivariateAnalysisDeleted;
                }

                if (_innerAnalysis != null)
                {
                    _innerAnalysis.BivariateAnalysis = _bivariateAnalysis?.InnerAnalysis as ModelAnalyses.BivariateAnalysis;
                    SyncMarginalChainsToInnerAnalysis();
                }

                ValidateBivariateAnalysis();
                SetIsValid();
                if (!UndoManager.IsExecutingAction) ClearResults();
                RecordPropertyChange(nameof(BivariateAnalysis), old, value);
            }
        }

        /// <summary>
        /// Gets the X (primary) ordinates for the response-surface rows. Must be strictly ascending.
        /// </summary>
        [Category("Inputs")]
        [DisplayName("X Values")]
        [Description("Primary ordinates (X axis); the response-surface rows. Must be strictly ascending.")]
        [Browsable(true)]
        public ObservableCollection<double> XValues
        {
            get { return _xValues; }
        }

        /// <summary>
        /// Gets the Y (secondary) ordinates for the response-surface columns. Must be strictly ascending.
        /// </summary>
        [Category("Inputs")]
        [DisplayName("Y Values")]
        [Description("Secondary ordinates (Y axis); the response-surface columns. Must be strictly ascending.")]
        [Browsable(true)]
        public ObservableCollection<double> YValues
        {
            get { return _yValues; }
        }

        /// <summary>
        /// Gets or sets the response surface Z[i, j] indexed by (X primary row i, Y secondary
        /// column j). Must be strictly increasing along both axes.
        /// </summary>
        [Category("Inputs")]
        [DisplayName("Bivariate Response")]
        [Description("The user-entered response surface Z = f(X, Y) tabulated on the X Ã— Y grid.")]
        [Browsable(true)]
        public double[,] BivariateResponse
        {
            // Null-safe getter — _innerAnalysis is nulled on Delete(); a stale binding can still
            // hit this accessor before WPF tears down the view. Return an empty surface as a
            // benign placeholder rather than throwing NRE.
            get { return _innerAnalysis?.BivariateResponse ?? new double[0, 0]; }
            set
            {
                if (_innerAnalysis == null) return;
                if (ReferenceEquals(_innerAnalysis.BivariateResponse, value)) return;
                _innerAnalysis.BivariateResponse = value ?? new double[0, 0];
                RecordBivariateResponseUndo();
                if (!UndoManager.IsExecutingAction) ClearResults();
                ValidateOrdinates();
                SetIsValid();
                RaisePropertyChange(nameof(BivariateResponse));
            }
        }

        /// <summary>
        /// Gets or sets the number of evenly-spaced Z output bins. Default is 50.
        /// Range checking (5 â‰¤ N â‰¤ 1000) and the slow-run warning (N &gt; 100) are surfaced
        /// through <see cref="ModelAnalyses.CoincidentFrequencyAnalysis.Validate"/>.
        /// </summary>
        [Category("Settings")]
        [DisplayName("Number Of Bins")]
        [Description("Number of evenly-spaced Z output bins. Default 50.")]
        [Browsable(true)]
        public int NumberOfBins
        {
            // Null-safe getter — _innerAnalysis is nulled on Delete(); 50 is the documented default
            // (see ModelAnalyses.CoincidentFrequencyAnalysis.NumberOfBins).
            get { return _innerAnalysis?.NumberOfBins ?? 50; }
            set
            {
                if (_innerAnalysis == null) return;
                if (_innerAnalysis.NumberOfBins != value)
                {
                    var old = _innerAnalysis.NumberOfBins;
                    _innerAnalysis.NumberOfBins = value;
                    if (!UndoManager.IsExecutingAction) ClearResults();
                    ValidateBayesianOptions();
                    SetIsValid();
                    RecordPropertyChange(nameof(NumberOfBins), old, value);
                }
            }
        }

        /// <summary>
        /// Gets the uncertainty analysis results (delegates to inner analysis).
        /// </summary>
        public UncertaintyAnalysisResults AnalysisResults => _innerAnalysis?.AnalysisResults;

        /// <summary>
        /// Gets the Z output bin values where AEPs are evaluated (delegates to inner analysis).
        /// </summary>
        public double[] ZOutputValues => _innerAnalysis?.ZOutputValues;

        /// <summary>
        /// Gets a value indicating whether the analysis has been estimated.
        /// </summary>
        public bool IsEstimated => _innerAnalysis?.IsEstimated ?? false;

        /// <summary>
        /// Gets the Stage-Frequency plot.
        /// </summary>
        public Plot FrequencyPlot => _frequencyPlot;

        /// <summary>
        /// Gets the Bayesian Analysis settings (CredibleIntervalWidth, OutputLength,
        /// PointEstimator, and PRNGSeed) owned by this CFA element via the inner model analysis.
        /// CFA does not run its own MCMC chain; this object holds result settings that are
        /// independent of the upstream <see cref="BivariateAnalysis"/>'s settings, so CFA
        /// state round-trips cleanly across save/open.
        /// </summary>
        public BayesianAnalysis BayesianAnalysis => _innerAnalysis?.BayesianAnalysis;

        #endregion

        #endregion

        #region IElement Methods

        /// <summary>
        /// The required columns for the per-subtype SQLite table.
        /// </summary>
        private static Dictionary<string, Type> RequiredColumns { get; } = new Dictionary<string, Type>()
        {
            { nameof(Name),                  typeof(string) },
            { nameof(Description),           typeof(string) },
            { nameof(CreationDate),          typeof(string) },
            { nameof(LastModified),          typeof(string) },
            { nameof(BivariateAnalysis),     typeof(string) },
            { nameof(InputData),             typeof(string) },
            { nameof(XValues),               typeof(string) },
            { nameof(YValues),               typeof(string) },
            { nameof(BivariateResponse),     typeof(string) },
            { nameof(NumberOfBins),          typeof(string) },
            { nameof(BayesianAnalysis),      typeof(string) },
            { nameof(AnalysisResults),       typeof(string) },
            { nameof(ZOutputValues),         typeof(string) },
            { "FrequencyPlotSettings",       typeof(string) },
        };

        /// <summary>
        /// Creates the parent collection table (Name + Type discriminator) and the per-subtype
        /// table for this element type. Idempotent — safe to call repeatedly. Mirrors the
        /// CompositeAnalysis pattern.
        /// </summary>
        private void CreateTable(SQLiteManager sqlite)
        {
            // Parent collection table — Name + Type discriminator only.
            if (!sqlite.TableNames.Contains(ParentCollection.Name))
            {
                var dataTable = new DataTable(ParentCollection.Name);
                dataTable.Columns.Add("Name", typeof(string));
                dataTable.Columns.Add("Type", typeof(string));
                sqlite.SaveDataTable(dataTable);
            }
            else
            {
                var dt = sqlite.GetTableManager(ParentCollection.Name);
                if (!dt.ColumnNames.Contains("Name")) dt.AddColumn("Name", typeof(string));
                if (!dt.ColumnNames.Contains("Type")) dt.AddColumn("Type", typeof(string));
                dt.ApplyEdits();
            }

            // Per-subtype table — full element data.
            if (!sqlite.TableNames.Contains(CollectionName))
            {
                var dataTable = new DataTable(CollectionName);
                foreach (KeyValuePair<string, Type> column in RequiredColumns)
                    dataTable.Columns.Add(column.Key, column.Value);
                sqlite.SaveDataTable(dataTable);
            }
            else
            {
                var dt = sqlite.GetTableManager(CollectionName);
                foreach (KeyValuePair<string, Type> column in RequiredColumns)
                {
                    int columnIndex = Array.IndexOf(dt.ColumnNames, column.Key);
                    if (columnIndex < 0)
                    {
                        dt.AddColumn(column.Key, column.Value);
                    }
                    else if (dt.ColumnTypes[columnIndex] != column.Value)
                    {
                        dt.DeleteColumn(columnIndex);
                        dt.AddColumn(column.Key, column.Value);
                    }
                }
                dt.ApplyEdits();
            }
        }

        /// <inheritdoc/>
        public override void Open()
        {
            Open(new SQLiteManager(ParentCollection.ParentProject.FullFileName));
        }

        /// <summary>
        /// Loads persisted state from the per-subtype SQLite table.
        /// </summary>
        public void Open(SQLiteManager sqlite)
        {
            var wasUndoEnabled = IsUndoEnabled;
            IsUndoEnabled = false;
            try
            {
                _messenger.Clear(this);
                _validationAdapter.ClearAll();
                var wasOpen = sqlite.DataBaseOpen;
                if (!wasOpen) sqlite.Open();

                if (!sqlite.TableNames.Contains(CollectionName))
                {
                    if (!wasOpen) sqlite.Close();
                    return;
                }

                var dtView = sqlite.GetTableManager(CollectionName);
                int rowIndex = dtView.SearchColumn(0, dtView.NumberOfRows - 1, "Name", NameOnDisk, true, true);
                if (rowIndex == -1)
                {
                    if (!wasOpen) sqlite.Close();
                    return;
                }

                // Use backing fields to avoid repeated SetIsValid() / ClearResults() cascades
                // during deserialization. A single SetIsValid() runs at the end of Open().
                if (dtView.ColumnNames.Contains(nameof(Name)))
                {
                    _name = dtView.GetCell(nameof(Name), rowIndex).ToString();
                    foreach (var item in _messages) item.SourceName = _name;
                    _nameValid = ValidateName(BestFitProject.InvalidNameCharacters, 50, "CFA");
                }
                if (dtView.ColumnNames.Contains(nameof(Description)))
                {
                    _description = dtView.GetCell(nameof(Description), rowIndex).ToString();
                    if (string.IsNullOrEmpty(_description))
                        _messenger.Add(_descriptionMsg);
                    else
                        _messenger.Remove(_descriptionMsg);
                }
                if (dtView.ColumnNames.Contains(nameof(CreationDate)))
                    _creationDate = FrameworkInterfaces.Utilities.Tools.DateFromString(
                        dtView.GetCell(nameof(CreationDate), rowIndex).ToString()) ?? DateTime.MinValue;
                if (dtView.ColumnNames.Contains(nameof(LastModified)))
                    _lastModified = FrameworkInterfaces.Utilities.Tools.DateFromString(
                        dtView.GetCell(nameof(LastModified), rowIndex).ToString()) ?? DateTime.MinValue;

                // Resolve upstream BivariateAnalysis by name from the parent collection.
                // Defensive: unsubscribe any previously-bound bivariate so a second Open()
                // (CopyFromExternal, undo replay) does not double-subscribe handlers on the
                // old reference. Mirrors the InputData-resolution branch below.
                if (dtView.ColumnNames.Contains(nameof(BivariateAnalysis)))
                {
                    var biName = dtView.GetCell(nameof(BivariateAnalysis), rowIndex).ToString();
                    if (!string.IsNullOrEmpty(biName))
                    {
                        foreach (var sibling in ParentCollection)
                        {
                            if (sibling is BivariateAnalysis bi && bi.Name == biName)
                            {
                                if (_bivariateAnalysis != null)
                                {
                                    _bivariateAnalysis.PropertyChanged -= BivariateAnalysis_PropertyChanged;
                                    _bivariateAnalysis.Deleted -= OnBivariateAnalysisDeleted;
                                }
                                _bivariateAnalysis = bi;
                                _bivariateAnalysis.PropertyChanged += BivariateAnalysis_PropertyChanged;
                                _bivariateAnalysis.Deleted += OnBivariateAnalysisDeleted;
                                if (_innerAnalysis != null)
                                    _innerAnalysis.BivariateAnalysis = bi.InnerAnalysis as ModelAnalyses.BivariateAnalysis;
                                break;
                            }
                        }
                    }
                }

                // Resolve InputData by name from the project's InputDataCollection (optional).
                // Backing-field assignment mirrors CompositeAnalysis.Open and avoids the
                // SetIsValid / message cascade on each setter call during deserialization.
                // Blank, missing, or unresolved names leave the optional overlay unset.
                if (_inputData != null)
                {
                    _inputData.PropertyChanged -= InputDataChanged;
                    _inputData.Deleted -= OnInputDataDeleted;
                    _inputData = null;
                }
                _inputDataValid = true;
                _messenger.Remove(_inputDataInValidMsg);
                if (dtView.ColumnNames.Contains(nameof(InputData)))
                {
                    var inputDataName = dtView.GetCell(nameof(InputData), rowIndex).ToString();
                    if (!string.IsNullOrEmpty(inputDataName))
                    {
                        foreach (IElementCollection collection in ParentCollection.ParentProject.ElementCollections)
                        {
                            if (collection.GetType() == typeof(InputDataCollection))
                            {
                                foreach (IElement element in collection)
                                {
                                    if (element.Name == inputDataName && element.GetType() == typeof(InputData))
                                    {
                                        if (_inputData != null)
                                        {
                                            _inputData.PropertyChanged -= InputDataChanged;
                                            _inputData.Deleted -= OnInputDataDeleted;
                                        }
                                        _inputData = (InputData)element;
                                        _inputData.PropertyChanged += InputDataChanged;
                                        _inputData.Deleted += OnInputDataDeleted;
                                        _inputDataValid = _inputData.IsValid;
                                        if (!_inputDataValid)
                                            _messenger.Add(_inputDataInValidMsg);
                                        else
                                            _messenger.Remove(_inputDataInValidMsg);
                                        break;
                                    }
                                }
                                break;
                            }
                        }
                    }
                }

                // X values
                _xValues.CollectionChanged -= XValues_CollectionChanged;
                _xValues.Clear();
                if (dtView.ColumnNames.Contains(nameof(XValues)))
                {
                    foreach (var v in ParseDoubleArray(dtView.GetCell(nameof(XValues), rowIndex).ToString()))
                        _xValues.Add(v);
                }
                _xValues.CollectionChanged += XValues_CollectionChanged;

                // Y values
                _yValues.CollectionChanged -= YValues_CollectionChanged;
                _yValues.Clear();
                if (dtView.ColumnNames.Contains(nameof(YValues)))
                {
                    foreach (var v in ParseDoubleArray(dtView.GetCell(nameof(YValues), rowIndex).ToString()))
                        _yValues.Add(v);
                }
                _yValues.CollectionChanged += YValues_CollectionChanged;

                // Push the freshly-loaded ordinates to the inner analysis. Inner setters do
                // their own ClearResults; the no-op SetIsValid at the end of Open absorbs any
                // duplicate work.
                _innerAnalysis.XValues = _xValues.ToArray();
                _innerAnalysis.YValues = _yValues.ToArray();

                // BivariateResponse 2D array
                if (dtView.ColumnNames.Contains(nameof(BivariateResponse)) && _xValues.Count > 0 && _yValues.Count > 0)
                {
                    var flat = ParseDoubleArray(dtView.GetCell(nameof(BivariateResponse), rowIndex).ToString());
                    int rows = _xValues.Count;
                    int cols = _yValues.Count;
                    if (flat.Length == rows * cols)
                    {
                        var z = new double[rows, cols];
                        int idx = 0;
                        for (int i = 0; i < rows; i++)
                            for (int j = 0; j < cols; j++)
                                z[i, j] = flat[idx++];
                        _innerAnalysis.BivariateResponse = z;
                    }
                }

                // Settings
                if (dtView.ColumnNames.Contains(nameof(NumberOfBins)) &&
                    int.TryParse(dtView.GetCell(nameof(NumberOfBins), rowIndex).ToString(),
                        NumberStyles.Any, CultureInfo.InvariantCulture, out int bins))
                {
                    _innerAnalysis.NumberOfBins = bins;
                }

                // BayesianAnalysis presentation settings and posterior-resampling seed. Parse the
                // XElement attributes directly rather than rebuilding a
                // throwaway BayesianAnalysis(XElement) instance — the heavy constructor pulls in
                // priors / sampler config / etc. and throws on partial or legacy XML, even though
                // CFA only consumes these result-construction fields.
                if (dtView.ColumnNames.Contains(nameof(BayesianAnalysis)))
                {
                    var bayesXml = dtView.GetCell(nameof(BayesianAnalysis), rowIndex).ToString();
                    if (!string.IsNullOrEmpty(bayesXml))
                    {
                        try
                        {
                            var bayesElement = XElement.Parse(bayesXml);
                            var ciAttr = bayesElement.Attribute(nameof(BayesianAnalysis.CredibleIntervalWidth));
                            var olAttr = bayesElement.Attribute(nameof(BayesianAnalysis.OutputLength));
                            var peAttr = bayesElement.Attribute(nameof(BayesianAnalysis.PointEstimator));
                            var seedAttr = bayesElement.Attribute(nameof(BayesianAnalysis.PRNGSeed));

                            if (ciAttr != null && double.TryParse(ciAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double ci))
                                _innerAnalysis.BayesianAnalysis.CredibleIntervalWidth = ci;
                            if (olAttr != null && int.TryParse(olAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out int ol))
                                _innerAnalysis.BayesianAnalysis.OutputLength = ol;
                            if (peAttr != null && Enum.TryParse(peAttr.Value, out BayesianAnalysis.PointEstimateType pe))
                                _innerAnalysis.BayesianAnalysis.PointEstimator = pe;
                            if (seedAttr != null && int.TryParse(seedAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out int seed))
                                _innerAnalysis.BayesianAnalysis.PRNGSeed = seed;
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine(
                                $"CoincidentFrequencyAnalysis.Open: could not deserialize BayesianAnalysis for '{Name}': {ex.Message}");
                        }
                    }
                }

                // Plot settings
                DeserializePlotSettings(dtView, rowIndex, "FrequencyPlotSettings", _frequencyPlot);

                // ZOutputValues — restore the Z bin grid alongside AnalysisResults so consumers
                // can render the saved frequency curve without re-running.
                if (dtView.ColumnNames.Contains(nameof(ZOutputValues)))
                {
                    var zCsv = dtView.GetCell(nameof(ZOutputValues), rowIndex).ToString();
                    if (!string.IsNullOrEmpty(zCsv))
                    {
                        try { _innerAnalysis.SetZOutputValues(ParseDoubleArray(zCsv)); }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine(
                                $"CoincidentFrequencyAnalysis.Open: could not deserialize ZOutputValues for '{Name}': {ex.Message}");
                        }
                    }
                }

                // AnalysisResults — XElement round-trip via UncertaintyAnalysisResults.FromXElement.
                // Only the summary curves (ModeCurve, MeanCurve, ConfidenceIntervals) + scalar fit
                // metrics are persisted; per-realisation matrices are never stored.
                // Use RestoreAnalysisResults (not SetAnalysisResults) so IsEstimated also flips back
                // to true alongside the curves. The earlier BA-settings copy may have triggered
                // BayesianAnalysis_PropertyChanged â†’ ClearResults() â†’ IsEstimated = false; this restores it.
                if (dtView.ColumnNames.Contains(nameof(AnalysisResults)))
                {
                    var arXml = dtView.GetCell(nameof(AnalysisResults), rowIndex).ToString();
                    if (!string.IsNullOrEmpty(arXml))
                    {
                        try
                        {
                            var results = UncertaintyAnalysisResults.FromXElement(XElement.Parse(arXml));
                            _innerAnalysis.RestoreAnalysisResults(results);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine(
                                $"CoincidentFrequencyAnalysis.Open: could not deserialize AnalysisResults for '{Name}': {ex.Message}");
                        }
                    }
                }

                if (!wasOpen) sqlite.Close();

                // Rebuild plot/collection undo bridges against the freshly deserialized
                // inner analysis and plot instances. Matches the pattern used by every
                // other analysis with an inner-analysis swap (FittingAnalysis, UnivariateAnalysis,
                // MixtureAnalysis, BivariateAnalysis, TimeSeriesAnalysis, RatingCurveAnalysis).
                SetupBridges();

                // Re-validate after deserialization.
                ValidateBivariateAnalysis();
                ValidateOrdinates();
                ValidateBayesianOptions();
                SetIsValid();
                SetIsDirty(false);

                // Notify WPF bindings.
                RaisePropertyChange(nameof(Name));
                RaisePropertyChange(nameof(Description));
                RaisePropertyChange(nameof(BivariateAnalysis));
                RaisePropertyChange(nameof(BayesianAnalysis));
                RaisePropertyChange(nameof(InputData));
                RaisePropertyChange(nameof(XValues));
                RaisePropertyChange(nameof(YValues));
                RaisePropertyChange(nameof(BivariateResponse));
                RaisePropertyChange(nameof(NumberOfBins));
                RaisePropertyChange(nameof(AnalysisResults));
                RaisePropertyChange(nameof(ZOutputValues));
                RaisePropertyChange(nameof(IsEstimated));
            }
            finally
            {
                IsUndoEnabled = wasUndoEnabled;
                if (wasUndoEnabled) ClearUndoHistory();
            }
        }

        /// <summary>
        /// Raises the preview-saved event; called by <see cref="BivariateAnalysisCollection.Save"/>
        /// before this element is saved so plot settings can be captured.
        /// </summary>
        public void RaisePreviewSaved(ref bool cancel)
        {
            RaisePreviewObjectSaved(this, ref cancel);
        }

        /// <inheritdoc/>
        public override void Save()
        {
            if (Name == null) return;

            var sqlite = new SQLiteManager(ParentCollection.ParentProject.FullFileName);
            sqlite.Open();
            DateTime previousLastModified = _lastModified;
            bool committed = false;
            try
            {
                if (IsDirty)
                {
                    _lastModified = DateTime.Now;
                    RaisePropertyChange(nameof(LastModified));
                }

                CreateTable(sqlite);

                // Parent collection row (Name + Type discriminator).
                var parentDt = sqlite.GetTableManager(ParentCollection.Name);
                int parentRowIndex = ParentCollection.IndexOf(this);
                if (parentRowIndex < 0 || parentRowIndex >= parentDt.NumberOfRows)
                {
                    parentDt.AddRow();
                    parentRowIndex = parentDt.NumberOfRows - 1;
                }
                parentDt.EditCell(parentRowIndex, "Name", Name);
                parentDt.EditCell(parentRowIndex, "Type", GetType().ToString());
                parentDt.ApplyEdits();

                // Per-subtype data row.
                var dt = sqlite.GetTableManager(CollectionName);
                int rowIndex = dt.SearchColumn(0, dt.NumberOfRows - 1, "Name", NameOnDisk, true, true);
                if (rowIndex < 0 || rowIndex >= dt.NumberOfRows)
                {
                    dt.AddRow();
                    rowIndex = dt.NumberOfRows - 1;
                }

                dt.EditCell(rowIndex, nameof(Name), Name);
                dt.EditCell(rowIndex, nameof(Description), Description ?? string.Empty);
                dt.EditCell(rowIndex, nameof(CreationDate), FrameworkInterfaces.Utilities.Tools.DateToUniversalString(CreationDate));
                dt.EditCell(rowIndex, nameof(LastModified), FrameworkInterfaces.Utilities.Tools.DateToUniversalString(LastModified));
                dt.EditCell(rowIndex, nameof(BivariateAnalysis), BivariateAnalysis?.Name ?? string.Empty);
                dt.EditCell(rowIndex, nameof(InputData), InputData?.Name ?? string.Empty);
                dt.EditCell(rowIndex, nameof(XValues), JoinDoubles(_xValues));
                dt.EditCell(rowIndex, nameof(YValues), JoinDoubles(_yValues));
                dt.EditCell(rowIndex, nameof(BivariateResponse), FlattenAndJoin(_innerAnalysis.BivariateResponse));
                dt.EditCell(rowIndex, nameof(NumberOfBins), _innerAnalysis.NumberOfBins.ToString(CultureInfo.InvariantCulture));
                dt.EditCell(rowIndex, nameof(BayesianAnalysis),
                    _innerAnalysis.BayesianAnalysis.ToXElement().ToString());
                dt.EditCell(rowIndex, nameof(AnalysisResults),
                    _innerAnalysis.AnalysisResults != null ? _innerAnalysis.AnalysisResults.ToXElement().ToString() : string.Empty);
                dt.EditCell(rowIndex, nameof(ZOutputValues),
                    _innerAnalysis.ZOutputValues != null ? JoinDoubles(_innerAnalysis.ZOutputValues) : string.Empty);
                dt.EditCell(rowIndex, "FrequencyPlotSettings",
                    _frequencyPlot != null ? PlotSerializer.ToXElement(_frequencyPlot).ToString() : string.Empty);

                dt.ApplyEdits();
                committed = true;
            }
            finally
            {
                if (sqlite.DataBaseOpen) sqlite.Close();
                if (!committed && _lastModified != previousLastModified)
                {
                    _lastModified = previousLastModified;
                    RaisePropertyChange(nameof(LastModified));
                }
            }

            // Only mark clean / fire ObjectSaved when the commit actually succeeded.
            if (committed)
            {
                SetIsDirty(false);
                MarkUndoSavePoint();
                _nameOnDisk = Name;
                RaiseObjectSaved(this);
            }
        }

        /// <inheritdoc/>
        public override IElement Copy(string newName = "")
        {
            var element = new CoincidentFrequencyAnalysis(
                string.IsNullOrEmpty(newName) ? Name : newName, ParentCollection);

            element.IsUndoEnabled = false;
            try
            {
                // Scalars
                element.Description = Description;
                element._innerAnalysis.NumberOfBins = _innerAnalysis.NumberOfBins;

                // BayesianAnalysis result-construction settings.
                element._innerAnalysis.BayesianAnalysis.CredibleIntervalWidth = _innerAnalysis.BayesianAnalysis.CredibleIntervalWidth;
                element._innerAnalysis.BayesianAnalysis.OutputLength = _innerAnalysis.BayesianAnalysis.OutputLength;
                element._innerAnalysis.BayesianAnalysis.PointEstimator = _innerAnalysis.BayesianAnalysis.PointEstimator;
                element._innerAnalysis.BayesianAnalysis.PRNGSeed = _innerAnalysis.BayesianAnalysis.PRNGSeed;

                // Upstream link — copy the reference; both elements share the same upstream
                // BivariateAnalysis, matching how Composite copies InputData.
                element.BivariateAnalysis = BivariateAnalysis;
                element.InputData = InputData;

                // Ordinates — replace the contents of the destination's collections.
                element._xValues.CollectionChanged -= element.XValues_CollectionChanged;
                element._yValues.CollectionChanged -= element.YValues_CollectionChanged;
                element._xValues.Clear();
                element._yValues.Clear();
                foreach (var v in _xValues) element._xValues.Add(v);
                foreach (var v in _yValues) element._yValues.Add(v);
                element._xValues.CollectionChanged += element.XValues_CollectionChanged;
                element._yValues.CollectionChanged += element.YValues_CollectionChanged;
                element._innerAnalysis.XValues = element._xValues.ToArray();
                element._innerAnalysis.YValues = element._yValues.ToArray();

                // Response surface (deep copy)
                if (_innerAnalysis.BivariateResponse != null)
                {
                    int r = _innerAnalysis.BivariateResponse.GetLength(0);
                    int c = _innerAnalysis.BivariateResponse.GetLength(1);
                    var z = new double[r, c];
                    Array.Copy(_innerAnalysis.BivariateResponse, z, r * c);
                    element._innerAnalysis.BivariateResponse = z;
                }

                // Plot settings (inside undo suppression — matches FittingAnalysis.Copy template)
                if (_frequencyPlot != null)
                    PlotSerializer.FromXElement(element._frequencyPlot, PlotSerializer.ToXElement(_frequencyPlot));

                // Re-validate after the bypass-property assignments above so the copy's
                // batch-eligibility flags reflect the copied ordinates / bins / Bayesian options.
                element.ValidateBivariateAnalysis();
                element.ValidateOrdinates();
                element.ValidateBayesianOptions();
                element.SetIsValid();

                element.ClearResults();
            }
            finally
            {
                element.SetupBridges();
                element.IsUndoEnabled = true;
                element.ClearUndoHistory();
            }

            return element;
        }

        /// <inheritdoc/>
        public override IElement CopyFromExternal(string itemName, string fullFileName)
        {
            var sqlite = new SQLiteManager(fullFileName);
            var element = new CoincidentFrequencyAnalysis(itemName, ParentCollection);
            element.Open(sqlite);
            return element;
        }

        /// <inheritdoc/>
        public override void Delete()
        {
            if (Name == null) return;

            // Direct unhook from upstream so undo cannot resurrect a deleted reference.
            if (_bivariateAnalysis != null)
            {
                _bivariateAnalysis.Deleted -= OnBivariateAnalysisDeleted;
                _bivariateAnalysis.PropertyChanged -= BivariateAnalysis_PropertyChanged;
            }
            if (_inputData != null)
            {
                _inputData.Deleted -= OnInputDataDeleted;
                _inputData.PropertyChanged -= InputDataChanged;
            }
            DisposeBridges();
            UnsubscribeInnerAnalysis();

            // Null _innerAnalysis so the model's internal BayesianAnalysis.PropertyChanged
            // subscription can be GC'd along with the inner analysis instance. Property accessors
            // (BivariateResponse, NumberOfBins, AnalysisResults, IsEstimated, ZOutputValues,
            // BayesianAnalysis) all use null-conditional access and tolerate the post-Delete state.
            _innerAnalysis = null;

            SetIsDirty(false);

            var sqlite = new SQLiteManager(ParentCollection.ParentProject.FullFileName);
            sqlite.Open();
            try
            {
                // Parent collection row.
                if (sqlite.TableNames.Contains(ParentCollection.Name))
                {
                    var parentDt = sqlite.GetTableManager(ParentCollection.Name);
                    int parentRow = parentDt.SearchColumn(0, parentDt.NumberOfRows - 1, "Name", NameOnDisk, true, true);
                    if (parentRow >= 0 && parentRow < parentDt.NumberOfRows)
                        parentDt.DeleteRow(parentRow);
                    parentDt.ApplyEdits();
                }

                // Per-subtype row.
                if (sqlite.TableNames.Contains(CollectionName))
                {
                    var dt = sqlite.GetTableManager(CollectionName);
                    int rowIndex = dt.SearchColumn(0, dt.NumberOfRows - 1, "Name", NameOnDisk, true, true);
                    if (rowIndex >= 0 && rowIndex < dt.NumberOfRows)
                        dt.DeleteRow(rowIndex);
                    dt.ApplyEdits();
                }
            }
            finally
            {
                sqlite.Close();
            }

            _messenger.Clear(this);
            _validationAdapter.ClearAll();
            RaiseDeleted(this);
        }

        /// <summary>
        /// Splits a comma-separated string into a double array. Returns an empty array if the
        /// input is null or whitespace.
        /// </summary>
        /// <param name="csv">The comma-separated string to parse.</param>
        /// <returns>
        /// An array of doubles parsed from <paramref name="csv"/>, or an empty array when
        /// the input is null or whitespace.
        /// </returns>
        private static double[] ParseDoubleArray(string csv)
        {
            if (string.IsNullOrWhiteSpace(csv)) return Array.Empty<double>();
            return csv.Split(',')
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => double.Parse(s.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture))
                .ToArray();
        }

        /// <summary>
        /// Joins a sequence of doubles into a comma-separated round-trip-safe string using
        /// the G17 format, which guarantees exact IEEE-754 round-trip fidelity.
        /// </summary>
        /// <param name="values">The sequence of doubles to join.</param>
        /// <returns>A comma-separated string of G17-formatted values.</returns>
        private static string JoinDoubles(IEnumerable<double> values)
        {
            return string.Join(",", values.Select(d => d.ToString("G17", CultureInfo.InvariantCulture)));
        }

        /// <summary>
        /// Flattens a 2D response surface into a comma-separated string in row-major order.
        /// Returns an empty string for a null or empty array.
        /// </summary>
        /// <param name="response">The 2D array to flatten. May be null or have zero length.</param>
        /// <returns>A comma-separated row-major string, or <see cref="string.Empty"/> when
        /// <paramref name="response"/> is null or empty.</returns>
        private static string FlattenAndJoin(double[,] response)
        {
            if (response == null || response.Length == 0) return string.Empty;
            int r = response.GetLength(0);
            int c = response.GetLength(1);
            var values = new List<double>(r * c);
            for (int i = 0; i < r; i++)
                for (int j = 0; j < c; j++)
                    values.Add(response[i, j]);
            return JoinDoubles(values);
        }

        /// <summary>
        /// Serializes a 2D response surface into a <c>&lt;Response Rows="R" Cols="C"&gt;</c> element
        /// with a row-major flattened space-delimited string payload. Used for BivariateResponse
        /// undo snapshots.
        /// </summary>
        /// <param name="response">The 2D array to serialize. May be null or empty.</param>
        /// <returns>An XElement carrying the row/col dimensions and the flattened values.</returns>
        private static XElement SerializeBivariateResponse(double[,] response)
        {
            int r = response?.GetLength(0) ?? 0;
            int c = response?.GetLength(1) ?? 0;
            var element = new XElement("Response",
                new XAttribute("Rows", r),
                new XAttribute("Cols", c));
            if (r > 0 && c > 0)
                element.Value = FlattenAndJoin(response);
            return element;
        }

        /// <summary>
        /// Inverse of <see cref="SerializeBivariateResponse"/>. Reconstructs the 2D response
        /// surface from the row-major flattened payload using the embedded Rows/Cols attributes.
        /// Returns an empty 2D array when the snapshot is missing or has zero dimensions.
        /// </summary>
        /// <param name="snapshot">The XElement returned by <see cref="SerializeBivariateResponse"/>.</param>
        /// <returns>The reconstructed 2D array.</returns>
        private static double[,] DeserializeBivariateResponse(XElement snapshot)
        {
            int r = int.TryParse(snapshot?.Attribute("Rows")?.Value, out var rr) ? rr : 0;
            int c = int.TryParse(snapshot?.Attribute("Cols")?.Value, out var cc) ? cc : 0;
            if (r <= 0 || c <= 0) return new double[0, 0];
            var flat = ParseDoubleArray(snapshot?.Value);
            var z = new double[r, c];
            int idx = 0;
            for (int i = 0; i < r && idx < flat.Length; i++)
                for (int j = 0; j < c && idx < flat.Length; j++)
                    z[i, j] = flat[idx++];
            return z;
        }

        #endregion

        #region Analysis Methods

        /// <summary>
        /// Runs the coincident frequency analysis asynchronously.
        /// </summary>
        public async Task RunAsync(SafeProgressReporter progressReporter)
        {
            ValidateBivariateAnalysis();
            SetIsValid();

            // Per project policy, partial / point-estimate-only output must not be silently
            // produced. _bivariateNotEstimatedMsg (added by ValidateBivariateAnalysis when the
            // upstream BA is null / unestimated / invalid) and the model-layer Validate() messages
            // are surfaced to the user; IsValid going false short-circuits here. No duplicate
            // 'CFA-ERR-010' message — _bivariateNotEstimatedMsg already explains the cause.
            if (!IsValid) return;

            _messenger.Add(new BasicMessageItem(MessageType.Event,
                $"The coincident frequency analysis for '{Name}' has started.",
                this, ParentCollection.Name, Name, nameof(CoincidentFrequencyAnalysis)));

            bool succeeded = false;
            try
            {
                // Sync UI inputs into the inner analysis before running.
                _innerAnalysis.XValues = _xValues.ToArray();
                _innerAnalysis.YValues = _yValues.ToArray();

                // Pull the marginal MCMC chains from the upstream univariate analyses so the
                // inner algorithm can vary all three chains across realisations. If a marginal
                // is a CompositeAnalysis (or any non-UnivariateAnalysis IUnivariate), the chain
                // will be null — emit a warning so the user understands uncertainty bands then
                // come from the copula chain only, not the marginal posterior.
                SyncMarginalChainsToInnerAnalysis();
                WarnIfMarginalChainMissing(_bivariateAnalysis?.MarginalX, "X", _innerAnalysis.MarginalXChain);
                WarnIfMarginalChainMissing(_bivariateAnalysis?.MarginalY, "Y", _innerAnalysis.MarginalYChain);

                progressReporter?.IndicateTaskStart();
                await _innerAnalysis.RunAsync(progressReporter);
                succeeded = true;

                RaisePropertyChange(nameof(AnalysisResults));
                RaisePropertyChange(nameof(ZOutputValues));
                RaisePropertyChange(nameof(IsEstimated));

                _messenger.Add(new BasicMessageItem(MessageType.Event,
                    $"The coincident frequency analysis for '{Name}' is complete.",
                    this, ParentCollection.Name, Name, nameof(CoincidentFrequencyAnalysis)));
            }
            catch (OperationCanceledException)
            {
                ClearResults();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CoincidentFrequencyAnalysis.RunAsync failed for '{Name}': {ex}");
                ClearResults();
                _messenger.Add(new BasicMessageItem(MessageType.Error,
                    $"The coincident frequency analysis for '{Name}' failed: {ex.Message}",
                    this, ParentCollection.Name, Name, nameof(CoincidentFrequencyAnalysis)));
            }
            finally
            {
                progressReporter?.IndicateTaskEnded();
                SetIsValid();
                _ = succeeded; // suppress unused-variable warning when no other consumer
            }
        }

        /// <summary>
        /// Emits a warning message when the upstream marginal is non-null but does not expose
        /// posterior MCMC samples (e.g., a <c>CompositeAnalysis</c>, which weights pre-fitted
        /// univariates and runs no chain of its own). Without the marginal chain, CFA's
        /// uncertainty bands are driven only by the copula chain — partial uncertainty
        /// propagation that the user must understand explicitly.
        /// </summary>
        /// <param name="marginal">The marginal exposed by the upstream <see cref="BivariateAnalysis"/>.</param>
        /// <param name="axisLabel">'X' or 'Y' for message text.</param>
        /// <param name="chain">The chain returned by <see cref="TryGetMarginalChain"/>.</param>
        private void WarnIfMarginalChainMissing(IUnivariate marginal, string axisLabel, MCMCResults chain)
        {
            if (marginal != null && chain == null)
            {
                _messenger.Add(new BasicMessageItem(MessageType.Warning,
                    $"Upstream marginal {axisLabel} ('{(marginal as IElement)?.Name ?? marginal.GetType().Name}') does not provide posterior MCMC samples. " +
                    "CFA uncertainty bands will reflect only the copula chain — partial uncertainty propagation.",
                    this, ParentCollection.Name, Name, nameof(BivariateAnalysis), $"CFA-WRN-MARGINAL-{axisLabel}"));
            }
        }

        /// <inheritdoc/>
        public void CancelAnalysis()
        {
            _innerAnalysis?.CancelAnalysis();
            SetIsValid();
        }

        /// <summary>
        /// Returns the empirical (Z, AEP) distribution corresponding to the upstream
        /// <see cref="BayesianAnalysis.PointEstimator"/>. Delegates to
        /// <see cref="ModelAnalyses.CoincidentFrequencyAnalysis.GetPointEstimateDistribution"/>.
        /// </summary>
        public EmpiricalDistribution GetPointEstimateDistribution() =>
            _innerAnalysis?.GetPointEstimateDistribution();

        /// <summary>
        /// Returns the empirical (Z, AEP) distribution for a single posterior draw.
        /// Delegates to <see cref="ModelAnalyses.CoincidentFrequencyAnalysis.GetEmpiricalDistribution(int)"/>.
        /// </summary>
        /// <param name="index">Posterior realisation index.</param>
        public EmpiricalDistribution GetEmpiricalDistribution(int index) =>
            _innerAnalysis?.GetEmpiricalDistribution(index);

        /// <summary>
        /// Clears analysis results.
        /// </summary>
        public void ClearResults()
        {
            _innerAnalysis?.ClearResults();
            RaisePropertyChange(nameof(AnalysisResults));
            RaisePropertyChange(nameof(ZOutputValues));
            RaisePropertyChange(nameof(IsEstimated));
        }

        /// <summary>
        /// Returns the marginal univariate analysis's posterior MCMC results, or null if the
        /// marginal isn't a UnivariateAnalysis or hasn't been estimated.
        /// </summary>
        private static MCMCResults TryGetMarginalChain(IUnivariate marginal)
        {
            if (marginal is UnivariateAnalysis ua && ua.BayesianAnalysis?.IsEstimated == true)
                return ua.BayesianAnalysis.Results;
            return null;
        }

        /// <summary>
        /// Synchronizes marginal posterior chains from the selected upstream bivariate
        /// wrapper into the model-layer CFA analysis.
        /// </summary>
        /// <remarks>
        /// The batch runner executes <see cref="InnerAnalysis"/> directly, bypassing
        /// this wrapper's <see cref="RunAsync"/> method. Keeping these references synced
        /// as upstream state changes preserves the same marginal uncertainty propagation
        /// in batch mode that standalone execution gets immediately before running.
        /// </remarks>
        private void SyncMarginalChainsToInnerAnalysis()
        {
            if (_innerAnalysis == null) return;

            _innerAnalysis.MarginalXChain = TryGetMarginalChain(_bivariateAnalysis?.MarginalX);
            _innerAnalysis.MarginalYChain = TryGetMarginalChain(_bivariateAnalysis?.MarginalY);
        }

        /// <summary>
        /// Subscribes to inner analysis events.
        /// </summary>
        private void SubscribeInnerAnalysis()
        {
            if (_innerAnalysis == null) return;
            _innerAnalysis.PropertyChanged += InnerAnalysis_PropertyChanged;
        }

        /// <summary>
        /// Unsubscribes from inner analysis events.
        /// </summary>
        private void UnsubscribeInnerAnalysis()
        {
            if (_innerAnalysis == null) return;
            _innerAnalysis.PropertyChanged -= InnerAnalysis_PropertyChanged;
        }

        /// <summary>
        /// Forwards property changes from the inner analysis to WPF bindings. Specific properties
        /// trigger validation refresh; all others (including BayesianAnalysis sub-properties such
        /// as <c>CredibleIntervalWidth</c>, <c>OutputLength</c>, and <c>PointEstimator</c>) are
        /// propagated by name so consumers can react to granular changes — e.g., the App control's
        /// <c>Element_PropertyChanged</c> updates frequency-curve column headers when the credible
        /// interval changes. Mirrors the canonical UnivariateAnalysis pattern.
        /// </summary>
        private void InnerAnalysis_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ModelAnalyses.CoincidentFrequencyAnalysis.IsEstimated) ||
                e.PropertyName == nameof(ModelAnalyses.CoincidentFrequencyAnalysis.AnalysisResults) ||
                e.PropertyName == nameof(ModelAnalyses.CoincidentFrequencyAnalysis.ZOutputValues))
            {
                SetIsValid();
            }

            // BayesianAnalysis sub-property changes bubble up through here as raw property
            // names. Re-validate the output options when any of the three feed IsBatchEligible
            // / IsValid via _bayesianOptionsValid.
            if (e.PropertyName == nameof(BayesianAnalysis.CredibleIntervalWidth) ||
                e.PropertyName == nameof(BayesianAnalysis.OutputLength) ||
                e.PropertyName == nameof(BayesianAnalysis.PointEstimator) ||
                e.PropertyName == nameof(BayesianAnalysis.PRNGSeed))
            {
                ValidateBayesianOptions();
                SetIsValid();
            }

            // Forward all property names — including BayesianAnalysis sub-property changes —
            // so granular bindings refresh and the App's Element_PropertyChanged sees the
            // original property name (not just "BayesianAnalysis").
            RaisePropertyChange(e.PropertyName);
        }

        /// <summary>
        /// Handles property changes from the upstream <see cref="BivariateAnalysis"/>.
        /// </summary>
        /// <remarks>
        /// Structural changes to the upstream input set invalidate CFA results. Estimated-state
        /// notifications are split: becoming unavailable clears dependent CFA curves, while a
        /// completed upstream batch run only refreshes validation and marginal-chain references.
        /// This preserves the existing wrapper notification path used by the App controls without
        /// erasing CFA results after the dependent batch phase completes.
        /// </remarks>
        private void BivariateAnalysis_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(BivariateAnalysis.IsEstimated) ||
                e.PropertyName == nameof(BivariateAnalysis.AnalysisResults) ||
                e.PropertyName == nameof(BivariateAnalysis.IsValid))
            {
                SyncMarginalChainsToInnerAnalysis();
                ValidateBivariateAnalysis();
                SetIsValid();
            }

            bool dependencyBecameUnavailable =
                e.PropertyName == nameof(BivariateAnalysis.IsEstimated) && _bivariateAnalysis?.IsEstimated == false;
            bool dependencyResultsWereCleared =
                e.PropertyName == nameof(BivariateAnalysis.AnalysisResults) && _bivariateAnalysis?.AnalysisResults == null;
            bool upstreamStructureChanged =
                e.PropertyName == nameof(BivariateAnalysis.BivariateDistribution) ||
                e.PropertyName == nameof(BivariateAnalysis.MarginalX) ||
                e.PropertyName == nameof(BivariateAnalysis.MarginalY) ||
                e.PropertyName == nameof(BivariateAnalysis.XYOrdinates);

            if (dependencyBecameUnavailable || dependencyResultsWereCleared || upstreamStructureChanged)
            {
                SyncMarginalChainsToInnerAnalysis();
                if (!UndoManager.IsExecutingAction) ClearResults();
            }

            RaisePropertyChange(nameof(BivariateAnalysis));
        }

        /// <summary>
        /// Handles deletion of the upstream BivariateAnalysis. Clears the reference with undo
        /// recording disabled so pressing Undo cannot resurrect a deleted reference.
        /// </summary>
        private void OnBivariateAnalysisDeleted(IElement element)
        {
            var wasUndoEnabled = IsUndoEnabled;
            IsUndoEnabled = false;
            try { BivariateAnalysis = null; }
            finally { IsUndoEnabled = wasUndoEnabled; }
        }

        /// <summary>
        /// Handles property changes on the <see cref="InputData"/>, refreshing the validity flag
        /// and re-firing PropertyChanged on InputData so plot overlays can rebind.
        /// </summary>
        private void InputDataChanged(object sender, PropertyChangedEventArgs e)
        {
            _inputDataValid = true;
            _messenger.Remove(_inputDataInValidMsg);
            if (_inputData != null && _inputData.IsValid == false)
            {
                _inputDataValid = false;
                _messenger.Add(_inputDataInValidMsg);
            }

            SetIsValid();
            RaisePropertyChange(nameof(InputData));
        }

        /// <summary>
        /// Handles deletion of the <see cref="InputData"/> element. Clears the reference with
        /// undo disabled so pressing Undo cannot resurrect a deleted upstream element.
        /// </summary>
        private void OnInputDataDeleted(IElement element)
        {
            var wasUndoEnabled = IsUndoEnabled;
            IsUndoEnabled = false;
            try { InputData = null; }
            finally { IsUndoEnabled = wasUndoEnabled; }
        }

        /// <summary>
        /// Handles changes to the X values collection: auto-resizes the response surface (preserving
        /// overlap, zero-filling new cells) on Add/Remove, then pushes the updated arrays to the
        /// inner analysis and records a combined ordinate+response undo entry.
        /// </summary>
        private void XValues_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // Ordinate add/remove/reset â†’ resize the response surface. The resize runs on both
            // forward edits AND undo replay so the response always matches the current X/Y count.
            // The auto-resize itself is NOT undoable (see ResizeBivariateResponseToMatchOrdinates);
            // _xValuesBridge records the ordinate change separately via the canonical pattern
            // from UnivariateAnalysis / CompositeAnalysis.
            if (e.Action == NotifyCollectionChangedAction.Add ||
                e.Action == NotifyCollectionChangedAction.Remove ||
                e.Action == NotifyCollectionChangedAction.Reset)
            {
                ResizeBivariateResponseToMatchOrdinates();
            }

            _innerAnalysis.XValues = _xValues.ToArray();
            if (!UndoManager.IsExecutingAction) ClearResults();
            ValidateOrdinates();
            SetIsValid();
            RaisePropertyChange(nameof(XValues));
            RaisePropertyChange(nameof(BivariateResponse));
        }

        /// <summary>
        /// Handles changes to the Y values collection. Mirrors <see cref="XValues_CollectionChanged"/>.
        /// </summary>
        private void YValues_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // See XValues_CollectionChanged for design notes — auto-resize runs on both
            // forward edits and undo replay; not undoable.
            if (e.Action == NotifyCollectionChangedAction.Add ||
                e.Action == NotifyCollectionChangedAction.Remove ||
                e.Action == NotifyCollectionChangedAction.Reset)
            {
                ResizeBivariateResponseToMatchOrdinates();
            }

            _innerAnalysis.YValues = _yValues.ToArray();
            if (!UndoManager.IsExecutingAction) ClearResults();
            ValidateOrdinates();
            SetIsValid();
            RaisePropertyChange(nameof(YValues));
            RaisePropertyChange(nameof(BivariateResponse));
        }

        /// <summary>
        /// Resizes <see cref="ModelAnalyses.CoincidentFrequencyAnalysis.BivariateResponse"/> to match
        /// the current X / Y ordinate counts. Overlap is preserved; new cells default to 0. Mirrors
        /// TotalRisk's <c>UpdateProbabilityMatrix()</c>. The auto-resize is intentionally NOT
        /// recorded as an undoable action — undoing the X/Y ordinate add/remove (via the bridges)
        /// triggers this method again on the replay path and re-derives the response shape from
        /// the restored ordinate counts. Only direct cell edits to <see cref="BivariateResponse"/>
        /// (via the public setter, which calls <see cref="RecordBivariateResponseUndo"/>) are
        /// recorded as undo entries. The rolling baseline snapshot is updated in-place so the
        /// next user cell edit is diffed against the freshly-resized array.
        /// </summary>
        private void ResizeBivariateResponseToMatchOrdinates()
        {
            int newRows = _xValues.Count;
            int newCols = _yValues.Count;
            var current = _innerAnalysis.BivariateResponse;
            int oldRows = current?.GetLength(0) ?? 0;
            int oldCols = current?.GetLength(1) ?? 0;

            if (newRows == oldRows && newCols == oldCols) return;

            var next = new double[newRows, newCols];
            int copyR = Math.Min(oldRows, newRows);
            int copyC = Math.Min(oldCols, newCols);
            for (int i = 0; i < copyR; i++)
                for (int j = 0; j < copyC; j++)
                    next[i, j] = current[i, j];

            _innerAnalysis.BivariateResponse = next;
            // Refresh the rolling baseline so subsequent cell-edit undo diffs are computed
            // against the resized array (not the pre-resize array, which would mismatch
            // dimensions).
            _bivariateResponseSnapshot = SerializeBivariateResponse(_innerAnalysis.BivariateResponse);
        }

        /// <summary>
        /// Validates the upstream <see cref="BivariateAnalysis"/> reference and updates the
        /// associated message-bus messages. Sets BOTH <see cref="_bivariateConfigValid"/>
        /// (BA selected and self-valid; drives <see cref="IsBatchEligible"/>) AND
        /// <see cref="_bivariateValid"/> (above + IsEstimated; drives <see cref="IsValid"/>).
        /// </summary>
        private void ValidateBivariateAnalysis()
        {
            _bivariateConfigValid = true;
            _bivariateValid = true;
            _messenger.Remove(_bivariateNullMsg);
            _messenger.Remove(_bivariateNotEstimatedMsg);
            _messenger.Remove(_bivariateInvalidMsg);

            if (_bivariateAnalysis == null)
            {
                _bivariateConfigValid = false;
                _bivariateValid = false;
                _messenger.Add(_bivariateNullMsg);
                return;
            }
            if (!_bivariateAnalysis.IsValid)
            {
                _bivariateConfigValid = false;
                _bivariateValid = false;
                _messenger.Add(_bivariateInvalidMsg);
                return;
            }
            if (!_bivariateAnalysis.IsEstimated)
            {
                // Estimation is required for IsValid (full readiness) but NOT for batch
                // eligibility — the batch runner orders BAs into Phase 1 before CFAs in
                // Phase 2, so an un-estimated BA included in the batch will be fitted
                // before this CFA runs. _bivariateConfigValid stays true; only
                // _bivariateValid drops.
                _bivariateValid = false;
                _messenger.Add(_bivariateNotEstimatedMsg);
            }
        }

        /// <summary>
        /// Validates the X / Y ordinate arrays and the BivariateResponse surface shape:
        /// each axis must have at least 2 strictly-ascending values; the response must be
        /// non-null with dimensions matching <see cref="XValues"/>.Length × <see cref="YValues"/>.Length.
        /// Sets <see cref="_ordinatesValid"/> and adds/removes <see cref="_ordinatesInvalidMsg"/>.
        /// </summary>
        private void ValidateOrdinates()
        {
            _ordinatesValid = true;
            _messenger.Remove(_ordinatesInvalidMsg);

            int xCount = _xValues?.Count ?? 0;
            int yCount = _yValues?.Count ?? 0;
            if (xCount < 2 || yCount < 2)
            {
                _ordinatesValid = false;
                _messenger.Add(_ordinatesInvalidMsg);
                return;
            }
            for (int i = 1; i < xCount; i++)
            {
                if (!(_xValues[i] > _xValues[i - 1]))
                {
                    _ordinatesValid = false;
                    _messenger.Add(_ordinatesInvalidMsg);
                    return;
                }
            }
            for (int j = 1; j < yCount; j++)
            {
                if (!(_yValues[j] > _yValues[j - 1]))
                {
                    _ordinatesValid = false;
                    _messenger.Add(_ordinatesInvalidMsg);
                    return;
                }
            }
            var resp = _innerAnalysis?.BivariateResponse;
            if (resp == null || resp.GetLength(0) != xCount || resp.GetLength(1) != yCount)
            {
                _ordinatesValid = false;
                _messenger.Add(_ordinatesInvalidMsg);
            }
        }

        /// <summary>
        /// Validates <see cref="NumberOfBins"/> and the BayesianAnalysis output options
        /// (CredibleIntervalWidth ∈ (0, 1), OutputLength ≥ 1, PointEstimator a defined enum).
        /// Sets <see cref="_bayesianOptionsValid"/> and adds/removes <see cref="_bayesianOptionsInvalidMsg"/>.
        /// </summary>
        private void ValidateBayesianOptions()
        {
            _bayesianOptionsValid = true;
            _messenger.Remove(_bayesianOptionsInvalidMsg);

            if (_innerAnalysis == null)
            {
                _bayesianOptionsValid = false;
                _messenger.Add(_bayesianOptionsInvalidMsg);
                return;
            }
            if (_innerAnalysis.NumberOfBins < 2)
            {
                _bayesianOptionsValid = false;
                _messenger.Add(_bayesianOptionsInvalidMsg);
                return;
            }
            var bayes = _innerAnalysis.BayesianAnalysis;
            if (bayes == null)
            {
                _bayesianOptionsValid = false;
                _messenger.Add(_bayesianOptionsInvalidMsg);
                return;
            }
            if (bayes.CredibleIntervalWidth <= 0d || bayes.CredibleIntervalWidth >= 1d ||
                bayes.OutputLength < 1 ||
                bayes.PRNGSeed < 0 ||
                !Enum.IsDefined(typeof(BayesianAnalysis.PointEstimateType), bayes.PointEstimator))
            {
                _bayesianOptionsValid = false;
                _messenger.Add(_bayesianOptionsInvalidMsg);
            }
        }

        /// <summary>
        /// Aggregates UI-only and model-layer validation into the <see cref="IsValid"/> flag.
        /// Callers (property setters, <see cref="BivariateAnalysis_PropertyChanged"/>, <see cref="Open()"/>)
        /// invoke <see cref="ValidateBivariateAnalysis"/> directly when needed; this method only
        /// reads the flags and computes the aggregate. <see cref="InputData"/> validity is
        /// non-blocking (overlay-only) — invalid InputData surfaces a message but does not block
        /// estimation.
        /// </summary>
        private void SetIsValid()
        {
            bool uiValid = true;
            if (!_nameValid) uiValid = false;
            if (!_ordinatesValid) uiValid = false;
            if (!_bayesianOptionsValid) uiValid = false;
            if (!_bivariateValid) uiValid = false;

            // Sync model validation messages through the adapter so the messenger surfaces
            // monotonicity / dimension / NaN errors automatically.
            bool modelValid = _validationAdapter.SyncValidation(_innerAnalysis.Validate(), Name);

            bool valid = uiValid && modelValid;
            if (valid != _isValid)
            {
                _isValid = valid;
                RaisePropertyChange(nameof(IsValid));
            }
        }

        #endregion

        #region Plot Factory Methods

        /// <summary>
        /// Applies the default visual style to a plot (background, legend, padding).
        /// </summary>
        private static void ApplyDefaultPlotStyle(Plot plot)
        {
            plot.BorderThickness = new System.Windows.Thickness(0);
            plot.Background = Brushes.Transparent;
            plot.LegendBackground = ColorFromHex("#8CFFFFFF");
            plot.LegendBorder = Colors.DarkGray;
            plot.LegendPosition = OxyPlot.Legends.LegendPosition.TopRight;
            plot.Padding = new System.Windows.Thickness(10, 10, 14, 10);
            plot.PlotAreaBackground = new SolidColorBrush(Colors.White);
        }

        /// <summary>
        /// Creates the default Stage-Frequency plot (logarithmic AEP axis, linear Z axis).
        /// </summary>
        private static Plot CreateDefaultFrequencyPlot()
        {
            var plot = new Plot();
            ApplyDefaultPlotStyle(plot);
            plot.Title = "Frequency";
            plot.LegendPosition = OxyPlot.Legends.LegendPosition.TopLeft;
            // Y axis: response (Z), linear
            plot.Axes.Add(new OxyPlot.Wpf.LinearAxis
            {
                Key = "Yaxis",
                Position = OxyPlot.Axes.AxisPosition.Left,
                Title = "Response (Z)",
                AxisTitleDistance = 20,
                TitleFontSize = 16,
                FontSize = 12,
                MajorGridlineStyle = OxyPlot.LineStyle.Solid,
                MinorGridlineStyle = OxyPlot.LineStyle.Dash,
            });
            // X axis: AEP, normal-probability scaled (matches BivariateAnalysisControl convention)
            plot.Axes.Add(new OxyPlot.Wpf.NormalProbabilityAxis
            {
                Key = "Xaxis",
                Title = "Annual Exceedance Probability",
                Unit = "P(Z > z)",
                Position = OxyPlot.Axes.AxisPosition.Bottom,
                AxisTitleDistance = 20,
                TitleFontSize = 16,
                FontSize = 12,
            });
            return plot;
        }

        /// <summary>
        /// Converts a hexadecimal color string to a <see cref="Color"/>.
        /// </summary>
        /// <param name="hex">A hex color string, e.g. <c>"#FF0000"</c> or <c>"#8CFFFFFF"</c>.</param>
        /// <returns>The parsed <see cref="Color"/>.</returns>
        private static Color ColorFromHex(string hex) => (Color)ColorConverter.ConvertFromString(hex);

        /// <summary>
        /// Deserializes plot settings from a named column of a SQLite row into an existing
        /// <see cref="Plot"/> object using <see cref="PlotSerializer.FromXElement"/>.
        /// No-ops silently when the column is absent or the cell is empty.
        /// </summary>
        /// <param name="dtView">The table view containing the serialized plot XML.</param>
        /// <param name="rowIndex">Zero-based row index of the element's data row.</param>
        /// <param name="columnName">Name of the column that holds the plot XElement string.</param>
        /// <param name="plot">The <see cref="Plot"/> to populate with the deserialized settings.</param>
        private static void DeserializePlotSettings(DataTableView dtView, int rowIndex, string columnName, Plot plot)
        {
            if (plot == null) return;
            if (!dtView.ColumnNames.Contains(columnName)) return;
            var xml = dtView.GetCell(columnName, rowIndex)?.ToString();
            if (string.IsNullOrEmpty(xml)) return;
            try
            {
                PlotSerializer.FromXElement(plot, XElement.Parse(xml));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Could not deserialize plot settings '{columnName}': {ex.Message}");
            }
        }

        #endregion

        #region Undo Bridge Management

        /// <summary>
        /// Sets up undo bridges for the X / Y ordinate collections, the frequency plot, and the
        /// rolling snapshot for the 2D <see cref="BivariateResponse"/>. Mirrors the canonical
        /// SetupBridges pattern from <see cref="UnivariateAnalysis"/> / <see cref="CompositeAnalysis"/>
        /// — both wrap their <c>ProbabilityOrdinates</c> in an <see cref="UndoableCollectionBridge{T}"/>;
        /// here we wrap <see cref="XValues"/> and <see cref="YValues"/> the same way. The 2D response
        /// array stays on a snapshot-based recorder (<see cref="RecordBivariateResponseUndo"/>) since
        /// the framework has no array-bridge equivalent. Disposes any existing bridges first.
        /// </summary>
        private void SetupBridges()
        {
            DisposeBridges();

            UndoManager.StateChanged += UndoManager_StateChanged;

            Func<IUndoManager> getUndo = () => IsUndoEnabled ? UndoManager : null;

            // Bridges for X / Y ordinate collections — match UnivariateAnalysis / CompositeAnalysis.
            _xValuesBridge = new UndoableCollectionBridge<double>(_xValues, getUndo, nameof(XValues), this);
            _yValuesBridge = new UndoableCollectionBridge<double>(_yValues, getUndo, nameof(YValues), this);

            // BayesianAnalysis output settings bridge — records user edits to CI width,
            // output length, point estimator, and posterior-resampling seed as undo entries. The BayesianAnalysis
            // setters themselves only RaisePropertyChange (no undo recording); this bridge
            // captures the change externally via INotifyPropertyChanged.
            if (_innerAnalysis?.BayesianAnalysis != null)
            {
                _bayesianSettingsBridge = new UndoableStateBridge(
                    _innerAnalysis.BayesianAnalysis,
                    getUndo,
                    "Bayesian settings",
                    this,
                    includedProperties: new[]
                    {
                        nameof(BayesianAnalysis.CredibleIntervalWidth),
                        nameof(BayesianAnalysis.OutputLength),
                        nameof(BayesianAnalysis.PointEstimator),
                        nameof(BayesianAnalysis.PRNGSeed)
                    },
                    onActionRecorded: () => SetIsDirty(true));
            }

            // BivariateResponse keeps a rolling XElement snapshot baseline — there is no
            // UndoableArrayBridge<T> equivalent for double[,] in the framework.
            _bivariateResponseSnapshot = SerializeBivariateResponse(_innerAnalysis?.BivariateResponse);

            // Plot undo manager
            Action onRecorded = () => SetIsDirty(true);

            if (_frequencyPlot != null)
                _frequencyPlotUndo = new PlotUndoManager(_frequencyPlot, getUndo, "frequency plot", this, onRecorded);
        }

        /// <summary>
        /// Revalidates the element after undo/redo completes.
        /// </summary>
        private void UndoManager_StateChanged(object sender, EventArgs e)
        {
            SetIsValid();
        }

        /// <summary>
        /// Disposes all undo bridges and detaches the StateChanged handler.
        /// </summary>
        private void DisposeBridges()
        {
            UndoManager.StateChanged -= UndoManager_StateChanged;

            _xValuesBridge?.Dispose();
            _xValuesBridge = null;
            _yValuesBridge?.Dispose();
            _yValuesBridge = null;

            _bayesianSettingsBridge?.Dispose();
            _bayesianSettingsBridge = null;

            _frequencyPlotUndo?.Dispose();
            _frequencyPlotUndo = null;
        }

        /// <summary>
        /// Suspends plot undo recording during bulk plot updates (e.g., series population).
        /// </summary>
        public IDisposable SuspendPlotBridges()
        {
            var suspensions = new List<IDisposable>();
            if (_frequencyPlotUndo != null) suspensions.Add(_frequencyPlotUndo.SuspendRecording());
            return new AggregateDisposable(suspensions);
        }

        /// <summary>
        /// Rebuilds series and annotation undo bridges after series-collection mutation.
        /// </summary>
        public void RebuildSeriesAndAnnotationBridges(Plot plot)
        {
            if (plot == _frequencyPlot) _frequencyPlotUndo?.RebuildSeriesAndAnnotationBridges();
        }

        /// <summary>
        /// Records a snapshot-based undo entry for the 2D <see cref="BivariateResponse"/> array.
        /// </summary>
        private void RecordBivariateResponseUndo()
        {
            var undoManager = IsUndoEnabled ? UndoManager : null;
            if (undoManager == null || undoManager.IsExecutingAction) return;
            if (_bivariateResponseSnapshot == null)
            {
                _bivariateResponseSnapshot = SerializeBivariateResponse(_innerAnalysis?.BivariateResponse);
                return;
            }

            var currentSnapshot = SerializeBivariateResponse(_innerAnalysis?.BivariateResponse);
            if (XNode.DeepEquals(_bivariateResponseSnapshot, currentSnapshot)) return;

            var oldSnapshot = _bivariateResponseSnapshot;
            var action = new DelegateAction(
                $"Change {nameof(BivariateResponse)}",
                () => RestoreBivariateResponseFromSnapshot(currentSnapshot),
                () => RestoreBivariateResponseFromSnapshot(oldSnapshot),
                this);
            undoManager.RecordAction(action);
            SetIsDirty(true);
            _bivariateResponseSnapshot = currentSnapshot;
        }


        /// <summary>
        /// Restores a serialized 2D response surface during undo/redo.
        /// </summary>
        private void RestoreBivariateResponseFromSnapshot(XElement snapshot)
        {
            _innerAnalysis.BivariateResponse = DeserializeBivariateResponse(snapshot);
            _bivariateResponseSnapshot = SerializeBivariateResponse(_innerAnalysis.BivariateResponse);
            SetIsValid();
            SetIsDirty(true);
            RaisePropertyChange(nameof(BivariateResponse));
        }

        #endregion

    }
}
