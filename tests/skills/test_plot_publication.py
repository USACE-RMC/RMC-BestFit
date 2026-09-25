"""Reader-facing plot semantics, using detached coordinates and no estimator."""
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "skills/bestfit-frequency"))


def test_wire_nonfinite_values_are_gaps_with_dates_and_alignment_preserved():
    from bestfit_plots.adapters.common import clean
    assert clean([1, "NaN", "Infinity", "-Infinity", None, 4]) == [1., None, None, None, None, 4.]
    assert clean(["2026-01-01", None, "2026-01-03"]) == ["2026-01-01", None, "2026-01-03"]


def test_bulletin17c_uncertainty_plots_never_claim_posterior_samples():
    from bestfit_plots.adapters.diagnostics import diagnostic_plots
    source = {"analysisId": "b17c", "lastRunUtc": "saved", "kind": "Bulletin17CAnalysis",
              "parameterDiagnostics": [{"parameterIndex": 0, "histogram": [{"x": 1., "y": 1.}],
                                        "kernelDensity": [{"x": 1., "y": 1.}]}],
              "samples": {"output": [{"values": [0., 0.]}, {"values": [1., 1.]}]}}
    specs = diagnostic_plots(source, 0, 1)
    assert {"histogram", "kde", "pair_heatmap"} <= specs.keys()
    for spec in specs.values():
        assert "posterior" not in spec["title"].lower()
        assert all("posterior" not in s["name"].lower() for s in spec["series"])


def test_contours_have_readable_numeric_levels():
    import matplotlib.pyplot as plt
    from bestfit_plots.adapters.common import plot, axis
    from bestfit_plots.render import render_plot
    spec = plot("bivariate.distribution", {"kind": "saved", "id": "contour", "runId": "fixture"},
                "Joint exceedance probability", axis("X"), axis("Y"),
                [{"name": "Contour", "kind": "contour", "x": [0, 1, 2], "y": [0, 1, 2],
                  "z": [[0, .1, .2], [.1, .2, .3], [.2, .3, .4]], "style": {"levels": [.1, .2, .3]}}])
    figure = render_plot(spec)
    try:
        assert {"0.1", "0.2", "0.3"} <= {text.get_text() for text in figure.axes[0].texts}
    finally:
        plt.close(figure)


def test_seasonality_ticks_show_months_without_an_artificial_year():
    import matplotlib.pyplot as plt
    from bestfit_plots.adapters.common import plot, axis, line
    from bestfit_plots.render import render_plot
    spec = plot("time_series_data.seasonality", {"kind": "saved", "id": "months", "runId": "fixture"},
                "Seasonality", axis("Month", "date", "date"), axis("Flow"),
                [line("Mean", ["2020-01-01", "2020-06-01", "2020-12-31"], [1, 2, 1])])
    figure = render_plot(spec)
    try:
        figure.canvas.draw()
        labels = [t.get_text() for t in figure.axes[0].get_xticklabels()]
        assert "Jan" in labels and "Dec" in labels
        assert all("2020" not in label for label in labels)
    finally:
        plt.close(figure)
