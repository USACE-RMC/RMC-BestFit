"""Reproducible distribution and single-source skill package contracts."""
import hashlib
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
import zipfile

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location("package_skill", ROOT / "scripts/package-bestfit-skill.py")
PACKAGE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(PACKAGE)


class PackageTests(unittest.TestCase):
    def test_claude_archive_contains_complete_workflows_and_is_reproducible(self):
        with tempfile.TemporaryDirectory() as temp:
            first, second = Path(temp) / "one.zip", Path(temp) / "two.zip"
            self.assertEqual(PACKAGE.package(first), PACKAGE.package(second))
            with zipfile.ZipFile(first) as archive:
                self.assertIsNone(archive.testzip())
                for name in ("scripts/plot_chronology.py", "scripts/run_study.py", "scripts/prepare_study.py",
                             "scripts/capture_source.py", "references/historical-data.md", "references/information-recipes.md",
                             "references/regional-information.md", "references/study-workflow.md", "assets/synthetic-study.json"):
                    self.assertEqual(archive.read("bestfit-frequency/" + name),
                                     (ROOT / "skills/bestfit-frequency" / name).read_bytes())

    def test_openai_package_uses_same_skill_without_mcp_or_profile_changes(self):
        self.assertTrue(hasattr(PACKAGE, "package_marketplace"), "Missing approved thin-plugin package")
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / "plugin.zip"
            PACKAGE.package_marketplace(path)
            original = path.read_bytes()
            PACKAGE.package_marketplace(path)
            self.assertEqual(original, path.read_bytes())
            with zipfile.ZipFile(path) as archive:
                prefix = "bestfit-frequency-marketplace/"
                catalog = json.loads(archive.read(prefix + ".agents/plugins/marketplace.json"))
                self.assertEqual(catalog["plugins"][0]["source"]["path"], "./plugins/bestfit-frequency")
                root = prefix + "plugins/bestfit-frequency/"
                manifest = json.loads(archive.read(root + ".codex-plugin/plugin.json"))
                self.assertEqual(manifest["skills"], "./skills/")
                self.assertNotIn("mcpServers", manifest)
                self.assertEqual(archive.read(root + "skills/bestfit-frequency/SKILL.md"),
                                 (ROOT / "skills/bestfit-frequency/SKILL.md").read_bytes())
                self.assertFalse(any("__pycache__" in name or name.endswith(".pyc") for name in archive.namelist()))


if __name__ == "__main__":
    unittest.main()
