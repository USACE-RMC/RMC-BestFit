<!-- verification-status: publication-draft -->

# RMC.BestFit 2.0 Verification Report

The report describes the current numerical test library and the scientific evidence supporting each analysis family. Start with the [executive summary](report/executive-summary.md), then read the [methodology](report/system-and-methodology.md) and the relevant analysis chapter. The [complete test index](report/test-coverage.md) accounts for every executable verification method.

The source inventory contains **329 methods in 71 active classes**. The catalog records 327 verified dispositions and two accepted limitations: generic sampling of coupled priors and the magnitude of one-step GMM deletion influence. These are numerical evidence dispositions, distinct from code coverage and repeated-sample interval coverage. The report explains the limits of each claim.

| Report area | Chapter |
|---|---|
| Document control and scope | [Report documentation](report/report-documentation.md) |
| Estimation, priors, model comparison, and diagnostics | [Estimation and diagnostics](report/estimation-diagnostics.md) |
| Distribution fitting, univariate recovery, Flike, Viglione, and Bulletin 17C | [Distributions and Bulletin 17C](report/data-distributions-b17c.md) |
| Peaks over threshold | [Point process](report/point-process-analysis.md) |
| Competing risks | [Competing-risk analysis](report/competing-risk-analysis.md) |
| Mixtures and composite distributions | [Mixtures](report/mixture-analysis.md), [composites](report/composite-analysis.md) |
| Bivariate and coincident frequency | [Bivariate analyses](report/bivariate-analyses.md) |
| Rating curves | [Rating-curve analysis](report/rating-curve.md) |
| AR, MA, ARIMA, and ARIMAX | [Time-series analysis](report/time-series-analyses.md) |
| Spatial likelihood, prediction, and uncertainty | [Spatial extremes](report/spatial-extremes.md) |
| Conclusions and limitations | [Evidence boundaries](report/evidence-boundaries-conclusions.md) |
| Full library coverage | [Appendix A](report/test-coverage.md), [machine-readable catalog](verification-catalog.json) |

## Build and review

Markdown is the publication source. [book-order.txt](book-order.txt) defines the PDF chapters. Report-specific source-review metadata lives in the `verification_report.checkpoint` object in [report-metadata.json](../report-metadata.json). Numerical artifact dates and run configurations remain attached to their evidence; the report date is not a library-wide rerun date.

```powershell
.\scripts\validate-verification-catalog.ps1 -Catalog docs/verification/verification-catalog.json -SourceRoot src/RMC.BestFit.Verification -RequireComplete
python scripts/build-verification-coverage.py --check
.\scripts\build-verification-report.ps1
```

The build produces `output/pdf/rmc-bestfit-verification-report.pdf`, using offline vector equations. Use `$...$` for inline mathematics and `$$` on separate lines for display mathematics; these delimiters also render on GitHub. After changing the catalog, regenerate Appendix A with `python scripts/build-verification-coverage.py` and review the diff. The PDF build checks that this generated index is current.

The [published snapshot record](#published-verification-report-snapshot) identifies the retained release PDF and its original validation checkpoint. Current navigation and support documentation can advance independently of that immutable publication.

## Test execution

Core, UI, App, and API fast suites are the regression gate. Scoped numerical development checks may run one relevant family, class, or namespace at a time, with the Microsoft.Testing.Platform filter after `--`. Evidence-grade verification uses the guarded runner:

```powershell
.\scripts\run-verification-test.ps1 -Test Namespace.Class.Method
```

The runner requires exactly one result. A whole-library numerical run is a deliberate, user-coordinated action. Do not infer numerical accuracy from a build, estimator completion, finite output, or agreement between production paths.

## Evidence records

The [methodology and scientific claim inventory](methodology.md#scientific-claim-inventory) connect report scope to the catalog. Supporting family pages retain experiment designs, exact runs, independent artifacts, corrections, and limitations. [Retired interval-coverage studies](bulletin-17c.md#retired-interval-coverage-studies) remain separate from current claims; their 56 records are retained in [verification-catalog-retired.json](verification-catalog-retired.json).


## Supporting evidence

| Scientific area | Detailed record |
|---|---|
| Distribution fitting and analytical corrections | [Distribution fitting](distribution-fitting.md) |
| Estimation, diagnostics, stationary and trend recovery | [Model estimation](model-estimation.md) |
| Bulletin 17C covariance, worked examples and bootstrap repairs | [Bulletin 17C](bulletin-17c.md) |
| Point processes, competing risks, mixtures and composites | [Point process](point-process.md), [competing risks](competing-risks.md), [mixtures](mixture.md), [composites](composite.md) |
| Bivariate dependence and coincident response | [Bivariate verification](bivariate.md) |
| Rating curves, time series and spatial models | [Rating curves](rating-curve.md), [time series](time-series.md), [spatial extremes](spatial-extremes.md) |
| Independent reference generators and frozen artifacts | [Verification data manifest](../../verification/data/MANIFEST.md) |

## Published verification-report snapshot

The retained [verification PDF](../../output/pdf/rmc-bestfit-verification-report.pdf) is the 27 September 2026 v2.0.1 draft: **95 pages, 18 chapters, 20 bookmarks**, SHA-256 `905b57a16fcaed8404472f50eee3f3ba1fda7e9cc409763c2ff0386afe764e15`. Its BestFit source-review baseline is `9427bd27c1ed7d757fde5a37ce24b90a06f3c9ed`; the Numerics baseline is `7e8e8d1c5f26e045a35ec9fc09367de95ed05b02`, matching the declared 2.2.0 package. The report metadata and PDF bytes remain fixed to that checkpoint.

Recorded validation for that edition reconciled 329 methods in 71 classes, with 327 verified dispositions and two accepted limitations. The four fast suites passed in Debug and package-mode Release: Core 3,519; UI 701; App 450; API 546; total 5,216. Ten documentation contract tests passed. These are dated results, not a claim that those suites or all numerical methods were rerun during later documentation maintenance.

All 95 pages were reviewed through Poppler renders; changed pages and their neighbors were inspected individually. All 216 unique equations passed offline LaTeX/SVG glyph checks and MathJax 3.2.2 rendering. The artifact contains 45 internal and 441 external links, including 81 source-file links checked against the pinned commit. Its embedded catalog and metadata matched the repository **at publication**; navigation-only catalog updates after that date do not change those embedded bytes. Report dates identify source review, while each numerical artifact retains its own execution configuration and date.

The PDF remains a draft with its existing review and evidence limitations. It does not certify broad confidence-interval coverage, generic coupled-prior sampling, calibrated GMM deletion magnitudes, or universal convergence. Any new PDF edition requires deliberate rebuilding and page review; repository cleanup does not regenerate this snapshot.
