using Numerics.Distributions;
using RMC.BestFit.Models.TrendFunctions.Support;
using System.ComponentModel;
using System.Globalization;
using System.Xml.Linq;

namespace RMC.BestFit.Models.TrendFunctions
{
    /// <summary>
    /// General linear function for covariate modeling.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class implements a general linear model of the form:
    /// <c>f(x) = β₀ + β₁x₁ + β₂x₂ + ... + βₚxₚ</c>
    /// where β₀ is the intercept and βᵢ are regression coefficients for covariates xᵢ.
    /// </para>
    /// <para>
    /// Unlike time-based trend models that use a scalar index, this model supports
    /// arbitrary covariate matrices, making it suitable for:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Spatial regression surfaces (location, elevation, etc.)</description></item>
    /// <item><description>Covariate effects on distribution parameters</description></item>
    /// <item><description>ARMAX exogenous variable modeling</description></item>
    /// <item><description>Hierarchical Bayesian spatial models (SpatialGEV)</description></item>
    /// </list>
    /// <para>
    ///     <b>Authors:</b>
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// </remarks>
    public class GeneralLinearFunction : ITrendModel, INotifyPropertyChanged
    {
        #region Construction

        /// <summary>
        /// Initializes a new instance of the <see cref="GeneralLinearFunction"/> class
        /// with default settings (intercept only, no covariates).
        /// </summary>
        public GeneralLinearFunction()
        {
            _ownerName = string.Empty;
            _parameters = new List<ModelParameter>();
            SetDefaultParameters();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GeneralLinearFunction"/> class
        /// for a specific distribution parameter with optional covariates.
        /// </summary>
        /// <param name="ownerName">
        /// The name of the owning distribution parameter (e.g., "Location", "Scale", "Shape").
        /// </param>
        /// <param name="covariates">
        /// The covariate matrix where each row corresponds to an observation (site)
        /// and each column corresponds to a covariate. Can be <c>null</c> for intercept-only model.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="ownerName"/> is <c>null</c>.
        /// </exception>
        public GeneralLinearFunction(string ownerName, double[,]? covariates = null)
        {
            _ownerName = ownerName ?? throw new ArgumentNullException(nameof(ownerName));
            _covariates = covariates;
            _parameters = new List<ModelParameter>();
            SetDefaultParameters();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GeneralLinearFunction"/> class
        /// from an <see cref="XElement"/> representation.
        /// </summary>
        /// <param name="xElement">The XML element to deserialize from.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="xElement"/> is <c>null</c>.
        /// </exception>
        public GeneralLinearFunction(XElement xElement)
        {
            if (xElement == null)
                throw new ArgumentNullException(nameof(xElement));

            _parameters = new List<ModelParameter>();

            // Deserialize basic properties
            if (xElement.Attribute(nameof(OwnerName)) != null)
                _ownerName = xElement.Attribute(nameof(OwnerName))!.Value;

            if (xElement.Attribute(nameof(UseDefaultFlatPriors)) != null)
                bool.TryParse(xElement.Attribute(nameof(UseDefaultFlatPriors))!.Value, out _useDefaultFlatPriors);

            if (xElement.Attribute(nameof(StartIndex)) != null)
                int.TryParse(xElement.Attribute(nameof(StartIndex))!.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out _startIndex);

            // Deserialize covariate dimensions
            int numRows = 0, numCols = 0;
            if (xElement.Attribute("CovariateRows") != null)
                int.TryParse(xElement.Attribute("CovariateRows")!.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out numRows);
            if (xElement.Attribute("CovariateCols") != null)
                int.TryParse(xElement.Attribute("CovariateCols")!.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out numCols);

            // Deserialize covariate matrix if present
            if (numRows > 0 && numCols > 0)
            {
                _covariates = new double[numRows, numCols];
                var covElement = xElement.Element("Covariates");
                if (covElement != null)
                {
                    string[] values = covElement.Value.Split(',');
                    int idx = 0;
                    for (int i = 0; i < numRows && idx < values.Length; i++)
                    {
                        for (int j = 0; j < numCols && idx < values.Length; j++)
                        {
                            double.TryParse(values[idx++], NumberStyles.Any, CultureInfo.InvariantCulture, out _covariates[i, j]);
                        }
                    }
                }
            }

            // Deserialize parameters
            foreach (XElement parms in xElement.Elements(nameof(Parameters)).Elements(nameof(ModelParameter)))
            {
                _parameters.Add(new ModelParameter(parms));
            }
        }

        #endregion

        #region Members

        private string _ownerName = string.Empty;
        private bool _useDefaultFlatPriors = true;
        private int _startIndex;
        private double[,]? _covariates;
        private List<ModelParameter> _parameters;

        #endregion

        #region Properties

        /// <inheritdoc/>
        public string OwnerName
        {
            get => _ownerName;
            set
            {
                if (!string.Equals(_ownerName, value, StringComparison.Ordinal))
                {
                    _ownerName = value ?? string.Empty;
                    RaisePropertyChanged(nameof(OwnerName));
                }
            }
        }

        /// <inheritdoc/>
        public TrendModelType Type => TrendModelType.GeneralLinear;

        /// <inheritdoc/>
        /// <remarks>
        /// For GeneralLinearFunction, StartIndex is used when the model is evaluated
        /// via the Predict(int index) method for compatibility with time-based models.
        /// When using PredictWithCovariates, this property is not used.
        /// </remarks>
        public int StartIndex
        {
            get => _startIndex;
            set
            {
                if (_startIndex != value)
                {
                    _startIndex = value;
                    RaisePropertyChanged(nameof(StartIndex));
                }
            }
        }

        /// <inheritdoc/>
        public List<ModelParameter> Parameters
        {
            get => _parameters;
            private set
            {
                _parameters = value ?? new List<ModelParameter>();
                RaisePropertyChanged(nameof(Parameters));
                RaisePropertyChanged(nameof(NumberOfParameters));
            }
        }

        /// <inheritdoc/>
        public int NumberOfParameters => _parameters.Count;

        /// <inheritdoc/>
        public bool UseDefaultFlatPriors
        {
            get => _useDefaultFlatPriors;
            set
            {
                if (_useDefaultFlatPriors != value)
                {
                    _useDefaultFlatPriors = value;
                    RaisePropertyChanged(nameof(UseDefaultFlatPriors));

                    if (_useDefaultFlatPriors)
                    {
                        SetDefaultParameters();
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the covariate matrix.
        /// </summary>
        /// <remarks>
        /// Each row corresponds to an observation (e.g., a site in spatial models).
        /// Each column corresponds to a covariate variable.
        /// The number of columns determines the number of regression coefficients (plus intercept).
        /// </remarks>
        public double[,]? Covariates
        {
            get => _covariates;
            set
            {
                _covariates = value;
                RaisePropertyChanged(nameof(Covariates));
                RaisePropertyChanged(nameof(NumberOfCovariates));
                // Rebuild parameters when covariates change
                SetDefaultParameters();
            }
        }

        /// <summary>
        /// Gets the number of covariates (columns in the covariate matrix).
        /// </summary>
        public int NumberOfCovariates => _covariates?.GetLength(1) ?? 0;

        /// <summary>
        /// Gets the number of observations (rows in the covariate matrix).
        /// </summary>
        public int NumberOfObservations => _covariates?.GetLength(0) ?? 0;

        /// <inheritdoc/>
        public event PropertyChangedEventHandler? PropertyChanged;

        #endregion

        #region Methods

        /// <summary>
        /// Raises the <c>PropertyChanged</c> event for the specified property.
        /// </summary>
        /// <param name="propertyName">Name of the property that changed.</param>
        private void RaisePropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Creates parameters for the intercept (β₀) and one coefficient (βᵢ) per covariate.
        /// Default priors are Uniform(-1, 1) for the regression coefficients.
        /// The intercept prior is left unbounded (uses ModelParameter defaults).
        /// </remarks>
        public void SetDefaultParameters()
        {
            _parameters = new List<ModelParameter>();

            // Intercept parameter (β₀)
            _parameters.Add(new ModelParameter
            {
                OwnerName = _ownerName,
                Name = string.IsNullOrEmpty(_ownerName) ? "β₀" : $"{_ownerName}.β₀"
            });

            // Covariate coefficients (β₁, β₂, ...)
            int numCovariates = _covariates?.GetLength(1) ?? 0;
            for (int j = 1; j <= numCovariates; j++)
            {
                string betaName = "β" + SubscriptFormatter.ToSubscript(j);
                _parameters.Add(new ModelParameter
                {
                    OwnerName = _ownerName,
                    Name = string.IsNullOrEmpty(_ownerName) ? betaName : $"{_ownerName}.{betaName}",
                    LowerBound = -1,
                    UpperBound = 1,
                    PriorDistribution = new Uniform(-1, 1)
                });
            }

            RaisePropertyChanged(nameof(Parameters));
            RaisePropertyChanged(nameof(NumberOfParameters));
        }

        /// <inheritdoc/>
        public void SetParameterValues(IList<double> parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));

            if (parameters.Count != NumberOfParameters)
                throw new ArgumentException(
                    $"Expected {NumberOfParameters} parameter(s) but received {parameters.Count}.",
                    nameof(parameters));

            for (int i = 0; i < NumberOfParameters; i++)
            {
                _parameters[i].Value = parameters[i];
            }

            RaisePropertyChanged(nameof(Parameters));
        }

        /// <inheritdoc/>
        /// <remarks>
        /// For GeneralLinearFunction, the index is used to select a row from the
        /// stored covariate matrix. The prediction is computed as:
        /// <c>β₀ + β₁×Covariates[index,0] + β₂×Covariates[index,1] + ...</c>
        /// If no covariates are stored, returns only the intercept.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if <paramref name="index"/> is outside the bounds of the covariate matrix.
        /// </exception>
        public double Predict(int index)
        {
            double result = _parameters[0].Value; // Intercept

            if (_covariates != null)
            {
                if (index < 0 || index >= _covariates.GetLength(0))
                    throw new ArgumentOutOfRangeException(nameof(index),
                        $"Index {index} is out of range [0, {_covariates.GetLength(0) - 1}].");

                for (int j = 0; j < _covariates.GetLength(1); j++)
                {
                    result += _parameters[j + 1].Value * _covariates[index, j];
                }
            }

            return result;
        }

        /// <summary>
        /// Evaluates the linear function using provided covariate values directly.
        /// </summary>
        /// <param name="covariates">
        /// The covariate values for prediction. Can be <c>null</c> or empty for intercept-only.
        /// </param>
        /// <returns>
        /// The predicted value: β₀ + β₁×x₁ + β₂×x₂ + ... + βₚ×xₚ
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Thrown if the length of <paramref name="covariates"/> does not match <see cref="NumberOfCovariates"/>.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This method is useful for spatial prediction at ungauged locations where covariate values
        /// are known but there is no corresponding row in the stored covariate matrix.
        /// </para>
        /// <para>
        /// Example usage for spatial interpolation:
        /// </para>
        /// <code>
        /// // Predict GEV location parameter at an ungauged site
        /// double[] siteFeatures = { latitude, longitude, elevation };
        /// double predictedLocation = locationTrend.PredictWithCovariates(siteFeatures);
        /// </code>
        /// </remarks>
        public double PredictWithCovariates(double[]? covariates)
        {
            double result = _parameters[0].Value; // Intercept

            // If model has no covariates, return just the intercept
            if (NumberOfCovariates == 0)
            {
                return result;
            }

            // Validate covariate array
            if (covariates == null || covariates.Length == 0)
            {
                return result;
            }

            if (covariates.Length != NumberOfCovariates)
            {
                throw new ArgumentException(
                    $"Expected {NumberOfCovariates} covariate(s) but received {covariates.Length}.",
                    nameof(covariates));
            }

            // Compute linear combination
            for (int j = 0; j < covariates.Length; j++)
            {
                result += _parameters[j + 1].Value * covariates[j];
            }

            return result;
        }

        /// <inheritdoc/>
        public ITrendModel Clone()
        {
            // Clone covariate matrix if present
            double[,]? clonedCovariates = null;
            if (_covariates != null)
            {
                clonedCovariates = (double[,])_covariates.Clone();
            }

            var model = new GeneralLinearFunction(_ownerName, clonedCovariates)
            {
                _useDefaultFlatPriors = _useDefaultFlatPriors,
                _startIndex = _startIndex
            };

            // Clone parameters
            model._parameters = new List<ModelParameter>();
            for (int i = 0; i < NumberOfParameters; i++)
            {
                model._parameters.Add(_parameters[i].Clone());
            }

            return model;
        }

        /// <inheritdoc/>
        public XElement ToXElement()
        {
            var result = new XElement(nameof(ITrendModel));
            result.SetAttributeValue(nameof(OwnerName), OwnerName);
            result.SetAttributeValue(nameof(UseDefaultFlatPriors), UseDefaultFlatPriors.ToString());
            result.SetAttributeValue(nameof(Type), Type.ToString());
            result.SetAttributeValue(nameof(StartIndex), StartIndex.ToString(CultureInfo.InvariantCulture));

            // Serialize covariate matrix dimensions
            if (_covariates != null)
            {
                result.SetAttributeValue("CovariateRows", _covariates.GetLength(0).ToString(CultureInfo.InvariantCulture));
                result.SetAttributeValue("CovariateCols", _covariates.GetLength(1).ToString(CultureInfo.InvariantCulture));

                // Serialize covariate values as comma-separated string
                var values = new List<string>();
                for (int i = 0; i < _covariates.GetLength(0); i++)
                {
                    for (int j = 0; j < _covariates.GetLength(1); j++)
                    {
                        values.Add(_covariates[i, j].ToString(CultureInfo.InvariantCulture));
                    }
                }
                result.Add(new XElement("Covariates", string.Join(",", values)));
            }

            // Serialize parameters
            var parms = new XElement(nameof(Parameters));
            foreach (var p in _parameters)
            {
                parms.Add(p.ToXElement());
            }
            result.Add(parms);

            return result;
        }

        #endregion
    }
}
