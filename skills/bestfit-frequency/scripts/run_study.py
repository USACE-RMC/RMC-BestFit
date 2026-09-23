"""Run documented stationary FFA candidates and preserve comparable results, failures and evidence."""
import argparse
import copy
import csv
import hashlib
import json
from pathlib import Path
import shutil

from prepare_study import prepare
from run_frequency import run_frequency, write_json


def read(path):
    """Read one saved UTF-8 JSON artifact."""
    return json.loads(Path(path).read_text(encoding="utf-8-sig"))


def apply_screening(source, screened):
    """Transfer API-derived flags only for the matching systematic cohort; never rerun MGBT on history."""
    result = copy.deepcopy(source)
    rows = {r["index"]: r for r in result["exactData"]}
    for row in screened["exactData"]:
        if row["index"] not in rows or rows[row["index"]]["value"] != row["value"]:
            raise ValueError("Screening cohort no longer matches candidate observations")
    flags = {r["index"]: r.get("isLowOutlier", False) for r in screened["exactData"]}
    for row in rows.values():
        row["isLowOutlier"] = flags.get(row["index"], row.get("isLowOutlier", False))
    result["useMultipleGrubbsBeckTest"] = False
    threshold = screened["inputData"].get("lowOutlierThreshold")
    if threshold is not None:
        result["lowOutlierThreshold"] = threshold
    return result


def compare(root, statuses):
    """Collect exact API curves/settings/diagnostics, retaining failed candidates without fabricated values."""
    records = []
    for status in statuses:
        record = dict(status)
        folder = Path(root) / record["name"]
        if (folder / "results.json").exists():
            results = read(folder / "results.json")
            if results.get("success") is not False:
                for key in ("frequencyCurve", "diagnostics", "parameterSummaries", "warnings", "bulletin17C", "informationCriteria"):
                    if key in results:
                        record[key] = results[key]
        if (folder / "analysis.json").exists():
            analysis = read(folder / "analysis.json")
            record["configuration"] = analysis.get("configuration")
            record["analysisWarnings"] = analysis.get("analysis", {}).get("warnings", [])
        records.append(record)
    return records


def save_comparison(output, records):
    """Write JSON and an aligned AEP table without interpolation, reweighting or model ranking."""
    write_json(output / "comparison.json", {"candidates": records,
        "interpretation": "Candidate comparison, not final engineering adoption. Inspect diagnostics, dependence and source applicability. Information criteria across different data are not directly comparable."})
    with (output / "comparison.csv").open("w", newline="", encoding="utf-8") as handle:
        writer = csv.writer(handle)
        writer.writerow(["candidate", "status", "aep", "mode", "mean", "lower", "upper", "intervalWidth"])
        for row in records:
            curve = row.get("frequencyCurve", {})
            probabilities = curve.get("probabilities", [])
            if not probabilities:
                writer.writerow([row["name"], row["status"], "", "", "", "", "", ""])
            for i, p in enumerate(probabilities):
                values = []
                for key in ("modeCurve", "meanCurve", "ciLower", "ciUpper"):
                    series = curve.get(key)
                    if series is not None and len(series) != len(probabilities):
                        raise ValueError(f"{row['name']}: {key} is not aligned with API probabilities")
                    values.append(series[i] if series is not None else None)
                writer.writerow([row["name"], row["status"], p, *values, curve.get("credibleIntervalWidth")])


def retain_sources(document, source_root, output):
    """Copy explicitly named evidence files inside the study folder and verify supplied checksums."""
    receipts = []
    for source in document.get("sources", []):
        if not source.get("artifact"):
            receipts.append({"id": source["id"], "retention": "citation-only; source bytes were not supplied"})
            continue
        root = Path(source_root).resolve()
        path = (root / source["artifact"]).resolve()
        if not path.is_relative_to(root) or not path.is_file():
            raise ValueError("Source artifact must be a file inside the study folder")
        digest = hashlib.sha256(path.read_bytes()).hexdigest()
        if source.get("sha256") and source["sha256"].lower() != digest:
            raise ValueError(f"Source checksum mismatch: {source['id']}")
        destination = output / "sources" / f"{len(receipts):03d}-{path.name}"
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(path, destination)
        receipts.append({"id": source["id"], "artifact": str(destination.relative_to(output)), "sha256": digest})
    write_json(output / "source-retention.json", receipts)


def execute(document, output, base="http://127.0.0.1:5210", source_root=".", prepare_only=False, timeout=1800):
    """Prepare all candidates, render each input before fitting, and retain independent failures."""
    prepared = prepare(document)
    output = Path(output)
    output.mkdir(parents=True, exist_ok=False)
    write_json(output / "study.json", document)
    write_json(output / "prepared.json", prepared)
    retain_sources(document, source_root, output)
    definition = prepared["study"]
    ylabel = f"{definition['flowDefinition']} ({definition['units']})"
    label = "Water year (ending year)" if definition["yearConvention"] == "waterYear" else "Calendar year"
    zoom = definition.get("systematicWindow")
    cohorts, statuses = {}, []
    for scenario in prepared["scenarios"]:
        name, kind = scenario["name"], scenario["kind"]
        status = {"name": name, "kind": kind, "input": scenario["input"], "rationale": scenario["rationale"],
                  "evidenceIds": scenario["evidenceIds"], "dependenceAssessment": scenario.get("dependenceAssessment")}
        try:
            source = copy.deepcopy(prepared["inputs"][scenario["input"]])
            cohort_name = scenario.get("screeningInput")
            if cohort_name:
                if cohort_name not in cohorts:
                    cohort = prepared["inputs"][cohort_name]
                    folder = output / "screening" / cohort_name
                    run_frequency(base, kind, cohort, "manual", {}, "on", folder, timeout,
                                  prepare_only=True, ylabel=ylabel, index_label=label, zoom=zoom)
                    cohorts[cohort_name] = read(folder / "input.json")
                source = apply_screening(source, cohorts[cohort_name])
                status["screeningSource"] = cohorts[cohort_name]["inputData"]["id"]
            folder = output / name
            run_frequency(base, kind, source, "manual", scenario["options"], "auto", folder, timeout,
                          prepare_only=prepare_only, ylabel=ylabel, index_label=label, zoom=zoom)
            status["status"] = "prepared" if prepare_only else "completed"
            if not prepare_only:
                from plot_frequency import build_figure, save_figure, plt
                fig, _, notes = build_figure(read(folder / "results.json"), read(folder / "input.json"), title=name, ylabel=ylabel)
                try:
                    save_figure(fig, folder / "frequency")
                finally:
                    plt.close(fig)
                write_json(folder / "frequency-display-notes.json", notes)
        except (ValueError, KeyError, TypeError, RuntimeError, OSError) as error:
            status.update(status="failed", error=str(error))
        statuses.append(status)
        save_comparison(output, compare(output, statuses))
    checksums = {str(path.relative_to(output)): hashlib.sha256(path.read_bytes()).hexdigest()
                 for path in sorted(output.rglob("*")) if path.is_file()}
    write_json(output / "sha256.json", checksums)
    return statuses


def main():
    """Execute a documented study in a fresh folder; no profile installation or remote hosting."""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--study", type=Path, required=True, help="Original evidence-bearing study.json")
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--base-url", default="http://127.0.0.1:5210")
    parser.add_argument("--prepare-only", action="store_true", help="Create inputs and chronology without estimating")
    parser.add_argument("--timeout", type=float, default=1800)
    args = parser.parse_args()
    try:
        statuses = execute(read(args.study), args.output, args.base_url, args.study.parent, args.prepare_only, args.timeout)
        print(args.output.resolve())
        if any(s["status"] == "failed" for s in statuses):
            parser.exit(1, "One or more candidates failed; inspect comparison.json and retained per-step responses.\n")
    except (ValueError, KeyError, TypeError, RuntimeError, OSError) as error:
        parser.exit(1, f"Study workflow stopped: {error}\n")


if __name__ == "__main__":
    main()
