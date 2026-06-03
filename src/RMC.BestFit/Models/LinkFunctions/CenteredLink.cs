using Numerics.Functions;
using System;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace RMC.BestFit.Models.LinkFunctions
{
    /// <summary>
    /// Affine wrapper that centers and scales any <see cref="ILinkFunction"/>:
    /// z = (x - μ₀)/s, then η = inner.Link(z).
    /// Inverse: x = μ₀ + s · inner.InverseLink(η).
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Allows recentering link computations around a focal point (e.g., parent estimate)
    ///     without recomputing the inner link's tuning parameters.
    /// </para>
    /// <para>
    ///     <b> Authors: </b>
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// </remarks>
    public sealed class CenteredLink : ILinkFunction
    {
        /// <summary>The inner link being centered/scaled (e.g., SESLink).</summary>
        public ILinkFunction Inner { get; private set; }

        /// <summary>Center μ₀ in x-units (e.g., parent estimate).</summary>
        public double Mu0 { get; set; }

        /// <summary>Scale s &gt; 0 in x-units (sets how quickly the link departs from linearity).</summary>
        public double Scale { get; set; } = 1.0;

        /// <summary>
        /// Constructs a centered link wrapper around an inner link function.
        /// </summary>
        /// <param name="inner">The inner link function to wrap. Must not be null.</param>
        /// <param name="mu0">Center value in x-units (e.g., parent estimate).</param>
        /// <param name="scale">Scale value (must be positive). Default is 1.0.</param>
        /// <exception cref="ArgumentNullException">Thrown if inner is null.</exception>
        public CenteredLink(ILinkFunction inner, double mu0, double scale = 1.0)
        {
            Inner = inner ?? throw new ArgumentNullException(nameof(inner));
            Mu0 = mu0;
            Scale = Math.Max(1e-12, scale);
        }

        /// <summary>
        /// Constructs a CenteredLink from a serialized <see cref="XElement"/>.
        /// </summary>
        /// <param name="xElement">The XElement to deserialize from.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="xElement"/> is null.</exception>
        /// <remarks>
        /// <para>
        ///     Reads Mu0 and Scale from attributes. The inner link function is deserialized from
        ///     the first child element via <see cref="BestFitLinkFunctionFactory.CreateFromXElement(XElement)"/>.
        ///     Falls back to <see cref="IdentityLink"/> if no inner link element is present.
        /// </para>
        /// </remarks>
        public CenteredLink(XElement xElement)
        {
            if (xElement == null) throw new ArgumentNullException(nameof(xElement));

            var mu0Attr = xElement.Attribute(nameof(Mu0));
            if (mu0Attr != null && double.TryParse(mu0Attr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double mu0Val))
                Mu0 = mu0Val;

            var scaleAttr = xElement.Attribute(nameof(Scale));
            if (scaleAttr != null && double.TryParse(scaleAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double scaleVal))
                Scale = scaleVal;

            var innerElement = xElement.Elements().FirstOrDefault();
            if (innerElement != null)
                Inner = BestFitLinkFunctionFactory.CreateFromXElement(innerElement);
            else
                Inner = new IdentityLink();
        }

        /// <inheritdoc/>
        public double Link(double x)
        {
            double z = (x - Mu0) / Scale;
            return Inner.Link(z);
        }

        /// <inheritdoc/>
        public double InverseLink(double eta)
        {
            double z = Inner.InverseLink(eta);
            return Mu0 + Scale * z;
        }

        /// <inheritdoc/>
        public double DLink(double x)
        {
            // dη/dx = (dη/dz) * (dz/dx) = Inner.DLink(z) * (1/Scale)
            double z = (x - Mu0) / Scale;
            return Inner.DLink(z) / Scale;
        }

        /// <inheritdoc/>
        public XElement ToXElement()
        {
            var element = new XElement(nameof(CenteredLink));
            element.SetAttributeValue(nameof(Mu0), Mu0.ToString("G17", CultureInfo.InvariantCulture));
            element.SetAttributeValue(nameof(Scale), Scale.ToString("G17", CultureInfo.InvariantCulture));
            element.Add(Inner.ToXElement());
            return element;
        }
    }
}
