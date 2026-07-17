using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FrameworkInterfaces;
using RMC.BestFit.Models;

namespace RMC.BestFit.UI
{
    /// <summary>
    /// Represents covariate data associated with a time series element for use in statistical analyses.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    /// This class wraps a TimeSeriesElement and provides property change notification functionality
    /// to support data binding and analysis workflows.
    /// </para>
    /// </remarks>
    public class CovariateData : INotifyPropertyChanged, IDisposable
    {

        /// <summary>
        /// Initializes a new instance of the CovariateData class.
        /// </summary>
        public CovariateData() { }

        /// <summary>
        /// The underlying time series element containing the covariate data.
        /// </summary>
        private TimeSeriesElement _timeSeriesElement;

        /// <summary>
        /// Occurs when a property value changes.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Gets or sets the time series element containing the covariate data.
        /// </summary>
        /// <remarks>
        /// When the value is set, the class subscribes to property changes on the new element
        /// and unsubscribes from the previous element to ensure proper change notification propagation.
        /// </remarks>
        public TimeSeriesElement TimeSeriesElement
        {
            get { return _timeSeriesElement; }
            set
            {
                if (_timeSeriesElement != null)
                {
                    _timeSeriesElement.PropertyChanged -= TimeSeriesElement_PropertyChanged;
                    _timeSeriesElement.Deleted -= TimeSeriesElement_Deleted;
                }

                _timeSeriesElement = value;

                if (_timeSeriesElement != null)
                {
                    _timeSeriesElement.PropertyChanged += TimeSeriesElement_PropertyChanged;
                    _timeSeriesElement.Deleted += TimeSeriesElement_Deleted;
                }

                RaisePropertyChange(nameof(TimeSeriesElement));
            }
        }

        /// <summary>
        /// Handles deletion of the wrapped <see cref="TimeSeriesElement"/> from its parent
        /// collection. Nulls out the reference; the owning <c>TimeSeriesAnalysis</c> observes
        /// the <c>TimeSeriesElement</c> property change and removes this <see cref="CovariateData"/>
        /// wrapper from its <c>Covariates</c> collection.
        /// </summary>
        private void TimeSeriesElement_Deleted(IElement element)
        {
            TimeSeriesElement = null;
        }

        /// <summary>
        /// Handles property change events from the associated time series element.
        /// </summary>
        /// <param name="sender">The source of the event, typically the TimeSeriesElement.</param>
        /// <param name="e">The PropertyChangedEventArgs containing the name of the changed property.</param>
        /// <remarks>
        /// This method forwards property change notifications from the time series element to listeners
        /// of this CovariateData instance, enabling cascading change notifications.
        /// </remarks>
        private void TimeSeriesElement_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            RaisePropertyChange(e.PropertyName);
        }

        /// <summary>
        /// Raises the PropertyChanged event to notify listeners of a property value change.
        /// </summary>
        /// <param name="propertyName">The name of the property that changed.</param>
        /// <remarks>
        /// This method can be overridden in derived classes to customize property change notification behavior.
        /// </remarks>
        protected virtual void RaisePropertyChange(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Releases the subscription on the wrapped <see cref="TimeSeriesElement"/> so this
        /// wrapper can be garbage-collected after it is removed from
        /// <c>TimeSeriesAnalysis.Covariates</c>.
        /// </summary>
        /// <remarks>
        /// Without this, the underlying time series element's <c>PropertyChanged</c> +
        /// <c>Deleted</c> delegate lists keep the wrapper alive even after it has been removed
        /// from its owning collection — a long-session memory leak for users who add and
        /// remove covariates repeatedly. Setting <see cref="TimeSeriesElement"/> to null
        /// triggers the setter's unsubscribe path.
        /// </remarks>
        public void Dispose()
        {
            TimeSeriesElement = null;
        }

    }
}
