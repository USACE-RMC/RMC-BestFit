using Numerics.Functions;
using System;
using System.Globalization;
using System.Xml.Linq;

namespace RMC.BestFit.Models.LinkFunctions
{
    /// <summary>
    /// A centered sinh-arcsinh link function for unbounded parameters, with optional
    /// asymmetry (epsilon) and tail-shape (delta) control.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The link uses the Jones-Pewsey sinh-arcsinh form around a fitted center while
    /// remaining fully analytical (no Newton solver). It maps R to R and is monotonically
    /// increasing for all parameter values.
    /// </para>
    /// <para>
    /// Let z = (x - x0) / s be the standardized parameter deviation. Then:
    /// </para>
    /// <para>
    ///     Forward (link):   eta = sinh(delta * asinh(z) - epsilon)
    ///     Inverse:          x = x0 + s * sinh((asinh(eta) + epsilon) / delta)
    ///     Derivative:       deta/dx = (delta / s) * cosh(delta * asinh(z) - epsilon) / sqrt(1 + z^2)
    /// </para>
    /// <para>
    /// <b>Parameter interpretation:</b>
    /// <list type="bullet">
    ///   <item><description>Epsilon controls skewness/asymmetry. Epsilon &gt; 0 inflates the
    ///     positive parameter tail; epsilon &lt; 0 inflates the negative tail. Epsilon = 0
    ///     gives symmetric behavior.</description></item>
    ///   <item><description>Delta controls the link curvature. For uncertainty draws generated
    ///     from a normal eta distribution, delta &lt; 1 produces heavier x-space tails and
    ///     delta &gt; 1 compresses tails. Delta = 1 is the identity-tail baseline.</description></item>
    ///   <item><description>When epsilon = 0 and delta = 1, the link reduces to the centered
    ///     identity standardization eta = z, x = x0 + s * eta. This is deliberate: tail and
    ///     asymmetry changes are explicit settings rather than hidden baseline behavior.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Adaptive asymmetry:</b> When <see cref="UseAdaptiveEpsilon"/> is true, epsilon is computed
    /// from <see cref="ParentIndicator"/> via a smooth tanh mapping:
    /// epsilon_eff = <see cref="EpsilonMax"/> * tanh(<see cref="EpsilonSlope"/> * ParentIndicator).
    /// This gives directional tail inflation that follows the sign and magnitude of the
    /// parent indicator.
    /// </para>
    /// <para>
    ///     <b>Authors:</b>
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    /// Reference: Jones, M.C. and Pewsey, A. (2009). Sinh-arcsinh distributions.
    /// Biometrika, 96(4), 761-780.
    /// </para>
    /// </remarks>
    public sealed class ASinHLink : ILinkFunction
    {
        /// <summary>
        /// Center x0, typically the GMM point estimate for the linked parameter.
        /// </summary>
        public double Gamma0 { get; set; } = 0.0;

        /// <summary>
        /// Scale parameter s used to standardize deviations from <see cref="Gamma0"/>.
        /// Typically set to the parameter standard error from the GMM sandwich covariance.
        /// </summary>
        public double Scale { get; set; } = 1.0;

        /// <summary>
        /// If <see cref="UseAdaptiveEpsilon"/> is false, this fixed epsilon is used for asymmetry.
        /// Positive epsilon inflates the positive parameter tail; negative epsilon inflates the negative tail.
        /// </summary>
        public double Epsilon { get; set; } = 0.0;

        /// <summary>
        /// Tail-shape parameter delta (&gt; 0). For normal link-space draws, delta &lt; 1
        /// produces heavier parameter-space tails and delta &gt; 1 compresses tails.
        /// </summary>
        public double Delta { get; set; } = 1.0;

        /// <summary>
        /// Enable adaptive asymmetry driven by <see cref="ParentIndicator"/>.
        /// When true, <see cref="Epsilon"/> is ignored and epsilon is computed from the parent indicator.
        /// </summary>
        public bool UseAdaptiveEpsilon { get; set; } = false;

        /// <summary>
        /// Parent indicator controlling asymmetry sign and strength.
        /// Positive values strengthen the positive tail; negative values strengthen the negative tail.
        /// Only used when <see cref="UseAdaptiveEpsilon"/> is true.
        /// </summary>
        public double ParentIndicator { get; set; } = 0.0;

        /// <summary>
        /// Maximum magnitude of the adaptive asymmetry epsilon_max (&gt;= 0).
        /// Only used when <see cref="UseAdaptiveEpsilon"/> is true.
        /// </summary>
        public double EpsilonMax { get; set; } = 0.5;

        /// <summary>
        /// Sensitivity of epsilon_eff to the parent indicator. Larger values approach +/-EpsilonMax faster.
        /// Only used when <see cref="UseAdaptiveEpsilon"/> is true.
        /// </summary>
        public double EpsilonSlope { get; set; } = 1.0;

        /// <summary>
        /// Numerical floor for the scale and delta parameters.
        /// </summary>
        private const double Eps = 1e-12;

        /// <summary>
        /// Constructs a new ASinHLink with default parameters (symmetric, Gamma0 = 0, Scale = 1, Delta = 1).
        /// </summary>
        public ASinHLink() { }

        /// <summary>
        /// Constructs a new symmetric ASinHLink with specified center and scale (epsilon = 0, delta = 1).
        /// </summary>
        /// <param name="gamma0">Center value for the linked parameter.</param>
        /// <param name="scale">Scale parameter, usually a sandwich-covariance standard error.</param>
        public ASinHLink(double gamma0, double scale)
        {
            Gamma0 = gamma0;
            Scale = Math.Max(scale, Eps);
        }

        /// <summary>
        /// Constructs an ASinHLink with specified center, scale, asymmetry, and tail weight.
        /// </summary>
        /// <param name="gamma0">Center value for the linked parameter.</param>
        /// <param name="scale">Scale parameter, usually a sandwich-covariance standard error.</param>
        /// <param name="epsilon">Asymmetry parameter. Positive values inflate the positive parameter tail.</param>
        /// <param name="delta">Tail-shape parameter. Delta &lt; 1 produces heavier parameter-space tails for normal link-space draws.</param>
        public ASinHLink(double gamma0, double scale, double epsilon, double delta = 1.0)
        {
            Gamma0 = gamma0;
            Scale = Math.Max(scale, Eps);
            Epsilon = epsilon;
            Delta = Math.Max(delta, Eps);
        }

        /// <summary>
        /// Constructs an ASinHLink from a serialized <see cref="XElement"/>.
        /// </summary>
        /// <param name="xElement">The XElement to deserialize from.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="xElement"/> is null.</exception>
        public ASinHLink(XElement xElement)
        {
            if (xElement == null) throw new ArgumentNullException(nameof(xElement));

            Gamma0 = ParseDouble(xElement, nameof(Gamma0), Gamma0);
            Scale = Math.Max(ParseDouble(xElement, nameof(Scale), Scale), Eps);
            Epsilon = ParseDouble(xElement, nameof(Epsilon), Epsilon);
            Delta = Math.Max(ParseDouble(xElement, nameof(Delta), Delta), Eps);

            var useAdaptiveAttr = xElement.Attribute(nameof(UseAdaptiveEpsilon));
            if (useAdaptiveAttr != null && bool.TryParse(useAdaptiveAttr.Value, out bool useAdaptiveVal))
                UseAdaptiveEpsilon = useAdaptiveVal;

            ParentIndicator = ParseDouble(xElement, nameof(ParentIndicator), ParentIndicator);
            EpsilonMax = ParseDouble(xElement, nameof(EpsilonMax), EpsilonMax);
            EpsilonSlope = ParseDouble(xElement, nameof(EpsilonSlope), EpsilonSlope);
        }

        /// <summary>
        /// Evaluates the link function: eta = sinh(delta * asinh(z) - epsilon)
        /// where z = (x - x0) / s. When epsilon = 0 and delta = 1, this reduces
        /// to the centered identity standardization eta = z.
        /// </summary>
        /// <param name="gamma">The parameter value.</param>
        /// <returns>The link-space value eta.</returns>
        public double Link(double gamma)
        {
            double s = Math.Max(Scale, Eps);
            double d = Math.Max(Delta, Eps);
            double eps = EffectiveEpsilon();
            double z = (gamma - Gamma0) / s;
            return Math.Sinh(d * Asinh(z) - eps);
        }

        /// <summary>
        /// Evaluates the inverse link: x = x0 + s * sinh((asinh(eta) + epsilon) / delta).
        /// When epsilon = 0 and delta = 1, this reduces to x = x0 + s * eta.
        /// </summary>
        /// <param name="eta">The link-space value eta.</param>
        /// <returns>The parameter value.</returns>
        public double InverseLink(double eta)
        {
            double s = Math.Max(Scale, Eps);
            double d = Math.Max(Delta, Eps);
            double eps = EffectiveEpsilon();
            return Gamma0 + s * Math.Sinh((Asinh(eta) + eps) / d);
        }

        /// <summary>
        /// Evaluates the derivative of the link function:
        /// deta/dx = (delta / s) * cosh(delta * asinh(z) - epsilon) / sqrt(1 + z^2).
        /// When epsilon = 0 and delta = 1, this reduces to 1 / s.
        /// </summary>
        /// <param name="gamma">The parameter value.</param>
        /// <returns>The derivative deta/dx.</returns>
        public double DLink(double gamma)
        {
            double s = Math.Max(Scale, Eps);
            double d = Math.Max(Delta, Eps);
            double eps = EffectiveEpsilon();
            double z = (gamma - Gamma0) / s;
            double asinhZ = Asinh(z);
            double coshTerm = Math.Cosh(d * asinhZ - eps);
            double sqrtTerm = Math.Sqrt(1.0 + z * z);
            return (d / s) * coshTerm / sqrtTerm;
        }

        /// <inheritdoc/>
        public XElement ToXElement()
        {
            var element = new XElement(nameof(ASinHLink));
            element.SetAttributeValue(nameof(Gamma0), Gamma0.ToString("G17", CultureInfo.InvariantCulture));
            element.SetAttributeValue(nameof(Scale), Scale.ToString("G17", CultureInfo.InvariantCulture));
            element.SetAttributeValue(nameof(Epsilon), Epsilon.ToString("G17", CultureInfo.InvariantCulture));
            element.SetAttributeValue(nameof(Delta), Delta.ToString("G17", CultureInfo.InvariantCulture));
            element.SetAttributeValue(nameof(UseAdaptiveEpsilon), UseAdaptiveEpsilon.ToString());
            element.SetAttributeValue(nameof(ParentIndicator), ParentIndicator.ToString("G17", CultureInfo.InvariantCulture));
            element.SetAttributeValue(nameof(EpsilonMax), EpsilonMax.ToString("G17", CultureInfo.InvariantCulture));
            element.SetAttributeValue(nameof(EpsilonSlope), EpsilonSlope.ToString("G17", CultureInfo.InvariantCulture));
            return element;
        }

        // --- Adaptive epsilon ---

        /// <summary>
        /// Computes the effective epsilon given current settings.
        /// When <see cref="UseAdaptiveEpsilon"/> is true, maps <see cref="ParentIndicator"/>
        /// to (-EpsilonMax, +EpsilonMax) via tanh saturation.
        /// </summary>
        /// <returns>The effective epsilon value.</returns>
        private double EffectiveEpsilon()
        {
            if (!UseAdaptiveEpsilon)
                return Epsilon;

            return EpsilonMax * Math.Tanh(EpsilonSlope * ParentIndicator);
        }

        // --- Utilities ---

        /// <summary>
        /// Computes the inverse hyperbolic sine (asinh) of x.
        /// Implementation for .NET Framework 4.8.1 which lacks Math.Asinh.
        /// </summary>
        /// <param name="x">The input value.</param>
        /// <returns>The asinh of x.</returns>
        private static double Asinh(double x)
        {
            double ax = Math.Abs(x);
            if (ax < 1e-6) return x;
            return Math.Log(x + Math.Sqrt(x * x + 1.0));
        }

        /// <summary>
        /// Parses a double attribute from an XElement, returning the parsed value or a default.
        /// </summary>
        /// <param name="xElement">The XElement to read from.</param>
        /// <param name="name">The attribute name.</param>
        /// <param name="defaultValue">The default value if the attribute is missing or unparseable.</param>
        /// <returns>The parsed value, or <paramref name="defaultValue"/> if not found.</returns>
        private static double ParseDouble(XElement xElement, string name, double defaultValue)
        {
            var attr = xElement.Attribute(name);
            if (attr != null && double.TryParse(attr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double val))
                return val;
            return defaultValue;
        }
    }
}
