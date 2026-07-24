"""Apply deterministic metadata and page furniture to the verification-report PDF."""

from __future__ import annotations

import argparse
from io import BytesIO
from pathlib import Path

from pypdf import PdfReader, PdfWriter
from reportlab.lib.colors import HexColor
from reportlab.pdfgen import canvas


def create_cover(width: float, height: float) -> BytesIO:
    """Create the deterministic full-page verification-report cover."""
    packet = BytesIO()
    cover = canvas.Canvas(packet, pagesize=(width, height), pageCompression=1)
    dark = HexColor("#10283c")
    light = HexColor("#2c779f")
    accent = HexColor("#84c6e6")
    pale = HexColor("#d9edf7")

    if hasattr(cover, "linearGradient"):
        cover.linearGradient(0, height, width, 0, [dark, light], extend=True)
    else:
        cover.setFillColor(dark)
        cover.rect(0, 0, width, height, stroke=0, fill=1)

    cover.setStrokeColorRGB(1, 1, 1, alpha=0.13)
    cover.setLineWidth(14)
    cover.circle(width + 10, 70, 160, stroke=1, fill=0)

    cover.setFillColor(accent)
    cover.rect(50, height - 67, 42, 6, stroke=0, fill=1)

    cover.setFillColorRGB(1, 1, 1)
    cover.setFont("Helvetica-Bold", 28)
    cover.drawString(50, height - 135, "RMC.BestFit 2.0")
    cover.drawString(50, height - 170, "Verification Report")

    cover.setStrokeColor(accent)
    cover.setLineWidth(0.8)
    cover.line(50, height - 184, width - 90, height - 184)

    cover.setFillColor(pale)
    cover.setFont("Helvetica", 12.5)
    cover.drawString(50, height - 215, "Numerical verification, independent package parity,")
    cover.drawString(50, height - 234, "published benchmarks, and scientific finding traceability")

    cover.setFillColor(HexColor("#b9dbe9"))
    cover.setFont("Helvetica-Bold", 8.5)
    cover.drawString(50, height - 290, "RISK MANAGEMENT CENTER")

    cover.setFillColor(HexColor("#e5f2f8"))
    cover.setFont("Helvetica", 9.5)
    cover.drawString(50, 86, "Verification program draft")
    cover.drawString(50, 70, "RMC.Numerics 2.1.4 dependency")
    cover.drawString(50, 54, "24 July 2026")

    cover.save()
    packet.seek(0)
    return packet

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
    overlay.drawString(50, height - 26, "RMC.BestFit 2.0 Verification Report")

    overlay.setFillColor(muted)
    overlay.setFont("Helvetica", 7.2)
    overlay.drawRightString(width - 50, height - 26, "Verification program draft")
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
        width = float(page.mediabox.width)
        height = float(page.mediabox.height)
        if index == 0:
            cover_reader = PdfReader(create_cover(width, height))
            writer.add_page(cover_reader.pages[0])
            continue

        overlay_reader = PdfReader(create_overlay(width, height, index + 1, page_count))
        page.merge_page(overlay_reader.pages[0], over=True)
        writer.add_page(page)

    writer.add_metadata(
        {
            "/Title": "RMC.BestFit 2.0 Verification Report",
            "/Author": "USACE Risk Management Center",
            "/Subject": "Formal numerical verification and validation report for RMC.BestFit 2.0",
            "/Keywords": "verification, validation, flood frequency, Bayesian inference, RMC.BestFit",
            "/Creator": "RMC.BestFit reproducible verification-report build",
            "/Producer": "Chromium, pypdf, and ReportLab",
            "/CreationDate": "D:20260724000000+00'00'",
            "/ModDate": "D:20260724000000+00'00'",
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
