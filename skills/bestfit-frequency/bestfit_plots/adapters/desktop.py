"""Display an exported OxyPlot snapshot without re-estimating its source project.

The desktop exporter owns all statistical coordinates. This adapter translates
date encoding, colors, grids and uncertainty orientation into portable PlotSpec.
Label corrections are recorded separately from the immutable desktop snapshot.
"""
from datetime import datetime, timedelta
import math
import re

from .common import area, axis, clean, line, plot
from ..spec import validate_spec

SCALES = {"LinearAxis": "linear", "CategoryAxis": "linear", "LogarithmicAxis": "log",
          "NormalProbabilityAxis": "normal_probability", "DateTimeAxis": "date"}
KINDS = {"LineSeries": "line", "ScatterSeries": "scatter", "ScatterErrorSeries": "scatter",
         "AreaSeries": "area", "HistogramSeries": "bars", "BarSeries": "bars",
         "HeatMapSeries": "heatmap", "ContourSeries": "contour"}


def _date(value):
    return None if value is None else (datetime(1899, 12, 30) + timedelta(days=value)).isoformat()


def _color(value, fallback="black"):
    if not value or value in {"#00000001", "Automatic", "Undefined"}:
        return fallback
    # OxyPlot exports alpha first; Matplotlib expects alpha last.
    return "#" + value[3:] + value[1:3] if value.startswith("#") and len(value) == 9 else value


def _style(item, kind):
    s = item.get("style") or {}
    color = _color(s.get("MarkerFill") if kind == "scatter" else s.get("Color"))
    style = {"color": color, "legend": item.get("RenderInLegend", True),
             "visible": item.get("IsVisible", True), "linewidth": s.get("StrokeThickness", 1.2)}
    style["linestyle"] = {"Solid": "-", "Dash": "--", "Dot": ":", "DashDot": "-.", "None": "None"}.get(s.get("LineStyle"), "-")
    style["marker"] = {"Circle": "o", "Cross": "x", "Plus": "+", "Diamond": "D", "Square": "s",
                       "Triangle": "^", "None": "None"}.get(s.get("MarkerType"), "o" if kind == "scatter" else "None")
    if kind == "scatter":
        style.update(size=(2 * s.get("MarkerSize", 3)) ** 2,
                     edgecolor=_color(s.get("MarkerStroke")), boundsColor=_color(s.get("ErrorBarColor")))
        if style["marker"] in {"x", "+"}:
            style["color"] = _color(s.get("MarkerStroke"), color)
    if kind in {"area", "bars"}:
        style.update(facecolor=_color(s.get("Fill") or s.get("FillColor"), "#688caf4b"),
                     edgecolor=_color(s.get("StrokeColor") or s.get("Color"), "none"), alpha=None)
    return style


def desktop_plot(snapshot):
    """Return one validated plot; fail explicitly on unsupported visible geometry."""
    if snapshot.get("formatVersion") != 1 or not snapshot.get("sourceSha256"):
        raise ValueError("A versioned desktop snapshot with sourceSha256 is required")
    app_axes = snapshot["axes"]
    bottom = next(a for a in app_axes if a["position"] == "Bottom")
    left = next(a for a in app_axes if a["position"] == "Left")
    horizontal = left["type"] == "CategoryAxis"
    raw_x, raw_y = (left, bottom) if horizontal else (bottom, left)
    def make_axis(raw):
        if raw["type"] not in SCALES:
            raise ValueError(f"Unsupported desktop axis {raw['type']}")
        scale = SCALES[raw["type"]]
        result = axis(raw.get("Title") or ("Observations" if horizontal else "Value"), scale,
                      "aep" if scale == "normal_probability" else "date" if scale == "date" else "value")
        for key in ("Minimum", "Maximum"):
            value = raw.get(key)
            if value is None:
                value = raw.get("Actual" + key)
            if value is not None:
                result[key.lower()] = _date(value) if scale == "date" else value
        # AEP's display transform decreases; OxyPlot's default probability axis
        # reverses raw probabilities so rare events remain on the right.
        probability = scale == "normal_probability"
        start = raw.get("StartPosition", 1. if probability else 0.)
        end = raw.get("EndPosition", 0. if probability else 1.)
        result["reversed"] = (start > end) != probability
        return result
    xaxis, yaxis = make_axis(raw_x), make_axis(raw_y)
    plot_id = snapshot["plotId"]
    corrections = list(snapshot.get("displayCorrections") or [])
    if plot_id == "fitting.qq":
        xaxis["label"], yaxis["label"] = "Quantile (Data)", "Quantile (Model)"
        corrections.append("Q-Q labels identify the exported data X and model Y coordinates.")
    if plot_id == "time_series_analysis.residuals":
        xaxis.update(label="Date", scale="date", value="date")
        for key in ("minimum", "maximum"):
            if key in xaxis and not isinstance(xaxis[key], str):
                xaxis[key] = _date(xaxis[key])
        corrections.append("OLE observation dates are displayed on a date axis instead of the legacy response-unit axis.")
    b17c = any(value in snapshot.get("analysisKind", "").lower() for value in ("b17c", "bulletin17c")) or plot_id.startswith("b17c.")
    predictive = plot_id in {"rating.curve", "time_series_analysis.series"}
    if predictive:
        corrections.append("Prediction intervals include residual/process variation as well as parameter uncertainty; exported bounds are unchanged.")
    seasonal_ranges = plot_id == "time_series_data.seasonality" and any(
        raw["type"] == "AreaSeries" for raw in snapshot["series"])
    if seasonal_ranges:
        corrections.append("Monthly sample percentile bands are labeled observed ranges, not confidence intervals for an estimated statistic.")
    if b17c and any("Quantile Prior" in (raw.get("Title") or "") for raw in snapshot["series"]):
        corrections.append("GMM quantile information is labeled Quantile Penalty rather than Quantile Prior.")
    def label(value):
        if predictive:
            value = value.replace("Credible Interval", "Prediction Interval").replace("Confidence Interval", "Prediction Interval")
        if seasonal_ranges:
            value = value.replace("Confidence Interval", "Observed Range")
        if b17c:
            return value.replace("Posterior", "Uncertainty").replace("Credible", "Confidence").replace("Quantile Prior", "Quantile Penalty")
        return value
    series, omissions, names = [], [], set()
    for raw in snapshot["series"]:
        if not raw.get("IsVisible", True):
            continue
        kind = KINDS.get(raw["type"])
        if kind is None:
            raise ValueError(f"Unsupported desktop series {raw['type']}")
        points = raw.get("points") or []
        items = raw.get("actualItems") or raw.get("items") or []
        grid = raw.get("grid")
        if not (points or items or grid):
            continue
        name = label(raw.get("Title") or raw.get("name") or raw["type"])
        base = name
        count = 2
        while name in names:
            name = f"{base} ({count})"
            count += 1
        names.add(name)
        item = {"name": name, "kind": kind, "style": _style(raw, kind)}
        if points:
            item.update(x=clean([p.get("X") for p in points]), y=clean([p.get("Y") for p in points]))
        if kind == "scatter":
            for dimension in ("X", "Y"):
                for suffix, prefix in (("Lower", "LowerError"), ("Upper", "UpperError")):
                    values = clean([p.get(prefix + dimension) for p in points])
                    if any(v is not None for v in values):
                        item[dimension.lower() + suffix] = values
            if any(key in item for key in ("xLower", "yLower")):
                item["interval"] = {"kind": "penalty" if b17c and "Quantile Penalty" in name else "prior" if "Prior" in name else "measurement"}
        elif kind == "area":
            other = raw.get("points2") or []
            if len(points) != len(other):
                raise ValueError(f"Unaligned area boundaries for {name}")
            x2, y2 = clean([p.get("X") for p in other]), clean([p.get("Y") for p in other])
            probability = re.search(r"([\d.]+)%", name)
            interval_kind = "prediction" if predictive and "Prediction Interval" in name else "confidence" if b17c or "Confidence" in name else "credible" if "Credible" in name else None
            if probability and interval_kind:
                item.update(kind="band", interval={"kind": interval_kind, "level": float(probability[1]) / 100})
            if item["x"] == x2:
                item.update(yLower=item["y"], yUpper=y2)
            elif item["y"] == y2 and item["kind"] == "band":
                item.update(xLower=item["x"], xUpper=x2)
            else:
                raise ValueError(f"Unsupported nonaligned area orientation for {name}")
        elif kind == "bars":
            if raw["type"] == "BarSeries":
                item.update(x=[p["CategoryIndex"] for p in items], y=clean([p["Value"] for p in items]), width=.7)
                labels = left.get("labels") or []
                item["style"].update(orientation="horizontal", labels=[labels[p["CategoryIndex"]] for p in items])
            else:
                item.update(x=[(p["RangeStart"] + p["RangeEnd"]) / 2 for p in items],
                            y=[p["Area"] / (p["RangeEnd"] - p["RangeStart"]) for p in items],
                            width=[p["RangeEnd"] - p["RangeStart"] for p in items])
        elif kind in {"heatmap", "contour"}:
            matrix = grid["data"]
            nx, ny = len(matrix), len(matrix[0])
            def coordinates(key, lo, hi, count):
                return grid.get(key) or [grid[lo] + i * (grid[hi] - grid[lo]) / max(1, count - 1) for i in range(count)]
            item.update(x=coordinates("columnCoordinates", "x0", "x1", nx),
                        y=coordinates("rowCoordinates", "y0", "y1", ny),
                        z=[clean(row) for row in zip(*matrix)])
            if kind == "contour":
                levels = sorted(set(raw.get("contourLevels") or []))
                item["style"].update(colors="black", levels=levels or None, legend=False)
            else:
                item["style"].update(colorbar=True, colorbarLabel="Bin probability" if "pair_heatmap" in plot_id else "Value")
        for key in ("x", "xLower", "xUpper"):
            if key in item and xaxis["scale"] == "date":
                item[key] = [_date(value) for value in item[key]]
        series.append(item)
    for annotation in snapshot.get("annotations", []):
        g = annotation["geometry"]
        if annotation["type"] != "LineAnnotation":
            raise ValueError(f"Unsupported annotation {annotation['type']}")
        lo, hi = bottom.get("ActualMinimum"), bottom.get("ActualMaximum")
        if lo is None or hi is None:
            continue
        x = [lo, hi]
        if g.get("Type") == "LinearEquation":
            y = [g["Slope"] * x + g["Intercept"] for x in (lo, hi)]
        elif g.get("Type") == "Horizontal":
            y = [g["Y"], g["Y"]]
        elif g.get("Type") == "Vertical":
            x = [g["X"], g["X"]]
            y = [left["ActualMinimum"], left["ActualMaximum"]]
        else:
            raise ValueError(f"Unsupported line annotation {g.get('Type')}")
        x = [_date(v) for v in x] if xaxis["scale"] == "date" else x
        series.append(line(annotation.get("text") or "Reference", x, y,
                           color=_color(annotation.get("style", {}).get("Color")), linestyle="--", legend=True))
    legacy = snapshot.get("legacySavedFrequency")
    if legacy:
        if not b17c or plot_id != "b17c.frequency" or not series or any(s['kind'] != 'scatter' for s in series):
            raise ValueError("Legacy saved curves require an observation-only B17C frequency snapshot")
        p, point, expected = legacy['probabilities'], legacy['point'], legacy['expected']
        lower, upper, width = legacy['lower'], legacy['upper'], legacy['width']
        if len(p) < 2 or any(len(v) != len(p) for v in (point, expected, lower, upper)):
            raise ValueError("Legacy saved frequency arrays are misaligned")
        if not 0 < width < 1 or any(not 0 < v < 1 for v in p) or any(
                not math.isfinite(v) for values in (p, point, expected, lower, upper) for v in values):
            raise ValueError("Legacy saved frequency arrays or interval width are invalid")
        if any(lo > hi for lo, hi in zip(lower, upper)):
            raise ValueError("Legacy saved confidence bounds are inverted")
        series = [area(f"{100*width:g}% Confidence Intervals", p, lower, upper,
                       {'kind': 'confidence', 'level': width}, color='#353b7a', edgecolor='#353b7a',
                       linewidth=1, facecolor='#688caf4b', alpha=None),
                  line('Expected Probability', p, expected, color='blue', linestyle='--'),
                  line('Computed', p, point, color='black')] + series
        # Extend display limits only; keep every persisted ordinate unmodified.
        positive = [v for values in (point, expected, lower, upper) for v in values if v > 0]
        if positive and yaxis['scale'] == 'log':
            yaxis['minimum'] = min(yaxis.get('minimum', min(positive)), 10**math.floor(math.log10(min(positive))))
            yaxis['maximum'] = max(yaxis.get('maximum', max(positive)), 10**math.ceil(math.log10(max(positive))))
        corrections.append("Original legacy GMM curves and bounds are displayed directly from saved arrays because the current app loader omits this older result format; no fit, interpolation or parameter conversion was performed.")
    if not series:
        omissions.append("The desktop source contains no populated visible series.")
    spec = plot(plot_id, {"kind": "saved", "id": snapshot["element"], "runId": "sha256:" + snapshot["sourceSha256"]},
                label(snapshot["title"] or snapshot["element"]), xaxis, yaxis, series,
                variant=snapshot["variant"], omissions=omissions)
    spec["desktopSource"] = {k: snapshot[k] for k in ("project", "element", "runtime", "appFactory", "appPopulation") if k in snapshot}
    spec["displayCorrections"] = corrections
    if legacy:
        spec['desktopSource']['legacyFrequencyColumns'] = list(legacy['columns'])
    if plot_id == "shared_diagnostics.trace":
        spec["legendLocation"] = "outside right"
        corrections.append("Chain legend placed outside the axes so diagnostic excursions remain visible.")
    return validate_spec(spec)
