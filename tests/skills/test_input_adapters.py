"""Observation geometry retains historical bounds and flagged values."""
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "skills" / "bestfit-frequency"))

SOURCE = {"kind": "saved", "id": "fixture", "runId": "sha256:source"}


def test_frequency_preserves_flagged_zero_and_original_probability():
    from bestfit_plots.adapters.input_data import observation_plot
    observations = [dict(kind="exact", index=1980, value=0, aep=.8, lowOutlier=True),
                    dict(kind="exact", index=1981, value=10, aep=.2, lowOutlier=False)]
    spec = observation_plot(observations, SOURCE, "frequency", "Flow (cfs)")
    zero = next(s for s in spec["series"] if s["name"] == "Low Outlier Data")
    assert zero["x"] == [.8] and zero["y"] == [0]
    assert zero["style"]["minimumPositive"] == 1e-16
    assert observations[0]["value"] == 0


def test_historical_interval_bounds_are_not_reestimated():
    from bestfit_plots.adapters.input_data import observation_plot
    observations = [dict(kind="interval", index=1921, value=100, lower=80, upper=120, aep=.02)]
    spec = observation_plot(observations, SOURCE, "chronology", "Flow")
    point = spec["series"][0]
    assert point["x"] == [1921]
    assert point["yLower"] == [80] and point["yUpper"] == [120]
    assert point["interval"]["kind"] == "measurement"


def test_threshold_window_is_area_not_uncertainty_interval():
    from bestfit_plots.adapters.input_data import observation_plot
    observations = [dict(kind="threshold", start=1800, end=1900, value=900)]
    spec = observation_plot(observations, SOURCE, "chronology", "Flow")
    window = spec["series"][0]
    assert window["x"] == [1800, 1900]
    assert window["kind"] == "area"
    assert window["yLower"] == [0, 0] and window["yUpper"] == [900, 900]
    assert "interval" not in window


def test_missing_coordinates_become_gaps_without_reordering():
    from bestfit_plots.adapters.common import line
    series = line("Observed", [2000, 2001, 2002], [1, float("nan"), 3])
    assert series["x"] == [2000, 2001, 2002]
    assert series["y"] == [1, None, 3]
