# Technical Reference Publication-Quality Record

**Completed:** 22 September 2026. **Scope:** Session 2, technical-reference Markdown, publication controls, and the finished PDF. This is an internal execution record, excluded from both report manifests. It records editorial and engineering acceptance for an external peer-review draft; it does not record external scientific approval.

## Delivered artifact and reviewed source

| Item | Final reviewed value |
|---|---|
| PDF | [`output/pdf/rmc-bestfit-technical-reference.pdf`](../../output/pdf/rmc-bestfit-technical-reference.pdf) |
| SHA-256 | `c373529ac30d7ef114cd4b10bb579e79889d57cc32f2c847618a26be1fa013fc` |
| Pages / chapters / bookmarks | 249 / 66 / 68 |
| Contents | Two pages, physical pages 2 and 3; all 66 chapter destinations have printed page numbers |
| Mathematics | 1,940 source occurrences; 1,256 unique inline/display equations; 1,256 complete offline vector assets |
| Bibliography | 84 deduplicated sources |
| BestFit source checkpoint | `3fa55a0f75bbd583e2fb7fce42180faa376f007c` |
| Numerics source checkpoint | `7e8e8d1c5f26e045a35ec9fc09367de95ed05b02` |
| Version and dependency | BestFit 2.0.0; declared RMC.Numerics 2.2.0; validation used the sibling Numerics project |
| Workspace / branch | `C:\GIT\RMC-BestFit` / `documentation-verification-updates` |
| Release status | Draft for external peer review - not for public release |

The PDF identifies the production source reviewed, before this documentation-only commit. The commit containing this record and the PDF is available with `git log -1 -- docs/technical-reference/publication-quality.md`. Report-specific checkpoint overrides keep the technical HTML, cover, front matter, and PDF metadata consistent while preserving the verification report's separate checkpoint. Session 3 owns the version change and package-only release validation.

## Source, API, evidence, and editorial review

Every entry in `book-order.txt` was reviewed. In the table, **S/A/V/E complete** means that source behavior, API/example consistency, verification/evidence scope, and reader-facing exposition were checked. For overview, governance, and index entries, these checks concern the claims and links they summarize; they do not imply a separate numerical oracle for an index. The 15 family chapters were each checked for parameter order, units, support, sign/logarithm convention, probability functions, moments and limits, inference, implementation, assumptions, examples, and evidence. Existing completeness markers were not used as evidence of correctness.

| No. | Publication entry | Checks | Review focus |
|---|---|---|---|
| 1 | [front-matter.md](front-matter.md) | S/A/V/E complete | Actual source/dependency checkpoint, authors, draft status, review notice |
| 2 | [index.md](index.md) | S/A/V/E complete | Reader route, Bayesian emphasis, current dependency, capability boundaries |
| 3 | [documentation-contract.md](documentation-contract.md) | S/A/V/E complete | Scientific treatment and traceability requirements |
| 4 | [models/overview.md](models/overview.md) | S/A/V/E complete | Data/prior/full target, pointwise unit, model and analysis roles |
| 5 | [models/parameters-and-priors.md](models/parameters-and-priors.md) | S/A/V/E complete | Bounds, Jeffreys and quantile terms, Jacobians, conjugate information example |
| 6 | [support/trend-functions.md](support/trend-functions.md) | S/A/V/E complete | Trend coordinates, nonstationarity, covariate and parameter ordering |
| 7 | [support/link-functions.md](support/link-functions.md) | S/A/V/E complete | Forward/inverse derivatives, domains, caller Jacobian, SES limitation |
| 8 | [support/trend-and-link-functions.md](support/trend-and-link-functions.md) | S/A/V/E complete | Composition of regression and links, related chapter routing |
| 9 | [estimation/index.md](estimation/index.md) | S/A/V/E complete | Estimator roles and supported samplers |
| 10 | [estimation/maximum-likelihood.md](estimation/maximum-likelihood.md) | S/A/V/E complete | Bounds, optimizer defaults, data target, profile interpretation |
| 11 | [estimation/maximum-a-posteriori.md](estimation/maximum-a-posteriori.md) | S/A/V/E complete | Posterior target, coordinate dependence, covariance and profiles |
| 12 | [estimation/generalized-method-of-moments.md](estimation/generalized-method-of-moments.md) | S/A/V/E complete | Moments, weighting, three outer stop rules, BFGS, sandwich and penalties |
| 13 | [estimation/bayesian-mcmc.md](estimation/bayesian-mcmc.md) | S/A/V/E complete | DEMCz/DEMCzs/ARWMH/NUTS, targets, retained draws and diagnostics |
| 14 | [estimation/model-comparison.md](estimation/model-comparison.md) | S/A/V/E complete | DIC/WAIC/PSIS-LOO units, reliability and evidence scope |
| 15 | [estimation/diagnostics.md](estimation/diagnostics.md) | S/A/V/E complete | Rank-normalized R-hat, bulk/tail ESS and numerical interpretation |
| 16 | [estimation/influence-diagnostics.md](estimation/influence-diagnostics.md) | S/A/V/E complete | Influence scale limitation, GMM curvature, time-series prior decomposition |
| 17 | [estimation/predictive-checks.md](estimation/predictive-checks.md) | S/A/V/E complete | Prior/posterior prediction, replicated observations, joint-prior limitation |
| 18 | [analysis/overview.md](analysis/overview.md) | S/A/V/E complete | Orchestration, terminal state, usable results and reproducibility |
| 19 | [data/time-series-data.md](data/time-series-data.md) | S/A/V/E complete | Chronology, gaps, interval, provenance and persistence |
| 20 | [data/input-data.md](data/input-data.md) | S/A/V/E complete | Source selection, processing and analysis-ready observations |
| 21 | [data-frame/index.md](data-frame/index.md) | S/A/V/E complete | Exact/censored/threshold/interval/uncertain likelihood contributions |
| 22 | [analysis/distribution-fitting.md](analysis/distribution-fitting.md) | S/A/V/E complete | Candidate fitting, selection, plotting and external family comparisons |
| 23 | [distributions/index.md](distributions/index.md) | S/A/V/E complete | Fifteen-family inventory and terminology |
| 24 | [distributions/univariate.md](distributions/univariate.md) | S/A/V/E complete | Shared mixed-observation model, priors, nonstationarity and inference |
| 25 | [distributions/exponential.md](distributions/exponential.md) | S/A/V/E complete | Shift/scale, support endpoint and boundary-aware recovery |
| 26 | [distributions/gamma.md](distributions/gamma.md) | S/A/V/E complete | Scale/shape mapping, support, incomplete-Gamma functions and inference |
| 27 | [distributions/generalized-extreme-value.md](distributions/generalized-extreme-value.md) | S/A/V/E complete | Numerics shape sign, support, Gumbel limit and finite moments |
| 28 | [distributions/generalized-logistic.md](distributions/generalized-logistic.md) | S/A/V/E complete | Hosking transform, sign convention, tail and moment limits |
| 29 | [distributions/generalized-normal.md](distributions/generalized-normal.md) | S/A/V/E complete | Hosking family, transformed-Normal moment derivation and mode |
| 30 | [distributions/generalized-pareto.md](distributions/generalized-pareto.md) | S/A/V/E complete | Shape sign, finite endpoint, threshold/exposure distinction |
| 31 | [distributions/gumbel.md](distributions/gumbel.md) | S/A/V/E complete | Location/scale, extreme-value limit and upper-tail behavior |
| 32 | [distributions/kappa-four.md](distributions/kappa-four.md) | S/A/V/E complete | Two-shape support, limiting families, moments, mode and integration |
| 33 | [distributions/ln-normal.md](distributions/ln-normal.md) | S/A/V/E complete | Arithmetic response moments versus latent natural-log parameters |
| 34 | [distributions/logistic.md](distributions/logistic.md) | S/A/V/E complete | Symmetry, scale convention, moments and inference |
| 35 | [distributions/log-normal.md](distributions/log-normal.md) | S/A/V/E complete | Base-10 parameterization and original-measure Jacobian |
| 36 | [distributions/log-pearson-type-iii.md](distributions/log-pearson-type-iii.md) | S/A/V/E complete | Log-space moments, Pearson mapping, support and B17C relationship |
| 37 | [distributions/normal.md](distributions/normal.md) | S/A/V/E complete | Mean/standard deviation, analytical inference and oracle scope |
| 38 | [distributions/pearson-type-iii.md](distributions/pearson-type-iii.md) | S/A/V/E complete | Mean/SD/skew, shifted-Gamma mapping and negative skew |
| 39 | [distributions/weibull.md](distributions/weibull.md) | S/A/V/E complete | Scale-before-shape, support and moment conventions |
| 40 | [analysis/univariate.md](analysis/univariate.md) | S/A/V/E complete | Frequency ordinates, uncertainty, saved draws and reporting |
| 41 | [analysis/bulletin-17c.md](analysis/bulletin-17c.md) | S/A/V/E complete | Specialized method, source examples, qualified reliability evidence |
| 42 | [analysis/bulletin-17c-estimation.md](analysis/bulletin-17c-estimation.md) | S/A/V/E complete | Expected moments, penalties, analytical P3/LP3 Jacobian and convergence |
| 43 | [analysis/bulletin-17c-uncertainty.md](analysis/bulletin-17c-uncertainty.md) | S/A/V/E complete | Covariance, linked MVN, pivotal bootstrap and ensemble terminology |
| 44 | [distributions/point-process.md](distributions/point-process.md) | S/A/V/E complete | Exposure, intensity, seasonal blocks, threshold choice and annual maxima |
| 45 | [distributions/competing-risks.md](distributions/competing-risks.md) | S/A/V/E complete | Min/max composition, dependence, identifiability and prior-sensitive recovery |
| 46 | [distributions/mixture.md](distributions/mixture.md) | S/A/V/E complete | Full-K physical model, K-1 stored coordinates, hurdle and EM evidence |
| 47 | [distributions/composite.md](distributions/composite.md) | S/A/V/E complete | Model combination, weights and distinct source uncertainties |
| 48 | [analysis/composite.md](analysis/composite.md) | S/A/V/E complete | Results-only orchestration and independent posterior indexing |
| 49 | [analysis/bivariate.md](analysis/bivariate.md) | S/A/V/E complete | Seven copulas, densities, tail dependence, IFM and fixed margins |
| 50 | [analysis/coincident-frequency.md](analysis/coincident-frequency.md) | S/A/V/E complete | Joint event semantics, response integration and product-posterior propagation |
| 51 | [analysis/rating-curve.md](analysis/rating-curve.md) | S/A/V/E complete | Discharge-measure likelihood, predictive versus latent bands, grid coverage |
| 52 | [analysis/time-series.md](analysis/time-series.md) | S/A/V/E complete | Conditional sample by family, transforms, differencing and forecasting |
| 53 | [analysis/autoregressive.md](analysis/autoregressive.md) | S/A/V/E complete | AR conditioning from p, transformed residuals and prior terms |
| 54 | [analysis/moving-average.md](analysis/moving-average.md) | S/A/V/E complete | Zero presample innovations and all residual contributions from index zero |
| 55 | [analysis/arima.md](analysis/arima.md) | S/A/V/E complete | max(p,q) start, raw-index Jacobian and forecast reintegration |
| 56 | [analysis/arimax.md](analysis/arimax.md) | S/A/V/E complete | max(p,q,b) window, dated regressors, lags and future covariates |
| 57 | [spatial/spatial-extremes.md](spatial/spatial-extremes.md) | S/A/V/E complete | Hierarchy, marginalization, distances, prediction and uncertainty dispatch |
| 58 | [api-traceability.md](api-traceability.md) | S/A/V/E complete | Current public API mapping and claim-specific evidence |
| 59 | [appendices/notation.md](appendices/notation.md) | S/A/V/E complete | Symbols, dimensions, probability conventions and local overrides |
| 60 | [appendices/parameterization-crosswalk.md](appendices/parameterization-crosswalk.md) | S/A/V/E complete | Natural/log/link/stored coordinates and API order |
| 61 | [appendices/glossary.md](appendices/glossary.md) | S/A/V/E complete | Consistent statistical and engineering terminology |
| 62 | [appendices/implementation-source-index.md](appendices/implementation-source-index.md) | S/A/V/E complete | Current BestFit/Numerics sources and test ownership |
| 63 | [distributions/verification-matrix.md](distributions/verification-matrix.md) | S/A/V/E complete | Formula, optimum, recovery and published-example acceptance rules |
| 64 | [appendices/verification-evidence.md](appendices/verification-evidence.md) | S/A/V/E complete | 328-method inventory, two accepted limitations and retired-study separation |
| 65 | [appendices/reviewer-checklist.md](appendices/reviewer-checklist.md) | S/A/V/E complete | Method-specific external review questions and evidence boundaries |
| 66 | [appendices/bibliography.md](appendices/bibliography.md) | S/A/V/E complete | 84-source regeneration, deduplication and chapter backreferences |

The revisions emphasize explanation rather than only API inventory. Worked additions explain conjugate prior/data precision and the distinction between parameter and future-observation variance; derive transformed-Normal moments and the GNO mode; explain linearization behind GMM sandwich covariance; derive the systematic P3/LP3 moment Jacobian and its link-space chain rule; and explain the maximum-over-grid construction of a simultaneous rating-curve verification band. Existing extended treatments of mixed observations, MCMC, copulas, time-series forecasting, and spatial prediction were reconciled with their implementations and current evidence.

An independent final-reference review found four important and two minor documentation issues, all resolved: the negative-Clayton CDF/domain and limiting cases; the maximum-competing-risk prior qualification; fragmented TeX; an overgeneralized posterior-curvature interpretation of GMM; the GMM covariance cross-reference; and a stale bivariate recovery count. Follow-up source checks also corrected the missing-covariate contract, the time-series conditioning summary and Jeffreys-prior classification, and the SES custom-curvature limitation. These were corrections to descriptions, not changes to the numerical implementation.

## Separate mathematics checks

### Markdown / GitHub compatibility

Canonical mathematics remains editable `$...$` and separate-line `$$` blocks. Code fences and inline code are protected, display boundaries have blank lines, table mathematics uses TeX delimiters that cannot split Markdown columns, and malformed or missing-backslash TeX was repaired without changing intended formulas.

The maintained `scripts/validate-technical-reference-math.mjs` independently extracted all 1,940 occurrences and rendered all 1,256 unique expressions with **MathJax 3.2.2**. Results: zero Markdown failures, MathJax errors, missing equation mappings, broken images/fragments, nonportable local links, duplicate IDs, unrendered heading markup, or malformed table rows. The engine bundle was `tex-svg-full.js` from `https://cdn.jsdelivr.net/npm/mathjax@3.2.2/es5/tex-svg-full.js`, loaded locally. This is a compatibility check against the syntax documented in [GitHub's mathematics guidance](https://docs.github.com/en/get-started/writing-on-github/working-with-advanced-formatting/writing-mathematical-expressions), not a claim to have inspected GitHub's published renderer. No push was performed.

### Offline PDF equations

The separate publication path uses LaTeX **OT1** encoding and dvisvgm `--no-fonts --exact-bbox --page=1-`. Report-specific temporary directories prevent cross-report equation reuse. The builder clears only its generated `eq-*.svg` files, canonicalizes the resulting names, and runs the complete asset guard before Chromium printing; any missing or invalid asset is fatal.

`scripts/validate-equation-assets.py` passed for the exact 1,256-asset set against the generated TeX preview blocks. It checked vector paths, local glyph references and absence of raster or font-dependent text. This directly guards against the earlier silent T1/EC loss of equation-number glyphs. All source equations were accounted for in the rendered document. Page inspection verified subscripts, superscripts, matrices, limits, equation numbers and black mathematical glyphs on light table headers. Plain-text extraction alone cannot establish equation correctness because the final equations are vector outlines.

## Page-level visual review and final PDF audit

Every page was rendered with Poppler at **120 dpi** and inspected as a readable individual page, with renewed inspection after changes. The review sequence is retained locally under `tmp/pdfs/technical-reference/`:

1. The 251-page candidate (`90abc52ad88e18c62bd82a7ae89f8f39c2a6e15567c3b697524a23951bb2aac5`) was inspected page by page. Corrections addressed orphaned reference tails, equation context, long/short code-block breaks, and dense table widths.
2. The 250-page candidate (`a88fc30187f5b2d4bdfd0cde4397a1dade8b989ec6e56a353678add43800bc49`) retained 189 exact body/header image matches; all 61 differing pages were inspected individually.
3. The final 249-page artifact removed the remaining isolated reference tail through two concise prose edits. All 169 differing pages were inspected individually. The other 80 body/header regions are pixel-for-pixel identical to previously inspected pages. Comparison used the full 1,020-pixel width and rows 0-1,239 of each 1,320-pixel image, excluding only the footer. Printed page numbers were checked on every final non-cover page; the final page total and footer appearance were also inspected visually throughout the changed pages.

Thus every final page body has page-level visual coverage, including unchanged pages carried forward only after exact pixel comparison. No contact sheet was used as a substitute for readable inspection. Chapter-ending white space is intentional; there are no unexplained empty pages, clipped equations, collisions, or unresolved illegible tables.

Additional **180-dpi, original-resolution** inspection passed on final pages **2, 3, 20, 25, 27, 42, 43, 109, 134, 137, 138, 143, 171, 172, 184, 186, 206, 208, 211, 219, 224, 228, and 231**. These cover both contents pages, prior and link equations, GMM covariance, B17C derivations, copula formulas, rating uncertainty, spatial kernels/prediction, API tables and the parameterization crosswalk.

The immutable final PDF audit passed:

- 66 chapter destinations in manifest order and 68 bookmarks; two correctly numbered contents pages. Chapter locations come from named destinations, not potentially repeated title text.
- 398 internal and 109 external links. All internal destinations resolve; 55 distinct commit-pinned repository file/directory targets exist with the correct Git object type at the source checkpoint. No machine-specific or external `file:` links remain. These checks establish link structure and source existence, not live availability of every external literature site.
- Tagged PDF structure, meaningful body text and source metadata retained. Body text measures approximately 10.24 pt in the final PDF, ordinary tables/code 8.70 pt, inline code 9.01 pt and references 9.00 pt. The 7.65 pt text is limited to compact table code/control labels; those pages received enlarged inspection.
- No horizontal text overflow, missing printed page numbers, or low-content pages flagged by the structural audit.

The final hash belongs to the artifact reviewed above. A rebuild may produce a different hash and requires renewed QA; do not overwrite this reviewed file merely to refresh a date.

## Validation and preservation

| Gate | Result |
|---|---|
| Core fast suite | 3,434 passed; zero failures/skips |
| UI fast suite | 645 passed; zero failures/skips |
| App fast suite | 444 passed; zero failures/skips |
| API fast suite | 515 passed; zero failures/skips |
| Total mandatory fast gate | **5,038 passed** |
| Technical-reference documentation contracts | **10 passed**; metadata, manifest, links, exact compiled snippets, API/source traceability and references |
| Strict Debug XML/build gate | **949 source files**, zero compiler/XML warnings or errors |
| Verification catalog | **328 methods / execution units**, zero open gaps with `-RequireComplete` |
| Complete verification index | **328 active methods / 56 retired records**, projection current |
| Consolidated bibliography | **84 sources**, regeneration and `--check` passed |
| PDF navigation regression tests | **3 passed**; multi-page contents, wrapped-link deduplication, repeated titles, missing anchors and slash-prefixed names |
| Mathematics / final PDF audits | Passed separately as detailed above |
| Final diff | Explicit path review and `git diff --check` passed |

All .NET validation used **local-project Numerics 2.2.0**, not package-only release mode. The strict XML gate compiled Verification but **no numerical Verification method was executed**. No new source/API mismatch required an evidence rerun. Production C#, compiled example regions, reference data, tolerances, acceptance rules, active/retired catalogs and scientific algorithms are unchanged.

The verification report deliverable remains byte-identical at SHA-256 **`8c842772cafdcd60952a836dc2a8da2c65c4a03d597571a9383e313edc8e354c`**. A separate temporary finalization regression using the changed shared helper retained its 95 pages, 20 bookmarks, identical per-page text and named destinations, tagging, and identical catalog attachment. Its contents page was compared visually with the reviewed original. The report's Markdown and checkpoint were not revised. The unrelated Numerics `.github/workflows/Release.yml` modification was preserved.

## Explicit scientific qualifications

1. Independent marginal prior-predictive sampling does not sample an additional soft joint-prior coupling; the accepted limitation remains stated.
2. One-step GMM influence supports the verified outlier ranking, not calibrated exact-deletion magnitudes.
3. The retained B17C experiment produced 1,000 accepted refits: 947 outer-converged and 53 capped. Inner optimizer fallback, outer convergence, usable output, and interval coverage remain distinct.
4. The Kamp/Viglione systematic-only Q1000 lower endpoint retains **163 m3/s**, while Skahill et al. (2016), Table 2, gives **183 m3/s**. The v1 report's Table 83 distinguishes those BestFit and external-source columns. **Haden's numerical disposition remains open.** Both drafts retain the qualification; no fixture was changed and full published-endpoint parity is not claimed.
5. The maximum-competing-risk Bayesian example's successful bounded-prior configuration disables component Jeffreys scale terms. It does not establish reliability for arbitrary component priors or difficult correlated cases.
6. SES links with custom curvature smaller than the magnitude of the effective asymmetry can be nonmonotone; the fixed asymmetry clamp does not scale with curvature. The documented default is within the stated monotonicity condition.
7. Mathematical copula limits are distinguished from supported API evaluations; the negative-Clayton support truncation, singular endpoint and absent exact-zero branch are explicit. Bivariate copula fitting holds the marginal models fixed; coincident-frequency propagation has a different product-posterior contract.
8. A penalized GMM covariance is interpreted as posterior curvature only under the stated Gaussian information model. Fixed regularization and external uncertain information have different covariance interpretations.
9. No claim is added for broad B17C interval coverage, untested rating-curve extrapolation, arbitrary spatial networks, unimplemented pairwise likelihood, or universal sampler convergence.

These qualifications do not prevent delivery of the completed draft. They remain visible boundaries for scientific review, not authorization for numerical remediation.

## Reproduction commands and local evidence

The maintained scripts accept explicit runtime paths; no project or system configuration was changed. This run used the bundled Python 3.12.14, Node 24.19.0 and Poppler 26.07.0, installed MiKTeX/LaTeX and dvisvgm, and installed Chrome. The PowerShell builder resolves or accepts the Node, module, Python, LaTeX, dvisvgm and browser paths. The separate MathJax check accepts a complete local 3.2.2 bundle.

```powershell
# Set these to the installed runtime locations on the reviewing machine.
$reportDependencies = 'C:\Users\haden\.cache\codex-runtimes\codex-primary-runtime\dependencies'
$reportPython = Join-Path $reportDependencies 'python\python.exe'
$reportNode = Join-Path $reportDependencies 'node\bin\node.exe'
$env:BESTFIT_NODE_MODULES = Join-Path $reportDependencies 'node\node_modules'

& $reportPython scripts/generate-technical-reference-bibliography.py --check
& $reportPython scripts/test_pdf_report_common.py
dotnet test src/RMC.BestFit.Tests -c Debug -- --filter 'FullyQualifiedName~RMC.BestFit.Tests.Documentation.TechnicalReferenceDocumentationTests'
.\scripts\validate-code-xml-docs.ps1 -Configuration Debug
dotnet test src/RMC.BestFit.Tests -c Debug --no-build
dotnet test src/RMC.BestFit.UI.Tests -c Debug --no-build
dotnet test src/RMC.BestFit.App.Tests -c Debug --no-build
dotnet test src/RMC.BestFit.Api.Tests -c Debug --no-build
.\scripts\validate-verification-catalog.ps1 -Catalog docs/verification/verification-catalog.json -SourceRoot src/RMC.BestFit.Verification -RequireComplete
& $reportPython scripts/build-verification-coverage.py --check

# This replaces the PDF: use only when preparing a new, separately reviewed build.
.\scripts\build-technical-reference-book.ps1
& $reportNode scripts/validate-technical-reference-math.mjs tmp/pdfs/technical-reference/rmc-bestfit-technical-reference.html tmp/pdfs/technical-reference/mathjax-tex-svg-full.js tmp/pdfs/technical-reference/final-math-qa.json
& $reportPython scripts/audit-report-pdf.py output/pdf/rmc-bestfit-technical-reference.pdf --report technical_reference --output tmp/pdfs/technical-reference/final-pdf-qa.json
Get-FileHash output/pdf/rmc-bestfit-technical-reference.pdf -Algorithm SHA256
git diff --check
```

Ignored local evidence includes `final-build.log`, `final-math-qa.json`, `final-pdf-qa.json`, `final-*-tests.log`, `xml-gate.log`, `catalog-gate.log`, `coverage-gate.log`, `verification-regression-qa.json`, `250-page-comparison.json`, `final-page-comparison.json`, and the `review-final-251`, `review-final-250`, `review-final-249`, and `review-dense-249` page images. These scratch files are not prerequisites for a clean-checkout build; this record preserves the final counts, decisions and hashes.

## Session 3 boundary

Session 2 is complete. Continue only with the separately scoped [Session 3 release work](../verification/v2.0.1-closeout-plan.md#session-3---v201-release-candidate-and-publication-handoff): reconcile subsequent source changes, set the approved version, validate `UseLocalRmcNumerics=false`, inspect resolved/packed dependencies, stage release assets, and review any newly generated PDF pages. No merge, push, tag, package publication, or external scientific sign-off is included in this completion.
