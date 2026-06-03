using Numerics;
using Numerics.Distributions;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Xml.Linq;

namespace RMC.BestFit.Models
{

    /// <summary>
    /// A class for model parameters.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// </remarks>
    public class ModelParameter : INotifyPropertyChanged
    {

        /// <summary>
        /// Constructs an empty model parameter.
        /// </summary>
        public ModelParameter()
        {
            _priorDistribution = new Uniform(LowerBound, UpperBound);
        }

        /// <summary>
        /// Constructs a new model parameter.
        /// </summary>
        /// <param name="ownerName">The name of the owning model component.</param>
        /// <param name="name">The parameter name.</param>
        /// <param name="value">The parameter value.</param>
        /// <param name="lowerBound">The minimum value (inclusive) for the model parameter.</param>
        /// <param name="upperBound">The maximum value (inclusive) for the model parameter.</param>
        /// <param name="priorDistribution">The prior distribution for the model parameter.</param>
        /// <param name="isPositive">Determines if the parameter must be strictly greater than zero.</param>
        /// <param name="isFixed">Determines if the parameter should be held fixed during model estimation. Default = true.</param>
        public ModelParameter(string ownerName, string name, double value, double lowerBound, double upperBound, UnivariateDistributionBase priorDistribution, bool isPositive = false, bool isFixed = false)
        {
            _ownerName = ownerName;
            _name = name;
            _value = value;
            _lowerBound = lowerBound;
            _upperBound = upperBound;
            _priorDistribution = priorDistribution;
            _isPositive = isPositive;
            _isFixed = isFixed;
        }

        /// <summary>
        /// Constructs a model parameter from XElement.
        /// </summary>
        /// <param name="xElement">The XElement to deserialize.</param>
        public ModelParameter(XElement xElement)
        {
            var ownerAttr = xElement.Attribute(nameof(OwnerName));
            if (ownerAttr != null) _ownerName = ownerAttr.Value;
            var nameAttr = xElement.Attribute(nameof(Name));
            if (nameAttr != null) _name = nameAttr.Value;
            var valueAttr = xElement.Attribute(nameof(Value));
            if (valueAttr != null) double.TryParse(valueAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out _value);
            var lowerAttr = xElement.Attribute(nameof(LowerBound));
            if (lowerAttr != null) double.TryParse(lowerAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out _lowerBound);
            var upperAttr = xElement.Attribute(nameof(UpperBound));
            if (upperAttr != null) double.TryParse(upperAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out _upperBound);
            var positiveAttr = xElement.Attribute(nameof(IsPositive));
            if (positiveAttr != null)
            {
                bool.TryParse(positiveAttr.Value, out _isPositive);
            }
            else
            {
                _isPositive = LowerBound == Tools.DoubleMachineEpsilon;
            }
            var fixedAttr = xElement.Attribute(nameof(IsFixed));
            if (fixedAttr != null) bool.TryParse(fixedAttr.Value, out _isFixed);
            var distElement = xElement.Element("Distribution");
            if (distElement != null)
                _priorDistribution = UnivariateDistributionFactory.CreateDistribution(distElement);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private string _ownerName = "";
        private string _name = "Parameter";
        private double _value = 0d;
        private double _lowerBound = double.MinValue;
        private double _upperBound = double.MaxValue;
        private bool _isPositive = false;
        private bool _isFixed = false;
        private UnivariateDistributionBase _priorDistribution = null!;
        
        /// <summary>
        /// The name of the owning model component. Useful for keeping track of names in hierarchical models.
        /// </summary>
        public string OwnerName
        {
            get { return _ownerName; }
            set
            {
                if (_ownerName != value)
                {
                    _ownerName = value;
                    RaisePropertyChange(nameof(OwnerName));
                }
            }
        }

        /// <summary>
        /// The parameter name.
        /// </summary>
        public string Name
        {
            get { return _name; }
            set
            {
                if (_name != value)
                {
                    _name = value;
                    RaisePropertyChange(nameof(Name));
                }
            }
        }

        /// <summary>
        /// The parameter display name.
        /// </summary>
        public string DisplayName => OwnerName == "" ? Name : Name == "" ? OwnerName : OwnerName + " " + Name;

        /// <summary>
        /// The parameter value (point estimate).
        /// </summary>
        public double Value
        {
            get { return _value; }
            set
            {
                if (_value != value)
                {
                    _value = value;
                    RaisePropertyChange(nameof(Value));
                }
            }
        }

        /// <summary>
        /// The minimum value (inclusive) for the model parameter. Default = Double.MinValue
        /// </summary>
        public double LowerBound
        {
            get { return _lowerBound; }
            set
            {
                if (_lowerBound != value)
                {
                    _lowerBound = value;
                    RaisePropertyChange(nameof(LowerBound));
                }
            }
        }

        /// <summary>
        /// The maximum value (inclusive) for the model parameter. Default = Double.MaxValue
        /// </summary>
        public double UpperBound
        {
            get { return _upperBound; }
            set
            {
                if (_upperBound != value)
                {
                    _upperBound = value;
                    RaisePropertyChange(nameof(UpperBound));
                }
            }
        }

        /// <summary>
        /// The prior distribution for the model parameter. Default = Uniform(Double.MinValue, Double.MaxValue).
        /// </summary>
        public UnivariateDistributionBase PriorDistribution
        {
            get { return _priorDistribution; }
            set
            {
                if (_priorDistribution != value)
                {
                    _priorDistribution = value;
                    RaisePropertyChange(nameof(PriorDistribution));
                }
            }
        }

        /// <summary>
        /// Determines if the parameter must be strictly greater than zero.
        /// </summary>
        public bool IsPositive
        {
            get { return _isPositive; }
            set
            {
                if (_isPositive != value)
                {
                    _isPositive = value;
                    RaisePropertyChange(nameof(IsPositive));
                }
            }
        }

        /// <summary>
        /// Determines if the parameter should be held fixed during model estimation.
        /// </summary>
        public bool IsFixed 
        {
            get { return _isFixed; }
            set
            {
                if (_isFixed != value)
                {
                    _isFixed = value;
                    RaisePropertyChange(nameof(IsFixed));
                }
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
        /// Create a copy of the model parameter.
        /// </summary>
        public ModelParameter Clone()
        {
            return new ModelParameter(ToXElement());
        }

        /// <summary>
        /// Returns the model parameter as XElement.
        /// </summary>
        public XElement ToXElement()
        {
            var result = new XElement(nameof(ModelParameter));
            result.SetAttributeValue(nameof(OwnerName), OwnerName);
            result.SetAttributeValue(nameof(Name), Name);
            result.SetAttributeValue(nameof(Value), Value.ToString("G17", CultureInfo.InvariantCulture));
            result.SetAttributeValue(nameof(LowerBound), LowerBound.ToString("G17", CultureInfo.InvariantCulture));
            result.SetAttributeValue(nameof(UpperBound), UpperBound.ToString("G17", CultureInfo.InvariantCulture));
            result.SetAttributeValue(nameof(IsPositive), IsPositive.ToString());
            result.SetAttributeValue(nameof(IsFixed), IsFixed.ToString());
            result.Add(PriorDistribution.ToXElement());
            return result;
        }

        /// <summary>
        /// Validates the current state of the object and reports any issues found.
        /// </summary>
        /// <returns>
        /// A tuple containing:
        /// <list type="bullet">
        /// <item>
        /// <description><c>IsValid</c>: <c>true</c> if the object passes all validation checks; otherwise <c>false</c>.</description>
        /// </item>
        /// <item>
        /// <description><c>ValidationMessages</c>: a list of messages describing any validation errors or warnings. This list is empty when the object is valid.</description>
        /// </item>
        /// </list>
        /// </returns>
        public (bool IsValid, List<string> ValidationMessages) Validate()
        {
            bool isValid = true;
            var messageList = new List<string>();

            if (LowerBound > UpperBound)
            {
                isValid = false;
                messageList.Add("Error: The lower bound cannot be greater than the upper bound for '" + DisplayName + "'.");
            }
            if (!Tools.IsFinite(Value) || Value < LowerBound || Value > UpperBound)
            {
                isValid = false;
                messageList.Add("Error: The parameter value for '" + DisplayName + "' must be a finite number between the lower and upper bounds.");
            }
            if (PriorDistribution.ValidateParameters(PriorDistribution.GetParameters, false) != null)
            {
                isValid = false;
                messageList.Add("Error: The prior distribution for the parameter '" + DisplayName + "' is invalid.");
            }
            if (IsPositive)
            {
                double thetaMin = Tools.DoubleMachineEpsilon;
                double min = double.IsInfinity(PriorDistribution.Minimum) ? PriorDistribution.InverseCDF(1e-16) : PriorDistribution.Minimum;
                if (min < thetaMin)
                {
                    isValid = false;
                    messageList.Add("Error: Prior distribution for " + DisplayName + " is invalid. The minimum value allowable for " + DisplayName + " is " + thetaMin.ToString() + ".");
                }
            }

            return (isValid, messageList);
        }

    }
}
