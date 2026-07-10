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
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Xml.Linq;
using ModelAnalyses = RMC.BestFit.Analyses;

namespace RMC.BestFit.UI
{
    /// <summary>
    /// Time series analysis UI wrapper.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    /// This class wraps a <see cref="ModelAnalyses.ARIMAXAnalysis"/> from the model library,
    /// delegating all computation to the inner analysis while providing UI-specific concerns:
    /// time series element reference management, covariate collection management, SQLite persistence,
    /// messenger-based validation, and plot settings.
    /// </para>
    /// </remarks>
    [Category("General"),
    DisplayName("Time Series Analysis"),
    Description("The Bayesian Markov Chain Monte Carlo (MCMC) method is used to estimate a time series model."),
    Browsable(true)]
    public class TimeSeriesAnalysis : ElementBase, IAnalysisElement
    {
        #region Construction

        /// <summary>
        /// Constructs a new time series analysis instance.
        /// </summary>
        /// <param name="name">The name of the element.</param>
        /// <param name="parentCollection">The parent collection name.</param>
        /// <param name="openFromFile">Optional parameter to open the function from disk upon construction.</param>
        public TimeSeriesAnalysis(string name, IElementCollection parentCollection, bool openFromFile = false) : base(name, parentCollection)
        {
            IsUndoEnabled = false;
            try
            {
                _nameOnDisk = name;
                _creationDate = DateTime.Now;
                _lastModified = DateTime.Now;

                // Create plot objects
                _timeSeriesPlot = CreateDefaultTimeSeriesPlot();
                _residualPlot = CreateDefaultResidualPlot();
                _residualHistogramPlot = CreateDefaultResidualHistogramPlot();
                _residualQQPlot = CreateDefaultResidualQQPlot();
                _residualACFPlot = CreateDefaultResidualACFPlot();
                _residualPACFPlot = CreateDefaultResidualPACFPlot();
                _bayesianController = new BayesianController();

                // Create the inner model analysis
                var arimax = new ARIMAX();
                _innerAnalysis = new ModelAnalyses.ARIMAXAnalysis(arimax);

                // Subscribe to inner analysis property changes
                _innerAnalysis.PropertyChanged += InnerAnalysis_PropertyChanged;

                // Add UI-only messages
                _descriptionMsg = new BasicMessageItem(MessageType.Message, "The time series analysis does not have a description.", this, ParentCollection.Name, Name, nameof(Description), "TSA-MSG-001");
                _tsDataNullMsg = new BasicMessageItem(MessageType.Error, "Time series data is missing. Please select a valid time series.", this, ParentCollection.Name, Name, nameof(TimeSeriesData), "TSA-ERR-005");
                _tsDataInValidMsg = new BasicMessageItem(MessageType.Error, "The selected time series is not valid.", this, ParentCollection.Name, Name, nameof(TimeSeriesData), "TSA-ERR-006");
                _seriesEqualMsg = new BasicMessageItem(MessageType.Error, "The selected time series cannot be equal.", this, ParentCollection.Name, Name, nameof(TimeSeriesData), "TSA-ERR-009");
                _seriesOverlapMsg = new BasicMessageItem(MessageType.Error, "The selected time series must have overlapping dates.", this, ParentCollection.Name, Name, nameof(TimeSeriesData), "TSA-ERR-010");

                _messages = new List<BasicMessageItem>()
                {   _descriptionMsg,
                    _tsDataNullMsg,
                    _tsDataInValidMsg,
                    _seriesEqualMsg,
                    _seriesOverlapMsg
                };

                // Create messenger and validation adapter
                _messenger = Messenger.GetInstance();
                _validationAdapter = new ValidationMessageAdapter(_messenger, this, ParentCollection.Name, "TSA");
                _messenger.Add(_descriptionMsg);
                _messenger.Add(_tsDataNullMsg);

                Covariates = new ObservableCollection<CovariateData>();

                if (openFromFile == true) Open();
                _nameValid = ValidateName(BestFitProject.InvalidNameCharacters, 50, "TSA");

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
        [Description("Unique label identifying this time series analysis; max 50 characters.")]
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
                    _nameValid = ValidateName(BestFitProject.InvalidNameCharacters, 50, "TSA");
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
        [Description("Free-text annotation describing this time series analysis.")]
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
        public override System.Windows.Media.ImageSource ElementImage => System.Windows.Application.Current?.TryFindResource("TimeSeriesAnalysisIcon") as System.Windows.Media.ImageSource;

        /// <summary>
        /// Gets the resource dictionary key for the element's theme-aware vector icon.
        /// The project explorer uses <see cref="System.Windows.FrameworkElement.SetResourceReference"/>
        /// with this key so the icon updates automatically when the theme changes.
        /// </summary>
        public string ElementImageResourceKey => "TimeSeriesAnalysisIcon";

        /// <summary>
        /// Gets a value indicating whether the element can be copied from an external application.
        /// </summary>
        public override bool CanCopyFromExternal => false;

        /// <summary>
        /// Gets a value indicating whether the element is valid.
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
        private ModelAnalyses.ARIMAXAnalysis _innerAnalysis;

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
        /// Message item for null time series data.
        /// </summary>
        private BasicMessageItem _tsDataNullMsg;
        /// <summary>
        /// Message item for invalid time series data.
        /// </summary>
        private BasicMessageItem _tsDataInValidMsg;
        /// <summary>
        /// Message item for when time series are the same.
        /// </summary>
        private BasicMessageItem _seriesEqualMsg;
        /// <summary>
        /// Message item for when time series do not overlap.
        /// </summary>
        private BasicMessageItem _seriesOverlapMsg;

        /// <summary>
        /// Indicates whether the element name is valid.
        /// </summary>
        private bool _nameValid = false;
        /// <summary>
        /// Indicates whether the time series data is valid.
        /// </summary>
        private bool _tsDataValid = false;

        /// <summary>
        /// Tracks whether this element was migrated from a legacy (v1) project file during <see cref="Open()"/>.
        /// When true, the constructor and <see cref="Open()"/> mark the element dirty so the user is prompted
        /// to save the upgraded schema.
        /// </summary>
        private bool openedFromV1 = false;

        /// <summary>
        /// Reference to the legacy-migration warning message added to the messenger during
        /// <see cref="Open()"/> when a pre-v2.0 'ARMAX' project file is detected. Held so that
        /// <see cref="RunAsync"/> can remove it from the messenger once the user has
        /// successfully re-run the Bayesian analysis and refreshed the results.
        /// </summary>
        private BasicMessageItem _legacyMigrationMsg = null;

        /// <summary>
        /// The time series element containing the data to be analyzed.
        /// </summary>
        private TimeSeriesElement _timeSeriesData;

        /// <summary>
        /// Collection of covariate data for the analysis.
        /// </summary>
        private ObservableCollection<CovariateData> _covariates;

        // Plot settings
        /// <summary>The time series plot (observed series + ARIMAX fit overlay).</summary>
        private Plot _timeSeriesPlot;
        /// <summary>The residual scatter plot (residual vs. fitted).</summary>
        private Plot _residualPlot;
        /// <summary>The residual histogram plot.</summary>
        private Plot _residualHistogramPlot;
        /// <summary>The residual Q-Q plot.</summary>
        private Plot _residualQQPlot;
        /// <summary>The residual autocorrelation plot.</summary>
        private Plot _residualACFPlot;
        /// <summary>The residual partial-autocorrelation plot.</summary>
        private Plot _residualPACFPlot;

        /// <summary>Undo bridge for visual edits to <see cref="_timeSeriesPlot"/>.</summary>
        private PlotUndoManager _timeSeriesPlotUndo;
        /// <summary>Undo bridge for visual edits to <see cref="_residualPlot"/>.</summary>
        private PlotUndoManager _residualPlotUndo;
        /// <summary>Undo bridge for visual edits to <see cref="_residualHistogramPlot"/>.</summary>
        private PlotUndoManager _residualHistogramPlotUndo;
        /// <summary>Undo bridge for visual edits to <see cref="_residualQQPlot"/>.</summary>
        private PlotUndoManager _residualQQPlotUndo;
        /// <summary>Undo bridge for visual edits to <see cref="_residualACFPlot"/>.</summary>
        private PlotUndoManager _residualACFPlotUndo;
        /// <summary>Undo bridge for visual edits to <see cref="_residualPACFPlot"/>.</summary>
        private PlotUndoManager _residualPACFPlotUndo;

        /// <summary>
        /// Undo bridge for the Covariates ObservableCollection (Add/Remove/Replace).
        /// </summary>
        private UndoableCollectionBridge<CovariateData> _covariatesBridge;

        /// <summary>Owns the 7 Bayesian diagnostic plots (trace, histogram, KDE, autocorrelation, mean-likelihood, bivariate heat map, influence). Exposed via <see cref="BayesianPlots"/>.</summary>
        private BayesianController _bayesianController;

        /// <summary>
        /// Rolling XElement baseline of the ARIMAX model state.
        /// </summary>
        private XElement _modelSnapshot;

        /// <summary>
        /// Property names that trigger snapshot comparison.
        /// </summary>
        private static readonly HashSet<string> ModelUndoProperties = new()
        {
            "Parameters", "UseJeffreysRuleForScale", "AROrderP", "MAOrderQ",
            "DiffOrderD", "IncludeIntercept", "TransformType",
            "TrainingTimeSteps", "UseDefaultTrainingSteps",
            "XOrderB", "IncludeSeasonality", "TrendType", "CovariateExtension"
        };

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the selected time series element.
        /// </summary>
        [Category("General")]
        [DisplayName("Time Series Data")]
        [Description("The response time series modeled by the AR/ARIMA/ARIMAX likelihood. Must have regularly spaced (constant-step) timestamps. Its UnitLabel drives the time-series plot Y-axis title via reactive binding. Cannot be null when running an analysis.")]
        [Browsable(true)]
        public TimeSeriesElement TimeSeriesData
        {
            get { return _timeSeriesData; }
            set
            {
                if (_timeSeriesData == value) return;
                var old = _timeSeriesData;

                if (_timeSeriesData != null)
                {
                    _timeSeriesData.PropertyChanged -= TimeSeriesElementChanged;
                    _timeSeriesData.Deleted -= OnTimeSeriesDataDeleted;
                }

                _timeSeriesData = value;

                if (_timeSeriesData != null)
                {
                    _timeSeriesData.PropertyChanged += TimeSeriesElementChanged;
                    _timeSeriesData.Deleted += OnTimeSeriesDataDeleted;
                }

                SyncTimeSeriesDataToInnerModel();

                SetIsValid();
                if (!UndoManager.IsExecutingAction) ClearResults();
                RecordPropertyChange(nameof(TimeSeriesData), old, value);
            }
        }

        /// <summary>
        /// Gets and sets the collection of covariates.
        /// </summary>
        [Category("Inputs")]
        [DisplayName("Covariates")]
        [Description("Exogenous time series that enter an ARIMAX model as regressors of the response. Each covariate's timestamps must align with TimeSeriesData; misaligned points are dropped. Forecasting beyond the observed range requires extending each covariate (see CovariateExtension). Empty for AR/MA/ARIMA models.")]
        [Browsable(true)]
        public ObservableCollection<CovariateData> Covariates
        {
            get { return _covariates; }
            set
            {
                if (_covariates != null)
                    _covariates.CollectionChanged -= Covariate_CollectionChanged;

                _covariates = value;

                if (_covariates != null)
                    _covariates.CollectionChanged += Covariate_CollectionChanged;

                // Sync covariates to inner analysis model
                if (_innerAnalysis != null && _innerAnalysis.ARIMAX != null)
                {
                    _innerAnalysis.ARIMAX.SetCovariates(_covariates.Where(x => x.TimeSeriesElement != null).Select(x => x.TimeSeriesElement.TimeSeries).ToList());
                }

                RaisePropertyChange(nameof(Covariates));
            }
        }

        /// <summary>
        /// Gets the ARIMAX time series model (delegates to inner analysis).
        /// </summary>
        public ARIMAX ARIMAX
        {
            get { return _innerAnalysis.ARIMAX; }
        }

        /// <summary>
        /// Gets the Bayesian Analysis object (delegates to inner analysis).
        /// </summary>
        public BayesianAnalysis BayesianAnalysis
        {
            get { return _innerAnalysis.BayesianAnalysis; }
        }

        /// <summary>
        /// Gets or sets the number of time steps to forecast past the end of the observed series.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Number of steps to forecast <b>past the end of the observed series</b>. The
        /// predictive window always covers the full observed range (training + validation)
        /// plus <see cref="ForecastSteps"/> additional future steps. Must be in
        /// the range <c>[0, 100]</c>.
        /// </para>
        /// <para>
        /// Disambiguated from the model-layer <c>ARIMAX.ForecastingTimeSteps</c> (which is a
        /// derived validation-window count equal to <c>TimeSeries.Count - TrainingTimeSteps</c>)
        /// and the analysis-layer <c>ARIMAXAnalysis.ForecastingTimeSteps</c> (the settable
        /// out-of-sample horizon this property delegates to).
        /// </para>
        /// </remarks>
        [Category("Output")]
        [DisplayName("Forecast Steps")]
        [Description("Number of steps to forecast past the end of the observed series. Range: 0 (no future forecast) to 100.")]
        [Browsable(true)]
        public int ForecastSteps
        {
            get { return _innerAnalysis.ForecastingTimeSteps; }
            set
            {
                int clamped = Math.Max(0, Math.Min(100, value));
                if (_innerAnalysis.ForecastingTimeSteps != clamped)
                {
                    var old = _innerAnalysis.ForecastingTimeSteps;
                    _innerAnalysis.ForecastingTimeSteps = clamped;
                    RecordPropertyChange(nameof(ForecastSteps), old, clamped);
                }
            }
        }

        /// <summary>
        /// Gets or sets the number of training time steps used to fit the model.
        /// </summary>
        /// <remarks>
        /// Lightweight delegating wrapper over <see cref="ARIMAX.TrainingTimeSteps"/>. Undo-redo
        /// is handled by the existing <c>ModelUndoProperties</c> XElement-snapshot mechanism in
        /// <see cref="InnerAnalysis_PropertyChanged"/>, which already watches this property name â€”
        /// so the setter uses <c>RaisePropertyChange</c> rather than
        /// <c>RecordPropertyChange</c> to avoid duplicate undo entries.
        /// </remarks>
        [Category("Output")]
        [DisplayName("Training Time Steps")]
        [Description("Number of time steps used to fit the model. Must be at least max(10, parameter count) and no greater than the observed series length.")]
        [Browsable(true)]
        public int TrainingTimeSteps
        {
            get { return _innerAnalysis.ARIMAX.TrainingTimeSteps; }
            set
            {
                if (_innerAnalysis.ARIMAX.TrainingTimeSteps != value)
                {
                    // A manual edit overrides the 80% default rule. Flip the flag BEFORE assigning
                    // so subsequent model-internal reset paths (TimeSeries setter, CollectionChanged,
                    // SetDefaultTrainingSteps) no longer clobber the user's value with floor(0.8Â·N).
                    // Matches the canonical pattern in the verification tests:
                    // UseDefaultTrainingSteps = false THEN TrainingTimeSteps = N.
                    if (_innerAnalysis.ARIMAX.UseDefaultTrainingSteps)
                        _innerAnalysis.ARIMAX.UseDefaultTrainingSteps = false;
                    _innerAnalysis.ARIMAX.TrainingTimeSteps = value;
                    RaisePropertyChange(nameof(TrainingTimeSteps));
                    RaisePropertyChange(nameof(UseDefaultTrainingSteps));
                }
            }
        }

        /// <summary>
        /// Gets or sets whether the training time steps are automatically set to 80% of the observed series length.
        /// </summary>
        /// <remarks>
        /// Lightweight delegating wrapper over <see cref="ARIMAX.UseDefaultTrainingSteps"/>. When
        /// flipped to <c>true</c>, the model recomputes <see cref="TrainingTimeSteps"/> via the
        /// 80% rule and fires its own <c>PropertyChanged</c>. Undo-redo is handled by the
        /// <c>ModelUndoProperties</c> XElement-snapshot mechanism.
        /// </remarks>
        [Category("Output")]
        [DisplayName("Use Default Training Steps")]
        [Description("When true, training time steps are automatically set to 80% of the observed series length (with a minimum floor of max(10, parameter count)).")]
        [Browsable(true)]
        public bool UseDefaultTrainingSteps
        {
            get { return _innerAnalysis.ARIMAX.UseDefaultTrainingSteps; }
            set
            {
                if (_innerAnalysis.ARIMAX.UseDefaultTrainingSteps != value)
                {
                    _innerAnalysis.ARIMAX.UseDefaultTrainingSteps = value;
                    RaisePropertyChange(nameof(UseDefaultTrainingSteps));
                    RaisePropertyChange(nameof(TrainingTimeSteps));
                }
            }
        }

        /// <summary>
        /// Gets or sets the method used to extend covariate time series past the observed range
        /// when forecasting with out-of-sample horizons.
        /// </summary>
        /// <remarks>
        /// Lightweight delegating wrapper over <see cref="ARIMAX.CovariateExtension"/>. Default is
        /// <see cref="ARIMAX.CovariateExtensionMethod.BlockBootstrap"/>, which preserves temporal
        /// autocorrelation within resampled blocks. Undo-redo is handled by the
        /// <c>ModelUndoProperties</c> XElement-snapshot mechanism (this property name is in the
        /// HashSet), so the setter uses <c>RaisePropertyChange</c> rather than
        /// <c>RecordPropertyChange</c> to avoid duplicate undo entries.
        /// </remarks>
        [Category("Output")]
        [DisplayName("Covariate Extension")]
        [Description("How to extend covariate time series when forecasting past the observed range. BlockBootstrap (default) preserves temporal autocorrelation; KNN preserves local structure; None throws if covariates are insufficient.")]
        [Browsable(true)]
        public ARIMAX.CovariateExtensionMethod CovariateExtension
        {
            get { return _innerAnalysis.ARIMAX.CovariateExtension; }
            set
            {
                if (_innerAnalysis.ARIMAX.CovariateExtension != value)
                {
                    _innerAnalysis.ARIMAX.CovariateExtension = value;
                    RaisePropertyChange(nameof(CovariateExtension));
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether the analysis has been estimated.
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
        /// Gets the time series plot (observed series + ARIMAX fit overlay).
        /// </summary>
        public Plot TimeSeriesPlot => _timeSeriesPlot;

        /// <summary>
        /// Gets the residual scatter plot (residual vs. fitted value).
        /// </summary>
        public Plot ResidualPlot => _residualPlot;

        /// <summary>
        /// Gets the residual histogram plot.
        /// </summary>
        public Plot ResidualHistogramPlot => _residualHistogramPlot;

        /// <summary>
        /// Gets the residual Q-Q plot (residual quantile vs. standard-normal quantile).
        /// </summary>
        public Plot ResidualQQPlot => _residualQQPlot;

        /// <summary>
        /// Gets the residual autocorrelation function plot.
        /// </summary>
        public Plot ResidualACFPlot => _residualACFPlot;

        /// <summary>
        /// Gets the residual partial-autocorrelation function plot.
        /// </summary>
        public Plot ResidualPACFPlot => _residualPACFPlot;

        /// <summary>
        /// Gets the <see cref="BayesianController"/> that owns the 7 Bayesian diagnostic plots
        /// (trace, histogram, KDE, autocorrelation, mean-likelihood, bivariate heat map, influence).
        /// </summary>
        public BayesianController BayesianPlots => _bayesianController;

        #endregion

        #endregion

        #region Methods

        /// <summary>
        /// Handles property changes forwarded from the inner model library analysis.
        /// </summary>
        private void InnerAnalysis_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Record undo for ARIMAX model property changes via XElement snapshot
            if (ModelUndoProperties.Contains(e.PropertyName))
            {
                RecordModelUndo(e.PropertyName);
            }

            // Forward relevant property changes to the WPF framework
            if (e.PropertyName == nameof(ModelAnalyses.ARIMAXAnalysis.IsEstimated) ||
                e.PropertyName == nameof(ModelAnalyses.ARIMAXAnalysis.AnalysisResults) ||
                e.PropertyName == nameof(ModelAnalyses.ARIMAXAnalysis.ARIMAX) ||
                e.PropertyName == nameof(ModelAnalyses.ARIMAXAnalysis.BayesianAnalysis))
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
        /// Handles property changes in the time series element.
        /// </summary>
        private void TimeSeriesElementChanged(object sender, PropertyChangedEventArgs e)
        {
            if (!ReferenceEquals(sender, _timeSeriesData)) return;

            if (e.PropertyName == nameof(TimeSeriesElement.TimeSeries))
            {
                SyncTimeSeriesDataToInnerModel();
                if (!UndoManager.IsExecutingAction) ClearResults();
                RaisePropertyChange(nameof(TimeSeriesData));
                RaisePropertyChange(nameof(TrainingTimeSteps));
                RaisePropertyChange(nameof(UseDefaultTrainingSteps));
            }

            if (_timeSeriesData?.TimeSeries == null)
            {
                SetIsValid();
                return;
            }
            if (_timeSeriesData.TimeSeries.SuppressCollectionChanged == false)
            {
                SetIsValid();
                RaisePropertyChange(e.PropertyName);
            }
        }

        /// <summary>
        /// Synchronizes the selected UI time-series element into the wrapped ARIMAX model.
        /// </summary>
        /// <remarks>
        /// The wrapper owns the relationship between project elements and model inputs. Keeping
        /// the sync in one place ensures both element selection changes and replacement of the
        /// selected element's underlying series reset the model training split consistently.
        /// </remarks>
        private void SyncTimeSeriesDataToInnerModel()
        {
            if (_innerAnalysis?.ARIMAX == null) return;

            _innerAnalysis.ARIMAX.TimeSeries = _timeSeriesData?.TimeSeries;
            if (_covariates != null)
                _innerAnalysis.ARIMAX.SetCovariates(_covariates.Where(x => x.TimeSeriesElement != null).Select(x => x.TimeSeriesElement.TimeSeries).ToList());

            RaisePropertyChange(nameof(TrainingTimeSteps));
            RaisePropertyChange(nameof(UseDefaultTrainingSteps));
        }

        /// <summary>
        /// Handles deletion of the <see cref="TimeSeriesData"/> element from its parent collection.
        /// Clears the reference with undo recording disabled so that pressing Undo cannot
        /// resurrect the now-deleted element. Pre-existing undo history on this analysis is
        /// preserved.
        /// </summary>
        private void OnTimeSeriesDataDeleted(IElement element)
        {
            var wasUndoEnabled = IsUndoEnabled;
            IsUndoEnabled = false;
            try { TimeSeriesData = null; }
            finally { IsUndoEnabled = wasUndoEnabled; }
        }

        /// <summary>
        /// Handles changes to the covariate collection.
        /// </summary>
        private void Covariate_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (CovariateData oldItem in e.OldItems)
                {
                    if (oldItem != null)
                    {
                        oldItem.PropertyChanged -= Covariate_PropertyChanged;
                        // Drop the wrapper's subscription on the underlying TimeSeriesElement so
                        // it can be garbage-collected. Without Dispose, the underlying element's
                        // PropertyChanged + Deleted delegate lists keep the wrapper rooted.
                        oldItem.Dispose();
                    }
                }
            }
            if (e.NewItems != null)
            {
                foreach (CovariateData newItem in e.NewItems)
                {
                    if (newItem != null)
                        newItem.PropertyChanged += Covariate_PropertyChanged;
                }
            }

            // Sync covariates to inner analysis model
            _innerAnalysis.ARIMAX.SetCovariates(_covariates.Where(x => x.TimeSeriesElement != null).Select(x => x.TimeSeriesElement.TimeSeries).ToList());
            SetIsValid();
            // Guard against undo replay: clearing the fit on replay-driven CollectionChanged
            // produces asymmetric undo (the UI wrapper consistency rule).
            if (!UndoManager.IsExecutingAction)
                ClearResults();
            RaisePropertyChange(nameof(Covariates));
        }

        /// <summary>
        /// Handles property changes in individual covariate data items.
        /// </summary>
        /// <remarks>
        /// When a covariate's <see cref="CovariateData.TimeSeriesElement"/> transitions to
        /// <c>null</c>, the upstream <see cref="TimeSeriesElement"/> was deleted from the
        /// project. Remove the now-empty wrapper from <see cref="Covariates"/> with undo
        /// recording disabled so that pressing Undo cannot re-insert a wrapper pointing at the
        /// now-deleted time series. Pre-existing undo history on this analysis is preserved.
        /// </remarks>
        private void Covariate_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CovariateData.TimeSeriesElement)
                && sender is CovariateData orphan && orphan.TimeSeriesElement == null)
            {
                var wasUndoEnabled = IsUndoEnabled;
                IsUndoEnabled = false;
                try { Covariates.Remove(orphan); }
                finally { IsUndoEnabled = wasUndoEnabled; }
                return;
            }

            // Sync covariates to inner analysis model
            _innerAnalysis.ARIMAX.SetCovariates(_covariates.Where(x => x.TimeSeriesElement != null).Select(x => x.TimeSeriesElement.TimeSeries).ToList());
            SetIsValid();
            // Guard against undo replay (same rationale as Covariates_CollectionChanged above).
            if (!UndoManager.IsExecutingAction)
                ClearResults();
            RaisePropertyChange(nameof(Covariates));
        }

        /// <summary>
        /// Validates the time series data and updates validation messages.
        /// </summary>
        private void ValidateTimeSeries()
        {
            _tsDataValid = true;
            _messenger.Remove(_tsDataNullMsg);
            _messenger.Remove(_tsDataInValidMsg);
            if (_timeSeriesData == null)
            {
                _tsDataValid = false;
                _messenger.Add(_tsDataNullMsg);
            }
            if (_timeSeriesData != null && (_timeSeriesData.IsValid == false))
            {
                _tsDataValid = false;
                _tsDataInValidMsg.Description = "The selected time series is not valid.";
                _messenger.Add(_tsDataInValidMsg);
            }
        }

        /// <summary>
        /// Updates the IsValid property based on the current validation state of all analysis components.
        /// </summary>
        private void SetIsValid()
        {
            ValidateTimeSeries();

            bool uiValid = true;
            if (_nameValid == false) uiValid = false;
            if (_tsDataValid == false) uiValid = false;

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
            { nameof(TimeSeriesData), typeof(string) },
            { nameof(Covariates), typeof(string) },
            { nameof(ARIMAX), typeof(string) },
            { nameof(BayesianAnalysis), typeof(string) },
            { "ForecastingTimeSteps", typeof(int) },
            { nameof(MCMCResults), typeof(byte[]) },
            { nameof(AnalysisResults), typeof(string) },
            { "AnalysisXml", typeof(string) },
            { "TimeSeriesPlotSettings", typeof(string) },
            { "ResidualPlotSettings", typeof(string) },
            { "ResidualHistogramPlotSettings", typeof(string) },
            { "ResidualQQPlotSettings", typeof(string) },
            { "ResidualACFPlotSettings", typeof(string) },
            { "ResidualPACFPlotSettings", typeof(string) },
            { "KernelDensityPlotSettings", typeof(string) },
            { "HistogramPlotSettings", typeof(string) },
            { "BivariatePlotSettings", typeof(string) },
            { "MeanLikelihoodPlotSettings", typeof(string) },
            { "AutocorrelationPlotSettings", typeof(string) },
            { "MarkovChainTracesPlotSettings", typeof(string) },
            { "InfluenceDiagnosticsPlotSettings", typeof(string) },
            { "MCMCReport", typeof(string) } };

        /// <summary>
        /// Creates the SQLite database table for storing time series analysis data.
        /// </summary>
        private void CreateTable(SQLiteManager sqlite)
        {
            if (sqlite.TableNames.Contains(ParentCollection.Name) == false)
            {
                var dataTable = new DataTable(ParentCollection.Name);
                foreach (KeyValuePair<string, Type> column in RequiredColumns)
                    dataTable.Columns.Add(column.Key, column.Value);
                sqlite.SaveDataTable(dataTable);
            }
            else
            {
                var dt = sqlite.GetTableManager(ParentCollection.Name);
                int columnIndex;
                foreach (KeyValuePair<string, Type> column in RequiredColumns)
                {
                    columnIndex = Array.IndexOf(dt.ColumnNames, column.Key);
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
        /// Opens the element from the specified SQLite database.
        /// </summary>
        /// <param name="sqlite">The SQLite manager instance.</param>
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
                // The TimeSeriesData setter triggers SetIsValid(), ClearResults(), and syncs the inner ARIMAX model;
                // calling it here would prematurely clear results before the MCMC bytes and model XElement have
                // been loaded. A single SetIsValid() call at the end of Open() is sufficient.
                if (dtView.ColumnNames.Contains(nameof(Name)))
                {
                    _name = dtView.GetCell(nameof(Name), rowIndex).ToString();
                    foreach (var item in _messages) item.SourceName = _name;
                    _nameValid = ValidateName(BestFitProject.InvalidNameCharacters, 50, "TSA");
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

                // Get time series â€” use backing field to avoid SetIsValid(), ClearResults(),
                // and premature inner-analysis sync (the inner analysis is reconstructed below).
                if (dtView.ColumnNames.Contains(nameof(TimeSeriesData)))
                {
                    var elementName = dtView.GetCell(nameof(TimeSeriesData), rowIndex).ToString();
                    foreach (IElementCollection collection in ParentCollection.ParentProject.ElementCollections)
                    {
                        if (collection.GetType() == typeof(TimeSeriesCollection))
                        {
                            foreach (IElement element in collection)
                            {
                                if (element.Name == elementName && element.GetType() == typeof(TimeSeriesElement))
                                {
                                    _timeSeriesData = (TimeSeriesElement)element;
                                    _timeSeriesData.PropertyChanged += TimeSeriesElementChanged;
                                    _timeSeriesData.Deleted += OnTimeSeriesDataDeleted;
                                    _tsDataValid = _timeSeriesData.IsValid;
                                    _messenger.Remove(_tsDataNullMsg);
                                    if (!_tsDataValid)
                                        _messenger.Add(_tsDataInValidMsg);
                                    else
                                        _messenger.Remove(_tsDataInValidMsg);
                                    break;
                                }
                            }
                        }
                    }
                }

                // Plot Properties
                DeserializePlotSettings(dtView, rowIndex, "TimeSeriesPlotSettings", _timeSeriesPlot);
                DeserializePlotSettings(dtView, rowIndex, "ResidualPlotSettings", _residualPlot);
                DeserializePlotSettings(dtView, rowIndex, "ResidualHistogramPlotSettings", _residualHistogramPlot);
                DeserializePlotSettings(dtView, rowIndex, "ResidualQQPlotSettings", _residualQQPlot);
                DeserializePlotSettings(dtView, rowIndex, "ResidualACFPlotSettings", _residualACFPlot);
                DeserializePlotSettings(dtView, rowIndex, "ResidualPACFPlotSettings", _residualPACFPlot);
                _bayesianController.Deserialize(dtView, rowIndex);

                // Get ARIMAX model XElement. Pre-v2.0 projects stored the model in a
                // column named "ARMAX" with an additive parameter layout that cannot be
                // mapped onto the new ARIMAX model. We detect that case here so we can
                // discard the stale model state and start the user with a fresh default
                // model bound to the loaded input series.
                XElement modelXElement = null;
                bool isLegacyFormat = false;
                if (TimeSeriesData != null)
                {
                    if (dtView.ColumnNames.Contains(nameof(ARIMAX)))
                    {
                        var armaxStr = dtView.GetCell(nameof(ARIMAX), rowIndex)?.ToString();
                        if (!string.IsNullOrEmpty(armaxStr))
                        {
                            try { modelXElement = XElement.Parse(armaxStr); }
                            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Failed to load ARIMAX for '{Name}': {ex.Message}"); }
                        }
                    }
                    if (modelXElement == null && dtView.ColumnNames.Contains("ARMAX"))
                    {
                        var legacyStr = dtView.GetCell("ARMAX", rowIndex)?.ToString();
                        if (!string.IsNullOrEmpty(legacyStr))
                            isLegacyFormat = true;
                    }
                }
                openedFromV1 = isLegacyFormat;

                // Get covariates
                if (dtView.ColumnNames.Contains(nameof(Covariates)))
                {
                    Covariates = new ObservableCollection<CovariateData>();
                    var covariateName = dtView.GetCell(nameof(Covariates), rowIndex).ToString().Split('|');
                    for (int i = 0; i < covariateName.Length; i++)
                    {
                        foreach (IElementCollection collection in ParentCollection.ParentProject.ElementCollections)
                        {
                            if (collection.GetType() == typeof(TimeSeriesCollection))
                            {
                                foreach (IElement element in collection)
                                {
                                    if (element.Name == covariateName[i] && element.GetType() == typeof(TimeSeriesElement))
                                    {
                                        Covariates.Add(new CovariateData() { TimeSeriesElement = (TimeSeriesElement)element });
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }

                MCMCResults mcmcResults = isLegacyFormat
                    ? null
                    : AnalysisPersistenceHelper.TryLoadMCMCResults(dtView, rowIndex, Name);
                XElement innerXElement = isLegacyFormat
                    ? null
                    : AnalysisPersistenceHelper.TryLoadXElement(dtView, "AnalysisXml", rowIndex, Name);

                // Get Bayesian analysis XElement â€” ignored for legacy files (we
                // construct a fresh BayesianAnalysis with new defaults below).
                XElement analysisXElement = null;
                if (!isLegacyFormat && dtView.ColumnNames.Contains(nameof(BayesianAnalysis)))
                {
                    var xElementString = dtView.GetCell(nameof(BayesianAnalysis), rowIndex).ToString();
                    try { analysisXElement = XElement.Parse(xElementString); }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Failed to load BayesianAnalysis for '{Name}': {ex.Message}"); }
                }

                // Reconstruct inner analysis from persisted data
                _innerAnalysis.PropertyChanged -= InnerAnalysis_PropertyChanged;

                UncertaintyAnalysisResults analysisResults = isLegacyFormat
                    ? null
                    : AnalysisPersistenceHelper.TryLoadAnalysisResults(dtView, nameof(AnalysisResults), rowIndex, Name);

                if (TimeSeriesData != null)
                {
                    ARIMAX arimax;
                    if (modelXElement != null)
                    {
                        // Reconstruct ARIMAX model from persisted XElement
                        arimax = new ARIMAX(TimeSeriesData.TimeSeries, modelXElement);
                    }
                    else
                    {
                        // Either legacy XML (discarded) or no model has been saved yet:
                        // build a fresh ARIMAX bound to the loaded input series. Using
                        // the data-only ctor (instead of the empty one) ensures
                        // `arimax.TimeSeries` is non-null so the App control can render
                        // without dereferencing null.
                        arimax = new ARIMAX(TimeSeriesData.TimeSeries);
                    }

                    // Sync covariates to model
                    if (_covariates != null && _covariates.Count > 0)
                        arimax.SetCovariates(_covariates.Where(x => x.TimeSeriesElement != null).Select(x => x.TimeSeriesElement.TimeSeries).ToList());

                    if (innerXElement == null && !isLegacyFormat && analysisXElement != null)
                    {
                        // Build a combined XElement for the inner analysis constructor
                        innerXElement = new XElement("ARIMAXAnalysis",
                            new XAttribute("IsEstimated", mcmcResults != null));

                        // ForecastingTimeSteps is an analysis-layer property (not on the ARIMAX
                        // model or BayesianAnalysis) and is persisted to its own column. Pre-fix
                        // saves don't have the column — Contains() returns false and the value
                        // defaults to 0 inside the constructor, matching legacy behavior.
                        if (dtView.ColumnNames.Contains("ForecastingTimeSteps"))
                        {
                            var raw = dtView.GetCell("ForecastingTimeSteps", rowIndex);
                            if (raw != null && int.TryParse(raw.ToString(), out var fts))
                                innerXElement.SetAttributeValue("ForecastingTimeSteps", fts);
                        }

                        // Add Bayesian analysis element
                        innerXElement.Add(analysisXElement);

                    }

                    if (innerXElement != null)
                    {
                        _innerAnalysis = new ModelAnalyses.ARIMAXAnalysis(arimax, innerXElement, mcmcResults, analysisResults);
                    }
                    else
                    {
                        _innerAnalysis = new ModelAnalyses.ARIMAXAnalysis(arimax);
                    }
                }
                else
                {
                    _innerAnalysis = new ModelAnalyses.ARIMAXAnalysis(new ARIMAX());
                }

                _innerAnalysis.PropertyChanged += InnerAnalysis_PropertyChanged;

                if (isLegacyFormat)
                {
                    _legacyMigrationMsg = new BasicMessageItem(
                        MessageType.Warning,
                        $"The time series analysis '{Name}' was created with an older version of RMC-BestFit and stored under the legacy 'ARMAX' schema. Previous parameter estimates, MCMC samples, and uncertainty results were discarded. Re-run the Bayesian analysis to refresh the results.",
                        this, ParentCollection.Name, Name, nameof(TimeSeriesAnalysis), "TSA-WRN-LEGACY");
                    _messenger.Add(_legacyMigrationMsg);
                }
            }

            if (wasOpen == false) sqlite.Close();
            SetupBridges();
            SetIsValid();
            // Leave the project dirty when we discarded legacy content so the user
            // is prompted to save it in the new format.
            SetIsDirty(openedFromV1);
            // Notify project-tree node header binding that Name was restored from disk via backing field.
            RaisePropertyChange(nameof(Name), setDirty: openedFromV1);
            }
            finally
            {
                IsUndoEnabled = wasUndoEnabled;
                if (wasUndoEnabled) ClearUndoHistory();
            }
        }

        /// <summary>
        /// Raises the preview saved event before saving the element.
        /// </summary>
        public void RaisePreviewSaved(ref bool cancel)
        {
            RaisePreviewObjectSaved(this, ref cancel);
        }

        /// <summary>
        /// Saves the element to disk.
        /// </summary>
        public override void Save()
        {
            if (Name == null) return;

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

            CreateTable(sqlite);

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
            dtView.EditCell(rowIndex, nameof(TimeSeriesData), TimeSeriesData == null ? "" : TimeSeriesData.Name);
            // Filter out covariates whose upstream TimeSeriesElement was deleted (CovariateData
            // explicitly supports a null TimeSeriesElement during the deletion race window —
            // see CovariateData.Dispose() and the Covariate_PropertyChanged orphan-removal path).
            // Without the filter, Save() would NRE if a save is triggered between the upstream
            // delete and the orphan-removal handler firing.
            dtView.EditCell(rowIndex, nameof(Covariates),
                String.Join("|", Covariates.Where(x => x.TimeSeriesElement != null).Select(x => x.TimeSeriesElement.Name)));
            dtView.EditCell(rowIndex, nameof(ARIMAX), _innerAnalysis.ARIMAX.ToXElement().ToString());
            dtView.EditCell(rowIndex, nameof(BayesianAnalysis), _innerAnalysis.BayesianAnalysis.ToXElement().ToString());
            dtView.EditCell(rowIndex, "ForecastingTimeSteps", _innerAnalysis.ForecastingTimeSteps.ToString(System.Globalization.CultureInfo.InvariantCulture));

            dtView.EditCell(rowIndex, nameof(MCMCResults),
                AnalysisPersistenceHelper.SerializeMCMCResults(_innerAnalysis.BayesianAnalysis.Results));
            dtView.EditCell(rowIndex, nameof(AnalysisResults),
                AnalysisPersistenceHelper.SerializeAnalysisResults(_innerAnalysis.AnalysisResults));
            dtView.EditCell(rowIndex, "AnalysisXml", _innerAnalysis?.ToXElement()?.ToString() ?? "");

            dtView.EditCell(rowIndex, "TimeSeriesPlotSettings", _timeSeriesPlot != null ? PlotSerializer.ToXElement(_timeSeriesPlot).ToString() : "");
            dtView.EditCell(rowIndex, "ResidualPlotSettings", _residualPlot != null ? PlotSerializer.ToXElement(_residualPlot).ToString() : "");
            dtView.EditCell(rowIndex, "ResidualHistogramPlotSettings", _residualHistogramPlot != null ? PlotSerializer.ToXElement(_residualHistogramPlot).ToString() : "");
            dtView.EditCell(rowIndex, "ResidualQQPlotSettings", _residualQQPlot != null ? PlotSerializer.ToXElement(_residualQQPlot).ToString() : "");
            dtView.EditCell(rowIndex, "ResidualACFPlotSettings", _residualACFPlot != null ? PlotSerializer.ToXElement(_residualACFPlot).ToString() : "");
            dtView.EditCell(rowIndex, "ResidualPACFPlotSettings", _residualPACFPlot != null ? PlotSerializer.ToXElement(_residualPACFPlot).ToString() : "");
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
        public override IElement Copy(string newName = "")
        {
            var element = new TimeSeriesAnalysis(newName == "" || string.IsNullOrEmpty(newName) ? Name : newName, ParentCollection);

            element.IsUndoEnabled = false;
            try
            {
                element.Description = Description;

                // Replace the inner analysis with a cloned model
                element._innerAnalysis.PropertyChanged -= element.InnerAnalysis_PropertyChanged;
                var clonedARIMAX = (ARIMAX)ARIMAX.Clone();
                element._innerAnalysis = new ModelAnalyses.ARIMAXAnalysis(clonedARIMAX);
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

                // Copy time series reference
                element.TimeSeriesData = TimeSeriesData;

                // Copy covariates â€” deep clone each CovariateData so the copy does not
                // share references with the source element.
                element.Covariates.Clear();
                foreach (var cov in Covariates)
                {
                    element.Covariates.Add(new CovariateData { TimeSeriesElement = cov.TimeSeriesElement });
                }

                // Copy forecasting time steps
                element._innerAnalysis.ForecastingTimeSteps = ForecastSteps;

                // Copy plot settings (inside undo suppression â€” matches FittingAnalysis.Copy template)
                if (_timeSeriesPlot != null) PlotSerializer.FromXElement(element._timeSeriesPlot, PlotSerializer.ToXElement(_timeSeriesPlot));
                if (_residualPlot != null) PlotSerializer.FromXElement(element._residualPlot, PlotSerializer.ToXElement(_residualPlot));
                if (_residualHistogramPlot != null) PlotSerializer.FromXElement(element._residualHistogramPlot, PlotSerializer.ToXElement(_residualHistogramPlot));
                if (_residualQQPlot != null) PlotSerializer.FromXElement(element._residualQQPlot, PlotSerializer.ToXElement(_residualQQPlot));
                if (_residualACFPlot != null) PlotSerializer.FromXElement(element._residualACFPlot, PlotSerializer.ToXElement(_residualACFPlot));
                if (_residualPACFPlot != null) PlotSerializer.FromXElement(element._residualPACFPlot, PlotSerializer.ToXElement(_residualPACFPlot));
                _bayesianController.CopyTo(element._bayesianController);

                // Reset the cloned element to a clean post-construction state â€” no inherited
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
        /// Copies the element from an external project file.
        /// </summary>
        public override IElement CopyFromExternal(string itemName, string fullFileName)
        {
            var sqlite = new SQLiteManager(fullFileName);
            var element = new TimeSeriesAnalysis(itemName, ParentCollection);
            element.Open(sqlite);
            return element;
        }

        /// <summary>
        /// Deletes the element from disk and from the parent collection.
        /// </summary>
        public override void Delete()
        {
            if (Name == null) return;
            // Unhook upstream Deleted subscription directly (do not route through the
            // TimeSeriesData setter â€” that would re-add _tsDataNullMsg and re-flip IsDirty=true).
            if (_timeSeriesData != null) _timeSeriesData.Deleted -= OnTimeSeriesDataDeleted;
            // Unhook covariate handlers to prevent memory leaks: each CovariateData wrapper
            // holds a PropertyChanged subscription back to this analysis, and the Covariates
            // collection itself is subscribed via Covariate_CollectionChanged. Without these
            // unsubs, deleting the analysis leaks each wrapper (and transitively this instance).
            if (_covariates != null)
            {
                _covariates.CollectionChanged -= Covariate_CollectionChanged;
                foreach (var cov in _covariates)
                {
                    if (cov != null) cov.PropertyChanged -= Covariate_PropertyChanged;
                }
            }
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
        /// Runs the time series analysis asynchronously.
        /// </summary>
        public async Task RunAsync(SafeProgressReporter progressReporter)
        {
            SetIsValid();
            if (IsValid == false) return;

            _messenger.Add(new BasicMessageItem(MessageType.Event, "The time series analysis for '" + Name + "' has started.", this, ParentCollection.Name, Name, nameof(TimeSeriesAnalysis)));

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
                System.Diagnostics.Debug.WriteLine($"TimeSeriesAnalysis.RunAsync failed for '{Name}': {ex}");
                ClearResults();
                _messenger.Add(new BasicMessageItem(MessageType.Error, $"The time series analysis for '{Name}' failed: {ex.Message}", this, ParentCollection.Name, Name, nameof(TimeSeriesAnalysis)));
            }
            finally
            {
                progressReporter.IndicateTaskEnded();
                SetIsValid();
                _messenger.Add(new BasicMessageItem(MessageType.Event, "The time series analysis for '" + Name + "' is complete.", this, ParentCollection.Name, Name, nameof(TimeSeriesAnalysis)));

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
        /// <see cref="ARIMAX"/> XElement and the rolling snapshot baseline.
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

            var currentSnapshot = _innerAnalysis.ARIMAX.ToXElement();
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
        /// Replays an <see cref="ARIMAX"/> XElement snapshot by reconstructing
        /// <c>_innerAnalysis</c> from the preserved time-series data and the snapshot XML,
        /// then re-establishing the inner-analysis subscription. Used as the do/undo callback
        /// in <see cref="RecordModelUndo"/>.
        /// </summary>
        /// <param name="snapshot">The XElement snapshot to restore.</param>
        private void RestoreModelFromSnapshot(XElement snapshot)
        {
            if (_innerAnalysis != null)
                _innerAnalysis.PropertyChanged -= InnerAnalysis_PropertyChanged;
            var ts = _innerAnalysis?.ARIMAX?.TimeSeries;
            var analysisXml = _innerAnalysis?.ToXElement();
            var newModel = new ARIMAX(ts, snapshot);
            _innerAnalysis = new ModelAnalyses.ARIMAXAnalysis(newModel, analysisXml);
            if (_innerAnalysis.BayesianAnalysis.UseSimulationDefaults)
                _innerAnalysis.BayesianAnalysis.SetDefaultSimulationOptions();
            if (_innerAnalysis.BayesianAnalysis.UseAdvancedSimulationDefaults)
                _innerAnalysis.BayesianAnalysis.SetDefaultAdvancedSimulationOptions();

            // Re-apply covariates to the restored ARIMAX model
            if (_covariates != null && _covariates.Count > 0)
            {
                var covTimeSeries = new System.Collections.Generic.List<Numerics.Data.TimeSeries>();
                foreach (var cov in _covariates)
                {
                    if (cov.TimeSeriesElement?.TimeSeries != null)
                        covTimeSeries.Add(cov.TimeSeriesElement.TimeSeries);
                }
                if (covTimeSeries.Count > 0)
                    _innerAnalysis.ARIMAX.SetCovariates(covTimeSeries);
            }

            _innerAnalysis.PropertyChanged += InnerAnalysis_PropertyChanged;
            SetupBridges();
            RaisePropertyChange(nameof(ARIMAX));
            RaisePropertyChange(nameof(BayesianAnalysis));
            RaisePropertyChange(nameof(IsEstimated));
            RaisePropertyChange(nameof(AnalysisResults));
            RaisePropertyChange(nameof(ForecastSteps));
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
        /// Creates the default time series plot (linear value vs. DateTime axes) for the
        /// observed series + ARIMAX fit overlay.
        /// </summary>
        /// <returns>A new <see cref="Plot"/> configured for time series display.</returns>
        private static Plot CreateDefaultTimeSeriesPlot()
        {
            var plot = new Plot();
            ApplyDefaultPlotStyle(plot);
            plot.Title = "Time Series";
            plot.LegendPosition = OxyPlot.Legends.LegendPosition.TopLeft;
            plot.Axes.Add(new OxyPlot.Wpf.LinearAxis { Key = "Yaxis", Position = OxyPlot.Axes.AxisPosition.Left, Title = "", StringFormat = "N0", AxisTitleDistance = 20, TitleFontSize = 16, FontSize = 12, MajorGridlineStyle = OxyPlot.LineStyle.Solid, MinorGridlineStyle = OxyPlot.LineStyle.None });
            plot.Axes.Add(new OxyPlot.Wpf.DateTimeAxis { Key = "Xaxis", Position = OxyPlot.Axes.AxisPosition.Bottom, Title = "Date", AxisTitleDistance = 15, TitleFontSize = 16, FontSize = 12, MajorGridlineStyle = OxyPlot.LineStyle.Solid, MinorGridlineStyle = OxyPlot.LineStyle.None });
            return plot;
        }

        /// <summary>
        /// Creates the default residual plot (residual vs. fitted value) for ARIMAX
        /// regression diagnostics.
        /// </summary>
        /// <returns>A new <see cref="Plot"/> configured for residual display.</returns>
        private static Plot CreateDefaultResidualPlot()
        {
            var plot = new Plot();
            ApplyDefaultPlotStyle(plot);
            plot.Title = "Residuals";
            plot.Axes.Add(new OxyPlot.Wpf.LinearAxis { Key = "Yaxis", Position = OxyPlot.Axes.AxisPosition.Left, Title = "Residual", AxisTitleDistance = 20, TitleFontSize = 16, FontSize = 12, MajorGridlineStyle = OxyPlot.LineStyle.Solid, MinorGridlineStyle = OxyPlot.LineStyle.None });
            plot.Axes.Add(new OxyPlot.Wpf.LinearAxis { Key = "Xaxis", Position = OxyPlot.Axes.AxisPosition.Bottom, Title = "", StringFormat = "N0", AxisTitleDistance = 20, TitleFontSize = 16, FontSize = 12, MajorGridlineStyle = OxyPlot.LineStyle.Solid, MinorGridlineStyle = OxyPlot.LineStyle.None });
            return plot;
        }

        /// <summary>
        /// Creates the default residual histogram plot for testing residual normality.
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
        /// Creates the default residual Q-Q plot for testing residual normality against
        /// the standard normal distribution.
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
        /// Creates the default residual ACF plot for testing residual independence
        /// (autocorrelation should not be statistically significant at any lag).
        /// </summary>
        /// <returns>A new <see cref="Plot"/> configured for residual ACF display.</returns>
        private static Plot CreateDefaultResidualACFPlot()
        {
            var plot = new Plot();
            ApplyDefaultPlotStyle(plot);
            plot.Title = "Residual Autocorrelation Function";
            plot.Axes.Add(new OxyPlot.Wpf.LinearAxis { Key = "Yaxis", Position = OxyPlot.Axes.AxisPosition.Left, Title = "ACF", AxisTitleDistance = 20, TitleFontSize = 16, FontSize = 12, MajorGridlineStyle = OxyPlot.LineStyle.Solid, MinorGridlineStyle = OxyPlot.LineStyle.None });
            plot.Axes.Add(new OxyPlot.Wpf.LinearAxis { Key = "Xaxis", Position = OxyPlot.Axes.AxisPosition.Bottom, Title = "Lag", AxisTitleDistance = 20, TitleFontSize = 16, FontSize = 12, MajorGridlineStyle = OxyPlot.LineStyle.Solid, MinorGridlineStyle = OxyPlot.LineStyle.None });
            return plot;
        }

        /// <summary>
        /// Creates the default residual PACF plot for testing residual independence
        /// (partial autocorrelation should not be statistically significant at any lag).
        /// </summary>
        /// <returns>A new <see cref="Plot"/> configured for residual PACF display.</returns>
        private static Plot CreateDefaultResidualPACFPlot()
        {
            var plot = new Plot();
            ApplyDefaultPlotStyle(plot);
            plot.Title = "Residual Partial Autocorrelation Function";
            plot.Axes.Add(new OxyPlot.Wpf.LinearAxis { Key = "Yaxis", Position = OxyPlot.Axes.AxisPosition.Left, Title = "PACF", AxisTitleDistance = 20, TitleFontSize = 16, FontSize = 12, MajorGridlineStyle = OxyPlot.LineStyle.Solid, MinorGridlineStyle = OxyPlot.LineStyle.None });
            plot.Axes.Add(new OxyPlot.Wpf.LinearAxis { Key = "Xaxis", Position = OxyPlot.Axes.AxisPosition.Bottom, Title = "Lag", AxisTitleDistance = 20, TitleFontSize = 16, FontSize = 12, MajorGridlineStyle = OxyPlot.LineStyle.Solid, MinorGridlineStyle = OxyPlot.LineStyle.None });
            return plot;
        }

        /// <summary>
        /// Converts a hex color string (e.g. <c>"#8CFFFFFF"</c>) to a <see cref="Color"/>.
        /// </summary>
        /// <param name="hex">The hex color string.</param>
        /// <returns>The parsed <see cref="Color"/>.</returns>
        private static Color ColorFromHex(string hex) => (Color)ColorConverter.ConvertFromString(hex);

        /// <summary>
        /// Deserializes plot settings from a SQLite column into a live <see cref="Plot"/>.
        /// Silently no-ops on null plot, missing column, empty payload, or malformed XML.
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

            _modelSnapshot = _innerAnalysis?.ARIMAX?.ToXElement();

            Func<IUndoManager> getUndo = () => IsUndoEnabled ? UndoManager : null;
            Action onRecorded = () => SetIsDirty(true);

            if (_timeSeriesPlot != null) _timeSeriesPlotUndo = new PlotUndoManager(_timeSeriesPlot, getUndo, "time series plot", this, onRecorded);
            if (_residualPlot != null) _residualPlotUndo = new PlotUndoManager(_residualPlot, getUndo, "residual plot", this, onRecorded);
            if (_residualHistogramPlot != null) _residualHistogramPlotUndo = new PlotUndoManager(_residualHistogramPlot, getUndo, "residual histogram plot", this, onRecorded);
            if (_residualQQPlot != null) _residualQQPlotUndo = new PlotUndoManager(_residualQQPlot, getUndo, "residual QQ plot", this, onRecorded);
            if (_residualACFPlot != null) _residualACFPlotUndo = new PlotUndoManager(_residualACFPlot, getUndo, "residual ACF plot", this, onRecorded);
            if (_residualPACFPlot != null) _residualPACFPlotUndo = new PlotUndoManager(_residualPACFPlot, getUndo, "residual PACF plot", this, onRecorded);

            // Collection bridge for Covariates (Add/Remove/Replace via the CovariateDataGrid).
            if (_covariates != null)
            {
                _covariatesBridge = new UndoableCollectionBridge<CovariateData>(
                    _covariates, getUndo, "covariates", this);
            }

            _bayesianController?.SetupBridges(getUndo, this, onRecorded, _innerAnalysis?.BayesianAnalysis, () => SetIsValid());
        }

        /// <summary>
        /// Handles <see cref="UndoManager.StateChanged"/> to revalidate after undo/redo.
        /// </summary>
        /// <param name="sender">The undo manager raising the event.</param>
        /// <param name="e">Empty event args.</param>
        private void UndoManager_StateChanged(object sender, EventArgs e) { SetIsValid(); }

        /// <summary>
        /// Disposes all undo bridges and unsubscribes <see cref="UndoManager.StateChanged"/>.
        /// Called from <see cref="SetupBridges"/> at the start of bridge re-creation and
        /// from <see cref="Delete"/> to release subscriptions before the element is destroyed.
        /// </summary>
        private void DisposeBridges()
        {
            UndoManager.StateChanged -= UndoManager_StateChanged;
            _timeSeriesPlotUndo?.Dispose(); _timeSeriesPlotUndo = null;
            _residualPlotUndo?.Dispose(); _residualPlotUndo = null;
            _residualHistogramPlotUndo?.Dispose(); _residualHistogramPlotUndo = null;
            _residualQQPlotUndo?.Dispose(); _residualQQPlotUndo = null;
            _residualACFPlotUndo?.Dispose(); _residualACFPlotUndo = null;
            _residualPACFPlotUndo?.Dispose(); _residualPACFPlotUndo = null;
            _covariatesBridge?.Dispose(); _covariatesBridge = null;
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
            if (_timeSeriesPlotUndo != null) suspensions.Add(_timeSeriesPlotUndo.SuspendRecording());
            if (_residualPlotUndo != null) suspensions.Add(_residualPlotUndo.SuspendRecording());
            if (_residualHistogramPlotUndo != null) suspensions.Add(_residualHistogramPlotUndo.SuspendRecording());
            if (_residualQQPlotUndo != null) suspensions.Add(_residualQQPlotUndo.SuspendRecording());
            if (_residualACFPlotUndo != null) suspensions.Add(_residualACFPlotUndo.SuspendRecording());
            if (_residualPACFPlotUndo != null) suspensions.Add(_residualPACFPlotUndo.SuspendRecording());
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
            if (plot == _timeSeriesPlot) _timeSeriesPlotUndo?.RebuildSeriesAndAnnotationBridges();
            else if (plot == _residualPlot) _residualPlotUndo?.RebuildSeriesAndAnnotationBridges();
            else if (plot == _residualHistogramPlot) _residualHistogramPlotUndo?.RebuildSeriesAndAnnotationBridges();
            else if (plot == _residualQQPlot) _residualQQPlotUndo?.RebuildSeriesAndAnnotationBridges();
            else if (plot == _residualACFPlot) _residualACFPlotUndo?.RebuildSeriesAndAnnotationBridges();
            else if (plot == _residualPACFPlot) _residualPACFPlotUndo?.RebuildSeriesAndAnnotationBridges();
            else _bayesianController?.RebuildSeriesAndAnnotationBridges(plot);
        }

        #endregion

    }
}
