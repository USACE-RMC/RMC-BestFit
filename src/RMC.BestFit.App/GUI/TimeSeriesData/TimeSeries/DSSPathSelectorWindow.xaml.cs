using GenericControls;
using Hec.Dss;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;


namespace RMC_BestFit
{
    /// <summary>
    /// Dialog window for selecting a data pathname from a HEC-DSS (Hydrologic Engineering Center Data Storage System) file.
    /// Provides filtering capabilities by DSS path parts (A through F) and displays available pathnames in a data grid.
    /// </summary>
    public partial class DSSPathSelectorWindow : MetroWindow
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DSSPathSelectorWindow"/> class.
        /// </summary>
        public DSSPathSelectorWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Collection of DSS paths loaded from the DSS file.
        /// </summary>
        private DssPathCollection _pathCollection;

        /// <summary>
        /// Number of filtered rows that may be resolved automatically without user selection.
        /// </summary>
        private const int AutoResolveDisplayRangeLimit = 25;

        /// <summary>
        /// List of DSS selector rows filtered by the selected path part criteria.
        /// </summary>
        private List<DssPathSelectorRow> _filteredPathRows = new List<DssPathSelectorRow>();

        /// <summary>
        /// Resolver used to read and cache actual DSS data ranges for display.
        /// </summary>
        private DssPathRangeResolver _rangeResolver;

        /// <summary>
        /// List of unique A-part values (typically watershed or project name) from the DSS file.
        /// </summary>
        private List<string> _Aparts = new List<string>();

        /// <summary>
        /// List of unique B-part values (typically location) from the DSS file.
        /// </summary>
        private List<string> _Bparts = new List<string>();

        /// <summary>
        /// List of unique C-part values (typically parameter) from the DSS file.
        /// </summary>
        private List<string> _Cparts = new List<string>();

        /// <summary>
        /// List of unique D-part values (typically date or time range) from the DSS file.
        /// </summary>
        private List<string> _Dparts = new List<string>();

        /// <summary>
        /// List of unique E-part values (typically time interval) from the DSS file.
        /// </summary>
        private List<string> _Eparts = new List<string>();

        /// <summary>
        /// List of unique F-part values (typically version) from the DSS file.
        /// </summary>
        private List<string> _Fparts = new List<string>();

        /// <summary>
        /// Dependency property for the DSS File Name.
        /// </summary>
        public static DependencyProperty FullFileNameProperty = DependencyProperty.Register(nameof(FullFileName), typeof(string), typeof(DSSPathSelectorWindow), new PropertyMetadata(null, FileNameCallback));

        /// <summary>
        /// Gets or sets the full file path to the DSS file from which pathnames will be loaded.
        /// </summary>
        public string FullFileName
        {
            get { return (string)this.GetValue(FullFileNameProperty); }
            set { this.SetValue(FullFileNameProperty, value); }
        }

        /// <summary>
        /// Dependency property for the selected pathname.
        /// </summary>
        public static DependencyProperty SelectedPathnameProperty = DependencyProperty.Register(nameof(SelectedPathname), typeof(string), typeof(DSSPathSelectorWindow), new PropertyMetadata(null));

        /// <summary>
        /// Gets or sets the pathname selected by the user from the available DSS paths.
        /// </summary>
        public string SelectedPathname
        {
            get { return (string)this.GetValue(SelectedPathnameProperty); }
            set { this.SetValue(SelectedPathnameProperty, value); }
        }

        /// <summary>
        /// Callback method invoked when the FullFileName property changes to load pathnames from the new file.
        /// </summary>
        /// <param name="d">The dependency object whose property changed.</param>
        /// <param name="e">Event arguments containing the old and new values.</param>
        private static void FileNameCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not DSSPathSelectorWindow thisControl) return;

            if (e.NewValue == null) return;
            string newFileName = e.NewValue as string;
            if (newFileName == null) return;

            thisControl.FileNameTextBox.Text = newFileName;
            thisControl.LoadPathnames();

        }

        /// <summary>
        /// Loads all pathnames from the DSS file and populates the filter combo boxes with unique path parts.
        /// Shows a user-friendly error message if the file cannot be read (e.g., unsupported DSS version).
        /// </summary>
        private void LoadPathnames()
        {
            if (File.Exists(FullFileName))
            {
                try
                {
                    using (DssReader dssReader = new DssReader(FullFileName))
                    {
                        _pathCollection = dssReader.GetCatalog();
                    }
                    _rangeResolver = new DssPathRangeResolver(ReadTimeSeriesForDisplayRange);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"DSSPathSelectorWindow.LoadPathnames: {ex.Message}");
                    GenericControls.MessageBox.Show(
                        $"Unable to open DSS file.{Environment.NewLine}{Environment.NewLine}{ex.Message}",
                        "HEC-DSS Import Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }
            }

            if (_pathCollection == null) return;

            ShowFilteredPathnames(_pathCollection.FilterByPart());

            _Aparts = _pathCollection.Select(x => x.Apart).Distinct().ToList();
            _Bparts = _pathCollection.Select(x => x.Bpart).Distinct().ToList();
            _Cparts = _pathCollection.Select(x => x.Cpart).Distinct().ToList();
            _Dparts = _pathCollection.Select(x => x.Dpart).Distinct().ToList();
            _Eparts = _pathCollection.Select(x => x.Epart).Distinct().ToList();
            _Fparts = _pathCollection.Select(x => x.Fpart).Distinct().ToList();
            _Aparts.Insert(0, "");
            _Bparts.Insert(0, "");
            _Cparts.Insert(0, "");
            _Dparts.Insert(0, "");
            _Eparts.Insert(0, "");
            _Fparts.Insert(0, "");

            APartComboBox.ItemsSource = _Aparts;
            BPartComboBox.ItemsSource = _Bparts;
            CPartComboBox.ItemsSource = _Cparts;
            DPartComboBox.ItemsSource = _Dparts;
            EPartComboBox.ItemsSource = _Eparts;
            FPartComboBox.ItemsSource = _Fparts;
        }

        /// <summary>
        /// Filters the displayed pathnames based on the currently selected values in the path part combo boxes.
        /// </summary>
        private void FilterPathnames()
        {
            if (_pathCollection == null) return;

            string a = APartComboBox.SelectedValue == null ? "" : APartComboBox.SelectedValue as string;
            string b = BPartComboBox.SelectedValue == null ? "" : BPartComboBox.SelectedValue as string;
            string c = CPartComboBox.SelectedValue == null ? "" : CPartComboBox.SelectedValue as string;
            string d = DPartComboBox.SelectedValue == null ? "" : DPartComboBox.SelectedValue as string;
            string e = EPartComboBox.SelectedValue == null ? "" : EPartComboBox.SelectedValue as string;
            string f = FPartComboBox.SelectedValue == null ? "" : FPartComboBox.SelectedValue as string;
            ShowFilteredPathnames(_pathCollection.FilterByPart(a, b, c, d, e, f));
        }

        /// <summary>
        /// Shows filtered catalog paths as selector rows and optionally resolves their real data ranges.
        /// </summary>
        /// <param name="pathnames">The filtered DSS catalog paths.</param>
        private void ShowFilteredPathnames(List<DssPath> pathnames)
        {
            _filteredPathRows = CreateSelectorRows(pathnames);
            BindSelectorRows();
        }

        /// <summary>
        /// Creates selector rows for the current filtered catalog paths.
        /// </summary>
        /// <param name="pathnames">The filtered DSS catalog paths.</param>
        /// <returns>The selector rows to bind to the grid.</returns>
        /// <remarks>
        /// Small filtered sets are resolved before binding so the user never sees stale catalog
        /// D-parts for the common single-record selector case.
        /// </remarks>
        private List<DssPathSelectorRow> CreateSelectorRows(List<DssPath> pathnames)
        {
            bool resolveBeforeBinding = pathnames.Count <= AutoResolveDisplayRangeLimit;
            return pathnames
                .Select(path =>
                {
                    var row = new DssPathSelectorRow(path);
                    return resolveBeforeBinding ? ResolveSelectorRow(row) : row;
                })
                .ToList();
        }

        /// <summary>
        /// Binds the current selector rows to the grid.
        /// </summary>
        private void BindSelectorRows()
        {
            MyDataGrid.ItemsSource = null;
            MyDataGrid.ItemsSource = _filteredPathRows;
            MyDataGrid.Items.Refresh();
        }

        /// <summary>
        /// Reads a DSS time series for selector display-range resolution.
        /// </summary>
        /// <param name="displayReadPath">The concrete DSS pathname used to read the record.</param>
        /// <returns>The DSS time series returned by the reader.</returns>
        private Hec.Dss.TimeSeries ReadTimeSeriesForDisplayRange(DssPath displayReadPath)
        {
            using (DssReader dssReader = new DssReader(FullFileName))
            {
                return dssReader.GetTimeSeries(displayReadPath);
            }
        }

        /// <summary>
        /// Updates the selected pathname preview from a selector row.
        /// </summary>
        /// <param name="row">The selected row.</param>
        private void UpdateSelectedPathPreview(DssPathSelectorRow row)
        {
            PathNameTextBox.Text = row.DisplayPath;
            PathNameTextBox.ToolTip = row.RangeResolutionMessage;
        }

        /// <summary>
        /// Resolves the displayed data range for one selector row.
        /// </summary>
        /// <param name="row">The row to resolve.</param>
        /// <returns>A selector row containing resolved display state.</returns>
        private DssPathSelectorRow ResolveSelectorRow(DssPathSelectorRow row)
        {
            if (row == null || row.IsRangeResolved || _rangeResolver == null) return row;

            DssPathRangeResult result = _rangeResolver.Resolve(row.CatalogPath);
            return row.WithRangeResult(result);
        }

        /// <summary>
        /// Replaces a row in the current grid source and preserves the user's selection.
        /// </summary>
        /// <param name="oldRow">The currently bound row.</param>
        /// <param name="newRow">The replacement row.</param>
        private void ReplaceSelectorRow(DssPathSelectorRow oldRow, DssPathSelectorRow newRow)
        {
            if (oldRow == null || newRow == null || ReferenceEquals(oldRow, newRow)) return;

            int rowIndex = _filteredPathRows.IndexOf(oldRow);
            if (rowIndex < 0) return;

            bool wasSelected = MyDataGrid.SelectedItem == oldRow;
            _filteredPathRows[rowIndex] = newRow;
            BindSelectorRows();
            if (wasSelected)
            {
                MyDataGrid.SelectedItem = newRow;
            }
        }

        /// <summary>
        /// Handles the SelectionChanged event of the path part combo boxes to update the filtered pathname list.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            FilterPathnames();
        }

        /// <summary>
        /// Handles the SelectionChanged event of the data grid to update the selected pathname text box and enable the set button.
        /// Shows the full path (including date range) in the text box for display purposes.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void MyDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MyDataGrid.SelectedItem == null)
            {
                PathNameTextBox.Text = "";
                PathNameTextBox.ToolTip = null;
                SetPathButton.IsEnabled = false;
                return;
            }

            var row = (DssPathSelectorRow)MyDataGrid.SelectedItem;
            var resolvedRow = ResolveSelectorRow(row);
            ReplaceSelectorRow(row, resolvedRow);
            row = resolvedRow;
            UpdateSelectedPathPreview(row);
            SetPathButton.IsEnabled = true;
        }

        /// <summary>
        /// Gets the dateless pathname from the selected catalog entry for use with <see cref="Hec.Dss.DssReader.GetTimeSeries(DssPath, DateTime, DateTime, TimeWindow.TimeWindowBehavior, TimeWindow.ConsecutiveValueCompression)"/>.
        /// </summary>
        /// <returns>The DSS pathname with an empty D-part (e.g., /A/B/C//E/F/), or an empty string if nothing is selected.</returns>
        /// <remarks>
        /// The catalog returns condensed paths where the D-part is a date range (e.g., "01JAN2000-31DEC2020").
        /// This condensed D-part is incompatible with the native <c>hec_dss_tsGetDateTimeRange</c> function,
        /// which expects individual record dates. Using <see cref="DssPath.PathWithoutDate"/> (empty D-part)
        /// allows <c>GetTimeSeries</c> to auto-discover the full date range from the native library.
        /// </remarks>
        private string GetCorrectPathName()
        {
            if (MyDataGrid.SelectedItem == null) return "";
            var row = (DssPathSelectorRow)MyDataGrid.SelectedItem;
            return row.DatelessPath;
        }

        /// <summary>
        /// Handles the Click event of the Set Path button to confirm the selected pathname and close the dialog.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void SetPathButton_Click(object sender, RoutedEventArgs e)
        {
            if (MyDataGrid.SelectedItem == null) return;
            SelectedPathname = GetCorrectPathName();
            this.DialogResult = true;
            this.Close();
        }

        /// <summary>
        /// Handles the Click event of the Cancel button to close the dialog without selecting a pathname.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }

}
