"""Reject incomplete offline equation SVGs, including silently missing font glyphs."""
from __future__ import annotations

import argparse
from pathlib import Path
import re
import xml.etree.ElementTree as ET


def validate(directory: Path, tex_source: Path) -> list[str]:
    """Check asset count, canonical names, vector glyphs, and local references."""
    expected_count = len(re.findall(r"\\begin\{preview\}", tex_source.read_text(encoding="utf-8")))
    expected_names = {f"eq-{index:04d}.svg" for index in range(1, expected_count + 1)}
    actual = {path.name: path for path in directory.glob("eq-*.svg")}
    failures = []
    if not expected_count or set(actual) != expected_names:
        failures.append(f"Expected {expected_count} canonical assets; missing {sorted(expected_names-set(actual))}; extra {sorted(set(actual)-expected_names)}.")
    for name, path in sorted(actual.items()):
        tree = ET.parse(path)
        ids = {node.attrib["id"] for node in tree.iter() if "id" in node.attrib}
        if not any(node.tag.rsplit("}", 1)[-1] == "path" for node in tree.iter()):
            failures.append(f"{name}: no vector glyph paths.")
        for node in tree.iter():
            tag = node.tag.rsplit("}", 1)[-1]
            if tag in {"text", "image"}:
                failures.append(f"{name}: font-dependent text or raster content remains.")
            href = node.attrib.get("{http://www.w3.org/1999/xlink}href", node.attrib.get("href", ""))
            if href and (not href.startswith("#") or href[1:] not in ids):
                failures.append(f"{name}: unresolved glyph reference {href}.")
    return failures


def main() -> None:
    """Validate one rendered equation set without modifying its assets."""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory", type=Path)
    parser.add_argument("--tex-source", required=True, type=Path)
    args = parser.parse_args()
    failures = validate(args.directory, args.tex_source)
    if failures:
        raise SystemExit("Equation asset validation failed:\n" + "\n".join(failures))
    print(f"Validated {len(list(args.directory.glob('eq-*.svg')))} complete vector equations.")


if __name__ == "__main__":
    main()
