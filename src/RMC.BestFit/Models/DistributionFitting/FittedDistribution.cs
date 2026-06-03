using System;
using System.ComponentModel;
using Numerics.Distributions;
using System.Xml.Linq;
using System.Globalization;

namespace RMC.BestFit.Models
{

    /// <summary>
    /// Fitted distribution class.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// </remarks>
    public class FittedDistribution : INotifyPropertyChanged
    {

        /// <summary>
        /// Constructs a new fitted distribution. 
        /// </summary>
        /// <param name="distribution">The fitted distribution.</param>
        /// <param name="aic">The Akaike information criteria (AIC) of the fit.</param>
        /// <param name="bic">The Bayesian information criteria (BIC) of the fit.</param>
        /// <param name="rmse">The Root Mean Square Error (RMSE) of the fit.</param>
        /// <param name="fitSucceeded">Determines whether the distribution was successfully fitted. </param>
        /// <param name="showResults">Determines whether to show the fitting results. </param>
        public FittedDistribution(UnivariateDistributionBase distribution, double aic = double.NaN, double bic = double.NaN, double rmse = double.NaN, bool fitSucceeded = false, bool showResults = false)
        {
            Distribution = distribution;
            AIC = aic;
            BIC = bic;
            RMSE = rmse;
            FitSucceeded = fitSucceeded;
            ShowResults = showResults;
        }

        /// <summary>
        /// Constructs a new fitted distribution. 
        /// </summary>
        /// <param name="xElement">The XElement that will be deserialized.<</param>
        public FittedDistribution(XElement xElement)
        {
            var distributionElement = xElement.Element("Distribution");
            if (distributionElement != null)
                Distribution = UnivariateDistributionFactory.CreateDistribution(distributionElement);

            var aicAttr = xElement.Attribute(nameof(AIC));
            if (aicAttr != null)
            {
                double.TryParse(aicAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var aic);
                AIC = aic;
            }
            var bicAttr = xElement.Attribute(nameof(BIC));
            if (bicAttr != null)
            {
                double.TryParse(bicAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var bic);
                BIC = bic;
            }
            var rmseAttr = xElement.Attribute(nameof(RMSE));
            if (rmseAttr != null)
            {
                double.TryParse(rmseAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var rmse);
                RMSE = rmse;
            }
            var fitSucceededAttr = xElement.Attribute(nameof(FitSucceeded));
            if (fitSucceededAttr != null)
            {
                bool.TryParse(fitSucceededAttr.Value, out var fitSucceeded);
                FitSucceeded = fitSucceeded;
            }
            var showResultsAttr = xElement.Attribute(nameof(ShowResults));
            if (showResultsAttr != null)
            {
                bool.TryParse(showResultsAttr.Value, out var showResults);
                ShowResults = showResults;
            }
        }


        /// <summary>
        /// Occurs when a property value changes.
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Backing field for <see cref="ShowResults"/>.
        /// </summary>
        private bool _showResults = false;

        /// <summary>
        /// Gets or sets a brief description of the failure when <see cref="FitSucceeded"/>
        /// is <c>false</c>. Empty string when the fit succeeded or no diagnostic was
        /// captured. Set by <see cref="FittingAnalysis"/> when an MLE attempt throws.
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the fitted distribution.
        /// </summary>
        public UnivariateDistributionBase? Distribution { get; }

        /// <summary>
        /// Gets or sets the Akaike information criteria (AIC) of the fit.
        /// </summary>
        public double AIC { get; }

        /// <summary>
        /// Gets or sets the Bayesian information criteria (BIC) of the fit.
        /// </summary>
        public double BIC { get; }

        /// <summary>
        /// Gets or sets the Root Mean Square Error (RMSE) of the fit. 
        /// </summary>
        public double RMSE { get; }

        /// <summary>
        /// Determines whether the distribution was successfully fitted. 
        /// </summary>
        public bool FitSucceeded { get; }

        /// <summary>
        /// Determines whether to show the fitting results. 
        /// </summary>
        public bool ShowResults
        {
            get { return _showResults; }
            set
            {
                if (_showResults != value)
                {
                    _showResults = value;
                    RaisePropertyChange(nameof(ShowResults));
                }
            }
        }

        /// <summary>
        /// Gets the distribution tool tip. 
        /// </summary>
        public string ToolTip
        {
            get
            {
                if (Distribution is null)
                    return "No distribution set";

                var parmString = Distribution.ParameterNames;
                var parms = Distribution.GetParameters;
                string tip = Distribution.DisplayName;
                for (int i = 0; i < Distribution.NumberOfParameters; i++)
                    tip += Environment.NewLine + parmString[i] + " = " + parms[i].ToString("N4");
                return tip;
            }
        }

        /// <summary>
        /// Raise property changed event.
        /// </summary>
        /// <param name="propertyName">Name of property that changed.</param>
        protected virtual void RaisePropertyChange(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Returns the fitted distribution as XElement.
        /// </summary>
        public XElement ToXElement()
        {
            var result = new XElement(nameof(FittedDistribution));
            if (Distribution is not null)
                result.Add(Distribution.ToXElement());
            result.SetAttributeValue(nameof(AIC), AIC.ToString("G17", CultureInfo.InvariantCulture));
            result.SetAttributeValue(nameof(BIC), BIC.ToString("G17", CultureInfo.InvariantCulture));
            result.SetAttributeValue(nameof(RMSE), RMSE.ToString("G17", CultureInfo.InvariantCulture));
            result.SetAttributeValue(nameof(FitSucceeded), FitSucceeded.ToString());
            result.SetAttributeValue(nameof(ShowResults), ShowResults.ToString());
            return result;
        }

    }
}
