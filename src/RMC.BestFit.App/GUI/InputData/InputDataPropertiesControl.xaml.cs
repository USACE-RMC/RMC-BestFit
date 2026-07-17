using Numerics.Data;
using FrameworkInterfaces;
using RMC.BestFit.Models;
using RMC.BestFit.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace RMC_BestFit
{
    /// <summary>
    /// User control for managing and displaying input data properties and configuration settings.
    /// Provides interface for data entry methods, time series processing, and statistical parameter configuration.
    /// </summary>
    public partial class InputDataPropertiesControl : UserControl
    {
        /// <summary>
        /// Maximum time the properties panel waits for an external USGS download before returning control to the user.
        /// </summary>
        /// <remarks>
        /// This UI-level cap complements the Numerics request timeout so a blocked provider cannot
        /// leave the input-data panel appearing to process indefinitely.
        /// </remarks>
        private static readonly TimeSpan ExternalDownloadTimeout = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Initializes a new instance of the <see cref="InputDataPropertiesControl"/> class.
        /// </summary>
        public InputDataPropertiesControl()
        {
            InitializeComponent();
            DataContext = this;
        }

        /// <summary>
        /// Stores the previous name value for validation and rollback purposes.
        /// Initialized in <see cref="ElementCallback"/> to prevent null corruption if
        /// Name_LostFocus fires before Name_GotFocus (e.g., programmatic focus).
        /// </summary>
        private string _previousName;

        /// <summary>
        /// Tracks the currently subscribed <see cref="TimeSeriesCollection"/> to allow
        /// proper unsubscription when the Element changes or the control unloads.
        /// </summary>
        private TimeSeriesCollection _subscribedTimeSeriesCollection;

        /// <summary>
        /// Dependency property for the Element property.
        /// </summary>
        public static DependencyProperty ElementProperty = DependencyProperty.Register(nameof(Element), typeof(InputData), typeof(InputDataPropertiesControl), new PropertyMetadata(null, ElementCallback));

        /// <summary>
        /// Gets or sets the input data element being displayed and edited in this control.
        /// </summary>
        public InputData Element
        {
            get { return (InputData)GetValue(ElementProperty); }
            set { SetValue(ElementProperty, value); }
        }

        /// <summary>
        /// Callback method invoked when the Element dependency property changes.
        /// Updates the property attributes and loads time series data for the new element.
        /// </summary>
        /// <param name="d">The dependency object whose property changed.</param>
        /// <param name="e">Event args containing the old and new values.</param>
        private static void ElementCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d == null) return;
            if (d as InputDataPropertiesControl == null) return;
            var thisControl = (InputDataPropertiesControl)d;

            if (e.NewValue == null) return;
            var newElement = e.NewValue as InputData;
            if (newElement == null) return;

            thisControl._previousName = newElement.Name;
            thisControl.PropertyAttributes.GetClassAttributes(newElement);
            thisControl.UnsubscribeTimeSeriesCollection();
            thisControl.LoadTimeSeriesData();
        }

        /// <summary>
        /// Cached options for exact data entry methods. Static to avoid allocating a new list on every XAML binding call.
        /// </summary>
        private static readonly List<ExactDataMethodItem> _exactDataMethodOptions = new List<ExactDataMethodItem>
        {
            new ExactDataMethodItem("Manual Entry", InputData.ExactDataEntryType.Manual),
            new ExactDataMethodItem("Block Series", InputData.ExactDataEntryType.BlockSeries),
            new ExactDataMethodItem("Peaks-Over-Threshold Series", InputData.ExactDataEntryType.PeaksOverThresholdSeries),
            new ExactDataMethodItem("USGS Peak Discharge", InputData.ExactDataEntryType.USGSPeakDischarge),
            new ExactDataMethodItem("USGS Peak Stage", InputData.ExactDataEntryType.USGSPeakStage)
        };

        /// <summary>
        /// Gets the available options for exact data entry methods.
        /// </summary>
        public List<ExactDataMethodItem> ExactDataMethodOptions => _exactDataMethodOptions;

        /// <summary>
        /// Cached month options for calendar-based filtering.
        /// </summary>
        private static readonly List<MonthItem> _monthOptions = new List<MonthItem>
        {
            new MonthItem("January", 1),
            new MonthItem("February", 2),
            new MonthItem("March", 3),
            new MonthItem("April", 4),
            new MonthItem("May", 5),
            new MonthItem("June", 6),
            new MonthItem("July", 7),
            new MonthItem("August", 8),
            new MonthItem("September", 9),
            new MonthItem("October", 10),
            new MonthItem("November", 11),
            new MonthItem("December", 12)
        };

        /// <summary>
        /// Gets the available month options for calendar-based filtering.
        /// </summary>
        public List<MonthItem> MonthOptions => _monthOptions;

        /// <summary>
        /// Cached block function options for time series aggregation.
        /// </summary>
        private static readonly List<BlockFunctionItem> _blockFunctionOptions = new List<BlockFunctionItem>
        {
            new BlockFunctionItem("Maximum", BlockFunctionType.Maximum),
            new BlockFunctionItem("Minimum", BlockFunctionType.Minimum),
            new BlockFunctionItem("Average", BlockFunctionType.Average),
            new BlockFunctionItem("Sum", BlockFunctionType.Sum)
        };

        /// <summary>
        /// Gets the available block function options for time series aggregation.
        /// </summary>
        public List<BlockFunctionItem> BlockFunctionOptions => _blockFunctionOptions;

        /// <summary>
        /// Cached time block window options for defining analysis periods.
        /// </summary>
        private static readonly List<TimeBlockItem> _timeBlockOptions = new List<TimeBlockItem>
        {
            new TimeBlockItem("Calendar Year", TimeBlockWindow.CalendarYear),
            new TimeBlockItem("Water Year", TimeBlockWindow.WaterYear),
            new TimeBlockItem("Custom Year", TimeBlockWindow.CustomYear)
        };

        /// <summary>
        /// Gets the available time block window options for defining analysis periods.
        /// </summary>
        public List<TimeBlockItem> TimeBlockOptions => _timeBlockOptions;

        /// <summary>
        /// Cached smoothing function options for time series preprocessing.
        /// </summary>
        private static readonly List<SmoothingFunctionItem> _smoothingFunctionOptions = new List<SmoothingFunctionItem>
        {
            new SmoothingFunctionItem("None", SmoothingFunctionType.None),
            new SmoothingFunctionItem("Moving Average", SmoothingFunctionType.MovingAverage),
            new SmoothingFunctionItem("Moving Sum", SmoothingFunctionType.MovingSum),
            new SmoothingFunctionItem("Difference", SmoothingFunctionType.Difference)
        };

        /// <summary>
        /// Gets the available smoothing function options for time series preprocessing.
        /// </summary>
        public List<SmoothingFunctionItem> SmoothingFunctionOptions => _smoothingFunctionOptions;

        /// <summary>
        /// Cached plotting position parameter options with their associated alpha values.
        /// </summary>
        private static readonly List<PlottingPositionItem> _parameterOptions = new List<PlottingPositionItem>
        {
            new PlottingPositionItem("Weibull (α = 0.0)", 0.0, "The Weibull plotting position formula (α = 0.0). Recommended as the default value because it is unbiased for all distributions."),
            new PlottingPositionItem("Median (α = 0.3175)", 0.3175, "The Median plotting position formula (α = 0.3175). Provides median exceedance probabilities for all distributions."),
            new PlottingPositionItem("Blom (α = 0.375)", 0.375, "The Blom (1958) plotting position formula (α = 0.375). Recommended for Normal, Gamma, 2-parameter Log Normal, 3-parameter Log Normal, and Log Pearson Type III distributions."),
            new PlottingPositionItem("Cunnane (α = 0.40)", 0.4, "The Cunnane (1978) plotting position formula (α = 0.40). Recommended for GEV and Log-Gumbel distributions, approximately quantile unbiased."),
            new PlottingPositionItem("Gringorten (α = 0.44)", 0.44, "The Gringorten (1963) plotting position formula (α = 0.44). Recommended for Exponential, Gumbel and Weibull distributions."),
            new PlottingPositionItem("Hazen (α = 0.50)", 0.5, "The Hazen plotting position formula (α = 0.50). Recommended when the parameters of the parent distribution are unknown.")
        };

        /// <summary>
        /// Gets the available plotting position parameter options with their associated alpha values.
        /// </summary>
        public List<PlottingPositionItem> ParameterOptions => _parameterOptions;

        /// <summary>
        /// Dependency property for the existing names collection.
        /// </summary>
        public static DependencyProperty ExistingNamesProperty = DependencyProperty.Register(nameof(ExistingNames), typeof(string[]), typeof(InputDataPropertiesControl), new FrameworkPropertyMetadata(new string[] { }));

        /// <summary>
        /// Gets or sets the collection of existing element names for validation purposes.
        /// </summary>
        public string[] ExistingNames
        {
            get { return (string[])GetValue(ExistingNamesProperty); }
            private set { SetValue(ExistingNamesProperty, value); }
        }

        /// <summary>
        /// Gets the observable collection of time series elements available for selection.
        /// </summary>
        public ObservableCollection<TimeSeriesElement> TimeSeriesElements { get; private set; } = new ObservableCollection<TimeSeriesElement>();

        /// <summary>
        /// Handles the mouse button down event on the Name field to display property attributes.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event args containing mouse button data.</param>
        private void Name_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.Name), Element);
        }

        /// <summary>
        /// Handles the mouse button down event on the Description field to display property attributes.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event args containing mouse button data.</param>
        private void Description_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.Description), Element);
        }

        /// <summary>
        /// Handles the mouse button down event on the CreationDate field to display property attributes.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event args containing mouse button data.</param>
        private void CreationDate_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.CreationDate), Element);
        }

        /// <summary>
        /// Handles the mouse button down event on the LastModified field to display property attributes.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event args containing mouse button data.</param>
        private void LastModified_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.LastModified), Element);
        }

        /// <summary>
        /// Handles the mouse button down event on the UnitLabel field to display property attributes.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event args containing mouse button data.</param>
        private void UnitLabel_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.UnitLabel), Element);
        }

        /// <summary>
        /// Handles the mouse button down event on the IndexLabel field to display property attributes.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event args containing mouse button data.</param>
        private void IndexLabel_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.IndexLabel), Element);
        }

        /// <summary>
        /// Handles the mouse button down event on the ExactDataMethod field to display property attributes.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event args containing mouse button data.</param>
        private void ExactDataMethod_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.ExactDataMethod), Element);
        }

        /// <summary>
        /// Handles the mouse button down event on the USGSSiteNumber field to display property attributes.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event args containing mouse button data.</param>
        private void USGSSiteNumber_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.USGSSiteNumber), Element);
        }

        /// <summary>
        /// Handles the mouse button down event on the TimeSeriesElement field to display property attributes.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event args containing mouse button data.</param>
        private void TimeSeriesElement_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.TimeSeriesElement), Element);
        }

        /// <summary>
        /// Handles the mouse button down event on the BlockFunction field to display property attributes.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event args containing mouse button data.</param>
        private void BlockFunction_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.BlockFunction), Element);
        }

        /// <summary>
        /// Handles the mouse button down event on the TimeBlock field to display property attributes.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event args containing mouse button data.</param>
        private void TimeBlock_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.TimeBlock), Element);
        }

        /// <summary>
        /// Handles the mouse button down event on the StartMonth field to display property attributes.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event args containing mouse button data.</param>
        private void StartMonth_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.StartMonth), Element);
        }

        /// <summary>
        /// Handles the mouse button down event on the EndMonth field to display property attributes.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event args containing mouse button data.</param>
        private void EndMonth_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.EndMonth), Element);
        }

        /// <summary>
        /// Handles the mouse button down event on the SmoothingFunction field to display property attributes.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event args containing mouse button data.</param>
        private void SmoothingFunction_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.SmoothingFunction), Element);
        }

        /// <summary>
        /// Handles the mouse button down event on the Period field to display property attributes.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event args containing mouse button data.</param>
        private void Period_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.Period), Element);
        }

        /// <summary>
        /// Handles the mouse button down event on the POTThresholdValue field to display property attributes.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event args containing mouse button data.</param>
        private void POTThresholdValue_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.Threshold), Element);
        }

        /// <summary>
        /// Handles the mouse button down event on the MinStepsBetweenPeaks field to display property attributes.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event args containing mouse button data.</param>
        private void MinStepsBetweenPeaks_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.MinStepsBetweenPeaks), Element);
        }

        /// <summary>
        /// Handles the mouse button down event on the PlottingParameter field to display property attributes.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event args containing mouse button data.</param>
        private void PlottingParameter_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.DataFrame.PlottingParameter), Element.DataFrame);
        }

        /// <summary>
        /// Handles the mouse button down event on the MGBTButton field to display property attributes.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event args containing mouse button data.</param>
        private void MGBTButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.UseMultipleGrubbsBeckTest), Element);
        }

        /// <summary>
        /// Handles the mouse button down event on the LOThresholdValue field to display property attributes.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event args containing mouse button data.</param>
        private void LOThresholdValue_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PropertyAttributes.GetPropertyAttributes(nameof(Element.DataFrame.LowOutlierThreshold), Element.DataFrame);
        }

        /// <summary>
        /// Handles the GotFocus event on the Name field, storing the previous value and loading existing names for validation.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event args containing routed event data.</param>
        private void Name_GotFocus(object sender, RoutedEventArgs e)
        {
            _previousName = Element.Name;
            ExistingNames = Element.ParentCollection.GetElementNames(Element).ToArray();
        }

        /// <summary>
        /// Handles the LostFocus event on the Name field, restoring the previous value if validation fails.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event args containing routed event data.</param>
        private void Name_LostFocus(object sender, RoutedEventArgs e)
        {
            if (ElementName.NameTextBox.IsValid) return;
            if (Element == null) return;
            Element.Name = _previousName;
        }

        /// <summary>
        /// Loads available time series elements from the parent project's element collections.
        /// Subscribes to element added and removed events for dynamic updates.
        /// </summary>
        private void LoadTimeSeriesData()
        {
            TimeSeriesElements.Clear();
            foreach (IElementCollection collection in Element.ParentCollection.ParentProject.ElementCollections)
            {
                if (collection is TimeSeriesCollection tsc)
                {
                    _subscribedTimeSeriesCollection = tsc;
                    tsc.ElementAdded += OnTimeSeriesElementAdded;
                    tsc.ElementRemoved += OnTimeSeriesElementRemoved;
                    foreach (IElement element in collection)
                        TimeSeriesElements.Add((TimeSeriesElement)element);
                    break;
                }
            }
        }

        /// <summary>
        /// Unsubscribes from the currently tracked <see cref="TimeSeriesCollection"/> events.
        /// Called before subscribing to a new collection and during unload to prevent memory leaks.
        /// </summary>
        private void UnsubscribeTimeSeriesCollection()
        {
            if (_subscribedTimeSeriesCollection != null)
            {
                _subscribedTimeSeriesCollection.ElementAdded -= OnTimeSeriesElementAdded;
                _subscribedTimeSeriesCollection.ElementRemoved -= OnTimeSeriesElementRemoved;
                _subscribedTimeSeriesCollection = null;
            }
        }

        /// <summary>
        /// Handles the addition of a time series element to the subscribed collection.
        /// </summary>
        /// <param name="element">The element that was added.</param>
        private void OnTimeSeriesElementAdded(IElement element) => TimeSeriesElements.Add((TimeSeriesElement)element);

        /// <summary>
        /// Handles the removal of a time series element from the subscribed collection.
        /// </summary>
        /// <param name="element">The element that was removed.</param>
        private void OnTimeSeriesElementRemoved(IElement element) => TimeSeriesElements.Remove((TimeSeriesElement)element);

        /// <summary>
        /// Handles the Unloaded event for the control. Unsubscribes from time series collection
        /// events to prevent memory leaks when the control is removed from the visual tree.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event args containing routed event data.</param>
        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            UnsubscribeTimeSeriesCollection();
        }

        /// <summary>
        /// Handles the selection changed event for the exact data method combo box.
        /// Updates UI element visibility based on the selected data entry method.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event args containing selection changed data.</param>
        private void ExactDateMethodComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Element == null) return;
            if (Element.ExactDataMethod == InputData.ExactDataEntryType.Manual)
            {
                USGSSiteNumber.Visibility = Visibility.Collapsed;
                TimeSeriesElement.Visibility = Visibility.Collapsed;
                BlockFunction.Visibility = Visibility.Collapsed;
                TimeBlock.Visibility = Visibility.Collapsed;
                SmoothingFunction.Visibility = Visibility.Collapsed;
                Period.Visibility = Visibility.Collapsed;
                POTThresholdValue.Visibility = Visibility.Collapsed;
                MinStepsBetweenPeaks.Visibility = Visibility.Collapsed;
                ProcessDataButton.Visibility = Visibility.Collapsed;
                ProcessDataButton.ToolTip = "Click to process the time series data.";
                ProcessDataButtonText.Text = "Process";
            }
            else if (Element.ExactDataMethod == InputData.ExactDataEntryType.BlockSeries)
            {
                USGSSiteNumber.Visibility = Visibility.Collapsed;
                TimeSeriesElement.Visibility = Visibility.Visible;
                BlockFunction.Visibility = Visibility.Visible;
                TimeBlock.Visibility = Visibility.Visible;
                POTThresholdValue.Visibility = Visibility.Collapsed;
                MinStepsBetweenPeaks.Visibility = Visibility.Collapsed;
                SmoothingFunction.Visibility = Visibility.Visible;
                Period.Visibility = Element.SmoothingFunction == SmoothingFunctionType.None ? Visibility.Collapsed : Visibility.Visible;
                ProcessDataButton.Visibility = Visibility.Visible;
                ProcessDataButton.ToolTip = "Click to process the time series data.";
                ProcessDataButtonText.Text = "Process";
            }
            else if (Element.ExactDataMethod == InputData.ExactDataEntryType.PeaksOverThresholdSeries)
            {
                USGSSiteNumber.Visibility = Visibility.Collapsed;
                TimeSeriesElement.Visibility = Visibility.Visible;
                BlockFunction.Visibility = Visibility.Collapsed;
                TimeBlock.Visibility = Visibility.Collapsed;
                POTThresholdValue.Visibility = Visibility.Visible;
                MinStepsBetweenPeaks.Visibility = Visibility.Visible;
                SmoothingFunction.Visibility = Visibility.Visible;
                Period.Visibility = Element.SmoothingFunction == SmoothingFunctionType.None ? Visibility.Collapsed : Visibility.Visible;
                ProcessDataButton.Visibility = Visibility.Visible;
                ProcessDataButton.ToolTip = "Click to process the time series data.";
                ProcessDataButtonText.Text = "Process";
            }
            else if (Element.ExactDataMethod == InputData.ExactDataEntryType.USGSPeakDischarge ||
                Element.ExactDataMethod == InputData.ExactDataEntryType.USGSPeakStage)
            {
                USGSSiteNumber.Visibility = Visibility.Visible;
                TimeSeriesElement.Visibility = Visibility.Collapsed;
                BlockFunction.Visibility = Visibility.Collapsed;
                TimeBlock.Visibility = Visibility.Collapsed;
                SmoothingFunction.Visibility = Visibility.Collapsed;
                Period.Visibility = Visibility.Collapsed;
                POTThresholdValue.Visibility = Visibility.Collapsed;
                MinStepsBetweenPeaks.Visibility = Visibility.Collapsed;
                ProcessDataButton.Visibility = Visibility.Visible;
                ProcessDataButton.ToolTip = "Click to download the time series data.";
                ProcessDataButtonText.Text = "Download";
            }
            UpdateTimeBlockVisibility();

        }

        /// <summary>
        /// Handles the selection changed event for the time block combo box.
        /// Updates month selection visibility based on the selected time block window.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event args containing selection changed data.</param>
        private void TimeBlockComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Element == null) return;
            UpdateTimeBlockVisibility();
        }

        /// <summary>
        /// Updates the visibility of StartMonth and EndMonth controls based on the current
        /// exact data method and time block window settings. Called from both
        /// <see cref="ExactDateMethodComboBox_SelectionChanged"/> and <see cref="TimeBlockComboBox_SelectionChanged"/>
        /// to avoid duplicating the visibility logic.
        /// </summary>
        private void UpdateTimeBlockVisibility()
        {
            if (Element.ExactDataMethod == InputData.ExactDataEntryType.BlockSeries)
            {
                StartMonth.Visibility = Element.TimeBlock == TimeBlockWindow.CalendarYear ? Visibility.Collapsed : Visibility.Visible;
                EndMonth.Visibility = Element.TimeBlock == TimeBlockWindow.CustomYear ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                StartMonth.Visibility = Visibility.Collapsed;
                EndMonth.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Handles the selection changed event for the time series element combo box.
        /// Updates border and tooltip based on whether a valid time series is selected.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event args containing selection changed data.</param>
        private void TimeSeriesElementComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Element == null) return;
            if (Element.TimeSeriesElement == null || Element.TimeSeriesElement.Name == null)
            {
                ((Border)TimeSeriesElement.InnerContent).BorderThickness = new Thickness(1);
                TimeSeriesElement.ToolTip = "Select a valid time series.";
            }
            else
            {
                ((Border)TimeSeriesElement.InnerContent).BorderThickness = new Thickness(0);
                TimeSeriesElement.ToolTip = null;
            }
        }

        /// <summary>
        /// Handles the selection changed event for the smoothing function combo box.
        /// Shows or hides the period field based on whether a smoothing function is selected.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event args containing selection changed data.</param>
        private void SmoothingFunction_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Element == null) return;
            if (Element.SmoothingFunction == SmoothingFunctionType.None)
            {
                Period.Visibility = Visibility.Collapsed;
            }
            else
            {
                Period.Visibility = Visibility.Visible;
            }

        }

        /// <summary>
        /// Handles the click event for the process data button.
        /// Processes time series data based on the selected exact data entry method.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event args containing routed event data.</param>
        private async void ProcessDataButton_Click(object sender, RoutedEventArgs e)
        {
            if (Element == null || Element.DataFrame == null) return;
            if (Element.IsTimeSeriesInputValid() == false)
            {
                GenericControls.MessageBox.Show("Unable to process time series data due to invalid input.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            Element.ClearTimeSeriesResults();
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                if (Element.ExactDataMethod == InputData.ExactDataEntryType.BlockSeries)
                {
                    Element.CreateBlockSeries();
                }
                else if (Element.ExactDataMethod == InputData.ExactDataEntryType.PeaksOverThresholdSeries)
                {
                    Element.CreatePeaksOverThresholdSeries();
                }
                else if (Element.ExactDataMethod == InputData.ExactDataEntryType.USGSPeakDischarge ||
                         Element.ExactDataMethod == InputData.ExactDataEntryType.USGSPeakStage)
                {
                    using (var cts = new CancellationTokenSource(ExternalDownloadTimeout))
                    {
                        await Element.CreateFromUSGS(cts.Token);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                GenericControls.MessageBox.Show(
                    $"The download did not complete within {ExternalDownloadTimeout.TotalSeconds:0} seconds. " +
                    "The USGS service may be blocked or unavailable from this network.",
                    "Download timed out",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }
            catch(Exception ex)
            {
                GenericControls.MessageBox.Show("An error occurred while processing the time series. " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }

        }

        /// <summary>
        /// Handles the click event for the run test button.
        /// Executes the Multiple Grubbs-Beck Test or threshold-based test to identify low outliers.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event args containing routed event data.</param>
        private void RunTestButton_Click(object sender, RoutedEventArgs e)
        {
            if (Element == null || Element.DataFrame == null || Element.DataFrame.ExactSeries == null)
                return;

            if (!Element.IsValid)
            {
                GenericControls.MessageBox.Show("Unable to perform the low outlier test because the input data is invalid.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                if (MGBTButton.IsSelected)
                {
                    Element.DataFrame.SetLowOutliersFromMGBT();
                    GenericControls.MessageBox.Show($"The Multiple Grubbs-Beck Test identified {Element.DataFrame.NumberOfLowOutliers} low outlier(s).",
                                    "Low Outlier Test Complete", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                }
                else
                {
                    Element.DataFrame.SetLowOutliersFromThreshold();
                    GenericControls.MessageBox.Show($"The user-defined threshold identified {Element.DataFrame.NumberOfLowOutliers} low outlier(s).",
                                    "Low Outlier Test Complete", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                }
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }

        }

        /// <summary>
        /// Handles the loaded event for the combo box.
        /// Configures the item source with sorted time series elements.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event args containing routed event data.</param>
        private void ComboBox_Loaded(object sender, RoutedEventArgs e)
        {
            if (Element == null) return;
            ComboBox cmbo = (ComboBox)sender;
            CollectionViewSource csv = new CollectionViewSource() { Source = TimeSeriesElements, IsLiveSortingRequested = true };
            csv.SortDescriptions.Add(new SortDescription(nameof(IElement.Name), ListSortDirection.Ascending));
            var view = csv.View;
            view.MoveCurrentToPosition(-1);
            cmbo.ItemsSource = view;
        }
    }
}
