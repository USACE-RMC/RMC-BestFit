"""Matplotlib display of source-owned PlotSpec geometry."""

from pathlib import Path
from statistics import NormalDist

import matplotlib
matplotlib.use("Agg")
import matplotlib.dates as mdates
import matplotlib.pyplot as plt
import numpy as np

from .spec import validate_spec


NORMAL = NormalDist()
DEFAULT_COLORS = ("black", "blue", "#4f789a", "#b04b48", "#588157")


def _axis_values(values, scale):
    if scale == "normal_probability":
        return np.array([np.nan if value is None else -NORMAL.inv_cdf(float(value)) for value in values])
    if scale == "date":
        return np.asarray([np.nan if value is None else mdates.date2num(np.datetime64(value.replace("Z", "+00:00"))) for value in values])
    return np.asarray([np.nan if value is None else value for value in values], dtype=float)


def _display(values, scale, name, omissions, coordinate="ordinate", minimum_positive=0):
    output = _axis_values(values, scale)
    if scale == "log":
        hidden = np.isfinite(output) & (output <= minimum_positive)
        if np.any(hidden):
            limit = "nonpositive" if minimum_positive == 0 else f"<= {minimum_positive:g}"
            omissions.append(f"{name}: omitted {np.count_nonzero(hidden)} {limit} log-display {coordinate}; gaps retained.")
            output[hidden] = np.nan
    return output


def _label(axis):
    return axis["label"] + (f" ({axis['unit']})" if axis["unit"] else "")


def render_plot(spec, ax=None):
    """Render a validated PlotSpec and return its Figure; no input is modified."""
    validate_spec(spec)
    own_axes = ax is None
    if own_axes:
        fig, ax = plt.subplots(figsize=(10, 6.5), layout="constrained")
    else:
        fig = ax.figure
    omissions = list(spec.get("omissions", []))
    xscale, yscale = spec["axes"]["x"]["scale"], spec["axes"]["y"]["scale"]
    horizontal_bars = any(item["kind"] == "bars" and item.get("style", {}).get("orientation") == "horizontal"
                          for item in spec["series"] if item.get("style", {}).get("visible", True))
    category_labels = {}
    try:
        fig.patch.set_facecolor("white")
        ax.set_facecolor("white")
        ax.set_title(spec["title"])
        ax.set_xlabel(_label(spec["axes"]["y"] if horizontal_bars else spec["axes"]["x"]))
        ax.set_ylabel(_label(spec["axes"]["x"] if horizontal_bars else spec["axes"]["y"]))
        actual_xscale, actual_yscale = (yscale, xscale) if horizontal_bars else (xscale, yscale)
        if actual_yscale == "log":
            ax.set_yscale("log")
        if actual_xscale == "log":
            ax.set_xscale("log")
        if actual_xscale == "date":
            ax.xaxis_date()
        ax.grid(axis="y", color="#d3d3d3", linewidth=0.6)
        for index, item in enumerate(spec["series"]):
            kind, name = item["kind"], item["name"]
            omissions.extend(f"{name}: {note}" for note in item.get("omissions", []))
            style = item.get("style", {})
            if style.get("visible") is False:
                continue
            legend_label = name if style.get("legend", True) else "_nolegend_"
            color = style.get("color", DEFAULT_COLORS[index % len(DEFAULT_COLORS)])
            xcut = style.get("minimumPositive", spec["axes"]["x"].get("minimumPositive", 0))
            ycut = style.get("minimumPositive", spec["axes"]["y"].get("minimumPositive", 0))
            x = _display(item["x"], xscale, name, omissions, "abscissa", xcut)
            y = _display(item["y"], yscale, name, omissions, minimum_positive=ycut)
            if kind == "line":
                ax.plot(x, y, label=legend_label, color=color, linestyle=style.get("linestyle", "-"), linewidth=style.get("linewidth", 1.2), marker=style.get("marker", "None"))
            elif kind == "scatter":
                valid = np.isfinite(x) & np.isfinite(y)
                marker = style.get("marker", "o")
                edge = {"edgecolor": style["edgecolor"]} if "edgecolor" in style and marker not in {"x", "+"} else {}
                ax.scatter(x[valid], y[valid], label=legend_label, color=color, marker=marker, s=style.get("size", 28), zorder=4, **edge)
                if "xLower" in item or "yLower" in item:
                    bounds_color = style.get("boundsColor", color)
                    for lower_key, upper_key, axis in (("xLower", "xUpper", "x"), ("yLower", "yUpper", "y")):
                        if lower_key not in item:
                            continue
                        scale = xscale if axis == "x" else yscale
                        cutoff = xcut if axis == "x" else ycut
                        lo = _display(item[lower_key], scale, name + " bounds", omissions, "abscissa" if axis == "x" else "ordinate", cutoff)
                        hi = _display(item[upper_key], scale, name + " bounds", omissions, "abscissa" if axis == "x" else "ordinate", cutoff)
                        okay = valid & np.isfinite(lo) & np.isfinite(hi)
                        if np.any(okay):
                            if axis == "x":
                                ax.hlines(y[okay], np.minimum(lo[okay], hi[okay]), np.maximum(lo[okay], hi[okay]), color=bounds_color, linewidth=1, zorder=3)
                            else:
                                ax.vlines(x[okay], lo[okay], hi[okay], color=bounds_color, linewidth=1, zorder=3)
            elif kind in {"band", "area"}:
                fill_style = {"facecolor": style.get("facecolor", color), "edgecolor": style.get("edgecolor", "none"), "linewidth": style.get("linewidth", 1), "alpha": style.get("alpha", 0.29), "label": legend_label}
                if kind == "band" and "xLower" in item:
                    lo = _display(item["xLower"], xscale, name + " lower bound", omissions, "abscissa", xcut)
                    hi = _display(item["xUpper"], xscale, name + " upper bound", omissions, "abscissa", xcut)
                    ax.fill_betweenx(y, np.minimum(lo, hi), np.maximum(lo, hi), **fill_style)
                else:
                    lo = _display(item["yLower"], yscale, name + " lower bound", omissions, minimum_positive=ycut)
                    hi = _display(item["yUpper"], yscale, name + " upper bound", omissions, minimum_positive=ycut)
                    ax.fill_between(x, lo, hi, **fill_style)
            elif kind == "bars":
                bar_style = {"label": legend_label, "color": style.get("facecolor", color),
                             "edgecolor": style.get("edgecolor", "none"), "alpha": style.get("alpha", 0.8)}
                if style.get("orientation") == "horizontal":
                    ax.barh(x, y, height=item["width"], **bar_style)
                    category_labels.update(zip(x, style.get("labels", [str(value) for value in x])))
                else:
                    ax.bar(x, y, width=item["width"], **bar_style)
            elif kind == "heatmap":
                mesh = ax.pcolormesh(x, y, np.asarray(item["z"], dtype=float), shading="nearest", cmap=style.get("cmap", "Blues"))
                if style.get("colorbar"):
                    fig.colorbar(mesh, ax=ax, label=style.get("colorbarLabel", "Value"))
            elif kind == "contour":
                contours = ax.contour(x, y, np.asarray(item["z"], dtype=float), levels=style.get("levels"), colors=style.get("colors", color))
                ax.clabel(contours, inline=True, fontsize=8, fmt="%g")
        if category_labels:
            positions = sorted(category_labels)
            ax.set_yticks(positions, [category_labels[position] for position in positions])
        if actual_xscale == "date" and spec["plotId"].endswith(".seasonality"):
            ax.xaxis.set_major_locator(mdates.MonthLocator())
            ax.xaxis.set_major_formatter(mdates.DateFormatter("%b"))
        for name, data_axis in (("x", spec["axes"]["y" if horizontal_bars else "x"]),
                                ("y", spec["axes"]["x" if horizontal_bars else "y"])):
            get_limits = ax.get_xlim if name == "x" else ax.get_ylim
            set_limits = ax.set_xlim if name == "x" else ax.set_ylim
            bounds = list(get_limits())
            for index, key in enumerate(("minimum", "maximum")):
                if key in data_axis:
                    target = 1 - index if data_axis["scale"] == "normal_probability" else index
                    bounds[target] = _axis_values([data_axis[key]], data_axis["scale"])[0]
            bounds.sort()
            set_limits(bounds[::-1] if data_axis.get("reversed", False) else bounds)
        if actual_xscale == "normal_probability":
            probabilities = [0.999, 0.99, 0.9, 0.5, 0.1, 0.01, 0.001, 0.0001, 0.00001, 0.000001, 0.0000001]
            left, right = ax.get_xlim()
            ticks = [(float(_axis_values([p], xscale)[0]), p) for p in probabilities]
            ticks = [(position, p) for position, p in ticks if min(left, right) <= position <= max(left, right)]
            ax.set_xticks([position for position, _ in ticks], [f"{p:g}" for _, p in ticks])
            ax.set_xlim(left, right)
        if any(item["kind"] in {"line", "scatter", "band", "area", "bars"} for item in spec["series"]):
            location = spec.get("legendLocation", "outside right" if spec["plotId"] == "shared_diagnostics.trace" else "best")
            placement = {"loc": "upper left", "bbox_to_anchor": (1.01, 1)} if location == "outside right" else {"loc": location}
            ax.legend(**placement, facecolor="white", edgecolor="#999999")
        fig.bestfit_omissions = omissions
        return fig
    except Exception:
        if own_axes:
            plt.close(fig)
        raise


def export_plot(spec, output_prefix):
    """Write PNG and SVG with the same source-owned geometry."""
    fig = render_plot(spec)
    prefix = Path(output_prefix)
    prefix.parent.mkdir(parents=True, exist_ok=True)
    paths = [Path(str(prefix) + suffix) for suffix in (".png", ".svg")]
    try:
        for path in paths:
            fig.savefig(path, dpi=180, facecolor="white")
            if path.suffix == ".svg":
                path.write_text("\n".join(line.rstrip() for line in path.read_text(encoding="utf-8").splitlines()) + "\n", encoding="utf-8")
    finally:
        plt.close(fig)
    return paths
