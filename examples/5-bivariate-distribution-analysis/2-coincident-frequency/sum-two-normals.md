# sum-two-normals

## Overview

Coincident frequency analysis (CFA) verification using the sum of two correlated normals. Tests the bivariate copula machinery against the closed-form analytical answer for three correlation coefficients (-0.5, 0, +0.5).

## What's Inside

### Input Data

| Element | Description |
|---|---|
| `X Data - Rho = 0.0` | Synthetic X-margin data with correlation rho = 0.0 between X and Y. |
| `Y Data - Rho = 0.0` | Synthetic Y-margin data with correlation rho = 0.0 between X and Y. |
| `X+Y Data - Rho = 0.0` | Synthetic sum X+Y for the rho = 0.0 case (closed-form Normal with combined variance). |
| `X Data - Rho = -0.5` | Synthetic X-margin data with correlation rho = -0.5 between X and Y. |
| `Y Data - Rho = -0.5` | Synthetic Y-margin data with correlation rho = -0.5 between X and Y. |
| `X+Y Data - Rho = -0.5` | Synthetic sum X+Y for the rho = -0.5 case (analytical comparison reference). |
| `X Data - Rho = +0.5` | Synthetic X-margin data with correlation rho = +0.5 between X and Y. |
| `Y Data - Rho = +0.5` | Synthetic Y-margin data with correlation rho = +0.5 between X and Y. |
| `X+Y Data - Rho = +0.5` | Synthetic sum X+Y for the rho = +0.5 case (analytical comparison reference). |

### Univariate Distribution

| Element | Description |
|---|---|
| `X Marginal - Rho = 0.0` | Univariate Normal fit of the X-margin (rho = 0.0 case). |
| `Y Marginal - Rho = 0.0` | Univariate Normal fit of the Y-margin (rho = 0.0 case). |
| `X Marginal - Rho = -0.5` | Univariate Normal fit of the X-margin (rho = -0.5 case). |
| `Y Marginal - Rho = -0.5` | Univariate Normal fit of the Y-margin (rho = -0.5 case). |
| `X Marginal - Rho = +0.5` | Univariate Normal fit of the X-margin (rho = +0.5 case). |
| `Y Marginal - Rho =+0.5` | Univariate Normal fit of the Y-margin (rho = +0.5 case). |

### Bivariate Distribution

| Element | Description |
|---|---|
| `Normal Copula - Rho = 0.0` | Normal copula fit with rho = 0.0 (independence baseline). |
| `Normal Copula - Rho = -0.5` | Normal copula fit with rho = -0.5 (negative correlation). |
| `Normal Copula - Rho =+0.5` | Normal copula fit with rho = +0.5 (positive correlation). |

### Coincident Frequency

| Element | Description |
|---|---|
| `CFA - Rho = 0.0` | Coincident frequency analysis using the rho = 0.0 Normal copula — should match the analytical X+Y distribution. |
| `CFA - Rho = -0.5` | Coincident frequency analysis using the rho = -0.5 Normal copula — should match the analytical X+Y distribution. |
| `CFA - Rho = +0.5` | Coincident frequency analysis using the rho = +0.5 Normal copula — should match the analytical X+Y distribution. |

## Step-by-Step Walkthrough

### Opening the Project

1. Open RMC-BestFit 2.0.
2. Select **File > Open** and navigate to `examples/5-bivariate-distribution-analysis/2-coincident-frequency/`.
3. Open `sum-two-normals.bestfit`.

### Exploring the Elements

For each Coincident Frequency Analysis alternative:

1. Click the alternative in the Project Explorer.
2. Open the **Joint Density** tab to view the bivariate fit.
3. Open the **Frequency** tab to view the derived response-variable AEP curve.
4. Adjust the **X / Y ordinates** in the Properties panel to refine the response surface.

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

![Joint copula density contour overlaid on the X-Y scatter.](images/sum-two-normals-joint-density.png)
*Figure: Joint copula density contour overlaid on the X-Y scatter.*

![Marginal X frequency curve.](images/sum-two-normals-marginal-x.png)
*Figure: Marginal X frequency curve.*

![Marginal Y frequency curve.](images/sum-two-normals-marginal-y.png)
*Figure: Marginal Y frequency curve.*

![Derived coincident-response frequency curve (CFA only).](images/sum-two-normals-frequency.png)
*Figure: Derived coincident-response frequency curve (CFA only).*

### MCMC Diagnostics

Verify chain convergence before interpreting any results:

- **R-hat** — should be < 1.01 for every parameter.
- **Effective Sample Size (ESS)** — at least a few hundred per parameter.
- **Trace plots** — should look like fuzzy, well-mixed caterpillars (no drift, no sticking).
- **Posterior** — overall posterior log-likelihood should be visually stationary in the mean-likelihood plot.

## Next Steps

- Use the joint AEP table to size structures whose response depends on two correlated drivers (e.g., coincident streamflow and downstream stage).
- Compare results against a closed-form analytical answer where one is available.

## References

<!-- Cite published case studies / source datasets here. Example format:

> Viglione, A., Merz, R., Salinas, J. L., & Bloeschl, G. (2013). Flood frequency hooves of a galloping horse. *Water Resources Research*, 49(2), 675-692.
-->

---

_This tutorial was generated from the project's `.bestfit` file metadata. The narrative and figure / table sections are placeholders — capture screenshots from the BestFit GUI and paste output tables to complete the guide._
