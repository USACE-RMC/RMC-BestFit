using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GenericControls;
using Hec.Dss;
using Numerics.Data;
using FrameworkInterfaces;
using RMC.BestFit.Models;
using RMC.BestFit.UI;

namespace RMC_BestFit
{
    /// <summary>
    /// User control for editing time series properties including entry method, data sources,
    /// and configuration options for various time series data providers (Manual, HEC-DSS, GHCN, USGS).
    /// </summary>
    public partial class TimeSeriesPropertiesControl : UserControl
    {
        /// <summary>
        /// Maximum time the properties panel waits for an external data download before returning control to the user.
        /// </summary>
        /// <remarks>
        /// Provider requests are also bounded in Numerics, but this UI-level limit prevents the
        /// application from appearing to churn indefinitely on locked-down networks.
        /// </remarks>
        private static readonly TimeSpan ExternalDownloadTimeout = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Maximum time the properties panel waits for instantaneous external data downloads.
        /// </summary>
        /// <remarks>
        /// Instantaneous provider payloads can be much larger than daily or peak downloads, so they
        /// need a longer UI-level cancellation window than the default external download timeout.
        /// </remarks>
        private static readonly TimeSpan InstantaneousExternalDownloadTimeout = TimeSpan.FromSeconds(180);

        /// <summary>
        /// Maximum time the properties panel waits for GHCN daily station-file downloads.
        /// </summary>
        /// <remarks>
        /// NOAA NCEI can take several minutes to return station-file response headers even when the
        /// data are reachable and parse quickly after the response starts.
        /// </remarks>
        private static readonly TimeSpan GhcnExternalDownloadTimeout = TimeSpan.FromSeconds(300);

        /// <summary>
        /// Initializes a new instance of the <see cref="TimeSeriesPropertiesControl"/> class.
        /// </summary>
        public TimeSeriesPropertiesControl()
        {
            InitializeComponent();
            DataContext = this;
        }

        #region Members

        /// <summary>
        /// Stores the previous name value for validation purposes.
        /// </summary>
        private string _previousName;

        /// <summary>
        /// Dependency property for the time series element.
        /// </summary>
        public static DependencyProperty ElementProperty = DependencyProperty.Register(nameof(Element), typeof(TimeSeriesElement), typeof(TimeSeriesPropertiesControl), new PropertyMetadata(null, ElementCallback));

        /// <summary>
        /// Gets or sets the time series element whose properties are being edited.
        /// </summary>
        public TimeSeriesElement Element
        {
            get { return (TimeSeriesElement)GetValue(ElementProperty); }
            set { SetValue(ElementProperty, value); }
        }

        /// <summary>
        /// Callback method invoked when the Element property changes.
        /// </summary>
        /// <param name="d">The dependency object whose property changed.</param>
        /// <param name="e">Event arguments containing the old and new values.</param>
        private static void ElementCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d == null) return;
            if (d as TimeSeriesPropertiesControl == null) return;
            var thisControl = (TimeSeriesPropertiesControl)d;

            if (e.NewValue == null) return;
            var newElement = e.NewValue as TimeSeriesElement;
            if (newElement == null) return;

            thisControl._previousName = newElement.Name;
            thisControl.PropertyAttributes.GetClassAttributes(newElement);
            thisControl.UpdateProcessDataButtonText();
        }

        /// <summary>
        /// Dependency property for the existing names property.
        /// </summary>
        public static DependencyProperty ExistingNamesProperty = DependencyProperty.Register(nameof(ExistingNames), typeof(string[]), typeof(TimeSeriesPropertiesControl), new FrameworkPropertyMetadata(new string[] { }));

        /// <summary>
        /// Gets the array of existing time series names for validation purposes.
        /// </summary>
        public string[] ExistingNames
        {
            get { return (string[])GetValue(ExistingNamesProperty); }
            private set { SetValue(ExistingNamesProperty, value); }
        }

        /// <summary>
        /// Cached list of available time series entry method options.
        /// </summary>
        private static readonly List<TimeSeriesEntryMethodItem> _entryMethodOptions = new List<TimeSeriesEntryMethodItem>
        {
            new TimeSeriesEntryMethodItem("Manual Entry", TimeSeriesElement.TimeSeriesEntryMethod.Manual, "Manually enter time series data"),
            new TimeSeriesEntryMethodItem("HEC-DSS", TimeSeriesElement.TimeSeriesEntryMethod.HECDSS, "Import from HEC Data Storage System file"),
            new TimeSeriesEntryMethodItem("USGS", TimeSeriesElement.TimeSeriesEntryMethod.USGS, "United States Geological Survey"),
            new TimeSeriesEntryMethodItem("GHCN", TimeSeriesElement.TimeSeriesEntryMethod.GHCN, "Global Historical Climatology Network (NOAA)"),
            new TimeSeriesEntryMethodItem("ABOM", TimeSeriesElement.TimeSeriesEntryMethod.ABOM, "Australian Bureau of Meteorology"),
            new TimeSeriesEntryMethodItem("CHMN", TimeSeriesElement.TimeSeriesEntryMethod.CHMN, "Canadian Hydrometric Monitoring Network")
        };

        /// <summary>
        /// Gets the list of available time series entry method options.
        /// </summary>
        public static List<TimeSeriesEntryMethodItem> EntryMethodOptions => _entryMethodOptions;

        /// <summary>
        /// Cached list of available depth unit options for precipitation and snow data.
        /// </summary>
        private static readonly List<DepthUnitItem> _depthUnitOptions = new List<DepthUnitItem>
        {
            new DepthUnitItem("Millimeters", TimeSeriesDownload.DepthUnit.Millimeters),
            new DepthUnitItem("Centimeters", TimeSeriesDownload.DepthUnit.Centimeters),
            new DepthUnitItem("Inches", TimeSeriesDownload.DepthUnit.Inches),
        };

        /// <summary>
        /// Gets the list of available depth unit options for precipitation and snow data.
        /// </summary>
        public static List<DepthUnitItem> DepthUnitOptions => _depthUnitOptions;

        /// <summary>
        /// Cached list of available time interval options for manual time series entry.
        /// </summary>
        private static readonly List<TimeIntervalItem> _timeIntervalOptions = new List<TimeIntervalItem>
        {
            new TimeIntervalItem("15-Min", TimeInterval.FifteenMinute),
            new TimeIntervalItem("30-Min", TimeInterval.ThirtyMinute),
            new TimeIntervalItem("1-Hr", TimeInterval.OneHour),
            new TimeIntervalItem("6-Hr", TimeInterval.SixHour),
            new TimeIntervalItem("12-Hr", TimeInterval.TwelveHour),
            new TimeIntervalItem("1-Day", TimeInterval.OneDay),
            new TimeIntervalItem("1-Month", TimeInterval.OneMonth),
            new TimeIntervalItem("1-Quarter", TimeInterval.OneQuarter),
            new TimeIntervalItem("1-Year", TimeInterval.OneYear),
            new TimeIntervalItem("Irregular", TimeInterval.Irregular),
        };

        /// <summary>
        /// Gets the list of available time interval options for manual time series entry.
        /// </summary>
        public static List<TimeIntervalItem> TimeIntervalOptions => _timeIntervalOptions;

        /// <summary>
        /// Cached list of available GHCN time series type options.
        /// </summary>
        private static readonly List<TimeSeriesTypeItem> _ghcnSeriesTypes = new List<TimeSeriesTypeItem>
        {
            new TimeSeriesTypeItem("Daily Precipitation", TimeSeriesDownload.TimeSeriesType.DailyPrecipitation),
            new TimeSeriesTypeItem("Daily Snow", TimeSeriesDownload.TimeSeriesType.DailySnow)
        };

        /// <summary>
        /// Cached list of available USGS time series type options.
        /// </summary>
        private static readonly List<TimeSeriesTypeItem> _usgsSeriesTypes = new List<TimeSeriesTypeItem>
        {
            new TimeSeriesTypeItem("Daily Discharge", TimeSeriesDownload.TimeSeriesType.DailyDischarge),
            new TimeSeriesTypeItem("Daily Stage", TimeSeriesDownload.TimeSeriesType.DailyStage),
            new TimeSeriesTypeItem("Instantaneous Discharge", TimeSeriesDownload.TimeSeriesType.InstantaneousDischarge),
            new TimeSeriesTypeItem("Instantaneous Stage", TimeSeriesDownload.TimeSeriesType.InstantaneousStage),
            new TimeSeriesTypeItem("Peak Discharge", TimeSeriesDownload.TimeSeriesType.PeakDischarge),
            new TimeSeriesTypeItem("Peak Stage", TimeSeriesDownload.TimeSeriesType.PeakStage),
            new TimeSeriesTypeItem("Measured Discharge", TimeSeriesDownload.TimeSeriesType.MeasuredDischarge),
            new TimeSeriesTypeItem("Measured Stage", TimeSeriesDownload.TimeSeriesType.MeasuredStage)
        };

        /// <summary>
        /// Cached list of available CHMN time series type options.
        /// </summary>
        private static readonly List<TimeSeriesTypeItem> _chmnSeriesTypes = new List<TimeSeriesTypeItem>
        {
            new TimeSeriesTypeItem("Daily Discharge", TimeSeriesDownload.TimeSeriesType.DailyDischarge),
            new TimeSeriesTypeItem("Daily Stage", TimeSeriesDownload.TimeSeriesType.DailyStage),
            new TimeSeriesTypeItem("Instantaneous Discharge", TimeSeriesDownload.TimeSeriesType.InstantaneousDischarge),
            new TimeSeriesTypeItem("Instantaneous Stage", TimeSeriesDownload.TimeSeriesType.InstantaneousStage),
            new TimeSeriesTypeItem("Peak Discharge", TimeSeriesDownload.TimeSeriesType.PeakDischarge),
            new TimeSeriesTypeItem("Peak Stage", TimeSeriesDownload.TimeSeriesType.PeakStage),
        };

        /// <summary>
        /// Cached list of available ABOM time series type options.
        /// </summary>
        private static readonly List<TimeSeriesTypeItem> _abomSeriesTypes = new List<TimeSeriesTypeItem>
        {
            new TimeSeriesTypeItem("Daily Discharge", TimeSeriesDownload.TimeSeriesType.DailyDischarge),
            new TimeSeriesTypeItem("Daily Stage", TimeSeriesDownload.TimeSeriesType.DailyStage),
            new TimeSeriesTypeItem("Daily Precipitation", TimeSeriesDownload.TimeSeriesType.DailyPrecipitation),
            new TimeSeriesTypeItem("Instantaneous Discharge", TimeSeriesDownload.TimeSeriesType.InstantaneousDischarge),
            new TimeSeriesTypeItem("Instantaneous Stage", TimeSeriesDownload.TimeSeriesType.InstantaneousStage),
        };

        #endregion

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event on the Name field to display property attributes.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void Name_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Element == null) return;
            PropertyAttributes.GetPropertyAttributes(nameof(Element.Name), Element);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event on the Description field to display property attributes.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void Description_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Element == null) return;
            PropertyAttributes.GetPropertyAttributes(nameof(Element.Description), Element);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event on the CreationDate field to display property attributes.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void CreationDate_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Element == null) return;
            PropertyAttributes.GetPropertyAttributes(nameof(Element.CreationDate), Element);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event on the LastModified field to display property attributes.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void LastModified_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Element == null) return;
            PropertyAttributes.GetPropertyAttributes(nameof(Element.LastModified), Element);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event on the UnitLabel field to display property attributes.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void UnitLabel_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Element == null) return;
            PropertyAttributes.GetPropertyAttributes(nameof(Element.UnitLabel), Element);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event on the EntryMethod field to display property attributes.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void EntryMethod_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Element == null) return;
            PropertyAttributes.GetPropertyAttributes(nameof(Element.EntryMethod), Element);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event on the HEC-DSS Filename field to display property attributes.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void HECDSSFilename_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Element == null) return;
            PropertyAttributes.GetPropertyAttributes(nameof(Element.HECDSSFullFilename), Element);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event on the HEC-DSS Pathname field to display property attributes.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void HECDSSPathname_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Element == null) return;
            PropertyAttributes.GetPropertyAttributes(nameof(Element.HECDSSDataPathname), Element);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event on the DataType field to display property attributes.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void DataType_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Element == null) return;
            PropertyAttributes.GetPropertyAttributes(nameof(Element.SeriesType), Element);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event on the USGS Site Number field to display property attributes.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void USGSSiteNumber_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Element == null) return;
            PropertyAttributes.GetPropertyAttributes(nameof(Element.USGSSiteNumber), Element);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event on the GHCN Site Number field to display property attributes.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void GHCNSiteNumber_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Element == null) return;
            PropertyAttributes.GetPropertyAttributes(nameof(Element.GHCNSiteNumber), Element);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event on the CHMN Site Number field to display property attributes.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void CHMNSiteNumber_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Element == null) return;
            PropertyAttributes.GetPropertyAttributes(nameof(Element.CHMNSiteNumber), Element);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event on the ABOM Site Number field to display property attributes.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void ABOMSiteNumber_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Element == null) return;
            PropertyAttributes.GetPropertyAttributes(nameof(Element.ABOMSiteNumber), Element);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event on the Depth Unit Selector field to display property attributes.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void DepthUnitSelector_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Element == null) return;
            PropertyAttributes.GetPropertyAttributes(nameof(Element.DepthUnit), Element);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event on the Time Interval field to display property attributes.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void TimeInterval_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Element == null) return;
            PropertyAttributes.GetPropertyAttributes(nameof(Element.TimeInterval), Element);
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event on the Start Date Time field to display property attributes.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void StartDateTime_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Element == null) return;
            PropertyAttributes.GetPropertyAttributes(nameof(Element.StartDateTime), Element);
        }

        /// <summary>
        /// Handles the GotFocus event on the Name field to store the current name and populate existing names for validation.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void Name_GotFocus(object sender, RoutedEventArgs e)
        {
            if (Element == null) return;
            _previousName = Element.Name;
            ExistingNames = Element.ParentCollection.GetElementNames(Element).ToArray();
        }

        /// <summary>
        /// Handles the LostFocus event on the Name field to restore the previous name if validation fails.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void Name_LostFocus(object sender, RoutedEventArgs e)
        {
            if (ElementName.NameTextBox.IsValid) return;
            if (Element == null) return;
            Element.Name = _previousName;
        }

        /// <summary>
        /// Updates the process data button text and tooltip based on the current entry method.
        /// HEC-DSS uses "Import" since it reads from a local file; all other external sources use "Download".
        /// </summary>
        private void UpdateProcessDataButtonText()
        {
            if (Element == null) return;
            if (Element.EntryMethod == TimeSeriesElement.TimeSeriesEntryMethod.HECDSS)
            {
                ProcessDataButtonText.Text = "Import";
                ProcessDataButton.ToolTip = "Import Time Series Data";
            }
            else
            {
                ProcessDataButtonText.Text = "Download";
                ProcessDataButton.ToolTip = "Download Time Series Data";
            }
        }

        /// <summary>
        /// Handles the SelectionChanged event of the Entry Method combo box to show/hide relevant fields based on the selected entry method.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void EntryMethodComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Element == null) return;

            UpdateProcessDataButtonText();
            var comboBox = (ComboBox)DataType.InnerContent;

            if (Element.EntryMethod == TimeSeriesElement.TimeSeriesEntryMethod.Manual)
            {
                TimeIntervalSelector.Visibility = Visibility.Visible;
                StartDateTime.Visibility = Visibility.Visible;
                HECDSSFilename.Visibility = Visibility.Collapsed;
                HECDSSPathname.Visibility = Visibility.Collapsed;
                GHCNSiteNumber.Visibility = Visibility.Collapsed;
                DepthUnitSelector.Visibility = Visibility.Collapsed;
                USGSSiteNumber.Visibility = Visibility.Collapsed;
                CHMNSiteNumber.Visibility = Visibility.Collapsed;
                ABOMSiteNumber.Visibility = Visibility.Collapsed;
                DataType.Visibility = Visibility.Collapsed;
                ProcessDataButton.Visibility = Visibility.Collapsed;
            }
            else if (Element.EntryMethod == TimeSeriesElement.TimeSeriesEntryMethod.HECDSS)
            {
                TimeIntervalSelector.Visibility = Visibility.Collapsed;
                StartDateTime.Visibility = Visibility.Collapsed;
                HECDSSFilename.Visibility = Visibility.Visible;
                HECDSSPathname.Visibility = Visibility.Visible;
                GHCNSiteNumber.Visibility = Visibility.Collapsed;
                DepthUnitSelector.Visibility = Visibility.Collapsed;
                USGSSiteNumber.Visibility = Visibility.Collapsed;
                CHMNSiteNumber.Visibility = Visibility.Collapsed;
                ABOMSiteNumber.Visibility = Visibility.Collapsed;
                DataType.Visibility = Visibility.Collapsed;
                ProcessDataButton.Visibility = Visibility.Visible;
            }
            else if (Element.EntryMethod == TimeSeriesElement.TimeSeriesEntryMethod.GHCN)
            {
                TimeIntervalSelector.Visibility = Visibility.Collapsed;
                StartDateTime.Visibility = Visibility.Collapsed;
                HECDSSFilename.Visibility = Visibility.Collapsed;
                HECDSSPathname.Visibility = Visibility.Collapsed;
                GHCNSiteNumber.Visibility = Visibility.Visible;
                DepthUnitSelector.Visibility = Visibility.Visible;
                USGSSiteNumber.Visibility = Visibility.Collapsed;
                CHMNSiteNumber.Visibility = Visibility.Collapsed;
                ABOMSiteNumber.Visibility = Visibility.Collapsed;
                DataType.Visibility = Visibility.Visible;
                ProcessDataButton.Visibility = Visibility.Visible;

                comboBox.ItemsSource = _ghcnSeriesTypes;
                DefaultSeriesTypeIfNeeded(_ghcnSeriesTypes);
            }
            else if (Element.EntryMethod == TimeSeriesElement.TimeSeriesEntryMethod.USGS)
            {
                TimeIntervalSelector.Visibility = Visibility.Collapsed;
                StartDateTime.Visibility = Visibility.Collapsed;
                HECDSSFilename.Visibility = Visibility.Collapsed;
                HECDSSPathname.Visibility = Visibility.Collapsed;
                GHCNSiteNumber.Visibility = Visibility.Collapsed;
                DepthUnitSelector.Visibility = Visibility.Collapsed;
                USGSSiteNumber.Visibility = Visibility.Visible;
                CHMNSiteNumber.Visibility = Visibility.Collapsed;
                ABOMSiteNumber.Visibility = Visibility.Collapsed;
                DataType.Visibility = Visibility.Visible;
                ProcessDataButton.Visibility = Visibility.Visible;

                comboBox.ItemsSource = _usgsSeriesTypes;
                DefaultSeriesTypeIfNeeded(_usgsSeriesTypes);
            }
            else if (Element.EntryMethod == TimeSeriesElement.TimeSeriesEntryMethod.CHMN)
            {
                TimeIntervalSelector.Visibility = Visibility.Collapsed;
                StartDateTime.Visibility = Visibility.Collapsed;
                HECDSSFilename.Visibility = Visibility.Collapsed;
                HECDSSPathname.Visibility = Visibility.Collapsed;
                GHCNSiteNumber.Visibility = Visibility.Collapsed;
                DepthUnitSelector.Visibility = Visibility.Collapsed;
                USGSSiteNumber.Visibility = Visibility.Collapsed;
                CHMNSiteNumber.Visibility = Visibility.Visible;
                ABOMSiteNumber.Visibility = Visibility.Collapsed;
                DataType.Visibility = Visibility.Visible;
                ProcessDataButton.Visibility = Visibility.Visible;

                comboBox.ItemsSource = _chmnSeriesTypes;
                DefaultSeriesTypeIfNeeded(_chmnSeriesTypes);
            }
            else if (Element.EntryMethod == TimeSeriesElement.TimeSeriesEntryMethod.ABOM)
            {
                TimeIntervalSelector.Visibility = Visibility.Collapsed;
                StartDateTime.Visibility = Visibility.Collapsed;
                HECDSSFilename.Visibility = Visibility.Collapsed;
                HECDSSPathname.Visibility = Visibility.Collapsed;
                GHCNSiteNumber.Visibility = Visibility.Collapsed;
                DepthUnitSelector.Visibility = Element.SeriesType == TimeSeriesDownload.TimeSeriesType.DailyPrecipitation
                    ? Visibility.Visible : Visibility.Collapsed;
                USGSSiteNumber.Visibility = Visibility.Collapsed;
                CHMNSiteNumber.Visibility = Visibility.Collapsed;
                ABOMSiteNumber.Visibility = Visibility.Visible;
                DataType.Visibility = Visibility.Visible;
                ProcessDataButton.Visibility = Visibility.Visible;

                comboBox.ItemsSource = _abomSeriesTypes;
                DefaultSeriesTypeIfNeeded(_abomSeriesTypes);
            }
        }

        /// <summary>
        /// If the current <see cref="TimeSeriesElement.SeriesType"/> is not available in the new
        /// data type list, defaults to the first item. Otherwise, keeps the current selection.
        /// </summary>
        /// <param name="list">The list of available data type options for the current entry method.</param>
        private void DefaultSeriesTypeIfNeeded(List<TimeSeriesTypeItem> list)
        {
            if (!list.Any(t => t.Value == Element.SeriesType))
            {
                Element.SeriesType = list[0].Value;
            }

            // Force the ComboBox to re-resolve its SelectedValue against the new ItemsSource.
            // Setting ItemsSource clears WPF's internal selection; the TwoWay binding alone
            // doesn't reliably re-select the matching item from a completely new list.
            var comboBox = (ComboBox)DataType.InnerContent;
            comboBox.SelectedValue = Element.SeriesType;
        }

        /// <summary>
        /// Handles the Click event of the Process Data button to download time series data from the selected source.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private async void ProcessDataButton_Click(object sender, RoutedEventArgs e)
        {
            if (Element == null) return;
            ProcessDataButton.IsEnabled = false;
            Mouse.OverrideCursor = Cursors.Wait;
            TimeSpan timeout = GetExternalDownloadTimeout();
            try
            {
                using (var cts = new CancellationTokenSource(timeout))
                {
                    await Element.Download(cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                GenericControls.MessageBox.Show(
                    $"The download did not complete within {timeout.TotalSeconds:0} seconds. " +
                    "The selected data provider may be blocked or unavailable from this network.",
                    "Download timed out",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }
            catch (Exception ex)
            {
                GenericControls.MessageBox.Show("An error occurred: " + ex.Message, "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            finally
            {
                Mouse.OverrideCursor = null;
                ProcessDataButton.IsEnabled = true;
            }
        }

        /// <summary>
        /// Gets the UI cancellation timeout for the selected external download.
        /// </summary>
        /// <returns>The timeout to apply to the current download request.</returns>
        /// <remarks>
        /// Instantaneous discharge and stage requests use a longer timeout because their provider
        /// payloads are larger and may take longer to stream through agency networks.
        /// </remarks>
        private TimeSpan GetExternalDownloadTimeout()
        {
            if (Element?.EntryMethod == TimeSeriesElement.TimeSeriesEntryMethod.GHCN)
            {
                return GhcnExternalDownloadTimeout;
            }

            if (Element != null &&
                (Element.SeriesType == TimeSeriesDownload.TimeSeriesType.InstantaneousDischarge ||
                 Element.SeriesType == TimeSeriesDownload.TimeSeriesType.InstantaneousStage))
            {
                return InstantaneousExternalDownloadTimeout;
            }

            return ExternalDownloadTimeout;
        }

        /// <summary>
        /// Handles the Click event of the Pathname button to open the DSS path selector window.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void PathNameButton_Click(object sender, RoutedEventArgs e)
        {
            if (Element == null) return;
            if (File.Exists(Element.HECDSSFullFilename))
            {
                var dssControl = new DSSPathSelectorWindow() { FullFileName = Element.HECDSSFullFilename };
                if (dssControl.ShowDialog() == true)
                {
                    Element.HECDSSDataPathname = dssControl.SelectedPathname;
                }
            }

        }

        /// <summary>
        /// Handles the SelectionChanged event of the Data Type combo box.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void DataType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Element == null) return;

            // Show DepthUnitSelector for precipitation types when ABOM is selected.
            // GHCN always shows DepthUnitSelector (set in EntryMethodComboBox_SelectionChanged).
            if (Element.EntryMethod == TimeSeriesElement.TimeSeriesEntryMethod.ABOM)
            {
                DepthUnitSelector.Visibility = Element.SeriesType == TimeSeriesDownload.TimeSeriesType.DailyPrecipitation
                    ? Visibility.Visible : Visibility.Collapsed;
            }
        }


    }
}
