"""The 15 raw-series and input-data plot slots in BestFit's desktop controls.

These adapters use portable BestFit/Numerics methods at the same settings as
the app. They neither re-screen low outliers nor replace saved plotting ranks.
"""
from __future__ import annotations
import calendar
import math
from datetime import datetime, timedelta
from .common import area, axis, bars, clean, correlation_plot, density, histogram, line, net_array, plot, scatter


def observations_from_dataframe(frame):
    """Read exact values and historical information, retaining their separate types."""
    observations = []
    for kind, rows in (("exact", frame.ExactSeries), ("uncertain", frame.UncertainSeries),
                       ("interval", frame.IntervalSeries)):
        for row in rows:
            item = dict(kind=kind, index=float(row.Index), value=float(row.Value),
                        aep=float(row.PlottingPosition))
            if kind == "exact":
                item.update(lowOutlier=bool(row.IsLowOutlier), date=str(row.DateTime.ToString("o")))
            else:
                item.update(lower=float(row.LowerValue), upper=float(row.UpperValue))
            for target, attr in (("standardized", "StandardizedValue"), ("standardizedLog10", "StandardizedLog10Value"),
                                 ("log10", "Log10Value"), ("log10Lower", "Log10LowerValue"), ("log10Upper", "Log10UpperValue")):
                if hasattr(row, attr):
                    item[target] = float(getattr(row, attr))
            observations.append(item)
    for row in frame.ThresholdSeries:
        observations.append(dict(kind="threshold", start=float(row.StartIndex), end=float(row.EndIndex),
                                 value=float(row.Value)))
    return observations


def observation_plot(observations, source, kind, unit="Value", log_space=False, y_scale=None, index_label="Index"):
    """Create chronology, frequency or real/log QQ geometry from typed observations."""
    if kind not in {"chronology", "frequency", "qq"}:
        raise ValueError(f"Unsupported observation plot: {kind}")
    scale = y_scale or ("log" if kind == "frequency" else "linear")
    x_field = "aep" if kind == "frequency" else "index" if kind == "chronology" else "standardizedLog10" if log_space else "standardized"
    y_field = "log10" if kind == "qq" and log_space else "value"
    series = []
    groups = [("exact", False, "Exact Data", "black", "o"),
              ("exact", True, "Low Outlier Data", "red", "x"),
              ("uncertain", None, "Uncertain Data", "green", "D"),
              ("interval", None, "Interval Data", "cyan", "o")]
    for record_kind, low, name, color, marker in groups:
        rows = [r for r in observations if r["kind"] == record_kind and
                (low is None or bool(r.get("lowOutlier", False)) == low)]
        if not rows:
            continue
        item = scatter(name, [r.get(x_field) for r in rows], [r.get(y_field) for r in rows],
                       color=color, marker=marker, boundsColor="black", size=28,
                       minimumPositive=1e-16 if kind == "frequency" and record_kind == "exact" else 0)
        if low is None:
            lower = "log10Lower" if kind == "qq" and log_space else "lower"
            upper = "log10Upper" if kind == "qq" and log_space else "upper"
            item.update(yLower=clean([r.get(lower) for r in rows]), yUpper=clean([r.get(upper) for r in rows]),
                        interval={"kind": "measurement"})
        series.append(item)
    if kind == "chronology":
        for i, r in enumerate(o for o in observations if o["kind"] == "threshold"):
            start, end = r["start"], r["end"]
            if start == end:
                start, end = start - .5, end + .5
            label = "Threshold Data" if i == 0 else f"Threshold {r['start']:g}-{r['end']:g}"
            series.append(area(label, [start, end], [0, 0], [r["value"]]*2,
                               color="salmon", facecolor="salmon", alpha=100/255, legend=i==0))
    if kind == "qq" and series:
        values = [v for s in series for v in s["x"] if v is not None]
        if values:
            bounds = [min(values), max(values)]
            series.append(line("1:1 Line", bounds, bounds, color="black", linestyle="--"))
    x = axis("Exceedance Probability", "normal_probability", "aep") if kind == "frequency" else axis(index_label if kind == "chronology" else "Quantile (Standardized)")
    title = {"chronology": "Chronology", "frequency": "Nonparametric Frequency", "qq": "Normal Q-Q Plot"}[kind]
    return plot(f"input_data.{kind}", source, title, x, axis("Quantile (Data)" if kind == "qq" else unit, scale), series,
                variant="log10" if log_space else "default", omissions=[] if series else ["No observations for this view."])


def _month_histogram(months):
    total = sum(months)
    dates = [(datetime(2020, i, 1) + timedelta(days=calendar.monthrange(2020, i)[1]/2)).isoformat() for i in range(1, 13)]
    return bars("Seasonality Plot", dates, [v/total for v in months],
                width=[calendar.monthrange(2020, i)[1] for i in range(1, 13)],
                color="#29d372", alpha=117/255, edgecolor="#353b7a")


def time_series_plots(time_series, source, unit="Value", peak=False):
    """Use monthly summary/frequency and correlation guards from TimeSeriesControl."""
    dates = [str(r.Index.ToString("o")) for r in time_series]
    values = [float(r.Value) for r in time_series]
    output = {"series": plot("time_series_data.series", source, "Time Series", axis("Date", "date", "date"),
                             axis(unit), [line("Time Series Data", dates, values, color="#64c8fa")])}
    years = {int(r.Index.Year) for r in time_series}
    season = []
    if len(values) > 2 and len(years) > 1:
        if peak:
            months = list(time_series.MonthlyFrequency())
            if sum(months):
                season = [_month_histogram(months)]
        else:
            stats = time_series.MonthlySummaryStatistics()
            x = [f"2020-{i+1:02d}-01" for i in range(stats.GetLength(0))]
            cols = lambda j: [float(stats[i, j]) for i in range(stats.GetLength(0))]
            season = [area("90% Observed Range", x, cols(1), cols(5),
                           color="#353b7a", facecolor="#688caf", alpha=74/255),
                      area("50% Observed Range", x, cols(2), cols(4),
                           color="#353b7a", facecolor="#29d372", alpha=117/255),
                      line("Median", x, cols(3), color="blue"), line("Mean", x, cols(7), color="blue", linestyle="-.")]
    output["seasonality"] = plot("time_series_data.seasonality", source, "Seasonality", axis("Month", "date", "date"),
        axis("Relative Frequency" if peak else unit), season, variant="peak" if peak else "summary",
        omissions=[] if season else ["Seasonality requires observations in at least two years."])
    for key in ("acf", "pacf"):
        output[key] = correlation_plot(values, source, f"time_series_data.{key}", partial=key == "pacf")
    return output


def input_data_plots(frame, source, unit="Value", index_label="Index"):
    """Create eight input diagnostics; POT diagnostics require their source time series."""
    from RMC.BestFit.Models import DataFrame
    # Standardization is a derived display operation. Apply it to a detached copy.
    frame = DataFrame(frame.ToXElement())
    frame.SetStandardizedValues()
    observations = observations_from_dataframe(frame)
    output = {kind: observation_plot(observations, source, kind, unit, index_label=index_label) for kind in ("chronology", "frequency")}
    output["qq_real"] = observation_plot(observations, source, "qq", unit)
    output["qq_log10"] = observation_plot(observations, source, "qq", unit, log_space=True)
    output["qq"] = output["qq_log10"]  # App LogSpaceRadioButton is checked by default.
    values = [float(r.Value) for collection in (frame.ExactSeries, frame.UncertainSeries, frame.IntervalSeries) for r in collection]
    enough = frame.ExactSeries.Count >= 10
    for kind, function in (("density", density), ("histogram", histogram)):
        output[kind] = plot(f"input_data.{kind}", source, kind.title(), axis(unit), axis("Density"),
                            [function(values)] if enough else [],
                            omissions=[] if enough else ["The app requires at least 10 exact observations."])
    months = [0]*12
    for r in frame.ExactSeries:
        if int(r.DateTime.Year) != 1:
            months[int(r.DateTime.Month)-1] += 1
    output["seasonality"] = plot("input_data.seasonality", source, "Seasonality", axis("Month", "date", "date"), axis("Relative Frequency"),
        [_month_histogram(months)] if enough and sum(months) else [],
        omissions=[] if enough and sum(months) else ["At least 10 exact observations with event dates are required."])
    for kind in ("acf", "pacf"):
        output[kind] = correlation_plot([float(r.Value) for r in frame.ExactSeries], source, f"input_data.{kind}", partial=kind=="pacf")
    return output


def threshold_plots(time_series, source, smoothing_function, period, threshold, results=None):
    """Prepare all three POT diagnostics at the saved extraction settings.

    Supplying ``results=(mrl, stability)`` renders existing results. Otherwise the
    portable app diagnostics run their original 100/50-threshold calculations.
    """
    from RMC.BestFit.Models import ThresholdDiagnostics
    if results is None:
        smoothed = time_series.SmoothedSeries(smoothing_function, period)
        values = [float(r.Value) for r in smoothed if not math.isnan(float(r.Value))]
        if len(values) < 20:
            raise ValueError("POT diagnostics require at least 20 smoothed observations")
        ordered = sorted(values)
        a, lo, hi = net_array(values), ordered[len(ordered)//2], ordered[-1]
        results = (ThresholdDiagnostics.ComputeMeanResidualLife(a, lo, hi),
                   ThresholdDiagnostics.ComputeParameterStability(a, lo, hi))
    mrl, stability = results
    output = {}
    for key, result, attr, low, high, label in (
        ("mean_residual_life", mrl, "MeanExcess", "LowerCI", "UpperCI", "Mean Excess"),
        ("modified_scale", stability, "ModifiedScale", "ModifiedScaleLowerCI", "ModifiedScaleUpperCI", "Modified Scale"),
        ("shape", stability, "Shape", "ShapeLowerCI", "ShapeUpperCI", "Shape")):
        points = list(result.Points)
        x = [float(r.Threshold) for r in points]
        lower = [float(getattr(r, low)) for r in points]
        upper = [float(getattr(r, high)) for r in points]
        series = [area("95% CI", x, lower, upper, {"kind":"confidence", "level":.95},
                       color="#353b7a", facecolor="#688caf", alpha=75/255),
                  line(label, x, [float(getattr(r, attr)) for r in points], color="#353b7a", linewidth=2)] if points else []
        finite = [v for v in lower+upper if math.isfinite(v)]
        if finite:
            series.append(line("Threshold", [threshold, threshold], [min(finite), max(finite)], color="red", linestyle="--", linewidth=1.5))
        title = "Mean Residual Life" if key == "mean_residual_life" else label + " vs. Threshold"
        output[key] = plot(f"input_data.{key}", source, title, axis("Threshold"), axis(label), series,
                           omissions=[] if series else ["No supported threshold estimates."])
    return output
