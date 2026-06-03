using System.ComponentModel;
using System.Globalization;
using System.Xml.Linq;

namespace RMC.BestFit.Models.TrendFunctions.Support
{
    /// <summary>
    /// Base class for trend models that provides common storage
    /// and XML serialization logic.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// </remarks>
    public abstract class TrendModelBase : ITrendModel, INotifyPropertyChanged
    {
        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="TrendModelBase"/> class
        /// with default settings and parameters created by
        /// <see cref="SetDefaultParameters"/>.
        /// </summary>
        protected TrendModelBase()
        {
            Parameters = new List<ModelParameter>();
            SetDefaultParameters();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TrendModelBase"/> class
        /// from an <see cref="XElement"/> representation.
        /// </summary>
        /// <param name="xElement">The XML element to deserialize from.</param>
        protected TrendModelBase(XElement xElement)
        {
            if (xElement == null)
            {
                throw new ArgumentNullException(nameof(xElement));
            }

            var ownerAttr = xElement.Attribute(nameof(OwnerName));
            if (ownerAttr != null)
            {
                _ownerName = ownerAttr.Value;
            }

            var priorsAttr = xElement.Attribute(nameof(UseDefaultFlatPriors));
            if (priorsAttr != null)
            {
                bool.TryParse(priorsAttr.Value, out _useDefaultFlatPriors);
            }

            var startAttr = xElement.Attribute(nameof(StartIndex));
            if (startAttr != null)
            {
                int.TryParse(startAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out _startIndex);
            }

            Parameters = new List<ModelParameter>();

            foreach (XElement parms in xElement.Elements(nameof(Parameters)).Elements(nameof(ModelParameter)))
            {
                Parameters.Add(new ModelParameter(parms));
            }
        }

        #endregion

        #region Members

        protected string _ownerName = string.Empty;
        protected bool _useDefaultFlatPriors = true;
        protected int _startIndex;
        protected List<ModelParameter> _parameters = new List<ModelParameter>();

        /// <inheritdoc/>
        public string OwnerName
        {
            get => _ownerName;
            set
            {
                if (!string.Equals(_ownerName, value, StringComparison.Ordinal))
                {
                    _ownerName = value ?? string.Empty;
                    for (int i = 0; i < Parameters.Count; i++)
                        Parameters[i].OwnerName = _ownerName;
                    RaisePropertyChange(nameof(OwnerName));
                }
            }
        }

        /// <inheritdoc/>
        public abstract TrendModelType Type { get; }

        /// <inheritdoc/>
        public int StartIndex
        {
            get => _startIndex;
            set
            {
                if (_startIndex != value)
                {
                    _startIndex = value;
                    RaisePropertyChange(nameof(StartIndex));
                }
            }
        }

        /// <inheritdoc/>
        public List<ModelParameter> Parameters
        {
            get => _parameters;
            protected set
            {
                _parameters = value ?? new List<ModelParameter>();
                RaisePropertyChange(nameof(Parameters));
                RaisePropertyChange(nameof(NumberOfParameters));
            }
        }

        /// <inheritdoc/>
        public int NumberOfParameters => Parameters.Count;

        /// <inheritdoc/>
        public bool UseDefaultFlatPriors
        {
            get => _useDefaultFlatPriors;
            set
            {
                if (_useDefaultFlatPriors != value)
                {
                    _useDefaultFlatPriors = value;
                    RaisePropertyChange(nameof(UseDefaultFlatPriors));

                    if (_useDefaultFlatPriors)
                    {
                        SetDefaultParameters();
                    }
                }
            }
        }

        /// <inheritdoc/>
        public event PropertyChangedEventHandler? PropertyChanged;

        #endregion

        #region Methods

        /// <summary>
        /// Raises the <see cref="PropertyChanged"/> event for the specified
        /// property name.
        /// </summary>
        /// <param name="propertyName">Name of the property that changed.</param>
        protected void RaisePropertyChange(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <inheritdoc/>
        public abstract void SetDefaultParameters();

        /// <inheritdoc/>
        public void SetParameterValues(IList<double> parameters)
        {
            if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            if (parameters.Count != NumberOfParameters)
            {
                throw new ArgumentException(
                    "The list of parameter values has the wrong length.",
                    nameof(parameters));
            }

            for (int i = 0; i < NumberOfParameters; i++)
            {
                Parameters[i].Value = parameters[i];
            }

            RaisePropertyChange(nameof(Parameters));
        }

        /// <inheritdoc/>
        public abstract double Predict(int index);

        /// <inheritdoc/>
        public abstract ITrendModel Clone();

        /// <inheritdoc/>
        public XElement ToXElement()
        {
            var result = new XElement(nameof(ITrendModel));
            result.SetAttributeValue(nameof(OwnerName), OwnerName);
            result.SetAttributeValue(nameof(UseDefaultFlatPriors), UseDefaultFlatPriors.ToString());
            result.SetAttributeValue(nameof(Type), Type.ToString());
            result.SetAttributeValue(nameof(StartIndex), StartIndex.ToString(CultureInfo.InvariantCulture));

            var parms = new XElement(nameof(Parameters));
            foreach (var p in Parameters)
            {
                parms.Add(p.ToXElement());
            }

            result.Add(parms);
            return result;
        }

        #endregion
    }
}
