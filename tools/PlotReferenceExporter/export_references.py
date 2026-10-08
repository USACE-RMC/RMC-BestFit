"""Run the saved-project app plot exporter, recording successes and real failures.

Each CLI invocation has its own WPF process and disposable copy of the source
project. Only nonempty model geometry is retained in validation; PNG/SVG files
stay under output for visual inspection.
"""

from __future__ import annotations

import argparse
import json
import shutil
import subprocess
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
EXE = ROOT / "tools/PlotReferenceExporter/bin/Debug/net10.0-windows/PlotReferenceExporter.exe"
MANIFEST = ROOT / "skills/bestfit-frequency/references/app-plot-map.json"
REFERENCE = ROOT / "validation/plot-parity/app-reference"
GALLERY = ROOT / "output/plot-reference-gallery"


def project(name: str) -> Path:
    matches = list((ROOT / "examples").rglob(name + ".bestfit"))
    if len(matches) != 1:
        raise ValueError(f"Expected one saved project named {name}: {matches}")
    return matches[0]


CASES: dict[str, tuple[str, str]] = {
    "time_series_data": ("manual-entry-example", "Airline Passengers"),
    "input_data": ("usgs-block-max-example", "USGS - 01134500 - Block Max - Calendar Year"),
    "fitting": ("viglione-et-al-2013", "Fit - Systematic (1951-2001)"),
    "univariate": ("viglione-et-al-2013", "MCMC - Systematic (1951-2001)"),
    "b17c": ("bulletin-17c-examples", "Example #1"),
    "point_process": ("point-process-examples", "USC00040741 - Point Process"),
    "mixture": ("mixture-distribution-examples", "Mixture Distribution - 2 Normals"),
    "composite": ("mixed-population-examples", "Competing Flood Types"),
    "bivariate": ("bivariate-distribution-examples", "Normal Copula"),
    "coincident": ("sum-two-normals", "CFA - Rho = 0.0"),
    "rating": ("usgs-07024175-mississippi-rating-curve", "USGS 07024175 Rating Curve"),
    "time_series_analysis": ("classic-time-series-examples", "Airline Passengers - TSA"),
    "shared_diagnostics": ("viglione-et-al-2013", "MCMC - Systematic (1951-2001)"),
}


def case_for(plot_id: str, variant: str) -> tuple[Path, str]:
    family = plot_id.split(".")[0]
    source, element = CASES[family]
    if plot_id.startswith("input_data.mean_") or plot_id in {
        "input_data.modified_scale", "input_data.shape"
    }:
        source, element = "point-process-examples", "USC00040741 - POT"
    if plot_id == "input_data.chronology" and variant == "water_year":
        element = "USGS - 01134500 - Block Max - Water Year"
    if plot_id == "input_data.frequency":
        if variant == "uncertain":
            source, element = "sinnemahoning-move3-bayesian", "Sinnemahoning - MOVE.3 - With Errors"
        elif variant in {"exact", "interval", "low_outlier"}:
            source = "bulletin-17c-examples"
            element = {
                "exact": "Example #1 - Data",
                "interval": "Example #4 - Data",
                "low_outlier": "Example #2 - Data",
            }[variant]
    if plot_id in {"univariate.frequency", "univariate.chronology"} and variant == "nonstationary":
        source, element = "nsffa-brays-bayou-texas", "NSFFA - Linear"
    if plot_id == "univariate.frequency" and variant == "quantile_prior":
        element = "MCMC - Systematic (1951-2001) + 3 Quantile Priors"
    if plot_id == "point_process.frequency":
        element = {
            "seasonal": "USC00040741 - Seasonal Point Process",
        }.get(variant, element)
    if plot_id == "mixture.frequency" and variant == "zero_inflated":
        element = "Mixture Distribution - 2 Normals - Zero-Inflated"
    if plot_id == "b17c.frequency":
        element = {
            "bcb": "Example #1 - BCB",
            "historical_interval": "Example #4",
            "low_outlier": "Example #2",
        }.get(variant, element)
    if plot_id == "shared_diagnostics.influence" and variant.startswith("gmm_"):
        source, element = "bulletin-17c-examples", "Example #1"
    if plot_id == "rating.curve" and variant == "segmented":
        source, element = "synthetic-rating-curve-examples", "2 Segment Rating Curve"
    return project(source), element


def nonempty(geometry: dict) -> bool:
    return any(
        series.get("points") or series.get("points2") or series.get("items") or series.get("actualItems")
        or (series.get("grid") or {}).get("data") or series.get("contours")
        for series in geometry["series"]
    )


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--only", help="Plot ID prefix for a targeted reference refresh")
    args = parser.parse_args()
    slots = json.loads(MANIFEST.read_text(encoding="utf-8"))["slots"]
    order = {(slot["plotId"], variant): rank for rank, (slot, variant) in enumerate(
        (pair for slot in slots for pair in ((slot, variant) for variant in slot["variants"]))) }
    REFERENCE.mkdir(parents=True, exist_ok=True)
    GALLERY.mkdir(parents=True, exist_ok=True)
    index_path = REFERENCE / "index.json"
    results = json.loads(index_path.read_text(encoding="utf-8")) if args.only and index_path.exists() else []
    for slot in slots:
        plot_id = slot["plotId"]
        if args.only and not plot_id.startswith(args.only):
            continue
        for variant in slot["variants"]:
            source, element = case_for(plot_id, variant)
            stem = f"{plot_id}--{variant}"
            output = GALLERY / stem
            command = [
                str(EXE), "--project", str(source), "--element", element,
                "--plot-id", plot_id, "--variant", variant,
                "--output", str(output),
            ]
            try:
                process = subprocess.run(command, capture_output=True, text=True, timeout=120)
                if process.returncode:
                    status = "unsupported"
                    detail = process.stderr.splitlines()[0] if process.stderr else "Nonzero exit"
                else:
                    geometry_file = Path(str(output) + ".json")
                    geometry = json.loads(geometry_file.read_text(encoding="utf-8"))
                    status = "exported" if nonempty(geometry) else "empty"
                    detail = f"{len(geometry['series'])} model series"
                    if plot_id == "univariate.chronology" and variant == "stationary" and status == "empty":
                        status = "app_conditional_empty"
                        detail = "App UpdateChronologyPlot populates only IsNonstationary != false analyses"
                    if status == "exported":
                        shutil.copyfile(geometry_file, REFERENCE / f"{stem}.json")
            except subprocess.TimeoutExpired:
                status, detail = "timeout", "App export exceeded 120 seconds"
            results = [record for record in results
                       if (record["plotId"], record["variant"]) != (plot_id, variant)]
            results.append({
                "plotId": plot_id, "variant": variant, "source": str(source.relative_to(ROOT)),
                "element": element, "status": status, "detail": detail,
            })
            results.sort(key=lambda record: order[(record["plotId"], record["variant"])])
            print(f"{stem}: {status} ({detail})", flush=True)
            index_path.write_text(json.dumps(results, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
