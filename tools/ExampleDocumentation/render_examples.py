"""Regenerate tutorial PNG/SVG figures from saved desktop plot coordinates.

Build tools/PlotReferenceExporter first. The manifest identifies each saved
element and view. Every export opens a disposable project copy; saved analyses
are not rerun. Threshold diagnostic views call the app's diagnostic fits.
Python uses the skill's maintained renderer. The source hash is checked
before and after export, and exact display inputs accompany each figure.
"""
from __future__ import annotations

import argparse
import gzip
import hashlib
import json
from pathlib import Path
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "skills/bestfit-frequency"))
from bestfit_plots.adapters.desktop import desktop_plot
from bestfit_plots.render import export_plot


def file_hash(path):
    """Return the exact source or figure SHA-256."""
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def render_entry(entry, exporter, cache, refresh=False):
    """Export one source view and render it using the shared Python package."""
    source = (ROOT / entry["project"]).resolve()
    output = (ROOT / entry["output"]).resolve()
    if not source.is_relative_to(ROOT / "examples") or not output.is_relative_to(ROOT / "examples"):
        raise ValueError("Figure source and output must be within examples")
    before = file_hash(source)
    prefix = cache / entry["id"]
    prefix.parent.mkdir(parents=True, exist_ok=True)
    snapshot_path = prefix.with_suffix(".json")
    saved = json.loads(snapshot_path.read_text(encoding="utf-8")) if snapshot_path.exists() else None
    identity = {"sourceSha256": before, "element": entry["element"], "plotId": entry["plotId"], "variant": entry["variant"]}
    if refresh or saved is None or any(saved.get(k) != value for k, value in identity.items()):
        command = [str(exporter), "--project", str(source), "--element", entry["element"],
                   "--plot-id", entry["plotId"], "--variant", entry["variant"], "--output", str(prefix)]
        result = subprocess.run(command, capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=180)
        if file_hash(source) != before:
            raise RuntimeError(f"Source file changed during export: {source}")
        if result.returncode:
            raise RuntimeError(result.stderr.strip() or result.stdout.strip())
        saved = json.loads(snapshot_path.read_text(encoding="utf-8"))
    if any(saved.get(k) != value for k, value in identity.items()):
        raise ValueError("Desktop export does not match the requested source and view")
    saved["project"] = entry["project"]
    spec = desktop_plot(saved)
    if not spec["series"]:
        raise ValueError("A tutorial figure cannot be an empty plot")
    paths = export_plot(spec, output)
    spec_path = Path(str(output) + ".plotspec.json.gz")
    spec_path.write_bytes(gzip.compress(json.dumps(spec, ensure_ascii=False, allow_nan=False).encode("utf-8"), mtime=0))
    paths.append(spec_path)
    return {"id": entry["id"], **identity, "project": entry["project"], "runtime": saved["runtime"],
            "artifacts": {p.relative_to(ROOT).as_posix(): file_hash(p) for p in paths},
            "displayCorrections": spec.get("displayCorrections", []), "omissions": spec["omissions"]}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--manifest", type=Path, default=ROOT / "examples/figure-manifest.json")
    parser.add_argument("--only", help="Render figure IDs or project paths containing this text")
    parser.add_argument("--refresh", action="store_true", help="Re-export even if source hashes match")
    parser.add_argument("--cache", type=Path, default=ROOT / "artifacts/example-documentation/desktop")
    parser.add_argument("--exporter", type=Path, default=ROOT / "tools/PlotReferenceExporter/bin/Debug/net10.0-windows/PlotReferenceExporter.exe")
    args = parser.parse_args()
    entries = json.loads(args.manifest.read_text(encoding="utf-8"))
    results, failures = [], []
    for entry in entries:
        if args.only and args.only not in entry["id"] and args.only not in entry["project"]:
            continue
        try:
            receipt = render_entry(entry, args.exporter, args.cache, args.refresh)
            results.append(receipt)
            print(f"Rendered {entry['id']}", flush=True)
        except (OSError, ValueError, RuntimeError, subprocess.TimeoutExpired) as error:
            failures.append({"id": entry["id"], "error": str(error)})
            print(f"FAILED {entry['id']}: {error}", flush=True)
    receipt_path = ROOT / "artifacts/example-documentation" / ("render-" + (args.only or "all").replace("/", "-") + ".json")
    receipt_path.write_text(json.dumps({"rendered": results, "failures": failures}, indent=2), encoding="utf-8")
    if failures or not results:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
