# Blakely Mountain Dam: Bayesian study reference awaiting its saved project

The intended Bayesian `.bestfit` project is not present in this checkout. This page records the study scope and reading path; it cannot yet provide a reproducible saved-result walkthrough or Python figures for that project. The project author has been asked for the existing file. No replacement fit, assumed sampler settings or numerical results have been created.

## Understand the intended study

The supplied [Hydrologic Hazard Report, Appendix E2](../../2-bulletin-17C-analysis/2-information-expansion/Case%20Study/IES%20Appendix%20E2%20Hydrologic%20Hazard%20Report.pdf), section 4.3, describes Bayesian information expansion for annual maximum three-day inflows. Systematic, historical/paleoflood, regional-skew and rainfall-runoff information play distinct roles. Flow duration, units and the evidence behind each information term must match before comparing curves.

The available [one-day GMM tutorial](../../2-bulletin-17C-analysis/2-information-expansion/blakely-mountain-dam-b17c.md) covers different saved projects and a different estimator. Its figures are not substitutes for the missing three-day Bayesian results. The imperial and metric GMM projects remain available with their original results.

## A useful reading sequence

1. Work through [Kamp at Zwettl: historical evidence and quantile priors](viglione-et-al-2013.md) to learn the information-expansion workflow using a complete saved Bayesian project.
2. Read section 4.3 of the Blakely report. Identify the duration and units of the target inflow before reading a frequency ordinate.
3. Keep dated flood intervals separate from perception windows, regional parameter information and quantile priors. Record the source and dependence of each contribution.
4. Use the available one-day GMM tutorial to inspect the Blakely chronology, while retaining the duration and estimator distinctions.

## What is required to finish this example

The existing Bayesian project must establish the actual input elements, fitted alternatives, prior distributions, saved sampler settings, diagnostics and results. The author should identify the duration-specific basis for the adopted regional and rainfall-runoff information. Once supplied, figures can be rendered from those saved results without replacing them or rerunning the analyses.

Until then, this is a study-reference page, not a completed numerical example. The missing project and required source context are listed in the [author follow-up log](../../../../docs/example-issues-for-haden.md).
