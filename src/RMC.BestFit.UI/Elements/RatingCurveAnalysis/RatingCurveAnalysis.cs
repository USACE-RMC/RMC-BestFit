using DatabaseManager;
using FrameworkInterfaces;
using FrameworkInterfaces.Messaging;
using FrameworkInterfaces.Undo;
using FrameworkInterfaces.Undo.Actions;
using Numerics;
using Numerics.Data.Statistics;
using Numerics.Distributions;
using Numerics.Sampling.MCMC;
using Numerics.Utilities;
using OxyPlot.Wpf;
using OxyPlot.Wpf.Serialization;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using System;
using System.Collections.Generic;
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
    /// Rating curve analysis UI wrapper.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    /// This class wraps a <see cref="ModelAnalyses.RatingCurveAnalysis"/> from the model library,
    /// delegating all computation to the inner analysis while providing UI-specific concerns:
    /// time series element reference management, SQLite persistence, messenger-based validation,
    /// and plot settings.
    /// </para>
    /// </remarks>
    [Category("General")]
    [DisplayName("Rating Curve Analysis")]
    [Description("Fits a piecewise power-law rating curve relating stage to discharge using Bayesian MCMC. Supports a configurable number of segments (1-3) joined at user-specified or estimated breakpoints, with log-normal multiplicative error. Requires both a stage and a discharge time series with overlapping measurement dates; only aligned (date, stage, discharge) observations contribute to the fit.")]
    [Browsable(true)]
    public class RatingCurveAnalysis : ElementBase, IAnalysisElement
    {

        #region Construction

        /// <summary>
        /// Constructs a new rating curve analysis instance.
        /// </summary>
        /// <param name="name">The name of the rating curve analysis element.</param>
        /// <param name="parentCollection">The parent collection that contains this element.</param>
        /// <param name="openFromFile">If true, opens the element from disk upon construction. Default is false.</param>
        public RatingCurveAnalysis(string name, IElementCollection parentCollection, bool openFromFile = false) : base(name, parentCollection)
        {
            IsUndoEnabled = false;
            try
            {
                _nameOnDisk = name;
                _creationDate = DateTime.Now;
                _lastModified = DateTime.Now;

                // Create plot objects
                _ratingCurvePlot = CreateDefaultRatingCurvePlot();
                _residualPlot = CreateDefaultResidualPlot();
                _residualHistogramPlot = CreateDefaultResidualHistogramPlot();
                _residualQQPlot = CreateDefaultResidualQQPlot();
                _bayesianController = new BayesianController();

                // Create the inner model analysis
                var rc = new RatingCurve();
                _innerAnalysis = new ModelAnalyses.RatingCurveAnalysis(rc);

                // Subscribe to inner analysis property changes
                _innerAnalysis.PropertyChanged += InnerAnalysis_PropertyChanged;

                // Add UI-only messages
                _descriptionMsg = new BasicMessageItem(MessageType.Message, "The rating curve analysis does not have a description.", this, ParentCollection.Name, Name, nameof(Description), "RCA-MSG-001");
                _stageDataInValidMsg = new BasicMessageItem(MessageType.Error, "The selected stage time series is invalid.", this, ParentCollection.Name, Name, nameof(StageData), "RCA-ERR-006");
                _dischargeDataInValidMsg = new BasicMessageItem(MessageType.Error, "The selected discharge time series is invalid.", this, ParentCollection.Name, Name, nameof(DischargeData), "RCA-ERR-008");
                _seriesEqualMsg = new BasicMessageItem(MessageType.Error, "Stage and discharge time series must be different.", this, ParentCollection.Name, Name, nameof(StageData), "RCA-ERR-009");
                _partialDateOverlapMsg = new BasicMessageItem(MessageType.Warning, "Stage and discharge time series do not perfectly overlap by date. Unpaired dates are excluded from the fit.", this, ParentCollection.Name, Name, nameof(StageData), "RCA-WRN-011");
                _insufficientOverlapMsg = new BasicMessageItem(MessageType.Error, $"Stage and discharge series share fewer than {RMC.BestFit.Models.RatingCurve.MinimumAlignedObservations} dates in common. Rating curve fitting requires at least that many aligned (date, stage, discharge) observations.", this, ParentCollection.Name, Name, nameof(StageData), "RCA-ERR-010");

                // "Stage data null" and "Discharge data null" messages are surfaced by the
                // model-layer RatingCurve.Validate() routed through _validationAdapter.

                _messages = new List<BasicMessageItem>()
                {   _descriptionMsg,
                    _stageDataInValidMsg,
                    _dischargeDataInValidMsg,
                    _seriesEqualMsg,
                    _partialDateOverlapMsg,
                    _insufficientOverlapMsg
                };

                // Create messenger and validation adapter
                _messenger = Messenger.GetInstance();
                _validationAdapter = new ValidationMessageAdapter(_messenger, this, ParentCollection.Name, "RCA");
                _messenger.Add(_descriptionMsg);

                if (openFromFile == true) Open();
                _nameValid = ValidateName(BestFitProject.InvalidNameCharacters, 50, "RCA");

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
        [Description("Unique label identifying this rating curve analysis; max 50 characters.")]
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
                    _nameValid = ValidateName(BestFitProject.InvalidNameCharacters, 50, "RCA");
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
        [Description("Free-text annotation describing this rating curve analysis.")]
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
        /// Gets the name as it appears on disk.
        /// </summary>
        public override string NameOnDisk => _nameOnDisk;

        /// <summary>
        /// Gets the element image as an ImageSource from the theme-aware resource dictionary.
        /// </summary>
        public override System.Windows.Media.ImageSource ElementImage => System.Windows.Application.Current?.TryFindResource("RatingCurveAnalysisIcon") as System.Windows.Media.ImageSource;

        /// <summary>
        /// Gets the resource dictionary key for the element's theme-aware vector icon.
        /// The project explorer uses <see cref="System.Windows.FrameworkElement.SetResourceReference"/>
        /// with this key so the icon updates automatically when the theme changes.
        /// </summary>
        public string ElementImageResourceKey => "RatingCurveAnalysisIcon";

        /// <summary>
        /// Gets a value indicating whether the element can be copied from an external application.
        /// </summary>
        public override bool CanCopyFromExternal => false;

        /// <summary>
        /// Gets a value indicating whether the element is valid and ready for analysis.
        /// </summary>
        public override bool IsValid
        {
            get { return _isValid; }
        }

        #endregion

        #region Fields

        /// <summary>
        /// The inner model library analysis that performs all computation.
        /// </summary>
        private ModelAnalyses.RatingCurveAnalysis _innerAnalysis;

        /// <inheritdoc/>
        public ModelAnalyses.IAnalysis InnerAnalysis => _innerAnalysis;

        /// <summary>
        /// The validation message adapter that bridges model validation to UI messaging.
        /// </summary>
        private ValidationMessageAdapter _validationAdapter;

        /// <summary>
        /// List of all message items for the element.
        /// </summary>
        private List<BasicMessageItem> _messages;
        /// <summary>
        /// The messenger instance for posting validation and status messages.
        /// </summary>
        private Messenger _messenger;
        /// <summary>
        /// Message item for missing element description.
        /// </summary>
        private BasicMessageItem _descriptionMsg;
        /// <summary>
        /// Message item for invalid stage data.
        /// </summary>
        private BasicMessageItem _stageDataInValidMsg;
        /// <summary>
        /// Message item for invalid discharge data.
        /// </summary>
        private BasicMessageItem _dischargeDataInValidMsg;
        /// <summary>
        /// Message item for when stage and discharge time series are the same.
        /// </summary>
        private BasicMessageItem _seriesEqualMsg;
        /// <summary>
        /// Warning message item raised when stage and discharge time series are valid
        /// for fitting but do not fully overlap by date.
        /// </summary>
        private BasicMessageItem _partialDateOverlapMsg;

        /// <summary>
        /// Error message item raised when stage and discharge time series share fewer than
        /// <see cref="RMC.BestFit.Models.RatingCurve.MinimumAlignedObservations"/> common
        /// dates. Below that floor, rating curve fitting cannot proceed.
        /// </summary>
        private BasicMessageItem _insufficientOverlapMsg;

        /// <summary>
        /// Indicates whether the element name is valid.
        /// </summary>
        private bool _nameValid = false;
        /// <summary>
        /// Indicates whether the stage data is valid.
        /// </summary>
        private bool _stageDataValid = false;
        /// <summary>
        /// Indicates whether the discharge data is valid.
        /// </summary>
        private bool _dischargeDataValid = false;

        /// <summary>
        /// Tracks whether this element was migrated from a legacy (v1) project file during <see cref="Open()"/>.
        /// When true, the constructor and <see cref="Open()"/> mark the element dirty so the user is prompted
        /// to save the upgraded schema.
        /// </summary>
        private bool openedFromV1 = false;

        /// <summary>
        /// Reference to the legacy-migration warning message added to the messenger during
        /// <see cref="Open()"/> when a pre-v2.0 project file with an incompatible parameter
        /// layout is detected. Held so that <see cref="RunAsync"/> can remove it from the
        /// messenger once the user has successfully re-run the Bayesian analysis and
        /// refreshed the results.
        /// </summary>
        private BasicMessageItem _legacyMigrationMsg = null;

        /// <summary>
        /// The stage time series element used for the rating curve analysis.
        /// </summary>
        private TimeSeriesElement _stageData;
        /// <summary>
        /// The discharge time series element used for the rating curve analysis.
        /// </summary>
        private TimeSeriesElement _dischargeData;

        // Plot settings
        /// <summary>
        /// The rating curve plot.
        /// </summary>
        private Plot _ratingCurvePlot;
        /// <summary>
        /// The residual plot.
        /// </summary>
        private Plot _residualPlot;
        /// <summary>
        /// The residual histogram plot.
        /// </summary>
        private Plot _residualHistogramPlot;
        /// <summary>
        /// The residual QQ plot.
        /// </summary>
        private Plot _residualQQPlot;
        /// <summary>
        /// Undo manager for the rating curve plot.
        /// </summary>
        private PlotUndoManager _ratingCurvePlotUndo;
        /// <summary>
        /// Undo manager for the residual plot.
        /// </summary>
        private PlotUndoManager _residualPlotUndo;
        /// <summary>
        /// Undo manager for the residual histogram plot.
        /// </summary>
        private PlotUndoManager _residualHistogramPlotUndo;
        /// <summary>
        /// Undo manager for the residual QQ plot.
        /// </summary>
        private PlotUndoManager _residualQQPlotUndo;
        /// <summary>
        /// Manages the 7 standard Bayesian MCMC diagnostic plots.
        /// </summary>
        private BayesianController _bayesianController;

        /// <summary>
        /// Rolling XElement baseline of the RatingCurve model state.
        /// </summary>
        private XElement _modelSnapshot;

        /// <summary>
        /// Property names that trigger snapshot comparison.
        /// </summary>
        private static readonly HashSet<string> ModelUndoProperties = new()
        {
            "Parameters", "UseJeffreysRuleForScale", "NumberOfSegments"
        };

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the selected stage time series element.
        /// </summary>
        [Category("General")]
        [DisplayName("Stage Data")]
        [Description("The stage time series data. The stage and discharge time series data must have overlapping measurement dates.")]
        [Browsable(true)]
        public TimeSeriesElement StageData
        {
            get { return _stageData; }
            set
            {
                if (_stageData == value) return;
                var old = _stageData;

                if (_stageData != null)
                {
                    _stageData.PropertyChanged -= StageSeriesElementChanged;
                    _stageData.Deleted -= OnStageDataDeleted;
                }

                _stageData = value;

                if (_stageData != null)
                {
                    _stageData.PropertyChanged += StageSeriesElementChanged;
                    _stageData.Deleted += OnStageDataDeleted;
                }

                // Sync stage data to inner analysis model
                if (_innerAnalysis != null && _innerAnalysis.RatingCurve != null)
                {
                    if (_stageData != null && _stageData.TimeSeries != null)
                        _innerAnalysis.RatingCurve.StageData = _stageData.TimeSeries;
                    else
                        _innerAnalysis.RatingCurve.StageData = null;
                }

                SetIsValid();
                if (!UndoManager.IsExecutingAction) ClearResults();
                RecordPropertyChange(nameof(StageData), old, value);
            }
        }

        /// <summary>
        /// Gets or sets the selected discharge time series element.
        /// </summary>
        [Category("General")]
        [DisplayName("Discharge Data")]
        [Description("The discharge time series data. The stage and discharge time series data must have overlapping measurement dates.")]
        [Browsable(true)]
        public TimeSeriesElement DischargeData
        {
            get { return _dischargeData; }
            set
            {
                if (_dischargeData == value) return;
                var old = _dischargeData;

                if (_dischargeData != null)
                {
                    _dischargeData.PropertyChanged -= DischargeSeriesElementChanged;
                    _dischargeData.Deleted -= OnDischargeDataDeleted;
                }

                _dischargeData = value;

                if (_dischargeData != null)
                {
                    _dischargeData.PropertyChanged += DischargeSeriesElementChanged;
                    _dischargeData.Deleted += OnDischargeDataDeleted;
                }

                // Sync discharge data to inner analysis model
                if (_innerAnalysis != null && _innerAnalysis.RatingCurve != null)
                {
                    if (_dischargeData != null && _dischargeData.TimeSeries != null)
                        _innerAnalysis.RatingCurve.DischargeData = _dischargeData.TimeSeries;
                    else
                        _innerAnalysis.RatingCurve.DischargeData = null;
                }

                SetIsValid();
                if (!UndoManager.IsExecutingAction) ClearResults();
                RecordPropertyChange(nameof(DischargeData), old, value);
            }
        }

        /// <summary>
        /// Gets the rating curve model (delegates to inner analysis).
        /// </summary>
        public RatingCurve RatingCurve
        {
            get { return _innerAnalysis.RatingCurve; }
        }

        /// <summary>
        /// Gets the Bayesian Analysis object (delegates to inner analysis).
        /// </summary>
        public BayesianAnalysis BayesianAnalysis
        {
            get { return _innerAnalysis.BayesianAnalysis; }
        }

        /// <summary>
        /// Gets or sets the minimum stage value to evaluate in the output rating curve.
        /// </summary>
        [Category("Output")]
        [DisplayName("Minimum Stage")]
        [Description("The minimum stage to evaluate.")]
        [Browsable(true)]
        public double MinStage
        {
            get { return _innerAnalysis.MinStage; }
            set
            {
                if (_innerAnalysis.MinStage != value)
                {
                    var old = _innerAnalysis.MinStage;
                    _innerAnalysis.MinStage = value;
                    SetIsValid();
                    RecordPropertyChange(nameof(MinStage), old, value);
                }
            }
        }

        /// <summary>
        /// Gets or sets the maximum stage value to evaluate in the output rating curve.
        /// </summary>
        [Category("Output")]
        [DisplayName("Maximum Stage")]
        [Description("The maximum stage to evaluate.")]
        [Browsable(true)]
        public double MaxStage
        {
            get { return _innerAnalysis.MaxStage; }
            set
            {
                if (_innerAnalysis.MaxStage != value)
                {
                    var old = _innerAnalysis.MaxStage;
                    _innerAnalysis.MaxStage = value;
                    SetIsValid();
                    RecordPropertyChange(nameof(MaxStage), old, value);
                }
            }
        }

        /// <summary>
        /// Gets or sets the number of stage bins used for constructing the output rating curve.
        /// </summary>
        [Category("Output")]
        [DisplayName("Stage Bins")]
        [Description("The number of stage bins used for constructing the rating curve.")]
        [Browsable(true)]
        public int StageBins
        {
            get { return _innerAnalysis.StageBins; }
            set
            {
                if (_innerAnalysis.StageBins != value)
                {
                    var old = _innerAnalysis.StageBins;
                    _innerAnalysis.StageBins = value;
                    SetIsValid();
                    RecordPropertyChange(nameof(StageBins), old, value);
                }
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether default stage bins are automatically calculated based on the input data range.
        /// </summary>
        [Category("Output")]
        [DisplayName("Use Default Stage Bins")]
        [Description("Specifies whether default stage bins are used for rating curve construction.")]
        [Browsable(true)]
        public bool UseDefaultStageBins
        {
            get { return _innerAnalysis.UseDefaultStageBins; }
            set
            {
                if (_innerAnalysis.UseDefaultStageBins != value)
                {
                    var old = _innerAnalysis.UseDefaultStageBins;
                    _innerAnalysis.UseDefaultStageBins = value;
                    RecordPropertyChange(nameof(UseDefaultStageBins), old, value);
                    RaisePropertyChange(nameof(MinStage));
                    RaisePropertyChange(nameof(MaxStage));
                    RaisePropertyChange(nameof(StageBins));
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether the analysis has been successfully estimated.
        /// </summary>
        public bool IsEstimated
        {
            get { return _innerAnalysis.IsEstimated; }
        }

        /// <summary>
        /// Gets the uncertainty analysis results (delegates to inner analysis).
        /// </summary>
        public UncertaintyAnalysisResults AnalysisResults
        {
            get { return _innerAnalysis.AnalysisResults; }
        }

        /// <summary>
        /// Gets the rating curve plot.
        /// </summary>
        public Plot RatingCurvePlot => _ratingCurvePlot;
        /// <summary>
        /// Gets the residual plot.
        /// </summary>
        public Plot ResidualPlot => _residualPlot;
        /// <summary>
        /// Gets the residual histogram plot.
        /// </summary>
        public Plot ResidualHistogramPlot => _residualHistogramPlot;
        /// <summary>
        /// Gets the residual QQ plot.
        /// </summary>
        public Plot ResidualQQPlot => _residualQQPlot;
        /// <summary>
        /// Gets the Bayesian MCMC diagnostic plot settings (7 shared diagnostic plots).
        /// </summary>
        public BayesianController BayesianPlots => _bayesianController;

        #endregion

        #endregion

        #region Methods

        /// <summary>
        /// Handles property changes forwarded from the inner model library analysis.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Property changed event arguments.</param>
        private void InnerAnalysis_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (ModelUndoProperties.Contains(e.PropertyName))
            {
                RecordModelUndo(e.PropertyName);
            }

            if (e.PropertyName == nameof(ModelAnalyses.RatingCurveAnalysis.IsEstimated) ||
                e.PropertyName == nameof(ModelAnalyses.RatingCurveAnalysis.AnalysisResults) ||
                e.PropertyName == nameof(ModelAnalyses.RatingCurveAnalysis.RatingCurve) ||
                e.PropertyName == nameof(ModelAnalyses.RatingCurveAnalysis.BayesianAnalysis))
            {
                SetIsValid();
                RaisePropertyChange(e.PropertyName);
            }
            else if (e.PropertyName == nameof(ModelAnalyses.RatingCurveAnalysis.MinStage) ||
                     e.PropertyName == nameof(ModelAnalyses.RatingCurveAnalysis.MaxStage) ||
                     e.PropertyName == nameof(ModelAnalyses.RatingCurveAnalysis.StageBins))
            {
                // Stage bin changes from inner (e.g. SetDefaultStageBins)
                RaisePropertyChange(e.PropertyName);
            }
            else
            {
                // Forward all other property changes (e.g., from the model or BayesianAnalysis)
                RaisePropertyChange(e.PropertyName);
            }
        }

        /// <summary>
        /// Event handler called when the stage time series element properties change.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event arguments containing the changed property name.</param>
        private void StageSeriesElementChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_stageData?.TimeSeries == null) return;
            if (_stageData.TimeSeries.SuppressCollectionChanged == false)
            {
                if (e.PropertyName == nameof(TimeSeriesElement.TimeSeries))
                {
                    if (!UndoManager.IsExecutingAction) ClearResults();
                }
                SetIsValid();
                RaisePropertyChange(e.PropertyName);
            }
        }

        /// <summary>
        /// Event handler called when the discharge time series element properties change.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event arguments containing the changed property name.</param>
        private void DischargeSeriesElementChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_dischargeData?.TimeSeries == null) return;
            if (_dischargeData.TimeSeries.SuppressCollectionChanged == false)
            {
                if (e.PropertyName == nameof(TimeSeriesElement.TimeSeries))
                {
                    if (!UndoManager.IsExecutingAction) ClearResults();
                }
                SetIsValid();
                RaisePropertyChange(e.PropertyName);
            }
        }

        /// <summary>
        /// Handles deletion of the stage <see cref="TimeSeriesElement"/> from its parent collection.
        /// Clears the reference with undo recording disabled so that pressing Undo cannot
        /// resurrect the now-deleted element. Pre-existing undo history is preserved.
        /// </summary>
        private void OnStageDataDeleted(IElement element)
        {
            var wasUndoEnabled = IsUndoEnabled;
            IsUndoEnabled = false;
            try { StageData = null; }
            finally { IsUndoEnabled = wasUndoEnabled; }
        }

        /// <summary>
        /// Handles deletion of the discharge <see cref="TimeSeriesElement"/> from its parent collection.
        /// Clears the reference with undo recording disabled so that pressing Undo cannot
        /// resurrect the now-deleted element. Pre-existing undo history is preserved.
        /// </summary>
        private void OnDischargeDataDeleted(IElement element)
        {
            var wasUndoEnabled = IsUndoEnabled;
            IsUndoEnabled = false;
            try { DischargeData = null; }
            finally { IsUndoEnabled = wasUndoEnabled; }
        }

        /// <summary>
        /// Validates that the stage and discharge time series are properly configured and have overlapping dates.
        /// </summary>
        private void ValidateTimeSeries()
        {
            // Check if stage series is valid
            // Track local validity flags for IsValid. Diagnostic messages for "stage data null",
            // "discharge data null", and "insufficient overlap" are surfaced by the model-layer
            // RatingCurve.Validate() routed through _validationAdapter inside SetIsValid().
            // The UI handles only the messages without a model-layer equivalent: invalid upstream
            // TimeSeriesElement, identical-series, and length-mismatch warning.
            _stageDataValid = true;
            _messenger.Remove(_stageDataInValidMsg);
            if (_stageData == null)
            {
                _stageDataValid = false;
            }
            if (_stageData != null && (_stageData.IsValid == false))
            {
                _stageDataValid = false;
                _stageDataInValidMsg.Description = "The selected stage time series is invalid.";
                _messenger.Add(_stageDataInValidMsg);
            }

            // Check if discharge data is valid
            _dischargeDataValid = true;
            _messenger.Remove(_dischargeDataInValidMsg);
            if (_dischargeData == null)
            {
                _dischargeDataValid = false;
            }
            if (_dischargeData != null && (_dischargeData.IsValid == false))
            {
                _dischargeDataValid = false;
                _dischargeDataInValidMsg.Description = "The selected discharge time series is invalid.";
                _messenger.Add(_dischargeDataInValidMsg);
            }

            // Check if marginals are equal
            _messenger.Remove(_seriesEqualMsg);
            if (_stageData != null && _dischargeData != null && _stageData.Name == _dischargeData.Name)
            {
                _stageDataValid = false;
                _dischargeDataValid = false;
                _messenger.Add(_seriesEqualMsg);
            }

            // Insufficient-overlap error and partial-overlap warning both use the
            // model alignment helper so UI messages match the observations used by the fit.
            _messenger.Remove(_partialDateOverlapMsg);
            _messenger.Remove(_insufficientOverlapMsg);
            if (_stageData != null && _dischargeData != null && _stageData.TimeSeries != null && _dischargeData.TimeSeries != null)
            {
                var counts = RatingCurve.GetDataAlignmentCounts();
                if (counts.PairedCount < RMC.BestFit.Models.RatingCurve.MinimumAlignedObservations)
                {
                    _stageDataValid = false;
                    _dischargeDataValid = false;
                    _messenger.Add(_insufficientOverlapMsg);
                }
                else if (counts.PairedCount < counts.StageCount || counts.PairedCount < counts.DischargeCount)
                {
                    _partialDateOverlapMsg.Description =
                        $"Stage and discharge time series do not perfectly overlap by date. The fit will use {counts.PairedCount} aligned observations ({counts.StageCount} stage, {counts.DischargeCount} discharge); unpaired dates are excluded.";
                    _messenger.Add(_partialDateOverlapMsg);
                }
            }
        }

        /// <summary>
        /// Returns the date-aligned (stage, discharge) observations that the rating
        /// curve fit will actually use. Observations whose <see cref="DateTime"/>
        /// index is present in only one series are excluded.
        /// </summary>
        /// <returns>
        /// Read-only list of <c>(Date, Stage, Discharge)</c> tuples sorted by
        /// <c>Date</c>. Empty when either series is <see langword="null"/>.
        /// </returns>
        /// <remarks>
        /// Delegates to the inner <see cref="RMC.BestFit.Models.RatingCurve"/>'s
        /// <c>GetAlignedObservations</c> helper so the App's plot loops and any
        /// external caller see exactly the same aligned data that the likelihood
        /// iterates.
        /// </remarks>
        public IReadOnlyList<(DateTime Date, double Stage, double Discharge)> GetAlignedObservations()
        {
            var innerRc = _innerAnalysis?.RatingCurve;
            return innerRc == null
                ? Array.Empty<(DateTime, double, double)>()
                : innerRc.GetAlignedObservations();
        }

        /// <summary>
        /// Updates the IsValid property based on the current validation state of all analysis components.
        /// </summary>
        private void SetIsValid()
        {
            ValidateTimeSeries();

            bool uiValid = true;
            if (_nameValid == false) uiValid = false;
            if (_stageDataValid == false) uiValid = false;
            if (_dischargeDataValid == false) uiValid = false;

            // Sync model validation messages to UI
            bool modelValid = _validationAdapter.SyncValidation(_innerAnalysis.Validate(), Name);

            bool valid = uiValid && modelValid;

            if (valid != _isValid)
            {
                _isValid = valid;
                RaisePropertyChange(nameof(IsValid));
            }
        }

        /// <summary>
        /// Gets the required columns for the SQLite database table.
        /// </summary>
        private static Dictionary<string, Type> RequiredColumns { get; } = new Dictionary<string, Type>() {
            { nameof(Name), typeof(string) },
            { nameof(Description), typeof(string) },
            { nameof(CreationDate), typeof(string) },
            { nameof(LastModified), typeof(string) },
            { nameof(StageData), typeof(string) },
            { nameof(DischargeData), typeof(string) },
            { nameof(MinStage), typeof(double) },
            { nameof(MaxStage), typeof(double) },
            { nameof(StageBins), typeof(int) },
            { nameof(UseDefaultStageBins), typeof(bool) },
            { nameof(RatingCurve), typeof(string) },
            { nameof(BayesianAnalysis), typeof(string) },
            { nameof(MCMCResults), typeof(byte[]) },
            { nameof(AnalysisResults), typeof(string) },
            { "AnalysisXml", typeof(string) },
            { "RatingCurvePlotSettings", typeof(string) },
            { "ResidualPlotSettings", typeof(string) },
            { "ResidualHistogramPlotSettings", typeof(string) },
            { "ResidualQQPlotSettings", typeof(string) },
            { "KernelDensityPlotSettings", typeof(string) },
            { "HistogramPlotSettings", typeof(string) },
            { "BivariatePlotSettings", typeof(string) },
            { "MeanLikelihoodPlotSettings", typeof(string) },
            { "AutocorrelationPlotSettings", typeof(string) },
            { "MarkovChainTracesPlotSettings", typeof(string) },
            { "InfluenceDiagnosticsPlotSettings", typeof(string) },
            { "MCMCReport", typeof(string) } };

        /// <summary>
        /// Creates the SQLite database table for storing rating curve analysis data.
        /// </summary>
        /// <param name="sqlite">The SQLite manager instance.</param>
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
        /// Opens the element from the project database file on disk.
        /// </summary>
        public override void Open()
        {
            Open(new SQLiteManager(ParentCollection.ParentProject.FullFileName));
        }

        /// <summary>
        /// Opens the element from the specified SQLite database.
        /// </summary>
        /// <param name="sqlite">The SQLite manager instance connected to the project database.</param>
        public void Open(SQLiteManager sqlite)
        {
            var wasUndoEnabled = IsUndoEnabled;
            IsUndoEnabled = false;
            try
            {
            openedFromV1 = false;
            _messenger.Clear(this);
            _validationAdapter.ClearAll();
            var wasOpen = sqlite.DataBaseOpen;
            if (wasOpen == false) sqlite.Open();

            // open element
            var dtView = sqlite.GetTableManager(ParentCollection.Name);
            int rowIndex = dtView.SearchColumn(0, dtView.NumberOfRows - 1, "Name", NameOnDisk, true, true);
            if (rowIndex != -1)
            {
                // Use backing fields during deserialization to avoid repeated SetIsValid() and ClearResults() calls.
                // The StageData/DischargeData setters trigger SetIsValid(), ClearResults(), and sync the inner RatingCurve;
                // calling them here would prematurely clear results before the model XElement and MCMC bytes have been loaded.
                // A single SetIsValid() call at the end of Open() is sufficient; the inner analysis is reconstructed below.
                if (dtView.ColumnNames.Contains(nameof(Name)))
                {
                    _name = dtView.GetCell(nameof(Name), rowIndex).ToString();
                    foreach (var item in _messages) item.SourceName = _name;
                    _nameValid = ValidateName(BestFitProject.InvalidNameCharacters, 50, "RCA");
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

                // Get stage time series — use backing field to avoid SetIsValid(), ClearResults(),
                // and premature inner-analysis sync (the inner analysis is reconstructed below).
                if (dtView.ColumnNames.Contains(nameof(StageData)))
                {
                    var elementName = dtView.GetCell(nameof(StageData), rowIndex).ToString();
                    foreach (IElementCollection collection in ParentCollection.ParentProject.ElementCollections)
                    {
                        if (collection.GetType() == typeof(TimeSeriesCollection))
                        {
                            foreach (IElement element in collection)
                            {
                                if (element.Name == elementName && element.GetType() == typeof(TimeSeriesElement))
                                {
                                    _stageData = (TimeSeriesElement)element;
                                    _stageData.PropertyChanged += StageSeriesElementChanged;
                                    _stageData.Deleted += OnStageDataDeleted;
                                    _stageDataValid = _stageData.IsValid;
                                    if (!_stageDataValid)
                                        _messenger.Add(_stageDataInValidMsg);
                                    else
                                        _messenger.Remove(_stageDataInValidMsg);
                                    break;
                                }
                            }
                        }
                    }
                }
                // Get discharge time series — same backing-field pattern as StageData above.
                if (dtView.ColumnNames.Contains(nameof(DischargeData)))
                {
                    var elementName = dtView.GetCell(nameof(DischargeData), rowIndex).ToString();
                    foreach (IElementCollection collection in ParentCollection.ParentProject.ElementCollections)
                    {
                        if (collection.GetType() == typeof(TimeSeriesCollection))
                        {
                            foreach (IElement element in collection)
                            {
                                if (element.Name == elementName && element.GetType() == typeof(TimeSeriesElement))
                                {
                                    _dischargeData = (TimeSeriesElement)element;
                                    _dischargeData.PropertyChanged += DischargeSeriesElementChanged;
                                    _dischargeData.Deleted += OnDischargeDataDeleted;
                                    _dischargeDataValid = _dischargeData.IsValid;
                                    if (!_dischargeDataValid)
                                        _messenger.Add(_dischargeDataInValidMsg);
                                    else
                                        _messenger.Remove(_dischargeDataInValidMsg);
                                    break;
                                }
                            }
                        }
                    }
                }

                // Plot Properties
                DeserializePlotSettings(dtView, rowIndex, "RatingCurvePlotSettings", _ratingCurvePlot);
                DeserializePlotSettings(dtView, rowIndex, "ResidualPlotSettings", _residualPlot);
                DeserializePlotSettings(dtView, rowIndex, "ResidualHistogramPlotSettings", _residualHistogramPlot);
                DeserializePlotSettings(dtView, rowIndex, "ResidualQQPlotSettings", _residualQQPlot);
                _bayesianController.Deserialize(dtView, rowIndex);

                // Get model XElement
                XElement modelXElement = null;
                if (dtView.ColumnNames.Contains(nameof(RatingCurve)) && StageData != null && DischargeData != null)
                {
                    try { modelXElement = XElement.Parse(dtView.GetCell(nameof(RatingCurve), rowIndex).ToString()); }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Failed to load RatingCurve for '{Name}': {ex.Message}"); }
                }

                // Earlier rating-curve implementations saved parameter layouts that do not
                // map onto the current BaRatin addition-mode model
                // ([h₁, log₁₀α₁, β₁, h₂, log₁₀α₂, β₂, …, σ]). Legacy shapes recognized:
                //   (a) pre-v2.0 v1 additive (Coefficient (α), Location (ξ), … — different order)
                //   (b) v2.0 interim "piecewise-without-continuity" (Zero-Flow Stage (ξ) shared)
                //   (c) v2.0 interim NVE-style (fit Location (ξ2) / (ξ3) directly)
                // In every legacy case we rebuild the model with fresh defaults and drop
                // the stale parameters, MCMC samples, and uncertainty results. Projects
                // saved under the transient v2.0 piecewise-continuity era share parameter
                // names and layout with the current model and load without intervention;
                // their stored fits are reinterpreted under the new additive semantics, so
                // the user may see a different curve until they re-run the analysis.
                bool isLegacyFormat = IsLegacyRatingCurveXml(modelXElement);
                openedFromV1 = isLegacyFormat;

                MCMCResults mcmcResults = isLegacyFormat
                    ? null
                    : AnalysisPersistenceHelper.TryLoadMCMCResults(dtView, rowIndex, Name);
                XElement innerXElement = isLegacyFormat
                    ? null
                    : AnalysisPersistenceHelper.TryLoadXElement(dtView, "AnalysisXml", rowIndex, Name);

                // Get Bayesian analysis XElement — ignored for legacy files (we
                // construct a fresh BayesianAnalysis with new defaults below).
                XElement analysisXElement = null;
                if (!isLegacyFormat && dtView.ColumnNames.Contains(nameof(BayesianAnalysis)))
                {
                    var xElementString = dtView.GetCell(nameof(BayesianAnalysis), rowIndex).ToString();
                    try { analysisXElement = XElement.Parse(xElementString); }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Failed to load BayesianAnalysis for '{Name}': {ex.Message}"); }
                }

                // Read stage bin settings
                double minStage = 0;
                double maxStage = 100;
                int stageBins = 100;
                bool useDefaultStageBins = true;
                if (dtView.ColumnNames.Contains(nameof(MinStage))) double.TryParse(dtView.GetCell(nameof(MinStage), rowIndex).ToString(), out minStage);
                if (dtView.ColumnNames.Contains(nameof(MaxStage))) double.TryParse(dtView.GetCell(nameof(MaxStage), rowIndex).ToString(), out maxStage);
                if (dtView.ColumnNames.Contains(nameof(StageBins))) int.TryParse(dtView.GetCell(nameof(StageBins), rowIndex).ToString(), out stageBins);
                if (dtView.ColumnNames.Contains(nameof(UseDefaultStageBins))) bool.TryParse(dtView.GetCell(nameof(UseDefaultStageBins), rowIndex).ToString(), out useDefaultStageBins);

                // Reconstruct inner analysis from persisted data
                _innerAnalysis.PropertyChanged -= InnerAnalysis_PropertyChanged;

                UncertaintyAnalysisResults analysisResults = isLegacyFormat
                    ? null
                    : AnalysisPersistenceHelper.TryLoadAnalysisResults(dtView, nameof(AnalysisResults), rowIndex, Name);

                if (modelXElement != null && StageData != null && DischargeData != null)
                {
                    RatingCurve rc;
                    if (isLegacyFormat)
                    {
                        // Preserve segment count from the old file (clamped to 1..3)
                        // but rebuild parameters via the new data-only constructor so
                        // priors match the new layout.
                        int legacySegments = 1;
                        var segAttr = modelXElement.Attribute("NumberOfSegments");
                        if (segAttr != null && int.TryParse(segAttr.Value, out var parsedSegments))
                            legacySegments = Math.Max(1, Math.Min(3, parsedSegments));
                        rc = new RatingCurve(StageData.TimeSeries, DischargeData.TimeSeries, legacySegments);
                    }
                    else
                    {
                        // Reconstruct RatingCurve model from persisted XElement
                        rc = new RatingCurve(StageData.TimeSeries, DischargeData.TimeSeries, modelXElement);
                    }

                    if (innerXElement == null && !isLegacyFormat && analysisXElement != null)
                    {
                        // Build a combined XElement for the inner analysis constructor
                        innerXElement = new XElement("RatingCurveAnalysis",
                            new XAttribute("IsEstimated", mcmcResults != null),
                            new XAttribute(nameof(MinStage), minStage),
                            new XAttribute(nameof(MaxStage), maxStage),
                            new XAttribute(nameof(StageBins), stageBins),
                            new XAttribute(nameof(UseDefaultStageBins), useDefaultStageBins));

                        // Add Bayesian analysis element
                        innerXElement.Add(analysisXElement);

                    }

                    if (innerXElement != null)
                    {
                        _innerAnalysis = new ModelAnalyses.RatingCurveAnalysis(rc, innerXElement, mcmcResults, analysisResults);
                    }
                    else
                    {
                        _innerAnalysis = new ModelAnalyses.RatingCurveAnalysis(rc);
                        _innerAnalysis.MinStage = minStage;
                        _innerAnalysis.MaxStage = maxStage;
                        _innerAnalysis.StageBins = stageBins;
                        _innerAnalysis.UseDefaultStageBins = useDefaultStageBins;
                    }
                }
                else
                {
                    _innerAnalysis = new ModelAnalyses.RatingCurveAnalysis(new RatingCurve());
                }

                _innerAnalysis.PropertyChanged += InnerAnalysis_PropertyChanged;

                if (isLegacyFormat)
                {
                    _legacyMigrationMsg = new BasicMessageItem(
                        MessageType.Warning,
                        $"The rating curve analysis '{Name}' was created with an earlier version of RMC-BestFit whose parameter layout is not compatible with the current BaRatin addition-mode model (Le Coz et al. 2014). Previous parameter estimates, MCMC samples, and uncertainty results were discarded. Re-run the Bayesian analysis to refresh the results.",
                        this, ParentCollection.Name, Name, nameof(RatingCurveAnalysis), "RCA-WRN-LEGACY");
                    _messenger.Add(_legacyMigrationMsg);
                }
            }

            if (wasOpen == false) sqlite.Close();
            SetupBridges();
            SetIsValid();
            // Leave the project dirty when we discarded legacy content so the user
            // is prompted to save it in the new format.
            SetIsDirty(openedFromV1);
            }
            finally
            {
                IsUndoEnabled = wasUndoEnabled;
                if (wasUndoEnabled) ClearUndoHistory();
            }
        }

        /// <summary>
        /// Determines whether a serialized <c>&lt;RatingCurve&gt;</c> element was written by an
        /// earlier rating-curve implementation whose parameter layout is incompatible with
        /// the current BaRatin addition-mode model (Le Coz et al. 2014).
        /// </summary>
        /// <param name="ratingCurveElement">The <c>&lt;RatingCurve&gt;</c> XML element read from the project database, or <see langword="null"/>.</param>
        /// <returns><see langword="true"/> when the element uses one of the legacy parameter layouts; otherwise <see langword="false"/>.</returns>
        /// <remarks>
        /// Legacy formats detected:
        /// <list type="bullet">
        /// <item>
        /// <b>Pre-v2.0 v1 additive layout</b> — parameter names such as
        /// <c>Coefficient (α)</c>, <c>Exponent (β)</c>, <c>Location (ξ)</c>, <c>Sigma (σ)</c>
        /// (no per-segment subscript). Parameter order was
        /// [α₁, β₁, ξ₁, α₂, β₂, ξ₂, …, σ] — different from the current layout.
        /// </item>
        /// <item>
        /// <b>v2.0 interim "piecewise-without-continuity" layout</b> — parameter index 0
        /// named <c>Zero-Flow Stage (ξ)</c> (shared ξ across segments) with free
        /// <c>Coefficient (α2)</c>/<c>(α3)</c>. No continuity constraint.
        /// </item>
        /// <item>
        /// <b>v2.0 interim NVE-style layout</b> — free per-segment <c>Location (ξ2)</c>
        /// and <c>Location (ξ3)</c>. That layout fit locations directly; the current
        /// layout fits <c>Breakpoint (h2)</c> and <c>Breakpoint (h3)</c> as activation
        /// stages (which equal the "b" offsets in addition mode).
        /// </item>
        /// </list>
        /// The current layout has <c>Zero-Flow Stage (h₁)</c>, <c>Coefficient (α₁)</c>,
        /// <c>Exponent (β₁)</c>, then <c>Activation Stage (h₂)</c>, <c>Coefficient (α₂)</c>,
        /// <c>Exponent (β₂)</c>, etc. (Unicode subscripts). Files from the transient v2.0
        /// piecewise-continuity era use the same parameter positions with digit-form names
        /// (<c>Location (ξ1)</c>, <c>Breakpoint (h2)</c>, <c>Coefficient (α1)</c>, …); they
        /// load without a legacy flag because the positions match — their stored fit
        /// results are reinterpreted under the new additive semantics, so the displayed
        /// curve may differ from the old save. For true-legacy cases detected here, the
        /// loader rebuilds the model with default priors and discards stale parameter
        /// values, MCMC samples, and uncertainty results.
        /// </remarks>
        internal static bool IsLegacyRatingCurveXml(XElement ratingCurveElement)
        {
            if (ratingCurveElement == null) return false;
            var parameters = ratingCurveElement.Element("Parameters");
            if (parameters == null) return false;

            foreach (var p in parameters.Elements("ModelParameter"))
            {
                var name = (string)p.Attribute("Name") ?? string.Empty;

                // Pre-v2.0 v1 additive layout markers.
                if (name == "Coefficient (α)" ||
                    name == "Exponent (β)" ||
                    name == "Location (ξ)" ||
                    name == "Sigma (σ)")
                {
                    return true;
                }

                // v2.0 interim piecewise-without-continuity layout marker.
                if (name == "Zero-Flow Stage (ξ)")
                {
                    return true;
                }

                // v2.0 interim NVE-style layout marker (fit locations for k ≥ 2).
                // Current addition-mode layout uses Breakpoint (h2/h3) as both the
                // activation stage and the "b" offset (continuity is automatic).
                if (name == "Location (ξ2)" ||
                    name == "Location (ξ3)")
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Raises the preview saved event before the element is saved to disk.
        /// </summary>
        /// <param name="cancel">Output parameter that can be set to true to cancel the save operation.</param>
        public void RaisePreviewSaved(ref bool cancel)
        {
            RaisePreviewObjectSaved(this, ref cancel);
        }

        /// <summary>
        /// Saves the element to the project database file on disk.
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

            dtView.EditCell(rowIndex, nameof(Name), Name);
            dtView.EditCell(rowIndex, nameof(Description), Description);
            dtView.EditCell(rowIndex, nameof(CreationDate), FrameworkInterfaces.Utilities.Tools.DateToUniversalString(CreationDate));
            dtView.EditCell(rowIndex, nameof(LastModified), FrameworkInterfaces.Utilities.Tools.DateToUniversalString(LastModified));
            dtView.EditCell(rowIndex, nameof(StageData), StageData == null ? "" : StageData.Name);
            dtView.EditCell(rowIndex, nameof(DischargeData), DischargeData == null ? "" : DischargeData.Name);
            dtView.EditCell(rowIndex, nameof(MinStage), MinStage.ToString("G17", CultureInfo.InvariantCulture));
            dtView.EditCell(rowIndex, nameof(MaxStage), MaxStage.ToString("G17", CultureInfo.InvariantCulture));
            dtView.EditCell(rowIndex, nameof(StageBins), StageBins.ToString());
            dtView.EditCell(rowIndex, nameof(UseDefaultStageBins), UseDefaultStageBins.ToString());
            dtView.EditCell(rowIndex, nameof(RatingCurve), _innerAnalysis.RatingCurve.ToXElement().ToString());
            dtView.EditCell(rowIndex, nameof(BayesianAnalysis), _innerAnalysis.BayesianAnalysis.ToXElement().ToString());

            dtView.EditCell(rowIndex, nameof(MCMCResults),
                AnalysisPersistenceHelper.SerializeMCMCResults(_innerAnalysis.BayesianAnalysis.Results));
            dtView.EditCell(rowIndex, nameof(AnalysisResults),
                AnalysisPersistenceHelper.SerializeAnalysisResults(_innerAnalysis.AnalysisResults));
            dtView.EditCell(rowIndex, "AnalysisXml", _innerAnalysis?.ToXElement()?.ToString() ?? "");

            dtView.EditCell(rowIndex, "RatingCurvePlotSettings", _ratingCurvePlot != null ? PlotSerializer.ToXElement(_ratingCurvePlot).ToString() : "");
            dtView.EditCell(rowIndex, "ResidualPlotSettings", _residualPlot != null ? PlotSerializer.ToXElement(_residualPlot).ToString() : "");
            dtView.EditCell(rowIndex, "ResidualHistogramPlotSettings", _residualHistogramPlot != null ? PlotSerializer.ToXElement(_residualHistogramPlot).ToString() : "");
            dtView.EditCell(rowIndex, "ResidualQQPlotSettings", _residualQQPlot != null ? PlotSerializer.ToXElement(_residualQQPlot).ToString() : "");
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
        /// Creates a copy of the element with an optional new name.
        /// </summary>
        /// <param name="newName">The name for the copied element. If empty or null, uses the current name.</param>
        /// <returns>A new instance of the rating curve analysis with copied properties.</returns>
        public override IElement Copy(string newName = "")
        {
            var element = new RatingCurveAnalysis(newName == "" || string.IsNullOrEmpty(newName) ? Name : newName, ParentCollection);

            element.IsUndoEnabled = false;
            try
            {
                element.Description = Description;

                // Replace the inner analysis with a cloned model
                element._innerAnalysis.PropertyChanged -= element.InnerAnalysis_PropertyChanged;
                var clonedRC = (RatingCurve)RatingCurve.Clone();
                element._innerAnalysis = new ModelAnalyses.RatingCurveAnalysis(clonedRC);
                element._innerAnalysis.PropertyChanged += element.InnerAnalysis_PropertyChanged;

                // Copy BayesianAnalysis settings
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

                // Copy time series references
                element.StageData = StageData;
                element.DischargeData = DischargeData;

                // Copy stage bin settings
                element._innerAnalysis.MinStage = MinStage;
                element._innerAnalysis.MaxStage = MaxStage;
                element._innerAnalysis.StageBins = StageBins;
                element._innerAnalysis.UseDefaultStageBins = UseDefaultStageBins;

                // Copy plot settings (inside undo suppression — matches FittingAnalysis.Copy template)
                if (_ratingCurvePlot != null) PlotSerializer.FromXElement(element._ratingCurvePlot, PlotSerializer.ToXElement(_ratingCurvePlot));
                if (_residualPlot != null) PlotSerializer.FromXElement(element._residualPlot, PlotSerializer.ToXElement(_residualPlot));
                if (_residualHistogramPlot != null) PlotSerializer.FromXElement(element._residualHistogramPlot, PlotSerializer.ToXElement(_residualHistogramPlot));
                if (_residualQQPlot != null) PlotSerializer.FromXElement(element._residualQQPlot, PlotSerializer.ToXElement(_residualQQPlot));
                _bayesianController.CopyTo(element._bayesianController);

                // Reset the cloned element to a clean post-construction state — no inherited
                // fitted parameters or chain references. Matches the canonical FittingAnalysis.Copy template.
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
        /// Copies the element from an external project file to the current project.
        /// </summary>
        /// <param name="itemName">The name of the item to copy from the external project.</param>
        /// <param name="fullFileName">The full file path of the external project database.</param>
        /// <returns>A new instance of the rating curve analysis loaded from the external project.</returns>
        public override IElement CopyFromExternal(string itemName, string fullFileName)
        {
            // Create SQLite connection
            var sqlite = new SQLiteManager(fullFileName);
            var element = new RatingCurveAnalysis(itemName, ParentCollection);
            element.Open(sqlite);
            return element;
        }

        /// <summary>
        /// Deletes the element from the project database file on disk and removes it from the parent collection.
        /// </summary>
        public override void Delete()
        {
            if (Name == null) return;
            // Unhook upstream Deleted subscriptions directly (do not route through the
            // StageData / DischargeData setters — those would re-add null messages and
            // re-flip IsDirty=true).
            if (_stageData != null) _stageData.Deleted -= OnStageDataDeleted;
            if (_dischargeData != null) _dischargeData.Deleted -= OnDischargeDataDeleted;
            DisposeBridges();
            _bayesianController?.Dispose();
            if (_innerAnalysis != null) _innerAnalysis.PropertyChanged -= InnerAnalysis_PropertyChanged;
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
        /// Clears all analysis results by delegating to the inner analysis.
        /// </summary>
        public void ClearResults()
        {
            _innerAnalysis.ClearResults();
        }

        /// <summary>
        /// Runs the rating curve analysis asynchronously using Bayesian MCMC methods.
        /// </summary>
        /// <param name="progressReporter">The progress reporter for tracking analysis progress.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task RunAsync(SafeProgressReporter progressReporter)
        {
            SetIsValid();
            if (IsValid == false) return;

            _messenger.Add(new BasicMessageItem(MessageType.Event, "The rating curve analysis for '" + Name + "' has started.", this, ParentCollection.Name, Name, nameof(RatingCurveAnalysis)));

            bool succeeded = false;
            try
            {
                // Delegate to inner analysis
                progressReporter.IndicateTaskStart();
                await _innerAnalysis.RunAsync(progressReporter);
                succeeded = true;
            }
            catch (OperationCanceledException)
            {
                ClearResults();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RatingCurveAnalysis.RunAsync failed for '{Name}': {ex}");
                ClearResults();
                _messenger.Add(new BasicMessageItem(MessageType.Error, $"The rating curve analysis for '{Name}' failed: {ex.Message}", this, ParentCollection.Name, Name, nameof(RatingCurveAnalysis)));
            }
            finally
            {
                progressReporter.IndicateTaskEnded();
                SetIsValid();
                _messenger.Add(new BasicMessageItem(MessageType.Event, "The rating curve analysis for '" + Name + "' is complete.", this, ParentCollection.Name, Name, nameof(RatingCurveAnalysis)));

                // A successful re-run refreshes the parameter estimates and MCMC samples
                // that the legacy-migration warning asked the user to regenerate, so the
                // warning no longer applies.
                if (succeeded && _legacyMigrationMsg != null)
                {
                    _messenger.Remove(_legacyMigrationMsg);
                    _legacyMigrationMsg = null;
                }
            }
        }

        /// <summary>
        /// Cancels the currently running analysis.
        /// </summary>
        public void CancelAnalysis()
        {
            _innerAnalysis.CancelAnalysis();
            SetIsValid();
        }

        #endregion

        #region Model Undo

        /// <summary>
        /// Records an undo action capturing the difference between the current
        /// <see cref="RatingCurve"/> XElement and the rolling snapshot baseline.
        /// </summary>
        /// <param name="propertyName">Display name of the property that triggered the change
        /// (appears in the undo stack).</param>
        /// <remarks>
        /// No-ops when undo is disabled, when the action is already an undo replay, when there
        /// is no baseline yet, or when the model XElement is unchanged. Updates the rolling
        /// baseline so the next call records the next delta.
        /// </remarks>
        private void RecordModelUndo(string propertyName)
        {
            var undoManager = IsUndoEnabled ? UndoManager : null;
            if (undoManager == null || undoManager.IsExecutingAction) return;
            if (_modelSnapshot == null) return;

            var currentSnapshot = _innerAnalysis.RatingCurve.ToXElement();
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
        /// Replays a <see cref="RatingCurve"/> XElement snapshot by reconstructing
        /// <c>_innerAnalysis</c> from the preserved StageData / DischargeData and the snapshot
        /// XML, then re-establishing the inner-analysis subscription. Used as the do/undo
        /// callback in <see cref="RecordModelUndo"/>.
        /// </summary>
        /// <param name="snapshot">The XElement snapshot to restore.</param>
        private void RestoreModelFromSnapshot(XElement snapshot)
        {
            if (_innerAnalysis != null)
                _innerAnalysis.PropertyChanged -= InnerAnalysis_PropertyChanged;
            var stageData = _innerAnalysis?.RatingCurve?.StageData;
            var dischargeData = _innerAnalysis?.RatingCurve?.DischargeData;
            var analysisXml = _innerAnalysis?.ToXElement();
            var newModel = new RatingCurve(stageData, dischargeData, snapshot);
            _innerAnalysis = new ModelAnalyses.RatingCurveAnalysis(newModel, analysisXml);
            if (_innerAnalysis.BayesianAnalysis.UseSimulationDefaults)
                _innerAnalysis.BayesianAnalysis.SetDefaultSimulationOptions();
            if (_innerAnalysis.BayesianAnalysis.UseAdvancedSimulationDefaults)
                _innerAnalysis.BayesianAnalysis.SetDefaultAdvancedSimulationOptions();
            _innerAnalysis.PropertyChanged += InnerAnalysis_PropertyChanged;
            SetupBridges();
            RaisePropertyChange(nameof(RatingCurve));
            RaisePropertyChange(nameof(BayesianAnalysis));
            RaisePropertyChange(nameof(IsEstimated));
            RaisePropertyChange(nameof(AnalysisResults));
            RaisePropertyChange(nameof(MinStage));
            RaisePropertyChange(nameof(MaxStage));
            RaisePropertyChange(nameof(StageBins));
            RaisePropertyChange(nameof(UseDefaultStageBins));
            SetIsValid();
            SetIsDirty(true);
        }

        #endregion

        #region Plot Factory Methods

        /// <summary>
        /// Applies the default visual style (background, legend, padding) to a plot. Shared
        /// across all element-owned plots so styling stays consistent.
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
        /// Creates the default rating curve plot (linear stage vs. discharge axes) with
        /// TopLeft legend placement to avoid overlapping the curve sweep.
        /// </summary>
        /// <returns>A new <see cref="Plot"/> configured for rating curve display.</returns>
        private static Plot CreateDefaultRatingCurvePlot()
        {
            var plot = new Plot();
            ApplyDefaultPlotStyle(plot);
            plot.Title = "Rating Curve";
            // Override the default TopRight legend placement: rating curves sweep up
            // through the top-right corner of the plot area, so the legend typically
            // overlaps the posterior/mode curves. TopLeft is consistently clear.
            plot.LegendPosition = OxyPlot.Legends.LegendPosition.TopLeft;
            plot.Axes.Add(new OxyPlot.Wpf.LinearAxis { Key = "Yaxis", Position = OxyPlot.Axes.AxisPosition.Left, Title = "", StringFormat = "N0", AxisTitleDistance = 20, TitleFontSize = 16, FontSize = 12, MajorGridlineStyle = OxyPlot.LineStyle.Solid, MinorGridlineStyle = OxyPlot.LineStyle.None });
            plot.Axes.Add(new OxyPlot.Wpf.LinearAxis { Key = "Xaxis", Position = OxyPlot.Axes.AxisPosition.Bottom, Title = "", StringFormat = "N0", AxisTitleDistance = 20, TitleFontSize = 16, FontSize = 12, MajorGridlineStyle = OxyPlot.LineStyle.Solid, MinorGridlineStyle = OxyPlot.LineStyle.None });
            return plot;
        }

        /// <summary>
        /// Creates the default residual plot (residual vs. fitted log-discharge) for
        /// regression diagnostics.
        /// </summary>
        /// <returns>A new <see cref="Plot"/> configured for residual display.</returns>
        private static Plot CreateDefaultResidualPlot()
        {
            var plot = new Plot();
            ApplyDefaultPlotStyle(plot);
            plot.Title = "Residuals";
            plot.Axes.Add(new OxyPlot.Wpf.LinearAxis { Key = "Yaxis", Position = OxyPlot.Axes.AxisPosition.Left, Title = "Residuals", AxisTitleDistance = 20, TitleFontSize = 16, FontSize = 12, MajorGridlineStyle = OxyPlot.LineStyle.Solid, MinorGridlineStyle = OxyPlot.LineStyle.None });
            plot.Axes.Add(new OxyPlot.Wpf.LinearAxis { Key = "Xaxis", Position = OxyPlot.Axes.AxisPosition.Bottom, Title = "Log\u2081\u2080 Fitted Values", AxisTitleDistance = 20, TitleFontSize = 16, FontSize = 12, MajorGridlineStyle = OxyPlot.LineStyle.Solid, MinorGridlineStyle = OxyPlot.LineStyle.None });
            return plot;
        }

        /// <summary>
        /// Creates the default residual histogram plot (density vs. residual value)
        /// for testing residual normality.
        /// </summary>
        /// <returns>A new <see cref="Plot"/> configured for residual histogram display.</returns>
        private static Plot CreateDefaultResidualHistogramPlot()
        {
            var plot = new Plot();
            ApplyDefaultPlotStyle(plot);
            plot.Title = "Residual Histogram";
            plot.Axes.Add(new OxyPlot.Wpf.LinearAxis { Key = "Yaxis", Position = OxyPlot.Axes.AxisPosition.Left, Title = "Density", AxisTitleDistance = 20, TitleFontSize = 16, FontSize = 12, MajorGridlineStyle = OxyPlot.LineStyle.Solid, MinorGridlineStyle = OxyPlot.LineStyle.None });
            plot.Axes.Add(new OxyPlot.Wpf.LinearAxis { Key = "Xaxis", Position = OxyPlot.Axes.AxisPosition.Bottom, Title = "Residuals", AxisTitleDistance = 20, TitleFontSize = 16, FontSize = 12, MajorGridlineStyle = OxyPlot.LineStyle.Solid, MinorGridlineStyle = OxyPlot.LineStyle.None });
            return plot;
        }

        /// <summary>
        /// Creates the default residual Q-Q plot (residual quantile vs. standard normal
        /// quantile) for testing residual normality.
        /// </summary>
        /// <returns>A new <see cref="Plot"/> configured for residual Q-Q display.</returns>
        private static Plot CreateDefaultResidualQQPlot()
        {
            var plot = new Plot();
            ApplyDefaultPlotStyle(plot);
            plot.Title = "Residual Q-Q Plot";
            plot.Axes.Add(new OxyPlot.Wpf.LinearAxis { Key = "Yaxis", Position = OxyPlot.Axes.AxisPosition.Left, Title = "Quantile (Residuals)", AxisTitleDistance = 20, TitleFontSize = 16, FontSize = 12, MajorGridlineStyle = OxyPlot.LineStyle.Solid, MinorGridlineStyle = OxyPlot.LineStyle.None });
            plot.Axes.Add(new OxyPlot.Wpf.LinearAxis { Key = "Xaxis", Position = OxyPlot.Axes.AxisPosition.Bottom, Title = "Quantile (Standardized)", AxisTitleDistance = 20, TitleFontSize = 16, FontSize = 12, MajorGridlineStyle = OxyPlot.LineStyle.Solid, MinorGridlineStyle = OxyPlot.LineStyle.None });
            return plot;
        }

        /// <summary>
        /// Converts a hex color string (e.g. <c>"#8CFFFFFF"</c>) to a <see cref="Color"/>.
        /// </summary>
        /// <param name="hex">The hex color string. Must be parseable by <see cref="ColorConverter.ConvertFromString"/>.</param>
        /// <returns>The parsed <see cref="Color"/>.</returns>
        private static Color ColorFromHex(string hex)
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }

        /// <summary>
        /// Deserializes plot settings from a SQLite column into a live <see cref="Plot"/>.
        /// Silently no-ops on null plot, missing column, empty payload, or malformed XML
        /// (failure logged to <see cref="System.Diagnostics.Debug"/>).
        /// </summary>
        /// <param name="dtView">The SQLite table view to read from.</param>
        /// <param name="rowIndex">The row index in the table.</param>
        /// <param name="columnName">The column name containing the serialized plot XML.</param>
        /// <param name="plot">The target plot whose settings will be replaced.</param>
        private static void DeserializePlotSettings(DataTableView dtView, int rowIndex, string columnName, Plot plot)
        {
            if (plot == null) return;
            if (!dtView.ColumnNames.Contains(columnName)) return;
            var xml = dtView.GetCell(columnName, rowIndex)?.ToString();
            if (string.IsNullOrEmpty(xml)) return;
            try { PlotSerializer.FromXElement(plot, XElement.Parse(xml)); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Could not deserialize plot settings '{columnName}': {ex.Message}"); }
        }

        #endregion

        #region Undo Bridge Management

        /// <summary>
        /// Creates plot undo managers and the model-snapshot baseline. Disposes any existing
        /// bridges first, so SetupBridges is safe to call repeatedly during construction,
        /// Open(), and Copy().
        /// </summary>
        private void SetupBridges()
        {
            DisposeBridges();
            UndoManager.StateChanged += UndoManager_StateChanged;

            _modelSnapshot = _innerAnalysis?.RatingCurve?.ToXElement();

            Func<IUndoManager> getUndo = () => IsUndoEnabled ? UndoManager : null;
            Action onRecorded = () => SetIsDirty(true);

            if (_ratingCurvePlot != null) _ratingCurvePlotUndo = new PlotUndoManager(_ratingCurvePlot, getUndo, "rating curve plot", this, onRecorded);
            if (_residualPlot != null) _residualPlotUndo = new PlotUndoManager(_residualPlot, getUndo, "residual plot", this, onRecorded);
            if (_residualHistogramPlot != null) _residualHistogramPlotUndo = new PlotUndoManager(_residualHistogramPlot, getUndo, "residual histogram plot", this, onRecorded);
            if (_residualQQPlot != null) _residualQQPlotUndo = new PlotUndoManager(_residualQQPlot, getUndo, "residual QQ plot", this, onRecorded);

            _bayesianController?.SetupBridges(getUndo, this, onRecorded, _innerAnalysis?.BayesianAnalysis, () => SetIsValid());
        }

        /// <summary>
        /// Handles <see cref="UndoManager.StateChanged"/> to revalidate after undo/redo.
        /// </summary>
        /// <param name="sender">The undo manager raising the event.</param>
        /// <param name="e">Empty event args.</param>
        private void UndoManager_StateChanged(object sender, EventArgs e)
        {
            SetIsValid();
        }

        /// <summary>
        /// Disposes all undo bridges and unsubscribes <see cref="UndoManager.StateChanged"/>.
        /// Called from <see cref="SetupBridges"/> at the start of bridge re-creation and
        /// from <see cref="Delete"/> to release subscriptions before the element is destroyed.
        /// </summary>
        private void DisposeBridges()
        {
            UndoManager.StateChanged -= UndoManager_StateChanged;
            _ratingCurvePlotUndo?.Dispose(); _ratingCurvePlotUndo = null;
            _residualPlotUndo?.Dispose(); _residualPlotUndo = null;
            _residualHistogramPlotUndo?.Dispose(); _residualHistogramPlotUndo = null;
            _residualQQPlotUndo?.Dispose(); _residualQQPlotUndo = null;
            _bayesianController?.DisposeBridges();
        }

        /// <summary>
        /// Suspends all plot undo bridges so programmatic plot mutations (axis binding,
        /// series population, plot-settings restoration) do not record undo entries.
        /// </summary>
        /// <returns>An <see cref="IDisposable"/> that resumes recording when disposed.</returns>
        public IDisposable SuspendPlotBridges()
        {
            var suspensions = new List<IDisposable>();
            if (_ratingCurvePlotUndo != null) suspensions.Add(_ratingCurvePlotUndo.SuspendRecording());
            if (_residualPlotUndo != null) suspensions.Add(_residualPlotUndo.SuspendRecording());
            if (_residualHistogramPlotUndo != null) suspensions.Add(_residualHistogramPlotUndo.SuspendRecording());
            if (_residualQQPlotUndo != null) suspensions.Add(_residualQQPlotUndo.SuspendRecording());
            if (_bayesianController != null) suspensions.Add(_bayesianController.SuspendPlotBridges());
            return new AggregateDisposable(suspensions);
        }

        /// <summary>
        /// Rebuilds series and annotation bridges for the specified plot after a bulk series
        /// update. Call this after populating series inside a <see cref="SuspendPlotBridges"/> block.
        /// </summary>
        /// <param name="plot">The plot whose bridges should be rebuilt.</param>
        public void RebuildSeriesAndAnnotationBridges(Plot plot)
        {
            if (plot == _ratingCurvePlot) _ratingCurvePlotUndo?.RebuildSeriesAndAnnotationBridges();
            else if (plot == _residualPlot) _residualPlotUndo?.RebuildSeriesAndAnnotationBridges();
            else if (plot == _residualHistogramPlot) _residualHistogramPlotUndo?.RebuildSeriesAndAnnotationBridges();
            else if (plot == _residualQQPlot) _residualQQPlotUndo?.RebuildSeriesAndAnnotationBridges();
            else _bayesianController?.RebuildSeriesAndAnnotationBridges(plot);
        }

        #endregion

    }
}
