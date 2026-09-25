"""Metadata edits must preserve every unrelated SQLite cell and roll back failures."""
import hashlib
from pathlib import Path
import sqlite3
import sys

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parent))


def database(path):
    with sqlite3.connect(path) as c:
        c.execute('CREATE TABLE "Input Data" (Name TEXT, Description TEXT, DataFrame BLOB, Seed INTEGER)')
        c.execute('INSERT INTO "Input Data" VALUES (?, ?, ?, ?)', ("Record", "Old description", b"\x00\xffpayload", 12345))
    return hashlib.sha256(path.read_bytes()).hexdigest()


def test_description_edit_preserves_seed_and_binary_payload(tmp_path):
    from update_descriptions import apply_updates
    from project_inventory import read_project
    path = tmp_path / "fixture.bestfit"
    source_hash = database(path)
    before = read_project(path)
    receipt = apply_updates(path, source_hash, [{"table": "Input Data", "rowid": 1, "name": "Record", "description": "Annual peak record."}])
    after = read_project(path)
    assert after["Input Data"][0]["Description"] == "Annual peak record."
    assert after["Input Data"][0]["DataFrame"] == before["Input Data"][0]["DataFrame"]
    assert after["Input Data"][0]["Seed"] == 12345
    assert receipt["onlyApprovedCellsChanged"] is True


def test_invalid_or_stale_request_cannot_partly_edit_project(tmp_path):
    from update_descriptions import apply_updates
    from project_inventory import read_project
    path = tmp_path / "fixture.bestfit"
    source_hash = database(path)
    before = read_project(path)
    edits = [{"table": "Input Data", "rowid": 1, "name": "Record", "description": "New description"},
             {"table": "Input Data", "rowid": 99, "name": "Missing", "description": "Cannot apply"}]
    with pytest.raises(ValueError):
        apply_updates(path, source_hash, edits)
    assert read_project(path) == before
    with pytest.raises(ValueError, match="hash"):
        apply_updates(path, "stale", edits[:1])
    assert read_project(path) == before


def test_unapproved_trigger_change_rolls_back_description_and_payload(tmp_path):
    from update_descriptions import apply_updates
    from project_inventory import read_project
    path = tmp_path / "fixture.bestfit"
    database(path)
    with sqlite3.connect(path) as connection:
        connection.execute('CREATE TRIGGER unexpected AFTER UPDATE ON "Input Data" BEGIN UPDATE "Input Data" SET Seed=1; END')
    before = read_project(path)
    source_hash = hashlib.sha256(path.read_bytes()).hexdigest()
    with pytest.raises(ValueError, match="Unapproved"):
        apply_updates(path, source_hash, [{"table": "Input Data", "rowid": 1, "name": "Record", "description": "New"}])
    assert read_project(path) == before
