using Numerics.Functions;
using System;
using System.Diagnostics;
using System.Xml.Linq;

namespace RMC.BestFit.Models.LinkFunctions
{
    /// <summary>
    /// Factory for creating <see cref="ILinkFunction"/> instances from serialized <see cref="XElement"/> representations,
    /// supporting both standard Numerics link types and BestFit-specific types (ASinHLink, SESLink, LogSESLink,
    /// LogASinHLink, CenteredLink).
    /// </summary>
    /// <remarks>
    /// <para>
    ///     This factory extends the Numerics <see cref="LinkFunctionFactory"/> by handling BestFit-specific
    ///     link function types first, then falling through to the Numerics factory for standard types.
    /// </para>
    /// <para>
    ///     <b> Authors: </b>
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// </remarks>
    public static class BestFitLinkFunctionFactory
    {
        /// <summary>
        /// Creates an <see cref="ILinkFunction"/> instance from a serialized <see cref="XElement"/>,
        /// supporting both Numerics standard types and BestFit-specific types.
        /// </summary>
        /// <param name="xElement">The XElement representing the link function. The element name must match a known link function class name.</param>
        /// <returns>A new <see cref="ILinkFunction"/> instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="xElement"/> is null.</exception>
        /// <exception cref="NotSupportedException">Thrown when the element name does not correspond to a known link function type.</exception>
        public static ILinkFunction CreateFromXElement(XElement xElement)
        {
            if (xElement == null) throw new ArgumentNullException(nameof(xElement));
            switch (xElement.Name.LocalName)
            {
                // BestFit-specific types
                case nameof(ASinHLink):
                    return new ASinHLink(xElement);
                case nameof(SESLink):
                    return new SESLink(xElement);
                case nameof(LogSESLink):
                    return new LogSESLink(xElement);
                case nameof(LogASinHLink):
                    return new LogASinHLink(xElement);
                case nameof(CenteredLink):
                    return new CenteredLink(xElement);
                case nameof(YeoJohnsonLink):
                    return CreateYeoJohnsonLinkOrIdentity(xElement);
                // Fall through to Numerics factory for standard types
                default:
                    return LinkFunctionFactory.CreateFromXElement(xElement);
            }
        }

        /// <summary>
        /// Creates a Numerics Yeo-Johnson link from XML, falling back to identity for legacy BestFit XML.
        /// </summary>
        /// <param name="xElement">The serialized Yeo-Johnson link element.</param>
        /// <returns>
        /// A <see cref="YeoJohnsonLink"/> when <paramref name="xElement"/> contains a valid lambda;
        /// otherwise an <see cref="IdentityLink"/>.
        /// </returns>
        /// <remarks>
        /// Older BestFit projects could persist a <c>YeoJohnsonLink</c> element without a valid
        /// <c>Lambda</c> attribute because the removed BestFit implementation defaulted to lambda = 1.
        /// Mapping those legacy forms to identity preserves that behavior while using the Numerics link.
        /// </remarks>
        private static ILinkFunction CreateYeoJohnsonLinkOrIdentity(XElement xElement)
        {
            try
            {
                return new YeoJohnsonLink(xElement);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is FormatException || ex is OverflowException)
            {
                Debug.WriteLine($"BestFitLinkFunctionFactory: YeoJohnsonLink XML could not be restored; using IdentityLink. {ex.Message}");
                return new IdentityLink();
            }
        }
    }
}
