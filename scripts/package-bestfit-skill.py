"""Package the portable skill from an explicit file list; do not include caches or local data."""
import argparse
import hashlib
from pathlib import Path
import zipfile

FILES = (
    "SKILL.md", "requirements.txt", "pyproject.toml", "agents/openai.yaml",
    "scripts/run_frequency.py", "scripts/plot_frequency.py", "scripts/plot_source.py",
    "references/setup.md", "references/workflow.md", "references/plot-contract.md",
    "references/install.md", "assets/synthetic-annual-flows.json",
    "scripts/plot_chronology.py", "scripts/prepare_study.py", "scripts/capture_source.py", "scripts/run_study.py",
    "references/historical-data.md", "references/regional-information.md", "references/information-recipes.md",
    "references/study-workflow.md", "assets/synthetic-study.json",
    "references/app-plot-map.json", "references/saved-plots.md", "references/examples.md",
    "bestfit_plots/__init__.py", "bestfit_plots/spec.py", "bestfit_plots/render.py",
    "bestfit_plots/legacy_frequency.py", "bestfit_plots/source.py",
    "bestfit_plots/adapters/__init__.py", "bestfit_plots/adapters/common.py",
    "bestfit_plots/adapters/input_data.py", "bestfit_plots/adapters/frequency.py",
    "bestfit_plots/adapters/diagnostics.py", "bestfit_plots/adapters/response_models.py",
    "bestfit_plots/adapters/api_response_models.py", "bestfit_plots/adapters/desktop.py",
)


def skill_entries():
    """Read the single maintained skill and license from an explicit allowlist."""
    root = Path(__file__).resolve().parents[1]
    skill = root / "skills" / "bestfit-frequency"
    entries = [(name, (skill / name).read_bytes()) for name in FILES]
    entries.append(("LICENSE", (root / "LICENSE").read_bytes()))
    return entries


def write_archive(output, entries):
    """Write stable names, timestamps and permissions and return the archive checksum."""
    output = Path(output)
    output.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(output, "w", compression=zipfile.ZIP_DEFLATED) as archive:
        for name, content in sorted(entries):
            info = zipfile.ZipInfo(name, date_time=(1980, 1, 1, 0, 0, 0))
            info.compress_type = zipfile.ZIP_DEFLATED
            info.external_attr = 0o100644 << 16
            archive.writestr(info, content)
    digest = hashlib.sha256(output.read_bytes()).hexdigest()
    Path(str(output) + ".sha256").write_text(f"{digest}  {output.name}\n", encoding="utf-8")
    return digest


def package(output):
    """Build the Claude/standalone ZIP with exactly one top-level skill directory."""
    return write_archive(output, [("bestfit-frequency/" + name, content) for name, content in skill_entries()])


def package_marketplace(output):
    """Build a thin local OpenAI skills-only plugin without writing any user profile."""
    root = Path(__file__).resolve().parents[1]
    templates = root / "packaging/bestfit-frequency"
    prefix = "bestfit-frequency-marketplace/"
    plugin = prefix + "plugins/bestfit-frequency/"
    entries = [(prefix + ".agents/plugins/marketplace.json", (templates / "marketplace.json").read_bytes()),
               (plugin + ".codex-plugin/plugin.json", (templates / "plugin.json").read_bytes())]
    entries.extend((plugin + "skills/bestfit-frequency/" + name, content) for name, content in skill_entries())
    return write_archive(output, entries)


def main():
    """Package to the default artifact path or a caller-supplied ZIP path."""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=Path(__file__).resolve().parents[1] / "artifacts/bestfit-frequency-skill.zip")
    parser.add_argument("--plugin-output", type=Path, default=Path(__file__).resolve().parents[1] / "artifacts/bestfit-frequency-marketplace.zip")
    args = parser.parse_args()
    digest = package(args.output)
    print(f"{args.output.resolve()}\nSHA-256 {digest}")
    digest = package_marketplace(args.plugin_output)
    print(f"{args.plugin_output.resolve()}\nSHA-256 {digest}")


if __name__ == "__main__":
    main()
