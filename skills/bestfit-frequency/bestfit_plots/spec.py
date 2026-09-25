"""Validation for version 1 plot data; numerical estimation belongs to BestFit."""

from datetime import datetime
import math
from numbers import Real


SERIES_KINDS = {"line", "scatter", "band", "area", "bars", "heatmap", "contour"}
INTERVAL_KINDS = {"confidence", "credible", "prediction", "measurement", "prior"}
SCALES = {"linear", "log", "normal_probability", "date"}


def _date(value):
    if not isinstance(value, str):
        raise ValueError("date coordinates must be ISO 8601 strings")
    try:
        return datetime.fromisoformat(value.replace("Z", "+00:00"))
    except ValueError as error:
        raise ValueError("date coordinates must be ISO 8601 strings") from error


def _number(value, field, *, nullable=False):
    if value is None and nullable:
        return
    if isinstance(value, bool) or not isinstance(value, Real) or not math.isfinite(value):
        raise ValueError(f"{field} must contain finite numbers or JSON null gaps")


def _array(series, key, count, *, scale="linear", nullable=False, required=True):
    value = series.get(key)
    if value is None and not required:
        return None
    if not isinstance(value, list) or not value:
        raise ValueError(f"{key} must be a nonempty coordinate array")
    if count is not None and len(value) != count:
        raise ValueError(f"{key} coordinates must align with x")
    for item in value:
        if item is None and nullable:
            continue
        if scale == "date":
            _date(item)
        else:
            _number(item, key, nullable=nullable)
            if scale == "normal_probability" and not (0 < item < 1):
                raise ValueError("AEP coordinates must be strictly between 0 and 1")
    return value


def validate_spec(spec):
    """Return a valid PlotSpec unchanged, or raise ValueError with a field hint.

    Coordinates are supplied by the source. A missing ordinate is JSON null;
    nonfinite JSON extensions are rejected. Log display filtering is rendering only.
    """
    if not isinstance(spec, dict):
        raise ValueError("PlotSpec must be an object")
    if spec.get("version") != 1 or isinstance(spec.get("version"), bool):
        raise ValueError("Unsupported PlotSpec version")
    if not isinstance(spec.get("legendLocation", "best"), str) or spec.get("legendLocation", "best") not in {"best", "upper left", "upper right", "lower left", "lower right"}:
        raise ValueError("legendLocation must name a supported legend position")
    for key in ("plotId", "variant", "title"):
        if not isinstance(spec.get(key), str) or not spec[key].strip():
            raise ValueError(f"{key} must be a nonempty string")
    source = spec.get("source")
    if not isinstance(source, dict):
        raise ValueError("source identity must be an object")
    for key in ("kind", "id", "runId"):
        if not isinstance(source.get(key), str) or not source[key].strip():
            raise ValueError(f"source.{key} must be a nonempty string")
    axes = spec.get("axes")
    if not isinstance(axes, dict):
        raise ValueError("axes must be an object")
    for name in ("x", "y"):
        axis = axes.get(name)
        if not isinstance(axis, dict):
            raise ValueError(f"axes.{name} must be an object")
        for key in ("label", "unit", "scale", "value"):
            if not isinstance(axis.get(key), str):
                raise ValueError(f"axes.{name}.{key} must be a string")
        if axis["scale"] not in SCALES or (name == "y" and axis["scale"] in {"date", "normal_probability"}):
            raise ValueError(f"axes.{name}.scale is unsupported")
        if axis["scale"] == "normal_probability" and axis["value"] != "aep":
            raise ValueError("normal_probability x axis requires aep values")
        if "reversed" in axis and not isinstance(axis["reversed"], bool):
            raise ValueError(f"axes.{name}.reversed must be boolean")
        for key in ("minimum", "maximum"):
            if key not in axis:
                continue
            value = axis[key]
            if axis["scale"] == "date":
                _date(value)
            else:
                _number(value, f"axes.{name}.{key}")
                if axis["scale"] == "log" and value <= 0:
                    raise ValueError("log axis limits must be positive")
                if axis["scale"] == "normal_probability" and not 0 < value < 1:
                    raise ValueError("AEP axis limits must be strictly between 0 and 1")
        if "minimum" in axis and "maximum" in axis:
            lower, upper = axis["minimum"], axis["maximum"]
            if axis["scale"] == "date":
                lower, upper = _date(lower), _date(upper)
            if lower >= upper:
                raise ValueError("axis minimum must be less than maximum")
        if "minimumPositive" in axis:
            _number(axis["minimumPositive"], f"axes.{name}.minimumPositive")
            if axis["minimumPositive"] < 0:
                raise ValueError(f"axes.{name}.minimumPositive must be nonnegative")
    omissions = spec.get("omissions", [])
    if not isinstance(omissions, list) or not all(isinstance(note, str) for note in omissions):
        raise ValueError("omissions must be a list of strings")
    series = spec.get("series")
    if not isinstance(series, list):
        raise ValueError("series must be a list")
    if not series and not omissions:
        raise ValueError("empty series requires documented omissions")
    names = set()
    for index, item in enumerate(series):
        if not isinstance(item, dict):
            raise ValueError(f"series[{index}] must be an object")
        name, kind = item.get("name"), item.get("kind")
        if not isinstance(name, str) or not name.strip() or name in names:
            raise ValueError("series names must be unique and nonempty")
        names.add(name)
        if kind not in SERIES_KINDS:
            raise ValueError(f"series {name}: unsupported kind")
        if not isinstance(item.get("style", {}), dict):
            raise ValueError(f"series {name}: style must be an object")
        for flag in ("visible", "legend"):
            if flag in item.get("style", {}) and not isinstance(item["style"][flag], bool):
                raise ValueError(f"series {name}: style.{flag} must be boolean")
        if "minimumPositive" in item.get("style", {}):
            cutoff = item["style"]["minimumPositive"]
            _number(cutoff, "style.minimumPositive")
            if cutoff < 0:
                raise ValueError("style.minimumPositive must be nonnegative")
        style = item.get("style", {})
        if "orientation" in style and style["orientation"] not in {"vertical", "horizontal"}:
            raise ValueError(f"series {name}: style.orientation is unsupported")
        if ("orientation" in style or "labels" in style) and kind != "bars":
            raise ValueError(f"series {name}: categorical bar style is unsupported for {kind}")
        if "labels" in style and (not isinstance(style["labels"], list)
                                  or not all(isinstance(label, str) for label in style["labels"])):
            raise ValueError(f"series {name}: style.labels must be a list of strings")
        xscale, yscale = axes["x"]["scale"], axes["y"]["scale"]
        x = _array(item, "x", None, scale=xscale, nullable=kind in {"line", "scatter", "band", "area"})
        y = _array(item, "y", None if kind in {"heatmap", "contour"} else len(x), scale=yscale, nullable=kind in {"line", "scatter", "band", "area", "bars"})
        if kind in {"heatmap", "contour"}:
            z = item.get("z")
            if not isinstance(z, list) or len(z) != len(y) or any(not isinstance(row, list) or len(row) != len(x) for row in z):
                raise ValueError(f"series {name}: z matrix must align with x and y")
            for row in z:
                for value in row:
                    _number(value, "z")
        for coordinate, scale, center in (("xLower", xscale, x), ("xUpper", xscale, x), ("yLower", yscale, y), ("yUpper", yscale, y)):
            if coordinate in item:
                if kind not in {"scatter", "band", "area"} or (kind == "area" and coordinate.startswith("x")):
                    raise ValueError(f"series {name}: {coordinate} unsupported for {kind}")
                _array(item, coordinate, len(center), scale=scale, nullable=True)
        for low, high in (("xLower", "xUpper"), ("yLower", "yUpper")):
            if (low in item) != (high in item):
                raise ValueError(f"series {name}: {low} and {high} must be supplied together")
            if low in item:
                scale = xscale if low.startswith("x") else yscale
                for lower, upper in zip(item[low], item[high]):
                    if lower is None or upper is None:
                        continue
                    a = _date(lower) if scale == "date" else lower
                    b = _date(upper) if scale == "date" else upper
                    if a > b:
                        raise ValueError(f"series {name}: lower bound exceeds upper bound")
        if kind == "band" and ("xLower" in item) == ("yLower" in item):
            raise ValueError(f"series {name}: band requires exactly one interval orientation")
        if kind == "area" and ("yLower" not in item or "yUpper" not in item):
            raise ValueError(f"series {name}: area requires aligned yLower and yUpper")
        if kind == "bars":
            if "labels" in style and len(style["labels"]) != len(x):
                raise ValueError(f"series {name}: style.labels must align with x")
            widths = item.get("width")
            if isinstance(widths, list):
                if len(widths) != len(x):
                    raise ValueError("bar widths must align with x")
                for width in widths:
                    _number(width, "width")
                    if width <= 0:
                        raise ValueError("bar width must be positive")
            else:
                _number(widths, "width")
                if widths <= 0:
                    raise ValueError("bar width must be positive")
        notes = item.get("omissions", [])
        if not isinstance(notes, list) or not all(isinstance(note, str) for note in notes):
            raise ValueError(f"series {name}: omissions must be strings")
        interval = item.get("interval")
        if interval is not None:
            if not isinstance(interval, dict) or interval.get("kind") not in INTERVAL_KINDS:
                raise ValueError(f"series {name}: interval.kind is unsupported")
            level = interval.get("level")
            if interval["kind"] in {"confidence", "credible", "prediction"} and level is None:
                raise ValueError(f"series {name}: interval.level is required")
            if level is not None:
                _number(level, "interval.level")
                if not 0 < level < 1:
                    raise ValueError(f"series {name}: interval.level must be between 0 and 1")
        elif kind == "band" or (kind == "scatter" and any(key in item for key in ("xLower", "xUpper", "yLower", "yUpper"))):
            raise ValueError(f"series {name}: interval meaning is required")
    return spec
