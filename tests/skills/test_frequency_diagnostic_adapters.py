"""Frozen result geometry: adapters do not estimate or relabel GMM draws."""
from pathlib import Path
from types import SimpleNamespace as NS
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "skills" / "bestfit-frequency"))

IDENTITY = {"kind": "saved", "id": "fixture", "runId": "sha256:fixture"}


def _restored(analysis, model=None, observations=None):
    return {"analysis": analysis, "model": model, "observations": observations or [],
            "plotSourceIdentity": IDENTITY}


def test_univariate_frequency_preserves_estimator_interval_and_observation():
    from bestfit_plots.adapters.frequency import frequency_plots
    original = [dict(kind="exact", index=1990, value=0, aep=.9, lowOutlier=True)]
    results = NS(ModeCurve=[12., 35.], MeanCurve=[13., 37.],
                 ConfidenceIntervals=[[10., 15.], [30., 40.]])
    analysis = NS(ProbabilityOrdinates=[.5, .01], AnalysisResults=results,
                  BayesianAnalysis=NS(CredibleIntervalWidth=.9, PointEstimator="PosteriorMean"))
    specs = frequency_plots(_restored(analysis, NS(IsNonstationary=False), original))
    freq = specs["frequency"]
    assert freq["plotId"] == "univariate.frequency"
    assert freq["source"] == IDENTITY
    assert next(s for s in freq["series"] if s["kind"] == "band")["interval"] == {"kind": "credible", "level": .9}
    assert next(s for s in freq["series"] if s["name"] == "Posterior Mean")["y"] == [12., 35.]
    assert next(s for s in freq["series"] if s["name"] == "Low Outlier Data")["y"] == [0.]
    assert original[0]["value"] == 0


def test_point_process_api_requires_exported_ams_ranks():
    import pytest
    from bestfit_plots.adapters.frequency import frequency_plots
    with pytest.raises(ValueError, match="Langbein AMS"):
        frequency_plots({"analysisId": "old", "lastRunUtc": "2026-09-22T00:00:00Z",
                         "kind": "pointProcess", "observations": [], "results": {}})


def test_b17c_intervals_are_confidence_and_not_mcmc():
    from bestfit_plots.adapters.frequency import frequency_plots
    from bestfit_plots.adapters.diagnostics import diagnostic_plots
    result = NS(ModeCurve=[100.], MeanCurve=[101.], ConfidenceIntervals=[[90., 110.]])
    analysis = NS(ProbabilityOrdinates=[.01], AnalysisResults=result,
                  BayesianAnalysis=NS(CredibleIntervalWidth=.95, PointEstimator="PosteriorMode"))
    restored = _restored(analysis)
    restored["row"] = {"Type": "RMC.BestFit.UI.B17CAnalysis"}
    assert frequency_plots(restored)["frequency"]["series"][0]["interval"]["kind"] == "confidence"
    assert frequency_plots(restored)["frequency"]["plotId"] == "b17c.frequency"
    assert "trace" not in diagnostic_plots(restored)
    assert "mean_log_likelihood" not in diagnostic_plots(restored)


def test_quantile_annotation_semantics_distinguish_gmm_penalties():
    from bestfit_plots.adapters.frequency import frequency_plots
    result = NS(ModeCurve=[100.], MeanCurve=[101.], ConfidenceIntervals=[[90., 110.]])
    analysis = NS(ProbabilityOrdinates=[.01], AnalysisResults=result,
                  BayesianAnalysis=NS(CredibleIntervalWidth=.9, PointEstimator="PosteriorMean"))
    for row_type, expected in (("B17CAnalysis", "penalty"), ("UnivariateAnalysis", "prior")):
        restored = _restored(analysis)
        restored['row'] = {'Type': 'RMC.BestFit.UI.' + row_type}
        restored['results'] = {'quantileAnnotations': [{'aep': .001, 'value': 100., 'lowerBound': 80., 'upperBound': 120.}]}
        annotations = [s for s in frequency_plots(restored)['frequency']['series'] if s['name'].startswith('Quantile')]
        assert len(annotations) == 1
        assert annotations[0]['interval']['kind'] == expected
        assert annotations[0]['yLower'] == [80.] and annotations[0]['yUpper'] == [120.]


def test_diagnostics_opt_in_chains_and_joint_grid_mass():
    from bestfit_plots.adapters.diagnostics import diagnostic_plots
    samples = {"output": [{"values": [0., 0.]}, {"values": [1., 1.]}, {"values": [1., 0.]}],
               "markovChains": [[{"values": [0., 0.]}, {"values": [1., 1.]}]]}
    restored = {"analysisId": "a", "lastRunUtc": "2026-09-22T00:00:00Z", "kind": "univariate",
                "sampleOrigin": "mcmc", "parameterDiagnostics": [
                    {"parameterIndex": 0, "displayName": "Location (ξ)", "histogram": [{"x": 0., "y": 1.}],
                     "kernelDensity": [{"x": 0., "y": .5}], "autocorrelation": [{"x": 0., "y": 1.}]}],
                "meanLogLikelihood": [-4., -3.], "samples": samples}
    specs = diagnostic_plots(restored, 0, 1)
    assert {"trace", "histogram", "kde", "acf", "mean_log_likelihood", "pair_heatmap"} <= specs.keys()
    assert specs["trace"]["series"][0]["y"] == [0., 1.]
    assert specs["trace"]["title"] == "Trace of Location (ξ)"
    assert specs["histogram"]["series"][0]["name"] == "Posterior Histogram"
    assert specs["acf"]["series"][0]["x"] == [.5]
    assert {item["name"] for item in specs["pair_heatmap"]["series"]} == {"Bivariate HeatMap", "Bivariate Contour"}
    assert specs["mean_log_likelihood"]["title"] == "Mean Log-Likelihood"
    grid = specs["pair_heatmap"]["series"][0]
    assert sum(map(sum, grid["z"])) == 1.
    assert samples["output"][0]["values"] == [0., 0.]
    assert all(spec["plotId"].startswith("shared_diagnostics.") for spec in specs.values())
    bivariate = {**restored, "kind": "bivariate"}
    assert "pair_heatmap" not in diagnostic_plots(bivariate, 0, 1)


def test_api_histogram_bin_area_matches_desktop_height():
    from bestfit_plots.adapters.diagnostics import diagnostic_plots
    restored = {"analysisId": "a", "lastRunUtc": "2026-09-22T00:00:00Z", "kind": "univariate",
                "parameterDiagnostics": [{"parameterIndex": 0, "histogram": [
                    {"lowerBound": 0., "upperBound": 2., "frequency": 2.},
                    {"lowerBound": 2., "upperBound": 4., "frequency": 6.}]}]}
    bars = diagnostic_plots(restored)["histogram"]["series"][0]
    assert bars["x"] == [1., 3.] and bars["width"] == [2., 2.]
    assert bars["y"] == [.125, .375]


def test_frozen_saved_results_make_valid_fitting_and_mcmc_geometry():
    import pytest
    pytest.importorskip("bestfit_examples", reason="Optional notebook Pythonnet integration; desktop/API rendering is self-contained")
    from bestfit_examples.analysis import restore_analysis
    from bestfit_examples.project_data import load_project
    from bestfit_plots.adapters.frequency import fitting_plots, frequency_plots
    from bestfit_plots.adapters.diagnostics import diagnostic_plots
    from bestfit_plots.spec import validate_spec

    project = load_project("viglione-et-al-2013")
    fitting = restore_analysis(project, "Distribution Fitting Analysis", "Fit - Systematic (1951-2001)")
    fit_plots = frequency_plots(fitting)
    assert set(fitting_plots(fitting)) == set(fit_plots)
    assert set(fit_plots) == {"frequency", "pdf", "cdf", "pp", "qq"}
    assert any(item["kind"] == "line" for item in fit_plots["frequency"]["series"])
    for spec in fit_plots.values():
        validate_spec(spec)
    fit = next(item for item in fitting["analysis"].FittedDistributions if item.FitSucceeded and item.ShowResults)
    values = sorted(float(item.Value) for group in (fitting["input"].ExactSeries,
                    fitting["input"].UncertainSeries, fitting["input"].IntervalSeries) for item in group)
    complements = sorted(1-float(item.PlottingPosition) for group in (fitting["input"].ExactSeries,
                         fitting["input"].UncertainSeries, fitting["input"].IntervalSeries) for item in group)
    name = str(fit.Distribution.DisplayName)
    pp = next(item for item in fit_plots["pp"]["series"] if item["name"] == name)
    qq = next(item for item in fit_plots["qq"]["series"] if item["name"] == name)
    assert pp["x"][0] == float(fit.Distribution.CDF(values[0])) and pp["y"][0] == complements[0]
    assert qq["x"][0] == values[0] and qq["y"][0] == float(fit.Distribution.InverseCDF(complements[0]))
    assert fit_plots["pp"]["axes"]["x"]["label"] == "Probability (Model)"
    assert fit_plots["qq"]["axes"]["x"]["label"] == "Quantile (Data)"
    api_source = {"analysisId": "fixture", "lastRunUtc": "2026-09-22T00:00:00Z",
                  "kind": "distributionFitting", "analysisXml": str(fitting["analysis"].ToXElement()),
                  "dataFrameXml": str(fitting["input"].ToXElement())}
    assert set(fitting_plots(api_source)) == set(fit_plots)
    mcmc = restore_analysis(project, "Univariate Distribution Analysis", "MCMC - Systematic (1951-2001)")
    diagnostics = diagnostic_plots(mcmc)
    assert {"trace", "histogram", "kde", "acf", "mean_log_likelihood", "pair_heatmap", "influence"} == set(diagnostics)
    assert diagnostics["trace"]["series"][0]["x"][0] == max(1, int(mcmc["analysis"].BayesianAnalysis.WarmupIterations))
    histogram = mcmc["analysis"].BayesianAnalysis.Results.ParameterResults[0].Histogram
    total_area = sum(float(histogram[i].Frequency*histogram.BinWidth) for i in range(histogram.NumberOfBins))
    assert diagnostics["histogram"]["series"][0]["y"][0] == float(histogram[0].Frequency)/total_area
    assert len(diagnostics["pair_heatmap"]["series"][0]["x"]) == 16
    for spec in diagnostics.values():
        validate_spec(spec)


def test_frozen_b17c_point_process_mixture_and_composite_geometry():
    import pytest
    pytest.importorskip("bestfit_examples", reason="Optional notebook Pythonnet integration; desktop/API rendering is self-contained")
    from bestfit_examples.analysis import restore_analysis
    from bestfit_examples.project_data import load_project
    from bestfit_plots.adapters.frequency import frequency_plots
    from bestfit_plots.adapters.diagnostics import diagnostic_plots
    from bestfit_plots.spec import validate_spec

    cases = [
        ("bulletin-17c-examples", "Example #2"),
        ("point-process-examples", "USC00040741 - Point Process"),
        ("mixture-distribution-examples", "Mixture Distribution - 2 Normals"),
        ("mixed-population-examples", "Competing Flood Types"),
    ]
    for project, name in cases:
        restored = restore_analysis(load_project(project), "Univariate Distribution Analysis", name)
        spec = frequency_plots(restored)["frequency"]
        validate_spec(spec)
        assert spec["source"]["kind"] == "saved"
        if project == "point-process-examples":
            assert any(item["name"].startswith("AMS ") for item in spec["series"])
            ams = frequency_plots(restored)["frequency_ams"]
            validate_spec(ams)
            assert ams["plotId"] == "point_process.frequency" and ams["variant"] == "ams"
            assert not any(item["name"] == "Exact Data" for item in ams["series"])
            pot = next(item for item in spec["series"] if item["name"] == "POT Exact Data")
            converted = next(item for item in spec["series"] if item["name"] == "AMS Exact Data")
            assert pot["y"][0] == converted["y"][0] and pot["x"][0] != converted["x"][0]
            result = restored["analysis"].AnalysisResults
            from RMC.BestFit.Models import DataFrame
            from bestfit_plots.adapters.input_data import observations_from_dataframe
            ams_frame = DataFrame(restored["input"].ToXElement())
            ams_frame.ApplyLangbeinConversion(float(restored["model"].Lambda))
            api_source = {"analysisId": "point-process-api", "lastRunUtc": "2026-09-22T00:00:00Z",
                          "kind": "pointProcess", "modelXml": str(restored["model"].ToXElement()),
                          "dataFrameXml": str(restored["input"].ToXElement()),
                          "observations": observations_from_dataframe(restored["input"]),
                          "amsObservations": observations_from_dataframe(ams_frame),
                          "results": {"frequencyCurve": {
                              "probabilities": list(restored["analysis"].ProbabilityOrdinates),
                              "modeCurve": list(result.ModeCurve), "meanCurve": list(result.MeanCurve),
                              "ciLower": [result.ConfidenceIntervals[i, 0] for i in range(len(result.ModeCurve))],
                              "ciUpper": [result.ConfidenceIntervals[i, 1] for i in range(len(result.ModeCurve))],
                              "credibleIntervalWidth": float(restored["analysis"].BayesianAnalysis.CredibleIntervalWidth)}}}
            api_spec = frequency_plots(api_source)["frequency"]
            assert any(item["name"].startswith("AMS ") for item in api_spec["series"])
            api_ams = frequency_plots(api_source)["frequency_ams"]
            assert next(s for s in api_ams["series"] if s["name"] == "AMS Exact Data")["x"] == converted["x"]
            assert not any(s["name"].startswith("POT ") for s in api_ams["series"])
            assert api_spec["axes"]["y"]["label"] == "Value (unit unavailable)"
            api_source["unitLabel"] = "Peak Discharge (cms)"
            assert frequency_plots(api_source)["frequency"]["axes"]["y"]["label"] == "Peak Discharge (cms)"
        if project == "mixed-population-examples":
            assert any(" - " in item["name"] for item in spec["series"])
            components = []
            for component_name, dependency in restored["dependencies"].items():
                component = dependency["analysis"]
                components.append({"name": component_name,
                                   "probabilities": list(component.ProbabilityOrdinates),
                                   "values": list(component.AnalysisResults.ModeCurve)})
            api_source = {"analysisId": "composite-api", "lastRunUtc": "2026-09-22T00:00:00Z",
                          "kind": "composite", "componentCurves": components,
                          "results": {"frequencyCurve": {"probabilities": list(restored["analysis"].ProbabilityOrdinates),
                          "modeCurve": list(restored["analysis"].AnalysisResults.ModeCurve),
                          "meanCurve": list(restored["analysis"].AnalysisResults.MeanCurve),
                          "ciLower": [restored["analysis"].AnalysisResults.ConfidenceIntervals[i, 0]
                                      for i in range(len(restored["analysis"].AnalysisResults.ModeCurve))],
                          "ciUpper": [restored["analysis"].AnalysisResults.ConfidenceIntervals[i, 1]
                                      for i in range(len(restored["analysis"].AnalysisResults.ModeCurve))],
                          "credibleIntervalWidth": float(restored["analysis"].BayesianAnalysis.CredibleIntervalWidth)}}}
            assert len(frequency_plots(api_source)["frequency"]["series"]) == 5
        if project == "bulletin-17c-examples":
            diagnostics = diagnostic_plots(restored)
            assert set(diagnostics) == {"histogram", "kde", "pair_heatmap", "influence"}
            assert any(item["name"] == "Computed" for item in spec["series"])
            assert any(item["name"] == "Expected Probability" for item in spec["series"])
            assert spec["series"][0]["interval"]["kind"] == "confidence"
            for item in diagnostics.values():
                validate_spec(item)


def test_frozen_nonstationary_chronology_uses_saved_result():
    import pytest
    pytest.importorskip("bestfit_examples", reason="Optional notebook Pythonnet integration; desktop/API rendering is self-contained")
    from bestfit_examples.analysis import restore_analysis
    from bestfit_examples.project_data import load_project
    from bestfit_plots.adapters.frequency import frequency_plots
    from bestfit_plots.spec import validate_spec

    restored = restore_analysis(load_project("nsffa-brays-bayou-texas"),
                                "Univariate Distribution Analysis", "NSFFA - Linear")
    specs = frequency_plots(restored)
    chronology = validate_spec(specs["chronology"])
    assert any(item["interval"]["kind"] == "credible" for item in chronology["series"] if "interval" in item)
    assert any(item["name"] == "Mean" for item in chronology["series"])
    stored = restored["analysis"].ChronologyAnalysisResults
    x = next(item for item in chronology["series"] if item["name"] == "Mean")["x"]
    api_source = {"analysisId": "nonstationary-api", "lastRunUtc": "2026-09-22T00:00:00Z",
                  "kind": "univariate", "chronology": {"indices": x, "meanCurve": list(stored.MeanCurve),
                  "ciLower": [stored.ConfidenceIntervals[i, 0] for i in range(len(x))],
                  "ciUpper": [stored.ConfidenceIntervals[i, 1] for i in range(len(x))],
                  "credibleIntervalWidth": float(restored["analysis"].BayesianAnalysis.CredibleIntervalWidth)}}
    assert next(item for item in frequency_plots(api_source)["chronology"]["series"]
                if item["name"] == "Mean")["y"] == list(stored.MeanCurve)
