"""Bivariate, coincident, rating and time-series app plot preparation."""
from __future__ import annotations
import math
from .common import (area, axis, clean, column, correlation_plot, histogram, line,
                     net_array, plot, scatter, source_identity)
from .input_data import observation_plot, observations_from_dataframe


def _point_name(analysis):
    return "Posterior Mean" if str(analysis.BayesianAnalysis.PointEstimator) == "PosteriorMean" else "Posterior Mode"


def paired_observations(x_rows, y_rows, cdf=False):
    """Match the app's sorted, low-outlier-excluding two-pointer join."""
    x_rows = sorted((r for r in x_rows if not r.get("lowOutlier")), key=lambda r:r["index"])
    y_rows = sorted((r for r in y_rows if not r.get("lowOutlier")), key=lambda r:r["index"])
    i = j = 0
    pairs = []
    while i < len(x_rows) and j < len(y_rows):
        x, y = x_rows[i], y_rows[j]
        if x["index"] == y["index"]:
            pairs.append((1-x["aep"] if cdf else x["value"], 1-y["aep"] if cdf else y["value"]))
            i += 1
            j += 1
        elif x["index"] < y["index"]:
            i += 1
        else:
            j += 1
    return pairs


def bivariate_plots(restored):
    """Six app variants; simulated points use the original output length and seed."""
    from Numerics.Sampling import Stratify, StratificationOptions
    m, analysis = restored["model"], restored["analysis"]
    source = source_identity(restored)
    x_dist, y_dist, copula = m.MarginalX.Distribution.Clone(), m.MarginalY.Distribution.Clone(), m.Copula.Clone()
    xrows = [r for r in observations_from_dataframe(m.MarginalX.DataFrame) if r["kind"] == "exact"]
    yrows = [r for r in observations_from_dataframe(m.MarginalY.DataFrame) if r["kind"] == "exact"]
    observed = paired_observations(xrows, yrows)
    if not observed:
        raise ValueError("Bivariate view requires matched exact observations")
    simulated = m.GenerateRandomValues(min(10000, int(analysis.BayesianAnalysis.OutputLength)), int(analysis.BayesianAnalysis.PRNGSeed))
    sx, sy = column(simulated, 0), column(simulated, 1)
    p = 1 / 10**math.ceil(math.log10(len(observed)) + 2)
    def grid(dist):
        bins = list(Stratify.XValues(StratificationOptions(dist.InverseCDF(p), dist.InverseCDF(1-p), 99), False))
        return [float(b.LowerBound) for b in bins]+[float(bins[-1].UpperBound)]
    gx, gy = grid(x_dist), grid(y_dist)
    fx, fy = [float(x_dist.CDF(x)) for x in gx], [float(y_dist.CDF(y)) for y in gy]
    # PlotSpec matrices are rows(y) by columns(x); OxyPlot's Data is indexed [x,y].
    log_pdf = [[float(copula.LogPDF(x, y)+x_dist.LogPDF(gx[i])+y_dist.LogPDF(gy[j]))
                for i, x in enumerate(fx)] for j, y in enumerate(fy)]
    exceedance = [[float(copula.ANDJointExceedanceProbability(x, y)) for x in fx] for y in fy]
    maxf = max(v for row in log_pdf for v in row)
    minf = math.log(1 / 10**math.ceil(math.log10(1 / math.exp(maxf)) + 2.5))
    zbins = list(Stratify.XValues(StratificationOptions(minf, maxf, 9), False))
    density_levels = [float(b.LowerBound) for b in zbins]+[float(zbins[-1].UpperBound)]
    output = {}
    for cdf in (False, True):
        coords = "cdf" if cdf else "values"
        pairs = paired_observations(xrows, yrows, cdf)
        exact = scatter("Exact Data", [v[0] for v in pairs], [v[1] for v in pairs], color="#4b81fa", size=40)
        for kind in ("scatter", "density", "joint_exceedance"):
            if kind == "scatter":
                simulation = scatter("Simulated Data", [float(x_dist.CDF(v)) for v in sx] if cdf else sx,
                                     [float(y_dist.CDF(v)) for v in sy] if cdf else sy,
                                     color="#fa6d6d", marker="s", size=18, alpha=175/255)
                data = [simulation, exact]
            else:
                data = [dict(name="Contours", kind="contour", x=fx if cdf else gx, y=fy if cdf else gy,
                             z=log_pdf if kind == "density" else exceedance,
                             style={"levels":density_levels if kind == "density" else [.001,.002,.005,.01,.02,.05,.1,.2,.5],
                                    "color":"black"}), exact]
            variant = f"{kind}_{coords}"
            x_label = restored["dependencies"]["MarginalX"]["input_row"].get("UnitLabel", "Value")
            y_label = restored["dependencies"]["MarginalY"]["input_row"].get("UnitLabel", "Value")
            output[variant] = plot("bivariate.distribution", source, "Copula",
                                  axis("Marginal X - Probability" if cdf else "Marginal X - " + x_label),
                                  axis("Marginal Y - Probability" if cdf else "Marginal Y - " + y_label), data, variant)
    return output


def horizontal_band(name, center, y, lower, upper, level, *, interval_kind="credible", **style):
    return dict(name=name, kind="band", x=clean(center), y=clean(y), xLower=clean(lower), xUpper=clean(upper),
                interval={"kind":interval_kind, "level":level}, style=style)


def response_frequency_spec(source, response, mode, predictive, lower, upper, level, point_name, unit="Response"):
    series = [horizontal_band(f"{100*level:g}% Credible Intervals", predictive, response, lower, upper, level,
                              color="#353b7a", facecolor="#688caf", alpha=75/255),
              line("Posterior Predictive", predictive, response, color="blue", linestyle="--"),
              line(point_name, mode, response, color="black")]
    return plot("coincident.frequency", source, "Frequency", axis("Annual Exceedance Probability", "normal_probability", "aep"), axis(unit), series)


def coincident_plots(restored):
    analysis = restored["analysis"]
    r = analysis.AnalysisResults
    if r is None:
        raise ValueError("Coincident analysis has no saved results")
    source = source_identity(restored)
    spec = response_frequency_spec(source, list(analysis.ZOutputValues), list(r.ModeCurve), list(r.MeanCurve),
                                   column(r.ConfidenceIntervals, 0), column(r.ConfidenceIntervals, 1),
                                   float(analysis.BayesianAnalysis.CredibleIntervalWidth), _point_name(analysis),
                                   "Response (Z)")
    if restored.get("input") is not None:
        spec["series"] += observation_plot(observations_from_dataframe(restored["input"]), source, "frequency", y_scale="linear")["series"]
    return {"frequency":spec}


def _residual_plots(restored, family, x, residuals, x_axis, default_bins=False):
    from Numerics.Distributions import Normal
    from Numerics.Data.Statistics import Statistics, PlottingPositions
    source, model = source_identity(restored), restored["model"]
    residuals = [float(v) for v in residuals]
    if len(x) != len(residuals):
        raise ValueError(f"Completed {family} residual coordinates are misaligned")
    valid = [(a,b) for a,b in zip(x,residuals) if a is not None and math.isfinite(b)
             and (isinstance(a,str) or math.isfinite(float(a)))]
    output = {}
    series = [scatter("Residuals", [a for a,b in valid], [b for a,b in valid], color="black")]
    if valid:
        limits = [min(a for a,b in valid), max(a for a,b in valid)]
        series.append(line("Zero", limits, [0,0], color="black", linestyle="--", legend=False, linewidth=2))
    output["residuals"] = plot(f"{family}.residuals", source, "Residuals", x_axis,
                               axis("Residual" if family == "time_series_analysis" else "Residuals"), series)
    finite = [r for r in residuals if math.isfinite(r)]
    hist = [histogram(finite, "Residuals", default_bins)] if len(finite)>=2 else []
    scale = float(list(model.Parameters)[-1].Value)
    if hist and math.isfinite(scale) and scale>0:
        pdf = Normal(0, scale).CreatePDFGraph()
        hist.append(line("Normal Density", column(pdf,0), column(pdf,1), color="black"))
    output["residual_histogram"] = plot(f"{family}.residual_histogram", source, "Residual Histogram", axis("Residuals"), axis("Density"),
                                        hist, omissions=[] if hist else ["Fewer than two finite residuals."])
    qq = []
    if len(finite)>=2:
        ordered = sorted(finite)
        moments = Statistics.MeanStandardDeviation(net_array(ordered))
        if math.isfinite(float(moments.Item2)) and float(moments.Item2)>0:
            normal = Normal(moments.Item1, moments.Item2)
            pp = PlottingPositions.Weibull(len(ordered))
            q = [float(normal.InverseCDF(p)) for p in pp]
            qq = [scatter("Residuals", q, ordered, color="black"), line("1:1 Line", [min(q),max(q)], [min(q),max(q)], color="black", linestyle="--")]
    output["residual_qq"] = plot(f"{family}.residual_qq", source, "Residual Q-Q Plot", axis("Quantile (Standardized)"), axis("Quantile (Residuals)"),
                                 qq, omissions=[] if qq else ["Residual normal quantiles are undefined."])
    return output


def rating_plots(restored):
    analysis, model = restored["analysis"], restored["model"]
    r = analysis.AnalysisResults
    if r is None or len(r.ModeCurve) != int(analysis.StageBins):
        raise ValueError("Rating results must match the saved StageBins")
    source = source_identity(restored)
    stage = column(r.ConfidenceIntervals,0)
    level = float(analysis.BayesianAnalysis.CredibleIntervalWidth)
    pairs = list(model.GetAlignedObservations())
    data = [horizontal_band(f"{100*level:g}% Prediction Intervals", list(r.MeanCurve), stage,
                            column(r.ConfidenceIntervals,1), column(r.ConfidenceIntervals,2), level,
                            interval_kind="prediction", color="#353b7a", facecolor="#688caf", alpha=75/255),
            line("Posterior Predictive", list(r.MeanCurve), stage, color="blue", linestyle="--"),
            line(_point_name(analysis), list(r.ModeCurve), stage, color="black"),
            scatter("Stage-Discharge Data", [float(p.Item3) for p in pairs], [float(p.Item2) for p in pairs], color="red")]
    output = {"curve":plot("rating.curve", source, "Rating Curve",
                           axis(restored["discharge_row"].get("UnitLabel", "Discharge")),
                           axis(restored["input_row"].get("UnitLabel", "Stage")), data)}
    parameters = net_array([p.Value for p in model.Parameters])
    residuals = list(model.Residuals(parameters))
    fitted = list(model.FittedValues(parameters))
    output.update(_residual_plots(restored, "rating", fitted, residuals, axis("Log₁₀ Fitted Values")))
    return output


def _dates(series, count):
    """Equivalent to App.TimeSeriesResultTimeline.CreateDates, including forecast dates."""
    from Numerics.Data import TimeSeries
    if str(series.TimeInterval) == "Irregular" or (count and not series.Count):
        raise ValueError("A regular, nonempty source series is required for result dates")
    date = series[0].Index
    output = []
    for _ in range(count):
        output.append(str(date.ToString("o")))
        date = TimeSeries.AddTimeInterval(date, series.TimeInterval)
    return output


def time_series_curve_spec(source, dates, observed, mode_dates, mode, prediction_dates, predictive,
                           lower, upper, training_steps, level, point_name, unit):
    split = max(0, min(len(prediction_dates)-1, training_steps-1))
    intervals = []
    for name, selection, color in (("Training", slice(0,split+1), "#688caf"),
                                    ("Prediction", slice(split,None), "crimson")):
        if name == "Prediction" and split == len(prediction_dates)-1:
            continue
        intervals.append(area(f"{100*level:g}% Prediction Intervals — {name}", prediction_dates[selection], lower[selection], upper[selection],
                              {"kind":"prediction", "level":level}, color="#353b7a" if name=="Training" else "red", facecolor=color, alpha=75/255))
    data = intervals+[line("Posterior Predictive", prediction_dates, predictive, color="blue", linestyle="--"),
                      line(point_name, mode_dates, mode, color="black"),
                      line("Time Series", dates, observed, color="red", linestyle=":")]
    finite = [float(v) for v in lower+upper+observed if v is not None and math.isfinite(float(v))]
    if finite and 0<training_steps<=len(dates):
        data.append(line("End of Training Period", [dates[training_steps-1]]*2, [min(finite),max(finite)], color="black", linestyle="--", linewidth=3))
    return plot("time_series_analysis.series", source, "Time Series", axis("Date", "date", "date"), axis(unit), data)


def time_series_analysis_plots(restored):
    model, analysis = restored["model"], restored["analysis"]
    r = analysis.AnalysisResults
    if r is None:
        raise ValueError("Time-series analysis has no saved results")
    source = source_identity(restored)
    dates = [str(o.Index.ToString("o")) for o in model.TimeSeries]
    observed = [float(o.Value) for o in model.TimeSeries]
    prediction_dates, mode_dates = _dates(model.TimeSeries, len(r.MeanCurve)), _dates(model.TimeSeries, len(r.ModeCurve))
    spec = time_series_curve_spec(source, dates, observed, mode_dates, list(r.ModeCurve), prediction_dates, list(r.MeanCurve),
                                  column(r.ConfidenceIntervals,1), column(r.ConfidenceIntervals,2), int(model.TrainingTimeSteps),
                                  float(analysis.BayesianAnalysis.CredibleIntervalWidth), _point_name(analysis),
                                  restored.get("input_row", {}).get("UnitLabel", "Value"))
    output = {"series":spec}
    parameters = net_array([p.Value for p in model.Parameters])
    residuals = list(model.Residuals(parameters))
    # Use the same observation dates, with a date axis instead of desktop's
    # legacy OLE serial-number axis titled with the response unit.
    residual_dates = [str(o.Index.ToString("o")) for o in model.TrainingTimeSeries][:len(residuals)]
    output.update(_residual_plots(restored, "time_series_analysis", residual_dates, residuals,
                                 axis("Date", "date", "date"), default_bins=True))
    for kind in ("acf", "pacf"):
        view = correlation_plot(residuals, source, f"time_series_analysis.residual_{kind}", partial=kind=="pacf")
        view["title"] = "Residual " + view["title"]
        view["axes"]["y"]["label"] = kind.upper()
        output[f"residual_{kind}"] = view
    return output
