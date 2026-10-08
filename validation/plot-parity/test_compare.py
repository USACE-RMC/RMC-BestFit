"""Independent geometry comparator behavior on explicit app-like examples."""

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
from compare import compare_geometry  # noqa: E402


def app(series, x_axis="DateTimeAxis", y_axis="LinearAxis", annotations=None):
    return {
        "plotId": "time_series_data.series", "variant": "default", "title": "Time Series",
        "axes": [{"type": x_axis, "Title": "Date", "position": "Bottom"},
                 {"type": y_axis, "Title": "Flow (cfs)", "position": "Left"}],
        "series": series, "annotations": annotations or [],
    }


def spec(series, x_scale="date"):
    return {
        "plotId": "time_series_data.series", "variant": "default", "title": "Time Series",
        "axes": {"x": {"label": "Date", "unit": "", "scale": x_scale, "value": "date"},
                 "y": {"label": "Flow", "unit": "cfs", "scale": "linear", "value": "value"}},
        "series": series,
    }


def test_date_coordinates_use_oadate_and_extra_app_points_are_not_ignored():
    model = app([{"name": "TimeSeriesLine", "Title": "Time Series", "type": "LineSeries",
                  "IsVisible": True, "points": [{"X": 17899, "Y": 112}], "points2": []}])
    portable = spec([{"name": "Time Series", "kind": "line", "x": ["1949-01-01"], "y": [112]}])
    assert compare_geometry(model, portable)["ok"]
    model["series"][0]["points"].append({"X": 17930, "Y": 118})
    result = compare_geometry(model, portable)
    assert not result["ok"]
    assert any("length" in issue["message"] for issue in result["differences"])


def test_histogram_compares_every_bin_edge_and_height():
    model = app([{"name": "Histogram", "Title": "Histogram", "type": "HistogramSeries",
                  "IsVisible": True, "actualItems": [
                      {"RangeStart": 0, "RangeEnd": 1, "Value": .25},
                      {"RangeStart": 1, "RangeEnd": 2, "Value": .75}],
                  "points": []}], x_axis="LinearAxis")
    model["axes"][0]["Title"] = "Date"
    portable = spec([{"name": "Histogram", "kind": "bars", "x": [.5, 1.5],
                      "y": [.25, .75], "width": [1, 1]}], x_scale="linear")
    assert compare_geometry(model, portable)["ok"]
    portable["series"][0]["width"][1] = .9
    result = compare_geometry(model, portable)
    assert not result["ok"]
    assert any("RangeEnd" in issue["path"] for issue in result["differences"])


def test_band_bounds_and_grid_transpose_are_checked():
    model = app([{"name": "Interval", "Title": "Interval", "type": "AreaSeries",
                  "IsVisible": True, "points": [{"X": .1, "Y": 8}, {"X": .2, "Y": 9}],
                  "points2": [{"X": .1, "Y": 12}, {"X": .2, "Y": 13}]}], x_axis="LinearAxis")
    model["axes"][0]["Title"] = "AEP"
    portable = spec([{"name": "Interval", "kind": "band", "x": [.1, .2], "y": [10, 11],
                      "yLower": [8, 9], "yUpper": [12, 13]}], x_scale="linear")
    portable["axes"]["x"]["label"] = "AEP"
    assert compare_geometry(model, portable)["ok"]
    portable["series"][0]["yUpper"][1] = 14
    assert any("yUpper" in issue["path"] for issue in compare_geometry(model, portable)["differences"])

    model["series"] = [{"name": "Surface", "Title": "Surface", "type": "HeatMapSeries",
                         "IsVisible": True, "grid": {"data": [[1, 3], [2, 4]],
                                                      "columnCoordinates": [0, 1],
                                                      "rowCoordinates": [10, 20]}}]
    portable["series"] = [{"name": "Surface", "kind": "heatmap", "x": [0, 1],
                            "y": [10, 20], "z": [[1, 2], [3, 4]]}]
    assert compare_geometry(model, portable)["ok"]
    portable["series"][0]["z"][1][1] = 5
    assert any("z" in issue["path"] for issue in compare_geometry(model, portable)["differences"])


def test_hidden_series_and_unmatched_annotation_are_reported():
    model = app([{"name": "Hidden", "Title": "Hidden", "type": "LineSeries",
                  "IsVisible": False, "points": [{"X": 17899, "Y": 5}]}],
                annotations=[{"type": "LineAnnotation", "text": "95% CI",
                              "geometry": {"Type": "Horizontal", "Y": 3}, "style": {}}])
    portable = spec([])
    result = compare_geometry(model, portable)
    assert not result["ok"]
    assert any("annotation" in issue["path"] for issue in result["differences"])
    assert any("hidden" in note.lower() for note in result["notes"])


def test_unique_primitive_fallback_reports_label_and_coordinates():
    model = app([{"name": "TimeSeriesLine", "Title": "Time Series Data", "type": "LineSeries",
                  "IsVisible": True, "points": [{"X": 17899, "Y": 112}]}])
    portable = spec([{"name": "Time Series", "kind": "line", "x": ["1949-01-01"], "y": [111]}])
    result = compare_geometry(model, portable)
    assert not result["ok"]
    assert any("label" in issue["path"] for issue in result["differences"])
    assert any(".y[0]" in issue["path"] for issue in result["differences"])


def test_date_histogram_and_scatter_error_display_cutoff():
    model = app([{"name": "Month", "Title": "Month", "type": "HistogramSeries", "IsVisible": True,
                  "actualItems": [{"RangeStart": 17899, "RangeEnd": 17929, "Value": .5}]}])
    portable = spec([{"name": "Month", "kind": "bars", "x": ["1949-01-16"],
                      "y": [.5], "width": [30]}])
    assert compare_geometry(model, portable)["ok"]


def test_horizontal_bar_axes_categories_and_values_match_app_geometry():
    model = app([{"name": "", "Title": "Observations", "type": "BarSeries", "IsVisible": True,
                  "actualItems": [{"CategoryIndex": -1, "Value": .25},
                                  {"CategoryIndex": 2, "Value": .75}]}],
                x_axis="LinearAxis", y_axis="CategoryAxis")
    model["axes"] = [
        {"type": "CategoryAxis", "Title": "Observation", "position": "Left",
         "labels": ["1951", "unused", "1953"]},
        {"type": "LinearAxis", "Title": "Fit Influence", "position": "Bottom"},
    ]
    portable = spec([{"name": "Observations", "kind": "bars", "x": [0, 2],
                      "y": [.25, .75], "width": .8,
                      "style": {"orientation": "horizontal", "labels": ["1951", "1953"]}}],
                    x_scale="linear")
    portable["axes"]["x"].update(label="Observation", unit="", value="index")
    portable["axes"]["y"].update(label="Fit Influence", unit="", value="influence")
    assert compare_geometry(model, portable)["ok"]

    model = app([{"name": "LowOutlierData", "Title": "Low Outlier Data",
                  "type": "ScatterErrorSeries", "IsVisible": True,
                  "points": [{"X": .2, "Y": 5, "LowerErrorY": 4, "UpperErrorY": 6}],
                  "style": {"MarkerFill": "#00000001", "MarkerStroke": "#ffff0000", "MarkerType": "Cross"}}],
                x_axis="NormalProbabilityAxis", y_axis="LogarithmicAxis")
    model["axes"][0]["Title"] = "AEP"
    portable = spec([{"name": "Low Outlier Data", "kind": "scatter", "x": [.1, .2], "y": [0, 5],
                      "yLower": [0, 4], "yUpper": [0, 6],
                      "style": {"color": "red", "marker": "x", "minimumPositive": 1e-16}}], x_scale="normal_probability")
    portable["axes"]["x"].update(label="AEP", value="aep")
    portable["axes"]["y"]["scale"] = "log"
    assert compare_geometry(model, portable)["ok"]
