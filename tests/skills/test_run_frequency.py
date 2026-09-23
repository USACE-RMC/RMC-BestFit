"""Client request/artifact contracts, without HTTP, downloads, or estimation."""
import importlib.util
from pathlib import Path
import tempfile
import sys
import unittest
from unittest.mock import patch

SCRIPT = Path(__file__).resolve().parents[2] / "skills/bestfit-frequency/scripts/run_frequency.py"
sys.path.insert(0, str(SCRIPT.parent))
SPEC = importlib.util.spec_from_file_location("run_frequency", SCRIPT)
run = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(run)


class RunFrequencyTests(unittest.TestCase):
    def test_screening_default_and_manual_override(self):
        self.assertTrue(run.input_request({}, "bulletin17c", "auto")["useMultipleGrubbsBeckTest"])
        self.assertNotIn("useMultipleGrubbsBeckTest", run.input_request({}, "univariate", "auto"))
        for request in [{"useMultipleGrubbsBeckTest": False}, {"lowOutlierThreshold": 3},
                        {"exactData": [{"value": 1, "isLowOutlier": True}]}]:
            self.assertEqual(run.input_request(request, "bulletin17c", "auto"), request)
        self.assertFalse(run.input_request({}, "bulletin17c", "off")["useMultipleGrubbsBeckTest"])

    def test_run_preserves_options_and_saves_requests_before_calls(self):
        calls = []
        def request(base, path, body=None, timeout=1800):
            calls.append((path, body))
            if path.endswith("/manual"):
                return {"success": True, "inputData": {"id": "input-id"}}
            if "includeData" in path:
                return {"success": True, "exactData": []}
            if path.endswith("/chronology"):
                return {"success": True, "schemaVersion": 1, "inputData": {"id": "input-id"}, "exactData": []}
            if path == "/api/analyses/bulletin17c":
                return {"success": True, "analysis": {"id": "analysis-id"}}
            return {"success": True}
        with tempfile.TemporaryDirectory() as temp, patch.object(run, "request_json", side_effect=request):
            output = Path(temp) / "run"
            run.run_frequency("http://127.0.0.1:5210", "bulletin17c", {"exactData": []}, "manual",
                              {"uncertaintyMethod": "bootstrap"}, "auto", output)
            create = next(body for path, body in calls if path == "/api/analyses/bulletin17c")
            self.assertEqual(create, {"inputDataId": "input-id", "uncertaintyMethod": "bootstrap"})
            for name in ["input-request.json", "analysis-request.json", "input.json", "results.json", "defaults.json", "client-version.json"]:
                self.assertTrue((output / name).is_file(), name)
            for name in ["chronology.json", "chronology.png", "chronology.svg", "source.json"]:
                self.assertTrue((output / name).is_file(), name)
            paths = [path for path, _ in calls]
            self.assertLess(paths.index("/api/inputdata/input-id/chronology"), paths.index("/api/analyses/bulletin17c"))

    def test_prepare_only_never_creates_or_runs_an_analysis(self):
        calls = []
        def request(base, path, body=None, timeout=1800):
            calls.append(path)
            if path.endswith("/manual"):
                return {"inputData": {"id": "input-id"}}
            if path.endswith("/chronology"):
                return {"schemaVersion": 1, "inputData": {"id": "input-id"}, "exactData": []}
            return {"success": True}
        with tempfile.TemporaryDirectory() as temp, patch.object(run, "request_json", side_effect=request):
            run.run_frequency("http://127.0.0.1:5210", "univariate", {"exactData": []}, "manual", {}, "off",
                              Path(temp) / "input", prepare_only=True)
            self.assertFalse(any("/analyses/" in path for path in calls))

    def test_failure_stops_before_analysis_and_is_saved(self):
        with tempfile.TemporaryDirectory() as temp, patch.object(run, "request_json", return_value={"success": False, "errorMessage": "screening failed"}):
            output = Path(temp) / "run"
            with self.assertRaises(RuntimeError):
                run.run_frequency("http://127.0.0.1:5210", "bulletin17c", {}, "manual", {}, "auto", output)
            self.assertFalse((output / "results.json").exists())

    def test_manual_threshold_requires_explicit_flags_before_api_calls(self):
        with tempfile.TemporaryDirectory() as temp, patch.object(run, "request_json", side_effect=AssertionError("Threshold-only screening reached the API before asking for flags")) as request:
            with self.assertRaisesRegex(ValueError, "isLowOutlier"):
                run.run_frequency("http://127.0.0.1:5210", "bulletin17c",
                                  {"lowOutlierThreshold": 25, "exactData": [{"index": 2000, "value": 12}]},
                                  "manual", {}, "auto", Path(temp) / "run")
            request.assert_not_called()


if __name__ == "__main__":
    unittest.main()
