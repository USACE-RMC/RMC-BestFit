"""Verify or recapture the native example views without changing saved sources.

The manifest pins source and output hashes. --refresh runs the native exporter on
disposable copies and updates output receipts; it refuses changed source projects.
Existing POT threshold diagnostics perform their usual display-only GPD fits.
"""
import argparse
from concurrent.futures import ThreadPoolExecutor
import gzip
import hashlib
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[2]
MANIFEST = ROOT / "examples/screenshot-manifest.json"
EXPORTER = ROOT / "tools/PlotReferenceExporter/bin/Debug/net10.0-windows/PlotReferenceExporter.exe"


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def local(value):
    path = (ROOT / value).resolve()
    if not path.is_relative_to(ROOT):
        raise ValueError(f"Path outside repository: {value}")
    return path


def validate_snapshot(entry, snapshot):
    """Require the receipt to identify the requested source and displayed view."""
    if any(snapshot.get(key) != entry["sourceSha256"] for key in ("sourceSha256", "sourceSha256After")):
        raise ValueError("Capture source preservation mismatch: " + entry["prPath"])
    if entry["disposition"] == "ui-capture":
        if snapshot.get("selectedElement") != entry["element"] or snapshot.get("panel") != entry["panel"]:
            raise ValueError("Capture panel identity mismatch: " + entry["prPath"])
        if entry["panel"] == "dss-selector" and snapshot.get("configuration", {}).get("dssSelectedPath", "").lower() != entry["pathname"].lower():
            raise ValueError("Capture DSS selection mismatch: " + entry["prPath"])
    else:
        if any(snapshot.get(key) != entry[key] for key in ("element", "plotId", "variant")):
            raise ValueError("Capture plot identity mismatch: " + entry["prPath"])
        for key in ("parameterIndex", "alternativeElement"):
            if key in entry and snapshot.get("displaySelection", {}).get(key) != entry[key]:
                raise ValueError("Capture display selection mismatch: " + entry["prPath"])
        if "timeIndex" in entry and snapshot.get("savedDisplaySettings", {}).get("distribution", {}).get("ParameterTimeIndex") != entry["timeIndex"]:
            raise ValueError("Capture evaluation index mismatch: " + entry["prPath"])
        if not any(s.get("points") or s.get("points2") or s.get("actualItems") or s.get("grid") for s in snapshot.get("series", [])):
            raise ValueError("Empty native plot: " + entry["prPath"])


def capture(entry, exporter, refresh):
    if entry["disposition"] == "omit":
        return entry
    source = local(entry["project"])
    if digest(source) != entry["sourceSha256"]:
        raise ValueError(f"Source changed: {entry['project']}; review the new scientific state first")
    output = local(entry["output"])
    if not refresh:
        expected = {".png", ".json.gz"} | ({".svg"} if entry["disposition"] == "native-export" else set())
        recorded = {a["path"].removeprefix(entry["output"]) for a in entry.get("outputs", [])}
        if recorded != expected or len(entry.get("outputs", [])) != len(expected) or "evidence" not in entry:
            raise ValueError(f"Incomplete capture receipt: {entry['prPath']}")
        evidence = entry["evidence"]
        if evidence.get("snapshot") != entry["output"] + ".json.gz" or any(
            evidence.get(key) != entry["sourceSha256"] for key in ("sourceSha256Before", "sourceSha256After")
        ):
            raise ValueError(f"Capture evidence mismatch: {entry['prPath']}")
        for artifact in entry["outputs"]:
            if digest(local(artifact["path"])) != artifact["sha256"]:
                raise ValueError(f"Output changed: {artifact['path']}")
        snapshot = json.loads(gzip.decompress(output.with_suffix(".json.gz").read_bytes()))
        validate_snapshot(entry, snapshot)
        return entry
    args = [str(exporter), "--project", str(source), "--element", entry["element"], "--output", str(output)]
    if entry["disposition"] == "ui-capture":
        args += ["--panel", entry["panel"], "--control-type", entry["controlType"]]
        if entry.get("dssFile"):
            args += ["--dss-file", str(local(entry["dssFile"])), "--dss-path", entry["pathname"]]
    else:
        args += ["--plot-id", entry["plotId"], "--variant", entry["variant"]]
        for field, option in [("parameterIndex", "parameter-index"), ("timeIndex", "time-index"),
                              ("alternativeElement", "alternative-element")]:
            if field in entry:
                args += ["--" + option, str(entry[field])]
    try:
        result = subprocess.run(args, cwd=ROOT, capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=600)
    finally:
        if digest(source) != entry["sourceSha256"]:
            raise ValueError(f"Source changed during export: {entry['project']}")
    if result.returncode:
        raise RuntimeError(f"{entry['prPath']}\n{result.stdout}\n{result.stderr}")
    receipt = output.with_suffix(".json")
    snapshot = json.loads(receipt.read_text(encoding="utf-8"))
    validate_snapshot(entry, snapshot)
    snapshot_path = output.with_suffix(".json.gz")
    snapshot_path.write_bytes(gzip.compress(receipt.read_bytes(), mtime=0))
    receipt.unlink()
    paths = [output.with_suffix(".png"), snapshot_path]
    if entry["disposition"] == "native-export":
        paths.insert(1, output.with_suffix(".svg"))
    entry["outputs"] = [{"path": p.relative_to(ROOT).as_posix(), "sha256": digest(p)} for p in paths]
    entry["evidence"] = {
        "kind": "recaptured-current-project",
        "snapshot": snapshot_path.relative_to(ROOT).as_posix(),
        "sourceSha256Before": entry["sourceSha256"], "sourceSha256After": digest(source),
        "savedEstimatorsRerun": False,
        "displayDiagnosticFits": entry.get("plotId") in {
            "input_data.mean_excess", "input_data.mean_residual_life", "input_data.modified_scale", "input_data.shape"},
    }
    print("captured " + entry["output"], flush=True)
    return entry


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--only", default="")
    parser.add_argument("--refresh", action="store_true")
    parser.add_argument("--kind", choices=["native-export", "ui-capture"])
    parser.add_argument("--jobs", type=int, default=2)
    parser.add_argument("--exporter", type=Path, default=EXPORTER)
    args = parser.parse_args()
    manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
    assets = manifest["assets"]
    selected = [e for e in assets if any(args.only in e.get(key, "") for key in ("prPath", "project", "page", "output"))
                and (not args.kind or e["disposition"] == args.kind)]
    if not selected:
        raise ValueError("No matching screenshots")
    generated = {e["output"] for e in json.loads((ROOT / "examples/figure-manifest.json").read_text())}
    outputs = [e["output"] for e in assets if e["disposition"] != "omit"]
    if len(outputs) != len(set(outputs)) or generated.intersection(outputs):
        raise ValueError("Screenshot destinations collide with other figures")
    for item in manifest["protectedFiles"]:
        if digest(local(item["path"])) != item["sha256"]:
            raise ValueError("Protected source changed: " + item["path"])
    failures = []
    with ThreadPoolExecutor(max_workers=max(1, min(args.jobs, 4))) as pool:
        futures = [(e, pool.submit(capture, e, args.exporter.resolve(), args.refresh)) for e in selected]
        for entry, future in futures:
            try:
                future.result()
            except Exception as error:
                failures.append(str(error))
    if args.refresh:
        MANIFEST.write_text(json.dumps(manifest, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    for item in manifest["protectedFiles"]:
        if digest(local(item["path"])) != item["sha256"]:
            failures.append("Protected source changed: " + item["path"])
    print(json.dumps({"selected": len(selected), "protectedFiles": len(manifest["protectedFiles"]), "failures": failures}, indent=2))
    if failures:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
