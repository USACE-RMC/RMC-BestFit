using Numerics.Functions;
using System;
using System.Globalization;
using System.Xml.Linq;

namespace RMC.BestFit.Models.LinkFunctions
{
    /// <summary>
    /// A positive-support sinh-arcsinh link applied to log-relative scale.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     <see cref="LogASinHLink"/> maps a positive parameter, such as a scale or standard
    ///     deviation, to an unconstrained link-space variable by applying the Jones-Pewsey
    ///     sinh-arcsinh transformation to the log-relative deviation from a fitted center.
    /// </para>
    /// <para>
    ///     Let sigma0 &gt; 0 be the fitted center, s &gt; 0 be a dimensionless log-scale, and
    ///     r = log(sigma / sigma0). Define z = r / s. Then:
    /// </para>
    /// <para>
    ///     Forward (link): eta = sinh(delta * asinh(z) - epsilon)
    ///     <br/>
    ///     Inverse: sigma = sigma0 * exp(s * sinh((asinh(eta) + epsilon) / delta))
    ///     <br/>
    ///     Derivative: deta/dsigma = (delta / (s * sigma)) *
    ///     cosh(delta * asinh(z) - epsilon) / sqrt(1 + z^2)
    /// </para>
    /// <para>
    ///     This is preferable to applying <see cref="ASinHLink"/> directly to a positive
    ///     parameter because it preserves positive support exactly. Epsilon controls
    ///     log-scale asymmetry, with positive epsilon inflating the upper sigma tail.
    ///     Delta controls tail thickness for normal link-space draws: delta &lt; 1 gives
    ///     heavier positive-parameter tails and delta &gt; 1 compresses them.
    /// </para>
    /// <para>
    ///     When epsilon = 0 and delta = 1, the link reduces to eta = log(sigma / sigma0) / s.
    ///     This preserves the familiar log-link behavior as the centered baseline while making
    ///     skewness and tail-weight adjustments explicit.
    /// </para>
    /// <para>
    ///     Reference: Jones, M.C. and Pewsey, A. (2009). Sinh-arcsinh distributions.
    ///     Biometrika, 96(4), 761-780.
    /// </para>
    /// </remarks>
    public sealed class LogASinHLink : ILinkFunction
    {
        /// <summary>
        /// Default numerical floor for positive quantities.
        /// </summary>
        private const double DefaultEps = 1e-12;

        /// <summary>
        /// Maximum log argument used in exponential and hyperbolic evaluations.
        /// </summary>
        private const double MaxSafeLog = 700.0;

        /// <summary>
        /// Center sigma0 &gt; 0, typically the fitted scale parameter.
        /// </summary>
        public double Sigma0
        {
            get => _sigma0;
            set => _sigma0 = IsPositiveFinite(value) ? value : Math.Max(Eps, DefaultEps);
        }
        private double _sigma0 = 1.0;

        /// <summary>
        /// Dimensionless scale used to standardize log(sigma / sigma0).
        /// </summary>
        /// <remarks>
        /// For uncertainty propagation this is usually derived from a relative standard error,
        /// for example sqrt(log(1 + CV^2)). This keeps the transform invariant to measurement
        /// units while preserving the local log-scale variance implied by the covariance matrix.
        /// </remarks>
        public double LogScale
        {
            get => _logScale;
            set => _logScale = IsPositiveFinite(value) ? value : Math.Max(Eps, DefaultEps);
        }
        private double _logScale = 1.0;

        /// <summary>
        /// Fixed asymmetry parameter used when <see cref="UseAdaptiveEpsilon"/> is false.
        /// </summary>
        /// <remarks>
        /// Positive epsilon inflates the upper sigma tail; negative epsilon inflates the lower
        /// tail on the log-relative scale.
        /// </remarks>
        public double Epsilon { get; set; } = 0.0;

        /// <summary>
        /// Tail-shape parameter delta &gt; 0.
        /// </summary>
        /// <remarks>
        /// For normal link-space draws, delta &lt; 1 produces heavier sigma-space tails and
        /// delta &gt; 1 compresses them.
        /// </remarks>
        public double Delta
        {
            get => _delta;
            set => _delta = IsPositiveFinite(value) ? value : Math.Max(Eps, DefaultEps);
        }
        private double _delta = 1.0;

        /// <summary>
        /// Enables adaptive asymmetry driven by <see cref="ParentIndicator"/>.
        /// </summary>
        /// <remarks>
        /// When true, <see cref="Epsilon"/> is ignored and the effective epsilon is computed
        /// from the parent indicator using a bounded tanh map.
        /// </remarks>
        public bool UseAdaptiveEpsilon { get; set; } = false;

        /// <summary>
        /// Parent diagnostic controlling adaptive asymmetry sign and strength.
        /// </summary>
        /// <remarks>
        /// For scale parameters this is typically a nonnegative uncertainty diagnostic, such as
        /// scaleSE / scaleHat mapped into [0, 1], because the scale tail direction is structural.
        /// </remarks>
        public double ParentIndicator { get; set; } = 0.0;

        /// <summary>
        /// Maximum magnitude of adaptive asymmetry epsilon.
        /// </summary>
        public double EpsilonMax
        {
            get => _epsilonMax;
            set => _epsilonMax = double.IsFinite(value) ? Math.Max(0.0, value) : 0.0;
        }
        private double _epsilonMax = 0.5;

        /// <summary>
        /// Sensitivity of adaptive epsilon to <see cref="ParentIndicator"/>.
        /// </summary>
        public double EpsilonSlope
        {
            get => _epsilonSlope;
            set => _epsilonSlope = double.IsFinite(value) ? Math.Max(0.0, value) : 0.0;
        }
        private double _epsilonSlope = 1.0;

        /// <summary>
        /// Numerical floor for positive sigma, sigma0, log-scale, and delta values.
        /// </summary>
        public double Eps
        {
            get => _eps;
            set => _eps = IsPositiveFinite(value) ? value : DefaultEps;
        }
        private double _eps = DefaultEps;

        /// <summary>
        /// Constructs a new symmetric log-sinh-arcsinh link with default settings.
        /// </summary>
        public LogASinHLink()
        {
        }

        /// <summary>
        /// Constructs a new log-sinh-arcsinh link with the specified center and log-scale.
        /// </summary>
        /// <param name="sigma0">The positive fitted scale parameter.</param>
        /// <param name="logScale">The positive dimensionless log-standardization scale.</param>
        /// <param name="epsilon">The fixed asymmetry parameter.</param>
        /// <param name="delta">The positive tail-shape parameter.</param>
        public LogASinHLink(double sigma0, double logScale, double epsilon = 0.0, double delta = 1.0)
        {
            Sigma0 = sigma0;
            LogScale = logScale;
            Epsilon = epsilon;
            Delta = delta;
        }

        /// <summary>
        /// Constructs a <see cref="LogASinHLink"/> from a serialized <see cref="XElement"/>.
        /// </summary>
        /// <param name="xElement">The XElement to deserialize from.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="xElement"/> is null.</exception>
        public LogASinHLink(XElement xElement)
        {
            if (xElement == null) throw new ArgumentNullException(nameof(xElement));

            Sigma0 = ParseDouble(xElement, nameof(Sigma0), Sigma0);
            LogScale = ParseDouble(xElement, nameof(LogScale), LogScale);
            Epsilon = ParseDouble(xElement, nameof(Epsilon), Epsilon);
            Delta = ParseDouble(xElement, nameof(Delta), Delta);

            var useAdaptiveAttr = xElement.Attribute(nameof(UseAdaptiveEpsilon));
            if (useAdaptiveAttr != null && bool.TryParse(useAdaptiveAttr.Value, out bool useAdaptiveVal))
                UseAdaptiveEpsilon = useAdaptiveVal;

            ParentIndicator = ParseDouble(xElement, nameof(ParentIndicator), ParentIndicator);
            EpsilonMax = ParseDouble(xElement, nameof(EpsilonMax), EpsilonMax);
            EpsilonSlope = ParseDouble(xElement, nameof(EpsilonSlope), EpsilonSlope);
            Eps = ParseDouble(xElement, nameof(Eps), Eps);

            Sigma0 = Sigma0;
            LogScale = LogScale;
            Delta = Delta;
        }

        /// <summary>
        /// Evaluates the link function.
        /// </summary>
        /// <param name="sigma">The positive parameter value.</param>
        /// <returns>The unconstrained link-space value.</returns>
        /// <remarks>
        /// Nonpositive sigma values are floored at <see cref="Eps"/>. This mirrors the other
        /// positive-support links and prevents transient numerical failures during finite
        /// differencing or covariance transformations.
        /// </remarks>
        public double Link(double sigma)
        {
            double s = Math.Max(LogScale, Eps);
            double d = Math.Max(Delta, Eps);
            double eps = EffectiveEpsilon();
            double z = LogRatio(sigma) / s;
            return SafeSinh((d * Asinh(z)) - eps);
        }

        /// <summary>
        /// Evaluates the inverse link function.
        /// </summary>
        /// <param name="eta">The unconstrained link-space value.</param>
        /// <returns>The positive parameter value.</returns>
        public double InverseLink(double eta)
        {
            double sigma0 = Math.Max(Sigma0, Eps);
            double s = Math.Max(LogScale, Eps);
            double d = Math.Max(Delta, Eps);
            double eps = EffectiveEpsilon();

            double logRelative = s * SafeSinh((Asinh(eta) + eps) / d);
            double logSigma = Math.Log(sigma0) + logRelative;
            return SafeExp(logSigma);
        }

        /// <summary>
        /// Evaluates the derivative of the link function with respect to sigma.
        /// </summary>
        /// <param name="sigma">The positive parameter value.</param>
        /// <returns>The derivative deta/dsigma.</returns>
        public double DLink(double sigma)
        {
            double x = Math.Max(sigma, Eps);
            double s = Math.Max(LogScale, Eps);
            double d = Math.Max(Delta, Eps);
            double eps = EffectiveEpsilon();
            double z = LogRatio(x) / s;
            double transformed = (d * Asinh(z)) - eps;
            double denominator = s * x * SafeSqrtOnePlusSquare(z);
            double derivative = d * SafeCosh(transformed) / Math.Max(denominator, Eps);

            if (!double.IsFinite(derivative))
                return double.MaxValue;

            return Math.Max(derivative, Eps);
        }

        /// <summary>
        /// Serializes this link function to an <see cref="XElement"/>.
        /// </summary>
        /// <returns>An XElement representation of this link function.</returns>
        public XElement ToXElement()
        {
            var element = new XElement(nameof(LogASinHLink));
            element.SetAttributeValue(nameof(Sigma0), Sigma0.ToString("G17", CultureInfo.InvariantCulture));
            element.SetAttributeValue(nameof(LogScale), LogScale.ToString("G17", CultureInfo.InvariantCulture));
            element.SetAttributeValue(nameof(Epsilon), Epsilon.ToString("G17", CultureInfo.InvariantCulture));
            element.SetAttributeValue(nameof(Delta), Delta.ToString("G17", CultureInfo.InvariantCulture));
            element.SetAttributeValue(nameof(UseAdaptiveEpsilon), UseAdaptiveEpsilon.ToString());
            element.SetAttributeValue(nameof(ParentIndicator), ParentIndicator.ToString("G17", CultureInfo.InvariantCulture));
            element.SetAttributeValue(nameof(EpsilonMax), EpsilonMax.ToString("G17", CultureInfo.InvariantCulture));
            element.SetAttributeValue(nameof(EpsilonSlope), EpsilonSlope.ToString("G17", CultureInfo.InvariantCulture));
            element.SetAttributeValue(nameof(Eps), Eps.ToString("G17", CultureInfo.InvariantCulture));
            return element;
        }

        /// <summary>
        /// Computes the effective epsilon value from fixed or adaptive settings.
        /// </summary>
        /// <returns>The epsilon value used by the transformation.</returns>
        private double EffectiveEpsilon()
        {
            if (!UseAdaptiveEpsilon)
                return Epsilon;

            return EpsilonMax * Math.Tanh(EpsilonSlope * ParentIndicator);
        }

        /// <summary>
        /// Computes log(sigma / sigma0) with positive floors.
        /// </summary>
        /// <param name="sigma">The sigma value.</param>
        /// <returns>The log-relative deviation from <see cref="Sigma0"/>.</returns>
        private double LogRatio(double sigma)
        {
            double x = Math.Max(sigma, Eps);
            double sigma0 = Math.Max(Sigma0, Eps);
            return Math.Log(x) - Math.Log(sigma0);
        }

        /// <summary>
        /// Parses a double attribute from an XElement.
        /// </summary>
        /// <param name="xElement">The XElement to read from.</param>
        /// <param name="name">The attribute name.</param>
        /// <param name="defaultValue">The value returned if the attribute is missing or invalid.</param>
        /// <returns>The parsed value or <paramref name="defaultValue"/>.</returns>
        private static double ParseDouble(XElement xElement, string name, double defaultValue)
        {
            var attr = xElement.Attribute(name);
            if (attr != null && double.TryParse(attr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double value))
                return value;

            return defaultValue;
        }

        /// <summary>
        /// Determines whether a value is finite and positive.
        /// </summary>
        /// <param name="value">The value to test.</param>
        /// <returns><c>true</c> when <paramref name="value"/> is finite and greater than zero.</returns>
        private static bool IsPositiveFinite(double value)
        {
            return double.IsFinite(value) && value > 0.0;
        }

        /// <summary>
        /// Computes a numerically stable inverse hyperbolic sine.
        /// </summary>
        /// <param name="x">The input value.</param>
        /// <returns>The inverse hyperbolic sine of <paramref name="x"/>.</returns>
        private static double Asinh(double x)
        {
            if (!double.IsFinite(x))
                return x;

            double ax = Math.Abs(x);
            if (ax < 1e-8)
                return x;

            double value = ax > 1e154
                ? Math.Log(ax) + Math.Log(2.0)
                : Math.Log(ax + Math.Sqrt((ax * ax) + 1.0));

            return Math.CopySign(value, x);
        }

        /// <summary>
        /// Computes sinh with overflow protection.
        /// </summary>
        /// <param name="x">The input value.</param>
        /// <returns>The hyperbolic sine of <paramref name="x"/>, clamped to a finite range.</returns>
        private static double SafeSinh(double x)
        {
            double bounded = Math.Clamp(x, -MaxSafeLog, MaxSafeLog);
            return Math.Sinh(bounded);
        }

        /// <summary>
        /// Computes cosh with overflow protection.
        /// </summary>
        /// <param name="x">The input value.</param>
        /// <returns>The hyperbolic cosine of <paramref name="x"/>, clamped to a finite range.</returns>
        private static double SafeCosh(double x)
        {
            double bounded = Math.Clamp(x, -MaxSafeLog, MaxSafeLog);
            return Math.Cosh(bounded);
        }

        /// <summary>
        /// Computes exp with overflow and underflow protection.
        /// </summary>
        /// <param name="x">The input log value.</param>
        /// <returns>A finite positive exponential value.</returns>
        private static double SafeExp(double x)
        {
            double bounded = Math.Clamp(x, -MaxSafeLog, MaxSafeLog);
            return Math.Exp(bounded);
        }

        /// <summary>
        /// Computes sqrt(1 + x^2) without overflowing for very large x.
        /// </summary>
        /// <param name="x">The input value.</param>
        /// <returns>The finite square-root value.</returns>
        private static double SafeSqrtOnePlusSquare(double x)
        {
            double ax = Math.Abs(x);
            if (ax > 1e154)
                return ax;

            return Math.Sqrt(1.0 + (x * x));
        }
    }
}
