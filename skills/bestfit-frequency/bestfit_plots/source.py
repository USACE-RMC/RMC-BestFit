"""Load versioned, local plotting artifacts without running an analysis."""
from copy import deepcopy
import gzip
import json
from pathlib import Path

from .spec import validate_spec


def plots_from_source(source, *, unit_label=None, parameter=0, second_parameter=1,
                      include_warmup=False, show_prior=False, influence_view=None):
    """Accept PlotSpec, cached case, or a completed API plot-source response."""
    source = deepcopy(source)
    if source.get("version") == 1 and "plotId" in source:
        plots = {source["plotId"]: source}
    elif source.get("schemaVersion") == 1 and "plots" in source:
        plots = source["plots"]
    elif source.get("schemaVersion") == 1 and "analysisId" in source:
        if (source.get("success") is not True or str(source.get("state")).lower() != "succeeded"
                or not source.get("analysisId") or not source.get("lastRunUtc")):
            raise ValueError("An API plot source must identify a successful completed run")
        if unit_label:
            source["unitLabel"] = unit_label
        kind = str(source.get("kind", "")).lower()
        if any(word in kind for word in ("coincident", "bivariate", "rating", "timeseries", "arimax")):
            from .adapters.api_response_models import response_plots
            plots = response_plots(source)
        else:
            from .adapters.frequency import frequency_plots
            plots = frequency_plots(source)
        from .adapters.diagnostics import diagnostic_plots
        plots.update({"diagnostic_" + key: value for key, value in
                      diagnostic_plots(source, parameter, second_parameter, include_warmup=include_warmup,
                                       show_prior=show_prior, influence_view=influence_view).items()})
    else:
        raise ValueError("Expected PlotSpec v1, saved case v1, or API plot-source v1")
    if not plots:
        raise ValueError("The artifact contains no supported plot views")
    for spec in plots.values():
        validate_spec(spec)
    return plots


def load_plots(path, **options):
    """Read plain or gzipped JSON; return validated named PlotSpec objects."""
    data = Path(path).read_bytes()
    if data.startswith(b"\x1f\x8b"):
        data = gzip.decompress(data)
    return plots_from_source(json.loads(data), **options)


def add_frequency_comparison(base, alternative, name, *, color="#ff4a46"):
    """Overlay supplied result curves, preserving both run identities and app styling."""
    validate_spec(base)
    validate_spec(alternative)
    if not base["plotId"].endswith(".frequency") or not alternative["plotId"].endswith(".frequency"):
        raise ValueError("Comparison overlays require frequency plots")
    if base["axes"] != alternative["axes"]:
        raise ValueError("Comparison axes, units, and labels must match")
    if not isinstance(name, str) or not name.strip():
        raise ValueError("Comparison requires an explicit alternative name")
    combined = deepcopy(base)
    curves = [s for s in alternative["series"] if s["kind"] == "band" or
              (s["kind"] == "line" and s["name"] in {
                  "Posterior Mode", "Posterior Mean", "Posterior Predictive", "Computed", "Expected Probability"})]
    if not curves:
        raise ValueError("Comparison source contains no completed result curves")
    for original in curves:
        curve = deepcopy(original)
        curve["name"] = name + " - " + curve["name"]
        curve.setdefault("style", {})["color"] = color
        if curve["kind"] == "band":
            curve["style"].update(facecolor=color, color="none", alpha=100/255)
        combined["series"].append(curve)
    combined["variant"] = "comparison"
    combined["comparedSources"] = combined.get("comparedSources", [deepcopy(base["source"])]) + [deepcopy(alternative["source"])]
    validate_spec(combined)
    return combined
