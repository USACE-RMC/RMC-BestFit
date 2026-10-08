"""Capture receipts must bind unchanged sources to the requested native view.

All source/artifact writes stay under tmp_path. The only replaced dependency is
subprocess.run: starting the Windows desktop exporter is outside a unit test.
"""
import copy
import gzip
import hashlib
import json
from pathlib import Path
import subprocess
import sys

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parent))
import render_screenshots as renderer


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


@pytest.fixture
def capture_case(tmp_path, monkeypatch):
    root = tmp_path / "repository"
    root.mkdir()
    monkeypatch.setattr(renderer, "ROOT", root)
    source = root / "examples/current.bestfit"
    source.parent.mkdir()
    source.write_bytes(b"authoritative saved project bytes")
    output = root / "examples/tutorial/screenshots/current-view"
    output.parent.mkdir(parents=True)
    entry = {
        "prPath": "examples/contributor-view.png",
        "disposition": "native-export",
        "project": "examples/current.bestfit",
        "sourceSha256": sha(source),
        "output": "examples/tutorial/screenshots/current-view",
        "element": "Saved Fit",
        "plotId": "rating.curve",
        "variant": "default",
    }
    snapshot = {
        "formatVersion": 1,
        "project": str(source),
        "sourceSha256": entry["sourceSha256"],
        "sourceSha256After": entry["sourceSha256"],
        "element": "Saved Fit",
        "plotId": "rating.curve",
        "variant": "default",
        "series": [{"name": "StageDischargeData", "points": [[1, 2], [3, 4]]}],
    }
    return root, source, output, entry, snapshot


def saved_receipt(output, entry, snapshot):
    """Write independently constructed artifact bytes and their recorded digests."""
    output.with_suffix(".png").write_bytes(b"native PNG fixture")
    output.with_suffix(".svg").write_text("<svg>native fixture</svg>", encoding="utf-8")
    output.with_suffix(".json.gz").write_bytes(gzip.compress(json.dumps(snapshot).encode(), mtime=0))
    paths = [output.with_suffix(suffix) for suffix in (".png", ".svg", ".json.gz")]
    entry["outputs"] = [{"path": path.relative_to(renderer.ROOT).as_posix(), "sha256": sha(path)} for path in paths]
    entry["evidence"] = {
        "kind": "recaptured-current-project",
        "snapshot": entry["output"] + ".json.gz",
        "sourceSha256Before": entry["sourceSha256"],
        "sourceSha256After": entry["sourceSha256"],
        "savedEstimatorsRerun": False,
    }


def forbid_export(*args, **kwargs):
    raise AssertionError("Rejected or cached request must not start the native exporter")


@pytest.mark.parametrize("refresh", [False, True])
def test_changed_source_is_refused_before_any_export(capture_case, monkeypatch, refresh):
    _, source, output, entry, _ = capture_case
    source.write_bytes(b"different scientific state")
    monkeypatch.setattr(renderer.subprocess, "run", forbid_export)
    with pytest.raises(ValueError, match="Source changed"):
        renderer.capture(entry, Path("unused.exe"), refresh)
    assert not output.with_suffix(".png").exists()


def test_valid_saved_artifacts_verify_without_rewriting(capture_case, monkeypatch):
    _, source, output, entry, snapshot = capture_case
    saved_receipt(output, entry, snapshot)
    before = {p: p.read_bytes() for p in [source, *output.parent.iterdir()]}
    monkeypatch.setattr(renderer.subprocess, "run", forbid_export)
    renderer.capture(entry, Path("unused.exe"), False)
    assert {p: p.read_bytes() for p in before} == before


def test_changed_png_is_rejected_even_when_source_is_current(capture_case, monkeypatch):
    _, source, output, entry, snapshot = capture_case
    saved_receipt(output, entry, snapshot)
    output.with_suffix(".png").write_bytes(b"different displayed result")
    monkeypatch.setattr(renderer.subprocess, "run", forbid_export)
    with pytest.raises(ValueError, match="Output changed"):
        renderer.capture(entry, Path("unused.exe"), False)
    assert sha(source) == entry["sourceSha256"]


@pytest.mark.parametrize("missing", [".svg", ".json.gz", "evidence"])
def test_incomplete_saved_receipt_is_rejected(capture_case, monkeypatch, missing):
    _, _, output, entry, snapshot = capture_case
    saved_receipt(output, entry, snapshot)
    if missing == "evidence":
        del entry["evidence"]
    else:
        entry["outputs"] = [a for a in entry["outputs"] if not a["path"].endswith(missing)]
    monkeypatch.setattr(renderer.subprocess, "run", forbid_export)
    with pytest.raises(ValueError):
        renderer.capture(entry, Path("unused.exe"), False)


def test_duplicate_output_receipt_cannot_mask_ambiguous_artifacts(capture_case, monkeypatch):
    _, _, output, entry, snapshot = capture_case
    saved_receipt(output, entry, snapshot)
    entry["outputs"].append(copy.deepcopy(entry["outputs"][0]))
    monkeypatch.setattr(renderer.subprocess, "run", forbid_export)
    with pytest.raises(ValueError):
        renderer.capture(entry, Path("unused.exe"), False)


@pytest.mark.parametrize("field,wrong", [("sourceSha256", "wrong-source"), ("sourceSha256After", None),
                                         ("element", "Different Fit"), ("plotId", "rating.residuals"), ("variant", "segmented")])
def test_saved_snapshot_must_match_the_requested_view(capture_case, monkeypatch, field, wrong):
    _, _, output, entry, snapshot = capture_case
    snapshot[field] = wrong
    saved_receipt(output, entry, snapshot)
    monkeypatch.setattr(renderer.subprocess, "run", forbid_export)
    with pytest.raises(ValueError):
        renderer.capture(entry, Path("unused.exe"), False)


@pytest.mark.parametrize("field,wrong", [("snapshot", "examples/tutorial/screenshots/unrecorded.json.gz"),
                                         ("sourceSha256Before", "wrong-source"), ("sourceSha256After", "wrong-source")])
def test_saved_evidence_must_bind_the_verified_snapshot_and_source(capture_case, monkeypatch, field, wrong):
    _, _, output, entry, snapshot = capture_case
    saved_receipt(output, entry, snapshot)
    entry["evidence"][field] = wrong
    monkeypatch.setattr(renderer.subprocess, "run", forbid_export)
    with pytest.raises(ValueError):
        renderer.capture(entry, Path("unused.exe"), False)


@pytest.mark.parametrize("field,wrong", [("sourceSha256", "wrong-source"), ("sourceSha256After", None),
                                         ("element", "Different Fit"), ("plotId", "rating.residuals"), ("variant", "segmented")])
def test_fresh_export_receipt_must_match_source_and_view(capture_case, monkeypatch, field, wrong):
    _, source, output, entry, snapshot = capture_case
    snapshot[field] = wrong

    def export_result(args, **kwargs):
        output.with_suffix(".png").write_bytes(b"native PNG fixture")
        output.with_suffix(".svg").write_text("<svg/>", encoding="utf-8")
        output.with_suffix(".json").write_text(json.dumps(snapshot), encoding="utf-8")
        return subprocess.CompletedProcess(args, 0, "", "")

    monkeypatch.setattr(renderer.subprocess, "run", export_result)
    with pytest.raises(ValueError):
        renderer.capture(entry, Path("fake-exporter.exe"), True)
    assert sha(source) == entry["sourceSha256"]


@pytest.mark.parametrize("absolute", [False, True])
def test_output_path_cannot_escape_repository(capture_case, monkeypatch, absolute):
    root, source, _, entry, _ = capture_case
    outside = root.parent / "outside"
    entry["output"] = str(outside) if absolute else "../outside"
    monkeypatch.setattr(renderer.subprocess, "run", forbid_export)
    with pytest.raises(ValueError, match="outside"):
        renderer.capture(entry, Path("unused.exe"), True)
    assert not outside.with_suffix(".png").exists()
    assert sha(source) == entry["sourceSha256"]


@pytest.mark.parametrize("raises_timeout", [False, True])
def test_failed_export_still_detects_original_source_modification(capture_case, monkeypatch, raises_timeout):
    _, source, _, entry, _ = capture_case

    def failed_export(args, **kwargs):
        source.write_bytes(b"unexpected exporter-side source mutation")
        if raises_timeout:
            raise subprocess.TimeoutExpired(args, 600)
        return subprocess.CompletedProcess(args, 1, "", "capture failed")

    monkeypatch.setattr(renderer.subprocess, "run", failed_export)
    with pytest.raises(ValueError, match="Source changed during export"):
        renderer.capture(entry, Path("fake-exporter.exe"), True)
    assert source.read_bytes() == b"unexpected exporter-side source mutation"
