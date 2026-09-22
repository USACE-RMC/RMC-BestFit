<!-- technical-reference-status: complete -->

# Report Documentation

## Document control

| Field | Value |
|---|---|
| Title | RMC.BestFit 2.0 Technical Reference Manual |
| Report number | Pending assignment |
| Version | External peer-review draft |
| Date | 22 September 2026 |
| Prepared for | U.S. Army Corps of Engineers, Risk Management Center |
| Software checkpoint | RMC.BestFit 2.0.0, commit `3fa55a0f75bbd583e2fb7fce42180faa376f007c` |
| Numerical source checkpoint | RMC.Numerics commit `7e8e8d1c5f26e045a35ec9fc09367de95ed05b02` |
| Declared Numerics dependency | RMC.Numerics 2.2.0 |
| Review status | Draft for external peer review - not for public release |

## Authors

- C. Haden Smith, U.S. Army Corps of Engineers, Risk Management Center, Lakewood, Colorado, USA.
- Woodrow L. Fields, U.S. Army Corps of Engineers, Risk Management Center, Lakewood, Colorado, USA.
- Brian Skahill, Fariborz Maseeh Department of Mathematics and Statistics, Portland State University, Portland, Oregon, USA.

## Abstract

RMC.BestFit is a Bayesian-first statistical analysis framework for flood-frequency and related water-resources applications. This manual defines the models, likelihoods, priors, estimation methods, uncertainty calculations, diagnostic measures, parameterizations, numerical conventions, and public scientific interfaces implemented by version 2.0. It follows the application order: time-series data; input data and mixed observations; distribution fitting; univariate and Bulletin 17C analyses; point-process, competing-risk, mixture, and composite models; bivariate copulas and coincident frequency; rating curves; AR, MA, ARIMA, and ARIMAX models; and spatial extremes. Each chapter distinguishes mathematical theory from implemented conventions, states assumptions and limitations, and points to independent verification evidence. The implementation and dependency checkpoints above define the software configuration reviewed by this draft.

## Review and release notice

This document is a technical draft supplied for independent scientific and engineering review. A report number, named reviewers, approval signatures, and final distribution statement will be assigned after comment disposition. The draft does not authorize changes to established algorithms, priors, samplers, seeds, tolerances, likelihoods, or reference-result contracts.

## Revision record

This edition describes the source checkpoints above. It is a finished draft for external review; scientific approval and public release remain separate decisions. Local validation used the sibling Numerics project at the stated commit. The source checkpoint identifies the reviewed implementation, while the documentation commit and final PDF checksum are recorded in the publication-quality receipt.
