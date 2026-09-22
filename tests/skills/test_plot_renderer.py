"""Renderer uses supplied geometry and display-only axis transforms."""
import copy
import sys
import tempfile
from pathlib import Path

import matplotlib
matplotlib.use("Agg")
import matplotlib.dates as mdates
import matplotlib.pyplot as plt
from matplotlib.colors import to_rgba
import numpy as np
import pytest

SKILL = Path(__file__).resolve().parents[2] / "skills/bestfit-frequency"
sys.path.insert(0, str(SKILL))

from bestfit_plots import export_plot, render_plot  # noqa: E402
from test_plot_spec import valid_spec  # noqa: E402


@pytest.fixture(autouse=True)
def close_plots():
    yield
    plt.close("all")


def test_normal_probability_line_preserves_order_labels_and_input():
    spec = valid_spec()
    spec["series"][0]["x"] = [0.1, 0.5]
    spec["series"][0]["y"] = [300, 100]
    before = copy.deepcopy(spec)
    fig = render_plot(spec)
    ax = fig.axes[0]
    line = ax.lines[0]
    np.testing.assert_allclose(line.get_xdata(), [1.2815515655446004, 0], atol=1e-12)
    np.testing.assert_array_equal(line.get_ydata(), [300, 100])
    assert ax.get_xlabel() == "AEP"
    assert ax.get_ylabel() == "Flow (cfs)"
    assert ax.get_yscale() == "log"
    assert ax.get_xlim()[1] < 2
    assert spec == before


def test_log_axis_exclusions_are_gaps_with_omission_count():
    spec = valid_spec()
    spec["series"][0].update(x=[0.5, 0.1, 0.01], y=[100, 0, 300])
    fig = render_plot(spec)
    assert np.isnan(fig.axes[0].lines[0].get_ydata()[1])
    assert fig.bestfit_omissions == ["Computed: omitted 1 nonpositive log-display ordinate; gaps retained."]


def test_log_x_exclusions_are_gaps():
    spec = valid_spec()
    spec["axes"]["x"].update(scale="log", value="magnitude")
    spec["axes"]["y"].update(scale="linear")
    spec["series"][0].update(x=[1, 0, 3], y=[4, 5, 6])
    fig = render_plot(spec)
    assert np.isnan(fig.axes[0].lines[0].get_xdata()[1])
    assert any("omitted 1 nonpositive log-display abscissa" in note for note in fig.bestfit_omissions)


def test_band_and_scatter_bounds_are_supplied_and_labeled():
    spec = valid_spec()
    spec["series"] = [
        {"name": "90% Confidence Intervals", "kind": "band", "x": [0.5, 0.1], "y": [100, 300], "yLower": [80, 200], "yUpper": [140, 450], "interval": {"kind": "confidence", "level": 0.9}, "style": {}},
        {"name": "Observed", "kind": "scatter", "x": [0.2], "y": [220], "yLower": [180], "yUpper": [260], "interval": {"kind": "measurement"}, "style": {"marker": "o"}},
    ]
    fig = render_plot(spec)
    ax = fig.axes[0]
    assert set(ax.get_legend_handles_labels()[1]) == {"90% Confidence Intervals", "Observed"}
    segments = [seg for artist in ax.collections if hasattr(artist, "get_segments") for seg in artist.get_segments()]
    assert any(np.allclose(seg[:, 1], [180, 260]) for seg in segments)
    band_vertices = [path.vertices[:, 1] for artist in ax.collections if hasattr(artist, "get_paths") for path in artist.get_paths() if len(path.vertices) > 5]
    assert any({80, 200, 450, 140}.issubset(set(vertices)) for vertices in band_vertices)


def test_date_axis_and_asymmetric_x_errorbars():
    spec = valid_spec()
    spec["axes"]["x"].update(scale="date", value="date")
    spec["axes"]["y"].update(scale="linear")
    spec["series"] = [{"name": "Measured", "kind": "scatter", "x": ["2001-01-02"], "y": [4], "xLower": ["2001-01-01"], "xUpper": ["2001-01-05"], "interval": {"kind": "measurement"}, "style": {}}]
    fig = render_plot(spec)
    ax = fig.axes[0]
    x = mdates.date2num(np.datetime64("2001-01-02"))
    assert any(np.allclose(seg[:, 0], [x - 1, x + 3]) for artist in ax.collections if hasattr(artist, "get_segments") for seg in artist.get_segments())


def test_aep_x_bounds_follow_reversed_display_orientation():
    spec = valid_spec()
    spec["axes"]["y"].update(scale="linear")
    spec["series"] = [{"name": "Interval", "kind": "scatter", "x": [0.5], "y": [4], "xLower": [0.4], "xUpper": [0.6], "interval": {"kind": "measurement"}, "style": {}}]
    fig = render_plot(spec)
    segments = [seg for artist in fig.axes[0].collections if hasattr(artist, "get_segments") for seg in artist.get_segments()]
    assert any(np.allclose(seg[:, 0], [-0.2533471031357997, 0.2533471031357997], atol=1e-12) for seg in segments)


def test_null_scatter_bound_leaves_other_supplied_bound_visible():
    spec = valid_spec()
    spec["axes"]["y"].update(scale="linear")
    spec["series"] = [{"name": "Interval", "kind": "scatter", "x": [0.5, 0.1], "y": [4, 8], "xLower": [0.4, None], "xUpper": [0.6, None], "interval": {"kind": "measurement"}, "style": {}}]
    fig = render_plot(spec)
    segments = [seg for artist in fig.axes[0].collections if hasattr(artist, "get_segments") for seg in artist.get_segments()]
    assert len(segments) == 1


def test_histogram_heatmap_contour_use_supplied_values():
    spec = valid_spec()
    spec["axes"]["x"].update(scale="linear", value="x")
    spec["axes"]["y"].update(scale="linear", value="y")
    spec["series"] = [{"name": "Counts", "kind": "bars", "x": [1, 2], "y": [3, 5], "width": 0.5, "style": {}}]
    fig = render_plot(spec)
    assert [bar.get_height() for bar in fig.axes[0].patches] == [3, 5]
    assert [bar.get_x() for bar in fig.axes[0].patches] == [0.75, 1.75]
    with tempfile.TemporaryDirectory(dir=SKILL) as output_dir:
        paths = export_plot(spec, Path(output_dir) / "bars")
        assert {path.suffix for path in paths} == {".png", ".svg"}
        assert all(path.stat().st_size > 1000 for path in paths)

    spec["series"] = [{"name": "Density", "kind": "heatmap", "x": [1, 2], "y": [3, 4], "z": [[1, 2], [3, 4]], "style": {}}]
    fig = render_plot(spec)
    np.testing.assert_array_equal(np.asarray(fig.axes[0].collections[0].get_array()).reshape(2, 2), [[1, 2], [3, 4]])
    spec["series"][0]["kind"] = "contour"
    spec["series"][0]["style"] = {"levels": [2.5]}
    fig = render_plot(spec)
    assert len(fig.axes[0].collections) > 0


def test_variable_width_histogram_and_series_omission_note():
    spec = valid_spec()
    spec["axes"]["x"].update(scale="linear", value="magnitude")
    spec["axes"]["y"].update(scale="linear", value="count")
    spec["series"][0].update(kind="bars", x=[1, 2], y=[3, 4], width=[0.5, 1.5], omissions=["one failed fit omitted"])
    fig = render_plot(spec)
    assert [bar.get_width() for bar in fig.axes[0].patches] == [0.5, 1.5]
    assert fig.bestfit_omissions == ["Computed: one failed fit omitted"]


def test_area_fills_supplied_baseline_without_interval_label():
    spec = valid_spec()
    spec["axes"]["y"].update(scale="linear")
    spec["series"][0].update(kind="area", yLower=[0, 0], yUpper=[100, 300])
    fig = render_plot(spec)
    vertices = fig.axes[0].collections[0].get_paths()[0].vertices[:, 1]
    assert {0, 100, 300}.issubset(set(vertices))
    assert fig.axes[0].get_legend_handles_labels()[1] == ["Computed"]


def test_bounds_color_and_per_series_log_cutoff():
    spec = valid_spec()
    spec["axes"]["y"]["minimumPositive"] = 1e-16
    spec["series"] = [
        {"name": "Curve", "kind": "line", "x": [0.5, 0.1], "y": [1e-20, 100], "style": {"minimumPositive": 0}},
        {"name": "Observation", "kind": "scatter", "x": [0.5], "y": [1e-20], "yLower": [1e-21], "yUpper": [1e-19], "interval": {"kind": "measurement"}, "style": {"color": "green", "boundsColor": "black"}},
        {"name": "Visible interval", "kind": "scatter", "x": [0.1], "y": [10], "yLower": [8], "yUpper": [12], "interval": {"kind": "measurement"}, "style": {"color": "green", "boundsColor": "black"}},
    ]
    fig = render_plot(spec)
    ax = fig.axes[0]
    assert ax.lines[0].get_ydata()[0] == 1e-20
    assert len(next(c for c in ax.collections if c.get_label() == "Observation").get_offsets()) == 0
    assert any("Observation" in note and "1e-16" in note for note in fig.bestfit_omissions)
    stroke = next(c for c in ax.collections if hasattr(c, "get_segments") and c.get_segments())
    np.testing.assert_allclose(stroke.get_colors()[0], [0, 0, 0, 1])


def test_null_qq_x_coordinate_remains_a_gap():
    spec = valid_spec()
    spec["axes"]["x"].update(scale="linear", value="expected")
    spec["axes"]["y"].update(scale="linear", value="observed")
    spec["series"][0]["x"] = [1, None]
    fig = render_plot(spec)
    assert np.isnan(fig.axes[0].lines[0].get_xdata()[1])


def test_hidden_source_series_is_not_drawn_or_listed_in_legend():
    spec = valid_spec()
    spec["series"].append({"name": "Hidden app alternative", "kind": "line",
                           "x": [.5, .1], "y": [10, 20], "style": {"visible": False}})
    fig = render_plot(spec)
    assert len(fig.axes[0].lines) == 1
    assert fig.axes[0].get_legend_handles_labels()[1] == ["Computed"]


def test_source_series_can_render_without_a_legend_entry():
    spec = valid_spec()
    spec["series"].append({"name": "Threshold 1980-2000", "kind": "scatter",
                           "x": [.5], "y": [10], "style": {"legend": False}})
    fig = render_plot(spec)
    assert len(fig.axes[0].collections) == 1
    assert fig.axes[0].get_legend_handles_labels()[1] == ["Computed"]


def test_horizontal_categorical_bars_use_values_as_widths_and_observation_labels():
    spec = valid_spec()
    spec["axes"]["x"] = {"label": "Observation", "unit": "", "scale": "linear", "value": "index"}
    spec["axes"]["y"] = {"label": "Fit Influence", "unit": "", "scale": "linear", "value": "influence"}
    spec["series"] = [{"name": "Observations", "kind": "bars", "x": [0, 2],
                       "y": [0.25, 0.75], "width": 0.8,
                       "style": {"orientation": "horizontal", "labels": ["1951", "1953"]}}]
    fig = render_plot(spec)
    ax = fig.axes[0]
    np.testing.assert_allclose([bar.get_width() for bar in ax.patches], [0.25, 0.75])
    np.testing.assert_allclose([bar.get_y() + bar.get_height() / 2 for bar in ax.patches], [0, 2])
    assert ax.get_xlabel() == "Fit Influence"
    assert ax.get_ylabel() == "Observation"
    assert [tick.get_text() for tick in ax.get_yticklabels()] == ["1951", "1953"]


def test_empty_plot_with_omission_renders_axes():
    spec = valid_spec()
    spec["series"] = []
    spec["omissions"] = ["insufficient sample"]
    fig = render_plot(spec)
    assert fig.axes[0].get_title() == "Frequency"
    assert fig.bestfit_omissions == ["insufficient sample"]


def test_area_and_bar_face_and_edge_colors_are_applied():
    spec = valid_spec()
    spec["axes"]["x"].update(scale="linear", value="x")
    spec["axes"]["y"].update(scale="linear", value="y")
    spec["series"] = [
        {"name": "Area", "kind": "area", "x": [1, 2], "y": [3, 5], "yLower": [0, 0], "yUpper": [3, 5], "style": {"facecolor": "cyan", "edgecolor": "blue", "alpha": 1}},
        {"name": "Bars", "kind": "bars", "x": [3, 4], "y": [2, 4], "width": 0.5, "style": {"facecolor": "green", "edgecolor": "black", "alpha": 1}},
    ]
    fig = render_plot(spec)
    area = fig.axes[0].collections[0]
    bar = fig.axes[0].patches[0]
    np.testing.assert_allclose(area.get_facecolor()[0], to_rgba("cyan"))
    np.testing.assert_allclose(area.get_edgecolor()[0], to_rgba("blue"))
    np.testing.assert_allclose(bar.get_facecolor(), to_rgba("green"))
    np.testing.assert_allclose(bar.get_edgecolor(), to_rgba("black"))


def test_horizontal_aep_band_uses_supplied_response_and_reversed_bounds():
    spec = valid_spec()
    spec["axes"]["y"].update(scale="linear")
    spec["series"][0].update(kind="band", x=[0.5, 0.1], y=[10, 20], xLower=[0.4, 0.08], xUpper=[0.6, 0.12], interval={"kind": "credible", "level": 0.9})
    fig = render_plot(spec)
    vertices = fig.axes[0].collections[0].get_paths()[0].vertices
    assert set(vertices[:, 1]) >= {10, 20}
    assert np.isclose(vertices[:, 0].min(), -0.2533471031357997, atol=1e-12)
    assert np.isclose(vertices[:, 0].max(), 1.4050715603096329, atol=1e-12)
