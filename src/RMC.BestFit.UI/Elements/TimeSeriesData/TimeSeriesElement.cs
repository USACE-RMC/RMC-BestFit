using DatabaseManager;
using FrameworkInterfaces;
using FrameworkInterfaces.Undo;
using Numerics;
using Numerics.Data;
using Numerics.Data.Statistics;
using Numerics.Utilities;
using OxyPlot.Wpf;
using OxyPlot.Wpf.Serialization;
using RMC.BestFit.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Xml;
using System.Xml.Linq;
using static Numerics.Data.TimeSeriesDownload;


namespace RMC.BestFit.UI
{
    /// <summary>
    /// Represents a time-series element that wraps a <see cref="Numerics.Data.TimeSeries"/> with
    /// project-tree metadata, validation, undo/redo, and optional plot persistence. Acts as the
    /// shared time-series source for downstream <see cref="InputData"/> peaks-over-threshold and
    /// block-maxima derivations and for <see cref="TimeSeriesAnalysis"/> covariates.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// </remarks>
    [Category("General"),
    DisplayName("Time Series Data"),
    Description("A time series can be created from manual entry or by importing from HEC-DSS, USGS, GHCN, CHMN, and ABOM."),
    Browsable(true)]
    public class TimeSeriesElement : ElementBase
    {
        #region Construction

        /// <summary>
        /// Constructs a new time series class.
        /// </summary>
        /// <param name="name">The name of the element.</param>
        /// <param name="parentCollection">The parent collection name.</param>
        /// <param name="openFromFile">Optional parameter to open the function from disk upon construction.</param>
        public TimeSeriesElement(string name = "Time Series Data", IElementCollection parentCollection = null, bool openFromFile = false) : base(name, parentCollection)
        {
            IsUndoEnabled = false;
            try
            {
                // Create dummy collection
                if (parentCollection == null)
                {
                    this.ParentCollection = new TimeSeriesCollection(BestFitProject.GetInstance());
                }

                _nameOnDisk = name;
                _creationDate = DateTime.Now;
                _lastModified = DateTime.Now;

                // Add messages
                _descriptionMsg = new BasicMessageItem(MessageType.Message, "The time series does not have a description.", this, ParentCollection.Name, Name, nameof(Description), "TS-MSG-001");
                _unitLabelMsg = new BasicMessageItem(MessageType.Error, "The unit label cannot be empty.", this, ParentCollection.Name, Name, nameof(UnitLabel), "TS-ERR-005");
                _hecDSSFilenameMsg = new BasicMessageItem(MessageType.Error, "The HEC-DSS filename must have a '.dss' extension.", this, ParentCollection.Name, Name, nameof(HECDSSFullFilename), "TS-ERR-006");
                _hecDSSPathnameMsg = new BasicMessageItem(MessageType.Error, "The HEC-DSS pathname must consist of 6 parts separated by '/' (e.g., /A/B/C/D/E/F/).", this, ParentCollection.Name, Name, nameof(HECDSSDataPathname), "TS-ERR-007");
                _ghcnSiteNumberMsg = new BasicMessageItem(MessageType.Error, "The GHCN site number must be 11 digits long.", this, ParentCollection.Name, Name, nameof(GHCNSiteNumber), "TS-ERR-008");
                _usgsSiteNumberMsg = new BasicMessageItem(MessageType.Error, "The USGS site number must be 8 to 15 digits long.", this, ParentCollection.Name, Name, nameof(USGSSiteNumber), "TS-ERR-009");
                _chmnSiteNumberMsg = new BasicMessageItem(MessageType.Error, "The CHMN site number must be 7 characters long.", this, ParentCollection.Name, Name, nameof(CHMNSiteNumber), "TS-ERR-010");
                _abomSiteNumberMsg = new BasicMessageItem(MessageType.Error, "The ABOM site number must be 6 digits long.", this, ParentCollection.Name, Name, nameof(ABOMSiteNumber), "TS-ERR-011");
                _timeSeriesDataMsg = new BasicMessageItem(MessageType.Error, "The time series must contain more than 2 ordinates.", this, ParentCollection.Name, Name, nameof(TimeSeries), "TS-ERR-012");
                _timeSeriesOrderMsg = new BasicMessageItem(MessageType.Error, "Irregular time-series date/time values must be in ascending data order. Grid sorting does not reorder the time series used by the plot.", this, ParentCollection.Name, Name, nameof(TimeSeries), "TS-ERR-013");
                _missingDataMsg = new BasicMessageItem(MessageType.Warning, "Missing data detected; analysis may be affected.", this, ParentCollection.Name, Name, nameof(TimeSeries), "TS-WNG-005");
                _messages = new List<BasicMessageItem>() { _descriptionMsg, _unitLabelMsg, _hecDSSFilenameMsg, _hecDSSPathnameMsg, _ghcnSiteNumberMsg, _usgsSiteNumberMsg, _chmnSiteNumberMsg, _abomSiteNumberMsg, _timeSeriesDataMsg, _timeSeriesOrderMsg, _missingDataMsg };

                // Create messenger
                _messenger = FrameworkInterfaces.Messaging.Messenger.GetInstance();
                _messenger.Add(_descriptionMsg);
                _messenger.Add(_timeSeriesDataMsg);

                // Populate the placeholder before any event or undo subscriptions are installed.
                // The constructor's finally block installs the complete tracking graph once.
                _timeSeries = CreatePlaceholderTimeSeries(TimeInterval, StartDateTime);
                AttachTimeSeriesEventHandlers(_timeSeries);

                // Create default plots
                _timeSeriesPlot = CreateDefaultTimeSeriesPlot();
                _seasonalityPlot = CreateDefaultSeasonalityPlot();
                _acfPlot = CreateDefaultACFPlot();
                _pacfPlot = CreateDefaultPACFPlot();

                if (openFromFile == true)
                {
                    _deferBridgeSetup = true;
                    Open();
                }
                else
                {
                    _nameValid = ValidateName(BestFitProject.InvalidNameCharacters, 50, "TS");
                    SetIsValid();
                }
                SetIsDirty(false);
            }
            finally
            {
                _deferBridgeSetup = false;
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
        [Description("Unique label identifying this time series element; max 50 characters.")]
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
                    _nameValid = ValidateName(BestFitProject.InvalidNameCharacters, 50, "TS");
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
        [Description("Free-text annotation describing this time series element.")]
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
        [Description("The date and time when the time series data was first created.")]
        [Browsable(true)]
        public override DateTime CreationDate => _creationDate;

        /// <summary>
        /// Gets and sets the date when the element was last modified.
        /// </summary>
        [Category("Meta Data")]
        [DisplayName("Last Edited")]
        [Description("The date and time when the time series data was last modified.")]
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
            System.Windows.Application.Current?.TryFindResource("TimeSeriesDataIcon") as System.Windows.Media.ImageSource;

        /// <summary>
        /// Gets the resource key for the element icon, enabling dynamic theme updates.
        /// The project explorer uses <see cref="System.Windows.FrameworkElement.SetResourceReference"/>
        /// with this key so the icon updates automatically when the theme changes.
        /// </summary>
        public string ElementImageResourceKey => "TimeSeriesDataIcon";

        /// <summary>
        /// Determines if the element can be copied from an external application.
        /// </summary>
        public override bool CanCopyFromExternal
        {
            get { return true; }
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

        // Error messaging
        /// <summary>
        /// The list of message items for validation and error reporting.
        /// </summary>
        private List<BasicMessageItem> _messages;

        /// <summary>
        /// The messenger instance for managing and broadcasting messages.
        /// </summary>
        private FrameworkInterfaces.Messaging.Messenger _messenger;

        /// <summary>
        /// Message item for missing time series description.
        /// </summary>
        private BasicMessageItem _descriptionMsg;

        /// <summary>
        /// Error message item for missing unit label.
        /// </summary>
        private BasicMessageItem _unitLabelMsg;

        /// <summary>
        /// Error message item for invalid HEC-DSS filename.
        /// </summary>
        private BasicMessageItem _hecDSSFilenameMsg;

        /// <summary>
        /// Error message item for invalid HEC-DSS pathname.
        /// </summary>
        private BasicMessageItem _hecDSSPathnameMsg;

        /// <summary>
        /// Error message item for invalid USGS site number.
        /// </summary>
        private BasicMessageItem _usgsSiteNumberMsg;

        /// <summary>
        /// Error message item for invalid GHCN site number.
        /// </summary>
        private BasicMessageItem _ghcnSiteNumberMsg;

        /// <summary>
        /// Error message item for invalid CHMN site number.
        /// </summary>
        private BasicMessageItem _chmnSiteNumberMsg;

        /// <summary>
        /// Error message item for invalid ABOM site number.
        /// </summary>
        private BasicMessageItem _abomSiteNumberMsg;

        /// <summary>
        /// Error message item for insufficient time series data.
        /// </summary>
        private BasicMessageItem _timeSeriesDataMsg;

        /// <summary>
        /// Error message item for irregular time series data that is not in ascending date-time order.
        /// </summary>
        private BasicMessageItem _timeSeriesOrderMsg;

        /// <summary>
        /// Warning message item for missing data in time series.
        /// </summary>
        private BasicMessageItem _missingDataMsg;

        // Is valid properties
        /// <summary>
        /// Indicates whether the time series name is valid.
        /// </summary>
        private bool _nameValid = false;

        /// <summary>
        /// Indicates whether the unit label is valid.
        /// </summary>
        private bool _unitLabelValid = true;

        /// <summary>
        /// Indicates whether the USGS site number is valid.
        /// </summary>
        private bool _usgsSiteNumberValid = true;

        /// <summary>
        /// Indicates whether the GHCN site number is valid.
        /// </summary>
        private bool _ghcnSiteNumberValid = true;

        /// <summary>
        /// Indicates whether the CHMN site number is valid.
        /// </summary>
        private bool _chmnSiteNumberValid = true;

        /// <summary>
        /// Indicates whether the ABOM site number is valid.
        /// </summary>
        private bool _abomSiteNumberValid = true;

        /// <summary>
        /// Indicates whether the time series data is valid.
        /// </summary>
        private bool _timeSeriesValid = true;

        // properties
        /// <summary>
        /// The underlying time series data collection.
        /// </summary>
        private TimeSeries _timeSeries;

        /// <summary>
        /// The method used to enter or import time series data.
        /// </summary>
        private TimeSeriesEntryMethod _entryMethod = TimeSeriesEntryMethod.Manual;

        /// <summary>
        /// The type of time series data.
        /// </summary>
        private TimeSeriesType _seriesType = TimeSeriesType.DailyDischarge;

        /// <summary>
        /// The depth unit for downloaded time series data.
        /// </summary>
        private DepthUnit _depthUnit = DepthUnit.Inches;

        /// <summary>
        /// The discharge unit for downloaded time series data.
        /// </summary>
        private DischargeUnit _dischargeUnit = DischargeUnit.CubicFeetPerSecond;

        /// <summary>
        /// The height unit for downloaded time series data.
        /// </summary>
        private HeightUnit _heightUnit = HeightUnit.Feet;

        /// <summary>
        /// The time interval between data points in the time series.
        /// </summary>
        private TimeInterval _timeInterval = TimeInterval.OneDay;

        /// <summary>
        /// The start date and time of the time series.
        /// </summary>
        private DateTime _startDateTime = new DateTime(2000, 1, 1, 0, 0, 0);

        /// <summary>
        /// The factory-default data unit label.
        /// </summary>
        private const string DefaultUnitLabel = "Value";

        /// <summary>
        /// The label describing the units of the time series values.
        /// </summary>
        private string _unitLabel = DefaultUnitLabel;

        /// <summary>
        /// The full file path to the HEC-DSS file.
        /// </summary>
        private string _hecDSSFullFilename = "";

        /// <summary>
        /// The HEC-DSS pathname for the time series data.
        /// </summary>
        private string _hecDSSDataPathname = "";

        /// <summary>
        /// The GHCN station identification code.
        /// </summary>
        private string _ghcnSiteNumber = "00000000000";

        /// <summary>
        /// The USGS site number.
        /// </summary>
        private string _usgsSiteNumber = "00000000";

        /// <summary>
        /// The CHMN site number.
        /// </summary>
        private string _chmnSiteNumber = "0000000";

        /// <summary>
        /// The ABOM site number.
        /// </summary>
        private string _abomSiteNumber = "000000";

        /// <summary>
        /// The raw text data from USGS annual peak downloads.
        /// </summary>
        private string _usgsRawText = "";

        /// <summary>
        /// Compressed USGS response retained from project storage until the raw text is requested.
        /// </summary>
        private byte[] _usgsRawTextCompressed = Array.Empty<byte>();

        /// <summary>
        /// Legacy text used only if a lazily decompressed USGS response is corrupt.
        /// </summary>
        private string _usgsRawTextLegacyFallback = "";

        /// <summary>
        /// Indicates whether <see cref="USGSRawText"/> has been expanded into a managed string.
        /// </summary>
        private bool _usgsRawTextMaterialized = true;

        /// <summary>
        /// Defers undo bridge rebuilding while a constructor or copy operation performs a bulk replacement.
        /// </summary>
        /// <remarks>
        /// Event handlers remain fully installed for every ordinate. This flag only prevents building and
        /// immediately discarding a complete undo shadow before the final collection has been assigned.
        /// </remarks>
        private bool _deferBridgeSetup;

        /// <summary>
        /// The time series plot.
        /// </summary>
        private Plot _timeSeriesPlot;

        /// <summary>
        /// The seasonality plot.
        /// </summary>
        private Plot _seasonalityPlot;

        /// <summary>
        /// The autocorrelation function (ACF) plot.
        /// </summary>
        private Plot _acfPlot;

        /// <summary>
        /// The partial autocorrelation function (PACF) plot.
        /// </summary>
        private Plot _pacfPlot;

        /// <summary>
        /// Undo bridge for the time series collection.
        /// </summary>
        private UndoableCollectionBridge<SeriesOrdinate<DateTime, double>> _timeSeriesBridge;

        /// <summary>
        /// Per-plot undo managers for undo/redo of plot visual properties.
        /// </summary>
        private PlotUndoManager _timeSeriesPlotUndo;
        private PlotUndoManager _seasonalityPlotUndo;
        private PlotUndoManager _acfPlotUndo;
        private PlotUndoManager _pacfPlotUndo;

        /// <summary>
        /// Specifies the method used to enter or import time series data.
        /// </summary>
        public enum TimeSeriesEntryMethod
        {
            /// <summary>
            /// Time series data is manually entered by the user.
            /// </summary>
            Manual,

            /// <summary>
            /// Time series data is imported from a HEC-DSS file.
            /// </summary>
            HECDSS,

            /// <summary>
            /// Time series data is downloaded from the Global Historical Climate Network (GHCN).
            /// </summary>
            GHCN,

            /// <summary>
            /// Time series data is downloaded from the United States Geological Survey (USGS).
            /// </summary>
            USGS,

            /// <summary>
            /// Time series data is downloaded from the Canadian Hydrometric Monitoring Network (CHMN).
            /// </summary>
            CHMN,

            /// <summary>
            /// Time series data is downloaded from the Australian Bureau of Meteorology (ABOM).
            /// </summary>
            ABOM,

            /// <summary>
            /// Legacy option for GHCN daily precipitation data. Maintained for backward compatibility with version 1.0 projects.
            /// </summary>
            GHCNDailyPrecipitation,

            /// <summary>
            /// Legacy option for USGS daily discharge data. Maintained for backward compatibility with version 1.0 projects.
            /// </summary>
            USGSDailyDischarge,

            /// <summary>
            /// Legacy option for USGS daily stage data. Maintained for backward compatibility with version 1.0 projects.
            /// </summary>
            USGSDailyStage,
        }

        #endregion

        #region Properties

        /// <summary>
        /// Determines how the time series data is entered.
        /// </summary>
        /// <remarks>
        /// When changed from Manual to another method, resets the time series data.
        /// When changed to GHCN, sets SeriesType to DailyPrecipitation.
        /// When changed to USGS, CHMN, or ABOM, sets SeriesType to DailyDischarge.
        /// Side effects are suppressed during undo/redo replay.
        /// </remarks>
        [Category("General")]
        [DisplayName("Time Series Entry Method")]
        [Description("Determines how the time series data is entered. Data can be manually entered or imported from HEC-DSS, USGS, GHCN, CHMN, or ABOM.")]
        [Browsable(true)]
        public TimeSeriesEntryMethod EntryMethod
        {
            get { return _entryMethod; }
            set
            {
                if (_entryMethod != value)
                {
                    var old = _entryMethod;
                    _entryMethod = value;

                    if (!UndoManager.IsExecutingAction)
                    {
                        if (_entryMethod != TimeSeriesEntryMethod.Manual)
                            TimeSeries = CreatePlaceholderTimeSeries(TimeInterval, StartDateTime);

                        if (_entryMethod == TimeSeriesEntryMethod.GHCN)
                        {
                            SeriesType = TimeSeriesType.DailyPrecipitation;
                        }
                        if (_entryMethod == TimeSeriesEntryMethod.USGS ||
                            _entryMethod == TimeSeriesEntryMethod.CHMN ||
                            _entryMethod == TimeSeriesEntryMethod.ABOM)
                        {
                            SeriesType = TimeSeriesType.DailyDischarge;
                        }
                    }

                    SetIsValid();
                    RecordPropertyChange(nameof(EntryMethod), old, value);
                }
            }
        }

        /// <summary>
        /// Gets or sets the type of time series data to be downloaded.
        /// </summary>
        [Category("General")]
        [DisplayName("Time Series Data Type")]
        [Description("The time series data type to be downloaded, e.g., daily discharge or daily rainfall.")]
        [Browsable(true)]
        public TimeSeriesType SeriesType
        {
            get { return _seriesType; }
            set
            {
                if (_seriesType != value)
                {
                    var old = _seriesType;
                    _seriesType = value;

                    using (SuspendPlotBridges())
                    {
                        UpdatePlotYAxisTitles(_unitLabel);
                    }
                    SetIsValid();
                    RecordPropertyChange(nameof(SeriesType), old, value);
                }
            }
        }

        /// <summary>
        /// Gets or sets the time series data collection.
        /// </summary>
        /// <remarks>
        /// When set, unsubscribes CollectionChanged and PropertyChanged handlers from the old series,
        /// subscribes to the new series, and calls <see cref="SetIsValid"/>.
        /// This property uses <see cref="ElementBase.RaisePropertyChange"/> (not undoable) because
        /// collection-level undo is managed by the <see cref="UndoableCollectionBridge{T}"/>.
        /// </remarks>
        public TimeSeries TimeSeries
        {
            get { return _timeSeries; }
            set
            {
                DetachTimeSeriesEventHandlers(_timeSeries);
                    
                _timeSeries = value;

                AttachTimeSeriesEventHandlers(_timeSeries);

                if (!_deferBridgeSetup) SetupBridges();
                SetIsValid();
                RaisePropertyChange(nameof(TimeSeries));
            }
        }

        /// <summary>
        /// The time interval for the time-series.
        /// </summary>
        /// <remarks>
        /// When changed, recreates the time series data with the new interval while preserving values.
        /// For regular intervals, dates are recalculated from <see cref="StartDateTime"/>.
        /// For irregular intervals, existing date-time/value pairs are cloned.
        /// Side effects are suppressed during undo/redo replay.
        /// </remarks>
        [Category("General")]
        [DisplayName("Time Interval")]
        [Description("The time interval for the time series data. Date-times are automatically set based on the selected interval and start date-time.")]
        [Browsable(true)]
        public TimeInterval TimeInterval
        {
            get { return _timeInterval; }
            set
            {
                if (_timeInterval != value)
                {
                    var old = _timeInterval;
                    _timeInterval = value;

                    if (!UndoManager.IsExecutingAction)
                    {
                        // Create new time series
                        if (_timeInterval != TimeInterval.Irregular)
                        {
                            TimeSeries = new TimeSeries(TimeInterval, StartDateTime, TimeSeries.ValuesToArray());
                        }
                        else
                        {
                            var newTS = new TimeSeries(TimeInterval);
                            for (int i = 0; i < TimeSeries.Count; i++)
                            {
                                newTS.Add(TimeSeries[i].Clone());
                            }
                            TimeSeries = newTS;
                        }
                    }

                    SetIsValid();
                    RecordPropertyChange(nameof(TimeInterval), old, value);
                }
            }
        }

        /// <summary>
        /// The start date and time of the time-series.
        /// </summary>
        /// <remarks>
        /// When changed, calls <see cref="TimeSeries.ShiftAllDates"/> to recalculate all date-times
        /// in the time series based on the new start date.
        /// Side effects are suppressed during undo/redo replay.
        /// </remarks>
        [Category("General")]
        [DisplayName("Start Date and Time")]
        [Description("The start date and time of the time series data. Date-times are automatically set based on the selected interval and start date-time.")]
        [Browsable(true)]
        public DateTime StartDateTime
        {
            get { return _startDateTime; }
            set
            {
                if (_startDateTime != value)
                {
                    var old = _startDateTime;
                    _startDateTime = value;

                    if (!UndoManager.IsExecutingAction)
                    {
                        if (TimeSeries != null)
                        {
                            TimeSeries.ShiftAllDates(_startDateTime);
                            RaisePropertyChange(nameof(TimeSeries));
                        }
                    }

                    SetIsValid();
                    RecordPropertyChange(nameof(StartDateTime), old, value);
                }
            }
        }

        /// <summary>
        /// The data unit label. 
        /// </summary>
        [Category("General")]
        [DisplayName("Unit Label")]
        [Description("The data units; e.g., Water Surface Elevation (ft), Flow (cfs), or Rainfall (in).")]
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
                    if (_unitLabel == "" || _unitLabel == null)
                    {
                        _unitLabelValid = false;
                        _messenger.Add(_unitLabelMsg);
                    }
                    else
                    {
                        _unitLabelValid = true;
                        _messenger.Remove(_unitLabelMsg);
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
        /// The HEC-DSS full filename.
        /// </summary>
        [Category("General")]
        [DisplayName("HEC-DSS Full Filename")]
        [Description("The full filename of the HEC-DSS (.dss) file.")]
        [Browsable(true)]
        public string HECDSSFullFilename
        {
            get { return _hecDSSFullFilename; }
            set
            {
                if (_hecDSSFullFilename != value)
                {
                    var old = _hecDSSFullFilename;
                    _hecDSSFullFilename = value;

                    SetIsValid();
                    RecordPropertyChange(nameof(HECDSSFullFilename), old, value);
                }
            }
        }

        /// <summary>
        /// The HEC-DSS time series data pathname. 
        /// </summary>
        [Category("General")]
        [DisplayName("HEC-DSS Data Pathname")]
        [Description("The 6-part (A-F) time series data pathname written as: /A/B/C/D/E/F/")]
        [Browsable(true)]
        public string HECDSSDataPathname
        {
            get { return _hecDSSDataPathname; }
            set
            {
                if (_hecDSSDataPathname != value)
                {
                    var old = _hecDSSDataPathname;
                    _hecDSSDataPathname = value;

                    SetIsValid();
                    RecordPropertyChange(nameof(HECDSSDataPathname), old, value);
                }
            }
        }

        /// <summary>
        /// The GHCN site number.
        /// </summary>
        [Category("General")]
        [DisplayName("GHCN Site Number")]
        [Description("The Global Historical Climate Network (GHCN) station identification code. The site number is 11 digits long: the first 3 digits represent the country code, the next 5 digits represent the nearby WMO station, and the last 3 digits represent the station identifier.")]
        [Browsable(true)]
        public string GHCNSiteNumber
        {
            get { return _ghcnSiteNumber; }
            set
            {
                if (_ghcnSiteNumber != value)
                {
                    var old = _ghcnSiteNumber;
                    _ghcnSiteNumber = value;

                    _ghcnSiteNumberValid = true;
                    _messenger.Remove(_ghcnSiteNumberMsg);
                    if (_ghcnSiteNumber.Length != 11 && EntryMethod == TimeSeriesEntryMethod.GHCN)
                    {
                        _ghcnSiteNumberValid = false;
                        _messenger.Add(_ghcnSiteNumberMsg);
                    }

                    if (!UndoManager.IsExecutingAction)
                    {
                        if (_entryMethod != TimeSeriesEntryMethod.Manual)
                            TimeSeries = CreatePlaceholderTimeSeries(TimeInterval, StartDateTime);
                    }

                    SetIsValid();
                    RecordPropertyChange(nameof(GHCNSiteNumber), old, value);
                }
            }
        }

        /// <summary>
        /// The USGS gage site number.
        /// </summary>
        [Category("General")]
        [DisplayName("USGS Site Number")]
        [Description("The USGS surface water site number. The site number is 8 to 15 digits long.")]
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

                    _usgsSiteNumberValid = true;
                    _messenger.Remove(_usgsSiteNumberMsg);
                    if ((_usgsSiteNumber.Length < 8 || _usgsSiteNumber.Length > 15) && EntryMethod == TimeSeriesEntryMethod.USGS)
                    {
                        _usgsSiteNumberValid = false;
                        _messenger.Add(_usgsSiteNumberMsg);
                    }

                    if (!UndoManager.IsExecutingAction)
                    {
                        if (_entryMethod != TimeSeriesEntryMethod.Manual)
                            TimeSeries = CreatePlaceholderTimeSeries(TimeInterval, StartDateTime);
                    }

                    SetIsValid();
                    RecordPropertyChange(nameof(USGSSiteNumber), old, value);
                }
            }
        }

        /// <summary>
        /// The CHMN site number.
        /// </summary>
        [Category("General")]
        [DisplayName("CHMN Site Number")]
        [Description("The Canadian Hydrometric Monitoring Network (CHMN) site number. The site number is 7 characters long, where the first 2 digits represent the major drainage basin, the next 2 characters represent the sub-basin and sub-sub-basin, and the final 3 digits represent the station number.")]
        [Browsable(true)]
        public string CHMNSiteNumber
        {
            get { return _chmnSiteNumber; }
            set
            {
                if (_chmnSiteNumber != value)
                {
                    var old = _chmnSiteNumber;
                    _chmnSiteNumber = value;

                    _chmnSiteNumberValid = true;
                    _messenger.Remove(_chmnSiteNumberMsg);
                    if (_chmnSiteNumber.Length != 7 && EntryMethod == TimeSeriesEntryMethod.CHMN)
                    {
                        _chmnSiteNumberValid = false;
                        _messenger.Add(_chmnSiteNumberMsg);
                    }

                    if (!UndoManager.IsExecutingAction)
                    {
                        if (_entryMethod != TimeSeriesEntryMethod.Manual)
                            TimeSeries = CreatePlaceholderTimeSeries(TimeInterval, StartDateTime);
                    }

                    SetIsValid();
                    RecordPropertyChange(nameof(CHMNSiteNumber), old, value);
                }
            }
        }

        /// <summary>
        /// The ABOM site number.
        /// </summary>
        [Category("General")]
        [DisplayName("ABOM Site Number")]
        [Description("The Australian Bureau of Meteorology (ABOM) site number. The site number is 6 digits long: the first 4 digits represent the station code, and the last 2 digits represent the site identifier.")]
        [Browsable(true)]
        public string ABOMSiteNumber
        {
            get { return _abomSiteNumber; }
            set
            {
                if (_abomSiteNumber != value)
                {
                    var old = _abomSiteNumber;
                    _abomSiteNumber = value;

                    _abomSiteNumberValid = true;
                    _messenger.Remove(_abomSiteNumberMsg);
                    if (_abomSiteNumber.Length != 6 && EntryMethod == TimeSeriesEntryMethod.ABOM)
                    {
                        _abomSiteNumberValid = false;
                        _messenger.Add(_abomSiteNumberMsg);
                    }

                    if (!UndoManager.IsExecutingAction)
                    {
                        if (_entryMethod != TimeSeriesEntryMethod.Manual)
                            TimeSeries = CreatePlaceholderTimeSeries(TimeInterval, StartDateTime);
                    }

                    SetIsValid();
                    RecordPropertyChange(nameof(ABOMSiteNumber), old, value);
                }
            }
        }

        /// <summary>
        /// The depth unit.
        /// </summary>
        [Category("General")]
        [DisplayName("Depth Unit")]
        [Description("The depth unit for downloaded time series data.")]
        [Browsable(true)]
        public DepthUnit DepthUnit
        {
            get { return _depthUnit; }
            set
            {
                if (_depthUnit != value)
                {
                    var old = _depthUnit;
                    _depthUnit = value;

                    SetIsValid();
                    RecordPropertyChange(nameof(DepthUnit), old, value);
                }
            }
        }

        /// <summary>
        /// The discharge unit.
        /// </summary>
        [Category("General")]
        [DisplayName("Discharge Unit")]
        [Description("The discharge unit for downloaded time series data.")]
        [Browsable(true)]
        public DischargeUnit DischargeUnit
        {
            get { return _dischargeUnit; }
            set
            {
                if (_dischargeUnit != value)
                {
                    var old = _dischargeUnit;
                    _dischargeUnit = value;

                    SetIsValid();
                    RecordPropertyChange(nameof(DischargeUnit), old, value);
                }
            }
        }

        /// <summary>
        /// The height unit.
        /// </summary>
        [Category("General")]
        [DisplayName("Height Unit")]
        [Description("The height unit for downloaded time series data.")]
        [Browsable(true)]
        public HeightUnit HeightUnit
        {
            get { return _heightUnit; }
            set
            {
                if (_heightUnit != value)
                {
                    var old = _heightUnit;
                    _heightUnit = value;

                    SetIsValid();
                    RecordPropertyChange(nameof(HeightUnit), old, value);
                }
            }
        }

        /// <summary>
        /// Gets the USGS raw text file for annual peak data.
        /// </summary>
        public string USGSRawText
        {
            get
            {
                MaterializeUSGSRawText();
                return _usgsRawText;
            }
        }

        /// <summary>
        /// Gets the time series plot. Created once in the constructor; never replaced.
        /// </summary>
        public Plot TimeSeriesPlot => _timeSeriesPlot;

        /// <summary>
        /// Gets the seasonality plot. Created once in the constructor; never replaced.
        /// </summary>
        public Plot SeasonalityPlot => _seasonalityPlot;

        /// <summary>
        /// Gets the autocorrelation function (ACF) plot. Created once in the constructor; never replaced.
        /// </summary>
        public Plot ACFPlot => _acfPlot;

        /// <summary>
        /// Gets the partial autocorrelation function (PACF) plot. Created once in the constructor; never replaced.
        /// </summary>
        public Plot PACFPlot => _pacfPlot;

        /// <summary>
        /// Determines whether the current configuration uses peak seasonality (monthly frequency histogram)
        /// rather than the default monthly summary statistics (confidence interval plot).
        /// </summary>
        /// <remarks>
        /// Peak seasonality applies to any Peak Discharge or Peak Stage data regardless of entry method
        /// (USGS, CHMN, ABOM, Manual, etc.). Monthly frequency histograms are more meaningful than
        /// confidence interval bands for sparse annual peak records.
        /// </remarks>
        public bool IsPeakSeasonality
        {
            get
            {
                return _seriesType == TimeSeriesType.PeakDischarge || _seriesType == TimeSeriesType.PeakStage;
            }
        }

        #endregion

        #endregion

        #region IElement Methods

        /// <summary>
        /// Gets the required columns for the SQLite time series table.
        /// </summary>
        /// <remarks>
        /// If you want to add a new column, add it to the end of the dictionary to maintain
        /// backward compatibility with existing project files.
        /// </remarks>
        private static Dictionary<string, Type> RequiredColumns { get; } = new Dictionary<string, Type>() {
            { nameof(Name), typeof(string) },
            { nameof(Description), typeof(string) },
            { nameof(CreationDate), typeof(string) },
            { nameof(LastModified), typeof(string) },       
            { nameof(TimeSeries), typeof(string) },
            { nameof(UnitLabel), typeof(string) },
            { nameof(EntryMethod), typeof(string) },
            { nameof(SeriesType), typeof(string) },
            { nameof(HECDSSFullFilename), typeof(string) },
            { nameof(HECDSSDataPathname), typeof(string) },
            { nameof(GHCNSiteNumber), typeof(string) },
            { nameof(USGSSiteNumber), typeof(string) },
            { nameof(CHMNSiteNumber), typeof(string) },
            { nameof(ABOMSiteNumber), typeof(string) },
            { nameof(DepthUnit), typeof(string) },
            { nameof(DischargeUnit), typeof(string) },
            { nameof(HeightUnit), typeof(string) },
            { nameof(TimeInterval), typeof(string) },
            { nameof(StartDateTime), typeof(string) },
            { nameof(USGSRawText), typeof(string) },
            { "TimeSeriesPlotSettings", typeof(string) },
            { "SeasonalityPlotSettings", typeof(string) },
            { "ACFPlotSettings", typeof(string) },
            { "PACFPlotSettings", typeof(string) },
            { "TimeSeriesCompressed", typeof(byte[]) },
            { "USGSRawTextCompressed", typeof(byte[]) } };

        /// <summary>
        /// Creates or updates the SQLite time series table with required columns.
        /// </summary>
        /// <param name="sqlite">The SQLite database manager instance.</param>
        /// <remarks>
        /// This method ensures the time series table exists with all required columns. If the table doesn't exist,
        /// it creates it. If the table exists but is missing columns, it adds them. This supports forward
        /// compatibility when opening older project files.
        /// </remarks>
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
        /// Opens the time series element from disk using the provided SQLite manager.
        /// </summary>
        /// <param name="sqlite">The SQLite database manager instance.</param>
        /// <remarks>
        /// This method reads all time series properties from the database and deserializes the time series data.
        /// It also handles backward compatibility with legacy entry method enumerations.
        /// </remarks>
        public void Open(SQLiteManager sqlite)
        {
            // Suppress undo recording during deserialization to prevent plot PropertyChanged
            // events from polluting the undo stack. Mirrors constructor and Copy() patterns.
            var wasUndoEnabled = IsUndoEnabled;
            IsUndoEnabled = false;
            try
            {
                _messenger.Clear(this);
                var wasOpen = sqlite.DataBaseOpen;
                if (wasOpen == false) sqlite.Open();

                var dtView = sqlite.GetTableManager(ParentCollection.Name);
                int rowIndex = dtView.SearchColumn(0, dtView.NumberOfRows - 1, "Name", NameOnDisk, true, true);
                if (rowIndex != -1)
                {
                    // Use backing fields during deserialization to avoid repeated SetIsValid() calls.
                    // A single SetIsValid() call at the end of Open() is sufficient.
                    if (dtView.ColumnNames.Contains(nameof(Name)))
                    {
                        _name = dtView.GetCell(nameof(Name), rowIndex).ToString();
                        foreach (var item in _messages) item.SourceName = _name;
                        _nameValid = ValidateName(BestFitProject.InvalidNameCharacters, 50, "TS");
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
                    if (dtView.ColumnNames.Contains(nameof(UnitLabel)))
                    {
                        _unitLabel = dtView.GetCell(nameof(UnitLabel), rowIndex).ToString();
                        _unitLabelValid = !string.IsNullOrEmpty(_unitLabel);
                        if (!_unitLabelValid)
                            _messenger.Add(_unitLabelMsg);
                    }
                    if (dtView.ColumnNames.Contains(nameof(EntryMethod))) Enum.TryParse(dtView.GetCell(nameof(EntryMethod), rowIndex).ToString(), out _entryMethod);
                    if (dtView.ColumnNames.Contains(nameof(SeriesType))) Enum.TryParse(dtView.GetCell(nameof(SeriesType), rowIndex).ToString(), out _seriesType);

                    // Needed for backwards compatibility with v2.0_beta_1
                    if (_entryMethod == TimeSeriesEntryMethod.GHCNDailyPrecipitation)
                    {
                        _entryMethod = TimeSeriesEntryMethod.GHCN;
                    }
                    else if (_entryMethod == TimeSeriesEntryMethod.USGSDailyDischarge)
                    {
                        _entryMethod = TimeSeriesEntryMethod.USGS;
                    }
                    else if (_entryMethod == TimeSeriesEntryMethod.USGSDailyStage)
                    {
                        _entryMethod = TimeSeriesEntryMethod.USGS;
                        _seriesType = TimeSeriesType.DailyStage;
                    }

                    if (dtView.ColumnNames.Contains(nameof(HECDSSFullFilename))) _hecDSSFullFilename = dtView.GetCell(nameof(HECDSSFullFilename), rowIndex).ToString();
                    if (dtView.ColumnNames.Contains(nameof(HECDSSDataPathname))) _hecDSSDataPathname = dtView.GetCell(nameof(HECDSSDataPathname), rowIndex).ToString();
                    if (dtView.ColumnNames.Contains(nameof(GHCNSiteNumber)))
                    {
                        _ghcnSiteNumber = dtView.GetCell(nameof(GHCNSiteNumber), rowIndex).ToString();
                        _ghcnSiteNumberValid = _ghcnSiteNumber.Length == 11 || _entryMethod != TimeSeriesEntryMethod.GHCN;
                    }
                    if (dtView.ColumnNames.Contains(nameof(USGSSiteNumber)))
                    {
                        _usgsSiteNumber = dtView.GetCell(nameof(USGSSiteNumber), rowIndex).ToString();
                        _usgsSiteNumberValid = (_usgsSiteNumber.Length >= 8 && _usgsSiteNumber.Length <= 15) || _entryMethod != TimeSeriesEntryMethod.USGS;
                    }
                    if (dtView.ColumnNames.Contains(nameof(CHMNSiteNumber)))
                    {
                        _chmnSiteNumber = dtView.GetCell(nameof(CHMNSiteNumber), rowIndex).ToString();
                        _chmnSiteNumberValid = _chmnSiteNumber.Length == 7 || _entryMethod != TimeSeriesEntryMethod.CHMN;
                    }
                    if (dtView.ColumnNames.Contains(nameof(ABOMSiteNumber)))
                    {
                        _abomSiteNumber = dtView.GetCell(nameof(ABOMSiteNumber), rowIndex).ToString();
                        _abomSiteNumberValid = _abomSiteNumber.Length == 6 || _entryMethod != TimeSeriesEntryMethod.ABOM;
                    }
                    if (dtView.ColumnNames.Contains(nameof(DepthUnit))) Enum.TryParse(dtView.GetCell(nameof(DepthUnit), rowIndex).ToString(), out _depthUnit);
                    if (dtView.ColumnNames.Contains(nameof(DischargeUnit))) Enum.TryParse(dtView.GetCell(nameof(DischargeUnit), rowIndex).ToString(), out _dischargeUnit);
                    if (dtView.ColumnNames.Contains(nameof(HeightUnit))) Enum.TryParse(dtView.GetCell(nameof(HeightUnit), rowIndex).ToString(), out _heightUnit);
                    if (dtView.ColumnNames.Contains(nameof(TimeInterval))) Enum.TryParse(dtView.GetCell(nameof(TimeInterval), rowIndex).ToString(), out _timeInterval);
                    if (dtView.ColumnNames.Contains(nameof(StartDateTime)))
                    {
                        // Try to parse the invariant date string using TryParseExact
                        // If it fails, do a regular try parse.
                        if (!DateTime.TryParseExact(dtView.GetCell(nameof(StartDateTime), rowIndex).ToString(), "o", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _startDateTime))
                        {
                            DateTime.TryParse(dtView.GetCell(nameof(StartDateTime), rowIndex).ToString(), out _startDateTime);
                        }
                    }
                    // Retain compressed USGSRawText until a consumer requests the raw response.
                    // Full-period instantaneous responses expand to tens of megabytes and are not
                    // needed merely to populate the project tree.
                    _usgsRawText = "";
                    _usgsRawTextCompressed = Array.Empty<byte>();
                    _usgsRawTextLegacyFallback = "";
                    _usgsRawTextMaterialized = true;
                    if (dtView.ColumnNames.Contains("USGSRawTextCompressed"))
                    {
                        var cell = dtView.GetCell("USGSRawTextCompressed", rowIndex);
                        if (cell is byte[] compressedBytes && compressedBytes.Length > 0)
                        {
                            _usgsRawTextCompressed = compressedBytes;
                            _usgsRawTextMaterialized = false;
                        }
                    }
                    if (dtView.ColumnNames.Contains(nameof(USGSRawText)))
                    {
                        var legacyCell = dtView.GetCell(nameof(USGSRawText), rowIndex);
                        if (legacyCell != null && legacyCell != DBNull.Value)
                        {
                            string legacyText = legacyCell.ToString();
                            if (_usgsRawTextMaterialized)
                                _usgsRawText = legacyText;
                            else
                                _usgsRawTextLegacyFallback = legacyText;
                        }
                    }

                    // Deserialize plot settings into Plot objects, then refresh only axes
                    // that still carry their factory-default labels.
                    bool timeSeriesPlotRestored = DeserializePlotSettings(dtView, rowIndex, "TimeSeriesPlotSettings", _timeSeriesPlot);
                    bool seasonalityPlotRestored = DeserializePlotSettings(dtView, rowIndex, "SeasonalityPlotSettings", _seasonalityPlot);
                    DeserializePlotSettings(dtView, rowIndex, "ACFPlotSettings", _acfPlot);
                    DeserializePlotSettings(dtView, rowIndex, "PACFPlotSettings", _pacfPlot);
                    RefreshPlotAxisTitlesAfterOpen(timeSeriesPlotRestored, seasonalityPlotRestored);

                    // Get time series — stream the compressed UTF-8 payload directly so project
                    // opening does not allocate both a large managed string and an XElement tree.
                    TimeSeries loadedTimeSeries = null;
                    bool compressedPayloadFound = false;
                    if (dtView.ColumnNames.Contains("TimeSeriesCompressed"))
                    {
                        byte[] decompressedBytes = null;
                        try
                        {
                            var cell = dtView.GetCell("TimeSeriesCompressed", rowIndex);
                            if (cell is byte[] compressedBytes && compressedBytes.Length > 0)
                            {
                                decompressedBytes = Tools.Decompress(compressedBytes);
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Could not decompress TimeSeries: {ex.Message}");
                        }

                        if (decompressedBytes != null && decompressedBytes.Length > 0)
                        {
                            compressedPayloadFound = true;
                            try
                            {
                                loadedTimeSeries = DeserializeTimeSeries(decompressedBytes);
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Could not deserialize compressed TimeSeries: {ex.Message}");
                            }
                        }
                    }
                    if (!compressedPayloadFound && dtView.ColumnNames.Contains(nameof(TimeSeries)))
                    {
                        var legacyCell = dtView.GetCell(nameof(TimeSeries), rowIndex);
                        if (legacyCell != null && legacyCell != DBNull.Value)
                        {
                            string legacyXml = legacyCell.ToString();
                            if (!string.IsNullOrEmpty(legacyXml))
                            {
                                try
                                {
                                    loadedTimeSeries = DeserializeTimeSeries(legacyXml);
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"Could not deserialize legacy TimeSeries: {ex.Message}");
                                }
                            }
                        }
                    }

                    if (loadedTimeSeries != null)
                    {
                        // Install complete per-ordinate tracking once the collection is fully populated.
                        DetachTimeSeriesEventHandlers(_timeSeries);
                        _timeSeries = loadedTimeSeries;
                        AttachTimeSeriesEventHandlers(_timeSeries);
                    }
                }
                else
                {
                    // Row not found by Name in the parent collection table � usually a deleted row
                    // or a mismatch between disk state and the live ElementList. Surface the
                    // condition rather than silently leaving the element with constructor defaults.
                    System.Diagnostics.Debug.WriteLine($"TimeSeriesElement.Open: no row matched NameOnDisk='{NameOnDisk}' in '{ParentCollection.Name}'; element loaded with constructor defaults.");
                }

                if (wasOpen == false) sqlite.Close();

            if (!_deferBridgeSetup) SetupBridges();
            SetIsValid();
            SetIsDirty(false);
            RaisePropertyChange(nameof(Name), setDirty: false);  // Notify node header binding that Name was restored from disk
            }
            finally
            {
                IsUndoEnabled = wasUndoEnabled;
                if (wasUndoEnabled) ClearUndoHistory();
            }
        }

        /// <summary>
        /// Raises the preview saved event before saving the time series element.
        /// </summary>
        /// <param name="cancel">A reference parameter that can be set to true to cancel the save operation.</param>
        /// <remarks>
        /// This method allows external handlers to perform operations before save or cancel the save operation.
        /// </remarks>
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
                dtView.EditCell(rowIndex, nameof(TimeSeries), "");  // Clear legacy TEXT column
                dtView.EditCell(rowIndex, "TimeSeriesCompressed",
                    TimeSeries == null ? Array.Empty<byte>() : Tools.Compress(Encoding.UTF8.GetBytes(TimeSeries.ToXElement().ToString())));
                dtView.EditCell(rowIndex, nameof(UnitLabel), UnitLabel);
                dtView.EditCell(rowIndex, nameof(EntryMethod), EntryMethod);
                dtView.EditCell(rowIndex, nameof(SeriesType), SeriesType);
                dtView.EditCell(rowIndex, nameof(HECDSSFullFilename), HECDSSFullFilename);
                dtView.EditCell(rowIndex, nameof(HECDSSDataPathname), HECDSSDataPathname);
                dtView.EditCell(rowIndex, nameof(GHCNSiteNumber), GHCNSiteNumber);
                dtView.EditCell(rowIndex, nameof(USGSSiteNumber), USGSSiteNumber);
                dtView.EditCell(rowIndex, nameof(CHMNSiteNumber), CHMNSiteNumber);
                dtView.EditCell(rowIndex, nameof(ABOMSiteNumber), ABOMSiteNumber);
                dtView.EditCell(rowIndex, nameof(DepthUnit), DepthUnit);
                dtView.EditCell(rowIndex, nameof(DischargeUnit), DischargeUnit);
                dtView.EditCell(rowIndex, nameof(HeightUnit), HeightUnit);
                dtView.EditCell(rowIndex, nameof(TimeInterval), TimeInterval);
                dtView.EditCell(rowIndex, nameof(StartDateTime), StartDateTime.ToString("o", CultureInfo.InvariantCulture));
                dtView.EditCell(rowIndex, nameof(USGSRawText), "");  // Clear legacy TEXT column
                dtView.EditCell(rowIndex, "USGSRawTextCompressed", GetUSGSRawTextCompressedForSave());
                dtView.EditCell(rowIndex, "TimeSeriesPlotSettings", _timeSeriesPlot != null ? PlotSerializer.ToXElement(_timeSeriesPlot).ToString() : "");
                dtView.EditCell(rowIndex, "SeasonalityPlotSettings", _seasonalityPlot != null ? PlotSerializer.ToXElement(_seasonalityPlot).ToString() : "");
                dtView.EditCell(rowIndex, "ACFPlotSettings", _acfPlot != null ? PlotSerializer.ToXElement(_acfPlot).ToString() : "");
                dtView.EditCell(rowIndex, "PACFPlotSettings", _pacfPlot != null ? PlotSerializer.ToXElement(_pacfPlot).ToString() : "");

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
            var element = new TimeSeriesElement(newName == "" || string.IsNullOrEmpty(newName) ? Name : newName, ParentCollection);
            element.IsUndoEnabled = false;
            element._deferBridgeSetup = true;
            try
            {
                // Use backing fields to avoid repeated SetIsValid() calls during Copy.
                // A single SetIsValid() runs via the constructor's finally block.
                element._description = Description;
                element._unitLabel = UnitLabel;
                element._unitLabelValid = !string.IsNullOrEmpty(UnitLabel);
                element._entryMethod = EntryMethod;
                element._seriesType = SeriesType;
                element._hecDSSFullFilename = HECDSSFullFilename;
                element._hecDSSDataPathname = HECDSSDataPathname;
                element._ghcnSiteNumber = GHCNSiteNumber;
                element._ghcnSiteNumberValid = GHCNSiteNumber.Length == 11 || EntryMethod != TimeSeriesEntryMethod.GHCN;
                element._usgsSiteNumber = USGSSiteNumber;
                element._usgsSiteNumberValid = (USGSSiteNumber.Length >= 8 && USGSSiteNumber.Length <= 15) || EntryMethod != TimeSeriesEntryMethod.USGS;
                element._chmnSiteNumber = CHMNSiteNumber;
                element._chmnSiteNumberValid = CHMNSiteNumber.Length == 7 || EntryMethod != TimeSeriesEntryMethod.CHMN;
                element._abomSiteNumber = ABOMSiteNumber;
                element._abomSiteNumberValid = ABOMSiteNumber.Length == 6 || EntryMethod != TimeSeriesEntryMethod.ABOM;
                element._depthUnit = DepthUnit;
                element._dischargeUnit = DischargeUnit;
                element._heightUnit = HeightUnit;
                element._timeInterval = TimeInterval;
                element._startDateTime = StartDateTime;
                element._usgsRawText = _usgsRawText;
                element._usgsRawTextCompressed = _usgsRawTextCompressed.ToArray();
                element._usgsRawTextLegacyFallback = _usgsRawTextLegacyFallback;
                element._usgsRawTextMaterialized = _usgsRawTextMaterialized;

                // Copy plot settings via serialize/deserialize round-trip
                if (_timeSeriesPlot != null)
                    PlotSerializer.FromXElement(element._timeSeriesPlot, PlotSerializer.ToXElement(_timeSeriesPlot));
                if (_seasonalityPlot != null)
                    PlotSerializer.FromXElement(element._seasonalityPlot, PlotSerializer.ToXElement(_seasonalityPlot));
                if (_acfPlot != null)
                    PlotSerializer.FromXElement(element._acfPlot, PlotSerializer.ToXElement(_acfPlot));
                if (_pacfPlot != null)
                    PlotSerializer.FromXElement(element._pacfPlot, PlotSerializer.ToXElement(_pacfPlot));

                // Refresh only copied axes that still carry their factory-default labels.
                element.UpdatePlotYAxisTitles();

                element.TimeSeries = TimeSeries.Clone();
            }
            finally
            {
                element._deferBridgeSetup = false;
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
            var element = new TimeSeriesElement(itemName, ParentCollection);
            element.Open(sqlite);
            return element;
        }

        /// <summary>
        /// Deletes the element from disk and from the parent element collection. 
        /// </summary>
        public override void Delete()
        {
            // Early-return on missing Name BEFORE bridge teardown � otherwise an element
            // constructed with a null/empty name (rare but possible during partial init)
            // would have its bridges torn down without writing to disk, leaving a
            // half-deleted state. Mirrors InputData.Delete's order.
            if (string.IsNullOrEmpty(Name)) return;
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
            RaiseDeleted(this);
        }

        /// <summary>
        /// Validates the time series element and updates the IsValid property.
        /// </summary>
        /// <remarks>
        /// This method checks all validation rules including name validity, unit label, time series data count,
        /// missing data, HEC-DSS file and pathname validity, and site number validity based on the entry method.
        /// It should be called whenever a property that affects validity is changed.
        /// </remarks>
        public void SetIsValid()
        {
            bool valid = true;
            if (_nameValid == false) valid = false;
            if (_unitLabelValid == false) valid = false;

            InspectTimeSeries(_timeSeries, out bool hasMissingValues, out bool hasOutOfOrderDateTimes);

            _timeSeriesValid = true;
            _messenger.Remove(_missingDataMsg);
            if (hasMissingValues)
                _messenger.Add(_missingDataMsg);

            _messenger.Remove(_timeSeriesDataMsg);
            if (_timeSeries != null && _timeSeries.Count <= 2)
            {
                _timeSeriesValid = false;
                _messenger.Add(_timeSeriesDataMsg);
            }
            _messenger.Remove(_timeSeriesOrderMsg);
            if (hasOutOfOrderDateTimes)
            {
                _timeSeriesValid = false;
                _messenger.Add(_timeSeriesOrderMsg);
            }
            if (_timeSeriesValid == false) valid = false;


            // Check HEC-DSS 
            _messenger.Remove(_hecDSSFilenameMsg);
            _messenger.Remove(_hecDSSPathnameMsg);
            if (EntryMethod == TimeSeriesEntryMethod.HECDSS)
            {
                // Check if file name extension is correct
                if (Path.GetExtension(HECDSSFullFilename) != ".dss")
                {
                    _messenger.Add(_hecDSSFilenameMsg);
                    valid = false;
                }
                // Check if pathname is valid
                var split = HECDSSDataPathname.Split('/');
                if (split.Length != 8)
                {
                    _messenger.Add(_hecDSSPathnameMsg);
                    valid = false;
                }
            }

            // Check site numbers
            if (EntryMethod == TimeSeriesEntryMethod.GHCN && _ghcnSiteNumberValid == false)
                valid = false;

            if (EntryMethod == TimeSeriesEntryMethod.USGS && _usgsSiteNumberValid == false)
                valid = false;

            if (EntryMethod == TimeSeriesEntryMethod.CHMN && _chmnSiteNumberValid == false)
                valid = false;

            if (EntryMethod == TimeSeriesEntryMethod.ABOM && _abomSiteNumberValid == false)
                valid = false;

            if (valid != _isValid)
            {
                _isValid = valid;
                RaisePropertyChange(nameof(IsValid));
            }
        }

        /// <summary>
        /// Inspects missing values and irregular date ordering in one traversal.
        /// </summary>
        /// <param name="timeSeries">The time series to inspect.</param>
        /// <param name="hasMissingValues">Returns whether any ordinate value is <see cref="double.NaN"/>.</param>
        /// <param name="hasOutOfOrderDateTimes">Returns whether an irregular series contains non-ascending date-times.</param>
        /// <remarks>
        /// Grid sorting is a view-only operation, while plots read the backing collection order. The
        /// traversal stops as soon as every applicable condition has been resolved.
        /// </remarks>
        private static void InspectTimeSeries(
            TimeSeries timeSeries,
            out bool hasMissingValues,
            out bool hasOutOfOrderDateTimes)
        {
            hasMissingValues = false;
            hasOutOfOrderDateTimes = false;
            if (timeSeries == null) return;

            bool inspectOrder = timeSeries.TimeInterval == TimeInterval.Irregular;
            for (int i = 0; i < timeSeries.Count; i++)
            {
                if (double.IsNaN(timeSeries[i].Value))
                {
                    hasMissingValues = true;
                }

                if (inspectOrder && i > 0 && timeSeries[i].Index <= timeSeries[i - 1].Index)
                {
                    hasOutOfOrderDateTimes = true;
                }

                if (hasMissingValues && (!inspectOrder || hasOutOfOrderDateTimes)) return;
            }
        }

        #endregion

        #region Data Methods

        /// <summary>
        /// Deserializes a time series from UTF-8 XML bytes without creating an intermediate string or DOM.
        /// </summary>
        /// <param name="xmlBytes">The decompressed XML payload.</param>
        /// <returns>The fully populated time series.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="xmlBytes"/> is null.</exception>
        /// <exception cref="XmlException">Thrown when the payload is not well-formed XML.</exception>
        /// <remarks>
        /// No element-level event handlers or undo bridges can observe this private collection until the
        /// caller assigns the returned instance. This preserves bulk-load notification semantics.
        /// </remarks>
        private static TimeSeries DeserializeTimeSeries(byte[] xmlBytes)
        {
            if (xmlBytes == null) throw new ArgumentNullException(nameof(xmlBytes));

            using var stream = new MemoryStream(xmlBytes, writable: false);
            using var reader = XmlReader.Create(stream, CreateTimeSeriesXmlReaderSettings());
            return DeserializeTimeSeries(reader);
        }

        /// <summary>
        /// Deserializes a legacy time-series XML string through the shared streaming reader.
        /// </summary>
        /// <param name="xml">The legacy XML payload.</param>
        /// <returns>The fully populated time series.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="xml"/> is null.</exception>
        /// <exception cref="XmlException">Thrown when the payload is not well-formed XML.</exception>
        /// <remarks>
        /// The string overload maintains compatibility with legacy TEXT columns while sharing the exact
        /// ordinate parsing logic used by compressed BLOB storage.
        /// </remarks>
        private static TimeSeries DeserializeTimeSeries(string xml)
        {
            if (xml == null) throw new ArgumentNullException(nameof(xml));

            using var textReader = new StringReader(xml);
            using var reader = XmlReader.Create(textReader, CreateTimeSeriesXmlReaderSettings());
            return DeserializeTimeSeries(reader);
        }

        /// <summary>
        /// Creates the secure, whitespace-tolerant reader settings used for persisted time-series XML.
        /// </summary>
        /// <returns>Settings that prohibit external document resolution and DTD processing.</returns>
        /// <remarks>
        /// Persisted time-series XML never requires external entities. Prohibiting them also matches
        /// the safe defaults used by modern LINQ-to-XML parsing.
        /// </remarks>
        private static XmlReaderSettings CreateTimeSeriesXmlReaderSettings()
        {
            return new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                IgnoreComments = true,
                IgnoreWhitespace = true,
                XmlResolver = null,
            };
        }

        /// <summary>
        /// Reads persisted time-series metadata and ordinates from a forward-only XML reader.
        /// </summary>
        /// <param name="reader">The reader positioned before or on the document element.</param>
        /// <returns>The fully populated time series.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="reader"/> is null.</exception>
        /// <exception cref="XmlException">Thrown when the document contains malformed XML.</exception>
        /// <remarks>
        /// Attribute parsing intentionally mirrors <c>Numerics.Data.TimeSeries(XElement)</c>: ISO round-trip
        /// dates are preferred with invariant fallbacks, numeric values use invariant <see cref="NumberStyles.Any"/>,
        /// and missing attributes retain the original default date and NaN value.
        /// </remarks>
        private static TimeSeries DeserializeTimeSeries(XmlReader reader)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));

            reader.MoveToContent();
            int rootDepth = reader.Depth;
            TimeInterval interval = TimeInterval.OneDay;
            string intervalText = reader.GetAttribute(nameof(TimeInterval));
            if (intervalText != null) Enum.TryParse(intervalText, out interval);

            var timeSeries = new TimeSeries(interval);
            if (reader.IsEmptyElement) return timeSeries;

            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.EndElement && reader.Depth == rootDepth) break;
                if (reader.NodeType != XmlNodeType.Element ||
                    reader.Depth != rootDepth + 1 ||
                    reader.Name != "SeriesOrdinate")
                    continue;

                DateTime index = default;
                double value = double.NaN;
                string indexText = reader.GetAttribute("Index");
                string valueText = reader.GetAttribute("Value");
                if (indexText != null &&
                    !DateTime.TryParseExact(indexText, "o", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out index))
                {
                    DateTime.TryParse(indexText, CultureInfo.InvariantCulture, DateTimeStyles.None, out index);
                }
                if (valueText != null)
                    double.TryParse(valueText, NumberStyles.Any, CultureInfo.InvariantCulture, out value);

                timeSeries.Add(new SeriesOrdinate<DateTime, double>(index, value));
            }

            return timeSeries;
        }

        /// <summary>
        /// Expands the persisted USGS response the first time a consumer requests it.
        /// </summary>
        /// <remarks>
        /// Saved instantaneous responses can expand to tens of megabytes. Deferring this work avoids
        /// allocating those strings during project-tree construction while preserving the public
        /// <see cref="USGSRawText"/> behavior. A legacy TEXT value remains available as a fallback if
        /// a compressed value cannot be decoded.
        /// </remarks>
        private void MaterializeUSGSRawText()
        {
            if (_usgsRawTextMaterialized) return;

            try
            {
                _usgsRawText = _usgsRawTextCompressed.Length == 0
                    ? _usgsRawTextLegacyFallback
                    : Encoding.UTF8.GetString(Tools.Decompress(_usgsRawTextCompressed));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Could not decompress USGSRawText: {ex.Message}");
                _usgsRawText = _usgsRawTextLegacyFallback;
            }
            finally
            {
                _usgsRawTextLegacyFallback = "";
                _usgsRawTextMaterialized = true;
            }
        }

        /// <summary>
        /// Returns the compressed USGS response for persistence without forcing lazy materialization.
        /// </summary>
        /// <returns>The existing compressed payload or a compressed representation of the materialized text.</returns>
        /// <remarks>
        /// Reusing the stored payload makes a metadata-only save O(1) with respect to raw response size.
        /// Downloaded or legacy text is compressed when it has no reusable persisted payload.
        /// </remarks>
        private byte[] GetUSGSRawTextCompressedForSave()
        {
            // Transitional files can contain both compressed and legacy text. Materialize those
            // rare rows so a corrupt compressed payload is repaired from its legacy fallback.
            if (!_usgsRawTextMaterialized && !string.IsNullOrEmpty(_usgsRawTextLegacyFallback))
                MaterializeUSGSRawText();

            if (!_usgsRawTextMaterialized && _usgsRawTextCompressed.Length > 0)
                return _usgsRawTextCompressed;

            return string.IsNullOrEmpty(_usgsRawText)
                ? Array.Empty<byte>()
                : Tools.Compress(Encoding.UTF8.GetBytes(_usgsRawText));
        }

        /// <summary>
        /// Replaces the retained USGS response with newly downloaded text.
        /// </summary>
        /// <param name="rawText">The raw response returned by the USGS service.</param>
        /// <remarks>
        /// A download already owns the expanded text, so any persisted compressed payload and legacy
        /// fallback are discarded. Subsequent saves compress the new response once.
        /// </remarks>
        private void SetUSGSRawText(string rawText)
        {
            _usgsRawText = rawText ?? "";
            _usgsRawTextCompressed = Array.Empty<byte>();
            _usgsRawTextLegacyFallback = "";
            _usgsRawTextMaterialized = true;
        }

        /// <summary>
        /// Creates a placeholder time series for reset and initialization paths.
        /// </summary>
        /// <param name="timeInterval">The interval to assign to the placeholder series.</param>
        /// <param name="startDateTime">The date-time for the placeholder ordinate.</param>
        /// <returns>A one-ordinate placeholder time series using the requested interval.</returns>
        /// <remarks>
        /// Regular intervals use the Numerics regular-grid constructor. Irregular intervals cannot
        /// use that constructor, so this method creates an irregular collection and inserts one NaN
        /// ordinate at the current start date-time.
        /// </remarks>
        private static TimeSeries CreatePlaceholderTimeSeries(TimeInterval timeInterval, DateTime startDateTime)
        {
            if (timeInterval != TimeInterval.Irregular)
            {
                return new TimeSeries(timeInterval, startDateTime, startDateTime);
            }

            var timeSeries = new TimeSeries(TimeInterval.Irregular);
            timeSeries.Add(new SeriesOrdinate<DateTime, double>(startDateTime, double.NaN));
            return timeSeries;
        }

        /// <summary>
        /// Assigns a downloaded time series while synchronizing element metadata first.
        /// </summary>
        /// <param name="downloadedTimeSeries">The time series returned by an external source.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="downloadedTimeSeries"/> is null.</exception>
        /// <remarks>
        /// The backing interval and start date-time are updated before raising the
        /// <see cref="TimeSeries"/> property change so bound controls do not briefly observe a
        /// downloaded irregular series with stale regular-series metadata.
        /// </remarks>
        private void AssignDownloadedTimeSeries(TimeSeries downloadedTimeSeries)
        {
            if (downloadedTimeSeries == null) throw new ArgumentNullException(nameof(downloadedTimeSeries));

            TimeInterval oldInterval = _timeInterval;
            DateTime oldStartDateTime = _startDateTime;

            _timeInterval = downloadedTimeSeries.TimeInterval;
            _startDateTime = downloadedTimeSeries.Count > 0 ? downloadedTimeSeries.StartDate : _startDateTime;
            TimeSeries = downloadedTimeSeries;

            if (oldInterval != _timeInterval) RaisePropertyChange(nameof(TimeInterval));
            if (oldStartDateTime != _startDateTime) RaisePropertyChange(nameof(StartDateTime));
        }

        /// <summary>
        /// Detaches collection and ordinate event handlers from a time series.
        /// </summary>
        /// <param name="timeSeries">The time series to detach from.</param>
        /// <remarks>
        /// Every ordinate receives a property-change handler so row edits remain visible to
        /// validation and undo tracking.
        /// </remarks>
        private void DetachTimeSeriesEventHandlers(TimeSeries timeSeries)
        {
            if (timeSeries == null) return;

            timeSeries.CollectionChanged -= TimeSeriesCollectionChanged;
            for (int i = 0; i < timeSeries.Count; i++)
            {
                timeSeries[i].PropertyChanged -= SeriesOrdinateChanged;
            }
        }

        /// <summary>
        /// Attaches collection and ordinate event handlers to a time series.
        /// </summary>
        /// <param name="timeSeries">The time series to attach to.</param>
        /// <remarks>
        /// Every ordinate receives a property-change handler so row edits remain visible to
        /// validation and undo tracking.
        /// </remarks>
        private void AttachTimeSeriesEventHandlers(TimeSeries timeSeries)
        {
            if (timeSeries == null) return;

            timeSeries.CollectionChanged += TimeSeriesCollectionChanged;

            for (int i = 0; i < timeSeries.Count; i++)
            {
                timeSeries[i].PropertyChanged += SeriesOrdinateChanged;
            }
        }

        /// <summary>
        /// Handles collection changed events from the time series data collection.
        /// </summary>
        /// <param name="sender">The source of the event, typically the TimeSeries collection.</param>
        /// <param name="e">The NotifyCollectionChangedEventArgs containing information about the collection change.</param>
        /// <remarks>
        /// This method manages property change event subscriptions for series ordinates as they are added,
        /// removed, or replaced in the collection. It ensures proper event handling and validity checking.
        /// </remarks>
        private void TimeSeriesCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // Data was added or inserted
            if (e.Action == NotifyCollectionChangedAction.Add || e.Action == NotifyCollectionChangedAction.Replace)
            {
                for (int i = 0; i < e.NewItems.Count; i++)
                {
                    ((SeriesOrdinate<DateTime, double>)e.NewItems[i]).PropertyChanged += SeriesOrdinateChanged;
                }
            }
            // Data was removed
            if (e.Action == NotifyCollectionChangedAction.Remove || e.Action == NotifyCollectionChangedAction.Replace)
            {
                for (int i = 0; i < e.OldItems.Count; i++)
                {
                    ((SeriesOrdinate<DateTime, double>)e.OldItems[i]).PropertyChanged -= SeriesOrdinateChanged;
                }
            }
            // Collection was reset
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                for (int i = 0; i < TimeSeries.Count; i++)
                {
                    TimeSeries[i].PropertyChanged -= SeriesOrdinateChanged;
                    TimeSeries[i].PropertyChanged += SeriesOrdinateChanged;
                }
            }
            //
            if (TimeSeries.SuppressCollectionChanged == false)
            {
                RaisePropertyChange("TimeSeriesCollection");
                SetIsValid();
            }
        }

        /// <summary>
        /// Handles property change events from individual series ordinates in the time series.
        /// </summary>
        /// <param name="sender">The source of the event, typically a SeriesOrdinate instance.</param>
        /// <param name="e">The PropertyChangedEventArgs containing the name of the changed property.</param>
        /// <remarks>
        /// This method propagates property changes from individual data points to the time series element
        /// and triggers validation when changes occur.
        /// </remarks>
        private void SeriesOrdinateChanged(object sender, PropertyChangedEventArgs e)
        {
            if (TimeSeries.SuppressCollectionChanged == false)
            {
                RaisePropertyChange(e.PropertyName);
                // Filter validation to property changes that actually affect validity. Index/Value
                // changes affect series ordering / sentinel detection; other notifications (if any
                // are added later) would need the same audit before being whitelisted.
                if (e.PropertyName == nameof(SeriesOrdinate<DateTime, double>.Index) ||
                    e.PropertyName == nameof(SeriesOrdinate<DateTime, double>.Value))
                    SetIsValid();
            }
        }

        /// <summary>
        /// Downloads time series data from the specified external source based on the entry method.
        /// </summary>
        /// <param name="cancellationToken">Token used to cancel HTTP-based downloads.</param>
        /// <returns>A task representing the asynchronous download operation.</returns>
        /// <remarks>
        /// This method supports downloading data from HEC-DSS files, GHCN, USGS, CHMN, and ABOM sources.
        /// The downloaded data populates the TimeSeries property. If an error occurs during download,
        /// the time series is reset and the exception is re-thrown.
        /// </remarks>
        /// <exception cref="System.IO.IOException">Thrown when reading the source file or stream fails.</exception>
        /// <exception cref="System.Net.Http.HttpRequestException">Thrown when an HTTP-based source (GHCN/USGS/CHMN/ABOM) fails to respond.</exception>
        /// <exception cref="System.InvalidOperationException">Thrown when the source data is malformed or no time series can be constructed.</exception>
        public async Task Download(CancellationToken cancellationToken = default)
        {
            if (EntryMethod == TimeSeriesEntryMethod.Manual) return;
            cancellationToken.ThrowIfCancellationRequested();

            // Suppress undo recording during download so that intermediate property changes
            // (TimeInterval, TimeSeries replacement, plot updates) don't each record separate
            // undo entries. The caller (ProcessDataButton_Click) is responsible for ensuring
            // the overall download action is undoable if desired.
            bool wasUndoEnabled = IsUndoEnabled;
            IsUndoEnabled = false;
            try
            {
                if (EntryMethod == TimeSeriesEntryMethod.HECDSS)
                {
                    if (!File.Exists(HECDSSFullFilename))
                    {
                        throw new Exception("The selected DSS file does not exist.");
                    }

                    Hec.Dss.DssReader dssReader;
                    try
                    {
                        dssReader = new Hec.Dss.DssReader(HECDSSFullFilename);
                    }
                    catch (DllNotFoundException ex)
                    {
                        throw new Exception(
                            "HEC-DSS native library 'hecdss.dll' was not found. " +
                            "Verify the application installation.", ex);
                    }
                    catch (BadImageFormatException ex)
                    {
                        throw new Exception(
                            "HEC-DSS native library architecture mismatch (x64 required).", ex);
                    }
                    catch (IOException ex)
                    {
                        throw new Exception(
                            $"Cannot open DSS file '{HECDSSFullFilename}'. " +
                            "It may be locked by HEC-DSSVue or another process.", ex);
                    }

                    using (dssReader)
                    {
                        Hec.Dss.DssPath pathName;
                        try
                        {
                            pathName = new Hec.Dss.DssPath(HECDSSDataPathname);
                        }
                        catch (Exception ex)
                        {
                            throw new Exception($"Invalid DSS pathname '{HECDSSDataPathname}'. " +
                                $"Use the path selector to choose a valid path. Details: {ex.Message}");
                        }

                        // If the D-part contains a date range (condensed path from catalog) or spaces
                        // (from a legacy spacing bug), use PathWithoutDate so GetTimeSeries auto-discovers
                        // the full date range from the native library.
                        if (pathName.IsDPartARange() || pathName.Dpart.Contains(" "))
                        {
                            pathName = new Hec.Dss.DssPath(pathName.PathWithoutDate);
                        }

                        var timeSeries = dssReader.GetTimeSeries(pathName);

                        // Check if the time series data was found
                        if (timeSeries == null || timeSeries.Values == null || timeSeries.Values.Length == 0)
                        {
                            throw new Exception("No data found for the specified DSS path.");
                        }
                        else
                        {
                            var interval = ResolveDssTimeInterval(timeSeries);
                            AssignDownloadedTimeSeries(BuildFromDssTimeSeries(timeSeries, interval));
                            if (!string.IsNullOrWhiteSpace(timeSeries.Units))
                            {
                                UnitLabel = timeSeries.Units.Trim();
                            }
                        }
                    }
                }
                else if (EntryMethod == TimeSeriesEntryMethod.GHCN)
                {
                    AssignDownloadedTimeSeries(await TimeSeriesDownload.FromGHCN(GHCNSiteNumber, SeriesType, DepthUnit, cancellationToken));
                }
                else if (EntryMethod == TimeSeriesEntryMethod.USGS)
                {
                    var result = await TimeSeriesDownload.FromUSGS(USGSSiteNumber, SeriesType, cancellationToken);
                    SetUSGSRawText(result.RawText);      // Set BEFORE TimeSeries so PropertyChanged handlers read correct value
                    AssignDownloadedTimeSeries(result.TimeSeries);
                }
                else if (EntryMethod == TimeSeriesEntryMethod.CHMN)
                {
                    AssignDownloadedTimeSeries(await TimeSeriesDownload.FromCHMN(CHMNSiteNumber, SeriesType, cancellationToken: cancellationToken));
                }
                else if (EntryMethod == TimeSeriesEntryMethod.ABOM)
                {
                    AssignDownloadedTimeSeries(await TimeSeriesDownload.FromABOM(ABOMSiteNumber, SeriesType, cancellationToken: cancellationToken));
                }

                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
                TimeSeries = CreatePlaceholderTimeSeries(TimeInterval, StartDateTime);
                throw;
            }
            finally
            {
                IsUndoEnabled = wasUndoEnabled;
            }
        }

        /// <summary>
        /// Resolves the RMC-BestFit time interval represented by a DSS time-series E-part.
        /// </summary>
        /// <param name="dssTs">The DSS time series returned by <c>DssReader.GetTimeSeries</c>.</param>
        /// <returns>The corresponding <see cref="TimeInterval"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="dssTs"/> is null.</exception>
        /// <exception cref="Exception">Thrown when the DSS path metadata is missing or the regular interval is unsupported.</exception>
        /// <remarks>
        /// DSS marks irregular time series with E-parts that start with <c>IR-</c> or <c>~</c>.
        /// Those records must remain irregular even when the returned timestamps happen to be
        /// evenly spaced. Regular records are mapped only when the interval is explicitly
        /// supported by <see cref="TimeInterval"/>.
        /// </remarks>
        internal static TimeInterval ResolveDssTimeInterval(Hec.Dss.TimeSeries dssTs)
        {
            if (dssTs == null) throw new ArgumentNullException(nameof(dssTs));

            string ePart = dssTs.Path?.Epart?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(ePart))
            {
                throw new Exception("The DSS time series does not include an E-part interval.");
            }

            if (ePart.StartsWith("IR-", StringComparison.OrdinalIgnoreCase) ||
                ePart.StartsWith("~", StringComparison.OrdinalIgnoreCase))
            {
                return TimeInterval.Irregular;
            }

            string intervalToken = NormalizeDssIntervalToken(ePart);
            switch (intervalToken)
            {
                case "1MIN":
                case "1MINUTE":
                    return TimeInterval.OneMinute;
                case "5MIN":
                case "5MINUTE":
                    return TimeInterval.FiveMinute;
                case "15MIN":
                case "15MINUTE":
                    return TimeInterval.FifteenMinute;
                case "30MIN":
                case "30MINUTE":
                    return TimeInterval.ThirtyMinute;
                case "1HOUR":
                    return TimeInterval.OneHour;
                case "6HOUR":
                    return TimeInterval.SixHour;
                case "12HOUR":
                    return TimeInterval.TwelveHour;
                case "1DAY":
                    return TimeInterval.OneDay;
                case "1WEEK":
                    return TimeInterval.SevenDay;
                case "1MON":
                case "1MONTH":
                    return TimeInterval.OneMonth;
                case "3MON":
                case "3MONTH":
                case "1QUARTER":
                    return TimeInterval.OneQuarter;
                case "1YEAR":
                    return TimeInterval.OneYear;
                default:
                    throw new Exception(
                        $"The DSS time series interval '{ePart}' is not supported in RMC-BestFit. " +
                        "Supported DSS intervals are 1Minute, 5Minute, 15Minute, 30Minute, " +
                        "1Hour, 6Hour, 12Hour, 1Day, 1Week, 1Month, 3Month/1Quarter, 1Year, " +
                        "and irregular IR-* or ~* records.");
            }
        }

        /// <summary>
        /// Normalizes a DSS E-part interval string for case-insensitive alias matching.
        /// </summary>
        /// <param name="ePart">The DSS E-part interval text.</param>
        /// <returns>A compact upper-case token with separators removed.</returns>
        /// <remarks>
        /// DSS v6 and v7 use different spellings for the same intervals. Removing separators
        /// lets the resolver treat forms such as <c>1MIN</c> and <c>1Minute</c> consistently.
        /// </remarks>
        private static string NormalizeDssIntervalToken(string ePart)
        {
            return ePart.Trim()
                .Replace("-", "", StringComparison.Ordinal)
                .Replace("_", "", StringComparison.Ordinal)
                .Replace(" ", "", StringComparison.Ordinal)
                .ToUpperInvariant();
        }

        /// <summary>
        /// Determines whether a DSS value should define the imported record extent.
        /// </summary>
        /// <param name="value">The raw DSS value.</param>
        /// <returns><c>true</c> when the value is a real observation; otherwise, <c>false</c>.</returns>
        /// <remarks>
        /// <see cref="Hec.Dss.DssReader.IsValid(double)"/> recognizes the DSS sentinel constants.
        /// This wrapper also treats <see cref="double.NaN"/> as missing so synthetic tests and
        /// future readers do not accidentally use NaN as a valid extent marker.
        /// </remarks>
        private static bool IsValidDssValue(double value)
        {
            return !double.IsNaN(value) && Hec.Dss.DssReader.IsValid(value);
        }

        /// <summary>
        /// Copies values and timestamps from a <see cref="Hec.Dss.TimeSeries"/> into a new
        /// <see cref="TimeSeries"/>, converting DSS missing-value sentinels to <see cref="double.NaN"/>.
        /// </summary>
        /// <param name="dssTs">The DSS time series returned by <c>DssReader.GetTimeSeries</c>.</param>
        /// <param name="interval">The resolved time interval (regular or irregular).</param>
        /// <returns>A normalized <see cref="TimeSeries"/> with sentinel values converted to NaN.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="dssTs"/> is null.</exception>
        /// <exception cref="Exception">Thrown when DSS arrays are malformed, duplicate timestamps exist, or no valid regular extent can be determined.</exception>
        /// <remarks>
        /// Uses <see cref="Hec.Dss.DssReader.IsValid(double)"/> as the canonical sentinel detector,
        /// which recognizes -901.0 (MISSING_VALUE), -902.0 (MISSING_RECORD), and the two
        /// UNDEFINED_DOUBLE float-to-double conversion artifacts (-3.402823466e+38 and
        /// -3.4028234663852886E+38).
        /// The length-mismatch check is
        /// defensive — DssReader already resizes both arrays to <c>numberValuesRead</c> in normal
        /// operation, but a malformed file could still expose a mismatch.
        /// The importer sorts timestamps chronologically, trims regular DSS block padding,
        /// fills omitted regular timesteps with NaN, and preserves irregular ordinates without
        /// filling gaps.
        /// </remarks>
        internal static TimeSeries BuildFromDssTimeSeries(Hec.Dss.TimeSeries dssTs, TimeInterval interval)
        {
            if (dssTs == null) throw new ArgumentNullException(nameof(dssTs));

            int valueCount = dssTs.Values?.Length ?? 0;
            int timeCount = dssTs.Times?.Length ?? 0;
            if (dssTs.Values == null || dssTs.Times == null)
            {
                throw new Exception("DSS file returned null Times or Values arrays.");
            }

            if (valueCount != timeCount)
            {
                throw new Exception(
                    $"DSS file returned mismatched arrays (Times: {timeCount}, Values: {valueCount}). " +
                    "The DSS record may be corrupt.");
            }

            var ordinates = Enumerable.Range(0, valueCount)
                .Select(i =>
                {
                    double rawValue = dssTs.Values[i];
                    bool isValid = IsValidDssValue(rawValue);
                    return new
                    {
                        Time = dssTs.Times[i],
                        IsValid = isValid,
                        Value = isValid ? rawValue : double.NaN,
                    };
                })
                .OrderBy(x => x.Time)
                .ToList();

            for (int i = 1; i < ordinates.Count; i++)
            {
                if (ordinates[i].Time == ordinates[i - 1].Time)
                {
                    throw new Exception(
                        $"DSS file returned duplicate timestamp '{ordinates[i].Time:o}'. " +
                        "Duplicate DSS ordinates cannot be imported safely.");
                }
            }

            var ts = new TimeSeries(interval);
            if (interval == TimeInterval.Irregular)
            {
                foreach (var ordinate in ordinates)
                {
                    ts.Add(new SeriesOrdinate<DateTime, double>(ordinate.Time, ordinate.Value));
                }
                return ts;
            }

            int firstValidIndex = ordinates.FindIndex(x => x.IsValid);
            if (firstValidIndex < 0)
            {
                throw new Exception(
                    "The DSS regular time series does not contain any valid data values. " +
                    "Leading and trailing DSS block padding cannot define a record extent.");
            }

            int lastValidIndex = ordinates.FindLastIndex(x => x.IsValid);
            var trimmedOrdinates = ordinates
                .Skip(firstValidIndex)
                .Take(lastValidIndex - firstValidIndex + 1)
                .ToList();
            var lookup = trimmedOrdinates.ToDictionary(x => x.Time, x => x.Value);
            var consumedDates = new HashSet<DateTime>();
            DateTime endDate = trimmedOrdinates[trimmedOrdinates.Count - 1].Time;
            for (DateTime date = trimmedOrdinates[0].Time; date <= endDate;)
            {
                if (lookup.TryGetValue(date, out double value))
                {
                    ts.Add(new SeriesOrdinate<DateTime, double>(date, value));
                    consumedDates.Add(date);
                }
                else
                {
                    ts.Add(new SeriesOrdinate<DateTime, double>(date, double.NaN));
                }

                DateTime nextDate = TimeSeries.AddTimeInterval(date, interval);
                if (nextDate <= date)
                {
                    throw new Exception(
                        $"The DSS time series interval '{interval}' cannot advance dates during import.");
                }
                date = nextDate;
            }

            foreach (var ordinate in trimmedOrdinates)
            {
                if (!consumedDates.Contains(ordinate.Time))
                {
                    throw new Exception(
                        $"DSS timestamp '{ordinate.Time:o}' does not align with the resolved interval '{interval}'. " +
                        "The record cannot be imported as a regular continuous time series.");
                }
            }

            return ts;
        }

        #endregion

        #region Plot Factory Methods

        /// <summary>
        /// Applies common default plot settings (border, background, legend, padding, plot area).
        /// </summary>
        private static void ApplyDefaultPlotStyle(Plot plot)
        {
            plot.BorderThickness = new Thickness(0);
            plot.Background = System.Windows.Media.Brushes.Transparent;
            plot.LegendBackground = ColorFromHex("#8CFFFFFF");
            plot.LegendBorder = Colors.DarkGray;
            plot.LegendPosition = OxyPlot.Legends.LegendPosition.TopRight;
            plot.Padding = new Thickness(10);
            plot.PlotAreaBackground = new SolidColorBrush(Colors.White);
        }

        /// <summary>
        /// Parses a hex color string to a <see cref="Color"/>.
        /// </summary>
        private static System.Windows.Media.Color ColorFromHex(string hex)
        {
            return (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
        }

        /// <summary>
        /// Creates the default time series plot with axes and series matching the original XAML declaration.
        /// </summary>
        private Plot CreateDefaultTimeSeriesPlot()
        {
            var plot = new Plot();
            ApplyDefaultPlotStyle(plot);
            plot.Title = "Time Series";
            plot.LegendPosition = OxyPlot.Legends.LegendPosition.TopLeft;

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
            plot.Axes.Add(new DateTimeAxis
            {
                Key = "Xaxis",
                Position = OxyPlot.Axes.AxisPosition.Bottom,
                Title = "Date",
                AxisTitleDistance = 15,
                TitleFontSize = 16,
                FontSize = 12,
                MajorGridlineStyle = OxyPlot.LineStyle.Solid,
                MinorGridlineStyle = OxyPlot.LineStyle.None,
            });

            plot.Series.Add(new LineSeries
            {
                Name = "TimeSeriesLine",
                Title = "Time Series Data",
                Color = System.Windows.Media.Color.FromArgb(255, 100, 200, 250),
                MarkerFill = Colors.Transparent,
                StrokeThickness = 2,
                LineStyle = OxyPlot.LineStyle.Solid,
                // Note: DataFieldX/DataFieldY omitted — UpdateTimeSeriesPlot() sets Mapping instead.
            });

            return plot;
        }

        /// <summary>
        /// Creates the default seasonality plot with axes and series matching the original XAML declaration.
        /// </summary>
        private Plot CreateDefaultSeasonalityPlot()
        {
            var plot = new Plot();
            ApplyDefaultPlotStyle(plot);
            plot.Title = "Seasonality";

            plot.Axes.Add(new LinearAxis
            {
                Name = "SeasonalityYAxis",
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
            plot.Axes.Add(new DateTimeAxis
            {
                Name = "MonthAxis",
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

            plot.Series.Add(new AreaSeries
            {
                Name = "ConfidenceInterval",
                Title = "90% Confidence Interval",
                Fill = ColorFromHex("#4A688CAF"),
                Color = ColorFromHex("#FF353B7A"),
                MarkerFill = Colors.Transparent,
                StrokeThickness = 1,
                DataFieldX = "X1",
                DataFieldX2 = "X2",
                DataFieldY = "Y1",
                DataFieldY2 = "Y2",
            });
            plot.Series.Add(new AreaSeries
            {
                Name = "InnerConfidenceInterval",
                Title = "50% Confidence Interval",
                Fill = ColorFromHex("#7529D372"),
                Color = ColorFromHex("#FF353B7A"),
                MarkerFill = Colors.Transparent,
                StrokeThickness = 1,
                DataFieldX = "X1",
                DataFieldX2 = "X2",
                DataFieldY = "Y1",
                DataFieldY2 = "Y2",
            });
            plot.Series.Add(new LineSeries
            {
                Name = "MedianLine",
                Title = "Median",
                Color = Colors.Blue,
                MarkerFill = Colors.Transparent,
                StrokeThickness = 2,
                LineStyle = OxyPlot.LineStyle.Solid,
                DataFieldX = "X",
                DataFieldY = "Y",
            });
            plot.Series.Add(new LineSeries
            {
                Name = "MeanLine",
                Title = "Mean",
                Color = Colors.Blue,
                MarkerFill = Colors.Transparent,
                StrokeThickness = 2,
                LineStyle = OxyPlot.LineStyle.DashDot,
                DataFieldX = "X",
                DataFieldY = "Y",
            });
            plot.Series.Add(new LineSeries
            {
                Name = "MinLine",
                Title = "Minimum",
                Color = Colors.Black,
                MarkerFill = Colors.Transparent,
                StrokeThickness = 2,
                LineStyle = OxyPlot.LineStyle.Dot,
                Visibility = Visibility.Hidden,
                DataFieldX = "X",
                DataFieldY = "Y",
            });
            plot.Series.Add(new LineSeries
            {
                Name = "MaxLine",
                Title = "Maximum",
                Color = Colors.Black,
                MarkerFill = Colors.Transparent,
                StrokeThickness = 2,
                LineStyle = OxyPlot.LineStyle.Dot,
                Visibility = Visibility.Hidden,
                DataFieldX = "X",
                DataFieldY = "Y",
            });
            plot.Series.Add(new HistogramSeries
            {
                Name = "Histogram",
                Title = "Seasonality Plot",
                FillColor = ColorFromHex("#7529D372"),
                StrokeColor = System.Windows.Media.Color.FromArgb(255, 53, 59, 122),
                StrokeThickness = 1,
            });

            return plot;
        }

        /// <summary>
        /// Creates the default ACF plot with axes matching the original XAML declaration.
        /// </summary>
        private static Plot CreateDefaultACFPlot()
        {
            var plot = new Plot { Title = "Autocorrelation Function" };
            ApplyDefaultPlotStyle(plot);

            plot.Axes.Add(new LinearAxis
            {
                Key = "Yaxis",
                Position = OxyPlot.Axes.AxisPosition.Left,
                Title = "Autocorrelation",
                AxisTitleDistance = 20,
                TitleFontSize = 16,
                FontSize = 12,
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
            });

            plot.Series.Add(new HistogramSeries
            {
                Name = "Autocorrelation",
                Title = "Autocorrelation",
                FillColor = System.Windows.Media.Color.FromArgb(125, 104, 140, 175),
                StrokeColor = System.Windows.Media.Color.FromArgb(255, 53, 59, 122),
                StrokeThickness = 1,
            });

            return plot;
        }

        /// <summary>
        /// Creates the default PACF plot with axes matching the original XAML declaration.
        /// </summary>
        private static Plot CreateDefaultPACFPlot()
        {
            var plot = new Plot { Title = "Partial Autocorrelation Function" };
            ApplyDefaultPlotStyle(plot);

            plot.Axes.Add(new LinearAxis
            {
                Key = "Yaxis",
                Position = OxyPlot.Axes.AxisPosition.Left,
                Title = "Partial Autocorrelation",
                AxisTitleDistance = 20,
                TitleFontSize = 16,
                FontSize = 12,
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
            });

            plot.Series.Add(new HistogramSeries
            {
                Name = "PartialAutocorrelation",
                Title = "Partial Autocorrelation",
                FillColor = System.Windows.Media.Color.FromArgb(125, 104, 140, 175),
                StrokeColor = System.Windows.Media.Color.FromArgb(255, 53, 59, 122),
                StrokeThickness = 1,
            });

            return plot;
        }

        /// <summary>
        /// Deserializes plot settings from a database column into the specified plot.
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

        #endregion

        #region Plot Axis Sync

        /// <summary>
        /// Updates default-managed Y-axis titles on the time series and seasonality plots.
        /// </summary>
        /// <param name="previousUnitLabel">The unit label value that was automatic before the current update.</param>
        /// <remarks>
        /// The seasonality plot uses <c>Relative Frequency</c> for peak seasonality and
        /// <see cref="UnitLabel"/> for monthly summary statistics. User-customized titles are preserved.
        /// </remarks>
        private void UpdatePlotYAxisTitles(string previousUnitLabel = null)
        {
            const string relativeFrequencyTitle = "Relative Frequency";

            SetAxisTitleIfDefault(_timeSeriesPlot, "Yaxis", _unitLabel, previousUnitLabel);

            string seasonalityTitle = IsPeakSeasonality ? relativeFrequencyTitle : _unitLabel;
            if (!SetAxisTitleIfDefault(_seasonalityPlot, "Yaxis", seasonalityTitle, previousUnitLabel) && !IsPeakSeasonality)
            {
                SetAxisTitleIfDefault(_seasonalityPlot, "Yaxis", seasonalityTitle, relativeFrequencyTitle);
            }
        }

        /// <summary>
        /// Refreshes axis titles after plot settings have been loaded from disk.
        /// </summary>
        /// <param name="timeSeriesPlotRestored">Whether time-series plot settings were restored from storage.</param>
        /// <param name="seasonalityPlotRestored">Whether seasonality plot settings were restored from storage.</param>
        /// <remarks>
        /// Factory plots still use the original <c>Value</c> default and should be advanced to the
        /// current unit label. Successfully deserialized titles are treated as intentional unless
        /// they are blank or already match the current source label or seasonality mode default.
        /// </remarks>
        private void RefreshPlotAxisTitlesAfterOpen(bool timeSeriesPlotRestored, bool seasonalityPlotRestored)
        {
            const string relativeFrequencyTitle = "Relative Frequency";

            SetAxisTitleIfDefault(_timeSeriesPlot, "Yaxis", _unitLabel, timeSeriesPlotRestored ? null : DefaultUnitLabel);

            string seasonalityTitle = IsPeakSeasonality ? relativeFrequencyTitle : _unitLabel;
            string previousSeasonalityTitle = seasonalityPlotRestored ? null : DefaultUnitLabel;
            if (!SetAxisTitleIfDefault(_seasonalityPlot, "Yaxis", seasonalityTitle, previousSeasonalityTitle) && !IsPeakSeasonality)
            {
                SetAxisTitleIfDefault(_seasonalityPlot, "Yaxis", seasonalityTitle, relativeFrequencyTitle);
            }
        }

        /// <summary>
        /// Sets the title of a specific axis on a plot when the axis still uses an automatic title.
        /// </summary>
        /// <param name="plot">The plot containing the axis.</param>
        /// <param name="axisKey">The axis key.</param>
        /// <param name="title">The automatic title to apply.</param>
        /// <param name="previousTitle">The automatic title that was valid before the current update.</param>
        /// <returns><c>true</c> when the title was applied; otherwise, <c>false</c>.</returns>
        /// <remarks>
        /// The helper centralizes the beta.3 default-versus-custom title behavior for element-owned plots.
        /// </remarks>
        private static bool SetAxisTitleIfDefault(Plot plot, string axisKey, string title, string previousTitle = null)
        {
            if (plot == null) return false;
            var axis = plot.Axes.FirstOrDefault(a => a.Key == axisKey);
            if (axis != null && PlotAxisTitleDefaults.SetTitleIfDefault(axis, title, previousTitle))
            {
                plot.InvalidatePlot(false);
                return true;
            }

            return false;
        }

        #endregion

        #region Undo Bridge Management

        /// <summary>
        /// Creates undo bridges for the time series collection and plot visual properties.
        /// </summary>
        /// <remarks>
        /// The bridge connects the <see cref="TimeSeries"/> collection to the <see cref="ElementBase.UndoManager"/>
        /// so that add, remove, replace, and clear operations are automatically recorded for undo/redo.
        /// A <see cref="UndoableCollectionBridge{T}.BulkRestoreWrapper"/> suppresses CollectionChanged during
        /// bulk restore, then fires a single Reset event afterward.
        /// Plot bridges monitor visual properties on each Plot, its Axes, Series, and Annotations.
        /// </remarks>
        private void SetupBridges()
        {
            DisposeBridges();

            // Subscribe to UndoManager.StateChanged to revalidate after undo/redo completes.
            UndoManager.StateChanged += UndoManager_StateChanged;

            if (_timeSeries != null)
            {
                _timeSeriesBridge = new UndoableCollectionBridge<SeriesOrdinate<DateTime, double>>(
                    _timeSeries,
                    () => IsUndoEnabled ? UndoManager : null,
                    "time series",
                    this
                );
                _timeSeriesBridge.BulkRestoreWrapper = (restoreAction) =>
                {
                    _timeSeries.SuppressCollectionChanged = true;
                    try
                    {
                        restoreAction();
                    }
                    finally
                    {
                        _timeSeries.SuppressCollectionChanged = false;
                    }
                    _timeSeries.RaiseCollectionChangedReset();
                };
            }

            // Create plot undo managers — each monitors its plot's axes and annotations
            // for collection changes and auto-rebuilds bridges as needed.
            Func<IUndoManager> getUndo = () => IsUndoEnabled ? UndoManager : null;
            Action onRecorded = () => SetIsDirty(true);

            if (_timeSeriesPlot != null)
                _timeSeriesPlotUndo = new PlotUndoManager(_timeSeriesPlot, getUndo, "time series plot", this, onRecorded);
            if (_seasonalityPlot != null)
                _seasonalityPlotUndo = new PlotUndoManager(_seasonalityPlot, getUndo, "seasonality plot", this, onRecorded);
            if (_acfPlot != null)
                _acfPlotUndo = new PlotUndoManager(_acfPlot, getUndo, "ACF plot", this, onRecorded);
            if (_pacfPlot != null)
                _pacfPlotUndo = new PlotUndoManager(_pacfPlot, getUndo, "PACF plot", this, onRecorded);
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
        private void DisposeBridges()
        {
            UndoManager.StateChanged -= UndoManager_StateChanged;

            _timeSeriesBridge?.Dispose();
            _timeSeriesBridge = null;

            _timeSeriesPlotUndo?.Dispose();
            _timeSeriesPlotUndo = null;
            _seasonalityPlotUndo?.Dispose();
            _seasonalityPlotUndo = null;
            _acfPlotUndo?.Dispose();
            _acfPlotUndo = null;
            _pacfPlotUndo?.Dispose();
            _pacfPlotUndo = null;
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
            if (_timeSeriesPlotUndo != null) suspensions.Add(_timeSeriesPlotUndo.SuspendRecording());
            if (_seasonalityPlotUndo != null) suspensions.Add(_seasonalityPlotUndo.SuspendRecording());
            if (_acfPlotUndo != null) suspensions.Add(_acfPlotUndo.SuspendRecording());
            if (_pacfPlotUndo != null) suspensions.Add(_pacfPlotUndo.SuspendRecording());
            return new AggregateDisposable(suspensions);
        }

        /// <summary>
        /// Finds the <see cref="PlotUndoManager"/> for the specified plot.
        /// </summary>
        /// <param name="plot">The plot to look up.</param>
        /// <returns>The corresponding <see cref="PlotUndoManager"/>, or null if not found.</returns>
        private PlotUndoManager FindPlotUndoManager(Plot plot)
        {
            if (plot == _timeSeriesPlot) return _timeSeriesPlotUndo;
            if (plot == _seasonalityPlot) return _seasonalityPlotUndo;
            if (plot == _acfPlot) return _acfPlotUndo;
            if (plot == _pacfPlot) return _pacfPlotUndo;
            return null;
        }


        #endregion

    }
}
