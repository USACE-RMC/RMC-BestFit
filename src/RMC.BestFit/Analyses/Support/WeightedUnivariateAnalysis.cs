using System;
using System.ComponentModel;
using System.Globalization;
using System.Xml.Linq;

namespace RMC.BestFit.Analyses
{
    /// <summary>
    /// Represents a univariate analysis paired with a weight for use in composite distributions.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    /// This class is used by <c>CompositeAnalysis</c> to combine multiple univariate
    /// analyses with specified weights for mixture models or model averaging.
    /// The wrapped analysis is typed as <see cref="IUnivariateAnalysis"/> so any sibling
    /// type (<see cref="UnivariateAnalysis"/>, <see cref="Bulletin17CAnalysis"/>, etc.)
    /// can serve as a composite component, EXCEPT another <c>CompositeAnalysis</c>
    /// — that is rejected at the setter to avoid circular references and the
    /// undefined behavior of nesting composites.
    /// </para>
    /// </remarks>
    public class WeightedUnivariateAnalysis : INotifyPropertyChanged
    {
        #region Construction

        /// <summary>
        /// Initializes a new instance of the <see cref="WeightedUnivariateAnalysis"/> class.
        /// </summary>
        public WeightedUnivariateAnalysis()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WeightedUnivariateAnalysis"/> class
        /// with the specified analysis and weight.
        /// </summary>
        /// <param name="analysis">The univariate analysis.</param>
        /// <param name="weight">The weight for this analysis (typically between 0 and 1).</param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="analysis"/> is itself a <c>CompositeAnalysis</c>;
        /// composites cannot be nested.
        /// </exception>
        public WeightedUnivariateAnalysis(IUnivariateAnalysis analysis, double weight)
        {
            UnivariateAnalysis = analysis;
            Weight = weight;
        }

        #endregion

        #region Members

        private double _weight;
        private IUnivariateAnalysis _univariateAnalysis = null!;

        /// <summary>
        /// Occurs when a property value changes.
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Gets or sets the weight for this univariate analysis in the composite.
        /// </summary>
        /// <remarks>
        /// For mixture models, weights should be between 0 and 1 and sum to 1 across all analyses.
        /// For model averaging, weights are computed based on the selected averaging method.
        /// </remarks>
        public double Weight
        {
            get { return _weight; }
            set
            {
                if (_weight != value)
                {
                    _weight = value;
                    RaisePropertyChange(nameof(Weight));
                }
            }
        }

        /// <summary>
        /// Gets or sets the univariate analysis associated with this weighted entry.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// Thrown when the assigned value is itself a <c>CompositeAnalysis</c>;
        /// composite-of-composite is not supported and would risk circular references.
        /// </exception>
        public IUnivariateAnalysis UnivariateAnalysis
        {
            get { return _univariateAnalysis; }
            set
            {
                if (value is CompositeAnalysis)
                {
                    throw new ArgumentException(
                        "A CompositeAnalysis cannot be used as a child of another CompositeAnalysis. " +
                        "Composite-of-composite is not supported because it can introduce circular " +
                        "references and undefined weighting semantics.",
                        nameof(value));
                }

                if (_univariateAnalysis != null)
                    _univariateAnalysis.PropertyChanged -= UnivariateAnalysis_PropertyChanged;

                _univariateAnalysis = value;

                if (_univariateAnalysis != null)
                    _univariateAnalysis.PropertyChanged += UnivariateAnalysis_PropertyChanged;

                RaisePropertyChange(nameof(UnivariateAnalysis));
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Supports the <c>UnivariateAnalysis_PropertyChanged</c> helper.
        /// </summary>
        /// <param name="sender">The event source.</param>
        /// <param name="e">The event data.</param>
        /// <remarks>
        /// This member supports the owning analysis or model implementation.
        /// </remarks>
        private void UnivariateAnalysis_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            RaisePropertyChange(e.PropertyName);
        }

        /// <summary>
        /// Raises the <c>PropertyChanged</c> event for the specified property.
        /// </summary>
        /// <param name="propertyName">The name of the property that changed.</param>
        protected virtual void RaisePropertyChange(string? propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Serializes this instance to an XML element.
        /// </summary>
        /// <returns>An XML element representing this weighted analysis, or null if no analysis is set.</returns>
        public XElement? ToXElement()
        {
            if (UnivariateAnalysis == null) return null;

            var result = new XElement(nameof(WeightedUnivariateAnalysis));
            result.SetAttributeValue(nameof(Weight), Weight.ToString("G17", CultureInfo.InvariantCulture));
            // Note: The actual UnivariateAnalysis reference must be resolved by the caller
            // using the analysis name or ID when deserializing
            return result;
        }

        /// <summary>
        /// Validates this weighted analysis.
        /// </summary>
        /// <returns>A tuple indicating whether the analysis is valid and any validation messages.</returns>
        public (bool IsValid, string Message) Validate()
        {
            if (UnivariateAnalysis == null)
                return (false, "Error: A selected univariate analysis is invalid or requires estimation.");

            if (!UnivariateAnalysis.IsEstimated)
                return (false, "Error: A selected univariate analysis is invalid or requires estimation.");

            var validation = UnivariateAnalysis.Validate();
            if (!validation.IsValid)
                return (false, string.Join("; ", validation.ValidationMessages));

            return (true, string.Empty);
        }

        #endregion
    }
}
