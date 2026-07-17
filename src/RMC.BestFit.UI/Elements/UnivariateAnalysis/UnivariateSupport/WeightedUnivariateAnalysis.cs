using System;
using System.ComponentModel;
using Numerics.Distributions;
using System.Xml.Linq;
using System.Globalization;
using RMC.BestFit.Models;

namespace RMC.BestFit.UI
{
    /// <summary>
    /// A weight-bearing wrapper over a <see cref="UnivariateAnalysis"/> used as a component of a
    /// <see cref="CompositeAnalysis"/>. Forwards property changes from the underlying analysis so
    /// the composite can react to upstream re-fits, and serializes to a (Name, Weight) XML element
    /// during composite persistence.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// </remarks>
    public class WeightedUnivariateAnalysis : INotifyPropertyChanged, IDisposable
    {

        /// <summary>
        /// Constructs an empty weighted univariate analysis.
        /// </summary>
        public WeightedUnivariateAnalysis() { }

        /// <summary>Backing field for <see cref="Weight"/>.</summary>
        private double _weight;

        /// <summary>Backing field for <see cref="UnivariateAnalysis"/>.</summary>
        private IUnivariate _univariateAnalysis;

        /// <summary>
        /// Occurs when a property value changes.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Gets or sets the weight for this univariate analysis in the composite.
        /// </summary>
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
        /// Gets or sets the analysis associated with this weighted entry. Typed as <see cref="IUnivariate"/>
        /// so that any sibling analysis in the parent collection (UnivariateAnalysis, B17CAnalysis,
        /// MixtureAnalysis, PointProcessAnalysis, CompositeAnalysis) can serve as a composite component.
        /// </summary>
        /// <remarks>
        /// Was previously typed as <c>UnivariateAnalysis</c>, which produced an
        /// <see cref="InvalidCastException"/> on Open if the saved component name now resolved to a
        /// non-<c>UnivariateAnalysis</c> sibling (e.g., a B17CAnalysis renamed to the same name).
        /// </remarks>
        public IUnivariate UnivariateAnalysis
        {
            get { return _univariateAnalysis; }
            set
            {
                if (_univariateAnalysis != null)
                    _univariateAnalysis.PropertyChanged -= UnivariateAnalysis_PropertyChanged;

                _univariateAnalysis = value;

                if (_univariateAnalysis != null)
                    _univariateAnalysis.PropertyChanged += UnivariateAnalysis_PropertyChanged;

                RaisePropertyChange(nameof(UnivariateAnalysis));
            }
        }

        /// <summary>
        /// Handles property changes from the associated univariate analysis, propagating them up.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        private void UnivariateAnalysis_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            RaisePropertyChange(e.PropertyName);
        }

        /// <summary>
        /// Raises the property changed event for the specified property.
        /// </summary>
        /// <param name="propertyName">The name of the property that changed.</param>
        protected virtual void RaisePropertyChange(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Serializes this instance to an XML element.
        /// </summary>
        /// <returns>An XML element representing this weighted univariate analysis, or null if the analysis is not set.</returns>
        public XElement ToXElement()
        {
            if (UnivariateAnalysis == null)  return null;
            var result = new XElement(nameof(WeightedUnivariateAnalysis));
            result.SetAttributeValue(nameof(UnivariateAnalysis), UnivariateAnalysis.NameOnDisk);
            result.SetAttributeValue(nameof(Weight), Weight.ToString("G17", CultureInfo.InvariantCulture));
            return result;
        }

        /// <summary>
        /// Releases the subscription on the wrapped <see cref="UnivariateAnalysis"/> so this
        /// wrapper can be garbage-collected after it is removed from <c>CompositeAnalysis.Analyses</c>.
        /// </summary>
        /// <remarks>
        /// Without this, the underlying univariate's <c>PropertyChanged</c> delegate list keeps
        /// the wrapper alive even after it has been removed from its owning collection — a
        /// long-session memory leak for users who add and remove composite components repeatedly.
        /// Setting <see cref="UnivariateAnalysis"/> to null triggers the setter's unsubscribe path.
        /// </remarks>
        public void Dispose()
        {
            UnivariateAnalysis = null;
        }

    }
}
