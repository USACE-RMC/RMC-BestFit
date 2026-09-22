"""Shared deterministic PDF finalization for the publication reports."""

from __future__ import annotations

import json
import re
from io import BytesIO
from pathlib import Path

from pypdf import PdfReader, PdfWriter
from reportlab.lib.colors import HexColor
from reportlab.pdfgen import canvas


def _load_metadata(repository_root: Path) -> dict:
    """Load the controlled report metadata file."""
    metadata_path = repository_root / "docs" / "report-metadata.json"
    return json.loads(metadata_path.read_text(encoding="utf-8"))


def _create_overlay(
    width: float,
    height: float,
    page_number: int,
    page_count: int,
    short_title: str,
    status: str,
) -> BytesIO:
    """Create a one-page header and footer overlay."""
    packet = BytesIO()
    overlay = canvas.Canvas(packet, pagesize=(width, height), pageCompression=1)
    navy = HexColor("#15324b")
    muted = HexColor("#60717f")
    line = HexColor("#c8d4dd")

    overlay.setStrokeColor(line)
    overlay.setLineWidth(0.45)
    overlay.line(50, height - 34, width - 50, height - 34)
    overlay.line(50, 32, width - 50, 32)

    overlay.setFillColor(navy)
    overlay.setFont("Helvetica-Bold", 7.4)
    overlay.drawString(50, height - 26, short_title)

    overlay.setFillColor(muted)
    overlay.setFont("Helvetica", 7.2)
    overlay.drawRightString(width - 50, height - 26, status)
    overlay.drawString(50, 21, "Risk Management Center")
    overlay.drawRightString(width - 50, 21, f"{page_number} / {page_count}")

    overlay.save()
    packet.seek(0)
    return packet


def _normalize_text(value: str) -> str:
    """Normalize extracted PDF text for deterministic title matching."""
    return " ".join(value.split()).casefold()


def _load_chapter_titles(repository_root: Path, source_directory: str) -> list[str]:
    """Read level-one titles in publication-manifest order."""
    reference_root = repository_root / "docs" / source_directory
    manifest = reference_root / "book-order.txt"
    titles: list[str] = []
    for raw_entry in manifest.read_text(encoding="utf-8").splitlines():
        entry = raw_entry.strip()
        if not entry or entry.startswith("#"):
            continue
        source = (reference_root / entry).read_text(encoding="utf-8")
        title_match = re.search(r"^#\s+(.+?)\s*$", source, flags=re.MULTILINE)
        if title_match is None:
            raise ValueError(f"Publication chapter has no level-one title: {entry}")
        titles.append(title_match.group(1).strip())
    return titles


def _find_chapter_pages(reader: PdfReader, titles: list[str]) -> list[tuple[str, int]]:
    """Locate chapter openings in the rendered PDF in manifest order."""
    page_text = [_normalize_text(page.extract_text() or "") for page in reader.pages]
    matches: list[tuple[str, int]] = []
    cursor = 2
    for title in titles:
        normalized_title = _normalize_text(title)
        for page_index in range(cursor, len(page_text)):
            if normalized_title in page_text[page_index]:
                matches.append((title, page_index))
                cursor = page_index + 1
                break
        else:
            raise ValueError(f"Could not locate rendered chapter title in PDF: {title}")
    return matches


def _add_contents_page_numbers(reader: PdfReader, writer: PdfWriter) -> None:
    """Print destination page numbers in the space reserved by the contents CSS."""
    page = writer.pages[1]
    packet = BytesIO()
    overlay = canvas.Canvas(packet, pagesize=(float(page.mediabox.width), float(page.mediabox.height)))
    overlay.setFont("Helvetica", 9)
    overlay.setFillColor(HexColor("#15324b"))
    destinations = {str(name).lstrip("/"): value for name, value in reader.named_destinations.items()}
    for reference in reader.pages[1].get("/Annots", []):
        annotation = reference.get_object()
        name = str(annotation.get("/Dest", "")).lstrip("/")
        if name not in destinations:
            continue
        destination_page = reader.get_destination_page_number(destinations[name])
        _, _, right, top = [float(value) for value in annotation["/Rect"]]
        overlay.drawRightString(right - 1, top - 10, str(destination_page + 1))
    overlay.save()
    packet.seek(0)
    page.merge_page(PdfReader(packet).pages[0], over=True)


def finalize_report_pdf(
    input_path: Path,
    output_path: Path,
    report_key: str,
    source_directory: str,
    subject: str,
    keywords: str,
) -> None:
    """Preserve the tagged document, add furniture and outlines, and set metadata."""
    repository_root = Path(__file__).resolve().parents[1]
    metadata = _load_metadata(repository_root)
    report = metadata[report_key]
    metadata = {**metadata, **report.get("checkpoint", {})}
    reader = PdfReader(str(input_path))
    if not reader.pages:
        raise ValueError(f"Input PDF contains no pages: {input_path}")

    chapter_pages = _find_chapter_pages(
        reader,
        _load_chapter_titles(repository_root, source_directory),
    )
    writer = PdfWriter()
    writer.clone_document_from_reader(reader)
    page_count = len(writer.pages)

    if report_key == "verification_report":
        _add_contents_page_numbers(reader, writer)
        # Keep the complete current acceptance/provenance record with the PDF,
        # including before the editorial workspace changes are committed.
        for name, relative in (
            ("verification-catalog.json", "docs/verification/verification-catalog.json"),
            ("report-metadata.json", "docs/report-metadata.json"),
        ):
            writer.add_attachment(name, (repository_root / relative).read_bytes())

    for index in range(1, page_count):
        page = writer.pages[index]
        overlay_reader = PdfReader(
            _create_overlay(
                float(page.mediabox.width),
                float(page.mediabox.height),
                index + 1,
                page_count,
                report["short_title"],
                "External peer-review draft",
            )
        )
        page.merge_page(overlay_reader.pages[0], over=True)

    writer.add_outline_item(report["title"], 0, bold=True)
    writer.add_outline_item("Contents", 1)
    for title, page_index in chapter_pages:
        writer.add_outline_item(title, page_index)

    author_names = ", ".join(author["name"] for author in metadata["authors"])
    pdf_date = metadata["publication_date_iso"].replace("-", "")
    writer.add_metadata(
        {
            "/Title": report["title"],
            "/Author": author_names,
            "/Subject": subject,
            "/Keywords": keywords,
            "/Creator": "RMC.BestFit reproducible publication build",
            "/Producer": "Chromium, pypdf, and ReportLab",
            "/CreationDate": f"D:{pdf_date}000000+00'00'",
            "/ModDate": f"D:{pdf_date}000000+00'00'",
            "/BestFitCommit": metadata["bestfit_commit"],
            "/NumericsCommit": metadata["numerics_source_commit"],
            "/ReleaseStatus": metadata["release_status"],
        }
    )

    output_path.parent.mkdir(parents=True, exist_ok=True)
    with output_path.open("wb") as output_stream:
        writer.write(output_stream)

    print(f"Finalized {output_path} ({page_count} pages, {len(chapter_pages)} chapters).")
