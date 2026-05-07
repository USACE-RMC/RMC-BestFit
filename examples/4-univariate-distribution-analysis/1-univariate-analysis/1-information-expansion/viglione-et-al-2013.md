# viglione-et-al-2013

## Overview

Replicates the systematic, temporal-expansion, and causal-information examples from Viglione et al. (2013). Compares Bayesian MCMC fits at the Kamp at Zwettl, Austria gage with and without quantile priors and historical record extensions.

## What's Inside

### Input Data

| Element | Description |
|---|---|
| `Systematic (1951-2001)` | Kamp at Zwettl, 1951-2001 |
| `Systematic (1951-2005)` | Kamp at Zwettl, 1951-2005 |
| `Systematic (1951-2001) + Temporal Expansion` | Kamp at Zwettl, 1951-2001 |
| `Systematic (1951-2005) + Temporal Expansion` | Kamp at Zwettl, 1951-2005 |

### Distribution Fitting Analysis

| Element | Description |
|---|---|
| `Fit - Systematic (1951-2001)` | Distribution-fitting comparison across LP-III, GEV, Gumbel, and other distributions on the 1951-2001 systematic record. |
| `Fit - Systematic (1951-2005)` | Distribution-fitting comparison on the 1951-2005 systematic record. |
| `Fit - Systematic (1951-2001) + Temporal Expansion` | Distribution-fitting comparison with the 1951-2001 record extended via historical / paleoflood data. |
| `Fit - Systematic (1951-2005) + Temporal Expansion` | Distribution-fitting comparison with the 1951-2005 record extended via historical / paleoflood data. |

### Univariate Distribution

| Element | Description |
|---|---|
| `MCMC - Systematic (1951-2001)` | Bayesian MCMC fit of the LP-III distribution using only the 1951-2001 systematic record. |
| `MCMC - Systematic (1951-2005)` | Bayesian MCMC fit using the 1951-2005 systematic record (includes the August 2002 flood). |
| `MCMC - Systematic (1951-2001) + Temporal` | Bayesian MCMC fit incorporating temporal information expansion (historical record extension). |
| `MCMC - Systematic (1951-2005) + Temporal` | Bayesian MCMC fit on the 1951-2005 record with temporal information expansion. |
| `MCMC - Systematic (1951-2001) + Causal` | Bayesian MCMC fit incorporating causal information expansion (atmospheric / hydrologic covariate prior). |
| `MCMC - Systematic (1951-2005) + Causal` | Bayesian MCMC fit on the 1951-2005 record with causal information expansion. |
| `MCMC - Systematic (1951-2001) + Temporal + Causal` | Bayesian MCMC fit combining temporal and causal information expansion on the 1951-2001 record. |
| `MCMC - Systematic (1951-2005) + Temporal + Causal` | Bayesian MCMC fit combining temporal and causal information expansion on the 1951-2005 record. |
| `MCMC - Systematic (1951-2001) + 3 Quantile Priors` | Bayesian MCMC fit using three quantile priors for engineering-judgement information expansion. |

## Step-by-Step Walkthrough

### Opening the Project

1. Open RMC-BestFit 2.0.
2. Select **File > Open** and navigate to `examples/4-univariate-distribution-analysis/1-univariate-analysis/1-information-expansion/`.
3. Open `viglione-et-al-2013.bestfit`.

### Exploring the Elements

For each Univariate Distribution alternative:

1. Click the alternative in the Project Explorer.
2. Open the **Frequency** tab to view the AEP-vs-quantile plot.
3. Open the **Markov Chain Trace** tab to confirm chain mixing (well-mixed traces look like fuzzy caterpillars).
4. Open the **Autocorrelation** tab to check effective sample size.
5. Inspect the **Properties** panel for sampler settings (iterations, warmup, point estimator, credible-interval width).

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

![Frequency curve (AEP versus quantile) for each alternative, with the credible band.](images/viglione-et-al-2013-frequency.png)
*Figure: Frequency curve (AEP versus quantile) for each alternative, with the credible band.*

![Posterior kernel density for each parameter.](images/viglione-et-al-2013-kernel-density.png)
*Figure: Posterior kernel density for each parameter.*

![Markov-chain traces for each parameter.](images/viglione-et-al-2013-trace.png)
*Figure: Markov-chain traces for each parameter.*

![Autocorrelation function of the chains, used to estimate effective sample size.](images/viglione-et-al-2013-autocorrelation.png)
*Figure: Autocorrelation function of the chains, used to estimate effective sample size.*

### MCMC Diagnostics

Verify chain convergence before interpreting any results:

- **R-hat** — should be < 1.01 for every parameter.
- **Effective Sample Size (ESS)** — at least a few hundred per parameter.
- **Trace plots** — should look like fuzzy, well-mixed caterpillars (no drift, no sticking).
- **Posterior** — overall posterior log-likelihood should be visually stationary in the mean-likelihood plot.

## Next Steps

- Compare alternatives via the **Bayesian Model Average** element to combine results from multiple distributions.
- Compute **return-period quantiles** (1%, 0.5%, 0.2% AEP) from the frequency-curve table.
- Re-run with informative **quantile priors** if engineering judgment suggests specific upper-bound flood magnitudes.
- Cross-check the LP-III fit against a **Bulletin 17C** fit on the same input data.

## References

<!-- Cite published case studies / source datasets here. Example format:

> Viglione, A., Merz, R., Salinas, J. L., & Bloeschl, G. (2013). Flood frequency hooves of a galloping horse. *Water Resources Research*, 49(2), 675-692.
-->

---

_This tutorial was generated from the project's `.bestfit` file metadata. The narrative and figure / table sections are placeholders — capture screenshots from the BestFit GUI and paste output tables to complete the guide._
