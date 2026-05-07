# mixed-population-examples

## Overview

Composite-distribution analysis on a synthetic mixed-population annual maximum series (snow-driven and rainfall-driven floods). Demonstrates both the competing-risks formulation (max of two populations) and the AMS-mixture formulation.

## What's Inside

### Input Data

| Element | Description |
|---|---|
| `AMS Sub-Sample - Snow Driven Floods` | Sub-sampled annual maximum series of snow-driven floods (subset of the full POR record). |
| `Full POR - Snow Driven Floods` | 100 years of annual max snow-driven flood data. |
| `Full POR - Rainfall Driven Floods` | 100-years of annual max rain-driven data. |
| `Full POR - Annual Max Series` | The annual max of the rain and snow data. |
| `AMS Sub-Sample - Rain Driven Floods` | Sub-sampled annual maximum series of rainfall-driven floods (subset of the full POR record). |
| `AMS Mixture of Flood Types` | Pooled annual maximum series combining both snow-driven and rainfall-driven floods. |

### Univariate Distribution

| Element | Description |
|---|---|
| `Full POR Snow Driven` | Bayesian LP-III fit on the full POR snow-driven flood series. |
| `Full POR Rainfall Driven` | Bayesian LP-III fit on the full POR rainfall-driven flood series. |
| `Sub-Sample - Snow Driven` | Bayesian LP-III fit on the sub-sampled snow-driven AMS. |
| `Sub-Sample - Rain Driven` | Bayesian LP-III fit on the sub-sampled rain-driven AMS. |

### Composite Distribution

| Element | Description |
|---|---|
| `Competing Flood Types` | Composite distribution combining the two population fits via the competing-risks (max of two CDFs) formulation. |
| `Mixture of Flood Types` | Composite distribution combining the two population fits via the AMS-mixture (weighted-sum of CDFs) formulation. |

## Step-by-Step Walkthrough

### Opening the Project

1. Open RMC-BestFit 2.0.
2. Select **File > Open** and navigate to `examples/4-univariate-distribution-analysis/5-composite-analysis/`.
3. Open `mixed-population-examples.bestfit`.

### Exploring the Elements

For each Composite Distribution alternative:

1. Click the alternative in the Project Explorer.
2. Inspect the underlying univariate fits in the Properties panel.
3. Open the **Frequency** tab to view the combined AEP curve.
4. Switch between **competing-risks** and **AMS-mixture** formulations in the Properties panel.

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

![Frequency curve (AEP versus quantile) for each alternative, with the credible band.](images/mixed-population-frequency.png)
*Figure: Frequency curve (AEP versus quantile) for each alternative, with the credible band.*

![Posterior kernel density for each parameter.](images/mixed-population-kernel-density.png)
*Figure: Posterior kernel density for each parameter.*

![Markov-chain traces for each parameter.](images/mixed-population-trace.png)
*Figure: Markov-chain traces for each parameter.*

![Autocorrelation function of the chains, used to estimate effective sample size.](images/mixed-population-autocorrelation.png)
*Figure: Autocorrelation function of the chains, used to estimate effective sample size.*

### MCMC Diagnostics

Verify chain convergence before interpreting any results:

- **R-hat** — should be < 1.01 for every parameter.
- **Effective Sample Size (ESS)** — at least a few hundred per parameter.
- **Trace plots** — should look like fuzzy, well-mixed caterpillars (no drift, no sticking).
- **Posterior** — overall posterior log-likelihood should be visually stationary in the mean-likelihood plot.

## Next Steps

- Compare **competing-risks** (max of two CDFs) versus **AMS-mixture** (weighted CDFs) for combining sub-populations.
- Use the per-population sub-fits to inform engineering judgment about flood-type frequencies.

## References

<!-- Cite published case studies / source datasets here. Example format:

> Viglione, A., Merz, R., Salinas, J. L., & Bloeschl, G. (2013). Flood frequency hooves of a galloping horse. *Water Resources Research*, 49(2), 675-692.
-->

---

_This tutorial was generated from the project's `.bestfit` file metadata. The narrative and figure / table sections are placeholders — capture screenshots from the BestFit GUI and paste output tables to complete the guide._
