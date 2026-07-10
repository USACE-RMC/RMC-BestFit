using DatabaseManager;
using Numerics.Data;
using Numerics.Distributions;
using Numerics.Utilities;
using FrameworkInterfaces;
using FrameworkInterfaces.Messaging;
using FrameworkInterfaces.Undo;
using OxyPlot.Wpf;
using OxyPlot.Wpf.Serialization;
using RMC.BestFit.Models;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Xml;
using System.Xml.Linq;
using ModelAnalyses = RMC.BestFit.Analyses;

namespace RMC.BestFit.UI
{
    /// <summary>
    /// Distribution fitting analysis UI wrapper.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    /// This class wraps a <see cref="ModelAnalyses.FittingAnalysis"/> from the model library,
    /// delegating all computation to the inner analysis while providing UI-specific concerns:
    /// input data element reference management, messenger-based validation, SQLite persistence,
    /// and plot settings. Unlike other analysis wrappers, this analysis uses maximum likelihood
    /// estimation (MLE) rather than Bayesian MCMC.
    /// </para>
    /// </remarks>
    [Category("General"),
    DisplayName("Distribution Fitting Analysis"),
    Description("This analysis uses maximum likelihood estimation (MLE) to fit multiple distributions to the input data. Model selection is guided by comparing AIC, BIC, or RMSE values - lower values indicate a better fit."),
    Browsable(true)]
    public class FittingAnalysis : ElementBase
    {

        #region Construction

        /// <summary>
        /// Constructs a new distribution fitting analysis instance.
        /// </summary>
        /// <param name="name">The name of the element.</param>
        /// <param name="parentCollection">The parent collection name.</param>
        /// <param name="openFromFile">Optional parameter to open the function from disk upon construction.</param>
        public FittingAnalysis(string name, IElementCollection parentCollection, bool openFromFile = false) : base(name, parentCollection)
        {
            IsUndoEnabled = false;
            try
            {
                _nameOnDisk = name;
                _creationDate = DateTime.Now;
                _lastModified = DateTime.Now;

                // Create inner analysis with empty DataFrame placeholder (until InputData is set)
                _innerAnalysis = new ModelAnalyses.FittingAnalysis(new DataFrame());
                SubscribeInnerAnalysis();

                // Add messages
                _descriptionMsg = new BasicMessageItem(MessageType.Message, "No description provided for the distribution fitting analysis.", this, ParentCollection.Name, Name, nameof(Description), "DFA-MSG-001");
                _inputDataNullMsg = new BasicMessageItem(MessageType.Error, "Input data is missing. Please select valid input data.", this, ParentCollection.Name, Name, nameof(InputData), "DFA-ERR-005");
                _inputDataInValidMsg = new BasicMessageItem(MessageType.Error, "The selected input data is invalid.", this, ParentCollection.Name, Name, nameof(InputData), "DFA-ERR-006");
                _estimatedMsg = new BasicMessageItem(MessageType.Warning, "The distribution fitting analysis has not been performed.", this, ParentCollection.Name, Name, nameof(IsEstimated), "DFA-WRN-010");
                _messages = new List<BasicMessageItem>() { _descriptionMsg, _inputDataNullMsg, _inputDataInValidMsg, _estimatedMsg };

                // Create messenger and validation adapter
                _messenger = Messenger.GetInstance();
                _validationAdapter = new ValidationMessageAdapter(_messenger, this, ParentCollection.Name, "DFA");
                _messenger.Add(_descriptionMsg);
                _messenger.Add(_inputDataNullMsg);
                _messenger.Add(_estimatedMsg);

                // Create default plots
                _frequencyPlot = CreateDefaultFrequencyPlot();
                _pdfPlot = CreateDefaultPDFPlot();
                _cdfPlot = CreateDefaultCDFPlot();
                _ppPlot = CreateDefaultPPPlot();
                _qqPlot = CreateDefaultQQPlot();

                if (openFromFile == true) Open();

                _nameValid = ValidateName(BestFitProject.InvalidNameCharacters, 50, "DFA");
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
        [Description("Unique label identifying this fitting analysis; max 50 characters.")]
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
                    _nameValid = ValidateName(BestFitProject.InvalidNameCharacters, 50, "DFA");
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
        [Description("Free-text annotation describing this fitting analysis.")]
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

        /// <summary>
        /// Represents the name as it appears on disk.
        /// </summary>
        public override string NameOnDisk => _nameOnDisk;

        /// <summary>
        /// Gets the element image as an ImageSource.
        /// </summary>
        public override System.Windows.Media.ImageSource ElementImage =>
            System.Windows.Application.Current?.TryFindResource("FittingAnalysisIcon") as System.Windows.Media.ImageSource;

        /// <summary>
        /// Gets the resource key for the element icon, enabling dynamic theme updates.
        /// The project explorer uses <see cref="System.Windows.FrameworkElement.SetResourceReference"/>
        /// with this key so the icon updates automatically when the theme changes.
        /// </summary>
        public string ElementImageResourceKey => "FittingAnalysisIcon";

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
        /// The inner model analysis that performs MLE fitting.
        /// </summary>
        private ModelAnalyses.FittingAnalysis _innerAnalysis;

        /// <summary>
        /// All validation messages for this element.
        /// </summary>
        private List<BasicMessageItem> _messages;

        /// <summary>
        /// The singleton messenger instance for broadcasting validation messages.
        /// </summary>
        private Messenger _messenger;

        /// <summary>
        /// Bridges model library validation messages to the UI messaging system.
        /// </summary>
        private ValidationMessageAdapter _validationAdapter;

        /// <summary>
        /// Message displayed when no description is provided.
        /// </summary>
        private BasicMessageItem _descriptionMsg;

        /// <summary>
        /// Error message displayed when input data is null.
        /// </summary>
        private BasicMessageItem _inputDataNullMsg;

        /// <summary>
        /// Error message displayed when input data is invalid.
        /// </summary>
        private BasicMessageItem _inputDataInValidMsg;

        /// <summary>
        /// Warning message displayed when the analysis has not been performed.
        /// </summary>
        private BasicMessageItem _estimatedMsg;

        /// <summary>
        /// Whether this element was opened from a v1.0 project file.
        /// </summary>
        private bool openedFromV1 = false;

        /// <summary>
        /// Whether the element name passes validation.
        /// </summary>
        private bool _nameValid = false;

        /// <summary>
        /// Whether the input data reference is valid.
        /// </summary>
        private bool _inputDataValid = false;

        /// <summary>
        /// Whether the probability ordinates pass validation.
        /// </summary>
        private bool _ordinatesValid = true;

        /// <summary>
        /// The input data element.
        /// </summary>
        private InputData _inputData;

        // ProbabilityOrdinates removed â€” now owned by _innerAnalysis (pass-through property)

        /// <summary>
        /// The frequency plot.
        /// </summary>
        private Plot _frequencyPlot;

        /// <summary>
        /// The probability density function (PDF) plot.
        /// </summary>
        private Plot _pdfPlot;

        /// <summary>
        /// The cumulative distribution function (CDF) plot.
        /// </summary>
        private Plot _cdfPlot;

        /// <summary>
        /// The probability-probability (P-P) plot.
        /// </summary>
        private Plot _ppPlot;

        /// <summary>
        /// The quantile-quantile (Q-Q) plot.
        /// </summary>
        private Plot _qqPlot;

        /// <summary>
        /// Undo bridge for the probability ordinates collection.
        /// </summary>
        private UndoableCollectionBridge<double> _probabilityOrdinatesBridge;

        /// <summary>
        /// Per-plot undo managers for undo/redo of plot visual properties.
        /// </summary>
        private PlotUndoManager _frequencyPlotUndo;
        private PlotUndoManager _pdfPlotUndo;
        private PlotUndoManager _cdfPlotUndo;
        private PlotUndoManager _ppPlotUndo;
        private PlotUndoManager _qqPlotUndo;

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the input data used for evaluating the likelihood function.
        /// </summary>
        [Category("General")]
        [DisplayName("Input Data")]
        [Description("Input Data element supplied to the maximum-likelihood fitting engine.")]
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

                // Update the inner analysis DataFrame when input data changes
                if (_inputData != null && _inputData.DataFrame != null)
                    RecreateInnerAnalysis();

                SetIsValid();
                if (!UndoManager.IsExecutingAction)
                    ClearResults();
                RecordPropertyChange(nameof(InputData), old, value);
            }
        }

        /// <summary>
        /// Determines whether the distributions have been fitted.
        /// </summary>
        public bool IsEstimated
        {
            get
            {
                if (_innerAnalysis != null)
                    return _innerAnalysis.IsEstimated;
                return false;
            }
            private set
            {
                bool current = _innerAnalysis?.IsEstimated ?? false;
                if (current != value)
                {
                    // For the fitting analysis, IsEstimated is managed by the inner analysis
                    // after RunAsync completes. This setter is used during Open() for persistence.
                    _messenger.Remove(_estimatedMsg);
                    if (value == false)
                        _messenger.Add(_estimatedMsg);

                    RaisePropertyChange(nameof(IsEstimated));
                }
            }
        }

        /// <summary>
        /// Gets the exceedance probability values from the inner analysis.
        /// </summary>
        /// <remarks>
        /// The model-layer <see cref="ModelAnalyses.FittingAnalysis"/> owns the
        /// <see cref="Numerics.Data.ProbabilityOrdinates"/> collection. This property
        /// is a read-only pass-through for UI binding.
        /// </remarks>
        public ProbabilityOrdinates ProbabilityOrdinates => _innerAnalysis?.ProbabilityOrdinates;

        /// <summary>
        /// Gets the list of fitted distributions with their statistical metrics.
        /// </summary>
        /// <remarks>
        /// Each fitted distribution includes the distribution parameters and goodness-of-fit measures (AIC, BIC, RMSE).
        /// Results are sourced from the inner model analysis after fitting.
        /// </remarks>
        public List<FittedDistribution> FittedDistributions
        {
            get
            {
                if (_innerAnalysis != null)
                    return _innerAnalysis.FittedDistributions;
                return new List<FittedDistribution>();
            }
        }

        /// <summary>
        /// Gets the list of univariate distributions available for the fitting analysis.
        /// </summary>
        /// <remarks>
        /// This list includes common probability distributions such as Normal, Log-Normal, Gumbel, GEV, and others used in hydrologic frequency analysis.
        /// The list is sourced from the inner model analysis.
        /// </remarks>
        public List<UnivariateDistributionBase> DistributionList
        {
            get
            {
                if (_innerAnalysis != null)
                    return _innerAnalysis.DistributionList;
                return new List<UnivariateDistributionBase>()
                {
                    new Exponential(),
                    new GammaDistribution(),
                    new GeneralizedExtremeValue(),
                    new GeneralizedLogistic(),
                    new GeneralizedNormal(),
                    new GeneralizedPareto(),
                    new Gumbel(),
                    new KappaFour(),
                    new LnNormal(),
                    new Logistic(),
                    new LogNormal(),
                    new LogPearsonTypeIII(),
                    new Normal(),
                    new PearsonTypeIII(),
                    new Weibull()
                };
            }
        }

        /// <summary>
        /// Gets the frequency plot. Created once in the constructor; never replaced.
        /// </summary>
        public Plot FrequencyPlot => _frequencyPlot;

        /// <summary>
        /// Gets the probability density function (PDF) plot. Created once in the constructor; never replaced.
        /// </summary>
        public Plot PDFPlot => _pdfPlot;

        /// <summary>
        /// Gets the cumulative distribution function (CDF) plot. Created once in the constructor; never replaced.
        /// </summary>
        public Plot CDFPlot => _cdfPlot;

        /// <summary>
        /// Gets the probability-probability (P-P) plot. Created once in the constructor; never replaced.
        /// </summary>
        public Plot PPPlot => _ppPlot;

        /// <summary>
        /// Gets the quantile-quantile (Q-Q) plot. Created once in the constructor; never replaced.
        /// </summary>
        public Plot QQPlot => _qqPlot;

        #endregion

        #endregion

        #region IElement Methods

        /// <summary>
        /// Gets the required columns for the SQLite database table.
        /// </summary>
        /// <remarks>
        /// If you want to add a new column, add it to the end of the dictionary to maintain backward compatibility.
        /// Column names use string literals to match the original property names for IO compatibility,
        /// even though the properties have been replaced with Plot objects.
        /// </remarks>
        private static Dictionary<string, Type> RequiredColumns { get; } = new Dictionary<string, Type>() {
            { nameof(Name), typeof(string) },
            { nameof(Description), typeof(string) },
            { nameof(CreationDate), typeof(string) },
            { nameof(LastModified), typeof(string) },
            { nameof(InputData), typeof(string) },
            { nameof(FittedDistributions), typeof(string) },
            { nameof(ProbabilityOrdinates), typeof(string) },
            { nameof(IsEstimated), typeof(bool) },
            { "FrequencyPlotSettings", typeof(string) },
            { "PDFPlotSettings", typeof(string) },
            { "CDFPlotSettings", typeof(string) },
            { "PPPlotSettings", typeof(string) },
            { "QQPlotSettings", typeof(string) },
            { "AnalysisXml", typeof(string) } };

        /// <summary>
        /// Creates or updates the SQLite database table for storing fitting analysis elements.
        /// </summary>
        /// <param name="sqlite">The SQLite database manager instance.</param>
        private void CreateTable(SQLiteManager sqlite)
        {
            if (sqlite.TableNames.Contains(ParentCollection.Name) == false)
            {
                // If the table does not exist, then create the table
                var dataTable = new DataTable(ParentCollection.Name);
                foreach (KeyValuePair<string, Type> column in RequiredColumns)
                    dataTable.Columns.Add(column.Key, column.Value);
                sqlite.SaveDataTable(dataTable);
            }
            else
            {
                // Add any required columns that don't exist
                var dt = sqlite.GetTableManager(ParentCollection.Name);
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
        /// Opens the element from disk.
        /// </summary>
        /// <param name="sqlite">The SQLite manager.</param>
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

            // open element
            dtView = sqlite.GetTableManager(ParentCollection.Name);
            int rowIndex = dtView.SearchColumn(0, dtView.NumberOfRows - 1, "Name", NameOnDisk, true, true);
            if (rowIndex != -1)
            {
                // Use backing fields during deserialization to avoid repeated SetIsValid() and ClearResults() calls.
                // The InputData setter triggers SetIsValid(), ClearResults(), and RecreateInnerAnalysis().
                // ProbabilityOrdinates.FromDelimitedString() triggers CollectionChanged â†’ SetIsValid() + ClearResults().
                // A single SetIsValid() call at the end of Open() is sufficient.
                if (dtView.ColumnNames.Contains(nameof(Name)))
                {
                    _name = dtView.GetCell(nameof(Name), rowIndex).ToString();
                    foreach (var item in _messages) item.SourceName = _name;
                    _nameValid = ValidateName(BestFitProject.InvalidNameCharacters, 50, "DFA");
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

                // Get input data - use backing field to avoid SetIsValid(), ClearResults(), and RecreateInnerAnalysis()
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

                // Deserialize plot settings from SQLite into element-owned Plot objects
                DeserializePlotSettings(dtView, rowIndex, "FrequencyPlotSettings", _frequencyPlot);
                DeserializePlotSettings(dtView, rowIndex, "PDFPlotSettings", _pdfPlot);
                DeserializePlotSettings(dtView, rowIndex, "CDFPlotSettings", _cdfPlot);
                DeserializePlotSettings(dtView, rowIndex, "PPPlotSettings", _ppPlot);
                DeserializePlotSettings(dtView, rowIndex, "QQPlotSettings", _qqPlot);

                // Build an XElement for the inner analysis constructor.
                // This atomically restores ProbabilityOrdinates + FittedDistributions without side effects.
                XElement analysisXElement = null;

                if (version == "1.0")
                {
                    openedFromV1 = true;

                    // v1.0: Build XElement from OutputFrequencyOrdinates XML
                    if (dtView.ColumnNames.Contains("OutputFrequencyOrdinates"))
                    {
                        string xmlText = dtView.GetCell("OutputFrequencyOrdinates", rowIndex).ToString();
                        XmlDocument xmlDocument = new XmlDocument();
                        xmlDocument.LoadXml(xmlText);
                        var nodes = xmlDocument.GetElementsByTagName("OutputFrequencyOrdinates").Item(0)?.ChildNodes;
                        if (nodes == null || nodes.Count == 0)
                        {
                            if (wasOpen == false) sqlite.Close();
                            return;
                        }

                        // Extract probability ordinates from v1.0 XML nodes
                        var ordinateValues = new List<string>();
                        foreach (XmlNode node in nodes)
                        {
                            if (double.TryParse(node.Attributes.GetNamedItem("AEP").Value.ToString(), out var outP))
                                ordinateValues.Add(outP.ToString(System.Globalization.CultureInfo.InvariantCulture));
                        }

                        analysisXElement = new XElement("FittingAnalysis",
                            new XAttribute("IsEstimated", false),
                            new XElement("ProbabilityOrdinates",
                                string.Join(ProbabilityOrdinates.DefaultDelimiter, ordinateValues)));
                    }
                }
                else
                {
                    // v2.0+: Try new single-column format first
                    if (dtView.ColumnNames.Contains("AnalysisXml"))
                    {
                        var xml = dtView.GetCell("AnalysisXml", rowIndex).ToString();
                        if (!string.IsNullOrEmpty(xml))
                        {
                            try { analysisXElement = XElement.Parse(xml); }
                            catch { Debug.WriteLine("Failed to parse AnalysisXml column."); }
                        }
                    }

                    // Fall back to legacy multi-column format
                    if (analysisXElement == null)
                    {
                        string probOrdinates = "";
                        if (dtView.ColumnNames.Contains(nameof(ProbabilityOrdinates)))
                            probOrdinates = dtView.GetCell(nameof(ProbabilityOrdinates), rowIndex).ToString();

                        string fittedXml = "";
                        if (dtView.ColumnNames.Contains(nameof(FittedDistributions)))
                            fittedXml = dtView.GetCell(nameof(FittedDistributions), rowIndex).ToString();

                        bool wasEstimated = false;
                        if (dtView.ColumnNames.Contains(nameof(IsEstimated)))
                            bool.TryParse(dtView.GetCell(nameof(IsEstimated), rowIndex).ToString(), out wasEstimated);

                        var builder = new XElement("FittingAnalysis",
                            new XAttribute("IsEstimated", wasEstimated),
                            new XElement("ProbabilityOrdinates", probOrdinates));

                        if (!string.IsNullOrEmpty(fittedXml))
                        {
                            try { builder.Add(XElement.Parse(fittedXml)); }
                            catch { Debug.WriteLine("Failed to parse legacy FittedDistributions XML."); }
                        }

                        analysisXElement = builder;
                    }
                }

                // Create inner analysis from XElement â€” atomically loads ProbOrdinates + FittedDists
                if (_inputData?.DataFrame != null && analysisXElement != null)
                {
                    UnsubscribeInnerAnalysis();
                    try
                    {
                        _innerAnalysis = new ModelAnalyses.FittingAnalysis(_inputData.DataFrame, analysisXElement);
                    }
                    catch
                    {
                        _innerAnalysis = new ModelAnalyses.FittingAnalysis(_inputData.DataFrame);
                        Debug.WriteLine("Failed to reconstruct inner analysis from XElement; using defaults.");
                    }
                    SubscribeInnerAnalysis();
                }
            }

            if (wasOpen == false) sqlite.Close();

            // Reconnect undo bridges to the (possibly new) inner analysis collections.
            // This is critical when Open() is called outside the constructor (e.g., CopyFromExternal),
            // because the constructor's finally block already ran SetupBridges() on the old default
            // _innerAnalysis, and Open() just replaced it.
            SetupBridges();

            // Update the estimated message based on inner analysis state
            _messenger.Remove(_estimatedMsg);
            if (!IsEstimated)
                _messenger.Add(_estimatedMsg);

            SetIsValid();
            SetIsDirty(openedFromV1);
            RaisePropertyChange(nameof(Name), setDirty: openedFromV1);  // Notify node header binding that Name was restored from disk

            // Ensure WPF bindings point at the correct collection after deserialization
            // replaced _innerAnalysis. Without this, bindings may hold stale references.
            RaisePropertyChange(nameof(ProbabilityOrdinates), setDirty: false);

            }
            finally
            {
                IsUndoEnabled = wasUndoEnabled;
                if (wasUndoEnabled) ClearUndoHistory();
            }
        }

        /// <summary>
        /// Raises the PreviewObjectSaved event before saving the element to allow cancellation.
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
                int rowIndex = dtView.SearchColumn(0, dtView.NumberOfRows - 1, "Name", NameOnDisk, true, true);
                if (rowIndex < 0 || rowIndex >= dtView.NumberOfRows)
                {
                    dtView.AddRow();
                    rowIndex = dtView.NumberOfRows - 1;
                }

                // Get fitted distributions from inner analysis as XElement
                var fittedDists = new XElement(nameof(FittedDistributions));
                for (int i = 0; i < FittedDistributions.Count; i++)
                    fittedDists.Add(FittedDistributions[i].ToXElement());

                dtView.EditCell(rowIndex, nameof(Name), Name);
                dtView.EditCell(rowIndex, nameof(Description), Description);
                dtView.EditCell(rowIndex, nameof(CreationDate), FrameworkInterfaces.Utilities.Tools.DateToUniversalString(CreationDate));
                dtView.EditCell(rowIndex, nameof(LastModified), FrameworkInterfaces.Utilities.Tools.DateToUniversalString(LastModified));
                dtView.EditCell(rowIndex, nameof(InputData), InputData == null ? "" : InputData.Name);
                dtView.EditCell(rowIndex, nameof(FittedDistributions), fittedDists.ToString());
                dtView.EditCell(rowIndex, nameof(ProbabilityOrdinates), ProbabilityOrdinates?.ToDelimitedString("|") ?? "");
                dtView.EditCell(rowIndex, nameof(IsEstimated), IsEstimated);
                dtView.EditCell(rowIndex, "FrequencyPlotSettings", _frequencyPlot != null ? PlotSerializer.ToXElement(_frequencyPlot).ToString() : "");
                dtView.EditCell(rowIndex, "PDFPlotSettings", _pdfPlot != null ? PlotSerializer.ToXElement(_pdfPlot).ToString() : "");
                dtView.EditCell(rowIndex, "CDFPlotSettings", _cdfPlot != null ? PlotSerializer.ToXElement(_cdfPlot).ToString() : "");
                dtView.EditCell(rowIndex, "PPPlotSettings", _ppPlot != null ? PlotSerializer.ToXElement(_ppPlot).ToString() : "");
                dtView.EditCell(rowIndex, "QQPlotSettings", _qqPlot != null ? PlotSerializer.ToXElement(_qqPlot).ToString() : "");
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
        public override IElement Copy(string newName = "")
        {
            var element = new FittingAnalysis(newName == "" || string.IsNullOrEmpty(newName) ? Name : newName, ParentCollection);
            element.IsUndoEnabled = false;
            try
            {
                element.Description = Description;
                element.InputData = InputData;

                // Copy plots via serialize/deserialize
                if (_frequencyPlot != null) PlotSerializer.FromXElement(element._frequencyPlot, PlotSerializer.ToXElement(_frequencyPlot));
                if (_pdfPlot != null) PlotSerializer.FromXElement(element._pdfPlot, PlotSerializer.ToXElement(_pdfPlot));
                if (_cdfPlot != null) PlotSerializer.FromXElement(element._cdfPlot, PlotSerializer.ToXElement(_cdfPlot));
                if (_ppPlot != null) PlotSerializer.FromXElement(element._ppPlot, PlotSerializer.ToXElement(_ppPlot));
                if (_qqPlot != null) PlotSerializer.FromXElement(element._qqPlot, PlotSerializer.ToXElement(_qqPlot));

                // Copy probability ordinates into new element's inner analysis
                if (ProbabilityOrdinates != null && ProbabilityOrdinates.Count > 0)
                    element._innerAnalysis.ProbabilityOrdinates.FromDelimitedString(
                        ProbabilityOrdinates.ToDelimitedString(ProbabilityOrdinates.DefaultDelimiter),
                        ProbabilityOrdinates.DefaultDelimiter);
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
        public override IElement CopyFromExternal(string itemName, string fullFileName)
        {
            // Create SQLite connection
            var sqlite = new SQLiteManager(fullFileName);
            var element = new FittingAnalysis(itemName, ParentCollection);
            element.Open(sqlite);
            return element;
        }

        /// <summary>
        /// Deletes the element from disk and from the parent element collection.
        /// </summary>
        public override void Delete()
        {
            if (Name == null) return;
            DisposeBridges();
            UnsubscribeInnerAnalysis();
            if (_inputData != null)
            {
                _inputData.PropertyChanged -= InputDataChanged;
                _inputData.Deleted -= OnInputDataDeleted;
            }
            SetIsDirty(false);
            var sqlite = new SQLiteManager(ParentCollection.ParentProject.FullFileName);
            sqlite.Open();
            try
            {
                var dtView = sqlite.GetTableManager(ParentCollection.Name);
                int rowIndex = dtView.SearchColumn(0, dtView.NumberOfRows - 1, "Name", NameOnDisk, true, true);
                if (rowIndex >= 0 && rowIndex < dtView.NumberOfRows) dtView.DeleteRow(rowIndex);
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
        /// Validates the element and updates the IsValid property based on all validation criteria.
        /// </summary>
        /// <remarks>
        /// This method checks name validity, input data validity, and probability ordinates validity.
        /// It should be called whenever a property that affects validity is changed.
        /// </remarks>
        private void SetIsValid()
        {
            bool valid = true;

            // UI-only validations
            if (_nameValid == false) valid = false;
            if (_inputDataValid == false) valid = false;
            if (_ordinatesValid == false) valid = false;

            // Delegate model validation to inner analysis
            bool modelValid = _validationAdapter.SyncValidation(_innerAnalysis.Validate(), Name);
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
        /// Subscribes to inner analysis events (PropertyChanged and ProbabilityOrdinates.CollectionChanged).
        /// </summary>
        private void SubscribeInnerAnalysis()
        {
            if (_innerAnalysis == null) return;
            _innerAnalysis.PropertyChanged += InnerAnalysis_PropertyChanged;
            _innerAnalysis.ProbabilityOrdinates.CollectionChanged += ProbabilityOrdinates_CollectionChanged;
        }

        /// <summary>
        /// Unsubscribes from inner analysis events.
        /// </summary>
        private void UnsubscribeInnerAnalysis()
        {
            if (_innerAnalysis == null) return;
            _innerAnalysis.PropertyChanged -= InnerAnalysis_PropertyChanged;
            _innerAnalysis.ProbabilityOrdinates.CollectionChanged -= ProbabilityOrdinates_CollectionChanged;
        }

        /// <summary>
        /// Recreates the inner model analysis with the current InputData's DataFrame.
        /// Saves and restores probability ordinates across the replacement, and refreshes
        /// the undo bridge on the new collection instance.
        /// </summary>
        /// <remarks>
        /// Called when InputData changes to provide the inner analysis with a valid DataFrame for fitting.
        /// </remarks>
        private void RecreateInnerAnalysis()
        {
            // Save current probability ordinates before destroying old inner analysis
            string savedOrdinates = _innerAnalysis?.ProbabilityOrdinates?.ToDelimitedString(
                ProbabilityOrdinates.DefaultDelimiter);

            UnsubscribeInnerAnalysis();

            // Dispose old ProbabilityOrdinates bridge (collection instance is changing)
            _probabilityOrdinatesBridge?.Dispose();
            _probabilityOrdinatesBridge = null;

            // Create new inner analysis with real DataFrame
            _innerAnalysis = new ModelAnalyses.FittingAnalysis(InputData.DataFrame);

            // Restore probability ordinates into the new inner analysis
            if (!string.IsNullOrEmpty(savedOrdinates))
                _innerAnalysis.ProbabilityOrdinates.FromDelimitedString(
                    savedOrdinates, ProbabilityOrdinates.DefaultDelimiter);

            SubscribeInnerAnalysis();

            // Recreate ProbabilityOrdinates bridge on new collection
            _probabilityOrdinatesBridge = new UndoableCollectionBridge<double>(
                _innerAnalysis.ProbabilityOrdinates,
                () => IsUndoEnabled ? UndoManager : null,
                "probability ordinates",
                this
            );

            RaisePropertyChange(nameof(ProbabilityOrdinates));
        }

        /// <summary>
        /// Handles property change events forwarded from the inner model analysis.
        /// </summary>
        /// <param name="sender">The event source.</param>
        /// <param name="e">The property changed event arguments.</param>
        private void InnerAnalysis_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ModelAnalyses.FittingAnalysis.IsEstimated))
            {
                // Update estimated message
                bool isEstimated = _innerAnalysis.IsEstimated;
                _messenger.Remove(_estimatedMsg);
                if (!isEstimated)
                    _messenger.Add(_estimatedMsg);

                RaisePropertyChange(nameof(IsEstimated));
                RaisePropertyChange(nameof(FittedDistributions));
            }
            else
            {
                RaisePropertyChange(e.PropertyName);
            }
        }

        /// <summary>
        /// Handles property change events from the InputData element.
        /// </summary>
        /// <param name="sender">The event source.</param>
        /// <param name="e">The property changed event arguments.</param>
        /// <remarks>
        /// Clears analysis results when input data properties that affect the analysis are modified.
        /// </remarks>
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

            // Check if we need to clear results. Guard against undo replay: clearing the
            // fit on a replay-driven InputData event produces asymmetric undo (CLAUDE.md
            // "CRITICAL: never short-circuit in the UI layer").
            if (e.PropertyName != nameof(InputData.Name) &&
                e.PropertyName != nameof(InputData.DisplayName) &&
                e.PropertyName != nameof(InputData.Description) &&
                e.PropertyName != nameof(InputData.LastModified) &&
                e.PropertyName != nameof(InputData.UnitLabel) &&
                e.PropertyName != nameof(InputData.IndexLabel) &&
                e.PropertyName != nameof(InputData.DataFrame.PlottingParameter) &&
                e.PropertyName != nameof(Data.PlottingPosition) &&
                e.PropertyName != nameof(InputData.IsDirty) &&
                !UndoManager.IsExecutingAction)
            {
                ClearResults();
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
        /// Handles collection change events from the ProbabilityOrdinates collection.
        /// </summary>
        /// <param name="sender">The event source.</param>
        /// <param name="e">The collection changed event arguments.</param>
        /// <remarks>
        /// Validates probability ordinates. Does not invalidate the MLE fit â€” ordinates
        /// are an output display grid only; the fitted distribution parameters don't
        /// depend on them. The App-layer control listens for the <c>ProbabilityOrdinates</c>
        /// property change and refreshes the frequency plot / summary tables there.
        /// </remarks>
        private void ProbabilityOrdinates_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // Check probability ordinates (flag only â€” messages come from model via adapter in SetIsValid)
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
            RaisePropertyChange(nameof(ProbabilityOrdinates));
        }

        /// <summary>
        /// Clears all fitting analysis results and resets the IsEstimated flag.
        /// </summary>
        /// <remarks>
        /// This method delegates to the inner analysis to clear results, then updates UI messaging.
        /// </remarks>
        public void ClearResults()
        {
            if (_innerAnalysis != null)
                _innerAnalysis.ClearResults();

            _messenger.Remove(_estimatedMsg);
            _messenger.Add(_estimatedMsg);

            RaisePropertyChange(nameof(IsEstimated));
            RaisePropertyChange(nameof(FittedDistributions));
        }

        /// <summary>
        /// Cancels the currently running analysis operation.
        /// </summary>
        public void CancelAnalysis()
        {
            _innerAnalysis?.CancelAnalysis();
        }

        /// <summary>
        /// Performs the distribution fitting analysis by delegating to the inner model analysis.
        /// </summary>
        /// <param name="progressReporter">The progress reporter for tracking analysis progress.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <remarks>
        /// This method delegates the MLE fitting computation to the inner <see cref="ModelAnalyses.FittingAnalysis"/>,
        /// wrapping it with UI messaging for start/complete events.
        /// </remarks>
        public async Task RunAsync(SafeProgressReporter progressReporter)
        {
            if (IsValid == false) return;
            if (_innerAnalysis == null) return;

            _messenger.Add(new BasicMessageItem(MessageType.Event, "The distribution fitting analysis for '" + Name + "' has started.", this, ParentCollection.Name, Name, nameof(FittingAnalysis)));

            try
            {
                // Ensure the DataFrame threshold series is processed
                InputData.DataFrame.ProcessThresholdSeries();

                await _innerAnalysis.RunAsync(progressReporter);
            }
            catch (OperationCanceledException)
            {
                // Mirror UnivariateAnalysis.RunAsync's cancellation pattern: clear stale
                // results so the UI doesn't observe IsEstimated stuck on partial state.
                _innerAnalysis.ClearResults();
            }
            catch (Exception ex)
            {
                // Genuine failure (singular Hessian, divergent optimizer, bad data).
                // Clear results and surface a user-visible error message rather than
                // silently advertising "is complete" with stale state.
                System.Diagnostics.Debug.WriteLine($"Distribution fitting analysis failed: {ex.Message}");
                _innerAnalysis.ClearResults();
                _messenger.Add(new BasicMessageItem(MessageType.Error, "The distribution fitting analysis for '" + Name + "' failed: " + ex.Message, this, ParentCollection.Name, Name, nameof(FittingAnalysis)));
            }
            finally
            {
                _messenger.Add(new BasicMessageItem(MessageType.Event, "The distribution fitting analysis for '" + Name + "' is complete.", this, ParentCollection.Name, Name, nameof(FittingAnalysis)));
            }
        }

        #endregion

        #region Plot Factory Methods

        /// <summary>
        /// Applies the default plot style to a plot.
        /// </summary>
        /// <param name="plot">The plot to style.</param>
        private static void ApplyDefaultPlotStyle(Plot plot)
        {
            plot.BorderThickness = new System.Windows.Thickness(0);
            plot.Background = Brushes.Transparent;
            plot.LegendBackground = ColorFromHex("#8CFFFFFF");
            plot.LegendBorder = Colors.DarkGray;
            plot.Padding = new System.Windows.Thickness(10);
            plot.PlotAreaBackground = new SolidColorBrush(Colors.White);
        }

        /// <summary>
        /// Creates a color from a hex string.
        /// </summary>
        /// <param name="hex">The hex color string (e.g., "#8CFFFFFF").</param>
        /// <returns>The parsed color.</returns>
        private static Color ColorFromHex(string hex)
        {
            return (Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
        }

        /// <summary>
        /// Creates the default frequency plot with logarithmic Y-axis and normal probability X-axis.
        /// </summary>
        /// <returns>A new frequency plot with default axes configured.</returns>
        private Plot CreateDefaultFrequencyPlot()
        {
            var plot = new Plot();
            ApplyDefaultPlotStyle(plot);
            plot.Title = "Frequency";
            plot.LegendPosition = OxyPlot.Legends.LegendPosition.TopLeft;
            plot.Padding = new System.Windows.Thickness(10, 10, 14, 10);

            plot.Axes.Add(new LogarithmicAxis
            {
                Key = "Yaxis",
                Position = OxyPlot.Axes.AxisPosition.Left,
                PowerPadding = true,
                AxisTitleDistance = 20,
                TitleFontSize = 16,
                FontSize = 12,
                MajorGridlineStyle = OxyPlot.LineStyle.Solid,
                MinorGridlineStyle = OxyPlot.LineStyle.Dash,
                StringFormat = "N0",
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
        /// Creates the default probability density function (PDF) plot with linear axes.
        /// </summary>
        /// <returns>A new PDF plot with default axes configured.</returns>
        private Plot CreateDefaultPDFPlot()
        {
            var plot = new Plot();
            ApplyDefaultPlotStyle(plot);
            plot.Title = "Probability Density Function";
            plot.LegendPosition = OxyPlot.Legends.LegendPosition.TopRight;
            plot.Padding = new System.Windows.Thickness(10, 10, 14, 10);

            plot.Axes.Add(new LinearAxis
            {
                Key = "Xaxis",
                Position = OxyPlot.Axes.AxisPosition.Bottom,
                AxisTitleDistance = 20,
                TitleFontSize = 16,
                FontSize = 12,
                MajorGridlineStyle = OxyPlot.LineStyle.Solid,
                StringFormat = "N0",
            });
            plot.Axes.Add(new LinearAxis
            {
                Key = "Yaxis",
                Position = OxyPlot.Axes.AxisPosition.Left,
                Title = "Density",
                AxisTitleDistance = 20,
                TitleFontSize = 16,
                FontSize = 12,
                MajorGridlineStyle = OxyPlot.LineStyle.Solid,
                StringFormat = "E2",
            });

            return plot;
        }

        /// <summary>
        /// Creates the default cumulative distribution function (CDF) plot with linear axes.
        /// </summary>
        /// <returns>A new CDF plot with default axes configured.</returns>
        private Plot CreateDefaultCDFPlot()
        {
            var plot = new Plot();
            ApplyDefaultPlotStyle(plot);
            plot.Title = "Cumulative Distribution Function";
            plot.LegendPosition = OxyPlot.Legends.LegendPosition.BottomRight;
            plot.Padding = new System.Windows.Thickness(10, 10, 14, 10);

            plot.Axes.Add(new LinearAxis
            {
                Key = "Xaxis",
                Position = OxyPlot.Axes.AxisPosition.Bottom,
                AxisTitleDistance = 20,
                TitleFontSize = 16,
                FontSize = 12,
                MajorGridlineStyle = OxyPlot.LineStyle.Solid,
                StringFormat = "N0",
            });
            plot.Axes.Add(new LinearAxis
            {
                Key = "Yaxis",
                Position = OxyPlot.Axes.AxisPosition.Left,
                Title = "Non-Exceedance Probability",
                AxisTitleDistance = 20,
                TitleFontSize = 16,
                FontSize = 12,
                MajorGridlineStyle = OxyPlot.LineStyle.Solid,
                StringFormat = "F2",
            });

            return plot;
        }

        /// <summary>
        /// Creates the default probability-probability (P-P) plot with linear axes bounded [0, 1].
        /// </summary>
        /// <returns>A new P-P plot with default axes configured.</returns>
        private Plot CreateDefaultPPPlot()
        {
            var plot = new Plot();
            ApplyDefaultPlotStyle(plot);
            plot.Title = "P-P Plot";
            plot.LegendPosition = OxyPlot.Legends.LegendPosition.TopLeft;
            plot.Padding = new System.Windows.Thickness(10, 10, 14, 10);

            plot.Axes.Add(new LinearAxis
            {
                Key = "Xaxis",
                Position = OxyPlot.Axes.AxisPosition.Bottom,
                Title = "Probability (Model)",
                AbsoluteMaximum = 1.0,
                AbsoluteMinimum = 0.0,
                Maximum = 1.0,
                Minimum = 0.0,
                AxisTitleDistance = 20,
                TitleFontSize = 16,
                FontSize = 12,
                MajorGridlineStyle = OxyPlot.LineStyle.Solid,
                StringFormat = "F2",
            });
            plot.Axes.Add(new LinearAxis
            {
                Key = "Yaxis",
                Position = OxyPlot.Axes.AxisPosition.Left,
                Title = "Probability (Data)",
                AbsoluteMaximum = 1.0,
                AbsoluteMinimum = 0.0,
                Maximum = 1.0,
                Minimum = 0.0,
                AxisTitleDistance = 20,
                TitleFontSize = 16,
                FontSize = 12,
                MajorGridlineStyle = OxyPlot.LineStyle.Solid,
                StringFormat = "F2",
            });

            return plot;
        }

        /// <summary>
        /// Creates the default quantile-quantile (Q-Q) plot with linear axes.
        /// </summary>
        /// <returns>A new Q-Q plot with default axes configured.</returns>
        private Plot CreateDefaultQQPlot()
        {
            var plot = new Plot();
            ApplyDefaultPlotStyle(plot);
            plot.Title = "Q-Q Plot";
            plot.LegendPosition = OxyPlot.Legends.LegendPosition.TopLeft;
            plot.Padding = new System.Windows.Thickness(10, 10, 14, 10);

            plot.Axes.Add(new LinearAxis
            {
                Key = "Xaxis",
                Position = OxyPlot.Axes.AxisPosition.Bottom,
                Title = "Quantile (Model)",
                AxisTitleDistance = 20,
                TitleFontSize = 16,
                FontSize = 12,
                MajorGridlineStyle = OxyPlot.LineStyle.Solid,
                StringFormat = "N0",
            });
            plot.Axes.Add(new LinearAxis
            {
                Key = "Yaxis",
                Position = OxyPlot.Axes.AxisPosition.Left,
                Title = "Quantile (Data)",
                AxisTitleDistance = 20,
                TitleFontSize = 16,
                FontSize = 12,
                MajorGridlineStyle = OxyPlot.LineStyle.Solid,
                StringFormat = "N0",
            });

            return plot;
        }

        /// <summary>
        /// Deserializes plot settings from a SQLite column into a Plot object.
        /// </summary>
        /// <param name="dtView">The database table view.</param>
        /// <param name="rowIndex">The row index to read from.</param>
        /// <param name="columnName">The column name containing serialized plot settings.</param>
        /// <param name="plot">The target plot to apply settings to.</param>
        /// <remarks>
        /// If deserialization fails (e.g., old format or corrupt data), the plot retains its default settings.
        /// </remarks>
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
                System.Diagnostics.Debug.WriteLine($"Could not deserialize plot settings '{columnName}': {ex.Message}");
            }
        }

        #endregion

        #region Undo Bridge Management

        /// <summary>
        /// Sets up undo bridges for all undoable collections and plots.
        /// </summary>
        /// <remarks>
        /// Called in the constructor's finally block and after Open()/Copy().
        /// Disposes any existing bridges before creating new ones.
        /// </remarks>
        private void SetupBridges()
        {
            DisposeBridges();

            // Subscribe to UndoManager.StateChanged to revalidate after undo/redo completes.
            // Collection bridge undo replays may not reliably fire CollectionChanged (e.g., when
            // ProbabilityOrdinates extends List<T> with 'new' keyword shadowing), so this ensures
            // SetIsValid() is always called after the undo stack changes.
            UndoManager.StateChanged += UndoManager_StateChanged;

            // Create collection bridge for probability ordinates (owned by inner analysis)
            if (_innerAnalysis != null)
            {
                _probabilityOrdinatesBridge = new UndoableCollectionBridge<double>(
                    _innerAnalysis.ProbabilityOrdinates,
                    () => IsUndoEnabled ? UndoManager : null,
                    "probability ordinates",
                    this
                );
            }

            // Create plot undo managers â€” each monitors its plot's axes and annotations
            // for collection changes and auto-rebuilds bridges as needed.
            Func<IUndoManager> getUndo = () => IsUndoEnabled ? UndoManager : null;
            Action onRecorded = () => SetIsDirty(true);

            if (_frequencyPlot != null)
                _frequencyPlotUndo = new PlotUndoManager(_frequencyPlot, getUndo, "frequency plot", this, onRecorded);
            if (_pdfPlot != null)
                _pdfPlotUndo = new PlotUndoManager(_pdfPlot, getUndo, "PDF plot", this, onRecorded);
            if (_cdfPlot != null)
                _cdfPlotUndo = new PlotUndoManager(_cdfPlot, getUndo, "CDF plot", this, onRecorded);
            if (_ppPlot != null)
                _ppPlotUndo = new PlotUndoManager(_ppPlot, getUndo, "P-P plot", this, onRecorded);
            if (_qqPlot != null)
                _qqPlotUndo = new PlotUndoManager(_qqPlot, getUndo, "Q-Q plot", this, onRecorded);
        }

        /// <summary>
        /// Handles UndoManager.StateChanged to revalidate the element after undo/redo.
        /// </summary>
        private void UndoManager_StateChanged(object sender, EventArgs e)
        {
            SetIsValid();
        }

        /// <summary>
        /// Disposes all undo bridges and plot undo managers.
        /// Called from <see cref="SetupBridges"/> at the start of bridge re-creation and from
        /// <see cref="Delete"/> to release subscriptions before the element is destroyed.
        /// </summary>
        private void DisposeBridges()
        {
            UndoManager.StateChanged -= UndoManager_StateChanged;

            _probabilityOrdinatesBridge?.Dispose();
            _probabilityOrdinatesBridge = null;

            _frequencyPlotUndo?.Dispose();
            _frequencyPlotUndo = null;
            _pdfPlotUndo?.Dispose();
            _pdfPlotUndo = null;
            _cdfPlotUndo?.Dispose();
            _cdfPlotUndo = null;
            _ppPlotUndo?.Dispose();
            _ppPlotUndo = null;
            _qqPlotUndo?.Dispose();
            _qqPlotUndo = null;
        }

        /// <summary>
        /// Suspends undo recording for all plot undo managers.
        /// </summary>
        /// <returns>An <see cref="IDisposable"/> that resumes recording when disposed.</returns>
        /// <remarks>
        /// Use this when performing bulk plot operations (e.g., adding data series) that should not be recorded as undo actions.
        /// </remarks>
        public IDisposable SuspendPlotBridges()
        {
            var suspensions = new List<IDisposable>();
            if (_frequencyPlotUndo != null) suspensions.Add(_frequencyPlotUndo.SuspendRecording());
            if (_pdfPlotUndo != null) suspensions.Add(_pdfPlotUndo.SuspendRecording());
            if (_cdfPlotUndo != null) suspensions.Add(_cdfPlotUndo.SuspendRecording());
            if (_ppPlotUndo != null) suspensions.Add(_ppPlotUndo.SuspendRecording());
            if (_qqPlotUndo != null) suspensions.Add(_qqPlotUndo.SuspendRecording());
            return new AggregateDisposable(suspensions);
        }

        /// <summary>
        /// Rebuilds the series and annotation bridges for the specified plot.
        /// </summary>
        /// <param name="plot">The plot whose bridges need rebuilding.</param>
        /// <remarks>
        /// Call this after adding or removing series/annotations from a plot to reconnect undo tracking.
        /// </remarks>
        public void RebuildSeriesAndAnnotationBridges(Plot plot)
        {
            FindPlotUndoManager(plot)?.RebuildSeriesAndAnnotationBridges();
        }

        /// <summary>
        /// Finds the PlotUndoManager associated with the specified plot.
        /// </summary>
        /// <param name="plot">The plot to find the manager for.</param>
        /// <returns>The associated PlotUndoManager, or null if not found.</returns>
        private PlotUndoManager FindPlotUndoManager(Plot plot)
        {
            if (plot == _frequencyPlot) return _frequencyPlotUndo;
            if (plot == _pdfPlot) return _pdfPlotUndo;
            if (plot == _cdfPlot) return _cdfPlotUndo;
            if (plot == _ppPlot) return _ppPlotUndo;
            if (plot == _qqPlot) return _qqPlotUndo;
            return null;
        }


        #endregion
    }
}
