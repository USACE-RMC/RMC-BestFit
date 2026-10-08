"""Focused regression tests for multi-page publication navigation."""
from io import BytesIO
import unittest

from pypdf import PdfReader, PdfWriter
from pypdf.generic import ArrayObject, DictionaryObject, FloatObject, NameObject, TextStringObject
from reportlab.pdfgen import canvas

from pdf_report_common import _add_contents_page_numbers, _find_chapter_pages


def fixture(prefix: str = '') -> PdfReader:
    """Build two contents pages with wrapped links and a misleading title mention."""
    data = BytesIO()
    drawing = canvas.Canvas(data, pagesize=(612, 792))
    for title in ['Cover', 'Contents Alpha', 'Contents Beta', 'Alpha mentions Beta', 'Alpha continued', 'Beta']:
        drawing.drawString(50, 700, title)
        drawing.showPage()
    drawing.save()
    writer = PdfWriter(clone_from=PdfReader(BytesIO(data.getvalue())))
    for name, page in [('doc-alpha', 3), ('doc-beta', 5)]:
        writer.add_named_destination(prefix + name, page)
    for page, name, top in [(1, 'doc-alpha', 650), (1, 'doc-alpha', 638), (2, 'doc-beta', 650)]:
        writer.add_annotation(page, DictionaryObject({
            NameObject('/Type'): NameObject('/Annot'),
            NameObject('/Subtype'): NameObject('/Link'),
            NameObject('/Rect'): ArrayObject([FloatObject(x) for x in (60, top-12, 280, top)]),
        }))
        writer.pages[page]['/Annots'][-1].get_object()[NameObject('/Dest')] = TextStringObject(prefix + name)
    result = BytesIO()
    writer.write(result)
    return PdfReader(BytesIO(result.getvalue()))


class PublicationNavigationTests(unittest.TestCase):
    """Check real destinations rather than text coincidences or a fixed TOC page."""

    def test_chapter_destinations_skip_contents_and_earlier_mentions(self):
        """A repeated chapter title must not redirect its bookmark."""
        for prefix in ['', '/']:
            with self.subTest(prefix=prefix):
                self.assertEqual(
                    _find_chapter_pages(fixture(prefix), ['Alpha', 'Beta'], ['doc-alpha', 'doc-beta']),
                    [('Alpha', 3), ('Beta', 5)],
                )

    def test_all_contents_pages_are_numbered_once_per_destination(self):
        """Wrapped title annotations receive one page number on each contents page."""
        reader = fixture()
        writer = PdfWriter(clone_from=reader)
        _add_contents_page_numbers(reader, writer, range(1, 3))
        self.assertEqual(writer.pages[1].extract_text().split().count('4'), 1)
        self.assertEqual(writer.pages[2].extract_text().split().count('6'), 1)
        self.assertNotIn('4', writer.pages[0].extract_text().split())

    def test_missing_chapter_destination_is_an_explicit_failure(self):
        """A removed anchor must stop finalization instead of guessing from body text."""
        with self.assertRaisesRegex(ValueError, 'doc-missing'):
            _find_chapter_pages(fixture(), ['Missing'], ['doc-missing'])


if __name__ == '__main__':
    unittest.main()
