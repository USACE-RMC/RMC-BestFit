# nsffa-brays-bayou-texas

## Overview

Nonstationary flood-frequency analysis (NSFFA) for Brays Bayou (USGS gage 08075000), Texas. Compares constant, linear, logistic, and step trend functions on the LP-III parameters and aggregates results via Bayesian model averaging.

## What's Inside

### Input Data

| Element | Description |
|---|---|
| `USGS 08075000 Brays Bayou` | Annual peak discharge for Brays Bayou at Houston, TX (USGS gage 08075000), in cubic feet per second. |
| `USGS 08075000 Brays Bayou - Metric` | Annual peak discharge for Brays Bayou at Houston, TX, converted to metric units (cubic meters per second). |

### Univariate Distribution

| Element | Description |
|---|---|
| `NSFFA - Constant` | Stationary LP-III fit (constant trend on all parameters) — baseline for the non-stationary comparisons. |
| `NSFFA - Linear` | Non-stationary LP-III fit with a linear trend on the location parameter. |
| `NSFFA - Logistic` | Non-stationary LP-III fit with a logistic trend on the location parameter. |
| `NSFFA - Step` | Non-stationary LP-III fit with a step-change trend on the location parameter. |
| `NSFFA - Linear - Logistic` | Non-stationary LP-III fit with a linear trend on location and a logistic trend on scale. |
| `NSFFA - Logistic - Logistic` | Non-stationary LP-III fit with a logistic trend on both location and scale. |
| `NSFFA - Step - Logistic` | Non-stationary LP-III fit with a step-change on location and a logistic trend on scale. |

### Composite Distribution

| Element | Description |
|---|---|
| `Bayesian Model Average` | Bayesian model average over the seven NSFFA alternatives, weighted by DIC / WAIC. |

## Step-by-Step Walkthrough

### Opening the Project

1. Open RMC-BestFit 2.0.
2. Select **File > Open** and navigate to `examples/4-univariate-distribution-analysis/1-univariate-analysis/5-nonstationary-ffa/`.
3. Open `nsffa-brays-bayou-texas.bestfit`.

### Exploring the Elements

For each NSFFA alternative:

1. Click the alternative in the Project Explorer.
2. Open the **Frequency** tab. The displayed curve is conditional on the time index in the **Properties** panel — change it to see how the curve evolves.
3. Open the **Chronology** tab to view the time-varying location / scale through the record.
4. Use the **Bayesian Model Average** Composite Distribution element to view the trend-marginalized AEP curve.

## Analysis Settings

Each Bayesian analysis in this project uses the DEMCzs sampler with project-specific iteration / warm-up settings. Open the **Properties** panel of any alternative to inspect:

- **Sampler type** (DEMCzs, ARWMH, HMC).
- **Iterations / Warm-up Iterations** — total post-warmup samples per chain.
- **Number of Chains** — typically 6 for routine work.
- **Thinning Interval** — keeps every Nth sample to reduce storage / autocorrelation.
- **Point Estimator** — Posterior Mean (default), Posterior Median, or Posterior Mode (MAP).
- **Credible Interval Width** — typically 0.90 or 0.95.

## Expected Results

<!-- Replace placeholder content as you capture screenshots and copy values out of the BestFit GUI. -->

### Parameter Estimates

<!-- TODO: paste the parameter-estimate table from the MCMC report (right-click the alternative > Open MCMC Report) -->

| Alternative | Parameter | Mean | Median | Lower CI | Upper CI | R-hat | ESS |
|---|---|---|---|---|---|---|---|
| _(placeholder)_ |  |  |  |  |  |  |  |

### Frequency / Quantile Table

<!-- TODO: paste the AEP / return-period table from the Frequency tab (right-click the chart > Copy Table). -->

| AEP (%) | Return Period (yr) | Median | Lower CI | Upper CI |
|---|---|---|---|---|
| 50    | 2     |  |  |  |
| 10    | 10    |  |  |  |
| 1     | 100   |  |  |  |
| 0.5   | 200   |  |  |  |
| 0.2   | 500   |  |  |  |

### Plots

![Frequency curve (AEP versus quantile) for each alternative, with the credible band.](images/nsffa-brays-bayou-frequency.png)
*Figure: Frequency curve (AEP versus quantile) for each alternative, with the credible band.*

![Posterior kernel density for each parameter.](images/nsffa-brays-bayou-kernel-density.png)
*Figure: Posterior kernel density for each parameter.*

![Markov-chain traces for each parameter.](images/nsffa-brays-bayou-trace.png)
*Figure: Markov-chain traces for each parameter.*

![Autocorrelation function of the chains, used to estimate effective sample size.](images/nsffa-brays-bayou-autocorrelation.png)
*Figure: Autocorrelation function of the chains, used to estimate effective sample size.*

### MCMC Diagnostics

Verify chain convergence before interpreting any results:

- **R-hat** — should be < 1.01 for every parameter.
- **Effective Sample Size (ESS)** — at least a few hundred per parameter.
- **Trace plots** — should look like fuzzy, well-mixed caterpillars (no drift, no sticking).
- **Posterior** — overall posterior log-likelihood should be visually stationary in the mean-likelihood plot.

## Next Steps

- Compare alternatives by **DIC**, **WAIC**, and **LOO-CV** information criteria.
- Average results across alternatives via the **Bayesian Model Average** Composite Distribution element.
- Project **conditional return-period quantiles** for future time indices using the trend functions.

## References

<!-- Cite published case studies / source datasets here. Example format:

> Viglione, A., Merz, R., Salinas, J. L., & Bloeschl, G. (2013). Flood frequency hooves of a galloping horse. *Water Resources Research*, 49(2), 675-692.
-->

---

_This tutorial was generated from the project's `.bestfit` file metadata. The narrative and figure / table sections are placeholders — capture screenshots from the BestFit GUI and paste output tables to complete the guide._
