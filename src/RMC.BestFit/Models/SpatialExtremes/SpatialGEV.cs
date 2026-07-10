using Numerics;
using Numerics.Data.Statistics;
using Numerics.Distributions;
using RMC.BestFit.Models.TrendFunctions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace RMC.BestFit.Models.SpatialExtremes
{
    /// <summary>
    /// Spatial Generalized Extreme Value (GEV) model following Renard's Bayesian Hierarchical Model framework.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This model implements a hierarchical structure for spatial extreme value analysis:
    /// </para>
    /// <para>
    /// <b>Level 1 (Data):</b> Y_ij | θ_j ~ GEV(ξ_j, α_j, κ_j)
    /// where Y_ij is observation i at site j, and θ_j = (ξ_j, α_j, κ_j) are the GEV parameters.
    /// </para>
    /// <para>
    /// <b>Level 2 (Spatial Process):</b>
    /// - Copula dependence: Gaussian copula with correlation function ρ(h)
    /// - Parameter spatial structure: ξ_j = exp(β₀ + Σβₖ*X_kj + ε_j) where ε ~ GP(0, Σ)
    /// - Similar structure for scale and shape parameters
    /// </para>
    /// <para>
    /// <b>Level 3 (Priors):</b> Bayesian priors on all hyperparameters (β, correlation parameters, σ)
    /// </para>
    /// <para>
    /// <b>Proper Confidence Interval Coverage:</b>
    /// For proper uncertainty quantification with correlated spatial data, enable both:
    /// (1) Gaussian copula for modeling spatial dependence between sites, and
    /// (2) Spatially correlated regression errors (latent GP errors).
    /// Call <see cref="ConfigureForProperCoverage"/> for the recommended Bayesian configuration.
    /// </para>
    /// <para>
    /// The latent regression errors ε_j are sampled as parameters in MCMC, providing fully Bayesian
    /// uncertainty propagation. Optional weighted likelihood can provide additional robustness
    /// when the copula may be mis-specified.
    /// </para>
    /// <para>
    ///     <b>Authors:</b>
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    ///     <b>References:</b>
    ///     - Renard, B., et al. (2006). Use of a Gaussian copula for multivariate extreme value analysis.
    ///       Journal of Hydrology, 315(1-4), 203-215.
    ///     - Cooley, D., Nychka, D., and Naveau, P. (2007). Bayesian spatial modeling of extreme
    ///       precipitation return levels. Journal of the American Statistical Association, 102(479), 824-840.
    ///     - Davison, A.C., Padoan, S.A., and Ribatet, M. (2012). Statistical modeling of spatial extremes.
    ///       Statistical Science, 27(2), 161-186.
    /// </para>
    /// </remarks>
    public class SpatialGEV : ModelBase, ISimulatable<double[]>
    {
        #region Construction

        /// <summary>
        /// Constructs a new spatial GEV model.
        /// </summary>
        /// <param name="atSiteData">The at-site data array [observations × sites].</param>
        /// <param name="coordinates">The site coordinates [sites × 2] as (X,Y) or (Lat,Lon).</param>
        /// <param name="location">The linear trend model for location parameter.</param>
        /// <param name="scale">The linear trend model for scale parameter.</param>
        /// <param name="shape">The linear trend model for shape parameter.</param>
        public SpatialGEV(double[,] atSiteData, double[,] coordinates,
                          GeneralLinearFunction location, GeneralLinearFunction scale, GeneralLinearFunction shape)
        {
            if (atSiteData == null)
                throw new ArgumentNullException(nameof(atSiteData));
            if (coordinates == null)
                throw new ArgumentNullException(nameof(coordinates));
            if (atSiteData.GetLength(1) != coordinates.GetLength(0))
                throw new ArgumentException("Number of sites in data must match number of coordinates.");

            AtSiteData = atSiteData;
            Coordinates = coordinates;
            Location = location ?? throw new ArgumentNullException(nameof(location));
            Scale = scale ?? throw new ArgumentNullException(nameof(scale));
            Shape = shape ?? throw new ArgumentNullException(nameof(shape));

            // Initialize options
            UseCopulaDependence = false;
            UseLocationErrors = false;
            UseScaleErrors = false;
            UseShapeErrors = false;
            UseLogLinkForLocation = true;  // Default to log-link for location
            UseLogLinkForScale = true;     // Default to log-link for scale

            // Initialize weights to 1.0 (equal weighting)
            SiteWeights = new double[Sites];
            for (int i = 0; i < Sites; i++)
                SiteWeights[i] = 1.0;

            SetDefaultParameters();
        }

        /// <summary>
        /// Constructs a spatial GEV model from a serialized <see cref="XElement"/>.
        /// </summary>
        /// <remarks>
        /// Following the BivariateDistribution paired-ctor pattern, the data
        /// (<paramref name="atSiteData"/>, <paramref name="coordinates"/>) and the
        /// trend models (<paramref name="location"/>, <paramref name="scale"/>,
        /// <paramref name="shape"/>) are passed in by the caller — they are not
        /// round-tripped through the XElement because the data is the single source
        /// of truth and the trend models carry their own covariates and serialization
        /// schema. Scalar configuration (link flags, error flags, copula flag, site
        /// weights, parameter values) is read from the XElement.
        /// </remarks>
        /// <param name="atSiteData">The at-site data array [observations × sites].</param>
        /// <param name="coordinates">The site coordinates [sites × 2].</param>
        /// <param name="location">The location trend model (already deserialized).</param>
        /// <param name="scale">The scale trend model.</param>
        /// <param name="shape">The shape trend model.</param>
        /// <param name="xElement">The serialized configuration.</param>
        public SpatialGEV(double[,] atSiteData, double[,] coordinates,
                          GeneralLinearFunction location, GeneralLinearFunction scale, GeneralLinearFunction shape,
                          XElement xElement)
            : this(atSiteData, coordinates, location, scale, shape)
        {
            if (xElement == null) return;

            bool TryParseBool(string name, out bool value)
            {
                var attr = xElement.Attribute(name);
                value = false;
                return attr != null && bool.TryParse(attr.Value, out value);
            }

            if (TryParseBool(nameof(UseCopulaDependence), out var ucd)) UseCopulaDependence = ucd;
            if (TryParseBool(nameof(UseLocationErrors), out var ule)) UseLocationErrors = ule;
            if (TryParseBool(nameof(UseScaleErrors), out var use)) UseScaleErrors = use;
            if (TryParseBool(nameof(UseShapeErrors), out var ushp)) UseShapeErrors = ushp;
            if (TryParseBool(nameof(UseLogLinkForLocation), out var ull)) UseLogLinkForLocation = ull;
            if (TryParseBool(nameof(UseLogLinkForScale), out var ulls)) UseLogLinkForScale = ulls;

            // Site weights
            var weightsElem = xElement.Element(nameof(SiteWeights));
            if (weightsElem != null && !string.IsNullOrWhiteSpace(weightsElem.Value))
            {
                var parts = weightsElem.Value.Split(',');
                if (parts.Length == Sites)
                {
                    var w = new double[Sites];
                    bool ok = true;
                    for (int i = 0; i < Sites; i++)
                        if (!double.TryParse(parts[i], NumberStyles.Any, CultureInfo.InvariantCulture, out w[i]))
                        { ok = false; break; }
                    if (ok) SiteWeights = w;
                }
            }

            // Parameter values (preserve bounds and priors set by SetDefaultParameters
            // chained above; only restore Value from XML).
            var parmsElem = xElement.Element(nameof(Parameters));
            if (parmsElem != null)
            {
                var paramElems = parmsElem.Elements().ToList();
                int n = Math.Min(paramElems.Count, Parameters.Count);
                for (int i = 0; i < n; i++)
                {
                    var valueAttr = paramElems[i].Attribute(nameof(ModelParameter.Value));
                    if (valueAttr != null && double.TryParse(valueAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
                        Parameters[i].Value = v;
                }
            }
        }

        #endregion

        #region Members

        /// <summary>
        /// Gets the at-site data [observations × sites].
        /// </summary>
        public double[,] AtSiteData { get; private set; }

        /// <summary>
        /// Gets the site coordinates [sites × 2].
        /// </summary>
        public double[,] Coordinates { get; private set; }

        /// <summary>
        /// Gets the number of sites.
        /// </summary>
        public int Sites => AtSiteData.GetLength(1);

        /// <summary>
        /// Gets the number of observations (rows in data matrix).
        /// </summary>
        public int Observations => AtSiteData.GetLength(0);

        /// <summary>
        /// Gets or sets the linear trend model for location parameter.
        /// </summary>
        [Category("GEV Parameters")]
        [DisplayName("Location Trend")]
        [Description("Linear trend model for the GEV location parameter.")]
        public GeneralLinearFunction Location { get; set; }

        /// <summary>
        /// Gets or sets the linear trend model for scale parameter.
        /// </summary>
        [Category("GEV Parameters")]
        [DisplayName("Scale Trend")]
        [Description("Linear trend model for the GEV scale parameter.")]
        public GeneralLinearFunction Scale { get; set; }

        /// <summary>
        /// Gets or sets the linear trend model for shape parameter.
        /// </summary>
        [Category("GEV Parameters")]
        [DisplayName("Shape Trend")]
        [Description("Linear trend model for the GEV shape parameter.")]
        public GeneralLinearFunction Shape { get; set; }

        /// <summary>
        /// Gets or sets the Gaussian copula for spatial dependence.
        /// </summary>
        [Category("Spatial Structure")]
        [DisplayName("Copula Dependence")]
        [Description("Gaussian copula for modeling spatial dependence between sites.")]
        public GaussianCopula SpatialDependence { get; set; } = null!;

        /// <summary>
        /// Gets or sets the spatial regression errors for location parameter.
        /// </summary>
        [Category("Spatial Structure")]
        [DisplayName("Location Errors")]
        [Description("Spatially correlated errors in location parameter.")]
        public SpatialRegressionErrors LocationErrors { get; set; } = null!;

        /// <summary>
        /// Gets or sets the spatial regression errors for scale parameter.
        /// </summary>
        [Category("Spatial Structure")]
        [DisplayName("Scale Errors")]
        [Description("Spatially correlated errors in scale parameter.")]
        public SpatialRegressionErrors ScaleErrors { get; set; } = null!;

        /// <summary>
        /// Gets or sets the spatial regression errors for shape parameter.
        /// </summary>
        [Category("Spatial Structure")]
        [DisplayName("Shape Errors")]
        [Description("Spatially correlated errors in shape parameter.")]
        public SpatialRegressionErrors ShapeErrors { get; set; } = null!;

        /// <summary>
        /// Gets or sets whether to use Gaussian copula for spatial dependence.
        /// </summary>
        [Category("Options")]
        [DisplayName("Use Copula Dependence")]
        [Description("Enable Gaussian copula to model spatial dependence between observations.")]
        public bool UseCopulaDependence { get; set; }

        /// <summary>
        /// Gets or sets whether to use spatial regression errors for location.
        /// </summary>
        [Category("Options")]
        [DisplayName("Use Location Errors")]
        [Description("Enable spatially correlated errors in location parameter.")]
        public bool UseLocationErrors { get; set; }

        /// <summary>
        /// Gets or sets whether to use spatial regression errors for scale.
        /// </summary>
        [Category("Options")]
        [DisplayName("Use Scale Errors")]
        [Description("Enable spatially correlated errors in scale parameter.")]
        public bool UseScaleErrors { get; set; }

        /// <summary>
        /// Gets or sets whether to use spatial regression errors for shape.
        /// </summary>
        [Category("Options")]
        [DisplayName("Use Shape Errors")]
        [Description("Enable spatially correlated errors in shape parameter.")]
        public bool UseShapeErrors { get; set; }

        /// <summary>
        /// Gets or sets whether to use log-link for location parameter.
        /// </summary>
        [Category("Options")]
        [DisplayName("Log-Link for Location")]
        [Description("Use log-link for location: ξ = exp(β₀ + ...). If false, uses identity link.")]
        public bool UseLogLinkForLocation { get; set; }

        /// <summary>
        /// Gets or sets whether to use log-link for scale parameter.
        /// </summary>
        [Category("Options")]
        [DisplayName("Log-Link for Scale")]
        [Description("Use log-link for scale: α = exp(β₀ + ...). If false, uses identity link with α > 0 constraint.")]
        public bool UseLogLinkForScale { get; set; }

        /// <summary>
        /// Gets or sets the site-specific weights for weighted likelihood.
        /// </summary>
        [Category("Options")]
        [DisplayName("Site Weights")]
        [Description("Weight for each site in likelihood computation. Default is 1.0 for all sites.")]
        public double[] SiteWeights { get; set; }

        #endregion

        #region Methods

        /// <inheritdoc/>
        public override void SetDefaultParameters()
        {
            // Remove old handlers from the flat Parameters list and any nested
            // child-model parameter collections so they don't outlive the rebuild.
            if (Parameters.Count > 0)
            {
                for (int i = 0; i < Parameters.Count; i++)
                    Parameters[i].PropertyChanged -= Parameter_PropertyChanged;
            }

            if (Location.Parameters is not null)
            {
                for (int i = 0; i < Location.Parameters.Count; i++)
                    Location.Parameters[i].PropertyChanged -= Parameter_PropertyChanged;
            }

            if (Scale.Parameters is not null)
            {
                for (int i = 0; i < Scale.Parameters.Count; i++)
                    Scale.Parameters[i].PropertyChanged -= Parameter_PropertyChanged;
            }

            if (Shape.Parameters is not null)
            {
                for (int i = 0; i < Shape.Parameters.Count; i++)
                    Shape.Parameters[i].PropertyChanged -= Parameter_PropertyChanged;
            }

            if (SpatialDependence?.Parameters != null)
            {
                for (int i = 0; i < SpatialDependence.Parameters.Count; i++)
                    SpatialDependence.Parameters[i].PropertyChanged -= Parameter_PropertyChanged;
            }

            if (LocationErrors?.Parameters != null)
            {
                for (int i = 0; i < LocationErrors.Parameters.Count; i++)
                    LocationErrors.Parameters[i].PropertyChanged -= Parameter_PropertyChanged;
            }

            if (ScaleErrors?.Parameters != null)
            {
                for (int i = 0; i < ScaleErrors.Parameters.Count; i++)
                    ScaleErrors.Parameters[i].PropertyChanged -= Parameter_PropertyChanged;
            }

            if (ShapeErrors?.Parameters != null)
            {
                for (int i = 0; i < ShapeErrors.Parameters.Count; i++)
                    ShapeErrors.Parameters[i].PropertyChanged -= Parameter_PropertyChanged;
            }

            // Compute initial parameter estimates from at-site data moments. We use
            // method-of-moments style estimates (mean for location, std for scale,
            // sign of skew for shape) rather than per-site MLE. Per-site MLE was
            // accurate but prohibitively expensive when called inside Clone() during
            // every MCMC iteration; data-moment bounds are good enough to seed the
            // sampler and an order of magnitude cheaper.
            double nt = 0;
            double sumLoc = 0, sumScl = 0, sumShp = 0;
            var locList = new List<double>();
            var sclList = new List<double>();
            var shpList = new List<double>();

            for (int j = 0; j < Sites; j++)
            {
                var siteData = new List<double>();
                for (int i = 0; i < Observations; i++)
                {
                    if (!double.IsNaN(AtSiteData[i, j]))
                        siteData.Add(AtSiteData[i, j]);
                }

                if (siteData.Count > 0)
                {
                    nt += siteData.Count;
                    // Location estimate: sample mean.
                    double mean = 0.0;
                    for (int k = 0; k < siteData.Count; k++) mean += siteData[k];
                    mean /= siteData.Count;
                    // Scale estimate: sample std.
                    double sumSq = 0.0;
                    for (int k = 0; k < siteData.Count; k++) sumSq += (siteData[k] - mean) * (siteData[k] - mean);
                    double std = siteData.Count > 1 ? Math.Sqrt(sumSq / (siteData.Count - 1)) : Math.Max(Math.Abs(mean), 1e-3);
                    // Shape estimate: weak default near 0; MCMC explores [-0.5, 0.5].
                    double shape = 0.0;

                    sumLoc += mean * siteData.Count;
                    sumScl += std * siteData.Count;
                    sumShp += shape * siteData.Count;

                    locList.Add(mean);
                    sclList.Add(std);
                    shpList.Add(shape);
                }
            }

            double avgLoc = nt > 0 ? sumLoc / nt : 0.0;
            double avgScl = nt > 0 ? Math.Max(sumScl / nt, 1e-3) : 1.0;
            double avgShp = nt > 0 ? sumShp / nt : 0.0;

            // Set location parameter
            var locationParams = Location!.Parameters!;
            var scaleParams = Scale!.Parameters!;
            var shapeParams = Shape!.Parameters!;

            if (UseLogLinkForLocation)
            {
                locationParams[0].Value = Math.Log(Math.Max(avgLoc, 0.01));
                locationParams[0].LowerBound = Math.Log(0.01);
                locationParams[0].UpperBound = Math.Ceiling(Math.Log(Math.Abs(avgLoc)) + 3.0);
            }
            else
            {
                locationParams[0].Value = avgLoc;
                double range = Math.Pow(10, Math.Ceiling(Math.Log10(Math.Abs(avgLoc)) + 1));
                locationParams[0].LowerBound = -range;
                locationParams[0].UpperBound = range;
            }
            locationParams[0].PriorDistribution = new Uniform(
                locationParams[0].LowerBound,
                locationParams[0].UpperBound);

            // Set scale parameter
            if (UseLogLinkForScale)
            {
                scaleParams[0].Value = Math.Log(Math.Max(avgScl, 0.01));
                scaleParams[0].LowerBound = Math.Log(0.01);
                scaleParams[0].UpperBound = Math.Ceiling(Math.Log(Math.Abs(avgScl)) + 3.0);
            }
            else
            {
                scaleParams[0].Value = avgScl;
                scaleParams[0].LowerBound = Tools.DoubleMachineEpsilon;
                scaleParams[0].UpperBound = Math.Pow(10, Math.Ceiling(Math.Log10(Math.Abs(avgScl)) + 1));
            }
            scaleParams[0].PriorDistribution = new Uniform(
                scaleParams[0].LowerBound,
                scaleParams[0].UpperBound);

            // Set shape parameter
            shapeParams[0].Value = avgShp;
            shapeParams[0].LowerBound = -0.5;
            shapeParams[0].UpperBound = 0.5;
            shapeParams[0].PriorDistribution = new Uniform(-0.5, 0.5);

            // Build parameter list via backing field to avoid a spurious empty-list
            // nameof(Parameters) raise from the base setter before the list is populated.
            _parameters = new List<ModelParameter>();

            // Add copula parameters if enabled
            if (UseCopulaDependence && SpatialDependence != null)
                _parameters.AddRange(SpatialDependence.Parameters);

            // Add GEV trend parameters
            _parameters.AddRange(locationParams);
            _parameters.AddRange(scaleParams);
            _parameters.AddRange(shapeParams);

            // Add spatial error parameters if enabled
            if (UseLocationErrors && LocationErrors != null)
            {
                double maxLocError = Math.Ceiling((Statistics.Maximum(locList.ToArray()) - avgLoc) * 3);
                LocationErrors.SetDefaultParameters(Math.Max(maxLocError, 1.0));
                _parameters.AddRange(LocationErrors.Parameters);
            }

            if (UseScaleErrors && ScaleErrors != null)
            {
                double maxSclError = Math.Ceiling((Statistics.Maximum(sclList.ToArray()) - avgScl) * 3);
                ScaleErrors.SetDefaultParameters(Math.Max(maxSclError, 1.0));
                _parameters.AddRange(ScaleErrors.Parameters);
            }

            if (UseShapeErrors && ShapeErrors != null)
            {
                double maxShpError = Math.Ceiling((Statistics.Maximum(shpList.ToArray()) - avgShp) * 3);
                ShapeErrors.SetDefaultParameters(Math.Max(maxShpError, 0.5));
                _parameters.AddRange(ShapeErrors.Parameters);
            }

            // Attach handlers to every freshly-assembled parameter.
            for (int i = 0; i < Parameters.Count; i++)
                Parameters[i].PropertyChanged += Parameter_PropertyChanged;

            RaisePropertyChange(nameof(SetDefaultParameters));
        }

        /// <inheritdoc/>
        public override void SetParameterValues(IList<double> parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));
            if (parameters.Count != NumberOfParameters)
                throw new ArgumentException($"Expected {NumberOfParameters} parameters but got {parameters.Count}.");

            int index = 0;

            // Set copula parameters
            if (UseCopulaDependence && SpatialDependence != null)
            {
                var copParams = new List<double>();
                for (int i = 0; i < SpatialDependence.NumberOfParameters; i++)
                    copParams.Add(parameters[index++]);
                SpatialDependence.SetParameterValues(copParams);
            }

            // Set location parameters
            var locParams = new List<double>();
            for (int i = 0; i < Location.NumberOfParameters; i++)
                locParams.Add(parameters[index++]);
            Location.SetParameterValues(locParams);

            // Set scale parameters
            var sclParams = new List<double>();
            for (int i = 0; i < Scale.NumberOfParameters; i++)
                sclParams.Add(parameters[index++]);
            Scale.SetParameterValues(sclParams);

            // Set shape parameters
            var shpParams = new List<double>();
            for (int i = 0; i < Shape.NumberOfParameters; i++)
                shpParams.Add(parameters[index++]);
            Shape.SetParameterValues(shpParams);

            // Set location error parameters
            if (UseLocationErrors && LocationErrors != null)
            {
                var locErrParams = new List<double>();
                for (int i = 0; i < LocationErrors.NumberOfParameters; i++)
                    locErrParams.Add(parameters[index++]);
                LocationErrors.SetParameterValues(locErrParams);
            }

            // Set scale error parameters
            if (UseScaleErrors && ScaleErrors != null)
            {
                var sclErrParams = new List<double>();
                for (int i = 0; i < ScaleErrors.NumberOfParameters; i++)
                    sclErrParams.Add(parameters[index++]);
                ScaleErrors.SetParameterValues(sclErrParams);
            }

            // Set shape error parameters
            if (UseShapeErrors && ShapeErrors != null)
            {
                var shpErrParams = new List<double>();
                for (int i = 0; i < ShapeErrors.NumberOfParameters; i++)
                    shpErrParams.Add(parameters[index++]);
                ShapeErrors.SetParameterValues(shpErrParams);
            }
        }

        /// <summary>
        /// Gets the GEV parameters for a specific site.
        /// </summary>
        /// <param name="siteIndex">The site index (0-based).</param>
        /// <returns>Array [ξ, α, κ] of GEV parameters.</returns>
        public double[] GetGEVParameters(int siteIndex)
        {
            if (siteIndex < 0 || siteIndex >= Sites)
                throw new ArgumentOutOfRangeException(nameof(siteIndex));

            // Location
            double locTrend = Location.Predict(siteIndex);
            double locError = UseLocationErrors && LocationErrors != null ? LocationErrors.GetError(siteIndex) : 0.0;
            double xi = UseLogLinkForLocation ? Math.Exp(locTrend + locError) : locTrend + locError;

            // Scale
            double sclTrend = Scale.Predict(siteIndex);
            double sclError = UseScaleErrors && ScaleErrors != null ? ScaleErrors.GetError(siteIndex) : 0.0;
            double alpha = UseLogLinkForScale ? Math.Exp(sclTrend + sclError) : Math.Max(sclTrend + sclError, Tools.DoubleMachineEpsilon);

            // Shape
            double kappa = Shape.Predict(siteIndex);
            if (UseShapeErrors && ShapeErrors != null)
                kappa += ShapeErrors.GetError(siteIndex);

            return new double[] { xi, alpha, kappa };
        }

        /// <summary>
        /// Gets GEV parameters for a site using local (cloned) trend models.
        /// Thread-safe helper for use in parallel MCMC chains.
        /// </summary>
        /// <param name="siteIndex">The site index (0-based).</param>
        /// <param name="location">Local copy of location trend model.</param>
        /// <param name="scale">Local copy of scale trend model.</param>
        /// <param name="shape">Local copy of shape trend model.</param>
        /// <param name="locErrors">Local copy of location errors (may be null).</param>
        /// <param name="sclErrors">Local copy of scale errors (may be null).</param>
        /// <param name="shpErrors">Local copy of shape errors (may be null).</param>
        /// <returns>Array [ξ, α, κ] of GEV parameters.</returns>
        private double[] GetGEVParametersLocal(int siteIndex,
            GeneralLinearFunction location, GeneralLinearFunction scale, GeneralLinearFunction shape,
            SpatialRegressionErrors? locErrors, SpatialRegressionErrors? sclErrors, SpatialRegressionErrors? shpErrors)
        {
            // Location
            double locTrend = location.Predict(siteIndex);
            double locError = UseLocationErrors && locErrors != null ? locErrors.GetError(siteIndex) : 0.0;
            double xi = UseLogLinkForLocation ? Math.Exp(locTrend + locError) : locTrend + locError;

            // Scale
            double sclTrend = scale.Predict(siteIndex);
            double sclError = UseScaleErrors && sclErrors != null ? sclErrors.GetError(siteIndex) : 0.0;
            double alpha = UseLogLinkForScale ? Math.Exp(sclTrend + sclError) : Math.Max(sclTrend + sclError, Tools.DoubleMachineEpsilon);

            // Shape
            double kappa = shape.Predict(siteIndex);
            if (UseShapeErrors && shpErrors != null)
                kappa += shpErrors.GetError(siteIndex);

            return new double[] { xi, alpha, kappa };
        }

        /// <summary>
        /// Computes log-likelihood using only local copies of models.
        /// This method is thread-safe for parallel MCMC chains.
        /// </summary>
        /// <param name="parameters">The parameter values.</param>
        /// <returns>The log-likelihood value.</returns>
        private double ComputeLogLikelihoodInternal(double[] parameters)
        {
            // Snapshot SiteWeights once per LL evaluation. RunCrossValidationAsync
            // mutates the SiteWeights field between LOOCV iterations; the snapshot
            // ensures all reads inside this call see a consistent vector even if a
            // future change overlaps LOOCV iterations or runs MCMC chains in
            // parallel with weight updates.
            var localSiteWeights = SiteWeights != null ? (double[])SiteWeights.Clone() : new double[Sites];

            // Clone trend models for thread-local computation
            var localLocation = (GeneralLinearFunction)Location.Clone();
            var localScale = (GeneralLinearFunction)Scale.Clone();
            var localShape = (GeneralLinearFunction)Shape.Clone();

            // Clone copula if used
            GaussianCopula? localCopula = null;
            if (UseCopulaDependence && SpatialDependence != null)
                localCopula = SpatialDependence.Clone();

            // Clone error models if used
            SpatialRegressionErrors? localLocErrors = null;
            SpatialRegressionErrors? localSclErrors = null;
            SpatialRegressionErrors? localShpErrors = null;

            if (UseLocationErrors && LocationErrors != null)
                localLocErrors = LocationErrors.Clone();
            if (UseScaleErrors && ScaleErrors != null)
                localSclErrors = ScaleErrors.Clone();
            if (UseShapeErrors && ShapeErrors != null)
                localShpErrors = ShapeErrors.Clone();

            // Set parameter values on local copies
            int index = 0;

            // Set copula parameters
            if (UseCopulaDependence && localCopula != null)
            {
                var copParams = new List<double>();
                for (int i = 0; i < localCopula.NumberOfParameters; i++)
                    copParams.Add(parameters[index++]);
                localCopula.SetParameterValues(copParams);
            }

            // Set location parameters
            var locParams = new List<double>();
            for (int i = 0; i < localLocation.NumberOfParameters; i++)
                locParams.Add(parameters[index++]);
            localLocation.SetParameterValues(locParams);

            // Set scale parameters
            var sclParams = new List<double>();
            for (int i = 0; i < localScale.NumberOfParameters; i++)
                sclParams.Add(parameters[index++]);
            localScale.SetParameterValues(sclParams);

            // Set shape parameters
            var shpParams = new List<double>();
            for (int i = 0; i < localShape.NumberOfParameters; i++)
                shpParams.Add(parameters[index++]);
            localShape.SetParameterValues(shpParams);

            // Set location error parameters
            if (UseLocationErrors && localLocErrors != null)
            {
                var locErrParams = new List<double>();
                for (int i = 0; i < localLocErrors.NumberOfParameters; i++)
                    locErrParams.Add(parameters[index++]);
                localLocErrors.SetParameterValues(locErrParams);
            }

            // Set scale error parameters
            if (UseScaleErrors && localSclErrors != null)
            {
                var sclErrParams = new List<double>();
                for (int i = 0; i < localSclErrors.NumberOfParameters; i++)
                    sclErrParams.Add(parameters[index++]);
                localSclErrors.SetParameterValues(sclErrParams);
            }

            // Set shape error parameters
            if (UseShapeErrors && localShpErrors != null)
            {
                var shpErrParams = new List<double>();
                for (int i = 0; i < localShpErrors.NumberOfParameters; i++)
                    shpErrParams.Add(parameters[index++]);
                localShpErrors.SetParameterValues(shpErrParams);
            }

            // Create local GEV for evaluation
            var localGEV = new GeneralizedExtremeValue();
            double logLH = 0.0;

            // Data likelihood with optional copula dependence
            if (UseCopulaDependence && localCopula != null)
            {
                // Likelihood with copula: L = ∏_i [∏_j f_j(y_ij)] * c(u_i1, ..., u_in)
                for (int i = 0; i < Observations; i++)
                {
                    var z = new double[Sites];
                    bool hasData = false;

                    for (int j = 0; j < Sites; j++)
                    {
                        if (!double.IsNaN(AtSiteData[i, j]))
                        {
                            hasData = true;
                            var gevParams = GetGEVParametersLocal(j, localLocation, localScale, localShape,
                                localLocErrors, localSclErrors, localShpErrors);
                            if (gevParams[1] <= 0) // Scale must be positive
                                return double.NegativeInfinity;

                            localGEV.SetParameters(gevParams);

                            // Marginal likelihood
                            double margLogLH = localGEV.LogPDF(AtSiteData[i, j]);
                            if (double.IsInfinity(margLogLH) || double.IsNaN(margLogLH))
                                return double.NegativeInfinity;

                            logLH += localSiteWeights[j] * margLogLH;

                            // Transform to standard normal for copula
                            double u = localGEV.CDF(AtSiteData[i, j]);
                            z[j] = Normal.StandardZ(u);
                        }
                        else
                        {
                            z[j] = 0.0; // Placeholder for missing data
                        }
                    }

                    // Add copula contribution if we have data
                    if (hasData)
                    {
                        double copLogLH = localCopula.LogPDF(z);
                        if (double.IsInfinity(copLogLH) || double.IsNaN(copLogLH))
                            return double.NegativeInfinity;
                        logLH += copLogLH;
                    }
                }
            }
            else
            {
                // Standard likelihood without copula
                for (int i = 0; i < Observations; i++)
                {
                    for (int j = 0; j < Sites; j++)
                    {
                        if (!double.IsNaN(AtSiteData[i, j]))
                        {
                            var gevParams = GetGEVParametersLocal(j, localLocation, localScale, localShape,
                                localLocErrors, localSclErrors, localShpErrors);
                            if (gevParams[1] <= 0) // Scale must be positive
                                return double.NegativeInfinity;

                            localGEV.SetParameters(gevParams);
                            double margLogLH = localGEV.LogPDF(AtSiteData[i, j]);

                            if (double.IsInfinity(margLogLH) || double.IsNaN(margLogLH))
                                return double.NegativeInfinity;

                            logLH += localSiteWeights[j] * margLogLH;
                        }
                    }
                }
            }

            // Add spatial error contributions (Gaussian process priors)
            if (UseLocationErrors && localLocErrors != null)
            {
                double locErrLogLH = localLocErrors.LogPDF();
                if (double.IsInfinity(locErrLogLH) || double.IsNaN(locErrLogLH))
                    return double.NegativeInfinity;
                logLH += locErrLogLH;
            }

            if (UseScaleErrors && localSclErrors != null)
            {
                double sclErrLogLH = localSclErrors.LogPDF();
                if (double.IsInfinity(sclErrLogLH) || double.IsNaN(sclErrLogLH))
                    return double.NegativeInfinity;
                logLH += sclErrLogLH;
            }

            if (UseShapeErrors && localShpErrors != null)
            {
                double shpErrLogLH = localShpErrors.LogPDF();
                if (double.IsInfinity(shpErrLogLH) || double.IsNaN(shpErrLogLH))
                    return double.NegativeInfinity;
                logLH += shpErrLogLH;
            }

            return logLH;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// <para>
        /// This method is thread-safe for use with parallel MCMC chains.
        /// All computation uses local copies of trend models and the GEV distribution.
        /// Returns only the data likelihood (not parameter priors). The base class
        /// <see cref="ModelBase.LogLikelihood"/> combines this with parameter priors.
        /// </para>
        /// <para>
        /// <b>Spatial-error decomposition (non-canonical):</b> This method intentionally INCLUDES
        /// the Gaussian-process spatial-error log densities (location / scale / shape errors) in
        /// the returned data likelihood — historically they have been treated as "data" so that
        /// the marginal site likelihood plus spatial dependence is a single integrable quantity.
        /// As a consequence:
        /// <list type="bullet">
        /// <item><description><see cref="PointwiseDataLogLikelihoodComponents"/> does NOT add the spatial-error
        /// contributions (it is per-site, not per-process), so its <c>Sum()</c> does NOT match
        /// <see cref="DataLogLikelihood"/>.</description></item>
        /// <item><description><see cref="PointwisePriorLogLikelihood"/> DOES emit the spatial-error contributions as
        /// <c>PriorComponentType.SpatialError</c>, so its <c>Sum()</c> does NOT match <see cref="ModelBase.PriorLogLikelihood"/>
        /// (which only sums parameter priors).</description></item>
        /// <item><description>WAIC and LOO-CV (computed from <see cref="PointwiseDataLogLikelihoodComponents"/>) therefore
        /// EXCLUDE the spatial-error term — they reflect the marginal site-by-site predictive
        /// performance only, not the joint spatial process.</description></item>
        /// <item><description>A consumer that adds <c>DataLogLikelihood + PointwisePriorLogLikelihood.Sum()</c> would
        /// double-count the spatial-error term. Always compute the joint via the inherited
        /// <see cref="ModelBase.LogLikelihood"/> instead.</description></item>
        /// </list>
        /// This asymmetry is a known design tradeoff. Future work may move the spatial errors
        /// fully into <see cref="PointwiseDataLogLikelihoodComponents"/> so WAIC / LOO-CV reflect
        /// the joint process, OR fully into a <c>PriorLogLikelihood</c> override so the four
        /// methods compose by the canonical <c>LogLikelihood == DataLogLikelihood + PriorLogLikelihood</c>
        /// identity. Either choice changes the semantics of model comparison.
        /// </para>
        /// </remarks>
        public override double DataLogLikelihood(double[] parameters)
        {
            // Validate parameter count
            if (parameters == null || parameters.Length != NumberOfParameters)
                return double.NegativeInfinity;

            // Delegate to thread-safe internal method
            return ComputeLogLikelihoodInternal(parameters);
        }

        /// <inheritdoc/>
        /// <remarks>
        /// This method is thread-safe for use with parallel MCMC chains.
        /// All computation uses local copies of trend models and the GEV distribution.
        /// </remarks>
        public override double[] PointwiseDataLogLikelihood(double[] parameters)
        {
            // Validate parameters
            if (parameters == null || parameters.Length != NumberOfParameters)
            {
                var errorResult = new double[Observations];
                for (int i = 0; i < Observations; i++)
                    errorResult[i] = double.NegativeInfinity;
                return errorResult;
            }

            // Snapshot SiteWeights once per LL evaluation; see ComputeLogLikelihoodInternal.
            var localSiteWeights = SiteWeights != null ? (double[])SiteWeights.Clone() : new double[Sites];

            // Clone trend models for thread-local computation
            var localLocation = (GeneralLinearFunction)Location.Clone();
            var localScale = (GeneralLinearFunction)Scale.Clone();
            var localShape = (GeneralLinearFunction)Shape.Clone();

            // Clone copula if used
            GaussianCopula? localCopula = null;
            if (UseCopulaDependence && SpatialDependence != null)
                localCopula = SpatialDependence.Clone();

            // Clone error models if used
            SpatialRegressionErrors? localLocErrors = null;
            SpatialRegressionErrors? localSclErrors = null;
            SpatialRegressionErrors? localShpErrors = null;

            if (UseLocationErrors && LocationErrors != null)
                localLocErrors = LocationErrors.Clone();
            if (UseScaleErrors && ScaleErrors != null)
                localSclErrors = ScaleErrors.Clone();
            if (UseShapeErrors && ShapeErrors != null)
                localShpErrors = ShapeErrors.Clone();

            // Set parameter values on local copies
            int index = 0;

            // Set copula parameters
            if (UseCopulaDependence && localCopula != null)
            {
                var copParams = new List<double>();
                for (int i = 0; i < localCopula.NumberOfParameters; i++)
                    copParams.Add(parameters[index++]);
                localCopula.SetParameterValues(copParams);
            }

            // Set location parameters
            var locParams = new List<double>();
            for (int i = 0; i < localLocation.NumberOfParameters; i++)
                locParams.Add(parameters[index++]);
            localLocation.SetParameterValues(locParams);

            // Set scale parameters
            var sclParams = new List<double>();
            for (int i = 0; i < localScale.NumberOfParameters; i++)
                sclParams.Add(parameters[index++]);
            localScale.SetParameterValues(sclParams);

            // Set shape parameters
            var shpParams = new List<double>();
            for (int i = 0; i < localShape.NumberOfParameters; i++)
                shpParams.Add(parameters[index++]);
            localShape.SetParameterValues(shpParams);

            // Set location error parameters
            if (UseLocationErrors && localLocErrors != null)
            {
                var locErrParams = new List<double>();
                for (int i = 0; i < localLocErrors.NumberOfParameters; i++)
                    locErrParams.Add(parameters[index++]);
                localLocErrors.SetParameterValues(locErrParams);
            }

            // Set scale error parameters
            if (UseScaleErrors && localSclErrors != null)
            {
                var sclErrParams = new List<double>();
                for (int i = 0; i < localSclErrors.NumberOfParameters; i++)
                    sclErrParams.Add(parameters[index++]);
                localSclErrors.SetParameterValues(sclErrParams);
            }

            // Set shape error parameters
            if (UseShapeErrors && localShpErrors != null)
            {
                var shpErrParams = new List<double>();
                for (int i = 0; i < localShpErrors.NumberOfParameters; i++)
                    shpErrParams.Add(parameters[index++]);
                localShpErrors.SetParameterValues(shpErrParams);
            }

            // Create local GEV for evaluation
            var localGEV = new GeneralizedExtremeValue();
            var result = new double[Observations];

            // Cache GEV parameters for each site
            var gevParamsCache = new double[Sites][];
            for (int j = 0; j < Sites; j++)
            {
                gevParamsCache[j] = GetGEVParametersLocal(j, localLocation, localScale, localShape,
                    localLocErrors, localSclErrors, localShpErrors);
            }

            if (UseCopulaDependence && localCopula != null)
            {
                // Likelihood with copula: each observation includes marginal PDFs and copula contribution
                for (int i = 0; i < Observations; i++)
                {
                    double obsLogLH = 0.0;
                    var z = new double[Sites];
                    bool hasData = false;
                    bool valid = true;

                    for (int j = 0; j < Sites; j++)
                    {
                        if (!double.IsNaN(AtSiteData[i, j]))
                        {
                            hasData = true;
                            var gevParams = gevParamsCache[j];
                            if (gevParams[1] <= 0) // Scale must be positive
                            {
                                valid = false;
                                break;
                            }

                            localGEV.SetParameters(gevParams);

                            // Marginal likelihood
                            double margLogLH = localGEV.LogPDF(AtSiteData[i, j]);
                            if (double.IsInfinity(margLogLH) || double.IsNaN(margLogLH))
                            {
                                valid = false;
                                break;
                            }

                            obsLogLH += localSiteWeights[j] * margLogLH;

                            // Transform to standard normal for copula
                            double u = localGEV.CDF(AtSiteData[i, j]);
                            z[j] = Normal.StandardZ(u);
                        }
                        else
                        {
                            z[j] = 0.0; // Placeholder for missing data
                        }
                    }

                    if (!valid)
                    {
                        result[i] = double.NegativeInfinity;
                        continue;
                    }

                    // Add copula contribution if we have data
                    if (hasData)
                    {
                        double copLogLH = localCopula.LogPDF(z);
                        if (double.IsInfinity(copLogLH) || double.IsNaN(copLogLH))
                        {
                            result[i] = double.NegativeInfinity;
                            continue;
                        }
                        obsLogLH += copLogLH;
                    }

                    result[i] = obsLogLH;
                }
            }
            else
            {
                // Standard likelihood without copula
                for (int i = 0; i < Observations; i++)
                {
                    double obsLogLH = 0.0;
                    bool valid = true;

                    for (int j = 0; j < Sites; j++)
                    {
                        if (!double.IsNaN(AtSiteData[i, j]))
                        {
                            var gevParams = gevParamsCache[j];
                            if (gevParams[1] <= 0) // Scale must be positive
                            {
                                valid = false;
                                break;
                            }

                            localGEV.SetParameters(gevParams);
                            double margLogLH = localGEV.LogPDF(AtSiteData[i, j]);

                            if (double.IsInfinity(margLogLH) || double.IsNaN(margLogLH))
                            {
                                valid = false;
                                break;
                            }

                            obsLogLH += localSiteWeights[j] * margLogLH;
                        }
                    }

                    result[i] = valid ? obsLogLH : double.NegativeInfinity;
                }
            }

            return result;
        }

        /// <inheritdoc/>
        public override List<DataComponent> PointwiseDataLogLikelihoodComponents(double[] parameters)
        {
            // Get the raw log-likelihoods
            var logLiks = PointwiseDataLogLikelihood(parameters);
            var result = new List<DataComponent>(logLiks.Length);

            for (int i = 0; i < logLiks.Length; i++)
            {
                // For spatial models, each observation is a time step across all sites
                // The value is a representative (e.g., first non-NaN site value for that observation)
                double value = 0.0;
                if (AtSiteData != null && Sites > 0)
                {
                    for (int j = 0; j < Sites; j++)
                    {
                        if (!double.IsNaN(AtSiteData[i, j]))
                        {
                            value = AtSiteData[i, j];
                            break;
                        }
                    }
                }

                result.Add(new DataComponent(i, logLiks[i], value, DataComponentType.Exact, Sites, $"Obs {i + 1}"));
            }

            return result;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// <b>Sum-equality contract is INTENTIONALLY broken for SpatialGEV.</b> This method
        /// emits per-parameter prior components AND <c>PriorComponentType.SpatialError</c>
        /// components for the location / scale / shape Gaussian-process spatial errors. The
        /// spatial-error contributions are also included in <see cref="DataLogLikelihood"/>
        /// (see its remarks), so <c>this.Sum() != ModelBase.PriorLogLikelihood</c> — the
        /// canonical pointwise-vs-scalar identity is deliberately violated to surface the
        /// per-error-process contribution to the prior diagnostics panel. Adding
        /// <see cref="DataLogLikelihood"/> + this method's <c>Sum()</c> would double-count the
        /// spatial-error term; use the inherited <see cref="ModelBase.LogLikelihood"/> for any
        /// joint computation.
        /// </remarks>
        public override List<PriorComponent> PointwisePriorLogLikelihood(double[] parameters)
        {
            var result = new List<PriorComponent>();

            // Save the current parameter values so the caller observes a pure-functional
            // evaluation (matches the contract of every other IModel.PointwisePriorLogLikelihood).
            // The error-model LogPDF() calls below read the error models' current state, which
            // must reflect the supplied parameters; we mutate, evaluate, then restore.
            var savedParameters = Parameters.Select(p => p.Value).ToArray();

            try
            {
                SetParameterValues(parameters);
            }
            catch (Exception ex)
            {
                // Restore in case partial assignment occurred.
                Debug.WriteLine($"SpatialGEV.PointwisePriorLogLikelihoodComponents: SetParameterValues failed: {ex.Message}");
                try { SetParameterValues(savedParameters); }
                catch (Exception restoreEx)
                {
                    Debug.WriteLine($"SpatialGEV.PointwisePriorLogLikelihoodComponents: best-effort restore failed: {restoreEx.Message}");
                }
                return result;
            }

            try
            {
                // Parameter priors from base implementation
                for (int i = 0; i < Parameters.Count; i++)
                {
                    double ll = Parameters[i].PriorDistribution.LogPDF(parameters[i]);
                    string paramName = string.IsNullOrEmpty(Parameters[i].OwnerName) ? Parameters[i].Name : Parameters[i].OwnerName;
                    result.Add(new PriorComponent($"Parameter Prior: {paramName}", ll, PriorComponentType.ParameterPrior));
                }

                // Spatial error priors (Gaussian process priors)
                if (UseLocationErrors && LocationErrors != null)
                {
                    double locErrLogLH = LocationErrors.LogPDF();
                    if (Tools.IsFinite(locErrLogLH))
                    {
                        result.Add(new PriorComponent("Spatial Error: Location", locErrLogLH, PriorComponentType.SpatialError));
                    }
                }

                if (UseScaleErrors && ScaleErrors != null)
                {
                    double sclErrLogLH = ScaleErrors.LogPDF();
                    if (Tools.IsFinite(sclErrLogLH))
                    {
                        result.Add(new PriorComponent("Spatial Error: Scale", sclErrLogLH, PriorComponentType.SpatialError));
                    }
                }

                if (UseShapeErrors && ShapeErrors != null)
                {
                    double shpErrLogLH = ShapeErrors.LogPDF();
                    if (Tools.IsFinite(shpErrLogLH))
                    {
                        result.Add(new PriorComponent("Spatial Error: Shape", shpErrLogLH, PriorComponentType.SpatialError));
                    }
                }
            }
            finally
            {
                // Restore caller-observable state.
                try { SetParameterValues(savedParameters); }
                catch (Exception restoreEx)
                {
                    Debug.WriteLine($"SpatialGEV.PointwisePriorLogLikelihoodComponents: best-effort restore failed: {restoreEx.Message}");
                }
            }

            return result;
        }

        /// <summary>
        /// Gets the PDF of the GEV distribution at a given site.
        /// </summary>
        /// <param name="x">The value to evaluate.</param>
        /// <param name="siteIndex">The site index.</param>
        public double PDF(double x, int siteIndex)
        {
            var gev = new GeneralizedExtremeValue();
            gev.SetParameters(GetGEVParameters(siteIndex));
            return gev.PDF(x);
        }

        /// <summary>
        /// Gets the CDF of the GEV distribution at a given site.
        /// </summary>
        /// <param name="x">The value to evaluate.</param>
        /// <param name="siteIndex">The site index.</param>
        public double CDF(double x, int siteIndex)
        {
            var gev = new GeneralizedExtremeValue();
            gev.SetParameters(GetGEVParameters(siteIndex));
            return gev.CDF(x);
        }

        /// <summary>
        /// Gets the inverse CDF (quantile) of the GEV distribution at a given site.
        /// </summary>
        /// <param name="probability">The probability value [0,1].</param>
        /// <param name="siteIndex">The site index.</param>
        public double InverseCDF(double probability, int siteIndex)
        {
            var gev = new GeneralizedExtremeValue();
            gev.SetParameters(GetGEVParameters(siteIndex));
            return gev.InverseCDF(probability);
        }

        /// <inheritdoc/>
        public override IModel Clone()
        {
            var clone = new SpatialGEV(AtSiteData, Coordinates,
                (GeneralLinearFunction)Location.Clone(),
                (GeneralLinearFunction)Scale.Clone(),
                (GeneralLinearFunction)Shape.Clone())
            {
                UseCopulaDependence = UseCopulaDependence,
                UseLocationErrors = UseLocationErrors,
                UseScaleErrors = UseScaleErrors,
                UseShapeErrors = UseShapeErrors,
                UseLogLinkForLocation = UseLogLinkForLocation,
                UseLogLinkForScale = UseLogLinkForScale,
                SiteWeights = (double[])SiteWeights.Clone()
            };

            if (SpatialDependence != null)
                clone.SpatialDependence = SpatialDependence.Clone();
            if (LocationErrors != null)
                clone.LocationErrors = LocationErrors.Clone();
            if (ScaleErrors != null)
                clone.ScaleErrors = ScaleErrors.Clone();
            if (ShapeErrors != null)
                clone.ShapeErrors = ShapeErrors.Clone();

            // Preserve the source's current parameter values. Calling
            // SetDefaultParameters here would (a) overwrite the source's values
            // with data-derived defaults, breaking Clone() semantics, and
            // (b) re-run a per-site MLE GEV fit on every clone — prohibitively
            // slow inside the MCMC inner loop where every iteration clones.
            // The trend-model Clone() calls above already deep-copied the
            // ModelParameter list (including .Value); no further work needed.
            return clone;
        }

        /// <inheritdoc/>
        public override XElement ToXElement()
        {
            var result = new XElement(nameof(SpatialGEV));

            result.SetAttributeValue(nameof(UseCopulaDependence), UseCopulaDependence.ToString());
            result.SetAttributeValue(nameof(UseLocationErrors), UseLocationErrors.ToString());
            result.SetAttributeValue(nameof(UseScaleErrors), UseScaleErrors.ToString());
            result.SetAttributeValue(nameof(UseShapeErrors), UseShapeErrors.ToString());
            result.SetAttributeValue(nameof(UseLogLinkForLocation), UseLogLinkForLocation.ToString());
            result.SetAttributeValue(nameof(UseLogLinkForScale), UseLogLinkForScale.ToString());

            // Site weights
            var weights = new XElement(nameof(SiteWeights));
            weights.Value = string.Join(",", SiteWeights.Select(w => w.ToString(CultureInfo.InvariantCulture)));
            result.Add(weights);

            // Parameters
            var parms = new XElement(nameof(Parameters));
            foreach (var p in Parameters)
                parms.Add(p.ToXElement());
            result.Add(parms);

            return result;
        }

        /// <inheritdoc/>
        public override (bool IsValid, List<string> ValidationMessages) Validate()
        {
            bool isValid = true;
            var messages = new List<string>();

            if (AtSiteData == null)
            {
                isValid = false;
                messages.Add("Error: At-site data is null.");
            }

            if (Coordinates == null)
            {
                isValid = false;
                messages.Add("Error: Coordinates are null.");
            }

            if (Sites < 2)
            {
                isValid = false;
                messages.Add("Error: At least 2 sites are required for spatial modeling.");
            }

            if (Location == null || Scale == null || Shape == null)
            {
                isValid = false;
                messages.Add("Error: GEV parameter trend models cannot be null.");
            }

            if (UseCopulaDependence && SpatialDependence == null)
            {
                isValid = false;
                messages.Add("Error: Copula dependence enabled but SpatialDependence is null.");
            }

            if (UseLocationErrors && LocationErrors == null)
            {
                isValid = false;
                messages.Add("Error: Location errors enabled but LocationErrors is null.");
            }

            if (UseScaleErrors && ScaleErrors == null)
            {
                isValid = false;
                messages.Add("Error: Scale errors enabled but ScaleErrors is null.");
            }

            if (UseShapeErrors && ShapeErrors == null)
            {
                isValid = false;
                messages.Add("Error: Shape errors enabled but ShapeErrors is null.");
            }

            if (SiteWeights == null || SiteWeights.Length != Sites)
            {
                isValid = false;
                messages.Add("Error: Site weights must be specified for all sites.");
            }

            return (isValid, messages);
        }

        /// <summary>
        /// Computes site weights based on intersite correlation to ensure proper CI coverage.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When using independence likelihood with correlated data, uncertainty is underestimated.
        /// This method computes weights that downweight sites with high correlation to others,
        /// effectively adjusting for the reduced effective sample size.
        /// </para>
        /// <para>
        /// For each site j, the weight is computed as:
        /// w_j = 1 / (1 + (n-1) * ρ̄_j)
        /// where ρ̄_j is the average correlation of site j with all other sites.
        /// </para>
        /// <para>
        /// The weights are normalized to sum to the number of sites, so the total
        /// effective sample size equals the nominal sample size when correlations are zero.
        /// </para>
        /// <para>
        ///     <b>References:</b>
        ///     - Ribatet, M., Cooley, D., Davison, A.C. (2012). Bayesian inference from composite likelihoods.
        ///       Statistica Sinica, 22(2), 813-845.
        ///     - Varin, C., Reid, N., Firth, D. (2011). An overview of composite likelihood methods.
        ///       Statistica Sinica, 21, 5-42.
        /// </para>
        /// </remarks>
        /// <param name="correlationMatrix">Intersite correlation matrix [sites × sites].
        /// If null, computes empirical correlation from data.</param>
        public void ComputeEffectiveSampleSizeWeights(double[,]? correlationMatrix = null)
        {
            if (correlationMatrix == null)
            {
                // Compute empirical intersite correlation from data
                correlationMatrix = ComputeIntersiteCorrelation();
            }

            if (correlationMatrix.GetLength(0) != Sites || correlationMatrix.GetLength(1) != Sites)
                throw new ArgumentException($"Correlation matrix must be {Sites}×{Sites}.", nameof(correlationMatrix));

            var weights = new double[Sites];
            double sumWeights = 0.0;

            for (int j = 0; j < Sites; j++)
            {
                // Compute average correlation with other sites
                double sumCorr = 0.0;
                int count = 0;
                for (int k = 0; k < Sites; k++)
                {
                    if (k != j)
                    {
                        // Use absolute correlation to handle negative correlations
                        sumCorr += Math.Abs(correlationMatrix[j, k]);
                        count++;
                    }
                }
                double avgCorr = count > 0 ? sumCorr / count : 0.0;

                // Effective sample size weight: w_j = 1 / (1 + (n-1) * ρ̄_j)
                // This accounts for reduced information due to correlation
                weights[j] = 1.0 / (1.0 + (Sites - 1) * avgCorr);
                sumWeights += weights[j];
            }

            // Normalize weights to sum to Sites (preserve total effective sample size when ρ=0)
            for (int j = 0; j < Sites; j++)
            {
                weights[j] = weights[j] * Sites / sumWeights;
            }

            SiteWeights = weights;
        }

        /// <summary>
        /// Computes the empirical intersite correlation matrix from the at-site data.
        /// </summary>
        /// <returns>Correlation matrix [sites × sites].</returns>
        /// <remarks>
        /// Uses Pearson correlation computed from pairwise complete observations.
        /// Missing values (NaN) are handled by using only observations where both sites have data.
        /// </remarks>
        public double[,] ComputeIntersiteCorrelation()
        {
            var corrMatrix = new double[Sites, Sites];

            for (int i = 0; i < Sites; i++)
            {
                corrMatrix[i, i] = 1.0;

                for (int j = i + 1; j < Sites; j++)
                {
                    // Extract paired observations (both non-NaN)
                    var pairsI = new List<double>();
                    var pairsJ = new List<double>();

                    for (int t = 0; t < Observations; t++)
                    {
                        if (!double.IsNaN(AtSiteData[t, i]) && !double.IsNaN(AtSiteData[t, j]))
                        {
                            pairsI.Add(AtSiteData[t, i]);
                            pairsJ.Add(AtSiteData[t, j]);
                        }
                    }

                    if (pairsI.Count >= 2)
                    {
                        // Compute Pearson correlation
                        double corr = Correlation.Pearson(pairsI.ToArray(), pairsJ.ToArray());
                        corrMatrix[i, j] = corr;
                        corrMatrix[j, i] = corr;
                    }
                    else
                    {
                        // Not enough data, assume no correlation
                        corrMatrix[i, j] = 0.0;
                        corrMatrix[j, i] = 0.0;
                    }
                }
            }

            return corrMatrix;
        }

        /// <summary>
        /// Computes the effective sample size based on intersite correlation.
        /// </summary>
        /// <param name="correlationMatrix">Intersite correlation matrix. If null, computes from data.</param>
        /// <returns>The effective sample size accounting for spatial correlation.</returns>
        /// <remarks>
        /// The effective sample size is computed as:
        /// n_eff = n * Sites / (1 + (Sites-1) * ρ̄)
        /// where ρ̄ is the overall average intersite correlation.
        /// </remarks>
        public double ComputeEffectiveSampleSize(double[,]? correlationMatrix = null)
        {
            if (correlationMatrix == null)
            {
                correlationMatrix = ComputeIntersiteCorrelation();
            }

            // Compute overall average correlation (excluding diagonal)
            double sumCorr = 0.0;
            int count = 0;
            for (int i = 0; i < Sites; i++)
            {
                for (int j = i + 1; j < Sites; j++)
                {
                    sumCorr += Math.Abs(correlationMatrix[i, j]);
                    count++;
                }
            }
            double avgCorr = count > 0 ? sumCorr / count : 0.0;

            // n_eff = n * Sites / (1 + (Sites-1) * ρ̄)
            double nominalN = Observations * Sites;
            double effectiveN = nominalN / (1.0 + (Sites - 1) * avgCorr);

            return effectiveN;
        }

        /// <summary>
        /// Configures the model for proper confidence interval coverage with correlated spatial data.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method enables the recommended Bayesian configuration for spatial GEV analysis with proper
        /// uncertainty quantification. The core Bayesian components are:
        /// </para>
        /// <list type="bullet">
        /// <item><description><b>Gaussian copula</b>: Models spatial dependence between site observations</description></item>
        /// <item><description><b>Spatial regression errors</b>: Latent errors with Gaussian process prior (Renard's BHM Level 2)</description></item>
        /// </list>
        /// <para>
        /// <b>Bayesian vs. Frequentist approaches:</b>
        /// The copula + regression errors approach is fully Bayesian - all parameters including latent
        /// errors are sampled jointly via MCMC with proper priors. This naturally propagates uncertainty.
        /// </para>
        /// <para>
        /// Weighted likelihood (optional) is a composite/pseudo-likelihood approach that provides
        /// additional robustness when the copula may be mis-specified, but is not strictly necessary
        /// in the pure Bayesian framework.
        /// </para>
        /// <para>
        ///     <b>References:</b>
        ///     - Renard, B., et al. (2006). Use of a Gaussian copula for multivariate extreme value analysis.
        ///       Journal of Hydrology, 315(1-4), 203-215.
        ///     - Cooley, D., Nychka, D., Naveau, P. (2007). Bayesian spatial modeling of extreme
        ///       precipitation return levels. JASA, 102(479), 824-840.
        ///     - Ribatet, M., Cooley, D., Davison, A.C. (2012). Bayesian inference from composite likelihoods.
        ///       Statistica Sinica, 22(2), 813-845.
        /// </para>
        /// </remarks>
        /// <param name="correlationType">The spatial correlation function type for copula and errors.</param>
        /// <param name="includeScaleErrors">Whether to include spatially correlated errors for scale parameter.</param>
        /// <param name="includeShapeErrors">Whether to include spatially correlated errors for shape parameter.</param>
        /// <param name="useWeightedLikelihood">If true, applies composite likelihood weights based on intersite
        /// correlation. This is a frequentist adjustment that provides robustness when the copula may be
        /// mis-specified. Default is false for pure Bayesian inference.</param>
        public void ConfigureForProperCoverage(
            CorrelationFunctionType correlationType = CorrelationFunctionType.Exponential,
            bool includeScaleErrors = false,
            bool includeShapeErrors = false,
            bool useWeightedLikelihood = false)
        {
            // Enable Gaussian copula for spatial dependence
            UseCopulaDependence = true;
            SpatialDependence = new GaussianCopula(Coordinates, correlationType);

            // Enable spatially correlated regression errors for location (core Bayesian component)
            UseLocationErrors = true;
            LocationErrors = new SpatialRegressionErrors(Coordinates, correlationType);

            // Optionally enable scale and shape errors
            if (includeScaleErrors)
            {
                UseScaleErrors = true;
                ScaleErrors = new SpatialRegressionErrors(Coordinates, correlationType);
            }

            if (includeShapeErrors)
            {
                UseShapeErrors = true;
                ShapeErrors = new SpatialRegressionErrors(Coordinates, correlationType);
            }

            // Optionally compute composite likelihood weights (frequentist robustification)
            if (useWeightedLikelihood)
            {
                ComputeEffectiveSampleSizeWeights();
            }
            else
            {
                // Reset to equal weights for pure Bayesian inference
                for (int i = 0; i < Sites; i++)
                    SiteWeights[i] = 1.0;
            }

            // Rebuild parameter list with new components
            SetDefaultParameters();
        }

        /// <summary>
        /// Gets the variance inflation factor (VIF) due to intersite correlation.
        /// </summary>
        /// <param name="correlationMatrix">Intersite correlation matrix. If null, computes from data.</param>
        /// <returns>The variance inflation factor (VIF ≥ 1).</returns>
        /// <remarks>
        /// <para>
        /// The VIF indicates how much wider confidence intervals should be compared to
        /// assuming independence. A VIF of 2.0 means CIs should be sqrt(2) ≈ 1.41 times wider.
        /// </para>
        /// <para>
        /// VIF = n_nominal / n_effective = 1 + (Sites-1) * ρ̄
        /// </para>
        /// </remarks>
        public double ComputeVarianceInflationFactor(double[,]? correlationMatrix = null)
        {
            if (correlationMatrix == null)
            {
                correlationMatrix = ComputeIntersiteCorrelation();
            }

            // Compute overall average correlation (excluding diagonal)
            double sumCorr = 0.0;
            int count = 0;
            for (int i = 0; i < Sites; i++)
            {
                for (int j = i + 1; j < Sites; j++)
                {
                    sumCorr += Math.Abs(correlationMatrix[i, j]);
                    count++;
                }
            }
            double avgCorr = count > 0 ? sumCorr / count : 0.0;

            // VIF = 1 + (Sites-1) * ρ̄
            return 1.0 + (Sites - 1) * avgCorr;
        }

        /// <summary>
        /// Predicts GEV parameters at an ungauged location using the spatial regression model
        /// with kriging interpolation for spatial errors.
        /// </summary>
        /// <param name="coordinates">The coordinates [X, Y] or [Lat, Lon] of the ungauged location.</param>
        /// <param name="covariates">Optional covariate values at the ungauged location for trend models.</param>
        /// <returns>
        /// A tuple containing:
        /// - GEVParams: Array [ξ, α, κ] of predicted GEV parameters
        /// - ErrorVariances: Array [var_ξ, var_α, var_κ] of kriging prediction variances for errors
        /// </returns>
        /// <remarks>
        /// <para>
        /// This method evaluates the trend surfaces at the ungauged location and uses kriging
        /// (conditional Gaussian process) to interpolate the spatial errors if enabled.
        /// </para>
        /// <para>
        /// The prediction includes uncertainty from the kriging variance, which can be used
        /// to construct prediction intervals. For full Bayesian uncertainty propagation,
        /// use <c>PredictAtUngaugedLocation</c>.
        /// </para>
        /// </remarks>
        public (double[] GEVParams, double[] ErrorVariances) PredictAtUngauged(double[] coordinates, double[]? covariates = null)
        {
            if (coordinates == null || coordinates.Length != 2)
                throw new ArgumentException("Coordinates must be a 2-element array [X, Y].", nameof(coordinates));

            // Evaluate trend at ungauged location
            double locTrend = Location.PredictWithCovariates(covariates);
            double sclTrend = Scale.PredictWithCovariates(covariates);
            double shpTrend = Shape.PredictWithCovariates(covariates);

            // Initialize error variances
            double locErrVar = 0, sclErrVar = 0, shpErrVar = 0;
            double locErr = 0, sclErr = 0, shpErr = 0;

            // Kriging interpolation for spatial errors if enabled
            if (UseLocationErrors && LocationErrors != null)
            {
                var (mean, variance) = LocationErrors.GetKrigingPrediction(coordinates);
                locErr = mean;
                locErrVar = variance;
            }

            if (UseScaleErrors && ScaleErrors != null)
            {
                var (mean, variance) = ScaleErrors.GetKrigingPrediction(coordinates);
                sclErr = mean;
                sclErrVar = variance;
            }

            if (UseShapeErrors && ShapeErrors != null)
            {
                var (mean, variance) = ShapeErrors.GetKrigingPrediction(coordinates);
                shpErr = mean;
                shpErrVar = variance;
            }

            // Apply link functions
            double xi = UseLogLinkForLocation ? Math.Exp(locTrend + locErr) : locTrend + locErr;
            double alpha = UseLogLinkForScale ? Math.Exp(sclTrend + sclErr) : Math.Max(sclTrend + sclErr, Tools.DoubleMachineEpsilon);
            double kappa = shpTrend + shpErr;

            return (new double[] { xi, alpha, kappa }, new double[] { locErrVar, sclErrVar, shpErrVar });
        }

        /// <summary>
        /// Gets the PDF, CDF, or inverse CDF at an ungauged location.
        /// </summary>
        /// <param name="coordinates">The coordinates [X, Y] or [Lat, Lon] of the ungauged location.</param>
        /// <param name="covariates">Optional covariate values at the ungauged location.</param>
        /// <returns>A GEV distribution configured with the predicted parameters.</returns>
        public GeneralizedExtremeValue GetGEVAtUngauged(double[] coordinates, double[]? covariates = null)
        {
            var (gevParams, _) = PredictAtUngauged(coordinates, covariates);
            return new GeneralizedExtremeValue(gevParams[0], gevParams[1], gevParams[2]);
        }

        /// <inheritdoc/>
        /// <remarks>
        /// <para>
        /// Generates random samples from the spatial GEV model. Samples are generated
        /// independently for each site using the site-specific GEV parameters
        /// (location, scale, shape) computed from the spatial regression.
        /// </para>
        /// <para>
        /// The returned array contains values for all sites, with samples grouped
        /// by site (site 1 samples, then site 2 samples, etc.). The total length
        /// is sampleSize * Sites.
        /// </para>
        /// <para>
        /// Note: Spatial correlation between sites is not included in this simple
        /// generation scheme. For correlated simulations, use the copula-based
        /// simulation methods.
        /// </para>
        /// </remarks>
        public double[] GenerateRandomValues(int sampleSize, int seed = -1)
        {
            if (sampleSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(sampleSize), "Sample size must be positive.");
            if (Parameters == null || Parameters.Count == 0)
                throw new InvalidOperationException("Parameters must be set before generating random values.");
            if (Sites <= 0)
                throw new InvalidOperationException("At least one site must be defined.");

            var rng = seed > 0 ? new Numerics.Sampling.MersenneTwister(seed) : new Numerics.Sampling.MersenneTwister();
            var paramValues = Parameters.Select(p => p.Value).ToArray();
            SetParameterValues(paramValues);

            // Generate samples for each site
            var result = new double[sampleSize * Sites];
            int resultIndex = 0;

            for (int s = 0; s < Sites; s++)
            {
                // Get site-specific GEV parameters
                var gevParams = GetGEVParameters(s);
                double xi = gevParams[0];   // location
                double alpha = gevParams[1]; // scale
                double kappa = gevParams[2]; // shape

                var gev = new Numerics.Distributions.GeneralizedExtremeValue(xi, alpha, kappa);

                for (int i = 0; i < sampleSize; i++)
                {
                    result[resultIndex++] = gev.InverseCDF(rng.NextDouble());
                }
            }

            return result;
        }

        #endregion
    }
}
