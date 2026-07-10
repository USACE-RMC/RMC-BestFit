using RMC.BestFit.Models;
using RMC.BestFit.Estimation;
using RMC.BestFit.UI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
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
    /// User control for displaying parameter sets (posterior samples) from a Bayesian analysis in a tabular format.
    /// Provides a data grid view showing all sampled parameter values and their associated log-likelihood values.
    /// </summary>
    public partial class ParameterSetsControl : UserControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ParameterSetsControl"/> class.
        /// </summary>
        public ParameterSetsControl()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Dependency property for the <see cref="Analysis"/> property.
        /// </summary>
        public static DependencyProperty AnalysisProperty = DependencyProperty.Register(nameof(Analysis), typeof(BayesianAnalysis), typeof(ParameterSetsControl), new PropertyMetadata(null, ElementCallback));

        /// <summary>
        /// Gets or sets the Bayesian analysis instance containing the parameter sets to be displayed.
        /// </summary>
        public BayesianAnalysis Analysis
        {
            get { return (BayesianAnalysis)GetValue(AnalysisProperty); }
            set { SetValue(AnalysisProperty, value); }
        }

        /// <summary>
        /// Callback method invoked when the Analysis dependency property changes.
        /// Subscribes to property changed events on the new analysis instance.
        /// </summary>
        /// <param name="d">The dependency object that owns the property.</param>
        /// <param name="e">Event data containing the old and new property values.</param>
        private static void ElementCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d == null) return;
            if (d as ParameterSetsControl == null) return;
            var thisControl = (ParameterSetsControl)d;

            if (e.NewValue == null) return;
            var newElement = e.NewValue as BayesianAnalysis;
            if (newElement == null) return;

            newElement.PropertyChanged += thisControl.Analysis_PropertyChanged;

        }

        /// <summary>
        /// Gets or sets a value indicating whether the log-likelihood column should be displayed in the parameter sets table.
        /// Default is true.
        /// </summary>
        public bool ShowLikelihoodColumn { get; set; } = true;

        /// <summary>
        /// Flag indicating whether the control has been loaded.
        /// </summary>
        private bool _isLoaded = false;

        /// <summary>
        /// Event handler for when the user control is loaded. Initializes the parameter set data grid.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event data.</param>
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (_isLoaded == false)
            {
                BindParameterSetDataGrid();
            }
            _isLoaded = true;
        }

        /// <summary>
        /// Event handler for property changes in the Analysis object. Updates the data grid when the estimation status changes.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event data containing the property name that changed.</param>
        private void Analysis_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Analysis.IsEstimated))
            {
                BindParameterSetDataGrid();
            }
        }

        /// <summary>
        /// Binds the parameter set data to the data grid viewer.
        /// Creates a data table containing all parameter values and optional log-likelihood values from the analysis results.
        /// </summary>
        private void BindParameterSetDataGrid()
        {
            ParameterSetTableViewer.DataView = null;
            if (Analysis == null || Analysis.Results == null) return;

            if (Analysis.IsEstimated == true)
            {
                var names = Analysis.ParameterNames;
                int numParams = Analysis.NumberOfEstimatedParameters;
                if (names == null || numParams == 0) return;

                System.Data.DataTable dt = new System.Data.DataTable("ParameterSetTable");

                for (int i = 0; i < numParams; i++)
                {
                    dt.Columns.Add(new System.Data.DataColumn(names[i], typeof(double)));
                }
                if (ShowLikelihoodColumn)
                    dt.Columns.Add(new System.Data.DataColumn("Log-Likelihood", typeof(double)));

                foreach (var pSet in Analysis.Results.Output)
                {
                    var data = new object[ShowLikelihoodColumn ? numParams + 1 : numParams];
                    for (int i = 0; i < numParams; i++)
                        data[i] = pSet.Values != null ? pSet.Values[i] : 0;

                    if (ShowLikelihoodColumn)
                        data[numParams] = pSet.Fitness;

                    dt.Rows.Add(data);
                }

                ParameterSetTableViewer.DataView = new DatabaseManager.InMemoryReader(dt).GetTableManager(dt.TableName);
            }

        }
    }
}
