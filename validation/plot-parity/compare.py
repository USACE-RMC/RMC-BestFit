"""Compare independent app OxyPlot geometry with a portable Python PlotSpec.

No values are estimated here. Missing/extra visible series, points, bins, grid
cells, and annotations are differences. The only coordinate conversion is the
documented OxyPlot DateTimeAxis OADate representation (1899-12-30 epoch).
"""

from __future__ import annotations

import argparse
from datetime import datetime, timezone
import gzip
import json
import math
from pathlib import Path


OA_EPOCH = datetime(1899, 12, 30)
APP_KINDS = {
    "LineSeries": "line", "ScatterSeries": "scatter", "ScatterErrorSeries": "scatter",
    "AreaSeries": "area", "HistogramSeries": "bars", "BarSeries": "bars",
    "HeatMapSeries": "heatmap", "ContourSeries": "contour",
}
AXIS_SCALES = {
    "LinearAxis": "linear", "LogarithmicAxis": "log",
    "NormalProbabilityAxis": "normal_probability", "DateTimeAxis": "date",
    "CategoryAxis": "linear",
}


def _oadate(value):
    if value is None:
        return None
    if isinstance(value, str):
        dt = datetime.fromisoformat(value.replace("Z", "+00:00"))
        if dt.tzinfo is not None:
            dt = dt.astimezone(timezone.utc).replace(tzinfo=None)
        return (dt - OA_EPOCH).total_seconds() / 86400
    return value


def _close(a, b, atol, rtol):
    if a is None or b is None:
        return a is None and b is None
    return math.isclose(float(a), float(b), abs_tol=atol, rel_tol=rtol)


def _compare_values(differences, path, expected, actual, atol, rtol):
    if not _close(expected, actual, atol, rtol):
        differences.append({"path": path, "message": "coordinate differs",
                            "app": actual, "python": expected})


def _compare_vector(differences, path, expected, actual, atol, rtol):
    if len(expected) != len(actual):
        differences.append({"path": path, "message": "length differs",
                            "app": len(actual), "python": len(expected)})
    for i, (want, got) in enumerate(zip(expected, actual)):
        _compare_values(differences, f"{path}[{i}]", want, got, atol, rtol)


def _axis_title(axis):
    label = axis.get("label", "").strip()
    unit = axis.get("unit", "").strip()
    return " ".join((label + (f" ({unit})" if unit else "")).split())


def _app_axis(app, position):
    return next((axis for axis in app.get("axes", []) if axis.get("position") == position), None)


def _label(series):
    return series.get("Title") or series.get("name") or ""


def _color(color):
    if color is None:
        return None
    color = str(color)
    if color.startswith("#") and len(color) == 9:  # OxyPlot uses #AARRGGBB.
        color = "#" + color[3:]
    try:
        from matplotlib.colors import to_rgb
        return tuple(to_rgb(color))
    except (ImportError, ValueError):
        return color.lower()


def _compare_style(differences, notes, path, app_series, spec_series, atol, rtol):
    app_style = app_series.get("style") or {}
    style = spec_series.get("style") or {}
    kind = spec_series["kind"]
    if "legend" in style and app_series.get("RenderInLegend") is not None \
            and bool(style["legend"]) != bool(app_series["RenderInLegend"]):
        differences.append({"path": path + ".legend", "message": "legend visibility differs",
                            "app": app_series["RenderInLegend"], "python": style["legend"]})
    color_key = (("MarkerStroke" if style.get("marker") == "x" else "MarkerFill") if kind == "scatter" else
                 "FillColor" if kind == "bars" else
                 "Fill" if kind in {"band", "area"} else "Color")
    expected_color = style.get("facecolor", style.get("color")) if kind in {"bars", "band", "area"} else style.get("color")
    if expected_color and color_key in app_style and _color(expected_color) != _color(app_style[color_key]):
        differences.append({"path": path + ".color", "message": "visible color differs",
                            "app": app_style[color_key], "python": expected_color})
    if expected_color and color_key not in app_style:
        notes.append(f"{path}: app {color_key} was not exported; color not compared")
    marker_map = {"Circle": "o", "Cross": "x", "Diamond": "D", "Square": "s", "Triangle": "^", "None": "None"}
    if "marker" in style and "MarkerType" in app_style:
        actual = marker_map.get(app_style["MarkerType"], app_style["MarkerType"])
        if style["marker"] != actual:
            differences.append({"path": path + ".marker", "message": "marker differs",
                                "app": actual, "python": style["marker"]})
    line_map = {"Solid": "-", "Dash": "--", "Dot": ":", "DashDot": "-."}
    if "linestyle" in style and "LineStyle" in app_style:
        actual = line_map.get(app_style["LineStyle"], app_style["LineStyle"])
        if style["linestyle"] != actual:
            differences.append({"path": path + ".linestyle", "message": "line style differs",
                                "app": actual, "python": style["linestyle"]})
    if "linewidth" in style and "StrokeThickness" in app_style:
        _compare_values(differences, path + ".linewidth", style["linewidth"], app_style["StrokeThickness"], atol, rtol)


def _compare_series(differences, notes, path, app_series, spec_series, xscale, yscale, atol, rtol):
    kind = spec_series["kind"]
    app_kind = APP_KINDS.get(app_series.get("type"))
    if app_kind != kind and not (app_kind == "area" and kind == "band"):
        differences.append({"path": path + ".kind", "message": "primitive differs",
                            "app": app_series.get("type"), "python": kind})
        return
    _compare_style(differences, notes, path + ".style", app_series, spec_series, atol, rtol)
    if kind in {"line", "scatter"}:
        points = app_series.get("points") or []
        keep = list(range(len(spec_series["x"])))
        cutoff = spec_series.get("style", {}).get("minimumPositive", 0)
        if kind == "scatter" and yscale == "log":
            keep = [i for i in keep if spec_series["y"][i] is not None and spec_series["y"][i] > cutoff]
            if len(keep) != len(spec_series["x"]):
                notes.append(f"{path}: {len(spec_series['x']) - len(keep)} log-display points excluded")
        x = [_oadate(spec_series["x"][i]) if xscale == "date" else spec_series["x"][i] for i in keep]
        _compare_vector(differences, path + ".x", x, [p.get("X") for p in points], atol, rtol)
        _compare_vector(differences, path + ".y", [spec_series["y"][i] for i in keep], [p.get("Y") for p in points], atol, rtol)
        for field, app_field in (("xLower", "LowerErrorX"), ("xUpper", "UpperErrorX"),
                                 ("yLower", "LowerErrorY"), ("yUpper", "UpperErrorY")):
            if field in spec_series:
                wanted = [spec_series[field][i] for i in keep]
                if field.startswith("x") and xscale == "date":
                    wanted = [_oadate(value) for value in wanted]
                _compare_vector(differences, path + "." + field, wanted,
                                [p.get(app_field) for p in points], atol, rtol)
        return
    if kind in {"band", "area"}:
        lower, upper = app_series.get("points") or [], app_series.get("points2") or []
        if "xLower" in spec_series:
            _compare_vector(differences, path + ".xLower", spec_series["xLower"], [p.get("X") for p in lower], atol, rtol)
            _compare_vector(differences, path + ".xUpper", spec_series["xUpper"], [p.get("X") for p in upper], atol, rtol)
            _compare_vector(differences, path + ".y", spec_series["y"], [p.get("Y") for p in lower], atol, rtol)
            notes.append(f"{path}: band x center is not represented in app AreaSeries; bounds and y compared")
        else:
            x = [_oadate(value) if xscale == "date" else value for value in spec_series["x"]]
            _compare_vector(differences, path + ".xLowerPath", x, [p.get("X") for p in lower], atol, rtol)
            _compare_vector(differences, path + ".xUpperPath", x, [p.get("X") for p in upper], atol, rtol)
            _compare_vector(differences, path + ".yLower", spec_series["yLower"], [p.get("Y") for p in lower], atol, rtol)
            _compare_vector(differences, path + ".yUpper", spec_series["yUpper"], [p.get("Y") for p in upper], atol, rtol)
            notes.append(f"{path}: band y center is not represented in app AreaSeries; bounds and x compared")
        return
    if kind == "bars":
        items = app_series.get("actualItems") or app_series.get("items") or []
        _compare_vector(differences, path + ".height", spec_series["y"], [item.get("Value") for item in items], atol, rtol)
        if app_series.get("type") == "HistogramSeries":
            widths = spec_series["width"]
            if not isinstance(widths, list):
                widths = [widths] * len(spec_series["x"])
            _compare_vector(differences, path + ".RangeStart",
                            [_oadate(x) - width / 2 for x, width in zip(spec_series["x"], widths)],
                            [item.get("RangeStart") for item in items], atol, rtol)
            _compare_vector(differences, path + ".RangeEnd",
                            [_oadate(x) + width / 2 for x, width in zip(spec_series["x"], widths)],
                            [item.get("RangeEnd") for item in items], atol, rtol)
        else:
            category = [item.get("CategoryIndex", i) if item.get("CategoryIndex", i) >= 0 else i
                        for i, item in enumerate(items)]
            _compare_vector(differences, path + ".category", spec_series["x"], category, atol, rtol)
            notes.append(f"{path}: categorical bar width is presentation geometry, not a numeric app bin bound")
        return
    if kind in {"heatmap", "contour"}:
        grid = app_series.get("grid") or {}
        data = grid.get("data") or []
        expected = spec_series["z"]
        actual = [list(row) for row in zip(*data)] if data else []  # OxyPlot Data[x,y] -> PlotSpec z[y][x].
        if len(expected) != len(actual) or any(len(a) != len(b) for a, b in zip(expected, actual)):
            differences.append({"path": path + ".z", "message": "matrix shape differs",
                                "app": [len(actual), len(actual[0]) if actual else 0],
                                "python": [len(expected), len(expected[0]) if expected else 0]})
        for row, (want, got) in enumerate(zip(expected, actual)):
            _compare_vector(differences, f"{path}.z[{row}]", want, got, atol, rtol)
        for field, app_field in (("x", "columnCoordinates"), ("y", "rowCoordinates")):
            coordinates = grid.get(app_field)
            if coordinates is not None:
                _compare_vector(differences, path + "." + field, spec_series[field], coordinates, atol, rtol)
            else:
                notes.append(f"{path}: app {app_field} not exported; grid endpoints need visual review")
        if kind == "contour" and spec_series.get("style", {}).get("levels") is not None:
            levels = app_series.get("contourLevels")
            if levels is None:
                differences.append({"path": path + ".levels", "message": "configured app contour levels unavailable"})
                levels = []
            _compare_vector(differences, path + ".levels", sorted(spec_series["style"]["levels"]), levels, atol, rtol)
        if kind == "contour":
            notes.append(f"{path}: app-rendered contour paths have no direct PlotSpec field; grid and levels compared")


def _annotation_matches(note, series, xscale, atol, rtol):
    if series.get("kind") != "line" or not series.get("x") or not series.get("y"):
        return False
    geometry = note.get("geometry") or {}
    kind = geometry.get("Type")
    x = [_oadate(v) if xscale == "date" else v for v in series["x"]]
    y = series["y"]
    if kind == "Horizontal":
        return all(_close(v, geometry.get("Y"), atol, rtol) for v in y)
    if kind == "Vertical":
        return all(_close(v, geometry.get("X"), atol, rtol) for v in x)
    if kind == "LinearEquation":
        return all(_close(v, geometry.get("Slope", 0) * u + geometry.get("Intercept", 0), atol, rtol)
                   for u, v in zip(x, y))
    return False


def compare_geometry(app, spec, *, atol=1e-10, rtol=1e-8, name_map=None):
    """Return a complete, machine-readable parity report for two loaded objects."""
    differences, notes = [], []
    name_map = name_map or {}
    for key in ("plotId", "variant"):
        if app.get(key) != spec.get(key):
            differences.append({"path": key, "message": "identity differs", "app": app.get(key), "python": spec.get(key)})
    if " ".join(str(app.get("title", "")).split()) != " ".join(str(spec.get("title", "")).split()):
        differences.append({"path": "title", "message": "title differs", "app": app.get("title"), "python": spec.get("title")})
    horizontal_bars = any(item.get("kind") == "bars"
                          and item.get("style", {}).get("orientation") == "horizontal"
                          and item.get("style", {}).get("visible", True)
                          for item in spec.get("series", []))
    for side, position in (("x", "Bottom"), ("y", "Left")):
        axis = _app_axis(app, position)
        logical_side = ({"x": "y", "y": "x"}[side] if horizontal_bars else side)
        wanted = spec.get("axes", {}).get(logical_side, {})
        if axis is None:
            differences.append({"path": f"axes.{side}", "message": "app axis missing"})
            continue
        scale = AXIS_SCALES.get(axis.get("type"), axis.get("type"))
        if scale != wanted.get("scale"):
            differences.append({"path": f"axes.{side}.scale", "message": "axis scale differs",
                                "app": scale, "python": wanted.get("scale")})
        if " ".join(str(axis.get("Title", "")).split()) != _axis_title(wanted):
            differences.append({"path": f"axes.{side}.title", "message": "axis title differs",
                                "app": axis.get("Title"), "python": _axis_title(wanted)})
    if horizontal_bars:
        category_axis = _app_axis(app, "Left") or {}
        app_labels = category_axis.get("labels") or []
        expected_labels = {}
        for item in spec.get("series", []):
            if item.get("kind") == "bars" and item.get("style", {}).get("orientation") == "horizontal":
                expected_labels.update(zip(item["x"], item.get("style", {}).get("labels", [])))
        for position, label in expected_labels.items():
            actual = app_labels[int(position)] if int(position) < len(app_labels) else None
            if actual != label:
                differences.append({"path": f"axes.y.labels[{position}]", "message": "category label differs",
                                    "app": actual, "python": label})
    active = [item for item in app.get("series", []) if item.get("IsVisible", True)]
    hidden = [item for item in app.get("series", []) if not item.get("IsVisible", True)]
    if hidden:
        notes.append(f"{len(hidden)} hidden app series excluded from visible geometry comparison")
    portable = [item for item in spec.get("series", []) if item.get("style", {}).get("visible", True)]
    if len(portable) != len(spec.get("series", [])):
        notes.append("hidden PlotSpec series excluded from visible geometry comparison")
    unused_app = list(range(len(active)))
    unused_annotations = list(range(len(app.get("annotations", []))))
    matches = []
    xscale = spec.get("axes", {}).get("x", {}).get("scale", "linear")
    yscale = spec.get("axes", {}).get("y", {}).get("scale", "linear")
    for portable_series in portable:
        name = portable_series["name"]
        expected_app_name = name_map.get(name, name)
        candidates = [i for i in unused_app if expected_app_name in {active[i].get("name"), active[i].get("Title")}]
        if len(candidates) > 1:
            differences.append({"path": f"series.{name}", "message": "ambiguous app series label",
                                "app": [active[i].get("name") for i in candidates]})
            continue
        if candidates:
            i = candidates[0]
            unused_app.remove(i)
            matches.append({"python": name, "app": active[i].get("name")})
            _compare_series(differences, notes, f"series.{name}", active[i], portable_series, xscale, yscale, atol, rtol)
            continue
        matching_annotations = [i for i in unused_annotations
                                if _annotation_matches(app["annotations"][i], portable_series, xscale, atol, rtol)]
        if matching_annotations:
            i = next((i for i in matching_annotations if app["annotations"][i].get("text") == expected_app_name),
                     matching_annotations[0])
            annotation = app["annotations"][i]
            if annotation.get("text") and annotation["text"] != expected_app_name:
                differences.append({"path": f"series.{name}.label", "message": "annotation label differs",
                                    "app": annotation["text"], "python": name})
            unused_annotations.remove(i)
            matches.append({"python": name, "appAnnotation": i})
            continue
        kind_matches = [i for i in unused_app if APP_KINDS.get(active[i].get("type")) == portable_series["kind"]
                        or (APP_KINDS.get(active[i].get("type")) == "area" and portable_series["kind"] == "band")]
        if len(kind_matches) == 1:
            i = kind_matches[0]
            unused_app.remove(i)
            differences.append({"path": f"series.{name}.label", "message": "unique primitive label differs",
                                "app": _label(active[i]), "python": name})
            matches.append({"python": name, "app": active[i].get("name"), "matchedBy": "unique primitive"})
            _compare_series(differences, notes, f"series.{name}", active[i], portable_series, xscale, yscale, atol, rtol)
            continue
        differences.append({"path": f"series.{name}", "message": "visible Python series missing from app"})
    for i in unused_app:
        differences.append({"path": f"app.series[{i}]", "message": "visible app series missing from Python",
                            "app": _label(active[i])})
    for i in unused_annotations:
        differences.append({"path": f"app.annotations[{i}]", "message": "app annotation missing from Python",
                            "app": app["annotations"][i]})
    return {"ok": not differences, "differences": differences, "notes": notes, "matchedSeries": matches,
            "appVisibleSeries": len(active), "pythonVisibleSeries": len(portable)}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("app_json", type=Path)
    parser.add_argument("plot_spec_json", type=Path)
    parser.add_argument("--name-map", type=Path, help="Explicit JSON mapping from PlotSpec labels to app names/titles")
    parser.add_argument("--atol", type=float, default=1e-10)
    parser.add_argument("--rtol", type=float, default=1e-8)
    args = parser.parse_args()
    def read_json(path):
        if path.suffix == ".gz":
            with gzip.open(path, "rt", encoding="utf-8") as source:
                return json.load(source)
        return json.loads(path.read_text(encoding="utf-8"))
    app = read_json(args.app_json)
    spec = read_json(args.plot_spec_json)
    names = json.loads(args.name_map.read_text(encoding="utf-8")) if args.name_map else None
    result = compare_geometry(app, spec, atol=args.atol, rtol=args.rtol, name_map=names)
    print(json.dumps(result, indent=2, ensure_ascii=False))
    return 0 if result["ok"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
