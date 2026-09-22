<!-- verification-status: publication-draft -->

# RMC.BestFit 2.0 Verification Report

The report describes the current numerical test library and the scientific evidence supporting each analysis family. Start with the [executive summary](report/executive-summary.md), then read the [methodology](report/system-and-methodology.md) and the relevant analysis chapter. The [complete test index](report/test-coverage.md) accounts for every executable verification method.

The source inventory contains **328 methods in 71 active classes**. The catalog records 326 verified dispositions and two accepted limitations: generic sampling of coupled priors and the magnitude of one-step GMM deletion influence. These are numerical evidence dispositions, distinct from code coverage and repeated-sample interval coverage. The report explains the limits of each claim.

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

The [publication quality record](publication-quality.md) records source alignment, XML and regression gates, equation rendering, and PDF layout checks for this edition.

## Test execution

Core, UI, App, and API fast suites are the regression gate. Scoped numerical development checks may run one relevant family, class, or namespace at a time, with the Microsoft.Testing.Platform filter after `--`. Evidence-grade verification uses the guarded runner:

```powershell
.\scripts\run-verification-test.ps1 -Test Namespace.Class.Method
```

The runner requires exactly one result. A whole-library numerical run is a deliberate, user-coordinated action. Do not infer numerical accuracy from a build, estimator completion, finite output, or agreement between production paths.

## Evidence records

The [claim-evidence ledger](claim-evidence-ledger.md) connects current report scope to the catalog. The [completeness audit](verification-completeness-audit.md), [test inventory](test-inventory.md), and [finalization plan](verification-finalization-plan.md) preserve the work records. Catalog entries for source files deleted on 9 September 2026 are retained separately in [verification-catalog-retired.json](verification-catalog-retired.json); they are excluded from the current library and report counts.
