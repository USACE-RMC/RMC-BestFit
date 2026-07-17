using DatabaseManager;
using FrameworkInterfaces;
using FrameworkInterfaces.Messaging;
using FrameworkInterfaces.Undo;
using FrameworkInterfaces.Undo.Actions;
using Numerics;
using Numerics.Data;
using Numerics.Distributions;
using Numerics.Sampling.MCMC;
using Numerics.Utilities;
using OxyPlot.Wpf;
using OxyPlot.Wpf.Serialization;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
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
    /// UI wrapper for the model-layer <see cref="ModelAnalyses.MixtureAnalysis"/>. Estimates a
    /// finite-mixture flood-frequency distribution from up to 3 user-selected component
    /// distribution types, fitting both component parameters and mixture weights via Bayesian
    /// MCMC. Surfaces the component <see cref="Distributions"/> as an observable collection
    /// for GUI binding.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    /// This class wraps a <see cref="ModelAnalyses.MixtureAnalysis"/> from the model library,
    /// delegating all computation to the inner analysis while providing UI-specific concerns:
    /// input data management, SQLite persistence, messenger-based validation, plot settings,
    /// and the <see cref="Distributions"/> observable collection for GUI binding.
    /// </para>
    /// </remarks>
    [Category("General")]
    [DisplayName("Mixture Distribution Analysis")]
    [Description("Estimates a mixture distribution from a collection of univariate distributions using Bayesian MCMC to determine mixture weights and distribution parameters.")]
    [Browsable(true)]
    public class MixtureAnalysis : ElementBase, IUnivariate
    {

        #region Construction

        /// <summary>
        /// Constructs a new mixture distribution analysis.
        /// </summary>
        /// <param name="name">The name of the element.</param>
        /// <param name="parentCollection">The parent collection name.</param>
        /// <param name="openFromFile">Optional parameter to open the function from disk upon construction.</param>
        public MixtureAnalysis(string name, IElementCollection parentCollection, bool openFromFile = false) : base(name, parentCollection)
        {
            IsUndoEnabled = false;
            try
            {
                _nameOnDisk = name;
                _creationDate = DateTime.Now;
                _lastModified = DateTime.Now;

                // Create plot objects
                _frequencyPlot = CreateDefaultFrequencyPlot();
                _bayesianController = new BayesianController();

                // Create the inner model analysis
                var model = new MixtureModel();
                _innerAnalysis = new ModelAnalyses.MixtureAnalysis(model);

                // Set up the UI distributions collection (max 3)
                // Populate before assigning so the setter's SetDefaultMixture call receives a non-empty list
                var dists = new ObservableCollection<UnivariateDistributionType>();
                dists.Add(UnivariateDistributionType.Normal);
                dists.Add(UnivariateDistributionType.Normal);
                Distributions = dists;

                // Subscribe to inner analysis property changes and ProbabilityOrdinates collection changes
                SubscribeInnerAnalysis();

                // Add UI-only messages
                _descriptionMsg = new BasicMessageItem(MessageType.Message, "The mixture distribution analysis does not have a description.", this, ParentCollection.Name, Name, nameof(Description), "MDA-MSG-001");
                _inputDataNullMsg = new BasicMessageItem(MessageType.Error, "Input data is missing. Please select valid input data.", this, ParentCollection.Name, Name, nameof(InputData), "MDA-ERR-005");
                _inputDataInValidMsg = new BasicMessageItem(MessageType.Error, "The selected input data is invalid.", this, ParentCollection.Name, Name, nameof(InputData), "MDA-ERR-006");

                // ProbabilityOrdinates validation messages are surfaced by the model-layer
                // ProbabilityOrdinates.Validate() routed through _validationAdapter â€” defining
                // duplicates here would produce two messages per error.
                _messages = new List<BasicMessageItem>()
                {   _descriptionMsg,
                    _inputDataNullMsg,
                    _inputDataInValidMsg,
                };

                // Create messenger and validation adapter
                _messenger = Messenger.GetInstance();
                _validationAdapter = new ValidationMessageAdapter(_messenger, this, ParentCollection.Name, "MDA");
                _messenger.Add(_descriptionMsg);
                _messenger.Add(_inputDataNullMsg);

                if (openFromFile == true) Open();

                _nameValid = ValidateName(BestFitProject.InvalidNameCharacters, 50, "MDA");
                SetIsValid();
                // Not a v1 feature â€” no legacy migration path, so no openedFromV1 flag.
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
        /// Gets and sets the element name.
        /// </summary>
        [Category("Meta Data")]
        [DisplayName("Name")]
        [Description("Unique label identifying this mixture analysis; max 50 characters.")]
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
                    _nameValid = ValidateName(BestFitProject.InvalidNameCharacters, 50, "MDA");
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
        [Description("Free-text annotation describing this mixture analysis.")]
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
        public override System.Windows.Media.ImageSource ElementImage =>
            System.Windows.Application.Current?.TryFindResource("MixtureAnalysisIcon") as System.Windows.Media.ImageSource;

        /// <summary>
        /// Gets the resource key for the element icon, enabling dynamic theme updates.
        /// The project explorer uses <see cref="System.Windows.FrameworkElement.SetResourceReference"/>
        /// with this key so the icon updates automatically when the theme changes.
        /// </summary>
        public string ElementImageResourceKey => "MixtureAnalysisIcon";

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
        private ModelAnalyses.MixtureAnalysis _innerAnalysis;

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
        /// Gets the collection name for storing mixture analyses in the database.
        /// </summary>
        public static string CollectionName => "<Mixture Distribution>";

        /// <summary>
        /// The input data for the analysis.
        /// </summary>
        private InputData _inputData;

        /// <summary>
        /// Collection of distribution types that compose the mixture distribution.
        /// Maximum of 3 component distributions allowed.
        /// </summary>
        private ObservableCollection<UnivariateDistributionType> _distributions;

        /// <summary>
        /// The frequency plot showing the fitted mixture distribution and data.
        /// </summary>
        private Plot _frequencyPlot;

        /// <summary>
        /// Undo manager for the frequency plot.
        /// </summary>
        private PlotUndoManager _frequencyPlotUndo;

        /// <summary>
        /// Manages the 7 standard Bayesian MCMC diagnostic plots.
        /// </summary>
        private BayesianController _bayesianController;

        /// <summary>
        /// Undo bridge for the probability ordinates collection.
        /// </summary>
        private UndoableCollectionBridge<double> _probabilityOrdinatesBridge;

        /// <summary>
        /// Undo bridge for the Distributions ObservableCollection (Add/Remove/Replace).
        /// </summary>
        private UndoableCollectionBridge<UnivariateDistributionType> _distributionsBridge;

        /// <summary>
        /// Rolling XElement baseline of the <see cref="MixtureModel"/> state.
        /// Captured after every recorded undo action and after initialization/deserialization.
        /// Used by <see cref="RecordModelUndo"/> to detect changes and record undo entries.
        /// </summary>
        private XElement _modelSnapshot;

        /// <summary>
        /// Property names from the <see cref="MixtureModel"/> that trigger XElement snapshot
        /// comparison for undo recording. These bubble up through InnerAnalysis_PropertyChanged.
        /// </summary>
        private static readonly HashSet<string> ModelUndoProperties = new()
        {
            "Parameters", "UseJeffreysRuleForScale", "IsZeroInflated", "Mixture",
            "EnableQuantilePriors", "QuantilePriors", "UseDefaultFlatPriors"
        };

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the input data used for evaluating the likelihood function.
        /// </summary>
        [Category("General")]
        [DisplayName("Input Data")]
        [Description("Input Data element shared by all mixture components in the joint likelihood.")]
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

                // Update the inner analysis model's DataFrame
                if (_innerAnalysis.MixtureDistribution != null)
                {
                    if (InputData != null && InputData.DataFrame != null)
                        _innerAnalysis.MixtureDistribution.DataFrame = InputData.DataFrame;
                    else
                        _innerAnalysis.MixtureDistribution.DataFrame = null;
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
        /// Delegates to the inner analysis's <see cref="Numerics.Data.ProbabilityOrdinates"/> collection.
        /// </summary>
        public ProbabilityOrdinates ProbabilityOrdinates => _innerAnalysis?.ProbabilityOrdinates;

        /// <summary>
        /// Gets and sets the collection of distribution types for the mixture distribution.
        /// Maximum of 3 component distributions allowed.
        /// </summary>
        [Category("Inputs")]
        [DisplayName("Univariate Distributions")]
        [Description("Distribution types that define the mixture distribution (max 3).")]
        [Browsable(true)]
        public ObservableCollection<UnivariateDistributionType> Distributions
        {
            get { return _distributions; }
            set
            {
                if (_distributions != null)
                    _distributions.CollectionChanged -= Distributions_CollectionChanged;

                _distributions = value;

                if (_distributions != null)
                    _distributions.CollectionChanged += Distributions_CollectionChanged;

                if (_innerAnalysis != null && _innerAnalysis.MixtureDistribution != null)
                {
                    _innerAnalysis.MixtureDistribution.SetDefaultMixture(_distributions.ToList());
                }

                RaisePropertyChange(nameof(Distributions));
            }
        }

        /// <summary>
        /// The mixture distribution model. Delegates to the inner analysis.
        /// </summary>
        public MixtureModel MixtureDistribution
        {
            get { return _innerAnalysis.MixtureDistribution; }
        }

        /// <summary>
        /// The Bayesian Analysis object. Delegates to the inner analysis.
        /// </summary>
        public BayesianAnalysis BayesianAnalysis
        {
            get { return _innerAnalysis.BayesianAnalysis; }
        }

        /// <summary>
        /// Determines whether the analysis has been estimated.
        /// </summary>
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
        /// Gets the frequency plot showing the fitted mixture distribution and data.
        /// </summary>
        public Plot FrequencyPlot => _frequencyPlot;

        /// <summary>
        /// Gets the Bayesian MCMC diagnostic plot settings (7 shared diagnostic plots).
        /// </summary>
        public BayesianController BayesianPlots => _bayesianController;

        #endregion

        #endregion

        #region Methods

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

            // Check if we need to clear results. Guard against undo replay so that undoing
            // a metadata-only change (e.g., UnitLabel rename) does not silently wipe fit results.
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
                if (!UndoManager.IsExecutingAction) ClearResults();
            }

            RaisePropertyChange(nameof(InputData));
        }

        /// <summary>
        /// Handles deletion of the <see cref="InputData"/> element from its parent collection.
        /// Clears the reference with undo recording disabled so that pressing Undo cannot
        /// resurrect the now-deleted element. Pre-existing undo history on this analysis is
        /// preserved.
        /// </summary>
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
            // Check probability ordinates
            // Validate ordinates locally only for the IsValid flag â€” diagnostic messages are
            // surfaced by the model-layer ProbabilityOrdinates.Validate() routed through
            // _validationAdapter inside SetIsValid().
            _ordinatesValid = true;
            if (ProbabilityOrdinates.Count == 0)
            {
                _ordinatesValid = false;
            }
            else
            {
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
            }
            SetIsValid();
            // Invalidation (if any) is owned by the model-layer handler now. We only
            // validate + message here. See RMC.BestFit.Analyses.MixtureAnalysis
            // .ProbabilityOrdinates_CollectionChanged for the reprocess/clear logic.
            RaisePropertyChange(nameof(ProbabilityOrdinates));
        }

        /// <summary>
        /// Subscribes to inner analysis events (PropertyChanged and ProbabilityOrdinates.CollectionChanged).
        /// </summary>
        private void SubscribeInnerAnalysis()
        {
            if (_innerAnalysis == null) return;
            _innerAnalysis.PropertyChanged += InnerAnalysis_PropertyChanged;
            _innerAnalysis.ProbabilityOrdinates.CollectionChanged += ProbabilityOrdinates_CollectionChanged;
        }

        /// <summary>
        /// Unsubscribes from inner analysis events (PropertyChanged and ProbabilityOrdinates.CollectionChanged).
        /// </summary>
        private void UnsubscribeInnerAnalysis()
        {
            if (_innerAnalysis == null) return;
            _innerAnalysis.PropertyChanged -= InnerAnalysis_PropertyChanged;
            _innerAnalysis.ProbabilityOrdinates.CollectionChanged -= ProbabilityOrdinates_CollectionChanged;
        }

        /// <summary>
        /// Handles changes to the distributions collection by attaching/detaching event handlers and updating the mixture model.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">The collection change event arguments.</param>
        private void Distributions_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // Note: Max-3 enforcement is handled by the App's PreviewAddRows handler
            // to avoid re-entrancy (cannot modify ObservableCollection during CollectionChanged).

            // Update the inner analysis's mixture distribution
            if (_distributions.Count > 0)
                _innerAnalysis.MixtureDistribution.SetDefaultMixture(_distributions.ToList());
            SetIsValid();
            if (!UndoManager.IsExecutingAction) ClearResults();
            RaisePropertyChange(nameof(Distributions));
        }

        /// <summary>
        /// Forwards property change notifications from the inner analysis to the UI framework.
        /// </summary>
        /// <param name="sender">The inner analysis object.</param>
        /// <param name="e">The property change event arguments.</param>
        private void InnerAnalysis_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Record undo for MixtureModel property changes via XElement snapshot
            if (ModelUndoProperties.Contains(e.PropertyName))
            {
                RecordModelUndo(e.PropertyName);
            }

            // Forward relevant property changes to the WPF framework
            if (e.PropertyName == nameof(ModelAnalyses.MixtureAnalysis.IsEstimated) ||
                e.PropertyName == nameof(ModelAnalyses.MixtureAnalysis.AnalysisResults) ||
                e.PropertyName == nameof(ModelAnalyses.MixtureAnalysis.MixtureDistribution) ||
                e.PropertyName == nameof(ModelAnalyses.MixtureAnalysis.BayesianAnalysis))
            {
                SetIsValid();
                RaisePropertyChange(e.PropertyName);
            }
            else
            {
                RaisePropertyChange(e.PropertyName);
            }
        }

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
            { nameof(Distributions), typeof(string) },
            { nameof(MixtureDistribution), typeof(string) },
            { nameof(BayesianAnalysis), typeof(string) },
            { nameof(MCMCResults), typeof(byte[]) },
            { nameof(AnalysisResults), typeof(string) },
            { nameof(ProbabilityOrdinates), typeof(string) },
            { "FrequencyPlotSettings", typeof(string) },
            { "KernelDensityPlotSettings", typeof(string) },
            { "HistogramPlotSettings", typeof(string) },
            { "BivariatePlotSettings", typeof(string) },
            { "MeanLikelihoodPlotSettings", typeof(string) },
            { "AutocorrelationPlotSettings", typeof(string) },
            { "MarkovChainTracesPlotSettings", typeof(string) },
            { "InfluenceDiagnosticsPlotSettings", typeof(string) },
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
            var wasUndoEnabled = IsUndoEnabled;
            IsUndoEnabled = false;
            try
            {
            _messenger.Clear(this);
            _validationAdapter.ClearAll();
            var wasOpen = sqlite.DataBaseOpen;
            if (wasOpen == false) sqlite.Open();

            // open element
            var dtView = sqlite.GetTableManager(CollectionName);
            int rowIndex = dtView.SearchColumn(0, dtView.NumberOfRows - 1, "Name", NameOnDisk, true, true);
            if (rowIndex != -1)
            {
                if (dtView.ColumnNames.Contains(nameof(Name)))
                {
                    _name = dtView.GetCell(nameof(Name), rowIndex).ToString();
                    foreach (var item in _messages) item.SourceName = _name;
                    _nameValid = ValidateName(BestFitProject.InvalidNameCharacters, 50, "MA");
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

                // Plot Properties
                DeserializePlotSettings(dtView, rowIndex, "FrequencyPlotSettings", _frequencyPlot);
                _bayesianController.Deserialize(dtView, rowIndex);

                // Get probability ordinates (save to local â€” inner analysis gets reconstructed below)
                string probOrdinatesStr = null;
                if (dtView.ColumnNames.Contains(nameof(ProbabilityOrdinates)))
                    probOrdinatesStr = dtView.GetCell(nameof(ProbabilityOrdinates), rowIndex).ToString();

                // Get distributions. Suppress CollectionChanged during the bulk load — each
                // Add otherwise fires SetDefaultMixture (which rebuilds the inner mixture
                // distribution) and ClearResults (a no-op during Open since IsUndoEnabled=false,
                // but still emits PropertyChanged). For a 3-component mixture the inner analysis
                // would be replaced 4 times before XElement-based reconstruction at line 855
                // overwrites it.
                if (dtView.ColumnNames.Contains(nameof(Distributions)))
                {
                    _distributions.CollectionChanged -= Distributions_CollectionChanged;
                    try
                    {
                        Distributions.Clear();
                        var types = dtView.GetCell(nameof(Distributions), rowIndex).ToString().Split('|');
                        for (int i = 0; i < types.Length; i++)
                        {
                            if (Enum.TryParse(types[i], out UnivariateDistributionType type))
                                Distributions.Add(type);
                        }
                    }
                    finally
                    {
                        _distributions.CollectionChanged += Distributions_CollectionChanged;
                    }
                }

                // Get model and reconstruct the inner analysis
                if (dtView.ColumnNames.Contains(nameof(MixtureDistribution)) && InputData != null && InputData.DataFrame != null)
                {
                    var modelXElement = XElement.Parse(dtView.GetCell(nameof(MixtureDistribution), rowIndex).ToString());
                    var model = new MixtureModel(InputData.DataFrame, modelXElement);

                    MCMCResults mcmcResults = AnalysisPersistenceHelper.TryLoadMCMCResults(dtView, rowIndex, Name);
                    XElement innerXElement = AnalysisPersistenceHelper.TryLoadXElement(dtView, "AnalysisXml", rowIndex, Name);

                    // Get Bayesian analysis XElement
                    XElement analysisXElement = null;
                    if (dtView.ColumnNames.Contains(nameof(BayesianAnalysis)))
                    {
                        var xElementString = dtView.GetCell(nameof(BayesianAnalysis), rowIndex).ToString();
                        analysisXElement = XElement.Parse(xElementString);
                    }

                    UncertaintyAnalysisResults analysisResults =
                        AnalysisPersistenceHelper.TryLoadAnalysisResults(dtView, nameof(AnalysisResults), rowIndex, Name);

                    // Reconstruct inner analysis from persisted data
                    UnsubscribeInnerAnalysis();

                    if (innerXElement == null && analysisXElement != null)
                    {
                        // Build a combined XElement for the inner analysis constructor
                        innerXElement = new XElement("MixtureAnalysis",
                            new XAttribute("IsEstimated", mcmcResults != null));

                        // Add probability ordinates
                        if (!string.IsNullOrEmpty(probOrdinatesStr))
                            innerXElement.Add(new XElement("ProbabilityOrdinates", probOrdinatesStr));

                        // Add Bayesian analysis element
                        innerXElement.Add(analysisXElement);

                    }

                    if (innerXElement != null)
                    {
                        _innerAnalysis = new ModelAnalyses.MixtureAnalysis(model, innerXElement, mcmcResults, analysisResults);
                    }
                    else
                    {
                        _innerAnalysis = new ModelAnalyses.MixtureAnalysis(model);

                        // Load probability ordinates into the new inner analysis
                        if (!string.IsNullOrEmpty(probOrdinatesStr))
                            _innerAnalysis.ProbabilityOrdinates.FromDelimitedString(probOrdinatesStr, "|");
                    }

                    SubscribeInnerAnalysis();
                }
            }

            if (wasOpen == false) sqlite.Close();
            SetupBridges();

            // Validate probability ordinates â€” ProbabilityOrdinates_CollectionChanged didn't fire
            // during Open because SubscribeInnerAnalysis hadn't been called when the ordinates were loaded.
            // Diagnostic messages are surfaced by the model-layer Validate() routed through
            // _validationAdapter inside SetIsValid(); we only set the local flag here.
            _ordinatesValid = true;
            if (ProbabilityOrdinates.Count == 0)
            {
                _ordinatesValid = false;
            }
            else
            {
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
            }

            SetIsValid();
            SetIsDirty(false);
            // Notify project-tree node header binding that Name was restored from disk via backing field.
            RaisePropertyChange(nameof(Name), setDirty: false);
            }
            finally
            {
                IsUndoEnabled = wasUndoEnabled;
                if (wasUndoEnabled) ClearUndoHistory();
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

            // Update parent collection table
            var dtView = sqlite.GetTableManager(ParentCollection.Name);
            int rowIndex = dtView.SearchColumn(0, dtView.NumberOfRows - 1, "Name", NameOnDisk, true, true);  // B-013: key by Name for parent-table consistency
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
            dtView.EditCell(rowIndex, nameof(Distributions), String.Join("|", Distributions));

            // Serialize from inner analysis
            dtView.EditCell(rowIndex, nameof(MixtureDistribution), _innerAnalysis.MixtureDistribution.ToXElement().ToString());
            dtView.EditCell(rowIndex, nameof(BayesianAnalysis), _innerAnalysis.BayesianAnalysis.ToXElement().ToString());

            dtView.EditCell(rowIndex, nameof(MCMCResults),
                AnalysisPersistenceHelper.SerializeMCMCResults(_innerAnalysis.BayesianAnalysis.Results));
            dtView.EditCell(rowIndex, nameof(AnalysisResults),
                AnalysisPersistenceHelper.SerializeAnalysisResults(_innerAnalysis.AnalysisResults));

            dtView.EditCell(rowIndex, nameof(ProbabilityOrdinates), ProbabilityOrdinates.ToDelimitedString("|"));
            dtView.EditCell(rowIndex, "AnalysisXml", _innerAnalysis?.ToXElement()?.ToString() ?? "");
            dtView.EditCell(rowIndex, "FrequencyPlotSettings", _frequencyPlot != null ? PlotSerializer.ToXElement(_frequencyPlot).ToString() : "");
            _bayesianController.Serialize(dtView, rowIndex);

            dtView.ApplyEdits();
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
            var element = new MixtureAnalysis(newName == "" || string.IsNullOrEmpty(newName) ? Name : newName, ParentCollection);

            element.IsUndoEnabled = false;
            try
            {
                element.Description = Description;

                // Reconstruct inner analysis with cloned mixture model
                element.UnsubscribeInnerAnalysis();
                var clonedModel = (MixtureModel)MixtureDistribution.Clone();
                element._innerAnalysis = new ModelAnalyses.MixtureAnalysis(clonedModel);
                element.SubscribeInnerAnalysis();

                // Copy Bayesian analysis settings
                element._innerAnalysis.BayesianAnalysis.UseSimulationDefaults = BayesianAnalysis.UseSimulationDefaults;
                element._innerAnalysis.BayesianAnalysis.NumberOfChains = BayesianAnalysis.NumberOfChains;
                element._innerAnalysis.BayesianAnalysis.WarmupIterations = BayesianAnalysis.WarmupIterations;
                element._innerAnalysis.BayesianAnalysis.Iterations = BayesianAnalysis.Iterations;
                element._innerAnalysis.BayesianAnalysis.ThinningInterval = BayesianAnalysis.ThinningInterval;
                element._innerAnalysis.BayesianAnalysis.UseAdvancedSimulationDefaults = BayesianAnalysis.UseAdvancedSimulationDefaults;
                element._innerAnalysis.BayesianAnalysis.PRNGSeed = BayesianAnalysis.PRNGSeed;
                element._innerAnalysis.BayesianAnalysis.InitialIterations = BayesianAnalysis.InitialIterations;
                element._innerAnalysis.BayesianAnalysis.Jump = BayesianAnalysis.Jump;
                element._innerAnalysis.BayesianAnalysis.JumpThreshold = BayesianAnalysis.JumpThreshold;
                element._innerAnalysis.BayesianAnalysis.SnookerThreshold = BayesianAnalysis.SnookerThreshold;
                element._innerAnalysis.BayesianAnalysis.Noise = BayesianAnalysis.Noise;
                element._innerAnalysis.BayesianAnalysis.CredibleIntervalWidth = BayesianAnalysis.CredibleIntervalWidth;
                element._innerAnalysis.BayesianAnalysis.OutputLength = BayesianAnalysis.OutputLength;
                element._innerAnalysis.BayesianAnalysis.PointEstimator = BayesianAnalysis.PointEstimator;

                // Set input data
                element.InputData = InputData;

                // Set distributions
                element.Distributions.Clear();
                foreach (var distType in Distributions)
                {
                    element.Distributions.Add(distType);
                }

                // Copy probability ordinates
                if (ProbabilityOrdinates != null && ProbabilityOrdinates.Count > 0)
                    element._innerAnalysis.ProbabilityOrdinates.FromDelimitedString(
                        ProbabilityOrdinates.ToDelimitedString(ProbabilityOrdinates.DefaultDelimiter),
                        ProbabilityOrdinates.DefaultDelimiter);

                // Copy plot settings (inside undo suppression â€” matches FittingAnalysis.Copy template)
                if (_frequencyPlot != null) PlotSerializer.FromXElement(element._frequencyPlot, PlotSerializer.ToXElement(_frequencyPlot));
                _bayesianController.CopyTo(element._bayesianController);

                // Clear results
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
            var element = new MixtureAnalysis(itemName, ParentCollection);
            element.Open(sqlite);
            return element;
        }

        /// <summary>
        /// Deletes the element from disk and from the parent element collection.
        /// </summary>
        public override void Delete()
        {
            if (Name == null) return;
            // Unhook upstream Deleted subscription directly (do not route through the
            // InputData setter â€” that would re-add _inputDataNullMsg and re-flip IsDirty=true).
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
            // Skip model validation when InputData is invalid â€” the UI layer already reports that
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

        /// <summary>
        /// Clears all analysis results by delegating to the inner analysis.
        /// </summary>
        public void ClearResults()
        {
            _innerAnalysis.ClearResults();
            RaisePropertyChange(nameof(AnalysisResults));
        }

        /// <summary>
        /// Runs the mixture distribution analysis asynchronously by delegating to the inner analysis.
        /// </summary>
        /// <param name="progressReporter">Progress reporter for tracking analysis progress.</param>
        public async Task RunAsync(SafeProgressReporter progressReporter)
        {
            SetIsValid();
            if (IsValid == false) return;

            _messenger.Add(new BasicMessageItem(MessageType.Event, "The mixture distribution analysis for '" + Name + "' has started.", this, ParentCollection.Name, Name, nameof(MixtureAnalysis)));

            try
            {
                // Delegate to inner analysis RunAsync
                progressReporter.IndicateTaskStart();
                await _innerAnalysis.RunAsync(progressReporter);
            }
            catch (OperationCanceledException)
            {
                ClearResults();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MixtureAnalysis.RunAsync failed for '{Name}': {ex}");
                ClearResults();
                _messenger.Add(new BasicMessageItem(MessageType.Error, $"The mixture distribution analysis for '{Name}' failed: {ex.Message}", this, ParentCollection.Name, Name, nameof(MixtureAnalysis)));
            }
            finally
            {
                progressReporter.IndicateTaskEnded();
                SetIsValid();
                _messenger.Add(new BasicMessageItem(MessageType.Event, "The mixture distribution analysis for '" + Name + "' is complete.", this, ParentCollection.Name, Name, nameof(MixtureAnalysis)));
            }
        }

        /// <summary>
        /// Cancels the running analysis by delegating to the inner analysis.
        /// </summary>
        public void CancelAnalysis()
        {
            _innerAnalysis.CancelAnalysis();
            SetIsValid();
        }

        /// <summary>
        /// Gets a posterior mixture distribution for a given output index from the MCMC results.
        /// Delegates to the inner analysis.
        /// </summary>
        /// <param name="index">The output index.</param>
        /// <returns>The mixture distribution with parameters from the specified MCMC output index, or null if not estimated.</returns>
        public UnivariateDistributionBase GetDistribution(int index)
        {
            return _innerAnalysis.GetDistribution(index);
        }

        /// <summary>
        /// Gets the point estimate distribution based on the selected point estimator.
        /// Delegates to the inner analysis.
        /// </summary>
        /// <returns>The point estimate mixture distribution, or null if not estimated.</returns>
        public UnivariateDistributionBase GetPointEstimateDistribution()
        {
            return _innerAnalysis.GetPointEstimateDistribution();
        }

        /// <inheritdoc/>
        public IUnivariateModel GetMarginalModel()
        {
            return MixtureDistribution;
        }

        #endregion

        #region Model Undo

        /// <summary>
        /// Records an undo action for a <see cref="MixtureModel"/> property change using XElement snapshot comparison.
        /// Called from <see cref="InnerAnalysis_PropertyChanged"/> when the property name is in <see cref="ModelUndoProperties"/>.
        /// </summary>
        /// <param name="propertyName">The property name that changed.</param>
        private void RecordModelUndo(string propertyName)
        {
            var undoManager = IsUndoEnabled ? UndoManager : null;
            if (undoManager == null || undoManager.IsExecutingAction) return;
            if (_modelSnapshot == null) return;

            var currentSnapshot = _innerAnalysis.MixtureDistribution.ToXElement();
            if (XNode.DeepEquals(_modelSnapshot, currentSnapshot)) return;

            var oldSnapshot = _modelSnapshot;
            var action = new DelegateAction(
                $"Change {propertyName}",
                () => RestoreModelFromSnapshot(currentSnapshot),
                () => RestoreModelFromSnapshot(oldSnapshot),
                this);
            undoManager.RecordAction(action);
            SetIsDirty(true);
            _modelSnapshot = currentSnapshot;
        }

        /// <summary>
        /// Restores the <see cref="MixtureModel"/> to the state captured in the XElement snapshot.
        /// Creates a new model-layer analysis with the restored model while preserving
        /// <see cref="BayesianAnalysis"/> settings and <see cref="ProbabilityOrdinates"/>.
        /// </summary>
        /// <param name="snapshot">The XElement snapshot to restore from.</param>
        private void RestoreModelFromSnapshot(XElement snapshot)
        {
            // 1. Disconnect from current inner analysis
            UnsubscribeInnerAnalysis();

            // 2. Preserve analysis config (BayesianAnalysis settings, ProbabilityOrdinates)
            var df = _innerAnalysis.MixtureDistribution.DataFrame;
            var analysisXml = _innerAnalysis.ToXElement();

            // 3. Create new model from snapshot
            var newModel = new MixtureModel(df, snapshot);

            // 4. Create new analysis with restored model + preserved config
            _innerAnalysis = new ModelAnalyses.MixtureAnalysis(newModel, analysisXml);

            // 4b. Recalculate Bayesian simulation defaults for the restored model's parameter count
            if (_innerAnalysis.BayesianAnalysis.UseSimulationDefaults)
                _innerAnalysis.BayesianAnalysis.SetDefaultSimulationOptions();
            if (_innerAnalysis.BayesianAnalysis.UseAdvancedSimulationDefaults)
                _innerAnalysis.BayesianAnalysis.SetDefaultAdvancedSimulationOptions();

            // 5. Rebuild the Distributions collection from the restored model's component types
            var restoredTypes = newModel.Mixture.Distributions.Select(d => d.Type).ToList();
            _distributions.CollectionChanged -= Distributions_CollectionChanged;
            _distributions.Clear();
            foreach (var t in restoredTypes)
                _distributions.Add(t);
            _distributions.CollectionChanged += Distributions_CollectionChanged;

            // 6. Reconnect subscriptions and rebuild all bridges
            SubscribeInnerAnalysis();
            SetupBridges();

            // 7. Notify WPF bindings that all model references have changed
            RaisePropertyChange(nameof(MixtureDistribution));
            RaisePropertyChange(nameof(BayesianAnalysis));
            RaisePropertyChange(nameof(ProbabilityOrdinates));
            RaisePropertyChange(nameof(IsEstimated));
            RaisePropertyChange(nameof(AnalysisResults));
            RaisePropertyChange(nameof(Distributions));

            // 8. Revalidate and mark dirty
            SetIsValid();
            SetIsDirty(true);
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
            plot.Background = Brushes.Transparent;
            plot.LegendBackground = ColorFromHex("#8CFFFFFF");
            plot.LegendBorder = Colors.DarkGray;
            plot.LegendPosition = OxyPlot.Legends.LegendPosition.TopRight;
            plot.Padding = new System.Windows.Thickness(10, 10, 14, 10);
            plot.PlotAreaBackground = new SolidColorBrush(Colors.White);
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
            plot.Axes.Add(new OxyPlot.Wpf.LogarithmicAxis
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
            plot.Axes.Add(new OxyPlot.Wpf.NormalProbabilityAxis
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
        /// Converts a hexadecimal color string to a <see cref="Color"/>.
        /// </summary>
        /// <param name="hex">The hex color string (e.g., "#8CFFFFFF").</param>
        /// <returns>The parsed <see cref="Color"/>.</returns>
        private static Color ColorFromHex(string hex)
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }

        /// <summary>
        /// Deserializes plot settings from a database column into an existing Plot object.
        /// Tries <see cref="PlotSerializer"/> first (modern format), falls back to
        /// <c>OxyPlotSettingsSerializer</c> for legacy project files.
        /// </summary>
        /// <param name="dtView">The database table view.</param>
        /// <param name="rowIndex">The row index.</param>
        /// <param name="columnName">The database column name.</param>
        /// <param name="plot">The target plot to apply settings to.</param>
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
                System.Diagnostics.Debug.WriteLine($"Could not deserialize plot settings '{columnName}': {ex.Message}");
            }
        }

        #endregion

        #region Undo Bridge Management

        /// <summary>
        /// Sets up undo bridges for observable collections, plot objects, and BayesianAnalysis settings.
        /// Disposes any existing bridges before creating new ones.
        /// </summary>
        private void SetupBridges()
        {
            DisposeBridges();

            // Subscribe to UndoManager.StateChanged to revalidate after undo/redo completes.
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

            // Collection bridge for the user-editable Distributions list (mixture components)
            if (_distributions != null)
            {
                _distributionsBridge = new UndoableCollectionBridge<UnivariateDistributionType>(
                    _distributions,
                    () => IsUndoEnabled ? UndoManager : null,
                    "mixture distributions",
                    this);
            }

            // Model undo â€” capture baseline snapshot for XElement comparison
            _modelSnapshot = _innerAnalysis?.MixtureDistribution?.ToXElement();

            // Plot undo managers for element-level plots
            Func<IUndoManager> getUndo = () => IsUndoEnabled ? UndoManager : null;
            Action onRecorded = () => SetIsDirty(true);

            if (_frequencyPlot != null)
                _frequencyPlotUndo = new PlotUndoManager(_frequencyPlot, getUndo, "frequency plot", this, onRecorded);

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
        /// Disposes all undo bridges and sets them to null.
        /// </summary>
        private void DisposeBridges()
        {
            UndoManager.StateChanged -= UndoManager_StateChanged;

            _probabilityOrdinatesBridge?.Dispose();
            _probabilityOrdinatesBridge = null;

            _distributionsBridge?.Dispose();
            _distributionsBridge = null;

            _frequencyPlotUndo?.Dispose();
            _frequencyPlotUndo = null;

            _bayesianController?.DisposeBridges();
        }

        /// <summary>
        /// Suspends plot undo bridges and Bayesian analysis recording to prevent spurious undo entries
        /// during bulk data updates such as plot series population and axis title binding.
        /// </summary>
        /// <returns>An <see cref="IDisposable"/> that resumes recording when disposed.</returns>
        public IDisposable SuspendPlotBridges()
        {
            var suspensions = new List<IDisposable>();

            // Suspend element-level plot bridges
            if (_frequencyPlotUndo != null) suspensions.Add(_frequencyPlotUndo.SuspendRecording());

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
            else _bayesianController?.RebuildSeriesAndAnnotationBridges(plot);
        }

        #endregion

    }
}
