"""Frequency and fitting geometry from an already completed BestFit analysis."""
from __future__ import annotations

import math
import xml.etree.ElementTree as ET

from .common import area, axis, bars, clean, column, histogram, line, net_array, plot, scatter, source_identity
from .input_data import observations_from_dataframe, observation_plot


def _get(value, *names, default=None):
    for name in names:
        if isinstance(value, dict) and name in value:
            return value[name]
        if value is not None and hasattr(value, name):
            return getattr(value, name)
    return default


def _kind(restored):
    value = str(restored.get("kind") or _get(restored.get("row"), "Type", default="") or "").lower()
    if value:
        return value
    analysis = restored.get("analysis")
    if analysis is not None:
        return str(analysis.GetType().Name if hasattr(analysis, "GetType") else type(analysis).__name__).lower()
    return "distribution fitting" if restored.get("table") == "Distribution Fitting Analysis" else "univariate"


def _observations(restored):
    if "observations" in restored:
        return list(restored["observations"])
    frame = restored.get("input")
    return observations_from_dataframe(frame) if frame is not None else []


def _unit_label(restored):
    label = restored.get("unitLabel") or _get(restored.get("input_row"), "UnitLabel")
    return str(label) if label else "Value (unit unavailable)"


def _unit_value(unit_label):
    return "" if unit_label == "Value (unit unavailable)" else unit_label


def _fitting_cdf_observations(observations):
    """App CDF data use magnitude versus the saved plotting-position complement."""
    output = []
    for kind, low, name, color, marker in (("exact", False, "Exact Data", "black", "o"),
                                          ("exact", True, "Low Outlier Data", "red", "x"),
                                          ("uncertain", None, "Uncertain Data", "green", "D"),
                                          ("interval", None, "Interval Data", "cyan", "o")):
        rows = [row for row in observations if row["kind"] == kind and
                (low is None or bool(row.get("lowOutlier")) == low) and row.get("aep") is not None]
        if not rows:
            continue
        item = scatter(name, [row["value"] for row in rows], [1-float(row["aep"]) for row in rows],
                       color=color, marker=marker)
        if low is None:
            item["xLower"] = clean([row.get("lower") for row in rows])
            item["xUpper"] = clean([row.get("upper") for row in rows])
            item["interval"] = {"kind": "measurement"}
        output.append(item)
    return output


def _portable_model(restored):
    return restored.get("model")


def _bayesian_settings(restored):
    settings = _get(restored.get("analysis"), "BayesianAnalysis")
    if settings is not None or not restored.get("analysisXml"):
        return settings
    node = ET.fromstring(restored["analysisXml"]).find(".//BayesianAnalysis")
    return dict(node.attrib) if node is not None else None


def _curve(restored):
    analysis = restored.get("analysis")
    result = _get(analysis, "AnalysisResults")
    if result is not None:
        p = [float(x) for x in analysis.ProbabilityOrdinates]
        n = len(p)
        ci = result.ConfidenceIntervals
        ci_rows = ci.GetLength(0) if hasattr(ci, "GetLength") else len(ci)
        if len(result.ModeCurve) != n or len(result.MeanCurve) != n or ci_rows != n:
            raise ValueError("Stored curve and probability ordinates are misaligned")
        low = column(ci, 0) if hasattr(ci, "GetLength") else [float(row[0]) for row in ci]
        high = column(ci, 1) if hasattr(ci, "GetLength") else [float(row[1]) for row in ci]
        return p, [float(x) for x in result.ModeCurve], [float(x) for x in result.MeanCurve], \
            low, high
    curve = _get(restored.get("results"), "frequencyCurve")
    if curve:
        p = _get(curve, "probabilities")
        mode, mean = _get(curve, "modeCurve"), _get(curve, "meanCurve")
        lo, hi = _get(curve, "ciLower"), _get(curve, "ciUpper")
        if any(v is None or len(v) != len(p) for v in (mode, mean, lo, hi)):
            raise ValueError("Stored curve and probability ordinates are misaligned")
        return p, mode, mean, lo, hi
    return None


def _frequency(restored, *, show_pot=True):
    source = source_identity(restored)
    kind = _kind(restored)
    b17c = "b17c" in kind or "bulletin17c" in kind
    title = "Frequency"
    observations = _observations(restored)
    unit = _unit_label(restored)
    base = observation_plot(observations, source, "frequency", unit) if observations and show_pot else None
    series = []
    curve = _curve(restored)
    point_name = "Posterior Mode"
    if curve:
        p, mode, mean, lo, hi = curve
        settings = _bayesian_settings(restored)
        width = float(_get(settings, "CredibleIntervalWidth", default=_get(_get(restored.get("results"), "frequencyCurve"), "credibleIntervalWidth", default=.9)))
        interval_kind = "confidence" if b17c else "credible"
        point_name = "Posterior Mean" if "posteriormean" in str(_get(settings, "PointEstimator", default="")).lower() else "Posterior Mode"
        if b17c:
            point_name = "Computed"
        prediction_name = "Expected Probability" if b17c else "Posterior Predictive"
        series += [area(f"{width:.0%} {interval_kind.title()} Intervals", p, lo, hi,
                        {"kind": interval_kind, "level": width}, color="#353b7a", facecolor="#688caf", alpha=75/255),
                   line(prediction_name, p, mean, color="blue", linestyle="--"),
                   line(point_name, p, mode, color="black", linewidth=2 if "composite" in kind else 1)]
    if base:
        if "pointprocess" in kind:
            for item in base["series"]:
                item["name"] = "POT " + item["name"]
        series.extend(base["series"])
    annotations = _get(restored.get("results"), "quantileAnnotations", default=[]) or []
    if not annotations:
        model = _portable_model(restored)
        if _get(model, "EnableQuantilePriors", default=False):
            annotations = _get(model, "QuantilePriors", default=[]) or []
        elif b17c:
            annotations = [q for q in (_get(restored.get("analysis"), "QuantilePenalties", default=[]) or [])
                           if _get(q, "Enabled", default=False)]
    if annotations:
        x, y, lower, upper = [], [], [], []
        for item in annotations:
            p = _get(item, "aep", "AEP", "probability", "alpha", "Alpha")
            value = _get(item, "value", "meanValue", "MeanValue")
            if p is not None and value is not None:
                x.append(float(p)); y.append(float(value))
                lower.append(_get(item, "lowerBound", "lower", "LowerValue", default=value))
                upper.append(_get(item, "upperBound", "upper", "UpperValue", default=value))
        if x:
            prior = scatter("Quantile Prior" if not b17c else "Quantile Penalties", x, y,
                            color="red", boundsColor="black", marker="s")
            if lower != y or upper != y:
                prior.update(yLower=clean(lower), yUpper=clean(upper),
                             interval={"kind": "prior"})
            series.append(prior)
    model = _portable_model(restored)
    if "pointprocess" in kind and model is not None:
        # The desktop displays Langbein-converted AMS ranks alongside POT ranks.
        ams = _get(model, "DataFrame")
        if ams is not None:
            from RMC.BestFit.Models import DataFrame
            ams_copy = DataFrame(ams.ToXElement())
            ams_copy.ApplyLangbeinConversion(float(model.Lambda))
            for item in observation_plot(observations_from_dataframe(ams_copy), source, "frequency", unit)["series"]:
                item["name"] = "AMS " + item["name"]
                series.append(item)
        if bool(_get(model, "IsSeasonal", default=False)) and curve:
            parent = _get(_get(restored.get("analysis"), "AnalysisResults"), "ParentDistribution")
            for i, dist in enumerate(_get(parent, "Distributions", default=[]) or []):
                series.append(line(f"{point_name} - Season {i+1}", p,
                                   [float(dist.InverseCDF(1-v)) for v in p]))
    if "pointprocess" in kind and restored.get("analysis") is None:
        if "amsObservations" not in restored:
            raise ValueError("Point-process API plot source lacks Langbein AMS observations")
        if restored.get("amsObservations"):
            for item in observation_plot(restored["amsObservations"], source, "frequency", unit)["series"]:
                item["name"] = "AMS " + item["name"]
                series.append(item)
        for item in restored.get("componentCurves") or []:
            series.append(line(f"{point_name} - {_get(item, 'name', 'Name')}",
                               _get(item, "probabilities", "Probabilities"), _get(item, "values", "Values")))
    if "composite" in kind:
        for name, dependency in (restored.get("dependencies") or {}).items():
            subcurve = _curve(dependency)
            if subcurve:
                series.append(line(f"{point_name} - {name}", subcurve[0], subcurve[1], linewidth=1.5))
        for item in restored.get("componentCurves") or []:
            series.append(line(f"{point_name} - {_get(item, 'name', 'Name')}",
                               _get(item, "probabilities", "Probabilities"), _get(item, "values", "Values"), linewidth=1.5))
    prefix = ("b17c" if b17c else "point_process" if "pointprocess" in kind else
              "mixture" if "mixture" in kind else "composite" if "composite" in kind else "univariate")
    variant = ("pot" if show_pot else "ams") if prefix == "point_process" else "default"
    return plot(f"{prefix}.frequency", source, title, axis("Exceedance Probability", "normal_probability", "aep"),
                axis(unit, "log"), series,
                variant=variant,
                omissions=[] if series else ["No completed frequency result or observations are available."])


def _chronology(restored):
    source = source_identity(restored)
    observations = _observations(restored)
    unit = _unit_label(restored)
    index_label = _get(restored.get("input_row"), "IndexLabel", default="Index")
    base = observation_plot(observations, source, "chronology", unit,
                            index_label=index_label) if observations else None
    series = list(base["series"]) if base else []
    result = _get(restored.get("analysis"), "ChronologyAnalysisResults")
    if result is not None:
        full = _get(restored.get("input"), "FullTimeSeries")
        start = float(full[0].Index) if full is not None and len(full) else \
            min((r["index"] for r in observations if r["kind"] != "threshold"), default=None)
        if start is not None:
            n = len(result.MeanCurve)
            x = [start+i for i in range(n)]
            width = float(restored["analysis"].BayesianAnalysis.CredibleIntervalWidth)
            series.extend([area(f"{width:.0%} Credible Intervals", x,
                                column(result.ConfidenceIntervals, 0), column(result.ConfidenceIntervals, 1),
                                {"kind": "credible", "level": width}, color="#353b7a", facecolor="#688caf", alpha=75/255),
                           line("Mean", x, result.MeanCurve, color="blue")])
    api_chronology = restored.get("chronology")
    if api_chronology:
        x = _get(api_chronology, "indices", "Indices")
        width = float(_get(api_chronology, "credibleIntervalWidth", "CredibleIntervalWidth"))
        series.extend([area(f"{width:.0%} Credible Intervals", x,
                            _get(api_chronology, "ciLower", "CiLower"), _get(api_chronology, "ciUpper", "CiUpper"),
                            {"kind": "credible", "level": width}, color="#353b7a", facecolor="#688caf", alpha=75/255),
                       line("Mean", x, _get(api_chronology, "meanCurve", "MeanCurve"), color="blue")])
    return plot("univariate.chronology", source, "Chronology", axis(index_label), axis(unit), series,
                omissions=[] if series else ["No chronology observations or saved result are available."])


def _fitting(restored):
    analysis = restored.get("analysis")
    if analysis is None:
        return _fitting_api(restored)
    source = source_identity(restored)
    unit = _unit_label(restored)
    observations = _observations(restored)
    values = [float(r["value"]) for r in observations if r["kind"] in {"exact", "uncertain", "interval"}]
    p = [float(v) for v in analysis.ProbabilityOrdinates]
    successful = [fit for fit in analysis.FittedDistributions if fit.FitSucceeded and fit.ShowResults]
    frequency_series = list(observation_plot(observations, source, "frequency", unit)["series"]) if observations else []
    for fit in successful:
        frequency_series.append(line(str(fit.Distribution.DisplayName), p,
                                     [float(fit.Distribution.InverseCDF(1-v)) for v in p]))
    output = {"frequency": plot("fitting.frequency", source, "Frequency",
                 axis("Exceedance Probability", "normal_probability", "aep"), axis(unit, "log"),
                 frequency_series, omissions=[] if frequency_series else ["No observations or visible successful fits."])}
    if values:
        lo, hi = min(values), max(values)
        # Match UpdatePDFPlot/UpdateCDFPlot's exact source range and Numerics grid.
        if lo <= 0 or hi <= 0:
            raise ValueError("The app's logarithmic fitting range requires positive observations")
        from Numerics.Sampling import Stratify, StratificationOptions
        x = Stratify.XValues(StratificationOptions(lo-10**math.floor(math.log10(lo)),
                                                  hi+10**math.floor(math.log10(hi)), 1000))
    else:
        x = None
    for key, method, label in (("pdf", "CreatePDFGraph", "Density"), ("cdf", "CreateCDFGraph", "Probability")):
        series = [histogram(values)] if key == "pdf" and values and min(values) < max(values) else []
        if key == "pdf" and series:
            series[0]["name"] = "Input Data Histogram"
            series[0]["style"].update(color="#b0e0e6", alpha=100/255)
        if x is not None:
            for fit in successful:
                points = getattr(fit.Distribution, method)(x)
                series.append(line(str(fit.Distribution.DisplayName), column(points, 0), column(points, 1)))
        if key == "cdf": series.extend(_fitting_cdf_observations(observations))
        output[key] = plot(f"fitting.{key}", source,
                           "Probability Density Function" if key == "pdf" else "Cumulative Distribution Function",
                           axis(unit), axis("Non-Exceedance Probability" if key == "cdf" else label), series,
                           omissions=[] if series else ["No observations or visible successful fits."])
    ordered = sorted(values)
    probabilities = sorted(float(r["aep"]) for r in observations if r["kind"] in {"exact", "uncertain", "interval"})
    complements = sorted(1-v for v in probabilities)
    for key in ("pp", "qq"):
        series = []
        if ordered and complements and len(ordered) == len(complements):
            bounds = [0, 1] if key == "pp" else [min(ordered), max(ordered)]
            series.append(line("1:1 Line", bounds, bounds, color="black", linestyle="--"))
            for fit in successful:
                distribution = fit.Distribution
                x = [float(distribution.CDF(v)) for v in ordered] if key == "pp" else ordered
                y = complements if key == "pp" else [float(distribution.InverseCDF(v)) for v in complements]
                series.append(line(str(distribution.DisplayName), x, y))
        output[key] = plot(f"fitting.{key}", source, f"{key.upper()[0]}-{key.upper()[1]} Plot",
                           axis("Probability (Model)" if key == "pp" else "Quantile (Model)"),
                           axis("Probability (Data)" if key == "pp" else "Quantile (Data)"), series,
                           omissions=[] if series else ["No aligned observations or visible successful fits."])
    return output


def _fitting_api(restored):
    """Render exported candidate geometry without loading a managed runtime."""
    source, unit = source_identity(restored), _unit_label(restored)
    observations = _observations(restored)
    curves = restored.get("fittingCurves") or []
    frequency_series = list(observation_plot(observations, source, "frequency", unit)["series"]) if observations else []
    def coordinates(items):
        return ([float(_get(item, "x", "X")) for item in items],
                [float(_get(item, "y", "Y")) for item in items])
    for candidate in curves:
        points = _get(candidate, "frequency", "Frequency", default=[])
        if points:
            x, y = coordinates(points)
            frequency_series.append(line(_get(candidate, "name", "Name"), x, y))
    output = {"frequency": plot("fitting.frequency", source, "Frequency",
        axis("Exceedance Probability", "normal_probability", "aep"), axis(unit, "log"), frequency_series)}
    for key, ylabel in (("pdf", "Density"), ("cdf", "Probability")):
        series = []
        if key == "pdf":
            bins = restored.get("fittingHistogram") or []
            if bins:
                lower = [float(_get(b, "lowerBound", "LowerBound")) for b in bins]
                upper = [float(_get(b, "upperBound", "UpperBound")) for b in bins]
                frequencies = [float(_get(b, "frequency", "Frequency")) for b in bins]
                widths = [b-a for a,b in zip(lower,upper)]
                denominator = sum(f*w for f,w in zip(frequencies,widths))
                series.append(bars("Input Data Histogram", [(a+b)/2 for a,b in zip(lower,upper)],
                                   [f/denominator if denominator else 0 for f in frequencies], width=widths,
                                   color="#b0e0e6", alpha=100/255))
        for candidate in curves:
            points = _get(candidate, key, key.title(), default=[])
            if points:
                x,y=coordinates(points)
                series.append(line(_get(candidate,"name","Name"),x,y))
        if key == "cdf": series.extend(_fitting_cdf_observations(observations))
        output[key] = plot(f"fitting.{key}", source,
            "Probability Density Function" if key == "pdf" else "Cumulative Distribution Function",
            axis(unit),axis("Non-Exceedance Probability" if key == "cdf" else ylabel),series)
    for key in ("pp","qq"):
        series=[]
        all_points=[_get(candidate,key,key.title(),default=[]) for candidate in curves]
        all_points=[points for points in all_points if points]
        if all_points:
            if key=="pp": bounds=[0,1]
            else:
                xs=[float(_get(point,"x","X")) for points in all_points for point in points]
                bounds=[min(xs),max(xs)]
            series.append(line("1:1 Line",bounds,bounds,color="black",linestyle="--"))
        for candidate in curves:
            points=_get(candidate,key,key.title(),default=[])
            if points:
                x,y=coordinates(points)
                series.append(line(_get(candidate,"name","Name"),x,y))
        output[key]=plot(f"fitting.{key}",source,f"{key.upper()[0]}-{key.upper()[1]} Plot",
            axis("Probability (Model)" if key=="pp" else "Quantile (Model)"),
            axis("Probability (Data)" if key=="pp" else "Quantile (Data)"),series)
    return output


def frequency_plots(restored):
    """Return supported frequency-family plots from one completed saved or API source."""
    kind = _kind(restored)
    if "fitting" in kind:
        return _fitting(restored)
    output = {"frequency": _frequency(restored)}
    if "pointprocess" in kind:
        output["frequency_ams"] = _frequency(restored, show_pot=False)
    model = restored.get("model")
    if "univariate" in kind and (bool(_get(model, "IsNonstationary", default=False)) or restored.get("chronology")):
        output["chronology"] = _chronology(restored)
    return output


def fitting_plots(restored):
    """Return the desktop fitting control's frequency, PDF, CDF, PP and QQ views."""
    return _fitting(restored)
