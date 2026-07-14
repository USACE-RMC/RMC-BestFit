using System;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Navigation;

namespace RMC_BestFit
{
    /// <summary>
    /// Creates the RMC-BestFit license and citation document displayed by the Terms and Conditions window.
    /// </summary>
    /// <remarks>
    /// The document reproduces the repository's Zero-Clause BSD license verbatim and keeps the optional
    /// citation guidance separate so citation cannot be interpreted as a condition of the license grant.
    /// </remarks>
    internal static class TermsAndConditionsDocumentFactory
    {
        /// <summary>
        /// User-visible label for the RMC-BestFit Zenodo concept DOI hyperlink.
        /// </summary>
        internal const string ZenodoLinkLabel = "RMC-BestFit on Zenodo (Concept DOI: 10.5281/zenodo.21301036)";

        /// <summary>
        /// Creates the RMC-BestFit license and citation document.
        /// </summary>
        /// <param name="citationLinkHandler">Handler invoked when the Zenodo concept DOI hyperlink is activated.</param>
        /// <returns>A formatted document containing the 0BSD license and optional citation guidance.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="citationLinkHandler"/> is <c>null</c>.
        /// </exception>
        /// <remarks>
        /// Browser navigation remains with the application controller so the document factory has no network,
        /// process, or message-box side effects and can be tested deterministically.
        /// </remarks>
        internal static FlowDocument Create(RequestNavigateEventHandler citationLinkHandler)
        {
            if (citationLinkHandler == null) throw new ArgumentNullException(nameof(citationLinkHandler));

            var document = new FlowDocument();
            document.Blocks.Add(CreateHeading("RMC-BestFit License and Citation", 20d));
            document.Blocks.Add(CreateParagraph(
                "RMC-BestFit is free and open-source software developed by the U.S. Army Corps of Engineers Risk Management Center and made available under the Zero-Clause BSD (0BSD) License."));

            document.Blocks.Add(CreateHeading("Zero-Clause BSD License", 16d));
            document.Blocks.Add(CreateParagraph(
                "Permission to use, copy, modify, and/or distribute this software for any purpose with or without fee is hereby granted."));
            document.Blocks.Add(CreateParagraph(
                "THE SOFTWARE IS PROVIDED \"AS IS\" AND THE AUTHOR DISCLAIMS ALL WARRANTIES WITH REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS. IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR ANY SPECIAL, DIRECT, INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES WHATSOEVER RESULTING FROM LOSS OF USE, DATA OR PROFITS, WHETHER IN AN ACTION OF CONTRACT, NEGLIGENCE OR OTHER TORTIOUS ACTION, ARISING OUT OF OR IN CONNECTION WITH THE USE OR PERFORMANCE OF THIS SOFTWARE."));

            document.Blocks.Add(CreateHeading("Citation", 16d));
            document.Blocks.Add(CreateParagraph(
                "Citation is not required by the 0BSD License. If you use RMC-BestFit in research, engineering analysis, technical reports, or other published work, citation is appreciated. For reproducibility, please cite the specific version of RMC-BestFit that you used. Citation metadata and archived releases are available through Zenodo."));

            var hyperlink = new Hyperlink(new Run(ZenodoLinkLabel))
            {
                NavigateUri = new Uri(OnlineHelpLauncher.ZenodoConceptDoiUrl, UriKind.Absolute)
            };
            hyperlink.RequestNavigate += citationLinkHandler;
            document.Blocks.Add(new Paragraph(hyperlink)
            {
                Margin = new Thickness(0, 0, 0, 10)
            });

            return document;
        }

        /// <summary>
        /// Creates a formatted heading paragraph.
        /// </summary>
        /// <param name="text">Heading text.</param>
        /// <param name="fontSize">Heading font size in device-independent units.</param>
        /// <returns>The formatted heading paragraph.</returns>
        /// <remarks>
        /// A zero bottom margin keeps the heading visually attached to the body paragraph that follows it.
        /// </remarks>
        private static Paragraph CreateHeading(string text, double fontSize)
        {
            return new Paragraph(new Run(text))
            {
                FontSize = fontSize,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0)
            };
        }

        /// <summary>
        /// Creates a formatted body paragraph.
        /// </summary>
        /// <param name="text">Paragraph text.</param>
        /// <returns>The formatted body paragraph.</returns>
        /// <remarks>
        /// Consistent spacing separates the license, disclaimer, and citation sections without altering their text.
        /// </remarks>
        private static Paragraph CreateParagraph(string text)
        {
            return new Paragraph(new Run(text))
            {
                Margin = new Thickness(0, 0, 0, 10)
            };
        }
    }
}
