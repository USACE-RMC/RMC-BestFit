using Numerics;
using Numerics.Functions;
using System;
using System.Globalization;
using System.Xml.Linq;

namespace RMC.BestFit.Models.LinkFunctions
{
    /// <summary>
    /// Asymmetric heavy-tailed link on log σ using the Skew-Exponential-Sinh (SES) map.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     r(η) = (1/a) · exp(λ η) · sinh(a η), where r = log(σ/σ₀), a &gt; 0, |λ| &lt; 1.
    ///     Inverse: σ = σ₀ · exp(r(η)). Monotone; tunable asymmetry and tail weight.
    /// </para>
    /// <para>
    ///     <b> Authors: </b>
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// </remarks>
    public sealed class LogSESLink : ILinkFunction
    {
        /// <summary>Center σ0 &gt; 0 (fix per fit; e.g., parent σ̂).</summary>
        public double Sigma0 { get; set; } = 1.0;

        /// <summary>Curvature a &gt; 0 (heavier tails as a increases).</summary>
        public double A { get; set; } = 1.0;

        /// <summary>
        /// If <see cref="UseAdaptiveLambda"/> is false, this fixed λ is used (|λ| &lt; 1).
        /// </summary>
        public double Lambda { get; set; } = 0.2;

        /// <summary>
        /// Enable adaptive asymmetry driven by <see cref="ParentIndicator"/>.
        /// </summary>
        public bool UseAdaptiveLambda { get; set; } = false;

        /// <summary>
        /// Parent indicator controlling asymmetry sign and strength (e.g., max(0, γ̂)).
        /// Positive values strengthen the positive tail of log σ; zero gives symmetric tails.
        /// </summary>
        public double ParentIndicator { get; set; } = 0.0;

        /// <summary>
        /// Maximum magnitude of the adaptive asymmetry (0 &lt;= LambdaMax &lt; 1).
        /// </summary>
        public double LambdaMax { get; set; } = 0.4;

        /// <summary>
        /// Sensitivity of adaptive λ to <see cref="ParentIndicator"/>. Higher values
        /// cause faster saturation toward <see cref="LambdaMax"/>.
        /// </summary>
        public double LambdaSlope { get; set; } = 1.0;

        /// <summary>Maximum Newton iterations for the inverse (link) solve.</summary>
        public int MaxIterations { get; set; } = 20;

        /// <summary>Newton tolerance on parameter update (|Δη|).</summary>
        /// <remarks>
        /// Clamped to a minimum of <c>1e-16</c>. Mirrors <see cref="SESLink.Tolerance"/>:
        /// a non-positive value would make the Newton loop never converge.
        /// </remarks>
        public double Tolerance
        {
            get => _tolerance;
            set => _tolerance = Math.Max(value, 1e-16);
        }
        private double _tolerance = 1e-12;

        /// <summary>Numerical floor for sigma and internal terms to prevent underflow.</summary>
        public double Eps { get; set; } = 1e-12;

        /// <summary>
        /// Indicates whether the most recent <see cref="Link(double)"/> call's Newton
        /// iteration converged (|Δη| &lt; <see cref="Tolerance"/> within
        /// <see cref="MaxIterations"/>). Defaults to <c>true</c> until the first
        /// non-converging call. Diagnostic only — not serialized.
        /// </summary>
        public bool LastInverseConverged { get; private set; } = true;

        /// <summary>
        /// |Δη| at the final iteration of the most recent <see cref="Link(double)"/>
        /// call. Diagnostic only — not serialized. <see cref="double.NaN"/> if Link
        /// has not yet been called.
        /// </summary>
        public double LastInverseResidual { get; private set; } = double.NaN;

        /// <summary>
        /// Constructs a new LogSESLink with default parameters.
        /// </summary>
        public LogSESLink() { }

        /// <summary>
        /// Constructs a new LogSESLink with specified parameters.
        /// </summary>
        /// <param name="sigma0">Center value sigma0 (must be greater than 0).</param>
        /// <param name="a">Curvature parameter a (must be greater than 0). Default is 1.0.</param>
        /// <param name="lambda">Asymmetry parameter lambda (must be in range (-1, 1)). Default is 0.2.</param>
        public LogSESLink(double sigma0, double a = 1.0, double lambda = 0.2)
        {
            Sigma0 = Math.Max(sigma0, 1e-12);
            A = Math.Max(a, 1e-12);
            Lambda = ClampLambda(lambda);
        }

        /// <summary>
        /// Constructs a LogSESLink from a serialized <see cref="XElement"/>.
        /// </summary>
        /// <param name="xElement">The XElement to deserialize from.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="xElement"/> is null.</exception>
        public LogSESLink(XElement xElement)
        {
            if (xElement == null) throw new ArgumentNullException(nameof(xElement));

            var sigma0Attr = xElement.Attribute(nameof(Sigma0));
            if (sigma0Attr != null && double.TryParse(sigma0Attr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double sigma0Val))
                Sigma0 = sigma0Val;

            var aAttr = xElement.Attribute(nameof(A));
            if (aAttr != null && double.TryParse(aAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double aVal))
                A = aVal;

            var lambdaAttr = xElement.Attribute(nameof(Lambda));
            if (lambdaAttr != null && double.TryParse(lambdaAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double lambdaVal))
                Lambda = lambdaVal;

            var useAdaptiveAttr = xElement.Attribute(nameof(UseAdaptiveLambda));
            if (useAdaptiveAttr != null && bool.TryParse(useAdaptiveAttr.Value, out bool useAdaptiveVal))
                UseAdaptiveLambda = useAdaptiveVal;

            var parentAttr = xElement.Attribute(nameof(ParentIndicator));
            if (parentAttr != null && double.TryParse(parentAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double parentVal))
                ParentIndicator = parentVal;

            var lambdaMaxAttr = xElement.Attribute(nameof(LambdaMax));
            if (lambdaMaxAttr != null && double.TryParse(lambdaMaxAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double lambdaMaxVal))
                LambdaMax = lambdaMaxVal;

            var lambdaSlopeAttr = xElement.Attribute(nameof(LambdaSlope));
            if (lambdaSlopeAttr != null && double.TryParse(lambdaSlopeAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double lambdaSlopeVal))
                LambdaSlope = lambdaSlopeVal;

            var maxIterAttr = xElement.Attribute(nameof(MaxIterations));
            if (maxIterAttr != null && int.TryParse(maxIterAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out int maxIterVal))
                MaxIterations = maxIterVal;

            var tolAttr = xElement.Attribute(nameof(Tolerance));
            if (tolAttr != null && double.TryParse(tolAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double tolVal))
                Tolerance = tolVal;

            var epsAttr = xElement.Attribute(nameof(Eps));
            if (epsAttr != null && double.TryParse(epsAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double epsVal))
                Eps = epsVal;
        }

        /// <inheritdoc/>
        public double Link(double x)
        {
            // Solve r(η) = ln(σ/σ0) for η via Newton (monotone ⇒ unique).
            double sigma = Math.Max(x, Eps);
            double r = Math.Log(sigma / Math.Max(Sigma0, Eps));
            double a = Math.Max(A, 1e-12);
            double lambda = EffectiveLambda();

            double eta = InitialGuess(r, a, lambda);

            bool converged = false;
            double lastStep = double.NaN;
            for (int it = 0; it < MaxIterations; it++)
            {
                double f = ROfEta(eta, a, lambda) - r;
                double fp = dR_dEta(eta, a, lambda);
                fp = Math.Max(fp, 1e-16);
                double step = f / fp;
                lastStep = Math.Abs(step);
                double newEta = eta - step;

                if (!Tools.IsFinite(newEta)) newEta = 0.5 * eta;
                if (Math.Abs(step) < Tolerance) { eta = newEta; converged = true; break; }
                if (Math.Abs(step) > 4.0) newEta = eta - Math.Sign(step) * 4.0;

                eta = newEta;
            }

            LastInverseConverged = converged;
            LastInverseResidual = lastStep;
            if (!converged)
                System.Diagnostics.Debug.WriteLine(
                    $"LogSESLink.Link: Newton did not converge after {MaxIterations} iterations (|Δη|={lastStep:G6}, tol={Tolerance:G6}, x={x:G6}).");

            return eta;
        }

        /// <inheritdoc/>
        public double InverseLink(double eta)
        {
            double a = Math.Max(A, 1e-12);
            double lambda = EffectiveLambda();
            double r = ROfEta(eta, a, lambda);
            return Math.Max(Sigma0, Eps) * Math.Exp(r);
        }

        /// <inheritdoc/>
        public double DLink(double x /* dη/dσ */)
        {
            double sigma = Math.Max(x, Eps);
            double r = Math.Log(sigma / Math.Max(Sigma0, Eps));
            double a = Math.Max(A, 1e-12);
            double lambda = EffectiveLambda();

            // dη/dσ = (1/σ) * 1 / (dr/dη)
            double eta = Link(sigma);
            double drdeta = dR_dEta(eta, a, lambda);
            drdeta = Math.Max(drdeta, 1e-16);
            return (1.0 / sigma) * (1.0 / drdeta);
        }

        /// <inheritdoc/>
        public XElement ToXElement()
        {
            var element = new XElement(nameof(LogSESLink));
            element.SetAttributeValue(nameof(Sigma0), Sigma0.ToString("G17", CultureInfo.InvariantCulture));
            element.SetAttributeValue(nameof(A), A.ToString("G17", CultureInfo.InvariantCulture));
            element.SetAttributeValue(nameof(Lambda), Lambda.ToString("G17", CultureInfo.InvariantCulture));
            element.SetAttributeValue(nameof(UseAdaptiveLambda), UseAdaptiveLambda.ToString());
            element.SetAttributeValue(nameof(ParentIndicator), ParentIndicator.ToString("G17", CultureInfo.InvariantCulture));
            element.SetAttributeValue(nameof(LambdaMax), LambdaMax.ToString("G17", CultureInfo.InvariantCulture));
            element.SetAttributeValue(nameof(LambdaSlope), LambdaSlope.ToString("G17", CultureInfo.InvariantCulture));
            element.SetAttributeValue(nameof(MaxIterations), MaxIterations.ToString(CultureInfo.InvariantCulture));
            element.SetAttributeValue(nameof(Tolerance), Tolerance.ToString("G17", CultureInfo.InvariantCulture));
            element.SetAttributeValue(nameof(Eps), Eps.ToString("G17", CultureInfo.InvariantCulture));
            return element;
        }

        /// <summary>
        /// Computes r(η) = (1/a) e^{λ η} sinh(a η).
        /// </summary>
        /// <param name="eta">The link parameter.</param>
        /// <param name="a">Curvature parameter.</param>
        /// <param name="lambda">Asymmetry parameter.</param>
        /// <returns>The value of r at the given eta.</returns>
        private static double ROfEta(double eta, double a, double lambda)
            => SafeExp(lambda * eta) * Math.Sinh(a * eta) / a;

        /// <summary>
        /// Computes the derivative dr/dη = e^{λ η} [ (λ/a) sinh(a η) + cosh(a η) ].
        /// </summary>
        /// <param name="eta">The link parameter.</param>
        /// <param name="a">Curvature parameter.</param>
        /// <param name="lambda">Asymmetry parameter.</param>
        /// <returns>The derivative of r with respect to eta.</returns>
        private static double dR_dEta(double eta, double a, double lambda)
        {
            double e = SafeExp(lambda * eta);
            double s = Math.Sinh(a * eta);
            double c = Math.Cosh(a * eta);
            return e * ((lambda / a) * s + c);
        }

        /// <summary>
        /// Generates an initial guess for the Newton solver based on central and tail-based approximations.
        /// </summary>
        /// <param name="r">The target r value.</param>
        /// <param name="a">Curvature parameter.</param>
        /// <param name="lambda">Asymmetry parameter.</param>
        /// <returns>An initial estimate for eta.</returns>
        private static double InitialGuess(double r, double a, double lambda)
        {
            // Central fallback
            double central = Asinh(a * r) / a;

            // Tail-based guess
            if (Tools.IsFinite(r) && Math.Abs(r) > 0.5)
            {
                double denomPos = a + lambda;
                double denomNeg = a - lambda;

                if (r > 0.0 && denomPos > 1e-10)
                {
                    double val = Math.Log(2.0 * a * r);
                    if (Tools.IsFinite(val)) return val / denomPos;
                }
                else if (r < 0.0 && denomNeg > 1e-10)
                {
                    double val = Math.Log(2.0 * a * (-r));
                    if (Tools.IsFinite(val)) return -val / denomNeg;
                }
            }
            return central;
        }

        /// <summary>
        /// Computes the effective λ given current settings.
        /// Ensures |λ_eff| &lt; 1 for strict monotonicity.
        /// </summary>
        /// <returns>The effective lambda value.</returns>
        private double EffectiveLambda()
        {
            if (!UseAdaptiveLambda)
                return ClampLambda(Lambda);

            // Map ParentIndicator to (-LambdaMax, +LambdaMax) with tanh so that:
            //  - sign follows ParentIndicator (directional inflation),
            //  - magnitude grows with |ParentIndicator| and saturates at LambdaMax,
            //  - ParentIndicator≈0 ⇒ λ_eff≈0 ⇒ symmetric tails (no directional inflation).
            double lamEff = LambdaMax * Math.Tanh(LambdaSlope * ParentIndicator);
            return ClampLambda(lamEff);
        }

        /// <summary>
        /// Clamps lambda to the valid range (-1, 1) to ensure monotonicity.
        /// </summary>
        /// <param name="lambda">The lambda value to clamp.</param>
        /// <returns>Lambda clamped to (-0.999, 0.999).</returns>
        private static double ClampLambda(double lambda)
        {
            if (lambda >= 1.0) return 0.999;
            if (lambda <= -1.0) return -0.999;
            return lambda;
        }

        /// <summary>
        /// Computes the inverse hyperbolic sine (asinh) of x.
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
        /// Computes exp(z) with protection against overflow by clamping z to [-700, 700].
        /// </summary>
        /// <param name="z">The exponent value.</param>
        /// <returns>A safe exponential value.</returns>
        private static double SafeExp(double z)
        {
            if (z > 700.0) return Math.Exp(700.0);
            if (z < -700.0) return Math.Exp(-700.0);
            return Math.Exp(z);
        }
    }
}
