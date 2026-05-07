# arr-flike-examples

## Overview

Bayesian replication of the Australian Rainfall and Runoff (ARR) flood-frequency worked examples. Demonstrates the LP-III distribution applied to systematic, censored, and historical flood records following the FLIKE software conventions.

## What's Inside

### Input Data

| Element | Description |
|---|---|
| `Example #3` | Hunter River at Singleton |
| `Example #4` | ARR Example #4 input data (peak-flow record from the Australian Rainfall and Runoff worked examples). |
| `Example #6a` | I do not have the source data for this gauge. I am using the table of flows provided in ARR for the Wimmera River at Glynwylin. The flows are provided in descending order, with no years. This is why the chronology plot looks odd. |
| `Example #6b` | I do not have the source data for this gauge. I am using the table of flows provided in ARR for the Wimmera River at Glynwylin. The flows are provided in descending order, with no years. This is why the chronology plot looks odd. |

### Univariate Distribution

| Element | Description |
|---|---|
| `Example #3` | Bayesian LP-III fit replicating ARR Example #3 (Hunter River at Singleton). |
| `Example #4` | Bayesian LP-III fit replicating ARR Example #4. |
| `Example #5` | Bayesian LP-III fit replicating ARR Example #5 (with low-outlier censoring). |
| `Example #6a` | Bayesian LP-III fit replicating ARR Example #6a (Wimmera River at Glynwylin, with historical record extension). |
| `Example #6b` | Bayesian LP-III fit replicating ARR Example #6b (Wimmera River, alternative historical interpretation). |

### Bulletin 17C

| Element | Description |
|---|---|
| `B17C - Example 5 - MVN` | B17C fit of ARR Example #5 using multivariate-normal quantile confidence intervals. |
| `B17C - Example 5 - Bootstrap` | B17C fit of ARR Example #5 using bias-corrected bootstrap confidence intervals. |
| `B17C - Example 3 - MVN` | B17C fit of ARR Example #3 using multivariate-normal quantile confidence intervals. |
| `B17C - Example 3 - Bootstrap` | B17C fit of ARR Example #3 using bias-corrected bootstrap confidence intervals. |
| `B17C - Example 5 - Bootstrap_copy` | Duplicate of the Example #5 bootstrap fit (kept for comparison; safe to delete). |

## Step-by-Step Walkthrough

### Opening the Project

1. Open RMC-BestFit 2.0.
2. Select **File > Open** and navigate to `examples/4-univariate-distribution-analysis/1-univariate-analysis/2-arr-flike/`.
3. Open `arr-flike-examples.bestfit`.

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

![Frequency curve (AEP versus quantile) for each alternative, with the credible band.](images/arr-flike-examples-frequency.png)
*Figure: Frequency curve (AEP versus quantile) for each alternative, with the credible band.*

![Posterior kernel density for each parameter.](images/arr-flike-examples-kernel-density.png)
*Figure: Posterior kernel density for each parameter.*

![Markov-chain traces for each parameter.](images/arr-flike-examples-trace.png)
*Figure: Markov-chain traces for each parameter.*

![Autocorrelation function of the chains, used to estimate effective sample size.](images/arr-flike-examples-autocorrelation.png)
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
