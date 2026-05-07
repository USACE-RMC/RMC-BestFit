# nsffa-oc-fisher-dam

## Overview

Nonstationary flood-frequency analysis (NSFFA) for inflows to OC Fisher Dam, Texas. Compares constant, linear, step, and sinusoidal trend functions on the LP-III parameters and combines them via Bayesian model averaging.

## What's Inside

### Input Data

| Element | Description |
|---|---|
| `OC Fisher Inflows` | Annual peak inflow series to OC Fisher Dam, Texas. |

### Univariate Distribution

| Element | Description |
|---|---|
| `SFFA` | Stationary frequency analysis (LP-III) — baseline for comparison with the non-stationary alternatives. |
| `NSFFA - Constant` | Non-stationary fit with a constant trend (functionally equivalent to SFFA, kept for explicit comparison). |
| `NSFFA - Linear Trend` | Non-stationary fit with a linear trend on the location parameter. |
| `NSFFA - Step Function` | Non-stationary fit with a step-change on the location parameter. |
| `NSFFA - Sinusoidal Trend` | Non-stationary fit with a sinusoidal (annual cycle) trend on the location parameter. |

### Composite Distribution

| Element | Description |
|---|---|
| `Bayesian Model Average` | Bayesian model average over the five NSFFA alternatives, weighted by DIC / WAIC. |

## Step-by-Step Walkthrough

### Opening the Project

1. Open RMC-BestFit 2.0.
2. Select **File > Open** and navigate to `examples/4-univariate-distribution-analysis/1-univariate-analysis/5-nonstationary-ffa/`.
3. Open `nsffa-oc-fisher-dam.bestfit`.

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

![Frequency curve (AEP versus quantile) for each alternative, with the credible band.](images/nsffa-oc-fisher-frequency.png)
*Figure: Frequency curve (AEP versus quantile) for each alternative, with the credible band.*

![Posterior kernel density for each parameter.](images/nsffa-oc-fisher-kernel-density.png)
*Figure: Posterior kernel density for each parameter.*

![Markov-chain traces for each parameter.](images/nsffa-oc-fisher-trace.png)
*Figure: Markov-chain traces for each parameter.*

![Autocorrelation function of the chains, used to estimate effective sample size.](images/nsffa-oc-fisher-autocorrelation.png)
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
