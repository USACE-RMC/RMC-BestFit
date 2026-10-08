"""Finalize the publication verification-report PDF."""

from __future__ import annotations

import argparse
from pathlib import Path

from pdf_report_common import finalize_report_pdf


def main() -> None:
    """Parse command-line arguments and finalize the verification report."""
    parser = argparse.ArgumentParser()
    parser.add_argument("input", type=Path, help="Raw PDF printed by Chromium.")
    parser.add_argument("output", type=Path, help="Final external-review PDF.")
    arguments = parser.parse_args()
    finalize_report_pdf(
        arguments.input.resolve(),
        arguments.output.resolve(),
        "verification_report",
        "verification",
        "Numerical verification and validation report for RMC.BestFit 2.0",
        "verification, validation, flood frequency, Bayesian inference, RMC.BestFit",
    )


if __name__ == "__main__":
    main()
