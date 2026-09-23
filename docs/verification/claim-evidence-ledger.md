<!-- verification-status: internal-evidence-control -->

# Verification Claim-Evidence Ledger

This control is excluded from the publication manifest. The report describes the current repository; development chronology remains in the test inventory and source history.

## Current claim map

The source-matched catalog has 328 methods in 71 active classes: 326 verified dispositions and two accepted limitations. No numerical method was rerun for the editorial refresh. The existing per-method references and acceptance rules are preserved. Appendix A is generated from these records; the catalog validator checks them against executable C# declarations.

| Claim area | Methods | Primary public chapter |
|---|---:|---|
| Estimation and diagnostics | 56 | [Chapter](report/estimation-diagnostics.md) |
| Distribution fitting | 52 | [Chapter](report/data-distributions-b17c.md) |
| Univariate recovery | 23 | [Chapter](report/data-distributions-b17c.md) |
| Published univariate comparisons | 14 | [Chapter](report/data-distributions-b17c.md) |
| Bulletin 17C | 41 | [Chapter](report/data-distributions-b17c.md) |
| Point process | 13 | [Chapter](report/point-process-analysis.md) |
| Competing risks | 9 | [Chapter](report/competing-risk-analysis.md) |
| Mixtures | 7 | [Chapter](report/mixture-analysis.md) |
| Composite analyses | 14 | [Chapter](report/composite-analysis.md) |
| Bivariate and coincident frequency | 25 | [Chapter](report/bivariate-analyses.md) |
| Rating curves | 19 | [Chapter](report/rating-curve.md) |
| Time-series models | 24 | [Chapter](report/time-series-analyses.md) |
| Spatial extremes | 31 | [Chapter](report/spatial-extremes.md) |

Shared posterior-resampling evidence is counted under estimation and diagnostics and supports both composite and coincident-frequency chapters. Cross-references do not create additional methods.

## Evidence controls

- [Catalog](verification-catalog.json): exact identities, oracle, sample unit, seed, acceptance, disposition, and original evidence location.
- [Complete test index](report/test-coverage.md): every active identity exactly once, grouped into its primary report chapter.
- [Publication quality](publication-quality.md): documentation/build/regression/rendering checks for this edition.
- [Retired catalog](verification-catalog-retired.json): 56 original coverage-study records whose source files were deleted in commit ae4aaa1; no current declaration or interval-coverage claim derives from them.

## Interpretation controls

A catalog disposition does not establish that every test ran on the current source-review baseline. Artifact-specific execution configurations retain that role. Supporting B17C bootstrap diagnostics distinguish accepted output (1000), outer convergence (947), capped fits (53), and inner fallback (77); the delivery count is not a coverage result.

The two accepted limitations remain visible in the executive summary, estimation chapter, complete index, and conclusions. Publication excludes broad Cohn/interval coverage, generic joint-prior sampling, exact GMM deletion magnitude, general correlated Bayesian competing-risk recovery, and untested extrapolation or spatial-scale claims.
