"""Generate the consolidated technical-reference bibliography.

Chapter-local reference numbering remains authoritative for citations in each
Markdown page. This script extracts those entries, de-duplicates them by DOI or
normalized title, and records every chapter in which each source appears.
"""

from __future__ import annotations

import argparse
import re
from collections import defaultdict
from pathlib import Path


REFERENCE_RE = re.compile(
    r'^<a id="ref-\d+"></a>\[\d+\]\s*(?P<citation>.+?)\s*$'
)
DOI_RE = re.compile(r"\bdoi:\s*(?P<doi>10\.\d{4,9}/\S+)", re.IGNORECASE)
QUOTED_TITLE_RE = re.compile(r"[“\"](?P<title>.+?)[”\"]")
ITALIC_TITLE_RE = re.compile(r"\*(?P<title>[^*]+)\*")


def normalized_text(value: str) -> str:
    """Return a conservative alphanumeric normalization for matching."""
    return re.sub(r"[^a-z0-9]+", " ", value.casefold()).strip()


def citation_key(citation: str) -> str:
    """Build a stable de-duplication key from DOI, article title, or book title."""
    doi = DOI_RE.search(citation)
    if doi:
        return "doi:" + doi.group("doi").rstrip(".,").casefold()

    quoted = QUOTED_TITLE_RE.search(citation)
    if quoted:
        return "title:" + normalized_text(quoted.group("title"))

    italic = ITALIC_TITLE_RE.search(citation)
    if italic:
        return "title:" + normalized_text(italic.group("title"))

    return "citation:" + normalized_text(citation)


def collect_references(root: Path) -> dict[str, tuple[str, set[Path]]]:
    """Collect unique citations and their source pages."""
    citations: dict[str, str] = {}
    sources: defaultdict[str, set[Path]] = defaultdict(set)

    for path in sorted(root.rglob("*.md")):
        if path.name == "bibliography.md":
            continue
        for line in path.read_text(encoding="utf-8").splitlines():
            match = REFERENCE_RE.match(line)
            if not match:
                continue
            citation = match.group("citation").strip()
            key = citation_key(citation)
            existing = citations.get(key)
            if existing is None or len(citation) > len(existing):
                citations[key] = citation
            sources[key].add(path.relative_to(root))

    return {
        key: (citation, sources[key])
        for key, citation in citations.items()
    }


def render_bibliography(
    references: dict[str, tuple[str, set[Path]]],
) -> str:
    """Render the consolidated bibliography as canonical Markdown."""
    lines = [
        "<!-- technical-reference-status: complete -->",
        "",
        "# Consolidated Bibliography",
        "",
        "[Technical reference](../index.md) | "
        "[Reviewer checklist](reviewer-checklist.md)",
        "",
        "This appendix consolidates the page-local reference lists used throughout "
        "the technical reference. Chapter-local numeric citations remain local to "
        "their chapter; the identifiers below are stable bibliography identifiers "
        "generated from DOI or normalized title. Source pages are listed to make "
        "citation use auditable.",
        "",
    ]

    ordered = sorted(
        references.items(),
        key=lambda item: normalized_text(item[1][0]),
    )
    for index, (_, (citation, paths)) in enumerate(ordered, start=1):
        identifier = f"B{index:03d}"
        source_links = ", ".join(
            f"[{path.as_posix()}](../{path.as_posix()})"
            for path in sorted(paths)
        )
        lines.extend(
            [
                f'<a id="{identifier.casefold()}"></a>',
                f"**[{identifier}]** {citation}",
                "",
                f"Used by: {source_links}.",
                "",
            ]
        )

    lines.extend(
        [
            "## Generation",
            "",
            "Regenerate this file from chapter reference sections with:",
            "",
            "```powershell",
            "python scripts/generate-technical-reference-bibliography.py",
            "```",
            "",
            f"The current file contains {len(ordered)} unique sources after "
            "DOI/title de-duplication.",
            "",
            "---",
            "",
            "[Technical reference](../index.md) | "
            "[Reviewer checklist](reviewer-checklist.md)",
            "",
        ]
    )
    return "\n".join(lines)


def main() -> int:
    """Generate the bibliography and optionally verify it is current."""
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--check",
        action="store_true",
        help="Fail if the generated bibliography differs from the checked-in file.",
    )
    args = parser.parse_args()

    repository_root = Path(__file__).resolve().parents[1]
    technical_root = repository_root / "docs" / "technical-reference"
    output_path = technical_root / "appendices" / "bibliography.md"
    generated = render_bibliography(collect_references(technical_root))

    if args.check:
        if not output_path.exists():
            print(f"Missing generated bibliography: {output_path}")
            return 1
        if output_path.read_text(encoding="utf-8") != generated:
            print("Consolidated bibliography is stale.")
            return 1
        print("Consolidated bibliography is current.")
        return 0

    output_path.write_text(generated, encoding="utf-8", newline="\n")
    print(f"Wrote {output_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
