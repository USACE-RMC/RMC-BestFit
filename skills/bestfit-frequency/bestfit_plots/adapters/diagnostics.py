"""Shared saved-sample diagnostic geometry used by BestFit desktop controls."""
from __future__ import annotations

import math
import xml.etree.ElementTree as ET

from .common import area, axis, bars, column, line, plot, source_identity
from .frequency import _get, _kind


def _is_b17c(restored):
    kind = _kind(restored)
    return "b17c" in kind or "bulletin17c" in kind or restored.get("sampleOrigin") == "bulletin17CUncertainty"


def _pairs(values):
    if values is None:
        return []
    if hasattr(values, "GetLength"):
        return [(float(values[i, 0]), float(values[i, 1])) for i in range(values.GetLength(0))]
    return [(float(_get(v, "x", "X")), float(_get(v, "y", "Y"))) for v in values]


def _parameters(restored):
    prepared = restored.get("parameterDiagnostics")
    if prepared is not None:
        return list(prepared)
    results = _get(_get(restored.get("analysis"), "BayesianAnalysis"), "Results")
    return list(_get(results, "ParameterResults", default=[]) or [])


def _sample_values(sample):
    values = _get(sample, "values", "Values")
    return [float(v) for v in values] if values is not None else []


def _saved_samples(restored):
    item = restored.get("samples")
    if item is None:
        item = _get(_get(restored.get("analysis"), "BayesianAnalysis"), "Results")
    return item


def _parameter_name(restored, index):
    prepared = restored.get("parameterDiagnostics") or []
    if index < len(prepared):
        label = _get(prepared[index], "displayName", "DisplayName", "parameterName", "ParameterName")
        if label:
            return str(label)
    analysis = restored.get("analysis")
    names = list(_get(_get(analysis, "BayesianAnalysis"), "ParameterNames", default=[]) or [])
    if index < len(names) and names[index]:
        return str(names[index])
    model = restored.get("model")
    parameters = _get(model, "Parameters", default=[]) or []
    if index < len(parameters):
        return str(_get(parameters[index], "DisplayName", "Name", default=f"Parameter {index+1}") or f"Parameter {index+1}")
    return f"Parameter {index+1}"


def _trace_window(restored, include_warmup=False):
    settings = _get(restored.get("analysis"), "BayesianAnalysis")
    warmup = _get(settings, "WarmupIterations")
    iterations = _get(settings, "Iterations")
    if warmup is None and restored.get("analysisXml"):
        bayes = ET.fromstring(restored["analysisXml"]).find(".//BayesianAnalysis")
        if bayes is not None:
            warmup, iterations = bayes.get("WarmupIterations"), bayes.get("Iterations")
    return (0 if include_warmup else max(0, int(warmup)-1)) if warmup is not None else 0, \
        int(iterations) if iterations is not None else None


def _histogram_bins(restored, selected):
    prepared = _get(selected, "histogram")
    if prepared is not None:
        if prepared and _get(prepared[0], "lowerBound", "LowerBound") is not None:
            lower = [float(_get(item, "lowerBound", "LowerBound")) for item in prepared]
            upper = [float(_get(item, "upperBound", "UpperBound")) for item in prepared]
            frequencies = [float(_get(item, "frequency", "Frequency")) for item in prepared]
            widths = [b-a for a, b in zip(lower, upper)]
            total_area = sum(f*w for f, w in zip(frequencies, widths))
            return [(a+b)/2 for a, b in zip(lower, upper)], \
                [f/total_area if total_area else 0. for f in frequencies], widths
        points = _pairs(prepared)
        if not points:
            return None
        x, y = zip(*points)
        width = abs(x[1]-x[0]) if len(x) > 1 else 1.
        total_area = sum(v * width for v in y)
        return list(x), [v/total_area if total_area else 0. for v in y], width
    h = _get(selected, "Histogram")
    if h is None:
        return None
    n = int(h.NumberOfBins)
    if not n:
        return None
    width = float(h.BinWidth)
    # HistogramControl uses the stored bin frequencies and its own normalization.
    denominator = sum(float(h[i].Frequency) * width for i in range(n))
    x = [(float(h[i].LowerBound)+float(h[i].UpperBound))/2 for i in range(n)]
    y = [float(h[i].Frequency) / denominator if denominator else 0. for i in range(n)]
    return x, y, width


def _pair_heatmap(restored, source, samples, first, second):
    output = _get(samples, "output", "Output", default=[]) or []
    values = [_sample_values(s) for s in output]
    values = [v for v in values if len(v) > max(first, second) and all(math.isfinite(v[j]) for j in (first, second))]
    if not values:
        return None
    x_values, y_values = [v[first] for v in values], [v[second] for v in values]
    min_x, max_x, min_y, max_y = min(x_values), max(x_values), min(y_values), max(y_values)
    if min_x == max_x or min_y == max_y:
        return None
    bins = 16
    dx, dy = (max_x-min_x)/(bins-1), (max_y-min_y)/(bins-1)
    x = [min_x+i*dx for i in range(bins)]
    y = [min_y+i*dy for i in range(bins)]
    z = [[0.]*bins for _ in range(bins)]
    for xv, yv in zip(x_values, y_values):
        xi = max(0, min(bins-1, math.floor((xv-min_x)/dx)))
        yi = max(0, min(bins-1, math.floor((yv-min_y)/dy)))
        z[yi][xi] += 1/len(values)
    label_x, label_y = _parameter_name(restored, first), _parameter_name(restored, second)
    title = "Joint Uncertainty Density" if _is_b17c(restored) else "Joint Posterior Density"
    nonzero = [value for row in z for value in row if value > 0]
    minimum = min(nonzero) if nonzero else 0.
    maximum = max(nonzero) if nonzero else 0.
    step = (maximum-minimum)/5
    levels = [round(minimum+step*(i+1), 5) for i in range(6)]
    series = [
        dict(name="Bivariate HeatMap", kind="heatmap", x=x, y=y, z=z, style={"legend": False}),
        dict(name="Bivariate Contour", kind="contour", x=x, y=y, z=z,
             style={"levels": levels, "colors": "black", "legend": False}),
    ]
    return plot("shared_diagnostics.pair_heatmap", source, f"{title} of {label_x} and {label_y}",
                axis(label_x), axis(label_y), series)


def _observation_label(item, fallback):
    explicit = _get(item, "label", "Label")
    if explicit and " - " in str(explicit):
        return str(explicit)
    data_type = str(_get(item, "dataType", "DataType", default="Obs")).split(".")[-1]
    prefix = {"Exact": "Exact", "Uncertain": "Uncertain", "Interval": "Interval",
              "LeftCensored": "Threshold", "RightCensored": "Threshold"}.get(data_type, "Obs")
    name = _get(item, "name", "Name")
    index = _get(item, "index", "Index", default=fallback)
    value = _get(item, "value", "Value")
    if value is None:
        return str(explicit or name or index)
    return f"{prefix} - {name if name else index} - {float(value):.4g}"


def _influence(restored, source, view=None):
    b17c = _is_b17c(restored)
    view = view or ("gmm_fit" if b17c else "bayesian_leverage")
    valid = {"bayesian_leverage", "bayesian_fit", "bayesian_variance", "gmm_fit", "gmm_variance", "leave_one_out"}
    if view not in valid:
        raise ValueError(f"Unknown influence view: {view}")
    if (view.startswith("gmm_") and not b17c) or (view.startswith("bayesian_") and b17c) or (view == "leave_one_out" and b17c):
        raise ValueError(f"Influence view {view} is incompatible with this analysis")
    diagnostics = restored.get("influenceDiagnostics")
    analysis = restored.get("analysis")
    if diagnostics is None and analysis is not None:
        if view == "leave_one_out":
            method = _get(_get(analysis, "BayesianAnalysis"), "ComputeInfluenceDiagnostics")
            if callable(method):
                computed = method()
                diagnostics = {"leaveOneOut": list(computed.Observations)}
        else:
            method = _get(_get(analysis, "GMM") if b17c else _get(analysis, "BayesianAnalysis"),
                          "GetLeverageDiagnostics" if b17c else "ComputeLeverageDiagnostics")
            if callable(method): diagnostics = method()
    if view == "leave_one_out":
        observations = _get(diagnostics, "leaveOneOut", "LeaveOneOut", default=[]) or []
        if not observations: return None
        ordered = list(reversed(sorted(observations,
            key=lambda item: abs(float(_get(item,"elpdLoo","ElpdLoo"))), reverse=True)[:20]))
        categories = (("Good", "Good (k < 0.5)", "#4caf50"),
                      ("OK", "OK (0.5 ≤ k < 0.7)", "#ffc107"),
                      ("Bad", "Bad (0.7 ≤ k < 1.0)", "#ff9800"),
                      ("VeryBad", "Very Bad (k ≥ 1.0)", "#f44336"))
        labels = [_observation_label(item, i+1) for i,item in enumerate(ordered)]
        colors = {"Good": ("#4caf50", "#388e3c"), "OK": ("#ffc107", "#ffa000"),
                  "Bad": ("#ff9800", "#e67e00"), "VeryBad": ("#f44336", "#d32f2f")}
        series = [bars(name, list(range(len(ordered))),
            [abs(float(_get(item,"elpdLoo","ElpdLoo"))) if
             str(_get(item,"category","Category")).split(".")[-1] == category else 0 for item in ordered],
            facecolor=colors[category][0], edgecolor=colors[category][1], alpha=75/255,
            labels=labels, orientation="horizontal") for category,name,color in categories
            if any(str(_get(item,"category","Category")).split(".")[-1] == category for item in ordered)]
        return plot("shared_diagnostics.influence", source, "Leave-One-Out Predictive Surprise",
            axis("Observations", "linear", "index"), axis("LOO Predictive Surprise (|ĒLPD|)"), series,
            variant=view)
    observations = _get(diagnostics, "observations", "Observations", default=[]) or []
    if not observations: return None
    priors = _get(diagnostics, "priorComponents", "PriorComponents", default=[]) or []
    metric = "leverage" if view.endswith("leverage") else "fitInfluence" if view.endswith("fit") else "varianceInfluence"
    def value(item, name):
        return float(_get(item, name, name[0].upper()+name[1:], default=0))
    combined = [(item, True) for item in observations] + [(item, False) for item in priors]
    ordered = list(reversed(sorted(combined, key=lambda pair: value(pair[0],metric), reverse=True)[:20]))
    labels = [_observation_label(item, i+1) if is_obs else str(_get(item,"label","Label","name","Name",default=i+1))
              for i,(item,is_obs) in enumerate(ordered)]
    title = "Combined Leverage (% of Total Influence)" if metric == "leverage" else \
        "Fit Influence (Cook's Distance)" if metric == "fitInfluence" else "Variance Influence"
    ordinate = "% of Total Influence" if metric == "leverage" else \
        "Fit Influence" if metric == "fitInfluence" else "Variance Influence"
    series=[]
    for is_observation,name,color in ((True,"Observations","#dc143c"),
                                      (False,"Penalties" if b17c else "Priors","#688caf")):
        positions=[i for i,(_,is_obs) in enumerate(ordered) if is_obs==is_observation]
        if positions:
            series.append(bars(name, positions,
                [value(ordered[i][0],"percentOfTotal") if metric=="leverage" else value(ordered[i][0],metric)
                 for i in positions], facecolor=color,
                edgecolor="#ff0000" if is_observation else "#353b7a",
                alpha=(75 if is_observation else 125)/255,
                labels=[labels[i] for i in positions],orientation="horizontal"))
    return plot("shared_diagnostics.influence",source,title,
                axis("","linear","index"),axis(ordinate),series,variant=view)


def diagnostic_plots(restored, parameter=0, second_parameter=1, *, include_warmup=False,
                     show_prior=False, influence_view=None):
    """Return available diagnostics; never create a Bayesian chain for B17C GMM."""
    kind = _kind(restored)
    if any(name in kind for name in ("composite", "coincident", "fitting")):
        return {}
    source = source_identity(restored)
    selected = _parameters(restored)
    if parameter < 0 or second_parameter < 0:
        raise ValueError("Parameter indices must be nonnegative")
    selected = selected[parameter] if parameter < len(selected) else None
    samples = _saved_samples(restored)
    output = {}
    label = _parameter_name(restored, parameter)
    b17c = _is_b17c(restored)
    if selected is not None:
        bins = _histogram_bins(restored, selected)
        if bins:
            x, y, width = bins
            hseries = []
            if show_prior:
                prior = _pairs(_get(selected, "priorDensity", "PriorDensity"))
                if not prior:
                    model = restored.get("model")
                    parameters = _get(model, "Parameters", default=[]) or []
                    if parameter < len(parameters):
                        prior = _pairs(_get(parameters[parameter], "PriorDistribution").CreatePDFGraph())
                if prior:
                    px, py = zip(*prior)
                    hseries.append(area(_get(selected, "priorName", "PriorName", default="Prior Density"),
                                        px, [0]*len(px), py, facecolor="#688caf", edgecolor="#353b7a",
                                        linewidth=1, alpha=125/255))
            hseries.append(bars("Uncertainty Histogram" if b17c else "Posterior Histogram", x, y, width=width,
                                facecolor="#dc143c", edgecolor="#ff0000", linewidth=1, alpha=75/255))
            output["histogram"] = plot("shared_diagnostics.histogram", source,
                f"{'Marginal' if b17c else 'Marginal Posterior'} Histogram of {label}",
                axis(label), axis("Density"), hseries,
                variant="prior" if show_prior else "posterior")
        kde = _pairs(_get(selected, "kernelDensity", "KernelDensity"))
        if kde:
            x, y = zip(*kde)
            output["kde"] = plot("shared_diagnostics.kde", source,
                f"{'Marginal' if b17c else 'Marginal Posterior'} Density of {label}",
                axis(label), axis("Density"), [area("Uncertainty Density" if b17c else "Posterior Density", x, [0.]*len(x), y,
                    facecolor="#dc143c", edgecolor="#ff0000", linewidth=1, alpha=75/255)])
        if not b17c:
            acf = _pairs(_get(selected, "autocorrelation", "Autocorrelation"))
            if acf:
                _, y = zip(*acf)
                x = [i+.5 for i in range(len(y))]
                series = [bars("Autocorrelation", x, y, width=1., facecolor="#dc143c",
                               edgecolor="#ff0000", linewidth=1, alpha=75/255)]
                ci = restored.get("acfConfidenceInterval")
                if ci is None:
                    analysis = restored.get("analysis")
                    results = _get(_get(analysis, "BayesianAnalysis"), "Results")
                    draws = _get(results, "Output", default=[]) or []
                    width = _get(_get(analysis, "BayesianAnalysis"), "CredibleIntervalWidth")
                    if len(draws) and width is not None:
                        from Numerics.Data.Statistics import Autocorrelation
                        ci = list(Autocorrelation.CorrelationConfidenceInterval(len(draws), float(width)))
                if ci is not None:
                    level = float(_get(_get(restored.get("analysis"), "BayesianAnalysis"),
                                       "CredibleIntervalWidth", default=restored.get("credibleIntervalWidth", .95)))
                    alpha = (1-level)/2
                    series.extend([
                        line(f"{alpha*100:.1f}% CI", [x[0], x[-1]], [float(ci[0])]*2,
                             color="black", linestyle="--", linewidth=2, legend=False),
                        line(f"{(1-alpha)*100:.1f}% CI", [x[0], x[-1]], [float(ci[1])]*2,
                             color="black", linestyle="--", linewidth=2, legend=False),
                    ])
                output["acf"] = plot("shared_diagnostics.acf", source, f"Autocorrelation of {label}",
                    axis("Lag"), axis("Autocorrelation"), series)
    if samples is not None:
        pair = None if "bivariate" in kind else _pair_heatmap(restored, source, samples, parameter, second_parameter)
        if pair:
            output["pair_heatmap"] = pair
        if not b17c:
            chains = _get(samples, "markovChains", "MarkovChains", default=[]) or []
            trace = []
            start, end = _trace_window(restored, include_warmup)
            trace_colors = ["#f492ec", "#94c899", "#c6a5ea", "#aba2d0", "#a799a2", "#c6dac8"]
            for i, chain in enumerate(chains):
                values = [_sample_values(sample) for sample in list(chain)[start:end]]
                y = [row[parameter] for row in values if len(row) > parameter]
                if y:
                    trace.append(line(f"Chain {i+1}", list(range(start+1, start+len(y)+1)), y,
                                      color=trace_colors[i % len(trace_colors)], linewidth=1.5))
            if trace:
                output["trace"] = plot("shared_diagnostics.trace", source, f"Trace of {label}",
                                       axis("Iteration"), axis(label), trace,
                                       variant="warmup" if include_warmup else "chain")
    if not b17c:
        values = restored.get("meanLogLikelihood")
        if values is None:
            values = _get(_get(_get(restored.get("analysis"), "BayesianAnalysis"), "Results"), "MeanLogLikelihood")
        if values is not None and len(values):
            output["mean_log_likelihood"] = plot("shared_diagnostics.mean_log_likelihood", source,
                "Mean Log-Likelihood", axis("Iteration"), axis("Mean Log-Likelihood"),
                [line("Mean Log-Likelihood", list(range(1, len(values)+1)), values,
                      color="#dc143c", linewidth=2)])
    influence = _influence(restored, source, influence_view)
    if influence:
        output["influence"] = influence
    return output
