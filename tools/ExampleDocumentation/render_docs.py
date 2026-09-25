"""Build local HTML previews and check links for example Markdown guides.

Install markdown-it-py in addition to the plotting requirements. Serve the
repository root on loopback to view the generated pages under artifacts/.
"""
import argparse
import html
import json
from pathlib import Path
import re
from urllib.parse import unquote, urlsplit

from markdown_it import MarkdownIt

ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / "artifacts/example-documentation/site"
CSS = """body{font:17px/1.6 system-ui,sans-serif;color:#202830;background:#f8f9fb;margin:0}
main{max-width:1000px;margin:auto;padding:42px 42px 70px;background:white}
h1{font-size:34px;line-height:1.2}h2{font-size:25px;margin-top:36px}h3{font-size:21px}
a{color:#165786}img{display:block;width:100%;height:auto;margin:24px auto 10px}
table{border-collapse:collapse;font-size:14px;width:100%;margin:22px 0}
td,th{border:1px solid #cad2d9;padding:9px;text-align:left;overflow-wrap:normal}
th{background:#edf2f6}pre{padding:16px;background:#f2f5f7;overflow:auto;font-size:14px}
code{font-size:.88em}li{margin-bottom:12px}blockquote{border-left:4px solid #547c98;padding-left:18px}
p{overflow-wrap:anywhere}nav{font-size:14px;border-bottom:1px solid #cad2d9;padding-bottom:15px}
@media(max-width:700px){main{padding:22px 18px}table{font-size:12px}h1{font-size:28px}}
"""


def slug(text):
    """Use the simple heading anchors needed by the example guides."""
    return re.sub(r"[^\w\- ]", "", text.lower()).replace(" ", "-")


def headings(tokens):
    """Assign unique heading IDs and return the anchors used by this preview."""
    anchors, duplicates = set(), {}
    for i, token in enumerate(tokens):
        if token.type != "heading_open":
            continue
        base = slug(tokens[i + 1].content)
        occurrence = duplicates.get(base, 0)
        duplicates[base] = occurrence + 1
        anchor = f"{base}-{occurrence}" if occurrence else base
        token.attrSet("id", anchor)
        anchors.add(anchor)
    return anchors


def render(path):
    """Render one page and return local-link failures and figure count."""
    parser = MarkdownIt("commonmark", {"html": True}).enable("table")
    tokens = parser.parse(path.read_text(encoding="utf-8"))
    anchors = headings(tokens)
    failures, count = [], 0
    for token in tokens:
        for child in token.children or []:
            key = "src" if child.type == "image" else "href" if child.type == "link_open" else None
            if key is None:
                continue
            if child.type == "image":
                count += 1
            value = child.attrGet(key)
            parts = urlsplit(value)
            if parts.scheme or parts.netloc:
                continue
            target = (path.parent / unquote(parts.path)).resolve() if parts.path else path.resolve()
            if not target.is_relative_to(ROOT) or not target.exists():
                failures.append(value)
                continue
            if parts.fragment and target.suffix == ".md":
                target_anchors = anchors if target == path.resolve() else headings(parser.parse(target.read_text(encoding="utf-8")))
                if unquote(parts.fragment) not in target_anchors:
                    failures.append(value)
            # Only examples are built here. Keep other Markdown links pointing
            # at their actual source files instead of nonexistent HTML previews.
            if target.suffix == ".md" and target.is_relative_to(ROOT / "examples"):
                target = OUTPUT / target.relative_to(ROOT).with_suffix(".html")
            child.attrSet(key, "/" + target.relative_to(ROOT).as_posix() + ("#" + parts.fragment if parts.fragment else ""))
    body = parser.renderer.render(tokens, parser.options, {})
    title = next((tokens[i+1].content for i,t in enumerate(tokens) if t.type == "heading_open"), path.stem)
    destination = OUTPUT / path.relative_to(ROOT).with_suffix(".html")
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_text(f'<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width"><title>{html.escape(title)}</title><style>{CSS}</style></head><body><main><nav>{html.escape(path.relative_to(ROOT).as_posix())}</nav>{body}</main></body></html>', encoding="utf-8")
    return {"page": path.relative_to(ROOT).as_posix(), "figures": count, "brokenLinks": failures,
            "preview": destination.relative_to(ROOT).as_posix()}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--only", default="")
    args = parser.parse_args()
    pages = [render(path) for path in sorted((ROOT / "examples").rglob("*.md")) if args.only in str(path)]
    (OUTPUT / "qa-links.json").write_text(json.dumps(pages, indent=2), encoding="utf-8")
    print(json.dumps({"pages": len(pages), "figures": sum(p['figures'] for p in pages),
                      "failures": [p for p in pages if p['brokenLinks']]}, indent=2))
    if not pages or any(p["brokenLinks"] for p in pages):
        raise SystemExit(1)


if __name__ == "__main__":
    main()
