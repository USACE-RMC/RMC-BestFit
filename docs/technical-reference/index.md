<!-- technical-reference-status: complete -->

# RMC.BestFit Technical Reference

[Documentation home](../index.md) | [API traceability](api-traceability.md) | [Documentation contract](documentation-contract.md)

## Scope

This reference documents the scientific formulations, likelihoods, priors, estimation algorithms, uncertainty propagation, numerical behavior, validation evidence, and C# API for RMC.BestFit 2.0 with RMC.Numerics 2.1.4. It is written for statistical reviewers, hydrologic practitioners, and programmatic users.

Implementation behavior is the authority for API and numerical claims. Primary literature and official standards establish theoretical context. Pages that have not yet passed the contract are labeled in progress and must not be treated as final peer-review chapters.

## Foundations

- [Scientific model and analysis contracts](models/overview.md)
- [Parameters, bounds, and priors](models/parameters-and-priors.md)
- [Data frame and mixed-observation likelihood](data-frame/index.md)
- [Trend functions and nonstationary parameters](support/trend-functions.md)
- [Link functions](support/link-functions.md)
- [Analysis architecture and lifecycle](analysis/overview.md)

## Distribution models

- [Distribution family index](distributions/index.md)
- [Univariate distributions](distributions/univariate.md)
- [Peaks over threshold and point process](distributions/point-process.md)
- [Mixture distributions](distributions/mixture.md)
- [Competing risks](distributions/competing-risks.md)
- [Composite distributions and model averaging](distributions/composite.md)

All fifteen univariate family chapters have completed their source, parameterization, likelihood, API-example, and evidence audit. The Kappa Four zero-shape Numerics defect in TR-001 is fixed and analytically verified.

## Estimation and diagnostics

- [Estimation index](estimation/index.md)
- [Maximum likelihood](estimation/maximum-likelihood.md)
- [Maximum a posteriori](estimation/maximum-a-posteriori.md)
- [Generalized method of moments](estimation/generalized-method-of-moments.md)
- [Bayesian MCMC](estimation/bayesian-mcmc.md)
- [Model comparison](estimation/model-comparison.md)
- [Estimation and convergence diagnostics](estimation/diagnostics.md)
- [Influence diagnostics](estimation/influence-diagnostics.md)
- [Predictive checks](estimation/predictive-checks.md)

The Phase 5 estimator and diagnostic chapters are source-audited. They distinguish implemented conventions from standard theory and link all production discrepancies to the review-findings register. Phase 5 execution remains gated by the Phase 4 competing-risk/composite recovery supplement.

## Analysis workflows

- [Distribution fitting](analysis/distribution-fitting.md)
- [Univariate frequency analysis](analysis/univariate.md)
- [Bulletin 17C overview](analysis/bulletin-17c.md)
  - [Expected moments and penalized GMM](analysis/bulletin-17c-estimation.md)
  - [Uncertainty, calibration, and diagnostics](analysis/bulletin-17c-uncertainty.md)
- [Composite analysis](analysis/composite.md)
- [Bivariate analysis](analysis/bivariate.md)
- [Coincident-frequency analysis](analysis/coincident-frequency.md)
- [Rating-curve analysis](analysis/rating-curve.md)
- [Time-series analysis](analysis/time-series.md)
  - [Autoregressive models](analysis/autoregressive.md)
  - [Moving-average models](analysis/moving-average.md)
  - [ARIMA models](analysis/arima.md)
  - [ARIMAX models](analysis/arimax.md)
- [Spatial extremes](spatial/spatial-extremes.md)

Time-series and rating-curve chapters have completed their Phase 6 source audits. Bivariate, coincident-frequency, and spatial chapters have completed their Phase 7 source, likelihood, API, and evidence audits. Affected production paths are explicitly qualified by TR-036 through TR-062 in the review-findings register.

## Reviewer appendices

- [Global notation and conventions](appendices/notation.md)
- [Parameterization crosswalk](appendices/parameterization-crosswalk.md)
- [Glossary](appendices/glossary.md)
- [Consolidated bibliography](appendices/bibliography.md)
- [Implementation-source index](appendices/implementation-source-index.md)
- [Verification-evidence index](appendices/verification-evidence.md)
- [Scientific reviewer checklist](appendices/reviewer-checklist.md)
- [Scientific API traceability matrix](api-traceability.md)
- [Chapter quality contract](documentation-contract.md)

## Peer-review release artifacts

The [canonical book manifest](book-order.txt) defines the order of the Markdown reference. Build the reproducible book-style PDF from the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\build-technical-reference-book.ps1
```

The build first checks that the consolidated bibliography is current, then produces `output/pdf/rmc-bestfit-technical-reference.pdf`. Intermediate HTML, the browser-produced PDF, and rendered QA images belong under `tmp/pdfs/`.

## Completion states

| Marker | Meaning |
|---|---|
| `technical-reference-status: complete` | Source-audited, link/citation checked, and all marked C# snippets compile from exact test-fixture regions |
| `technical-reference-status: in-progress` | Useful working material with unresolved audit rows; not a final peer-review chapter |
| No marker | Legacy page awaiting contract-based rewrite |

The fast `TechnicalReferenceDocumentationTests` enforce local-link integrity, deleted-namespace rejection, exact compiled snippets, citation anchors on completed pages, and coverage of exported scientific API types in the traceability matrix.

[Documentation home](../index.md) | [API traceability](api-traceability.md) | [Documentation contract](documentation-contract.md)
