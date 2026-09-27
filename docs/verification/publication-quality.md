<!-- verification-status: internal-evidence-control -->

# Verification report publication quality

This record accompanies the 27 September 2026 edition, prepared for the v2.0.1 release, and is excluded from the public book. The first edition was dated 21 September 2026. It records editorial and engineering checks, not a rerun of the numerical verification library.

## Source and coverage

- BestFit source baseline: `9427bd27c1ed7d757fde5a37ce24b90a06f3c9ed` (RMC.BestFit 2.0.1). The 21 September edition reviewed `8808f19f3bfa712241e55a0dfa47407ff5a60970`.
- Numerics source baseline: `7e8e8d1c5f26e045a35ec9fc09367de95ed05b02`; declared package baseline 2.2.0. Builds use the published RMC.Numerics 2.2.0 package, which was built from that commit.
- On 27 September 2026 the source catalog validator found 329 methods and 329 execution units, with no open catalog gaps.
- Independent identity comparison confirms that Appendix A contains all 329 class/method identities exactly once across 71 classes.
- Scientific dispositions are 327 verified and two accepted limitations. The method added since the 21 September edition is `TimeSeriesIndependentOracleTests.ArimaxDistributedLagConditioningMatchesIndependentOracle`, which checks the ARIMAX max(q, p + b) conditioning order against an independent R calculation.
- Acceptance thresholds and numerical reference values are unchanged. One catalog rule, for the AR(1) maximum-likelihood recovery, now names the Numerics default Differential Evolution population (10k) that its evidence used, rather than a minimum of 100 (review decision D1); the affected maximum-likelihood and MAP evidence was rerun under the shipped default and passed, as recorded in the evidence ledgers. Two SpatialGEV maximum-likelihood recovery entries now point to their 26 September 2026 rerun artifacts.
- Earlier catalog corrections remain: the Flike descriptions match the existing source rules (Example 3 uses 7.5%, Example 6a uses 10%, and Examples 6a/6b use GEV), and the Viglione case-1 reference description states its published-endpoint discrepancy.
- The 56 deleted coverage-study records are preserved separately, outside the current inventory.

## Engineering checks

The strict Debug XML gate passed across 972 source files. Since the 21 September edition, three Verification project files changed. `TimeSeriesIndependentOracleTests.cs` adds the ARIMAX conditioning method, and one of its fixtures now turns the default training window off after attaching its series, so that fixture's window does not change under the approved default-window rule. The project file links the new method's R oracle data. The Log10-Normal equivalence test's XML comment now names the Numerics default population. A static trace of the time-series Verification methods found four whose training window changes under that rule; all four passed in individual single-method runs on 26 September 2026. The documentation regression test checks each report's own metadata, rather than allowing one report's front matter to satisfy the other's metadata contract.

All four fast suites passed with no failures or skipped tests, in Debug and in the clean package-mode Release gate:

| Suite | Passed |
|---|---:|
| Core | 3,519 |
| UI | 701 |
| App | 450 |
| API | 546 |
| Total | 5,216 |

The ten documentation contract tests also passed after the final editorial and rendering changes. They check local links, publication manifests, cross-references, metadata, and the scientific chapter structure. The 21 September edition recorded 949 XML-gated files and 5,038 fast tests.

The XML scanner prunes its existing `bin`, `obj`, and `TestResults` exclusions before traversal and does not follow directory junctions. This prevents unrelated generated caches from blocking source inspection; no source-documentation exclusion was added. The final source-only scan retained the same 949-file denominator.

## Reader-facing review

The editorial review used the repository's v1 report, `docs/reports/RMC-TR-2020-02 - Verification of the Bayesian Estimation and Fitting Software.pdf`, including its distribution-fitting examples, prior-comparison explanations, and Flike and Viglione result tables. Its organizing principle is retained: introduce the engineering question and data, explain the comparison, show supported numerical results, and interpret what they establish. Prior-edition BestFit estimates are not republished as current outputs.

All fifteen analysis types have narrative coverage independent of the code index:

| Analysis | Setup, procedure, and results described |
|---|---|
| Distribution fitting | Named generating families and parameters; independent fitted optima; common-data ranking and weighting. |
| Univariate | Stationary and nonstationary designs; Bayesian recovery; published Bayesian information comparisons. |
| Bulletin 17C | Seven worked examples; covariance; regional information; synthetic recovery; quantile-profile response; bootstrap convergence distinction. |
| Point process | Threshold, exposure, event rates, seasonal dates, mixed observations, prior placement, and simulation. |
| Competing risk | Component parameters, controlling shares, dependence, MLE/Bayesian recovery, and prior sensitivity. |
| Mixture | Component proportions and parameters; exact-zero probability; EM/Bayesian procedure; independent fitted reference. |
| Composite | Combination rules; fixed distributions; controlled uncertainty; fitted children; independent resampling. |
| Bivariate | Marginal distributions, copula parameters, independent optima, and dependence recovery. |
| Coincident frequency | Linear and nonlinear response tables, integration accuracy, and propagation of input uncertainty. |
| Rating curve | Named controls, calibration ranges, noise, likelihoods, continuity, fitted outputs, and predictive bands. |
| AR | First-order mean/persistence experiment; conditional optimum and recurrence. |
| MA | First-order innovation experiment; conditional optimum and recurrence. |
| ARIMA | Log transformation, differencing, known coefficients, independent profile intervals, and level reconstruction. |
| ARIMAX | Dated covariates, difference-scale regression, known coefficients, independent optimum, and forecast. |
| Spatial extremes | Network geometry, missing observations, GEV parents, dependence, recovery, held-out prediction, and uncertainty methods. |

The estimation chapter additionally explains analytical prior-to-posterior calculations, profile likelihood, covariance, information criteria, influence, and sampling diagnostics. The data chapters describe controlled import/persistence examples and incomplete-record warnings. The methodology defines the statistical terms needed to read these chapters.

Reference values, actual retained BestFit values, and pass-within-rule outcomes are labelled separately. Missing fitted-output tables are acknowledged rather than reconstructed from the generating truth or from another program's estimates. The profile-likelihood illustration is explicitly an independent reference plot, reproducible with `scripts/build-verification-report-figures.py` using Python, NumPy, and Matplotlib; its SVG is included with the report sources.

## Published-reference decision

The current `ViglioneEtAlData.Test1_ExactData_1951_2001` target for the Q1000 lower endpoint is 163 m³/s. The original Skahill et al. (2016) note, Table 2 on page 5, gives 183 m³/s. The repository's v1 report, Table 83 on printed page 58, also gives 183 in the external-reference column and 163 in the BestFit column. The original note was checked directly at its existing reference URL: https://usace.contentdm.oclc.org/digital/api/collection/p266001coll1/id/4163/download.

The XML comments, catalog description, analysis chapter, and conclusion now state that agreement with the retained 163 target does not verify this published endpoint. Other case-1 reference targets match the published table. No test target, tolerance, algorithm, or scientific disposition was changed. A technical-authority decision is required before altering the reference-result contract; the release plan carries that decision explicitly. The catalog's zero-open-gap result is a structural inventory result, not resolution of this source discrepancy.

## Rendering controls

The final artifact is `output/pdf/rmc-bestfit-verification-report.pdf`: 95 pages, 18 chapters, and 20 navigation bookmarks. Its SHA-256 is `905b57a16fcaed8404472f50eee3f3ba1fda7e9cc409763c2ff0386afe764e15`. It replaces the reviewed 21 September artifact (SHA-256 `8c842772cafdcd60952a836dc2a8da2c65c4a03d597571a9383e313edc8e354c`).

- All 95 pages of the 21 September artifact were rendered with Poppler and visually reviewed. Contents numbering, headers, footers, equation tags, table wrapping, and chapter transitions were checked. Equations, the contents page, and dense tables were also inspected at full rendering size.
- For this edition, every page of the reviewed and rebuilt PDFs was rendered with Poppler 24.04 at 120 dpi and compared over the body and running header, excluding only the footer. Of the first rebuilt candidate's 95 pages, 83 exactly match a reviewed page. The other 12 (1, 3, 5, 10, 55-59, 66, 67 and 90) were inspected individually at full size with their neighboring pages, and every page break among them was read for continuity. The cover and document control show the 2.0.1 checkpoint, the Numerics commit, the 2.2.0 package baseline, and the draft status.
- That inspection found one content error: the time-series conclusion still counted sixteen independent calculation checks after the ARIMAX conditioning method made them seventeen (thirteen shared and four model-interaction checks). It was corrected. The final build differs from the inspected candidate only on that page (59), which was inspected again.
- Main text is set at 10.24 pt with 8.7 pt tables. Document control and the code index use 9.6 pt text and 8.1 pt tables. No body text extends outside the printable horizontal bounds. Light table headers preserve contrast for mathematical symbols. Cover decoration is clipped so it cannot shrink the publication's typography.
- All 216 unique equations passed LaTeX compilation and a complete SVG glyph-reference check (212 in the 21 September edition). Every equation is vector geometry, including its number; no external font or network fetch is required to display it.
- The SVG guard was tested against the original missing-glyph failure and rejected it. The build now uses scalable Computer Modern glyphs and runs the guard before printing.
- All 216 inline and display equations also rendered without error in MathJax 3.2.2. Markdown uses GitHub-compatible dollar delimiters. This is a local compatibility check, not a published GitHub preview.
- The HTML check found no broken equation images, internal fragments, external `file:` links, malformed table rows, or horizontal overflow. The PDF contains 45 internal and 441 external links; all 81 commit-pinned source-file links resolve to files at the recorded source baseline.
- The PDF embeds the current catalog and report metadata as attachments. Their extracted bytes match the repository files exactly. Appendix A matches all 329 class, method, and source-file identities exactly once, including methods in partial class files.
- Layout patterns already present in the reviewed artifact remain: code-formatted names can break inside the word in narrow table cells (for example `posterior` in the executive summary), inline equations sit slightly below the text baseline, and a small gap follows code-formatted words before punctuation.

Reproduction commands, run from the repository root with the installed .NET, Python, Node.js, LaTeX, dvisvgm, and Chromium dependencies:

```powershell
.\scripts\validate-code-xml-docs.ps1 -Configuration Debug
dotnet test src/RMC.BestFit.Tests -c Debug --no-build
dotnet test src/RMC.BestFit.UI.Tests -c Debug --no-build
dotnet test src/RMC.BestFit.App.Tests -c Debug --no-build
dotnet test src/RMC.BestFit.Api.Tests -c Debug --no-build
.\scripts\validate-verification-catalog.ps1 -Catalog docs/verification/verification-catalog.json -SourceRoot src/RMC.BestFit.Verification -RequireComplete
python scripts/build-verification-coverage.py --check
.\scripts\build-verification-report.ps1
```

PDF creation timestamps can change the file hash on a rebuild; the hash above identifies the reviewed artifact. Rebuilds require a fresh page review. Build-script parameters allow explicit runtime paths when the tools are not on `PATH`.

## Evidence boundaries

Preparing this edition did not alter algorithms, priors, seeds, samplers, or acceptance thresholds, and did not run the numerical Verification suite. The Verification source and catalog changes listed above are the only ones since the 21 September edition. The two v2.0.1 production changes to time-series likelihood windows, the ARIMAX conditioning order (review decision D6) and the default training window, are covered by the new ARIMAX method and the four single-method reruns; the other remediation reruns are recorded in the evidence ledgers. Test results in the public report refer to their supporting catalog and artifact records. The report's source-review date does not assert a common fresh execution checkpoint for all methods.
