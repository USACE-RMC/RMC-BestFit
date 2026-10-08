"""Evidence capture and candidate comparison contracts, without network access or estimators."""
import importlib.util
import io
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch
from urllib.error import HTTPError

SCRIPTS = Path(__file__).resolve().parents[2] / "skills/bestfit-frequency/scripts"
sys.path.insert(0, str(SCRIPTS))


def load(name):
    """Load a helper directly from the portable skill."""
    path = SCRIPTS / (name + ".py")
    assert path.is_file(), f"Missing approved FFA helper: {name}"
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class ResearchTests(unittest.TestCase):
    def test_location_queries_include_primary_b17c_usgs_and_nws_sources(self):
        helper = load("capture_source")
        plan = helper.research_plan({"location": "Ouachita River at Blakely Mountain Dam", "siteNumber": "07357000"})
        self.assertTrue(any("tm4B5" in url for url in plan["primaryResources"]))
        self.assertTrue(any("07357000" in q and "usgs.gov" in q for q in plan["queries"]))
        self.assertTrue(any("weather.gov" in q for q in plan["queries"]))

    def test_service_failure_is_retained_and_not_treated_as_regional_information(self):
        helper = load("capture_source")
        error = HTTPError("https://streamstats.usgs.gov/failure", 503, "unavailable", {}, io.BytesIO(b"unavailable"))
        with tempfile.TemporaryDirectory() as temp, patch.object(helper, "urlopen", side_effect=error):
            output = Path(temp) / "source"
            result = helper.capture("https://streamstats.usgs.gov/failure", output)
            self.assertFalse(result["success"])
            self.assertEqual(result["httpStatus"], 503)
            self.assertEqual((output / "response.bin").read_bytes(), b"unavailable")
            self.assertEqual(len(result["sha256"]), 64)


class ScenarioTests(unittest.TestCase):
    def test_comparison_preserves_failed_candidates_and_diagnostics(self):
        helper = load("run_study")
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            good = root / "baseline"
            good.mkdir()
            (good / "results.json").write_text(json.dumps({"success": True,
                "frequencyCurve": {"probabilities": [0.01], "values": [500], "lowerBounds": [300], "upperBounds": [800]},
                "diagnostics": {"convergenceWarnings": ["low ESS"]}}))
            (good / "analysis.json").write_text(json.dumps({"configuration": {"distribution": "logPearsonTypeIII"}}))
            records = helper.compare(root, [{"name": "baseline", "status": "completed"},
                                           {"name": "historical", "status": "failed", "error": "invalid threshold"}])
            self.assertEqual(records[0]["diagnostics"]["convergenceWarnings"], ["low ESS"])
            self.assertEqual(records[1]["status"], "failed")
            self.assertNotIn("frequencyCurve", records[1])

    def test_screened_cohort_flags_are_transferred_without_rescreening_history(self):
        helper = load("run_study")
        source = {"exactData": [{"index": 2000, "value": 5}, {"index": 1882, "value": 500}], "useMultipleGrubbsBeckTest": True}
        screened = {"inputData": {"id": "cohort", "lowOutlierThreshold": 10},
                    "exactData": [{"index": 2000, "value": 5, "isLowOutlier": True}]}
        result = helper.apply_screening(source, screened)
        self.assertFalse(result["useMultipleGrubbsBeckTest"])
        self.assertEqual([r["isLowOutlier"] for r in result["exactData"]], [True, False])
        self.assertEqual(result["lowOutlierThreshold"], 10)
        self.assertNotIn("isLowOutlier", source["exactData"][0])
        screened["exactData"][0]["value"] = 6
        with self.assertRaisesRegex(ValueError, "cohort"):
            helper.apply_screening(source, screened)

    def test_screening_threshold_never_flags_unscreened_historical_rows(self):
        """The API applies a manual threshold to every exact row, so the transferred one must not reach unscreened rows."""
        helper = load("run_study")
        source = {"exactData": [{"index": 2000, "value": 5}, {"index": 1882, "value": 4}], "useMultipleGrubbsBeckTest": True}
        screened = {"inputData": {"id": "cohort", "lowOutlierThreshold": 10},
                    "exactData": [{"index": 2000, "value": 5, "isLowOutlier": True}]}
        with self.assertRaisesRegex(ValueError, "1882"):
            helper.apply_screening(source, screened)
        # An explicit flag in the study input is the analyst's decision to censor that row.
        source["exactData"][1]["isLowOutlier"] = True
        result = helper.apply_screening(source, screened)
        self.assertEqual([r["isLowOutlier"] for r in result["exactData"]], [True, True])
        self.assertEqual(result["lowOutlierThreshold"], 10)

    def test_screening_flags_are_not_transferred_without_their_threshold(self):
        """The API rejects isLowOutlier flags that have no lowOutlierThreshold to define their censoring."""
        helper = load("run_study")
        screened = {"inputData": {"id": "cohort"}, "exactData": [{"index": 2000, "value": 5, "isLowOutlier": True}]}
        with self.assertRaisesRegex(ValueError, "threshold"):
            helper.apply_screening({"exactData": [{"index": 2000, "value": 5}]}, screened)


if __name__ == "__main__":
    unittest.main()
