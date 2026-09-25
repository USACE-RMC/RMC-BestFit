"""Plot completed response-model API snapshots without CLR or examples imports."""
from __future__ import annotations

import math
from datetime import datetime
import xml.etree.ElementTree as ET

from .common import area, axis, bars, line, plot, scatter, source_identity
from .response_models import horizontal_band, response_frequency_spec, time_series_curve_spec


def _pairs(items):
    return [p["x"] for p in items], [p["y"] for p in items]


def _point_name(snapshot):
    xml = snapshot.get("analysisXml")
    if xml:
        node = ET.fromstring(xml).find(".//BayesianAnalysis")
        if node is not None and node.get("PointEstimator") == "PosteriorMean":
            return "Posterior Mean"
    return "Posterior Mode"


def _unit(snapshot, default="Value"):
    return snapshot.get("unitLabel") or f"{default} (unit unavailable)"


def _bivariate(snapshot):
    data = snapshot.get("bivariatePlot")
    if not data:
        raise ValueError("Completed bivariate plot geometry is absent")
    source = source_identity(snapshot)
    output = {}
    for cdf in (False, True):
        suffix = "cdf" if cdf else "values"
        observed = data["observedCdf" if cdf else "observed"]
        simulated = data["simulatedCdf" if cdf else "simulated"]
        ox, oy = _pairs(observed)
        sx, sy = _pairs(simulated)
        exact = scatter("Exact Data", ox, oy, color="#4b81fa", size=40)
        for kind in ("scatter", "density", "joint_exceedance"):
            if kind == "scatter":
                series = [scatter("Simulated Data", sx, sy, color="#fa6d6d", marker="s", size=18,
                                  alpha=175/255), exact]
            else:
                series = [dict(name="Contours", kind="contour",
                    x=data["xCdf" if cdf else "xGrid"], y=data["yCdf" if cdf else "yGrid"],
                    z=data["logPdf" if kind == "density" else "jointExceedance"],
                    style={"levels": data["densityLevels"] if kind == "density" else
                           [.001, .002, .005, .01, .02, .05, .1, .2, .5], "color": "black"}), exact]
            variant = f"{kind}_{suffix}"
            output[variant] = plot("bivariate.distribution", source, "Copula",
                axis("Marginal X - Probability" if cdf else "Marginal X"),
                axis("Marginal Y - Probability" if cdf else "Marginal Y"), series, variant)
    return output


def _coincident(snapshot):
    r = snapshot["results"]
    source = source_identity(snapshot)
    spec = response_frequency_spec(source, r["zValues"], r["aepMode"], r["aepMean"],
        r["ciLower"], r["ciUpper"], r["credibleIntervalWidth"], _point_name(snapshot),
        "Response (Z)")
    return {"frequency": spec}


def _histogram(bins):
    if not bins:
        return None
    lower = [float(b["lowerBound"]) for b in bins]
    upper = [float(b["upperBound"]) for b in bins]
    frequencies = [float(b["frequency"]) for b in bins]
    widths = [b-a for a,b in zip(lower,upper)]
    total = sum(f*w for f,w in zip(frequencies,widths))
    return bars("Residuals", [(a+b)/2 for a,b in zip(lower,upper)],
        [f/total if total else 0 for f in frequencies], width=widths,
        color="#688caf", alpha=125/255, edgecolor="#353b7a")


def _residual_views(snapshot, family, x, xaxis):
    data = snapshot.get("residualPlot")
    if not data:
        raise ValueError(f"Completed {family} residual geometry is absent")
    source = source_identity(snapshot)
    residuals = data["residuals"]
    if len(x) != len(residuals):
        raise ValueError(f"Completed {family} residual coordinates are misaligned")
    valid = [(a,b) for a,b in zip(x,residuals) if a is not None and b is not None
             and math.isfinite(float(b)) and (isinstance(a, str) or math.isfinite(float(a)))]
    series = [scatter("Residuals", [a for a,b in valid], [b for a,b in valid], color="black")]
    if valid:
        limits = [min(a for a,b in valid), max(a for a,b in valid)]
        series.append(line("Zero", limits, [0,0], color="black", linestyle="--", legend=False, linewidth=2))
    output = {"residuals":plot(f"{family}.residuals",source,"Residuals",xaxis,
                             axis("Residual" if family == "time_series_analysis" else "Residuals"),series)}
    hist = _histogram(data.get("histogram"))
    hseries = [hist] if hist else []
    pdf = data.get("normalPdf") or []
    if pdf:
        px,py = _pairs(pdf)
        hseries.append(line("Normal Density",px,py,color="black"))
    output["residual_histogram"] = plot(f"{family}.residual_histogram",source,"Residual Histogram",
        axis("Residuals"),axis("Density"),hseries)
    qq = data.get("qq") or []
    qseries=[]
    if qq:
        qx,qy=_pairs(qq)
        qseries=[scatter("Residuals",qx,qy,color="black"),
                 line("1:1 Line",[min(qx),max(qx)],[min(qx),max(qx)],color="black",linestyle="--")]
    output["residual_qq"] = plot(f"{family}.residual_qq",source,"Residual Q-Q Plot",
        axis("Quantile (Standardized)"),axis("Quantile (Residuals)"),qseries,
        omissions=[] if qseries else ["Residual normal quantiles are undefined."])
    return output


def _rating(snapshot):
    curve = snapshot["results"]["ratingCurve"]
    if curve is None:
        raise ValueError("Completed rating curve is absent")
    source = source_identity(snapshot)
    stage, mode, mean = curve["stages"], curve["modeCurve"], curve["meanCurve"]
    lower, upper, level = curve["ciLower"],curve["ciUpper"],curve["credibleIntervalWidth"]
    observed = snapshot.get("residualPlot",{}).get("alignedObservations") or []
    ox,oy=_pairs(observed)
    series=[horizontal_band(f"{100*level:g}% Credible Intervals",mean,stage,lower,upper,level,
                            color="#353b7a",facecolor="#688caf",alpha=75/255),
            line("Posterior Predictive",mean,stage,color="blue",linestyle="--"),
            line(_point_name(snapshot),mode,stage,color="black"),
            scatter("Stage-Discharge Data",ox,oy,color="red")]
    output={"curve":plot("rating.curve",source,"Rating Curve",axis("Discharge"),axis("Stage"),series)}
    output.update(_residual_views(snapshot,"rating",snapshot["residualPlot"]["fitted"],axis("Log₁₀ Fitted Values")))
    return output


def _time_series(snapshot):
    r = snapshot["results"]
    curve = r["curve"]
    if not curve:
        raise ValueError("Completed time-series curve is absent")
    observed = next((s["points"] for s in snapshot.get("series",[]) if s["name"]=="observed"),None)
    dates = snapshot.get("resultDates")
    if not observed or not dates or len(dates)!=len(curve["meanCurve"]):
        raise ValueError("Exact completed time-series date grid is absent or misaligned")
    date_values=[point["date"] for point in observed]
    unit=_unit(snapshot)
    source=source_identity(snapshot)
    output={"series":time_series_curve_spec(source,date_values,[p["value"] for p in observed],
        dates[:len(curve["modeCurve"])],curve["modeCurve"],dates,curve["meanCurve"],
        curve["ciLower"],curve["ciUpper"],r["trainingTimeSteps"],curve["credibleIntervalWidth"],
        _point_name(snapshot),unit)}
    residual=snapshot.get("residualPlot") or {}
    residual_dates = [None if date is None else
                      (datetime.fromisoformat(date.replace("Z", "+00:00")).replace(tzinfo=None)
                       - datetime(1899,12,30)).total_seconds()/86400
                      for date in residual.get("dates",[])]
    output.update(_residual_views(snapshot,"time_series_analysis",residual_dates,axis(unit)))
    confidence=residual.get("correlationConfidenceInterval") or []
    for kind in ("acf","pacf"):
        title="Partial Autocorrelation" if kind=="pacf" else "Autocorrelation"
        pairs=residual.get(kind) or []
        spec=[]
        if pairs:
            lag,values=_pairs(pairs)
            spec=[bars(title,[i+.5 for i in range(len(values))],values,
                       color="#688caf",alpha=125/255,edgecolor="#353b7a")]
            for label,bound in zip(("2.5% CI","97.5% CI"),confidence):
                spec.append(line(label,[0,len(values)],[bound,bound],color="black",linestyle="--"))
        output[f"residual_{kind}"]=plot(f"time_series_analysis.residual_{kind}",source,"Residual " + title + " Function",
            axis("Lag"),axis(kind.upper()),spec,omissions=[] if spec else
            ["The app requires at least 10 observations and no missing values for correlation plots."])
    return output


def response_plots(snapshot):
    """Return supported response-model views from one completed API snapshot."""
    if snapshot.get("state") != "succeeded":
        raise ValueError("A completed plot source is required")
    kind=str(snapshot.get("kind","")).lower()
    if kind=="bivariate": return _bivariate(snapshot)
    if kind=="coincidentfrequency": return _coincident(snapshot)
    if kind=="ratingcurve": return _rating(snapshot)
    if kind=="timeseries": return _time_series(snapshot)
    raise ValueError(f"Unsupported response kind: {kind}")
