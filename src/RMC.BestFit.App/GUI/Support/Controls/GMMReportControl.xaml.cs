using RMC.BestFit.UI;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace RMC_BestFit
{
    /// <summary>
    /// User control for displaying a comprehensive Bulletin 17C GMM estimation report.
    /// Shows estimation configuration, GMM fit summary, parameter estimates, uncertainty diagnostics,
    /// covariance/correlation matrices, penalty configuration, and identification status.
    /// </summary>
    public partial class GMMReportControl : UserControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GMMReportControl"/> class.
        /// </summary>
        public GMMReportControl()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Dependency property for the <see cref="Element"/> property.
        /// </summary>
        public static DependencyProperty ElementProperty = DependencyProperty.Register(
            nameof(Element), typeof(B17CAnalysis), typeof(GMMReportControl),
            new PropertyMetadata(null, ElementCallback));

        /// <summary>
        /// Gets or sets the B17C analysis element to generate the report from.
        /// </summary>
        public B17CAnalysis Element
        {
            get { return (B17CAnalysis)GetValue(ElementProperty); }
            set { SetValue(ElementProperty, value); }
        }

        /// <summary>
        /// Handles changes to the <see cref="Element"/> dependency property.
        /// Subscribes to PropertyChanged events and updates the report.
        /// </summary>
        private static void ElementCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as GMMReportControl;
            if (control == null) return;

            if (e.OldValue is B17CAnalysis oldElement)
                oldElement.PropertyChanged -= control.Element_PropertyChanged;

            if (e.NewValue is B17CAnalysis newElement)
            {
                newElement.PropertyChanged += control.Element_PropertyChanged;
                control.UpdateReport();
            }
            else
            {
                control.ClearReport();
            }
        }

        /// <summary>
        /// Handles PropertyChanged events from the B17C analysis element.
        /// Updates the report when estimation completes, the GMM object changes, or cached report text changes.
        /// </summary>
        private void Element_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(B17CAnalysis.IsEstimated) ||
                e.PropertyName == nameof(B17CAnalysis.GMM) ||
                e.PropertyName == nameof(B17CAnalysis.GMMReport))
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
        /// Generates and displays the full GMM estimation report by delegating to the model layer.
        /// </summary>
        private void UpdateReport()
        {
            if (Element == null || !Element.IsEstimated)
            {
                ClearReport();
                return;
            }

            // Use live GMM report if available (has Optimizer from a fresh run);
            // fall back to cached report from disk (Optimizer is null after RestoreFromXElement).
            string reportText = Element.GMM?.Optimizer != null ? Element.GenerateGMMReport() : Element.GMMReport;
            if (string.IsNullOrEmpty(reportText))
            {
                ClearReport();
                return;
            }

            // Build FlowDocument
            var doc = new FlowDocument();
            doc.FontFamily = new System.Windows.Media.FontFamily("Consolas");
            doc.FontSize = 12;
            doc.PageWidth = 1200;

            var paragraph = new Paragraph();
            paragraph.Inlines.Add(new Run(reportText));
            doc.Blocks.Add(paragraph);

            ReportTextBox.Document = doc;
        }
    }
}
