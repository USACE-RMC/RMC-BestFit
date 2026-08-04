using DatabaseManager;
using FrameworkInterfaces;
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
    /// UI wrapper for the model-layer <see cref="ModelAnalyses.CompositeAnalysis"/>. Combines
    /// multiple already-fitted univariate analyses into a composite frequency curve via competing
    /// risks, mixture aggregation, or model averaging â€” runs no MCMC chain of its own and
    /// propagates uncertainty by sampling the components' posteriors.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    /// <b>No <see cref="BayesianController"/>:</b> Unlike the other Bayesian analyses
    /// (<c>UnivariateAnalysis</c>, <c>B17CAnalysis</c>, <c>MixtureAnalysis</c>, <c>PointProcessAnalysis</c>,
    /// <c>BivariateAnalysis</c>, <c>TimeSeriesAnalysis</c>, <c>RatingCurveAnalysis</c>), <see cref="CompositeAnalysis"/>
    /// intentionally does not own a <see cref="BayesianController"/> and has no 7-plot Bayesian diagnostic suite.
    /// A composite is a weighted average over already-fitted univariate analyses â€” it runs no MCMC chain of its own,
    /// so there is no Markov chain trace, autocorrelation, or other chain diagnostic to display. Only a single
    /// <c>FrequencyPlot</c> is exposed. The <see cref="BayesianAnalysis"/> property copies in <see cref="Copy"/>
    /// propagate the uncertainty-presentation settings and the posterior-resampling seed,
    /// not any chain or diagnostic state.
    /// </para>
    /// </remarks>
    [Category("General")]
    [DisplayName("Composite Distribution Analysis")]
    [Description("Combine multiple univariate analyses into a composite distribution using competing risks, mixture models, or model averaging.")]
    [Browsable(true)]
    public class CompositeAnalysis : ElementBase, IUnivariate
    {

        #region Construction

        /// <summary>
        /// Constructs a new composite univariate analysis class.
        /// </summary>
        /// <param name="name">The name of the element.</param>
        /// <param name="parentCollection">The parent collection name.</param>
        /// <param name="openFromFile">Optional parameter to open the function from disk upon construction.</param>
        public CompositeAnalysis(string name, IElementCollection parentCollection, bool openFromFile = false) : base(name, parentCollection)
        {
            IsUndoEnabled = false;
            try
            {
                _nameOnDisk = name;
                _creationDate = DateTime.Now;
                _lastModified = DateTime.Now;

                // Create plot objects
                _frequencyPlot = CreateDefaultFrequencyPlot();

                // Create the inner model analysis (ProbabilityOrdinates defaults come from Numerics)
                _innerAnalysis = new ModelAnalyses.CompositeAnalysis();

                // Set up UI analyses collection (tracks UI WeightedUnivariateAnalysis for XAML binding)
                Analyses = new ObservableCollection<WeightedUnivariateAnalysis>();

                // Subscribe to inner analysis events
                SubscribeInnerAnalysis();

                // Add messages
                _descriptionMsg = new BasicMessageItem(MessageType.Message, "The composite distribution analysis does not have a description.", this, ParentCollection.Name, Name, nameof(Description), "CUDA-MSG-001");
                _inputDataInValidMsg = new BasicMessageItem(MessageType.Error, "The selected input data is invalid.", this, ParentCollection.Name, Name, nameof(InputData), "CUDA-ERR-006");
                _sumWeightWrnMsg = new BasicMessageItem(MessageType.Warning, "Weights sum to less than 1. The remaining weight will be assigned to all x-values = 0.", this, ParentCollection.Name, Name, nameof(Analyses), "CUDA-WRN-011");

                // ProbabilityOrdinates, "no analyses defined", weight range, weight sum > 1, and
                // child-analysis-invalid messages are surfaced by the model-layer Validate()
                // routed through _validationAdapter â€” defining duplicates here would produce
                // two messages per error.
                _messages = new List<BasicMessageItem>()
                {   _descriptionMsg,
                    _inputDataInValidMsg,
                };

                // Create messenger
                _messenger = FrameworkInterfaces.Messaging.Messenger.GetInstance();
                _validationAdapter = new ValidationMessageAdapter(_messenger, this, ParentCollection.Name, "CUDA");
                _messenger.Add(_descriptionMsg);

                if (openFromFile == true) Open();
                _nameValid = ValidateName(BestFitProject.InvalidNameCharacters, 50, "CUDA");

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
        /// Gets or sets the element name.
        /// </summary>
        [Category("Meta Data")]
        [DisplayName("Name")]
        [Description("Unique label identifying this composite analysis; max 50 characters.")]
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
                    _nameValid = ValidateName(BestFitProject.InvalidNameCharacters, 50, "CUDA");
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
        [Description("Free-text annotation describing this composite analysis.")]
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
            System.Windows.Application.Current?.TryFindResource("CompositeAnalysisIcon") as System.Windows.Media.ImageSource;

        /// <summary>
        /// Gets the resource key for the element icon, enabling dynamic theme updates.
        /// The project explorer uses <see cref="System.Windows.FrameworkElement.SetResourceReference"/>
        /// with this key so the icon updates automatically when the theme changes.
        /// </summary>
        public string ElementImageResourceKey => "CompositeAnalysisIcon";

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

        /// <summary>
        /// Gets whether this analysis is eligible for batch execution.
        /// Returns <c>true</c> if the analysis is valid, or if the only validation failure
        /// is that component analyses have not yet been estimated. This allows composite
        /// analyses to be included in batch runs where their component analyses will be
        /// estimated first.
        /// </summary>
        public bool IsBatchEligible => IsValid || (_nameValid && _ordinatesValid && _analysesConfigValid);

        #endregion

        #region Fields

        // Error messaging
        /// <summary>The list of message items this element owns; used by the Name setter to bulk-update SourceName.</summary>
        private List<BasicMessageItem> _messages;
        /// <summary>The shared <see cref="FrameworkInterfaces.Messaging.Messenger"/> singleton used to publish messages to the project-wide message bar.</summary>
        private FrameworkInterfaces.Messaging.Messenger _messenger;
        /// <summary>Bridge that maps model-layer <see cref="ModelAnalyses.IAnalysis.Validate"/> results onto messenger entries.</summary>
        private ValidationMessageAdapter _validationAdapter;
        /// <summary>Informational message shown when no <see cref="Description"/> has been set.</summary>
        private BasicMessageItem _descriptionMsg;
        /// <summary>Error message shown when the configured <see cref="InputData"/> is invalid.</summary>
        private BasicMessageItem _inputDataInValidMsg;
        /// <summary>Warning message shown when the sum of component weights is &lt; 1.0.</summary>
        private BasicMessageItem _sumWeightWrnMsg;

        /// <inheritdoc/>
        public ModelAnalyses.IAnalysis InnerAnalysis => _innerAnalysis;

        /// <summary>
        /// The inner model analysis that handles all computation.
        /// </summary>
        private ModelAnalyses.CompositeAnalysis _innerAnalysis;

        /// <summary>
        /// True while model-derived weights are being copied back to the UI rows.
        /// </summary>
        private bool _isSyncingWeightsFromInnerAnalysis;

        /// <summary>
        /// Gets the collection name for composite distribution analyses.
        /// </summary>
        public static string CollectionName => "<Composite Distribution>";

        // Is valid properties
        /// <summary>True when <see cref="Name"/> passes <see cref="ElementBase.ValidateName"/> checks.</summary>
        private bool _nameValid = false;
        /// <summary>True when <see cref="InputData"/> is unset or itself <see cref="IElement.IsValid"/>. InputData is optional for composites â€” defaults to true.</summary>
        private bool _inputDataValid = true;
        /// <summary>True when <see cref="ProbabilityOrdinates"/> are non-empty, strictly increasing, and within [0,1].</summary>
        private bool _ordinatesValid = true;
        /// <summary>True when at least one component <see cref="UnivariateAnalysis"/> has been added AND all components are estimated.</summary>
        private bool _analysesValid = false;
        /// <summary>True when components are added and configured for batch-eligibility checks (estimation status not required).</summary>
        private bool _analysesConfigValid = false;

        // Properties
        /// <summary>Backing field for the optional <see cref="InputData"/> overlay.</summary>
        private InputData _inputData;
        /// <summary>Backing collection for <see cref="Analyses"/>; ObservableCollection so XAML bindings react to add/remove.</summary>
        private ObservableCollection<WeightedUnivariateAnalysis> _analyses = new ObservableCollection<WeightedUnivariateAnalysis>();
        /// <summary>Backing field for <see cref="Dependency"/>; defaults to <see cref="Probability.DependencyType.Independent"/>.</summary>
        private Probability.DependencyType _dependency = Probability.DependencyType.Independent;
        /// <summary>Backing field for <see cref="CompositeDistributionType"/>; defaults to <see cref="ModelAnalyses.CompositeType.CompetingRisks"/>.</summary>
        private ModelAnalyses.CompositeType _compositeDistributionType = ModelAnalyses.CompositeType.CompetingRisks;
        /// <summary>Backing field for <see cref="IsMaximum"/>; defaults true (competing risks selects the per-realization maximum).</summary>
        private bool _isMaximum = true;
        /// <summary>Backing field for <see cref="ModelAverageMethod"/>; defaults to <see cref="ModelAnalyses.AverageMethod.DIC"/>.</summary>
        private ModelAnalyses.AverageMethod _modelAverageMethod = ModelAnalyses.AverageMethod.DIC;

        /// <summary>
        /// The frequency plot showing the composite distribution and data.
        /// </summary>
        private Plot _frequencyPlot;

        /// <summary>
        /// Undo manager for the frequency plot.
        /// </summary>
        private PlotUndoManager _frequencyPlotUndo;

        /// <summary>
        /// Undo bridge for the probability ordinates collection.
        /// </summary>
        private UndoableCollectionBridge<double> _probabilityOrdinatesBridge;

        /// <summary>
        /// Undo bridge for <see cref="BayesianAnalysis"/> result settings the user can edit
        /// from the <c>BayesianOutputControl</c> combos (<c>CredibleIntervalWidth</c>,
        /// <c>OutputLength</c>, <c>PointEstimator</c>, and <c>PRNGSeed</c>). Composite does not run its own MCMC,
        /// so the broader simulation/advanced bridges in <see cref="BayesianController"/> do
        /// not apply â€” only this scoped settings bridge is needed.
        /// </summary>
        private UndoableStateBridge _bayesianSettingsBridge;

        /// <summary>
        /// Rolling XElement baseline of the Analyses collection state.
        /// Captures the weighted analysis list for undo.
        /// </summary>
        private XElement _analysesSnapshot;

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the input data used for graphical comparison with the composite distribution.
        /// </summary>
        [Category("General")]
        [DisplayName("Input Data")]
        [Description("Input data for graphical comparison with the composite distribution.")]
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
        /// Gets the exceedance probability values used for plotting the distribution.
        /// Delegates to the inner analysis's <see cref="Numerics.Data.ProbabilityOrdinates"/> collection.
        /// </summary>
        public ProbabilityOrdinates ProbabilityOrdinates => _innerAnalysis?.ProbabilityOrdinates;

        /// <summary>
        /// Gets or sets how the univariate distributions will be composited.
        /// </summary>
        [Category("Composition")]
        [DisplayName("Composite Type")]
        [Description("Specifies the method for combining univariate distributions.")]
        [Browsable(true)]
        public ModelAnalyses.CompositeType CompositeDistributionType
        {
            get { return _compositeDistributionType; }
            set
            {
                if (_compositeDistributionType != value)
                {
                    var old = _compositeDistributionType;
                    _compositeDistributionType = value;
                    if (_innerAnalysis != null) _innerAnalysis.CompositeDistributionType = value;

                    if (_compositeDistributionType == ModelAnalyses.CompositeType.ModelAverage)
                        SyncAndEstimateModelWeights();
                    ValidateFunctions();
                    if (!UndoManager.IsExecutingAction) ClearResults();
                    RecordPropertyChange(nameof(CompositeDistributionType), old, value);
                }
            }
        }

        /// <summary>
        /// Gets or sets the model averaging method.
        /// </summary>
        [Category("Composition")]
        [DisplayName("Model Averaging Method")]
        [Description("Specifies the method for averaging models using weights based on AIC, BIC, DIC, or RMSE.")]
        [Browsable(true)]
        public ModelAnalyses.AverageMethod ModelAverageMethod
        {
            get { return _modelAverageMethod; }
            set
            {
                if (_modelAverageMethod != value)
                {
                    var old = _modelAverageMethod;
                    _modelAverageMethod = value;
                    if (_innerAnalysis != null) _innerAnalysis.ModelAverageMethod = value;

                    if (_compositeDistributionType == ModelAnalyses.CompositeType.ModelAverage)
                        SyncAndEstimateModelWeights();
                    SetIsValid();
                    if (!UndoManager.IsExecutingAction) ClearResults();
                    RecordPropertyChange(nameof(ModelAverageMethod), old, value);
                }
            }
        }

        /// <summary>
        /// Gets or sets the dependency between random variables when combining as competing risks.
        /// </summary>
        [Category("Composition")]
        [DisplayName("Dependency")]
        [Description("Specifies the statistical dependency between competing distributions. For maximum values, perfect positive dependency yields the upper bound and perfect negative dependency the lower bound; the reverse applies for minimum values.")]
        [Browsable(true)]
        public Probability.DependencyType Dependency
        {
            get { return _dependency; }
            set
            {
                if (_dependency != value)
                {
                    var old = _dependency;
                    _dependency = value;
                    if (_innerAnalysis != null) _innerAnalysis.Dependency = value;
                    SetIsValid();
                    if (!UndoManager.IsExecutingAction) ClearResults();
                    RecordPropertyChange(nameof(Dependency), old, value);
                }
            }
        }

        /// <summary>
        /// Gets or sets the correlation matrix used by correlation-matrix competing-risk dependency.
        /// </summary>
        /// <value>An owned matrix copy, or <see langword="null"/> when no matrix is configured.</value>
        /// <exception cref="ArgumentException">Thrown when the supplied matrix is structurally invalid.</exception>
        /// <remarks>
        /// Matrix editing is not exposed in the current WPF property controls; this property
        /// supports programmatic configuration and project persistence while delegating
        /// validation and ownership to the model-layer analysis.
        /// </remarks>
        [Browsable(false)]
        public double[,] CorrelationMatrix
        {
            get { return _innerAnalysis?.CorrelationMatrix; }
            set
            {
                double[,] old = _innerAnalysis?.CorrelationMatrix;
                if (CorrelationMatricesEqual(old, value)) return;

                _innerAnalysis.CorrelationMatrix = value;
                SetIsValid();
                if (!UndoManager.IsExecutingAction) ClearResults();
                SetIsDirty(true);
                RaisePropertyChange(nameof(CorrelationMatrix));
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the distributions are competing to be maximum or minimum value.
        /// </summary>
        [Category("Composition")]
        [DisplayName("Is Maximum")]
        [Description("Specifies whether the distributions are competing to be the maximum or minimum event.")]
        [Browsable(true)]
        public bool IsMaximum
        {
            get { return _isMaximum; }
            set
            {
                if (_isMaximum != value)
                {
                    var old = _isMaximum;
                    _isMaximum = value;
                    if (_innerAnalysis != null) _innerAnalysis.IsMaximum = value;
                    SetIsValid();
                    if (!UndoManager.IsExecutingAction) ClearResults();
                    RecordPropertyChange(nameof(IsMaximum), old, value);
                }
            }
        }

        /// <summary>
        /// Gets or sets the collection of univariate distribution analysis for the composite distribution.
        /// </summary>
        [Category("Composition")]
        [DisplayName("Univariate Distributions")]
        [Description("Individual univariate analyses and their weights used to construct the composite distribution.")]
        [Browsable(true)]
        public ObservableCollection<WeightedUnivariateAnalysis> Analyses
        {
            get { return _analyses; }
            set
            {
                if (_analyses != null)
                    _analyses.CollectionChanged -= Analyses_CollectionChanged;

                _analyses = value;

                if (_analyses != null)
                    _analyses.CollectionChanged += Analyses_CollectionChanged;

                RaisePropertyChange(nameof(Analyses));
            }
        }

        /// <summary>
        /// Gets the Bayesian Analysis object. Delegates to inner analysis.
        /// </summary>
        public BayesianAnalysis BayesianAnalysis => _innerAnalysis?.BayesianAnalysis;

        /// <summary>
        /// Gets a value indicating whether the analysis has been estimated.
        /// </summary>
        public bool IsEstimated => _innerAnalysis?.IsEstimated ?? false;

        /// <summary>
        /// Gets the frequency analysis results. Delegates to inner analysis.
        /// </summary>
        public UncertaintyAnalysisResults AnalysisResults => _innerAnalysis?.AnalysisResults;

        /// <summary>
        /// Gets the frequency plot showing the composite distribution and data.
        /// </summary>
        public Plot FrequencyPlot => _frequencyPlot;

        /// <summary>
        /// Gets a value indicating whether the analyses collection is valid.
        /// </summary>
        public bool AnalysesValid => _analysesValid;

        #endregion

        #endregion

        #region Methods

        /// <summary>
        /// Handles property changes for input data, validating and clearing results as needed.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        private void InputDataChanged(object sender, PropertyChangedEventArgs e)
        {
            // Check if input data is valid
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
            if (e.PropertyName == nameof(ModelAnalyses.CompositeAnalysis.Analyses))
            {
                return;
            }

            if (e.PropertyName == nameof(ModelAnalyses.CompositeAnalysis.AnalysisResults) &&
                _innerAnalysis.AnalysisResults == null &&
                _innerAnalysis.IsEstimated == false)
            {
                return;
            }

            if (e.PropertyName == nameof(ModelAnalyses.CompositeAnalysis.IsEstimated) ||
                e.PropertyName == nameof(ModelAnalyses.CompositeAnalysis.AnalysisResults) ||
                e.PropertyName == nameof(ModelAnalyses.CompositeAnalysis.BayesianAnalysis))
            {
                SetIsValid();
            }

            // Forward all property names needed by the App controls. Inner Analyses events
            // are suppressed because the UI wrapper raises Analyses for membership and user
            // edits itself; forwarding model child-event Analyses would restyle the grid for
            // every batch child completion.
            RaisePropertyChange(e.PropertyName);
        }

        /// <summary>
        /// Determines whether two optional correlation matrices contain exactly equal values.
        /// </summary>
        /// <param name="left">The first matrix.</param>
        /// <param name="right">The second matrix.</param>
        /// <returns><see langword="true"/> when both matrices are null or exactly equal.</returns>
        private static bool CorrelationMatricesEqual(double[,] left, double[,] right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left == null || right == null) return false;
            if (left.GetLength(0) != right.GetLength(0) || left.GetLength(1) != right.GetLength(1)) return false;

            for (int row = 0; row < left.GetLength(0); row++)
            {
                for (int column = 0; column < left.GetLength(1); column++)
                {
                    if (left[row, column] != right[row, column]) return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Syncs the UI analyses collection to the inner model analysis, then estimates model weights.
        /// Weights are synced back to the UI collection afterward.
        /// </summary>
        private void SyncAndEstimateModelWeights()
        {
            SyncAnalysesToInnerAnalysis();
            _innerAnalysis?.EstimateModelWeights();
            SyncWeightsFromInnerAnalysis();
        }

        /// <summary>
        /// Pushes the UI <see cref="Analyses"/> collection into the inner model analysis.
        /// Each UI <see cref="WeightedUnivariateAnalysis"/> is mapped 1:1 to a model-layer
        /// equivalent by extracting the inner analysis reference. Children whose inner
        /// analysis is itself a <see cref="ModelAnalyses.CompositeAnalysis"/> are mapped
        /// to a null model-layer analysis (composite-of-composite would throw at the
        /// setter); rows with a null UI <see cref="UnivariateAnalysis"/> selection are
        /// also mapped to null so the model-layer <see cref="ModelAnalyses.CompositeAnalysis.Validate"/>
        /// surfaces the "select a univariate analysis" error to the user. Filtering
        /// nulls out here would desync the UI row count from the model row count and
        /// silently hide invalid configurations.
        /// </summary>
        private void SyncAnalysesToInnerAnalysis()
        {
            if (_innerAnalysis == null) return;
            _innerAnalysis.Analyses.Clear();
            foreach (var uiWua in _analyses)
            {
                ModelAnalyses.IUnivariateAnalysis modelAnalysis = null;
                if (uiWua.UnivariateAnalysis?.InnerAnalysis is ModelAnalyses.IUnivariateAnalysis ma &&
                    ma is not ModelAnalyses.CompositeAnalysis)
                {
                    modelAnalysis = ma;
                }
                _innerAnalysis.Analyses.Add(
                    new ModelAnalyses.WeightedUnivariateAnalysis(modelAnalysis, uiWua.Weight));
            }
        }

        /// <summary>
        /// Syncs model weight values back to the UI analyses collection after model estimation.
        /// </summary>
        private void SyncWeightsFromInnerAnalysis()
        {
            if (_innerAnalysis == null) return;
            _isSyncingWeightsFromInnerAnalysis = true;
            try
            {
                for (int i = 0; i < Math.Min(_analyses.Count, _innerAnalysis.Analyses.Count); i++)
                    _analyses[i].Weight = _innerAnalysis.Analyses[i].Weight;
            }
            finally
            {
                _isSyncingWeightsFromInnerAnalysis = false;
            }
        }

        /// <summary>
        /// Refreshes model-average weights after a child analysis reaches a final usable state.
        /// </summary>
        /// <param name="weightedAnalysis">The weighted child row that forwarded the child event.</param>
        private void RefreshModelAverageWeightsFromCompletedChild(WeightedUnivariateAnalysis weightedAnalysis)
        {
            if (_innerAnalysis == null) return;
            if (CompositeDistributionType != ModelAnalyses.CompositeType.ModelAverage) return;
            if (weightedAnalysis?.UnivariateAnalysis?.IsEstimated != true) return;
            if (weightedAnalysis.UnivariateAnalysis.AnalysisResults == null) return;

            if (_innerAnalysis.Analyses.Count != _analyses.Count)
            {
                SyncAndEstimateModelWeights();
                return;
            }

            _innerAnalysis.EstimateModelWeights();
            SyncWeightsFromInnerAnalysis();
        }

        /// <summary>
        /// Pushes UI-owned scalar properties to the inner model analysis.
        /// </summary>
        private void SyncPropertiesToInnerAnalysis()
        {
            if (_innerAnalysis == null) return;
            _innerAnalysis.CompositeDistributionType = _compositeDistributionType;
            _innerAnalysis.ModelAverageMethod = _modelAverageMethod;
            _innerAnalysis.Dependency = _dependency;
            _innerAnalysis.IsMaximum = _isMaximum;
        }

        /// <summary>
        /// Handles changes to the probability ordinates collection, validating and clearing results.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
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
            // validate + message here. See RMC.BestFit.Analyses.CompositeAnalysis
            // .ProbabilityOrdinates_CollectionChanged for the reprocess/clear logic.
            RaisePropertyChange(nameof(ProbabilityOrdinates));
        }

        /// <summary>
        /// Validates the collection of univariate analyses, checking weights and estimation status.
        /// </summary>
        private void ValidateFunctions()
        {
            // Track local config/estimation state for IsValid + IsBatchEligible flags.
            // Diagnostic error messages are surfaced by the model-layer Validate() routed
            // through _validationAdapter inside SetIsValid(). Only the warning that has no
            // model-layer equivalent (sum < 1) is added/removed here.
            _analysesConfigValid = true;
            _analysesValid = true;
            _messenger.Remove(_sumWeightWrnMsg);
            if (Analyses == null || Analyses.Count == 0)
            {
                _analysesConfigValid = false;
                _analysesValid = false;
            }
            else
            {
                double sum = 0;
                foreach (WeightedUnivariateAnalysis wua in Analyses)
                {
                    if (wua.UnivariateAnalysis == null || wua.UnivariateAnalysis.IsValid == false)
                    {
                        _analysesConfigValid = false;
                        _analysesValid = false;
                    }
                    else if (wua.UnivariateAnalysis.BayesianAnalysis.IsEstimated == false)
                    {
                        // Estimation issue only â€” does not affect _analysesConfigValid
                        _analysesValid = false;
                    }
                    if (CompositeDistributionType == ModelAnalyses.CompositeType.Mixture && (wua.Weight <= 0 || wua.Weight >= 1))
                    {
                        _analysesConfigValid = false;
                        _analysesValid = false;
                    }
                    sum += wua.Weight;
                }
                if (CompositeDistributionType == ModelAnalyses.CompositeType.Mixture && sum - 1 > Tools.DoubleMachineEpsilon * 2)
                {
                    _analysesConfigValid = false;
                    _analysesValid = false;
                }
                else if (CompositeDistributionType == ModelAnalyses.CompositeType.Mixture && sum < 1)
                {
                    _messenger.Add(_sumWeightWrnMsg);
                }
            }
            SetIsValid();
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
            { nameof(BayesianAnalysis), typeof(string) },
            { nameof(CompositeDistributionType), typeof(string) },
            { nameof(ModelAverageMethod), typeof(string) },
            { nameof(Dependency), typeof(string) },
            { nameof(IsMaximum), typeof(bool) },
            { nameof(Analyses), typeof(string) },
            { nameof(ProbabilityOrdinates), typeof(string) },
            { nameof(AnalysisResults), typeof(string) },
            { "FrequencyPlotSettings", typeof(string) },
            { "MCMCReport", typeof(string) },
            { nameof(CorrelationMatrix), typeof(string) } };


        /// <summary>
        /// Create SQLite table.
        /// </summary>
        /// <param name="sqlite">The SQLite manager.</param>
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
        /// Opens the element from disk.
        /// </summary>
        /// <param name="sqlite">The SQLite manager.</param>
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
                // General
                if (dtView.ColumnNames.Contains(nameof(Name)))
                {
                    _name = dtView.GetCell(nameof(Name), rowIndex).ToString();
                    foreach (var item in _messages) item.SourceName = _name;
                    _nameValid = ValidateName(BestFitProject.InvalidNameCharacters, 50, "CA");
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
                // Get input data. The overlay is optional, so blank, missing, or unresolved
                // persisted names must leave the backing field null.
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
                // Composite properties
                if (dtView.ColumnNames.Contains(nameof(CompositeDistributionType))) Enum.TryParse(dtView.GetCell(nameof(CompositeDistributionType), rowIndex).ToString(), out _compositeDistributionType);
                if (dtView.ColumnNames.Contains(nameof(ModelAverageMethod))) Enum.TryParse(dtView.GetCell(nameof(ModelAverageMethod), rowIndex).ToString(), out _modelAverageMethod);
                if (dtView.ColumnNames.Contains(nameof(Dependency))) Enum.TryParse(dtView.GetCell(nameof(Dependency), rowIndex).ToString(), out _dependency);
                if (dtView.ColumnNames.Contains(nameof(IsMaximum))) bool.TryParse(dtView.GetCell(nameof(IsMaximum), rowIndex).ToString(), out _isMaximum);
                string correlationMatrixXml = null;
                if (dtView.ColumnNames.Contains(nameof(CorrelationMatrix)))
                    correlationMatrixXml = dtView.GetCell(nameof(CorrelationMatrix), rowIndex)?.ToString();
                // Plot Properties
                DeserializePlotSettings(dtView, rowIndex, "FrequencyPlotSettings", _frequencyPlot);             
                // Get probability ordinates (save to local â€” inner analysis gets reconstructed below)
                string probOrdinatesStr = null;
                if (dtView.ColumnNames.Contains(nameof(ProbabilityOrdinates)))
                    probOrdinatesStr = dtView.GetCell(nameof(ProbabilityOrdinates), rowIndex).ToString();
                // univariate analyses
                if (dtView.ColumnNames.Contains(nameof(Analyses)))
                {
                    try
                    {
                        AnalysesFromXElement(XElement.Parse(dtView.GetCell(nameof(Analyses), rowIndex).ToString()));
                    }
                    catch (Exception ex)
                    {
                        // Couldn't parse the XML. It doesn't necessarily mean an error
                        // since the cell may have never been set on a freshly-saved composite.
                        System.Diagnostics.Debug.WriteLine($"CompositeAnalysis.Open: failed to parse Analyses XElement for '{Name}': {ex.Message}");
                    }
                }

                // Reconstruct inner analysis with saved state
                {
                    // Build XElement for inner analysis constructor
                    var innerXElement = new XElement("CompositeAnalysis",
                        new XAttribute("IsEstimated", false),
                        new XAttribute("CompositeDistributionType", _compositeDistributionType),
                        new XAttribute("ModelAverageMethod", _modelAverageMethod),
                        new XAttribute("Dependency", _dependency),
                        new XAttribute("IsMaximum", _isMaximum));

                    // Add probability ordinates
                    if (!string.IsNullOrEmpty(probOrdinatesStr))
                        innerXElement.Add(new XElement("ProbabilityOrdinates", probOrdinatesStr));

                    if (!string.IsNullOrEmpty(correlationMatrixXml))
                        innerXElement.Add(XElement.Parse(correlationMatrixXml));

                    // Add Bayesian analysis
                    if (dtView.ColumnNames.Contains(nameof(BayesianAnalysis)))
                    {
                        var bayesStr = dtView.GetCell(nameof(BayesianAnalysis), rowIndex).ToString();
                        if (!string.IsNullOrEmpty(bayesStr))
                            innerXElement.Add(XElement.Parse(bayesStr));
                    }

                    // Create analysis resolver that maps names to model-layer
                    // IUnivariateAnalysis instances (UnivariateAnalysis or B17CAnalysis,
                    // explicitly NOT another CompositeAnalysis — that would be
                    // composite-of-composite, which is rejected upstream).
                    Func<string, ModelAnalyses.IUnivariateAnalysis> analysisResolver = (name) =>
                    {
                        foreach (var uiWua in _analyses)
                        {
                            if (uiWua.UnivariateAnalysis?.NameOnDisk == name &&
                                uiWua.UnivariateAnalysis?.InnerAnalysis is ModelAnalyses.IUnivariateAnalysis modelAnalysis &&
                                modelAnalysis is not ModelAnalyses.CompositeAnalysis)
                            {
                                return modelAnalysis;
                            }
                        }
                        return null;
                    };

                    // Reconstruct inner analysis
                    UnsubscribeInnerAnalysis();
                    _innerAnalysis = new ModelAnalyses.CompositeAnalysis(innerXElement, analysisResolver);
                    SubscribeInnerAnalysis();

                    // Sync UI analyses to inner model (innerXElement doesn't include Analyses)
                    SyncAnalysesToInnerAnalysis();
                }

                // Restore analysis results via UncertaintyAnalysisResults.FromXElement.
                // Only the summary curves (ModeCurve, MeanCurve, ConfidenceIntervals) + scalar
                // fit metrics are persisted; per-realisation matrices are never stored, so a
                // small XElement string is sufficient.
                if (dtView.ColumnNames.Contains(nameof(AnalysisResults)))
                {
                    try
                    {
                        var arXml = dtView.GetCell(nameof(AnalysisResults), rowIndex)?.ToString();
                        if (!string.IsNullOrEmpty(arXml))
                        {
                            var results = UncertaintyAnalysisResults.FromXElement(XElement.Parse(arXml));
                            if (results != null)
                                _innerAnalysis.RestoreAnalysisResults(results);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Could not deserialize results â€” will need re-estimation
                        System.Diagnostics.Debug.WriteLine($"CompositeAnalysis.Open: could not deserialize AnalysisResults for '{Name}': {ex.Message}");
                    }
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
        /// Raise preview saved before saving.
        /// </summary>
        /// <param name="cancel">Determines if the save should be canceled.</param>
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
            //int rowIndex = dtView.SearchColumn(0, dtView.NumberOfRows - 1, "Name", NameOnDisk, true, true);
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
            dtView.EditCell(rowIndex, nameof(CompositeDistributionType), CompositeDistributionType);
            dtView.EditCell(rowIndex, nameof(ModelAverageMethod), ModelAverageMethod);        
            dtView.EditCell(rowIndex, nameof(Dependency), Dependency);
            dtView.EditCell(rowIndex, nameof(IsMaximum), IsMaximum);
            dtView.EditCell(rowIndex, nameof(Analyses), AnalysesToXElement().ToString());
            dtView.EditCell(rowIndex, nameof(BayesianAnalysis), BayesianAnalysis?.ToXElement()?.ToString() ?? "");
            dtView.EditCell(rowIndex, nameof(ProbabilityOrdinates), ProbabilityOrdinates?.ToDelimitedString("|") ?? "");
            dtView.EditCell(rowIndex, "FrequencyPlotSettings", _frequencyPlot != null ? PlotSerializer.ToXElement(_frequencyPlot).ToString() : "");
            XElement correlationElement = _innerAnalysis?.ToXElement().Element(nameof(CorrelationMatrix));
            dtView.EditCell(rowIndex, nameof(CorrelationMatrix),
                correlationElement?.ToString(SaveOptions.DisableFormatting) ?? string.Empty);
            // Persist AnalysisResults via XElement round-trip â€” only summary curves + scalar
            // fit metrics; per-realisation matrices are never stored (Numerics's
            // UncertaintyAnalysisResults.ToXElement explicitly excludes parameter sets).
            dtView.EditCell(rowIndex, nameof(AnalysisResults),
                AnalysisResults != null ? AnalysisResults.ToXElement().ToString() : string.Empty);
            

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
        public override IElement Copy(string newName = "")
        {
            var element = new CompositeAnalysis(newName == "" || string.IsNullOrEmpty(newName) ? Name : newName, ParentCollection);

            element.IsUndoEnabled = false;
            try
            {
                element.Description = Description;

                // Copy scalar settings
                element.InputData = InputData;
                element.CompositeDistributionType = CompositeDistributionType;
                element.ModelAverageMethod = ModelAverageMethod;
                element.Dependency = Dependency;
                element.CorrelationMatrix = CorrelationMatrix;
                element.IsMaximum = IsMaximum;

                // Copy Bayesian analysis settings (uncertainty-presentation only â€” CompositeAnalysis runs no MCMC chain itself)
                element._innerAnalysis.BayesianAnalysis.CredibleIntervalWidth = BayesianAnalysis.CredibleIntervalWidth;
                element._innerAnalysis.BayesianAnalysis.OutputLength = BayesianAnalysis.OutputLength;
                element._innerAnalysis.BayesianAnalysis.PointEstimator = BayesianAnalysis.PointEstimator;
                element._innerAnalysis.BayesianAnalysis.PRNGSeed = BayesianAnalysis.PRNGSeed;

                // Copy UI analyses collection
                foreach (WeightedUnivariateAnalysis wua in Analyses)
                {
                    element.Analyses.Add(new WeightedUnivariateAnalysis() { Weight = wua.Weight, UnivariateAnalysis = wua.UnivariateAnalysis });
                }

                // Copy probability ordinates
                if (ProbabilityOrdinates != null && ProbabilityOrdinates.Count > 0)
                    element._innerAnalysis.ProbabilityOrdinates.FromDelimitedString(
                        ProbabilityOrdinates.ToDelimitedString(ProbabilityOrdinates.DefaultDelimiter),
                        ProbabilityOrdinates.DefaultDelimiter);

                // Copy plot settings (inside undo suppression â€” matches FittingAnalysis.Copy template)
                if (_frequencyPlot != null) PlotSerializer.FromXElement(element._frequencyPlot, PlotSerializer.ToXElement(_frequencyPlot));

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
        public override IElement CopyFromExternal(string itemName, string fullFileName)
        {
            // Create SQLite connection
            var sqlite = new SQLiteManager(fullFileName);
            var element = new CompositeAnalysis(itemName, ParentCollection);
            element.Open(sqlite);
            return element;
        }

        /// <summary>
        /// Deletes the element from disk and from the parent element collection.
        /// </summary>
        public override void Delete()
        {
            if (Name == null) return;
            // Unhook upstream Deleted subscriptions directly (do not route through setters â€”
            // those would re-add messages and re-flip IsDirty=true, breaking messenger cleanup
            // and triggering a spurious save prompt on tab close).
            if (_inputData != null) _inputData.Deleted -= OnInputDataDeleted;
            for (int i = 0; i < Analyses.Count; i++)
            {
                if (Analyses[i].UnivariateAnalysis != null)
                    Analyses[i].UnivariateAnalysis.Deleted -= OnComponentDeleted;
            }
            DisposeBridges();
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
        /// Sets the IsValid property for the element. This needs to be called whenever a property is changed.
        /// </summary>
        private void SetIsValid()
        {
            bool valid = true;

            // UI-only validations
            if (_nameValid == false) valid = false;
            if (_inputDataValid == false) valid = false;
            if (_ordinatesValid == false) valid = false;
            if (_analysesValid == false) valid = false;

            // Delegate model validation to inner analysis
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
        /// Handles changes to the analyses collection, attaching property handlers and validating.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        private void Analyses_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // Record analyses collection undo via snapshot
            RecordAnalysesUndo("Analyses");

            if (e.OldItems != null)
            {
                foreach (WeightedUnivariateAnalysis oldItem in e.OldItems)
                {
                    oldItem.PropertyChanged -= WeightedAnalysis_PropertyChanged;
                    if (oldItem.UnivariateAnalysis != null)
                        oldItem.UnivariateAnalysis.Deleted -= OnComponentDeleted;
                    // Drop the wrapper's subscription on the underlying UnivariateAnalysis so
                    // it can be garbage-collected. Without Dispose, the underlying analysis's
                    // PropertyChanged delegate list keeps the wrapper rooted indefinitely.
                    oldItem.Dispose();
                }
            }
            if (e.NewItems != null)
            {
                foreach (WeightedUnivariateAnalysis newItem in e.NewItems)
                {
                    newItem.PropertyChanged += WeightedAnalysis_PropertyChanged;
                    if (newItem.UnivariateAnalysis != null)
                        newItem.UnivariateAnalysis.Deleted += OnComponentDeleted;
                }
            }
            SyncAndEstimateModelWeights();
            ValidateFunctions();
            if (!UndoManager.IsExecutingAction) ClearResults();
            RaisePropertyChange(nameof(Analyses));
        }

        /// <summary>
        /// Handles deletion of a <see cref="UnivariateAnalysis"/> component from the project.
        /// Removes the <see cref="WeightedUnivariateAnalysis"/> wrapper(s) referencing the deleted
        /// component from <see cref="Analyses"/>.
        /// </summary>
        /// <remarks>
        /// The wrapper removal is performed with undo recording disabled so that pressing Undo
        /// cannot re-insert a wrapper pointing at the now-deleted component. Pre-existing undo
        /// history on this composite (weight edits, renames, etc.) is preserved.
        /// </remarks>
        private void OnComponentDeleted(IElement element)
        {
            var wasUndoEnabled = IsUndoEnabled;
            IsUndoEnabled = false;
            try
            {
                for (int i = Analyses.Count - 1; i >= 0; i--)
                {
                    if (ReferenceEquals(Analyses[i].UnivariateAnalysis, element))
                        Analyses.RemoveAt(i);
                }
            }
            finally { IsUndoEnabled = wasUndoEnabled; }
        }

        /// <summary>
        /// Handles property changes for weighted univariate analyses, updating model weights and results.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        private void WeightedAnalysis_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(WeightedUnivariateAnalysis.Weight) &&
                _isSyncingWeightsFromInnerAnalysis)
            {
                return;
            }

            if (e.PropertyName == nameof(WeightedUnivariateAnalysis.UnivariateAnalysis))
            {
                SyncAndEstimateModelWeights();
                if (!UndoManager.IsExecutingAction) ClearResults();
                ValidateFunctions();
                RaisePropertyChange(nameof(Analyses));
                return;
            }

            if (e.PropertyName == nameof(UnivariateAnalysis.AnalysisResults) ||
                e.PropertyName == nameof(UnivariateAnalysis.IsEstimated))
            {
                if (sender is WeightedUnivariateAnalysis weightedAnalysis)
                    RefreshModelAverageWeightsFromCompletedChild(weightedAnalysis);

                if (!UndoManager.IsExecutingAction) ClearResults();
                ValidateFunctions();
                return;
            }

            if (e.PropertyName == nameof(WeightedUnivariateAnalysis.Weight))
            {
                SyncAnalysesToInnerAnalysis();
                // Only record undo for user-edited weights - i.e., the Mixture mode where the
                // grid's Weight column is editable. Model-Average weights are derived from AIC/BIC
                // and Competing-Risks weights are not user-editable, so writes from those paths
                // are programmatic and should not appear on the undo stack.
                if (CompositeDistributionType == ModelAnalyses.CompositeType.Mixture)
                    RecordAnalysesUndo("Weight");
                if (!UndoManager.IsExecutingAction) ClearResults();
                ValidateFunctions();
                RaisePropertyChange(nameof(Analyses));
                return;
            }

            ValidateFunctions();
        }

        /// <summary>
        /// Deserializes the collection of weighted univariate analyses from an XML element.
        /// </summary>
        /// <param name="element">The XML element containing serialized analyses.</param>
        public void AnalysesFromXElement(XElement element)
        {
            if (Analyses != null)
            {
                foreach (var item in _analyses)
                {
                    item.PropertyChanged -= WeightedAnalysis_PropertyChanged;
                }
                Analyses.CollectionChanged -= Analyses_CollectionChanged;
            }
            Analyses.Clear();
            foreach (XElement e in element.Elements(nameof(WeightedUnivariateAnalysis)))
            {
                if (e.Attribute(nameof(WeightedUnivariateAnalysis.UnivariateAnalysis)) == null)
                    continue;
                if (e.Attribute(nameof(WeightedUnivariateAnalysis.Weight)) == null)
                    continue;
                foreach (var hf in ParentCollection)
                {
                    if (hf.Name == e.Attribute(nameof(WeightedUnivariateAnalysis.UnivariateAnalysis)).Value)
                    {
                        // Defensive: ParentCollection holds every IUnivariate subtype
                        // (UnivariateAnalysis, B17CAnalysis, MixtureAnalysis, PointProcessAnalysis,
                        // CompositeAnalysis). If a saved composite refers to a name that, after a
                        // rename or type change, now resolves to a non-IUnivariate sibling, skip
                        // rather than throw — prior versions did `(UnivariateAnalysis)hf` which
                        // crashed the entire project Open path with InvalidCastException.
                        // CompositeAnalysis is also skipped: composite-of-composite is not
                        // supported (would risk circular references / undefined weighting).
                        if (hf is not IUnivariate iu || iu is CompositeAnalysis)
                        {
                            System.Diagnostics.Debug.WriteLine(
                                $"CompositeAnalysis.AnalysesFromXElement: skipping '{hf.Name}' " +
                                $"({hf.GetType().Name}) — not a valid composite child.");
                            continue;
                        }

                        double w;
                        double.TryParse(e.Attribute(nameof(WeightedUnivariateAnalysis.Weight)).Value, NumberStyles.Any, CultureInfo.InvariantCulture, out w);
                        var wHF = new WeightedUnivariateAnalysis() { UnivariateAnalysis = iu, Weight = w };
                        wHF.PropertyChanged += WeightedAnalysis_PropertyChanged;
                        // Subscribe Deleted explicitly here. Analyses.Add fires CollectionChanged,
                        // but the handler is unsubscribed during this loop (line 1345), so the
                        // Analyses_CollectionChanged path that wires Deleted is bypassed.
                        if (wHF.UnivariateAnalysis != null)
                            wHF.UnivariateAnalysis.Deleted += OnComponentDeleted;
                        Analyses.Add(wHF);
                    }
                }
            }
            Analyses.CollectionChanged += Analyses_CollectionChanged;
            ValidateFunctions();
            RaisePropertyChange(nameof(Analyses));
        }

        /// <summary>
        /// Serializes the collection of weighted univariate analyses to an XML element.
        /// </summary>
        /// <returns>An XML element containing the serialized analyses.</returns>
        private XElement AnalysesToXElement()
        {
            if (_analyses == null)  return null;
            var result = new XElement(nameof(Analyses));
            for (int i = 0; i < _analyses.Count; i++)
                result.Add(_analyses[i].ToXElement());
            return result;
        }

        /// <summary>
        /// Clears stale composite analysis results when the inner analysis currently has output.
        /// </summary>
        public void ClearResults()
        {
            if (_innerAnalysis == null) return;
            if (_innerAnalysis.AnalysisResults == null && _innerAnalysis.IsEstimated == false) return;

            _innerAnalysis.ClearResults();
        }

        /// <summary>
        /// Runs the composite distribution analysis asynchronously by delegating to the inner model analysis.
        /// </summary>
        /// <param name="progressReporter">Progress reporter for tracking analysis progress.</param>
        public async Task RunAsync(SafeProgressReporter progressReporter)
        {
            if (IsValid == false) return;

            _messenger.Add(new BasicMessageItem(MessageType.Event, "The composite distribution analysis for '" + Name + "' has started.", this, ParentCollection.Name, Name, nameof(CompositeAnalysis)));

            try
            {
                // Sync all UI state to the inner model before running
                SyncPropertiesToInnerAnalysis();
                SyncAnalysesToInnerAnalysis();

                // Delegate to inner analysis
                progressReporter.IndicateTaskStart();
                await _innerAnalysis.RunAsync(progressReporter);

                // Sync weights back from model (may have been estimated by model)
                SyncWeightsFromInnerAnalysis();

                RaisePropertyChange(nameof(AnalysisResults));
                RaisePropertyChange(nameof(IsEstimated));
                _messenger.Add(new BasicMessageItem(MessageType.Event, "The composite distribution analysis for '" + Name + "' is complete.", this, ParentCollection.Name, Name, nameof(CompositeAnalysis)));
            }
            catch (OperationCanceledException)
            {
                ClearResults();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CompositeAnalysis.RunAsync failed for '{Name}': {ex}");
                ClearResults();
                _messenger.Add(new BasicMessageItem(MessageType.Error, $"The composite distribution analysis for '{Name}' failed: {ex.Message}", this, ParentCollection.Name, Name, nameof(CompositeAnalysis)));
            }
            finally
            {
                progressReporter.IndicateTaskEnded();
                SetIsValid();
                _messenger.Add(new BasicMessageItem(MessageType.Event, "The composite distribution analysis for '" + Name + "' is complete.", this, ParentCollection.Name, Name, nameof(CompositeAnalysis)));
            }
        }

        /// <summary>
        /// Cancel the running analysis. Delegates to the inner model analysis, which
        /// flips the shared <see cref="System.Threading.CancellationTokenSource"/> so the
        /// <c>Parallel.For</c> loops in <c>CreateFrequencyAnalysisResultsAsync</c> observe
        /// the cancellation on their next iteration boundary.
        /// </summary>
        /// <remarks>
        /// Was previously an empty <c>return;</c> stub, which made the App's
        /// CompositeAnalysisPropertiesControl Cancel button a visual-only no-op:
        /// the click reached this method, this method dropped the signal, and the
        /// model-layer simulation kept running until natural completion.
        /// </remarks>
        public void CancelAnalysis()
        {
            _innerAnalysis?.CancelAnalysis();
            SetIsValid();
        }

        /// <summary>
        /// Returns the distribution for a given output index.
        /// </summary>
        /// <param name="index">The output index.</param>
        /// <returns>Always returns null for composite distributions.</returns>
        public UnivariateDistributionBase GetDistribution(int index)
        {
            return _innerAnalysis?.GetDistribution(index);
        }

        /// <summary>
        /// Returns the point estimate distribution from the inner composite analysis.
        /// </summary>
        /// <returns>The point estimate distribution from the inner analysis, or null if not estimated or no inner analysis is set.</returns>
        public UnivariateDistributionBase GetPointEstimateDistribution()
        {
            return _innerAnalysis?.GetPointEstimateDistribution();
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Composite analyses do not own raw paired data â€” their posterior is averaged across
        /// component <see cref="UnivariateAnalysis"/> fits. Returning <c>null</c> excludes them
        /// from the bivariate marginal picker.
        /// </remarks>
        public IUnivariateModel GetMarginalModel()
        {
            return null;
        }

        #endregion

        #region Analyses Undo

        /// <summary>
        /// Records an undo action capturing the difference between the current Analyses
        /// collection (serialized via <see cref="AnalysesToXElement"/>) and the rolling
        /// snapshot baseline.
        /// </summary>
        /// <param name="propertyName">Display name of the property that triggered the change
        /// (e.g. <c>"Analyses"</c>) â€” appears in the undo stack.</param>
        /// <remarks>
        /// Composite uses a different name for its undo pair (<c>RecordAnalyses*</c> rather than
        /// <c>RecordModel*</c>) because the composite's "model" is the weighted set of components
        /// rather than a single distribution XElement. Otherwise functionally analogous to the
        /// pattern in BivariateAnalysis / RatingCurveAnalysis / TimeSeriesAnalysis / PointProcessAnalysis.
        /// </remarks>
        private void RecordAnalysesUndo(string propertyName)
        {
            var undoManager = IsUndoEnabled ? UndoManager : null;
            if (undoManager == null || undoManager.IsExecutingAction) return;
            if (_analysesSnapshot == null) return;

            var currentSnapshot = AnalysesToXElement();
            if (currentSnapshot == null) return;
            if (XNode.DeepEquals(_analysesSnapshot, currentSnapshot)) return;

            var oldSnapshot = _analysesSnapshot;
            var action = new DelegateAction(
                $"Change {propertyName}",
                () => RestoreAnalysesFromSnapshot(currentSnapshot),
                () => RestoreAnalysesFromSnapshot(oldSnapshot),
                this);
            undoManager.RecordAction(action);
            SetIsDirty(true);
            _analysesSnapshot = currentSnapshot;
        }

        /// <summary>
        /// Replays an Analyses XElement snapshot by re-running
        /// <see cref="AnalysesFromXElement(XElement)"/>. Used as the do/undo callback in
        /// <see cref="RecordAnalysesUndo"/>.
        /// </summary>
        /// <param name="snapshot">The XElement snapshot to restore.</param>
        private void RestoreAnalysesFromSnapshot(XElement snapshot)
        {
            AnalysesFromXElement(snapshot);
            SetupBridges();
            RaisePropertyChange(nameof(Analyses));
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
        /// Sets up undo bridges for observable collections and plot objects.
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

            // BayesianAnalysis output settings bridge â€” records user edits to CI width,
            // output length, point estimator, and posterior-resampling seed as undo entries. The BayesianAnalysis
            // setters themselves only RaisePropertyChange (no undo recording); this bridge
            // captures the change externally via INotifyPropertyChanged.
            if (_innerAnalysis?.BayesianAnalysis != null)
            {
                _bayesianSettingsBridge = new UndoableStateBridge(
                    _innerAnalysis.BayesianAnalysis,
                    () => IsUndoEnabled ? UndoManager : null,
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

            // Analyses undo â€” capture baseline snapshot
            _analysesSnapshot = AnalysesToXElement();

            // Plot undo manager for the frequency plot
            Func<IUndoManager> getUndo = () => IsUndoEnabled ? UndoManager : null;
            Action onRecorded = () => SetIsDirty(true);

            if (_frequencyPlot != null)
                _frequencyPlotUndo = new PlotUndoManager(_frequencyPlot, getUndo, "frequency plot", this, onRecorded);
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

            _bayesianSettingsBridge?.Dispose();
            _bayesianSettingsBridge = null;

            _frequencyPlotUndo?.Dispose();
            _frequencyPlotUndo = null;
        }

        /// <summary>
        /// Suspends plot undo bridges to prevent spurious undo entries
        /// during bulk data updates such as plot series population.
        /// </summary>
        /// <returns>An <see cref="IDisposable"/> that resumes recording when disposed.</returns>
        public IDisposable SuspendPlotBridges()
        {
            var suspensions = new List<IDisposable>();
            if (_frequencyPlotUndo != null) suspensions.Add(_frequencyPlotUndo.SuspendRecording());
            return new AggregateDisposable(suspensions);
        }

        /// <summary>
        /// Rebuilds the series and annotation bridges for the specified plot after series/annotation changes.
        /// </summary>
        /// <param name="plot">The plot whose bridges should be rebuilt.</param>
        public void RebuildSeriesAndAnnotationBridges(Plot plot)
        {
            if (plot == _frequencyPlot) _frequencyPlotUndo?.RebuildSeriesAndAnnotationBridges();
        }

        #endregion

    }
}
