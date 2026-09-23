"""Deterministic rendering contracts; synthetic coordinates never invoke estimation."""
import copy
import importlib.util
from pathlib import Path
import tempfile
import unittest

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
import numpy as np

SCRIPT = Path(__file__).resolve().parents[2] / "skills/bestfit-frequency/scripts/plot_frequency.py"
SPEC = importlib.util.spec_from_file_location("plot_frequency", SCRIPT)
plot = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(plot)


def results(kind="bulletin17C"):
    return {"success": True, "kind": kind, "fittedDistribution": {"pointEstimator": "gmm" if kind == "bulletin17C" else "posteriorMode"}, "frequencyCurve": {
        "probabilities": [0.5, 0.1, 0.01], "modeCurve": [100, 300, 900],
        "meanCurve": [110, 350, 1000], "ciLower": [80, 200, 600],
        "ciUpper": [140, 450, 1500], "credibleIntervalWidth": 0.9},
        "quantileAnnotations": [{"aep": 0.02, "value": 800, "lowerBound": 600, "upperBound": 1200}]}


def inputs():
    return {"success": True, "exactData": [
        {"value": 120, "plottingPosition": 0.45, "isLowOutlier": False},
        {"value": 10, "plottingPosition": 0.95, "isLowOutlier": True},
        {"value": 0, "plottingPosition": 0.98, "isLowOutlier": True}],
        "uncertainData": [{"value": 400, "lowerBound": 320, "upperBound": 490, "plottingPosition": 0.08}],
        "intervalData": [{"value": 600, "lowerBound": 500, "upperBound": 700, "plottingPosition": 0.04}]}


class PlotFrequencyTests(unittest.TestCase):
    def tearDown(self):
        plt.close("all")

    def test_probability_coordinates_orientation_labels_and_no_refit(self):
        payload, observations = results(), inputs()
        original = copy.deepcopy((payload, observations))
        fig, ax, notes = plot.build_figure(payload, observations)
        line = next(line for line in ax.lines if line.get_label() == "Computed")
        np.testing.assert_allclose(line.get_xdata(), [0, 1.2815515655446004, 2.3263478740408408])
        np.testing.assert_array_equal(line.get_ydata(), [100, 300, 900])
        self.assertEqual(ax.get_yscale(), "log")
        self.assertLess(*ax.get_xlim())
        self.assertIn("90% Confidence Intervals", ax.get_legend_handles_labels()[1])
        self.assertIn("Expected Probability", ax.get_legend_handles_labels()[1])
        self.assertEqual((payload, observations), original)
        self.assertTrue(any("Low Outlier Data" in note and "1" in note for note in notes))
        marker = next(c for c in ax.collections if c.get_label() == "Low Outlier Data")
        np.testing.assert_allclose(marker.get_offsets(), [[-1.6448536269514722, 10]])

    def test_bayesian_labels_and_export(self):
        fig, ax, _ = plot.build_figure(results("univariate"), inputs(), ylabel="Flow (m³/s)")
        labels = ax.get_legend_handles_labels()[1]
        self.assertIn("Posterior Mode", labels)
        self.assertIn("Posterior Predictive", labels)
        self.assertIn("90% Credible Intervals", labels)
        with tempfile.TemporaryDirectory() as temp:
            paths = plot.save_figure(fig, Path(temp) / "frequency")
            self.assertEqual({p.suffix for p in paths}, {".png", ".svg"})
            self.assertTrue(all(p.stat().st_size > 1000 for p in paths))

    def test_reordering_preserves_curve_alignment(self):
        payload = results()
        for key, value in payload["frequencyCurve"].items():
            if isinstance(value, list):
                payload["frequencyCurve"][key] = value[::-1]
        _, ax, _ = plot.build_figure(payload, inputs())
        line = next(line for line in ax.lines if line.get_label() == "Computed")
        np.testing.assert_array_equal(line.get_ydata(), [100, 300, 900])

    def test_posterior_mean_label_uses_returned_estimator(self):
        payload = results("univariate")
        payload["fittedDistribution"]["pointEstimator"] = "posteriorMean"
        _, ax, _ = plot.build_figure(payload, inputs())
        labels = ax.get_legend_handles_labels()[1]
        self.assertIn("Posterior Mean", labels)
        self.assertNotIn("Posterior Mode", labels)
        self.assertIn("Posterior Predictive", labels)

    def test_unknown_bayesian_point_estimator_is_not_guessed(self):
        payload = results("univariate")
        payload["fittedDistribution"]["pointEstimator"] = "unknown"
        with self.assertRaisesRegex(ValueError, "pointEstimator"):
            plot.build_figure(payload, inputs())

    def test_nonfinite_curve_is_a_gap_and_reported(self):
        payload = results()
        payload["frequencyCurve"]["modeCurve"][1] = "NaN"
        _, ax, notes = plot.build_figure(payload, inputs())
        line = next(line for line in ax.lines if line.get_label() == "Computed")
        self.assertTrue(np.isnan(line.get_ydata()[1]))
        self.assertTrue(any("Computed" in note for note in notes))

    def test_workflow_envelope_and_failed_response(self):
        plot.build_figure({"success": True, "results": results()}, inputs())
        for bad in [{"success": False, "results": results()}, {"success": True, "results": {"success": False}}]:
            with self.assertRaisesRegex(ValueError, "failed"):
                plot.build_figure(bad, inputs())

    def test_rejects_bad_probability_shape_and_empty_curve(self):
        for field, values in [("probabilities", [0, 0.1, 0.01]), ("probabilities", [0.5, 0.5, 0.01]),
                              ("probabilities", [0.5, "NaN", 0.01]), ("modeCurve", [1, 2]),
                              ("modeCurve", [0, "NaN", -1]), ("ciUpper", [1, 2])]:
            payload = results()
            payload["frequencyCurve"][field] = values
            with self.subTest(field=field, values=values), self.assertRaises(ValueError):
                plot.build_figure(payload, inputs())

    def test_requires_observations_and_display_bounds(self):
        for payload in [{"success": False}, {"success": True}]:
            with self.assertRaises(ValueError):
                plot.build_figure(results(), payload)
        payload = inputs()
        del payload["uncertainData"][0]["lowerBound"]
        with self.assertRaisesRegex(ValueError, "lowerBound"):
            plot.build_figure(results(), payload)

    def test_error_bars_use_supplied_endpoints(self):
        _, ax, _ = plot.build_figure(results(), inputs())
        bars = next(c for c in ax.collections if c.get_gid() == "Uncertain Data bounds")
        np.testing.assert_allclose(bars.get_segments()[0][:, 1], [320, 490])


if __name__ == "__main__":
    unittest.main()
