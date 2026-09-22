"""Run a local BestFit frequency analysis and retain requests, inputs, results and defaults."""
import argparse
import copy
from datetime import datetime, timezone
import importlib.metadata
import json
from pathlib import Path
import platform
import sys
from urllib.error import HTTPError
from urllib.parse import urlparse
from urllib.request import Request, urlopen


def input_request(source, kind, mgbt):
    """Apply the skill's B17C screening convention while preserving explicit manual choices."""
    body = copy.deepcopy(source)
    manual = body.get("lowOutlierThreshold") is not None or any(r.get("isLowOutlier") for r in body.get("exactData", []))
    if mgbt != "auto":
        body["useMultipleGrubbsBeckTest"] = mgbt == "on"
    elif kind == "bulletin17c" and "useMultipleGrubbsBeckTest" not in body and not manual:
        body["useMultipleGrubbsBeckTest"] = True
    return body


def request_json(base, path, body=None, timeout=1800):
    """Send one request; retain JSON error bodies for the caller to save and check."""
    data = None if body is None else json.dumps(body, allow_nan=False).encode("utf-8")
    request = Request(base.rstrip("/") + path, data=data, headers={"Content-Type": "application/json"})
    try:
        with urlopen(request, timeout=timeout) as response:
            return json.load(response)
    except HTTPError as error:
        raw = error.read().decode("utf-8", errors="replace")
        try:
            payload = json.loads(raw)
        except ValueError:
            payload = {"errorMessage": raw}
        if not isinstance(payload, dict):
            payload = {"errorMessage": raw}
        payload["success"] = False
        payload["httpStatus"] = error.code
        return payload


def write_json(path, payload):
    """Save an artifact as UTF-8 JSON; API named floating-point strings remain strings."""
    Path(path).write_text(json.dumps(payload, indent=2, ensure_ascii=False, allow_nan=False) + "\n", encoding="utf-8")


def checked(payload, path):
    """Stop on application failures even when the HTTP status was 200."""
    if not isinstance(payload, dict) or payload.get("success") is False or payload.get("isValid") is False:
        raise RuntimeError(f"API step failed; inspect {path}: {payload}")
    return payload


def run_frequency(base, kind, source, source_kind, options, mgbt, output, timeout=1800,
                  *, prepare_only=False, ylabel="Value (units not supplied)", index_label="Year / declared index", zoom=None):
    """Create, validate and run an analysis, saving every request and response in a new folder."""
    parsed = urlparse(base)
    if parsed.scheme != "http" or parsed.hostname not in ("localhost", "127.0.0.1", "::1") or parsed.username or parsed.password:
        raise ValueError("Use this skill with an HTTP loopback API URL")
    if kind not in ("bulletin17c", "univariate") or source_kind not in ("manual", "usgs-peaks"):
        raise ValueError("Unsupported analysis or input kind")
    if "inputDataId" in options:
        raise ValueError("analysis-options must omit inputDataId; the new input is linked automatically")
    if source.get("lowOutlierThreshold") is not None and any("isLowOutlier" not in row for row in source.get("exactData", [])):
        raise ValueError("Manual lowOutlierThreshold does not derive flags: obtain explicit isLowOutlier flags for every exact observation")
    output = Path(output)
    output.mkdir(parents=True, exist_ok=False)
    versions = {"python": sys.version, "platform": platform.platform(), "baseUrl": base,
                "startedUtc": datetime.now(timezone.utc).isoformat(), "packages": {}}
    for package in ("matplotlib", "numpy"):
        try:
            versions["packages"][package] = importlib.metadata.version(package)
        except importlib.metadata.PackageNotFoundError:
            versions["packages"][package] = "not installed"
    write_json(output / "client-version.json", versions)

    def step(path, name, body=None):
        """Persist a response before checking whether the operation succeeded."""
        artifact = output / name
        try:
            payload = request_json(base, path, body, timeout)
        except OSError as error:
            write_json(artifact, {"success": False, "errorMessage": str(error), "path": path})
            raise
        write_json(artifact, payload)
        return checked(payload, artifact)

    step("/api/info", "api-info.json")
    step("/api/metadata/defaults", "defaults.json")
    body = input_request(source, kind, mgbt)
    write_json(output / "input-request.json", body)
    resource = step(f"/api/inputdata/{source_kind}", "input-created.json", body)
    input_id = resource["inputData"]["id"]
    step(f"/api/inputdata/{input_id}?includeData=true", "input.json")
    step(f"/api/inputdata/{input_id}/source", "source.json")
    chronology = step(f"/api/inputdata/{input_id}/chronology", "chronology.json")
    from plot_chronology import render
    render(chronology, output / "chronology", ylabel=ylabel, index_label=index_label, zoom=zoom)
    if prepare_only:
        return output / "chronology.json"
    analysis_body = {**options, "inputDataId": input_id}
    write_json(output / "analysis-request.json", analysis_body)
    analysis = step(f"/api/analyses/{kind}", "analysis-created.json", analysis_body)
    analysis_id = analysis["analysis"]["id"]
    step(f"/api/analyses/{kind}/{analysis_id}/validate", "validation.json")
    try:
        step(f"/api/analyses/{kind}/{analysis_id}/run", "results.json", {})
    finally:
        # A failed run still has useful state/diagnostics. Do not mask its original error.
        try:
            step(f"/api/analyses/{kind}/{analysis_id}", "analysis.json")
        except (OSError, RuntimeError) as error:
            write_json(output / "state-capture-error.json", {"errorMessage": str(error)})
    return output / "results.json"


def main():
    """Parse CLI options and execute one workflow against an already-running local host."""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--base-url", default="http://127.0.0.1:5210")
    parser.add_argument("--kind", choices=("univariate", "bulletin17c"), default="univariate")
    source = parser.add_mutually_exclusive_group(required=True)
    source.add_argument("--manual", type=Path, help="Manual input request JSON, including exactData")
    source.add_argument("--usgs", help="USGS site number; treated as text to preserve leading zeros")
    parser.add_argument("--analysis-options", type=Path, help="Analysis request JSON without inputDataId")
    parser.add_argument("--mgbt", choices=("auto", "on", "off"), default="auto")
    parser.add_argument("--output", required=True, type=Path, help="New artifact directory; existing folders are rejected")
    parser.add_argument("--timeout", type=float, default=1800, help="Per-request wait in seconds, not an estimator budget")
    parser.add_argument("--prepare-only", action="store_true", help="Create input, preserve sources and render chronology without creating or running an analysis")
    parser.add_argument("--ylabel", default="Value (units not supplied)")
    parser.add_argument("--index-label", default="Year / declared index")
    parser.add_argument("--zoom", nargs=2, type=float, metavar=("START", "END"))
    args = parser.parse_args()
    try:
        source_body = json.loads(args.manual.read_text(encoding="utf-8-sig")) if args.manual else {"siteNumber": args.usgs}
        options = json.loads(args.analysis_options.read_text(encoding="utf-8-sig")) if args.analysis_options else {}
        path = run_frequency(args.base_url, args.kind, source_body, "manual" if args.manual else "usgs-peaks",
                             options, args.mgbt, args.output, args.timeout, prepare_only=args.prepare_only,
                             ylabel=args.ylabel, index_label=args.index_label, zoom=args.zoom)
        print(path.resolve())
    except (RuntimeError, ValueError, KeyError, TypeError, OSError) as error:
        parser.exit(1, f"Frequency workflow stopped: {error}\n")


if __name__ == "__main__":
    main()
