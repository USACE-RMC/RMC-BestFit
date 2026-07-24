"""Apply deterministic metadata and page furniture to the technical-reference PDF."""

from __future__ import annotations

import argparse
from io import BytesIO
from pathlib import Path

from pypdf import PdfReader, PdfWriter
from reportlab.lib.colors import HexColor
from reportlab.pdfgen import canvas


def create_overlay(width: float, height: float, page_number: int, page_count: int) -> BytesIO:
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
    overlay.drawString(50, height - 26, "RMC.BestFit 2.0 Technical Reference")

    overlay.setFillColor(muted)
    overlay.setFont("Helvetica", 7.2)
    overlay.drawRightString(width - 50, height - 26, "Peer-review release")
    overlay.drawString(50, 21, "Risk Management Center")
    overlay.drawRightString(width - 50, 21, f"{page_number} / {page_count}")

    overlay.save()
    packet.seek(0)
    return packet


def finalize_pdf(input_path: Path, output_path: Path) -> None:
    """Merge page furniture and write stable release metadata."""
    reader = PdfReader(str(input_path))
    if not reader.pages:
        raise ValueError(f"Input PDF contains no pages: {input_path}")

    writer = PdfWriter()
    page_count = len(reader.pages)

    for index, page in enumerate(reader.pages):
        if index > 0:
            width = float(page.mediabox.width)
            height = float(page.mediabox.height)
            overlay_reader = PdfReader(create_overlay(width, height, index + 1, page_count))
            page.merge_page(overlay_reader.pages[0], over=True)
        writer.add_page(page)

    writer.add_metadata(
        {
            "/Title": "RMC.BestFit 2.0 Technical Reference",
            "/Author": "USACE Risk Management Center",
            "/Subject": "Statistical and hydrologic technical reference for RMC.BestFit 2.0",
            "/Keywords": "flood frequency, Bayesian inference, hydrology, uncertainty, RMC.BestFit",
            "/Creator": "RMC.BestFit reproducible technical-reference build",
            "/Producer": "Chromium, pypdf, and ReportLab",
            "/CreationDate": "D:20260723000000+00'00'",
            "/ModDate": "D:20260723000000+00'00'",
        }
    )

    output_path.parent.mkdir(parents=True, exist_ok=True)
    with output_path.open("wb") as output_stream:
        writer.write(output_stream)

    print(f"Finalized {output_path} ({page_count} pages).")


def main() -> None:
    """Parse command-line arguments and finalize the release PDF."""
    parser = argparse.ArgumentParser()
    parser.add_argument("input", type=Path, help="Raw PDF printed by Chromium.")
    parser.add_argument("output", type=Path, help="Final peer-review PDF.")
    arguments = parser.parse_args()

    finalize_pdf(arguments.input.resolve(), arguments.output.resolve())


if __name__ == "__main__":
    main()
