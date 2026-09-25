"""Check model-view alignment independently of .NET estimators."""
from pathlib import Path
import sys
sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "skills" / "bestfit-frequency"))

SOURCE = {"kind":"saved", "id":"case", "runId":"fixed"}


def test_copula_observations_join_index_and_keep_empirical_probability():
    from bestfit_plots.adapters.response_models import paired_observations
    x = [dict(index=3,value=30,aep=.1,lowOutlier=False), dict(index=1,value=10,aep=.9,lowOutlier=False),
         dict(index=2,value=20,aep=.5,lowOutlier=True)]
    y = [dict(index=1,value=11,aep=.8,lowOutlier=False), dict(index=2,value=21,aep=.3,lowOutlier=False)]
    pairs = paired_observations(x, y, cdf=True)
    assert len(pairs) == 1
    assert abs(pairs[0][0]-.1) < 1e-15 and abs(pairs[0][1]-.2) < 1e-15


def test_coincident_band_is_horizontal_and_response_axis_linear():
    from bestfit_plots.adapters.response_models import response_frequency_spec
    s = response_frequency_spec(SOURCE, [10,20], [.5,.1], [.55,.12], [.4,.08], [.6,.15], .9, "Posterior Mode")
    assert s["axes"]["y"]["scale"] == "linear"
    assert s["series"][0]["xLower"] == [.4,.08]
    assert s["series"][0]["y"] == [10,20]
    assert s["series"][1]["x"] == [.55,.12]


def test_training_prediction_bands_share_exact_boundary():
    from bestfit_plots.adapters.response_models import time_series_curve_spec
    dates=["2000-01-01","2001-01-01","2002-01-01","2003-01-01"]
    s=time_series_curve_spec(SOURCE, dates[:3], [10,11,12], dates[:3], [9,10,11],
                             dates, [9,10,11,12], [8,9,10,11], [10,11,12,13], 2, .9, "Posterior Mean", "Flow")
    training,prediction=s["series"][:2]
    assert training["x"][-1] == prediction["x"][0] == dates[1]
    assert training["yLower"][-1] == prediction["yLower"][0] == 9
    assert training["interval"] == prediction["interval"] == {"kind": "prediction", "level": .9}
    assert "Prediction Intervals" in training["name"]
    assert s["series"][3]["name"] == "Posterior Mean"
