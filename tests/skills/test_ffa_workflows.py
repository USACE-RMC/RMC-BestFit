"""Behavioral FFA preparation, chronology and evidence contracts; no estimation or live services."""
import copy
import importlib.util
import json
from pathlib import Path
import sys
import tempfile
import unittest

SCRIPTS = Path(__file__).resolve().parents[2] / "skills/bestfit-frequency/scripts"
sys.path.insert(0, str(SCRIPTS))


def load(name):
    """Load a portable helper without installing the skill in a user profile."""
    path = SCRIPTS / (name + ".py")
    assert path.is_file(), f"Missing approved FFA helper: {path.name}"
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def study():
    """Return a documented synthetic study with historical completeness evidence."""
    return {
        "schemaVersion": 1,
        "study": {"location": "Synthetic river", "flowDefinition": "annual instantaneous peak discharge",
                  "units": "cfs", "yearConvention": "waterYear", "waterYearStartMonth": 10,
                  "regulation": "unregulated"},
        "sources": [{"id": "report", "url": "https://example.org/report", "title": "Synthetic fixture",
                     "retrievedUtc": "2026-09-22T00:00:00Z", "locator": "Table 1"}],
        "inputs": {"historical": {"exactData": [
            {"dateTime": "2000-10-01", "value": 100, "evidenceIds": ["report"]},
            {"index": 2002, "value": 200, "evidenceIds": ["report"]}],
            "intervalData": [{"index": 1882, "lowerBound": 115000, "upperBound": 150000, "evidenceIds": ["report"]}],
            "thresholdData": [{"startIndex": 1870, "endIndex": 1922, "value": 110000,
                               "numberAbove": 0, "evidenceIds": ["report"],
                               "completenessRationale": "Fixture explicitly documents no other exceedances."}]}},
        "scenarios": [{"name": "historical", "input": "historical", "kind": "univariate", "options": {},
                       "evidenceIds": ["report"], "rationale": "Test the documented historical information."}],
        "decisions": [{"status": "unresolved", "rationale": "Newspaper crest has no discharge estimate."}]
    }


class PreparationTests(unittest.TestCase):
    def test_historical_exact_records_require_a_screening_decision(self):
        helper = load("prepare_study")
        value = study()
        data = value["inputs"]["historical"]
        data.pop("intervalData")
        data.pop("thresholdData")
        data["exactData"].append({"index": 1882, "value": 150000, "recordType": "historical", "evidenceIds": ["report"]})
        value["scenarios"][0]["kind"] = "bulletin17c"
        with self.assertRaisesRegex(ValueError, "screening"):
            helper.prepare(value)
        data["useMultipleGrubbsBeckTest"] = False
        self.assertEqual(helper.prepare(value)["coverage"]["historical"]["historicalExactYears"], [1882])
        value["scenarios"][0]["screeningInput"] = "historical"
        with self.assertRaisesRegex(ValueError, "cohort"):
            helper.prepare(value)

    def test_unknown_options_at_each_scientific_level_are_rejected(self):
        helper = load("prepare_study")
        cases = [{"useJeffreysRuleForScales": False}, {"bayesianOptions": {"prgnSeed": 123}},
                 {"parameterPriors": [{"parameterName": "Skew (of log)", "distribution": {"type": "normal", "parameters": [0, .3]}, "fixed": True}]},
                 {"quantilePriors": [{"alpha": .01, "distribution": {"type": "normal", "parameter": [400, 40]}}]}]
        for options in cases:
            value = study()
            value["scenarios"][0]["options"] = options
            with self.subTest(options=options), self.assertRaisesRegex(ValueError, "Unknown"):
                helper.prepare(value)

    def test_water_year_and_threshold_accounting(self):
        helper = load("prepare_study")
        original = study()
        result = helper.prepare(original)
        row = result["inputs"]["historical"]["exactData"][0]
        self.assertEqual(row["index"], 2001)
        self.assertNotIn("evidenceIds", row)
        self.assertNotIn("index", original["inputs"]["historical"]["exactData"][0])
        count = result["coverage"]["historical"]["thresholds"][0]
        self.assertEqual(count["remainingYears"], 52)
        self.assertEqual(count["expectedNumberBelow"], 52)
        self.assertEqual(len(result["decisions"]), 1)

    def test_duplicate_years_ambiguous_dates_and_incomplete_thresholds_stop(self):
        helper = load("prepare_study")
        for change, message in [
            (lambda d: d["inputs"]["historical"]["exactData"][1].update(index=2001), "duplicate"),
            (lambda d: d["inputs"]["historical"]["exactData"][0].update(index=2000), "year"),
            (lambda d: d["inputs"]["historical"]["thresholdData"][0].pop("completenessRationale"), "completeness"),
            (lambda d: d["inputs"]["historical"]["intervalData"][0].update(evidenceIds=["missing"]), "evidence"),
            (lambda d: d["inputs"]["historical"]["exactData"][0].update(units="m3/s"), "units")]:
            value = study()
            change(value)
            with self.subTest(message=message), self.assertRaisesRegex(ValueError, message):
                helper.prepare(value)

    def test_overlap_invalid_counts_stop_and_uncertain_b17c_is_retained(self):
        helper = load("prepare_study")
        value = study()
        threshold = value["inputs"]["historical"]["thresholdData"][0]
        value["inputs"]["historical"]["thresholdData"].append(copy.deepcopy(threshold))
        with self.assertRaisesRegex(ValueError, "overlap"):
            helper.prepare(value)
        value = study()
        value["inputs"]["historical"]["thresholdData"][0]["numberAbove"] = 53
        with self.assertRaisesRegex(ValueError, "count"):
            helper.prepare(value)
        value = study()
        value["scenarios"][0]["kind"] = "bulletin17c"
        value["inputs"]["historical"]["uncertainData"] = [{"index": 1890,
            "distribution": {"type": "normal", "parameters": [500, 50]}, "evidenceIds": ["report"]}]
        value["inputs"]["historical"]["useMultipleGrubbsBeckTest"] = False
        prepared = helper.prepare(value)
        self.assertEqual(prepared["inputs"]["historical"]["uncertainData"][0]["distribution"],
                         {"type": "normal", "parameters": [500, 50]})

    def test_skew_mse_and_quantile_spaces_remain_explicit(self):
        helper = load("prepare_study")
        skew = helper.skew_information(-0.17, 0.12, "univariate")
        self.assertAlmostEqual(skew["parameterPriors"][0]["distribution"]["parameters"][1], 0.12 ** 0.5)
        self.assertEqual(helper.skew_information(-0.17, 0.12, "bulletin17c")["parameterPenalties"][0]["mse"], 0.12)
        causal = helper.quantile_information(0.002, 480, 80, "sd", "physical", "univariate")
        self.assertTrue(causal["useSingleQuantile"])
        self.assertEqual(causal["quantilePriors"][0]["distribution"]["parameters"], [480, 80])
        penalty = helper.quantile_information(0.0001, 5.4689, 0.016, "variance", "log10", "bulletin17c")
        self.assertEqual(penalty["quantilePenalties"][0]["mean"], 5.4689)
        self.assertEqual(penalty["quantilePenalties"][0]["mse"], 0.016)
        with self.assertRaisesRegex(ValueError, "log10"):
            helper.quantile_information(0.01, 4, 0.1, "sd", "log10", "univariate")

    def test_multiple_information_requires_dependence_rationale(self):
        helper = load("prepare_study")
        value = study()
        value["scenarios"][0]["information"] = [
            {"type": "skew", "mean": -0.17, "mse": 0.12, "evidenceIds": ["report"], "rationale": "Applicable region"},
            {"type": "quantile", "aep": 0.01, "mean": 400, "uncertainty": 40,
             "uncertaintyKind": "sd", "space": "physical", "evidenceIds": ["report"], "rationale": "Applicable rural regression"}]
        with self.assertRaisesRegex(ValueError, "dependence"):
            helper.prepare(value)
        value["scenarios"][0]["dependenceAssessment"] = "Explicitly adopted conditional independence for this sensitivity candidate only."
        self.assertIn("quantilePriors", helper.prepare(value)["scenarios"][0]["options"])


class ChronologyTests(unittest.TestCase):
    def test_negative_years_zeros_counts_and_zoom_without_fabricated_events(self):
        plot = load("plot_chronology")
        payload = {"schemaVersion": 1, "inputData": {"id": "fixture", "name": "Chronology fixture"},
            "exactData": [{"index": 2000, "value": 0}, {"index": 2002, "value": 15, "isLowOutlier": True}],
            "intervalData": [{"index": 1882, "value": 100, "lowerBound": 80, "upperBound": 120}],
            "uncertainData": [{"index": 1950, "value": 90, "lowerBound": 70, "upperBound": 130}],
            "thresholdData": [{"startIndex": -2000, "endIndex": 1881, "value": 200, "numberAbove": 2, "numberBelow": 3880},
                              {"startIndex": 1999, "endIndex": 1999, "value": 50, "numberAbove": 0, "numberBelow": 1}]}
        fig, ax, notes = plot.build_figure(payload, ylabel="Discharge (cfs)", index_label="Water year")
        self.assertEqual(ax.get_xscale(), "linear")
        self.assertEqual(ax.get_yscale(), "linear")
        self.assertEqual(len(ax.patches), 2)
        self.assertTrue(any("2 above" in t.get_text() for t in ax.texts))
        self.assertLess(ax.get_xlim()[0], -2000)
        self.assertEqual(notes, [])
        with tempfile.TemporaryDirectory() as temp:
            files = plot.render(payload, Path(temp) / "chronology", ylabel="Discharge (cfs)", zoom=(1950, 2002))
            self.assertTrue(all(Path(p).stat().st_size > 100 for p in files))
        plot.plt.close(fig)

    def test_failed_or_missing_model_bounds_do_not_create_a_misleading_plot(self):
        plot = load("plot_chronology")
        with self.assertRaises(ValueError):
            plot.build_figure({"success": False})
        with self.assertRaisesRegex(ValueError, "lowerBound"):
            plot.build_figure({"schemaVersion": 1, "inputData": {"id": "x"}, "exactData": [],
                "uncertainData": [{"index": 2000, "value": 100}]})


if __name__ == "__main__":
    unittest.main()
