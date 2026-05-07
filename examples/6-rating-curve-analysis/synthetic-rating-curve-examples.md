# synthetic-rating-curve-examples

## Overview

Synthetic stage-discharge rating curve fits using one-, two-, and three-segment piecewise power-law models. Used to verify the rating-curve solver against ground-truth synthetic data.

## What's Inside

### Time Series Data

| Element | Description |
|---|---|
| `Stage Data` | Synthetic stage time series used to drive all three rating-curve verification cases. |
| `1 Segment - Flow Data` | Synthetic discharge time series generated from a known one-segment power-law rating curve. |
| `2 Segment - Flow Data` | Synthetic discharge time series generated from a known two-segment power-law rating curve. |
| `3 Segment - Flow Data` | Synthetic discharge time series generated from a known three-segment power-law rating curve. |

### Rating Curve Analysis

| Element | Description |
|---|---|
| `1 Segment Rating Curve` | One-segment power-law rating-curve fit on the corresponding synthetic data — should recover the ground-truth parameters. |
| `2 Segment Rating Curve` | Two-segment power-law rating-curve fit on the corresponding synthetic data — should recover the ground-truth parameters. |
| `3 Segment Rating Curve` | Three-segment power-law rating-curve fit on the corresponding synthetic data — should recover the ground-truth parameters. |

## Step-by-Step Walkthrough

### Opening the Project

1. Open RMC-BestFit 2.0.
2. Select **File > Open** and navigate to `examples/6-rating-curve-analysis/`.
3. Open `synthetic-rating-curve-examples.bestfit`.

### Exploring the Elements

For each Rating Curve Analysis alternative:

1. Click the alternative in the Project Explorer.
2. Open the **Rating Curve** tab to view the fit overlaid on the measured stage / discharge pairs.
3. Adjust **MinStage**, **MaxStage**, and **StageBins** in the Properties panel to set the prediction range.
4. Inspect breakpoints (h2, h3) for multi-segment fits.

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

![Stage-discharge rating curve fit, with measured pairs and Bayesian credible band.](images/synthetic-rc-rating-curve.png)
*Figure: Stage-discharge rating curve fit, with measured pairs and Bayesian credible band.*

![Residuals of measured discharge minus rating-curve estimate, plotted against stage.](images/synthetic-rc-residuals.png)
*Figure: Residuals of measured discharge minus rating-curve estimate, plotted against stage.*

![Markov-chain traces for the rating-curve parameters.](images/synthetic-rc-trace.png)
*Figure: Markov-chain traces for the rating-curve parameters.*

### MCMC Diagnostics

Verify chain convergence before interpreting any results:

- **R-hat** — should be < 1.01 for every parameter.
- **Effective Sample Size (ESS)** — at least a few hundred per parameter.
- **Trace plots** — should look like fuzzy, well-mixed caterpillars (no drift, no sticking).
- **Posterior** — overall posterior log-likelihood should be visually stationary in the mean-likelihood plot.

## Next Steps

- Use the Bayesian credible intervals to bound the rating curve at extreme stages.
- Apply the rating curve to a stage time series (Time Series Data element) to derive a discharge time series.
- Refit with **more segments** if structural breaks in the data are visible in the residual plot.

## References

<!-- Cite published case studies / source datasets here. Example format:

> Viglione, A., Merz, R., Salinas, J. L., & Bloeschl, G. (2013). Flood frequency hooves of a galloping horse. *Water Resources Research*, 49(2), 675-692.
-->

---

_This tutorial was generated from the project's `.bestfit` file metadata. The narrative and figure / table sections are placeholders — capture screenshots from the BestFit GUI and paste output tables to complete the guide._
