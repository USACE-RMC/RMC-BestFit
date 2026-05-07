# point-process-examples

## Overview

Peaks-over-threshold (POT) modeling using non-homogeneous Poisson point processes for daily precipitation at two GHCN stations (USC00040741 Big Bear Lake CA, USC00042402). Compares stationary and seasonal point-process fits against GEV block-maximum analyses on the same record.

## What's Inside

### Time Series Data

| Element | Description |
|---|---|
| `USC00040741` | Daily precipitation for GHCN station USC00040741 (Big Bear Lake, CA). |
| `USC00042402` | Daily precipitation for GHCN station USC00042402. |

### Input Data

| Element | Description |
|---|---|
| `USC00040741 - POT` | Peaks-over-threshold precipitation series extracted from USC00040741. |
| `USC00040741 - AMS` | Annual maximum precipitation series extracted from USC00040741. |
| `USC00042402 - POT` | Peaks-over-threshold precipitation series extracted from USC00042402. |
| `USC00042402 - AMS` | Annual maximum precipitation series extracted from USC00042402. |

### Univariate Distribution

| Element | Description |
|---|---|
| `USC00040741 - GEV` | Bayesian GEV fit on the AMS series at USC00040741 — comparison baseline for the point-process fits. |
| `USC00042402 - GEV` | Bayesian GEV fit on the AMS series at USC00042402 — comparison baseline for the point-process fits. |

### Point Process

| Element | Description |
|---|---|
| `USC00040741 - Point Process` | Stationary non-homogeneous Poisson point-process fit on the POT series at USC00040741. |
| `USC00042402 - Point Process` | Stationary non-homogeneous Poisson point-process fit on the POT series at USC00042402. |
| `USC00040741 - Seasonal Point Process` | Seasonal (sinusoidal-rate) Poisson point-process fit on the POT series at USC00040741. |

## Step-by-Step Walkthrough

### Opening the Project

1. Open RMC-BestFit 2.0.
2. Select **File > Open** and navigate to `examples/4-univariate-distribution-analysis/3-point-process-analysis/`.
3. Open `point-process-examples.bestfit`.

### Exploring the Elements

For each Point Process alternative:

1. Click the alternative in the Project Explorer.
2. Open the **Frequency** tab to view the AEP curve derived from the fitted intensity function.
3. Compare against the **GEV block-maximum** alternative on the same record.
4. For seasonal point processes, inspect the rate function over the annual cycle.

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

![Frequency curve (AEP versus quantile) for each alternative, with the credible band.](images/point-process-frequency.png)
*Figure: Frequency curve (AEP versus quantile) for each alternative, with the credible band.*

![Posterior kernel density for each parameter.](images/point-process-kernel-density.png)
*Figure: Posterior kernel density for each parameter.*

![Markov-chain traces for each parameter.](images/point-process-trace.png)
*Figure: Markov-chain traces for each parameter.*

![Autocorrelation function of the chains, used to estimate effective sample size.](images/point-process-autocorrelation.png)
*Figure: Autocorrelation function of the chains, used to estimate effective sample size.*

### MCMC Diagnostics

Verify chain convergence before interpreting any results:

- **R-hat** — should be < 1.01 for every parameter.
- **Effective Sample Size (ESS)** — at least a few hundred per parameter.
- **Trace plots** — should look like fuzzy, well-mixed caterpillars (no drift, no sticking).
- **Posterior** — overall posterior log-likelihood should be visually stationary in the mean-likelihood plot.

## Next Steps

- Compare the **Point Process** AEP curve to the **GEV block-maximum** AEP curve on the same record.
- Add a **seasonal** sinusoidal-rate point process to capture annual-cycle clustering.
- Compute **expected number of exceedances** above engineering thresholds from the fitted intensity.

## References

<!-- Cite published case studies / source datasets here. Example format:

> Viglione, A., Merz, R., Salinas, J. L., & Bloeschl, G. (2013). Flood frequency hooves of a galloping horse. *Water Resources Research*, 49(2), 675-692.
-->

---

_This tutorial was generated from the project's `.bestfit` file metadata. The narrative and figure / table sections are placeholders — capture screenshots from the BestFit GUI and paste output tables to complete the guide._
