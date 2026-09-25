"""Small geometry helpers shared by Pythonnet and HTTP source adapters."""
from __future__ import annotations
import math
import hashlib
import json


def source_identity(restored):
    """Bind figures to the source, analysis configuration and saved/run identity."""
    if restored.get("plotSourceIdentity"):
        return dict(restored["plotSourceIdentity"])
    if "analysisId" in restored:
        return dict(kind="api", id=str(restored["analysisId"]), runId=str(restored["lastRunUtc"]))
    identity = {"sha256": restored["source"]["sha256"], "table": restored.get("table"),
                "name": restored["name"], "analysisXml": restored.get("row", {}).get("AnalysisXml")}
    digest = hashlib.sha256(json.dumps(identity, sort_keys=True).encode()).hexdigest()
    return dict(kind="saved", id=restored["name"], runId="sha256:"+digest)


def clean(values):
    """Convert nonfinite coordinates to explicit JSON null gaps."""
    return [None if v is None or (isinstance(v, str) and v in {"NaN", "Infinity", "-Infinity"})
            or (not isinstance(v, str) and not math.isfinite(float(v)))
            else v if isinstance(v, str) else float(v) for v in values]


def axis(label, scale="linear", value="value", unit=""):
    return dict(label=label, unit=unit, scale=scale, value=value)


def plot(plot_id, source, title, x, y, series, variant="default", omissions=None):
    return dict(version=1, plotId=plot_id, variant=variant, source=dict(source), title=title,
                axes=dict(x=x, y=y), series=series, omissions=omissions or [])


def line(name, x, y, **style):
    return dict(name=name, kind="line", x=clean(x), y=clean(y), style=style)


def scatter(name, x, y, **style):
    return dict(name=name, kind="scatter", x=clean(x), y=clean(y), style=style)


def area(name, x, lower, upper, interval=None, **style):
    result = dict(name=name, kind="band" if interval else "area", x=clean(x),
                  y=clean(upper), yLower=clean(lower), yUpper=clean(upper), style=style)
    if interval:
        result["interval"] = interval
    return result


def bars(name, x, heights, width=1.0, **style):
    return dict(name=name, kind="bars", x=clean(x), y=clean(heights), width=width, style=style)


def column(matrix, j):
    """Extract a .NET rectangular matrix column without implicit flattening."""
    return [float(matrix[i, j]) for i in range(matrix.GetLength(0))]


def net_array(values):
    from System import Array, Double
    return Array[Double]([float(v) for v in values])


def correlation_plot(values, source, plot_id, partial=False):
    """Use the app's Numerics ACF/PACF and confidence bounds, including its guards."""
    from Numerics.Data.Statistics import Autocorrelation
    title = "Partial Autocorrelation" if partial else "Autocorrelation"
    if len(values) < 10 or any(not math.isfinite(float(v)) for v in values):
        return plot(plot_id, source, title + " Function", axis("Lag"), axis(title), [],
                    omissions=["The app requires at least 10 observations and no missing values for correlation plots."])
    a = net_array(values)
    matrix = Autocorrelation.Function(a, -1, Autocorrelation.Type.Partial) if partial else Autocorrelation.Function(a)
    y = column(matrix, 1)
    ci = list(Autocorrelation.CorrelationConfidenceInterval(len(values)))
    data = [bars(title, [i + .5 for i in range(len(y))], y,
                 color="#688caf", alpha=125/255, edgecolor="#353b7a")]
    for label, bound in zip(("2.5% CI", "97.5% CI"), ci):
        data.append(line(label, [0, len(y)], [bound, bound], color="black", linestyle="--"))
    return plot(plot_id, source, title + " Function", axis("Lag"), axis(title), data)


def histogram(values, name="Histogram", default_bins=False):
    """Match the app's natural-log Sturges bin count and Numerics bin edges."""
    from Numerics.Data.Statistics import Histogram
    h = Histogram(net_array(values)) if default_bins else Histogram(net_array(values), int(1 + 3.322 * math.log(len(values))))
    normalization = sum(float(h[i].Frequency) * float(h.BinWidth) for i in range(h.NumberOfBins))
    widths = [float(h[i].UpperBound - h[i].LowerBound) for i in range(h.NumberOfBins)]
    return bars(name, [float((h[i].LowerBound + h[i].UpperBound)/2) for i in range(h.NumberOfBins)],
                [float(h[i].Frequency * h.BinWidth / normalization / widths[i]) for i in range(h.NumberOfBins)],
                width=widths, color="#688caf", alpha=125/255, edgecolor="#353b7a")


def density(values, name="Density"):
    from Numerics.Distributions import KernelDensity
    from Numerics.Sampling import Stratify, StratificationOptions
    a = net_array(values)
    x = Stratify.XValues(StratificationOptions(min(values), max(values), 1000))
    pdf = KernelDensity(a).CreatePDFGraph(x)
    return area(name, column(pdf, 0), [0]*pdf.GetLength(0), column(pdf, 1),
                color="#353b7a", facecolor="#688caf", alpha=125/255)
