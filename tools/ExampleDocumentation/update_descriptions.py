"""Apply reviewed description edits with an exact SQLite cell-preservation audit.

The manifest names each project, its expected SHA-256 and its reviewed edits.
Without --apply this command only validates the manifest and reports its scope.
Original files and before/after receipts are retained outside the examples tree.
"""
from __future__ import annotations

import argparse
from copy import deepcopy
import hashlib
import json
from pathlib import Path
import shutil
import sqlite3

from project_inventory import ROOT, cell_snapshot, digest, quote, read_project


def apply_updates(path, expected_hash, edits, *, remove_abom_test=False):
    """Atomically change descriptions and audit every cell before committing.

    The optional deletion is restricted to the explicitly approved ABOM test row.
    Callers managing real example projects must retain the original file first.
    """
    path = Path(path)
    if hashlib.sha256(path.read_bytes()).hexdigest() != expected_hash:
        raise ValueError(f"Source hash changed: {path}")
    if any(Path(str(path) + suffix).exists() and Path(str(path) + suffix).stat().st_size
           for suffix in ("-wal", "-journal")):
        raise ValueError(f"Close the project before editing metadata: {path}")
    before = read_project(path)
    expected = deepcopy(cell_snapshot(before))
    changes = []
    for edit in edits:
        if set(edit) != {"table", "rowid", "name", "description"} or not str(edit["description"]).strip():
            raise ValueError("Each edit must identify one row and provide a nonempty description")
        row = next((r for r in before.get(edit["table"], []) if r["_rowid_"] == edit["rowid"]), None)
        if row is None or row.get("Name") != edit["name"] or "Description" not in row:
            raise ValueError(f"Description row does not match the reviewed manifest: {edit['name']}")
        expected[edit["table"]][str(edit["rowid"])]["Description"] = digest(edit["description"])
        changes.append({**edit, "oldDescription": row["Description"]})
    deleted = None
    if remove_abom_test:
        if path.name != "abom-download-example.bestfit":
            raise ValueError("Only the approved ABOM test element may be removed")
        matches = [row for row in before.get("Time Series Data", []) if row.get("Name") == "Time Series_7" and row.get("Description") == "Test"]
        if len(matches) != 1:
            raise ValueError("The reviewed ABOM test row no longer matches")
        deleted = matches[0]
        expected["Time Series Data"].pop(str(deleted["_rowid_"]))
    with sqlite3.connect(path) as connection:
        connection.row_factory = sqlite3.Row
        connection.execute("BEGIN IMMEDIATE")
        for edit in edits:
            connection.execute(f"UPDATE {quote(edit['table'])} SET Description=? WHERE rowid=?",
                               (edit["description"], edit["rowid"]))
        if deleted:
            connection.execute('DELETE FROM "Time Series Data" WHERE rowid=?', (deleted["_rowid_"],))
        after = {table: [dict(row) for row in connection.execute(f"SELECT rowid AS _rowid_, * FROM {quote(table)} ORDER BY rowid")]
                 for table in before}
        if cell_snapshot(after) != expected:
            raise ValueError("Unapproved SQLite cells changed; transaction rolled back")
        if connection.execute("PRAGMA integrity_check").fetchone()[0] != "ok":
            raise ValueError("SQLite integrity failure; transaction rolled back")
    return {"beforeSha256": expected_hash, "afterSha256": hashlib.sha256(path.read_bytes()).hexdigest(),
            "onlyApprovedCellsChanged": True, "edits": changes,
            "deletedRow": {"table": "Time Series Data", "rowid": deleted["_rowid_"], "name": deleted["Name"]} if deleted else None,
            "preservedCellCount": sum(len(row) for rows in expected.values() for row in rows.values()) - len(edits)}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("manifest", type=Path)
    parser.add_argument("--apply", action="store_true")
    parser.add_argument("--audit-dir", type=Path, default=ROOT / "artifacts/example-documentation/metadata-audit")
    args = parser.parse_args()
    manifest = json.loads(args.manifest.read_text(encoding="utf-8"))
    for item in manifest:
        path = (ROOT / item["project"]).resolve()
        if not path.is_relative_to((ROOT / "examples").resolve()) or path.suffix != ".bestfit":
            raise ValueError("Manifest project must be an active database within examples")
        if hashlib.sha256(path.read_bytes()).hexdigest() != item["sha256"]:
            raise ValueError(f"Source hash changed: {path}")
        print(f"{path.name}: {len(item['edits'])} description edits")
        if not args.apply:
            continue
        backup = args.audit_dir / "originals" / path.name
        backup.parent.mkdir(parents=True, exist_ok=True)
        if backup.exists() and hashlib.sha256(backup.read_bytes()).hexdigest() != item["sha256"]:
            raise ValueError(f"Refusing to overwrite a different original backup: {backup}")
        if not backup.exists():
            shutil.copy2(path, backup)
        receipt = apply_updates(path, item["sha256"], item["edits"], remove_abom_test=item.get("removeAbomTest", False))
        (args.audit_dir / (path.stem + ".json")).write_text(json.dumps(receipt, indent=2, ensure_ascii=False), encoding="utf-8")


if __name__ == "__main__":
    main()
