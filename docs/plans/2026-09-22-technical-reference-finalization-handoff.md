# Technical Reference Finalization Handoff and Implementation Plan

> **For the next session:** Execute Session 2 of the v2.0.1 closeout. Use `superpowers:executing-plans` to carry this bounded documentation task through editing, rendering, review, validation, and a scoped commit. This handoff is an internal work document and must remain outside both publication manifests.

**Goal:** Revise, edit, polish, and finalize the technical-reference Markdown and PDF as a coherent, current, readable draft for inclusion with v2.0.1, completing this work in one session.

**Architecture:** Production source defines implemented behavior; the current verification report and catalog define the numerical evidence available to support it. Markdown remains the publication source. Reuse the verification report's proven equation and publication controls in the technical-reference build, adapting them to the larger book.

**Tech stack:** C#/.NET 10, PowerShell, Markdown, Node.js/Marked, MathJax, LaTeX/dvisvgm, Chromium, Python/pypdf/pdfplumber, and Poppler.

**Spec:** Haden's request to finalize the technical reference using the equation-rendering lessons from the verification report, and [Session 2 of the three-session closeout plan](../verification/v2.0.1-closeout-plan.md#session-2---technical-reference-draft-and-final-evidence-reconciliation).

## Start here: exact handoff state

| Item | State verified on 22 September 2026 |
|---|---|
| Workspace | `C:\GIT\RMC-BestFit` |
| Branch | `documentation-verification-updates` |
| BestFit handoff commit | `f4421e424128e2929235341655192f5d54e08d78` — `Polish verification report and library documentation` |
| Remote | `https://github.com/USACE-RMC/RMC-BestFit.git`; the handoff commit is pushed |
| Working tree before this handoff document | Clean |
| Supporting Numerics checkout | `C:\GIT\Numerics`, commit `7e8e8d1c5f26e045a35ec9fc09367de95ed05b02` |
| Unrelated Numerics work | Modified `.github/workflows/Release.yml`; preserve it |
| Current source/package labels | BestFit 2.0.0; declared Numerics package baseline 2.2.0; local builds select the sibling Numerics project |
| Schedule | Session 1 complete; finish Session 2 by 23 September, Session 3 release preparation by 25 September, release target no later than 28 September 2026 |

This document may be committed after the handoff commit above. At startup, read `AGENTS.md`, inspect both repository statuses and HEADs, and review any intervening commits. Do not reset to the recorded commit or overwrite new work.

Read these repository files before editing:

1. `docs/verification/v2.0.1-closeout-plan.md`, especially the reviewed-commit table and Session 2.
2. `docs/verification/publication-quality.md` and the current verification report's methodology, relevant analysis chapters, conclusions, and complete test index.
3. `docs/technical-reference/book-order.txt`, `documentation-contract.md`, `front-matter.md`, `index.md`, `api-traceability.md`, and `review-findings.md`.
4. `docs/technical-reference/appendices/notation.md`, `parameterization-crosswalk.md`, `implementation-source-index.md`, `verification-evidence.md`, and `bibliography.md`.
5. `docs/report-metadata.json`, both report builders, `scripts/pdf_report_common.py`, and `scripts/validate-equation-assets.py`.
6. `src/RMC.BestFit.Tests/Documentation/TechnicalReferenceDocumentationTests.cs` and the compiled example regions under its `Examples` directory.
7. The v1 user guide and verification report in `docs/reports/` for practitioner-facing explanations and reference examples. Prior-edition numbers are not current software results.

## Preserve the completed verification work

The verification report is the editorial and rendering reference for this session:

- `output/pdf/rmc-bestfit-verification-report.pdf`: 95 reviewed pages, 18 chapters, 20 bookmarks, and 212 unique inline/display equations.
- Reviewed PDF SHA-256: `8c842772cafdcd60952a836dc2a8da2c65c4a03d597571a9383e313edc8e354c`. Its download from the pushed commit was checked against this hash.
- The current catalog and Appendix A cover 328 methods in 71 classes: 326 verified dispositions and two accepted limitations. The 56 removed coverage-study records are preserved separately in `verification-catalog-retired.json` and are outside the current inventory.
- All 37 changed Verification C# files contain XML-only changes; executable tests, reference values, and tolerances are unchanged.
- The latest pre-push fast gate passed Core 3,434, UI 645, App 444, and API 515 tests: 5,038 total, zero failures or skips. The strict Debug XML gate passed across 949 source files. The numerical Verification suite was not rerun.

Those counts describe the completed verification-report checkpoint, not automatic acceptance criteria for the technical reference. Do not rebuild or rewrite that PDF merely to change its date. If shared rendering changes affect its behavior, perform a separate temporary regression build and compare the relevant rendering and structural checks; retain the reviewed deliverable unless a concrete correction requires a replacement and fresh QA.

## Global constraints and editorial requirements

- Haden remains the final technical and numerical authority. Preserve algorithms, formulas defining implemented methods, likelihoods, priors, samplers, seeds, defaults, convergence rules, tolerances, reference contracts, and public APIs. Documentation may correct a demonstrable description/transcription error to match the established implementation. A substantive mathematical disagreement becomes a specific finding for Haden, not an algorithm repair.
- Complete the technical-reference work within this session. Reserve version changes, package-only release validation, release workflow changes, tags, and publication for Session 3. Finish the draft without claiming external scientific approval or inventing report numbers, reviewers, or signatures.
- Write a fresh technical manual describing the repository. Keep change logs, phase/chunk labels, investigation narratives, and historical execution receipts in internal work documents. Preserve necessary limitations in plain language.
- Write for technically qualified water-resources practitioners who need not read C#. Explain the engineering question and process before presenting implementation details. Keep exact source/API traceability available without making it the main narrative.
- Keep the Bayesian-first treatment: Bayesian inference and uncertainty propagation are primary; MLE/MAP and GMM have their actual roles. Do not introduce traditional method-of-moments estimation or expose plain HMC as a BestFit option.
- Audit all 66 current publication entries and all 15 univariate families, plus every analysis family. Existing `complete` markers do not establish current correctness. Use the documentation contract and API traceability matrix to identify omissions.
- Source-backed validation statements must agree with current tests, the catalog, and the verification report. Distinguish reference targets from recorded BestFit outputs, recovery from repeated-sample coverage, and numerical reliability from convergence.
- Preserve unrelated dirty work. Commit only reviewed task files after required validation. Any push must remain within the user's existing explicit authorization; do not infer permission to merge, tag, or publish a release.

## Known scientific boundaries

Carry these qualifications into the relevant technical-reference sections:

1. Generic prior sampling does not incorporate an additional soft joint-prior coupling; the independent marginal sampler is an accepted limitation.
2. One-step GMM observation-influence magnitude is not calibrated to exact-deletion scale; the supported outlier-ranking claim is narrower.
3. The recorded B17C experiment delivered 1,000 accepted refits, comprising 947 outer-converged and 53 capped fits. Inner optimizer fallback and outer convergence are separate properties; accepted output is not interval coverage.
4. The Viglione/Kamp systematic-only case's Q1000 lower endpoint remains a source discrepancy: the retained test target is 163 m3/s, while Skahill et al. (2016), Table 2, gives 183 m3/s. The v1 report's Table 83 places 163 in the BestFit column and 183 in the external-source column. Preserve the explicit qualification. Do not alter the fixture or claim complete published-endpoint parity without Haden's decision. Continue the rest of the document while that numerical decision remains open.
5. Do not expand claims to broad B17C interval coverage, difficult correlated Bayesian competing-risk cases, untested rating-curve extrapolation, arbitrary spatial network sizes, or universal MCMC convergence.

## Task 1: Reconcile the complete technical narrative

**Files:** Chapters listed in `docs/technical-reference/book-order.txt`; `front-matter.md`; `documentation-contract.md`; `api-traceability.md`; the notation, parameterization, implementation, evidence, and bibliography appendices; `docs/report-metadata.json`.

- [ ] Make an internal checklist of all 66 manifest entries and mark each source/API/evidence/editorial review complete. Reorganize only where it improves the manual; keep the manifest, links, and traceability synchronized.
- [ ] Review relevant changes since the technical reference's old BestFit checkpoint `4304fb39f8e162cdb746083108042df88e153afc` and Numerics checkpoint `90a63a46394db9ef95e72b0fcbba943408110636`. Use the closeout plan's reviewed-commit table to focus the comparison; inspect supporting implementations in `C:\GIT\Numerics` read-only.
- [ ] Reconcile each model's purpose, notation and units, parameterization and support, observation model, likelihood, priors/posterior, estimation, numerical implementation, assumptions/identifiability, limitations, and verification evidence. Explain orchestration, uncertainty propagation, outputs, validation, cancellation, and persistence where the analysis contract requires them.
- [ ] Concentrate source checks on distribution tails/support/constraints and logarithm/sign conventions; MLE/MAP bounds and DE defaults; BFGS supplied-gradient roundoff handling; B17C analytical Jacobians, covariance repair, and convergence semantics; PSIS/R-hat/ESS and influence; time-series conditioning and transforms; discharge-space rating likelihood and simultaneous predictive bands; spatial marginalization, distances, prediction, and uncertainty dispatch.
- [ ] Replace stale validation totals in `appendices/verification-evidence.md` and related matrices with claims traceable to the current catalog and report. Do not copy the 328-method total into every chapter as proof of scientific completeness.
- [ ] Reconcile all code examples with the compiled snippet regions. Preserve exact snippets where the contract requires them; use prose or explicitly labelled pseudocode for conceptual steps. Add or modify source examples only when needed for a concrete documentation correction, without changing numerical behavior.
- [ ] Update the technical reference's own source-review date, actual BestFit/Numerics checkpoints, and declared package baseline. The current front matter and shared metadata still show 28 August and Numerics 2.1.4. Use `technical_reference.checkpoint`, following `verification_report.checkpoint`, and make the HTML builder consume the same override as the shared PDF finalizer. Do not overwrite the verification report's checkpoint or mislabel BestFit as released 2.0.1 before the release work.
- [ ] Remove log-style content from the publication, define every symbol at first use, check units and logarithm bases, repair chapter/figure/table/equation references, and regenerate the consolidated bibliography after changing local references.

**Reviewable result:** A coherent source-audited manual with a complete chapter checklist, current traceability, and explicit evidence boundaries. Store the checklist and any remaining findings outside the publication manifest.

## Task 2: Apply the equation-rendering lessons

**Files:** `scripts/build-technical-reference-book.mjs`, `scripts/build-technical-reference-book.ps1`, `scripts/validate-equation-assets.py`, and narrowly scoped shared helpers if needed. Use `scripts/build-verification-report.mjs/.ps1` as the working reference.

### Markdown and GitHub

- [ ] Keep inline mathematics in `$...$`. Put display `$$` delimiters on separate lines, with blank lines outside the block. Do not replace canonical mathematics with images or put equations in code fences.
- [ ] Use supported TeX consistently, preserve stable equation tags, and define notation locally. Keep long derivations readable with suitable aligned expressions instead of shrinking them to fit.
- [ ] Protect ordinary code, backticks, and literal dollar signs during extraction. Check equations in table cells: use suitable delimiters such as `\lvert ... \rvert` instead of a raw pipe that splits Markdown columns. Do not mechanically rewrite correct mathematical meaning.
- [ ] Parse and render every unique inline and display equation with MathJax, and inspect malformed table rows, broken fragments, and missing equation source. The verification report passed a local MathJax 3.2.2 check for 212 equations; that was a compatibility check, not an inspection of GitHub's published renderer. Record the technical reference's own equation count and engine version. If a push is authorized, inspect representative actual GitHub pages too and state exactly what was checked.

### Offline PDF equations

- [ ] Change the technical builder so its generated TeX uses `\usepackage[OT1]{fontenc}` instead of `\usepackage[T1]{fontenc}`, following the verification builder; escape the backslash appropriately in the JavaScript string. T1/EC bitmap fallback previously omitted equation-number glyphs silently in SVG output. A successful LaTeX or dvisvgm exit code did not detect the loss.
- [ ] Generate vector outlines with dvisvgm `--no-fonts --exact-bbox --page=1-`. Preserve glyphs for ordinary mathematics, subscripts, superscripts, text within equations, and equation numbers. Do not rely on web fonts, network math rendering, or raster equations in the delivered PDF.
- [ ] Remove only stale generated `eq-*.svg` files from the verified task output directory before conversion; normalize filenames to `eq-0001.svg`, `eq-0002.svg`, and so on. The current technical builder checks only the first expected file and lacks the verification builder's cleanup, canonicalization, and complete asset guard.
- [ ] Call `scripts/validate-equation-assets.py` with the equation directory and `--tex-source` before Chromium printing. It checks the exact asset set against `\begin{preview}` blocks, vector paths, absence of font-dependent text/raster content, and resolution of every local glyph reference. Keep failure fatal; do not bypass it to obtain a PDF.
- [ ] Check both report builders' temporary paths. They currently share `tmp/pdfs/equations` and `tmp/pdfs/chromium-profile`; do not run them concurrently. Prefer report-specific temporary directories for any regression builds so one report cannot consume the other's equations or invalidate retained QA. Resolve and bound any cleanup path inside the intended workspace.

**Reviewable result:** Every technical-reference equation is valid in the Markdown math path and complete in the actual offline PDF assets; stale files cannot disguise missing equations.

## Task 3: Finish layout, navigation, and reproducible delivery

**Files:** Technical-reference builder/finalizer; `scripts/pdf_report_common.py` only as required; `output/pdf/rmc-bestfit-technical-reference.pdf`; a new internal `docs/technical-reference/publication-quality.md` excluded from `book-order.txt`.

- [ ] Apply the verification report's cover `overflow: hidden` fix. Oversized cover decoration previously expanded the document's printable width and made Chromium shrink the entire book's typography. Check actual PDF text sizes and page bounds, not CSS declarations alone.
- [ ] Use the verified report as a typography starting point: approximately 10.25 pt main text and 8.7 pt ordinary tables, with carefully justified smaller index/control text. Keep headings, context paragraphs, equations, captions, and short tables together where practical. Avoid both orphaned equations and large blank areas from over-broad keep-together rules.
- [ ] Use light table headers with dark text when headers contain equation SVGs. Their black vector glyphs do not inherit white CSS text color and were hard to read on navy headers. Check dense tables and long symbol definitions at full size.
- [ ] Provide printed contents page numbers, clickable contents, bookmarks, internal equation/section references, consistent headers/footers, and readable chapter transitions. Do not blindly reuse `_add_contents_page_numbers`: it currently assumes a one-page contents section at PDF page index 1, and `_find_chapter_pages` begins at index 2. The technical book has 66 entries; discover all contents pages and the actual first chapter so multi-page contents cannot be mistaken for chapter openings.
- [ ] Convert publication links outside the book into durable, commit-pinned HTTPS links. Use repository `blob` links for files and `tree` links for directories; preserve relevant fragments. Internal book links remain local PDF destinations. Reject missing targets, machine-specific paths, and external `file:` links. Verify links point to the intended source checkpoint and do not imply that older catalog content is the newly reviewed snapshot.
- [ ] Build the technical-reference PDF, render every page with Poppler, inspect all pages, and inspect dense equations/tables and contents pages at full resolution. Contact sheets help navigation but do not replace readable inspection. Iterate on source/layout until no clipped content, missing symbols, collisions, unexplained empty pages, or unreadable tables remain.
- [ ] Record the final page/chapter/bookmark/equation counts, exact source/dependency metadata, link and asset checks, visual review, test results, and SHA-256 in the internal publication-quality record. PDF creation timestamps can change the hash; after the final reviewed build, do not rebuild casually. Any replacement needs renewed QA and its own hash.
- [ ] Preserve meaningful PDF text, links, and tagging through finalization. Vector equations can be absent from extracted plain text even when displayed correctly; use SVG checks and page rendering together, not text extraction as the sole mathematics check.

Useful local implementation aids from Session 1, if still present: `tmp/pdfs/check-report-math.mjs`, `tmp/pdfs/final-report-qa.py`, `tmp/pdfs/math-and-html-qa.json`, and `tmp/pdfs/final-review/`. These are ignored, report-specific scratch files. Adapt useful checks into maintained scripts if needed; do not depend on their old counts, hard-coded paths, or availability in a clean checkout.

## Task 4: Validate, checkpoint, and hand back the finished draft

Use the bundled runtime discovery tool when Node/Python/Poppler are not on `PATH`. The report PowerShell builders already accept explicit `-NodePath`, `-NodeModulesPath`, `-PythonPath`, `-LatexPath`, `-DvisvgmPath`, and `-BrowserPath` values. Do not change project or system configuration just to address sandbox access to existing SDKs or packages.

Run from `C:\GIT\RMC-BestFit`, checking each exit code before proceeding:

```powershell
$reportPython = 'C:\Users\haden\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'

# Regenerate after citation edits, then check the committed projection.
& $reportPython scripts/generate-technical-reference-bibliography.py
& $reportPython scripts/generate-technical-reference-bibliography.py --check

# Documentation/API/source checks and the four mandatory fast suites.
dotnet test src/RMC.BestFit.Tests -c Debug -- --filter "FullyQualifiedName~RMC.BestFit.Tests.Documentation.TechnicalReferenceDocumentationTests"
.\scripts\validate-code-xml-docs.ps1 -Configuration Debug
dotnet test src/RMC.BestFit.Tests -c Debug --no-build
dotnet test src/RMC.BestFit.UI.Tests -c Debug --no-build
dotnet test src/RMC.BestFit.App.Tests -c Debug --no-build
dotnet test src/RMC.BestFit.Api.Tests -c Debug --no-build

# Preserve report/library alignment.
.\scripts\validate-verification-catalog.ps1 -Catalog docs/verification/verification-catalog.json -SourceRoot src/RMC.BestFit.Verification -RequireComplete
& $reportPython scripts/build-verification-coverage.py --check

# Build after incorporating the equation guard, then perform the full visual QA.
.\scripts\build-technical-reference-book.ps1
Get-FileHash output/pdf/rmc-bestfit-technical-reference.pdf -Algorithm SHA256
git diff --check
```

- [ ] Build scripts and complete-document tests must verify metadata, local links/fragments, manifest order, API traceability, exact C# snippets, citations/bibliography, and absence of internal work-history prose. Add only focused checks justified by the rendering failure modes above; do not weaken existing contracts.
- [ ] Confirm all four fast suites pass. Run the full strict XML gate for any C# or XML/example changes; it is also the established checkpoint command above. State whether validation used local Numerics or package mode. Package-only release acceptance remains Session 3.
- [ ] Do not run the whole numerical Verification project. If a concrete discrepancy requires execution, use only an authorized affected scope under `AGENTS.md`; evidence-grade runs require `scripts/run-verification-test.ps1`, one exact method, and exactly one TRX result. A documentation review is not permission to refresh all results.
- [ ] Review the final diff and all new files, including whitespace/conflict checks on generated assets. Matplotlib adds trailing spaces to SVG path lines; follow the normalization in `scripts/build-verification-report-figures.py` without changing plotted coordinates.
- [ ] Commit only the validated technical-reference chapters, necessary shared controls/tests, quality record, and reviewed PDF. Update the Session 2 checklist and leave a concise Session 3 handoff. Preserve the sibling Numerics workflow modification.
- [ ] In the final response, provide the exact commit, tests and dependency mode, final PDF path/hash, page/equation counts, and any precise unresolved scientific qualification. Provide a download URL only after a successful authorized push/upload, and verify the retrieved PDF hash. Local file citations did not give this user a downloadable phone attachment; do not describe them as uploaded attachments. A commit-pinned raw GitHub PDF URL worked after the previous authorized push.

## Definition of done and stop boundary

Every manifest chapter has been reviewed; the technical reference accurately explains the current models, methods, assumptions, implementation, uncertainty, and evidence in polished prose. GitHub-compatible mathematics and the PDF's complete offline equations have been checked separately. The final PDF has readable typography, correct navigation, usable source links, and a recorded review of every page. Required checks pass and the scoped changes are committed. The verification report retains its reviewed content and scientific boundaries.

Finish this deliverable rather than returning another proposal-only handoff. Stop before release version changes, broad numerical remediation, package publication, merge, or tag creation. An unresolved source discrepancy may remain explicitly qualified in a completed draft; it must not be silently resolved by changing mathematics or overstating evidence.
