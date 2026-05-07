# sinnemahoning-move3-b17c

## Overview

B17C-method flood-frequency analysis for Sinnemahoning Creek (USGS gage 01543500) demonstrating record extension via MOVE.3 regression. Companion to sinnemahoning-move3-bayesian.

## What's Inside

### Input Data

| Element | Description |
|---|---|
| `Sinnemahoning - MOVE.3 - No Errors` | The years 1914-1938 were derived from MOVE.3 record extension. Typically, errors from the regression are ignored. |
| `Sinnemahoning - MOVE.3 - With Errors` | The years 1914-1938 were derived from MOVE.3 record extension. Errors from the regression are incorporated using uncertain data. |
| `Sinnemahoning - No Extension` | Sinnemahoning Creek peak-flow record with no MOVE.3 extension applied (systematic record only). |

### Univariate Distribution

| Element | Description |
|---|---|
| `LPIII - No Extension` | Bayesian LP-III fit on the systematic record only (no MOVE.3 extension). |
| `LPIII - No Errors` | Bayesian LP-III fit on the MOVE.3-extended record, ignoring the regression measurement errors. |
| `LPIII - With Errors` | Bayesian LP-III fit on the MOVE.3-extended record with regression measurement errors propagated as uncertain data. |

### Bulletin 17C

| Element | Description |
|---|---|
| `B17C - LPIII - No Extension` | B17C fit on the systematic record only (no MOVE.3 extension). |
| `B17C - LPIII - No Errors` | B17C fit on the MOVE.3-extended record, ignoring the regression measurement errors. |
| `B17C - LPIII - With Errors` | B17C fit on the MOVE.3-extended record with regression measurement errors propagated as uncertain data. |

## Step-by-Step Walkthrough

### Opening the Project

1. Open RMC-BestFit 2.0.
2. Select **File > Open** and navigate to `examples/4-univariate-distribution-analysis/2-bulletin-17C-analysis/3-measurement-errors/`.
3. Open `sinnemahoning-move3-b17c.bestfit`.

### Exploring the Elements

For each B17C alternative:

1. Click the alternative in the Project Explorer.
2. Open the **Frequency** tab — the central LP-III curve plus the chosen confidence intervals are shown.
3. Switch the confidence-interval type in the Properties panel between **MVN** and **BCB** (Bias-Corrected Bootstrap) to compare.
4. Use the **Information Expansion** Properties pane to add or remove historical and paleoflood records.

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

![Frequency curve (AEP versus quantile) for each alternative, with the credible band.](images/sinnemahoning-b17c-frequency.png)
*Figure: Frequency curve (AEP versus quantile) for each alternative, with the credible band.*

![Posterior kernel density for each parameter.](images/sinnemahoning-b17c-kernel-density.png)
*Figure: Posterior kernel density for each parameter.*

![Markov-chain traces for each parameter.](images/sinnemahoning-b17c-trace.png)
*Figure: Markov-chain traces for each parameter.*

![Autocorrelation function of the chains, used to estimate effective sample size.](images/sinnemahoning-b17c-autocorrelation.png)
*Figure: Autocorrelation function of the chains, used to estimate effective sample size.*

### MCMC Diagnostics

Verify chain convergence before interpreting any results:

- **R-hat** — should be < 1.01 for every parameter.
- **Effective Sample Size (ESS)** — at least a few hundred per parameter.
- **Trace plots** — should look like fuzzy, well-mixed caterpillars (no drift, no sticking).
- **Posterior** — overall posterior log-likelihood should be visually stationary in the mean-likelihood plot.

## Next Steps

- Compare MVN-quantile and bias-corrected bootstrap (BCB) confidence intervals on the same dataset.
- Re-run on the same input data using the **Bayesian Univariate** workflow and compare AEPs and confidence intervals.
- Add a **regional skew** weighting if a regional skew estimate is available.

## References

<!-- Cite published case studies / source datasets here. Example format:

> Viglione, A., Merz, R., Salinas, J. L., & Bloeschl, G. (2013). Flood frequency hooves of a galloping horse. *Water Resources Research*, 49(2), 675-692.
-->

---

_This tutorial was generated from the project's `.bestfit` file metadata. The narrative and figure / table sections are placeholders — capture screenshots from the BestFit GUI and paste output tables to complete the guide._
