"""Audit saved example metadata without loading, migrating or saving a project.

Run from the repository root with ``python tools/ExampleDocumentation/project_inventory.py``.
The JSON inventory includes original metadata, decoded configuration, input counts,
saved diagnostic summaries and a digest of every SQLite cell. Result arrays and
source payloads remain in their original databases.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import math
from pathlib import Path
import sqlite3
import xml.etree.ElementTree as ET
import zlib

ROOT = Path(__file__).resolve().parents[2]


def quote(name):
    """Quote a SQLite identifier; values always use query parameters."""
    return '"' + name.replace('"', '""') + '"'


def digest(value):
    """Hash the storage type and exact cell payload, including binary results."""
    payload = value if isinstance(value, bytes) else repr(value).encode("utf-8")
    return hashlib.sha256(type(value).__name__.encode() + b":" + payload).hexdigest()


def read_project(path):
    """Return all cells using immutable SQLite access that cannot create sidecars."""
    with sqlite3.connect(Path(path).resolve().as_uri() + "?mode=ro&immutable=1", uri=True) as connection:
        if connection.execute("PRAGMA integrity_check").fetchone()[0] != "ok":
            raise ValueError(f"SQLite integrity check failed: {path}")
        connection.row_factory = sqlite3.Row
        tables = [row[0] for row in connection.execute("SELECT name FROM sqlite_master WHERE type='table' ORDER BY name")]
        return {table: [dict(row) for row in connection.execute(f"SELECT rowid AS _rowid_, * FROM {quote(table)} ORDER BY rowid")]
                for table in tables}


def cell_snapshot(tables):
    """Create stable cell receipts for an exact logical before/after audit."""
    return {table: {str(row["_rowid_"]): {column: digest(value) for column, value in row.items() if column != "_rowid_"}
                    for row in rows} for table, rows in tables.items()}


def decode(value):
    """Decode the raw-DEFLATE payload convention used by saved BestFit cells."""
    if isinstance(value, bytes):
        return zlib.decompress(value, -15).decode("utf-8-sig")
    return value


def compact_xml(node):
    """Keep configuration trees but summarize large numerical array attributes."""
    attrs = {key: value for key, value in node.attrib.items() if len(value) < 1500}
    arrays = {key: len(value.split("|")) for key, value in node.attrib.items() if len(value) >= 1500}
    result = {"tag": node.tag, "attributes": attrs}
    if arrays:
        result["arrayLengths"] = arrays
    if node.text and node.text.strip():
        result["text"] = node.text.strip()
    children = [compact_xml(child) for child in node]
    if children:
        result["children"] = children
    return result


def input_summary(node):
    """Summarize saved coverage without treating missing ordinates as zeros."""
    if node.tag == "TimeSeries":
        records = list(node)
        values = [float(row.get("Value", "nan")) for row in records]
        finite = [value for value in values if math.isfinite(value)]
        return {"interval": node.get("TimeInterval"), "count": len(records), "finiteCount": len(finite),
                "missingCount": len(values) - len(finite), "first": records[0].get("Index") if records else None,
                "last": records[-1].get("Index") if records else None,
                "minimum": min(finite) if finite else None, "maximum": max(finite) if finite else None}
    result = {"attributes": {key: value for key, value in node.attrib.items() if key != "USGSRawText"}, "series": {}}
    for group in node:
        rows = list(group)
        indices = [float(row.get("Index")) for row in rows if row.get("Index") is not None]
        info = {"count": len(rows), "firstIndex": min(indices) if indices else None,
                "lastIndex": max(indices) if indices else None}
        if group.tag in {"ThresholdSeries", "IntervalSeries", "UncertainSeries"}:
            info["records"] = [compact_xml(row) for row in rows]
        else:
            info["lowOutlierCount"] = sum(row.get("IsLowOutlier", "false").lower() == "true" for row in rows)
        result["series"][group.tag] = info
    return result


def summarize_row(row):
    """Extract actual configuration and saved results needed to write a tutorial."""
    output = {"rowid": row["_rowid_"], "name": row.get("Name"), "description": row.get("Description"),
              "metadata": {}, "configuration": {}, "inputs": {}, "results": {}}
    for key, value in row.items():
        if key in {"_rowid_", "Name", "Description"} or "Layout" in key or "PlotSettings" in key or key in {"USGSRawText", "USGSRawTextCompressed", "MCMCReport"}:
            continue
        if value is None or value == "":
            continue
        if key == "MCMCResults":
            try:
                samples = json.loads(decode(value))
            except (zlib.error, UnicodeError, json.JSONDecodeError):
                output["results"]["diagnosticsUnavailable"] = "Legacy saved payload requires the desktop compatibility loader."
                continue
            output["results"]["diagnostics"] = [item.get("SummaryStatistics") for item in samples.get("ParameterResults", [])]
            output["results"]["savedDraws"] = len(samples.get("Output") or [])
            output["results"]["savedChains"] = len(samples.get("MarkovChains") or [])
            continue
        if isinstance(value, bytes):
            try:
                value = decode(value)
            except (zlib.error, UnicodeError):
                output["metadata"][key] = {"bytes": len(value), "sha256": digest(value)}
                continue
        if isinstance(value, str) and value.lstrip().startswith("<"):
            node = ET.fromstring(value)
            if node.tag in {"TimeSeries", "DataFrame"}:
                output["inputs"][key] = input_summary(node)
            elif "Results" in key:
                output["results"][key] = compact_xml(node)
            else:
                output["configuration"][key] = compact_xml(node)
        else:
            output["metadata"][key] = value
    return output


def inventory(path):
    """Return a source-bound inventory plus per-cell preservation receipts."""
    path = Path(path).resolve()
    tables = read_project(path)
    return {"project": path.relative_to(ROOT).as_posix(), "sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
            "tables": {table: [summarize_row(row) for row in rows] for table, rows in tables.items()},
            "cellDigests": cell_snapshot(tables)}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=ROOT / "artifacts/example-documentation/inventory")
    parser.add_argument("--project", type=Path)
    args = parser.parse_args()
    paths = [args.project] if args.project else sorted((ROOT / "examples").rglob("*.bestfit"))
    args.output.mkdir(parents=True, exist_ok=True)
    manifest = []
    for path in paths:
        result = inventory(path)
        destination = args.output / (path.stem + ".json")
        destination.write_text(json.dumps(result, indent=2, ensure_ascii=False, allow_nan=False), encoding="utf-8")
        count = sum(len(rows) for rows in result["tables"].values())
        manifest.append({"project": result["project"], "sha256": result["sha256"], "rows": count, "inventory": destination.name})
        print(f"{path.stem}: {count} rows")
    (args.output / "index.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")


if __name__ == "__main__":
    main()
