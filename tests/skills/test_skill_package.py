"""Reproducible distribution and single-source skill package contracts."""
import hashlib
import importlib.util
import json
from pathlib import Path
import re
import struct
import tempfile
import unittest
import zipfile
from urllib.parse import urlparse

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location("package_skill", ROOT / "scripts/package-bestfit-skill.py")
PACKAGE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(PACKAGE)


class PackageTests(unittest.TestCase):
    def test_all_distributions_include_the_maintained_privacy_policy(self):
        policy = (ROOT / "docs/plugin-privacy.md").read_bytes()
        builds = (
            (PACKAGE.package, ("bestfit-frequency/",)),
            (PACKAGE.package_marketplace, (
                "rmc-bestfit-marketplace/plugins/rmc-bestfit/",
                "rmc-bestfit-marketplace/plugins/rmc-bestfit/skills/bestfit-frequency/")),
            (PACKAGE.package_openai_plugin, ("", "skills/bestfit-frequency/")),
            (PACKAGE.package_claude_plugin, (
                "rmc-bestfit-claude-plugin/",
                "rmc-bestfit-claude-plugin/skills/bestfit-frequency/")),
        )
        with tempfile.TemporaryDirectory() as temp:
            for index, (build, roots) in enumerate(builds):
                with self.subTest(build=build.__name__):
                    path = Path(temp) / f"package-{index}.zip"
                    build(path)
                    with zipfile.ZipFile(path) as archive:
                        for root in roots:
                            self.assertEqual(archive.read(root + "PRIVACY.md"), policy)

    def test_directory_metadata_identifies_the_purpose_and_public_policy(self):
        templates = ROOT / "packaging/bestfit-frequency"
        openai = json.loads((templates / "plugin.json").read_text(encoding="utf-8"))
        claude = json.loads((templates / "claude-plugin.json").read_text(encoding="utf-8"))
        catalog = json.loads((templates / "marketplace.json").read_text(encoding="utf-8"))
        claude_catalog = json.loads((templates / "claude-marketplace.json").read_text(encoding="utf-8"))
        self.assertEqual(openai["name"], claude["name"])
        self.assertEqual(catalog["plugins"][0]["name"], openai["name"])
        self.assertEqual(claude_catalog["plugins"][0]["name"], claude["name"])
        self.assertEqual(openai["version"], claude["version"])
        self.assertRegex(openai["version"], r"^\d+\.\d+\.\d+$")
        self.assertEqual(openai["interface"]["displayName"], "RMC-BestFit")
        self.assertEqual(claude["displayName"], openai["interface"]["displayName"])
        self.assertEqual(openai["interface"]["category"], "Data & Analytics")
        self.assertEqual(catalog["plugins"][0]["category"], openai["interface"]["category"])
        self.assertEqual(claude_catalog["plugins"][0]["category"], "data-analysis")
        self.assertEqual(openai["interface"]["privacyPolicyURL"], claude["privacyPolicyUrl"])
        for field in ("websiteURL", "supportURL", "privacyPolicyURL"):
            parsed = urlparse(openai["interface"][field])
            self.assertEqual(parsed.scheme, "https")
            self.assertEqual(parsed.netloc, "github.com")
            self.assertTrue(parsed.path.startswith("/USACE-RMC/RMC-BestFit"))
        policy_path = urlparse(openai["interface"]["privacyPolicyURL"]).path
        self.assertEqual(policy_path, "/USACE-RMC/RMC-BestFit/blob/main/docs/plugin-privacy.md")
        self.assertTrue((ROOT / policy_path.split("/blob/main/", 1)[1]).is_file())
        self.assertEqual(claude["supportUrl"], openai["interface"]["supportURL"])
        self.assertEqual(claude["documentationUrl"], openai["interface"]["websiteURL"])

    def test_claude_directory_autodetects_full_size_official_icon(self):
        artwork = (ROOT / "packaging/bestfit-frequency/assets/bestfit-icon-512.png").read_bytes()
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / "claude-plugin.zip"
            PACKAGE.package_claude_plugin(path)
            with zipfile.ZipFile(path) as archive:
                content = archive.read("rmc-bestfit-claude-plugin/.claude-plugin/icon.png")
                self.assertEqual(content, artwork)
                self.assertEqual(content[:8], b"\x89PNG\r\n\x1a\n")
                width, height = struct.unpack_from(">II", content, 16)
                self.assertEqual(width, height)
                self.assertGreaterEqual(width, 512)
                self.assertLessEqual(width, 2048)
                self.assertLess(len(content), 2 * 1024 * 1024)

    def test_claude_directory_package_contains_required_root_readme(self):
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / "claude-plugin.zip"
            PACKAGE.package_claude_plugin(path)
            with zipfile.ZipFile(path) as archive:
                readme_path = "rmc-bestfit-claude-plugin/README.md"
                self.assertIn(readme_path, archive.namelist(),
                              "Claude directory submission requires a README in the plugin folder")
                readme = archive.read(readme_path).decode("utf-8")
                prose = re.sub(r"(?ms)^```.*?^```[^\n]*", "", readme)
                self.assertGreaterEqual(len(prose.split()), 40,
                                        "Claude directory README needs at least 40 words outside code blocks")

    def test_public_openai_metadata_and_referenced_artwork_meet_submission_limits(self):
        templates = ROOT / "packaging/bestfit-frequency"
        manifest = json.loads((templates / "plugin.json").read_text(encoding="utf-8"))
        interface = manifest["interface"]
        for field, limit in (("displayName", 30), ("shortDescription", 30),
                             ("longDescription", 4000), ("developerName", 80), ("defaultPrompt", 128)):
            with self.subTest(field=field):
                self.assertGreater(len(interface[field]), 0)
                self.assertLessEqual(len(interface[field]), limit)
        for field in ("logo", "composerIcon"):
            with self.subTest(field=field):
                self.assertIn(field, interface)
                asset = Path(interface[field])
                self.assertFalse(asset.is_absolute())
                self.assertNotIn("..", asset.parts)
                content = (templates / asset).read_bytes()
                self.assertEqual(content[:8], b"\x89PNG\r\n\x1a\n")
                width, height = struct.unpack_from(">II", content, 16)
                self.assertEqual(width, height, "Plugin artwork must be square")
                self.assertGreaterEqual(width, 48)

    def test_shared_skill_description_fits_claude_chat_limit(self):
        skill = (ROOT / "skills/bestfit-frequency/SKILL.md").read_text(encoding="utf-8")
        frontmatter = skill.split("---", 2)[1]
        description = next(line.removeprefix("description: ") for line in frontmatter.splitlines()
                           if line.startswith("description: "))
        self.assertGreater(len(description), 0)
        self.assertLessEqual(len(description), 200)

    def test_submission_root_all_assets_payload_parity_and_reproducible_checksums(self):
        self.assertTrue(hasattr(PACKAGE, "package_openai_plugin"), "Missing OpenAI submission package")
        builds = (
            ("skill", PACKAGE.package, "bestfit-frequency/", None),
            ("marketplace", PACKAGE.package_marketplace,
             "rmc-bestfit-marketplace/plugins/rmc-bestfit/skills/bestfit-frequency/",
             "rmc-bestfit-marketplace/plugins/rmc-bestfit/"),
            ("openai-plugin", PACKAGE.package_openai_plugin, "skills/bestfit-frequency/", ""),
            ("claude-plugin", PACKAGE.package_claude_plugin,
             "rmc-bestfit-claude-plugin/skills/bestfit-frequency/", None),
        )
        expected_skill = dict(PACKAGE.skill_entries())
        with tempfile.TemporaryDirectory() as temp:
            for label, build, skill_root, openai_root in builds:
                with self.subTest(package=label):
                    path = Path(temp) / (label + ".zip")
                    digest = build(path)
                    original = path.read_bytes()
                    self.assertEqual(digest, hashlib.sha256(original).hexdigest())
                    self.assertEqual(Path(str(path) + ".sha256").read_text(encoding="utf-8"),
                                     f"{digest}  {path.name}\n")
                    self.assertEqual(build(path), digest)
                    self.assertEqual(path.read_bytes(), original)
                    with zipfile.ZipFile(path) as archive:
                        self.assertIsNone(archive.testzip())
                        names = archive.namelist()
                        actual_skill = {name[len(skill_root):]: archive.read(name) for name in names
                                        if name.startswith(skill_root)}
                        self.assertEqual(actual_skill, expected_skill)
                        self.assertFalse(any(name.endswith((".dll", ".exe", ".so", ".dylib", ".pyc"))
                                             or "__pycache__" in name or "mcp.json" in name for name in names))
                        if openai_root is not None:
                            manifest = json.loads(archive.read(openai_root + ".codex-plugin/plugin.json"))
                            self.assertNotIn("mcpServers", manifest)
                            for field in ("logo", "composerIcon"):
                                relative = manifest["interface"][field].removeprefix("./")
                                self.assertEqual(archive.read(openai_root + relative),
                                                 (ROOT / "packaging/bestfit-frequency" / relative).read_bytes())
                        if label == "openai-plugin":
                            self.assertEqual({name.split("/")[0] for name in names},
                                             {".codex-plugin", "skills", "assets", "PRIVACY.md"})
                            self.assertFalse(any(name.endswith("marketplace.json") for name in names))

    def test_claude_archive_contains_complete_workflows_and_is_reproducible(self):
        with tempfile.TemporaryDirectory() as temp:
            first, second = Path(temp) / "one.zip", Path(temp) / "two.zip"
            self.assertEqual(PACKAGE.package(first), PACKAGE.package(second))
            with zipfile.ZipFile(first) as archive:
                self.assertIsNone(archive.testzip())
                for name in ("scripts/plot_chronology.py", "scripts/run_study.py", "scripts/prepare_study.py",
                             "scripts/capture_source.py", "references/historical-data.md", "references/information-recipes.md",
                             "references/regional-information.md", "references/study-workflow.md", "references/examples.md", "assets/synthetic-study.json"):
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
                prefix = "rmc-bestfit-marketplace/"
                catalog = json.loads(archive.read(prefix + ".agents/plugins/marketplace.json"))
                self.assertEqual(catalog["plugins"][0]["source"]["path"], "./plugins/rmc-bestfit")
                root = prefix + "plugins/rmc-bestfit/"
                manifest = json.loads(archive.read(root + ".codex-plugin/plugin.json"))
                self.assertEqual(manifest["skills"], "./skills/")
                self.assertNotIn("mcpServers", manifest)
                self.assertEqual(archive.read(root + "skills/bestfit-frequency/SKILL.md"),
                                 (ROOT / "skills/bestfit-frequency/SKILL.md").read_bytes())
                self.assertEqual(archive.read(root + "skills/bestfit-frequency/references/examples.md"),
                                 (ROOT / "skills/bestfit-frequency/references/examples.md").read_bytes())
                self.assertFalse(any("__pycache__" in name or name.endswith(".pyc") for name in archive.namelist()))

    def test_claude_plugin_uploads_or_serves_as_its_own_marketplace(self):
        self.assertTrue(hasattr(PACKAGE, "package_claude_plugin"), "Missing Claude plugin package")
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / "claude-plugin.zip"
            PACKAGE.package_claude_plugin(path)
            original = path.read_bytes()
            PACKAGE.package_claude_plugin(path)
            self.assertEqual(original, path.read_bytes())
            with zipfile.ZipFile(path) as archive:
                self.assertIsNone(archive.testzip())
                names = archive.namelist()
                # Uploads accept a plugin root at the top of the archive or one folder down.
                root = "rmc-bestfit-claude-plugin/"
                self.assertTrue(all(name.startswith(root) for name in names))
                manifest = json.loads(archive.read(root + ".claude-plugin/plugin.json"))
                openai = json.loads((ROOT / "packaging/bestfit-frequency/plugin.json").read_text(encoding="utf-8"))
                self.assertEqual(manifest["name"], "rmc-bestfit")
                self.assertEqual(manifest["version"], openai["version"])
                self.assertNotIn("mcpServers", manifest)
                catalog = json.loads(archive.read(root + ".claude-plugin/marketplace.json"))
                entry = catalog["plugins"][0]
                self.assertEqual((entry["name"], entry["source"]), (manifest["name"], "./"))
                self.assertNotIn("version", entry)
                skill = root + "skills/bestfit-frequency/"
                standalone = {name for name, _ in PACKAGE.skill_entries()}
                self.assertEqual({name[len(skill):] for name in names if name.startswith(skill)}, standalone)
                for name in ("SKILL.md", "references/install.md", "scripts/run_study.py"):
                    self.assertEqual(archive.read(skill + name), (ROOT / "skills/bestfit-frequency" / name).read_bytes())
                self.assertFalse(any("__pycache__" in name or name.endswith(".pyc") for name in names))


if __name__ == "__main__":
    unittest.main()
