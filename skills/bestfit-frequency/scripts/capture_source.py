"""Create location-specific FFA research queries and retain exact public-source/service responses.

Discover service URLs, region IDs, scenarios, predictor names and units from current
official documentation. This helper does not guess them or certify applicability.
"""
import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
from urllib.error import HTTPError, URLError
from urllib.parse import urlparse
from urllib.request import Request, urlopen

PRIMARY = [
    "https://pubs.usgs.gov/publication/tm4B5",
    "https://www.usgs.gov/streamstats/science/flood-frequency-reports",
    "https://www.usgs.gov/streamstats/science/national-streamflow-statistics-nss",
    "https://www.usgs.gov/streamstats/web-services",
    "https://water.noaa.gov/about/api",
    "https://www.usgs.gov/tools/flood-event-viewer",
]


def research_plan(definition):
    """Build source-first searches using the supplied site identity, without geocoding guesses."""
    location = definition.get("location")
    if not location:
        raise ValueError("study.location is required")
    site = str(definition.get("siteNumber", ""))
    return {"study": definition, "primaryResources": PRIMARY, "queries": [
        f'site:usgs.gov "{location}" "{site}" regional skew mean square error',
        f'site:pubs.usgs.gov "{location}" flood frequency regression prediction uncertainty',
        f'site:pubs.usgs.gov "{location}" "{site}" historical flood perception threshold',
        f'site:weather.gov "{location}" flood report historical crest',
        f'"{location}" historic flood high water mark gage datum regulation'],
        "requiredChecks": ["Confirm outlet, watershed, gage identity and coordinates before selecting a region.",
            "Read Bulletin 17C data representation, appendixes 3 and 10; do not copy PeakFQ sentinel values into BestFit.",
            "Verify report revision, geographic/predictor/regulation applicability and uncertainty meaning.",
            "A missing service or missing uncertainty is unresolved, not zero."]}


def capture(url, output, body=None, timeout=60):
    """Save one read-only research response, request and checksum, including HTTP failures."""
    parsed = urlparse(url)
    if parsed.scheme != "https" or not parsed.hostname or parsed.username or parsed.password:
        raise ValueError("Use a public HTTPS research URL without credentials")
    output = Path(output)
    output.mkdir(parents=True, exist_ok=False)
    record = {"requestedUrl": url, "retrievedUtc": datetime.now(timezone.utc).isoformat(),
              "method": "GET" if body is None else "POST", "request": body,
              "purpose": "Source capture only; applicability and uncertainty require review", "success": False}
    (output / "request.json").write_text(json.dumps(record, indent=2, allow_nan=False) + "\n", encoding="utf-8")
    data = None if body is None else json.dumps(body, allow_nan=False).encode("utf-8")
    request = Request(url, data=data, headers={"User-Agent": "BestFit-FFA-source-capture/1.0", "Content-Type": "application/json"})
    raw = None
    try:
        with urlopen(request, timeout=timeout) as response:
            raw = response.read()
            record.update(success=True, httpStatus=response.status, finalUrl=response.geturl(), contentType=response.headers.get("Content-Type"))
    except HTTPError as error:
        raw = error.read()
        record.update(httpStatus=error.code, error=str(error))
    except (URLError, OSError) as error:
        record["error"] = str(error)
    if raw is not None:
        (output / "response.bin").write_bytes(raw)
        record["sha256"] = hashlib.sha256(raw).hexdigest()
        record["byteLength"] = len(raw)
        try:
            parsed_body = json.loads(raw)
            (output / "response.json").write_text(json.dumps(parsed_body, indent=2, allow_nan=False) + "\n", encoding="utf-8")
        except (ValueError, UnicodeError):
            pass  # The authoritative bytes can be a PDF, HTML, RDB or a non-JSON error body.
    (output / "receipt.json").write_text(json.dumps(record, indent=2, allow_nan=False) + "\n", encoding="utf-8")
    return record


def main():
    """Plan searches or capture a discovered source/service call into a new directory."""
    parser = argparse.ArgumentParser(description=__doc__)
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument("--study", type=Path, help="Write a research query plan from study.study (no network)")
    mode.add_argument("--url", help="Exact source or read-only NSS/StreamStats calculation URL discovered from official metadata")
    parser.add_argument("--body", type=Path, help="JSON body for a documented read-only calculation POST")
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    try:
        if args.study:
            if args.body:
                raise ValueError("--body is only valid with --url")
            definition = json.loads(args.study.read_text(encoding="utf-8-sig"))["study"]
            args.output.mkdir(parents=True, exist_ok=False)
            (args.output / "research-plan.json").write_text(json.dumps(research_plan(definition), indent=2) + "\n", encoding="utf-8")
        else:
            body = json.loads(args.body.read_text(encoding="utf-8-sig")) if args.body else None
            record = capture(args.url, args.output, body)
            if not record["success"]:
                parser.exit(1, f"Source unavailable; retained evidence at {args.output}\n")
        print(args.output.resolve())
    except (ValueError, KeyError, TypeError, OSError) as error:
        parser.exit(1, f"Source capture stopped: {error}\n")


if __name__ == "__main__":
    main()
