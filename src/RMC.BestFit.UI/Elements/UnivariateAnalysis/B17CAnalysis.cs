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
using RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;
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
using System.Xml.Linq;
using ModelAnalyses = RMC.BestFit.Analyses;

namespace RMC.BestFit.UI
{

    /// <summary>
    /// UI wrapper for the model-layer <see cref="ModelAnalyses.Bulletin17CAnalysis"/>. Implements
    /// the USGS Bulletin 17C flood frequency procedure (Generalized Method of Moments fit to a
    /// Log-Pearson Type III distribution, with regional skew weighting and historical-data /
    /// outlier handling) and exposes configurable uncertainty quantification (parametric or
    /// bootstrap) for stage-frequency curve generation.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    /// This class wraps a <see cref="ModelAnalyses.Bulletin17CAnalysis"/> from the model library,
    /// delegating all computation to the inner analysis while providing UI-specific concerns:
    /// input data management, SQLite persistence, messenger-based validation, and plot settings.
    /// </para>
    /// <para>
    /// Bulletin 17C uses the Generalized Method of Moments (GMM) rather than Bayesian MCMC.
    /// The <see cref="BayesianAnalysis"/> property delegates to the inner analysis's
    /// <see cref="ModelAnalyses.Bulletin17CAnalysis.BayesianAnalysis"/> which stores
    /// bootstrap results in a compatibility wrapper.
    /// </para>
    /// </remarks>
    [Category("General")]
    [DisplayName("Bulletin 17C Analysis")]
    [Description("Estimates flood frequency using the Bulletin 17C Generalized Method of Moments (GMM) with configurable uncertainty quantification methods.")]
    [Browsable(true)]
    public class B17CAnalysis : ElementBase, IUnivariate
    {

        #region Construction

        /// <summary>
        /// Constructs a new Bulletin 17C flood frequency analysis.
        /// </summary>
        /// <param name="name">The name of the element.</param>
        /// <param name="parentCollection">The parent collection name.</param>
        /// <param name="openFromFile">Optional parameter to open the function from disk upon construction.</param>
        public B17CAnalysis(string name, IElementCollection parentCollection, bool openFromFile = false) : base(name, parentCollection)
        {
            IsUndoEnabled = false;
            try
            {
                _nameOnDisk = name;
                _creationDate = DateTime.Now;
                _lastModified = DateTime.Now;

                // Create element-owned plots and Bayesian diagnostic controller
                _frequencyPlot = CreateDefaultFrequencyPlot();
                _bayesianController = new BayesianController();

                // Create the inner model analysis
                var dist = new Bulletin17CDistribution();
                _innerAnalysis = new ModelAnalyses.Bulletin17CAnalysis(dist);

                // Subscribe to inner analysis property changes and ProbabilityOrdinates collection changes
                SubscribeInnerAnalysis();

                // Add UI-only messages (message prefix "B17-")
                _descriptionMsg = new BasicMessageItem(MessageType.Message, "The Bulletin 17C analysis does not have a description.", this, ParentCollection.Name, Name, nameof(Description), "B17-MSG-001");
                _inputDataNullMsg = new BasicMessageItem(MessageType.Error, "Input data is missing. Please select valid input data.", this, ParentCollection.Name, Name, nameof(InputData), "B17-ERR-005");
                _inputDataInValidMsg = new BasicMessageItem(MessageType.Error, "The selected input data is invalid.", this, ParentCollection.Name, Name, nameof(InputData), "B17-ERR-006");
                _uncertaintyFailedMsg = new BasicMessageItem(MessageType.Warning, "Uncertainty quantification failed â€” the covariance matrix is not positive-definite. The point estimate is still valid but confidence intervals could not be computed. Consider using a different distribution or the Bootstrap uncertainty method.", this, ParentCollection.Name, Name, nameof(B17CAnalysis), "B17-WRN-001");

                // ProbabilityOrdinates validation messages are surfaced by the model-layer
                // ProbabilityOrdinates.Validate() routed through _validationAdapter â€” defining
                // duplicates here would produce two messages per error.
                _messages = new List<BasicMessageItem>()
                {   _descriptionMsg,
                    _inputDataNullMsg,
                    _inputDataInValidMsg,
                    _uncertaintyFailedMsg
                };

                // Create messenger and validation adapter
                _messenger = Messenger.GetInstance();
                _validationAdapter = new ValidationMessageAdapter(_messenger, this, ParentCollection.Name, "B17");
                _messenger.Add(_descriptionMsg);
                _messenger.Add(_inputDataNullMsg);

                if (openFromFile == true) Open();

                _nameValid = ValidateName(BestFitProject.InvalidNameCharacters, 50, "B17");
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
        [Description("Unique label identifying this Bulletin 17C analysis; max 50 characters.")]
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
                    _nameValid = ValidateName(BestFitProject.InvalidNameCharacters, 50, "B17");
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
        [Description("Free-text annotation describing this Bulletin 17C analysis.")]
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
            System.Windows.Application.Current?.TryFindResource("B17AnalysisIcon") as System.Windows.Media.ImageSource;

        /// <summary>
        /// Gets the resource key for the element icon, enabling dynamic theme updates.
        /// The project explorer uses <see cref="System.Windows.FrameworkElement.SetResourceReference"/>
        /// with this key so the icon updates automatically when the theme changes.
        /// </summary>
        public string ElementImageResourceKey => "B17AnalysisIcon";

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
        private ModelAnalyses.Bulletin17CAnalysis _innerAnalysis;

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
        /// Warning message displayed when uncertainty quantification fails (e.g., non-positive-definite covariance).
        /// </summary>
        private BasicMessageItem _uncertaintyFailedMsg;

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
        /// Gets the collection name for storing Bulletin 17C analyses in the database.
        /// </summary>
        public static string CollectionName => "<Bulletin 17C>";

        /// <summary>
        /// The input data for the analysis.
        /// </summary>
        private InputData _inputData;

        /// <summary>
        /// The element-owned frequency plot. Created in the constructor and persisted via <see cref="PlotSerializer"/>.
        /// </summary>
        private Plot _frequencyPlot;

        /// <summary>
        /// Undo manager for the frequency plot visual properties (axis, legend, series styling).
        /// </summary>
        private PlotUndoManager _frequencyPlotUndo;

        /// <summary>
        /// Manages the Bayesian diagnostic plots (kernel density, histogram, bivariate heat map, influence diagnostics)
        /// including undo bridges and serialization.
        /// </summary>
        /// <remarks>
        /// Although B17C uses GMM rather than MCMC, <see cref="BayesianController"/> provides the canonical
        /// plot ownership, undo bridge, and serialization pattern. The MeanLikelihood, Autocorrelation, and
        /// MarkovChainTraces plots are unused by B17C (never displayed or serialized) but are harmless empty
        /// Plot objects created by the controller.
        /// </remarks>
        private BayesianController _bayesianController;

        /// <summary>
        /// Undo bridge for the probability ordinates collection.
        /// </summary>
        private UndoableCollectionBridge<double> _probabilityOrdinatesBridge;

        /// <summary>
        /// XElement snapshot of the Bulletin17CDistribution state before the most recent user change.
        /// Used to compute undo/redo actions via snapshot comparison.
        /// </summary>
        private XElement _distributionSnapshot;

        /// <summary>
        /// The set of <see cref="Bulletin17CDistribution"/> property names that represent user-editable
        /// model state and should trigger XElement snapshot comparison for undo recording.
        /// </summary>
        private static readonly HashSet<string> DistributionUndoProperties = new()
        {
            "DistributionType", "Parameters", "ParameterPenalties", "QuantilePenalties"
        };

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the input data used for evaluating the likelihood function.
        /// </summary>
        [Category("General")]
        [DisplayName("Input Data")]
        [Description("Input Data element with annual-peak streamflow data for the Bulletin 17C GMM algorithm.")]
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
                if (_innerAnalysis.Bulletin17CDistribution != null)
                {
                    if (InputData != null && InputData.DataFrame != null)
                        _innerAnalysis.Bulletin17CDistribution.DataFrame = InputData.DataFrame;
                    else
                        _innerAnalysis.Bulletin17CDistribution.DataFrame = null;
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
        /// Gets the Bulletin 17C distribution model. Delegates to the inner analysis.
        /// </summary>
        public Bulletin17CDistribution Bulletin17CDistribution => _innerAnalysis.Bulletin17CDistribution;

        /// <summary>
        /// Gets the exceedance probability values used for plotting the distribution.
        /// </summary>
        public ProbabilityOrdinates ProbabilityOrdinates => _innerAnalysis.ProbabilityOrdinates;

        /// <summary>
        /// Gets the parameter penalties from the B17C distribution. Used for regional skew priors.
        /// </summary>
        /// <remarks>
        /// <b>Returns a live reference, not a copy.</b> Mutations to the returned list (Add / Remove /
        /// item-property edits) bypass the element's undo/dirty tracking â€” only changes that flow through
        /// <c>RecordDistributionUndo</c> via <c>Bulletin17CDistribution.PropertyChanged</c> are captured.
        /// Treat this property as read-via-binding; route programmatic edits through the wrapping
        /// distribution's API so undo / dirty / persistence stay coherent.
        /// </remarks>
        public List<ParameterPenalty> ParameterPenalties => _innerAnalysis.Bulletin17CDistribution.ParameterPenalties;

        /// <summary>
        /// Gets the quantile penalties from the B17C distribution. Used for regional quantile priors.
        /// </summary>
        /// <remarks>
        /// Same caveat as <see cref="ParameterPenalties"/> â€” live reference, not a copy. Mutate via
        /// the wrapping distribution to preserve undo / dirty tracking.
        /// </remarks>
        public List<QuantilePenalty> QuantilePenalties => _innerAnalysis.Bulletin17CDistribution.QuantilePenalties;

        /// <summary>
        /// The Bayesian Analysis object. Delegates to the inner analysis.
        /// </summary>
        /// <remarks>
        /// Although B17C uses GMM rather than true Bayesian MCMC, results are stored in a
        /// <see cref="BayesianAnalysis"/> object to maintain compatibility with the uncertainty
        /// analysis framework.
        /// </remarks>
        public BayesianAnalysis BayesianAnalysis => _innerAnalysis.BayesianAnalysis;

        /// <summary>
        /// Determines whether the analysis has been estimated.
        /// </summary>
        public bool IsEstimated => _innerAnalysis.IsEstimated;

        /// <summary>
        /// The frequency analysis results. Delegates to the inner analysis.
        /// </summary>
        public UncertaintyAnalysisResults AnalysisResults => _innerAnalysis.AnalysisResults;

        /// <summary>
        /// Gets the Generalized Method of Moments estimator. Delegates to the inner analysis.
        /// </summary>
        public GeneralizedMethodOfMoments GMM => _innerAnalysis.GMM;

        /// <summary>
        /// Gets the total elapsed wall-clock time for the entire analysis. Delegates to the inner analysis.
        /// </summary>
        public TimeSpan? ElapsedTime => _innerAnalysis.ElapsedTime;

        /// <summary>
        /// Gets the elapsed time for GMM parameter estimation only. Delegates to the inner analysis.
        /// </summary>
        public TimeSpan? GMMElapsedTime => _innerAnalysis.GMMElapsedTime;

        /// <summary>
        /// Gets the elapsed time for the uncertainty quantification phase. Delegates to the inner analysis.
        /// </summary>
        public TimeSpan? UncertaintyElapsedTime => _innerAnalysis.UncertaintyElapsedTime;

        /// <summary>
        /// Gets the bootstrap diagnostics from the most recent analysis. Delegates to the inner analysis.
        /// </summary>
        public BootstrapDiagnostics BootstrapResults => _innerAnalysis.BootstrapResults;

        /// <summary>
        /// Gets or sets the uncertainty quantification method.
        /// </summary>
        [Category("General")]
        [DisplayName("Uncertainty Method")]
        [Description("The method used for uncertainty quantification in the Bulletin 17C analysis.")]
        [Browsable(true)]
        public ModelAnalyses.UncertaintyMethod UncertaintyMethod
        {
            get => _innerAnalysis.UncertaintyMethod;
            set
            {
                if (_innerAnalysis.UncertaintyMethod != value)
                {
                    var old = _innerAnalysis.UncertaintyMethod;
                    _innerAnalysis.UncertaintyMethod = value;
                    RecordPropertyChange(nameof(UncertaintyMethod), old, value);
                }
            }
        }

        /// <summary>
        /// Gets or sets the pseudo-random number generator seed.
        /// Wraps <see cref="BayesianAnalysis.PRNGSeed"/> with undo recording.
        /// </summary>
        [Category("General")]
        [DisplayName("PRNG Seed")]
        [Description("Specifies the pseudo-random number generator seed for reproducibility.")]
        [Browsable(true)]
        public int PRNGSeed
        {
            get => _innerAnalysis.BayesianAnalysis.PRNGSeed;
            set
            {
                if (_innerAnalysis.BayesianAnalysis.PRNGSeed != value)
                {
                    var old = _innerAnalysis.BayesianAnalysis.PRNGSeed;
                    _innerAnalysis.BayesianAnalysis.PRNGSeed = value;
                    RecordPropertyChange(nameof(PRNGSeed), old, value);
                }
            }
        }

        /// <summary>
        /// Gets the element-owned frequency plot. Persisted via <see cref="PlotSerializer"/>.
        /// </summary>
        public Plot FrequencyPlot => _frequencyPlot;

        /// <summary>
        /// Gets the Bayesian diagnostic plot controller (kernel density, histogram, bivariate, influence).
        /// </summary>
        public BayesianController BayesianPlots => _bayesianController;

        /// <summary>
        /// Backing field for the cached GMM report text.
        /// </summary>
        private string _gmmReport = string.Empty;

        /// <summary>
        /// Gets the cached GMM report text from the last save/open cycle.
        /// </summary>
        public string GMMReport => _gmmReport;

        /// <summary>
        /// Generates a comprehensive plain-text GMM estimation report by delegating to the inner analysis.
        /// </summary>
        /// <returns>The report string, or empty string if not estimated.</returns>
        public string GenerateGMMReport() => _innerAnalysis?.GenerateGMMReport() ?? string.Empty;

        /// <summary>
        /// Updates the cached GMM report text and notifies bindings when it changes.
        /// </summary>
        /// <param name="report">The report text to cache. Null values are normalized to an empty string.</param>
        /// <param name="setDirty">Whether this cache update should mark the element dirty.</param>
        /// <remarks>
        /// The report is persisted as UI-owned text because restored GMM estimators intentionally do
        /// not carry the transient optimizer object needed to regenerate the full report.
        /// </remarks>
        private void SetGMMReport(string report, bool setDirty = true)
        {
            string normalizedReport = report ?? string.Empty;
            if (_gmmReport == normalizedReport) return;

            _gmmReport = normalizedReport;
            RaisePropertyChange(nameof(GMMReport), setDirty);
        }

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

            // Check if we need to clear results (skip during undo/redo replay)
            if (!UndoManager.IsExecutingAction &&
                e.PropertyName != nameof(InputData.Name) &&
                e.PropertyName != nameof(InputData.DisplayName) &&
                e.PropertyName != nameof(InputData.Description) &&
                e.PropertyName != nameof(InputData.LastModified) &&
                e.PropertyName != nameof(InputData.UnitLabel) &&
                e.PropertyName != nameof(InputData.IndexLabel) &&
                e.PropertyName != nameof(InputData.DataFrame.PlottingParameter) &&
                e.PropertyName != "PlottingPosition" &&
                e.PropertyName != nameof(InputData.IsDirty))
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
        /// Handles changes to the UI probability ordinates collection.
        /// Syncs changes to the inner analysis and validates the ordinates.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">The collection change event arguments.</param>
        private void ProbabilityOrdinates_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
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
            // validate + message here. See RMC.BestFit.Analyses.Bulletin17CAnalysis
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
        /// Forwards property change notifications from the inner analysis to the UI framework.
        /// </summary>
        /// <param name="sender">The inner analysis object.</param>
        /// <param name="e">The property change event arguments.</param>
        private void InnerAnalysis_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Record undo for Bulletin17CDistribution property changes via XElement snapshot.
            // The model-layer analysis passes distribution property names through unchanged
            // (e.g., "DistributionType", "Parameters", "ParameterPenalties", "QuantilePenalties").
            if (DistributionUndoProperties.Contains(e.PropertyName))
            {
                RecordDistributionUndo(e.PropertyName);
            }

            if (e.PropertyName == nameof(ModelAnalyses.Bulletin17CAnalysis.IsEstimated) &&
                _innerAnalysis.IsEstimated == false)
            {
                SetGMMReport(string.Empty);
            }

            // Forward relevant property changes to the WPF framework
            if (e.PropertyName == nameof(ModelAnalyses.Bulletin17CAnalysis.IsEstimated) ||
                e.PropertyName == nameof(ModelAnalyses.Bulletin17CAnalysis.AnalysisResults) ||
                e.PropertyName == nameof(ModelAnalyses.Bulletin17CAnalysis.Bulletin17CDistribution) ||
                e.PropertyName == nameof(ModelAnalyses.Bulletin17CAnalysis.BayesianAnalysis))
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
        /// The required columns for the SQLite table.
        /// If you want to add a new column, add it to the end of the dictionary.
        /// </summary>
        private static Dictionary<string, Type> RequiredColumns { get; } = new Dictionary<string, Type>() {
            { nameof(Name), typeof(string) },
            { nameof(Description), typeof(string) },
            { nameof(CreationDate), typeof(string) },
            { nameof(LastModified), typeof(string) },
            { nameof(InputData), typeof(string) },
            { "Bulletin17CDistribution", typeof(string) },
            { nameof(BayesianAnalysis), typeof(string) },
            { nameof(MCMCResults), typeof(byte[]) },
            { nameof(AnalysisResults), typeof(string) },
            { nameof(ProbabilityOrdinates), typeof(string) },
            { "FrequencyPlotSettings", typeof(string) },
            { "KernelDensityPlotSettings", typeof(string) },
            { "HistogramPlotSettings", typeof(string) },
            { "BivariatePlotSettings", typeof(string) },
            { "InfluenceDiagnosticsPlotSettings", typeof(string) },
            { "AnalysisXml", typeof(string) },
            { "MCMCReport", typeof(string) },
            { "GMMReport", typeof(string) } };

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
                SetGMMReport(string.Empty, setDirty: false);
                var wasOpen = sqlite.DataBaseOpen;
                if (wasOpen == false) sqlite.Open();

                var dtView = sqlite.GetTableManager(CollectionName);
                int rowIndex = dtView.SearchColumn(0, dtView.NumberOfRows - 1, "Name", NameOnDisk, true, true);
                if (rowIndex != -1)
                {
                    if (dtView.ColumnNames.Contains(nameof(Name)))
                    {
                        _name = dtView.GetCell(nameof(Name), rowIndex).ToString();
                        foreach (var item in _messages) item.SourceName = _name;
                        _nameValid = ValidateName(BestFitProject.InvalidNameCharacters, 50, "B17");
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

                    // Deserialize the element-owned frequency plot.
                    DeserializePlotSettings(dtView, rowIndex, "FrequencyPlotSettings", _frequencyPlot);

                    // Delegate Bayesian-diagnostic plot deserialization to the controller (canonical path).
                    // BayesianController.DeserializePlot guards each column with ColumnNames.Contains,
                    // so the 3 columns B17C does not persist (MarkovChainTraces, Autocorrelation,
                    // MeanLikelihood) are safely skipped.
                    _bayesianController.Deserialize(dtView, rowIndex);

                    // GMM report
                    if (dtView.ColumnNames.Contains("GMMReport"))
                        SetGMMReport(dtView.GetCell("GMMReport", rowIndex)?.ToString() ?? string.Empty, setDirty: false);

                    // Get probability ordinates
                    if (dtView.ColumnNames.Contains(nameof(ProbabilityOrdinates)))
                    {
                        ProbabilityOrdinates.FromDelimitedString(
                            dtView.GetCell(nameof(ProbabilityOrdinates), rowIndex).ToString(), "|");
                    }

                    // Get model and reconstruct the inner analysis
                    var modelXmlStr = dtView.ColumnNames.Contains("Bulletin17CDistribution") ? dtView.GetCell("Bulletin17CDistribution", rowIndex)?.ToString() : null;
                    if (!string.IsNullOrEmpty(modelXmlStr) && InputData != null && InputData.DataFrame != null)
                    {
                        var modelXElement = XElement.Parse(modelXmlStr);
                        var dist = new Bulletin17CDistribution(InputData.DataFrame, modelXElement);

                        MCMCResults mcmcResults = AnalysisPersistenceHelper.TryLoadMCMCResults(dtView, rowIndex, Name);

                        UncertaintyAnalysisResults analysisResults =
                            AnalysisPersistenceHelper.TryLoadAnalysisResults(dtView, nameof(AnalysisResults), rowIndex, Name);

                        // Build analysis XElement for deserialization constructor
                        XElement analysisXElement = AnalysisPersistenceHelper.TryLoadXElement(dtView, "AnalysisXml", rowIndex, Name);
                        if (analysisXElement == null)
                        {
                            // Legacy fallback: construct minimal XElement from individual columns
                            analysisXElement = new XElement("Bulletin17CAnalysis",
                                new XAttribute("IsEstimated", mcmcResults != null));
                            if (dtView.ColumnNames.Contains(nameof(ProbabilityOrdinates)))
                                analysisXElement.Add(new XElement("ProbabilityOrdinates", ProbabilityOrdinates.ToDelimitedString("|")));
                            if (dtView.ColumnNames.Contains(nameof(BayesianAnalysis)))
                            {
                                var bayesStr = dtView.GetCell(nameof(BayesianAnalysis), rowIndex).ToString();
                                if (!string.IsNullOrEmpty(bayesStr))
                                    analysisXElement.Add(XElement.Parse(bayesStr));
                            }
                        }

                        // Reconstruct inner analysis (UncertaintyMethod restored from analysisXElement)
                        UnsubscribeInnerAnalysis();
                        _innerAnalysis = new ModelAnalyses.Bulletin17CAnalysis(dist, analysisXElement, mcmcResults, analysisResults);
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
                RaisePropertyChange(nameof(Name), setDirty: false);  // Notify node header binding

                // Ensure WPF bindings point at the correct collection/object after deserialization
                // replaced _innerAnalysis. Without these, bindings may hold stale references.
                RaisePropertyChange(nameof(Bulletin17CDistribution), setDirty: false);
                RaisePropertyChange(nameof(ParameterPenalties), setDirty: false);
                RaisePropertyChange(nameof(QuantilePenalties), setDirty: false);
                RaisePropertyChange(nameof(ProbabilityOrdinates), setDirty: false);
                RaisePropertyChange(nameof(BayesianAnalysis), setDirty: false);
                RaisePropertyChange(nameof(IsEstimated), setDirty: false);
                RaisePropertyChange(nameof(GMM), setDirty: false);
                RaisePropertyChange(nameof(AnalysisResults), setDirty: false);
                RaisePropertyChange(nameof(GMMReport), setDirty: false);
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

                // Serialize from inner analysis (UncertaintyMethod is persisted in AnalysisXml)
                dtView.EditCell(rowIndex, "Bulletin17CDistribution", _innerAnalysis.Bulletin17CDistribution.ToXElement().ToString());
                dtView.EditCell(rowIndex, nameof(BayesianAnalysis), _innerAnalysis.BayesianAnalysis.ToXElement().ToString());

                dtView.EditCell(rowIndex, nameof(MCMCResults),
                    AnalysisPersistenceHelper.SerializeMCMCResults(_innerAnalysis.BayesianAnalysis.Results));
                dtView.EditCell(rowIndex, nameof(AnalysisResults),
                    AnalysisPersistenceHelper.SerializeAnalysisResults(_innerAnalysis.AnalysisResults));

                // Save the full analysis XML for atomic deserialization
                dtView.EditCell(rowIndex, "AnalysisXml", _innerAnalysis?.ToXElement()?.ToString() ?? "");

                dtView.EditCell(rowIndex, nameof(ProbabilityOrdinates), ProbabilityOrdinates?.ToDelimitedString("|") ?? "");

                // Serialize the element-owned frequency plot.
                dtView.EditCell(rowIndex, "FrequencyPlotSettings", _frequencyPlot != null ? PlotSerializer.ToXElement(_frequencyPlot).ToString() : "");

                // Delegate Bayesian-diagnostic plot serialization to the controller (canonical path).
                // BayesianController.SerializePlot guards each column with ColumnNames.Contains,
                // so the 3 columns B17C does not persist are safely skipped.
                _bayesianController.Serialize(dtView, rowIndex);

                // GMM report. A restored GMM has no transient Optimizer, so report generation
                // returns empty after Open(); preserve the cached text in that path.
                string generatedGMMReport = GenerateGMMReport();
                if (!string.IsNullOrEmpty(generatedGMMReport))
                    SetGMMReport(generatedGMMReport);
                dtView.EditCell(rowIndex, "GMMReport", _gmmReport);

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
            var element = new B17CAnalysis(newName == "" || string.IsNullOrEmpty(newName) ? Name : newName, ParentCollection);

            element.IsUndoEnabled = false;
            try
            {
                element.Description = Description;

                // Set input data first so the default inner analysis gets a DataFrame,
                // and validation/messaging is wired up.
                element.InputData = InputData;

                // Reconstruct inner analysis with cloned B17C distribution.
                // Clone() preserves all parameters, penalties, and links via XElement round-trip.
                // The cloned distribution already has the correct DataFrame from the source.
                element.UnsubscribeInnerAnalysis();
                var clonedDist = (Bulletin17CDistribution)Bulletin17CDistribution.Clone();
                element._innerAnalysis = new ModelAnalyses.Bulletin17CAnalysis(clonedDist);
                element.SubscribeInnerAnalysis();

                // Copy Bayesian analysis settings
                element._innerAnalysis.BayesianAnalysis.CredibleIntervalWidth = BayesianAnalysis.CredibleIntervalWidth;
                element._innerAnalysis.BayesianAnalysis.PRNGSeed = BayesianAnalysis.PRNGSeed;
                element._innerAnalysis.BayesianAnalysis.OutputLength = BayesianAnalysis.OutputLength;
                element._innerAnalysis.BayesianAnalysis.PointEstimator = BayesianAnalysis.PointEstimator;
                element._innerAnalysis.UncertaintyMethod = _innerAnalysis.UncertaintyMethod;

                // Copy probability ordinates
                if (ProbabilityOrdinates != null && ProbabilityOrdinates.Count > 0)
                    element._innerAnalysis.ProbabilityOrdinates.FromDelimitedString(
                        ProbabilityOrdinates.ToDelimitedString(ProbabilityOrdinates.DefaultDelimiter),
                        ProbabilityOrdinates.DefaultDelimiter);

                // Copy plot settings via PlotSerializer round-trip (inside undo suppression â€” matches FittingAnalysis.Copy template)
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
            var element = new B17CAnalysis(itemName, ParentCollection);
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
            // InputData setter â€” that would re-add _inputDataNullMsg and re-flip IsDirty=true,
            // breaking messenger cleanup and triggering a spurious save prompt on tab close).
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
            SetGMMReport(string.Empty);
            _messenger.Remove(_uncertaintyFailedMsg);
            RaisePropertyChange(nameof(AnalysisResults));
        }

        /// <summary>
        /// Runs the Bulletin 17C analysis asynchronously by delegating to the inner analysis.
        /// </summary>
        /// <param name="progressReporter">Progress reporter for tracking analysis progress.</param>
        public async Task RunAsync(SafeProgressReporter progressReporter)
        {
            SetIsValid();
            if (IsValid == false) return;

            _messenger.Add(new BasicMessageItem(MessageType.Event, "The Bulletin 17C analysis for '" + Name + "' has started.", this, ParentCollection.Name, Name, nameof(B17CAnalysis)));

            try
            {
                // Prepare input data (UI-specific preprocessing)
                InputData.DataFrame.ProcessThresholdSeries();

                // Clear stale warning from previous run
                _messenger.Remove(_uncertaintyFailedMsg);

                // Delegate to inner analysis RunAsync
                progressReporter.IndicateTaskStart();
                await _innerAnalysis.RunAsync(progressReporter);

                if (_innerAnalysis.IsEstimated)
                {
                    string generatedGMMReport = GenerateGMMReport();
                    if (!string.IsNullOrEmpty(generatedGMMReport))
                        SetGMMReport(generatedGMMReport);
                }
                else
                {
                    SetGMMReport(string.Empty);
                }

                // Check for partial failure: GMM point estimate succeeded but uncertainty quantification failed.
                // When cancelled, the model sets IsEstimated = false, so this only triggers on genuine failure.
                if (_innerAnalysis.IsEstimated && _innerAnalysis.AnalysisResults == null)
                {
                    // Prefer the model's specific diagnostic (e.g., "more than half of the requested
                    // replicates were discarded") over the generic covariance text. The field and the
                    // _messages entry are replaced together so ClearResults' and Delete's
                    // remove-by-field calls target the live message instance.
                    _messages.Remove(_uncertaintyFailedMsg);
                    _uncertaintyFailedMsg = new BasicMessageItem(MessageType.Warning,
                        GetUncertaintyFailureMessageText(), this, ParentCollection.Name, Name,
                        nameof(B17CAnalysis), "B17-WRN-001");
                    _messages.Add(_uncertaintyFailedMsg);
                    _messenger.Add(_uncertaintyFailedMsg);
                }
            }
            catch (OperationCanceledException)
            {
                ClearResults();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"B17CAnalysis.RunAsync failed for '{Name}': {ex}");
                ClearResults();
                _messenger.Add(new BasicMessageItem(MessageType.Error, $"The Bulletin 17C analysis for '{Name}' failed: {ex.Message}", this, ParentCollection.Name, Name, nameof(B17CAnalysis)));
            }
            finally
            {
                progressReporter.IndicateTaskEnded();
                SetIsValid();
                _messenger.Add(new BasicMessageItem(MessageType.Event, "The Bulletin 17C analysis for '" + Name + "' is complete.", this, ParentCollection.Name, Name, nameof(B17CAnalysis)));
            }
        }

        /// <summary>
        /// Selects the warning text shown when the point estimate succeeded but uncertainty
        /// quantification produced no results.
        /// </summary>
        /// <returns>
        /// The model's <see cref="ModelAnalyses.Bulletin17CAnalysis.UncertaintyDiagnosticMessage"/>
        /// when one was recorded; otherwise the generic covariance failure text.
        /// </returns>
        internal string GetUncertaintyFailureMessageText()
        {
            return string.IsNullOrEmpty(_innerAnalysis.UncertaintyDiagnosticMessage)
                ? "Uncertainty quantification failed — the covariance matrix is not positive-definite. The point estimate is still valid but confidence intervals could not be computed. Consider using a different distribution or the Bootstrap uncertainty method."
                : _innerAnalysis.UncertaintyDiagnosticMessage;
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
        /// Gets a distribution for a given output index from the analysis results.
        /// Delegates to the inner analysis.
        /// </summary>
        /// <param name="index">The output index.</param>
        /// <returns>The distribution with parameters from the specified output index, or <c>null</c> if not estimated.</returns>
        public UnivariateDistributionBase GetDistribution(int index)
        {
            return _innerAnalysis.GetDistribution(index);
        }

        /// <summary>
        /// Gets the point estimate distribution based on the selected point estimator.
        /// Delegates to the inner analysis.
        /// </summary>
        /// <returns>The point estimate distribution, or <c>null</c> if not estimated.</returns>
        public UnivariateDistributionBase GetPointEstimateDistribution()
        {
            return _innerAnalysis.GetPointEstimateDistribution();
        }

        /// <inheritdoc/>
        public IUnivariateModel GetMarginalModel()
        {
            return Bulletin17CDistribution;
        }

        #endregion

        #region Distribution Undo

        /// <summary>
        /// Records an undo action for a change to the Bulletin17CDistribution.
        /// Compares the current XElement snapshot against the previous snapshot;
        /// if they differ, a DelegateAction is recorded that restores from either snapshot.
        /// </summary>
        /// <param name="propertyName">The name of the property that triggered the change.</param>
        private void RecordDistributionUndo(string propertyName)
        {
            var undoManager = IsUndoEnabled ? UndoManager : null;
            if (undoManager == null || undoManager.IsExecutingAction) return;
            if (_distributionSnapshot == null) return;

            var currentSnapshot = _innerAnalysis.Bulletin17CDistribution.ToXElement();
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
        /// Rebuilds the inner analysis from an XElement snapshot of the Bulletin17CDistribution,
        /// preserving analysis configuration (BayesianAnalysis settings, ProbabilityOrdinates).
        /// Used by the undo/redo system.
        /// </summary>
        /// <param name="snapshot">The XElement snapshot to restore from.</param>
        private void RestoreDistributionFromSnapshot(XElement snapshot)
        {
            // 1. Disconnect from current inner analysis
            UnsubscribeInnerAnalysis();

            // 2. Preserve analysis config
            var df = _innerAnalysis.Bulletin17CDistribution.DataFrame;
            var analysisXml = _innerAnalysis.ToXElement();

            // 3. Create new distribution from snapshot.
            //    DataFrame may be null if InputData was undone back to null â€” use default constructor
            //    and restore from XElement without data-dependent parameter initialization.
            var newDist = df != null
                ? new Bulletin17CDistribution(df, snapshot)
                : new Bulletin17CDistribution(snapshot);

            // 4. Create new analysis with restored distribution + preserved config
            _innerAnalysis = new ModelAnalyses.Bulletin17CAnalysis(newDist, analysisXml);

            // 5. Reconnect subscriptions and rebuild all bridges

            // Re-apply Bayesian simulation defaults for the restored distribution's parameter count
            if (_innerAnalysis.BayesianAnalysis.UseSimulationDefaults)
                _innerAnalysis.BayesianAnalysis.SetDefaultSimulationOptions();
            if (_innerAnalysis.BayesianAnalysis.UseAdvancedSimulationDefaults)
                _innerAnalysis.BayesianAnalysis.SetDefaultAdvancedSimulationOptions();

            SubscribeInnerAnalysis();
            SetupBridges();

            // 6. Notify WPF bindings that all model references have changed
            RaisePropertyChange(nameof(Bulletin17CDistribution));
            RaisePropertyChange(nameof(ParameterPenalties));
            RaisePropertyChange(nameof(QuantilePenalties));
            RaisePropertyChange(nameof(BayesianAnalysis));
            RaisePropertyChange(nameof(ProbabilityOrdinates));
            RaisePropertyChange(nameof(IsEstimated));
            RaisePropertyChange(nameof(AnalysisResults));

            // 7. Revalidate and mark dirty
            SetIsValid();
            SetIsDirty(true);
        }

        #endregion

        #region Plot Factory Methods

        /// <summary>
        /// Applies the default plot style used across all element-owned plots.
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
        /// <returns>A new <see cref="Plot"/> configured for frequency analysis display.</returns>
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
        /// Converts a hex color string (e.g., "#8CFFFFFF") to a <see cref="Color"/>.
        /// </summary>
        private static Color ColorFromHex(string hex)
        {
            return (Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
        }

        /// <summary>
        /// Deserializes plot settings from a SQLite column into a live <see cref="Plot"/> object.
        /// </summary>
        /// <param name="dtView">The data table view to read from.</param>
        /// <param name="rowIndex">The row index in the table.</param>
        /// <param name="columnName">The column name containing serialized plot XML.</param>
        /// <param name="plot">The target plot to deserialize into.</param>
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

        /// <summary>
        /// Serializes a plot's settings into a SQLite column.
        /// </summary>
        /// <param name="dtView">The data table view to write to.</param>
        /// <param name="rowIndex">The row index in the table.</param>
        /// <param name="columnName">The column name to write to.</param>
        /// <param name="plot">The plot to serialize.</param>
        private static void SerializePlotSetting(DataTableView dtView, int rowIndex, string columnName, Plot plot)
        {
            dtView.EditCell(rowIndex, columnName, plot != null ? PlotSerializer.ToXElement(plot).ToString() : "");
        }

        #endregion

        #region Undo Bridge Management

        /// <summary>
        /// Creates undo bridges for all undoable collections. Disposes any existing bridges before creating new ones.
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

            // Distribution undo â€” capture baseline snapshot for XElement comparison.
            _distributionSnapshot = _innerAnalysis?.Bulletin17CDistribution?.ToXElement();

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
        /// Disposes all undo bridges and sets their references to null.
        /// </summary>
        private void DisposeBridges()
        {
            UndoManager.StateChanged -= UndoManager_StateChanged;

            _probabilityOrdinatesBridge?.Dispose();
            _probabilityOrdinatesBridge = null;

            _frequencyPlotUndo?.Dispose();
            _frequencyPlotUndo = null;

            _bayesianController?.DisposeBridges();
        }

        /// <summary>
        /// Suspends all plot undo bridges so that programmatic plot changes (axis binding,
        /// series population, plot settings restoration) do not create undo entries.
        /// Returns an <see cref="IDisposable"/> that resumes recording when disposed.
        /// </summary>
        public IDisposable SuspendPlotBridges()
        {
            var suspensions = new List<IDisposable>();

            // Suspend element-level plot bridges
            if (_frequencyPlotUndo != null) suspensions.Add(_frequencyPlotUndo.SuspendRecording());

            // Suspend Bayesian diagnostic plot bridges
            if (_bayesianController != null) suspensions.Add(_bayesianController.SuspendPlotBridges());

            return new AggregateDisposable(suspensions);
        }

        /// <summary>
        /// Rebuilds series and annotation bridges for the specified plot after a bulk series update.
        /// Call this after populating series inside a <see cref="SuspendPlotBridges"/> block.
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
