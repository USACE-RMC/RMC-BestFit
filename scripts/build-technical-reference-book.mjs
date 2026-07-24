import fs from "node:fs";
import path from "node:path";
import { createRequire } from "node:module";
import { pathToFileURL } from "node:url";

const scriptDirectory = path.dirname(new URL(import.meta.url).pathname.replace(/^\/([A-Za-z]:)/, "$1"));
const repositoryRoot = path.resolve(scriptDirectory, "..");
const referenceRoot = path.join(repositoryRoot, "docs", "technical-reference");
const manifestPath = path.join(referenceRoot, "book-order.txt");
const htmlOutputPath = path.resolve(process.argv[2] || path.join(repositoryRoot, "tmp", "pdfs", "rmc-bestfit-technical-reference.html"));
const equationOutputPath = path.resolve(process.argv[3] || path.join(repositoryRoot, "tmp", "pdfs", "technical-reference-equations.tex"));

function loadMarked() {
    const searchRoots = [
        path.join(repositoryRoot, "node_modules"),
        process.env.BESTFIT_NODE_MODULES
    ].filter(Boolean);

    for (const searchRoot of searchRoots) {
        try {
            const requireFromRoot = createRequire(pathToFileURL(path.join(searchRoot, "package.json")));
            return requireFromRoot("marked");
        } catch {
            // Continue to the next explicitly scoped dependency location.
        }
    }

    throw new Error("Unable to load the marked package. Set BESTFIT_NODE_MODULES to a Node dependency directory containing marked.");
}

const { marked } = loadMarked();

function slugify(value) {
    return value
        .toLowerCase()
        .normalize("NFKD")
        .replace(/[\u0300-\u036f]/g, "")
        .replace(/[^a-z0-9]+/g, "-")
        .replace(/^-+|-+$/g, "") || "section";
}

function escapeHtml(value) {
    return value
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;");
}

function normalizeRelativePath(value) {
    return value.split(path.sep).join("/");
}

const equations = [];
const equationIndexByKey = new Map();

function registerEquation(tex, display) {
    const normalizedTex = tex.trim();
    const key = (display ? "display:" : "inline:") + normalizedTex;
    let index = equationIndexByKey.get(key);

    if (index === undefined) {
        equations.push({ tex: normalizedTex, display });
        index = equations.length;
        equationIndexByKey.set(key, index);
    }

    return "RMCEQUATION" + index + "TOKEN";
}

function extractEquations(source) {
    const protectedSegments = [];
    const protect = (value) => {
        const index = protectedSegments.push(value) - 1;
        return "RMCPROTECTED" + index + "TOKEN";
    };

    let transformed = source.replace(/\x60{3}[\s\S]*?\x60{3}/g, protect);
    transformed = transformed.replace(/\x60[^\x60\n]+\x60/g, protect);
    transformed = transformed.replace(/\$\$([\s\S]*?)\$\$/g, (match, tex) => registerEquation(tex, true));
    transformed = transformed.replace(/\\\[([\s\S]*?)\\\]/g, (match, tex) => registerEquation(tex, true));
    transformed = transformed.replace(/(?<!\\)\$([^$\n]+?)(?<!\\)\$/g, (match, tex) => registerEquation(tex, false));
    transformed = transformed.replace(/\\\(([\s\S]*?)\\\)/g, (match, tex) => registerEquation(tex, false));

    return transformed.replace(/RMCPROTECTED(\d+)TOKEN/g, (match, index) => protectedSegments[Number(index)]);
}

function replaceEquationTokens(html) {
    return html.replace(/RMCEQUATION(\d+)TOKEN/g, (match, rawIndex) => {
        const index = Number(rawIndex);
        const equation = equations[index - 1];
        if (!equation) {
            throw new Error("Missing equation record for token " + match);
        }

        const source = "equations/eq-" + String(index).padStart(4, "0") + ".svg";
        const alternateText = escapeHtml(equation.tex.replace(/\s+/g, " "));
        if (equation.display) {
            return "<span class=\"display-equation\" role=\"img\" aria-label=\"" + alternateText +
                "\"><img src=\"" + source + "\" alt=\"\"></span>";
        }

        return "<img class=\"inline-equation\" src=\"" + source + "\" alt=\"" + alternateText + "\">";
    });
}

function buildEquationDocument() {
    const lines = [
        "\\documentclass[10pt]{article}",
        "\\usepackage[T1]{fontenc}",
        "\\usepackage{amsmath,amssymb,mathtools,bm}",
        "\\usepackage[active,tightpage]{preview}",
        "\\PreviewBorder=1pt",
        "\\begin{document}"
    ];

    for (const equation of equations) {
        lines.push("\\begin{preview}");
        if (equation.display) {
            lines.push("\\begin{equation*}");
            lines.push(equation.tex);
            lines.push("\\end{equation*}");
        } else {
            lines.push("\\(" + equation.tex + "\\)");
        }
        lines.push("\\end{preview}");
    }

    lines.push("\\end{document}");
    return lines.join("\n") + "\n";
}

const manifestEntries = fs.readFileSync(manifestPath, "utf8")
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter((line) => line.length > 0 && !line.startsWith("#"));

if (manifestEntries.length === 0) {
    throw new Error("The technical-reference book manifest is empty.");
}

const duplicateEntries = manifestEntries.filter((entry, index) => manifestEntries.indexOf(entry) !== index);
if (duplicateEntries.length > 0) {
    throw new Error("Duplicate book manifest entries: " + [...new Set(duplicateEntries)].join(", "));
}

const documents = manifestEntries.map((entry) => {
    const absolutePath = path.resolve(referenceRoot, entry);
    if (!absolutePath.startsWith(referenceRoot + path.sep) || !fs.existsSync(absolutePath)) {
        throw new Error("Book manifest entry does not resolve to a reference page: " + entry);
    }

    const source = fs.readFileSync(absolutePath, "utf8");
    const titleMatch = source.match(/^#\s+(.+)$/m);
    if (!titleMatch) {
        throw new Error("Book page has no level-one title: " + entry);
    }

    return {
        entry,
        absolutePath,
        source,
        title: titleMatch[1].trim(),
        documentSlug: "doc-" + slugify(entry.replace(/\.md$/i, ""))
    };
});

const documentByPath = new Map(documents.map((document) => [
    normalizeRelativePath(path.relative(referenceRoot, document.absolutePath)),
    document
]));

function cleanSource(document) {
    const headingCounts = new Map();
    let inFence = false;
    let source = document.source
        .replace(/<!--\s*technical-reference-status:[\s\S]*?-->\s*/gi, "")
        .replace(/^\[(Documentation home|Technical reference|API traceability|Documentation contract)[^\n]*\]\([^)]+\)(?:\s*\|\s*\[[^\n]+\]\([^)]+\))*\s*$/gmi, "")
        .replace(/^\s*\[Back to [^\]]+\]\([^)]+\)\s*$/gmi, "");

    const lines = source.split(/\r?\n/).map((line) => {
        if (/^\s*\x60{3}/.test(line)) {
            inFence = !inFence;
            return line;
        }

        if (inFence) {
            return line;
        }

        const headingMatch = line.match(/^(#{1,6})\s+(.+?)\s*$/);
        if (!headingMatch) {
            return line;
        }

        const headingText = headingMatch[2].replace(/\s+\{#[^}]+\}\s*$/, "").trim();
        const baseSlug = document.documentSlug + "-" + slugify(headingText);
        const occurrence = (headingCounts.get(baseSlug) || 0) + 1;
        headingCounts.set(baseSlug, occurrence);
        const uniqueSlug = occurrence === 1 ? baseSlug : baseSlug + "-" + occurrence;
        return headingMatch[1] + " " + headingText + " {#" + uniqueSlug + "}";
    }).join("\n");

    source = lines.replace(/\]\(([^)\s]+\.md)(#[^)]+)?\)/gi, (match, target, fragment) => {
        const targetPath = normalizeRelativePath(path.relative(
            referenceRoot,
            path.resolve(path.dirname(document.absolutePath), decodeURIComponent(target))
        ));
        const targetDocument = documentByPath.get(targetPath);
        if (!targetDocument) {
            return match;
        }

        if (!fragment) {
            return "](#" + targetDocument.documentSlug + ")";
        }

        return "](#" + targetDocument.documentSlug + "-" + slugify(decodeURIComponent(fragment.slice(1))) + ")";
    });

    source = source.replace(/\]\(#([^)]+)\)/g, (match, fragment) => {
        if (fragment.startsWith("doc-")) {
            return match;
        }

        return "](#" + document.documentSlug + "-" + slugify(decodeURIComponent(fragment)) + ")";
    });

    return source.trim();
}

marked.use({
    gfm: true,
    pedantic: false,
    renderer: {
        heading({ tokens, depth, text }) {
            const rawText = text || this.parser.parseInline(tokens);
            const explicitIdMatch = rawText.match(/\s*\{#([^}]+)\}\s*$/);
            const headingText = rawText.replace(/\s*\{#[^}]+\}\s*$/, "");
            const headingId = explicitIdMatch ? explicitIdMatch[1] : slugify(headingText.replace(/<[^>]+>/g, ""));
            return "<h" + depth + " id=\"" + escapeHtml(headingId) + "\">" + headingText + "</h" + depth + ">\n";
        }
    }
});

const renderedDocuments = documents.map((document, index) => {
    const markdown = extractEquations(cleanSource(document));
    const renderedHtml = replaceEquationTokens(marked.parse(markdown));
    const chapterLabel = String(index + 1).padStart(2, "0");
    return [
        "<section class=\"book-document\" id=\"" + document.documentSlug + "\">",
        "<div class=\"chapter-kicker\">Chapter " + chapterLabel + "</div>",
        renderedHtml,
        "</section>"
    ].join("\n");
});

const tocItems = documents.map((document, index) =>
    "<li><a href=\"#" + document.documentSlug + "\"><span class=\"toc-number\">" +
    String(index + 1).padStart(2, "0") + "</span><span>" +
    escapeHtml(document.title) + "</span></a></li>"
).join("\n");

const style = [
    "@page { size: Letter; margin: 0.76in 0.70in 0.72in; }",
    "@page:first { margin: 0; }",
    ":root { --navy: #15324b; --blue: #1e6594; --pale: #edf4f8; --ink: #1f2a33; --muted: #60717f; --line: #c8d4dd; }",
    "* { box-sizing: border-box; }",
    "html { font-size: 9.6pt; }",
    "body { margin: 0; color: var(--ink); font-family: Aptos, 'Segoe UI', Arial, sans-serif; line-height: 1.42; }",
    "a { color: #145f8f; text-decoration: none; }",
    ".cover { page-break-after: always; height: 11in; padding: 1.04in 0.94in 0.86in; color: white; background: linear-gradient(148deg, #10283c 0%, #174c70 62%, #2c779f 100%); position: relative; }",
    ".cover:after { content: ''; position: absolute; right: -1.05in; bottom: -0.80in; width: 4.4in; height: 4.4in; border: 0.19in solid rgba(255,255,255,.13); border-radius: 50%; }",
    ".cover-rule { width: 0.76in; height: 0.08in; background: #84c6e6; margin-bottom: 0.52in; }",
    ".cover h1 { color: white; font-size: 31pt; line-height: 1.08; letter-spacing: -0.4pt; margin: 0 0 0.22in; max-width: 7.0in; }",
    ".cover .subtitle { font-size: 16pt; line-height: 1.35; max-width: 6.8in; color: #d9edf7; }",
    ".cover .edition { position: absolute; left: 0.94in; bottom: 0.92in; font-size: 11pt; line-height: 1.7; color: #e5f2f8; }",
    ".cover .agency { margin-top: 0.58in; font-size: 11pt; text-transform: uppercase; letter-spacing: 1.1pt; color: #b9dbe9; }",
    ".front-matter { page-break-after: always; }",
    ".front-matter h1 { color: var(--navy); border-bottom: 2px solid var(--blue); padding-bottom: 0.10in; }",
    ".toc { columns: 2; column-gap: 0.34in; padding: 0; list-style: none; }",
    ".toc li { break-inside: avoid; margin: 0 0 0.08in; border-bottom: 0.5px dotted #bac6ce; padding-bottom: 0.045in; }",
    ".toc a { display: flex; gap: 0.08in; color: var(--ink); }",
    ".toc-number { color: var(--blue); font-variant-numeric: tabular-nums; min-width: 0.23in; }",
    ".book-document { page-break-before: always; }",
    ".chapter-kicker { color: var(--blue); font-size: 8.5pt; font-weight: 700; letter-spacing: 1.1pt; text-transform: uppercase; margin-bottom: 0.06in; }",
    "h1, h2, h3, h4 { color: var(--navy); page-break-after: avoid; break-after: avoid-page; }",
    "h1 { font-size: 23pt; line-height: 1.12; margin: 0 0 0.22in; padding-bottom: 0.09in; border-bottom: 2px solid var(--blue); }",
    "h2 { font-size: 15.5pt; margin: 0.28in 0 0.10in; padding-bottom: 0.035in; border-bottom: 0.6px solid var(--line); }",
    "h3 { font-size: 12.2pt; margin: 0.20in 0 0.07in; }",
    "h4 { font-size: 10.4pt; margin: 0.16in 0 0.05in; }",
    "p { margin: 0.07in 0 0.10in; orphans: 3; widows: 3; }",
    "ul, ol { margin-top: 0.06in; padding-left: 0.25in; }",
    "li { margin-bottom: 0.035in; }",
    "blockquote { margin: 0.12in 0; padding: 0.04in 0.16in; border-left: 3px solid var(--blue); background: var(--pale); color: #334956; }",
    "table { width: 100%; border-collapse: collapse; margin: 0.12in 0 0.16in; font-size: 8.1pt; page-break-inside: auto; }",
    "thead { display: table-header-group; }",
    "tr { page-break-inside: avoid; }",
    "th { background: var(--navy); color: white; font-weight: 650; text-align: left; }",
    "th, td { border: 0.6px solid #b9c6cf; padding: 0.045in 0.055in; vertical-align: top; overflow-wrap: anywhere; }",
    "tbody tr:nth-child(even) { background: #f5f8fa; }",
    "code { font-family: 'Cascadia Mono', Consolas, monospace; font-size: 0.88em; background: #eef2f5; padding: 0.01in 0.025in; border-radius: 2px; overflow-wrap: anywhere; }",
    "pre { margin: 0.12in 0 0.16in; padding: 0.11in 0.13in; color: #edf5f8; background: #172936; border-left: 3px solid #4f9fc5; border-radius: 3px; white-space: pre-wrap; overflow-wrap: anywhere; font-size: 7.9pt; line-height: 1.34; page-break-inside: avoid; }",
    "pre code { color: inherit; background: transparent; padding: 0; }",
    ".inline-equation { display: inline-block; width: auto; max-width: none; vertical-align: -0.24em; margin: 0 0.025em; }",
    ".display-equation { display: block; text-align: center; margin: 0.13in auto; page-break-inside: avoid; break-inside: avoid; }",
    ".display-equation img { display: inline-block; max-width: 100%; width: auto; height: auto; }",
    "img { max-width: 100%; height: auto; page-break-inside: avoid; }",
    "hr { border: 0; border-top: 1px solid var(--line); margin: 0.22in 0; }",
    ".release-note { padding: 0.14in 0.17in; background: var(--pale); border: 1px solid #c9dce7; border-radius: 4px; }",
    ".review-register h2 { border-top: 3px solid #a94f3c; padding-top: 0.12in; }",
    "@media print { a { color: inherit; } }"
].join("\n");

const html = [
    "<!doctype html>",
    "<html lang=\"en\">",
    "<head>",
    "<meta charset=\"utf-8\">",
    "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">",
    "<title>RMC.BestFit 2.0 Technical Reference</title>",
    "<style>" + style + "</style>",
    "</head>",
    "<body>",
    "<section class=\"cover\">",
    "<div class=\"cover-rule\"></div>",
    "<h1>RMC.BestFit 2.0<br>Technical Reference</h1>",
    "<div class=\"subtitle\">Statistical models, likelihoods, estimation, uncertainty, diagnostics, and scientific API traceability</div>",
    "<div class=\"agency\">Risk Management Center</div>",
    "<div class=\"edition\">Peer-review release<br>RMC.Numerics 2.1.4 parameterization<br>23 July 2026</div>",
    "</section>",
    "<section class=\"front-matter\">",
    "<h1>Contents</h1>",
    "<p class=\"release-note\">This canonical book is generated from the source-audited Markdown chapters. Implementation behavior governs API claims; the review-findings register identifies discrepancies reserved for production-code triage.</p>",
    "<ol class=\"toc\">" + tocItems + "</ol>",
    "</section>",
    renderedDocuments.join("\n"),
    "</body>",
    "</html>"
].join("\n");

fs.mkdirSync(path.dirname(htmlOutputPath), { recursive: true });
fs.mkdirSync(path.dirname(equationOutputPath), { recursive: true });
fs.writeFileSync(htmlOutputPath, html, "utf8");
fs.writeFileSync(equationOutputPath, buildEquationDocument(), "utf8");
console.log("Generated " + htmlOutputPath + " from " + documents.length + " canonical chapters.");
console.log("Prepared " + equations.length + " unique equations in " + equationOutputPath + ".");
