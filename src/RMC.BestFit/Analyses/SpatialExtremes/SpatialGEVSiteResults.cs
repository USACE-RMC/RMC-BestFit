namespace RMC.BestFit.Analyses
{
    /// <summary>
    /// Contains the analysis results for a single site in the spatial GEV model.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     <b>Authors:</b>
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    /// This class stores the GEV (Generalized Extreme Value) parameters and quantile curves
    /// for a single site within a spatial extremes analysis. Each site has its own set of
    /// GEV parameters (location, scale, shape) derived from the hierarchical Bayesian model.
    /// </para>
    /// <para>
    /// The results include:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Posterior mean and credible intervals for GEV parameters</description></item>
    /// <item><description>Quantile curves at specified exceedance probabilities</description></item>
    /// <item><description>Uncertainty bounds on all estimates</description></item>
    /// </list>
    /// <para>
    /// For ungauged locations (predicted via spatial interpolation), the <see cref="SiteIndex"/>
    /// is set to -1 and the <see cref="Coordinate"/> contains the prediction location.
    /// </para>
    /// </remarks>
    public class SpatialGEVSiteResults
    {
        /// <summary>
        /// Gets or sets the site index (0-based). -1 indicates an ungauged location.
        /// </summary>
        public int SiteIndex { get; set; }

        /// <summary>
        /// Gets or sets the site coordinates [X, Y].
        /// </summary>
        public double[] Coordinate { get; set; } = Array.Empty<double>();

        /// <summary>
        /// Gets or sets the posterior mean of the GEV location parameter (xi).
        /// </summary>
        /// <remarks>
        /// The location parameter determines where the distribution is centered.
        /// For flood frequency analysis, this represents the typical magnitude of annual maxima.
        /// </remarks>
        public double LocationMean { get; set; }

        /// <summary>
        /// Gets or sets the lower credible bound of the GEV location parameter.
        /// </summary>
        public double LocationLower { get; set; }

        /// <summary>
        /// Gets or sets the upper credible bound of the GEV location parameter.
        /// </summary>
        public double LocationUpper { get; set; }

        /// <summary>
        /// Gets or sets the posterior mean of the GEV scale parameter (alpha).
        /// </summary>
        /// <remarks>
        /// The scale parameter controls the spread of the distribution.
        /// Larger values indicate greater variability in annual maxima.
        /// </remarks>
        public double ScaleMean { get; set; }

        /// <summary>
        /// Gets or sets the lower credible bound of the GEV scale parameter.
        /// </summary>
        public double ScaleLower { get; set; }

        /// <summary>
        /// Gets or sets the upper credible bound of the GEV scale parameter.
        /// </summary>
        public double ScaleUpper { get; set; }

        /// <summary>
        /// Gets or sets the posterior mean of the GEV shape parameter (kappa).
        /// </summary>
        /// <remarks>
        /// The shape parameter controls the tail behavior:
        /// <list type="bullet">
        /// <item><description>kappa &lt; 0: Frechet type (heavy upper tail, bounded lower)</description></item>
        /// <item><description>kappa = 0: Gumbel type (exponential tails)</description></item>
        /// <item><description>kappa &gt; 0: Weibull type (bounded upper tail)</description></item>
        /// </list>
        /// </remarks>
        public double ShapeMean { get; set; }

        /// <summary>
        /// Gets or sets the lower credible bound of the GEV shape parameter.
        /// </summary>
        public double ShapeLower { get; set; }

        /// <summary>
        /// Gets or sets the upper credible bound of the GEV shape parameter.
        /// </summary>
        public double ShapeUpper { get; set; }

        /// <summary>
        /// Gets or sets the exceedance probabilities for the quantile curves.
        /// </summary>
        /// <remarks>
        /// Common values include 0.5 (T=2), 0.1 (T=10), 0.01 (T=100), and 0.001 (T=1000).
        /// </remarks>
        public double[] Probabilities { get; set; } = Array.Empty<double>();

        /// <summary>
        /// Gets or sets the posterior mean quantiles at each probability.
        /// </summary>
        public double[] QuantileMean { get; set; } = Array.Empty<double>();

        /// <summary>
        /// Gets or sets the lower credible bound quantiles at each probability.
        /// </summary>
        public double[] QuantileLower { get; set; } = Array.Empty<double>();

        /// <summary>
        /// Gets or sets the upper credible bound quantiles at each probability.
        /// </summary>
        public double[] QuantileUpper { get; set; } = Array.Empty<double>();

        /// <summary>
        /// Gets or sets the point estimate (MAP or mean) quantiles at each probability.
        /// </summary>
        public double[] QuantileMode { get; set; } = Array.Empty<double>();
    }
}
