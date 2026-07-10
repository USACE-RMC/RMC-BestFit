using RMC.BestFit.Estimation;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace RMC_BestFit
{
    /// <summary>
    /// User control for displaying a comprehensive MCMC simulation report.
    /// Shows simulation configuration, convergence diagnostics, parameter estimates,
    /// covariance/correlation matrices, information criteria, and prior configuration.
    /// </summary>
    public partial class MCMCReportControl : UserControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MCMCReportControl"/> class.
        /// </summary>
        public MCMCReportControl()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Dependency property for the <see cref="Analysis"/> property.
        /// </summary>
        public static DependencyProperty AnalysisProperty = DependencyProperty.Register(
            nameof(Analysis), typeof(BayesianAnalysis), typeof(MCMCReportControl),
            new PropertyMetadata(null, ElementCallback));

        /// <summary>
        /// Gets or sets the Bayesian analysis to generate the report from.
        /// </summary>
        public BayesianAnalysis Analysis
        {
            get { return (BayesianAnalysis)GetValue(AnalysisProperty); }
            set { SetValue(AnalysisProperty, value); }
        }

        /// <summary>
        /// Handles changes to the <see cref="Analysis"/> dependency property.
        /// Subscribes to PropertyChanged events and updates the report.
        /// </summary>
        private static void ElementCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as MCMCReportControl;
            if (control == null) return;

            if (e.OldValue is BayesianAnalysis oldAnalysis)
                oldAnalysis.PropertyChanged -= control.Analysis_PropertyChanged;

            if (e.NewValue is BayesianAnalysis newAnalysis)
            {
                newAnalysis.PropertyChanged += control.Analysis_PropertyChanged;
                control.UpdateReport();
            }
            else
            {
                control.ClearReport();
            }
        }

        /// <summary>
        /// Handles PropertyChanged events from the Bayesian analysis.
        /// Updates the report when estimation completes or results change.
        /// </summary>
        private void Analysis_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(BayesianAnalysis.IsEstimated) ||
                e.PropertyName == nameof(BayesianAnalysis.Results))
            {
                UpdateReport();
            }
        }

        /// <summary>
        /// Clears the report display.
        /// </summary>
        private void ClearReport()
        {
            ReportTextBox.Document = new FlowDocument();
        }

        /// <summary>
        /// Generates and displays the full MCMC simulation report.
        /// </summary>
        private void UpdateReport()
        {
            string report = Analysis?.GenerateReport() ?? string.Empty;
            if (string.IsNullOrEmpty(report))
            {
                ClearReport();
                return;
            }

            DisplayReport(report);
        }

        /// <summary>
        /// Displays report text in the rich text control.
        /// </summary>
        /// <param name="report">The report text to display.</param>
        private void DisplayReport(string report)
        {
            var doc = new FlowDocument();
            doc.FontFamily = new System.Windows.Media.FontFamily("Consolas");
            doc.FontSize = 12;
            doc.PageWidth = 1200;

            var paragraph = new Paragraph();
            paragraph.Inlines.Add(new Run(report));
            doc.Blocks.Add(paragraph);

            ReportTextBox.Document = doc;
        }
    }
}
