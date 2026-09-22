"""Render saved BestFit API coordinates with the default desktop frequency styling.

This module performs display transforms only. Estimation, plotting positions, screening,
and uncertainty calculations remain in BestFit. See references/plot-contract.md.
"""
import argparse
import json
import math
from pathlib import Path
from statistics import NormalDist
import sys

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.ticker import FuncFormatter, LogLocator, NullFormatter
import numpy as np

NORMAL = NormalDist()
DISPLAY_MINIMUM = 1e-16


def read_json(path):
    """Read UTF-8 API artifacts, including PowerShell's optional byte-order mark."""
    return json.loads(Path(path).read_text(encoding="utf-8-sig"))


def checked_response(payload, name):
    """Reject failed API/workflow responses before looking for usable coordinates."""
    if not isinstance(payload, dict):
        raise ValueError(f"{name} must be a JSON object")
    if payload.get("success") is False or payload.get("errors"):
        raise ValueError(f"{name} failed: {payload.get('errorMessage') or payload.get('validationErrors') or payload.get('failedStep') or 'unknown error'}")
    return payload


def probability_coordinates(probabilities):
    """Map AEP to Phi^-1(1-AEP), using symmetry to avoid cancellation in small tails."""
    values = np.asarray(probabilities, dtype=float)
    if np.any(~np.isfinite(values)) or np.any((values <= 0) | (values >= 1)):
        raise ValueError("Probabilities must be finite and strictly between 0 and 1")
    return np.asarray([-NORMAL.inv_cdf(float(p)) for p in values])


def curve_values(curve, key, count, notes, label):
    """Validate alignment and leave non-displayable ordinates as gaps, without interpolation."""
    values = np.asarray(curve[key], dtype=float)
    if values.ndim != 1 or len(values) != count:
        raise ValueError(f"frequencyCurve.{key} must have {count} entries")
    valid = np.isfinite(values) & (values > DISPLAY_MINIMUM)
    if not np.all(valid):
        notes.append(f"{label}: omitted {np.count_nonzero(~valid)} nonfinite or <=1e-16 log-display ordinates; gaps retained.")
    return np.where(valid, values, np.nan)


def draw_observations(ax, records, label, marker, color, notes, *, bounds=False, probability_key="plottingPosition"):
    """Draw model-owned marker coordinates and physical-unit bound endpoints."""
    if not records:
        return
    required = [probability_key, "value"] + (["lowerBound", "upperBound"] if bounds else [])
    for record in records:
        for key in required:
            if key not in record or record[key] is None:
                raise ValueError(f"{label} needs {key}; fetch includeData=true from a compatible API")
    p = np.asarray([r[probability_key] for r in records], dtype=float)
    y = np.asarray([r["value"] for r in records], dtype=float)
    valid = np.isfinite(p) & (p > 0) & (p < 1) & np.isfinite(y) & (y > DISPLAY_MINIMUM)
    if not np.all(valid):
        notes.append(f"{label}: omitted {np.count_nonzero(~valid)} markers with invalid AEP or nonfinite/<=1e-16 magnitude; input retained.")
    if not np.any(valid):
        return
    x = probability_coordinates(p[valid])
    if bounds:
        lower = np.asarray([r["lowerBound"] for r in records], dtype=float)[valid]
        upper = np.asarray([r["upperBound"] for r in records], dtype=float)[valid]
        if np.any(np.isfinite(lower) & np.isfinite(upper) & (lower > upper)):
            raise ValueError(f"{label}: lowerBound exceeds upperBound")
        displayable = np.isfinite(lower) & np.isfinite(upper) & (lower > DISPLAY_MINIMUM) & (upper > DISPLAY_MINIMUM)
        if not np.all(displayable):
            notes.append(f"{label}: omitted {np.count_nonzero(~displayable)} bounds not representable on the log axis.")
        bars = ax.vlines(x[displayable], lower[displayable], upper[displayable], color="black", linewidth=1, zorder=4)
        bars.set_gid(f"{label} bounds")
    if marker == "x":
        ax.scatter(x, y[valid], marker=marker, c=color, s=36, linewidths=2, label=label, zorder=5)
    else:
        ax.scatter(x, y[valid], marker=marker, c=color, edgecolors="black", s=28, linewidths=1, label=label, zorder=5)


def set_probability_ticks(ax):
    """Use the desktop NormalProbabilityAxis major/minor AEP tick families."""
    major = [0.9, 0.5, 0.1]
    minor = [0.2, 0.3, 0.4, 0.6, 0.7, 0.8]
    for exponent in range(2, 16):
        tail = 10.0 ** -exponent
        major.extend([tail, 1 - tail])
        for multiplier in range(2, 10):
            minor.extend([tail * multiplier, 1 - tail * multiplier])
    left, right = ax.get_xlim()
    pairs = sorted((float(probability_coordinates([p])[0]), p) for p in set(major) if 0 < p < 1)
    pairs = [(x, p) for x, p in pairs if left <= x <= right]
    ax.set_xticks([x for x, _ in pairs], [format(p, ".3g") if p < 0.001 or p > 0.999 else format(p, ".6f").rstrip("0").rstrip(".") for _, p in pairs])
    minor_x = probability_coordinates(sorted(set(minor)))
    ax.set_xticks(minor_x[(minor_x >= left) & (minor_x <= right)], minor=True)
    ax.set_xlim(left, right)


def build_figure(results, input_data, *, title="Frequency", ylabel=""):
    """Build a single B17C/Bayesian univariate figure and explicit display-only omission notes."""
    results = checked_response(results, "Results")
    if "results" in results:
        results = checked_response(results["results"], "Workflow results")
    input_data = checked_response(input_data, "Input data")
    if "exactData" not in input_data:
        raise ValueError("Input data must include observations; request includeData=true")
    kind = results.get("kind")
    if kind not in ("bulletin17C", "univariate"):
        raise ValueError("This renderer supports single bulletin17C and univariate frequency results")
    curve = results.get("frequencyCurve")
    if not isinstance(curve, dict) or not curve.get("probabilities") or curve.get("modeCurve") is None:
        raise ValueError("Results must contain frequencyCurve.probabilities and modeCurve")
    probabilities = np.asarray(curve["probabilities"], dtype=float)
    if probabilities.ndim != 1 or len(probabilities) < 2 or len(set(probabilities)) != len(probabilities):
        raise ValueError("At least two distinct, aligned curve probabilities are required")
    x = probability_coordinates(probabilities)
    order = np.argsort(x)
    x = x[order]
    notes = []
    b17c = kind == "bulletin17C"
    estimator = (results.get("fittedDistribution") or {}).get("pointEstimator")
    if not b17c and estimator not in ("posteriorMode", "posteriorMean"):
        raise ValueError("fittedDistribution.pointEstimator must identify posteriorMode or posteriorMean")
    mode_label = "Computed" if b17c else ("Posterior Mean" if estimator == "posteriorMean" else "Posterior Mode")
    mean_label = "Expected Probability" if b17c else "Posterior Predictive"
    mode = curve_values(curve, "modeCurve", len(x), notes, mode_label)[order]
    if np.count_nonzero(np.isfinite(mode)) < 2:
        raise ValueError("Fewer than two computed curve ordinates can be displayed on a log axis")
    mean = None if curve.get("meanCurve") is None else curve_values(curve, "meanCurve", len(x), notes, mean_label)[order]
    has_lower, has_upper = curve.get("ciLower") is not None, curve.get("ciUpper") is not None
    if has_lower != has_upper:
        raise ValueError("Both ciLower and ciUpper must be provided together")
    lower = upper = None
    if has_lower:
        lower = curve_values(curve, "ciLower", len(x), notes, "Interval lower bound")[order]
        upper = curve_values(curve, "ciUpper", len(x), notes, "Interval upper bound")[order]
        if np.any(lower > upper):
            raise ValueError("ciLower exceeds ciUpper")
        width = float(curve.get("credibleIntervalWidth", float("nan")))
        if not 0 < width < 1:
            raise ValueError("A valid credibleIntervalWidth is required to label uncertainty")

    with plt.rc_context({"font.family": "sans-serif", "font.size": 12, "axes.labelsize": 16, "axes.titlesize": 18}):
        fig, ax = plt.subplots(figsize=(10, 6.5), layout="constrained")
        try:
            fig.patch.set_facecolor("white")
            ax.set_facecolor("white")
            ax.set_title(title, pad=14)
            ax.set_xlabel("Exceedance Probability [P(X > x)]", labelpad=20)
            ax.set_ylabel(ylabel, labelpad=20)
            ax.set_yscale("log")
            ax.set_axisbelow(True)
            ax.grid(axis="y", which="major", color="#c0c0c0", linestyle="-", linewidth=0.7)
            ax.grid(axis="y", which="minor", color="#dedede", linestyle="--", linewidth=0.5)
            if lower is not None:
                interval_label = f"{100 * width:.0f}% {'Confidence' if b17c else 'Credible'} Intervals"
                ax.fill_between(x, lower, upper, facecolor=(104 / 255, 140 / 255, 175 / 255, 75 / 255), label=interval_label)
                ax.plot(x, lower, color="#353b7a", linewidth=1)
                ax.plot(x, upper, color="#353b7a", linewidth=1)
            if mean is not None:
                ax.plot(x, mean, color="blue", linestyle="--", linewidth=1, label=mean_label)
            ax.plot(x, mode, color="black", linewidth=1, label=mode_label)
            exact = input_data["exactData"]
            draw_observations(ax, [r for r in exact if not r.get("isLowOutlier", False)], "Exact Data", "o", "black", notes)
            draw_observations(ax, [r for r in exact if r.get("isLowOutlier", False)], "Low Outlier Data", "x", "red", notes)
            draw_observations(ax, input_data.get("uncertainData"), "Uncertain Data", "D", "green", notes, bounds=True)
            draw_observations(ax, input_data.get("intervalData"), "Interval Data", "o", "cyan", notes, bounds=True)
            draw_observations(ax, results.get("quantileAnnotations"), "Quantile Prior", "s", "red", notes, bounds=True, probability_key="aep")
            ax.margins(x=0.03)
            y_min, y_max = ax.dataLim.intervaly
            bottom, top = 10 ** math.floor(math.log10(y_min)), 10 ** math.ceil(math.log10(y_max))
            ax.set_ylim(bottom if bottom < top else bottom / 10, top if bottom < top else top * 10)
            ax.yaxis.set_major_locator(LogLocator(base=10))
            ax.yaxis.set_minor_locator(LogLocator(base=10, subs=np.arange(2, 10)))
            ax.yaxis.set_major_formatter(FuncFormatter(lambda value, _: f"{value:,.0f}"))
            ax.yaxis.set_minor_formatter(NullFormatter())
            set_probability_ticks(ax)
            ax.legend(loc="upper left", facecolor="white", edgecolor="darkgray", framealpha=140 / 255, fontsize=10)
            return fig, ax, notes
        except Exception:
            plt.close(fig)
            raise


def save_figure(figure, output_stem):
    """Save a PNG for chat and an SVG for export, returning both paths."""
    stem = Path(output_stem)
    stem.parent.mkdir(parents=True, exist_ok=True)
    paths = [Path(str(stem) + extension) for extension in (".png", ".svg")]
    for path in paths:
        figure.savefig(path, dpi=180, facecolor="white")
    return paths


def main():
    """Render saved API JSON and report display omissions to stderr."""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--results", required=True, type=Path)
    parser.add_argument("--input", required=True, type=Path, dest="input_path")
    parser.add_argument("--output", required=True, type=Path, help="Output stem; .png and .svg are appended")
    parser.add_argument("--title", default="Frequency")
    parser.add_argument("--ylabel", default="", help="Use only units established from the source/user")
    args = parser.parse_args()
    try:
        fig, _, notes = build_figure(read_json(args.results), read_json(args.input_path), title=args.title, ylabel=args.ylabel)
        for path in save_figure(fig, args.output):
            print(path.resolve())
        plt.close(fig)
        for note in notes:
            print(note, file=sys.stderr)
    except (ValueError, KeyError, TypeError, OSError) as error:
        parser.exit(1, f"Cannot render frequency plot: {error}\n")


if __name__ == "__main__":
    main()
