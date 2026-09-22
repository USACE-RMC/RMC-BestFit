"""Package the portable skill from an explicit file list; do not include caches or local data."""
import argparse
import hashlib
from pathlib import Path
import zipfile

FILES = (
    "SKILL.md", "requirements.txt", "pyproject.toml", "agents/openai.yaml",
    "scripts/run_frequency.py", "scripts/plot_frequency.py", "scripts/plot_source.py",
    "references/setup.md", "references/workflow.md", "references/plot-contract.md",
    "references/install.md", "references/app-plot-map.json", "references/saved-plots.md", "assets/synthetic-annual-flows.json",
    "bestfit_plots/__init__.py", "bestfit_plots/spec.py", "bestfit_plots/render.py",
    "bestfit_plots/legacy_frequency.py", "bestfit_plots/source.py",
    "bestfit_plots/adapters/__init__.py", "bestfit_plots/adapters/common.py",
    "bestfit_plots/adapters/input_data.py", "bestfit_plots/adapters/frequency.py",
    "bestfit_plots/adapters/diagnostics.py", "bestfit_plots/adapters/response_models.py",
    "bestfit_plots/adapters/api_response_models.py",
)


def package(output):
    """Build a reproducible ZIP containing one skill folder and return its SHA-256."""
    root = Path(__file__).resolve().parents[1]
    skill = root / "skills" / "bestfit-frequency"
    entries = [(name, (skill / name).read_bytes()) for name in FILES]
    entries.append(("LICENSE", (root / "LICENSE").read_bytes()))
    output = Path(output)
    output.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(output, "w", compression=zipfile.ZIP_DEFLATED) as archive:
        for name, content in sorted(entries):
            info = zipfile.ZipInfo("bestfit-frequency/" + name, date_time=(1980, 1, 1, 0, 0, 0))
            info.compress_type = zipfile.ZIP_DEFLATED
            info.external_attr = 0o100644 << 16
            archive.writestr(info, content)
    digest = hashlib.sha256(output.read_bytes()).hexdigest()
    Path(str(output) + ".sha256").write_text(f"{digest}  {output.name}\n", encoding="utf-8")
    return digest


def main():
    """Package to the default artifact path or a caller-supplied ZIP path."""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=Path(__file__).resolve().parents[1] / "artifacts/bestfit-frequency-skill.zip")
    args = parser.parse_args()
    digest = package(args.output)
    print(f"{args.output.resolve()}\nSHA-256 {digest}")


if __name__ == "__main__":
    main()
