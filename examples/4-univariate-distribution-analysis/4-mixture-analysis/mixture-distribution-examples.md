# mixture-distribution-examples

## Overview

Mixture-distribution fitting on synthetic samples from two- and three-component normal mixtures, including a zero-inflated variant. Demonstrates Bayesian estimation of mixture weights, location, and scale parameters.

## What's Inside

### Input Data

| Element | Description |
|---|---|
| `Mixture of 2 Normals - Data` | Synthetic data from the mixture of two normal distributions. |
| `Mixture of 3 Normals - Data` | Synthetic data drawn from a three-component normal mixture. |
| `Mixture of 2 Normals and Zero Inflated - Data` | Synthetic data drawn from a two-component normal mixture with a zero-inflated component. |

### Mixture Distribution

| Element | Description |
|---|---|
| `Mixture Distribution - 2 Normals` | Bayesian fit of a two-component normal mixture to the corresponding synthetic sample. |
| `Mixture Distribution - 3 Normals` | Bayesian fit of a three-component normal mixture to the corresponding synthetic sample. |
| `Mixture Distribution - 2 Normals - Zero-Inflated` | Bayesian fit of a zero-inflated two-component normal mixture to the corresponding synthetic sample. |

## Step-by-Step Walkthrough

### Opening the Project

1. Open RMC-BestFit 2.0.
2. Select **File > Open** and navigate to `examples/4-univariate-distribution-analysis/4-mixture-analysis/`.
3. Open `mixture-distribution-examples.bestfit`.

### Exploring the Elements

For each Mixture Distribution alternative:

1. Click the alternative in the Project Explorer.
2. Open the **Frequency** tab to view the mixture AEP curve.
3. Open the **Kernel Density** tab to see the component contributions.
4. Inspect the component **weights** in the parameter table.

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

![Frequency curve (AEP versus quantile) for each alternative, with the credible band.](images/mixture-frequency.png)
*Figure: Frequency curve (AEP versus quantile) for each alternative, with the credible band.*

![Posterior kernel density for each parameter.](images/mixture-kernel-density.png)
*Figure: Posterior kernel density for each parameter.*

![Markov-chain traces for each parameter.](images/mixture-trace.png)
*Figure: Markov-chain traces for each parameter.*

![Autocorrelation function of the chains, used to estimate effective sample size.](images/mixture-autocorrelation.png)
*Figure: Autocorrelation function of the chains, used to estimate effective sample size.*

### MCMC Diagnostics

Verify chain convergence before interpreting any results:

- **R-hat** — should be < 1.01 for every parameter.
- **Effective Sample Size (ESS)** — at least a few hundred per parameter.
- **Trace plots** — should look like fuzzy, well-mixed caterpillars (no drift, no sticking).
- **Posterior** — overall posterior log-likelihood should be visually stationary in the mean-likelihood plot.

## Next Steps

- Compare 2-component versus 3-component mixtures via DIC / WAIC.
- Use a **zero-inflated** variant if the data has many true zeros (e.g., dry-day precipitation).
- Visualize component memberships via the kernel-density plot.

## References

<!-- Cite published case studies / source datasets here. Example format:

> Viglione, A., Merz, R., Salinas, J. L., & Bloeschl, G. (2013). Flood frequency hooves of a galloping horse. *Water Resources Research*, 49(2), 675-692.
-->

---

_This tutorial was generated from the project's `.bestfit` file metadata. The narrative and figure / table sections are placeholders — capture screenshots from the BestFit GUI and paste output tables to complete the guide._
