# nsffa-synthetic-data

## Overview

Synthetic NSFFA datasets covering nine trend types (constant, cubic, exponential, linear, logistic, power, quadratic, sinusoidal, step). Each dataset has a paired NSFFA fit using the matching trend function for verifying nonstationary parameter recovery.

## What's Inside

### Input Data

| Element | Description |
|---|---|
| `Constant Trend Data` | Synthetic LP-III sample with a constant location parameter (no trend) — control case. |
| `Cubic Trend Data` | Synthetic LP-III sample with a cubic trend on the location parameter. |
| `Exponential Trend Data` | Synthetic LP-III sample with an exponential trend on the location parameter. |
| `Linear Trend Data` | Synthetic LP-III sample with a linear trend on the location parameter. |
| `Logistic Trend Data` | Synthetic LP-III sample with a logistic trend on the location parameter. |
| `Power Trend Data` | Synthetic LP-III sample with a power-law trend on the location parameter. |
| `Quadratic Trend Data` | Synthetic LP-III sample with a quadratic trend on the location parameter. |
| `Sinusoidal Trend Data` | Synthetic LP-III sample with a sinusoidal (annual cycle) trend on the location parameter. |
| `Step Function Data` | Synthetic LP-III sample with a step-change on the location parameter. |

### Univariate Distribution

| Element | Description |
|---|---|
| `NSFFA - Constant Trend` | NSFFA fit with a constant trend on the constant-trend synthetic data — should recover the ground-truth parameters. |
| `NSFFA - Cubic Trend` | NSFFA fit with a cubic trend on the cubic-trend synthetic data. |
| `NSFFA - Exponential Trend` | NSFFA fit with an exponential trend on the exponential-trend synthetic data. |
| `NSFFA - Linear Trend` | NSFFA fit with a linear trend on the linear-trend synthetic data. |
| `NSFFA - Logistic Trend` | NSFFA fit with a logistic trend on the logistic-trend synthetic data. |
| `NSFFA - Power Trend` | NSFFA fit with a power-law trend on the power-trend synthetic data. |
| `NSFFA - Quadratic Trend` | NSFFA fit with a quadratic trend on the quadratic-trend synthetic data. |
| `NSFFA - Sinusoidal Trend` | NSFFA fit with a sinusoidal trend on the sinusoidal-trend synthetic data. |
| `NSFFA - Step Function` | NSFFA fit with a step-change trend on the step-function synthetic data. |

## Step-by-Step Walkthrough

### Opening the Project

1. Open RMC-BestFit 2.0.
2. Select **File > Open** and navigate to `examples/4-univariate-distribution-analysis/1-univariate-analysis/5-nonstationary-ffa/`.
3. Open `nsffa-synthetic-data.bestfit`.

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

![Frequency curve (AEP versus quantile) for each alternative, with the credible band.](images/nsffa-synthetic-frequency.png)
*Figure: Frequency curve (AEP versus quantile) for each alternative, with the credible band.*

![Posterior kernel density for each parameter.](images/nsffa-synthetic-kernel-density.png)
*Figure: Posterior kernel density for each parameter.*

![Markov-chain traces for each parameter.](images/nsffa-synthetic-trace.png)
*Figure: Markov-chain traces for each parameter.*

![Autocorrelation function of the chains, used to estimate effective sample size.](images/nsffa-synthetic-autocorrelation.png)
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
