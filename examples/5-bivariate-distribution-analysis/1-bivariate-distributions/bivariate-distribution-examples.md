# bivariate-distribution-examples

## Overview

Bivariate distribution fitting using six copula families (AMH, Clayton, Frank, Gumbel, Joe, Normal) on synthetic paired data. Demonstrates marginal estimation followed by copula selection across the standard Archimedean and elliptical families.

## What's Inside

### Input Data

| Element | Description |
|---|---|
| `AMH - X Data` | Synthetic X-margin data for the AMH copula example. |
| `AMH - Y Data` | Synthetic Y-margin data for the AMH copula example. |
| `Clayton - X Data` | Synthetic X-margin data for the Clayton copula example. |
| `Clayton - Y Data` | Synthetic Y-margin data for the Clayton copula example. |
| `Frank - X Data` | Synthetic X-margin data for the Frank copula example. |
| `Frank - Y Data` | Synthetic Y-margin data for the Frank copula example. |
| `Gumbel - X Data` | Synthetic X-margin data for the Gumbel copula example. |
| `Gumbel - Y Data` | Synthetic Y-margin data for the Gumbel copula example. |
| `Joe - X Data` | Synthetic X-margin data for the Joe copula example. |
| `Joe - Y Data` | Synthetic Y-margin data for the Joe copula example. |
| `Normal - X Data` | Synthetic X-margin data for the Normal copula example. |
| `Normal - Y Data` | Synthetic Y-margin data for the Normal copula example. |

### Univariate Distribution

| Element | Description |
|---|---|
| `AMH - Marginal X` | Univariate fit of the X-margin for the AMH copula example. |
| `AMH - Marginal Y` | Univariate fit of the Y-margin for the AMH copula example. |
| `Clayton - Marginal X` | Univariate fit of the X-margin for the Clayton copula example. |
| `Clayton - Marginal Y` | Univariate fit of the Y-margin for the Clayton copula example. |
| `Frank - Marginal X` | Univariate fit of the X-margin for the Frank copula example. |
| `Frank - Marginal Y` | Univariate fit of the Y-margin for the Frank copula example. |
| `Gumbel - Marginal X` | Univariate fit of the X-margin for the Gumbel copula example. |
| `Gumbel - Marginal Y` | Univariate fit of the Y-margin for the Gumbel copula example. |
| `Joe - Marginal X` | Univariate fit of the X-margin for the Joe copula example. |
| `Joe - Marginal Y` | Univariate fit of the Y-margin for the Joe copula example. |
| `Normal - Marginal X` | Univariate fit of the X-margin for the Normal copula example. |
| `Normal - Marginal Y` | Univariate fit of the Y-margin for the Normal copula example. |

### Bivariate Distribution

| Element | Description |
|---|---|
| `AMH Copula` | Bivariate fit using the AMH (Ali-Mikhail-Haq) copula on the AMH marginals. |
| `Clayton Copula` | Bivariate fit using the Clayton copula on the Clayton marginals. |
| `Frank Copula` | Bivariate fit using the Frank copula on the Frank marginals. |
| `Gumbel Copula` | Bivariate fit using the Gumbel copula on the Gumbel marginals. |
| `Joe Copula` | Bivariate fit using the Joe copula on the Joe marginals. |
| `Normal Copula` | Bivariate fit using the Normal (Gaussian) copula on the Normal marginals. |

## Step-by-Step Walkthrough

### Opening the Project

1. Open RMC-BestFit 2.0.
2. Select **File > Open** and navigate to `examples/5-bivariate-distribution-analysis/1-bivariate-distributions/`.
3. Open `bivariate-distribution-examples.bestfit`.

### Exploring the Elements

For each Bivariate Distribution alternative:

1. Click the alternative in the Project Explorer.
2. Open the **Joint Density** tab to see the fitted copula contour overlay on the data scatter.
3. Inspect the dependence parameter (theta or rho) in the parameter table.
4. Compare AIC / BIC across copula families to select the best fit.

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

![Joint copula density contour overlaid on the X-Y scatter.](images/bivariate-joint-density.png)
*Figure: Joint copula density contour overlaid on the X-Y scatter.*

![Marginal X frequency curve.](images/bivariate-marginal-x.png)
*Figure: Marginal X frequency curve.*

![Marginal Y frequency curve.](images/bivariate-marginal-y.png)
*Figure: Marginal Y frequency curve.*

![Derived coincident-response frequency curve (CFA only).](images/bivariate-frequency.png)
*Figure: Derived coincident-response frequency curve (CFA only).*

### MCMC Diagnostics

Verify chain convergence before interpreting any results:

- **R-hat** — should be < 1.01 for every parameter.
- **Effective Sample Size (ESS)** — at least a few hundred per parameter.
- **Trace plots** — should look like fuzzy, well-mixed caterpillars (no drift, no sticking).
- **Posterior** — overall posterior log-likelihood should be visually stationary in the mean-likelihood plot.

## Next Steps

- Use the joint distribution to estimate **AND / OR / Kendall-return-period** quantiles.
- Compare copula families via AIC / BIC; the heavy-tail families (Gumbel, Joe) often win for hydrologic peaks.
- Pair with a **Coincident Frequency Analysis** to derive a sum / difference / max distribution.

## References

<!-- Cite published case studies / source datasets here. Example format:

> Viglione, A., Merz, R., Salinas, J. L., & Bloeschl, G. (2013). Flood frequency hooves of a galloping horse. *Water Resources Research*, 49(2), 675-692.
-->

---

_This tutorial was generated from the project's `.bestfit` file metadata. The narrative and figure / table sections are placeholders — capture screenshots from the BestFit GUI and paste output tables to complete the guide._
