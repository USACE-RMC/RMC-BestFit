# blakely-mountain-dam-b17c

## Overview

B17C-method flood-frequency analysis for Blakely Mountain Dam, Arkansas. Demonstrates incorporating historical data, paleoflood records, regional skew, quantile penalties, and bias-corrected bootstrap confidence intervals using the Bulletin 17C method.

## What's Inside

### Input Data

| Element | Description |
|---|---|
| `Systematic - 1Day` | Systematic 1-day annual peak inflow record for Blakely Mountain Dam. |
| `Systematic + Historical - 1Day` | Systematic + historical 1-day annual peak inflow record for Blakely Mountain Dam. |
| `Systematic + Historical + Paleo - 1Day` | Systematic + historical + paleoflood 1-day annual peak inflow record for Blakely Mountain Dam. |

### Univariate Distribution

| Element | Description |
|---|---|
| `Univariate Analysis_8` | Bayesian LP-III fit for cross-comparison with the B17C alternatives — placeholder element. |

### Bulletin 17C

| Element | Description |
|---|---|
| `Systematic` | B17C fit using the systematic record only. |
| `Systematic + Historical` | B17C fit incorporating historical record extension. |
| `Systematic + Historical + Paleo` | B17C fit incorporating historical and paleoflood records. |
| `Systematic + Reg Skew` | B17C fit on the systematic record with regional skew weighting. |
| `Systematic + Historical + Reg Skew` | B17C fit with historical record + regional skew weighting. |
| `Systematic + Historical + Paleo + Reg Skew` | B17C fit with historical + paleoflood records + regional skew weighting. |
| `Systematic + Reg Skew_copy` | Duplicate of the systematic + regional-skew fit (kept for comparison; safe to delete). |
| `Systematic + Historical + Paleo + Reg Skew + QPen` | B17C fit with full information expansion + quantile-penalty prior. |
| `Sys + Hist + Paleo + SkewPen + QPen +BC` | B17C fit with full information expansion, skew penalty, quantile penalty, and bias-corrected bootstrap confidence intervals. |
| `Systematic - Linked MVT` | B17C fit on the systematic record with linked MVT (multivariate-t) confidence intervals. |
| `Systematic - MVN` | B17C fit on the systematic record with multivariate-normal confidence intervals. |
| `Systematic - Linked MVN` | B17C fit on the systematic record with linked multivariate-normal confidence intervals. |
| `Systematic + Reg Skew + QPen_copy` | Duplicate of the systematic + regional-skew + quantile-penalty fit (kept for comparison; safe to delete). |

## Step-by-Step Walkthrough

### Opening the Project

1. Open RMC-BestFit 2.0.
2. Select **File > Open** and navigate to `examples/4-univariate-distribution-analysis/2-bulletin-17C-analysis/2-information-expansion/`.
3. Open `blakely-mountain-dam-b17c.bestfit`.

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

![Frequency curve (AEP versus quantile) for each alternative, with the credible band.](images/blakely-b17c-frequency.png)
*Figure: Frequency curve (AEP versus quantile) for each alternative, with the credible band.*

![Posterior kernel density for each parameter.](images/blakely-b17c-kernel-density.png)
*Figure: Posterior kernel density for each parameter.*

![Markov-chain traces for each parameter.](images/blakely-b17c-trace.png)
*Figure: Markov-chain traces for each parameter.*

![Autocorrelation function of the chains, used to estimate effective sample size.](images/blakely-b17c-autocorrelation.png)
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
