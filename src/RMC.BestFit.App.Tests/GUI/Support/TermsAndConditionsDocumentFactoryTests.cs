using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Navigation;

namespace RMC.BestFit.App.Tests.GUI.Support
{
    /// <summary>
    /// Tests the RMC-BestFit Terms and Conditions document factory.
    /// </summary>
    /// <remarks>
    /// Tests inspect the generated WPF document directly and never open a browser or access the network.
    /// </remarks>
    [TestClass]
    public class TermsAndConditionsDocumentFactoryTests
    {
        /// <summary>
        /// Verifies the generated document contains the exact 0BSD text and optional citation guidance.
        /// </summary>
        /// <remarks>
        /// Paragraph-level comparisons preserve the license wording while allowing the document to style each
        /// section independently. Assertions also prevent restrictive terms from the framework default returning.
        /// </remarks>
        [STATestMethod]
        public void Create_ContainsVerbatimLicenseAndOptionalCitation()
        {
            FlowDocument document = RMC_BestFit.TermsAndConditionsDocumentFactory.Create((sender, e) => { });
            List<Paragraph> paragraphs = document.Blocks.Cast<Block>().OfType<Paragraph>().ToList();

            Assert.AreEqual(8, paragraphs.Count);
            Assert.AreEqual("RMC-BestFit License and Citation", ReadParagraph(paragraphs[0]));
            Assert.AreEqual(
                "RMC-BestFit is free and open-source software developed by the U.S. Army Corps of Engineers Risk Management Center and made available under the Zero-Clause BSD (0BSD) License.",
                ReadParagraph(paragraphs[1]));
            Assert.AreEqual("Zero-Clause BSD License", ReadParagraph(paragraphs[2]));
            Assert.AreEqual(
                "Permission to use, copy, modify, and/or distribute this software for any purpose with or without fee is hereby granted.",
                ReadParagraph(paragraphs[3]));
            Assert.AreEqual(
                "THE SOFTWARE IS PROVIDED \"AS IS\" AND THE AUTHOR DISCLAIMS ALL WARRANTIES WITH REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS. IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR ANY SPECIAL, DIRECT, INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES WHATSOEVER RESULTING FROM LOSS OF USE, DATA OR PROFITS, WHETHER IN AN ACTION OF CONTRACT, NEGLIGENCE OR OTHER TORTIOUS ACTION, ARISING OUT OF OR IN CONNECTION WITH THE USE OR PERFORMANCE OF THIS SOFTWARE.",
                ReadParagraph(paragraphs[4]));
            Assert.AreEqual("Citation", ReadParagraph(paragraphs[5]));
            Assert.AreEqual(
                "Citation is not required by the 0BSD License. If you use RMC-BestFit in research, engineering analysis, technical reports, or other published work, citation is appreciated. For reproducibility, please cite the specific version of RMC-BestFit that you used. Citation metadata and archived releases are available through Zenodo.",
                ReadParagraph(paragraphs[6]));

            string documentText = new TextRange(document.ContentStart, document.ContentEnd).Text;
            Assert.IsFalse(documentText.Contains("may not be modified", StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(documentText.Contains("indemnify", StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(documentText.Contains("voluntarily accept", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Verifies the citation hyperlink displays and targets the concept DOI and invokes its supplied handler.
        /// </summary>
        /// <remarks>
        /// Raising the routed navigation event confirms the factory wires behavior without starting a browser.
        /// </remarks>
        [STATestMethod]
        public void Create_ConfiguresConceptDoiHyperlink()
        {
            object capturedSender = null;
            Uri capturedUri = null;
            RequestNavigateEventHandler handler = (sender, e) =>
            {
                capturedSender = sender;
                capturedUri = e.Uri;
            };

            FlowDocument document = RMC_BestFit.TermsAndConditionsDocumentFactory.Create(handler);
            Paragraph linkParagraph = document.Blocks.Cast<Block>().OfType<Paragraph>().Last();
            Hyperlink hyperlink = linkParagraph.Inlines.FirstInline as Hyperlink;

            Assert.IsNotNull(hyperlink);
            Assert.AreEqual(RMC_BestFit.TermsAndConditionsDocumentFactory.ZenodoLinkLabel, ReadInline(hyperlink));
            Assert.AreEqual(RMC_BestFit.OnlineHelpLauncher.ZenodoConceptDoiUrl, hyperlink.NavigateUri.AbsoluteUri);

            var eventArgs = new RequestNavigateEventArgs(hyperlink.NavigateUri, string.Empty)
            {
                RoutedEvent = Hyperlink.RequestNavigateEvent
            };
            hyperlink.RaiseEvent(eventArgs);

            Assert.AreSame(hyperlink, capturedSender);
            Assert.AreEqual(hyperlink.NavigateUri, capturedUri);
        }

        /// <summary>
        /// Verifies the document factory rejects a missing citation navigation handler.
        /// </summary>
        /// <remarks>
        /// Requiring the handler prevents the document from presenting a hyperlink that cannot respond to users.
        /// </remarks>
        [STATestMethod]
        public void Create_WithNullHandler_ThrowsArgumentNullException()
        {
            Assert.ThrowsException<ArgumentNullException>(() =>
                RMC_BestFit.TermsAndConditionsDocumentFactory.Create(null));
        }

        /// <summary>
        /// Reads the visible text from a document paragraph without its trailing paragraph terminator.
        /// </summary>
        /// <param name="paragraph">Paragraph to read.</param>
        /// <returns>The visible paragraph text.</returns>
        /// <remarks>
        /// WPF text ranges append carriage-return and line-feed characters at block boundaries.
        /// </remarks>
        private static string ReadParagraph(Paragraph paragraph)
        {
            return new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text.TrimEnd('\r', '\n');
        }

        /// <summary>
        /// Reads the visible text from an inline element.
        /// </summary>
        /// <param name="inline">Inline element to read.</param>
        /// <returns>The visible inline text.</returns>
        /// <remarks>
        /// A text range captures the hyperlink label without depending on its concrete child-inline structure.
        /// </remarks>
        private static string ReadInline(Inline inline)
        {
            return new TextRange(inline.ContentStart, inline.ContentEnd).Text;
        }
    }
}
