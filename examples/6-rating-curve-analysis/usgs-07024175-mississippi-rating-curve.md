# usgs-07024175-mississippi-rating-curve

## Overview

Stage-discharge rating curve for the Mississippi River at New Madrid, MO (USGS gage 07024175), fit using the piecewise power-law rating curve model with Bayesian MCMC.

## What's Inside

### Time Series Data

| Element | Description |
|---|---|
| `USGS 07024175 Measured Stage` | Field-measured (rated) gage height for the Mississippi River at New Madrid, MO (USGS gage 07024175). |
| `USGS 07024175 Measured Discharge` | Field-measured (rated) discharge for the Mississippi River at New Madrid, MO (USGS gage 07024175). |

### Rating Curve Analysis

| Element | Description |
|---|---|
| `USGS 07024175 Rating Curve` | Bayesian piecewise power-law rating-curve fit to the field-measured stage-discharge pairs at the Mississippi River at New Madrid, MO. |

## Step-by-Step Walkthrough

### Opening the Project

1. Open RMC-BestFit 2.0.
2. Select **File > Open** and navigate to `examples/6-rating-curve-analysis/`.
3. Open `usgs-07024175-mississippi-rating-curve.bestfit`.

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

![Stage-discharge rating curve fit, with measured pairs and Bayesian credible band.](images/mississippi-rating-curve.png)
*Figure: Stage-discharge rating curve fit, with measured pairs and Bayesian credible band.*

![Residuals of measured discharge minus rating-curve estimate, plotted against stage.](images/mississippi-residuals.png)
*Figure: Residuals of measured discharge minus rating-curve estimate, plotted against stage.*

![Markov-chain traces for the rating-curve parameters.](images/mississippi-trace.png)
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
