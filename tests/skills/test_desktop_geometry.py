"""Independent coordinate fixtures for the desktop snapshot display adapter."""
from pathlib import Path
import copy
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "skills/bestfit-frequency"))


def snapshot():
    return {"formatVersion": 1, "sourceSha256": "abc", "element": "Recorded flow", "project": "source.bestfit",
            "plotId": "time_series_data.series", "variant": "default", "title": "Time Series",
            "axes": [{"position": "Bottom", "type": "DateTimeAxis", "Title": "Date"},
                     {"position": "Left", "type": "LinearAxis", "Title": "Flow (cfs)"}],
            "series": [{"type": "LineSeries", "Title": "Recorded", "points": [
                {"X": 36526., "Y": 7}, {"X": 36527., "Y": None}, {"X": 36528., "Y": 9}],
                "style": {"Color": "#ff0000ff"}}]}


def test_requested_legend_position_can_leave_a_peak_visible():
    import matplotlib.pyplot as plt
    import pytest
    from bestfit_plots.adapters.desktop import desktop_plot
    from bestfit_plots.render import render_plot
    spec = desktop_plot(snapshot())
    spec["legendLocation"] = "upper left"
    figure = render_plot(spec)
    try:
        figure.canvas.draw()
        axes = figure.axes[0]
        legend = axes.get_legend().get_window_extent()
        assert legend.x1 < axes.get_window_extent().x0 + axes.get_window_extent().width / 2
    finally:
        plt.close(figure)
    for invalid in ("misspelled", [], {}):
        spec["legendLocation"] = invalid
        with pytest.raises(ValueError, match="legendLocation"):
            render_plot(spec)


def test_desktop_dates_gaps_and_source_hash_are_retained_without_managed_runtime():
    from bestfit_plots.adapters.desktop import desktop_plot
    original = snapshot()
    before = copy.deepcopy(original)
    spec = desktop_plot(original)
    assert spec["series"][0]["x"] == ["2000-01-01T00:00:00", "2000-01-02T00:00:00", "2000-01-03T00:00:00"]
    assert spec["series"][0]["y"] == [7., None, 9.]
    assert spec["source"]["runId"] == "sha256:abc"
    assert original == before


def test_horizontal_probability_bounds_stay_at_their_response_ordinates():
    from bestfit_plots.adapters.desktop import desktop_plot
    data = snapshot()
    data.update(plotId="coincident.frequency")
    data["axes"][0].update(type="NormalProbabilityAxis", Title="Exceedance Probability")
    data["series"] = [{"type": "AreaSeries", "Title": "90% Credible Intervals",
                       "points": [{"X": .01, "Y": 20}, {"X": .001, "Y": 30}],
                       "points2": [{"X": .1, "Y": 20}, {"X": .01, "Y": 30}], "style": {}}]
    spec = desktop_plot(data)
    item = spec["series"][0]
    assert item["y"] == [20, 30]
    assert item["xLower"] == [.01, .001] and item["xUpper"] == [.1, .01]
    assert item["interval"] == {"kind": "credible", "level": .9}


def test_unknown_visible_series_cannot_silently_disappear():
    import pytest
    from bestfit_plots.adapters.desktop import desktop_plot
    data = snapshot()
    data["series"][0]["type"] = "NewUnsupportedSeries"
    with pytest.raises(ValueError, match="Unsupported"):
        desktop_plot(data)


def test_desktop_fill_keeps_original_transparency():
    import matplotlib.pyplot as plt
    import pytest
    from bestfit_plots.adapters.desktop import desktop_plot
    from bestfit_plots.render import render_plot
    data = snapshot()
    data["axes"][0].update(type="LinearAxis")
    data["series"] = [{"type": "AreaSeries", "Title": "90% Credible Intervals",
                       "points": [{"X": 1, "Y": 2}, {"X": 2, "Y": 3}],
                       "points2": [{"X": 1, "Y": 4}, {"X": 2, "Y": 5}],
                       "style": {"Fill": "#4b688caf"}}]
    figure = render_plot(desktop_plot(data))
    try:
        assert figure.axes[0].collections[0].get_facecolors()[0, 3] == pytest.approx(75 / 255)
    finally:
        plt.close(figure)


def test_desktop_probability_bounds_and_reversed_y_are_displayed():
    import matplotlib.pyplot as plt
    import pytest
    from statistics import NormalDist
    from bestfit_plots.adapters.desktop import desktop_plot
    from bestfit_plots.render import render_plot
    data = snapshot()
    data["axes"][0].update(type="NormalProbabilityAxis", Minimum=1e-7, Maximum=.999,
                           StartPosition=1., EndPosition=0.)
    data["axes"][1].update(ActualMinimum=0., ActualMaximum=10., StartPosition=1., EndPosition=0.)
    data["series"][0]["points"] = [{"X": .5, "Y": 7}, {"X": .01, "Y": 9}]
    figure = render_plot(desktop_plot(data))
    try:
        axes = figure.axes[0]
        assert axes.get_xlim() == pytest.approx((-NormalDist().inv_cdf(.999), -NormalDist().inv_cdf(1e-7)))
        assert axes.get_ylim() == (10., 0.)
    finally:
        plt.close(figure)


def test_seasonality_percentiles_are_observed_ranges_not_confidence_bands():
    from bestfit_plots.adapters.desktop import desktop_plot
    data = snapshot()
    data['plotId'] = 'time_series_data.seasonality'
    data['series'] = [{'type': 'AreaSeries', 'Title': '90% Confidence Interval',
                       'points': [{'X': 36526., 'Y': 2}, {'X': 36557., 'Y': 3}],
                       'points2': [{'X': 36526., 'Y': 4}, {'X': 36557., 'Y': 5}], 'style': {}}]
    spec = desktop_plot(data)
    assert spec['series'][0]['name'] == '90% Observed Range'
    assert spec['series'][0]['kind'] == 'area'
    assert 'interval' not in spec['series'][0]
    assert any('percentile' in note for note in spec['displayCorrections'])


def test_unfilled_low_outlier_marker_uses_desktop_red_stroke():
    import matplotlib.pyplot as plt
    import pytest
    from bestfit_plots.adapters.desktop import desktop_plot
    from bestfit_plots.render import render_plot
    data = snapshot()
    data['series'][0].update(type='ScatterSeries', Title='Low Outlier Data',
                            style={'MarkerType':'Cross', 'MarkerFill':'#00000001', 'MarkerStroke':'#ffff0000'})
    figure = render_plot(desktop_plot(data))
    try:
        assert figure.axes[0].collections[0].get_edgecolors()[0] == pytest.approx([1.,0.,0.,1.])
    finally:
        plt.close(figure)
