using DatabaseManager;
using Numerics;
using Numerics.Data;
using Numerics.Data.Statistics;
using Numerics.Distributions;
using Numerics.Sampling;
using FrameworkInterfaces;
using FrameworkInterfaces.Undo;
using OxyPlot;
using OxyPlot.Wpf;
using OxyPlot.Wpf.Serialization;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Xml.Linq;
using RMC.BestFit.Models;

namespace RMC.BestFit.UI
{
    /// <summary>
    /// Represents an input-data element that hosts a <see cref="DataFrame"/> of exact, uncertain,
    /// interval-censored, and threshold-censored observations for univariate and bivariate
    /// frequency analyses, plus optional time-series-derived series (peaks-over-threshold, block
    /// maxima) sourced from a referenced <see cref="TimeSeriesElement"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// </remarks>
    [Category("General")]
    [DisplayName("Input Data")]
    [Description("A data frame with exact, uncertain, interval-, and threshold-censored data. Used for univariate and bivariate distribution analyses.")]
    [Browsable(true)]
    public class InputData : ElementBase
    {
        #region Construction

        /// <summary>
        /// Constructs a new input data class.
        /// </summary>
        /// <param name="name">The name of the element.</param>
        /// <param name="parentCollection">The parent collection name.</param>
        /// <param name="openFromFile">Optional parameter to open the function from disk upon construction.</param>
        public InputData(string name, IElementCollection parentCollection, bool openFromFile = false) : base(name, parentCollection)
        {
            // Disable undo during construction to prevent initialization from recording actions
            IsUndoEnabled = false;
            try
            {
                _nameOnDisk = name;
                _creationDate = DateTime.Now;
                _lastModified = DateTime.Now;

                // Add messages � RegisterMessage tracks each one in _messages so the Name setter
                // can bulk-update SourceName and we don't risk drift between the per-field
                // declarations and the master list.
                _messages = new List<BasicMessageItem>();
                _descriptionMsg = RegisterMessage(MessageType.Message, "The input data does not have a description.", nameof(Description), "ID-MSG-001");
                _unitLabelMsg = RegisterMessage(MessageType.Error, "The unit label cannot be empty.", nameof(UnitLabel), "ID-ERR-005");
                _indexLabelMsg = RegisterMessage(MessageType.Error, "The index label cannot be empty.", nameof(IndexLabel), "ID-ERR-006");
                _siteNumberMsg = RegisterMessage(MessageType.Error, "The USGS site number must be 8 digits long.", nameof(USGSSiteNumber), "ID-ERR-007");
                _dataFrameMsg = RegisterMessage(MessageType.Error, "At least 10 data points are required (exact, uncertain, or interval).", nameof(DataFrame), "ID-ERR-008");
                _timeSeriesNullMsg = RegisterMessage(MessageType.Error, "Time series not selected. Please choose a valid time series.", nameof(TimeSeriesElement), "ID-ERR-013");
                _timeSeriesInValidMsg = RegisterMessage(MessageType.Error, "The selected time series is invalid.", nameof(TimeSeriesElement), "ID-ERR-014");
                _startMonthMsg = RegisterMessage(MessageType.Error, "The start month must be between 1 and 12.", nameof(StartMonth), "ID-ERR-015");
                _endMonthMsg = RegisterMessage(MessageType.Error, "The end month must be between 1 and 12.", nameof(EndMonth), "ID-ERR-016");
                _periodMsg = RegisterMessage(MessageType.Error, "The smoothing period must be non-negative and not exceed the time series length.", nameof(Period), "ID-ERR-017");
                _potThresholdMsg = RegisterMessage(MessageType.Error, "The threshold must be less than the max of the time series values.", nameof(Threshold), "ID-ERR-018");
                _minStepsMsg = RegisterMessage(MessageType.Error, "The minimum steps between peaks must be non-negative and not exceed the time series length.", nameof(MinStepsBetweenPeaks), "ID-ERR-019");
                _partialBlockSeriesMsg = RegisterMessage(MessageType.Warning, "The selected time series contains missing values, gaps, or incomplete block years. Block-series results may be biased; consider trimming to complete block years or filling missing data before processing.", nameof(TimeSeriesElement), "ID-WNG-020");

                // Create messenger and validation adapter
                _messenger = FrameworkInterfaces.Messaging.Messenger.GetInstance();
                _validationAdapter = new ValidationMessageAdapter(_messenger, this, ParentCollection.Name, "ID");
                _messenger.Add(_descriptionMsg);
                _messenger.Add(_dataFrameMsg);

                _dataFrame.PropertyChanged += DataFramePropertyChanged;

                // Create default plots
                _chronologyPlot = CreateDefaultChronologyPlot();
                _frequencyPlot = CreateDefaultFrequencyPlot();
                _seasonalityPlot = CreateDefaultSeasonalityPlot();
                _densityPlot = CreateDefaultDensityPlot();
                _histogramPlot = CreateDefaultHistogramPlot();
                _qqPlot = CreateDefaultQQPlot();
                _acfPlot = CreateDefaultACFPlot();
                _pacfPlot = CreateDefaultPACFPlot();
                _mrlPlot = CreateDefaultMRLPlot();
                _modifiedScalePlot = CreateDefaultModifiedScalePlot();
                _shapePlot = CreateDefaultShapePlot();

                if (openFromFile == true) Open();

                _nameValid = ValidateName(BestFitProject.InvalidNameCharacters, 50, "ID");
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
        [Description("Unique label identifying this input data element; max 50 characters.")]
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
                    _nameValid = ValidateName(BestFitProject.InvalidNameCharacters, 50, "ID");
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
        [Description("Free-text annotation describing this input data element.")]
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
        [Description("The date and time when the input data was first created.")]
        [Browsable(true)]
        public override DateTime CreationDate => _creationDate;

        /// <summary>
        /// Gets and sets the date when the element was last modified.
        /// </summary>
        [Category("Meta Data")]
        [DisplayName("Last Edited")]
        [Description("The date and time when the input data was last modified.")]
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
            System.Windows.Application.Current?.TryFindResource("InputDataIcon") as System.Windows.Media.ImageSource;

        /// <summary>
        /// Gets the resource key for the theme-aware element icon.
        /// The project explorer uses <see cref="System.Windows.FrameworkElement.SetResourceReference"/>
        /// with this key so the icon updates automatically when the theme changes.
        /// </summary>
        public string ElementImageResourceKey => "InputDataIcon";

        /// <summary>
        /// Determines if the element can be copied from an external application.
        /// </summary>
        public override bool CanCopyFromExternal
        {
            get { return ExactDataMethod == ExactDataEntryType.Manual ? true : false; }
        }

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
        /// Collection of all validation message items for this element.
        /// </summary>
        private List<BasicMessageItem> _messages;

        /// <summary>
        /// Messenger instance for publishing validation messages.
        /// </summary>
        private FrameworkInterfaces.Messaging.Messenger _messenger;

        /// <summary>
        /// Bridges model library DataFrame validation messages to the UI messaging system.
        /// </summary>
        private ValidationMessageAdapter _validationAdapter;

        /// <summary>
        /// Validation message for missing description.
        /// </summary>
        private BasicMessageItem _descriptionMsg;

        /// <summary>
        /// Validation message for invalid unit label.
        /// </summary>
        private BasicMessageItem _unitLabelMsg;

        /// <summary>
        /// Validation message for invalid index label.
        /// </summary>
        private BasicMessageItem _indexLabelMsg;

        /// <summary>
        /// Validation message for invalid USGS site number.
        /// </summary>
        private BasicMessageItem _siteNumberMsg;

        /// <summary>
        /// Validation message for insufficient data frame records.
        /// </summary>
        private BasicMessageItem _dataFrameMsg;

        /// <summary>
        /// Validation message for null time series element.
        /// </summary>
        private BasicMessageItem _timeSeriesNullMsg;

        /// <summary>
        /// Validation message for invalid time series element.
        /// </summary>
        private BasicMessageItem _timeSeriesInValidMsg;

        /// <summary>
        /// Validation message for invalid start month.
        /// </summary>
        private BasicMessageItem _startMonthMsg;

        /// <summary>
        /// Validation message for invalid end month.
        /// </summary>
        private BasicMessageItem _endMonthMsg;

        /// <summary>
        /// Validation message for invalid period.
        /// </summary>
        private BasicMessageItem _periodMsg;

        /// <summary>
        /// Validation message for invalid POT threshold.
        /// </summary>
        private BasicMessageItem _potThresholdMsg;

        /// <summary>
        /// Validation message for invalid minimum steps between peaks.
        /// </summary>
        private BasicMessageItem _minStepsMsg;

        /// <summary>
        /// Warning message for block-series source data with missing values, gaps, or partial boundary blocks.
        /// </summary>
        private BasicMessageItem _partialBlockSeriesMsg;

        /// <summary>
        /// Indicates whether the file was opened from version 1.0 format.
        /// </summary>
        private bool openedFromV1 = false;

        /// <summary>
        /// Indicates whether the element name is valid.
        /// </summary>
        private bool _nameValid = false;

        /// <summary>
        /// Indicates whether the unit label is valid.
        /// </summary>
        private bool _unitLabelValid = true;

        /// <summary>
        /// Indicates whether the index label is valid.
        /// </summary>
        private bool _indexLabelValid = true;

        /// <summary>
        /// Indicates whether the USGS site number is valid.
        /// </summary>
        private bool _siteNumberValid = true;

        /// <summary>
        /// Indicates whether the start month is valid.
        /// </summary>
        private bool _startMonthValid = true;

        /// <summary>
        /// Indicates whether the end month is valid.
        /// </summary>
        private bool _endMonthValid = true;

        /// <summary>
        /// Indicates whether the period is valid.
        /// </summary>
        private bool _periodValid = true;

        /// <summary>
        /// The data frame containing exact, uncertain, interval, and threshold data series.
        /// </summary>
        private DataFrame _dataFrame = new DataFrame();

        /// <summary>
        /// The method used for entering exact data (manual, block series, POT, or USGS download).
        /// </summary>
        private ExactDataEntryType _exactDataMethod = ExactDataEntryType.Manual;

        /// <summary>
        /// The factory-default data unit label.
        /// </summary>
        private const string DefaultUnitLabel = "Value";

        /// <summary>
        /// The factory-default data index label.
        /// </summary>
        private const string DefaultIndexLabel = "Year";

        /// <summary>
        /// The label for the value (Y) axis on plots.
        /// </summary>
        private string _unitLabel = DefaultUnitLabel;

        /// <summary>
        /// The label for the index (X) axis on plots.
        /// </summary>
        private string _indexLabel = DefaultIndexLabel;

        /// <summary>
        /// The USGS site number for downloading peak data.
        /// </summary>
        private string _usgsSiteNumber = "00000000";

        /// <summary>
        /// Whether to apply the Multiple Grubbs-Beck Test for low outlier detection.
        /// </summary>
        private bool _useMultipleGrubbsBeckTest = true;

        /// <summary>
        /// The selected time series element for block series or POT extraction.
        /// </summary>
        private TimeSeriesElement _timeSeriesElement;

        /// <summary>
        /// The block function type (e.g., Maximum, Minimum) for block series extraction.
        /// </summary>
        private BlockFunctionType _blockFunction = BlockFunctionType.Maximum;

        /// <summary>
        /// The time block window type (e.g., CalendarYear, WaterYear) for block series extraction.
        /// </summary>
        private TimeBlockWindow _timeBlock = TimeBlockWindow.CalendarYear;

        /// <summary>
        /// The start month for custom year time blocks.
        /// </summary>
        private int _startMonth = 10;

        /// <summary>
        /// The end month for custom year time blocks.
        /// </summary>
        private int _endMonth = 9;

        /// <summary>
        /// The smoothing function applied during series extraction.
        /// </summary>
        private SmoothingFunctionType _smoothingFunction = SmoothingFunctionType.None;

        /// <summary>
        /// The recurrence period for block series extraction.
        /// </summary>
        private int _period = 1;

        /// <summary>
        /// The threshold value for peaks-over-threshold extraction.
        /// </summary>
        private double _threshold = 0;

        /// <summary>
        /// The minimum number of time steps between peaks for POT extraction.
        /// </summary>
        private int _minStepsBetweenPeaks = 1;

        /// <summary>
        /// Indicates whether the data has been processed (block series or POT created).
        /// </summary>
        private bool _isProcessed = false;

        /// <summary>
        /// The chronology plot showing data values over time.
        /// </summary>
        private Plot _chronologyPlot;

        /// <summary>
        /// The frequency plot showing exceedance probability vs. value.
        /// </summary>
        private Plot _frequencyPlot;

        /// <summary>
        /// The seasonality plot showing circular statistics of event timing.
        /// </summary>
        private Plot _seasonalityPlot;

        /// <summary>
        /// The kernel density estimation plot.
        /// </summary>
        private Plot _densityPlot;

        /// <summary>
        /// The histogram plot of the data.
        /// </summary>
        private Plot _histogramPlot;

        /// <summary>
        /// The quantile-quantile plot comparing data to a theoretical distribution.
        /// </summary>
        private Plot _qqPlot;

        /// <summary>
        /// The autocorrelation function plot.
        /// </summary>
        private Plot _acfPlot;

        /// <summary>
        /// The partial autocorrelation function plot.
        /// </summary>
        private Plot _pacfPlot;

        /// <summary>
        /// The mean residual life plot for POT threshold diagnostics.
        /// </summary>
        private Plot _mrlPlot;

        /// <summary>
        /// The modified scale parameter stability plot for POT threshold diagnostics.
        /// </summary>
        private Plot _modifiedScalePlot;

        /// <summary>
        /// The shape parameter stability plot for POT threshold diagnostics.
        /// </summary>
        private Plot _shapePlot;

        /// <summary>
        /// Undo manager for the chronology plot visual properties.
        /// </summary>
        private PlotUndoManager _chronologyPlotUndo;

        /// <summary>
        /// Undo manager for the frequency plot visual properties.
        /// </summary>
        private PlotUndoManager _frequencyPlotUndo;

        /// <summary>
        /// Undo manager for the seasonality plot visual properties.
        /// </summary>
        private PlotUndoManager _seasonalityPlotUndo;

        /// <summary>
        /// Undo manager for the density plot visual properties.
        /// </summary>
        private PlotUndoManager _densityPlotUndo;

        /// <summary>
        /// Undo manager for the histogram plot visual properties.
        /// </summary>
        private PlotUndoManager _histogramPlotUndo;

        /// <summary>
        /// Undo manager for the QQ plot visual properties.
        /// </summary>
        private PlotUndoManager _qqPlotUndo;

        /// <summary>
        /// Undo manager for the ACF plot visual properties.
        /// </summary>
        private PlotUndoManager _acfPlotUndo;

        /// <summary>
        /// Undo manager for the PACF plot visual properties.
        /// </summary>
        private PlotUndoManager _pacfPlotUndo;

        /// <summary>
        /// Undo manager for the MRL plot visual properties.
        /// </summary>
        private PlotUndoManager _mrlPlotUndo;

        /// <summary>
        /// Undo manager for the modified scale plot visual properties.
        /// </summary>
        private PlotUndoManager _modifiedScalePlotUndo;

        /// <summary>
        /// Undo manager for the shape plot visual properties.
        /// </summary>
        private PlotUndoManager _shapePlotUndo;

        /// <summary>
        /// Undo bridge for DataFrame scalar property changes (LowOutlierThreshold, PlottingParameter).
        /// </summary>
        private UndoableStateBridge _dataFrameBridge;

        /// <summary>
        /// Undo bridge for exact data series collection changes.
        /// </summary>
        private UndoableCollectionBridge<Data> _exactSeriesBridge;

        /// <summary>
        /// Undo bridge for uncertain data series collection changes.
        /// </summary>
        private UndoableCollectionBridge<Data> _uncertainSeriesBridge;

        /// <summary>
        /// Undo bridge for interval data series collection changes.
        /// </summary>
        private UndoableCollectionBridge<Data> _intervalSeriesBridge;

        /// <summary>
        /// Undo bridge for threshold data series collection changes.
        /// </summary>
        private UndoableCollectionBridge<Data> _thresholdSeriesBridge;

        /// <summary>
        /// Enumeration of exact data entry options.
        /// </summary>
        public enum ExactDataEntryType
        {
            /// <summary>
            /// Exact observations are entered directly by the user.
            /// </summary>
            Manual,
            /// <summary>
            /// Exact observations are derived from block maxima in a time series.
            /// </summary>
            BlockSeries,
            /// <summary>
            /// Exact observations are derived from peaks over a threshold in a time series.
            /// </summary>
            PeaksOverThresholdSeries,
            /// <summary>
            /// Exact observations are imported from USGS peak discharge data.
            /// </summary>
            USGSPeakDischarge,
            /// <summary>
            /// Exact observations are imported from USGS peak stage data.
            /// </summary>
            USGSPeakStage
        }

        #endregion

        #region Properties

        /// <summary>
        /// Determines how the exact data is entered.
        /// </summary>
        [Category("General")]
        [DisplayName("Exact Data Entry Method")]
        [Description("Select whether exact data is manually entered, downloaded from USGS, or derived from a time series.")]
        [Browsable(true)]
        public ExactDataEntryType ExactDataMethod
        {
            get { return _exactDataMethod; }
            set
            {
                if (_exactDataMethod != value)
                {
                    var old = _exactDataMethod;
                    _exactDataMethod = value;
                    SetIsValid();
                    RecordPropertyChange(nameof(ExactDataMethod), old, value);
                }
            }
        }

        /// <summary>
        /// The input data frame. 
        /// </summary>
        [Category("General")]
        [DisplayName("Data Frame")]
        [Description("Contains exact, uncertain, interval-, and threshold-censored data.")]
        [Browsable(true)]
        public DataFrame DataFrame
        {
            get { return _dataFrame; }
            set
            {
                if (_dataFrame != value)
                {
                    if (_dataFrame != null)
                        _dataFrame.PropertyChanged -= DataFramePropertyChanged;
                    _dataFrame = value;
                    if (_dataFrame != null)
                    {
                        _dataFrame.PropertyChanged += DataFramePropertyChanged;
                        SetupBridges();
                    }
                    SetIsValid();
                    RaisePropertyChange(nameof(DataFrame));
                }
            }
        }

        /// <summary>
        /// The data unit label.
        /// </summary>
        [Category("General")]
        [DisplayName("Unit Label")]
        [Description("The value units, e.g., Water Surface Elevation (FT), Flow (CFS), or Rainfall (IN).")]
        [Browsable(true)]
        public string UnitLabel
        {
            get { return _unitLabel; }
            set
            {
                if (_unitLabel != value)
                {
                    var old = _unitLabel;
                    _unitLabel = value;

                    _unitLabelValid = true;
                    _messenger.Remove(_unitLabelMsg);
                    if (_unitLabel == "" || _unitLabel == null)
                    {
                        _unitLabelValid = false;
                        _messenger.Add(_unitLabelMsg);
                    }

                    using (SuspendPlotBridges())
                    {
                        UpdatePlotYAxisTitles(old);
                    }
                    SetIsValid();
                    RecordPropertyChange(nameof(UnitLabel), old, value);
                }
            }
        }

        /// <summary>
        /// The data index label.
        /// </summary>
        [Category("General")]
        [DisplayName("Index Label")]
        [Description("Specifies the index units, e.g., Year, Month, etc.")]
        [Browsable(true)]
        public string IndexLabel
        {
            get { return _indexLabel; }
            set
            {
                if (_indexLabel != value)
                {
                    var old = _indexLabel;
                    _indexLabel = value;

                    _indexLabelValid = true;
                    _messenger.Remove(_indexLabelMsg);
                    if (_indexLabel == "" || _indexLabel == null)
                    {
                        _indexLabelValid = false;
                        _messenger.Add(_indexLabelMsg);
                    }

                    using (SuspendPlotBridges())
                    {
                        UpdatePlotXAxisTitles(old);
                    }
                    SetIsValid();
                    RecordPropertyChange(nameof(IndexLabel), old, value);
                }
            }
        }

        /// <summary>
        /// Determines whether to use the Multiple Grubbs-Beck Test for low outliers.
        /// </summary>
        [Category("Low Outlier Tests")]
        [DisplayName("Multiple Grubbs-Beck Test")]
        [Description("Toggle the Multiple Grubbs-Beck Test for detecting low outliers.")]
        [Browsable(true)]
        public bool UseMultipleGrubbsBeckTest
        {
            get { return _useMultipleGrubbsBeckTest; }
            set
            {
                if (_useMultipleGrubbsBeckTest != value)
                {
                    var old = _useMultipleGrubbsBeckTest;
                    _useMultipleGrubbsBeckTest = value;
                    RecordPropertyChange(nameof(UseMultipleGrubbsBeckTest), old, value);
                }
            }
        }

        /// <summary>
        /// The USGS gage site number.
        /// </summary>
        [Category("General")]
        [DisplayName("USGS Site Number")]
        [Description("The USGS surface water site number. Must be 8 digits: the first 2 represent the part number and the following 6 the downstream-order number.")]
        [Browsable(true)]
        public string USGSSiteNumber
        {
            get { return _usgsSiteNumber; }
            set
            {
                if (_usgsSiteNumber != value)
                {
                    var old = _usgsSiteNumber;
                    _usgsSiteNumber = value;

                    _siteNumberValid = true;
                    _messenger.Remove(_siteNumberMsg);
                    if (_usgsSiteNumber.Length != 8 && (ExactDataMethod == ExactDataEntryType.USGSPeakDischarge || ExactDataMethod == ExactDataEntryType.USGSPeakStage))
                    {
                        _siteNumberValid = false;
                        _messenger.Add(_siteNumberMsg);
                    }

                    if (_exactDataMethod != ExactDataEntryType.Manual && !UndoManager.IsExecutingAction)
                        ClearTimeSeriesResults();
                    SetIsValid();
                    RecordPropertyChange(nameof(USGSSiteNumber), old, value);
                }
            }
        }

        #region Block and POT Series Properties

        /// <summary>
        /// The selected time series element.
        /// </summary>
        [Category("Time Series Options")]
        [DisplayName("Time Series")]
        [Description("Specifies the time series used to compute block or peaks-over-threshold estimates.")]
        [Browsable(true)]
        public TimeSeriesElement TimeSeriesElement
        {
            get { return _timeSeriesElement; }
            set
            {
                if (_timeSeriesElement != value)
                {
                    var old = _timeSeriesElement;

                    if (_timeSeriesElement != null)
                    {
                        _timeSeriesElement.PropertyChanged -= TimeSeriesElementChanged;
                        _timeSeriesElement.Deleted -= OnTimeSeriesElementDeleted;
                    }

                    _timeSeriesElement = value;

                    if (_timeSeriesElement != null)
                    {
                        _timeSeriesElement.PropertyChanged += TimeSeriesElementChanged;
                        _timeSeriesElement.Deleted += OnTimeSeriesElementDeleted;
                    }

                    if (_timeSeriesElement != null && ExactDataMethod != ExactDataEntryType.Manual)
                    {
                        UnitLabel = _timeSeriesElement.UnitLabel;
                    }

                    if (ExactDataMethod != ExactDataEntryType.Manual && !UndoManager.IsExecutingAction)
                        ClearTimeSeriesResults();
                    SetIsValid();
                    RecordPropertyChange(nameof(TimeSeriesElement), old, value);
                }
            }
        }

        /// <summary>
        /// The time block function.
        /// </summary>
        [Category("Time Series Options")]
        [DisplayName("Block Function")]
        [Description("Specifies the function applied to each time block (default is maximum).")]
        [Browsable(true)]
        public BlockFunctionType BlockFunction
        {
            get { return _blockFunction; }
            set
            {
                if (_blockFunction != value)
                {
                    var old = _blockFunction;
                    _blockFunction = value;

                    if (ExactDataMethod != ExactDataEntryType.Manual && !UndoManager.IsExecutingAction)
                        ClearTimeSeriesResults();
                    SetIsValid();
                    RecordPropertyChange(nameof(BlockFunction), old, value);
                }
            }
        }

        /// <summary>
        /// The time block window.
        /// </summary>
        [Category("Time Series Options")]
        [DisplayName("Time Block Window")]
        [Description("Defines the time block window for creating the block series (default is calendar year).")]
        [Browsable(true)]
        public TimeBlockWindow TimeBlock
        {
            get { return _timeBlock; }
            set
            {
                if (_timeBlock != value)
                {
                    var old = _timeBlock;
                    _timeBlock = value;

                    if (ExactDataMethod != ExactDataEntryType.Manual && !UndoManager.IsExecutingAction)
                        ClearTimeSeriesResults();
                    SetIsValid();
                    RecordPropertyChange(nameof(TimeBlock), old, value);
                }
            }
        }

        /// <summary>
        /// The start month.
        /// </summary>
        [Category("Time Series Options")]
        [DisplayName("Start Month")]
        [Description("Specifies the starting month for the custom time block (default is October).")]
        [Browsable(true)]
        public int StartMonth
        {
            get { return _startMonth; }
            set
            {
                if (_startMonth != value)
                {
                    var old = _startMonth;
                    _startMonth = value;

                    _startMonthValid = true;
                    _messenger.Remove(_startMonthMsg);
                    if (_startMonth < 1 || _startMonth > 12)
                    {
                        _startMonthValid = false;
                        _messenger.Add(_startMonthMsg);
                    }

                    if (ExactDataMethod != ExactDataEntryType.Manual && !UndoManager.IsExecutingAction)
                        ClearTimeSeriesResults();
                    SetIsValid();
                    RecordPropertyChange(nameof(StartMonth), old, value);
                }
            }
        }

        /// <summary>
        /// The end month.
        /// </summary>
        [Category("Time Series Options")]
        [DisplayName("End Month")]
        [Description("Specifies the ending month for the custom time block (default is September).")]
        [Browsable(true)]
        public int EndMonth
        {
            get { return _endMonth; }
            set
            {
                if (_endMonth != value)
                {
                    var old = _endMonth;
                    _endMonth = value;

                    _endMonthValid = true;
                    _messenger.Remove(_endMonthMsg);
                    if (_endMonth < 1 || _endMonth > 12)
                    {
                        _endMonthValid = false;
                        _messenger.Add(_endMonthMsg);
                    }

                    if (ExactDataMethod != ExactDataEntryType.Manual && !UndoManager.IsExecutingAction)
                        ClearTimeSeriesResults();
                    SetIsValid();
                    RecordPropertyChange(nameof(EndMonth), old, value);
                }
            }
        }

        /// <summary>
        /// The time smoothing function.
        /// </summary>
        [Category("Time Series Options")]
        [DisplayName("Smoothing Function")]
        [Description("Specifies the smoothing function for the time series (default: forward moving average over specified time steps).")]
        [Browsable(true)]
        public SmoothingFunctionType SmoothingFunction
        {
            get { return _smoothingFunction; }
            set
            {
                if (_smoothingFunction != value)
                {
                    var old = _smoothingFunction;
                    _smoothingFunction = value;

                    if (ExactDataMethod != ExactDataEntryType.Manual && !UndoManager.IsExecutingAction)
                        ClearTimeSeriesResults();
                    SetIsValid();
                    RecordPropertyChange(nameof(SmoothingFunction), old, value);
                }
            }
        }

        /// <summary>
        /// The time period over which smoothing is computed.
        /// </summary>
        [Category("Time Series Options")]
        [DisplayName("Smoothing Period")]
        [Description("Defines the period for smoothing (e.g., with a 1-hour interval and period 12, smoothing is computed over 12 hours).")]
        [Browsable(true)]
        public int Period
        {
            get { return _period; }
            set
            {
                if (_period != value)
                {
                    var old = _period;
                    _period = value;
                    _periodValid = _period >= 1;

                    if (ExactDataMethod != ExactDataEntryType.Manual && !UndoManager.IsExecutingAction)
                        ClearTimeSeriesResults();
                    SetIsValid();
                    RecordPropertyChange(nameof(Period), old, value);
                }
            }
        }

        /// <summary>
        /// The threshold value. Peaks exceeding this value are recorded.
        /// </summary>
        [Category("Time Series Options")]
        [DisplayName("Threshold")]
        [Description("Sets the threshold; peaks exceeding this value are recorded.")]
        [Browsable(true)]
        public double Threshold
        {
            get { return _threshold; }
            set
            {
                if (_threshold != value)
                {
                    var old = _threshold;
                    _threshold = value;

                    if (ExactDataMethod != ExactDataEntryType.Manual && !UndoManager.IsExecutingAction)
                        ClearTimeSeriesResults();
                    SetIsValid();
                    RecordPropertyChange(nameof(Threshold), old, value);
                }
            }
        }

        /// <summary>
        /// The minimum number of time steps between peaks.
        /// </summary>
        [Category("Time Series Options")]
        [DisplayName("Minimum Steps Between Peaks")]
        [Description("Specifies the minimum number of time steps required between peak events.")]
        [Browsable(true)]
        public int MinStepsBetweenPeaks
        {
            get { return _minStepsBetweenPeaks; }
            set
            {
                if (_minStepsBetweenPeaks != value)
                {
                    var old = _minStepsBetweenPeaks;
                    _minStepsBetweenPeaks = value;

                    if (ExactDataMethod != ExactDataEntryType.Manual && !UndoManager.IsExecutingAction)
                        ClearTimeSeriesResults();
                    SetIsValid();
                    RecordPropertyChange(nameof(MinStepsBetweenPeaks), old, value);
                }
            }
        }

        /// <summary>
        /// Determines if the block series has been processed. 
        /// </summary>
        public bool IsProcessed
        {
            get { return _isProcessed; }
            private set
            {
                if (_isProcessed != value)
                {
                    _isProcessed = value;
                    RaisePropertyChange(nameof(IsProcessed));
                }
            }
        }

        #endregion

        #region Plot Properties

        /// <summary>
        /// Gets the chronology plot.
        /// </summary>
        public Plot ChronologyPlot => _chronologyPlot;

        /// <summary>
        /// Gets the frequency plot.
        /// </summary>
        public Plot FrequencyPlot => _frequencyPlot;

        /// <summary>
        /// Gets the seasonality plot.
        /// </summary>
        public Plot SeasonalityPlot => _seasonalityPlot;

        /// <summary>
        /// Gets the density plot.
        /// </summary>
        public Plot DensityPlot => _densityPlot;

        /// <summary>
        /// Gets the histogram plot.
        /// </summary>
        public Plot HistogramPlot => _histogramPlot;

        /// <summary>
        /// Gets the normal Q-Q plot.
        /// </summary>
        public Plot QQPlot => _qqPlot;

        /// <summary>
        /// Gets the autocorrelation function (ACF) plot.
        /// </summary>
        public Plot ACFPlot => _acfPlot;

        /// <summary>
        /// Gets the partial autocorrelation function (PACF) plot.
        /// </summary>
        public Plot PACFPlot => _pacfPlot;

        /// <summary>
        /// Gets the Mean Residual Life (MRL) plot for POT threshold diagnostics.
        /// </summary>
        public Plot MRLPlot => _mrlPlot;

        /// <summary>
        /// Gets the modified scale parameter stability plot for POT threshold diagnostics.
        /// </summary>
        public Plot ModifiedScalePlot => _modifiedScalePlot;

        /// <summary>
        /// Gets the shape parameter stability plot for POT threshold diagnostics.
        /// </summary>
        public Plot ShapePlot => _shapePlot;

        #endregion

        #endregion

        #endregion

        #region IElement Methods

        /// <summary>
        /// Constructs a <see cref="BasicMessageItem"/> and registers it in the <see cref="_messages"/>
        /// list so the <see cref="Name"/> setter can bulk-update its <c>SourceName</c>. Centralizing
        /// registration here means a future-added message cannot accidentally drift out of sync
        /// with the master list.
        /// </summary>
        /// <param name="type">The message severity (Message / Warning / Error).</param>
        /// <param name="description">The user-facing message text.</param>
        /// <param name="propertyName">The property the message is attributed to (drives jump-to behavior).</param>
        /// <param name="errorCode">The stable error code (e.g. <c>"ID-ERR-013"</c>) used for messenger lookup and external linking.</param>
        /// <returns>The constructed message, already added to <see cref="_messages"/>.</returns>
        private BasicMessageItem RegisterMessage(MessageType type, string description, string propertyName, string errorCode)
        {
            var msg = new BasicMessageItem(type, description, this, ParentCollection.Name, Name, propertyName, errorCode);
            _messages.Add(msg);
            return msg;
        }

        /// <summary>
        /// Gets the required columns for the SQLite database table.
        /// </summary>
        /// <remarks>
        /// If you want to add a new column, add it to the end of the dictionary to maintain backward compatibility.
        /// </remarks>
        public static Dictionary<string, Type> RequiredColumns { get; } = new Dictionary<string, Type>() {
            { nameof(Name), typeof(string) },
            { nameof(Description), typeof(string) },
            { nameof(CreationDate), typeof(string) },
            { nameof(LastModified), typeof(string) },
            { nameof(ExactDataMethod), typeof(string) },
            { nameof(DataFrame), typeof(string) },
            { nameof(UnitLabel), typeof(string) },
            { nameof(IndexLabel), typeof(string) },
            { nameof(USGSSiteNumber), typeof(string) },
            { nameof(UseMultipleGrubbsBeckTest), typeof(bool) },
            { nameof(TimeSeriesElement), typeof(string) },
            { nameof(BlockFunction), typeof(string) },
            { nameof(TimeBlock), typeof(string) },
            { nameof(StartMonth), typeof(int) },
            { nameof(EndMonth), typeof(int) },
            { nameof(SmoothingFunction), typeof(string) },
            { nameof(Period), typeof(int) },
            { nameof(Threshold), typeof(double) },
            { nameof(MinStepsBetweenPeaks), typeof(int) },
            { nameof(IsProcessed), typeof(bool) },
            { "ChronologyPlotSettings", typeof(string) },
            { "FrequencyPlotSettings", typeof(string) },
            { "SeasonalityPlotSettings", typeof(string) },
            { "DensityPlotSettings", typeof(string) },
            { "HistogramPlotSettings", typeof(string) },
            { "QQPlotSettings", typeof(string) },
            { "ACFPlotSettings", typeof(string) },
            { "PACFPlotSettings", typeof(string) },
            { "MRLPlotSettings", typeof(string) },
            { "ModifiedScalePlotSettings", typeof(string) },
            { "ShapePlotSettings", typeof(string) } };

        /// <summary>
        /// Creates or updates the SQLite database table for storing input data elements.
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
                var dtView = sqlite.GetTableManager(ParentCollection.Name);
                // Add any required columns that don't exist
                int columnIndex;
                foreach (KeyValuePair<string, Type> column in RequiredColumns)
                {
                    columnIndex = Array.IndexOf(dtView.ColumnNames, column.Key);
                    // If the column doesn't exist in the database then create it.
                    if (columnIndex < 0)
                    {
                        dtView.AddColumn(column.Key, column.Value);
                    }
                    else
                    {
                        if (dtView.ColumnTypes[columnIndex] != column.Value)
                        {
                            dtView.DeleteColumn(columnIndex);
                            dtView.AddColumn(column.Key, column.Value);
                        }
                    }
                }
                dtView.ApplyEdits();
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
            if (version == "1.0")
            {
                openedFromV1 = true;
                OpenFromVersion1(sqlite);
            }
            else
            {
                dtView = sqlite.GetTableManager(ParentCollection.Name);
                int rowIndex = dtView.SearchColumn(0, dtView.NumberOfRows - 1, "Name", NameOnDisk, true, true);
                if (rowIndex != -1)
                {
                    // Use backing fields during deserialization to avoid repeated SetIsValid() and ClearTimeSeriesResults() calls.
                    if (dtView.ColumnNames.Contains(nameof(Name)))
                    {
                        _name = dtView.GetCell(nameof(Name), rowIndex).ToString();
                        foreach (var item in _messages) item.SourceName = _name;
                        _nameValid = ValidateName(BestFitProject.InvalidNameCharacters, 50, "ID");
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
                    // General
                    // Use backing fields during deserialization to avoid repeated SetIsValid() calls.
                    // Each public setter calls SetIsValid() which runs 4 series Validate() methods.
                    // A single SetIsValid() call at the end of Open() is sufficient.
                    if (dtView.ColumnNames.Contains(nameof(ExactDataMethod))) Enum.TryParse(dtView.GetCell(nameof(ExactDataMethod), rowIndex).ToString(), out _exactDataMethod);
                    if (dtView.ColumnNames.Contains(nameof(UnitLabel)))
                    {
                        _unitLabel = dtView.GetCell(nameof(UnitLabel), rowIndex).ToString();
                        _unitLabelValid = !string.IsNullOrEmpty(_unitLabel);
                    }
                    if (dtView.ColumnNames.Contains(nameof(IndexLabel)))
                    {
                        _indexLabel = dtView.GetCell(nameof(IndexLabel), rowIndex).ToString();
                        _indexLabelValid = !string.IsNullOrEmpty(_indexLabel);
                    }
                    if (dtView.ColumnNames.Contains(nameof(USGSSiteNumber)))
                    {
                        _usgsSiteNumber = dtView.GetCell(nameof(USGSSiteNumber), rowIndex).ToString();
                        _siteNumberValid = _usgsSiteNumber.Length == 8 ||
                            (_exactDataMethod != ExactDataEntryType.USGSPeakDischarge && _exactDataMethod != ExactDataEntryType.USGSPeakStage);
                    }
                    if (dtView.ColumnNames.Contains(nameof(UseMultipleGrubbsBeckTest))) bool.TryParse(dtView.GetCell(nameof(UseMultipleGrubbsBeckTest), rowIndex).ToString(), out _useMultipleGrubbsBeckTest);

                    // Get time series element
                    if (dtView.ColumnNames.Contains(nameof(TimeSeriesElement)))
                    {
                        var timeSeriesName = dtView.GetCell(nameof(TimeSeriesElement), rowIndex).ToString();
                        // Unsubscribe from old element before assigning new (prevents handler leak on repeated Open)
                        if (_timeSeriesElement != null)
                        {
                            _timeSeriesElement.PropertyChanged -= TimeSeriesElementChanged;
                            _timeSeriesElement.Deleted -= OnTimeSeriesElementDeleted;
                        }
                        foreach (IElementCollection collection in ParentCollection.ParentProject.ElementCollections)
                        {
                            if (collection.GetType() == typeof(TimeSeriesCollection))
                            {
                                foreach (IElement element in collection)
                                {
                                    if (element.Name == timeSeriesName && element.GetType() == typeof(TimeSeriesElement))
                                    {
                                        _timeSeriesElement = (TimeSeriesElement)element;
                                        _timeSeriesElement.PropertyChanged += TimeSeriesElementChanged;
                                        _timeSeriesElement.Deleted += OnTimeSeriesElementDeleted;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    // Time-Series Properties
                    if (dtView.ColumnNames.Contains(nameof(BlockFunction))) Enum.TryParse(dtView.GetCell(nameof(BlockFunction), rowIndex).ToString(), out _blockFunction);
                    if (dtView.ColumnNames.Contains(nameof(TimeBlock))) Enum.TryParse(dtView.GetCell(nameof(TimeBlock), rowIndex).ToString(), out _timeBlock);
                    if (dtView.ColumnNames.Contains(nameof(StartMonth)))
                    {
                        int.TryParse(dtView.GetCell(nameof(StartMonth), rowIndex).ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out _startMonth);
                        _startMonthValid = _startMonth >= 1 && _startMonth <= 12;
                    }
                    if (dtView.ColumnNames.Contains(nameof(EndMonth)))
                    {
                        int.TryParse(dtView.GetCell(nameof(EndMonth), rowIndex).ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out _endMonth);
                        _endMonthValid = _endMonth >= 1 && _endMonth <= 12;
                    }
                    if (dtView.ColumnNames.Contains(nameof(SmoothingFunction))) Enum.TryParse(dtView.GetCell(nameof(SmoothingFunction), rowIndex).ToString(), out _smoothingFunction);
                    if (dtView.ColumnNames.Contains(nameof(Period)))
                    {
                        int.TryParse(dtView.GetCell(nameof(Period), rowIndex).ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out _period);
                    }
                    if (dtView.ColumnNames.Contains(nameof(Threshold)))
                    {
                        double.TryParse(dtView.GetCell(nameof(Threshold), rowIndex).ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out _threshold);
                    }
                    if (dtView.ColumnNames.Contains(nameof(MinStepsBetweenPeaks)))
                    {
                        int.TryParse(dtView.GetCell(nameof(MinStepsBetweenPeaks), rowIndex).ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out _minStepsBetweenPeaks);
                    }
                    if (dtView.ColumnNames.Contains(nameof(IsProcessed))) bool.TryParse(dtView.GetCell(nameof(IsProcessed), rowIndex).ToString(), out _isProcessed);
                    // Deserialize plot settings into Plot objects, then refresh only axes
                    // that still carry their factory-default labels.
                    bool chronologyPlotRestored = DeserializePlotSettings(dtView, rowIndex, "ChronologyPlotSettings", _chronologyPlot);
                    bool frequencyPlotRestored = DeserializePlotSettings(dtView, rowIndex, "FrequencyPlotSettings", _frequencyPlot);
                    DeserializePlotSettings(dtView, rowIndex, "SeasonalityPlotSettings", _seasonalityPlot);
                    bool densityPlotRestored = DeserializePlotSettings(dtView, rowIndex, "DensityPlotSettings", _densityPlot);
                    bool histogramPlotRestored = DeserializePlotSettings(dtView, rowIndex, "HistogramPlotSettings", _histogramPlot);
                    DeserializePlotSettings(dtView, rowIndex, "QQPlotSettings", _qqPlot);
                    DeserializePlotSettings(dtView, rowIndex, "ACFPlotSettings", _acfPlot);
                    DeserializePlotSettings(dtView, rowIndex, "PACFPlotSettings", _pacfPlot);
                    DeserializePlotSettings(dtView, rowIndex, "MRLPlotSettings", _mrlPlot);
                    DeserializePlotSettings(dtView, rowIndex, "ModifiedScalePlotSettings", _modifiedScalePlot);
                    DeserializePlotSettings(dtView, rowIndex, "ShapePlotSettings", _shapePlot);
                    RefreshPlotAxisTitlesAfterOpen(chronologyPlotRestored, frequencyPlotRestored, densityPlotRestored, histogramPlotRestored);
                    // Get data frame
                    if (dtView.ColumnNames.Contains(nameof(DataFrame)))
                    {
                        try
                        {
                            if (_dataFrame != null)
                                _dataFrame.PropertyChanged -= DataFramePropertyChanged;
                            _dataFrame = new DataFrame(XElement.Parse(dtView.GetCell(nameof(DataFrame), rowIndex).ToString()));
                            _dataFrame.ProcessThresholdSeries();
                            _dataFrame.PropertyChanged += DataFramePropertyChanged;
                        }
                        catch (Exception ex)
                        {
                            // Surface the deserialization failure to Debug for diagnosis. A corrupt
                            // or malformed DataFrame XML payload leaves _dataFrame in its previous
                            // state; the user sees an InputData with no series and the validation
                            // adapter surfaces the empty-series message via SetIsValid.
                            System.Diagnostics.Debug.WriteLine($"InputData.Open: could not deserialize DataFrame for '{Name}': {ex.Message}");
                        }
                    }

                }


            }

            if (wasOpen == false) sqlite.Close();

            SetupBridges();
            SetIsValid();
            SetIsDirty(openedFromV1);
            }
            finally
            {
                IsUndoEnabled = wasUndoEnabled;
                if (wasUndoEnabled) ClearUndoHistory();
            }
        }

        /// <summary>
        /// Opens and migrates input data from BestFit version 1.0 format to the current format.
        /// </summary>
        /// <param name="sqlite">The SQLite database manager instance containing the version 1.0 data.</param>
        private void OpenFromVersion1(SQLiteManager sqlite)
        {
            var dtView = sqlite.GetTableManager(ParentCollection.Name);
            int rowIndex = dtView.SearchColumn(0, dtView.NumberOfRows - 1, "Name", NameOnDisk, true, true);
            if (rowIndex != -1)
            {
                if (dtView.ColumnNames.Contains(nameof(Name))) Name = dtView.GetCell(nameof(Name), rowIndex).ToString();
                if (dtView.ColumnNames.Contains(nameof(Description))) Description = dtView.GetCell(nameof(Description), rowIndex).ToString();
                if (dtView.ColumnNames.Contains(nameof(CreationDate))) _creationDate = FrameworkInterfaces.Utilities.Tools.DateFromString(dtView.GetCell(nameof(CreationDate), rowIndex).ToString()) ?? DateTime.MinValue;
                if (dtView.ColumnNames.Contains(nameof(LastModified))) _lastModified = FrameworkInterfaces.Utilities.Tools.DateFromString(dtView.GetCell(nameof(LastModified), rowIndex).ToString()) ?? DateTime.MinValue;
                // General
                if (dtView.ColumnNames.Contains(nameof(UnitLabel))) UnitLabel = dtView.GetCell(nameof(UnitLabel), rowIndex).ToString();
                if (dtView.ColumnNames.Contains(nameof(UseMultipleGrubbsBeckTest))) bool.TryParse(dtView.GetCell(nameof(UseMultipleGrubbsBeckTest), rowIndex).ToString(), out _useMultipleGrubbsBeckTest);
                // Deserialize plot settings into Plot objects, then refresh only axes
                // that still carry their factory-default labels.
                bool chronologyPlotRestored = DeserializePlotSettings(dtView, rowIndex, "ChronologyPlotSettings", _chronologyPlot);
                bool frequencyPlotRestored = DeserializePlotSettings(dtView, rowIndex, "FrequencyPlotSettings", _frequencyPlot);
                RefreshPlotAxisTitlesAfterOpen(chronologyPlotRestored, frequencyPlotRestored, false, false);

                // Get systematic data
                if (dtView.ColumnNames.Contains("SystematicDataList"))
                {
                    var xElement = XElement.Parse(dtView.GetCell("SystematicDataList", rowIndex).ToString());
                    foreach(XElement element in xElement.Elements("Row"))
                    {
                        int index = 0;
                        double value = 0;
                        double plottingPosition = 0;
                        bool isLowOutlier = false;
                        if (element.Attribute("Year") != null) int.TryParse(element.Attribute("Year").Value, out index);
                        if (element.Attribute("Value") != null) double.TryParse(element.Attribute("Value").Value, out value);
                        if (element.Attribute("PlottingPosition") != null) double.TryParse(element.Attribute("PlottingPosition").Value, out plottingPosition);
                        if (element.Attribute("IsLowOutlier") != null) bool.TryParse(element.Attribute("IsLowOutlier").Value, out isLowOutlier);
                        DataFrame.ExactSeries.Add(new ExactData(index, value, plottingPosition, isLowOutlier));
                    }
                }
                // Get interval data
                if (dtView.ColumnNames.Contains("IntervalDataList"))
                {
                    var xElement = XElement.Parse(dtView.GetCell("IntervalDataList", rowIndex).ToString());
                    foreach (XElement element in xElement.Elements("Row"))
                    {
                        int index = 0;
                        double value = 0, lowerValue = 0, upperValue = 0;
                        double plottingPosition = 0;
                        if (element.Attribute("Year") != null) int.TryParse(element.Attribute("Year").Value, out index);
                        if (element.Attribute("LowerValue") != null) double.TryParse(element.Attribute("LowerValue").Value, out lowerValue);
                        if (element.Attribute("Value") != null) double.TryParse(element.Attribute("Value").Value, out value);
                        if (element.Attribute("UpperValue") != null) double.TryParse(element.Attribute("UpperValue").Value, out upperValue);
                        if (element.Attribute("PlottingPosition") != null) double.TryParse(element.Attribute("PlottingPosition").Value, out plottingPosition);
                        DataFrame.IntervalSeries.Add(new IntervalData(index,lowerValue, value, upperValue, plottingPosition));
                    }
                }
                // Get threshold data
                if (dtView.ColumnNames.Contains("ThresholdDataList"))
                {
                    var xElement = XElement.Parse(dtView.GetCell("ThresholdDataList", rowIndex).ToString());
                    foreach (XElement element in xElement.Elements("Row"))
                    {
                        int startIndex = 0, endIndex = 0;
                        double value = 0;
                        if (element.Attribute("StartYear") != null) int.TryParse(element.Attribute("StartYear").Value, out startIndex);
                        if (element.Attribute("EndYear") != null) int.TryParse(element.Attribute("EndYear").Value, out endIndex);
                        if (element.Attribute("Value") != null) double.TryParse(element.Attribute("Value").Value, out value);
                        DataFrame.ThresholdSeries.Add(new ThresholdData(startIndex, endIndex, value));
                    }
                }

                // Data Frame
                double plottingParameter = 0;
                if (dtView.ColumnNames.Contains("PlottingParameter")) double.TryParse(dtView.GetCell("PlottingParameter", rowIndex).ToString(), out plottingParameter);
                DataFrame.PlottingParameter = plottingParameter;
                double threshold = 0;
                if (dtView.ColumnNames.Contains("LowOutlierThresholdValue")) double.TryParse(dtView.GetCell("LowOutlierThresholdValue", rowIndex).ToString(), out threshold);
                DataFrame.LowOutlierThreshold = threshold;
                // Update low outliers
                if (UseMultipleGrubbsBeckTest == true)
                    DataFrame.SetLowOutliersFromMGBT();
                else
                    DataFrame.SetLowOutliersFromThreshold();
                
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

                dtView.EditCell(rowIndex, nameof(Name), Name);
                dtView.EditCell(rowIndex, nameof(Description), Description);
                dtView.EditCell(rowIndex, nameof(CreationDate), FrameworkInterfaces.Utilities.Tools.DateToUniversalString(CreationDate));
                dtView.EditCell(rowIndex, nameof(LastModified), FrameworkInterfaces.Utilities.Tools.DateToUniversalString(LastModified));
                dtView.EditCell(rowIndex, nameof(ExactDataMethod), ExactDataMethod);
                dtView.EditCell(rowIndex, nameof(DataFrame), DataFrame == null ? "" : DataFrame.ToXElement().ToString());
                dtView.EditCell(rowIndex, nameof(UnitLabel), UnitLabel);
                dtView.EditCell(rowIndex, nameof(IndexLabel), IndexLabel);
                dtView.EditCell(rowIndex, nameof(USGSSiteNumber), USGSSiteNumber);
                dtView.EditCell(rowIndex, nameof(UseMultipleGrubbsBeckTest), UseMultipleGrubbsBeckTest);
                dtView.EditCell(rowIndex, nameof(TimeSeriesElement), TimeSeriesElement == null ? "" : TimeSeriesElement.Name);
                dtView.EditCell(rowIndex, nameof(BlockFunction), BlockFunction);
                dtView.EditCell(rowIndex, nameof(TimeBlock), TimeBlock);
                dtView.EditCell(rowIndex, nameof(StartMonth), StartMonth.ToString(CultureInfo.InvariantCulture));
                dtView.EditCell(rowIndex, nameof(EndMonth), EndMonth.ToString(CultureInfo.InvariantCulture));
                dtView.EditCell(rowIndex, nameof(SmoothingFunction), SmoothingFunction);
                dtView.EditCell(rowIndex, nameof(Period), Period.ToString(CultureInfo.InvariantCulture));
                dtView.EditCell(rowIndex, nameof(Threshold), Threshold.ToString("G17", CultureInfo.InvariantCulture));
                dtView.EditCell(rowIndex, nameof(MinStepsBetweenPeaks), MinStepsBetweenPeaks.ToString(CultureInfo.InvariantCulture));
                dtView.EditCell(rowIndex, nameof(IsProcessed), IsProcessed);
                dtView.EditCell(rowIndex, "ChronologyPlotSettings", _chronologyPlot != null ? PlotSerializer.ToXElement(_chronologyPlot).ToString() : "");
                dtView.EditCell(rowIndex, "FrequencyPlotSettings", _frequencyPlot != null ? PlotSerializer.ToXElement(_frequencyPlot).ToString() : "");
                dtView.EditCell(rowIndex, "SeasonalityPlotSettings", _seasonalityPlot != null ? PlotSerializer.ToXElement(_seasonalityPlot).ToString() : "");
                dtView.EditCell(rowIndex, "DensityPlotSettings", _densityPlot != null ? PlotSerializer.ToXElement(_densityPlot).ToString() : "");
                dtView.EditCell(rowIndex, "HistogramPlotSettings", _histogramPlot != null ? PlotSerializer.ToXElement(_histogramPlot).ToString() : "");
                dtView.EditCell(rowIndex, "QQPlotSettings", _qqPlot != null ? PlotSerializer.ToXElement(_qqPlot).ToString() : "");
                dtView.EditCell(rowIndex, "ACFPlotSettings", _acfPlot != null ? PlotSerializer.ToXElement(_acfPlot).ToString() : "");
                dtView.EditCell(rowIndex, "PACFPlotSettings", _pacfPlot != null ? PlotSerializer.ToXElement(_pacfPlot).ToString() : "");
                dtView.EditCell(rowIndex, "MRLPlotSettings", _mrlPlot != null ? PlotSerializer.ToXElement(_mrlPlot).ToString() : "");
                dtView.EditCell(rowIndex, "ModifiedScalePlotSettings", _modifiedScalePlot != null ? PlotSerializer.ToXElement(_modifiedScalePlot).ToString() : "");
                dtView.EditCell(rowIndex, "ShapePlotSettings", _shapePlot != null ? PlotSerializer.ToXElement(_shapePlot).ToString() : "");

                dtView.ApplyEdits();
                sqlite.Close();
                committed = true;
            }
            finally
            {
                if (sqlite.DataBaseOpen) sqlite.Close();
                // Roll back the in-memory LastModified change so it stays in sync with disk on
                // failure. The on-disk LastModified column was never written when committed=false.
                if (!committed && _lastModified != previousLastModified)
                {
                    _lastModified = previousLastModified;
                    RaisePropertyChange(nameof(LastModified));
                }
            }

            // Only mark clean / fire ObjectSaved when the commit actually succeeded. A failed
            // Save (disk full, file lock, sqlite throw) MUST leave IsDirty=true so the user is
            // prompted on Close and the next Save retries. Updating NameOnDisk on a failed
            // Save would also point future Save() calls at a row that never got written.
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
            var element = new InputData(newName == "" || string.IsNullOrEmpty(newName) ? Name : newName, ParentCollection);
            element.IsUndoEnabled = false;
            try
            {
                element.Description = Description;
                element.ExactDataMethod = ExactDataMethod;
                element.UnitLabel = UnitLabel;
                element.IndexLabel = IndexLabel;
                element.USGSSiteNumber = USGSSiteNumber;
                element.UseMultipleGrubbsBeckTest = UseMultipleGrubbsBeckTest;
                element.TimeSeriesElement = TimeSeriesElement;
                element.BlockFunction = BlockFunction;
                element.TimeBlock = TimeBlock;
                element.StartMonth = StartMonth;
                element.EndMonth = EndMonth;
                element.SmoothingFunction = SmoothingFunction;
                element.Period = Period;
                element.Threshold = Threshold;
                element.MinStepsBetweenPeaks = MinStepsBetweenPeaks;
                element.IsProcessed = IsProcessed;
                // Clone plot settings via serialize/deserialize round-trip
                if (_chronologyPlot != null) PlotSerializer.FromXElement(element._chronologyPlot, PlotSerializer.ToXElement(_chronologyPlot));
                if (_frequencyPlot != null) PlotSerializer.FromXElement(element._frequencyPlot, PlotSerializer.ToXElement(_frequencyPlot));
                if (_seasonalityPlot != null) PlotSerializer.FromXElement(element._seasonalityPlot, PlotSerializer.ToXElement(_seasonalityPlot));
                if (_densityPlot != null) PlotSerializer.FromXElement(element._densityPlot, PlotSerializer.ToXElement(_densityPlot));
                if (_histogramPlot != null) PlotSerializer.FromXElement(element._histogramPlot, PlotSerializer.ToXElement(_histogramPlot));
                if (_qqPlot != null) PlotSerializer.FromXElement(element._qqPlot, PlotSerializer.ToXElement(_qqPlot));
                if (_acfPlot != null) PlotSerializer.FromXElement(element._acfPlot, PlotSerializer.ToXElement(_acfPlot));
                if (_pacfPlot != null) PlotSerializer.FromXElement(element._pacfPlot, PlotSerializer.ToXElement(_pacfPlot));
                if (_mrlPlot != null) PlotSerializer.FromXElement(element._mrlPlot, PlotSerializer.ToXElement(_mrlPlot));
                if (_modifiedScalePlot != null) PlotSerializer.FromXElement(element._modifiedScalePlot, PlotSerializer.ToXElement(_modifiedScalePlot));
                if (_shapePlot != null) PlotSerializer.FromXElement(element._shapePlot, PlotSerializer.ToXElement(_shapePlot));
                element.UpdatePlotYAxisTitles();
                element.UpdatePlotXAxisTitles();
                element.DataFrame = DataFrame.Clone();
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
            var element = new InputData(itemName, ParentCollection);
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
            // TimeSeriesElement setter � that would re-trigger validation messages and
            // re-flip IsDirty=true).
            if (_timeSeriesElement != null) _timeSeriesElement.Deleted -= OnTimeSeriesElementDeleted;
            DisposeBridges();
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
        /// This method checks name validity, unit and index labels, site number, data frame contents, and time series inputs.
        /// It should be called whenever a property that affects validity is changed.
        /// </remarks>
        private void SetIsValid()
        {
            bool valid = true;
            if (_nameValid == false) valid = false;          
            if (_unitLabelValid == false) valid = false;
            if (_indexLabelValid == false) valid = false;

            // Check site number
            if ((ExactDataMethod == ExactDataEntryType.USGSPeakDischarge ||
                ExactDataMethod == ExactDataEntryType.USGSPeakStage) && _siteNumberValid == false)
                valid = false;

            // Check Data Frame � minimum count is UI-only, series validation delegated to model via adapter.
            // The DataFrame setter accepts null (during deserialization mid-flight); treat that as
            // invalid rather than throwing NRE from every property edit that calls SetIsValid.
            _messenger.Remove(_dataFrameMsg);
            if (DataFrame == null ||
                DataFrame.ExactSeries.Count + DataFrame.UncertainSeries.Count + DataFrame.IntervalSeries.Count < 10)
            {
                valid = false;
                _messenger.Add(_dataFrameMsg);
            }

            // Delegate DataFrame model validation (series, plotting parameter) to adapter.
            if (DataFrame != null)
            {
                bool modelValid = _validationAdapter.SyncValidation(DataFrame.Validate(), Name);
                if (modelValid == false) valid = false;
            }
            else
            {
                valid = false;
            }

            if (IsTimeSeriesInputValid() == false)
            {
                valid = false;
            }


            if (valid != _isValid)
            {
                _isValid = valid;
                RaisePropertyChange(nameof(IsValid));
            }
        }

        /// <summary>
        /// Validates all time series related input parameters.
        /// </summary>
        /// <returns>True if all time series inputs are valid; otherwise, false.</returns>
        /// <remarks>
        /// Checks validity of start/end months, period, threshold, minimum steps between peaks, and time series selection.
        /// </remarks>
        public bool IsTimeSeriesInputValid()
        {
            bool valid = true;
            _messenger.Remove(_partialBlockSeriesMsg);

            // Check all time series dependent inputs
            if (_startMonthValid == false) valid = false;
            if (_endMonthValid == false) valid = false;
            
           
            // Peaks over threshold
            if (ExactDataMethod == ExactDataEntryType.PeaksOverThresholdSeries)
            {
                // Threshold
                _messenger.Remove(_potThresholdMsg);
                if (TimeSeriesElement?.TimeSeries != null && TimeSeriesElement.TimeSeries.Count > 0 && (_threshold > TimeSeriesElement.TimeSeries.ValuesToList().Max()))
                {
                    valid = false;
                    _messenger.Add(_potThresholdMsg);
                }

                // Min steps between events
                _messenger.Remove(_minStepsMsg);
                if (TimeSeriesElement?.TimeSeries != null && (_minStepsBetweenPeaks < 1 || _minStepsBetweenPeaks > TimeSeriesElement.TimeSeries.Count))
                {
                    valid = false;
                    _messenger.Add(_minStepsMsg);
                }
            }

            if (_periodValid == false) valid = false;
            _messenger.Remove(_periodMsg);
            if (_period < 1 || (TimeSeriesElement?.TimeSeries != null && _period > TimeSeriesElement.TimeSeries.Count))
            {
                valid = false;
                _messenger.Add(_periodMsg);
            }

            // Check Time Series
            _messenger.Remove(_timeSeriesNullMsg);
            _messenger.Remove(_timeSeriesInValidMsg);
            if ((ExactDataMethod == ExactDataEntryType.BlockSeries ||
                ExactDataMethod == ExactDataEntryType.PeaksOverThresholdSeries) &&
                TimeSeriesElement == null)
            {
                valid = false;
                _messenger.Add(_timeSeriesNullMsg);
            }
            if ((ExactDataMethod == ExactDataEntryType.BlockSeries ||
                ExactDataMethod == ExactDataEntryType.PeaksOverThresholdSeries) &&
                TimeSeriesElement != null &&
                TimeSeriesElement.IsValid == false)
            {
                valid = false;
                _messenger.Add(_timeSeriesInValidMsg);
            }

            if (ShouldWarnForBlockSeriesCoverage())
            {
                _messenger.Add(_partialBlockSeriesMsg);
            }

            return valid;
        }

        /// <summary>
        /// Determines whether the selected block-series source should raise a coverage warning.
        /// </summary>
        /// <returns><c>true</c> when missing values, timestamp gaps, or partial boundary blocks are detected; otherwise, <c>false</c>.</returns>
        /// <remarks>
        /// The warning is intentionally non-blocking because analysts may knowingly process partial records,
        /// but the message calls attention to cases that can bias annual maximum series and other block summaries.
        /// </remarks>
        private bool ShouldWarnForBlockSeriesCoverage()
        {
            if (ExactDataMethod != ExactDataEntryType.BlockSeries) return false;
            var timeSeries = TimeSeriesElement?.TimeSeries;
            if (timeSeries == null || timeSeries.Count == 0) return false;

            if (timeSeries.HasMissingValues) return true;
            if (HasRegularTimeSeriesGaps(timeSeries)) return true;
            return HasIncompleteBoundaryBlock(timeSeries);
        }

        /// <summary>
        /// Determines whether a regular time series skips one or more expected timestamps.
        /// </summary>
        /// <param name="timeSeries">The source time series selected for block-series processing.</param>
        /// <returns><c>true</c> when adjacent ordinates are not separated by the declared interval; otherwise, <c>false</c>.</returns>
        /// <remarks>
        /// DSS records with missing periods can arrive as either explicit missing values or omitted ordinates.
        /// This check catches the omitted-ordinate case for regular intervals.
        /// </remarks>
        private static bool HasRegularTimeSeriesGaps(TimeSeries timeSeries)
        {
            if (timeSeries == null || timeSeries.Count < 2 || timeSeries.TimeInterval == TimeInterval.Irregular) return false;

            var dates = timeSeries.Select(x => x.Index).OrderBy(x => x).ToList();
            for (int i = 1; i < dates.Count; i++)
            {
                DateTime expected = TimeSeries.AddTimeInterval(dates[i - 1], timeSeries.TimeInterval);
                if (dates[i] != expected) return true;
            }

            return false;
        }

        /// <summary>
        /// Determines whether the first or last selected block is only partially covered by the source time series.
        /// </summary>
        /// <param name="timeSeries">The source time series selected for block-series processing.</param>
        /// <returns><c>true</c> when the first or last block lacks full boundary coverage; otherwise, <c>false</c>.</returns>
        /// <remarks>
        /// Boundary blocks are the most common source of artificially low block maxima because the source record
        /// often starts or stops inside a calendar year, water year, or custom block window.
        /// </remarks>
        private bool HasIncompleteBoundaryBlock(TimeSeries timeSeries)
        {
            if (timeSeries == null || !AreSelectedBlockMonthsValid()) return false;
            if (!TryGetBlockSeriesBoundaryDates(timeSeries, out DateTime firstDate, out DateTime lastDate)) return false;

            DateTime firstBlockStart = GetBlockStart(firstDate);
            if (firstDate > firstBlockStart) return true;

            DateTime lastBlockEnd = GetBlockEnd(lastDate);
            if (timeSeries.TimeInterval != TimeInterval.Irregular)
            {
                DateTime nextAfterLast = TimeSeries.AddTimeInterval(lastDate, timeSeries.TimeInterval);
                return nextAfterLast < lastBlockEnd;
            }

            return lastDate.Date < lastBlockEnd.AddDays(-1).Date;
        }

        /// <summary>
        /// Attempts to find the first and last source dates relevant to the selected block window.
        /// </summary>
        /// <param name="timeSeries">The source time series selected for block-series processing.</param>
        /// <param name="firstDate">The earliest relevant ordinate date when this method returns <c>true</c>.</param>
        /// <param name="lastDate">The latest relevant ordinate date when this method returns <c>true</c>.</param>
        /// <returns><c>true</c> when at least one relevant date is available; otherwise, <c>false</c>.</returns>
        /// <remarks>
        /// Calendar-year and water-year block series use every source ordinate. Custom-year block series only
        /// use ordinates inside the selected month window, so boundary detection follows that same selection.
        /// </remarks>
        private bool TryGetBlockSeriesBoundaryDates(TimeSeries timeSeries, out DateTime firstDate, out DateTime lastDate)
        {
            firstDate = default;
            lastDate = default;
            if (timeSeries == null || timeSeries.Count == 0) return false;

            IEnumerable<DateTime> dates = timeSeries.Select(x => x.Index);
            if (TimeBlock == TimeBlockWindow.CustomYear)
            {
                dates = dates.Where(IsDateInsideSelectedCustomBlock);
            }

            var orderedDates = dates.OrderBy(x => x).ToList();
            if (orderedDates.Count == 0) return false;

            firstDate = orderedDates[0];
            lastDate = orderedDates[orderedDates.Count - 1];
            return true;
        }

        /// <summary>
        /// Determines whether a date falls inside the selected custom block month window.
        /// </summary>
        /// <param name="date">The date to test against the selected custom block window.</param>
        /// <returns><c>true</c> when the date's month is inside the window; otherwise, <c>false</c>.</returns>
        /// <remarks>
        /// Wrapped windows such as October through March include months on either side of the calendar-year boundary.
        /// </remarks>
        private bool IsDateInsideSelectedCustomBlock(DateTime date)
        {
            if (!IsValidMonth(StartMonth) || !IsValidMonth(EndMonth)) return false;
            if (StartMonth <= EndMonth) return date.Month >= StartMonth && date.Month <= EndMonth;
            return date.Month >= StartMonth || date.Month <= EndMonth;
        }

        /// <summary>
        /// Determines whether the selected block-month settings are valid enough for warning detection.
        /// </summary>
        /// <returns><c>true</c> when the selected block month settings are in range; otherwise, <c>false</c>.</returns>
        /// <remarks>
        /// Invalid month values are handled by separate validation errors; the coverage warning stays quiet
        /// until those inputs are corrected.
        /// </remarks>
        private bool AreSelectedBlockMonthsValid()
        {
            if (TimeBlock == TimeBlockWindow.CalendarYear || TimeBlock == TimeBlockWindow.Quarter || TimeBlock == TimeBlockWindow.Month)
                return true;

            if (!IsValidMonth(StartMonth)) return false;
            if (TimeBlock == TimeBlockWindow.CustomYear && !IsValidMonth(EndMonth)) return false;
            return true;
        }

        /// <summary>
        /// Gets the start boundary for the block containing the specified date.
        /// </summary>
        /// <param name="date">The source ordinate date.</param>
        /// <returns>The inclusive start boundary of the containing block.</returns>
        /// <remarks>
        /// Calendar-year, water-year, custom-year, quarterly, and monthly windows are mapped to explicit
        /// date boundaries so partial first and last blocks can be detected consistently.
        /// </remarks>
        private DateTime GetBlockStart(DateTime date)
        {
            if (TimeBlock == TimeBlockWindow.CalendarYear)
                return new DateTime(date.Year, 1, 1);
            if (TimeBlock == TimeBlockWindow.WaterYear)
                return GetFullYearBlockStart(date, StartMonth);
            if (TimeBlock == TimeBlockWindow.CustomYear)
                return GetCustomBlockStart(date, StartMonth, EndMonth);
            if (TimeBlock == TimeBlockWindow.Quarter)
                return new DateTime(date.Year, ((date.Month - 1) / 3) * 3 + 1, 1);
            if (TimeBlock == TimeBlockWindow.Month)
                return new DateTime(date.Year, date.Month, 1);

            return GetFullYearBlockStart(date, StartMonth);
        }

        /// <summary>
        /// Gets the exclusive end boundary for the block containing the specified date.
        /// </summary>
        /// <param name="date">The source ordinate date.</param>
        /// <returns>The exclusive end boundary of the containing block.</returns>
        /// <remarks>
        /// The end boundary is exclusive, which lets regular time series use the timestamp after the last
        /// ordinate to determine whether the final block is fully covered.
        /// </remarks>
        private DateTime GetBlockEnd(DateTime date)
        {
            DateTime blockStart = GetBlockStart(date);
            if (TimeBlock == TimeBlockWindow.CustomYear)
                return blockStart.AddMonths(CountMonthsInInclusiveWindow(StartMonth, EndMonth));
            if (TimeBlock == TimeBlockWindow.Quarter)
                return blockStart.AddMonths(3);
            if (TimeBlock == TimeBlockWindow.Month)
                return blockStart.AddMonths(1);

            return blockStart.AddMonths(12);
        }

        /// <summary>
        /// Gets the start boundary for a full-year block with a configurable start month.
        /// </summary>
        /// <param name="date">The source ordinate date.</param>
        /// <param name="startMonth">The month that starts the full-year block.</param>
        /// <returns>The inclusive start boundary of the containing full-year block.</returns>
        /// <remarks>
        /// A water year that starts in October, for example, assigns January through September dates
        /// to the block that began the previous October.
        /// </remarks>
        private static DateTime GetFullYearBlockStart(DateTime date, int startMonth)
        {
            int year = date.Month >= startMonth ? date.Year : date.Year - 1;
            return new DateTime(year, startMonth, 1);
        }

        /// <summary>
        /// Gets the start boundary for a custom month-window block.
        /// </summary>
        /// <param name="date">The source ordinate date.</param>
        /// <param name="startMonth">The month that starts the custom block.</param>
        /// <param name="endMonth">The month that ends the custom block.</param>
        /// <returns>The inclusive start boundary of the containing custom block.</returns>
        /// <remarks>
        /// Wrapped windows such as October through March assign January through March dates to the
        /// block that began the previous October.
        /// </remarks>
        private static DateTime GetCustomBlockStart(DateTime date, int startMonth, int endMonth)
        {
            if (startMonth <= endMonth)
                return new DateTime(date.Year, startMonth, 1);

            int year = date.Month >= startMonth ? date.Year : date.Year - 1;
            return new DateTime(year, startMonth, 1);
        }

        /// <summary>
        /// Counts the number of months in an inclusive month window.
        /// </summary>
        /// <param name="startMonth">The first month in the window.</param>
        /// <param name="endMonth">The last month in the window.</param>
        /// <returns>The number of calendar months included in the window.</returns>
        /// <remarks>
        /// The count supports both non-wrapped windows, such as March through August, and wrapped windows,
        /// such as October through March.
        /// </remarks>
        private static int CountMonthsInInclusiveWindow(int startMonth, int endMonth)
        {
            if (startMonth <= endMonth) return endMonth - startMonth + 1;
            return 12 - startMonth + 1 + endMonth;
        }

        /// <summary>
        /// Determines whether a month value is inside the calendar month range.
        /// </summary>
        /// <param name="month">The month number to test.</param>
        /// <returns><c>true</c> when the month is between 1 and 12; otherwise, <c>false</c>.</returns>
        /// <remarks>
        /// This guard keeps warning detection from throwing while separate validation messages report
        /// invalid month inputs to the user.
        /// </remarks>
        private static bool IsValidMonth(int month)
        {
            return month >= 1 && month <= 12;
        }

        #endregion

        #region Data Methods

        /// <summary>
        /// Handles property change events from the DataFrame to validate and propagate changes.
        /// </summary>
        /// <param name="sender">The event source.</param>
        /// <param name="e">The property changed event arguments.</param>
        private void DataFramePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (DataFrame.ExactSeries.SuppressCollectionChanged == false &&
                DataFrame.IntervalSeries.SuppressCollectionChanged == false &&
                DataFrame.ThresholdSeries.SuppressCollectionChanged == false)
            {
                SetIsValid();
                RaisePropertyChange(e.PropertyName);
            }
        }

        /// <summary>
        /// Handles property change events from the TimeSeriesElement to clear results when the time series data changes.
        /// </summary>
        /// <param name="sender">The event source.</param>
        /// <param name="e">The property changed event arguments.</param>
        private void TimeSeriesElementChanged(object sender, PropertyChangedEventArgs e)
        {
            // Defensive null guards. TimeSeriesElement.TimeSeries is initialized non-null in the
            // ctor but a future refactor or partial init could expose null transiently; mirrors
            // the guard used in TimeSeriesAnalysis.TimeSeriesData_PropertyChanged at line 670.
            if (TimeSeriesElement?.TimeSeries == null) return;
            if (TimeSeriesElement.TimeSeries.SuppressCollectionChanged == false)
            {
                if (e.PropertyName == nameof(TimeSeriesElement.TimeSeries))
                    if (ExactDataMethod != ExactDataEntryType.Manual)
                        ClearTimeSeriesResults();

                SetIsValid();
                RaisePropertyChange(e.PropertyName);
            }
        }

        /// <summary>
        /// Handles deletion of the <see cref="TimeSeriesElement"/> from its parent collection.
        /// Clears the reference with undo recording disabled so that pressing Undo cannot
        /// resurrect the now-deleted element. Pre-existing undo history on this InputData is
        /// preserved.
        /// </summary>
        private void OnTimeSeriesElementDeleted(IElement element)
        {
            var wasUndoEnabled = IsUndoEnabled;
            IsUndoEnabled = false;
            try { TimeSeriesElement = null; }
            finally { IsUndoEnabled = wasUndoEnabled; }
        }

        /// <summary>
        /// Clears all time series derived results and resets the IsProcessed flag.
        /// </summary>
        /// <remarks>
        /// This method is called when time series parameters change to ensure stale results are removed.
        /// When derived exact data will use the Multiple Grubbs-Beck Test, the prior threshold is also stale and reset.
        /// </remarks>
        public void ClearTimeSeriesResults()
        {
            if (DataFrame != null && DataFrame.ExactSeries != null)
            {
                DataFrame.ExactSeries.Clear();
                DataFrame.ClearLowOutliers();

                if (ExactDataMethod != ExactDataEntryType.Manual && UseMultipleGrubbsBeckTest)
                {
                    DataFrame.LowOutlierThreshold = 0;
                }
            }
            IsProcessed = false;
        }

        /// <summary>
        /// Creates a block series for exact data from the selected time series using the specified time block parameters.
        /// </summary>
        /// <remarks>
        /// The block series is created by applying the specified block function (e.g., maximum, minimum) to each time block window.
        /// This method validates inputs before processing and sets the IsProcessed flag upon completion.
        /// </remarks>
        public void CreateBlockSeries()
        {
            if (IsTimeSeriesInputValid() == false) return;
            DataFrame.CreateBlockSeries(TimeSeriesElement.TimeSeries, TimeBlock, BlockFunction, SmoothingFunction, StartMonth, EndMonth, Period);
            IsProcessed = true;
        }

        /// <summary>
        /// Creates a peaks-over-threshold series for exact data from the selected time series.
        /// </summary>
        /// <remarks>
        /// Extracts peak values that exceed the specified threshold with minimum separation between peaks.
        /// This method validates inputs before processing and sets the IsProcessed flag upon completion.
        /// </remarks>
        public void CreatePeaksOverThresholdSeries()
        {
            if (IsTimeSeriesInputValid() == false) return;
            DataFrame.CreatePeaksOverThresholdSeries(TimeSeriesElement.TimeSeries, Threshold, MinStepsBetweenPeaks, SmoothingFunction, Period);
            IsProcessed = true;
        }

        /// <summary>
        /// Creates exact data by downloading peak discharge or peak stage data from the USGS National Water Information System (NWIS) API.
        /// </summary>
        /// <param name="cancellationToken">Token used to cancel the USGS download.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <remarks>
        /// The data type (peak discharge or peak stage) is determined by the ExactDataMethod property.
        /// This method sets the IsProcessed flag upon successful completion.
        /// </remarks>
        public async Task CreateFromUSGS(CancellationToken cancellationToken = default)
        {
            if (ExactDataMethod != ExactDataEntryType.USGSPeakDischarge && ExactDataMethod != ExactDataEntryType.USGSPeakStage) return;
            await DataFrame.CreateFromUSGS(USGSSiteNumber, ExactDataMethod == ExactDataEntryType.USGSPeakDischarge ? TimeSeriesDownload.TimeSeriesType.PeakDischarge : TimeSeriesDownload.TimeSeriesType.PeakStage, cancellationToken);
            IsProcessed = true;
        }

        #endregion


        #region Plot Threshold Annotation

        /// <summary>
        /// Adds a red dashed vertical line annotation at the current threshold value to the specified plot.
        /// </summary>
        /// <param name="plot">The OxyPlot plot control to add the annotation to.</param>
        private void AddThresholdAnnotation(Plot plot)
        {
            if (Threshold > 0)
            {
                plot.Annotations.Add(new LineAnnotation()
                {
                    Type = OxyPlot.Annotations.LineAnnotationType.Vertical,
                    X = Threshold,
                    Color = Colors.Red,
                    TextColor = Colors.Red,
                    LineStyle = LineStyle.Dash,
                    StrokeThickness = 1.5,
                    Text = "Threshold",
                    TextLinePosition = 0.95
                });
            }
        }

        /// <summary>
        /// Updates the threshold annotation on all three diagnostic plots without recomputing the diagnostics.
        /// Called when the user changes the threshold value in the properties panel.
        /// </summary>
        public void UpdateThresholdAnnotation()
        {
            using (SuspendPlotBridges())
            {
                foreach (var plot in new[] { _mrlPlot, _modifiedScalePlot, _shapePlot })
                {
                    if (plot == null) continue;
                    plot.SuppressPropertyChanged = true;
                    for (int i = plot.Annotations.Count - 1; i >= 0; i--)
                    {
                        if (plot.Annotations[i] is LineAnnotation la && la.Text == "Threshold")
                            plot.Annotations.RemoveAt(i);
                    }
                    AddThresholdAnnotation(plot);
                    plot.InvalidatePlot(true);
                    plot.SuppressPropertyChanged = false;
                }
            }
            RebuildSeriesAndAnnotationBridges(_mrlPlot);
            RebuildSeriesAndAnnotationBridges(_modifiedScalePlot);
            RebuildSeriesAndAnnotationBridges(_shapePlot);
        }

        #endregion

        #region Plot Factory Methods

        /// <summary>
        /// Applies the default visual style to a plot.
        /// </summary>
        /// <param name="plot">The plot to style.</param>
        private static void ApplyDefaultPlotStyle(Plot plot)
        {
            plot.BorderThickness = new System.Windows.Thickness(0);
            plot.Background = System.Windows.Media.Brushes.Transparent;
            plot.LegendBackground = ColorFromHex("#8CFFFFFF");
            plot.LegendBorder = Colors.DarkGray;
            plot.LegendPosition = OxyPlot.Legends.LegendPosition.TopRight;
            plot.Padding = new System.Windows.Thickness(10);
            plot.PlotAreaBackground = new SolidColorBrush(Colors.White);
        }

        /// <summary>
        /// Parses a hex color string to a <see cref="Color"/>.
        /// </summary>
        /// <param name="hex">The hex color string (e.g., "#FF353B7A").</param>
        /// <returns>The parsed <see cref="Color"/>.</returns>
        private static Color ColorFromHex(string hex)
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }

        /// <summary>
        /// Creates the default chronology plot with linear axes.
        /// </summary>
        private Plot CreateDefaultChronologyPlot()
        {
            var plot = new Plot();
            ApplyDefaultPlotStyle(plot);
            plot.Title = "Chronology";

            plot.Axes.Add(new LinearAxis
            {
                Key = "Yaxis",
                Position = OxyPlot.Axes.AxisPosition.Left,
                Title = _unitLabel,
                AxisTitleDistance = 20,
                TitleFontSize = 16,
                FontSize = 12,
                MajorGridlineStyle = OxyPlot.LineStyle.Solid,
                MinorGridlineStyle = OxyPlot.LineStyle.None,
                StringFormat = "N0",
            });
            plot.Axes.Add(new LinearAxis
            {
                Key = "Xaxis",
                Position = OxyPlot.Axes.AxisPosition.Bottom,
                Title = _indexLabel,
                AxisTitleDistance = 15,
                TitleFontSize = 16,
                FontSize = 12,
                MajorGridlineStyle = OxyPlot.LineStyle.Solid,
                MinorGridlineStyle = OxyPlot.LineStyle.None,
            });

            return plot;
        }

        /// <summary>
        /// Creates the default frequency plot with logarithmic Y-axis and probability X-axis.
        /// </summary>
        private Plot CreateDefaultFrequencyPlot()
        {
            var plot = new Plot();
            ApplyDefaultPlotStyle(plot);
            plot.Title = "Nonparametric Frequency";

            plot.Axes.Add(new LogarithmicAxis
            {
                Key = "Yaxis",
                Position = OxyPlot.Axes.AxisPosition.Left,
                Title = _unitLabel,
                PowerPadding = true,
                AxisTitleDistance = 15,
                TitleFontSize = 16,
                FontSize = 12,
                MajorGridlineStyle = OxyPlot.LineStyle.Solid,
                MinorGridlineStyle = OxyPlot.LineStyle.Dash,
                StringFormat = "N0",
            });
            plot.Axes.Add(new NormalProbabilityAxis
            {
                Key = "Xaxis",
                Position = OxyPlot.Axes.AxisPosition.Bottom,
                Title = "Exceedance Probability",
                Unit = "P(X > x)",
                AxisTitleDistance = 15,
                TitleFontSize = 16,
                FontSize = 12,
                AxislineThickness = 1,
            });

            return plot;
        }

        /// <summary>
        /// Creates the default seasonality plot with DateTimeAxis for monthly data.
        /// </summary>
        private Plot CreateDefaultSeasonalityPlot()
        {
            var plot = new Plot();
            ApplyDefaultPlotStyle(plot);
            plot.Title = "Seasonality";

            plot.Axes.Add(new DateTimeAxis
            {
                Key = "Xaxis",
                Position = OxyPlot.Axes.AxisPosition.Bottom,
                Title = "Month",
                AxisTitleDistance = 15,
                TitleFontSize = 16,
                FontSize = 12,
                MajorGridlineStyle = OxyPlot.LineStyle.Solid,
                MinorGridlineStyle = OxyPlot.LineStyle.None,
                StringFormat = "MMM",
            });
            plot.Axes.Add(new LinearAxis
            {
                Key = "Yaxis",
                Position = OxyPlot.Axes.AxisPosition.Left,
                Title = "Relative Frequency",
                AxisTitleDistance = 20,
                TitleFontSize = 16,
                FontSize = 12,
                MajorGridlineStyle = OxyPlot.LineStyle.Solid,
                MinorGridlineStyle = OxyPlot.LineStyle.None,
                StringFormat = "N2",
            });

            return plot;
        }

        /// <summary>
        /// Creates the default density plot.
        /// </summary>
        private Plot CreateDefaultDensityPlot()
        {
            var plot = new Plot();
            ApplyDefaultPlotStyle(plot);
            plot.Title = "Density";

            plot.Axes.Add(new LinearAxis
            {
                Key = "Xaxis",
                Position = OxyPlot.Axes.AxisPosition.Bottom,
                Title = _unitLabel,
                AxisTitleDistance = 20,
                TitleFontSize = 16,
                FontSize = 12,
                MajorGridlineStyle = OxyPlot.LineStyle.Solid,
                MinorGridlineStyle = OxyPlot.LineStyle.None,
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
                MinorGridlineStyle = OxyPlot.LineStyle.None,
                StringFormat = "E2",
            });

            return plot;
        }

        /// <summary>
        /// Creates the default histogram plot.
        /// </summary>
        private Plot CreateDefaultHistogramPlot()
        {
            var plot = new Plot();
            ApplyDefaultPlotStyle(plot);
            plot.Title = "Histogram";

            plot.Axes.Add(new LinearAxis
            {
                Key = "Xaxis",
                Position = OxyPlot.Axes.AxisPosition.Bottom,
                Title = _unitLabel,
                AxisTitleDistance = 20,
                TitleFontSize = 16,
                FontSize = 12,
                MajorGridlineStyle = OxyPlot.LineStyle.Solid,
                MinorGridlineStyle = OxyPlot.LineStyle.None,
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
                MinorGridlineStyle = OxyPlot.LineStyle.None,
                StringFormat = "E2",
            });

            return plot;
        }

        /// <summary>
        /// Creates the default normal Q-Q plot.
        /// </summary>
        private Plot CreateDefaultQQPlot()
        {
            var plot = new Plot();
            ApplyDefaultPlotStyle(plot);
            plot.Title = "Normal Q-Q Plot";
            plot.LegendPosition = OxyPlot.Legends.LegendPosition.TopLeft;

            plot.Axes.Add(new LinearAxis
            {
                Key = "Xaxis",
                Position = OxyPlot.Axes.AxisPosition.Bottom,
                Title = "Quantile (Standardized)",
                AxisTitleDistance = 20,
                TitleFontSize = 16,
                FontSize = 12,
                MajorGridlineStyle = OxyPlot.LineStyle.Solid,
                MinorGridlineStyle = OxyPlot.LineStyle.None,
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
                MinorGridlineStyle = OxyPlot.LineStyle.None,
                StringFormat = "N0",
            });

            return plot;
        }

        /// <summary>
        /// Creates the default autocorrelation function (ACF) plot.
        /// </summary>
        private Plot CreateDefaultACFPlot()
        {
            var plot = new Plot();
            ApplyDefaultPlotStyle(plot);
            plot.Title = "Autocorrelation Function";

            plot.Axes.Add(new LinearAxis
            {
                Key = "Yaxis",
                Position = OxyPlot.Axes.AxisPosition.Left,
                Title = "Autocorrelation",
                AxisTitleDistance = 20,
                TitleFontSize = 16,
                FontSize = 12,
                MajorGridlineStyle = OxyPlot.LineStyle.Solid,
                MinorGridlineStyle = OxyPlot.LineStyle.None,
                StringFormat = "N2",
            });
            plot.Axes.Add(new LinearAxis
            {
                Key = "Xaxis",
                Position = OxyPlot.Axes.AxisPosition.Bottom,
                Title = "Lag",
                AxisTitleDistance = 20,
                TitleFontSize = 16,
                FontSize = 12,
                MajorGridlineStyle = OxyPlot.LineStyle.Solid,
                MinorGridlineStyle = OxyPlot.LineStyle.None,
            });

            return plot;
        }

        /// <summary>
        /// Creates the default partial autocorrelation function (PACF) plot.
        /// </summary>
        private Plot CreateDefaultPACFPlot()
        {
            var plot = new Plot();
            ApplyDefaultPlotStyle(plot);
            plot.Title = "Partial Autocorrelation Function";

            plot.Axes.Add(new LinearAxis
            {
                Key = "Yaxis",
                Position = OxyPlot.Axes.AxisPosition.Left,
                Title = "Partial Autocorrelation",
                AxisTitleDistance = 20,
                TitleFontSize = 16,
                FontSize = 12,
                MajorGridlineStyle = OxyPlot.LineStyle.Solid,
                MinorGridlineStyle = OxyPlot.LineStyle.None,
                StringFormat = "N2",
            });
            plot.Axes.Add(new LinearAxis
            {
                Key = "Xaxis",
                Position = OxyPlot.Axes.AxisPosition.Bottom,
                Title = "Lag",
                AxisTitleDistance = 20,
                TitleFontSize = 16,
                FontSize = 12,
                MajorGridlineStyle = OxyPlot.LineStyle.Solid,
                MinorGridlineStyle = OxyPlot.LineStyle.None,
            });

            return plot;
        }

        /// <summary>
        /// Creates the default Mean Residual Life (MRL) plot for POT threshold diagnostics.
        /// </summary>
        private Plot CreateDefaultMRLPlot()
        {
            var plot = new Plot();
            ApplyDefaultPlotStyle(plot);
            plot.Title = "Mean Residual Life";

            plot.Axes.Add(new LinearAxis
            {
                Key = "Xaxis",
                Position = OxyPlot.Axes.AxisPosition.Bottom,
                Title = "Threshold",
                AxisTitleDistance = 20,
                TitleFontSize = 16,
                FontSize = 12,
                MajorGridlineStyle = OxyPlot.LineStyle.Solid,
                MinorGridlineStyle = OxyPlot.LineStyle.None,
            });
            plot.Axes.Add(new LinearAxis
            {
                Key = "Yaxis",
                Position = OxyPlot.Axes.AxisPosition.Left,
                Title = "Mean Excess",
                AxisTitleDistance = 20,
                TitleFontSize = 16,
                FontSize = 12,
                MajorGridlineStyle = OxyPlot.LineStyle.Solid,
                MinorGridlineStyle = OxyPlot.LineStyle.None,
            });

            return plot;
        }

        /// <summary>
        /// Creates the default modified scale parameter stability plot for POT threshold diagnostics.
        /// </summary>
        private Plot CreateDefaultModifiedScalePlot()
        {
            var plot = new Plot();
            ApplyDefaultPlotStyle(plot);
            plot.Title = "Modified Scale vs. Threshold";

            plot.Axes.Add(new LinearAxis
            {
                Key = "Xaxis",
                Position = OxyPlot.Axes.AxisPosition.Bottom,
                Title = "Threshold",
                AxisTitleDistance = 20,
                TitleFontSize = 16,
                FontSize = 12,
                MajorGridlineStyle = OxyPlot.LineStyle.Solid,
                MinorGridlineStyle = OxyPlot.LineStyle.None,
            });
            plot.Axes.Add(new LinearAxis
            {
                Key = "Yaxis",
                Position = OxyPlot.Axes.AxisPosition.Left,
                Title = "Modified Scale",
                AxisTitleDistance = 20,
                TitleFontSize = 16,
                FontSize = 12,
                MajorGridlineStyle = OxyPlot.LineStyle.Solid,
                MinorGridlineStyle = OxyPlot.LineStyle.None,
            });

            return plot;
        }

        /// <summary>
        /// Creates the default shape parameter stability plot for POT threshold diagnostics.
        /// </summary>
        private Plot CreateDefaultShapePlot()
        {
            var plot = new Plot();
            ApplyDefaultPlotStyle(plot);
            plot.Title = "Shape vs. Threshold";

            plot.Axes.Add(new LinearAxis
            {
                Key = "Xaxis",
                Position = OxyPlot.Axes.AxisPosition.Bottom,
                Title = "Threshold",
                AxisTitleDistance = 20,
                TitleFontSize = 16,
                FontSize = 12,
                MajorGridlineStyle = OxyPlot.LineStyle.Solid,
                MinorGridlineStyle = OxyPlot.LineStyle.None,
            });
            plot.Axes.Add(new LinearAxis
            {
                Key = "Yaxis",
                Position = OxyPlot.Axes.AxisPosition.Left,
                Title = "Shape",
                AxisTitleDistance = 20,
                TitleFontSize = 16,
                FontSize = 12,
                MajorGridlineStyle = OxyPlot.LineStyle.Solid,
                MinorGridlineStyle = OxyPlot.LineStyle.None,
            });

            return plot;
        }

        /// <summary>
        /// Deserializes plot settings from a database column into the specified plot
        /// using <see cref="PlotSerializer"/>. If the XML cannot be parsed (e.g., from an older
        /// project format), default plot settings are retained.
        /// </summary>
        /// <param name="dtView">The database table view.</param>
        /// <param name="rowIndex">The row index in the table.</param>
        /// <param name="columnName">The column name containing the XML string.</param>
        /// <param name="plot">The plot to deserialize settings into.</param>
        /// <returns><c>true</c> when plot settings were restored; otherwise, <c>false</c>.</returns>
        private static bool DeserializePlotSettings(DatabaseManager.DataTableView dtView, int rowIndex, string columnName, Plot plot)
        {
            if (plot == null) return false;
            if (!dtView.ColumnNames.Contains(columnName)) return false;

            var xml = dtView.GetCell(columnName, rowIndex).ToString();
            if (string.IsNullOrEmpty(xml)) return false;

            try
            {
                PlotSerializer.FromXElement(plot, XElement.Parse(xml));
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Could not deserialize plot settings '{columnName}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Updates default-managed Y-axis titles on applicable plots to match <see cref="UnitLabel"/>.
        /// </summary>
        /// <param name="previousUnitLabel">The unit label value that was automatic before the current update.</param>
        /// <remarks>
        /// User-customized axis titles are preserved. Blank titles opt back into default management.
        /// </remarks>
        private void UpdatePlotYAxisTitles(string previousUnitLabel = null)
        {
            SetAxisTitleIfDefault(_chronologyPlot, "Yaxis", _unitLabel, previousUnitLabel);
            SetAxisTitleIfDefault(_frequencyPlot, "Yaxis", _unitLabel, previousUnitLabel);
            SetAxisTitleIfDefault(_densityPlot, "Xaxis", _unitLabel, previousUnitLabel);
            SetAxisTitleIfDefault(_histogramPlot, "Xaxis", _unitLabel, previousUnitLabel);
        }

        /// <summary>
        /// Updates the default-managed X-axis title on the chronology plot to match <see cref="IndexLabel"/>.
        /// </summary>
        /// <param name="previousIndexLabel">The index label value that was automatic before the current update.</param>
        /// <remarks>
        /// User-customized axis titles are preserved. Blank titles opt back into default management.
        /// </remarks>
        private void UpdatePlotXAxisTitles(string previousIndexLabel = null)
        {
            SetAxisTitleIfDefault(_chronologyPlot, "Xaxis", _indexLabel, previousIndexLabel);
        }

        /// <summary>
        /// Refreshes axis titles after plot settings have been loaded from disk.
        /// </summary>
        /// <param name="chronologyPlotRestored">Whether chronology plot settings were restored from storage.</param>
        /// <param name="frequencyPlotRestored">Whether frequency plot settings were restored from storage.</param>
        /// <param name="densityPlotRestored">Whether density plot settings were restored from storage.</param>
        /// <param name="histogramPlotRestored">Whether histogram plot settings were restored from storage.</param>
        /// <remarks>
        /// Factory plots still use the original <c>Value</c> and <c>Year</c> defaults and should be
        /// advanced to the current source labels. Successfully deserialized titles are treated as
        /// intentional unless they are blank or already match the current source label.
        /// </remarks>
        private void RefreshPlotAxisTitlesAfterOpen(
            bool chronologyPlotRestored,
            bool frequencyPlotRestored,
            bool densityPlotRestored,
            bool histogramPlotRestored)
        {
            string chronologyPreviousUnitLabel = chronologyPlotRestored ? null : DefaultUnitLabel;
            string chronologyPreviousIndexLabel = chronologyPlotRestored ? null : DefaultIndexLabel;

            SetAxisTitleIfDefault(_chronologyPlot, "Yaxis", _unitLabel, chronologyPreviousUnitLabel);
            SetAxisTitleIfDefault(_chronologyPlot, "Xaxis", _indexLabel, chronologyPreviousIndexLabel);
            SetAxisTitleIfDefault(_frequencyPlot, "Yaxis", _unitLabel, frequencyPlotRestored ? null : DefaultUnitLabel);
            SetAxisTitleIfDefault(_densityPlot, "Xaxis", _unitLabel, densityPlotRestored ? null : DefaultUnitLabel);
            SetAxisTitleIfDefault(_histogramPlot, "Xaxis", _unitLabel, histogramPlotRestored ? null : DefaultUnitLabel);
        }

        /// <summary>
        /// Sets the title of a specific axis on a plot when the axis still uses an automatic title.
        /// </summary>
        /// <param name="plot">The plot containing the axis.</param>
        /// <param name="axisKey">The axis key.</param>
        /// <param name="title">The automatic title to apply.</param>
        /// <param name="previousTitle">The automatic title that was valid before the current update.</param>
        /// <remarks>
        /// The helper centralizes the beta.3 default-versus-custom title behavior for element-owned plots.
        /// </remarks>
        private static void SetAxisTitleIfDefault(Plot plot, string axisKey, string title, string previousTitle = null)
        {
            if (plot == null) return;
            var axis = plot.Axes.FirstOrDefault(a => a.Key == axisKey);
            if (axis != null && PlotAxisTitleDefaults.SetTitleIfDefault(axis, title, previousTitle))
            {
                plot.InvalidatePlot(false);
            }
        }

        #endregion
        #region Undo Bridge Management

        /// <summary>
        /// Initializes undo bridges for the DataFrame properties and collections.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Since DataFrame is in the model library (no FrameworkInterfaces reference),
        /// we bridge its INotifyPropertyChanged and INotifyCollectionChanged events
        /// to the undo system from the UI layer.
        /// </para>
        /// <para>
        /// Cell-level edits are handled by the A2 clone-and-replace pattern: RowItem setters
        /// clone the Data object, modify the clone, and replace it in the series via the indexer.
        /// This fires CollectionChanged(Replace) which the bridge records � no per-item
        /// UndoableStateBridge is needed.
        /// </para>
        /// <para>
        /// Each collection bridge has a BulkRestoreWrapper that suppresses intermediate
        /// CollectionChanged events during undo/redo replay of Reset actions. This prevents
        /// O(n�) CalculatePlottingPositions calls when the bridge's Clear+AddAll loop replays.
        /// </para>
        /// </remarks>
        private void SetupBridges()
        {
            DisposeBridges();

            // Subscribe to UndoManager.StateChanged to revalidate after undo/redo completes.
            UndoManager.StateChanged += UndoManager_StateChanged;

            if (_dataFrame == null) return;

            // Bridge DataFrame scalar properties (LowOutlierThreshold, PlottingParameter)
            _dataFrameBridge = new UndoableStateBridge(
                _dataFrame,
                () => IsUndoEnabled ? UndoManager : null,
                "data frame",
                this,
                includedProperties: new[] { nameof(DataFrame.LowOutlierThreshold), nameof(DataFrame.PlottingParameter) }
            );

            // Bridge each data series collection for row-level add/remove/replace/clear
            _exactSeriesBridge = new UndoableCollectionBridge<Data>(
                _dataFrame.ExactSeries,
                () => IsUndoEnabled ? UndoManager : null,
                "exact data",
                this
            );
            _exactSeriesBridge.BulkRestoreWrapper = CreateBulkRestoreWrapper(_dataFrame.ExactSeries);

            _uncertainSeriesBridge = new UndoableCollectionBridge<Data>(
                _dataFrame.UncertainSeries,
                () => IsUndoEnabled ? UndoManager : null,
                "uncertain data",
                this
            );
            _uncertainSeriesBridge.BulkRestoreWrapper = CreateBulkRestoreWrapper(_dataFrame.UncertainSeries);

            _intervalSeriesBridge = new UndoableCollectionBridge<Data>(
                _dataFrame.IntervalSeries,
                () => IsUndoEnabled ? UndoManager : null,
                "interval data",
                this
            );
            _intervalSeriesBridge.BulkRestoreWrapper = CreateBulkRestoreWrapper(_dataFrame.IntervalSeries);

            _thresholdSeriesBridge = new UndoableCollectionBridge<Data>(
                _dataFrame.ThresholdSeries,
                () => IsUndoEnabled ? UndoManager : null,
                "threshold data",
                this
            );
            _thresholdSeriesBridge.BulkRestoreWrapper = CreateBulkRestoreWrapper(_dataFrame.ThresholdSeries);

            // Create plot undo managers � each monitors its plot's axes, series, and annotations
            // for collection changes and auto-rebuilds bridges as needed.
            Func<IUndoManager> getUndo = () => IsUndoEnabled ? UndoManager : null;
            Action onRecorded = () => SetIsDirty(true);

            if (_chronologyPlot != null) _chronologyPlotUndo = new PlotUndoManager(_chronologyPlot, getUndo, "chronology plot", this, onRecorded);
            if (_frequencyPlot != null) _frequencyPlotUndo = new PlotUndoManager(_frequencyPlot, getUndo, "frequency plot", this, onRecorded);
            if (_seasonalityPlot != null) _seasonalityPlotUndo = new PlotUndoManager(_seasonalityPlot, getUndo, "seasonality plot", this, onRecorded);
            if (_densityPlot != null) _densityPlotUndo = new PlotUndoManager(_densityPlot, getUndo, "density plot", this, onRecorded);
            if (_histogramPlot != null) _histogramPlotUndo = new PlotUndoManager(_histogramPlot, getUndo, "histogram plot", this, onRecorded);
            if (_qqPlot != null) _qqPlotUndo = new PlotUndoManager(_qqPlot, getUndo, "QQ plot", this, onRecorded);
            if (_acfPlot != null) _acfPlotUndo = new PlotUndoManager(_acfPlot, getUndo, "ACF plot", this, onRecorded);
            if (_pacfPlot != null) _pacfPlotUndo = new PlotUndoManager(_pacfPlot, getUndo, "PACF plot", this, onRecorded);
            if (_mrlPlot != null) _mrlPlotUndo = new PlotUndoManager(_mrlPlot, getUndo, "MRL plot", this, onRecorded);
            if (_modifiedScalePlot != null) _modifiedScalePlotUndo = new PlotUndoManager(_modifiedScalePlot, getUndo, "modified scale plot", this, onRecorded);
            if (_shapePlot != null) _shapePlotUndo = new PlotUndoManager(_shapePlot, getUndo, "shape plot", this, onRecorded);
        }

        /// <summary>
        /// Creates a BulkRestoreWrapper callback for a DataSeries.
        /// The wrapper suppresses CollectionChanged on the series during the restore action,
        /// then raises a single Reset event afterward.
        /// </summary>
        /// <param name="series">The data series to wrap.</param>
        /// <returns>An action that wraps a restore delegate with suppress/reset.</returns>
        private Action<Action> CreateBulkRestoreWrapper(DataSeries series)
        {
            return (restoreAction) =>
            {
                series.SuppressCollectionChanged = true;
                restoreAction();
                series.SuppressCollectionChanged = false;
                series.RaiseCollectionChangedReset();
            };
        }

        /// <summary>
        /// Handles UndoManager.StateChanged to revalidate the element after undo/redo.
        /// </summary>
        private void UndoManager_StateChanged(object sender, EventArgs e)
        {
            SetIsValid();
        }

        /// <summary>
        /// Disposes all undo bridges and unsubscribes <see cref="UndoManager.StateChanged"/>.
        /// Called from <see cref="SetupBridges"/> at the start of bridge re-creation and from
        /// <see cref="Delete"/> to release subscriptions before the element is destroyed.
        /// </summary>
        /// <remarks>
        /// <b>Maintenance contract:</b> every bridge field (UndoableStateBridge,
        /// UndoableCollectionBridge, PlotUndoManager, etc.) declared in <see cref="SetupBridges"/>
        /// MUST have a corresponding <c>?.Dispose(); = null;</c> line here. Missing one will
        /// leak the bridge's internal subscriptions across element lifetime cycles. The
        /// field-cached pattern is preferred over a generic <c>List&lt;IDisposable&gt;</c> because the
        /// named fields are needed in <see cref="SetupBridges"/> to assign each typed instance,
        /// and dual-state (field + list) introduces drift risk of its own.
        /// </remarks>
        private void DisposeBridges()
        {
            UndoManager.StateChanged -= UndoManager_StateChanged;

            _dataFrameBridge?.Dispose(); _dataFrameBridge = null;
            _exactSeriesBridge?.Dispose(); _exactSeriesBridge = null;
            _uncertainSeriesBridge?.Dispose(); _uncertainSeriesBridge = null;
            _intervalSeriesBridge?.Dispose(); _intervalSeriesBridge = null;
            _thresholdSeriesBridge?.Dispose(); _thresholdSeriesBridge = null;

            _chronologyPlotUndo?.Dispose(); _chronologyPlotUndo = null;
            _frequencyPlotUndo?.Dispose(); _frequencyPlotUndo = null;
            _seasonalityPlotUndo?.Dispose(); _seasonalityPlotUndo = null;
            _densityPlotUndo?.Dispose(); _densityPlotUndo = null;
            _histogramPlotUndo?.Dispose(); _histogramPlotUndo = null;
            _qqPlotUndo?.Dispose(); _qqPlotUndo = null;
            _acfPlotUndo?.Dispose(); _acfPlotUndo = null;
            _pacfPlotUndo?.Dispose(); _pacfPlotUndo = null;
            _mrlPlotUndo?.Dispose(); _mrlPlotUndo = null;
            _modifiedScalePlotUndo?.Dispose(); _modifiedScalePlotUndo = null;
            _shapePlotUndo?.Dispose(); _shapePlotUndo = null;
        }

        /// <summary>
        /// Rebuilds per-item state bridges for series and annotations after UpdateXxxPlot() calls.
        /// Call this AFTER adding series/annotations to the plot and OUTSIDE <see cref="SuspendPlotBridges"/>.
        /// </summary>
        /// <param name="plot">The plot whose series/annotation bridges should be rebuilt.</param>
        public void RebuildSeriesAndAnnotationBridges(Plot plot)
        {
            FindPlotUndoManager(plot)?.RebuildSeriesAndAnnotationBridges();
        }

        /// <summary>
        /// Suspends all plot bridges to prevent spurious undo entries during data updates.
        /// </summary>
        /// <returns>An <see cref="IDisposable"/> that resumes recording when disposed.</returns>
        public IDisposable SuspendPlotBridges()
        {
            var suspensions = new List<IDisposable>();
            if (_chronologyPlotUndo != null) suspensions.Add(_chronologyPlotUndo.SuspendRecording());
            if (_frequencyPlotUndo != null) suspensions.Add(_frequencyPlotUndo.SuspendRecording());
            if (_seasonalityPlotUndo != null) suspensions.Add(_seasonalityPlotUndo.SuspendRecording());
            if (_densityPlotUndo != null) suspensions.Add(_densityPlotUndo.SuspendRecording());
            if (_histogramPlotUndo != null) suspensions.Add(_histogramPlotUndo.SuspendRecording());
            if (_qqPlotUndo != null) suspensions.Add(_qqPlotUndo.SuspendRecording());
            if (_acfPlotUndo != null) suspensions.Add(_acfPlotUndo.SuspendRecording());
            if (_pacfPlotUndo != null) suspensions.Add(_pacfPlotUndo.SuspendRecording());
            if (_mrlPlotUndo != null) suspensions.Add(_mrlPlotUndo.SuspendRecording());
            if (_modifiedScalePlotUndo != null) suspensions.Add(_modifiedScalePlotUndo.SuspendRecording());
            if (_shapePlotUndo != null) suspensions.Add(_shapePlotUndo.SuspendRecording());
            return new AggregateDisposable(suspensions);
        }

        /// <summary>
        /// Finds the <see cref="PlotUndoManager"/> for the specified plot.
        /// </summary>
        /// <param name="plot">The plot to look up.</param>
        /// <returns>The corresponding <see cref="PlotUndoManager"/>, or null if not found.</returns>
        private PlotUndoManager FindPlotUndoManager(Plot plot)
        {
            if (plot == _chronologyPlot) return _chronologyPlotUndo;
            if (plot == _frequencyPlot) return _frequencyPlotUndo;
            if (plot == _seasonalityPlot) return _seasonalityPlotUndo;
            if (plot == _densityPlot) return _densityPlotUndo;
            if (plot == _histogramPlot) return _histogramPlotUndo;
            if (plot == _qqPlot) return _qqPlotUndo;
            if (plot == _acfPlot) return _acfPlotUndo;
            if (plot == _pacfPlot) return _pacfPlotUndo;
            if (plot == _mrlPlot) return _mrlPlotUndo;
            if (plot == _modifiedScalePlot) return _modifiedScalePlotUndo;
            if (plot == _shapePlot) return _shapePlotUndo;
            return null;
        }

        #endregion

    }
}
