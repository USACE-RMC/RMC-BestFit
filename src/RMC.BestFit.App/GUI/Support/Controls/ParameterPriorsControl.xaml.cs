using NumericControls.Distributions.Univariate;
using Numerics.Distributions;
using RMC.BestFit.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
    /// User control for configuring and managing prior probability distributions for model parameters in Bayesian analysis.
    /// Provides an interactive interface for selecting and editing prior distributions for each parameter.
    /// </summary>
    public partial class ParameterPriorsControl : UserControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ParameterPriorsControl"/> class.
        /// </summary>
        public ParameterPriorsControl()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Raised when the user requests to view property attributes for a model property.
        /// </summary>
        public event ShowPropertyAttributesEventHandler ShowPropertyAttributes;

        /// <summary>
        /// Delegate for the <see cref="ShowPropertyAttributes"/> event.
        /// </summary>
        /// <param name="propertyName">The name of the property whose attributes are requested.</param>
        /// <param name="classObject">The object instance containing the property.</param>
        public delegate void ShowPropertyAttributesEventHandler(string propertyName, object classObject);

        /// <summary>
        /// Dependency property for the <see cref="Model"/> property.
        /// </summary>
        public static DependencyProperty ModelProperty = DependencyProperty.Register(nameof(Model), typeof(IModel), typeof(ParameterPriorsControl), new PropertyMetadata(null, ModelCallback));

        /// <summary>
        /// Gets or sets the statistical model whose parameter priors are being configured.
        /// </summary>
        public IModel Model
        {
            get { return (IModel)GetValue(ModelProperty); }
            set { SetValue(ModelProperty, value); }
        }

        /// <summary>
        /// Callback method invoked when the Model dependency property changes.
        /// Subscribes to property changed events on the new model and updates the data grid.
        /// </summary>
        /// <param name="d">The dependency object that owns the property.</param>
        /// <param name="e">Event data containing the old and new property values.</param>
        private static void ModelCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d == null) return;
            if (d as ParameterPriorsControl == null) return;
            var thisControl = (ParameterPriorsControl)d;

            // Unsubscribe from old model to prevent memory leaks and stale event handlers
            if (e.OldValue is IModel oldModel)
                oldModel.PropertyChanged -= thisControl.Model_PropertyChanged;

            if (e.NewValue == null) return;
            var newElement = e.NewValue as IModel;
            if (newElement == null) return;

            newElement.PropertyChanged += thisControl.Model_PropertyChanged;
            thisControl.UpdateDataGrid();
        }

        /// <summary>
        /// Event handler for property changes in the Model object. Updates the control when model parameters change.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event data containing the property name that changed.</param>
        private void Model_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Model.Parameters) || e.PropertyName == nameof(Model.SetDefaultParameters))
            {
                UpdateDataGrid();
            }
        }

        /// <summary>
        /// Event handler for mouse down events on the "Use Prior Distribution Defaults" control.
        /// Raises the ShowPropertyAttributes event to display related property information.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event data containing mouse button information.</param>
        private void UsePriorDistributionDefaults_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            ShowPropertyAttributes?.Invoke(nameof(Model.UseDefaultFlatPriors), Model);
        }

        /// <summary>
        /// Event handler for mouse down events on the prior distributions data grid.
        /// Raises the ShowPropertyAttributes event to display parameter information.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event data containing mouse button information.</param>
        private void PriorDistributionsDataGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            ShowPropertyAttributes?.Invoke(nameof(Model.Parameters), Model);
        }

        /// <summary>
        /// Event handler for when the distribution selector popup is opened.
        /// Initializes the distribution selector with available prior distributions and the current parameter's distribution.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event data.</param>
        private void DistributionPopup_Opened(object sender, EventArgs e)
        {
            if (Model == null) return;
            Popup p = (Popup)sender;
            if (p.Child == null) return;
            var b = (Border)p.Child;
            DistributionSelectorControl dsc = (DistributionSelectorControl)b.Child;
            UnivariateDistributionBase dist = null;
            if(p.DataContext.GetType() == typeof(ModelParameter))
            {
                dist = ((ModelParameter)p.DataContext).PriorDistribution.Clone();
            }

            var PriorDistributionsList  = new List<UnivariateDistributionBase>()
            {
              new Exponential(),
              new GammaDistribution(),
              new GeneralizedBeta(),
              new LnNormal(),
              new LogNormal(),
              new Normal(),
              new Pert(),
              new StudentT(),
              new Triangular(),
              new TruncatedNormal(),
              new Uniform()
            };

            dsc.Distributions = PriorDistributionsList;
            dsc.SelectedDistribution = dist;

        }

        /// <summary>
        /// Event handler for when the distribution selector popup is closed.
        /// Applies the selected distribution to the parameter if valid and updates the data grid.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event data.</param>
        private void DistributionPopup_Closed(object sender, EventArgs e)
        {
            if (Model == null) return;
            Popup p = (Popup)sender;
            if (p.Child == null) return;
            var b = (Border)p.Child;
            DistributionSelectorControl dsc = (DistributionSelectorControl)b.Child;
            if (p.DataContext.GetType() == typeof(ModelParameter) && dsc.SelectedDistribution.ParametersValid == true)
            {
                ((ModelParameter)p.DataContext).PriorDistribution = dsc.SelectedDistribution.Clone();
            }
            dsc.SelectedDistribution = null;
            UpdateDataGrid();
        }

        /// <summary>
        /// Updates the data grid with the current model parameters and their prior distributions.
        /// Configures column headers with tooltips and refreshes the display.
        /// </summary>
        private void UpdateDataGrid()
        {
            PriorDistributionsDataGrid.ItemsSource = null;
            if (Model == null || Model.Parameters == null) return;

            PriorDistributionsDataGrid.ItemsSource = Model.Parameters;

            // Parameter Column Header
            var style = new Style(typeof(DataGridColumnHeader), ParameterNameColumn.HeaderStyle);
            style.Setters.Add(new Setter(ToolTipProperty, new TextBlock() { Text = "The model parameter names.", FontWeight = System.Windows.FontWeights.Normal, TextAlignment = TextAlignment.Left, TextWrapping = TextWrapping.Wrap }));
            ParameterNameColumn.HeaderStyle = style;

            // Distribution Column Header
            style = new Style(typeof(DataGridColumnHeader), ParameterDistributionColumn.HeaderStyle);
            style.Setters.Add(new Setter(ToolTipProperty, new TextBlock() { Text = "Set the prior distributions of the model parameters.", FontWeight = System.Windows.FontWeights.Normal, TextAlignment = TextAlignment.Left, TextWrapping = TextWrapping.Wrap }));
            ParameterDistributionColumn.HeaderStyle = style;

            PriorDistributionsDataGrid.Items.Refresh();

        }

    }
}
