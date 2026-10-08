"""Use an actual saved project and app plot population as the reference oracle."""
import hashlib
import json
from pathlib import Path
import subprocess
import math

import pytest


REPO = Path(__file__).resolve().parents[2]
EXE = REPO / "tools/PlotReferenceExporter/bin/Debug/net10.0-windows/PlotReferenceExporter.exe"
PROJECT = REPO / "examples/2-input-data/1-block-maximum/usgs-block-max-example.bestfit"


def test_regression_residuals_use_the_saved_coefficients_without_refitting(tmp_path):
    source = next((REPO / "examples").rglob("time-series-regression-example.bestfit"))
    before = hashlib.sha256(source.read_bytes()).hexdigest()
    prefix = tmp_path / "regression-residuals"
    result = subprocess.run([str(EXE), "--project", str(source), "--element", "Multiple Linear Regression",
                             "--plot-id", "time_series_analysis.residuals", "--variant", "default", "--output", str(prefix)],
                            capture_output=True, text=True, timeout=120)
    assert result.returncode == 0, result.stderr
    geometry = json.loads(prefix.with_suffix('.json').read_text(encoding='utf8'))
    residuals = next(s['points'] for s in geometry['series'] if s['name'] == 'Residuals')
    # Independent observed-minus-saved-curve arithmetic, not reset live parameters.
    assert len(residuals) == 149
    assert residuals[0]['Y'] == pytest.approx(.1900051893782668, abs=1e-12)
    assert residuals[41]['Y'] == pytest.approx(-.6470923247633205, abs=1e-12)
    assert math.sqrt(sum(p['Y']**2 for p in residuals)/149) == pytest.approx(.3411468296454542, abs=1e-12)
    assert any('saved parameter' in note for note in geometry['displayCorrections'])
    assert hashlib.sha256(source.read_bytes()).hexdigest() == before


def test_same_named_input_and_analysis_resolve_by_requested_view(tmp_path):
    source = next((REPO / "examples").rglob("arr-flike-examples.bestfit"))
    before = hashlib.sha256(source.read_bytes()).hexdigest()
    for slot, variant, kind in (("input_data.frequency", "default", "InputData"),
                                ("univariate.frequency", "stationary", "UnivariateAnalysis"),
                                ("shared_diagnostics.trace", "chain", "UnivariateAnalysis")):
        prefix = tmp_path / slot
        result = subprocess.run([str(EXE), "--project", str(source), "--element", "Example #3",
                                 "--plot-id", slot, "--variant", variant, "--output", str(prefix)],
                                capture_output=True, text=True, timeout=120)
        assert result.returncode == 0, result.stderr
        geometry = json.loads(Path(str(prefix) + ".json").read_text(encoding="utf-8"))
        assert geometry["analysisKind"] == kind
        assert any(s.get("points") for s in geometry["series"])
    assert hashlib.sha256(source.read_bytes()).hexdigest() == before


def test_enabled_quantile_prior_does_not_require_a_magic_element_name(tmp_path):
    source = next((REPO / "examples").rglob("viglione-et-al-2013.bestfit"))
    prefix = tmp_path / "causal"
    result = subprocess.run([str(EXE), "--project", str(source), "--element", "MCMC - Systematic (1951-2001) + Temporal + Causal",
                             "--plot-id", "univariate.frequency", "--variant", "quantile_prior", "--output", str(prefix)],
                            capture_output=True, text=True, timeout=120)
    assert result.returncode == 0, result.stderr
    geometry = json.loads(prefix.with_suffix(".json").read_text(encoding="utf-8"))
    assert any('prior' in str(s.get('Title','')).lower() and s.get('points') for s in geometry['series'])


def test_exports_app_geometry_without_touching_source(tmp_path):
    before = hashlib.sha256(PROJECT.read_bytes()).hexdigest()
    prefix = tmp_path / "moose-frequency"
    result = subprocess.run([
        str(EXE), "--project", str(PROJECT), "--element", "USGS - 01134500 - Block Max - Calendar Year",
        "--plot-id", "input_data.frequency", "--variant", "default", "--output", str(prefix),
    ], capture_output=True, text=True, timeout=120)
    assert result.returncode == 0, result.stderr
    assert hashlib.sha256(PROJECT.read_bytes()).hexdigest() == before
    geometry = json.loads(prefix.with_suffix(".json").read_text(encoding="utf-8"))
    assert geometry["sourceSha256"] == before
    assert geometry["plotId"] == "input_data.frequency"
    assert any(axis["type"] == "NormalProbabilityAxis" for axis in geometry["axes"])
    exact = next(series for series in geometry["series"] if series["name"] == "ExactData")
    assert any(abs(point["X"] - 0.5432098765432098) < 1e-12 and point["Y"] == 1770 for point in exact["points"])
    assert exact["IsVisible"] is True
    assert exact["style"]["MarkerType"] == "Circle"
    assert geometry["runtime"]["appSha256"]
    assert prefix.with_suffix(".svg").stat().st_size > 1000
    assert prefix.with_suffix(".png").stat().st_size > 1000


def test_exports_app_contour_grid_and_rendered_paths(tmp_path):
    source = next((REPO / "examples").rglob("bivariate-distribution-examples.bestfit"))
    prefix = tmp_path / "normal-density"
    result = subprocess.run([
        str(EXE), "--project", str(source), "--element", "Normal Copula",
        "--plot-id", "bivariate.distribution", "--variant", "density_values",
        "--output", str(prefix),
    ], capture_output=True, text=True, timeout=120)
    assert result.returncode == 0, result.stderr
    geometry = json.loads(prefix.with_suffix(".json").read_text(encoding="utf-8"))
    assert geometry["variantSelection"] == "PlotTypeComboBox=1;AxisTypeComboBox=0"
    contour = next(series for series in geometry["series"] if series["type"] == "ContourSeries")
    assert len(contour["grid"]["data"]) == 100
    assert len(contour["grid"]["data"][0]) == 100
    assert len(contour["contours"]) > 0
    assert all(len(path["points"]) >= 2 for path in contour["contours"])


def test_exports_app_histogram_bin_geometry_and_reference_annotation(tmp_path):
    histogram = tmp_path / "moose-histogram"
    result = subprocess.run([
        str(EXE), "--project", str(PROJECT),
        "--element", "USGS - 01134500 - Block Max - Calendar Year",
        "--plot-id", "input_data.histogram", "--variant", "default",
        "--output", str(histogram),
    ], capture_output=True, text=True, timeout=120)
    assert result.returncode == 0, result.stderr
    geometry = json.loads(histogram.with_suffix(".json").read_text(encoding="utf-8"))
    bins = geometry["series"][0]["actualItems"]
    assert len(bins) >= 10
    assert all(bin_["RangeStart"] < bin_["RangeEnd"] for bin_ in bins)
    assert abs(sum(bin_["Area"] for bin_ in bins) - 1) < 1e-12

    source = next((REPO / "examples").rglob("usgs-07024175-mississippi-rating-curve.bestfit"))
    residuals = tmp_path / "rating-residuals"
    result = subprocess.run([
        str(EXE), "--project", str(source), "--element", "USGS 07024175 Rating Curve",
        "--plot-id", "rating.residuals", "--variant", "default", "--output", str(residuals),
    ], capture_output=True, text=True, timeout=120)
    assert result.returncode == 0, result.stderr
    geometry = json.loads(residuals.with_suffix(".json").read_text(encoding="utf-8"))
    zero = next(note for note in geometry["annotations"] if note["type"] == "LineAnnotation")
    assert zero["geometry"]["Intercept"] == 0
    assert zero["geometry"]["Slope"] == 0
