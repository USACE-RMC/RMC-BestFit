"""PlotSpec rejects ambiguous or misaligned source coordinates."""
import copy
import sys
from pathlib import Path

import pytest

SKILL = Path(__file__).resolve().parents[2] / "skills/bestfit-frequency"
sys.path.insert(0, str(SKILL))

import bestfit_plots  # noqa: E402
from bestfit_plots import validate_spec  # noqa: E402


def valid_spec():
    return {
        "version": 1,
        "plotId": "fitting.frequency",
        "variant": "default",
        "source": {"kind": "api", "id": "example-1", "runId": "run-7"},
        "title": "Frequency",
        "axes": {
            "x": {"label": "AEP", "unit": "", "scale": "normal_probability", "value": "aep"},
            "y": {"label": "Flow", "unit": "cfs", "scale": "log", "value": "flow"},
        },
        "series": [{"name": "Computed", "kind": "line", "x": [0.5, 0.1], "y": [100, 300], "style": {}}],
        "omissions": [],
    }


@pytest.mark.parametrize("change,match", [
    (lambda s: s.update(version=2), "version"),
    (lambda s: s["source"].pop("runId"), "runId"),
    (lambda s: s["axes"]["x"].update(scale="normal_probability", value="date"), "aep"),
    (lambda s: s["series"][0].update(y=[100]), "align"),
    (lambda s: s["series"][0].update(x=[0.5, 1.0]), "AEP"),
    (lambda s: s["series"][0].update(y=[100, float("inf")]), "finite"),
    (lambda s: s["series"][0].update(kind="unknown"), "kind"),
    (lambda s: s["series"][0].update(interval={"kind": "confidence", "level": 1.0}), "level"),
])
def test_rejects_malformed_or_unidentified_coordinates(change, match):
    spec = valid_spec()
    change(spec)
    with pytest.raises(ValueError, match=match):
        validate_spec(spec)


def test_date_axis_requires_parseable_iso_dates():
    spec = valid_spec()
    spec["axes"]["x"].update(scale="date", value="date")
    spec["series"][0]["x"] = ["1897-01-01", "1898-01-01T12:00:00Z"]
    validate_spec(spec)
    spec["series"][0]["x"][1] = "1898?"
    with pytest.raises(ValueError, match="ISO"):
        validate_spec(spec)


def test_intervals_require_supplied_aligned_endpoints():
    spec = valid_spec()
    spec["series"][0].update(kind="band", yLower=[80, 200], yUpper=[140, 450], interval={"kind": "confidence", "level": 0.9})
    validate_spec(spec)
    bad = copy.deepcopy(spec)
    bad["series"][0]["yUpper"] = [140]
    with pytest.raises(ValueError, match="align"):
        validate_spec(bad)
    bad = copy.deepcopy(spec)
    bad["series"][0]["yLower"] = [150, 200]
    with pytest.raises(ValueError, match="lower"):
        validate_spec(bad)


def test_band_center_may_fall_outside_source_interval():
    spec = valid_spec()
    spec["series"][0].update(kind="band", y=[160, 300], yLower=[80, 200], yUpper=[140, 450], interval={"kind": "credible", "level": 0.9})
    validate_spec(spec)


def test_scatter_supports_asymmetric_supplied_x_and_y_bounds():
    spec = valid_spec()
    spec["axes"]["x"].update(scale="linear", value="stage")
    spec["series"][0].update(kind="scatter", x=[2, 3], y=[4, 6], xLower=[1.8, 2.7], xUpper=[2.4, 3.2], yLower=[3, 5], yUpper=[5, 8], interval={"kind": "measurement"})
    validate_spec(spec)


def test_heatmap_matrix_matches_supplied_axes():
    spec = valid_spec()
    spec["axes"]["x"].update(scale="linear", value="x")
    spec["axes"]["y"].update(scale="linear", value="y")
    spec["series"] = [{"name": "Density", "kind": "heatmap", "x": [1, 2, 3], "y": [4, 5], "z": [[1, 2, 3], [4, 5, 6]], "style": {}}]
    validate_spec(spec)
    spec["series"][0]["z"][1].pop()
    with pytest.raises(ValueError, match="matrix"):
        validate_spec(spec)


def test_histogram_widths_must_align_with_supplied_bins():
    spec = valid_spec()
    spec["axes"]["x"].update(scale="linear", value="magnitude")
    spec["axes"]["y"].update(scale="linear", value="count")
    spec["series"][0].update(kind="bars", width=[0.5, 1.5])
    validate_spec(spec)
    spec["series"][0]["width"] = [0.5]
    with pytest.raises(ValueError, match="align"):
        validate_spec(spec)


def test_series_omission_notes_must_be_strings():
    spec = valid_spec()
    spec["series"][0]["omissions"] = ["source dropped one failed fit"]
    validate_spec(spec)
    spec["series"][0]["omissions"] = [1]
    with pytest.raises(ValueError, match="omissions"):
        validate_spec(spec)


def test_null_interval_bounds_are_explicit_display_gaps():
    spec = valid_spec()
    spec["series"][0].update(kind="scatter", xLower=[0.4, None], xUpper=[0.6, None], interval={"kind": "measurement"})
    validate_spec(spec)


def test_existing_frequency_entry_points_are_package_exports():
    assert callable(bestfit_plots.build_figure)
    assert callable(bestfit_plots.save_figure)
    assert callable(bestfit_plots.main)


def test_area_is_a_baseline_fill_without_interval_claim():
    spec = valid_spec()
    spec["series"][0].update(kind="area", yLower=[0, 0], yUpper=[100, 300])
    validate_spec(spec)


def test_null_x_entries_are_explicit_qq_gaps():
    spec = valid_spec()
    spec["axes"]["x"].update(scale="linear", value="expected")
    spec["axes"]["y"].update(scale="linear", value="observed")
    spec["series"][0]["x"] = [1, None]
    validate_spec(spec)


def test_empty_plot_requires_documented_omission():
    spec = valid_spec()
    spec["series"] = []
    with pytest.raises(ValueError, match="omissions"):
        validate_spec(spec)
    spec["omissions"] = ["too few observations for ACF"]
    validate_spec(spec)


def test_positive_display_cutoffs_are_validated():
    spec = valid_spec()
    spec["axes"]["y"]["minimumPositive"] = 1e-16
    spec["series"][0]["style"]["minimumPositive"] = 0
    validate_spec(spec)
    spec["series"][0]["style"]["minimumPositive"] = -1
    with pytest.raises(ValueError, match="minimumPositive"):
        validate_spec(spec)


def test_horizontal_band_requires_one_interval_orientation():
    spec = valid_spec()
    spec["series"][0].update(kind="band", xLower=[0.4, 0.08], xUpper=[0.6, 0.12], interval={"kind": "credible", "level": 0.9})
    validate_spec(spec)
    spec["series"][0].update(yLower=[80, 200], yUpper=[140, 450])
    with pytest.raises(ValueError, match="orientation"):
        validate_spec(spec)


def test_horizontal_bar_orientation_and_labels_are_validated():
    spec = valid_spec()
    spec["axes"]["x"].update(scale="linear")
    spec["axes"]["y"].update(scale="linear")
    spec["series"][0].update(kind="bars", width=0.8,
                              style={"orientation": "horizontal", "labels": ["one", "two"]})
    validate_spec(spec)
    spec["series"][0]["style"]["labels"] = ["one"]
    with pytest.raises(ValueError, match="style.labels"):
        validate_spec(spec)
