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
using OxyPlot.Wpf;
using OxyPlot.Wpf.Serialization;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using System;
using System.Collections.Generic;
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
    /// Bivariate distribution analysis UI wrapper.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    /// This class wraps a <see cref="ModelAnalyses.BivariateAnalysis"/> from the model library,
    /// delegating all computation to the inner analysis while providing UI-specific concerns:
    /// marginal distribution reference management, SQLite persistence, messenger-based validation,
    /// and plot settings.
    /// </para>
    /// </remarks>
    [Category("General"),
    DisplayName("Bivariate Distribution Analysis"),
    Description("Estimates a bivariate distribution using Bayesian MCMC."),
    Browsable(true)]
    public class BivariateAnalysis : ElementBase, IAnalysisElement
    {

        #region Construction

        /// <summary>
        /// Constructs a new bivariate analysis class.
        /// </summary>
        /// <param name="name">The name of the element.</param>
        /// <param name="parentCollection">The parent collection name.</param>
        /// <param name="openFromFile">Optional parameter to open the function from disk upon construction.</param>
        public BivariateAnalysis(string name, IElementCollection parentCollection, bool openFromFile = false) : base(name, parentCollection)
        {
            IsUndoEnabled = false;
            try
            {
                _nameOnDisk = name;
                _creationDate = DateTime.Now;
                _lastModified = DateTime.Now;

                // Create plot objects
                _copulaPlot = CreateDefaultCopulaPlot();
                _bayesianController = new BayesianController();

                // Create the inner model analysis
                var dist = new BivariateDistribution();
                _innerAnalysis = new ModelAnalyses.BivariateAnalysis(dist);

                // Subscribe to inner analysis property changes
                _innerAnalysis.PropertyChanged += InnerAnalysis_PropertyChanged;

                // Add UI-only messages
                _descriptionMsg = new BasicMessageItem(MessageType.Message, "The bivariate distribution analysis does not have a description.", this, ParentCollection.Name, Name, nameof(Description), "BDA-MSG-001");
                _marginalXNullMsg = new BasicMessageItem(MessageType.Error, "Marginal-X is missing. Please select a valid input.", this, ParentCollection.Name, Name, nameof(MarginalX), "BDA-ERR-005");
                _marginalXInValidMsg = new BasicMessageItem(MessageType.Error, "Selected marginal-X is invalid.", this, ParentCollection.Name, Name, nameof(MarginalX), "BDA-ERR-006");
                _marginalYNullMsg = new BasicMessageItem(MessageType.Error, "Marginal-Y is missing. Please select a valid input.", this, ParentCollection.Name, Name, nameof(MarginalY), "BDA-ERR-007");
                _marginalYInValidMsg = new BasicMessageItem(MessageType.Error, "Selected marginal-Y is invalid.", this, ParentCollection.Name, Name, nameof(MarginalY), "BDA-ERR-008");
                _marginalsEqualMsg = new BasicMessageItem(MessageType.Error, "Marginal-X and Marginal-Y cannot be the same.", this, ParentCollection.Name, Name, nameof(MarginalX), "BDA-ERR-009");
                _exactDataInValidMsg = new BasicMessageItem(MessageType.Error, "At least 10 overlapping exact data points are required between marginals.", this, ParentCollection.Name, Name, nameof(MarginalX), "BDA-ERR-010");
                _noOrdinatesMsg = new BasicMessageItem(MessageType.Warning, "There are no x-y data ordinates.", this, ParentCollection.Name, Name, nameof(UncertainOrderedPairedData), "BDA-WRN-011");
                _partialExactDataOverlapMsg = new BasicMessageItem(MessageType.Warning, "Marginal exact data do not perfectly overlap by index. Unpaired observations are excluded from the fit.", this, ParentCollection.Name, Name, nameof(MarginalX), "BDA-WRN-012");
                _badOrdinatesMsg = new BasicMessageItem(MessageType.Error, "Invalid ordinates.", this, ParentCollection.Name, Name, nameof(UncertainOrderedPairedData), "BDA-ERR-011");

                _messages = new List<BasicMessageItem>()
                {   _descriptionMsg,
                    _marginalXNullMsg,
                    _marginalXInValidMsg,
                    _marginalYNullMsg,
                    _marginalYInValidMsg,
                    _marginalsEqualMsg,
                    _exactDataInValidMsg,
                    _partialExactDataOverlapMsg
                };

                // Create messenger and validation adapter
                _messenger = Messenger.GetInstance();
                _validationAdapter = new ValidationMessageAdapter(_messenger, this, ParentCollection.Name, "BDA");
                _messenger.Add(_descriptionMsg);
                _messenger.Add(_marginalXNullMsg);
                _messenger.Add(_marginalYNullMsg);

                // XYOrdinates.CollectionChanged subscription is wired in SetupBridges()
                // (called from the finally block). This keeps subscription lifecycle
                // centralized alongside the other undo bridges.

                // Model constructor creates a default (0,0) row; reconcile messages to state.
                ValidateOrdinates();

                if (openFromFile == true) Open();
                _nameValid = ValidateName(BestFitProject.InvalidNameCharacters, 50, "BDA");

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
        [Description("Unique label identifying this bivariate analysis; max 50 characters.")]
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
                    _nameValid = ValidateName(BestFitProject.InvalidNameCharacters, 50, "BDA");
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
        [Description("Free-text annotation describing this bivariate analysis.")]
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
            System.Windows.Application.Current?.TryFindResource("BivariateAnalysisIcon") as System.Windows.Media.ImageSource;

        /// <summary>
        /// Gets the resource key for the element icon, enabling dynamic theme updates.
        /// The project explorer uses <see cref="System.Windows.FrameworkElement.SetResourceReference"/>
        /// with this key so the icon updates automatically when the theme changes.
        /// </summary>
        public string ElementImageResourceKey => "BivariateAnalysisIcon";

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
        /// The inner model library analysis that performs all computation.
        /// </summary>
        private ModelAnalyses.BivariateAnalysis _innerAnalysis;

        /// <inheritdoc/>
        public ModelAnalyses.IAnalysis InnerAnalysis => _innerAnalysis;

        /// <summary>
        /// The validation message adapter that bridges model validation to UI messaging.
        /// </summary>
        private ValidationMessageAdapter _validationAdapter;

        /// <summary>
        /// List of validation messages for this analysis.
        /// </summary>
        private List<BasicMessageItem> _messages;
        /// <summary>
        /// Messenger instance for broadcasting validation messages.
        /// </summary>
        private Messenger _messenger;
        /// <summary>
        /// Message item for missing description warnings.
        /// </summary>
        private BasicMessageItem _descriptionMsg;
        /// <summary>
        /// Message item for missing ordinate warnings.
        /// </summary>
        private BasicMessageItem _noOrdinatesMsg;
        /// <summary>
        /// Message item for invalid ordinate errors.
        /// </summary>
        private BasicMessageItem _badOrdinatesMsg;

        /// <summary>
        /// Indicates whether the element name is valid.
        /// </summary>
        private bool _nameValid = false;
        /// <summary>
        /// Indicates whether the marginal X distribution is valid.
        /// </summary>
        private bool _marginalXValid = false;
        /// <summary>
        /// Indicates whether the marginal Y distribution is valid.
        /// </summary>
        private bool _marginalYValid = false;
        /// <summary>
        /// Indicates whether the XY ordinates are valid.
        /// </summary>
        private bool _ordinatesValid = true;

        /// <summary>
        /// Tracks whether this element was migrated from a legacy (v1) project file during <see cref="Open()"/>.
        /// When true, the constructor and <see cref="Open()"/> mark the element dirty so the user is prompted
        /// to save the upgraded schema.
        /// </summary>
        private bool openedFromV1 = false;

        /// <summary>
        /// Message item for missing marginal X errors.
        /// </summary>
        private BasicMessageItem _marginalXNullMsg;
        /// <summary>
        /// Message item for invalid marginal X errors.
        /// </summary>
        private BasicMessageItem _marginalXInValidMsg;
        /// <summary>
        /// Message item for missing marginal Y errors.
        /// </summary>
        private BasicMessageItem _marginalYNullMsg;
        /// <summary>
        /// Message item for invalid marginal Y errors.
        /// </summary>
        private BasicMessageItem _marginalYInValidMsg;
        /// <summary>
        /// Message item for when marginals X and Y are the same.
        /// </summary>
        private BasicMessageItem _marginalsEqualMsg;
        /// <summary>
        /// Message item for insufficient overlapping exact data points.
        /// </summary>
        private BasicMessageItem _exactDataInValidMsg;
        /// <summary>
        /// Warning message item raised when marginal exact data are valid for fitting
        /// but do not fully overlap by index.
        /// </summary>
        private BasicMessageItem _partialExactDataOverlapMsg;

        /// <summary>
        /// The marginal distribution for the X variable.
        /// </summary>
        private IUnivariate _marginalX;
        /// <summary>
        /// The marginal distribution for the Y variable.
        /// </summary>
        private IUnivariate _marginalY;

        /// <summary>
        /// The copula plot showing the bivariate distribution and data.
        /// </summary>
        private Plot _copulaPlot;

        /// <summary>
        /// Undo manager for the copula plot.
        /// </summary>
        private PlotUndoManager _copulaPlotUndo;

        /// <summary>
        /// Manages the 7 standard Bayesian MCMC diagnostic plots.
        /// </summary>
        private BayesianController _bayesianController;

        /// <summary>
        /// Rolling XElement baseline of the BivariateDistribution state.
        /// </summary>
        private XElement _modelSnapshot;

        /// <summary>
        /// Rolling XElement baseline of the XYOrdinates state for snapshot-based undo.
        /// </summary>
        private XElement _xyOrdinatesSnapshot;

        /// <summary>
        /// Property names that trigger snapshot comparison.
        /// </summary>
        private static readonly HashSet<string> ModelUndoProperties = new()
        {
            "Parameters", "CopulaType", "CopulaEstimationMethod", "UseJeffreysRuleForScale",
            "EnableQuantilePriors", "QuantilePriors", "UseDefaultFlatPriors"
        };

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the input data used for evaluating the likelihood function.
        /// </summary>
        [Category("General")]
        [DisplayName("Marginal-X")]
        [Description("Fitted univariate analysis providing the X-variable marginal CDF for the copula.")]
        [Browsable(true)]
        public IUnivariate MarginalX
        {
            get { return _marginalX; }
            set
            {
                if (_marginalX == value) return;
                var old = _marginalX;

                if (_marginalX != null)
                {
                    _marginalX.PropertyChanged -= MarginalXChanged;
                    _marginalX.Deleted -= OnMarginalXDeleted;
                }

                _marginalX = value;

                if (_marginalX != null)
                {
                    _marginalX.PropertyChanged += MarginalXChanged;
                    _marginalX.Deleted += OnMarginalXDeleted;
                }

                // Sync marginal to inner analysis model
                if (_innerAnalysis != null && _innerAnalysis.BivariateDistribution != null)
                {
                    var model = MarginalX?.GetMarginalModel();
                    if (model != null)
                        _innerAnalysis.BivariateDistribution.MarginalX = model;
                    else
                        _innerAnalysis.BivariateDistribution.MarginalX = null;
                }

                // Check if marginal is valid
                _marginalXValid = true;
                _messenger.Remove(_marginalXNullMsg);
                _messenger.Remove(_marginalXInValidMsg);
                if (_marginalX == null)
                {
                    _marginalXValid = false;
                    _messenger.Add(_marginalXNullMsg);
                }
                if (_marginalX != null && (_marginalX.IsValid == false || _marginalX.IsEstimated == false))
                {
                    _marginalXValid = false;
                    _messenger.Add(_marginalXInValidMsg);
                }

                SetIsValid();
                if (!UndoManager.IsExecutingAction) ClearResults();
                RecordPropertyChange(nameof(MarginalX), old, value);
            }
        }

        /// <summary>
        /// Gets or sets the input data used for evaluating the likelihood function.
        /// </summary>
        [Category("General")]
        [DisplayName("Marginal-Y")]
        [Description("Fitted univariate analysis providing the Y-variable marginal CDF for the copula.")]
        [Browsable(true)]
        public IUnivariate MarginalY
        {
            get { return _marginalY; }
            set
            {
                if (_marginalY == value) return;
                var old = _marginalY;

                if (_marginalY != null)
                {
                    _marginalY.PropertyChanged -= MarginalYChanged;
                    _marginalY.Deleted -= OnMarginalYDeleted;
                }

                _marginalY = value;

                if (_marginalY != null)
                {
                    _marginalY.PropertyChanged += MarginalYChanged;
                    _marginalY.Deleted += OnMarginalYDeleted;
                }

                // Sync marginal to inner analysis model
                if (_innerAnalysis != null && _innerAnalysis.BivariateDistribution != null)
                {
                    var model = MarginalY?.GetMarginalModel();
                    if (model != null)
                        _innerAnalysis.BivariateDistribution.MarginalY = model;
                    else
                        _innerAnalysis.BivariateDistribution.MarginalY = null;
                }

                // Check if marginal is valid
                _marginalYValid = true;
                _messenger.Remove(_marginalYNullMsg);
                _messenger.Remove(_marginalYInValidMsg);
                if (_marginalY == null)
                {
                    _marginalYValid = false;
                    _messenger.Add(_marginalYNullMsg);
                }
                if (_marginalY != null && (_marginalY.IsValid == false || _marginalY.IsEstimated == false))
                {
                    _marginalYValid = false;
                    _messenger.Add(_marginalYInValidMsg);
                }
                SetIsValid();
                if (!UndoManager.IsExecutingAction) ClearResults();
                RecordPropertyChange(nameof(MarginalY), old, value);
            }
        }

        /// <summary>
        /// Gets and sets the uncertain ordered paired data.
        /// </summary>
        /// <remarks>
        /// This is a UI-owned collection with change handlers. Changes are synced to the inner analysis.
        /// </remarks>
        public UncertainOrderedPairedData XYOrdinates
        {
            get { return _innerAnalysis.XYOrdinates; }
            set
            {
                if (_innerAnalysis.XYOrdinates == null && value == null) return;

                if (_innerAnalysis.XYOrdinates == null || value == null || _innerAnalysis.XYOrdinates.Equals(value) == false)
                {
                    if (_innerAnalysis.XYOrdinates != null)
                        _innerAnalysis.XYOrdinates.CollectionChanged -= XYOrdinates_CollectionChanged;

                    _innerAnalysis.XYOrdinates = value;

                    if (_innerAnalysis.XYOrdinates != null)
                        _innerAnalysis.XYOrdinates.CollectionChanged += XYOrdinates_CollectionChanged;

                    ValidateOrdinates();
                    RaisePropertyChange(nameof(XYOrdinates));
                }
            }
        }

        /// <summary>
        /// The bivariate distribution model (delegates to inner analysis).
        /// </summary>
        public BivariateDistribution BivariateDistribution
        {
            get { return _innerAnalysis.BivariateDistribution; }
        }

        /// <summary>
        /// The Bayesian Analysis object (delegates to inner analysis).
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
        /// The frequency analysis results (delegates to inner analysis).
        /// </summary>
        public UncertaintyAnalysisResults AnalysisResults
        {
            get { return _innerAnalysis.AnalysisResults; }
        }

        /// <summary>
        /// Gets the copula plot showing the bivariate distribution and data.
        /// </summary>
        public Plot CopulaPlot => _copulaPlot;

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
            // Record undo for BivariateDistribution property changes via XElement snapshot
            if (ModelUndoProperties.Contains(e.PropertyName))
            {
                RecordModelUndo(e.PropertyName);
            }

            // Forward relevant property changes to the WPF framework
            if (e.PropertyName == nameof(ModelAnalyses.BivariateAnalysis.IsEstimated) ||
                e.PropertyName == nameof(ModelAnalyses.BivariateAnalysis.AnalysisResults) ||
                e.PropertyName == nameof(ModelAnalyses.BivariateAnalysis.BivariateDistribution) ||
                e.PropertyName == nameof(ModelAnalyses.BivariateAnalysis.BayesianAnalysis))
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
        /// Validates the marginal distributions to ensure they meet analysis requirements.
        /// </summary>
        /// <remarks>
        /// Checks that both marginals are valid, estimated, stationary, not equal to each other,
        /// and have at least 10 overlapping exact data points.
        /// </remarks>
        private void ValidateMarginals()
        {
            // Check if marginal X is valid
            _marginalXValid = true;
            _messenger.Remove(_marginalXNullMsg);
            _messenger.Remove(_marginalXInValidMsg);
            if (_marginalX == null)
            {
                _marginalXValid = false;
                _messenger.Add(_marginalXNullMsg);
            }
            if (_marginalX != null && (_marginalX.IsValid == false || _marginalX.IsEstimated == false))
            {
                _marginalXValid = false;
                _marginalXInValidMsg.Description = "Selected marginal-X is invalid.";
                _messenger.Add(_marginalXInValidMsg);
            }
            if (_marginalX != null && _marginalX.GetMarginalModel()?.IsNonstationary == true)
            {
                _marginalXValid = false;
                _marginalXInValidMsg.Description = "Selected marginal-X must be stationary.";
                _messenger.Add(_marginalXInValidMsg);
            }

            // Check if marginal Y is valid
            _marginalYValid = true;
            _messenger.Remove(_marginalYNullMsg);
            _messenger.Remove(_marginalYInValidMsg);
            if (_marginalY == null)
            {
                _marginalYValid = false;
                _messenger.Add(_marginalYNullMsg);
            }
            if (_marginalY != null && (_marginalY.IsValid == false || _marginalY.IsEstimated == false))
            {
                _marginalYValid = false;
                _marginalYInValidMsg.Description = "Selected marginal-Y is invalid.";
                _messenger.Add(_marginalYInValidMsg);
            }
            if (_marginalY != null && _marginalY.GetMarginalModel()?.IsNonstationary == true)
            {
                _marginalYValid = false;
                _marginalYInValidMsg.Description = "Selected marginal-Y must be stationary.";
                _messenger.Add(_marginalYInValidMsg);
            }

            // Check if marginals are equal
            _messenger.Remove(_marginalsEqualMsg);
            if (_marginalX != null && _marginalY != null && _marginalX.Name == _marginalY.Name)
            {
                _marginalXValid = false;
                _marginalYValid = false;
                _messenger.Add(_marginalsEqualMsg);
            }

            // Check if there are 10 overlapping exact data points
            _messenger.Remove(_exactDataInValidMsg);
            _messenger.Remove(_partialExactDataOverlapMsg);
            var modelX = _marginalX?.GetMarginalModel();
            var modelY = _marginalY?.GetMarginalModel();
            if (modelX != null && modelY != null &&
                modelX.DataFrame != null && modelY.DataFrame != null)
            {
                var counts = BivariateDistribution.GetSampleDataAlignmentCounts();
                if (counts.PairedCount < 10)
                {
                    _marginalXValid = false;
                    _marginalYValid = false;
                    _messenger.Add(_exactDataInValidMsg);
                }
                else if (counts.PairedCount < counts.XExactCount || counts.PairedCount < counts.YExactCount)
                {
                    _partialExactDataOverlapMsg.Description =
                        $"Marginal exact data do not perfectly overlap by index. The fit will use {counts.PairedCount} paired observations ({counts.XExactCount} X exact, {counts.YExactCount} Y exact); unpaired observations are excluded.";
                    _messenger.Add(_partialExactDataOverlapMsg);
                }

            }
        }

        /// <summary>
        /// Validates the X-Y ordinate data to ensure it meets analysis requirements.
        /// </summary>
        /// <remarks>
        /// Checks that ordinates exist and are valid for the analysis.
        /// </remarks>
        private void ValidateOrdinates()
        {
            var target = XYOrdinates;
            _ordinatesValid = true;
            _messenger.Remove(_noOrdinatesMsg);
            _messenger.Remove(_badOrdinatesMsg);
            if (target == null || target.Count < 1)
            {
                _messenger.Add(_noOrdinatesMsg);
            }
            else if (target.IsValid == false)
            {
                _ordinatesValid = false;
                _messenger.Add(_badOrdinatesMsg);
            }
            SetIsValid();
        }

        /// <summary>
        /// Handles property changes from the marginal X distribution.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Property changed event arguments.</param>
        private void MarginalXChanged(object sender, PropertyChangedEventArgs e)
        {
            // Only clear results when the marginal's fit actually changes.
            // Other properties (e.g. ElapsedTime during MCMC) fire on background threads
            // and should not trigger ClearResults or the resulting UI cascade.
            // Guard against undo replay: clearing the bivariate fit on a replay-driven
            // marginal IsEstimated/AnalysisResults change produces asymmetric undo
            // (the UI wrapper consistency rule).
            if ((e.PropertyName == nameof(MarginalX.AnalysisResults) ||
                 e.PropertyName == nameof(MarginalX.IsEstimated))
                && !UndoManager.IsExecutingAction)
            {
                ClearResults();
            }
            SetIsValid();
            RaisePropertyChange(nameof(MarginalX));
        }

        /// <summary>
        /// Handles property changes from the marginal Y distribution.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Property changed event arguments.</param>
        private void MarginalYChanged(object sender, PropertyChangedEventArgs e)
        {
            // Only clear results when the marginal's fit actually changes.
            // Other properties (e.g. ElapsedTime during MCMC) fire on background threads
            // and should not trigger ClearResults or the resulting UI cascade.
            // Same undo-replay guard as MarginalXChanged above.
            if ((e.PropertyName == nameof(MarginalY.AnalysisResults) ||
                 e.PropertyName == nameof(MarginalY.IsEstimated))
                && !UndoManager.IsExecutingAction)
            {
                ClearResults();
            }
            SetIsValid();
            RaisePropertyChange(nameof(MarginalY));
        }

        /// <summary>
        /// Handles deletion of the X marginal from its parent collection. Clears the reference
        /// with undo recording disabled so that pressing Undo cannot resurrect the now-deleted
        /// marginal. Pre-existing undo history on this analysis is preserved.
        /// </summary>
        private void OnMarginalXDeleted(IElement element)
        {
            var wasUndoEnabled = IsUndoEnabled;
            IsUndoEnabled = false;
            try { MarginalX = null; }
            finally { IsUndoEnabled = wasUndoEnabled; }
        }

        /// <summary>
        /// Handles deletion of the Y marginal from its parent collection. Clears the reference
        /// with undo recording disabled so that pressing Undo cannot resurrect the now-deleted
        /// marginal. Pre-existing undo history on this analysis is preserved.
        /// </summary>
        private void OnMarginalYDeleted(IElement element)
        {
            var wasUndoEnabled = IsUndoEnabled;
            IsUndoEnabled = false;
            try { MarginalY = null; }
            finally { IsUndoEnabled = wasUndoEnabled; }
        }

        /// <summary>
        /// Handles collection changes from the X-Y ordinates.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Collection changed event arguments.</param>
        /// <remarks>
        /// Per the model-layer's classification, an ordinate grid edit only affects the
        /// post-processing of an already-fit MCMC chain. Delegates to the inner analysis's
        /// <see cref="RMC.BestFit.Analyses.BivariateAnalysis.ReprocessOrClearXYOrdinates"/>:
        /// re-process derived results in the background if the analysis is estimated and
        /// the new ordinate grid is valid; clear <see cref="RMC.BestFit.Analyses.BivariateAnalysis.AnalysisResults"/>
        /// only if estimated but the grid is invalid; no-op otherwise. Preserves the
        /// underlying MCMC fit either way â€” never calls <see cref="ClearResults"/>
        /// (which would invalidate the fit).
        /// </remarks>
        private void XYOrdinates_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            RecordXYOrdinatesUndo();
            ValidateOrdinates();
            _innerAnalysis.ReprocessOrClearXYOrdinates();
            RaisePropertyChange(nameof(XYOrdinates));
        }

        /// <summary>
        /// Records an XElement-snapshot delta for XYOrdinates row add/remove/replace events.
        /// Mirrors <see cref="RecordModelUndo"/> but operates on the XYOrdinates collection.
        /// </summary>
        /// <remarks>
        /// No-ops when undo is disabled, the manager is mid-execution, or no baseline exists.
        /// Updates the rolling baseline so the next call records the next delta.
        /// </remarks>
        private void RecordXYOrdinatesUndo()
        {
            var undoManager = IsUndoEnabled ? UndoManager : null;
            if (undoManager == null || undoManager.IsExecutingAction) return;
            if (_xyOrdinatesSnapshot == null) return;
            if (_innerAnalysis?.XYOrdinates == null) return;

            var currentSnapshot = _innerAnalysis.XYOrdinates.SaveToXElement();
            if (XNode.DeepEquals(_xyOrdinatesSnapshot, currentSnapshot)) return;

            var oldSnapshot = _xyOrdinatesSnapshot;
            var action = new DelegateAction(
                "Edit XY ordinates",
                () => RestoreXYOrdinatesFromSnapshot(currentSnapshot),
                () => RestoreXYOrdinatesFromSnapshot(oldSnapshot),
                this);
            undoManager.RecordAction(action);
            SetIsDirty(true);
            _xyOrdinatesSnapshot = currentSnapshot;
        }

        /// <summary>
        /// Replays an XYOrdinates XElement snapshot by reconstructing the collection and
        /// re-establishing the CollectionChanged subscription. Used as the do/undo callback
        /// in <see cref="RecordXYOrdinatesUndo"/>.
        /// </summary>
        /// <param name="snapshot">The XElement snapshot to restore.</param>
        private void RestoreXYOrdinatesFromSnapshot(XElement snapshot)
        {
            if (_innerAnalysis == null) return;

            if (_innerAnalysis.XYOrdinates != null)
                _innerAnalysis.XYOrdinates.CollectionChanged -= XYOrdinates_CollectionChanged;

            _innerAnalysis.XYOrdinates = snapshot != null ? new UncertainOrderedPairedData(snapshot) : null;

            if (_innerAnalysis.XYOrdinates != null)
                _innerAnalysis.XYOrdinates.CollectionChanged += XYOrdinates_CollectionChanged;

            _xyOrdinatesSnapshot = _innerAnalysis.XYOrdinates?.SaveToXElement();
            ValidateOrdinates();
            RaisePropertyChange(nameof(XYOrdinates));
        }

        /// <summary>
        /// Updates the IsValid property based on the current validation state of all analysis components.
        /// </summary>
        /// <remarks>
        /// Validates marginals, ordinates, name, and model validation via the inner analysis.
        /// This method should be called whenever any property affecting validity is changed.
        /// </remarks>
        private void SetIsValid()
        {
            ValidateMarginals();

            bool uiValid = true;
            if (_nameValid == false) uiValid = false;
            if (_marginalXValid == false) uiValid = false;
            if (_marginalYValid == false) uiValid = false;
            if (_ordinatesValid == false) uiValid = false;

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
        /// Gets the SQLite table name where this element type's data is stored. Mirrors the
        /// canonical two-table pattern used by <see cref="CompositeAnalysis"/> and
        /// <see cref="UI.UnivariateAnalysis"/>: the parent collection table holds Name+Type
        /// rows for type discrimination; per-element data lives in this dedicated subtype table.
        /// </summary>
        public static string CollectionName => "<Bivariate Distribution>";

        /// <summary>
        /// Gets the required columns for the per-subtype SQLite table.
        /// </summary>
        /// <remarks>
        /// New columns should be added to the end of the dictionary to maintain backward compatibility.
        /// </remarks>
        private static Dictionary<string, Type> RequiredColumns { get; } = new Dictionary<string, Type>() {
            { nameof(Name), typeof(string) },
            { nameof(Description), typeof(string) },
            { nameof(CreationDate), typeof(string) },
            { nameof(LastModified), typeof(string) },
            { nameof(MarginalX), typeof(string) },
            { nameof(MarginalY), typeof(string) },
            { nameof(XYOrdinates), typeof(string) },
            { nameof(BivariateDistribution), typeof(string) },
            { nameof(BayesianAnalysis), typeof(string) },
            { nameof(MCMCResults), typeof(byte[]) },
            { nameof(AnalysisResults), typeof(string) },
            { "CopulaPlotSettings", typeof(string) },
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
        /// Creates or updates the parent collection table (Name + Type discriminator) and the
        /// per-subtype <see cref="CollectionName"/> table for this element type. Mirrors the
        /// canonical two-table pattern used by <see cref="UI.UnivariateAnalysis"/> and
        /// <see cref="CompositeAnalysis"/>.
        /// </summary>
        /// <param name="sqlite">The SQLite database manager instance.</param>
        private void CreateTable(SQLiteManager sqlite)
        {
            // Parent collection table â€” Name + Type discriminator only.
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

            // Per-subtype table â€” full element data.
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
                int columnIndex;
                foreach (KeyValuePair<string, Type> column in RequiredColumns)
                {
                    columnIndex = Array.IndexOf(dt.ColumnNames, column.Key);
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

        /// <summary>
        /// Opens the element from disk.
        /// </summary>
        public override void Open()
        {
            Open(new SQLiteManager(ParentCollection.ParentProject.FullFileName));
        }

        /// <summary>
        /// Opens the element from disk using the specified SQLite database manager.
        /// </summary>
        /// <param name="sqlite">The SQLite database manager instance.</param>
        /// <remarks>
        /// Loads all analysis settings, marginal distributions, ordinates, models, MCMC results,
        /// and plot settings from the database.
        /// </remarks>
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

                // Modern path: per-subtype CollectionName table holds the data. Legacy fallback:
                // pre-migration projects wrote element data directly into the parent collection
                // table â€” read from there and mark the element dirty so the next save migrates
                // the project to the two-table layout.
                DataTableView dtView = null;
                int rowIndex = -1;

                if (sqlite.TableNames.Contains(CollectionName))
                {
                    dtView = sqlite.GetTableManager(CollectionName);
                    rowIndex = dtView.SearchColumn(0, dtView.NumberOfRows - 1, "Name", NameOnDisk, true, true);
                }
                if (rowIndex == -1 && sqlite.TableNames.Contains(ParentCollection.Name))
                {
                    // Legacy single-table layout â€” only fall back if the parent table actually
                    // contains element-data columns (presence of MarginalX is a good sentinel).
                    var legacyDt = sqlite.GetTableManager(ParentCollection.Name);
                    if (legacyDt.ColumnNames.Contains(nameof(MarginalX)))
                    {
                        int legacyRow = legacyDt.SearchColumn(0, legacyDt.NumberOfRows - 1, "Name", NameOnDisk, true, true);
                        if (legacyRow != -1)
                        {
                            openedFromV1 = true;
                            dtView = legacyDt;
                            rowIndex = legacyRow;
                        }
                    }
                }

                if (rowIndex != -1)
                {
                    ReadFromTable(dtView, rowIndex);
                }

                if (wasOpen == false) sqlite.Close();
                SetupBridges();
                SetIsValid();
                SetIsDirty(openedFromV1);

                // Notify WPF bindings that the inner analysis, its collections, and settings were restored from disk.
                RaisePropertyChange(nameof(Name), setDirty: openedFromV1);
                RaisePropertyChange(nameof(XYOrdinates));
                RaisePropertyChange(nameof(MarginalX));
                RaisePropertyChange(nameof(MarginalY));
                RaisePropertyChange(nameof(BayesianAnalysis));
            }
            finally
            {
                IsUndoEnabled = wasUndoEnabled;
                if (wasUndoEnabled) ClearUndoHistory();
            }
        }

        /// <summary>
        /// Reads element data from a SQLite row into backing fields and the inner analysis.
        /// Works against either the per-subtype <see cref="CollectionName"/> table (modern
        /// layout) or the parent collection table (legacy single-table layout) â€” every read
        /// is column-name-based and the column set is identical between the two.
        /// </summary>
        /// <param name="dtView">The table view (modern or legacy).</param>
        /// <param name="rowIndex">The row containing this element's persisted state.</param>
        /// <remarks>
        /// Uses backing fields for scalar properties to avoid repeated SetIsValid()/ClearResults()
        /// cascades during deserialization. A single SetIsValid() runs at the end of <see cref="Open()"/>.
        /// </remarks>
        private void ReadFromTable(DataTableView dtView, int rowIndex)
        {
            if (dtView.ColumnNames.Contains(nameof(Name)))
            {
                _name = dtView.GetCell(nameof(Name), rowIndex).ToString();
                foreach (var item in _messages) item.SourceName = _name;
                _nameValid = ValidateName(BestFitProject.InvalidNameCharacters, 50, "BDA");
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

            // Get marginal X â€” use backing field to avoid SetIsValid(), ClearResults(),
            // and premature inner-analysis sync (the inner analysis is reconstructed below).
            if (dtView.ColumnNames.Contains(nameof(MarginalX)))
            {
                var marginalXName = dtView.GetCell(nameof(MarginalX), rowIndex).ToString();
                foreach (IElementCollection collection in ParentCollection.ParentProject.ElementCollections)
                {
                    if (collection.GetType() == typeof(UnivariateAnalysisCollection))
                    {
                        foreach (IElement element in collection)
                        {
                            if (element.Name == marginalXName
                                && element is IUnivariate uniX
                                && uniX.GetMarginalModel() is not null)
                            {
                                _marginalX = uniX;
                                _marginalX.PropertyChanged += MarginalXChanged;
                                _marginalX.Deleted += OnMarginalXDeleted;
                                _marginalXValid = _marginalX.IsValid && _marginalX.IsEstimated;
                                _messenger.Remove(_marginalXNullMsg);
                                if (!_marginalXValid)
                                    _messenger.Add(_marginalXInValidMsg);
                                else
                                    _messenger.Remove(_marginalXInValidMsg);
                                break;
                            }
                        }
                    }
                }
            }

            // Get marginal Y â€” same backing-field pattern as MarginalX above.
            if (dtView.ColumnNames.Contains(nameof(MarginalY)))
            {
                var marginalYName = dtView.GetCell(nameof(MarginalY), rowIndex).ToString();
                foreach (IElementCollection collection in ParentCollection.ParentProject.ElementCollections)
                {
                    if (collection.GetType() == typeof(UnivariateAnalysisCollection))
                    {
                        foreach (IElement element in collection)
                        {
                            if (element.Name == marginalYName
                                && element is IUnivariate uniY
                                && uniY.GetMarginalModel() is not null)
                            {
                                _marginalY = uniY;
                                _marginalY.PropertyChanged += MarginalYChanged;
                                _marginalY.Deleted += OnMarginalYDeleted;
                                _marginalYValid = _marginalY.IsValid && _marginalY.IsEstimated;
                                _messenger.Remove(_marginalYNullMsg);
                                if (!_marginalYValid)
                                    _messenger.Add(_marginalYInValidMsg);
                                else
                                    _messenger.Remove(_marginalYInValidMsg);
                                break;
                            }
                        }
                    }
                }
            }

            // X-Y Ordinates
            XElement xyOrdinatesXElement = null;
            if (dtView.ColumnNames.Contains(nameof(XYOrdinates)))
            {
                try
                {
                    xyOrdinatesXElement = XElement.Parse(dtView.GetCell(nameof(XYOrdinates), rowIndex).ToString());
                    XYOrdinates = new UncertainOrderedPairedData(xyOrdinatesXElement);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                }
            }

            // Plot settings
            DeserializePlotSettings(dtView, rowIndex, "CopulaPlotSettings", _copulaPlot);
            _bayesianController.Deserialize(dtView, rowIndex);

            // Get BivariateDistribution model XElement
            XElement modelXElement = null;
            if (dtView.ColumnNames.Contains(nameof(BivariateDistribution)) && MarginalX != null && MarginalY != null)
            {
                try { modelXElement = XElement.Parse(dtView.GetCell(nameof(BivariateDistribution), rowIndex).ToString()); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Failed to load BivariateDistribution for '{Name}': {ex.Message}"); }
            }

            MCMCResults mcmcResults = AnalysisPersistenceHelper.TryLoadMCMCResults(dtView, rowIndex, Name);
            XElement innerXElement = AnalysisPersistenceHelper.TryLoadXElement(dtView, "AnalysisXml", rowIndex, Name);

            // Get Bayesian analysis XElement
            XElement analysisXElement = null;
            if (dtView.ColumnNames.Contains(nameof(BayesianAnalysis)))
            {
                var xElementString = dtView.GetCell(nameof(BayesianAnalysis), rowIndex).ToString();
                try { analysisXElement = XElement.Parse(xElementString); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Failed to load BayesianAnalysis for '{Name}': {ex.Message}"); }
            }

            // Reconstruct inner analysis from persisted data.
            // Defensive cleanup: the XYOrdinates property setter (line 1087) subscribed
            // CollectionChanged on the current _innerAnalysis.XYOrdinates. Replacing
            // _innerAnalysis below leaves that subscription orphaned on a now-unreferenced
            // collection. Unsub explicitly before reassignment so SetupBridges() at the
            // end of Open() can wire a single fresh subscription on the new inner analysis.
            _innerAnalysis.PropertyChanged -= InnerAnalysis_PropertyChanged;
            if (_innerAnalysis?.XYOrdinates != null)
                _innerAnalysis.XYOrdinates.CollectionChanged -= XYOrdinates_CollectionChanged;

            UncertaintyAnalysisResults analysisResults =
                AnalysisPersistenceHelper.TryLoadAnalysisResults(dtView, nameof(AnalysisResults), rowIndex, Name);

            if (modelXElement != null && MarginalX?.GetMarginalModel() != null && MarginalY?.GetMarginalModel() != null)
            {
                var dist = new BivariateDistribution(MarginalX.GetMarginalModel()!, MarginalY.GetMarginalModel()!, modelXElement);

                if (innerXElement == null && analysisXElement != null)
                {
                    innerXElement = new XElement("BivariateAnalysis",
                        new XAttribute("IsEstimated", mcmcResults != null));
                    if (XYOrdinates != null)
                        innerXElement.Add(XYOrdinates.SaveToXElement());
                    innerXElement.Add(analysisXElement);
                }

                if (innerXElement != null)
                {
                    _innerAnalysis = new ModelAnalyses.BivariateAnalysis(dist, innerXElement, mcmcResults, analysisResults);
                }
                else
                {
                    _innerAnalysis = new ModelAnalyses.BivariateAnalysis(dist);
                }
            }
            else
            {
                _innerAnalysis = new ModelAnalyses.BivariateAnalysis(new BivariateDistribution());
            }

            // XYOrdinates collection-changed subscription is re-established by SetupBridges()
            // in the calling Open(), which runs after the inner analysis reconstruction.
            _innerAnalysis.PropertyChanged += InnerAnalysis_PropertyChanged;
        }

        /// <summary>
        /// Raises the preview saved event before saving the element.
        /// </summary>
        /// <param name="cancel">Set to true to cancel the save operation.</param>
        public void RaisePreviewSaved(ref bool cancel)
        {
            RaisePreviewObjectSaved(this, ref cancel);
        }

        /// <summary>
        /// Saves the element to disk.
        /// </summary>
        /// <remarks>
        /// Persists all analysis properties, marginal distributions, ordinates, model parameters,
        /// MCMC results, uncertainty results, and plot settings to the SQLite database.
        /// Updates the last modified timestamp.
        /// </remarks>
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

            // Create the parent collection table and the per-subtype data table if needed.
            CreateTable(sqlite);

            // Parent collection row â€” Name + Type discriminator only.
            var dtView = sqlite.GetTableManager(ParentCollection.Name);
            int rowIndex = dtView.SearchColumn(0, dtView.NumberOfRows - 1, "Name", NameOnDisk, true, true);  // B-013: key by Name for parent-table consistency
            if (rowIndex < 0 || rowIndex >= dtView.NumberOfRows)
            {
                dtView.AddRow();
                rowIndex = dtView.NumberOfRows - 1;
            }
            dtView.EditCell(rowIndex, "Name", Name);
            dtView.EditCell(rowIndex, "Type", GetType().ToString());
            dtView.ApplyEdits();

            // Per-subtype data row.
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
            dtView.EditCell(rowIndex, nameof(MarginalX), MarginalX == null ? "" : MarginalX.Name);
            dtView.EditCell(rowIndex, nameof(MarginalY), MarginalY == null ? "" : MarginalY.Name);
            dtView.EditCell(rowIndex, nameof(XYOrdinates), XYOrdinates.SaveToXElement().ToString());
            dtView.EditCell(rowIndex, nameof(BivariateDistribution), _innerAnalysis.BivariateDistribution.ToXElement().ToString());
            dtView.EditCell(rowIndex, nameof(BayesianAnalysis), _innerAnalysis.BayesianAnalysis.ToXElement().ToString());

            dtView.EditCell(rowIndex, nameof(MCMCResults),
                AnalysisPersistenceHelper.SerializeMCMCResults(_innerAnalysis.BayesianAnalysis.Results));
            dtView.EditCell(rowIndex, nameof(AnalysisResults),
                AnalysisPersistenceHelper.SerializeAnalysisResults(_innerAnalysis.AnalysisResults));
            dtView.EditCell(rowIndex, "AnalysisXml", _innerAnalysis?.ToXElement()?.ToString() ?? "");

            dtView.EditCell(rowIndex, "CopulaPlotSettings", _copulaPlot != null ? PlotSerializer.ToXElement(_copulaPlot).ToString() : "");
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
        /// Creates a copy of the element with all its settings.
        /// </summary>
        /// <param name="newName">Optional new name for the copied element. If empty or null, uses the current name.</param>
        /// <returns>A new instance of BivariateAnalysis with copied properties.</returns>
        public override IElement Copy(string newName = "")
        {
            var element = new BivariateAnalysis(newName == "" || string.IsNullOrEmpty(newName) ? Name : newName, ParentCollection);

            element.IsUndoEnabled = false;
            try
            {
                element.Description = Description;

                // Replace the inner analysis with a cloned model
                element._innerAnalysis.PropertyChanged -= element.InnerAnalysis_PropertyChanged;
                var clonedDist = (BivariateDistribution)BivariateDistribution.Clone();
                element._innerAnalysis = new ModelAnalyses.BivariateAnalysis(clonedDist);
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

                // Copy marginals
                element.MarginalX = MarginalX;
                element.MarginalY = MarginalY;

                // Copy ordinates
                element.XYOrdinates = XYOrdinates == null ? null : XYOrdinates.Clone();

                // Copy plot settings (inside undo suppression â€” matches FittingAnalysis.Copy template)
                if (_copulaPlot != null) PlotSerializer.FromXElement(element._copulaPlot, PlotSerializer.ToXElement(_copulaPlot));
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
        /// Copies an element from an external project file into the current project.
        /// </summary>
        /// <param name="itemName">The name of the element to copy from the external project.</param>
        /// <param name="fullFileName">The full file path of the external project database.</param>
        /// <returns>A new instance of BivariateAnalysis loaded from the external project.</returns>
        public override IElement CopyFromExternal(string itemName, string fullFileName)
        {
            // Create SQLite connection
            var sqlite = new SQLiteManager(fullFileName);
            var element = new BivariateAnalysis(itemName, ParentCollection);
            element.Open(sqlite);
            return element;
        }

        /// <summary>
        /// Deletes the element from disk and removes it from the parent collection.
        /// </summary>
        /// <remarks>
        /// Removes the element's row from the SQLite database and clears all validation messages.
        /// </remarks>
        public override void Delete()
        {
            if (Name == null) return;
            // Unhook upstream Deleted subscriptions directly (do not route through the
            // MarginalX / MarginalY setters â€” those would re-add marginal null messages and
            // re-flip IsDirty=true).
            if (_marginalX != null) _marginalX.Deleted -= OnMarginalXDeleted;
            if (_marginalY != null) _marginalY.Deleted -= OnMarginalYDeleted;
            DisposeBridges();
            _bayesianController?.Dispose();
            if (_innerAnalysis != null) _innerAnalysis.PropertyChanged -= InnerAnalysis_PropertyChanged;
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

                // Per-subtype data row.
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
        /// Clears all analysis results by delegating to the inner analysis.
        /// </summary>
        public void ClearResults()
        {
            _innerAnalysis.ClearResults();
        }

        /// <summary>
        /// Executes the bivariate distribution analysis asynchronously.
        /// </summary>
        /// <param name="progressReporter">Progress reporter for tracking analysis progress.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <remarks>
        /// Validates inputs, sends start/complete event messages, and delegates execution
        /// to the inner model library analysis.
        /// </remarks>
        public async Task RunAsync(SafeProgressReporter progressReporter)
        {
            SetIsValid();
            if (IsValid == false) return;

            _messenger.Add(new BasicMessageItem(MessageType.Event, "The bivariate distribution analysis for '" + Name + "' has started.", this, ParentCollection.Name, Name, nameof(BivariateAnalysis)));

            try
            {
                // Set the bivariate sample data
                BivariateDistribution.SetSampleData();

                // Delegate to inner analysis
                progressReporter.IndicateTaskStart();
                await _innerAnalysis.RunAsync(progressReporter);
            }
            catch (OperationCanceledException)
            {
                ClearResults();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"BivariateAnalysis.RunAsync failed for '{Name}': {ex}");
                ClearResults();
                _messenger.Add(new BasicMessageItem(MessageType.Error, $"The bivariate analysis for '{Name}' failed: {ex.Message}", this, ParentCollection.Name, Name, nameof(BivariateAnalysis)));
            }
            finally
            {
                progressReporter.IndicateTaskEnded();
                SetIsValid();
                _messenger.Add(new BasicMessageItem(MessageType.Event, "The bivariate distribution analysis for '" + Name + "' is complete.", this, ParentCollection.Name, Name, nameof(BivariateAnalysis)));
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
        /// <see cref="BivariateDistribution"/> XElement and the rolling snapshot baseline.
        /// </summary>
        /// <param name="propertyName">Display name of the property that triggered the change
        /// (e.g. <c>"BivariateDistribution"</c>) â€” appears in the undo stack.</param>
        /// <remarks>
        /// No-ops when undo is disabled, when the action is already an undo replay, when there
        /// is no baseline yet, or when the model XElement is unchanged. Updates the rolling
        /// baseline to the new snapshot so the next call records the next delta.
        /// </remarks>
        private void RecordModelUndo(string propertyName)
        {
            var undoManager = IsUndoEnabled ? UndoManager : null;
            if (undoManager == null || undoManager.IsExecutingAction) return;
            if (_modelSnapshot == null) return;

            var currentSnapshot = _innerAnalysis.BivariateDistribution.ToXElement();
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
        /// Replays a model XElement snapshot by reconstructing <c>_innerAnalysis</c> with the
        /// preserved marginals and the snapshot's bivariate distribution XML, then re-establishes
        /// the inner-analysis subscription. Used as the do/undo callback in
        /// <see cref="RecordModelUndo"/>.
        /// </summary>
        /// <param name="snapshot">The XElement snapshot to restore.</param>
        private void RestoreModelFromSnapshot(XElement snapshot)
        {
            if (_innerAnalysis != null)
                _innerAnalysis.PropertyChanged -= InnerAnalysis_PropertyChanged;
            var margX = _innerAnalysis?.BivariateDistribution?.MarginalX;
            var margY = _innerAnalysis?.BivariateDistribution?.MarginalY;
            var analysisXml = _innerAnalysis?.ToXElement();
            var newModel = new BivariateDistribution(margX, margY, snapshot);
            _innerAnalysis = new ModelAnalyses.BivariateAnalysis(newModel, analysisXml);
            if (_innerAnalysis.BayesianAnalysis.UseSimulationDefaults)
                _innerAnalysis.BayesianAnalysis.SetDefaultSimulationOptions();
            if (_innerAnalysis.BayesianAnalysis.UseAdvancedSimulationDefaults)
                _innerAnalysis.BayesianAnalysis.SetDefaultAdvancedSimulationOptions();
            _innerAnalysis.PropertyChanged += InnerAnalysis_PropertyChanged;
            SetupBridges();
            RaisePropertyChange(nameof(BivariateDistribution));
            RaisePropertyChange(nameof(BayesianAnalysis));
            RaisePropertyChange(nameof(IsEstimated));
            RaisePropertyChange(nameof(AnalysisResults));
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
        /// Creates the default copula plot with linear axes.
        /// </summary>
        /// <returns>A new copula <see cref="Plot"/> with standard axis configuration.</returns>
        private static Plot CreateDefaultCopulaPlot()
        {
            var plot = new Plot();
            ApplyDefaultPlotStyle(plot);
            plot.Title = "Copula";
            plot.Axes.Add(new OxyPlot.Wpf.LinearAxis
            {
                Key = "Yaxis",
                Position = OxyPlot.Axes.AxisPosition.Left,
                Title = "",
                AxisTitleDistance = 20,
                TitleFontSize = 16,
                FontSize = 12,
                MajorGridlineStyle = OxyPlot.LineStyle.Solid,
                MinorGridlineStyle = OxyPlot.LineStyle.None,
            });
            plot.Axes.Add(new OxyPlot.Wpf.LinearAxis
            {
                Key = "Xaxis",
                Position = OxyPlot.Axes.AxisPosition.Bottom,
                Title = "",
                AxisTitleDistance = 20,
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
            return (Color)ColorConverter.ConvertFromString(hex);
        }

        /// <summary>
        /// Deserializes plot settings from a database column into an existing Plot object.
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
        /// Sets up undo bridges for plot objects and BayesianAnalysis settings.
        /// Disposes any existing bridges before creating new ones.
        /// </summary>
        private void SetupBridges()
        {
            DisposeBridges();

            UndoManager.StateChanged += UndoManager_StateChanged;

            // Subscribe to XYOrdinates collection changes so row edits run ValidateOrdinates().
            // The XYOrdinates property setter handles sub/unsub when the entire collection reference
            // is swapped; this subscription covers the initial reference and reconstructions from Open().
            if (_innerAnalysis?.XYOrdinates != null)
                _innerAnalysis.XYOrdinates.CollectionChanged += XYOrdinates_CollectionChanged;

            // Model undo â€” capture baseline snapshot
            _modelSnapshot = _innerAnalysis?.BivariateDistribution?.ToXElement();

            // XYOrdinates undo â€” capture baseline snapshot for delta-based row-edit undo
            _xyOrdinatesSnapshot = _innerAnalysis?.XYOrdinates?.SaveToXElement();

            // Plot undo managers for element-level plots
            Func<IUndoManager> getUndo = () => IsUndoEnabled ? UndoManager : null;
            Action onRecorded = () => SetIsDirty(true);

            if (_copulaPlot != null)
                _copulaPlotUndo = new PlotUndoManager(_copulaPlot, getUndo, "copula plot", this, onRecorded);

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

            if (_innerAnalysis?.XYOrdinates != null)
                _innerAnalysis.XYOrdinates.CollectionChanged -= XYOrdinates_CollectionChanged;

            _copulaPlotUndo?.Dispose();
            _copulaPlotUndo = null;

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

            if (_copulaPlotUndo != null) suspensions.Add(_copulaPlotUndo.SuspendRecording());
            if (_bayesianController != null) suspensions.Add(_bayesianController.SuspendPlotBridges());

            return new AggregateDisposable(suspensions);
        }

        /// <summary>
        /// Rebuilds the series and annotation bridges for the specified plot after series/annotation changes.
        /// </summary>
        /// <param name="plot">The plot whose bridges should be rebuilt.</param>
        public void RebuildSeriesAndAnnotationBridges(Plot plot)
        {
            if (plot == _copulaPlot) _copulaPlotUndo?.RebuildSeriesAndAnnotationBridges();
            else _bayesianController?.RebuildSeriesAndAnnotationBridges(plot);
        }

        #endregion

    }
}
