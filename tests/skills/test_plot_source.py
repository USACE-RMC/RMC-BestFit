"""Saved artifacts render without starting a server or importing a numerical runtime."""
import gzip
import importlib.util
import json
from pathlib import Path
import sys

import pytest

SKILL = Path(__file__).resolve().parents[2] / "skills/bestfit-frequency"
sys.path.insert(0, str(SKILL))


def artifact():
    return {"version": 1, "plotId": "rating.curve", "variant": "default",
            "source": {"kind": "saved", "id": "field rating", "runId": "sha256:source"},
            "title": "Rating Curve", "axes": {
                "x": {"label": "Discharge", "unit": "cfs", "scale": "linear", "value": "value"},
                "y": {"label": "Stage", "unit": "ft", "scale": "linear", "value": "value"}},
            "series": [{"name": "Posterior Mode", "kind": "line", "x": [20, 40], "y": [2, 3]}],
            "omissions": []}


def test_gzipped_case_and_single_spec_keep_exact_source_geometry(tmp_path, monkeypatch):
    import builtins
    original_import = builtins.__import__
    def guarded_import(name, *args, **kwargs):
        assert name.split(".")[0] not in {"clr", "pythonnet", "RMC", "Numerics", "bestfit_examples"}
        return original_import(name, *args, **kwargs)
    monkeypatch.setattr(builtins, "__import__", guarded_import)
    from bestfit_plots.source import load_plots
    spec = artifact()
    path = tmp_path / "case.json.gz"
    path.write_bytes(gzip.compress(json.dumps({"schemaVersion": 1, "plots": {"curve": spec}}).encode()))
    assert load_plots(path) == {"curve": spec}
    path = tmp_path / "plot.json"
    path.write_text(json.dumps(spec))
    assert load_plots(path) == {"rating.curve": spec}


@pytest.mark.parametrize("change", [{"success": False}, {"state": "Running"},
                                  {"lastRunUtc": None}, {"analysisId": None}])
def test_rejects_noncompleted_api_sources_before_adapter(tmp_path, change):
    from bestfit_plots.source import load_plots
    source = {"schemaVersion": 1, "success": True, "analysisId": "one", "state": "succeeded",
              "lastRunUtc": "2026-09-22T12:00:00Z", "kind": "rating"}
    source.update(change)
    path = tmp_path / "source.json"
    path.write_text(json.dumps(source))
    with pytest.raises(ValueError):
        load_plots(path)


def test_package_includes_every_python_module_and_plot_map(tmp_path):
    import zipfile
    module_spec = importlib.util.spec_from_file_location("package_skill", SKILL.parents[1] / "scripts/package-bestfit-skill.py")
    module = importlib.util.module_from_spec(module_spec)
    module_spec.loader.exec_module(module)
    target = tmp_path / "skill.zip"
    module.package(target)
    with zipfile.ZipFile(target) as archive:
        names = set(archive.namelist())
    for file in (SKILL / "bestfit_plots").rglob("*.py"):
        assert "bestfit-frequency/" + file.relative_to(SKILL).as_posix() in names
    assert "bestfit-frequency/references/app-plot-map.json" in names
    assert "bestfit-frequency/scripts/plot_source.py" in names


def test_frequency_comparison_preserves_both_sources_and_rejects_unit_mismatch():
    from copy import deepcopy
    from bestfit_plots.source import add_frequency_comparison
    base = artifact()
    base.update(plotId="univariate.frequency", title="Frequency")
    base["axes"]["x"] = {"label":"AEP", "unit":"", "scale":"normal_probability", "value":"aep"}
    base["series"][0]["x"] = [.5, .1]
    other = deepcopy(base)
    other["source"]["id"] = "alternative"
    other["source"]["runId"] = "sha256:other"
    combined = add_frequency_comparison(base, other, "Alternative")
    assert len(base["series"]) == 1
    assert len(combined["series"]) == 2
    assert combined["series"][-1]["name"] == "Alternative - Posterior Mode"
    assert combined["comparedSources"] == [base["source"], other["source"]]
    with pytest.raises(ValueError, match="single"):
        add_frequency_comparison(base, combined, "Nested")
    other["axes"]["y"]["unit"] = "m"
    with pytest.raises(ValueError, match="axes"):
        add_frequency_comparison(base, other, "Alternative")
