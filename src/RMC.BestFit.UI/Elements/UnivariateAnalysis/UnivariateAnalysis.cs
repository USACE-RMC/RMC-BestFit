using DatabaseManager;
using FrameworkInterfaces;
using FrameworkInterfaces.Messaging;
using FrameworkInterfaces.Undo;
using FrameworkInterfaces.Undo.Actions;
using Numerics;
using Numerics.Data;
using Numerics.Data.Statistics;
using Numerics.Distributions;
using Numerics.Sampling.MCMC;
using Numerics.Utilities;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using OxyPlot.Wpf;
using OxyPlot.Wpf.Serialization;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Xml;
using System.Xml.Linq;
using ModelAnalyses = RMC.BestFit.Analyses;

namespace RMC.BestFit.UI
{
    /// <summary>
    /// Univariate distribution analysis UI wrapper.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    /// This class wraps a <see cref="ModelAnalyses.UnivariateAnalysis"/> from the model library,
    /// delegating all computation to the inner analysis while providing UI-specific concerns:
    /// input data management, SQLite persistence, messenger-based validation, and plot settings.
    /// </para>
    /// </remarks>
    [Category("General")]
    [DisplayName("Univariate Distribution Analysis")]
    [Description("Uses Bayesian MCMC to estimate distribution parameters from input data based on a specified parent distribution, providing point estimates and quantifying uncertainty.")]
    [Browsable(true)]
    public class UnivariateAnalysis : ElementBase, IUnivariate
    {

        #region Construction

        /// <summary>
        /// Constructs a new univariate analysis class.
        /// </summary>
        /// <param name="name">The name of the element.</param>
        /// <param name="parentCollection">The parent collection name.</param>
        /// <param name="openFromFile">Optional parameter to open the function from disk upon construction.</param>
        public UnivariateAnalysis(string name, IElementCollection parentCollection, bool openFromFile = false) : base(name, parentCollection)
        {
            IsUndoEnabled = false;
            try
            {
                _nameOnDisk = name;
                _creationDate = DateTime.Now;
                _lastModified = DateTime.Now;

                // Create plot objects
                _frequencyPlot = CreateDefaultFrequencyPlot();
                _chronologyPlot = CreateDefaultChronologyPlot();
                _bayesianController = new BayesianController();

                // Create the inner model analysis
                var dist = new UnivariateDistribution();
                _innerAnalysis = new ModelAnalyses.UnivariateAnalysis(dist);

                // Subscribe to inner analysis property changes and ProbabilityOrdinates collection changes
                SubscribeInnerAnalysis();

                // Add UI-only messages
                _descriptionMsg = new BasicMessageItem(MessageType.Message, "The univariate distribution analysis does not have a description.", this, ParentCollection.Name, Name, nameof(Description), "UDA-MSG-001");
                _inputDataNullMsg = new BasicMessageItem(MessageType.Error, "Input data is missing. Please select valid input data.", this, ParentCollection.Name, Name, nameof(InputData), "UDA-ERR-005");
                _inputDataInValidMsg = new BasicMessageItem(MessageType.Error, "The selected input data is invalid.", this, ParentCollection.Name, Name, nameof(InputData), "UDA-ERR-006");

                _messages = new List<BasicMessageItem>()
                {   _descriptionMsg,
                    _inputDataNullMsg,
                    _inputDataInValidMsg
                };

                // Create messenger and validation adapter
                _messenger = Messenger.GetInstance();
                _validationAdapter = new ValidationMessageAdapter(_messenger, this, ParentCollection.Name, "UDA");
                _messenger.Add(_descriptionMsg);
                _messenger.Add(_inputDataNullMsg);

                if (openFromFile == true) Open();

                _nameValid = ValidateName(BestFitProject.InvalidNameCharacters, 50, "UDA");
                SetIsValid();
                SetIsDirty(openedFromV1);
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
        /// Gets and sets the element name.
        /// </summary>
        [Category("Meta Data")]
        [DisplayName("Name")]
        [Description("Unique label identifying this univariate analysis; max 50 characters.")]
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
                    _nameValid = ValidateName(BestFitProject.InvalidNameCharacters, 50, "UDA");
                    SetIsValid();
                    RecordPropertyChange(nameof(Name), old, value);
                }
            }
        }

        /// <summary>
        /// Gets and sets the element description.
        /// </summary>
        [Category("Meta Data")]
        [DisplayName("Description")]
        [Description("Free-text annotation describing this univariate analysis.")]
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
        /// Gets and sets the element creation date.
        /// </summary>
        [Category("Meta Data")]
        [DisplayName("Creation Date")]
        [Description("The date and time when the analysis was first created.")]
        [Browsable(true)]
        public override DateTime CreationDate => _creationDate;
        /// <summary>
        /// Gets and sets the date when the element was last modified.
        /// </summary>
        [Category("Meta Data")]
        [DisplayName("Last Edited")]
        [Description("The date and time when the analysis was last modified.")]
        [Browsable(true)]
        public override DateTime LastModified => _lastModified;

        #endregion

        #region IElement Properties

        /// <summary>
        /// Represents the name as it appears on disk.
        /// </summary>
        public override string NameOnDisk => _nameOnDisk;

        /// <summary>
        /// Gets the element image as an ImageSource.
        /// </summary>
        public override System.Windows.Media.ImageSource ElementImage => System.Windows.Application.Current?.TryFindResource("UnivariateAnalysisIcon") as System.Windows.Media.ImageSource;

        /// <summary>
        /// Gets the resource key for the element image, enabling dynamic theme updates.
        /// </summary>
        public string ElementImageResourceKey => "UnivariateAnalysisIcon";

        /// <summary>
        /// Determines if the element can be copied from an external application.
        /// </summary>
        public override bool CanCopyFromExternal => false;

        /// <summary>
        /// Determines if the element is valid.
        /// </summary>
        public override bool IsValid
        {
            get { return _isValid; }
        }

        #endregion

        #region Fields

        /// <summary>
        /// The inner model library analysis that owns all computation logic.
        /// </summary>
        private ModelAnalyses.UnivariateAnalysis _innerAnalysis;

        /// <inheritdoc/>
        public ModelAnalyses.IAnalysis InnerAnalysis => _innerAnalysis;

        /// <summary>
        /// Adapter that bridges model Validate() messages to UI BasicMessageItem/Messenger.
        /// </summary>
        private ValidationMessageAdapter _validationAdapter;

        /// <summary>
        /// Collection of UI-only validation messages for the analysis.
        /// </summary>
        private List<BasicMessageItem> _messages;

        /// <summary>
        /// Messaging system for error and validation notifications.
        /// </summary>
        private Messenger _messenger;

        /// <summary>
        /// Message item for missing description.
        /// </summary>
        private BasicMessageItem _descriptionMsg;

        /// <summary>
        /// Message item for null input data.
        /// </summary>
        private BasicMessageItem _inputDataNullMsg;

        /// <summary>
        /// Message item for invalid input data.
        /// </summary>
        private BasicMessageItem _inputDataInValidMsg;

        /// <summary>
        /// Indicates whether the file was opened from version 1.0 format.
        /// </summary>
        private bool openedFromV1 = false;

        /// <summary>
        /// Indicates whether the element name is valid.
        /// </summary>
        private bool _nameValid = false;

        /// <summary>
        /// Indicates whether the input data is valid.
        /// </summary>
        private bool _inputDataValid = false;

        /// <summary>
        /// Indicates whether the probability ordinates are valid.
        /// </summary>
        private bool _ordinatesValid = true;

        /// <summary>
        /// Gets the collection name for storing univariate analyses in the database.
        /// </summary>
        public static string CollectionName => "<Univariate Distribution>";

        /// <summary>
        /// The input data for the analysis.
        /// </summary>
        private InputData _inputData;

        /// <summary>
        /// The frequency plot showing the fitted distribution and data.
        /// </summary>
        private Plot _frequencyPlot;

        /// <summary>
        /// The chronology plot showing nonstationary trends over time.
        /// </summary>
        private Plot _chronologyPlot;

        /// <summary>
        /// Undo manager for the frequency plot.
        /// </summary>
        private PlotUndoManager _frequencyPlotUndo;

        /// <summary>
        /// Undo manager for the chronology plot.
        /// </summary>
        private PlotUndoManager _chronologyPlotUndo;

        /// <summary>
        /// Manages the 6 standard Bayesian MCMC diagnostic plots.
        /// </summary>
        private BayesianController _bayesianController;

        /// <summary>
        /// Undo bridge for probability ordinates collection changes.
        /// </summary>
        private UndoableCollectionBridge<double> _probabilityOrdinatesBridge;

        /// <summary>
        /// Rolling XElement baseline of the <see cref="UnivariateDistribution"/> state.
        /// Captured after every recorded undo action and after initialization/deserialization.
        /// Used by <see cref="InnerAnalysis_PropertyChanged"/> to detect changes and record
        /// <see cref="DelegateAction"/> entries for distribution undo.
        /// </summary>
        private XElement _distributionSnapshot;

        /// <summary>
        /// The set of <see cref="UnivariateDistribution"/> property names that represent user-editable
        /// model state and should trigger XElement snapshot comparison for undo recording.
        /// </summary>
        private static readonly HashSet<string> DistributionUndoProperties = new()
        {
            "DistributionType", "Parameters", "QuantilePriors",
            "TrendModels", "UseJeffreysRuleForScale", "IsNonstationary",
            "ParameterTimeIndex", "Alpha", "EnableQuantilePriors",
            "UseSingleQuantile", "UseDefaultFlatPriors"
        };

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the input data used for evaluating the likelihood function.
        /// </summary>
        [Category("General")]
        [DisplayName("Input Data")]
        [Description("Input Data element supplied to the Bayesian MCMC likelihood function.")]
        [Browsable(true)]
        public InputData InputData
        {
            get { return _inputData; }
            set
            {
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

                // Update the inner analysis model's DataFrame
                if (_innerAnalysis.UnivariateDistribution != null)
                {
                    if (InputData != null && InputData.DataFrame != null)
                        _innerAnalysis.UnivariateDistribution.DataFrame = InputData.DataFrame;
                    else
                        _innerAnalysis.UnivariateDistribution.DataFrame = null;
                }

                // Check if input data is valid
                _inputDataValid = true;
                _messenger.Remove(_inputDataNullMsg);
                _messenger.Remove(_inputDataInValidMsg);
                if (_inputData == null)
                {
                    _inputDataValid = false;
                    _messenger.Add(_inputDataNullMsg);
                }
                if (_inputData != null && _inputData.IsValid == false)
                {
                    _inputDataValid = false;
                    _messenger.Add(_inputDataInValidMsg);
                }
                SetIsValid();
                if (!UndoManager.IsExecutingAction) ClearResults();
                RecordPropertyChange(nameof(InputData), old, value);
            }
        }

        /// <summary>
        /// Gets the exceedance probability values used for plotting the distribution.
        /// </summary>
        public ProbabilityOrdinates ProbabilityOrdinates => _innerAnalysis.ProbabilityOrdinates;

        /// <summary>
        /// The univariate distribution model. Delegates to the inner analysis.
        /// </summary>
        public UnivariateDistribution UnivariateDistribution
        {
            get { return _innerAnalysis.UnivariateDistribution; }
        }

        /// <summary>
        /// The Bayesian Analysis object. Delegates to the inner analysis.
        /// </summary>
        public BayesianAnalysis BayesianAnalysis
        {
            get { return _innerAnalysis.BayesianAnalysis; }
        }

        /// <inheritdoc/>
        public bool IsEstimated
        {
            get { return _innerAnalysis.IsEstimated; }
        }

        /// <summary>
        /// The frequency analysis results. Delegates to the inner analysis.
        /// </summary>
        public UncertaintyAnalysisResults AnalysisResults
        {
            get { return _innerAnalysis.AnalysisResults; }
        }

        /// <summary>
        /// The chronology results. Delegates to the inner analysis.
        /// </summary>
        public UncertaintyAnalysisResults ChronologyAnalysisResults
        {
            get { return _innerAnalysis.ChronologyAnalysisResults; }
        }

        /// <summary>
        /// Gets the frequency plot showing the fitted distribution and data.
        /// </summary>
        public Plot FrequencyPlot => _frequencyPlot;

        /// <summary>
        /// Gets the chronology plot showing nonstationary trends over time.
        /// </summary>
        public Plot ChronologyPlot => _chronologyPlot;

        /// <summary>
        /// Gets the Bayesian MCMC diagnostic plot settings (6 shared diagnostic plots).
        /// </summary>
        public BayesianController BayesianPlots => _bayesianController;

        #endregion

        #endregion

        #region Event Handlers

        /// <summary>
        /// Handles changes to the input data and validates the data, clearing results as needed.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">The property change event arguments.</param>
        private void InputDataChanged(object sender, PropertyChangedEventArgs e)
        {
            // Check if input data is valid
            _inputDataValid = true;
            _messenger.Remove(_inputDataNullMsg);
            _messenger.Remove(_inputDataInValidMsg);
            if (_inputData == null)
            {
                _inputDataValid = false;
                _messenger.Add(_inputDataNullMsg);
            }
            if (_inputData != null && _inputData.IsValid == false)
            {
                _inputDataValid = false;
                _messenger.Add(_inputDataInValidMsg);
            }
            SetIsValid();

            // Check if we need to clear results
            if (e.PropertyName != nameof(InputData.Name) &&
                e.PropertyName != nameof(InputData.DisplayName) &&
                e.PropertyName != nameof(InputData.Description) &&
                e.PropertyName != nameof(InputData.LastModified) &&
                e.PropertyName != nameof(InputData.UnitLabel) &&
                e.PropertyName != nameof(InputData.IndexLabel) &&
                e.PropertyName != nameof(InputData.DataFrame.PlottingParameter) &&
                e.PropertyName != "PlottingPosition" &&
                e.PropertyName != nameof(InputData.IsDirty))
            {
                if (!UndoManager.IsExecutingAction)
                    ClearResults();
            }

            RaisePropertyChange(nameof(InputData));
        }

        /// <summary>
        /// Handles deletion of the <see cref="InputData"/> element from its parent collection.
        /// Clears the reference with undo recording disabled so that pressing Undo cannot
        /// resurrect the now-deleted element. Pre-existing undo history on this analysis
        /// (renames, ordinate edits, etc.) is preserved.
        /// </summary>
        /// <param name="element">The deleted element — ignored; we already hold the reference.</param>
        private void OnInputDataDeleted(IElement element)
        {
            var wasUndoEnabled = IsUndoEnabled;
            IsUndoEnabled = false;
            try { InputData = null; }
            finally { IsUndoEnabled = wasUndoEnabled; }
        }

        /// <summary>
        /// Handles changes to the UI probability ordinates collection.
        /// Syncs changes to the inner analysis and validates the ordinates.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">The collection change event arguments.</param>
        private void ProbabilityOrdinates_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // Check probability ordinates (flag only — messages come from model via adapter in SetIsValid)
            _ordinatesValid = true;
            if (ProbabilityOrdinates.Count == 0)
            {
                _ordinatesValid = false;
            }
            for (int i = 0; i < ProbabilityOrdinates.Count; i++)
            {
                if (i > 0 && ProbabilityOrdinates[i] <= ProbabilityOrdinates[i - 1])
                {
                    _ordinatesValid = false;
                    break;
                }
                if (ProbabilityOrdinates[i] < 0 || ProbabilityOrdinates[i] > 1)
                {
                    _ordinatesValid = false;
                    break;
                }
            }
            SetIsValid();
            // Invalidation (if any) is owned by the model-layer handler now. We only
            // validate + message here. See RMC.BestFit.Analyses.UnivariateAnalysis
            // .ProbabilityOrdinates_CollectionChanged for the reprocess/clear logic.
            RaisePropertyChange(nameof(ProbabilityOrdinates));
        }

        /// <summary>
        /// Forwards property change notifications from the inner analysis to the UI framework.
        /// </summary>
        /// <param name="sender">The inner analysis object.</param>
        /// <param name="e">The property change event arguments.</param>
        private void InnerAnalysis_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Record undo for UnivariateDistribution property changes via XElement snapshot.
            // The model-layer analysis passes distribution property names through unchanged
            // (e.g., "Distribution", "DistributionType", "Parameters", "IsNonstationary").
            if (DistributionUndoProperties.Contains(e.PropertyName))
            {
                RecordDistributionUndo(e.PropertyName);  
            }

            // Forward relevant property changes to the WPF framework
            if (e.PropertyName == nameof(ModelAnalyses.UnivariateAnalysis.IsEstimated) ||
                e.PropertyName == nameof(ModelAnalyses.UnivariateAnalysis.AnalysisResults) ||
                e.PropertyName == nameof(ModelAnalyses.UnivariateAnalysis.ChronologyAnalysisResults) ||
                e.PropertyName == nameof(ModelAnalyses.UnivariateAnalysis.UnivariateDistribution) ||
                e.PropertyName == nameof(ModelAnalyses.UnivariateAnalysis.BayesianAnalysis))
            {
                SetIsValid();
                RaisePropertyChange(e.PropertyName);
            }
            else
            {
                // Forward all other property changes (e.g., from the distribution model or BayesianAnalysis)
                RaisePropertyChange(e.PropertyName);
            }
        }



        /// <summary>
        /// Subscribes to the inner analysis's PropertyChanged event and
        /// the ProbabilityOrdinates CollectionChanged event.
        /// </summary>
        private void SubscribeInnerAnalysis()
        {
            if (_innerAnalysis == null) return;
            _innerAnalysis.PropertyChanged += InnerAnalysis_PropertyChanged;
            _innerAnalysis.ProbabilityOrdinates.CollectionChanged += ProbabilityOrdinates_CollectionChanged;
        }

        /// <summary>
        /// Unsubscribes from the inner analysis's PropertyChanged event and
        /// the ProbabilityOrdinates CollectionChanged event.
        /// </summary>
        private void UnsubscribeInnerAnalysis()
        {
            if (_innerAnalysis == null) return;
            _innerAnalysis.PropertyChanged -= InnerAnalysis_PropertyChanged;
            _innerAnalysis.ProbabilityOrdinates.CollectionChanged -= ProbabilityOrdinates_CollectionChanged;
        }

        #endregion

        #region IElement Methods

        /// <summary>
        /// The required columns for the SQLite table.
        /// If you want to add a new column, add it to the end of the dictionary.
        /// </summary>
        private static Dictionary<string, Type> RequiredColumns { get; } = new Dictionary<string, Type>() {
            { nameof(Name), typeof(string) },
            { nameof(Description), typeof(string) },
            { nameof(CreationDate), typeof(string) },
            { nameof(LastModified), typeof(string) },
            { nameof(InputData), typeof(string) },
            { nameof(UnivariateDistribution), typeof(string) },
            { nameof(BayesianAnalysis), typeof(string) },
            { nameof(MCMCResults), typeof(byte[]) },
            { nameof(AnalysisResults), typeof(string) },
            { nameof(ChronologyAnalysisResults), typeof(string) },
            { nameof(ProbabilityOrdinates), typeof(string) },
            { "FrequencyPlotSettings", typeof(string) },
            { "ChronologyPlotSettings", typeof(string) },
            { "InfluenceDiagnosticsPlotSettings", typeof(string) },
            { "KernelDensityPlotSettings", typeof(string) },
            { "HistogramPlotSettings", typeof(string) },
            { "BivariatePlotSettings", typeof(string) },
            { "MeanLikelihoodPlotSettings", typeof(string) },
            { "AutocorrelationPlotSettings", typeof(string) },
            { "MarkovChainTracesPlotSettings", typeof(string) },
            { "AnalysisXml", typeof(string) },
            { "MCMCReport", typeof(string) } };

        /// <summary>
        /// Creates the SQLite table structure for storing analysis data.
        /// </summary>
        /// <param name="sqlite">The SQLite manager instance.</param>
        private void CreateTable(SQLiteManager sqlite)
        {
            // If the parent collection table does not exist, then create the table.
            if (sqlite.TableNames.Contains(ParentCollection.Name) == false)
            {
                var dataTable = new DataTable(ParentCollection.Name);
                dataTable.Columns.Add("Name", typeof(string));
                dataTable.Columns.Add("Type", typeof(string));
                sqlite.SaveDataTable(dataTable);
            }
            else
            {
                var dt = sqlite.GetTableManager(ParentCollection.Name);
                if (dt.ColumnNames.Contains("Name") == false)
                    dt.AddColumn("Name", typeof(string));
                if (dt.ColumnNames.Contains("Type") == false)
                    dt.AddColumn("Type", typeof(string));
                dt.ApplyEdits();
            }

            // If the element collection table does not exist, then create the table.
            if (sqlite.TableNames.Contains(CollectionName) == false)
            {
                // If the table does not exist, then create the table
                var dataTable = new DataTable(CollectionName);
                foreach (KeyValuePair<string, Type> column in RequiredColumns)
                    dataTable.Columns.Add(column.Key, column.Value);
                sqlite.SaveDataTable(dataTable);

            }
            else
            {
                // Add any required columns that don't exist
                var dt = sqlite.GetTableManager(CollectionName);
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
        /// Opens the element from disk.
        /// </summary>
        public override void Open()
        {
            Open(new SQLiteManager(ParentCollection.ParentProject.FullFileName));
        }

        /// <summary>
        /// Opens the element from disk using the specified SQLite manager.
        /// </summary>
        /// <param name="sqlite">The SQLite manager to use for opening the element.</param>
        public void Open(SQLiteManager sqlite)
        {
            // Suppress undo recording during deserialization to prevent plot PropertyChanged
            // events from polluting the undo stack. Mirrors constructor and Copy() patterns.
            var wasUndoEnabled = IsUndoEnabled;
            IsUndoEnabled = false;
            try
            {

            openedFromV1 = false;
            _messenger.Clear(this);
            _validationAdapter.ClearAll();
            var wasOpen = sqlite.DataBaseOpen;
            if (wasOpen == false) sqlite.Open();

            // First check if we need to open from version 1.0.
            var dtView = sqlite.GetTableManager("Project");
            string version = "";
            if (dtView.ColumnNames.Contains(nameof(BestFitProject.SoftwareVersion))) version = dtView.GetCell(nameof(BestFitProject.SoftwareVersion), 0).ToString();
            if (version == "1.0")
            {
                openedFromV1 = true;
                OpenFromVersion1(sqlite);
            }
            else
            {
                // open element
                dtView = sqlite.GetTableManager(CollectionName);
                int rowIndex = dtView.SearchColumn(0, dtView.NumberOfRows - 1, "Name", NameOnDisk, true, true);
                if (rowIndex != -1)
                {
                    // Use backing fields during deserialization to avoid repeated SetIsValid() and ClearResults() calls.
                    // The InputData setter triggers SetIsValid(), ClearResults(), and updates inner analysis DataFrame.
                    // ProbabilityOrdinates.FromDelimitedString() triggers CollectionChanged â†’ SetIsValid() + ClearResults().
                    // A single SetIsValid() call at the end of Open() is sufficient.
                    if (dtView.ColumnNames.Contains(nameof(Name)))
                    {
                        _name = dtView.GetCell(nameof(Name), rowIndex).ToString();
                        foreach (var item in _messages) item.SourceName = _name;
                        _nameValid = ValidateName(BestFitProject.InvalidNameCharacters, 50, "UDA");
                    }
                    if (dtView.ColumnNames.Contains(nameof(Description)))
                    {
                        _description = dtView.GetCell(nameof(Description), rowIndex).ToString();
                        if (string.IsNullOrEmpty(_description))
                            _messenger.Add(_descriptionMsg);
                        else
                            _messenger.Remove(_descriptionMsg);
                    }
                    if (dtView.ColumnNames.Contains(nameof(CreationDate))) _creationDate = FrameworkInterfaces.Utilities.Tools.DateFromString(dtView.GetCell(nameof(CreationDate), rowIndex).ToString()) ?? DateTime.MinValue;
                    if (dtView.ColumnNames.Contains(nameof(LastModified))) _lastModified = FrameworkInterfaces.Utilities.Tools.DateFromString(dtView.GetCell(nameof(LastModified), rowIndex).ToString()) ?? DateTime.MinValue;
                    // Plot Properties — deserialize into Plot objects
                    DeserializePlotSettings(dtView, rowIndex, "FrequencyPlotSettings", _frequencyPlot);
                    DeserializePlotSettings(dtView, rowIndex, "ChronologyPlotSettings", _chronologyPlot);
                    _bayesianController.Deserialize(dtView, rowIndex);


                    // Get input data - use backing field to avoid SetIsValid() and ClearResults()
                    if (dtView.ColumnNames.Contains(nameof(InputData)))
                    {
                        var inputDataName = dtView.GetCell(nameof(InputData), rowIndex).ToString();
                        foreach (IElementCollection collection in ParentCollection.ParentProject.ElementCollections)
                        {
                            if (collection.GetType() == typeof(InputDataCollection))
                            {
                                foreach (IElement element in collection)
                                {
                                    if (element.Name == inputDataName && element.GetType() == typeof(InputData))
                                    {
                                        _inputData = (InputData)element;
                                        _inputData.PropertyChanged += InputDataChanged;
                                        _inputDataValid = _inputData.IsValid;
                                        if (!_inputDataValid)
                                        {
                                            _messenger.Remove(_inputDataNullMsg);
                                            _messenger.Add(_inputDataInValidMsg);
                                        }
                                        else
                                        {
                                            _messenger.Remove(_inputDataNullMsg);
                                            _messenger.Remove(_inputDataInValidMsg);
                                        }
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    // If InputData was not found, re-add the null message (cleared by _messenger.Clear above)
                    if (_inputData == null)
                        _messenger.Add(_inputDataNullMsg);

                    // Get model and reconstruct the inner analysis
                    if (dtView.ColumnNames.Contains(nameof(UnivariateDistribution)) && InputData != null && InputData.DataFrame != null)
                    {
                        var modelXElement = XElement.Parse(dtView.GetCell(nameof(UnivariateDistribution), rowIndex).ToString());
                        var dist = new UnivariateDistribution(InputData.DataFrame, modelXElement);

                        // Build the analysis XElement — try AnalysisXml first (atomic), fall back to legacy columns
                        XElement analysisXElement = AnalysisPersistenceHelper.TryLoadXElement(dtView, "AnalysisXml", rowIndex, Name);
                        if (analysisXElement == null)
                        {
                            // Legacy fallback: build XElement from individual columns
                            analysisXElement = new XElement("UnivariateAnalysis");
                            if (dtView.ColumnNames.Contains(nameof(ProbabilityOrdinates)))
                            {
                                var probStr = dtView.GetCell(nameof(ProbabilityOrdinates), rowIndex).ToString();
                                analysisXElement.Add(new XElement("ProbabilityOrdinates", probStr));
                            }
                            if (dtView.ColumnNames.Contains(nameof(BayesianAnalysis)))
                            {
                                var bayesStr = dtView.GetCell(nameof(BayesianAnalysis), rowIndex).ToString();
                                if (!string.IsNullOrEmpty(bayesStr))
                                    analysisXElement.Add(XElement.Parse(bayesStr));
                            }
                        }

                        // Load MCMCResults from byte[] column
                        MCMCResults mcmcResults = AnalysisPersistenceHelper.TryLoadMCMCResults(dtView, rowIndex, Name);

                        // Set IsEstimated attribute based on whether MCMC results exist (for legacy fallback XElement)
                        if (analysisXElement.Attribute("IsEstimated") == null)
                            analysisXElement.Add(new XAttribute("IsEstimated", mcmcResults != null));

                        // Load AnalysisResults from XML column
                        UncertaintyAnalysisResults analysisResults =
                            AnalysisPersistenceHelper.TryLoadAnalysisResults(dtView, nameof(AnalysisResults), rowIndex, Name);

                        // Load ChronologyAnalysisResults from XML column
                        UncertaintyAnalysisResults chronResults =
                            AnalysisPersistenceHelper.TryLoadAnalysisResults(dtView, nameof(ChronologyAnalysisResults), rowIndex, Name);

                        // Reconstruct inner analysis — model handles XElement parsing + results restoration
                        UnsubscribeInnerAnalysis();
                        _innerAnalysis = new ModelAnalyses.UnivariateAnalysis(
                            dist, analysisXElement, mcmcResults, analysisResults, chronResults);
                        SubscribeInnerAnalysis();
                    }

                }

            }

            if (wasOpen == false) sqlite.Close();

            // Reconnect undo bridges to the (possibly new) inner analysis collections.
            // Critical when Open() is called outside the constructor (e.g., CopyFromExternal),
            // because the constructor's finally block already ran SetupBridges() on the old default
            // _innerAnalysis, and Open() just replaced it.
            SetupBridges();

            SetIsValid();
            SetIsDirty(openedFromV1);
            RaisePropertyChange(nameof(Name), setDirty: openedFromV1);  // Notify node header binding that Name was restored from disk

            // Ensure WPF bindings point at the correct collection/object after deserialization
            // replaced _innerAnalysis. Without these, bindings may hold stale references.
            RaisePropertyChange(nameof(ProbabilityOrdinates), setDirty: false);
            RaisePropertyChange(nameof(BayesianAnalysis), setDirty: false);

            }
            finally
            {
                IsUndoEnabled = wasUndoEnabled;
                if (wasUndoEnabled) ClearUndoHistory();
            }
        }

        /// <summary>
        /// Opens the univariate Bayesian analysis from BestFit version 1.0.
        /// </summary>
        /// <param name="sqlite">The SQLite manager.</param>
        private void OpenFromVersion1(SQLiteManager sqlite)
        {
            var dtView = sqlite.GetTableManager("Bayesian Estimation Analysis");
            int rowIndex = dtView.SearchColumn(0, dtView.NumberOfRows - 1, "Name", NameOnDisk, true, true);
            if (rowIndex != -1)
            {
                if (dtView.ColumnNames.Contains(nameof(Name)))
                {
                    _name = dtView.GetCell(nameof(Name), rowIndex).ToString();
                    foreach (var item in _messages) item.SourceName = _name;
                    _nameValid = ValidateName(BestFitProject.InvalidNameCharacters, 50, "UDA");
                }
                if (dtView.ColumnNames.Contains(nameof(Description)))
                {
                    _description = dtView.GetCell(nameof(Description), rowIndex).ToString();
                    if (string.IsNullOrEmpty(_description))
                        _messenger.Add(_descriptionMsg);
                    else
                        _messenger.Remove(_descriptionMsg);
                }
                if (dtView.ColumnNames.Contains(nameof(CreationDate))) _creationDate = FrameworkInterfaces.Utilities.Tools.DateFromString(dtView.GetCell(nameof(CreationDate), rowIndex).ToString()) ?? DateTime.MinValue;
                if (dtView.ColumnNames.Contains(nameof(LastModified))) _lastModified = FrameworkInterfaces.Utilities.Tools.DateFromString(dtView.GetCell(nameof(LastModified), rowIndex).ToString()) ?? DateTime.MinValue;
                // Get input data
                if (dtView.ColumnNames.Contains(nameof(InputData)))
                {
                    var inputDataName = dtView.GetCell(nameof(InputData), rowIndex).ToString();
                    foreach (IElementCollection collection in ParentCollection.ParentProject.ElementCollections)
                    {
                        if (collection.GetType() == typeof(InputDataCollection))
                        {
                            foreach (IElement element in collection)
                            {
                                if (element.Name == inputDataName && element.GetType() == typeof(InputData))
                                {
                                    InputData = (InputData)element;
                                    break;
                                }
                            }
                        }
                    }
                }
                // Plot Properties — deserialize into Plot objects (v1 format used OxyPlotSettingsSerializer)
                DeserializePlotSettings(dtView, rowIndex, "FrequencyPlotSettings", _frequencyPlot);
                DeserializePlotSettings(dtView, rowIndex, "KernelDensityPlotSettings", _bayesianController.KernelDensityPlot);
                DeserializePlotSettings(dtView, rowIndex, "HistogramPlotSettings", _bayesianController.HistogramPlot);
                DeserializePlotSettings(dtView, rowIndex, "BivariatePlotSettings", _bayesianController.BivariateHeatMapPlot);
                DeserializePlotSettings(dtView, rowIndex, "MeanLikelihoodPlotSettings", _bayesianController.MeanLikelihoodPlot);
                DeserializePlotSettings(dtView, rowIndex, "AutocorrelationPlotSettings", _bayesianController.AutocorrelationPlot);
                DeserializePlotSettings(dtView, rowIndex, "MarkovChainTracesPlotSettings", _bayesianController.MarkovChainTracePlot);

                // Get probability ordinates
                if(dtView.ColumnNames.Contains("OutputFrequencyOrdinates"))
                {
                    string xmlText = dtView.GetCell("OutputFrequencyOrdinates", rowIndex).ToString();
                    XmlDocument xmlDocument = new XmlDocument();
                    xmlDocument.LoadXml(xmlText);
                    if (xmlDocument.GetElementsByTagName("OutputFrequencyOrdinates").Item(0).ChildNodes.Count == 0)
                        return;
                    ProbabilityOrdinates.Clear();
                    foreach (XmlNode node in xmlDocument.GetElementsByTagName("OutputFrequencyOrdinates").Item(0).ChildNodes)
                    {
                        double.TryParse(node.Attributes.GetNamedItem("AEP").Value.ToString(), out var outP);
                        ProbabilityOrdinates.Add(outP);
                    }
                }

                // Unsubscribe from old inner analysis (including ProbabilityOrdinates.CollectionChanged)
                UnsubscribeInnerAnalysis();

                // Create model
                UnivariateDistribution dist = null;
                if (dtView.ColumnNames.Contains("ParentDistribution") && InputData != null && InputData.DataFrame != null)
                {
                    UnivariateDistributionType type;
                    Enum.TryParse(dtView.GetCell("ParentDistribution", rowIndex).ToString(), out type);
                    dist = new UnivariateDistribution(InputData.DataFrame, type);
                    dist.UseJeffreysRuleForScale = false;
                }

                if (dist == null)
                {
                    _innerAnalysis = new ModelAnalyses.UnivariateAnalysis(new UnivariateDistribution());
                    SubscribeInnerAnalysis();
                    return;
                }

                // Parameter Priors
                if (dtView.ColumnNames.Contains(nameof(UnivariateDistribution.UseDefaultFlatPriors)))
                {
                    bool.TryParse(dtView.GetCell(nameof(UnivariateDistribution.UseDefaultFlatPriors), rowIndex).ToString(), out var result);
                    dist.UseDefaultFlatPriors = result;
                }
                if (dtView.ColumnNames.Contains("PriorDistributions"))
                {
                    string xmlText = dtView.GetCell("PriorDistributions", rowIndex).ToString();
                    XmlDocument xmlDocument = new XmlDocument();
                    xmlDocument.LoadXml(xmlText);
                    if (xmlDocument.GetElementsByTagName("PriorDistributions").Item(0).ChildNodes.Count == 0)
                        return;

                    var distList = new Dictionary<string, UnivariateDistributionBase>()
                    {
                        {"Exponential", new Exponential()},
                        {"Gamma", new GammaDistribution()},
                        {"Generalized Beta", new GeneralizedBeta()},
                        {"Ln-Normal", new LnNormal()},
                        {"Noncentral t", new NoncentralT()},
                        {"Normal", new Normal()},
                        {"PERT", new Pert()},
                        {"Student's t", new StudentT()},
                        {"Triangular", new Triangular()},
                        {"Truncated Normal", new TruncatedNormal()},
                        {"Uniform", new Uniform()}
                    };

                    for (int i = 0; i < xmlDocument.GetElementsByTagName("PriorDistributions").Item(0).ChildNodes.Count; i++)
                    {
                        var node = xmlDocument.GetElementsByTagName("PriorDistributions").Item(0).ChildNodes[i];
                        string displayName = node.Attributes.GetNamedItem("Distribution").Value.ToString();
                        var keys = distList.Keys.ToList();
                        for (int j = 0; j < keys.Count; j++)
                        {
                            if (displayName == keys[j])
                            {
                                var parmNames = distList[keys[j]].GetParameterPropertyNames;
                                var parms = new double[distList[keys[j]].NumberOfParameters];
                                for (int k = 0; k < parmNames.Length; k++)
                                {
                                    double.TryParse(node.Attributes.GetNamedItem(parmNames[k]).Value.ToString(), out var result);
                                    parms[k] = result;
                                }
                                distList[keys[j]].SetParameters(parms);
                                dist.Parameters[i].PriorDistribution = distList[keys[j]].Clone();
                            }
                        }
                    }
                }

                // Quantile Priors
                if (dtView.ColumnNames.Contains("EnablePriorQuantileDistributions"))
                {
                    bool.TryParse(dtView.GetCell("EnablePriorQuantileDistributions", rowIndex).ToString(), out var result);
                    dist.EnableQuantilePriors = result;
                }
                if (dtView.ColumnNames.Contains(nameof(UnivariateDistribution.UseSingleQuantile)))
                {
                    bool.TryParse(dtView.GetCell(nameof(UnivariateDistribution.UseSingleQuantile), rowIndex).ToString(), out var result);
                    dist.UseSingleQuantile = result;
                }
                if (dtView.ColumnNames.Contains("PriorQuantileDistributions"))
                {
                    string xmlText = dtView.GetCell("PriorQuantileDistributions", rowIndex).ToString();
                    XmlDocument xmlDocument = new XmlDocument();
                    xmlDocument.LoadXml(xmlText);
                    if (xmlDocument.GetElementsByTagName("PriorQuantileDistributions").Item(0).ChildNodes.Count == 0)
                        return;

                    var distList = new Dictionary<string, UnivariateDistributionBase>()
                    {
                        {"Exponential", new Exponential()},
                        {"Gamma", new GammaDistribution()},
                        {"Generalized Beta", new GeneralizedBeta()},
                        {"Ln-Normal", new LnNormal()},
                        {"Noncentral t", new NoncentralT()},
                        {"Normal", new Normal()},
                        {"PERT", new Pert()},
                        {"Student's t", new StudentT()},
                        {"Triangular", new Triangular()},
                        {"Truncated Normal", new TruncatedNormal()},
                        {"Uniform", new Uniform()}
                    };

                    var priors = new List<QuantilePrior>();
                    for (int i = 0; i < xmlDocument.GetElementsByTagName("PriorQuantileDistributions").Item(0).ChildNodes.Count; i++)
                    {
                        var node = xmlDocument.GetElementsByTagName("PriorQuantileDistributions").Item(0).ChildNodes[i];
                        double.TryParse(node.Attributes.GetNamedItem("AEP").Value.ToString(), out var aep);
                        string displayName = node.Attributes.GetNamedItem("Distribution").Value.ToString();
                        var keys = distList.Keys.ToList();
                        for (int j = 0; j < keys.Count; j++)
                        {
                            if (displayName == keys[j])
                            {
                                var parmNames = distList[keys[j]].GetParameterPropertyNames;
                                var parms = new double[distList[keys[j]].NumberOfParameters];
                                for (int k = 0; k < parmNames.Length; k++)
                                {
                                    double.TryParse(node.Attributes.GetNamedItem(parmNames[k]).Value.ToString(), out var result);
                                    parms[k] = result;
                                }
                                distList[keys[j]].SetParameters(parms);
                                priors.Add(new QuantilePrior(aep, distList[keys[j]].Clone()));
                            }
                        }
                    }
                    dist.QuantilePriors = priors;
                }

                // Create a new inner analysis with the restored distribution
                _innerAnalysis = new ModelAnalyses.UnivariateAnalysis(dist);

                // Restore Bayesian analysis settings from v1.0 format
                if (dtView.ColumnNames.Contains(nameof(BayesianAnalysis.UseSimulationDefaults)))
                {
                    bool.TryParse(dtView.GetCell(nameof(BayesianAnalysis.UseSimulationDefaults), rowIndex).ToString(), out var result);
                    _innerAnalysis.BayesianAnalysis.UseSimulationDefaults = result;
                }
                if (dtView.ColumnNames.Contains(nameof(BayesianAnalysis.NumberOfChains)))
                {
                    int.TryParse(dtView.GetCell(nameof(BayesianAnalysis.NumberOfChains), rowIndex).ToString(), out var result);
                    _innerAnalysis.BayesianAnalysis.NumberOfChains = result;
                }
                if (dtView.ColumnNames.Contains(nameof(BayesianAnalysis.WarmupIterations)))
                {
                    int.TryParse(dtView.GetCell(nameof(BayesianAnalysis.WarmupIterations), rowIndex).ToString(), out var result);
                    _innerAnalysis.BayesianAnalysis.WarmupIterations = result;
                }
                if (dtView.ColumnNames.Contains(nameof(BayesianAnalysis.Iterations)))
                {
                    int.TryParse(dtView.GetCell(nameof(BayesianAnalysis.Iterations), rowIndex).ToString(), out var result);
                    _innerAnalysis.BayesianAnalysis.Iterations = result;
                }
                if (dtView.ColumnNames.Contains(nameof(BayesianAnalysis.ThinningInterval)))
                {
                    int.TryParse(dtView.GetCell(nameof(BayesianAnalysis.ThinningInterval), rowIndex).ToString(), out var result);
                    _innerAnalysis.BayesianAnalysis.ThinningInterval = result;
                }
                if (dtView.ColumnNames.Contains(nameof(BayesianAnalysis.UseAdvancedSimulationDefaults)))
                {
                    bool.TryParse(dtView.GetCell(nameof(BayesianAnalysis.UseAdvancedSimulationDefaults), rowIndex).ToString(), out var result);
                    _innerAnalysis.BayesianAnalysis.UseAdvancedSimulationDefaults = result;
                }
                if (dtView.ColumnNames.Contains(nameof(BayesianAnalysis.PRNGSeed)))
                {
                    int.TryParse(dtView.GetCell(nameof(BayesianAnalysis.PRNGSeed), rowIndex).ToString(), out var result);
                    _innerAnalysis.BayesianAnalysis.PRNGSeed = result;
                }
                if (dtView.ColumnNames.Contains(nameof(BayesianAnalysis.InitialIterations)))
                {
                    int.TryParse(dtView.GetCell(nameof(BayesianAnalysis.InitialIterations), rowIndex).ToString(), out var result);
                    _innerAnalysis.BayesianAnalysis.InitialIterations = result;
                }
                if (dtView.ColumnNames.Contains(nameof(BayesianAnalysis.Jump)))
                {
                    double.TryParse(dtView.GetCell(nameof(BayesianAnalysis.Jump), rowIndex).ToString(), out var result);
                    _innerAnalysis.BayesianAnalysis.Jump = result;
                }
                if (dtView.ColumnNames.Contains(nameof(BayesianAnalysis.JumpThreshold)))
                {
                    double.TryParse(dtView.GetCell(nameof(BayesianAnalysis.JumpThreshold), rowIndex).ToString(), out var result);
                    _innerAnalysis.BayesianAnalysis.JumpThreshold = result;
                }
                if (dtView.ColumnNames.Contains(nameof(BayesianAnalysis.Noise)))
                {
                    double.TryParse(dtView.GetCell(nameof(BayesianAnalysis.Noise), rowIndex).ToString(), out var result);
                    _innerAnalysis.BayesianAnalysis.Noise = result;
                }
                if (dtView.ColumnNames.Contains(nameof(BayesianAnalysis.CredibleIntervalWidth)))
                {
                    double.TryParse(dtView.GetCell(nameof(BayesianAnalysis.CredibleIntervalWidth), rowIndex).ToString(), out var result);
                    _innerAnalysis.BayesianAnalysis.CredibleIntervalWidth = result;
                }
                if (dtView.ColumnNames.Contains(nameof(BayesianAnalysis.OutputLength)))
                {
                    int.TryParse(dtView.GetCell(nameof(BayesianAnalysis.OutputLength), rowIndex).ToString(), out var result);
                    _innerAnalysis.BayesianAnalysis.OutputLength = result;
                }

                // Clear results for v1 migration
                _innerAnalysis.ClearResults();
                SubscribeInnerAnalysis();
            }
        }

        /// <summary>
        /// Raises the PreviewObjectSaved event before saving the element.
        /// </summary>
        /// <param name="cancel">Output parameter that determines if the save operation should be canceled.</param>
        public void RaisePreviewSaved(ref bool cancel)
        {
            RaisePreviewObjectSaved(this, ref cancel);
        }

        /// <summary>
        /// Save the element to disk.
        /// </summary>
        public override void Save()
        {
            if (Name == null) return;

            // Create SQLite connection
            var sqlite = new SQLiteManager(ParentCollection.ParentProject.FullFileName);
            sqlite.Open();
            DateTime previousLastModified = _lastModified;
            bool committed = false;
            try
            {
                // Only update last edited if user data actually changed
                if (IsDirty)
                {
                    _lastModified = DateTime.Now;
                    RaisePropertyChange(nameof(LastModified));
                }

                // Create the element collection table if it doesn't exist.
                CreateTable(sqlite);

                // Update parent collection table — key by Name (not ParentCollection.IndexOf(this))
                // for consistency with the per-subtype table lookup at line 1244 below. Index-keying
                // is unsafe when the parent table is out of sync with ElementList (rare but possible
                // after a partial save failure pre-B-001, or when an element is saved out of band).
                var dtView = sqlite.GetTableManager(ParentCollection.Name);
                int rowIndex = dtView.SearchColumn(0, dtView.NumberOfRows - 1, "Name", NameOnDisk, true, true);
                if (rowIndex < 0 || rowIndex >= dtView.NumberOfRows)
                {
                    dtView.AddRow();
                    rowIndex = dtView.NumberOfRows - 1;
                }
                dtView.EditCell(rowIndex, "Name", Name);
                dtView.EditCell(rowIndex, "Type", this.GetType().ToString());
                dtView.ApplyEdits();

                // Update collection table
                dtView = sqlite.GetTableManager(CollectionName);
                rowIndex = dtView.SearchColumn(0, dtView.NumberOfRows - 1, "Name", NameOnDisk, true, true);
                if (rowIndex < 0 || rowIndex >= dtView.NumberOfRows)
                {
                    dtView.AddRow();
                    rowIndex = dtView.NumberOfRows - 1;
                }

                dtView.EditCell(rowIndex, nameof(Name), Name);
                dtView.EditCell(rowIndex, nameof(Description), Description);
                dtView.EditCell(rowIndex, nameof(CreationDate), FrameworkInterfaces.Utilities.Tools.DateToUniversalString(CreationDate));
                dtView.EditCell(rowIndex, nameof(LastModified), FrameworkInterfaces.Utilities.Tools.DateToUniversalString(LastModified));
                dtView.EditCell(rowIndex, nameof(InputData), InputData == null ? "" : InputData.Name);

                // Serialize from inner analysis
                dtView.EditCell(rowIndex, nameof(UnivariateDistribution), _innerAnalysis.UnivariateDistribution.ToXElement().ToString());
                dtView.EditCell(rowIndex, nameof(BayesianAnalysis), _innerAnalysis.BayesianAnalysis.ToXElement().ToString());

                dtView.EditCell(rowIndex, nameof(MCMCResults),
                    AnalysisPersistenceHelper.SerializeMCMCResults(_innerAnalysis.BayesianAnalysis.Results));
                dtView.EditCell(rowIndex, nameof(AnalysisResults),
                    AnalysisPersistenceHelper.SerializeAnalysisResults(_innerAnalysis.AnalysisResults));
                dtView.EditCell(rowIndex, nameof(ChronologyAnalysisResults),
                    AnalysisPersistenceHelper.SerializeAnalysisResults(_innerAnalysis.ChronologyAnalysisResults));

                dtView.EditCell(rowIndex, nameof(ProbabilityOrdinates), ProbabilityOrdinates?.ToDelimitedString("|") ?? "");

                // Serialize plot objects
                dtView.EditCell(rowIndex, "FrequencyPlotSettings", _frequencyPlot != null ? PlotSerializer.ToXElement(_frequencyPlot).ToString() : "");
                dtView.EditCell(rowIndex, "ChronologyPlotSettings", _chronologyPlot != null ? PlotSerializer.ToXElement(_chronologyPlot).ToString() : "");
                _bayesianController.Serialize(dtView, rowIndex);

                // Atomic save of analysis state via ToXElement (BayesianAnalysis + ProbabilityOrdinates + IsEstimated)
                dtView.EditCell(rowIndex, "AnalysisXml", _innerAnalysis?.ToXElement()?.ToString() ?? "");

                dtView.ApplyEdits();
                sqlite.Close();
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

        /// <summary>
        /// Copies the element. You can optionally provide a new name when copying the element.
        /// </summary>
        /// <param name="newName">Optional. New name of the cloned element.</param>
        /// <returns>A new copy of the element.</returns>
        public override IElement Copy(string newName = "")
        {
            var element = new UnivariateAnalysis(newName == "" || string.IsNullOrEmpty(newName) ? Name : newName, ParentCollection);

            element.IsUndoEnabled = false;
            try
            {
                element.Description = Description;

                // Reconstruct inner analysis with cloned distribution
                element.UnsubscribeInnerAnalysis();
                var clonedDist = (UnivariateDistribution)UnivariateDistribution.Clone();
                element._innerAnalysis = new ModelAnalyses.UnivariateAnalysis(clonedDist);
                element.SubscribeInnerAnalysis();

                // Set input data
                element.InputData = InputData;

                // Copy probability ordinates into the new element's existing collection
                if (ProbabilityOrdinates != null && ProbabilityOrdinates.Count > 0)
                    element._innerAnalysis.ProbabilityOrdinates.FromDelimitedString(
                        ProbabilityOrdinates.ToDelimitedString(ProbabilityOrdinates.DefaultDelimiter),
                        ProbabilityOrdinates.DefaultDelimiter);

                // Copy plot settings via PlotSerializer round-trip (inside undo suppression — matches FittingAnalysis.Copy template)
                if (_frequencyPlot != null) PlotSerializer.FromXElement(element._frequencyPlot, PlotSerializer.ToXElement(_frequencyPlot));
                if (_chronologyPlot != null) PlotSerializer.FromXElement(element._chronologyPlot, PlotSerializer.ToXElement(_chronologyPlot));
                _bayesianController.CopyTo(element._bayesianController);

                // Clear results on the copy
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

        /// <summary>
        /// Copy the object from an external project to disk within the current project.
        /// </summary>
        /// <param name="itemName">The item to copy from.</param>
        /// <param name="fullFileName">The full file name of the project to copy from.</param>
        /// <returns>The copied element.</returns>
        public override IElement CopyFromExternal(string itemName, string fullFileName)
        {
            // Create SQLite connection
            var sqlite = new SQLiteManager(fullFileName);
            var element = new UnivariateAnalysis(itemName, ParentCollection);
            element.Open(sqlite);
            return element;
        }

        /// <summary>
        /// Deletes the element from disk and from the parent element collection.
        /// </summary>
        public override void Delete()
        {
            if (Name == null) return;
            // Unhook upstream Deleted subscription so the upstream's event-handler list
            // does not keep this instance alive. We do NOT go through the InputData setter
            // here — that setter would re-add _inputDataNullMsg and re-flip IsDirty=true,
            // which would break messenger cleanup and trigger a spurious save prompt when
            // an open tab for this element is closed afterwards.
            if (_inputData != null) _inputData.Deleted -= OnInputDataDeleted;
            DisposeBridges();
            _bayesianController?.Dispose();
            UnsubscribeInnerAnalysis();
            SetIsDirty(false);
            // Create SQLite connection
            var sqlite = new SQLiteManager(ParentCollection.ParentProject.FullFileName);
            sqlite.Open();
            try
            {
                // Parent collection
                var dtView = sqlite.GetTableManager(ParentCollection.Name);
                int rowIndex = dtView.SearchColumn(0, dtView.NumberOfRows - 1, "Name", NameOnDisk, true, true);
                if (rowIndex >= 0 && rowIndex < dtView.NumberOfRows)
                {
                    dtView.DeleteRow(rowIndex);
                }
                dtView.ApplyEdits();
                // This collection
                dtView = sqlite.GetTableManager(CollectionName);
                rowIndex = dtView.SearchColumn(0, dtView.NumberOfRows - 1, "Name", NameOnDisk, true, true);
                if (rowIndex >= 0 && rowIndex < dtView.NumberOfRows)
                {
                    dtView.DeleteRow(rowIndex);
                }
                dtView.ApplyEdits();
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
        /// Sets the IsValid property for the element by combining UI-only validations
        /// with model library validation results.
        /// </summary>
        private void SetIsValid()
        {
            bool valid = true;

            // UI-only validations
            if (_nameValid == false) valid = false;
            if (_inputDataValid == false) valid = false;
            if (_ordinatesValid == false) valid = false;

            // Delegate model validation to inner analysis.
            // Skip model validation when InputData is invalid — the UI layer already reports that
            // via _inputDataNullMsg / _inputDataInValidMsg, and the model's "DataFrame is null"
            // message would be a confusing developer-facing duplicate.
            bool modelValid = _inputDataValid
                ? _validationAdapter.SyncValidation(_innerAnalysis.Validate(), Name)
                : _validationAdapter.SyncValidation((true, new List<string>()), Name);
            if (modelValid == false) valid = false;

            if (valid != _isValid)
            {
                _isValid = valid;
                RaisePropertyChange(nameof(IsValid));
            }
        }

        #endregion

        #region Analysis Methods

        /// <summary>
        /// Clears all analysis results by delegating to the inner analysis.
        /// </summary>
        public void ClearResults()
        {
            _innerAnalysis.ClearResults();
            RaisePropertyChange(nameof(AnalysisResults));
            RaisePropertyChange(nameof(ChronologyAnalysisResults));
        }

        /// <inheritdoc/>
        public async Task RunAsync(SafeProgressReporter progressReporter)
        {
            // Snapshot validity locally — guards against a hypothetical scenario where a
            // dispatcher re-entrancy (a binding firing during SetIsValid) flips IsValid back
            // to true between the SetIsValid() call and the gate. Defensive; in current code
            // SetIsValid is fully synchronous on the dispatcher.
            SetIsValid();
            bool valid = IsValid;
            if (!valid) return;

            _messenger.Add(new BasicMessageItem(MessageType.Event, "The univariate distribution analysis for '" + Name + "' has started.", this, ParentCollection.Name, Name, nameof(UnivariateAnalysis)));

            try
            {
                // Delegate to inner analysis RunAsync (inner analysis handles ProcessThresholdSeries and CreateFullTimeSeries)
                progressReporter.IndicateTaskStart();
                await _innerAnalysis.RunAsync(progressReporter);
            }
            catch (OperationCanceledException)
            {
                // User cancelled — clear results silently
                ClearResults();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UnivariateAnalysis.RunAsync failed for '{Name}': {ex}");
                ClearResults();
                _messenger.Add(new BasicMessageItem(MessageType.Error, $"The univariate distribution analysis for '{Name}' failed: {ex.Message}", this, ParentCollection.Name, Name, nameof(UnivariateAnalysis)));
            }
            finally
            {
                progressReporter.IndicateTaskEnded();
                SetIsValid();
                _messenger.Add(new BasicMessageItem(MessageType.Event, "The univariate distribution analysis for '" + Name + "' is complete.", this, ParentCollection.Name, Name, nameof(UnivariateAnalysis)));
            }
        }

        /// <inheritdoc/>
        public void CancelAnalysis()
        {
            _innerAnalysis.CancelAnalysis();
            SetIsValid();
        }

        /// <inheritdoc/>
        public UnivariateDistributionBase GetDistribution(int index)
        {
            return _innerAnalysis.GetDistribution(index);
        }

        /// <inheritdoc/>
        public UnivariateDistributionBase GetPointEstimateDistribution()
        {
            return _innerAnalysis.GetPointEstimateDistribution();
        }

        /// <inheritdoc/>
        public IUnivariateModel GetMarginalModel()
        {
            return UnivariateDistribution;
        }

        #endregion

        #region Plot Factory Methods

        /// <summary>
        /// Applies the default visual style to a plot (background, legend, padding).
        /// </summary>
        /// <param name="plot">The plot to style.</param>
        private static void ApplyDefaultPlotStyle(Plot plot)
        {
            plot.BorderThickness = new System.Windows.Thickness(0);
            plot.Background = System.Windows.Media.Brushes.Transparent;
            plot.LegendBackground = ColorFromHex("#8CFFFFFF");
            plot.LegendBorder = Colors.DarkGray;
            plot.LegendPosition = OxyPlot.Legends.LegendPosition.TopRight;
            plot.Padding = new System.Windows.Thickness(10, 10, 14, 10);
            plot.PlotAreaBackground = new System.Windows.Media.SolidColorBrush(Colors.White);
        }

        /// <summary>
        /// Creates the default frequency plot with logarithmic Y-axis and normal probability X-axis.
        /// </summary>
        /// <returns>A new frequency <see cref="Plot"/> with standard axis configuration.</returns>
        private static Plot CreateDefaultFrequencyPlot()
        {
            var plot = new Plot();
            ApplyDefaultPlotStyle(plot);
            plot.Title = "Frequency";
            plot.LegendPosition = OxyPlot.Legends.LegendPosition.TopLeft;
            plot.Axes.Add(new LogarithmicAxis
            {
                Key = "Yaxis",
                Position = OxyPlot.Axes.AxisPosition.Left,
                PowerPadding = true,
                Title = "",
                AxisTitleDistance = 20,
                TitleFontSize = 16,
                FontSize = 12,
                MajorGridlineStyle = OxyPlot.LineStyle.Solid,
                MinorGridlineStyle = OxyPlot.LineStyle.Dash,
                StringFormat = "N0"
            });
            plot.Axes.Add(new NormalProbabilityAxis
            {
                Key = "Xaxis",
                Title = "Exceedance Probability ",
                Unit = "P(X > x)",
                Position = OxyPlot.Axes.AxisPosition.Bottom,
                AxisTitleDistance = 20,
                TitleFontSize = 16,
                FontSize = 12,
            });
            return plot;
        }

        /// <summary>
        /// Creates the default chronology plot with linear axes for time series display.
        /// </summary>
        /// <returns>A new chronology <see cref="Plot"/> with standard axis configuration.</returns>
        private static Plot CreateDefaultChronologyPlot()
        {
            var plot = new Plot();
            ApplyDefaultPlotStyle(plot);
            plot.Title = "Chronology";
            plot.Axes.Add(new LinearAxis
            {
                Key = "Yaxis",
                Position = OxyPlot.Axes.AxisPosition.Left,
                Title = "",
                AxisTitleDistance = 20,
                TitleFontSize = 16,
                FontSize = 12,
                MajorGridlineStyle = OxyPlot.LineStyle.Solid,
                MinorGridlineStyle = OxyPlot.LineStyle.None,
                StringFormat = "N0"
            });
            plot.Axes.Add(new LinearAxis
            {
                Key = "Xaxis",
                Position = OxyPlot.Axes.AxisPosition.Bottom,
                Title = "",
                AxisTitleDistance = 15,
                TitleFontSize = 16,
                FontSize = 12,
                MajorGridlineStyle = OxyPlot.LineStyle.Solid,
                MinorGridlineStyle = OxyPlot.LineStyle.None,
            });
            return plot;
        }

        /// <summary>
        /// Converts a hexadecimal color string to a <see cref="Color"/>.
        /// </summary>
        /// <param name="hex">The hex color string (e.g., "#8CFFFFFF").</param>
        /// <returns>The parsed <see cref="Color"/>.</returns>
        private static Color ColorFromHex(string hex)
        {
            return (Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
        }

        /// <summary>
        /// Deserializes plot settings from a database column into an existing Plot object.
        /// Tries <see cref="PlotSerializer"/> first (modern format), falls back to
        /// <c>OxyPlotSettingsSerializer</c> for legacy project files.
        /// </summary>
        /// <param name="dtView">The data table view to read from.</param>
        /// <param name="rowIndex">The row index in the table.</param>
        /// <param name="columnName">The column name containing serialized plot XML.</param>
        /// <param name="plot">The target plot to apply settings to.</param>
        private static void DeserializePlotSettings(DataTableView dtView, int rowIndex, string columnName, Plot plot)
        {
            if (plot == null) return;
            if (!dtView.ColumnNames.Contains(columnName)) return;
            var xml = dtView.GetCell(columnName, rowIndex).ToString();
            if (string.IsNullOrEmpty(xml)) return;
            try
            {
                PlotSerializer.FromXElement(plot, XElement.Parse(xml));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Could not deserialize plot '{columnName}': {ex.Message}");
            }
        }

        #endregion

        #region Distribution Undo

        /// <summary>
        /// Records an undo action for a <see cref="UnivariateDistribution"/> property change using XElement snapshot comparison.
        /// Called from <see cref="InnerAnalysis_PropertyChanged"/> when the property name is in <see cref="DistributionUndoProperties"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Uses <see cref="XNode.DeepEquals"/> to deduplicate cascaded PropertyChanged events from a single user action.
        /// For example, changing DistributionType fires "DistributionType" then "SetDefaultParameters" etc. —
        /// only the first event that detects a diff records the action; subsequent events find no diff and are no-ops.
        /// </para>
        /// <para>
        /// Individual sub-item changes (e.g., <c>ModelParameter.PriorDistribution</c>) bubble up as
        /// PropertyChanged("Parameters") on the model. Since the list reference is unchanged,
        /// <see cref="UndoableStateBridge"/> cannot capture these — but the XElement snapshot does,
        /// because <see cref="UnivariateDistribution.ToXElement"/> serializes all parameter values and priors.
        /// </para>
        /// </remarks>
        /// <param name="propertyName">The specific property name that changed (e.g., "DistributionType", "IsNonstationary").</param>
        private void RecordDistributionUndo(string propertyName)
        {
            var undoManager = IsUndoEnabled ? UndoManager : null;
            if (undoManager == null || undoManager.IsExecutingAction) return;
            if (_distributionSnapshot == null) return;

            var currentSnapshot = _innerAnalysis.UnivariateDistribution.ToXElement();
            if (XNode.DeepEquals(_distributionSnapshot, currentSnapshot)) return;

            var oldSnapshot = _distributionSnapshot;
            var action = new DelegateAction(
                $"Change {propertyName}",
                () => RestoreDistributionFromSnapshot(currentSnapshot),
                () => RestoreDistributionFromSnapshot(oldSnapshot),
                this);
            undoManager.RecordAction(action);
            SetIsDirty(true);
            _distributionSnapshot = currentSnapshot;
        }

        /// <summary>
        /// Restores the <see cref="UnivariateDistribution"/> to the state captured in the XElement snapshot.
        /// Creates a new model-layer analysis with the restored distribution while preserving
        /// <see cref="BayesianAnalysis"/> settings and <see cref="ProbabilityOrdinates"/> via the
        /// analysis XElement round-trip.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The <see cref="UnivariateDistribution"/> deserialization constructor uses
        /// <c>_isDeserializing = true</c>, which suppresses <c>SetDefaultParameters()</c> and
        /// <c>SetDefaultQuantilePriors()</c>. The restored state is exactly what was captured —
        /// no destructive side effects.
        /// </para>
        /// <para>
        /// After replacing <c>_innerAnalysis</c>, all bridges are rebuilt via <see cref="SetupBridges"/>
        /// and all WPF bindings are notified via <see cref="ElementBase.RaisePropertyChange"/>.
        /// </para>
        /// </remarks>
        /// <param name="snapshot">The XElement snapshot to restore from.</param>
        private void RestoreDistributionFromSnapshot(XElement snapshot)
        {
            // 1. Disconnect from current inner analysis
            UnsubscribeInnerAnalysis();

            // 2. Preserve analysis config (BayesianAnalysis settings, ProbabilityOrdinates)
            var df = _innerAnalysis.UnivariateDistribution.DataFrame;
            var analysisXml = _innerAnalysis.ToXElement();

            // 3. Create new distribution from snapshot.
            //    The (DataFrame, XElement) constructor uses _isDeserializing = true,
            //    which suppresses SetDefaultParameters() and SetDefaultQuantilePriors().
            var newDist = new UnivariateDistribution(df, snapshot);

            // 4. Create new analysis with restored distribution + preserved config
            _innerAnalysis = new ModelAnalyses.UnivariateAnalysis(newDist, analysisXml);

            // 4b. Recalculate Bayesian simulation defaults for the restored distribution's parameter count.
            // The BayesianAnalysis(IModel, XElement) constructor sets Model first (calling SetDefaultSimulationOptions),
            // then deserializes XElement properties which OVERRIDE the defaults with stale pre-undo values.
            // We must re-apply defaults after construction to match the restored distribution.
            if (_innerAnalysis.BayesianAnalysis.UseSimulationDefaults)
                _innerAnalysis.BayesianAnalysis.SetDefaultSimulationOptions();
            if (_innerAnalysis.BayesianAnalysis.UseAdvancedSimulationDefaults)
                _innerAnalysis.BayesianAnalysis.SetDefaultAdvancedSimulationOptions();

            // 5. Reconnect subscriptions and rebuild all bridges
            SubscribeInnerAnalysis();
            SetupBridges();

            // 6. Notify WPF bindings that all model references have changed
            RaisePropertyChange(nameof(UnivariateDistribution));
            RaisePropertyChange(nameof(BayesianAnalysis));
            RaisePropertyChange(nameof(ProbabilityOrdinates));
            RaisePropertyChange(nameof(IsEstimated));
            RaisePropertyChange(nameof(AnalysisResults));
            RaisePropertyChange(nameof(ChronologyAnalysisResults));

            // 7. Revalidate and mark dirty
            SetIsValid();
            SetIsDirty(true);
        }

        #endregion

        #region Undo Bridge Management

        /// <summary>
        /// Sets up undo bridges for the inner analysis's observable collections, BayesianAnalysis
        /// scalar properties, distribution snapshot monitoring, and all plot objects.
        /// Disposes any existing bridges before creating new ones.
        /// </summary>
        private void SetupBridges()
        {
            DisposeBridges();

            // Subscribe to UndoManager.StateChanged to revalidate after undo/redo completes.
            // Collection bridge undo replays may not reliably fire CollectionChanged (e.g., when
            // ProbabilityOrdinates extends List<T> with 'new' keyword shadowing), so this ensures
            // SetIsValid() is always called after the undo stack changes.
            UndoManager.StateChanged += UndoManager_StateChanged;

            // Collection bridge for probability ordinates
            if (_innerAnalysis != null)
            {
                _probabilityOrdinatesBridge = new UndoableCollectionBridge<double>(
                    _innerAnalysis.ProbabilityOrdinates,
                    () => IsUndoEnabled ? UndoManager : null,
                    "probability ordinates",
                    this);
            }

            // Distribution undo — capture baseline snapshot for XElement comparison.
            // Recording is done in InnerAnalysis_PropertyChanged (not a separate subscription).
            _distributionSnapshot = _innerAnalysis?.UnivariateDistribution?.ToXElement();

            // Plot undo managers for element-level plots
            Func<IUndoManager> getUndo = () => IsUndoEnabled ? UndoManager : null;
            Action onRecorded = () => SetIsDirty(true);

            if (_frequencyPlot != null)
                _frequencyPlotUndo = new PlotUndoManager(_frequencyPlot, getUndo, "frequency plot", this, onRecorded);
            if (_chronologyPlot != null)
                _chronologyPlotUndo = new PlotUndoManager(_chronologyPlot, getUndo, "chronology plot", this, onRecorded);

            // Bayesian diagnostic plot bridges + BayesianAnalysis conditional undo recording
            _bayesianController?.SetupBridges(getUndo, this, onRecorded,
                _innerAnalysis?.BayesianAnalysis, () => SetIsValid());
        }

        /// <summary>
        /// Handles UndoManager.StateChanged to revalidate the element after undo/redo.
        /// </summary>
        private void UndoManager_StateChanged(object sender, EventArgs e)
        {
            SetIsValid();
        }

        /// <summary>
        /// Disposes all undo bridges and sets their references to null.
        /// </summary>
        private void DisposeBridges()
        {
            UndoManager.StateChanged -= UndoManager_StateChanged;

            _probabilityOrdinatesBridge?.Dispose();
            _probabilityOrdinatesBridge = null;

            _frequencyPlotUndo?.Dispose();
            _frequencyPlotUndo = null;
            _chronologyPlotUndo?.Dispose();
            _chronologyPlotUndo = null;

            _bayesianController?.DisposeBridges();
        }

        /// <summary>
        /// Suspends plot undo bridges and Bayesian analysis recording to prevent spurious undo entries
        /// during bulk data updates such as plot series population and axis title binding.
        /// </summary>
        /// <returns>An <see cref="IDisposable"/> that resumes recording when disposed.</returns>
        /// <remarks>
        /// Distribution snapshot recording (via <see cref="RecordDistributionUndo"/>) is NOT suspended here.
        /// It flows through <see cref="InnerAnalysis_PropertyChanged"/> and must not be disrupted by plot updates.
        /// </remarks>
        public IDisposable SuspendPlotBridges()
        {
            var suspensions = new List<IDisposable>();

            // Suspend element-level plot bridges
            if (_frequencyPlotUndo != null) suspensions.Add(_frequencyPlotUndo.SuspendRecording());
            if (_chronologyPlotUndo != null) suspensions.Add(_chronologyPlotUndo.SuspendRecording());

            // Suspend Bayesian plot bridges + BA property recording (handled internally)
            if (_bayesianController != null) suspensions.Add(_bayesianController.SuspendPlotBridges());

            return new AggregateDisposable(suspensions);
        }

        /// <summary>
        /// Rebuilds the series and annotation bridges for the specified plot after series/annotation changes.
        /// Call this AFTER adding series/annotations to the plot and OUTSIDE <see cref="SuspendPlotBridges"/>.
        /// </summary>
        /// <param name="plot">The plot whose bridges should be rebuilt.</param>
        public void RebuildSeriesAndAnnotationBridges(Plot plot)
        {
            if (plot == _frequencyPlot) _frequencyPlotUndo?.RebuildSeriesAndAnnotationBridges();
            else if (plot == _chronologyPlot) _chronologyPlotUndo?.RebuildSeriesAndAnnotationBridges();
            else _bayesianController?.RebuildSeriesAndAnnotationBridges(plot);
        }


        #endregion

    }
}
