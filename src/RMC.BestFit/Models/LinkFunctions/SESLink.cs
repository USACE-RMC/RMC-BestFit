using Numerics;
using Numerics.Functions;
using System;
using System.Globalization;
using System.Xml.Linq;

namespace RMC.BestFit.Models.LinkFunctions
{
    /// <summary>
    /// A skew–exponential–sinh (SES) link function with tunable asymmetry and fat tails.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    /// Unbounded, strictly monotone, near-linear around 0, and heavy-tailed with controllable asymmetry.<br/>
    /// Forward (inverse link): γ(η) = (1/a) · exp(λ η) · sinh(a η), with a &gt; 0 and |λ| &lt; 1.<br/>
    /// Derivative: dγ/dη = exp(λ η) · [ (λ/a) sinh(a η) + cosh(a η) ].<br/>
    /// Link h(x) solves η = h(γ) as the unique root of γ(η) - γ = 0 (Newton method).
    /// </para>
    /// <para>
    /// Interpretation: a controls overall curvature/fat tails; λ controls tail asymmetry (λ&gt;0
    /// accentuates the positive γ tail and tempers the negative, λ&lt;0 does the opposite).
    /// Local slope at 0 is 1, so the optimizer behaves similarly to the identity near γ=0.
    /// </para>
    /// </remarks>
    public class SESLink : ILinkFunction
    {
        /// <summary>
        /// Curvature / symmetric tail-heaviness (a &gt; 0). Larger <c>a</c> ⇒ fatter tails on both sides.
        /// </summary>
        public double A { get; set; } = 1.0;

        /// <summary>
        /// If <see cref="UseAdaptiveLambda"/> is false, this fixed λ is used (|λ| &lt; 1).
        /// </summary>
        public double Lambda { get; set; } = 0.4;

        /// <summary>
        /// Enable adaptive asymmetry driven by <see cref="ParentIndicator"/>.
        /// </summary>
        public bool UseAdaptiveLambda { get; set; } = true;

        /// <summary>
        /// Parent indicator controlling asymmetry sign and strength (e.g., parent γ̂).
        /// Positive values strengthen the positive tail; negative values strengthen the negative tail.
        /// </summary>
        public double ParentIndicator { get; set; } = 0.0;

        /// <summary>
        /// Maximum magnitude of the adaptive asymmetry (0 &lt;= LambdaMax &lt; 1).
        /// </summary>
        public double LambdaMax { get; set; } = 0.8;

        /// <summary>
        /// Sensitivity of λ_eff to the parent indicator. Larger ⇒ faster approach to ±LambdaMax.
        /// </summary>
        public double LambdaSlope { get; set; } = 1.0;

        /// <summary>Maximum Newton iterations for the inverse (link) solve.</summary>
        public int MaxIterations { get; set; } = 20;

        /// <summary>Newton tolerance on parameter update (|Δη|).</summary>
        /// <remarks>
        /// Clamped to a minimum of <c>1e-16</c>. A non-positive value would make the
        /// convergence loop never converge — every step's <c>Math.Abs(Δη)</c> would
        /// exceed it. The constructor already enforces the same minimum.
        /// </remarks>
        public double Tolerance
        {
            get => _tolerance;
            set => _tolerance = Math.Max(value, 1e-16);
        }
        private double _tolerance = 1e-12;

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
        /// Constructs a new SkewExponentialSinhLink with default settings (a=1.0, adaptive λ enabled).
        /// </summary>
        public SESLink() { }

        /// <summary>
        /// Constructs a new SkewExponentialSinhLink with custom settings.
        /// </summary>
        /// <param name="a">Curvature / symmetric tail-heaviness parameter (must be greater than 0).</param>
        /// <param name="useAdaptiveLambda">If true, enables adaptive asymmetry driven by ParentIndicator. Default is true.</param>
        /// <param name="parentIndicator">Parent indicator controlling asymmetry (e.g., parent γ̂). Default is 0.0.</param>
        /// <param name="lambdaMax">Maximum magnitude of adaptive asymmetry (must be in [0, 1)). Default is 0.4.</param>
        /// <param name="lambdaSlope">Sensitivity of λ_eff to parent indicator. Default is 1.0.</param>
        /// <param name="maxIterations">Maximum Newton iterations for inverse solve. Default is 20.</param>
        /// <param name="tolerance">Newton tolerance on parameter update. Default is 1e-12.</param>
        public SESLink(
            double a,
            bool useAdaptiveLambda = true,
            double parentIndicator = 0.0,
            double lambdaMax = 0.4,
            double lambdaSlope = 1.0,
            int maxIterations = 20,
            double tolerance = 1e-12)
        {
            A = a;
            UseAdaptiveLambda = useAdaptiveLambda;
            ParentIndicator = parentIndicator;
            LambdaMax = lambdaMax;
            LambdaSlope = lambdaSlope;
            MaxIterations = Math.Max(1, maxIterations);
            Tolerance = Math.Max(1e-16, tolerance);
        }

        /// <summary>
        /// Constructs a SESLink from a serialized <see cref="XElement"/>.
        /// </summary>
        /// <param name="xElement">The XElement to deserialize from.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="xElement"/> is null.</exception>
        public SESLink(XElement xElement)
        {
            if (xElement == null) throw new ArgumentNullException(nameof(xElement));

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
        }

        // --- ILinkFunction ---

        /// <inheritdoc/>
        public double Link(double x)
        {
            // Solve for η given γ=x using Newton with a good asymptotic/central initial guess.
            double a = Math.Max(A, 1e-12);
            double lambda = EffectiveLambda();

            double eta = InitialGuess(x, a, lambda);

            bool converged = false;
            double lastStep = double.NaN;
            for (int it = 0; it < MaxIterations; it++)
            {
                double f = GammaOfEta(eta, a, lambda) - x; // f(η) = γ(η) - γ
                double fp = dGamma_dEta(eta, a, lambda);    // f'(η) = dγ/dη
                fp = Math.Max(fp, 1e-16);
                double step = f / fp;
                lastStep = Math.Abs(step);

                double newEta = eta - step;
                if (!Tools.IsFinite(newEta))
                    newEta = 0.5 * eta;

                if (Math.Abs(step) < Tolerance)
                {
                    eta = newEta;
                    converged = true;
                    break;
                }

                // Mild damping in very extreme tails. Intentionally evaluated AFTER
                // the convergence check so a step that satisfies tolerance exits via
                // the un-damped value (preserves Newton's quadratic convergence near
                // the root). Damping only kicks in for the next iteration when the
                // current step is large (> 4 in η-space), preventing runaway updates.
                if (Math.Abs(step) > 4.0)
                    newEta = eta - Math.Sign(step) * 4.0;

                eta = newEta;
            }

            LastInverseConverged = converged;
            LastInverseResidual = lastStep;
            if (!converged)
                System.Diagnostics.Debug.WriteLine(
                    $"SESLink.Link: Newton did not converge after {MaxIterations} iterations (|Δη|={lastStep:G6}, tol={Tolerance:G6}, x={x:G6}).");

            return eta;
        }

        /// <inheritdoc/>
        public double InverseLink(double eta)
        {
            double a = Math.Max(A, 1e-12);
            double lambda = EffectiveLambda();
            return GammaOfEta(eta, a, lambda);
        }

        /// <inheritdoc/>
        public double DLink(double x)
        {
            // h'(x) = dη/dγ = 1 / (dγ/dη) evaluated at η = h(x)
            double eta = Link(x);
            double a = Math.Max(A, 1e-12);
            double lambda = EffectiveLambda();
            double dgdeta = dGamma_dEta(eta, a, lambda);
            dgdeta = Math.Max(dgdeta, 1e-16);
            return 1.0 / dgdeta;
        }

        /// <inheritdoc/>
        public XElement ToXElement()
        {
            var element = new XElement(nameof(SESLink));
            element.SetAttributeValue(nameof(A), A.ToString("G17", CultureInfo.InvariantCulture));
            element.SetAttributeValue(nameof(Lambda), Lambda.ToString("G17", CultureInfo.InvariantCulture));
            element.SetAttributeValue(nameof(UseAdaptiveLambda), UseAdaptiveLambda.ToString());
            element.SetAttributeValue(nameof(ParentIndicator), ParentIndicator.ToString("G17", CultureInfo.InvariantCulture));
            element.SetAttributeValue(nameof(LambdaMax), LambdaMax.ToString("G17", CultureInfo.InvariantCulture));
            element.SetAttributeValue(nameof(LambdaSlope), LambdaSlope.ToString("G17", CultureInfo.InvariantCulture));
            element.SetAttributeValue(nameof(MaxIterations), MaxIterations.ToString(CultureInfo.InvariantCulture));
            element.SetAttributeValue(nameof(Tolerance), Tolerance.ToString("G17", CultureInfo.InvariantCulture));
            return element;
        }

        // --- Core formulas ---

        /// <summary>
        /// Computes γ(η) = (1/a) e^{λ η} sinh(a η).
        /// </summary>
        /// <param name="eta">The link parameter.</param>
        /// <param name="a">Curvature parameter.</param>
        /// <param name="lambda">Asymmetry parameter.</param>
        /// <returns>The gamma value at the given eta.</returns>
        private static double GammaOfEta(double eta, double a, double lambda)
        {
            // γ(η) = (1/a) e^{λ η} sinh(a η)
            return SafeExp(lambda * eta) * Math.Sinh(a * eta) / a;
        }

        /// <summary>
        /// Computes the derivative dγ/dη = e^{λ η} [ (λ/a) sinh(a η) + cosh(a η) ].
        /// </summary>
        /// <param name="eta">The link parameter.</param>
        /// <param name="a">Curvature parameter.</param>
        /// <param name="lambda">Asymmetry parameter.</param>
        /// <returns>The derivative of gamma with respect to eta.</returns>
        private static double dGamma_dEta(double eta, double a, double lambda)
        {
            // dγ/dη = e^{λ η} [ (λ/a) sinh(a η) + cosh(a η) ]
            double e = SafeExp(lambda * eta);
            double s = Math.Sinh(a * eta);
            double c = Math.Cosh(a * eta);
            return e * ((lambda / a) * s + c);
        }

        // --- Adaptive λ ---

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
        /// Clamps lambda to strictly within (-1, 1) to preserve global monotonicity.
        /// </summary>
        /// <param name="lambda">The lambda value to clamp.</param>
        /// <returns>Lambda clamped to (-0.999, 0.999).</returns>
        private static double ClampLambda(double lambda)
        {
            // Keep strictly within (-1,1) to preserve global monotonicity.
            if (lambda >= 1.0) return 0.999;
            if (lambda <= -1.0) return -0.999;
            return lambda;
        }

        // --- Initialization & utilities ---

        /// <summary>
        /// Generates an initial guess for the Newton solver using both central and tail-based approximations.
        /// </summary>
        /// <param name="gamma">The target gamma value.</param>
        /// <param name="a">Curvature parameter.</param>
        /// <param name="lambda">Asymmetry parameter.</param>
        /// <returns>An initial estimate for eta.</returns>
        private static double InitialGuess(double gamma, double a, double lambda)
        {
            // Central fallback: symmetric guess
            double central = Asinh(a * gamma) / a;

            // Tail-based guess to speed convergence when |γ| is large
            if (Tools.IsFinite(gamma) && Math.Abs(gamma) > 1.0)
            {
                double denomPos = a + lambda;
                double denomNeg = a - lambda;

                if (gamma > 0.0 && denomPos > 1e-10)
                {
                    double val = Math.Log(2.0 * a * gamma);
                    if (Tools.IsFinite(val)) return val / denomPos;
                }
                else if (gamma < 0.0 && denomNeg > 1e-10)
                {
                    double val = Math.Log(2.0 * a * (-gamma));
                    if (Tools.IsFinite(val)) return -val / denomNeg;
                }
            }

            return central;
        }

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
        /// Computes exp(z) with protection against overflow by clamping z to [-700, 700].
        /// Reduces overflow risk in extreme tails.
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
