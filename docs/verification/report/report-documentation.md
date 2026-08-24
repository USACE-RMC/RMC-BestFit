<!-- verification-status: publication-draft -->

# Report Documentation

## Document control

| Field | Value |
|---|---|
| Title | RMC.BestFit 2.0 Verification Report |
| Report number | Pending assignment |
| Version | External peer-review draft |
| Date | 28 August 2026 |
| Prepared for | U.S. Army Corps of Engineers, Risk Management Center |
| Software checkpoint | RMC.BestFit 2.0.0, commit `4304fb39f8e162cdb746083108042df88e153afc` |
| Numerical source checkpoint | RMC.Numerics commit `90a63a46394db9ef95e72b0fcbba943408110636` |
| Published-package compatibility baseline | RMC.Numerics 2.1.4 |
| Review status | Draft for external peer review - not for public release |

## Authors

- C. Haden Smith, U.S. Army Corps of Engineers, Risk Management Center, Lakewood, Colorado, USA.
- Woodrow L. Fields, U.S. Army Corps of Engineers, Risk Management Center, Lakewood, Colorado, USA.
- Brian Skahill, Fariborz Maseeh Department of Mathematics and Statistics, Portland State University, Portland, Oregon, USA.

## Abstract

This report presents numerical verification evidence for RMC.BestFit 2.0, a Bayesian-first statistical analysis framework used in flood-frequency and water-resources engineering. Verification is based on analytical calculations, independently implemented numerical oracles, external scientific packages, published examples, parameter-recovery experiments, and simulation-coverage experiments. The report states the software configuration, test data, oracle provenance, acceptance criteria, observed results, and supported conclusion for each claim. It follows the application order: time-series data; input data; distribution fitting; univariate and Bulletin 17C analyses; point-process, competing-risk, mixture, and composite analyses; bivariate and coincident-frequency analyses; rating curves; AR, MA, ARIMA, and ARIMAX analyses; and spatial extremes. Deterministic unit and regression tests are reported as software-quality controls but are not substituted for scientific verification. Evidence boundaries are stated explicitly where the current checkpoint does not support a broader claim.

## Review and release notice

This document is a technical draft supplied for independent scientific and engineering review. A report number, named reviewers, approval signatures, and final distribution statement will be assigned after comment disposition. Numerical conclusions apply only to the software and dependency checkpoints identified above and to the parameter regions, data designs, and acceptance rules stated in this report.

## Revision record

| Revision | Date | Description |
|---|---:|---|
| External-review draft | 28 August 2026 | Reorganized the verification record around test design, oracle provenance, numerical results, and conclusions; refreshed the documented software checkpoint and publication controls. |

## Suggested citation

Smith, C. H., Fields, W. L., and Skahill, B. (2026). *RMC.BestFit 2.0 Verification Report*. External peer-review draft. U.S. Army Corps of Engineers, Risk Management Center.
