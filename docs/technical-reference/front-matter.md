<!-- technical-reference-status: complete -->

# Report Documentation

## Document control

| Field | Value |
|---|---|
| Title | RMC.BestFit 2.0 Technical Reference Manual |
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

RMC.BestFit is a Bayesian-first statistical analysis framework for flood-frequency and related water-resources applications. This manual defines the models, likelihoods, priors, estimation methods, uncertainty calculations, diagnostic measures, parameterizations, numerical conventions, and public scientific interfaces implemented by version 2.0. It follows the application order: time-series data; input data and mixed observations; distribution fitting; univariate and Bulletin 17C analyses; point-process, competing-risk, mixture, and composite models; bivariate copulas and coincident frequency; rating curves; AR, MA, ARIMA, and ARIMAX models; and spatial extremes. Each chapter distinguishes mathematical theory from implemented conventions, states assumptions and limitations, and points to independent verification evidence. The implementation and dependency checkpoints above define the software configuration reviewed by this draft.

## Review and release notice

This document is a technical draft supplied for independent scientific and engineering review. A report number, named reviewers, approval signatures, and final distribution statement will be assigned after comment disposition. The draft does not authorize changes to established algorithms, priors, samplers, seeds, tolerances, likelihoods, or reference-result contracts.

## Revision record

| Revision | Date | Description |
|---|---:|---|
| External-review draft | 28 August 2026 | Reconciled current behavior and verification evidence; refreshed publication metadata and PDF controls. |
