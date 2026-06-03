using Numerics.Data.Statistics;
using Numerics.Functions;
using System;
using System.Globalization;
using System.Xml.Linq;

namespace RMC.BestFit.Models.LinkFunctions
{
    /// <summary>
    /// A Yeo–Johnson power-transformation link function for bootstrap pivot methods.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    ///     The Yeo–Johnson transformation is a generalization of the Box–Cox transformation that
    ///     handles both positive and negative values. The power parameter λ is estimated from the
    ///     bootstrap parameter samples using maximum likelihood, making the transformed values
    ///     approximately normal.
    /// </para>
    /// <para>
    ///     This link is used in the parametric bootstrap pivot method to variance-stabilize
    ///     location and shape parameter estimates before computing pivots. The fitted λ adapts
    ///     to the empirical distribution of bootstrap estimates.
    /// </para>
    /// <para>
    ///     Reference: Yeo, I.-K. and Johnson, R.A. (2000). A new family of power transformations
    ///     to improve normality or symmetry. Biometrika, 87(4), 954–959.
    /// </para>
    /// </remarks>
    public class YeoJohnsonLink : ILinkFunction
    {
        /// <summary>
        /// The fitted Yeo–Johnson power parameter.
        /// </summary>
        public double Lambda { get; set; } = 1.0;

        /// <summary>
        /// Constructs a new <see cref="YeoJohnsonLink"/> with a default identity transform (λ = 1).
        /// </summary>
        public YeoJohnsonLink() { }

        /// <summary>
        /// Constructs a new <see cref="YeoJohnsonLink"/> with a specified power parameter.
        /// </summary>
        /// <param name="lambda">The Yeo–Johnson power parameter.</param>
        public YeoJohnsonLink(double lambda)
        {
            Lambda = lambda;
        }

        /// <summary>
        /// Constructs a new <see cref="YeoJohnsonLink"/> by fitting the power parameter from a sample of values.
        /// </summary>
        /// <param name="values">
        /// The sample values used to estimate λ via maximum likelihood. Typically the bootstrap
        /// parameter estimates for a single parameter across all B replicates.
        /// </param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="values"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="values"/> has fewer than 2 elements.</exception>
        /// <remarks>
        /// Uses <see cref="YeoJohnson.FitLambda(double[], out double)"/> from the Numerics library
        /// to estimate the optimal power parameter via maximum profile log-likelihood.
        /// </remarks>
        public YeoJohnsonLink(double[] values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            if (values.Length < 2) throw new ArgumentException("At least 2 values are required to fit lambda.", nameof(values));

            YeoJohnson.FitLambda(values, out var lambda);
            Lambda = lambda;
        }

        /// <summary>
        /// Constructs a <see cref="YeoJohnsonLink"/> from a serialized <see cref="XElement"/>.
        /// </summary>
        /// <param name="xElement">The XElement to deserialize from.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="xElement"/> is null.</exception>
        public YeoJohnsonLink(XElement xElement)
        {
            if (xElement == null) throw new ArgumentNullException(nameof(xElement));

            var lambdaAttr = xElement.Attribute(nameof(Lambda));
            if (lambdaAttr != null && double.TryParse(lambdaAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double lambdaVal))
                Lambda = lambdaVal;
        }

        /// <summary>
        /// Applies the link function h(x) = YeoJohnson.Transform(x, λ), mapping real-space to link-space.
        /// </summary>
        /// <param name="x">The real-space parameter value.</param>
        /// <returns>The link-space value η = h(x).</returns>
        public double Link(double x)
        {
            return YeoJohnson.Transform(x, Lambda);
        }

        /// <summary>
        /// Applies the inverse link h⁻¹(η), mapping link-space back to real-space.
        /// </summary>
        /// <param name="eta">The link-space value.</param>
        /// <returns>The real-space parameter value x = h⁻¹(η).</returns>
        public double InverseLink(double eta)
        {
            return YeoJohnson.InverseTransform(eta, Lambda);
        }

        /// <summary>
        /// Computes the derivative of the link function dη/dx at point x.
        /// </summary>
        /// <param name="x">The real-space parameter value.</param>
        /// <returns>The derivative dη/dx = h'(x).</returns>
        /// <remarks>
        /// For x ≥ 0: dη/dx = (x + 1)^(λ − 1).
        /// For x &lt; 0: dη/dx = (−x + 1)^(1 − λ).
        /// Both cases reduce to 1 when λ = 1 (identity transform).
        /// </remarks>
        public double DLink(double x)
        {
            return x >= 0
                ? Math.Pow(x + 1.0, Lambda - 1.0)
                : Math.Pow(-x + 1.0, 1.0 - Lambda);
        }

        /// <summary>
        /// Serializes this link function to an <see cref="XElement"/>.
        /// </summary>
        /// <returns>An XElement representing this link function.</returns>
        public XElement ToXElement()
        {
            // "G17" round-trips a double exactly; default G15 loses bits.
            return new XElement(nameof(YeoJohnsonLink),
                new XAttribute(nameof(Lambda), Lambda.ToString("G17", CultureInfo.InvariantCulture)));
        }
    }
}
