"""Render authoritative BestFit input chronology before fitting, without inventing censored-event dates."""
import argparse
import json
import math
from pathlib import Path

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.patches import Rectangle


def number(row, key):
    """Require an API coordinate; named nonfinite values remain nonfinite for explicit display handling."""
    if key not in row or row[key] is None:
        raise ValueError(f"Chronology requires {key}; fetch /api/inputdata/{{id}}/chronology from a compatible API")
    return float(row[key])


def draw_records(ax, records, label, marker, color, notes, bounds=False):
    """Draw explicit dated observations and model-owned bounds, leaving missing years empty."""
    points = []
    for row in records:
        x, y = number(row, "index"), number(row, "value")
        if not math.isfinite(x) or not math.isfinite(y):
            notes.append(f"{label}: omitted nonfinite marker; original input retained.")
            continue
        points.append((x, y))
        if bounds:
            low, high = number(row, "lowerBound"), number(row, "upperBound")
            if low > high:
                raise ValueError(f"{label}: lowerBound exceeds upperBound")
            if math.isfinite(low) and math.isfinite(high):
                ax.vlines(x, low, high, color=color, linewidth=1.1, zorder=3)
                ax.plot([x, x], [low, high], "_", color=color, markersize=6, zorder=3)
            else:
                notes.append(f"{label} at {x:g}: unbounded/nonfinite interval omitted; marker and source bounds retained.")
    if points:
        ax.scatter([p[0] for p in points], [p[1] for p in points], label=label,
                   marker=marker, color=color, s=24, zorder=4)


def build_figure(payload, ylabel="Value (units not supplied)", index_label="Year / declared index", title=None, zoom=None):
    """Build a linear-axis chronology matching the desktop's observation and threshold conventions."""
    if not isinstance(payload, dict) or payload.get("success") is False or payload.get("schemaVersion") != 1:
        raise ValueError("A successful schemaVersion=1 chronology response is required")
    if not payload.get("inputData", {}).get("id") or "exactData" not in payload:
        raise ValueError("Chronology must identify its input and contain the exactData list")
    notes = []
    fig, ax = plt.subplots(figsize=(11, 6), constrained_layout=True)
    try:
        for i, row in enumerate(payload.get("thresholdData", [])):
            start, end, value = (number(row, k) for k in ("startIndex", "endIndex", "value"))
            if not all(math.isfinite(v) for v in (start, end, value)) or start > end:
                raise ValueError("Threshold window must have finite ordered indexes and value")
            left, right = (start - 0.5, end + 0.5) if start == end else (start, end)
            ax.add_patch(Rectangle((left, 0), right - left, value, facecolor="salmon", alpha=0.39,
                                   edgecolor="salmon", label="Perception threshold" if i == 0 else None, zorder=1))
            above, below = (number(row, k) for k in ("numberAbove", "numberBelow"))
            ax.annotate(f"{start:g}–{end:g}: {above:g} above / {below:g} below",
                        ((left + right) / 2, value), xytext=(0, 4), textcoords="offset points",
                        ha="center", fontsize=8, clip_on=True)
        exact = payload["exactData"]
        draw_records(ax, [r for r in exact if not r.get("isLowOutlier")], "Exact", "o", "black", notes)
        draw_records(ax, [r for r in exact if r.get("isLowOutlier")], "Low outlier", "x", "red", notes)
        draw_records(ax, payload.get("uncertainData", []), "Uncertain", "D", "green", notes, bounds=True)
        draw_records(ax, payload.get("intervalData", []), "Interval", "o", "darkturquoise", notes, bounds=True)
        ax.autoscale_view()
        ax.margins(x=0.03, y=0.15)
        if zoom:
            if len(zoom) != 2 or not all(math.isfinite(v) for v in zoom) or zoom[0] >= zoom[1]:
                raise ValueError("zoom requires an increasing pair of finite indexes")
            # Include whole annual markers at both ends of the requested period.
            ax.set_xlim(zoom[0] - 0.5, zoom[1] + 0.5)
        ax.set_xlabel(index_label, fontsize=12)
        ax.set_ylabel(ylabel, fontsize=12)
        ax.set_title(title or payload["inputData"].get("name") or "Input data chronology", fontsize=16)
        ax.grid(True, alpha=0.25)
        ax.ticklabel_format(style="plain", useOffset=False, axis="x")
        handles, labels = ax.get_legend_handles_labels()
        if handles:
            ax.legend(handles, labels, loc="upper left", fontsize=9)
        fig.text(0.5, -0.015, "Shaded windows show perception limits and aggregate counts; blank years are unknown. Counts do not locate events.",
                 ha="center", fontsize=8)
        return fig, ax, notes
    except Exception:
        plt.close(fig)
        raise


def render(payload, output, ylabel="Value (units not supplied)", index_label="Year / declared index", title=None, zoom=None):
    """Save full chronology PNG/SVG and an optional systematic-period zoom, plus display notes."""
    output = Path(output)
    output.parent.mkdir(parents=True, exist_ok=True)
    files, all_notes = [], []
    views = [("", None)] + ([("-systematic", zoom)] if zoom else [])
    for suffix, window in views:
        fig, _, notes = build_figure(payload, ylabel, index_label, title, window)
        try:
            for extension in ("png", "svg"):
                path = output.parent / f"{output.name}{suffix}.{extension}"
                fig.savefig(path, dpi=180, bbox_inches="tight")
                files.append(path)
            all_notes.extend(notes)
        finally:
            plt.close(fig)
    output.with_name(output.name + "-display-notes.json").write_text(
        json.dumps({"inputDataId": payload["inputData"]["id"], "notes": sorted(set(all_notes))}, indent=2) + "\n", encoding="utf-8")
    return files


def main():
    """Render a saved API response; keep units and year labels explicitly user-controlled."""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--input", type=Path, required=True, help="Saved chronology.json response")
    parser.add_argument("--output", type=Path, required=True, help="Output path prefix")
    parser.add_argument("--ylabel", default="Value (units not supplied)")
    parser.add_argument("--index-label", default="Year / declared index")
    parser.add_argument("--title")
    parser.add_argument("--zoom", nargs=2, type=float, metavar=("START", "END"))
    args = parser.parse_args()
    try:
        payload = json.loads(args.input.read_text(encoding="utf-8-sig"))
        for file in render(payload, args.output, args.ylabel, args.index_label, args.title, args.zoom):
            print(file.resolve())
    except (ValueError, KeyError, TypeError, OSError) as error:
        parser.exit(1, f"Chronology rendering stopped: {error}\n")


if __name__ == "__main__":
    main()
