"""JSON-only geometry contracts for completed response-model sources."""
from __future__ import annotations

import importlib.abc
import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "skills" / "bestfit-frequency"))
from bestfit_plots.adapters.api_response_models import response_plots
from bestfit_plots.adapters.frequency import frequency_plots
from bestfit_plots.adapters.diagnostics import diagnostic_plots
from bestfit_plots.spec import validate_spec


BASE = {"state": "succeeded", "analysisId": "saved-run", "lastRunUtc": "2026-09-22T12:00:00Z"}


class _NoManagedImports(importlib.abc.MetaPathFinder):
    def find_spec(self, fullname, path=None, target=None):
        if fullname.split(".")[0] in {"bestfit_examples", "clr", "Numerics", "RMC", "System"}:
            raise AssertionError(f"Managed runtime import during API rendering: {fullname}")
        return None


@pytest.fixture(autouse=True)
def no_managed_imports():
    guard = _NoManagedImports()
    sys.meta_path.insert(0, guard)
    try:
        yield
    finally:
        sys.meta_path.remove(guard)


def _validate(plots):
    for spec in plots.values():
        validate_spec(spec)
        assert spec["source"] == {"kind": "api", "id": "saved-run", "runId": BASE["lastRunUtc"]}


def _residual():
    return {"alignedObservations": [{"x": 10, "y": 2}, {"x": 20, "y": 3}],
            "fitted": [1., 2.], "residuals": [-1., 1.],
            "dates": ["2000-01-01T00:00:00", "2000-01-02T00:00:00"],
            "histogram": [{"lowerBound": -2., "upperBound": 0., "frequency": 1.},
                          {"lowerBound": 0., "upperBound": 2., "frequency": 1.}],
            "normalPdf": [{"x": -1., "y": .2}, {"x": 1., "y": .2}],
            "qq": [{"x": -1., "y": -1.}, {"x": 1., "y": 1.}],
            "acf": [{"x": 0., "y": 1.}, {"x": 1., "y": .2}],
            "pacf": [{"x": 0., "y": 1.}, {"x": 1., "y": .1}],
            "correlationConfidenceInterval": [-.5, .5]}


def test_bivariate_six_variants_use_exported_simulation_and_contours():
    data = {"observed": [{"x": 10, "y": 20}], "observedCdf": [{"x": .1, "y": .2}],
            "simulated": [{"x": 11, "y": 21}], "simulatedCdf": [{"x": .3, "y": .4}],
            "xGrid": [1, 2], "yGrid": [3, 4], "xCdf": [.1, .9], "yCdf": [.2, .8],
            "logPdf": [[-4, -3], [-2, -1]], "jointExceedance": [[.8, .7], [.3, .2]],
            "densityLevels": [-4, -3, -2, -1]}
    plots = response_plots({**BASE, "kind": "bivariate", "bivariatePlot": data})
    assert set(plots) == {f"{kind}_{coords}" for kind in ("scatter", "density", "joint_exceedance")
                          for coords in ("values", "cdf")}
    assert plots["scatter_cdf"]["series"][0]["x"] == [.3]
    assert plots["density_values"]["series"][0]["z"] == data["logPdf"]
    assert plots["joint_exceedance_cdf"]["series"][0]["x"] == [.1, .9]
    _validate(plots)


def test_coincident_uses_z_as_vertical_response_and_aep_as_horizontal():
    plots = response_plots({**BASE, "kind": "coincidentFrequency", "results": {
        "zValues": [100, 200], "aepMode": [.4, .1], "aepMean": [.5, .2],
        "ciLower": [.3, .05], "ciUpper": [.7, .4], "credibleIntervalWidth": .9}})
    mode = next(series for series in plots["frequency"]["series"] if series["name"] == "Posterior Mode")
    assert mode["x"] == [.4, .1] and mode["y"] == [100., 200.]
    _validate(plots)


def test_rating_four_plots_preserve_stage_discharge_and_residual_qq():
    plots = response_plots({**BASE, "kind": "ratingCurve", "residualPlot": _residual(),
        "results": {"ratingCurve": {"stages": [2., 3.], "modeCurve": [10., 20.],
            "meanCurve": [11., 21.], "ciLower": [9., 19.], "ciUpper": [12., 22.],
            "credibleIntervalWidth": .9}}})
    assert set(plots) == {"curve", "residuals", "residual_histogram", "residual_qq"}
    assert plots["curve"]["series"][-1]["x"] == [10., 20.]
    assert plots["residual_qq"]["series"][0]["y"] == [-1., 1.]
    assert plots["residual_histogram"]["series"][0]["y"] == [.25, .25]
    _validate(plots)


@pytest.mark.parametrize("xaxis", [[1., 2.], ["2000-01-01", "2000-01-02"]])
def test_residual_misalignment_is_rejected_before_gap_filtering(xaxis):
    from bestfit_plots.adapters.api_response_models import _residual_views
    with pytest.raises(ValueError, match="misaligned"):
        _residual_views({**BASE, "residualPlot": {"residuals": [1., 2., 999.]}},
                        "rating", xaxis, {"label": "Fitted Values", "scale": "linear"})


def test_time_series_six_plots_retain_exported_dates():
    dates = ["2000-01-01T00:00:00", "2000-01-02T00:00:00", "2000-01-03T00:00:00"]
    plots = response_plots({**BASE, "kind": "timeSeries", "resultDates": dates,
        "series": [{"name": "observed", "points": [{"date": date, "value": value}
                   for date,value in zip(dates[:2],[10.,11.])]}], "residualPlot": _residual(),
        "results": {"trainingTimeSteps": 2, "curve": {"modeCurve": [10.,11.,12.],
            "meanCurve": [10.,11.,12.], "ciLower": [9.,10.,11.], "ciUpper": [11.,12.,13.],
            "credibleIntervalWidth": .9}}})
    assert set(plots) == {"series", "residuals", "residual_histogram", "residual_qq", "residual_acf", "residual_pacf"}
    assert plots["series"]["series"][2]["x"] == dates
    assert plots["residuals"]["axes"]["x"]["scale"] == "date"
    assert plots["residuals"]["axes"]["x"]["label"] == "Date"
    assert plots["residuals"]["series"][0]["x"] == dates[:2]
    assert plots["residual_acf"]["series"][0]["y"] == [1., .2]
    _validate(plots)


def test_frequency_api_observations_and_fits_need_no_managed_runtime():
    observations = [{"kind": "exact", "index": 2000., "value": 100., "aep": .5, "lowOutlier": False}]
    fitting = frequency_plots({**BASE, "kind": "distributionFitting", "observations": observations,
        "fittingHistogram": [{"lowerBound": 90., "upperBound": 110., "frequency": 1.}],
        "fittingCurves": [{"name": "Normal", "frequency": [{"x": .5, "y": 100.}],
            "pdf": [{"x": 100., "y": .1}], "cdf": [{"x": 100., "y": .5}],
            "pp": [{"x": .5, "y": .5}], "qq": [{"x": 100., "y": 100.}]}]})
    assert set(fitting) == {"frequency", "pdf", "cdf", "pp", "qq"}
    assert fitting["pdf"]["series"][0]["y"] == [.05]
    assert fitting["qq"]["axes"]["x"]["label"] == "Quantile (Data)"
    assert fitting["qq"]["axes"]["y"]["label"] == "Quantile (Model)"
    _validate(fitting)


def test_diagnostic_variants_use_distinct_exported_prior_trace_and_influence():
    source = {**BASE, "kind": "univariate",
        "analysisXml": '<UnivariateAnalysis><BayesianAnalysis WarmupIterations="3" Iterations="4" /></UnivariateAnalysis>',
        "parameterDiagnostics": [{"histogram": [{"lowerBound": 0., "upperBound": 1., "frequency": 2.}],
            "priorDensity": [{"x": 0., "y": .25}, {"x": 1., "y": .25}],
            "priorName": "Prior Density", "kernelDensity": [], "autocorrelation": []}],
        "samples": {"output": [], "markovChains": [[{"values": [1.]}, {"values": [2.]},
            {"values": [3.]}, {"values": [4.]}]]},
        "influenceDiagnostics": {"method": "bayesian", "observations": [
            {"label": "Exact - 1 - 10", "isObservation": True, "leverage": .3,
             "percentOfTotal": 30., "fitInfluence": .02, "varianceInfluence": .1}],
            "priorComponents": [{"label": "Prior 1", "isObservation": False, "leverage": .7,
             "percentOfTotal": 70., "fitInfluence": .01, "varianceInfluence": .5}],
            "leaveOneOut": [{"label": "Exact - 1 - 10", "elpdLoo": -.7, "category": "Good"}]}}
    default = diagnostic_plots(source, influence_view="bayesian_leverage")
    selected = diagnostic_plots(source, include_warmup=True, show_prior=True, influence_view="bayesian_fit")
    loo = diagnostic_plots(source, influence_view="leave_one_out")
    assert default["trace"]["series"][0]["y"] == [3., 4.]
    assert selected["trace"]["series"][0]["y"] == [1., 2., 3., 4.]
    assert selected["trace"]["variant"] == "warmup"
    assert selected["histogram"]["variant"] == "prior"
    assert selected["histogram"]["series"][0]["name"] == "Prior Density"
    assert default["influence"]["series"][0]["y"] != selected["influence"]["series"][0]["y"]
    assert selected["influence"]["variant"] == "bayesian_fit"
    assert loo["influence"]["variant"] == "leave_one_out"
    assert loo["influence"]["series"][0]["y"] == [.7]
    _validate(selected)
